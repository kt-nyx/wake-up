// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

internal static class CompatibilityStatus
{
    private static readonly object Gate = new();
    internal static OperationRegistry? Registry { get; private set; }
    private static bool deliveryInstalled, deliveryError;
    private static CompatibilityWindow? window;
    private static readonly HashSet<string> deferredThisSession = new(StringComparer.Ordinal);
    internal static void EnsureInitialized(string root)
    {
        lock (Gate) Registry ??= new OperationRegistry(Path.Combine(root, "WakeUp", "Compatibility", "notices.xml"),
            message => Log.Message("[Wake-Up compatibility] " + message));
    }
    internal static void Declare(string id, string name, bool requested = true) => Registry?.Request(id, name, requested);
    internal static void Available(string id, string reason = "Available when the native path runs; use and speed are not implied.")
        => Registry?.Set(id, OperationState.Available, reason);
    internal static void Refuse(string id, string reason, string code = "required-contract", string provider = "Wake-Up")
        => Registry?.Set(id, OperationState.Unavailable, reason, code, provider);
    internal static void Absent(string id) => Registry?.Set(id, OperationState.NotApplicable, "Optional provider is not active.");
    internal static bool Guard(string id, bool allowed)
    { if (!allowed) Refuse(id, "A required method or callback changed after admission.", "required-contract"); return allowed; }
    internal static void Receipt(string id, string kind, string reason)
    {
        if (kind == "installed") Available(id);
        else if (kind == "inactive" || reason == "supplier-absent" || reason == "optional-supplier-absent" || reason == "supplier-not-active") Absent(id);
        else if (kind == "refused") Refuse(id, reason);
    }
    internal static void SetupFailed(string name, Exception error)
    {
        string[] ids = name switch {
            "Character Editor" => new[] { "character" }, "Giddy-Up" => new[] { "giddy" },
            "Loading Progress" => new[] { "repaint" }, "PNG cache" => new[] { "texture-loader", "texture-cache", "prepared", "psd", "quality" },
            "Gagarin" => new[] { "gagarin" }, "Translations" => new[] { "translations" },
            "Asset routing" => new[] { "asset-routing" }, "Streaming XML" => new[] { "streaming-xml" }, "Processed XML" => new[] { "processed-xml" },
            "Type searches" => new[] { "type-name" }, "Leaf searches" => new[] { "leaf" }, "Reflection searches" => new[] { "reflection", "attributes" },
            "Definition searches" => new[] { "definitions", "single-query", "query-plans", "extended-query" },
            "Search setup" => new[] { "type-name", "leaf", "reflection", "attributes", "definitions", "single-query", "query-plans", "extended-query" },
            "Prepared textures" => new[] { "prepared", "quality" }, "Loading display" => new[] { "display", "display-diagnostics", "display-scheduling" },
            "Native loading summary" => new[] { "summary" }, "Per-mod XML timings" => new[] { "observation", "xml-timings", "invocations", "early-observation", "report-storage" },
            "Background save loading" => new[] { "background", "background-world", "background-colony", "background-map" }, _ => Array.Empty<string>() };
        foreach (string id in ids) Refuse(id, "Setup failed: " + error.GetType().Name, "setup-interrupted");
    }
    internal static void LogHardExclusions()
    {
        // Diagnose retained metadata exclusions, without adding package bans or
        // removing another owner's patches. Run before normal feature setup.
        if (AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == typeof(WakeUpMod).Assembly.GetName().Name) > 1)
            Log.Warning("[Wake-Up] Multiple Wake-Up runtime assemblies are loaded. Keep one distribution; compatibility is not established.");
        foreach (var mod in LoadedModManager.RunningModsListForReading)
            if (!mod.assemblies.loadedAssemblies.Contains(typeof(WakeUpMod).Assembly)
                && (mod.PackageId == "local.rimworldloadingoptimizer.op7validation" || mod.PackageId == "kt.nyx.startupfixes"))
                Log.Warning("[Wake-Up] A retained incompatible legacy/validation package is active: " + mod.PackageId + ". Remove that duplicate distribution.");
    }
    internal static void Select(string[] args)
    {
        bool candidate = StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate;
        bool Has(string value) => candidate && args.Contains(value);
        bool types = Has("--wake-up-strategy=type-lookup") || Has("--wake-up-strategy=startup-searches") && !Has("--wake-up-user-type-search=off");
        bool defs = Has("--wake-up-strategy=def-lookup") || Has("--wake-up-strategy=startup-searches") && !Has("--wake-up-user-def-search=off");
        Declare("type-name", "Code type-name lookup", types);
        Declare("leaf", "Leaf subclass search", types);
        Declare("reflection", "Loading member lookup", types);
        Declare("attributes", "Attribute-existence lookup", types);
        Declare("definitions", "Definition worker queries", defs);
        bool queries = Has("--wake-up-query-extensions=on");
        Declare("single-query", "Single-result XML query reuse", queries);
        Declare("query-plans", "Reusable XML query syntax", queries);
        Declare("extended-query", "XML Extensions eager queries", queries);
        foreach (var item in new[] {
            ("translations", "Native translation application", "translations=on"),
            ("asset-routing", "Asset route lookup", "asset-routing=on"),
            ("streaming-xml", "Streaming XML input", "streaming-xml=on"),
            ("processed-xml", "Completed XML patch reuse", "processed-xml=on"),
            ("character", "Character Editor preset lookup", "character-presets=on"),
            ("giddy", "Giddy-Up texture readback", "giddy-textures=on"),
            ("gagarin", "Gagarin parsed XML reuse", "gagarin-cache=on"),
            ("repaint", "Loading Progress repaint coalescing", "loading-progress=coalesce"),
            ("display", "Loading display", "loading-display=on"),
            ("summary", "Native summary hiding", "hide-loading-summary=on"),
            ("background", "Background save loading", "background-loading=on"),
            ("background-world", "Background world generation", "background-loading=on"),
            ("background-colony", "Background colony creation", "background-loading=on"),
            ("background-map", "Background map loading", "background-loading=on") })
            Declare(item.Item1, item.Item2, Has("--wake-up-" + item.Item3));
        bool display = Has("--wake-up-loading-display=on"), reports = Has("--wake-up-loading-timings=on");
        Declare("display-diagnostics", "Display diagnostics", display && Has("--wake-up-loading-display-diagnostics=on"));
        Declare("display-scheduling", "Loading display callback scheduling", display);
        Declare("observation", "Native loading observation", display || reports);
        Declare("xml-timings", "Per-mod XML observation", display || reports);
        Declare("invocations", "Individual loading invocation observation", display || reports);
        Declare("early-observation", "Early mod-constructor observation", display || reports);
        Declare("report-storage", "Loading report storage", reports);
        bool png = candidate && args.Any(a => a.StartsWith("--wake-up-png=", StringComparison.Ordinal));
        Declare("texture-loader", "Native texture loader", png);
        Declare("texture-cache", "Native texture output caching", Has("--wake-up-png=cache") || Has("--wake-up-png=verify-cache"));
        Declare("prepared", "Prepared native texture reuse", candidate && args.Any(a => a.StartsWith("--wake-up-prepared=", StringComparison.Ordinal)));
        Declare("psd", "Composited PSD decoding", Has("--wake-up-psd=merged"));
        Declare("quality", "Explicit prepared texture quality", candidate && args.Any(a => a.StartsWith("--wake-up-prepared=", StringComparison.Ordinal)));
    }
    internal static void TextureLoader(bool available, string reason = "")
    {
        if (available) Available("texture-loader");
        else foreach (string id in new[] { "texture-loader", "texture-cache", "prepared", "psd", "quality" }) Refuse(id, reason);
    }
    internal static void InstallDelivery()
    {
        if (deliveryInstalled) return;
        try
        {
            new Harmony("wakeup.compatibility-notices").Patch(AccessTools.Method(typeof(WindowStack), "WindowStackOnGUI"),
                postfix: new HarmonyMethod(typeof(CompatibilityStatus), nameof(Pump)));
            deliveryInstalled = true;
        }
        catch (Exception error) { Log.Warning("[Wake-Up] Compatibility notices remain pending; open settings for details. " + error.GetType().Name); }
    }
    private static void Pump()
    {
        try
        {
            if (!PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting || window != null || Registry == null || !Registry.HasPending) return;
            var pending = Registry.Pending().Where(n => !deferredThisSession.Contains(n.Key)).ToArray();
            if (pending.Length == 0) return;
            window = new CompatibilityWindow(Registry, pending, () => { foreach (var notice in pending) deferredThisSession.Add(notice.Key); window = null; });
            Find.WindowStack.Add(window);
        }
        catch (Exception error)
        { window = null; if (!deliveryError) { deliveryError = true; Log.Warning("[Wake-Up] Compatibility notice delivery failed: " + error.GetType().Name); } }
    }
}

