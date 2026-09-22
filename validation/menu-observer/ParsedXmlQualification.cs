// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using HarmonyLib;
using Verse;

namespace FixtureMenuObserver;

// Standard functional evidence starts AFTER source combination. A separate
// explicit adversarial option exercises a foreign LoadDefs source observer.
internal static class ParsedXmlQualification
{
    private const string Owner = "local.fixture.menuobserver.parsedxml";
    private const string SourceOwner = "local.fixture.menuobserver.retained-source";
    private const string SourceMarker = " [C01 observer]";
    private static string? path;
    private static bool combined, prePatch, postPatch, completed;
    private static readonly Dictionary<LoadableXmlAsset, string> sourceBefore = new();
    private static bool sourceObserverSelected, retainedCombinationPassed;
    private static LoadableXmlAsset? retainedAsset;
    private static XmlDocument? retainedDocument;
    private static XmlNode? retainedRoot;
    private static XmlElement? retainedLabel;
    private static XmlText? retainedText;
    private static string? retainedExpected;

    internal static void Install(string directory)
    {
        path = Path.Combine(directory, "parsed-xml-qualification.jsonl");
        var harmony = new Harmony(Owner);
        try
        {
            harmony.Patch(AccessTools.Method(typeof(TKeySystem), "Parse"),
                prefix: new HarmonyMethod(typeof(ParsedXmlQualification), nameof(Combined)) { priority = Priority.Last });
            harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "ApplyPatches"),
                prefix: new HarmonyMethod(typeof(ParsedXmlQualification), nameof(BeforePatches)) { priority = Priority.Last });
            harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "ParseAndProcessXML"),
                prefix: new HarmonyMethod(typeof(ParsedXmlQualification), nameof(BeforeDefs)) { priority = Priority.Last });
            Write("installed", "\"coverage\":\"combined XML, mapped sources, and selected final Def fields; no gameplay or performance claim\"");
            if (Environment.GetCommandLineArgs().Contains("--fixture-functional-probes")
                && Environment.GetCommandLineArgs().Contains("--fixture-parsed-source-observer"))
                InstallSourceObserver();
        }
        catch (Exception e) { try { harmony.UnpatchAll(Owner); } catch { } Failure("installation", e); }
    }

    private static void InstallSourceObserver()
    {
        sourceObserverSelected = true;
        var harmony = new Harmony(SourceOwner);
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ModContentPack), "LoadDefs"),
                postfix: new HarmonyMethod(typeof(ParsedXmlQualification), nameof(ObserveSources)));
            Write("retained-source-installed", "\"owner\":" + Quote(SourceOwner) + ",\"targetDef\":\"WoodLog\"");
        }
        catch (Exception e) { try { harmony.UnpatchAll(SourceOwner); } catch { } Failure("retained-source-installation", e); }
    }

    private static void ObserveSources(bool __0, ref IEnumerable<LoadableXmlAsset> __result)
    {
        if (__0 || !sourceObserverSelected || completed || __result == null) return;
        __result = RetainSources(__result);
    }

    private static IEnumerable<LoadableXmlAsset> RetainSources(IEnumerable<LoadableXmlAsset> original)
    {
        // Enumerate the original exactly once, preserving order and its own
        // exceptions/disposal. Change only an existing in-memory text node.
        foreach (LoadableXmlAsset asset in original)
        {
            if (retainedAsset == null)
            {
                try
                {
                    XmlNode? root = asset.xmlDoc?.DocumentElement?.SelectSingleNode("ThingDef[defName='WoodLog']");
                    XmlElement? label = root?["label"];
                    if (label != null && label.ChildNodes.Count == 1 && label.FirstChild is XmlText text && !string.IsNullOrEmpty(text.Value))
                    {
                        retainedExpected = text.Value + SourceMarker;
                        retainedAsset = asset; retainedDocument = asset.xmlDoc; retainedRoot = root; retainedLabel = label; retainedText = text;
                        text.Value = retainedExpected;
                        Write("retained-source-mutated", "\"package\":" + Quote(asset.mod?.PackageId) + ",\"name\":" + Quote(asset.name)
                            + ",\"fullFolderPath\":" + Quote(asset.fullFolderPath) + ",\"expectedLabel\":" + Quote(retainedExpected));
                    }
                }
                catch (Exception e) { Failure("retained-source-mutation", e); }
            }
            yield return asset;
        }
    }

    private static bool RetainedSourceIntact() => retainedAsset != null && retainedDocument != null && retainedRoot != null
        && retainedLabel != null && retainedText != null
        && ReferenceEquals(retainedAsset.xmlDoc, retainedDocument)
        && ReferenceEquals(retainedDocument.DocumentElement?.SelectSingleNode("ThingDef[defName='WoodLog']"), retainedRoot)
        && ReferenceEquals(retainedRoot["label"], retainedLabel) && ReferenceEquals(retainedLabel.FirstChild, retainedText)
        && retainedText.Value == retainedExpected;

    private static void CheckRetainedCombination(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map)
    {
        if (!sourceObserverSelected) return;
        try
        {
            XmlNode[] matches = document.DocumentElement!.ChildNodes.Cast<XmlNode>()
                .Where(n => n.Name == "ThingDef" && n["defName"]?.InnerText == "WoodLog").ToArray();
            XmlNode? root = matches.Length == 1 ? matches[0] : null;
            bool sourceIntact = RetainedSourceIntact();
            bool attribution = root != null && map.TryGetValue(root, out LoadableXmlAsset asset) && ReferenceEquals(asset, retainedAsset);
            bool distinct = root != null && !ReferenceEquals(document, retainedDocument) && !ReferenceEquals(root, retainedRoot)
                && !ReferenceEquals(root["label"], retainedLabel) && ReferenceEquals(root.OwnerDocument, document);
            bool marker = root?["label"]?.InnerText == retainedExpected && retainedExpected != null;
            retainedCombinationPassed = sourceIntact && attribution && distinct && marker;
            Write("retained-source-combination", "\"passed\":" + Bool(retainedCombinationPassed) + ",\"sourceFound\":" + Bool(retainedAsset != null)
                + ",\"sameSourceAssetAndNodes\":" + Bool(sourceIntact) + ",\"sameSourceAttribution\":" + Bool(attribution)
                + ",\"distinctCombinedNodes\":" + Bool(distinct) + ",\"combinedLabelHasMutation\":" + Bool(marker)
                + ",\"woodLogRoots\":" + matches.Length);
        }
        catch (Exception e) { Failure("retained-source-combination", e); }
    }

    private static void CheckRetainedDef()
    {
        if (!sourceObserverSelected) return;
        try
        {
            ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
            bool sourceIntact = RetainedSourceIntact();
            bool labelMatches = retainedExpected != null && def != null && def.label == retainedExpected;
            Write("retained-source-final-def", "\"passed\":" + Bool(retainedCombinationPassed && sourceIntact && labelMatches)
                + ",\"combinationPassed\":" + Bool(retainedCombinationPassed) + ",\"sameSourceAssetAndNodes\":" + Bool(sourceIntact)
                + ",\"defFound\":" + Bool(def != null) + ",\"labelMatches\":" + Bool(labelMatches)
                + ",\"expectedLabel\":" + Quote(retainedExpected) + ",\"actualLabel\":" + Quote(def?.label));
        }
        catch (Exception e) { Failure("retained-source-final-def", e); }
        finally { retainedAsset = null; retainedDocument = null; retainedRoot = null; retainedLabel = null; retainedText = null; }
    }

    private static void Combined(XmlDocument __0)
    {
        if (completed) return;
        try { Write("combined-before-tkey", "\"structuralSha256\":" + Quote(TreeHash(__0))); combined = true; }
        catch (Exception e) { Failure("combined-before-tkey", e); }
    }

    private static void BeforePatches(XmlDocument __0, Dictionary<XmlNode, LoadableXmlAsset> __1)
    {
        if (prePatch || completed) return;
        try
        {
            CheckRetainedCombination(__0, __1);
            foreach (LoadableXmlAsset asset in __1.Values.Distinct())
                sourceBefore.Add(asset, TreeHash(asset.xmlDoc));
            Snapshot("before-patches", __0, __1);
            prePatch = true;
        }
        catch (Exception e) { Failure("before-patches", e); }
    }

    private static void BeforeDefs(XmlDocument __0, Dictionary<XmlNode, LoadableXmlAsset> __1, bool __2)
    {
        if (__2 || postPatch || completed) return;
        try
        {
            Snapshot("before-def-construction", __0, __1);
            int changed = sourceBefore.Count(pair => TreeHash(pair.Key.xmlDoc) != pair.Value);
            Write("source-preservation", "\"mappedSourcesObservedBeforePatches\":" + sourceBefore.Count
                + ",\"changedSourceTrees\":" + changed + ",\"beforePatchesObserved\":" + Bool(prePatch));
            postPatch = true;
        }
        catch (Exception e) { Failure("before-def-construction", e); }
        finally { sourceBefore.Clear(); }
    }

    private static void Snapshot(string stage, XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map)
    {
        int roots = 0, mapped = 0, unmapped = 0, ownershipFailures = 0;
        string attribution = Hash(writer =>
        {
            foreach (XmlNode root in document.DocumentElement!.ChildNodes)
            {
                writer.Write(roots++);
                writer.Write(TreeHash(root));
                bool found = map.TryGetValue(root, out LoadableXmlAsset asset);
                writer.Write(found);
                if (!found) { unmapped++; continue; }
                mapped++;
                WriteAsset(writer, asset);
                if (ReferenceEquals(document, asset.xmlDoc) || !ReferenceEquals(root.OwnerDocument, document)
                    || ReferenceEquals(root.OwnerDocument, asset.xmlDoc)) ownershipFailures++;
            }
        });
        // Include removed roots' sources too: patching can remove their map keys
        // from the final document without changing the original source assets.
        var sources = map.Values.Concat(sourceBefore.Keys).Distinct()
            .OrderBy(a => a.mod?.PackageId, StringComparer.Ordinal)
            .ThenBy(a => a.fullFolderPath, StringComparer.Ordinal).ThenBy(a => a.name, StringComparer.Ordinal).ToArray();
        var documents = new HashSet<XmlDocument>();
        foreach (LoadableXmlAsset source in sources)
        {
            if (ReferenceEquals(source.xmlDoc, document)) ownershipFailures++;
            if (source.xmlDoc != null)
            {
                if (!documents.Add(source.xmlDoc)) ownershipFailures++;
                foreach (XmlNode node in Descendants(source.xmlDoc))
                    if (!ReferenceEquals(node, source.xmlDoc) && !ReferenceEquals(node.OwnerDocument, source.xmlDoc)) ownershipFailures++;
            }
            Write("source", "\"stage\":" + Quote(stage) + ",\"package\":" + Quote(source.mod?.PackageId)
                + ",\"name\":" + Quote(source.name) + ",\"fullFolderPath\":" + Quote(source.fullFolderPath)
                + ",\"structuralSha256\":" + Quote(TreeHash(source.xmlDoc)));
        }
        Write(stage, "\"structuralSha256\":" + Quote(TreeHash(document)) + ",\"orderedAttributionSha256\":" + Quote(attribution)
            + ",\"roots\":" + roots + ",\"mappedRoots\":" + mapped + ",\"unmappedRoots\":" + unmapped
            + ",\"mappedSourceAssets\":" + sources.Length + ",\"distinctSourceDocuments\":" + documents.Count + ",\"ownershipFailures\":" + ownershipFailures);
    }

    internal static void Menu()
    {
        if (path == null || completed) return;
        completed = true;
        try
        {
            CheckRetainedDef();
            Defs("ThingDef", DefDatabase<ThingDef>.AllDefsListForReading, (writer, def) =>
            {
                writer.Write((int)def.category); writer.Write(def.stackLimit); writer.Write(def.useHitPoints);
                Text(writer, def.thingClass?.FullName);
            });
            Defs("RecipeDef", DefDatabase<RecipeDef>.AllDefsListForReading, (writer, def) =>
            {
                writer.Write(def.workAmount); Text(writer, def.workSkill?.defName);
                writer.Write(def.products?.Count ?? -1);
                if (def.products != null) foreach (var product in def.products) { Text(writer, product.thingDef?.defName); writer.Write(product.count); }
            });
            Defs("PawnKindDef", DefDatabase<PawnKindDef>.AllDefsListForReading, (writer, def) =>
            {
                Text(writer, def.race?.defName); writer.Write(def.combatPower);
            });
            Write("complete", "\"beforeTKeyObserved\":" + Bool(combined) + ",\"beforePatchesObserved\":" + Bool(prePatch) + ",\"beforeDefConstructionObserved\":" + Bool(postPatch)
                + ",\"coverage\":\"startup definition observations only; equality requires a matched native control; no gameplay or performance claim\"");
        }
        catch (Exception e) { Failure("menu", e); }
        finally { sourceBefore.Clear(); }
    }

    private static void Defs<T>(string family, IEnumerable<T> values, Action<BinaryWriter, T> extra) where T : Def
    {
        T[] defs = values.OrderBy(d => d.defName, StringComparer.Ordinal).ThenBy(d => d.modContentPack?.PackageId, StringComparer.Ordinal).ToArray();
        string hash = Hash(writer =>
        {
            writer.Write(defs.Length);
            foreach (T def in defs)
            {
                Text(writer, def.GetType().FullName); Text(writer, def.defName); Text(writer, def.label); Text(writer, def.description);
                Text(writer, def.modContentPack?.PackageId); extra(writer, def);
            }
        });
        Write("final-defs", "\"family\":" + Quote(family) + ",\"count\":" + defs.Length + ",\"selectedFieldsSha256\":" + Quote(hash));
        foreach (var origin in defs.GroupBy(d => d.modContentPack?.PackageId ?? "unknown").OrderBy(g => g.Key, StringComparer.Ordinal))
            Write("final-def-origin", "\"family\":" + Quote(family) + ",\"package\":" + Quote(origin.Key) + ",\"count\":" + origin.Count());
    }

    private static IEnumerable<XmlNode> Descendants(XmlNode root)
    {
        var pending = new Stack<XmlNode>(); pending.Push(root);
        while (pending.Count != 0)
        {
            XmlNode node = pending.Pop(); yield return node;
            for (XmlNode? child = node.LastChild; child != null; child = child.PreviousSibling) pending.Push(child);
        }
    }

    private static string TreeHash(XmlNode? root) => Hash(writer =>
    {
        writer.Write(root != null); if (root == null) return;
        foreach (XmlNode node in Descendants(root))
        {
            writer.Write((int)node.NodeType); Text(writer, node.Name); Text(writer, node.NamespaceURI); Text(writer, node.Prefix); Text(writer, node.Value);
            writer.Write(node is XmlElement element && element.IsEmpty); writer.Write(node.ChildNodes.Count);
            writer.Write(node.Attributes?.Count ?? 0);
            if (node.Attributes != null) foreach (XmlAttribute attribute in node.Attributes)
            {
                Text(writer, attribute.Name); Text(writer, attribute.NamespaceURI); Text(writer, attribute.Prefix); Text(writer, attribute.Value);
                writer.Write(attribute.Specified);
            }
        }
    });

    private static void WriteAsset(BinaryWriter writer, LoadableXmlAsset asset)
    { Text(writer, asset.mod?.PackageId); Text(writer, asset.name); Text(writer, asset.fullFolderPath); }

    private static string Hash(Action<BinaryWriter> content)
    {
        using var hash = SHA256.Create();
        using var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        content(writer); writer.Flush(); stream.FlushFinalBlock();
        return BitConverter.ToString(hash.Hash!).Replace("-", "");
    }
    private static void Text(BinaryWriter writer, string? text) { writer.Write(text != null); if (text != null) writer.Write(text); }
    private static string Bool(bool value) => value ? "true" : "false";
    private static string Quote(string? value)
    {
        if (value == null) return "null";
        var result = new StringBuilder("\"");
        foreach (char c in value)
            if (c == '\\' || c == '"') result.Append('\\').Append(c);
            else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else result.Append(c);
        return result.Append('"').ToString();
    }
    private static void Failure(string stage, Exception e)
    { try { Write("observation-error", "\"stage\":" + Quote(stage) + ",\"exception\":" + Quote(e.ToString())); } catch { } }
    private static void Write(string name, string fields)
    {
        if (path == null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, "{\"schema\":\"fixture-parsed-xml.v1\",\"feature\":\"parsed-xml\",\"event\":" + Quote(name) + "," + fields + "}\n", new UTF8Encoding(false));
    }
}
