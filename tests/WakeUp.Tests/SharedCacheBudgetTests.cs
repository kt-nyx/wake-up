// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class SharedCacheBudgetTests
{
    private string root = null!;
    private static string Key(char c) => new(c, 64);
    private string Category(string name) => Path.Combine(root, "WakeUp", name, "v1");
    private static CacheLaunchPolicy Policy(string action = "normal")
    {
        CacheLaunchPolicy? selected = null;
        return CacheLaunchPolicy.Latch(ref selected, action, () => { });
    }
    private OwnedCacheStore Open(SharedCacheBudget budget, string name, string action = "normal")
        => new(Category(name), 2048, budget.MaximumBytes, Policy(action), sharedBudget: budget);
    [SetUp] public void SetUp() => root = Path.Combine(Path.GetTempPath(), "wake-up-budget-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Test]
    public void UnopenedCategoriesOldVersionsPendingAndExportsCountWithoutBeingDeleted()
    {
        string exports = Path.Combine(Category("PreparedTextures"), "exports");
        Directory.CreateDirectory(exports);
        string file = Path.Combine(exports, "unopened.dds");
        File.WriteAllBytes(file, new byte[300]);
        string legacy = Path.Combine(root, "WakeUp", "PngCache");
        Directory.CreateDirectory(legacy);
        File.WriteAllBytes(Path.Combine(legacy, "old.pending"), new byte[200]);
        using var budget = new SharedCacheBudget(root, 900);
        using var xml = Open(budget, "ParsedXml");
        Assert.That(budget.StoredBytes, Is.EqualTo(500));
        Assert.That(xml.Publish(Key('A'), new byte[100]), Is.True);
        Assert.That(xml.Publish(Key('B'), new byte[100]), Is.False);
        Assert.That(File.ReadAllBytes(file).Length, Is.EqualTo(300));
        Assert.That(budget.StoredBytes, Is.EqualTo(708));
    }

    [Test]
    public void CategoriesBorrowUnusedCapacityAndReleaseItOnClear()
    {
        using var budget = new SharedCacheBudget(root, 2000);
        using (var xml = Open(budget, "ParsedXml"))
        {
            // One category may consume more than a fixed one-third allocation.
            Assert.That(xml.Publish(Key('A'), new byte[1300]), Is.True);
            using var png = Open(budget, "PngCache");
            Assert.That(png.Publish(Key('B'), new byte[500]), Is.False);
        }
        using (var clear = Open(budget, "ParsedXml", "clear")) Assert.That(clear.StoredBytes, Is.Zero);
        using var following = Open(budget, "PngCache");
        Assert.That(following.Publish(Key('B'), new byte[1300]), Is.True);
    }

    [Test]
    public void LanguageStoreSharesTheTotalLimitAndClearReleasesItsSpace()
    {
        using var budget = new SharedCacheBudget(root, 1800);
        using (var language = Open(budget, "ParsedLanguage")) Assert.That(language.Publish(Key('A'), new byte[1100]), Is.True);
        using var xml = Open(budget, "ParsedXml");
        Assert.That(xml.Publish(Key('B'), new byte[600]), Is.False);
        using (var clear = Open(budget, "ParsedLanguage", "clear")) Assert.That(clear.StoredBytes, Is.Zero);
        Assert.That(xml.Publish(Key('B'), new byte[600]), Is.True);
    }

    [Test]
    public void ReplacementAndNestedWriterReserveOldGenerationTemporaryBytesAndMetadata()
    {
        using var budget = new SharedCacheBudget(root, 900);
        using var xml = Open(budget, "ParsedXml");
        using var png = Open(budget, "PngCache");
        Assert.That(xml.Publish(Key('A'), new byte[200]), Is.True);
        bool nested = true;
        Assert.That(xml.Publish(Key('A'), 200, stream =>
        {
            nested = png.Publish(Key('B'), new byte[200]);
            stream.Write(new byte[200], 0, 200);
        }), Is.True);
        Assert.That(nested, Is.False);
        Assert.That(budget.StoredBytes, Is.EqualTo(308));
        xml.Complete();
        Assert.That(budget.StoredBytes, Is.EqualTo(Directory.GetFiles(Category("ParsedXml")).Sum(p => new FileInfo(p).Length)));
    }

    [Test]
    public void SimultaneousCategoryWritersCannotBothUseSameHeadroom()
    {
        using var budget = new SharedCacheBudget(root, 1000);
        using var xml = Open(budget, "ParsedXml");
        using var png = Open(budget, "PngCache");
        var writes = new[] { Task.Run(() => xml.Publish(Key('A'), new byte[500])), Task.Run(() => png.Publish(Key('B'), new byte[500])) };
        Task.WaitAll(writes);
        Assert.That(writes.Count(t => t.Result), Is.EqualTo(1));
        Assert.That(budget.StoredBytes, Is.EqualTo(608));
    }

    [Test]
    public void ExternalPayloadMappingAndFailedTemporaryCleanupStayVisibleAcrossCategories()
    {
        using var budget = new SharedCacheBudget(root, 1200);
        long external = 0;
        using var prepared = new OwnedCacheStore(Category("PreparedTextures"), 2048, 1200, Policy(), () => external,
            sharedBudget: budget);
        using var xml = Open(budget, "ParsedXml");
        Assert.That(prepared.PublishExternal(800, () => { external = 800; return true; }), Is.True);
        Assert.That(xml.Publish(Key('A'), new byte[200]), Is.False);
        Assert.Throws<IOException>((Action)(() => prepared.PublishExternal(200, () => { external += 200; throw new IOException("pending-cleanup"); })));
        Assert.That(budget.StoredBytes, Is.EqualTo(1000));
        Assert.That(xml.Publish(Key('A'), new byte[1]), Is.False);
    }

    [Test]
    public void SharedAndLegacyCategoryOwnersRemainExclusiveEvenWhenUnopenedOrDetached()
    {
        using (var budget = new SharedCacheBudget(root, 4096))
        {
            Assert.Throws<IOException>((Action)(() => { using var duplicate = new SharedCacheBudget(root, 4096); }));
            Assert.Throws<IOException>((Action)(() => { using var legacy = new OwnedCacheStore(Category("PngCache"), 2048, 4096, Policy()); }));
            using (var xml = Open(budget, "ParsedXml"))
                Assert.Throws<IOException>((Action)(() => { using var duplicate = Open(budget, "ParsedXml"); }));
            Assert.Throws<IOException>((Action)(() => { using var legacy = new OwnedCacheStore(Category("ParsedXml"), 2048, 4096, Policy()); }));
        }
        using var released = new OwnedCacheStore(Category("ParsedXml"), 2048, 4096, Policy());
    }

    [Test]
    public void BypassDoesNotAttachAndRebuildDoesNotReadPreviousEntry()
    {
        using var budget = new SharedCacheBudget(root, 4096);
        using (var seed = Open(budget, "ParsedXml")) Assert.That(seed.Publish(Key('A'), new byte[] { 1 }), Is.True);
        using (var bypass = Open(budget, "ParsedXml", "bypass"))
        {
            Assert.That(bypass.Read(Key('A')), Is.Null);
            Assert.That(bypass.Publish(Key('A'), new byte[] { 2 }), Is.False);
        }
        using var rebuild = Open(budget, "ParsedXml", "rebuild");
        Assert.That(rebuild.Read(Key('A')), Is.Null);
        Assert.That(rebuild.Publish(Key('A'), new byte[] { 2 }), Is.True);
    }
}
