// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using UnityEngine.Rendering;

namespace WakeUp;

internal enum PngCapturePath { Unsupported, CpuTextureData, D3D11CompressionBlocks }

internal static class TexturePlatformSupport
{
    internal static bool SupportsReadback(GameBuildContract build, GraphicsDeviceType backend) =>
        (build.HasReviewedLoadingMethods && !build.IsLinux && backend == GraphicsDeviceType.Direct3D11)
        || (build.IsLinux && backend == GraphicsDeviceType.OpenGLCore);

    internal static PngCapturePath SelectPngPath(GameBuildContract build, GraphicsDeviceType backend,
        bool compression, bool compute, bool floatReadback)
    {
        if (!SupportsReadback(build, backend))
            return PngCapturePath.Unsupported;
        // Steam's Linux launch uses disabled compute shaders; the reviewed Linux
        // game also forces ComputeShadersSupported false. Preserve those defaults.
        if (!compression || !compute)
            return PngCapturePath.CpuTextureData;
        return backend == GraphicsDeviceType.Direct3D11 && floatReadback
            ? PngCapturePath.D3D11CompressionBlocks : PngCapturePath.Unsupported;
    }
}
