// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Private functional observer: actual native consumers, without UI input,
// Def mutation, profiling, or a performance claim. Native language switching is
// a separate explicitly selected fixture-only correctness probe below.
internal static class LanguageQualification
{
    private static readonly string[] MenuKeys = { "NewColony", "LoadGame", "Options", "Mods", "Credits", "QuitToOS" };
    private static readonly FieldInfo Loaded = AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded");

    internal static bool StartLifecycle(string directory)
    {
        if (!Environment.GetCommandLineArgs().Contains("--fixture-language-lifecycle")) return false;
        if (!Environment.GetCommandLineArgs().Contains("--fixture-functional-probes")) return false;
        const string profile = @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\profile";
        if (!string.Equals(Path.GetFullPath(GenFilePaths.SaveDataFolderPath), profile, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFullPath(directory), Path.Combine(profile, "FixtureMenuObserver"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Language lifecycle requires the isolated observer profile.");
        if (LanguageLifecycleProbe.Started) return true;
        var probe = new GameObject("FixtureLanguageLifecycle").AddComponent<LanguageLifecycleProbe>();
        UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
        probe.Begin(directory);
        return true;
    }

    internal static void Menu(string directory, bool inspectContracts = true)
    {
        if (inspectContracts) CaptureContractMismatches(directory);
        try
        {
            LoadedLanguage active = LanguageDatabase.activeLanguage;
            LoadedLanguage fallback = LanguageDatabase.defaultLanguage;
            bool fallbackLoadedBefore = (bool)Loaded.GetValue(fallback);
            var menu = new List<string>();
            foreach (string key in MenuKeys)
            {
                bool found = active.TryGetTextFromKey(key, out TaggedString direct);
                // These are real MainMenuDrawer keys. Record both native paths:
                // the active-language lookup and Translator's fallback behavior.
                menu.Add("{\"key\":" + Q(key) + ",\"activeFound\":" + B(found)
                    + ",\"activeValue\":" + Q(direct.ToString()) + ",\"translatorValue\":" + Q(key.Translate().ToString()) + "}");
            }
            // A native English lookup may lazily LoadData. This is the sole
            // permitted loading side effect; record its before/after state.
            bool englishFound = fallback.TryGetTextFromKey("NewColony", out TaggedString english);
            string? fallbackKey = fallback.keyedReplacements.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Where(p => !p.Value.isPlaceholder && (!active.keyedReplacements.TryGetValue(p.Key, out var v) || v.isPlaceholder))
                .Select(p => p.Key).FirstOrDefault();
            string fallbackSample = "null";
            if (fallbackKey != null)
            {
                bool activeFound = active.TryGetTextFromKey(fallbackKey, out TaggedString activeText);
                bool defaultFound = fallback.TryGetTextFromKey(fallbackKey, out TaggedString defaultText);
                fallbackSample = "{\"key\":" + Q(fallbackKey) + ",\"activeFound\":" + B(activeFound)
                    + ",\"defaultFound\":" + B(defaultFound) + ",\"defaultValue\":" + Q(defaultText.ToString())
                    + ",\"translatorValue\":" + Q(fallbackKey.Translate().ToString()) + "}";
            }
            var defs = DefDatabase<ThingDef>.AllDefsListForReading.OrderBy(d => d.defName, StringComparer.Ordinal)
                .Select(d => "{\"defName\":" + Q(d.defName) + ",\"label\":" + Q(d.label) + ",\"description\":" + Q(d.description) + "}").ToArray();
            var samples = new[] { "Steel", "WoodLog", "ComponentIndustrial" }.Select(name =>
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                return "{\"defName\":" + Q(name) + ",\"found\":" + B(def != null)
                    + ",\"label\":" + Q(def?.label) + ",\"description\":" + Q(def?.description) + "}";
            });
            string json = "{\"schema\":\"fixture-language-qualification.v1\",\"observed\":true,\"devMode\":" + B(Prefs.DevMode)
                + ",\"coverage\":\"native lookups and final Def output; no visual, gameplay or performance claim\""
                + ",\"activeLanguage\":" + Language(active) + ",\"defaultLanguage\":" + Language(fallback)
                + ",\"defaultLoadedBeforeLookups\":" + B(fallbackLoadedBefore)
                + ",\"defaultLoadedAfterLookups\":" + B((bool)Loaded.GetValue(fallback))
                + ",\"englishNewColonyFound\":" + B(englishFound) + ",\"englishNewColony\":" + Q(english.ToString())
                + ",\"menuLookups\":[" + string.Join(",", menu) + "],\"fallbackSample\":" + fallbackSample
                + ",\"thingDefCount\":" + defs.Length + ",\"thingDefSha256\":" + Q(Hash(defs))
                + ",\"thingDefSamples\":[" + string.Join(",", samples) + "]}";
            File.WriteAllText(Path.Combine(directory, "language-qualification.json"), json + "\n", new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "language-qualification.json"), "{\"observed\":false}\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "language-qualification-error.txt"), e.ToString(), new UTF8Encoding(false));
        }
    }

    private static void CaptureContractMismatches(string directory)
    {
        // Diagnose the loaded runtime without treating it as an authority for
        // accepted bodies. Only the product's already-pinned contract tables are
        // consulted; this observer never writes or changes production contracts.
        try
        {
            Assembly? product = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "WakeUp");
            Type? contract = product?.GetType("WakeUp.ParsedLanguageContract");
            Type? identity = product?.GetType("WakeUp.SemanticMethodIdentity");
            if (contract == null || identity == null) return;
            var expected = ((IEnumerable<KeyValuePair<string, string>>)AccessTools.Field(contract, "Expected").GetValue(null))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var methods = (MethodBase[])AccessTools.Method(contract, "Methods").Invoke(null, null);
            MethodInfo signature = AccessTools.Method(identity, "Signature", new[] { typeof(MethodBase) });
            MethodInfo hash = identity.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(m => m.Name == "TryHash" && m.GetParameters().Length == 4);
            var rows = new List<string>();
            foreach (MethodBase method in methods)
            {
                string name = (string)signature.Invoke(null, new object[] { method });
                object[] args = { method, "", "", "" };
                bool hashed = (bool)hash.Invoke(null, args);
                bool pinned = expected.TryGetValue(name, out string pinnedHash);
                // Coordinated Prepatcher bodies are pinned before this process,
                // not learned from its observed output. Older cores lack this table.
                Type? prepatch = product!.GetType("WakeUp.ParsedLanguagePrepatch");
                if (prepatch != null)
                {
                    var names = (string[])AccessTools.Field(prepatch, "Names").GetValue(null);
                    var types = (string[])AccessTools.Field(prepatch, "Types").GetValue(null);
                    var bodies = (string[])AccessTools.Field(prepatch, "Rewritten").GetValue(null);
                    int index = Array.FindIndex(names, n => n == method.Name);
                    if (index >= 0 && method.DeclaringType?.FullName == types[index])
                    { pinned = true; pinnedHash = bodies[index]; }
                }
                if (hashed && pinned && pinnedHash == (string)args[1]) continue;
                rows.Add("{\"signature\":" + Q(name) + ",\"expectedPresent\":" + B(pinned)
                    + ",\"expectedHash\":" + Q(pinned ? pinnedHash : null) + ",\"hashSucceeded\":" + B(hashed)
                    + ",\"actualHash\":" + Q((string)args[1]) + ",\"reason\":" + Q((string)args[2])
                    + ",\"canonical\":" + Q((string)args[3]) + "}");
            }
            File.WriteAllLines(Path.Combine(directory, "language-contract-mismatches.jsonl"), rows, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            // Contract diagnostics must not suppress actual language evidence.
            try { File.WriteAllText(Path.Combine(directory, "language-contract-diagnostic-error.txt"), e.ToString(), new UTF8Encoding(false)); }
            catch { }
        }
    }

    private static string Language(LoadedLanguage language)
    {
        string[] keyed = language.keyedReplacements.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
            "[" + Q(p.Key) + "," + Q(p.Value.key) + "," + Q(p.Value.value) + "," + Q(p.Value.fileSource)
            + "," + p.Value.fileSourceLine + "," + Q(p.Value.fileSourceFullPath) + "," + B(p.Value.isPlaceholder) + "]").ToArray();
        string[] strings = language.stringFiles.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => "[" + Q(p.Key) + "," + TextList(p.Value) + "]").ToArray();
        var queries = new List<string>();
        foreach (string name in language.stringFiles.Keys.OrderBy(s => s, StringComparer.Ordinal).Take(3))
        {
            bool found = language.TryGetStringsFromFile(name, out List<string> list);
            queries.Add("{\"file\":" + Q(name) + ",\"found\":" + B(found) + ",\"count\":" + (list?.Count ?? 0)
                + ",\"sha256\":" + Q(Hash(new[] { TextList(list) })) + ",\"firstValues\":" + TextList(list?.Take(3)) + "}");
        }
        var injections = new List<string>();
        var lists = new List<string>();
        foreach (DefInjectionPackage package in language.defInjections.OrderBy(p => p.defType.FullName, StringComparer.Ordinal))
        {
            foreach (var entry in package.injections.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var v = entry.Value;
                injections.Add("[" + Q(package.defType.FullName) + "," + Q(entry.Key) + "," + Q(v.path)
                    + "," + Q(v.nonBackCompatiblePath) + "," + Q(v.injection) + "," + TextList(v.fullListInjection)
                    + "," + (v.fullListInjectionComments == null ? "null" : "[" + string.Join(",", v.fullListInjectionComments
                        .Select(c => "[" + c.First.ToString(CultureInfo.InvariantCulture) + "," + Q(c.Second) + "]")) + "]")
                    + "," + Q(v.fileSource) + "," + B(v.isPlaceholder) + "," + B(v.injected)
                    + "," + Q(v.normalizedPath) + "," + Q(v.suggestedPath) + "," + Q(v.replacedString)
                    + "," + TextList(v.replacedList) + "]");
                // Resolve only direct native fields on a Def. Unsupported nested
                // paths are skipped honestly; never invoke a property's getter.
                if (v.fullListInjection != null && lists.Count < 6)
                {
                    string[] parts = (v.normalizedPath ?? v.path ?? "").Split('.');
                    if (parts.Length != 2) continue;
                    Def def = GenDefDatabase.GetDefSilentFail(package.defType, parts[0]);
                    FieldInfo? field = def == null ? null : AccessTools.Field(def.GetType(), parts[1]);
                    if (field == null || field.IsStatic || !(field.GetValue(def) is IEnumerable<string> actual)) continue;
                    lists.Add("{\"type\":" + Q(package.defType.FullName) + ",\"path\":" + Q(v.path)
                        + ",\"injected\":" + B(v.injected) + ",\"expected\":" + TextList(v.fullListInjection)
                        + ",\"actual\":" + TextList(actual) + "}");
                }
            }
        }
        var diagnostics = language.defInjections.OrderBy(p => p.defType.FullName, StringComparer.Ordinal)
            .Select(p => "{\"type\":" + Q(p.defType.FullName) + ",\"legacy\":" + B(p.usedOldRepSyntax)
                + ",\"errors\":" + TextList(p.loadErrors) + ",\"suggestions\":" + TextList(p.loadSyntaxSuggestions) + "}");
        return "{\"folder\":" + Q(language.folderName) + ",\"legacyFolder\":" + Q(language.LegacyFolderName)
            + ",\"keyedCount\":" + keyed.Length + ",\"keyedSha256\":" + Q(Hash(keyed))
            + ",\"stringsCount\":" + strings.Length + ",\"stringsSha256\":" + Q(Hash(strings))
            + ",\"stringsQueries\":[" + string.Join(",", queries) + "]"
            + ",\"injectionCount\":" + injections.Count + ",\"injectionSha256\":" + Q(Hash(injections))
            + ",\"directListFieldSamples\":[" + string.Join(",", lists) + "]"
            + ",\"errors\":" + TextList(language.loadErrors) + ",\"anyError\":" + B(language.anyError)
            + ",\"keyedXmlError\":" + B(language.anyKeyedReplacementsXmlParseError)
            + ",\"keyedXmlErrorFile\":" + Q(language.lastKeyedReplacementsXmlParseErrorInFile)
            + ",\"injectionXmlError\":" + B(language.anyDefInjectionsXmlParseError)
            + ",\"injectionXmlErrorFile\":" + Q(language.lastDefInjectionsXmlParseErrorInFile)
            + ",\"packageDiagnostics\":[" + string.Join(",", diagnostics) + "]}";
    }

