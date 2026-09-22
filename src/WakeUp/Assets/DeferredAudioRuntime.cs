// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.IO;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;

namespace WakeUp;

// Bridges are public only because Prepatcher emits calls in game/mod assemblies.
// No incomplete AudioClip escapes. The holder receives the ordinary native clip
// and native stream owner together, and remains their lifetime owner.
public static class DeferredAudioRuntime
{
    internal const string Owner = "kt-nyx.wake-up.deferred-audio";
    internal const int MaxEntries = 16384;
    // Optional reflection must not run in this class's initializer: inert
    // prepatch bridges are also used when the feature is off or refused.
    private static FieldInfo AudioHolder = null!, HolderMod = null!, Trie = null!;
    private static MethodInfo TrieAdd = null!;
    private static MethodInfo GetAudioFormat = null!;
    private static Func<FileInfo, VirtualFile> ToVirtualFile = null!;
    private static readonly Dictionary<ModContentHolder<AudioClip>, HolderState> Holders = new();
    private static readonly HashSet<SongDef> Songs = new();
    private static readonly Queue<SongDef> Warmups = new();
    private static readonly HashSet<ModContentHolder<AudioClip>> Escaped = new();
    private static volatile bool enabled, drainRequested;
    private static bool draining, installed, earlyExposure;
    internal static Func<VirtualFile, LoadedContentItem<AudioClip>> LoadSource = null!;
    internal static Func<VirtualFile, LoadedContentItem<AudioClip>?> LoadReadySource = null!;
    internal static Func<string, AudioClip> FindClip = path => ContentFinder<AudioClip>.Get(path);
    internal static Action<AudioClip> DestroyClip = clip => UnityEngine.Object.Destroy(clip);
    private static int completed, failures, exposures;
    private static DeferredAudioPatchGuard? guard;
    private static readonly HashSet<Assembly> Inspected = new();
    internal static string Status { get; private set; } = "Deferred audio is off.";
    private sealed class HolderState
    {
        internal readonly ModContentPack Mod;
        internal readonly DeferredAssetSet<List<FileInfo>, LoadedContentItem<AudioClip>> Pending = new();
        internal bool Escaped;
        internal string[] Order = Array.Empty<string>();
        internal HolderState(ModContentPack mod) { Mod = mod; }
    }
    internal static int PendingCount => Holders.Values.Sum(s => s.Pending.Count);
    private static void InitializeAccessors()
    {
        LoadSource = ModContentLoader<AudioClip>.LoadItem;
        LoadReadySource = LoadReadyStream;
        GetAudioFormat = AccessTools.Method(typeof(ModContentLoader<AudioClip>), "GetFormat") ?? throw new InvalidOperationException("audio format selection changed");
        AudioHolder = AccessTools.Field(typeof(ModContentPack), "audioClips") ?? throw new InvalidOperationException("audio holder field changed");
        HolderMod = AccessTools.Field(typeof(ModContentHolder<AudioClip>), "mod") ?? throw new InvalidOperationException("holder provider field changed");
        Trie = AccessTools.Field(typeof(ModContentHolder<AudioClip>), "contentListTrie") ?? throw new InvalidOperationException("audio trie field changed");
        TrieAdd = Trie.FieldType.GetMethod("Add", new[] { typeof(string) }) ?? throw new InvalidOperationException("audio trie publication changed");
        ToVirtualFile = (Func<FileInfo, VirtualFile>)Delegate.CreateDelegate(typeof(Func<FileInfo, VirtualFile>),
            typeof(VirtualFile).Assembly.GetType("RimWorld.IO.FilesystemFile", true).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(FileInfo) })));
    }
    private static LoadedContentItem<AudioClip>? LoadReadyStream(VirtualFile file)
    {
        // Pending sources were admitted as native streams. Keep that mode even
        // if a source is replaced by a smaller file; never start a background
        // decode and expose its not-yet-ready clip at the point of first use.
        Stream? stream = null;
        AudioClip? clip = null;
        bool transferred = false;
        try
        {
            stream = file.CreateReadStream();
            clip = Manager.Load(stream, (AudioFormat)GetAudioFormat.Invoke(null, new object[] { file.Name }),
                file.Name, doStream: true, loadInBackground: false);
            // The native loader returns a content container even if its audio
            // manager returned an unusable clip. Preserve that distinction from
            // a thrown file/loader error, which permits a later source candidate.
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
                return new LoadedContentItem<AudioClip>(file, null!);
            clip.name = Path.GetFileNameWithoutExtension(file.Name);
            var item = new LoadedContentItem<AudioClip>(file, clip, stream);
            transferred = true;
            return item;
        }
        catch (Exception exception)
        {
            Log.Warning("[Wake-Up] Could not complete audio " + file.FullPath + ": " + exception.GetBaseException().Message);
            return null;
        }
        finally
        {
            if (!transferred)
            {
                stream?.Dispose();
                if (clip != null) DestroyClip(clip);
            }
        }
    }

    internal static void TryInitialize(IReadOnlyList<string> args)
    {
        if (StartupLaunchSelector.Parse(args).Selection != StartupSelection.Candidate
            || args.Count(a => a.StartsWith("--wake-up-deferred-audio=", StringComparison.Ordinal)) != 1
            || !args.Contains("--wake-up-deferred-audio=on")) return;
        try
        {
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason)) throw new InvalidOperationException(reason);
            InitializeAccessors();
            if (earlyExposure) throw new InvalidOperationException("an audio holder was retained before feature selection");
            if (typeof(SongDef).GetMethod("WakeUpDeferredAudioContract")?.Invoke(null, null) is not int version || version != 3)
                throw new InvalidOperationException("required consumer rewrites are unavailable");
            DeferredAudioContract.Validate(typeof(SongDef).Assembly);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) CheckConsumer(assembly);
            guard = new DeferredAudioPatchGuard();
            if (!guard.Allows()) throw new InvalidOperationException("another patch changes audio ownership or consumers");
            var harmony = new Harmony(Owner);
            harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(DeferredAudioRuntime), nameof(Idle)));
            UnityData.DisposeStatic += Dispose;
            AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded;
            enabled = installed = true;
            Status = "Deferred audio is ready; awaiting supported filesystem streams.";
        }
        catch (Exception exception) { Status = "Deferred audio refused: " + exception.Message + ". Ordinary loading is in use."; }
        Log.Message("[Wake-Up] " + Status);
    }
    private static void CheckConsumer(Assembly assembly)
    {
        lock (Inspected)
        {
        if (Inspected.Contains(assembly)) return;
        if (assembly == typeof(DeferredAudioRuntime).Assembly || assembly == typeof(SongDef).Assembly) { Inspected.Add(assembly); return; }
        if (!assembly.GetReferencedAssemblies().Any(a => a.Name == "Assembly-CSharp")) { Inspected.Add(assembly); return; }
        if (assembly.GetType("WakeUp.Generated.DeferredAudioConsumerContract")?.GetMethod("Version")?.Invoke(null, null) is not int version || version != 3)
            throw new InvalidOperationException("uncovered audio consumer assembly " + assembly.GetName().Name);
        Inspected.Add(assembly);
        }
    }
    private static void AssemblyLoaded(object sender, AssemblyLoadEventArgs args)
    {
        if (!enabled) return;
        try { CheckConsumer(args.LoadedAssembly); }
        catch (Exception exception)
        {
            // Most native mod loading is on this owner thread. Never call Unity
            // from an assembly-loader worker; the next owner gate drains instead.
            if (UnityData.IsInMainThread) Refuse(exception.Message);
            else { enabled = false; drainRequested = true; Status = "Deferred audio requires ordinary completion: " + exception.Message; }
        }
    }
    private static bool Admitted()
    {
        if (!UnityData.IsInMainThread) return false;
        if (draining) return true;
        if (enabled && guard?.Allows() == true) return true;
        if (PendingCount != 0 || Songs.Count != 0) Refuse("changed audio consumer or hook");
        return false;
    }
    private static void Refuse(string reason)
    {
        enabled = false;
        if (draining) return;
        draining = true;
        try
        {
            foreach (var holder in Holders.Keys.ToArray()) CompleteAll(holder);
            foreach (SongDef song in Songs.ToArray()) GetSongClip(song);
            // The feature is now refused. Failed native loads remain absent as
            // they would after eager loading; cancel the optional retry schedule.
            foreach (HolderState state in Holders.Values) state.Pending.Cancel();
            Songs.Clear(); Warmups.Clear();
        }
        finally { draining = false; Status = "Deferred audio stopped: " + reason + ". Existing work completed through native loading where available."; }
    }

    public static bool TryReload(Type type, object holderObject, bool hotReload)
    {
        // Mono may share a Harmony-patched generic body with another reference
        // type. Dispatch from the actual holder, not that body's type token.
        if (holderObject is ModContentHolder<Texture2D>) return DemandTextureRuntime.TryReload(holderObject, hotReload);
        if (!(holderObject is ModContentHolder<AudioClip>) || !Admitted() || hotReload) return false;
        var holder = (ModContentHolder<AudioClip>)holderObject;
        if (Escaped.Contains(holder)) return false;
        // A holder that has escaped may be retained and read directly later.
        // Preserve ordinary loading on all subsequent reloads of that holder.
        if (Holders.TryGetValue(holder, out HolderState prior))
        {
            CompleteAll(holder);
            if (prior.Escaped || prior.Pending.Count != 0) return false;
        }
        var mod = (ModContentPack)HolderMod.GetValue(holder);
        var files = Discover(mod);
        if (files.Count > MaxEntries || PendingCount + files.Count > MaxEntries || Holders.Count >= 4096) return false;
        var state = new HolderState(mod);
        state.Order = files.Keys.ToArray();
        Holders[holder] = state;
        foreach (var pair in files)
        {
            if (holder.contentList.ContainsKey(pair.Key)) continue;
            // Smaller clips keep native upfront background decoding. This family
            // postpones only the native stream/header/clip creation path.
            pair.Value[0].Refresh();
            if (pair.Value[0].Exists && pair.Value[0].Length > 307200)
                state.Pending.Add(pair.Key, pair.Value, MaxEntries);
            else
            {
                state.Pending.Add(pair.Key, pair.Value, MaxEntries);
                Complete(holder, state, pair.Key, refresh: false);
            }
        }
        UpdateStatus();
        return true;
    }
    private static Dictionary<string, List<FileInfo>> Discover(ModContentPack mod)
    {
        var result = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);
        string root = GenFilePaths.ContentPath<AudioClip>();
        foreach (var pair in ModContentPack.GetAllFilesForMod(mod, root, ModContentLoader<AudioClip>.IsAcceptableExtension))
        {
            string key = pair.Key.Replace('\\', '/');
            if (key.StartsWith(root)) key = key.Substring(root.Length);
            key = key.Substring(0, key.Length - Path.GetExtension(key).Length);
            if (!result.TryGetValue(key, out List<FileInfo> sources)) result.Add(key, sources = new List<FileInfo>());
            sources.Add(pair.Value);
        }
        return result;
    }
    public static ModContentHolder<T> GetHolder<T>(ModContentPack mod) where T : class
    {
        if (typeof(T) == typeof(Texture2D) && DemandTextureRuntime.Enabled)
            return (ModContentHolder<T>)(object)DemandTextureRuntime.GetHolder(mod);
        if (typeof(T) == typeof(AudioClip) && (enabled || draining || drainRequested) && Admitted())
            return (ModContentHolder<T>)AudioHolder.GetValue(mod);
        return mod.GetContentHolder<T>();
    }
    public static void EnsurePath(Type type, object holderObject, string path, bool subtree)
    {
        if (holderObject is ModContentHolder<Texture2D>) { DemandTextureRuntime.EnsurePath(holderObject, path, subtree); return; }
        if (!(holderObject is ModContentHolder<AudioClip>)) return;
        if (!UnityData.IsInMainThread)
        {
            if (enabled || drainRequested) throw new InvalidOperationException("Deferred audio access requires the native main-thread resource contract.");
            return;
        }
        Admitted();
        var holder = (ModContentHolder<AudioClip>)holderObject;
        if (!Holders.TryGetValue(holder, out HolderState state)) return;
        if (!subtree) { Complete(holder, state, path); return; }
        string prefix = string.IsNullOrEmpty(path) || path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
        foreach (string key in state.Pending.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal)) Complete(holder, state, key);
    }
    public static void ExposeHolder(Type type, object holderObject)
    {
        if (holderObject is ModContentHolder<Texture2D>) { DemandTextureRuntime.ExposeHolder(holderObject); return; }
        if (!(holderObject is ModContentHolder<AudioClip>)) return;
        if (!UnityData.IsInMainThread)
        {
            if (enabled || drainRequested) throw new InvalidOperationException("Deferred audio holders must complete on the main thread before exposure.");
            return;
        }
        if (!installed && !enabled) { earlyExposure = true; return; }
        Admitted();
        var holder = (ModContentHolder<AudioClip>)holderObject;
        Escaped.Add(holder);
        if (Holders.TryGetValue(holder, out HolderState state))
        {
            bool firstExposure = !state.Escaped;
            state.Escaped = true;
            exposures++;
            CompleteAll(holder);
            // The first public dictionary exposure also restores native source
            // insertion order while retaining the dictionary and object identities.
            if (firstExposure)
            {
                var values = new Dictionary<string, AudioClip>(holder.contentList, StringComparer.Ordinal);
                holder.contentList.Clear();
                foreach (string key in state.Order)
                    if (values.TryGetValue(key, out AudioClip value)) holder.contentList.Add(key, value);
                foreach (var value in values)
                    if (!holder.contentList.ContainsKey(value.Key)) holder.contentList.Add(value.Key, value.Value);
            }
        }
    }
    public static void ClearHolder(Type type, object holderObject)
    {
        if (holderObject is ModContentHolder<Texture2D>) { DemandTextureRuntime.ClearHolder(holderObject); return; }
        if (!(holderObject is ModContentHolder<AudioClip>)) return;
        var holder = (ModContentHolder<AudioClip>)holderObject;
        if (Holders.TryGetValue(holder, out HolderState state))
        {
            Songs.RemoveWhere(song => state.Pending.Contains(song.clipPath));
            state.Pending.Cancel(); Holders.Remove(holder);
        }
        // Native ClearDestroy owns destruction/disposal of published clips.
    }
    public static bool HasPending(object holderObject) => holderObject is ModContentHolder<AudioClip> holder
        && (Holders.TryGetValue(holder, out HolderState state) && state.Pending.Count != 0
            || DemandTextureRuntime.HasPendingForAudioHolder(holderObject));
    public static bool ResolveSong(SongDef song)
    {
        if (!Admitted() || song == null || song.clip != null) return false;
        if (!Holders.Values.Any(s => s.Pending.Contains(song.clipPath))) return false;
        if (Songs.Count >= MaxEntries || Warmups.Count >= MaxEntries) return false;
        if (Songs.Add(song)) Warmups.Enqueue(song);
        return true;
    }
    public static AudioClip? GetSongClip(SongDef song)
    {
        // This module is excluded from consumer rewriting: these field reads are
        // deliberately real reads, preserving external assignments and identity.
        if (song.clip != null) { Songs.Remove(song); return song.clip; }
        if (!Songs.Contains(song)) return song.clip;
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("Deferred song clips require the native main-thread resource contract.");
        Admitted();
        if (song.clip != null) return song.clip;
        AudioClip clip = FindClip(song.clipPath);
        if (clip != null) { song.clip = clip; Songs.Remove(song); }
        return clip;
    }
    private static void CompleteAll(ModContentHolder<AudioClip> holder)
    {
        if (!Holders.TryGetValue(holder, out HolderState state)) return;
        foreach (string key in state.Pending.Keys) Complete(holder, state, key);
    }
    private static void Complete(ModContentHolder<AudioClip> holder, HolderState state, string key, bool refresh = true)
    {
        if (!state.Pending.Contains(key)) return;
        try
        {
            bool success = state.Pending.Complete(key, original =>
            {
                if (holder.contentList.ContainsKey(key)) return new LoadedContentItem<AudioClip>(null, holder.contentList[key]);
                // Resolve the current provider's native folder precedence again;
                // pending metadata is never authority for stale source bytes.
                List<FileInfo> sources = original;
                if (refresh && !Discover(state.Mod).TryGetValue(key, out sources)) return null;
                foreach (FileInfo source in sources)
                {
                    LoadedContentItem<AudioClip>? item = refresh ? LoadReadySource(ToVirtualFile(source)) : LoadSource(ToVirtualFile(source));
                    if (item != null) return item;
                }
                return null;
            }, (path, item) => Publish(holder, path, item), Discard);
            if (success) completed++; else failures++;
        }
        catch (Exception exception) { failures++; Log.Warning("[Wake-Up] Deferred audio remains retryable: " + exception.GetType().Name); }
        UpdateStatus();
    }
    private static bool Publish(ModContentHolder<AudioClip> holder, string key, LoadedContentItem<AudioClip> item)
    {
        if (ReferenceEquals(item.contentItem, null)) return false;
        if (holder.contentList.TryGetValue(key, out AudioClip existing))
        {
            if (!ReferenceEquals(existing, item.contentItem)) Discard(item);
            return true;
        }
        // Prepare trie membership before exposing the object; roll back a failed
        // disposable-list append/dictionary insertion so the pending item survives.
        TrieAdd.Invoke(Trie.GetValue(holder), new object[] { key });
        bool streamAdded = false;
        try
        {
            if (item.extraDisposable != null) { holder.extraDisposables.Add(item.extraDisposable); streamAdded = true; }
            holder.contentList.Add(key, item.contentItem);
            return true;
        }
        catch
        {
            if (streamAdded) holder.extraDisposables.Remove(item.extraDisposable);
            Trie.FieldType.GetMethod("Remove", new[] { typeof(string) })?.Invoke(Trie.GetValue(holder), new object[] { key });
            throw;
        }
    }
    private static void Discard(LoadedContentItem<AudioClip> item)
    {
        item.extraDisposable?.Dispose();
        // internalFile == null denotes a borrowed, already-published object.
        if (item.internalFile != null && item.contentItem != null) DestroyClip(item.contentItem);
    }
    private static void Idle()
    {
        if (drainRequested && UnityData.IsInMainThread) { drainRequested = false; Refuse("new runtime consumer requires ordinary completion"); }
        if (!enabled || !PlayDataLoader.Loaded || Current.ProgramState != ProgramState.Entry
            || LongEventHandler.AnyEventNowOrWaiting || !Admitted()) return;
        WarmOne();
    }
    internal static void WarmOne()
    {
        // Only song requests enter the idle warm-up schedule. No first-render
        // bulk drain; at most one native streamed item is completed per update.
        for (int checkedEntries = 0; checkedEntries < 16 && Warmups.Count != 0; checkedEntries++)
        {
            SongDef song = Warmups.Dequeue();
            if (!Songs.Contains(song)) continue;
            GetSongClip(song);
            break;
        }
    }
    private static void UpdateStatus()
    {
        if (enabled) Status = $"Deferred audio active: {PendingCount} pending, {completed} completed, {failures} failed attempts, {exposures} full-holder requests.";
    }
    private static void Dispose()
    {
        foreach (HolderState state in Holders.Values) state.Pending.Cancel();
        Holders.Clear(); Songs.Clear(); Warmups.Clear(); Escaped.Clear();
        enabled = false;
        if (installed) AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded;
        Status = "Deferred audio released; native holders own published clips.";
    }
}
