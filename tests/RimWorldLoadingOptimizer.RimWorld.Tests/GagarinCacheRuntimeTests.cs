// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using RimWorldLoadingOptimizer.RimWorld;
using Verse;

namespace RimWorldLoadingOptimizer.RimWorld.Tests;

[TestFixture, NonParallelizable]
public sealed class GagarinCacheRuntimeTests
{
    private static readonly Type Runtime = typeof(GagarinCacheRuntime);
    private static object? Call(string name, params object[] args) => AccessTools.Method(Runtime, name).Invoke(null, args);

    [Test]
    public void OptionalSupplierIsNotAnAssemblyReference()
    {
        Assert.That(Runtime.Assembly.GetReferencedAssemblies().Any(a => a.Name == "Gagarin" || a.Name == "MissileGirl"), Is.False);
    }

    [TestCase("<same />", true)]
    [TestCase("<changed />", false)]
    public void ConstructorTextComparisonDetectsDifferentInputWithoutChangingItsReader(string constructorText, bool expected)
    {
        Type scopeType = Runtime.GetNestedType("Scope", BindingFlags.NonPublic)!;
        object scope = Activator.CreateInstance(scopeType, true)!;
        AccessTools.Field(scopeType, "Admitted").SetValue(scope, true);
        AccessTools.Field(scopeType, "Text").SetValue(scope, "<same />");
        FieldInfo current = AccessTools.Field(Runtime, "current");
        try
        {
            current.SetValue(null, scope);
            using var reader = (StringReader)Call("CaptureReader", constructorText)!;
            Assert.That(reader.ReadToEnd(), Is.EqualTo(constructorText));
            Assert.That(AccessTools.Field(scopeType, "SameText").GetValue(scope), Is.EqualTo(expected));
            Assert.That(AccessTools.Field(scopeType, "Text").GetValue(scope), Is.Null);
        }
        finally { current.SetValue(null, null); }
    }

