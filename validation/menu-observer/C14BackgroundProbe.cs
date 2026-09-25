// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;
using Verse.AI;

// Private functional observer, never shipped. Calls actual native entry APIs;
// observes real Unity focus and engine preference without patching game methods.
// One process per endpoint: false restoration can stop future Unity frames.
[DefaultExecutionOrder(-10000)]
public sealed class C14BackgroundProbe : MonoBehaviour
{
    private const string Selector = "--fixture-c14-background=";
    private const string SaveName = "RloFixtureSmoke";
    internal static string SelectedPhase => Environment.GetCommandLineArgs()
        .FirstOrDefault(a => a.StartsWith(Selector, StringComparison.Ordinal))?.Substring(Selector.Length) ?? "";
    internal static bool Required => SelectedPhase.Length != 0;
    internal static bool AutostartSelected => SelectedPhase.StartsWith("autostart-", StringComparison.Ordinal);
    internal static bool AutostartCompleted { get; private set; }
    internal static bool AutostartReturned { get; private set; }
    private static bool autostartInstalled;
    private static string directory = "";
    private static string requestedPhase = "";
    private string operation = "";
    private bool initialPreference;
    private bool finalPreference;
    private bool pauseOnLoad;
    private bool originalPause;
    private bool captured;
    private int stage;
    private int startFrame;
    private int savedTick;
    private int earlyTick = -1;
    private int earlyFrame = -1;
    private int runningFrames;
    private bool sawFocused;
    private long holdStarted;
    private int holdTick;
    private long started = Stopwatch.GetTimestamp();
    private long lastReceipt;
    private IntPtr window;
    private Page_CreateWorldParams? page;
    private Page_SelectStartingSite? next;
    private Map? sourceMap;
    private Map? generatedMap;
    private MapParent? destination;
    private Pawn? traveller;
    private bool cancelledRenderer;
    private bool deferredFaultRan, deferredTailRan, deferredRetryQueued;
    private bool nativeSynchronous;
    private AncientHatch? nativePortal;
    private bool portalErrorObserved;
    private bool portalErrorOwnerCleared;
    private bool portalRepeatMatched;
    private bool portalReturning, portalWalkObserved, portalJobObserved, portalWaitDecreased;
    private int portalInitialMapCount, portalLegStartTick, portalTransferTick, portalLastToil = -1;
    private int portalFirstWait = -1, portalLastWait = -1;
    private int portalOutboundWaitFirst, portalOutboundWaitLast, portalOutboundTransferTick;
    private IntVec3 portalLegStartPosition;
    private long portalJobStarted;
    private Job? portalJob;
    private string portalTargetId = "";
    private PlanetTile operationTile;
    private const string ForeignLoadOwner = "local.fixture.c14.synthetic-foreign-save-prefix";
    private Harmony? foreignLoadHarmony;
    private MethodInfo? foreignLoadTarget;
    private string refusedSaveStatus = "";
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    internal static void Start(string evidenceDirectory, string phase)
    {
        directory = evidenceDirectory;
        requestedPhase = phase;
        var host = new GameObject("C14BackgroundProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<C14BackgroundProbe>();
    }

    internal static void InstallAutostart(string evidenceDirectory)
    {
        if (!AutostartSelected || autostartInstalled) return;
        autostartInstalled = true;
        directory = evidenceDirectory;
        requestedPhase = SelectedPhase;
        // Mod construction may occur on the loading worker. The native
        // completion callback creates the observer on Unity's main thread;
        // Root_Entry remains solely responsible for selecting/loading the save.
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            var host = new GameObject("C14NativeAutostartObserver");
            DontDestroyOnLoad(host);
            var observer = host.AddComponent<C14BackgroundProbe>();
            try { observer.Configure(); }
            catch (Exception error) { observer.Fail(error); }
        });
    }

    private static string Bool(bool value) => value ? "true" : "false";
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    private int Tick => Current.Game?.tickManager?.TicksGame ?? -1;
    private bool WindowFocused
    {
        get { GetWindowThreadProcessId(GetForegroundWindow(), out uint pid); return pid == Process.GetCurrentProcess().Id; }
    }
    private void Receipt(string point)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundProcess);
        File.AppendAllText(Path.Combine(directory, "c14-background.jsonl"),
            "{\"point\":" + Quote(point) + ",\"phase\":" + Quote(requestedPhase)
            + ",\"utc\":" + Quote(DateTime.UtcNow.ToString("O")) + ",\"stage\":" + stage
            + ",\"frame\":" + Time.frameCount + ",\"focused\":" + Bool(Application.isFocused)
            + ",\"foregroundProcess\":" + foregroundProcess + ",\"fixtureProcess\":" + Process.GetCurrentProcess().Id
            + ",\"windowHandle\":" + window.ToInt64()
            + ",\"minimized\":" + Bool(window != IntPtr.Zero && IsIconic(window))
            + ",\"engineBackground\":" + Bool(Application.runInBackground)
            + ",\"preference\":" + Bool(Prefs.RunInBackground) + ",\"pauseOnLoad\":" + Bool(Prefs.PauseOnLoad)
            + ",\"longEvent\":" + Bool(LongEventHandler.AnyEventNowOrWaiting)
            + ",\"scene\":" + Quote(SceneManager.GetActiveScene().name)
            + ",\"programState\":" + Quote(Current.ProgramState.ToString())
            + ",\"tick\":" + Tick + ",\"speed\":" + Quote(Current.Game?.tickManager?.CurTimeSpeed.ToString() ?? "none")
            + ",\"mapPresent\":" + Bool(Current.Game?.CurrentMap != null)
            + ",\"worldPresent\":" + Bool(Current.Game?.World != null)
            + ",\"generatedMapPresent\":" + Bool(generatedMap != null || destination?.HasMap == true)
            + ",\"mapCount\":" + (Current.Game?.Maps.Count ?? 0)
            + ",\"pageOpen\":" + Bool(page?.IsOpen == true) + ",\"nextOpen\":" + Bool(next?.IsOpen == true) + "}\n");
    }

    private void Update()
    {
        if (stage < 0) return;
        try
        {
            if (stage == 7) { ObservePostLoadHold(); return; }
            if (Stopwatch.GetTimestamp() - started > Stopwatch.Frequency * 540)
                throw new TimeoutException("C14 functional operation exceeded nine minutes.");
            if (stage == 4)
            {
                if (LongEventHandler.AnyEventNowOrWaiting || !GenScene.InEntryScene || Current.Game != null) return;
                Receipt("private-autostart-cleanup-returned-to-menu-not-loading-evidence");
                AutostartReturned = true;
                stage = -1;
                return;
            }
            if (stage == 0)
            {
                if (LongEventHandler.AnyEventNowOrWaiting) return;
                using (var process = Process.GetCurrentProcess()) window = process.MainWindowHandle;
                if (Stopwatch.GetTimestamp() - lastReceipt > Stopwatch.Frequency)
                { lastReceipt = Stopwatch.GetTimestamp(); Receipt("initial-real-focus-observation"); }
                // Initial Unity focus notification can trail startup. Observe
                // several natural frames without changing either focus state.
                if (Stopwatch.GetTimestamp() - started < Stopwatch.Frequency * 3) return;
                Configure();
                return;
            }
            if (stage == 5 || stage == 6)
            {
                sawFocused |= WindowFocused;
                if (Tick - savedTick > 3600 || Stopwatch.GetTimestamp() - portalJobStarted > Stopwatch.Frequency * 180)
                    throw new TimeoutException("Native portal round trip exceeded 3600 game ticks or three minutes.");
                return;
            }
            if (stage != 2) return;
            sawFocused |= WindowFocused;
            earlyTick = Tick;
            earlyFrame = Time.frameCount;
            if (LongEventHandler.AnyEventNowOrWaiting)
            {
                runningFrames++;
                if (Stopwatch.GetTimestamp() - lastReceipt > Stopwatch.Frequency * 2)
                {
                    lastReceipt = Stopwatch.GetTimestamp();
                    Receipt("native-loading");
                }
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private void Configure()
    {
        operation = new[] { "world-render-cancel", "world-render", "refused-save", "save-recovery", "save-hold", "portal-error", "portal-job", "standalone", "encounter", "autostart", "portal", "settle", "pocket", "colony", "world", "camp", "save" }
            .FirstOrDefault(name => requestedPhase.StartsWith(name + "-", StringComparison.Ordinal)) ?? "";
        if (operation.Length == 0) throw new ArgumentException("Unknown C14 operation.");
        string[] pieces = requestedPhase.Substring(operation.Length + 1).Split('-');
        if (pieces.Length < 1 || (pieces[0] != "false" && pieces[0] != "true"))
            throw new ArgumentException("C14 phase requires initial true/false preference.");
        initialPreference = pieces[0] == "true";
        finalPreference = initialPreference;
        pauseOnLoad = !requestedPhase.EndsWith("-unpaused", StringComparison.Ordinal);
        int optionsEnd = pieces.Length - (pauseOnLoad ? 0 : 1);
        if (optionsEnd != 1 && (optionsEnd != 3 || pieces[1] != "to" || (pieces[2] != "true" && pieces[2] != "false")))
            throw new ArgumentException("C14 phase suffix must be -to-true/-to-false and optional -unpaused.");
        if (optionsEnd == 3) finalPreference = pieces[2] == "true";
        if (operation == "portal-job" && requestedPhase != "portal-job-true")
            throw new ArgumentException("Native portal job traversal requires exactly portal-job-true; walking is deliberately enabled gameplay.");
        if (operation == "save-hold" && (requestedPhase != "save-hold-false-unpaused" || WindowFocused))
            throw new InvalidOperationException("Hold unqualified: requires save-hold-false-unpaused and an actually nonforeground fixture before loading; no focus is changed.");
        using (var process = Process.GetCurrentProcess()) window = process.MainWindowHandle;
        Receipt("configured-real-focus-precondition");
        // Unattended native checks can also observe a foreground fixture.
        // Preserve the actual state and qualify unfocused behavior separately;
        // a startup display request is not proof of foreground ownership.
        originalPause = Prefs.PauseOnLoad;
        captured = true;
        bool nativeLogging = Environment.GetCommandLineArgs().Contains("--fixture-native-stack-traces");
        File.WriteAllText(Path.Combine(directory, "c14-stack-trace-policy.txt"), nativeLogging
            ? "ordinary-native-unchanged"
            : "private frozen-GOG formatter workaround: Exception/Error stack formatting suppressed; messages retained; not production scene-entry qualification");
        if (!nativeLogging)
        {
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
        }
        if (operation == "autostart")
        {
            if (Prefs.RunInBackground != initialPreference || Prefs.PauseOnLoad != pauseOnLoad)
                throw new InvalidOperationException("Native autostart preferences must already match the prepared fixture profile; observer will not change startup permission.");
            if (!(bool)AccessTools.Field(typeof(Root), "checkedAutostartSaveFile").GetValue(null))
                throw new InvalidOperationException("Native Root_Entry had not performed its own autostart selection.");
            var xml = new XmlDocument();
            xml.Load(GenFilePaths.FilePathForSavedGame("autostart"));
            savedTick = int.Parse((xml.SelectSingleNode("/savegame/game/tickManager/ticksGame")
                ?? throw new InvalidDataException("Native autostart save has no ticksGame field.")).InnerText, CultureInfo.InvariantCulture);
            stage = 2;
            startFrame = Time.frameCount;
            sawFocused = WindowFocused;
            Receipt("native-autostart-observer-attached-no-entry-call");
            if (!LongEventHandler.AnyEventNowOrWaiting || !Application.runInBackground)
                throw new InvalidOperationException("Native autostart was not observed with loading queued and native startup permission active.");
            if (initialPreference != finalPreference)
            {
                // An actual current preference change during the native queue;
                // this is a separate variant from unmodified initial startup.
                Prefs.RunInBackground = finalPreference;
                Prefs.Apply();
                Receipt("native-autostart-current-preference-changed-during-load");
                if (!Application.runInBackground)
                    throw new InvalidOperationException("Current preference change revoked native autostart loading permission.");
            }
            return;
        }
        Prefs.PauseOnLoad = pauseOnLoad;
        if (operation != "save" && operation != "save-recovery" && operation != "save-hold" && operation != "refused-save" && operation != "world" && operation != "colony")
        {
            // This load only supplies a real native colony/world. Endpoint
            // observations begin after it has completed and setup is paused.
            Prefs.RunInBackground = true;
            Prefs.Apply();
            Prefs.PauseOnLoad = true;
            stage = 3;
            Receipt("map-operation-save-preparation");
            GameDataSaveLoader.LoadGame(SaveName);
            return;
        }
        if (operation == "save" || operation == "save-recovery" || operation == "save-hold" || operation == "refused-save")
        {
            var xml = new XmlDocument();
            xml.Load(GenFilePaths.FilePathForSavedGame(SaveName));
            var ticks = xml.SelectSingleNode("/savegame/game/tickManager/ticksGame")
                ?? throw new InvalidDataException("Copied fixture save has no native ticksGame field.");
            savedTick = int.Parse(ticks.InnerText, CultureInfo.InvariantCulture);
            if (operation == "refused-save") InstallForeignSaveControl();
            BeginOperation(() => GameDataSaveLoader.LoadGame(SaveName));
            if (operation == "save-recovery")
            {
                LongEventHandler.ExecuteWhenFinished(() => { deferredFaultRan = true; throw new InvalidOperationException("Expected private fixture deferred callback failure"); });
                LongEventHandler.ExecuteWhenFinished(() => deferredTailRan = true);
                Receipt("native-deferred-error-and-following-action-queued");
            }
            if (operation == "refused-save") VerifyForeignSaveRefusal();
            return;
        }
        // World preparation uses native page generation, including its queued
        // completion/page/redraw callbacks. Colony setup is separately labeled.
        Game.ClearCaches();
        Current.Game = new Game { InitData = new GameInitData(), Scenario = ScenarioDefOf.Crashlanded.scenario };
        Find.Scenario.PreConfigure();
        Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
        page = new Page_CreateWorldParams();
        next = new Page_SelectStartingSite();
        page.next = next;
        Find.WindowStack.Add(page);
        AccessTools.Field(typeof(Page_CreateWorldParams), "seedString").SetValue(page, "WakeUpC14Background");
        // Native developer coverage keeps a representative functional world small.
        AccessTools.Field(typeof(Page_CreateWorldParams), "planetCoverage").SetValue(page, 0.05f);
        if (operation == "world") BeginOperation(GenerateWorldFromPage);
        else
        {
            Prefs.RunInBackground = true;
            Prefs.Apply();
            stage = 1;
            Receipt("colony-preparation-world-start");
            GenerateWorldFromPage();
        }
    }

    private void GenerateWorldFromPage()
    {
        if ((bool)AccessTools.Method(typeof(Page_CreateWorldParams), "CanDoNext").Invoke(page, null))
            throw new InvalidOperationException("Native world page unexpectedly returned immediate transition.");
    }

    private void BeginOperation(Action nativeEntry, bool synchronous = false)
    {
        if (synchronous && initialPreference != finalPreference)
            throw new InvalidOperationException("A synchronous call has no intervening observer frame for a real preference-change probe.");
        nativeSynchronous = synchronous;
        Prefs.RunInBackground = initialPreference;
        Prefs.Apply();
        stage = 2;
        startFrame = Time.frameCount;
        sawFocused = WindowFocused;
        Receipt("before-native-entry");
        nativeEntry();
        Receipt("native-entry-returned");
        if (!LongEventHandler.AnyEventNowOrWaiting && !synchronous)
            throw new InvalidOperationException("Native operation did not queue loading work.");
        if (LongEventHandler.AnyEventNowOrWaiting && !Application.runInBackground)
            throw new InvalidOperationException("Native queued load did not have temporary background permission.");
        if (initialPreference != finalPreference)
        {
            Prefs.RunInBackground = finalPreference;
            Prefs.Apply();
            Receipt("current-preference-changed-during-load");
            if (!Application.runInBackground)
                throw new InvalidOperationException("Preference change revoked permission while native loading remained queued.");
        }
    }

    private void LateUpdate()
    {
        if (stage < 1 || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            if (stage == 5 || stage == 6) { ObservePortalJob(); return; }
            if (stage == 1)
            {
                RequireWorldCompletion();
                Receipt("colony-preparation-world-completed");
                next!.Close();
                Find.GameInitData.ChooseRandomStartingTile();
                Find.GameInitData.mapSize = 75;
                Find.GameInitData.startedFromEntry = true;
                Find.Scenario.PostIdeoChosen();
                savedTick = Tick;
                BeginOperation(PageUtility.InitGameStart);
                return;
            }
            if (stage == 3)
            {
                if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
                    throw new InvalidOperationException("Copied-save preparation did not reach its colony.");
                sourceMap = Find.CurrentMap;
                if (Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
                    throw new InvalidOperationException("Copied-save preparation did not respect native PauseOnLoad.");
                // Native queue completion precedes its half-second screen fade.
                // Let ordinary input control return before asking to unpause;
                // the native speed setter correctly refuses during that fade.
                if (operation == "portal-job" && (!Current.Game.PlayerHasControl || Find.TickManager.ForcePaused)) return;
                savedTick = Tick;
                Prefs.PauseOnLoad = pauseOnLoad;
                Receipt("map-operation-preparation-completed");
                BeginMapOperation();
                // Synchronous native calls complete in this exact invocation;
                // capture them here even when false prevents another frame.
                if (!nativeSynchronous || LongEventHandler.AnyEventNowOrWaiting) return;
            }
            if (stage != 2) return;
            Receipt("completion-late-update-before-observer-changes");
            sawFocused |= WindowFocused;
            if (Prefs.RunInBackground != finalPreference || Application.runInBackground != finalPreference)
                throw new InvalidOperationException("Completion did not restore the current actual preference.");
            if (Prefs.PauseOnLoad != pauseOnLoad)
                throw new InvalidOperationException("Loading changed native PauseOnLoad preference.");
            if (operation == "save-recovery")
            {
                bool executing = (bool)AccessTools.Field(typeof(LongEventHandler), "executingToExecuteWhenFinished").GetValue(null);
                var pending = (System.Collections.ICollection)AccessTools.Field(typeof(LongEventHandler), "toExecuteWhenFinished").GetValue(null);
                if (!deferredFaultRan || !deferredTailRan || executing || pending.Count != 0)
                    throw new InvalidOperationException("Native deferred callback error did not drain and clear its execution state.");
                if (!deferredRetryQueued)
                {
                    deferredRetryQueued = true;
                    Receipt("native-deferred-error-cleaned-before-real-save-retry");
                    BeginOperation(() => GameDataSaveLoader.LoadGame(SaveName));
                    return;
                }
                Receipt("real-save-retry-completed-after-native-deferred-error");
            }
            if (operation == "world") RequireWorldCompletion();
            else if (sourceMap != null) RequireMapOperationCompletion();
            else
            {
                if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || Find.CurrentMap.Size.x != 75)
                    throw new InvalidOperationException("Native scene/map entry did not complete on the small fixture map.");
                int expectedTick = savedTick + (pauseOnLoad ? 1 : 0);
                if (pauseOnLoad && Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
                    throw new InvalidOperationException("Native PauseOnLoad did not preserve paused completion.");
                if (!pauseOnLoad && Find.TickManager.CurTimeSpeed != TimeSpeed.Normal)
                    throw new InvalidOperationException("Unpaused native completion did not preserve Normal speed.");
                if ((pauseOnLoad || (!finalPreference && !sawFocused)) && Tick != expectedTick)
                    throw new InvalidOperationException("Unexpected completion gameplay tick: expected " + expectedTick + ", observed " + Tick + ".");
            }
            File.WriteAllText(Path.Combine(directory, "c14-background.json"),
                "{\"passed\":true,\"phase\":" + Quote(requestedPhase) + ",\"actualFocusObserved\":true,\"focusedDuringLoad\":" + Bool(sawFocused)
                + ",\"unfocusedLifetimeQualified\":" + Bool(!sawFocused)
                + ",\"unityFocusedAtCompletion\":" + Bool(Application.isFocused) + ",\"windowsForegroundAtCompletion\":" + Bool(WindowFocused)
                + ",\"initialPreference\":" + Bool(initialPreference) + ",\"currentPreference\":" + Bool(finalPreference)
                + ",\"pauseOnLoad\":" + Bool(pauseOnLoad) + ",\"savedOrInitialTick\":" + savedTick
                + ",\"completionTick\":" + Tick + ",\"earlyUpdateTick\":" + earlyTick + ",\"earlyUpdateFrame\":" + earlyFrame
                + ",\"startFrame\":" + startFrame + ",\"completionFrame\":" + Time.frameCount + ",\"loadingFrames\":" + runningFrames
                + ",\"nativeSynchronousCall\":" + Bool(nativeSynchronous) + ",\"nativeWorldCancelledAndRepeated\":" + Bool(cancelledRenderer)
                + ",\"nativeDeferredErrorCleanupAndRetry\":" + Bool(operation == "save-recovery" && deferredFaultRan && deferredTailRan && deferredRetryQueued)
                + ",\"nativeInitialAutostartObserved\":" + Bool(operation == "autostart")
                + ",\"nativePortalObserved\":" + Bool(nativePortal != null) + ",\"malformedPortalDefinitionFaultInjected\":" + Bool(portalErrorObserved)
                + ",\"portalErrorOwnerCleared\":" + Bool(portalErrorOwnerCleared) + ",\"portalRepeatReturnedSameMap\":" + Bool(portalRepeatMatched)
                + ",\"syntheticForeignHookControl\":" + Bool(operation == "refused-save")
                + ",\"foreignHookOwner\":" + Quote(operation == "refused-save" ? ForeignLoadOwner : "") + ",\"refusedSaveStatus\":" + Quote(refusedSaveStatus)
                + ",\"sourceMapPreserved\":" + Bool(sourceMap != null && Find.Maps.Contains(sourceMap))
                + ",\"generatedMapSize\":" + (generatedMap?.Size.x ?? 0) + ",\"mapCount\":" + (Current.Game?.Maps.Count ?? 0)
                + ",\"screenVisibilityQualified\":false,\"postCompletionHoldQualified\":false,\"automaticExit\":true}\n");
            if (operation == "autostart")
            {
                AutostartCompleted = true;
                // Endpoint above is immutable. Native GoToMainMenu first
                // disposes the game, then queues scene cleanup. Only after
                // that disposal may this private observer resume Unity for
                // returning to the independent menu/normal-exit observer.
                stage = 4;
                Receipt("autostart-endpoint-recorded-before-private-cleanup");
                GenScene.GoToMainMenu();
                Receipt("private-autostart-game-disposed-native-menu-return-queued");
                Application.runInBackground = true;
                Receipt("private-autostart-cleanup-engine-resume-not-loading-evidence");
            }
            else if (operation == "save-hold")
            {
                string holdResult = Path.Combine(directory, "c14-background.json");
                File.WriteAllText(holdResult, File.ReadAllText(holdResult).Replace("\"passed\":true", "\"passed\":false"));
                if (sawFocused || WindowFocused)
                    throw new InvalidOperationException("Hold unqualified: loading did not remain outside the Windows foreground.");
                holdTick = Tick;
                holdStarted = Stopwatch.GetTimestamp();
                stage = 7;
                Receipt("post-load-hold-started-no-preference-speed-or-focus-changes");
            }
            else Finish();
        }
        catch (Exception error) { Fail(error); }
    }

    private void RequireWorldCompletion()
    {
        if (Current.ProgramState != ProgramState.Entry || SceneManager.GetActiveScene().name != "Entry"
            || Current.Game?.World == null || page?.IsOpen != false || next?.IsOpen != true)
            throw new InvalidOperationException("Native world/page/redraw completion did not finish in Entry.");
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused || stage != 7) return;
        try { ObservePostLoadHold(); }
        catch (Exception error) { Fail(error); }
    }

    private void ObservePostLoadHold()
    {
        double seconds = (Stopwatch.GetTimestamp() - holdStarted) / (double)Stopwatch.Frequency;
        if (Stopwatch.GetTimestamp() - lastReceipt > Stopwatch.Frequency || Tick != holdTick || WindowFocused)
        { lastReceipt = Stopwatch.GetTimestamp(); Receipt("post-load-hold-observed"); }
        if (Tick != holdTick || Find.TickManager.CurTimeSpeed != TimeSpeed.Normal
            || Prefs.RunInBackground || Application.runInBackground || Prefs.PauseOnLoad
            || LongEventHandler.AnyEventNowOrWaiting || Current.ProgramState != ProgramState.Playing)
            throw new InvalidOperationException("Untouched post-load hold changed: initial tick=" + holdTick + ", current tick=" + Tick
                + ", seconds=" + seconds.ToString("F3", CultureInfo.InvariantCulture) + ".");
        if (seconds < 5)
        {
            if (WindowFocused) throw new InvalidOperationException("Hold unqualified: foreground returned before five seconds.");
            return;
        }
        Type display = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "WakeUp")
            .GetType("WakeUp.LoadingDisplayRuntime", true);
        object state = AccessTools.Field(display, "state").GetValue(null);
        bool displayActive = state != null && (bool)AccessTools.Property(state.GetType(), "Active").GetValue(state, null);
        if (displayActive) throw new InvalidOperationException("Loading display state stayed active after the completed hold.");
        string path = Path.Combine(directory, "c14-background.json");
        string result = File.ReadAllText(path).TrimEnd();
        result = result.Replace("\"postCompletionHoldQualified\":false", "\"postCompletionHoldQualified\":true")
            .Replace("\"passed\":false", "\"passed\":true");
        File.WriteAllText(path, result.Substring(0, result.Length - 1)
            + ",\"holdSeconds\":" + seconds.ToString("F3", CultureInfo.InvariantCulture)
            + ",\"holdTick\":" + holdTick + ",\"holdCompletionTick\":" + Tick
            + ",\"loadingDisplayStateInactive\":true,\"holdReturnedToForeground\":" + Bool(WindowFocused) + "}\n");
        // All observations precede normal shutdown. No timer or worker touches
        // Unity; if Unity stops callbacks, real owner return is needed to exit.
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        Receipt("hold-receipt-complete-before-observer-paused-for-shutdown");
        Finish();
    }

    private bool RendererBusy => (bool)AccessTools.Field(typeof(WorldRenderer), "asynchronousRegenerationActive").GetValue(Find.World.renderer);

    private void BeginMapOperation()
    {
        if (operation == "portal-job") { BeginPortalJob(); return; }
        if (operation == "portal" || operation == "portal-error")
        {
            PrepareNativePortal();
            BeginOperation(GenerateNativePortal, true);
            return;
        }
        if (operation.StartsWith("world-render", StringComparison.Ordinal))
        {
            // This is the actual native world-opening worker. Its own UI reset
            // and queued renderer complete normally; no desktop UI is driven.
            new MainButtonWorker_ToggleWorld().Activate();
            Find.World.renderer.SetAllLayersDirty();
            Action render = () =>
            {
                if (!Find.World.renderer.RegenerateLayersIfDirtyInLongEvent())
                    throw new InvalidOperationException("Native dirty terrain did not queue world regeneration.");
                if (!RendererBusy) throw new InvalidOperationException("Native renderer did not set its in-progress flag.");
            };
            BeginOperation(render);
            if (operation == "world-render-cancel")
            {
                LongEventHandler.ClearQueuedEvents();
                Receipt("native-world-queue-cleared");
                if (RendererBusy || LongEventHandler.AnyEventNowOrWaiting || Application.runInBackground != Prefs.RunInBackground)
                    throw new InvalidOperationException("Clearing the queued native world render did not restore renderer/background ownership.");
                cancelledRenderer = true;
                BeginOperation(render);
                Receipt("native-world-render-repeated-after-cancel");
            }
            return;
        }
        if (operation == "pocket")
        {
            // Native Odyssey generator has pocket-map biome metadata and no
            // portal-specific required genstep. Do not substitute a generator
            // if this representative DLC is absent from the selected modlist.
            MapGeneratorDef generator = DefDatabase<MapGeneratorDef>.GetNamed("AncientStockpile", false)
                ?? throw new InvalidOperationException("Pocket phase requires the native Odyssey AncientStockpile generator.");
            BeginOperation(() => generatedMap = PocketMapUtility.GeneratePocketMap(new IntVec3(30, 1, 30), generator, null, sourceMap!), true);
            return;
        }
        BeginSurfaceMapOperation();
    }

    private void RequireMapOperationCompletion()
    {
        if (Current.ProgramState != ProgramState.Playing || Current.Game?.Maps.Contains(sourceMap!) != true)
            throw new InvalidOperationException("Map/world operation did not preserve the original colony.");
        if (Tick != savedTick)
            throw new InvalidOperationException("Paused existing colony advanced a gameplay tick during native map/world operation.");
        if (Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
            throw new InvalidOperationException("Native map/world loading changed the existing colony's paused speed.");
        if (operation.StartsWith("world-render", StringComparison.Ordinal))
        {
            if (RendererBusy || Find.World.renderer.AllVisibleDrawLayers.OfType<WorldDrawLayer_Terrain>().Any(layer => layer.Dirty))
                throw new InvalidOperationException("World rendering completed with visible native terrain dirty or renderer ownership retained.");
            if (operation == "world-render-cancel" && !cancelledRenderer)
                throw new InvalidOperationException("World cancellation phase did not exercise native cancellation.");
            Receipt("world-native-renderer-cleanup-verified");
            return;
        }
        if (operation == "camp") destination = Find.WorldObjects.MapParentAt(operationTile);
        generatedMap = generatedMap ?? destination?.Map;
        if (generatedMap == null || !Find.Maps.Contains(generatedMap) || ReferenceEquals(generatedMap, sourceMap))
            throw new InvalidOperationException("Native map operation did not add its distinct new map.");
        if (operation == "pocket" || nativePortal != null)
        {
            if (!generatedMap.IsPocketMap || !(generatedMap.Parent is PocketMapParent parent)
                || parent.sourceMap != sourceMap || !Find.World.pocketMaps.Contains(parent))
                throw new InvalidOperationException("Native pocket map registration/source ownership did not match.");
        }
        if (nativePortal != null)
        {
            if (generatedMap.Size.x != 75 || generatedMap.Size.z != 75 || nativePortal.PocketMap != generatedMap
                || nativePortal.exit == null || nativePortal.exit.Map != generatedMap || nativePortal.exit.entrance != nativePortal
                || nativePortal.exit.GetOtherMap() != sourceMap || PocketMapUtility.currentlyGeneratingPortal != null
                || !portalRepeatMatched)
                throw new InvalidOperationException("Native portal map/exit/ownership/repeated-entry result did not match.");
        }
        if (operation == "camp" && (destination?.def != WorldObjectDefOf.Camp || destination.Faction != Faction.OfPlayer
            || generatedMap.Size != (WorldObjectDefOf.Camp.overrideMapSize ?? Find.World.info.initialMapSize)))
            throw new InvalidOperationException("Native camp definition, faction or unchanged native map size did not match.");
        if (traveller != null && (!traveller.Spawned || traveller.Map != generatedMap))
            throw new InvalidOperationException("Native caravan completion did not spawn the real transferred pawn on the new map.");
        Receipt("native-map-registration-and-pawn-completion-verified");
    }

    private void BeginSurfaceMapOperation()
    {
        if (operation == "encounter")
        {
            // A real native settlement avoids a fabricated Site (which needs
            // a nonempty SitePart and enforces a minimum 200-cell map).
            Settlement settlement = Find.WorldObjects.Settlements.FirstOrDefault(s => !s.HasMap
                && s.Faction != null && s.Faction != Faction.OfPlayer)
                ?? throw new InvalidOperationException("Copied world has no unloaded native nonplayer settlement.");
            if (Find.World.info.initialMapSize.x != 75)
                throw new InvalidOperationException("Encounter requires copied world's existing 75-cell initial map size.");
            destination = settlement;
            Caravan caravan = MakeNativeCaravan(settlement.Tile);
            BeginOperation(() => SettlementUtility.Attack(caravan, settlement));
            return;
        }
        PlanetTile tile = TileFinder.RandomSettlementTileFor(Faction.OfPlayer,
            extraValidator: candidate => TileFinder.IsValidTileForNewSettlement(candidate));
        if (!tile.Valid || Find.WorldObjects.AnyMapParentAt(tile))
            throw new InvalidOperationException("Native tile finder did not find an empty settlement tile.");
        if (operation == "standalone")
        {
            BeginOperation(() => generatedMap = GetOrGenerateMapUtility.GetOrGenerateMap(tile,
                new IntVec3(75, 1, 75), DefDatabase<WorldObjectDef>.GetNamed("AttackedNonPlayerCaravan")), true);
            return;
        }
        if (operation == "camp")
        {
            operationTile = tile;
            Caravan campers = MakeNativeCaravan(tile);
            var command = SettleInEmptyTileUtility.SetupCamp(campers) as Command_Action
                ?? throw new InvalidOperationException("Native SetupCamp did not return its actual action command.");
            if (command.Disabled) throw new InvalidOperationException("Native SetupCamp refused the selected empty tile.");
            // Invoke the actual native command's action unchanged. It owns its
            // real ASYNC queue and native Camp overrideMapSize (200 on GOG).
            BeginOperation(command.action);
            return;
        }
        if (operation != "settle") throw new InvalidOperationException("Unknown surface operation " + operation);
        Caravan settlers = MakeNativeCaravan(tile);
        BeginOperation(() =>
        {
            SettleInEmptyTileUtility.Settle(settlers);
            destination = Find.WorldObjects.MapParentAt(tile);
        });
    }

    private Caravan MakeNativeCaravan(PlanetTile tile)
    {
        traveller = sourceMap!.mapPawns.FreeColonistsSpawned.FirstOrDefault()
            ?? throw new InvalidOperationException("Copied save has no colonist for the representative native caravan.");
        traveller.DeSpawn();
        Caravan caravan = CaravanMaker.MakeCaravan(new[] { traveller }, Faction.OfPlayer, tile, true);
        if (!caravan.Spawned || !caravan.PawnsListForReading.Contains(traveller))
            throw new InvalidOperationException("Native caravan preparation did not own the real copied-save pawn.");
        Receipt("native-caravan-prepared");
        return caravan;
    }

    private void PrepareNativePortal()
    {
        if (!ModsConfig.OdysseyActive)
            throw new InvalidOperationException("Native AncientHatch portal qualification requires Odyssey in the selected copied-save lane.");
        if (PocketMapUtility.currentlyGeneratingPortal != null)
            throw new InvalidOperationException("Portal preparation found an existing native portal owner; the probe will not clear it.");
        ThingDef definition = DefDatabase<ThingDef>.GetNamed("AncientHatch");
        if (definition.portal == null || definition.portal.pocketMapSize != 75)
            throw new InvalidOperationException("Native AncientHatch definition is missing its ordinary 75-cell portal metadata.");
        IntVec3 cell = sourceMap!.AllCells.First(candidate =>
            (operation != "portal-job" || (candidate.DistanceToSquared(traveller!.Position) >= 36
                && candidate.DistanceToSquared(traveller.Position) <= 100
                && traveller.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly))) &&
            new CellRect(candidate.x - 2, candidate.z - 2, 5, 5).Cells.All(c =>
                c.InBounds(sourceMap) && c.Standable(sourceMap) && c.GetEdifice(sourceMap) == null
                && !c.GetThingList(sourceMap).OfType<Pawn>().Any()));
        nativePortal = ThingMaker.MakeThing(definition) as AncientHatch
            ?? throw new InvalidOperationException("Native AncientHatch definition constructed an unexpected portal class.");
        GenSpawn.Spawn(nativePortal, cell, sourceMap);
        // Native developer helper supplies only the already-hacked prerequisite.
        // GetOtherMap and its actual generation/exit linking stay unchanged.
        nativePortal.GetComp<CompHackable>().HackNow();
        MethodInfo inherited = AccessTools.Method(nativePortal.GetType(), "GeneratePocketMapInt");
        MethodInfo original = AccessTools.Method(typeof(MapPortal), "GeneratePocketMapInt");
        File.WriteAllText(Path.Combine(directory, "c14-native-portal-method.txt"),
            "declaring=" + inherited.DeclaringType + "; reflected=" + inherited.ReflectedType
            + "; reflectionObjectEqual=" + (inherited == original)
            + "; sameModuleAndToken=" + (inherited.Module == original.Module && inherited.MetadataToken == original.MetadataToken));
        if (!nativePortal.IsEnterable(out string reason))
            throw new InvalidOperationException("Native prepared hatch is not enterable: " + reason);
        Receipt("native-ancient-hatch-prepared-with-native-hack-helper");
    }

    private void BeginPortalJob()
    {
        traveller = sourceMap!.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn =>
            !pawn.Dead && !pawn.Downed && !pawn.InMentalState
            && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
            && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
            && pawn.jobs.IsCurrentJobPlayerInterruptible())
            ?? throw new InvalidOperationException("No healthy, interruptible copied-save colonist can walk through the portal.");
        PrepareNativePortal();
        if (nativePortal!.PocketMap != null)
            throw new InvalidOperationException("Portal job must generate its own destination, not consume an observer-generated map.");
        portalInitialMapCount = Find.Maps.Count;
        portalJobStarted = Stopwatch.GetTimestamp();
        startFrame = Time.frameCount;
        sawFocused = WindowFocused;
        Prefs.RunInBackground = true;
        Prefs.Apply();
        // Walking and the native 90-tick wait are gameplay. This explicit
        // fixture setup is not a claim that Wake-Up unpauses or permits it.
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        if (Find.TickManager.CurTimeSpeed != TimeSpeed.Normal)
            throw new InvalidOperationException("Native speed setter refused portal gameplay setup after player control returned.");
        Receipt("observer-set-normal-for-native-portal-gameplay-true-preference");
        OrderPortalJob(nativePortal);
    }

    private void OrderPortalJob(MapPortal target)
    {
        if (!target.IsEnterable(out string reason) || !traveller!.CanReach(target, PathEndMode.Touch, Danger.Deadly))
            throw new InvalidOperationException("Native portal job target is not enterable/reachable: " + reason);
        portalJob = JobMaker.MakeJob(JobDefOf.EnterPortal, target);
        portalTargetId = target.ThingID;
        portalJob.playerForced = true;
        portalLegStartTick = Tick;
        portalLegStartPosition = traveller!.Position;
        portalJobObserved = false;
        portalWaitDecreased = false;
        portalFirstWait = portalLastWait = portalLastToil = -1;
        stage = 5;
        PortalJobReceipt("before-native-ordered-job");
        // Same public order used by FloatMenuOptionProvider_EnterMapPortal.
        // Native job/toil ticking, map generation and pawn transfer stay intact.
        if (!traveller.jobs.TryTakeOrderedJob(portalJob, JobTag.Misc))
            throw new InvalidOperationException("Native job tracker refused the portal order.");
        PortalJobReceipt("native-ordered-job-accepted");
    }

    private void ObservePortalJob()
    {
        sawFocused |= WindowFocused;
        if (!Prefs.RunInBackground || !Application.runInBackground || Prefs.PauseOnLoad != pauseOnLoad
            || Find.TickManager.CurTimeSpeed != TimeSpeed.Normal)
            throw new InvalidOperationException("Native traversal changed the explicit gameplay/preference setup.");
        if (traveller == null || !traveller.Spawned || traveller.Dead || traveller.Downed)
            throw new InvalidOperationException("Native portal traveller is no longer a live spawned pawn.");
        Map origin = portalReturning ? generatedMap! : sourceMap!;
        Map? targetMap = portalReturning ? sourceMap : nativePortal!.PocketMap;
        if (targetMap != null && traveller.Map == targetMap)
        {
            if (stage == 5)
            {
                if (!portalJobObserved || !portalWaitDecreased || portalFirstWait > 90 || Tick - portalLegStartTick < 90
                    || (!portalReturning && !portalWalkObserved))
                    throw new InvalidOperationException("Native portal transfer lacked observed walking/job/wait progression.");
                if (origin.mapPawns.AllPawnsSpawned.Contains(traveller) || !targetMap.mapPawns.AllPawnsSpawned.Contains(traveller))
                    throw new InvalidOperationException("Native transfer did not move the same pawn's map membership.");
                generatedMap = nativePortal!.PocketMap;
                RequirePortalJobMaps();
                portalTransferTick = Tick;
                stage = 6;
                PortalJobReceipt("same-pawn-native-transfer-observed");
            }
            // Let the actual job tracker finish its transfer toil; do not
            // force-end it or call a private toil/tick method for the receipt.
            if (traveller.CurJobDef == JobDefOf.EnterPortal || Tick < portalTransferTick + 2) return;
            PortalJobReceipt("native-entry-job-ended-after-transfer");
            if (!portalReturning)
            {
                portalOutboundWaitFirst = portalFirstWait;
                portalOutboundWaitLast = portalLastWait;
                portalOutboundTransferTick = portalTransferTick;
                portalReturning = true;
                OrderPortalJob(nativePortal!.exit);
                return;
            }
            RequirePortalJobMaps();
            File.WriteAllText(Path.Combine(directory, "c14-background.json"),
                "{\"passed\":true,\"phase\":" + Quote(requestedPhase)
                + ",\"nativePawnPortalRoundTrip\":true,\"samePawnReturned\":true,\"outboundWalkingObserved\":true"
                + ",\"nativeOutboundAndReturnWaitObserved\":true,\"nativeJobsEnded\":true,\"observerSetNormalForGameplay\":true"
                + ",\"initialPreference\":true,\"currentPreference\":true,\"pauseOnLoad\":" + Bool(Prefs.PauseOnLoad)
                + ",\"actualFocusObserved\":true,\"focusedDuringLoad\":" + Bool(sawFocused)
                + ",\"unfocusedLifetimeQualified\":false,\"falsePreferenceLoadingQualified\":false"
                + ",\"pawnId\":" + Quote(traveller.ThingID) + ",\"savedOrInitialTick\":" + savedTick
                + ",\"outboundTransferTick\":" + portalOutboundTransferTick + ",\"completionTick\":" + Tick
                + ",\"outboundWaitFirst\":" + portalOutboundWaitFirst + ",\"outboundWaitLast\":" + portalOutboundWaitLast
                + ",\"returnWaitFirst\":" + portalFirstWait + ",\"returnWaitLast\":" + portalLastWait
                + ",\"sourceMapPreserved\":true,\"generatedMapSize\":" + generatedMap!.Size.x
                + ",\"initialMapCount\":" + portalInitialMapCount + ",\"mapCount\":" + Find.Maps.Count
                + ",\"displayedMapChangeInferred\":false,\"screenVisibilityQualified\":false,\"postCompletionHoldQualified\":false,\"automaticExit\":true}\n");
            Finish();
            return;
        }
        if (stage == 6 || traveller.Map != origin)
            throw new InvalidOperationException("Native traveller left the expected portal maps.");
        var driver = traveller.jobs.curDriver as JobDriver_EnterPortal;
        if (!ReferenceEquals(traveller.CurJob, portalJob) || driver == null || driver.MapPortal != portalJob!.targetA.Thing)
        {
            if (portalJobObserved || Tick - portalLegStartTick > 120)
                throw new InvalidOperationException("Native portal job ended/replaced before transfer, or did not start.");
            return;
        }
        portalJobObserved = true;
        if (!portalReturning && driver.CurToilIndex == 0 && traveller.Position != portalLegStartPosition)
            portalWalkObserved = true;
        if (driver.CurToilIndex == 1)
        {
            if (portalFirstWait < 0) portalFirstWait = driver.ticksLeftThisToil;
            if (portalLastWait >= 0 && driver.ticksLeftThisToil < portalLastWait) portalWaitDecreased = true;
            portalLastWait = driver.ticksLeftThisToil;
        }
        if (driver.CurToilIndex != portalLastToil || Stopwatch.GetTimestamp() - lastReceipt > Stopwatch.Frequency)
        {
            portalLastToil = driver.CurToilIndex;
            lastReceipt = Stopwatch.GetTimestamp();
            PortalJobReceipt("native-walking-or-waiting");
        }
    }

    private void RequirePortalJobMaps()
    {
        if (generatedMap == null || generatedMap.Size.x != 75 || generatedMap.Size.z != 75 || !generatedMap.IsPocketMap
            || !(generatedMap.Parent is PocketMapParent parent) || parent.sourceMap != sourceMap
            || !Find.World.pocketMaps.Contains(parent) || !Find.Maps.Contains(sourceMap!) || !Find.Maps.Contains(generatedMap)
            || Find.Maps.Count != portalInitialMapCount + 1 || nativePortal!.exit == null
            || nativePortal.exit.Map != generatedMap || nativePortal.exit.entrance != nativePortal
            || PocketMapUtility.currentlyGeneratingPortal != null)
            throw new InvalidOperationException("Native portal traversal did not preserve map count, links or generation ownership.");
    }

    private void PortalJobReceipt(string point)
    {
        Receipt(point);
        var driver = traveller?.jobs.curDriver as JobDriver_EnterPortal;
        File.AppendAllText(Path.Combine(directory, "c14-portal-job.jsonl"),
            "{\"point\":" + Quote(point) + ",\"returning\":" + Bool(portalReturning)
            + ",\"frame\":" + Time.frameCount + ",\"tick\":" + Tick + ",\"pawnId\":" + Quote(traveller!.ThingID)
            + ",\"position\":" + Quote(traveller.Position.ToString()) + ",\"mapId\":" + (traveller.Map?.uniqueID ?? -1)
            + ",\"sourceMapId\":" + sourceMap!.uniqueID + ",\"pocketMapId\":" + (nativePortal?.PocketMap?.uniqueID ?? -1)
            + ",\"nativeJobIsOrderedJob\":" + Bool(ReferenceEquals(traveller.CurJob, portalJob))
            + ",\"orderedPortalId\":" + Quote(portalTargetId) + ",\"driverPortalId\":" + Quote(driver?.MapPortal?.ThingID ?? "none")
            + ",\"jobDef\":" + Quote(traveller.CurJobDef?.defName ?? "none")
            + ",\"toilIndex\":" + (driver?.CurToilIndex ?? -1) + ",\"waitTicksLeft\":" + (driver?.ticksLeftThisToil ?? -1)
            + ",\"outboundWalkingObserved\":" + Bool(portalWalkObserved) + ",\"waitDecreased\":" + Bool(portalWaitDecreased) + "}\n");
    }

    private void GenerateNativePortal()
    {
        AncientHatch portal = nativePortal!;
        if (operation == "portal-error")
        {
            int mapsBefore = Find.Maps.Count;
            int pocketsBefore = Find.World.pocketMaps.Count;
            var nativeMetadata = portal.def.portal;
            try
            {
                // Explicit malformed-definition fault injection on this real
                // native portal. No method/action/owner substitute is installed.
                // The base body sets its owner, then faults before map creation.
                portal.def.portal = null;
                try { portal.GetOtherMap(); }
                catch (NullReferenceException) { portalErrorObserved = true; }
            }
            finally { portal.def.portal = nativeMetadata; }
            if (!portalErrorObserved || portal.PocketMap != null || Find.Maps.Count != mapsBefore
                || Find.World.pocketMaps.Count != pocketsBefore)
                throw new InvalidOperationException("Malformed portal metadata did not produce the expected pre-allocation native failure.");
            portalErrorOwnerCleared = PocketMapUtility.currentlyGeneratingPortal == null;
            Receipt("malformed-definition-native-portal-error-metadata-restored");
            if (FeatureQualification.Setting("BackgroundLoading") && !portalErrorOwnerCleared)
                throw new InvalidOperationException("Enabled native portal error cleanup retained its own portal pointer.");
            if (!portalErrorOwnerCleared && !ReferenceEquals(PocketMapUtility.currentlyGeneratingPortal, portal))
                throw new InvalidOperationException("Native portal failure changed a different portal's ownership.");
        }
        generatedMap = portal.GetOtherMap();
        int countAfterGeneration = Find.Maps.Count;
        portalRepeatMatched = ReferenceEquals(generatedMap, portal.GetOtherMap()) && Find.Maps.Count == countAfterGeneration;
        Receipt(operation == "portal-error" ? "native-portal-retry-and-repeat-completed" : "native-portal-generation-and-repeat-completed");
    }

    private static void ForeignSavePrefix() { }

    private void InstallForeignSaveControl()
    {
        if (!initialPreference || !finalPreference)
            throw new InvalidOperationException("Synthetic foreign-hook save control requires true preference throughout.");
        if (!FeatureQualification.Setting("BackgroundLoading"))
            throw new InvalidOperationException("Synthetic foreign-hook refusal requires enabled candidate background loading.");
        foreignLoadTarget = AccessTools.Method(typeof(GameDataSaveLoader), "LoadGame", new[] { typeof(string) });
        foreignLoadHarmony = new Harmony(ForeignLoadOwner);
        foreignLoadHarmony.Patch(foreignLoadTarget, prefix: new HarmonyMethod(typeof(C14BackgroundProbe), nameof(ForeignSavePrefix)));
        File.WriteAllText(Path.Combine(directory, "c14-foreign-hook-control.txt"),
            "Synthetic compatibility control only; no real supplier conflict claim. Owner=" + ForeignLoadOwner
            + "; target=Verse.GameDataSaveLoader.LoadGame(System.String); prefix is void/no-op; native loader is unchanged.");
        Receipt("synthetic-foreign-no-op-save-prefix-installed");
    }

    private void VerifyForeignSaveRefusal()
    {
        Type product = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "WakeUp")
            .GetType("WakeUp.BackgroundLoadingRuntime", true)!;
        refusedSaveStatus = (string)AccessTools.Property(product, "Status").GetValue(null, null);
        if (!refusedSaveStatus.Contains("stopped because another mod changed the loading lifecycle"))
            throw new InvalidOperationException("Candidate did not explicitly refuse the synthetic foreign loader hook: " + refusedSaveStatus);
        string mapStatus = (string)AccessTools.Property(product, "MapStatus").GetValue(null, null);
        File.AppendAllText(Path.Combine(directory, "c14-foreign-hook-control.txt"), "\nsharedStatus=" + refusedSaveStatus + "\nmapStatus=" + mapStatus);
        if (!mapStatus.Contains("stopped because another mod changed the shared loading lifecycle"))
            throw new InvalidOperationException("Map status still claimed support after shared lifecycle refusal.");
        Receipt("synthetic-foreign-hook-refused-native-save-remains-queued");
    }

    private void Finish()
    {
        stage = -1;
        if (foreignLoadHarmony != null && foreignLoadTarget != null)
            foreignLoadHarmony.Unpatch(foreignLoadTarget, HarmonyPatchType.Prefix, ForeignLoadOwner);
        // No resume to true: record and request normal exit in this same frame.
        // Process-local preference changes are not saved by this probe.
        if (captured) Prefs.PauseOnLoad = originalPause;
        Root.Shutdown();
    }

    private void Fail(Exception error)
    {
        if (stage < 0) return;
        File.WriteAllText(Path.Combine(directory, "c14-background-error.txt"), error.ToString());
        File.WriteAllText(Path.Combine(directory, "c14-background.json"), "{\"passed\":false,\"phase\":" + Quote(requestedPhase) + "}\n");
        Finish();
    }
}
