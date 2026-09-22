// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WakeUp;

internal static class BackgroundLoadingColonyContract
{
    internal static readonly MethodInfo[] Targets = FindTargets();
    // The shared contract already covers Root_Play.Start's complete branches,
    // scene/queue execution, callback drainage, abort and preference restoration.
    // Here admit the ordinary entry caller, its preparation action, new-game
    // completion/pause callback and map-generation failure handler independently.
    internal static readonly string[] Expected = new[] {
        "C7622BDAD2A63338BBE62F92F357238A631F99CC72ABD7A4FAA1A87318F5702E", // PageUtility preparation callback
        "AB4F4A53681AC7152D46899F1326565F4DE59746657ACF81BCA37355005A63DB", // PageUtility.InitGameStart
        "1BB73D2D9DE75553AC91D34C816FA503A187FC5C2FA3296ABDD2297C9A22FB71", // Game.InitNewGame mod log callback
        "72DDAF1A317FF71BC007FB5C56CA81F4C96AE145DC0A1151863895667A57415A", // Game.InitNewGame pause-on-load callback
        "9C7681036DF71AD91A8A63CFC0B4EAF3646B134F4BE16263FF1EF509B7E04AFE", // Game.InitNewGame
        "F1292FF0F41303BE9BBA1AAB32B1B6176B797B65EBB26BD75173C23AE65F18A5", // ErrorWhileGeneratingMap
        "529F534469FB8CBB53D22CBC095EF3FA72F4427CAC3EC54A354C7F7DABEC154F", // Root_Entry.Update
    };

    private static MethodInfo[] FindTargets()
    {
        var methods = new List<MethodInfo> {
            BackgroundLoadingRuntime.NewColony,
            AccessTools.Method(typeof(Game), "InitNewGame"),
            AccessTools.Method(typeof(GameAndMapInitExceptionHandlers), "ErrorWhileGeneratingMap"),
            AccessTools.Method(typeof(Root_Entry), "Update") };
        AddClosures(methods, typeof(PageUtility), "<InitGameStart>");
        AddClosures(methods, typeof(Game), "<InitNewGame>");
        return methods.Distinct().OrderBy(BackgroundLoadingContract.Key, StringComparer.Ordinal).ToArray();
    }
    private static void AddClosures(List<MethodInfo> methods, Type type, string prefix)
    {
        methods.AddRange(type.GetMethods(AccessTools.allDeclared)
            .Where(method => method.Name.StartsWith(prefix, StringComparison.Ordinal)));
        foreach (Type nested in type.GetNestedTypes(AccessTools.all)) AddClosures(methods, nested, prefix);
    }
    internal static void Validate()
    {
        if (Targets.Length != Expected.Length) throw new InvalidOperationException("Native new-colony loading functions changed.");
        for (int i = 0; i < Targets.Length; i++)
            if (!SemanticMethodIdentity.TryHash(Targets[i], out string hash, out _) || hash != Expected[i])
                throw new InvalidOperationException("Native new-colony loading function changed: " + BackgroundLoadingContract.Key(Targets[i]));
    }
}
