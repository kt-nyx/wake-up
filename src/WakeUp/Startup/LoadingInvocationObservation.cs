// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Observation at synchronous invocation boundaries. Selection never installs a
// scheduler, replaces a loop, enumerates a provider iterator, or enables drawing.
internal static class LoadingInvocationObservation
{
    internal const string Owner = "wakeup.loading-invocations";
    internal static readonly MethodInfo Constructor = AccessTools.Method(typeof(RuntimeHelpers), nameof(RuntimeHelpers.RunClassConstructor), new[] { typeof(RuntimeTypeHandle) });
    internal static readonly MethodInfo Invoke = AccessTools.Method(typeof(Action), nameof(Action.Invoke));
    internal static readonly MethodInfo NativeConstructors = AccessTools.Method(typeof(StaticConstructorOnStartupUtility), "CallAll");
    internal static readonly MethodInfo NativeCallbacks = AccessTools.Method(typeof(LongEventHandler), "ExecuteToExecuteWhenFinished");
    private static readonly Dictionary<MethodBase, string> targets = new();
    private static FieldInfo? providerMod;
    internal static string Status { get; private set; } = "Individual invocation observation is off.";

    internal static void Install()
    {
        var harmony = new Harmony(Owner);
        try
        {
            Admit(NativeConstructors, NativeLoadingUnits.CallAllBody);
            if (!AtlasContract.Matches(NativeCallbacks)) throw new InvalidOperationException("Native callback body changed");
            Patch(harmony, NativeConstructors, nameof(RewriteConstructors));
            Patch(harmony, NativeCallbacks, nameof(RewriteCallbacks));
            Status = "Native constructor and callback invocations observed independently of the display. CLR initialization checks can be no-ops for initialized types.";
        }
        catch (Exception error)
        {
            harmony.UnpatchAll(Owner); targets.Clear();
            Status = "Individual native invocations unobserved: " + error.Message;
            LoadingObservationRuntime.Current?.NoteUnobserved(Status);
            return;
        }
        if (!LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == LoadingProgressCompatibility.PackageId)) return;
        try
        {
            if (!LoadingProgressCompatibility.TryInitialize()) throw new InvalidOperationException("Loading Progress assembly unavailable");
            InstallProvider(harmony, LoadingProgressCompatibility.FrameworkAssembly!);
            Status += " Qualified Loading Progress constructor/callback callsites and actual content iterator steps are observed; provider scheduling and drawing remain unchanged.";
        }
        catch (Exception error)
        {
            // Keep native observation. Unknown provider regions stay explicit.
            foreach (var target in targets.Keys.Where(m => m != NativeConstructors && m != NativeCallbacks).ToArray())
            {
                harmony.Unpatch(target, HarmonyPatchType.All, Owner);
                targets.Remove(target);
            }
            providerMod = null;
            Status += " Loading Progress execution detail unobserved: " + error.Message;
            LoadingObservationRuntime.Current?.NoteUnobserved("Loading Progress execution detail unavailable: " + error.Message);
        }
    }

    internal static MethodInfo ProviderIterator(Assembly assembly, string type, string method)
    {
        var factory = AccessTools.Method(assembly.GetType("ilyvion.LoadingProgress." + type, true), method);
        var attribute = factory.GetCustomAttributes(typeof(IteratorStateMachineAttribute), false).Cast<IteratorStateMachineAttribute>().Single();
        return AccessTools.Method(attribute.StateMachineType, "MoveNext");
    }
    internal static readonly string[] ProviderBodies = {
        "38F307E8D3A8D14C2C4C22186FCCECB73359B055495095D951B49075176B7D31", "F74D9A85DC0815F20088C88411F546C749E46EFAAC2C4EAE738794636E19A782", "CB7EF9E6F51A8EBB4870A974B7E06FFE5985D9F0F9B9BD10F0329FEA4A7A9560" };
    // Qualification identity of the original supplier file, not a runtime gate.
    internal const string ProviderAssemblyHash = "4F19F836FD41C8E702295CB90D7D5E66C53F38CDA8A13A3ED79F1D063E1AFCEF";
    internal static MethodInfo[] ProviderMethods(Assembly assembly) => new[] {
        ProviderIterator(assembly, "StaticConstructorOnStartupUtilityReplacement", "CallAllAndRest"),
        ProviderIterator(assembly, "LongEventHandler_ExecuteToExecuteWhenFinished_Patches", "ExecuteToExecuteWhenFinished"),
        ProviderIterator(assembly, "ReloadContentIntReplacement", "ReloadContentInt") };

    internal static MethodInfo[] AdmitProvider(Assembly assembly)
    {
        var methods = ProviderMethods(assembly);
        // Resolve actual operands, not file locations, MVIDs or metadata rows.
        // The original callback's Mono API is qualified by the existing modern
        // test host; the other two bodies also match the .NET Framework host.
        for (int i = 0; i < methods.Length; i++) Admit(methods[i], i == 1 ? LoadingCallbackPrepatch.RuntimeBody : ProviderBodies[i]);
        return methods;
    }
    private static void InstallProvider(Harmony harmony, Assembly assembly)
    {
        var methods = AdmitProvider(assembly);
        var field = AccessTools.Field(methods[2].DeclaringType, "modContentPack");
        if (field?.FieldType != typeof(ModContentPack)) throw new InvalidOperationException("Provider content iterator ownership field changed");
        // Admit all boundaries before adding any. Factory creation is never a span.
        providerMod = field;
        Patch(harmony, methods[0], nameof(RewriteConstructors));
        // Its exact pre-load Action wrapper avoids Harmony's fault-region
        // rewrite; the native/other supplier iterator bodies stay untouched.
        targets[methods[2]] = nameof(BeforeProviderStep);
        harmony.Patch(methods[2], prefix: new HarmonyMethod(typeof(LoadingInvocationObservation), nameof(BeforeProviderStep)),
            finalizer: new HarmonyMethod(typeof(LoadingInvocationObservation), nameof(AfterProviderStep)));
    }
    private static void Admit(MethodInfo method, string expected)
    {
        if (!SemanticMethodIdentity.TryHash(method, out string actual, out _) || actual != expected)
            throw new InvalidOperationException("Execution boundary changed: " + method.DeclaringType + "." + method.Name + " [" + actual + "]");
    }
    private static void Patch(Harmony harmony, MethodInfo target, string hook)
    {
        targets[target] = hook;
        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(LoadingInvocationObservation), hook));
    }
    internal static bool AllowsHook(MethodBase target, Patch patch)
    {
        if (patch.owner != Owner || !targets.TryGetValue(target, out string expected)) return false;
        bool content = expected == nameof(BeforeProviderStep);
        if (content && patch.PatchMethod == AccessTools.Method(typeof(LoadingInvocationObservation), nameof(AfterProviderStep))) expected = nameof(AfterProviderStep);
        if (patch.PatchMethod != AccessTools.Method(typeof(LoadingInvocationObservation), expected)) return false;
        var record = Harmony.GetPatchInfo(target);
        if (record == null) return false;
        var kind = content ? expected == nameof(AfterProviderStep) ? record.Finalizers : record.Prefixes : record.Transpilers;
        return kind.Count(p => p.owner == Owner && p.PatchMethod == patch.PatchMethod) == 1
            && record.Prefixes.Concat(record.Postfixes).Concat(record.Transpilers).Concat(record.Finalizers)
                .Concat(record.InnerPrefixes).Concat(record.InnerPostfixes).Count(p => p.PatchMethod == patch.PatchMethod) == 1;
    }
    internal static IEnumerable<CodeInstruction> RewriteConstructors(IEnumerable<CodeInstruction> instructions)
        => Rewrite(instructions, Constructor, nameof(RunConstructor));
    internal static IEnumerable<CodeInstruction> RewriteCallbacks(IEnumerable<CodeInstruction> instructions)
        => Rewrite(instructions, Invoke, nameof(ExecuteAction));
    private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodInfo call, string wrapper)
    {
        var code = instructions.ToList();
        if (code.Count(i => i.Calls(call)) != 1)
        {
            LoadingObservationRuntime.Current?.NoteUnobserved("Invocation observation did not replace a changed callsite: " + call.Name);
            return code;
        }
        var instruction = code.Single(i => i.Calls(call));
        instruction.opcode = OpCodes.Call;
        instruction.operand = AccessTools.Method(typeof(LoadingInvocationObservation), wrapper);
        return code; // Same instruction retains all branch labels and exception blocks.
    }
    internal static LoadingSession.Token? BeginAction(Action action)
    {
        try
        {
            bool single = action.GetInvocationList().Length == 1;
            return LoadingObservationRuntime.Begin("Deferred callbacks", single ? action.Method.DeclaringType + " -> " + action.Method
                : "Multicast callback (one native delegate invocation)", single ? LoadingObservationRuntime.PackageFor(action.Method.DeclaringType) : "shared/unknown");
        }
        catch { return null; }
    }
    internal static void ExecuteAction(Action action)
    {
        var token = BeginAction(action);
        Exception? failure = null;
        try { action(); }
        catch (Exception error) { failure = error; throw; }
        finally { LoadingObservationRuntime.End(token, failure); }
    }
    internal static void RunConstructor(RuntimeTypeHandle handle)
    {
        LoadingSession.Token? token = null;
        try
        {
            Type type = Type.GetTypeFromHandle(handle);
            token = LoadingObservationRuntime.Begin("Static constructors", "RunClassConstructor: " + type.FullName, LoadingObservationRuntime.PackageFor(type));
        }
        catch { }
        Exception? failure = null;
        try { RuntimeHelpers.RunClassConstructor(handle); }
        catch (Exception error) { failure = error; throw; }
        finally { LoadingObservationRuntime.End(token, failure); }
    }
    private static void BeforeProviderStep(object __instance, out LoadingSession.Token? __state)
    {
        __state = null;
        try
        {
            var mod = (ModContentPack?)providerMod?.GetValue(__instance);
            __state = LoadingObservationRuntime.Begin("Content", "Loading Progress content iterator execution step (may only update progress)", mod?.PackageId ?? "shared/unknown");
        }
        catch { }
    }
    private static void AfterProviderStep(LoadingSession.Token? __state, Exception? __exception)
        => LoadingObservationRuntime.End(__state, __exception);
}
