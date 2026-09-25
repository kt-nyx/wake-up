// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class DefLookupQueryExtensionTests
{
    private static int foreignCalls;
    private static XPathExpression redirected = null!;

    [TestCase(0)] // Hook already present: both installations must refuse.
    [TestCase(1)] // Installed caches, but no memo result exists yet.
    [TestCase(2)] // A memo result was captured before the hook appeared.
    [TestCase(3)] // Memo installed, then the hook makes plan installation refuse.
    public void DownstreamEvaluatorEffectsAndResultsStayNativeRegardlessOfPlanInstallation(int hookTiming)
    {
        const string owner = "wakeup.def-lookup";
        var harmony = new Harmony(owner);
        var foreign = new Harmony("WakeUp.Query.Tests.Evaluator");
        MethodInfo target = AccessTools.Method(typeof(XmlNode), nameof(XmlNode.SelectSingleNode), new[] { typeof(string) });
        MethodInfo nodes = AccessTools.Method(typeof(XmlNode), nameof(XmlNode.SelectNodes), new[] { typeof(string) });
        MethodInfo evaluator = AccessTools.Method(typeof(XPathNavigator), nameof(XPathNavigator.Select), new[] { typeof(XPathExpression) });
        FieldInfo activeField = AccessTools.Field(typeof(DefLookupRuntime), "singleMemo");
        var guards = (List<PublishedPatchGuard>)AccessTools.Field(typeof(DefLookupRuntime), "SingleGuards").GetValue(null)!;
        var document = new XmlDocument();
        document.LoadXml("<Defs><ThingDef><verbs><li>one</li><li>two</li></verbs></ThingDef></Defs>");
        XmlNode context = document.DocumentElement!.FirstChild!;
        using var memo = new ScopedSingleQueryMemo(document);
        ScopedXPathPlans? plans = null;
        redirected = XPathExpression.Compile("verbs/li[2]");
        var query = new object[] { "verbs/li[1]" };
        void Hook() => foreign.Patch(evaluator, prefix: new HarmonyMethod(typeof(DefLookupQueryExtensionTests), nameof(RedirectEvaluation)));
        try
        {
            Assert.That(guards, Is.Empty);
            Assert.That(activeField.GetValue(null), Is.Null);
            if (hookTiming == 0) Hook();
            bool memoInstalled = (bool)AccessTools.Method(typeof(DefLookupRuntime), "PrepareQueryGuards").Invoke(null, null)!
                && (bool)AccessTools.Method(typeof(DefLookupRuntime), "InstallSingleMemo").Invoke(null, new object[] { harmony })!;
            if (hookTiming == 3) Hook();
            bool planInstalled = XPathPlanRuntime.Install(harmony);
            Assert.That(memoInstalled, Is.EqualTo(hookTiming != 0));
            Assert.That(planInstalled, Is.EqualTo(hookTiming == 1 || hookTiming == 2));
            if (memoInstalled) activeField.SetValue(null, memo);
            if (planInstalled) plans = XPathPlanRuntime.Begin(document);
            if (hookTiming == 2)
            {
                Assert.That(target.Invoke(context, query), Is.SameAs(context["verbs"]!.FirstChild));
                Assert.That(memo.Captures, Is.EqualTo(1));
            }
            if (hookTiming == 1 || hookTiming == 2) Hook();
            foreignCalls = 0;
            long captures = memo.Captures;
            long compilations = plans?.Compilations ?? 0;
            Assert.That(target.Invoke(context, query), Is.SameAs(context["verbs"]!.LastChild));
            Assert.That(target.Invoke(context, query), Is.SameAs(context["verbs"]!.LastChild));
            Assert.That(foreignCalls, Is.EqualTo(2), "Every native evaluator callback must execute, including after a memo result already exists.");
            Assert.That(memo.Hits, Is.Zero);
            Assert.That(memo.Captures, Is.EqualTo(captures), "Refused downstream evaluation must not populate the memo.");
            Assert.That(plans?.Compilations ?? 0, Is.EqualTo(compilations));
            Assert.That(plans?.Hits ?? 0, Is.Zero);
        }
        finally
        {
            activeField.SetValue(null, null);
            XPathPlanRuntime.End(plans);
            foreign.Unpatch(evaluator, HarmonyPatchType.All, foreign.Id);
            harmony.Unpatch(target, HarmonyPatchType.All, owner);
            harmony.Unpatch(nodes, HarmonyPatchType.Transpiler, owner);
            guards.Clear();
            ((List<PublishedPatchGuard>)AccessTools.Field(typeof(XPathPlanRuntime), "Guards").GetValue(null)!).Clear();
            AccessTools.Field(typeof(XPathPlanRuntime), "<Installed>k__BackingField").SetValue(null, false);
        }
    }

    private static void RedirectEvaluation(ref XPathExpression __0)
    { foreignCalls++; __0 = redirected; }

    [TestCase(false)]
    [TestCase(true)]
    public void ActualSingleNodeCallsiteCapturesReusesAndRefusesLaterForeignEffects(bool indexed)
    {
        const string owner = "wakeup.def-lookup";
        var harmony = new Harmony(owner);
        var foreign = new Harmony("WakeUp.Query.Tests.Foreign");
        MethodInfo target = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectSingleNode), new[] { typeof(string) })!;
        MethodInfo underlying = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectNodes), new[] { typeof(string) })!;
        FieldInfo activeField = AccessTools.Field(typeof(DefLookupRuntime), "singleMemo");
        var guards = (List<PublishedPatchGuard>)AccessTools.Field(typeof(DefLookupRuntime), "SingleGuards").GetValue(null)!;
        Assert.That(activeField.GetValue(null), Is.Null);
        Assert.That(guards, Is.Empty);
        var document = new XmlDocument();
        document.LoadXml("<Defs><ThingDef><defName>A</defName><verbs><li>one</li></verbs></ThingDef></Defs>");
        using var memo = new ScopedSingleQueryMemo(document);
        using var lookup = new ScopedDefLookup(document);
        FieldInfo lookupField = AccessTools.Field(typeof(DefLookupRuntime), "active");
        try
        {
            bool installed = (bool)AccessTools.Method(typeof(DefLookupRuntime), "PrepareQueryGuards").Invoke(null, null)!
                && (bool)AccessTools.Method(typeof(DefLookupRuntime), "InstallSingleMemo").Invoke(null, new object[] { harmony })!;
            Assert.That(installed, Is.True);
            activeField.SetValue(null, memo);
            if (indexed) lookupField.SetValue(null, lookup);
            XmlNode customContext = document.DocumentElement!.FirstChild!;
            // Reflection ensures this tiny framework method is called through
            // its actual patched entry rather than JIT-inlined into the test.
            var query = new object[] { indexed ? "Defs/ThingDef[ defName='A' ][1]/verbs/li[1]" : "verbs/li[1]" };
            XmlNode callContext = indexed ? document : customContext;
            Assert.That(target.Invoke(callContext, query), Is.SameAs(customContext["verbs"]!.FirstChild));
            Assert.That(target.Invoke(callContext, query), Is.SameAs(customContext["verbs"]!.FirstChild));
            Assert.That(memo.Hits, Is.EqualTo(1));
            Assert.That(memo.Captures, Is.EqualTo(1));
            Assert.That(lookup.Hits, Is.EqualTo(indexed ? 1 : 0));

            foreach (MethodInfo changed in new[] { target, underlying })
            {
                foreignCalls = 0;
                foreign.Patch(changed, prefix: new HarmonyMethod(typeof(DefLookupQueryExtensionTests), nameof(ForeignEffect)));
                long hits = memo.Hits;
                target.Invoke(callContext, query);
                target.Invoke(callContext, query);
                Assert.That(foreignCalls, Is.EqualTo(2), "Effects on the single query or its native list call cannot be skipped.");
                Assert.That(memo.Hits, Is.EqualTo(hits));
                foreign.Unpatch(changed, HarmonyPatchType.All, foreign.Id);
            }

            customContext["verbs"]!.InnerXml = "<li>replacement</li>";
            Assert.That(target.Invoke(callContext, query), Is.SameAs(customContext["verbs"]!.FirstChild));
            Assert.That(target.Invoke(callContext, query), Is.SameAs(customContext["verbs"]!.FirstChild));
            Assert.That(memo.Hits, Is.EqualTo(2));
            Assert.That(lookup.Hits, Is.EqualTo(indexed ? 2 : 0));

            activeField.SetValue(null, null);
            lookupField.SetValue(null, null);
            target.Invoke(customContext, query);
            target.Invoke(customContext, query);
            Assert.That(memo.Hits, Is.EqualTo(2), "The global patch has no behavior outside the active stage.");
        }
        finally
        {
            activeField.SetValue(null, null);
            foreign.Unpatch(target, HarmonyPatchType.All, foreign.Id);
            lookupField.SetValue(null, null);
            foreign.Unpatch(underlying, HarmonyPatchType.All, foreign.Id);
            harmony.Unpatch(target, HarmonyPatchType.All, owner);
            guards.Clear();
        }
    }

    private static void ForeignEffect() => foreignCalls++;
}
