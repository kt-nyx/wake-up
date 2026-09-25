// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using Verse;
namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class CompatibilityIndependenceTests
{
    private static IEnumerable<CodeInstruction> Foreign(IEnumerable<CodeInstruction> instructions) => instructions;
    [TestCase(false)]
    [TestCase(true)]
    public void OccupiedOrDeselectedWorkersDoNotDisableQueryPlans(bool deselected)
    {
        var foreign = new Harmony("WakeUp.Tests.OccupiedWorkers");
        var own = new Harmony("wakeup.def-lookup");
        var targets = (IList)AccessTools.Field(typeof(DefLookupRuntime), "Targets").GetValue(null);
        var guards = (IList)AccessTools.Field(typeof(DefLookupRuntime), "Guards").GetValue(null);
        var singleGuards = (IList)AccessTools.Field(typeof(DefLookupRuntime), "SingleGuards").GetValue(null);
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "xml-split-" + Guid.NewGuid().ToString("N"));
        object? state = null;
        try
        {
            Assert.That(RuntimeIdentity.ValidateBinaryIdentity(out string reason), Is.True, reason);
            if (!deselected)
                foreach (string suffix in new[] { "Add", "AddModExtension", "Insert", "Remove", "Replace", "SetName", "AttributeAdd", "AttributeRemove", "AttributeSet", "Test", "Conditional" })
                    foreign.Patch(AccessTools.Method(typeof(PatchOperation).Assembly.GetType("Verse.PatchOperation" + suffix), "ApplyWorker"),
                        transpiler: new HarmonyMethod(typeof(CompatibilityIndependenceTests), nameof(Foreign)));
            var args = new List<string> { "--wake-up-mode=candidate", "--wake-up-strategy=def-lookup", "--wake-up-query-extensions=on", "-savedatafolder=" + root };
            if (deselected) args.Add("--wake-up-user-def-search=off");
            DefLookupRuntime.TryInitialize(args);
            Assert.That(targets, Is.Empty);
            Assert.That(XPathPlanRuntime.Installed, Is.True);
            Assert.That(AccessTools.Field(typeof(DefLookupRuntime), "singleMemoInstalled").GetValue(null), Is.True);
            var document = new XmlDocument(); document.LoadXml("<Defs><ThingDef><defName>A</defName></ThingDef></Defs>");
            object?[] stage = { document, true, null };
            AccessTools.Method(typeof(DefLookupRuntime), "StagePrefix").Invoke(null, stage); state = stage[2];
            Assert.That(state, Is.Not.Null);
            MethodInfo nodes = AccessTools.Method(typeof(XmlNode), "SelectNodes", new[] { typeof(string) });
            Assert.That(((XmlNodeList)nodes.Invoke(document, new object[] { "Defs/ThingDef" })).Count, Is.EqualTo(1));
            Assert.That(((XmlNodeList)nodes.Invoke(document, new object[] { "Defs/ThingDef" })).Count, Is.EqualTo(1));
            var plans = (ScopedXPathPlans)state!.GetType().GetField("Plans", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state);
            Assert.That(plans.Hits, Is.GreaterThan(0));
        }
        finally
        {
            if (state != null) AccessTools.Method(typeof(DefLookupRuntime), "StageFinalizer").Invoke(null, new[] { null, state });
            own.UnpatchAll(own.Id); foreign.UnpatchAll(foreign.Id); ExtendedXmlQueryRuntime.Uninstall(); XPathPlanRuntime.Reset();
            targets.Clear(); guards.Clear(); singleGuards.Clear();
            foreach (string field in new[] { "attempted", "installed", "candidate", "consumed", "queryExtensions", "singleMemoInstalled" }) AccessTools.Field(typeof(DefLookupRuntime), field).SetValue(null, false);
            AccessTools.Field(typeof(DefLookupRuntime), "evidencePath").SetValue(null, null);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    [TestCase(true)] [TestCase(false)] public void OccupiedTypePreservesIndependentLeafAndReflection(bool leafOccupied)
    {
        var foreign = new Harmony("WakeUp.Tests.OccupiedCodeSearches");
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "type-split-" + Guid.NewGuid().ToString("N"));
        var saved = new Dictionary<FieldInfo, object?>();
        foreach (var pair in new[] { (typeof(TypeLookupRuntime), "attempted"), (typeof(TypeLookupRuntime), "candidate"),
            (typeof(TypeLookupRuntime), "installed"), (typeof(TypeSearchLifetime), "selected"), (typeof(TypeSearchLifetime), "finished") })
        { var field = AccessTools.Field(pair.Item1, pair.Item2); saved.Add(field, field.GetValue(null)); }
        try
        {
            foreign.Patch(AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName)),
                transpiler: new HarmonyMethod(typeof(CompatibilityIndependenceTests), nameof(Foreign)));
            if (leafOccupied) foreign.Patch(AccessTools.Method(typeof(GenTypes), nameof(GenTypes.AllLeafSubclasses)),
                transpiler: new HarmonyMethod(typeof(CompatibilityIndependenceTests), nameof(Foreign)));
            TypeSearchLifetime.Initialize(new[] { "--wake-up-mode=candidate", "--wake-up-strategy=type-lookup", "-savedatafolder=" + root });
            Assert.That(AccessTools.Field(typeof(TypeLookupRuntime), "installed").GetValue(null), Is.False);
            Assert.That(AccessTools.Field(typeof(LeafSubclassRuntime), "installed").GetValue(null), Is.EqualTo(!leafOccupied));
            Assert.That(AccessTools.Field(typeof(LoadingReflectionRuntime), "installed").GetValue(null), Is.True);
            Assert.That(TypeSearchLifetime.Active, Is.True);
            Assert.That(Harmony.GetPatchInfo(AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName))).Owners, Does.Contain(foreign.Id));
        }
        finally
        {
            LoadingReflectionRuntime.Complete();
            AppDomain.CurrentDomain.AssemblyLoad -= (AssemblyLoadEventHandler)Delegate.CreateDelegate(typeof(AssemblyLoadEventHandler),
                AccessTools.Method(typeof(LoadingReflectionRuntime), "AssemblyLoaded"));
            foreach (string owner in new[] { "wakeup.type-lookup", TypeSearchLifetime.Owner, "wakeup.leaf-subclasses", "wakeup.loading-reflection", foreign.Id }) new Harmony(owner).UnpatchAll(owner);
            AccessTools.Field(typeof(LoadingReflectionRuntime), "installed").SetValue(null, false);
            AccessTools.Field(typeof(LeafSubclassRuntime), "installed").SetValue(null, false);
            ((IDictionary)AccessTools.Field(typeof(LoadingReflectionRuntime), "guards").GetValue(null)).Clear();
            foreach (var pair in saved) pair.Key.SetValue(null, pair.Value);
            AccessTools.Field(typeof(TypeLookupRuntime), "evidencePath").SetValue(null, null);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    [Test] public void ReflectionOverloadsHaveDistinctStableOperationIds()
    {
        var methods = LoadingReflectionRuntime.ContractMethods();
        Assert.That(methods.Select(LoadingReflectionRuntime.OperationId).Distinct().Count(), Is.EqualTo(methods.Length));
    }
}
