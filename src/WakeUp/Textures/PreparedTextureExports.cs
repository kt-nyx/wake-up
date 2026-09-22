// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WakeUp;

// Reusable DDS files are owned prepared output, never a second native-cache
// entry. All methods run under PreparedTextureStore's one OwnedCacheStore lock.
// A readable text manifest is the atomic commit point for a digest-named DDS.
internal sealed class PreparedTextureExports
{
    private const int InventoryLimit = 32768;
    private const int MaximumManifest = 32 * 1024;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly Dictionary<string, long> files = new(StringComparer.Ordinal);
    internal string Root { get; }
    private long storedBytes;
    private bool accountingFailed;
    internal long StoredBytes => accountingFailed ? long.MaxValue / 4 : storedBytes;
    internal string LastReason { get; private set; } = "none";

    internal PreparedTextureExports(string root) => Root = root;

    internal void Initialize()
    {
        OwnedCacheStore.RejectLinkedPath(Root);
        if (!Directory.Exists(Root)) return;
        foreach (string path in Directory.EnumerateFileSystemEntries(Root))
        {
            if (files.Count >= InventoryLimit) throw new IOException("export-inventory-limit");
            OwnedCacheStore.RejectLinkedPath(path);
            if (Directory.Exists(path)) throw new IOException("export-unexpected-directory");
            string name = Path.GetFileName(path);
            files.Add(name, new FileInfo(path).Length);
            storedBytes += files[name];
        }
        foreach (string name in files.Keys.Where(IsPending).ToArray()) Remove(name);
        // An interrupted publication may have installed the DDS but not its
        // mapping. Reclaim only recognized unreferenced owned DDS files.
        foreach (string name in files.Keys.Where(IsDdsName).ToArray())
        {
            string manifest = name.Substring(0, 64) + ".manifest";
            try { if (ReadManifest(manifest)[3] == name) continue; }
            catch (Exception e) when (IsStorageError(e)) { }
            Remove(name);
        }
    }

    internal void Clear()
    {
        foreach (string name in files.Keys.Where(n => IsPending(n) || IsDdsName(n) || IsManifestName(n)).ToArray()) Remove(name);
    }

    internal byte[]? Read(string identity)
    {
        string? key = IdentityKey(identity);
        if (key == null) { LastReason = "export-identity"; return null; }
        string manifest = key + ".manifest";
        if (!files.ContainsKey(manifest)) { LastReason = "export-missing"; return null; }
        try
        {
            string[] fields = ReadManifest(manifest);
            if (Utf8.GetString(Convert.FromBase64String(fields[1])) != identity) throw new InvalidDataException("export-identity");
            string logical = fields[2];
            if (!ValidLogical(logical) || !IsDdsName(fields[3]) || !fields[3].StartsWith(key + ".", StringComparison.Ordinal))
                throw new InvalidDataException("export-mapping");
            string path = Path.Combine(Root, fields[3]);
            OwnedCacheStore.RejectLinkedPath(path);
            Track(manifest);
            byte[] dds = ReadBounded(path, PreparedTextureStore.MaximumEntry - checked((int)files[manifest]));
            if (key + "." + PngCache.Hex(PngCache.Hash(dds)) + ".dds" != fields[3]) throw new InvalidDataException("export-digest");
            PreparedDds.Parse(dds);
            LastReason = "export-hit";
            return dds;
        }
        catch (Exception e) when (IsStorageError(e))
        {
            LastReason = e is InvalidDataException ? e.Message : "export-read-io";
            Remove(manifest);
            foreach (string name in files.Keys.Where(n => IsDdsName(n) && n.StartsWith(key + ".", StringComparison.Ordinal)).ToArray()) Remove(name);
            return null;
        }
    }

    internal bool Publish(OwnedCacheStore owner, string identity, string logical, byte[] dds)
    {
        string? key = IdentityKey(identity);
        if (key == null || !ValidLogical(logical) || dds == null) { LastReason = "export-identity-or-path"; return false; }
        try
        {
            PreparedDds.Parse(dds);
            string name = key + "." + PngCache.Hex(PngCache.Hash(dds)) + ".dds";
            byte[] manifest = Utf8.GetBytes("WakeUp DDS export v1\n" + Convert.ToBase64String(Utf8.GetBytes(identity)) + "\n"
                + logical + "\n" + name + "\n");
            if (manifest.Length > MaximumManifest || (long)dds.Length + manifest.Length > PreparedTextureStore.MaximumEntry)
            { LastReason = "export-complete-entry-size"; return false; }
            bool result = owner.PublishExternal((long)dds.Length + manifest.Length, () =>
            {
                if (files.Count + 4 > InventoryLimit) { LastReason = "export-inventory-limit"; return false; }
                OwnedCacheStore.RejectLinkedPath(Root);
                Directory.CreateDirectory(Root);
                PublishFile(key, name, dds);
                PublishFile(key, key + ".manifest", manifest);
                foreach (string old in files.Keys.Where(n => IsDdsName(n) && n.StartsWith(key + ".", StringComparison.Ordinal) && n != name).ToArray()) Remove(old);
                LastReason = "export-published";
                return true;
            });
            if (!result && owner.LastReason != "external-refused") LastReason = "export-" + owner.LastReason;
            return result;
        }
        catch (Exception e) when (IsStorageError(e))
        { LastReason = e is InvalidDataException ? e.Message : "export-write-io"; return false; }
    }

