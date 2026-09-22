// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture]
public sealed class ProcessedXmlSnapshotTests
{
    private static byte[] Encode(XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset>? map = null, LoadableXmlAsset[]? assets = null)
        => ProcessedXmlSnapshot.Encode(document, map ?? new Dictionary<XmlNode, LoadableXmlAsset>(), assets ?? Array.Empty<LoadableXmlAsset>(),
            Array.Empty<byte>(), Array.Empty<int>(), Array.Empty<bool>(), Array.Empty<bool>());

    [Test]
    public void RemovedTemporaryNamesRemainAtomizedAfterRestart()
    {
        var document = new XmlDocument(); document.LoadXml("<Defs><Kept /></Defs>");
        var temporary = document.CreateElement("prefix", "RemovedTemporary", "urn:temporary");
        temporary.SetAttribute("RemovedAttribute", "value");
        document.DocumentElement!.AppendChild(temporary);
        string qualified = temporary.Name;
        document.DocumentElement.RemoveChild(temporary);
        document.NameTable.Add("ExplicitAtomWithoutNode");
        var restored = ProcessedXmlSnapshot.Decode(Encode(document), Array.Empty<LoadableXmlAsset>(), 0);
        Assert.Multiple((Action)delegate
        {
            Assert.That(restored.Document.OuterXml, Is.EqualTo(document.OuterXml));
            foreach (string atom in new[] { "prefix", "RemovedTemporary", "urn:temporary", "RemovedAttribute", qualified, "ExplicitAtomWithoutNode" })
                Assert.That(restored.Document.NameTable.Get(atom), Is.EqualTo(atom));
            Assert.That(restored.Document.NameTable.Get("NeverPresent"), Is.Null);
            Assert.That(restored.Document.NameTable.Get("Kept"), Is.SameAs(restored.Document.DocumentElement!.FirstChild!.LocalName));
        });
        // Randomized native hash buckets must not make the generation unstable.
        Assert.That(Encode(restored.Document), Is.EqualTo(Encode(document)));
    }

    [Test]
    public void RestoresMappedUnmappedAndDetachedRootsWithRealCurrentSources()
    {
        var document = new XmlDocument(); document.LoadXml("<Defs><Kept><defName>Live</defName></Kept><Removed><defName>Old</defName></Removed></Defs>");
        XmlNode kept = document.DocumentElement!.FirstChild!, removed = document.DocumentElement.LastChild!;
        var oldSources = new[] { Asset("first.xml"), Asset("second.xml") };
        var map = new Dictionary<XmlNode, LoadableXmlAsset> { [kept] = oldSources[0], [removed] = oldSources[1] };
        document.DocumentElement.RemoveChild(removed);
        var added = document.CreateElement("AddedByPatch"); document.DocumentElement.AppendChild(added);
        byte[] payload = Encode(document, map, oldSources);
        var currentSources = new[] { Asset("first.xml"), Asset("second.xml") };
        var restored = ProcessedXmlSnapshot.Decode(payload, currentSources, 0);
        XmlNode live = restored.Document.DocumentElement!.FirstChild!, unmapped = restored.Document.DocumentElement.LastChild!;
        XmlNode detached = restored.Map.Single(pair => ReferenceEquals(pair.Value, currentSources[1])).Key;
        Assert.Multiple((Action)delegate
        {
            Assert.That(restored.Document.OuterXml, Is.EqualTo(document.OuterXml));
            Assert.That(restored.Map.Count, Is.EqualTo(2));
            Assert.That(restored.Map[live], Is.SameAs(currentSources[0]));
            Assert.That(restored.Map.ContainsKey(unmapped), Is.False);
            Assert.That(detached.ParentNode, Is.Null);
            Assert.That(detached.OwnerDocument, Is.SameAs(restored.Document));
            Assert.That(detached.OuterXml, Is.EqualTo(removed.OuterXml));
            Assert.That(restored.Map.Values.Any(source => oldSources.Contains(source)), Is.False);
            Assert.That(currentSources[0].xmlDoc!.OuterXml, Is.EqualTo("<Defs><OriginalSource /></Defs>"));
        });
    }

