// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class ScopedSingleQueryMemoTests
{
    [TestCase("Defs/ThingDef[defName='Beer' or defName='Wine']/statBases/*")]
    [TestCase("Defs/ThingDef[defName='Beer' and @Name='Drink']/statBases/*")]
    [TestCase("Defs/ThingDef[defName='Beer'][1]/@Name")]
    [TestCase("Defs/ThingDef[defName='Absent']")]
    [TestCase("Defs/ThingDef[2]/preceding-sibling::*")]
    public void CompletedNativeSingleQueriesReuseTheSameFirstNodeOrMiss(string xpath)
    {
        XmlDocument document = Sample();
        using var memo = new ScopedSingleQueryMemo(document);
        XmlNode? native = document.SelectSingleNode(xpath);
        Assert.That(Query(memo, document, xpath), Is.SameAs(native));
        Assert.That(Query(memo, document, xpath), Is.SameAs(native));
        Assert.That(memo.Hits, Is.EqualTo(1));
        Assert.That(memo.Captures, Is.EqualTo(1));
        Assert.That(memo.MissHits, Is.EqualTo(native == null ? 1 : 0));
    }

    [Test]
    public void DescendantContextsUseReferenceIdentityAndDetachedNodesStayNative()
    {
        XmlDocument document = Sample();
        XmlNode first = document.DocumentElement!.FirstChild!;
        XmlNode second = document.DocumentElement.LastChild!;
        using var memo = new ScopedSingleQueryMemo(document);
        foreach (XmlNode context in new[] { first, second })
        {
            Assert.That(Query(memo, context, "statBases/*"), Is.SameAs(context.SelectSingleNode("statBases/*")));
            Assert.That(Query(memo, context, "statBases/*"), Is.SameAs(context.SelectSingleNode("statBases/*")));
        }
        Assert.That(memo.Hits, Is.EqualTo(2));
        document.DocumentElement.RemoveChild(first);
        long hits = memo.Hits;
        Query(memo, first, "statBases/*");
        Query(memo, first, "statBases/*");
        Assert.That(memo.Hits, Is.EqualTo(hits));
    }

    [Test]
    public void MissesAndOrderAndAttributePredicatesRefreshAfterRealMutations()
    {
        XmlDocument document = Sample();
        using var memo = new ScopedSingleQueryMemo(document);
        const string missing = "Defs/ThingDef[@Name='New']/statBases/Mass";
        Assert.That(Query(memo, document, missing), Is.Null);
        Assert.That(Query(memo, document, missing), Is.Null);
        XmlElement first = (XmlElement)document.DocumentElement!.FirstChild!;
        first.SetAttribute("Name", "New");
        Assert.That(Query(memo, document, missing), Is.SameAs(first["statBases"]!["Mass"]));
        XmlNode saved = Query(memo, document, "Defs/ThingDef[1]")!;
        document.DocumentElement.PrependChild(document.DocumentElement.LastChild!);
        Assert.That(Query(memo, document, "Defs/ThingDef[1]"), Is.Not.SameAs(saved));
        first["statBases"]!.RemoveChild(first["statBases"]!["Mass"]!);
        Assert.That(Query(memo, document, missing), Is.Null);
        first["defName"]!.InnerText = "Mead";
        Assert.That(Query(memo, document, "Defs/ThingDef[defName='Mead']"), Is.SameAs(first));
    }

    [Test]
    public void MutationCallbacksOnBothSidesCannotPublishAnOldResult()
    {
        XmlDocument document = Sample();
        ScopedSingleQueryMemo? memo = null;
        const string xpath = "Defs/ThingDef[defName='Wine']";
        XmlNode name = document.DocumentElement!.LastChild!["defName"]!.FirstChild!;
        document.NodeChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Node, name))
                Assert.That(Query(memo!, document, xpath), Is.Null);
        };
        using (memo = new ScopedSingleQueryMemo(document))
        {
            Query(memo, document, xpath);
            document.NodeChanging += (_, args) =>
            {
                if (!ReferenceEquals(args.Node, name))
                    return;
                Assert.That(Query(memo, document, xpath), Is.Not.Null);
                document.DocumentElement!.FirstChild!["statBases"]!["Mass"]!.InnerText = "3";
            };
            name.Value = "Mead";
            Assert.That(Query(memo, document, xpath), Is.Null);
            Assert.That(Query(memo, document, xpath), Is.Null);
            Assert.That(memo.Hits, Is.EqualTo(1));
        }
    }

    [Test]
    public void AbortedMutationKeepsAllSubsequentCallsNative()
    {
        XmlDocument document = Sample();
        using var memo = new ScopedSingleQueryMemo(document);
        const string xpath = "Defs/ThingDef";
        Query(memo, document, xpath);
        XmlNodeChangedEventHandler abort = (_, _) => throw new InvalidOperationException("abort");
        document.NodeChanging += abort;
        Assert.Throws<InvalidOperationException>((Action)(() => document.DocumentElement!.FirstChild!["defName"]!.FirstChild!.Value = "Mead"));
        document.NodeChanging -= abort;
        Query(memo, document, xpath);
        Query(memo, document, xpath);
        Assert.That(memo.Hits, Is.Zero);
    }

    [Test]
    public void InFlightCaptureCannotCrossMutationOrForeignThread()
    {
        XmlDocument document = Sample();
        using var memo = new ScopedSingleQueryMemo(document);
        const string xpath = "Defs/ThingDef[1]";
        Assert.That(memo.TryRead(document, xpath, out _, out long version), Is.False);
        XmlNode original = document.SelectSingleNode(xpath)!;
        document.DocumentElement!.PrependChild(document.DocumentElement.LastChild!);
        memo.Capture(document, xpath, version, original);
        Assert.That(memo.Captures, Is.Zero);
        Query(memo, document, xpath);
        var foreign = new Thread(() => memo.TryRead(document, xpath, out _, out _));
        foreign.Start();
        foreign.Join();
        Query(memo, document, xpath);
        Query(memo, document, xpath);
        Assert.That(memo.Hits, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CustomNodesAlreadyPresentOrInsertedAreNeverMemoized(bool afterConstruction)
    {
        XmlDocument document = Sample();
        var custom = new CustomElement(document);
        if (!afterConstruction)
            document.DocumentElement!.AppendChild(custom);
        using var memo = new ScopedSingleQueryMemo(document);
        if (afterConstruction)
            document.DocumentElement!.AppendChild(custom);
        Query(memo, document, "Defs/ThingDef");
        Query(memo, document, "Defs/ThingDef");
        Assert.That(memo.Captures, Is.Zero);
    }

    [Test]
    public void ErrorsCapacityAndDisposalRetainNativeBehavior()
    {
        XmlDocument document = Sample();
        using var memo = new ScopedSingleQueryMemo(document, maximumEntries: 1);
        Assert.Throws<XPathException>((Action)(() => Query(memo, document, "Defs/[")));
        Assert.Throws<XPathException>((Action)(() => Query(memo, document, "Defs/[")));
        Assert.That(memo.Captures, Is.Zero);
        Query(memo, document, "Defs/ThingDef[1]");
        Query(memo, document, "Defs/ThingDef[2]");
        Query(memo, document, "Defs/ThingDef[2]");
        Assert.That(memo.Captures, Is.EqualTo(1));
        Assert.That(memo.Hits, Is.Zero);
        memo.Dispose();
        Assert.That(Query(memo, document, "Defs/ThingDef[1]"), Is.SameAs(document.SelectSingleNode("Defs/ThingDef[1]")));
        Assert.That(memo.Hits, Is.Zero);
    }

    private static XmlNode? Query(ScopedSingleQueryMemo memo, XmlNode context, string xpath)
    {
        if (memo.TryRead(context, xpath, out XmlNode? result, out long version))
            return result;
        result = context.SelectSingleNode(xpath);
        memo.Capture(context, xpath, version, result);
        return result;
    }

    private static XmlDocument Sample()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs><ThingDef Name='Drink'><defName>Beer</defName><statBases><Mass>1</Mass><MarketValue>2</MarketValue></statBases></ThingDef>"
            + "<ThingDef><defName>Wine</defName><statBases><Mass>3</Mass></statBases></ThingDef></Defs>");
        return document;
    }

    private sealed class CustomElement : XmlElement
    {
        internal CustomElement(XmlDocument document) : base(string.Empty, "Custom", string.Empty, document) { }
    }
}
