// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class RetainedXmlPublicationTests
{
    [Test]
    public void NativeReplaceAndRemovalKeepEveryOldAliasAndNewSiblingOrder()
    {
        XmlDocument actual = Parse("<Defs><ThingDef><old><retained>value</retained></old><middle/><last/></ThingDef></Defs>");
        XmlDocument expected = Parse(actual.OuterXml);
        XmlElement parent = (XmlElement)actual.DocumentElement!.FirstChild!;
        XmlElement old = (XmlElement)parent.FirstChild!;
        XmlNode descendant = old.FirstChild!;
        XmlNode text = descendant.FirstChild!;
        XmlElement originalLast = (XmlElement)parent.LastChild!;
        XmlElement x = actual.CreateElement("x");
        XmlElement y = actual.CreateElement("y");
        var edits = new[]
        {
            RetainedXmlPublication.Edit.Insert(parent, x, old),
            RetainedXmlPublication.Edit.Insert(parent, y, old),
            RetainedXmlPublication.Edit.Remove(parent, old),
            RetainedXmlPublication.Edit.Remove(parent, originalLast)
        };
        XmlElement native = (XmlElement)expected.DocumentElement!.FirstChild!;
        XmlNode nativeOld = native.FirstChild!;
        native.InsertBefore(expected.CreateElement("x"), nativeOld);
        native.InsertBefore(expected.CreateElement("y"), nativeOld);
        native.RemoveChild(nativeOld);
        native.RemoveChild(native.LastChild!);

        Prepare(actual, edits).Commit();

        Assert.That(actual.OuterXml, Is.EqualTo(expected.OuterXml));
        Assert.That(old.ParentNode, Is.Null);
        Assert.That(old.NextSibling, Is.Null);
        Assert.That(old.PreviousSibling, Is.Null);
        Assert.That(old.FirstChild, Is.SameAs(descendant));
        Assert.That(descendant.ParentNode, Is.SameAs(old));
        Assert.That(descendant.FirstChild, Is.SameAs(text));
        Assert.That(text.OwnerDocument, Is.SameAs(actual));
        Assert.That(originalLast.ParentNode, Is.Null);
        Assert.That(parent.FirstChild, Is.SameAs(x));
        Assert.That(x.NextSibling, Is.SameAs(y));
        Assert.That(y.PreviousSibling, Is.SameAs(x));
    }

    [Test]
    public void SequentialNewNodesCanBeMutatedRemovedAndReinserted()
    {
        XmlDocument document = Parse("<Defs><a/><b/></Defs>");
        XmlElement root = document.DocumentElement!;
        XmlElement a = (XmlElement)root.FirstChild!;
        XmlElement b = (XmlElement)root.LastChild!;
        XmlElement newParent = document.CreateElement("newParent");
        XmlElement newChild = document.CreateElement("newChild");
        XmlElement replacement = document.CreateElement("replacement");
        Prepare(document, new[]
        {
            RetainedXmlPublication.Edit.Insert(a, newParent),
            RetainedXmlPublication.Edit.Insert(newParent, newChild),
            RetainedXmlPublication.Edit.Remove(a, newParent),
            RetainedXmlPublication.Edit.Insert(b, newParent),
            RetainedXmlPublication.Edit.Insert(newParent, replacement, newChild),
            RetainedXmlPublication.Edit.Remove(newParent, newChild)
        }).Commit();
        Assert.That(document.OuterXml, Is.EqualTo("<Defs><a></a><b><newParent><replacement /></newParent></b></Defs>"));
        Assert.That(newParent.ParentNode, Is.SameAs(b));
        Assert.That(newParent.FirstChild, Is.SameAs(replacement));
        Assert.That(newChild.ParentNode, Is.Null);
        Assert.That(a.IsEmpty, Is.False, "Native removal leaves a long empty tag.");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void RemovingFirstMiddleOrLastMatchesNativeCircularSiblingLinks(int removeIndex)
    {
        XmlDocument document = Parse("<Defs><a/><b/><c/></Defs>");
        XmlDocument native = Parse(document.OuterXml);
        XmlElement root = document.DocumentElement!;
        XmlElement child = (XmlElement)root.ChildNodes[removeIndex]!;
        Prepare(document, new[] { RetainedXmlPublication.Edit.Remove(root, child) }).Commit();
        native.DocumentElement!.RemoveChild(native.DocumentElement.ChildNodes[removeIndex]!);
        Assert.That(document.OuterXml, Is.EqualTo(native.OuterXml));
        Assert.That(root.ChildNodes.Cast<XmlNode>().Select(n => n.PreviousSibling?.Name),
            Is.EqualTo(native.DocumentElement.ChildNodes.Cast<XmlNode>().Select(n => n.PreviousSibling?.Name)));
        Assert.That(child.ParentNode, Is.Null);
    }

    [Test]
    public void InvalidLaterEditLeavesOriginalDocumentAndImportedNodeUntouched()
    {
        XmlDocument document = Parse("<Defs><a/><b/></Defs>");
        string original = document.OuterXml;
        XmlElement root = document.DocumentElement!;
        XmlElement x = document.CreateElement("x");
        XmlElement missing = document.CreateElement("missing");
        bool accepted = RetainedXmlPublication.TryPrepare(document, new[]
        {
            RetainedXmlPublication.Edit.Insert(root, x),
            RetainedXmlPublication.Edit.Remove(root, missing)
        }, null, out RetainedXmlPublication.Prepared? prepared, out _);
        Assert.That(accepted, Is.False);
        Assert.That(prepared, Is.Null);
        Assert.That(document.OuterXml, Is.EqualTo(original));
        Assert.That(x.ParentNode, Is.Null);
        Assert.That(x.NextSibling, Is.Null);
    }

    [Test]
    public void TextSiblingRingForeignDocumentAndAttachedInsertionRefuseBeforeMutation()
    {
        XmlDocument text = Parse("<Defs>text<a/>tail</Defs>");
        XmlElement textRoot = text.DocumentElement!;
        Assert.That(RetainedXmlPublication.TryPrepare(text, new[] { RetainedXmlPublication.Edit.Insert(textRoot, text.CreateElement("x")) }, null, out _, out _), Is.False);
        XmlDocument document = Parse("<Defs><a/><b/></Defs>");
        XmlElement root = document.DocumentElement!;
        Assert.That(RetainedXmlPublication.TryPrepare(document, new[] { RetainedXmlPublication.Edit.Insert(root, text.CreateElement("x")) }, null, out _, out _), Is.False);
        Assert.That(RetainedXmlPublication.TryPrepare(document, new[] { RetainedXmlPublication.Edit.Insert(root, (XmlElement)root.FirstChild!) }, null, out _, out _), Is.False);
        Assert.That(document.OuterXml, Is.EqualTo("<Defs><a /><b /></Defs>"));
    }

    [Test]
    public void EventObserversRefuseUnlessCallerOwnsAndInvalidatesThem()
    {
        XmlDocument document = Parse("<Defs/>");
        int events = 0;
        XmlNodeChangedEventHandler owned = (_, _) => events++;
        document.NodeInserted += owned;
        XmlElement child = document.CreateElement("x");
        var edits = new[] { RetainedXmlPublication.Edit.Insert(document.DocumentElement!, child) };
        Assert.That(RetainedXmlPublication.TryPrepare(document, edits, null, out _, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("xml-event-observer"));
        Assert.That(RetainedXmlPublication.TryPrepare(document, edits, candidate => candidate == (Delegate)owned, out RetainedXmlPublication.Prepared? prepared, out _), Is.True);
        prepared!.Commit();
        Assert.That(events, Is.Zero, "Caller explicitly owns observer invalidation; publication never fabricates callbacks.");
        Assert.That(child.ParentNode, Is.SameAs(document.DocumentElement));
        document.NodeInserted -= owned;
    }

    [Test]
    public void PreparationDoesNotWriteAndCommitIsSingleUse()
    {
        XmlDocument document = Parse("<Defs/>");
        XmlElement root = document.DocumentElement!;
        XmlElement x = document.CreateElement("x");
        RetainedXmlPublication.Prepared prepared = Prepare(document, new[] { RetainedXmlPublication.Edit.Insert(root, x) });
        Assert.That(root.IsEmpty, Is.True);
        Assert.That(x.ParentNode, Is.Null);
        prepared.Commit();
        Assert.That(root.FirstChild, Is.SameAs(x));
        Assert.That((Action)(() => prepared.Commit()), Throws.TypeOf<InvalidOperationException>());
        Assert.That(root.ChildNodes.Count, Is.EqualTo(1));
    }

    [Test]
    public void CyclesAndCustomDocumentsRefuse()
    {
        XmlDocument document = Parse("<Defs><a><b/></a></Defs>");
        XmlElement root = document.DocumentElement!;
        XmlElement a = (XmlElement)root.FirstChild!;
        XmlElement b = (XmlElement)a.FirstChild!;
        Assert.That(RetainedXmlPublication.TryPrepare(document, new[]
        {
            RetainedXmlPublication.Edit.Remove(root, a),
            RetainedXmlPublication.Edit.Insert(b, a)
        }, null, out _, out _), Is.False);
        Assert.That(a.ParentNode, Is.SameAs(root));
        var custom = new CustomDocument();
        custom.LoadXml("<Defs/>");
        Assert.That(RetainedXmlPublication.TryPrepare(custom, Array.Empty<RetainedXmlPublication.Edit>(), null, out _, out _), Is.False);
    }

    private static RetainedXmlPublication.Prepared Prepare(XmlDocument document, IReadOnlyList<RetainedXmlPublication.Edit> edits)
    {
        Assert.That(RetainedXmlPublication.TryPrepare(document, edits, null, out RetainedXmlPublication.Prepared? prepared, out string reason), Is.True, reason);
        return prepared!;
    }

    private static XmlDocument Parse(string xml) { var document = new XmlDocument(); document.LoadXml(xml); return document; }
    private sealed class CustomDocument : XmlDocument { }
}
