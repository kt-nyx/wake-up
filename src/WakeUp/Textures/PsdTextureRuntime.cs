// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WakeUp;

// Explicit optional new decoding, not a claim of native PSD preservation.
internal static class PsdTextureRuntime
{
    internal const string Contract = PsdTextureDecoder.Contract + "|Unity-RGBA32-sRGB-full-mips-trilinear-aniso2-unreadable-v1";

    internal static Texture2D Create(byte[] source, Action<PngCache.Entry>? publish = null, Func<int, bool>? admit = null)
    {
        if (!PreparedTextureRuntime.PsdSupport) throw new NotSupportedException("PSD decoding is not selected");
        var decoded = PsdTextureDecoder.Decode(source);
        return CreateDecoded(decoded, publish, admit);
    }

    internal static Texture2D CreateDecoded(PsdTextureDecoder.Result decoded, Action<PngCache.Entry>? publish = null, Func<int, bool>? admit = null)
    {
        if (!PreparedTextureRuntime.PsdSupport) throw new NotSupportedException("PSD decoding is not selected");
        if (!Verse.UnityData.IsInMainThread) throw new InvalidOperationException("PSD publication requires main thread");
        var texture = new Texture2D(decoded.Width, decoded.Height, TextureFormat.RGBA32, true, false);
        try
        {
            texture.SetPixelData(decoded.Pixels, 0, 0);
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 2;
            texture.Apply(true, false);
            int bytes = NativeTextureData.ExpectedBytes(texture.width, texture.height, (int)texture.format, texture.mipmapCount);
            byte[]? captured = publish != null && bytes > 0 && (admit == null || admit(bytes)) ? texture.GetRawTextureData() : null;
            texture.Apply(false, true);
            if (captured != null) publish!(PngRuntime.Describe(texture, captured));
            return texture;
        }
        catch { Object.DestroyImmediate(texture); throw; }
    }

    internal static PngCache.Entry? Capture(byte[] source, Func<int, bool> admit)
    {
        using var stream = new MemoryStream(source, false);
        var header = PsdTextureDecoder.ReadHeader(stream);
        int bytes = NativeTextureData.ExpectedBytes(header.Width, header.Height, 4, PreparationImageHeader.Mips(header.Width, header.Height));
        if (bytes < 1 || !admit(bytes)) return null;
        PngCache.Entry? entry = null;
        Texture2D texture = Create(source, value => entry = value, admit);
        Object.DestroyImmediate(texture);
        return entry;
    }
}
