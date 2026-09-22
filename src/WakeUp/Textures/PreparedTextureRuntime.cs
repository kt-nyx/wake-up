// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Verse;

namespace WakeUp;

// A selection refers to a provider's own holder, not only ContentFinder's global
// winner. Future deferred loading must revalidate and use this same resolver.
internal static class PreparedTextureRuntime
{
    internal static bool UseAtStartup { get; private set; }
    internal static string SaveRoot = "", ModRoot = "";
    private static PreparedTextureStore? startupStore;
    internal static bool CompressStorage { get; private set; }
    internal static bool PsdSupport { get; private set; }
    internal static PreparedTextureStore StartupStore => startupStore ??= new PreparedTextureStore(SaveRoot, null, null, CompressStorage, batched: true);
    internal static long Hits, Misses, Refused;
    internal static long SourceSnapshotTicks, IdentityTicks;
    internal static string Status = "Not selected for this launch";
    internal static void Initialize(string saveRoot, string modRoot, IReadOnlyList<string> args)
    {
        SaveRoot = saveRoot; ModRoot = modRoot;
        PsdSupport = StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
            && args.Count(x => x.StartsWith("--wake-up-psd=", StringComparison.Ordinal)) == 1 && args.Contains("--wake-up-psd=merged");
        CompressStorage = args.Count(x => x.StartsWith("--wake-up-texture-storage=", StringComparison.Ordinal)) == 1
            && args.Contains("--wake-up-texture-storage=deflate");
        var selected = args.Where(x => x.StartsWith("--wake-up-prepared=", StringComparison.Ordinal)).ToArray();
        UseAtStartup = StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate && selected.Length == 1
            && selected[0] == "--wake-up-prepared=0";
        Status = UseAtStartup ? "Selected; waiting for the guarded texture loader" : "Not selected for this launch";
    }
    internal static string Frame(params string[] parts)
    {
        using var bytes = new MemoryStream();
        using (var w = new BinaryWriter(bytes, Encoding.UTF8, true)) foreach (var part in parts) w.Write(part);
        return PngCache.Hex(PngCache.Hash(bytes.ToArray()));
    }
    internal static string ActiveIdentity() => Frame(LoadedModManager.RunningModsListForReading.Select((m, i) =>
        Frame(i.ToString(System.Globalization.CultureInfo.InvariantCulture), m.PackageId, Path.GetFullPath(m.RootDir),
            Frame(m.foldersToLoadDescendingOrder.Select(Path.GetFullPath).ToArray()))).ToArray());
    internal static string SelectionIdentity(ModContentPack mod, IEnumerable<KeyValuePair<string, FileInfo>> selected)
        => Frame("native-selection-v2", mod.PackageId, Path.GetFullPath(mod.RootDir),
            Frame(mod.foldersToLoadDescendingOrder.Select(Path.GetFullPath).ToArray()));
    internal static string OwnerIdentity(string selection, string logicalPath, int choice)
        => Frame("native-slot", selection, logicalPath, choice.ToString(System.Globalization.CultureInfo.InvariantCulture));
    internal static string Identity(string selection, string logicalPath, string sourcePath, byte[] source,
        int choice, string platform, string helper)
    {
        long started = Stopwatch.GetTimestamp();
        try { return IdentityCore(selection, logicalPath, sourcePath, source, choice, platform, helper); }
        finally { Interlocked.Add(ref IdentityTicks, Stopwatch.GetTimestamp() - started); }
    }
    private static string IdentityCore(string selection, string logicalPath, string sourcePath, byte[] source,
        int choice, string platform, string helper) => "prepared-v3:" + OwnerIdentity(selection, logicalPath, choice) + ":" + string.Concat(new[] { selection, logicalPath, Path.GetFullPath(sourcePath),
            PngCache.Hex(PngCache.Hash(source)), choice.ToString(System.Globalization.CultureInfo.InvariantCulture), platform,
            choice == 0 ? "native-c05" : helper, choice == 0 ? (sourcePath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ? PsdTextureRuntime.Contract : "native-output-c05-v2") : "sRGB-straight-alpha-linear-box-bottom-up-full-mips-v1|" + PreparedTextureEligibility.Contract + "|" + PreparedColorRole.Identity }
            .Select(value => value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value));
    internal const int MaximumNativeSource = 96 * 1024 * 1024;
    // All selection/platform/path values are captured by the ordered owner.
    // Workers supply only the full leased source bytes to a fixed SHA-256 path.
    internal static Func<byte[], string> NativeIdentityReader(string owner, string selection, string logical, string path, string platform)
    {
        string canonical = Path.GetFullPath(path);
        string contract = path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ? PsdTextureRuntime.Contract : "native-output-c05-v2";
        return source => "prepared-v3:" + owner + ":" + string.Concat(new[] {
            selection, logical, canonical, PngCache.Hex(CacheDigest.HashInput(source)), "0", platform, "native-c05", contract
        }.Select(value => value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value));
    }
    internal static byte[] ReadSource(FileInfo file)
    {
        using var input = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadSourceSnapshot(input);
    }
    internal static byte[] ReadSourceSnapshot(Stream input)
    {
        long started = Stopwatch.GetTimestamp();
        try { return ReadSourceSnapshotCore(input); }
        finally { Interlocked.Add(ref SourceSnapshotTicks, Stopwatch.GetTimestamp() - started); }
    }
    private static byte[] ReadSourceSnapshotCore(Stream input)
    {
        if (input.Length < 1 || input.Length > MaximumNativeSource) throw new InvalidDataException("source-size");
        var bytes = new byte[(int)input.Length]; int offset = 0, count;
        while (offset < bytes.Length && (count = input.Read(bytes, offset, bytes.Length - offset)) > 0) offset += count;
        if (offset != bytes.Length || input.ReadByte() != -1) throw new InvalidDataException("source-changed");
        return bytes;
    }
    internal static Texture2D? TryResolve(ModContentPack provider, string logicalPath, FileInfo file, string selection)
    {
        // Faithful authored-DDS recaching is retired. Explicit C08 quality is
        // applied separately after native publication and remains available.
        if (file.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase)) return null;
        if (!UseAtStartup || !CacheLaunchPolicy.Current.AllowRead) return null;
        try
        {
            // Explicit quality is installed only after complete native groups
            // and their callbacks finish; native preparation remains an early
            // producer-equivalent reuse path.
            // An absent provider/logical slot cannot match any source version.
            // Presence is only admission: exact source/platform identity and
            // descriptor validation below still decide whether reuse is safe.
            if (!StartupStore.HasOwner(OwnerIdentity(selection, logicalPath, 0)))
            { Misses++; Status = "No matching native output; ordinary loading"; return null; }
            string platform = PngRuntime.PreparationPlatform();
            if (file.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && !NativeDdsCapture.Compatible) return null;
            byte[] source = ReadSource(file);
            // The native fallback has a separate identity from explicit quality
            // overrides admitted by the complete-group resolver above.
            string identity = Identity(selection, logicalPath, file.FullName, source, 0, platform, "");
            var entry = StartupStore.Read(identity);
            if (entry == null) { Misses++; Status = "No matching native output; ordinary loading"; return null; }
            var texture = PngRuntime.Restore(entry);
            texture.name = Path.GetFileNameWithoutExtension(file.Name);
            // Prepared output owns this exact native representation. A failed
            // restore never removes the automatic fallback.
            // Automatic capture and preparation now own this same entry.
            Hits++; Status = "Native prepared output used";
            return texture;
        }
        catch (Exception e) { Refused++; Status = "Prepared output refused: " + e.Message; return null; }
    }
    internal static void FinishStartup()
    {
        PreparedQualityRuntime.Finish();
        if (UseAtStartup && Hits == 0 && Misses == 0 && Refused == 0)
            Status = "Selected but no prepared requests reached the guarded loader";
        UseAtStartup = false;
        try { startupStore?.Complete(); } finally { startupStore?.Dispose(); startupStore = null; }
    }
    internal static bool Revalidate(ModContentPack mod, string logical, string physical, string selection, byte[] original, int choice = 0, PreparedTextureEligibility? eligibility = null)
    {
        var selected = PngRuntime.SelectedFiles(mod).ToArray();
        return LoadedModManager.RunningModsListForReading.Contains(mod) && SelectionIdentity(mod, selected) == selection
            && (choice == 0 || eligibility != null && eligibility.ActiveIdentity == ActiveIdentity()
                && eligibility.Allows(logical, PreparedColorRole.Allows))
            && selected.Any(p => p.Key == logical && p.Value.FullName == physical)
            && PngCache.Hash(ReadSource(new FileInfo(physical))).SequenceEqual(PngCache.Hash(original));
    }
}
