// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WakeUp;

// Quality records occupy the same owned groups as native captures. The stable
// slot is a selection, while its envelope binds the exact chosen representation.
// Owner==slot is the grouped index's durable-selection convention: ordinary
// quota and age reclamation must not remove the only saved user choice. Every
// member is protected, while explicit replacement/reset/clear remain permitted.
internal static class PreparationStorage
{
    internal static PreparationRecord? Read(OwnedCacheStore owner, GroupedTextureBlocks groups, string provider, string logical)
    {
        byte[]? bytes = owner.ReadExternal(() => groups.Read(PreparationContract.Slot(provider, logical)));
        if (bytes == null) return null;
        try
        {
            var record = PreparationContract.Decode(bytes);
            return record.Provider == provider && record.Logical == logical ? record : null;
        }
        catch (Exception e) when (e is IOException || e is ArgumentException || e is OverflowException) { return null; }
    }
    internal static bool Publish(OwnedCacheStore owner, GroupedTextureBlocks groups, PreparationRecord record, bool compress)
    {
        string key = PreparationContract.Slot(record.Provider, record.Logical);
        byte[] bytes = PreparationContract.Encode(record);
        return groups.Publish(owner, key, bytes, compress, ownerKey: key);
    }
    internal static bool Reset(OwnedCacheStore owner, GroupedTextureBlocks groups, string provider, string logical)
        => groups.Invalidate(owner, PreparationContract.Slot(provider, logical));
    internal static bool PublishGroup(OwnedCacheStore owner, GroupedTextureBlocks groups,
        IReadOnlyList<PreparationRecord> records, bool compress, CancellationToken cancellation)
    {
        if(records.Count==0 || records.Count>512) return false;
        long total=0;
        var encoded=new List<(string Key,byte[] Payload,string Owner)>();
        foreach(var record in records)
        {
            cancellation.ThrowIfCancellationRequested();
            byte[] payload=PreparationContract.Encode(record);
            if((total+=payload.Length)>GroupedTextureBlocks.MaximumEntry) return false;
            string key=PreparationContract.Slot(record.Provider,record.Logical);
            encoded.Add((key,payload,key));
        }
        return groups.PublishAtomic(owner,encoded,compress,cancellation);
    }
}

// Standalone preparation borrows no Unity code and owns the identical category
// lock. Existing exports/legacy data count against the shared budget but this
// facade does not erase or reinterpret them. Explicit global clear stays with
// the established in-game maintenance operation.
public sealed class PreparationChoiceSnapshot
{
    public string Provider { get; internal set; } = "";
    public string Logical { get; internal set; } = "";
    // Metadata only: the complete payload was verified, then released. This
    // record cannot be consumed or published because Output.Pixels is empty.
    public PreparationRecord? Choice { get; internal set; }
    public int PixelBytes { get; internal set; }
    public string Reason { get; internal set; } = "selection-missing";
    public string Description => Choice == null
        ? Reason == "selection-missing" ? "Saved: native quality (no override)" : "Saved selection unavailable: " + Reason
        : Describe(Choice);
    public static string Describe(PreparationRecord choice)
    {
        string preset = choice.Options.Preset == 1 ? choice.Role == "mask" ? "full-size mask" : "full-size lossy compression"
            : choice.Options.Preset == 2 ? "half dimensions" : "quarter dimensions";
        string filter = choice.Options.Filter == 0 ? "point" : choice.Options.Filter == 1 ? "bilinear" : "trilinear";
        return "Saved: " + preset + "; " + choice.Output.Width + "x" + choice.Output.Height + "; " + filter
            + "; mip bias " + choice.Options.MipBias.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + "; anisotropy " + choice.Options.Anisotropy;
    }
}

