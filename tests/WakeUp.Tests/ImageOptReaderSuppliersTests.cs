// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ImageOptReaderSuppliersTests
{
    [MethodImpl(MethodImplOptions.NoInlining)] private static int Load(int value) => value + 1;
    private static void Prefix() { }
    private static void Postfix() { }
    private static void Foreign() { }
    private static ImageOptReaderSuppliers Adapter(MethodInfo target)
        => (ImageOptReaderSuppliers)Activator.CreateInstance(typeof(ImageOptReaderSuppliers), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { target, target }, null)!;

    [Test]
    public void ExactPreviewPairIsRequiredAndLateForeignCallbackRevokesAdmission()
    {
        var target = AccessTools.Method(typeof(ImageOptReaderSuppliersTests), nameof(Load));
        var prefix = AccessTools.Method(typeof(ImageOptReaderSuppliersTests), nameof(Prefix));
        var postfix = AccessTools.Method(typeof(ImageOptReaderSuppliersTests), nameof(Postfix));
        var adapter = Adapter(target);
        AccessTools.Field(typeof(ImageOptReaderSuppliers), "previewPrefix").SetValue(adapter, prefix);
        AccessTools.Field(typeof(ImageOptReaderSuppliers), "previewPostfix").SetValue(adapter, postfix);
        var supplier = new Harmony("FasterGameLoadingMod");
        var foreign = new Harmony("WakeUp.Tests.ReaderForeign");
        try
        {
            supplier.Patch(target, prefix: new HarmonyMethod(prefix) { before = new[] { ImageSupplierPolicy.GraphicsOwner } });
            Assert.That(adapter.AllowsLoad(Harmony.GetPatchInfo(target).Prefixes[0]), Is.False, "Missing postfix must refuse");
            supplier.Patch(target, postfix: new HarmonyMethod(postfix) { before = new[] { ImageSupplierPolicy.GraphicsOwner } });
            Assert.That(PublishedPatchGuard.TryCreate(target, GiddyTextureRuntime.Owner, out var guard, true, adapter.AllowsLoad), Is.True);
            Assert.That(guard!.AllowsOriginalContract(), Is.True);
            foreign.Patch(target, postfix: new HarmonyMethod(typeof(ImageOptReaderSuppliersTests), nameof(Foreign)));
            Assert.That(guard.AllowsOriginalContract(), Is.False, "New foreign callback cannot inherit supplier admission");
        }
        finally { supplier.UnpatchAll(supplier.Id); foreign.UnpatchAll(foreign.Id); }
    }

    [Test]
    public void CurrentDestructionAndProviderChoicesAreRecheckedWithoutChangingThem()
    {
        var adapter = Adapter(AccessTools.Method(typeof(ImageOptReaderSuppliersTests), nameof(Load)));
        bool active = true, destroys = false;
        AccessTools.Field(typeof(ImageOptReaderSuppliers), "imageOptActive").SetValue(adapter, (Func<bool>)(() => active));
        AccessTools.Field(typeof(ImageOptReaderSuppliers), "destroysOriginals").SetValue(adapter, (Func<bool>)(() => destroys));
        Assert.That(adapter.AllowsCurrentState(out _, out _), Is.True);
        destroys = true;
        Assert.That(adapter.AllowsCurrentState(out var reason, out var code), Is.False);
        Assert.That(code, Is.EqualTo("image-original-destruction"));
        Assert.That(reason, Does.Contain("does not change this setting"));
        Assert.That(destroys, Is.True);
        destroys = false; active = false;
        Assert.That(adapter.AllowsCurrentState(out _, out code), Is.False);
        Assert.That(code, Is.EqualTo("image-producer-tuple"));
        active = true;
        Assert.That(adapter.AllowsCurrentState(out _, out _), Is.True);
    }
}
