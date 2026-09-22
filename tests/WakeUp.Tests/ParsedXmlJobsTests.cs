// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ParsedXmlJobsTests
{
    [Test]
    public void OutOfOrderCompletionRetainsIndexedResultsWithoutReplayingJobs()
    {
        var results = new int[12];
        var calls = new int[results.Length];
        var completion = new ConcurrentQueue<int>();
        using var secondFinished = new ManualResetEventSlim();
        ParsedXmlJobs.Run(results.Length, index =>
        {
            if (index == 0 && !secondFinished.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            Interlocked.Increment(ref calls[index]);
            results[index] = index * index;
            completion.Enqueue(index);
            if (index == 1) secondFinished.Set();
        }, true);
        Assert.That(results, Is.EqualTo(Enumerable.Range(0, results.Length).Select(index => index * index)));
        Assert.That(calls, Is.All.EqualTo(1));
        int[] order = completion.ToArray();
        Assert.That(Array.IndexOf(order, 1), Is.LessThan(Array.IndexOf(order, 0)));
    }

    [Test]
    public void OriginalFailureReturnsOnlyAfterEveryWorkerHasStopped()
    {
        var original = new InvalidOperationException("private-input-failed");
        Exception? observed = null;
        int active = 0, completed = 0;
        var threads = new ConcurrentDictionary<int, Thread>();
        using var entered = new CountdownEvent(3);
        using var throwing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var returned = new ManualResetEventSlim();
        var caller = new Thread(() =>
        {
            try
            {
                ParsedXmlJobs.Run(3, index =>
                {
                    threads.TryAdd(Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread);
                    Interlocked.Increment(ref active);
                    try
                    {
                        entered.Signal();
                        if (!entered.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                        if (index == 0) { throwing.Set(); throw original; }
                        if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                        Interlocked.Increment(ref completed);
                    }
                    finally { Interlocked.Decrement(ref active); }
                }, true);
            }
            catch (Exception exception) { observed = exception; }
            finally { returned.Set(); }
        }) { IsBackground = true };
        caller.Start();
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(throwing.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(returned.IsSet, Is.False, "The caller must join the two still-active jobs before rethrowing.");
        }
        finally
        {
            release.Set();
            Assert.That(caller.Join(TimeSpan.FromSeconds(10)), Is.True);
        }
        Assert.That(observed, Is.SameAs(original));
        Assert.That(active, Is.Zero);
        Assert.That(completed, Is.EqualTo(2));
        Assert.That(threads.Values.All(thread => !thread.IsAlive), Is.True);
    }

    [Test]
    public void ParallelWorkUsesAtMostTwoWorkersAndTheCaller()
    {
        int active = 0, maximum = 0, entered = 0;
        int caller = Thread.CurrentThread.ManagedThreadId;
        var threads = new ConcurrentDictionary<int, byte>();
        using var allThree = new ManualResetEventSlim();
        ParsedXmlJobs.Run(24, _ =>
        {
            threads.TryAdd(Thread.CurrentThread.ManagedThreadId, 0);
            int current = Interlocked.Increment(ref active);
            int previous;
            do { previous = Volatile.Read(ref maximum); }
            while (current > previous && Interlocked.CompareExchange(ref maximum, current, previous) != previous);
            try
            {
                if (Interlocked.Increment(ref entered) == 3) allThree.Set();
                if (!allThree.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            }
            finally { Interlocked.Decrement(ref active); }
        }, true);
        Assert.That(maximum, Is.EqualTo(3));
        Assert.That(threads.Count, Is.EqualTo(3));
        Assert.That(threads.ContainsKey(caller), Is.True);
        Assert.That(entered, Is.EqualTo(24));
    }

    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(12, false)]
    public void SerialAndTrivialWorkStayOnTheCallingThread(int count, bool parallel)
    {
        int caller = Thread.CurrentThread.ManagedThreadId;
        int calls = 0;
        ParsedXmlJobs.Run(count, index =>
        {
            Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(caller));
            Assert.That(index, Is.EqualTo(calls));
            calls++;
        }, parallel);
        Assert.That(calls, Is.EqualTo(count));
    }

    [Test]
    public void SerialFailureDoesNotReplayOrContinueWork()
    {
        var original = new InvalidOperationException("serial-input-failed");
        int calls = 0;
        var observed = Assert.Throws<InvalidOperationException>((Action)(() => ParsedXmlJobs.Run(12, _ =>
        {
            if (++calls == 2) throw original;
        }, false)));
        Assert.That(observed, Is.SameAs(original));
        Assert.That(calls, Is.EqualTo(2));
    }
}
