// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Globalization;

namespace WakeUp;

internal readonly struct LoadingCacheSnapshot
{
    internal readonly bool Requested, Installed;
    internal readonly string Refusal, Reason;
    internal readonly CacheAction Action;
    internal readonly long Hits, Misses, Writes, Errors, Bytes;
    internal LoadingCacheSnapshot(bool requested, bool installed, string refusal, CacheAction action,
        long hits, long misses, long writes, long errors, long bytes, string reason)
    {
        Requested = requested; Installed = installed; Refusal = refusal; Action = action;
        Hits = hits; Misses = misses; Writes = writes; Errors = errors; Bytes = bytes; Reason = reason;
    }

    internal string Describe()
    {
        if (!Requested) return "PNG/JPEG cache: off; ordinary texture loading.";
        if (Refusal.Length != 0 || !Installed) return "PNG/JPEG cache: unavailable because its setup checks failed; ordinary texture loading. See Wake-Up's log.";
        if (Action == CacheAction.Bypass) return "PNG/JPEG cache: bypassed for this launch; existing files kept.";
        if (Action == CacheAction.Clear) return "PNG/JPEG cache: clear requested; unused this launch. See maintenance log for result.";
        string activity = "PNG/JPEG cache: " + Hits + " reused, " + Writes + " rebuilt, " + Misses + " misses.";
        if (Errors != 0) return activity + " " + Errors + " errors; affected textures use ordinary loading.";
        if (Reason == "quota" || Reason == "entry-count") return activity + " Storage limit reached; some textures stay uncached.";
        if (Action == CacheAction.Rebuild) return activity + " Rebuild requested; reads skipped.";
        return activity + (Misses != 0 ? " Missing or unusable entries use ordinary loading." : " Eligible textures only.");
    }
}

// The loading thread publishes only its current string. The GUI thread formats
// a bounded snapshot four times/second. No item callback requests a frame/wait.
internal sealed class LoadingDisplayState
{
    internal const double RefreshSeconds = 0.25;
    internal const int HistoryLimit = 64;
    private readonly object gate = new();
    private readonly bool diagnostics;
    private readonly double started;
    private readonly string[] history = new string[HistoryLimit];
    private readonly double[] historyTimes = new double[HistoryLimit];
    private int count;
    private long transitions;
    private string stage = "Loading";
    private string text = "";
    private double nextRefresh;
    private bool active = true;
    internal LoadingDisplayState(double now, bool diagnostics) { started = now; this.diagnostics = diagnostics; }
    internal bool Active { get { lock (gate) return active; } }
    internal long Transitions { get { lock (gate) return transitions; } }
    internal void Observe(string? value, double now)
    {
        lock (gate)
        {
            if (!active || string.IsNullOrEmpty(value) || string.Equals(stage, value, StringComparison.Ordinal)) return;
            stage = value!;
            transitions++;
            if (diagnostics && count < HistoryLimit)
            {
                history[count] = value!;
                historyTimes[count++] = Math.Max(0, now - started);
            }
        }
    }
    internal bool Refresh(double now, int mods, Func<LoadingCacheSnapshot> cache, out string result, Func<string>? features = null,
        Func<LoadingSession.View?>? observation = null, bool compact = false)
    {
        lock (gate)
        {
            result = text;
            if (!active || (text.Length != 0 && now < nextRefresh)) return false;
            nextRefresh = now + RefreshSeconds;
            var snapshot = cache();
            LoadingSession.View? session = observation?.Invoke();
            long seconds = (long)Math.Max(0, now - started);
            text = "Wake-Up · Loading\n" + Plain(stage, 160)
                + "\nElapsed since observation began: " + (seconds / 60).ToString(CultureInfo.InvariantCulture)
                + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture)
                + "  ·  " + mods + " mods (names hidden)\n" + snapshot.Describe();
            if (session.HasValue)
            {
                var current = session.Value;
                if (compact)
                {
                    text = "Wake-Up · " + Plain(current.Stage, 60) + " · "
                        + (current.Total.HasValue ? current.Completed + " / " + current.Total.Value + " completed"
                            : current.Completed > 0 ? current.Completed + " completed; total unknown" : "Working; total unknown")
                        + " · " + current.Errors + " logged errors\n"
                        + Plain(current.Work, 110) + " · "
                        + (current.Package == "shared/unknown" ? "Shared or unattributed work" : Plain(current.Package, 50));
                    result = text;
                    return true;
                }
                text = "Wake-Up · " + Plain(current.Stage, 120) + "\n" + Plain(current.Work, 200)
                    + "\n" + (current.Total.HasValue ? current.Completed + " / " + current.Total.Value + " completed in this stage"
                        : current.Completed > 0 ? current.Completed + " completed; total unknown" : "Working · total unknown")
                    + "\n" + (current.Package == "shared/unknown" ? "Shared or unattributed work" : Plain(current.Package, 120))
                    + " · " + current.Errors + " logged errors"
                    + "\n" + snapshot.Describe();
            }
            if (diagnostics)
                text += "\nDiagnostics: " + transitions + " stage changes; "
                    + (snapshot.Bytes / 1048576d).ToString("F1", CultureInfo.InvariantCulture) + " MiB stored; reason: " + Plain(snapshot.Reason, 64);
            if (diagnostics && features != null) text += "\n" + features();
            result = text;
            return true;
        }
    }
    internal string Stop(string reason)
    {
        lock (gate)
        {
            active = false;
            string record = "\"reason\":\"" + JsonLineLog.Escape(reason) + "\",\"stageChanges\":" + transitions;
            if (diagnostics)
            {
                var items = new string[count];
                for (int i = 0; i < count; i++)
                    items[i] = "{\"secondsSinceObservation\":" + historyTimes[i].ToString("F3", CultureInfo.InvariantCulture)
                        + ",\"stage\":\"" + JsonLineLog.Escape(Plain(history[i], 160)) + "\"}";
                record += ",\"historyTruncated\":" + (transitions > HistoryLimit ? "true" : "false") + ",\"stages\":[" + string.Join(",", items) + "]";
            }
            Array.Clear(history, 0, history.Length);
            stage = text = "";
            return record;
        }
    }
    private static string Plain(string value, int limit)
    {
        string bounded = value.Length > limit ? value.Substring(0, limit) + "…" : value;
        return bounded.Replace('\r', ' ').Replace('\n', ' ');
    }
}
