// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class OwnedCacheStoreTests
{
    private string root = null!;
    private static string Key(char value) => new(value, 64);
    private static CacheLaunchPolicy Policy(string action = "normal")
    {
        CacheLaunchPolicy? selected = null;
        return CacheLaunchPolicy.Latch(ref selected, action, () => { });
    }
    private OwnedCacheStore Open(long budget = 4096, string action = "normal") => new(root, 64, budget, Policy(action));
    private string EntryPath(char value) => Path.Combine(root, Key(value) + ".bin");

    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "wake-up-store-test-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Test]
    public void PathAttributesMatchManagedAttributesAndPermitMissingFutureFiles()
    {
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "ordinary-\u03bb.bin");
        File.WriteAllBytes(file, new byte[] { 1 });
        foreach (string path in new[] { root, file, Path.Combine(root, ".", "ordinary-\u03bb.bin") })
        {
            Assert.That(OwnedCacheStore.ExistingPathAttributes(Path.GetFullPath(path)), Is.EqualTo(File.GetAttributes(path)));
            Assert.DoesNotThrow((Action)(() => OwnedCacheStore.RejectLinkedPath(path)));
        }
        string missing = Path.Combine(root, "future", "nested", "entry.bin");
        Assert.That(OwnedCacheStore.ExistingPathAttributes(missing), Is.Null);
        Assert.DoesNotThrow((Action)(() => OwnedCacheStore.RejectLinkedPath(missing)));
    }

    [Test]
    public void InvalidWindowsAttributeQueryFailsClosed()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows error contract.");
        Directory.CreateDirectory(root);
        Assert.Throws<IOException>((Action)(() => OwnedCacheStore.ExistingPathAttributes(Path.Combine(root, "invalid?.bin"))));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CreateSymbolicLink(string link, string target, uint flags);

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void LinkedFileDirectoryAndAncestorOfMissingFileAreRefused(bool directory, bool missingDescendant)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows reparse-point contract.");
        Directory.CreateDirectory(root);
        string target = Path.Combine(root, "target");
        string link = Path.Combine(root, "link");
        if (directory) Directory.CreateDirectory(target);
        else File.WriteAllBytes(target, new byte[] { 1 });
        uint flags = directory ? 1u : 0u;
        bool created = CreateSymbolicLink(link, target, flags | 2u); // Allow an existing developer-mode grant.
        int error = created ? 0 : Marshal.GetLastWin32Error();
        if (!created && error == 87) // Older Windows does not recognize that flag.
        {
            created = CreateSymbolicLink(link, target, flags);
            error = created ? 0 : Marshal.GetLastWin32Error();
        }
        if (!created && error == 1314) Assert.Ignore("Existing Windows permissions do not permit creating a test symlink.");
        Assert.That(created, Is.True, "CreateSymbolicLinkW error " + error);
        try
        {
            Assert.That(OwnedCacheStore.ExistingPathAttributes(link), Is.EqualTo(File.GetAttributes(link)));
            string path = missingDescendant ? Path.Combine(link, "future", "entry.bin") : link;
            var failure = Assert.Throws<IOException>((Action)(() => OwnedCacheStore.RejectLinkedPath(path)));
            Assert.That(failure!.Message, Is.EqualTo("cache-linked-path"));
        }
        finally
        {
            // Remove only the link itself before the ordinary directory cleanup.
            if (directory) Directory.Delete(link);
            else File.Delete(link);
        }
    }

    [TestCase("interrupted")]
    [TestCase("short")]
    [TestCase("long")]
    public void FailedWriterPreservesPublishedGenerationAndRemovesItsTemporaryFile(string failure)
    {
        using var store = Open();
        byte[] original = { 1, 2, 3, 4 };
        Assert.That(store.Publish(Key('A'), original), Is.True);
        byte[] published = File.ReadAllBytes(EntryPath('A'));
        Assert.That(store.Publish(Key('A'), 4, stream =>
        {
            stream.WriteByte(9);
            if (failure == "interrupted") throw new IOException("simulated-interruption");
            if (failure == "long") stream.Write(new byte[4], 0, 4);
        }), Is.False);
        Assert.That(File.ReadAllBytes(EntryPath('A')), Is.EqualTo(published));
        Assert.That(store.Read(Key('A')), Is.EqualTo(original));
        Assert.That(store.StoredBytes, Is.EqualTo(published.Length));
        Assert.That(Directory.GetFiles(root, "*.pending"), Is.Empty);
    }

    [Test]
    public void ReopeningRemovesRecognizedInterruptedWritesAndRetainsPublishedDataAndUnknownFiles()
    {
        using (var initial = Open()) Assert.That(initial.Publish(Key('A'), new byte[] { 1 }), Is.True);
        string pending = Path.Combine(root, Key('B') + "." + Guid.NewGuid().ToString("N") + ".pending");
        string usagePending = Path.Combine(root, "usage." + Guid.NewGuid().ToString("N") + ".pending");
        string unknown = Path.Combine(root, "source.dds.pending");
        string malformedUsage = Path.Combine(root, "usage.pending");
        File.WriteAllBytes(pending, new byte[] { 2 });
        File.WriteAllBytes(usagePending, new byte[] { 3 });
        File.WriteAllBytes(unknown, new byte[] { 4 });
        File.WriteAllBytes(malformedUsage, new byte[] { 5 });
        using var reopened = Open();
        Assert.That(reopened.PendingRemoved, Is.EqualTo(2));
        Assert.That(File.Exists(pending) || File.Exists(usagePending), Is.False);
        Assert.That(File.ReadAllBytes(unknown), Is.EqualTo(new byte[] { 4 }));
        Assert.That(File.ReadAllBytes(malformedUsage), Is.EqualTo(new byte[] { 5 }));
        Assert.That(reopened.Read(Key('A')), Is.EqualTo(new byte[] { 1 }));
    }

    [Test]
    public void QuotaPrunesOldestUnusedEntryAndProtectsEntriesReadOrPublishedThisLaunch()
    {
        long size = OwnedCacheStore.EnvelopeBytes + 64;
        using (var seed = Open())
            foreach (char key in new[] { 'A', 'B', 'C' }) Assert.That(seed.Publish(Key(key), new byte[64]), Is.True);
        File.WriteAllLines(Path.Combine(root, "usage"), new[] { Key('A') + " 1", Key('B') + " 2", Key('C') + " 3" });
        long metadata = new FileInfo(Path.Combine(root, "usage")).Length;
        // Room for three payloads, the old usage file, and its replacement.
        // Pruning one entry must also reduce the replacement-summary reservation.
        long budget = 3 * size + metadata + 3 * 96;
        using var store = Open(budget);
        Assert.That(store.Read(Key('A')), Is.Not.Null);
        Assert.That(store.Publish(Key('D'), new byte[64]), Is.True);
        Assert.That(File.Exists(EntryPath('A')), Is.True);
        Assert.That(File.Exists(EntryPath('B')), Is.False);
        Assert.That(File.Exists(EntryPath('C')), Is.True);
        Assert.That(store.Pruned, Is.EqualTo(1));
        Assert.That(store.StoredBytes, Is.EqualTo(3 * size + metadata));
        Assert.That(store.Read(Key('C')), Is.Not.Null);
        Assert.That(store.Publish(Key('E'), new byte[64]), Is.False);
        Assert.That(store.LastReason, Is.EqualTo("quota"));
        Assert.That(store.StoredBytes, Is.EqualTo(3 * size + metadata));
        store.Complete();
        long actualBytes = Directory.GetFiles(root, "*.bin").Sum(path => new FileInfo(path).Length)
            + new FileInfo(Path.Combine(root, "usage")).Length;
        Assert.That(store.StoredBytes, Is.EqualTo(actualBytes));
        Assert.That(actualBytes, Is.LessThanOrEqualTo(budget));
        Assert.That(Directory.GetFiles(root, "*.pending"), Is.Empty);
    }

    [Test]
    public void ReplacementReservesTemporaryAndPreviousBytesBeforeTouchingOldGeneration()
    {
        long size = OwnedCacheStore.EnvelopeBytes + 1;
        using var store = Open(size + 96);
        Assert.That(store.Publish(Key('A'), new byte[] { 1 }), Is.True);
        bool invoked = false;
        Assert.That(store.Publish(Key('A'), 1, stream => { invoked = true; stream.WriteByte(2); }), Is.False);
        Assert.That(invoked, Is.False);
        Assert.That(store.Read(Key('A')), Is.EqualTo(new byte[] { 1 }));
        Assert.That(Directory.GetFiles(root, "*.pending"), Is.Empty);
    }

    [Test]
    public void ReturnedBytesArePrivateAndRemainValidAfterReplacementAndClear()
    {
        byte[] first;
        byte[] second;
        using (var store = Open())
        {
            Assert.That(store.Publish(Key('A'), new byte[] { 1, 2 }), Is.True);
            first = store.Read(Key('A'))!;
            second = store.Read(Key('A'))!;
            Assert.That(first, Is.Not.SameAs(second));
            first[0] = 7;
            Assert.That(second, Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(store.Publish(Key('A'), new byte[] { 3, 4 }), Is.True);
            Assert.That(store.Read(Key('A')), Is.EqualTo(new byte[] { 3, 4 }));
        }
        using var cleared = Open(action: "clear");
        Assert.That(File.Exists(EntryPath('A')), Is.False);
        Assert.That(first, Is.EqualTo(new byte[] { 7, 2 }));
        Assert.That(second, Is.EqualTo(new byte[] { 1, 2 }));
    }

    [Test]
    public void AnotherOwnerIsRefusedUntilFirstOwnerDisposes()
    {
        using (var first = Open())
        {
            Assert.Throws<IOException>((Action)(() => { using var second = Open(); }));
            Assert.That(first.Publish(Key('A'), new byte[] { 1 }), Is.True);
        }
        using var next = Open();
        Assert.That(next.Read(Key('A')), Is.EqualTo(new byte[] { 1 }));
    }

    [TestCase("truncated")]
    [TestCase("version")]
    [TestCase("identity")]
    [TestCase("length")]
    [TestCase("digest")]
    [TestCase("payload")]
    public void DamagedEnvelopeIsRejectedAndRemovedThenCanBeRebuilt(string damage)
    {
        using var store = Open();
        Assert.That(store.Publish(Key('A'), new byte[] { 1, 2, 3 }), Is.True);
        byte[] bytes = File.ReadAllBytes(EntryPath('A'));
        if (damage == "truncated") bytes = bytes.Take(OwnedCacheStore.EnvelopeBytes - 1).ToArray();
        else bytes[damage == "version" ? 4 : damage == "identity" ? 8 : damage == "length" ? 72 : damage == "digest" ? 76 : 108] ^= 1;
        File.WriteAllBytes(EntryPath('A'), bytes);
        Assert.That(store.Read(Key('A')), Is.Null);
        Assert.That(store.Rejected, Is.EqualTo(1));
        Assert.That(File.Exists(EntryPath('A')), Is.False);
        Assert.That(store.StoredBytes, Is.Zero);
        Assert.That(store.Publish(Key('A'), new byte[] { 4 }), Is.True);
        Assert.That(store.Read(Key('A')), Is.EqualTo(new byte[] { 4 }));
    }

    [Test]
    public void ConsumerValidationFailureRejectsPublishedPayloadAndAllowsRecovery()
    {
        using var store = Open();
        Assert.That(store.Publish(Key('A'), new byte[] { 1 }), Is.True);
        Assert.That(store.Read<byte[]>(Key('A'), _ => throw new InvalidDataException("consumer-layout")), Is.Null);
        Assert.That(store.LastReason, Is.EqualTo("consumer-layout"));
        Assert.That(File.Exists(EntryPath('A')), Is.False);
        Assert.That(store.StoredBytes, Is.Zero);
        Assert.That(store.Publish(Key('A'), new byte[] { 2 }), Is.True);
        Assert.That(store.Read(Key('A'), bytes => bytes), Is.EqualTo(new byte[] { 2 }));
    }

    [Test]
    public void BypassForTwoConsumersDoesNotTouchPublishedOrPendingBytesAndNextLaunchRecovers()
    {
        using (var seed = Open()) Assert.That(seed.Publish(Key('A'), new byte[] { 1 }), Is.True);
        string pending = Path.Combine(root, Key('B') + "." + Guid.NewGuid().ToString("N") + ".pending");
        File.WriteAllBytes(pending, new byte[] { 8 });
        byte[] before = File.ReadAllBytes(EntryPath('A'));
        CacheLaunchPolicy? latch = null;
        string saved = "bypass";
        var firstPolicy = CacheLaunchPolicy.Latch(ref latch, saved, () => saved = "normal");
        var secondPolicy = CacheLaunchPolicy.Latch(ref latch, saved, () => Assert.Fail("already consumed"));
        using (var first = new OwnedCacheStore(root, 64, 4096, firstPolicy))
        using (var second = new OwnedCacheStore(root, 64, 4096, secondPolicy))
        {
            Assert.That(first.Read(Key('A')), Is.Null);
            Assert.That(second.Publish(Key('A'), new byte[] { 9 }), Is.False);
            first.Invalidate(Key('A'));
            first.Complete();
            second.Complete();
        }
        Assert.That(File.ReadAllBytes(EntryPath('A')), Is.EqualTo(before));
        Assert.That(File.ReadAllBytes(pending), Is.EqualTo(new byte[] { 8 }));
        using var following = Open(action: saved);
        Assert.That(following.Read(Key('A')), Is.EqualTo(new byte[] { 1 }));
        Assert.That(File.Exists(pending), Is.False);
    }

    [Test]
    public void CompletionReclaimsExpiredUnusedEntriesButKeepsCurrentLaunchHits()
    {
        using (var seed = Open())
            foreach (char key in new[] { 'A', 'B' }) Assert.That(seed.Publish(Key(key), new byte[] { 1 }), Is.True);
        File.WriteAllLines(Path.Combine(root, "usage"), new[] { Key('A') + " 1", Key('B') + " 2" });
        using var store = Open();
        Assert.That(store.Read(Key('A')), Is.Not.Null);
        store.Complete();
        Assert.That(File.Exists(EntryPath('A')), Is.True);
        Assert.That(File.Exists(EntryPath('B')), Is.False);
        Assert.That(File.ReadAllLines(Path.Combine(root, "usage")).Length, Is.EqualTo(1));
    }
}
