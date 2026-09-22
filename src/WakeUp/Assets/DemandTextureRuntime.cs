// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.IO;
using UnityEngine;
using Verse;

namespace WakeUp;

// Experimental private assets, selected explicitly for this process only. A
// pending entry is source metadata, never a public incomplete Unity object.
// Unknown dictionary/reflection bypasses are deliberately outside this trial's
// supported contract; there is no claim that every bypass can be detected.
public static class DemandTextureRuntime
{
    internal const string Owner = "wakeup.experimental-demand-textures";
    private const int MaximumEntries = 65536;
    // Disabled prepatch/C09 bridges touch this class too. Optional engine
    // reflection belongs inside explicit feature admission, never its cctor.
    private static FieldInfo TextureHolder = null!, HolderMod = null!, AudioHolderMod = null!, Trie = null!;
    private static MethodInfo TrieAdd = null!;
    private static Func<FileInfo, VirtualFile> ToFile = null!;
    private static bool accessorsInitialized;
    private sealed class Source
    {
        internal readonly string Logical;
        internal readonly FileInfo File;
        internal Source(string logical, FileInfo file) { Logical = logical; File = file; }
    }
    private sealed class HolderState
    {
        internal readonly ModContentPack Mod;
        internal readonly DeferredAssetSet<List<Source>, LoadedContentItem<Texture2D>> Pending = new();
        internal readonly Dictionary<string, List<Source>> Sources;
        internal readonly string Selection;
        internal string Exposure = "";
        internal HolderState(ModContentPack mod, Dictionary<string, List<Source>> sources)
        { Mod = mod; Sources = sources; Selection = PreparedTextureRuntime.SelectionIdentity(mod, Array.Empty<KeyValuePair<string, FileInfo>>()); }
    }
    private static readonly Dictionary<ModContentHolder<Texture2D>, HolderState> Holders = new();
    private static readonly HashSet<ModContentHolder<Texture2D>> Exposed = new();
    private static readonly List<PublishedPatchGuard> Guards = new();
    private static readonly bool Requested = Environment.GetCommandLineArgs().Contains("--wake-up-demand-textures=on");
    private static bool draining, prepared;
    private static PreparedTextureStore? store;
    public static bool Enabled { get; private set; }
    public static int PendingCount => Holders.Values.Sum(h => h.Pending.Count);
    public static long DeferredCount { get; private set; }
    public static long CompletedCount { get; private set; }
    public static long EagerCount { get; private set; }
    public static long NativeLoads { get; private set; }
    public static long PreparedHits { get; private set; }
    public static long PreparedRefusals { get; private set; }
    public static long Failures { get; private set; }
    public static string Status { get; private set; } = "Experimental demand textures off";

