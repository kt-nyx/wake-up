// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class WarmSourceSizeTests
{
    private string root = null!;
    [SetUp] public void Setup()
    {
        root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "warm-size-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }
    [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    public void EnumerationSizeAvoidsRefreshAndWorkerChecksActualSourceBeforeAllocation(int actualBytes)
    {
        string path = Path.Combine(root, "image.png");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        // This is the same native DirectoryInfo.GetFiles shape used by GOG.
        FileInfo selected = new DirectoryInfo(root).GetFiles("*.*", SearchOption.AllDirectories).Single();
        if (actualBytes < 0) File.Delete(path);
        else { File.WriteAllBytes(path, Enumerable.Repeat((byte)9, actualBytes).ToArray()); File.SetLastWriteTimeUtc(path, timestamp); }
        Assert.That(WarmSourceSize.TryGet(selected, out long hint), Is.True);
        Assert.That(hint, Is.EqualTo(4), "Even a now-missing file keeps its enumeration hint: no metadata refresh occurred.");
        bool decoded = false;
        long reservation = FirstBuildImageQueue<byte[]>.RowOverheadBytes + hint + 24;
        using var queue = new FirstBuildImageQueue<byte[]>(new[] { selected },
            _ => throw new InvalidOperationException("Warm hints do not use the C06 estimator."),
            (_, _) => throw new InvalidOperationException("Use the warm recipe."), maxReservedBytes: reservation,
            unopenedPlan: _ => new FirstBuildImageQueue<byte[]>.WorkItem(24,
                (source, _) => { decoded = true; return source; }, expectedSourceBytes: hint, retainAcrossCallbacks: false));
        Assert.That(queue.TryTake(0, out var lease), Is.True);
        Assert.That(queue.MaxReservedBytes, Is.EqualTo(reservation));
        if (actualBytes == 4)
        {
            Assert.That(lease!.Error, Is.Null);
            Assert.That(lease.Source, Is.EqualTo(new byte[] { 9, 9, 9, 9 }), "Equal metadata never supplies old source contents.");
            Assert.That(decoded, Is.True);
        }
        else
        {
            if (actualBytes < 0) Assert.That(lease!.Error, Is.InstanceOf<IOException>());
            else Assert.That(lease!.Error, Is.TypeOf<InvalidDataException>());
            Assert.That(lease.Source, Is.Empty, "Stale longer/shorter hints cannot allocate an undercharged source.");
            Assert.That(lease.SourceComplete, Is.False);
            Assert.That(decoded, Is.False);
        }
        queue.SuspendAfter(0);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.OpenStreams, Is.Zero);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        lease!.Dispose();
    }

    [Test]
    public void ProviderCreatedFileUsesOrdinaryLengthAndMissingOrUnsupportedHintsRefusePerFile()
    {
        string path = Path.Combine(root, "provider.png");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        Assert.That(WarmSourceSize.TryGet(new FileInfo(path), out long hint), Is.True);
        Assert.That(hint, Is.EqualTo(3));
        Assert.That(WarmSourceSize.TryGet(new FileInfo(Path.Combine(root, "missing.png")), out _), Is.False);
        Assert.That(WarmSourceSize.TryGet(new DirectoryInfo(root), out _), Is.False);
        Assert.That(WarmSourceSize.TryGet(null, out _), Is.False);
        // An unsupported/missing hint does not poison subsequent admissions.
        Assert.That(WarmSourceSize.TryGet(new FileInfo(path), out hint), Is.True);
        Assert.That(hint, Is.EqualTo(3));
    }
}
