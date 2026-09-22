// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Registration, parent choice, traversal and diagnostic calls remain native.
// This owner reuses only completed CloneNode + recursive merge results, while
// their original root and the native private inheritance graph stay intact.
public static class ResolvedInheritanceRuntime
{
    internal const string Owner = "wakeup.resolved-inheritance";
    internal static readonly Dictionary<MethodBase, string> ExpectedBodies = RequiredMethods().ToDictionary(m => m, m => XmlSemanticContracts.Inheritance[XmlSemanticContracts.Key(m)]);
    internal static string Status { get; private set; } = "Resolved inheritance reuse is off.";
    private static readonly FieldInfo[] EventFields = typeof(XmlDocument).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).ToArray();
    private static OwnedCacheStore? store;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static MethodBase[] guardMethods = Array.Empty<MethodBase>();
    private static string? log;
    private static string semantics = "";
    private static bool enabled, finished;
    private static Session? session;
    private static int hitChildren, nativeChildren, generations, fallbackScopes;
    private static bool timing;
    private static long registrationTicks, keyTicks, readTicks, publicationTicks;

    private sealed class Session
    {
        internal readonly XmlDocument Document;
        internal readonly int Thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        internal readonly Dictionary<XmlNode, int> Ordinals;
        internal readonly string Key;
        internal readonly string DuplicateFields;
        internal readonly Dictionary<int, XmlNode> Captured = new();
        internal Dictionary<int, XmlNode>? Restored;
        internal bool Invalid, HasNativeResults;
        internal Session(XmlDocument document, Dictionary<XmlNode, int> ordinals, string key, string duplicateFields)
        { Document = document; Ordinals = ordinals; Key = key; DuplicateFields = duplicateFields; }
    }

    internal static void Initialize(string[] args, string saveDataRoot)
    {
        log = Path.Combine(saveDataRoot, "WakeUp", "resolved-inheritance.jsonl");
        timing = LoadingTimingRuntime.Selected(args);
        var policy = CacheLaunchPolicy.Current;
        bool selected = StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
            && args.Count(a => a.StartsWith("--wake-up-resolved-inheritance=", StringComparison.Ordinal)) == 1
            && args.Contains("--wake-up-resolved-inheritance=on");
        if (!selected && policy.Action != CacheAction.Clear) return;
        try
        {
            string root = Path.Combine(saveDataRoot, "WakeUp", "ResolvedInheritance", "v1");
            var shared = SharedCacheBudget.ForRoot(root);
            store = new OwnedCacheStore(root, ResolvedInheritanceFormat.MaximumPayload, shared?.MaximumBytes ?? 1024L * 1024 * 1024, policy);
            if (policy.Action == CacheAction.Clear)
            {
                JsonLineLog.WriteEvent(log, "clear", "\"remainingBytes\":" + store.StoredBytes);
                store.Dispose(); store = null; Status = "Resolved inheritance cache cleared; native loading this launch."; return;
            }
            if (!selected || !policy.AllowRead && !policy.AllowWrite)
            { store.Dispose(); store = null; Status = "Resolved inheritance cache bypassed this launch."; return; }
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason)) throw new InvalidOperationException(reason);
            if (PlayDataLoader.Loaded) throw new InvalidOperationException("startup already ended");
            guards = CreateGuards();
            semantics = string.Join("|", ExpectedBodies.OrderBy(p => p.Key.DeclaringType!.FullName + "." + p.Key.Name, StringComparer.Ordinal)
                .Select(p => p.Key.DeclaringType!.FullName + "." + p.Key.Name + ":" + p.Value))
                + "|" + typeof(XmlInheritance).Module.ModuleVersionId + "|" + typeof(XmlDocument).Module.ModuleVersionId + "|resolved-child-v1|" + PrepatcherXmlContract.Identity;
            var harmony = new Harmony(Owner);
            foreach (string name in new[] { "Error", "Warning" })
                harmony.Patch(AccessTools.Method(typeof(Log), name, new[] { typeof(string) }),
                    prefix: new HarmonyMethod(typeof(ResolvedInheritanceRuntime), nameof(ObserveDiagnostic)));
            harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(ResolvedInheritanceRuntime), nameof(Menu)));
            enabled = true; Status = "Resolved inheritance selected; waiting for native registration.";
            JsonLineLog.WriteEvent(log, "selected");
        }
        catch (Exception e)
        {
            enabled = false; store?.Dispose(); store = null;
            Status = "Resolved inheritance refused: " + e.Message + ". Native loading remains.";
            JsonLineLog.WriteReceipt(log, "refused", e.Message);
        }
        Log.Message("[Wake-Up] " + Status);
    }

    internal static PublishedPatchGuard[] CreateGuards()
    {
        // The caller supplies reviewed original/post-prepatch method bodies;
        // missing identities refuse selection instead of accepting an update.
        if (RequiredMethods().Any(m => !ExpectedBodies.ContainsKey(m))) throw new InvalidOperationException("inheritance method contracts incomplete");
        foreach (var pair in ExpectedBodies)
            if (!SemanticMethodIdentity.TryHash(pair.Key, out string hash, out _) || hash != pair.Value)
                throw new InvalidOperationException("inheritance method changed: " + pair.Key.Name);
        var xmlMethods = new MethodBase[] { typeof(XmlNode).GetMethod("CloneNode")!, typeof(XmlElement).GetMethod("CloneNode")!,
            typeof(XmlDocument).GetMethod("ImportNode")!, typeof(XmlNode).GetMethod("AppendChild")!, typeof(XmlNode).GetMethod("RemoveChild")!,
            typeof(XmlDocument).GetMethod("CreateElement", new[] { typeof(string), typeof(string), typeof(string) })!,
            typeof(XmlDocument).GetMethod("CreateAttribute", new[] { typeof(string), typeof(string), typeof(string) })! };
        guardMethods = ExpectedBodies.Keys.Where(m => !(m.DeclaringType == typeof(XmlInheritance) && m.Name == "GetResolvedNodeFor")).Concat(xmlMethods).Distinct().ToArray();
        return guardMethods.Select(m =>
            PublishedPatchGuard.TryCreate(m, Owner, out var guard, true, p => PrepatcherXmlContract.AllowsPatch(m, p)) ? guard! : throw new InvalidOperationException("inheritance observer guard unavailable")).ToArray();
    }

    internal static MethodBase[] RequiredMethods()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        return typeof(XmlInheritance).GetMethods(flags).Cast<MethodBase>()
            .Concat(typeof(XmlInheritance).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(t => t.GetMethods(flags)).Cast<MethodBase>())
            .Concat(new[] { AccessTools.Method(typeof(LoadedModManager), "ParseAndProcessXML") })
            .Concat(typeof(ModLister).GetMethods(flags).Where(m => m.Name == "AllModsActiveNoSuffix" || m.Name == "GetActiveModWithIdentifier")).ToArray();
    }

    private static void ObserveDiagnostic()
    {
        Session? current = session;
        if (current != null && current.Thread == Thread.CurrentThread.ManagedThreadId) current.Invalid = true;
    }

    internal static bool NoDocumentObservers(XmlDocument document) => !HasForeignXmlObservers(document);

    internal static bool HasForeignXmlObservers(XmlDocument document, Func<Delegate, bool>? allowed = null)
    {
        if (document.GetType() != typeof(XmlDocument) || EventFields.Length < 6) return true;
        try { return EventFields.Any(f => f.GetValue(document) is Delegate observer
            && observer.GetInvocationList().Any(callback => allowed?.Invoke(callback) != true)); }
        catch { return true; }
    }

    private static bool Admitted() => guards.Length != 0 && guards.All(g => g.AllowsOriginalContract()) && PrepatcherXmlContract.AllowsCallbacks();
    private static string DuplicateFieldsIdentity() => string.Join("\n", XmlInheritance.allowDuplicateNodesFieldNames.OrderBy(n => n, StringComparer.Ordinal));

    public static void Resolve(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map, bool hotReload)
    {
        // A nested call remains entirely native and invalidates the outer
        // generation; it must never borrow that generation's ordinal mapping.
        if (session != null)
        {
            Session outer = session; outer.Invalid = true; session = null;
            try { XmlInheritance.Resolve(); }
            finally { session = outer; }
            return;
        }
        Begin(document, map, hotReload);
        bool completed = false;
        try { XmlInheritance.Resolve(); completed = true; }
        finally { End(completed); }
    }

    public static void Begin(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map, bool hotReload)
    {
        if (hotReload) { session = null; JsonLineLog.WriteEvent(log, "hot-reload-native"); return; }
        if (!enabled || finished || store == null) return;
        try
        {
            if (!Admitted())
            {
                int rejected = Array.FindIndex(guards, g => !g.AllowsOriginalContract());
                throw new InvalidDataException("inheritance-method-observer-" + (rejected >= 0 ? ProcessedXmlContract.DescribePatches(guardMethods[rejected]) : "unavailable"));
            }
            if (!NoDocumentObservers(document)) throw new InvalidDataException("inheritance-document-observer");
            var roots = document.DocumentElement!.ChildNodes.Cast<XmlNode>().ToArray();
            if (roots.Length > ResolvedInheritanceFormat.MaximumRoots) throw new InvalidDataException("inheritance-root-limit");
            var ordinals = roots.Select((node, ordinal) => (node, ordinal)).ToDictionary(p => p.node, p => p.ordinal);
            string duplicateFields = DuplicateFieldsIdentity();
            long phase = Clock();
            byte[] registered = RegistrationIdentity(ordinals, map);
            registrationTicks += Elapsed(phase); phase = Clock();
            string key = InputKey(document, map, registered, duplicateFields, semantics);
            keyTicks += Elapsed(phase);
            var next = new Session(document, ordinals, key, duplicateFields);
            phase = Clock();
            next.Restored = store.Read(key, payload => DecodeGeneration(payload, document, roots.Length));
            readTicks += Elapsed(phase);
            session = next;
            JsonLineLog.WriteEvent(log, "scope", "\"key\":" + JsonLineLog.Quote(key) + ",\"restoredChildren\":" + (next.Restored?.Count ?? 0));
        }
        catch (Exception e)
        { session = null; fallbackScopes++; JsonLineLog.WriteReceipt(log, "native-scope", e.Message); }
    }

    // Called before the ORIGINAL clone/merge instructions. A false result
    // executes those instructions unchanged; there is no reflective fallback.
    public static bool TryRestore(XmlNode child, XmlNode resolvedParent, out XmlNode result)
    {
        result = null!;
        Session? current = session;
        if (!Usable(current, child, resolvedParent) || current!.Restored == null
            || !current.Ordinals.TryGetValue(child, out int ordinal) || !current.Restored.TryGetValue(ordinal, out var cached))
        { if (enabled) nativeChildren++; return false; }
        // Each native resolution creates a distinct detached mutable node.
        // Consume once so an unexpected repeat cannot alias the prior result.
        current.Restored.Remove(ordinal); current.Captured[ordinal] = cached;
        result = cached; hitChildren++; return true;
    }

    private static bool Usable(Session? current, XmlNode child, XmlNode parent)
    {
        if (current == null || current.Invalid || current.Thread != Thread.CurrentThread.ManagedThreadId) return false;
        if (!ReferenceEquals(child.OwnerDocument, current.Document) || !ReferenceEquals(parent.OwnerDocument, current.Document)
            || !Admitted() || !NoDocumentObservers(current.Document) || current.DuplicateFields != DuplicateFieldsIdentity())
        { current.Invalid = true; return false; }
        return true;
    }

    public static void Capture(XmlNode child, XmlNode resolvedParent, XmlNode result)
    {
        Session? current = session;
        if (!Usable(current, child, resolvedParent) || !current!.Ordinals.TryGetValue(child, out int ordinal)) return;
        if (result.ParentNode != null || !ReferenceEquals(result.OwnerDocument, current.Document) || current.Captured.ContainsKey(ordinal))
        { current.Invalid = true; return; }
        current.Captured.Add(ordinal, result);
        current.HasNativeResults = true;
    }

    private static void End(bool completed)
    {
        Session? current = session; session = null;
        if (current == null || !completed || current.Invalid || store == null || !Admitted() || !NoDocumentObservers(current.Document)) return;
        long phase = Clock();
        try
        {
            // A fully restored generation is already durable. Read marked it
            // used for maintenance; only newly computed native results require
            // another serialization and atomic publication.
            if (current.Captured.Count > 0 && (current.Restored == null || current.HasNativeResults)
                && CacheLaunchPolicy.Current.AllowWrite)
            {
                byte[] payload = EncodeGeneration(current.Captured);
                if (store.CanPublish(current.Key, payload.Length) && store.Publish(current.Key, payload)) generations++;
            }
        }
        catch (Exception e) { JsonLineLog.WriteReceipt(log, "generation-unstored", e.Message); }
        finally { publicationTicks += Elapsed(phase); }
    }

    private static byte[] RegistrationIdentity(Dictionary<XmlNode, int> ordinals, Dictionary<XmlNode, LoadableXmlAsset> map)
    {
        var type = typeof(XmlInheritance);
        var unresolved = (IEnumerable)AccessTools.Field(type, "unresolvedNodes").GetValue(null);
        var resolved = (IDictionary)AccessTools.Field(type, "resolvedNodes").GetValue(null);
        if (resolved.Count != 0) throw new InvalidDataException("inheritance-prior-resolution");
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        var known = new HashSet<object>();
        foreach (object registered in unresolved)
        {
            var nativeType = registered.GetType();
            var node = (XmlNode)AccessTools.Field(nativeType, "xmlNode").GetValue(registered);
            var mod = AccessTools.Field(nativeType, "mod").GetValue(registered);
            if (!ordinals.TryGetValue(node, out int ordinal) || !known.Add(registered)
                || !ReferenceEquals(mod, map.TryGetValue(node, out var asset) ? asset.mod : null))
                throw new InvalidDataException("inheritance-external-registration");
            if (AccessTools.Field(nativeType, "parent").GetValue(registered) != null
                || AccessTools.Field(nativeType, "resolvedXmlNode").GetValue(registered) != null
                || ((ICollection)AccessTools.Field(nativeType, "children").GetValue(registered)).Count != 0)
                throw new InvalidDataException("inheritance-prior-links");
            writer.Write(ordinal);
        }
        foreach (DictionaryEntry named in (IDictionary)AccessTools.Field(type, "nodesByName").GetValue(null))
            foreach (object registered in (IEnumerable)named.Value)
                if (!known.Contains(registered)) throw new InvalidDataException("inheritance-external-template");
        writer.Flush(); return output.ToArray();
    }

    internal static string InputKey(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map, byte[] registrations, string duplicateFields, string semanticIdentity)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write(semanticIdentity); writer.Write(duplicateFields);
        writer.Write(CacheDigest.Hash(ResolvedInheritanceFormat.EncodeRoots(document.ChildNodes.Cast<XmlNode>().ToArray())));
        writer.Write(registrations.Length); writer.Write(registrations);
        var roots = document.DocumentElement!.ChildNodes.Cast<XmlNode>().ToArray();
        writer.Write(roots.Length);
        foreach (var root in roots) WriteSource(writer, map.TryGetValue(root, out var source) ? source : null);
        // Detached removed source-map keys are part of the supplied dictionary;
        // sort their content/attribution identities, never runtime object hashes.
        var live = new HashSet<XmlNode>(roots);
        var detached = map.Where(p => !live.Contains(p.Key)).Select(pair =>
        {
            using var bytes = new MemoryStream(); using var record = new BinaryWriter(bytes, Encoding.UTF8, true);
            WriteSource(record, pair.Value); record.Write(CacheDigest.Hash(ResolvedInheritanceFormat.EncodeRoots(new[] { pair.Key })));
            record.Flush(); return Hex(CacheDigest.Hash(bytes.ToArray()));
        }).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        writer.Write(detached.Length); foreach (string item in detached) writer.Write(item);
        writer.Write(LoadedModManager.RunningModsListForReading.Count);
        foreach (var mod in LoadedModManager.RunningModsListForReading)
        {
            writer.Write(mod.PackageId); writer.Write(mod.Name); writer.Write(mod.RootDir); writer.Write(mod.loadOrder);
            writer.Write(mod.assemblies.loadedAssemblies.Count);
            foreach (var assembly in mod.assemblies.loadedAssemblies)
            { writer.Write(assembly.FullName); writer.Write(assembly.ManifestModule.ModuleVersionId.ToString()); }
        }
        writer.Flush(); return Hex(CacheDigest.Hash(output.ToArray()));
    }

    private static void WriteSource(BinaryWriter writer, LoadableXmlAsset? source)
    {
        writer.Write(source != null); if (source == null) return;
        writer.Write(source.name ?? ""); writer.Write(source.fullFolderPath ?? "");
        writer.Write(source.mod != null);
        if (source.mod != null) { writer.Write(source.mod.PackageId); writer.Write(source.mod.RootDir); writer.Write(source.mod.loadOrder); }
    }
    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

    internal static byte[] EncodeGeneration(Dictionary<int, XmlNode> captured)
    {
        var ordered = captured.OrderBy(p => p.Key).ToArray();
        byte[] trees = ResolvedInheritanceFormat.EncodeRoots(ordered.Select(p => p.Value).ToArray());
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write(1); writer.Write(ordered.Length);
        foreach (var pair in ordered) writer.Write(pair.Key);
        writer.Write(trees.Length); writer.Write(trees); writer.Flush();
        if (output.Length > ResolvedInheritanceFormat.MaximumPayload) throw new InvalidDataException("inheritance-generation-limit");
        return output.ToArray();
    }

    internal static Dictionary<int, XmlNode> DecodeGeneration(byte[] payload, XmlDocument document, int rootCount)
    {
        using var input = new MemoryStream(payload, false); using var reader = new BinaryReader(input, Encoding.UTF8, true);
        if (payload.Length > ResolvedInheritanceFormat.MaximumPayload || reader.ReadInt32() != 1) throw new InvalidDataException("inheritance-generation-format");
        int count = reader.ReadInt32();
        if (count < 0 || count > rootCount || count > ResolvedInheritanceFormat.MaximumRoots || count > (input.Length - input.Position) / 4)
            throw new InvalidDataException("inheritance-generation-count");
        var ordinals = new int[count];
        for (int i = 0; i < count; i++)
        {
            ordinals[i] = reader.ReadInt32();
            if (ordinals[i] < 0 || ordinals[i] >= rootCount || i > 0 && ordinals[i] <= ordinals[i - 1])
                throw new InvalidDataException("inheritance-generation-ordinal");
        }
        int length = reader.ReadInt32();
        if (length < 1 || length != input.Length - input.Position) throw new InvalidDataException("inheritance-generation-length");
        XmlNode[] roots = ResolvedInheritanceFormat.DecodeRoots(reader.ReadBytes(length), document);
        if (roots.Length != count || roots.Any(n => n.NodeType != XmlNodeType.Element || n.ParentNode != null)) throw new InvalidDataException("inheritance-generation-roots");
        var result = new Dictionary<int, XmlNode>();
        for (int i = 0; i < count; i++) result.Add(ordinals[i], roots[i]);
        return result;
    }

    private static void Menu()
    { if (PlayDataLoader.Loaded && !LongEventHandler.AnyEventNowOrWaiting) Finish(); }

    private static long Clock() => timing ? Stopwatch.GetTimestamp() : 0;
    private static long Elapsed(long start) => timing ? Stopwatch.GetTimestamp() - start : 0;
    private static string Milliseconds(long ticks) => (ticks * 1000d / Stopwatch.Frequency).ToString("F6", CultureInfo.InvariantCulture);

    internal static void Finish()
    {
        if (finished) return; finished = true; enabled = false; session = null;
        store?.Complete(); store?.Dispose(); store = null;
        Status = "Resolved inheritance: " + hitChildren + " child merges avoided, " + nativeChildren + " native child merges. Performance is not yet qualified.";
        JsonLineLog.WriteEvent(log, "complete", "\"hitChildren\":" + hitChildren + ",\"nativeChildren\":" + nativeChildren
            + ",\"generations\":" + generations + ",\"fallbackScopes\":" + fallbackScopes);
        if (timing) JsonLineLog.WriteEvent(log, "work", "\"registrationMs\":" + Milliseconds(registrationTicks)
            + ",\"inputKeyMs\":" + Milliseconds(keyTicks) + ",\"readAndRestoreMs\":" + Milliseconds(readTicks)
            + ",\"encodeAndPublishMs\":" + Milliseconds(publicationTicks)
            + ",\"coverage\":\"completed private phases inside inheritance; excludes native traversal and guards\"");
        Log.Message("[Wake-Up] " + Status);
    }
}
