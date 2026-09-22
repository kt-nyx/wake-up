// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ParsedLanguageConsumerContractTests
{
    [Test]
    public void PinnedConsumerBodies()
    {
        string? fixture = Environment.GetEnvironmentVariable("WAKE_UP_LANGUAGE_SUPPLIER_ROOT");
        if (fixture == null) Assert.Ignore("Pinned supplier inspection requires the explicit isolated fixture path.");
        var domain = AppDomain.CreateDomain("language-consumer-contracts", null, new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(typeof(Reader).Assembly.Location) });
        try
        {
            var reader = (Reader)domain.CreateInstanceFromAndUnwrap(typeof(Reader).Assembly.Location, typeof(Reader).FullName);
            string[] rows = reader.Read(fixture!);
            File.WriteAllLines(Path.ChangeExtension(Environment.GetEnvironmentVariable("WAKE_UP_LANGUAGE_CONTRACT_OUTPUT")!, "consumers.txt"), rows);
        }
        finally { AppDomain.Unload(domain); }
    }

    public sealed class Reader : MarshalByRefObject
    {
      public string[] Read(string fixture)
      {
        var rows = new System.Collections.Generic.List<string>();
        ResolveEventHandler resolver = (_, args) =>
        {
            var existing = AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (existing != null) return existing;
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            return loaded == null ? Assembly.ReflectionOnlyLoad(args.Name) : Assembly.ReflectionOnlyLoadFrom(loaded.Location);
        };
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolver;
        try
        {
            foreach (var row in new[] {
                new[] { "2890901044/Assemblies/CombatExtended.dll", "CombatExtended.HarmonyCE.Harmony_BackCompatibility_1_5", "Postfix" },
                new[] { "2890901044/Assemblies/CombatExtended.dll", "<PrivateImplementationDetails>", "ComputeStringHash" },
                new[] { "2023507013/1.6/Assemblies/VEF.dll", "VEF.BackwardsCompatibilityMigrationUtility+BackCompatabilityConverter_VEF", "BackCompatibleDefName" },
                new[] { "2023507013/1.6/Assemblies/VEF.dll", "VEF.VanillaExpandedFramework_BackCompatibility_BackCompatibleDefName_Patch", "Postfix" } })
            {
                var assembly = Assembly.ReflectionOnlyLoadFrom(Path.Combine(fixture!, row[0]));
                var method = assembly.GetType(row[1], true)!.GetMethod(row[2], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!;
                Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
                var contract = ParsedLanguageConsumers.Contracts.Single(c => c.TypeName == row[1] && c.Name == row[2]);
                Assert.That(contract.Resolve(assembly), Is.EqualTo(method));
                rows.Add(SemanticMethodIdentity.Signature(method) + "=" + hash);
            }
        }
        finally { AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= resolver; }
        return rows.ToArray();
      }
    }
    private static void DoNothing() { }
    [Test]
    public void RealCePostfixRequiresPostfixPlacementAndItsCurrentDependencyClosure()
    {
        string? fixture = Environment.GetEnvironmentVariable("WAKE_UP_LANGUAGE_SUPPLIER_ROOT");
        if (fixture == null) Assert.Ignore("Requires explicitly selected pinned isolated supplier.");
        var beforeSupplier = new ParsedLanguageConsumers(); beforeSupplier.Validate();
        var ce = Assembly.LoadFrom(Path.Combine(fixture!, "2890901044/Assemblies/CombatExtended.dll"));
        var postfix = ParsedLanguageConsumers.Contracts.Single(c => c.Role == "ce-postfix").Resolve(ce) as MethodInfo;
        var hash = ParsedLanguageConsumers.Contracts.Single(c => c.Role == "ce-string-hash").Resolve(ce);
        var target = AccessTools.Method(typeof(BackCompatibilityConverter_1_5), "BackCompatibleDefName");
        var foreign = new Harmony("wakeup.tests.consumer-closure");
        try
        {
            // Simulate a session whose closure was resolved before CE appeared,
            // even when another test has already loaded the DLL in this host.
            var earlier = (Dictionary<string, MethodBase>)AccessTools.Field(typeof(ParsedLanguageConsumers), "methods").GetValue(beforeSupplier)!;
            earlier.Remove("ce-postfix"); earlier.Remove("ce-string-hash");
            foreign.Patch(target, postfix: new HarmonyMethod(postfix));
            var patch = Harmony.GetPatchInfo(target).Postfixes.Single(p => p.owner == "wakeup.tests.consumer-closure");
            Assert.That(ParsedLanguageConsumers.AllowsPostfix(target, patch), Is.True);
            Assert.Throws<InvalidDataException>(new Action(() => beforeSupplier.Validate()));
            var current = new ParsedLanguageConsumers(); current.Validate();
            foreign.Patch(hash, prefix: new HarmonyMethod(typeof(ParsedLanguageConsumerContractTests), nameof(DoNothing)));
            Assert.Throws<InvalidDataException>(new Action(() => current.Validate()));
            foreign.UnpatchAll("wakeup.tests.consumer-closure");
            foreign.Patch(target, prefix: new HarmonyMethod(postfix));
            patch = Harmony.GetPatchInfo(target).Prefixes.Single(p => p.owner == "wakeup.tests.consumer-closure");
            Assert.That(ParsedLanguageConsumers.AllowsPostfix(target, patch), Is.False);
        }
        finally { foreign.UnpatchAll("wakeup.tests.consumer-closure"); }
    }

}
