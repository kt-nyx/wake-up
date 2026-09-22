// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using NAudio.Wave;
using RuntimeAudioClipLoader;
using UnityEngine;

namespace WakeUp;

// Construct the original lower reader on its borrowed native source handle.
// Manager, CustomAudioFileReader and SampleChannel remain ordinary game code.
internal static class PreparedAudioNative
{
    static PreparedAudioNative() { } // Resolve only when the admitted factory is used.
    internal static readonly Type ReaderType = typeof(Manager).Assembly.GetType("NVorbis.NAudioSupport.VorbisWaveReader", true)!;
    internal static readonly ConstructorInfo Constructor = AccessTools.Constructor(ReaderType, new[] { typeof(Stream) });
    internal static readonly ConstructorInfo WaveConstructor = AccessTools.Constructor(typeof(WaveFileReader), new[] { typeof(Stream) });
    internal static readonly ConstructorInfo Mp3Constructor = AccessTools.Constructor(typeof(Mp3FileReader), new[] { typeof(Stream) });
    // Assigned once by production. Kept non-readonly so the isolated observer
    // can induce inspection failure without changing the native table; Mono
    // otherwise constant-folds this handle despite a reflection-only test write.
    private static FieldInfo LoadStates = typeof(Manager).GetField("audioLoadState", BindingFlags.Static | BindingFlags.NonPublic)!;

    // Force reflection/Harmony accessor setup before any reader is admitted.
    internal static void Initialize()
    {
        if (LoadStates == null || LoadStates.FieldType != typeof(Dictionary<AudioClip, AudioDataLoadState>))
            throw new InvalidOperationException("Native audio load-state layout changed.");
        // The pinned Manager.Load body calls this setter. Qualify its direct
        // storage contract once, before installing any runtime hooks.
        var setter = AccessTools.Method(typeof(Manager), "SetAudioClipLoadState", new[] { typeof(AudioClip), typeof(AudioDataLoadState) });
        var code = PatchProcessor.GetOriginalInstructions(setter).Where(c => c.opcode != OpCodes.Nop).ToArray();
        if (code.Length != 5 || code[0].opcode != OpCodes.Ldsfld || !Equals(code[0].operand, LoadStates)
            || code[1].opcode != OpCodes.Ldarg_0 || code[2].opcode != OpCodes.Ldarg_1
            || code[3].opcode != OpCodes.Callvirt || !Equals(code[3].operand,
                typeof(Dictionary<AudioClip, AudioDataLoadState>).GetProperty("Item")!.GetSetMethod())
            || code[4].opcode != OpCodes.Ret)
            throw new InvalidOperationException("Native audio load-state setter changed.");
    }

    internal enum LoadStateInspection { Indeterminate, Active, Failed }

    internal static LoadStateInspection InspectLoadState(AudioClip? clip)
    {
        if (ReferenceEquals(clip, null)) return LoadStateInspection.Failed;
        // Do not call the public getter, AudioClip properties, Unity equality,
        // or the dictionary comparer. These can execute additional game hooks.
        try
        {
            var states = (Dictionary<AudioClip, AudioDataLoadState>)LoadStates.GetValue(null)!;
            foreach (var entry in states)
                if (ReferenceEquals(entry.Key, clip))
                    return entry.Value == AudioDataLoadState.Loading || entry.Value == AudioDataLoadState.Loaded
                        ? LoadStateInspection.Active : entry.Value == AudioDataLoadState.Failed
                            ? LoadStateInspection.Failed : LoadStateInspection.Indeterminate;
        }
        // Native writers do not share our lifetime lock. Optional enumeration
        // failure cannot replace their result or establish that a clip is dead.
        catch { }
        return LoadStateInspection.Indeterminate;
    }

    // Native fallback must not inspect or seek an arbitrary caller's stream.
    internal static WaveStream Create(Stream source) => Create(source, AudioFormat.ogg);

    internal static WaveStream Create(Stream source, AudioFormat format)
    {
        ConstructorInfo factory = format == AudioFormat.mp3 ? Mp3Constructor : format == AudioFormat.wav ? WaveConstructor : Constructor;
        try { return (WaveStream)factory.Invoke(new object[] { source }); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }

    internal static WaveStream Create(Stream source, long initialPosition)
        => Create(source, initialPosition, AudioFormat.ogg);

    internal static WaveStream Create(Stream source, long initialPosition, AudioFormat format)
    {
        source.Position = initialPosition;
        return Create(source, format);
    }
}
