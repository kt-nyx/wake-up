// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class CompatibilityGuardTests
{
    [Test]
    public void FloatingOperandsCompareExactBitsIncludingSignedZero()
    {
        var first = new[] { new CodeInstruction(OpCodes.Ldc_R4, 0f), new CodeInstruction(OpCodes.Ldc_R8, 20d) };
        var same = new[] { new CodeInstruction(OpCodes.Ldc_R4, 0f), new CodeInstruction(OpCodes.Ldc_R8, 20d) };
        Assert.That(InstructionComparison.SameInstructions(first, same), Is.True);
        same[0].operand = BitConverter.ToSingle(new byte[] { 0, 0, 0, 128 }, 0);
        Assert.That(InstructionComparison.SameInstructions(first, same), Is.False);
        same[0].operand = 0f;
        same[1].operand = 21d;
        Assert.That(InstructionComparison.SameInstructions(first, same), Is.False);
    }
    [Test]
    public void IncomingInstructionComparisonNormalizesLabelsAndDetectsForeignChanges()
    {
        List<CodeInstruction> first = ComparisonBody(0), equivalent = ComparisonBody(5);
        Assert.That(InstructionComparison.SameInstructions(first, equivalent), Is.True);
        equivalent[0].operand = 2;
        Assert.That(InstructionComparison.SameInstructions(first, equivalent), Is.False);
        equivalent = ComparisonBody(5);
        equivalent[1].operand = equivalent[1].labels[0]; // Redirect branch to a different instruction.
        Assert.That(InstructionComparison.SameInstructions(first, equivalent), Is.False);
        equivalent = ComparisonBody(5);
        equivalent[2].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock));
        Assert.That(InstructionComparison.SameInstructions(first, equivalent), Is.False);
        equivalent = ComparisonBody(5);
        equivalent[0].operand = new object();
        Assert.That(InstructionComparison.SameInstructions(first, equivalent), Is.False);
        Assert.That(InstructionComparison.SameInstructions(equivalent, equivalent), Is.False, "Unknown operands never become trusted by reference equality.");
    }

    private static List<CodeInstruction> ComparisonBody(int extraLabels)
    {
        ILGenerator il = new DynamicMethod("Compare", typeof(void), Type.EmptyTypes).GetILGenerator();
        for (int index = 0; index < extraLabels; index++)
            il.DefineLabel();
        Label branch = il.DefineLabel(), destination = il.DefineLabel();
        var code = new List<CodeInstruction> { new(OpCodes.Ldc_I4, 1), new(OpCodes.Br, destination), new(OpCodes.Ret) };
        code[1].labels.Add(branch);
        code[2].labels.Add(destination);
        return code;
    }

    [Test]
    public void PublishedPatchGuardCachesReadsAndDetectsTheFirstLaterForeignTranspiler()
    {
        const string ownId = "WakeUp.Compatibility.Tests.Own";
        const string foreignId = "WakeUp.Compatibility.Tests.Foreign";
        var own = new Harmony(ownId);
        var foreign = new Harmony(foreignId);
        MethodInfo target = AccessTools.Method(typeof(CompatibilityGuardTests), nameof(PatchGuardTarget));
        var transpiler = new HarmonyMethod(typeof(CompatibilityGuardTests), nameof(IdentityTranspiler));
        try
        {
            own.Patch(target, transpiler: transpiler);
            Assert.That(PublishedPatchGuard.TryCreate(target, ownId, out PublishedPatchGuard? guard), Is.True);
            PublishedPatchGuard activeGuard = guard!;
            for (int i = 0; i < 200; i++)
                Assert.That(activeGuard.AllowsOriginalContract(), Is.True);
            Assert.That(activeGuard.PatchInfoReads, Is.EqualTo(1), "Unchanged state must not deserialize once per lookup.");

            // This is the first foreign addition, so a check made during its
            // wrapper rebuild would still see only our old published entry.
            foreign.Patch(target, transpiler: transpiler);
            Assert.That(activeGuard.AllowsOriginalContract(), Is.False, "The first call after publication must use native fallback.");
            for (int i = 0; i < 200; i++)
                Assert.That(activeGuard.AllowsOriginalContract(), Is.False);
            Assert.That(activeGuard.PatchInfoReads, Is.EqualTo(2));

            foreign.Unpatch(target, HarmonyPatchType.All, foreignId);
            Assert.That(activeGuard.AllowsOriginalContract(), Is.True);
            Assert.That(activeGuard.PatchInfoReads, Is.EqualTo(3));
        }
        finally
        {
            foreign.Unpatch(target, HarmonyPatchType.All, foreignId);
            own.Unpatch(target, HarmonyPatchType.All, ownId);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int PatchGuardTarget(int value) => value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SecondPatchGuardTarget(int value) => value + 2;

    private const string TransientOwner = "WakeUp.Guard.TransientPublication";
    private static void RemoveTransientPrefix(MethodBase __originalMethod)
        => new Harmony(TransientOwner).Unpatch(__originalMethod, HarmonyPatchType.All, TransientOwner);

    [Test]
    public void PublicationSetChecksEveryStampWithoutDeserializingAndDetectsSelfUnpatch()
    {
        var own = new Harmony("WakeUp.Guard.PublicationSet");
        var transient = new Harmony(TransientOwner);
        var targets = new[] { AccessTools.Method(typeof(CompatibilityGuardTests), nameof(PatchGuardTarget)),
            AccessTools.Method(typeof(CompatibilityGuardTests), nameof(SecondPatchGuardTarget)) };
        var guards = new List<PublishedPatchGuard>();
        try
        {
            foreach (var target in targets)
            {
                own.Patch(target, transpiler: new HarmonyMethod(typeof(CompatibilityGuardTests), nameof(IdentityTranspiler)));
                Assert.That(PublishedPatchGuard.TryCreate(target, own.Id, out var guard, allPatchKinds: true), Is.True);
                guards.Add(guard!);
            }
            Assert.That(PublishedPatchGuard.TryCapturePublications(guards, out var publications), Is.True);
            for (int i = 0; i < 200; i++) Assert.That(publications!.Unchanged, Is.True);
            Assert.That(guards.Select(g => g.PatchInfoReads), Is.EqualTo(new[] { 1, 1 }));

            // The later target gains a callback that removes itself. Its final
            // patch list/count again matches capture, but its publication does
            // not. Every target stamp must be compared, not just a count/first.
            transient.Patch(targets[1], prefix: new HarmonyMethod(typeof(CompatibilityGuardTests), nameof(RemoveTransientPrefix)));
            Assert.That(SecondPatchGuardTarget(1), Is.EqualTo(3));
            Assert.That(publications!.Unchanged, Is.False);
            Assert.That(guards.Select(g => g.PatchInfoReads), Is.EqualTo(new[] { 1, 1 }), "Checking stamps never calls GetPatchInfo.");
            Assert.That(guards[1].AllowsOriginalContract(), Is.True, "The transient foreign patch really removed itself.");
        }
        finally
        {
            foreach (var target in targets)
            {
                transient.Unpatch(target, HarmonyPatchType.All, TransientOwner);
                own.Unpatch(target, HarmonyPatchType.All, own.Id);
            }
        }
    }

    [Test]
    public void PublicationSetRejectsAnInitiallyForeignContract()
    {
        var foreign = new Harmony("WakeUp.Guard.InitiallyForeign");
        var target = AccessTools.Method(typeof(CompatibilityGuardTests), nameof(PatchGuardTarget));
        try
        {
            foreign.Patch(target, transpiler: new HarmonyMethod(typeof(CompatibilityGuardTests), nameof(IdentityTranspiler)));
            Assert.That(PublishedPatchGuard.TryCreate(target, "WakeUp.Guard.Owner", out var guard, allPatchKinds: true), Is.True);
            Assert.That(PublishedPatchGuard.TryCapturePublications(new[] { guard! }, out var publications), Is.False);
            Assert.That(publications, Is.Null);
        }
        finally { foreign.Unpatch(target, HarmonyPatchType.All, foreign.Id); }
    }

    [Test]
    public void PublicationCaptureRunsPredicatesOutsideLockAndRechecksEarlierTargets()
    {
        var own = new Harmony("WakeUp.Guard.CaptureOwner");
        var known = new Harmony("WakeUp.Guard.CaptureKnown");
        var first = AccessTools.Method(typeof(CompatibilityGuardTests), nameof(PatchGuardTarget));
        var second = AccessTools.Method(typeof(CompatibilityGuardTests), nameof(SecondPatchGuardTarget));
        bool lockWasAvailable = false;
        int predicateCalls = 0;
        try
        {
            own.Patch(first, transpiler: new HarmonyMethod(typeof(CompatibilityGuardTests), nameof(IdentityTranspiler)));
            known.Patch(second, transpiler: new HarmonyMethod(typeof(CompatibilityGuardTests), nameof(IdentityTranspiler)));
            Assert.That(PublishedPatchGuard.TryCreate(first, own.Id, out var firstGuard, allPatchKinds: true), Is.True);
            Assert.That(PublishedPatchGuard.TryCreate(second, own.Id, out var secondGuard, allPatchKinds: true,
                allowedForeignPatch: patch =>
                {
                    predicateCalls++;
                    var read = Task.Run(() => firstGuard!.Publication);
                    lockWasAvailable = read.Wait(TimeSpan.FromSeconds(5));
                    if (!lockWasAvailable) return false;
                    // The first target was already validated; a later callback
                    // changing it must refuse the complete captured set.
                    own.Unpatch(first, HarmonyPatchType.All, own.Id);
                    return patch.owner == known.Id;
                }), Is.True);
            Assert.That(PublishedPatchGuard.TryCapturePublications(new[] { firstGuard!, secondGuard! }, out var publications), Is.False);
            Assert.That(lockWasAvailable, Is.True);
            Assert.That(predicateCalls, Is.EqualTo(1));
            Assert.That(publications, Is.Null);
        }
        finally
        {
            own.Unpatch(first, HarmonyPatchType.All, own.Id);
            known.Unpatch(second, HarmonyPatchType.All, known.Id);
        }
    }

    [Test]
    public void NamedForeignContractAllowsOnlyTheKnownCallbackAndRechecksPublication()
    {
        const string owner = "WakeUp.Guard.Allowlist";
        var known = new Harmony("WakeUp.Guard.Known");
        var unknown = new Harmony("WakeUp.Guard.Unknown");
        MethodInfo target = AccessTools.Method(typeof(CompatibilityGuardTests), nameof(PatchGuardTarget));
        MethodInfo callback = AccessTools.Method(typeof(CompatibilityGuardTests), nameof(IdentityTranspiler));
        try
        {
            known.Patch(target, transpiler: new HarmonyMethod(callback));
            Assert.That(PublishedPatchGuard.TryCreate(target, owner, out var guard, true,
                p => p.owner == known.Id && Equals(p.PatchMethod, callback)), Is.True);
            Assert.That(guard!.AllowsOriginalContract(), Is.True);
            unknown.Patch(target, transpiler: new HarmonyMethod(callback));
            Assert.That(guard.AllowsOriginalContract(), Is.False, "Matching method under another owner is not admitted.");
            unknown.Unpatch(target, HarmonyPatchType.All, unknown.Id);
            Assert.That(guard.AllowsOriginalContract(), Is.True);
        }
        finally
        {
            known.Unpatch(target, HarmonyPatchType.All, known.Id);
            unknown.Unpatch(target, HarmonyPatchType.All, unknown.Id);
        }
    }

    private static IEnumerable<CodeInstruction> IdentityTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;

    [Test]
    public void BinaryIdentityAuthenticatesPinnedFilesWithoutResolvingXmlMembers()
    {
        Assert.That(RuntimeIdentity.ValidateBinaryIdentity(out string reason), Is.True, reason);
    }

}
