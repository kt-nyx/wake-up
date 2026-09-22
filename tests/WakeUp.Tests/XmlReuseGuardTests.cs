// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class XmlReuseGuardTests
{
    [TestCase("AddPrefixes")]
    [TestCase("AddPostfixes")]
    [TestCase("AddTranspilers")]
    [TestCase("AddFinalizers")]
    [TestCase("AddInnerPrefixes")]
    [TestCase("AddInnerPostfixes")]
    public void EveryPublishedForeignHookKindRefusesSkippedWorkers(string category)
    {
        var target = AccessTools.Method(typeof(PatchOperationTest), "ApplyWorker");
        Type shared = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")!;
        var state = (Dictionary<MethodBase, byte[]>)AccessTools.Field(shared, "state").GetValue(null);
        byte[]? previous;
        lock (state) state.TryGetValue(target, out previous);
        Type serialization = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfoSerialization")!;
        Type infoType = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfo")!;
        object info = previous == null ? Activator.CreateInstance(infoType)!
            : AccessTools.Method(serialization, "Deserialize").Invoke(null, new object[] { previous });
        try
        {
            var hook = new HarmonyMethod(typeof(XmlReuseGuardTests), nameof(EmptyHook));
            AccessTools.Method(infoType, category).Invoke(info, new object[] { "foreign.xml.test", new[] { hook } });
            var published = (byte[])AccessTools.Method(serialization, "Serialize").Invoke(null, new[] { info });
            lock (state) state[target] = published;
            Assert.That(XmlReuseContract.AllowsPatches(target), Is.False);
            lock (state) Assert.That(state[target], Is.SameAs(published));
        }
        finally { lock (state) { if (previous == null) state.Remove(target); else state[target] = previous; } }
        Assert.That(XmlReuseContract.AllowsPatches(target), Is.True);
    }

    private static void EmptyHook() { }

    [Test]
    public void OrdinaryProductExcludesExperimentTypesSettingsAndAutomaticRegistration()
    {
        var settings = new WakeUpSettings();
        Assert.That(typeof(WakeUpSettings).GetField("IndependentXmlReuse"), Is.Null);
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, TestContext.CurrentContext.WorkDirectory),
            Does.Not.Contain("--wake-up-xml-reuse=on"));
        var product = typeof(WakeUpMod).Assembly;
        foreach (string name in new[] { "XmlReuseRuntime", "XmlReusePrepatch", "XmlReuseContract", "XmlReuseOperations", "XmlReuseSnapshot" })
            Assert.That(product.GetType("WakeUp." + name), Is.Null, name + " must not ship");
        Assert.That(typeof(XmlReuseRuntime).Assembly, Is.Not.SameAs(product));
        foreach (var method in typeof(XmlReusePrepatch).GetMethods())
            Assert.That(method.GetCustomAttributesData().Any(a => a.AttributeType.Namespace == "Prepatcher"), Is.False);
    }
}
