// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class PreparedAudioSourceHashTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(65535)]
    [TestCase(65536)]
    [TestCase(65537)]
    [TestCase(131079)]
    public void NativeAndManagedBlocksMatchWholeInputSha256(int length)
    {
        var bytes = new byte[length]; new Random(43).NextBytes(bytes);
        using var reference = new SHA256Managed();
        byte[] expected = reference.ComputeHash(bytes);
        foreach (bool native in new[] { false, true })
        {
            using var source = new MemoryStream(bytes);
            long nativeBefore = PreparedAudioSourceHash.NativeCalls;
            byte[] actual = PreparedAudioSourceHash.Compute(source, out long read, native);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(read, Is.EqualTo(length));
            Assert.That(source.Position, Is.EqualTo(length));
            Assert.That(source.CanRead, Is.True);
            if (native && Environment.OSVersion.Platform == PlatformID.Win32NT)
                Assert.That(PreparedAudioSourceHash.NativeCalls, Is.EqualTo(nativeBefore + 1));
        }
    }
}
