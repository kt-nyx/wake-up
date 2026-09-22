// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace WakeUp;

// Shared SHA-256 primitive retained from the PNG cache, including its Windows
// acceleration and portable managed fallback. This does not weaken freshness.
internal static class CacheDigest
{
    private static bool nativeHash = Environment.OSVersion.Platform == PlatformID.Win32NT;
    internal static long NativeHashCalls, ManagedHashCalls, HashTicks;
    [DllImport("bcrypt.dll", ExactSpelling = true)]
    private static extern int BCryptHash(IntPtr algorithm, IntPtr secret, int secretLength, [In] byte[] input, int inputLength, [Out] byte[] output, int outputLength);
    [DllImport("bcrypt.dll", EntryPoint = "BCryptHash", ExactSpelling = true)]
    private static extern int BCryptHashRange(IntPtr algorithm, IntPtr secret, int secretLength, IntPtr input, int inputLength, [Out] byte[] output, int outputLength);
    internal static byte[] Hash(byte[] bytes) => Hash(bytes, 0, bytes?.Length ?? 0, true);
    internal static byte[] Hash(byte[] bytes, int offset, int count) => Hash(bytes, offset, count, true);
    // Input workers must not construct a CryptoConfig-supplied custom provider.
    internal static byte[] HashInput(byte[] bytes) => Hash(bytes, 0, bytes?.Length ?? 0, false);
    internal static byte[] HashInput(byte[] bytes, int offset, int count) => Hash(bytes, offset, count, false);
    private static byte[] Hash(byte[] bytes, int offset, int count, bool configuredProvider)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (offset < 0 || offset > bytes.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || count > bytes.Length - offset) throw new ArgumentOutOfRangeException(nameof(count));
        long start = Stopwatch.GetTimestamp();
        try
        {
            if (nativeHash)
            {
                try
                {
                    var output = new byte[32];
                    // Windows 10+ SHA-256 algorithm pseudo-handle; no owned handle.
                    if (NativeHash(bytes, offset, count, output) == 0)
                    {
                        Interlocked.Increment(ref NativeHashCalls);
                        return output;
                    }
                    nativeHash = false;
                }
                catch (DllNotFoundException) { nativeHash = false; }
                catch (EntryPointNotFoundException) { nativeHash = false; }
            }
            Interlocked.Increment(ref ManagedHashCalls);
#if NET8_0_OR_GREATER
            // The standalone runtime supplies its fixed SHA-256 implementation;
            // Mono retains the explicit managed fallback required by input workers.
            using var sha = SHA256.Create();
#else
            using var sha = configuredProvider ? SHA256.Create() : new SHA256Managed();
#endif
            return sha.ComputeHash(bytes, offset, count);
        }
        finally { Interlocked.Add(ref HashTicks, Stopwatch.GetTimestamp() - start); }
    }

    private static int NativeHash(byte[] bytes, int offset, int count, byte[] output)
    {
        if (offset == 0)
            return BCryptHash(new IntPtr(0x41), IntPtr.Zero, 0, bytes, count, output, output.Length);
        // Pin only for the synchronous native call; hashing a block must not
        // allocate and copy the block merely to select its array range.
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return BCryptHashRange(new IntPtr(0x41), IntPtr.Zero, 0,
                IntPtr.Add(pinned.AddrOfPinnedObject(), offset), count, output, output.Length);
        }
        finally { pinned.Free(); }
    }
}
