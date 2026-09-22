// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using NUnit.Framework;
using Verse;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
[NonParallelizable]
public sealed class LeafSubclassRuntimeTests
{
    private object? previousTypes, previousSubclasses;
    private readonly Dictionary<string, object?> previousActivation = new();
    private List<Type> types = null!;
    private Dictionary<Type, List<Type>> cache = null!;
    private const string ForeignOwner = "WakeUp.Leaf.Tests.Foreign";
    private int testScope;

    [OneTimeSetUp]
    public void InstallGuardedAdapter()
    {
        foreach (string name in new[] { "installed", "candidate", "finished" })
            previousActivation.Add(name, AccessTools.Field(typeof(TypeLookupRuntime), name).GetValue(null));
        SetActivation("installed", true);
        SetActivation("candidate", true);
        SetActivation("finished", 0);
        LeafSubclassRuntime.Initialize();
        Assert.That(AccessTools.Field(typeof(LeafSubclassRuntime), "installed").GetValue(null), Is.True);
    }

    [OneTimeTearDown]
    public void RemoveAdapter()
    {
        new Harmony("wakeup.leaf-subclasses").UnpatchAll("wakeup.leaf-subclasses");
        AccessTools.Field(typeof(LeafSubclassRuntime), "installed").SetValue(null, false);
        LeafSubclassRuntime.Complete();
        foreach (var pair in previousActivation) SetActivation(pair.Key, pair.Value!);
    }

    [SetUp]
    public void SetNativeLists()
    {
        previousTypes = AccessTools.Field(typeof(GenTypes), "allTypesCached").GetValue(null);
        previousSubclasses = AccessTools.Field(typeof(GenTypes), "cachedSubclasses").GetValue(null);
        types = new List<Type> { typeof(Root), typeof(Branch), typeof(Left), typeof(Right), typeof(AbstractLeaf), typeof(string) };
        cache = new Dictionary<Type, List<Type>>();
        SetNative("allTypesCached", types);
        SetNative("cachedSubclasses", cache);
        SetActivation("candidate", true);
        SetActivation("finished", 0);
        LeafSubclassRuntime.BeginConstruction(out testScope);
    }

    [TearDown]
    public void RestoreNativeLists()
    {
        EndTestScope();
        SetActivation("finished", 1);
        LeafSubclassRuntime.CompleteStartup();
        new Harmony(ForeignOwner).UnpatchAll(ForeignOwner);
        SetNative("allTypesCached", previousTypes);
        SetNative("cachedSubclasses", previousSubclasses);
        SetActivation("candidate", true);
        SetActivation("finished", 0);
    }

    [Test]
    public void NativeMethodContractsMatchReviewedReference()
    {
        MethodBase[] methods = LeafSubclassRuntime.ContractMethods();
        for (int i = 0; i < methods.Length; i++)
        {
            Assert.That(SemanticMethodIdentity.TryHash(methods[i], out string hash, out string reason), Is.True, reason);
            Assert.That(hash, Is.EqualTo(LeafSubclassRuntime.ExpectedBodies[i]), methods[i].Name);
        }
    }

    [Test]
    public void OrderedMultilevelResultsAndPublishedChildListsMatchNative()
    {
        Type[] order = { typeof(Right), typeof(Branch), typeof(AbstractLeaf), typeof(Left), typeof(Left) };
        cache.Add(typeof(Root), order.ToList());
        SetActivation("candidate", false);
        Type[] expected = typeof(Root).AllLeafSubclasses().ToArray();
        var expectedCache = cache.ToDictionary(p => p.Key, p => p.Value.ToArray());
        cache.Clear();
        cache.Add(typeof(Root), order.ToList());
        SetActivation("candidate", true);
        long before = AvoidedScans;
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.That(cache.Count, Is.EqualTo(1), "Child searches remain deferred.");
        Assert.That(result, Is.Not.InstanceOf<ICollection<Type>>());
        Assert.That(result.ToArray(), Is.EqualTo(expected));
        Assert.That(AvoidedScans - before, Is.EqualTo(3), "Each distinct terminal child avoids one full scan.");
        foreach (var pair in expectedCache)
            Assert.That(cache[pair.Key], Is.EqualTo(pair.Value), pair.Key.Name);
        Assert.That(result.ToArray(), Is.EqualTo(expected));
        Assert.That(AvoidedScans - before, Is.EqualTo(3), "Repeated iteration uses the same mutable native child caches.");
    }

