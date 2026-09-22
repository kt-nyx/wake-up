// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WakeUp;

internal static partial class BackgroundLoadingRuntime
{
    internal const string Owner = "wakeup.background-loading";
    internal const string WorldOwner = "wakeup.background-world-loading";
    internal const string ColonyOwner = "wakeup.background-colony-loading";
    internal static readonly MethodInfo NewColony = AccessTools.Method(typeof(PageUtility), "InitGameStart");
    internal static readonly MethodInfo WorldNext = AccessTools.Method(typeof(Page_CreateWorldParams), "CanDoNext");
    internal static readonly MethodInfo WorldQueue = AccessTools.Method(typeof(LongEventHandler), "QueueLongEvent",
        new[] { typeof(Action), typeof(string), typeof(bool), typeof(Action<Exception>), typeof(bool), typeof(bool), typeof(Action) });
    internal static readonly MethodInfo Load = AccessTools.Method(typeof(GameDataSaveLoader), "LoadGame", new[] { typeof(string) });
    internal static readonly MethodInfo Update = AccessTools.Method(typeof(LongEventHandler), "LongEventsUpdate");
    internal static readonly MethodInfo Callbacks = AccessTools.Method(typeof(LongEventHandler), "ExecuteToExecuteWhenFinished");
    internal static readonly MethodInfo PlayStart = AccessTools.Method(typeof(Root_Play), "Start");
    internal static readonly MethodInfo PlayUpdate = AccessTools.Method(typeof(Root_Play), "Update");
    internal static readonly MethodInfo Apply = AccessTools.Method(typeof(PrefsData), "Apply");
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static bool installed;
    private static bool releaseRequested;
    private static PublishedPatchGuard[] worldGuards = Array.Empty<PublishedPatchGuard>();
    private static bool worldInstalled;
    private static PublishedPatchGuard[] colonyGuards = Array.Empty<PublishedPatchGuard>();
    private static bool colonyInstalled;
    private static bool nativeStartupPending;
    private static readonly FieldInfo NativePreferencesApplied = AccessTools.Field(typeof(Root), "prefsApplied");
    // Internal engine boundary is replaced only by the offline native-hook tests.
    internal static Func<bool> MainThread = () => UnityData.IsInMainThread;
    internal static Func<bool> Loaded = () => PlayDataLoader.Loaded;
    internal static Func<bool> Focused = BackgroundLoadingFocus.IsFocused;
    internal static Action<bool> WriteEngineBackground = value => Application.runInBackground = value;
    internal static BackgroundLoadingSession Session = new(() => Prefs.RunInBackground,
        ApplyEngineBackground, () => Time.frameCount);
    internal static void ApplyEngineBackground(bool value) => WriteEngineBackground(nativeStartupPending || value);
    internal static string Status { get; private set; } = "Background save loading was off for this launch.";
    internal static string WorldStatus { get; private set; } = "Background world generation was off for this launch.";
    internal static string ColonyStatus { get; private set; } = "Background new-colony loading was off for this launch.";

    internal static bool Selected(string[] args) => StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
        && args.Count(a => a.StartsWith("--wake-up-background-loading=", StringComparison.Ordinal)) == 1
        && args.Contains("--wake-up-background-loading=on");

    internal static void TryInitialize(string[] args)
    {
        if (!Selected(args)) return;
        var harmony = new Harmony(Owner);
        try
        {
            if (!RuntimeIdentity.ValidateBinaryIdentity(out _)) throw new InvalidOperationException("Required patching functions are unavailable.");
            InstallHooks(harmony);
            Status = "Supported save and direct Play loads can continue while unfocused; your current background preference resumes afterward. Native initial loading remains native.";
        }
        catch (Exception e)
        {
            installed = false;
            try { harmony.UnpatchAll(Owner); } catch { }
            try { new Harmony(WorldOwner).UnpatchAll(WorldOwner); } catch { }
            try { new Harmony(ColonyOwner).UnpatchAll(ColonyOwner); } catch { }
            try { new Harmony(MapOwner).UnpatchAll(MapOwner); } catch { }
            worldInstalled = false;
            colonyInstalled = false;
            WorldStatus = "Background world generation is unavailable because the shared loading lifecycle is unavailable.";
            ColonyStatus = "Background new-colony loading is unavailable because the shared loading lifecycle is unavailable.";
            MapStatus = "Background map loading is unavailable because the shared loading lifecycle is unavailable; native loading remains.";
            Status = "Background save loading is unavailable: " + e.Message;
            Log.Message("[Wake-Up] " + Status);
        }
    }

