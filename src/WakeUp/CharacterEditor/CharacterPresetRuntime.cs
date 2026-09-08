// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Verse;
using UnityEngine;

namespace WakeUp;

internal static class CharacterPresetRuntime
{
    private const string Owner = "wakeup.character-presets";
    internal const string SupplierSha256 = "BFF3B2268321F8C9566E15CBBD71CA6F975E9139F976C8BD4A9E0CA924C5B457";
    private static readonly Guid SupplierMvid = new("3db27871-e2d8-425d-98d1-a758fe85d3bd");
    // Derived from the physical pinned CharacterEditor 1.6.3.3 DLL using the
    // existing semantic-IL reader; tokens identify this exact supplier only.
    internal static readonly IReadOnlyDictionary<int, string> Bodies = new Dictionary<int, string>
    {
        [0x06000201] = "A49B2326B0203C1A85FCE052BBC1B6F6F4B37B6BD1E34861E5DA2D9B6143CAAE", // CreateDefaults
        [0x06000208] = "FDDFA8ADE318F45E683469B4976572FA06663CEEB60FA12DD46D80A5EB58399E", // ListAllTurrets
        [0x06000209] = "FC70FAAB6414EE81D526AFC01B00ED8DFC6916978DD6F5DF85993EEDF9E12B79", // DicGunAndTurret
        [0x0600020A] = "3591435C0BC7D1B892012E7C9AB17ADD820D7253952ED1E1AB627C4ECB559606", // CreateDefaultObjects
        [0x0600020B] = "56E2B433E1D0DF301F7EDC72100C5FCEEAE0E7AE97576CF1760C814171369DDB", // CreateDefaultTurrets
        [0x06000210] = "E73322DF927B5DE606BCA71499481042F531A325A8F7DD9DCD28A4B5CD6E9D3A", // PresetObject(ThingDef)
        [0x060003DB] = "595EF872EA39DB407B35338D244A38DAA6C5A793FA546816ED5799182E42F8BA", // ListBy<T>
        [0x06000642] = "481CA1D2BFBF1829FC267E8B7BB7CE3E41438CC403955552C0788454DF81B356", // KeyByValue<T1,T2>
        [0x060009AB] = "8830C6E04DAFC769EB0F21046C993BE75F62C69EBDBB4A285ED8EE0B441CFD66", // GetTurretDef
    };
    private static bool attempted;
    private static bool candidate;
    private static bool installed;
    private static volatile bool startupComplete;
    private static string? evidencePath;
    private static MethodInfo? lookup;
    private static MethodInfo? dictionaryGetter;
    private static Func<Dictionary<string, ThingDef>>? originalDictionary;
    private static MethodBase[] guardedMethods = Array.Empty<MethodBase>();
    private static readonly HashSet<MethodBase> Consumed = new();
    [ThreadStatic] private static CharacterPresetLookupScope? currentScope;

