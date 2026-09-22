// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace WakeUp;

// Prerequisite observation only, explicitly fixture-selected. Keep this path
// BCL/Harmony/Cecil-only: resolving codec types would invalidate the observation.
// The emitted Harmony guards depend only on BCL delegates, not a WakeUp/game
// assembly that may not have been loaded yet by Prepatcher's replacement loop.
public static class PreparedAudioBootstrap
{
    public const string StateKey = "WakeUp.PreparedAudio.Bootstrap.v1";
    private const string CallbackKey = "WakeUp.PreparedAudio.Bootstrap.Mutation.v1";
    private const string Owner = "kt-nyx.wake-up.prepared-audio-bootstrap";
    private const string HarmonyHash = "7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C";
    private const string Marker = "WakeUp.Generated.PreparedAudioBootstrapV1";
    private static readonly object installation = new();

    public static void ObserveFirstExtension()
    {
        try { ObserveExtensionCore(); }
        catch (Exception exception)
        {
            if (AppDomain.CurrentDomain.GetData(StateKey) is Dictionary<string, object> state)
                state["failure"] = exception.ToString();
        }
    }

    private static void ObserveExtensionCore()
    {
        if (!Environment.GetCommandLineArgs().Contains("--fixture-c10-bootstrap-probe")) return;
        lock (installation)
        {
            if (AppDomain.CurrentDomain.GetData(StateKey) is Dictionary<string, object> early)
            {
                // Doorstop's BCL-only owner survives replacement. Preserve its
                // original assembly references, session, callbacks and guards.
                Action<Assembly, Assembly>? observe;
                lock (early["gate"])
                    observe = early.TryGetValue("observeExtension", out object callback)
                        ? callback as Action<Assembly, Assembly> : null;
                if (observe != null)
                {
                    Assembly game = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "Assembly-CSharp");
                    observe(typeof(Harmony).Assembly, game);
                }
                return;
            }
            if (AppDomain.CurrentDomain.GetData(StateKey) != null) return;
            Assembly harmonyAssembly = typeof(Harmony).Assembly;
            var state = new Dictionary<string, object>
            {
                ["version"] = 1, ["session"] = Guid.NewGuid().ToString("N"),
                ["gate"] = new object(), ["oldHarmony"] = harmonyAssembly,
                ["gameBefore"] = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp"),
                ["oldGuardsInstalled"] = false, ["finalGuardsEmitted"] = false,
                ["failure"] = "", ["entryEvents"] = new List<object[]>(), ["finalCallbackEntries"] = 0,
                ["sharedAtGuard"] = Array.Empty<Assembly>(), ["admissionClosed"] = true
            };
            AppDomain.CurrentDomain.SetData(StateKey, state);
            AppDomain.CurrentDomain.SetData(CallbackKey, (Action<Assembly, MethodBase>)ObserveMutation);
            try
            {
                using (var input = File.OpenRead(harmonyAssembly.Location))
                using (var hash = new SHA256Managed())
                    if (BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "") != HarmonyHash)
                        throw new InvalidOperationException("Bootstrap Harmony identity changed");
                var targets = Targets(harmonyAssembly);
                if (targets.Any(t => Harmony.GetPatchInfo(t) is Patches p
                    && p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers).Concat(p.Finalizers)
                        .Concat(p.InnerPrefixes).Concat(p.InnerPostfixes).Any()))
                    throw new InvalidOperationException("Bootstrap mutation entries already hooked");
                var owner = new Harmony(Owner);
                owner.Patch(targets[0], prefix: new HarmonyMethod(typeof(PreparedAudioBootstrap), nameof(OldUpdatePrefix)));
                owner.Patch(targets[1], prefix: new HarmonyMethod(typeof(PreparedAudioBootstrap), nameof(OldReversePrefix)));
                // This snapshot MUST follow full old-guard installation. It does
                // not load either shared codec or infer identity from its name.
                Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
                state["sharedAtGuard"] = loaded.Where(a => a.GetName().Name == "NAudio" || a.GetName().Name == "NVorbis").ToArray();
                state["oldGuardsInstalled"] = true;
                state["oldGuardTargets"] = targets;
            }
            catch (Exception exception) { state["failure"] = exception.ToString(); }
        }
    }

    private static MethodInfo[] Targets(Assembly assembly)
    {
        Type type = assembly.GetType("HarmonyLib.PatchFunctions", true)!;
        return new[] { AccessTools.Method(type, "UpdateWrapper"), AccessTools.Method(type, "ReversePatch") };
    }
    private static void OldUpdatePrefix(MethodBase __0, MethodBase __originalMethod)
        => Invoke(__originalMethod.Module.Assembly, __0);
    private static void OldReversePrefix(object __0, MethodBase __originalMethod)
    {
        if (__0 == null) return; // Preserve native invalid-argument behavior.
        if (__0.GetType().GetField("method")?.GetValue(__0) is MethodBase target)
            Invoke(__originalMethod.Module.Assembly, target);
    }
    private static void Invoke(Assembly caller, MethodBase target)
        => (AppDomain.CurrentDomain.GetData(CallbackKey) as Action<Assembly, MethodBase>)?.Invoke(caller, target);
    private static void ObserveMutation(Assembly caller, MethodBase target)
    {
        if (target == null || AppDomain.CurrentDomain.GetData(StateKey) is not Dictionary<string, object> state) return;
        lock (state["gate"])
        {
            if (!ReferenceEquals(caller, state["oldHarmony"]))
                state["finalCallbackEntries"] = (int)state["finalCallbackEntries"] + 1;
            var entries = (List<object[]>)state["entryEvents"];
            if (entries.Count < 128) entries.Add(new object[] { caller, target });
        }
    }

    // Dormant C10 research; no automatic Prepatcher registration.
    public static bool RewriteMutationEntries(ModuleDefinition module)
    {
        ObserveFirstExtension();
        if (module.Assembly.Name.Name != "0Harmony"
            || AppDomain.CurrentDomain.GetData(StateKey) is not Dictionary<string, object> state
            || !(bool)state["oldGuardsInstalled"] || module.GetType(Marker) != null) return false;
        try
        {
            TypeDefinition type = module.GetType("HarmonyLib.PatchFunctions");
            var update = type.Methods.Single(m => m.Name == "UpdateWrapper");
            var reverse = type.Methods.Single(m => m.Name == "ReversePatch");
            Inject(module, update, false);
            Inject(module, reverse, true);
            var marker = new TypeDefinition("WakeUp.Generated", "PreparedAudioBootstrapV1",
                Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed, module.TypeSystem.Object);
            module.Types.Add(marker);
            state["finalGuardHashes"] = new[] { AssetRoutingPrepatch.Fingerprint(update), AssetRoutingPrepatch.Fingerprint(reverse) };
            state["finalGuardsEmitted"] = true;
            return true;
        }
        catch (Exception exception) { state["failure"] = exception.ToString(); return false; }
    }

    private static void Inject(ModuleDefinition module, MethodDefinition target, bool reverse)
    {
        var first = target.Body.Instructions[0];
        var il = target.Body.GetILProcessor();
        var invoke = il.Create(OpCodes.Ldtoken, target.DeclaringType);
        var items = new List<Instruction>();
        if (reverse) { items.Add(il.Create(OpCodes.Ldarg_0)); items.Add(il.Create(OpCodes.Brfalse, first)); }
        items.Add(il.Create(OpCodes.Call, module.ImportReference(typeof(AppDomain).GetProperty(nameof(AppDomain.CurrentDomain))!.GetGetMethod())));
        items.Add(il.Create(OpCodes.Ldstr, CallbackKey));
        items.Add(il.Create(OpCodes.Callvirt, module.ImportReference(typeof(AppDomain).GetMethod(nameof(AppDomain.GetData)))));
        items.Add(il.Create(OpCodes.Isinst, module.ImportReference(typeof(Action<Assembly, MethodBase>))));
        items.Add(il.Create(OpCodes.Dup)); items.Add(il.Create(OpCodes.Brtrue, invoke));
        items.Add(il.Create(OpCodes.Pop)); items.Add(il.Create(OpCodes.Br, first));
        items.Add(invoke);
        items.Add(il.Create(OpCodes.Call, module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle)))));
        items.Add(il.Create(OpCodes.Callvirt, module.ImportReference(typeof(Type).GetProperty(nameof(Type.Assembly))!.GetGetMethod())));
        items.Add(il.Create(OpCodes.Ldarg_0));
        if (reverse) items.Add(il.Create(OpCodes.Ldfld, module.GetType("HarmonyLib.HarmonyMethod").Fields.Single(f => f.Name == "method")));
        items.Add(il.Create(OpCodes.Callvirt, module.ImportReference(typeof(Action<Assembly, MethodBase>).GetMethod("Invoke"))));
        foreach (var item in items) il.InsertBefore(first, item);
        target.Body.MaxStackSize = Math.Max(3, target.Body.MaxStackSize);
    }

    public static bool VerifyFinalGuards()
    {
        if (AppDomain.CurrentDomain.GetData(StateKey) is not Dictionary<string, object> state
            || state["finalGuardHashes"] is not string[] hashes || hashes.Length != 2) return false;
        Assembly final = typeof(Harmony).Assembly;
        if (ReferenceEquals(final, state["oldHarmony"]) || final.GetType(Marker) == null) return false;
        var targets = Targets(final);
        var actualHashes = new string[2];
        for (int i = 0; i < 2; i++)
        {
            if (!SemanticMethodIdentity.TryHash(targets[i], out actualHashes[i], out _)) return false;
            // The source Cecil fingerprint and runtime semantic digest use
            // different encodings. Verify the emitted executable guard prefix
            // directly and retain both identities; do not compare unlike hashes.
            var code = PatchProcessor.GetOriginalInstructions(targets[i]).ToArray();
            int at = i == 0 ? 0 : 2;
            if (code.Length < at + (i == 0 ? 13 : 14) || code[at].opcode != System.Reflection.Emit.OpCodes.Call
                || !Equals(code[at].operand, typeof(AppDomain).GetProperty(nameof(AppDomain.CurrentDomain))!.GetGetMethod())
                || !Equals(code[at + 1].operand, CallbackKey)
                || !Equals(code[at + 2].operand, typeof(AppDomain).GetMethod(nameof(AppDomain.GetData)))
                || !Equals(code[at + 3].operand, typeof(Action<Assembly, MethodBase>))
                || code[at + 4].opcode != System.Reflection.Emit.OpCodes.Dup
                || code[at + 8].operand as Type != targets[i].DeclaringType
                || !Equals(code[at + 9].operand, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle)))
                || !Equals(code[at + 10].operand, typeof(Type).GetProperty(nameof(Type.Assembly))!.GetGetMethod())
                || code[at + 11].opcode != System.Reflection.Emit.OpCodes.Ldarg_0
                || !Equals(code[at + (i == 0 ? 12 : 13)].operand, typeof(Action<Assembly, MethodBase>).GetMethod("Invoke"))) return false;
        }
        lock (state["gate"]) state["finalRuntimeGuardHashes"] = actualHashes;
        return true;
    }
}
