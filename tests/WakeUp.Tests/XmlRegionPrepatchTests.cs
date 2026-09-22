// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class XmlRegionPrepatchTests
{
    [Test]
    public void RejectedExperimentHasNoProductTypesSettingOrAutomaticRegistration()
    {
        var product = typeof(WakeUpSettings).Assembly;
        Assert.That(product.GetType("WakeUp.XmlRegionRuntime"), Is.Null);
        Assert.That(product.GetType("WakeUp.XmlRegionPrepatch"), Is.Null);
        Assert.That(typeof(WakeUpSettings).GetField("XmlRegionReuse"), Is.Null);
        Assert.That(typeof(XmlRegionPrepatch).GetMethod("RewriteAssembly")!.GetCustomAttributes(false), Is.Empty);
    }

    [Test]
    public void ExactNativeCallsiteRewritePreservesIterationCatchLocalsAndOtherXmlHooks()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        var apply = XmlRegionPrepatch.FindApplyPatches(module)!;
        var load = module.GetType("Verse.LoadedModManager").Methods.Single(m => m.Name == "LoadAllActiveMods");
        var ctor = StreamingXmlPrepatch.FindConstructor(module)!;
        string loadHash = AssetRoutingPrepatch.Fingerprint(load), ctorHash = AssetRoutingPrepatch.Fingerprint(ctor);
        Assert.That(AssetRoutingPrepatch.Fingerprint(apply), Is.EqualTo(XmlRegionPrepatch.NativeBody));
        var instructions = apply.Body.Instructions.ToArray();
        var handlers = apply.Body.ExceptionHandlers.Select(h => new[] { h.TryStart, h.TryEnd, h.HandlerStart, h.HandlerEnd, h.FilterStart }).ToArray();
        var variables = apply.Body.Variables.ToArray(); int stack = apply.Body.MaxStackSize;
        var original = apply.Body.Instructions.Single(i => i.OpCode == OpCodes.Callvirt && i.Operand is MethodReference m && m.DeclaringType.FullName == "Verse.PatchOperation" && m.Name == "Apply");
        Assert.That(XmlRegionPrepatch.RewriteAssembly(module), Is.True);
        Assert.That(apply.Body.Instructions.ToArray(), Is.EqualTo(instructions), "The exact existing instruction remains inside native foreach/catch.");
        Assert.That(original.OpCode, Is.EqualTo(OpCodes.Call));
        var bridge = (MethodReference)original.Operand;
        Assert.That(bridge.DeclaringType.FullName, Is.EqualTo("WakeUp.XmlRegionRuntime"));
        Assert.That(bridge.Name, Is.EqualTo("ApplyOne"));
        Assert.That(bridge.HasThis, Is.False);
        Assert.That(bridge.ReturnType.FullName, Is.EqualTo("System.Boolean"));
        Assert.That(bridge.Parameters.Select(p => p.ParameterType.FullName), Is.EqualTo(new[] { "System.Object", "System.Xml.XmlDocument" }));
        Assert.That(apply.Body.Variables.ToArray(), Is.EqualTo(variables));
        Assert.That(apply.Body.MaxStackSize, Is.EqualTo(stack));
        Assert.That(apply.Body.ExceptionHandlers.Select(h => new[] { h.TryStart, h.TryEnd, h.HandlerStart, h.HandlerEnd, h.FilterStart }).ToArray(), Is.EqualTo(handlers));
        Assert.That(AssetRoutingPrepatch.Fingerprint(load), Is.EqualTo(loadHash));
        Assert.That(AssetRoutingPrepatch.Fingerprint(ctor), Is.EqualTo(ctorHash));
    }

    [Test]
    public void CurrentEffectiveMethodFingerprintsMatchActualRewrittenGameCallsite()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        Assert.That(XmlRegionPrepatch.RewriteAssembly(module), Is.True);
        using var bytes = new MemoryStream(); module.Write(bytes);
        File.WriteAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "xml-region-native.dll"), bytes.ToArray());
        Assembly rewritten = Assembly.Load(bytes.ToArray());
        var methods = XmlRegionContract.Methods();
        Assert.That(methods.Length, Is.EqualTo(XmlRegionContract.Bodies.Length));
        for (int i = 0; i < methods.Length; i++)
        {
            MethodBase method = i == 0 ? rewritten.GetType("Verse.LoadedModManager")!.GetMethod("ApplyPatches")! : methods[i];
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            Assert.That(hash, Is.EqualTo(XmlRegionContract.Bodies[i]), method.DeclaringType!.FullName + "." + method.Name);
        }
    }

    [Test]
    public void RegionAndStreamingConstructorRewritesCoexistInEitherOrder()
    {
        using var first = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        using var second = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        Assert.That(StreamingXmlPrepatch.RewriteAssembly(first), Is.True);
        string streaming = AssetRoutingPrepatch.Fingerprint(StreamingXmlPrepatch.FindConstructor(first)!);
        Assert.That(XmlRegionPrepatch.RewriteAssembly(first), Is.True);
        Assert.That(AssetRoutingPrepatch.Fingerprint(StreamingXmlPrepatch.FindConstructor(first)!), Is.EqualTo(streaming));
        Assert.That(XmlRegionPrepatch.RewriteAssembly(second), Is.True);
        Assert.That(StreamingXmlPrepatch.RewriteAssembly(second), Is.True);
        Assert.That(AssetRoutingPrepatch.Fingerprint(XmlRegionPrepatch.FindApplyPatches(first)!), Is.EqualTo(AssetRoutingPrepatch.Fingerprint(XmlRegionPrepatch.FindApplyPatches(second)!)));
        Assert.That(AssetRoutingPrepatch.Fingerprint(StreamingXmlPrepatch.FindConstructor(first)!), Is.EqualTo(AssetRoutingPrepatch.Fingerprint(StreamingXmlPrepatch.FindConstructor(second)!)));
    }

    [Test]
    public void ChangedAndPreviouslyRewrittenCallsitesRefuseWithoutFurtherMutation()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        var apply = XmlRegionPrepatch.FindApplyPatches(module)!;
        apply.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Nop));
        string changed = AssetRoutingPrepatch.Fingerprint(apply);
        Assert.That(XmlRegionPrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(apply), Is.EqualTo(changed));
        apply.Body.Instructions.RemoveAt(0);
        Assert.That(XmlRegionPrepatch.RewriteAssembly(module), Is.True);
        string rewritten = AssetRoutingPrepatch.Fingerprint(apply);
        Assert.That(XmlRegionPrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(apply), Is.EqualTo(rewritten));
    }

    private static MethodInfo EmptyHookMethod => AccessTools.Method(typeof(XmlRegionPrepatchTests), nameof(EmptyHook));
    private static void EmptyHook() { }
    private static Dictionary<MethodBase, byte[]> Publications =>
        (Dictionary<MethodBase, byte[]>)AccessTools.Field(typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")!, "state").GetValue(null);
    private static void WithPublished(MethodBase target, string category, string owner, MethodInfo callback, Action check)
    {
        var state = Publications; byte[]? previous;
        lock (state) state.TryGetValue(target, out previous);
        Type serialization = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfoSerialization")!;
        Type infoType = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfo")!;
        object info = previous == null ? Activator.CreateInstance(infoType)!
            : AccessTools.Method(serialization, "Deserialize").Invoke(null, new object[] { previous });
        try
        {
            AccessTools.Method(infoType, category).Invoke(info, new object[] { owner, new[] { new HarmonyMethod(callback) } });
            var published = (byte[])AccessTools.Method(serialization, "Serialize").Invoke(null, new[] { info });
            lock (state) state[target] = published;
            check();
            lock (state) Assert.That(state[target], Is.SameAs(published), "Admission must not change external hook registrations.");
        }
        finally { lock (state) { if (previous == null) state.Remove(target); else state[target] = previous; } }
    }

    [TestCase("AddPrefixes")]
    [TestCase("AddPostfixes")]
    [TestCase("AddTranspilers")]
    [TestCase("AddFinalizers")]
    [TestCase("AddInnerPrefixes")]
    [TestCase("AddInnerPostfixes")]
    public void EveryForeignWorkerHookCategoryRefusesAtUseAndOwnOwnerStringCannotSpoofApproval(string category)
    {
        var target = AccessTools.Method(typeof(PatchOperationAdd), "ApplyWorker");
        var guards = XmlRegionContract.CreateGuards(new MethodBase[] { target });
        Assert.That(guards.Allows(out _), Is.True);
        int reads = guards.PatchInfoReads;
        Assert.That(guards.Allows(out _), Is.True);
        Assert.That(guards.PatchInfoReads, Is.EqualTo(reads), "Unchanged publications use their stored byte-array identity.");
        foreach (string owner in new[] { "foreign.region.test", XmlRegionContract.Owner, "wakeup.def-lookup" })
            WithPublished(target, category, owner, EmptyHookMethod, () => Assert.That(guards.Allows(out _), Is.False));
        Assert.That(guards.Allows(out _), Is.True);
    }

    [Test]
    public void ExactExistingSearchHooksRemainAllowedButWrongCategoriesRefuse()
    {
        var worker = AccessTools.Method(typeof(PatchOperationAdd), "ApplyWorker");
        var stage = AccessTools.Method(typeof(LoadedModManager), "ApplyPatches");
        var guards = XmlRegionContract.CreateGuards(new MethodBase[] { worker, stage });
        WithPublished(worker, "AddTranspilers", "wakeup.def-lookup", AccessTools.Method(typeof(DefLookupRuntime), "Transpiler"),
            () => Assert.That(guards.Allows(out _), Is.True));
        WithPublished(stage, "AddPrefixes", "wakeup.def-lookup", AccessTools.Method(typeof(DefLookupRuntime), "StagePrefix"),
            () => WithPublished(stage, "AddFinalizers", "wakeup.def-lookup", AccessTools.Method(typeof(DefLookupRuntime), "StageFinalizer"),
                () => Assert.That(guards.Allows(out _), Is.True)));
        WithPublished(stage, "AddInnerPrefixes", "wakeup.def-lookup", AccessTools.Method(typeof(DefLookupRuntime), "StagePrefix"),
            () => Assert.That(guards.Allows(out _), Is.False));
        WithPublished(worker, "AddPostfixes", "wakeup.def-lookup", AccessTools.Method(typeof(DefLookupRuntime), "Transpiler"),
            () => Assert.That(guards.Allows(out _), Is.False));
    }

    [Test]
    public void ExactRegionStageCallbacksAreTheOnlyAllowedRegionHooks()
    {
        Type runtime = typeof(XmlRegionContract).Assembly.GetType("WakeUp.XmlRegionRuntime", true)!;
        var stage = AccessTools.Method(typeof(LoadedModManager), "ApplyPatches");
        var guards = XmlRegionContract.CreateGuards(new MethodBase[] { stage });
        WithPublished(stage, "AddPrefixes", XmlRegionContract.Owner, AccessTools.Method(runtime, "StagePrefix"),
            () => WithPublished(stage, "AddFinalizers", XmlRegionContract.Owner, AccessTools.Method(runtime, "StageFinalizer"),
                () => Assert.That(guards.Allows(out _), Is.True)));
        WithPublished(stage, "AddInnerPrefixes", XmlRegionContract.Owner, AccessTools.Method(runtime, "StagePrefix"),
            () => Assert.That(guards.Allows(out _), Is.False));
    }

    [TestCase("AddPrefixes")]
    [TestCase("AddPostfixes")]
    [TestCase("AddTranspilers")]
    [TestCase("AddFinalizers")]
    [TestCase("AddInnerPrefixes")]
    [TestCase("AddInnerPostfixes")]
    public void NewlyPublishedXmlLibraryHooksAreNotHiddenByEarlierAdmission(string category)
    {
        var guards = XmlRegionContract.CreateGuards(Array.Empty<MethodBase>());
        Assert.That(guards.Allows(out _), Is.True);
        var target = typeof(XmlDocument).GetMethod("ImportNode", new[] { typeof(XmlNode), typeof(bool) })!;
        WithPublished(target, category, "foreign.xml.library", EmptyHookMethod,
            () => { Assert.That(guards.Allows(out string reason), Is.False); Assert.That(reason, Is.EqualTo("foreign-system-xml-hook")); });
        Assert.That(guards.Allows(out _), Is.True);
    }
}
