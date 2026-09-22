// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Verse;

namespace WakeUp;

// A finite dependency grammar, not an XPath evaluator. Native workers still
// evaluate selectors. Only named roots and child-element paths are described.
internal sealed class XmlRegionOperations
{
    internal const int MaxOperations = 128, MaxRoots = 64, MaxBytes = 1024 * 1024, MaxNodes = 32768;
    private static readonly Regex Selector = new("^/?Defs/(?<type>[A-Za-z_][A-Za-z0-9_.-]*)\\[(?<terms>[^\\]]+)\\](?<tail>(?:/[A-Za-z_][A-Za-z0-9_.-]*)*)$", RegexOptions.CultureInvariant);
    private static readonly Regex Term = new("\\G(?<key>defName|@Name)\\s*=\\s*(?<q>['\"])(?<value>[^'\"]*)\\k<q>(?:\\s+or\\s+|$)", RegexOptions.CultureInvariant);
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo Never = typeof(PatchOperation).GetField("neverSucceeded", Fields)!;
    private readonly PatchOperation[] all;
    private readonly RootKey[] keys;
    private readonly Dictionary<string, HashSet<string>> names = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> templates = new(StringComparer.Ordinal);
    internal PatchOperation[] Tops { get; }
    internal byte[] Identity { get; }
    internal int ObjectCount => all.Length;
    internal int BroadSelectorCount { get; }

    private XmlRegionOperations(PatchOperation[] tops, PatchOperation[] all, RootKey[] keys, byte[] identity)
    {
        Tops = tops; this.all = all; this.keys = keys; Identity = identity;
        foreach (var op in all)
            if (TrySelector((string?)typeof(PatchOperationPathed).GetField("xpath", Fields)!.GetValue(op), out var selected, out _)
                && selected.Select(k => k.Key).Distinct().Count() > 1) BroadSelectorCount++;
        foreach (RootKey key in keys)
        {
            var table = key.Attribute ? templates : names;
            if (!table.TryGetValue(key.Type, out var values)) table.Add(key.Type, values = new HashSet<string>(StringComparer.Ordinal));
            values.Add(key.Name);
        }
    }

    internal sealed class RootKey
    {
        internal readonly string Type, Name;
        internal readonly bool Attribute;
        internal RootKey(string type, string name, bool attribute) { Type = type; Name = name; Attribute = attribute; }
        internal string Key => Type + (Attribute ? "@" : "=") + Name;
    }

    internal static bool TryCreate(IReadOnlyList<PatchOperation> operations, int start, out XmlRegionOperations? region)
    {
        region = null;
        try
        {
            var tops = new List<PatchOperation>();
            var objects = new List<PatchOperation>();
            var roots = new Dictionary<string, RootKey>(StringComparer.Ordinal);
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            writer.Write(1);
            for (int i = start; i < operations.Count && tops.Count < MaxOperations; i++)
            {
                var nextObjects = new List<PatchOperation>();
                var nextRoots = new List<RootKey>();
                using var next = new MemoryStream();
                using var output = new BinaryWriter(next, Encoding.UTF8, true);
                if (!Describe(operations[i], output, nextObjects, nextRoots, 0)
                    || objects.Count + nextObjects.Count > MaxOperations
                    || nextObjects.Any(objects.Contains)
                    || roots.Keys.Concat(nextRoots.Select(k => k.Key)).Distinct().Count() > MaxRoots
                    || bytes.Length + next.Length > MaxBytes) break;
                tops.Add(operations[i]); objects.AddRange(nextObjects);
                foreach (var key in nextRoots) roots[key.Key] = key;
                writer.Write((int)next.Length); writer.Write(next.ToArray());
            }
            if (tops.Count == 0) return false;
            region = new XmlRegionOperations(tops.ToArray(), objects.ToArray(), roots.Values.ToArray(), bytes.ToArray());
            return true;
        }
        catch { return false; }
    }

