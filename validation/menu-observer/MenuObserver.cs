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
    private static string packageRoot = "";
    private static string runId = "";
    private static long startCounter;
    private static long counterFrequency;
    private static int emitted;
    internal static bool MenuObserved => Volatile.Read(ref emitted) != 0;
    private static bool exitAfterMenuReady;
    private static bool initialized;

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
        if (initialized) return; // Native language reload constructs mods again.
        var harmony = new Harmony(Owner);
        try
        {
            string[] paths = Environment.GetCommandLineArgs()
                .Where(a => a.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (paths.Length != 1 || !string.Equals(Path.GetFullPath(paths[0].Substring(16)), Profile, StringComparison.OrdinalIgnoreCase)) return;
            directory = Path.Combine(Profile, "FixtureMenuObserver");
            packageRoot = content.RootDir;
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
            Guid gameModule = typeof(Mod).Assembly.ManifestModule.ModuleVersionId;
            if (gameModule != new Guid("13bee51f-e6fa-4214-a4a5-e1e7b84a41ee")
                && gameModule != new Guid("61e41735-6189-4da4-9d21-0260257b5097"))
                throw new InvalidOperationException("Unsupported fixture game.");
            MethodInfo target = AccessTools.Method("RimWorld.MainMenuDrawer:MainMenuOnGUI");
            if (target == null || !target.IsStatic || target.ReturnType != typeof(void) || target.GetParameters().Length != 0)
                throw new InvalidOperationException("Main-menu drawing method unavailable.");
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(MenuObserverMod), nameof(MenuDrawn)) { priority = Priority.Last });
            if (Environment.GetCommandLineArgs().Contains("--fixture-supplier-menu=rimthemes")) RimThemesMenuProbe.Install(harmony);
            C01StageTimings.Install(directory);
            C14BackgroundProbe.InstallAutostart(directory);
            if (CompatibilityProbe.Selected) CompatibilityProbe.Install(directory);
            if (Environment.GetCommandLineArgs().Contains("--fixture-c13-loading-probe")) C13LoadingProbe.Install(directory);
            if (Environment.GetCommandLineArgs().Contains("--fixture-functional-probes")
                && !Environment.GetCommandLineArgs().Contains("--fixture-c10-bootstrap-probe"))
            {
                if (Environment.GetCommandLineArgs().Contains("--fixture-processed-xml-observer")) ProcessedXmlQualification.Install(directory);
                else ParsedXmlQualification.Install(directory);
                MethodInfo? prepared = AccessTools.Method("WakeUp.PreparedTextureRuntime:TryResolve");
                if (prepared != null)
                    harmony.Patch(prepared, postfix: new HarmonyMethod(typeof(MenuObserverMod), nameof(PreparedResolved)));
            }
            if (Environment.GetCommandLineArgs().Contains("--fixture-residual-probe"))
                ResidualProbe.Install(harmony, target, directory);
            if (Environment.GetCommandLineArgs().Contains("--fixture-atlas-lifecycle"))
                AtlasLifecycleQualification.Install(directory);
            Log.Message("[FixtureMenuObserver] installed: first completed main-menu repaint; launchCounter="
                + startCounter.ToString(Invariant) + "; nativeCounter=" + Counter().ToString(Invariant)
                + "; monoStopwatchCounter=" + Stopwatch.GetTimestamp().ToString(Invariant));
            if (Environment.GetCommandLineArgs().Contains("--fixture-c05-texture-probe")) C05TextureProbe.StageSources(content);
            SourceSelectionProbe.Schedule(content, directory);
            initialized = true;
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
            if (Volatile.Read(ref emitted) != 0 || (!__runOriginal && !RimThemesMenuProbe.Completed) || directory == null
                || Event.current == null || Event.current.type != EventType.Repaint
                || Current.ProgramState != ProgramState.Entry || !PlayDataLoader.Loaded) return;
            long counter = Counter();
            if (Interlocked.CompareExchange(ref emitted, 1, 0) != 0) return;
            int pid;
            using (Process process = Process.GetCurrentProcess()) pid = process.Id;
            double elapsed = (counter - startCounter) / (double)counterFrequency;
            string json = "{\"schema\":\"fixture-menu-ready.v1\",\"event\":\"menu-ready\","
                + "\"definition\":\"" + (__runOriginal ? "first-completed-main-menu-repaint" : "first-completed-rimthemes-menu-repaint") + "\",\"runId\":\"" + runId
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
            C01StageTimings.Menu();
            PreviewTupleProbe.Run(directory);
            if (CompatibilityProbe.Selected) { CompatibilityProbe.Start(directory); return; }
            if (C10DemandTextureProbe.Required)
            {
                C10DemandTextureProbe.Start(directory);
                if (exitAfterMenuReady) new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>();
                return;
            }
            if (C10SerializedTextureProbe.Selected)
            {
                C10SerializedTextureProbe.Start(directory);
                if (exitAfterMenuReady) new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>();
                return;
            }
            if (C10AudioMeasurementProbe.Selected)
            {
                try
                {
                    C10AudioMeasurementProbe.Menu(directory);
                    if (C10AudioMeasurementProbe.BootstrapExpected) C10BootstrapProbe.Start(directory);
                    FixtureGameplaySmoke.Start(directory);
                }
                catch (Exception ex) { C10AudioMeasurementProbe.Fail(ex); if (exitAfterMenuReady) new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>(); }
                return;
            }
            if (C10PublicPatchProbe.Selected && !Environment.GetCommandLineArgs().Contains("--fixture-c10-bootstrap-probe"))
            {
                try { C10PublicPatchProbe.Run(); }
                catch (Exception ex) { Log.Error("[FixtureMenuObserver] C10 public patch control failed: " + ex); }
                if (exitAfterMenuReady) new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>();
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--fixture-c10-bootstrap-probe"))
            {
                if (C10NaturalAudioProbe.Selected)
                {
                    try { C10NaturalAudioProbe.Menu(directory); }
                    catch (Exception ex) { Log.Error("[FixtureMenuObserver] C10 natural audio receipt failed: " + ex); }
                }
                try { C10BootstrapProbe.Start(directory); }
                catch (Exception ex) { Log.Error("[FixtureMenuObserver] C10 bootstrap observation failed: " + ex); }
                if (Environment.GetCommandLineArgs().Contains("--fixture-c10-native-entry-probe")
                    && !Environment.GetCommandLineArgs().Any(a => a.StartsWith("--fixture-c10-prepared-audio=", StringComparison.Ordinal)))
                    C10NativeEntryProbe.Start(directory);
                if (C10Mp3CompletionProbe.Selected) C10Mp3CompletionProbe.Start(directory);
                if (C10PreparedAudioProbe.Selected && !C10PreparedAudioProbe.Gameplay)
                {
                    try { C10PreparedAudioProbe.Start(directory); }
                    catch (Exception ex) { Log.Error("[FixtureMenuObserver] C10 prepared audio probe failed: " + ex); }
                }
                if (C10PreparedAudioProbe.Gameplay || C10NaturalAudioProbe.Selected) { FixtureGameplaySmoke.Start(directory); return; }
                if (exitAfterMenuReady)
                    new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>();
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--fixture-c10-stream-probe"))
            {
                try { C10StreamProbe.Start(directory); }
                catch (Exception ex) { Log.Error("[FixtureMenuObserver] C10 stream probe failed: " + ex); }
            }
            if (Environment.GetCommandLineArgs().Contains("--fixture-functional-probes"))
            {
                try { C10NativeProbe.Menu(directory, packageRoot); }
                catch (Exception ex) { Log.Error("[FixtureMenuObserver] C10 native probe receipt failed: " + ex); }
                ParsedXmlQualification.Menu();
                ProcessedXmlQualification.Menu();
                C12NativeSearchQualification.Run(directory);
                FeatureQualification.Menu(directory);
                C09RouteQualification.Menu(directory);
                LanguageQualification.Menu(directory);
                LanguageSourceProbe.Menu(directory);
            }
            else if (C01StageTimings.Selected || Environment.GetCommandLineArgs().Contains("--fixture-language-observer"))
            {
                // Text equality is observed after the independent menu timestamp.
                // No contract decoding, source mutation or lifecycle probe here.
                LanguageQualification.Menu(directory, inspectContracts: false);
            }
            if (Environment.GetCommandLineArgs().Contains("--fixture-c05-texture-probe")) C05TextureProbe.Start(directory);
            string? qualityPhase = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--fixture-c08-quality=", StringComparison.Ordinal));
            if (qualityPhase == "--fixture-c08-quality=boundaries") { C08BoundaryProbe.RunFormats(directory); C08BoundaryProbe.Run(directory); }
            else if (qualityPhase == "--fixture-c08-quality=unity-exposure") C08BoundaryProbe.RunUnityExposure(directory);
            else if (qualityPhase?.StartsWith("--fixture-c08-quality=coverage-", StringComparison.Ordinal) == true) C08CoverageProbe.Start(directory);
            else if (qualityPhase != null && !C08InspectionScene.Selected) C08QualityProbe.Start(directory);
            if (Environment.GetCommandLineArgs().Contains("--fixture-atlas-probe")) AtlasQualification.Start(directory);
            if (Environment.GetCommandLineArgs().Contains("--fixture-atlas-lifecycle")) AtlasLifecycleQualification.Menu();
            // Only arm shutdown after the timing evidence is safely written.
            // Update runs outside this GUI callback; the delay is not loading time.
            if (LanguageQualification.StartLifecycle(directory)) return;
            if (Environment.GetCommandLineArgs().Contains("--fixture-c13-loading-probe")) { C13LoadingProbe.Start(directory); return; }
            if (C14BackgroundProbe.AutostartSelected)
            {
                if (C14BackgroundProbe.AutostartCompleted)
                    new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>();
                return;
            }
            if (C14BackgroundProbe.Required) { C14BackgroundProbe.Start(directory, C14BackgroundProbe.SelectedPhase); return; }
            if (Environment.GetCommandLineArgs().Contains("--fixture-world-background"))
                FixtureWorldBackground.Start(directory!);
            else if (Environment.GetCommandLineArgs().Contains("--fixture-gameplay-smoke"))
                FixtureGameplaySmoke.Start(directory!);
            else if (exitAfterMenuReady)
                new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>();
        }
        catch { /* Observation must not change menu behavior or its exceptions. */ }
    }

    private static void PreparedResolved(Texture2D? __result)
    {
        if (__result == null || directory == null) return;
        try { File.AppendAllText(Path.Combine(directory, "prepared-startup.jsonl"),
            "{\"nativeStartupHit\":true,\"width\":" + __result.width + ",\"height\":" + __result.height
            + ",\"mips\":" + __result.mipmapCount + ",\"format\":\"" + __result.format + "\"}\n"); }
        catch { /* Receipt failure must not replace native loader behavior. */ }
    }
}

