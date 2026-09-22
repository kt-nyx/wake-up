// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

internal sealed class FirstBuildImageData
{
    internal bool RawHelper;
    internal FirstBuildPngDecoder.Result? Png;
    internal PsdTextureDecoder.Result? Psd;
    internal FirstBuildJpegDecoder.Result? Jpeg;
    internal PreparedTextureStore.ReadPlan? WarmPlan;
    internal PreparedTextureStore.ReadOutcome? WarmOutcome;
    internal string? WarmIdentity;
    // Moved from the decoder result after conversion to the qualified engine
    // byte order. The decoder's standalone result remains RGBA8 by contract.
    internal byte[] Pixels = Array.Empty<byte>();
    internal static long Estimate(Stream input)
    {
        input.Position = 0;
        bool jpeg = input.ReadByte() == 255 && input.ReadByte() == 216;
        input.Position = 0;
        if (jpeg)
        {
            var metadata = FirstBuildJpegDecoder.ReadHeader(input);
            if (!NativeJpegFamily(metadata)) throw new NotSupportedException("JPEG family uses ordinary loading");
            return metadata.RequiredBytes - metadata.SourceBytes - FirstBuildJpegDecoder.ScratchAllowance;
        }
        var header = PreparationImageHeader.Read(input, true);
        return checked((long)header.Width * header.Height * 4);
    }
    private static bool NativeJpegFamily(FirstBuildJpegDecoder.Header h)
    {
        // Keep the existing C05 image-size boundary and the exact qualified
        // grayscale / YCbCr branches. CMYK, direct RGB and unusual sampling
        // remain ordinary native loading, including native rejection behavior.
        if ((h.FrameMarker != 192 && h.FrameMarker != 194)
            || (long)h.Width * h.Height * 4 > PreparationImageHeader.MaximumDecoded) return false;
        if (h.Components == 1) return h.HorizontalSampling[0] == 1 && h.VerticalSampling[0] == 1;
        if (h.Components != 3 || h.AdobeTransform != -1 && h.AdobeTransform != 1
            || h.ComponentIds[0] == 'R' && h.ComponentIds[1] == 'G' && h.ComponentIds[2] == 'B') return false;
        return h.HorizontalSampling[1] == 1 && h.HorizontalSampling[2] == 1
            && h.VerticalSampling[1] == 1 && h.VerticalSampling[2] == 1
            && (h.HorizontalSampling[0] == 1 && h.VerticalSampling[0] == 1
                || h.HorizontalSampling[0] == 2 && (h.VerticalSampling[0] == 1 || h.VerticalSampling[0] == 2));
    }
    internal static FirstBuildImageData Decode(byte[] source, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (source.Length >= 2 && source[0] == 255 && source[1] == 216)
        {
            using var stream = new MemoryStream(source, false);
            if (!NativeJpegFamily(FirstBuildJpegDecoder.ReadHeader(stream, cancellation)))
                throw new NotSupportedException("JPEG family uses ordinary loading");
            var jpeg = FirstBuildJpegDecoder.Decode(source, cancellation);
            // APP markers after the frame can change interpretation during decode.
            if (!NativeJpegFamily(jpeg.Metadata)) throw new NotSupportedException("JPEG metadata uses ordinary loading");
            return FromJpeg(jpeg);
        }
        if (source.Length >= 4 && source[0] == '8' && source[1] == 'B' && source[2] == 'P' && source[3] == 'S')
        {
            var psd = PsdTextureDecoder.Decode(source);
            cancellation.ThrowIfCancellationRequested();
            return new FirstBuildImageData { Psd = psd };
        }
        var png = FirstBuildPngDecoder.Decode(source, cancellation);
        // GOG native comparisons cover packed/8/16-bit samples, Adam7 and
        // gAMA/cHRM/sBIT/sRGB/iCCP. Like native loading, retain source samples
        // without a profile transform. Legacy gamma behavior is guarded by
        // the caller before admission and again before publication.
        return FromPng(png, cancellation);
    }
    internal static FirstBuildImageData FromPng(FirstBuildPngDecoder.Result png, CancellationToken cancellation)
    {
        byte[] pixels = png.Pixels;
        png.Pixels = Array.Empty<byte>();
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte alpha = pixels[i + 3];
            pixels[i + 3] = pixels[i + 2]; pixels[i + 2] = pixels[i + 1]; pixels[i + 1] = pixels[i]; pixels[i] = alpha;
            if ((i & 65535) == 0) cancellation.ThrowIfCancellationRequested();
        }
        return new FirstBuildImageData { Png = png, Pixels = pixels };
    }
    internal static FirstBuildImageData FromJpeg(FirstBuildJpegDecoder.Result jpeg)
        => new() { Jpeg = jpeg, Pixels = jpeg.Pixels };
}

