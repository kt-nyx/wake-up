// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class XmlReuseContentTests
{
    [Test]
    public void FrozenOfficialContentCapturesAndAvoidsActualNativeOperations()
    {
        string? data = Environment.GetEnvironmentVariable("WAKE_UP_XML_OFFLINE_DATA");
        if (data == null) { Assert.Ignore("Optional explicit read-only frozen XML sample."); return; }
        var assets = new List<LoadableXmlAsset>();
        var patchNodes = new List<(XmlNode Node, string File)>();
        // This is an explicit official-content sample, not a production
        // admission allowlist or a reconstruction of a mixed modlist's loader.
        foreach (string package in new[] { "Core", "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey" })
        {
            string defs = Path.Combine(data, package, "Defs");
            foreach (string file in Directory.GetFiles(defs, "*.xml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                assets.Add(new LoadableXmlAsset(file, File.ReadAllText(file)));
            string patches = Path.Combine(data, package, "Patches");
            if (!Directory.Exists(patches)) continue;
            foreach (string file in Directory.GetFiles(patches, "*.xml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                var xml = new LoadableXmlAsset(file, File.ReadAllText(file)).xmlDoc;
                foreach (XmlNode node in xml.SelectNodes("/Patch/Operation")!) patchNodes.Add((node, file));
            }
        }
        PatchOperation[] Operations() => patchNodes.Select(p => ReadOperation(p.Node, p.File)).ToArray();
        Assert.That(XmlReuseOperations.TryCreate(Operations(), out var descriptor), Is.True);
        Assert.That(descriptor!.PrefixCount, Is.EqualTo(patchNodes.Count));
        string profile = Path.Combine(Path.GetTempPath(), "xml-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        bool profiling = DeepProfiler.enabled; DeepProfiler.enabled = false;
        try
        {
            var firstMap = new Dictionary<XmlNode, LoadableXmlAsset>();
            var first = LoadedModManager.CombineIntoUnifiedXML(assets, firstMap);
            var firstOps = Operations();
            int failures = 0;
            void Native(XmlDocument xml, Dictionary<XmlNode, LoadableXmlAsset> map)
            { foreach (var op in firstOps) if (!XmlReuseRuntime.ApplyOne(op, xml)) failures++; }
            var watch = Stopwatch.StartNew();
            XmlReuseRuntime.Execute(ref first, ref firstMap, assets, firstOps, "explicit-official-sample", profile,
                CacheLaunchPolicy.Current, () => true, Native);
            double cold = watch.Elapsed.TotalMilliseconds;
            Assert.That(failures, Is.Zero);
            Assert.That(XmlReuseRuntime.Status, Does.Contain("captured yes"));
            byte[] expected = XmlReuseSnapshot.Encode(first, firstMap, assets, Array.Empty<byte>());
            var secondMap = new Dictionary<XmlNode, LoadableXmlAsset>();
            var second = LoadedModManager.CombineIntoUnifiedXML(assets, secondMap);
            var secondOps = Operations();
            watch.Restart();
            XmlReuseRuntime.Execute(ref second, ref secondMap, assets, secondOps, "explicit-official-sample", profile,
                CacheLaunchPolicy.Current, () => true,
                (xml, map) => { foreach (var op in secondOps) Assert.That(XmlReuseRuntime.ApplyOne(op, xml), Is.True); });
            double warm = watch.Elapsed.TotalMilliseconds;
            Assert.That(XmlReuseRuntime.Status, Does.Contain("validated hit").And.Contain("Skipped " + patchNodes.Count));
            Assert.That(XmlReuseSnapshot.Encode(second, secondMap, assets, Array.Empty<byte>()), Is.EqualTo(expected));
            var ordinaryMap = new Dictionary<XmlNode, LoadableXmlAsset>();
            var ordinary = LoadedModManager.CombineIntoUnifiedXML(assets, ordinaryMap);
            watch.Restart(); foreach (var op in Operations()) Assert.That(op.Apply(ordinary), Is.True);
            double native = watch.Elapsed.TotalMilliseconds;
            Assert.That(XmlReuseSnapshot.Encode(ordinary, ordinaryMap, assets, Array.Empty<byte>()), Is.EqualTo(expected));
            var guardedMap = new Dictionary<XmlNode, LoadableXmlAsset>();
            var guarded = LoadedModManager.CombineIntoUnifiedXML(assets, guardedMap);
            var guardedOps = Operations();
            XmlReuseRuntime.Execute(ref guarded, ref guardedMap, assets, guardedOps, "explicit-official-sample", profile,
                CacheLaunchPolicy.Current, () => true,
                (xml, map) => { foreach (var op in guardedOps) Assert.That(XmlReuseRuntime.ApplyOne(op, xml), Is.True); }, requireCostBenefit: true);
            Assert.That(XmlReuseRuntime.Status, Does.Contain("Skipped 0").And.Contain("Cache cost exceeded"));
            Assert.That(XmlReuseSnapshot.Encode(guarded, guardedMap, assets, Array.Empty<byte>()), Is.EqualTo(expected));
            var guardedColdMap = new Dictionary<XmlNode, LoadableXmlAsset>();
            var guardedCold = LoadedModManager.CombineIntoUnifiedXML(assets, guardedColdMap);
            var guardedColdOps = Operations();
            string costProfile = Path.Combine(profile, "cost-only");
            XmlReuseRuntime.Execute(ref guardedCold, ref guardedColdMap, assets, guardedColdOps, "explicit-official-sample", costProfile,
                CacheLaunchPolicy.Current, () => true,
                (xml, map) => { foreach (var op in guardedColdOps) Assert.That(XmlReuseRuntime.ApplyOne(op, xml), Is.True); }, requireCostBenefit: true);
            Assert.That(XmlReuseRuntime.Status, Does.Contain("captured no").And.Contain("Cache cost exceeded"));
            Assert.That(Directory.GetFiles(Path.Combine(costProfile, "WakeUp", "XmlPrefix", "v1"), "*.bin"), Is.Empty);
            TestContext.Out.WriteLine("Official source files=" + assets.Count + "; top-level avoided=" + patchNodes.Count
                + "; encoded result bytes=" + expected.Length + "; offline cold ms=" + cold.ToString("F1")
                + "; offline hit ms=" + warm.ToString("F1") + "; offline original patch ms=" + native.ToString("F1")
                + ". .NET Framework offline sample, NOT game timing or mixed-workload admission.");
        }
        finally { DeepProfiler.enabled = profiling; Directory.Delete(profile, true); }
    }

    [TestCase(19, 780, false)]
    [TestCase(150, 100, false)]
    [TestCase(151, 100, true)]
    [TestCase(double.NaN, 1, false)]
    [TestCase(double.PositiveInfinity, 1, false)]
    public void CostAdmissionRequiresMeasuredHeadroom(double saved, double cost, bool expected)
        => Assert.That(XmlReuseRuntime.HasBenefit(saved, cost), Is.EqualTo(expected));

    [Test]
    public void RuntimeScopeStopsBeforeFindModIncludingUnselectedBranches()
    {
        var find = new PatchOperationFindMod();
        var conditional = new PatchOperationConditional();
        AccessTools.Field(typeof(PatchOperationConditional), "nomatch").SetValue(conditional, find);
        Assert.That(XmlReuseOperations.TryCreate(new PatchOperation[] { conditional }, out _, allowFindMod: false), Is.False);
    }

    // Reviewed field decoding for this finite native sample; no arbitrary mod
    // object loader, callbacks, fixture-specific implementation branch or Defs.
    private static PatchOperation ReadOperation(XmlNode node, string file)
    {
        Type type = typeof(PatchOperation).Assembly.GetType("Verse." + node.Attributes!["Class"]!.Value, true)!;
        var op = (PatchOperation)Activator.CreateInstance(type)!; op.sourceFile = file;
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            var field = AccessTools.Field(type, child.Name) ?? throw new InvalidDataException("unreviewed sample field");
            object value = field.FieldType == typeof(XmlContainer) ? new XmlContainer { node = child }
                : field.FieldType == typeof(string) ? child.InnerText
                : throw new InvalidDataException("unreviewed sample field type");
            field.SetValue(op, value);
        }
        return op;
    }
}
