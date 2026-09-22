// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace WakeUp;

// Cache bytes are optional. The caller continues native loading on every miss.
internal sealed class PreparedAudioCache : IDisposable
{
    private readonly object gate = new();
    private OwnedCacheStore? store;
    // Include the owned-store integrity envelope in the per-entry ceiling.
    private const int MaximumPayload = PreparedAudioData.MaximumBytes - OwnedCacheStore.EnvelopeBytes;
    internal long ReadBytes, WrittenBytes, Hits, Misses, Writes;

    internal PreparedAudioCache(string saveDataRoot, CacheLaunchPolicy? policy = null, long? maximumStore = null)
    {
        try
        {
            var selected = policy ?? CacheLaunchPolicy.Current;
            string root = Path.Combine(saveDataRoot, "WakeUp", "PreparedAudio", "v1");
            long capacity = maximumStore ?? (selected.Action == CacheAction.Bypass ? null
                : SharedCacheBudget.ForRoot(root)?.MaximumBytes) ?? SharedCacheBudget.DefaultMiB * 1024L * 1024;
            if (capacity < 1) return;
            store = new OwnedCacheStore(root, MaximumPayload, capacity, selected);
        }
        catch (Exception error) when (OptionalFailure(error)) { }
    }

    internal PreparedAudioData? Read(string key)
    {
        lock (gate)
        {
            try
            {
                int length = 0;
                var result = store?.Read(key, bytes =>
                {
                    var decoded = PreparedAudioData.Decode(bytes);
                    length = bytes.Length;
                    return decoded;
                });
                if (result != null) { Hits++; ReadBytes += length; return result; }
            }
            catch (Exception error) when (OptionalFailure(error)) { }
            Misses++;
            return null;
        }
    }

    internal bool Publish(string key, PreparedAudioData data)
    {
        lock (gate)
        {
            if (store == null || data == null || !OwnedCacheStore.IsKey(key)) return false;
            try
            {
                byte[] payload = data.Encode();
                if (payload.Length > MaximumPayload || !store.Publish(key, payload)) return false;
                Writes++; WrittenBytes += payload.Length;
                return true;
            }
            catch (Exception error) when (OptionalFailure(error)) { return false; }
        }
    }

    // Index keys include their own source/native identity discriminator. Both
    // payload kinds share this exact store, launch policy, inventory and budget.
    internal PreparedMp3IndexData? ReadIndex(string key)
    {
        lock (gate)
        {
            try
            {
                int length = 0;
                var result = store?.Read(key, bytes =>
                {
                    var decoded = PreparedMp3IndexData.Decode(bytes);
                    length = bytes.Length;
                    return decoded;
                });
                if (result != null) { Hits++; ReadBytes += length; return result; }
            }
            catch (Exception error) when (OptionalFailure(error)) { }
            Misses++;
            return null;
        }
    }

    internal bool PublishIndex(string key, PreparedMp3IndexData data)
    {
        lock (gate)
        {
            if (store == null || data == null || !OwnedCacheStore.IsKey(key)) return false;
            try
            {
                byte[] payload = data.Encode();
                if (payload.Length > MaximumPayload || !store.Publish(key, payload)) return false;
                Writes++; WrittenBytes += payload.Length;
                return true;
            }
            catch (Exception error) when (OptionalFailure(error)) { return false; }
        }
    }

    internal PreparedMp3FrameData.Writer? CreateMp3TapeWriter(string key)
    {
        lock (gate) return store != null && OwnedCacheStore.IsKey(key) ? new PreparedMp3FrameData.Writer(this, key) : null;
    }

    internal PreparedMp3FrameData.Reader? OpenMp3Tape(string key)
    {
        var manifest = ReadMp3Manifest(key);
        return manifest == null ? null : new PreparedMp3FrameData.Reader(this, manifest);
    }

    internal PreparedMp3FrameData.Chunk? ReadMp3Chunk(string key)
        => ReadMp3Payload(key, bytes =>
        {
            if (PreparedMp3FrameData.ChunkKey(bytes) != key) throw new InvalidDataException("mp3-chunk-key");
            return PreparedMp3FrameData.Chunk.Decode(bytes);
        });

    internal PreparedMp3FrameData.Manifest? ReadMp3Manifest(string key)
        => ReadMp3Payload(key, PreparedMp3FrameData.Manifest.Decode);