    [Test]
    public void CustomNameTableRefusesBeforeSerialization()
    {
        var document = new XmlDocument(new DerivedNameTable()); document.LoadXml("<Defs />");
        Assert.Throws<InvalidDataException>((Action)delegate { Encode(document); });
    }

    [Test]
    public void NativeModNameCallCountsRoundTripForEachActualOperation()
    {
        var document = new XmlDocument(); document.LoadXml("<Defs />");
        byte[] bytes = ProcessedXmlSnapshot.Encode(document, new Dictionary<XmlNode, LoadableXmlAsset>(), Array.Empty<LoadableXmlAsset>(),
            Array.Empty<byte>(), new[] { -1, 0, 0 }, new[] { true, false, true }, new[] { true, false, true }, new[] { 0, 0, 2 });
        var restored = ProcessedXmlSnapshot.Decode(bytes, Array.Empty<LoadableXmlAsset>(), 3);
        Assert.That(restored.ModNameCalls, Is.EqualTo(new[] { 0, 0, 2 }));
        Assert.Throws<InvalidDataException>((Action)delegate { ProcessedXmlSnapshot.Decode(bytes, Array.Empty<LoadableXmlAsset>(), 2); });

        byte[] defaults = ProcessedXmlSnapshot.Encode(document, new Dictionary<XmlNode, LoadableXmlAsset>(), Array.Empty<LoadableXmlAsset>(),
            Array.Empty<byte>(), new[] { -1 }, new[] { true }, new[] { true });
        Assert.That(ProcessedXmlSnapshot.Decode(defaults, Array.Empty<LoadableXmlAsset>(), 1).ModNameCalls, Is.EqualTo(new[] { 0 }));
    }

    [TestCase(-1)]
    [TestCase(ProcessedXmlSnapshot.MaximumModNameCalls + 1)]
    public void InvalidNativeCallCountsRefuseBothEncodingAndDecoding(int count)
    {
        var document = new XmlDocument(); document.LoadXml("<Defs />");
        var map = new Dictionary<XmlNode, LoadableXmlAsset>(); var assets = Array.Empty<LoadableXmlAsset>();
        Assert.Throws<InvalidDataException>((Action)delegate { ProcessedXmlSnapshot.Encode(document, map, assets, Array.Empty<byte>(),
            new[] { -1 }, new[] { true }, new[] { true }, new[] { count }); });
        byte[] valid = ProcessedXmlSnapshot.Encode(document, map, assets, Array.Empty<byte>(), new[] { -1 }, new[] { true }, new[] { true }, new[] { 1 });
        Buffer.BlockCopy(BitConverter.GetBytes(count), 0, valid, valid.Length - sizeof(int), sizeof(int));
        Assert.Throws<InvalidDataException>((Action)delegate { ProcessedXmlSnapshot.Decode(valid, assets, 1); });
    }

    [Test]
    public void UncalledOperationsCannotAcquireNativeLookupEffects()
    {
        var document = new XmlDocument(); document.LoadXml("<Defs />");
        var map = new Dictionary<XmlNode, LoadableXmlAsset>(); var assets = Array.Empty<LoadableXmlAsset>();
        Assert.Throws<InvalidDataException>((Action)delegate { ProcessedXmlSnapshot.Encode(document, map, assets, Array.Empty<byte>(),
            new[] { -1 }, new[] { false }, new[] { false }, new[] { 1 }); });
        byte[] valid = ProcessedXmlSnapshot.Encode(document, map, assets, Array.Empty<byte>(), new[] { -1 }, new[] { false }, new[] { false });
        Buffer.BlockCopy(BitConverter.GetBytes(1), 0, valid, valid.Length - sizeof(int), sizeof(int));
        Assert.Throws<InvalidDataException>((Action)delegate { ProcessedXmlSnapshot.Decode(valid, assets, 1); });
        Assert.Throws<InvalidDataException>((Action)delegate { ProcessedXmlAdapters.ReplayModNameEffects(new PatchOperationTest(), 1); });
    }

    private sealed class DerivedNameTable : NameTable { }
    private static LoadableXmlAsset Asset(string name) => new(name, "<Defs><OriginalSource /></Defs>");
}
