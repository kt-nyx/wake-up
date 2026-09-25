// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace WakeUp;

internal enum NoticeKind { Information, Limited, OptionalChoice, ActionNeeded, Advisory }

internal sealed class CompatibilityNotice
{
    internal readonly string Key, Id, Code, Provider, Details, DisplayProvider, DisplayCode;
    internal readonly bool Current;
    internal readonly bool EarlierLaunch;
    internal readonly OperationState? CurrentState;
    internal CompatibilityNotice(string key, string details, bool current = false, string displayProvider = "", string displayCode = "", OperationState? currentState = null, bool earlierLaunch = true)
    {
        Key = key; Details = details; Current = current; DisplayProvider = displayProvider; DisplayCode = displayCode; CurrentState = currentState;
        EarlierLaunch = !current && earlierLaunch;
        // Provider labels may contain '|'. Never split the stable acknowledgement key again.
        string[] parts = key.Split(new[] { '|' }, 3);
        Id = parts.Length == 3 ? parts[0] : "";
        Code = displayCode.Length != 0 ? displayCode : parts.Length == 3 ? parts[1] : "";
        Provider = parts.Length == 3 ? parts[2] : "";
    }
    internal string Text => CompatibilityNoticePresentation.Describe(this).Text;
    internal CompatibilityNotice Historical(OperationState? currentState = null) => new(Key, Details, false, DisplayProvider, DisplayCode, currentState, EarlierLaunch);
}

internal sealed class CompatibilityCard
{
    internal readonly string Area, Mods, Change, Continues, Action, Cause;
    internal readonly NoticeKind Kind;
    internal readonly bool Current;
    internal readonly string[] Keys;
    internal CompatibilityCard(string area, string mods, NoticeKind kind, string change, string continues,
        string action, string cause, bool current, string[] keys)
    { Area = area; Mods = mods; Kind = kind; Change = change; Continues = continues; Action = action; Cause = cause; Current = current; Keys = keys; }
    internal string Heading => Area + (Mods.Length == 0 ? "" : " — " + Mods);
    internal string Status => !Current ? "Previously recorded — not the current status" : Kind switch {
        NoticeKind.Information => "Working alongside another mod", NoticeKind.OptionalChoice => "Optional setting change", NoticeKind.ActionNeeded => "Action needed",
        NoticeKind.Advisory => "Check these settings", _ => "Some Wake-Up improvements are unavailable" };
    internal string Text => Heading + "\n" + Status + "\n" + Change + "\n" + Continues + "\n" + Action;
}

