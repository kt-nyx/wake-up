// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NUnit.Framework;
using RimWorldLoadingOptimizer.RimWorld;

namespace RimWorldLoadingOptimizer.RimWorld.Tests;

[TestFixture]
public sealed class GiddyTextureRuntimeTests
{
    [Test]
    public void ExactIdentityRejectsSameVersionModifiedOrUpdatedSupplier()
    {
        Assert.That(GiddyTextureRuntime.MatchesSupplierIdentity("2.2.5.0", GiddyTextureRuntime.SupplierMvid, GiddyTextureRuntime.SupplierSha256), Is.True);
        Assert.That(GiddyTextureRuntime.MatchesSupplierIdentity("2.2.5.0", GiddyTextureRuntime.SupplierMvid, new string('0', 64)), Is.False);
        Assert.That(GiddyTextureRuntime.MatchesSupplierIdentity("2.2.5.0", Guid.Empty, GiddyTextureRuntime.SupplierSha256), Is.False);
        Assert.That(GiddyTextureRuntime.MatchesSupplierIdentity("1.0.0.0", GiddyTextureRuntime.SupplierMvid, GiddyTextureRuntime.SupplierSha256), Is.False);
    }

    [TestCase(HarmonyPatchType.Prefix)]
    [TestCase(HarmonyPatchType.Postfix)]
    [TestCase(HarmonyPatchType.Transpiler)]
    [TestCase(HarmonyPatchType.Finalizer)]
    public void ConversionHelperGuardRejectsEachForeignPatchKind(HarmonyPatchType kind)
    {
        string managed = Environment.GetEnvironmentVariable("RLO_RIMWORLD_MANAGED_DIR")!;
        string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed))!, "Mods", "3674332861", "1.6", "Assemblies", "GiddyUpCore.dll");
        Type supplier = Assembly.LoadFrom(path).GetType("GiddyUp.TextureUtility", true)!;
        MethodInfo target = AccessTools.Method(supplier, "GetBackHeight");
        var foreign = new Harmony("Rlo.Tests.CombatForeign");
        var patch = new HarmonyMethod(typeof(GiddyTextureRuntimeTests), kind == HarmonyPatchType.Transpiler ? nameof(Identity) : nameof(Noop));
        Assert.That(PublishedPatchGuard.TryCreate(target, GiddyTextureRuntime.Owner, out var guard, allPatchKinds: true), Is.True);
        Assert.That(guard!.AllowsOriginalContract(), Is.True);
        try
        {
            foreign.Patch(target, prefix: kind == HarmonyPatchType.Prefix ? patch : null,
                postfix: kind == HarmonyPatchType.Postfix ? patch : null,
                transpiler: kind == HarmonyPatchType.Transpiler ? patch : null,
                finalizer: kind == HarmonyPatchType.Finalizer ? patch : null);
            Assert.That(guard.AllowsOriginalContract(), Is.False);
            Assert.That(PublishedPatchGuard.TryCreate(target, GiddyTextureRuntime.Owner, out var newGuard, allPatchKinds: true), Is.True);
            Assert.That(newGuard!.AllowsOriginalContract(), Is.False);
        }
        finally { foreign.UnpatchAll(foreign.Id); }
        Assert.That(guard.AllowsOriginalContract(), Is.True);
    }

    private static void Noop()
    {
    }
    private static System.Collections.Generic.IEnumerable<CodeInstruction> Identity(System.Collections.Generic.IEnumerable<CodeInstruction> code) => code;

    [Test]
    public void PhysicalSupplierAndMethodBodiesMatch()
    {
        string managed = Environment.GetEnvironmentVariable("RLO_RIMWORLD_MANAGED_DIR")!;
        string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed))!, "Mods", "3674332861", "1.6", "Assemblies", "GiddyUpCore.dll");
        Assembly supplier = Assembly.LoadFrom(path);
        Assert.That(GiddyTextureRuntime.ValidateSupplier(supplier, path), Is.True);
        Assert.That(GiddyTextureRuntime.ValidateBodies(supplier), Is.True);
        var body = PatchProcessor.GetOriginalInstructions(AccessTools.Method(supplier.GetType("GiddyUp.TextureUtility"), "SetDrawOffset"));
        Assert.That(InstructionComparison.SameInstructions(body, PatchProcessor.GetOriginalInstructions(AccessTools.Method(supplier.GetType("GiddyUp.TextureUtility"), "SetDrawOffset"))), Is.True);
    }
}
