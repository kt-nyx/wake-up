// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Verse;

// Qualification-only observation, separate from timing comparisons and from
// the product package. Existing supplier receipts cover other loading stages.
internal static class ResidualProbe
{
    private static string? path;
    private static int draws;
    private static long lateCalls, lateTicks;
    internal static void Install(Harmony harmony, MethodInfo menu, string directory)
    {
        path = Path.Combine(directory, "residual-probe.jsonl");
        harmony.Patch(menu, prefix: new HarmonyMethod(typeof(ResidualProbe), nameof(BeginMenu)),
            finalizer: new HarmonyMethod(typeof(ResidualProbe), nameof(EndMenu)));
        harmony.Patch(AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName)),
            prefix: new HarmonyMethod(typeof(ResidualProbe), nameof(BeginType)),
            finalizer: new HarmonyMethod(typeof(ResidualProbe), nameof(EndType)));
    }
    private static void BeginMenu(out long __state) => __state = draws < 8 ? Stopwatch.GetTimestamp() : 0;
    private static Exception? EndMenu(long __state, Exception? __exception)
    {
        if (__state != 0)
        {
            draws++;
            Write("{\"event\":\"menu-call\",\"call\":" + draws + ",\"milliseconds\":" + Ms(Stopwatch.GetTimestamp()-__state)
                + ",\"lateTypeCalls\":" + lateCalls + ",\"lateTypeMilliseconds\":" + Ms(lateTicks)
                + ",\"exception\":" + (__exception == null ? "false" : "true") + "}");
        }
        return __exception;
    }
    private static void BeginType(out long __state)
        => __state = draws < 8 && PlayDataLoader.Loaded && !LongEventHandler.AnyEventNowOrWaiting ? Stopwatch.GetTimestamp() : 0;
    private static Exception? EndType(long __state, Exception? __exception)
    {
        if (__state != 0) { lateCalls++; lateTicks += Stopwatch.GetTimestamp()-__state; }
        return __exception;
    }
    private static string Ms(long ticks) => (ticks*1000d/Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture);
    private static void Write(string text) { try { File.AppendAllText(path!,text+Environment.NewLine); } catch { } }
}
