// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;

namespace WakeUp;

// Owns one native callback-list invocation across frames. The live list is kept
// until completion, so callbacks appended by callbacks remain in native order.
internal sealed class LoadingCallbackSequence : IEnumerator, IDisposable
{
    private readonly List<Action> callbacks;
    private readonly Func<Action, IEnumerator> execute;
    private readonly Action<Exception> error;
    private readonly Action finished;
    private int consumed;
    private IEnumerator? current;
    private bool disposed;
    internal LoadingCallbackSequence(List<Action> callbacks, Func<Action, IEnumerator> execute,
        Action<Exception> error, Action finished)
    { this.callbacks = callbacks; this.execute = execute; this.error = error; this.finished = finished; }
    public object? Current => null;
    public bool MoveNext()
    {
        if (disposed) return false;
        if (current == null)
        {
            if (consumed == callbacks.Count) { Dispose(); return false; }
            Action callback = callbacks[consumed++];
            try { current = execute(callback); }
            catch (Exception exception) { error(exception); return true; }
        }
        try { if (current.MoveNext()) return true; }
        catch (Exception exception) { error(exception); }
        DisposeCurrent();
        return true;
    }
    private void DisposeCurrent()
    {
        var previous = current; current = null;
        try { (previous as IDisposable)?.Dispose(); }
        catch (Exception exception) { error(exception); }
    }
    public void Reset() => throw new NotSupportedException();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { DisposeCurrent(); callbacks.RemoveRange(0, consumed); }
        finally { finished(); }
    }
}
