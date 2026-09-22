// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class GroupedTextureBlocksTests
{
    private string root = null!;
    private string Category => Path.Combine(root, "WakeUp", "PreparedTextures", "v1");
    private string Groups => Path.Combine(Category, "groups");
    private static string Key(char value) => new(value, 64);
    private static CacheLaunchPolicy Policy(string action)
    {
        CacheLaunchPolicy? selected = null;
        return CacheLaunchPolicy.Latch(ref selected, action, () => { });
    }
    private OwnedCacheStore Open(out GroupedTextureBlocks blocks, long limit = 256 * 1024 * 1024, string action = "normal", SharedCacheBudget? shared = null, bool batched = false, bool retainReadHandles = false)
    {
        var companion = new GroupedTextureBlocks(Groups, batched, retainReadHandles);
        blocks = companion;
        return new OwnedCacheStore(Category, GroupedTextureBlocks.MaximumEntry, limit, Policy(action),
            () => companion.StoredBytes, companion.Clear, companion.Initialize, shared);
    }
    private static byte[] Payload(int size, int seed = 1)
    {
        byte[] result = new byte[size]; new Random(seed).NextBytes(result); return result;
    }
    private byte[]? Read(OwnedCacheStore owner, GroupedTextureBlocks blocks, char key) => owner.ReadExternal(() => blocks.Read(Key(key)));
    private long ActualBytes => Directory.GetFiles(Category, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length);

    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "wake-up-group-test-" + Guid.NewGuid().ToString("N"));
    [TearDown]
    public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [TestCase(false)]
    [TestCase(true)]
    public void ConcurrentSnapshotsUseIndependentPositionsWithinOneGroup(bool retainWorkerHandles)
    {
        using var owner = Open(out var blocks, retainReadHandles: true);
        using var lifetime = blocks;
        byte[] first = Payload(GroupedTextureBlocks.BlockBytes + 3000, 11);
        byte[] second = new byte[GroupedTextureBlocks.BlockBytes + 7000]; second[123] = 12;
        Assert.That(blocks.Publish(owner, Key('A'), first, ownerKey: Key('E')), Is.True);
        Assert.That(blocks.Publish(owner, Key('B'), second, compress: true, ownerKey: Key('F')), Is.True);
        Assert.That(Directory.GetFiles(Groups, "*.group"), Has.Length.EqualTo(1));
        var a = owner.ReadExternal(() => blocks.PlanNative(Key('E')))!;
        var b = owner.ReadExternal(() => blocks.PlanNative(Key('F')))!;
        using var start = new Barrier(2);
        Task ReadMany(GroupedTextureBlocks.ReadSnapshot plan, byte[] expected) => Task.Run(() =>
        {
            using var context = retainWorkerHandles ? new GroupedTextureBlocks.ReadContext() : null;
            Assert.That(start.SignalAndWait(TimeSpan.FromSeconds(5)), Is.True);
            for (int i = 0; i < 8; i++)
                Assert.That(plan.Read(CancellationToken.None, () => { }, context), Is.EqualTo(expected));
            if (context != null) Assert.That(context.OpenHandleCount, Is.EqualTo(1));
        });
        Task.WaitAll(ReadMany(a, first), ReadMany(b, second));
        Assert.That(blocks.OpenReadHandleCount, Is.Zero, "Worker streams do not borrow shared-position serial handles.");
        foreach (string path in Directory.GetFiles(Groups, "*.group"))
            using (var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)) { }
    }

    [Test]
    public void WorkerReadContextClearReleasesReplacementAndRevalidatesReopenedData()
    {
        using var owner = Open(out var blocks);
        using var lifetime = blocks;
        using var context = new GroupedTextureBlocks.ReadContext();
        byte[] first = Payload(600);
        Assert.That(blocks.Publish(owner, Key('A'), first, ownerKey: Key('E')), Is.True);
        var snapshot = owner.ReadExternal(() => blocks.PlanNative(Key('E')))!;
        Assert.That(snapshot.Read(CancellationToken.None, () => { }, context), Is.EqualTo(first));
        string path = Directory.GetFiles(Groups, "*.group").Single();
        Assert.Throws<IOException>((Action)(() => { using var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read); }));
        context.Clear();
        Assert.That(context.OpenHandleCount, Is.Zero);
        byte[] original = File.ReadAllBytes(path);
        byte[] corrupt = (byte[])original.Clone(); corrupt[corrupt.Length - 1] ^= 1;
        File.Move(path, path + ".old"); File.WriteAllBytes(path, corrupt);
        var failure = Assert.Throws<InvalidDataException>((Action)(() => snapshot.Read(CancellationToken.None, () => { }, context)));
        Assert.That(failure!.Message, Is.EqualTo("group-block-digest"));
        context.Clear();
        File.Delete(path); File.Move(path + ".old", path);
        byte[] replacement = Payload(700, 19);
        Assert.That(blocks.Publish(owner, Key('A'), replacement, ownerKey: Key('E')), Is.True);
        var replaced = owner.ReadExternal(() => blocks.PlanNative(Key('E')))!;
        Assert.That(replaced.Read(CancellationToken.None, () => { }, context), Is.EqualTo(replacement));
        context.Dispose();
        Assert.That(context.OpenHandleCount, Is.Zero);
        Assert.Throws<ObjectDisposedException>((Action)(() => replaced.Read(CancellationToken.None, () => { }, context)));
    }

    [Test]
    public void WorkerReadContextEvictsLeastRecentlyUsedHandleAtItsFixedLimit()
    {
        Directory.CreateDirectory(Groups);
        string[] paths = Enumerable.Range(0, GroupedTextureBlocks.ReadContext.MaximumHandles + 1)
            .Select(_ => Path.Combine(Groups, Guid.NewGuid().ToString("N") + ".group")).ToArray();
        foreach (string path in paths) File.WriteAllBytes(path, new byte[20]);
        using var context = new GroupedTextureBlocks.ReadContext();
        for (int i = 0; i < GroupedTextureBlocks.ReadContext.MaximumHandles; i++) context.Acquire(paths[i]);
        var first = context.Acquire(paths[0]); // Keep the first; the second is now oldest.
        context.Acquire(paths[paths.Length - 1]);
        Assert.That(context.OpenHandleCount, Is.EqualTo(GroupedTextureBlocks.ReadContext.MaximumHandles));
        Assert.That(context.Acquire(paths[0]), Is.SameAs(first));
        using (var write = new FileStream(paths[1], FileMode.Open, FileAccess.Write, FileShare.None)) { }
        Assert.Throws<IOException>((Action)(() => { using var write = new FileStream(paths[0], FileMode.Open, FileAccess.Write, FileShare.None); }));
        context.Clear();
        foreach (string path in paths)
            using (var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)) { }
        Assert.That(context.Acquire(paths[0]), Is.Not.SameAs(first));
        Assert.That(context.OpenHandleCount, Is.EqualTo(1));
    }

    [Test]
    public void StartupReadLeaseProtectsGroupUntilWriteAdmissionAndReopensAppendedData()
    {
        using var owner = Open(out var blocks, retainReadHandles: true);
        using var lifetime = blocks;
        Assert.That(blocks.Publish(owner, Key('A'), Payload(600), compress: true), Is.True);
        string path = Directory.GetFiles(Groups, "*.group").Single();
        Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(600)));
        Assert.Throws<IOException>((Action)(() => { using var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read); }));
        Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(600)));
        Assert.That(blocks.CanPublish(owner, Key('B'), 700), Is.True);
        using (var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
        Assert.That(blocks.Publish(owner, Key('B'), Payload(700)), Is.True);
        Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(600)));
        Assert.That(Read(owner, blocks, 'B'), Is.EqualTo(Payload(700)));
        Assert.That(blocks.PublishAtomic(owner, new[] { (Key('C'), Payload(800), Key('C')) }, true, CancellationToken.None), Is.True);
        Assert.That(Read(owner, blocks, 'C'), Is.EqualTo(Payload(800)));
        Assert.That(blocks.Invalidate(owner, Key('C')), Is.True);
        Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(600)));
        blocks.Complete(owner);
        Assert.That(blocks.OpenReadHandleCount, Is.Zero);
        using (var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
    }

    [Test]
    public void StartupReadHandlesAreBoundedAndEvictionReleasesOldGroup()
    {
        using var owner = Open(out var blocks, retainReadHandles: true);
        using var lifetime = blocks;
        byte[] bytes = Payload(9 * 1024 * 1024);
        string first = "";
        // Entries can share a 16 MiB group; write enough bytes to exceed eight
        // distinct groups without depending on the writer's packing decisions.
        int count = (GroupedTextureBlocks.MaximumReadHandles + 1) * 2;
        for (int i = 0; i < count; i++)
        {
            Assert.That(blocks.Publish(owner, i.ToString("X64"), bytes), Is.True);
            if (i == 0) first = Directory.GetFiles(Groups, "*.group").Single();
        }
        for (int i = 0; i < count; i++)
        {
            Assert.That(owner.ReadExternal(() => blocks.Read(i.ToString("X64")))?.Length, Is.EqualTo(bytes.Length));
            Assert.That(blocks.OpenReadHandleCount, Is.LessThanOrEqualTo(GroupedTextureBlocks.MaximumReadHandles));
        }
        Assert.That(blocks.OpenReadHandleCount, Is.EqualTo(GroupedTextureBlocks.MaximumReadHandles));
        using (var write = new FileStream(first, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
        blocks.Dispose();
        foreach (string path in Directory.GetFiles(Groups, "*.group"))
            using (var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
    }

    [Test]
    public void StartupReadRevalidatesCorruptionAfterLeaseReleaseAndClearClosesHandles()
    {
        using var owner = Open(out var blocks, retainReadHandles: true);
        using var lifetime = blocks;
        Assert.That(blocks.Publish(owner, Key('A'), Payload(600)), Is.True);
        Assert.That(blocks.Publish(owner, Key('B'), Payload(700)), Is.True);
        Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(600)));
        Assert.That(blocks.CanPublish(owner, Key('C'), 800), Is.True);
        string path = Directory.GetFiles(Groups, "*.group").Single();
        using (var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
        { write.Position = 113; write.WriteByte(0xFF); }
        Assert.That(Read(owner, blocks, 'A'), Is.Null);
        Assert.That(blocks.OpenReadHandleCount, Is.Zero);
        Assert.That(Read(owner, blocks, 'B'), Is.EqualTo(Payload(700)));
        blocks.Clear();
        Assert.That(blocks.OpenReadHandleCount, Is.Zero);
        Assert.That(Directory.GetFiles(Groups, "*.group"), Is.Empty);
    }

    [Test]
    public void AutomaticBatchIsReadableThenDurableAtNormalCompletion()
    {
        using (var owner = Open(out var blocks, batched: true))
        {
            Assert.That(blocks.Publish(owner, Key('A'), Payload(900)), Is.True);
            Assert.That(blocks.LastReason, Is.EqualTo("group-staged"));
            Assert.That(blocks.Publish(owner, Key('B'), Payload(700)), Is.True);
            Assert.That(File.Exists(Path.Combine(Groups, "index")), Is.False);
            Assert.That(Directory.GetFiles(Groups, "*.delta"), Is.Empty);
            Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(900)));
            Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
            blocks.Complete(owner);
        }
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(Payload(900)));
        Assert.That(Read(reopened, restored, 'B'), Is.EqualTo(Payload(700)));
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void AutomaticBatchThresholdCommits128AndLeavesNextEntryUncommitted()
    {
        using (var owner = Open(out var blocks, batched: true))
        {
            for (int i = 0; i < 128; i++)
                Assert.That(blocks.Publish(owner, i.ToString("X64"), Payload(30, i)), Is.True);
            Assert.That(File.Exists(Path.Combine(Groups, "index")), Is.True);
            Assert.That(Directory.GetFiles(Groups, "*.delta"), Is.Empty);
            Assert.That(blocks.Publish(owner, 128.ToString("X64"), Payload(30, 128)), Is.True);
            Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
            // Dispose releases ownership without a normal startup completion.
        }
        using var reopened = Open(out var restored);
        for (int i = 0; i < 128; i++)
            Assert.That(reopened.ReadExternal(() => restored.Read(i.ToString("X64"))), Is.EqualTo(Payload(30, i)));
        Assert.That(reopened.ReadExternal(() => restored.Read(128.ToString("X64"))), Is.Null);
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void InterruptedBatchPreservesPriorOwnerEntryAndNeighbour()
    {
        using (var seed = Open(out var initial))
        {
            Assert.That(initial.Publish(seed, Key('A'), Payload(600), ownerKey: Key('D')), Is.True);
            Assert.That(initial.Publish(seed, Key('B'), Payload(700)), Is.True);
        }
        long originalBytes = ActualBytes;
        using (var owner = Open(out var blocks, batched: true))
        {
            Assert.That(blocks.Publish(owner, Key('C'), Payload(800), ownerKey: Key('D')), Is.True);
            Assert.That(Read(owner, blocks, 'A'), Is.Null);
            Assert.That(Read(owner, blocks, 'C'), Is.EqualTo(Payload(800)));
        }
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(Payload(600)));
        Assert.That(Read(reopened, restored, 'B'), Is.EqualTo(Payload(700)));
        Assert.That(Read(reopened, restored, 'C'), Is.Null);
        Assert.That(reopened.StoredBytes, Is.EqualTo(originalBytes));
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void FailedBatchCheckpointRollsBackAndStopsFurtherWrites()
    {
        using (var seed = Open(out var initial))
            Assert.That(initial.Publish(seed, Key('A'), Payload(600), ownerKey: Key('D')), Is.True);
        long originalBytes = ActualBytes;
        using (var owner = Open(out var blocks, batched: true))
        {
            Assert.That(blocks.Publish(owner, Key('B'), Payload(700), ownerKey: Key('D')), Is.True);
            using (var held = new FileStream(Path.Combine(Groups, "index"), FileMode.Open, FileAccess.Read, FileShare.Read))
                blocks.Complete(owner);
            Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(600)));
            Assert.That(Read(owner, blocks, 'B'), Is.Null);
            Assert.That(blocks.Publish(owner, Key('C'), Payload(800)), Is.False);
            Assert.That(blocks.LastReason, Is.EqualTo("group-batch-stopped"));
            Assert.That(owner.StoredBytes, Is.EqualTo(originalBytes));
            Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
        }
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(Payload(600)));
        Assert.That(Read(reopened, restored, 'B'), Is.Null);
    }

    [Test]
    public void BatchByteLimitCommitsBeforeAnotherEntryWouldExceed64MiB()
    {
        byte[] payload = new byte[33 * 1024 * 1024];
        using (var owner = Open(out var blocks, batched: true))
        {
            Assert.That(blocks.Publish(owner, Key('A'), payload), Is.True);
            Assert.That(File.Exists(Path.Combine(Groups, "index")), Is.False);
            Assert.That(blocks.Publish(owner, Key('B'), payload), Is.True);
            Assert.That(File.Exists(Path.Combine(Groups, "index")), Is.True);
            Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
        }
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(payload));
        Assert.That(Read(reopened, restored, 'B'), Is.Null);
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void OversizedBatchEntryIsImmediatelyDurableAndQuotaCountsStagedBytes()
    {
        using (var owner = Open(out var blocks, limit: 3000, batched: true))
        {
            Assert.That(blocks.Publish(owner, Key('A'), Payload(1024)), Is.True);
            Assert.That(blocks.Publish(owner, Key('B'), Payload(1800)), Is.False);
            Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
            Assert.That(owner.StoredBytes, Is.LessThanOrEqualTo(3000));
            blocks.Complete(owner);
        }
        byte[] large = new byte[64 * 1024 * 1024]; // Block headers take this beyond the batch limit.
        using (var owner = Open(out var blocks, batched: true))
            Assert.That(blocks.Publish(owner, Key('C'), large), Is.True);
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(Payload(1024)));
        Assert.That(Read(reopened, restored, 'B'), Is.Null);
        Assert.That(Read(reopened, restored, 'C'), Is.EqualTo(large));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SharedGroupReopensAndReturnsExactIndependentEntryBytes(bool compress)
    {
        byte[] first = Payload(1024 * 1024 + 37);
        byte[] second = new byte[50000];
        second[49999] = 19;
        using (var owner = Open(out var blocks))
        {
            Assert.That(blocks.CanPublish(owner, Key('A'), first.Length), Is.True);
            Assert.That(blocks.Publish(owner, Key('A'), first, compress), Is.True);
            Assert.That(blocks.Publish(owner, Key('B'), second, compress), Is.True);
            Assert.That(Directory.GetFiles(Groups, "*.group"), Has.Length.EqualTo(1));
            Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
            if (compress) Assert.That(new FileInfo(Directory.GetFiles(Groups, "*.group")[0]).Length, Is.LessThan(first.Length + second.Length));
        }
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(first));
        Assert.That(Read(reopened, restored, 'B'), Is.EqualTo(second));
        byte[] privateCopy = Read(reopened, restored, 'B')!;
        privateCopy[0] = 97;
        Assert.That(Read(reopened, restored, 'B'), Is.EqualTo(second));
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void LargeEntrySpansBoundedGroupsAndEveryBlockMustExist()
    {
        byte[] payload = Payload(17 * 1024 * 1024 + 13);
        using (var owner = Open(out var blocks)) Assert.That(blocks.Publish(owner, Key('A'), payload), Is.True);
        string[] groups = Directory.GetFiles(Groups, "*.group");
        Assert.That(groups.Length, Is.GreaterThan(1));
        Assert.That(groups.All(p => new FileInfo(p).Length <= GroupedTextureBlocks.GroupBytes), Is.True);
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(payload));
        File.Delete(groups[0]);
        Assert.That(Read(reopened, restored, 'A'), Is.Null);
        Assert.That(restored.LastReason, Is.EqualTo("group-read-io"));
    }

    [TestCase(false, 1, false)]
    [TestCase(false, 1, true)]
    [TestCase(true, 1, false)]
    [TestCase(true, 1, true)]
    [TestCase(false, 2, false)]
    [TestCase(false, 2, true)]
    [TestCase(true, 2, false)]
    [TestCase(true, 2, true)]
    public void BlockAndWholeEntryDigestsAreIndependentlyValidated(bool compress, int blockCount, bool corruptEntryDigest)
    {
        byte[] payload = new byte[(blockCount - 1) * GroupedTextureBlocks.BlockBytes + 3000];
        payload[payload.Length - 1] = 19;
        using (var owner = Open(out var blocks))
        {
            Assert.That(blocks.Publish(owner, Key('A'), payload, compress, ownerKey: Key('E')), Is.True);
            Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(payload));
        }

        string indexPath = Path.Combine(Groups, "index");
        byte[] index = File.ReadAllBytes(indexPath);
        // Version 3: 60-byte index header, then key (64), length (4),
        // entry digest (32), block count (4), owner (64) and used ticks (8).
        Assert.That(BitConverter.ToInt32(index, 4), Is.EqualTo(3));
        Assert.That(BitConverter.ToInt32(index, 160), Is.EqualTo(blockCount));
        if (corruptEntryDigest)
            index[128] ^= 1; // Leave every block and its expected digest valid.
        else
        {
            // Target the last block, so the multi-block case exercises a later
            // block as well. Keep its on-disk header consistent with the index,
            // leaving the payload and whole-entry digest untouched.
            int blockStart = 236 + (blockCount - 1) * 77;
            int blockOffset = BitConverter.ToInt32(index, blockStart + 32);
            int digestOffset = blockStart + 45;
            Assert.That(index[blockStart + 44], Is.EqualTo(compress ? 1 : 0));
            index[digestOffset] ^= 1;
            string groupPath = Directory.GetFiles(Groups, "*.group").Single();
            using var group = new FileStream(groupPath, FileMode.Open, FileAccess.Write, FileShare.None);
            group.Position = blockOffset + 81;
            group.Write(index, digestOffset, 32);
        }
        // Authenticate the changed index so rejection must come from payload
        // validation, rather than the outer index checksum or block identity.
        byte[] indexDigest = CacheDigest.Hash(index, 0, index.Length - 32);
        Buffer.BlockCopy(indexDigest, 0, index, index.Length - 32, indexDigest.Length);
        File.WriteAllBytes(indexPath, index);

        using var reopened = Open(out var restored);
        var snapshot = reopened.ReadExternal(() => restored.PlanNative(Key('E')))!;
        using var context = new GroupedTextureBlocks.ReadContext();
        var failure = Assert.Throws<InvalidDataException>((Action)(() => snapshot.Read(CancellationToken.None, () => { }, context)));
        Assert.That(failure!.Message, Is.EqualTo(corruptEntryDigest ? "group-entry-digest" : "group-block-digest"));
        var repeated = Assert.Throws<InvalidDataException>((Action)(() => snapshot.Read(CancellationToken.None, () => { }, context)));
        Assert.That(repeated!.Message, Is.EqualTo(failure.Message), "Retained handles never cache validation outcomes.");
        Assert.That(Read(reopened, restored, 'A'), Is.Null);
        Assert.That(restored.LastReason, Is.EqualTo(corruptEntryDigest ? "group-entry-digest" : "group-block-digest"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CorruptNeighbourDoesNotPreventRandomAccessToOtherEntry(bool compress)
    {
        byte[] first = new byte[3000], second = Payload(1700);
        using var owner = Open(out var blocks);
        Assert.That(blocks.Publish(owner, Key('A'), first, compress), Is.True);
        Assert.That(blocks.Publish(owner, Key('B'), second, compress), Is.True);
        string path = Directory.GetFiles(Groups, "*.group").Single();
        using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // The first block begins at offset zero. Damage its stored bytes,
            // leaving the second entry's independently checked block intact.
            file.Position = 113;
            int value = file.ReadByte(); file.Position = 113; file.WriteByte((byte)(value ^ 0x55));
        }
        Assert.That(Read(owner, blocks, 'A'), Is.Null);
        Assert.That(Read(owner, blocks, 'B'), Is.EqualTo(second));
    }

    [Test]
    public void ChangedSourceKeyAndRemovalLeaveUnchangedEntryReadableAcrossReopen()
    {
        byte[] first = Payload(777), second = Payload(888), changed = Payload(999);
        using (var owner = Open(out var blocks))
        {
            Assert.That(blocks.Publish(owner, Key('A'), first), Is.True);
            Assert.That(blocks.Publish(owner, Key('B'), second), Is.True);
            Assert.That(blocks.Publish(owner, Key('C'), changed), Is.True);
            Assert.That(blocks.Invalidate(owner, Key('A')), Is.True);
            Assert.That(Read(owner, blocks, 'A'), Is.Null);
            Assert.That(Read(owner, blocks, 'B'), Is.EqualTo(second));
            Assert.That(Read(owner, blocks, 'C'), Is.EqualTo(changed));
        }
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.Null);
        Assert.That(Read(reopened, restored, 'B'), Is.EqualTo(second));
        Assert.That(Read(reopened, restored, 'C'), Is.EqualTo(changed));
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void SameProviderSourceReplacementRemovesOldKeyOnlyAfterCommit()
    {
        using (var owner = Open(out var blocks))
        {
            Assert.That(blocks.Publish(owner, Key('A'), Payload(700), ownerKey: Key('D')), Is.True);
            Assert.That(blocks.Publish(owner, Key('B'), Payload(800), ownerKey: Key('E')), Is.True);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.That(blocks.Publish(owner, Key('C'), Payload(900), cancellation: cancelled.Token, ownerKey: Key('D')), Is.False);
            Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(700)));
            Assert.That(blocks.Publish(owner, Key('C'), Payload(900), ownerKey: Key('D')), Is.True);
            blocks.Complete(owner);
        }
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.Null);
        Assert.That(Read(reopened, restored, 'B'), Is.EqualTo(Payload(800)));
        Assert.That(Read(reopened, restored, 'C'), Is.EqualTo(Payload(900)));
    }

    [Test]
    public void QuotaCanReclaimUnvisitedPreviousSessionEntries()
    {
        using (var owner = Open(out var blocks, limit: 3700))
            Assert.That(blocks.Publish(owner, Key('A'), Payload(1024)), Is.True);
        using var reopened = Open(out var restored, limit: 3700);
        Assert.That(restored.Publish(reopened, Key('B'), Payload(1800)), Is.True);
        Assert.That(Read(reopened, restored, 'A'), Is.Null);
        Assert.That(Read(reopened, restored, 'B'), Is.EqualTo(Payload(1800)));
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void RefusedReplacementAndPrecancelPreserveThePreviousCommittedGeneration()
    {
        byte[] original = Payload(1024);
        using var owner = Open(out var blocks, limit: 2700);
        Assert.That(blocks.Publish(owner, Key('A'), original), Is.True);
        byte[] index = File.ReadAllBytes(Path.Combine(Groups, "index"));
        Assert.That(blocks.Publish(owner, Key('A'), Payload(1024, 2)), Is.False);
        Assert.That(blocks.LastReason, Is.EqualTo("group-quota"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.That(blocks.Publish(owner, Key('A'), new byte[] { 4 }, cancellation: cancellation.Token), Is.False);
        Assert.That(blocks.LastReason, Is.EqualTo("group-cancelled"));
        Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(original));
        Assert.That(File.ReadAllBytes(Path.Combine(Groups, "index")), Is.EqualTo(index));
        Assert.That(Directory.GetFiles(Groups, "*.pending"), Is.Empty);
        Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void SharedBudgetIncludesGroupedIndexAndOtherCategories()
    {
        using var budget = new SharedCacheBudget(root, 4000);
        using var owner = Open(out var blocks, shared: budget);
        Assert.That(blocks.Publish(owner, Key('A'), Payload(1024)), Is.True);
        string xmlRoot = Path.Combine(root, "WakeUp", "ParsedXml", "v1");
        using var xml = new OwnedCacheStore(xmlRoot, 4000, 4000, Policy("normal"), sharedBudget: budget);
        Assert.That(xml.Publish(Key('D'), Payload(2000)), Is.True);
        Assert.That(blocks.Publish(owner, Key('B'), Payload(1024)), Is.False);
        Assert.That(Read(owner, blocks, 'A'), Is.Not.Null);
        Assert.That(budget.StoredBytes, Is.EqualTo(owner.StoredBytes + xml.StoredBytes));
        Assert.That(budget.StoredBytes, Is.LessThanOrEqualTo(4000));
    }

    [Test]
    public void BypassDoesNotTouchGroupsAndClearWorksWithoutAFeatureReader()
    {
        using (var seed = Open(out var blocks)) Assert.That(blocks.Publish(seed, Key('A'), Payload(1000)), Is.True);
        byte[] index = File.ReadAllBytes(Path.Combine(Groups, "index"));
        using (var bypass = Open(out var blocks, action: "bypass"))
        {
            Assert.That(Read(bypass, blocks, 'A'), Is.Null);
            Assert.That(blocks.Publish(bypass, Key('A'), Payload(1)), Is.False);
            Assert.That(blocks.Invalidate(bypass, Key('A')), Is.False);
        }
        Assert.That(File.ReadAllBytes(Path.Combine(Groups, "index")), Is.EqualTo(index));
        using var clear = Open(out _, action: "clear");
        Assert.That(Directory.GetFiles(Groups), Is.Empty);
        Assert.That(clear.StoredBytes, Is.Zero);
    }

    [Test]
    public void RebuildSkipsOldReadsThenNormalLaunchConsumesReplacement()
    {
        using (var seed = Open(out var blocks)) Assert.That(blocks.Publish(seed, Key('A'), new byte[] { 1 }), Is.True);
        using (var rebuild = Open(out var blocks, action: "rebuild"))
        {
            Assert.That(Read(rebuild, blocks, 'A'), Is.Null);
            Assert.That(blocks.Publish(rebuild, Key('A'), new byte[] { 2 }), Is.True);
        }
        using var normal = Open(out var restored);
        Assert.That(Read(normal, restored, 'A'), Is.EqualTo(new byte[] { 2 }));
    }

    [Test]
    public void RefusedCheckpointKeepsDurableEntriesAndOldIndex()
    {
        using var owner = Open(out var blocks);
        Assert.That(blocks.Publish(owner, Key('A'), Payload(1200)), Is.True);
        string path = Directory.GetFiles(Groups, "*.group").Single();
        byte[] original = File.ReadAllBytes(Path.Combine(Groups, "index"));
        Assert.That(blocks.Publish(owner, Key('B'), Payload(900)), Is.True);
        using (var held = new FileStream(Path.Combine(Groups, "index"), FileMode.Open, FileAccess.Read, FileShare.Read))
            blocks.Complete(owner);
        Assert.That(File.ReadAllBytes(Path.Combine(Groups, "index")), Is.EqualTo(original));
        Assert.That(Read(owner, blocks, 'A'), Is.EqualTo(Payload(1200)));
        Assert.That(Read(owner, blocks, 'B'), Is.EqualTo(Payload(900)));
        Assert.That(Directory.GetFiles(Groups, "*.delta"), Has.Length.EqualTo(1));
        Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void EntryDeltasAreBoundedAndReopenWithoutPerImageFullIndexRewrite()
    {
        using (var owner = Open(out var blocks))
        {
            Assert.That(blocks.Publish(owner, 0.ToString("X64"), Payload(30)), Is.True);
            byte[] firstIndex = File.ReadAllBytes(Path.Combine(Groups, "index"));
            for (int i = 1; i <= 128; i++) Assert.That(blocks.Publish(owner, i.ToString("X64"), Payload(30, i)), Is.True);
            Assert.That(File.ReadAllBytes(Path.Combine(Groups, "index")), Is.EqualTo(firstIndex));
            Assert.That(Directory.GetFiles(Groups, "*.delta"), Has.Length.EqualTo(128));
            Assert.That(blocks.Publish(owner, 129.ToString("X64"), Payload(30, 129)), Is.True);
            Assert.That(File.ReadAllBytes(Path.Combine(Groups, "index")), Is.Not.EqualTo(firstIndex));
            Assert.That(Directory.GetFiles(Groups, "*.delta"), Has.Length.EqualTo(1));
            Assert.That(owner.StoredBytes, Is.EqualTo(ActualBytes));
        }
        using var reopened = Open(out var restored);
        for (int i = 1; i <= 129; i++)
            Assert.That(reopened.ReadExternal(() => restored.Read(i.ToString("X64"))), Is.EqualTo(Payload(30, i)));
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void CorruptDeltaIsLocalAndCheckpointIgnoresObsoleteRecords()
    {
        byte[] oldDelta; string name;
        using (var owner = Open(out var blocks))
        {
            Assert.That(blocks.Publish(owner, Key('A'), Payload(600)), Is.True);
            Assert.That(blocks.Publish(owner, Key('B'), Payload(700)), Is.True);
            name = Directory.GetFiles(Groups, "*.delta").Single(); oldDelta = File.ReadAllBytes(name);
            Assert.That(blocks.Publish(owner, Key('C'), Payload(800)), Is.True);
        }
        byte[] corrupt = (byte[])oldDelta.Clone(); corrupt[50] ^= 1; File.WriteAllBytes(name, corrupt);
        using (var reopened = Open(out var restored))
        {
            Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(Payload(600)));
            Assert.That(Read(reopened, restored, 'B'), Is.Null);
            Assert.That(Read(reopened, restored, 'C'), Is.EqualTo(Payload(800)));
            restored.Complete(reopened);
        }
        // Crash between checkpoint publication and removing an old record.
        File.WriteAllBytes(name, oldDelta);
        using var final = Open(out var settled);
        Assert.That(Read(final, settled, 'A'), Is.EqualTo(Payload(600)));
        Assert.That(Read(final, settled, 'B'), Is.Null);
        Assert.That(Read(final, settled, 'C'), Is.EqualTo(Payload(800)));
        Assert.That(File.Exists(name), Is.False);
    }

    [Test]
    public void IdenticalCheckpointContentsDoNotReviveAnInvalidatedDelta()
    {
        byte[] stale; string name;
        using (var owner = Open(out var blocks))
        {
            Assert.That(blocks.Publish(owner, Key('A'), Payload(600)), Is.True);
            byte[] originalIndex = File.ReadAllBytes(Path.Combine(Groups, "index"));
            Assert.That(blocks.Publish(owner, Key('B'), Payload(700)), Is.True);
            name = Directory.GetFiles(Groups, "*.delta").Single(); stale = File.ReadAllBytes(name);
            Assert.That(blocks.Invalidate(owner, Key('B')), Is.True);
            // No reads changed A's use timestamp: the logical index is exactly
            // the old A-only state, but its authenticated generation must differ.
            Assert.That(File.ReadAllBytes(Path.Combine(Groups, "index")), Is.Not.EqualTo(originalIndex));
        }
        File.WriteAllBytes(name, stale);
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(Payload(600)));
        Assert.That(Read(reopened, restored, 'B'), Is.Null);
        Assert.That(File.Exists(name), Is.False);
    }

    [Test]
    public void RestartTrimsUncommittedSuffixAndKeepsCommittedPrefix()
    {
        using (var seed = Open(out var blocks)) Assert.That(blocks.Publish(seed, Key('A'), Payload(1200)), Is.True);
        string path = Directory.GetFiles(Groups, "*.group").Single();
        byte[] original = File.ReadAllBytes(path);
        using (var file = new FileStream(path, FileMode.Append)) file.Write(Payload(700), 0, 700);
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(Payload(1200)));
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
        Assert.That(restored.Publish(reopened, Key('B'), Payload(800)), Is.True);
        Assert.That(Directory.GetFiles(Groups, "*.group").Single(), Is.EqualTo(path));
        Assert.That(File.ReadAllBytes(path).Take(original.Length), Is.EqualTo(original));
        Assert.That(Read(reopened, restored, 'B'), Is.EqualTo(Payload(800)));
    }

    [Test]
    public void RestartReclaimsInterruptedOwnedFilesAndAccountsForUnknownFiles()
    {
        using (var seed = Open(out var blocks)) Assert.That(blocks.Publish(seed, Key('A'), new byte[] { 1 }), Is.True);
        string pending = Path.Combine(Groups, Guid.NewGuid().ToString("N") + ".pending");
        string orphan = Path.Combine(Groups, Guid.NewGuid().ToString("N") + ".group");
        string unknown = Path.Combine(Groups, "not-ours.pending");
        File.WriteAllBytes(pending, new byte[] { 2 }); File.WriteAllBytes(orphan, new byte[] { 3 }); File.WriteAllBytes(unknown, new byte[] { 4 });
        using var reopened = Open(out var restored);
        Assert.That(File.Exists(pending) || File.Exists(orphan), Is.False);
        Assert.That(File.Exists(unknown), Is.True);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(new byte[] { 1 }));
        Assert.That(reopened.StoredBytes, Is.EqualTo(ActualBytes));
    }

    [Test]
    public void CorruptIndexRefusesMappingsAndCanBeRebuilt()
    {
        using (var seed = Open(out var blocks)) Assert.That(blocks.Publish(seed, Key('A'), Payload(50)), Is.True);
        string path = Path.Combine(Groups, "index"); byte[] bytes = File.ReadAllBytes(path); bytes[76] ^= 1; File.WriteAllBytes(path, bytes);
        using var reopened = Open(out var restored);
        Assert.That(Read(reopened, restored, 'A'), Is.Null);
        Assert.That(Directory.GetFiles(Groups, "*.group"), Is.Empty);
        Assert.That(restored.Publish(reopened, Key('A'), new byte[] { 9 }), Is.True);
        Assert.That(Read(reopened, restored, 'A'), Is.EqualTo(new byte[] { 9 }));
    }

    [Test]
    public void AdmissionRejectsOversizedOrUnboundEntriesBeforeConstructingBytes()
    {
        using var owner = Open(out var blocks);
        Assert.That(blocks.CanPublish(owner, Key('A'), GroupedTextureBlocks.MaximumEntry), Is.True);
        Assert.That(blocks.CanPublish(owner, Key('A'), GroupedTextureBlocks.MaximumEntry + 1), Is.False);
        Assert.That(blocks.CanPublish(owner, "unbound", 1), Is.False);
        Assert.That(blocks.CanPublish(owner, Key('A'), 0), Is.False);
        Assert.That(Directory.Exists(Groups), Is.False);
    }
}
