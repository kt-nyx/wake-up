// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace FixtureMenuObserver;

// Existing runtime identities only: no codec types, type scans, assembly loads,
// PCM operations or feature acceptance. The core verifier also has this contract.
internal static class C10BootstrapProbe
{
    private const string StateKey = "WakeUp.PreparedAudio.Bootstrap.v1";
    internal static void Start(string directory)
    {
        string failure = "", session = "";
        bool installed = false, emitted = false, verified = false, passed = false;
        int version = 0, finalEntries = 0;
        Assembly? oldHarmony = null, gameBefore = null, finalHarmony = null, finalGame = null;
        Assembly[] shared = Array.Empty<Assembly>(), before = AppDomain.CurrentDomain.GetAssemblies();
        object[][] events = Array.Empty<object[]>();
        string[] hashes = Array.Empty<string>(), runtimeHashes = Array.Empty<string>();
        try
        {
            if (AppDomain.CurrentDomain.GetData(StateKey) is not Dictionary<string, object> state)
                throw new InvalidOperationException("bootstrap-state-missing");
            lock (state["gate"])
            {
                version = (int)state["version"]; session = (string)state["session"];
                oldHarmony = (Assembly)state["oldHarmony"]; gameBefore = (Assembly)state["gameBefore"];
                installed = (bool)state["oldGuardsInstalled"]; emitted = (bool)state["finalGuardsEmitted"];
                failure = (string)state["failure"];
                shared = ((Assembly[])state["sharedAtGuard"]).ToArray();
                events = ((List<object[]>)state["entryEvents"]).Take(128).Select(e => (object[])e.Clone()).ToArray();
                if (state.TryGetValue("finalGuardHashes", out object value)) hashes = ((string[])value).ToArray();
                if (state.TryGetValue("finalCallbackEntries", out object count)) finalEntries = (int)count;
            }
            finalHarmony = Live(before, "0Harmony"); finalGame = Live(before, "Assembly-CSharp");
            Assembly core = Live(before, "WakeUp") ?? throw new InvalidOperationException("final-core-missing");
            Type bootstrap = core.GetType("WakeUp.PreparedAudioBootstrap", true)!;
            MethodInfo method = bootstrap.GetMethod("VerifyFinalGuards", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("final-guard-verifier-missing");
            verified = method.Invoke(null, null) is bool result && result;
            lock (state["gate"])
            {
                if (state.TryGetValue("finalRuntimeGuardHashes", out object actual)) runtimeHashes = ((string[])actual).ToArray();
            }
            passed = version == 1 && session.Length != 0 && failure.Length == 0 && installed && emitted && verified
                && finalHarmony != null && finalGame != null
                && !ReferenceEquals(finalHarmony, oldHarmony) && !ReferenceEquals(finalGame, gameBefore);
        }
        catch (Exception exception) { failure = exception.ToString(); }

        Assembly[] after = AppDomain.CurrentDomain.GetAssemblies();
        Assembly[] codecs = after.Where(IsCodec).ToArray();
        bool loadedCodec = codecs.Any(a => !before.Any(b => ReferenceEquals(a, b)));
        if (loadedCodec) { passed = false; failure += " observer-verification-loaded-codec"; }
        var fields = new List<string>
        {
            "\"schema\":\"c10-bootstrap-observation.v1\"", "\"version\":" + version,
            "\"session\":" + Quote(session), "\"failure\":" + Quote(failure),
            "\"observationPassed\":" + Bool(passed), "\"featureAcceptance\":false",
            "\"oldGuardsInstalled\":" + Bool(installed), "\"finalGuardsEmitted\":" + Bool(emitted),
            "\"finalGuardsVerified\":" + Bool(verified), "\"finalCallbackEntries\":" + finalEntries,
            "\"finalGuardHashes\":[" + string.Join(",", hashes.Select(Quote)) + "]",
            "\"finalRuntimeGuardHashes\":[" + string.Join(",", runtimeHashes.Select(Quote)) + "]",
            "\"oldHarmony\":" + Identity(oldHarmony), "\"finalHarmony\":" + Identity(finalHarmony),
            "\"gameBefore\":" + Identity(gameBefore), "\"finalGame\":" + Identity(finalGame),
            "\"oldAndFinalHarmonyReferenceEqual\":" + Bool(ReferenceEquals(oldHarmony, finalHarmony)),
            "\"oldAndFinalGameReferenceEqual\":" + Bool(ReferenceEquals(gameBefore, finalGame)),
            "\"sharedDependencyAlreadyExposed\":" + Bool(shared.Length != 0),
            "\"eligibilityConditional\":true", "\"eligibility\":" + Quote(shared.Length == 0
                ? "Bootstrap observed; feature admission remains unqualified."
                : "Shared dependencies were already exposed; prior mutation state remains unqualified."),
            "\"probeLoadedCodecAssembly\":" + Bool(loadedCodec),
            "\"caseLoaded\":{\"NAudio\":" + Bool(codecs.Any(a => Name(a) == "NAudio"))
                + ",\"NVorbis\":" + Bool(codecs.Any(a => Name(a) == "NVorbis")) + "}",
            "\"codecAssemblies\":[" + string.Join(",", codecs.Select(Identity)) + "]",
            "\"sharedAtGuard\":[" + string.Join(",", shared.Select(a => "{\"assembly\":" + Identity(a)
                + ",\"sameReferenceStillLoaded\":" + Bool(after.Any(b => ReferenceEquals(a, b)))
                + ",\"sameReferenceAsLiveNamedAssembly\":" + Bool(after.Any(b => !b.ReflectionOnly && Name(a) == Name(b) && ReferenceEquals(a, b))) + "}")) + "]",
            "\"entryEvents\":[" + string.Join(",", events.Select(e => Event(e, oldHarmony, finalHarmony))) + "]"
        };
        fields.AddRange(PreloaderFields(passed, loadedCodec, oldHarmony, gameBefore, finalHarmony, finalEntries));
        File.WriteAllText(Path.Combine(directory, "c10-bootstrap-probe.json"), "{" + string.Join(",", fields) + "}" + Environment.NewLine, new UTF8Encoding(false));
    }

    private static IEnumerable<string> PreloaderFields(bool bootstrapPassed, bool loadedCodec,
        Assembly? oldHarmony, Assembly? oldGame, Assembly? finalHarmony, int finalEntries)
    {
        bool present = false, qualified = false, completed = false, truncated = false, passed = false;
        bool gamePrefix = false, prepatcherPrefix = false, extensionRecorded = false, ordered = false;
        bool cleanSnapshots = false, sameHarmony = false, sameGame = false, admissionClosed = false;
        int guardSequence = 0, gameEntries = 0, prepatcherEntries = 0, lateOmitted = 0;
        string failure = "preloader-state-missing";
        Assembly? oldPrepatcher = null, extensionHarmony = null, extensionGame = null;
        Assembly[] initial = Array.Empty<Assembly>(), afterHarmony = Array.Empty<Assembly>(), afterGuard = Array.Empty<Assembly>();
        Assembly[] initialRuntime = Array.Empty<Assembly>(), afterHarmonyRuntime = Array.Empty<Assembly>(), afterGuardRuntime = Array.Empty<Assembly>();
        object[][] chronology = Array.Empty<object[]>();
        try
        {
            if (AppDomain.CurrentDomain.GetData(StateKey) is Dictionary<string, object> state)
            {
                lock (state["gate"])
                {
                    present = state.TryGetValue("preloader", out object selected) && selected is bool enabled && enabled;
                    if (present)
                    {
                        failure = (string)state["failure"];
                        qualified = (bool)state["preloaderQualified"]; completed = (bool)state["preloaderCompleted"];
                        initial = Snapshot(state, "initialAssemblies"); afterHarmony = Snapshot(state, "afterHarmonyAssemblies");
                        afterGuard = Snapshot(state, "afterGuardAssemblies");
                        initialRuntime = Snapshot(state, "initialRuntimeAssemblies");
                        afterHarmonyRuntime = Snapshot(state, "afterHarmonyRuntimeAssemblies");
                        afterGuardRuntime = Snapshot(state, "afterGuardRuntimeAssemblies");
                        admissionClosed = (bool)state["admissionClosed"];
                        var rows = (List<object[]>)state["chronology"];
                        truncated = (bool)state["chronologyTruncated"] || rows.Count > 512;
                        if (state.TryGetValue("lateChronologyOmitted", out object omitted)) lateOmitted = (int)omitted;
                        chronology = rows.Take(512).Select(row => (object[])row.Clone()).ToArray();
                        guardSequence = (int)state["guardCompletionSequence"];
                        gameEntries = (int)state["createModClassesEntries"]; prepatcherEntries = (int)state["prepatcherConstructorEntries"];
                        gamePrefix = (bool)state["createModClassesPrefixInstalled"];
                        prepatcherPrefix = (bool)state["prepatcherConstructorPrefixInstalled"];
                        oldPrepatcher = (Assembly)state["oldPrepatcher"];
                        extensionRecorded = (bool)state["firstExtensionRecorded"];
                        extensionHarmony = (Assembly)state["firstExtensionHarmony"];
                        extensionGame = (Assembly)state["firstExtensionGame"];
                    }
                }
            }
            if (present)
            {
                // Check raw event shape before interpreting or serializing it.
                if (chronology.Where((row, index) => row.Length != 5 || row[0] is not int sequence || sequence != index + 1
                    || row[1] is not string || row[2] is not int thread || thread <= 0
                    || row[3] != null && row[3] is not Assembly || row[4] != null && row[4] is not MethodBase).Any())
                    throw new InvalidOperationException("preloader-chronology-layout");
                sameHarmony = oldHarmony != null && ReferenceEquals(extensionHarmony, oldHarmony);
                sameGame = oldGame != null && ReferenceEquals(extensionGame, oldGame);
                // Runtime membership was captured then: Prepatcher changes the
                // original assembly objects' ReflectionOnly flags during reload.
                cleanSnapshots = initialRuntime.Length != 0 && afterHarmonyRuntime.Length != 0 && afterGuardRuntime.Length != 0
                    && IsSubset(initialRuntime, initial) && IsSubset(afterHarmonyRuntime, afterHarmony) && IsSubset(afterGuardRuntime, afterGuard)
                    && !initialRuntime.Any(a => Protected(a) || Name(a) == "0Harmony")
                    && !afterHarmonyRuntime.Any(Protected) && !afterGuardRuntime.Any(Protected)
                    && afterHarmonyRuntime.Count(a => Name(a) == "0Harmony") == 1
                    && afterGuardRuntime.Count(a => Name(a) == "0Harmony") == 1
                    && afterHarmonyRuntime.Any(a => ReferenceEquals(a, oldHarmony)) && afterGuardRuntime.Any(a => ReferenceEquals(a, oldHarmony));
                object[] entry = Unique(chronology, "doorstop-entry");
                object[] harmony = Unique(chronology, "harmony-loaded");
                object[] guard = Unique(chronology, "both-old-guards-complete");
                object[] gameInstall = Unique(chronology, "old-create-mod-classes-observer-installed");
                object[] prepatcherInstall = Unique(chronology, "old-prepatcher-constructor-observer-installed");
                object[] extension = Unique(chronology, "first-wake-up-extension");
                object[][] gameCalls = chronology.Where(row => (string)row[1] == "old-create-mod-classes-entry").ToArray();
                object[][] prepatcherCalls = chronology.Where(row => (string)row[1] == "old-prepatcher-constructor-entry").ToArray();
                object[][] mutations = chronology.Where(row => (string)row[1] == "mutation-entry").ToArray();
                ordered = (int)entry[0] == 1 && (int)harmony[0] > 1 && (int)harmony[0] < guardSequence
                    && (int)guard[0] == guardSequence && ReferenceEquals(harmony[3], oldHarmony) && ReferenceEquals(guard[3], oldHarmony)
                    && EntryTarget(gameInstall, oldGame, "Verse.LoadedModManager", "CreateModClasses")
                    && EntryTarget(prepatcherInstall, oldPrepatcher, "Prepatcher.PrepatcherMod", ".ctor")
                    && (int)gameInstall[0] > guardSequence && (int)prepatcherInstall[0] > guardSequence
                    && gameCalls.Length > 0 && gameCalls.Length == gameEntries
                    && prepatcherCalls.Length > 0 && prepatcherCalls.Length == prepatcherEntries
                    && gameCalls.All(row => (int)row[0] > (int)gameInstall[0] && EntryTarget(row, oldGame, "Verse.LoadedModManager", "CreateModClasses"))
                    && prepatcherCalls.All(row => (int)row[0] > (int)prepatcherInstall[0] && EntryTarget(row, oldPrepatcher, "Prepatcher.PrepatcherMod", ".ctor"))
                    && (int)gameCalls[0][0] < (int)prepatcherCalls[0][0] && (int)prepatcherCalls[0][0] < (int)extension[0]
                    && ReferenceEquals(extension[3], extensionGame)
                    && !chronology.Any(row => (string)row[1] == "assembly-load" && row[3] is Assembly a && Protected(a) && (int)row[0] <= guardSequence)
                    && !chronology.Any(row => (string)row[1] == "assembly-load" && row[3] is Assembly a && Name(a) == "0Harmony"
                        && (int)row[0] <= guardSequence && !ReferenceEquals(a, oldHarmony))
                    && mutations.All(row => row[4] is MethodBase && (ReferenceEquals(row[3], oldHarmony) || ReferenceEquals(row[3], finalHarmony)))
                    && mutations.Any(row => ReferenceEquals(row[3], finalHarmony) && (int)row[0] > (int)extension[0]);
                passed = bootstrapPassed && !loadedCodec && qualified && completed && (admissionClosed || C10PreparedAudioProbe.Selected || C10NaturalAudioProbe.Selected || C10Mp3CompletionProbe.Selected || C10AudioMeasurementProbe.Selected) && !truncated && failure.Length == 0
                    && cleanSnapshots && gamePrefix && prepatcherPrefix && extensionRecorded && sameHarmony && sameGame
                    && oldPrepatcher != null && ordered && finalEntries > 0;
                if (!passed && failure.Length == 0) failure = "preloader-evidence-incomplete-or-inconsistent";
            }
        }
        catch (Exception exception) { failure = exception.ToString(); passed = false; }
        return new[]
        {
            "\"preloaderObservationPassed\":" + Bool(passed),
            "\"preloader\":{\"present\":" + Bool(present) + ",\"qualified\":" + Bool(qualified)
                + ",\"completed\":" + Bool(completed) + ",\"failure\":" + Quote(failure)
                + ",\"admissionClosed\":" + Bool(admissionClosed)
                + ",\"chronologyTruncated\":" + Bool(truncated) + ",\"guardCompletionSequence\":" + guardSequence
                + ",\"lateChronologyOmitted\":" + lateOmitted
                + ",\"snapshotsExcludeEarlyTargets\":" + Bool(cleanSnapshots) + ",\"entryOrderVerified\":" + Bool(ordered)
                + ",\"createModClassesPrefixInstalled\":" + Bool(gamePrefix) + ",\"prepatcherConstructorPrefixInstalled\":" + Bool(prepatcherPrefix)
                + ",\"createModClassesEntries\":" + gameEntries + ",\"prepatcherConstructorEntries\":" + prepatcherEntries
                + ",\"firstExtensionRecorded\":" + Bool(extensionRecorded) + ",\"firstExtensionHarmonyIsGuardedInstance\":" + Bool(sameHarmony)
                + ",\"firstExtensionGameIsOriginalInstance\":" + Bool(sameGame) + ",\"oldPrepatcher\":" + Identity(oldPrepatcher)
                + ",\"firstExtensionHarmony\":" + Identity(extensionHarmony) + ",\"firstExtensionGame\":" + Identity(extensionGame)
                + ",\"initialAssemblies\":" + Assemblies(initial) + ",\"afterHarmonyAssemblies\":" + Assemblies(afterHarmony)
                + ",\"afterGuardAssemblies\":" + Assemblies(afterGuard)
                + ",\"initialRuntimeAssemblies\":" + Assemblies(initialRuntime) + ",\"afterHarmonyRuntimeAssemblies\":" + Assemblies(afterHarmonyRuntime)
                + ",\"afterGuardRuntimeAssemblies\":" + Assemblies(afterGuardRuntime)
                + ",\"chronology\":[" + string.Join(",", chronology.Where(ValidRow).Select(ChronologyRow)) + "]}"
        };
    }
    private static Assembly[] Snapshot(Dictionary<string, object> state, string key)
    {
        var value = (Assembly[])state[key];
        if (value.Length > 256 || value.Any(a => a == null)) throw new InvalidOperationException("preloader-snapshot-layout: " + key);
        return value.ToArray();
    }
    private static bool Protected(Assembly assembly) => IsCodec(assembly) || Name(assembly) == "Assembly-CSharp";
    private static bool IsSubset(Assembly[] subset, Assembly[] all) => subset.All(a => all.Any(b => ReferenceEquals(a, b)));
    private static object[] Unique(object[][] rows, string name) => rows.Single(row => (string)row[1] == name);
    private static bool EntryTarget(object[] row, Assembly? assembly, string type, string method)
        => assembly != null && ReferenceEquals(row[3], assembly) && row[4] is MethodBase target
            && ReferenceEquals(target.Module.Assembly, assembly) && target.DeclaringType?.FullName == type && target.Name == method;
    private static string Assemblies(Assembly[] values) => "[" + string.Join(",", values.Select(Identity)) + "]";
    private static bool ValidRow(object[] row) => row.Length == 5 && row[0] is int && row[1] is string && row[2] is int
        && (row[3] == null || row[3] is Assembly) && (row[4] == null || row[4] is MethodBase);
    private static string ChronologyRow(object[] row) => "[" + row[0] + "," + Quote((string)row[1]) + "," + row[2]
        + "," + Identity(row[3] as Assembly) + "," + (row[4] is MethodBase target
            ? "{\"name\":" + Quote((target.DeclaringType?.FullName ?? "") + "." + target.Name)
                + ",\"assembly\":" + Identity(target.Module.Assembly) + "}" : "null") + "]";

    private static Assembly? Live(Assembly[] all, string name) => all.SingleOrDefault(a => !a.ReflectionOnly && Name(a) == name);
    private static string Name(Assembly assembly) => assembly.GetName().Name ?? "";
    private static bool IsCodec(Assembly assembly) => Name(assembly) == "NAudio" || Name(assembly) == "NVorbis";
    private static string Bool(bool value) => value ? "true" : "false";
    private static string Identity(Assembly? assembly) => assembly == null ? "null"
        : "{\"name\":" + Quote(Name(assembly)) + ",\"fullName\":" + Quote(assembly.FullName ?? "")
            + ",\"mvid\":" + Quote(assembly.ManifestModule.ModuleVersionId.ToString("D"))
            + ",\"instanceIdentity\":" + RuntimeHelpers.GetHashCode(assembly).ToString(CultureInfo.InvariantCulture)
            + ",\"reflectionOnly\":" + Bool(assembly.ReflectionOnly) + "}";
    private static string Event(object[] row, Assembly? oldHarmony, Assembly? finalHarmony)
    {
        Assembly? caller = row.Length > 0 ? row[0] as Assembly : null;
        MethodBase? target = row.Length > 1 ? row[1] as MethodBase : null;
        return "{\"caller\":" + Identity(caller) + ",\"callerIsOldHarmony\":" + Bool(ReferenceEquals(caller, oldHarmony))
            + ",\"callerIsFinalHarmony\":" + Bool(ReferenceEquals(caller, finalHarmony))
            + ",\"target\":" + Quote(target == null ? "" : (target.DeclaringType?.FullName ?? "") + "." + target.Name)
            + ",\"targetAssembly\":" + Identity(target?.Module.Assembly) + "}";
    }
    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        foreach (char c in value)
            if (c == '"' || c == '\\') result.Append('\\').Append(c);
            else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else result.Append(c);
        return result.Append('"').ToString();
    }
}
