// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.IO;
using UnityEngine;
using Verse;

namespace WakeUp;

// Keeps the existing asynchronous startup event alive while owned atlas work
// yields. Native constructor calls run first; native cleanup, queued callbacks,
// and the event's completion callback cannot pass unfinished atlas work.
internal static class AtlasBatchScheduling
{
    internal const string Owner = "wakeup.atlas-batching";
    internal static readonly MethodInfo AsyncUpdate = AccessTools.Method(typeof(LongEventHandler), "UpdateCurrentAsynchronousEvent");
    internal static readonly MethodInfo Callbacks = AccessTools.Method(typeof(LongEventHandler), "ExecuteToExecuteWhenFinished");
    internal static readonly MethodInfo AtlasCallback = AccessTools.Method(
        typeof(PlayDataLoader).GetNestedType("<>c", BindingFlags.NonPublic), "<DoPlayLoad>b__4_4");
    private static readonly FieldInfo CallbackList = AccessTools.Field(typeof(LongEventHandler), "toExecuteWhenFinished");
    private static readonly FieldInfo ExecutingCallbacks = AccessTools.Field(typeof(LongEventHandler), "executingToExecuteWhenFinished");
    private static readonly FieldInfo CurrentEvent = AccessTools.Field(typeof(LongEventHandler), "currentEvent");
    private static readonly Action InvokeCallbacks = AccessTools.MethodDelegate<Action>(Callbacks);
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static IEnumerator? pending;
    private static object? owningEvent;
    private static bool armed;
    private static bool displayScheduling, displayPending;
    private static int displayCompletedCallbacks;
    private static int lastFrame = -1;
    private static long advances;
    internal static bool Installed { get; private set; }
    internal static bool Active => pending != null;
    internal static string Status { get; private set; } = "Atlas batching is off.";

