// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class XmlReuseOperationsTests
{
    private bool profiling;
    [SetUp] public void SetUp() { profiling = DeepProfiler.enabled; DeepProfiler.enabled = false; }
    [TearDown] public void TearDown() { DeepProfiler.enabled = profiling; }
    private static XmlDocument Document(string xml)
    { var document = new XmlDocument { PreserveWhitespace = true }; document.LoadXml(xml); return document; }
    private static T Op<T>(params (string Name, object? Value)[] fields) where T : PatchOperation, new()
    {
        var operation = new T { sourceFile = "Patches/Example.xml" };
        foreach (var field in fields) Set(operation, field.Name, field.Value);
        return operation;
    }
    private static void Set(object target, string name, object? value)
    {
        FieldInfo field = AccessTools.Field(target.GetType(), name);
        field.SetValue(target, field.FieldType.IsEnum && value is string text ? Enum.Parse(field.FieldType, text) : value);
    }
    private static T Get<T>(object target, string name) => (T)AccessTools.Field(target.GetType(), name).GetValue(target);
    private static XmlContainer Value(string xml) => new() { node = Document(xml).DocumentElement! };
    private static XmlReuseOperations Describe(params PatchOperation[] operations)
    {
        Assert.That(XmlReuseOperations.TryCreate(operations, out var result), Is.True);
        return result!;
    }
    private static PatchOperationTest Test(string xpath = "/Defs/A") => Op<PatchOperationTest>(("xpath", xpath));
    private static PatchOperationSequence Sequence(params PatchOperation[] children) => Op<PatchOperationSequence>(("operations", children.ToList()));

    [Test]
    public void NativeNestedConditionalCaptureRestoresExactStateIncludingUnselectedChildrenAndCompletion()
    {
        PatchOperationSequence Tree()
        {
            var inner = Sequence(Test("/Defs/Missing"), Test());
            Set(inner, "success", "Always");
            var conditional = Op<PatchOperationConditional>(("xpath", "/Defs/A"), ("match", inner), ("nomatch", Test()));
            var top = Sequence(conditional, Test("/Defs/AlsoMissing"));
            Set(top, "success", "Always");
            return top;
        }
        var original = Tree(); var capture = Describe(original);
        Assert.That(capture.CompletedSuccessfully, Is.False);
        Assert.That(original.Apply(Document("<Defs><A /></Defs>")), Is.True);
        Assert.That(capture.CompletedSuccessfully, Is.True);
        byte[] state = capture.CaptureState();
        var restored = Tree(); var hit = Describe(restored);
        Assert.That(hit.Identity, Is.EqualTo(capture.Identity));
        Action commit = hit.PrepareRestore(state);
        Assert.That(hit.CompletedSuccessfully, Is.False, "Preparation cannot alter live patch instances.");
        commit();
        Assert.That(hit.CompletedSuccessfully, Is.True);
        Assert.That(hit.CaptureState(), Is.EqualTo(state));
        var children = Get<List<PatchOperation>>(restored, "operations");
        var condition = children[0];
        var innerRestored = Get<PatchOperationSequence>(condition, "match");
        Assert.That(Get<bool>(Get<PatchOperation>(condition, "nomatch"), "neverSucceeded"), Is.True);
        Assert.That(Get<bool>(Get<List<PatchOperation>>(innerRestored, "operations")[1], "neverSucceeded"), Is.True);
        Assert.That(Get<PatchOperation>(restored, "lastFailedOperation"), Is.SameAs(children[1]));
        restored.Complete("offline-test");
        Assert.That(Get<PatchOperation?>(restored, "lastFailedOperation"), Is.Null);
        Assert.That(Get<PatchOperation?>(innerRestored, "lastFailedOperation"), Is.Not.Null,
            "Native top-level completion does not complete nested operations early.");
    }

    private sealed class CustomOperation : PatchOperation
    {
        internal int Calls;
        protected override bool ApplyWorker(XmlDocument xml) { Calls++; return true; }
    }
    private sealed class DerivedTest : PatchOperationTest { }
    [Test]
    public void UnknownTopLevelOrUnselectedNestedBranchEndsThePrefixWithoutInvokingCustomCode()
    {
        var custom = new CustomOperation();
        var conditional = Op<PatchOperationConditional>(("xpath", "/Defs/A"), ("match", Test()), ("nomatch", custom));
        Assert.That(XmlReuseOperations.TryCreate(new PatchOperation[] { conditional, Test() }, out _), Is.False);
        var prefix = Describe(Test(), conditional, Test());
        Assert.That(prefix.PrefixCount, Is.EqualTo(1));
        Assert.That(prefix.OperationCount, Is.EqualTo(1));
        Assert.That(custom.Calls, Is.Zero);
        Assert.That(XmlReuseOperations.TryCreate(new PatchOperation[] { custom }, out _), Is.False);
        Assert.That(XmlReuseOperations.TryCreate(new PatchOperation[] { new DerivedTest() }, out _), Is.False);
        Assert.That(XmlReuseOperations.TryCreate(Array.Empty<PatchOperation>(), out _), Is.False);
    }

    [Test]
    public void CyclesSharedChildrenAndForeignSequenceFailureReferencesRefuseThatWholeTopLevel()
    {
        var cycle = Sequence(); Set(cycle, "operations", new List<PatchOperation> { cycle });
        Assert.That(XmlReuseOperations.TryCreate(new PatchOperation[] { cycle }, out _), Is.False);
        var child = Test(); var shared = Sequence(child, child);
        Assert.That(XmlReuseOperations.TryCreate(new PatchOperation[] { shared }, out _), Is.False);
        var foreign = Sequence(Test()); Set(foreign, "lastFailedOperation", Test());
        Assert.That(XmlReuseOperations.TryCreate(new PatchOperation[] { foreign }, out _), Is.False);
        Assert.That(Describe(Test(), foreign).PrefixCount, Is.EqualTo(1));
    }

    [Test]
    public void AllThirteenExactNativeFamiliesHaveCompleteSupportedLayouts()
    {
        var operations = new PatchOperation[] {
            Op<PatchOperationAdd>(("xpath", "/Defs"), ("value", Value("<value><B /></value>"))),
            Op<PatchOperationAddModExtension>(("xpath", "/Defs/A"), ("value", Value("<value><li /></value>"))),
            Op<PatchOperationInsert>(("xpath", "/Defs/A"), ("value", Value("<value><B /></value>"))),
            Op<PatchOperationRemove>(("xpath", "/Defs/A")),
            Op<PatchOperationReplace>(("xpath", "/Defs/A"), ("value", Value("<value><B /></value>"))),
            Op<PatchOperationSetName>(("xpath", "/Defs/A"), ("name", "B")),
            Op<PatchOperationAttributeAdd>(("xpath", "/Defs/A"), ("attribute", "Name"), ("value", "B")),
            Op<PatchOperationAttributeRemove>(("xpath", "/Defs/A"), ("attribute", "Name")),
            Op<PatchOperationAttributeSet>(("xpath", "/Defs/A"), ("attribute", "Name"), ("value", "B")),
            Test(), Sequence(), Op<PatchOperationConditional>(("xpath", "/Defs/A")),
            Op<PatchOperationFindMod>(("mods", new List<string> { "Exact Display Name" }))
        };
        var descriptor = Describe(operations);
        Assert.That(descriptor.PrefixCount, Is.EqualTo(13));
        Assert.That(descriptor.OperationCount, Is.EqualTo(13));
    }

    [TestCase("sourceFile", "Other.xml")]
    [TestCase("xpath", "/Defs/B")]
    [TestCase("success", "Invert")]
    [TestCase("order", "Prepend")]
    public void EffectiveFieldsChangeIdentity(string field, string value)
    {
        var operation = Op<PatchOperationAdd>(("xpath", "/Defs"), ("value", Value("<value><A /></value>")));
        byte[] initial = Describe(operation).Identity;
        Set(operation, field, value);
        Assert.That(Describe(operation).Identity, Is.Not.EqualTo(initial));
    }

    [Test]
    public void ValuesOperationOrderModNamesAndUnselectedBranchesBindIdentity()
    {
        var add = Op<PatchOperationAdd>(("xpath", "/Defs"), ("value", Value("<value><A attr='first' /></value>")));
        byte[] initial = Describe(add).Identity;
        Get<XmlContainer>(add, "value").node.FirstChild!.Attributes!["attr"]!.Value = "second";
        Assert.That(Describe(add).Identity, Is.Not.EqualTo(initial));
        Assert.That(Describe(Test("/Defs/A"), Test("/Defs/B")).Identity, Is.Not.EqualTo(Describe(Test("/Defs/B"), Test("/Defs/A")).Identity));
        var find = Op<PatchOperationFindMod>(("mods", new List<string> { "Name A", "Name B" }), ("nomatch", Test()));
        initial = Describe(find).Identity;
        Get<List<string>>(find, "mods").Reverse();
        Assert.That(Describe(find).Identity, Is.Not.EqualTo(initial));
        initial = Describe(find).Identity;
        Set(Get<PatchOperation>(find, "nomatch"), "xpath", "/Defs/Changed");
        Assert.That(Describe(find).Identity, Is.Not.EqualTo(initial));
        Set(find, "mods", new List<string>()); initial = Describe(find).Identity;
        Set(find, "mods", null);
        Assert.That(Describe(find).Identity, Is.Not.EqualTo(initial));
    }

    [Test]
    public void InitialMutableStateAndOwnFailedChildArePartOfIdentity()
    {
        var child = Test(); var top = Sequence(child);
        byte[] initial = Describe(top).Identity;
        Set(child, "neverSucceeded", false);
        Assert.That(Describe(top).Identity, Is.Not.EqualTo(initial));
        initial = Describe(top).Identity;
        Set(top, "lastFailedOperation", child);
        Assert.That(Describe(top).Identity, Is.Not.EqualTo(initial));
    }

    [Test]
    public void XmlNodeKindsNamespaceAndEmptyElementStateAreDistinguished()
    {
        byte[] Identity(string xml) => Describe(Op<PatchOperationAdd>(("xpath", "/Defs"), ("value", Value(xml)))).Identity;
        Assert.That(Identity("<value><A /></value>"), Is.Not.EqualTo(Identity("<value><A></A></value>")));
        Assert.That(Identity("<value><A>x</A></value>"), Is.Not.EqualTo(Identity("<value><A><![CDATA[x]]></A></value>")));
        Assert.That(Identity("<value><A xmlns='one' /></value>"), Is.Not.EqualTo(Identity("<value><A xmlns='two' /></value>")));
        Assert.That(Identity("<value><!--one--></value>"), Is.Not.EqualTo(Identity("<value><!--two--></value>")));
    }

    [Test]
    public void CorruptStateIsRejectedBeforeAnyAssignmentsAndBindsTheDescriptorIdentity()
    {
        var top = Sequence(Test()); var descriptor = Describe(top);
        Assert.That(top.Apply(Document("<Defs><A /></Defs>")), Is.True);
        byte[] valid = descriptor.CaptureState();
        var target = Describe(Sequence(Test()));
        byte[] before = target.CaptureState();
        void Reject(byte[] malformed)
        {
            Assert.Throws<InvalidDataException>((Action)(() => { target.PrepareRestore(malformed); }));
            Assert.That(target.CaptureState(), Is.EqualTo(before));
        }
        Reject(valid.Take(valid.Length - 1).ToArray());
        Reject(valid.Concat(new byte[] { 0 }).ToArray());
        byte[] changed = (byte[])valid.Clone(); changed[0] ^= 1; Reject(changed);
        changed = (byte[])valid.Clone(); changed[8] ^= 1; Reject(changed);
        changed = (byte[])valid.Clone(); changed[40] = 2; Reject(changed);
        changed = (byte[])valid.Clone(); Array.Copy(BitConverter.GetBytes(0), 0, changed, 41, 4); Reject(changed); // sequence cannot fail itself
        changed = (byte[])valid.Clone(); Array.Copy(BitConverter.GetBytes(-1), 0, changed, 46, 4); Reject(changed); // nonsequence has no failure reference
        var other = Describe(Sequence(Test("/Defs/Different")));
        Assert.Throws<InvalidDataException>((Action)(() => { other.PrepareRestore(valid); }));
        target.PrepareRestore(valid)();
        Assert.That(target.CaptureState(), Is.EqualTo(valid));
    }

    [Test]
    public void FiniteDepthOperationAndIdentityBudgetsTruncateBeforeTheAffectedTree()
    {
        PatchOperation deep = Test();
        for (int i = 0; i < 64; i++) deep = Sequence(deep);
        Assert.That(Describe(Test(), deep).PrefixCount, Is.EqualTo(1));
        var wide = Sequence(Enumerable.Range(0, 32768).Select(_ => (PatchOperation)Test()).ToArray());
        Assert.That(XmlReuseOperations.TryCreate(new PatchOperation[] { wide }, out _), Is.False);
        var large = Test(); large.sourceFile = new string('x', 16 * 1024 * 1024);
        Assert.That(Describe(Test(), large).PrefixCount, Is.EqualTo(1));
    }
}