    private void PublishFile(string key, string name, byte[] bytes)
    {
        string pending = key + "." + Guid.NewGuid().ToString("N") + ".pending";
        string path = Path.Combine(Root, pending);
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            Track(pending);
            string destination = Path.Combine(Root, name);
            OwnedCacheStore.RejectLinkedPath(destination);
            if (File.Exists(destination)) File.Replace(path, destination, null); else File.Move(path, destination);
            Forget(pending); Track(name);
        }
        finally
        {
            // Failed writes and cleanup remain visible to subsequent capacity
            // checks. A new process reclaims recognized interrupted output.
            if (File.Exists(path)) { Track(pending); Remove(pending); }
        }
    }

    private string[] ReadManifest(string name)
    {
        string path = Path.Combine(Root, name);
        OwnedCacheStore.RejectLinkedPath(path);
        string[] fields = Utf8.GetString(ReadBounded(path, MaximumManifest)).Split('\n');
        if (fields.Length != 5 || fields[0] != "WakeUp DDS export v1" || fields[4] != "") throw new InvalidDataException("export-manifest-version");
        return fields;
    }

    private static string? IdentityKey(string identity)
    {
        try
        {
            if (string.IsNullOrEmpty(identity) || identity.Length > PreparedTextureStore.MaximumIdentityBytes
                || Utf8.GetByteCount(identity) > PreparedTextureStore.MaximumIdentityBytes) return null;
            return PngCache.Hex(PngCache.Hash(Utf8.GetBytes(identity)));
        }
        catch (EncoderFallbackException) { return null; }
    }

    private static bool ValidLogical(string logical) => !string.IsNullOrEmpty(logical) && logical.Length <= 4096
        && !logical.Any(c => char.IsControl(c) || c == ':' || c == '\\') && !Path.IsPathRooted(logical)
        && logical.Split('/').All(part => part.Length > 0 && part != "." && part != "..");
    private static bool IsManifestName(string name) => name.Length == 73 && name.EndsWith(".manifest", StringComparison.Ordinal) && OwnedCacheStore.IsKey(name.Substring(0, 64));
    private static bool IsDdsName(string name) => name.Length == 133 && name[64] == '.' && name.EndsWith(".dds", StringComparison.Ordinal)
        && OwnedCacheStore.IsKey(name.Substring(0, 64)) && OwnedCacheStore.IsKey(name.Substring(65, 64));
    private static bool IsPending(string name) => name.Length == 105 && name[64] == '.' && name.EndsWith(".pending", StringComparison.Ordinal)
        && OwnedCacheStore.IsKey(name.Substring(0, 64)) && Guid.TryParseExact(name.Substring(65, 32), "N", out _);
    private void Track(string name)
    {
        try
        {
            string path = Path.Combine(Root, name);
            OwnedCacheStore.RejectLinkedPath(path);
            long length = new FileInfo(path).Length;
            Forget(name); files[name] = length; storedBytes += length;
        }
        catch (Exception e) when (IsStorageError(e)) { accountingFailed = true; throw; }
    }
    private void Forget(string name) { if (files.TryGetValue(name, out long length)) { storedBytes -= length; files.Remove(name); } }
    private void Remove(string name)
    {
        try { string path = Path.Combine(Root, name); OwnedCacheStore.RejectLinkedPath(path); File.Delete(path); Forget(name); }
        catch (Exception e) when (IsStorageError(e)) { LastReason = "export-cleanup-io"; }
    }
    private static bool IsStorageError(Exception e) => e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException
        || e is NotSupportedException || e is FormatException || e is OverflowException;

    private static byte[] ReadBounded(string path, int maximum)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length < 1 || file.Length > maximum) throw new InvalidDataException("export-size");
        using var reader = new BinaryReader(file);
        byte[] bytes = reader.ReadBytes(checked((int)file.Length));
        if (bytes.Length != file.Length) throw new InvalidDataException("export-truncated");
        return bytes;
    }
}
