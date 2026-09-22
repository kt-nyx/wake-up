// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System.Linq;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PreparedTextureRoleContractTests
{
    [Test]
    public void NativeMetadataPathsAreNotRewrittenAsSourceFilenames()
    {
        Assert.That(PreparedTextureRoles.Normalize("Textures/Things/Example.PNG"), Is.EqualTo("Things/Example"));
        Assert.That(PreparedTextureRoles.Normalize("Things/Example.PNG", false), Is.EqualTo("Things/Example.PNG"));
        Assert.That(PreparedTextureRoles.Normalize("Textures/Things/Example", false), Is.EqualTo("Textures/Things/Example"));
        Assert.That(PreparedTextureRoles.Normalize("Things\\Example", false), Is.Null);
        Assert.That(PreparedTextureRoles.Normalize("Things/../Example", false), Is.Null);
    }

    [Test]
    public void EveryNativeConsumerHasAPinnedBody()
    {
        // Full hashes were captured with the scoped modern metadata host. This
        // host cannot resolve all newer APIs referenced by the original DLL.
        Assert.That(PreparedTextureRoles.ContractMethods.Select(PreparedTextureRoles.MethodKey),
            Is.EquivalentTo(PreparedTextureRoles.ExpectedBodies.Keys));
        Assert.That(PreparedTextureRoles.ExpectedBodies.Count, Is.EqualTo(28));
        Assert.That(PreparedTextureRoles.ExpectedBodies.Values.All(v => v.Length == 64
            && v.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F')), Is.True);
        Assert.That(PreparedTextureRoles.ExpectedBodies["Verse.Graphic_Single:Void Init(Verse.GraphicRequest)"],
            Is.EqualTo("81F101E6709DE06DEB1BB11B62C804EB01839D2B0F0BD86850D948BB2169760A"));
    }
}
