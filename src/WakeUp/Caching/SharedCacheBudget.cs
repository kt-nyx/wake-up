// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WakeUp;

// The production stores share one disk ceiling. Inventory runs at launch
// and category opening, never per entry. Existing category file locks protect
// unopened data too; attached stores borrow those locks for the whole launch.
internal sealed class SharedCacheBudget : IDisposable
{
    internal const int DefaultMiB = 1024;
    private const int InventoryLimit = 100000;
    private static SharedCacheBudget? current;
    private static string? failedRoot;
    private readonly Dictionary<string, Category> categories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileStream> owners = new();
    private bool disposed;
    private long reserved;
    internal object Gate { get; } = new();
    internal long MaximumBytes { get; }
    internal long StoredBytes { get { lock (Gate) return categories.Values.Sum(c => c.OtherBytes + (c.Usage?.Invoke() ?? 0)); } }
    private sealed class Category
    {
        internal string InventoryRoot = "";
        internal long OtherBytes;
        internal Func<long>? Usage;
        internal bool Active;
    }

    internal static void Initialize(string saveDataRoot, int totalMiB, CacheLaunchPolicy policy)
    {
        if (current != null || policy.Action == CacheAction.Bypass) return;
        if (totalMiB < 1 || totalMiB > 65536) throw new ArgumentOutOfRangeException(nameof(totalMiB));
        failedRoot = Path.GetFullPath(Path.Combine(saveDataRoot, "WakeUp")) + Path.DirectorySeparatorChar;
        current = new SharedCacheBudget(saveDataRoot, totalMiB * 1024L * 1024);
        failedRoot = null;
    }

    internal static SharedCacheBudget? ForRoot(string root)
    {
        string full = Path.GetFullPath(root);
        // A failed ownership/inventory attempt must never silently restore the
        // old independent per-category write allowances for this launch.
        if (failedRoot != null && full.StartsWith(failedRoot, StringComparison.OrdinalIgnoreCase))
            throw new IOException("shared-cache-unavailable");
        return current != null && current.categories.ContainsKey(full) ? current : null;
    }

    internal SharedCacheBudget(string saveDataRoot, long maximumBytes)
    {
        if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        MaximumBytes = maximumBytes;
        string root = Path.GetFullPath(Path.Combine(saveDataRoot, "WakeUp"));
        try
        {
            Own(root, ".budget-owner");
            foreach (string name in new[] { "PngCache", "PreparedTextures", "PreparedAudio", "StaticAtlases", "ParsedXml", "ProcessedXml", "ResolvedInheritance", "ParsedLanguage" })
            {
                string inventoryRoot = Path.Combine(root, name);
                string storeRoot = Path.Combine(inventoryRoot, "v1");
                Own(storeRoot, ".owner");
                categories.Add(storeRoot, new Category { InventoryRoot = inventoryRoot, OtherBytes = Inventory(inventoryRoot) });
            }
        }
        catch { Dispose(); throw; }
    }

    private void Own(string root, string name)
    {
        OwnedCacheStore.RejectLinkedPath(root);
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, name);
        OwnedCacheStore.RejectLinkedPath(path);
        var owner = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        owners.Add(owner);
        owner.SetLength(0);
    }

    private static long Inventory(string root)
    {
        long total = 0;
        int count = 0;
        var directories = new Stack<string>();
        directories.Push(root);
        while (directories.Count != 0)
        {
            string directory = directories.Pop();
            OwnedCacheStore.RejectLinkedPath(directory);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++count > InventoryLimit) throw new IOException("shared-cache-inventory-limit");
                OwnedCacheStore.RejectLinkedPath(path);
                if (Directory.Exists(path)) directories.Push(path);
                else total = checked(total + new FileInfo(path).Length);
            }
        }
        return total;
    }

    internal void Attach(string root)
    {
        lock (Gate)
        {
            if (disposed || !categories.TryGetValue(root, out var category)) throw new IOException("shared-cache-category");
            if (category.Active) throw new IOException("shared-cache-category-owned");
            category.Active = true;
        }
    }

    // A menu preview can borrow this launch's already-held file locks without
    // opening a mutable cache. Reject an attached store, including reentrant
    // calls on this thread, so its uncommitted changes are never previewed.
    internal T ReadIdleCategory<T>(string root, Func<T> read)
    {
        lock (Gate)
        {
            if (disposed || !categories.TryGetValue(root, out var category)) throw new IOException("shared-cache-category-unavailable");
            if (category.Active) throw new IOException("shared-cache-category-owned");
            return read();
        }
    }

    internal void Refresh(string root, Func<long> usage)
    {
        lock (Gate)
        {
            var category = categories[root];
            // Unknown files, old versions and legacy PNG files count, but are
            // not silently deleted by another producer's quota enforcement.
            category.OtherBytes = Math.Max(0, Inventory(category.InventoryRoot) - usage());
            category.Usage = usage;
        }
    }

    internal void Detach(string root)
    {
        lock (Gate)
        {
            var category = categories[root];
            category.OtherBytes += category.Usage?.Invoke() ?? 0;
            category.Usage = null;
            category.Active = false;
        }
    }

    internal bool Fits(long incoming)
    {
        lock (Gate)
        {
            long stored = StoredBytes;
            return !disposed && incoming >= 0 && stored <= MaximumBytes && incoming <= MaximumBytes - stored - reserved;
        }
    }

    // Hold the shared monitor during the write as well. Reservations also make
    // a nested writer callback account for the outer writer's temporary bytes.
    internal IDisposable Reserve(long bytes)
    {
        if (!Fits(bytes)) throw new IOException("shared-cache-quota");
        reserved += bytes;
        return new Reservation(this, bytes);
    }

    private sealed class Reservation : IDisposable
    {
        private SharedCacheBudget? owner;
        private readonly long bytes;
        internal Reservation(SharedCacheBudget owner, long bytes) { this.owner = owner; this.bytes = bytes; }
        public void Dispose() { if (owner == null) return; owner.reserved -= bytes; owner = null; }
    }

    public void Dispose()
    {
        lock (Gate)
        {
            if (disposed) return;
            disposed = true;
            foreach (var owner in owners) owner.Dispose();
            owners.Clear();
        }
    }
}
