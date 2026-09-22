// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class TexturePreparationBatchTests
{
    private string root = null!;
    private static PngCache.Entry Entry() => new() { Width = 4, Height = 4, Mips = 3, TextureFormat = 12,
        GraphicsFormat = 100, Filter = 2, Aniso = 2, Pixels = new byte[48] };
    [SetUp] public void Setup() => root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "r2-batch-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [TestCase(2048, 2048)]
    [TestCase(4096, 2304)]
    public void LargeCompressedCandidateIsNotRejectedByRgbaEstimate(int width, int height)
    {
        var header = new PreparationImageHeader { Width = width, Height = height };
        Assert.That(PngCache.ExpectedBytes(width, height, 4, header.FullMips), Is.EqualTo(-1));
        Assert.That(TexturePreparationBatch.NativeAdmission(header), Is.Empty);
        Assert.That(PreparedDds.LayoutBytes(width, height, 10, header.FullMips), Is.LessThan(PreparedTextureStore.MaximumEntry - 65536));
    }

    [Test]
    public void DecodeAndCompleteEntryLimitsRemainDistinct()
    {
        Assert.That(TexturePreparationBatch.NativeAdmission(new PreparationImageHeader { Width = 8192, Height = 8192 }), Does.Contain("decoded"));
        using var store = new PreparedTextureStore(root);
        Assert.That(store.CanPublish("conditional", 5592432), Is.True);
        Assert.That(store.CanPublish("conditional", NativeTextureData.MaximumPixels + 1), Is.False);
        Assert.That(TexturePreparationBatch.NativeAdmission(new PreparationImageHeader { Width = 256, Height = 256, IsDds = true }), Does.Contain("loads natively"));
    }

    [Test]
    public void GuaranteedOversizedExportIsSkippedBeforeHelperWork()
    {
        var large = new PreparationImageHeader { Width = 4096, Height = 4096 };
        Assert.That(TexturePreparationBatch.ExportAdmission(large, 1), Does.Contain("exceeds"));
        Assert.That(TexturePreparationBatch.ExportAdmission(large, 2), Is.Empty);
        Assert.That(TexturePreparationBatch.ExportAdmission(new PreparationImageHeader { Width = 4096, Height = 2304, IsDds = true }, 1), Is.Empty);
    }

    [Test]
    public void MultiplePathsRespectFolderBoundariesAndDoNotDuplicateSelections()
    {
        Assert.That(TexturePreparationBatch.Matches("Textures/Things/A.png", "Textures/Things; Textures/UI/Blue.png"), Is.True);
        Assert.That(TexturePreparationBatch.Matches("Textures/UI/Blue.png", "Textures/Things; Textures/UI/Blue.png"), Is.True);
        Assert.That(TexturePreparationBatch.Matches("Textures/ThingsElse/A.png", "Textures/Things"), Is.False);
        Assert.Throws<InvalidDataException>((Action)(() => TexturePreparationBatch.Matches("x", "../Textures")));
    }

    [Test]
    public void ProviderAndDdsChangesAreDetectedWithoutWholeModRescan()
    {
        string high = Path.Combine(root, "high"), low = Path.Combine(root, "low");
        Directory.CreateDirectory(Path.Combine(low, "Textures")); Directory.CreateDirectory(Path.Combine(high, "Textures"));
        string source = Path.Combine(low, "Textures", "a.png"); File.WriteAllText(source, "low");
        var folders = new[] { high, low };
        Assert.That(TexturePreparationBatch.StillSelected(folders, "Textures/a.png", source), Is.True);
        File.WriteAllText(Path.Combine(high, "Textures", "a.png"), "higher");
        Assert.That(TexturePreparationBatch.StillSelected(folders, "Textures/a.png", source), Is.False);
        File.Delete(Path.Combine(high, "Textures", "a.png"));
        File.WriteAllText(Path.Combine(high, "Textures", "A.DDS"), "authored");
        Assert.That(TexturePreparationBatch.StillSelected(folders, "Textures/a.png", source), Is.False);
    }

    [Test]
    public void NativeBatchPromotesAutomaticBytesAndResumeDoesNoDecode()
    {
        using var automatic = new PngCache(Path.Combine(root, "auto"));
        string key = new string('A', 64); automatic.Write(key, Entry());
        using var store = new PreparedTextureStore(root);
        int captures = 0;
        string Run() => TexturePreparationBatch.Native(store, "source-platform-provider", () => true,
            _ => { captures++; return Entry(); }, CancellationToken.None,
            () => automatic.Read(key), () => automatic.Invalidate(key));
        Assert.That(Run(), Does.Contain("promoted"));
        Assert.That(automatic.Read(key), Is.Null);
        Assert.That(Run(), Does.StartWith("reused"));
        Assert.That(captures, Is.Zero);
        Assert.That(store.Read("source-platform-provider")!.Pixels, Is.EqualTo(Entry().Pixels));
    }

    [Test]
    public void CancelOrInputDriftCannotPublishAndResumeRevalidates()
    {
        using var store = new PreparedTextureStore(root);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>((Action)(() => TexturePreparationBatch.Native(store, "cancel", () => true,
            _ => { cancellation.Cancel(); return Entry(); }, cancellation.Token)));
        Assert.That(store.Read("cancel"), Is.Null);
        bool valid = true;
        Assert.Throws<InvalidDataException>((Action)(() => TexturePreparationBatch.Native(store, "changed", () => valid,
            _ => { valid = false; return Entry(); }, CancellationToken.None)));
        Assert.That(store.Read("changed"), Is.Null);
        Assert.That(TexturePreparationBatch.Native(store, "resumed", () => true, _ => Entry(), CancellationToken.None), Does.StartWith("prepared"));
    }

    [Test]
    public void ExportPublicationUsesDistinctIdentityAndRevalidatesCancellation()
    {
        using var store = new PreparedTextureStore(root);
        byte[] source = { 1, 2, 3 };
        string key = TexturePreparationBatch.ExportIdentity("selection", "Textures/art.dds", Path.Combine(root, "art.dds"), source, 2, "helper-v2");
        byte[] dds = PreparedDds.ToFile(Entry());
        Assert.That(TexturePreparationBatch.PublishExport(store, key, "Textures/art.dds", dds, () => true, CancellationToken.None), Does.StartWith("prepared"));
        Assert.That(store.ReadExport(key), Is.EqualTo(dds));
        Assert.That(store.Read(key), Is.Null, "Portable DDS can never be read as bottom-up native engine output");
        string drift = TexturePreparationBatch.ExportIdentity("selection", "Textures/art.dds", Path.Combine(root, "art.dds"), source, 3, "helper-v2");
        Assert.That(store.ReadExport(drift), Is.Null);
        Assert.Throws<InvalidDataException>((Action)(() => TexturePreparationBatch.PublishExport(store, drift, "Textures/art.dds", dds, () => false, CancellationToken.None)));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>((Action)(() => TexturePreparationBatch.PublishExport(store, drift, "Textures/art.dds", dds, () => true, cancel.Token)));
        Assert.That(store.ReadExport(drift), Is.Null);
    }
}
