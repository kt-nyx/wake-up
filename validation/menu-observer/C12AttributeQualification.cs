// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace FixtureMenuObserver;

// Shared focused check runs on the modern test host and the actual fixture Mono.
// Hooks affect only this private marker Def's fields and are always removed.
public static class C12AttributeQualification
{
    private const string Owner = "local.fixture.c12-attribute-qualification";
    private static FieldInfo? target;
    private static int calls;
    private static bool throwFromHook;

    public static MethodInfo Closed(Type attribute) => AccessTools.Method(typeof(GenAttribute), "HasAttribute").MakeGenericMethod(attribute);
    public static void Install(Type attribute, string field, bool throws)
    {
        target = typeof(C12AttributeProbeDef).GetField(field);
        calls = 0; throwFromHook = throws;
        new Harmony(Owner).Patch(Closed(attribute), prefix: new HarmonyMethod(typeof(C12AttributeQualification), nameof(Prefix)));
    }
    public static void Remove()
    {
        new Harmony(Owner).UnpatchAll(Owner);
        target = null; throwFromHook = false;
    }
    private static bool Prefix(MemberInfo __0, ref bool __result)
    {
        if (!Equals(__0, target)) return true;
        calls++;
        if (throwFromHook) throw new InvalidOperationException("C12 attribute callback exception");
        __result = false;
        return false;
    }

    // No Mono helper detour is installed: check ordinary decisions through the
    // actual setter while member reuse is still active during startup.
    public static string ExerciseNative(string field)
    {
        var definition = new C12AttributeProbeDef { defName = "C12AttributeProbe" };
        object? oldKeys = AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").GetValue(null);
        object? oldSuggestions = AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").GetValue(null);
        try
        {
            AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").SetValue(null, new Dictionary<string, string>());
            AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").SetValue(null, new Dictionary<string, string>());
            DefDatabase<C12AttributeProbeDef>.Add(definition);
            for (int i = 0; i < 2; i++)
            {
                definition.text = "original"; definition.unsaved = "original";
                definition.words = new List<string> { "original" };
                var observed = Inject(definition, field);
                bool expected = field == "words";
                if (observed.injected != expected)
                    throw new InvalidOperationException("Unexpected native attribute decision: " + field + ":" + observed.errors);
                object? value = typeof(C12AttributeProbeDef).GetField(field)!.GetValue(definition);
                if (field != "words" && (value as string) != (expected ? "translated" : "original"))
                    throw new InvalidOperationException("Unexpected native field assignment.");
                if (field == "words" && !((IEnumerable<string>)value!).SequenceEqual(new[] { "translated", "additional" }))
                    throw new InvalidOperationException("Native list translation was not applied.");
            }
            return field + ":native-decisions=2:passed";
        }
        finally
        {
            DefDatabase<C12AttributeProbeDef>.Clear();
            AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").SetValue(null, oldKeys);
            AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").SetValue(null, oldSuggestions);
        }
    }

    private static (bool injected, string errors) Inject(C12AttributeProbeDef definition, string field)
    {
        var package = new DefInjectionPackage(typeof(C12AttributeProbeDef));
        string path = definition.defName + "." + field;
        var injection = new DefInjectionPackage.DefInjection {
            path = path, nonBackCompatiblePath = path, injection = "translated", fileSource = "c12-attribute-probe.xml" };
        if (field == "words") injection.fullListInjection = new List<string> { "translated", "additional" };
        package.injections.Add(path, injection);
        package.InjectIntoDefs(false);
        return (injection.injected, string.Join("\n", package.loadErrors));
    }
}

public sealed class C12AttributeProbeDef : Def
{
    [NoTranslate] public string text = "original";
    [Unsaved] public string unsaved = "original";
    [TranslationCanChangeCount] public List<string> words = new() { "original" };
}

[StaticConstructorOnStartup]
public static class C12AttributeMonoProbe
{
    static C12AttributeMonoProbe()
    {
        if (!Environment.GetCommandLineArgs().Contains("--fixture-functional-probes")) return;
        string directory = Path.Combine(GenFilePaths.SaveDataFolderPath, "FixtureMenuObserver");
        Directory.CreateDirectory(directory);
        try
        {
            var rows = new List<string> { "memberHitsBeforeProbe=" + (Counter("Hits") - Counter("AttributeHits")),
                "attributeHitsBeforeProbe=" + Counter("AttributeHits"), "attributeMissesBeforeProbe=" + Counter("AttributeMisses") };
            MethodBase setter = AccessTools.Method(typeof(DefInjectionPackage), "SetDefFieldAtPath");
            var nativeCalls = AttributeCalls(PatchProcessor.GetOriginalInstructions(setter));
            var currentCalls = AttributeCalls(PatchProcessor.GetCurrentInstructions(setter));
            if (nativeCalls.Length != 6 || !nativeCalls.SequenceEqual(currentCalls))
                throw new InvalidOperationException("Effective closed attribute call instructions differ from native.");
            rows.Add("exactNativeAttributeCalls=" + nativeCalls.Length);
            foreach (string field in new[] { "text", "unsaved", "words" })
                rows.Add(C12AttributeQualification.ExerciseNative(field));
            rows.Add("memberHitsAfterProbe=" + (Counter("Hits") - Counter("AttributeHits")));
            rows.Add("attributeHitsAfterProbe=" + Counter("AttributeHits"));
            rows.Add("attributeMissesAfterProbe=" + Counter("AttributeMisses"));
            File.WriteAllLines(Path.Combine(directory, "c12-attribute-mono-passed.txt"), rows);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "c12-attribute-mono-error.txt"), e.ToString());
        }
    }

    private static (System.Reflection.Emit.OpCode, MethodInfo)[] AttributeCalls(IEnumerable<CodeInstruction> code) => code
        .Where(i => i.operand is MethodInfo m && m.DeclaringType == typeof(GenAttribute) && m.Name == "HasAttribute")
        .Select(i => (i.opcode, (MethodInfo)i.operand)).ToArray();

    private static long Counter(string name)
    {
        Type? runtime = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "WakeUp")?.GetType("WakeUp.LoadingReflectionRuntime");
        object? index = runtime?.GetField("startup", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        return index == null ? -1 : (long?)index.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(index) ?? 0;
    }
}
