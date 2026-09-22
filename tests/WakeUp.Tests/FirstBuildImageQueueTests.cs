// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class FirstBuildImageQueueTests
{
    private string root = null!;

    [SetUp]
    public void Setup()
    {
        root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "first-image-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TearDown]
    public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private FileInfo File(int id, params byte[] bytes)
    {
        string path = Path.Combine(root, id + ".image");
        System.IO.File.WriteAllBytes(path, bytes);
        return new FileInfo(path);
    }

    [Test]
    public void ConcurrentPrivateResultsArePublishedInSourceOrderAndExplicitlyReceipted()
    {
        var files = new FileInfo?[] { File(0, 0), File(1, 1), File(2, 2) };
        int caller = Thread.CurrentThread.ManagedThreadId;
        using var secondEntered = new ManualResetEventSlim();
        using var queue = new FirstBuildImageQueue<byte[]>(files, stream =>
        {
            Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(caller));
            Assert.That(stream.ReadByte(), Is.GreaterThanOrEqualTo(0));
            return 4;
        }, (source, _) =>
        {
            if (source[0] == 0 && !secondEntered.Wait(5000)) throw new InvalidOperationException("Second worker did not enter.");
            if (source[0] == 1) secondEntered.Set();
            return source;
        }, workers: 2, jobs: 2);

        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(first!.Error, Is.Null);
        Assert.That(first.Value, Is.SameAs(first.Source));
        Assert.That(first.SourceComplete, Is.True);
        Assert.That(first.Source, Is.EqualTo(new byte[] { 0 }));
        Assert.That(queue.MaxActiveWorkers, Is.EqualTo(2));
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        Assert.That(queue.Consumed, Is.Zero);
        Assert.Throws<InvalidOperationException>((Action)(() => queue.TryTake(1, out _)));
        first.MarkConsumed(); first.MarkConsumed();
        first.Dispose();
        Assert.That(queue.Consumed, Is.EqualTo(1));
        Assert.That(queue.Scheduled, Is.EqualTo(2), "Releasing publication must not silently refill the horizon.");
        Assert.That(queue.TryTake(1, out var second), Is.True);
        Assert.That(second!.Source, Is.EqualTo(new byte[] { 1 }));
        second.Dispose();
        Assert.That(queue.TryTake(2, out var third), Is.True);
        Assert.That(third!.Source, Is.EqualTo(new byte[] { 2 }));
        third.MarkConsumed(); third.Dispose();
        Assert.That(queue.Consumed, Is.EqualTo(2));
    }

    [Test]
    public void ReservationLimitStopsAdmissionUntilPublicationIsReleased()
    {
        var files = new FileInfo?[] { File(0, 0), File(1, 1), File(2, 2) };
        long oneReservation = FirstBuildImageQueue<byte[]>.RowOverheadBytes + 1 + 4;
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            workers: 2, jobs: 8, maxReservedBytes: oneReservation);
        for (int i = 0; i < files.Length; i++)
        {
            Assert.That(queue.TryTake(i, out var lease), Is.True);
            Assert.That(queue.Scheduled, Is.EqualTo(i + 1));
            Assert.That(queue.HeldBytes, Is.EqualTo(oneReservation));
            Assert.That(queue.MaxReservedBytes, Is.EqualTo(oneReservation));
            lease!.Dispose();
            Assert.That(queue.HeldBytes, Is.Zero);
        }
    }

    [Test]
    public void ExactSourceLeaseBlocksWritesAndReplacementUntilCallerReleasesIt()
    {
        FileInfo file = File(0, 3, 2, 1);
        using var queue = new FirstBuildImageQueue<byte[]>(new[] { file }, _ => 12, (source, _) => source);
        Assert.That(queue.TryTake(0, out var lease), Is.True);
        Assert.That(lease!.Source, Is.EqualTo(new byte[] { 3, 2, 1 }));
        Assert.Throws<IOException>((Action)(() =>
        {
            using var writer = new FileStream(file.FullName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        }));
        // The product target for this slice is Windows; FileShare.Delete is
        // deliberately absent, retaining the same physical source until publish.
        Assert.Throws<IOException>((Action)(() => System.IO.File.Delete(file.FullName)));
        lease.Dispose();
        System.IO.File.WriteAllBytes(file.FullName, new byte[] { 9 });
        Assert.That(System.IO.File.ReadAllBytes(file.FullName), Is.EqualTo(new byte[] { 9 }));
    }

    [Test]
    public void NullMissingOversizedAndRejectedHeadersLeaveNativeFallbackAndAdvance()
    {
        var files = new FileInfo?[]
        {
            null, new(Path.Combine(root, "missing")), File(2, 1, 2, 3),
            File(3, 0), File(4, 1), File(5, 2), File(6, 3)
        };
        using var queue = new FirstBuildImageQueue<byte[]>(files, stream =>
        {
            int marker = stream.ReadByte();
            if (marker == 1) throw new InvalidDataException("Invalid header");
            return marker == 0 ? 0 : marker == 2 ? 65 : 4;
        }, (source, _) => source, maxSourceBytes: 2, maxDecodedBytes: 64);
        for (int i = 0; i < 6; i++) Assert.That(queue.TryTake(i, out _), Is.False);
        Assert.That(queue.TryTake(6, out var lease), Is.True);
        Assert.That(lease!.Source, Is.EqualTo(new byte[] { 3 }), "Header probing must rewind the leased source.");
        lease.Dispose();
        Assert.That(queue.Scheduled, Is.EqualTo(1));
        Assert.That(queue.Failed, Is.EqualTo(2));
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [Test]
    public void WorkerErrorIsDeliveredInOrderWithoutPreventingFollowingDecode()
    {
        var files = new FileInfo?[] { File(0, 0), File(1, 1) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) =>
        {
            if (source[0] == 0) throw new InvalidDataException("Decode rejected");
            return source;
        });
        Assert.That(queue.TryTake(0, out var failed), Is.True);
        Assert.That(failed!.Error, Is.TypeOf<InvalidDataException>());
        Assert.That(failed.Value, Is.Null);
        Assert.That(failed.Source, Is.EqualTo(new byte[] { 0 }));
        Assert.That(failed.SourceComplete, Is.True);
        Assert.Throws<InvalidOperationException>((Action)(() => failed.MarkConsumed()));
        failed.Dispose();
        Assert.That(failed.SourceComplete, Is.False);
        Assert.That(failed.Source, Is.Empty);
        Assert.That(queue.TryTake(1, out var success), Is.True);
        Assert.That(success!.Error, Is.Null);
        success.MarkConsumed(); success.Dispose();
        Assert.That(queue.Decoded, Is.EqualTo(1));
        Assert.That(queue.Consumed, Is.EqualTo(1));
        Assert.That(queue.Failed, Is.EqualTo(1));
    }

    [Test]
    public void EnumeratorAbandonmentCancelsAndJoinsBeforeReleasingAllSources()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2) };
        using var entered = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        int thirdDecoded = 0;
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, token) =>
        {
            if (source[0] == 2) Interlocked.Increment(ref thirdDecoded);
            if (source[0] == 1)
            {
                entered.Set();
                try
                {
                    if (!token.WaitHandle.WaitOne(5000)) throw new InvalidOperationException("Cancellation did not arrive.");
                    token.ThrowIfCancellationRequested();
                }
                finally { exited.Set(); }
            }
            return source;
        }, workers: 1, jobs: 3);
        Assert.That(queue.TryTake(0, out var first), Is.True);
        first!.MarkConsumed(); first.Dispose();
        Assert.That(entered.Wait(5000), Is.True);
        queue.Dispose();
        Assert.That(exited.IsSet, Is.True);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.Decoded, Is.EqualTo(1));
        Assert.That(queue.Consumed, Is.EqualTo(1));
        Assert.That(thirdDecoded, Is.Zero);
        foreach (FileInfo file in files) System.IO.File.WriteAllBytes(file.FullName, new byte[] { 7 });
        Assert.Throws<ObjectDisposedException>((Action)(() => queue.TryTake(1, out _)));
    }

    [Test]
    public void SkippedIndicesReleaseUnpublishedResultsWithoutCountingConsumption()
    {
        var files = new FileInfo?[] { File(0, 0), File(1, 1), File(2, 2), File(3, 3) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source, jobs: 1);
        Assert.That(queue.TryTake(3, out var last), Is.True);
        Assert.That(last!.Source, Is.EqualTo(new byte[] { 3 }));
        last.MarkConsumed(); last.Dispose();
        Assert.That(queue.Scheduled, Is.EqualTo(4));
        Assert.That(queue.Consumed, Is.EqualTo(1));
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [Test]
    public void CallbackBarrierReleasesOlderSourcesAndDefersReadingChangedNextSource()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2), File(3, 3), File(4, 4) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            barriers: new[] { false, false, true, false, false });
        Assert.That(queue.TryTake(0, out var first), Is.True);
        first!.MarkConsumed(); first.Dispose();
        Assert.That(queue.Scheduled, Is.EqualTo(2), "No source beyond the callback boundary may be opened.");
        Assert.That(queue.HeldBytes, Is.GreaterThan(0), "The skipped earlier result remains privately held until the boundary.");
        Assert.That(queue.TryTake(2, out var barrier), Is.False);
        Assert.That(barrier, Is.Null);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        Assert.That(queue.Consumed, Is.EqualTo(1), "Discarding private results must not count as engine consumption.");

        // Represents the ordinary loader's callback: it can change both an
        // earlier source and the next source because no queue lease survives.
        System.IO.File.WriteAllBytes(files[1].FullName, new byte[] { 81 });
        System.IO.File.WriteAllBytes(files[3].FullName, new byte[] { 93 });
        Assert.That(queue.TryTake(3, out var changed), Is.True);
        Assert.That(changed!.Source, Is.EqualTo(new byte[] { 93 }));
        Assert.That(changed.Value, Is.SameAs(changed.Source));
        changed.Dispose();
        Assert.That(queue.TryTake(4, out var last), Is.True);
        last!.Dispose();
    }

    [Test]
    public void FirstLastAndConsecutiveBarriersNeverAdmitTheirOwnSources()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2), File(3, 3) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            barriers: new[] { true, true, false, true });
        for (int index = 0; index < 2; index++)
        {
            Assert.That(queue.TryTake(index, out _), Is.False);
            Assert.That(queue.Scheduled, Is.Zero);
            Assert.That(queue.HeldBytes, Is.Zero);
            System.IO.File.WriteAllBytes(files[2].FullName, new byte[] { (byte)(70 + index) });
        }
        Assert.That(queue.TryTake(2, out var only), Is.True);
        Assert.That(only!.Source, Is.EqualTo(new byte[] { 71 }));
        only.MarkConsumed(); only.Dispose();
        Assert.That(queue.TryTake(3, out _), Is.False);
        Assert.That(queue.Scheduled, Is.EqualTo(1));
        Assert.That(queue.Consumed, Is.EqualTo(1));
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
    }

    [Test]
    public void IndexJumpAcrossBarrierUsesNativeForRequestedItemAndResumesOnNextCall()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2), File(3, 3), File(4, 4) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            barriers: new[] { false, false, true, false, false });
        Assert.That(queue.TryTake(0, out var first), Is.True);
        first!.Dispose();
        Assert.That(queue.TryTake(3, out var skippedAcross), Is.False);
        Assert.That(skippedAcross, Is.Null);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        System.IO.File.WriteAllBytes(files[3].FullName, new byte[] { 83 });
        System.IO.File.WriteAllBytes(files[4].FullName, new byte[] { 94 });
        Assert.That(queue.TryTake(4, out var resumed), Is.True);
        Assert.That(resumed!.Source, Is.EqualTo(new byte[] { 94 }));
        resumed.Dispose();
        Assert.That(queue.Scheduled, Is.EqualTo(3));
    }

    [Test]
    public void BarrierMapMustMatchSourcesAndCannotBeChangedAfterConstruction()
    {
        var files = new[] { File(0, 0), File(1, 1) };
        Assert.Throws<ArgumentException>((Action)(() => new FirstBuildImageQueue<byte[]>(files,
            _ => 4, (source, _) => source, barriers: new[] { true })));
        var barriers = new[] { true, false };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source, barriers: barriers);
        barriers[0] = false;
        Assert.That(queue.TryTake(0, out _), Is.False);
        Assert.That(queue.Scheduled, Is.Zero);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.TryTake(1, out var next), Is.True);
        next!.Dispose();
    }

    [Test]
    public void PerItemRecipesRunOnWorkersAndReserveCopiesAndScratchUntilOrderedRelease()
    {
        var files = new[] { File(0, 0), File(1, 1) };
        int caller = Thread.CurrentThread.ManagedThreadId;
        using var secondEntered = new ManualResetEventSlim();
        long firstReservation = FirstBuildImageQueue<byte[]>.RowOverheadBytes + 1 + 4;
        // A warm recipe may hold its payload, copied pixels and decode scratch
        // at once; its reservation includes all three, beyond source storage.
        long secondReservation = FirstBuildImageQueue<byte[]>.RowOverheadBytes + 1 + 8 + 8 + 8;
        using var queue = new FirstBuildImageQueue<byte[]>(files,
            _ => throw new InvalidOperationException("Legacy estimator must not run."),
            (_, _) => throw new InvalidOperationException("Legacy decoder must not run."),
            maxReservedBytes: firstReservation + secondReservation,
            plan: (index, stream) =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(caller));
                Assert.That(stream.ReadByte(), Is.EqualTo(index));
                return new FirstBuildImageQueue<byte[]>.WorkItem(index == 0 ? 4 : 24, (source, _) =>
                {
                    Assert.That(Thread.CurrentThread.ManagedThreadId, Is.Not.EqualTo(caller));
                    if (index == 0 && !secondEntered.Wait(5000))
                        throw new InvalidOperationException("Second recipe did not enter.");
                    if (index == 1) secondEntered.Set();
                    return new[] { (byte)(source[0] + (index == 0 ? 10 : 20)) };
                });
            });
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(first!.Error, Is.Null);
        Assert.That(first.Value, Is.EqualTo(new byte[] { 10 }));
        Assert.That(SpinWait.SpinUntil(() => queue.ActiveWorkers == 0, 5000), Is.True);
        Assert.That(queue.MaxActiveWorkers, Is.EqualTo(2), "Worker occupancy includes concurrent decoding.");
        Assert.That(queue.HeldBytes, Is.EqualTo(firstReservation + secondReservation),
            "Finished results retain their full reservation until ordered release.");
        Assert.That(queue.MaxReservedBytes, Is.EqualTo(firstReservation + secondReservation));
        first.MarkConsumed();
        first.Dispose();
        Assert.That(queue.HeldBytes, Is.EqualTo(secondReservation));
        Assert.Throws<IOException>((Action)(() => System.IO.File.Delete(files[1].FullName)));
        Assert.That(queue.TryTake(1, out var second), Is.True);
        Assert.That(second!.Error, Is.Null);
        Assert.That(second.Value, Is.EqualTo(new byte[] { 21 }));
        second.MarkConsumed();
        second.Dispose();
        System.IO.File.WriteAllBytes(files[1].FullName, new byte[] { 90 });
        Assert.That(queue.Consumed, Is.EqualTo(2));
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [TestCase("null")]
    [TestCase("missing")]
    [TestCase("source-size")]
    [TestCase("zero")]
    [TestCase("decoded-size")]
    [TestCase("aggregate-size")]
    [TestCase("overflow")]
    [TestCase("exception")]
    public void RefusedRecipeIsABarrierBeforeOrdinaryFallback(string refusal)
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2) };
        if (refusal == "missing") files[1] = new FileInfo(Path.Combine(root, "missing"));
        if (refusal == "source-size") System.IO.File.WriteAllBytes(files[1].FullName, new byte[] { 1, 1, 1 });
        bool futurePlanned = false;
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            maxSourceBytes: 2,
            maxDecodedBytes: refusal == "overflow" ? long.MaxValue : 64,
            maxReservedBytes: FirstBuildImageQueue<byte[]>.RowOverheadBytes + 32,
            plan: (index, _) =>
            {
                if (index == 2) futurePlanned = true;
                if (index == 1)
                {
                    if (refusal == "null") return null;
                    if (refusal == "exception") throw new InvalidDataException("Plan rejected.");
                }
                long required = index != 1 ? 4 : refusal == "zero" ? 0 :
                    refusal == "decoded-size" ? 65 : refusal == "aggregate-size" ? 33 :
                    refusal == "overflow" ? long.MaxValue : 4;
                return new FirstBuildImageQueue<byte[]>.WorkItem(required, (source, _) => source);
            });
        Assert.That(queue.TryTake(0, out var first), Is.True);
        first!.Dispose();
        Assert.That(queue.Scheduled, Is.EqualTo(1));
        Assert.That(queue.TryTake(1, out _), Is.False);
        Assert.That(futurePlanned, Is.False);
        Assert.That(queue.Scheduled, Is.EqualTo(1));
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        // Ordinary fallback may replace a later file after refusal has drained.
        System.IO.File.WriteAllBytes(files[2].FullName, new byte[] { 92 });
        Assert.That(queue.TryTake(2, out var resumed), Is.True);
        Assert.That(resumed!.Source, Is.EqualTo(new byte[] { 92 }));
        resumed.Dispose();
    }

    [Test]
    public void JumpAcrossDiscoveredRecipeBarrierDrainsEarlierResultsAndResumesNextCall()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2), File(3, 3), File(4, 4) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            plan: (index, _) => index == 2 ? null :
                new FirstBuildImageQueue<byte[]>.WorkItem(4, (source, _) => source));
        Assert.That(queue.TryTake(0, out var first), Is.True);
        first!.Dispose();
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        Assert.That(queue.TryTake(3, out _), Is.False);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        System.IO.File.WriteAllBytes(files[1].FullName, new byte[] { 81 });
        System.IO.File.WriteAllBytes(files[4].FullName, new byte[] { 94 });
        Assert.That(queue.TryTake(4, out var last), Is.True);
        Assert.That(last!.Source, Is.EqualTo(new byte[] { 94 }));
        last.Dispose();
    }

    [Test]
    public void RestartIndexDoesNotPlanEarlierFilesOrRevisitEarlierBarriers()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2), File(3, 3) };
        int planned = 0;
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            barriers: new[] { true, true, false, false },
            plan: (index, _) =>
            {
                Assert.That(index, Is.GreaterThanOrEqualTo(2));
                planned++;
                return new FirstBuildImageQueue<byte[]>.WorkItem(4, (source, _) => source);
            }, startIndex: 2);
        Assert.Throws<ArgumentOutOfRangeException>((Action)(() => queue.TryTake(1, out _)));
        Assert.That(queue.TryTake(2, out var first), Is.True);
        Assert.That(first!.Source, Is.EqualTo(new byte[] { 2 }));
        first.Dispose();
        Assert.That(queue.TryTake(3, out var last), Is.True);
        last!.Dispose();
        Assert.That(planned, Is.EqualTo(2));
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        Assert.Throws<ArgumentOutOfRangeException>((Action)(() => new FirstBuildImageQueue<byte[]>(
            files, _ => 4, (source, _) => source, startIndex: -1)));
        Assert.Throws<ArgumentOutOfRangeException>((Action)(() => new FirstBuildImageQueue<byte[]>(
            files, _ => 4, (source, _) => source, startIndex: files.Length + 1)));
    }

    [Test]
    public void RecipeAbandonmentCancelsAndJoinsBeforeReleasingItsSourceAndReservation()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2) };
        using var entered = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        int thirdDecoded = 0;
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            workers: 1, jobs: 3, plan: (index, _) =>
                new FirstBuildImageQueue<byte[]>.WorkItem(24, (source, token) =>
                {
                    if (index == 2) Interlocked.Increment(ref thirdDecoded);
                    if (index == 1)
                    {
                        entered.Set();
                        try
                        {
                            if (!token.WaitHandle.WaitOne(5000))
                                throw new InvalidOperationException("Cancellation did not arrive.");
                            token.ThrowIfCancellationRequested();
                        }
                        finally { exited.Set(); }
                    }
                    return source;
                }));
        Assert.That(queue.TryTake(0, out var first), Is.True);
        first!.Dispose();
        Assert.That(entered.Wait(5000), Is.True);
        queue.Dispose();
        Assert.That(exited.IsSet, Is.True);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(thirdDecoded, Is.Zero);
        foreach (FileInfo file in files) System.IO.File.WriteAllBytes(file.FullName, new byte[] { 70 });
    }

    [Test]
    public void UnopenedWarmRecipeReplacesBeforeWorkerOpenAndRetainsTheActualSourceLease()
    {
        FileInfo file = File(0, 1);
        int caller = Thread.CurrentThread.ManagedThreadId;
        long reservation = FirstBuildImageQueue<byte[]>.RowOverheadBytes + 1 + 24;
        using var queue = new FirstBuildImageQueue<byte[]>(new[] { file },
            _ => throw new InvalidOperationException("Warm source must not use the header estimator."),
            (_, _) => throw new InvalidOperationException("Private recipe expected."),
            maxReservedBytes: reservation, unopenedPlan: _ =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(caller));
                // This would fail if the ordered caller already held Share.Read.
                System.IO.File.Delete(file.FullName);
                System.IO.File.WriteAllBytes(file.FullName, new byte[] { 9 });
                return new FirstBuildImageQueue<byte[]>.WorkItem(24, (source, _) =>
                {
                    Assert.That(Thread.CurrentThread.ManagedThreadId, Is.Not.EqualTo(caller));
                    return source;
                }, expectedSourceBytes: 1);
            });
        Assert.That(queue.TryTake(0, out var lease), Is.True);
        Assert.That(lease!.Error, Is.Null);
        Assert.That(lease.Source, Is.EqualTo(new byte[] { 9 }));
        Assert.That(lease.SourceComplete, Is.True);
        Assert.That(queue.HeldBytes, Is.EqualTo(reservation));
        Assert.That(queue.MaxReservedBytes, Is.EqualTo(reservation));
        Assert.Throws<IOException>((Action)(() => System.IO.File.Delete(file.FullName)));
        lease.MarkConsumed(); lease.Dispose();
        System.IO.File.WriteAllBytes(file.FullName, new byte[] { 8 });
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [TestCase(0)]
    [TestCase(2)]
    public void ChangedWarmSourceLengthFailsOnWorkerBeforeAllocatingSource(int actualBytes)
    {
        FileInfo file = File(0, 1);
        bool decoded = false;
        using var queue = new FirstBuildImageQueue<byte[]>(new[] { file }, _ => 4, (source, _) => source,
            maxSourceBytes: 1, unopenedPlan: _ =>
            {
                System.IO.File.WriteAllBytes(file.FullName, new byte[actualBytes]);
                return new FirstBuildImageQueue<byte[]>.WorkItem(4,
                    (source, _) => { decoded = true; return source; }, expectedSourceBytes: 1);
            });
        Assert.That(queue.TryTake(0, out var lease), Is.True, "Worker failures are delivered in order.");
        Assert.That(lease!.Error, Is.TypeOf<InvalidDataException>());
        Assert.That(lease.Source, Is.Empty);
        Assert.That(lease.SourceComplete, Is.False);
        Assert.That(decoded, Is.False);
        Assert.That(queue.Failed, Is.EqualTo(1));
        queue.DrainAfter(0);
        Assert.That(queue.HeldBytes, Is.Zero);
        System.IO.File.WriteAllBytes(file.FullName, new byte[] { 7 });
        lease.Dispose();
    }

    [TestCase(null)]
    [TestCase(-1L)]
    [TestCase(0L)]
    [TestCase(3L)]
    public void InvalidUnopenedSourceHintRefusesAdmissionWithoutOpening(long? hint)
    {
        FileInfo file = File(0, 1);
        using var writer = new FileStream(file.FullName, FileMode.Open, FileAccess.Write, FileShare.None);
        using var queue = new FirstBuildImageQueue<byte[]>(new[] { file }, _ => 4, (source, _) => source,
            maxSourceBytes: 2, unopenedPlan: _ => new FirstBuildImageQueue<byte[]>.WorkItem(4, (source, _) => source, hint));
        Assert.That(queue.TryTake(0, out _), Is.False);
        Assert.That(queue.Scheduled, Is.Zero);
        Assert.That(queue.Failed, Is.Zero, "Invalid hints are refused before source IO.");
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.WorkerOpenTicks, Is.Zero);
    }

    [Test]
    public void NullUnopenedPlanPreservesTheOriginalHeaderEstimatorAndSourceRewind()
    {
        FileInfo file = File(0, 7);
        int caller = Thread.CurrentThread.ManagedThreadId;
        bool estimated = false;
        using var queue = new FirstBuildImageQueue<byte[]>(new[] { file }, stream =>
        {
            Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(caller));
            Assert.That(stream.ReadByte(), Is.EqualTo(7));
            estimated = true;
            return 4;
        }, (source, _) => source, unopenedPlan: _ => null);
        Assert.That(queue.TryTake(0, out var lease), Is.True);
        Assert.That(estimated, Is.True);
        Assert.That(lease!.Error, Is.Null);
        Assert.That(lease.Source, Is.EqualTo(new byte[] { 7 }));
        Assert.That(queue.WorkerOpenTicks, Is.Zero, "The original path keeps its caller-opened lease.");
        lease.Dispose();
    }

    [Test]
    public void MissingUnopenedSourceProducesAnOrderedErrorThatCanDrainBeforeFallback()
    {
        var files = new[] { new FileInfo(Path.Combine(root, "missing")), File(1, 1) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            unopenedPlan: _ => new FirstBuildImageQueue<byte[]>.WorkItem(4, (source, _) => source, 1));
        Assert.That(queue.TryTake(0, out var lease), Is.True);
        Assert.That(lease!.Error, Is.InstanceOf<IOException>());
        Assert.That(lease.Source, Is.Empty);
        Assert.That(lease.SourceComplete, Is.False);
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        queue.DrainAfter(0);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        System.IO.File.WriteAllBytes(files[1].FullName, new byte[] { 9 });
        Assert.That(queue.TryTake(1, out var next), Is.True);
        Assert.That(next!.Source, Is.EqualTo(new byte[] { 9 }));
        next.Dispose(); lease.Dispose();
    }

    [Test]
    public void SuspensionClosesEverySourceAndRetainsSuccessfulPublicationUntilCallerRelease()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2) };
        long reservation = FirstBuildImageQueue<byte[]>.RowOverheadBytes + 1 + 4;
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            maxReservedBytes: 3 * reservation);
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded == 3, 5000), Is.True);
        queue.SuspendAfter(0);
        Assert.That(queue.IsSuspended, Is.True);
        Assert.That(queue.OpenStreams, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.HeldBytes, Is.EqualTo(3 * reservation));
        Assert.That(first!.Value, Is.EqualTo(new byte[] { 0 }));
        Assert.That(first.SourceComplete, Is.True);
        first.MarkConsumed(); // PSD publication can receipt after its capture callback.
        queue.SuspendAfter(0);
        first.MarkConsumed();
        Assert.That(queue.Consumed, Is.EqualTo(1));
        Assert.That(queue.Retained, Is.EqualTo(3), "Repeated suspension must not repeat work.");
        foreach (FileInfo file in files)
            using (new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        first.Dispose();
        Assert.That(first.WasConsumed, Is.True, "Unlocking and disposing cannot erase an upload receipt.");
        Assert.That(queue.TryTake(1, out var second), Is.True);
        Assert.That(second!.WasRevalidated, Is.True);
        Assert.That(queue.Scheduled, Is.EqualTo(3));
        Assert.That(queue.Revalidated, Is.EqualTo(1));
        Assert.Throws<IOException>((Action)(() => System.IO.File.Delete(files[1].FullName)));
        second.MarkConsumed();
        queue.SuspendAfter(1);
        Assert.That(second.Value, Is.EqualTo(new byte[] { 1 }));
        second.Dispose();
        Assert.That(queue.TryTake(2, out var third), Is.True);
        third!.MarkConsumed(); third.Dispose();
        Assert.That(queue.Decoded, Is.EqualTo(3));
        Assert.That(queue.Consumed, Is.EqualTo(3));
        Assert.That(queue.MaxReservedBytes, Is.EqualTo(3 * reservation));
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [TestCase("same-size")]
    [TestCase("changed-size")]
    [TestCase("missing")]
    [TestCase("unreadable")]
    public void RetainedSourceRequiresFullCurrentContentsAndRejectsOnlyChangedItem(string mutation)
    {
        var bytes = new byte[140000];
        bytes[bytes.Length - 1] = 11;
        var files = new[] { File(0, 0), File(1, bytes), File(2, 2) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source);
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded == 3, 5000), Is.True);
        DateTime originalTime = System.IO.File.GetLastWriteTimeUtc(files[1].FullName);
        first!.Dispose();
        queue.SuspendAfter(0);
        FileStream? writer = null;
        try
        {
            if (mutation == "missing") System.IO.File.Delete(files[1].FullName);
            else if (mutation == "unreadable")
                writer = new FileStream(files[1].FullName, FileMode.Open, FileAccess.Write, FileShare.None);
            else
            {
                bytes[bytes.Length - 1] = 91; // beyond two scratch chunks; metadata stays identical.
                System.IO.File.WriteAllBytes(files[1].FullName, mutation == "same-size" ? bytes : new byte[] { 91 });
                System.IO.File.SetLastWriteTimeUtc(files[1].FullName, originalTime);
            }
            Assert.That(queue.TryTake(1, out var rejected), Is.False);
            Assert.That(rejected, Is.Null);
            Assert.That(queue.RejectedRetained, Is.EqualTo(1));
            Assert.That(queue.ActiveWorkers, Is.Zero);
            Assert.That(queue.OpenStreams, Is.Zero);
            Assert.That(queue.IsSuspended, Is.True);
        }
        finally { writer?.Dispose(); }
        // Ordinary fallback can now read changed current bytes. Following
        // unchanged work remains available without replaying its decoder.
        Assert.That(queue.TryTake(2, out var unchanged), Is.True);
        Assert.That(unchanged!.Value, Is.EqualTo(new byte[] { 2 }));
        Assert.That(unchanged.WasRevalidated, Is.True);
        Assert.That(queue.Scheduled, Is.EqualTo(3));
        Assert.That(queue.Decoded, Is.EqualTo(3));
        unchanged.Dispose();
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [TestCase(7, -1)]
    [TestCase(7, 6)]
    [TestCase(8, 7)]
    [TestCase(9, 8)]
    [TestCase(65536, -1)]
    [TestCase(65537, 65536)]
    [TestCase(131079, -1)]
    public void RetainedComparisonChecksWordAndChunkTails(int length, int changedOffset)
    {
        var source = new byte[length];
        for (int i = 0; i < source.Length; i++) source[i] = (byte)(i * 17 + 3);
        var files = new[] { File(0, 0), File(1, source) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (bytes, _) => bytes);
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded == 2, 5000), Is.True);
        first!.Dispose();
        queue.SuspendAfter(0);
        if (changedOffset >= 0)
        {
            source[changedOffset] ^= 0x80;
            System.IO.File.WriteAllBytes(files[1].FullName, source);
        }
        Assert.That(queue.TryTake(1, out var retained), Is.EqualTo(changedOffset < 0));
        if (retained != null)
        {
            Assert.That(retained.Source, Is.EqualTo(source));
            Assert.That(retained.WasRevalidated, Is.True);
            retained.Dispose();
        }
        Assert.That(queue.Revalidated, Is.EqualTo(changedOffset < 0 ? 1 : 0));
        Assert.That(queue.RejectedRetained, Is.EqualTo(changedOffset < 0 ? 0 : 1));
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.OpenStreams, Is.Zero);
    }

    [Test]
    public void MixedWarmColdAndFailedResultsRetainOnlyCompletedColdSuccessAndReplanHoles()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2), File(3, 3) };
        int[] decodes = new int[4];
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            plan: (index, _) => new FirstBuildImageQueue<byte[]>.WorkItem(4, (source, _) =>
            {
                Interlocked.Increment(ref decodes[index]);
                if (index == 2) throw new InvalidDataException("Failed decode is not a retained success.");
                return source;
            }, retainAcrossCallbacks: index != 1));
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded == 3 && queue.Failed == 1 && queue.ActiveWorkers == 0, 5000), Is.True);
        first!.Dispose();
        queue.SuspendAfter(0);
        Assert.That(queue.OpenStreams, Is.Zero);
        Assert.That(queue.Retained, Is.EqualTo(1), "Only the future cold success survives mixed planning.");
        Assert.That(queue.TryTake(1, out var warm), Is.True);
        Assert.That(warm!.WasRevalidated, Is.False);
        warm.Dispose();
        Assert.That(queue.TryTake(2, out var failed), Is.True);
        Assert.That(failed!.Error, Is.TypeOf<InvalidDataException>());
        Assert.Throws<InvalidOperationException>((Action)(() => failed.MarkConsumed()));
        failed.Dispose();
        queue.SuspendAfter(2);
        Assert.That(queue.TryTake(3, out var cold), Is.True);
        Assert.That(cold!.WasRevalidated, Is.True);
        cold.MarkConsumed(); cold.Dispose();
        Assert.That(decodes, Is.EqualTo(new[] { 1, 2, 2, 1 }));
        Assert.That(queue.Consumed, Is.EqualTo(1));
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [Test]
    public void ReplannedLargerHoleEvictsFutureRetentionInsteadOfExceedingBudgetOrStalling()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2) };
        long budget = 3L * (FirstBuildImageQueue<byte[]>.RowOverheadBytes + 5);
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            maxReservedBytes: budget, plan: (index, _) =>
                new FirstBuildImageQueue<byte[]>.WorkItem(4, (source, _) => source,
                    retainAcrossCallbacks: index != 1));
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded == 3, 5000), Is.True);
        first!.Dispose();
        queue.SuspendAfter(0);
        System.IO.File.WriteAllBytes(files[1].FullName, new byte[FirstBuildImageQueue<byte[]>.RowOverheadBytes + 100]);
        Assert.That(queue.TryTake(1, out var larger), Is.True);
        Assert.That(larger!.Source.Length, Is.EqualTo(FirstBuildImageQueue<byte[]>.RowOverheadBytes + 100));
        Assert.That(queue.HeldBytes, Is.LessThanOrEqualTo(budget));
        larger.Dispose();
        Assert.That(queue.TryTake(2, out var following), Is.True);
        Assert.That(following!.WasRevalidated, Is.False, "The future result yielded its reservation to ordered work.");
        following.Dispose();
        Assert.That(queue.MaxReservedBytes, Is.LessThanOrEqualTo(budget));
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [Test]
    public void SuspensionStopsPendingDequeueAndWaitsForActiveWorkerBeforeReturning()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2) };
        using var entered = new ManualResetEventSlim();
        using var allowCompletion = new ManualResetEventSlim();
        int thirdDecodes = 0;
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) =>
        {
            if (source[0] == 1)
            {
                entered.Set();
                if (!allowCompletion.Wait(5000)) throw new InvalidOperationException("Suspension did not begin.");
            }
            if (source[0] == 2) Interlocked.Increment(ref thirdDecodes);
            return source;
        }, workers: 1, jobs: 3);
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(entered.Wait(5000), Is.True);
        first!.Dispose();
        var releaser = new Thread(() =>
        {
            SpinWait.SpinUntil(() => queue.IsSuspended, 5000);
            allowCompletion.Set();
        });
        releaser.Start();
        try { queue.SuspendAfter(0); }
        finally { allowCompletion.Set(); releaser.Join(); }
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.OpenStreams, Is.Zero);
        Assert.That(thirdDecodes, Is.Zero, "Pending work must not dequeue during quiescence.");
        Assert.That(queue.Decoded, Is.EqualTo(2));
        Assert.That(queue.TryTake(1, out var second), Is.True);
        second!.Dispose();
        Assert.That(queue.TryTake(2, out var third), Is.True);
        third!.Dispose();
        Assert.That(thirdDecodes, Is.EqualTo(1));
        Assert.That(queue.Scheduled, Is.EqualTo(4), "Dropped pending admission is replanned once, not decoded twice.");
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FullRetirementAfterSuspensionReleasesOuterBudgetAndEveryRetainedResult(bool dispose)
    {
        var files = new[] { File(0, 0), File(1, 1) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source);
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded == 2, 5000), Is.True);
        queue.SuspendAfter(0);
        Assert.That(queue.HeldBytes, Is.GreaterThan(0));
        if (dispose) queue.Dispose(); else queue.DrainAfter(0);
        Assert.That(first!.Released, Is.True);
        Assert.That(first.Value, Is.Null);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.OpenStreams, Is.Zero);
    }

    [Test]
    public void RetainedBatchPublishesWithoutRefillAndMissingCurrentStartsNextBoundedBatch()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2), File(3, 3), File(4, 4) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            workers: 2, jobs: 3);
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded == 3, 5000), Is.True);
        first!.MarkConsumed(); first.Dispose();
        queue.SuspendAfter(0);
        for (int index = 1; index <= 2; index++)
        {
            Assert.That(queue.TryTake(index, out var retained), Is.True);
            Assert.That(retained!.WasRevalidated, Is.True);
            Assert.That(queue.Scheduled, Is.EqualTo(3), "A retained take must not admit a replacement job.");
            Assert.That(queue.Decoded, Is.EqualTo(3));
            Assert.That(queue.ActiveWorkers, Is.Zero);
            Assert.That(queue.IsSuspended, Is.True);
            Assert.That(queue.OpenStreams, Is.EqualTo(1), "Only the fully revalidated current source is reopened.");
            retained.MarkConsumed();
            queue.SuspendAfter(index);
            queue.SuspendAfter(index);
            Assert.That(queue.OpenStreams, Is.Zero, "Suspension must close a reopened lease even while workers stayed paused.");
            Assert.That(queue.Scheduled, Is.EqualTo(3));
            Assert.That(queue.ActiveWorkers, Is.Zero);
            using (new FileStream(files[index].FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            retained.Dispose();
        }
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.TryTake(3, out var nextBatch), Is.True);
        Assert.That(nextBatch!.WasRevalidated, Is.False);
        Assert.That(queue.IsSuspended, Is.False);
        Assert.That(queue.Scheduled, Is.EqualTo(5), "A missing current item admits the next bounded horizon.");
        nextBatch.MarkConsumed(); nextBatch.Dispose();
        Assert.That(queue.TryTake(4, out var last), Is.True);
        last!.MarkConsumed(); last.Dispose();
        Assert.That(queue.Decoded, Is.EqualTo(5));
        Assert.That(queue.Consumed, Is.EqualTo(5));
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [Test]
    public void RetiringSpeculationWithoutPublicationReleasesAllRetainedBuffers()
    {
        var files = new[] { File(0, 0), File(1, 1) };
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source);
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded == 2, 5000), Is.True);
        first!.Dispose();
        queue.SuspendAfter(0);
        queue.RetireSpeculationAfter(0);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.OpenStreams, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.IsSuspended, Is.True);
    }

    [TestCase("cold", false)]
    [TestCase("cold", true)]
    [TestCase("warm", false)]
    [TestCase("failed", false)]
    public void NestedRetirementPreservesOnlyCurrentPublicationAndItsChargedReservation(string kind, bool suspended)
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2) };
        long reservation = FirstBuildImageQueue<byte[]>.RowOverheadBytes + 5;
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            plan: (index, _) => new FirstBuildImageQueue<byte[]>.WorkItem(4, (source, _) =>
            {
                if (index == 0 && kind == "failed") throw new InvalidDataException("Current fallback still owns source.");
                return source;
            }, retainAcrossCallbacks: kind != "warm"));
        Assert.That(queue.TryTake(0, out var current), Is.True);
        Assert.That(SpinWait.SpinUntil(() => queue.Decoded + queue.Failed == 3 && queue.ActiveWorkers == 0, 5000), Is.True);
        var source = current!.Source;
        var pixels = current.Value;
        if (kind != "failed") current.MarkConsumed();
        if (suspended) queue.SuspendAfter(0);
        queue.RetireSpeculationAfter(0);
        queue.RetireSpeculationAfter(0);
        Assert.That(queue.HeldBytes, Is.EqualTo(reservation));
        Assert.That(queue.OpenStreams, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.IsSuspended, Is.True);
        Assert.That(current.Released, Is.False);
        Assert.That(current.Source, Is.SameAs(source));
        Assert.That(current.Value, Is.SameAs(pixels));
        Assert.That(current.SourceComplete, Is.True);
        Assert.That(queue.Consumed, Is.EqualTo(kind == "failed" ? 0 : 1));
        foreach (FileInfo file in files)
            using (new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        current.Dispose();
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.Consumed, Is.EqualTo(kind == "failed" ? 0 : 1));
        // Returning to the outer ordered caller replans the future image; no
        // old speculative result survived nested entry or got double charged.
        Assert.That(queue.TryTake(1, out var next), Is.True);
        Assert.That(next!.WasRevalidated, Is.False);
        next.Dispose();
        queue.DrainAfter(1);
        Assert.That(queue.HeldBytes, Is.Zero);
    }

    [Test]
    public void DisposalCancelsActiveUnopenedRecipeAndReleasesPendingJobsWithNoStreams()
    {
        var files = new[] { File(0, 0), File(1, 1), File(2, 2) };
        using var entered = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        // The third job can be admitted without opening this exclusively held
        // source. One blocked worker guarantees it remains unopened on dispose.
        using var writer = new FileStream(files[2].FullName, FileMode.Open, FileAccess.Write, FileShare.None);
        using var queue = new FirstBuildImageQueue<byte[]>(files, _ => 4, (source, _) => source,
            workers: 1, jobs: 3, unopenedPlan: index => new FirstBuildImageQueue<byte[]>.WorkItem(24, (source, token) =>
            {
                if (index == 1)
                {
                    entered.Set();
                    try
                    {
                        if (!token.WaitHandle.WaitOne(5000)) throw new InvalidOperationException("Cancellation did not arrive.");
                        token.ThrowIfCancellationRequested();
                    }
                    finally { exited.Set(); }
                }
                return source;
            }, expectedSourceBytes: 1));
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(entered.Wait(5000), Is.True);
        Assert.That(queue.Scheduled, Is.EqualTo(3));
        queue.Dispose();
        Assert.That(exited.IsSet, Is.True);
        Assert.That(first!.Released, Is.True);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.Decoded, Is.EqualTo(1));
        Assert.That(queue.Failed, Is.Zero);
        writer.Dispose();
        foreach (FileInfo file in files) System.IO.File.WriteAllBytes(file.FullName, new byte[] { 7 });
    }
}
