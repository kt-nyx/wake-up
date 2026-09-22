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
public sealed class DeferredAudioPrepatchTests
{
    private static ModuleDefinition Native() => ModuleDefinition.ReadModule(typeof(ContentFinder<>).Assembly.Location);

    [Test]
    public void FrozenCoreContractsAndRewrittenRoutingIdentities()
    {
        using var module = Native();
        var targets = DeferredAudioPrepatch.Targets(module);
        for (int i = 0; i < targets.Length; i++)
            TestContext.Out.WriteLine($"AUDIO_NATIVE_{i} {targets[i].FullName}: {AssetRoutingPrepatch.Fingerprint(targets[i])}");
        AssetRoutingPrepatch.Inject(module, AssetRoutingPrepatch.FindGet(module)!);
        DeferredAudioPrepatch.InjectNative(module, targets);
        using var bytes = new MemoryStream();
        module.Write(bytes);
        var copy = Assembly.Load(bytes.ToArray());
        int identityIndex = 0;
        foreach (var pair in new[] { ("Verse.ContentFinder`1", "Get"), ("Verse.ModContentHolder`1", "Get"), ("Verse.ModContentPack", "GetContentHolder"), ("Verse.ModContentHolder`1", "ReloadAll") })
        {
            // Match production's closed texture holder. Hashing the open generic
            // type misses the concrete typeof(T) tokens introduced by the bridge.
            var type = copy.GetType(pair.Item1)!;
            if (pair.Item1 == "Verse.ModContentHolder`1") type = type.MakeGenericType(typeof(UnityEngine.Texture2D));
            var method = type.GetMethod(pair.Item2)!;
            if (method.IsGenericMethodDefinition) method = method.MakeGenericMethod(typeof(UnityEngine.Texture2D));
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            TestContext.Out.WriteLine($"AUDIO_ROUTING {pair.Item1}.{pair.Item2}: {hash}");
            if (identityIndex < 3) Assert.That(hash, Is.EqualTo(AssetRoutingRuntime.DeferredBodies[identityIndex]));
            else Assert.That(PngRuntime.BodySupported(GameBuildContract.SteamRev590, "ReloadAll", hash), Is.True);
            identityIndex++;
        }
        Assert.That(copy.GetType("Verse.SongDef")!.GetMethod("WakeUpDeferredAudioContract")!.Invoke(null, null), Is.EqualTo(3));
        var effective = DeferredAudioContract.Methods(copy).Select(m =>
        {
            Assert.That(SemanticMethodIdentity.TryHash(m, out string hash, out string reason), Is.True, reason);
            TestContext.Out.WriteLine("AUDIO_EFFECTIVE " + m.Name + " " + hash);
            return hash;
        }).ToArray();
        Assert.That(effective, Is.EqualTo(DeferredAudioContract.EffectiveBodies));
        DeferredAudioContract.Validate(copy);
        using var original = Native();
        Assert.That(DeferredAudioPrepatch.Targets(original).Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(DeferredAudioPrepatch.NativeBodies));
    }

    [TestCase(2)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(11)]
    public void ChangedCoreRefusesBeforeAnyAudioMutation(int changed)
    {
        using var module = Native();
        var targets = DeferredAudioPrepatch.Targets(module);
        targets[changed].Body.Instructions.Insert(0, Instruction.Create(OpCodes.Nop));
        var hashes = targets.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
        Assert.That(DeferredAudioPrepatch.RewriteNative(module), Is.False);
        Assert.That(targets.Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(hashes));
        Assert.That(module.GetType("Verse.SongDef").Methods.Any(m => m.Name == "WakeUpDeferredAudioContract"), Is.False);
    }

    [Test]
    public void CoreLookupBypassesExposureButNativeProviderAndFallbackRemain()
    {
        using var module = Native();
        AssetRoutingPrepatch.Inject(module, AssetRoutingPrepatch.FindGet(module)!);
        DeferredAudioPrepatch.InjectNative(module, DeferredAudioPrepatch.Targets(module));
        var get = AssetRoutingPrepatch.FindGet(module)!;
        var calls = get.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
        Assert.That(calls.Any(m => m.Name == "GetHolder"), Is.True);
        Assert.That(calls.Any(m => m.Name == "GetContentHolder"), Is.False);
        Assert.That(calls.Any(m => m.DeclaringType.FullName == "UnityEngine.Resources"), Is.True);
        Assert.That(calls.Any(m => m.Name == "TryFindAssetInModBundles"), Is.True);
        Assert.That(DeferredAudioPrepatch.Types(module).SelectMany(t => t.Methods).Where(m => m.HasBody)
            .SelectMany(m => m.Body.Instructions).Any(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference f && f.DeclaringType.FullName == "Verse.SongDef" && f.Name == "clip"), Is.False);
        var getter = module.GetType("Verse.ModContentPack").Methods.Single(m => m.Name == "GetContentHolder");
        foreach (var ret in getter.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret))
            Assert.That(ret.Previous.Operand is MethodReference m && m.Name == "ExposeHolder", Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExternalReadIsRewrittenButAddressAccessRefuses(bool address)
    {
        using var module = ModuleDefinition.CreateModule("DeferredAudioConsumerTest", ModuleKind.Dll);
        var reference = AssemblyNameReference.Parse(typeof(SongDef).Assembly.FullName);
        module.AssemblyReferences.Add(reference);
        var consumer = new TypeDefinition("Test", "Consumer", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(consumer);
        var song = new TypeReference("Verse", "SongDef", module, reference);
        var clipType = module.ImportReference(typeof(UnityEngine.AudioClip));
        var field = new FieldReference("clip", clipType, song);
        var method = new MethodDefinition("Read", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, address ? new ByReferenceType(clipType) : clipType);
        method.Parameters.Add(new ParameterDefinition(song));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        method.Body.Instructions.Add(Instruction.Create(address ? OpCodes.Ldflda : OpCodes.Ldfld, field));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        consumer.Methods.Add(method);
        Assert.That(DeferredAudioPrepatch.RewriteConsumers(module), Is.EqualTo(!address));
        Assert.That(module.GetType(DeferredAudioPrepatch.ConsumerMarker) != null, Is.EqualTo(!address));
        Assert.That(method.Body.Instructions[1].OpCode, Is.EqualTo(address ? OpCodes.Ldflda : OpCodes.Call));
        Assert.That(DeferredAudioPrepatch.RewriteConsumers(module), Is.False);
        if (!address)
        {
            using var bytes = new MemoryStream(); module.Write(bytes);
            var assembly = Assembly.Load(bytes.ToArray());
            Assert.That(assembly.GetType("Test.Consumer")!.GetMethod("Read")!.Invoke(null, new object[] { new SongDef() }), Is.Null);
        }
    }
    [Test]
    public void KnownReflectedSongFieldConsumerIsNotMarkedAdmitted()
    {
        using var module = ModuleDefinition.CreateModule("ReflectedAudioConsumer", ModuleKind.Dll);
        module.AssemblyReferences.Add(AssemblyNameReference.Parse(typeof(SongDef).Assembly.FullName));
        var consumer = new TypeDefinition("Test", "Consumer", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        var method = new MethodDefinition("Read", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Object);
        method.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(FieldInfo))));
        method.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "clip"));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, module.ImportReference(typeof(FieldInfo).GetMethod("GetValue"))));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        consumer.Methods.Add(method); module.Types.Add(consumer);
        Assert.That(DeferredAudioPrepatch.RewriteConsumers(module), Is.False);
        Assert.That(module.GetType(DeferredAudioPrepatch.ConsumerMarker), Is.Null);
    }
}
