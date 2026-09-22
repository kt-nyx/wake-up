// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace WakeUp;

// GPU atlas writes do not update a readable Texture2D's CPU copy. These are two
// different observable states, so restoring only GetRawTextureData is incorrect.
// Every Unity operation here belongs to the main-thread atlas transaction.
internal static class AtlasPixels
{
    internal sealed class Image
    {
        internal PngCache.Entry Gpu = null!;
        internal byte[]? Cpu;
        internal int MinimumMipmapLevel;
        internal int RequestedMipmapLevel;
    }

    [ThreadStatic] private static CaptureScope? current;

    internal static CaptureScope BeginCapture()
    {
        MainThread();
        if (current != null) throw new InvalidOperationException("atlas-pixel-nested-capture");
        return current = new CaptureScope();
    }

    internal sealed class CaptureScope : IDisposable
    {
        private readonly Dictionary<Texture2D, byte[][]> compressed = new();
        private readonly Dictionary<Texture2D, string> failed = new();
        private bool disposed;
        private long capturedBytes;

        internal void Copy(Texture source, int srcElement, int srcMip, int srcX, int srcY,
            int width, int height, Texture destination, int dstElement, int dstMip, int dstX, int dstY)
        {
            if (!(destination is Texture2D target)) return;
            try
            {
                MainThread();
                if (disposed || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11 || target.format != TextureFormat.DXT5 || target.isReadable
                    || source.graphicsFormat != GraphicsFormat.R32G32B32A32_SInt
                    || srcElement != 0 || srcMip != 0 || srcX != 0 || srcY != 0
                    || dstElement != 0 || dstX != 0 || dstY != 0 || dstMip < 0 || dstMip >= target.mipmapCount
                    || width != source.width || height != source.height
                    || width * 4 != Math.Max(1, target.width >> dstMip)
                    || height * 4 != Math.Max(1, target.height >> dstMip)
                    || NativeTextureData.ExpectedBytes(target.width, target.height, (int)target.format, target.mipmapCount) < 1)
                    throw new InvalidDataException("atlas-compressed-copy-shape");
                if (!SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, FormatUsage.ReadPixels))
                    throw new NotSupportedException("atlas-compressed-readback-format");
                if (!compressed.TryGetValue(target, out byte[][] blocks))
                    compressed.Add(target, blocks = new byte[target.mipmapCount][]);
                if (blocks[dstMip] != null) throw new InvalidDataException("atlas-compressed-duplicate-mip");
                long incoming = (long)width * height * 16;
                if (incoming > AtlasCacheStore.MaximumEntry - capturedBytes)
                    throw new InvalidDataException("atlas-compressed-group-bound");

                RenderTexture? readable = null;
                try
                {
                    // The native compressor exposes BC3 blocks as four signed
                    // words. D3D11 CopyTexture is a bit copy, not a numeric cast.
                    // This is the existing C05 block capture mechanism.
                    readable = RenderTexture.GetTemporary(width, height, 0,
                        RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                    Graphics.CopyTexture(source, srcElement, srcMip, readable, 0, 0);
                    byte[] bytes = Readback(readable, 0);
                    if (bytes.Length != checked(width * height * 16))
                        throw new InvalidDataException("atlas-compressed-block-length");
                    blocks[dstMip] = bytes;
                    capturedBytes += bytes.Length;
                }
                finally { if (readable != null) RenderTexture.ReleaseTemporary(readable); }
            }
            catch (Exception e)
            {
                // Capture is optional. An error must never interrupt the native
                // compressor or cause destruction of its input/output textures.
                compressed.Remove(target);
                failed[target] = e.GetType().Name + ":" + e.Message;
            }
        }

