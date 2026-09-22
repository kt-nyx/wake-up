// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace WakeUp;

// Hash every byte from the already-owned source in bounded blocks. Use the
// same Windows SHA-256 primitive as CacheDigest, with explicit managed fallback;
// neither route consults CryptoConfig or reopens the file by pathname.
internal static class PreparedAudioSourceHash
{
    private static bool nativeAvailable = Environment.OSVersion.Platform == PlatformID.Win32NT;
    internal static long NativeCalls, ManagedCalls;
    [DllImport("bcrypt.dll", ExactSpelling = true)]
    private static extern int BCryptCreateHash(IntPtr algorithm, out IntPtr hash, IntPtr hashObject,
        int objectLength, IntPtr secret, int secretLength, int flags);
    [DllImport("bcrypt.dll", ExactSpelling = true)]
    private static extern int BCryptHashData(IntPtr hash, [In] byte[] bytes, int count, int flags);
    [DllImport("bcrypt.dll", ExactSpelling = true)]
    private static extern int BCryptFinishHash(IntPtr hash, [Out] byte[] output, int count, int flags);
    [DllImport("bcrypt.dll", ExactSpelling = true)]
    private static extern int BCryptDestroyHash(IntPtr hash);

    internal static byte[] Compute(Stream source, out long readBytes, bool allowNative = true)
    {
        readBytes = 0;
        IntPtr handle = IntPtr.Zero;
        try
        {
            if (allowNative && nativeAvailable)
            {
                try
                {
                    // Windows 10+ SHA-256 pseudo-handle, matching CacheDigest.
                    // The hash object owns its allocation until DestroyHash.
                    if (BCryptCreateHash(new IntPtr(0x41), out handle, IntPtr.Zero, 0, IntPtr.Zero, 0, 0) != 0)
                        nativeAvailable = false;
                }
                catch (DllNotFoundException) { nativeAvailable = false; }
                catch (EntryPointNotFoundException) { nativeAvailable = false; }
            }
            var buffer = new byte[65536];
            if (handle != IntPtr.Zero)
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
                {
                    readBytes += read;
                    if (BCryptHashData(handle, buffer, read, 0) != 0)
                        throw new CryptographicException("Audio source SHA-256 update failed.");
                }
                var digest = new byte[32];
                if (BCryptFinishHash(handle, digest, digest.Length, 0) != 0)
                    throw new CryptographicException("Audio source SHA-256 finalization failed.");
                Interlocked.Increment(ref NativeCalls);
                return digest;
            }
            using var managed = new SHA256Managed();
            int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) != 0)
            {
                readBytes += count;
                managed.TransformBlock(buffer, 0, count, buffer, 0);
            }
            managed.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            Interlocked.Increment(ref ManagedCalls);
            return managed.Hash!;
        }
        finally
        {
            if (handle != IntPtr.Zero) BCryptDestroyHash(handle);
        }
    }
}
