// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;

// Private functional probe. Native page and callbacks, real external minimization;
// no patch to the guarded lifecycle and no simulated focus/background engine flag.
[DefaultExecutionOrder(-10000)]
public sealed class FixtureWorldBackground : MonoBehaviour
{
    private static string directory = "";
    private Page_CreateWorldParams? page;
    private Page_SelectStartingSite? next;
    private int stage;
    private long started = Stopwatch.GetTimestamp();
    private long minimizedAt;
    private long completedAt;
    private IntPtr window;
    private bool originalPreference;
    private bool capturedPreference;
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr handle);

    internal static void Start(string path)
    {
        directory = path;
        new GameObject("FixtureWorldBackground").AddComponent<FixtureWorldBackground>();
    }
    private void Receipt(string phase) => File.AppendAllText(Path.Combine(directory, "world-background.jsonl"),
        "{\"phase\":\"" + phase + "\",\"utc\":\"" + DateTime.UtcNow.ToString("O")
        + "\",\"frame\":" + Time.frameCount + ",\"focused\":" + Bool(Application.isFocused)
        + ",\"minimized\":" + Bool(IsIconic(window)) + ",\"engineBackground\":" + Bool(Application.runInBackground)
        + ",\"preference\":" + Bool(Prefs.RunInBackground) + ",\"scene\":\"" + SceneManager.GetActiveScene().name
        + "\",\"worldPresent\":" + Bool(Current.Game?.World != null) + ",\"pageOpen\":" + Bool(page?.IsOpen == true)
        + ",\"nextOpen\":" + Bool(next?.IsOpen == true) + "}\n");
    private static string Bool(bool value) => value ? "true" : "false";
    private void Update()
    {
        if (stage < 0) return;
        try
        {
            if (Stopwatch.GetTimestamp() - started > Stopwatch.Frequency * 540)
                throw new TimeoutException("World background probe exceeded nine minutes.");
            if (stage == 3) { Resume(); return; }
            if (LongEventHandler.AnyEventNowOrWaiting) return;
            if (stage == 0)
            {
                using (var process = Process.GetCurrentProcess()) window = process.MainWindowHandle;
                if (window == IntPtr.Zero || !Application.isFocused) return;
                // Accepted private formatter workaround remains probe-only.
                bool nativeStackTraces = Array.IndexOf(Environment.GetCommandLineArgs(), "--fixture-native-stack-traces") >= 0;
                File.WriteAllText(Path.Combine(directory, "stack-trace-policy.txt"), nativeStackTraces ? "ordinary-native-unchanged" : "private-probe-suppression");
                if (!nativeStackTraces)
                {
                    Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
                    Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
                }
                originalPreference = Prefs.RunInBackground;
                capturedPreference = true;
                Prefs.RunInBackground = true; Prefs.Apply();
                Game.ClearCaches();
                Current.Game = new Game { InitData = new GameInitData(), Scenario = ScenarioDefOf.Crashlanded.scenario };
                Find.Scenario.PreConfigure();
                Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
                OpenPage();
                stage = 1;
                Receipt("minimize-arm");
                return;
            }
            if (stage == 1)
            {
                if (Application.isFocused || !IsIconic(window)) { minimizedAt = 0; return; }
                if (minimizedAt == 0) minimizedAt = Stopwatch.GetTimestamp();
                if (Stopwatch.GetTimestamp() - minimizedAt < Stopwatch.Frequency * 2) return;
                Begin(false);
            }
        }
        catch (Exception e) { Fail(e); }
    }
    private void OpenPage()
    {
        page = new Page_CreateWorldParams();
        next = new Page_SelectStartingSite();
        page.next = next;
        Find.WindowStack.Add(page);
        // Deterministic seed; ordinary public 30% coverage and native faction setup.
        AccessTools.Field(typeof(Page_CreateWorldParams), "seedString").SetValue(page, "WakeUpWorldBackground");
        AccessTools.Field(typeof(Page_CreateWorldParams), "planetCoverage").SetValue(page, 0.3f);
    }
    private void Begin(bool changePreference)
    {
        Prefs.RunInBackground = false; Prefs.Apply();
        FeatureQualification.Background(directory, "world-before", false, false);
        stage = changePreference ? 4 : 2;
        bool returned = (bool)AccessTools.Method(typeof(Page_CreateWorldParams), "CanDoNext").Invoke(page, null);
        if (returned) throw new InvalidOperationException("Native world page did not defer its own transition.");
        FeatureQualification.Background(directory, "world-queued", true, false);
        Receipt(changePreference ? "changed-preference-queued" : "minimized-queued");
        if (changePreference)
        {
            Prefs.RunInBackground = true; Prefs.Apply();
            FeatureQualification.Background(directory, "world-preference-changed", true, true);
        }
    }
    private void LateUpdate()
    {
        if ((stage != 2 && stage != 4) || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            if (Current.ProgramState != ProgramState.Entry || SceneManager.GetActiveScene().name != "Entry"
                || Current.Game?.World == null || page?.IsOpen != false || next?.IsOpen != true)
                throw new InvalidOperationException("World/page/redraw chain did not complete in Entry.");
            FeatureQualification.Background(directory, "world-completed", false, stage == 4);
            Receipt(stage == 4 ? "changed-preference-completed" : "minimized-completed");
            if (stage == 4) { Finish(); return; }
            if (Application.isFocused || !IsIconic(window)) throw new InvalidOperationException("World did not complete while minimized.");
            completedAt = Stopwatch.GetTimestamp();
            stage = 3;
        }
        catch (Exception e) { Fail(e); }
    }
    private void OnApplicationFocus(bool focused)
    {
        if (focused && stage == 3) try { Resume(); } catch (Exception e) { Fail(e); }
    }
    private void Resume()
    {
        if (Prefs.RunInBackground || Application.runInBackground) throw new InvalidOperationException("World completion did not restore false preference.");
        if (!Application.isFocused) return;
        double seconds = (Stopwatch.GetTimestamp() - completedAt) / (double)Stopwatch.Frequency;
        if (seconds < 5) throw new InvalidOperationException("Restore must follow a five-second completion hold.");
        Receipt("minimized-hold-resumed");
        File.WriteAllText(Path.Combine(directory, "world-hold-seconds.txt"), seconds.ToString("F3", CultureInfo.InvariantCulture));
        Prefs.RunInBackground = true; Prefs.Apply();
        next!.Close();
        OpenPage();
        Begin(true);
    }
    private void Finish()
    {
        stage = -1;
        File.WriteAllText(Path.Combine(directory, "world-background.json"), "{\"passed\":true,\"nativePageRuns\":2,\"minimized\":true,\"changedPreference\":true}");
        if (capturedPreference) { Prefs.RunInBackground = originalPreference; Prefs.Apply(); }
        Root.Shutdown();
    }
    private void Fail(Exception e)
    {
        stage = -1;
        File.WriteAllText(Path.Combine(directory, "world-background-error.txt"), e.ToString());
        File.WriteAllText(Path.Combine(directory, "world-background.json"), "{\"passed\":false}");
        if (capturedPreference) { Prefs.RunInBackground = originalPreference; Prefs.Apply(); }
        Root.Shutdown();
    }
}
