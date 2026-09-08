// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.Rendering;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class TexturePlatformTests
{
    [Test]
    public void DeckDefaultsSelectCpuCaptureWithoutComputeReadbackSupport()
    {
        Assert.That(TexturePlatformSupport.SupportsReadback(GameBuildContract.LinuxRev600, GraphicsDeviceType.OpenGLCore), Is.True);
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.LinuxRev600, GraphicsDeviceType.OpenGLCore,
            compression: true, compute: false, floatReadback: false), Is.EqualTo(PngCapturePath.CpuTextureData));
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.LinuxRev600, GraphicsDeviceType.OpenGLCore,
            compression: false, compute: false, floatReadback: false), Is.EqualTo(PngCapturePath.CpuTextureData));
        // Linux GPU block copying has not been ported. A changed compute path
        // must retain the game loader instead of using D3D-specific operations.
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.LinuxRev600, GraphicsDeviceType.OpenGLCore,
            compression: true, compute: true, floatReadback: true), Is.EqualTo(PngCapturePath.Unsupported));
    }

    [Test]
    public void WindowsRetainsComputePathAndCanCaptureCpuFallback()
    {
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.GogRev573, GraphicsDeviceType.Direct3D11,
            true, true, true), Is.EqualTo(PngCapturePath.D3D11CompressionBlocks));
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.GogRev573, GraphicsDeviceType.Direct3D11,
            true, true, false), Is.EqualTo(PngCapturePath.Unsupported));
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.GogRev573, GraphicsDeviceType.Direct3D11,
            true, false, false), Is.EqualTo(PngCapturePath.CpuTextureData));
        foreach (var build in new[] { GameBuildContract.GogRev573, GameBuildContract.LinuxRev600, GameBuildContract.SteamRev590 })
        {
            Assert.That(TexturePlatformSupport.SupportsReadback(build, GraphicsDeviceType.Vulkan), Is.False);
            Assert.That(TexturePlatformSupport.SelectPngPath(build, GraphicsDeviceType.Vulkan, true, false, true), Is.EqualTo(PngCapturePath.Unsupported));
        }
        Assert.That(TexturePlatformSupport.SupportsReadback(GameBuildContract.SteamRev590, GraphicsDeviceType.Direct3D11), Is.True);
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.SteamRev590, GraphicsDeviceType.Direct3D11,
            true, false, false), Is.EqualTo(PngCapturePath.CpuTextureData));
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.SteamRev590, GraphicsDeviceType.Direct3D11,
            true, true, true), Is.EqualTo(PngCapturePath.D3D11CompressionBlocks));
        Assert.That(TexturePlatformSupport.SelectPngPath(GameBuildContract.SteamRev590, GraphicsDeviceType.OpenGLCore,
            true, false, true), Is.EqualTo(PngCapturePath.Unsupported));
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void CaptureSeesFinalMipsBeforeReadablePixelsAreDiscarded(bool updateMips, bool unreadable)
    {
        bool readable = true;
        int pixels = 1, captured = 0;
        var operations = new List<string>();
        PngRuntime.ApplyWithReadableCapture((update, discard) =>
        {
            Assert.That(readable, Is.True);
            if (update) pixels = 2;
            if (discard) readable = false;
            operations.Add("apply:" + update + ":" + discard);
        }, () =>
        {
            Assert.That(readable, Is.True);
            captured = pixels;
            operations.Add("capture");
        }, updateMips, unreadable);
        Assert.That(captured, Is.EqualTo(updateMips ? 2 : 1));
        Assert.That(readable, Is.EqualTo(!unreadable));
        Assert.That(operations, Is.EqualTo(unreadable
            ? new[] { "apply:" + updateMips + ":False", "capture", "apply:False:True" }
            : new[] { "apply:" + updateMips + ":False", "capture" }));
    }

    [Test]
    public void CaptureFailureStillDiscardsPixelsButOriginalApplyFailureIsNotHidden()
    {
        bool discarded = false;
        Assert.Throws<InvalidOperationException>(new Action(() => PngRuntime.ApplyWithReadableCapture(
            (_, discard) => discarded |= discard, () => throw new InvalidOperationException("capture"), true, true)));
        Assert.That(discarded, Is.True);
        bool captured = false;
        Assert.Throws<InvalidOperationException>(new Action(() => PngRuntime.ApplyWithReadableCapture(
            (_, _) => throw new InvalidOperationException("native apply"), () => captured = true, true, true)));
        Assert.That(captured, Is.False);
    }
}
