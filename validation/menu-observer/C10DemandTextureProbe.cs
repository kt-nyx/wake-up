// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using HarmonyLib;
using RimWorld.IO;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Functional observer only: natural shared assets remain owned by native holders.
// Direct LoadItem controls are separate owned textures and are destroyed here.
internal static class C10DemandTextureProbe
{
    internal static bool Required => Environment.GetCommandLineArgs().Contains("--wake-up-demand-probe=on");
    internal static bool Completed { get; private set; }
    internal static bool Passed { get; private set; }
    private static Type runtime = null!, graphics = null!;
    private static readonly Dictionary<string, object> report = new();
    private static readonly List<object> samples = new(), graphicSamples = new();
    private static int missingPreparedCases;
    private static object Call(Type type, string name, params object[] args) => AccessTools.Method(type, name).Invoke(null, args)!;
    private static long Count(string name) => Convert.ToInt64(runtime.GetProperty(name)!.GetValue(null), CultureInfo.InvariantCulture);
    private static void Require(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
    private static ModContentPack Mod(string id) => LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == id);
    private static ModContentHolder<Texture2D> Holder(ModContentPack mod) => (ModContentHolder<Texture2D>)Call(runtime, "GetHolder", mod);
    private static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static string Describe(Texture2D t) => string.Join("/", t.width, t.height, (int)t.format, (int)t.graphicsFormat,
        t.mipmapCount, (int)t.filterMode, (int)t.wrapModeU, (int)t.wrapModeV, (int)t.wrapModeW, t.anisoLevel,
        t.mipMapBias.ToString("R", CultureInfo.InvariantCulture), t.isReadable);
    private static string Cpu(Texture2D t)
    {
        try { byte[] bytes = t.GetRawTextureData(); return bytes == null ? "native-null" : Hash(bytes); }
        catch (UnityException) { return "native-unreadable"; }
    }
    private static bool Small(string path)
    {
        // Only header metadata is inspected while selecting; no texture demand.
        try
        {
            using var file = File.OpenRead(path); var b = new byte[32]; if (file.Read(b, 0, b.Length) != b.Length) return false;
            int width, height;
            if (Path.GetExtension(path).Equals(".dds", StringComparison.OrdinalIgnoreCase) && BitConverter.ToUInt32(b, 0) == 0x20534444)
            { height = BitConverter.ToInt32(b, 12); width = BitConverter.ToInt32(b, 16); }
            else if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) && b[0] == 137 && b[1] == 80)
            { width = b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19]; height = b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23]; }
            else return false;
            return width > 0 && height > 0 && width <= 1024 && height <= 1024;
        }
        catch (IOException) { return false; }
    }
    private static string Folder(string path) { int i = path.LastIndexOf('/'); return i < 0 ? "" : path.Substring(0, i); }
    private static bool Winning(string[] row, string[][] pending)
    {
        var mods = LoadedModManager.RunningModsListForReading;
        int index = mods.IndexOf(Mod(row[0]));
        for (int i = index + 1; i < mods.Count; i++)
            if (Holder(mods[i]).contentList.ContainsKey(row[1]) || pending.Any(p => p[0] == mods[i].PackageId && p[1] == row[1])) return false;
        return true;
    }
    internal static void Start(string directory)
    {
        if (!Required || Completed) return;
        report["schema"] = "c10-demand-texture-functional.v1"; report["required"] = true;
        report["samples"] = samples; report["graphics"] = graphicSamples;
        report["coveragePending"] = new[] { "unknown direct/reflected consumers", "undeclared worker access beyond tested holder.Get", "gameplay and atlas lifecycle", "performance" };
        try
        {
            Assembly core = AppDomain.CurrentDomain.GetAssemblies().First(a => !a.ReflectionOnly && a.GetName().Name == "WakeUp");
            runtime = core.GetType("WakeUp.DemandTextureRuntime", true)!; graphics = core.GetType("WakeUp.DemandGraphicRuntime", true)!;
            bool enabled = (bool)runtime.GetProperty("Enabled")!.GetValue(null)!;
            report["enabled"] = enabled; report["runtimeAtMenuJson"] = Call(runtime, "Snapshot");
            var graphicsAtMenu = (Dictionary<string, object>)Call(graphics, "Snapshot");
            report["graphicsAtMenu"] = graphicsAtMenu;
            report["providersAtMenu"] = Call(runtime, "ProviderSnapshot");
            report["expandableAtMenu"] = Call(core.GetType("WakeUp.DemandExpandableTextureRuntime", true)!, "Snapshot");
            report["deferredAtMenu"] = Count("DeferredCount"); report["pendingAtMenu"] = Count("PendingCount");
            // Natural startup song callbacks also use the shared generic holder
            // methods. Their native references must survive texture demand mode.
            var songs = DefDatabase<SongDef>.AllDefsListForReading.Where(s => !string.IsNullOrEmpty(s.clipPath)).ToArray();
            var missingSongs = songs.Where(s => s.clip == null).Select(s => s.defName).ToArray();
            report["songCount"] = songs.Length; report["missingSongClips"] = missingSongs;
            Require(songs.Length > 0 && missingSongs.Length == 0, "Natural song initialization lost clips: " + string.Join(", ", missingSongs));
            string audioFolder = Folder(songs.OrderBy(s => s.clipPath, StringComparer.Ordinal).First().clipPath);
            var audioFiles = ContentFinder<AudioClip>.GetAllInFolder(audioFolder).ToArray();
            report["audioFolder"] = audioFolder;
            report["audioFolderClips"] = audioFiles.Select(c => c == null ? "<native-null>" : c.name).ToArray();
            report["audioFolderEnumerationPassed"] = true;
            var selected = new List<string[]>(); var defNames = new List<string>(); string folder;
            string? input = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--wake-up-demand-selection=", StringComparison.Ordinal));
            if (input != null)
            {
                var xml = new XmlDocument(); xml.Load(input.Substring("--wake-up-demand-selection=".Length));
                foreach (XmlElement e in xml.SelectNodes("/selection/texture")!) selected.Add(new[] { e.GetAttribute("package"), e.GetAttribute("path"), e.GetAttribute("source"), e.GetAttribute("sha256") });
                foreach (XmlElement e in xml.SelectNodes("/selection/graphic")!) defNames.Add(e.GetAttribute("def"));
                folder = xml.DocumentElement!.GetAttribute("folder");
            }
            else
            {
                Require(enabled, "Off control requires the carried natural selection XML.");
                var all = (string[][])Call(runtime, "PendingSources");
                var eligible = all.Where(r => Small(r[2]) && Winning(r, all)).OrderBy(r => r[0], StringComparer.Ordinal).ThenBy(r => r[1], StringComparer.Ordinal).ToArray();
                Require(eligible.Select(r => r[0]).Distinct().Count() >= 2, "Need pending natural textures from two providers.");
                // Prefer a small natural folder so enumeration remains bounded.
                var group = eligible.Where(r => Folder(r[1]) != "").GroupBy(r => Folder(r[1]))
                    .FirstOrDefault(g => g.Count() <= 3 && all.Count(r => r[1].StartsWith(g.Key + "/", StringComparison.Ordinal)) <= 3);
                Require(group != null, "No bounded natural folder available."); folder = group!.Key;
                selected.AddRange(group);
                foreach (string extension in new[] { ".png", ".dds" })
                {
                    var next = eligible.FirstOrDefault(r => Path.GetExtension(r[2]).Equals(extension, StringComparison.OrdinalIgnoreCase));
                    if (next != null && !selected.Any(s => s[1] == next[1])) selected.Add(next);
                }
                if (selected.Select(r => r[0]).Distinct().Count() < 2)
                    selected.Add(eligible.First(r => r[0] != selected[0][0]));
                if (selected.All(r => r[1].StartsWith(folder + "/", StringComparison.Ordinal)))
                    selected.Add(eligible.First(r => !r[1].StartsWith(folder + "/", StringComparison.Ordinal)));
                var bothPending = ((IEnumerable)graphicsAtMenu["rows"]).Cast<Dictionary<string, object>>()
                    .Where(r => (bool)r["graphicPending"] && (bool)r["iconPending"]).Select(r => (string)r["defName"]).ToArray();
                defNames.AddRange(((ThingDef[])Call(graphics, "PendingDefs")).Where(d => bothPending.Contains(d.defName))
                    .OrderBy(d => d.defName, StringComparer.Ordinal).Take(2).Select(d => d.defName));
                foreach (var row in selected) row[3] = Hash(File.ReadAllBytes(row[2]));
            }
            Require(selected.Count > 0 && selected.Count <= 6 && selected.Select(r => r[0]).Distinct().Count() >= 2, "Natural selection bounds/provider coverage.");
            Require(defNames.Count > 0, "No postponed natural graphic/icon selected.");
            SaveSelection(directory, selected, defNames, folder);
            if (enabled)
            {
                Require(Count("DeferredCount") > 0 && Count("PendingCount") > 0, "No real texture work remains deferred at menu.");
                Require((bool)graphicsAtMenu["active"] && Convert.ToInt64(graphicsAtMenu["capturedGraphics"]) > 0
                    && Convert.ToInt64(graphicsAtMenu["capturedIcons"]) > 0, "No real graphic/icon work was postponed.");
                var first = selected[0]; var holder = Holder(Mod(first[0]));
                Require(!holder.contentList.ContainsKey(first[1]), "Selected raw dictionary entry was already published.");
                bool missing = false; try { _ = holder.contentList[first[1]]; } catch (KeyNotFoundException) { missing = true; }
                Exception? workerError = null;
                var worker = new Thread(() => { try { holder.Get(first[1]); } catch (Exception e) { workerError = e; } }) { IsBackground = true };
                worker.Start(); Require(worker.Join(3000), "Undeclared worker lookup did not return while main waited.");
                Require(missing && workerError is InvalidOperationException && !holder.contentList.ContainsKey(first[1]), "Pending raw/worker refusal contract.");
                report["rawMissingAndWorkerRefusalPassed"] = true;
            }
            long folderBefore = Count("CompletedCount");
            var folderTextures = ContentFinder<Texture2D>.GetAllInFolder(folder).ToArray();
            Require(selected.Where(r => r[1].StartsWith(folder + "/", StringComparison.Ordinal)).All(r => folderTextures.Any(t => ReferenceEquals(t, Holder(Mod(r[0])).Get(r[1])))), "Folder enumeration omitted selected assets.");
            report["folder"] = folder; report["folderCount"] = folderTextures.Length;
            report["folderOrder"] = folderTextures.Select(t => t.name + "|" + Describe(t)).ToArray();
            report["folderCompletedDelta"] = Count("CompletedCount") - folderBefore;
            if (enabled) Require(Count("CompletedCount") > folderBefore, "Folder request did not complete pending textures.");
            foreach (var row in selected) CheckTexture(row);
            var paths = selected.Select(r => r[1]).ToArray();
            var readyHolders = selected.Select(r => Holder(Mod(r[0]))).ToArray();
            var prepared = (Texture2D[])Call(runtime, "PrepareWorker", (object)paths);
            bool readyRefs = false; Exception? readyError = null;
            var readyWorker = new Thread(() =>
            {
                try { readyRefs = selected.Select((r, i) => ReferenceEquals(readyHolders[i].Get(r[1]), prepared[i])).All(v => v); }
                catch (Exception e) { readyError = e; }
            }) { IsBackground = true };
            readyWorker.Start(); Require(readyWorker.Join(3000) && readyError == null && readyRefs, "Prepared worker reference/holder lookup mismatch: " + readyError);
            report["preparedWorkerReferencesPassed"] = true;
            foreach (string name in defNames) CheckGraphic(name);
            if (enabled)
            {
                var afterGraphics = (Dictionary<string, object>)Call(graphics, "Snapshot");
                Require(Convert.ToInt64(afterGraphics["completedGraphics"]) > Convert.ToInt64(graphicsAtMenu["completedGraphics"])
                    && Convert.ToInt64(afterGraphics["completedIcons"]) > Convert.ToInt64(graphicsAtMenu["completedIcons"]), "Natural requests did not complete postponed graphics and icons.");
                CheckPreparedRecords();
                Require(missingPreparedCases > 0, "No real absent prepared owner reached native fallback.");
                report["missingPreparedNativeFallbacks"] = missingPreparedCases;
            }
            if (enabled)
            {
                var remaining = (string[][])Call(runtime, "PendingSources");
                var provider = remaining.GroupBy(r => r[0]).OrderBy(g => g.Count()).FirstOrDefault();
                Require(provider != null, "No remaining provider for direct whole-holder completion control.");
                long eagerBefore = Count("EagerCount"); var mod = Mod(provider!.Key);
                var exposed = mod.GetContentHolder<Texture2D>();
                Require(ReferenceEquals(exposed, Holder(mod)) && provider.All(r => exposed.contentList.ContainsKey(r[1])) && Count("EagerCount") > eagerBefore, "Known whole-holder exposure did not complete its pending assets.");
                var enumerator = exposed.contentList.GetEnumerator();
                Require(enumerator.MoveNext(), "Completed provider was unexpectedly empty.");
                Require(ReferenceEquals(exposed, mod.GetContentHolder<Texture2D>()), "Repeated holder exposure changed identity.");
                enumerator.MoveNext(); // Must retain the native dictionary version.
                report["repeatedHolderPreservesEnumeration"] = true;
                report["wholeHolder"] = new Dictionary<string, object> { ["package"] = mod.PackageId, ["pendingBefore"] = provider.Count(), ["eagerDelta"] = Count("EagerCount") - eagerBefore };
            }
            report["runtimeAfterJson"] = Call(runtime, "Snapshot"); report["graphicsAfter"] = Call(graphics, "Snapshot");
            Passed = true;
        }
        catch (Exception e) { report["failure"] = e.ToString(); Log.Error("[FixtureMenuObserver] demand texture probe failed: " + e); }
        finally
        {
            Completed = true; report["completed"] = true; report["passed"] = Passed; report["functionalPassed"] = Passed;
            File.WriteAllText(Path.Combine(directory, "c10-demand-texture-probe.json"), C10NaturalAudioProbe.Json(Normalize(report)) + "\n", new UTF8Encoding(false));
        }
    }
    private static object? Normalize(object? value)
    {
        if (value is IDictionary map)
        {
            var result = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in map) result[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!] = Normalize(entry.Value)!;
            return result;
        }
        if (value is IEnumerable items && !(value is string)) return items.Cast<object>().Select(Normalize).ToArray();
        return value;
    }
    private static void SaveSelection(string directory, List<string[]> rows, List<string> defs, string folder)
    {
        using var xml = XmlWriter.Create(Path.Combine(directory, "c10-demand-texture-selection.xml"), new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) });
        xml.WriteStartElement("selection"); xml.WriteAttributeString("folder", folder);
        foreach (var row in rows)
        { xml.WriteStartElement("texture"); xml.WriteAttributeString("package", row[0]); xml.WriteAttributeString("path", row[1]); xml.WriteAttributeString("source", row[2]); xml.WriteAttributeString("sha256", row[3]); xml.WriteEndElement(); }
        foreach (string name in defs) { xml.WriteStartElement("graphic"); xml.WriteAttributeString("def", name); xml.WriteEndElement(); }
        xml.WriteEndElement();
    }
    private static void CheckTexture(string[] row, List<object>? destination = null)
    {
        var mod = Mod(row[0]);
        Require(Path.GetFullPath(row[2]).StartsWith(Path.GetFullPath(mod.RootDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Source outside selected fixture mod.");
        Require(Hash(File.ReadAllBytes(row[2])) == row[3], "Carried source changed.");
        var item = new Dictionary<string, object> { ["package"] = row[0], ["path"] = row[1], ["source"] = row[2], ["sourceSha256"] = row[3], ["rawPresentBefore"] = Holder(mod).contentList.ContainsKey(row[1]) };
        (destination ?? samples).Add(item);
        var pendingSource = ((string[][])Call(runtime, "PendingSources")).FirstOrDefault(r => r[0] == row[0] && r[1] == row[1]);
        long loadsBefore = Count("NativeLoads"), hitsBefore = Count("PreparedHits");
        Texture2D actual = ContentFinder<Texture2D>.Get(row[1]);
        if (pendingSource != null && Small(row[2]))
        {
            object? preparedStore = AccessTools.Field(runtime, "store").GetValue(null);
            if (preparedStore != null)
            {
                Type preparedRuntime = runtime.Assembly.GetType("WakeUp.PreparedTextureRuntime", true)!;
                string selection = (string)Call(preparedRuntime, "SelectionIdentity", mod, Array.Empty<KeyValuePair<string, FileInfo>>());
                string owner = (string)Call(preparedRuntime, "OwnerIdentity", selection, pendingSource[3], 0);
                bool absent = !(bool)AccessTools.Method(preparedStore.GetType(), "HasOwner").Invoke(preparedStore, new object[] { owner })!;
                if (absent)
                {
                    Require(Count("NativeLoads") == loadsBefore + 1 && Count("PreparedHits") == hitsBefore, "Absent prepared owner did not fall back natively.");
                    item["absentPreparedOwnerNativeFallback"] = true; missingPreparedCases++;
                }
            }
        }
        Require(actual != null && ReferenceEquals(actual, Holder(mod).Get(row[1])), "ContentFinder chose a different provider or failed.");
        Require(ReferenceEquals(actual, ContentFinder<Texture2D>.Get(row[1])), "Repeated request changed identity.");
        Type fileType = typeof(VirtualFile).Assembly.GetType("RimWorld.IO.FilesystemFile", true)!;
        var conversion = fileType.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == "op_Implicit" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(FileInfo));
        var file = (VirtualFile)conversion.Invoke(null, new object[] { new FileInfo(row[2]) })!;
        LoadedContentItem<Texture2D>? control = null;
        try
        {
            control = ModContentLoader<Texture2D>.LoadItem(file);
            Require(control?.contentItem != null && !ReferenceEquals(control.contentItem, actual), "Native LoadItem control failed or borrowed shared asset.");
            string descriptor = Describe(actual!), revision = C08TexturePixels.Revision(actual!), cpu = Cpu(actual!);
            item["descriptor"] = descriptor; item["revision"] = revision; item["cpu"] = cpu;
            Require(descriptor == Describe(control!.contentItem) && revision == C08TexturePixels.Revision(control.contentItem) && cpu == Cpu(control.contentItem), "Native descriptor/all-mip GPU/readability mismatch.");
            item["nativeEqual"] = true; item["repeatReferenceEqual"] = true;
        }
        finally
        {
            control?.extraDisposable?.Dispose();
            if (control?.contentItem != null && !ReferenceEquals(control.contentItem, actual) && !ReferenceEquals(control.contentItem, BaseContent.BadTex)) UnityEngine.Object.DestroyImmediate(control.contentItem);
        }
    }
    private static void CheckPreparedRecords()
    {
        Require(Environment.GetCommandLineArgs().Contains("--wake-up-prepared=0"), "Prepared fallback controls require native-quality prepared mode.");
        var pending = (string[][])Call(runtime, "PendingSources");
        var selected = pending.Where(r => Small(r[2]) && Winning(r, pending))
            .OrderBy(r => r[0], StringComparer.Ordinal).ThenBy(r => r[1], StringComparer.Ordinal).Take(4).ToArray();
        Require(selected.Length == 4, "Need four additional naturally pending PNG or DDS textures for prepared record controls.");
        object store = AccessTools.Field(runtime, "store").GetValue(null) ?? throw new InvalidOperationException("Demand prepared store was not opened by normal demand.");
        Type preparedType = runtime.Assembly.GetType("WakeUp.PreparedTextureRuntime", true)!;
        Type pngType = runtime.Assembly.GetType("WakeUp.PngRuntime", true)!;
        Type nativeType = runtime.Assembly.GetType("WakeUp.NativeTextureData", true)!;
        var controls = new List<object>(); report["preparedRecordCases"] = controls;
        string[] labels = { "valid", "stale-source-identity", "corrupt-native-payload", "unsupported-native-descriptor" };
        for (int i = 0; i < selected.Length; i++)
        {
            var sourceRow = selected[i]; byte[] bytes = File.ReadAllBytes(sourceRow[2]);
            string selection = (string)Call(preparedType, "SelectionIdentity", Mod(sourceRow[0]), Array.Empty<KeyValuePair<string, FileInfo>>());
            string owner = (string)Call(preparedType, "OwnerIdentity", selection, sourceRow[3], 0);
            string platform = (string)Call(pngType, "PreparationPlatform");
            string Identity(byte[] data) => (string)Call(preparedType, "Identity", selection, sourceRow[3], sourceRow[2], data, 0, platform, "");
            string identity = Identity(bytes), publishedIdentity = identity;
            object native = Call(pngType, "CapturePrepared", new FileInfo(sourceRow[2]), bytes, new Func<int, bool>(_ => true));
            Require(native != null, "Native preparation capture refused: " + sourceRow[2]);
            var receipt = new Dictionary<string, object> { ["case"] = labels[i], ["path"] = sourceRow[1], ["identity"] = identity };
            controls.Add(receipt);
            long hits = Count("PreparedHits"), loads = Count("NativeLoads"), refusals = Count("PreparedRefusals");
            try
            {
                AccessTools.Method(store.GetType(), "Invalidate").Invoke(store, new object[] { identity });
                bool published;
                if (i <= 1)
                {
                    if (i == 1) { var stale = (byte[])bytes.Clone(); stale[stale.Length - 1] ^= 1; publishedIdentity = Identity(stale); }
                    published = (bool)AccessTools.Method(store.GetType(), "Publish").Invoke(store, new[] { (object)publishedIdentity, native })!;
                }
                else
                {
                    // Keep the real indexed group/checksum valid so the normal
                    // native record decoder must reject the damaged payload.
                    byte[] payload = (byte[])Call(nativeType, "Encode", identity, native!);
                    if (i == 2) payload[0] ^= 1;
                    else Buffer.BlockCopy(BitConverter.GetBytes(-1), 0, payload, 8 + Encoding.UTF8.GetByteCount(identity) + 8, 4);
                    object grouped = AccessTools.Field(store.GetType(), "grouped").GetValue(store);
                    object ownerStore = AccessTools.Field(store.GetType(), "store").GetValue(store);
                    string key = Hash(Encoding.UTF8.GetBytes(identity)).ToUpperInvariant();
                    published = (bool)AccessTools.Method(grouped.GetType(), "Publish").Invoke(grouped,
                        new object[] { ownerStore, key, payload, false, CancellationToken.None, owner })!;
                }
                Require(published, "Prepared control publication failed: " + labels[i]);
                AccessTools.Method(store.GetType(), "Complete").Invoke(store, null);
                Require((bool)AccessTools.Method(store.GetType(), "HasOwner").Invoke(store, new object[] { owner })!, "Prepared control did not create a real owner mapping.");
                var outputs = new List<object>(); receipt["sample"] = outputs;
                CheckTexture(new[] { sourceRow[0], sourceRow[1], sourceRow[2], Hash(bytes) }, outputs);
                receipt["preparedHitsDelta"] = Count("PreparedHits") - hits;
                receipt["nativeLoadsDelta"] = Count("NativeLoads") - loads;
                receipt["preparedRefusalsDelta"] = Count("PreparedRefusals") - refusals;
                Require(i == 0 ? Count("PreparedHits") == hits + 1 && Count("NativeLoads") == loads
                    : Count("PreparedHits") == hits && Count("NativeLoads") == loads + 1 && Count("PreparedRefusals") > refusals,
                    "Prepared hit/fallback counters differ: " + labels[i]);
                receipt["passed"] = true;
            }
            finally
            {
                // This fixture-only control owns precisely this written record;
                // it does not remove unrelated cache groups or native sources.
                if (i != 0) AccessTools.Method(store.GetType(), "Invalidate").Invoke(store, new object[] { publishedIdentity });
            }
        }
        report["preparedRecordCasesPassed"] = true;
    }
    private static void CheckGraphic(string name)
    {
        var def = DefDatabase<ThingDef>.GetNamed(name);
        var row = new Dictionary<string, object> { ["def"] = name, ["graphicWasAbsent"] = ReferenceEquals(AccessTools.Field(typeof(GraphicData), "cachedGraphic").GetValue(def.graphicData), null), ["iconWasNativeInitial"] = ReferenceEquals(def.uiIcon, BaseContent.BadTex) };
        graphicSamples.Add(row);
        // This ordinary UI request must run the product demand path itself.
        Texture2D icon = Widgets.GetIconFor(def);
        Graphic graphic = def.graphicData.Graphic;
        Require(icon != null && graphic != null && ReferenceEquals(icon, Widgets.GetIconFor(def)) && ReferenceEquals(graphic, def.graphicData.Graphic), "Natural graphic/icon request was not ready and stable.");
        row["iconDescriptor"] = Describe(icon!); row["iconRevision"] = C08TexturePixels.Revision(icon!);
        row["graphicType"] = graphic!.GetType().FullName!; row["sameReferences"] = true;
        Material material = graphic.MatSingle;
        Require(material != null && ReferenceEquals(material, graphic.MatSingle) && material.mainTexture is Texture2D,
            "First natural material request was not complete and stable.");
        var materialTexture = (Texture2D)material!.mainTexture;
        row["materialShader"] = material.shader.name; row["materialRenderQueue"] = material.renderQueue;
        row["materialColor"] = string.Join("/", new[] { material.color.r, material.color.g, material.color.b, material.color.a }
            .Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
        row["materialTextureDescriptor"] = Describe(materialTexture);
        row["materialTextureRevision"] = C08TexturePixels.Revision(materialTexture);
        row["materialRepeatReferenceEqual"] = true;
        row["comparison"] = "Original natural definition; cross-run native-off comparison uses carried def name and icon revision.";
    }
}