// Snapshot at opening; late decisions cannot inherit acknowledgement. Scrolling
// records only fully rendered entries. One acknowledgement covers that aggregate.
internal sealed class CompatibilityWindow : Window
{
    private readonly OperationRegistry registry;
    private readonly CompatibilityNotice[] notices;
    private readonly Action closed;
    private readonly HashSet<string> rendered = new(StringComparer.Ordinal);
    private Vector2 scroll;
    private GUIStyle? style;
    internal CompatibilityWindow(OperationRegistry registry, CompatibilityNotice[] notices, Action closed)
    { this.registry = registry; this.notices = notices; this.closed = closed; absorbInputAroundWindow = true; closeOnAccept = false; closeOnCancel = true; doCloseX = true; }
    public override Vector2 InitialSize => new(850, 680);
    public override void DoWindowContents(Rect inRect)
    {
        style ??= new GUIStyle(Text.CurFontStyle) { wordWrap = true, richText = false };
        GUI.Label(new Rect(0, 0, inRect.width, 115), "These notices describe unavailable Wake-Up improvements or conflicts in other mods' settings. Compatible improvements can still run. Your saved choices and other mods' behavior are unchanged. Notices may be from an earlier launch; current Wake-Up status and technical details are in settings. Close X to defer without acknowledgement.", style);
        var area = new Rect(0, 120, inRect.width, inRect.height - 180);
        float width = area.width - 22;
        float[] heights = notices.Select(n => style.CalcHeight(new GUIContent(n.Text), width) + 15).ToArray();
        var view = new Rect(0, 0, width, Math.Max(area.height, heights.Sum()));
        scroll = GUI.BeginScrollView(area, scroll, view);
        try
        {
            float y = 0;
            for (int i = 0; i < notices.Length; i++)
            {
                GUI.Label(new Rect(0, y, width, heights[i]), notices[i].Text, style);
                if (Event.current.type == EventType.Repaint && y >= scroll.y - 1 && y + heights[i] <= scroll.y + area.height + 1)
                    rendered.Add(notices[i].Key);
                y += heights[i];
            }
        }
        finally { GUI.EndScrollView(); }
        bool allRead = rendered.Count == notices.Length;
        string label = allRead ? "Acknowledge these compatibility notices" : "Scroll to read remaining notices (" + rendered.Count + "/" + notices.Length + ")";
        if (Widgets.ButtonText(new Rect(0, inRect.height - 45, inRect.width, 40), label)) AcknowledgeRendered();
    }
    // Shared with the private functional observer after genuine native rendering.
    // The handler retains the same all-rendered and durable-write requirements.
    internal bool AcknowledgeRendered()
    {
        if (rendered.Count != notices.Length || !registry.Acknowledge(rendered)) return false;
        Close();
        return true;
    }
    public override void PostClose() { base.PostClose(); closed(); }
}

internal sealed class CompatibilityDetailsWindow : Window
{
    private Vector2 scroll;
    public override Vector2 InitialSize => new(850, 700);
    internal CompatibilityDetailsWindow() { doCloseButton = true; doCloseX = true; }
    public override void DoWindowContents(Rect inRect)
    {
        var style = new GUIStyle(Text.CurFontStyle) { wordWrap = true, richText = false };
        string text = string.Join("\n\n", CompatibilityStatus.Registry?.Snapshot() ?? Array.Empty<string>());
        var area = new Rect(0, 0, inRect.width, inRect.height - 60);
        var view = new Rect(0, 0, area.width - 22, Math.Max(area.height, style.CalcHeight(new GUIContent(text), area.width - 22)));
        scroll = GUI.BeginScrollView(area, scroll, view);
        try { GUI.Label(view, text, style); } finally { GUI.EndScrollView(); }
    }
}
