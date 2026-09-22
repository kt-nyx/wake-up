// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WakeUp;

// Prepare only a clip already selected by native playback code. The original
// source assignment and playback remain in place; no future song is selected.
internal static class PreparedAudioReadiness
{
    // Semantic bodies of the pinned ORIGINAL GOG reference.
    internal static readonly string[] Expected =
    {
        "53768E1BDC4965A783C54E7C269D6C5865200B88C9BD844C26DF1B3245B9B4FB",
        "2EE497D22B0BDF4C7EB1326B425A5FF37CB8BFCE90A739725674724E63097BC4",
        "DC275DC578FA3E706A1279D8C927D9649D1160C2986CAB7D63CF730C472150A7"
    };
    // Derived offline from the pinned original through our existing prepatch
    // pipeline and Cecil serialization; never learned from a running game.
    internal static readonly string[] EffectiveExpected =
    {
        "44CB6AC644A88C4DBEA21CA5916140DB8FC6D43E9C6F81CB4DE007DA64324631",
        "2EE497D22B0BDF4C7EB1326B425A5FF37CB8BFCE90A739725674724E63097BC4",
        "DC275DC578FA3E706A1279D8C927D9649D1160C2986CAB7D63CF730C472150A7"
    };
    private static readonly string[] Causes = { "music", "oneshot", "sustainer" };
    private static readonly MethodInfo ClipSetter = AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.clip));
    private static readonly MethodInfo PrepareMethod = AccessTools.Method(typeof(PreparedAudioReadiness), nameof(Prepare));
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static MethodInfo[] targets = Array.Empty<MethodInfo>();
    private static bool active;
    internal static bool Active => Volatile.Read(ref active);
    internal static string Status { get; private set; } = "Selected audio readiness is off.";

    internal static MethodInfo[] Targets() => Targets(typeof(MusicManagerPlay).Assembly);

    internal static MethodInfo[] Targets(Assembly assembly)
    {
        MethodInfo Find(string type, string name, params string[] parameters) => assembly.GetType(type, true)!
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Single(method => method.Name == name && method.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(parameters));
        return new[]
        {
            Find("RimWorld.MusicManagerPlay", "PlaySong", "Verse.SongDef", "System.Boolean", "System.Boolean"),
            Find("Verse.Sound.SampleOneShot", "TryMakeAndPlay", "Verse.Sound.SubSoundDef", "UnityEngine.AudioClip", "Verse.Sound.SoundInfo"),
            Find("Verse.Sound.SampleSustainer", "TryMakeAndPlay", "Verse.Sound.SubSustainer", "UnityEngine.AudioClip", "System.Single", "System.Single")
        };
    }

    internal static void Initialize(Harmony harmony)
    {
        Close();
        try
        {
            MethodInfo[] selected = Targets();
            for (int i = 0; i < selected.Length; i++)
            {
                bool hashed = SemanticMethodIdentity.TryHash(selected[i], out string body, out string reason);
                if (EffectiveExpected[i].Length != 64 || !hashed || body != EffectiveExpected[i])
                    throw new InvalidOperationException("effective playback body changed or is not pinned: " + Causes[i]
                        + "; actual=" + body + "; reason=" + reason);
                if (!AssignmentSite(PatchProcessor.GetOriginalInstructions(selected[i]).ToList(), out _))
                    throw new InvalidOperationException("native clip assignment changed: " + Causes[i]);
            }
            var checks = new List<PublishedPatchGuard>();
            foreach (MethodInfo target in selected.Concat(new[] { ClipSetter }))
            {
                if (target == null || !PublishedPatchGuard.TryCreate(target, harmony.Id, out var guard, allPatchKinds: true)
                    || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("a published patch changes the playback consumer: " + target);
                checks.Add(guard);
            }
            targets = selected;
            guards = checks.ToArray();
            foreach (MethodInfo target in targets)
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(PreparedAudioReadiness), nameof(RewriteAssignment)));
            Volatile.Write(ref active, true);
            Status = "Selected native music and sound clips are prepared before source assignment.";
        }
        catch (Exception error)
        {
            // Any already-installed pass-throughs remain dormant. In particular,
            // do not unpatch the shared owner's unrelated native audio hooks.
            Volatile.Write(ref active, false);
            Status = "Selected audio readiness refused: " + error.GetBaseException().Message;
        }
    }

    internal static void Close()
    {
        Volatile.Write(ref active, false);
        Status = "Selected audio readiness is off.";
    }

    // Caller must not hold the audio registry or reader gate. These publication
    // checks call Harmony only when its immutable patch record has changed.
    internal static bool Allowed(string cause)
    {
        if (!Volatile.Read(ref active)) return false;
        int index = Array.IndexOf(Causes, cause);
        PublishedPatchGuard[] current = guards;
        return index >= 0 && current.Length == 4 && current[index].AllowsOriginalContract()
            && current[3].AllowsOriginalContract();
    }

    private static AudioClip Prepare(AudioClip clip, string cause)
        => Allowed(cause) ? PreparedAudioRuntime.PrepareForPlayback(clip, cause) : clip;

    internal static IEnumerable<CodeInstruction> RewriteAssignment(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.ToList();
        int index = Array.FindIndex(Targets(), target => Equals(target, __originalMethod));
        // A later foreign transpiler can change the site. Leave that body alone;
        // a published foreign patch also disables the readiness call at runtime.
        if (index < 0 || !AssignmentSite(code, out int at)) return code;
        var cause = new CodeInstruction(OpCodes.Ldstr, Causes[index]);
        cause.labels.AddRange(code[at].labels);
        code[at].labels.Clear();
        code.Insert(at, cause);
        code.Insert(at + 1, new CodeInstruction(OpCodes.Call, PrepareMethod));
        return code;
    }

    private static bool AssignmentSite(List<CodeInstruction> code, out int at)
    {
        int[] sites = Enumerable.Range(0, code.Count)
            .Where(i => (code[i].opcode == OpCodes.Call || code[i].opcode == OpCodes.Callvirt)
                && Equals(code[i].operand, ClipSetter)).ToArray();
        at = sites.Length == 1 ? sites[0] : -1;
        // Do not move an exception-region boundary across an inserted call.
        return at >= 0 && code[at].blocks.Count == 0;
    }
}
