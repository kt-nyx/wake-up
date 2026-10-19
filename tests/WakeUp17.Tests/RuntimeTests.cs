// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;
using WakeUp.V17;

namespace WakeUp17.Tests;

[TestFixture, NonParallelizable]
public sealed class RuntimeTests
{
    private const string Foreign = "wakeup17.tests.foreign";
    private RuntimeContext context = null!;
    private readonly List<string> logs = new();
    private readonly List<Action> cleanup = new();

    [SetUp]
    public void SetUp()
    {
        logs.Clear(); cleanup.Clear();
        context = new RuntimeContext(typeof(RuntimeTests).Assembly, logs.Add, _ => true);
        Synthetic.Nested = null; Synthetic.Throw = false;
        NativeTypes.Types = new List<Type> { typeof(Base), typeof(Child), typeof(Leaf), typeof(Other) };
        NativeTypes.Cache = new Dictionary<Type, List<Type>>();
    }
    [TearDown]
    public void TearDown()
    {
        context.CompleteStartup();
        foreach (Action action in cleanup) action();
        foreach (string feature in new[] { "definitions", "type-names", "leaf-classes", "members", "lifetime" }) context.Unpatch(feature);
        new Harmony(Foreign).UnpatchAll(Foreign);
    }

    private static MethodInfo M(string name) => AccessTools.Method(typeof(Synthetic), name);
    private static XmlDocument Xml(string name = "Beer")
    {
        var xml = new XmlDocument();
        xml.LoadXml("<Defs><ThingDef><defName>" + name + "</defName><label>native</label></ThingDef></Defs>");
        return xml;
    }
    private DefinitionSearches Definitions()
    {
        var result = new DefinitionSearches(context); cleanup.Add(result.Clear);
        result.Install(M(nameof(Synthetic.Stage)), new[] { M(nameof(Synthetic.Nodes)), M(nameof(Synthetic.Single)) });
        return result;
    }
    [Test]
    public void ActualXmlPatchesReuseQueriesAndPreserveOriginalNodes()
    {
        var xml = Xml(); var expected = xml.DocumentElement!.FirstChild;
        Definitions(); Synthetic.Stage(xml);
        Assert.That(Synthetic.Last, Is.SameAs(expected));
        Assert.That(context.Report, Does.Contain("3 indexed queries"));
        Assert.That(xml.SelectSingleNode("Defs/ThingDef/label")!.InnerText, Is.EqualTo("changed"));
    }
    [Test]
    public void StageCallbacksRemainNativeWhileWorkerQueriesAreReused()
    {
        new Harmony(Foreign).Patch(M(nameof(Synthetic.Stage)),
            prefix: new HarmonyMethod(typeof(RuntimeTests), nameof(ChangeStageLabel)),
            postfix: new HarmonyMethod(typeof(RuntimeTests), nameof(AfterStage)));
        Definitions(); var xml = Xml(); Synthetic.Stage(xml);
        Assert.That(context.Report, Does.Contain("3 indexed queries"));
        Assert.That(xml.DocumentElement!.GetAttribute("callback"), Is.EqualTo("completed"));
        Assert.That(xml.SelectSingleNode("Defs/ThingDef/label")!.InnerText, Is.EqualTo("changed"));
    }
    [Test]
    public void SupplierSkippingTheStageDoesNotBuildOrConsumeAnIndex()
    {
        new Harmony(Foreign).Patch(M(nameof(Synthetic.Stage)),
            prefix: new HarmonyMethod(typeof(RuntimeTests), nameof(SkipStage)));
        Definitions(); var xml = Xml(); Synthetic.Stage(xml);
        Assert.That(context.Report, Does.Not.Contain("indexed queries"));
        Assert.That(xml.SelectSingleNode("Defs/ThingDef/label")!.InnerText, Is.EqualTo("native"));
    }
    [Test]
    public void AStageArgumentReplacedAfterScopeCreationUsesNativeQueries()
    {
        new Harmony(Foreign).Patch(M(nameof(Synthetic.Stage)),
            prefix: new HarmonyMethod(typeof(RuntimeTests), nameof(ReplaceStageDocument))
            { priority = Priority.Last, after = new[] { RuntimeContext.OwnerPrefix + ".definitions" } });
        Definitions(); Synthetic.Stage(Xml("Wine"));
        Assert.That(Synthetic.Last!.SelectSingleNode("defName")!.InnerText, Is.EqualTo("Beer"));
        Assert.That(context.Report, Does.Contain("0 indexed queries"));
    }
    [Test]
    public void AForeignStageBodyRewriteStillRefusesIndexInstallation()
    {
        new Harmony(Foreign).Patch(M(nameof(Synthetic.Stage)),
            transpiler: new HarmonyMethod(typeof(RuntimeTests), nameof(KeepInstructions)));
        var feature = new DefinitionSearches(context); cleanup.Add(feature.Clear);
        context.Install(DefinitionSearches.Feature, true,
            () => feature.Install(M(nameof(Synthetic.Stage)), new[] { M(nameof(Synthetic.Nodes)) }), feature.Clear);
        Assert.That(context.Report, Does.Contain("Ordinary loading"));
    }
    private static void ChangeStageLabel(XmlDocument __0)
        => __0.SelectSingleNode("Defs/ThingDef/label")!.InnerText = "prefix";
    private static void AfterStage(XmlDocument __0) => __0.DocumentElement!.SetAttribute("callback", "completed");
    private static bool SkipStage() => false;
    private static void ReplaceStageDocument(ref XmlDocument __0) => __0 = Xml();
    private static IEnumerable<CodeInstruction> KeepInstructions(IEnumerable<CodeInstruction> instructions) => instructions;
    [Test]
    public void ThreeArgumentQueryHelpersPreserveNodesAndSeeMutationsWithinTheStage()
    {
        var feature = new DefinitionSearches(context); cleanup.Add(feature.Clear);
        feature.Install(M(nameof(Synthetic.HelperStage)), new[] { M(nameof(Synthetic.HelperNodes)) });
        var xml = Xml(); Synthetic.HelperStage(xml);
        Assert.That(Synthetic.Last, Is.SameAs(xml.DocumentElement!.FirstChild));
        Assert.That(Synthetic.Last!["label"]!.InnerText, Is.EqualTo("helper-change"));
        Assert.That(context.Report, Does.Contain("2 indexed queries"));
    }
    [Test]
    public void MissingOptionalXmlSupplierDoesNotAdmitUnrelatedHelpers()
    {
        Assert.That(OptionalXmlQueries.Find(), Is.Empty);
        Assert.That(OptionalXmlQueries.AllowsMethod(M(nameof(Synthetic.HelperNodes))), Is.False);
    }
    [Test]
    public void TypeSequenceRefreshPreservesOldSimpleNamesAndNewFullNames()
    {
        var feature = new TypeNameSearches(context); cleanup.Add(feature.Clear); feature.Install();
        Assert.That(AccessTools.TypeByName(nameof(RuntimeTests)), Is.SameAs(typeof(RuntimeTests)));
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("WakeUp17.Order." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("main");
        Type newer = module.DefineType("Later." + nameof(RuntimeTests)).CreateType()!;
        Assert.That(AccessTools.TypeByName(nameof(RuntimeTests)), Is.SameAs(typeof(RuntimeTests)));
        Assert.That(AccessTools.TypeByName(newer.FullName!), Is.SameAs(newer));
    }
    [Test]
    public void XmlStageExceptionsReleaseScopeAndPreserveTheException()
    {
        Definitions(); Synthetic.Throw = true;
        Assert.Throws<InvalidOperationException>((Action)(() => Synthetic.Stage(Xml())));
        Synthetic.Throw = false;
        var xml = Xml("Wine");
        Assert.That(Synthetic.Single(xml), Is.False);
        Synthetic.Stage(Xml());
        Assert.That(context.Report, Does.Contain("3 indexed queries"));
    }
    [Test]
    public void NestedXmlStageSuspendsItsIndexAndRestoresTheParent()
    {
        Definitions();
        Synthetic.Nested = () => { Synthetic.Nested = null; Synthetic.Stage(Xml("Wine")); };
        Synthetic.Stage(Xml());
        Assert.That(context.Report, Does.Contain("3 indexed queries"));
        Assert.That(Synthetic.Last!.SelectSingleNode("defName")!.InnerText, Is.EqualTo("Beer"));
    }
    [Test]
    public void ForeignWorkerCallbacksRemainInstalledAndOriginalQueriesAreReused()
    {
        new Harmony(Foreign).Patch(M(nameof(Synthetic.Nodes)), prefix: new HarmonyMethod(M(nameof(Synthetic.Observe))));
        Definitions();
        Assert.That(Harmony.GetPatchInfo(M(nameof(Synthetic.Nodes))).Owners, Does.Contain(Foreign));
        Synthetic.Stage(Xml());
        Assert.That(context.Report, Does.Contain("3 indexed queries"));
    }
    [Test]
    public void ChangedIncomingWorkerDuringHarmonyRebuildPassesThroughWithoutThrowing()
    {
        Definitions();
        new Harmony(Foreign).Patch(M(nameof(Synthetic.Nodes)), transpiler: new HarmonyMethod(M(nameof(Synthetic.ChangeQuery))) { priority = Priority.First });
        Synthetic.Stage(Xml("Wine"));
        Assert.That(context.Report, Does.Contain("worker changed during patch rebuilding"));
        Assert.That(Synthetic.NodeResult, Is.True);
        Assert.That(Synthetic.SingleResult, Is.False);
        new Harmony(Foreign).UnpatchAll(Foreign);
        Synthetic.Stage(Xml());
        Assert.That(Synthetic.NodeResult, Is.True);
    }
    [Test]
    public void AQueryObserverKeepsTheOriginalExpressionAndContext()
    {
        var single = typeof(XmlNode).GetMethod("SelectSingleNode", new[] { typeof(string) })!;
        new Harmony(Foreign).Patch(single, prefix: new HarmonyMethod(M(nameof(Synthetic.Observe))));
        var feature = new DefinitionSearches(context); cleanup.Add(feature.Clear);
        context.Install(DefinitionSearches.Feature, true, () => feature.Install(M(nameof(Synthetic.Stage)), new[] { M(nameof(Synthetic.Single)) }), feature.Clear);
        Assert.That(context.Report, Does.Contain("Ordinary loading"));
        Assert.That(Harmony.GetPatchInfo(M(nameof(Synthetic.Single)))?.Owners.ToArray() ?? Array.Empty<string>(), Is.Empty);
    }

    [Test]
    public void Harmony3PublicationGuardDetectsAddAndRemoveWithoutRepeatedDeserialization()
    {
        var target = M(nameof(Synthetic.GuardTarget)); var guard = context.Guard(target);
        Assert.That(guard.AllowsOriginalContract(), Is.True);
        int reads = guard.PatchInfoReads;
        for (int i = 0; i < 20; i++) Assert.That(guard.AllowsOriginalContract(), Is.True);
        Assert.That(guard.PatchInfoReads, Is.EqualTo(reads));
        object? stamp = guard.Publication;
        new Harmony(Foreign).Patch(target, prefix: new HarmonyMethod(M(nameof(Synthetic.Observe))));
        Assert.That(guard.AllowsOriginalContract(), Is.False);
        new Harmony(Foreign).UnpatchAll(Foreign);
        Assert.That(guard.AllowsOriginalContract(), Is.True);
        Assert.That(guard.PublicationUnchanged(stamp), Is.False);
    }

    [Test]
    public void TypeNameFallbackReadsNewTypesInAnExistingDynamicAssembly()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("WakeUp17.Dynamic." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("main");
        string firstName = "First" + Guid.NewGuid().ToString("N");
        Type first = module.DefineType("Test." + firstName).CreateType()!;
        var feature = new TypeNameSearches(context); cleanup.Add(feature.Clear); feature.Install();
        Assert.That(AccessTools.TypeByName(firstName), Is.SameAs(first));
        string secondName = "Second" + Guid.NewGuid().ToString("N");
        Type second = module.DefineType("Test." + secondName).CreateType()!;
        Assert.That(AccessTools.TypeByName(secondName), Is.SameAs(second));
        Assert.That(AccessTools.TypeByName("System.Int32"), Is.SameAs(typeof(int)));
    }
    [Test]
    public void TypeIndexPreservesFirstMatchesAndFullNameVersusSimpleName()
    {
        int count = 0, characters = 0;
        var index = TypeLookupIndex.Create(new[] { typeof(Child), typeof(Leaf), typeof(Child) }, ref count, ref characters)!;
        Assert.That(index.Find(typeof(Child).FullName!, true), Is.SameAs(typeof(Child)));
        Assert.That(index.Find(nameof(Leaf), false), Is.SameAs(typeof(Leaf)));
        Assert.That(index.Find("missing", false), Is.Null);
    }
    [Test]
    public void ForeignTypeEnumerationMakesTheFallbackStayNative()
    {
        var feature = new TypeNameSearches(context); cleanup.Add(feature.Clear); feature.Install();
        var target = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.AllTypes));
        new Harmony(Foreign).Patch(target, prefix: new HarmonyMethod(M(nameof(Synthetic.ReplaceTypes))));
        Assert.That(AccessTools.TypeByName("DefinitelyAbsentWakeUp17"), Is.Null);
    }

    private LeafSearches Leaves()
    {
        var feature = new LeafSearches(context); cleanup.Add(feature.Clear);
        feature.Install(AccessTools.Method(typeof(NativeTypes), nameof(NativeTypes.Leaves)),
            AccessTools.Method(typeof(NativeTypes), nameof(NativeTypes.Subclasses)),
            AccessTools.PropertyGetter(typeof(NativeTypes), nameof(NativeTypes.AllTypes)),
            typeof(Alerts).GetConstructor(Type.EmptyTypes)!, () => NativeTypes.Types, () => NativeTypes.Cache);
        return feature;
    }
    [Test]
    public void LeafOptimizationRetainsNativeMutableSubclassLists()
    {
        Leaves();
        Assert.That(NativeTypes.Leaves(typeof(Base)).ToArray(), Is.EqualTo(new[] { typeof(Leaf) }));
        Assert.That(NativeTypes.Cache[typeof(Leaf)], Is.Empty);
        NativeTypes.Cache[typeof(Leaf)].Add(typeof(Other));
        Assert.That(NativeTypes.Leaves(typeof(Base)), Is.Empty);
    }
    [Test]
    public void LeafProofInvalidatesOnSameLengthTypeListReplacement()
    {
        var index = new LeafSubclassIndex();
        var types = new List<Type> { typeof(Base), typeof(Child) };
        Assert.That(index.TryProveLeaf(typeof(Child), types), Is.True);
        types[0] = typeof(Leaf);
        Assert.That(index.TryProveLeaf(typeof(Child), types), Is.False);
    }
    [Test]
    public void DeferredLeafIterationAfterStartupUsesNativePredicate()
    {
        Leaves(); var deferred = NativeTypes.Leaves(typeof(Base));
        context.CompleteStartup();
        Assert.That(deferred.ToArray(), Is.EqualTo(new[] { typeof(Leaf) }));
        Assert.That(NativeTypes.Scans, Is.GreaterThan(0));
        var alerts = new Alerts();
        Assert.That(alerts.Items, Is.EqualTo(new[] { typeof(Leaf) }));
        Assert.That(context.Report, Does.Contain("Alert scope completed"));
    }

    [Test]
    public void MemberReuseNeverCachesPropertyValuesOrExceptions()
    {
        var feature = new MemberSearches(context, true, false); cleanup.Add(feature.Dispose);
        feature.Install(new[] { M(nameof(Synthetic.ReadMember)) }, null);
        context.StartupCompleted += feature.Clear;
        var value = new Values();
        Assert.That(Synthetic.ReadMember(value), Is.EqualTo(1));
        Assert.That(Synthetic.ReadMember(value), Is.EqualTo(2));
        value.Throw = true;
        Assert.Throws<TargetInvocationException>((Action)(() => Synthetic.ReadMember(value)));
        Assert.Throws<TargetInvocationException>((Action)(() => Synthetic.ReadMember(value)));
        context.CompleteStartup();
        Assert.That(context.Report, Does.Contain("3 reused member lookups"));
    }
    [Test]
    public void TranslationMetadataScopesAreIndependentAndReleaseOnException()
    {
        var feature = new MemberSearches(context, false, true); cleanup.Add(feature.Dispose);
        feature.Install(new[] { M(nameof(Synthetic.ReadMember)) }, M(nameof(Synthetic.Package)));
        var value = new Values();
        Synthetic.Package(() => { Synthetic.ReadMember(value); Synthetic.ReadMember(value); });
        Assert.That(logs.Last(), Does.Contain("1 hits, 1 native"));
        Assert.Throws<InvalidOperationException>((Action)(() => Synthetic.Package(() => { Synthetic.ReadMember(value); throw new InvalidOperationException(); })));
        Synthetic.Package(() => Synthetic.ReadMember(value));
        Assert.That(logs.Last(), Does.Contain("0 hits, 1 native"));
    }
    [Test]
    public void LifetimeWaitsForLoadedDataAndQueueDrainThenCompletesOnce()
    {
        bool loaded = false, busy = true; int completed = 0;
        context.StartupCompleted += () => completed++;
        new SessionLifetime(context, () => loaded, () => busy).Install(M(nameof(Synthetic.Update)));
        Synthetic.Update(); Assert.That(completed, Is.Zero);
        loaded = true; Synthetic.Update(); Assert.That(completed, Is.Zero);
        busy = false; Synthetic.Update(); Synthetic.Update(); Assert.That(completed, Is.EqualTo(1));
    }
    [Test]
    public void SupportedHostStillRejectsAnUnreviewedGameAssembly()
    {
        var error = Assert.Throws<InvalidOperationException>((Action)(() => RuntimeContract.RequireRuntime(typeof(RuntimeTests).Assembly)));
        Assert.That(error!.Message, Does.Contain("Unreviewed"));
    }
    [Test]
    public void TestsDoNotLoadGameOrEngineAssemblies()
    {
        Assert.That(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name),
            Has.None.Matches<string?>(name => name == "Assembly-CSharp" || name == "UnityStubs" || name?.StartsWith("Waylith", StringComparison.Ordinal) == true));
    }

    public class Base { }
    public class Child : Base { }
    public class Leaf : Child { }
    public class Other { }
    public sealed class Values
    {
        public bool Throw;
        private int reads;
        public int Value => Throw ? throw new InvalidOperationException("getter") : ++reads;
    }
    public sealed class Alerts
    {
        public Type[] Items;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Alerts() { Items = NativeTypes.Leaves(typeof(Base)).ToArray(); }
    }
    public static class NativeTypes
    {
        public static List<Type> Types = new();
        public static Dictionary<Type, List<Type>> Cache = new();
        public static int Scans;
        public static List<Type> AllTypes => Types;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static List<Type> Subclasses(Type parent)
        {
            if (!Cache.TryGetValue(parent, out var result))
            { Scans++; result = Types.Where(t => t.IsSubclassOf(parent)).ToList(); Cache.Add(parent, result); }
            return result;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IEnumerable<Type> Leaves(Type parent) => Subclasses(parent).Where(t => !Subclasses(t).Any());
    }
    public static class Synthetic
    {
        public static Action? Nested;
        public static bool Throw, NodeResult, SingleResult;
        public static XmlNode? Last;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HelperStage(XmlDocument xml)
        {
            var before = HelperNodes("Defs/ThingDef[defName='Beer']", xml, new object());
            Last = before[0]; Last!["label"]!.InnerText = "helper-change";
            var after = HelperNodes("Defs/ThingDef[defName='Beer']", xml, new object());
            Last = after[0];
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static XmlNodeList HelperNodes(string path, XmlDocument xml, object operation) => xml.SelectNodes(path)!;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Stage(XmlDocument xml)
        {
            NodeResult = Nodes(xml); Nodes(xml); Nested?.Invoke(); SingleResult = Single(xml);
            if (Throw) throw new InvalidOperationException("stage");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Nodes(XmlDocument xml)
        {
            bool found = false;
            foreach (XmlNode node in xml.SelectNodes("Defs/ThingDef[defName='Beer']")!)
            { Last = node; node["label"]!.InnerText = "changed"; found = true; }
            return found;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Single(XmlDocument xml) => xml.SelectSingleNode("Defs/ThingDef[defName='Beer']") != null;
        public static IEnumerable<CodeInstruction> ChangeQuery(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            { if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, "Defs/ThingDef[defName='Beer']")) instruction.operand = "Defs/ThingDef[defName='Wine']"; yield return instruction; }
        }
        public static void Observe() { }
        public static bool ReplaceTypes(ref IEnumerable<Type> __result) { __result = Array.Empty<Type>(); return false; }
        [MethodImpl(MethodImplOptions.NoInlining)] public static int GuardTarget() => 1;
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Update() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public static object? ReadMember(object value)
            => value.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)!.GetValue(value);
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Package(Action action) => action();
    }
}
