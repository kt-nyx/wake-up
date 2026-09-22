// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NAudio.Wave;
using NAudio.Wave.Compression;

namespace WakeUp;

// Isolated native-entry experiment only. The adapter observes the originally
// selected stream, never opens/selects a substitute codec or changes registration.
internal static class PreparedMp3Provider
{
    private const string Library = "mono-profiler-wakeupentry";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Conversion = typeof(AcmMp3FrameDecompressor).GetField("conversionStream", Fields)!;
    private static readonly FieldInfo Handle = typeof(AcmStream).GetField("streamHandle", Fields)!;
    private static readonly FieldInfo SourceFormat = typeof(AcmStream).GetField("sourceFormat", Fields)!;
    private static readonly FieldInfo PcmFormat = typeof(AcmMp3FrameDecompressor).GetField("pcmFormat", Fields)!;
    private static bool installed, attempted;
    private static string status = "off", overrides = "";
    [ThreadStatic] private static Observation? observation;
    private sealed class Observation
    {
        internal GCHandle Source, Destination;
        internal byte[] SourceFormat = null!, DestinationFormat = null!;
    }
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int wakeup_acm_install(string path);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_begin(IntPtr stream, IntPtr source, uint sourceLength, IntPtr destination, uint destinationLength,
        byte[] sourceFormat, uint sourceFormatLength, byte[] destinationFormat, uint destinationFormatLength);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_end([Out] ulong[] receipt, uint count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_current(IntPtr stream, ulong token);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_stop();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong wakeup_acm_stat(int index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_overrides([Out] ulong[] values, uint count);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_begin(IntPtr stream, uint mode, ulong[] context, uint count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_end([Out] ulong[] context, uint count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_try_cached(IntPtr stream, ulong token, ulong[] context, uint count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_current_base(IntPtr stream, ulong token);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_cache_diagnostics([Out] ulong[] values, uint count);
    internal static string CacheRefusal()
    {
        var values = new ulong[12];
        return wakeup_acm_math_cache_diagnostics(values, 12) == 1 ? "cache-math:" + string.Join(",", values) : "cache-math-refused";
    }
    private static IntPtr StreamHandle(AcmMp3FrameDecompressor decoder) => (IntPtr)Handle.GetValue(Conversion.GetValue(decoder))!;
    internal static bool MathBegin(AcmMp3FrameDecompressor decoder, ulong[] context, bool replay = false)
        => installed && wakeup_acm_math_begin(StreamHandle(decoder), replay ? 1u : 0u, context, 8) == 1;
    internal static bool MathEnd(ulong[] context) => wakeup_acm_math_end(context, 8) == 1;
    internal static bool TryCached(AcmMp3FrameDecompressor decoder, ulong token, ulong[] context)
        => installed && wakeup_acm_math_try_cached(StreamHandle(decoder), token, context, 8) == 1;
    internal static bool CurrentBase(AcmMp3FrameDecompressor decoder, ulong token)
        => installed && wakeup_acm_current_base(StreamHandle(decoder), token) == 1;

    internal static bool Initialize()
    {
        try
        {
            attempted = true;
            installed = wakeup_acm_install(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "l3codeca.acm")) == 1;
            if (installed)
            {
                var selected = new ulong[16];
                installed = wakeup_acm_overrides(selected, 16) == 1 && selected[0] == 1 && selected[1] == 13;
                overrides = string.Join(",", selected);
            }
            status = installed ? "qualified-provider-math-v2" : "native-observer-refused";
        }
        catch (Exception error) { status = error.GetType().Name; }
        return installed;
    }
    internal static bool Begin(AcmMp3FrameDecompressor decoder, int length)
    {
        if (!installed || observation != null) return false;
        var scope = new Observation();
        try
        {
            var stream = (AcmStream)Conversion.GetValue(decoder)!;
            scope.SourceFormat = Format((WaveFormat)SourceFormat.GetValue(stream)!);
            scope.DestinationFormat = Format((WaveFormat)PcmFormat.GetValue(decoder)!);
            scope.Source = GCHandle.Alloc(stream.SourceBuffer, GCHandleType.Pinned);
            scope.Destination = GCHandle.Alloc(stream.DestBuffer, GCHandleType.Pinned);
            bool entered = wakeup_acm_begin((IntPtr)Handle.GetValue(stream)!, scope.Source.AddrOfPinnedObject(), (uint)length,
                scope.Destination.AddrOfPinnedObject(), (uint)stream.DestBuffer.Length,
                scope.SourceFormat, (uint)scope.SourceFormat.Length, scope.DestinationFormat, (uint)scope.DestinationFormat.Length) == 1;
            if (entered) { observation = scope; return true; }
        }
        catch { }
        Release(scope); return false;
    }
    internal static bool End(out ulong token, out string identity, out string refusal)
    {
        token = 0; identity = ""; refusal = "provider-not-observed";
        var scope = observation; observation = null;
        if (scope == null) return false;
        try
        {
            var cells = new ulong[32];
            bool matched = wakeup_acm_end(cells, 32) == 1;
            token = cells[2];
            // Exact format bytes (not merely their native diagnostic digest)
            // and output-determining open/convert inputs form the persistent key.
            bool contract = matched && cells[0] == 1 && cells[4] == 1 && cells[5] == 0
                && cells[11] == cells[9] && cells[14] == 4 && cells[13] == 20
                && cells[17] == 0 && cells[18] == 1 && cells[19] == 0 && cells[20] == 0
                && cells[22] == 0 && cells[23] == 0x3a10 && cells[24] == 1 && cells[25] == 1;
            identity = "qualified-provider-math-v2:l3codeca:5d7f5961411fe64b2bc48297e7c424aaf19272aa814287b0a4ff1c469b8bd3df:"
                + "ucrt:5c52e3a303baaac0e0af8bd9b96134993da34bc9d834a31ef37e1d2cdc7fe192:" + Hash(scope.SourceFormat) + ":" + Hash(scope.DestinationFormat) + ":open=" + cells[14] + ":first=" + cells[13] + ":fp=" + (cells[31] & ~1UL) + ":loader=" + overrides;
            refusal = contract ? "" : "provider-contract:" + string.Join(",", cells);
            return contract;
        }
        finally { Release(scope); }
    }
    internal static bool Current(AcmMp3FrameDecompressor decoder, ulong token)
    {
        try { return installed && wakeup_acm_current((IntPtr)Handle.GetValue(Conversion.GetValue(decoder))!, token) == 1; }
        catch { return false; }
    }
    internal static int SourceCapacity(AcmMp3FrameDecompressor decoder) => ((AcmStream)Conversion.GetValue(decoder)!).SourceBuffer.Length;
    private static void Release(Observation scope)
    {
        if (scope.Source.IsAllocated) scope.Source.Free();
        if (scope.Destination.IsAllocated) scope.Destination.Free();
    }
    private static byte[] Format(WaveFormat format)
    {
        int size = Marshal.SizeOf(format);
        IntPtr pointer = Marshal.AllocHGlobal(size);
        try { Marshal.StructureToPtr(format, pointer, false); var bytes = new byte[size]; Marshal.Copy(pointer, bytes, 0, size); return bytes; }
        finally { Marshal.FreeHGlobal(pointer); }
    }
    private static string Hash(byte[] bytes)
    { using var hash = new SHA256Managed(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
    internal static Dictionary<string, object> Snapshot()
    {
        var stats = new ulong[45];
        if (attempted) try { for (int i = 0; i < stats.Length; i++) stats[i] = wakeup_acm_stat(i); } catch { }
        return new Dictionary<string, object> { ["status"] = status, ["installed"] = installed, ["nativeStats"] = stats };
    }
    internal static void Stop() { if (installed) wakeup_acm_stop(); installed = false; }
}
