// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;

namespace RimWorldLoadingOptimizer.RimWorld;

internal enum StartupSelection : byte
{
    Off = 0,
    Baseline = 1,
    Candidate = 2,
}

internal readonly struct StartupLaunchDecision
{
    internal StartupLaunchDecision(StartupSelection selection, string reason, string? saveDataRoot)
    {
        Selection = selection;
        Reason = reason;
        SaveDataRoot = saveDataRoot;
    }

    internal StartupSelection Selection
    {
        get;
    }
    internal string Reason
    {
        get;
    }
    internal string? SaveDataRoot
    {
        get;
    }
}

internal static class StartupLaunchSelector
{
    internal static StartupLaunchDecision Parse(IReadOnlyList<string> arguments)
    {
        if (arguments is null)
            throw new ArgumentNullException(nameof(arguments));
        string? selected = null;
        string? saveDataRoot = null;
        bool conflict = false;
        bool reference = false;
        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index] ?? string.Empty;
            if (string.Equals(argument, "--rlo-reference", StringComparison.Ordinal)
                || string.Equals(argument, "--rlo-bypass", StringComparison.Ordinal))
            {
                reference = true;
            }

            if (argument.StartsWith("--rlo-op7=", StringComparison.Ordinal))
            {
                string value = argument.Substring("--rlo-op7=".Length);
                if (selected is not null)
                    conflict = true;
                selected = value;
            }

            int separator = argument.IndexOf('=');
            if (separator > 0
                && (string.Equals(argument.Substring(0, separator), "-savedatafolder", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument.Substring(0, separator), "savedatafolder", StringComparison.OrdinalIgnoreCase)))
            {
                string value = argument.Substring(separator + 1);
                if (saveDataRoot is not null || value.Length == 0)
                    conflict = true;
                saveDataRoot = value;
            }
        }

        if (conflict)
            return new StartupLaunchDecision(StartupSelection.Off, "selector-ambiguous", null);
        if (selected is null)
            return new StartupLaunchDecision(StartupSelection.Off, "selector-absent", null);
        if (reference)
            return new StartupLaunchDecision(StartupSelection.Off, "reference-precedence", null);
        StartupSelection selection = string.Equals(selected, "baseline", StringComparison.Ordinal)
            ? StartupSelection.Baseline
            : string.Equals(selected, "candidate", StringComparison.Ordinal)
                ? StartupSelection.Candidate
                : StartupSelection.Off;
        if (selection == StartupSelection.Off)
            return new StartupLaunchDecision(selection, "selector-unknown", null);
        if (string.IsNullOrWhiteSpace(saveDataRoot) || !Path.IsPathRooted(saveDataRoot))
            return new StartupLaunchDecision(StartupSelection.Off, "isolated-savedata-required", null);
        string canonical = Path.GetFullPath(saveDataRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (canonical.StartsWith("\\\\", StringComparison.Ordinal))
            return new StartupLaunchDecision(StartupSelection.Off, "isolated-savedata-unsafe", null);
        return new StartupLaunchDecision(selection, "selected", canonical);
    }
}
