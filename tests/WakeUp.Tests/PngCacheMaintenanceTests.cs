// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PngCacheMaintenanceTests
{
    private string root = null!;
    private static readonly string Key = new('A', 64);
    private static CacheLaunchPolicy Policy(string action)
    {
        CacheLaunchPolicy? selected = null;
        return CacheLaunchPolicy.Latch(ref selected, action, () => { });
    }
    private static PngCache.Entry Entry(byte value) => new()
    {
        Width = 1, Height = 1, TextureFormat = 4, GraphicsFormat = 8, Mips = 1,
        Filter = 2, WrapU = 1, WrapV = 2, WrapW = 0, Aniso = 4, Bias = 0.25f,
        Pixels = new byte[] { value, 2, 3, 4 }
    };

    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "wake-up-png-maintenance-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Test]
    public void LegacyCleanupOnlyRemovesExactGeneratedNames()
    {
        Directory.CreateDirectory(root);
        string[] owned = { Key + ".bin", Key + ".bin.pending" };
        string[] retained = { Key + ".dds", "source.png", "notes.bin", new string('b', 64) + ".bin", "unrecognized.pending" };
        foreach (string name in owned) File.WriteAllBytes(Path.Combine(root, name), new byte[] { 1 });
        foreach (string name in retained) File.WriteAllBytes(Path.Combine(root, name), new byte[] { 2 });
        using var cache = new PngCache(root, Policy("normal"));
        foreach (string name in owned) Assert.That(File.Exists(Path.Combine(root, name)), Is.False, name);
        foreach (string name in retained) Assert.That(File.ReadAllBytes(Path.Combine(root, name)), Is.EqualTo(new byte[] { 2 }), name);
    }

    [Test]
    public void BypassLeavesLegacyDataUntouchedAndDoesNotCreateVersionedStore()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, Key + ".bin.pending");
        File.WriteAllBytes(path, new byte[] { 1 });
        using var cache = new PngCache(root, Policy("bypass"));
        cache.Write(Key, Entry(1));
        Assert.That(cache.Read(Key), Is.Null);
        cache.Invalidate(Key);
        cache.Complete();
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1 }));
        Assert.That(Directory.Exists(Path.Combine(root, "v1")), Is.False);
    }

    [Test]
    public void RebuildSkipsReadsAndPublishesReplacementForFollowingLaunch()
    {
        using (var seed = new PngCache(root, Policy("normal"))) seed.Write(Key, Entry(1));
        using (var rebuild = new PngCache(root, Policy("rebuild")))
        {
            Assert.That(rebuild.Read(Key), Is.Null);
            rebuild.Write(Key, Entry(9));
            Assert.That(rebuild.Writes, Is.EqualTo(1));
            Assert.That(rebuild.Read(Key), Is.Null);
        }
        using var following = new PngCache(root, Policy("normal"));
        Assert.That(following.Read(Key)!.Pixels, Is.EqualTo(Entry(9).Pixels));
    }

    [Test]
    public void ClearMaintenanceWorksWithoutActivatingTextureCacheAndDoesNotRepopulate()
    {
        string cacheRoot = Path.Combine(root, "WakeUp", "PngCache");
        using (var seed = new PngCache(cacheRoot, Policy("normal"))) seed.Write(Key, Entry(1));
        var policy = Policy("clear");
        PngCache.Maintain(root, policy);
        using var cleared = new PngCache(cacheRoot, policy);
        Assert.That(cleared.StoredBytes, Is.Zero);
        Assert.That(cleared.Read(Key), Is.Null);
        cleared.Write(Key, Entry(9));
        Assert.That(cleared.Writes, Is.Zero);
        Assert.That(Directory.GetFiles(Path.Combine(cacheRoot, "v1"), "*.bin"), Is.Empty);
    }

    [Test]
    public void ChangedSourceBytesHaveDifferentContentIdentityWithSamePathLengthAndTimestamp()
    {
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "source.png");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3, 4 });
        DateTime stamp = File.GetLastWriteTimeUtc(source);
        long length = new FileInfo(source).Length;
        string oldKey = PngCache.Hex(PngCache.Hash(File.ReadAllBytes(source)));
        using var cache = new PngCache(Path.Combine(root, "cache"), Policy("normal"));
        cache.Write(oldKey, Entry(1));
        File.WriteAllBytes(source, new byte[] { 4, 3, 2, 1 });
        File.SetLastWriteTimeUtc(source, stamp);
        string newKey = PngCache.Hex(PngCache.Hash(File.ReadAllBytes(source)));
        Assert.That(new FileInfo(source).Length, Is.EqualTo(length));
        Assert.That(File.GetLastWriteTimeUtc(source), Is.EqualTo(stamp));
        Assert.That(newKey, Is.Not.EqualTo(oldKey));
        Assert.That(cache.Read(newKey), Is.Null);
        Assert.That(cache.Read(oldKey), Is.Not.Null);
    }

    [Test]
    public void FullProtectedStoreRejectsCaptureButKeepsWarmHits()
    {
        using var cache = new PngCache(root, Policy("normal"), maximumStore: 600);
        cache.Write(Key, Entry(1));
        cache.Write(new string('B', 64), Entry(2));
        Assert.That(cache.Writes, Is.EqualTo(2));
        int captures = 0;
        Assert.That(cache.TryCapture(new string('C', 64), 4, () => captures++), Is.False);
        Assert.That(captures, Is.Zero);
        Assert.That(cache.LastReason, Is.EqualTo("quota"));
        Assert.That(cache.Read(Key)!.Pixels, Is.EqualTo(Entry(1).Pixels));
        Assert.That(cache.Pruned, Is.Zero);
    }

    [Test]
    public void BoundedEvictionAdmitsCaptureAcrossMipCallbacksAndPublication()
    {
        string retained = new('B', 64), incoming = new('C', 64);
        using (var seed = new PngCache(root, Policy("normal"), maximumStore: 600))
        {
            seed.Write(Key, Entry(1));
            seed.Write(retained, Entry(2));
        }
        using var cache = new PngCache(root, Policy("normal"), maximumStore: 600);
        Assert.That(cache.Read(retained), Is.Not.Null);
        int captures = 0;
        for (int mip = 0; mip < 3; mip++)
            Assert.That(cache.TryCapture(incoming, 4, () => captures++), Is.True);
        Assert.That(captures, Is.EqualTo(3));
        Assert.That(cache.Pruned, Is.EqualTo(1));
        cache.Write(incoming, Entry(9));
        Assert.That(cache.Writes, Is.EqualTo(1));
        Assert.That(cache.Read(incoming)!.Pixels, Is.EqualTo(Entry(9).Pixels));
        Assert.That(cache.Read(retained)!.Pixels, Is.EqualTo(Entry(2).Pixels));
        Assert.That(cache.Read(Key), Is.Null);
        cache.Complete();
        Assert.That(cache.StoredBytes, Is.LessThanOrEqualTo(600));
    }

    [Test]
    public void RebuildWithoutReplacementHeadroomRejectsCaptureAndRetainsOldFile()
    {
        using (var seed = new PngCache(root, Policy("normal"), maximumStore: 300)) seed.Write(Key, Entry(1));
        string path = Path.Combine(root, "v1", Key + ".bin");
        byte[] old = File.ReadAllBytes(path);
        using (var rebuild = new PngCache(root, Policy("rebuild"), maximumStore: 300))
        {
            int captures = 0;
            Assert.That(rebuild.Read(Key), Is.Null);
            Assert.That(rebuild.TryCapture(Key, 4, () => captures++), Is.False);
            Assert.That(captures, Is.Zero);
            Assert.That(rebuild.Pruned, Is.Zero);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(old));
        }
        using var following = new PngCache(root, Policy("normal"), maximumStore: 300);
        Assert.That(following.Read(Key)!.Pixels, Is.EqualTo(Entry(1).Pixels));
    }

    [TestCase("capture")]
    [TestCase("publication")]
    public void FailedRebuildAfterAdmissionPreservesPreviousGenerationWithoutLeakingCapacity(string failure)
    {
        using (var seed = new PngCache(root, Policy("normal"), maximumStore: 600)) seed.Write(Key, Entry(1));
        string path = Path.Combine(root, "v1", Key + ".bin");
        byte[] old = File.ReadAllBytes(path);
        using (var rebuild = new PngCache(root, Policy("rebuild"), maximumStore: 600))
        {
            int captures = 0;
            if (failure == "capture")
                Assert.Throws<IOException>((Action)(() => rebuild.TryCapture(Key, 4, () =>
                { captures++; throw new IOException("simulated-capture-failure"); })));
            else
            {
                Assert.That(rebuild.TryCapture(Key, 4, () => captures++), Is.True);
                // Deny replacement after admission, without changing the valid file.
                using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                rebuild.Write(Key, Entry(9));
                Assert.That(rebuild.Errors, Is.EqualTo(1));
            }
            Assert.That(captures, Is.EqualTo(1));
            Assert.That(rebuild.Writes, Is.Zero);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(old));
            Assert.That(Directory.GetFiles(Path.Combine(root, "v1"), "*.pending"), Is.Empty);
            Assert.That(rebuild.CanCapture(Key, 4), Is.True);
        }
        using var following = new PngCache(root, Policy("normal"), maximumStore: 600);
        Assert.That(following.Read(Key)!.Pixels, Is.EqualTo(Entry(1).Pixels));
    }

    [Test]
    public void ExistingPixelAndStoreLimitsRemainUnchanged()
    {
        Assert.That(PngCache.MaximumEntry, Is.EqualTo(8 * 1024 * 1024));
        Assert.That(PngCache.MaximumStore, Is.EqualTo(512L * 1024 * 1024));
        using var cache = new PngCache(root, Policy("normal"));
        Assert.That(cache.CanCapture(Key, PngCache.MaximumEntry), Is.True);
        Assert.That(cache.CanCapture(Key, PngCache.MaximumEntry + 1), Is.False);
        Assert.That(cache.CanCapture(Key, 0), Is.False);
        Assert.That(cache.CanCapture(Key, -1), Is.False);
    }
}
