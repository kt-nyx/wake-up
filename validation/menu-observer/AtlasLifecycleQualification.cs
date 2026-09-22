// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

// One opt-in startup correctness case. Observation hooks are on Wake-Up methods,
// not guarded native methods, until an actual batch has yielded to a later frame.
internal static class AtlasLifecycleQualification
{
    private const string Owner = "local.fixture.atlas-lifecycle";
    internal const string ExpectedError = "FIXTURE_EXPECTED_ATLAS_LIFECYCLE_CALLBACK_01";
    private static readonly FieldInfo CurrentEvent = AccessTools.Field(typeof(LongEventHandler), "currentEvent");
    private static readonly FieldInfo CallbackList = AccessTools.Field(typeof(LongEventHandler), "toExecuteWhenFinished");
    private static readonly FieldInfo Executing = AccessTools.Field(typeof(LongEventHandler), "executingToExecuteWhenFinished");
    private static readonly FieldInfo GlobalAtlases = AccessTools.Field(typeof(GlobalTextureAtlasManager), "staticTextureAtlases");
    private static readonly FieldInfo Textures = AccessTools.Field(typeof(StaticTextureAtlas), "textures");
    private static readonly FieldInfo Tiles = AccessTools.Field(typeof(StaticTextureAtlas), "tiles");
    private static readonly List<string> order = new();
    private static readonly List<string> errors = new();
    private static readonly List<StaticTextureAtlas> nativeGroups = new();
    private static StaticTextureAtlas[] initialGroups = Array.Empty<StaticTextureAtlas>();
    private static Type scheduler = null!;
    private static FieldInfo pending = null!, owningEvent = null!, advances = null!;
    private static Harmony harmony = null!;
    private static string directory = "";
    private static bool installed, injected, finished, inDrain, originalEventPresent;
    private static object? originalEvent;
    private static Action? originalEventCallback;
    private static FieldInfo? eventCallbackField;
    private static int firstYieldFrame = -1, injectionFrame = -1, drainFrame = -1, drainEndFrame = -1;
    private static long yieldAdvances;
    private static int drains, drainEnds, cleanupStarts, cleanupEnds, requests, nativeCallbackPostfixes;
    private static int expectedThrows, expectedLogs, sentinels, eventCallbacks, originalEventCalls;
    private static int[] trailingStarts = Array.Empty<int>(), trailingEnds = Array.Empty<int>();

