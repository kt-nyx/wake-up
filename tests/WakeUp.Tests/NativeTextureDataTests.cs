// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class NativeTextureDataTests
{
    private const string Identity = "owned-native-record";
    private static PngCache.Entry Entry() => new()
    {
        Width = 2, Height = 2, TextureFormat = 4, GraphicsFormat = 8, Mips = 2,
        Filter = 2, WrapU = 1, WrapV = 2, WrapW = 0, Aniso = 2, Bias = 0.25f, Readable = true,
        Pixels = Enumerable.Range(0, 20).Select(i => (byte)(17 + i)).ToArray()
    };

    [Test]
    public void OwnedRecordPreservesDescriptorAndLendsExactlyItsPixelRange()
    {
        var expected = Entry();
        byte[] bytes = NativeTextureData.Encode(Identity, expected);
        var record = NativeTextureData.DecodeOwned(Identity, bytes);
        Assert.That(record.RecordBytes, Is.EqualTo(bytes.Length));
        Assert.That(record.PixelOffset, Is.EqualTo(57 + Encoding.UTF8.GetByteCount(Identity)));
        Assert.That(record.PixelBytes, Is.EqualTo(expected.Pixels.Length));
        var descriptor = record.Descriptor.ToEntry();
        Assert.That(descriptor.Pixels, Is.Empty);
        Assert.That(descriptor.Width, Is.EqualTo(expected.Width));
        Assert.That(descriptor.Height, Is.EqualTo(expected.Height));
        Assert.That(descriptor.TextureFormat, Is.EqualTo(expected.TextureFormat));
        Assert.That(descriptor.GraphicsFormat, Is.EqualTo(expected.GraphicsFormat));
        Assert.That(descriptor.Mips, Is.EqualTo(expected.Mips));
        Assert.That(descriptor.Filter, Is.EqualTo(expected.Filter));
        Assert.That(descriptor.WrapU, Is.EqualTo(expected.WrapU));
        Assert.That(descriptor.WrapV, Is.EqualTo(expected.WrapV));
        Assert.That(descriptor.WrapW, Is.EqualTo(expected.WrapW));
        Assert.That(descriptor.Aniso, Is.EqualTo(expected.Aniso));
        Assert.That(descriptor.Bias, Is.EqualTo(expected.Bias));
        Assert.That(descriptor.Readable, Is.EqualTo(expected.Readable));
        descriptor.Width = 99; descriptor.Pixels = new byte[5];
        Assert.That(record.Descriptor.Width, Is.EqualTo(expected.Width));
        Assert.That(record.Descriptor.ToEntry().Pixels, Is.Empty);
        record.WithPinnedPixels((pointer, count) =>
        {
            var copy = new byte[count]; Marshal.Copy(pointer, copy, 0, count);
            Assert.That(copy, Is.EqualTo(expected.Pixels));
            // This must be a view of the transferred record, not a duplicate
            // pixel array that happens to contain the same bytes.
            var expectedPin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { Assert.That(pointer, Is.EqualTo(IntPtr.Add(expectedPin.AddrOfPinnedObject(), record.PixelOffset))); }
            finally { expectedPin.Free(); }
        });
    }

    [TestCase("identity")]
    [TestCase("magic")]
    [TestCase("name-length")]
    [TestCase("width")]
    [TestCase("mips")]
    [TestCase("graphics")]
    [TestCase("filter")]
    [TestCase("wrap")]
    [TestCase("bias")]
    [TestCase("negative-length")]
    [TestCase("oversized-length")]
    [TestCase("truncated-descriptor")]
    [TestCase("truncated-pixels")]
    [TestCase("trailing-bytes")]
    public void OwnedRecordRejectsTheSameIdentityDescriptorAndRangeCorruptionAsDecode(string corruption)
    {
        byte[] bytes = NativeTextureData.Encode(Identity, Entry());
        int descriptor = 8 + Encoding.UTF8.GetByteCount(Identity);
        void SetInt(int offset, int value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, offset, 4);
        switch (corruption)
        {
            case "identity": bytes[8] ^= 1; break;
            case "magic": bytes[0] ^= 1; break;
            case "name-length": SetInt(4, int.MaxValue); break;
            case "width": SetInt(descriptor, int.MaxValue); break;
            case "mips": SetInt(descriptor + 16, 3); break;
            case "graphics": SetInt(descriptor + 8, int.MaxValue); break;
            case "filter": SetInt(descriptor + 20, 3); break;
            case "wrap": SetInt(descriptor + 32, 4); break;
            case "bias": Buffer.BlockCopy(BitConverter.GetBytes(float.NaN), 0, bytes, descriptor + 40, 4); break;
            case "negative-length": SetInt(descriptor + 45, -1); break;
            case "oversized-length": SetInt(descriptor + 45, int.MaxValue); break;
            case "truncated-descriptor": bytes = bytes.Take(descriptor + 10).ToArray(); break;
            case "truncated-pixels": bytes = bytes.Take(bytes.Length - 1).ToArray(); break;
            case "trailing-bytes": bytes = bytes.Concat(new byte[] { 0 }).ToArray(); break;
        }
        var original = Assert.Catch<Exception>((Action)(() => NativeTextureData.Decode(Identity, bytes)));
        var owned = Assert.Catch<Exception>((Action)(() => NativeTextureData.DecodeOwned(Identity, bytes)));
        Assert.That(owned!.GetType(), Is.EqualTo(original!.GetType()));
        Assert.That(owned.Message, Is.EqualTo(original.Message));
    }

    [Test]
    public void OrdinaryDecodeStillReturnsItsOwnPixelArray()
    {
        byte[] bytes = NativeTextureData.Encode(Identity, Entry());
        var first = NativeTextureData.Decode(Identity, bytes);
        first.Pixels[0] = 255;
        Assert.That(NativeTextureData.Decode(Identity, bytes).Pixels, Is.EqualTo(Entry().Pixels));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PixelPinDoesNotKeepRecordAliveAfterReturnOrException(bool throwFromUpload)
    {
        WeakReference bytes = CreateAndReleasePinnedRecord(throwFromUpload);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.That(bytes.IsAlive, Is.False, "A retained GCHandle would keep the backing record rooted.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndReleasePinnedRecord(bool throwFromUpload)
    {
        byte[] bytes = NativeTextureData.Encode(Identity, Entry());
        var weak = new WeakReference(bytes);
        var record = NativeTextureData.DecodeOwned(Identity, bytes);
        bool entered = false;
        try
        {
            record.WithPinnedPixels((pointer, count) =>
            {
                entered = true;
                Assert.That(count, Is.EqualTo(20));
                Assert.That(Marshal.ReadByte(pointer), Is.EqualTo(17));
                if (throwFromUpload) throw new InvalidOperationException("upload-failed");
            });
        }
        catch (InvalidOperationException error) when (error.Message == "upload-failed" && throwFromUpload) { }
        Assert.That(entered, Is.True);
        // Check the same record remains usable after an upload failure too.
        record.WithPinnedPixels((pointer, count) => Assert.That(Marshal.ReadByte(pointer, count - 1), Is.EqualTo(36)));
        return weak;
    }
}
