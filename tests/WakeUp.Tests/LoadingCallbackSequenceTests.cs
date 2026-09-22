// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

public class LoadingCallbackSequenceTests
{
    private static IEnumerator Run(Action action) { action(); yield break; }

    [Test]
    public void ExecutesOnlyWhenAdvancedAndPreservesAppendedCallbackOrder()
    {
        var order = new List<string>();
        var callbacks = new List<Action>();
        callbacks.Add(() => { order.Add("first"); callbacks.Add(() => order.Add("appended")); });
        callbacks.Add(() => order.Add("second"));
        int released = 0;
        using var sequence = new LoadingCallbackSequence(callbacks, Run, e => Assert.Fail(e.ToString()), () => released++);
        Assert.That(order, Is.Empty);
        Assert.That(sequence.MoveNext(), Is.True);
        Assert.That(order, Is.EqualTo(new[] { "first" }));
        Assert.That(released, Is.Zero);
        Assert.That(sequence.MoveNext(), Is.True);
        Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
        Assert.That(sequence.MoveNext(), Is.True);
        Assert.That(order, Is.EqualTo(new[] { "first", "second", "appended" }));
        Assert.That(sequence.MoveNext(), Is.False);
        Assert.That(callbacks, Is.Empty);
        Assert.That(released, Is.EqualTo(1));
    }

    [Test]
    public void PreservesFailureAndContinuesLaterCallbacks()
    {
        var failure = new InvalidOperationException("native callback failure");
        var errors = new List<Exception>();
        bool later = false;
        var callbacks = new List<Action> { () => throw failure, () => later = true };
        using var sequence = new LoadingCallbackSequence(callbacks, Run, errors.Add, () => { });
        while (sequence.MoveNext()) { }
        Assert.That(errors, Is.EqualTo(new[] { failure }));
        Assert.That(later, Is.True);
        Assert.That(callbacks, Is.Empty);
    }

    [Test]
    public void CancellationDoesNotRunOrDiscardUnstartedCallbacks()
    {
        int calls = 0, released = 0;
        Action second = () => calls += 10;
        var callbacks = new List<Action> { () => calls++, second };
        var sequence = new LoadingCallbackSequence(callbacks, Run, e => Assert.Fail(e.ToString()), () => released++);
        sequence.MoveNext();
        sequence.Dispose(); sequence.Dispose();
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(callbacks, Is.EqualTo(new[] { second }));
        Assert.That(released, Is.EqualTo(1));
        Assert.That(sequence.MoveNext(), Is.False);
    }

    [Test]
    public void StaticConstructorAdmissionMatchesNativeBody()
    {
        var method = AccessTools.Method(typeof(StaticConstructorOnStartupUtility), "CallAll");
        Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
        TestContext.Out.WriteLine("C13_NATIVE_CALLALL_BODY=" + hash);
        Assert.That(hash, Is.EqualTo(NativeLoadingUnits.CallAllBody));
    }
}
