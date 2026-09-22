// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class XmlReuseRuntimeTests
{
    private string root = null!;
    private bool profiling;
    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(TestContext.CurrentContext.TestDirectory, "xml-reuse-runtime-" + Guid.NewGuid().ToString("N"));
        profiling = DeepProfiler.enabled; DeepProfiler.enabled = false;
    }
    [TearDown]
    public void TearDown()
    {
        DeepProfiler.enabled = profiling;
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private static CacheLaunchPolicy Policy(string request = "normal")
    { CacheLaunchPolicy? latch = null; return CacheLaunchPolicy.Latch(ref latch, request, () => { }); }
    private string[] Entries() => Directory.Exists(Path.Combine(root, "WakeUp", "XmlPrefix", "v1"))
        ? Directory.GetFiles(Path.Combine(root, "WakeUp", "XmlPrefix", "v1"), "*.bin") : Array.Empty<string>();
    private static T Op<T>(params (string Name, object? Value)[] values) where T : PatchOperation, new()
    {
        var operation = new T { sourceFile = "Patches/Example.xml" };
        foreach (var field in values) AccessTools.Field(operation.GetType(), field.Name).SetValue(operation, field.Value);
        return operation;
    }
    private static XmlContainer Value(string xml)
    { var document = new XmlDocument(); document.LoadXml(xml); return new XmlContainer { node = document.DocumentElement! }; }
    private static PatchOperationAdd Add(string element = "Added") => Op<PatchOperationAdd>(("xpath", "/Defs"), ("value", Value("<value><" + element + " /></value>")));
    private static PatchOperationTest Test(string xpath = "/Defs/A") => Op<PatchOperationTest>(("xpath", xpath));
    private static PatchOperationSequence Sequence(params PatchOperation[] operations) => Op<PatchOperationSequence>(("operations", operations.ToList()));
    private static bool Never(PatchOperation operation) => (bool)AccessTools.Field(typeof(PatchOperation), "neverSucceeded").GetValue(operation);

    private sealed class Observer : PatchOperation
    {
        internal int Calls, Completions;
        internal string Seen = "";
        internal bool Throw;
        protected override bool ApplyWorker(XmlDocument xml)
        {
            Calls++; Seen = xml.OuterXml;
            xml.DocumentElement!.AppendChild(xml.CreateElement("SuffixEffect"));
            if (Throw) throw new InvalidOperationException("offline-custom-failure");
            return true;
        }
        public override void Complete(string modIdentifier) { Completions++; base.Complete(modIdentifier); }
    }
    private sealed class Work
    {
        internal XmlDocument Document;
        internal Dictionary<XmlNode, LoadableXmlAsset> Sources = new();
        internal List<LoadableXmlAsset> Inputs;
        internal PatchOperation[] Operations;
        internal readonly Observer Suffix;
        internal int OriginalCalls, Attempts, Errors;
        internal string Status = "";
        internal string Context = "effective-environment-A";
        internal Work(string definitions = "<Defs><A /><Remove /><Replace /></Defs>", string sourceName = "Definitions.xml", params PatchOperation[] prefix)
        {
            Inputs = new List<LoadableXmlAsset> { new(sourceName, definitions) };
            // Exercise RimWorld's real combination/source-map construction too.
            Document = LoadedModManager.CombineIntoUnifiedXML(Inputs, Sources);
            Suffix = new Observer { sourceFile = "Patches/Unknown.xml" };
            Operations = prefix.Concat(new PatchOperation[] { Suffix }).ToArray();
        }
    }
    private void Run(Work work, CacheLaunchPolicy? policy = null, Func<bool>? admission = null)
    {
        XmlReuseRuntime.Execute(ref work.Document, ref work.Sources, work.Inputs, work.Operations,
            work.Context, root, policy ?? Policy(), admission ?? (() => true), (document, sources) =>
            {
                work.OriginalCalls++;
                // The reviewed native loop calls each top-level operation once
                // and catches each exception independently. Only logging is
                // replaced by a counter in this offline host.
                foreach (PatchOperation operation in work.Operations)
                {
                    work.Attempts++;
                    try { XmlReuseRuntime.ApplyOne(operation, document); }
                    catch (Exception) { work.Errors++; }
                }
            });
        work.Status = XmlReuseRuntime.Status;
    }
    private static Work Basic() => new(prefix: new PatchOperation[] { Add() });
    private static void Once(Work work)
    {
        Assert.That(work.OriginalCalls, Is.EqualTo(1));
        Assert.That(work.Attempts, Is.EqualTo(work.Operations.Length));
        Assert.That(work.Suffix.Calls, Is.EqualTo(1));
        Assert.That(work.Document.SelectNodes("/Defs/SuffixEffect")!.Count, Is.EqualTo(1));
    }

    [Test]
    public void ColdCaptureAndWarmHitMatchNativeXmlAndRunUnknownSuffixOnceWithNormalCompletion()
    {
        var cold = Basic(); Run(cold); Once(cold);
        Assert.That(cold.Status, Does.Contain("captured yes"));
        Assert.That(Entries(), Has.Length.EqualTo(1));
        var warm = Basic(); XmlDocument before = warm.Document; Run(warm); Once(warm);
        Assert.That(warm.Status, Does.Contain("validated hit").And.Contain("Skipped 1"));
        Assert.That(warm.Document, Is.Not.SameAs(before));
        Assert.That(warm.Document.OuterXml, Is.EqualTo(cold.Document.OuterXml));
        Assert.That(warm.Suffix.Seen, Is.EqualTo(cold.Suffix.Seen));
        Assert.That(warm.Document.SelectNodes("/Defs/Added")!.Count, Is.EqualTo(1));
        Assert.That(warm.Operations.All(op => !Never(op)), Is.True);
        foreach (PatchOperation operation in warm.Operations) operation.Complete("offline-test");
        Assert.That(warm.Suffix.Completions, Is.EqualTo(1));
    }

    [Test]
    public void RestoredSourceMapRetainsMappedDetachedRootsAndUnmappedNewRootsOnOneCurrentOwnerDocument()
    {
        Work Build() => new(prefix: new PatchOperation[] { Sequence(
            Op<PatchOperationAttributeSet>(("xpath", "/Defs/A"), ("attribute", "changed"), ("value", "yes")),
            Op<PatchOperationRemove>(("xpath", "/Defs/Remove")),
            Op<PatchOperationReplace>(("xpath", "/Defs/Replace"), ("value", Value("<value><New /></value>"))), Add()) });
        var cold = Build(); Run(cold);
        var warm = Build(); Run(warm);
        Assert.That(warm.Status, Does.Contain("validated hit"));
        Assert.That(warm.Document.OuterXml, Is.EqualTo(cold.Document.OuterXml));
        XmlNode mapped = warm.Document.SelectSingleNode("/Defs/A")!;
        Assert.That(warm.Sources[mapped], Is.SameAs(warm.Inputs[0]));
        Assert.That(mapped.Attributes!["changed"]!.Value, Is.EqualTo("yes"));
        Assert.That(warm.Sources.Keys.Select(node => node.Name), Is.EquivalentTo(new[] { "A", "Remove", "Replace" }));
        foreach (var source in warm.Sources)
        {
            Assert.That(source.Key.OwnerDocument, Is.SameAs(warm.Document));
            Assert.That(source.Value, Is.SameAs(warm.Inputs[0]));
            if (source.Key.Name != "A") Assert.That(source.Key.ParentNode, Is.Null);
        }
        Assert.That(warm.Sources.ContainsKey(warm.Document.SelectSingleNode("/Defs/New")!), Is.False);
        Assert.That(warm.Sources.ContainsKey(warm.Document.SelectSingleNode("/Defs/Added")!), Is.False);
        Assert.That(warm.Inputs[0], Is.Not.SameAs(cold.Inputs[0]), "Hits rebind to this launch's source assets.");
    }

    [TestCase("definitions")]
    [TestCase("patch")]
    [TestCase("order")]
    [TestCase("context")]
    [TestCase("source-name")]
    [TestCase("patch-source")]
    public void ChangedEffectiveInputsCauseAnIntegratedMissAndCapture(string change)
    {
        Work Build(string definitions = "<Defs><A /></Defs>", string name = "Definitions.xml", bool reverse = false, string element = "Added")
        {
            PatchOperation[] operations = { Add(element), Test() };
            if (reverse) Array.Reverse(operations);
            return new Work(definitions, name, operations);
        }
        var cold = Build(); Run(cold);
        var changed = Build(change == "definitions" ? "<Defs><A changed='yes' /></Defs>" : "<Defs><A /></Defs>",
            change == "source-name" ? "Renamed.xml" : "Definitions.xml", change == "order", change == "patch" ? "Different" : "Added");
        if (change == "context") changed.Context = "effective-environment-B";
        if (change == "patch-source") changed.Operations[0].sourceFile = "Patches/Renamed.xml";
        Run(changed); Once(changed);
        Assert.That(changed.Status, Does.Contain("miss").And.Contain("captured yes").And.Contain("Skipped 0"));
        Assert.That(Entries(), Has.Length.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RefusedAdmissionIncludingChangedContractBeforeCommitKeepsOriginalInputsAndExecutesOnce(bool late)
    {
        var cold = Basic(); Run(cold);
        var refused = Basic(); XmlDocument originalDocument = refused.Document;
        Dictionary<XmlNode, LoadableXmlAsset> originalSources = refused.Sources;
        int decisions = 0;
        Run(refused, admission: () => late && decisions++ == 0);
        Once(refused);
        Assert.That(refused.Status, Does.Contain("refused"));
        Assert.That(refused.Document, Is.SameAs(originalDocument));
        Assert.That(refused.Sources, Is.SameAs(originalSources));
        Assert.That(refused.Document.OuterXml, Is.EqualTo(cold.Document.OuterXml));
        Assert.That(refused.Errors, Is.Zero);
    }

    [Test]
    public void CorruptEntryRunsOrdinaryPatchesOnceAndRebuildsForTheFollowingHit()
    {
        var cold = Basic(); Run(cold);
        string file = Entries().Single(); byte[] bytes = File.ReadAllBytes(file); bytes[bytes.Length - 1] ^= 1;
        File.WriteAllBytes(file, bytes);
        var fallback = Basic(); Run(fallback); Once(fallback);
        Assert.That(fallback.Status, Does.Contain("miss").And.Contain("Skipped 0").And.Contain("captured yes"));
        Assert.That(fallback.Document.OuterXml, Is.EqualTo(cold.Document.OuterXml));
        var following = Basic(); Run(following);
        Assert.That(following.Status, Does.Contain("validated hit"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FailedOrThrowingNativePrefixKeepsPartialChangesAndSuffixWithoutPublishing(bool throws)
    {
        Work Build() => new(prefix: new PatchOperation[] { Sequence(Add(), Test(throws ? "[" : "/Defs/Missing")) });
        var first = Build(); Run(first); Once(first);
        Assert.That(first.Errors, Is.EqualTo(throws ? 1 : 0));
        Assert.That(first.Document.SelectNodes("/Defs/Added")!.Count, Is.EqualTo(1));
        Assert.That(first.Suffix.Seen, Does.Contain("Added"));
        Assert.That(first.Status, Does.Contain("captured no"));
        Assert.That(Entries(), Is.Empty);
        var second = Build(); Run(second); Once(second);
        Assert.That(second.Status, Does.Contain("miss").And.Contain("Skipped 0"));
        Assert.That(second.Document.OuterXml, Is.EqualTo(first.Document.OuterXml));
    }

    [TestCase("bypass")]
    [TestCase("clear")]
    [TestCase("rebuild")]
    public void OneShotPolicyIsSharedAcrossConsumersAndNormalReuseReturnsNextLaunch(string action)
    {
        var cold = Basic(); Run(cold);
        byte[] previous = File.ReadAllBytes(Entries().Single());
        string saved = action; int consumed = 0; CacheLaunchPolicy? latch = null;
        var selected = CacheLaunchPolicy.Latch(ref latch, saved, () => { saved = "normal"; consumed++; });
        var sameLaunch = CacheLaunchPolicy.Latch(ref latch, saved, () => consumed++);
        Assert.That(sameLaunch, Is.SameAs(selected));
        foreach (var policy in new[] { selected, sameLaunch })
        {
            var work = Basic(); Run(work, policy); Once(work);
            Assert.That(work.Status, Does.Contain("Skipped 0"));
            Assert.That(work.Status, Does.Contain(action == "rebuild" ? "captured yes" : "captured no"));
        }
        Assert.That(consumed, Is.EqualTo(1));
        if (action == "clear") Assert.That(Entries(), Is.Empty);
        if (action == "bypass") Assert.That(File.ReadAllBytes(Entries().Single()), Is.EqualTo(previous));
        var normal = Basic(); Run(normal, Policy(saved));
        Assert.That(normal.Status, Does.Contain(action == "clear" ? "miss" : "validated hit"));
        var following = Basic(); Run(following, Policy(saved));
        Assert.That(following.Status, Does.Contain("validated hit"));
    }

    [Test]
    public void UnknownSuffixFailureKeepsTheCommittedHitAndNeverRestartsPatching()
    {
        var cold = Basic(); cold.Suffix.Throw = true; Run(cold); Once(cold);
        Assert.That(cold.Errors, Is.EqualTo(1));
        Assert.That(cold.Status, Does.Contain("captured yes"));
        var warm = Basic(); warm.Suffix.Throw = true; Run(warm); Once(warm);
        Assert.That(warm.Status, Does.Contain("validated hit").And.Contain("Skipped 1"));
        Assert.That(warm.Errors, Is.EqualTo(1));
        Assert.That(warm.Document.SelectNodes("/Defs/Added")!.Count, Is.EqualTo(1));
        Assert.That(warm.Document.OuterXml, Is.EqualTo(cold.Document.OuterXml));
    }
}
