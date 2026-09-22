// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PreparedTextureExportTests
{
    private string root = null!;
    private static CacheLaunchPolicy Policy(string action)
    { CacheLaunchPolicy? selected = null; return CacheLaunchPolicy.Latch(ref selected, action, () => { }); }
    private static byte[] Dds(byte value = 0)
    {
        var pixels = new byte[8]; pixels[0] = value;
        return PreparedDds.ToFile(new PngCache.Entry { Width = 4, Height = 4, TextureFormat = 10, Mips = 1, Pixels = pixels });
    }
    private static PngCache.Entry Native() => new() { Width = 2, Height = 2, TextureFormat = 4, GraphicsFormat = 8,
        Mips = 1, Pixels = new byte[16] };
    [SetUp] public void SetUp() => root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "export-test-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Test]
    public void ReusableDdsAndMappingRoundTripWithoutNativeEnvelope()
    {
        const string identity = "source-digest|provider-order|preset-version";
        string path;
        using (var store = new PreparedTextureStore(root, Policy("normal")))
        {
            byte[] original = Dds(42);
            Assert.That(store.PublishExport(identity, "Items/Blue hat", original), Is.True);
            path = store.ExportRoot;
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.bin"), Is.Empty);
            Assert.That(File.ReadAllBytes(Directory.GetFiles(path, "*.dds").Single()), Is.EqualTo(original));
            string[] mapping = File.ReadAllLines(Directory.GetFiles(path, "*.manifest").Single());
            Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(mapping[1])), Is.EqualTo(identity));
            Assert.That(mapping[2], Is.EqualTo("Items/Blue hat"));
            Assert.That(store.StoredBytes, Is.EqualTo(Directory.GetFiles(path).Sum(f => new FileInfo(f).Length)));
            store.ReadExport(identity)![128] = 10;
            Assert.That(store.ReadExport(identity)![128], Is.EqualTo(42));
            Assert.That(store.Read(identity), Is.Null, "quality export is never a native prepared-cache hit");
        }
        using var resumed = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(resumed.ReadExport(identity), Is.EqualTo(Dds(42)));
        Assert.That(resumed.ReadExport(identity + "|changed-source"), Is.Null);
        Assert.That(resumed.ReadExport(identity.Replace("preset-version", "changed-preset")), Is.Null);
    }

    [Test]
    public void ExportAndNativeShareCapacityIncludingReplacementTemporaryBytes()
    {
        long nativeSize;
        using (var native = new PreparedTextureStore(root, Policy("normal")))
        { Assert.That(native.Publish("native", Native()), Is.True); nativeSize = native.StoredBytes; }
        PreparedTextureStore.Maintain(root, Policy("clear"));
        long exportSize;
        using (var measure = new PreparedTextureStore(root, Policy("normal")))
        { Assert.That(measure.PublishExport("identity", "Item", Dds()), Is.True); exportSize = measure.StoredBytes; }
        PreparedTextureStore.Maintain(root, Policy("clear"));
        using var limited = new PreparedTextureStore(root, Policy("normal"), exportSize + nativeSize + 96);
        Assert.That(limited.Publish("native", Native()), Is.True);
        Assert.That(limited.PublishExport("identity", "Item", Dds()), Is.True);
        Assert.That(limited.PublishExport("identity", "Item", Dds(7)), Is.False, "the previous committed output occupies capacity until replacement commits");
        Assert.That(limited.ExportLastReason, Is.EqualTo("export-quota"));
        Assert.That(limited.ReadExport("identity"), Is.EqualTo(Dds()));
        Assert.That(limited.CanPublish("other-native", 16), Is.False);
        limited.Complete();
        Assert.That(limited.StoredBytes, Is.LessThanOrEqualTo(exportSize + nativeSize + 96));
    }

    [Test]
    public void CorruptDdsAndMappingFailClosedAndAreReclaimable()
    {
        using var store = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(store.PublishExport("identity", "Item", Dds()), Is.True);
        string dds = Directory.GetFiles(store.ExportRoot, "*.dds").Single();
        byte[] changed = File.ReadAllBytes(dds); changed[128] ^= 1; File.WriteAllBytes(dds, changed);
        Assert.That(store.ReadExport("identity"), Is.Null);
        Assert.That(store.ExportLastReason, Is.EqualTo("export-digest"));
        Assert.That(store.StoredBytes, Is.Zero);
        Assert.That(store.PublishExport("identity", "Item", Dds()), Is.True);
        string manifest = Directory.GetFiles(store.ExportRoot, "*.manifest").Single();
        string[] fields = File.ReadAllLines(manifest); fields[3] = "../../source.dds"; File.WriteAllLines(manifest, fields);
        Assert.That(store.ReadExport("identity"), Is.Null);
        Assert.That(Directory.GetFiles(store.ExportRoot), Is.Empty);
    }

    [Test]
    public void InterruptedFilesAreReclaimedWithoutDeletingCommittedExportOrOtherCategory()
    {
        string exportRoot;
        using (var seed = new PreparedTextureStore(root, Policy("normal")))
        { Assert.That(seed.PublishExport("identity", "Item", Dds()), Is.True); exportRoot = seed.ExportRoot; }
        string key = new string('A', 64);
        File.WriteAllBytes(Path.Combine(exportRoot, key + "." + Guid.NewGuid().ToString("N") + ".pending"), new byte[23]);
        File.WriteAllBytes(Path.Combine(exportRoot, key + "." + new string('B', 64) + ".dds"), Dds());
        string unrelated = Path.Combine(root, "original.dds"); File.WriteAllText(unrelated, "original");
        using (var resumed = new PreparedTextureStore(root, Policy("normal")))
        {
            Assert.That(resumed.ReadExport("identity"), Is.EqualTo(Dds()));
            Assert.That(Directory.GetFiles(exportRoot, "*.pending"), Is.Empty);
            Assert.That(Directory.GetFiles(exportRoot, "*.dds").Length, Is.EqualTo(1));
        }
        PreparedTextureStore.Maintain(root, Policy("clear"));
        Assert.That(Directory.GetFiles(exportRoot), Is.Empty);
        Assert.That(File.ReadAllText(unrelated), Is.EqualTo("original"));
    }

    [Test]
    public void BypassCreatesNothingAndCategoryOwnerIsExclusive()
    {
        using (var bypass = new PreparedTextureStore(root, Policy("bypass")))
        { Assert.That(bypass.PublishExport("identity", "Item", Dds()), Is.False); Assert.That(bypass.ReadExport("identity"), Is.Null); }
        Assert.That(Directory.Exists(root), Is.False);
        using var owned = new PreparedTextureStore(root, Policy("normal"));
        Assert.Throws<IOException>((Action)(() => { using var conflict = new PreparedTextureStore(root, Policy("normal")); }));
    }

    [TestCase("../Item")]
    [TestCase("Items//Item")]
    [TestCase("C:/Item")]
    [TestCase("Items\\Item")]
    [TestCase("Items/\nItem")]
    public void UnsafeMappingCannotPublish(string logical)
    {
        using var store = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(store.PublishExport("identity", logical, Dds()), Is.False);
        Assert.That(store.StoredBytes, Is.Zero);
    }

    [Test]
    public void UnsupportedOrMalformedOutputCannotBecomeReusableFile()
    {
        using var store = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(store.PublishExport("identity", "Item", new byte[136]), Is.False);
        Assert.That(store.PublishExport(new string('\u00e9', PreparedTextureStore.MaximumIdentityBytes), "Item", Dds()), Is.False);
        Assert.That(store.StoredBytes, Is.Zero);
    }
}
