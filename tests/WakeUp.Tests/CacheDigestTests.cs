// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class CacheDigestTests
{
    [TestCase(0, 0)]
    [TestCase(0, 63)]
    [TestCase(1, 64)]
    [TestCase(17, 65)]
    [TestCase(127, 1024)]
    [TestCase(1151, 0)]
    public void RangeMatchesSha256AndPreservesSurroundingBytes(int offset, int count)
    {
        var bytes = Enumerable.Range(0, 1151).Select(i => (byte)(i * 31 + i / 257)).ToArray();
        var saved = (byte[])bytes.Clone();
        using var sha = new SHA256Managed();
        byte[] expected = sha.ComputeHash(bytes.Skip(offset).Take(count).ToArray());
        Assert.That(CacheDigest.Hash(bytes, offset, count), Is.EqualTo(expected));
        Assert.That(bytes, Is.EqualTo(saved));
    }

    [TestCase(-1, 1)]
    [TestCase(4, 0)]
    [TestCase(0, -1)]
    [TestCase(1, 3)]
    [TestCase(1, int.MaxValue)]
    public void InvalidRangeIsRejectedBeforeNativeAccess(int offset, int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>((Action)(() => CacheDigest.Hash(new byte[3], offset, count)));
    }

    [Test]
    public void NullArrayIsRejected()
    {
        Assert.Throws<ArgumentNullException>((Action)(() => CacheDigest.Hash(null!, 0, 0)));
    }
}
