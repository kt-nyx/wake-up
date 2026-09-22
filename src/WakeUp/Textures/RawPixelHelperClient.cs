// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WakeUp;

// Fixture-only raw trial transport. The caller reserves the child allowance
// plus both processes' source/output copies before admitting any request.
// This class does not select images, start Unity work, or activate a setting.
internal sealed class RawPixelHelperClient : IDisposable
{
    internal const int ChildReservation = 96 * 1024 * 1024;
    internal const int ParentTransportReservation = 1024 * 1024;
    internal const int MaxSource = 16 * 1024 * 1024;
    internal const int MaxPixels = 16 * 1024 * 1024;
    internal const int MaximumDimension = 8192;
    private readonly string path, expectedHash;
    private readonly object gate = new object();
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    private Process? child;
    private FileStream? executable;
    private Thread? stderr;
    private bool failed, disposed;
    private uint nextId;
    private int requests;
    internal int Starts { get; private set; }
    internal int Requests => Volatile.Read(ref requests);
    internal int Failures { get; private set; }
    internal long PeakProcessBytes { get; private set; }
    internal int? ProcessId { get; private set; }

    internal RawPixelHelperClient(string path, string expectedHash)
    {
        this.path = path; this.expectedHash = expectedHash;
    }

    // Invalid/refused framing and unavailable children throw IOException;
    // unsupported images throw NotSupportedException. Cancellation is preserved.
    // A complete unsupported response permits reuse; any broken transaction
    // disables this client rather than repeatedly restarting the helper.
    internal byte[] Decode(byte[] source, int width, int height, int kind, CancellationToken cancellation)
    {
        ValidateRequest(source.Length, width, height, kind);
        lock (gate)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RawPixelHelperClient));
            if (failed) throw new IOException("raw-helper-disabled-after-failure");
            cancellation.ThrowIfCancellationRequested();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token, deadline.Token);
            bool unsupported = false;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                bool starting = child == null;
                if (starting) Start();
                var owned = child!;
                // Registration also fires immediately if startup exhausted the
                // deadline. Killing this exact process unblocks synchronous pipe
                // reads AND writes; no I/O task can outlive the request.
                using var stop = linked.Token.Register(() => Stop(owned));
                linked.Token.ThrowIfCancellationRequested();
                if (owned.HasExited) throw new IOException("raw-helper-exited");
                using var reader = new BinaryReader(owned.StandardOutput.BaseStream, Encoding.ASCII, true);
                if (starting) ReadHandshake(reader);
                uint id = checked(++nextId);
                using (var writer = new BinaryWriter(owned.StandardInput.BaseStream, Encoding.ASCII, true))
                    WriteRequest(writer, id, source, width, height, kind);
                Interlocked.Increment(ref requests);
                var result = ReadResponseCore(reader, id, width, height, out unsupported);
                linked.Token.ThrowIfCancellationRequested();
                if (owned.HasExited) throw new IOException("raw-helper-exited-after-response");
                owned.Refresh();
                PeakProcessBytes = Math.Max(PeakProcessBytes, owned.PeakWorkingSet64);
                return result;
            }
            catch (NotSupportedException) when (unsupported && !linked.IsCancellationRequested)
            {
                // All six fields of this refusal were validated, so the next
                // transaction begins at a known frame boundary in the same child.
                Failures++; throw;
            }
            catch (Exception error)
            {
                failed = true; Failures++;
                CloseChild();
                cancellation.ThrowIfCancellationRequested();
                if (lifetime.IsCancellationRequested) throw new OperationCanceledException("raw-helper-disposed", error, lifetime.Token);
                if (deadline.IsCancellationRequested) throw new IOException("raw-helper-timeout", error);
                if (error is IOException || error is NotSupportedException) throw;
                throw new IOException("raw-helper-failed", error);
            }
        }
    }

    private void Start()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT || !Environment.Is64BitProcess || !Path.IsPathRooted(path))
            throw new NotSupportedException("raw-helper-windows-x64-only");
        OwnedCacheStore.RejectLinkedPath(path);
        executable = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (executable.Length < 1 || executable.Length > MaxSource) throw new IOException("raw-helper-executable-size");
        using (var hash = SHA256.Create())
            if (!string.Equals(BitConverter.ToString(hash.ComputeHash(executable)).Replace("-", ""), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("raw-helper-executable-identity");
        var owned = new Process { StartInfo = new ProcessStartInfo {
            FileName = path, Arguments = "--stdio-raw-v1", UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(path)!
        }};
        try
        {
            if (!owned.Start()) throw new IOException("raw-helper-start");
        }
        catch { owned.Dispose(); throw; }
        child = owned; ProcessId = owned.Id; Starts++;
        var errors = owned.StandardError.BaseStream;
        var drain = new Thread(() => Drain(errors)) { IsBackground = true, Name = "WakeUp raw helper stderr" };
        drain.Start(); stderr = drain;
    }

    private static void Drain(Stream stream)
    {
        // Do not use line-based events: an unterminated diagnostic line could
        // otherwise retain arbitrary memory in the parent process.
        var scratch = new byte[4096];
        try { while (stream.Read(scratch, 0, scratch.Length) != 0) { } }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private static void Stop(Process owned)
    {
        try { if (!owned.HasExited) owned.Kill(); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private void CloseChild()
    {
        var owned = child;
        if (owned != null)
        {
            Stop(owned);
            owned.WaitForExit();
            stderr?.Join();
            owned.Dispose(); child = null; stderr = null;
        }
        executable?.Dispose(); executable = null;
    }

    public void Dispose()
    {
        // Cancel before taking the decode lock, allowing disposal to interrupt
        // an active read/write and then join that exact transaction.
        lifetime.Cancel();
        lock (gate)
        {
            if (disposed) return;
            disposed = true; CloseChild();
        }
    }

    internal static void ValidateRequest(int length, int width, int height, int kind)
    {
        if (length < 1 || length > MaxSource || width < 1 || height < 1
            || width > MaximumDimension || height > MaximumDimension
            || (long)width * height * 4 > MaxPixels || kind != 1 && kind != 2)
            throw new NotSupportedException("raw-helper-admission");
    }

    internal static void ReadHandshake(BinaryReader reader)
    {
        if (Encoding.ASCII.GetString(reader.ReadBytes(16)) != "WUTXRAWHELP00001"
            || reader.ReadUInt32() != ChildReservation)
            throw new InvalidDataException("raw-helper-handshake");
    }

    internal static void WriteRequest(BinaryWriter writer, uint id, byte[] source, int width, int height, int kind)
    {
        ValidateRequest(source.Length, width, height, kind);
        writer.Write(Encoding.ASCII.GetBytes("WUTXRAWQ")); writer.Write(id);
        writer.Write(width); writer.Write(height); writer.Write(kind); writer.Write(source.Length);
        writer.Write(source); writer.Flush();
    }

    internal static byte[] ReadResponse(BinaryReader reader, uint expectedId, int width, int height)
        => ReadResponseCore(reader, expectedId, width, height, out _);

    private static byte[] ReadResponseCore(BinaryReader reader, uint expectedId, int width, int height, out bool unsupported)
    {
        unsupported = false;
        if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "WUTXRAWR" || reader.ReadUInt32() != expectedId)
            throw new InvalidDataException("raw-helper-response-identity");
        uint status = reader.ReadUInt32(), actualWidth = reader.ReadUInt32(), actualHeight = reader.ReadUInt32();
        uint format = reader.ReadUInt32(), length = reader.ReadUInt32();
        if (status != 0)
        {
            if (status > 2 || actualWidth != 0 || actualHeight != 0 || format != 0 || length != 0)
                throw new InvalidDataException("raw-helper-error-frame");
            if (status == 1) { unsupported = true; throw new NotSupportedException("raw-helper-unsupported"); }
            throw new InvalidDataException("raw-helper-decode-failed");
        }
        if (width < 1 || height < 1 || width > MaximumDimension || height > MaximumDimension
            || actualWidth != width || actualHeight != height || format != 4 || length == 0
            || length > MaxPixels || length != (long)width * height * 4)
            throw new InvalidDataException("raw-helper-response-layout");
        // ReadBytes can allocate a second, shortened array on truncation. The
        // queue charges exactly one parent output, including failure paths.
        var pixels = new byte[(int)length];
        int offset = 0;
        while (offset < pixels.Length)
        {
            int count = reader.Read(pixels, offset, pixels.Length - offset);
            if (count == 0) throw new EndOfStreamException("raw-helper-truncated-pixels");
            offset += count;
        }
        return pixels;
    }
}
