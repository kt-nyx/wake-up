// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Selected only by private, code-free fixture inputs and the functional
// compatibility observer. Borrow native assets; never alter supplier choices.
internal static class CompatibilityAssetProbe
{
    private static int wrapperCalls;
    private static object Field(Type type, string name, object? owner = null) => AccessTools.Field(type, name).GetValue(owner);
    private static long Count(Type type, string name, object? owner = null) => Convert.ToInt64(Field(type, name, owner));
    private static long Property(object owner, string name) => Convert.ToInt64(AccessTools.Property(owner.GetType(), name).GetValue(owner, null));
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    internal static string Run(string directory)
    {
        bool images = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "local.fixture.compatibilityimages");
        bool bundle = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "local.fixture.compatibilitybundle");
        if (!images && !bundle) return "";
        try
        {
            if (SourceSelectionProbe.Selected)
                Require(File.Exists(Path.Combine(directory, "source-selection.json")) && File.ReadAllText(Path.Combine(directory, "source-selection.json")).Contains("\"passed\":true"), "Source selection startup probe failed or did not run");
            string receipt = images ? Images() : Bundle();
            File.WriteAllText(Path.Combine(directory, "compatibility-assets.json"), "{\"passed\":true," + receipt + "}");
            return "";
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(directory, "compatibility-assets-error.txt"), error.ToString());
            return " Asset workload failed: " + error.Message;
        }
    }
    private static string Images()
    {
        var type = AccessTools.TypeByName("WakeUp.PngRuntime");
        Require((string)Field(type, "mode") == "verify-cache", "Native image comparison must be enabled");
        var mod = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == "local.fixture.compatibilityimages");
        foreach (var expected in new[] { (Path: "Phase2/Opaque", Width: 32, Height: 32), (Path: "Phase2/Alpha", Width: 32, Height: 16) })
        {
            var texture = mod.GetContentHolder<Texture2D>().Get(expected.Path);
            Require(texture != null && texture.width == expected.Width && texture.height == expected.Height, "Expected image missing or dimensions changed: " + expected.Path);
            Require(ReferenceEquals(texture, ContentFinder<Texture2D>.Get(expected.Path, false)), "Unexpected image provider: " + expected.Path);
        }
        object cache = Field(type, "cache") ?? throw new InvalidOperationException("Image cache not active");
        long hits = Count(cache.GetType(), "Hits", cache), writes = Count(cache.GetType(), "Writes", cache);
        long verified = Count(type, "verified"), mismatches = Count(type, "mismatches");
        Require(verified >= 2 && mismatches == 0 && Count(type, "errors") == 0 && Count(cache.GetType(), "Errors", cache) == 0,
            "Native image comparison failed or did not execute");
        Require(hits + writes >= 2, "Image cache neither built nor reused expected entries");
        return "\"kind\":\"images\",\"expectedImages\":2,\"nativeComparisons\":" + verified
            + ",\"mismatches\":" + mismatches + ",\"hits\":" + hits + ",\"writes\":" + writes;
    }
    private static void CountWrapper(Type itemType, string itemPath)
    { if (itemType == typeof(Texture2D) && itemPath == "UI/Icons/FluidIdeo") wrapperCalls++; }
    private static string Bundle()
    {
        const string path = "UI/Icons/FluidIdeo", asset = "assets/data/ideology/textures/ui/icons/fluidideo.png";
        var runtime = AccessTools.TypeByName("WakeUp.AssetRoutingRuntime");
        var enabled = AccessTools.Field(runtime, "bundlesEnabled");
        Require((bool)enabled.GetValue(null) && !(bool)Field(runtime, "enabled"), "Expected independent bundle admission beside YaOpt");
        var supplier = AccessTools.TypeByName("YaOpt.Patches.Prepatch.Verse_ContentFinder_Get");
        Require((bool)Field(supplier, "Enabled"), "YaOpt wrapper not enabled");
        var provider = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == "ludeon.rimworld.ideology");
        var source = provider.assetBundles.loadedAssetBundles.Single(b => b.GetAllAssetNames().Contains(asset));
        var expected = source.LoadAsset<Texture2D>(asset);
        Require(expected != null, "Native bundle asset missing");
        try
        {
            enabled.SetValue(null, false); // Only Wake-Up's optional route; supplier remains intact.
            Require(ReferenceEquals(expected, ContentFinder<Texture2D>.TryFindAssetInModBundles(path)), "Native bundle winner differs");
        }
        finally { enabled.SetValue(null, true); }
        object cache = Field(runtime, "TextureBundles");
        long hits = Property(cache, "Hits");
        Require(ReferenceEquals(expected, ContentFinder<Texture2D>.TryFindAssetInModBundles(path)), "First routed bundle winner differs");
        long loads = Property(cache, "Loads");
        Require(ReferenceEquals(expected, ContentFinder<Texture2D>.TryFindAssetInModBundles(path)), "Repeated bundle winner differs");
        Require(Property(cache, "Hits") > hits && Property(cache, "Loads") == loads + 1, "Repeated route did not hit and perform its native load");
        var method = AccessTools.Method(AccessTools.TypeByName("YaOpt.Helpers.ContentManager"), "GetContent", new[] { typeof(Type), typeof(string), typeof(bool) });
        var harmony = new Harmony("local.fixture.compatibility-assets");
        var prefix = AccessTools.Method(typeof(CompatibilityAssetProbe), nameof(CountWrapper));
        wrapperCalls = 0; long wrapperHits = Property(cache, "Hits");
        try
        {
            harmony.Patch(method, prefix: new HarmonyMethod(prefix));
            Require(ReferenceEquals(expected, ContentFinder<Texture2D>.Get(path, false)), "YaOpt first winner differs");
            Require(ReferenceEquals(expected, ContentFinder<Texture2D>.Get(path, false)), "YaOpt repeated winner differs");
            Require(wrapperCalls == 2 && Property(cache, "Hits") >= wrapperHits + 2, "YaOpt wrapper did not execute both routed calls");
        }
        finally { harmony.Unpatch(method, prefix); }
        Require((bool)Field(supplier, "Enabled") && (bool)enabled.GetValue(null) && !(bool)Field(runtime, "enabled"), "Admission changed during bundle checks");
        return "\"kind\":\"bundle\",\"provider\":\"ludeon.rimworld.ideology\",\"path\":\"" + path
            + "\",\"sameNativeObject\":true,\"width\":" + expected!.width + ",\"height\":" + expected.height
            + ",\"hitDelta\":" + (Property(cache, "Hits") - hits) + ",\"wrapperCalls\":" + wrapperCalls;
    }
}