public sealed class MenuReadyExit : MonoBehaviour
{
    private readonly long armedAt = Stopwatch.GetTimestamp();
    private bool requested;

    private void Update()
    {
        if (requested) return;
        if (C10DemandTextureProbe.Required && !C10DemandTextureProbe.Completed) return;
        if (C10SerializedTextureProbe.Selected && C10SerializedTextureProbe.Advance()) return;
        bool bootstrap = Environment.GetCommandLineArgs().Contains("--fixture-c10-bootstrap-probe");
        if (!bootstrap && Current.ProgramState == ProgramState.Entry && Current.Game == null && C08QualityProbe.Advance()) return;
        if (!bootstrap && Current.ProgramState == ProgramState.Entry && Current.Game == null && C08CoverageProbe.Advance()) return;
        if (!bootstrap && Current.ProgramState == ProgramState.Entry && Current.Game == null && AtlasQualification.Advance()) return;
        if (!bootstrap && Current.ProgramState == ProgramState.Entry && Current.Game == null && C05TextureProbe.Advance()) return;
        if (!bootstrap && !Environment.GetCommandLineArgs().Contains("--fixture-c05-texture-probe")
            && !Environment.GetCommandLineArgs().Any(a => a.StartsWith("--fixture-c08-quality=", StringComparison.Ordinal))
            && !Environment.GetCommandLineArgs().Contains("--fixture-atlas-probe")
            && !Environment.GetCommandLineArgs().Contains("--fixture-atlas-lifecycle")
            && Current.ProgramState == ProgramState.Entry && Current.Game == null && Rc2FeatureProbe.Advance()) return;
        if (Stopwatch.GetTimestamp() - armedAt < Stopwatch.Frequency) return;
        requested = true;
        // The in-game save-value predicate requires a TickManager, absent at startup.
        if (Current.ProgramState != ProgramState.Entry || Current.Game != null)
        {
            Log.Warning("[FixtureMenuObserver] automatic exit cancelled: no longer at the startup menu");
            return;
        }
        Log.Message("[FixtureMenuObserver] requesting normal shutdown after menu-ready");
        if (C10PreparedAudioProbe.Selected)
        {
            try { C10PreparedAudioProbe.BeforeExit(); }
            catch (Exception ex) { Log.Error("[FixtureMenuObserver] C10 prepared audio cleanup failed: " + ex); }
        }
        if (!bootstrap)
        {
            try { C10StreamProbe.BeforeExit(); }
            catch (Exception ex) { Log.Error("[FixtureMenuObserver] C10 stream cleanup failed: " + ex); }
            C05TextureProbe.BeforeExit();
            C08QualityProbe.BeforeExit();
            C08CoverageProbe.BeforeExit();
            Rc2FeatureProbe.BeforeExit();
        }
        C10ProductShutdownProbe.BeforeRootShutdown();
        try { Root.Shutdown(); } // The same path as the main menu's Quit to OS button.
        finally { C10ProductShutdownProbe.AfterRootShutdown(); }
    }
}
