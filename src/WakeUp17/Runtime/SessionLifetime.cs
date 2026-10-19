// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Reflection;

namespace WakeUp.V17;

internal sealed class SessionLifetime
{
    private static SessionLifetime? instance;
    private readonly RuntimeContext context;
    private readonly Func<bool> loaded, busy;
    internal SessionLifetime(RuntimeContext context, Func<bool> loaded, Func<bool> busy)
    { this.context = context; this.loaded = loaded; this.busy = busy; instance = this; }
    internal void Install(MethodInfo update)
    {
        context.Require(update);
        context.Patch("lifetime", update, postfix: RuntimeContext.Hook(typeof(SessionLifetime), nameof(AfterUpdate)));
    }
    private static void AfterUpdate()
    {
        var self = instance;
        if (self == null || !self.context.StartupActive || !self.loaded() || self.busy()) return;
        self.context.CompleteStartup();
        try { self.context.Unpatch("lifetime"); } catch { }
    }
}