    private static bool Describe(PatchOperation op, BinaryWriter writer, List<PatchOperation> objects, List<RootKey> roots, int depth)
    {
        if (op == null || depth > 16 || objects.Contains(op) || objects.Count >= MaxOperations) return false;
        Type type = op.GetType();
        if (type != typeof(PatchOperationAdd) && type != typeof(PatchOperationReplace)
            && type != typeof(PatchOperationRemove) && type != typeof(PatchOperationAddModExtension)
            && type != typeof(PatchOperationConditional)) return false;
        string? xpath = (string?)typeof(PatchOperationPathed).GetField("xpath", Fields)!.GetValue(op);
        if (!TrySelector(xpath, out var found, out string tail)) return false;
        if ((type == typeof(PatchOperationReplace) || type == typeof(PatchOperationRemove)) && tail.Length == 0) return false;
        if (tail == "/defName" || tail.StartsWith("/defName/", StringComparison.Ordinal)) return false;
        roots.AddRange(found); objects.Add(op);
        writer.Write(type.Name); writer.Write(op.sourceFile ?? ""); writer.Write(xpath!);
        writer.Write(Convert.ToInt32(typeof(PatchOperation).GetField("success", Fields)!.GetValue(op)));
        writer.Write((bool)Never.GetValue(op));
        if (type == typeof(PatchOperationConditional))
        {
            foreach (string name in new[] { "match", "nomatch" })
            {
                var child = (PatchOperation?)type.GetField(name, Fields)!.GetValue(op);
                writer.Write(child != null);
                if (child != null && !Describe(child, writer, objects, roots, depth + 1)) return false;
            }
        }
        else if (type != typeof(PatchOperationRemove))
        {
            var value = (XmlContainer?)type.GetField("value", Fields)!.GetValue(op);
            if (value?.GetType() != typeof(XmlContainer) || value.node == null) return false;
            int nodes = 0; WriteNode(writer, value.node, ref nodes);
            foreach (XmlNode node in value.node.ChildNodes)
                if (node.GetType() != typeof(XmlElement) || node.Name == "defName"
                    && (tail.Length == 0 || type == typeof(PatchOperationReplace) && tail.LastIndexOf('/') == 0)) return false;
            if (type == typeof(PatchOperationAdd))
            {
                int order = Convert.ToInt32(type.GetField("order", Fields)!.GetValue(op));
                if (order < 0 || order > 1) return false;
                writer.Write(order);
            }
        }
        return writer.BaseStream.Length <= MaxBytes;
    }

    internal static bool TrySelector(string? xpath, out RootKey[] keys, out string tail)
    {
        keys = Array.Empty<RootKey>(); tail = "";
        if (xpath == null || xpath.Length > 4096) return false;
        Match match = Selector.Match(xpath);
        if (!match.Success) return false;
        string terms = match.Groups["terms"].Value;
        var found = new List<RootKey>();
        int position = 0;
        while (position < terms.Length)
        {
            Match term = Term.Match(terms, position);
            if (!term.Success || term.Index != position || found.Count >= MaxRoots) return false;
            found.Add(new RootKey(match.Groups["type"].Value, term.Groups["value"].Value, term.Groups["key"].Value == "@Name"));
            position += term.Length;
        }
        if (found.Count == 0 || Regex.IsMatch(terms, "\\s+or\\s+$", RegexOptions.CultureInvariant)) return false;
        keys = found.ToArray(); tail = match.Groups["tail"].Value; return true;
    }

    internal XmlElement[] Bind(XmlDocument document, BinaryWriter writer)
    {
        if (document.GetType() != typeof(XmlDocument) || document.DocumentType != null
            || document.DocumentElement?.GetType() != typeof(XmlElement)
            || document.DocumentElement.Name != "Defs" || document.DocumentElement.NamespaceURI.Length != 0)
            throw new InvalidDataException("region-document");
        var found = new List<XmlElement>();
        foreach (XmlNode child in document.DocumentElement.ChildNodes)
        {
            if (child.GetType() != typeof(XmlElement)) throw new InvalidDataException("region-root-kind");
            var element = (XmlElement)child;
            if (Matches(element))
            {
                if (found.Count >= MaxRoots) throw new InvalidDataException("region-root-bound");
                found.Add(element);
            }
        }
        // List order and root count encode duplicates and absent requested keys;
        // the operation identity already includes every requested key.
        writer.Write(found.Count);
        int nodes = 0;
        foreach (var root in found) WriteNode(writer, root, ref nodes);
        return found.ToArray();
    }