    internal static bool TryInitializeAccessors(IReadOnlyList<string> args, Func<Func<FileInfo, VirtualFile>>? conversionFactory = null)
    {
        if (StartupLaunchSelector.Parse(args).Selection != StartupSelection.Candidate
            || args.Count(a => a.StartsWith("--wake-up-demand-textures=", StringComparison.Ordinal)) != 1
            || !args.Contains("--wake-up-demand-textures=on")) return false;
        if (accessorsInitialized) return true;
        try
        {
            // Resolve locally so a missing conversion/field publishes no partial
            // accessor set. The factory is a focused refusal-test seam.
            FieldInfo Field(Type type, string name) => AccessTools.Field(type, name)
                ?? throw new MissingFieldException(type.FullName, name);
            var textureHolder = Field(typeof(ModContentPack), "textures");
            var holderMod = Field(typeof(ModContentHolder<Texture2D>), "mod");
            var audioHolderMod = Field(typeof(ModContentHolder<AudioClip>), "mod");
            var trie = Field(typeof(ModContentHolder<Texture2D>), "contentListTrie");
            var trieAdd = trie.FieldType.GetMethod("Add", new[] { typeof(string) })
                ?? throw new MissingMethodException("texture trie publication changed");
            var toFile = (conversionFactory ?? CreateFileConversion)()
                ?? throw new MissingMethodException("texture file conversion unavailable");
            TextureHolder = textureHolder; HolderMod = holderMod; AudioHolderMod = audioHolderMod;
            Trie = trie; TrieAdd = trieAdd; ToFile = toFile;
            accessorsInitialized = true;
            return true;
        }
        catch (Exception e) { Status = "Experimental demand textures refused: " + e.Message; return false; }
    }
    private static Func<FileInfo, VirtualFile> CreateFileConversion()
        => (Func<FileInfo, VirtualFile>)Delegate.CreateDelegate(typeof(Func<FileInfo, VirtualFile>),
            typeof(VirtualFile).Assembly.GetType("RimWorld.IO.FilesystemFile", true).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(FileInfo) })));

    internal static void Initialize(IReadOnlyList<string> args)
    {
        if (!TryInitializeAccessors(args)) return;
        try
        {
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason)) throw new InvalidOperationException(reason);
            DeferredAudioContract.Validate(typeof(ContentFinder<>).Assembly);
            // These callbacks must retain their ordinary native contract before
            // texture work can move across the startup boundary.
            foreach (MethodInfo method in new[] { PngRuntime.LoadAll, PngRuntime.LoadItem, PngRuntime.LoadTexture, PngRuntime.Image })
            {
                if (!SemanticMethodIdentity.TryHash(method, out string hash, out _)
                    || !PngRuntime.BodySupported(GameBuildContract.Current, method.Name, hash))
                    throw new InvalidOperationException("texture body changed: " + method.Name);
                if (!PublishedPatchGuard.TryCreate(method, Owner, out var guard, true) || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("opaque texture callback: " + method.Name);
                Guards.Add(guard!);
            }
            prepared = args.Contains("--wake-up-prepared=0");
            Enabled = true;
            DemandGraphicRuntime.Initialize(new Harmony(Owner));
            var expandable = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetType("VEF.Weapons.ExpandableGraphicData", false) != null);
            if (expandable != null) DemandExpandableTextureRuntime.Initialize(new Harmony(Owner + ".expandable"), expandable);
            UnityData.DisposeStatic += Dispose;
            Status = "Experimental private demand textures active; unknown direct access unsupported";
        }
        catch (Exception e) { Enabled = false; Status = "Experimental demand textures refused: " + e.Message; }
        Log.Message("[Wake-Up] " + Status);
    }

    private static bool Admitted()
    {
        if (!Enabled || !UnityData.IsInMainThread) return false;
        if (!draining && Guards.Any(g => !g.AllowsOriginalContract()))
        {
            CompleteAll("changed native callback");
            Enabled = false;
            Status = "Demand textures stopped after native callbacks changed";
            return false;
        }
        return true;
    }
    private static Dictionary<string, List<Source>> Discover(ModContentPack mod)
    {
        var result = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        string root = GenFilePaths.ContentPath<Texture2D>();
        foreach (var pair in PngRuntime.SelectedFiles(mod))
        {
            string key = pair.Key.Replace('\\', '/');
            if (key.StartsWith(root, StringComparison.Ordinal)) key = key.Substring(root.Length);
            key = key.Substring(0, key.Length - Path.GetExtension(key).Length);
            if (!result.TryGetValue(key, out var sources)) result.Add(key, sources = new List<Source>());
            sources.Add(new Source(pair.Key, pair.Value));
        }
        return result;
    }
    internal static bool TryReload(object value, bool hotReload)
    {
        if (!Admitted()) return false;
        var holder = (ModContentHolder<Texture2D>)value;
        if (hotReload || Exposed.Contains(holder)) { CompleteHolder(holder); return false; }
        if (Holders.ContainsKey(holder)) CompleteHolder(holder);
        var mod = (ModContentPack)HolderMod.GetValue(holder);
        var sources = Discover(mod);
        // Native reload still invokes constructors and reports duplicates even
        // when a key is already present. Keep those populations entirely native.
        if (holder.contentList.Count != 0 || sources.Values.Any(s => s.Count != 1)) return false;
        if (PendingCount + sources.Count > MaximumEntries) return false;
        var state = new HolderState(mod, sources);
        Holders[holder] = state;
        foreach (var pair in sources)
            if (!holder.contentList.ContainsKey(pair.Key))
            {
                state.Pending.Add(pair.Key, pair.Value, MaximumEntries); DeferredCount++;
                // The private path index records source order independently of
                // object readiness. Supported folder requests complete every
                // matching value before native enumeration can read it. Later
                // individual publication never invalidates a trie enumerator.
                TrieAdd.Invoke(Trie.GetValue(holder), new object[] { pair.Key });
            }
        return true;
    }
    public static ModContentHolder<Texture2D> GetHolder(ModContentPack mod)
        => Enabled ? (ModContentHolder<Texture2D>)TextureHolder.GetValue(mod) : mod.GetContentHolder<Texture2D>();
    internal static ModContentHolder<Texture2D> GetHolderForPrefix(ModContentPack mod, string prefix)
    {
        if (!Enabled) return mod.GetContentHolder<Texture2D>();
        var holder = GetHolder(mod);
        if (!Holders.TryGetValue(holder, out var state)) return holder;
        var keys = state.Pending.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (keys.Length == 0) return holder;
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("Pending texture folder must complete before worker dispatch.");
        Admitted();
        var selected = PlayDataLoader.Loaded ? Discover(mod) : state.Sources;
        foreach (string key in keys) Complete(holder, state, key, selected);
        return holder;
    }
    internal static void EnsurePath(object value, string path, bool subtree)
    {
        if (!Enabled && !draining) return;
        var holder = (ModContentHolder<Texture2D>)value;
        if (!Holders.TryGetValue(holder, out var state)) return;
        if (!UnityData.IsInMainThread)
        {
            if (subtree && state.Pending.Count != 0 || !subtree && state.Pending.Contains(path))
                throw new InvalidOperationException("Pending texture must be prepared on the main thread before worker dispatch.");
            return;
        }
        Admitted();
        if (!subtree) { Complete(holder, state, path); return; }
        string prefix = string.IsNullOrEmpty(path) || path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
        foreach (string key in state.Pending.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal)) Complete(holder, state, key);
    }
    internal static void ExposeHolder(object value)
    {
        var holder = (ModContentHolder<Texture2D>)value;
        if (!Enabled) { if (Requested) Exposed.Add(holder); return; }
        if (!UnityData.IsInMainThread)
        {
            if (Holders.TryGetValue(holder, out var state) && state.Pending.Count != 0)
                throw new InvalidOperationException("Pending texture holder must complete before worker exposure.");
            return;
        }
        Exposed.Add(holder);
        if (Holders.TryGetValue(holder, out var exposureState) && exposureState.Pending.Count != 0 && exposureState.Exposure.Length == 0)
            exposureState.Exposure = string.Join(" <- ", new System.Diagnostics.StackTrace(false).GetFrames().Skip(1).Take(10)
                .Select(f => f.GetMethod()?.DeclaringType?.FullName + "." + f.GetMethod()?.Name));
        long before = CompletedCount;
        CompleteHolder(holder);
        EagerCount += CompletedCount - before;
    }
    internal static void ClearHolder(object value)
    {
        var holder = (ModContentHolder<Texture2D>)value;
        if (Holders.TryGetValue(holder, out var state)) { state.Pending.Cancel(); Holders.Remove(holder); }
        // Exposed holders remain eager on subsequent reload. Native owns and
        // destroys all published values; this metadata owns none of them.
    }
    internal static bool HasPendingForAudioHolder(object value)
    {
        if (!Enabled || value == null) return false;
        var holder = GetHolder((ModContentPack)AudioHolderMod.GetValue(value));
        return Holders.TryGetValue(holder, out var state) && state.Pending.Count != 0;
    }
    public static Texture2D[] PrepareWorker(string[] paths)
    {
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("Prepare worker dependencies before dispatch on the main thread.");
        return paths.Select(path => ContentFinder<Texture2D>.Get(path)).ToArray();
    }
    public static void CompleteAll(string reason)
    {
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("Complete private assets on the main thread before opaque access.");
        bool previous = draining; draining = true;
        try
        {
            long before = CompletedCount;
            foreach (var holder in Holders.Keys.ToArray()) { Exposed.Add(holder); CompleteHolder(holder); }
            EagerCount += CompletedCount - before;
            DemandGraphicRuntime.CompleteAll(reason);
        }
        finally { draining = previous; }
    }
    private static void CompleteHolder(ModContentHolder<Texture2D> holder)
    {
        if (!Holders.TryGetValue(holder, out var state) || state.Pending.Count == 0) return;
        var current = Discover(state.Mod);
        foreach (string key in state.Pending.Keys) Complete(holder, state, key, current);
        // Preserve native insertion order at the point the dictionary escapes.
        var values = new Dictionary<string, Texture2D>(holder.contentList, StringComparer.Ordinal);
        holder.contentList.Clear();
        foreach (string key in state.Sources.Keys)
            if (values.TryGetValue(key, out var texture)) holder.contentList.Add(key, texture);
        foreach (var pair in values) if (!holder.contentList.ContainsKey(pair.Key)) holder.contentList.Add(pair.Key, pair.Value);
    }
    private static void Complete(ModContentHolder<Texture2D> holder, HolderState state, string key, Dictionary<string, List<Source>>? resolved = null)
    {
        if (!state.Pending.Contains(key)) return;
        bool completed = state.Pending.Complete(key, original =>
        {
            if (holder.contentList.TryGetValue(key, out var borrowed)) return new LoadedContentItem<Texture2D>(null, borrowed);
            // Re-resolve provider/folder precedence. Neither remembered metadata
            // nor prepared bytes may override a changed native source choice.
            var selected = resolved ?? (!PlayDataLoader.Loaded ? state.Sources : Discover(state.Mod));
            if (!LoadedModManager.RunningModsListForReading.Contains(state.Mod)) return null;
            // A source removed after discovery takes the native missing-file
            // path, including a native null result, before publication.
            if (!selected.TryGetValue(key, out var current)) current = original;
            foreach (var source in current)
            {
                Texture2D? texture = TryPrepared(state, source);
                if (texture != null) return new LoadedContentItem<Texture2D>(ToFile(source.File), texture);
                NativeLoads++;
                var item = ModContentLoader<Texture2D>.LoadItem(ToFile(source.File));
                if (item != null) return item;
            }
            return null;
        }, (path, item) =>
        {
            // LoadItem can successfully wrap a null texture for a missing file.
            // Native holders retain that key, and their trie enumerator expects
            // the matching dictionary entry even when its value is null.
            if (holder.contentList.TryGetValue(path, out var existing))
            { if (!ReferenceEquals(existing, item.contentItem)) Discard(item); return true; }
            holder.contentList.Add(path, item.contentItem);
            if (item.extraDisposable != null) holder.extraDisposables.Add(item.extraDisposable);
            return true;
        }, Discard);
        if (completed) CompletedCount++; else Failures++;
    }
    private static Texture2D? TryPrepared(HolderState state, Source source)
    {
        if (!prepared || !CacheLaunchPolicy.Current.AllowRead || Guards.Any(g => !g.AllowsOriginalContract())) return null;
        try
        {
            // Only C05 native records are admitted. Explicit lower-quality
            // choices never become this prototype's native-demand output.
            if (source.File.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && !NativeDdsCapture.Compatible) return null;
            string selection = PreparedTextureRuntime.SelectionIdentity(state.Mod, Array.Empty<KeyValuePair<string, FileInfo>>());
            if (PreparedTextureRuntime.UseAtStartup)
            {
                // C05 owns the category during startup; do not open a competing
                // writer. It releases that session at menu before later demand.
                var shared = PreparedTextureRuntime.TryResolve(state.Mod, source.Logical, source.File, selection);
                if (shared != null) PreparedHits++;
                return shared;
            }
            store ??= new PreparedTextureStore(PreparedTextureRuntime.SaveRoot);
            if (!store.HasOwner(PreparedTextureRuntime.OwnerIdentity(selection, source.Logical, 0))) return null;
            byte[] bytes = PreparedTextureRuntime.ReadSource(source.File);
            string identity = PreparedTextureRuntime.Identity(selection, source.Logical, source.File.FullName, bytes, 0, PngRuntime.PreparationPlatform(), "");
            var entry = store.Read(identity);
            if (entry == null) { PreparedRefusals++; return null; }
            var texture = PngRuntime.Restore(entry);
            texture.name = Path.GetFileNameWithoutExtension(source.File.Name);
            PreparedHits++;
            return texture;
        }
        catch (Exception) { PreparedRefusals++; return null; }
    }
    private static void Discard(LoadedContentItem<Texture2D> item)
    {
        item.extraDisposable?.Dispose();
        if (item.internalFile != null && item.contentItem != null && item.contentItem != BaseContent.BadTex)
            UnityEngine.Object.Destroy(item.contentItem);
    }
    // Plain records let the independent observer inspect pending metadata without
    // acquiring a public holder and thereby changing the observation.
    public static string[][] PendingSources() => Holders.Values.SelectMany(state => state.Pending.Keys.Select(key =>
        new[] { state.Mod.PackageId, key, state.Sources[key][0].File.FullName, state.Sources[key][0].Logical })).ToArray();
    public static object[] ProviderSnapshot() => Holders.Select(pair => (object)new Dictionary<string, object>
    {
        ["package"] = pair.Value.Mod.PackageId, ["selectedSources"] = pair.Value.Sources.Count,
        ["pending"] = pair.Value.Pending.Count, ["published"] = pair.Key.contentList.Count,
        ["publiclyExposed"] = Exposed.Contains(pair.Key), ["firstExposure"] = pair.Value.Exposure
    }).ToArray();
    public static string Snapshot() => "{\"enabled\":" + (Enabled ? "true" : "false") + ",\"pending\":" + PendingCount
        + ",\"deferred\":" + DeferredCount + ",\"completed\":" + CompletedCount + ",\"eager\":" + EagerCount
        + ",\"nativeLoads\":" + NativeLoads + ",\"preparedHits\":" + PreparedHits + ",\"preparedRefusals\":" + PreparedRefusals + ",\"failures\":" + Failures + "}";
    private static void Dispose()
    {
        foreach (var state in Holders.Values) state.Pending.Cancel();
        Holders.Clear(); Exposed.Clear(); Enabled = false;
        store?.Dispose(); store = null;
    }
}
