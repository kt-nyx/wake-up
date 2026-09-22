// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Reflection;
using System.Xml;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class RetainedXmlStagingTests
{
    private static XmlDocument Read(string xml)
    {
        var parsed = new XmlDocument(); parsed.LoadXml(xml);
        // Native combination imports parsed roots into a fresh document, which
        // has ordinary mutation validity state rather than parser-final state.
        var doc = new XmlDocument(); doc.AppendChild(doc.ImportNode(parsed.DocumentElement!, true)); return doc;
    }
    private static FieldInfo Field(string name) => typeof(XmlDocument).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? typeof(XmlDocument).GetField("_" + name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public void ExistingNamesAdmitWithoutChangingDocumentOrNameTable()
    {
        var document = Read("<Defs><stats Class='old'><mass>1</mass></stats></Defs>");
        var template = Read("<stats Class='new'><mass>2</mass></stats>").DocumentElement!;
        var original = document.OuterXml;
        var names = document.NameTable;
        var mass = names.Get("mass");
        Assert.That(RetainedXmlStaging.TryAdmit(document, new[] { template }, null, out string reason), Is.True, reason);
        Assert.That(document.OuterXml, Is.EqualTo(original));
        Assert.That(document.NameTable, Is.SameAs(names));
        Assert.That(document.NameTable.Get("mass"), Is.SameAs(mass));
        Assert.That(template.OwnerDocument.DocumentElement, Is.SameAs(template));
    }

    [Test]
    public void MissingNameRefusesWithoutInterningIt()
    {
        var document = Read("<Defs><stats/></Defs>");
        var template = Read("<stats><newName/></stats>").DocumentElement!;
        Assert.That(document.NameTable.Get("newName"), Is.Null);
        Assert.That(RetainedXmlStaging.TryAdmit(document, new[] { template }, null, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("xml-staging-missing-native-name"));
        Assert.That(document.NameTable.Get("newName"), Is.Null);
    }

    [Test]
    public void ExistingAtomsDoNotSubstituteForMissingExpandedNameTuple()
    {
        var document = Read("<Defs xmlns:p='urn:test'><stats/><p:other/></Defs>");
        var template = Read("<p:stats xmlns:p='urn:test'/>").DocumentElement!;
        Assert.That(document.NameTable.Get("stats"), Is.Not.Null);
        Assert.That(document.NameTable.Get("p"), Is.Not.Null);
        Assert.That(document.NameTable.Get("urn:test"), Is.Not.Null);
        Assert.That(RetainedXmlStaging.TryAdmit(document, new[] { template }, null, out _), Is.False);
    }

    [Test]
    public void ValidationSchemaAndCdataStateAreOutsideInitialStagingContract()
    {
        var document = Read("<Defs><stats/></Defs>");
        var template = Read("<stats/>").DocumentElement!;
        Field("reportValidity").SetValue(document, true);
        Assert.That(RetainedXmlStaging.TryAdmit(document, new[] { template }, null, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("xml-staging-validity"));
        Assert.That(Field("reportValidity").GetValue(document), Is.EqualTo(true));
        Field("reportValidity").SetValue(document, false);
        _ = document.Schemas;
        Assert.That(RetainedXmlStaging.TryAdmit(document, new[] { template }, null, out reason), Is.False);
        Assert.That(reason, Is.EqualTo("xml-staging-schema-or-id-state"));
        var clean = Read("<Defs><stats/></Defs>");
        var cdata = Read("<stats><![CDATA[value]]></stats>").DocumentElement!;
        Assert.That(RetainedXmlStaging.TryAdmit(clean, new[] { cdata }, null, out _), Is.False);
    }

    [Test]
    public void ForeignObserversRefuseBeforeAnyStaging()
    {
        var document = Read("<Defs><stats/></Defs>");
        var template = Read("<stats/>").DocumentElement!;
        XmlNodeChangedEventHandler observer = (_, _) => throw new InvalidOperationException("must not execute");
        document.NodeInserted += observer;
        Assert.That(RetainedXmlStaging.TryAdmit(document, new[] { template }, null, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("xml-event-observer"));
        document.NodeInserted -= observer;
    }

    [Test]
    public void RelevantNativeHelpersArePresentOnTestHost()
    {
        Assert.That(RetainedXmlStaging.RelevantMethods(), Is.All.Not.Null);
    }
}
