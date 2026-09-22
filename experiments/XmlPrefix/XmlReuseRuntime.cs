// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Nonshipping R1 experiment. No product initialization or selector calls this.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

public static class XmlReuseRuntime
{
    private static string? root;
    private static bool consumed;
    [ThreadStatic] private static Session? active;
    private static string status = "Independent XML reuse: not selected.";
    internal static string Status
    {
        get => root != null && !consumed && PlayDataLoader.Loaded
            ? "Independent XML reuse: selected, but the native patch boundary was not reached; no work reused."
            : status;
        private set => status = value;
    }
    internal static void Initialize(IReadOnlyList<string> args)
    {
        var mode = StartupLaunchSelector.Parse(args);
        var selected = args.Where(a => a.StartsWith("--wake-up-xml-reuse=", StringComparison.Ordinal)).ToArray();
        if (selected.Length == 0) return;
        Status = "Independent XML reuse: selected, awaiting the native patch boundary.";
        if (selected.Length != 1 || selected[0] != "--wake-up-xml-reuse=on" || mode.Selection != StartupSelection.Candidate)
        { Status = "Independent XML reuse: refused development selection; ordinary loading."; return; }
        root = mode.SaveDataRoot;
    }

    internal static void Maintain(string saveDataRoot)
    {
        if (CacheLaunchPolicy.Current.Action != CacheAction.Clear) return;
        using var cache = Open(saveDataRoot, CacheLaunchPolicy.Current);
        if (cache.StoredBytes != 0) throw new IOException("xml-clear-incomplete");
    }

    // The Prepatcher callsite passes both caller locals by reference. All
    // fallible work happens before commit; original patching is invoked once.
    public static void Apply(ref XmlDocument document, ref object sourceObject, object inputObject, bool hotReload)
    {
        var sources = (Dictionary<XmlNode, LoadableXmlAsset>)sourceObject;
        ApplyNative(ref document, ref sources, (List<LoadableXmlAsset>)inputObject, hotReload);
        sourceObject = sources;
    }

