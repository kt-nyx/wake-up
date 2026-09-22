// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
namespace WakeUp;

internal static class LoadingFeatureFeedback
{
    // Invoked only on the existing 250 ms diagnostic formatting budget. Read
    // retained status/counters; do not enumerate content or sample engine state.
    internal static string Snapshot() =>
        Bound(StreamingXmlRuntime.Status) + "\n" + Bound(AssetRoutingRuntime.Status) + "\n"
        + "Prepared textures: " + Bound(PreparedTextureRuntime.Status)
        + " (used " + PreparedTextureRuntime.Hits + ", missing " + PreparedTextureRuntime.Misses + ", refused " + PreparedTextureRuntime.Refused + ")."
        + (LoadingTimingRuntime.Attribution.Length == 0 ? "" : "\nObserving " + Bound(LoadingTimingRuntime.Attribution));
    private static string Bound(string value) => (value.Length > 210 ? value.Substring(0, 210) + "…" : value).Replace('\r', ' ').Replace('\n', ' ');
}