    private static string Hash(IEnumerable<string> rows)
    {
        using var hash = SHA256.Create();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            foreach (string row in rows) writer.Write(row);
        return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
    }
    private static string TextList(IEnumerable<string>? values) => values == null ? "null" : "[" + string.Join(",", values.Select(Q)) + "]";
    private static string B(bool value) => value ? "true" : "false";
    private static string Q(string? value)
    {
        if (value == null) return "null";
        var text = new StringBuilder("\"");
        foreach (char c in value)
            if (c == '\\' || c == '"') text.Append('\\').Append(c);
            else if (c < 32) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else text.Append(c);
        return text.Append('"').ToString();
    }
}

// Native asynchronous reloads, observed after a completed native menu repaint.
// No click/input/window activation, synthetic Def changes, or forced GC.
public sealed class LanguageLifecycleProbe : MonoBehaviour
{
    private const string Owner = "local.fixture.menuobserver.language-lifecycle";
    internal static bool Started { get; private set; }
    private static int completedMenu;
    private string directory = "", original = "";
    private string[] sequence = Array.Empty<string>();
    private int next, menuBefore;
    private bool waiting, finished, failed;
    private DateTime deadline;
    private WeakReference? oldLanguage, oldKeyed, oldStrings, oldInjections, oldDef;

