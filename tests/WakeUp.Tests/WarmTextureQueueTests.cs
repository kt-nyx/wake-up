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
public sealed class WarmTextureQueueTests
{
    private string root = null!;
    private sealed class Result
    {
        internal byte[]? Cold;
        internal PreparedTextureStore.ReadPlan? Plan;
        internal PreparedTextureStore.ReadOutcome? Warm;
    }
    [SetUp]
    public void Setup()
    {
        root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "warm-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }
    [TearDown]
    public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private static CacheLaunchPolicy Policy()
    {
        CacheLaunchPolicy? selected = null;
        return CacheLaunchPolicy.Latch(ref selected, "normal", () => { });
    }
    private FileInfo Source(int index, byte value)
    {
        string path = Path.Combine(root, index + ".png");
        File.WriteAllBytes(path, new[] { value, value, value, value });
        return new FileInfo(path);
    }
    private static string Logical(int index) => "Textures/" + index + ".png";
    private static string Owner(int index) => PreparedTextureRuntime.OwnerIdentity("selection", Logical(index), 0);
    private static string Identity(FileInfo file, int index, byte[] source)
        => PreparedTextureRuntime.Identity("selection", Logical(index), file.FullName, source, 0, "platform", "");
    private static PngCache.Entry Entry(byte value)
    {
        var entry = new PngCache.Entry
        {
            Width = 2, Height = 2, TextureFormat = 4, GraphicsFormat = 8, Mips = 2,
            Filter = 2, WrapU = 1, WrapV = 2, WrapW = 0, Aniso = 2, Bias = 0.25f,
            Pixels = new byte[20]
        };
        entry.Pixels[0] = value;
        return entry;
    }
    private static FirstBuildImageQueue<Result>.WorkItem WarmRecipe(
        FileInfo file, int index, PreparedTextureStore.ReadPlan plan)
    {
        var identity = PreparedTextureRuntime.NativeIdentityReader(Owner(index), "selection", Logical(index), file.FullName, "platform");
        return new FirstBuildImageQueue<Result>.WorkItem(plan.ExtraLiveBytes, (source, token) => new Result
        {
            Plan = plan, Warm = plan.Read(identity(source), token)
        });
    }

    [Test]
    public void WarmEngineCallbackTargetsIncludeEveryPlatformAndRestoreEntryPoint()
    {
        var targets = QualityUnityCallbacks.Targets().ToArray();
        Assert.That(targets, Has.None.Null);
        foreach (string name in new[] { "IsSRGBFormat", "IsFormatSupported", "get_graphicsDeviceType",
            "get_graphicsDeviceVersion", "get_graphicsDeviceVendorID", "get_graphicsDeviceID",
            "get_unityVersion", "get_activeColorSpace", "get_TextureCompression", "get_ComputeShadersSupported" })
            Assert.That(targets.Any(t => t.Name == name), Is.True, name);
    }

    [TestCase("png")]
    [TestCase("jpg")]
    [TestCase("jpeg")]
    [TestCase("PsD")]
    [TestCase("dds")]
    public void CapturedWorkerIdentityMatchesNativeContractAndHashesTheWholeSource(string extension)
    {
        const string selection = "provider|with:framing";
        string logical = "Textures/é-sample." + extension;
        string path = Path.Combine(root, "unused", "..", "sample." + extension);
        const string platform = "GOG|platform:with|framing";
        string owner = PreparedTextureRuntime.OwnerIdentity(selection, logical, 0);
        var reader = PreparedTextureRuntime.NativeIdentityReader(owner, selection, logical, path, platform);
        var source = new byte[1024]; source[0] = 1; source[source.Length - 1] = 2;
        string expected = PreparedTextureRuntime.Identity(selection, logical, path, source, 0, platform, "ignored-native-helper");
        string actual = Task.Run(() => reader(source)).GetAwaiter().GetResult();
        Assert.That(actual, Is.EqualTo(expected));
        source[source.Length - 1] = 3;
        string changed = Task.Run(() => reader(source)).GetAwaiter().GetResult();
        Assert.That(changed, Is.Not.EqualTo(actual));
        Assert.That(changed, Is.EqualTo(PreparedTextureRuntime.Identity(selection, logical, path, source, 0, platform, "")));
    }

    [Test]
    public void SameLengthAndTimestampSourceChangeRejectsWarmIdentity()
    {
        FileInfo file = Source(0, 1);
        DateTime timestamp = File.GetLastWriteTimeUtc(file.FullName);
        string originalIdentity = Identity(file, 0, File.ReadAllBytes(file.FullName));
        using var store = new PreparedTextureStore(root, Policy(), compress: true);
        Assert.That(store.Publish(originalIdentity, Entry(41)), Is.True);
        File.WriteAllBytes(file.FullName, new byte[] { 2, 2, 2, 2 });
        File.SetLastWriteTimeUtc(file.FullName, timestamp);
        Assert.That(new FileInfo(file.FullName).Length, Is.EqualTo(4));
        Assert.That(File.GetLastWriteTimeUtc(file.FullName), Is.EqualTo(timestamp));
        using var queue = new FirstBuildImageQueue<Result>(new[] { file }, _ => 4,
            (_, _) => throw new InvalidOperationException("Warm recipe expected."),
            plan: (index, _) => WarmRecipe(file, index, store.PlanNative(Owner(index))!));
        Assert.That(queue.TryTake(0, out var lease), Is.True);
        Assert.That(lease!.Error, Is.Null);
        var result = lease.Value!;
        Assert.That(result.Warm!.Reason, Is.EqualTo("prepared-source-miss"));
        Assert.That(store.CompleteRead(result.Plan!, result.Warm), Is.Null);
        Assert.That(store.Hits, Is.Zero);
        Assert.That(store.Misses, Is.EqualTo(1));
        Assert.That(store.Errors, Is.Zero);
        Assert.That(queue.Consumed, Is.Zero);
        lease.Dispose();
        Assert.That(store.Read(originalIdentity)!.Pixels[0], Is.EqualTo(41),
            "An unchanged size and timestamp cannot hide source changes or invalidate the older stored record.");
    }

    [Test]
    public void CallbackAfterDrainReplacesSourceAndReplansWithoutRecreatingWorkers()
    {
        var files = new[] { Source(0, 1), Source(1, 2) };
        using var store = new PreparedTextureStore(root, Policy());
        for (int i = 0; i < files.Length; i++)
            Assert.That(store.Publish(Identity(files[i], i, File.ReadAllBytes(files[i].FullName)), Entry((byte)(40 + i))), Is.True);
        int futurePlans = 0;
        using var queue = new FirstBuildImageQueue<Result>(files, _ => 4,
            (_, _) => throw new InvalidOperationException("Warm recipe expected."),
            plan: (index, _) =>
            {
                if (index == 1) futurePlans++;
                return WarmRecipe(files[index], index, store.PlanNative(Owner(index))!);
            });
        Assert.Throws<ArgumentOutOfRangeException>((Action)(() => queue.DrainAfter(0)));
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(first!.Error, Is.Null);
        var firstValue = first.Value!;
        Assert.That(store.CompleteRead(firstValue.Plan!, firstValue.Warm!)!.Pixels[0], Is.EqualTo(40));
        first.MarkConsumed();
        Assert.Throws<ArgumentOutOfRangeException>((Action)(() => queue.DrainAfter(1)));
        queue.DrainAfter(0);
        queue.DrainAfter(0); // nested publication preflights can repeat the boundary
        Assert.That(first.Released, Is.True);
        Assert.That(first.Value, Is.Null);
        Assert.That(queue.ActiveWorkers, Is.Zero);
        Assert.That(queue.HeldBytes, Is.Zero);
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        Assert.That(queue.Consumed, Is.EqualTo(1), "Discarded future work is not consumed.");
        Assert.Throws<InvalidOperationException>((Action)(() => first.MarkConsumed()));
        first.Dispose(); // the enumerator's existing using remains safe
        File.WriteAllBytes(files[1].FullName, new byte[] { 9, 9, 9, 9 });
        Assert.That(store.Publish(Identity(files[1], 1, File.ReadAllBytes(files[1].FullName)), Entry(99)), Is.True);
        Assert.That(queue.TryTake(1, out var second), Is.True);
        Assert.That(second!.Error, Is.Null);
        Assert.That(second.Source, Is.EqualTo(new byte[] { 9, 9, 9, 9 }));
        var secondValue = second.Value!;
        Assert.That(store.CompleteRead(secondValue.Plan!, secondValue.Warm!)!.Pixels[0], Is.EqualTo(99));
        second.MarkConsumed(); second.Dispose();
        Assert.That(futurePlans, Is.EqualTo(2), "The discarded future source gets a new store plan.");
        Assert.That(queue.Scheduled, Is.EqualTo(3));
        Assert.That(store.Hits, Is.EqualTo(2));
    }

    [TestCase("reset")]
    [TestCase("invalidate")]
    [TestCase("dispose")]
    public void StoreMutationWaitsForActiveQueueReadBeforeInvalidatingItsPlan(string mutation)
    {
        var files = new[] { Source(0, 1), Source(1, 2) };
        using var store = new PreparedTextureStore(root, Policy(), compress: true);
        string identity = Identity(files[1], 1, File.ReadAllBytes(files[1].FullName));
        Assert.That(store.Publish(identity, Entry(42)), Is.True);
        var plan = store.PlanNative(Owner(1))!;
        using var entered = new ManualResetEventSlim();
        using var continueRead = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        PreparedTextureStore.ReadOutcome? completed = null;
        using var queue = new FirstBuildImageQueue<Result>(files, _ => 4,
            (source, _) => new Result { Cold = source }, plan: (index, _) => index == 0
                ? new FirstBuildImageQueue<Result>.WorkItem(4, (source, _) => new Result { Cold = source })
                : new FirstBuildImageQueue<Result>.WorkItem(plan.ExtraLiveBytes, (source, token) =>
                {
                    entered.Set();
                    try
                    {
                        if (!continueRead.Wait(5000, token)) throw new InvalidOperationException("Mutation did not drain.");
                        completed = plan.Read(Identity(files[1], 1, source), token);
                        return new Result { Plan = plan, Warm = completed };
                    }
                    finally { exited.Set(); }
                }));
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(entered.Wait(5000), Is.True);
        bool drained = false;
        Action drain = () =>
        {
            // The owner lock must remain free while joining worker work.
            var probe = Task.Run(() => store.HasOwner(Owner(1)));
            Assert.That(probe.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(probe.Result, Is.True);
            continueRead.Set();
            queue.DrainAfter(0);
            Assert.That(exited.IsSet, Is.True);
            Assert.That(completed, Is.Not.Null);
            Assert.That(completed!.Cancelled, Is.False, "The store lifetime remains valid until workers finish.");
            Assert.That(completed.Result, Is.Not.Null);
            Assert.That(queue.ActiveWorkers, Is.Zero);
            Assert.That(queue.HeldBytes, Is.Zero);
            Assert.That(first!.Released, Is.True);
            File.WriteAllBytes(files[1].FullName, new byte[] { 8, 8, 8, 8 });
            drained = true;
        };
        store.BeforeMutation += drain;
        try
        {
            if (mutation == "reset") store.ResetQuality("selection", Logical(1));
            else if (mutation == "invalidate") store.Invalidate(identity);
            else store.Dispose();
        }
        finally { store.BeforeMutation -= drain; }
        Assert.That(drained, Is.True);
        Assert.That(plan.Read(identity, CancellationToken.None).Cancelled, Is.True);
        Assert.That(store.CompleteRead(plan, completed!), Is.Null);
        Assert.That(store.Hits, Is.Zero);
        first!.Dispose();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ColdAndWarmRecipesShareOneWorkerAndMemoryBudgetWithOrderedAccounting(bool ownedRecord)
    {
        var files = new[] { Source(0, 1), Source(1, 2) };
        using var store = new PreparedTextureStore(root, Policy(), compress: true);
        Assert.That(store.Publish(Identity(files[1], 1, File.ReadAllBytes(files[1].FullName)), Entry(42)), Is.True);
        var warm = store.PlanNative(Owner(1))!;
        long coldReservation = FirstBuildImageQueue<Result>.RowOverheadBytes + 4 + 4;
        long warmReservation = FirstBuildImageQueue<Result>.RowOverheadBytes + 4 + (ownedRecord ? warm.OwnedRecordLiveBytes : warm.ExtraLiveBytes);
        using var warmEntered = new ManualResetEventSlim();
        using var queue = new FirstBuildImageQueue<Result>(files, _ => 4,
            (_, _) => throw new InvalidOperationException("Per-item recipe expected."),
            workers: 2, jobs: 8, maxReservedBytes: coldReservation + warmReservation,
            plan: (index, _) => index == 0
                ? new FirstBuildImageQueue<Result>.WorkItem(4, (source, _) =>
                {
                    if (!warmEntered.Wait(5000)) throw new InvalidOperationException("Warm worker did not enter.");
                    return new Result { Cold = source };
                })
                : new FirstBuildImageQueue<Result>.WorkItem((ownedRecord ? warm.OwnedRecordLiveBytes : warm.ExtraLiveBytes), (source, token) =>
                {
                    warmEntered.Set();
                    return new Result { Plan = warm, Warm = warm.Read(Identity(files[1], 1, source), token, ownedRecord: ownedRecord) };
                }));
        Assert.That(queue.TryTake(0, out var first), Is.True);
        Assert.That(first!.Error, Is.Null);
        Assert.That(first.Value!.Cold, Is.EqualTo(new byte[] { 1, 1, 1, 1 }));
        Assert.That(SpinWait.SpinUntil(() => queue.ActiveWorkers == 0, 5000), Is.True);
        Assert.That(queue.Scheduled, Is.EqualTo(2));
        Assert.That(queue.Decoded, Is.EqualTo(2));
        Assert.That(queue.MaxActiveWorkers, Is.EqualTo(2));
        Assert.That(queue.MaxReservedBytes, Is.EqualTo(coldReservation + warmReservation));
        Assert.That(queue.HeldBytes, Is.EqualTo(coldReservation + warmReservation));
        Assert.That(store.Hits, Is.Zero, "Private warm completion is not yet ordered consumption.");
        first.MarkConsumed(); first.Dispose();
        Assert.That(queue.HeldBytes, Is.EqualTo(warmReservation));
        Assert.That(queue.TryTake(1, out var second), Is.True);
        var result = second!.Value!;
        if (ownedRecord) store.CompleteOwnedRead(result.Plan!, result.Warm!)!.WithPinnedPixels(
            (pointer, _) => Assert.That(System.Runtime.InteropServices.Marshal.ReadByte(pointer), Is.EqualTo(42)));
        else Assert.That(store.CompleteRead(result.Plan!, result.Warm!)!.Pixels[0], Is.EqualTo(42));
        second.MarkConsumed(); second.Dispose();
        Assert.That(queue.Consumed, Is.EqualTo(2));
        Assert.That(store.Hits, Is.EqualTo(1));
        Assert.That(store.ReadBytes, Is.EqualTo(warm.RecordBytes));
        Assert.That(queue.HeldBytes, Is.Zero);
    }
}
