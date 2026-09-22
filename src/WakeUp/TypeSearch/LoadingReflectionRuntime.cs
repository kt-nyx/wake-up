// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using Verse;

namespace WakeUp;

internal static class LoadingReflectionRuntime
{
    private const string Owner = "wakeup.loading-reflection";
    private const BindingFlags DefaultBindings = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    private static LoadingReflectionIndex? startup;
    private static readonly Dictionary<MethodBase, PublishedPatchGuard> guards = new();
    private static bool installed;
    private static int generation;
    [ThreadStatic] private static LoadingReflectionIndex? language;
    [ThreadStatic] private static int languageDepth;
    [ThreadStatic] private static PublishedPatchGuard? consumer;

    // Pinned GOG discovery and scope bodies. Generic attribute callers retain
    // their original operands; the early helper-body rewrite is separate.
    internal static readonly string[] ExpectedBodies = {
        "827901E8E83ADE4AF2A62FC33D73F59AD2E5224F52F107204BE461A2C47F8C56",
        "EFE3D12811D4CB2EFEB46222DC325FC2F0FC3E73A1A493DFEAB3075A735C1BAC",
        "2CAA55EAE0643AD3B3DE70CA29A9AEDE0E22BCFDE9AF801BFE18744C8880D1A2",
        "1D6574BF2CA2ACAF6DF864B0A10C483B5508CC89AB6ED89BA5515423E3D55954",
        "4EC29778E3981559960E329E6146DE2E22FE8E1F6C6F1E7D76B96325F0E10858",
        "D398F58613446091D39106A9CB6D3C25B4D5047EF1DB600AE210D549586B1478",
        "7E04C6C715F30A3A656137B9B794F3388891AEC2299EA60DEBA7EB71908C91CB",
        "9280158E0526F44CCAF3796DB43543D615BD704A3A3CE77F5AD244D3CB116A0F",
        "D840477B3CCAB3CEBE0B25FE2911120F43997B77FB43FA6440E62EC5519CE3BA",
        "A8ED272876D8EB49D91EC2358D003BFA62C18A11B112F836B3703C9BF80E2FC1",
        "01FA7D1614AC0F2B858182A669156DF1C3044349E3C63522F668C71577975906",
        "1DD00A2C1AA9F5C3D59EC67046667BF46D2131670EE3DD6B00C89E535A34B16F",
        "2F0080750996E398949319904976979BEBDEFBDFBBB45821D7082B26E98A90BE",
        "6C93B2A5C98135B22AFA2EE3F170608BFBC60FE31801E9EB88EE5B0867F0EF94",
        "70CA4C78EF28FA081F4E8EF9E2BF1AD0F1B08E7349384336E87F2EC9229A6718"
    };

    internal static MethodBase[] ConsumerMethods()
    {
        var result = new List<MethodBase>();
        foreach (Type third in new[] { typeof(string), typeof(System.Xml.XmlNode) })
            result.Add(AccessTools.Method(typeof(DirectXmlCrossRefLoader), "RegisterObjectWantsCrossRef",
                new[] { typeof(object), typeof(string), third, typeof(string), typeof(string), typeof(Type) }));
        result.Add(AccessTools.Method(typeof(DefInjectionPackage), "SetDefFieldAtPath"));
        result.Add(AccessTools.Method(typeof(DefInjectionPackage), "GetFieldNamed"));
        result.Add(AccessTools.Method(typeof(GenGeneric), "MethodOnGenericType"));
        result.Add(AccessTools.Method(typeof(GenGeneric), "InvokeGenericMethod"));
        result.Add(AccessTools.Method(typeof(GenGeneric), "InvokeStaticGenericMethod", new[] { typeof(Type), typeof(Type), typeof(string) }));
        result.Add(AccessTools.Method(typeof(GenGeneric), "InvokeStaticGenericMethod", new[] { typeof(Type), typeof(Type), typeof(string), typeof(object[]) }));
        result.Add(AccessTools.Method(typeof(GenGeneric), "PropertyOnGenericType"));
        result.Add(AccessTools.Method(typeof(XmlToObjectUtils), "DoFieldSearch"));
        result.Add(AccessTools.Method(typeof(XmlHelper), "ParseElements"));
        result.Add(AccessTools.Method(typeof(XmlHelper), "TryParseSingleDefault"));
        return result.ToArray();
    }

