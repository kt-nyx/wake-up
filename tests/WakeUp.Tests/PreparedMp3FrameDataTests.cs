// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class PreparedMp3FrameDataTests
{
    private const string Key = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    private string root = null!;
    [SetUp] public void Setup() => root = Path.Combine(Path.GetTempPath(), "wake-up-mp3-frame-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private static CacheLaunchPolicy Policy() { CacheLaunchPolicy? policy = null; return CacheLaunchPolicy.Latch(ref policy, "normal", () => { }); }
    private static PreparedMp3FrameData.Operation Frame(int pcm = 7, byte value = 0x81) => new()
    {
        Kind = PreparedMp3FrameData.OperationKind.Frame, FileOffset = 123, DestinationOffset = 3,
        Compressed = new byte[] { 0xFF, 0xFB, 0x90, 0x64 }, Pcm = Enumerable.Repeat(value, pcm).ToArray(),
        Math = new ulong[] { 0x4D415448, 0x1F80, 0x1FA0, 7, 7, 6, 1, 0x9DD10 }
    };

    [Test]
    public void SerializationPreservesResetZeroOutputAndExactBinaryFrameResults()
    {
        var chunk = new PreparedMp3FrameData.Chunk { FirstOperation = 19 };
        chunk.Operations.Add(Frame());
        chunk.Operations.Add(new PreparedMp3FrameData.Operation { Kind = PreparedMp3FrameData.OperationKind.Reset });
        chunk.Operations.Add(Frame(0));
        byte[] encoded = chunk.Encode();
        var result = PreparedMp3FrameData.Chunk.Decode(encoded);
        Assert.That(result.Encode(), Is.EqualTo(encoded));
        Assert.That(result.FirstOperation, Is.EqualTo(19));
        Assert.That(result.Operations[0].Compressed, Is.EqualTo(new byte[] { 0xFF, 0xFB, 0x90, 0x64 }));
        Assert.That(result.Operations[0].DestinationOffset, Is.EqualTo(3));
        Assert.That(result.Operations[0].Math, Is.EqualTo(chunk.Operations[0].Math));
        Assert.That(result.Operations[1].Kind, Is.EqualTo(PreparedMp3FrameData.OperationKind.Reset));
        Assert.That(result.Operations[2].Returned, Is.Zero);
        byte[] changed = (byte[])encoded.Clone(); changed[changed.Length - 1] ^= 1;
        Assert.That(PreparedMp3FrameData.ChunkKey(changed), Is.Not.EqualTo(PreparedMp3FrameData.ChunkKey(encoded)));
    }

    [Test]
    public void InvalidVersionsCountsLengthsResetPayloadsAndTrailingBytesAreRejected()
    {
        var chunk = new PreparedMp3FrameData.Chunk(); chunk.Operations.Add(Frame());
        byte[] encoded = chunk.Encode();
        foreach (int length in new[] { 0, 15, 16, 20, encoded.Length - 1 })
            Assert.Throws<InvalidDataException>((Action)(() => PreparedMp3FrameData.Chunk.Decode(encoded.Take(length).ToArray())));
        byte[] old = (byte[])encoded.Clone(); old[4] = 0;
        Assert.Throws<InvalidDataException>((Action)(() => PreparedMp3FrameData.Chunk.Decode(old)));
        byte[] huge = (byte[])encoded.Clone(); Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, huge, 29, 4);
        Assert.Throws<InvalidDataException>((Action)(() => PreparedMp3FrameData.Chunk.Decode(huge)));
        Assert.Throws<InvalidDataException>((Action)(() => PreparedMp3FrameData.Chunk.Decode(encoded.Concat(new byte[] { 0 }).ToArray())));
        chunk.Operations[0].Kind = PreparedMp3FrameData.OperationKind.Reset;
        Assert.Throws<InvalidDataException>((Action)(() => chunk.Encode()));
    }

    [Test]
    public void CompleteTapeCrossesChunkBoundaryReopensAndOwnsAppendedBytes()
    {
        using (var cache = new PreparedAudioCache(root, Policy()))
        {
            using var writer = cache.CreateMp3TapeWriter(Key)!;
            for (int i = 0; i < 34; i++)
            {
                var operation = Frame(PreparedMp3FrameData.MaximumPcmBytes, (byte)i);
                Assert.That(writer.Append(operation), Is.True);
                operation.Pcm[0] = 255; operation.Compressed[0] = 0; operation.Math[1] = 0;
            }
            Assert.That(cache.OpenMp3Tape(Key), Is.Null, "Published chunks alone are not a complete tape.");
            Assert.That(writer.Complete(), Is.True);
            Assert.That(writer.Publications, Is.EqualTo(3));
            Assert.That(writer.Append(Frame()), Is.False);
            Assert.That(writer.Complete(), Is.False);
        }
        using var reopened = new PreparedAudioCache(root, Policy());
        using var reader = reopened.OpenMp3Tape(Key)!;
        Assert.That(reader.OperationCount, Is.EqualTo(34));
        for (int i = 0; i < 34; i++)
        {
            var operation = reader.Next()!;
            Assert.That(operation.Pcm.Length, Is.EqualTo(PreparedMp3FrameData.MaximumPcmBytes));
            Assert.That(operation.Pcm.All(value => value == (byte)i), Is.True);
            Assert.That(operation.Compressed[0], Is.EqualTo(255));
            Assert.That(operation.Math[1], Is.EqualTo(0x1F80));
        }
        Assert.That(reader.Complete, Is.True);
        Assert.That(reader.Failed, Is.False);
        Assert.That(reader.Next(), Is.Null);
    }

    [Test]
    public void MissingLaterChunkFailsAtBoundaryWithoutInventingOutput()
    {
        using var cache = new PreparedAudioCache(root, Policy());
        using (var writer = cache.CreateMp3TapeWriter(Key)!)
        {
            for (int i = 0; i < 34; i++) Assert.That(writer.Append(Frame(65536, (byte)i)), Is.True);
            Assert.That(writer.Complete(), Is.True);
        }
        var manifest = cache.ReadMp3Manifest(Key)!;
        Assert.That(manifest.Chunks.Count, Is.EqualTo(2));
        using var reader = cache.OpenMp3Tape(Key)!;
        File.Delete(Path.Combine(root, "WakeUp", "PreparedAudio", "v1", manifest.Chunks[1].Key + ".bin"));
        for (int i = 0; i < manifest.Chunks[0].Operations; i++) Assert.That(reader.Next(), Is.Not.Null);
        Assert.That(reader.Next(), Is.Null);
        Assert.That(reader.Failed, Is.True);
        Assert.That(reader.Complete, Is.False);
        Assert.That(cache.PublishMp3Manifest(Key, manifest), Is.False);
    }

    [Test]
    public void CorruptChunkAndIncompleteManifestAreTransparentMisses()
    {
        using var cache = new PreparedAudioCache(root, Policy());
        var chunk = new PreparedMp3FrameData.Chunk(); chunk.Operations.Add(Frame());
        string chunkKey = PreparedMp3FrameData.ChunkKey(chunk.Encode());
        var manifest = new PreparedMp3FrameData.Manifest { OperationCount = 1, PcmBytes = 7 };
        manifest.Chunks.Add(new PreparedMp3FrameData.ChunkReference { Key = chunkKey, Operations = 1, Bytes = chunk.EncodedBytes });
        Assert.That(cache.PublishMp3Manifest(Key, manifest), Is.False);
        Assert.That(cache.ReadMp3Manifest(Key), Is.Null);
        Assert.That(cache.PublishMp3Chunk(Key, chunk), Is.False, "Content key must identify the complete encoded chunk.");
        Assert.That(cache.PublishMp3Chunk(chunkKey, chunk), Is.True);
        Assert.That(cache.PublishMp3Manifest(Key, manifest), Is.True);
        string path = Path.Combine(root, "WakeUp", "PreparedAudio", "v1", chunkKey + ".bin");
        byte[] bytes = File.ReadAllBytes(path); bytes[bytes.Length - 1] ^= 0x80; File.WriteAllBytes(path, bytes);
        using var reader = cache.OpenMp3Tape(Key)!;
        Assert.That(reader.Next(), Is.Null);
        Assert.That(reader.Failed, Is.True);
    }

    [Test]
    public void QuotaAbortAndInvalidOperationNeverPublishACompleteManifest()
    {
        using var cache = new PreparedAudioCache(root, Policy(), maximumStore: 128);
        using var writer = cache.CreateMp3TapeWriter(Key)!;
        Assert.That(writer.Append(Frame(1000)), Is.True);
        Assert.That(writer.Complete(), Is.False);
        Assert.That(writer.Failed, Is.True);
        Assert.That(cache.OpenMp3Tape(Key), Is.Null);
        using var invalid = cache.CreateMp3TapeWriter(Key)!;
        var operation = Frame(); operation.FileOffset = -1;
        Assert.That(invalid.Append(operation), Is.False);
        Assert.That(invalid.Complete(), Is.False);
        Assert.That(cache.OpenMp3Tape(Key), Is.Null);
    }

    [Test]
    public void ManifestRejectsFalseTotalsOldVersionAndTrailingData()
    {
        var chunk = new PreparedMp3FrameData.Chunk(); chunk.Operations.Add(Frame());
        var manifest = new PreparedMp3FrameData.Manifest { OperationCount = 1, PcmBytes = 7 };
        manifest.Chunks.Add(new PreparedMp3FrameData.ChunkReference {
            Key = PreparedMp3FrameData.ChunkKey(chunk.Encode()), Operations = 1, Bytes = chunk.EncodedBytes });
        byte[] encoded = manifest.Encode();
        Assert.That(PreparedMp3FrameData.Manifest.Decode(encoded).Encode(), Is.EqualTo(encoded));
        byte[] old = (byte[])encoded.Clone(); old[4] = 0;
        Assert.Throws<InvalidDataException>((Action)(() => PreparedMp3FrameData.Manifest.Decode(old)));
        Assert.Throws<InvalidDataException>((Action)(() => PreparedMp3FrameData.Manifest.Decode(encoded.Take(encoded.Length - 1).ToArray())));
        Assert.Throws<InvalidDataException>((Action)(() => PreparedMp3FrameData.Manifest.Decode(encoded.Concat(new byte[] { 0 }).ToArray())));
        manifest.OperationCount = 2;
        Assert.Throws<InvalidDataException>((Action)(() => manifest.Encode()));
    }
}