public sealed class PreparationStorageSession : IDisposable
{
    public const string ClearActionLabel = "Clear native cache, quality selections and DDS exports";
    public const string ClearDescription = "Clear removes all prepared native output, saved quality choices and DDS exports. Textures already loaded stay unchanged; the next launch uses ordinary native quality and sampling. Original artwork and authored DDS remain intact.";
    public const string ClearCompleted = "Native cache, quality selections and DDS exports cleared. Restart to use ordinary native quality and sampling. Original files remain intact.";
    public const int MaximumIndexReplacementBytes = GroupedTextureBlocks.MaximumIndexReplacementBytes;
    // New record data plus per-record/group metadata. A publication also needs
    // replacement-index space; it does not free old bytes before committing.
    public static long EstimateRecordStorageBytes(int recordBytes) => GroupedTextureBlocks.EstimateRecordStorageBytes(recordBytes);
    public static IReadOnlyList<PreparationChoiceSnapshot> Inspect(string saveDataRoot,
        IEnumerable<(string Provider, string Logical)> requests)
    {
        var requested = requests.Take(32769).ToArray();
        if (requested.Length > 32768) throw new ArgumentException("quality-preview-selection-bound");
        var result = requested.Select(r => new PreparationChoiceSnapshot { Provider = r.Provider, Logical = r.Logical }).ToArray();
        if (result.Length == 0) return result;
        string wakeRoot = Path.GetFullPath(Path.Combine(saveDataRoot, "WakeUp"));
        string root = Path.Combine(wakeRoot, "PreparedTextures", "v1");
        FileStream? budgetLease = null, categoryLease = null;
        try
        {
            OwnedCacheStore.RejectLinkedPath(root);
            if (!Directory.Exists(root)) return result;
            var existingBudget = SharedCacheBudget.ForRoot(root);
            if (existingBudget != null) return existingBudget.ReadIdleCategory(root, ReadChoices);
            string budgetLock = Path.Combine(wakeRoot, ".budget-owner"), categoryLock = Path.Combine(root, ".owner");
            OwnedCacheStore.RejectLinkedPath(budgetLock); OwnedCacheStore.RejectLinkedPath(categoryLock);
            // Never create even a lock file during a dry run. Older category-only
            // stores have no budget lock; their existing category lock suffices.
            if (File.Exists(budgetLock)) budgetLease = new FileStream(budgetLock, FileMode.Open, FileAccess.Read, FileShare.None);
            categoryLease = new FileStream(categoryLock, FileMode.Open, FileAccess.Read, FileShare.None);
            return ReadChoices();
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is OverflowException)
        { foreach (var item in result) { item.Choice = null; item.PixelBytes = 0; item.Reason = "selection-locked-or-unavailable: " + e.Message; } }
        finally { categoryLease?.Dispose(); budgetLease?.Dispose(); }
        return result;

        PreparationChoiceSnapshot[] ReadChoices()
        {
            var blocks = new GroupedTextureBlocks(Path.Combine(root, "groups"));
            blocks.InitializeReadOnly();
            foreach (var item in result)
            {
                byte[]? bytes = blocks.Read(PreparationContract.Slot(item.Provider, item.Logical));
                if (bytes == null) { item.Reason = blocks.LastReason == "group-missing" ? "selection-missing" : blocks.LastReason; continue; }
                try
                {
                    var record = PreparationContract.Decode(bytes);
                    if (record.Provider != item.Provider || record.Logical != item.Logical) throw new InvalidDataException("quality-preview-slot-mismatch");
                    item.PixelBytes = record.Output.Pixels.Length;
                    record.Output.Pixels = Array.Empty<byte>(); item.Choice = record; item.Reason = "completed-selection";
                }
                catch (Exception e) when (e is IOException || e is ArgumentException || e is OverflowException)
                { item.Reason = "selection-invalid: " + e.Message; }
            }
            return result;
        }
    }
    private readonly SharedCacheBudget? budget;
    private readonly OwnedCacheStore owner;
    private readonly GroupedTextureBlocks groups;
    private readonly bool compress;
    private long exportBytes;
    public long StoredBytes => owner.StoredBytes;
    public long SharedStoredBytes => budget?.StoredBytes ?? 0;
    public int SelectedQualityCount => groups.SelectedQualityCount;
    // Encoded complete records, not an estimate of physical disk/GPU usage.
    public long SelectedQualityRecordBytes => groups.SelectedQualityRecordBytes;
    public string LastReason => groups.LastReason == "none" ? owner.LastReason : groups.LastReason;
    public PreparationStorageSession(string saveDataRoot, int maximumMiB = 1024, bool compress = false, string action = "normal")
    {
        if (maximumMiB < 1 || maximumMiB > 65536) throw new ArgumentOutOfRangeException(nameof(maximumMiB));
        if (action != "normal" && action != "rebuild" && action != "bypass") throw new ArgumentException("preparation-storage-action");
        CacheLaunchPolicy? selected = null;
        var policy = CacheLaunchPolicy.Latch(ref selected, action, () => { });
        string root = Path.Combine(saveDataRoot, "WakeUp", "PreparedTextures", "v1");
        groups = new GroupedTextureBlocks(Path.Combine(root, "groups"));
        this.compress = compress;
        try
        {
            if (policy.Action != CacheAction.Bypass) budget = new SharedCacheBudget(saveDataRoot, maximumMiB * 1024L * 1024);
            owner = new OwnedCacheStore(root, 8 * 1024 * 1024 - OwnedCacheStore.EnvelopeBytes,
                maximumMiB * 1024L * 1024, policy, () => checked(groups.StoredBytes + exportBytes), null,
                () => { InventoryExports(Path.Combine(root, "exports")); groups.Initialize(); }, budget);
        }
        catch { budget?.Dispose(); throw; }
    }
    private void InventoryExports(string root)
    {
        OwnedCacheStore.RejectLinkedPath(root);
        if (!Directory.Exists(root)) return;
        int count = 0;
        foreach (string path in Directory.EnumerateFileSystemEntries(root))
        {
            if (++count > 32768) throw new IOException("export-inventory-limit");
            OwnedCacheStore.RejectLinkedPath(path);
            if (Directory.Exists(path)) throw new IOException("export-unexpected-directory");
            exportBytes = checked(exportBytes + new FileInfo(path).Length);
        }
    }
    public PreparationRecord? Read(string provider, string logical) => PreparationStorage.Read(owner, groups, provider, logical);
    public bool Publish(PreparationRecord record) => PreparationStorage.Publish(owner, groups, record, compress);
    public bool PublishGroup(IReadOnlyList<PreparationRecord> records, CancellationToken cancellation = default)
        => PreparationStorage.PublishGroup(owner, groups, records, compress, cancellation);
    public bool Reset(string provider, string logical) => PreparationStorage.Reset(owner, groups, provider, logical);
    public void Complete() { groups.Complete(owner); owner.Complete(); }
    public void Dispose() { owner.Dispose(); budget?.Dispose(); }
}
