// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Explicit private marker; the same source-pixel oracle runs without Wake-Up.
// Reading two copies of a black source is not proof that its artwork is correct.
internal static class PreviewTupleProbe
{
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    private static string Num(double n) => n.ToString("R", CultureInfo.InvariantCulture);
    private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    internal static void Run(string directory)
    {
        if (!LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "local.fixture.previewtuple")) return;
        var rows = new List<string>();
        try
        {
            var fgl = AccessTools.TypeByName("FasterGameLoading.FasterGameLoadingSettings");
            bool Setting(string name) => (bool)(AccessTools.Field(fgl, name)?.GetValue(null)
                ?? AccessTools.PropertyGetter(fgl, name)?.Invoke(null, null)
                ?? throw new InvalidOperationException("Missing FGL setting: " + name));
            foreach (string field in new[] { "earlyModContentLoading", "EnableMultiThreading", "XPathCaching" })
                Require(Setting(field), "Expected default FGL setting: " + field);
            foreach (string field in new[] { "DelayGraphicLoading", "StaticAtlasesBaking" })
                Require(!Setting(field), "Expected default FGL setting: " + field);
            var settings = AccessTools.PropertyGetter(AccessTools.TypeByName("ImageOptCompat.ImageOptCompatMod"), "Settings").Invoke(null, null);
            Require(!(bool)AccessTools.Field(settings.GetType(), "destroyOriginalTexture").GetValue(settings), "Original texture destruction must remain off");
            var pending = (IDictionary)AccessTools.Field(AccessTools.TypeByName("ImageOpt.ParallelTextureLoadPatch"), "Tasks").GetValue(null);
            Texture2D? copySource = null;
            foreach (var mod in LoadedModManager.RunningModsListForReading.Where(m => new[] {
                "local.fixture.previewtuple", "local.fixture.giddyinput", "erin.hair2", "vanillaexpanded.vplantsesucculents", "telkir.tmods.morefloors" }.Contains(m.PackageId)))
            {
                int checkedImages = 0;
                foreach (var pair in mod.GetContentHolder<Texture2D>().contentList.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    string? path = mod.foldersToLoadDescendingOrder.Select(root => Path.Combine(root, "Textures", pair.Key + ".png")).FirstOrDefault(File.Exists);
                    if (path == null || File.Exists(Path.ChangeExtension(path, ".dds")) || File.Exists(Path.ChangeExtension(path, ".dds.zstd"))) continue;
                    var actual = pair.Value;
                    if (actual == null) throw new InvalidOperationException("Missing image: " + pair.Key);
                    Require(!pending.Contains(actual.GetInstanceID()), "Pending image: " + pair.Key);
                    Require(ReferenceEquals(actual, ContentFinder<Texture2D>.Get(pair.Key, false)), "Wrong current holder object: " + pair.Key);
                    byte[] bytes = File.ReadAllBytes(path);
                    var expected = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        Require(ImageConversion.LoadImage(expected, bytes), "PNG oracle decoding failed");
                        Require(actual.width == expected.width && actual.height == expected.height, "Wrong source dimensions: " + pair.Key);
                        Color[] a = Pixels(actual), e = Pixels(expected);
                        double alpha = 0, alphaSum = 0, rgbSum = 0, rgbMax = 0; int opaque = 0;
                        for (int i = 0; i < a.Length; i++)
                        {
                            alpha = Math.Max(alpha, Math.Abs(a[i].a - e[i].a));
                            alphaSum += Math.Abs(a[i].a - e[i].a);
                            if (e[i].a < .1f) continue; // Transparent RGB is not visible artwork.
                            double error = Math.Max(Math.Abs(a[i].r - e[i].r), Math.Max(Math.Abs(a[i].g - e[i].g), Math.Abs(a[i].b - e[i].b)));
                            rgbMax = Math.Max(rgbMax, error); rgbSum += error; opaque++;
                        }
                        bool known = mod.PackageId == "local.fixture.previewtuple";
                        bool compressed = actual.format == TextureFormat.DXT1 || actual.format == TextureFormat.DXT5 || actual.format == TextureFormat.BC7;
                        Require(opaque > 0 && e.Any(c => c.a > .1f && Math.Max(c.r, Math.Max(c.g, c.b)) > .1f), "Source has no meaningful visible pixels");
                        rows.Add("{\"package\":" + Q(mod.PackageId) + ",\"path\":" + Q(pair.Key) + ",\"sourceSha256\":" + Q(Hash(bytes))
                            + ",\"width\":" + actual.width + ",\"height\":" + actual.height + ",\"format\":" + Q(actual.format.ToString())
                            + ",\"maxAlphaError\":" + Num(alpha) + ",\"meanAlphaError\":" + Num(alphaSum / a.Length)
                            + ",\"meanVisibleRgbError\":" + Num(rgbSum / opaque) + ",\"maxVisibleRgbError\":" + Num(rgbMax) + "}");
                        // Compressed artwork can legitimately differ from PNG.
                        // Keep the asymmetric probe's alpha exact; bound both
                        // average and worst alpha error for compressed real art.
                        Require(alpha <= (known || !compressed ? .005 : .1) && alphaSum / a.Length <= (compressed ? .015 : .005)
                            && rgbSum / opaque <= (compressed ? .04 : .005), "Artwork differs from source: " + pair.Key + " format=" + actual.format + " alpha=" + alpha + " meanRGB=" + rgbSum / opaque);
                        if (known) copySource = actual;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(expected); }
                    if (++checkedImages == 12) break;
                }
                Require(checkedImages > 0, "No actual PNG artwork checked for " + mod.PackageId);
            }
            Require(rows.Count >= 20 && copySource != null, "Insufficient source coverage");
            // A focused invocation of the real copy helper, not a spoofed vehicle
            // package or a claim that vehicle gameplay naturally ran.
            var copy = (Texture2D)AccessTools.Method(AccessTools.TypeByName("ImageOptCompat.VehicleReadback"), "ToCpuReadable").Invoke(null, new object[] { copySource! });
            if (copy == null) throw new InvalidOperationException("Compatibility copy missing");
            Require(!ReferenceEquals(copy, copySource), "Compatibility helper returned its source");
            var sourcePixels = Pixels(copySource!);
            try
            {
                var copiedPixels = Pixels(copy);
                Require(copiedPixels.Length == sourcePixels.Length && copiedPixels.Zip(sourcePixels, (a, b) => Math.Abs(a.a - b.a)).Max() < .005, "Compatibility copy alpha changed");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            Require(copySource != null && Pixels(copySource).Zip(sourcePixels, (a,b) => Math.Abs(a.a - b.a)).Max() < .005, "Destroying owned copy affected source");
            var menu = AccessTools.Method(typeof(RimWorld.MainMenuDrawer), "MainMenuOnGUI");
            Require(Harmony.GetPatchInfo(menu).Postfixes.Any(p => p.owner == "DegradingAnt.RimCompat"), "Compatibility status menu hook lost");
            var hooks = Harmony.GetAllPatchedMethods().SelectMany(m => { var p = Harmony.GetPatchInfo(m); return p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers)
                .Where(h => h.owner == "FasterGameLoadingMod" || h.owner == "DegradingAnt.RimCompat" || h.owner == "dev.soeur.ImageOpt")
                .Select(h => m.DeclaringType?.FullName + "." + m.Name + " | " + h.owner + " | " + h.PatchMethod.DeclaringType?.FullName + "." + h.PatchMethod.Name); }).ToArray();
            File.WriteAllLines(Path.Combine(directory, "preview-tuple-hooks.txt"), hooks);
            File.WriteAllText(Path.Combine(directory, "preview-tuple.json"), "{\"passed\":true,\"defaultFglSettings\":true,\"destroyOriginalTexture\":false,\"compatCopyAlphaAndLifetime\":true,\"sampledImages\":" + rows.Count + ",\"images\":[" + string.Join(",", rows) + "]}");
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(directory, "preview-tuple.json"), "{\"passed\":false,\"error\":" + Q(error.ToString()) + ",\"images\":[" + string.Join(",", rows) + "]}");
            Log.Error("[FixtureMenuObserver] Preview tuple verification failed: " + error);
        }
    }
    private static Color[] Pixels(Texture2D source)
    {
        var prior = RenderTexture.active;
        var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        try { Graphics.Blit(source, target); RenderTexture.active = target; copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply(); return copy.GetPixels(); }
        finally { RenderTexture.active = prior; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(copy); }
    }
}
