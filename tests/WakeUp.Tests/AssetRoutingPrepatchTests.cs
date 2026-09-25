// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class AssetRoutingPrepatchTests
{
    private static ModuleDefinition Native() => ModuleDefinition.ReadModule(typeof(ContentFinder<>).Assembly.Location);

    [Test]
    public void ReleaseRoutesLeaveNativeAudioUntouchedAndDoNotRegisterExperimentalBootstrap()
    {
        using var module = Native();
        var targets = DeferredAudioPrepatch.Targets(module);
        var before = targets.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
        Assert.That(AssetRoutingPrepatch.RewriteAssembly(module), Is.True);
        Assert.That(targets.Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(before));
        foreach (var type in new[] { typeof(DeferredAudioPrepatch), typeof(PreparedAudioBootstrap) })
            Assert.That(type.GetMethods().SelectMany(m => m.GetCustomAttributesData())
                .Any(a => a.AttributeType.Name.StartsWith("FreePatch", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void FrozenNativeBodyAndRewrittenRuntimeIdentity()
    {
        using var module = Native();
        var method = AssetRoutingPrepatch.FindGet(module)!;
        string native = AssetRoutingPrepatch.Fingerprint(method);
        TestContext.Out.WriteLine("S11 native Cecil fingerprint: " + native);
        // Also produce the runtime admission constant directly from a rewritten
        // native copy; this does not detour or replace the loaded game assembly.
        AssetRoutingPrepatch.Inject(module, method);
        using var bytes = new MemoryStream();
        module.Write(bytes);
        Assembly copy = Assembly.Load(bytes.ToArray());
        var rewritten = copy.GetType("Verse.ContentFinder`1")!.GetMethod("Get")!;
        Assert.That(SemanticMethodIdentity.TryHash(rewritten, out string hash, out string reason), Is.True, reason);
        TestContext.Out.WriteLine("S11 rewritten runtime semantic fingerprint: " + hash);
        Assert.That(hash, Is.EqualTo(AssetRoutingRuntime.Bodies[0]));
        var targets = AssetRoutingRuntime.ContractMethods();
        for (int i = 1; i < targets.Length; i++)
        {
            var target = targets[i];
            Assert.That(SemanticMethodIdentity.TryHash(target, out string body, out string why), Is.True, why);
            Assert.That(body, Is.EqualTo(AssetRoutingRuntime.Bodies[i]));
            TestContext.Out.WriteLine(target.Name + ": " + body);
        }
        Assert.That(native, Is.EqualTo(AssetRoutingPrepatch.NativeBody));
    }

    [Test]
    public void PrefixPassesActualGenericTypeAndFalseBranchKeepsCompleteNativeBody()
    {
        using var module = Native();
        var method = AssetRoutingPrepatch.FindGet(module)!;
        var original = method.Body.Instructions.ToArray();
        var originalOperands = original.Select(i => i.Operand).ToArray();
        int locals = method.Body.Variables.Count;
        AssetRoutingPrepatch.Inject(module, method);
        var instructions = method.Body.Instructions;
        Assert.That(instructions.Take(12).Select(i => i.OpCode), Is.EqualTo(new[] {
            OpCodes.Ldtoken, OpCodes.Call, OpCodes.Ldarg_0, OpCodes.Ldloca, OpCodes.Call,
            OpCodes.Brfalse, OpCodes.Ldloc, OpCodes.Brtrue, OpCodes.Br, OpCodes.Ldloc, OpCodes.Unbox_Any, OpCodes.Ret }));
        Assert.That(instructions[0].Operand, Is.SameAs(method.DeclaringType.GenericParameters[0]));
        Assert.That(instructions[10].Operand, Is.SameAs(method.DeclaringType.GenericParameters[0]));
        Assert.That(instructions[5].Operand, Is.SameAs(original[0]));
        Assert.That(instructions[8].Operand, Is.SameAs(original.Single(i => i.OpCode == OpCodes.Ldarg_1)), "missing result resumes original diagnostics");
        Assert.That(instructions.Skip(12), Is.EqualTo(original));
        Assert.That(instructions.Skip(12).Select(i => i.Operand), Is.EqualTo(originalOperands));
        Assert.That(method.Body.Variables.Count, Is.EqualTo(locals + 1));
        var bridge = (MethodReference)instructions[4].Operand;
        Assert.That(bridge.DeclaringType.FullName, Is.EqualTo("WakeUp.AssetRoutingRuntime"));
        Assert.That(bridge.Parameters.Select(p => p.ParameterType.FullName),
            Is.EqualTo(new[] { "System.Type", "System.String", "System.Object&" }));
        Assert.That(original.Any(i => i.Operand is MethodReference m && m.Name == "TryFindAssetInModBundles"), Is.True);
        Assert.That(original.Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "UnityEngine.Resources"), Is.True);
    }

    [Test]
    public void ChangedOrAlreadyRewrittenMethodsAreUntouched()
    {
        using var module = Native();
        var method = AssetRoutingPrepatch.FindGet(module)!;
        method.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Nop));
        string changed = AssetRoutingPrepatch.Fingerprint(method);
        Assert.That(AssetRoutingPrepatch.RewriteAssembly(module), Is.True, "The unchanged bundle method has independent admission.");
        Assert.That(AssetRoutingPrepatch.Fingerprint(method), Is.EqualTo(changed));
        method.Body.Instructions.RemoveAt(0);
        Assert.That(AssetRoutingPrepatch.RewriteAssembly(module), Is.True);
        string injected = AssetRoutingPrepatch.Fingerprint(method);
        Assert.That(AssetRoutingPrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(method), Is.EqualTo(injected));
    }
}