    internal static bool TryInitialize(IReadOnlyList<string> arguments)
    {
        string[] selectors = arguments.Where(a => a != null && a.StartsWith("--wake-up-character-presets=", StringComparison.Ordinal)).ToArray();
        if (selectors.Length != 1 || (selectors[0] != "--wake-up-character-presets=on" && selectors[0] != "--wake-up-character-presets=timing"))
            return false;
        if (attempted)
            return true;
        attempted = true;
        var harmony = new Harmony(Owner);
        try
        {
            StartupLaunchDecision mode = StartupLaunchSelector.Parse(arguments);
            string[] strategies = arguments.Where(a => a != null && a.StartsWith("--wake-up-strategy=", StringComparison.Ordinal)).ToArray();
            if (mode.Selection != StartupSelection.Candidate || strategies.Length != 1 || strategies[0] != "--wake-up-strategy=startup-searches")
                return true;
            evidencePath = Path.Combine(mode.SaveDataRoot!, "WakeUp", "character-presets.jsonl");
            candidate = selectors[0] == "--wake-up-character-presets=on";
            ModContentPack? supplier = LoadedModManager.RunningModsListForReading
                .SingleOrDefault(mod => string.Equals(mod.PackageId, "void.charactereditor", StringComparison.OrdinalIgnoreCase));
            if (supplier == null)
            {
                Receipt("inactive", "character-editor-not-active");
                return true;
            }
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason))
            {
                Receipt("refused", reason);
                return true;
            }
            if (!GameBuildContract.Current.HasReviewedLoadingMethods)
            {
                Receipt("refused", "unreviewed-preset-game-build");
                return true;
            }
            Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "CharacterEditor");
            if (assembly == null || !ValidateSupplier(assembly, Path.Combine(supplier.RootDir, "v1.6", "Assemblies", "CharacterEditor.dll")))
            {
                Receipt("refused", "character-editor-supplier-identity");
                return true;
            }
            if (!ValidateBodies(assembly, out reason))
            {
                Receipt("refused", reason);
                return true;
            }
            Module module = assembly.ManifestModule;
            dictionaryGetter = (MethodInfo)module.ResolveMethod(0x06000209);
            lookup = (MethodInfo)module.ResolveMethod(0x060009AB);
            MethodInfo objects = (MethodInfo)module.ResolveMethod(0x0600020A);
            MethodInfo turrets = (MethodInfo)module.ResolveMethod(0x0600020B);
            originalDictionary = (Func<Dictionary<string, ThingDef>>)Delegate.CreateDelegate(typeof(Func<Dictionary<string, ThingDef>>), dictionaryGetter);
            var tracked = Bodies.Keys.Select(token => module.ResolveMethod(token)).ToList();
            Type preset = assembly.GetType("CharacterEditor.PresetObject", true)!;
            tracked.Add(((MethodInfo)module.ResolveMethod(0x06000201)).MakeGenericMethod(preset, typeof(ThingDef)));
            tracked.Add(((MethodInfo)module.ResolveMethod(0x060003DB)).MakeGenericMethod(typeof(ThingDef)));
            tracked.Add(((MethodInfo)module.ResolveMethod(0x06000642)).MakeGenericMethod(typeof(string), typeof(ThingDef)));
            guardedMethods = tracked.ToArray();
            if (candidate)
            {
                if (!CharacterPresetPatchSnapshot.TryCapture(guardedMethods, Owner, out _))
                {
                    Receipt("refused", "foreign-preset-chain-patch");
                    return true;
                }
                harmony.Patch(lookup, transpiler: new HarmonyMethod(typeof(CharacterPresetRuntime), nameof(Transpiler)) { priority = Priority.Last });
            }
            foreach (MethodInfo method in new[] { objects, turrets })
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(CharacterPresetRuntime), nameof(ScopePrefix)),
                    finalizer: new HarmonyMethod(typeof(CharacterPresetRuntime), nameof(ScopeFinalizer)));
            harmony.Patch(AccessTools.Method(typeof(global::RimWorld.MainMenuDrawer), "MainMenuOnGUI"),
                postfix: new HarmonyMethod(typeof(CharacterPresetRuntime), nameof(Menu)));
            installed = true;
            Receipt("installed", candidate ? "scoped-original-turret-dictionary" : "original-presets-timing-only",
                "\"optimizationEnabled\":" + (candidate ? "true" : "false") + ",\"maximumEntries\":" + CharacterPresetLookupScope.MaximumEntries);
        }
        catch (Exception exception)
        {
            installed = false;
            try
            {
                harmony.UnpatchAll(Owner);
            }
            catch { }
            try
            {
                if (evidencePath != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
                    File.WriteAllText(evidencePath + ".installation.txt", exception.ToString());
                }
            }
            catch { }
            Receipt("refused", "installation-" + exception.GetType().Name);
        }
        return true;
    }

    internal static bool ValidateSupplier(Assembly assembly, string path)
    {
        if (assembly.ManifestModule.ModuleVersionId != SupplierMvid || assembly.GetName().Version?.ToString() != "1.6.3.3")
            return false;
        if (!string.IsNullOrEmpty(assembly.Location)
            && !string.Equals(Path.GetFullPath(assembly.Location), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            return false;
        using FileStream file = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return MatchesSupplierIdentity(assembly.GetName().Version?.ToString(), assembly.ManifestModule.ModuleVersionId,
            BitConverter.ToString(sha.ComputeHash(file)).Replace("-", string.Empty));
    }

    internal static bool MatchesSupplierIdentity(string? version, Guid mvid, string sha256)
        => version == "1.6.3.3" && mvid == SupplierMvid && sha256 == SupplierSha256;

    internal static bool ValidateBodies(Assembly assembly, out string reason)
    {
        foreach (KeyValuePair<int, string> entry in Bodies)
        {
            MethodBase method = assembly.ManifestModule.ResolveMethod(entry.Key);
            if (!SemanticMethodIdentity.TryHash(method, out string hash, out string failure) || hash != entry.Value)
            {
                reason = "preset-body-" + entry.Key.ToString("x8", CultureInfo.InvariantCulture) + "-" + hash + "-" + failure;
                return false;
            }
        }
        reason = "exact";
        return true;
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (lookup == null || dictionaryGetter == null)
            throw new InvalidOperationException("Preset lookup unavailable.");
        // A foreign earlier transform must keep its ordinary fresh dictionary.
        if (!InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(lookup)))
            return code;
        return RedirectDictionaryCall(code, dictionaryGetter,
            AccessTools.Method(typeof(CharacterPresetRuntime), nameof(GetDictionary)));
    }

    internal static IReadOnlyList<CodeInstruction> RedirectDictionaryCall(IEnumerable<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (code.Count(i => i.opcode == OpCodes.Call && Equals(i.operand, original)) != 1)
            throw new InvalidOperationException("Unique GetTurretDef dictionary callsite unavailable.");
        foreach (CodeInstruction instruction in code)
            if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, original))
                instruction.operand = replacement;
        return code;
    }

    private static Dictionary<string, ThingDef> GetDictionary() => currentScope?.Get() ?? originalDictionary!();

    private sealed class ScopeState
    {
        internal readonly Stopwatch Watch = Stopwatch.StartNew();
        internal CharacterPresetLookupScope? Previous;
        internal CharacterPresetLookupScope? Scope;
        internal string Reason = "original";
    }

    private static void ScopePrefix(MethodBase __originalMethod, out ScopeState __state)
    {
        __state = new ScopeState { Previous = currentScope };
        currentScope = null; // Nested creation must not borrow the outer scope.
        try
        {
            bool first;
            lock (Consumed)
                first = Consumed.Add(__originalMethod);
            if (!installed || !candidate || !first || __state.Previous != null)
                return;
            if (startupComplete)
            {
                __state.Reason = "startup-menu-already-complete";
                return;
            }
            if (!CharacterPresetPatchSnapshot.TryCapture(guardedMethods, Owner, out CharacterPresetPatchSnapshot? patches))
            {
                __state.Reason = "foreign-preset-chain-patch";
                return;
            }
            // The pinned synchronous constructors write their own presets, not
            // the definition database. A changed list membership/order during
            // this scope disables reuse. No whole-game cache survives the call.
            List<ThingDef> definitions = DefDatabase<ThingDef>.AllDefsListForReading;
            FieldInfo? version = AccessTools.Field(typeof(List<ThingDef>), "_version");
            if (version == null || version.GetValue(definitions) is not int initialVersion)
            {
                __state.Reason = "definition-version-unavailable";
                return;
            }
            __state.Scope = new CharacterPresetLookupScope(originalDictionary!, () =>
                patches!.Unchanged() && ReferenceEquals(definitions, DefDatabase<ThingDef>.AllDefsListForReading)
                && Equals(version.GetValue(definitions), initialVersion));
            currentScope = __state.Scope;
            __state.Reason = "scoped-original-turret-dictionary";
        }
        catch { __state.Reason = "scope-admission-failed"; }
    }

    private static Exception? ScopeFinalizer(MethodBase __originalMethod, ScopeState? __state, object? __result, Exception? __exception)
    {
        if (__state == null)
            return __exception;
        __state.Watch.Stop();
        CharacterPresetLookupScope? scope = __state.Scope;
        scope?.Dispose();
        currentScope = __state.Previous;
        string digest;
        try
        {
            digest = PresetDigest(__result as IDictionary);
        }
        catch { digest = "unavailable"; }
        Receipt("scope-complete", __state.Reason,
            "\"method\":\"" + __originalMethod.Name + "\",\"milliseconds\":" + __state.Watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
            + ",\"presetSha256\":\"" + digest + "\""
            + ",\"resultCount\":" + ((__result as IDictionary)?.Count ?? 0) + ",\"dictionaryBuilds\":" + (scope?.Builds ?? 0)
            + ",\"dictionaryHits\":" + (scope?.Hits ?? 0) + ",\"fallbacks\":" + (scope?.Fallbacks ?? 0)
            + ",\"retainedEntriesAfterScope\":" + (scope?.RetainedEntries ?? 0) + ",\"exception\":" + (__exception == null ? "false" : "true"));
        return __exception;
    }

    private static void Menu(bool __runOriginal)
    {
        if (__runOriginal && Event.current != null && Event.current.type == EventType.Repaint
            && Current.ProgramState == ProgramState.Entry && PlayDataLoader.Loaded)
            startupComplete = true;
    }

    // Read stored preset values, without calling supplier getters or rebuilding
    // presets. Both comparison arms record the same canonical content digest.
    internal static string PresetDigest(IDictionary? presets)
    {
        if (presets == null)
            return "unavailable";
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
        {
            foreach (string key in presets.Keys.Cast<string>().OrderBy(k => k, StringComparer.Ordinal))
            {
                writer.Write(key);
                var field = AccessTools.Field(presets[key].GetType(), "dicParams");
                if (field?.GetValue(presets[key]) is not IDictionary values)
                    return "unavailable";
                writer.Write(values.Count);
                foreach (object valueKey in values.Keys.Cast<object>().OrderBy(k => Convert.ToInt32(k, CultureInfo.InvariantCulture)))
                {
                    writer.Write(Convert.ToInt32(valueKey, CultureInfo.InvariantCulture));
                    writer.Write(values[valueKey] is string);
                    writer.Write(values[valueKey] as string ?? "");
                }
            }
        }
        bytes.Position = 0;
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
    }

    private static void Receipt(string kind, string reason, string? fields = null)
        => JsonLineLog.WriteReceipt(evidencePath, kind, reason, fields);
}
