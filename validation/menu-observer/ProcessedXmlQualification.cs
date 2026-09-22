// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using HarmonyLib;
using Verse;

namespace FixtureMenuObserver;

// Observes actual downstream consumers. It never hooks a cache's producer or
// changes its admission, and it stores values rather than retaining XML nodes.
internal static class ProcessedXmlQualification
{
    private static string? path;
    private static int converted, resolved, errors;
    [ThreadStatic] private static int depth;
    private static readonly Dictionary<LoadableXmlAsset, string> sources = new();
    internal static void Install(string directory)
    {
        path = Path.Combine(directory, "processed-xml-qualification.jsonl");
        var harmony = new Harmony("fixture.processed-xml-consumers");
        foreach (string name in new[] { "Verse.DirectXmlToObjectNew:DefFromNodeNew", "Verse.DirectXmlLoader:DefFromNode" })
            harmony.Patch(AccessTools.Method(name), prefix: new HarmonyMethod(typeof(ProcessedXmlQualification), nameof(BeforeDef)),
                finalizer: new HarmonyMethod(typeof(ProcessedXmlQualification), nameof(AfterDef)));
        harmony.Patch(AccessTools.Method(typeof(XmlInheritance), "GetResolvedNodeFor"), postfix: new HarmonyMethod(typeof(ProcessedXmlQualification), nameof(Resolved)));
        harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "ClearCachedPatches"), prefix: new HarmonyMethod(typeof(ProcessedXmlQualification), nameof(BeforeComplete)));
        Write("installed", "\"boundary\":\"native definition consumers after XML producers\"");
    }
    private static void BeforeDef(XmlNode __0, LoadableXmlAsset __1)
    {
        if (depth++ != 0) return;
        try
        {
            converted++;
            string asset = __1 == null ? "unmapped" : (__1.mod?.PackageId ?? "") + "|" + __1.fullFolderPath + "|" + __1.name;
            if (__1 != null && !sources.ContainsKey(__1)) sources.Add(__1, Tree(__1.xmlDoc));
            Write("consumer", "\"ordinal\":" + converted + ",\"source\":" + Q(asset) + ",\"xml\":" + Q(Tree(__0))
                + ",\"distinctSourceOwner\":" + (__1 == null || !ReferenceEquals(__0.OwnerDocument, __1.xmlDoc) ? "true" : "false"));
        }
        catch (Exception e) { errors++; Write("error", "\"message\":" + Q(e.ToString())); }
    }
    private static Exception? AfterDef(Exception? __exception) { depth--; return __exception; }
    private static void Resolved(XmlNode __0, XmlNode __result)
    {
        if (depth == 0) return;
        try { resolved++; Write("resolved", "\"consumer\":" + converted + ",\"input\":" + Q(Tree(__0)) + ",\"output\":" + Q(Tree(__result))
            + ",\"sameNode\":" + (ReferenceEquals(__0, __result) ? "true" : "false")
            + ",\"sameOwner\":" + (ReferenceEquals(__0.OwnerDocument, __result.OwnerDocument) ? "true" : "false")); }
        catch (Exception e) { errors++; Write("error", "\"message\":" + Q(e.ToString())); }
    }
    private static void BeforeComplete()
    {
        try
        {
            int i = 0;
            foreach (var mod in LoadedModManager.RunningModsListForReading)
                foreach (var operation in mod.Patches)
                {
                    var seen = new Dictionary<PatchOperation, int>();
                    var rows = new List<string>();
                    void State(PatchOperation op)
                    {
                        if (seen.ContainsKey(op)) return; seen[op] = seen.Count;
                        bool never = (bool)AccessTools.Field(typeof(PatchOperation), "neverSucceeded").GetValue(op);
                        rows.Add(op.GetType().FullName + "|" + op.sourceFile + "|" + never);
                        for (Type? t = op.GetType(); t != null && t != typeof(object); t = t.BaseType)
                            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(f => f.Name))
                            {
                                if (field.Name == "lastFailedOperation") { var failed = (PatchOperation?)field.GetValue(op); rows.Add("failed:" + failed?.ToString()); continue; }
                                if (field.FieldType == typeof(PatchOperation) && field.GetValue(op) is PatchOperation child) State(child);
                                else if (field.FieldType == typeof(List<PatchOperation>) && field.GetValue(op) is List<PatchOperation> children) foreach (var item in children) State(item);
                            }
                    }
                    State(operation); Write("operation-state", "\"ordinal\":" + i++ + ",\"hash\":" + Q(Hash(Encoding.UTF8.GetBytes(string.Join("\n", rows)))));
                }
            foreach (var pair in sources) Write("source", "\"name\":" + Q(pair.Key.mod?.PackageId + "|" + pair.Key.fullFolderPath + "|" + pair.Key.name)
                + ",\"hash\":" + Q(pair.Value) + ",\"unchanged\":" + (pair.Value == Tree(pair.Key.xmlDoc) ? "true" : "false"));
            sources.Clear();
        }
        catch (Exception e) { errors++; Write("error", "\"message\":" + Q(e.ToString())); }
    }
    internal static void Menu()
    {
        if (path == null) return;
        try
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                Write("thing-def", "\"name\":" + Q(def.defName) + ",\"source\":" + Q(def.modContentPack?.PackageId + "|" + def.fileName)
                    + ",\"hash\":" + Q(Hash(Encoding.UTF8.GetBytes(def.label + "|" + def.description + "|" + def.category + "|" + def.stackLimit + "|" + def.thingClass?.FullName))));
            Write("complete", "\"converted\":" + converted + ",\"resolvedCalls\":" + resolved + ",\"errors\":" + errors);
        }
        catch (Exception e) { Write("error", "\"message\":" + Q(e.ToString())); }
        path = null;
    }
    private static string Tree(XmlNode? node)
    {
        if (node == null) return "null";
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        void WriteNode(XmlNode n)
        {
            writer.Write((int)n.NodeType); writer.Write(n.Name); writer.Write(n.Value ?? ""); writer.Write(n.NamespaceURI); writer.Write(n.Prefix);
            writer.Write(n is XmlElement e && e.IsEmpty); writer.Write(n.Attributes?.Count ?? -1);
            if (n.Attributes != null) foreach (XmlAttribute a in n.Attributes) WriteNode(a);
            writer.Write(n.ChildNodes.Count); foreach (XmlNode child in n.ChildNodes) WriteNode(child);
        }
        WriteNode(node); writer.Flush(); return Hash(stream.ToArray());
    }
    private static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
    private static string Q(string? value) => "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    private static void Write(string kind, string fields)
    { if (path != null) File.AppendAllText(path, "{\"kind\":" + Q(kind) + "," + fields + "}\n", new UTF8Encoding(false)); }
}