    internal void Begin(string outputDirectory)
    {
        Started = true;
        directory = Path.Combine(outputDirectory, "language-lifecycle");
        Directory.CreateDirectory(directory);
        original = LanguageDatabase.activeLanguage.folderName;
        try
        {
            string french = FindLanguage("French").folderName;
            string chinese = FindLanguage("ChineseSimplified").folderName;
            var selections = new List<string>();
            if (original != french) selections.Add(french);
            selections.Add(chinese); selections.Add(french);
            if (original != french) selections.Add(original);
            sequence = selections.ToArray();
            new Harmony(Owner).Patch(AccessTools.Method("RimWorld.MainMenuDrawer:MainMenuOnGUI"),
                postfix: new HarmonyMethod(typeof(LanguageLifecycleProbe), nameof(MenuCompleted)) { priority = Priority.Last });
            Snapshot("00-initial");
            Receipt("started", "\"original\":" + Quote(original) + ",\"selections\":["
                + string.Join(",", sequence.Select(Quote)) + "]");
        }
        catch (Exception e) { Fail(e); }
    }

    private static LoadedLanguage FindLanguage(string folder) => LanguageDatabase.AllLoadedLanguages
        .FirstOrDefault(l => l.folderName == folder || l.LegacyFolderName == folder)
        ?? throw new InvalidOperationException("Required native language absent: " + folder);

