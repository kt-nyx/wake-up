// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class DefLookupHandoffTests
{
    private const string Own = "wakeup.def-lookup", Foreign = "WakeUp.Tests.WorkerSupplier";
    private static string rewrite = "replace";
    private static bool supplierAlreadyPublished, injectDuringInstall;
    private static readonly MethodInfo Query = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectNodes), new[] { typeof(string) })!;
    private static readonly MethodInfo Target = AccessTools.Method(typeof(Worker), nameof(Worker.ApplyWorker));
    private static readonly MethodInfo Healthy = AccessTools.Method(typeof(HealthyWorker), nameof(HealthyWorker.ApplyWorker));
    private readonly Harmony own = new(Own), foreign = new(Foreign), interception = new("WakeUp.Tests.InstallInterception");
    private readonly IList targets = (IList)AccessTools.Field(typeof(DefLookupRuntime), "Targets").GetValue(null);
    private readonly IList guards = (IList)AccessTools.Field(typeof(DefLookupRuntime), "Guards").GetValue(null);
    private readonly HashSet<MethodBase> yielded = AccessTools.Field(typeof(DefLookupRuntime), "YieldedWorkers")?.GetValue(null) as HashSet<MethodBase> ?? new();
    private readonly PropertyInfo registryProperty = typeof(CompatibilityStatus).GetProperty("Registry", BindingFlags.Static | BindingFlags.NonPublic)!;
    private object? savedRegistry;
    private OperationRegistry registry = null!;
    private string root = "";

    private sealed class Worker
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ApplyWorker(XmlDocument document) => document.SelectNodes("/Defs/Wanted")!.Count != 0;
    }
    private sealed class HealthyWorker
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ApplyWorker(XmlDocument document) => document.SelectNodes("/Defs/Survivor")!.Count != 0;
    }
    [SetUp] public void SetUp()
    {
        Assert.That(targets, Is.Empty); Assert.That(guards, Is.Empty);
        yielded.Clear(); injectDuringInstall = false; supplierAlreadyPublished = false;
        root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "worker-handoff-" + Guid.NewGuid().ToString("N"));
        savedRegistry = registryProperty.GetValue(null);
        registry = new OperationRegistry(Path.Combine(root, "notices.xml"), _ => { });
        registryProperty.SetValue(null, registry);
        CompatibilityStatus.Declare("definitions", "Definition queries");
    }
    [TearDown] public void TearDown()
    {
        injectDuringInstall = false;
        interception.UnpatchAll(interception.Id);
        own.UnpatchAll(Own); foreign.UnpatchAll(Foreign);
        targets.Clear(); guards.Clear(); yielded.Clear();
        registryProperty.SetValue(null, savedRegistry);
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private void Install(Type type) => AccessTools.Method(typeof(DefLookupRuntime), "InstallWorker").Invoke(null, new object[] { own, type });
    private static XmlNodeList SupplierQuery(XmlNode node, string expression) => node.SelectNodes("/Defs/Other")!;
    private static IEnumerable<CodeInstruction> SupplierRewrite(IEnumerable<CodeInstruction> instructions)
    {
        supplierAlreadyPublished = Harmony.GetPatchInfo(Target)?.Transpilers.Any(p => p.owner == Foreign) == true;
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (rewrite == "replace")
        {
            var call = code.Single(i => Equals(i.operand, Query));
            call.opcode = OpCodes.Call; call.operand = AccessTools.Method(typeof(DefLookupHandoffTests), nameof(SupplierQuery));
        }
        else if (rewrite == "expression") code.Single(i => i.opcode == OpCodes.Ldstr).operand = "/Defs/Other";
        else if (rewrite == "multiple")
            code.InsertRange(0, new[] { new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldstr, "/Defs/Other"),
                new CodeInstruction(OpCodes.Callvirt, Query), new CodeInstruction(OpCodes.Pop) });
        return code;
    }
    private void InstallSupplier() => foreign.Patch(Target, transpiler: new HarmonyMethod(typeof(DefLookupHandoffTests), nameof(SupplierRewrite)) { priority = Priority.First });

    [TestCase("replace", false)]
    [TestCase("replace", true)]
    [TestCase("expression", false)]
    [TestCase("expression", true)]
    [TestCase("multiple", false)]
    [TestCase("multiple", true)]
    public void ActualHarmonyChainYieldsOnlyAffectedWorkerInEitherOrder(string change, bool supplierFirst)
    {
        rewrite = change;
        if (supplierFirst) InstallSupplier();
        Install(typeof(Worker)); Install(typeof(HealthyWorker));
        if (!supplierFirst)
        {
            Assert.That(registry.Snapshot().Any(s => s.StartsWith("Definition query: Worker: Available")), Is.True);
            Assert.DoesNotThrow((Action)InstallSupplier);
            Assert.That(supplierAlreadyPublished, Is.False, "The first foreign rebuild sees the previous published record.");
        }
        Assert.That(Harmony.GetPatchInfo(Target)!.Transpilers.Any(p => p.owner == Foreign), Is.True);
        Assert.That(registry.Snapshot().Any(s => s.StartsWith("Definition query: Worker: Unavailable")), Is.True);
        Assert.That(registry.Snapshot().Any(s => s.StartsWith("Definition query: HealthyWorker: Available")), Is.True);
        var document = new XmlDocument(); document.LoadXml("<Defs><Other/><Survivor/></Defs>");
        using var scope = new ScopedDefLookup(document);
        var active = AccessTools.Field(typeof(DefLookupRuntime), "active");
        active.SetValue(null, scope);
        try
        {
            Assert.That(new Worker().ApplyWorker(document), Is.EqualTo(change != "multiple"));
            Assert.That(new HealthyWorker().ApplyWorker(document), Is.True);
            Assert.That(scope.QueryCount, Is.EqualTo(1), "Only the independent worker uses Wake-Up's lookup.");
        }
        finally { active.SetValue(null, null); }
        Assert.That(registry.Pending(), Has.Length.EqualTo(1));
        Assert.That(registry.Acknowledge(registry.Pending().Select(n => n.Key)), Is.True);
        foreign.Patch(Target, postfix: new HarmonyMethod(typeof(DefLookupHandoffTests), nameof(EmptyPostfix)));
        Assert.That(registry.Pending(), Is.Empty, "A later rebuild must not repeat an acknowledged decision.");
    }
    private static void EmptyPostfix() { }
    private static void BeforeOwnPatch(Harmony __instance, MethodBase __0)
    {
        if (!injectDuringInstall || __instance.Id != Own || __0 != Target) return;
        injectDuringInstall = false;
        new Harmony(Foreign).Patch(Target, transpiler: new HarmonyMethod(typeof(DefLookupHandoffTests), nameof(SupplierRewrite)) { priority = Priority.First });
    }
    [Test] public void ChangeBetweenAdmissionAndInstallationCannotBeOverwrittenByAvailable()
    {
        rewrite = "replace";
        interception.Patch(AccessTools.Method(typeof(Harmony), nameof(Harmony.Patch)),
            prefix: new HarmonyMethod(typeof(DefLookupHandoffTests), nameof(BeforeOwnPatch)));
        injectDuringInstall = true;
        Install(typeof(Worker));
        Assert.That(injectDuringInstall, Is.False);
        Assert.That(registry.Snapshot().Any(s => s.StartsWith("Definition query: Worker: Unavailable")), Is.True);
        Assert.That(registry.Pending(), Has.Length.EqualTo(1));
        Assert.That(Harmony.GetPatchInfo(Target)!.Transpilers.Any(p => p.owner == Foreign), Is.True);
    }
    [Test] public void UnsupportedInputPassesThroughWithoutChangingLabelsOrExceptionBlocks()
    {
        Install(typeof(Worker));
        var code = PatchProcessor.GetOriginalInstructions(Target);
        var query = code.Single(i => Equals(i.operand, Query));
        var generator = new DynamicMethod("Labels", typeof(void), Type.EmptyTypes).GetILGenerator();
        query.labels.Add(generator.DefineLabel());
        query.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        code.Add(new CodeInstruction(OpCodes.Nop)); // Incoming body differs, including metadata.
        var snapshot = code.Select(i => new CodeInstruction(i)).ToArray();
        var result = DefLookupRuntime.Transpiler(code, Target).ToArray();
        Assert.That(result, Has.Length.EqualTo(snapshot.Length));
        for (int i = 0; i < result.Length; i++)
        {
            Assert.That(result[i].opcode, Is.EqualTo(snapshot[i].opcode));
            Assert.That(result[i].operand, Is.EqualTo(snapshot[i].operand));
            Assert.That(result[i].labels, Is.EqualTo(snapshot[i].labels));
            Assert.That(result[i].blocks, Is.EqualTo(snapshot[i].blocks));
            Assert.That(code[i].labels, Is.EqualTo(snapshot[i].labels));
            Assert.That(code[i].blocks, Is.EqualTo(snapshot[i].blocks));
        }
    }
}
