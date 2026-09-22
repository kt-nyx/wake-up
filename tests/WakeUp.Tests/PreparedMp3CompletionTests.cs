// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using RuntimeAudioClipLoader;
using UnityEngine;

namespace WakeUp.Tests;

// Managed clip identities and native stored states stand in for the engine
// result. A separate isolated GOG case exercises the actual late getter hook;
// its Unity internal calls cannot be JIT-compiled in this ordinary test host.
[TestFixture, NonParallelizable]
public sealed class PreparedMp3CompletionTests
{
    private static readonly Type Runtime = typeof(PreparedAudioRuntime);
    private static readonly Type Frame = Runtime.GetNestedType("LoadFrame", BindingFlags.NonPublic)!;
    private static readonly MethodInfo Before = Runtime.GetMethod("BeforeLoad", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo After = Runtime.GetMethod("AfterLoad", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo States = typeof(Manager).GetField("audioLoadState", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Context = typeof(PreparedMp3IndexRuntime).GetField("context", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Enabled = Runtime.GetField("enabled", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo LoadFrame = Runtime.GetField("loadFrame", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly InvalidOperationException GetterError = new("late-getter-sentinel");
    private PreparedAudioCache cache = null!;
    private string root = "";
    private object? oldStates, oldEnabled, oldLoadFrame;
    private Dictionary<string, object> registry = null!;
    private Dictionary<AudioClip, AudioDataLoadState> states = null!;
    private readonly IdentityComparer comparer = new();

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "wake-up-mp3-completion-" + Guid.NewGuid().ToString("N"));
        CacheLaunchPolicy? policy = null;
        cache = new PreparedAudioCache(root, CacheLaunchPolicy.Latch(ref policy, "normal", () => { }));
        registry = new Dictionary<string, object> { ["audioRegistryGate"] = new object(), ["audioNativeOnly"] = false,
            ["audioAdmissionClosed"] = false, ["audioEpoch"] = 0L };
        PreparedMp3IndexRuntime.Initialize(cache, registry);
        PreparedAudioNative.Initialize();
        oldStates = States.GetValue(null); oldEnabled = Enabled.GetValue(null); oldLoadFrame = LoadFrame.GetValue(null);
        comparer.Throw = false;
        states = new Dictionary<AudioClip, AudioDataLoadState>(comparer);
        States.SetValue(null, states); Enabled.SetValue(null, true);

    }

    [TearDown]
    public void TearDown()
    {
        States.SetValue(null, oldStates); Enabled.SetValue(null, oldEnabled); LoadFrame.SetValue(null, oldLoadFrame);
        Context.SetValue(null, null); PreparedMp3IndexRuntime.Close(); cache?.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [TestCase(AudioDataLoadState.Loaded, true)]
    [TestCase(AudioDataLoadState.Loading, true)]
    [TestCase(AudioDataLoadState.Failed, false)]
    [TestCase(AudioDataLoadState.Unloaded, false)]
    public void CompletionUsesStoredIdentityWithoutNativeGetterOrComparer(AudioDataLoadState state, bool publish)
    {
        var clip = NewClip(); states.Add(clip, state); comparer.Throw = true;
        using var stream = new FileStream(Path.Combine(root, "source.mp3"), FileMode.Create, FileAccess.ReadWrite);
        object frame = AdmittedFrame(stream, new string('A', 64));
        Assert.That(Finish(clip, null, frame), Is.Null);
        var receipt = (Dictionary<string, object>)PreparedAudioRuntime.Mp3SnapshotForClip(clip)!["index"];
        Assert.That(receipt["published"], Is.EqualTo(publish));
        Assert.That(receipt["loadSucceeded"], Is.EqualTo(publish));
        Assert.That(cache.ReadIndex(new string('A', 64)) != null, Is.EqualTo(publish));
        Assert.That(Context.GetValue(null), Is.Null);
    }

    [Test]
    public void OptionalInspectionFailureIsIndeterminateAndCanBeRetried()
    {
        var clip = NewClip(); states.Add(clip, AudioDataLoadState.Loaded);
        comparer.Throw = true;
        States.SetValue(null, null);
        Assert.That(PreparedAudioNative.InspectLoadState(clip), Is.EqualTo(PreparedAudioNative.LoadStateInspection.Indeterminate));
        States.SetValue(null, states);
        Assert.That(PreparedAudioNative.InspectLoadState(clip), Is.EqualTo(PreparedAudioNative.LoadStateInspection.Active));
        Assert.That(PreparedAudioNative.InspectLoadState(NewClip()), Is.EqualTo(PreparedAudioNative.LoadStateInspection.Indeterminate));
        comparer.Throw = false; states[clip] = AudioDataLoadState.Failed; comparer.Throw = true;
        Assert.That(PreparedAudioNative.InspectLoadState(clip), Is.EqualTo(PreparedAudioNative.LoadStateInspection.Failed));
        Assert.That(PreparedAudioNative.InspectLoadState(null), Is.EqualTo(PreparedAudioNative.LoadStateInspection.Failed));
    }

    [Test]
    public void MissingStateAndBookkeepingFailureCannotPublishOrLeakNestedContext()
    {
        using var stream = new FileStream(Path.Combine(root, "source.mp3"), FileMode.Create, FileAccess.ReadWrite);
        object outer = AdmittedFrame(stream, new string('A', 64));
        object outerContext = Context.GetValue(null)!;
        object nested = AdmittedFrame(stream, new string('B', 64));
        var missing = NewClip();
        Assert.That(Finish(missing, null, nested), Is.Null);
        Assert.That(Context.GetValue(null), Is.SameAs(outerContext));
        Assert.That(cache.ReadIndex(new string('B', 64)), Is.Null);
        // Failure of optional state inspection still preserves a successful
        // native return and restores the parent before entering bookkeeping.
        nested = AdmittedFrame(stream, new string('C', 64));
        States.SetValue(null, null);
        Assert.That(Finish(NewClip(), null, nested), Is.Null);
        Assert.That(Context.GetValue(null), Is.SameAs(outerContext));
        var originalError = new IOException("original-native-error");
        Assert.That(Finish(NewClip(), originalError, outer), Is.SameAs(originalError));
        Assert.That(Context.GetValue(null), Is.Null);
        Assert.That(cache.ReadIndex(new string('A', 64)), Is.Null);
        Assert.That(cache.ReadIndex(new string('C', 64)), Is.Null);
    }

    [Test]
    public void ClosedAdmissionMasksNestedContextWithoutHashOrCompletionWork()
    {
        using var stream = new FileStream(Path.Combine(root, "source.mp3"), FileMode.Create, FileAccess.ReadWrite);
        object outer = AdmittedFrame(stream, new string('A', 64));
        object outerContext = Context.GetValue(null)!;
        registry["audioNativeOnly"] = true; registry["audioAdmissionClosed"] = true; registry["audioEpoch"] = 1L;
        long beforeBytes = (long)Runtime.GetField("sourceBytes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] arguments = { stream, AudioFormat.mp3, false, false, false, null };
        Before.Invoke(null, arguments);
        object nested = arguments[5]!;
        Assert.That(Context.GetValue(null), Is.Null);
        Assert.That(Frame.GetField("Mp3Admitted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(nested), Is.False);
        States.SetValue(null, null); // Must not even attempt completion inspection.
        int failures = (int)Runtime.GetField("mp3CompletionFailures", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var clip = NewClip();
        Assert.That(Finish(clip, null, nested), Is.Null);
        Assert.That(Context.GetValue(null), Is.SameAs(outerContext));
        Assert.That(PreparedAudioRuntime.Mp3SnapshotForClip(clip), Is.Null);
        Assert.That(Finish(clip, GetterError, outer), Is.SameAs(GetterError));
        Assert.That(Context.GetValue(null), Is.Null);
        Assert.That(Runtime.GetField("sourceBytes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null), Is.EqualTo(beforeBytes));
        Assert.That(Runtime.GetField("mp3CompletionFailures", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null), Is.EqualTo(failures));
    }

    private object AdmittedFrame(FileStream source, string key)
    {
        object frame = Activator.CreateInstance(Frame, true)!;
        void Set(string name, object? value) => Frame.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(frame, value);
        Set("PreviousMp3Context", PreparedMp3IndexRuntime.PushContext(source, key));
        Set("Mp3Admitted", true); Set("Format", AudioFormat.mp3); Set("Source", source); Set("Key", key);
        var context = (PreparedMp3IndexRuntime.Context)Context.GetValue(null)!;
        context.Captured = new PreparedMp3IndexData { SourceLength = 8, DataLength = 8, TotalSamples = 1152,
            TerminalPosition = 8, Frequency = 48000, Channels = 1 };
        context.Captured.Rows.Add(new PreparedMp3IndexData.Row { ByteCount = 8, SampleCount = 1152 });
        return frame;
    }
    private static Exception? Finish(AudioClip clip, Exception? error, object frame)
        => (Exception?)After.Invoke(null, new object?[] { clip, error, frame });
    private static AudioClip NewClip() => (AudioClip)FormatterServices.GetUninitializedObject(typeof(AudioClip));
    private sealed class IdentityComparer : IEqualityComparer<AudioClip>
    {
        internal bool Throw;
        public bool Equals(AudioClip? a, AudioClip? b) => Throw ? throw new InvalidOperationException("comparer callback") : ReferenceEquals(a, b);
        public int GetHashCode(AudioClip value) => Throw ? throw new InvalidOperationException("comparer callback") : RuntimeHelpers.GetHashCode(value);
    }
}