    private static void MenuCompleted(bool __runOriginal)
    {
        if (__runOriginal && Event.current != null && Event.current.type == EventType.Repaint
            && Current.ProgramState == ProgramState.Entry && Current.Game == null
            && PlayDataLoader.Loaded && !LongEventHandler.AnyEventNowOrWaiting) completedMenu++;
    }

    private void Update()
    {
        if (finished) return;
        try
        {
            if (Current.ProgramState != ProgramState.Entry || Current.Game != null)
                throw new InvalidOperationException("Language lifecycle left the startup menu.");
            if (waiting && DateTime.UtcNow > deadline)
                throw new TimeoutException("Native language reload did not complete within the five-minute correctness limit.");
            if (LongEventHandler.AnyEventNowOrWaiting || !PlayDataLoader.Loaded) return;
            if (waiting)
            {
                if (completedMenu == menuBefore) return;
                string expected = sequence[next - 1];
                LoadedLanguage current = LanguageDatabase.activeLanguage;
                bool freshLanguage = !ReferenceEquals(oldLanguage?.Target, current);
                bool freshKeyed = !ReferenceEquals(oldKeyed?.Target, current.keyedReplacements);
                bool freshStrings = !ReferenceEquals(oldStrings?.Target, current.stringFiles);
                bool freshInjections = !ReferenceEquals(oldInjections?.Target, current.defInjections);
                bool freshDef = !ReferenceEquals(oldDef?.Target, DefDatabase<ThingDef>.GetNamedSilentFail("Steel"));
                bool pass = current.folderName == expected && Prefs.LangFolderName == expected
                    && freshLanguage && freshKeyed && freshStrings && freshInjections && freshDef;
                Snapshot(next.ToString("D2", CultureInfo.InvariantCulture) + "-completed");
                Receipt("reload-completed", "\"step\":" + next + ",\"expected\":" + Quote(expected)
                    + ",\"actual\":" + Quote(current.folderName) + ",\"passed\":" + Bool(pass)
                    + ",\"freshLanguage\":" + Bool(freshLanguage) + ",\"freshKeyedTable\":" + Bool(freshKeyed)
                    + ",\"freshStringsTable\":" + Bool(freshStrings) + ",\"freshInjectionTable\":" + Bool(freshInjections)
                    + ",\"freshSteelDef\":" + Bool(freshDef));
                ReleasePrevious(); waiting = false;
                if (!pass) throw new InvalidOperationException("Native reload did not produce the requested language and fresh objects.");
            }
            if (next == sequence.Length) { Complete(); return; }
            LoadedLanguage active = LanguageDatabase.activeLanguage;
            oldLanguage = new WeakReference(active); oldKeyed = new WeakReference(active.keyedReplacements);
            oldStrings = new WeakReference(active.stringFiles); oldInjections = new WeakReference(active.defInjections);
            oldDef = new WeakReference(DefDatabase<ThingDef>.GetNamedSilentFail("Steel"));
            LoadedLanguage selected = FindLanguage(sequence[next++]);
            menuBefore = completedMenu; deadline = DateTime.UtcNow.AddMinutes(5); waiting = true;
            Receipt("reload-requested", "\"step\":" + next + ",\"folder\":" + Quote(selected.folderName));
            LanguageDatabase.SelectLanguage(selected);
        }
        catch (Exception e) { Fail(e); }
    }

