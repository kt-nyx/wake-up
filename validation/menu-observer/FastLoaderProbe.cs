// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Xml;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Functional-only: use the supplier's ordinary post-menu builder and observe
// its restored objects on a separate launch. Never initialize its caches early.
internal static class FastLoaderProbe
{
    internal static string Mode => Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--fixture-fastloader="))?.Split('=')[1] ?? "";
    internal static bool Selected => Mode.Length != 0;
    private static Type Supplier(string name) => LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == "solaris.fastloader")
        .assemblies.loadedAssemblies.Select(a => a.GetType("FastLoader." + name)).Single(t => t != null)!;
    private static object Field(object owner, string name) => AccessTools.Field(owner.GetType(), name).GetValue(owner);
    private static object Property(object owner, string name) => AccessTools.Property(owner.GetType(), name).GetValue(owner, null);
    private static object Static(Type type, string name) => AccessTools.Property(type, name).GetValue(null, null);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static IEnumerator Run(string directory, Action<string> fail)
    {
        Window? window = null;
        object? session = null;
        string error = "";
        try
        {
            Require(Mode == "build" || Mode == "warm", "Unknown FastLoader functional mode");
            if (Mode == "warm") VerifyWarm(directory);
            else
            {
                string cold = CompatibilityAssetProbe.Run(directory);
                Require(cold.Length == 0, cold);
                window = (Window)Activator.CreateInstance(Supplier("TextureCacheBuildWindow"));
                session = Field(window, "session");
                Find.WindowStack.Add(window);
            }
        }
        catch (Exception e) { error = e.ToString(); }
        float deadline = Time.realtimeSinceStartup + 240;
        while (error.Length == 0 && session != null)
        {
            yield return null; // The supplier's ordinary WindowUpdate advances its session.
            bool done = false;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Native texture builder exceeded four minutes");
                done = (bool)Property(session, "Finished");
                if (done && (bool)Field(window!, "completionHandled") && !window!.IsOpen)
                {
                    Require(!(bool)Property(session, "Cancelled"), "Native texture build was cancelled");
                    Require(Convert.ToInt32(Property(session, "ProcessedModCount")) == Convert.ToInt32(Property(session, "TotalMods")), "Not all native texture build work completed");
                    int count = Convert.ToInt32(Property(session, "SavedTextureCount"));
                    Require(count >= 2, "Native texture builder saved no fixture images");
                    string root = Path.Combine(GenFilePaths.ConfigFolderPath, "FastLoader");
                    Require(File.Exists(Path.Combine(root, "TextureCache", "mod_local.fixture.compatibilityimages.texcache")), "Fixture RAW cache file missing");
                    var xml = new XmlDocument(); xml.Load(Path.Combine(root, "cache_state.xml"));
                    var record = xml.SelectSingleNode("/FastLoaderCacheState/stages/stage[@kind='texture']");
                    Require(record?.Attributes?["success"]?.Value == "true" && record.Attributes["itemCount"]?.Value == count.ToString(), "Native texture cache completion was not persisted");
                    File.WriteAllText(Path.Combine(directory, "fastloader-native.json"), "{\"passed\":true,\"mode\":\"build\",\"savedTextures\":" + count + ",\"cachedMods\":" + Property(session, "CachedModCount") + "}");
                    session = null;
                }
            }
            catch (Exception e) { error = e.ToString(); }
        }
        if (error.Length != 0)
        {
            File.WriteAllText(Path.Combine(directory, "fastloader-native-error.txt"), error);
            fail(" FastLoader native probe failed: " + error);
        }
    }
    private static void VerifyWarm(string directory)
    {
        var cache = Supplier("TextureRawCache"); var runtime = Supplier("FastLoaderRuntime");
        long hits = Convert.ToInt64(Static(cache, "CacheHitCount")), entriesHit = Convert.ToInt64(Static(cache, "CacheHitEntryCount"));
        long rawBytes = Convert.ToInt64(Static(cache, "CacheHitRawBytes"));
        Require(hits > 0 && entriesHit >= 2 && rawBytes > 0 && (bool)Static(runtime, "ShouldUseTextureCache") && !(bool)Static(runtime, "IsCacheFallbackActive"), "No genuine startup RAW hits or supplier fallback active");
        var caches = (IDictionary)AccessTools.Field(cache, "loadedCaches").GetValue(null);
        Require(caches.Contains("local.fixture.compatibilityimages"), "Supplier did not load fixture image cache");
        var entries = ((IEnumerable)caches["local.fixture.compatibilityimages"]).Cast<object>().ToArray();
        var mod = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == "local.fixture.compatibilityimages");
        foreach (var expected in new[] { (Path: "Phase2/Opaque", Width: 32, Height: 32), (Path: "Phase2/Alpha", Width: 32, Height: 16) })
        {
            var entry = entries.Single(e => (string)Field(e, "InternalPath") == expected.Path);
            int Number(string name) => Convert.ToInt32(Field(entry, name));
            var actual = mod.GetContentHolder<Texture2D>().Get(expected.Path);
            Require(actual != null && ReferenceEquals(actual, ContentFinder<Texture2D>.Get(expected.Path, false)), "Supplier texture/provider missing");
            Require(Number("Width") == expected.Width && Number("Height") == expected.Height && actual!.width == expected.Width && actual.height == expected.Height
                && (int)actual.format == Number("TextureFormat") && actual.mipmapCount == Number("MipmapCount") && actual.name == (string)Field(entry, "Name")
                && (int)actual.filterMode == Number("FilterMode") && actual.anisoLevel == Number("AnisoLevel"), "Supplier texture metadata differs");
            var raw = (byte[])Field(entry, "RawData");
            Require(raw.Length > 0 && Number("MipmapCount") == 1, "Unexpected stored RAW representation");
            Texture2D? reference = null;
            try
            {
                reference = new Texture2D(expected.Width, expected.Height, (TextureFormat)Number("TextureFormat"), false);
                reference.LoadRawTextureData(raw); reference.Apply(false, true);
                Require(C08TexturePixels.Revision(actual!) == C08TexturePixels.Revision(reference), "Restored GPU pixels differ from captured supplier bytes");
            }
            finally { if (reference != null) UnityEngine.Object.DestroyImmediate(reference); }
        }
        File.WriteAllText(Path.Combine(directory, "fastloader-native.json"), "{\"passed\":true,\"mode\":\"warm\",\"startupHits\":" + hits
            + ",\"startupEntries\":" + entriesHit + ",\"startupRawBytes\":" + rawBytes + ",\"sameProviderObjects\":true,\"gpuRepresentationMatches\":true,\"expectedImages\":2}");
    }
}
