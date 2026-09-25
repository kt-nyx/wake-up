// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

public static class ProcessedXmlRuntime
{
    internal const string Owner = "wakeup.processed-xml";
    internal static string Status { get; private set; } = "Processed XML reuse is off.";
    private static bool enabled, consumed;
    private static string? log;
    private static OwnedCacheStore? store;
    [ThreadStatic] private static Session? active;
    [ThreadStatic] private static Invocation? invocation;
    private sealed class Invocation
    {
        internal XmlDocument Document;
        internal Dictionary<XmlNode, LoadableXmlAsset> Map;
        internal bool PreparationAttempted;
        internal Session? Session;
        internal Invocation(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map)
        { Document = document; Map = map; }
    }
    private sealed class Session
    {
        internal ProcessedXmlOperations Descriptor = null!;
        internal PatchOperation[] Tops = null!, Objects = null!;
        internal Dictionary<PatchOperation, int> Indices = null!;
        internal Dictionary<XmlNode, LoadableXmlAsset> Map = null!;
        internal LoadableXmlAsset[] Assets = null!;
        internal XmlDocument Document = null!;
        internal string Key = "";
        internal bool Hit, Invalid, Captured, Closed;
        internal int Skipped, Effects, CompletedTops, ModNameReplays;
        internal int[] Parents = null!;
        internal int[] ModNameCalls = null!;
        internal bool[] Called = null!, Results = null!;
        internal Stack<int> Stack = new();
    }
    internal static void Initialize(string[] args, string saveRoot)
    {
        log = Path.Combine(saveRoot, "WakeUp", "processed-xml.jsonl");
        bool selected = StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
            && args.Count(a => a.StartsWith("--wake-up-processed-xml=", StringComparison.Ordinal)) == 1
            && args.Contains("--wake-up-processed-xml=on");
        var policy = CacheLaunchPolicy.Current;
        if (!selected && policy.Action != CacheAction.Clear) return;
        if (selected && policy.Action != CacheAction.Clear && (policy.AllowRead || policy.AllowWrite) && LoaderSupplierPolicy.YieldXml)
        { LoaderSupplierPolicy.RefuseXml("processed-xml"); Status = LoaderSupplierPolicy.Reason(true); return; }
        try
        {
            string root = Path.Combine(saveRoot, "WakeUp", "ProcessedXml", "v1");
            store = new OwnedCacheStore(root, ProcessedXmlSnapshot.MaximumBytes,
                SharedCacheBudget.ForRoot(root)?.MaximumBytes ?? 1024L * 1024 * 1024, policy);
            if (policy.Action == CacheAction.Clear || !policy.AllowRead && !policy.AllowWrite)
            { store.Dispose(); store = null; CompatibilityStatus.Registry?.Set("processed-xml", OperationState.NotApplicable, "Cache use bypassed by selected maintenance."); Status = policy.Action == CacheAction.Clear ? "Processed XML cache cleared." : "Processed XML cache bypassed."; return; }
            if (!selected) return;
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string identityReason)) { CompatibilityStatus.Refuse("processed-xml", identityReason); return; }
            var harmony = new Harmony(Owner);
            harmony.Patch(AccessTools.Method(typeof(PatchOperation), "Apply"),
                prefix: new HarmonyMethod(typeof(ProcessedXmlRuntime), nameof(BeforeOperation)),
                finalizer: new HarmonyMethod(typeof(ProcessedXmlRuntime), nameof(AfterOperation)));
            harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "ApplyPatches"), prefix: PreparationPrefix());
            harmony.Patch(AccessTools.Method(typeof(ModLister), "HasActiveModWithName"),
                prefix: new HarmonyMethod(typeof(ProcessedXmlRuntime), nameof(ObserveModNameLookup)));
            foreach (string name in new[] { "Error", "Warning", "Message" })
                harmony.Patch(AccessTools.Method(typeof(Log), name, new[] { typeof(string) }),
                    prefix: new HarmonyMethod(typeof(ProcessedXmlRuntime), nameof(ObserveLog)));
            enabled = true; CompatibilityStatus.Available("processed-xml", "Stage hook installed; final XML contract is checked when native patch work runs."); Status = "Processed XML reuse selected; waiting for supported patch work.";
        }
        catch (Exception e) { CompatibilityStatus.Refuse("processed-xml", e.Message); store?.Dispose(); store = null; Status = "Processed XML refused: " + e.Message; }
    }
    public static void Apply(ref XmlDocument document, ref Dictionary<XmlNode, LoadableXmlAsset> map, bool hotReload)
    {
        ApplyCore(ref document, ref map, hotReload);
        LoadingDiagnosticsRuntime.Capture("processed-after-patches-before-inheritance", document, map);
    }

    private static void ApplyCore(ref XmlDocument document, ref Dictionary<XmlNode, LoadableXmlAsset> map, bool hotReload)
    {
        if (!enabled || consumed || hotReload || active != null || invocation != null || store == null)
        { LoadedModManager.ApplyPatches(document, map); return; }
        consumed = true;
        var current = new Invocation(document, map);
        invocation = current;
        try { LoadedModManager.ApplyPatches(document, map); }
        finally
        {
            // Native argument replacements happen after the native framework
            // prefixes. Publish their final owned pair back through the outer
            // loader boundary even when a later native callback throws.
            document = current.Document; map = current.Map;
            active = null; invocation = null;
            Session? session = current.Session;
            if (session != null)
            {
                Status = "Processed XML: " + session.Skipped + " operation calls reused; " + session.Effects + " custom branch calls executed. Performance pending.";
                JsonLineLog.WriteEvent(log, "complete", "\"admittedTopLevel\":" + session.Tops.Length + ",\"admittedOperations\":" + session.Objects.Length
                    + ",\"customOperations\":" + session.Objects.Count(o => o.GetType().Assembly != typeof(PatchOperation).Assembly)
                    + ",\"stopReason\":" + JsonLineLog.Quote(session.Descriptor.StopReason)
                    + ",\"recordedModLookups\":" + session.ModNameCalls.Sum()
                    + ",\"replayedModLookups\":" + session.ModNameReplays
                    + ",\"skippedCalls\":" + session.Skipped + ",\"liveWrappers\":" + session.Effects + ",\"warm\":" + (session.Hit ? "true" : "false")
                    + ",\"captured\":" + (session.Captured ? "true" : "false") + ",\"key\":" + JsonLineLog.Quote(session.Key));
            }
            store?.Complete(); store?.Dispose(); store = null;
        }
    }
    internal static HarmonyMethod PreparationPrefix() => new(typeof(ProcessedXmlRuntime), nameof(PrepareStage))
    {
        after = new[] { "ModSettingsFrameworkMod" },
        before = new[] { "wakeup.def-lookup" }
    };
    private static void PrepareStage(ref XmlDocument __0, ref Dictionary<XmlNode, LoadableXmlAsset> __1, bool __runOriginal)
    {
        Invocation? current = invocation;
        if (!__runOriginal || current == null || current.PreparationAttempted || active != null
            || !ReferenceEquals(__0, current.Document) || !ReferenceEquals(__1, current.Map)) return;
        current.PreparationAttempted = true;
        // This point follows the real framework settings-loading callback and
        // precedes query scope creation. The key sees its final settings and
        // operation fields, and the framework callback is never replayed.
        ref XmlDocument document = ref __0;
        ref Dictionary<XmlNode, LoadableXmlAsset> map = ref __1;
        Session? session = null; ProcessedXmlSnapshot.Restored? restored = null; Action? state = null;
        try
        {
            if (store == null) throw new InvalidOperationException("processed-store-unavailable");
            if (!ProcessedXmlContract.Allows(out string reason)) { CompatibilityStatus.Refuse("processed-xml", reason); throw new InvalidDataException(reason); }
            if (ResolvedInheritanceRuntime.HasForeignXmlObservers(document, DefLookupRuntime.OwnsXmlObserver)) { CompatibilityStatus.Refuse("processed-xml", "Another owner observes XML document mutations."); throw new InvalidDataException("foreign-xml-observer"); }
            var mods = LoadedModManager.RunningModsListForReading;
            var patchField = AccessTools.Field(typeof(ModContentPack), "patches");
            if (mods.Any(m => patchField.GetValue(m)?.GetType() != typeof(List<PatchOperation>))) throw new InvalidDataException("patch-lists-not-materialized");
            var tops = mods.SelectMany(m => m.Patches).ToArray();
            if (!ProcessedXmlOperations.TryCreate(tops, out var descriptor)) throw new InvalidDataException("no-supported-patch-stage");
            var objects = descriptor!.Objects.ToArray();
            var assets = map.Values.Distinct().ToArray();
            var parents = Enumerable.Repeat(-1, objects.Length).ToArray();
            var called = new bool[objects.Length]; var results = new bool[objects.Length];
            using var keyData = new MemoryStream(); using var writer = new BinaryWriter(keyData, Encoding.UTF8, true);
            writer.Write("processed-mixed-private-after-settings-effects-v3"); writer.Write(typeof(LoadedModManager).Module.ModuleVersionId.ToString());
            writer.Write(ProcessedXmlContract.SettingsPrefixIdentity);
            writer.Write(PrepatcherXmlContract.Identity);
            writer.Write(typeof(XmlDocument).Module.ModuleVersionId.ToString()); writer.Write(string.Join("|", ProcessedXmlContract.Bodies));
            writer.Write(string.Join("|", ProcessedXmlAdapters.ExpectedBodies.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value)));
            writer.Write(descriptor.Identity.Length); writer.Write(descriptor.Identity);
            foreach (var mod in mods) { writer.Write(mod.PackageId); writer.Write(mod.RootDir); writer.Write(mod.Name); }
            foreach (var asset in assets) { writer.Write(asset.name ?? ""); writer.Write(asset.fullFolderPath ?? ""); writer.Write(asset.mod?.PackageId ?? ""); }
            // Complete current XML and real source ownership bind earlier native
            // selection, content, ordering, patches and observer transformations.
            writer.Write(CacheDigest.Hash(ProcessedXmlSnapshot.Encode(document, map, assets, Array.Empty<byte>(), Array.Empty<int>(), Array.Empty<bool>(), Array.Empty<bool>())));
            writer.Flush(); string key = BitConverter.ToString(CacheDigest.Hash(keyData.ToArray())).Replace("-", "");
            session = new Session { Descriptor = descriptor, Tops = tops.Take(descriptor.PrefixCount).ToArray(), Objects = objects,
                Indices = objects.Select((o, i) => (o, i)).ToDictionary(p => p.o, p => p.i), Map = map, Assets = assets,
                Document = document, Key = key, Parents = parents, Called = called, Results = results, ModNameCalls = new int[objects.Length] };
            restored = store.Read(key, bytes => ProcessedXmlSnapshot.Decode(bytes, assets, objects.Length));
            if (restored != null)
            {
                state = descriptor.PrepareRestore(restored.State);
                ValidateTrace(session, restored);
                if (!ProcessedXmlContract.Allows(out reason) || !descriptor.AllowsAdapters()) throw new InvalidDataException(reason);
            }
        }
        catch (Exception e)
        { session = null; restored = null; state = null; JsonLineLog.WriteReceipt(log, "native", e.Message); }
        // The final document stays private until all allocations, source/state
        // bindings and trace checks pass. No retry after reference publication.
        if (restored != null)
        {
            state!(); document = restored.Document; map = restored.Map;
            session!.Document = document; session.Map = map; session.Hit = true;
            session.Parents = restored.Parents; session.Called = restored.Called; session.Results = restored.Results;
            session.ModNameCalls = restored.ModNameCalls;
        }
        active = session;
        current.Document = document; current.Map = map; current.Session = session;
    }
    private static void ValidateTrace(Session session, ProcessedXmlSnapshot.Restored restored)
    {
        foreach (var top in session.Tops)
        { int i = session.Indices[top]; if (!restored.Called[i] || restored.Parents[i] != -1) throw new InvalidDataException("processed-top-trace"); }
        for (int i = 0; i < restored.Called.Length; i++)
        {
            if (restored.Called[i] && restored.Parents[i] >= 0 && !restored.Called[restored.Parents[i]]) throw new InvalidDataException("processed-parent-trace");
            if (restored.ModNameCalls[i] != 0 && (!restored.Called[i] || !ProcessedXmlAdapters.IsGunOperation(session.Objects[i])))
                throw new InvalidDataException("processed-mod-name-effect-trace");
        }
    }
    private static bool BeforeOperation(PatchOperation __instance, XmlDocument xml, ref bool __result, out int __state)
    {
        __state = -1; var session = active;
        if (session == null || session.Closed || !ReferenceEquals(xml, session.Document) || !session.Indices.TryGetValue(__instance, out int index)) return true;
        if (!session.Hit)
        {
            if (session.Called[index]) session.Invalid = true;
            session.Called[index] = true; session.Parents[index] = session.Stack.Count == 0 ? -1 : session.Stack.Peek();
            session.Stack.Push(index); __state = index; return true;
        }
        __state = index;
        if (session.Descriptor.RunsOnHit(index)) { session.Effects++; return true; }
        ReplayEffects(session, index, xml);
        __result = session.Results[index]; session.Skipped++; return false;
    }
    private static void ReplayEffects(Session session, int parent, XmlDocument xml)
    {
        if (session.ModNameCalls[parent] != 0)
        {
            ProcessedXmlAdapters.ReplayModNameEffects(session.Objects[parent], session.ModNameCalls[parent]);
            session.ModNameReplays += session.ModNameCalls[parent];
        }
        for (int i = parent + 1; i < session.Objects.Length; i++)
        {
            if (!session.Called[i] || session.Parents[i] != parent) continue;
            if (session.Descriptor.RunsOnHit(i)) session.Objects[i].Apply(xml);
            else ReplayEffects(session, i, xml);
        }
    }
    private static Exception? AfterOperation(Exception? __exception, bool __result, int __state)
    {
        var session = active;
        if (session == null || __state < 0) return __exception;
        if (session.Hit)
        {
            if (session.Parents[__state] == -1 && ++session.CompletedTops == session.Tops.Length) session.Closed = true;
            return __exception;
        }
        if (session.Stack.Count == 0 || session.Stack.Pop() != __state) session.Invalid = true;
        session.Results[__state] = __result;
        if (__exception != null) session.Invalid = true;
        if (session.Parents[__state] == -1 && ++session.CompletedTops == session.Tops.Length)
        {
            session.Closed = true;
            if (session.Invalid) return __exception;
            try
            {
                if (ProcessedXmlContract.Allows(out _) && session.Descriptor.AllowsAdapters()
                    && !ResolvedInheritanceRuntime.HasForeignXmlObservers(session.Document, DefLookupRuntime.OwnsXmlObserver)
                    && store!.CanPublish(session.Key, ProcessedXmlSnapshot.MaximumBytes))
                    session.Captured = store.Publish(session.Key, ProcessedXmlSnapshot.Encode(session.Document, session.Map, session.Assets,
                        session.Descriptor.CaptureState(), session.Parents, session.Called, session.Results, session.ModNameCalls));
            }
            catch { session.Invalid = true; }
        }
        return __exception;
    }
    private static void ObserveLog() { if (active != null && !active.Hit) active.Invalid = true; }
    private static void ObserveModNameLookup(string __0)
    {
        Session? session = active;
        if (session == null || session.Hit || session.Closed || session.Stack.Count == 0) return;
        int index = session.Stack.Peek();
        if (!ProcessedXmlAdapters.IsGunOperation(session.Objects[index])) return;
        if (__0 != "RunAndGun" || session.ModNameCalls[index] >= 131072) { session.Invalid = true; return; }
        session.ModNameCalls[index]++;
    }
}
