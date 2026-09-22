// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using HarmonyLib;
using NUnit.Framework;
using UnityEngine;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class AssetRouteCacheTests
{
    private readonly AssetRouteCache<Texture2D> cache = new();
    private List<ModContentPack> mods = null!;
    private object? oldMods, oldThread;
    private const string Path = "Things/Example/LongTexturePath";
    private static Texture2D Texture() => (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
    private static ModContentPack Mod()
    {
        var mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        AccessTools.Field(typeof(ModContentPack), "textures").SetValue(mod, new ModContentHolder<Texture2D>(mod));
        return mod;
    }
    private static Dictionary<string, Texture2D> Content(ModContentPack mod) => mod.GetContentHolder<Texture2D>().contentList;
    [SetUp]
    public void SetUp()
    {
        cache.Clear();
        oldMods = AccessTools.Field(typeof(LoadedModManager), "runningMods").GetValue(null);
        var initializing = AccessTools.Field(typeof(UnityData).Assembly.GetType("Verse.UnityDataInitializer"), "initializing");
        object previousInitializing = initializing.GetValue(null);
        initializing.SetValue(null, true);
        oldThread = AccessTools.Field(typeof(UnityData), "mainThreadId").GetValue(null);
        initializing.SetValue(null, previousInitializing);
        mods = new List<ModContentPack> { Mod(), Mod(), Mod() };
        AccessTools.Field(typeof(LoadedModManager), "runningMods").SetValue(null, mods);
        AccessTools.Field(typeof(UnityData), "mainThreadId").SetValue(null, Thread.CurrentThread.ManagedThreadId);
    }
    [TearDown]
    public void TearDown()
    {
        cache.Clear();
        AccessTools.Field(typeof(LoadedModManager), "runningMods").SetValue(null, oldMods);
        AccessTools.Field(typeof(UnityData), "mainThreadId").SetValue(null, oldThread);
        AccessTools.Field(typeof(AssetRoutingRuntime), "enabled").SetValue(null, false);
        ((AssetRouteCache<Texture2D>)AccessTools.Field(typeof(AssetRoutingRuntime), "Textures").GetValue(null)).Clear();
    }
    private Texture2D Routed()
    {
        Assert.That(cache.TryGet(mods, Path, out var result), Is.True);
        Assert.That(ReferenceEquals(result, ContentFinder<Texture2D>.Get(Path)), Is.True);
        return result!;
    }
    [Test]
    public void NativeLastOverrideAndRepeatIdentitySkipHolderSearch()
    {
        var first = Texture(); var last = Texture();
        Content(mods[0])[Path] = first; Content(mods[1])[Path] = last;
        Assert.That(ReferenceEquals(Routed(), last), Is.True);
        long before = cache.HolderLookups;
        Assert.That(ReferenceEquals(Routed(), last), Is.True);
        Assert.That(cache.HolderLookups - before, Is.EqualTo(1));
        Assert.That(ReferenceEquals(mods[0].GetContentHolder<Texture2D>().Get(Path), first), Is.True);
        Assert.That(cache.TryGet(mods, Path.ToLowerInvariant(), out _), Is.False);
    }
    [Test]
    public void MissingAndEmptyHigherHolderRemainRetryable()
    {
        Assert.That(cache.TryGet(mods, Path, out _), Is.False);
        Assert.That(cache.Count, Is.Zero);
        Content(mods[0])[Path] = Texture(); Routed();
        var added = Texture(); Content(mods[2])[Path] = added;
        Assert.That(ReferenceEquals(Routed(), added), Is.True);
        Content(mods[2]).Remove(Path);
        Assert.That(ReferenceEquals(Routed(), Content(mods[0])[Path]), Is.True);
        Content(mods[0]).Clear();
        Assert.That(cache.TryGet(mods, Path, out _), Is.False);
        Assert.That(cache.Count, Is.Zero);
    }
    [Test]
    public void SameLengthReplacementReorderProviderRemovalAndHolderReplacementRevalidate()
    {
        Content(mods[0])[Path] = Texture(); Content(mods[1])[Path] = Texture(); Routed();
        var replacement = Texture(); Content(mods[1])[Path] = replacement;
        Assert.That(ReferenceEquals(Routed(), replacement), Is.True);
        mods.Reverse(); Assert.That(ReferenceEquals(Routed(), Content(mods[2])[Path]), Is.True);
        mods.RemoveAt(2); Routed();
        var holder = new ModContentHolder<Texture2D>(mods[1]); holder.contentList[Path] = Texture();
        AccessTools.Field(typeof(ModContentPack), "textures").SetValue(mods[1], holder);
        Assert.That(ReferenceEquals(Routed(), holder.Get(Path)), Is.True);
        holder.contentList = new Dictionary<string, Texture2D> { [Path] = replacement };
        Assert.That(ReferenceEquals(Routed(), replacement), Is.True);
        mods = new List<ModContentPack>(mods);
        AccessTools.Field(typeof(LoadedModManager), "runningMods").SetValue(null, mods); Routed();
    }
    [Test]
    public void NewPathCannotRefreshStampAndHideOverrideOfOldPath()
    {
        Content(mods[0])[Path] = Texture(); Routed();
        var added = Texture(); Content(mods[2])[Path] = added;
        Content(mods[2])["other"] = Texture();
        Assert.That(cache.TryGet(mods, "other", out _), Is.True);
        Assert.That(ReferenceEquals(Routed(), added), Is.True);
    }
    [Test]
    public void PreparedPublicationKeepsSourceHolderOwnershipAndReloadSelectsCurrentObject()
    {
        // Both ordinary and prepared eager loaders publish into this same native
        // dictionary. No Unity restoration is attempted in this offline host.
        var source = Texture(); var prepared = Texture();
        Content(mods[0])[Path] = source; Content(mods[1])[Path] = prepared;
        Assert.That(ReferenceEquals(Routed(), prepared), Is.True); Routed();
        Assert.That(ReferenceEquals(mods[0].GetContentHolder<Texture2D>().Get(Path), source), Is.True);
        Content(mods[1]).Clear(); Content(mods[1])[Path] = source;
        Assert.That(ReferenceEquals(Routed(), source), Is.True);
        var enumerate = AccessTools.Method(typeof(PngRuntime), "Enumerate");
        var iterator = enumerate.GetCustomAttribute<System.Runtime.CompilerServices.IteratorStateMachineAttribute>()!.StateMachineType;
        Assert.That(PatchProcessor.GetOriginalInstructions(AccessTools.Method(iterator, "MoveNext")).Any(i =>
            i.operand is MethodInfo m && m.DeclaringType == typeof(PreparedTextureRuntime) && m.Name == "TryResolve"), Is.True);
    }
    [Test]
    public void UnknownDictionaryBehaviorRefusesAndFailureCanRetry()
    {
        Content(mods[0])[Path] = Texture(); Routed();
        mods[2].GetContentHolder<Texture2D>().contentList = new Dictionary<string, Texture2D>(new CustomComparer());
        Assert.Throws<InvalidOperationException>((Action)(() => { cache.TryGet(mods, Path, out _); }));
        cache.Clear(); // runtime's exception boundary does exactly this
        mods[2].GetContentHolder<Texture2D>().contentList = new Dictionary<string, Texture2D>();
        Routed();
        Assert.That(AssetRoutingRuntime.MutationStampsWork(), Is.True);
    }
    private sealed class CustomComparer : IEqualityComparer<string>
    { public bool Equals(string? x, string? y) => x == y; public int GetHashCode(string value) => value.GetHashCode(); }
    [Test]
    public void RuntimePreservesOtherTypesOffThreadAndMissingFallback()
    {
        EnableBridge();
        Content(mods[0])[Path] = Texture();
        Assert.That(AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out var value), Is.True);
        Assert.That(ReferenceEquals(value, Content(mods[0])[Path]), Is.True);
        Assert.That(AssetRoutingRuntime.TryGet(typeof(string), Path, out _), Is.False);
        Assert.That(cache.TryGet(mods, "missing", out _), Is.False);
        bool otherThread = true;
        var thread = new Thread(() => otherThread = AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out _));
        thread.Start(); thread.Join(); Assert.That(otherThread, Is.False);
        mods[2].GetContentHolder<Texture2D>().contentList = null!;
        Assert.That(AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out _), Is.False);
        Assert.That(AssetRoutingRuntime.Status, Does.Contain("could not validate"));
        mods[2].GetContentHolder<Texture2D>().contentList = new Dictionary<string, Texture2D>();
        Assert.That(AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out _), Is.True);
        Assert.That(AssetRoutingRuntime.Status, Does.Contain("is active"));
    }
    private static void EnableBridge()
    {
        AccessTools.Field(typeof(AssetRoutingRuntime), "guard").SetValue(null, new AssetRoutingPatchGuard());
        AccessTools.Field(typeof(AssetRoutingRuntime), "enabled").SetValue(null, true);
    }
    [Test]
    public void ActualInjectedGenericTextureEntryReturnsExistingHolderObject()
    {
        Content(mods[0])[Path] = Texture(); EnableBridge();
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(typeof(ContentFinder<>).Assembly.Location);
        Assert.That(AssetRoutingPrepatch.RewriteAssembly(module), Is.True);
        using var bytes = new System.IO.MemoryStream(); module.Write(bytes);
        var assembly = Assembly.Load(bytes.ToArray());
        var get = assembly.GetType("Verse.ContentFinder`1")!.MakeGenericType(typeof(Texture2D)).GetMethod("Get")!;
        Assert.That(ReferenceEquals(get.Invoke(null, new object[] { Path, true }), Content(mods[0])[Path]), Is.True);
        Assert.That(ReferenceEquals(get.Invoke(null, new object[] { Path, true }), Content(mods[0])[Path]), Is.True);
    }
    [Test]
    public void LaterPatchToAnotherClosedGenericInstanceRefusesThenRecovers()
    {
        var target = AccessTools.Method(typeof(ContentFinder<string>), "Get");
        var stub = AccessTools.Method(typeof(AssetRouteCacheTests), nameof(GuardStub));
        var harmony = new Harmony("WakeUp.AssetRouting.Tests.Foreign");
        var guard = new AssetRoutingPatchGuard();
        var state = (Dictionary<MethodBase, byte[]>)typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")!
            .GetField("state", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        state.TryGetValue(target, out var previous);
        Assert.That(AssetRoutingPatchGuard.Relevant(target), Is.True);
        Assert.That(guard.Allows(), Is.True);
        try
        {
            // The desktop CLR cannot detour this game generic. Publish a real
            // Harmony record under its closed key to exercise the actual guard
            // and deserializer without claiming a Mono generic-detour test.
            harmony.Patch(stub, prefix: new HarmonyMethod(typeof(AssetRouteCacheTests), nameof(EmptyPrefix)));
            lock (state) state[target] = state[stub];
            Assert.That(guard.Allows(), Is.False);
        }
        finally
        {
            lock (state) { if (previous == null) state.Remove(target); else state[target] = previous; }
            harmony.Unpatch(stub, HarmonyPatchType.All, harmony.Id);
        }
        Assert.That(guard.Allows(), Is.True);
    }
    private static void EmptyPrefix() { }
    [TestCase(true)]
    [TestCase(false)]
    public void PublishedForeignInnerHolderRecordsRefuseExistingRouteAndUpdateStatusThenRecover(bool prefix)
    {
        Content(mods[0])[Path] = Texture(); EnableBridge();
        Assert.That(AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out _), Is.True);
        Assert.That(AssetRoutingRuntime.Status, Does.Contain("is active"));
        var routes = (AssetRouteCache<Texture2D>)AccessTools.Field(typeof(AssetRoutingRuntime), "Textures").GetValue(null);
        Assert.That(routes.Count, Is.EqualTo(1));
        var target = AccessTools.Method(typeof(ModContentHolder<Texture2D>), "Get");
        Type harmonyAssemblyType = typeof(Harmony);
        var state = (Dictionary<MethodBase, byte[]>)harmonyAssemblyType.Assembly.GetType("HarmonyLib.HarmonySharedState")!
            .GetField("state", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        Type serialization = harmonyAssemblyType.Assembly.GetType("HarmonyLib.PatchInfoSerialization")!;
        byte[]? previous;
        lock (state) state.TryGetValue(target, out previous);
        try
        {
            // The pinned Harmony cannot emit its public inner hooks on this
            // desktop host. Publish its real inner-only serialized records,
            // as the background guard regression does, without detouring Get.
            object info = previous == null
                ? Activator.CreateInstance(harmonyAssemblyType.Assembly.GetType("HarmonyLib.PatchInfo")!)!
                : AccessTools.Method(serialization, "Deserialize").Invoke(null, new object[] { previous });
            const string foreignId = "WakeUp.AssetRouting.Tests.ForeignInner";
            var hook = new HarmonyMethod(typeof(AssetRouteCacheTests), nameof(EmptyPrefix));
            AccessTools.Method(info.GetType(), prefix ? "AddInnerPrefixes" : "AddInnerPostfixes")
                .Invoke(info, new object[] { foreignId, new[] { hook } });
            var published = (byte[])AccessTools.Method(serialization, "Serialize").Invoke(null, new[] { info });
            lock (state) state[target] = published;
            var record = Harmony.GetPatchInfo(target);
            Assert.That((prefix ? record.InnerPrefixes : record.InnerPostfixes).Any(p => p.owner == foreignId), Is.True);
            Assert.That(record.Prefixes.Concat(record.Postfixes).Concat(record.Transpilers).Concat(record.Finalizers), Is.Empty);
            Assert.That(AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out _), Is.False);
            Assert.That(routes.Count, Is.Zero);
            Assert.That(AssetRoutingRuntime.Status, Does.Contain("refused a changed hook chain"));
            lock (state) Assert.That(state[target], Is.SameAs(published));
        }
        finally
        {
            lock (state) { if (previous == null) state.Remove(target); else state[target] = previous; }
        }
        Assert.That(AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out var restored), Is.True);
        Assert.That(ReferenceEquals(restored, Content(mods[0])[Path]), Is.True);
        Assert.That(AssetRoutingRuntime.Status, Does.Contain("is active"));
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int GuardStub(int value) => value + 1;
    [Test]
    public void NewFeatureDefaultsOffAndExplicitSelectionsWin()
    {
        var settings = new WakeUpSettings();
        Assert.That(settings.AssetRouting, Is.False);
        var original = UserStartupSelection.Resolve(Array.Empty<string>(), settings, TestContext.CurrentContext.WorkDirectory);
        settings.AssetRouting = true;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, TestContext.CurrentContext.WorkDirectory),
            Does.Contain("--wake-up-asset-routing=on"));
        Assert.That(UserStartupSelection.Resolve(original, settings, TestContext.CurrentContext.WorkDirectory), Is.EqualTo(original));
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, TestContext.CurrentContext.WorkDirectory), Is.Empty);
    }
    [TestCase(0)]
    [TestCase(249)]
    public void RepeatCostIncludesVersionProofAndRuntimeGuard(int supplier)
    {
        const int providers = 250, calls = 12000;
        mods.Clear(); for (int i = 0; i < providers; i++) mods.Add(Mod());
        Content(mods[supplier])[Path] = Texture();
        for (int i = 0; i < providers; i++) Content(mods[i])["another"] = Texture();
        EnableBridge();
        for (int i = 0; i < 500; i++) { ContentFinder<Texture2D>.Get(Path); AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out _); }
        double Measure(Action action)
        { var clock = Stopwatch.StartNew(); for (int i = 0; i < calls; i++) action(); return clock.Elapsed.TotalMilliseconds; }
        for (int pair = 0; pair < 4; pair++)
        {
            double native, route;
            if (pair % 2 == 0)
            { native = Measure(() => ContentFinder<Texture2D>.Get(Path)); route = Measure(() => AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out _)); }
            else
            { route = Measure(() => AssetRoutingRuntime.TryGet(typeof(Texture2D), Path, out _)); native = Measure(() => ContentFinder<Texture2D>.Get(Path)); }
            TestContext.Out.WriteLine($"S11 supplier {supplier} pair {pair}: {calls} calls/{providers} providers native={native:F3}ms routed={route:F3}ms");
        }
        // Deterministic work evidence, independent of host timing/noise.
        cache.TryGet(mods, Path, out _); long lookups = cache.HolderLookups;
        cache.TryGet(mods, Path, out _);
        Assert.That(cache.HolderLookups - lookups, Is.EqualTo(1));
    }
}
