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

namespace WakeUp;

public static class ParsedXmlRuntime
{
    internal const string Owner = "wakeup.parsed-xml";
    internal const int MaximumSourceBytes = 32 * 1024 * 1024;
    internal const int ChunkBytes = 4 * 1024 * 1024;
    internal const int MaximumFiles = 8192;
    internal static string Status { get; private set; } = "Parsed XML reuse is off.";
    private static bool enabled, loading, finished;
    private static OwnedCacheStore? store;
    private static string? log;
    private static string semantics = "";
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static XmlDocument? destination;
    private static readonly Dictionary<LoadableXmlAsset, XmlNode[]> prepared = new();
    private static readonly HashSet<LoadableXmlAsset> restoredAssets = new();
    [ThreadStatic] private static FileInfo? stagedFile;
    [ThreadStatic] private static XmlDocument? stagedDocument;
    [ThreadStatic] private static bool stagedRestored;
    private static int hitFiles, parsedFiles, fallbackFiles, importedRoots, fusedRoots, batches, unsupportedFiles;
    private static bool fusionValid;
    private static bool timing;
    private static long captureTicks, cacheReadTicks, restoreTicks, buildTicks, publishTicks, constructorTicks;
    private static readonly XmlReaderSettings Settings = new() { IgnoreComments = true, IgnoreWhitespace = true, CheckCharacters = false, Async = false };

