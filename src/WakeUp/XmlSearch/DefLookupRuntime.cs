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

namespace WakeUp;

internal static class DefLookupRuntime
{
    private const string Owner = "wakeup.def-lookup";
    private static readonly MethodInfo NativeNodes = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectNodes), new[] { typeof(string) })!;
    private static readonly MethodInfo NativeSingle = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectSingleNode), new[] { typeof(string) })!;
    private static readonly List<MethodBase> Targets = new();
    private static readonly List<PublishedPatchGuard> Guards = new();
    private static readonly HashSet<MethodBase> YieldedWorkers = new();
    private static readonly List<PublishedPatchGuard> SingleGuards = new();
    private static bool attempted;
    private static bool installed;
    private static bool candidate;
    private static bool consumed;
    private static bool queryExtensions;
    private static bool singleMemoInstalled;
    private static string? evidencePath;
    [ThreadStatic] private static ScopedDefLookup? active;
    [ThreadStatic] private static ScopedSingleQueryMemo? singleMemo;
    [ThreadStatic] private static bool selectingIndexedSingle;
    private static long customSingleIndexHits;
    private static long eagerCustomIndexHits;

    internal static bool TryInitialize(IReadOnlyList<string> arguments)
    {
        if (!arguments.Any(a => a != null && a.StartsWith("--wake-up-strategy=def-lookup", StringComparison.Ordinal)))
            return false;
        if (attempted)
            return true;
        attempted = true;
        var harmony = new Harmony(Owner);
        try
        {
            string[] strategies = arguments.Where(a => a != null && a.StartsWith("--wake-up-strategy=", StringComparison.Ordinal)).ToArray();
            StartupLaunchDecision mode = StartupLaunchSelector.Parse(arguments);
            if (strategies.Length != 1 || strategies[0] != "--wake-up-strategy=def-lookup" || mode.Selection == StartupSelection.Off || PlayDataLoader.Loaded)
            {
                Receipt("refused", "selector-or-startup-window");
                return true;
            }
            evidencePath = Path.Combine(mode.SaveDataRoot!, "WakeUp", "def-lookup.jsonl");
            candidate = mode.Selection == StartupSelection.Candidate;
            queryExtensions = candidate && arguments.Contains("--wake-up-query-extensions=on");
            if (candidate && LoaderSupplierPolicy.YieldXml)
            {
                LoaderSupplierPolicy.RefuseXml("definitions", "single-query", "query-plans");
                if (ExtendedXmlQueryRuntime.SupplierPresent) LoaderSupplierPolicy.RefuseXml("extended-query"); else CompatibilityStatus.Absent("extended-query");
                return true;
            }
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason))
            {
                foreach (string id in new[] { "definitions", "single-query", "query-plans" }) CompatibilityStatus.Refuse(id, reason);
                if (ExtendedXmlQueryRuntime.SupplierPresent) CompatibilityStatus.Refuse("extended-query", reason); else CompatibilityStatus.Absent("extended-query");
                Receipt("refused", reason);
                return true;
            }
            Assembly game = typeof(LoadedModManager).Assembly;
            if (candidate)
            {
                if (!arguments.Contains("--wake-up-user-def-search=off"))
                foreach (string name in new[] { "Add", "AddModExtension", "Insert", "Remove", "Replace", "SetName",
                    "AttributeAdd", "AttributeRemove", "AttributeSet", "Test", "Conditional" })
                    InstallWorker(harmony, game.GetType("Verse.PatchOperation" + name, true)!);
                if (Targets.Count == 0) CompatibilityStatus.Refuse("definitions", "No compatible native query workers.");
                else CompatibilityStatus.Available("definitions", Targets.Count + " native workers admitted; individual refusals are listed separately.");
                if (queryExtensions)
                {
                    bool queryGuards = PrepareQueryGuards();
                    singleMemoInstalled = queryGuards && InstallSingleMemo(harmony);
                    if (singleMemoInstalled) CompatibilityStatus.Available("single-query");
                    else CompatibilityStatus.Refuse("single-query", "Required XML query hooks or native query contract are unavailable.");
                    bool eager = queryGuards && ExtendedXmlQueryRuntime.Install(SelectEagerCustom);
                    if (eager) CompatibilityStatus.Available("extended-query");
                    else if (!ExtendedXmlQueryRuntime.SupplierPresent) CompatibilityStatus.Absent("extended-query");
                    else CompatibilityStatus.Refuse("extended-query", queryGuards ? ExtendedXmlQueryRuntime.Reason : "Required XML query contract unavailable.");
                    bool plans = XPathPlanRuntime.Install(harmony);
                    if (plans) CompatibilityStatus.Available("query-plans");
                    else CompatibilityStatus.Refuse("query-plans", "Required XPath syntax or query-library hooks are unavailable.");
                    queryExtensions = singleMemoInstalled || plans || eager;
                }
            }
            if (candidate && Targets.Count == 0 && !queryExtensions) return true;
            harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "ApplyPatches"),
                prefix: new HarmonyMethod(typeof(DefLookupRuntime), nameof(StagePrefix)) { priority = Priority.Last },
                finalizer: new HarmonyMethod(typeof(DefLookupRuntime), nameof(StageFinalizer)));
            installed = true;
            CompatibilityStatus.Registry?.SummarizeChildren("definitions");
            Receipt("installed", candidate ? "literal-def-and-template-name-lookup" : "baseline-stage-timer",
                "\"optimizationEnabled\":" + (candidate ? "true" : "false") + ",\"patchedMethods\":" + Targets.Count
                + ",\"queryExtensions\":" + (queryExtensions ? "true" : "false")
                + ",\"extendedXmlQueries\":" + (ExtendedXmlQueryRuntime.Installed ? "true" : "false")
                + ",\"extendedXmlReason\":\"" + ExtendedXmlQueryRuntime.Reason + "\""
                + ",\"queryPlans\":" + (XPathPlanRuntime.Installed ? "true" : "false"));
        }
        catch (Exception exception)
        {
            try
            {
                harmony.UnpatchAll(Owner);
            }
            catch { }
            installed = false;
            CompatibilityStatus.Registry?.RefuseChildren("definitions", "Shared XML stage setup failed.");
            singleMemoInstalled = false;
            XPathPlanRuntime.Reset();
            ExtendedXmlQueryRuntime.Uninstall();
            foreach (string id in new[] { "definitions", "single-query", "query-plans" })
                CompatibilityStatus.Refuse(id, "Shared XML stage setup failed: " + exception.GetType().Name);
            if (ExtendedXmlQueryRuntime.SupplierPresent) CompatibilityStatus.Refuse("extended-query", "Shared XML stage setup failed."); else CompatibilityStatus.Absent("extended-query");
            Receipt("refused", "installation-" + exception.GetType().Name);
        }
        return true;
    }

    private static bool PrepareQueryGuards()
    {
        SingleGuards.Clear();
        try
        {
            if (!XPathPlanRuntime.NativeStringWrapper()) return false;
            // A hit skips navigator creation, compilation, evaluation and
            // first-node consumption. Guard them independently of plan-cache
            // installation, at both the read and the later capture boundary.
            Type list = typeof(XmlNode).Assembly.GetType("System.Xml.XPathNodeList", true)!;
            foreach (MethodInfo method in new[] { NativeSingle, NativeNodes,
                typeof(XmlNode).GetMethod(nameof(XmlNode.CreateNavigator), Type.EmptyTypes)!,
                typeof(XmlDocument).GetMethod(nameof(XmlDocument.CreateNavigator), Type.EmptyTypes)!,
                AccessTools.Method(typeof(XmlDocument), nameof(XmlDocument.CreateNavigator), new[] { typeof(XmlNode) }),
                AccessTools.Method(list, "Item", new[] { typeof(int) }), AccessTools.Method(list, "ReadUntil") }
                .Concat(XPathQueryContract.Methods).Distinct())
            {
                if (!PublishedPatchGuard.TryCreate(method, Owner, out PublishedPatchGuard? guard, allPatchKinds: true)
                    || !guard!.AllowsOriginalContract())
                {
                    Receipt("query-extensions-refused", "foreign-query-observer");
                    SingleGuards.Clear();
                    return false;
                }
                SingleGuards.Add(guard);
            }
            return true;
        }
        catch { SingleGuards.Clear(); return false; }
    }
    private static bool InstallSingleMemo(Harmony harmony)
    {
        try
        {
            harmony.Patch(NativeSingle,
                prefix: new HarmonyMethod(typeof(DefLookupRuntime), nameof(SinglePrefix)) { priority = Priority.Last },
                postfix: new HarmonyMethod(typeof(DefLookupRuntime), nameof(SinglePostfix)) { priority = Priority.Last });
            return true;
        }
        catch (Exception exception)
        {
            harmony.Unpatch(NativeSingle, HarmonyPatchType.All, Owner);
            Receipt("query-extensions-refused", "installation-" + exception.GetType().Name);
            return false;
        }
    }

    private static bool SinglePrefix(XmlNode __instance, string xpath, bool __runOriginal, ref XmlNode? __result, out SingleState? __state)
    {
        __state = null;
        ScopedSingleQueryMemo? memo = singleMemo;
        if (!__runOriginal || memo == null || selectingIndexedSingle || !CompatibilityStatus.Guard("single-query", SingleGuards.All(g => g.AllowsOriginalContract())))
            return true;
        if (memo.TryRead(__instance, xpath, out XmlNode? result, out long version))
        {
            __result = result;
            return false;
        }
        if (version >= 0)
        {
            // The complete native first-node operation has the same guards as
            // memo reuse. Custom patch callers can use the literal index too;
            // their lazy SelectNodes lists are deliberately left native.
            selectingIndexedSingle = true;
            try
            {
                if (active?.TrySelectSingleNode(__instance, xpath, out result) == true)
                {
                    customSingleIndexHits++;
                    __result = result;
                    memo.Capture(__instance, xpath, version, result);
                    return false;
                }
            }
            finally { selectingIndexedSingle = false; }
            __state = new SingleState(memo, __instance, xpath, version);
        }
        return true;
    }

    private static void SinglePostfix(XmlNode? __result, bool __runOriginal, SingleState? __state)
    {
        if (__runOriginal && __state != null && SingleGuards.All(g => g.AllowsOriginalContract()))
            __state.Memo.Capture(__state.Context, __state.XPath, __state.Version, __result);
    }

    private static XmlNodeList? SelectEagerCustom(XmlNode context, string xpath)
    {
        if (active == null || !CompatibilityStatus.Guard("extended-query", SingleGuards.All(g => g.AllowsOriginalContract()))) return null;
        if (!active.TrySelectNodesEager(context, xpath, out XmlNodeList? result)) return null;
        eagerCustomIndexHits++;
        return result;
    }

    internal static bool OwnsXmlObserver(Delegate observer)
        => active?.OwnsObserver(observer) == true || singleMemo?.OwnsObserver(observer) == true;

    private sealed class SingleState
    {
        internal readonly ScopedSingleQueryMemo Memo;
        internal readonly XmlNode Context;
        internal readonly string XPath;
        internal readonly long Version;
        internal SingleState(ScopedSingleQueryMemo memo, XmlNode context, string xpath, long version)
        { Memo = memo; Context = context; XPath = xpath; Version = version; }
    }

    private static void InstallWorker(Harmony harmony, Type type)
    {
        string id = "definitions/" + type.Name;
        CompatibilityStatus.Declare(id, "Definition query: " + type.Name);
        MethodInfo? worker = type.GetMethod("ApplyWorker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            null, new[] { typeof(XmlDocument) }, null);
        int index = Targets.Count;
        bool patchAttempted = false;
        try
        {
            if (worker == null || worker.ReturnType != typeof(bool) || Harmony.GetPatchInfo(worker)?.Transpilers.Any(p => p.owner != Owner) == true
                || !PublishedPatchGuard.TryCreate(worker, Owner, out PublishedPatchGuard? guard) || !guard!.AllowsOriginalContract())
                throw new InvalidOperationException("Native worker query is occupied or unsupported.");
            var native = PatchProcessor.GetOriginalInstructions(worker);
            if (native.Count(IsNativeQuery) != 1)
                throw new InvalidOperationException("Unique native worker XPath callsite unavailable.");
            bool single = native.Any(i => Equals(i.operand, NativeSingle));
            if (single && !SingleWorkerQueryPolicy.CanInstall()) throw new InvalidOperationException("A downstream single-result query observer requires the original expression and context.");
            Targets.Add(worker); Guards.Add(guard);
            patchAttempted = true;
            harmony.Patch(worker, transpiler: new HarmonyMethod(typeof(DefLookupRuntime), nameof(Transpiler)) { priority = Priority.Last });
            if (!YieldedWorkers.Contains(worker)) CompatibilityStatus.Available(id);
        }
        catch (Exception error)
        {
            // Harmony rebuilds the method even when this owner has no patch.
            // An admission refusal must leave the supplier's publication alone.
            if (patchAttempted) harmony.Unpatch(worker!, HarmonyPatchType.Transpiler, Owner);
            // Only the just-appended slot can be removed: preceding indices are in IL.
            if (Targets.Count > index) { Targets.RemoveAt(index); Guards.RemoveAt(index); }
            if (worker != null) YieldedWorkers.Remove(worker);
            CompatibilityStatus.Refuse(id, error.Message, "required-contract",
                LoaderSupplierPolicy.ProviderFor(worker == null ? new MethodBase[] { NativeSingle } : new MethodBase[] { worker, NativeSingle }));
        }
    }

    private static bool IsNativeQuery(CodeInstruction instruction)
        => instruction.opcode == OpCodes.Callvirt && (Equals(instruction.operand, NativeNodes) || Equals(instruction.operand, NativeSingle));

    private static void YieldWorker(MethodBase worker)
    {
        if (!YieldedWorkers.Add(worker)) return;
        CompatibilityStatus.Refuse("definitions/" + worker.DeclaringType!.Name,
            "This worker changed while loading patches were being rebuilt. Wake-Up leaves its incoming implementation unchanged for this launch.",
            "worker-rewritten", "Other loading mod");
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int target = Targets.IndexOf(__originalMethod);
        if (target < 0)
            throw new InvalidOperationException("Def lookup worker is not registered.");
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        // Harmony publishes a new patch record only AFTER rebuilding its wrapper.
        // An earlier transpiler can already have changed this incoming body while
        // GetPatchInfo still reports only our previous publication. Inspect the
        // complete incoming body before modifying any instruction or metadata.
        if (YieldedWorkers.Contains(__originalMethod)) return code;
        if (Harmony.GetPatchInfo(__originalMethod)?.Transpilers.Any(p => p.owner != Owner) == true
            || code.Count(IsNativeQuery) != 1
            || !InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(__originalMethod)))
        { YieldWorker(__originalMethod); return code; }
        int replacements = 0;
        for (int index = 0; index < code.Count; index++)
        {
            CodeInstruction instruction = code[index];
            if (!IsNativeQuery(instruction))
                continue;
            bool single = Equals(instruction.operand, NativeSingle);
            var argument = new CodeInstruction(OpCodes.Ldc_I4, target);
            argument.labels.AddRange(instruction.labels);
            instruction.labels.Clear();
            argument.blocks.AddRange(instruction.blocks);
            instruction.blocks.Clear();
            code.Insert(index++, argument);
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(DefLookupRuntime), single ? nameof(SelectSingleNode) : nameof(SelectNodes));
            replacements++;
        }
        if (replacements != 1)
            throw new InvalidOperationException("Unique native worker XPath callsite unavailable.");
        return code;
    }

    private static bool WorkerAllowed(int worker)
    {
        if (YieldedWorkers.Contains(Targets[worker])) return false;
        if (Guards[worker].AllowsOriginalContract()) return true;
        return CompatibilityStatus.Guard("definitions/" + Targets[worker].DeclaringType!.Name, false);
    }
    private static XmlNodeList SelectNodes(XmlNode node, string xpath, int worker)
        => active != null && WorkerAllowed(worker) ? active.SelectNodes(node, xpath) : node.SelectNodes(xpath)!;

    private static XmlNode? SelectSingleNode(XmlNode node, string xpath, int worker)
    {
        if (active == null || !WorkerAllowed(worker)) return node.SelectSingleNode(xpath);
        if (SingleWorkerQueryPolicy.CanUse()) return active.SelectSingleNode(node, xpath);
        CompatibilityStatus.Refuse("definitions/" + Targets[worker].DeclaringType!.Name,
            "The single-result query observer or its patch-stage scope changed; original query context retained.", "single-query-observer");
        return node.SelectSingleNode(xpath);
    }

    private static void StagePrefix(XmlDocument __0, bool __runOriginal, out StageState? __state)
    {
        __state = null;
        if (!installed || consumed || !__runOriginal)
            return;
        consumed = true;
        __state = new StageState();
        if (candidate && (Targets.Count != 0 || singleMemoInstalled || ExtendedXmlQueryRuntime.Installed))
            active = __state.Lookup = new ScopedDefLookup(__0);
        if (singleMemoInstalled)
        {
            double before = __state.Watch.Elapsed.TotalMilliseconds;
            singleMemo = __state.SingleMemo = new ScopedSingleQueryMemo(__0);
            __state.SingleSetupMilliseconds = __state.Watch.Elapsed.TotalMilliseconds - before;
        }
        if (queryExtensions)
            __state.Plans = XPathPlanRuntime.Begin(__0);
    }

    private static Exception? StageFinalizer(Exception? __exception, StageState? __state)
    {
        if (__state == null)
            return __exception;
        __state.Watch.Stop();
        ScopedDefLookup? lookup = __state.Lookup;
        ScopedSingleQueryMemo? memo = __state.SingleMemo;
        ScopedXPathPlans? plans = __state.Plans;
        active = null;
        singleMemo = null;
        try
        {
            Receipt("patches-complete", __exception == null ? "original-completed" : "original-exception",
                "\"elapsedMs\":" + __state.Watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
                + ",\"hits\":" + (lookup?.Hits ?? 0) + ",\"fallbacks\":" + (lookup?.Fallbacks ?? 0)
                + ",\"attributeHits\":" + (lookup?.AttributeHits ?? 0)
                + ",\"expandedSyntaxHits\":" + (lookup?.ExpandedSyntaxHits ?? 0)
                + ",\"customSingleIndexHits\":" + customSingleIndexHits
                + ",\"eagerCustomIndexHits\":" + eagerCustomIndexHits
                + ",\"indexRebuilds\":" + (lookup?.Rebuilds ?? 0) + ",\"invalidations\":" + (lookup?.Invalidations ?? 0)
                + ",\"peakEntries\":" + (lookup?.PeakEntries ?? 0) + ",\"queries\":" + (lookup?.QueryCount ?? 0)
                + ",\"patchInfoReads\":" + Guards.Sum(g => g.PatchInfoReads)
                + ",\"singleQueryHits\":" + (memo?.Hits ?? 0) + ",\"singleQueryMissHits\":" + (memo?.MissHits ?? 0)
                + ",\"singleQueryCaptures\":" + (memo?.Captures ?? 0) + ",\"singleQueryInvalidations\":" + (memo?.Invalidations ?? 0)
                + ",\"singleQueryPeakEntries\":" + (memo?.PeakEntries ?? 0)
                + ",\"singleQuerySetupMs\":" + __state.SingleSetupMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
                + ",\"queryPlanCompilations\":" + (plans?.Compilations ?? 0) + ",\"queryPlanHits\":" + (plans?.Hits ?? 0)
                + ",\"queryPlanFallbacks\":" + (plans?.Fallbacks ?? 0) + ",\"queryPlanEntries\":" + (plans?.Count ?? 0));
        }
        finally { lookup?.Dispose(); memo?.Dispose(); XPathPlanRuntime.End(plans); }
        return __exception;
    }

    private sealed class StageState
    {
        internal readonly Stopwatch Watch = Stopwatch.StartNew();
        internal ScopedDefLookup? Lookup;
        internal ScopedSingleQueryMemo? SingleMemo;
        internal double SingleSetupMilliseconds;
        internal ScopedXPathPlans? Plans;
    }

    private static void Receipt(string kind, string reason, string? fields = null)
        => JsonLineLog.WriteReceipt(evidencePath, kind, reason, fields);
}
