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
using RimWorld.Planet;
using Verse;

namespace WakeUp;

internal static partial class BackgroundLoadingRuntime
{
    internal const string MapOwner = "wakeup.background-map-loading";
    internal static string MapStatus { get; private set; } = "Background map loading was off for this launch.";
    private static bool mapInstalled;
    private static PublishedPatchGuard[] mapGuards = Array.Empty<PublishedPatchGuard>();
    private static readonly MethodInfo EnumeratorQueue = AccessTools.Method(typeof(LongEventHandler), "QueueLongEvent",
        new[] { typeof(IEnumerable), typeof(string), typeof(Action<Exception>), typeof(bool), typeof(bool) });
    private static readonly FieldInfo EventQueue = AccessTools.Field(typeof(LongEventHandler), "eventQueue");
    private static readonly FieldInfo CurrentEvent = AccessTools.Field(typeof(LongEventHandler), "currentEvent");
    private static readonly FieldInfo RendererActive = AccessTools.Field(typeof(WorldRenderer), "asynchronousRegenerationActive");
    private static readonly List<WorldRendering> rendering = new();

    internal static void TryInstallMapHooks()
    {
        var harmony = new Harmony(MapOwner);
        try
        {
            BackgroundLoadingMapContract.Validate();
            var checks = new List<PublishedPatchGuard>();
            foreach (var target in BackgroundLoadingMapContract.Targets)
            {
                if (!PublishedPatchGuard.TryCreate(target, MapOwner, out var guard, allPatchKinds: true,
                    allowedForeignPatch: patch => LifecycleSupplierPolicy.AllowsThemeObserver(target, patch) || LoadingProgressBackgroundPolicy.Allows(target, patch, map: true))
                    || !guard!.AllowsOriginalContract())
                    throw LifecycleSupplierPolicy.BackgroundConflict(target);
                checks.Add(guard);
            }
            mapGuards = checks.ToArray();
            foreach (var caller in BackgroundLoadingMapContract.QueueCallers)
                harmony.Patch(caller, transpiler: Hook(nameof(RewriteMapQueue)));
            harmony.Patch(BackgroundLoadingMapContract.Renderer, transpiler: Hook(nameof(RewriteRendererQueue)));
            harmony.Patch(BackgroundLoadingMapContract.Portal, prefix: Hook(nameof(BeforePortal)), finalizer: Hook(nameof(AfterPortal)));
            mapInstalled = true;
            MapStatus = "Native settlement, camp and encounter map queues and world-map rendering can finish while unfocused. "
                + "Direct synchronous map/pocket generation remains native. Unknown mod-created queues are not covered."; CompatibilityStatus.Available("background-map");
        }
        catch (Exception error)
        {
            mapInstalled = false;
            try { harmony.UnpatchAll(MapOwner); } catch { }
            MapStatus = "Background map loading is unavailable; native loading remains: " + error.Message; LifecycleSupplierPolicy.Refuse("background-map", MapStatus, error);
        }
    }
    internal static bool CheckMapOwnership()
    {
        if (!mapInstalled) return false;
        bool loadingProgress = LoadingProgressBackgroundPolicy.Unchanged(Session.Active, map: true);
        var changed = mapGuards.FirstOrDefault(guard => !guard.AllowsOriginalContract());
        if (changed == null && LifecycleSupplierPolicy.ThemeCallbacksUnchanged(map: true) && loadingProgress) return true;
        mapInstalled = false;
        Exception conflict = !loadingProgress ? LoadingProgressBackgroundPolicy.Conflict()
            : changed != null ? LifecycleSupplierPolicy.BackgroundConflict(changed.Target)
            : LifecycleSupplierPolicy.ThemeBackgroundConflict();
        MapStatus = "Background map loading stopped: " + conflict.Message; LifecycleSupplierPolicy.Refuse("background-map", MapStatus, conflict);
        if (MainThread()) Session.ReleaseOwner(MapOwner);
        return false;
    }
    private static BackgroundLoadingSession.Lease? BeginMap()
    {
        if (!MainThread() || !Loaded() || !CheckOwnership() || !CheckMapOwnership() || !LoadingProgressBackgroundPolicy.CanAcquire()) return null;
        if (!Session.Active && LongEventHandler.AnyEventNowOrWaiting) return null;
        return Session.Begin(waitForPlay: false, owner: MapOwner);
    }
    internal static IEnumerable<CodeInstruction> RewriteMapQueue(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        if (!codes.Any(code => code.Calls(WorldQueue))) throw new InvalidOperationException("Native map queue call changed.");
        foreach (var code in codes)
        {
            if (code.Calls(WorldQueue)) code.operand = AccessTools.Method(typeof(BackgroundLoadingRuntime), nameof(QueueMap));
            yield return code;
        }
    }
    private static void QueueMap(Action action, string text, bool asynchronous, Action<Exception> exceptionHandler,
        bool showExtraUIInfo, bool forceHideUI, Action callback)
    {
        bool knownAction = !(action.Target is TravellingTransporters transport)
            || BackgroundLoadingMapContract.TransportTypes.Contains(
                AccessTools.Field(typeof(TravellingTransporters), "arrivalAction").GetValue(transport)?.GetType());
        var lease = knownAction ? BeginMap() : null;
        try
        {
            LongEventHandler.QueueLongEvent(action, text, asynchronous, exceptionHandler, showExtraUIInfo, forceHideUI, callback);
            if (lease != null && !asynchronous)
            {
                // Select native's nonstandard loading window, the same mode
                // used when a synchronous event follows an asynchronous load.
                // This removes the standard dialog's repaint prerequisite while
                // preserving its original main-thread action and native UI.
                object queued = ((IEnumerable)EventQueue.GetValue(null)).Cast<object>().Last();
                AccessTools.Field(queued.GetType(), "canEverUseStandardWindow").SetValue(queued, false);
            }
        }
        catch { if (lease != null) Session.Release(lease); throw; }
    }
    internal static IEnumerable<CodeInstruction> RewriteRendererQueue(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        if (codes.Count(code => code.Calls(EnumeratorQueue)) != 1) throw new InvalidOperationException("World rendering queue changed.");
        foreach (var code in codes)
        {
            if (code.Calls(EnumeratorQueue))
            {
                var instance = new CodeInstruction(OpCodes.Ldarg_0);
                instance.labels.AddRange(code.labels); code.labels.Clear();
                instance.blocks.AddRange(code.blocks); code.blocks.Clear();
                yield return instance;
                code.operand = AccessTools.Method(typeof(BackgroundLoadingRuntime), nameof(QueueWorldRendering));
            }
            yield return code;
        }
    }
    private static void QueueWorldRendering(IEnumerable action, string text, Action<Exception> exceptionHandler,
        bool showExtraUIInfo, bool forceHideUI, WorldRenderer renderer)
    {
        var lease = BeginMap();
        if (lease == null)
        {
            // A later native/unadmitted generation owns this same flag now.
            foreach (var item in rendering.Where(item => ReferenceEquals(item.Renderer, renderer))) item.MayCleanFlag = false;
            LongEventHandler.QueueLongEvent(action, text, exceptionHandler, showExtraUIInfo, forceHideUI);
            return;
        }
        WorldRendering? owned = null;
        try
        {
            owned = new WorldRendering(action.GetEnumerator(), renderer, lease);
            rendering.Add(owned);
            LongEventHandler.QueueLongEvent(owned, text, exceptionHandler, showExtraUIInfo, forceHideUI);
            owned.QueuedEvent = ((IEnumerable)EventQueue.GetValue(null)).Cast<object>().Last();
        }
        catch { owned?.Dispose(); Session.Release(lease); throw; }
    }
    private sealed class WorldRendering : IEnumerable, IEnumerator, IDisposable
    {
        private readonly IEnumerator native;
        internal readonly WorldRenderer Renderer;
        internal readonly BackgroundLoadingSession.Lease Lease;
        internal object? QueuedEvent;
        internal bool MayCleanFlag = true;
        private bool disposed, completed;
        internal WorldRendering(IEnumerator native, WorldRenderer renderer, BackgroundLoadingSession.Lease lease)
        { this.native = native; Renderer = renderer; Lease = lease; }
        public IEnumerator GetEnumerator() => this;
        public object Current => native.Current;
        public bool MoveNext()
        {
            bool more = native.MoveNext();
            if (!more) completed = true;
            return more;
        }
        public void Reset() => throw new NotSupportedException();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { (native as IDisposable)?.Dispose(); }
            finally
            {
                rendering.Remove(this);
                // Native clears this only on normal enumeration, not cancellation.
                // Retain a later owned operation's flag on the same renderer.
                if (!completed && MayCleanFlag && mapInstalled && mapGuards.All(guard => guard.AllowsOriginalContract())
                    && !rendering.Any(item => ReferenceEquals(item.Renderer, Renderer)))
                    RendererActive.SetValue(Renderer, false);
            }
        }
    }
    private static void CancelQueuedRendering()
    {
        object current = CurrentEvent.GetValue(null);
        foreach (var item in rendering.ToArray())
        {
            if (ReferenceEquals(item.QueuedEvent, current)) continue; // Native clear does not cancel current work.
            try { item.Dispose(); }
            finally { Session.Release(item.Lease); }
        }
    }
    private static void BeforePortal(MapPortal __instance, out MapPortal? __state)
    {
        // No background override is needed for a native synchronous call.
        // Only track the exact native implementation's missing error cleanup.
        __state = CheckOwnership() && CheckMapOwnership()
            // Inherited MethodInfo objects can have different ReflectedType
            // identities on Mono. The actual declaring implementation must be
            // this guarded native base, not an override or hidden mod method.
            && AccessTools.Method(__instance.GetType(), "GeneratePocketMapInt")?.DeclaringType == typeof(MapPortal)
            ? __instance : null;
    }
    private static void AfterPortal(MapPortal? __state, Exception? __exception)
    {
        if (__state != null && __exception != null && ReferenceEquals(PocketMapUtility.currentlyGeneratingPortal, __state))
            PocketMapUtility.currentlyGeneratingPortal = null;
    }
    private static void ResetMapHooksForTests()
    {
        mapInstalled = false;
        mapGuards = Array.Empty<PublishedPatchGuard>();
        rendering.Clear();
    }
}