// Copy is derived from semantic decisions, never displayed exception/patch text.
// Keep this game-independent so migration, grouping and acknowledgement can be tested offline.
internal static class CompatibilityNoticePresentation
{
    internal static CompatibilityCard Describe(CompatibilityNotice n)
    {
        string id = n.Id, code = n.Code, provider = n.Provider;
        string mod = ModName(n.DisplayProvider.Length == 0 ? n.Provider : n.DisplayProvider);
        string integration = id switch { "giddy" => "Giddy-Up", "character" => "Character Editor", "repaint" => "Loading Progress",
            "gagarin" => "Missile Girl / Gagarin", "extended-query" => "XML Extensions", _ => "" };
        string mods = mod.Length == 0 ? integration : integration.Length != 0 && integration != mod ? integration + " + " + mod : mod;
        string area = Area(id);
        string previous = n.EarlierLaunch ? "On an earlier launch, " : "Earlier in this launch, ";
        string when = n.Current ? "Wake-Up has " : previous + "Wake-Up had ";
        string change = when + "turned off the affected part of this improvement because it could not confirm that it would work with this setup.";
        string continues = "The game will load this part in its usual way. This adjustment does not switch off your other Wake-Up features.";
        string action = "You can leave your settings as they are. You may miss this particular speed-up.";
        NoticeKind kind = NoticeKind.Limited;
        string cause = code;

        if (id == "advisory")
        {
            kind = NoticeKind.Advisory; area = "Mod loading settings";
            change = n.Current ? "Wake-Up found a settings combination that needs attention." : previous + "Wake-Up recorded a settings warning; it may no longer apply.";
            continues = "Wake-Up has not changed the other mod's settings or disabled it.";
            action = "Check " + (mod.Length == 0 ? "the affected mod's" : mod + "'s") + " settings. The technical details are in the log.";
            if (provider == "DefLoadCache" && code == "source-skip-without-patch-replay")
            {
                change = n.Current ? "DefLoadCache is set to skip reading mod files but still apply changes to them. A cached launch can then be missing the files those changes need."
                    : previous + "DefLoadCache was set to skip reading mod files while still applying changes to them. Check whether those settings are still selected.";
                action = "In DefLoadCache settings, turn off 'Skip reading mod files on repeat launches', then restart. Wake-Up cannot repair this settings combination for you.";
            }
        }
        else if (code == "supplier-image-producer" && (provider == "Image Opt" || provider == "Graphics Settings+"))
        {
            kind = NoticeKind.Information; area = "Image loading and caching";
            change = when + "turned off its own image-loading and cache improvements to avoid competing with " + mod + ".";
            continues = mod + (n.Current ? " continues to load your images. Other Wake-Up improvements can still run." : " was left in charge of images. Other Wake-Up improvements had their own checks.");
            action = "No action needed for this notice. You can keep both mods enabled.";
        }
        else if (code == "image-original-destruction")
        {
            mods = "Giddy-Up + Image Opt + ImageOptCompat"; kind = NoticeKind.OptionalChoice;
            change = when + "left the affected Giddy-Up image work on its original path because ImageOptCompat's original-texture destruction option was enabled.";
            action = "To use Wake-Up's Giddy-Up improvement, turn off 'destroyOriginalTexture' in ImageOptCompat and restart. Keeping the original path requires no change.";
        }
        else if (code == "supplier-deferred-completion")
        {
            kind = NoticeKind.OptionalChoice;
            change = when + "turned off the affected background-loading improvement because Loading Progress's selected screen-refresh option changes how loading finishes.";
            continues = "Loading Progress keeps its screen. Use the usual focused game window for this loading work.";
            action = "Optional: to use Wake-Up's background loading, turn off 'Patch in-game renderer regeneration to keep the loading window responsive' in Loading Progress and restart. You can instead keep your current settings.";
        }
        else if (code == "supplier-display")
        {
            bool twoScreens = provider == "RimThemes + Loading Progress";
            kind = twoScreens ? NoticeKind.ActionNeeded : NoticeKind.Information;
            change = when + "left its own loading screen off because " + (twoScreens ? "RimThemes and Loading Progress both provide a loading screen." : (mod.Length == 0 ? "another mod provides a loading screen." : mod + " provides a loading screen."));
            continues = "This adjustment does not switch off your other Wake-Up features.";
            action = twoScreens ? "Choose one external loading screen. To keep Loading Progress's screen, disable RimThemes' custom loader and restart."
                : provider == "RimThemes" ? "No action needed to keep RimThemes' screen. To use Wake-Up's screen instead, disable RimThemes' custom loader and restart."
                : "No action needed. You can keep " + (mod.Length == 0 ? "the other mod's" : mod + "'s") + " loading screen.";
        }
        else if (code == "display-dependency" && provider == "Loading Progress")
        {
            area = "Loading screen"; kind = NoticeKind.Information;
            change = when + "left its unused loading-screen tracking off while Loading Progress supplies the display.";
            continues = "Loading Progress keeps its screen. This adjustment does not switch off your other Wake-Up features.";
            action = "No action needed for this notice.";
        }
        else if (code == "supplier-dlc-panel")
        {
            kind = NoticeKind.Information;
            change = when + "left the loading summary visible so No Modlist on Loading can keep the DLC panel.";
            continues = "The loading screen and other Wake-Up improvements keep their separate settings.";
            action = "No action needed. To hide the whole panel instead, select 'Also hide the DLC panel kept by No Modlist on Loading (restart required)' in Wake-Up settings.";
        }
        else if (code == "supplier-atlas-owner")
        {
            change = when + "turned off its image-quality changes because FastLoader may reuse images it has already combined.";
            continues = "FastLoader's images stay in use. You can still use Wake-Up to export converted image files.";
            action = "No action needed to keep existing images. To use Wake-Up's image-quality changes, turn off FastLoader's atlas reuse and restart.";
        }
        else if (provider == "WOWGAG")
        {
            string work = code.StartsWith("supplier-xml", StringComparison.Ordinal) ? "mod-data loading" : "early content loading";
            bool unknown = code.Contains("unqualified"), choice = code.Contains("action-required");
            kind = choice ? NoticeKind.OptionalChoice : unknown ? NoticeKind.Limited : NoticeKind.Information;
            change = when + "turned off its overlapping improvement " + (unknown ? "because this part of WOWGAG could not be checked." : "while WOWGAG handles " + work + ".");
            action = choice ? "You selected Wake-Up for this work. Turn off WOWGAG's " + (code.StartsWith("supplier-xml", StringComparison.Ordinal) ? "XML reuse" : "content preload") + " option and restart, or keep WOWGAG and select Automatic in Wake-Up."
                : "No action needed to keep WOWGAG's existing loading path.";
        }
        else if (code == "supplier-background-preference")
        {
            kind = NoticeKind.Information;
            change = when + "left the affected background-loading controls to Load in Background.";
            continues = "Load in Background keeps control of whether the game runs while unfocused. This adjustment does not switch off your other Wake-Up features.";
            action = "No action needed to keep Load in Background in charge.";
        }
        else if ((provider == "YaOpt" || provider == "Faster Game Loading") && code != "setup-interrupted")
        {
            kind = NoticeKind.Information;
            change = when + "switched off only the overlapping search improvements so it can work alongside " + Pretty(mod) + ".";
            continues = "These searches help RimWorld find the data it needs while loading. The affected searches still run without Wake-Up's speed-up; this adjustment does not switch off its other features.";
            action = "No action needed for this notice. You can keep both mods enabled.";
        }
        else if (id == "processed-xml" && n.Details.Contains("WakeUp.StreamingXmlRuntime"))
        {
            mods = "Wake-Up";
            change = when + "turned off reuse of completed mod-data changes because Wake-Up's streaming input option was also selected.";
            continues = "Streaming input stays enabled; the game still applies mod-data changes in the usual way. This is a limitation between two Wake-Up options.";
            action = "You can keep streaming input, or turn it off and restart if you prefer to use the completed-data cache.";
        }
        else if (code == "image-source-pending")
        {
            change = when + "used Giddy-Up's usual image calculations for images that Image Opt had not finished preparing.";
            continues = "This concerns those image calculations; it does not switch off all of Wake-Up's Giddy-Up improvement.";
            action = "No action needed for this notice.";
        }
        else if (code == "storage-unavailable")
        {
            area = "Saving loading reports"; kind = NoticeKind.ActionNeeded;
            change = when + "been unable to save a loading report.";
            continues = "This notice concerns report storage, not whether loading succeeded. The report may still be available in memory.";
            action = "If you need saved reports, check free disk space and write access to the game's save-data folder. Technical details are in the log.";
        }
        else if (id == "display-scheduling")
        {
            change = when + "left some loading work as one uninterrupted step.";
            continues = "The loading display can still run, but may pause while that work finishes.";
            action = "No action needed for this limitation.";
        }
        else if (id.StartsWith("background", StringComparison.Ordinal))
        {
            continues = "Keep the game window open and focused while this work finishes. This adjustment does not switch off your other Wake-Up features.";
        }
        if (mod.Length == 0 && mods != "Wake-Up" && (n.Provider == "Other loading mod" || code.StartsWith("foreign-", StringComparison.Ordinal)
            || n.Details.Contains("another mod") || n.Details.Contains("foreign-")))
            change += " Wake-Up could not identify which mod changed this part of loading.";
        if (!n.Current)
        {
            string current = n.CurrentState switch {
                OperationState.Available => "This improvement is available on this launch. ",
                OperationState.Off => "This option is off on this launch. ",
                OperationState.NotApplicable => "This improvement does not apply on this launch. ",
                _ => "Check current Technical compatibility details before changing settings. " };
            // Old choices must not masquerade as current settings or require stale action.
            action = "This is a saved notice. " + current
                + (n.CurrentState == OperationState.Available || n.CurrentState == OperationState.Off || n.CurrentState == OperationState.NotApplicable
                    ? "No action is required for this old decision."
                    : kind == NoticeKind.ActionNeeded || kind == NoticeKind.Advisory || kind == NoticeKind.OptionalChoice ? "If the same situation still applies: " + action : "No action is required just to dismiss this saved notice.");
        }
        return new CompatibilityCard(area, Pretty(mods), kind, change, continues, action, cause, n.Current, new[] { n.Key });
    }

