// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

// Product-owned shutdown registration is independent of optional audio admission.
// The bootstrap additionally covers managed process/domain teardown before Mono
// invalidates the bridge's domain. No observer is needed to settle or stop it.
internal static class NativeAudioLifetime
{
    private const string Owner = "kt-nyx.wake-up.native-audio-lifetime";
    private static Dictionary<string, object>? registry;
    private static Action? stop;

    internal static void Initialize(IReadOnlyList<string> arguments)
    {
        if (!arguments.Contains("--fixture-c10-native-entry-probe") || stop != null) return;
        var bootstrap = AppDomain.CurrentDomain.GetData("WakeUp.PreparedAudio.Bootstrap.v1") as Dictionary<string, object>
            ?? throw new InvalidOperationException("Native audio lifetime requires its early bootstrap.");
        registry = (Dictionary<string, object>)bootstrap["audioRegistry"];
        if (!(bool)registry["audioNativeEntryReady"])
            throw new InvalidOperationException("Native audio lifetime bridge is not ready.");
        stop = (Action)registry["audioNativeEntryStop"];
        registry["audioNativeProductStopEntries"] = 0;
        registry["audioNativeProductStopReason"] = "";
        registry["audioNativeProductStopCompleted"] = false;
        new Harmony(Owner).Patch(AccessTools.Method(typeof(Root), nameof(Root.Shutdown)),
            prefix: new HarmonyMethod(typeof(NativeAudioLifetime), nameof(BeforeShutdown)) { priority = Priority.First });
        Application.quitting += BeforeApplicationQuit;
        registry["audioNativeProductStopInstalled"] = true;
    }

    private static void BeforeShutdown() => Stop("Root.Shutdown");
    private static void BeforeApplicationQuit() => Stop("Application.quitting");

    private static void Stop(string reason)
    {
        lock (registry!["audioRegistryGate"])
        {
            registry["audioNativeProductStopEntries"] = (int)registry["audioNativeProductStopEntries"] + 1;
            if (((string)registry["audioNativeProductStopReason"]).Length == 0)
                registry["audioNativeProductStopReason"] = reason;
        }
        // Never hold the registry gate across the native join. Stop first drains
        // readers itself, then joins callbacks while managed execution is valid.
        stop!();
        lock (registry["audioRegistryGate"]) registry["audioNativeProductStopCompleted"] = true;
    }
}
