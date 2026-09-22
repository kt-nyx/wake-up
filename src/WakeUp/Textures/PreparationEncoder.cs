// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace WakeUp;

// Shared explicit user-triggered client. Never called by a startup resolver.
public static class PreparationEncoder
{
    public const string Contract = "srgb-canonical-metadata-area-alpha-bc-rgba-psd-v4";
    public const int MaximumSource = 16 * 1024 * 1024;
    public const int MaximumOutput = 8 * 1024 * 1024 - 65536;
    public static string Identity(string path)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT || !Environment.Is64BitProcess || !Path.IsPathRooted(path))
            throw new NotSupportedException("helper-platform-windows-x64-only");
        OwnedCacheStore.RejectLinkedPath(path);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1 || info.Length > MaximumSource) throw new IOException("helper-missing-or-size");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length < 1 || input.Length > MaximumSource) throw new IOException("helper-size-changed");
        using var hash = System.Security.Cryptography.SHA256.Create();
        return "stdio-v3|" + Contract + "|win-x64|" + BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "");
    }
    public static PreparationPixels Convert(string path, string expectedHelper, byte[] source, int width, int height,
        PreparationOptions options, bool mask, CancellationToken cancellation, Action<Process>? childStarted = null)
    {
        var estimate = PreparationImageInspection.Inspect(source, options, mask, psdEnabled: true);
        if (!estimate.Eligible || width != estimate.Width || height != estimate.Height)
            throw new InvalidDataException("helper-admission: " + estimate.Reason);
        cancellation.ThrowIfCancellationRequested();
        // Reuse the approved optional PSD decoder; the helper does not acquire a
        // second PSD implementation. Mode 2 is explicitly bottom-up RGBA input.
        bool raw = estimate.SourceKind == "PSD";
        if (raw) source = PsdTextureDecoder.Decode(source).Pixels;
        cancellation.ThrowIfCancellationRequested();
        if (Identity(path) != expectedHelper) throw new InvalidDataException("helper-changed");
        cancellation.ThrowIfCancellationRequested();
        // Keep the verified executable open without write/delete sharing until
        // the child has exited, preventing replacement after the identity check.
        using var executable = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Identity(path) != expectedHelper) throw new InvalidDataException("helper-changed");
        using var child = new Process { StartInfo = new ProcessStartInfo {
            FileName = path, Arguments = "--stdio-v3", UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(path)!
        }};
        if (!child.Start()) throw new IOException("helper-start");
        void Stop() { try { if (!child.HasExited) child.Kill(); }
            catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var cancel = cancellation.Register(Stop);
        using var timeout = deadline.Token.Register(Stop);
        child.ErrorDataReceived += (_, __) => { };
        try
        {
            child.BeginErrorReadLine(); childStarted?.Invoke(child);
            using var r = new BinaryReader(child.StandardOutput.BaseStream, Encoding.ASCII, true);
            if (Encoding.ASCII.GetString(r.ReadBytes(16)) != "WUTXHELPER000003"
                || Encoding.ASCII.GetString(r.ReadBytes(Contract.Length)) != Contract) throw new InvalidDataException("helper-handshake");
            using (var w = new BinaryWriter(child.StandardInput.BaseStream, Encoding.ASCII, true))
            {
                w.Write(Encoding.ASCII.GetBytes("WUTXREQ3")); w.Write(options.Preset); w.Write(raw ? 2 : 0); w.Write(mask ? 2 : 1);
                w.Write(width); w.Write(height); w.Write(source.Length); w.Write(source); w.Flush();
            }
            child.StandardInput.Close();
            var result = ReadResponse(r, width, height, options, mask);
            if (r.BaseStream.ReadByte() != -1) throw new InvalidDataException("helper-trailing-output");
            child.WaitForExit(); cancellation.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested || child.ExitCode != 0) throw new IOException("helper-failed-or-timeout");
            return result;
        }
        catch { cancellation.ThrowIfCancellationRequested(); throw; }
        finally { Stop(); child.WaitForExit(); }
    }
    internal static PreparationPixels ReadResponse(BinaryReader r, int width, int height, PreparationOptions options, bool mask)
    {
        if (Encoding.ASCII.GetString(r.ReadBytes(8)) != "WUTXRES3" || r.ReadInt32() != 0) throw new InvalidDataException("helper-result");
        var output = new PreparationPixels { Width = r.ReadInt32(), Height = r.ReadInt32(), TextureFormat = r.ReadInt32(), Mips = r.ReadInt32() };
        int length = r.ReadInt32(), expectedMips = 0;
        long expectedBytes = 0;
        bool rgba = mask || Math.Max(1, width / options.Divisor) % 4 != 0 || Math.Max(1, height / options.Divisor) % 4 != 0;
        if (output.Width != Math.Max(1, width / options.Divisor) || output.Height != Math.Max(1, height / options.Divisor)
            || (rgba ? output.TextureFormat != 4 : output.TextureFormat != 10 && output.TextureFormat != 12))
            throw new InvalidDataException("helper-layout");
        for (int x = output.Width, y = output.Height; ; x = Math.Max(1, x / 2), y = Math.Max(1, y / 2))
        {
            expectedBytes += rgba ? (long)x * y * 4 : (long)((x + 3) / 4) * ((y + 3) / 4) * (output.TextureFormat == 10 ? 8 : 16);
            expectedMips++; if (x == 1 && y == 1) break;
        }
        if (output.Mips != expectedMips || length < 1 || length > MaximumOutput || length != expectedBytes)
            throw new InvalidDataException("helper-mips-or-length");
        output.Pixels = r.ReadBytes(length);
        if (output.Pixels.Length != length) throw new EndOfStreamException();
        output.Filter = options.Filter; output.Aniso = options.Anisotropy; output.Bias = options.MipBias;
        // The qualified GOG player uses Gamma color space: its texture
        // constructor reports UNorm graphics formats even for sRGB artwork.
        // Pixels stay encoded as sRGB; the game verifies this runtime condition.
        output.GraphicsFormat = rgba ? 8 : output.TextureFormat == 10 ? 97 : 101;
        return output;
    }
}
