// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class AtlasCacheStoreTests
{
    private string root = null!;
    private string StoreRoot => Path.Combine(root, "WakeUp", "StaticAtlases", "v1");
    private string GroupRoot => Path.Combine(StoreRoot, "groups");
    private static readonly string Identity = new('A', 64);
    private static CacheLaunchPolicy Policy(string action)
    {
        CacheLaunchPolicy? selected = null;
        return CacheLaunchPolicy.Latch(ref selected, action, () => { });
    }
    private static PngCache.Entry Image(byte seed) => new()
    {
        Width = 2, Height = 2, TextureFormat = 4, GraphicsFormat = 8, Mips = 2,
        Filter = 2, WrapU = 1, WrapV = 2, WrapW = 0, Aniso = 2, Bias = 0.25f,
        Readable = true, Pixels = Enumerable.Range(0, 20).Select(i => (byte)(i + seed)).ToArray()
    };
    private static AtlasPixels.Image CapturedImage(byte seed) => new()
    {
        Gpu = Image(seed), Cpu = Image((byte)(seed + 50)).Pixels,
        MinimumMipmapLevel = 1, RequestedMipmapLevel = 1
    };
    private static AtlasCacheStore.Entry Entry() => new()
    {
        Color = CapturedImage(1), Mask = CapturedImage(51), Rects = new[] { 0f, 0f, .5f, 1f, .5f, 0f, .5f, 1f }
    };
    [SetUp] public void SetUp() => root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "wake-up-atlas-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void CompleteRoundTripPreservesBothImagesMipsSamplingReadabilityAndOrderedRects(bool compress, bool batched)
    {
        var expected = Entry(); expected.Mask!.Gpu.Readable = false; expected.Mask.Cpu = null;
        using (var cache = new AtlasCacheStore(root, Policy("normal"), compress: compress, batched: batched))
        {
            Assert.That(cache.Publish(Identity, expected), Is.True);
            expected.Color.Gpu.Pixels[0] = 255; expected.Rects[0] = .25f;
            cache.Complete();
        }
        using var opened = new AtlasCacheStore(root, Policy("normal"));
        var restored = opened.Read(Identity, 2)!;
        Assert.That(restored, Is.Not.Null);
        var reference = Entry(); reference.Mask!.Gpu.Readable = false; reference.Mask.Cpu = null;
        Assert.That(NativeTextureData.Encode(Identity, restored.Color.Gpu), Is.EqualTo(NativeTextureData.Encode(Identity, reference.Color.Gpu)));
        Assert.That(NativeTextureData.Encode(Identity, restored.Mask!.Gpu), Is.EqualTo(NativeTextureData.Encode(Identity, reference.Mask.Gpu)));
        Assert.That(restored.Rects, Is.EqualTo(reference.Rects));
        Assert.That(restored.Color.Cpu, Is.EqualTo(reference.Color.Cpu));
        Assert.That(restored.Color.Cpu, Is.Not.EqualTo(restored.Color.Gpu.Pixels));
        Assert.That(restored.Color.MinimumMipmapLevel, Is.EqualTo(1));
        Assert.That(restored.Color.RequestedMipmapLevel, Is.EqualTo(1));
        restored.Color.Gpu.Pixels[0] = 254; restored.Mask!.Gpu.Pixels[0] = 254; restored.Rects[0] = .2f;
        Assert.That(opened.Read(Identity)!.Color.Gpu.Pixels[0], Is.EqualTo(1));
        Assert.That(opened.Read(Identity)!.Mask!.Gpu.Pixels[0], Is.EqualTo(51));
        Assert.That(opened.Read(Identity)!.Rects[0], Is.Zero);
        Assert.That(opened.StoredBytes, Is.EqualTo(Directory.GetFiles(StoreRoot, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length)));
    }

    [Test]
    public void MaskAbsenceAndCompleteIdentityArePreserved()
    {
        using var cache = new AtlasCacheStore(root, Policy("normal"));
        var entry = Entry(); entry.Mask = null;
        Assert.That(cache.Publish(Identity, entry), Is.True);
        Assert.That(cache.Read(Identity)!.Mask, Is.Null);
        Assert.That(cache.Read(new string('B', 64)), Is.Null);
        Assert.That(cache.Read(Identity, 3), Is.Null);
        Assert.That(cache.LastReason, Is.EqualTo("atlas-tile-count"));
    }

    [Test]
    public void MaskMayHaveItsOwnMipChainAndMipSelectionSentinels()
    {
        var entry = Entry();
        entry.Mask!.Gpu.Mips = 1; entry.Mask.Gpu.Pixels = new byte[16]; entry.Mask.Cpu = new byte[16];
        entry.Mask.MinimumMipmapLevel = -1; entry.Mask.RequestedMipmapLevel = 31;
        using var cache = new AtlasCacheStore(root, Policy("normal"));
        Assert.That(cache.Publish(Identity, entry), Is.True);
        var result = cache.Read(Identity)!;
        Assert.That(result.Color.Gpu.Mips, Is.EqualTo(2));
        Assert.That(result.Mask!.Gpu.Mips, Is.EqualTo(1));
        Assert.That(result.Mask.MinimumMipmapLevel, Is.EqualTo(-1));
        Assert.That(result.Mask.RequestedMipmapLevel, Is.EqualTo(31));
    }

    [TestCase("nan")]
    [TestCase("infinite")]
    [TestCase("negative")]
    [TestCase("zero")]
    [TestCase("outside")]
    [TestCase("incomplete")]
    [TestCase("empty")]
    [TestCase("mask-size")]
    [TestCase("mips")]
    [TestCase("cpu-size")]
    [TestCase("cpu-missing")]
    [TestCase("unreadable-cpu")]
    [TestCase("mip-selection")]
    public void InvalidMappingOrNativeImageCannotPublish(string failure)
    {
        var entry = Entry();
        switch (failure)
        {
            case "nan": entry.Rects[0] = float.NaN; break;
            case "infinite": entry.Rects[0] = float.PositiveInfinity; break;
            case "negative": entry.Rects[0] = -.01f; break;
            case "zero": entry.Rects[2] = 0; break;
            case "outside": entry.Rects[0] = .75f; break;
            case "incomplete": entry.Rects = new float[5]; break;
            case "empty": entry.Rects = Array.Empty<float>(); break;
            case "mask-size": entry.Mask!.Gpu.Width = 1; entry.Mask.Gpu.Pixels = new byte[12]; break;
            case "mips": entry.Color.Gpu.Mips = 3; break;
            case "cpu-size": entry.Color.Cpu = new byte[1]; break;
            case "cpu-missing": entry.Color.Cpu = null; break;
            case "unreadable-cpu": entry.Color.Gpu.Readable = false; break;
            case "mip-selection": entry.Color.MinimumMipmapLevel = -2; break;
        }
        using var cache = new AtlasCacheStore(root, Policy("normal"));
        Assert.That(cache.Publish(Identity, entry), Is.False);
        Assert.That(cache.Read(Identity), Is.Null);
        Assert.That(cache.StoredBytes, Is.Zero);
    }

    [Test]
    public void CorruptionNeverReturnsHalfAGroupAndRebuildWorks()
    {
        using var cache = new AtlasCacheStore(root, Policy("normal"));
        Assert.That(cache.Publish(Identity, Entry()), Is.True);
        string file = Directory.GetFiles(GroupRoot, "*.group").Single();
        byte[] bytes = File.ReadAllBytes(file); bytes[bytes.Length - 1] ^= 1; File.WriteAllBytes(file, bytes);
        Assert.That(cache.Read(Identity), Is.Null);
        Assert.That(cache.Hits, Is.Zero);
        Assert.That(cache.Publish(Identity, Entry()), Is.True);
        Assert.That(cache.Read(Identity)!.Mask!.Gpu.Pixels, Is.EqualTo(Image(51).Pixels));
    }

    [TestCase("mapping")]
    [TestCase("count")]
    [TestCase("length")]
    [TestCase("identity")]
    [TestCase("trailing")]
    public void InnerValidationRejectsMalformedPayloadEvenWithValidGroupedDigest(string change)
    {
        byte[] bytes = AtlasCacheStore.Encode(Identity, Entry());
        if (change == "trailing") bytes = bytes.Concat(new byte[1]).ToArray();
        else if (change == "identity") bytes[8] = (byte)'B';
        else
        {
            int offset = change == "mapping" ? bytes.Length - 32 : change == "count" ? 72 : 76;
            byte[] value = change == "mapping" ? BitConverter.GetBytes(float.NaN) : BitConverter.GetBytes(int.MaxValue);
            Buffer.BlockCopy(value, 0, bytes, offset, 4);
        }
        Assert.Throws<InvalidDataException>((Action)(() => AtlasCacheStore.Decode(Identity, bytes)));
    }

    [Test]
    public void QuotaIncludesPrivateReplacementAndCancelledPublicationKeepsExistingGroup()
    {
        using (var small = new AtlasCacheStore(root, Policy("normal"), maximumStore: 200))
        {
            Assert.That(small.Publish(Identity, Entry()), Is.False);
            Assert.That(small.StoredBytes, Is.Zero);
        }
        using var cache = new AtlasCacheStore(root, Policy("normal"), maximumStore: 64 * 1024);
        Assert.That(cache.Publish(Identity, Entry(), ownerKey: new string('C', 64)), Is.True);
        var changed = Entry(); changed.Color.Gpu.Pixels[0] = 200;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.That(cache.Publish(Identity, changed, cancellation: cancelled.Token), Is.False);
        Assert.That(cache.Read(Identity)!.Color.Gpu.Pixels[0], Is.EqualTo(1));
        Assert.That(cache.StoredBytes, Is.LessThanOrEqualTo(64 * 1024));
    }

    [Test]
    public void StableOwnerReplacementRetiresOldIdentityAsOneCompleteGroup()
    {
        string replacement = new string('B', 64), owner = new string('C', 64);
        using (var cache = new AtlasCacheStore(root, Policy("normal"), maximumStore: 64 * 1024))
        {
            Assert.That(cache.Publish(Identity, Entry(), owner), Is.True);
            var changed = Entry(); changed.Color.Gpu.Pixels[0] = 199; changed.Mask!.Gpu.Pixels[0] = 198;
            Assert.That(cache.Publish(replacement, changed, owner), Is.True);
            cache.Complete();
        }
        using var reopened = new AtlasCacheStore(root, Policy("normal"));
        Assert.That(reopened.Read(Identity), Is.Null);
        Assert.That(reopened.Read(replacement)!.Color.Gpu.Pixels[0], Is.EqualTo(199));
        Assert.That(reopened.Read(replacement)!.Mask!.Gpu.Pixels[0], Is.EqualTo(198));
    }

    [Test]
    public void BypassDoesNotReadOrWriteAndClearMaintenanceWorksWhenFeatureIsOff()
    {
        using (var cache = new AtlasCacheStore(root, Policy("normal")))
        { Assert.That(cache.Publish(Identity, Entry()), Is.True); cache.Complete(); }
        string authored = Path.Combine(root, "source.dds"); File.WriteAllText(authored, "authored");
        var before = Directory.GetFiles(GroupRoot).ToDictionary(p => p, File.ReadAllBytes);
        using (var bypass = new AtlasCacheStore(root, Policy("bypass")))
        {
            Assert.That(bypass.Read(Identity), Is.Null);
            Assert.That(bypass.Publish(Identity, Entry()), Is.False);
            bypass.Invalidate(Identity); bypass.Complete();
        }
        foreach (var item in before) Assert.That(File.ReadAllBytes(item.Key), Is.EqualTo(item.Value));
        AtlasCacheStore.Maintain(root, Policy("clear"));
        Assert.That(File.ReadAllText(authored), Is.EqualTo("authored"));
        using var cleared = new AtlasCacheStore(root, Policy("normal"));
        Assert.That(cleared.Read(Identity), Is.Null);
        Assert.That(cleared.StoredBytes, Is.Zero);
    }
}