        internal Image Capture(Texture2D texture)
        {
            MainThread();
            if (disposed || !ReferenceEquals(current, this)) throw new InvalidOperationException("atlas-pixel-capture-lifetime");
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (failed.TryGetValue(texture, out string reason)) throw new InvalidDataException(reason);
            int expected = NativeTextureData.ExpectedBytes(texture.width, texture.height, (int)texture.format, texture.mipmapCount);
            if (expected < 1) throw new InvalidDataException("atlas-pixel-size");
            byte[] pixels;
            if (compressed.TryGetValue(texture, out byte[][] blocks))
            {
                if (blocks.Any(b => b == null) || blocks.Sum(b => (long)b.Length) != expected)
                    throw new InvalidDataException("atlas-compressed-incomplete-mips");
                pixels = Join(blocks, expected);
            }
            else
            {
                if (GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat))
                    throw new NotSupportedException("atlas-compressed-capture-boundary-missing");
                var mips = new byte[texture.mipmapCount][];
                for (int mip = 0; mip < mips.Length; mip++)
                {
                    mips[mip] = Readback(texture, mip);
                    int mipLength = NativeTextureData.ExpectedBytes(Math.Max(1, texture.width >> mip),
                        Math.Max(1, texture.height >> mip), (int)texture.format, 1);
                    if (mips[mip].Length != mipLength) throw new InvalidDataException("atlas-readback-mip-length");
                }
                pixels = Join(mips, expected);
            }
            var image = new Image
            {
                Gpu = PngRuntime.Describe(texture, pixels),
                Cpu = texture.isReadable ? texture.GetRawTextureData() : null,
                MinimumMipmapLevel = texture.minimumMipmapLevel,
                RequestedMipmapLevel = texture.requestedMipmapLevel
            };
            if (!Valid(image)) throw new InvalidDataException("atlas-pixel-descriptor");
            return image;
        }

        public void Dispose()
        {
            MainThread();
            if (disposed) return;
            disposed = true;
            compressed.Clear(); failed.Clear();
            if (ReferenceEquals(current, this)) current = null;
        }
    }

    // Same signature as the region-copy call inside native FastCompressDXT.
    // The caller installs a guarded transpiler at precisely that boundary.
    internal static void CopyCompressionBlocks(Texture source, int srcElement, int srcMip,
        int srcX, int srcY, int width, int height, Texture destination, int dstElement, int dstMip, int dstX, int dstY)
    {
        Graphics.CopyTexture(source, srcElement, srcMip, srcX, srcY, width, height, destination, dstElement, dstMip, dstX, dstY);
        current?.Copy(source, srcElement, srcMip, srcX, srcY, width, height, destination, dstElement, dstMip, dstX, dstY);
    }

    internal static bool Valid(Image image) => image != null && NativeTextureData.Valid(image.Gpu)
        && (image.Gpu.Readable ? image.Cpu != null && image.Cpu.Length == image.Gpu.Pixels.Length : image.Cpu == null)
        && image.MinimumMipmapLevel >= -1 && image.MinimumMipmapLevel <= 31
        && image.RequestedMipmapLevel >= -1 && image.RequestedMipmapLevel <= 31;

    internal static Texture2D Restore(Image image)
    {
        MainThread();
        if (!Valid(image)) throw new InvalidDataException("atlas-pixel-descriptor");
        Texture2D texture = PngRuntime.Restore(image.Gpu);
        try
        {
            // Apply above uploaded the GPU payload. This final CPU-only write is
            // deliberate: another Apply would overwrite GPU atlas pixels with
            // the native readable copy, which can contain stale initial bytes.
            if (image.Cpu != null) texture.LoadRawTextureData(image.Cpu);
            // Leave matching constructor defaults alone, including an unset
            // streaming override; atlas textures themselves do not stream.
            if (texture.minimumMipmapLevel != image.MinimumMipmapLevel)
            {
                if (image.MinimumMipmapLevel == -1) texture.ClearMinimumMipmapLevel();
                else texture.minimumMipmapLevel = image.MinimumMipmapLevel;
            }
            if (texture.requestedMipmapLevel != image.RequestedMipmapLevel)
            {
                if (image.RequestedMipmapLevel == -1) texture.ClearRequestedMipmapLevel();
                else texture.requestedMipmapLevel = image.RequestedMipmapLevel;
            }
            if (texture.minimumMipmapLevel != image.MinimumMipmapLevel || texture.requestedMipmapLevel != image.RequestedMipmapLevel)
                throw new InvalidDataException("atlas-pixel-mip-state");
            return texture;
        }
        catch { Object.DestroyImmediate(texture); throw; }
    }

    private static byte[] Readback(Texture texture, int mip)
    {
        var request = AsyncGPUReadback.Request(texture, mip);
        request.WaitForCompletion();
        if (request.hasError) throw new InvalidOperationException("atlas-gpu-readback");
        return request.GetData<byte>().ToArray();
    }

    private static byte[] Join(byte[][] mips, int expected)
    {
        var result = new byte[expected];
        int at = 0;
        foreach (byte[] mip in mips) { Buffer.BlockCopy(mip, 0, result, at, mip.Length); at += mip.Length; }
        if (at != expected) throw new InvalidDataException("atlas-readback-total-length");
        return result;
    }

    private static void MainThread()
    {
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("atlas-pixel-main-thread");
    }
}