    private bool Matches(XmlElement element)
    {
        if (element.NamespaceURI.Length != 0) return false;
        if (templates.TryGetValue(element.Name, out var templateNames))
        {
            var attribute = element.GetAttributeNode("Name", "");
            if (attribute != null)
            {
                RequireNativeTree(attribute);
                if (templateNames.Contains(attribute.Value)) return true;
            }
        }
        if (!names.TryGetValue(element.Name, out var defNames)) return false;
        foreach (XmlNode child in element.ChildNodes)
        {
            if (child.GetType().Assembly != typeof(XmlNode).Assembly) throw new InvalidDataException("region-custom-node");
            if (child is XmlElement name && name.Name == "defName" && name.NamespaceURI.Length == 0)
            { RequireNativeTree(name); if (defNames.Contains(name.InnerText)) return true; }
        }
        return false;
    }

    private static void RequireNativeTree(XmlNode node)
    {
        int count = 0;
        Check(node, 0);
        void Check(XmlNode current, int depth)
        {
            if (++count > MaxNodes || depth > 64 || current.GetType().Assembly != typeof(XmlNode).Assembly
                || current.NodeType == XmlNodeType.EntityReference) throw new InvalidDataException("region-name-tree");
            foreach (XmlNode child in current.ChildNodes) Check(child, depth + 1);
        }
    }

    // Structural identity preserves text-node boundaries, attribute order and
    // empty-element spelling; OuterXml alone loses some retained-node topology.
    internal static void WriteNode(BinaryWriter writer, XmlNode node, ref int nodes, int depth = 0)
    {
        if (++nodes > MaxNodes || depth > 64 || writer.BaseStream.Length > MaxBytes) throw new InvalidDataException("region-tree-bound");
        Type type = node.GetType();
        if (type != typeof(XmlElement) && type != typeof(XmlAttribute) && type != typeof(XmlText)
            && type != typeof(XmlCDataSection) && type != typeof(XmlComment) && type != typeof(XmlWhitespace)
            && type != typeof(XmlSignificantWhitespace) && type != typeof(XmlProcessingInstruction))
            throw new InvalidDataException("region-node-kind");
        writer.Write((int)node.NodeType); writer.Write(node.Prefix); writer.Write(node.LocalName);
        writer.Write(node.NamespaceURI); writer.Write(node.Value ?? "");
        writer.Write(node is XmlElement e && e.IsEmpty);
        writer.Write(node.Attributes?.Count ?? 0);
        if (node.Attributes != null) foreach (XmlAttribute attribute in node.Attributes) WriteNode(writer, attribute, ref nodes, depth + 1);
        writer.Write(node.ChildNodes.Count);
        foreach (XmlNode child in node.ChildNodes) WriteNode(writer, child, ref nodes, depth + 1);
        if (writer.BaseStream.Length > MaxBytes) throw new InvalidDataException("region-tree-bound");
    }

    internal bool[] CaptureState() => all.Select(op => (bool)Never.GetValue(op)).ToArray();
    internal PatchOperation[] Objects => all;
    internal XmlElement[] ImportedTemplates()
    {
        var result = new List<XmlElement>();
        foreach (var op in all)
        {
            if (op.GetType() == typeof(PatchOperationAddModExtension)) result.Add(new XmlDocument().CreateElement("modExtensions"));
            var field = op.GetType().GetField("value", Fields);
            if (field?.GetValue(op) is XmlContainer value)
                foreach (XmlElement child in value.node.ChildNodes) result.Add(child);
        }
        return result.ToArray();
    }
}
