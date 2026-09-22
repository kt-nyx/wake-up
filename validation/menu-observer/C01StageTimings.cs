// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using HarmonyLib;
using Verse;

namespace FixtureMenuObserver;

// Private measurement of the existing product wrappers, not native XML hooks.
// C01/C02/C03 guards do not target these outer methods. Nothing is skipped,
// replaced, or admitted differently; finalizers leave native exceptions intact.
internal static class C01StageTimings
{
    private const string Owner = "local.fixture.c01-stage-timings";
    private static readonly object Gate = new();
    private static readonly Dictionary<MethodBase, Record> Records = new();
    private static string? directory;
    private static string? installationError;
    private static bool selected, emitted, overlap;
    internal static bool Selected => selected;
    private static int active;
    private sealed class Record
    {
        internal string Name = "";
        internal long Ticks;
        internal int Calls, Completed, Failed;
    }
    private sealed class State
    {
        internal Record Record = null!;
        internal long Start;
    }

    internal static void Install(string outputDirectory)
    {
        var harmony = new Harmony(Owner);
        try
        {
            var runtime = AccessTools.TypeByName("WakeUp.ParsedXmlRuntime");
            if (runtime == null) return;
            // Read the actual ordinary setting without constructing Wake-Up or
            // its settings before their native turn. Both comparison arms use it.
            var owner = LoadedModManager.RunningModsListForReading.Single(m => m.assemblies.loadedAssemblies.Contains(runtime.Assembly));
            string file = Path.Combine(GenFilePaths.ConfigFolderPath,
                "Mod_" + new DirectoryInfo(owner.RootDir).Name + "_WakeUpMod.xml");
            if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024) return;
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var document = new XmlDocument { XmlResolver = null }; document.Load(reader);
            var settings = document.SelectNodes("/SettingsBlock/ModSettings[@Class='WakeUp.WakeUpSettings']");
            if (settings == null || settings.Count != 1) return;
            var enabled = settings[0]!.SelectNodes("enabled");
            var timing = settings[0]!.SelectNodes("loadingTimings");
            if (enabled == null || enabled.Count > 1 || enabled.Count == 1 && !True(enabled[0]!.InnerText)
                || timing == null || timing.Count != 1 || !True(timing[0]!.InnerText)) return;
            selected = true; directory = outputDirectory;
            var names = new[] { "Initialize", "LoadSources", "Combine", "Finish" };
            foreach (string name in names)
            {
                var method = runtime.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Single(m => m.Name == name);
                Records.Add(method, new Record { Name = name });
            }
            var shared = runtime.Assembly.GetType("WakeUp.SharedCacheBudget", true)!;
            var sharedInitialize = shared.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(m => m.Name == "Initialize");
            Records.Add(sharedInitialize, new Record { Name = "SharedCacheInitialize" });
            foreach (var boundary in new[] {
                (Type: "WakeUp.ProcessedXmlRuntime", Method: "Apply", Name: "ProcessedApply"),
                (Type: "WakeUp.ResolvedInheritanceRuntime", Method: "Resolve", Name: "InheritanceResolve"),
                (Type: "WakeUp.ParsedLanguageRuntime", Method: "Context", Name: "LanguageContext"),
                (Type: "WakeUp.ParsedLanguageRuntime", Method: "Key", Name: "LanguageSourceKey"),
                (Type: "WakeUp.ParsedLanguageRuntime", Method: "Initialize", Name: "LanguageInitialize"),
                (Type: "WakeUp.ParsedLanguageRuntime", Method: "OpenStore", Name: "LanguageOpenStore"),
                (Type: "WakeUp.ParsedLanguageRuntime", Method: "Begin", Name: "LanguageBegin"),
                (Type: "WakeUp.ParsedLanguageRuntime", Method: "End", Name: "LanguageEnd"),
                (Type: "WakeUp.ParsedLanguageRuntime", Method: "Admitted", Name: "LanguageGuard"),
                (Type: "WakeUp.ParsedLanguageFormat", Method: "DecodeInstructions", Name: "LanguageInstructionDecode"),
                (Type: "WakeUp.ParsedLanguageRuntime", Method: "Publish", Name: "LanguagePublish") })
            {
                var type = runtime.Assembly.GetType(boundary.Type, true)!;
                var method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Single(m => m.Name == boundary.Method);
                Records.Add(method, new Record { Name = boundary.Name });
            }
            foreach (var method in Records.Keys)
            {
                installationError = "Installing " + Records[method].Name;
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(C01StageTimings), nameof(Before)),
                    finalizer: new HarmonyMethod(typeof(C01StageTimings), nameof(After)));
            }
            installationError = null;
        }
        catch (Exception e)
        {
            installationError += ": " + e.GetType().Name;
            try { File.WriteAllText(Path.Combine(outputDirectory, "stage-timing-installation-error.txt"), installationError + "\n" + e, new UTF8Encoding(false)); } catch { }
            try { harmony.UnpatchAll(Owner); } catch { }
        }
    }

    private static bool True(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    private static void Before(MethodBase __originalMethod, out State? __state)
    {
        __state = null;
        try
        {
            lock (Gate)
            {
                if (emitted || !Records.TryGetValue(__originalMethod, out var record)) return;
                record.Calls++;
                if (active++ != 0) overlap = true;
                __state = new State { Record = record, Start = Stopwatch.GetTimestamp() };
            }
        }
        catch { }
    }
    private static void After(State? __state, Exception? __exception)
    {
        if (__state == null) return;
        try
        {
            long elapsed = Stopwatch.GetTimestamp() - __state.Start;
            lock (Gate)
            {
                __state.Record.Ticks += Math.Max(0, elapsed);
                __state.Record.Completed++;
                if (__exception != null) __state.Record.Failed++;
                active--;
            }
        }
        catch { }
    }

    internal static void Menu()
    {
        try
        {
            lock (Gate)
            {
                if (!selected || emitted || directory == null) return;
                emitted = true;
                string rows = string.Join(",", Records.Values.Select(r => "{\"stage\":\"" + r.Name
                    + "\",\"calls\":" + r.Calls + ",\"completed\":" + r.Completed + ",\"failedCalls\":" + r.Failed
                    + ",\"milliseconds\":" + (r.Ticks * 1000d / Stopwatch.Frequency).ToString("F6", CultureInfo.InvariantCulture) + "}"));
                File.WriteAllText(Path.Combine(directory, "c01-stage-timings.json"),
                    "{\"schema\":\"fixture-c01-stage-timings.v1\",\"selected\":true,\"installationFailed\":"
                    + (installationError != null ? "true" : "false") + ",\"failed\":"
                    + (installationError != null || active != 0 || Records.Values.Any(r => r.Failed != 0) ? "true" : "false")
                    + ",\"unfinished\":" + active + ",\"overlapObserved\":" + (overlap ? "true" : "false")
                    + ",\"stages\":[" + rows + "]}\n", new UTF8Encoding(false));
            }
        }
        catch { }
    }
}
