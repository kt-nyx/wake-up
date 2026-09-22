// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WakeUp;

// One native loading chain, including required scene arrival and queued follow-ups.
// Engine access stays in the adapter. No saved preference or time speed is written.
internal sealed class BackgroundLoadingSession
{
    private readonly Func<bool> preference;
    private readonly Action<bool> apply;
    private readonly Func<int> frame;
    private int boundaries;
    private int completionFrame = -1;
    private bool completionAwaitingFocus;
    private bool completionCanceled;
    // A nested operation owns its own permission. Refusal or failure of that
    // operation cannot cancel an outer save or world lifetime.
    internal sealed class Lease
    {
        internal readonly string Owner;
        internal bool AwaitingPlay;
        internal Lease(string owner, bool awaitingPlay) { Owner = owner; AwaitingPlay = awaitingPlay; }
    }
    private readonly List<Lease> leases = new();
    internal bool Active => leases.Count != 0;
    internal bool AwaitingPlay => leases.Any(lease => lease.AwaitingPlay);
    internal bool HasOwner(string owner) => leases.Any(lease => lease.Owner == owner);

    internal BackgroundLoadingSession(Func<bool> preference, Action<bool> apply, Func<int> frame)
    { this.preference = preference; this.apply = apply; this.frame = frame; }

    internal Lease Begin(bool waitForPlay = true, string owner = "save")
    {
        completionCanceled = false;
        var lease = new Lease(owner, waitForPlay);
        leases.Add(lease);
        apply(true);
        return lease;
    }
    internal void Enter() => boundaries++;
    internal void Leave() { if (boundaries > 0) boundaries--; }
    internal void ArrivedOrCanceled() { foreach (var lease in leases) lease.AwaitingPlay = false; }
    internal void ApplyPreference(bool intended)
    {
        if (intended) completionAwaitingFocus = false;
        apply(Active || intended);
    }
    internal void FinishIfIdle(bool anyEvents)
    {
        if (Active && !anyEvents && boundaries == 0 && !AwaitingPlay) Release();
    }
    internal void Release(Lease lease)
    {
        if (leases.Remove(lease) && !Active) Restore();
    }
    internal void ReleaseOwner(string owner)
    {
        if (leases.RemoveAll(lease => lease.Owner == owner) != 0 && !Active) Restore();
    }
    internal void Release()
    {
        if (!Active) return;
        leases.Clear();
        Restore();
    }
    private void Restore()
    {
        if (completionCanceled) ClearCompletionGuard();
        else MarkCompletion();
        apply(preference());
    }
    internal void MarkCompletion()
    {
        completionFrame = frame();
        completionAwaitingFocus = !preference();
    }
    internal void ClearCompletionGuard()
    {
        completionFrame = -1;
        completionAwaitingFocus = false;
    }
    internal void CancelCompletionGuard()
    {
        completionCanceled = true;
        ClearCompletionGuard();
    }
    internal bool NeedsPlayGuard
    {
        get
        {
            if (preference()) { completionAwaitingFocus = false; return false; }
            return Active || completionFrame == frame() || completionAwaitingFocus;
        }
    }
    internal bool BlockPlay(bool focused)
    {
        // Unity can keep delivering frames with a stale focused flag after a
        // minimized load. Keep this load's guard until actual focus returns or
        // the player permits background gameplay; never change simulation speed.
        if (focused || preference()) completionAwaitingFocus = false;
        return NeedsPlayGuard && !focused;
    }
}
