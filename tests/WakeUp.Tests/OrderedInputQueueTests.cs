// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class OrderedInputQueueTests
{
    private string root = null!;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "wake-up-input-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TearDown]
    public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private FileInfo Write(string name, params byte[] bytes)
    {
        string path = Path.Combine(root, name);
        File.WriteAllBytes(path, bytes);
        return new FileInfo(path);
    }

    [Test]
    public void OutOfOrderCompletionStillTransfersNativeOrderAndReadsExactBytes()
    {
        var files = new[] { Write("first", 1, 2, 3), Write("second", 8, 7) };
        using var firstStarted = new ManualResetEventSlim();
        using var allowFirst = new ManualResetEventSlim();
        using var queue = new OrderedInputQueue(files, beforeRead: (index, token) =>
        {
            if (index == 0) { firstStarted.Set(); allowFirst.Wait(token); }
            else firstStarted.Wait(token);
        });
        try
        {
            Assert.That(SpinWait.SpinUntil(() => queue.Completed == 1, 5000), Is.True, "second source completed while first is held");
            Assert.That(queue.MaximumActive, Is.EqualTo(2));
            Assert.That(queue.DifferentFileOverlap, Is.GreaterThan(0));
            Assert.That(queue.Overlaps.Any(pair => pair.Item1 == 0 && pair.Item2 == 1 || pair.Item1 == 1 && pair.Item2 == 0), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => queue.Take(1)));
            allowFirst.Set();
            using (var first = queue.Take(0))
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(first!.File, Is.SameAs(files[0]));
                Assert.That(first.Bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
                using var sha = SHA256.Create();
                Assert.That(first.Digest, Is.EqualTo(sha.ComputeHash(first.Bytes)));
                Assert.Throws<InvalidOperationException>((Action)(() => queue.Take(1)));
            }
            using (var second = queue.Take(1)) Assert.That(second!.Bytes, Is.EqualTo(new byte[] { 8, 7 }));
            Assert.That(queue.Taken, Is.EqualTo(2));
            Assert.That(queue.ReservedBytes, Is.Zero);
            Assert.That(queue.Outstanding, Is.Zero);
        }
        finally { allowFirst.Set(); }
    }

    [Test]
    public void CrossGroupReceiptsKeepCrossSourcePairsBeyondUngroupedReceiptLimit()
    {
        // The first group has more pairs than the ordinary receipt can retain.
        // Holding its first job keeps overlap deterministic until group 2 arrives.
        var files = Enumerable.Range(0, 68).Select(i => Write(i.ToString(), (byte)i)).ToArray();
        var groups = Enumerable.Range(0, files.Length).Select(i => i == 67 ? 2 : 1).ToArray();
        using var firstStarted = new ManualResetEventSlim();
        using var allowFirst = new ManualResetEventSlim();
        using var queue = new OrderedInputQueue(files, maxJobs: 68, sourceGroups: groups,
            beforeRead: (index, token) =>
            {
                if (index == 0) { firstStarted.Set(); allowFirst.Wait(token); }
                else firstStarted.Wait(token);
            });
        try
        {
            Assert.That(SpinWait.SpinUntil(() => queue.Completed == 67, 5000), Is.True);
            Assert.That(queue.Overlaps.Count, Is.EqualTo(64));
            Assert.That(queue.CrossGroupOverlap, Is.EqualTo(1));
            Assert.That(queue.CrossGroupOverlaps.Count, Is.EqualTo(1));
            Tuple<int, int> pair = queue.CrossGroupOverlaps[0];
            Assert.That(new[] { pair.Item1, pair.Item2 }, Is.EquivalentTo(new[] { 0, 67 }));
            allowFirst.Set();
            var consumed = new bool[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                using var snapshot = queue.Take(i);
                consumed[i] = snapshot != null;
            }
            Assert.That(consumed[pair.Item1] && consumed[pair.Item2], Is.True);
            Assert.That(queue.Taken, Is.EqualTo(files.Length));
            Assert.That(queue.ReservedBytes, Is.Zero);
        }
        finally { allowFirst.Set(); }
    }

    [Test]
    public void WorkerErrorsAndUnsupportedLengthsRemainOrderedNativeFallbackSlots()
    {
        var files = new[] { new FileInfo(Path.Combine(root, "missing")), Write("empty"), Write("error", 1), Write("large", 1), Write("good", 4) };
        using (var large = new FileStream(files[3].FullName, FileMode.Open, FileAccess.Write)) large.SetLength(OrderedInputQueue.MaximumFileBytes + 1L);
        using var queue = new OrderedInputQueue(files, maxJobs: 2, beforeRead: (index, _) =>
        {
            if (index == 2) throw new IOException("private-worker-failure");
        });
        for (int i = 0; i < 4; i++) Assert.That(queue.Take(i), Is.Null);
        using (var good = queue.Take(4)) Assert.That(good!.Bytes, Is.EqualTo(new byte[] { 4 }));
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        Assert.That(queue.Completed, Is.EqualTo(2));
        Assert.That(queue.Taken, Is.EqualTo(1));
        Assert.That(queue.ReservedBytes, Is.Zero);
    }

    [Test]
    public void ActualOpenedBytesAreUsedAndGrowthPastReservationFallsBack()
    {
        var files = new[] { Write("same-length", 1, 2), Write("grew", 3) };
        using var queue = new OrderedInputQueue(files, beforeRead: (index, _) =>
        {
            File.WriteAllBytes(files[index].FullName, index == 0 ? new byte[] { 9, 8 } : new byte[] { 7, 6 });
        });
        using (var first = queue.Take(0))
        {
            Assert.That(first!.Bytes, Is.EqualTo(new byte[] { 9, 8 }));
            using var sha = SHA256.Create();
            Assert.That(first.Digest, Is.EqualTo(sha.ComputeHash(new byte[] { 9, 8 })));
        }
        Assert.That(queue.Take(1), Is.Null);
        Assert.That(queue.ReservedBytes, Is.Zero);
    }

    [Test]
    public void ConsumedSourceKeepsWindowsMutationAndDeleteLeaseUntilDisposed()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows sharing contract; Linux promotion remains separate.");
        FileInfo file = Write("leased", 1, 2);
        using var queue = new OrderedInputQueue(new[] { file });
        using (var snapshot = queue.Take(0))
        {
            Assert.Throws<IOException>((Action)(() => { using var writer = new FileStream(file.FullName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); }));
            Assert.Throws<IOException>((Action)(() => File.Delete(file.FullName)));
            Assert.That(snapshot!.Bytes, Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(queue.ReservedBytes, Is.EqualTo(2));
        }
        File.WriteAllBytes(file.FullName, new byte[] { 3 });
        Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(new byte[] { 3 }));
        Assert.That(queue.ReservedBytes, Is.Zero);
    }

    [Test]
    public void SlidingAdmissionBoundsCompletedAndTransferredBuffersWithoutStarvation()
    {
        var files = Enumerable.Range(0, 20).Select(i => Write(i.ToString(), (byte)i, 1, 2)).ToArray();
        using var queue = new OrderedInputQueue(files, maxJobs: 3, maxBytes: 7);
        for (int i = 0; i < files.Length; i++)
        {
            using var snapshot = queue.Take(i);
            Assert.That(snapshot!.Bytes[0], Is.EqualTo(i));
            Assert.That(queue.ReservedBytes, Is.InRange(3, 7));
            Assert.That(queue.Outstanding, Is.InRange(1, 3));
        }
        Assert.That(queue.PeakReservedBytes, Is.LessThanOrEqualTo(7));
        Assert.That(queue.PeakOutstanding, Is.LessThanOrEqualTo(3));
        Assert.That(queue.Taken, Is.EqualTo(20));
        Assert.That(queue.ReservedBytes, Is.Zero);
        Assert.That(queue.Outstanding, Is.Zero);
    }

    [Test]
    public void SourceLargerThanQueueBudgetFallsBackWithoutBlockingLaterWork()
    {
        var files = new[] { Write("too-large", 1, 2, 3, 4), Write("fits", 5) };
        using var queue = new OrderedInputQueue(files, maxBytes: 3);
        Assert.That(queue.Take(0), Is.Null);
        using (var good = queue.Take(1)) Assert.That(good!.Bytes, Is.EqualTo(new byte[] { 5 }));
        Assert.That(queue.ReservedBytes, Is.Zero);
    }

    [Test]
    public void DisposeCancelsBlockedWorkersJoinsAndReleasesQueuedAndCompletedSources()
    {
        var files = Enumerable.Range(0, 8).Select(i => Write(i.ToString(), (byte)i)).ToArray();
        using var blocked = new ManualResetEventSlim();
        using var never = new ManualResetEventSlim();
        using var queue = new OrderedInputQueue(files, maxJobs: 4, beforeRead: (index, token) =>
        {
            if (index == 1) { blocked.Set(); never.Wait(token); }
        });
        Assert.That(blocked.Wait(5000), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Completed > 0, 5000), Is.True);
        queue.Dispose();
        Assert.That(queue.WorkersAlive, Is.False);
        Assert.That(queue.ReservedBytes, Is.Zero);
        Assert.That(queue.Outstanding, Is.Zero);
        Assert.Throws<ObjectDisposedException>((Action)(() => queue.Take(0)));
        foreach (FileInfo file in files) File.WriteAllBytes(file.FullName, new byte[] { 42 });
        queue.Dispose();
    }

    [Test]
    public void TransferredSnapshotRetainsItsConsumerOwnershipAcrossQueueDisposal()
    {
        FileInfo file = Write("consumer", 1, 2);
        using var queue = new OrderedInputQueue(new[] { file });
        using var snapshot = queue.Take(0);
        queue.Dispose();
        Assert.That(snapshot!.Bytes, Is.EqualTo(new byte[] { 1, 2 }));
        Assert.That(queue.ReservedBytes, Is.EqualTo(2));
        snapshot.Dispose();
        snapshot.Dispose();
        Assert.That(queue.ReservedBytes, Is.Zero);
        Assert.That(queue.Outstanding, Is.Zero);
    }
}

