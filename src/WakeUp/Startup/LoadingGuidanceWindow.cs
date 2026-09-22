// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using UnityEngine;
using Verse;

namespace WakeUp;

internal sealed class LoadingGuidanceWindow : Window
{
    private Vector2 scroll;
    private GUIStyle? style;
    private float width = -1, height;
    private readonly GUIContent content = new(
        "Wake-Up loading help\n\n" +
        "Loading display and reports\n" +
        "The display describes work that is actually executing. A completed/total count is shown only where the native sequence supplies a known total. An individual mod constructor can block until it returns; its name is useful context, not a promise of smooth progress. Errors and unobserved work belong in the report. An unsupported hook leaves ordinary loading intact.\n\n" +
        "If Loading Progress is active, it handles the loading display. Wake-Up's session reports remain a separate choice. Check the current feature status in settings to see which options are working or unavailable.\n\n" +
        "Open Loading session reports in settings to inspect startup and later loading sessions, then select Save report for a text copy. Refresh takes a new snapshot of an active session. Inclusive time contains nested work; exclusive time subtracts observed nested work. Work on different threads can overlap. Do not add stage totals or compare them with menu elapsed time as a speedup. Missing observations and truncation are reported explicitly.\n\n" +
        "XML diagnostics\n" +
        "Request XML export for the next launch in settings, then restart once. This explicitly writes combined and processed XML where supported, the active mod order, and source mappings. Paths identify local mod files and can disclose local folder names when shared. No full XML export runs without that request. Exports are limited to three launches and 256 MiB total; oversized output is marked incomplete. A source mapping describes origin, not ownership of every later patch. Cancel removes a request that has not started.\n\n" +
        "Cache maintenance and texture preparation\n" +
        "Use the existing cache maintenance controls in settings to clear Wake-Up-owned caches or bypass them on the next launch. The texture preparation window provides selection, dry run, prepare/resume, cancellation and owned-output clearing. Its coverage report explains which files were prepared, reused, skipped or failed. Converted output can deliberately change pixels; native preparation preserves the native producer's output. Original mod files remain separate. Restart when a setting says it affects the next launch.\n\n" +
        "Features still awaiting completion\n" +
        "Loading textures and audio only when needed, and progressively adding image detail, are deferred and cannot be activated in this release. Ordinary assets still load at their native points; this display does not imply background asset loading. Background-loading focus and ordinary-scene behavior still need qualification.\n\n" +
        "Keep capabilities you still need\n" +
        "Parsed XML/language reuse, inheritance caching, ordered read-ahead, authored-DDS recaching, managed first-build image acceleration, static atlas caching and atlas batching are retired from this release. The loading display still schedules supported native loading units for progress updates; retiring atlas options does not remove that scheduling. Keep supplier features you rely on; Wake-Up's unfinished asset features do not replace them. Performance Fish includes gameplay behavior outside Wake-Up's loading scope. External preparation tools may also provide workflows Wake-Up does not provide. Review the specific overlapping behavior before removing a mod or tool.\n\n" +
        "Mod artwork\n" +
        "A mod list does not need to open every preview image. Native lazy previews load artwork when it is requested. The loading display does not decode artwork merely to draw package names.\n\n" +
        "A loading report describes this session. Its timings alone do not establish that an option makes loading faster.");
    public override Vector2 InitialSize => new(850, 700);
    internal LoadingGuidanceWindow() { doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    public override void DoWindowContents(Rect inRect)
    {
        style ??= new GUIStyle(Text.CurFontStyle) { wordWrap = true, richText = false };
        var area = new Rect(0, 0, inRect.width, Math.Max(30, inRect.height - 55));
        float nextWidth = Math.Max(40, area.width - 22);
        if (Math.Abs(width - nextWidth) > .5f) { width = nextWidth; height = Math.Max(area.height, style.CalcHeight(content, width)); }
        var view = new Rect(0, 0, width, height);
        scroll = GUI.BeginScrollView(area, scroll, view);
        try { GUI.Label(view, content, style); }
        finally { GUI.EndScrollView(); }
    }
}