internal static partial class PngRuntime
{
    private static bool firstBuildSelected;
    private static bool firstBuildReloadAllowed;
    private static readonly List<PublishedPatchGuard> FirstBuildGuards = new();
    private static FirstBuildImageQueue<FirstBuildImageData>.Lease? firstBuildFlight;
    private static byte[]? FirstBuildSource => firstBuildFlight?.SourceComplete == true ? firstBuildFlight.Source : null;
    private static long firstBuildPngUploads;
    private static long firstBuildJpegUploads;
    private static int firstBuildGuardCount;
    private static PublishedPatchGuard? firstBuildHolderGuard;

    private static void InitializeFirstBuild(IReadOnlyList<string> args)
    {
        // C06 automatic decoding and raw-helper adoption are retired. The
        // holder guard below is still required by retained warm texture reads.
        firstBuildSelected = false;
        var holderConstructor = AccessTools.Constructor(typeof(LoadedContentItem<Texture2D>),
            new[] { typeof(RimWorld.IO.VirtualFile), typeof(Texture2D), typeof(IDisposable) });
        if (holderConstructor == null || !PublishedPatchGuard.TryCreate(holderConstructor, Owner, out firstBuildHolderGuard, true))
        { firstBuildSelected = false; return; }
        FirstBuildGuards.Add(firstBuildHolderGuard!);
        if (!firstBuildSelected) return;
        var targets = typeof(ImageConversion).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "LoadImage").Cast<MethodBase>()
            .Concat(typeof(Texture2D).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(c =>
                c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(int), typeof(int), typeof(TextureFormat), typeof(bool) })
                || c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(int), typeof(int), typeof(TextureFormat), typeof(int), typeof(bool) })
                || c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(int), typeof(int), typeof(TextureFormat), typeof(int), typeof(bool), typeof(IntPtr), typeof(bool), typeof(bool), typeof(string) })))
            .Concat(typeof(Texture2D).GetMethods().Where(m => m.Name == "SetPixelData" && m.IsGenericMethodDefinition)
                .Select(m => m.MakeGenericMethod(typeof(byte))).Cast<MethodBase>())
            .Concat(new MethodBase[] { AccessTools.Method(typeof(Texture2D), "Apply", new[] { typeof(bool), typeof(bool) }),
                AccessTools.Method(typeof(Texture2D), "Compress", new[] { typeof(bool) }) });
        foreach (var target in targets)
        {
            if (!PublishedPatchGuard.TryCreate(target, Owner, out var guard, true))
            { firstBuildSelected = false; return; }
            FirstBuildGuards.Add(guard!);
        }
        firstBuildGuardCount = FirstBuildGuards.Count;
    }
    private static bool FirstBuildCompatible() => firstBuildSelected && !ImageConversion.EnableLegacyPngGammaRuntimeLoadBehavior
        && firstBuildGuardCount >= 10 && FirstBuildGuards.Count == firstBuildGuardCount
        && FirstBuildGuards.All(g => g.AllowsOriginalContract());

    private static void ReportFirstBuild(FirstBuildImageQueue<FirstBuildImageData> queue)
        => Write("first-build-queue", "\"scheduled\":" + queue.Scheduled + ",\"decoded\":" + queue.Decoded
            + ",\"consumed\":" + queue.Consumed + ",\"failed\":" + queue.Failed
            + ",\"retained\":" + queue.Retained + ",\"revalidated\":" + queue.Revalidated + ",\"rejectedRetained\":" + queue.RejectedRetained
            + ",\"maxReservedBytes\":" + queue.MaxReservedBytes + ",\"maxActiveWorkers\":" + queue.MaxActiveWorkers
            + ",\"activeWorkers\":" + queue.ActiveWorkers + ",\"heldBytes\":" + queue.HeldBytes
            + ",\"pngRawUploadsTotal\":" + firstBuildPngUploads + ",\"jpegRawUploadsTotal\":" + firstBuildJpegUploads);

    internal static IEnumerable<CodeInstruction> FirstBuildTranspiler(IEnumerable<CodeInstruction> source)
    {
        int constructors = 0, loads = 0;
        var code = source.Select(i => new CodeInstruction(i)).ToArray();
        foreach (var instruction in code)
        {
            if (instruction.opcode == OpCodes.Newobj && instruction.operand is ConstructorInfo ctor && ctor.DeclaringType == typeof(Texture2D))
            {
                Type[] args = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
                string method = args.SequenceEqual(new[] { typeof(int), typeof(int), typeof(TextureFormat), typeof(bool) }) ? nameof(CreateFirstImage)
                    : args.SequenceEqual(new[] { typeof(int), typeof(int), typeof(TextureFormat), typeof(int), typeof(bool) }) ? nameof(CreateReducedImage)
                    : throw new InvalidOperationException("Unknown native image constructor");
                instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(PngRuntime), method); constructors++;
            }
            else if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(ImageConversion) && method.Name == "LoadImage")
            {
                if (method.GetParameters().Length != 2) throw new InvalidOperationException("Unknown native LoadImage overload");
                instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(PngRuntime), nameof(UploadFirstImage)); loads++;
            }
        }
        if (constructors != 2 || loads != 2) throw new InvalidOperationException("First image callsites changed");
        return code;
    }
    private static Texture2D CreateFirstImage(int width, int height, TextureFormat format, bool mips)
    {
        var prepared = current?.Prepared;
        if (prepared?.Png == null && prepared?.Jpeg == null) return new Texture2D(width, height, format, mips);
        int decodedWidth = prepared!.Png?.Width ?? prepared.Jpeg!.Width;
        int decodedHeight = prepared.Png?.Height ?? prepared.Jpeg!.Height;
        Texture2D texture;
        try { texture = new Texture2D(decodedWidth, decodedHeight, prepared.Png != null ? TextureFormat.ARGB32 : TextureFormat.RGB24, mips); }
        catch { current!.Prepared = null; return new Texture2D(width, height, format, mips); }
        current!.OwnedTextures.Add(texture);
        return texture;
    }
    private static Texture2D CreateReducedImage(int width, int height, TextureFormat format, int mips, bool linear)
    {
        if (current?.Prepared?.Png == null && current?.Prepared?.Jpeg == null) return new Texture2D(width, height, format, mips, linear);
        Texture2D texture;
        try { texture = new Texture2D(width, height, current!.Prepared!.Png != null ? TextureFormat.ARGB32 : TextureFormat.RGB24, mips, linear); }
        catch { current.Prepared = null; return new Texture2D(width, height, format, mips, linear); }
        current.OwnedTextures.Add(texture);
        return texture;
    }
    private static bool UploadFirstImage(Texture2D texture, byte[] source)
    {
        var decoded = current?.Prepared;
        if (decoded?.Png == null && decoded?.Jpeg == null) return ImageConversion.LoadImage(texture, source);
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("Image publication requires main thread");
        try
        {
        texture.SetPixelData(current!.Prepared!.Pixels, 0, 0);
        // LoadImage supplies the initial mip chain before native CPU compression.
        // The unchanged native body may later regenerate/finalize it again.
        texture.Apply(true, false);
        }
        catch
        {
            // Nothing is visible yet. Native LoadImage replaces the private
            // texture's complete content at this same callsite; no callbacks or
            // native finalization above this point are replayed.
            current!.Prepared = null;
            return ImageConversion.LoadImage(texture, source);
        }
        firstBuildFlight?.MarkConsumed();
        RawPixelTrial.Uploaded(decoded);
        if (decoded.Png != null) firstBuildPngUploads++; else firstBuildJpegUploads++;
        return true;
    }
}
