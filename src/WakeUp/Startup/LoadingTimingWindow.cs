// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace WakeUp;

internal sealed class LoadingTimingWindow : Window
{
    private Vector2 scroll;
    private LoadingSession? selected;
    private GUIContent[] lines = { new("No observed sessions this launch. Enable loading observations and restart to include startup.") };
    private float[] offsets = Array.Empty<float>();
    private GUIStyle? style;
    private float layoutWidth = -1, layoutHeight;
    private string feedback = "Select a session. Active sessions are snapshots; Refresh updates the displayed report.";
    public override Vector2 InitialSize => new(1050f, 720f);
    internal LoadingTimingWindow()
    {
        doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        Select(LoadingObservationRuntime.Sessions.LastOrDefault());
    }
    private void Select(LoadingSession? session)
    {
        selected = session;
        if (session != null) lines = session.RenderReport().Replace("\r", "").Split('\n').Select(line => new GUIContent(line.Length == 0 ? " " : line)).ToArray();
        layoutWidth = -1;
        scroll = Vector2.zero;
    }
    public override void DoWindowContents(Rect inRect)
    {
        style ??= new GUIStyle(Text.CurFontStyle) { wordWrap = true, richText = false };
        float half = (inRect.width - 10f) / 2f;
        if (Widgets.ButtonText(new Rect(0, 0, half, 32), selected == null ? "Choose loading session" : selected.Name + " · " + selected.Id.Substring(0, 8)))
        {
            var options = LoadingObservationRuntime.Sessions.Select((session, index) =>
                new FloatMenuOption((index + 1) + ". " + session.Name + " · " + session.Id.Substring(0, 8)
                    + (session.IsActive ? " (active)" : " (finished)"), () => Select(session))).ToList();
            if (options.Count == 0) feedback = "No sessions recorded this launch. " + LoadingObservationRuntime.Status;
            else Find.WindowStack.Add(new FloatMenu(options));
        }
        if (Widgets.ButtonText(new Rect(half + 10, 0, half / 2 - 5, 32), "Refresh snapshot"))
            Select(selected ?? LoadingObservationRuntime.Sessions.LastOrDefault());
        if (Widgets.ButtonText(new Rect(half * 1.5f + 10, 0, half / 2 - 5, 32), "Save report"))
        {
            try { feedback = selected == null ? "Choose a recorded session first." : "Saved: " + LoadingObservationRuntime.SaveReport(selected); }
            catch (Exception e) { feedback = "Report could not be saved: " + e.Message; }
        }
        Widgets.Label(new Rect(0, 40, inRect.width, 70), feedback);
        var area = new Rect(0, 116, inRect.width, Math.Max(30, inRect.height - 171));
        float width = Math.Max(40, area.width - 22);
        if (Math.Abs(layoutWidth - width) > .5f)
        {
            layoutWidth = width;
            offsets = new float[lines.Length + 1];
            for (int i = 0; i < lines.Length; i++) offsets[i + 1] = offsets[i] + Math.Max(20, style.CalcHeight(lines[i], width));
            layoutHeight = Math.Max(area.height, offsets[lines.Length]);
        }
        var view = new Rect(0, 0, width, layoutHeight);
        scroll = GUI.BeginScrollView(area, scroll, view);
        try
        {
            // Only visible lines become GUI text meshes, even on a full modlist.
            int first = Array.BinarySearch(offsets, scroll.y);
            if (first < 0) first = Math.Max(0, ~first - 1);
            for (int i = first; i < lines.Length && offsets[i] < scroll.y + area.height; i++)
                GUI.Label(new Rect(0, offsets[i], width, offsets[i + 1] - offsets[i]), lines[i], style);
        }
        finally { GUI.EndScrollView(); }
    }
}
