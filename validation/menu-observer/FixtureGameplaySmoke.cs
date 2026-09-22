// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;

// Explicitly selected fixture-only create/tick/save/reload check. This assembly
// is never shipped with Wake-Up. It runs after the independently measured menu,
// uses native game APIs and exits normally. It drives no UI and imports no save.
[DefaultExecutionOrder(-10000)]
public sealed class FixtureGameplaySmoke : MonoBehaviour
{
    private static string directory = "";
    private const string SaveName = "RloFixtureSmoke";
    private int stage;
    private long started = Stopwatch.GetTimestamp();
    private int savedTick;
    private string[] pawnIds = Array.Empty<string>();
    private string mapId = "";
    private Map? originalMap;
    private bool originalPauseOnLoad;
    private bool preferencesCaptured;
    private int pausedObservationTick;
    private int pausedObservationFrames;
    private int audioCleanupFrame;
    private bool audioCompleted;
    private int firstSavedTick;
    private long lastDiagnostic;
    private readonly bool holdMode = Environment.GetCommandLineArgs().Contains("--fixture-background-hold");
    private string? armedReload;
    private long minimizedSince;
    private long holdStarted;
    private int holdTick;
    private TimeSpeed holdSpeed;
    private int holdsPassed;
    private IntPtr fixtureWindow;
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);

    private void HoldReceipt(string phase, string extra = "") => File.AppendAllText(Path.Combine(directory, "background-hold.jsonl"),
        "{\"phase\":\"" + phase + "\",\"utc\":\"" + DateTime.UtcNow.ToString("O") + "\",\"frame\":" + Time.frameCount
        + ",\"tick\":" + Find.TickManager.TicksGame + ",\"speed\":\"" + Find.TickManager.CurTimeSpeed + "\",\"focused\":" + Application.isFocused.ToString().ToLowerInvariant()
        + ",\"minimized\":" + IsIconic(fixtureWindow).ToString().ToLowerInvariant() + extra + "}\n");

    private void OnApplicationFocus(bool focused)
    {
        if (!focused || (stage != 5 && stage != 6)) return;
        try { ResumeHold("OnApplicationFocus"); } catch (Exception e) { Fail(e); }
    }

    private void ResumeHold(string callback)
    {
        if (Find.TickManager.TicksGame != holdTick || Find.TickManager.CurTimeSpeed != holdSpeed
            || Prefs.RunInBackground || Application.runInBackground)
            throw new InvalidOperationException("Background hold advanced or changed the actual preference/speed before observation.");
        if (!Application.isFocused) return;
        double seconds = (Stopwatch.GetTimestamp() - holdStarted) / (double)Stopwatch.Frequency;
        if (seconds < 5) throw new InvalidOperationException("Fixture restored too early for five-second hold qualification.");
        HoldReceipt(stage == 5 ? "paused-resume" : "unpaused-resume", ",\"seconds\":" + seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
            + ",\"callback\":\"" + callback + "\",\"passed\":true");
        holdsPassed++;
        Prefs.RunInBackground = true;
        if (stage == 5) { stage = 3; return; }
        originalMap = Find.CurrentMap;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        Prefs.PauseOnLoad = true;
        savedTick = firstSavedTick;
        stage = 7;
        BeginReload("current-preference");
        Prefs.RunInBackground = true;
        FeatureQualification.Background(directory, "current-preference-changed-during-load", true, true);
    }

    private void ObserveScene()
    {
        long now = Stopwatch.GetTimestamp();
        if (now - lastDiagnostic < Stopwatch.Frequency * 2) return;
        lastDiagnostic = now;
        object? current = AccessTools.Field(typeof(LongEventHandler), "currentEvent").GetValue(null);
        object? queue = AccessTools.Field(typeof(LongEventHandler), "eventQueue").GetValue(null);
        var worker = (Thread?)AccessTools.Field(typeof(LongEventHandler), "eventThread").GetValue(null);
        var scene = (AsyncOperation?)AccessTools.Field(typeof(LongEventHandler), "levelLoadOp").GetValue(null);
        string Field(string name) => current == null ? "-" : Convert.ToString(AccessTools.Field(current.GetType(), name).GetValue(current)) ?? "null";
        File.AppendAllText(Path.Combine(directory, "scene-state.txt"), DateTime.UtcNow.ToString("O")
            + " stage=" + stage + " frame=" + Time.frameCount + " scene=" + SceneManager.GetActiveScene().name
            + " focused=" + Application.isFocused + " background=" + Application.runInBackground + " pref=" + Prefs.RunInBackground
            + " event=" + Field("eventTextKey") + " level=" + Field("levelToLoad") + " displayed=" + Field("alreadyDisplayed")
            + " async=" + Field("doAsynchronously") + " queued=" + (queue == null ? "-" : AccessTools.Property(queue.GetType(), "Count").GetValue(queue, null))
            + " worker=" + (worker == null ? "-" : worker.ThreadState.ToString())
            + " sceneOp=" + (scene == null ? "-" : scene.progress + "/" + scene.isDone + "/" + scene.allowSceneActivation)
            + Environment.NewLine);
    }

    internal static void Start(string evidenceDirectory)
    {
        directory = evidenceDirectory;
        var host = new GameObject("FixtureGameplaySmoke");
        DontDestroyOnLoad(host);
        host.AddComponent<FixtureGameplaySmoke>();
    }

    private void Update()
    {
        if (stage < 0) return;
        try
        {
            ObserveScene();
            if ((Stopwatch.GetTimestamp()-started)/(double)Stopwatch.Frequency > 540)
                throw new TimeoutException("Fixture gameplay smoke exceeded nine minutes.");
            if (stage == 5 || stage == 6) { ResumeHold("early-Update"); return; }
            if (armedReload != null)
            {
                if (Application.isFocused || !IsIconic(fixtureWindow)) { minimizedSince = 0; return; }
                if (minimizedSince == 0) minimizedSince = Stopwatch.GetTimestamp();
                if (Stopwatch.GetTimestamp() - minimizedSince < Stopwatch.Frequency * 2) return;
                string label = armedReload;
                armedReload = null;
                HoldReceipt(label + "-load-minimized");
                BeginReload(label);
                return;
            }
            if (LongEventHandler.AnyEventNowOrWaiting) return;
            if (stage == 0)
            {
                // Private bootstrap experiment: the frozen Unity player spins
                // while formatting an empty native exception stack trace.
                File.WriteAllText(Path.Combine(directory, "scene-bootstrap.txt"),
                    "Exception stack trace was " + Application.GetStackTraceLogType(LogType.Exception)
                    + "; Error stack trace was " + Application.GetStackTraceLogType(LogType.Error));
                bool nativeStackTraces = Array.IndexOf(Environment.GetCommandLineArgs(), "--fixture-native-stack-traces") >= 0;
                File.WriteAllText(Path.Combine(directory, "stack-trace-policy.txt"), nativeStackTraces ? "ordinary-native-unchanged" : "private-probe-suppression");
                if (!nativeStackTraces)
                {
                    Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
                    Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
                }
                originalPauseOnLoad = Prefs.PauseOnLoad;
                preferencesCaptured = true;
                if (holdMode)
                {
                    fixtureWindow = GetForegroundWindow();
                    GetWindowThreadProcessId(fixtureWindow, out uint pid);
                    if (pid != Process.GetCurrentProcess().Id) throw new InvalidOperationException("Hold qualification requires the fixture initially focused.");
                }
                CheckSettingsRoundTrip();
                stage = 1;
                if (Environment.GetCommandLineArgs().Contains("--fixture-gameplay-save"))
                    GameDataSaveLoader.LoadGame(SaveName);
                else
                    LongEventHandler.QueueLongEvent(PrepareGame, "Play", "GeneratingWorld", true, Fail);
                return;
            }
            // Advance-audio navigation intentionally has no current map for one
            // frame. Observe its native return before the ordinary map gate.
            if (stage == 11)
            {
                if (FixtureMenuObserver.C10AdvanceAudioProbe.AdvanceMapNavigation())
                { audioCompleted = true; stage = 1; }
                return;
            }
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
            if (stage == 10) { Finish(); return; }
            if (stage == 9)
            {
                if (FixtureMenuObserver.C10AudioMeasurementProbe.Selected)
                {
                    if (FixtureMenuObserver.C10AudioMeasurementProbe.AdvanceGameplay()) return;
                    audioCompleted = true; stage = 1;
                }
                if (!FixtureMenuObserver.C10AudioMeasurementProbe.Selected && FixtureMenuObserver.C10NaturalAudioProbe.AdvanceGameplay()) return;
                if (FixtureMenuObserver.C10AdvanceAudioProbe.Selected)
                {
                    FixtureMenuObserver.C10AdvanceAudioProbe.BeginMapNavigation();
                    stage = 11;
                    return;
                }
                audioCompleted = true;
                stage = 1;
            }
            if (stage == 8)
            {
                if (Time.frameCount < audioCleanupFrame + 3) return;
                FixtureMenuObserver.C10PreparedAudioProbe.BeforeExit();
                audioCompleted = true;
                stage = 1;
            }
            if (stage == 1)
            {
                if (!audioCompleted && FixtureMenuObserver.C10AudioMeasurementProbe.Selected)
                {
                    FixtureMenuObserver.C10AudioMeasurementProbe.BeginGameplay();
                    stage = 9;
                    return;
                }
                if (!audioCompleted && FixtureMenuObserver.C10NaturalAudioProbe.Selected)
                {
                    FixtureMenuObserver.C10NaturalAudioProbe.BeginGameplay();
                    stage = 9;
                    return;
                }
                if (!audioCompleted && FixtureMenuObserver.C10PreparedAudioProbe.Gameplay)
                {
                    FixtureMenuObserver.C10PreparedAudioProbe.Start(directory);
                    audioCleanupFrame = Time.frameCount;
                    stage = 8;
                    return;
                }
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                for (int i=0;i<600;i++) Find.TickManager.DoSingleTick();
                originalMap = Find.CurrentMap;
                if (originalMap.Size.x != 75 || originalMap.Size.z != 75) throw new InvalidOperationException("Unexpected map size.");
                pawnIds = originalMap.mapPawns.FreeColonistsSpawned.Select(p=>p.GetUniqueLoadID()).OrderBy(x=>x).ToArray();
                if (pawnIds.Length != 3) throw new InvalidOperationException("Expected three spawned crashlanded colonists.");
                savedTick = Find.TickManager.TicksGame;
                firstSavedTick = savedTick;
                mapId = originalMap.GetUniqueLoadID();
                FixtureMenuObserver.C08InspectionScene.Place(originalMap);
                Prefs.PauseOnLoad = true;
                GameDataSaveLoader.SaveGame(SaveName);
                if (!File.Exists(GenFilePaths.FilePathForSavedGame(SaveName))) throw new IOException("Save was not created.");
                File.WriteAllText(Path.Combine(directory,"gameplay-before-reload.json"),"{\"ticks\":"+savedTick+",\"colonists\":3,\"mapSize\":75}");
                stage = 2;
                QueueReload("paused");
                return;
            }
            if (stage == 3)
            {
                if (Find.TickManager.CurTimeSpeed != TimeSpeed.Paused || Find.TickManager.TicksGame != pausedObservationTick)
                    throw new InvalidOperationException("Paused reload advanced or changed speed after completion without observer tick calls.");
                if (++pausedObservationFrames < 3) return;
                originalMap = Find.CurrentMap;
                Prefs.PauseOnLoad = false;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                savedTick = Find.TickManager.TicksGame;
                GameDataSaveLoader.SaveGame(SaveName + "Unpaused");
                stage = 4;
                QueueReload("unpaused");
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private void QueueReload(string label)
    {
        if (holdMode)
        {
            // Only the OLD map is paused while awaiting external minimization.
            // The newly loaded map is observed before changing its native speed.
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Prefs.RunInBackground = true;
            minimizedSince = 0;
            armedReload = label;
            HoldReceipt(label + "-arm");
            return;
        }
        BeginReload(label);
    }

    private void BeginReload(string label)
    {
        // Use the actual preference setter (which applies native preferences),
        // never a simulated focus value or a direct engine-only false write.
        Prefs.RunInBackground = !FeatureQualification.Setting("BackgroundLoading");
        Prefs.Apply(); // Also applies when the stored value was already false.
        FeatureQualification.Background(directory, label + "-before-load", false);
        GameDataSaveLoader.LoadGame(label == "unpaused" ? SaveName + "Unpaused" : SaveName);
        FeatureQualification.Background(directory, label + "-load-queued", true);
    }

    private void LateUpdate()
    {
        // Root_Play releases the session during Update. LateUpdate observes the
        // real false restoration in that same frame, before an unfocused engine
        // can stop future updates. Only then may the observer restore true.
        if (armedReload != null || (stage != 2 && stage != 4 && stage != 7) || LongEventHandler.AnyEventNowOrWaiting
            || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
            || ReferenceEquals(originalMap, Find.CurrentMap)) return;
        try
        {
            bool changedCase = stage == 7;
            bool pausedCase = stage == 2 || changedCase;
            string label = changedCase ? "current-preference" : pausedCase ? "paused" : "unpaused";
            TimeSpeed observedSpeed = Find.TickManager.CurTimeSpeed;
            int observedTick = Find.TickManager.TicksGame;
            File.AppendAllText(Path.Combine(directory, "reload-observations.jsonl"),
                "{\"case\":\"" + label + "\",\"pauseOnLoad\":" + Prefs.PauseOnLoad.ToString().ToLowerInvariant()
                + ",\"savedTick\":" + savedTick + ",\"observedTick\":" + observedTick
                + ",\"observedSpeed\":\"" + observedSpeed + "\",\"frame\":" + Time.frameCount
                + ",\"focused\":" + Application.isFocused.ToString().ToLowerInvariant()
                + ",\"observedBeforeObserverPause\":true,\"nativeSerializesTimeSpeed\":false}\n");
            FeatureQualification.Background(directory, label + "-after-load", false, changedCase ? (bool?)true : null);
            string[] restored = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Select(p=>p.GetUniqueLoadID()).OrderBy(x=>x).ToArray();
            if (Find.CurrentMap.GetUniqueLoadID() != mapId || !restored.SequenceEqual(pawnIds))
                throw new InvalidOperationException(label + " save/reload map or colonist identity mismatch.");
            // Native TickManager does not serialize time speed. PauseOnLoad
            // deliberately performs one tick and pauses in a completion callback.
            if (pausedCase)
            {
                if (observedSpeed != TimeSpeed.Paused || observedTick != savedTick + 1)
                    throw new InvalidOperationException("PauseOnLoad expected Paused and exactly its one native completion tick; observed "
                        + observedSpeed + ", tick delta " + (observedTick - savedTick));
                pausedObservationTick = observedTick;
                if (changedCase) { Finish(); return; }
                if (holdMode) { StartHold(true, observedTick, observedSpeed); return; }
                Prefs.RunInBackground = true;
                stage = 3;
                return;
            }
            if (observedSpeed != TimeSpeed.Normal || observedTick < savedTick)
                throw new InvalidOperationException("PauseOnLoad=false expected native Normal speed and preserved tick state; observed "
                    + observedSpeed + ", tick delta " + (observedTick - savedTick));
            if (FeatureQualification.Setting("BackgroundLoading") && !Application.isFocused && observedTick != savedTick)
                throw new InvalidOperationException("Unfocused background-load completion advanced the unpaused colony before the observer restored its background preference; tick delta "
                    + (observedTick - savedTick));
            if (holdMode) { StartHold(false, observedTick, observedSpeed); return; }
            Prefs.RunInBackground = true;
            Finish();
        }
        catch (Exception error) { Fail(error); }
    }

    private void StartHold(bool paused, int tick, TimeSpeed speed)
    {
        if (Application.isFocused || !IsIconic(fixtureWindow)) throw new InvalidOperationException("Hold completion must remain unfocused and minimized.");
        holdTick = tick;
        holdSpeed = speed;
        holdStarted = Stopwatch.GetTimestamp();
        stage = paused ? 5 : 6;
        HoldReceipt(paused ? "paused-completed" : "unpaused-completed");
    }

    private void Finish()
    {
            FixtureMenuObserver.C08InspectionScene.Verify(Find.CurrentMap, directory);
            if (FixtureMenuObserver.C10AdvanceAudioProbe.Selected && !FixtureMenuObserver.C10AdvanceAudioProbe.TryAfterGameplayReload())
            { stage = 10; return; }
            FixtureMenuObserver.C10NaturalAudioProbe.AfterGameplayReload();
            FixtureMenuObserver.C10PreparedAudioProbe.AfterGameplayReload();
            if (holdMode && holdsPassed != 2) throw new InvalidOperationException("Both background holds must pass.");
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            for (int i=0;i<60;i++) Find.TickManager.DoSingleTick();
            bool copiedSave = Environment.GetCommandLineArgs().Contains("--fixture-gameplay-save");
            File.WriteAllText(Path.Combine(directory,"gameplay-smoke.json"),"{\"passed\":true,\"createdMap\":"+(!copiedSave).ToString().ToLowerInvariant()+",\"startedFromFixtureSave\":"+copiedSave.ToString().ToLowerInvariant()+",\"mapSize\":75,\"colonists\":3,\"ticksBeforeSave\":"+firstSavedTick+",\"ticksAfterReload\":"+Find.TickManager.TicksGame+",\"saveReloadMatched\":true,\"pausedCompletionNativeTickMatched\":true,\"pausedStableFrames\":"+pausedObservationFrames+",\"unpausedNativeSpeedMatched\":true,\"backgroundHoldsPassed\":"+holdsPassed+",\"currentPreferenceChangePassed\":"+holdMode.ToString().ToLowerInvariant()+"}");
            stage = -1;
            FixtureMenuObserver.C10AudioMeasurementProbe.Finish();
            RestorePreferences();
            Root.Shutdown();
    }

    private void RestorePreferences()
    {
        if (preferencesCaptured) Prefs.PauseOnLoad = originalPauseOnLoad;
        Prefs.RunInBackground = true; // Independent observer's normal-exit policy.
    }

    private static void PrepareGame()
    {
        // Same initialization order as native quick-test play, using a small
        // deterministic world/map to keep this a focused compatibility check.
        Rand.Seed = 19840117;
        Current.ProgramState = ProgramState.Entry;
        Game.ClearCaches();
        Current.Game = new Game();
        Current.Game.InitData = new GameInitData();
        Current.Game.Scenario = ScenarioDefOf.Crashlanded.scenario;
        Find.Scenario.PreConfigure();
        Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
        // Rare authored biomes need an ordinary world area; 5% is a native
        // developer option, while 30% is the smallest normal coverage choice.
        float coverage = FixtureMenuObserver.C10AdvanceAudioProbe.Selected ? 0.3f : 0.05f;
        Current.Game.World = WorldGenerator.GenerateWorld(coverage,"RloFixtureSmoke",OverallRainfall.Normal,OverallTemperature.Normal,OverallPopulation.Normal,LandmarkDensity.Normal);
        if (FixtureMenuObserver.C10AdvanceAudioProbe.Selected)
            Find.GameInitData.startingTile = FixtureMenuObserver.C10AdvanceAudioProbe.ChooseStartingTile();
        else Find.GameInitData.ChooseRandomStartingTile();
        Find.GameInitData.mapSize = 75;
        Find.Scenario.PostIdeoChosen();
        Find.GameInitData.PrepForMapGen();
        Find.Scenario.PreMapGenerate();
    }

    private static void CheckSettingsRoundTrip()
    {
        Type? type = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="WakeUp")
            ?.GetType("WakeUp.WakeUpMod");
        if (type == null) return; // Physically absent control.
        Mod mod = LoadedModManager.GetMod(type);
        Type settingsType = type.Assembly.GetType("WakeUp.WakeUpSettings",true)!;
        var get = AccessTools.Method(typeof(Mod),"GetSettings").MakeGenericMethod(settingsType);
        object settings = get.Invoke(mod,null);
        FieldInfo field = settingsType.GetField("PngCache")!;
        bool original = (bool)field.GetValue(settings);
        field.SetValue(settings,!original);
        mod.WriteSettings();
        // Read through the game's own loader, not the cached Mod settings field.
        MethodInfo read = AccessTools.Method(typeof(LoadedModManager),"ReadModSettings").MakeGenericMethod(settingsType);
        object reloaded = read.Invoke(null,new object[]{mod.Content.FolderName,type.Name});
        bool matched = (bool)field.GetValue(reloaded)==!original;
        field.SetValue(settings,original);
        mod.WriteSettings();
        if (!matched) throw new InvalidOperationException("Settings did not persist through the native loader.");
        File.WriteAllText(Path.Combine(directory,"settings-smoke.json"),"{\"passed\":true,\"originalValueRestored\":true}");
    }

    private void Fail(Exception error)
    {
        FixtureMenuObserver.C10AudioMeasurementProbe.Fail(error);
        try { if (!FixtureMenuObserver.C10AudioMeasurementProbe.Selected) FixtureMenuObserver.C10NaturalAudioProbe.Abort(error); }
        catch (Exception receiptError) { Log.Error("[FixtureGameplaySmoke] Natural audio failure receipt: " + receiptError); }
        stage = -1;
        RestorePreferences();
        File.WriteAllText(Path.Combine(directory,"gameplay-smoke.json"),"{\"passed\":false}");
        File.WriteAllText(Path.Combine(directory,"gameplay-smoke-error.txt"),error.ToString());
        Log.Error("[FixtureGameplaySmoke] " + error);
        Root.Shutdown();
    }
}