    private T? ReadMp3Payload<T>(string key, Func<byte[], T> decode) where T : class
    {
        lock (gate)
        {
            try
            {
                int length = 0;
                var result = store?.Read(key, bytes => { var value = decode(bytes); length = bytes.Length; return value; });
                if (result != null) { Hits++; ReadBytes += length; return result; }
            }
            catch (Exception error) when (OptionalFailure(error)) { }
            Misses++; return null;
        }
    }

    internal bool PublishMp3Chunk(string key, PreparedMp3FrameData.Chunk chunk)
    {
        lock (gate)
        {
            try
            {
                byte[] encoded = chunk.Encode();
                return PreparedMp3FrameData.ChunkKey(encoded) == key && PublishMp3Payload(key, encoded);
            }
            catch (Exception error) when (OptionalFailure(error)) { return false; }
        }
    }

    internal bool PublishMp3Manifest(string key, PreparedMp3FrameData.Manifest manifest)
    {
        lock (gate)
        {
            if (store == null || !OwnedCacheStore.IsKey(key)) return false;
            try
            {
                byte[] encoded = manifest.Encode();
                // Verify one stored chunk at a time. A failed earlier write,
                // changed chunk or quota failure never creates a complete tape.
                int ordinal = 0; long pcm = 0;
                foreach (var reference in manifest.Chunks)
                {
                    var chunk = ReadMp3Chunk(reference.Key);
                    if (chunk == null || chunk.FirstOperation != ordinal || chunk.Operations.Count != reference.Operations
                        || chunk.EncodedBytes != reference.Bytes) return false;
                    ordinal += chunk.Operations.Count;
                    foreach (var operation in chunk.Operations) pcm += operation.Pcm.Length;
                }
                return ordinal == manifest.OperationCount && pcm == manifest.PcmBytes && PublishMp3Payload(key, encoded);
            }
            catch (Exception error) when (OptionalFailure(error)) { return false; }
        }
    }

    // Caller holds the same cache gate used by every audio payload category.
    private bool PublishMp3Payload(string key, byte[] encoded)
    {
        if (store == null || !OwnedCacheStore.IsKey(key) || encoded.Length > MaximumPayload || !store.Publish(key, encoded)) return false;
        Writes++; WrittenBytes += encoded.Length; return true;
    }

    // Only the native filesystem-load caller admits the source. No pathname,
    // reopened handle, timestamp or platform-selected hash provider is used.
    // hashedBytes counts actual source reads, including a failed attempt.
    internal static string? SourceKey(FileStream source, string runtimeIdentity, string format, out long hashedBytes)
    {
        hashedBytes = 0;
        long original;
        try
        {
            if (source == null || !source.CanRead || !source.CanSeek
                || string.IsNullOrEmpty(runtimeIdentity) || string.IsNullOrEmpty(format)
                || runtimeIdentity.Length > 65536 || format.Length > 256) return null;
            original = source.Position;
            if (original != 0) return null;
        }
        catch (Exception error) when (OptionalFailure(error)) { return null; }

        string? result = null;
        try
        {
            source.Position = 0;
            byte[] digest = PreparedAudioSourceHash.Compute(source, out hashedBytes);
            using var identity = new MemoryStream();
            using (var writer = new BinaryWriter(identity, new UTF8Encoding(false, true), true))
            {
                writer.Write("WakeUp.PreparedAudio"); writer.Write(1);
                writer.Write(runtimeIdentity); writer.Write(format);
                writer.Write(hashedBytes); writer.Write(digest);
            }
            using var identityHash = new SHA256Managed();
            result = BitConverter.ToString(identityHash.ComputeHash(identity.ToArray())).Replace("-", "");
        }
        catch (Exception error) when (OptionalFailure(error)) { }
        finally
        {
            try { source.Position = original; }
            catch (Exception error) when (OptionalFailure(error)) { result = null; }
        }
        return result;
    }

    private static bool OptionalFailure(Exception error) => error is InvalidDataException || error is IOException || error is UnauthorizedAccessException
        || error is ArgumentException || error is NotSupportedException || error is ObjectDisposedException
        || error is OverflowException || error is SecurityException || error is CryptographicException;

    public void Dispose()
    {
        lock (gate)
        {
            var previous = store; store = null;
            try { previous?.Dispose(); }
            catch (Exception error) when (OptionalFailure(error)) { }
        }
    }
}
