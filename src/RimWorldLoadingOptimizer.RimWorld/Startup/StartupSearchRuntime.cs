// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;

namespace RimWorldLoadingOptimizer.RimWorld;

// One launch selector for the independently measured searches. Each component
// keeps its own admission, bounds, fallback and receipt. Failure of one does not
// grant it authority or prevent the other components from safely initializing.
internal static class StartupSearchRuntime
{
    private const string Selector = "--rlo-strategy=startup-searches";

    internal static void Initialize(IReadOnlyList<string> arguments)
    {
        if (!arguments.Contains(Selector))
        {
            // Each explicit single-search selector remains interpreted by its
            // own component; missing/conflicting selectors still refuse work.
            StartupFeatureRunner.Run("Definition searches", () => DefLookupRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Type searches", () => TypeLookupRuntime.TryInitialize(arguments));
            return;
        }
        // Retain every other argument, including the comparison mode and
        // isolated profile path. Multiple strategy arguments stay invalid in
        // each component's existing selector validation.
        if (!arguments.Contains("--rlo-user-type-search=off"))
            StartupFeatureRunner.Run("Type searches", () => TypeLookupRuntime.TryInitialize(For(arguments, "type-lookup")));
        if (!arguments.Contains("--rlo-user-def-search=off"))
            StartupFeatureRunner.Run("Definition searches", () => DefLookupRuntime.TryInitialize(For(arguments, "def-lookup")));
    }

    private static string[] For(IReadOnlyList<string> arguments, string strategy)
        => arguments.Select(argument => argument == Selector ? "--rlo-strategy=" + strategy : argument).ToArray();
}
