// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

// The verified VEF method consumes a holder only to enumerate its private trie
// by one prefix. It returns strings, never the holder or trie. Preserve the
// original enumeration and later material lookup; narrow only this acquisition.
public static class DemandExpandableTextureRuntime
{
    internal const string SupplierType = "VEF.Weapons.ExpandableGraphicData";
    internal static readonly string[] Expected = { "D34BA8DB29CC356A372E9879BE35C6241FB80C9BB51A3B5A1C2CEBCCF2F05430", "707A8574000C4474C686323D16AF8259AF5106B4C280FE82963B8941EA5FF2C8" };
    private static readonly MethodInfo NativeGetter = typeof(ModContentPack).GetMethods(AccessTools.allDeclared)
        .Single(m => m.Name == "GetContentHolder" && m.IsGenericMethodDefinition).MakeGenericMethod(typeof(Texture2D));
    private static readonly MethodInfo Bridge = AccessTools.Method(typeof(DemandExpandableTextureRuntime), nameof(GetHolderForPrefix));
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static MethodInfo? target;
    private static bool active;
    private static string status = "No verified expandable texture supplier observed.";
    private static long entries, scopedHolders, nativeHolders, closures;

    internal static MethodBase[] Targets(Assembly assembly)
    {
        Type type = assembly.GetType(SupplierType, true)!;
        return new MethodBase[]
        {
            type.GetMethods(AccessTools.allDeclared).Single(m => m.Name == "LoadAllFiles" && !m.IsStatic
                && m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.String" })),
            type.TypeInitializer!
        };
    }

    internal static void Initialize(Harmony harmony, Assembly assembly)
    {
        if (!DemandTextureRuntime.Enabled || assembly.GetType(SupplierType, false) == null) return;
        if (target != null)
        {
            if (!ReferenceEquals(target.Module.Assembly, assembly)) Close("another expandable supplier assembly appeared");
            return;
        }
        var owned = new Harmony(harmony.Id + ".expandable-textures");
        try
        {
            MethodBase[] methods = Targets(assembly);
            var checks = new List<PublishedPatchGuard>();
            for (int i = 0; i < methods.Length; i++)
            {
                bool hashed = SemanticMethodIdentity.TryHash(methods[i], out string hash, out string reason);
                if (!hashed || Expected[i].Length != 64 || hash != Expected[i])
                    throw new InvalidOperationException("expandable body not pinned: " + SemanticMethodIdentity.Signature(methods[i])
                        + "; actual=" + hash + "; " + reason);
                if (!PublishedPatchGuard.TryCreate(methods[i], owned.Id, out var guard, allPatchKinds: true)
                    || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException(DemandGraphicCompatibility.DescribePatches(methods[i]));
                checks.Add(guard!);
            }
            Type type = assembly.GetType(SupplierType, true)!;
            FieldInfo? accessor = type.GetField("contentListTrieRef", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            FieldInfo? trie = typeof(ModContentHolder<Texture2D>).GetField("contentListTrie", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (accessor == null || !accessor.IsInitOnly || !accessor.FieldType.IsGenericType || trie == null
                || accessor.FieldType.GetGenericTypeDefinition() != typeof(AccessTools.FieldRef<,>)
                || !accessor.FieldType.GetGenericArguments().SequenceEqual(new[] { typeof(ModContentHolder<Texture2D>), trie.FieldType })
                || trie.FieldType.FullName != "KTrie.StringTrieSet"
                || methods[0] is not MethodInfo load || load.ReturnType != typeof(List<string>))
                throw new InvalidOperationException("expandable readonly trie accessor shape changed");
            // The pinned cctor delegates field discovery and accessor creation
            // to these exact Harmony helpers. Watch both generic definitions
            // and the actual holder/trie instantiations, without running the
            // supplier cctor or reading its readonly delegate early.
            foreach (MethodBase helper in AccessorDependencies(trie.FieldType))
            {
                if (!PublishedPatchGuard.TryCreate(helper, owned.Id + ".accessor-dependency", out var guard, allPatchKinds: true)
                    || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("expandable accessor dependency: " + DemandGraphicCompatibility.DescribePatches(helper));
                checks.Add(guard!);
            }
            // Inspect metadata only: reading the field would initialize the
            // supplier earlier than its ordinary first use.
            if (!OneGetter(PatchProcessor.GetOriginalInstructions(load).ToList(), out _))
                throw new InvalidOperationException("expandable holder acquisition changed");
            guards = checks.ToArray();
            target = load;
            owned.Patch(load,
                prefix: new HarmonyMethod(typeof(DemandExpandableTextureRuntime), nameof(BeforeLoad)) { priority = Priority.First },
                transpiler: new HarmonyMethod(typeof(DemandExpandableTextureRuntime), nameof(Rewrite)));
            UnityData.DisposeStatic += DisposeMetadata;
            Volatile.Write(ref active, true);
            status = "Verified expandable scan completes only its native prefix population.";
        }
        catch (Exception error)
        {
            Volatile.Write(ref active, false);
            status = "Expandable texture route refused: " + error.GetBaseException().Message;
            guards = Array.Empty<PublishedPatchGuard>();
            target = null;
            owned.UnpatchAll(owned.Id);
        }
    }

    private static IEnumerable<MethodBase> AccessorDependencies(Type trieType)
    {
        Type access = typeof(AccessTools), tools = access.Assembly.GetType("HarmonyLib.Tools", true)!;
        MethodInfo Find(Type type, string name, int genericArguments, params Type[] parameters)
            => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Single(m => m.Name == name && m.GetGenericArguments().Length == genericArguments
                    && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
        MethodInfo entry = Find(access, "FieldRefAccess", 2, typeof(string));
        MethodInfo emit = Find(tools, "FieldRefAccess", 2, typeof(FieldInfo), typeof(bool));
        MethodInfo validate = Find(tools, "ValidateFieldType", 1, typeof(FieldInfo));
        yield return entry;
        yield return entry.MakeGenericMethod(typeof(ModContentHolder<Texture2D>), trieType);
        yield return Find(tools, "GetInstanceField", 0, typeof(Type), typeof(string));
        yield return Find(access, "Field", 0, typeof(Type), typeof(string));
        yield return emit;
        yield return emit.MakeGenericMethod(typeof(ModContentHolder<Texture2D>), trieType);
        yield return validate;
        yield return validate.MakeGenericMethod(trieType);
    }

    private static bool OneGetter(List<CodeInstruction> code, out int at)
    {
        int[] matches = Enumerable.Range(0, code.Count).Where(i => (code[i].opcode == OpCodes.Call || code[i].opcode == OpCodes.Callvirt)
            && Equals(code[i].operand, NativeGetter)).ToArray();
        at = matches.Length == 1 ? matches[0] : -1;
        return at >= 0 && code[at].blocks.Count == 0;
    }

    internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (!OneGetter(code, out int at)) throw new InvalidOperationException("expandable holder acquisition changed");
        // Stack originally contains the ModContentPack. Add the original raw
        // folder argument, retaining branch targets on the first inserted load.
        var folder = new CodeInstruction(OpCodes.Ldarg_1);
        folder.labels.AddRange(code[at].labels); code[at].labels.Clear();
        code.Insert(at, folder);
        code[at + 1].opcode = OpCodes.Call;
        code[at + 1].operand = Bridge;
        return code;
    }

    private static bool Allowed()
    {
        if (!Volatile.Read(ref active) || !DemandTextureRuntime.Enabled) return false;
        PublishedPatchGuard? refused = guards.FirstOrDefault(g => !g.AllowsOriginalContract());
        if (refused == null) return true;
        Close("changed expandable callback: " + DemandGraphicCompatibility.DescribePatches(refused.Target));
        return false;
    }

    private static void BeforeLoad()
    {
        Interlocked.Increment(ref entries);
        Allowed();
    }

    internal static string NormalizePrefix(string? folder)
        => !string.IsNullOrEmpty(folder) && folder![folder.Length - 1] == '/' ? folder : folder + "/";

    private static ModContentHolder<Texture2D> GetHolderForPrefix(ModContentPack mod, string? folder)
    {
        if (Allowed())
        {
            // Parent owns source ordering, complete-before-return and worker
            // refusal. No scheduling or main-thread wait is introduced here.
            ModContentHolder<Texture2D> holder = DemandTextureRuntime.GetHolderForPrefix(mod, NormalizePrefix(folder));
            Interlocked.Increment(ref scopedHolders);
            return holder;
        }
        Interlocked.Increment(ref nativeHolders);
        return mod.GetContentHolder<Texture2D>();
    }

    private static void Close(string reason)
    {
        Volatile.Write(ref active, false);
        status = "Expandable texture route closed: " + reason;
        Interlocked.Increment(ref closures);
        if (DemandTextureRuntime.PendingCount == 0) return;
        if (!UnityData.IsInMainThread)
            throw new InvalidOperationException("Changed expandable callback requires main-thread completion before worker dispatch.");
        DemandTextureRuntime.CompleteAll(reason);
    }

    private static void DisposeMetadata()
    {
        Volatile.Write(ref active, false);
        guards = Array.Empty<PublishedPatchGuard>();
        status = "Expandable texture metadata discarded with native static data.";
    }

    public static Dictionary<string, object> Snapshot() => new Dictionary<string, object>
    {
        ["active"] = Volatile.Read(ref active), ["status"] = status,
        ["entries"] = Interlocked.Read(ref entries), ["scopedHolders"] = Interlocked.Read(ref scopedHolders),
        ["nativeHolders"] = Interlocked.Read(ref nativeHolders), ["closures"] = Interlocked.Read(ref closures)
    };
}
