// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace WakeUp;

// Ordered native frame/reset results. Chunks bound resident PCM; the final
// manifest is published only after the caller confirms a complete recording.
internal static class PreparedMp3FrameData
{
    internal const string CacheIdentity = "mp3-selected-provider-frame-tape-v2";
    internal const int MaximumChunkBytes = PreparedAudioData.MaximumBytes - OwnedCacheStore.EnvelopeBytes;
    internal const int MaximumChunkOperations = 4096, MaximumOperations = 65536, MaximumChunks = 256;
    internal const int MaximumHistoryBytes = PreparedAudioData.MaximumBytes;
    internal const int MaximumReplayBytes = MaximumHistoryBytes;
    internal const int MaximumFrameBytes = 65536, MaximumPcmBytes = 65536;
    private const int ChunkMagic = 0x3143464D, ManifestMagic = 0x314D464D, Version = 2;
    internal enum OperationKind : byte { Frame = 1, Reset = 2 }

    internal sealed class Operation
    {
        internal OperationKind Kind;
        internal long FileOffset;
        internal int DestinationOffset;
        internal byte[] Compressed = Array.Empty<byte>(), Pcm = Array.Empty<byte>();
        internal ulong[] Math = new ulong[8];
        internal int Returned => Pcm.Length;
        internal int EncodedBytes => 85 + Compressed.Length + Pcm.Length;
        internal Operation Copy() => new Operation { Kind = Kind, FileOffset = FileOffset, DestinationOffset = DestinationOffset,
            Compressed = (byte[])Compressed.Clone(), Pcm = (byte[])Pcm.Clone(), Math = (ulong[])Math.Clone() };
        internal void Validate()
        {
            if (Compressed == null || Pcm == null || Math == null || Math.Length != 8) throw new InvalidDataException("frame-null");
            if (Kind == OperationKind.Reset)
                Require(FileOffset == 0 && DestinationOffset == 0 && Compressed.Length == 0 && Pcm.Length == 0, "reset-data");
            else
                Require(Kind == OperationKind.Frame && FileOffset >= 0 && DestinationOffset >= 0
                    && Compressed.Length > 0 && Compressed.Length <= MaximumFrameBytes
                    && Pcm.Length <= MaximumPcmBytes && (long)DestinationOffset + Pcm.Length <= MaximumChunkBytes, "frame-layout");
        }
    }

