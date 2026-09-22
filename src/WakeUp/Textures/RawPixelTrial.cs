// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace WakeUp;

// Explicit fixture investigation only. No installed-user setting selects this backend.
internal static class RawPixelTrial
{
    internal const long Reservation = 97L * 1024 * 1024; // Whole 96-MiB child + bounded parent IPC state.
    private static RawPixelHelperClient? client;
    internal static bool Selected { get; private set; }
    private static readonly object gate = new();
    private static long png, jpeg, pngUploads, jpegUploads, mismatchedJpeg, fallback;
    private static readonly Dictionary<string, long> reasons = new(StringComparer.Ordinal);
    private static readonly List<string> jpegExamples = new();
    internal static void Initialize(IReadOnlyList<string> args)
    {
        const string prefix = "--fixture-raw-pixel-helper=";
        var selected = args.Where(a => a.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        Selected = selected.Length == 1 && selected[0].Length == prefix.Length + 64
            && selected[0].Substring(prefix.Length).All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f')
            && args.Contains("--fixture-exit-after-menu-ready") && GameBuildContract.Current == GameBuildContract.GogRev573;
        if (Selected) client = new RawPixelHelperClient(Path.Combine(PreparedTextureRuntime.ModRoot,
            "Tools", "win-x64", "WakeUp.RawPixelTrial.exe"), selected[0].Substring(prefix.Length));
    }
    internal static long Estimate(Stream input)
    {
        long required = FirstBuildImageData.Estimate(input);
        if (!Selected) return required;
        // JPEG retains its strict managed decode while comparing the WIC result.
        input.Position = 0; bool jpeg = input.ReadByte() == 255 && input.ReadByte() == 216; input.Position = 0;
        if (Selected && jpeg)
        {
            var header = FirstBuildJpegDecoder.ReadHeader(input);
            if (header.SourceBytes <= 16 * 1024 * 1024 && (long)header.Width * header.Height * 4 <= 16 * 1024 * 1024)
                required += (long)header.Width * header.Height * 4;
        }
        return required;
    }
    private static void Fallback(string reason)
    {
        lock (gate) { fallback++; reasons.TryGetValue(reason, out long count); reasons[reason] = count + 1; }
    }
    internal static FirstBuildImageData Decode(byte[] source, CancellationToken cancellation)
    {
        if (!Selected || client == null) return FirstBuildImageData.Decode(source, cancellation);
        if (source.Length > 16 * 1024 * 1024) { Fallback("source-budget"); return FirstBuildImageData.Decode(source, cancellation); }
        bool isJpeg = source.Length >= 2 && source[0] == 255 && source[1] == 216;
        if (isJpeg)
        {
            // Current strict parsing and native-qualified family admission remain
            // authoritative. WIC rounding is not assumed equivalent to that decoder.
            var managed = FirstBuildImageData.Decode(source, cancellation);
            var image = managed.Jpeg!;
            if ((long)image.Width * image.Height * 4 > 16 * 1024 * 1024) { Fallback("jpeg-pixel-budget"); return managed; }
            try
            {
                var rgba = client.Decode(source, image.Width, image.Height, 2, cancellation);
                for (int i = 0, j = 0; i < managed.Pixels.Length; i += 3, j += 4)
                {
                    if ((i & 65535) == 0) cancellation.ThrowIfCancellationRequested();
                    if (managed.Pixels[i] != rgba[j] || managed.Pixels[i + 1] != rgba[j + 1]
                        || managed.Pixels[i + 2] != rgba[j + 2] || rgba[j + 3] != 255)
                    {
                        Interlocked.Increment(ref mismatchedJpeg);
                        lock (gate) if (jpegExamples.Count < 8) jpegExamples.Add(PngCache.Hex(PngCache.Hash(source)) + ":pixel=" + (i / 3)
                            + ":managed=" + managed.Pixels[i] + "," + managed.Pixels[i + 1] + "," + managed.Pixels[i + 2]
                            + ":wic=" + rgba[j] + "," + rgba[j + 1] + "," + rgba[j + 2] + "," + rgba[j + 3]);
                        Fallback("jpeg-exact-pixel-mismatch"); return managed;
                    }
                }
                for (int i = 0, j = 0; i < managed.Pixels.Length; i += 3, j += 4)
                { managed.Pixels[i] = rgba[j]; managed.Pixels[i + 1] = rgba[j + 1]; managed.Pixels[i + 2] = rgba[j + 2]; }
                managed.RawHelper = true; Interlocked.Increment(ref jpeg); return managed;
            }
            catch (Exception e) when (e is IOException || e is NotSupportedException || e is InvalidOperationException)
            { Fallback("jpeg-helper-" + e.GetType().Name); return managed; }
        }
        // Initial raw candidate: ordinary noninterlaced 8-bit PNG channels.
        // Palette/packed/16-bit/Adam7/PSD retain the existing complete route.
        if (source.Length < 33 || source[0] != 137 || source[1] != 80 || source[24] != 8 || source[28] != 0
            || !(source[25] == 0 || source[25] == 2 || source[25] == 4 || source[25] == 6))
        { Fallback("unqualified-family"); return FirstBuildImageData.Decode(source, cancellation); }
        var validated = FirstBuildPngDecoder.Validate(source, cancellation);
        // Native probe found WIC loses the grayscale transparent-key result.
        // Preserve the existing exact managed decoder for this metadata family.
        if (validated.ColorType == 0 && validated.HasTransparency)
        { Fallback("png-gray-transparent-key"); return FirstBuildImageData.Decode(source, cancellation); }
        if ((long)validated.Width * validated.Height * 4 > 16 * 1024 * 1024)
        { Fallback("png-pixel-budget"); return FirstBuildImageData.Decode(source, cancellation); }
        try
        {
            validated.Pixels = client.Decode(source, validated.Width, validated.Height, 1, cancellation);
            var result = FirstBuildImageData.FromPng(validated, cancellation); result.RawHelper = true;
            Interlocked.Increment(ref png); return result;
        }
        catch (Exception e) when (e is IOException || e is NotSupportedException || e is InvalidOperationException)
        { Fallback("png-helper-" + e.GetType().Name); return FirstBuildImageData.Decode(source, cancellation); }
    }
    internal static void Uploaded(FirstBuildImageData data)
    { if (data.RawHelper) { if (data.Png != null) Interlocked.Increment(ref pngUploads); else Interlocked.Increment(ref jpegUploads); } }
    internal static string Finish()
    {
        client?.Dispose();
        lock (gate) return "\"selected\":" + Selected.ToString().ToLowerInvariant() + ",\"pngDecoded\":" + png
            + ",\"jpegDecoded\":" + jpeg + ",\"pngUploads\":" + pngUploads + ",\"jpegUploads\":" + jpegUploads
            + ",\"jpegMismatches\":" + mismatchedJpeg + ",\"fallbacks\":" + fallback
            + ",\"childStarts\":" + (client?.Starts ?? 0) + ",\"requests\":" + (client?.Requests ?? 0)
            + ",\"failures\":" + (client?.Failures ?? 0) + ",\"childPeakWorkingSetBytes\":" + (client?.PeakProcessBytes ?? 0)
            + ",\"jpegMismatchExamples\":\"" + string.Join(";", jpegExamples) + "\""
            + ",\"reservedChildAndIpcBytes\":" + (Selected ? Reservation : 0)
            + ",\"fallbackReasons\":\"" + string.Join(";", reasons.Select(p => p.Key + "=" + p.Value)) + "\"";
    }
}
