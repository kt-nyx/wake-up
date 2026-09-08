// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
[NonParallelizable]
public sealed class PngLoadingProgressTests
{
    private static PngLoadingProgressBridge Bridge()
    {
        string managed = Environment.GetEnvironmentVariable("WAKE_UP_RIMWORLD_MANAGED_DIR")!;
        string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed))!, "Mods", "3535481557", "1.6", "Assemblies", "ilyvion.LoadingProgress.dll");
        using var sha = SHA256.Create();
        Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""),
            Is.EqualTo("4F19F836FD41C8E702295CB90D7D5E66C53F38CDA8A13A3ED79F1D063E1AFCEF"));
        return new PngLoadingProgressBridge(Assembly.LoadFrom(path));
    }

    [Test]
    public void SupplierIteratorChangesOnlyTextureCallAndCanBePatched()
    {
        var bridge = Bridge();
        var before = PatchProcessor.GetOriginalInstructions(bridge.Iterator);
        var after = PngRuntime.LoadingProgressTranspiler(before).ToList();
        Assert.That(after.Count, Is.EqualTo(before.Count));
        var changed = Enumerable.Range(0, before.Count).Where(i => !Equals(before[i].operand, after[i].operand)).ToArray();
        Assert.That(changed.Length, Is.EqualTo(1));
        int at = changed.Single();
        Assert.That(before[at].operand, Is.EqualTo(PngRuntime.Holder));
        var replacement = (MethodInfo)after[at].operand;
        Assert.That(replacement.GetParameters().Select(p => p.ParameterType),
            Is.EqualTo(new[] { PngRuntime.Holder.DeclaringType!, typeof(bool) }));
        Assert.That(replacement.ReturnType, Is.EqualTo(typeof(void)));
        // This also compares branch labels and exception regions: all yields,
        // progress/profiler calls and non-texture loaders must remain intact.
        after[at] = new CodeInstruction(before[at]);
        Assert.That(InstructionComparison.SameInstructions(before, after), Is.True);
        var own = new Harmony(PngRuntime.Owner);
        try
        {
            own.Patch(bridge.Iterator, transpiler: new HarmonyMethod(typeof(PngRuntime), nameof(PngRuntime.LoadingProgressTranspiler)));
            Assert.That(bridge.Compatible(), Is.True);
        }
        finally { own.Unpatch(bridge.Iterator, HarmonyPatchType.All, own.Id); }
    }

    [Test]
    public void ExactLoadingProgressPrefixesAreAllowedButUnknownCallbacksAreNot()
    {
        var bridge = Bridge();
        var supplier = new Harmony(LoadingProgressCompatibility.PackageId);
        Assert.That(PublishedPatchGuard.TryCreate(PngRuntime.Reload, PngRuntime.Owner, out var guard, true, bridge.AllowsReloadPatch), Is.True);
        try
        {
            foreach (var prefix in bridge.ReviewedMethods.Skip(2))
                supplier.Patch(PngRuntime.Reload, prefix: new HarmonyMethod(prefix));
            Assert.That(guard!.AllowsOriginalContract(), Is.True);
            // Reproduce the former refusal even on the Windows reference build.
            Assert.That(PublishedPatchGuard.TryCreate(PngRuntime.Reload, PngRuntime.Owner, out var oldGuard, true), Is.True);
            Assert.That(oldGuard!.AllowsOriginalContract(), Is.False);
            supplier.Patch(PngRuntime.Reload, prefix: new HarmonyMethod(typeof(PngLoadingProgressTests), nameof(Noop)));
            Assert.That(guard.AllowsOriginalContract(), Is.False, "An owner name alone cannot authenticate a callback.");
        }
        finally { supplier.Unpatch(PngRuntime.Reload, HarmonyPatchType.All, supplier.Id); }
        Assert.That(guard!.AllowsOriginalContract(), Is.True);
    }

    [TestCase(HarmonyPatchType.Prefix)]
    [TestCase(HarmonyPatchType.Postfix)]
    [TestCase(HarmonyPatchType.Transpiler)]
    [TestCase(HarmonyPatchType.Finalizer)]
    public void LaterForeignIteratorPatchStopsBridgeAdmission(HarmonyPatchType kind)
    {
        var bridge = Bridge();
        var foreign = new Harmony("WakeUp.Tests.ForeignPngLoader");
        var patch = new HarmonyMethod(typeof(PngLoadingProgressTests), kind == HarmonyPatchType.Transpiler ? nameof(Identity) : nameof(Noop));
        try
        {
            Assert.That(bridge.Compatible(), Is.True);
            foreign.Patch(bridge.Iterator, prefix: kind == HarmonyPatchType.Prefix ? patch : null,
                postfix: kind == HarmonyPatchType.Postfix ? patch : null,
                transpiler: kind == HarmonyPatchType.Transpiler ? patch : null,
                finalizer: kind == HarmonyPatchType.Finalizer ? patch : null);
            Assert.That(bridge.Compatible(), Is.False);
            Assert.Throws<InvalidOperationException>(new Action(() => Bridge()));
        }
        finally { foreign.Unpatch(bridge.Iterator, HarmonyPatchType.All, foreign.Id); }
        Assert.That(bridge.Compatible(), Is.True);
    }

    private static void Noop() { }
    private static IEnumerable<CodeInstruction> Identity(IEnumerable<CodeInstruction> code) => code;
}
