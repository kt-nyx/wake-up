// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using RimWorld.IO;
using UnityEngine;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class DeferredAudioRuntimeTests
{
    private string root = null!;
    private ModContentPack mod = null!;
    private ModContentHolder<AudioClip> holder = null!;
    private object? oldThread;
    private Func<VirtualFile, LoadedContentItem<AudioClip>> oldLoad = null!;
    private Action<AudioClip> oldDestroy = null!;
    private Func<string, AudioClip> oldFind = null!;
    private Func<VirtualFile, LoadedContentItem<AudioClip>?> oldReady = null!;
    private int loads, disposed;
    private static void Set(string name, object value) => AccessTools.Field(typeof(DeferredAudioRuntime), name).SetValue(null, value);
    private static object Get(string name) => AccessTools.Field(typeof(DeferredAudioRuntime), name).GetValue(null);
    private static AudioClip Clip()
    {
        var clip = (AudioClip)FormatterServices.GetUninitializedObject(typeof(AudioClip));
        AccessTools.Field(typeof(UnityEngine.Object), "m_CachedPtr").SetValue(clip, new IntPtr(1));
        return clip;
    }
    private sealed class StreamOwner : IDisposable
    {
        private readonly Action dispose;
        internal StreamOwner(Action dispose) { this.dispose = dispose; }
        public void Dispose() => dispose();
    }
    [SetUp]
    public void SetUp()
    {
        var initializing = AccessTools.Field(typeof(UnityData).Assembly.GetType("Verse.UnityDataInitializer"), "initializing");
        object previous = initializing.GetValue(null); initializing.SetValue(null, true);
        oldThread = AccessTools.Field(typeof(UnityData), "mainThreadId").GetValue(null);
        initializing.SetValue(null, previous);
        AccessTools.Field(typeof(UnityData), "mainThreadId").SetValue(null, Thread.CurrentThread.ManagedThreadId);
        AccessTools.Method(typeof(DeferredAudioRuntime), "InitializeAccessors").Invoke(null, null);
        root = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "r3-deferred-20260910", "tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Sounds"));
        mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        mod.foldersToLoadDescendingOrder = new List<string> { Path.GetFullPath(root) };
        holder = new ModContentHolder<AudioClip>(mod);
        AccessTools.Field(typeof(ModContentPack), "audioClips").SetValue(mod, holder);
        oldLoad = DeferredAudioRuntime.LoadSource; oldDestroy = DeferredAudioRuntime.DestroyClip;
        oldFind = DeferredAudioRuntime.FindClip;
        oldReady = DeferredAudioRuntime.LoadReadySource;
        DeferredAudioRuntime.LoadReadySource = file => DeferredAudioRuntime.LoadSource(file);
        DeferredAudioRuntime.FindClip = path => { DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, path, false); return holder.Get(path); };
        DeferredAudioRuntime.LoadSource = file => { loads++; return new LoadedContentItem<AudioClip>(file, Clip(), new StreamOwner(() => disposed++)); };
        DeferredAudioRuntime.DestroyClip = _ => { };
        Set("draining", true); Set("enabled", true); Set("installed", true); Set("earlyExposure", false);
        loads = disposed = 0;
    }
    [TearDown]
    public void TearDown()
    {
        foreach (IDisposable owner in holder.extraDisposables) owner.Dispose();
        DeferredAudioRuntime.ClearHolder(typeof(AudioClip), holder);
        AccessTools.Method(typeof(DeferredAudioRuntime), "Dispose").Invoke(null, null);
        Set("draining", false); Set("installed", false); Set("earlyExposure", false); Set("drainRequested", false);
        DeferredAudioRuntime.LoadSource = oldLoad; DeferredAudioRuntime.DestroyClip = oldDestroy;
        DeferredAudioRuntime.FindClip = oldFind;
        DeferredAudioRuntime.LoadReadySource = oldReady;
        AccessTools.Field(typeof(UnityData), "mainThreadId").SetValue(null, oldThread);
        // Evidence files stay under ignored artifacts; no fixture is used.
    }
    private void Source(string name, int size = 307201)
    {
        string path = Path.Combine(root, "Sounds", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path); file.SetLength(size);
    }
    [Test]
    public void DelaysLargeNativeStreamsButKeepsSmallClipsEagerAndSameHolderIdentity()
    {
        Source("Music/Song.ogg"); Source("Effect.wav", 10);
        Assert.That(DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false), Is.True);
        Assert.That(loads, Is.EqualTo(1)); Assert.That(holder.contentList.ContainsKey("Music/Song"), Is.False);
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "Music/Song", false);
        AudioClip first = holder.contentList["Music/Song"];
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "Music/Song", false);
        Assert.That(holder.Get("Music/Song"), Is.SameAs(first));
        Assert.That(loads, Is.EqualTo(2)); Assert.That(holder.extraDisposables.Count, Is.EqualTo(2));
        Assert.That(holder.GetAllUnderPath("Music").Single(), Is.SameAs(first));
    }
    [Test]
    public void SubtreeDemandDoesNotDrainOtherFoldersAndFullExposureCompletesBeforeReturn()
    {
        Source("One/A.ogg"); Source("Two/B.ogg");
        DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "One", true);
        Assert.That(holder.contentList.Keys, Is.EquivalentTo(new[] { "One/A" }));
        DeferredAudioRuntime.ExposeHolder(typeof(AudioClip), holder);
        Assert.That(holder.contentList.Count, Is.EqualTo(2));
        Assert.That(DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false), Is.False);
    }
    [Test]
    public void HolderExposedBeforeRegistrationNeverBecomesPartial()
    {
        Source("A.ogg");
        DeferredAudioRuntime.ExposeHolder(typeof(AudioClip), holder);
        Assert.That(DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false), Is.False);
        Assert.That(loads, Is.Zero);
    }
    [Test]
    public void FailedSiblingFallsThroughAndMissingInputRemainsRetryable()
    {
        Source("A.ogg"); Source("A.wav");
        DeferredAudioRuntime.LoadSource = file => { loads++; return file.Name.EndsWith(".ogg", StringComparison.Ordinal) ? null! : new LoadedContentItem<AudioClip>(file, Clip()); };
        DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "A", false);
        Assert.That(holder.contentList.ContainsKey("A"), Is.True); Assert.That(loads, Is.EqualTo(2));
        Source("Missing.ogg");
        DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "Missing", false);
        Assert.That(DeferredAudioRuntime.HasPending(holder), Is.True);
        Source("Missing.wav");
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "Missing", false);
        Assert.That(holder.contentList.ContainsKey("Missing"), Is.True);
    }
    [Test]
    public void ChangedProviderFolderWinsAtCompletionAndDirectReplacementIsRetained()
    {
        Source("A.ogg");
        DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        string newRoot = Path.Combine(root, "new"); Directory.CreateDirectory(Path.Combine(newRoot, "Sounds"));
        File.WriteAllText(Path.Combine(newRoot, "Sounds", "A.ogg"), "new bytes");
        mod.foldersToLoadDescendingOrder.Insert(0, Path.GetFullPath(newRoot));
        string loaded = "";
        DeferredAudioRuntime.LoadSource = file => { loaded = file.FullPath; return new LoadedContentItem<AudioClip>(file, Clip()); };
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "A", false);
        Assert.That(loaded, Does.Contain("new"));
        AudioClip replacement = Clip(); holder.contentList["A"] = replacement;
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "A", false);
        Assert.That(holder.contentList["A"], Is.SameAs(replacement));
    }
    [Test]
    public void CancellationOwnsNoUncreatedStreamAndDoesNotPublishLater()
    {
        Source("A.ogg"); DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        Assert.That(DeferredAudioRuntime.HasPending(holder), Is.True);
        DeferredAudioRuntime.ClearHolder(typeof(AudioClip), holder);
        DeferredAudioRuntime.EnsurePath(typeof(AudioClip), holder, "A", false);
        Assert.That((loads, disposed, holder.contentList.Count), Is.EqualTo((0, 0, 0)));
    }
    [Test]
    public void DisabledModeRetainsNoHolderAndUsesOrdinaryGetter()
    {
        Set("draining", false); Set("enabled", false); Set("installed", false);
        DeferredAudioRuntime.ExposeHolder(typeof(AudioClip), holder);
        Assert.That(((IEnumerable)Get("Escaped")).Cast<object>(), Is.Empty);
        Assert.That(DeferredAudioRuntime.GetHolder<AudioClip>(mod), Is.SameAs(holder));
        Assert.That(DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false), Is.False);
    }
    [Test]
    public void SelectionIsOffByDefaultAndMasterDisableWins()
    {
        var settings = new WakeUpSettings();
        Assert.That(settings.DeferredAudio, Is.False);
        settings.DeferredAudio = true;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, root), Does.Contain("--wake-up-deferred-audio=on"));
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, root), Is.Empty);
    }
    [Test]
    public void SongConsumerCompletesOnFirstReadAndRetainsTheNativeObject()
    {
        Source("Music/A.ogg"); DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        var song = new SongDef { clipPath = "Music/A" };
        Assert.That(DeferredAudioRuntime.ResolveSong(song), Is.True);
        Assert.That(loads, Is.Zero); Assert.That(ReferenceEquals(song.clip, null), Is.True);
        AudioClip first = DeferredAudioRuntime.GetSongClip(song)!;
        Assert.That(first, Is.SameAs(holder.contentList["Music/A"]));
        Assert.That(DeferredAudioRuntime.GetSongClip(song), Is.SameAs(first));
        AudioClip assigned = Clip(); song.clip = assigned;
        Assert.That(DeferredAudioRuntime.GetSongClip(song), Is.SameAs(assigned));
        Assert.That(loads, Is.EqualTo(1));
    }
    [Test]
    public void FailedSongRetainsRetryAndCancellationDoesNotResurrectIt()
    {
        Source("Music/A.ogg"); DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        var song = new SongDef { clipPath = "Music/A" };
        DeferredAudioRuntime.ResolveSong(song);
        DeferredAudioRuntime.LoadSource = _ => null!;
        Assert.That(ReferenceEquals(DeferredAudioRuntime.GetSongClip(song), null), Is.True);
        Assert.That(DeferredAudioRuntime.HasPending(holder), Is.True);
        DeferredAudioRuntime.ClearHolder(typeof(AudioClip), holder);
        DeferredAudioRuntime.LoadSource = _ => throw new Exception("cancelled work ran");
        Assert.That(ReferenceEquals(DeferredAudioRuntime.GetSongClip(song), null), Is.True);
    }
    [Test]
    public void IdleWarmupAttemptsOneSongAndFailureDoesNotStarveTheNext()
    {
        Source("A.ogg"); Source("B.ogg"); DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        var first = new SongDef { clipPath = "A" }; var second = new SongDef { clipPath = "B" };
        DeferredAudioRuntime.ResolveSong(first); DeferredAudioRuntime.ResolveSong(second);
        DeferredAudioRuntime.LoadSource = file => { loads++; return file.Name == "A.ogg" ? null! : new LoadedContentItem<AudioClip>(file, Clip()); };
        DeferredAudioRuntime.WarmOne(); Assert.That(loads, Is.EqualTo(1));
        DeferredAudioRuntime.WarmOne(); Assert.That(loads, Is.EqualTo(2));
        Assert.That(ReferenceEquals(second.clip, null), Is.False);
        DeferredAudioRuntime.WarmOne(); Assert.That(loads, Is.EqualTo(2));
        Assert.That(DeferredAudioRuntime.HasPending(holder), Is.True);
    }
    [Test]
    public void WorkerLoadedUncoveredAssemblyDisablesThenRequestsAnOwnerDrain()
    {
        Source("A.ogg"); DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false);
        var song = new SongDef { clipPath = "A" }; DeferredAudioRuntime.ResolveSong(song);
        using var module = Mono.Cecil.ModuleDefinition.CreateModule("UnknownAudioConsumer" + Guid.NewGuid().ToString("N"), Mono.Cecil.ModuleKind.Dll);
        module.AssemblyReferences.Add(Mono.Cecil.AssemblyNameReference.Parse(typeof(SongDef).Assembly.FullName));
        using var bytes = new MemoryStream(); module.Write(bytes);
        Assembly assembly = Assembly.Load(bytes.ToArray());
        Set("draining", false);
        Task.Run(() => AccessTools.Method(typeof(DeferredAudioRuntime), "AssemblyLoaded").Invoke(null, new object[] { new object(), new AssemblyLoadEventArgs(assembly) })).GetAwaiter().GetResult();
        Assert.That(Get("enabled"), Is.False); Assert.That(Get("drainRequested"), Is.True);
        Assert.That(loads, Is.Zero, "A worker cannot publish native Unity clips.");
        AccessTools.Method(typeof(DeferredAudioRuntime), "Idle").Invoke(null, null);
        Assert.That(Get("drainRequested"), Is.False); Assert.That(loads, Is.EqualTo(1));
        Assert.That(ReferenceEquals(song.clip, null), Is.False);
    }
    [Test]
    public void DisabledOrRefusedSongReadsDoNotTouchOptionalAccessors()
    {
        Set("enabled", false); Set("draining", false); Set("installed", false);
        Set("AudioHolder", null!); Set("Trie", null!); Set("ToVirtualFile", null!);
        var song = new SongDef { clip = Clip() };
        Assert.That(DeferredAudioRuntime.GetSongClip(song), Is.SameAs(song.clip));
        Assert.That(DeferredAudioRuntime.GetHolder<AudioClip>(mod), Is.SameAs(holder));
        Assert.That(DeferredAudioRuntime.TryReload(typeof(AudioClip), holder, false), Is.False);
    }
}
