// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PngProcessingTests
{
    [Test]
    public void NativeHashMatchesSha256WithoutChangingInput()
    {
        foreach (int size in new[] { 0, 1, 64, 65, 1048576 })
        {
            var bytes = Enumerable.Range(0, size).Select(i => (byte)(i * 31)).ToArray();
            var saved = (byte[])bytes.Clone();
            using var sha = System.Security.Cryptography.SHA256.Create();
            Assert.That(PngCache.Hash(bytes), Is.EqualTo(sha.ComputeHash(bytes)));
            Assert.That(bytes, Is.EqualTo(saved));
        }
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            Assert.That(PngCache.NativeHashCalls, Is.GreaterThanOrEqualTo(5));
    }
    [Test]
    public void PinnedOriginalBodiesAndPngCallsitesAreAvailable()
    {
        foreach (var method in PngRuntime.Targets)
        {
            // The iterator uses newer Mono core APIs absent from desktop net472.
            // Its body was checked during fixture qualification and remains
            // guarded by the runtime's own identity check before installation.
            if (method == PngRuntime.Iterator)
                continue;
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            Assert.That(hash, Is.EqualTo(PngRuntime.Expected[method.Name]));
        }
        var original = PatchProcessor.GetOriginalInstructions(PngRuntime.Image);
        var replaced = PngRuntime.ImageTranspiler(original).ToArray();
        Assert.That(replaced.Length, Is.EqualTo(original.Count));
        for (int i = 0; i < original.Count; i++)
        {
            if (!(original[i].operand is MethodInfo before) || !(replaced[i].operand is MethodInfo after) || before == after)
                continue;
            var parameters = (before.IsStatic ? Type.EmptyTypes : new[] { before.DeclaringType! }).Concat(before.GetParameters().Select(p => p.ParameterType));
            Assert.That(after.GetParameters().Select(p => p.ParameterType), Is.EqualTo(parameters), before.Name);
            Assert.That(after.ReturnType, Is.EqualTo(before.ReturnType), before.Name);
        }
        Assert.That(PngRuntime.ReloadTranspiler(PatchProcessor.GetOriginalInstructions(PngRuntime.Reload)).Count(), Is.GreaterThan(0));
    }
    [Test]
    public void ProcessedCachePreservesMipPayloadAndRejectsCorruptionAndInvalidLengths()
    {
        string root = Path.Combine(Path.GetTempPath(), "wake-up-png-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.That(PngCache.ExpectedBytes(4, 4, 4, 3), Is.EqualTo(84));
            Assert.That(PngCache.ExpectedBytes(4, 4, 4, 4), Is.EqualTo(-1));
            Assert.That(PngCache.ExpectedBytes(32, 16, 12, 2), Is.EqualTo(640));
            var cache = new PngCache(root);
            var entry = new PngCache.Entry { Width = 4, Height = 4, TextureFormat = 4, GraphicsFormat = 8, Mips = 3, Pixels = Enumerable.Range(0, 84).Select(i => (byte)i).ToArray() };
            string key = PngCache.Hex(PngCache.Hash(new byte[] { 1, 2, 3 }));
            cache.Write(key, entry);
            var read = cache.Read(key);
            Assert.That(read, Is.Not.Null);
            Assert.That(read!.Pixels, Is.EqualTo(entry.Pixels));
            Assert.That(read.Mips, Is.EqualTo(3));
            Assert.That(cache.Read(PngCache.Hex(PngCache.Hash(new byte[] { 1, 2, 4 }))), Is.Null);
            string file = Path.Combine(root, key + ".bin");
            byte[] damaged = File.ReadAllBytes(file);
            damaged[damaged.Length - 1] ^= 1;
            File.WriteAllBytes(file, damaged);
            Assert.That(cache.Read(key), Is.Null);
            Assert.That(cache.Errors, Is.EqualTo(1));
            cache.Write(key, entry);
            Assert.That(cache.Read(key)!.Pixels, Is.EqualTo(entry.Pixels));
            Assert.That(cache.CanCapture(PngCache.MaximumEntry + 1), Is.False);
            Assert.That(cache.CanCapture(-1), Is.False);
            Assert.That(cache.CanCapture(0), Is.False);
            cache.Invalidate(key);
            Assert.That(cache.Read(key), Is.Null);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
