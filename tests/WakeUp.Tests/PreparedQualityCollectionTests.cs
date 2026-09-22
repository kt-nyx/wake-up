// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HarmonyLib;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class PreparedQualityCollectionTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void OnlyActualCollectionInvokesBoundaryBeforeTouchingHolder(bool owner, bool collected)
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "quality-collection-" + Guid.NewGuid().ToString("N"));
        var storeField = AccessTools.Field(typeof(PreparedTextureRuntime), "startupStore");
        var selected = AccessTools.Property(typeof(PreparedTextureRuntime), "UseAtStartup");
        var policy = AccessTools.Field(typeof(CacheLaunchPolicy), "current");
        object? oldStore = storeField.GetValue(null), oldSelected = selected.GetValue(null), oldPolicy = policy.GetValue(null);
        var loaded = (IDictionary)AccessTools.Field(typeof(PreparedQualityRuntime), "loaded").GetValue(null);
        string slot = PreparationContract.Slot("collection-test", "image.png");
        Assert.That(loaded.Contains(slot), Is.False);
        try
        {
            policy.SetValue(null, null);
            using var store = new PreparedTextureStore(root);
            storeField.SetValue(null, store); selected.SetValue(null, true);
            if (owner)
                Assert.That(store.PublishQuality(new PreparationRecord {
                    Provider = "collection-test", Logical = "image.png", Physical = "source/image.png", SourceDigest = "digest",
                    Active = "order", Runtime = PreparationContract.RuntimeGog, Helper = "helper", Role = "mask", RoleIdentity = "pair",
                    Options = new PreparationOptions { Preset = 2 },
                    Output = new PreparationPixels { Width = 4, Height = 4, Mips = 3, TextureFormat = 4,
                        GraphicsFormat = 4, Filter = 2, Aniso = 2, Pixels = Enumerable.Repeat((byte)1, 84).ToArray() }
                }), Is.True);
            if (collected) loaded.Add(slot, null);
            int boundaries = 0;
            var stop = new InvalidOperationException("collection boundary reached");
            void Boundary() { boundaries++; throw stop; }
            // Null native objects are intentional: no-op decisions must not
            // touch them, and real collection must enter Boundary first.
            void Load() => PreparedQualityRuntime.Loaded(null!, "image.png", null!, "collection-test", null!, Boundary);
            if (owner && !collected) Assert.That(Assert.Throws<InvalidOperationException>((Action)Load), Is.SameAs(stop));
            else Assert.DoesNotThrow((Action)Load);
            Assert.That(boundaries, Is.EqualTo(owner && !collected ? 1 : 0));
        }
        finally
        {
            loaded.Remove(slot); storeField.SetValue(null, oldStore); selected.SetValue(null, oldSelected); policy.SetValue(null, oldPolicy);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
