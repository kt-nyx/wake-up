// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace WakeUp;

// One language session/thread owns this scratch storage. Large keys remain
// eligible but their buffers are not retained between files. No bytes escape.
internal sealed class ParsedLanguageKeyBuffer : IDisposable
{
    internal const int MaximumRetainedCapacity = 256 * 1024;
    private readonly int owner = Thread.CurrentThread.ManagedThreadId;
    private MemoryStream? stream;
    private BinaryWriter? writer;
    private bool busy, disposed;
    internal int RetainedCapacity => stream?.Capacity ?? 0;

    internal BinaryWriter Begin()
    {
        if (disposed) throw new ObjectDisposedException(nameof(ParsedLanguageKeyBuffer));
        if (owner != Thread.CurrentThread.ManagedThreadId || busy) throw new InvalidOperationException("language-key-owner");
        if (stream == null)
        {
            stream = new MemoryStream();
            writer = new BinaryWriter(stream, Encoding.UTF8, true);
        }
        stream.SetLength(0); stream.Position = 0; busy = true;
        return writer!;
    }

    internal string Digest()
    {
        writer!.Flush();
        return BitConverter.ToString(CacheDigest.Hash(stream!.GetBuffer(), 0, checked((int)stream.Length))).Replace("-", "");
    }

    internal void Release(bool retain)
    {
        busy = false;
        if (!retain || stream!.Capacity > MaximumRetainedCapacity) Drop();
        else { stream.SetLength(0); stream.Position = 0; }
    }

    // Cross-thread invalidation only marks the session. Its owner drops scratch
    // storage when the current key unwinds or the load ends, never concurrently
    // with a write. Same-thread idle invalidation can release it immediately.
    internal void DiscardIdle()
    { if (owner == Thread.CurrentThread.ManagedThreadId && !busy) Drop(); }

    private void Drop()
    { writer?.Dispose(); stream?.Dispose(); writer = null; stream = null; }
    public void Dispose() { disposed = true; Drop(); }
}
