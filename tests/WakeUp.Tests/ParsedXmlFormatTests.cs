// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class ParsedXmlFormatTests
{
    private static XmlReaderSettings NativeSettings() => new()
    {
        IgnoreComments = true, IgnoreWhitespace = true,
        CheckCharacters = false, Async = false
    };

    private static XmlDocument Parse(string text)
        => ParsedXmlFormat.Parse(Encoding.UTF8.GetBytes(text), NativeSettings());

    private static XmlDocument Destination()
    {
        var result = new XmlDocument();
        result.AppendChild(result.CreateElement("Defs"));
        return result;
    }

    // Independent structural assertions: the product's equality helper is not
    // the oracle for its own serialization tests.
    private static void SameTree(XmlNode expected, XmlNode actual)
    {
        Assert.That(actual.NodeType, Is.EqualTo(expected.NodeType));
        Assert.That(actual.Name, Is.EqualTo(expected.Name));
        Assert.That(actual.Value, Is.EqualTo(expected.Value));
        Assert.That(actual.NamespaceURI, Is.EqualTo(expected.NamespaceURI));
        Assert.That(actual.Prefix, Is.EqualTo(expected.Prefix));
        if (expected is XmlElement element)
            Assert.That(((XmlElement)actual).IsEmpty, Is.EqualTo(element.IsEmpty));
        Assert.That(actual.Attributes?.Count ?? 0, Is.EqualTo(expected.Attributes?.Count ?? 0));
        if (expected.Attributes != null)
            for (int i = 0; i < expected.Attributes.Count; i++) SameTree(expected.Attributes[i]!, actual.Attributes![i]!);
        Assert.That(actual.ChildNodes.Count, Is.EqualTo(expected.ChildNodes.Count));
        for (int i = 0; i < expected.ChildNodes.Count; i++) SameTree(expected.ChildNodes[i]!, actual.ChildNodes[i]!);
    }

    [TestCase("<Defs />")]
    [TestCase("<Defs></Defs>")]
    [TestCase("<Defs><ThingDef><defName>Example</defName><label>café &amp; tea</label></ThingDef></Defs>")]
    [TestCase("<?xml version=\"1.0\" encoding=\"UTF-16\" standalone=\"yes\"?><Defs><A /></Defs>")]
    [TestCase("<Defs><!--ignored--><A /><A></A><B> </B></Defs>")]
    [TestCase("<Defs><A z=\"last\" a=\"first\" middle=\"&amp;&quot;\"/><A z=\"second\" /></Defs>")]
    [TestCase("<Defs>before<A>inside<B/>after</A>between<A/>tail</Defs>")]
    [TestCase("<WrongRoot><A/><A>value</A></WrongRoot>")]
    public void SupportedTreesPreserveSourceAndNativeImportedChildren(string xml)
    {
        var original = Parse(xml);
        byte[]? encoded = ParsedXmlFormat.Encode(original);
        Assert.That(encoded, Is.Not.Null);
        var combined = Destination();
        var tree = ParsedXmlFormat.Restore(encoded!, combined);
        SameTree(original, tree.Source);
        Assert.That(tree.Source, Is.Not.SameAs(original));
        Assert.That(tree.Roots.Length, Is.EqualTo(original.DocumentElement!.ChildNodes.Count));
        Assert.That(combined.DocumentElement!.ChildNodes.Count, Is.Zero,
            "Restoration must leave combined nodes private until publication.");
        for (int i = 0; i < tree.Roots.Length; i++)
        {
            SameTree(combined.ImportNode(original.DocumentElement.ChildNodes[i]!, true), tree.Roots[i]);
            Assert.That(tree.Roots[i].OwnerDocument, Is.SameAs(combined));
            Assert.That(tree.Roots[i].ParentNode, Is.Null);
            Assert.That(tree.Roots[i], Is.Not.SameAs(tree.Source.DocumentElement!.ChildNodes[i]));
            combined.DocumentElement.AppendChild(tree.Roots[i]);
        }
        var nativeCombined = Destination();
        foreach (XmlNode child in original.DocumentElement.ChildNodes)
            nativeCombined.DocumentElement!.AppendChild(nativeCombined.ImportNode(child, true));
        SameTree(nativeCombined, combined);
    }

    [Test]
    public void Utf8BomAndNativeCommentWhitespacePolicyArePreserved()
    {
        const string xml = "<?xml version=\"1.0\"?><Defs>\n <!--gone--> <A>  meaningful  </A>\n</Defs>";
        byte[] bytes = new byte[] { 239, 187, 191 }.Concat(Encoding.UTF8.GetBytes(xml)).ToArray();
        var parsed = ParsedXmlFormat.Parse(bytes, NativeSettings());
        Assert.That(parsed.DocumentElement!.ChildNodes.Count, Is.EqualTo(1));
        Assert.That(parsed.DocumentElement.FirstChild!.InnerText, Is.EqualTo("  meaningful  "));
        var restored = ParsedXmlFormat.Restore(ParsedXmlFormat.Encode(parsed)!, Destination());
        SameTree(Parse(xml), restored.Source);
    }

    [TestCase("")]
    [TestCase("<Defs><Broken></Defs>")]
    [TestCase("<!DOCTYPE Defs [<!ENTITY x 'x'>]><Defs>&x;</Defs>")]
    public void MalformedOrProhibitedInputRemainsAParseFailure(string xml)
        => Assert.Throws<XmlException>(new Action(() => { Parse(xml); }));

    [TestCase("<Defs xmlns=\"urn:defs\"><A/></Defs>")]
    [TestCase("<Defs xmlns:p=\"urn:defs\"><p:A/></Defs>")]
    [TestCase("<Defs><A xml:space=\"preserve\"> </A></Defs>")]
    [TestCase("<Defs><A><![CDATA[not normalized]]></A></Defs>")]
    [TestCase("<?instruction keep?><Defs><A/></Defs>")]
    [TestCase("<Defs><A/><?instruction keep?></Defs>")]
    public void UnsupportedNativeTreesRefuseWithoutMutation(string xml)
    {
        var source = Parse(xml);
        string before = source.OuterXml;
        Assert.That(ParsedXmlFormat.Encode(source), Is.Null);
        Assert.That(source.OuterXml, Is.EqualTo(before));
    }

    [Test]
    public void RetainedSourceAndCombinedReferencesHaveIndependentMutation()
    {
        var original = Parse("<Defs><A id=\"one\"><value>original</value></A><A id=\"two\"/></Defs>");
        var destination = Destination();
        var first = ParsedXmlFormat.Restore(ParsedXmlFormat.Encode(original)!, destination);
        var retained = (XmlElement)first.Source.DocumentElement!.FirstChild!;
        var imported = (XmlElement)first.Roots[0];
        foreach (XmlNode root in first.Roots) destination.DocumentElement!.AppendChild(root);
        imported.SetAttribute("id", "combined");
        imported.FirstChild!.InnerText = "combined value";
        Assert.That(retained.GetAttribute("id"), Is.EqualTo("one"));
        Assert.That(retained.FirstChild!.InnerText, Is.EqualTo("original"));
        retained.FirstChild!.InnerText = "source value";
        retained.AppendChild(first.Source.CreateElement("onlySource"));
        Assert.That(imported.FirstChild.InnerText, Is.EqualTo("combined value"));
        Assert.That(imported.ChildNodes.Count, Is.EqualTo(1));
        var second = ParsedXmlFormat.Restore(ParsedXmlFormat.Encode(original)!, Destination());
        SameTree(original, second.Source);
        Assert.That(second.Roots[0], Is.Not.SameAs(imported));
    }

    [Test]
    public void MixedSupportedAndRefusedFilesCanKeepNativeChildOrder()
    {
        var sources = new[] { Parse("<Defs><A>first</A></Defs>"),
            Parse("<Defs><B><![CDATA[native middle]]></B></Defs>"), Parse("<Defs><A>last</A></Defs>") };
        var destination = Destination();
        var control = Destination();
        int restored = 0, native = 0;
        foreach (var source in sources)
        {
            byte[]? encoded = ParsedXmlFormat.Encode(source);
            XmlNode[] roots;
            if (encoded == null)
            {
                native++;
                roots = source.DocumentElement!.ChildNodes.Cast<XmlNode>().Select(n => destination.ImportNode(n, true)).ToArray();
            }
            else { restored++; roots = ParsedXmlFormat.Restore(encoded, destination).Roots; }
            foreach (XmlNode root in roots) destination.DocumentElement!.AppendChild(root);
            foreach (XmlNode child in source.DocumentElement!.ChildNodes)
                control.DocumentElement!.AppendChild(control.ImportNode(child, true));
        }
        Assert.That(restored, Is.EqualTo(2));
        Assert.That(native, Is.EqualTo(1));
        SameTree(control, destination);
        // This proves format-level composition only; runtime callback/admission
        // and source attribution need the production integration tests.
    }

    private static byte[] Payload() => ParsedXmlFormat.Encode(Parse("<Defs><A>value</A></Defs>"))!;

    [TestCase("<Defs />")]
    [TestCase("<Defs></Defs>")]
    [TestCase("<?xml version=\"1.0\" encoding=\"UTF-16\" standalone=\"yes\"?><Defs><A /></Defs>")]
    [TestCase("<Defs><A z=\"last\" a=\"first\" middle=\"&amp;&quot;\"/><A></A><B>café &amp; tea</B></Defs>")]
    [TestCase("<Defs>before<A>inside<B/>after</A>between<A/>tail</Defs>")]
    [TestCase("<WrongRoot><A>  meaningful  </A><A/></WrongRoot>")]
    [TestCase("<Defs><A>before<!--ignored-->after</A></Defs>")]
    public void SourceOnlyLoaderPreservesNativeSourceAndLaterImports(string xml)
    {
        var original = Parse(xml);
        byte[] payload = ParsedXmlFormat.Encode(original)!;
        var restored = ParsedXmlFormat.RestoreSource(payload);
        var other = ParsedXmlFormat.RestoreSource(payload);
        SameTree(original, restored);
        SameTree(original, other);
        Assert.That(restored.GetType(), Is.EqualTo(typeof(XmlDocument)));
        Assert.That(restored, Is.Not.SameAs(other));
        var expected = Destination();
        var actual = Destination();
        foreach (XmlNode child in original.DocumentElement!.ChildNodes)
            expected.DocumentElement!.AppendChild(expected.ImportNode(child, true));
        foreach (XmlNode child in restored.DocumentElement!.ChildNodes)
        {
            Assert.That(child.OwnerDocument, Is.SameAs(restored));
            actual.DocumentElement!.AppendChild(actual.ImportNode(child, true));
        }
        SameTree(expected, actual);
        restored.DocumentElement.SetAttribute("changed", "only this source");
        SameTree(original, other);
        Assert.That(actual.DocumentElement!.HasAttribute("changed"), Is.False);
    }

    private static byte[] BinaryPayload(Action<XmlDictionaryWriter> write, params byte[] empty)
    {
        using var binary = new MemoryStream();
        using (var writer = XmlDictionaryWriter.CreateBinaryWriter(binary, null, null, false)) write(writer);
        using var output = new MemoryStream();
        using (var header = new BinaryWriter(output, Encoding.UTF8, true))
        {
            header.Write(0x31585057); header.Write(false);
            header.Write(empty.Length); header.Write(empty);
            header.Write((int)binary.Length); header.Write(binary.ToArray());
        }
        return output.ToArray();
    }

    [TestCase("namespace")]
    [TestCase("attribute-namespace")]
    [TestCase("comment")]
    [TestCase("shape-child")]
    [TestCase("shape-text")]
    [TestCase("shape-count")]
    [TestCase("invalid-name")]
    [TestCase("invalid-attribute-name")]
    public void SourceOnlyLoaderRefusesUnsupportedOrCorruptTrees(string corruption)
    {
        byte[] payload = BinaryPayload(writer =>
        {
            writer.WriteStartElement("", "Defs", corruption == "namespace" ? "urn:unsupported" : "");
            if (corruption == "attribute-namespace") writer.WriteAttributeString("p", "attribute", "urn:unsupported", "value");
            if (corruption == "invalid-attribute-name") writer.WriteAttributeString("attr", "value");
            if (corruption == "comment") writer.WriteComment("unsupported");
            writer.WriteStartElement("A"); writer.WriteString("text"); writer.WriteEndElement();
            writer.WriteEndElement();
        }, 0, 0);
        if (corruption == "shape-child") payload[9] = 1;
        if (corruption == "shape-text") payload[10] = 1;
        if (corruption == "shape-count")
        {
            payload = payload.Take(9).Concat(new byte[] { 0 }).Concat(payload.Skip(9)).ToArray();
            WriteInt(payload, 5, 3);
        }
        if (corruption == "invalid-name" || corruption == "invalid-attribute-name")
        {
            byte[] name = Encoding.UTF8.GetBytes(corruption == "invalid-name" ? "Defs" : "attr");
            int offset = Enumerable.Range(15, payload.Length - 15 - name.Length + 1)
                .First(i => payload.Skip(i).Take(name.Length).SequenceEqual(name));
            payload[offset] = (byte)' ';
        }
        Assert.Throws<InvalidDataException>(new Action(() => { ParsedXmlFormat.RestoreSource(payload); }));
    }

    [TestCase("truncated")]
    [TestCase("magic")]
    [TestCase("shape-bit")]
    [TestCase("binary-length")]
    [TestCase("trailing")]
    public void SourceOnlyLoaderRefusesBrokenEnvelope(string corruption)
    {
        byte[] payload = Payload();
        if (corruption == "truncated") payload = payload.Take(3).ToArray();
        if (corruption == "magic") payload[0] ^= 0x7f;
        if (corruption == "shape-bit") payload[9] = 2;
        if (corruption == "binary-length") WriteInt(payload, 9 + BitConverter.ToInt32(payload, 5), 0);
        if (corruption == "trailing") payload = payload.Concat(new byte[] { 0 }).ToArray();
        Assert.Throws<InvalidDataException>(new Action(() => { ParsedXmlFormat.RestoreSource(payload); }));
    }

    private static void WriteInt(byte[] bytes, int offset, int value)
        => Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4);

    [TestCase("magic")]
    [TestCase("negative-count")]
    [TestCase("excess-count")]
    [TestCase("shape-bit")]
    [TestCase("binary-length")]
    [TestCase("trailing")]
    [TestCase("truncated")]
    [TestCase("missing-shape")]
    [TestCase("extra-shape")]
    public void CorruptLayoutIsRejectedBeforeAnyCombinedPublication(string corruption)
    {
        byte[] payload = Payload();
        int count = BitConverter.ToInt32(payload, 5);
        switch (corruption)
        {
            case "magic": payload[0] ^= 0x7f; break;
            case "negative-count": WriteInt(payload, 5, -1); break;
            case "excess-count": WriteInt(payload, 5, ParsedXmlFormat.MaximumNodes + 1); break;
            case "shape-bit": payload[9] = 2; break;
            case "binary-length": WriteInt(payload, 9 + count, 0); break;
            case "trailing": payload = payload.Concat(new byte[] { 0 }).ToArray(); break;
            case "truncated": payload = payload.Take(payload.Length - 1).ToArray(); break;
            case "missing-shape":
                payload = payload.Take(9).Concat(payload.Skip(10)).ToArray();
                WriteInt(payload, 5, count - 1); break;
            case "extra-shape":
                payload = payload.Take(9).Concat(new byte[] { 0 }).Concat(payload.Skip(9)).ToArray();
                WriteInt(payload, 5, count + 1); break;
        }
        var combined = Destination();
        var sentinel = combined.CreateElement("alreadyPublished");
        combined.DocumentElement!.AppendChild(sentinel);
        Assert.Throws<InvalidDataException>(new Action(() => { ParsedXmlFormat.Restore(payload, combined); }));
        Assert.That(combined.DocumentElement.ChildNodes.Count, Is.EqualTo(1));
        Assert.That(combined.DocumentElement.FirstChild, Is.SameAs(sentinel));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void SelfClosingShapeCannotContainElementOrText(int elementIndex)
    {
        byte[] payload = Payload();
        payload[9 + elementIndex] = 1;
        var combined = Destination();
        Assert.Throws<InvalidDataException>(new Action(() => { ParsedXmlFormat.Restore(payload, combined); }));
        Assert.That(combined.DocumentElement!.ChildNodes.Count, Is.Zero);
    }

    [Test]
    public void OversizedPayloadIsRefusedBeforeReaderAllocation()
        => Assert.Throws<InvalidDataException>(new Action(() => { ParsedXmlFormat.Restore(
            new byte[ParsedXmlFormat.MaximumPayload + 1], Destination()); }));
}
