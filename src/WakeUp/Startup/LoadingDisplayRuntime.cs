// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

internal static class LoadingDisplayRuntime
{
    internal const string Owner = "wakeup.loading-display";
    internal const string PrepatcherDrawBody = "F306D1982F9C51669966AAA065E6A87E161B2E0E35A1A3726721377634450B6E";
    internal static readonly Guid PrepatcherDrawModule = new("532f91d2-0a72-4da5-bec1-158b6b915d86");
    internal static readonly MethodInfo[] Targets = {
        AccessTools.Method(typeof(LongEventHandler), "LongEventsOnGUI"),
        AccessTools.Method(typeof(LongEventHandler), "DrawLongEventWindowContents"),
        AccessTools.Method(typeof(LongEventHandler), "SetCurrentEventText"),
        AccessTools.Method(typeof(LongEventHandler), "LongEventsUpdate"),
        AccessTools.Method(typeof(PlayDataLoader), "DoPlayLoad") };
    internal static readonly string[] ExpectedBodies = {
        "2A08E624D9F85591CA5A01FF70200E5A2530AFE88416523773A2B33E6FD6F0AA",
        "E258015793DF4D84874C84C97B6E3ED29C54E66ADD62676610ACD3F86FB79E8C",
        "9198FA398842756003C18E0D99DCBD90157028B2A899BCA74DD997148AA5A871",
        "43145D89CF55CBD93858867B52D713F58585A60838318743A0EF9CBFE867DEFA",
        "A9BAD58EC8ACDAA90A6913248D21F00AC36C48B8307A10E28909D003FCB94749" };
    private static FieldInfo? currentEvent, eventText, forceHide;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static LoadingDisplayState? state;
    private static object? observedEvent;
    private static bool sawEvent;
    private static string path = "";
    private static int modCount;
    private static bool enabled, diagnostics;
    private static GUIStyle? style;
    private static GUIContent? content;
    private static float lastWidth, panelHeight;
    internal static string Status { get; private set; } = "Wake-Up's loading display was off for this launch.";
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    internal static bool Selected(string[] args) => StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
        && args.Count(a => a.StartsWith("--wake-up-loading-display=", StringComparison.Ordinal)) == 1
        && args.Contains("--wake-up-loading-display=on");

