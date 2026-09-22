// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ParsedXmlRuntimeTests
{
    private const string ObserverOwner = "wakeup.tests.parsed-xml-observer";
    private const string CounterOwner = "wakeup.tests.parsed-xml-counter";
    private readonly Dictionary<string, object?> previous = new();
    private KeyValuePair<LoadableXmlAsset, XmlNode[]>[] oldPrepared = Array.Empty<KeyValuePair<LoadableXmlAsset, XmlNode[]>>();
    private LoadableXmlAsset[] oldRestored = Array.Empty<LoadableXmlAsset>();
    private static int nativeCalls;
    private static FieldInfo Field(string name) => typeof(ParsedXmlRuntime).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
    private static void Set(string name, object? value) => Field(name).SetValue(null, value);
    private static Dictionary<LoadableXmlAsset, XmlNode[]> Prepared
        => (Dictionary<LoadableXmlAsset, XmlNode[]>)Field("prepared").GetValue(null)!;
    private static HashSet<LoadableXmlAsset> Restored
        => (HashSet<LoadableXmlAsset>)Field("restoredAssets").GetValue(null)!;

    [SetUp]
    public void SaveRuntimeState()
    {
        foreach (string name in new[] { "enabled", "fusionValid", "destination", "guards", "log", "importedRoots", "fusedRoots" })
            previous[name] = Field(name).GetValue(null);
        oldPrepared = Prepared.ToArray(); oldRestored = Restored.ToArray();
        Prepared.Clear(); Restored.Clear();
        Set("log", null); Set("importedRoots", 0); Set("fusedRoots", 0);
        nativeCalls = 0;
    }

    [TearDown]
    public void RestoreRuntimeState()
    {
        new Harmony(ObserverOwner).UnpatchAll(ObserverOwner);
        new Harmony(CounterOwner).UnpatchAll(CounterOwner);
        Prepared.Clear(); foreach (var pair in oldPrepared) Prepared.Add(pair.Key, pair.Value);
        Restored.Clear(); foreach (var asset in oldRestored) Restored.Add(asset);
        foreach (var pair in previous) Set(pair.Key, pair.Value);
        previous.Clear(); nativeCalls = 0;
    }

    private static XmlDocument Document(string xml)
    {
        var result = new XmlDocument(); result.LoadXml(xml); return result;
    }

    private static LoadableXmlAsset Asset(XmlDocument source, ModContentPack mod, string folder)
    {
        // Test-only object identity avoids executing the native file constructor,
        // whose Span-based body is unavailable on the net472 semantic host.
        var asset = (LoadableXmlAsset)FormatterServices.GetUninitializedObject(typeof(LoadableXmlAsset));
        foreach (var pair in new[] { ("xmlDoc", (object)source), ("mod", (object)mod), ("name", (object)"same.xml"), ("fullFolderPath", (object)folder) })
            typeof(LoadableXmlAsset).GetField(pair.Item1, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(asset, pair.Item2);
        return asset;
    }

    private static void UnknownSourceObserver() { }
    private static void CountNativeCombination() { nativeCalls++; }

    private static PublishedPatchGuard[] Guards()
    {
        var source = AccessTools.Method(typeof(ModContentPack), "LoadDefs");
        var combine = AccessTools.Method(typeof(LoadedModManager), "CombineIntoUnifiedXML");
        var counter = AccessTools.Method(typeof(ParsedXmlRuntimeTests), nameof(CountNativeCombination));
        Assert.That(PublishedPatchGuard.TryCreate(source, ParsedXmlRuntime.Owner, out var sourceGuard, true), Is.True);
        Assert.That(PublishedPatchGuard.TryCreate(combine, ParsedXmlRuntime.Owner, out var combineGuard, true,
            patch => patch.PatchMethod == counter), Is.True);
        return new[] { sourceGuard!, combineGuard! };
    }

    private static void InstallObserver()
        => new Harmony(ObserverOwner).Patch(AccessTools.Method(typeof(ModContentPack), "LoadDefs"),
            prefix: new HarmonyMethod(typeof(ParsedXmlRuntimeTests), nameof(UnknownSourceObserver)));

    [TestCase(false)]
    [TestCase(true)]
    public void UnknownOrLateObserverUsesCurrentRetainedSourcesAndCallsNativeCombinationOnce(bool installedLate)
    {
        new Harmony(CounterOwner).Patch(AccessTools.Method(typeof(LoadedModManager), "CombineIntoUnifiedXML"),
            prefix: new HarmonyMethod(typeof(ParsedXmlRuntimeTests), nameof(CountNativeCombination)));
        if (!installedLate) InstallObserver();
        var guards = Guards();
        Assert.That(guards.All(g => g.AllowsOriginalContract()), Is.EqualTo(installedLate));
        var mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        var source = Document("<Defs><ThingDef><defName>Before</defName></ThingDef></Defs>");
        var otherSource = Document("<Defs><ThingDef><defName>Second</defName></ThingDef></Defs>");
        var asset = Asset(source, mod, "first-folder");
        var otherAsset = Asset(otherSource, mod, "second-folder");
        var staleDestination = Document("<Defs/>");
        Prepared.Add(asset, new[] { staleDestination.ImportNode(source.DocumentElement!.FirstChild!, true) });
        Prepared.Add(otherAsset, new[] { staleDestination.ImportNode(otherSource.DocumentElement!.FirstChild!, true) });
        Restored.Add(asset); Restored.Add(otherAsset);
        Set("enabled", true); Set("fusionValid", true); Set("destination", staleDestination); Set("guards", guards);

        // A constructor-installed observer may retain and later change this
        // exact source node after preparation but before native combination.
        XmlNode retained = source.DocumentElement.FirstChild!;
        retained.FirstChild!.InnerText = "ObserverMutation";
        if (installedLate) InstallObserver();
        var lookup = new Dictionary<XmlNode, LoadableXmlAsset>();
        var originalLookup = lookup;
        XmlDocument combined = ParsedXmlRuntime.Combine(new List<LoadableXmlAsset> { asset, otherAsset }, ref lookup);

        Assert.That(nativeCalls, Is.EqualTo(1));
        Assert.That(combined, Is.Not.SameAs(staleDestination));
        Assert.That(lookup, Is.SameAs(originalLookup), "Native fallback retains its caller-provided map.");
        Assert.That(combined.DocumentElement!.ChildNodes.Count, Is.EqualTo(2));
        var first = combined.DocumentElement.FirstChild!;
        var second = combined.DocumentElement.LastChild!;
        Assert.That(first.FirstChild!.InnerText, Is.EqualTo("ObserverMutation"));
        Assert.That(second.FirstChild!.InnerText, Is.EqualTo("Second"));
        Assert.That(lookup[first], Is.SameAs(asset));
        Assert.That(lookup[second], Is.SameAs(otherAsset));
        Assert.That(asset.name, Is.EqualTo(otherAsset.name));
        Assert.That(asset.fullFolderPath, Is.Not.EqualTo(otherAsset.fullFolderPath));
        Assert.That(asset.mod, Is.SameAs(mod));
        Assert.That(first, Is.Not.SameAs(retained));
        Assert.That(first.OwnerDocument, Is.SameAs(combined));
        first.FirstChild.InnerText = "CombinedMutation";
        Assert.That(retained.FirstChild!.InnerText, Is.EqualTo("ObserverMutation"));
        Assert.That(retained.OwnerDocument, Is.SameAs(source));
        Assert.That(asset.xmlDoc, Is.SameAs(source));
        Assert.That(Prepared, Is.Empty);
        Assert.That(Field("destination").GetValue(null), Is.Null);
    }

    [Test]
    public void SelectedInputKeyBindsBytesOrderChunkPositionAndSelection()
    {
        byte[] first = Encoding.UTF8.GetBytes("<A>one</A>"), changed = Encoding.UTF8.GetBytes("<A>two</A>"), second = Encoding.UTF8.GetBytes("<B/>");
        Assert.That(changed.Length, Is.EqualTo(first.Length));
        string key = ParsedXmlRuntime.Key("ordered-source-selection", 0, new[] { first, second });
        Assert.That(ParsedXmlRuntime.Key("ordered-source-selection", 0, new[] { (byte[])first.Clone(), (byte[])second.Clone() }), Is.EqualTo(key));
        foreach (string different in new[] {
            ParsedXmlRuntime.Key("ordered-source-selection", 0, new[] { changed, second }),
            ParsedXmlRuntime.Key("ordered-source-selection", 0, new[] { second, first }),
            ParsedXmlRuntime.Key("ordered-source-selection", 1, new[] { first, second }),
            ParsedXmlRuntime.Key("changed-provider-or-folder-order", 0, new[] { first, second }),
            ParsedXmlRuntime.Key("ordered-source-selection", 0, new[] { first }),
            ParsedXmlRuntime.Key("ordered-source-selection", 0, new[] { first, second, first }) })
            Assert.That(different, Is.Not.EqualTo(key));
    }

    [Test]
    public void BatchRoundTripPreservesMixedNativeSlotsAndPrivateRecordBytes()
    {
        byte[] first = { 1, 2, 3 }, last = { 4, 5 };
        byte[] payload = ParsedXmlRuntime.EncodeBatch(new byte[]?[] { first, null, last });
        var result = ParsedXmlRuntime.DecodeBatch(payload, 3);
        Assert.That(result[0], Is.EqualTo(first)); Assert.That(result[1], Is.Null); Assert.That(result[2], Is.EqualTo(last));
        result[0]![0] = 99;
        Assert.That(first[0], Is.EqualTo(1));
        Assert.That(ParsedXmlRuntime.DecodeBatch(payload, 3)[0]![0], Is.EqualTo(1));
    }

    [TestCase("schema")]
    [TestCase("count")]
    [TestCase("negative-size")]
    [TestCase("oversized-record")]
    [TestCase("truncated")]
    [TestCase("trailing")]
    public void CorruptBatchFramingRefuses(string corruption)
    {
        byte[] payload = ParsedXmlRuntime.EncodeBatch(new byte[]?[] { new byte[] { 1, 2, 3 }, null });
        void Write(int offset, int value) => Array.Copy(BitConverter.GetBytes(value), 0, payload, offset, 4);
        switch (corruption)
        {
            case "schema": Write(0, 2); break;
            case "count": Write(4, 1); break;
            case "negative-size": Write(8, -1); break;
            case "oversized-record": Write(8, payload.Length); break;
            case "truncated": payload = payload.Take(13).ToArray(); break;
            case "trailing": payload = payload.Concat(new byte[] { 0 }).ToArray(); break;
        }
        Assert.That((Action)(() => { ParsedXmlRuntime.DecodeBatch(payload, 2); }), Throws.TypeOf<InvalidDataException>());
    }

    private static XmlReaderSettings Settings() => new() { IgnoreComments = true, IgnoreWhitespace = true, CheckCharacters = false, Async = false };

    [TestCase("comments")]
    [TestCase("whitespace")]
    [TestCase("characters")]
    [TestCase("async")]
    [TestCase("close-input")]
    [TestCase("processing-instructions")]
    [TestCase("name-table")]
    [TestCase("dtd")]
    [TestCase("validation")]
    [TestCase("conformance")]
    [TestCase("document-limit")]
    [TestCase("entity-limit")]
    [TestCase("schemas")]
    public void ParserAdmissionRequiresTheActualNativeSettings(string change)
    {
        var settings = Settings();
        Assert.That(ParsedXmlRuntime.NativeSettings(settings), Is.True);
        switch (change)
        {
            case "comments": settings.IgnoreComments = false; break;
            case "whitespace": settings.IgnoreWhitespace = false; break;
            case "characters": settings.CheckCharacters = true; break;
            case "async": settings.Async = true; break;
            case "close-input": settings.CloseInput = true; break;
            case "processing-instructions": settings.IgnoreProcessingInstructions = true; break;
            case "name-table": settings.NameTable = new NameTable(); break;
            case "dtd": settings.DtdProcessing = DtdProcessing.Parse; break;
            case "validation": settings.ValidationType = ValidationType.Schema; break;
            case "conformance": settings.ConformanceLevel = ConformanceLevel.Fragment; break;
            case "document-limit": settings.MaxCharactersInDocument = 1234; break;
            case "entity-limit": settings.MaxCharactersFromEntities = 1234; break;
            case "schemas": settings.Schemas.Add(new XmlSchema()); break;
        }
        Assert.That(ParsedXmlRuntime.NativeSettings(settings), Is.False);
    }

    [Test]
    public void OrdinarySettingsKeepParsedReuseOffUntilSelectedAndHonorMasterOff()
    {
        string profile = Path.Combine(Path.GetTempPath(), "wakeup-parsed-settings-only");
        var settings = new WakeUpSettings();
        Assert.That(settings.ParsedXml, Is.False);
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile), Does.Not.Contain("--wake-up-parsed-xml=on"));
        settings.ParsedXml = true; settings.DefinitionSearches = false; settings.TypeSearches = false;
        var selected = UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile);
        Assert.That(selected.Count(a => a == "--wake-up-parsed-xml=on"), Is.EqualTo(1));
        Assert.That(selected, Does.Contain("--wake-up-user-def-search=off"));
        Assert.That(selected, Does.Not.Contain("--wake-up-streaming-xml=on"));
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile), Is.Empty);
    }

    [TestCase("--wake-up-parsed-xml=off")]
    [TestCase("--wake-up-parsed-xml=invalid")]
    [TestCase("--wake-up-bypass")]
    [TestCase("--wake-up-mode=baseline")]
    public void ExplicitLaunchSelectionCannotBeOverriddenByTheSavedParsedSetting(string control)
    {
        string profile = Path.Combine(Path.GetTempPath(), "wakeup-parsed-settings-only");
        string[] input = { control, "-savedatafolder=" + profile };
        Assert.That(UserStartupSelection.Resolve(input, new WakeUpSettings { ParsedXml = true }, profile), Is.EqualTo(input));
    }
}
