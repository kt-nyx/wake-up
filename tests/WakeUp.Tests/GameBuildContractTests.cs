// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Reflection;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class GameBuildContractTests
{
    [Test]
    public void OnlyReviewedVersionAndModulePairsSelectAContract()
    {
        foreach (GameBuildContract build in new[] { GameBuildContract.GogRev573, GameBuildContract.SteamRev590, GameBuildContract.LinuxRev600 })
        {
            var identity = new AssemblyName("Assembly-CSharp, Version=" + build.Version);
            Assert.That(GameBuildContract.Select(identity, build.Mvid), Is.SameAs(build));
            Assert.That(build.IsReviewedBuild, Is.True);
            Assert.That(GameBuildContract.Select(identity, Guid.Empty), Is.Null);
            Assert.That(GameBuildContract.Select(new AssemblyName("Other, Version=" + build.Version), build.Mvid), Is.Null);
            Assert.That(GameBuildContract.Select(new AssemblyName("Assembly-CSharp, Version=1.6.9999.1"), build.Mvid), Is.Null);
        }
        Assert.That(GameBuildContract.Select(new AssemblyName("Assembly-CSharp, Version=" + GameBuildContract.GogRev573.Version), GameBuildContract.SteamRev590.Mvid), Is.Null);
        Assert.That(GameBuildContract.Select(new AssemblyName("Assembly-CSharp, Version=" + GameBuildContract.LinuxRev600.Version), GameBuildContract.GogRev573.Mvid), Is.Null);
    }

    [TestCase(PlatformID.Win32NT, false)]
    [TestCase(PlatformID.Unix, true)]
    public void FutureBuildsReachFeatureChecksOnTheirActualPlatform(PlatformID platform, bool linux)
    {
        var mvid = Guid.NewGuid();
        foreach (string version in new[] { "1.6.9999.1", "1.7.1.2", "2.0.0.0" })
        {
            var build = GameBuildContract.SelectRuntime(new AssemblyName("Assembly-CSharp, Version=" + version), mvid, platform)!;
            Assert.That(build, Is.Not.Null);
            Assert.That(build.IsReviewedBuild, Is.False);
            Assert.That(build.IsLinux, Is.EqualTo(linux));
            Assert.That(build.Version, Is.EqualTo(version));
            Assert.That(build.Mvid, Is.EqualTo(mvid));
            Assert.That(TexturePlatformSupport.SupportsReadback(build, linux
                ? UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore
                : UnityEngine.Rendering.GraphicsDeviceType.Direct3D11), Is.True);
            Assert.That(PngRuntime.ExpectedBody(build, "LoadItem"), Is.EqualTo(
                PngRuntime.ExpectedBody(linux ? GameBuildContract.LinuxRev600 : GameBuildContract.SteamRev590, "LoadItem")));
            Assert.That(PngRuntime.ExpectedBody(build, "UnknownMethod"), Is.Null);
        }
    }

    [Test]
    public void RuntimeSelectionDoesNotMistakeAPlatformOrRevisionForAuthority()
    {
        var name = new AssemblyName("Assembly-CSharp, Version=" + GameBuildContract.LinuxRev600.Version);
        Assert.That(GameBuildContract.SelectRuntime(name, GameBuildContract.LinuxRev600.Mvid, PlatformID.Win32NT)!.IsLinux, Is.False);
        Assert.That(GameBuildContract.SelectRuntime(name, Guid.NewGuid(), PlatformID.Unix)!.IsReviewedBuild, Is.False);
        Assert.That(GameBuildContract.SelectRuntime(new AssemblyName("Other"), Guid.NewGuid(), PlatformID.Unix), Is.Null);
        Assert.That(GameBuildContract.SelectRuntime(name, Guid.NewGuid(), PlatformID.MacOSX), Is.Null);
    }

    [Test]
    public void GameUpdatesInvalidateProcessedPngCacheIdentity()
    {
        Assert.That(PngRuntime.CacheGameIdentity(GameBuildContract.SteamRev590.Mvid),
            Is.Not.EqualTo(PngRuntime.CacheGameIdentity(Guid.NewGuid())));
    }

}
