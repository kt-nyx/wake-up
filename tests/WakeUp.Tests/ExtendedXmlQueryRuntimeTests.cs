// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
[NonParallelizable]
public sealed class ExtendedXmlQueryRuntimeTests
{
    private static Assembly Supplier()
    {
        string managed = Environment.GetEnvironmentVariable("WAKE_UP_RIMWORLD_MANAGED_DIR")!;
        string path = Environment.GetEnvironmentVariable("WAKE_UP_XML_EXTENSIONS_ASSEMBLY") ?? Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed))!, "Mods", "2574315206", "1.6", "Assemblies", "XmlExtensions.dll");
        return Assembly.LoadFrom(path);
    }

    [Test]
    public void PhysicalSupplierEagerContractsMatch()
    {
        Assembly assembly = Supplier();
        foreach (SupplierMethodContract contract in ExtendedXmlQueryRuntime.Contracts)
        {
            MethodInfo method = assembly.GetType(contract.TypeName, true)!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(m => m.Name == contract.Name);
            Assert.That(SemanticMethodIdentity.TryHash(method, out string body, out string reason), Is.True, reason);
            TestContext.Out.WriteLine(contract.Role + "|" + SemanticMethodIdentity.Signature(method) + "|" + body);
        }
        Assert.That(SupplierMethodContract.ResolveAll(assembly, ExtendedXmlQueryRuntime.Contracts).Count, Is.EqualTo(2));
    }

    [Test]
    public void EagerAdapterPreservesMultipleNodeIdentityOrderMutationAndNativeErrors()
    {
        Assembly assembly = Supplier();
        Type type = assembly.GetType("XmlExtensions.PatchOperationReplace", true)!;
        object operation = Activator.CreateInstance(type, true)!;
        FieldInfo xpath = AccessTools.Field(type, "xpath");
        FieldInfo nodes = AccessTools.Field(type, "nodes");
        MethodInfo precheck = AccessTools.Method(type, "PreCheck", new[] { typeof(XmlDocument) });
        var document = new XmlDocument();
        document.LoadXml("<Defs><ThingDef><defName>A</defName><items><li>one</li><li>two</li></items></ThingDef></Defs>");
        using var lookup = new ScopedDefLookup(document);
        XmlNodeList? observedList = null;
        bool active = true;
        try
        {
            Assert.That(ExtendedXmlQueryRuntime.Install(assembly, (xml, path) =>
            {
                if (!active || !lookup.TrySelectNodesEager(xml, path, out var result)) return null;
                observedList = result;
                return result;
            }), Is.True, ExtendedXmlQueryRuntime.Reason);
            xpath.SetValue(operation, "Defs/ThingDef[defName='A']/items/li");
            Assert.That(precheck.Invoke(operation, new object[] { document }), Is.True);
            XmlNode[] original = document.SelectNodes("Defs/ThingDef[defName='A']/items/li")!.Cast<XmlNode>().ToArray();
            Assert.That(((IEnumerable)nodes.GetValue(operation)!).Cast<XmlNode>(), Is.EqualTo(original));
            XmlNodeList firstList = observedList!;
            XmlNode first = original[0];
            first.ParentNode!.RemoveChild(first);
            Assert.That(((IEnumerable)nodes.GetValue(operation)!).Cast<XmlNode>(), Is.EqualTo(original), "Native Count completed the list before caller mutation.");
            Assert.That(precheck.Invoke(operation, new object[] { document }), Is.True);
            Assert.That(observedList, Is.Not.SameAs(firstList));
            Assert.That(((IEnumerable)nodes.GetValue(operation)!).Cast<XmlNode>(), Is.EqualTo(new[] { original[1] }));
            Assert.That(ExtendedXmlQueryRuntime.Hits, Is.EqualTo(2));
            active = false;
            Assert.That(precheck.Invoke(operation, new object[] { document }), Is.True);
            Assert.That(ExtendedXmlQueryRuntime.Hits, Is.EqualTo(2));
            Assert.That(ExtendedXmlQueryRuntime.Fallbacks, Is.EqualTo(1));
            active = true;
            xpath.SetValue(operation, "Defs/ThingDef[defName='A']/[");
            TargetInvocationException? failure = Assert.Throws<TargetInvocationException>(new Action(() => { precheck.Invoke(operation, new object[] { document }); }));
            Assert.That(failure!.InnerException, Is.TypeOf<XPathException>());
        }
        finally { ExtendedXmlQueryRuntime.Uninstall(); }
    }

    [TestCase("PreCheck")]
    [TestCase("SelectNodes")]
    public void LaterForeignEffectsForceOriginalHelperPath(string role)
    {
        Assembly assembly = Supplier();
        object operation = Activator.CreateInstance(assembly.GetType("XmlExtensions.PatchOperationReplace", true)!, true)!;
        MethodInfo precheck = AccessTools.Method(operation.GetType(), "PreCheck", new[] { typeof(XmlDocument) });
        AccessTools.Field(operation.GetType(), "xpath").SetValue(operation, "Defs/ThingDef[defName='A']");
        var document = new XmlDocument();
        document.LoadXml("<Defs><ThingDef><defName>A</defName></ThingDef></Defs>");
        using var lookup = new ScopedDefLookup(document);
        var foreign = new Harmony("WakeUp.Tests.ExtendedXml.Foreign");
        MethodBase changed = SupplierMethodContract.ResolveAll(assembly, ExtendedXmlQueryRuntime.Contracts)[role];
        try
        {
            Assert.That(ExtendedXmlQueryRuntime.Install(assembly, (xml, path) => lookup.TrySelectNodesEager(xml, path, out var result) ? result : null), Is.True);
            Assert.That(precheck.Invoke(operation, new object[] { document }), Is.True);
            long hits = ExtendedXmlQueryRuntime.Hits;
            foreignCalls = 0;
            foreign.Patch(changed, prefix: new HarmonyMethod(typeof(ExtendedXmlQueryRuntimeTests), nameof(ForeignEffect)));
            Assert.That(precheck.Invoke(operation, new object[] { document }), Is.True);
            Assert.That(precheck.Invoke(operation, new object[] { document }), Is.True);
            Assert.That(foreignCalls, Is.EqualTo(2));
            Assert.That(ExtendedXmlQueryRuntime.Hits, Is.EqualTo(hits));
            foreign.Unpatch(changed, HarmonyPatchType.All, foreign.Id);
            Assert.That(precheck.Invoke(operation, new object[] { document }), Is.True);
            Assert.That(ExtendedXmlQueryRuntime.Hits, Is.EqualTo(hits + 1));
        }
        finally
        {
            foreign.Unpatch(changed, HarmonyPatchType.All, foreign.Id);
            ExtendedXmlQueryRuntime.Uninstall();
        }
    }

    private static int foreignCalls;
    private static void ForeignEffect() => foreignCalls++;
}
