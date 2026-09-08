// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using HarmonyLib;
using Verse;

namespace RimWorldLoadingOptimizer.RimWorld;

internal static class DefLookupRuntime
{
    private const string Owner = "rimworldloadingoptimizer.def-lookup";
    private static readonly MethodInfo NativeNodes = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectNodes), new[] { typeof(string) })!;
    private static readonly MethodInfo NativeSingle = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectSingleNode), new[] { typeof(string) })!;
    private static readonly List<MethodBase> Targets = new();
    private static readonly List<PublishedPatchGuard> Guards = new();
    private static bool attempted;
    private static bool installed;
    private static bool candidate;
    private static bool consumed;
    private static string? evidencePath;
    [ThreadStatic] private static ScopedDefLookup? active;

    internal static bool TryInitialize(IReadOnlyList<string> arguments)
    {
        if (!arguments.Any(a => a != null && a.StartsWith("--rlo-strategy=def-lookup", StringComparison.Ordinal))) return false;
        if (attempted) return true;
        attempted = true;
        var harmony = new Harmony(Owner);
        try
        {
            string[] strategies = arguments.Where(a => a != null && a.StartsWith("--rlo-strategy=", StringComparison.Ordinal)).ToArray();
            StartupLaunchDecision mode = StartupLaunchSelector.Parse(arguments);
            if (strategies.Length != 1 || strategies[0] != "--rlo-strategy=def-lookup" || mode.Selection == StartupSelection.Off || PlayDataLoader.Loaded)
            { Receipt("refused", "selector-or-startup-window"); return true; }
            evidencePath = Path.Combine(mode.SaveDataRoot!, "RimWorldLoadingOptimizer", "def-lookup.jsonl");
            candidate = mode.Selection == StartupSelection.Candidate;
            if (!ReferenceEquals(GameBuildContract.Current, GameBuildContract.GogRev573)
                || !RuntimeIdentity.ValidateBinaryIdentity(out _))
            { Receipt("refused", "pinned-binary-identity"); return true; }
            Assembly game = typeof(LoadedModManager).Assembly;
            if (candidate)
            {
                foreach (string name in new[] { "Add", "AddModExtension", "Insert", "Remove", "Replace", "SetName",
                    "AttributeAdd", "AttributeRemove", "AttributeSet", "Test", "Conditional" })
                    InstallWorker(harmony, game.GetType("Verse.PatchOperation" + name, true)!);
                if (Targets.Count == 0) { Receipt("refused", "no-compatible-query-workers"); return true; }
            }
            harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "ApplyPatches"),
                prefix: new HarmonyMethod(typeof(DefLookupRuntime), nameof(StagePrefix)) { priority = Priority.Last },
                finalizer: new HarmonyMethod(typeof(DefLookupRuntime), nameof(StageFinalizer)));
            installed = true;
            Receipt("installed", candidate ? "literal-def-and-template-name-lookup" : "baseline-stage-timer",
                "\"optimizationEnabled\":" + (candidate ? "true" : "false") + ",\"patchedMethods\":" + Targets.Count);
        }
        catch (Exception exception)
        {
            try { harmony.UnpatchAll(Owner); } catch { }
            installed = false;
            Receipt("refused", "installation-" + exception.GetType().Name);
        }
        return true;
    }

    private static void InstallWorker(Harmony harmony, Type type)
    {
        MethodInfo? worker = type.GetMethod("ApplyWorker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            null, new[] { typeof(XmlDocument) }, null);
        if (worker == null || worker.ReturnType != typeof(bool) || Harmony.GetPatchInfo(worker)?.Transpilers.Any(p => p.owner != Owner) == true) return;
        // Recheck published Harmony state without deserializing unchanged records.
        if (!PublishedPatchGuard.TryCreate(worker, Owner, out PublishedPatchGuard? guard)) return;
        Targets.Add(worker); Guards.Add(guard!);
        harmony.Patch(worker, transpiler: new HarmonyMethod(typeof(DefLookupRuntime), nameof(Transpiler)) { priority = Priority.Last });
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int target = Targets.IndexOf(__originalMethod);
        if (target < 0) throw new InvalidOperationException("Def lookup worker is not registered.");
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (Harmony.GetPatchInfo(__originalMethod)?.Transpilers.Any(p => p.owner != Owner) == true) return code;
        int replacements = 0;
        for (int index = 0; index < code.Count; index++)
        {
            CodeInstruction instruction = code[index];
            if (instruction.opcode != OpCodes.Callvirt || (!Equals(instruction.operand, NativeNodes) && !Equals(instruction.operand, NativeSingle))) continue;
            bool single = Equals(instruction.operand, NativeSingle);
            var argument = new CodeInstruction(OpCodes.Ldc_I4, target);
            argument.labels.AddRange(instruction.labels); instruction.labels.Clear();
            argument.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
            code.Insert(index++, argument);
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(DefLookupRuntime), single ? nameof(SelectSingleNode) : nameof(SelectNodes));
            replacements++;
        }
        if (replacements != 1) throw new InvalidOperationException("Unique native worker XPath callsite unavailable.");
        return code;
    }

    private static XmlNodeList SelectNodes(XmlNode node, string xpath, int worker)
        => active != null && Guards[worker].AllowsOriginalContract() ? active.SelectNodes(node, xpath) : node.SelectNodes(xpath)!;

    private static XmlNode? SelectSingleNode(XmlNode node, string xpath, int worker)
        => active != null && Guards[worker].AllowsOriginalContract() ? active.SelectSingleNode(node, xpath) : node.SelectSingleNode(xpath);

    private static void StagePrefix(XmlDocument __0, bool __runOriginal, out StageState? __state)
    {
        __state = null;
        if (!installed || consumed || !__runOriginal) return;
        consumed = true;
        __state = new StageState();
        if (candidate) active = __state.Lookup = new ScopedDefLookup(__0);
    }

    private static Exception? StageFinalizer(Exception? __exception, StageState? __state)
    {
        if (__state == null) return __exception;
        __state.Watch.Stop();
        ScopedDefLookup? lookup = __state.Lookup;
        active = null;
        try
        {
            Receipt("patches-complete", __exception == null ? "original-completed" : "original-exception",
                "\"elapsedMs\":" + __state.Watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
                + ",\"hits\":" + (lookup?.Hits ?? 0) + ",\"fallbacks\":" + (lookup?.Fallbacks ?? 0)
                + ",\"attributeHits\":" + (lookup?.AttributeHits ?? 0)
                + ",\"indexRebuilds\":" + (lookup?.Rebuilds ?? 0) + ",\"invalidations\":" + (lookup?.Invalidations ?? 0)
                + ",\"peakEntries\":" + (lookup?.PeakEntries ?? 0) + ",\"queries\":" + (lookup?.QueryCount ?? 0)
                + ",\"patchInfoReads\":" + Guards.Sum(g => g.PatchInfoReads));
        }
        finally { lookup?.Dispose(); }
        return __exception;
    }

    private sealed class StageState
    {
        internal readonly Stopwatch Watch = Stopwatch.StartNew();
        internal ScopedDefLookup? Lookup;
    }

    private static void Receipt(string kind, string reason, string? fields = null)
    {
        try
        {
            if (evidencePath == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            File.AppendAllText(evidencePath, "{\"event\":\"" + kind + "\",\"reason\":\"" + reason + "\""
                + (fields == null ? "" : "," + fields) + "}" + Environment.NewLine);
        }
        catch { }
    }
}