    internal static void Initialize(string[] args, string saveDataRoot)
    {
        log = Path.Combine(saveDataRoot, "WakeUp", "parsed-xml.jsonl");
        timing = LoadingTimingRuntime.Selected(args);
        var policy = CacheLaunchPolicy.Current;
        string root = Path.Combine(saveDataRoot, "WakeUp", "ParsedXml", "v1");
        bool selected = StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
            && args.Count(a => a.StartsWith("--wake-up-parsed-xml=", StringComparison.Ordinal)) == 1
            && args.Contains("--wake-up-parsed-xml=on");
        if (!selected && policy.Action != CacheAction.Clear) return;
        try
        {
            var shared = SharedCacheBudget.ForRoot(root);
            store = new OwnedCacheStore(root, ParsedXmlFormat.MaximumPayload, shared?.MaximumBytes ?? 1024L * 1024 * 1024, policy);
            if (policy.Action == CacheAction.Clear)
            {
                JsonLineLog.WriteEvent(log, "clear", "\"remainingBytes\":" + store.StoredBytes);
                store.Dispose(); store = null; Status = "Parsed XML cache cleared; native loading this launch."; return;
            }
            if (!selected || !policy.AllowRead && !policy.AllowWrite) { store.Dispose(); store = null; Status = "Parsed XML cache bypassed this launch."; return; }
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string why)) throw new InvalidOperationException(why);
            if (PlayDataLoader.Loaded) throw new InvalidOperationException("startup already ended");
            guards = CreateGuards().Concat(OrderedInputRuntime.CreateIntervalGuards()).ToArray();
            semantics = typeof(LoadedModManager).Assembly.ManifestModule.ModuleVersionId + "|" + typeof(XmlDocument).Assembly.FullName
                + "|" + typeof(XmlDocument).Module.ModuleVersionId + "|" + typeof(XmlDictionaryReader).Module.ModuleVersionId
                + "|parsed-selected-source-v2|utf8-bom|ignore-comments-whitespace|check-characters-false|dtd-prohibit";
            enabled = true;
            new Harmony(Owner).Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(ParsedXmlRuntime), nameof(Menu)));
            Status = "Parsed XML reuse selected; waiting for supported selected sources.";
            JsonLineLog.WriteEvent(log, "selected", "\"semantics\":" + JsonLineLog.Quote(semantics));
        }
        catch (Exception e)
        {
            enabled = false; store?.Dispose(); store = null;
            Status = "Parsed XML reuse refused: " + e.Message + ". Native loading remains.";
            JsonLineLog.WriteReceipt(log, "refused", e.Message);
        }
        Log.Message("[Wake-Up] " + Status);
    }

    internal static PublishedPatchGuard[] CreateGuards()
    {
        var constructor = typeof(LoadableXmlAsset).GetConstructor(new[] { typeof(FileInfo), typeof(ModContentPack) })!;
        if (!SemanticMethodIdentity.TryHash(constructor, out string ctorHash, out _) || ctorHash != StreamingXmlRuntime.EffectiveConstructorBody)
            throw new InvalidOperationException("coordinated file constructor unavailable");
        var direct = AccessTools.Method(typeof(DirectXmlLoader), "XmlAssetsInModFolder");
        var all = AccessTools.Method(typeof(LoadedModManager), "LoadAllActiveMods");
        foreach (var pair in new[] { (direct, EffectiveFiles), (all, EffectiveAll), (AccessTools.Method(typeof(DirectXmlLoader), "WakeUpSelectXmlFiles"), OrderedInputRuntime.SelectorBody) })
            if (!SemanticMethodIdentity.TryHash(pair.Item1, out string hash, out _) || hash != pair.Item2)
                throw new InvalidOperationException("selected-source boundary changed: " + pair.Item1.Name);
        var defs = typeof(ModContentPack).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Single(t => t.Name.StartsWith("<LoadDefs>", StringComparison.Ordinal)).GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        foreach (var pair in new[] {
            (AccessTools.Method(typeof(LoadedModManager), "LoadModXML"), "F4F01F65D5D6525E20BADB278F7F5EB0DA09468769D3329809CAC90517299051"),
            (AccessTools.Method(typeof(LoadedModManager), "CombineIntoUnifiedXML"), "4F9ECDC5FE541B25238FE5E34E7229DB87C332C73F177FCFFDF654B94F827ED3"),
            (AccessTools.Method(typeof(ModContentPack), "LoadDefs"), "F13471DAFA5A7729F11C7F7FC698BFB893C1482F993A753CEA2481052C557D5C"),
            (defs, "699C88A50199DBE5990FEA48C8531B9EEA11671E050215C1031B4B9D61E7FA75") })
            if (!SemanticMethodIdentity.TryHash(pair.Item1, out string hash, out _) || hash != pair.Item2)
                throw new InvalidOperationException("source ownership boundary changed: " + pair.Item1.Name);
        var targets = new MethodBase[] { direct, all, defs, constructor, AccessTools.Method(typeof(DirectXmlLoader), "WakeUpSelectXmlFiles"),
            AccessTools.Method(typeof(ModContentPack), "LoadDefs"), AccessTools.Method(typeof(LoadedModManager), "LoadModXML"),
            AccessTools.Method(typeof(LoadedModManager), "CombineIntoUnifiedXML"),
            typeof(XmlDocument).GetMethod("ImportNode")!, typeof(XmlDocument).GetMethod("CreateElement", new[] { typeof(string) })!,
            typeof(XmlNode).GetMethod("AppendChild")!, typeof(XmlElement).GetMethod("SetAttribute", new[] { typeof(string), typeof(string) })! };
        // The private loader uses native factories. A foreign observer on any
        // invoked factory keeps parsing and construction on the native path.
        var factories = typeof(XmlDocument).GetMethods().Where(m => m.Name == "CreateElement" || m.Name == "CreateAttribute"
            || m.Name == "CreateTextNode" || m.Name == "CreateXmlDeclaration" || m.Name == "PrependChild" || m.Name == "set_PreserveWhitespace"
            || m.Name == "CreateCDataSection" || m.Name == "CreateProcessingInstruction" || m.Name == "CreateSignificantWhitespace"
            || m.Name == "CreateWhitespace" || m.Name == "CreateComment")
            .Cast<MethodBase>().Concat(typeof(XmlDictionaryReader).GetMethods().Where(m => m.Name == "CreateBinaryReader"))
            .Concat(typeof(XmlDictionaryWriter).GetMethods().Where(m => m.Name == "CreateBinaryWriter"))
            .Concat(new MethodBase[] { typeof(XmlElement).GetProperty("IsEmpty")!.GetSetMethod()!,
                typeof(XmlNodeReader).GetConstructor(new[] { typeof(XmlNode) })!,
                typeof(XmlAttributeCollection).GetMethod("Append")!, typeof(XmlConvert).GetMethod("VerifyNCName", new[] { typeof(string) })! });
        return StreamingXmlRuntime.CreateGuards(constructor).Concat(targets.Concat(factories).Distinct().Select(m =>
            PublishedPatchGuard.TryCreate(m, Owner, out var guard, true, AllowedObservation)
                ? guard! : throw new InvalidOperationException("XML observer guard unavailable"))).ToArray();
    }
    internal const string EffectiveFiles = "642CF72FBA21646FD7F3F277B4773CFC4BC8ABB9E2ACF5FEDBAFD78EB0680D7A";
    internal const string EffectiveAll = "866BB660442698A5CFC8DBF00E5AE64B7A3F02BEB3E5E520637A8D84C0BDF6CE";

    private static bool AllowedObservation(Patch patch)
    {
        // Exact local callbacks only; owner names alone never grant admission.
        return patch.PatchMethod == AccessTools.Method(typeof(LoadingTimingRuntime), nameof(LoadingTimingRuntime.BeforeFiles))
            || patch.PatchMethod == AccessTools.Method(typeof(LoadingTimingRuntime), nameof(LoadingTimingRuntime.After))
            || patch.PatchMethod == AccessTools.Method(typeof(StreamingXmlRuntime), "Begin")
            || patch.PatchMethod == AccessTools.Method(typeof(StreamingXmlRuntime), "End");
    }
    private static bool Admitted() => guards.Length != 0 && guards.All(g => g.AllowsOriginalContract())
        && OrderedInputRuntime.IntervalCallbacksAllowed();

    public static List<LoadableXmlAsset> LoadSources(bool hotReload)
    {
        LoaderSupplierPolicy.BeforeXml();
        EarlyLoadingObservation.Finish();
        prepared.Clear(); restoredAssets.Clear(); destination = null; fusionValid = false;
        // Optional supplier discovery runs in native LoadModXML prefixes. Its
        // exact late absence/refusal may remove hooks before file consumption.
        loading = enabled && !finished && !hotReload;
        if (loading) { destination = new XmlDocument(); destination.AppendChild(destination.CreateElement("Defs")); fusionValid = true; }
        OrderedInputRuntime.Begin(hotReload);
        try { return LoadedModManager.LoadModXML(hotReload); }
        finally { OrderedInputRuntime.End(); loading = false; if (hotReload) JsonLineLog.WriteEvent(log, "hot-reload-native"); }
    }

    public static bool TryTake(FileInfo file, XmlReaderSettings settings, out XmlDocument document)
    {
        document = null!;
        if (!ReferenceEquals(stagedFile, file) || stagedDocument == null) return false;
        if (!Admitted() || !NativeSettings(settings)) { OrderedInputRuntime.Stop("parsed-constructor-guard"); fusionValid = false; fallbackFiles++; return false; }
        if (stagedRestored) hitFiles++;
        document = stagedDocument; stagedFile = null; stagedDocument = null; return true;
    }

    internal static bool NativeSettings(XmlReaderSettings settings)
        => settings.IgnoreComments && settings.IgnoreWhitespace && !settings.CheckCharacters && !settings.Async
            && !settings.CloseInput && !settings.IgnoreProcessingInstructions && settings.NameTable == null
            && settings.DtdProcessing == DtdProcessing.Prohibit && settings.ValidationType == System.Xml.ValidationType.None
            && settings.ConformanceLevel == ConformanceLevel.Document && settings.MaxCharactersInDocument == Settings.MaxCharactersInDocument
            && settings.MaxCharactersFromEntities == Settings.MaxCharactersFromEntities && settings.Schemas.Count == 0;

    public static bool TryLoadSelected(ModContentPack mod, string folder, List<FileInfo> files, out LoadableXmlAsset[] assets)
    {
        assets = null!;
        if (!loading || folder != "Defs/" || store == null || destination == null)
            return folder == "Defs/" && OrderedInputRuntime.TryLoadSelected(mod, files, out assets);
        if (!Admitted()) { OrderedInputRuntime.Stop("parsed-publication-guard"); fusionValid = false; fallbackFiles += files.Count; JsonLineLog.WriteReceipt(log, "batch-native", "source-observer-changed"); return false; }
        if (files.Count > MaximumFiles) { fallbackFiles += files.Count; return OrderedInputRuntime.TryLoadSelected(mod, files, out assets); }
        var documents = new XmlDocument?[files.Count];
        var restored = new bool[files.Count];
        // All source capture/restoration is private. Last safe whole-batch fallback
        // is BEFORE the first native asset constructor. No callback is replayed.
        try
        {
            long total = files.Sum(f => f.Length);
            if (total > MaximumSourceBytes || files.Any(f => f.Length < 1 || f.Length > StreamingXmlRuntime.MaxFileBytes))
            { OrderedInputRuntime.Stop("parsed-source-bounds"); fallbackFiles += files.Count; return false; }
            string selection = SelectionIdentity(mod, folder, files);
            int start = 0;
            while (start < files.Count)
            {
                int count = 0; long size = 0;
                do
                {
                    size += files[start + count].Length; count++;
                } while (start + count < files.Count && size + files[start + count].Length <= ChunkBytes && count < 512);
                var bytes = new byte[count][]; var digests = new byte[count][];
                // Match the native per-mod capacity (two workers plus caller).
                // C03 already owns bounded future-input workers: never nest them.
                bool parallel = !OrderedInputRuntime.HasQueue;
                long phase = Clock();
                ParsedXmlJobs.Run(count, i =>
                {
                    FileInfo file = files[start + i];
                    byte[] snapshot;
                    using (var inputSnapshot = OrderedInputRuntime.Take(file))
                    {
                        if (inputSnapshot != null)
                        {
                            snapshot = inputSnapshot.Bytes; digests[i] = inputSnapshot.Digest;
                            OrderedInputRuntime.ConsumedSnapshot();
                        }
                        else
                        {
                            OwnedCacheStore.RejectLinkedPath(file.FullName);
                            using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                            if (input.Length != file.Length || input.Length > StreamingXmlRuntime.MaxFileBytes) throw new IOException("source changed before capture");
                            snapshot = new byte[(int)input.Length];
                            int position = 0;
                            while (position < snapshot.Length) { int read = input.Read(snapshot, position, snapshot.Length - position); if (read == 0) throw new EndOfStreamException(); position += read; }
                            digests[i] = CacheDigest.HashInput(snapshot);
                        }
                    }
                    bytes[i] = snapshot;
                }, parallel);
                captureTicks += Elapsed(phase);
                string key = KeyDigests(selection, start, bytes.Select(b => b.Length).ToArray(), digests);
                phase = Clock();
                var cached = store.Read(key, payload => RestoreBatch(payload, count, parallel));
                cacheReadTicks += Elapsed(phase);
                bool hit = cached != null;
                var encoded = cached?.Records ?? new byte[]?[count];
                bool canWrite = !hit && store.CanPublish(key, ParsedXmlFormat.MaximumPayload);
                var parsedSlots = new bool[count]; var unsupportedSlots = new bool[count];
                var missing = new List<int>();
                phase = Clock();
                for (int i = 0; i < count; i++)
                {
                    if (cached?.Sources[i] != null)
                    {
                        documents[start + i] = cached.Sources[i]; restored[start + i] = true;
                    }
                    else missing.Add(i);
                }
                ParsedXmlJobs.Run(missing.Count, slot =>
                {
                    int i = missing[slot];
                    var parsed = ParsedXmlFormat.Parse(bytes[i], Settings); parsedSlots[i] = true;
                    byte[]? record = canWrite ? ParsedXmlFormat.Encode(parsed) : null;
                    if (record != null)
                    {
                        // First construction validates fidelity before durable
                        // publication. A shape the platform codec changes is
                        // retained natively, not silently normalized.
                        var check = ParsedXmlFormat.RestoreSource(record);
                        if (ParsedXmlFormat.Equal(parsed, check)) encoded[i] = record;
                    }
                    unsupportedSlots[i] = encoded[i] == null;
                    documents[start + i] = parsed;
                }, parallel);
                parsedFiles += parsedSlots.Count(value => value);
                unsupportedFiles += unsupportedSlots.Count(value => value);
                buildTicks += Elapsed(phase);
                phase = Clock();
                if (canWrite) { byte[] payload = EncodeBatch(encoded!); if (payload.Length <= ParsedXmlFormat.MaximumPayload) store.Publish(key, payload); }
                publishTicks += Elapsed(phase);
                batches++;
                JsonLineLog.WriteEvent(log, "selected-batch", "\"package\":" + JsonLineLog.Quote(mod.PackageId)
                    + ",\"key\":" + JsonLineLog.Quote(key) + ",\"start\":" + start + ",\"files\":" + count + ",\"warm\":" + (hit ? "true" : "false"));
                start += count;
            }
            if (!Admitted()) { OrderedInputRuntime.Stop("parsed-publication-guard"); fusionValid = false; fallbackFiles += files.Count; return false; }
        }
        catch (Exception e) { OrderedInputRuntime.Stop("parsed-batch-fallback"); fallbackFiles += files.Count; JsonLineLog.WriteReceipt(log, "batch-native", e.GetType().Name); return false; }
        // No catch-and-native-retry beyond this point: constructors may publish
        // callbacks. Their original attribution fields and exception path run.
        assets = new LoadableXmlAsset[files.Count];
        long constructorsStart = Clock();
        for (int i = 0; i < files.Count; i++)
        {
            try
            {
                stagedFile = files[i]; stagedDocument = documents[i]; stagedRestored = restored[i];
                var asset = new LoadableXmlAsset(files[i], mod); assets[i] = asset;
            }
            catch { OrderedInputRuntime.Stop("parsed-constructor-failed"); throw; }
            finally { stagedFile = null; stagedDocument = null; stagedRestored = false; }
        }
        constructorTicks += Elapsed(constructorsStart);
        return true;
    }

    private static XmlNode[] NativeRoots(XmlDocument source, XmlDocument owner)
        => source.DocumentElement!.ChildNodes.Cast<XmlNode>().Select(n => owner.ImportNode(n, true)).ToArray();

    public static XmlDocument Combine(List<LoadableXmlAsset> sources, ref Dictionary<XmlNode, LoadableXmlAsset> lookup)
    {
        XmlDocument result = CombineCore(sources, ref lookup);
        LoadingDiagnosticsRuntime.Capture("combined-before-patches", result, lookup);
        return result;
    }

    private static XmlDocument CombineCore(List<LoadableXmlAsset> sources, ref Dictionary<XmlNode, LoadableXmlAsset> lookup)
    {
        var output = destination; destination = null;
        if (!enabled || !fusionValid || output == null || !Admitted())
        {
            prepared.Clear(); JsonLineLog.WriteEvent(log, "native-combination");
            return LoadedModManager.CombineIntoUnifiedXML(sources, lookup);
        }
        var map = new Dictionary<XmlNode, LoadableXmlAsset>();
        var diagnostics = new List<string>();
        try
        {
            foreach (var asset in sources)
            {
                if (asset.xmlDoc == null || asset.xmlDoc.DocumentElement == null)
                { diagnostics.Add(asset.fullFolderPath + "/" + asset.name + ": unknown parse failure"); continue; }
                if (asset.xmlDoc.DocumentElement.Name != "Defs") diagnostics.Add(asset.fullFolderPath + "/" + asset.name
                    + ": root element named " + asset.xmlDoc.DocumentElement.Name + "; should be named Defs");
                if (!prepared.TryGetValue(asset, out var roots)) { roots = NativeRoots(asset.xmlDoc, output); importedRoots += roots.Length; }
                else if (restoredAssets.Contains(asset)) fusedRoots += roots.Length;
                else importedRoots += roots.Length;
                foreach (var root in roots) { map[root] = asset; output.DocumentElement!.AppendChild(root); }
            }
        }
        catch { prepared.Clear(); return LoadedModManager.CombineIntoUnifiedXML(sources, lookup); }
        prepared.Clear();
        if (!Admitted()) return LoadedModManager.CombineIntoUnifiedXML(sources, lookup);
        // Native diagnostics occur at combination, after all sources exist.
        // After the first diagnostic no retry can duplicate its observable effect.
        foreach (string diagnostic in diagnostics) Log.Error(diagnostic);
        lookup = map;
        JsonLineLog.WriteEvent(log, "combined", "\"sources\":" + sources.Count + ",\"mappedRoots\":" + map.Count
            + ",\"textParsesAvoided\":" + hitFiles + ",\"preparedRoots\":" + fusedRoots + ",\"nativeImportedRoots\":" + importedRoots);
        return output;
    }

    internal static string Key(string selection, int start, IReadOnlyList<byte[]> bytes)
        => KeyDigests(selection, start, bytes.Select(b => b.Length).ToArray(), bytes.Select(CacheDigest.Hash).ToArray());
    internal static string KeyDigests(string selection, int start, IReadOnlyList<int> lengths, IReadOnlyList<byte[]> digests)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data, Encoding.UTF8, true);
        writer.Write(selection); writer.Write(start); writer.Write(lengths.Count);
        for (int i = 0; i < lengths.Count; i++) { writer.Write(lengths[i]); writer.Write(digests[i]); }
        writer.Flush(); return Hex(CacheDigest.Hash(data.ToArray()));
    }
    private static string SelectionIdentity(ModContentPack mod, string folder, List<FileInfo> files)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data, Encoding.UTF8, true);
        writer.Write(semantics); writer.Write(mod.PackageId); writer.Write(mod.RootDir); writer.Write(folder);
        writer.Write(LoadedModManager.RunningModsListForReading.Count);
        foreach (var active in LoadedModManager.RunningModsListForReading) { writer.Write(active.PackageId); writer.Write(active.RootDir); }
        writer.Write(mod.foldersToLoadDescendingOrder.Count);
        foreach (string path in mod.foldersToLoadDescendingOrder) writer.Write(path);
        writer.Write(files.Count);
        foreach (var file in files) writer.Write(file.FullName);
        writer.Flush(); return Hex(CacheDigest.Hash(data.ToArray()));
    }
    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
    internal static byte[] EncodeBatch(byte[]?[] records)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write(1); writer.Write(records.Length);
        foreach (var record in records) { writer.Write(record?.Length ?? 0); if (record != null) writer.Write(record); }
        writer.Flush(); return output.ToArray();
    }
    internal static byte[]?[] DecodeBatch(byte[] payload, int count)
    {
        using var input = new MemoryStream(payload, false); using var reader = new BinaryReader(input);
        if (reader.ReadInt32() != 1 || reader.ReadInt32() != count || count > 512) throw new InvalidDataException("xml-batch-schema");
        var result = new byte[]?[count];
        for (int i = 0; i < count; i++)
        {
            int size = reader.ReadInt32();
            if (size < 0 || size > input.Length - input.Position) throw new InvalidDataException("xml-batch-layout");
            if (size > 0) result[i] = reader.ReadBytes(size);
        }
        if (input.Position != input.Length) throw new InvalidDataException("xml-batch-trailing");
        return result;
    }
    private sealed class RestoredBatch
    {
        internal byte[]?[] Records = null!;
        internal XmlDocument?[] Sources = null!;
    }
    private static RestoredBatch RestoreBatch(byte[] payload, int count, bool parallel)
    {
        long start = Clock();
        try
        {
            var records = DecodeBatch(payload, count);
            var sources = new XmlDocument?[count];
            ParsedXmlJobs.Run(count, i => { if (records[i] != null) sources[i] = ParsedXmlFormat.RestoreSource(records[i]!); }, parallel);
            return new RestoredBatch { Records = records, Sources = sources };
        }
        catch (Exception e) when (e is XmlException || e is ArgumentException || e is InvalidOperationException || e is IOException)
        { throw new InvalidDataException("xml-tree-layout", e); }
        finally { restoreTicks += Elapsed(start); }
    }
    private static long Clock() => timing ? Stopwatch.GetTimestamp() : 0;
    private static long Elapsed(long start) => timing ? Stopwatch.GetTimestamp() - start : 0;
    private static string Milliseconds(long ticks) => (ticks * 1000d / Stopwatch.Frequency).ToString("F6", CultureInfo.InvariantCulture);
    private static void Menu()
    {
        if (!PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting) return;
        Finish();
    }
    internal static void Finish()
    {
        if (finished) return; finished = true; enabled = false; loading = false;
        prepared.Clear(); restoredAssets.Clear(); destination = null; store?.Complete(); store?.Dispose(); store = null;
        Status = "Parsed XML: " + hitFiles + " source parses avoided, " + parsedFiles + " files parsed, " + fallbackFiles + " native fallbacks. Performance is not yet qualified.";
        JsonLineLog.WriteEvent(log, "complete", "\"hitFiles\":" + hitFiles + ",\"parsedFiles\":" + parsedFiles
            + ",\"fallbackFiles\":" + fallbackFiles + ",\"unsupportedOrUnstoredFiles\":" + unsupportedFiles + ",\"batches\":" + batches + ",\"preparedRoots\":" + fusedRoots);
        if (timing) JsonLineLog.WriteEvent(log, "work", "\"captureMs\":" + Milliseconds(captureTicks)
            + ",\"cacheReadIncludingRestoreMs\":" + Milliseconds(cacheReadTicks) + ",\"restoreNestedMs\":" + Milliseconds(restoreTicks)
            + ",\"coldBuildMs\":" + Milliseconds(buildTicks) + ",\"publishMs\":" + Milliseconds(publishTicks)
            + ",\"constructorsMs\":" + Milliseconds(constructorTicks) + ",\"coverage\":\"completed private phases; nested restore is not additive\"");
        Log.Message("[Wake-Up] " + Status);
    }
}
