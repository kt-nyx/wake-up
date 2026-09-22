// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ParsedLanguageContractTests
{
    [Test]
    public void NativeContractsMatchOriginalBodies()
    {
        var lines = new List<string>();
        var canonicalBodies = new Dictionary<string, string>();
        foreach (MethodBase method in ParsedLanguageContract.Methods())
        {
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason, out string canonical), Is.True, method.Name + ":" + reason);
            using var sha = SHA256.Create();
            hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Mono(canonical)))).Replace("-", "");
            lines.Add(Mono(SemanticMethodIdentity.Signature(method)) + "\t" + hash);
            canonicalBodies.Add(Mono(SemanticMethodIdentity.Signature(method)), Mono(canonical));
        }
        string? output = Environment.GetEnvironmentVariable("WAKE_UP_LANGUAGE_CONTRACT_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            File.WriteAllLines(output, lines, new UTF8Encoding(false));
            File.WriteAllText(Path.ChangeExtension(output, ".json"), System.Text.Json.JsonSerializer.Serialize(canonicalBodies), new UTF8Encoding(false));
        }
        foreach (string line in lines)
        {
            string[] pair = line.Split('\t');
            Assert.That(ParsedLanguageContract.Expected.TryGetValue(pair[0], out string? expected), Is.True, pair[0]);
            Assert.That(pair[1], Is.EqualTo(expected), pair[0]);
        }
    }
    private static string Mono(string value) => value.Replace("System.Private.CoreLib:", "mscorlib:")
        .Replace("System.Linq:", "System.Core:").Replace("System.Linq.Parallel:", "System.Core:").Replace("System.Private.Xml.Linq:", "System.Xml.Linq:")
        .Replace("System.Private.Xml:", "System.Xml:").Replace("System.Collections.NonGeneric:", "mscorlib:")
        .Replace("mscorlib:System.Collections.Generic.HashSet`1", "System.Core:System.Collections.Generic.HashSet`1");

    [Test]
    public void GuardIncludesHiddenParserBodiesAndKeepsAssignmentIndependent()
    {
        MethodBase[] methods = ParsedLanguageContract.Methods();
        Assert.That(methods.Any(m => m.Name == "MoveNext" && m.DeclaringType!.DeclaringType == typeof(DirectXmlLoaderSimple)), Is.True);
        Assert.That(methods.Any(m => m.Name == "MoveNext" && m.DeclaringType!.DeclaringType == typeof(GenText)), Is.True);
        Assert.That(methods.Any(m => m.Name.StartsWith("<LoadData>b__", StringComparison.Ordinal)), Is.True);
        Assert.That(methods.Any(m => m.Name == "InjectIntoDefs" || m.Name == "SetDefFieldAtPath"), Is.False);
    }

    private sealed class ContractDef : Def { }
    private static byte[] Context()
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        ParsedLanguageContract.WriteDefContext(writer, typeof(ContractDef)); writer.Flush();
        return bytes.ToArray();
    }

    [Test]
    public void ContextFollowsActualNameLookupAndRejectsForeignConverter()
    {
        var names = (IDictionary)AccessTools.Field(typeof(DefDatabase<ContractDef>), "defsByName").GetValue(null)!;
        var field = AccessTools.Field(typeof(BackCompatibility), "conversionChain");
        object original = field.GetValue(null)!;
        try
        {
            byte[] empty = Context();
            names.Add("NativeLookupAlias", new ContractDef { defName = "DifferentObjectName" });
            Assert.That(Context(), Is.Not.EqualTo(empty));
            names.Clear(); Assert.That(Context(), Is.EqualTo(empty));
            var changed = new List<BackCompatibilityConverter>(((IEnumerable)original).Cast<BackCompatibilityConverter>());
            changed.Reverse(); field.SetValue(null, changed);
            Assert.Throws<InvalidDataException>((Action)(() => Context()));
        }
        finally { names.Clear(); field.SetValue(null, original); }
    }

    [Test]
    public void ReusedLookupValidatorChecksLaterConverterChanges()
    {
        var validate = ParsedLanguageContract.CreateDefLookupValidator(typeof(ContractDef));
        var field = AccessTools.Field(typeof(BackCompatibility), "conversionChain");
        object original = field.GetValue(null)!;
        try
        {
            validate();
            var changed = new List<BackCompatibilityConverter>(((IEnumerable)original).Cast<BackCompatibilityConverter>());
            changed.Reverse(); field.SetValue(null, changed);
            Assert.Throws<InvalidDataException>(validate);
            field.SetValue(null, original); validate();
        }
        finally { field.SetValue(null, original); }
    }

    [TestCase("Neurotrainer.label")]
    [TestCase("MechSerumNeurotrainer.label")]
    public void RandomLegacyRenameCannotBePersisted(string original)
    {
        var package = new DefInjectionPackage(typeof(ThingDef));
        package.injections.Add("random.label", new DefInjectionPackage.DefInjection { path = "random.label", nonBackCompatiblePath = original });
        Assert.Throws<InvalidDataException>((Action)(() => ParsedLanguageContract.ValidateInjection(package)));
    }
}

