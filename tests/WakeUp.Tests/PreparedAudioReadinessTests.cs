// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mono.Cecil;
using NUnit.Framework;
using UnityEngine;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PreparedAudioReadinessTests
{
    [Test]
    public void OriginalReferenceContractEvidence()
    {
        var targets = PreparedAudioReadiness.Targets();
        string[] actual = targets.Select(method =>
        {
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            TestContext.Out.WriteLine("PREPARED_READINESS " + SemanticMethodIdentity.Signature(method) + "|" + hash);
            return hash;
        }).ToArray();
        Assert.That(actual, Is.EqualTo(PreparedAudioReadiness.Expected), "Pin these ORIGINAL reference hashes before enabling readiness.");
    }

    [Test, NonParallelizable]
    public void EffectiveContractDerivesOnlyFromKnownOriginalPrepatchChanges()
    {
        using var module = ModuleDefinition.ReadModule(typeof(Verse.SongDef).Assembly.Location);
        MethodDefinition[] Find(ModuleDefinition source) => PreparedAudioReadiness.Targets().Select(target =>
            source.GetType(target.DeclaringType!.FullName).Methods.Single(method => method.Name == target.Name
                && method.Parameters.Select(p => p.ParameterType.FullName)
                    .SequenceEqual(target.GetParameters().Select(p => p.ParameterType.FullName)))).ToArray();
        var methods = Find(module);
        var expectedBodies = methods.Select(StructuralBody).ToArray();
        var substitutions = methods.Select(method => method.Body.Instructions.Select((instruction, index) => new { instruction, index })
            .Where(item => item.instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldfld
                && item.instruction.Operand is FieldReference field && field.DeclaringType.FullName == "Verse.SongDef" && field.Name == "clip")
            .Select(item => item.index).ToArray()).ToArray();
        Assert.That(substitutions.Select(items => items.Length), Is.EqualTo(new[] { 2, 0, 0 }));

        // Same deterministic pipeline as DeferredAudioPrepatchTests. It includes
        // the owned branch widening; these three consumers are not widening targets.
        AssetRoutingPrepatch.Inject(module, AssetRoutingPrepatch.FindGet(module)!);
        DeferredAudioPrepatch.InjectNative(module, DeferredAudioPrepatch.Targets(module));
        for (int i = 0; i < methods.Length; i++)
        {
            string[] effective = StructuralBody(methods[i]);
            foreach (int index in substitutions[i])
            {
                var instruction = methods[i].Body.Instructions[index];
                Assert.That(instruction.OpCode.Code, Is.EqualTo(Mono.Cecil.Cil.Code.Call));
                Assert.That(instruction.Operand, Is.TypeOf<MethodReference>());
                var bridge = (MethodReference)instruction.Operand;
                Assert.That(bridge.FullName, Is.EqualTo("UnityEngine.AudioClip WakeUp.DeferredAudioRuntime::GetSongClip(Verse.SongDef)"));
                Assert.That(bridge.HasThis, Is.False);
                // Permit precisely the validated owned replacement; every
                // other instruction, local and body flag must remain unchanged.
                expectedBodies[i][index + 1] = effective[index + 1];
            }
            Assert.That(effective, Is.EqualTo(expectedBodies[i]), methods[i].FullName);
        }

        using var bytes = new MemoryStream();
        module.Write(bytes);
        byte[] serialized = bytes.ToArray();
        using (var roundTrip = ModuleDefinition.ReadModule(new MemoryStream(serialized)))
        {
            var written = Find(roundTrip);
            for (int i = 0; i < written.Length; i++)
                Assert.That(StructuralBody(written[i]), Is.EqualTo(expectedBodies[i]), "Serialization changed the effective consumer.");
        }
        var copy = Assembly.Load(serialized);
        string[] actual = PreparedAudioReadiness.Targets(copy).Select(method =>
        {
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            TestContext.Out.WriteLine("PREPARED_READINESS_EFFECTIVE " + SemanticMethodIdentity.Signature(method) + "|" + hash);
            return hash;
        }).ToArray();
        Assert.That(actual, Is.EqualTo(PreparedAudioReadiness.EffectiveExpected), "Pin only these offline-derived effective hashes.");
    }

    private static string[] StructuralBody(MethodDefinition method)
    {
        var body = method.Body;
        // Cecil recomputes MaxStackSize when writing. Preserve all executable
        // structure here; the final runtime hash still pins its serialized value.
        string Operand(object? operand) => operand switch
        {
            Mono.Cecil.Cil.Instruction target => "target:" + body.Instructions.IndexOf(target),
            Mono.Cecil.Cil.Instruction[] targets => "targets:" + string.Join(",", targets.Select(body.Instructions.IndexOf)),
            MemberReference member => member.FullName,
            Mono.Cecil.Cil.VariableDefinition variable => "local:" + variable.Index,
            ParameterDefinition parameter => "arg:" + parameter.Index,
            _ => Convert.ToString(operand, System.Globalization.CultureInfo.InvariantCulture) ?? ""
        };
        string header = method.Attributes + "|" + method.ImplAttributes + "|" + body.InitLocals + "|"
            + string.Join(";", body.Variables.Select(variable => variable.VariableType.FullName));
        return new[] { header }.Concat(body.Instructions.Select(instruction => instruction.OpCode.Code + "|" + Operand(instruction.Operand)))
            .Concat(body.ExceptionHandlers.Select(handler => "eh:" + handler.HandlerType + "|" + handler.CatchType?.FullName
                + "|" + body.Instructions.IndexOf(handler.TryStart) + "|" + body.Instructions.IndexOf(handler.TryEnd)
                + "|" + body.Instructions.IndexOf(handler.HandlerStart) + "|" + body.Instructions.IndexOf(handler.HandlerEnd)
                + "|" + body.Instructions.IndexOf(handler.FilterStart))).ToArray();
    }

    [Test]
    public void OriginalConsumersKeepTheirClipAssignmentAndAllPlaybackCalls()
    {
        var setter = AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.clip));
        var helper = AccessTools.Method(typeof(PreparedAudioReadiness), "Prepare");
        foreach (var target in PreparedAudioReadiness.Targets())
        {
            var original = PatchProcessor.GetOriginalInstructions(target).ToList();
            int site = original.FindIndex(c => Equals(c.operand, setter));
            Assert.That(site, Is.GreaterThanOrEqualTo(0));
            int count = original.Count;
            var changed = PreparedAudioReadiness.RewriteAssignment(original, target).ToList();
            Assert.That(changed.Count, Is.EqualTo(count + 2), target.ToString());
            Assert.That(changed[site].opcode, Is.EqualTo(OpCodes.Ldstr));
            Assert.That(changed[site + 1].opcode, Is.EqualTo(OpCodes.Call));
            Assert.That(changed[site + 1].operand, Is.EqualTo(helper));
            Assert.That(changed[site + 2].operand, Is.EqualTo(setter));
            changed.RemoveRange(site, 2);
            Assert.That(changed, Is.EqualTo(original), "Only the existing clip argument may be wrapped.");
        }
    }

    [Test, NonParallelizable]
    public void ReadinessRejectsExistingConsumerAndLateSetterPublications()
    {
        var consumer = AccessTools.Method(typeof(PreparedAudioReadinessTests), nameof(ManagedConsumer));
        var setter = AccessTools.Method(typeof(PreparedAudioReadinessTests), nameof(ManagedSetter));
        var patch = new HarmonyMethod(typeof(PreparedAudioReadinessTests), nameof(ForeignPrefix));
        var foreign = new Harmony("WakeUp.Tests.PreparedReadiness.Foreign");
        var guardsField = AccessTools.Field(typeof(PreparedAudioReadiness), "guards");
        var activeField = AccessTools.Field(typeof(PreparedAudioReadiness), "active");
        var helper = AccessTools.Method(typeof(PreparedAudioReadiness), "Prepare");
        object? previousGuards = guardsField.GetValue(null);
        object? previousActive = activeField.GetValue(null);
        try
        {
            foreign.Patch(consumer, prefix: patch);
            Assert.That(PublishedPatchGuard.TryCreate(consumer, PreparedAudioRuntime.Owner, out var consumerGuard, allPatchKinds: true), Is.True);
            Assert.That(PublishedPatchGuard.TryCreate(setter, PreparedAudioRuntime.Owner, out var setterGuard, allPatchKinds: true), Is.True);
            // Only the music slot and shared setter slot are exercised here;
            // the pinned-native tests above qualify actual target binding.
            guardsField.SetValue(null, new[] { consumerGuard!, consumerGuard!, consumerGuard!, setterGuard! });
            activeField.SetValue(null, true);
            Assert.That(PreparedAudioReadiness.Allowed("music"), Is.False, "An existing foreign consumer patch must refuse preparation.");
            Assert.That(helper.Invoke(null, new object?[] { null, "music" }), Is.Null);

            foreign.Unpatch(consumer, HarmonyPatchType.All, foreign.Id);
            Assert.That(PreparedAudioReadiness.Allowed("music"), Is.True, "Removing the foreign publication restores the original contract.");

            foreign.Patch(setter, prefix: patch);
            Assert.That(PreparedAudioReadiness.Allowed("music"), Is.False, "The first check after a late setter publication must refuse preparation.");
            Assert.That(helper.Invoke(null, new object?[] { null, "music" }), Is.Null);
            foreign.Unpatch(setter, HarmonyPatchType.All, foreign.Id);
            Assert.That(PreparedAudioReadiness.Allowed("music"), Is.True);
        }
        finally
        {
            try
            {
                foreign.Unpatch(consumer, HarmonyPatchType.All, foreign.Id);
                foreign.Unpatch(setter, HarmonyPatchType.All, foreign.Id);
            }
            finally
            {
                guardsField.SetValue(null, previousGuards);
                activeField.SetValue(null, previousActive);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ManagedConsumer(int value) => value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ManagedSetter(int value) => value + 2;

    private static void ForeignPrefix() { }
}
