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
using HarmonyLib;
using Verse;

namespace WakeUp;

// Small opt-in report of real synchronous boundaries. XmlAssetsInModFolder joins
// its native workers before return. LoadDefs is an iterator and ReloadContent
// queues later work: neither is timed as if its return measured that work.
internal static class LoadingTimingRuntime
{
    internal const string Owner = "wakeup.loading-timings";
    internal static readonly MethodInfo[] Targets = {
        AccessTools.Method(typeof(DirectXmlLoader), "XmlAssetsInModFolder"),
        AccessTools.Method(typeof(ModContentPack), "LoadPatches") };
    internal static readonly string[] Bodies = {
        "D9180C3AB1A05EF1BC089D12F0CA9C7DCC0C2D49FAAC715DE4B4C93D02CE88C4",
        "F6426B53BCF011E88AE92669EC9168ACEB1D3129B69DC4E583B77E4600D3C7EE" };
    internal static string Status => LoadingObservationRuntime.Status;
    internal static string Report => LoadingObservationRuntime.Latest?.RenderReport() ?? "No loading session was recorded this launch.";
    internal static string Attribution => LoadingObservationRuntime.Current?.Snapshot().Work ?? "";
    internal static bool Selected(IReadOnlyList<string> args) => StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
        && args.Count(a => a.StartsWith("--wake-up-loading-timings=", StringComparison.Ordinal)) == 1
        && args.Contains("--wake-up-loading-timings=on");
    internal static void TryInitialize(string[] args)
    {
        LoadingObservationRuntime.Attach(args);
        if (!Selected(args) && !LoadingDisplayRuntime.Selected(args)) return;
        var harmony = new Harmony(Owner);
        try { Install(harmony); }
        catch { harmony.UnpatchAll(Owner); }
    }
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(Targets[0], prefix: new HarmonyMethod(typeof(LoadingTimingRuntime), nameof(BeforeFiles)),
            finalizer: new HarmonyMethod(typeof(LoadingTimingRuntime), nameof(After)));
        harmony.Patch(Targets[1], prefix: new HarmonyMethod(typeof(LoadingTimingRuntime), nameof(BeforePatches)),
            finalizer: new HarmonyMethod(typeof(LoadingTimingRuntime), nameof(After)));
    }
    internal static void BeforeFiles(ModContentPack mod, string folderPath, out LoadingSession.Token? __state)
    {
        __state = null;
        try { __state = LoadingObservationRuntime.Begin("XML read", folderPath, mod.PackageId); } catch { }
    }
    internal static void BeforePatches(ModContentPack __instance, out LoadingSession.Token? __state)
    {
        __state = null;
        try { __state = LoadingObservationRuntime.Begin("XML patches", "Construct patch objects", __instance.PackageId); } catch { }
    }
    internal static void After(LoadingSession.Token? __state, Exception? __exception) => LoadingObservationRuntime.End(__state, __exception);
    internal static void Menu() { }
    internal static void BeginForTests(LoadingTimingReport value) { }
}
internal sealed class LoadingTimingReport
{
    internal const int Limit = 512;
    private readonly object gate = new();
    private readonly List<Token> records = new();
    private readonly List<Token> pending = new();
    private readonly Func<double> clock;
    private readonly double origin;
    private int omitted;
    private bool active = true;
    internal int Completed { get { lock (gate) return records.Count; } }
    internal string Current { get { lock (gate) return pending.Count == 0 ? "" : pending[pending.Count - 1].Package + " · " + pending[pending.Count - 1].Stage; } }
    internal sealed class Token
    {
        internal readonly LoadingTimingReport Owner;
        internal readonly string Package, Stage;
        internal readonly double Start;
        internal readonly int Depth, Thread;
        internal double Duration;
        internal string Outcome = "";
        internal Token(LoadingTimingReport owner, string package, string stage, double start, int depth, int thread)
        { Owner = owner; Package = Plain(package, 120); Stage = Plain(stage, 120); Start = start; Depth = depth; Thread = thread; }
    }
    internal LoadingTimingReport(Func<double>? clock = null)
    { this.clock = clock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency); origin = this.clock(); }
    internal Token? Begin(string package, string stage)
    {
        lock (gate)
        {
            if (!active) return null;
            if (records.Count + pending.Count >= Limit || pending.Count >= 32) { omitted++; return null; }
            int thread = Environment.CurrentManagedThreadId;
            var token = new Token(this, package, stage, Math.Max(0, clock() - origin), pending.Count(t => t.Thread == thread), thread);
            pending.Add(token);
            return token;
        }
    }
    internal void End(Token token, Exception? exception)
    {
        lock (gate)
        {
            if (!active || !pending.Remove(token)) return;
            token.Duration = Math.Max(0, clock() - origin - token.Start);
            token.Outcome = exception == null ? "returned (native caught errors may remain)" : "threw " + exception.GetType().Name;
            records.Add(token);
        }
    }
    internal string Finish(string reason)
    {
        lock (gate)
        {
            active = false;
            var text = new StringBuilder("Wake-Up: observed per-mod XML loading calls\n\n");
            text.AppendLine(reason).AppendLine("Times start after Wake-Up installs its observers; earlier work is absent.");
            text.AppendLine("XML files: folder discovery, native parallel reading/parsing and worker join. Patch construction INCLUDES its nested XML-files call.");
            text.AppendLine("Durations include nested work and may overlap across threads. DO NOT SUM them as startup or per-mod wall time.");
            text.AppendLine("Not covered: patch application, inheritance, definition construction, mod/static constructors, textures, audio, saves or complete startup.");
            text.AppendLine("No percentage, estimated completion or speedup is inferred. Mod identifiers are included only because this report was selected.");
            text.Append("Omitted calls: ").Append(omitted).Append("; unfinished calls at stop: ").Append(pending.Count).AppendLine(".");
            text.AppendLine("\nStart s | Duration ms | Thread | Nesting | Mod package | Boundary | Outcome");
            foreach (var row in records.OrderBy(r => r.Start))
                text.Append(row.Start.ToString("F3", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append((row.Duration * 1000).ToString("F3", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(row.Thread).Append(" | ").Append(row.Depth).Append(" | ").Append(row.Package).Append(" | ")
                    .Append(row.Stage).Append(" | ").AppendLine(row.Outcome);
            pending.Clear();
            return text.ToString();
        }
    }
    private static string Plain(string value, int limit) => (value.Length > limit ? value.Substring(0, limit) : value)
        .Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace('|', '/');
}