    internal static void InstallHooks(Harmony harmony, bool includeEngineHooks = true)
    {
        BackgroundLoadingContract.Validate();
        if (NativePreferencesApplied == null || NativePreferencesApplied.FieldType != typeof(bool) || !NativePreferencesApplied.IsStatic)
            throw new InvalidOperationException("Native initial preference boundary changed.");
        var checks = new List<PublishedPatchGuard>();
        foreach (MethodInfo target in BackgroundLoadingContract.Targets)
        {
            if (!PublishedPatchGuard.TryCreate(target, Owner, out var guard, allPatchKinds: true,
                allowedForeignPatch: patch => target == Update && patch.owner == LoadingDisplayRuntime.Owner
                    && patch.PatchMethod == AccessTools.Method(typeof(LoadingDisplayRuntime), nameof(LoadingDisplayRuntime.AfterUpdate))
                    || AtlasBatchScheduling.AllowsOwnHook(target, patch) || LoadingObservationRuntime.AllowsHook(target, patch))
                || !guard!.AllowsOriginalContract())
                throw new InvalidOperationException("Another mod changes the save-loading lifecycle.");
            checks.Add(guard);
        }
        guards = checks.ToArray();
        harmony.Patch(Load, prefix: Hook(nameof(BeforeLoad)), finalizer: Hook(nameof(AfterLoad)));
        harmony.Patch(Update, prefix: Hook(nameof(EnterBoundary)), finalizer: Hook(nameof(LeaveBoundary)));
        harmony.Patch(Callbacks, prefix: Hook(nameof(EnterBoundary)), finalizer: Hook(nameof(LeaveBoundary)));
        harmony.Patch(PlayStart, prefix: Hook(nameof(BeforePlayStart)), finalizer: Hook(nameof(AfterPlayStart)));
        harmony.Patch(AccessTools.Method(typeof(LongEventHandler), "ClearQueuedEvents"), postfix: Hook(nameof(AfterClear)));
        harmony.Patch(AccessTools.Method(typeof(GenScene), "GoToMainMenu"), prefix: Hook(nameof(CancelSceneWait)));
        // The offline host lacks Unity's internal calls (ECall). Tests check
        // these rewrites and execute their boundaries with engine substitutes;
        // production always installs both complete native method hooks.
        if (includeEngineHooks)
        {
            harmony.Patch(Apply, transpiler: Hook(nameof(RewritePreference)));
            harmony.Patch(PlayUpdate, transpiler: Hook(nameof(GuardPlayUpdate)));
        }
        releaseRequested = false;
        installed = true;
        // Installation occurs during native startup, after Root.Start has
        // granted permission. Observe its drain without repeating that write.
        nativeStartupPending = !Loaded();
        TryInstallWorldHooks();
        TryInstallColonyHooks();
        TryInstallMapHooks();
    }