    internal static CompatibilityCard[] Group(IEnumerable<CompatibilityNotice> notices)
        => notices.Select(Describe).GroupBy(c => new { c.Area, c.Mods, c.Kind, c.Current, c.Cause, c.Change, c.Continues, c.Action })
            .Select(g => new CompatibilityCard(g.Key.Area, g.Key.Mods, g.Key.Kind, g.Key.Change, g.Key.Continues,
                g.Key.Action, g.Key.Cause, g.Key.Current, g.SelectMany(c => c.Keys).ToArray())).ToArray();

    private static string ModName(string provider)
        => provider == "Wake-Up" || provider == "Other loading mod" || provider == "Native XML" || provider.Length > 160 ? "" : provider;
    private static string Pretty(string value) => value == "YaOpt" ? "Yet Another Optimizer (YaOpt)" : value == "WOWGAG" ? "WOWGAG's Performance Patch" : value;
    private static string Area(string id)
    {
        if (id.StartsWith("definitions", StringComparison.Ordinal) || id == "single-query" || id == "query-plans" || id == "extended-query") return "Definition and template searches";
        if (id.StartsWith("reflection", StringComparison.Ordinal) || id == "type-name" || id == "leaf" || id == "attributes") return "Code searches during loading";
        if (id.StartsWith("asset-routing", StringComparison.Ordinal)) return "Finding images and other resources";
        return id switch {
            "giddy" => "Preparing rider positions", "character" => "Preparing character presets", "gagarin" => "Reusing saved mod data",
            "translations" => "Applying translated text", "streaming-xml" => "Reading mod data", "processed-xml" => "Reusing completed mod-data changes",
            "texture-loader" or "texture-cache" or "prepared" or "psd" or "quality" => "Image loading and caching",
            "repaint" or "display" or "display-diagnostics" or "display-scheduling" => "Loading screen",
            "summary" => "Loading summary and DLC panel", "background" => "Loading saves while the game window is minimized",
            "background-world" => "Creating worlds while the game window is minimized", "background-colony" => "Starting colonies while the game window is minimized",
            "background-map" => "Loading maps while the game window is minimized",
            "observation" or "invocations" or "xml-timings" or "early-observation" or "report-storage" or "observation/content" or "xml-timings/files" => "Loading progress and reports",
            _ => "A loading improvement" };
    }
}

// Coverage of rendered card fragments, so a card taller than the viewport can
// still be read by scrolling. A gap never counts as having been displayed.
internal sealed class NoticeReadProgress
{
    private readonly List<(float Start, float End)> seen = new();
    internal bool Observe(float start, float end, float height)
    {
        start = Math.Max(0, start); end = Math.Min(height, end);
        if (end > start) seen.Add((start, end));
        seen.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (int i = 1; i < seen.Count;)
        {
            if (seen[i].Start <= seen[i - 1].End + 1)
            { seen[i - 1] = (seen[i - 1].Start, Math.Max(seen[i - 1].End, seen[i].End)); seen.RemoveAt(i); }
            else i++;
        }
        return seen.Count == 1 && seen[0].Start <= 1 && seen[0].End >= height - 1;
    }
}
