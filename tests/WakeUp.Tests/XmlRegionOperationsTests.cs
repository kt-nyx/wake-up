// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class XmlRegionOperationsTests
{
    private const string A = "Defs/ThingDef[defName='A']";
    private const string B = "Defs/ThingDef[defName='B']";
    private bool profiling;
    [SetUp] public void SetUp() { profiling = DeepProfiler.enabled; DeepProfiler.enabled = false; }
    [TearDown] public void TearDown() { DeepProfiler.enabled = profiling; }
    private static XmlDocument Document(string xml)
    { var document = new XmlDocument { PreserveWhitespace = true }; document.LoadXml(xml); return document; }
    private static T Op<T>(params (string Name, object? Value)[] fields) where T : PatchOperation, new()
    {
        var operation = new T { sourceFile = "Patches/Region.xml" };
        foreach (var field in fields) Set(operation, field.Name, field.Value);
        return operation;
    }
    private static void Set(object target, string name, object? value)
    {
        FieldInfo field = AccessTools.Field(target.GetType(), name);
        field.SetValue(target, field.FieldType.IsEnum && value is string text ? Enum.Parse(field.FieldType, text) : value);
    }
    private static XmlContainer Value(string xml) => new() { node = Document(xml).DocumentElement! };
    private static PatchOperationAdd Add(string xpath = A + "/stats", string value = "<value><power>2</power></value>") =>
        Op<PatchOperationAdd>(("xpath", xpath), ("value", Value(value)));
    private static PatchOperationReplace Replace(string xpath = A + "/label", string value = "<value><label>new</label></value>") =>
        Op<PatchOperationReplace>(("xpath", xpath), ("value", Value(value)));
    private static PatchOperationRemove Remove(string xpath = A + "/obsolete") => Op<PatchOperationRemove>(("xpath", xpath));
    private static XmlRegionOperations Describe(params PatchOperation[] operations)
    {
        Assert.That(XmlRegionOperations.TryCreate(operations, 0, out var region), Is.True);
        return region!;
    }
    private static (XmlElement[] Roots, byte[] Identity) Bind(XmlRegionOperations region, XmlDocument document)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        var roots = region.Bind(document, writer);
        return (roots, bytes.ToArray());
    }
    private sealed class CustomOperation : PatchOperation
    {
        internal int Calls;
        protected override bool ApplyWorker(XmlDocument xml) { Calls++; return true; }
    }
    private sealed class DerivedAdd : PatchOperationAdd { }
    private sealed class DerivedDocument : XmlDocument { }

    [Test]
    public void NativeConditionalCaptureIncludesUnselectedBranchAndOriginalMutableState()
    {
        var match = Replace();
        var nomatch = Remove(B + "/label");
        var conditional = Op<PatchOperationConditional>(("xpath", A), ("match", match), ("nomatch", nomatch));
        var region = Describe(conditional);
        Assert.That(region.Objects, Is.EqualTo(new PatchOperation[] { conditional, match, nomatch }));
        Assert.That(region.CaptureState(), Is.EqualTo(new[] { true, true, true }));
        byte[] before = region.Identity;
        var document = Document("<Defs><ThingDef><defName>A</defName><label>old</label></ThingDef></Defs>");
        Assert.That(conditional.Apply(document), Is.True);
        Assert.That(document.SelectSingleNode(A + "/label")!.InnerText, Is.EqualTo("new"));
        Assert.That(region.CaptureState(), Is.EqualTo(new[] { false, false, true }));
        Assert.That(Describe(conditional).Identity, Is.Not.EqualTo(before));
        Assert.That(region.Identity, Is.EqualTo(before), "A descriptor keeps its captured input identity after native execution.");
    }

    [Test]
    public void ActualFiveNativeFamiliesProduceStateInExactInstanceOrder()
    {
        var add = Add();
        var replace = Replace();
        var remove = Remove();
        var extension = Op<PatchOperationAddModExtension>(("xpath", A), ("value", Value("<value><li Class='DataOnly'/></value>")));
        var branch = Add(A + "/stats", "<value><extra>3</extra></value>");
        var conditional = Op<PatchOperationConditional>(("xpath", A + "/stats/power"), ("match", branch));
        var region = Describe(add, replace, remove, extension, conditional);
        Assert.That(region.ObjectCount, Is.EqualTo(6));
        var document = Document("<Defs><ThingDef><defName>A</defName><label>old</label><stats/><obsolete/></ThingDef></Defs>");
        foreach (var operation in region.Tops) Assert.That(operation.Apply(document), Is.True);
        Assert.That(region.CaptureState(), Is.All.False);
        Assert.That(document.SelectSingleNode(A + "/obsolete"), Is.Null);
        Assert.That(document.SelectSingleNode(A + "/stats/extra")!.InnerText, Is.EqualTo("3"));
        Assert.That(document.SelectSingleNode(A + "/modExtensions/li")!.Attributes!["Class"]!.Value, Is.EqualTo("DataOnly"));
    }

    [TestCase("Normal", false, true)]
    [TestCase("Always", true, false)]
    [TestCase("Never", false, true)]
    [TestCase("Invert", true, false)]
    public void NativeSuccessPolicyAndFailedTargetsRemainPartOfCapturedState(string policy, bool result, bool neverSucceeded)
    {
        var operation = Remove(); Set(operation, "success", policy);
        var region = Describe(operation);
        Assert.That(operation.Apply(Document("<Defs/>")), Is.EqualTo(result));
        Assert.That(region.CaptureState(), Is.EqualTo(new[] { neverSucceeded }));
    }

    [TestCase("Defs/ThingDef[defName='A']/stats", 1, "/stats")]
    [TestCase("/Defs/ThingDef[@Name=\"Template\"]", 1, "")]
    [TestCase("Defs/ThingDef[defName='A' or defName='B']/stats/armor", 2, "/stats/armor")]
    [TestCase("Defs/ThingDef[defName='A'\t or\t@Name='Template']/stats", 2, "/stats")]
    public void GrammarIncludesNativeNamedRootOrCases(string xpath, int count, string tail)
    {
        Assert.That(XmlRegionOperations.TrySelector(xpath, out var keys, out string parsedTail), Is.True);
        Assert.That(keys.Length, Is.EqualTo(count));
        Assert.That(parsedTail, Is.EqualTo(tail));
        Assert.That(keys.Select(k => k.Type), Is.All.EqualTo("ThingDef"));
    }

    [TestCase("Defs/ThingDef[defName='A']/../ThingDef")]
    [TestCase("Defs/ThingDef[defName='A']//stats")]
    [TestCase("Defs/ThingDef[defName='A']/stats|Defs/ThingDef")]
    [TestCase("Defs/ThingDef[defName='A']/parent::Defs")]
    [TestCase("Defs/ThingDef[contains(defName,'A')]/stats")]
    [TestCase("Defs/ThingDef[defName='A' and defName='B']/stats")]
    [TestCase("Defs/ThingDef[defName='A']/stats[@value='1']")]
    [TestCase("Defs/ThingDef[defName='A']/@Name")]
    [TestCase("Defs/*[defName='A']/stats")]
    [TestCase("Defs/ThingDef[defName='A' or ]/stats")]
    [TestCase("Defs/ThingDef[defName='A' or\t]/stats")]
    [TestCase("Defs/ThingDef[defName='A' or  ]/stats")]
    public void GrammarRefusesEscapesUnsupportedPredicatesAndDanglingOr(string xpath)
    { Assert.That(XmlRegionOperations.TrySelector(xpath, out _, out _), Is.False); }

    [Test]
    public void UnsupportedOperationsEndOnlyTheirLocalIntervalWithoutCallingThem()
    {
        var earlier = new CustomOperation(); var later = new CustomOperation();
        var first = Add(); var second = Replace(); var last = Remove();
        var operations = new PatchOperation[] { earlier, first, second, later, last };
        Assert.That(XmlRegionOperations.TryCreate(operations, 0, out _), Is.False);
        Assert.That(XmlRegionOperations.TryCreate(operations, 1, out var middle), Is.True);
        Assert.That(middle!.Tops, Is.EqualTo(new PatchOperation[] { first, second }));
        Assert.That(XmlRegionOperations.TryCreate(operations, 4, out var final), Is.True);
        Assert.That(final!.Tops, Is.EqualTo(new PatchOperation[] { last }));
        Assert.That(earlier.Calls + later.Calls, Is.Zero);
    }

    [Test]
    public void UnselectedCustomChildrenDerivedWorkersCyclesAndSharedInstancesRefuseLocally()
    {
        var custom = new CustomOperation();
        var conditional = Op<PatchOperationConditional>(("xpath", A), ("match", Replace()), ("nomatch", custom));
        Assert.That(XmlRegionOperations.TryCreate(new PatchOperation[] { conditional }, 0, out _), Is.False);
        Assert.That(Describe(Add(), conditional, Remove()).Tops.Length, Is.EqualTo(1));
        Assert.That(custom.Calls, Is.Zero);
        Assert.That(XmlRegionOperations.TryCreate(new PatchOperation[] { Op<DerivedAdd>(("xpath", A), ("value", Value("<value><stats/></value>"))) }, 0, out _), Is.False);
        var cycle = Op<PatchOperationConditional>(("xpath", A)); Set(cycle, "match", cycle);
        Assert.That(XmlRegionOperations.TryCreate(new PatchOperation[] { cycle }, 0, out _), Is.False);
        var shared = Add();
        var sharedChildren = Op<PatchOperationConditional>(("xpath", A), ("match", shared), ("nomatch", shared));
        Assert.That(XmlRegionOperations.TryCreate(new PatchOperation[] { sharedChildren }, 0, out _), Is.False);
        Assert.That(Describe(shared, shared, Remove()).Tops.Length, Is.EqualTo(1));
    }

    [TestCase("sourceFile", "Other/Region.xml")]
    [TestCase("xpath", B + "/stats")]
    [TestCase("success", "Invert")]
    [TestCase("order", "Prepend")]
    public void CurrentSourceOperationFieldsAndOrderInvalidateIdentity(string field, string changed)
    {
        var operation = Add(); var initial = Describe(operation).Identity;
        Set(operation, field, changed);
        Assert.That(Describe(operation).Identity, Is.Not.EqualTo(initial));
    }

    [Test]
    public void ValueContentsOrderAndUnselectedBranchChangesInvalidateIdentity()
    {
        var one = Add(); var two = Replace();
        Assert.That(Describe(one, two).Identity, Is.Not.EqualTo(Describe(two, one).Identity));
        var nomatch = Add(B + "/stats");
        var conditional = Op<PatchOperationConditional>(("xpath", A), ("match", one), ("nomatch", nomatch));
        var initial = Describe(conditional).Identity;
        Set(nomatch, "value", Value("<value><power>changed</power></value>"));
        Assert.That(Describe(conditional).Identity, Is.Not.EqualTo(initial));
        initial = Describe(conditional).Identity; Set(nomatch, "neverSucceeded", false);
        Assert.That(Describe(conditional).Identity, Is.Not.EqualTo(initial));
    }

    [Test]
    public void IdentifierAndRootReplacementStayOutsideTheSupportedMutationContract()
    {
        foreach (var operation in new PatchOperation[] {
            Remove(A), Replace(A), Remove(A + "/defName"), Replace(A + "/defName"),
            Add(A, "<value><defName>B</defName></value>"),
            Replace(A + "/label", "<value><defName>B</defName></value>") })
            Assert.That(XmlRegionOperations.TryCreate(new[] { operation }, 0, out _), Is.False);
    }

    [Test]
    public void BindingKeepsEveryDuplicateAndTemplateInActualDocumentOrder()
    {
        var region = Describe(Add("Defs/ThingDef[defName='A' or @Name='Template']/stats"));
        var document = Document("<Defs><ThingDef Name='Template'><stats/></ThingDef><ThingDef><defName>A</defName><stats/></ThingDef><ThingDef><defName>A</defName><stats><extra/></stats></ThingDef><Other><defName>A</defName></Other></Defs>");
        var bound = Bind(region, document);
        Assert.That(bound.Roots, Is.EqualTo(document.DocumentElement!.ChildNodes.Cast<XmlNode>().Take(3).ToArray()));
        var initial = bound.Identity;
        document.DocumentElement!.RemoveChild(bound.Roots[2]);
        Assert.That(Bind(region, document).Identity, Is.Not.EqualTo(initial));
        document.DocumentElement.PrependChild(bound.Roots[1]);
        Assert.That(Bind(region, document).Roots[0], Is.SameAs(bound.Roots[1]));
        Assert.That(Bind(region, document).Identity, Is.Not.EqualTo(Bind(region, Document("<Defs><ThingDef Name='Template'><stats/></ThingDef><ThingDef><defName>A</defName><stats/></ThingDef></Defs>")).Identity));
    }

    [Test]
    public void AnInitiallyAbsentTargetAndRepeatedDefNameChildrenHaveExactDependencies()
    {
        var region = Describe(Add(A + "/stats"), Add(B + "/stats"));
        var document = Document("<Defs><ThingDef><defName>A</defName><stats/></ThingDef></Defs>");
        var initial = Bind(region, document);
        Assert.That(initial.Roots.Length, Is.EqualTo(1));
        document.DocumentElement!.AppendChild(document.ImportNode(Document("<ThingDef><defName>B</defName><stats/></ThingDef>").DocumentElement!, true));
        Assert.That(Bind(region, document).Identity, Is.Not.EqualTo(initial.Identity));
        var repeated = Document("<Defs><ThingDef><defName>Unrelated</defName><defName>A</defName><defName>B</defName><stats/></ThingDef></Defs>");
        Assert.That(Bind(region, repeated).Roots.Length, Is.EqualTo(1), "XPath can match a later defName child; each physical root is bound once.");
        Assert.That(Bind(region, Document("<Defs/>" )).Roots, Is.Empty);
    }

    [Test]
    public void FullAffectedRootContentsBindWhileIndependentRootsDoNot()
    {
        var region = Describe(Add());
        var document = Document("<Defs><ThingDef><defName>A</defName><stats/><other attr='1'>outside selected suffix</other></ThingDef><ThingDef><defName>Other</defName><stats/></ThingDef></Defs>");
        var initial = Bind(region, document).Identity;
        document.SelectSingleNode("Defs/ThingDef[defName='Other']/stats")!.InnerText = "unrelated edit";
        Assert.That(Bind(region, document).Identity, Is.EqualTo(initial));
        document.SelectSingleNode(A + "/other")!.Attributes!["attr"]!.Value = "2";
        Assert.That(Bind(region, document).Identity, Is.Not.EqualTo(initial));
    }

    [Test]
    public void EqualOuterXmlWithDifferentRetainedTextBoundariesHasDifferentStructuralIdentity()
    {
        var region = Describe(Add());
        var split = Document("<Defs><ThingDef><defName>A</defName><stats>ab</stats></ThingDef></Defs>");
        var combined = (XmlDocument)split.CloneNode(true);
        var stats = split.SelectSingleNode(A + "/stats")!; stats.RemoveAll();
        stats.AppendChild(split.CreateTextNode("a")); stats.AppendChild(split.CreateTextNode("b"));
        Assert.That(split.OuterXml, Is.EqualTo(combined.OuterXml));
        Assert.That(Bind(region, split).Identity, Is.Not.EqualTo(Bind(region, combined).Identity));
    }

    [Test]
    public void TooManyMatchingRootsCustomDocumentsAndDtdRefuseBeforeNativeMutation()
    {
        var region = Describe(Add());
        var document = Document("<Defs>" + string.Concat(Enumerable.Repeat("<ThingDef><defName>A</defName><stats/></ThingDef>", XmlRegionOperations.MaxRoots + 1)) + "</Defs>");
        var original = document.OuterXml;
        Assert.That((Action)(() => Bind(region, document)), Throws.TypeOf<InvalidDataException>());
        Assert.That(document.OuterXml, Is.EqualTo(original));
        var custom = new DerivedDocument(); custom.LoadXml("<Defs/>");
        Assert.That((Action)(() => Bind(region, custom)), Throws.TypeOf<InvalidDataException>());
        Assert.That((Action)(() => Bind(region, Document("<!DOCTYPE Defs [<!ELEMENT Defs ANY>]><Defs/>"))), Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void OperationBoundEndsTheIntervalAndDoesNotConsumeLaterOperations()
    {
        var operations = Enumerable.Range(0, XmlRegionOperations.MaxOperations + 1).Select(_ => (PatchOperation)Add()).ToArray();
        var region = Describe(operations);
        Assert.That(region.Tops.Length, Is.EqualTo(XmlRegionOperations.MaxOperations));
        Assert.That(XmlRegionOperations.TryCreate(operations, region.Tops.Length, out var remaining), Is.True);
        Assert.That(remaining!.Tops.Length, Is.EqualTo(1));
    }
}
