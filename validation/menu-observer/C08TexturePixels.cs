// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

// A producer-independent revision adapter: observe the current GPU texels at
// the bake boundary, including generated images. Filesystem stamps and a CPU
// copy are not evidence that the GPU still contains those pixels. No source
// object is changed, retained between calls, or destroyed by this adapter.
internal static class C08TexturePixels
{
    internal static string Revision(Texture2D source)
    {
        if (!UnityData.IsInMainThread || source == null || source.width > 1024 || source.height > 1024
            || source.width < 1 || source.height < 1 || !SystemInfo.supportsAsyncGPUReadback)
            throw new NotSupportedException("atlas-input-snapshot");
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        writer.Write("c08-bounded-native-gpu-pixels-v1");
        writer.Write(source.width); writer.Write(source.height); writer.Write(source.mipmapCount);
        for (int mip = 0; mip < source.mipmapCount; mip++)
        {
            int width = Math.Max(1, source.width >> mip), height = Math.Max(1, source.height >> mip);
            Texture2D? copy = null;
            RenderTexture? target = null;
            try
            {
                // An isolated one-mip copy makes the sampled level explicit.
                // Floating RGBA retains BC decoder interpolation, which an
                // eight-bit readback could round away before atlas filtering.
                int copyWidth = width, copyHeight = height, copyMip = 0;
                if (UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsCompressedFormat(source.graphicsFormat))
                    while (copyWidth < 4 || copyHeight < 4 || copyWidth % 4 != 0 || copyHeight % 4 != 0)
                    { copyWidth *= 2; copyHeight *= 2; copyMip++; }
                copy = new Texture2D(copyWidth, copyHeight, source.format, copyMip + 1,
                    !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat), true);
                if (copy.mipmapCount != copyMip + 1 || copy.graphicsFormat != source.graphicsFormat)
                    throw new NotSupportedException("atlas-input-mip-copy");
                copy.filterMode = FilterMode.Point; copy.wrapMode = TextureWrapMode.Clamp;
                // Compressed 1x1/2x2 mips cannot be a Unity texture's base.
                // Put them at the end of a legal chain and clamp sampling to
                // that last level; no uninitialized earlier mip is sampled.
                copy.anisoLevel = 0; copy.mipMapBias = copyMip == 0 ? 0 : 3;
                Graphics.CopyTexture(source, 0, mip, copy, 0, copyMip);
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                var previous = RenderTexture.active;
                try { Graphics.Blit(copy, target); }
                finally { RenderTexture.active = previous; }
                var request = AsyncGPUReadback.Request(target, 0);
                request.WaitForCompletion();
                if (request.hasError) throw new IOException("atlas-input-readback");
                byte[] data = request.GetData<byte>().ToArray();
                if (data.Length != checked(width * height * 16)) throw new IOException("atlas-input-readback-size");
                writer.Write(Hash(data));
            }
            catch (Exception e)
            { throw new InvalidDataException("atlas-input:" + source.name + ":" + width + "x" + height + ":" + source.format + "/" + source.graphicsFormat + ":mip=" + mip + ":" + e.Message, e); }
            finally
            {
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (copy != null) Object.DestroyImmediate(copy);
            }
        }
        writer.Flush(); return Hash(bytes.ToArray());
    }
    private static string Hash(byte[] data)
    {
        using var hash = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "");
    }}

