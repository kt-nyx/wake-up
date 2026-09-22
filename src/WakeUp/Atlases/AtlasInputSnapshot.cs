// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace WakeUp;

// Inputs may be changed by a mod between frames. Own their GPU snapshots for
// the complete private bake, while published tile keys remain the real sources.
internal sealed class AtlasInputSnapshot : IDisposable
{
    private readonly Dictionary<Texture2D, Texture2D> copies = new();
    private long bytes;
    internal readonly bool Compression = Prefs.TextureCompression;
    internal readonly bool Compute = UnityData.ComputeShadersSupported;
    internal readonly int MaximumSize = StaticTextureAtlas.MaxAtlasSize;
    internal Texture2D Get(Texture2D original) => copies[original];
    internal void Add(Texture2D source)
    {
        if (copies.ContainsKey(source)) return;
        int length = NativeTextureData.ExpectedBytes(source.width, source.height, (int)source.format, source.mipmapCount);
        if (length < 1 || (bytes += length * (source.isReadable ? 2L : 1L)) > 96L * 1024 * 1024)
            throw new InvalidDataException("atlas-input-snapshot-bound");
        var copy = new Texture2D(source.width, source.height, source.format, source.mipmapCount,
            !GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat), true);
        try
        {
            if (copy.graphicsFormat != source.graphicsFormat || copy.mipmapCount != source.mipmapCount)
                throw new InvalidDataException("atlas-input-snapshot-format");
            copy.Apply(false, !source.isReadable);
            Graphics.CopyTexture(source, copy);
            copy.filterMode = source.filterMode; copy.wrapModeU = source.wrapModeU;
            copy.wrapModeV = source.wrapModeV; copy.wrapModeW = source.wrapModeW;
            copy.anisoLevel = source.anisoLevel; copy.mipMapBias = source.mipMapBias;
            if (source.minimumMipmapLevel >= 0) copy.minimumMipmapLevel = source.minimumMipmapLevel;
            if (source.requestedMipmapLevel >= 0) copy.requestedMipmapLevel = source.requestedMipmapLevel;
            copy.name = source.name;
            copies.Add(source, copy);
        }
        catch { Object.DestroyImmediate(copy); throw; }
    }
    public void Dispose() { foreach (var copy in copies.Values) Object.DestroyImmediate(copy); copies.Clear(); }
}