    private void Snapshot(string name)
    {
        string path = Path.Combine(directory, name);
        Directory.CreateDirectory(path);
        LanguageQualification.Menu(path);
        if (File.Exists(Path.Combine(path, "language-qualification-error.txt")))
            throw new InvalidOperationException("Language output observation failed at " + name);
    }

    private void Fail(Exception exception)
    {
        if (finished) return;
        failed = true;
        try { Receipt("failed", "\"error\":" + Quote(exception.ToString())); }
        catch { }
        // A failed reload is never presented as restored runtime state. Restore
        // only the selected preference before native shutdown; the owner also
        // restores the exact pre-run profile transactionally after capture.
        try { if (original.Length != 0) Prefs.LangFolderName = original; } catch { }
        Complete();
    }

    private void Complete()
    {
        if (finished) return;
        finished = true;
        ReleasePrevious();
        try { new Harmony(Owner).UnpatchAll(Owner); } catch { }
        try
        {
            bool restored = LanguageDatabase.activeLanguage?.folderName == original && Prefs.LangFolderName == original;
            Receipt("complete", "\"passed\":" + Bool(!failed && restored) + ",\"original\":" + Quote(original)
                + ",\"actual\":" + Quote(LanguageDatabase.activeLanguage?.folderName) + ",\"restored\":" + Bool(restored)
                + ",\"observerPreviousReferencesReleased\":true");
        }
        catch { }
        new GameObject("FixtureMenuObserverExit").AddComponent<MenuReadyExit>();
        Destroy(gameObject);
    }

    private void ReleasePrevious() { oldLanguage = null; oldKeyed = null; oldStrings = null; oldInjections = null; oldDef = null; }
    private void Receipt(string phase, string fields) => File.AppendAllText(Path.Combine(directory, "events.jsonl"),
        "{\"phase\":" + Quote(phase) + "," + fields + "}\n", new UTF8Encoding(false));
    private static string Bool(bool value) => value ? "true" : "false";
    private static string Quote(string? text) => text == null ? "null" : "\"" + string.Concat(text.Select(c =>
        c == '\\' || c == '"' ? "\\" + c : c < 32 ? "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture) : c.ToString())) + "\"";
}
