// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using RimWorld.IO;
using Verse;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ParsedLanguageRuntimeTests
{
    private string root = null!;
    private IDisposable? session;
    private object? state;
    private bool instructionsInstalled;
    private const string PatchOwner = ParsedLanguageRuntime.Owner;
    private const string ConverterOwner = "WakeUp.ParsedLanguage.Tests.ForeignConverter";
    private const string ConverterInput = "C04ScopedConverterInput", ConverterTarget = "C04ScopedConverterTarget";
    private static int converterCalls;
    [SetUp] public void Setup() { instructionsInstalled = false; root = Path.Combine(Path.GetTempPath(), "wakeup-language-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TearDown] public void Cleanup()
    {
        End(); new Harmony(PatchOwner).UnpatchAll(PatchOwner);
        Directory.Delete(root, true);
    }
    private static LoadedLanguage Language()
    {
        var language = (LoadedLanguage)FormatterServices.GetUninitializedObject(typeof(LoadedLanguage));
        language.folderName = "French (Fran\u00e7ais)"; language.loadErrors = new List<string>();
        language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
        language.stringFiles = new Dictionary<string, List<string>>(); language.defInjections = new List<DefInjectionPackage>();
        return language;
    }
    private VirtualFile FileFor(string name)
    {
        string path = Path.Combine(root, name); File.WriteAllText(path, "");
        return AbstractFilesystem.GetDirectory(root).GetFile(name);
    }
    private void Begin(LoadedLanguage language, string context = "selected-fallback-content-v1")
    {
        End();
        if (!instructionsInstalled)
        {
            var nativeHarmony = new Harmony(PatchOwner);
            ParsedInjectionInstructions.Install(nativeHarmony);
            nativeHarmony.Patch(AccessTools.Method(typeof(LoadedLanguage), "LoadFromFile_Keyed"),
                transpiler: new HarmonyMethod(typeof(ParsedLanguageRuntime), "KeyedTranspiler"));
            instructionsInstalled = true;
        }
        var store = new OwnedCacheStore(Path.Combine(root, "store"), ParsedLanguageFormat.MaximumPayload, 64 * 1024 * 1024, CacheLaunchPolicy.Current);
        Type type = typeof(ParsedLanguageRuntime).GetNestedType("Session", BindingFlags.NonPublic)!;
        state = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { language, store, context }, null)!;
        session = (IDisposable)state;
        AccessTools.Field(typeof(ParsedLanguageRuntime), "current").SetValue(null, state);
        AccessTools.Field(typeof(ParsedLanguageRuntime), "guards").SetValue(null, Array.Empty<PublishedPatchGuard>());
        AccessTools.Field(typeof(ParsedLanguageRuntime), "injectionNameGuards").SetValue(null, Array.Empty<PublishedPatchGuard>());
    }
    private void End() { AccessTools.Field(typeof(ParsedLanguageRuntime), "current").SetValue(null, null); session?.Dispose(); session = null; state = null; }
    private int Counter(string name) => (int)AccessTools.Field(state!.GetType(), name).GetValue(state)!;

    [Test]
    public void InjectionInstructionsReplayNativeConflictsAndOldSyntaxAgainstChangedPredecessors()
    {
        var file = FileFor("insertion.xml");
        const string text = "<LanguageData><rep><path>FixtureInstruction.label</path><trans>new\\nvalue</trans></rep><FixtureInstruction.items><li>one</li><!--comment--><li>two</li></FixtureInstruction.items></LanguageData>";
        var files = new List<VirtualFile> { file }; var texts = new List<string> { text };
        var cold = Language(); Begin(cold); ParsedLanguageRuntime.InjectionGroup(cold, files, texts, typeof(RecipeDef));
        Assert.That(Counter("Published"), Is.EqualTo(1));
        Assert.That(Counter("NativeInsertions"), Is.EqualTo(2));
        var original = cold.defInjections.Single();
        Assert.That(original.usedOldRepSyntax, Is.True);
        Assert.That(original.injections["FixtureInstruction.label"].injection, Is.EqualTo("new\nvalue"));
        foreach (bool conflict in new[] { false, true })
        {
            var warm = Language();
            var before = new DefInjectionPackage(typeof(RecipeDef)); warm.defInjections.Add(before);
            var prior = new DefInjectionPackage.DefInjection { path = "FixtureInstruction.label", nonBackCompatiblePath = "FixtureInstruction.label", injection = "earlier", fileSource = "earlier.xml" };
            before.injections.Add(prior.path, prior);
            if (conflict) before.injections.Add("FixtureInstruction.items.0", new DefInjectionPackage.DefInjection { path = "FixtureInstruction.items.0", nonBackCompatiblePath = "FixtureInstruction.items.0", injection = "kept" });
            Begin(warm); ParsedLanguageRuntime.InjectionGroup(warm, files, texts, typeof(RecipeDef));
            Assert.That(Counter("InjectionHits"), Is.EqualTo(1), "Changed predecessor state must execute native decisions, without invalidating source interpretation.");
            Assert.That(Counter("NativeInsertions"), Is.EqualTo(2));
            Assert.That(before.injections[prior.path], Is.Not.SameAs(prior));
            Assert.That(before.loadErrors.Any(e => e.StartsWith("Duplicate def-injected translation key:")), Is.True);
            Assert.That(before.usedOldRepSyntax, Is.True);
            Assert.That(before.injections.ContainsKey("FixtureInstruction.items"), Is.EqualTo(!conflict));
            if (!conflict)
            {
                var restored = before.injections["FixtureInstruction.items"];
                Assert.That(restored.fullListInjection, Is.EqualTo(new[] { "one", "two" }));
                Assert.That(restored.fullListInjection, Is.Not.SameAs(original.injections["FixtureInstruction.items"].fullListInjection));
                Assert.That(restored.fullListInjectionComments.Single().First, Is.EqualTo(1));
                Assert.That(restored.fullListInjectionComments.Single().Second, Is.EqualTo("comment"));
            }
            else Assert.That(before.loadErrors.Any(e => e.Contains("whole list and individual elements")), Is.True);
        }
    }

    [Test]
    public void InjectionInstructionReplayKeepsNativePartialFailureAndMalformedSourceFallback()
    {
        var file = FileFor("lists.xml");
        const string text = "<LanguageData><FixtureInstruction.items><li>value</li></FixtureInstruction.items><FixtureInstruction.after>later</FixtureInstruction.after></LanguageData>";
        var files = new List<VirtualFile> { file }; var texts = new List<string> { text };
        var cold = Language(); Begin(cold); ParsedLanguageRuntime.InjectionGroup(cold, files, texts, typeof(RecipeDef));
        var warm = Language(); var package = new DefInjectionPackage(typeof(RecipeDef)); warm.defInjections.Add(package);
        package.injections.Add("FixtureInstruction.items", new DefInjectionPackage.DefInjection { path = "FixtureInstruction.items", fullListInjection = new List<string> { "previous" } });
        Begin(warm); ParsedLanguageRuntime.InjectionGroup(warm, files, texts, typeof(RecipeDef));
        Assert.That(Counter("InjectionHits"), Is.EqualTo(1));
        Assert.That(Counter("NativeInsertions"), Is.EqualTo(1), "A native full-list duplicate throws; replay must not continue or retry.");
        Assert.That(warm.anyDefInjectionsXmlParseError, Is.True);
        Assert.That(package.injections.ContainsKey("FixtureInstruction.after"), Is.False);
        Assert.That(package.injections["FixtureInstruction.items"].fullListInjection, Is.EqualTo(new[] { "previous" }));
        var malformed = Language(); Begin(malformed);
        ParsedLanguageRuntime.InjectionGroup(malformed, files, new List<string> { "<LanguageData><A.label>value</A.label><broken>" }, typeof(RecipeDef));
        Assert.That(malformed.anyDefInjectionsXmlParseError, Is.True);
        Assert.That(Counter("InjectionHits"), Is.Zero); Assert.That(Counter("Published"), Is.Zero);
    }

    [Test]
    public void InjectionInstructionsUseCurrentNativeNamesAndAliasesWithoutPredictingThem()
    {
        var names = (IDictionary)AccessTools.Field(typeof(DefDatabase<ThingDef>), "defsByName").GetValue(null)!;
        const string sourceName = "Gun_Pistol";
        object? old = names.Contains(sourceName) ? names[sourceName] : null;
        bool existed = names.Contains(sourceName);
        try
        {
            names.Remove(sourceName);
            var file = FileFor("name.xml"); var files = new List<VirtualFile> { file };
            var texts = new List<string> { "<LanguageData><Gun_Pistol.label>same source</Gun_Pistol.label></LanguageData>" };
            var cold = Language(); Begin(cold); ParsedLanguageRuntime.InjectionGroup(cold, files, texts, typeof(ThingDef));
            Assert.That(cold.defInjections.Single().injections.ContainsKey("Gun_Revolver.label"), Is.True);
            var alias = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef)); alias.defName = "DifferentActualName";
            names.Add(sourceName, alias);
            var warm = Language(); Begin(warm); ParsedLanguageRuntime.InjectionGroup(warm, files, texts, typeof(ThingDef));
            Assert.That(Counter("InjectionHits"), Is.EqualTo(1));
            Assert.That(warm.defInjections.Single().injections.ContainsKey("Gun_Pistol.label"), Is.True);
            Assert.That(warm.defInjections.Single().injections.ContainsKey("Gun_Revolver.label"), Is.False);
            names.Remove(sourceName);
            var again = Language(); Begin(again); ParsedLanguageRuntime.InjectionGroup(again, files, texts, typeof(ThingDef));
            Assert.That(Counter("InjectionHits"), Is.EqualTo(1));
            Assert.That(again.defInjections.Single().injections.ContainsKey("Gun_Revolver.label"), Is.True);
            texts[0] = texts[0].Replace("same source", "edit source");
            var changed = Language(); Begin(changed); ParsedLanguageRuntime.InjectionGroup(changed, files, texts, typeof(ThingDef));
            Assert.That(Counter("InjectionHits"), Is.Zero, "Equal-length source edits must change the interpretation key.");
            Assert.That(changed.defInjections.Single().injections["Gun_Revolver.label"].injection, Is.EqualTo("edit source"));
        }
        finally { names.Remove(sourceName); if (existed) names.Add(sourceName, old); }
    }

    [Test]
    public void NativeKeyedRowsUseCurrentWinnersAndRejectOnlyChangedSource()
    {
        var files = new List<VirtualFile> { FileFor("one.xml"), FileFor("two.xml") };
        var texts = new List<string> { "<LanguageData><Hello>Bonjour</Hello><Empty>TODO</Empty></LanguageData>", "<LanguageData><Hello>Salut</Hello></LanguageData>" };
        var cold = Language(); Begin(cold); ParsedLanguageRuntime.KeyedGroup(cold, files, texts);
        Assert.That(Counter("Published"), Is.EqualTo(2));
        Assert.That(cold.keyedReplacements["Hello"].value, Is.EqualTo("Salut"));
        var warm = Language(); Begin(warm); ParsedLanguageRuntime.KeyedGroup(warm, files, texts);
        Assert.That(Counter("KeyedHits"), Is.EqualTo(2));
        Assert.That(ParsedLanguageFormat.EncodeKeyed(warm.keyedReplacements), Is.EqualTo(ParsedLanguageFormat.EncodeKeyed(cold.keyedReplacements)));
        Assert.That(warm.keyedReplacements["Hello"], Is.Not.SameAs(cold.keyedReplacements["Hello"]));
        texts[1] = texts[1].Replace("Salut", "Adieu");
        var changed = Language(); Begin(changed); ParsedLanguageRuntime.KeyedGroup(changed, files, texts);
        Assert.That(Counter("KeyedHits"), Is.EqualTo(1)); Assert.That(changed.keyedReplacements["Hello"].value, Is.EqualTo("Adieu"));
        var predecessor = Language(); predecessor.keyedReplacements.Add("Earlier", new LoadedLanguage.KeyedReplacement { key = "Earlier", value = "kept" });
        Begin(predecessor); ParsedLanguageRuntime.KeyedGroup(predecessor, files, texts);
        Assert.That(Counter("KeyedHits"), Is.EqualTo(2)); Assert.That(predecessor.keyedReplacements.ContainsKey("Earlier"), Is.True);
    }

    [Test]
    public void LaterKeyedGroupPreservesDictionaryAndUntouchedRecordsOnColdAndWarmLoads()
    {
        var earlierFiles = new List<VirtualFile> { FileFor("earlier.xml") };
        var laterFiles = new List<VirtualFile> { FileFor("later.xml") };
        var earlierText = new List<string> { "<LanguageData><Untouched>kept</Untouched><Replaced>same text</Replaced></LanguageData>" };
        var laterText = new List<string> { "<LanguageData><Replaced>same text</Replaced><Added>new</Added></LanguageData>" };
        LoadedLanguage? cold = null;
        for (int pass = 0; pass < 2; pass++)
        {
            var language = Language();
            var dictionary = language.keyedReplacements;
            Begin(language);
            ParsedLanguageRuntime.KeyedGroup(language, earlierFiles, earlierText);
            var untouched = dictionary["Untouched"];
            var replaced = dictionary["Replaced"];
            ParsedLanguageRuntime.KeyedGroup(language, laterFiles, laterText);

            Assert.That(language.keyedReplacements, Is.SameAs(dictionary));
            Assert.That(dictionary["Untouched"], Is.SameAs(untouched));
            Assert.That(dictionary["Replaced"], Is.Not.SameAs(replaced), "Native assignment creates a new record even when the text is equal.");
            Assert.That(dictionary["Replaced"].value, Is.EqualTo(replaced.value));
            Assert.That(dictionary["Added"].value, Is.EqualTo("new"));
            Assert.That(Counter("KeyedHits"), Is.EqualTo(pass == 0 ? 0 : 2));
            Assert.That(Counter("Published"), Is.EqualTo(pass == 0 ? 2 : 0));
            if (cold == null) cold = language;
            else
            {
                Assert.That(ParsedLanguageFormat.EncodeKeyed(dictionary), Is.EqualTo(ParsedLanguageFormat.EncodeKeyed(cold.keyedReplacements)));
                Assert.That(dictionary["Added"], Is.Not.SameAs(cold.keyedReplacements["Added"]));
            }
        }
    }

    [Test]
    public void CachedDuplicateRowsStillWarnAndMalformedKeyedFilesRemainNative()
    {
        var files = new List<VirtualFile> { FileFor("duplicate.xml"), FileFor("malformed.xml") };
        var texts = new List<string> { "<LanguageData><A>first</A><A>second</A></LanguageData>", "<LanguageData><B>partial</B><broken>" };
        for (int i = 0; i < 2; i++)
        {
            var language = Language(); Begin(language); ParsedLanguageRuntime.KeyedGroup(language, files, texts);
            Assert.That(language.keyedReplacements["A"].value, Is.EqualTo("first"));
            Assert.That(language.keyedReplacements.ContainsKey("B"), Is.False);
            Assert.That(language.loadErrors, Has.Count.EqualTo(2)); Assert.That(language.anyKeyedReplacementsXmlParseError, Is.True);
            Assert.That(Counter("Published"), Is.EqualTo(i == 0 ? 1 : 0)); Assert.That(Counter("KeyedHits"), Is.EqualTo(i));
        }
    }

    [Test]
    public void RealStringsMethodKeepsReadCatchAndNativeLineParsingWithWarmConsumption()
    {
        var method = AccessTools.Method(typeof(LoadedLanguage), "LoadFromFile_Strings");
        var harmony = new Harmony(PatchOwner);
        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(ParsedLanguageRuntime), "StringsTranspiler"));
        var file = FileFor("names.txt"); File.WriteAllText(Path.Combine(root, "names.txt"), "one\n two // comment\n//only\n\u4e09\n");
        var directory = AbstractFilesystem.GetDirectory(root);
        var cold = Language(); Begin(cold); method.Invoke(cold, new object[] { file, directory });
        Assert.That(Counter("Published"), Is.EqualTo(1));
        var warm = Language(); Begin(warm); method.Invoke(warm, new object[] { file, directory });
        Assert.That(Counter("StringsHits"), Is.EqualTo(1));
        Assert.That(warm.stringFiles["names"], Is.EqualTo(cold.stringFiles["names"]));
        Assert.That(warm.stringFiles["names"], Is.Not.SameAs(cold.stringFiles["names"]));
        using var locked = new FileStream(Path.Combine(root, "names.txt"), FileMode.Open, FileAccess.Read, FileShare.None);
        var failed = Language(); Begin(failed); method.Invoke(failed, new object[] { file, directory });
        Assert.That(failed.loadErrors, Has.Count.EqualTo(1)); Assert.That(failed.stringFiles, Is.Empty); Assert.That(Counter("StringsHits"), Is.Zero);
    }

    [Test]
    public void InjectionNameContractsExcludeSharedKeyedAndStringsParsing()
    {
        Assert.That(ParsedLanguageContract.IsInjectionNameContract(AccessTools.Method(typeof(BackCompatibilityConverter_1_5), "BackCompatibleDefName")), Is.True);
        Assert.That(ParsedLanguageContract.IsInjectionNameContract(AccessTools.Method(typeof(BackCompatibilityConverter), "BackCompatibleDefName")), Is.True);
        Assert.That(ParsedLanguageContract.IsInjectionNameContract(AccessTools.Method(typeof(BackCompatibility), "BackCompatibleDefName")), Is.True);
        Assert.That(ParsedLanguageContract.IsInjectionNameContract(AccessTools.Method(typeof(GenDefDatabase), "GetDefSilentFail")), Is.True);
        Assert.That(ParsedLanguageContract.IsInjectionNameContract(AccessTools.Method(typeof(DefDatabase<>), "GetNamedSilentFail")), Is.True);
        foreach (string name in new[] { "LoadData", "LoadFromFile_Keyed", "LoadFromFile_Strings", "TryRegisterFileIfNew" })
            Assert.That(ParsedLanguageContract.IsInjectionNameContract(AccessTools.Method(typeof(LoadedLanguage), name)), Is.False, name);
        Assert.That(ParsedLanguageContract.IsInjectionNameContract(AccessTools.Method(typeof(DirectXmlLoaderSimple), "ValuesFromXmlFile", new[] { typeof(string) })), Is.False);
    }

    [Test]
    public void ForeignNativeConverterKeepsInjectionCallbacksWhileKeyedAndStringsReuseWarmData()
    {
        var converter = AccessTools.Method(typeof(BackCompatibilityConverter_1_5), "BackCompatibleDefName");
        Assert.That(PublishedPatchGuard.TryCreate(converter, PatchOwner, out var nameGuard, true), Is.True);
        Assert.That(nameGuard!.AllowsOriginalContract(), Is.True);
        var foreign = new Harmony(ConverterOwner);
        var names = (IDictionary)AccessTools.Field(typeof(DefDatabase<ThingDef>), "defsByName").GetValue(null)!;
        var tKeys = AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey");
        var suggestions = AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey");
        object? oldKeys = tKeys.GetValue(null), oldSuggestions = suggestions.GetValue(null);
        Assert.That(names.Contains(ConverterInput) || names.Contains(ConverterTarget), Is.False, "The converter test uses unique private names.");
        try
        {
            Assert.That(Scribe.mode, Is.EqualTo(LoadSaveMode.Inactive));
            tKeys.SetValue(null, new Dictionary<string, string>());
            suggestions.SetValue(null, new Dictionary<string, string>());
            // Only native Def field assignment is under test; constructing a
            // ThingDef also requests Unity graphics resources outside the game.
            var target = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            target.defName = ConverterTarget; target.label = "original";
            names.Add(ConverterTarget, target);
            converterCalls = 0;
            foreign.Patch(converter, postfix: new HarmonyMethod(typeof(ParsedLanguageRuntimeTests), nameof(RenamePrivateDef)));
            Assert.That(nameGuard.AllowsOriginalContract(), Is.False);

            var stringsMethod = AccessTools.Method(typeof(LoadedLanguage), "LoadFromFile_Strings");
            new Harmony(PatchOwner).Patch(stringsMethod,
                transpiler: new HarmonyMethod(typeof(ParsedLanguageRuntime), "StringsTranspiler"));
            var keyedFiles = new List<VirtualFile> { FileFor("converter-keyed.xml") };
            var keyedText = new List<string> { "<LanguageData><Hello>Bonjour</Hello></LanguageData>" };
            var injectionFiles = new List<VirtualFile> { FileFor("converter-injection.xml") };
            var injectionText = new List<string> { "<LanguageData><" + ConverterInput + ".label>traduit</" + ConverterInput + ".label></LanguageData>" };
            var stringsFile = FileFor("converter-words.txt");
            File.WriteAllText(Path.Combine(root, "converter-words.txt"), "one\ntwo\n");
            var directory = AbstractFilesystem.GetDirectory(root);
            for (int pass = 0; pass < 2; pass++)
            {
                var language = Language(); Begin(language);
                AccessTools.Field(typeof(ParsedLanguageRuntime), "injectionNameGuards").SetValue(null, new[] { nameGuard });
                ParsedLanguageRuntime.KeyedGroup(language, keyedFiles, keyedText);
                ParsedLanguageRuntime.InjectionGroup(language, injectionFiles, injectionText, typeof(ThingDef));
                stringsMethod.Invoke(language, new object[] { stringsFile, directory });

                Assert.That(converterCalls, Is.EqualTo(pass + 1), "The real native converter callback must execute on every injection parse.");
                var package = language.defInjections.Single();
                Assert.That(package.loadErrors, Is.Empty);
                Assert.That(package.injections.Keys, Is.EqualTo(new[] { ConverterTarget + ".label" }), "The foreign native rename must reach the parsed injection path.");
                Assert.That(package.injections[ConverterTarget + ".label"].injected, Is.False);
                target.label = "original";
                package.InjectIntoDefs(true);
                Assert.That(target.label, Is.EqualTo("traduit"), "Native application must consume the callback-renamed record.");
                Assert.That(package.injections[ConverterTarget + ".label"].injected, Is.True);
                Assert.That(language.keyedReplacements["Hello"].value, Is.EqualTo("Bonjour"));
                Assert.That(language.stringFiles["converter-words"], Is.EqualTo(new[] { "one", "two" }));
                Assert.That(Counter("InjectionHits"), Is.Zero);
                Assert.That(Counter("KeyedHits"), Is.EqualTo(pass));
                Assert.That(Counter("StringsHits"), Is.EqualTo(pass));
                Assert.That(Counter("Published"), Is.EqualTo(pass == 0 ? 2 : 0));
                Assert.That(Counter("NativeFiles"), Is.EqualTo(pass == 0 ? 3 : 1));
            }
        }
        finally
        {
            End(); foreign.UnpatchAll(ConverterOwner);
            AccessTools.Field(typeof(ParsedLanguageRuntime), "injectionNameGuards").SetValue(null, Array.Empty<PublishedPatchGuard>());
            names.Remove(ConverterTarget);
            tKeys.SetValue(null, oldKeys); suggestions.SetValue(null, oldSuggestions);
        }
    }

    private static void RenamePrivateDef(Type __0, string __1, bool __2, ref string __result)
    {
        if (__0 != typeof(ThingDef) || __1 != ConverterInput || !__2) return;
        converterCalls++;
        __result = ConverterTarget;
    }

    [Test]
    public void RealLoadDataRewriterReplacesExactlyTheTwoIndexedParserLoops()
    {
        var original = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(LoadedLanguage), "LoadData"));
        var changed = ParsedLanguageRuntime.LoadTranspiler(original).ToList();
        Assert.That(changed.Count(c => c.Calls(AccessTools.Method(typeof(ParsedLanguageRuntime), "KeyedGroup"))), Is.EqualTo(1));
        Assert.That(changed.Count(c => c.Calls(AccessTools.Method(typeof(ParsedLanguageRuntime), "InjectionGroup"))), Is.EqualTo(1));
        Assert.That(changed.Count(c => c.Calls(AccessTools.Method(typeof(LoadedLanguage), "LoadFromFile_Strings"))), Is.EqualTo(1));
        // Building the real dynamic replacement validates labels and exception
        // regions without executing Unity or measuring work.
        new Harmony(PatchOwner).Patch(AccessTools.Method(typeof(LoadedLanguage), "LoadData"),
            transpiler: new HarmonyMethod(typeof(ParsedLanguageRuntime), "LoadTranspiler"));
    }

    [Test]
    public void StringsAppendPreservesExistingDictionaryAndListsOnColdAndWarmLoads()
    {
        var method = AccessTools.Method(typeof(LoadedLanguage), "LoadFromFile_Strings");
        new Harmony(PatchOwner).Patch(method,
            transpiler: new HarmonyMethod(typeof(ParsedLanguageRuntime), "StringsTranspiler"));
        var file = FileFor("names.txt");
        File.WriteAllText(Path.Combine(root, "names.txt"), "next\n last // comment\n");
        var directory = AbstractFilesystem.GetDirectory(root);
        List<string>? coldNames = null;
        for (int pass = 0; pass < 2; pass++)
        {
            var language = Language();
            var dictionary = language.stringFiles;
            var prefix = pass == 0 ? "earlier" : "changed predecessor";
            var names = new List<string> { prefix };
            var untouched = new List<string> { "kept" };
            dictionary.Add("names", names); dictionary.Add("other", untouched);
            Begin(language); method.Invoke(language, new object[] { file, directory });

            Assert.That(language.stringFiles, Is.SameAs(dictionary));
            Assert.That(dictionary["names"], Is.SameAs(names));
            Assert.That(dictionary["other"], Is.SameAs(untouched));
            Assert.That(names, Is.EqualTo(new[] { prefix, "next", "last " }));
            Assert.That(untouched, Is.EqualTo(new[] { "kept" }));
            Assert.That(Counter("StringsHits"), Is.EqualTo(pass));
            Assert.That(Counter("Published"), Is.EqualTo(pass == 0 ? 1 : 0));
            if (coldNames == null) coldNames = names;
            else
            {
                names.Add("warm owner only");
                Assert.That(coldNames, Is.EqualTo(new[] { "earlier", "next", "last " }));
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LanguageTranspilersLeavePreviouslyModifiedInstructionsUntouched(bool strings)
    {
        var method = AccessTools.Method(typeof(LoadedLanguage), strings ? "LoadFromFile_Strings" : "LoadData");
        var incoming = PatchProcessor.GetOriginalInstructions(method).Select(c => new CodeInstruction(c)).ToList();
        // A valid earlier instrumentation change must survive intact, rather
        // than being erased or combined with an assumed native parser shape.
        incoming.Insert(0, new CodeInstruction(OpCodes.Nop));
        var generator = new DynamicMethod("ModifiedLanguageInstructions", typeof(void), Type.EmptyTypes).GetILGenerator();
        var result = (strings ? ParsedLanguageRuntime.StringsTranspiler(incoming, generator)
            : ParsedLanguageRuntime.LoadTranspiler(incoming)).ToList();
        Assert.That(result.Count, Is.EqualTo(incoming.Count));
        for (int i = 0; i < incoming.Count; i++)
        {
            Assert.That(result[i].opcode, Is.EqualTo(incoming[i].opcode));
            Assert.That(result[i].operand, Is.EqualTo(incoming[i].operand));
            Assert.That(result[i].labels, Is.EqualTo(incoming[i].labels));
            Assert.That(result[i].blocks, Is.EqualTo(incoming[i].blocks));
        }
        Assert.That(result.Any(c => c.Calls(AccessTools.Method(typeof(ParsedLanguageRuntime), "KeyedGroup"))
            || c.Calls(AccessTools.Method(typeof(ParsedLanguageRuntime), "InjectionGroup"))
            || c.Calls(AccessTools.Method(typeof(ParsedLanguageRuntime), "StringLines"))), Is.False);
    }

    private ParsedLanguageKeyBuffer KeyBuffer => (ParsedLanguageKeyBuffer)AccessTools.Field(state!.GetType(), "KeyBuffer").GetValue(state)!;
    private string GroupKey(string kind, LoadedLanguage language, VirtualFile[] files, string[] texts, byte[] before, Type? type = null)
        => (string)AccessTools.Method(typeof(ParsedLanguageRuntime), "Key").Invoke(null, new object?[] { kind, language, files, texts, before, type, true })!;
    private static string FileKey(string kind, VirtualFile file, string text)
        => (string)AccessTools.Method(typeof(ParsedLanguageRuntime), "SingleFileKey").Invoke(null, new object[] { kind, file, text })!;
    // Independent old serialization: retain ToArray here as the byte oracle.
    private static byte[] OldKeyBytes(string context, string kind, VirtualFile[] files, string[] texts, byte[] before, Type? type = null)
    {
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true);
        using var sha = System.Security.Cryptography.SHA256.Create();
        writer.Write(context); writer.Write(kind); writer.Write(BitConverter.ToString(sha.ComputeHash(before)).Replace("-", "")); writer.Write(files.Length);
        for (int i = 0; i < files.Length; i++) { writer.Write(files[i].FullPath); writer.Write(files[i].Name); writer.Write(texts[i]); }
        if (type != null) { writer.Write(type.AssemblyQualifiedName!); writer.Write(type.Module.ModuleVersionId.ToString()); }
        writer.Flush(); return bytes.ToArray();
    }
    private static string OldDigest(byte[] bytes)
    { using var sha = System.Security.Cryptography.SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }

    [Test]
    public void SourceKeysRetainCanonicalBytesForAllFamiliesAndUtf8LengthBoundaries()
    {
        var language = Language(); const string context = "Fran\u00e7ais/\u65e5\u672c\u8a9e"; Begin(language, context);
        var file = FileFor("\u00e9-\u6587.txt"); var files = new[] { file }; var empty = Array.Empty<byte>();
        foreach (var kind in new[] { "native-keyed-rows-v1", "native-string-lines-v1", "injection-native-operations-v1" })
        foreach (var text in new[] { "", new string('a', 127), new string('b', 128), new string('c', 16384), "\u00e9\u6f22\U0001F600\r\n\0", "\ud800", "short" })
        {
            Type? type = kind.StartsWith("injection") ? typeof(RecipeDef) : null;
            var expected = OldKeyBytes(context, kind, files, new[] { text }, empty, type);
            Assert.That(GroupKey(kind, language, files, new[] { text }, empty, type), Is.EqualTo(OldDigest(expected)));
            var scratch = (MemoryStream)AccessTools.Field(typeof(ParsedLanguageKeyBuffer), "stream").GetValue(KeyBuffer)!;
            Assert.That(scratch.GetBuffer().Take(expected.Length), Is.EqualTo(expected));
            Assert.That(scratch.Length, Is.Zero); Assert.That(scratch.Position, Is.Zero);
            if (type == null) Assert.That(FileKey(kind, file, text), Is.EqualTo(OldDigest(expected)));
        }
        var other = FileFor("\u00e0-\u6587.txt"); var pair = new[] { file, other }; var values = new[] { "abc", "def" }; var prior = new byte[] { 0, 255, 7 };
        string original = GroupKey("injection-native-operations-v1", language, pair, values, prior, typeof(RecipeDef));
        Assert.That(original, Is.EqualTo(OldDigest(OldKeyBytes(context, "injection-native-operations-v1", pair, values, prior, typeof(RecipeDef)))));
        Assert.That(GroupKey("injection-native-operations-v1", language, pair, new[] { "abd", "def" }, prior, typeof(RecipeDef)), Is.Not.EqualTo(original));
        Assert.That(GroupKey("injection-native-operations-v1", language, pair.Reverse().ToArray(), values, prior, typeof(RecipeDef)), Is.Not.EqualTo(original));
        Assert.That(FileKey("native-string-lines-v1", file, "same"), Is.Not.EqualTo(FileKey("native-string-lines-v1", other, "same")));
    }

    [Test]
    public void SourceKeyScratchDropsLargeFailedInvalidatedAndDisposedStorage()
    {
        var language = Language(); Begin(language); var file = FileFor("bound.txt"); var buffer = KeyBuffer;
        string maximum = new string('a', 16 * 1024 * 1024);
        Assert.That(FileKey("native-string-lines-v1", file, maximum), Is.EqualTo(OldDigest(OldKeyBytes("selected-fallback-content-v1", "native-string-lines-v1", new[] { file }, new[] { maximum }, Array.Empty<byte>()))));
        Assert.That(buffer.RetainedCapacity, Is.Zero);
        Assert.That(FileKey("native-string-lines-v1", file, ""), Is.EqualTo(OldDigest(OldKeyBytes("selected-fallback-content-v1", "native-string-lines-v1", new[] { file }, new[] { "" }, Array.Empty<byte>()))));
        Assert.That(buffer.RetainedCapacity, Is.InRange(1, ParsedLanguageKeyBuffer.MaximumRetainedCapacity));
        Assert.That(Assert.Throws<TargetInvocationException>(new Action(() => FileKey("x", file, maximum + "x")))!.InnerException, Is.TypeOf<InvalidDataException>());
        Assert.That(Assert.Throws<TargetInvocationException>(new Action(() => FileKey("x", file, null!)))!.InnerException, Is.TypeOf<NullReferenceException>());
        Assert.That(Assert.Throws<TargetInvocationException>(new Action(() => GroupKey("x", language, new[] { file }, Array.Empty<string>(), Array.Empty<byte>())))!.InnerException, Is.TypeOf<InvalidDataException>());
        Assert.That(Assert.Throws<TargetInvocationException>(new Action(() => GroupKey("x", language, new VirtualFile[8193], new string[8193], Array.Empty<byte>())))!.InnerException, Is.TypeOf<InvalidDataException>());
        // A failure after serialization starts must drop scratch, then allow an
        // ordinary key. It must not hash old bytes or keep a broken writer.
        Assert.That(Assert.Throws<TargetInvocationException>(new Action(() => FileKey(null!, file, "text")))!.InnerException, Is.TypeOf<ArgumentNullException>());
        Assert.That(buffer.RetainedCapacity, Is.Zero);
        FileKey("x", file, "ok"); AccessTools.Method(state!.GetType(), "Invalidate").Invoke(state, null);
        Assert.That(buffer.RetainedCapacity, Is.Zero);
        End(); Assert.That(buffer.RetainedCapacity, Is.Zero);
        Assert.Throws<ObjectDisposedException>(new Action(() => buffer.Begin()));
    }

    [Test]
    public void InjectionKeyStillValidatesEachUseAndUnwindsAfterValidatorFailure()
    {
        var language = Language(); Begin(language); var file = FileFor("guard.xml"); int calls = 0;
        var validators = (Dictionary<Type, Action>)AccessTools.Field(state!.GetType(), "LookupValidators").GetValue(state)!;
        validators.Add(typeof(RecipeDef), () => calls++);
        for (int i = 0; i < 2; i++) GroupKey("injection-native-operations-v1", language, new[] { file }, new[] { "text" }, Array.Empty<byte>(), typeof(RecipeDef));
        Assert.That(calls, Is.EqualTo(2));
        validators[typeof(RecipeDef)] = () => throw new InvalidDataException("changed-validator");
        Assert.That(Assert.Throws<TargetInvocationException>(new Action(() => GroupKey("injection-native-operations-v1", language, new[] { file }, new[] { "text" }, Array.Empty<byte>(), typeof(RecipeDef))))!.InnerException!.Message, Is.EqualTo("changed-validator"));
        Assert.That(KeyBuffer.RetainedCapacity, Is.Zero);
        validators[typeof(RecipeDef)] = () => { calls++; AccessTools.Method(state!.GetType(), "Invalidate").Invoke(state, null); };
        GroupKey("injection-native-operations-v1", language, new[] { file }, new[] { "text" }, Array.Empty<byte>(), typeof(RecipeDef));
        Assert.That(KeyBuffer.RetainedCapacity, Is.Zero);
    }


    private static readonly List<string> consumerTrace = new();
    private static string consumerMapping = "A";
    private static void TraceConsumer(Type __0, string __1, ref string __result)
    {
        if (__0 != typeof(ThingDef) || !__1.StartsWith("C04Trace", StringComparison.Ordinal)) return;
        consumerTrace.Add(__1);
        __result = __1.Replace("C04Trace", "C04Current" + consumerMapping);
    }
    private static string[] InjectionOutput(LoadedLanguage language) => new[] { language.anyDefInjectionsXmlParseError.ToString() }
        .Concat(language.defInjections.SelectMany(p => new[] { p.usedOldRepSyntax.ToString(), string.Join("\n", p.loadErrors) }
            .Concat(p.injections.OrderBy(i => i.Key, StringComparer.Ordinal).Select(i => i.Key + "|" + i.Value.injection + "|"
                + string.Join(",", i.Value.fullListInjection ?? new List<string>()) + "|" + i.Value.fileSource + "|" + i.Value.nonBackCompatiblePath
                + "|" + string.Join(",", (i.Value.fullListInjectionComments ?? new List<Pair<int, string>>()).Select(c => c.First + ":" + c.Second)))))).ToArray();

    [Test]
    public void DetachedCaptureSkipsConsumersAndColdWarmReplayUsesCurrentMappingsOnceInOrder()
    {
        var file = FileFor("consumer.xml");
        const string text = "<LanguageData><C04TraceScalar.label>one</C04TraceScalar.label><C04TraceList.items><li>two</li><!--note--><li>three</li></C04TraceList.items><rep><path>C04TraceOld.description</path><trans>legacy</trans></rep><C04TraceScalar.label>duplicate</C04TraceScalar.label></LanguageData>";
        var foreign = new Harmony(ConverterOwner);
        try
        {
            foreign.Patch(AccessTools.Method(typeof(BackCompatibilityConverter_1_5), "BackCompatibleDefName"), postfix: new HarmonyMethod(typeof(ParsedLanguageRuntimeTests), nameof(TraceConsumer)));
            for (int pass = 0; pass < 2; pass++)
            {
                consumerMapping = pass == 0 ? "A" : "B";
                var native = Language(); Begin(native); consumerTrace.Clear();
                var instructions = ParsedInjectionInstructions.Parse(file, text, typeof(ThingDef));
                Assert.That(instructions, Is.Not.Null); Assert.That(consumerTrace, Is.Empty, "Detached parser capture must not call any converter.");
                native.LoadFromFile_DefInject(file, typeof(ThingDef), text);
                string[] expectedTrace = consumerTrace.ToArray(); var expected = InjectionOutput(native);
                Assert.That(expectedTrace, Is.EqualTo(new[] { "C04TraceScalar", "C04TraceList", "C04TraceOld", "C04TraceScalar" }));
                var reused = Language(); Begin(reused); consumerTrace.Clear();
                // The counting hook is confined to this parser/replay test.
                // Production admission of real suppliers is tested separately;
                // Begin's isolated guard array is intentionally empty here.
                ParsedLanguageRuntime.InjectionGroup(reused, new List<VirtualFile> { file }, new List<string> { text }, typeof(ThingDef));
                Assert.That(consumerTrace, Is.EqualTo(expectedTrace));
                Assert.That(InjectionOutput(reused), Is.EqualTo(expected));
                Assert.That(Counter("InjectionHits"), Is.EqualTo(pass));
                Assert.That(Counter("NativeInsertions"), Is.EqualTo(4));
                Assert.That(reused.defInjections.Single().injections.Keys.All(k => k.StartsWith("C04Current" + consumerMapping, StringComparison.Ordinal)), Is.True);
            }
        }
        finally { End(); foreign.UnpatchAll(ConverterOwner); consumerTrace.Clear(); }
    }

    private static int entryCalls;
    private static void CountEntry() => entryCalls++;
    private static void ChangeParsedTranslation(ref string __result) => __result += "-changed";
    [TestCase(false), TestCase(true)]
    public void ForeignInsertionEntryOrParserHooksRemainNativeWithoutDoubleEffects(bool parser)
    {
        var file = FileFor("foreign-entry.xml");
        const string text = "<LanguageData><FixtureA.label>first</FixtureA.label><FixtureB.label>second</FixtureB.label></LanguageData>";
        var foreign = new Harmony(ConverterOwner);
        try
        {
            var language = Language(); Begin(language);
            var target = AccessTools.Method(typeof(DefInjectionPackage), parser ? "ProcessedTranslation" : "TryAddInjection");
            Assert.That(PublishedPatchGuard.TryCreate(target, PatchOwner, out var guard, true), Is.True);
            AccessTools.Field(typeof(ParsedLanguageRuntime), "guards").SetValue(null, new[] { guard });
            if (parser) foreign.Patch(target, postfix: new HarmonyMethod(typeof(ParsedLanguageRuntimeTests), nameof(ChangeParsedTranslation)));
            else foreign.Patch(target, prefix: new HarmonyMethod(typeof(ParsedLanguageRuntimeTests), nameof(CountEntry)));
            entryCalls = 0;
            ParsedLanguageRuntime.InjectionGroup(language, new List<VirtualFile> { file }, new List<string> { text }, typeof(RecipeDef));
            Assert.That(Counter("InjectionHits"), Is.Zero); Assert.That(Counter("Published"), Is.Zero);
            if (parser)
            {
                var native = Language(); native.LoadFromFile_DefInject(file, typeof(RecipeDef), text);
                Assert.That(InjectionOutput(language), Is.EqualTo(InjectionOutput(native)));
                Assert.That(guard!.AllowsOriginalContract(), Is.False);
            }
            else Assert.That(entryCalls, Is.EqualTo(2));
        }
        finally { End(); foreign.UnpatchAll(ConverterOwner); }
    }

    private sealed class ForeignStringComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => throw new InvalidOperationException("custom comparer executed");
        public int GetHashCode(string value) => throw new InvalidOperationException("custom comparer executed");
    }
    [Test]
    public void VefMappingGuardAllowsLiveValuesAndRejectsCallbackComparersAndNonRuntimeTypes()
    {
        var map = new Dictionary<string, Dictionary<Type, string>> { ["old"] = new() { [typeof(ThingDef)] = "new" } };
        ParsedLanguageConsumers.ValidateMappings(map);
        map["old"][typeof(ThingDef)] = "changed"; ParsedLanguageConsumers.ValidateMappings(map);
        Assert.Throws<InvalidDataException>(new Action(() => ParsedLanguageConsumers.ValidateMappings(new Dictionary<string, Dictionary<Type, string>>(new ForeignStringComparer()))));
        map["old"] = new Dictionary<Type, string> { [new TypeDelegator(typeof(ThingDef))] = "custom type" };
        Assert.Throws<InvalidDataException>(new Action(() => ParsedLanguageConsumers.ValidateMappings(map)));
    }


}
