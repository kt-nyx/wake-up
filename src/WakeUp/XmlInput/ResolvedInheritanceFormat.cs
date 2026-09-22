// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace WakeUp;

// One bounded group of detached native results. Unlike source ImportNode,
// inheritance CloneNode preserves explicit empty-element state.
internal static class ResolvedInheritanceFormat
{
    internal const int MaximumPayload = 128 * 1024 * 1024;
    internal const int MaximumRoots = 131072;
    private const int MaximumNodes = 4_000_000, MaximumDepth = 256;
    private const int Magic = 0x31495257;

    internal static byte[] EncodeRoots(IReadOnlyList<XmlNode> roots)
    {
        if (roots.Count > MaximumRoots) throw new InvalidDataException("inheritance-root-limit");
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write(Magic); writer.Write(roots.Count);
        int remaining = MaximumNodes;
        foreach (var root in roots) WriteNode(writer, root, 0, ref remaining);
        writer.Flush(); return output.ToArray();
    }

    private static void WriteNode(BinaryWriter writer, XmlNode node, int depth, ref int remaining)
    {
        if (--remaining < 0 || depth > MaximumDepth) throw new InvalidDataException("inheritance-node-limit");
        writer.Write((byte)node.NodeType);
        switch (node.NodeType)
        {
            case XmlNodeType.Element:
                if (node.GetType() != typeof(XmlElement)) throw new InvalidDataException("inheritance-custom-node");
                var element = (XmlElement)node;
                writer.Write(element.Prefix); writer.Write(element.LocalName); writer.Write(element.NamespaceURI);
                writer.Write(element.IsEmpty); writer.Write(element.Attributes.Count);
                foreach (XmlAttribute attribute in element.Attributes)
                {
                    if (attribute.GetType() != typeof(XmlAttribute) || !attribute.Specified)
                        throw new InvalidDataException("inheritance-attribute-contract");
                    remaining -= 1 + attribute.ChildNodes.Count;
                    if (remaining < 0) throw new InvalidDataException("inheritance-node-limit");
                    foreach (XmlNode value in attribute.ChildNodes)
                        if (value.GetType() != typeof(XmlText)) throw new InvalidDataException("inheritance-attribute-value");
                    writer.Write(attribute.Prefix); writer.Write(attribute.LocalName); writer.Write(attribute.NamespaceURI);
                    writer.Write(attribute.ChildNodes.Count);
                    foreach (XmlNode value in attribute.ChildNodes) writer.Write(value.Value ?? "");
                }
                writer.Write(node.ChildNodes.Count);
                foreach (XmlNode child in node.ChildNodes) WriteNode(writer, child, depth + 1, ref remaining);
                break;
            case XmlNodeType.Text:
            case XmlNodeType.CDATA:
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
            case XmlNodeType.Comment:
                Type expected = node.NodeType == XmlNodeType.Text ? typeof(XmlText) : node.NodeType == XmlNodeType.CDATA ? typeof(XmlCDataSection)
                    : node.NodeType == XmlNodeType.Whitespace ? typeof(XmlWhitespace) : node.NodeType == XmlNodeType.SignificantWhitespace ? typeof(XmlSignificantWhitespace) : typeof(XmlComment);
                if (node.GetType() != expected) throw new InvalidDataException("inheritance-custom-node");
                writer.Write(node.Value ?? ""); break;
            case XmlNodeType.ProcessingInstruction:
                if (node.GetType() != typeof(XmlProcessingInstruction)) throw new InvalidDataException("inheritance-custom-node");
                writer.Write(node.Name); writer.Write(node.Value ?? ""); break;
            case XmlNodeType.XmlDeclaration:
                if (node.GetType() != typeof(XmlDeclaration)) throw new InvalidDataException("inheritance-custom-node");
                var declaration = (XmlDeclaration)node;
                writer.Write(declaration.Version); writer.Write(declaration.Encoding); writer.Write(declaration.Standalone); break;
            default: throw new InvalidDataException("inheritance-node-kind");
        }
        if (writer.BaseStream.Length > MaximumPayload) throw new InvalidDataException("inheritance-payload-limit");
    }

    internal static XmlNode[] DecodeRoots(byte[] bytes, XmlDocument owner)
    {
        if (bytes.Length > MaximumPayload || owner.GetType() != typeof(XmlDocument)) throw new InvalidDataException("inheritance-payload-limit");
        try
        {
            using var input = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(input, Encoding.UTF8, true);
            if (reader.ReadInt32() != Magic) throw new InvalidDataException("inheritance-format");
            int count = reader.ReadInt32();
            if (count < 0 || count > MaximumRoots || count > bytes.Length) throw new InvalidDataException("inheritance-root-limit");
            var roots = new XmlNode[count]; int remaining = MaximumNodes;
            for (int i = 0; i < count; i++) roots[i] = ReadNode(reader, owner, 0, ref remaining);
            if (input.Position != input.Length) throw new InvalidDataException("inheritance-trailing-data");
            return roots;
        }
        catch (Exception e) when (e is XmlException || e is ArgumentException || e is InvalidOperationException)
        { throw new InvalidDataException("inheritance-tree-contract", e); }
    }

    private static XmlNode ReadNode(BinaryReader reader, XmlDocument owner, int depth, ref int remaining)
    {
        if (--remaining < 0 || depth > MaximumDepth) throw new InvalidDataException("inheritance-node-limit");
        var kind = (XmlNodeType)reader.ReadByte();
        switch (kind)
        {
            case XmlNodeType.Element:
                var element = owner.CreateElement(reader.ReadString(), reader.ReadString(), reader.ReadString());
                bool empty = reader.ReadBoolean(); int attributes = reader.ReadInt32();
                if (attributes < 0 || attributes > remaining) throw new InvalidDataException("inheritance-attribute-limit");
                remaining -= attributes;
                for (int i = 0; i < attributes; i++)
                {
                    var attribute = owner.CreateAttribute(reader.ReadString(), reader.ReadString(), reader.ReadString());
                    int values = reader.ReadInt32();
                    if (values < 0 || values > remaining) throw new InvalidDataException("inheritance-attribute-value-limit");
                    remaining -= values;
                    for (int value = 0; value < values; value++) attribute.AppendChild(owner.CreateTextNode(reader.ReadString()));
                    if (element.Attributes[attribute.LocalName, attribute.NamespaceURI] != null) throw new InvalidDataException("inheritance-duplicate-attribute");
                    element.Attributes.Append(attribute);
                }
                int children = reader.ReadInt32();
                if (children < 0 || children > remaining || empty && children != 0) throw new InvalidDataException("inheritance-child-limit");
                for (int i = 0; i < children; i++) element.AppendChild(ReadNode(reader, owner, depth + 1, ref remaining));
                element.IsEmpty = empty; return element;
            case XmlNodeType.Text: return owner.CreateTextNode(reader.ReadString());
            case XmlNodeType.CDATA: return owner.CreateCDataSection(reader.ReadString());
            case XmlNodeType.Whitespace: return owner.CreateWhitespace(reader.ReadString());
            case XmlNodeType.SignificantWhitespace: return owner.CreateSignificantWhitespace(reader.ReadString());
            case XmlNodeType.Comment: return owner.CreateComment(reader.ReadString());
            case XmlNodeType.ProcessingInstruction: return owner.CreateProcessingInstruction(reader.ReadString(), reader.ReadString());
            case XmlNodeType.XmlDeclaration: return owner.CreateXmlDeclaration(reader.ReadString(), reader.ReadString(), reader.ReadString());
            default: throw new InvalidDataException("inheritance-node-kind");
        }
    }
}
