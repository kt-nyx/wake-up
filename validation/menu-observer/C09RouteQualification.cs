// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

// Bounded, nonvisual correctness checks. Native objects are borrowed and never
// destroyed. Temporary dictionary edits are restored even when a check fails.
internal static class C09RouteQualification
{
    private static Type Runtime = null!;
    private static FieldInfo Enabled = null!;
    private static readonly List<string> lines = new();
    private static int checks;
    private static string Q(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException(message); }
    private static object Cache(string field) => AccessTools.Field(Runtime, field).GetValue(null);
    private static long Hits(string field) => (long)AccessTools.Property(Cache(field).GetType(), "Hits").GetValue(Cache(field), null);
    private static string[] Paths(string field, string property) => (string[])AccessTools.Property(Cache(field).GetType(), property).GetValue(Cache(field), null);
    private static T Native<T>(Func<T> call)
    {
        bool old = (bool)Enabled.GetValue(null); Enabled.SetValue(null, false);
        try { return call(); } finally { Enabled.SetValue(null, old); }
    }
    internal static void Menu(string directory)
    {
        if (!FeatureQualification.Setting("AssetRouting")) return;
        bool passed = false;
        try
        {
            Runtime = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "WakeUp").GetType("WakeUp.AssetRoutingRuntime")!;
            Enabled = AccessTools.Field(Runtime, "enabled");
            Check((bool)Enabled.GetValue(null), "Broad routing refused initialization");
            Filesystem<Texture2D>("Textures"); Filesystem<AudioClip>("Audio"); Filesystem<string>("Strings");
            External<Texture2D>("Textures", "TextureBundles"); External<AudioClip>("Audio", "AudioBundles");
            Bundles<Shader>("ShaderBundles");
            MissingAndMutation();
            NativeStringProviders();
            ResourceCallbacks();
            BundleLifecycle();
            OwnedDestroyedObject();
            passed = true;
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(directory, "c09-routes-error.txt"), e.ToString()); }
        lines.Add("{\"event\":\"completed\",\"passed\":" + passed.ToString().ToLowerInvariant() + ",\"checks\":" + checks + "}");
        File.WriteAllLines(Path.Combine(directory, "c09-routes.jsonl"), lines);
    }
    private static void Filesystem<T>(string field) where T : class
    {
        int count = 0;
        foreach (var mod in LoadedModManager.RunningModsListForReading)
        {
            var holder = mod.GetContentHolder<T>();
            foreach (var sample in holder.contentList.Where(p => p.Value != null).Take(2).ToArray())
            {
                var expected = Native(() => ContentFinder<T>.Get(sample.Key, false));
                long before = Hits(field);
                Check(ReferenceEquals(expected, ContentFinder<T>.Get(sample.Key, false)), "filesystem first winner");
                Check(ReferenceEquals(expected, ContentFinder<T>.Get(sample.Key, false)), "filesystem repeated winner");
                Check(Hits(field) > before, "filesystem route did not hit");
                Check(ReferenceEquals(holder.Get(sample.Key), sample.Value), "direct provider identity");
                string folder = sample.Key.Contains("/") ? sample.Key.Substring(0, sample.Key.LastIndexOf('/')) : "";
                // A root audio query loads thousands of native clips; use its
                // direct provider folder here and scoped global folders below.
                var nativeFolder = Native(() => folder.Length == 0 ? holder.GetAllUnderPath(folder).ToArray() : ContentFinder<T>.GetAllInFolder(folder).ToArray());
                var routedFolder = folder.Length == 0 ? holder.GetAllUnderPath(folder).ToArray() : ContentFinder<T>.GetAllInFolder(folder).ToArray();
                Check(nativeFolder.Length == routedFolder.Length && nativeFolder.Zip(routedFolder, ReferenceEquals).All(v => v), "folder order/duplicates/identity");
                lines.Add("{\"event\":\"filesystem\",\"type\":" + Q(typeof(T).Name) + ",\"provider\":" + Q(mod.PackageIdPlayerFacing) + ",\"path\":" + Q(sample.Key) + ",\"folderCount\":" + nativeFolder.Length + "}");
                count++;
            }
            if (count >= 6) break;
        }
        lines.Add("{\"event\":\"family\",\"family\":" + Q("filesystem-" + typeof(T).Name) + ",\"samples\":" + count + "}");
    }
    private static void External<T>(string field, string bundles) where T : UnityEngine.Object
    {
        int count = 0;
        foreach (string path in Paths(field, "ExternalPaths"))
        {
            var resource = Resources.Load<T>(GenFilePaths.ContentPath<T>() + path);
            if (ReferenceEquals(resource, null)) continue;
            var expected = Native(() => ContentFinder<T>.Get(path, false));
            if (!ReferenceEquals(resource, expected)) continue;
            long before = Hits(field);
            Check(ReferenceEquals(expected, ContentFinder<T>.Get(path, false)), "Resources identity");
            Check(ReferenceEquals(expected, ContentFinder<T>.Get(path, false)), "Resources repeated identity");
            Check(Hits(field) > before, "Resources did not reuse route");
            lines.Add("{\"event\":\"resource\",\"type\":" + Q(typeof(T).Name) + ",\"path\":" + Q(path) + "}");
            if (++count == 3) break;
        }
        lines.Add("{\"event\":\"family\",\"family\":" + Q("Resources-" + typeof(T).Name) + ",\"samples\":" + count + "}");
        Bundles<T>(bundles);
    }
    private static void Bundles<T>(string field) where T : UnityEngine.Object
    {
        var paths = new List<string>(Paths(field, "Paths"));
        // Discover a bounded set of actual native names, without modifying the
        // public lazy trie/list caches or creating fabricated engine assets.
        foreach (var mod in LoadedModManager.RunningModsListForReading)
            foreach (var bundle in mod.assetBundles.loadedAssetBundles)
            {
                string[] suffixes = typeof(T) == typeof(Shader) ? ModAssetBundlesHandler.ShaderExtensions
                    : typeof(T) == typeof(AudioClip) ? ModAssetBundlesHandler.AudioClipExtensions : ModAssetBundlesHandler.TextureExtensions;
                foreach (string name in bundle.GetAllAssetNames().Where(n => suffixes.Contains(Path.GetExtension(n))).Take(8))
                    foreach (string provider in new[] { mod.FolderName, mod.PackageIdPlayerFacing })
                    {
                        string prefix = ("assets/data/" + provider + "/" + GenFilePaths.ContentPath<T>()).ToLowerInvariant();
                        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) paths.Add(name.Substring(prefix.Length, name.Length - prefix.Length - Path.GetExtension(name).Length));
                    }
            }
        int count = 0;
        foreach (string path in paths.Distinct().Take(16))
        {
            var expected = Native(() => ContentFinder<T>.TryFindAssetInModBundles(path));
            if (ReferenceEquals(expected, null)) continue;
            long before = Hits(field);
            Check(ReferenceEquals(expected, ContentFinder<T>.TryFindAssetInModBundles(path)), "direct bundle first");
            Check(ReferenceEquals(expected, ContentFinder<T>.TryFindAssetInModBundles(path)), "direct bundle repeat");
            Check(Hits(field) > before, "bundle route did not hit");
            var global = Native(() => ContentFinder<T>.Get(path, false));
            Check(ReferenceEquals(global, ContentFinder<T>.Get(path, false)), "global bundle precedence");
            lines.Add("{\"event\":\"bundle\",\"type\":" + Q(typeof(T).Name) + ",\"path\":" + Q(path) + "}");
            if (++count == 3) break;
        }
        lines.Add("{\"event\":\"family\",\"family\":" + Q("bundle-" + typeof(T).Name) + ",\"samples\":" + count + "}");
    }
    private static void MissingAndMutation()
    {
        const string path = "WakeUpC09/DeliberateMissingBoundary";
        var mods = LoadedModManager.RunningModsListForReading;
        var first = mods[0].GetContentHolder<string>().contentList;
        var last = mods[mods.Count - 1].GetContentHolder<string>().contentList;
        Check(!first.ContainsKey(path) && !last.ContainsKey(path), "boundary key already exists");
        Check(Native(() => ContentFinder<string>.Get(path, false)) == null && ContentFinder<string>.Get(path, false) == null, "native missing");
        try
        {
            first[path] = "first";
            Check(ContentFinder<string>.Get(path, false) == "first", "missing retry");
            Check(ContentFinder<string>.Get(path, false) == "first", "repeat first");
            last[path] = "last";
            Check(ContentFinder<string>.Get(path, false) == "last", "late overriding addition");
            last[path] = "replacement";
            Check(ContentFinder<string>.Get(path, false) == "replacement", "same-count replacement");
            last.Remove(path);
            Check(ContentFinder<string>.Get(path, false) == "first", "late removal");
        }
        finally { first.Remove(path); last.Remove(path); }
        Check(ContentFinder<string>.Get(path, false) == null, "removed result not cached");
        var requester = ContentFinderRequester.requester;
        try
        {
            ContentFinderRequester.requester = new ThingDef { defName = "C09MissingDiagnosticBoundary" };
            Log.Message("[C09] Native and routed missing diagnostics follow; both are deliberate.");
            Check(Native(() => ContentFinder<string>.Get(path, true)) == null, "native reported miss");
            Check(ContentFinder<string>.Get(path, true) == null, "routed reported miss");
        }
        finally { ContentFinderRequester.requester = requester; }
        lines.Add("{\"event\":\"deliberate-boundary\",\"missingRetryAndPublicMutation\":true}");
    }

    private static void NativeStringProviders()
    {
        var mods = LoadedModManager.RunningModsListForReading;
        var core = mods.First(m => m.IsCoreMod);
        string source = Path.Combine(core.RootDir, "Languages/English");
        Check(Directory.Exists(Path.Combine(source, "Strings")), "real native string source unavailable");
        var first = new ModContentPack(new DirectoryInfo(source), "c09.boundary.first", "C09.Boundary.First", 10000, "C09 string boundary", false);
        var last = new ModContentPack(new DirectoryInfo(source), "c09.boundary.last", "C09.Boundary.Last", 10001, "C09 string boundary", false);
        // Actual native file discovery, ReadAllText, trie and holder publication.
        first.GetContentHolder<string>().ReloadAll(); last.GetContentHolder<string>().ReloadAll();
        mods.Add(first); mods.Add(last);
        try
        {
            var a = first.GetContentHolder<string>(); var b = last.GetContentHolder<string>();
            var sample = b.contentList.First(p => p.Value.Length > 0 && p.Key.Contains("/"));
            string path = sample.Key;
            Check(!ReferenceEquals(a.Get(path), b.Get(path)), "distinct native string publications");
            Check(a.Get(path) == b.Get(path), "same original text bytes");
            long before = Hits("Strings");
            Check(ReferenceEquals(ContentFinder<string>.Get(path, false), b.Get(path)), "last native string provider");
            Check(ReferenceEquals(ContentFinder<string>.Get(path, false), b.Get(path)), "string route hit identity");
            Check(Hits("Strings") > before, "native strings did not route");
            string folder = path.Substring(0, path.LastIndexOf('/'));
            var original = Native(() => ContentFinder<string>.GetAllInFolder(folder).ToArray());
            var routed = ContentFinder<string>.GetAllInFolder(folder).ToArray();
            Check(original.Length == routed.Length && original.Zip(routed, ReferenceEquals).All(x => x), "string folder duplicates/order");
            mods.Remove(last);
            Check(ReferenceEquals(ContentFinder<string>.Get(path, false), a.Get(path)), "provider removal");
            mods.Insert(0, last);
            Check(ReferenceEquals(ContentFinder<string>.Get(path, false), a.Get(path)), "provider reorder");
            var replacement = new ModContentHolder<string>(first); replacement.ReloadAll();
            var holderField = AccessTools.Field(typeof(ModContentPack), "strings");
            holderField.SetValue(first, replacement);
            Check(ReferenceEquals(ContentFinder<string>.Get(path, false), replacement.Get(path)), "live holder replacement");
            lines.Add("{\"event\":\"native-filesystem-string-boundary\",\"source\":" + Q(source) + ",\"path\":" + Q(path) + ",\"nativeEntriesPerProvider\":" + a.contentList.Count + ",\"folderCount\":" + original.Length + ",\"syntheticProviders\":2,\"originalFilesChanged\":false}");
        }
        finally { mods.Remove(first); mods.Remove(last); AccessTools.Method(Runtime, "Clear").Invoke(null, null); }
    }

    private sealed class CallbackResources : ResourcesAPI
    {
        internal string Path = "";
        internal Action? Action;
        internal int Calls;
        protected override UnityEngine.Object Load(string path, Type type)
        {
            if (path == Path) { Calls++; Action?.Invoke(); }
            return base.Load(path, type);
        }
    }
    private static int lateGetterCalls;
    private static void LateGetter() => lateGetterCalls++;
    private static void ResourceCallbacks()
    {
        const string path = "Things/Item/Book/Textbook/Textbook";
        string resourcePath = GenFilePaths.ContentPath<Texture2D>() + path;
        var resource = Resources.Load<Texture2D>(resourcePath);
        Check(resource != null && ReferenceEquals(resource, Native(() => ContentFinder<Texture2D>.Get(path, false))), "resource callback sample must be native winner");
        var mods = LoadedModManager.RunningModsListForReading;
        var holder = mods[mods.Count - 1].GetContentHolder<Texture2D>().contentList;
        Check(!holder.ContainsKey(path), "callback override key is not private");
        ResourcesAPI previous = ResourcesAPI.overrideAPI;
        Check(previous == null, "probe requires default Resources API");
        var callback = new CallbackResources { Path = resourcePath };
        var harmony = new Harmony("WakeUp.C09.PrivateCallbackProbe");
        var target = AccessTools.PropertyGetter(typeof(LoadedModManager), "RunningModsListForReading");
        try
        {
            ResourcesAPI.overrideAPI = callback;
            callback.Action = () => holder[path] = resource!;
            Check(ReferenceEquals(ContentFinder<Texture2D>.Get(path, false), resource), "callback successful resource");
            Check(callback.Calls == 1, "callback executes once");
            Check(ReferenceEquals(ContentFinder<Texture2D>.Get(path, false), resource) && callback.Calls == 1, "late holder wins without resource callback");
            holder.Remove(path);
            callback.Action = () => {
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(C09RouteQualification), nameof(LateGetter)));
                lateGetterCalls = 0;
            };
            Check(ReferenceEquals(ContentFinder<Texture2D>.Get(path, false), resource), "hook-changing callback result");
            Check(lateGetterCalls == 0, "no post-publication provider callback replay");
            harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
            callback.Action = () => { throw new InvalidOperationException("c09 deliberate resource exception"); };
            int before = callback.Calls;
            string native = Failure(() => Native(() => ContentFinder<Texture2D>.Get(path, false)));
            string routed = Failure(() => ContentFinder<Texture2D>.Get(path, false));
            Check(native == routed && native.Contains("c09 deliberate"), "resource exception preserved");
            Check(callback.Calls - before == 2, "one callback per exceptional request");
            callback.Action = null;
            Check(ReferenceEquals(ContentFinder<Texture2D>.Get(path, false), resource), "callback failure retries");
            var strings = mods.Last().GetContentHolder<string>().contentList;
            const string nested = "WakeUpC09/ReentrantString";
            Check(!strings.ContainsKey(nested), "reentrant test key collision");
            try
            {
                strings[nested] = "ready";
                callback.Action = () => Check(ContentFinder<string>.Get(nested, false) == "ready", "reentrant native lookup");
                int nestedBefore = callback.Calls;
                Check(ReferenceEquals(ContentFinder<Texture2D>.Get(path, false), resource), "reentrant resource result");
                Check(callback.Calls == nestedBefore + 1, "reentrant callback not replayed");
            }
            finally { strings.Remove(nested); }
            lines.Add("{\"event\":\"resource-callbacks\",\"mutationHookPublicationExceptionRetryAndReentrancy\":true}");
        }
        finally
        {
            callback.Action = null; ResourcesAPI.overrideAPI = previous;
            holder.Remove(path); harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
        }
    }
    private static string Failure(Func<object?> call)
    { try { return call() == null ? "null" : "asset"; } catch (Exception e) { return e.GetType().FullName + ":" + e.Message; } }

    private static void BundleLifecycle()
    {
        // Use a real nonofficial shader bundle with its native source files.
        // Unload(false) preserves all previously published shader objects.
        foreach (var mod in LoadedModManager.RunningModsListForReading.Where(m => !m.IsOfficialMod))
        {
            var list = mod.assetBundles.loadedAssetBundles;
            if (list.Count != 1) continue;
            var oldBundle = list[0];
            string prefix = ("assets/data/" + mod.FolderName + "/" + GenFilePaths.ContentPath<Shader>()).ToLowerInvariant();
            string? name = oldBundle.GetAllAssetNames().FirstOrDefault(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".shader", StringComparison.Ordinal));
            if (name == null)
            {
                prefix = ("assets/data/" + mod.PackageIdPlayerFacing + "/" + GenFilePaths.ContentPath<Shader>()).ToLowerInvariant();
                name = oldBundle.GetAllAssetNames().FirstOrDefault(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".shader", StringComparison.Ordinal));
            }
            if (name == null) continue;
            string path = name.Substring(prefix.Length, name.Length - prefix.Length - 7);
            var borrowed = oldBundle.LoadAsset<Shader>(name);
            Check(borrowed != null, "real shader load");
            Check(ReferenceEquals(ContentFinder<Shader>.TryFindAssetInModBundles(path), Native(() => ContentFinder<Shader>.TryFindAssetInModBundles(path))), "shader native winner before unload");
            ContentFinder<Shader>.TryFindAssetInModBundles(path);
            string suffix = ModAssetBundlesHandler.ShaderExtensions[0];
            try
            {
                ModAssetBundlesHandler.ShaderExtensions[0] = ".c09missing";
                Check(ReferenceEquals(ContentFinder<Shader>.TryFindAssetInModBundles(path), Native(() => ContentFinder<Shader>.TryFindAssetInModBundles(path))), "mutable extension invalidation");
            }
            finally { ModAssetBundlesHandler.ShaderExtensions[0] = suffix; }
            ContentFinder<Shader>.TryFindAssetInModBundles(path);
            oldBundle.Unload(false);
            try
            {
                string native = Failure(() => Native(() => ContentFinder<Shader>.TryFindAssetInModBundles(path)));
                string routed = Failure(() => ContentFinder<Shader>.TryFindAssetInModBundles(path));
                Check(native == routed, "unloaded bundle retained in public list follows native failure");
            }
            finally
            {
                list.Remove(oldBundle);
                mod.assetBundles.ReloadAll();
            }
            Check(list.Count == 1 && list[0] != null && !ReferenceEquals(list[0], oldBundle), "native bundle reload generation");
            var expected = Native(() => ContentFinder<Shader>.TryFindAssetInModBundles(path));
            Check(expected != null && ReferenceEquals(ContentFinder<Shader>.TryFindAssetInModBundles(path), expected), "reload current object");
            Check(ReferenceEquals(ContentFinder<Shader>.TryFindAssetInModBundles(path), expected), "reload repeated route");
            Check(borrowed != null, "borrowed shader survives unload false");
            lines.Add("{\"event\":\"native-bundle-lifecycle\",\"provider\":" + Q(mod.PackageIdPlayerFacing) + ",\"name\":" + Q(name) + ",\"unloadFalseReloadAndBorrowedLifetime\":true}");
            return;
        }
        throw new InvalidOperationException("No real single native shader bundle available for lifecycle check");
    }

    private static void OwnedDestroyedObject()
    {
        const string path = "WakeUpC09/OwnedTextureBoundary";
        var mod = LoadedModManager.RunningModsListForReading.Last();
        var holder = mod.GetContentHolder<Texture2D>();
        Check(!holder.contentList.ContainsKey(path), "owned texture key collision");
        Texture2D? first = new Texture2D(2, 2); Texture2D? second = null;
        try
        {
            holder.contentList[path] = first;
            Check(ReferenceEquals(ContentFinder<Texture2D>.Get(path, false), first), "owned first generation");
            ContentFinder<Texture2D>.Get(path, false);
            UnityEngine.Object.DestroyImmediate(first);
            holder.contentList.Remove(path);
            second = new Texture2D(2, 2); holder.contentList[path] = second;
            Check(ReferenceEquals(ContentFinder<Texture2D>.Get(path, false), second), "destroyed object is not retained by route");
            Texture2D? workerValue = null;
            var thread = new System.Threading.Thread(() => workerValue = holder.Get(path));
            thread.Start(); Check(thread.Join(5000), "native direct worker access does not wait for Unity");
            Check(ReferenceEquals(workerValue, second), "native direct worker identity");
            lines.Add("{\"event\":\"owned-texture-boundary\",\"destroyedReplacementAndDirectWorkerIdentity\":true,\"borrowedObjectsDestroyed\":false}");
        }
        finally
        {
            holder.contentList.Remove(path);
            if (first != null) UnityEngine.Object.DestroyImmediate(first);
            if (second != null) UnityEngine.Object.DestroyImmediate(second);
        }
    }
}