    internal static void Install(string output)
    {
        directory = output;
        try
        {
            Type runtime = AccessTools.TypeByName("WakeUp.AtlasRuntime") ?? throw new InvalidOperationException("atlas-runtime-absent");
            scheduler = runtime.Assembly.GetType("WakeUp.AtlasBatchScheduling", true)!;
            pending = AccessTools.Field(scheduler, "pending");
            owningEvent = AccessTools.Field(scheduler, "owningEvent");
            advances = AccessTools.Field(scheduler, "advances");
            harmony = new Harmony(Owner);
            harmony.Patch(AccessTools.Method(runtime, "RequestNativeDrain"), postfix: Hook(nameof(NativeRequested)));
            harmony.Patch(AccessTools.Method(runtime, "ProbeNativeBake"), postfix: Hook(nameof(NativeBaked)));
            harmony.Patch(AccessTools.Method(scheduler, "NativeCleanupSuffix"), prefix: Hook(nameof(CleanupBefore)), postfix: Hook(nameof(CleanupAfter)));
            harmony.Patch(AccessTools.Method(scheduler, "DrainAfterLifecycleChange"), prefix: Hook(nameof(DrainBefore)), postfix: Hook(nameof(DrainAfter)));
            installed = true;
            // Mod construction runs on the loading worker. Queue Unity object
            // creation at its ordinary main-thread callback boundary, before
            // the later native static-constructor/atlas completion callback.
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Application.logMessageReceived += Logged;
                var runner = new GameObject("FixtureAtlasLifecycleObserver");
                Object.DontDestroyOnLoad(runner);
                runner.AddComponent<AtlasLifecycleQualificationRunner>();
            });
            Receipt("event", "installed", "gameModule", typeof(StaticTextureAtlas).Assembly.ManifestModule.ModuleVersionId,
                "productModule", runtime.Assembly.ManifestModule.ModuleVersionId,
                "observerModule", typeof(AtlasLifecycleQualification).Assembly.ManifestModule.ModuleVersionId,
                "expectedErrorMarker", ExpectedError);
        }
        catch (Exception e) { Failure(e); }
    }

    private static HarmonyMethod Hook(string name) => new(typeof(AtlasLifecycleQualification), name);
    private static bool Pending => pending.GetValue(null) != null;
    private static List<Action> Callbacks => (List<Action>)CallbackList.GetValue(null);
    private static bool IsExecuting => (bool)Executing.GetValue(null);
    private static List<StaticTextureAtlas> Atlases => (List<StaticTextureAtlas>)GlobalAtlases.GetValue(null);

    internal static void Tick()
    {
        if (!installed || finished || injected) return;
        Observe(() =>
        {
            long count = (long)advances.GetValue(null);
            if (!Pending || count < 1) return;
            if (firstYieldFrame < 0)
            {
                firstYieldFrame = Time.frameCount; yieldAdvances = count;
                Mark("first-yield-observed");
                return;
            }
            if (Time.frameCount <= firstYieldFrame) return;
            Inject();
        });
    }

    private static void Inject()
    {
        originalEvent = CurrentEvent.GetValue(null);
        if (originalEvent == null || !ReferenceEquals(originalEvent, owningEvent.GetValue(null)) || !IsExecuting)
            throw new InvalidOperationException("atlas-lifecycle-not-owning-native-event");
        injectionFrame = Time.frameCount;
        initialGroups = Atlases.ToArray();
        eventCallbackField = AccessTools.Field(originalEvent.GetType(), "callback");
        originalEventCallback = (Action?)eventCallbackField.GetValue(originalEvent);
        originalEventPresent = originalEventCallback != null;

        // Preserve each already-queued action and its exception behavior, while
        // independently recording that native trailing work runs once in order.
        var originals = Callbacks.ToArray();
        trailingStarts = new int[originals.Length]; trailingEnds = new int[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            int index = i; Action action = originals[i];
            Callbacks[i] = () => RunTrailing(index, action);
        }
        eventCallbackField.SetValue(originalEvent, (Action)EventCompleted);
        injected = true;
        // This no-op postfix is deliberately foreign to the product guard. It
        // does not suppress the native method or alter its callback list/state.
        harmony.Patch(AccessTools.Method(typeof(LongEventHandler), "ExecuteToExecuteWhenFinished"), postfix: Hook(nameof(NativeCallbacksAfter)));
        LongEventHandler.ExecuteWhenFinished(ThrowExpected);
        LongEventHandler.ExecuteWhenFinished(Sentinel);
        Mark("hook-published");
        Receipt("event", "injection-state", "pending", Pending, "executingCallbacks", IsExecuting,
            "trailingOriginals", originals.Length, "queuedCallbacks", Callbacks.Count,
            "priorGroups", initialGroups.Length, "originalEventCallbackPresent", originalEventPresent,
            "firstYieldFrame", firstYieldFrame, "injectionFrame", injectionFrame, "yieldAdvances", yieldAdvances);
    }

    private static void RunTrailing(int index, Action action)
    {
        Observe(() => { trailingStarts[index]++; Mark("trailing-" + index + "-start"); CheckTrailingState(); });
        try { action(); }
        catch (Exception e) { Failure(new InvalidOperationException("Unexpected original trailing callback failure: " + index, e)); throw; }
        Observe(() => { trailingEnds[index]++; Mark("trailing-" + index + "-end"); });
    }

    private static void ThrowExpected()
    {
        Observe(() =>
        {
            expectedThrows++; Mark("expected-throw"); CheckTrailingState();
            Receipt("event", "expected-callback-injection", "expectedInjection", true, "marker", ExpectedError);
        });
        throw new InvalidOperationException(ExpectedError);
    }

    private static void Sentinel() => Observe(() => { sentinels++; Mark("sentinel"); CheckTrailingState(); });
    private static void CheckTrailingState()
    {
        // Native ExecuteToExecuteWhenFinished is intentionally still true while
        // calling actions. It becomes false after the complete callback loop.
        if (Pending || owningEvent.GetValue(null) != null || !IsExecuting || cleanupEnds != 1 || drainEnds != 1)
            throw new InvalidOperationException("trailing-callback-overtook-atlas-release");
    }

    private static void EventCompleted()
    {
        Observe(() =>
        {
            eventCallbacks++; Mark("event-callback");
            if (Pending || owningEvent.GetValue(null) != null || IsExecuting || Callbacks.Count != 0
                || !ReferenceEquals(originalEvent, CurrentEvent.GetValue(null)))
                throw new InvalidOperationException("event-callback-before-native-state-release");
        });
        // Restore the field before invoking the original action. A callback
        // which inspects its own native event observes its original delegate.
        eventCallbackField!.SetValue(originalEvent, originalEventCallback);
        if (originalEventCallback != null) { originalEventCalls++; originalEventCallback(); }
        Observe(() => Mark("event-callback-returned"));
    }

    private static void DrainBefore() => Observe(() =>
    {
        drains++; inDrain = true; drainFrame = Time.frameCount; Mark("drain-start");
        if (!injected || !Pending || !ReferenceEquals(originalEvent, CurrentEvent.GetValue(null)))
            throw new InvalidOperationException("drain-did-not-retain-original-event");
    });
    private static void DrainAfter(bool __runOriginal) => Observe(() =>
    {
        drainEnds++; drainEndFrame = Time.frameCount; Mark("drain-end"); inDrain = false;
        if (!__runOriginal || Pending || owningEvent.GetValue(null) != null || IsExecuting
            || cleanupEnds != 1 || !ReferenceEquals(originalEvent, CurrentEvent.GetValue(null)))
            throw new InvalidOperationException("drain-did-not-release-scheduler-state");
    });
    private static void CleanupBefore() => Observe(() => { cleanupStarts++; Mark("cleanup-start"); });
    private static void CleanupAfter(bool __runOriginal) => Observe(() =>
    {
        cleanupEnds++; Mark("cleanup-end");
        if (!__runOriginal || !inDrain || Pending || owningEvent.GetValue(null) != null)
            throw new InvalidOperationException("cleanup-did-not-follow-owned-work");
    });
    private static void NativeRequested() => Observe(() => { requests++; Mark("native-drain-request"); });
    private static void NativeBaked(StaticTextureAtlas __0, bool __runOriginal) => Observe(() =>
    {
        if (!injected) return;
        nativeGroups.Add(__0); Mark("native-group-baked");
        if (!__runOriginal || !inDrain || Time.frameCount != drainFrame
            || ((ICollection)Textures.GetValue(__0)).Count != ((IDictionary)Tiles.GetValue(__0)).Count)
            throw new InvalidOperationException("native-group-did-not-complete-inside-drain");
    });
    private static void NativeCallbacksAfter(bool __runOriginal) => Observe(() =>
    {
        if (!ReferenceEquals(originalEvent, CurrentEvent.GetValue(null)))
        {
            // The ordinary startup path also calls the empty callback runner
            // after clearing the observed event. It is not a replay of this
            // event's actions and must not count as its completion loop.
            Receipt("event", "native-callback-loop-outside-observed-event", "frame", Time.frameCount,
                "queuedCallbacks", Callbacks.Count, "pending", Pending, "executingCallbacks", IsExecuting);
            return;
        }
        nativeCallbackPostfixes++; Mark("native-callback-loop-returned");
        if (!__runOriginal || Pending || IsExecuting || Callbacks.Count != 0 || sentinels != 1)
            throw new InvalidOperationException("native-callback-loop-did-not-finish");
    });
    private static void Logged(string condition, string stackTrace, LogType type)
    {
        if (injected && condition.Contains(ExpectedError)
            && condition.Contains("Could not execute post-long-event action")) expectedLogs++;
    }

    internal static void Menu()
    {
        if (finished) return;
        finished = true;
        Observe(() =>
        {
            Mark("menu");
            bool orderCorrect = Before("hook-published", "drain-start") && Before("drain-start", "native-drain-request")
                && Before("native-drain-request", "cleanup-start") && Before("cleanup-start", "cleanup-end")
                && Before("cleanup-end", "drain-end") && Before("drain-end", "expected-throw")
                && Before("expected-throw", "sentinel") && Before("sentinel", "native-callback-loop-returned")
                && Before("native-callback-loop-returned", "event-callback") && Before("event-callback-returned", "menu");
            for (int i = 0; i < trailingStarts.Length; i++)
                orderCorrect &= Before(i == 0 ? "drain-end" : "trailing-" + (i - 1) + "-end", "trailing-" + i + "-start")
                    && Before("trailing-" + i + "-start", "trailing-" + i + "-end")
                    && Before("trailing-" + i + "-end", "expected-throw");
            bool groupsIntact = nativeGroups.Count > 0 && initialGroups.Concat(nativeGroups)
                .All(group => Atlases.Count(item => ReferenceEquals(item, group)) == 1);
            bool released = installed && !Pending && owningEvent.GetValue(null) == null && !IsExecuting
                && Callbacks.Count == 0 && CurrentEvent.GetValue(null) == null && !LongEventHandler.AnyEventNowOrWaiting;
            bool pass = installed && injected && firstYieldFrame >= 0 && injectionFrame > firstYieldFrame && yieldAdvances > 0
                && drains == 1 && drainEnds == 1 && drainFrame == drainEndFrame && cleanupStarts == 1 && cleanupEnds == 1
                && requests == 1 && expectedThrows == 1 && sentinels == 1 && nativeCallbackPostfixes == 1
                && eventCallbacks == 1 && originalEventCalls == (originalEventPresent ? 1 : 0)
                && trailingStarts.All(count => count == 1) && trailingEnds.All(count => count == 1)
                && groupsIntact && released && orderCorrect && errors.Count == 0;
            Receipt("event", "complete", "pass", pass, "injected", injected, "firstYieldFrame", firstYieldFrame,
                "injectionFrame", injectionFrame, "drainFrame", drainFrame, "drainEndFrame", drainEndFrame,
                "drains", drains, "drainEnds", drainEnds, "cleanupStarts", cleanupStarts, "cleanupEnds", cleanupEnds,
                "nativeDrainRequests", requests, "nativeGroups", nativeGroups.Count, "globalGroupsIntact", groupsIntact,
                "originalTrailingCallbacks", trailingStarts.Length, "expectedThrows", expectedThrows, "expectedNativeErrorLogs", expectedLogs,
                "expectedErrorMarker", ExpectedError, "sentinels", sentinels, "nativeCallbackLoops", nativeCallbackPostfixes,
                "eventCallbacks", eventCallbacks, "originalEventCallbackPresent", originalEventPresent,
                "originalEventCalls", originalEventCalls, "stateReleased", released, "orderCorrect", orderCorrect,
                "errors", errors.Count, "timeline", string.Join(" -> ", order));
        });
        try { harmony?.UnpatchAll(Owner); Application.logMessageReceived -= Logged; } catch (Exception e) { Failure(e); }
    }

    private static bool Before(string first, string second) => order.IndexOf(first) >= 0 && order.IndexOf(second) > order.IndexOf(first);
    private static void Mark(string name) { order.Add(name); Receipt("event", name, "sequence", order.Count, "frame", Time.frameCount); }
    private static void Observe(Action action) { try { action(); } catch (Exception e) { Failure(e); } }
    private static void Failure(Exception error)
    {
        errors.Add(error.ToString());
        try { Receipt("event", "observer-error", "reason", error.ToString()); } catch { }
    }
    private static string Quote(object? value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture)!
        .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    private static void Receipt(params object?[] pairs)
    {
        var fields = new List<string> { "\"schema\":\"fixture-atlas-lifecycle.v1\"" };
        for (int i = 0; i < pairs.Length; i += 2)
        {
            object? value = pairs[i + 1];
            fields.Add(Quote(pairs[i]) + ":" + (value == null ? "null" : value is bool b ? b.ToString().ToLowerInvariant()
                : value is int || value is long ? Convert.ToString(value, CultureInfo.InvariantCulture) : Quote(value)));
        }
        File.AppendAllText(Path.Combine(directory, "atlas-lifecycle-qualification.jsonl"), "{" + string.Join(",", fields) + "}\n");
    }
}

public sealed class AtlasLifecycleQualificationRunner : MonoBehaviour
{
    private void Update() => AtlasLifecycleQualification.Tick();
}
