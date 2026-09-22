// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using UnityEngine;

namespace WakeUp.Tests;

[TestFixture]
public sealed class NativeDdsCaptureTests
{
    [Test]
    public void UnsupportedPreparationAllowsNativeDdsWithoutChangingSupportedIdentity()
    {
        Assert.That(PngRuntime.TryPreparationPlatform(() => throw new NotSupportedException("preparation-platform"), out string unavailable), Is.False);
        Assert.That(unavailable, Is.Empty);
        Assert.That(PngRuntime.TryPreparationPlatform(() => "existing-prepared-and-automatic-identity", out string supported), Is.True);
        Assert.That(supported, Is.EqualTo("existing-prepared-and-automatic-identity"));
        // A threading/programming error is not misreported as platform refusal.
        Assert.Throws<InvalidOperationException>((Action)(() => PngRuntime.TryPreparationPlatform(
            () => throw new InvalidOperationException("preparation-main-thread"), out _)));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NativeMipDecisionAndFinalReadabilitySurviveFailedCopy(bool updateMips)
    {
        var calls = new List<(bool, bool)>();
        Assert.That(NativeDdsCapture.ApplyAndCapture((a, b) => calls.Add((a, b)),
            () => throw new IOException("read failed"), updateMips, true, 32), Is.Null);
        Assert.That(calls, Is.EqualTo(new[] { (updateMips, false), (false, true) }));
    }

    [Test]
    public void NativeFailurePropagatesAndIncompleteBytesNeverPublish()
    {
        bool read = false;
        Assert.Throws<InvalidOperationException>((Action)(() => NativeDdsCapture.ApplyAndCapture(
            (_, _) => throw new InvalidOperationException("native apply"),
            () => { read = true; return new byte[4]; }, true, true, 4)));
        Assert.That(read, Is.False);
        var calls = new List<(bool, bool)>();
        Assert.That(NativeDdsCapture.ApplyAndCapture((a, b) => calls.Add((a, b)),
            () => new byte[3], false, true, 4), Is.Null);
        Assert.That(calls.Last(), Is.EqualTo((false, true)));
    }

    [Test]
    public void FullBytesAndReadableNativeContractRemainExact()
    {
        byte[] pixels = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var calls = new List<(bool, bool)>();
        Assert.That(NativeDdsCapture.ApplyAndCapture((a, b) => calls.Add((a, b)),
            () => pixels, false, false, pixels.Length), Is.SameAs(pixels));
        Assert.That(calls, Is.EqualTo(new[] { (false, false) }));
    }

    [TestCase(1, 21)]
    [TestCase(2, 42)]
    [TestCase(7, 42)]
    [TestCase(3, 63)]
    [TestCase(4, 84)]
    [TestCase(10, 24)]
    [TestCase(12, 48)]
    [TestCase(25, 48)]
    public void NativeDdsLayoutsIncludeEveryTailMip(int format, int bytes)
        => Assert.That(NativeDdsCapture.ExpectedBytes(4, 4, format, 3), Is.EqualTo(bytes));

    [Test]
    public void BoundsRefuseBeforeReadingAndLargeNonSquareLayoutsFit()
    {
        bool read = false;
        var calls = new List<(bool, bool)>();
        Assert.That(NativeDdsCapture.ApplyAndCapture((a, b) => calls.Add((a, b)),
            () => { read = true; return Array.Empty<byte>(); }, true, true,
            NativeDdsCapture.MaximumCaptureBytes + 1), Is.Null);
        Assert.That(read, Is.False);
        Assert.That(calls, Is.EqualTo(new[] { (true, true) }));
        Assert.That(NativeDdsCapture.ExpectedBytes(4096, 2304, 25, 1), Is.EqualTo(9437184));
        Assert.That(NativeDdsCapture.ExpectedBytes(8192, 8192, 4, 1), Is.EqualTo(-1));
        Assert.That(NativeDdsCapture.ExpectedBytes(4, 4, 4, 4), Is.EqualTo(-1));
    }

    [Test]
    public void ReadLeaseMapsSameHandleAndExcludesWritersAndReplacement()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dds-lease-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, new byte[128]);
        try
        {
            using var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var mapping = NativeDdsCapture.MapLease(lease, lease.Length);
            using var view = mapping.CreateViewAccessor(0, lease.Length, MemoryMappedFileAccess.Read);
            Assert.That(view.ReadByte(0), Is.Zero);
            Assert.Throws<IOException>((Action)(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); }));
            Assert.Throws<IOException>((Action)(() => File.Delete(path)));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void OriginalDdsBodiesArePinned()
    {
        var observed = new List<(string Name, bool Hashed, string Hash, string Reason)>();
        foreach (var method in NativeDdsCapture.Targets)
        {
            bool hashed = SemanticMethodIdentity.TryHash(method, out string hash, out string reason);
            TestContext.Out.WriteLine(method.Name + "\t" + hash + "\t" + reason);
            observed.Add((method.Name, hashed, hash, reason));
        }
        foreach (var item in observed)
        {
            // Span/ValueTuple method operands use Mono BCL types absent on the
            // desktop host. Exact GOG runtime pins are captured by C05 observer.
            if (!item.Hashed && item.Reason == "semantic-il-exception-typeloadexception"
                && new[] { "TryLoadDds", "CreateTexture", "OpenExistingMmf" }.Contains(item.Name)) continue;
            Assert.That(item.Hashed, Is.True, item.Name + ":" + item.Reason);
            Assert.That(NativeDdsCapture.Bodies.TryGetValue(item.Name, out string? expected), Is.True, "Pin original body " + item.Name);
            Assert.That(item.Hash, Is.EqualTo(expected), item.Name);
        }
    }

    [TestCase(64u, 24u, 255u, 65280u, 16711680u, 0u, 3)]
    [TestCase(65u, 32u, 16711680u, 65280u, 255u, 4278190080u, 4)]
    [TestCase(64u, 16u, 63488u, 2016u, 31u, 0u, 7)]
    [TestCase(65u, 16u, 61440u, 3840u, 240u, 15u, 2)]
    [TestCase(131072u, 8u, 255u, 0u, 0u, 0u, 1)]
    [TestCase(2u, 8u, 0u, 0u, 0u, 255u, 1)]
    public void NativeHeaderAdmitsActualUncompressedBranches(uint flags, uint bits,
        uint red, uint green, uint blue, uint alpha, int format)
    {
        byte[] data = new byte[128 + NativeDdsCapture.ExpectedBytes(4, 4, format, 3)];
        void Put(int at, uint value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, at, 4);
        Put(0, 0x20534444); Put(4, 124); Put(8, 0x20000); Put(12, 4); Put(16, 4); Put(28, 3);
        Put(76, 32); Put(80, flags); Put(88, bits); Put(92, red); Put(96, green); Put(100, blue); Put(104, alpha);
        var header = NativeDdsCapture.ReadHeader(new MemoryStream(data));
        Assert.That(header.IsDds, Is.True);
        Assert.That(header.OrdinaryMetadata, Is.False, "Native scan admission cannot authorize lossy export");
        Put(28, 4);
        Assert.Throws<InvalidDataException>((Action)(() => NativeDdsCapture.ReadHeader(new MemoryStream(data))));
    }

    [Test]
    public void ApplyRewritePreservesAllOtherCallsAndStackSignature()
    {
        var apply = AccessTools.Method(typeof(Texture2D), "Apply", new[] { typeof(bool), typeof(bool) });
        // Span<T> in the actual method belongs to Mono's BCL, so this fixture
        // tests the replacement contract without executing a Unity call.
        var original = new[] { new CodeInstruction(System.Reflection.Emit.OpCodes.Callvirt, apply) };
        var rewritten = NativeDdsCapture.RewriteApply(original).Single();
        var replacement = (MethodInfo)rewritten.operand;
        Assert.That(replacement.GetParameters().Select(p => p.ParameterType),
            Is.EqualTo(new[] { typeof(Texture2D), typeof(bool), typeof(bool) }));
        Assert.That(replacement.ReturnType, Is.EqualTo(typeof(void)));
        Assert.Throws<InvalidOperationException>((Action)(() => NativeDdsCapture.RewriteApply(Array.Empty<CodeInstruction>())));
        Assert.Throws<InvalidOperationException>((Action)(() => NativeDdsCapture.RewriteApply(original.Concat(original))));
    }
}
