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
            Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "CharacterEditor");
            if (assembly == null)
                throw new InvalidOperationException("character-editor-assembly-unavailable");
            var methods = SupplierMethodContract.ResolveAll(assembly, SupplierContracts.Character);
            dictionaryGetter = (MethodInfo)methods["Dictionary"];
            lookup = (MethodInfo)methods["Lookup"];
            MethodInfo objects = (MethodInfo)methods["Objects"];
            MethodInfo turrets = (MethodInfo)methods["Turrets"];
            originalDictionary = (Func<Dictionary<string, ThingDef>>)Delegate.CreateDelegate(typeof(Func<Dictionary<string, ThingDef>>), dictionaryGetter);
            var tracked = methods.Values.ToList();
            Type preset = assembly.GetType("CharacterEditor.PresetObject", true)!;
            tracked.Add(((MethodInfo)methods["CreateDefaults"]).MakeGenericMethod(preset, typeof(ThingDef)));
            tracked.Add(((MethodInfo)methods["ListBy"]).MakeGenericMethod(typeof(ThingDef)));
            tracked.Add(((MethodInfo)methods["KeyByValue"]).MakeGenericMethod(typeof(string), typeof(ThingDef)));
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
            Receipt("refused", "installation-" + exception.GetType().Name + ": " + exception.Message);
        }
        return true;
    }

    internal static bool ValidateBodies(Assembly assembly, out string reason)
    {
        try { SupplierMethodContract.ResolveAll(assembly, SupplierContracts.Character); reason = "compatible-methods"; return true; }
        catch (Exception e) { reason = e.Message; return false; }
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
