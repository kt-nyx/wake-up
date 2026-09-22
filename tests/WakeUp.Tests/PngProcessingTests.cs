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
    public void CoordinatedAtlasAdmissionRequiresExactOwnerAndMethod()
    {
        var atlasCopy = AccessTools.Method(typeof(AtlasRuntime), "CompressionTranspiler");
        var unrelated = AccessTools.Method(typeof(PngProcessingTests), nameof(CoordinatedAtlasAdmissionRequiresExactOwnerAndMethod));
        Assert.That(PngRuntime.AllowsAtlasCompressionPatch(new Patch(new HarmonyMethod(atlasCopy), 0, AtlasRuntime.Owner)), Is.True);
        Assert.That(PngRuntime.AllowsAtlasCompressionPatch(new Patch(new HarmonyMethod(atlasCopy), 0, "foreign")), Is.False);
        Assert.That(PngRuntime.AllowsAtlasCompressionPatch(new Patch(new HarmonyMethod(unrelated), 0, AtlasRuntime.Owner)), Is.False);
    }

    [Test]
    public void JpegUsesGuardedNativeImagePipelineAndContentIdentity()
    {
        foreach (string name in new[] { "a.png", "a.JPG", "a.jpeg", "a.JpEg" })
            Assert.That(PngRuntime.IsCacheableImageSource(name), Is.True, name);
        foreach (string name in new[] { "a.dds", "a.psd", "a.jpg.pending", "a.tga" })
            Assert.That(PngRuntime.IsCacheableImageSource(name), Is.False, name);
        var nativeCalls = PatchProcessor.GetOriginalInstructions(PngRuntime.LoadTexture)
            .Select(i => i.operand).OfType<MethodInfo>().ToArray();
        Assert.That(nativeCalls.Count(m => m == PngRuntime.Image), Is.EqualTo(1));
        Assert.That(nativeCalls.Any(m => m.Name == "TryLoadDds"), Is.True);
        // Same-length source changes must invalidate even with identical path
        // and dimensions. Keep the existing PNG key representation unchanged.
        byte[] original = { 255, 216, 1, 255, 217 }, changed = { 255, 216, 2, 255, 217 };
        string platform = PngRuntime.CacheGameIdentity(Guid.Empty) + "|unity|device|color";
        string Key(string path, byte[] bytes) => PngRuntime.SourceKey(platform, PngCapturePath.CpuTextureData, true, false, path, bytes);
        string key = Key("terrain.jpg", original);
        Assert.That(Key("terrain.jpg", changed), Is.Not.EqualTo(key));
        Assert.That(Key("terrain.png", original), Is.Not.EqualTo(key));
        Assert.That(PngRuntime.SourceKey(platform + "changed", PngCapturePath.CpuTextureData, true, false, "terrain.jpg", original), Is.Not.EqualTo(key));
        Assert.That(PngRuntime.SourceKey(platform, PngCapturePath.D3D11CompressionBlocks, true, true, "terrain.jpg", original), Is.Not.EqualTo(key));
        Assert.That(PngRuntime.SourceKey(platform, PngCapturePath.CpuTextureData, false, false, "terrain.jpg", original), Is.Not.EqualTo(key));
        Assert.That(key, Is.EqualTo(PngCache.Hex(PngCache.Hash(System.Text.Encoding.UTF8.GetBytes(
            platform + "|CpuTextureData|True|False|terrain.jpg|" + PngCache.Hex(PngCache.Hash(original)))))));
    }

    [TestCase(3, 11, 4194303)] // Native JPEG RGB24 with all mip levels.
    [TestCase(10, 11, 699064)] // CPU compressed DXT1, including sub-block tails.
    [TestCase(12, 7, 1398016)] // Native D3D11 DXT5 chain, ending at 16x16.
    public void JpegSizedPayloadPreservesEveryByteAndIndependentReads(int format, int mips, int bytes)
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "jpeg-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.That(PngCache.ExpectedBytes(1024, 1024, format, mips), Is.EqualTo(bytes));
            using var cache = new PngCache(root);
            var pixels = new byte[bytes];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 31 + i / 257);
            var entry = new PngCache.Entry { Width = 1024, Height = 1024, TextureFormat = format, Mips = mips,
                GraphicsFormat = 99, Filter = 2, Aniso = 2, WrapU = 1, WrapV = 2, WrapW = 0, Bias = 0.25f, Pixels = pixels };
            string key = PngRuntime.SourceKey("jpeg-test", PngCapturePath.CpuTextureData, true, false, "a.jpg", new byte[] { 1 });
            cache.Write(key, entry);
            var first = cache.Read(key)!;
            Assert.That(first.Pixels, Is.EqualTo(pixels));
            first.Pixels[bytes - 1] ^= 1;
            var second = cache.Read(key)!;
            Assert.That(second.Pixels, Is.EqualTo(pixels));
            Assert.That(second.Mips, Is.EqualTo(mips));
            Assert.That(new[] { second.Width, second.Height, second.TextureFormat, second.GraphicsFormat,
                second.Filter, second.Aniso, second.WrapU, second.WrapV, second.WrapW },
                Is.EqualTo(new[] { 1024, 1024, format, 99, 2, 2, 1, 2, 0 }));
            Assert.That(second.Bias, Is.EqualTo(0.25f));
            Assert.That(cache.Read(PngRuntime.SourceKey("jpeg-test", PngCapturePath.CpuTextureData, true, false, "a.jpg", new byte[] { 2 })), Is.Null);
            // Damage the final mip's last byte, beyond the base image.
            string file = Path.Combine(root, "v1", key + ".bin");
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
            {
                stream.Position = stream.Length - 1;
                stream.WriteByte((byte)(pixels[bytes - 1] ^ 1));
            }
            Assert.That(cache.Read(key), Is.Null);
            cache.Write(key, entry);
            Assert.That(cache.Read(key)!.Pixels, Is.EqualTo(pixels));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public void OversizedJpegAndInvalidLayoutNeverStartCapture()
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "jpeg-bound-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var cache = new PngCache(root);
            string key = PngCache.Hex(PngCache.Hash(new byte[] { 9 }));
            foreach (var layout in new[] { new[] { 1470, 1961, 3, 11 }, new[] { int.MaxValue, int.MaxValue, 3, 14 },
                new[] { 1024, 1024, 3, 12 }, new[] { 1024, 1024, 999, 11 } })
            {
                int bytes = PngCache.ExpectedBytes(layout[0], layout[1], layout[2], layout[3]);
                Assert.That(bytes, Is.EqualTo(-1));
                Assert.That(cache.TryCapture(key, bytes, () => Assert.Fail("Unexpected expensive capture")), Is.False);
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

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
            Assert.That(hash, Is.EqualTo(PngRuntime.ExpectedBody(GameBuildContract.Current, method.Name)));
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
            using var cache = new PngCache(root);
            var entry = new PngCache.Entry { Width = 4, Height = 4, TextureFormat = 4, GraphicsFormat = 8, Mips = 3, Pixels = Enumerable.Range(0, 84).Select(i => (byte)i).ToArray() };
            string key = PngCache.Hex(PngCache.Hash(new byte[] { 1, 2, 3 }));
            cache.Write(key, entry);
            var read = cache.Read(key);
            Assert.That(read, Is.Not.Null);
            Assert.That(read!.Pixels, Is.EqualTo(entry.Pixels));
            Assert.That(read.Mips, Is.EqualTo(3));
            Assert.That(cache.Read(PngCache.Hex(PngCache.Hash(new byte[] { 1, 2, 4 }))), Is.Null);
            string file = Path.Combine(root, "v1", key + ".bin");
            byte[] damaged = File.ReadAllBytes(file);
            damaged[damaged.Length - 1] ^= 1;
            File.WriteAllBytes(file, damaged);
            Assert.That(cache.Read(key), Is.Null);
            Assert.That(cache.Errors, Is.EqualTo(1));
            cache.Write(key, entry);
            Assert.That(cache.Read(key)!.Pixels, Is.EqualTo(entry.Pixels));
            Assert.That(cache.CanCapture(key, PngCache.MaximumEntry + 1), Is.False);
            Assert.That(cache.CanCapture(key, -1), Is.False);
            Assert.That(cache.CanCapture(key, 0), Is.False);
            cache.Invalidate(key);
            Assert.That(cache.Read(key), Is.Null);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestCase(32, 16, 10, 6, 360)] // CPU DXT1, complete mip chain including sub-block mips.
    [TestCase(32, 16, 12, 6, 720)] // CPU DXT5 with alpha.
    [TestCase(12, 8, 10, 2, 64)] // Reduced mip chain for a non-power-of-two image.
    [TestCase(7, 5, 3, 3, 126)] // RGB image which the native loader leaves uncompressed.
    public void CpuTextureLayoutsRoundTripWithSamplerSettings(int width, int height, int format, int mips, int bytes)
    {
        string root = Path.Combine(Path.GetTempPath(), "wake-up-cpu-png-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.That(PngCache.ExpectedBytes(width, height, format, mips), Is.EqualTo(bytes));
            using var cache = new PngCache(root);
            var entry = new PngCache.Entry { Width = width, Height = height, TextureFormat = format, Mips = mips,
                GraphicsFormat = 99, Filter = 2, Aniso = 2, WrapU = 1, WrapV = 2, WrapW = 0, Bias = 0.25f,
                Pixels = Enumerable.Range(0, bytes).Select(i => (byte)(i * 31)).ToArray() };
            string key = PngCache.Hex(PngCache.Hash(entry.Pixels));
            cache.Write(key, entry);
            var read = cache.Read(key);
            Assert.That(read, Is.Not.Null);
            Assert.That(read!.Pixels, Is.EqualTo(entry.Pixels));
            Assert.That(new[] { read.Width, read.Height, read.TextureFormat, read.Mips, read.GraphicsFormat,
                read.Filter, read.Aniso, read.WrapU, read.WrapV, read.WrapW },
                Is.EqualTo(new[] { width, height, format, mips, 99, 2, 2, 1, 2, 0 }));
            Assert.That(read.Bias, Is.EqualTo(entry.Bias));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