    internal static MethodBase[] ContractMethods() => ConsumerMethods().Concat(new MethodBase[] {
        AccessTools.Method(typeof(XmlToObjectUtils), "DirectGetFieldByName"),
        AccessTools.Method(typeof(LoadedLanguage), "InjectIntoData_BeforeImpliedDefs"),
        AccessTools.Method(typeof(LoadedLanguage), "InjectIntoData_AfterImpliedDefs") }).ToArray();

    internal static void Initialize()
    {
        if (installed) return;
        var harmony = new Harmony(Owner);
        try
        {
            MethodBase[] methods = ContractMethods();
            if (methods.Length != ExpectedBodies.Length) throw new InvalidOperationException("unqualified-reflection-contract");
            guards.Clear();
            for (int i = 0; i < methods.Length; i++)
            {
                if (!SemanticMethodIdentity.TryHash(methods[i], out string body, out _) || body != ExpectedBodies[i])
                {
                    TypeLookupRuntime.LeafReceipt("reflection-consumer-refused", "changed-native-" + methods[i].DeclaringType!.Name + "." + methods[i].Name);
                    continue;
                }
                if (!PublishedPatchGuard.TryCreate(methods[i], Owner, out PublishedPatchGuard? check, allPatchKinds: true,
                        allowedForeignPatch: IsKnownTranslationPatch)
                    || !check!.AllowsOriginalContract())
                {
                    TypeLookupRuntime.LeafReceipt("reflection-consumer-refused", "foreign-contract-" + methods[i].DeclaringType!.Name + "." + methods[i].Name);
                    continue;
                }
                guards.Add(methods[i], check);
            }
            int patched = 0;
            foreach (MethodBase method in ConsumerMethods())
            {
                if (!guards.ContainsKey(method)) continue;
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(LoadingReflectionRuntime), nameof(BeginConsumer)),
                    finalizer: new HarmonyMethod(typeof(LoadingReflectionRuntime), nameof(EndConsumer)),
                    transpiler: new HarmonyMethod(typeof(LoadingReflectionRuntime), nameof(Transpiler)) { priority = Priority.Last });
                patched++;
            }
            // This native method performs attribute work only. It establishes
            // caller admission without demanding a member-lookup replacement.
            MethodBase attributeScope = AccessTools.Method(typeof(XmlToObjectUtils), "DirectGetFieldByName");
            if (guards.ContainsKey(attributeScope))
                harmony.Patch(attributeScope, prefix: new HarmonyMethod(typeof(LoadingReflectionRuntime), nameof(BeginConsumer)),
                    finalizer: new HarmonyMethod(typeof(LoadingReflectionRuntime), nameof(EndConsumer)));
            foreach (MethodBase method in methods.Skip(methods.Length - 2))
            {
                if (!guards.ContainsKey(method)) continue;
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(LoadingReflectionRuntime), nameof(BeginLanguage)),
                    finalizer: new HarmonyMethod(typeof(LoadingReflectionRuntime), nameof(EndLanguage)));
            }
            if (patched == 0) throw new InvalidOperationException("no-supported-reflection-consumers");
            AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded;
            startup = new LoadingReflectionIndex();
            installed = true;
            LoadingAttributeRuntime.Initialize();
            TypeLookupRuntime.LeafReceipt("reflection-installed", "loading-consumer-metadata-only", "\"supportedConsumers\":" + patched);
        }
        catch (Exception exception)
        {
            installed = false;
            try { harmony.UnpatchAll(Owner); } catch { }
            AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded;
            TypeLookupRuntime.LeafReceipt("reflection-refused", exception.Message);
        }
    }

    private static void AssemblyLoaded(object sender, AssemblyLoadEventArgs args) => Interlocked.Increment(ref generation);
    private static bool Selected => installed && TypeLookupRuntime.CandidateSelected;
    private static LoadingReflectionIndex? Current => Selected && consumer?.AllowsOriginalContract() == true
        ? (TypeLookupRuntime.ActiveCandidate ? startup : language) : null;
    internal static LoadingReflectionIndex? AttributeIndex => Current;
    internal static int Generation => Volatile.Read(ref generation);

    internal static void BeginConsumer(MethodBase __originalMethod, out PublishedPatchGuard? __state)
    {
        __state = consumer;
        guards.TryGetValue(__originalMethod, out consumer);
    }

    internal static void EndConsumer(PublishedPatchGuard? __state) => consumer = __state;

    internal static void BeginLanguage(MethodBase __originalMethod, out bool __state)
    {
        __state = Selected && guards.TryGetValue(__originalMethod, out PublishedPatchGuard? guard) && guard.AllowsOriginalContract();
        if (!__state) return;
        if (languageDepth++ == 0 && !TypeLookupRuntime.ActiveCandidate) language = new LoadingReflectionIndex();
    }

    internal static void EndLanguage(bool __state)
    {
        if (!__state || --languageDepth != 0) return;
        LoadingReflectionIndex? done = language;
        language = null;
        if (done != null) Finish(done, "language-injection");
    }

    internal static void Complete()
    {
        LoadingReflectionIndex? done = Interlocked.Exchange(ref startup, null);
        if (done != null) Finish(done, "startup");
        // Assembly generation observation remains subscribed for the bounded
        // later language sessions; it owns no metadata or game objects.
    }

    private static void Finish(LoadingReflectionIndex index, string scope)
    {
        index.Clear();
        TypeLookupRuntime.LeafReceipt("reflection-loading-complete", scope,
            "\"hits\":" + index.Hits + ",\"misses\":" + index.Misses + ",\"fieldHits\":" + index.FieldHits
            + ",\"methodHits\":" + index.MethodHits + ",\"propertyHits\":" + index.PropertyHits
            + ",\"fieldArrayHits\":" + index.FieldArrayHits + ",\"attributeExistenceHits\":" + index.AttributeHits
            + ",\"attributeExistenceMisses\":" + index.AttributeMisses + ",\"attributeLookup\":\"" + LoadingAttributeRuntime.Status + "\""
            + ",\"retainedMetadata\":0");
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (Harmony.GetPatchInfo(__originalMethod)?.Transpilers.Any(p => p.owner != Owner && !IsKnownTranslationPatch(p)) == true)
            return code;
        List<CodeInstruction> native = PatchProcessor.GetOriginalInstructions(__originalMethod).ToList();
        bool unchanged = InstructionComparison.SameInstructions(code, native);
        if (!unchanged && __originalMethod.DeclaringType == typeof(DefInjectionPackage) && __originalMethod.Name == "SetDefFieldAtPath")
            unchanged = InstructionComparison.SameInstructions(code, TranslationRuntime.DuplicateTranspiler(native).ToList());
        if (!unchanged) return code;
        int changed = 0;
        foreach (CodeInstruction instruction in code)
        {
            if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
                || !(instruction.operand is MethodInfo method)) continue;
            MethodInfo? replacement = Replacement(method);
            if (replacement == null) continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = replacement;
            changed++;
        }
        if (changed == 0) throw new InvalidOperationException("missing-loading-reflection-calls-" + __originalMethod.Name);
        return code;
    }

    // The existing translation optimization changes only duplicate enumeration;
    // these exact callbacks can compose with metadata-only replacements. Unknown
    // owner callbacks retain the ordinary path rather than being allowlisted.
    private static bool IsKnownTranslationPatch(Patch patch) => patch.owner == TranslationRuntime.Owner
        && patch.PatchMethod.DeclaringType == typeof(TranslationRuntime)
        && new[] { "BeginSetter", "EndSetter", nameof(TranslationRuntime.DuplicateTranspiler) }
            .Any(name => patch.PatchMethod == AccessTools.Method(typeof(TranslationRuntime), name));

    internal static bool IsKnownPatch(Patch patch) => patch.owner == Owner
        && patch.PatchMethod.DeclaringType == typeof(LoadingReflectionRuntime)
        && new[] { nameof(BeginConsumer), nameof(EndConsumer), nameof(Transpiler) }
            .Any(name => patch.PatchMethod == AccessTools.Method(typeof(LoadingReflectionRuntime), name));

    internal static List<CodeInstruction> RestoreNativeCalls(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        foreach (CodeInstruction instruction in code)
        {
            if (instruction.opcode != OpCodes.Call || !(instruction.operand is MethodInfo method)
                || method.DeclaringType != typeof(LoadingReflectionRuntime)) continue;
            string? nativeName = method.Name == nameof(Field) || method.Name == nameof(DefaultField) ? "GetField"
                : method.Name == nameof(Property) || method.Name == nameof(DefaultProperty) ? "GetProperty"
                : method.Name == nameof(Method) ? "GetMethod" : method.Name == nameof(Fields) ? "GetFields" : null;
            if (nativeName == null) continue;
            Type[] parameters = method.GetParameters().Skip(1).Select(p => p.ParameterType).ToArray();
            instruction.operand = typeof(Type).GetMethod(nativeName, parameters)!;
            instruction.opcode = OpCodes.Callvirt;
        }
        return code;
    }

    internal static MethodInfo? Replacement(MethodInfo method)
    {
        // Keep the exact native attribute call, including its closed type and
        // dispatch. The separate early rewrite changes only the nongeneric
        // operation inside that helper, preserving its entry and hooks.
        if (method.DeclaringType != typeof(Type)) return null;
        Type[] parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
        string? name = null;
        if (parameters.SequenceEqual(new[] { typeof(string), typeof(BindingFlags) }))
            name = method.Name == "GetField" ? nameof(Field) : method.Name == "GetMethod" ? nameof(Method)
                : method.Name == "GetProperty" ? nameof(Property) : null;
        else if (parameters.SequenceEqual(new[] { typeof(string) }))
            name = method.Name == "GetField" ? nameof(DefaultField) : method.Name == "GetProperty" ? nameof(DefaultProperty) : null;
        else if (method.Name == "GetFields" && parameters.SequenceEqual(new[] { typeof(BindingFlags) })) name = nameof(Fields);
        return name == null ? null : AccessTools.Method(typeof(LoadingReflectionRuntime), name);
    }

    internal static FieldInfo? Field(Type type, string name, BindingFlags flags)
        => Current is LoadingReflectionIndex index ? index.Field(type, name, flags, Volatile.Read(ref generation)) : type.GetField(name, flags);
    internal static FieldInfo? DefaultField(Type type, string name) => Field(type, name, DefaultBindings);
    internal static MethodInfo? Method(Type type, string name, BindingFlags flags)
        => Current is LoadingReflectionIndex index ? index.Method(type, name, flags, Volatile.Read(ref generation)) : type.GetMethod(name, flags);
    internal static PropertyInfo? Property(Type type, string name, BindingFlags flags)
        => Current is LoadingReflectionIndex index ? index.Property(type, name, flags, Volatile.Read(ref generation)) : type.GetProperty(name, flags);
    internal static PropertyInfo? DefaultProperty(Type type, string name) => Property(type, name, DefaultBindings);
    internal static FieldInfo[] Fields(Type type, BindingFlags flags)
        => Current is LoadingReflectionIndex index ? index.Fields(type, flags, Volatile.Read(ref generation)) : type.GetFields(flags);
}
