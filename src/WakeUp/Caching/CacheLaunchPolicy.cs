// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

namespace WakeUp;

internal enum CacheAction { Normal, Bypass, Rebuild, Clear }

// Every owned cache sees the same decision for this launch. A consumer never
// consumes the saved one-shot request itself.
internal sealed class CacheLaunchPolicy
{
    private static CacheLaunchPolicy? current;
    private static readonly CacheLaunchPolicy normal = new(CacheAction.Normal);
    internal static CacheLaunchPolicy Current => current ?? normal;
    internal CacheAction Action { get; }
    internal bool AllowRead => Action == CacheAction.Normal;
    internal bool AllowWrite => Action == CacheAction.Normal || Action == CacheAction.Rebuild;

    private CacheLaunchPolicy(CacheAction action) => Action = action;

    internal static void Initialize(string requested, System.Action consumeRequest)
        => Latch(ref current, requested, consumeRequest);

    // Passing the latch explicitly also lets offline consumers exercise the
    // launch boundary without resetting global production state.
    internal static CacheLaunchPolicy Latch(ref CacheLaunchPolicy? selected, string requested, System.Action consumeRequest)
    {
        if (selected != null)
            return selected;
        CacheAction action = requested == "bypass" ? CacheAction.Bypass
            : requested == "rebuild" ? CacheAction.Rebuild
            : requested == "clear" ? CacheAction.Clear : CacheAction.Normal;
        selected = new CacheLaunchPolicy(action);
        // Unknown saved values also return to normal rather than recurring.
        // Select first: even a failed settings write cannot change this launch.
        if (requested != "normal")
            consumeRequest();
        return selected;
    }
}