    internal static void TryInitialize(string[] args)
    {
        if (!Selected(args) || PlayDataLoader.Loaded) return;
        var harmony = new Harmony(Owner);
        path = Path.Combine(StartupLaunchSelector.Parse(args).SaveDataRoot!, "WakeUp", "loading-display.jsonl");
        try
        {
            if (LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == LoadingProgressCompatibility.PackageId))
                throw new InvalidOperationException("Loading Progress is active, so Wake-Up's display is inactive. "
                    + "To keep Loading Progress, turn off 'Show Wake-Up loading display'. "
                    + "To use Wake-Up's display, disable Loading Progress in Mods. Restart after changing either choice.");
            if (!RuntimeIdentity.ValidateBinaryIdentity(out _))
                throw new InvalidOperationException("The required game or patching functions are unavailable.");
            for (int i = 0; i < Targets.Length; i++)
                if (!SemanticMethodIdentity.TryHash(Targets[i], out string body, out _) || body != ExpectedBodies[i])
                    throw new InvalidOperationException("The native loading functions have changed.");
            InstallHooks(harmony);
            modCount = LoadedModManager.RunningModsListForReading.Count;
            Begin(args.Count(a => a.StartsWith("--wake-up-loading-display-diagnostics=", StringComparison.Ordinal)) == 1
                && args.Contains("--wake-up-loading-display-diagnostics=on"));
            enabled = true;
            LoadingObservationRuntime.SetDisplaySelected(true);
            AtlasBatchScheduling.EnableDisplayScheduling();
            Status = "Wake-Up observes startup and later save/world/map loading. Unknown totals and indivisible native work are labelled explicitly.";
            JsonLineLog.WriteReceipt(path, "installed", "native-observer");
        }
        catch (Exception e)
        {
            Stop("inactive", e.Message);
            try { harmony.UnpatchAll(Owner); } catch { }
        }
    }

    // Shared by production setup and focused tests against the frozen native
    // methods. This installs observers only, never a callback/constructor clone.
    internal static void InstallHooks(Harmony harmony)
    {
        currentEvent = AccessTools.Field(typeof(LongEventHandler), "currentEvent");
        if (currentEvent == null) throw new InvalidOperationException("The native loading state has changed.");
        eventText = AccessTools.Field(currentEvent.FieldType, "eventText");
        forceHide = AccessTools.Field(currentEvent.FieldType, "forceHideUI");
        if (eventText?.FieldType != typeof(string) || forceHide?.FieldType != typeof(bool))
            throw new InvalidOperationException("The native loading state has changed.");
        var checks = new PublishedPatchGuard[2];
        for (int i = 0; i < checks.Length; i++)
        {
            MethodInfo target = Targets[i];
            if (!PublishedPatchGuard.TryCreate(target, Owner, out var check, allPatchKinds: true,
                    allowedForeignPatch: patch => AllowsPrepatcherDraw(target, patch, Harmony.GetPatchInfo(target))
                        || NativeLoadingSummaryRuntime.AllowsDisplayCooperation(target, patch))
                || !check!.AllowsOriginalContract())
                throw new InvalidOperationException("Another mod owns or changes the native loading screen.");
            checks[i] = check;
        }
        guards = checks;
        harmony.Patch(Targets[0], postfix: new HarmonyMethod(typeof(LoadingDisplayRuntime), nameof(Draw)));
        harmony.Patch(Targets[2], postfix: new HarmonyMethod(typeof(LoadingDisplayRuntime), nameof(ObserveText)));
        harmony.Patch(Targets[3], finalizer: new HarmonyMethod(typeof(LoadingDisplayRuntime), nameof(AfterUpdate)));
        harmony.Patch(Targets[4], finalizer: new HarmonyMethod(typeof(LoadingDisplayRuntime), nameof(AfterPlayLoad)));
        harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(LoadingDisplayRuntime), nameof(Menu)));
    }
    internal static bool AllowsPrepatcherDraw(MethodBase target, Patch patch, Patches? record)
    {
        try
        {
            if (target != Targets[1] || patch.owner != "prepatcher") return false;
            MethodInfo method = patch.PatchMethod;
            if (method.Module.ModuleVersionId != PrepatcherDrawModule
                || SemanticMethodIdentity.Signature(method) != "M|PrepatcherImpl:Prepatcher.HarmonyPatches|DrawPrestarterInfo|mscorlib:System.Void|S||UnityEngine.CoreModule:UnityEngine.Rect"
                || !SemanticMethodIdentity.TryHash(method, out string body, out _) || body != PrepatcherDrawBody) return false;
            // This reviewed void prefix draws below the native window and
            // restores its style. It cannot suppress or change native arguments.
            // Patch indexes are per-kind, so inspect every kind explicitly.
            return record != null && record.Prefixes.Count(p => p.owner == "prepatcher" && p.PatchMethod == method) == 1
                && !record.Postfixes.Concat(record.Transpilers).Concat(record.Finalizers)
                    .Concat(record.InnerPrefixes).Concat(record.InnerPostfixes).Any(p => p.PatchMethod == method);
        }
        catch { return false; }
    }
    internal static void Begin(bool diagnostics)
    {
        LoadingDisplayRuntime.diagnostics = diagnostics;
        state = new LoadingDisplayState(Now, diagnostics);
        sawEvent = false;
        observedEvent = null;
        Status = "Wake-Up observes the active native loading queue.";
    }
    internal static LoadingDisplayState? CurrentState => state;
    internal static bool CheckOwnership()
    {
        if (state?.Active != true) return false;
        for (int i = 0; i < guards.Length; i++)
            if (!guards[i].AllowsOriginalContract())
            {
                Stop("inactive", "Another mod took ownership of the native loading screen.");
                return false;
            }
        return true;
    }
    internal static void ObserveText(string newText) => state?.Observe(newText, Now);
    internal static void AfterUpdate(Exception? __exception)
    {
        try
        {
            if (enabled && state == null && LongEventHandler.AnyEventNowOrWaiting) Begin(diagnostics);
            if (state?.Active != true) return;
            if (__exception != null) { Stop("interrupted", "Native loading threw " + __exception.GetType().Name + "."); return; }
            object? current = currentEvent!.GetValue(null);
            if (!ReferenceEquals(current, observedEvent))
            {
                observedEvent = current;
                if (current != null)
                {
                    sawEvent = true;
                    state?.Observe((string?)eventText!.GetValue(current), Now);
                }
            }
            // Native catch paths may swallow errors. This is cleanup, not a
            // declaration of successful loading; the independent observer owns that.
            if (sawEvent && current == null && !LongEventHandler.AnyEventNowOrWaiting)
                Stop("ended", "Native loading queue ended.");
        }
        catch (Exception e) { Stop("inactive", "Loading observation failed: " + e.GetType().Name + "."); }
    }
    internal static void AfterPlayLoad(Exception? __exception)
    {
        if (__exception != null && state?.Active == true)
            Stop("interrupted", "Native content loading threw " + __exception.GetType().Name + ".");
    }
    private static void Menu()
    {
        if (state?.Active == true && PlayDataLoader.Loaded && !LongEventHandler.AnyEventNowOrWaiting)
            Stop("ended", "Native loading queue ended at the menu.");
    }
    private static void Draw()
    {
        LoadingDisplayState? drawingState = state;
        if (drawingState?.Active != true) return;
        try
        {
            if (!CheckOwnership() || Event.current.type != EventType.Repaint) return;
            object? current = currentEvent!.GetValue(null);
            if (current == null || (bool)forceHide!.GetValue(current)) return;
            // Later loading reserves most of the screen for the native tip and
            // mod summary. Keep our two-line strip in the free top margin;
            // never cover the native controls with the startup diagnostics box.
            bool compact = Current.Game != null;
            GUIStyle drawingStyle = style ??= new GUIStyle(Text.CurFontStyle) { alignment = TextAnchor.UpperLeft, richText = false, fontSize = 16 };
            drawingStyle.wordWrap = !compact;
            drawingStyle.clipping = TextClipping.Clip;
            GUIContent drawingContent = content ??= new GUIContent();
            bool changed = drawingState.Refresh(Now, modCount, PngRuntime.DisplaySnapshot, out string text,
                LoadingFeatureFeedback.Snapshot, () => LoadingObservationRuntime.Current?.Snapshot(), compact);
            if (!drawingState.Active) return;
            if (changed) drawingContent.text = text;
            float width = compact ? UI.screenWidth - 32f : Math.Min(580f, UI.screenWidth - 32f);
            if (changed || width != lastWidth)
            {
                lastWidth = width;
                panelHeight = compact ? 54f : Math.Min(UI.screenHeight - 32f, drawingStyle.CalcHeight(drawingContent, width - 24f) + 24f);
            }
            var panel = new Rect(16f, compact ? 8f : 16f, width, panelHeight);
            Color previous = GUI.color;
            try
            {
                GUI.color = Color.white;
                GUI.Box(panel, GUIContent.none);
                GUI.Label(new Rect(panel.x + 12f, panel.y + (compact ? 7f : 12f), panel.width - 24f, panel.height - (compact ? 14f : 24f)), drawingContent, drawingStyle);
            }
            finally { GUI.color = previous; }
        }
        catch (Exception e) { Stop("inactive", "Loading display drawing failed: " + e.GetType().Name + "."); }
    }
    internal static void Stop(string kind, string reason)
    {
        LoadingDisplayState? previous = Interlocked.Exchange(ref state, null);
        observedEvent = null;
        style = null;
        content = null;
        Status = "Wake-Up loading display: " + reason;
        if (kind == "inactive") { enabled = false; LoadingObservationRuntime.SetDisplaySelected(false); }
        if (previous != null) JsonLineLog.WriteEvent(path, kind, previous.Stop(reason));
        else JsonLineLog.WriteReceipt(path, kind, reason);
        if (kind == "inactive") Log.Message("[Wake-Up] " + Status);
    }
}
