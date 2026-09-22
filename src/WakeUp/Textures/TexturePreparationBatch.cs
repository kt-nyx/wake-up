// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace WakeUp;

// Engine-independent decisions shared by the menu workflow and offline tests.
internal static class TexturePreparationBatch
{
    internal static bool Matches(string logical, string paths)
    {
        string path = logical.Replace('\\', '/');
        var selected = paths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().Replace('\\', '/')).ToArray();
        if (selected.Length == 0) return true;
        if (selected.Length > 128 || selected.Any(x => x.StartsWith("/", StringComparison.Ordinal) || x.Contains(":")
            || x.Split('/').Any(p => p == ".." || p == ".") || x.IndexOfAny(new[] { '*', '?' }) >= 0))
            throw new InvalidDataException("Use relative texture files or folders separated by semicolons");
        return selected.Any(x => path.Equals(x, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(x.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
    }

    internal static string NativeAdmission(PreparationImageHeader h)
    {
        if (h.IsDds) return "authored DDS loads natively; choose explicit quality conversion or DDS export instead";
        if (h.Width < 1 || h.Height < 1 || (long)h.Width * h.Height * 4 > PreparationImageHeader.MaximumDecoded)
            return "decoded base image exceeds 64 MiB";
        // This is a lower bound, not a prediction of the game's final format or
        // mip count. Never reject BC output by assuming full-chain RGBA32.
        int minimum = NativeTextureData.ExpectedBytes(h.Width, h.Height, 10, 1);
        return minimum < 1 || minimum > NativeTextureData.MaximumPixels
            ? "even base BC1 output exceeds the complete-entry limit" : "";
    }

    internal static string ExportIdentity(string selection, string logical, string physical, byte[] source, int preset, string helper)
        => "dds-export-v2:" + PreparedTextureRuntime.Frame(selection, logical, Path.GetFullPath(physical),
            PngCache.Hex(PngCache.Hash(source)), preset.ToString(System.Globalization.CultureInfo.InvariantCulture), helper,
            "sRGB-straight-alpha-linear-box-top-row-full-mips-BC1-or-BC3");

    internal static string ExportAdmission(PreparationImageHeader h, int preset)
    {
        if (preset < 1 || preset > 3 || !h.LossyFits(preset)) return "unsupported export dimensions or color metadata";
        int divisor = 1 << (preset - 1), width = h.Width / divisor, height = h.Height / divisor;
        int smallest = PreparedDds.LayoutBytes(width, height, 10, PreparationImageHeader.Mips(width, height));
        return (long)smallest + 128 > TexturePreparationHelper.MaximumOutput
            ? "even opaque BC1 export exceeds the complete-entry limit" : "";
    }

    // Probe only the current item's native relative path in the ordered active
    // folders. A whole-mod recursive rescan per image makes large batches quadratic.
    // Newly created higher-priority files/DDS siblings are checked on every call.
    internal static bool StillSelected(string[] folders, string logical, string physical)
    {
        if (logical.Length < 5 || !Matches(logical, logical)) return false;
        string relative = logical.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string? winner = folders.Select(f => Path.GetFullPath(Path.Combine(f, relative))).FirstOrDefault(File.Exists);
        if (winner != Path.GetFullPath(physical)) return false;
        if (logical.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)) return true;
        string sibling = relative.Substring(0, relative.Length - 4) + ".dds"; // same native .jpeg behavior
        string directory = Path.GetDirectoryName(sibling)!, basename = Path.GetFileName(sibling);
        foreach (string folder in folders)
        {
            string root = Path.Combine(folder, directory);
            if (!Directory.Exists(root)) continue;
            if (Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Any(file => Path.GetFileName(file).Equals(basename, StringComparison.OrdinalIgnoreCase))) return false;
        }
        return true;
    }

    // Publication always rechecks the caller's current source/provider contract.
    // Resume uses a fresh key and verifies persisted bytes, never a cursor file.
    internal static string Native(PreparedTextureStore store, string identity, Func<bool> revalidate,
        Func<Func<int, bool>, PngCache.Entry?> capture, CancellationToken cancellation,
        Func<PngCache.Entry?>? automatic = null, Action? releaseAutomatic = null)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!revalidate()) throw new InvalidDataException("source or selection changed; rescan");
        if (store.Read(identity) != null) { releaseAutomatic?.Invoke(); return "reused native output"; }
        if (!store.CanPublish(identity, 1)) return "skipped: capacity or maintenance policy";
        var entry = automatic?.Invoke();
        bool promoted = entry != null;
        entry ??= capture(bytes => store.CanPublish(identity, bytes));
        cancellation.ThrowIfCancellationRequested();
        if (!revalidate()) throw new InvalidDataException("source or selection changed before publication");
        if (entry == null) return "skipped: actual native format/layout or capacity refused";
        if (!store.Publish(identity, entry)) return "skipped: complete-entry limit, capacity or write refused";
        releaseAutomatic?.Invoke();
        return promoted ? "prepared: promoted automatic output" : "prepared native output";
    }

    internal static string PublishExport(PreparedTextureStore store, string identity, string logical, byte[] dds,
        Func<bool> revalidate, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!revalidate()) throw new InvalidDataException("source, preset or selection changed before export");
        return store.PublishExport(identity, logical.Replace('\\', '/'), dds) ? "prepared DDS export" : "skipped: " + store.ExportLastReason;
    }
}
