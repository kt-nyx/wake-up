// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PreparedTextureStoreTests
{
    private string root = null!;
    private string StoreRoot => Path.Combine(root, "WakeUp", "PreparedTextures", "v1");
    private string GroupRoot => Path.Combine(StoreRoot, "groups");
    private static CacheLaunchPolicy Policy(string action)
    {
        CacheLaunchPolicy? selected = null;
        return CacheLaunchPolicy.Latch(ref selected, action, () => { });
    }
    private static PngCache.Entry Entry() => new()
    {
        Width = 2, Height = 2, TextureFormat = 4, GraphicsFormat = 8, Mips = 2,
        Filter = 2, WrapU = 1, WrapV = 2, WrapW = 0, Aniso = 2, Bias = 0.25f,
        Pixels = new byte[20]
    };
    [SetUp] public void SetUp() => root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "wake-up-prepared-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [TestCase(false)]
    [TestCase(true)]
    public void OwnedWarmRecordUsesSameAuthenticationAndRejectsStaleCompletion(bool compress)
    {
        string owner = new('A', 64), identity = "prepared-v3:" + new string('A', 64) + ":source-A";
        using var cache = new PreparedTextureStore(root, Policy("normal"), null, compress);
        Assert.That(cache.Publish(identity, Entry()), Is.True);
        var plan = cache.PlanNative(owner)!;
        Assert.That(plan.ExtraLiveBytes - plan.OwnedRecordLiveBytes, Is.EqualTo(plan.RecordBytes));
        var outcome = Task.Run(() => plan.Read(identity, CancellationToken.None, ownedRecord: true)).GetAwaiter().GetResult();
        Assert.That(outcome.Result, Is.Null);
        Assert.That(cache.CompleteRead(plan, outcome), Is.Null);
        Assert.That(cache.Hits, Is.Zero);
        var record = cache.CompleteOwnedRead(plan, outcome)!;
        Assert.That(record.PixelBytes, Is.EqualTo(Entry().Pixels.Length));
        record.WithPinnedPixels((pointer, length) => {
            var actual = new byte[length]; System.Runtime.InteropServices.Marshal.Copy(pointer, actual, 0, length);
            Assert.That(actual, Is.EqualTo(Entry().Pixels));
        });
        Assert.That(cache.Hits, Is.EqualTo(1));
        Assert.That(cache.CompleteOwnedRead(plan, outcome), Is.Null);
        var stale = plan.Read(identity, CancellationToken.None, ownedRecord: true);
        Assert.That(cache.CanPublish(identity, 20), Is.True);
        Assert.That(cache.CompleteOwnedRead(plan, stale), Is.Null);
        Assert.That(plan.Read(identity, CancellationToken.None, ownedRecord: true).Cancelled, Is.True);
        var fresh = cache.PlanNative(owner)!;
        var wrongSource = fresh.Read(identity + "changed", CancellationToken.None, ownedRecord: true);
        Assert.That(cache.CompleteOwnedRead(fresh, wrongSource), Is.Null);
        Assert.That(wrongSource.Reason, Is.EqualTo("prepared-source-miss"));
        var cancelled = fresh.Read(identity, new CancellationToken(true), ownedRecord: true);
        Assert.That(cancelled.Cancelled, Is.True);
        Assert.That(cache.CompleteOwnedRead(fresh, cancelled), Is.Null);
    }

    [Test]
    public void WarmPlanValidatesPrivatelyAndAccountsOnlyOnceAtOrderedCompletion()
    {
        string owner = new('A', 64);
        string identity = "prepared-v3:" + owner + ":source-A";
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(cache.Publish(identity, Entry()), Is.True);
        var plan = cache.PlanNative(owner)!;
        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.ExtraLiveBytes, Is.GreaterThanOrEqualTo(2L * plan.RecordBytes));
        var outcome = Task.Run(() => plan.Read(identity, CancellationToken.None)).GetAwaiter().GetResult();
        Assert.That(outcome.IsError, Is.False);
        Assert.That(cache.Hits, Is.Zero);
        Assert.That(cache.ReadBytes, Is.Zero);
        Assert.That(cache.CompleteRead(plan, outcome)!.Pixels, Is.EqualTo(Entry().Pixels));
        Assert.That(cache.Hits, Is.EqualTo(1));
        Assert.That(cache.ReadBytes, Is.EqualTo(plan.RecordBytes));
        Assert.That(cache.CompleteRead(plan, outcome), Is.Null);
        Assert.That(cache.Hits, Is.EqualTo(1));
    }

    [Test]
    public void WarmSourceMismatchIsMissWithoutInvalidatingTheExistingRecord()
    {
        string owner = new('A', 64);
        string identity = "prepared-v3:" + owner + ":source-A";
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(cache.Publish(identity, Entry()), Is.True);
        var plan = cache.PlanNative(owner)!;
        var outcome = plan.Read("prepared-v3:" + owner + ":source-B", CancellationToken.None);
        Assert.That(outcome.Reason, Is.EqualTo("prepared-source-miss"));
        Assert.That(cache.CompleteRead(plan, outcome), Is.Null);
        Assert.That(cache.Misses, Is.EqualTo(1));
        Assert.That(cache.Errors, Is.Zero);
        Assert.That(cache.Read(identity), Is.Not.Null);
    }

    [Test]
    public void MutationDrainsOutsideLockThenRejectsPendingAndLaterReadsFromOldPlans()
    {
        string owner = new('A', 64);
        string identity = "prepared-v3:" + owner + ":source-A";
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        using var context = new GroupedTextureBlocks.ReadContext();
        Assert.That(cache.Publish(identity, Entry()), Is.True);
        var plan = cache.PlanNative(owner)!;
        var pending = Task.Run(() => plan.Read(identity, CancellationToken.None, context));
        bool callback = false;
        Action drain = () =>
        {
            // A different thread must enter the owner lock before this callback
            // returns. This would time out if BeforeMutation ran under that lock.
            var probe = Task.Run(() => cache.HasOwner(owner));
            Assert.That(probe.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(probe.Result, Is.True);
            Assert.That(pending.GetAwaiter().GetResult().Cancelled, Is.False);
            Assert.That(context.OpenHandleCount, Is.EqualTo(1));
            context.Clear();
            callback = true;
        };
        cache.BeforeMutation += drain;
        Assert.That(cache.CanPublish(identity, 20), Is.True);
        cache.BeforeMutation -= drain;
        Assert.That(callback, Is.True);
        Assert.That(context.OpenHandleCount, Is.Zero);
        Assert.That(cache.CompleteRead(plan, pending.Result), Is.Null);
        Assert.That(plan.Read(identity, CancellationToken.None).Cancelled, Is.True);
        Assert.That(cache.Hits, Is.Zero);
        Assert.That(cache.PlanNative(owner)!.Generation, Is.GreaterThan(plan.Generation));
        var beforeDispose = cache.PlanNative(owner)!;
        cache.Dispose();
        Assert.That(beforeDispose.Read(identity, CancellationToken.None).Cancelled, Is.True);
        Assert.That(cache.PlanNative(owner), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WarmCorruptionIsAnOwnerAccountedErrorAndCancellationIsNot(bool ownedRecord)
    {
        string owner = new('A', 64);
        string identity = "prepared-v3:" + owner + ":source-A";
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(cache.Publish(identity, Entry()), Is.True);
        var cancelled = cache.PlanNative(owner)!;
        var cancellation = cancelled.Read(identity, new CancellationToken(true), ownedRecord: ownedRecord);
        Assert.That(cancellation.Cancelled, Is.True);
        Assert.That((ownedRecord ? (object?)cache.CompleteOwnedRead(cancelled, cancellation) : cache.CompleteRead(cancelled, cancellation)), Is.Null);
        Assert.That(cache.Misses, Is.Zero);
        string path = Directory.GetFiles(GroupRoot, "*.group").Single();
        byte[] bytes = File.ReadAllBytes(path); bytes[bytes.Length - 1] ^= 1; File.WriteAllBytes(path, bytes);
        var plan = cache.PlanNative(owner)!;
        var outcome = plan.Read(identity, CancellationToken.None, ownedRecord: ownedRecord);
        Assert.That(outcome.Reason, Is.EqualTo("group-block-digest"));
        Assert.That(cache.Errors, Is.Zero);
        Assert.That((ownedRecord ? (object?)cache.CompleteOwnedRead(plan, outcome) : cache.CompleteRead(plan, outcome)), Is.Null);
        Assert.That(cache.Errors, Is.EqualTo(1));
        Assert.That(cache.Misses, Is.EqualTo(1));
        Assert.That(cache.HasOwner(owner), Is.True, "Only ordered fallback may invalidate a failed mapping.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StartupStoreReleasesGroupStreamsBeforeReleasingOwnership(bool compress)
    {
        using (var cache = new PreparedTextureStore(root, Policy("normal"), null, compress, batched: true))
        {
            Assert.That(cache.Publish("startup-owned", Entry()), Is.True);
            cache.Complete();
            Assert.That(cache.Read("startup-owned"), Is.Not.Null);
        }
        foreach (string path in Directory.GetFiles(GroupRoot, "*.group"))
            using (var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
        using var reopened = new PreparedTextureStore(root, Policy("normal"), null, compress, batched: true);
        Assert.That(reopened.Read("startup-owned"), Is.Not.Null);
        Assert.That(reopened.Publish("next-startup-owned", Entry()), Is.True);
        reopened.Complete();
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void RoundTripRestoresDescriptorAndReturnsPrivatePayload(bool compress, bool readable)
    {
        using var cache = new PreparedTextureStore(root, Policy("normal"), compress: compress);
        var input = Entry(); input.Pixels[0] = 42; input.Readable = readable;
        Assert.That(cache.Publish("source\0options\nplatform", input), Is.True);
        input.Pixels[0] = 99;
        var output = cache.Read("source\0options\nplatform")!;
        Assert.That(output.Pixels[0], Is.EqualTo(42));
        Assert.That(output.Width, Is.EqualTo(2)); Assert.That(output.Height, Is.EqualTo(2));
        Assert.That(output.Mips, Is.EqualTo(2)); Assert.That(output.TextureFormat, Is.EqualTo(4));
        Assert.That(output.GraphicsFormat, Is.EqualTo(8)); Assert.That(output.Filter, Is.EqualTo(2));
        Assert.That(output.WrapU, Is.EqualTo(1)); Assert.That(output.WrapV, Is.EqualTo(2));
        Assert.That(output.WrapW, Is.Zero); Assert.That(output.Aniso, Is.EqualTo(2));
        Assert.That(output.Bias, Is.EqualTo(0.25f));
        Assert.That(output.Readable, Is.EqualTo(readable));
        output.Pixels[0] = 7;
        Assert.That(cache.Read("source\0options\nplatform")!.Pixels[0], Is.EqualTo(42));
    }

    [Test]
    public void NativeAdmissionUsesBroaderPixelLimitAndBoundsUtf8Identity()
    {
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        const string identity = "é";
        int pixels = NativeTextureData.MaximumPixels;
        Assert.That(pixels, Is.GreaterThan(PreparedTextureStore.MaximumEntry));
        Assert.That(cache.CanPublish(identity, pixels), Is.True);
        Assert.That(cache.CanPublish(identity, pixels + 1), Is.False);
        Assert.That(cache.CanPublish(identity, 0), Is.False);
        Assert.That(cache.CanPublish(identity, int.MaxValue), Is.False);
        Assert.That(cache.CanPublish(new string('a', PreparedTextureStore.MaximumIdentityBytes), 4), Is.True);
        Assert.That(cache.CanPublish(new string('é', PreparedTextureStore.MaximumIdentityBytes), 4), Is.False);
        Assert.That(cache.CanPublish("\ud800", 4), Is.False);
    }

    [Test]
    public void EarlyAdmissionAccountsForQuotaAndCancelledProducerLeavesNoPayload()
    {
        const string identity = "source";
        bool invoked = false;
        using (var tooSmall = new PreparedTextureStore(root, Policy("normal"), 20))
        {
            Assert.That(tooSmall.CanPublish(identity, Entry().Pixels.Length), Is.False);
            Assert.That(tooSmall.TryCapture(identity, Entry().Pixels.Length, () => invoked = true), Is.False);
            Assert.That(invoked, Is.False);
        }
        const int capacity = 64 * 1024;
        using var enough = new PreparedTextureStore(root, Policy("normal"), capacity);
        Assert.That(enough.CanPublish(identity, Entry().Pixels.Length), Is.True);
        Assert.Throws<OperationCanceledException>((Action)(() => enough.TryCapture(identity, Entry().Pixels.Length,
            () => throw new OperationCanceledException())));
        // Admission and a cancelled producer callback cannot publish an entry.
        Assert.That(enough.StoredBytes, Is.Zero);
        Assert.That(Directory.Exists(GroupRoot), Is.False);
        Assert.That(enough.Publish(identity, Entry()), Is.True);
        Assert.That(enough.StoredBytes, Is.EqualTo(Directory.GetFiles(StoreRoot, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length)));
        Assert.That(enough.StoredBytes, Is.LessThanOrEqualTo(capacity));
    }

    [Test]
    public void ChangedSourceOptionsHelperOrPlatformIdentityMisses()
    {
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(cache.Publish("source-A|options-A|helper-A|platform-A", Entry()), Is.True);
        foreach (string changed in new[] { "source-B|options-A|helper-A|platform-A", "source-A|options-B|helper-A|platform-A",
                     "source-A|options-A|helper-B|platform-A", "source-A|options-A|helper-A|platform-B" })
            Assert.That(cache.Read(changed), Is.Null);
    }

    [Test]
    public void OwnerPresenceIsSourceIndependentButNeverSubstitutesForExactRead()
    {
        string owner = PreparedTextureRuntime.OwnerIdentity("provider-A", "Textures/same.dds", 0);
        string other = PreparedTextureRuntime.OwnerIdentity("provider-B", "Textures/same.dds", 0);
        string path = Path.Combine(root, "same.dds");
        string Key(byte value) => PreparedTextureRuntime.Identity("provider-A", "Textures/same.dds", path,
            new[] { value }, 0, "platform", "");
        using (var cache = new PreparedTextureStore(root, Policy("normal")))
        {
            Assert.That(cache.HasOwner(owner), Is.False);
            Assert.That(cache.Publish(Key(1), Entry()), Is.True);
            Assert.That(cache.HasOwner(owner), Is.True);
            Assert.That(cache.HasOwner(other), Is.False);
            Assert.That(cache.Hits, Is.Zero, "Presence does not read or validate a texture.");
            Assert.That(cache.Read(Key(2)), Is.Null, "A changed source still requires an exact match.");
            Assert.That(cache.HasOwner(owner), Is.True);
            cache.Complete();
        }
        using var reopened = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(reopened.HasOwner(owner), Is.True);
        Assert.That(reopened.Read(Key(1)), Is.Not.Null);
        reopened.Invalidate(Key(1));
        Assert.That(reopened.HasOwner(owner), Is.False);
        reopened.Dispose();
        Assert.That(reopened.HasOwner(owner), Is.False);
    }

    [Test]
    public void CorruptGroupedPayloadIsRejectedAndCanBeRebuilt()
    {
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(cache.Publish("identity", Entry()), Is.True);
        string path = Directory.GetFiles(GroupRoot, "*.group").Single();
        byte[] file = File.ReadAllBytes(path); file[file.Length - 1] ^= 0xff; File.WriteAllBytes(path, file);
        Assert.That(cache.Read("identity"), Is.Null);
        Assert.That(cache.Hits, Is.Zero);
        Assert.That(cache.Publish("identity", Entry()), Is.True);
        Assert.That(cache.Read("identity")!.Pixels, Is.EqualTo(Entry().Pixels));
    }

    [TestCase("identity")]
    [TestCase("mips")]
    [TestCase("filter")]
    [TestCase("negative-length")]
    [TestCase("truncated-pixels")]
    [TestCase("trailing-bytes")]
    [TestCase("magic")]
    public void InvalidNativeDescriptorIsRejectedEvenWithValidGroupedHashes(string field)
    {
        string owner = new('A', 64);
        string identity = "prepared-v3:" + owner + ":descriptor-validation";
        string key = PngCache.Hex(PngCache.Hash(Encoding.UTF8.GetBytes(identity)));
        byte[] payload = NativeTextureData.Encode(field == "identity" ? "different-identity" : identity, Entry());
        using (var stream = new MemoryStream(payload))
        using (var reader = new BinaryReader(stream))
        {
            reader.ReadInt32(); reader.ReadBytes(reader.ReadInt32());
            int descriptor = checked((int)stream.Position);
            if (field == "mips") Buffer.BlockCopy(BitConverter.GetBytes(3), 0, payload, descriptor + 16, 4);
            if (field == "filter") Buffer.BlockCopy(BitConverter.GetBytes(3), 0, payload, descriptor + 20, 4);
            if (field == "negative-length") Buffer.BlockCopy(BitConverter.GetBytes(-1), 0, payload, descriptor + 45, 4);
        }
        if (field == "truncated-pixels") payload = payload.Take(payload.Length - 1).ToArray();
        if (field == "trailing-bytes") payload = payload.Concat(new byte[] { 0 }).ToArray();
        if (field == "magic") payload[0] ^= 1;
        var grouped = new GroupedTextureBlocks(GroupRoot);
        using (var owned = new OwnedCacheStore(StoreRoot, PreparedTextureStore.MaximumEntry, PreparedTextureStore.MaximumStore,
                   Policy("normal"), () => grouped.StoredBytes, grouped.Clear, grouped.Initialize))
            Assert.That(grouped.Publish(owned, key, payload, ownerKey: owner), Is.True);
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        var plan = cache.PlanNative(owner)!;
        var outcome = plan.Read(identity, CancellationToken.None);
        Assert.That(outcome.IsError, Is.True, "Warm workers must reject the same invalid native descriptor.");
        Assert.That(cache.Errors, Is.Zero, "Worker validation does not update owner accounting.");
        Assert.That(cache.Read(identity), Is.Null);
        Assert.That(cache.Errors, Is.EqualTo(1));
        Assert.That(cache.Read(identity), Is.Null);
        Assert.That(cache.Errors, Is.EqualTo(1), "The invalid mapping must have been removed after the first parser refusal.");
        Assert.That(Directory.GetFiles(GroupRoot, "*.group"), Is.Empty);
    }

    [TestCase("mips")]
    [TestCase("filter")]
    [TestCase("wrap")]
    [TestCase("aniso")]
    [TestCase("nan")]
    [TestCase("infinity")]
    [TestCase("graphics")]
    [TestCase("pixels")]
    public void InvalidDescriptorsAreRejectedBeforePublication(string field)
    {
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        var entry = Entry();
        switch (field)
        {
            case "mips": entry.Mips = 3; break;
            case "filter": entry.Filter = 3; break;
            case "wrap": entry.WrapW = 4; break;
            case "aniso": entry.Aniso = 17; break;
            case "nan": entry.Bias = float.NaN; break;
            case "infinity": entry.Bias = float.PositiveInfinity; break;
            case "graphics": entry.GraphicsFormat = int.MaxValue; break;
            case "pixels": entry.Pixels = new byte[19]; break;
        }
        Assert.That(cache.Publish("identity", entry), Is.False);
        Assert.That(cache.StoredBytes, Is.Zero);
    }

    [Test]
    public void NativeReducedMipCountIsRetained()
    {
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        var entry = Entry(); entry.Mips = 1; entry.Pixels = new byte[16];
        Assert.That(cache.Publish("native-preservation", entry), Is.True);
        Assert.That(cache.Read("native-preservation")!.Mips, Is.EqualTo(1));
    }

    [Test]
    public void LargeNonSquareNativeTexturePreservesAllMipsBeyondTheExportLimit()
    {
        var entry = Entry();
        entry.Width = 2048; entry.Height = 1024; entry.Mips = 12; entry.Readable = true;
        entry.Pixels = new byte[NativeTextureData.ExpectedBytes(entry.Width, entry.Height, entry.TextureFormat, entry.Mips)];
        for (int i = 0; i < entry.Pixels.Length; i++) entry.Pixels[i] = (byte)(i % 251);
        Assert.That(entry.Pixels.Length, Is.GreaterThan(PreparedTextureStore.MaximumEntry));
        using (var cache = new PreparedTextureStore(root, Policy("normal")))
            Assert.That(cache.Publish("large-non-square-native", entry), Is.True);
        using var reopened = new PreparedTextureStore(root, Policy("normal"));
        var restored = reopened.Read("large-non-square-native")!;
        Assert.That(restored.Width, Is.EqualTo(entry.Width)); Assert.That(restored.Height, Is.EqualTo(entry.Height));
        Assert.That(restored.Mips, Is.EqualTo(entry.Mips)); Assert.That(restored.Readable, Is.True);
        Assert.That(restored.Pixels, Is.EqualTo(entry.Pixels));
    }

    [Test]
    public void ExplicitInvalidationRemovesOnlyTheRequestedRepresentation()
    {
        using var cache = new PreparedTextureStore(root, Policy("normal"));
        Assert.That(cache.Publish("provider-A|source-A|native", Entry()), Is.True);
        Assert.That(cache.Publish("provider-B|source-B|native", Entry()), Is.True);
        cache.Invalidate("provider-A|source-A|native");
        Assert.That(cache.Read("provider-A|source-A|native"), Is.Null);
        Assert.That(cache.Read("provider-B|source-B|native"), Is.Not.Null);
    }

    [Test]
    public void BypassDoesNotCreateStoreAndClearOnlyRemovesPreparedCategory()
    {
        using (var bypass = new PreparedTextureStore(root, Policy("bypass")))
        {
            Assert.That(bypass.CanPublish("identity", 20), Is.False);
            Assert.That(bypass.Publish("identity", Entry()), Is.False);
            Assert.That(bypass.Read("identity"), Is.Null); bypass.Complete();
        }
        Assert.That(Directory.Exists(root), Is.False);
        using (var cache = new PreparedTextureStore(root, Policy("normal"))) Assert.That(cache.Publish("identity", Entry()), Is.True);
        string original = Path.Combine(root, "source.png"); File.WriteAllText(original, "source");
        string png = Path.Combine(root, "WakeUp", "PngCache", "keep.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(png)!); File.WriteAllText(png, "other category");
        PreparedTextureStore.Maintain(root, Policy("clear"));
        Assert.That(Directory.GetFiles(GroupRoot), Is.Empty);
        Assert.That(File.ReadAllText(original), Is.EqualTo("source"));
        Assert.That(File.ReadAllText(png), Is.EqualTo("other category"));
        using var cleared = new PreparedTextureStore(root, Policy("clear"));
        Assert.That(cleared.Publish("identity", Entry()), Is.False);
    }
}
