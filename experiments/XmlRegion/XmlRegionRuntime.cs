// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

public static class XmlRegionRuntime
{
    internal const string Owner = "wakeup.xml-region";
    // Historical probe selection. The final larger integrated input also lost;
    // this entire type is test-only and is not available in the product.
    internal const int MinimumRoots = 13000, MinimumBroadSelectors = 2;
    private static bool enabled;
    private static string? root;
    private static XmlRegionContract? contract;
    private static bool contractAttempted;
    [ThreadStatic] private static Stage? active;
    internal static string Status { get; private set; } = "XML mutation reuse is off.";
    private static readonly FieldInfo RunningMods = typeof(LoadedModManager).GetField("runningMods", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Patches = typeof(ModContentPack).GetField("patches", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Package = typeof(ModContentPack).GetField("packageIdInt", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo XPath = typeof(PatchOperationPathed).GetField("xpath", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void TryInitialize(IReadOnlyList<string> arguments)
    {
        var selection = StartupLaunchSelector.Parse(arguments);
        if (selection.Selection != StartupSelection.Candidate || !arguments.Contains("--wake-up-xml-region=on")
            || arguments.Count(a => a?.StartsWith("--wake-up-xml-region=", StringComparison.Ordinal) == true) != 1) return;
        try
        {
            if (PlayDataLoader.Loaded || selection.SaveDataRoot == null) throw new InvalidOperationException("startup window unavailable");
            root = Path.Combine(selection.SaveDataRoot, "WakeUp", "XmlRegion", "v1");
            var harmony = new Harmony(Owner);
            harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "ApplyPatches"),
                prefix: new HarmonyMethod(typeof(XmlRegionRuntime), nameof(StagePrefix)) { priority = Priority.Last },
                finalizer: new HarmonyMethod(typeof(XmlRegionRuntime), nameof(StageFinalizer)));
            enabled = true;
            Status = "XML mutation reuse selected; ready to check eligible native intervals.";
        }
        catch (Exception exception)
        {
            enabled = false; new Harmony(Owner).UnpatchAll(Owner);
            Status = "XML mutation reuse refused: " + exception.Message + ". Ordinary loading remains in use.";
        }
        Log.Message("[Wake-Up] " + Status);
    }

    internal static void Maintain(string saveDataRoot, CacheLaunchPolicy policy)
    {
        string path = Path.Combine(saveDataRoot, "WakeUp", "XmlRegion", "v1");
        if (!Directory.Exists(path)) return;
        using var store = new OwnedCacheStore(path, XmlRegionOperations.MaxBytes, XmlRegionReplay.StoreBytes, policy);
        store.Complete();
    }

    private static void StagePrefix(XmlDocument __0, Dictionary<XmlNode, LoadableXmlAsset> __1, bool __runOriginal, out Stage? __state)
    {
        __state = null;
        if (!enabled) return;
        if (!__runOriginal) { Status = "XML mutation reuse completed with zero work: native patching was skipped by another loader."; return; }
        if (active != null || DeepProfiler.enabled) { Status = "XML mutation reuse refused: nested or profiled patching. Ordinary patches run."; return; }
        if (__0.GetType() != typeof(XmlDocument) || __0.DocumentElement?.GetType() != typeof(XmlElement)
            || __0.DocumentElement.ChildNodes.Count < MinimumRoots)
        { Status = "XML mutation reuse refused: input is below the measured useful size. Ordinary patches run."; return; }
        __state = active = new Stage(__0, __1, root!, OwnsRegionObserver);
        Status = "XML mutation reuse active; checking ordered native intervals.";
    }

    private static Exception? StageFinalizer(Exception? __exception, Stage? __state)
    {
        if (__state == null) return __exception;
        active = null;
        __state.Dispose();
        Status = "XML mutation reuse completed: " + __state.Hits + " hits, " + __state.Skipped + " native operations reused, "
            + __state.Captured + " captures; " + __state.Refused + " refusals. " + __state.LastReason + ".";
        Log.Message("[Wake-Up] " + Status);
        return __exception;
    }

    // Preserve the experimental observer check without leaving an unused
    // integration seam in the shipping search implementation. The measured
    // integrated candidate used the equivalent direct internal-field check.
    private static bool OwnsRegionObserver(Delegate observer)
    {
        object? lookup = AccessTools.Field(typeof(DefLookupRuntime), "active").GetValue(null);
        return lookup != null && ReferenceEquals(observer.Target, lookup)
            && observer.Method.DeclaringType == typeof(ScopedDefLookup)
            && (observer.Method.Name == "Changing" || observer.Method.Name == "Changed")
            && !(bool)AccessTools.Field(typeof(ScopedDefLookup), "disposed").GetValue(lookup)
            && (int)AccessTools.Field(typeof(ScopedDefLookup), "pendingMutations").GetValue(lookup) == 0;
    }

    // The inert prepatch keeps the exact native operation call when disabled.
    // Active cold operations still execute one at a time inside the game's
    // original foreach/try/catch; unknown operations and Complete stay ordinary.
    public static bool ApplyOne(object operation, XmlDocument document)
    {
        var op = (PatchOperation)operation;
        if (!enabled || active == null) return op.Apply(document);
        return active.ApplyOne(op, document);
    }

    internal sealed class Stage : IDisposable
    {
        private readonly XmlDocument document;
        private readonly Dictionary<XmlNode, LoadableXmlAsset> sources;
        private readonly string storeRoot;
        private readonly Func<Delegate, bool>? ownedObserver;
        private OwnedCacheStore? store;
        private XmlRegionReplay.Session? session;
        private bool disposed;
        internal int Hits, Skipped, Captured, Refused;
        internal string LastReason = "no useful supported interval";
        internal Stage(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> sources, string storeRoot, Func<Delegate, bool>? ownedObserver)
        { this.document = document; this.sources = sources; this.storeRoot = storeRoot; this.ownedObserver = ownedObserver; }

        internal bool ApplyOne(PatchOperation operation, XmlDocument xml)
        {
            if (session != null)
            {
                if (!session.Matches(operation) || !ReferenceEquals(xml, document))
                {
                    // This cannot occur in the admitted native list/worker
                    // contract. Never rerun already replayed work as recovery.
                    session.Dispose(); session = null;
                    throw new InvalidOperationException("XML region native order changed during reuse.");
                }
                return Consume(operation, xml);
            }
            if (disposed || !ReferenceEquals(xml, document) || operation is not PatchOperationPathed
                || XPath.GetValue(operation) is not string xpath || !HasOrToken(xpath)
                || !XmlRegionOperations.TrySelector(xpath, out var anchorKeys, out _)
                || anchorKeys.Select(key => key.Key).Distinct().Count() < 2)
                return operation.Apply(xml);
            if (!TryStart(operation, xml)) return operation.Apply(xml);
            // Commit and native worker execution are outside setup's catch.
            return Consume(operation, xml);
        }
        private static bool HasOrToken(string xpath)
        {
            for (int i = 1; i + 2 < xpath.Length; i++)
                if (xpath[i] == 'o' && xpath[i + 1] == 'r' && char.IsWhiteSpace(xpath[i - 1]) && char.IsWhiteSpace(xpath[i + 2])) return true;
            return false;
        }
        private bool TryStart(PatchOperation operation, XmlDocument xml)
        {
            try
            {
                if (!contractAttempted)
                { contractAttempted = true; XmlRegionContract.TryCreate(out contract, out string reason); LastReason = reason; }
                if (contract == null || !contract.Allows(out LastReason)) { Refused++; return false; }
                if (!TryFindOperations(operation, out var list, out int start)) { Refused++; LastReason = "native operation list unavailable"; return false; }
                var admitted = new List<PatchOperation>();
                int objects = 0, broad = 0;
                for (int i = start; i < list.Count && objects < XmlRegionOperations.MaxOperations; i++)
                {
                    if (!XmlRegionOperations.TryCreate(new[] { list[i] }, 0, out var single)
                        || objects + single!.ObjectCount > XmlRegionOperations.MaxOperations
                        || !RetainedXmlStaging.TryAdmit(xml, single.ImportedTemplates(), ownedObserver, out _)) break;
                    admitted.Add(list[i]); objects += single.ObjectCount; broad += single.BroadSelectorCount;
                }
                if (broad < MinimumBroadSelectors) { LastReason = "insufficient broad native selector work"; return false; }
                store ??= new OwnedCacheStore(storeRoot, XmlRegionOperations.MaxBytes, XmlRegionReplay.StoreBytes, CacheLaunchPolicy.Current);
                if (!XmlRegionReplay.TryBegin(xml, admitted, store, ownedObserver, SourceIdentity, out session, out LastReason))
                { Refused++; return false; }
                return true;
            }
            catch (Exception exception)
            {
                session?.Dispose(); session = null; Refused++; LastReason = exception.GetType().Name;
                return false;
            }
        }
        private bool Consume(PatchOperation operation, XmlDocument xml)
        {
            var current = session!;
            bool success;
            try { success = current.ApplyOne(operation, xml, (op, doc) => op.Apply(doc)); }
            catch { current.Dispose(); session = null; throw; }
            if (current.Complete)
            {
                if (current.Result.Hit) { Hits++; Skipped += current.Result.Outcomes.Length; }
                if (current.Result.Captured) Captured++;
                LastReason = current.Result.Reason;
                current.Dispose(); session = null;
            }
            return success;
        }
        private string SourceIdentity(XmlElement node)
        {
            if (sources.GetType() != typeof(Dictionary<XmlNode, LoadableXmlAsset>)
                || !ReferenceEquals(sources.Comparer, EqualityComparer<XmlNode>.Default)
                || !sources.TryGetValue(node, out var asset) || asset.GetType() != typeof(LoadableXmlAsset)
                || asset.mod != null && asset.mod.GetType() != typeof(ModContentPack)) throw new InvalidDataException("region-source-attribution");
            return (asset.mod == null ? "" : (string?)Package.GetValue(asset.mod)) + "\0" + asset.fullFolderPath + "\0" + asset.name;
        }
        private static bool TryFindOperations(PatchOperation operation, out List<PatchOperation> list, out int index)
        {
            list = null!; index = -1;
            if (RunningMods.GetValue(null) is not List<ModContentPack> mods) return false;
            foreach (var mod in mods)
            {
                if (mod.GetType() != typeof(ModContentPack) || Patches.GetValue(mod) is not List<PatchOperation> operations) continue;
                for (int found = 0; found < operations.Count; found++)
                {
                    if (!ReferenceEquals(operations[found], operation)) continue;
                    if (index >= 0) return false;
                    list = operations; index = found;
                }
            }
            return index >= 0;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; session?.Dispose(); session = null;
            try { store?.Complete(); }
            catch (Exception exception) { LastReason = "storage cleanup " + exception.GetType().Name; }
            finally
            {
                try { store?.Dispose(); }
                catch (Exception exception) { LastReason = "storage close " + exception.GetType().Name; }
                store = null;
            }
        }
    }
}