    // Page entry is independently admitted; native direct Play entry shares
    // the same scene/queue lifetime below.
    internal static void TryInstallColonyHooks()
    {
        var harmony = new Harmony(ColonyOwner);
        try
        {
            BackgroundLoadingColonyContract.Validate();
            var checks = new List<PublishedPatchGuard>();
            foreach (MethodInfo target in BackgroundLoadingColonyContract.Targets)
            {
                if (!PublishedPatchGuard.TryCreate(target, ColonyOwner, out var guard, allPatchKinds: true,
                    allowedForeignPatch: patch => AllowsOwnEntryPostfix(target, patch, Harmony.GetPatchInfo(target)))
                    || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("Another mod changes the new-colony loading lifecycle.");
                checks.Add(guard);
            }
            colonyGuards = checks.ToArray();
            harmony.Patch(NewColony, prefix: Hook(nameof(BeforeNewColony)), finalizer: Hook(nameof(AfterLoad)));
            colonyInstalled = true;
            ColonyStatus = "Supported new colonies started from the entry pages can continue while unfocused; native pause-on-load is preserved.";
        }
        catch (Exception e)
        {
            colonyInstalled = false;
            try { harmony.UnpatchAll(ColonyOwner); } catch { }
            ColonyStatus = "Background new-colony loading is unavailable: " + e.Message;
        }
    }

    // World-only changes must not disable the independently verified save path.
    internal static void TryInstallWorldHooks()
    {
        var harmony = new Harmony(WorldOwner);
        try
        {
            WorldBackgroundLoadingContract.Validate();
            var checks = new List<PublishedPatchGuard>();
            foreach (MethodInfo target in WorldBackgroundLoadingContract.Targets)
            {
                if (!PublishedPatchGuard.TryCreate(target, WorldOwner, out var guard, allPatchKinds: true,
                    allowedForeignPatch: patch => AllowsOwnEntryPostfix(target, patch, Harmony.GetPatchInfo(target)))
                    || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("Another mod changes the world-generation page lifecycle.");
                checks.Add(guard);
            }
            worldGuards = checks.ToArray();
            harmony.Patch(WorldNext, prefix: Hook(nameof(EnterBoundary)),
                transpiler: Hook(nameof(RewriteWorldQueue)), finalizer: Hook(nameof(LeaveBoundary)));
            worldInstalled = true;
            WorldStatus = "Supported world generation can also continue while unfocused.";
        }
        catch (Exception e)
        {
            worldInstalled = false;
            try { harmony.UnpatchAll(WorldOwner); } catch { }
            WorldStatus = "Background world generation is unavailable: " + e.Message;
        }
    }
    private static bool AllowsOwnEntryPostfix(MethodBase target, Patch patch, Patches? record)
    {
        if (target != AccessTools.Method(typeof(Root_Entry), "Update") || record == null) return false;
        MethodInfo method = patch.PatchMethod;
        if (method.Module != typeof(BackgroundLoadingRuntime).Module) return false;
        bool known = (patch.owner == "wakeup.type-lookup" && method == AccessTools.Method(typeof(TypeLookupRuntime), "MenuUpdate"))
            || (patch.owner == PngRuntime.Owner && method == AccessTools.Method(typeof(PngRuntime), "Menu"))
            || (patch.owner == LoadingDisplayRuntime.Owner && method == AccessTools.Method(typeof(LoadingDisplayRuntime), "Menu"))
            || (patch.owner == StreamingXmlRuntime.Owner && method == AccessTools.Method(typeof(StreamingXmlRuntime), "MenuUpdate"))
            || (patch.owner == LoadingTimingRuntime.Owner && method == AccessTools.Method(typeof(LoadingTimingRuntime), "Menu"))
            || (patch.owner == DeferredAudioRuntime.Owner && method == AccessTools.Method(typeof(DeferredAudioRuntime), "Idle"));
        if (!known) return false;
        // These exact in-package postfixes retire startup observations or complete
        // bounded audio work. They cannot suppress Root.Update, queue long events or change the background
        // preference. No unrelated owner or alternate hook placement is admitted.
        return record.Postfixes.Count(p => p.owner == patch.owner && p.PatchMethod == method) == 1
            && !record.Prefixes.Concat(record.Transpilers).Concat(record.Finalizers)
                .Concat(record.InnerPrefixes).Concat(record.InnerPostfixes).Any(p => p.PatchMethod == method);
    }
    private static HarmonyMethod Hook(string name) => new(typeof(BackgroundLoadingRuntime), name);

    internal static bool CheckOwnership()
    {
        if (!installed) return false;
        foreach (var guard in guards)
            if (!guard.AllowsOriginalContract())
            {
                installed = false;
                releaseRequested = true;
                Status = "Background save loading stopped because another mod changed the loading lifecycle.";
                worldInstalled = false;
                WorldStatus = "Background world generation stopped because another mod changed the shared loading lifecycle.";
                colonyInstalled = false;
                ColonyStatus = "Background new-colony loading stopped because another mod changed the shared loading lifecycle.";
                MapStatus = "Background map loading stopped because another mod changed the shared loading lifecycle; native loading remains.";
                ServiceRelease();
                return false;
            }
        return true;
    }
    internal static bool CheckWorldOwnership()
    {
        if (!worldInstalled) return false;
        if (worldGuards.All(guard => guard.AllowsOriginalContract())) return true;
        worldInstalled = false;
        WorldStatus = "Background world generation stopped because another mod changed its page lifecycle.";
        if (MainThread()) Session.ReleaseOwner(WorldOwner);
        return false;
    }
    private static void CheckSessionOwnership()
    {
        CheckOwnership();
        if (Session.HasOwner(WorldOwner)) CheckWorldOwnership();
        if (Session.HasOwner(ColonyOwner)) CheckColonyOwnership();
        if (Session.HasOwner(MapOwner)) CheckMapOwnership();
    }
    internal static bool CheckColonyOwnership()
    {
        if (!colonyInstalled) return false;
        if (colonyGuards.All(guard => guard.AllowsOriginalContract())) return true;
        colonyInstalled = false;
        ColonyStatus = "Background new-colony loading stopped because another mod changed its entry or initialization lifecycle.";
        if (MainThread()) Session.ReleaseOwner(ColonyOwner);
        return false;
    }
    private static void ServiceRelease()
    {
        if (releaseRequested && MainThread())
        {
            releaseRequested = false;
            Session.Release();
            Session.ClearCompletionGuard();
        }
    }
    private static void BeforeLoad(out BackgroundLoadingSession.Lease? __state)
    {
        __state = null;
        if (!MainThread() || !Loaded() || !CheckOwnership()) return;
        // Do not adopt an unrelated queue. A nested save request belongs to the
        // already owned chain; its caller still controls native event ordering.
        if (!Session.Active && LongEventHandler.AnyEventNowOrWaiting) return;
        Session.Enter();
        __state = Session.Begin(owner: Owner); // Before the verified native QueueLongEvent call.
    }
    private static void BeforeNewColony(out BackgroundLoadingSession.Lease? __state)
    {
        __state = null;
        if (!MainThread() || !Loaded() || !CheckOwnership() || !CheckColonyOwnership()) return;
        if (!Session.Active && LongEventHandler.AnyEventNowOrWaiting) return;
        Session.Enter();
        // The page queues preparation, then a Play scene. Shared Root_Play.Start
        // arrival keeps ownership across InitNewGame, native pause callbacks and
        // the fade/tail queue. No preference or simulation speed is persisted.
        __state = Session.Begin(owner: ColonyOwner);
    }
    private static void AfterLoad(BackgroundLoadingSession.Lease? __state, Exception? __exception)
    {
        if (__state == null) return;
        Session.Leave();
        if (Session.Active) CheckSessionOwnership();
        if (__exception != null) Session.Release(__state);
    }
    private static void BeforePlayStart(out bool __state)
    {
        EnterBoundary(out __state);
        if (!__state || !Loaded() || !CheckOwnership() || (nativeStartupPending && !Session.Active)) return;
        // Every admitted Root_Play.Start branch queues native save/new-game
        // loading and its fade. It can be reached without the ordinary pages.
        if (!Session.Active && CheckColonyOwnership()) Session.Begin(waitForPlay: false, owner: ColonyOwner);
    }
    private static void EnterBoundary(out bool __state)
    {
        __state = false;
        if (!MainThread()) return;
        ServiceRelease();
        if (Session.Active) CheckSessionOwnership();
        // Track callbacks even if a callback itself initiates the first load.
        Session.Enter();
        __state = true;
    }
    private static void LeaveBoundary(bool __state, Exception? __exception)
    {
        if (!__state) return;
        Session.Leave();
        if (Session.Active) CheckSessionOwnership();
        if (__exception != null) Session.Release();
        else Session.FinishIfIdle(LongEventHandler.AnyEventNowOrWaiting);
    }
    private static void AfterPlayStart(bool __state, Exception? __exception)
    {
        if (__state) Session.ArrivedOrCanceled();
        LeaveBoundary(__state, __exception);
    }
    private static void CancelSceneWait()
    {
        Session.ArrivedOrCanceled();
        Session.CancelCompletionGuard();
    }
    private static void AfterClear()
    {
        // ClearQueuedEvents does not cancel the current event. Its callbacks
        // can still enqueue recovery/follow-up work before the outer boundary.
        if (!MainThread()) return;
        CancelQueuedRendering();
        if (!Session.Active) return;
        if (!LongEventHandler.AnyEventNowOrWaiting) Session.ArrivedOrCanceled();
        Session.FinishIfIdle(LongEventHandler.AnyEventNowOrWaiting);
    }
    internal static void SetBackground(bool intended)
    {
        // Native Root.Start owns initial background permission. Its first
        // preference application ends that permission. Root.Update sets its
        // flag immediately before Apply; an empty callback queue is not enough.
        // Earlier preference changes and nested releases preserve native ownership.
        if (nativeStartupPending && MainThread() && (bool)NativePreferencesApplied.GetValue(null))
        {
            nativeStartupPending = false;
            if (CheckOwnership()) Session.MarkCompletion();
        }
        if (Session.Active) CheckSessionOwnership();
        ServiceRelease();
        Session.ApplyPreference(intended);
    }
    internal static bool ShouldBlockPlay()
    {
        if (Session.Active) CheckSessionOwnership();
        else if (Session.NeedsPlayGuard && !CheckOwnership())
        {
            Session.ClearCompletionGuard();
            return false;
        }
        ServiceRelease();
        return Session.NeedsPlayGuard && Session.BlockPlay(Focused());
    }

    internal static IEnumerable<CodeInstruction> RewritePreference(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var setter = AccessTools.PropertySetter(typeof(Application), nameof(Application.runInBackground));
        if (codes.Count(c => c.Calls(setter)) != 1) throw new InvalidOperationException("Background preference write changed.");
        foreach (var code in codes)
        {
            if (code.Calls(setter)) code.operand = AccessTools.Method(typeof(BackgroundLoadingRuntime), nameof(SetBackground));
            yield return code;
        }
    }
    internal static IEnumerable<CodeInstruction> RewriteWorldQueue(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        if (codes.Count(code => code.Calls(WorldQueue)) != 1)
            throw new InvalidOperationException("World-generation queue boundary changed.");
        foreach (var code in codes)
        {
            if (code.Calls(WorldQueue)) code.operand = AccessTools.Method(typeof(BackgroundLoadingRuntime), nameof(QueueWorld));
            yield return code;
        }
    }
    private static void QueueWorld(Action action, string text, bool asynchronous, Action<Exception> exceptionHandler,
        bool showExtraUIInfo, bool forceHideUI, Action callback)
    {
        // This wrapper replaces only CanDoNext's exact admitted queue call. The
        // original action, callback, error handling and native queue are intact.
        if (MainThread() && Loaded() && CheckOwnership() && CheckWorldOwnership()
            && (Session.Active || !LongEventHandler.AnyEventNowOrWaiting))
        {
            Session.Begin(waitForPlay: false, owner: WorldOwner);
        }
        LongEventHandler.QueueLongEvent(action, text, asynchronous, exceptionHandler, showExtraUIInfo, forceHideUI, callback);
    }
    internal static IEnumerable<CodeInstruction> GuardPlayUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var rootUpdate = AccessTools.Method(typeof(Root), "Update");
        if (codes.Count(c => c.Calls(rootUpdate)) != 1) throw new InvalidOperationException("Play update boundary changed.");
        int index = codes.FindIndex(c => c.Calls(rootUpdate));
        Label resume = generator.DefineLabel();
        codes[index + 1].labels.Add(resume);
        codes.InsertRange(index + 1, new[] {
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BackgroundLoadingRuntime), nameof(ShouldBlockPlay))),
            new CodeInstruction(OpCodes.Brfalse, resume), new CodeInstruction(OpCodes.Ret) });
        return codes;
    }

    internal static void ResetForTests()
    {
        Session.Release();
        Session.ClearCompletionGuard();
        installed = false;
        releaseRequested = false;
        guards = Array.Empty<PublishedPatchGuard>();
        worldGuards = Array.Empty<PublishedPatchGuard>();
        worldInstalled = false;
        colonyGuards = Array.Empty<PublishedPatchGuard>();
        colonyInstalled = false;
        nativeStartupPending = false;
        ResetMapHooksForTests();
    }
}
