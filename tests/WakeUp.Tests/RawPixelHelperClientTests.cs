// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class RawPixelHelperClientTests
{
    private static void ExpectThrow<T>(Action action) where T : Exception => Assert.Throws<T>(action);
    private static void ExpectNoThrow(Action action) => Assert.DoesNotThrow(action);

    private static byte[] Response(uint id = 7, uint status = 0, uint width = 1, uint height = 1,
        uint format = 4, uint length = 4, byte[]? payload = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("WUTXRAWR")); writer.Write(id); writer.Write(status);
        writer.Write(width); writer.Write(height); writer.Write(format); writer.Write(length);
        writer.Write(payload ?? new byte[] { 1, 2, 3, 4 }); writer.Flush();
        return stream.ToArray();
    }

    private static byte[] Read(byte[] bytes, uint id = 7)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        return RawPixelHelperClient.ReadResponse(reader, id, 1, 1);
    }

    [Test]
    public void ExactPixelsSurviveTwoCorrelatedResponsesWithoutStateLeak()
    {
        using var stream = new MemoryStream();
        byte[] first = Response(), second = Response(id: 8, payload: new byte[] { 9, 8, 7, 6 });
        stream.Write(first, 0, first.Length); stream.Write(second, 0, second.Length); stream.Position = 0;
        using var reader = new BinaryReader(stream);
        Assert.That(RawPixelHelperClient.ReadResponse(reader, 7, 1, 1), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        Assert.That(RawPixelHelperClient.ReadResponse(reader, 8, 1, 1), Is.EqualTo(new byte[] { 9, 8, 7, 6 }));
        Assert.That(stream.Position, Is.EqualTo(stream.Length));
    }

    [Test]
    public void UnsolicitedOrStaleResponseCannotBeUsedForNextRequest()
    {
        ExpectThrow<InvalidDataException>(() => Read(Response(id: 6)));
        ExpectThrow<InvalidDataException>(() => Read(Response(), 8));
        var bytes = Response(); bytes[0] ^= 1;
        ExpectThrow<InvalidDataException>(() => Read(bytes));
    }

    [Test]
    public void EveryTruncatedHeaderAndPayloadIsRejected()
    {
        var response = Response();
        for (int length = 0; length < response.Length; length++)
        {
            var truncated = new byte[length]; Array.Copy(response, truncated, length);
            if (length < 8) ExpectThrow<InvalidDataException>(() => Read(truncated));
            else ExpectThrow<EndOfStreamException>(() => Read(truncated));
        }
    }

    [TestCase(0u)]
    [TestCase(3u)]
    [TestCase(5u)]
    [TestCase(16777217u)]
    [TestCase(uint.MaxValue)]
    public void InvalidPayloadLengthIsRejectedBeforeReadingOrAllocating(uint length)
    {
        ExpectThrow<InvalidDataException>(() => Read(Response(length: length, payload: Array.Empty<byte>())));
    }

    [Test]
    public void DescriptorMustExactlyMatchRequestedBaseRgba()
    {
        ExpectThrow<InvalidDataException>(() => Read(Response(width: 2)));
        ExpectThrow<InvalidDataException>(() => Read(Response(height: 2)));
        ExpectThrow<InvalidDataException>(() => Read(Response(format: 3)));
    }

    [Test]
    public void RefusalsHaveAnEmptyExactErrorFrame()
    {
        ExpectThrow<NotSupportedException>(() => Read(Response(status: 1, width: 0, height: 0, format: 0, length: 0, payload: Array.Empty<byte>())));
        ExpectThrow<InvalidDataException>(() => Read(Response(status: 2, width: 0, height: 0, format: 0, length: 0, payload: Array.Empty<byte>())));
        ExpectThrow<InvalidDataException>(() => Read(Response(status: 3, width: 0, height: 0, format: 0, length: 0, payload: Array.Empty<byte>())));
        ExpectThrow<InvalidDataException>(() => Read(Response(status: 1)));
    }

    [Test]
    public void HandshakePinsVersionAndRawProcessAllowance()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("WUTXRAWHELP00001")); writer.Write(RawPixelHelperClient.ChildReservation); writer.Flush();
        var bytes = stream.ToArray();
        Assert.That(bytes.Length, Is.EqualTo(20), "Sixteen ASCII handshake bytes plus the four-byte process cap.");
        using (var valid = new BinaryReader(new MemoryStream(bytes))) RawPixelHelperClient.ReadHandshake(valid);
        bytes[19] ^= 1;
        using var wrongBudget = new BinaryReader(new MemoryStream(bytes));
        ExpectThrow<InvalidDataException>(() => RawPixelHelperClient.ReadHandshake(wrongBudget));
        bytes[0] ^= 1;
        using var wrongVersion = new BinaryReader(new MemoryStream(bytes));
        ExpectThrow<InvalidDataException>(() => RawPixelHelperClient.ReadHandshake(wrongVersion));
    }

    [TestCase(0, 1, 1, 1)]
    [TestCase(16777217, 1, 1, 1)]
    [TestCase(1, 0, 1, 1)]
    [TestCase(1, 1, -1, 1)]
    [TestCase(1, 8193, 1, 1)]
    [TestCase(1, 1, 8193, 1)]
    [TestCase(1, 8192, 8192, 1)]
    [TestCase(1, 1, 1, 0)]
    [TestCase(1, 1, 1, 3)]
    public void UnsupportedInputCannotAllocateOrStartAChild(int length, int width, int height, int kind)
    {
        ExpectThrow<NotSupportedException>(() => RawPixelHelperClient.ValidateRequest(length, width, height, kind));
    }

    [Test]
    public void EncodedAndPixelLimitsAreIndependentAndInclusive()
    {
        ExpectNoThrow(() => RawPixelHelperClient.ValidateRequest(RawPixelHelperClient.MaxSource, 2048, 2048, 1));
        ExpectNoThrow(() => RawPixelHelperClient.ValidateRequest(1, 8192, 1, 2));
    }

    [Test]
    public void RequestHasOnlyFixedMetadataAndImmutableSourceBytes()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            RawPixelHelperClient.WriteRequest(writer, 19, new byte[] { 27, 28 }, 3, 4, 2);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        Assert.That(Encoding.ASCII.GetString(reader.ReadBytes(8)), Is.EqualTo("WUTXRAWQ"));
        Assert.That(reader.ReadUInt32(), Is.EqualTo(19));
        Assert.That(reader.ReadInt32(), Is.EqualTo(3));
        Assert.That(reader.ReadInt32(), Is.EqualTo(4));
        Assert.That(reader.ReadInt32(), Is.EqualTo(2));
        Assert.That(reader.ReadInt32(), Is.EqualTo(2));
        Assert.That(reader.ReadBytes(2), Is.EqualTo(new byte[] { 27, 28 }));
        Assert.That(stream.Position, Is.EqualTo(stream.Length));
    }

    [Test]
    public void ConstructionAndCancelledRequestNeverStartAChild()
    {
        using var client = new RawPixelHelperClient("missing-helper", "bad-hash");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        ExpectThrow<OperationCanceledException>(() => client.Decode(new byte[] { 1 }, 1, 1, 1, cancellation.Token));
        Assert.That(client.Starts, Is.Zero);
        Assert.That(client.Requests, Is.Zero);
        Assert.That(client.Failures, Is.Zero);
    }

    [Test]
    public void DisposalIsIdempotentAndPreventsLaunch()
    {
        var client = new RawPixelHelperClient("missing-helper", "bad-hash");
        client.Dispose(); client.Dispose();
        ExpectThrow<ObjectDisposedException>(() => client.Decode(new byte[] { 1 }, 1, 1, 1, CancellationToken.None));
        Assert.That(client.Starts, Is.Zero);
    }

    private static RawPixelHelperClient ActualClient()
    {
        string? path = Environment.GetEnvironmentVariable("WAKE_UP_RAW_HELPER_TEST");
        if (string.IsNullOrEmpty(path)) Assert.Ignore("Set WAKE_UP_RAW_HELPER_TEST to the actual built raw helper for owned-child integration checks.");
        using var input = File.OpenRead(path!);
        using var hash = SHA256.Create();
        return new RawPixelHelperClient(Path.GetFullPath(path!), BitConverter.ToString(hash.ComputeHash(input)).Replace("-", ""));
    }

    [Test]
    public void ActualChildReusesOneProcessAndReturnsExactBottomFirstPixels()
    {
        using var client = ActualClient();
        // Same asymmetric RGBA/alpha fixture as the managed PNG filter tests.
        byte[] upper = { 1, 2, 3, 0, 11, 18, 240, 37, 250, 8, 100, 255 };
        byte[] lower = { 90, 0, 222, 8, 80, 90, 3, 99, 180, 254, 91, 197 };
        var rows = new byte[26]; Array.Copy(upper, 0, rows, 1, 12); Array.Copy(lower, 0, rows, 14, 12);
        var expected = new byte[24]; Array.Copy(lower, expected, 12); Array.Copy(upper, 0, expected, 12, 12);
        Assert.That(client.Decode(Png(3, 2, rows), 3, 2, 1, CancellationToken.None), Is.EqualTo(expected));
        int id = client.ProcessId!.Value;
        // Valid PNG bytes deliberately tagged as JPEG receive a complete
        // unsupported frame; the following correct request must still work.
        ExpectThrow<NotSupportedException>(() => client.Decode(Png(3, 2, rows), 3, 2, 2, CancellationToken.None));
        Assert.That(client.Decode(Png(1, 1, new byte[] { 0, 77, 88, 99, 123 }), 1, 1, 1, CancellationToken.None),
            Is.EqualTo(new byte[] { 77, 88, 99, 123 }));
        Assert.That(client.Starts, Is.EqualTo(1)); Assert.That(client.Requests, Is.EqualTo(3));
        Assert.That(client.Failures, Is.EqualTo(1));
        Assert.That(client.ProcessId, Is.EqualTo(id));
        client.Dispose();
        AssertOwnedProcessExited(id);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActualActiveRequestCancellationOrDisposalJoinsOwnedChild(bool dispose)
    {
        using var client = ActualClient();
        client.Decode(Png(1, 1, new byte[] { 0, 17, 31, 53, 127 }), 1, 1, 1, CancellationToken.None);
        int id = client.ProcessId!.Value;
        // Maximum admitted base output gives the test time to interrupt real
        // codec/output work after the second request has actually been sent.
        var rows = new byte[2048 * (2048 * 4 + 1)];
        var source = Png(2048, 2048, rows);
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> pending = Task.Run(() => client.Decode(source, 2048, 2048, 1, cancellation.Token));
        try
        {
            Assert.That(SpinWait.SpinUntil(() => client.Requests == 2 || pending.IsCompleted, 5000), Is.True,
                "The real second request must reach the helper before interruption.");
            Assert.That(client.Requests, Is.EqualTo(2));
            Assert.That(pending.IsCompleted, Is.False, "The bounded image finished before the cancellation observation.");
            if (dispose) client.Dispose(); else cancellation.Cancel();
            ExpectThrow<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            client.Dispose();
            AssertOwnedProcessExited(id);
            Assert.That(client.Starts, Is.EqualTo(1));
        }
        finally
        {
            cancellation.Cancel(); client.Dispose();
            try { pending.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
    }

    private static void AssertOwnedProcessExited(int id)
    {
        try { using var process = Process.GetProcessById(id); Assert.That(process.HasExited, Is.True, "Owned helper survived teardown."); }
        catch (ArgumentException) { } // The OS has already released that process.
    }

    // The same independently authored PNG chunk/checksum grammar used by the
    // existing PNG tests, with DeflateStream so the 16 MiB cancellation image
    // stays inside the separate encoded-source cap.
    private static byte[] Png(int width, int height, byte[] rows)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
        using var header = new MemoryStream();
        BigEndian(header, (uint)width); BigEndian(header, (uint)height);
        header.Write(new byte[] { 8, 6, 0, 0, 0 }, 0, 5);
        Chunk(output, "IHDR", header.ToArray());
        using var zlib = new MemoryStream(); zlib.WriteByte(0x78); zlib.WriteByte(0x01);
        using (var deflate = new DeflateStream(zlib, CompressionLevel.Fastest, true)) deflate.Write(rows, 0, rows.Length);
        uint a = 1, b = 0;
        foreach (byte value in rows) { a = (a + value) % 65521; b = (b + a) % 65521; }
        BigEndian(zlib, b << 16 | a); Chunk(output, "IDAT", zlib.ToArray()); Chunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void BigEndian(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value);
    }

    private static void Chunk(Stream stream, string kind, byte[] payload)
    {
        BigEndian(stream, (uint)payload.Length);
        byte[] name = Encoding.ASCII.GetBytes(kind); stream.Write(name, 0, name.Length); stream.Write(payload, 0, payload.Length);
        uint crc = uint.MaxValue;
        foreach (var bytes in new[] { name, payload })
            foreach (byte value in bytes)
            { crc ^= value; for (int n = 0; n < 8; n++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320U : crc >> 1; }
        BigEndian(stream, ~crc);
    }
}
