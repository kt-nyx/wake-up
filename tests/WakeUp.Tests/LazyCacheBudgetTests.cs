// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class LazyCacheBudgetTests
{
    private string root = "";
    private readonly Dictionary<FieldInfo, object?> saved = new();
    private static CacheLaunchPolicy Policy(string action = "normal")
    { CacheLaunchPolicy? policy = null; return CacheLaunchPolicy.Latch(ref policy, action, () => { }); }
    [SetUp] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "wakeup-lazy-budget-" + Guid.NewGuid().ToString("N"));
        foreach (string name in new[] { "current", "failedRoot", "configuredRoot", "configuredMiB", "configuredPolicy" })
        {
            var field = typeof(SharedCacheBudget).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            saved[field] = field.GetValue(null); field.SetValue(null, name == "configuredMiB" ? (object)0 : null);
        }
    }
    [TearDown] public void Cleanup()
    {
        var field = typeof(SharedCacheBudget).GetField("current", BindingFlags.Static | BindingFlags.NonPublic)!;
        (field.GetValue(null) as IDisposable)?.Dispose();
        foreach (var pair in saved) pair.Key.SetValue(null, pair.Value);
        saved.Clear();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    [Test] public void SearchOnlySelectionAndReadOnlyPreviewDoNotCreateStores()
    {
        SharedCacheBudget.Configure(root, 7, Policy());
        Assert.That(SharedCacheBudget.ForRoot(Path.Combine(root, "WakeUp", "PreparedTextures", "v1"), acquire: false), Is.Null);
        Assert.That(Directory.Exists(root), Is.False);
    }
    [Test] public void LaterPreparationAcquiresConfiguredCapAndCountsOtherStores()
    {
        string other = Path.Combine(root, "WakeUp", "ParsedXml", "v0");
        Directory.CreateDirectory(other); File.WriteAllBytes(Path.Combine(other, "retained"), new byte[321]);
        SharedCacheBudget.Configure(root, 7, Policy());
        string category = Path.Combine(root, "WakeUp", "PreparedTextures", "v1");
        var budget = SharedCacheBudget.ForRoot(category)!;
        Assert.That(budget.MaximumBytes, Is.EqualTo(7L * 1024 * 1024));
        Assert.That(budget.StoredBytes, Is.EqualTo(321));
        using var store = new OwnedCacheStore(category, 2048, budget.MaximumBytes, Policy());
        Assert.That(store.Publish(new string('A', 64), new byte[100]), Is.True);
        Assert.That(budget.StoredBytes, Is.GreaterThan(421));
    }
    [Test] public void FailedLazyAcquisitionCannotFallBackToIndependentWrites()
    {
        string wake = Path.Combine(root, "WakeUp"); Directory.CreateDirectory(wake);
        string category = Path.Combine(wake, "PreparedTextures", "v1");
        SharedCacheBudget.Configure(root, 7, Policy());
        using (var held = new FileStream(Path.Combine(wake, ".budget-owner"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>((Action)(() => SharedCacheBudget.ForRoot(category)));
        Assert.Throws<IOException>((Action)(() => SharedCacheBudget.ForRoot(category)));
    }
    [Test] public void BypassDoesNotAcquireOrCreateStores()
    {
        SharedCacheBudget.Configure(root, 7, Policy("bypass"));
        Assert.That(SharedCacheBudget.ForRoot(Path.Combine(root, "WakeUp", "PreparedTextures", "v1")), Is.Null);
        Assert.That(Directory.Exists(root), Is.False);
    }
}