    internal sealed class Chunk
    {
        internal int FirstOperation;
        internal readonly List<Operation> Operations = new();
        internal int EncodedBytes => 16 + SumBytes();
        private int SumBytes() { int result = 0; foreach (var op in Operations) result = checked(result + op.EncodedBytes); return result; }
        internal byte[] Encode()
        {
            Validate();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(ChunkMagic); writer.Write(Version); writer.Write(FirstOperation); writer.Write(Operations.Count);
            foreach (var op in Operations)
            {
                writer.Write((byte)op.Kind); writer.Write(op.FileOffset); writer.Write(op.DestinationOffset);
                writer.Write(op.Compressed.Length); writer.Write(op.Pcm.Length);
                foreach (ulong value in op.Math) writer.Write(value);
                writer.Write(op.Compressed); writer.Write(op.Pcm);
            }
            return stream.ToArray();
        }
        internal static Chunk Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 16 || bytes.Length > MaximumChunkBytes) throw new InvalidDataException("chunk-size");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            Require(reader.ReadInt32() == ChunkMagic && reader.ReadInt32() == Version, "chunk-version");
            var result = new Chunk { FirstOperation = reader.ReadInt32() };
            int count = reader.ReadInt32();
            Require(count > 0 && count <= MaximumChunkOperations && count <= (stream.Length - stream.Position) / 85, "chunk-count");
            for (int i = 0; i < count; i++)
            {
                var op = new Operation { Kind = (OperationKind)reader.ReadByte(), FileOffset = reader.ReadInt64(), DestinationOffset = reader.ReadInt32() };
                int compressed = reader.ReadInt32(), pcm = reader.ReadInt32();
                Require(compressed >= 0 && compressed <= MaximumFrameBytes && pcm >= 0 && pcm <= MaximumPcmBytes
                    && (long)compressed + pcm <= stream.Length - stream.Position, "chunk-operation-size");
                Require(stream.Length - stream.Position >= 64L + compressed + pcm, "chunk-math-size");
                for (int j = 0; j < op.Math.Length; j++) op.Math[j] = reader.ReadUInt64();
                op.Compressed = reader.ReadBytes(compressed); op.Pcm = reader.ReadBytes(pcm);
                result.Operations.Add(op);
            }
            Require(stream.Position == stream.Length, "chunk-trailing");
            result.Validate(); return result;
        }
        internal void Validate()
        {
            Require(FirstOperation >= 0 && Operations.Count > 0 && Operations.Count <= MaximumChunkOperations
                && (long)FirstOperation + Operations.Count <= MaximumOperations, "chunk-range");
            foreach (var op in Operations) { if (op == null) throw new InvalidDataException("chunk-null-operation"); op.Validate(); }
            Require(EncodedBytes <= MaximumChunkBytes, "chunk-budget");
        }
    }

    internal sealed class ChunkReference
    {
        internal string Key = "";
        internal int Operations, Bytes;
    }
    internal sealed class Manifest
    {
        internal int OperationCount;
        internal long PcmBytes;
        internal readonly List<ChunkReference> Chunks = new();
        internal byte[] Encode()
        {
            Validate();
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(ManifestMagic); writer.Write(Version); writer.Write(OperationCount); writer.Write(PcmBytes); writer.Write(Chunks.Count);
            foreach (var chunk in Chunks)
            { writer.Write(System.Text.Encoding.ASCII.GetBytes(chunk.Key)); writer.Write(chunk.Operations); writer.Write(chunk.Bytes); }
            return stream.ToArray();
        }
        internal static Manifest Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 24 || bytes.Length > 24 + MaximumChunks * 72) throw new InvalidDataException("manifest-size");
            using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream);
            Require(reader.ReadInt32() == ManifestMagic && reader.ReadInt32() == Version, "manifest-version");
            var result = new Manifest { OperationCount = reader.ReadInt32(), PcmBytes = reader.ReadInt64() };
            int count = reader.ReadInt32();
            Require(count > 0 && count <= MaximumChunks && stream.Length - stream.Position == count * 72L, "manifest-count");
            for (int i = 0; i < count; i++) result.Chunks.Add(new ChunkReference {
                Key = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(64)), Operations = reader.ReadInt32(), Bytes = reader.ReadInt32() });
            result.Validate(); return result;
        }
        internal void Validate()
        {
            Require(Chunks.Count > 0 && Chunks.Count <= MaximumChunks && OperationCount > 0 && OperationCount <= MaximumOperations
                && PcmBytes >= 0 && PcmBytes <= (long)OperationCount * MaximumPcmBytes, "manifest-range");
            long count = 0;
            foreach (var chunk in Chunks)
            {
                if (chunk == null) throw new InvalidDataException("manifest-null-chunk");
                Require(OwnedCacheStore.IsKey(chunk.Key) && chunk.Operations > 0
                    && chunk.Operations <= MaximumChunkOperations && chunk.Bytes >= 16 + chunk.Operations * 85
                    && chunk.Bytes <= MaximumChunkBytes, "manifest-chunk");
                count += chunk.Operations;
            }
            Require(count == OperationCount, "manifest-total");
        }
    }

    internal static string ChunkKey(byte[] encoded)
    {
        using var hash = new SHA256Managed();
        return BitConverter.ToString(hash.ComputeHash(encoded)).Replace("-", "");
    }

    internal sealed class Writer : IDisposable
    {
        private PreparedAudioCache? cache;
        private readonly string key;
        private readonly Manifest manifest = new();
        private Chunk chunk = new();
        private int chunkBytes = 16;
        private bool failed, completed;
        internal bool Failed => failed;
        internal int OperationCount => manifest.OperationCount;
        internal int Publications => manifest.Chunks.Count + (completed ? 1 : 0);
        internal Writer(PreparedAudioCache cache, string key) { this.cache = cache; this.key = key; }
        internal bool Append(Operation operation)
        {
            if (failed || completed || cache == null) return false;
            try
            {
                operation.Validate();
                if (manifest.OperationCount >= MaximumOperations) return Fail();
                if (chunk.Operations.Count > 0 && (chunk.Operations.Count == MaximumChunkOperations
                    || (long)chunkBytes + operation.EncodedBytes > MaximumChunkBytes))
                    if (!Flush()) return Fail();
                chunk.Operations.Add(operation.Copy());
                chunkBytes += operation.EncodedBytes;
                manifest.OperationCount++; manifest.PcmBytes += operation.Pcm.Length;
                return true;
            }
            catch (Exception error) when (Optional(error)) { return Fail(); }
        }
        private bool Flush()
        {
            if (chunk.Operations.Count == 0) return true;
            if (manifest.Chunks.Count == MaximumChunks) return false;
            byte[] encoded = chunk.Encode(); string chunkKey = ChunkKey(encoded);
            if (!cache!.PublishMp3Chunk(chunkKey, chunk)) return false;
            manifest.Chunks.Add(new ChunkReference { Key = chunkKey, Operations = chunk.Operations.Count, Bytes = encoded.Length });
            chunk = new Chunk { FirstOperation = manifest.OperationCount };
            chunkBytes = 16;
            return true;
        }
        internal bool Complete()
        {
            if (failed || completed || cache == null) return false;
            try
            {
                if (!Flush() || !cache.PublishMp3Manifest(key, manifest)) return Fail();
                completed = true; cache = null; return true;
            }
            catch (Exception error) when (Optional(error)) { return Fail(); }
        }
        private bool Fail() { failed = true; chunk = new Chunk(); cache = null; return false; }
        public void Dispose() { cache = null; chunk = new Chunk(); }
    }

    internal sealed class Reader : IDisposable
    {
        private PreparedAudioCache? cache;
        private readonly Manifest manifest;
        private Chunk? chunk;
        private int chunkIndex, localIndex, ordinal;
        private long pcmBytes;
        internal bool Failed { get; private set; }
        internal bool Complete => !Failed && ordinal == manifest.OperationCount;
        internal int OperationCount => manifest.OperationCount;
        internal Reader(PreparedAudioCache cache, Manifest manifest) { this.cache = cache; this.manifest = manifest; }
        internal Operation? Next()
        {
            if (Failed || Complete || cache == null) return null;
            if (chunk == null || localIndex == chunk.Operations.Count)
            {
                var reference = manifest.Chunks[chunkIndex++];
                chunk = cache.ReadMp3Chunk(reference.Key);
                localIndex = 0;
                if (chunk == null || chunk.FirstOperation != ordinal || chunk.Operations.Count != reference.Operations
                    || chunk.EncodedBytes != reference.Bytes) return Fail();
            }
            Operation result = chunk.Operations[localIndex++];
            long total = pcmBytes + result.Pcm.Length;
            if (total > manifest.PcmBytes || (ordinal + 1 == manifest.OperationCount && total != manifest.PcmBytes)) return Fail();
            pcmBytes = total; ordinal++;
            return result;
        }
        private Operation? Fail() { Failed = true; chunk = null; cache = null; return null; }
        public void Dispose() { chunk = null; cache = null; }
    }
    private static bool Optional(Exception error) => error is InvalidDataException || error is IOException || error is ArgumentException || error is OverflowException;
    private static void Require(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
}
