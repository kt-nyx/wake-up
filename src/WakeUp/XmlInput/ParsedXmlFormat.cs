// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace WakeUp;

// Records are grouped into selected batches. The source-only consumer uses the
// native loader; the retained dual-owner decoder supports its existing oracle.
// Nonrepresentable native trees remain native, including CDATA, PI and namespaces.
internal static class ParsedXmlFormat
{
    internal const int MaximumPayload = 32 * 1024 * 1024;
    internal const int MaximumNodes = 2_000_000;
    internal const int MaximumDepth = 256;
    private const int Magic = 0x31585057; // WPX1
    internal sealed class Tree
    {
        internal readonly XmlDocument Source;
        internal readonly XmlNode[] Roots;
        internal readonly bool Restored;
        internal Tree(XmlDocument source, XmlNode[] roots, bool restored = false) { Source = source; Roots = roots; Restored = restored; }
    }

    internal static XmlDocument Parse(byte[] bytes, XmlReaderSettings settings)
    {
        int offset = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
        using var text = new StringReader(Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset));
        using var reader = XmlReader.Create(text, settings);
        var result = new XmlDocument(); result.Load(reader); return result;
    }

    internal static byte[]? Encode(XmlDocument source)
    {
        if (source.GetType() != typeof(XmlDocument) || source.DocumentElement == null) return null;
        var empties = new List<byte>();
        int events = 0;
        bool Accept(XmlNode node, int depth)
        {
            events += node is XmlElement ? 2 : 1;
            if (depth > MaximumDepth || events > MaximumNodes) return false;
            if (node is XmlElement element && element.GetType() == typeof(XmlElement))
            {
                if (element.NamespaceURI.Length != 0 || element.Prefix.Length != 0) return false;
                foreach (XmlAttribute attr in element.Attributes)
                    if (attr.GetType() != typeof(XmlAttribute) || attr.NamespaceURI.Length != 0 || attr.Prefix.Length != 0
                        || attr.Name == "xmlns" || !attr.Specified) return false;
                empties.Add(element.IsEmpty ? (byte)1 : (byte)0);
                foreach (XmlNode child in element.ChildNodes) if (!Accept(child, depth + 1)) return false;
                return true;
            }
            return node.GetType() == typeof(XmlText);
        }
        if (!Accept(source.DocumentElement, 0)) return null;
        XmlDeclaration? declaration = null;
        foreach (XmlNode child in source.ChildNodes)
            if (child is XmlDeclaration d && declaration == null) declaration = d;
            else if (!ReferenceEquals(child, source.DocumentElement)) return null;
        using var binary = new MemoryStream();
        using (var writer = XmlDictionaryWriter.CreateBinaryWriter(binary, null, null, false))
        using (var reader = new XmlNodeReader(source.DocumentElement)) writer.WriteNode(reader, false);
        if (binary.Length > MaximumPayload - empties.Count - 1024) return null;
        using var output = new MemoryStream();
        using (var header = new BinaryWriter(output, Encoding.UTF8, true))
        {
            header.Write(Magic); header.Write(declaration != null);
            if (declaration != null) { header.Write(declaration.Version); header.Write(declaration.Encoding); header.Write(declaration.Standalone); }
            header.Write(empties.Count); header.Write(empties.ToArray());
            header.Write((int)binary.Length); header.Write(binary.ToArray());
        }
        return output.ToArray();
    }

    // Restore only the real source document with the platform's own loader.
    // Native combination will import its children later. The binary loader's
    // private construction skips name checks, so validate names and the complete
    // supported tree before returning anything to the caller.
    internal static XmlDocument RestoreSource(byte[] payload)
    {
        try
        {
            if (payload.Length > MaximumPayload) throw new InvalidDataException("xml-payload-size");
            using var input = new MemoryStream(payload, false);
            using var header = new BinaryReader(input, Encoding.UTF8, true);
            if (header.ReadInt32() != Magic) throw new InvalidDataException("xml-schema");
            string? version = null, encoding = null, standalone = null;
            if (header.ReadBoolean())
            {
                version = header.ReadString(); encoding = header.ReadString(); standalone = header.ReadString();
                if (version.Length > 8 || encoding.Length > 64 || standalone.Length > 8)
                    throw new InvalidDataException("xml-declaration");
            }
            int count = header.ReadInt32();
            if (count < 1 || count > MaximumNodes || count > input.Length - input.Position)
                throw new InvalidDataException("xml-shape-count");
            byte[] empty = header.ReadBytes(count);
            if (empty.Any(b => b > 1)) throw new InvalidDataException("xml-shape-bit");
            int length = header.ReadInt32();
            if (length < 1 || length != input.Length - input.Position) throw new InvalidDataException("xml-binary-length");
            var quotas = new XmlDictionaryReaderQuotas { MaxDepth = MaximumDepth + 1, MaxArrayLength = MaximumPayload,
                MaxStringContentLength = MaximumPayload, MaxBytesPerRead = MaximumPayload, MaxNameTableCharCount = MaximumPayload };
            using var reader = XmlDictionaryReader.CreateBinaryReader(payload, (int)input.Position, length, quotas);
            // Retain otherwise discarded whitespace nodes so unsupported binary
            // content cannot silently disappear before the validation pass.
            var source = new XmlDocument { PreserveWhitespace = true };
            source.Load(reader);
            if (source.DocumentElement == null || source.FirstChild != source.DocumentElement
                || source.LastChild != source.DocumentElement)
                throw new InvalidDataException("xml-document-contract");

            var names = new HashSet<string>(StringComparer.Ordinal);
            int index = 0, nodes = 0;
            void Name(string name)
            {
                if (names.Add(name)) XmlConvert.VerifyNCName(name);
            }
            void Validate(XmlNode node, int depth)
            {
                nodes += node is XmlElement ? 2 : 1;
                if (depth > MaximumDepth || nodes > MaximumNodes) throw new InvalidDataException("xml-node-limit");
                if (node is XmlElement element && node.GetType() == typeof(XmlElement))
                {
                    if (index >= count || element.Prefix.Length != 0 || element.NamespaceURI.Length != 0)
                        throw new InvalidDataException("xml-element-contract");
                    Name(element.LocalName);
                    bool isEmpty = empty[index++] != 0;
                    if (isEmpty && element.HasChildNodes) throw new InvalidDataException("xml-empty-with-child");
                    if (element.HasAttributes)
                    {
                        var attributes = new HashSet<string>(StringComparer.Ordinal);
                        foreach (XmlAttribute attr in element.Attributes)
                        {
                            if (attr.GetType() != typeof(XmlAttribute) || attr.Prefix.Length != 0 || attr.NamespaceURI.Length != 0
                                || attr.Name == "xmlns" || !attr.Specified || !attributes.Add(attr.LocalName))
                                throw new InvalidDataException("xml-attribute-contract");
                            Name(attr.LocalName);
                            for (XmlNode? child = attr.FirstChild; child != null; child = child.NextSibling)
                                if (child.GetType() != typeof(XmlText)) throw new InvalidDataException("xml-attribute-value-contract");
                        }
                    }
                    // Setting true removes children; the check above must precede it.
                    element.IsEmpty = isEmpty;
                    for (XmlNode? child = element.FirstChild; child != null; child = child.NextSibling) Validate(child, depth + 1);
                }
                else if (node.GetType() != typeof(XmlText)) throw new InvalidDataException("xml-unsupported-binary-node");
            }
            Validate(source.DocumentElement, 0);
            if (index != count) throw new InvalidDataException("xml-incomplete-tree");
            source.PreserveWhitespace = false;
            if (version != null) source.PrependChild(source.CreateXmlDeclaration(version, encoding, standalone));
            return source;
        }
        catch (XmlException error) { throw new InvalidDataException("xml-invalid-binary", error); }
        catch (ArgumentException error) { throw new InvalidDataException("xml-invalid-binary", error); }
        catch (EndOfStreamException error) { throw new InvalidDataException("xml-truncated", error); }
    }

    internal static Tree Restore(byte[] payload, XmlDocument combined)
    {
        if (payload.Length > MaximumPayload) throw new InvalidDataException("xml-payload-size");
        using var input = new MemoryStream(payload, false);
        using var header = new BinaryReader(input, Encoding.UTF8, true);
        if (header.ReadInt32() != Magic) throw new InvalidDataException("xml-schema");
        var source = new XmlDocument();
        if (header.ReadBoolean())
        {
            string version = header.ReadString(), encoding = header.ReadString(), standalone = header.ReadString();
            if (version.Length > 8 || encoding.Length > 64 || standalone.Length > 8) throw new InvalidDataException("xml-declaration");
            source.AppendChild(source.CreateXmlDeclaration(version, encoding, standalone));
        }
        int count = header.ReadInt32();
        if (count < 1 || count > MaximumNodes || count > input.Length - input.Position) throw new InvalidDataException("xml-shape-count");
        byte[] empty = header.ReadBytes(count);
        if (empty.Any(b => b > 1)) throw new InvalidDataException("xml-shape-bit");
        int length = header.ReadInt32();
        if (length < 1 || length != input.Length - input.Position) throw new InvalidDataException("xml-binary-length");
        byte[] binary = header.ReadBytes(length);
        var quotas = new XmlDictionaryReaderQuotas { MaxDepth = MaximumDepth + 1, MaxArrayLength = MaximumPayload,
            MaxStringContentLength = MaximumPayload, MaxBytesPerRead = MaximumPayload, MaxNameTableCharCount = MaximumPayload };
        using var reader = XmlDictionaryReader.CreateBinaryReader(binary, quotas);
        var stack = new Stack<(XmlElement source, XmlElement? combined)>();
        var roots = new List<XmlNode>();
        int index = 0, nodes = 0;
        while (reader.Read())
        {
            if (++nodes > MaximumNodes) throw new InvalidDataException("xml-node-limit");
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (index >= count || stack.Count > MaximumDepth || reader.Prefix.Length != 0 || reader.NamespaceURI.Length != 0)
                    throw new InvalidDataException("xml-element-contract");
                var s = source.CreateElement(reader.Name);
                XmlElement? c = stack.Count == 0 ? null : combined.CreateElement(reader.Name);
                bool isEmpty = empty[index++] != 0;
                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        if (reader.Prefix.Length != 0 || reader.NamespaceURI.Length != 0 || reader.Name == "xmlns")
                            throw new InvalidDataException("xml-attribute-contract");
                        s.SetAttribute(reader.Name, reader.Value); c?.SetAttribute(reader.Name, reader.Value);
                    } while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }
                if (stack.Count == 0) source.AppendChild(s);
                else
                {
                    var parent = stack.Peek();
                    if (parent.source.IsEmpty) throw new InvalidDataException("xml-empty-with-child");
                    parent.source.AppendChild(s);
                    if (parent.combined == null) roots.Add(c!); else parent.combined.AppendChild(c!);
                }
                s.IsEmpty = isEmpty;
                // Native ImportNode creates an empty destination element and
                // appends children. Unlike the source it normalizes an explicit
                // empty pair (<x></x>) to IsEmpty=true when there are no children.
                // Binary XML does not preserve lexical empty-element syntax.
                // The sidecar restores it; the reader still supplies EndElement.
                if (!reader.IsEmptyElement) stack.Push((s, c));
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (stack.Count == 0) throw new InvalidDataException("xml-unbalanced-end");
                stack.Pop();
            }
            else if (reader.NodeType == XmlNodeType.Text)
            {
                if (stack.Count == 0) throw new InvalidDataException("xml-outside-text");
                var parent = stack.Peek();
                if (parent.source.IsEmpty) throw new InvalidDataException("xml-empty-with-text");
                parent.source.AppendChild(source.CreateTextNode(reader.Value));
                var child = combined.CreateTextNode(reader.Value);
                if (parent.combined == null) roots.Add(child); else parent.combined.AppendChild(child);
            }
            else throw new InvalidDataException("xml-unsupported-binary-node");
        }
        if (index != count || stack.Count != 0 || source.DocumentElement == null) throw new InvalidDataException("xml-incomplete-tree");
        return new Tree(source, roots.ToArray(), true);
    }

    internal static bool Equal(XmlNode expected, XmlNode actual)
    {
        if (expected.NodeType != actual.NodeType || expected.Name != actual.Name || expected.Value != actual.Value
            || expected.NamespaceURI != actual.NamespaceURI || expected.Prefix != actual.Prefix) return false;
        if (expected is XmlElement e && (!(actual is XmlElement a) || e.IsEmpty != a.IsEmpty)) return false;
        if ((expected.Attributes?.Count ?? 0) != (actual.Attributes?.Count ?? 0) || expected.ChildNodes.Count != actual.ChildNodes.Count) return false;
        if (expected.Attributes != null)
            for (int i = 0; i < expected.Attributes.Count; i++) if (!Equal(expected.Attributes[i]!, actual.Attributes![i]!)) return false;
        for (int i = 0; i < expected.ChildNodes.Count; i++) if (!Equal(expected.ChildNodes[i]!, actual.ChildNodes[i]!)) return false;
        return true;
    }
}
