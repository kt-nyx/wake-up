// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class XmlRegionReplayTests
{
    private string directory = null!;
    private bool profiler;
    private const string Input = "<Defs><ThingDef><defName>A</defName><stats><mass>1</mass></stats><keep/></ThingDef></Defs>";
    [SetUp]
    public void Setup()
    {
        directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "region-replay-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        profiler = DeepProfiler.enabled; DeepProfiler.enabled = false;
    }
    [TearDown]
    public void Cleanup() { DeepProfiler.enabled = profiler; Directory.Delete(directory, true); }
    private OwnedCacheStore Store() => new(directory, XmlRegionOperations.MaxBytes, XmlRegionReplay.StoreBytes, CacheLaunchPolicy.Current);
    private static XmlDocument Read(string text)
    {
        var parsed = new XmlDocument(); parsed.LoadXml(text);
        var doc = new XmlDocument(); doc.AppendChild(doc.ImportNode(parsed.DocumentElement!, true));
        // Model names already encountered during earlier ordinary parsing. The
        // cache itself may not introduce these names during failed staging.
        if (doc.DocumentElement!.Name == "Defs")
        {
            foreach (string name in new[] { "armor", "modExtensions", "li", "value" }) doc.CreateElement(name);
            doc.CreateAttribute("Class");
        }
        return doc;
    }
    private static PatchOperation Op(Type type, string tail, string? value = null)
    {
        var op = (PatchOperation)Activator.CreateInstance(type)!;
        typeof(PatchOperationPathed).GetField("xpath", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(op, "Defs/ThingDef[defName='A']" + tail);
        if (value != null) type.GetField("value", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(op, new XmlContainer { node = Read("<value>" + value + "</value>").DocumentElement });
        op.sourceFile = "ordinary/source.xml"; return op;
    }
    private static PatchOperation[] Operations() => new[] {
        Op(typeof(PatchOperationReplace), "/stats/mass", "<mass>2</mass>"),
        Op(typeof(PatchOperationAdd), "/stats", "<armor>3</armor>"),
        Op(typeof(PatchOperationRemove), "/stats"),
        Op(typeof(PatchOperationAddModExtension), "", "<li Class='Example.Extension'><value>4</value></li>"),
        Op(typeof(PatchOperationRemove), "/absent")
    };
    private static bool Native(PatchOperation op, XmlDocument doc) => op.Apply(doc);

    [Test]
    public void WarmNativeCapturedEditsRetainRemovedAliasesAndOperationOutcomes()
    {
        using var store = Store();
        var cold = Read(Input); var ordinary = Read(Input); var warm = Read(Input);
        var root = warm.DocumentElement!.FirstChild!; var stats = root["stats"]!; var mass = stats["mass"]!; var text = mass.FirstChild!;
        var originalOps = Operations(); bool[] outcomes = originalOps.Select(op => op.Apply(ordinary)).ToArray();
        Assert.That(XmlRegionReplay.Execute(cold, Operations(), store, Native).Captured, Is.True);
        int calls = 0;
        var warmOps = Operations();
        var result = XmlRegionReplay.Execute(warm, warmOps, store, (op, doc) => { calls++; return op.Apply(doc); });
        Assert.That(result.Hit, Is.True, result.Reason);
        Assert.That(calls, Is.Zero);
        Assert.That(result.Outcomes, Is.EqualTo(outcomes));
        Assert.That(warm.OuterXml, Is.EqualTo(ordinary.OuterXml));
        Assert.That(warm.DocumentElement.FirstChild, Is.SameAs(root));
        Assert.That(stats.ParentNode, Is.Null);
        Assert.That(stats.OuterXml, Is.EqualTo("<stats><mass>2</mass><armor>3</armor></stats>"));
        Assert.That(mass.ParentNode, Is.Null);
        Assert.That(mass.FirstChild, Is.SameAs(text));
        Assert.That(text.Value, Is.EqualTo("1"));
        var field = typeof(PatchOperation).GetField("neverSucceeded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.That(warmOps.Select(op => (bool)field.GetValue(op)), Is.EqualTo(originalOps.Select(op => (bool)field.GetValue(op))));
    }

    [TestCase("content")]
    [TestCase("source")]
    [TestCase("missing")]
    [TestCase("corrupt")]
    [TestCase("observer")]
    public void RefusalAndMissExecuteNativeOnUnchangedReachableInput(string change)
    {
        using var store = Store();
        Assert.That(XmlRegionReplay.Execute(Read(Input), Operations(), store, Native).Captured, Is.True);
        var doc = Read(change == "content" ? Input.Replace("<keep/>", "<keep>different</keep>") : Input);
        var ops = Operations();
        if (change == "source") ops[0].sourceFile = "changed.xml";
        string file = Directory.GetFiles(directory, "*.bin").Single();
        if (change == "missing") File.Delete(file);
        if (change == "corrupt") File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
        int observed = 0;
        if (change == "observer") doc.NodeInserted += (_, _) => observed++;
        var ordinary = (XmlDocument)doc.CloneNode(true); foreach (var op in Operations()) op.Apply(ordinary);
        int calls = 0;
        var result = XmlRegionReplay.Execute(doc, ops, store, (op, xml) => { calls++; return op.Apply(xml); });
        Assert.That(result.Hit, Is.False);
        Assert.That(calls, Is.EqualTo(ops.Length));
        Assert.That(doc.OuterXml, Is.EqualTo(ordinary.OuterXml));
        if (change == "observer") Assert.That(observed, Is.GreaterThan(0));
    }

    [Test]
    public void NativePartialFailurePropagatesWithoutRetryOrStoredEntry()
    {
        using var store = Store();
        var doc = Read(Input); int calls = 0;
        Assert.That((Action)(() => XmlRegionReplay.Execute(doc, Operations(), store, (op, xml) =>
        { calls++; op.Apply(xml); throw new InvalidOperationException("native-test-failure"); })), Throws.TypeOf<InvalidOperationException>());
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(doc.SelectSingleNode("/Defs/ThingDef/stats/mass")!.InnerText, Is.EqualTo("2"));
        Assert.That(Directory.GetFiles(directory, "*.bin"), Is.Empty);
    }

    [Test]
    public void StoredResultRemainsBoundedAcrossRepeatedFreshDocuments()
    {
        using var store = Store();
        Assert.That(XmlRegionReplay.Execute(Read(Input), Operations(), store, Native).Captured, Is.True);
        for (int i = 0; i < 3; i++) Assert.That(XmlRegionReplay.Execute(Read(Input), Operations(), store, Native).Hit, Is.True);
        store.Complete();
        Assert.That(Directory.GetFiles(directory, "*.bin").Length, Is.EqualTo(1));
        Assert.That(store.StoredBytes, Is.LessThan(XmlRegionReplay.StoreBytes));
    }

    [Test]
    public void ForeignColdObserverCannotPoisonLaterAdmissibleInput()
    {
        using var store = Store();
        var cold = Read(Input);
        cold.NodeInserted += (_, _) =>
        {
            var keep = cold.SelectSingleNode("/Defs/ThingDef/keep");
            if (keep != null) keep.ParentNode!.RemoveChild(keep);
        };
        Assert.That(XmlRegionReplay.Execute(cold, Operations(), store, Native).Captured, Is.False);
        Assert.That(Directory.GetFiles(directory, "*.bin"), Is.Empty);
        var warm = Read(Input);
        var result = XmlRegionReplay.Execute(warm, Operations(), store, Native);
        Assert.That(result.Hit, Is.False);
        Assert.That(warm.SelectSingleNode("/Defs/ThingDef/keep"), Is.Not.Null);
    }

    [Test]
    public void StructurallyInvalidJournalIsRemovedAndRebuiltOnOrdinaryFallback()
    {
        using var store = Store();
        Assert.That(XmlRegionReplay.Execute(Read(Input), Operations(), store, Native).Captured, Is.True);
        string key = Path.GetFileNameWithoutExtension(Directory.GetFiles(directory, "*.bin").Single());
        Assert.That(store.Publish(key, new byte[16]), Is.True);
        var result = XmlRegionReplay.Execute(Read(Input), Operations(), store, Native);
        Assert.That(result.Hit, Is.False);
        Assert.That(result.Captured, Is.True);
        Assert.That(XmlRegionReplay.Execute(Read(Input), Operations(), store, Native).Hit, Is.True);
    }

    [Test]
    public void InvalidTopologyAfterStagingDoesNotPoisonReplacementNodeIds()
    {
        using var store = Store();
        Assert.That(XmlRegionReplay.Execute(Read(Input), Operations(), store, Native).Captured, Is.True);
        string key = Path.GetFileNameWithoutExtension(Directory.GetFiles(directory, "*.bin").Single());
        byte[] payload = store.Read(key)!;
        // Valid envelope and IDs; insertion parent is the original text-bearing
        // mass element, so final topology preparation refuses after staging.
        Array.Copy(BitConverter.GetBytes(3), 0, payload, 12, 4);
        Assert.That(store.Publish(key, payload), Is.True);
        var doc = Read(Input); var originalName = doc.NameTable.Get("mass");
        var result = XmlRegionReplay.Execute(doc, Operations(), store, Native);
        Assert.That(result.Hit, Is.False);
        Assert.That(result.Captured, Is.True);
        Assert.That(doc.NameTable.Get("mass"), Is.SameAs(originalName));
        var next = XmlRegionReplay.Execute(Read(Input), Operations(), store, Native);
        Assert.That(next.Hit, Is.True, next.Reason);
    }
}
