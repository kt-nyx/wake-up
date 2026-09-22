// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class DeferredAssetSetTests
{
    [Test]
    public void RegistrationAvoidsCreationAndFirstSuccessPublishesOnce()
    {
        var set = new DeferredAssetSet<string, object>();
        int created = 0, published = 0;
        object value = new();
        Assert.That(set.Add("song", "source", 2), Is.True);
        Assert.That(set.Add("song", "duplicate", 2), Is.True);
        Assert.That(created, Is.Zero);
        Assert.That(set.Complete("song", source => { Assert.That(source, Is.EqualTo("source")); created++; return value; },
            (key, item) => { Assert.That(item, Is.SameAs(value)); published++; return true; }, _ => Assert.Fail()), Is.True);
        Assert.That(set.Complete("song", _ => throw new Exception(), (_, _) => true, _ => Assert.Fail()), Is.False);
        Assert.That((created, published, set.Count), Is.EqualTo((1, 1, 0)));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void MissingOrThrowingLoadRemainsRetryable(bool throws)
    {
        var set = new DeferredAssetSet<int, object>();
        set.Add("a", 1, 1);
        if (throws) Assert.Throws<InvalidOperationException>(new Action(() => { set.Complete("a", _ => throw new InvalidOperationException(), (_, _) => true, _ => Assert.Fail()); }));
        else Assert.That(set.Complete("a", _ => null, (_, _) => true, _ => Assert.Fail()), Is.False);
        Assert.That(set.Count, Is.EqualTo(1));
        Assert.That(set.Complete("a", _ => new object(), (_, _) => true, _ => Assert.Fail()), Is.True);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void FailedPublicationDiscardsOnlyTemporaryValueAndRetainsWork(bool throws)
    {
        var set = new DeferredAssetSet<int, object>();
        set.Add("a", 1, 1);
        int discarded = 0;
        if (throws) Assert.Throws<InvalidOperationException>(new Action(() => { set.Complete("a", _ => new object(), (_, _) => throw new InvalidOperationException(), _ => discarded++); }));
        else Assert.That(set.Complete("a", _ => new object(), (_, _) => false, _ => discarded++), Is.False);
        Assert.That((set.Count, discarded), Is.EqualTo((1, 1)));
    }
    [Test]
    public void CancellationDuringLoadPreventsStalePublicationAndAllowsNewGeneration()
    {
        var set = new DeferredAssetSet<int, object>();
        set.Add("a", 1, 1);
        int discarded = 0;
        Assert.That(set.Complete("a", _ => { set.Cancel(); set.Add("a", 2, 1); return new object(); }, (_, _) => throw new Exception(), _ => discarded++), Is.False);
        Assert.That(discarded, Is.EqualTo(1));
        Assert.That(set.Complete("a", source => { Assert.That(source, Is.EqualTo(2)); return new object(); }, (_, _) => true, _ => Assert.Fail()), Is.True);
    }
    [Test]
    public void NestedDuplicateDoesNotLoadAgainAndBoundsDoNotDropExistingWork()
    {
        var set = new DeferredAssetSet<int, object>();
        set.Add("a", 1, 1);
        Assert.That(set.Add("b", 2, 1), Is.False);
        Assert.That(set.Complete("a", _ =>
        {
            Assert.That(set.Complete("a", _ => throw new Exception(), (_, _) => true, _ => Assert.Fail()), Is.False);
            return new object();
        }, (_, _) => true, _ => Assert.Fail()), Is.True);
    }
}