    [Test]
    public void RootCaptureRemainsEagerAndChildSearchesDeferred()
    {
        long before = AvoidedScans;
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.That(cache.Keys, Is.EquivalentTo(new[] { typeof(Root) }));
        Assert.That(AvoidedScans, Is.EqualTo(before));
        Type[] captured = cache[typeof(Root)].ToArray();
        Assert.That(result.ToArray(), Is.EqualTo(captured.Where(t => t != typeof(Branch)).ToArray()));
    }

    [Test]
    public void MutatingOuterAndChildListsRemainsVisibleOnRepeatedEnumeration()
    {
        var outer = new List<Type> { typeof(Left), typeof(Right) };
        cache.Add(typeof(Root), outer);
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.That(result.ToArray(), Is.EqualTo(outer));
        cache[typeof(Left)].Add(typeof(string)); // Native exposes this mutable list.
        outer.Add(typeof(AbstractLeaf));
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Right), typeof(AbstractLeaf) }));
        cache[typeof(Left)].Clear();
        Assert.That(result.ToArray(), Is.EqualTo(outer));
        using IEnumerator<Type> iterator = result.GetEnumerator();
        Assert.That(iterator.MoveNext(), Is.True);
        outer[1] = typeof(AbstractLeaf);
        Assert.Throws<InvalidOperationException>((Action)(() => iterator.MoveNext()));
    }

    [Test]
    public void SameSizeTypeMutationRebuildsProofButExistingNativeChildListsWin()
    {
        cache.Add(typeof(Root), new List<Type> { typeof(Left) });
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Left) }));
        types[types.Count - 1] = typeof(Deeper);
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Left) }), "Native already cached an empty child list.");
        cache.Remove(typeof(Left));
        Assert.That(result.ToArray(), Is.Empty, "The same-size replacement invalidates the proof for a new child search.");
        Assert.That(cache[typeof(Left)], Is.EqualTo(new[] { typeof(Deeper) }));
    }

    [Test]
    public void ReplacedTypeAndSubclassCachesKeepCapturedOuterList()
    {
        var outer = new List<Type> { typeof(Left), typeof(Right) };
        cache.Add(typeof(Root), outer);
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.That(result.ToArray(), Is.EqualTo(outer));
        SetNative("allTypesCached", new List<Type> { typeof(Left), typeof(Deeper) });
        var replacement = new Dictionary<Type, List<Type>>();
        SetNative("cachedSubclasses", replacement);
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Right) }));
        Assert.That(replacement[typeof(Left)], Is.EqualTo(new[] { typeof(Deeper) }));
        Assert.That(replacement[typeof(Right)], Is.Empty);
    }

    [Test]
    public void DynamicAssemblyAndTypesKeepNativeUniverseAndFallback()
    {
        cache.Add(typeof(Root), new List<Type> { typeof(Left) });
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Left) }));
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("LeafChanges"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("main");
        Type added = module.DefineType("NewChild", TypeAttributes.Public, typeof(Left)).CreateType()!;
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Left) }), "Assembly events alone do not clear native caches.");
        cache.Remove(typeof(Left));
        types.Add(added);
        long before = AvoidedScans;
        Assert.That(result.ToArray(), Is.Empty);
        Assert.That(AvoidedScans, Is.EqualTo(before), "Dynamic metadata retains ordinary child enumeration.");
        Assert.That(cache[typeof(Left)], Is.EqualTo(new[] { added }));
    }

    [Test]
    public void PartialTypeListAndMissingGenericAncestorsMatchNative()
    {
        types.Clear();
        types.AddRange(new[] { typeof(GenericChild), typeof(AbstractLeaf) });
        Type[] candidates = { typeof(Root), typeof(Generic<>), typeof(Generic<int>), typeof(AbstractLeaf) };
        cache.Add(typeof(string), candidates.ToList()); // Mutated outer lists need not describe a real hierarchy.
        SetActivation("candidate", false);
        Type[] expected = typeof(string).AllLeafSubclasses().ToArray();
        cache.Clear();
        cache.Add(typeof(string), candidates.ToList());
        SetActivation("candidate", true);
        Assert.That(typeof(string).AllLeafSubclasses().ToArray(), Is.EqualTo(expected));
        Assert.That(expected, Is.EqualTo(new[] { typeof(Generic<>), typeof(AbstractLeaf) }));
    }

    [Test]
    public void FailedMetadataEnumerationRemainsDeferredAndThrowsNativeFailure()
    {
        types.Add(null!);
        cache.Add(typeof(Root), new List<Type> { typeof(Left) });
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.Throws<AggregateException>((Action)(() => result.ToArray()));
        Assert.That(cache.ContainsKey(typeof(Left)), Is.False, "A failed native enumeration is never replaced with an empty result.");
    }

    [Test]
    public void UninitializedUniverseAndLateForeignPatchUseOriginalPredicate()
    {
        cache.Add(typeof(Root), new List<Type> { typeof(Left) });
        SetNative("allTypesCached", null);
        MethodInfo getter = AccessTools.PropertyGetter(typeof(GenTypes), nameof(GenTypes.AllTypes));
        new Harmony(ForeignOwner).Patch(getter, prefix: new HarmonyMethod(typeof(LeafSubclassRuntimeTests), nameof(ThrowEnumeration)));
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.Throws<InvalidOperationException>((Action)(() => result.ToArray()));
        Assert.That(cache.ContainsKey(typeof(Left)), Is.False);
    }

    [Test]
    public void OutsideConstructorAndOtherThreadsUseNativeSearchAndReleaseProof()
    {
        cache.Add(typeof(Root), new List<Type> { typeof(Left), typeof(Right) });
        long before = AvoidedScans;
        Assert.That(Task.Run(() => typeof(Root).AllLeafSubclasses().ToArray()).GetAwaiter().GetResult(),
            Is.EqualTo(new[] { typeof(Left), typeof(Right) }));
        Assert.That(AvoidedScans, Is.EqualTo(before));
        cache.Remove(typeof(Left));
        Assert.That(typeof(Root).AllLeafSubclasses().ToArray(), Has.Length.EqualTo(2));
        Assert.That(AvoidedScans, Is.EqualTo(before + 1));
        EndTestScope();
        SetActivation("finished", 1);
        LeafSubclassRuntime.CompleteStartup();
        cache.Remove(typeof(Left));
        Assert.That(typeof(Root).AllLeafSubclasses().ToArray(), Has.Length.EqualTo(2));
        Assert.That(AvoidedScans, Is.EqualTo(before + 1));
        var index = (LeafSubclassIndex)AccessTools.Field(typeof(LeafSubclassRuntime), "Index").GetValue(null);
        Assert.That(index.IsCurrent(types), Is.False);
    }

    [Test]
    public void StartupDiscoveryBeforeAlertsAvoidsScansAndReleasesAtMenu()
    {
        EndTestScope();
        var outer = new List<Type> { typeof(Left), typeof(Right) };
        cache.Add(typeof(Root), outer);
        long before = StartupAvoidedScans;
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.That(cache.Keys, Is.EquivalentTo(new[] { typeof(Root) }), "Child discovery remains deferred.");
        Assert.That(result.ToArray(), Is.EqualTo(outer));
        Assert.That(StartupAvoidedScans - before, Is.EqualTo(2));
        Assert.That(cache[typeof(Left)], Is.Not.SameAs(cache[typeof(Right)]), "Native publishes separately mutable lists.");
        cache[typeof(Left)].Add(typeof(Deeper));
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Right) }), "Existing native child lists still win.");
        cache.Remove(typeof(Right));
        Assert.That(Task.Run(() => typeof(Root).AllLeafSubclasses().ToArray()).GetAwaiter().GetResult(),
            Is.EqualTo(new[] { typeof(Right) }));
        Assert.That(StartupAvoidedScans - before, Is.EqualTo(2), "Other loader threads keep native discovery.");
        SetActivation("finished", 1);
        LeafSubclassRuntime.CompleteStartup();
        cache.Remove(typeof(Right));
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Right) }));
        Assert.That(StartupAvoidedScans - before, Is.EqualTo(2), "Captured iterators cannot extend startup admission.");
        var index = (LeafSubclassIndex)AccessTools.Field(typeof(LeafSubclassRuntime), "Index").GetValue(null);
        Assert.That(index.IsCurrent(types), Is.False);
        Assert.That(AccessTools.Field(typeof(LeafSubclassRuntime), "ownerThread").GetValue(null), Is.EqualTo(0));
    }

    [Test]
    public void StartupDiscoveryRebuildsAfterTypeListMutationAndDoesNotHideErrors()
    {
        EndTestScope();
        cache.Add(typeof(Root), new List<Type> { typeof(Left) });
        IEnumerable<Type> result = typeof(Root).AllLeafSubclasses();
        Assert.That(result.ToArray(), Is.EqualTo(new[] { typeof(Left) }));
        types[types.Count - 1] = typeof(Deeper);
        cache.Remove(typeof(Left));
        Assert.That(result.ToArray(), Is.Empty);
        Assert.That(cache[typeof(Left)], Is.EqualTo(new[] { typeof(Deeper) }));
        cache.Remove(typeof(Left));
        types.Add(null!);
        Assert.Throws<AggregateException>((Action)(() => result.ToArray()));
        Assert.That(cache.ContainsKey(typeof(Left)), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForeignAlertConstructorHookDoesNotBlockIndependentStartupDiscovery(bool fail)
    {
        EndTestScope();
        var constructor = AccessTools.Constructor(typeof(AlertsReadout));
        new Harmony(ForeignOwner).Patch(constructor,
            postfix: new HarmonyMethod(typeof(LeafSubclassRuntimeTests), nameof(ForeignConstructorEffect)));
        cache.Add(typeof(Root), new List<Type> { typeof(Right) });
        long before = StartupAvoidedScans;
        Assert.That(typeof(Root).AllLeafSubclasses().ToArray(), Is.EqualTo(new[] { typeof(Right) }));
        Assert.That(StartupAvoidedScans, Is.EqualTo(before + 1), "The independent native startup contract remains intact.");
        List<Type> savedAlerts = AlertsReadout.allAlertTypesCached;
        bool savedProfiling = DeepProfiler.enabled;
        try
        {
            DeepProfiler.enabled = false;
            AlertsReadout.allAlertTypesCached = null!;
            types.Add(typeof(Alert_Custom));
            var outer = new List<Type> { typeof(Alert_Custom) };
            if (fail) outer.Add(null!);
            cache.Add(typeof(Alert), outer);
            foreignConstructorCalls = 0;
            long constructorBefore = AvoidedScans;
            Action construct = () => { _ = new AlertsReadout(); };
            if (fail) Assert.Throws<ArgumentNullException>(construct); else construct();
            Assert.That(foreignConstructorCalls, Is.EqualTo(fail ? 0 : 1));
            Assert.That(AvoidedScans, Is.EqualTo(constructorBefore));
            Assert.That(StartupAvoidedScans, Is.EqualTo(before + 1), "A refused constructor cannot enter through startup admission.");
            Assert.That(AccessTools.Field(typeof(LeafSubclassRuntime), "constructionEntries").GetValue(null), Is.EqualTo(0));
            Assert.That(AccessTools.Field(typeof(LeafSubclassRuntime), "constructionDepth").GetValue(null), Is.EqualTo(0));
            cache.Remove(typeof(Right));
            Assert.That(typeof(Root).AllLeafSubclasses().ToArray(), Is.EqualTo(new[] { typeof(Right) }));
            Assert.That(StartupAvoidedScans, Is.EqualTo(before + 2), "Return and exceptions both restore independent startup admission.");
            SetActivation("finished", 1);
            LeafSubclassRuntime.CompleteStartup();
            cache.Remove(typeof(Right));
            Assert.That(typeof(Root).AllLeafSubclasses().ToArray(), Is.EqualTo(new[] { typeof(Right) }));
            Assert.That(StartupAvoidedScans, Is.EqualTo(before + 2), "Later searches still require a valid constructor scope.");
        }
        finally
        {
            AlertsReadout.allAlertTypesCached = savedAlerts;
            DeepProfiler.enabled = savedProfiling;
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void ForeignLeafAndTypeHooksStillDisableIndependentStartupProof(int methodIndex)
    {
        EndTestScope();
        new Harmony(ForeignOwner).Patch(LeafSubclassRuntime.ContractMethods()[methodIndex],
            postfix: new HarmonyMethod(typeof(LeafSubclassRuntimeTests), nameof(ForeignNativeEffect)));
        cache.Add(typeof(Root), new List<Type> { typeof(Right) });
        long before = StartupAvoidedScans;
        foreignNativeCalls = 0;
        Assert.That(typeof(Root).AllLeafSubclasses().ToArray(), Is.EqualTo(new[] { typeof(Right) }));
        Assert.That(StartupAvoidedScans, Is.EqualTo(before));
        Assert.That(foreignNativeCalls, Is.GreaterThan(0), "Native foreign callbacks remain observable.");
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void ActualAlertConstructionAfterMenuAdmitsSearchAndAlwaysReleasesProof(bool fail, bool otherThread)
    {
        EndTestScope();
        SetActivation("finished", 1);
        Assert.That(TypeLookupRuntime.ActiveCandidate, Is.False, "The name-search cutoff remains closed.");
        List<Type> savedAlerts = AlertsReadout.allAlertTypesCached;
        bool savedProfiling = DeepProfiler.enabled;
        try
        {
            // Offline processes have no Prefs instance; profiling is unrelated
            // to the constructor's loading behavior and is restored below.
            DeepProfiler.enabled = false;
            // Alert_Custom is a real game type filtered before instantiation by
            // this constructor. Exercise its native leaf search without Unity
            // texture initialization or creating gameplay alert instances.
            AlertsReadout.allAlertTypesCached = null!;
            types.Clear();
            types.Add(typeof(Alert_Custom));
            var outer = new List<Type> { typeof(Alert_Custom) };
            if (fail) outer.Add(null!);
            cache.Add(typeof(Alert), outer);
            long before = AvoidedScans;
            Action construct = () => { _ = new AlertsReadout(); };
            Action invoke = () =>
            {
                if (otherThread) Task.Run(construct).GetAwaiter().GetResult();
                else construct();
            };
            if (fail)
                Assert.Throws<ArgumentNullException>(invoke);
            else
            {
                invoke();
                Assert.That(AlertsReadout.allAlertTypesCached, Is.Empty);
            }
            Assert.That(AvoidedScans, Is.EqualTo(before + 1), "The real patched constructor reaches the native leaf predicate after the menu cutoff.");
            Assert.That(TypeLookupRuntime.ActiveCandidate, Is.False);
            var index = (LeafSubclassIndex)AccessTools.Field(typeof(LeafSubclassRuntime), "Index").GetValue(null);
            Assert.That(index.IsCurrent(types), Is.False, "Finalizer releases the proof on both return and throw.");
            Assert.That(AccessTools.Field(typeof(LeafSubclassRuntime), "constructionDepth").GetValue(null), Is.EqualTo(0));
            Assert.That(AccessTools.Field(typeof(LeafSubclassRuntime), "ownerThread").GetValue(null), Is.EqualTo(0));
            outer.Remove(null!);
            cache.Remove(typeof(Alert_Custom));
            Assert.That(typeof(Alert).AllLeafSubclasses().ToArray(), Is.EqualTo(new[] { typeof(Alert_Custom) }));
            Assert.That(AvoidedScans, Is.EqualTo(before + 1), "An unrelated later search has no construction scope.");
        }
        finally
        {
            AlertsReadout.allAlertTypesCached = savedAlerts;
            DeepProfiler.enabled = savedProfiling;
        }
    }

    [Test]
    public void NativePlayUiConstructionReachesTheGuardedAlertConstructor()
    {
        var alertConstructor = AccessTools.Constructor(typeof(AlertsReadout));
        var uiConstructor = AccessTools.Constructor(typeof(UIRoot_Play));
        Assert.That(PatchProcessor.GetOriginalInstructions(uiConstructor)
            .Count(i => i.opcode == OpCodes.Newobj && Equals(i.operand, alertConstructor)), Is.EqualTo(1));
        MethodInfo[] startCallbacks = typeof(Verse.Root).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("<Start>", StringComparison.Ordinal)).ToArray();
        Assert.That(startCallbacks.SelectMany(m => PatchProcessor.GetOriginalInstructions(m))
            .Count(i => i.opcode == OpCodes.Newobj && Equals(i.operand, uiConstructor)), Is.EqualTo(1));
        Assert.That(PatchProcessor.GetOriginalInstructions(alertConstructor)
            .Count(i => i.opcode == OpCodes.Call && Equals(i.operand, LeafSubclassRuntime.ContractMethods()[0])), Is.EqualTo(1));
        Patches installed = Harmony.GetPatchInfo(alertConstructor);
        Assert.That(installed.Prefixes.Any(p => p.PatchMethod.Name == nameof(LeafSubclassRuntime.BeginConstruction)), Is.True);
        Assert.That(installed.Finalizers.Any(p => p.PatchMethod.Name == nameof(LeafSubclassRuntime.EndConstruction)), Is.True);
    }

    [Test]
    public void TranspilerChangesOnlyWhereCallAndRefusesForeignBody()
    {
        var original = PatchProcessor.GetOriginalInstructions(LeafSubclassRuntime.ContractMethods()[0]);
        var changed = LeafSubclassRuntime.Transpiler(original).ToList();
        Assert.That(changed.Count, Is.EqualTo(original.Count));
        Assert.That(Enumerable.Range(0, original.Count).Count(i => original[i].opcode != changed[i].opcode
            || !Equals(original[i].operand, changed[i].operand)), Is.EqualTo(1));
        var foreign = original.Select(i => new CodeInstruction(i)).ToList();
        foreign.Insert(0, new CodeInstruction(OpCodes.Nop));
        Assert.That(InstructionComparison.SameInstructions(LeafSubclassRuntime.Transpiler(foreign).ToList(), foreign), Is.True);
    }

    [Test]
    public void IndexBoundsAndCustomMetadataRefuseWithoutRepeatingBuilds()
    {
        var index = new LeafSubclassIndex();
        var oversized = Enumerable.Repeat(typeof(Left), TypeLookupIndex.MaximumTypes + 1).ToList();
        Assert.That(index.TryProveLeaf(typeof(Left), oversized), Is.False);
        Assert.That(index.TryProveLeaf(typeof(Left), oversized), Is.False);
        Assert.That(index.Builds, Is.EqualTo(1));
        Assert.That(index.Refusals, Is.EqualTo(1));
        Assert.That(index.TryProveLeaf(typeof(Left), new List<Type> { new TypeDelegator(typeof(Root)) }), Is.False);
        Assert.That(index.TryProveLeaf(typeof(object), types), Is.False);
        index.Clear();
        Assert.That(index.IsCurrent(oversized), Is.False);
    }

    private static long AvoidedScans => (long)AccessTools.Field(typeof(LeafSubclassRuntime), "avoidedScans").GetValue(null);
    private static long StartupAvoidedScans => (long)AccessTools.Field(typeof(LeafSubclassRuntime), "startupAvoidedScans").GetValue(null);
    private void EndTestScope()
    {
        LeafSubclassRuntime.EndConstruction(testScope);
        testScope = 0;
    }
    private static void SetNative(string name, object? value) => AccessTools.Field(typeof(GenTypes), name).SetValue(null, value);
    private static void SetActivation(string name, object value) => AccessTools.Field(typeof(TypeLookupRuntime), name).SetValue(null, value);
    private static void ThrowEnumeration() => throw new InvalidOperationException("native enumeration failure");
    private static int foreignConstructorCalls, foreignNativeCalls;
    private static void ForeignConstructorEffect() => foreignConstructorCalls++;
    private static void ForeignNativeEffect() => foreignNativeCalls++;

    public class Root { }
    public class Branch : Root { }
    public class Left : Branch { }
    public sealed class Right : Branch { }
    public sealed class Deeper : Left { }
    public abstract class AbstractLeaf : Root { }
    public class Generic<T> : Root { }
    public sealed class GenericChild : Generic<int> { }
}
