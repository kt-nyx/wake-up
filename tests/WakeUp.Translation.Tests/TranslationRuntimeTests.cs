// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using NUnit.Framework;
using Verse;
using WakeUp;
using Injection = Verse.DefInjectionPackage.DefInjection;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class TranslationRuntimeTests
{
    private object? oldKeys, oldSuggestions;
    private const string Foreign = "WakeUp.Translation.Tests.Foreign";

    [SetUp]
    public void SetUp()
    {
        oldKeys = AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").GetValue(null);
        oldSuggestions = AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").GetValue(null);
        SetKeys(new Dictionary<string, string>());
        AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").SetValue(null, new Dictionary<string, string>());
    }

    [TearDown]
    public void TearDown()
    {
        Disable();
        new Harmony(Foreign).UnpatchAll(Foreign);
        DefDatabase<SampleDef>.Clear();
        AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").SetValue(null, oldKeys);
        AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").SetValue(null, oldSuggestions);
        Assert.That(TranslationRuntime.HasScope, Is.False);
    }

    private static void Disable()
    {
        new Harmony(TranslationRuntime.Owner).UnpatchAll(TranslationRuntime.Owner);
        AccessTools.Field(typeof(TranslationRuntime), "installed").SetValue(null, false);
    }

    private static void Enable()
    {
        // The same IL resolves framework types into System.Private.CoreLib on
        // this host. Verify its Mono-shaped identity before substituting only
        // the expected host fingerprints in this test process.
        MethodBase[] methods = TranslationRuntime.ContractMethods();
        for (int i = 0; i < methods.Length; i++)
        {
            Assert.That(SemanticMethodIdentity.TryHash(methods[i], out string hostHash, out string reason, out string canonical), Is.True, reason);
            Assert.That(MonoHash(canonical), Is.EqualTo(ProductionBodies[i]));
            TranslationRuntime.ExpectedBodies[i] = hostHash;
        }
        string receipt = Path.Combine(TestContext.CurrentContext.TestDirectory, "translation-install.jsonl");
        AccessTools.Field(typeof(TranslationRuntime), "evidencePath").SetValue(null, receipt);
        bool installed = TranslationRuntime.Install();
        Assert.That(installed, Is.True, File.Exists(receipt) ? File.ReadAllText(receipt) : "no receipt");
    }

    private static readonly string[] ProductionBodies = TranslationRuntime.ExpectedBodies.ToArray();
    private static string MonoHash(string canonical)
    {
        canonical = canonical.Replace("System.Private.CoreLib:", "mscorlib:").Replace("System.Linq:", "System.Core:")
            .Replace("System.Private.Xml:", "System.Xml:");
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "");
    }
    private static void SetKeys(Dictionary<string, string> keys)
        => AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").SetValue(null, keys);

    [Test]
    public void ReviewedNativeBodies()
    {
        MethodBase[] methods = TranslationRuntime.ContractMethods();
        var hashes = new List<string>();
        for (int i = 0; i < methods.Length; i++)
        {
            Assert.That(SemanticMethodIdentity.TryHash(methods[i], out string hash, out string reason, out string canonical), Is.True, reason);
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "s03-" + methods[i].Name + "-canonical.txt"), canonical);
            TestContext.Out.WriteLine(methods[i].Name + "=" + MonoHash(canonical) + " (host=" + hash + ")");
            hashes.Add(MonoHash(canonical));
        }
        Assert.That(hashes, Is.EqualTo(ProductionBodies));
    }

    [TestCase("aliases")]
    [TestCase("failed-first")]
    [TestCase("placeholder")]
    [TestCase("fields")]
    [TestCase("lists")]
    [TestCase("missing")]
    [TestCase("dictionary-key")]
    [TestCase("preexisting-successes")]
    public void NativeAssignmentsAndDiagnosticsMatch(string scenario)
    {
        string[] expected = Run(scenario, false);
        string[] actual = Run(scenario, true);
        Assert.That(actual, Is.EqualTo(expected));
    }

    private static string[] Run(string scenario, bool candidate)
    {
        Disable();
        DefDatabase<SampleDef>.Clear();
        SampleDef def = Sample();
        DefDatabase<SampleDef>.Add(def);
        var package = new DefInjectionPackage(typeof(SampleDef));
        SetKeys(new Dictionary<string, string> { ["One.text"] = "Example.text", ["Two.text"] = "Example.text" });
        switch (scenario)
        {
            case "aliases":
                Add(package, "One.text", "winner").nonBackCompatiblePath = "Old.text";
                Add(package, "Two.text", "loser");
                break;
            case "failed-first":
                Add(package, "One.text", "wrong shape").fullListInjection = new List<string> { "wrong shape" };
                Add(package, "Two.text", "successful retry");
                break;
            case "placeholder":
                Add(package, "One.text", "placeholder").isPlaceholder = true;
                Add(package, "Two.text", "successful retry");
                break;
            case "fields":
                Add(package, "Example.TEXT", "case");
                Add(package, "Example.oldText", "alias");
                Add(package, "Example.inner.text", "nested");
                Add(package, "Example.value.inner.text", "value writeback");
                Add(package, "Example.blocked", "no");
                Add(package, "Example.unsaved", "no");
                Add(package, "Example.number", "bad type");
                Add(package, "Example.badField", "bad field");
                break;
            case "lists":
                Add(package, "Example.items.alpha.text", "handle");
                Add(package, "Example.items.0.text", "duplicate");
                Add(package, "Example.items.alpha-1.text", "second handle");
                Add(package, "Example.words.0", "element");
                Add(package, "Example.words", "").fullListInjection = new List<string> { "full", "replacement", "longer" };
                Add(package, "Example.words.2", "last");
                Add(package, "Example.fixedWords", "").fullListInjection = new List<string> { "not allowed" };
                break;
            case "missing":
                Add(package, "Missing.text", "missing");
                break;
            case "dictionary-key":
                Add(package, "One.text", "winner").path = "Two.text";
                Add(package, "Two.text", "loser");
                break;
            case "preexisting-successes":
                var first = Add(package, "One.text", "already");
                first.injected = true;
                first.normalizedPath = "Example.text";
                var second = Add(package, "Example.text", "already2");
                second.injected = true;
                second.normalizedPath = "Example.text";
                Add(package, "Two.text", "loser");
                break;
        }
        if (candidate) Enable();
        package.InjectIntoDefs(false);
        string[] before = Snapshot(def, package);
        // Native successes remain skipped; failures retry and errors refresh.
        package.InjectIntoDefs(true);
        return before.Concat(Snapshot(def, package)).ToArray();
    }

    [Test]
    public void ActualLanguageCallsReachBothPassesAndRetryNewDefinitions()
    {
        Enable();
        var package = new DefInjectionPackage(typeof(SampleDef));
        Add(package, "Example.text", "translated");
        LoadedLanguage language = Language(package);
        long scopes = TranslationRuntime.CompletedScopes;
        language.InjectIntoData_BeforeImpliedDefs();
        Assert.That(package.injections.Values.Single().injected, Is.False);
        Assert.That(package.loadErrors, Is.Empty);
        SampleDef def = Sample();
        DefDatabase<SampleDef>.Add(def);
        language.InjectIntoData_AfterImpliedDefs();
        Assert.That(def.text, Is.EqualTo("translated"));
        Assert.That(TranslationRuntime.CompletedScopes - scopes, Is.EqualTo(2));
        Assert.That(TranslationRuntime.HasScope, Is.False);
        foreach (string method in new[] { "InjectIntoData_BeforeImpliedDefs", "InjectIntoData_AfterImpliedDefs" })
            Assert.That(PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(LoadedLanguage), method))
                .Count(i => i.Calls((MethodInfo)TranslationRuntime.ContractMethods()[0])), Is.EqualTo(1));
        // WakeUpMod has a real feature entry; the two substitutions are present.
        Assert.That(PatchProcessor.GetOriginalInstructions(AccessTools.Constructor(typeof(WakeUpMod), new[] { typeof(ModContentPack) }))
            .Any(i => i.opcode == OpCodes.Ldftn && i.operand is MethodInfo callback
                && PatchProcessor.GetOriginalInstructions(callback).Any(c => c.Calls(AccessTools.Method(typeof(TranslationRuntime), "TryInitialize")))), Is.True);
        foreach (var method in TranslationRuntime.ContractMethods().Take(2))
            Assert.That(Harmony.GetPatchInfo(method).Transpilers.Any(p => p.owner == TranslationRuntime.Owner), Is.True);
    }

    [Test]
    public void PassBoundaryRebuildUsesCurrentRecordsAndNativeStoredPaths()
    {
        string[] Compare(bool candidate)
        {
            Disable();
            DefDatabase<SampleDef>.Clear();
            var def = Sample();
            DefDatabase<SampleDef>.Add(def);
            var package = new DefInjectionPackage(typeof(SampleDef));
            Add(package, "Example.items.alpha.text", "winner");
            Add(package, "Example.items.0.text", "retry");
            if (candidate) Enable();
            package.InjectIntoDefs(false);
            def.items.Reverse(); // Native stored success path remains index zero.
            package.injections["Example.items.alpha.text"].injected = false;
            package.injections["Example.items.0.text"].injection = "changed retry";
            package.InjectIntoDefs(true);
            return Snapshot(def, package);
        }
        Assert.That(Compare(true), Is.EqualTo(Compare(false)));
    }

    [Test]
    public void EnglishAndFallbackRecordsRemainNativeWhenNotSelected()
    {
        var args = UserStartupSelection.Resolve(Array.Empty<string>(), new WakeUpSettings(), TestContext.CurrentContext.WorkDirectory);
        Assert.That(TranslationRuntime.TryInitialize(args), Is.False, "No default changes in S03.");
        Assert.That(TranslationRuntime.TryInitialize(new[] { "--wake-up-translations=on", "--wake-up-mode=baseline", "-savedatafolder=" + TestContext.CurrentContext.WorkDirectory }), Is.False);
        Enable();
        long before = TranslationRuntime.Lookups;
        Language().InjectIntoData_BeforeImpliedDefs();
        Language().InjectIntoData_AfterImpliedDefs();
        Assert.That(TranslationRuntime.Lookups, Is.EqualTo(before), "Empty English language does no map work.");
        var def = Sample();
        DefDatabase<SampleDef>.Add(def);
        var translated = new DefInjectionPackage(typeof(SampleDef));
        Add(translated, "Example.text", "selected language");
        // Missing selected-language fields retain native English definition text.
        Language(translated).InjectIntoData_BeforeImpliedDefs();
        Assert.That(def.text, Is.EqualTo("selected language"));
        Assert.That(def.inner.text, Is.EqualTo("English nested"));
    }

    [Test]
    public void TranslationHeavySampleRemovesQuadraticDuplicateVisits()
    {
        Enable();
        const int count = 256;
        var package = new DefInjectionPackage(typeof(SampleDef));
        for (int i = 0; i < count; i++)
        {
            var def = Sample();
            def.defName = "Example" + i;
            DefDatabase<SampleDef>.Add(def);
            Add(package, def.defName + ".text", "translated");
        }
        long visits = TranslationRuntime.SeedVisits, lookups = TranslationRuntime.Lookups;
        package.InjectIntoDefs(false);
        Assert.That(package.injections.Values.All(i => i.injected), Is.True);
        Assert.That(TranslationRuntime.SeedVisits - visits, Is.EqualTo(count));
        Assert.That(TranslationRuntime.Lookups - lookups, Is.EqualTo(count));
        TestContext.Out.WriteLine($"Unique-target sample: native duplicate loop visits={count * count}; candidate seed visits={count}; candidate lookups={count}; matching loop entries=0.");
    }

    [Test]
    public void LateForeignPatchAndReentrancyRetainNativeAndReleaseState()
    {
        Enable();
        var def = Sample();
        DefDatabase<SampleDef>.Add(def);
        var package = new DefInjectionPackage(typeof(SampleDef));
        Add(package, "Example.text", "translated");
        TranslationRuntime.Begin(package, false, out bool outer);
        TranslationRuntime.Begin(package, false, out bool inner);
        Assert.That(outer, Is.True);
        Assert.That(inner, Is.False);
        long before = TranslationRuntime.Lookups;
        package.InjectIntoDefs(false);
        TranslationRuntime.End(inner);
        TranslationRuntime.End(outer);
        Assert.That(TranslationRuntime.Lookups, Is.EqualTo(before));
        Assert.That(def.text, Is.EqualTo("translated"));
        new Harmony(Foreign).Patch(TranslationRuntime.ContractMethods()[1], prefix: new HarmonyMethod(typeof(TranslationRuntimeTests), nameof(Noop)));
        package.injections.Values.Single().injected = false;
        package.InjectIntoDefs(false);
        Assert.That(TranslationRuntime.Lookups, Is.EqualTo(before));
    }

    [Test]
    public void ExceptionalPackageExitReleasesScope()
    {
        Enable();
        var package = new DefInjectionPackage(typeof(SampleDef));
        package.injections.Add("Example.text", null!);
        Assert.Throws<NullReferenceException>((Action)(() => package.InjectIntoDefs(false)));
        Assert.That(TranslationRuntime.HasScope, Is.False);
        Assert.That(AccessTools.Field(typeof(TranslationRuntime), "ownerThread").GetValue(null), Is.EqualTo(0));
    }

    [Test]
    public void CompetingThreadInvalidatesTheOwningScope()
    {
        Enable();
        var package = new DefInjectionPackage(typeof(SampleDef));
        TranslationRuntime.Begin(package, false, out bool owns);
        try
        {
            Task.Run(() =>
            {
                TranslationRuntime.Begin(package, true, out bool competes);
                Assert.That(competes, Is.False);
                TranslationRuntime.End(competes);
            }).GetAwaiter().GetResult();
            long before = TranslationRuntime.Lookups;
            // A direct native setter in the owner scope (not a nested package).
            DefDatabase<SampleDef>.Add(Sample());
            Add(package, "Example.text", "translated");
            object?[] args = { typeof(SampleDef), "Example.text", "translated", typeof(string), false, "controlled.xml", false, null, null, null, null };
            Assert.That(((MethodInfo)TranslationRuntime.ContractMethods()[1]).Invoke(package, args), Is.True);
            Assert.That(TranslationRuntime.Lookups, Is.EqualTo(before));
        }
        finally { TranslationRuntime.End(owns); }
    }

    [Test]
    public void ChangedInstructionsAreLeftUnmodified()
    {
        foreach (var entry in new[] { (TranslationRuntime.ContractMethods()[0], true), (TranslationRuntime.ContractMethods()[1], false) })
        {
            var code = PatchProcessor.GetOriginalInstructions(entry.Item1);
            code.Insert(0, new CodeInstruction(OpCodes.Nop));
            var result = (entry.Item2 ? TranslationRuntime.RecordTranspiler(code) : TranslationRuntime.DuplicateTranspiler(code)).ToList();
            Assert.That(InstructionComparison.SameInstructions(code, result), Is.True);
        }
    }

    private static void Noop() { }
    private static LoadedLanguage Language(params DefInjectionPackage[] packages)
    {
        // Bypass the texture/icon constructor only; execute the real native passes.
        var language = (LoadedLanguage)FormatterServices.GetUninitializedObject(typeof(LoadedLanguage));
        language.folderName = "English";
        language.defInjections = packages.ToList();
        language.loadErrors = new List<string>();
        AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
        return language;
    }

    private static Injection Add(DefInjectionPackage package, string path, string text)
    {
        var result = new Injection { path = path, nonBackCompatiblePath = path, injection = text, fileSource = "controlled.xml" };
        package.injections.Add(path, result);
        return result;
    }

    private static SampleDef Sample() => new() { defName = "Example" };
    private static string[] Snapshot(SampleDef def, DefInjectionPackage package) => new[] {
        def.text, def.inner.text, def.value.inner.text, def.blocked, def.unsaved,
        string.Join("|", def.words), string.Join("|", def.items.Select(i => i.text)),
        string.Join("\n", package.loadErrors), string.Join("\n", package.loadSyntaxSuggestions),
        string.Join("\n", package.injections.Select(p => p.Key + ":" + p.Value.injected + ":" + p.Value.normalizedPath
            + ":" + p.Value.suggestedPath + ":" + p.Value.replacedString + ":" + string.Join("|", p.Value.replacedList ?? Array.Empty<string>())))
    };

    public sealed class SampleDef : Def
    {
        [LoadAlias("oldText")] public string text = "English";
        public Node inner = new() { text = "English nested" };
        public OuterValue value = new() { inner = new InnerValue { text = "English value" } };
        [NoTranslate] public string blocked = "original blocked";
        [Unsaved] public string unsaved = "original unsaved";
        public int number = 1;
        [TranslationCanChangeCount] public List<string> words = new() { "one", "two" };
        public List<string> fixedWords = new() { "fixed" };
        public List<Node> items = new() { new Node { handle = "alpha", text = "first" }, new Node { handle = "alpha", text = "second" } };
    }
    public sealed class Node { [TranslationHandle] public string handle = ""; public string text = ""; }
    public struct InnerValue { public string text; }
    public struct OuterValue { public InnerValue inner; }
}
