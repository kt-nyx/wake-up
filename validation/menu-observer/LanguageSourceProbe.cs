// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using Verse;

namespace FixtureMenuObserver;

// These Defs are private observer test inputs, not product recognition rules.
public sealed class LanguageProbeDef : Def
{
    [LoadAlias("oldText")] public string text = "original";
    [TranslationCanChangeCount] public List<string> words = new() { "original" };
}

// Bounded explicit fixture-only native LoadData/translation application probe.
// Native source discovery sees a temporary folder on the existing observer mod.
// Its exact folder list and private Def database are restored in finally.
internal static class LanguageSourceProbe
{
    private const string HookOwner = "local.fixture.menuobserver.language-source-probe";
    private const string Language = "WakeUpLanguageProbe";
    private const string MainDef = "C04LanguageProbe", LateDef = "C04LanguageProbeLate";
    private const string Key = "C04LanguageProbeKey";
    // Native short-name resolution searches its built-in namespaces only. This
    // observer's namespace must be included in the DefInjected directory name.
    private static readonly string DefFolder = typeof(LanguageProbeDef).FullName!;
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private static bool ran;
    private static int hookCalls;
    private static string output = "";

    internal static void Menu(string directory)
    {
        if (ran || !Environment.GetCommandLineArgs().Contains("--fixture-language-source-probe")
            || !Environment.GetCommandLineArgs().Contains("--fixture-functional-probes")) return;
        const string profile = @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\profile";
        if (!string.Equals(Path.GetFullPath(GenFilePaths.SaveDataFolderPath), profile, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFullPath(directory), Path.Combine(profile, "FixtureMenuObserver"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Language source probe requires the isolated fixture profile.");
        ran = true;
        output = Path.Combine(directory, "language-source-probe");
        Directory.CreateDirectory(output);
        ModContentPack? mod = null;
        string[]? originalFolders = null;
        LanguageProbeDef[] originalDefs = DefDatabase<LanguageProbeDef>.AllDefsListForReading.ToArray();
        LoadedLanguage originalActive = LanguageDatabase.activeLanguage, originalDefault = LanguageDatabase.defaultLanguage;
        var harmony = new Harmony(HookOwner);
        bool passed = false, restored = false;
        try
        {
            if (!PlayDataLoader.Loaded || Current.ProgramState != ProgramState.Entry || Current.Game != null)
                throw new InvalidOperationException("Language source probe requires loaded startup data and no game.");
            mod = LoadedModManager.RunningMods.Single(m => m.PackageId == "local.fixture.menuobserver");
            Require(GenTypes.GetTypeInAnyAssembly(DefFolder) == typeof(LanguageProbeDef),
                "Native type resolver cannot find private probe Def type: " + DefFolder);
            originalFolders = mod.foldersToLoadDescendingOrder.ToArray();
            // A unique source root guarantees a new content identity for the
            // first sample without deleting or bypassing any product store.
            string source = Path.Combine(profile, "C04Source-" + Guid.NewGuid().ToString("N"));
            string languageRoot = Path.Combine(source, "Languages", Language);
            string keyed = Path.Combine(languageRoot, "Keyed", "source.xml");
            string injected = Path.Combine(languageRoot, "DefInjected", DefFolder, "source.xml");
            string strings = Path.Combine(languageRoot, "Strings", "Probe", "Words.txt");
            Write(keyed, Keyed("alpha")); Write(injected, Injection("alpha")); Write(strings, "alpha\nsecond\n");
            mod.foldersToLoadDescendingOrder.Add(source);
            Receipt("source-installed", "\"packageId\":" + Q(mod.PackageId) + ",\"source\":" + Q(source));

            var cold = Sample("cold", Language, "alpha", false);
            var warm = Sample("warm", Language, "alpha", false);
            Require(cold == warm, "Cold/warm native output differs.");
            SameSizeUpdate(keyed, Keyed("bravo")); SameSizeUpdate(injected, Injection("bravo"));
            SameSizeUpdate(strings, "bravo\nsecond\n");
            var updated = Sample("same-id-update", Language, "bravo", false);
            var updatedWarm = Sample("updated-warm", Language, "bravo", false);
            Require(updated == updatedWarm && updated != cold, "Updated native output is stale or unstable.");

            // A real unrecognized parser callback must retain the callback and
            // its mutation, even though an otherwise reusable warm entry exists.
            harmony.Patch(AccessTools.Method(typeof(LoadedLanguage), "LoadFromFile_Keyed"),
                postfix: new HarmonyMethod(typeof(LanguageSourceProbe), nameof(ForeignKeyed)));
            Sample("foreign-keyed-callback", Language, "bravo", true);
            Require(hookCalls > 0, "Foreign keyed callback was bypassed.");
            harmony.UnpatchAll(HookOwner);

            ProbeErrors(source);
            ProbeLegacy(source);
            passed = true;
        }
        catch (Exception e)
        {
            Receipt("failed", "\"error\":" + Q(e.ToString()));
        }
        finally
        {
            harmony.UnpatchAll(HookOwner);
            if (mod != null && originalFolders != null)
            {
                mod.foldersToLoadDescendingOrder.Clear();
                mod.foldersToLoadDescendingOrder.AddRange(originalFolders);
            }
            DefDatabase<LanguageProbeDef>.Clear();
            foreach (LanguageProbeDef def in originalDefs) DefDatabase<LanguageProbeDef>.Add(def);
            restored = (mod == null || originalFolders == null || mod.foldersToLoadDescendingOrder.SequenceEqual(originalFolders))
                && DefDatabase<LanguageProbeDef>.AllDefsListForReading.SequenceEqual(originalDefs)
                && ReferenceEquals(LanguageDatabase.activeLanguage, originalActive)
                && ReferenceEquals(LanguageDatabase.defaultLanguage, originalDefault);
            Receipt("complete", "\"passed\":" + B(passed && restored) + ",\"restored\":" + B(restored)
                + ",\"scope\":\"private native LoadData, native keyed/Strings consumers, private Def application; cache-hit evidence is separately recorded by product\"");
        }
    }

    private static string Sample(string phase, string folder, string expected, bool foreign)
    {
        DefDatabase<LanguageProbeDef>.Clear();
        var def = new LanguageProbeDef { defName = MainDef };
        DefDatabase<LanguageProbeDef>.Add(def);
        var language = new LoadedLanguage(folder);
        language.LoadData();
        DefInjectionPackage package = RequireInjectionSources(phase, language,
            MainDef + ".oldText", MainDef + ".words", LateDef + ".text");
        string[] injectionParseErrors = package.loadErrors.ToArray();
        Require(injectionParseErrors.Length == 0, "Unexpected injection parse errors: " + string.Join("; ", injectionParseErrors));
        bool fresh = package.injections.Values.All(v => !v.injected && v.normalizedPath == null
            && v.replacedString == null && v.replacedList == null);
        Require(fresh, "Restored injection records contain application state.");
        Require(language.TryGetTextFromKey(Key, out TaggedString keyed), "Native keyed lookup missing.");
        Require(keyed.ToString() == expected + (foreign ? "-foreign" : ""), "Native keyed value mismatch.");
        string stringKey = language.stringFiles.Keys.Single();
        Require(language.TryGetStringsFromFile(stringKey, out List<string> strings), "Native Strings lookup missing.");
        Require(strings.SequenceEqual(new[] { expected, "second" }), "Native Strings values mismatch.");
        Require(!language.TryGetTextFromKey("NewColony", out _), "Private language unexpectedly contains the English fallback key.");
        // Developer mode intentionally pseudo-translates fallback text. This
        // particular comparison requires ordinary native English output and
        // does not change that preference to manufacture a passing result.
        Require(!Prefs.DevMode, "English fallback equality probe requires developer mode off.");
        Require(LanguageDatabase.defaultLanguage.folderName == LanguageDatabase.DefaultLangFolderName,
            "Native default language is not English.");
        Require(LanguageDatabase.defaultLanguage.TryGetTextFromKey("NewColony", out TaggedString english),
            "Native default-language NewColony lookup missing.");
        LoadedLanguage selected = LanguageDatabase.activeLanguage;
        string fallback;
        try
        {
            // Synchronous owner-thread consumer only: the private language is
            // already loaded; native Translator must miss here and use English.
            // No SelectLanguage, preference write, reload, or GUI work occurs.
            LanguageDatabase.activeLanguage = language;
            fallback = "NewColony".Translate().ToString();
        }
        finally { LanguageDatabase.activeLanguage = selected; }
        Require(ReferenceEquals(LanguageDatabase.activeLanguage, selected) && fallback == english.ToString(),
            "Native Translator English fallback differs or active language was not restored.");
        language.InjectIntoData_BeforeImpliedDefs();
        Require(!package.injections[LateDef + ".text"].injected, "Absent Def was prematurely marked injected.");
        var late = new LanguageProbeDef { defName = LateDef };
        DefDatabase<LanguageProbeDef>.Add(late);
        language.InjectIntoData_AfterImpliedDefs();
        Require(def.text == expected && def.words.SequenceEqual(new[] { expected, "second" })
            && late.text == expected && package.injections.Values.All(v => v.injected), "Native Def application or retry mismatch.");
        string observed = "{\"keyed\":" + Q(keyed.ToString()) + ",\"stringsKey\":" + Q(stringKey)
            + ",\"strings\":" + List(strings) + ",\"scalar\":" + Q(def.text) + ",\"list\":" + List(def.words)
            + ",\"nativeFallback\":{\"key\":\"NewColony\",\"privateKeyAbsent\":true,\"activeRestored\":true"
            + ",\"defaultLanguage\":" + Q(LanguageDatabase.defaultLanguage.folderName) + ",\"defaultValue\":" + Q(english.ToString())
            + ",\"translatorValue\":" + Q(fallback) + "}"
            + ",\"lateScalar\":" + Q(late.text) + ",\"languageErrors\":" + List(language.loadErrors)
            + ",\"injectionParseErrors\":" + List(injectionParseErrors)
            + ",\"injectionErrors\":" + List(package.loadErrors) + ",\"injectionSuggestions\":" + List(package.loadSyntaxSuggestions) + "}";
        Receipt(phase, "\"passed\":true,\"freshPreApplicationRecords\":" + B(fresh)
            + ",\"lateDefRetryPassed\":true,\"foreignCalls\":" + hookCalls + ",\"output\":" + observed);
        return observed;
    }

    private static void ProbeErrors(string source)
    {
        string folder = Language + "Errors", root = Path.Combine(source, "Languages", folder);
        Write(Path.Combine(root, "Keyed", "duplicate.xml"), "<LanguageData><C04Duplicate>first</C04Duplicate><C04Duplicate>later</C04Duplicate></LanguageData>");
        Write(Path.Combine(root, "Keyed", "malformed.xml"), "<LanguageData><C04Discarded>discard</C04Discarded><Unclosed></LanguageData>");
        Write(Path.Combine(root, "DefInjected", DefFolder, "partial.xml"),
            "<LanguageData><" + MainDef + ".text>partial</" + MainDef + ".text><rep><path>" + MainDef + ".words</path></rep></LanguageData>");
        for (int i = 0; i < 2; i++)
        {
            DefDatabase<LanguageProbeDef>.Clear();
            var def = new LanguageProbeDef { defName = MainDef };
            DefDatabase<LanguageProbeDef>.Add(def);
            var language = new LoadedLanguage(folder);
            language.LoadData();
            DefInjectionPackage package = RequireInjectionSources(i == 0 ? "errors-first" : "errors-repeat", language, MainDef + ".text");
            bool firstKept = language.TryGetTextFromKey("C04Duplicate", out TaggedString duplicate) && duplicate.ToString() == "first";
            bool fileDropped = !language.TryGetTextFromKey("C04Discarded", out _) && language.anyKeyedReplacementsXmlParseError;
            bool partialKept = package.injections.ContainsKey(MainDef + ".text") && language.anyDefInjectionsXmlParseError;
            // InjectIntoDefs clears package.loadErrors at the start of EACH
            // application pass. Preserve parsing diagnostics at their actual
            // boundary, before the native application methods replace them.
            string[] languageParseErrors = language.loadErrors.ToArray();
            string[] injectionParseErrors = package.loadErrors.ToArray();
            bool parsePassed = firstKept && fileDropped && partialKept
                && languageParseErrors.Any(s => s.Contains("Duplicate keyed translation key")) && injectionParseErrors.Length > 0;
            Receipt(i == 0 ? "errors-first-parsed" : "errors-repeat-parsed", "\"passed\":" + B(parsePassed)
                + ",\"duplicateFirstKept\":" + B(firstKept) + ",\"malformedKeyedFileDropped\":" + B(fileDropped)
                + ",\"partialInjectionRetained\":" + B(partialKept) + ",\"languageErrors\":" + List(languageParseErrors)
                + ",\"injectionParseErrors\":" + List(injectionParseErrors));
            Require(parsePassed, "Native duplicate, whole-file failure or partial injection parsing behavior changed.");
            language.InjectIntoData_BeforeImpliedDefs(); language.InjectIntoData_AfterImpliedDefs();
            Require(def.text == "partial" && package.injections[MainDef + ".text"].injected,
                "Native partial injection application behavior changed.");
            Receipt(i == 0 ? "errors-first" : "errors-repeat", "\"passed\":true,\"duplicateFirstKept\":true,\"malformedKeyedFileDropped\":true"
                + ",\"partialInjectionApplied\":true,\"languageErrors\":" + List(languageParseErrors)
                + ",\"injectionParseErrors\":" + List(injectionParseErrors) + ",\"injectionApplicationErrors\":" + List(package.loadErrors));
        }
    }

    private static void ProbeLegacy(string source)
    {
        string folder = Language + "Legacy", root = Path.Combine(source, "Languages", folder);
        Write(Path.Combine(root, "CodeLinked", "source.xml"), Keyed("older"));
        Write(Path.Combine(root, "Keyed", "ignored.xml"), Keyed("wrong"));
        Write(Path.Combine(root, "DefLinked", DefFolder, "source.xml"), Injection("older"));
        Write(Path.Combine(root, "DefInjected", DefFolder, "ignored.xml"), Injection("wrong"));
        Write(Path.Combine(root, "Strings", "Probe", "Words.txt"), "older\nsecond\n");
        var first = Sample("legacy-first", folder, "older", false);
        var second = Sample("legacy-repeat", folder, "older", false);
        Require(first == second && first.Contains("CodeLinked") && first.Contains("DefLinked"), "Legacy precedence/warnings changed.");
    }

    private static void ForeignKeyed(LoadedLanguage __instance)
    {
        if (__instance.folderName != Language) return;
        hookCalls++;
        if (__instance.keyedReplacements.TryGetValue(Key, out var replacement)) replacement.value += "-foreign";
    }

    private static DefInjectionPackage RequireInjectionSources(string phase, LoadedLanguage language, params string[] expectedKeys)
    {
        DefInjectionPackage? package = language.defInjections.SingleOrDefault(p => p.defType == typeof(LanguageProbeDef));
        string[] keys = package?.injections.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
        bool present = package != null && keys.Length == expectedKeys.Length && expectedKeys.All(k => package.injections.ContainsKey(k));
        Receipt(phase + "-sources-loaded", "\"passed\":" + B(present) + ",\"language\":" + Q(language.folderName)
            + ",\"defTypeFolder\":" + Q(DefFolder) + ",\"packageFound\":" + B(package != null)
            + ",\"injectionKeys\":" + List(keys) + ",\"expectedInjectionKeys\":" + List(expectedKeys)
            + ",\"languageErrors\":" + List(language.loadErrors)
            + ",\"injectionParseErrors\":" + List(package?.loadErrors ?? new List<string>()));
        Require(present, "Private injection source records missing for " + language.folderName
            + "; expected type folder " + DefFolder + "; actual keys: " + string.Join(", ", keys)
            + "; language errors: " + string.Join("; ", language.loadErrors));
        return package!;
    }

    private static void SameSizeUpdate(string path, string value)
    {
        long before = new FileInfo(path).Length;
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        Require(Utf8.GetByteCount(value) == before, "Test update changed byte size.");
        File.WriteAllText(path, value, Utf8);
        File.SetLastWriteTimeUtc(path, stamp);
        Require(new FileInfo(path).Length == before && File.GetLastWriteTimeUtc(path) == stamp, "Test update failed to retain metadata.");
        Receipt("equal-size-same-timestamp-update", "\"file\":" + Q(path) + ",\"bytes\":" + before
            + ",\"lastWriteUtc\":" + Q(stamp.ToString("O", CultureInfo.InvariantCulture)));
    }
    private static string Keyed(string value) => "<LanguageData><" + Key + ">" + value + "</" + Key + "></LanguageData>";
    private static string Injection(string value) => "<LanguageData><" + MainDef + ".oldText>" + value + "</" + MainDef
        + ".oldText><" + MainDef + ".words><li>" + value + "</li><li>second</li></" + MainDef + ".words><"
        + LateDef + ".text>" + value + "</" + LateDef + ".text></LanguageData>";
    private static void Write(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text, Utf8); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Receipt(string phase, string fields) => File.AppendAllText(Path.Combine(output, "events.jsonl"),
        "{\"phase\":" + Q(phase) + "," + fields + "}\n", Utf8);
    private static string List(IEnumerable<string> values) => "[" + string.Join(",", values.Select(Q)) + "]";
    private static string B(bool value) => value ? "true" : "false";
    private static string Q(string? text) => text == null ? "null" : "\"" + string.Concat(text.Select(c =>
        c == '\\' || c == '"' ? "\\" + c : c < 32 ? "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture) : c.ToString())) + "\"";
}
