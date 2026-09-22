// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Nonshipping R1 experiment. Compiled only by the offline test project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Verse;

namespace WakeUp;

// A private document forest: live roots and removed source-map roots share one
// owner document. Node kinds and mapped/unmapped ownership remain distinct.
internal static class XmlReuseSnapshot
{
    internal const int MaximumBytes = 64 * 1024 * 1024;
    private const int MaximumNodes = 2000000;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    internal sealed class Restored
    {
        internal readonly XmlDocument Document;
        internal readonly Dictionary<XmlNode, LoadableXmlAsset> Sources;
        internal readonly byte[] State;
        internal readonly double NativeMilliseconds;
        internal Restored(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> sources, byte[] state, double nativeMilliseconds)
        { Document = document; Sources = sources; State = state; NativeMilliseconds = nativeMilliseconds; }
    }

    internal static byte[] Encode(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> sources,
        IReadOnlyList<LoadableXmlAsset> inputs, byte[] state, double nativeMilliseconds = 0)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Utf8, true);
        writer.Write(1);
        writer.Write(nativeMilliseconds);
        var nodes = new Dictionary<XmlNode, int>();
        WriteNode(writer, document, nodes, 0);
        var detached = new List<XmlNode>();
        foreach (var key in sources.Keys)
        {
            if (nodes.ContainsKey(key)) continue;
            XmlNode root = key;
            while (root.ParentNode != null) root = root.ParentNode;
            if (root == document || root.OwnerDocument != document) throw new InvalidDataException("xml-source-owner");
            if (!detached.Contains(root)) detached.Add(root);
        }
        writer.Write(detached.Count);
        foreach (var root in detached) WriteNode(writer, root, nodes, 0);
        writer.Write(sources.Count);
        foreach (var pair in sources)
        {
            int asset = IndexOf(inputs, pair.Value);
            if (asset < 0) throw new InvalidDataException("xml-unbound-source");
            writer.Write(nodes[pair.Key]); writer.Write(asset);
        }
        if (state.Length > MaximumBytes / 2) throw new InvalidDataException("xml-state-size");
        writer.Write(state.Length); writer.Write(state);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("xml-payload-size");
        return stream.ToArray();
    }

    internal static Restored Decode(byte[] payload, IReadOnlyList<LoadableXmlAsset> inputs)
    {
        if (payload.Length > MaximumBytes) throw new InvalidDataException("xml-payload-size");
        using var stream = new MemoryStream(payload, false);
        using var reader = new BinaryReader(stream, Utf8);
        if (reader.ReadInt32() != 1) throw new InvalidDataException("xml-format");
        double nativeMilliseconds = reader.ReadDouble();
        if (nativeMilliseconds < 0 || double.IsNaN(nativeMilliseconds) || double.IsInfinity(nativeMilliseconds))
            throw new InvalidDataException("xml-cost-metadata");
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        var nodes = new List<XmlNode>();
        if (ReadNode(reader, document, nodes, 0) != document || document.DocumentElement?.Name != "Defs")
            throw new InvalidDataException("xml-root");
        int detached = Count(reader);
        for (int i = 0; i < detached; i++)
            if (ReadNode(reader, document, nodes, 0) is XmlDocument) throw new InvalidDataException("xml-extra-document");
        var sources = new Dictionary<XmlNode, LoadableXmlAsset>();
        int mapped = Count(reader);
        for (int i = 0; i < mapped; i++)
        {
            int node = reader.ReadInt32(), asset = reader.ReadInt32();
            if (node < 0 || node >= nodes.Count || asset < 0 || asset >= inputs.Count)
                throw new InvalidDataException("xml-source-index");
            sources.Add(nodes[node], inputs[asset]);
        }
        int stateLength = reader.ReadInt32();
        if (stateLength < 0 || stateLength != stream.Length - stream.Position) throw new InvalidDataException("xml-state-layout");
        return new Restored(document, sources, reader.ReadBytes(stateLength), nativeMilliseconds);
    }

    internal static void WriteTree(BinaryWriter writer, XmlNode node) => WriteNode(writer, node, new Dictionary<XmlNode, int>(), 0);

    private static void WriteNode(BinaryWriter writer, XmlNode node, Dictionary<XmlNode, int> nodes, int depth)
    {
        if (depth > 256 || nodes.Count >= MaximumNodes || writer.BaseStream.Position > MaximumBytes)
            throw new InvalidDataException("xml-tree-limit");
        Type type = node.GetType();
        if (type.Assembly != typeof(XmlDocument).Assembly || !Supported(node.NodeType)
            || node is XmlAttribute a && !a.Specified) throw new InvalidDataException("xml-node-kind");
        nodes.Add(node, nodes.Count);
        writer.Write((byte)node.NodeType);
        writer.Write(node is XmlDocument doc ? doc.PreserveWhitespace : node is XmlElement element && element.IsEmpty);
        WriteString(writer, node.Prefix); WriteString(writer, node.LocalName);
        WriteString(writer, node.NamespaceURI); WriteString(writer, node.Value ?? "");
        if (node is XmlDeclaration declaration)
        { WriteString(writer, declaration.Version); WriteString(writer, declaration.Encoding); WriteString(writer, declaration.Standalone); }
        writer.Write(node.Attributes?.Count ?? 0);
        if (node.Attributes != null) foreach (XmlAttribute attr in node.Attributes) WriteNode(writer, attr, nodes, depth + 1);
        writer.Write(node.ChildNodes.Count);
        foreach (XmlNode child in node.ChildNodes) WriteNode(writer, child, nodes, depth + 1);
    }

    private static XmlNode ReadNode(BinaryReader reader, XmlDocument document, List<XmlNode> nodes, int depth)
    {
        if (depth > 256 || nodes.Count >= MaximumNodes) throw new InvalidDataException("xml-tree-limit");
        var kind = (XmlNodeType)reader.ReadByte();
        bool flag = reader.ReadBoolean();
        string prefix = ReadString(reader), name = ReadString(reader), ns = ReadString(reader), value = ReadString(reader);
        XmlNode node = kind switch
        {
            XmlNodeType.Document when nodes.Count == 0 => document,
            XmlNodeType.Element => document.CreateElement(prefix, name, ns),
            XmlNodeType.Attribute => document.CreateAttribute(prefix, name, ns),
            XmlNodeType.Text => document.CreateTextNode(value),
            XmlNodeType.CDATA => document.CreateCDataSection(value),
            XmlNodeType.Comment => document.CreateComment(value),
            XmlNodeType.Whitespace => document.CreateWhitespace(value),
            XmlNodeType.SignificantWhitespace => document.CreateSignificantWhitespace(value),
            XmlNodeType.ProcessingInstruction => document.CreateProcessingInstruction(name, value),
            XmlNodeType.XmlDeclaration => document.CreateXmlDeclaration(ReadString(reader), ReadString(reader), ReadString(reader)),
            _ => throw new InvalidDataException("xml-node-kind")
        };
        nodes.Add(node);
        int attributes = Count(reader);
        for (int i = 0; i < attributes; i++)
        {
            if (node is not XmlElement element || ReadNode(reader, document, nodes, depth + 1) is not XmlAttribute attribute)
                throw new InvalidDataException("xml-attribute-kind");
            element.Attributes.Append(attribute);
        }
        int children = Count(reader);
        for (int i = 0; i < children; i++) node.AppendChild(ReadNode(reader, document, nodes, depth + 1));
        if (node is XmlDocument doc) doc.PreserveWhitespace = flag;
        else if (node is XmlElement element)
        {
            if (flag && children != 0) throw new InvalidDataException("xml-empty-element");
            element.IsEmpty = flag;
        }
        else if (flag) throw new InvalidDataException("xml-node-flag");
        if (node.Value != null && node.Value != value) throw new InvalidDataException("xml-node-value");
        return node;
    }

    internal static void WriteString(BinaryWriter writer, string value)
    {
        int length = Utf8.GetByteCount(value);
        if (length > MaximumBytes || writer.BaseStream.Position + length > MaximumBytes) throw new InvalidDataException("xml-string-limit");
        writer.Write(length); writer.Write(Utf8.GetBytes(value));
    }
    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > MaximumBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("xml-string-layout");
        return Utf8.GetString(reader.ReadBytes(length));
    }
    private static int Count(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumNodes || count > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("xml-count");
        return count;
    }
    private static bool Supported(XmlNodeType kind) => kind is XmlNodeType.Document or XmlNodeType.Element or XmlNodeType.Attribute
        or XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Comment or XmlNodeType.Whitespace
        or XmlNodeType.SignificantWhitespace or XmlNodeType.ProcessingInstruction or XmlNodeType.XmlDeclaration;
    private static int IndexOf(IReadOnlyList<LoadableXmlAsset> inputs, LoadableXmlAsset asset)
    { for (int i = 0; i < inputs.Count; i++) if (ReferenceEquals(inputs[i], asset)) return i; return -1; }
}