    private static void ApplyNative(ref XmlDocument document, ref Dictionary<XmlNode, LoadableXmlAsset> sources,
        List<LoadableXmlAsset> inputs, bool hotReload)
    {
        if (root == null || consumed || hotReload || active != null)
        { LoadedModManager.ApplyPatches(document, sources); return; }
        consumed = true;
        var setup = Stopwatch.StartNew();
        string reason;
        if (!XmlReuseContract.Allows(out reason))
        { Status = "Independent XML reuse: refused (" + reason + "); ordinary loading."; Receipt(root, "refused", reason); LoadedModManager.ApplyPatches(document, sources); return; }
        PatchOperation[] operations;
        string context;
        try
        {
            if (inputs.GetType() != typeof(List<LoadableXmlAsset>) || sources.GetType() != typeof(Dictionary<XmlNode, LoadableXmlAsset>)
                || LoadedModManager.RunningModsListForReading.GetType() != typeof(List<ModContentPack>)
                || LoadedModManager.RunningModsListForReading.Any(m => AccessTools.Field(typeof(ModContentPack), "patches").GetValue(m)?.GetType() != typeof(List<PatchOperation>)))
                throw new InvalidDataException("xml-input-or-patch-list-not-materialized");
            operations = LoadedModManager.RunningModsListForReading.SelectMany(m => m.Patches).ToArray();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                XmlReuseSnapshot.WriteString(writer, mod.PackageId); XmlReuseSnapshot.WriteString(writer, mod.Name);
                XmlReuseSnapshot.WriteString(writer, mod.RootDir);
            }
            foreach (var op in operations)
            { XmlReuseSnapshot.WriteString(writer, op.GetType().AssemblyQualifiedName!); XmlReuseSnapshot.WriteString(writer, op.sourceFile ?? ""); }
            context = PngCache.Hex(PngCache.Hash(stream.ToArray())) + "|" + typeof(XmlDocument).Assembly.FullName
                + "|" + typeof(LoadedModManager).Module.ModuleVersionId + "|" + string.Join("|", XmlReuseContract.Bodies);
        }
        catch (Exception e)
        { Status = "Independent XML reuse: refused input binding (" + e.GetType().Name + ")."; LoadedModManager.ApplyPatches(document, sources); return; }
        Execute(ref document, ref sources, inputs, operations, context, root, CacheLaunchPolicy.Current,
            () => XmlReuseContract.Allows(out _), LoadedModManager.ApplyPatches, requireCostBenefit: true, priorMilliseconds: setup.Elapsed.TotalMilliseconds);
    }

    // Also exercised offline with the native top-level iteration and original
    // operation methods, without constructing a game or preparing a fixture.
    internal static void Execute(ref XmlDocument document, ref Dictionary<XmlNode, LoadableXmlAsset> sources,
        IReadOnlyList<LoadableXmlAsset> inputs, IReadOnlyList<PatchOperation> tops, string context, string saveRoot,
        CacheLaunchPolicy policy, Func<bool> admission, Action<XmlDocument, Dictionary<XmlNode, LoadableXmlAsset>> original,
        bool requireCostBenefit = false, double priorMilliseconds = 0)
    {
        var validation = Stopwatch.StartNew();
        Session? session = null;
        XmlReuseSnapshot.Restored? restored = null;
        Action? commit = null;
        try
        {
            if (active != null || !admission() || !XmlReuseOperations.TryCreate(tops, out var descriptor, allowFindMod: false))
                throw new InvalidDataException("no-admitted-native-prefix");
            string key = InputKey(document, sources, inputs, descriptor!, context);
            var store = Open(saveRoot, policy);
            session = new Session(store, key, descriptor!, tops, sources, inputs, requireCostBenefit, admission);
            restored = store.Read(key, bytes => XmlReuseSnapshot.Decode(bytes, inputs));
            if (restored != null)
            {
                commit = descriptor!.PrepareRestore(restored.State);
                if (!admission()) throw new InvalidDataException("changed-contract-before-restore");
                if (requireCostBenefit && !HasBenefit(restored.NativeMilliseconds, priorMilliseconds + validation.Elapsed.TotalMilliseconds))
                { restored = null; commit = null; session.CostRefused = true; }
            }
            session.ValidationMilliseconds = priorMilliseconds + validation.Elapsed.TotalMilliseconds;
            Status = "Independent XML reuse: admitted " + descriptor!.PrefixCount + " top-level patches ("
                + descriptor.OperationCount + " operations); " + (restored == null ? "miss, capturing ordinary work." : "validated hit.");
        }
        catch (Exception e)
        {
            session?.Dispose(); session = null; restored = null; commit = null;
            Status = "Independent XML reuse: refused (" + e.GetType().Name + "); ordinary loading.";
        }
        // No exception recovery below can rerun already published operations.
        if (restored != null)
        {
            commit!();
            document = restored.Document;
            sources = restored.Sources;
            session!.Hit = true;
        }
        active = session;
        try { original(document, sources); }
        finally
        {
            active = null;
            if (session != null)
            {
                Status += " Skipped " + session.Skipped + "; captured " + (session.Captured ? "yes" : "no") + ".";
                if (session.CostRefused) Status += " Cache cost exceeded the observed patch-work budget; ordinary loading retained.";
                Receipt(saveRoot, "complete", session.Hit ? "hit" : session.Captured ? "captured" : "ordinary-not-published",
                    "\"admittedTopLevel\":" + session.Descriptor.PrefixCount + ",\"admittedOperations\":" + session.Descriptor.OperationCount
                    + ",\"skippedTopLevel\":" + session.Skipped + ",\"captured\":" + (session.Captured ? "true" : "false")
                    + ",\"costRefused\":" + (session.CostRefused ? "true" : "false"));
                session.Dispose();
            }
            else Receipt(saveRoot, "refused", status);
        }
    }

    public static bool ApplyOne(object value, XmlDocument document)
    {
        var operation = (PatchOperation)value;
        Session? session = active;
        if (session == null || session.Position >= session.Descriptor.PrefixCount) return operation.Apply(document);
        if (!ReferenceEquals(operation, session.Tops[session.Position]))
            throw new InvalidOperationException("xml-prefix-iteration-changed");
        session.Position++;
        if (session.Hit) { session.Skipped++; return true; }
        bool result;
        var work = Stopwatch.StartNew();
        try { result = operation.Apply(document); }
        catch { session.Failed = true; throw; }
        finally { session.NativeMilliseconds += work.Elapsed.TotalMilliseconds; }
        if (!result) session.Failed = true;
        if (session.Position == session.Descriptor.PrefixCount && !session.Failed && session.Descriptor.CompletedSuccessfully)
        {
            try
            {
                if (session.RequireCostBenefit && !HasBenefit(session.NativeMilliseconds, session.ValidationMilliseconds))
                { session.CostRefused = true; return result; }
                // Capacity check before allocating the completed-tree payload;
                // Publish still rechecks the exact size and integrity envelope.
                if (session.Store.CanPublish(session.Key, XmlReuseSnapshot.MaximumBytes))
                {
                    var capture = Stopwatch.StartNew();
                    byte[] bytes = XmlReuseSnapshot.Encode(document, session.Sources, session.Inputs, session.Descriptor.CaptureState(), session.NativeMilliseconds);
                    if (session.RequireCostBenefit)
                    {
                        // A detached decode measures the actual restoration
                        // cost before publishing a first-build candidate.
                        XmlReuseSnapshot.Decode(bytes, session.Inputs);
                        if (!HasBenefit(session.NativeMilliseconds, session.ValidationMilliseconds + capture.Elapsed.TotalMilliseconds))
                        { session.CostRefused = true; return result; }
                    }
                    if (session.Admission()) session.Captured = session.Store.Publish(session.Key, bytes);
                }
            }
            catch { session.Failed = true; }
        }
        return result;
    }

    internal static string InputKey(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> sources,
        IReadOnlyList<LoadableXmlAsset> inputs, XmlReuseOperations descriptor, string context)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        XmlReuseSnapshot.WriteString(writer, "native-prefix-v1|" + context);
        writer.Write(inputs.Count);
        foreach (var input in inputs)
        {
            if (input.xmlDoc?.DocumentElement?.Name != "Defs") throw new InvalidDataException("xml-input-error");
            XmlReuseSnapshot.WriteString(writer, input.fullFolderPath ?? "");
            XmlReuseSnapshot.WriteString(writer, input.name ?? "");
            XmlReuseSnapshot.WriteString(writer, input.mod?.PackageId ?? "");
            XmlReuseSnapshot.WriteTree(writer, input.xmlDoc);
        }
        writer.Write(descriptor.Identity.Length); writer.Write(descriptor.Identity);
        byte[] combined = XmlReuseSnapshot.Encode(document, sources, inputs, Array.Empty<byte>());
        writer.Write(combined.Length); writer.Write(combined);
        if (stream.Length > 2L * XmlReuseSnapshot.MaximumBytes) throw new InvalidDataException("xml-input-size");
        return PngCache.Hex(PngCache.Hash(stream.ToArray()));
    }

    private static OwnedCacheStore Open(string saveRoot, CacheLaunchPolicy policy)
        => new(Path.Combine(saveRoot, "WakeUp", "XmlPrefix", "v1"), XmlReuseSnapshot.MaximumBytes,
            512L * 1024 * 1024, policy);

    internal static bool HasBenefit(double saved, double cost) => saved > 0 && cost >= 0
        && !double.IsInfinity(saved) && !double.IsNaN(saved) && !double.IsInfinity(cost) && !double.IsNaN(cost)
        && saved > cost * 1.5;

    private static void Receipt(string saveRoot, string kind, string reason, string? fields = null)
        => JsonLineLog.WriteReceipt(Path.Combine(saveRoot, "WakeUp", "xml-prefix.jsonl"), kind, reason, fields);

    private sealed class Session : IDisposable
    {
        internal readonly OwnedCacheStore Store;
        internal readonly string Key;
        internal readonly XmlReuseOperations Descriptor;
        internal readonly IReadOnlyList<PatchOperation> Tops;
        internal readonly Dictionary<XmlNode, LoadableXmlAsset> Sources;
        internal readonly IReadOnlyList<LoadableXmlAsset> Inputs;
        internal int Position, Skipped;
        internal bool Hit, Failed, Captured, CostRefused;
        internal double NativeMilliseconds, ValidationMilliseconds;
        internal readonly bool RequireCostBenefit;
        internal readonly Func<bool> Admission;
        internal Session(OwnedCacheStore store, string key, XmlReuseOperations descriptor, IReadOnlyList<PatchOperation> tops,
            Dictionary<XmlNode, LoadableXmlAsset> sources, IReadOnlyList<LoadableXmlAsset> inputs, bool requireCostBenefit, Func<bool> admission)
        { Store = store; Key = key; Descriptor = descriptor; Tops = tops; Sources = sources; Inputs = inputs;
          RequireCostBenefit = requireCostBenefit; Admission = admission; }
        public void Dispose() { try { Store.Complete(); } catch { } finally { Store.Dispose(); } }
    }
}
