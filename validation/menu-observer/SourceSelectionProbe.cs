// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// A private code-free marker selects this one functional startup test. All
// artwork belongs to the observer's profile overlay; frozen mods stay intact.
internal static class SourceSelectionProbe
{
    internal static bool Selected => CompatibilityProbe.Selected && LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "local.fixture.compatibilitysources");
    private const string Logical = "SourceSelection/Choice";
    internal static void Schedule(ModContentPack provider, string directory)
    {
        if (Selected) LongEventHandler.ExecuteWhenFinished(() => Run(provider, directory));
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Run(ModContentPack provider, string directory)
    {
        string overlay = Path.Combine(directory, "SourceSelection");
        string textures = Path.Combine(overlay, "Textures", "SourceSelection");
        string png = Path.Combine(textures, "Choice.png"), dds = Path.Combine(textures, "Choice.dds");
        Directory.CreateDirectory(textures);
        provider.foldersToLoadDescendingOrder.Insert(0, overlay);
        string step = "setup";
        void Reload()
        {
            // Native ReloadAll adds to an existing holder; begin each source
            // selection with its native cleanup while retaining the disk cache.
            provider.GetContentHolder<Texture2D>().ClearDestroy();
            AccessTools.Method(typeof(ModContentPack), "ReloadContentInt").Invoke(provider, new object[] { false });
        }
        try
        {
            var runtime = AccessTools.TypeByName("WakeUp.PngRuntime");
            Require((bool)AccessTools.Property(runtime, "Active").GetValue(null, null), "Image reuse must still be active at the native startup callback");
            object cache = AccessTools.Field(runtime, "cache").GetValue(null);
            long Count(string field) => Convert.ToInt64(AccessTools.Field(cache.GetType(), field).GetValue(cache));
            step = "cold PNG"; WritePng(png, Color.blue); long writes = Count("Writes");
            Reload(); Check(provider, Color.blue); Require(Count("Writes") > writes, "PNG did not construct a cache entry");
            step = "warm PNG"; long hits = Count("Hits"); Reload(); Check(provider, Color.blue); Require(Count("Hits") > hits, "Unchanged PNG did not reuse its output");
            step = "DDS sibling"; WriteDds(dds, 0xf800); Reload(); Check(provider, Color.red);
            step = "changed DDS"; WriteDds(dds, 0x07e0); Reload(); Check(provider, Color.green);
            step = "removed DDS falls back to PNG"; File.Delete(dds); Reload(); Check(provider, Color.blue);
            step = "restored DDS"; WriteDds(dds, 0x07e0); Reload(); Check(provider, Color.green);
            step = "removed PNG"; File.Delete(png); Reload(); Check(provider, Color.green);
            step = "both sources removed"; File.Delete(dds); Reload(); Missing(provider);
            step = "restored PNG"; WritePng(png, Color.blue); Reload(); Check(provider, Color.blue);
            step = "changed PNG"; WritePng(png, Color.yellow); Reload(); Check(provider, Color.yellow);
            step = "PNG removed again"; File.Delete(png); Reload(); Missing(provider);
            Require(Convert.ToInt64(AccessTools.Field(runtime, "mismatches").GetValue(null)) == 0, "Native image verification mismatch");
            File.WriteAllText(Path.Combine(directory, "source-selection.json"), "{\"passed\":true,\"pngColdAndWarm\":true,\"ddsSiblingSelected\":true,\"changedDdsSelected\":true,\"removedPngNotResurrected\":true,\"changedPngInvalidated\":true,\"missingSourceRemainsMissing\":true,\"frozenModFilesChanged\":false}");
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(directory, "source-selection-error.txt"), step + ": " + error);
            File.WriteAllText(Path.Combine(directory, "source-selection.json"), "{\"passed\":false}");
        }
        finally
        {
            provider.foldersToLoadDescendingOrder.Remove(overlay);
            try { Reload(); }
            catch (Exception error)
            {
                File.AppendAllText(Path.Combine(directory, "source-selection-error.txt"), "Overlay restoration: " + error);
                File.WriteAllText(Path.Combine(directory, "source-selection.json"), "{\"passed\":false}");
            }
        }
    }
    private static void Missing(ModContentPack provider)
    {
        Require(provider.GetContentHolder<Texture2D>().Get(Logical) == null && ContentFinder<Texture2D>.Get(Logical, false) == null,
            "Removed source was resurrected from cached output");
    }
    private static void Check(ModContentPack provider, Color expected)
    {
        var actual = provider.GetContentHolder<Texture2D>().Get(Logical);
        Require(actual != null && ReferenceEquals(actual, ContentFinder<Texture2D>.Get(Logical, false)), "Unexpected selected provider");
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(4, 4, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var pixels = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
        try
        {
            Graphics.Blit(actual, target); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 4, 4), 0, 0); pixels.Apply();
            foreach (var pixel in pixels.GetPixels())
                Require(Math.Abs(pixel.r - expected.r) < .02 && Math.Abs(pixel.g - expected.g) < .02
                    && Math.Abs(pixel.b - expected.b) < .02, "Selected GPU pixels do not match the actual source choice");
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(pixels); }
    }
    private static void WritePng(string path, Color color)
    {
        var image = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        try { image.SetPixels(Enumerable.Repeat(color, 16).ToArray()); image.Apply(); File.WriteAllBytes(path, ImageConversion.EncodeToPNG(image)); }
        finally { UnityEngine.Object.DestroyImmediate(image); }
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc));
    }
    private static void WriteDds(string path, ushort endpoint)
    {
        byte[] bytes = new byte[136];
        void Put(int offset, uint value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, offset, 4);
        Put(0, 0x20534444); Put(4, 124); Put(8, 0x81007); Put(12, 4); Put(16, 4); Put(20, 8); Put(28, 1);
        Put(76, 32); Put(80, 4); Put(84, 0x31545844); Put(108, 0x1000);
        Buffer.BlockCopy(BitConverter.GetBytes(endpoint), 0, bytes, 128, 2);
        File.WriteAllBytes(path, bytes); File.SetLastWriteTimeUtc(path, new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc));
    }
}
