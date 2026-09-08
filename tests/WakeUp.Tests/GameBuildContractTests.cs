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
            Assert.That(build.HasReviewedLoadingMethods, Is.True);
            Assert.That(GameBuildContract.Select(identity, Guid.Empty), Is.Null);
            Assert.That(GameBuildContract.Select(new AssemblyName("Other, Version=" + build.Version), build.Mvid), Is.Null);
            Assert.That(GameBuildContract.Select(new AssemblyName("Assembly-CSharp, Version=1.6.9999.1"), build.Mvid), Is.Null);
        }
        Assert.That(GameBuildContract.Select(new AssemblyName("Assembly-CSharp, Version=" + GameBuildContract.GogRev573.Version), GameBuildContract.SteamRev590.Mvid), Is.Null);
        Assert.That(GameBuildContract.Select(new AssemblyName("Assembly-CSharp, Version=" + GameBuildContract.LinuxRev600.Version), GameBuildContract.GogRev573.Mvid), Is.Null);
    }

}