    internal static bool Install()
    {
        if (Installed) return true;
        var harmony = new Harmony(Owner);
        try
        {
            var targets = new[] { AsyncUpdate, Callbacks, AtlasCallback,
                AccessTools.Method(typeof(LongEventHandler), "ExecuteWhenFinished"),
                AccessTools.Method(typeof(LongEventHandler), "ForceExecuteToExecuteWhenFinished") };
            var checks = new List<PublishedPatchGuard>();
            foreach (MethodInfo target in targets)
            {
                if (!AtlasContract.Matches(target)) throw new InvalidOperationException("Native startup callback changed: " + target);
                if (!PublishedPatchGuard.TryCreate(target, Owner, out var guard, allPatchKinds: true,
                    allowedForeignPatch: patch => AllowsBackgroundBoundary(target, patch)
                        || LoadingInvocationObservation.AllowsHook(target, patch)) || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("Another mod changes the atlas startup callback lifecycle.");
                checks.Add(guard);
            }
            guards = checks.ToArray();
            harmony.Patch(AsyncUpdate, prefix: Hook(nameof(AdvancePending)), transpiler: Hook(nameof(RewriteCompletion)));
            harmony.Patch(Callbacks, prefix: Hook(nameof(BeforeCallbacks)));
            Installed = true;
            Status = "Owned atlas batches yield through the existing startup event; native callbacks wait for completion.";
            AtlasRuntime.Record("batching-installed");
            return true;
        }
        catch (Exception error)
        {
            harmony.UnpatchAll(Owner);
            Installed = false;
            Status = "Atlas batching unavailable: " + error.Message;
            AtlasRuntime.Record("batching-refused", "\"reason\":" + JsonLineLog.Quote(error.ToString()));
            Log.Message("[Wake-Up] " + Status);
            return false;
        }
    }

    // Called only after the independent display has won drawing ownership.
    // This selection does not enable an atlas cache or a different texture path.
    internal static bool EnableDisplayScheduling()
    {
        displayScheduling = true;
        if (!Install()) { displayScheduling = false; return false; }
        Status = "Loading callbacks, supported static constructors and native atlases yield between completed units.";
        return true;
    }

    // These exact Wake-Up hooks observe nesting and release a session only when
    // no native loading event remains. They still run around both callback calls.
    private static bool AllowsBackgroundBoundary(MethodBase target, Patch patch) => target == Callbacks
        && patch.owner == BackgroundLoadingRuntime.Owner
        && patch.PatchMethod.Module == typeof(AtlasBatchScheduling).Module
        && (patch.PatchMethod == AccessTools.Method(typeof(BackgroundLoadingRuntime), "EnterBoundary")
            || patch.PatchMethod == AccessTools.Method(typeof(BackgroundLoadingRuntime), "LeaveBoundary"));

    private static HarmonyMethod Hook(string name) => new(AccessTools.Method(typeof(AtlasBatchScheduling), name));
    internal static bool AllowsOwnHook(MethodBase target, Patch patch)
    {
        if (patch.owner != Owner || patch.PatchMethod.Module != typeof(AtlasBatchScheduling).Module) return false;
        MethodInfo method = patch.PatchMethod;
        bool prefix = target == AsyncUpdate && method == AccessTools.Method(typeof(AtlasBatchScheduling), nameof(AdvancePending))
            || target == Callbacks && method == AccessTools.Method(typeof(AtlasBatchScheduling), nameof(BeforeCallbacks));
        bool transpiler = target == AsyncUpdate && method == AccessTools.Method(typeof(AtlasBatchScheduling), nameof(RewriteCompletion));
        Patches? record = Harmony.GetPatchInfo(target);
        if (record == null || !prefix && !transpiler) return false;
        var expected = prefix ? record.Prefixes : record.Transpilers;
        return expected.Count(p => p.owner == Owner && p.PatchMethod == method) == 1
            && record.Prefixes.Concat(record.Postfixes).Concat(record.Transpilers).Concat(record.Finalizers)
                .Concat(record.InnerPrefixes).Concat(record.InnerPostfixes).Count(p => p.PatchMethod == method) == 1;
    }
    private static bool Allowed()
    {
        if (!Installed || !UnityData.IsInMainThread || !(displayScheduling && LoadingObservationRuntime.DisplaySelected || AtlasRuntime.CanBatch)) return false;
        foreach (var guard in guards)
        {
            if (guard.AllowsOriginalContract()) continue;
            AtlasRuntime.ReportOverlap(guard.Target, "Adaptive atlas batching");
            return false;
        }
        return true;
    }

    internal static IEnumerable<CodeInstruction> RewriteCompletion(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        int index = codes.FindIndex(code => code.Calls(Callbacks));
        if (index < 0 || codes.Count(code => code.Calls(Callbacks)) != 1 || codes[index].blocks.Count != 0)
            throw new InvalidOperationException("Native asynchronous callback completion changed.");
        // This call is outside an exception region in the admitted native body.
        // Returning leaves currentEvent, its dead worker and scene operation
        // intact; no completion callback or scene-readiness state is released.
        Label finished = generator.DefineLabel();
        codes[index].operand = AccessTools.Method(typeof(AtlasBatchScheduling), nameof(FinishCallbacksOrPause));
        codes[index + 1].labels.Add(finished);
        codes.InsertRange(index + 1, new[] { new CodeInstruction(OpCodes.Brtrue, finished), new CodeInstruction(OpCodes.Ret) });
        return codes;
    }

    private static bool FinishCallbacksOrPause()
    {
        armed = true;
        try { InvokeCallbacks(); }
        finally { armed = false; }
        return !Active;
    }

    private static bool BeforeCallbacks()
    {
        if (!armed || Active || !Allowed() || (bool)ExecutingCallbacks.GetValue(null)) return true;
        var callbacks = (List<Action>)CallbackList.GetValue(null);
        if (displayScheduling && LoadingObservationRuntime.DisplaySelected && callbacks.Count > 0 && CurrentEvent.GetValue(null) != null)
        {
            displayCompletedCallbacks = 0;
            ExecutingCallbacks.SetValue(null, true);
            DeepProfiler.Start("ExecuteToExecuteWhenFinished()");
            pending = new LoadingCallbackSequence(callbacks, DisplayCallback, ReportCallbackError,
                () => { DeepProfiler.End(); ExecutingCallbacks.SetValue(null, false); });
            displayPending = true;
            owningEvent = CurrentEvent.GetValue(null);
            lastFrame = Time.frameCount;
            advances = 0;
            return false;
        }
        if (callbacks.Count(action => action.Method == AtlasCallback) != 1 || CurrentEvent.GetValue(null) == null) return true;
        ExecutingCallbacks.SetValue(null, true);
        // Native callbacks can append more callbacks; keep the actual live list
        // and native order rather than snapshotting or dropping additions.
        int consumed = 0;
        try
        {
            while (consumed < callbacks.Count)
            {
                Action action = callbacks[consumed++];
                DeepProfiler.Start(action.Method.DeclaringType + " -> " + action.Method);
                try
                {
                    if (action.Method != AtlasCallback) LoadingInvocationObservation.ExecuteAction(action);
                    else
                    {
                        NativeConstructorPrefix();
                        // Static constructors may install late hooks. Recheck at
                        // the actual handoff, and use the native bake if changed.
                        if (!Allowed())
                        {
                            GlobalTextureAtlasManager.BakeStaticAtlases();
                            NativeCleanupSuffix();
                            continue;
                        }
                        pending = AtlasRuntime.BakeBatches();
                        if (pending == null) throw new InvalidOperationException("Atlas batching returned no work iterator.");
                        owningEvent = CurrentEvent.GetValue(null);
                        lastFrame = Time.frameCount;
                        advances = 0;
                        AtlasRuntime.Record("batch-start", "\"frame\":" + lastFrame);
                    }
                }
                catch (Exception error) { ReportCallbackError(error); }
                finally { DeepProfiler.End(); }
                if (Active) break;
            }
        }
        finally
        {
            callbacks.RemoveRange(0, consumed);
            // While pending, ExecuteWhenFinished must keep appending, and an
            // explicit recursive ForceExecute retains native 'Already executing'.
            if (!Active) ExecutingCallbacks.SetValue(null, false);
        }
        return false;
    }

    private static bool AdvancePending()
    {
        if (!Active) return true;
        if (!UnityData.IsInMainThread) return false;
        if (!ReferenceEquals(owningEvent, CurrentEvent.GetValue(null)))
        {
            Cancel("native-event-replaced");
            return true;
        }
        if (displayPending) return AdvanceDisplayPending();
        if (guards.Any(guard => !guard.AllowsOriginalContract()))
        {
            DrainAfterLifecycleChange();
            // The already-started atlas callback is complete (or failed with
            // native catch/continue semantics). Let this very update execute
            // the remaining callbacks through the now-current native hooks,
            // then the original event callback and completion state changes.
            return true;
        }
        // The game can call an update recursively; it must not consume several
        // nominal yields inside a single real frame as its stock iterator does.
        int frame = Time.frameCount;
        if (frame == lastFrame) return false;
        lastFrame = frame;
        try
        {
            bool more;
            DeepProfiler.Start("Atlas baking.");
            try { more = pending!.MoveNext(); }
            finally { DeepProfiler.End(); }
            advances++;
            AtlasRuntime.Record("batch-progress", "\"frame\":" + frame + ",\"advance\":" + advances);
            if (more) return false;
            DisposePending();
            NativeCleanupSuffix();
            AtlasRuntime.Record("batch-scheduler-complete", "\"frame\":" + frame + ",\"advances\":" + advances);
        }
        catch (Exception error)
        {
            // Native ExecuteToExecuteWhenFinished logs a failed callback, skips
            // its remaining suffix, and continues with later queued callbacks.
            ReportCallbackError(error);
            try { DisposePending(); } catch (Exception disposeError) { ReportCallbackError(disposeError); }
            AtlasRuntime.Record("batch-failed", "\"exception\":" + JsonLineLog.Quote(error.GetType().FullName));
        }
        finally
        {
            if (!Active) ExecutingCallbacks.SetValue(null, false);
        }
        // On the next update the original completion path invokes the remaining
        // native callbacks, then its completion callback, then clears the event.
        return false;
    }

    private static IEnumerator DisplayCallback(Action action)
    {
        DeepProfiler.Start(action.Method.DeclaringType!.ToString() + " -> " + action.Method.ToString());
        var token = LoadingInvocationObservation.BeginAction(action);
        Exception? failure = null;
        bool completed = false;
        try
        {
            // Entry admission is repeated because earlier constructors can install hooks.
            bool nativeClosure = action.Method == AtlasCallback && guards.All(g => g.AllowsOriginalContract());
            if (nativeClosure)
            {
                var units = NativeLoadingUnits.StartupCallback();
                try
                {
                    while (true)
                    {
                        bool more;
                        try { more = units.MoveNext(); }
                        catch (Exception error) { failure = error; throw; }
                        if (!more) break;
                        yield return null;
                    }
                }
                finally { (units as IDisposable)?.Dispose(); }
            }
            else
            {
                try { action(); }
                catch (Exception error) { failure = error; throw; }
            }
            completed = true;
        }
        finally
        {
            DeepProfiler.End();
            LoadingObservationRuntime.End(token, failure ?? (completed ? null
                : new OperationCanceledException("The loading event ended before its callback completed.")));
            if (completed || failure != null)
                LoadingObservationRuntime.Progress("Deferred callbacks", "Callbacks returned or reported their native error",
                    ++displayCompletedCallbacks, ((List<Action>)CallbackList.GetValue(null)).Count);
        }
    }

    private static bool AdvanceDisplayPending()
    {
        int frame = Time.frameCount;
        if (frame == lastFrame) return false;
        lastFrame = frame;
        try
        {
            // If another owner changes the completion lifecycle, finish the current
            // invocation in place. Do not replay callbacks or replace the event.
            bool drain = !LoadingObservationRuntime.DisplaySelected || guards.Any(g => !g.AllowsOriginalContract());
            if (drain)
            {
                Status = "A loading hook changed; remaining callbacks finish without additional frame yields.";
                AtlasRuntime.RequestNativeDrain();
            }
            bool more;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            do { more = pending!.MoveNext(); advances++; }
            while (more && (drain || (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                * 1000.0 / System.Diagnostics.Stopwatch.Frequency < 8.0));
            if (more) return false;
            DisposePending();
        }
        catch (Exception error)
        {
            ReportCallbackError(error);
            try { DisposePending(); } catch (Exception disposeError) { ReportCallbackError(disposeError); }
        }
        return false;
    }

    private static void DrainAfterLifecycleChange()
    {
        Status = "Atlas batching stopped because a loading hook changed; remaining work uses native baking.";
        AtlasRuntime.Record("batch-native-drain", "\"reason\":\"late-loading-hook\",\"frame\":" + Time.frameCount);
        try
        {
            // The iterator owns rollback of its current private group. Already
            // published groups are retained, and original borrowed inputs remain
            // alive for native fallback. No arbitrary hook is called on a worker.
            AtlasRuntime.RequestNativeDrain();
            DeepProfiler.Start("Atlas baking.");
            try
            {
                while (pending!.MoveNext()) advances++;
                advances++;
            }
            finally { DeepProfiler.End(); }
            DisposePending();
            NativeCleanupSuffix();
            AtlasRuntime.Record("batch-native-drain-complete", "\"frame\":" + Time.frameCount + ",\"advances\":" + advances);
        }
        catch (Exception error)
        {
            ReportCallbackError(error);
            try { DisposePending(); } catch (Exception disposeError) { ReportCallbackError(disposeError); }
            AtlasRuntime.Record("batch-native-drain-failed", "\"exception\":" + JsonLineLog.Quote(error.GetType().FullName));
        }
        finally { ExecutingCallbacks.SetValue(null, false); }
    }

    private static void NativeConstructorPrefix()
    {
        DeepProfiler.Start("Static constructor calls");
        try
        {
            StaticConstructorOnStartupUtility.CallAll();
            if (Prefs.DevMode) StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes();
        }
        finally { DeepProfiler.End(); }
        FloatMenuMakerMap.Init();
    }

    private static void NativeCleanupSuffix()
    {
        DeepProfiler.Start("Garbage Collection");
        try
        {
            AbstractFilesystem.ClearAllCache();
            GC.Collect(int.MaxValue, GCCollectionMode.Forced);
            Resources.UnloadUnusedAssets();
        }
        finally { DeepProfiler.End(); }
    }

    private static void ReportCallbackError(Exception error) =>
        Log.Error("Could not execute post-long-event action. Exception: " + error);

    private static void DisposePending()
    {
        IEnumerator? iterator = pending;
        pending = null;
        owningEvent = null;
        displayPending = false;
        (iterator as IDisposable)?.Dispose();
    }

    // Only teardown/event replacement cancels current work. ClearQueuedEvents
    // intentionally does not call this: native cancellation leaves current work.
    internal static void Cancel(string reason)
    {
        if (!Active) return;
        try { DisposePending(); }
        finally
        {
            ExecutingCallbacks.SetValue(null, false);
            AtlasRuntime.Record("batch-cancel", "\"reason\":" + JsonLineLog.Quote(reason));
        }
    }
}
