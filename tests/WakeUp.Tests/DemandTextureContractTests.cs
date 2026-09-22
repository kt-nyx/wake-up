// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class DemandTextureContractTests
{
    [TestCase(null, "/")]
    [TestCase("", "/")]
    [TestCase("Things", "Things/")]
    [TestCase("Things/", "Things/")]
    public void ExpandablePrefixRetainsNativeEmptyFolderMeaning(string? path, string expected)
        => Assert.That(DemandExpandableTextureRuntime.NormalizePrefix(path), Is.EqualTo(expected));
    [Test]
    public void OptionalGraphicSupplierContractsArePinned()
    {
        string? root = Environment.GetEnvironmentVariable("WAKE_UP_DEMAND_SUPPLIER_ROOT");
        if (root == null) Assert.Ignore("Requires explicitly selected isolated supplier references.");
        ResolveEventHandler resolver = (_, args) =>
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            return loaded == null ? Assembly.ReflectionOnlyLoad(args.Name) : Assembly.ReflectionOnlyLoadFrom(loaded.Location);
        };
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolver;
        try
        {
            var supplier = Assembly.ReflectionOnlyLoadFrom(Path.Combine(root!, "2023507013/1.6/Assemblies/VEF.dll"));
            void Check(string category, MethodBase[] methods, string[] expected)
            {
                var hashes = methods.Select((method, index) =>
                {
                    Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
                    TestContext.Out.WriteLine("DEMAND_" + category + "_" + index + " " + hash + " " + SemanticMethodIdentity.Signature(method));
                    return hash;
                }).ToArray();
                Assert.That(hashes, Is.EqualTo(expected));
            }
            using (Assert.EnterMultipleScope())
            {
                Check("SUPPLIER", DemandGraphicCompatibility.SupplierTargets(supplier), DemandGraphicCompatibility.Contracts.Select(c => c.Body).ToArray());
                Check("EXPANDABLE", DemandExpandableTextureRuntime.Targets(supplier), DemandExpandableTextureRuntime.Expected);
                Check("NATIVE", DemandGraphicCompatibility.NativeTargets(typeof(Thing).Assembly), DemandGraphicCompatibility.NativeExpected);
                Check("HARMONY", DemandGraphicCompatibility.HarmonyTargets(typeof(Harmony).Assembly), DemandGraphicCompatibility.HarmonyExpected);
            }
        }
        finally { AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= resolver; }
    }
    [Test]
    public void NativeGraphicAndWorkerContractsArePinned()
    {
        var targets = DemandGraphicRuntime.Targets(typeof(ThingDef).Assembly);
        var hashes = targets.Select((method, index) =>
        {
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            TestContext.Out.WriteLine("DEMAND_GRAPHIC_" + index + " " + hash + " " + method);
            return hash;
        }).ToArray();
        Assert.That(hashes, Is.EqualTo(DemandGraphicRuntime.Expected));
    }
}
