// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using Verse;

namespace WakeUp;

internal static class StartupFeatureRunner
{
    internal static void Run(string name, Action initialize)
        => Run(initialize, exception =>
        {
            CompatibilityStatus.SetupFailed(name, exception);
            Log.Warning("[Wake-Up] " + name + " setup failed: "
                + exception.GetType().Name + ". Other features will continue their own checks.");
        });

    // Static initialization can fail before a feature's own TryInitialize body
    // is entered. Keep that failure inside its feature boundary as well.
    internal static void Run(Action initialize, Action<Exception> reportFailure)
    {
        try
        {
            initialize();
        }
        catch (Exception exception) { reportFailure(exception); }
    }
}
