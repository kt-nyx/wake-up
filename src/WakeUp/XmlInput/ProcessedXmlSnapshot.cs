// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using Verse;

namespace WakeUp;

// Stores the private completed combined tree and only its detached mapped roots.
// Original per-file source trees remain the real native assets of this launch.
internal static class ProcessedXmlSnapshot
{
    internal const int MaximumBytes = 64 * 1024 * 1024;
    internal const int MaximumModNameCalls = 131072;
    private const int MaximumAtoms = 131072, MaximumAtomBytes = 1024 * 1024;
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    internal sealed class Restored
    {
        internal XmlDocument Document = null!;
        internal Dictionary<XmlNode, LoadableXmlAsset> Map = null!;
        internal byte[] State = null!;
        internal int[] Parents = null!;
        internal int[] ModNameCalls = null!;
        internal bool[] Called = null!, Results = null!;
    }
    internal static byte[] Encode(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> map,
        IReadOnlyList<LoadableXmlAsset> assets, byte[] state, int[] parents, bool[] called, bool[] results, int[]? modNameCalls = null)
    {
        if (parents.Length > 32768 || called.Length != parents.Length || results.Length != parents.Length
            || modNameCalls != null && modNameCalls.Length != parents.Length) throw new InvalidDataException("processed-operation-trace-length");
        if (modNameCalls != null)
            for (int i = 0; i < modNameCalls.Length; i++)
                if (modNameCalls[i] < 0 || modNameCalls[i] > MaximumModNameCalls || !called[i] && modNameCalls[i] != 0)
                    throw new InvalidDataException("processed-mod-name-call-count");
        if (document.GetType() != typeof(XmlDocument) || document.DocumentElement?.Name != "Defs"
            || document.ChildNodes.Count != 1 || document.DocumentType != null) throw new InvalidDataException("processed-document");
        var roots = new List<XmlNode> { document.DocumentElement };
        var direct = document.DocumentElement.ChildNodes.Cast<XmlNode>().ToArray();
        var childIndices = direct.Select((node, index) => (node, index)).ToDictionary(p => p.node, p => p.index);
        var assetIndices = new Dictionary<LoadableXmlAsset, int>(new AssetReferenceComparer());
        for (int i = 0; i < assets.Count; i++)
            if (!assetIndices.ContainsKey(assets[i])) assetIndices.Add(assets[i], i);
        var bindings = new List<(int tree, int child, int asset)>();
        foreach (var pair in map)
        {
            if (!assetIndices.TryGetValue(pair.Value, out int asset) || pair.Key.OwnerDocument != document)
                throw new InvalidDataException("processed-source-binding");
            if (childIndices.TryGetValue(pair.Key, out int child)) bindings.Add((0, child, asset));
            else
            {
                if (pair.Key.ParentNode != null) throw new InvalidDataException("processed-mapped-root-moved");
                bindings.Add((roots.Count, -1, asset)); roots.Add(pair.Key);
            }
        }
        byte[] trees = ResolvedInheritanceFormat.EncodeRoots(roots);
        // Removed temporary names remain publicly observable through NameTable.
        // Capture after reading the trees, in case reading a node's qualified
        // name caused the native DOM to atomize that name lazily.
        string[] atoms = CaptureAtoms(document.NameTable);
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write(3); writer.Write(document.PreserveWhitespace);
        writer.Write(atoms.Length); foreach (string atom in atoms) writer.Write(atom);
        writer.Write(trees.Length); writer.Write(trees);
        writer.Write(bindings.Count);
        foreach (var row in bindings) { writer.Write(row.tree); writer.Write(row.child); writer.Write(row.asset); }
        writer.Write(state.Length); writer.Write(state); writer.Write(parents.Length);
        for (int i = 0; i < parents.Length; i++)
        { writer.Write(parents[i]); writer.Write(called[i]); writer.Write(results[i]); writer.Write(modNameCalls?[i] ?? 0); }
        writer.Flush(); if (output.Length > MaximumBytes) throw new InvalidDataException("processed-size");
        return output.ToArray();
    }
    internal static Restored Decode(byte[] bytes, IReadOnlyList<LoadableXmlAsset> assets, int operations)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("processed-size");
        using var input = new MemoryStream(bytes, false); using var reader = new BinaryReader(input);
        if (reader.ReadInt32() != 3) throw new InvalidDataException("processed-schema");
        var doc = new XmlDocument { PreserveWhitespace = reader.ReadBoolean() };
        int atomCount = ReadCount(reader, MaximumAtoms);
        string? previous = null;
        for (int i = 0; i < atomCount; i++)
        {
            string atom = reader.ReadString();
            if (atom.Length == 0 || atom.Length > MaximumAtomBytes || Encoding.UTF8.GetByteCount(atom) > MaximumAtomBytes
                || previous != null && StringComparer.Ordinal.Compare(previous, atom) >= 0) throw new InvalidDataException("processed-name-atom");
            doc.NameTable.Add(atom); previous = atom;
        }
        int size = ReadCount(reader, MaximumBytes); if (size > input.Length - input.Position) throw new InvalidDataException("processed-tree-size");
        var roots = ResolvedInheritanceFormat.DecodeRoots(reader.ReadBytes(size), doc);
        if (roots.Length == 0 || roots[0].Name != "Defs") throw new InvalidDataException("processed-root");
        doc.AppendChild(roots[0]);
        // XmlChildNodes is a linked traversal, including Count and the indexer.
        // Materialize once so each source binding does not walk every sibling.
        var direct = roots[0].ChildNodes.Cast<XmlNode>().ToArray();
        var map = new Dictionary<XmlNode, LoadableXmlAsset>();
        int count = ReadCount(reader, 1000000);
        for (int i = 0; i < count; i++)
        {
            int tree = reader.ReadInt32(), child = reader.ReadInt32(), asset = reader.ReadInt32();
            if (tree < 0 || tree >= roots.Length || asset < 0 || asset >= assets.Count
                || (tree == 0 ? child < 0 || child >= direct.Length : child != -1)) throw new InvalidDataException("processed-map");
            map.Add(tree == 0 ? direct[child] : roots[tree], assets[asset]);
        }
        int stateSize = ReadCount(reader, MaximumBytes); if (stateSize > input.Length - input.Position) throw new InvalidDataException("processed-state-size");
        var state = reader.ReadBytes(stateSize);
        count = ReadCount(reader, 32768); if (count != operations) throw new InvalidDataException("processed-operation-count");
        var parents = new int[count]; var called = new bool[count]; var results = new bool[count];
        var modNameCalls = new int[count];
        for (int i = 0; i < count; i++)
        {
            parents[i] = reader.ReadInt32(); called[i] = reader.ReadBoolean(); results[i] = reader.ReadBoolean();
            modNameCalls[i] = ReadCount(reader, MaximumModNameCalls);
            if (parents[i] < -1 || parents[i] >= i) throw new InvalidDataException("processed-call-parent");
            if (!called[i] && modNameCalls[i] != 0) throw new InvalidDataException("processed-mod-name-call-count");
        }
        if (input.Position != input.Length) throw new InvalidDataException("processed-trailing");
        return new Restored { Document = doc, Map = map, State = state, Parents = parents, Called = called, Results = results, ModNameCalls = modNameCalls };
    }
    private static int ReadCount(BinaryReader reader, int maximum)
    { int count = reader.ReadInt32(); if (count < 0 || count > maximum) throw new InvalidDataException("processed-count"); return count; }

    private sealed class AssetReferenceComparer : IEqualityComparer<LoadableXmlAsset>
    {
        public bool Equals(LoadableXmlAsset? x, LoadableXmlAsset? y) => ReferenceEquals(x, y);
        public int GetHashCode(LoadableXmlAsset value) => RuntimeHelpers.GetHashCode(value);
    }

    private static string[] CaptureAtoms(XmlNameTable table)
    {
        if (table.GetType() != typeof(NameTable)) throw new InvalidDataException("processed-custom-name-table");
        // Inspected GOG Mono System.Xml and .NET Framework 4.8 both use these
        // fields. Framework additionally owns an unrelated hash seed; neither
        // hash seed nor bucket order is serialized or transplanted.
        Type type = typeof(NameTable);
        Type? entry = type.GetNestedType("Entry", BindingFlags.NonPublic);
        if (entry == null || entry.BaseType != typeof(object)) throw new InvalidDataException("processed-name-entry-layout");
        var expected = new Dictionary<string, Type> { ["entries"] = entry.MakeArrayType(), ["count"] = typeof(int),
            ["mask"] = typeof(int), ["hashCodeRandomizer"] = typeof(int) };
        FieldInfo[] tableFields = type.GetFields(InstanceFields);
        if (tableFields.Any(f => f.Name == "marvinHashSeed")) expected.Add("marvinHashSeed", typeof(ulong));
        if (tableFields.Length != expected.Count || tableFields.Any(f => !expected.TryGetValue(f.Name, out Type fieldType) || fieldType != f.FieldType))
            throw new InvalidDataException("processed-name-table-layout");
        FieldInfo[] entryFields = entry.GetFields(InstanceFields);
        var expectedEntry = new Dictionary<string, Type> { ["str"] = typeof(string), ["hashCode"] = typeof(int), ["next"] = entry };
        if (entryFields.Length != expectedEntry.Count || entryFields.Any(f => !expectedEntry.TryGetValue(f.Name, out Type fieldType) || fieldType != f.FieldType))
            throw new InvalidDataException("processed-name-entry-layout");
        var buckets = (Array?)tableFields.Single(f => f.Name == "entries").GetValue(table);
        int count = (int)tableFields.Single(f => f.Name == "count").GetValue(table);
        int mask = (int)tableFields.Single(f => f.Name == "mask").GetValue(table);
        if (buckets == null || buckets.GetType() != entry.MakeArrayType() || count < 0 || count > MaximumAtoms
            || buckets.Length == 0 || buckets.Length > MaximumAtoms * 2 || mask != buckets.Length - 1)
            throw new InvalidDataException("processed-name-table-boundary");
        FieldInfo text = entryFields.Single(f => f.Name == "str"), next = entryFields.Single(f => f.Name == "next");
        var seen = new HashSet<object>(); var names = new HashSet<string>(StringComparer.Ordinal);
        long bytes = 0;
        foreach (object? bucket in buckets)
        {
            for (object? current = bucket; current != null; current = next.GetValue(current))
            {
                if (current.GetType() != entry || !seen.Add(current) || seen.Count > count) throw new InvalidDataException("processed-name-entry-chain");
                string? atom = (string?)text.GetValue(current);
                if (atom == null || atom.Length == 0 || atom.Length > MaximumAtomBytes || !names.Add(atom)) throw new InvalidDataException("processed-name-atom");
                int size = Encoding.UTF8.GetByteCount(atom);
                bytes += size + 5;
                if (size > MaximumAtomBytes || bytes > MaximumBytes) throw new InvalidDataException("processed-name-size");
            }
        }
        if (seen.Count != count) throw new InvalidDataException("processed-name-entry-count");
        return names.OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }
}
