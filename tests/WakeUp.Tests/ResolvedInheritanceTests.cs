// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class ResolvedInheritanceTests
{
    private static XmlDocument Document(string xml)
    { var document = new XmlDocument { PreserveWhitespace = true }; document.LoadXml(xml); return document; }

    [Test]
    public void GroupRestoresExactDetachedNodesUnderCurrentOwner()
    {
        var source = Document("<Defs><Template Name='Parent'><empty></empty><short/><text>before<![CDATA[<&>]]>after</text><!--comment--><?instruction value?><n:field xmlns:n='urn:n' n:value='yes'/></Template><Child ParentName='Parent'/></Defs>");
        var first = source.DocumentElement!.FirstChild!;
        // Preserve adjacent text nodes and attribute value-node boundaries;
        // concatenated serialization would lose both native observations.
        first.AppendChild(source.CreateTextNode("left")); first.AppendChild(source.CreateTextNode("right"));
        var attribute = source.CreateAttribute("segments");
        attribute.AppendChild(source.CreateTextNode("a")); attribute.AppendChild(source.CreateTextNode("b"));
        first.Attributes!.Append(attribute);
        first.Attributes.Append(source.CreateAttribute("noValueNode"));
        var roots = source.DocumentElement.ChildNodes.Cast<XmlNode>().ToArray();
        var current = Document("<Defs><Unrelated/></Defs>");
        XmlNode[] restored = ResolvedInheritanceFormat.DecodeRoots(ResolvedInheritanceFormat.EncodeRoots(roots), current);
        Assert.That(restored.Length, Is.EqualTo(2));
        for (int i = 0; i < roots.Length; i++)
        {
            Assert.That(ParsedXmlFormat.Equal(roots[i], restored[i]), Is.True);
            Assert.That(restored[i].OwnerDocument, Is.SameAs(current));
            Assert.That(restored[i].ParentNode, Is.Null);
            Assert.That(restored[i], Is.Not.SameAs(roots[i]));
        }
        restored[0].FirstChild!.InnerText = "changed";
        Assert.That(roots[0].FirstChild!.InnerText, Is.Empty);
        Assert.That(current.DocumentElement!.FirstChild!.Name, Is.EqualTo("Unrelated"));
    }

    [Test]
    public void GenerationBindsChildrenByOriginalOrdinalWithoutChangingInputRoots()
    {
        var document = Document("<Defs><Parent Name='P'/><Child ParentName='P'/><Other ParentName='P'/></Defs>");
        var resolved = Document("<Defs><Child><inherited>yes</inherited></Child><Other><inherited>yes</inherited></Other></Defs>");
        var generation = new Dictionary<int, XmlNode> { [2] = resolved.DocumentElement!.LastChild!, [1] = resolved.DocumentElement.FirstChild! };
        var decoded = ResolvedInheritanceRuntime.DecodeGeneration(ResolvedInheritanceRuntime.EncodeGeneration(generation), document, 3);
        Assert.That(decoded.Keys, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(decoded[1].OwnerDocument, Is.SameAs(document));
        Assert.That(decoded[1].ParentNode, Is.Null);
        Assert.That(document.DocumentElement!.ChildNodes[1]!.ChildNodes.Count, Is.Zero);
        Assert.That(decoded[1].FirstChild!.InnerText, Is.EqualTo("yes"));
    }

    [Test]
    public void GenerationRejectsOutOfRangeDuplicateAndTruncatedOrdinals()
    {
        var document = Document("<Defs><A/><B/></Defs>");
        var records = new Dictionary<int, XmlNode> { [0] = document.DocumentElement!.FirstChild!, [1] = document.DocumentElement.LastChild! };
        byte[] valid = ResolvedInheritanceRuntime.EncodeGeneration(records);
        byte[] duplicate = (byte[])valid.Clone(); Array.Copy(BitConverter.GetBytes(0), 0, duplicate, 12, 4);
        byte[] beyond = (byte[])valid.Clone(); Array.Copy(BitConverter.GetBytes(2), 0, beyond, 12, 4);
        Assert.Throws<InvalidDataException>((Action)delegate { ResolvedInheritanceRuntime.DecodeGeneration(duplicate, document, 2); });
        Assert.Throws<InvalidDataException>((Action)delegate { ResolvedInheritanceRuntime.DecodeGeneration(beyond, document, 2); });
        Assert.Throws<InvalidDataException>((Action)delegate { ResolvedInheritanceRuntime.DecodeGeneration(valid.Take(valid.Length - 1).ToArray(), document, 2); });
        Assert.Throws<InvalidDataException>((Action)delegate { ResolvedInheritanceRuntime.DecodeGeneration(valid.Concat(new byte[] { 0 }).ToArray(), document, 2); });
    }

    [Test]
    public void ActualParentRegistrationAndDuplicateFieldChangesInvalidateGeneration()
    {
        var document = Document("<Defs><Parent Name='P'><value>one</value></Parent><Child ParentName='P'/></Defs>");
        var map = new Dictionary<XmlNode, Verse.LoadableXmlAsset>();
        string Key(byte[] registration, string duplicates = "", string semantics = "native-v1")
            => ResolvedInheritanceRuntime.InputKey(document, map, registration, duplicates, semantics);
        string before = Key(new byte[] { 0, 1 });
        Assert.That(Key(new byte[] { 0, 1 }), Is.EqualTo(before));
        Assert.That(Key(new byte[] { 1, 0 }), Is.Not.EqualTo(before));
        Assert.That(Key(new byte[] { 0, 1 }, "customList"), Is.Not.EqualTo(before));
        Assert.That(Key(new byte[] { 0, 1 }, semantics: "native-v2"), Is.Not.EqualTo(before));
        document.DocumentElement!.FirstChild!.FirstChild!.InnerText = "two";
        Assert.That(Key(new byte[] { 0, 1 }), Is.Not.EqualTo(before));
    }

    [Test]
    public void UnknownAndLateDocumentObserversAreRefusedUntilRemoved()
    {
        var document = Document("<Defs/>");
        XmlNodeChangedEventHandler known = (_, _) => { };
        XmlNodeChangedEventHandler unknown = (_, _) => { };
        Assert.That(ResolvedInheritanceRuntime.HasForeignXmlObservers(document), Is.False);
        document.NodeInserted += known;
        Assert.That(ResolvedInheritanceRuntime.HasForeignXmlObservers(document), Is.True);
        Assert.That(ResolvedInheritanceRuntime.HasForeignXmlObservers(document, callback => callback == (Delegate)known), Is.False);
        document.NodeChanging += unknown;
        Assert.That(ResolvedInheritanceRuntime.HasForeignXmlObservers(document, callback => callback == (Delegate)known), Is.True);
        document.NodeChanging -= unknown; document.NodeInserted -= known;
        Assert.That(ResolvedInheritanceRuntime.HasForeignXmlObservers(document), Is.False);
    }

    [Test]
    public void UnsupportedEntityAndCorruptFormatFallBackBeforePublication()
    {
        var document = Document("<Defs/>");
        var entity = document.CreateEntityReference("unavailable");
        Assert.Throws<InvalidDataException>((Action)delegate { ResolvedInheritanceFormat.EncodeRoots(new[] { entity }); });
        byte[] bytes = ResolvedInheritanceFormat.EncodeRoots(new[] { document.DocumentElement! });
        bytes[0] ^= 0x40;
        Assert.Throws<InvalidDataException>((Action)delegate { ResolvedInheritanceFormat.DecodeRoots(bytes, document); });
        Assert.That(document.DocumentElement!.ChildNodes.Count, Is.Zero);
    }
}
