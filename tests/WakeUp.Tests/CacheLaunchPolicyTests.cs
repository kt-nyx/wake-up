// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class CacheLaunchPolicyTests
{
    [TestCase("normal", "Normal", true, true, 0)]
    [TestCase("bypass", "Bypass", false, false, 1)]
    [TestCase("rebuild", "Rebuild", false, true, 1)]
    [TestCase("clear", "Clear", false, false, 1)]
    [TestCase("unknown", "Normal", true, true, 1)]
    public void SavedActionsHaveDistinctPermissions(string saved, string action, bool read, bool write, int consumed)
    {
        CacheLaunchPolicy? state = null;
        int consumes = 0;
        var selected = CacheLaunchPolicy.Latch(ref state, saved, () => consumes++);
        Assert.That(selected.Action.ToString(), Is.EqualTo(action));
        Assert.That(selected.AllowRead, Is.EqualTo(read));
        Assert.That(selected.AllowWrite, Is.EqualTo(write));
        Assert.That(consumes, Is.EqualTo(consumed));
    }

    [Test]
    public void TwoConsumersShareBypassAndFollowingLaunchReturnsToNormal()
    {
        CacheLaunchPolicy? state = null;
        string saved = "bypass";
        int consumes = 0;
        var first = CacheLaunchPolicy.Latch(ref state, saved, () => { saved = "normal"; consumes++; });
        var second = CacheLaunchPolicy.Latch(ref state, saved, () => consumes++);
        Assert.That(second, Is.SameAs(first));
        Assert.That(first.AllowRead || first.AllowWrite || second.AllowRead || second.AllowWrite, Is.False);
        Assert.That(consumes, Is.EqualTo(1));
        CacheLaunchPolicy? followingLaunch = null;
        Assert.That(CacheLaunchPolicy.Latch(ref followingLaunch, saved, () => consumes++).Action, Is.EqualTo(CacheAction.Normal));
        Assert.That(consumes, Is.EqualTo(1));
    }

    [Test]
    public void DevelopmentLaunchLeavesQueuedUserActionForOrdinaryLaunch()
    {
        CacheLaunchPolicy? state = null;
        string saved = "clear";
        var selected = CacheLaunchPolicy.Latch(ref state, "normal", () => saved = "normal");
        Assert.That(selected.Action, Is.EqualTo(CacheAction.Normal));
        Assert.That(saved, Is.EqualTo("clear"));
    }

    [Test]
    public void FailedPersistenceDoesNotRelatchForLaterConsumers()
    {
        CacheLaunchPolicy? state = null;
        Assert.Throws<InvalidOperationException>((Action)(() =>
            CacheLaunchPolicy.Latch(ref state, "rebuild", () => throw new InvalidOperationException("settings-write"))));
        var second = CacheLaunchPolicy.Latch(ref state, "normal", () => Assert.Fail("must not consume again"));
        Assert.That(second.Action, Is.EqualTo(CacheAction.Rebuild));
    }
}
