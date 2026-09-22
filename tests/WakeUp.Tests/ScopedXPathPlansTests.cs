// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ScopedXPathPlansTests
{
    [Test]
    public void PlansSurviveMutationButIteratorsAndMissesAreAlwaysFresh()
    {
        XmlDocument document = Document();
        using var plans = new ScopedXPathPlans(document);
        const string xpath = "Defs/ThingDef[defName='A' or defName='B']/li";
        XPathNavigator navigator = document.CreateNavigator()!;
        var first = plans.Select(navigator, xpath);
        var second = plans.Select(navigator, xpath);
        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.MoveNext(), Is.True);
        Assert.That(second.MoveNext(), Is.True);
        Assert.That(((IHasXmlNode)first.Current!).GetNode(), Is.SameAs(((IHasXmlNode)second.Current!).GetNode()));
        Assert.That(first.MoveNext(), Is.True);
        Assert.That(second.Current!.Value, Is.EqualTo("one"), "Advancing another iterator cannot change this iterator's state.");
        const string missing = "Defs/Added";
        Assert.That(Nodes(plans.Select(navigator, missing)), Is.Empty);
        document.DocumentElement!.AppendChild(document.CreateElement("Added"));
        Assert.That(Nodes(plans.Select(navigator, missing)).Single(), Is.SameAs(document.DocumentElement.LastChild));
        document.DocumentElement.FirstChild!["defName"]!.InnerText = "C";
        Assert.That(Nodes(plans.Select(navigator, xpath)), Is.EqualTo(Nodes(navigator.Select(xpath))));
        Assert.That(plans.Compilations, Is.EqualTo(2));
        Assert.That(plans.Hits, Is.EqualTo(3));
    }

    [TestCase("Defs/ThingDef[defName='A' and li='one'][1]/li")]
    [TestCase("Defs/ThingDef[@Name='Template']/li | Defs/ThingDef[defName='B']/li")]
    [TestCase("Defs/ThingDef[2]/preceding-sibling::ThingDef/defName")]
    [TestCase("Defs/ThingDef[1]/@Name")]
    public void NativeExpressionEvaluationPreservesShapesOrderAndActualNodeReferences(string xpath)
    {
        XmlDocument document = Document();
        using var plans = new ScopedXPathPlans(document);
        for (int pass = 0; pass < 2; pass++)
            Assert.That(Nodes(plans.Select(document.CreateNavigator()!, xpath)), Is.EqualTo(document.SelectNodes(xpath)!.Cast<XmlNode>()));
        Assert.That(plans.Compilations, Is.EqualTo(1));
        Assert.That(plans.Hits, Is.EqualTo(1));
    }

    [TestCase("Defs/[")]
    [TestCase("Defs/unknown:ThingDef")]
    [TestCase("count(Defs/ThingDef)")]
    public void NativeErrorsAreNotRetriedOrConvertedToEmptyResults(string xpath)
    {
        XmlDocument document = Document();
        using var plans = new ScopedXPathPlans(document);
        Exception expected = Assert.Catch((Action)(() => Nodes(document.CreateNavigator()!.Select(xpath))))!;
        for (int pass = 0; pass < 2; pass++)
        {
            Exception actual = Assert.Catch((Action)(() => Nodes(plans.Select(document.CreateNavigator()!, xpath))))!;
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
            Assert.That(actual.Message, Is.EqualTo(expected.Message));
        }
    }

    [Test]
    public void CapacityForeignDocumentAndDisposedScopeKeepOriginalQueries()
    {
        XmlDocument document = Document();
        using var plans = new ScopedXPathPlans(document, maximumEntries: 1);
        Nodes(plans.Select(document.CreateNavigator()!, "Defs/ThingDef"));
        Nodes(plans.Select(document.CreateNavigator()!, "Defs/ThingDef/li"));
        Nodes(plans.Select(Document().CreateNavigator()!, "Defs/ThingDef"));
        Assert.That(plans.Compilations, Is.EqualTo(1));
        Assert.That(plans.Fallbacks, Is.EqualTo(2));
        plans.Dispose();
        Assert.That(Nodes(plans.Select(document.CreateNavigator()!, "Defs/ThingDef")), Has.Length.EqualTo(2));
        Assert.That(plans.Count, Is.Zero);
    }

    private static int foreignCompilations;
    [Test]
    public void ActualXmlNodeListRemainsNativeAndLazyWhileRepeatedCompilationsAreAvoided()
    {
        const string owner = "wakeup.def-lookup";
        var harmony = new Harmony(owner);
        var foreign = new Harmony("WakeUp.XPathPlans.Tests.Foreign");
        MethodInfo nodes = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectNodes), new[] { typeof(string) })!;
        MethodInfo compile = typeof(XPathExpression).GetMethod(nameof(XPathExpression.Compile), new[] { typeof(string), typeof(IXmlNamespaceResolver) })!;
        ScopedXPathPlans? plans = null;
        try
        {
            XmlDocument baseline = Document();
            const string xpath = "Defs/ThingDef/li";
            XmlNodeList expected = (XmlNodeList)nodes.Invoke(baseline, new object[] { xpath })!;
            Assert.That(XPathPlanRuntime.Install(harmony), Is.True);
            XmlDocument candidate = Document();
            plans = XPathPlanRuntime.Begin(candidate)!;
            Assert.That(plans, Is.Not.Null);
            XmlNodeList actual = (XmlNodeList)nodes.Invoke(candidate, new object[] { xpath })!;
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
            // Mutate before enumeration, then compare the native list's real
            // prefetch semantics rather than imposing a snapshot interpretation.
            foreach (XmlDocument document in new[] { baseline, candidate })
                document.DocumentElement!.FirstChild!.AppendChild(document.CreateElement("li")).InnerText = "later";
            Assert.That(actual.Cast<XmlNode>().Select(n => n.OuterXml), Is.EqualTo(expected.Cast<XmlNode>().Select(n => n.OuterXml)));
            XmlNodeList fresh = (XmlNodeList)nodes.Invoke(candidate, new object[] { xpath })!;
            Assert.That(fresh.Cast<XmlNode>(), Is.EqualTo(candidate.DocumentElement!.ChildNodes.Cast<XmlNode>().SelectMany(n => n.ChildNodes.Cast<XmlNode>()).Where(n => n.Name == "li")));
            Assert.That(plans.Compilations, Is.EqualTo(1));
            Assert.That(plans.Hits, Is.EqualTo(1));
            foreignCompilations = 0;
            foreign.Patch(compile, prefix: new HarmonyMethod(typeof(ScopedXPathPlansTests), nameof(ObserveCompile)));
            compile.Invoke(null, new object?[] { xpath, null });
            Assert.That(foreignCompilations, Is.EqualTo(1), "Verify that the foreign compilation observer is actually installed.");
            // Framework JIT inlining can bypass a hook added after the native
            // caller was compiled. Compare the ordinary native query path,
            // rather than requiring a late hook that native itself may miss.
            foreignCompilations = 0;
            baseline.CreateNavigator()!.Select(xpath);
            baseline.CreateNavigator()!.Select(xpath);
            int nativeObservations = foreignCompilations;
            foreignCompilations = 0;
            nodes.Invoke(candidate, new object[] { xpath });
            nodes.Invoke(candidate, new object[] { xpath });
            Assert.That(foreignCompilations, Is.EqualTo(nativeObservations), "Fallback must preserve the compilation effects observed on the ordinary native path.");
            Assert.That(plans.Hits, Is.EqualTo(1), "Foreign compilation hooks must disable reuse even if JIT inlining makes that hook unobservable to native calls.");
        }
        finally
        {
            XPathPlanRuntime.End(plans);
            foreign.Unpatch(compile, HarmonyPatchType.All, foreign.Id);
            harmony.Unpatch(nodes, HarmonyPatchType.Transpiler, owner);
            ((List<PublishedPatchGuard>)AccessTools.Field(typeof(XPathPlanRuntime), "Guards").GetValue(null)!).Clear();
            AccessTools.Field(typeof(XPathPlanRuntime), "<Installed>k__BackingField").SetValue(null, false);
        }
    }

    private static void ObserveCompile() => foreignCompilations++;
    private static XmlNode[] Nodes(XPathNodeIterator iterator)
    { var result = new List<XmlNode>(); while (iterator.MoveNext()) result.Add(((IHasXmlNode)iterator.Current!).GetNode()); return result.ToArray(); }
    private static XmlDocument Document()
    {
        var document = new XmlDocument();
        document.LoadXml("<Defs><ThingDef Name='Template'><defName>A</defName><li>one</li><li>two</li></ThingDef><ThingDef><defName>B</defName><li>three</li></ThingDef></Defs>");
        return document;
    }
}
