// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class LoadingDisplayTests
{
    [TestCase("prefix", true)]
    [TestCase("postfix", false)]
    [TestCase("prefix-and-postfix", false)]
    [TestCase("prefix-and-finalizer", false)]
    [TestCase("prefix-and-transpiler", false)]
    [TestCase("prefix-and-inner-prefix", false)]
    [TestCase("prefix-and-inner-postfix", false)]
    [TestCase("wrong-owner", false)]
    public void OnlyExactReviewedPrepatcherPrefixCanShareNativeDrawing(string kind, bool admitted)
    {
        string suppliers = Environment.GetEnvironmentVariable("WAKE_UP_PREPATCHER_ASSEMBLIES_DIR")!;
        string managed = Environment.GetEnvironmentVariable("WAKE_UP_RIMWORLD_MANAGED_DIR")!;
        ResolveEventHandler resolver = (_, request) =>
        {
            string name = new AssemblyName(request.Name).Name + ".dll";
            string path = new[] { suppliers, managed }.Select(p => Path.Combine(p, name)).FirstOrDefault(File.Exists);
            return path == null ? null : Assembly.LoadFrom(path);
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            Assembly assembly = Assembly.LoadFrom(Path.Combine(suppliers, "PrepatcherImpl.dll"));
            MethodInfo method = assembly.GetType("Prepatcher.HarmonyPatches", true)!.GetMethod("DrawPrestarterInfo", BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.That(method.Module.ModuleVersionId, Is.EqualTo(LoadingDisplayRuntime.PrepatcherDrawModule));
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            Assert.That(hash, Is.EqualTo(LoadingDisplayRuntime.PrepatcherDrawBody));
            var hook = new Patch(new HarmonyMethod(method), 0, kind == "wrong-owner" ? "wakeup.tests.impostor" : "prepatcher");
            Patch[] empty = Array.Empty<Patch>();
            // Real Harmony records retain each kind independently; use equal
            // indexes to prove a prefix cannot disguise another registration.
            var record = new Patches(kind != "postfix" ? new[] { hook } : empty,
                kind == "postfix" || kind == "prefix-and-postfix" ? new[] { hook } : empty,
                kind == "prefix-and-transpiler" ? new[] { hook } : empty,
                kind == "prefix-and-finalizer" ? new[] { hook } : empty,
                kind == "prefix-and-inner-prefix" ? new[] { hook } : empty,
                kind == "prefix-and-inner-postfix" ? new[] { hook } : empty);
            Assert.That(LoadingDisplayRuntime.AllowsPrepatcherDraw(LoadingDisplayRuntime.Targets[1], hook, record), Is.EqualTo(admitted));
            Assert.That(LoadingDisplayRuntime.AllowsPrepatcherDraw(LoadingDisplayRuntime.Targets[0], hook, record), Is.False);
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }
    [Test]
    public void NativeObserverContractsMatchFrozenBodies()
    {
        for (int i = 0; i < LoadingDisplayRuntime.Targets.Length; i++)
        {
            Assert.That(SemanticMethodIdentity.TryHash(LoadingDisplayRuntime.Targets[i], out string hash, out string reason), Is.True, reason);
            Assert.That(hash, Is.EqualTo(LoadingDisplayRuntime.ExpectedBodies[i]));
        }
    }

    [Test]
    public void ItemUpdatesDoNotFormatOrForceFramesAndDiagnosticsAreBounded()
    {
        var state = new LoadingDisplayState(10, true);
        int reads = 0;
        LoadingCacheSnapshot Cache() { reads++; return new(true, true, "", CacheAction.Normal, 2, 1, 1, 0, 1048576, "missing"); }
        Assert.That(state.Refresh(10, 20, Cache, out var first), Is.True);
        for (int i = 0; i < 1000; i++)
        {
            state.Observe("Stage " + i, 10.1);
            Assert.That(state.Refresh(10.1, 20, Cache, out var cached), Is.False);
            Assert.That(cached, Is.SameAs(first));
        }
        Assert.That(reads, Is.EqualTo(1));
        Assert.That(state.Refresh(10.25, 20, Cache, out var next), Is.True);
        Assert.That(next, Does.Contain("Stage 999").And.Contain("2 reused, 1 rebuilt, 1 misses").And.Not.Contain("%"));
        string report = state.Stop("ended");
        Assert.That(report, Does.Contain("\"historyTruncated\":true"));
        Assert.That(report.Split(new[] { "secondsSinceObservation" }, StringSplitOptions.None), Has.Length.EqualTo(65));
        state.Observe("ignored", 11);
        Assert.That(state.Transitions, Is.EqualTo(1000));
        Assert.That(state.Refresh(12, 20, Cache, out _), Is.False);
    }

    [Test]
    public void OrdinaryDiagnosticsContainNoHistoryAndElapsedStartsAtObservation()
    {
        var state = new LoadingDisplayState(100, false);
        state.Observe("Loading definitions", 101);
        state.Refresh(165, 42, () => new(false, false, "", CacheAction.Normal, 0, 0, 0, 0, 0, "none"), out var text);
        Assert.That(text, Does.Contain("1:05").And.Contain("cache: off").And.Contain("42 mods (names hidden)").And.Not.Contain("Diagnostics"));
        Assert.That(state.Stop("ended"), Does.Not.Contain("stages"));
    }

    [TestCase(false, false, "", (int)CacheAction.Normal, 0, "none", "cache: off")]
    [TestCase(true, false, "body", (int)CacheAction.Normal, 0, "none", "unavailable")]
    [TestCase(true, true, "", (int)CacheAction.Bypass, 0, "none", "bypassed")]
    [TestCase(true, true, "", (int)CacheAction.Clear, 0, "none", "clear requested")]
    [TestCase(true, true, "", (int)CacheAction.Rebuild, 0, "none", "Rebuild requested")]
    [TestCase(true, true, "", (int)CacheAction.Normal, 2, "read-io", "2 errors")]
    [TestCase(true, true, "", (int)CacheAction.Normal, 0, "quota", "Storage limit")]
    public void CacheStatesExplainFallback(bool requested, bool installed, string refusal, int action, long errors, string reason, string expected)
        => Assert.That(new LoadingCacheSnapshot(requested, installed, refusal, (CacheAction)action, 0, 0, 0, errors, 0, reason).Describe(), Does.Contain(expected));

    [Test]
    public void ActualCacheHitMissAndCorruptionReachTheDisplay()
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "display-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var cache = new PngCache(root);
            string key = new string('A', 64);
            Assert.That(cache.Read(key), Is.Null);
            var missing = Snapshot(cache).Describe();
            Assert.That(missing, Does.Contain("1 misses"));
            cache.Write(key, new PngCache.Entry { Width = 1, Height = 1, TextureFormat = 4, Mips = 1, Pixels = new byte[4] });
            Assert.That(cache.Read(key), Is.Not.Null);
            Assert.That(Snapshot(cache).Describe(), Does.Contain("1 reused, 1 rebuilt"));
            File.WriteAllBytes(Path.Combine(root, "v1", key + ".bin"), new byte[4]);
            Assert.That(cache.Read(key), Is.Null);
            Assert.That(Snapshot(cache).Describe(), Does.Contain("errors"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static LoadingCacheSnapshot Snapshot(PngCache cache) => new(true, true, "", CacheAction.Normal,
        cache.Hits, cache.Misses, cache.Writes, cache.Errors, cache.StoredBytes, cache.LastReason);

    [Test]
    public void SettingActivationIsLatchedForTheNextLaunchAndExplicitControlsWin()
    {
        string profile = Path.GetFullPath(TestContext.CurrentContext.WorkDirectory);
        var settings = new WakeUpSettings();
        var existing = UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile);
        settings.LoadingDisplay = settings.LoadingDisplayDiagnostics = true;
        var next = UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile);
        Assert.That(LoadingDisplayRuntime.Selected(existing), Is.False);
        Assert.That(LoadingDisplayRuntime.Selected(next), Is.True);
        Assert.That(next, Does.Contain("--wake-up-loading-display-diagnostics=on"));
        settings.LoadingDisplay = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile), Is.EqualTo(existing));
        settings.LoadingDisplay = true;
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile), Is.Empty);
        foreach (string control in new[] { "--wake-up-bypass", "--wake-up-mode=baseline", "--wake-up-loading-display=off" })
            Assert.That(LoadingDisplayRuntime.Selected(new[] { "--wake-up-mode=candidate", "-savedatafolder=" + profile, "--wake-up-loading-display=on", control }), Is.False);
    }

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    public void EarlyAndLateCompetingScreenPatchesRefuseOnlyOurDisplay(bool late, int target)
    {
        var harmony = new Harmony(LoadingDisplayRuntime.Owner);
        var other = new Harmony("wakeup.tests.other-display");
        var logger = new Harmony("wakeup.tests.display-logger");
        try
        {
            logger.Patch(AccessTools.Method(typeof(Log), nameof(Log.Message), new[] { typeof(string) }), prefix: new HarmonyMethod(typeof(LoadingDisplayTests), nameof(SkipLog)));
            if (late) { LoadingDisplayRuntime.InstallHooks(harmony); LoadingDisplayRuntime.Begin(false); }
            other.Patch(LoadingDisplayRuntime.Targets[target], prefix: new HarmonyMethod(typeof(LoadingDisplayTests), nameof(SkipLog)));
            if (late)
            {
                Assert.That(LoadingDisplayRuntime.CheckOwnership(), Is.False);
                Assert.That(LoadingDisplayRuntime.CurrentState, Is.Null);
            }
            else Assert.That((Action)(() => LoadingDisplayRuntime.InstallHooks(harmony)), Throws.TypeOf<InvalidOperationException>());
            Assert.That(Harmony.GetPatchInfo(LoadingDisplayRuntime.Targets[target]).Owners, Does.Contain(other.Id));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            other.UnpatchAll(other.Id);
            logger.UnpatchAll(logger.Id);
            LoadingDisplayRuntime.Stop("test", "cleanup");
        }
    }

    [TestCase(true, true, "ilyvion.loadingprogress", true)]
    [TestCase(false, true, "ilyvion.loadingprogress", false)]
    [TestCase(true, false, "ilyvion.loadingprogress", false)]
    [TestCase(true, true, "ilyvion.loadingprogress.fork", false)]
    [TestCase(true, true, "unrelated.mod", false)]
    public void DisplayRefusalExplainsChoicesOnlyForSelectedActiveExactPackage(
        bool selected, bool active, string packageId, bool explainsChoices)
    {
        var running = AccessTools.Field(typeof(LoadedModManager), "runningMods");
        object previous = running.GetValue(null);
        string previousStatus = LoadingDisplayRuntime.Status;
        var status = AccessTools.Field(typeof(LoadingDisplayRuntime), "<Status>k__BackingField");
        var harness = new Harmony("wakeup.tests.display-selection");
        var mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        AccessTools.Field(typeof(ModContentPack), "packageIdInt").SetValue(mod, packageId);
        string[] args = UserStartupSelection.Resolve(Array.Empty<string>(),
            new WakeUpSettings { LoadingDisplay = selected }, TestContext.CurrentContext.WorkDirectory);
        try
        {
            // The candidate exists in this test, but only active content belongs
            // in the native running list. Display names/installed inventory are
            // deliberately not inputs. Other features can remain selected.
            running.SetValue(null, active ? new List<ModContentPack> { mod } : new List<ModContentPack>());
            status.SetValue(null, "off");
            harness.Patch(AccessTools.Method(typeof(Log), nameof(Log.Message), new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(LoadingDisplayTests), nameof(SkipLog)));
            // Stop at binary admission on other paths: this host has no Unity
            // UI. The actual active-package refusal runs before this boundary.
            harness.Patch(AccessTools.Method(typeof(RuntimeIdentity), nameof(RuntimeIdentity.ValidateBinaryIdentity),
                new[] { typeof(string).MakeByRefType() }),
                prefix: new HarmonyMethod(typeof(LoadingDisplayTests), nameof(RefuseIdentity)));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                LoadingDisplayRuntime.TryInitialize(args);
                Assert.That(LoadingDisplayRuntime.Status.Contains("To keep Loading Progress"), Is.EqualTo(explainsChoices));
                Assert.That(LoadingDisplayRuntime.CurrentState, Is.Null);
                foreach (var target in LoadingDisplayRuntime.Targets)
                    Assert.That(Harmony.GetPatchInfo(target)?.Owners.Contains(LoadingDisplayRuntime.Owner) == true, Is.False);
                if (explainsChoices)
                {
                    Assert.That(LoadingDisplayRuntime.Status, Does.Contain("display is inactive"));
                    Assert.That(LoadingDisplayRuntime.Status, Does.Contain("disable Loading Progress in Mods"));
                    Assert.That(LoadingDisplayRuntime.Status, Does.Contain("Restart"));
                }
                if (!selected) Assert.That(LoadingDisplayRuntime.Status, Is.EqualTo("off"));
            }
        }
        finally
        {
            harness.UnpatchAll(harness.Id);
            running.SetValue(null, previous);
            status.SetValue(null, previousStatus);
        }
    }
    private static bool RefuseIdentity(out string reason, ref bool __result)
    {
        reason = "offline-test-refusal";
        __result = false;
        return false;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeEventsKeepActionDeferredCallbackOrderAndCleanup(bool fail)
    {
        var harmony = new Harmony(LoadingDisplayRuntime.Owner);
        var logger = new Harmony("wakeup.tests.display-logger");
        var order = new List<string>();
        Exception failure = new InvalidOperationException("native-test-failure");
        Exception? received = null;
        try
        {
            logger.Patch(AccessTools.Method(typeof(Log), nameof(Log.Error), new[] { typeof(string) }), prefix: new HarmonyMethod(typeof(LoadingDisplayTests), nameof(SkipLog)));
            // The native profiler calls Unity's engine clock. The offline host
            // has no engine; skip only its instrumentation, retaining the real
            // callback list, exception handling and event loop below.
            logger.Patch(AccessTools.Method(typeof(DeepProfiler), nameof(DeepProfiler.Start)), prefix: new HarmonyMethod(typeof(LoadingDisplayTests), nameof(SkipLog)));
            logger.Patch(AccessTools.Method(typeof(DeepProfiler), nameof(DeepProfiler.End)), prefix: new HarmonyMethod(typeof(LoadingDisplayTests), nameof(SkipLog)));
            LoadingDisplayRuntime.InstallHooks(harmony);
            Assert.That(Harmony.GetPatchInfo(LoadingDisplayRuntime.Targets[1]), Is.Null,
                "The native repaint acknowledgement and contents remain unpatched.");
            Assert.That(Harmony.GetPatchInfo(LoadingDisplayRuntime.Targets[0]).Transpilers, Is.Empty);
            Assert.That(Harmony.GetPatchInfo(AccessTools.Method(typeof(StaticConstructorOnStartupUtility), "CallAll")), Is.Null);
            LoadingDisplayRuntime.Begin(false);
            var observed = LoadingDisplayRuntime.CurrentState!;
            LongEventHandler.QueueLongEvent(() => { order.Add("action"); if (fail) throw failure; },
                null, false, e => { received = e; order.Add("handler"); }, callback: () => order.Add("callback"));
            LongEventHandler.LongEventsUpdate(out _);
            LongEventHandler.SetCurrentEventText("Observed stage");
            Assert.That(observed.Transitions, Is.EqualTo(1), "The actual native setter must reach the installed postfix.");
            // A nonempty standard-window message intentionally waits for native
            // Repaint. Use the native setter to empty it in this headless test.
            LongEventHandler.SetCurrentEventText("");
            LongEventHandler.ExecuteWhenFinished(() => order.Add("deferred"));
            LongEventHandler.LongEventsUpdate(out _);
            Assert.That(order, Is.EqualTo(fail ? new[] { "action", "handler" } : new[] { "action", "deferred", "callback" }));
            Assert.That(received, Is.SameAs(fail ? failure : null));
            Assert.That(LongEventHandler.AnyEventNowOrWaiting, Is.False);
            Assert.That(LoadingDisplayRuntime.CurrentState, Is.Null);
            Assert.That(observed.Active, Is.False);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            logger.UnpatchAll(logger.Id);
            LoadingDisplayRuntime.Stop("test", "cleanup");
            LongEventHandler.ClearQueuedEvents();
            ((List<Action>)AccessTools.Field(typeof(LongEventHandler), "toExecuteWhenFinished").GetValue(null)).Clear();
            AccessTools.Field(typeof(LongEventHandler), "currentEvent").SetValue(null, null);
        }
    }
    private static bool SkipLog() => false;
}
