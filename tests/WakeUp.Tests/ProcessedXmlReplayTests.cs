// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using Verse;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ProcessedXmlReplayTests
{
    private readonly Harmony harmony = new("WakeUp.ProcessedReplay.Tests");
    private static readonly MethodInfo Apply = AccessTools.Method(typeof(PatchOperation), nameof(PatchOperation.Apply));
    private static readonly FieldInfo Active = AccessTools.Field(typeof(ProcessedXmlRuntime), "active");
    private static readonly Type SessionType = typeof(ProcessedXmlRuntime).GetNestedType("Session", BindingFlags.NonPublic)!;
    private static readonly FieldInfo Invocation = AccessTools.Field(typeof(ProcessedXmlRuntime), "invocation");
    private static readonly Type InvocationType = typeof(ProcessedXmlRuntime).GetNestedType("Invocation", BindingFlags.NonPublic)!;
    private static readonly MethodInfo Boundary = AccessTools.Method(typeof(ProcessedXmlReplayTests), nameof(NativeBoundary));
    private static readonly List<string> BoundaryEvents = new();
    private static bool SkipBoundary;
    private bool profiling;

    [SetUp]
    public void SetUp()
    {
        Assert.That(Active.GetValue(null), Is.Null);
        profiling = DeepProfiler.enabled;
        DeepProfiler.enabled = false;
        harmony.Patch(Apply,
            prefix: new HarmonyMethod(AccessTools.Method(typeof(ProcessedXmlRuntime), "BeforeOperation")),
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(ProcessedXmlRuntime), "AfterOperation")));
    }

    [TearDown]
    public void TearDown()
    {
        Active.SetValue(null, null);
        Invocation.SetValue(null, null);
        foreach (string owner in new[] { "ModSettingsFrameworkMod", "wakeup.def-lookup", "wakeup.processed-xml", "WakeUp.ProcessedBoundary.Skip" })
            new Harmony(owner).Unpatch(Boundary, HarmonyPatchType.All, owner);
        harmony.Unpatch(Apply, HarmonyPatchType.All, harmony.Id);
        DeepProfiler.enabled = profiling;
    }

    [Test]
    public void PreparationOccursAfterSettingsAndBeforeQueryScopeAtTheNativeBoundary()
    {
        XmlDocument document = Document("<Defs />"); var map = new Dictionary<XmlNode, LoadableXmlAsset>();
        object lifetime = NewInvocation(document, map);
        InstallBoundary();
        BoundaryEvents.Clear(); SkipBoundary = false;
        NativeBoundary(document, map);
        Assert.That(BoundaryEvents, Is.EqualTo(new[] { "settings:unprepared", "query:prepared", "native" }));
        Assert.That(Get<bool>(lifetime, "PreparationAttempted"), Is.True);
    }

    [Test]
    public void SkippedNativeBodyAndCallsOutsideOuterLifetimeDoNotPrepare()
    {
        XmlDocument document = Document("<Defs />"); var map = new Dictionary<XmlNode, LoadableXmlAsset>();
        object lifetime = NewInvocation(document, map);
        InstallBoundary();
        BoundaryEvents.Clear(); SkipBoundary = true;
        NativeBoundary(document, map);
        Assert.That(Get<bool>(lifetime, "PreparationAttempted"), Is.False);
        Assert.That(BoundaryEvents, Does.Not.Contain("native"));
        Assert.That(BoundaryEvents, Does.Not.Contain("query:prepared"));

        Invocation.SetValue(null, null); SkipBoundary = false; BoundaryEvents.Clear();
        NativeBoundary(document, map);
        Assert.That(BoundaryEvents, Is.EqualTo(new[] { "settings:outside", "query:outside", "native" }));
        Assert.That(Active.GetValue(null), Is.Null);
    }

    private static object NewInvocation(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map)
    {
        object lifetime = Activator.CreateInstance(InvocationType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { document, map }, null)!;
        Invocation.SetValue(null, lifetime); return lifetime;
    }
    private static void InstallBoundary()
    {
        // Register in reverse order so explicit production ordering, rather
        // than incidental registration order, establishes the boundary.
        new Harmony("wakeup.def-lookup").Patch(Boundary, prefix: new HarmonyMethod(typeof(ProcessedXmlReplayTests), nameof(QueryBoundaryPrefix)) { priority = Priority.Last });
        new Harmony("wakeup.processed-xml").Patch(Boundary, prefix: ProcessedXmlRuntime.PreparationPrefix());
        new Harmony("ModSettingsFrameworkMod").Patch(Boundary, prefix: new HarmonyMethod(typeof(ProcessedXmlReplayTests), nameof(SettingsBoundaryPrefix)));
        new Harmony("WakeUp.ProcessedBoundary.Skip").Patch(Boundary, prefix: new HarmonyMethod(typeof(ProcessedXmlReplayTests), nameof(SkipBoundaryPrefix)) { priority = Priority.First });
    }
    private static string BoundaryState()
    { object? current = Invocation.GetValue(null); return current == null ? "outside" : Get<bool>(current, "PreparationAttempted") ? "prepared" : "unprepared"; }
    private static void SettingsBoundaryPrefix() => BoundaryEvents.Add("settings:" + BoundaryState());
    private static void QueryBoundaryPrefix() => BoundaryEvents.Add("query:" + BoundaryState());
    private static bool SkipBoundaryPrefix() => !SkipBoundary;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NativeBoundary(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map) => BoundaryEvents.Add("native");

    [Test]
    public void RecordedConditionalBranchRunsRequiredCustomEffectsOnceEvenWhenFinalXmlNoLongerMatches()
    {
        Graph Tree(List<string> effects)
        {
            var remove = Op<PatchOperationRemove>(("xpath", "/Defs/Marker"));
            var add = Add("/Defs", "<value><Done /></value>");
            var inner = Sequence(remove, add);
            var selected = new EffectOperation("selected", effects, inner);
            var unselected = new EffectOperation("wrong-branch", effects);
            var condition = Op<PatchOperationConditional>(("xpath", "/Defs/Marker"), ("match", selected), ("nomatch", unselected));
            var finalTest = Test("/Defs/Done");
            var after = new EffectOperation("after", effects, finalTest);
            var top = Sequence(condition, after);
            return new Graph(new PatchOperation[] { top, condition, selected, inner, remove, add, unselected, after, finalTest },
                new[] { new[] { 1, 7 }, new[] { 2, 6 }, new[] { 3 }, new[] { 4, 5 }, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), new[] { 8 }, Array.Empty<int>() },
                new[] { 2, 6, 7 });
        }

        var coldEffects = new List<string>();
        Graph cold = Tree(coldEffects);
        XmlDocument coldXml = Document("<Defs><Marker /></Defs>");
        object trace = Session(cold, coldXml, hit: false);
        Assert.That(cold.Top.Apply(coldXml), Is.True);
        Assert.That(coldEffects, Is.EqualTo(new[] { "selected", "after" }));
        Assert.That(Get<bool[]>(trace, "Called"), Is.EqualTo(new[] { true, true, true, true, true, true, false, true, true }));
        Assert.That(coldXml.SelectSingleNode("/Defs/Marker"), Is.Null);
        byte[] state = cold.Descriptor.CaptureState();

        var warmEffects = new List<string>();
        Graph warm = Tree(warmEffects);
        warm.Descriptor.PrepareRestore(state)();
        XmlDocument warmXml = Document(coldXml.OuterXml);
        object replay = Session(warm, warmXml, hit: true, trace);
        Assert.That(warm.Top.Apply(warmXml), Is.True);
        Assert.That(warmEffects, Is.EqualTo(coldEffects), "The recorded branch, callback count and callback order must survive final-tree restoration.");
        Assert.That(warmXml.OuterXml, Is.EqualTo(coldXml.OuterXml));
        Assert.That(warmXml.SelectNodes("/Defs/Done")!.Count, Is.EqualTo(1), "Native mutators beneath live wrappers must not execute twice.");
        Assert.That(warm.Descriptor.CaptureState(), Is.EqualTo(state));
        Assert.That(Get<int>(replay, "Effects"), Is.EqualTo(2));
        Assert.That(Get<int>(replay, "Skipped"), Is.EqualTo(3));
        Assert.That(Get<bool>(replay, "Closed"), Is.True);
    }

    [Test]
    public void FailedSequencePreservesShortCircuitAndFailureStateWithoutInventingLaterEffects()
    {
        Graph Tree(List<string> effects)
        {
            var firstTest = Test("/Defs/A");
            var first = new EffectOperation("before-failure", effects, firstTest);
            var failure = Test("/Defs/Missing");
            var unreachable = new EffectOperation("must-not-run", effects);
            var top = Sequence(first, failure, unreachable);
            Set(top, "success", "Always");
            return new Graph(new PatchOperation[] { top, first, firstTest, failure, unreachable },
                new[] { new[] { 1, 3, 4 }, new[] { 2 }, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>() }, new[] { 1, 4 });
        }

        var coldEffects = new List<string>();
        Graph cold = Tree(coldEffects);
        XmlDocument document = Document("<Defs><A /></Defs>");
        object trace = Session(cold, document, hit: false);
        Assert.That(cold.Top.Apply(document), Is.True, "Native Success.Always applies after the failed worker result.");
        Assert.That(Get<PatchOperation>(cold.Top, "lastFailedOperation"), Is.SameAs(cold.Objects[3]));
        Assert.That(Get<bool[]>(trace, "Called")[4], Is.False);
        byte[] state = cold.Descriptor.CaptureState();

        var warmEffects = new List<string>();
        Graph warm = Tree(warmEffects);
        warm.Descriptor.PrepareRestore(state)();
        Session(warm, document, hit: true, trace);
        Assert.That(warm.Top.Apply(document), Is.True);
        Assert.That(warmEffects, Is.EqualTo(new[] { "before-failure" }));
        Assert.That(warmEffects, Is.EqualTo(coldEffects));
        Assert.That(Get<PatchOperation>(warm.Top, "lastFailedOperation"), Is.SameAs(warm.Objects[3]));
        Assert.That(Get<bool>(warm.Objects[3], "neverSucceeded"), Is.True);
        Assert.That(Get<bool>(warm.Objects[4], "neverSucceeded"), Is.True);
        Assert.That(warm.Descriptor.CaptureState(), Is.EqualTo(state));
        warm.Top.Complete("replay-test");
        Assert.That(Get<PatchOperation?>(warm.Top, "lastFailedOperation"), Is.Null, "Native completion still owns clearing failure attribution.");
    }

    [Test]
    public void UnknownSuffixCallingAnAdmittedInstanceAfterPrefixClosureExecutesItNatively()
    {
        Graph Tree()
        {
            var add = Add("/Defs", "<value><Added /></value>");
            return new Graph(new PatchOperation[] { add }, new[] { Array.Empty<int>() }, Array.Empty<int>());
        }
        Graph cold = Tree();
        XmlDocument document = Document("<Defs />");
        object trace = Session(cold, document, hit: false);
        Assert.That(cold.Top.Apply(document), Is.True);
        byte[] state = cold.Descriptor.CaptureState();
        Graph warm = Tree();
        warm.Descriptor.PrepareRestore(state)();
        object replay = Session(warm, document, hit: true, trace);
        Assert.That(warm.Top.Apply(document), Is.True);
        Assert.That(Get<bool>(replay, "Closed"), Is.True);
        Assert.That(document.SelectNodes("/Defs/Added")!.Count, Is.EqualTo(1));
        var suffixEffects = new List<string>();
        var unknownSuffix = new EffectOperation("unknown-suffix", suffixEffects, warm.Top);
        Assert.That(unknownSuffix.Apply(document), Is.True);
        Assert.That(suffixEffects, Is.EqualTo(new[] { "unknown-suffix" }));
        Assert.That(document.SelectNodes("/Defs/Added")!.Count, Is.EqualTo(2), "An admitted object reused by unknown later code no longer belongs to the restored prefix.");
        Assert.That(Get<int>(replay, "Skipped"), Is.EqualTo(1));
    }

    private static object Session(Graph graph, XmlDocument document, bool hit, object? trace = null)
    {
        object session = Activator.CreateInstance(SessionType, nonPublic: true)!;
        Set(session, "Descriptor", graph.Descriptor); Set(session, "Tops", new[] { graph.Top });
        Set(session, "Objects", graph.Objects);
        Set(session, "Indices", graph.Objects.Select((op, index) => (op, index)).ToDictionary(p => p.op, p => p.index));
        Set(session, "Document", document); Set(session, "Hit", hit);
        // This tests real Apply traversal and replay, not store publication or
        // adapter admission. A cold invalid session records ordinary calls but
        // cannot attempt to publish into a fixture cache.
        Set(session, "Invalid", !hit);
        Set(session, "Parents", trace == null ? Enumerable.Repeat(-1, graph.Objects.Length).ToArray() : (int[])Get<int[]>(trace, "Parents").Clone());
        Set(session, "Called", trace == null ? new bool[graph.Objects.Length] : (bool[])Get<bool[]>(trace, "Called").Clone());
        Set(session, "Results", trace == null ? new bool[graph.Objects.Length] : (bool[])Get<bool[]>(trace, "Results").Clone());
        Set(session, "ModNameCalls", trace == null ? new int[graph.Objects.Length] : (int[])Get<int[]>(trace, "ModNameCalls").Clone());
        Active.SetValue(null, session);
        return session;
    }

    private sealed class Graph
    {
        internal readonly PatchOperation[] Objects;
        internal PatchOperation Top => Objects[0];
        internal readonly ProcessedXmlOperations Descriptor;
        internal Graph(PatchOperation[] objects, int[][] children, int[] liveEffects)
        {
            Objects = objects;
            // Private test-only descriptor assembly isolates the replay engine
            // with counting custom wrappers; production admission never accepts
            // these test types and no production bypass is introduced.
            Type entryType = typeof(ProcessedXmlOperations).GetNestedType("Entry", BindingFlags.NonPublic)!;
            Array entries = Array.CreateInstance(entryType, objects.Length);
            ConstructorInfo entry = entryType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            for (int i = 0; i < objects.Length; i++)
                entries.SetValue(entry.Invoke(new object?[] { objects[i], AccessTools.Field(typeof(PatchOperation), "neverSucceeded"),
                    objects[i] is PatchOperationSequence ? AccessTools.Field(typeof(PatchOperationSequence), "lastFailedOperation") : null,
                    children[i], liveEffects.Contains(i) }), i);
            ConstructorInfo descriptor = typeof(ProcessedXmlOperations).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            Descriptor = (ProcessedXmlOperations)descriptor.Invoke(new object[] { entries, new[] { 0 }, new byte[] { 2, 0, 2 } });
        }
    }

    private sealed class EffectOperation : PatchOperation
    {
        private readonly string label;
        private readonly List<string> effects;
        private readonly PatchOperation? child;
        internal EffectOperation(string label, List<string> effects, PatchOperation? child = null)
        { this.label = label; this.effects = effects; this.child = child; }
        protected override bool ApplyWorker(XmlDocument xml)
        { effects.Add(label); return child?.Apply(xml) ?? true; }
    }

    private static XmlDocument Document(string xml) { var document = new XmlDocument(); document.LoadXml(xml); return document; }
    private static T Op<T>(params (string Name, object? Value)[] fields) where T : PatchOperation, new()
    { var op = new T(); foreach (var field in fields) Set(op, field.Name, field.Value); return op; }
    private static PatchOperationTest Test(string xpath) => Op<PatchOperationTest>(("xpath", xpath));
    private static PatchOperationAdd Add(string xpath, string value) => Op<PatchOperationAdd>(("xpath", xpath), ("value", new XmlContainer { node = Document(value).DocumentElement! }));
    private static PatchOperationSequence Sequence(params PatchOperation[] children) => Op<PatchOperationSequence>(("operations", children.ToList()));
    private static void Set(object target, string name, object? value)
    { FieldInfo field = AccessTools.Field(target.GetType(), name); field.SetValue(target, field.FieldType.IsEnum && value is string text ? Enum.Parse(field.FieldType, text) : value); }
    private static T Get<T>(object target, string name) => (T)AccessTools.Field(target.GetType(), name).GetValue(target)!;
}