    [Test]
    public void ConstructorRewriteKeepsReaderArgumentsAndRefusesUnknownShape()
    {
        var original = new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Newobj,
            AccessTools.Constructor(typeof(StringReader), new[] { typeof(string) })), new CodeInstruction(OpCodes.Ret) };
        var result = GagarinCacheRuntime.RewriteConstructor(original).ToArray();
        Assert.That(result[0].opcode, Is.EqualTo(OpCodes.Ldarg_0));
        Assert.That(result[1].opcode, Is.EqualTo(OpCodes.Call));
        Assert.That(((MethodInfo)result[1].operand).Name, Is.EqualTo("CaptureReader"));
        Assert.Throws<InvalidOperationException>((Action)(() => GagarinCacheRuntime.RewriteConstructor(original.Concat(original))));
    }

    private static void ForeignXmlPrefix() { }

    [Test]
    public void UnknownXmlParserHookRefusesReuse()
    {
        const string owner = "tests.unknown-cache-parser";
        var harmony = new Harmony(owner);
        try
        {
            harmony.Patch(AccessTools.Method(typeof(XmlDocument), "Load", new[] { typeof(XmlReader) }),
                prefix: new HarmonyMethod(typeof(GagarinCacheRuntimeTests), nameof(ForeignXmlPrefix)));
            Assert.That(Call("SafeHooks"), Is.False);
        }
        finally { harmony.UnpatchAll(owner); }
    }

    [Test]
    public void RewriterRetainsOrderedMappingAndInstructionMetadataAndRefusesDrift()
    {
        var original = new List<CodeInstruction> {
            new(OpCodes.Call, AccessTools.Method(typeof(File), "ReadAllText", new[] { typeof(string) })),
            new(OpCodes.Newobj, AccessTools.Constructor(typeof(LoadableXmlAsset), new[] { typeof(FileInfo), typeof(ModContentPack) })),
            new(OpCodes.Pop),
            new(OpCodes.Callvirt, AccessTools.Method(typeof(XmlNode), "RemoveAll")),
            new(OpCodes.Newobj, AccessTools.Constructor(typeof(XmlDocument), Type.EmptyTypes)),
            new(OpCodes.Callvirt, AccessTools.Method(typeof(XmlNode), "RemoveAll")),
            new(OpCodes.Callvirt, AccessTools.Method(typeof(XmlDocument), "Load", new[] { typeof(XmlReader) })),
            new(OpCodes.Callvirt, AccessTools.Method(typeof(XmlDocument), "ImportNode")),
            new(OpCodes.Callvirt, AccessTools.Method(typeof(Dictionary<XmlNode, object>), "set_Item")),
            new(OpCodes.Callvirt, AccessTools.Method(typeof(XmlNode), "AppendChild")), new(OpCodes.Ret) };
        original[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var result = GagarinCacheRuntime.RewriteLoad(original).ToArray();
        Assert.That(result.Length, Is.EqualTo(original.Count));
        Assert.That(result[0].blocks.Count, Is.EqualTo(1));
        foreach (int index in new[] { 2, 7, 8, 9, 10 })
        { Assert.That(result[index].opcode, Is.EqualTo(original[index].opcode)); Assert.That(result[index].operand, Is.EqualTo(original[index].operand)); }
        Assert.That(((MethodInfo)result[1].operand).Name, Is.EqualTo("CreateAsset"));
        Assert.That(original[1].opcode, Is.EqualTo(OpCodes.Newobj));
        Assert.Throws<InvalidOperationException>((Action)(() => GagarinCacheRuntime.RewriteLoad(original.Skip(1))));
    }

    [Test]
    public void ReusedSourceRemainsIntactAndImportsHaveIndependentDestinationOwnership()
    {
        var source = new XmlDocument();
        source.LoadXml("<DefXmlStorage><Item path='one'><ThingDef><defName>A</defName><value><![CDATA[a & b]]></value></ThingDef></Item><Item path='missing'><OtherDef /></Item><Item path='one'><ThingDef /></Item></DefXmlStorage>");
        string before = source.OuterXml;
        Type scopeType = Runtime.GetNestedType("Scope", BindingFlags.NonPublic)!;
        object scope = Activator.CreateInstance(scopeType, true)!;
        AccessTools.Field(scopeType, "Parsed").SetValue(scope, source);
        FieldInfo current = AccessTools.Field(Runtime, "current");
        try
        {
            current.SetValue(null, scope);
            var reused = (XmlDocument)Call("CreateDocument")!;
            Assert.That(reused, Is.SameAs(source));
            Call("RemoveAll", reused);
            using var invalid = XmlReader.Create(new StringReader("not XML"));
            Call("LoadDocument", reused, invalid); // Redundant reader is never consumed.
            var destination = new XmlDocument();
            destination.LoadXml("<Old />");
            Call("RemoveAll", destination);
            destination.AppendChild(destination.CreateElement("Defs"));
            object asset = new();
            var sources = new Dictionary<string, object> { ["one"] = asset };
            var mapped = new Dictionary<XmlNode, object>();
            // This is the supplier's unchanged loop, including unmapped nodes.
            foreach (XmlElement item in reused.DocumentElement!.ChildNodes)
            {
                XmlNode node = destination.ImportNode(item.FirstChild!, true);
                if (sources.TryGetValue(item.GetAttribute("path"), out object value)) mapped[node] = value;
                destination.DocumentElement!.AppendChild(node);
            }
            Assert.That(source.OuterXml, Is.EqualTo(before));
            Assert.That(mapped.Count, Is.EqualTo(2));
            Assert.That(mapped.Values.All(v => ReferenceEquals(v, asset)), Is.True);
            Assert.That(destination.DocumentElement!.ChildNodes.Count, Is.EqualTo(3));
            foreach (XmlNode node in destination.SelectNodes("//*")!) Assert.That(node.OwnerDocument, Is.SameAs(destination));
            destination.DocumentElement.FirstChild!.FirstChild!.InnerText = "changed";
            Assert.That(source.OuterXml, Is.EqualTo(before));
        }
        finally { current.SetValue(null, null); }
        var fallback = (XmlDocument)Call("CreateDocument")!;
        using var reader = XmlReader.Create(new StringReader("<original />"));
        Call("LoadDocument", fallback, reader);
        Assert.That(fallback.DocumentElement!.Name, Is.EqualTo("original"));
    }
}
