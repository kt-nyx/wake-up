// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using UnityEngine;

namespace WakeUp.Tests;

[TestFixture]
public sealed class WarmTextureUploadContractTests
{
    [Test]
    public void ReviewedGogCoreAndPointerBodiesMatchTheAuthenticatedUploadContract()
    {
        string? managed = Environment.GetEnvironmentVariable("WAKE_UP_RIMWORLD_MANAGED_DIR");
        if (string.IsNullOrEmpty(managed)) Assert.Ignore("Exact GOG managed reference directory is required for binary identity evidence.");
        Assert.DoesNotThrow((Action)(() => WarmTextureUploadContract.ValidateBinary(Path.Combine(managed!, "UnityEngine.CoreModule.dll"))));
        // Test publicizing may rewrite metadata access flags. Validate the disk
        // supplier separately, then compare the actual loaded method bodies.
        Assert.DoesNotThrow((Action)(() => WarmTextureUploadContract.ValidateBodies(typeof(Texture2D).Assembly)));
        var pointer = AccessTools.Method(typeof(Texture2D), "LoadRawTextureData", new[] { typeof(IntPtr), typeof(int) });
        Assert.That(PngCache.Hex(CacheDigest.HashInput(pointer.GetMethodBody()!.GetILAsByteArray()!)),
            Is.EqualTo(WarmTextureUploadContract.PointerBodyHash));
        Assert.Throws<InvalidOperationException>((Action)(() => WarmTextureUploadContract.ValidateReference(typeof(string).Assembly)));
    }

    [Test]
    public void WarmArrayTargetsRemoveExactlyThePointerOverloadAndItsNativeImplementation()
    {
        var full = QualityUnityCallbacks.Targets().Distinct().ToArray();
        var array = QualityUnityCallbacks.WarmArrayTargets().Distinct().ToArray();
        var removed = full.Except(array).ToArray();
        Assert.That(removed, Has.Length.EqualTo(2));
        Assert.That(removed.All(WarmTextureUploadContract.IsPointerTarget), Is.True);
        Assert.That(array.Except(full), Is.Empty);
        Assert.That(array, Does.Contain(AccessTools.Method(typeof(Texture2D), "LoadRawTextureData", new[] { typeof(byte[]) })));
        Assert.That(array, Does.Contain(AccessTools.Method(typeof(Texture2D), "LoadRawTextureDataImplArray", new[] { typeof(byte[]) })));
        Assert.That(full.Count(WarmTextureUploadContract.IsPointerTarget), Is.EqualTo(2), "C08 retains both pointer targets.");
    }

    [Test]
    public void PointerGuardsIncludeReadableDispatchAndReachableExceptionConstruction()
    {
        var targets = WarmTextureUploadContract.Targets().ToArray();
        Assert.That(targets, Has.None.Null);
        foreach (var required in new MethodBase[] {
            AccessTools.PropertyGetter(typeof(Texture), "isReadable"),
            AccessTools.PropertyGetter(typeof(Texture2D), "isReadable"),
            AccessTools.Method(typeof(Texture), "CreateNonReadableException", new[] { typeof(Texture) }),
            AccessTools.PropertyGetter(typeof(UnityEngine.Object), "name"),
            AccessTools.Constructor(typeof(UnityException), new[] { typeof(string) }),
            AccessTools.Constructor(typeof(SystemException), new[] { typeof(string) }),
            AccessTools.Constructor(typeof(Exception), new[] { typeof(string) }),
            AccessTools.PropertySetter(typeof(Exception), "HResult"),
            AccessTools.Method(typeof(string), "Format", new[] { typeof(string), typeof(object) }),
            AccessTools.Method(typeof(IntPtr), "op_Equality", new[] { typeof(IntPtr), typeof(IntPtr) }) })
            Assert.That(targets, Does.Contain(required));
    }

    [Test]
    public void PointerOnlyPublicationChangesPreserveArrayAdmissionAndInvalidateFullC08()
    {
        foreach (var target in QualityUnityCallbacks.Targets().Where(WarmTextureUploadContract.IsPointerTarget))
        {
            var state = new Dictionary<MethodBase, byte[]>();
            var full = Capture(state, QualityUnityCallbacks.Targets());
            var array = Capture(state, QualityUnityCallbacks.WarmArrayTargets());
            var pointer = Capture(state, WarmTextureUploadContract.Targets());
            Assert.That(full.Unchanged && array.Unchanged && pointer.Unchanged, Is.True);
            state[target] = new byte[] { 1 };
            Assert.That(full.Unchanged, Is.False, "The full C08 policy must still notice " + target.Name);
            Assert.That(pointer.Unchanged, Is.False);
            Assert.That(array.Unchanged, Is.True, "The existing array path has no pointer callback dependency.");
        }
    }

    [Test]
    public void SharedArrayAndExceptionPublicationChangesPreventPointerConsumption()
    {
        foreach (var target in new MethodBase[] {
            AccessTools.Method(typeof(Texture2D), "LoadRawTextureData", new[] { typeof(byte[]) }),
            AccessTools.PropertyGetter(typeof(Texture2D), "isReadable"),
            AccessTools.Constructor(typeof(UnityException), new[] { typeof(string) }) })
        {
            var state = new Dictionary<MethodBase, byte[]>();
            var array = Capture(state, QualityUnityCallbacks.WarmArrayTargets());
            var pointer = Capture(state, WarmTextureUploadContract.Targets());
            state[target] = new byte[] { 1 };
            Assert.That(array.Unchanged && pointer.Unchanged, Is.False, target.ToString());
        }
    }

    private static PublishedPatchGuard.PublicationSet Capture(Dictionary<MethodBase, byte[]> state, IEnumerable<MethodBase> methods)
    {
        // Exercise the actual immutable-stamp checker without installing hooks
        // on Unity internal calls in a standalone test process.
        var targets = methods.Distinct().ToArray();
        return new PublishedPatchGuard.PublicationSet(state, targets, new byte[]?[targets.Length]);
    }
}
