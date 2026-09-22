// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace WakeUp;

internal static class TexturePreparationHelper
{
    internal const int MaximumOutput = 8 * 1024 * 1024 - 65536;
    internal const string Contract = "srgb-color-box-straight-alpha-bc1-bc3-v2";
    internal static string Platform => Environment.OSVersion.Platform == PlatformID.Win32NT ? "win-x64" : "linux-x64";
    internal static string ExactPath(string modRoot) => Path.Combine(modRoot, "Tools", Platform,
        Platform == "win-x64" ? "WakeUp.TextureHelper.exe" : "WakeUp.TextureHelper");
    internal static string Identity(string path)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT || !Environment.Is64BitProcess || !Path.IsPathRooted(path))
            throw new NotSupportedException("helper-platform-windows-x64-only");
        OwnedCacheStore.RejectLinkedPath(path);
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 16 * 1024 * 1024) throw new IOException("helper-missing-or-size");
        return "stdio-v2|" + Contract + "|" + Platform + "|" + PngCache.Hex(PngCache.Hash(File.ReadAllBytes(path)));
    }
    internal static PngCache.Entry Convert(string path, string expectedHelper, byte[] source,
        PreparationImageHeader header, int preset, CancellationToken cancellation, Action<Process>? childStarted = null)
        => ConvertCore(path, expectedHelper, source, header, preset, cancellation, false, childStarted).Entry;

    internal static byte[] ConvertDds(string path, string expectedHelper, byte[] source,
        PreparationImageHeader header, int preset, CancellationToken cancellation, Action<Process>? childStarted = null)
        => ConvertCore(path, expectedHelper, source, header, preset, cancellation, true, childStarted).Dds!;

    private static (PngCache.Entry Entry, byte[]? Dds) ConvertCore(string path, string expectedHelper, byte[] source,
        PreparationImageHeader header, int preset, CancellationToken cancellation, bool exportDds, Action<Process>? childStarted)
    {
        if (preset < 1 || preset > 3 || !header.LossyFits(preset) || source.Length > PreparationImageHeader.MaximumSource
            || header.IsDds && !exportDds || (long)header.Width * header.Height * 4 > PreparationImageHeader.MaximumDecoded)
            throw new InvalidDataException("helper-admission");
        if (Identity(path) != expectedHelper) throw new InvalidDataException("helper-changed");
        cancellation.ThrowIfCancellationRequested();
        using var child = new Process { StartInfo = new ProcessStartInfo {
            FileName = path, Arguments = "--stdio-v2", UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(path)!
        }};
        if (!child.Start()) throw new IOException("helper-start");
        // A private child only. Cancellation closes it and joins it before the
        // controller can resume. No process names, shell or descendant scans.
        void Stop() { try { if (!child.HasExited) child.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { } }
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var cancel = cancellation.Register(Stop);
        using var timeout = deadline.Token.Register(Stop);
        child.ErrorDataReceived += (_, __) => { }; // drain diagnostics without retaining arbitrary output
        try
        {
            child.BeginErrorReadLine();
            childStarted?.Invoke(child);
            using var r = new BinaryReader(child.StandardOutput.BaseStream, Encoding.ASCII, true);
            if (Encoding.ASCII.GetString(r.ReadBytes(16)) != "WUTXHELPER000002") throw new InvalidDataException("helper-handshake");
            if (Encoding.ASCII.GetString(r.ReadBytes(Contract.Length)) != Contract) throw new InvalidDataException("helper-contract");
            using (var w = new BinaryWriter(child.StandardInput.BaseStream, Encoding.ASCII, true))
            {
                w.Write(Encoding.ASCII.GetBytes("WUTXREQ2")); w.Write(preset); w.Write(exportDds ? 1 : 0); w.Write(header.Width); w.Write(header.Height);
                w.Write(source.Length); w.Write(source); w.Flush();
            }
            child.StandardInput.Close();
            byte[]? dds = exportDds ? ReadDdsResponse(r, header, preset) : null;
            var entry = dds != null ? PreparedDds.Parse(dds) : ReadResponse(r, header, preset);
            if (r.BaseStream.ReadByte() != -1) throw new InvalidDataException("helper-trailing-output");
            child.WaitForExit();
            cancellation.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested || child.ExitCode != 0) throw new IOException("helper-failed-or-timeout");
            return (entry, dds);
        }
        catch { cancellation.ThrowIfCancellationRequested(); throw; }
        finally { Stop(); child.WaitForExit(); }
    }
    internal static PngCache.Entry ReadResponse(BinaryReader r, PreparationImageHeader header, int preset)
    {
        if (preset < 1 || preset > 3) throw new InvalidDataException("helper-preset");
        if (Encoding.ASCII.GetString(r.ReadBytes(8)) != "WUTXRES2" || r.ReadInt32() != 0) throw new InvalidDataException("helper-result");
        var e = new PngCache.Entry { Width = r.ReadInt32(), Height = r.ReadInt32(), TextureFormat = r.ReadInt32(), Mips = r.ReadInt32() };
        int length = r.ReadInt32(), divisor = 1 << (preset - 1);
        if (e.Width != header.Width / divisor || e.Height != header.Height / divisor || e.Width < 4 || e.Height < 4
            || e.Mips != PreparationImageHeader.Mips(e.Width, e.Height) || e.TextureFormat != 10 && e.TextureFormat != 12
            || length < 1 || length > MaximumOutput || length != PngCache.ExpectedBytes(e.Width, e.Height, e.TextureFormat, e.Mips))
            throw new InvalidDataException("helper-layout");
        e.Pixels = r.ReadBytes(length);
        if (e.Pixels.Length != length) throw new EndOfStreamException();
        // Reviewed native Image loader uses sRGB, trilinear, anisotropy two,
        // repeat wrapping, zero bias and discards readability on this contract.
        e.GraphicsFormat = (int)(e.TextureFormat == 10 ? UnityEngine.Experimental.Rendering.GraphicsFormat.RGBA_DXT1_SRGB
            : UnityEngine.Experimental.Rendering.GraphicsFormat.RGBA_DXT5_SRGB);
        e.Filter = 2; e.Aniso = 2;
        return e;
    }

    internal static byte[] ReadDdsResponse(BinaryReader r, PreparationImageHeader header, int preset)
    {
        if (preset < 1 || preset > 3) throw new InvalidDataException("helper-preset");
        if (Encoding.ASCII.GetString(r.ReadBytes(8)) != "WUTXRES2" || r.ReadInt32() != 0) throw new InvalidDataException("helper-result");
        int width = r.ReadInt32(), height = r.ReadInt32(), format = r.ReadInt32(), mips = r.ReadInt32(), length = r.ReadInt32();
        int divisor = 1 << (preset - 1);
        if (width != header.Width / divisor || height != header.Height / divisor || width < 4 || height < 4
            || format != 10 && format != 12 || mips != PreparationImageHeader.Mips(width, height)
            || length < 128 || length > MaximumOutput) throw new InvalidDataException("helper-dds-layout");
        var bytes = r.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
        var e = PreparedDds.Parse(bytes);
        if (e.Width != width || e.Height != height || e.TextureFormat != format || e.Mips != mips)
            throw new InvalidDataException("helper-dds-descriptor");
        return bytes;
    }
}
