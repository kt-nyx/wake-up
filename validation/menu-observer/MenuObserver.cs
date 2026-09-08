// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Independent of the optimizer so absent and candidate runs use the same timer.
public sealed class MenuObserverMod : Mod
{
    private const string Owner = "local.fixture.menuobserver";
    private const string Profile = @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\profile";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static string? directory;
    private static string runId = "";
    private static long startCounter;
    private static long counterFrequency;
    private static int emitted;
    private static bool exitAfterMenuReady;

    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long value);

    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long value);

    private static long Counter()
    {
        if (!QueryPerformanceCounter(out long value)) throw new InvalidOperationException("Windows performance counter unavailable.");
        return value;
    }

    public MenuObserverMod(ModContentPack content) : base(content)
    {
        var harmony = new Harmony(Owner);
        try
        {
            string[] paths = Environment.GetCommandLineArgs()
                .Where(a => a.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (paths.Length != 1 || !string.Equals(Path.GetFullPath(paths[0].Substring(16)), Profile, StringComparison.OrdinalIgnoreCase)) return;
            directory = Path.Combine(Profile, "FixtureMenuObserver");
            // The launcher writes this immediately before starting this process.
            // Unity Mono Stopwatch has a process-relative origin, unlike raw QPC.
            if (!QueryPerformanceFrequency(out counterFrequency) || counterFrequency <= 0)
                throw new InvalidOperationException("Windows performance counter frequency unavailable.");
            string[] clock = File.ReadAllLines(Path.Combine(directory, "launch-clock.txt"));
            if (clock.Length != 3 || !Guid.TryParseExact(clock[0], "N", out _)
                || !long.TryParse(clock[1], NumberStyles.None, Invariant, out long frequency)
                || frequency != counterFrequency
                || !long.TryParse(clock[2], NumberStyles.None, Invariant, out startCounter)
                || startCounter <= 0 || startCounter > Counter())
                throw new InvalidOperationException("Invalid or incompatible fixture launch clock.");
            runId = clock[0];
            exitAfterMenuReady = Environment.GetCommandLineArgs().Contains("--fixture-exit-after-menu-ready");
            if (typeof(Mod).Assembly.ManifestModule.ModuleVersionId != new Guid("13bee51f-e6fa-4214-a4a5-e1e7b84a41ee"))
                throw new InvalidOperationException("Unsupported fixture game.");
            MethodInfo target = AccessTools.Method("RimWorld.MainMenuDrawer:MainMenuOnGUI");
            if (target == null || !target.IsStatic || target.ReturnType != typeof(void) || target.GetParameters().Length != 0)
                throw new InvalidOperationException("Main-menu drawing method unavailable.");
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(MenuObserverMod), nameof(MenuDrawn)) { priority = Priority.Last });
            if (Environment.GetCommandLineArgs().Contains("--fixture-residual-probe"))
                ResidualProbe.Install(harmony, target, directory);
            Log.Message("[FixtureMenuObserver] installed: first completed main-menu repaint; launchCounter="
                + startCounter.ToString(Invariant) + "; nativeCounter=" + Counter().ToString(Invariant)
                + "; monoStopwatchCounter=" + Stopwatch.GetTimestamp().ToString(Invariant));
        }
        catch (Exception exception)
        {
            try { harmony.UnpatchAll(Owner); } catch { }
            try
            {
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "installation-error.txt"), exception.ToString());
                }
            }
            catch { }
        }
    }

    private static void MenuDrawn(bool __runOriginal)
    {
        try
        {
            // Layout/input events are not a displayed menu. No button tests,
            // focus changes, quiet-period heuristics or minimized-state checks.
            if (Volatile.Read(ref emitted) != 0 || !__runOriginal || directory == null
                || Event.current == null || Event.current.type != EventType.Repaint
                || Current.ProgramState != ProgramState.Entry || !PlayDataLoader.Loaded) return;
            long counter = Counter();
            if (Interlocked.CompareExchange(ref emitted, 1, 0) != 0) return;
            int pid;
            using (Process process = Process.GetCurrentProcess()) pid = process.Id;
            double elapsed = (counter - startCounter) / (double)counterFrequency;
            string json = "{\"schema\":\"fixture-menu-ready.v1\",\"event\":\"menu-ready\","
                + "\"definition\":\"first-completed-main-menu-repaint\",\"runId\":\"" + runId
                + "\",\"processId\":" + pid.ToString(Invariant)
                + ",\"utc\":\"" + DateTime.UtcNow.ToString("O", Invariant)
                + "\",\"counter\":" + counter.ToString(Invariant)
                + ",\"counterFrequency\":" + counterFrequency.ToString(Invariant)
                + ",\"frame\":" + Time.frameCount.ToString(Invariant)
                + ",\"elapsedSeconds\":" + elapsed.ToString("F6", Invariant) + "}";
            string path = Path.Combine(directory, "menu-ready.json");
            File.WriteAllText(path + ".tmp", json + Environment.NewLine, new UTF8Encoding(false));
            File.Move(path + ".tmp", path);
            Log.Message("[FixtureMenuObserver] menu-ready elapsedSeconds=" + elapsed.ToString("F6", Invariant));
            // Only arm shutdown after the timing evidence is safely written.
            // Update runs outside this GUI callback; the delay is not loading time.
            if (Environment.GetCommandLineArgs().Contains("--fixture-gameplay-smoke"))
                FixtureGameplaySmoke.Start(directory!);
            else if (exitAfterMenuReady)
                new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>();
        }
        catch { /* Observation must not change menu behavior or its exceptions. */ }
    }
}

public sealed class MenuReadyExit : MonoBehaviour
{
    private readonly long armedAt = Stopwatch.GetTimestamp();
    private bool requested;

    private void Update()
    {
        if (requested || Stopwatch.GetTimestamp() - armedAt < Stopwatch.Frequency) return;
        requested = true;
        // The in-game save-value predicate requires a TickManager, absent at startup.
        if (Current.ProgramState != ProgramState.Entry || Current.Game != null)
        {
            Log.Warning("[FixtureMenuObserver] automatic exit cancelled: no longer at the startup menu");
            return;
        }
        Log.Message("[FixtureMenuObserver] requesting normal shutdown after menu-ready");
        Root.Shutdown(); // The same path as the main menu's Quit to OS button.
    }
}
