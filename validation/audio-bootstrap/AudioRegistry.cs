// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Doorstop;

// Shared by the original and rewritten Harmony guards. This object contains no
// game types; its dictionary is independent of the diagnostic state's lock.
internal sealed class AudioRegistry
{
    private readonly object gate = new object();
    private readonly Dictionary<object, Func<int>> pending = new Dictionary<object, Func<int>>(ReferenceComparer.Instance);
    private MethodBase? ownTarget;
    private int ownThread;
    private bool guardInstallationComplete;
    internal Dictionary<string, object> State { get; }

    internal AudioRegistry()
    {
        State = new Dictionary<string, object>
        {
            ["audioRegistryGate"] = gate,
            ["audioPending"] = pending,
            ["audioNativeOnly"] = false,
            ["audioEpoch"] = 0L,
            ["audioReason"] = "",
            ["audioMutationEntries"] = 0,
            ["audioAdmissionClosed"] = true,
            ["audioOriginalHarmony"] = null!,
            ["audioReplacementHarmony"] = null!,
            ["audioUnexpectedHarmonyCopies"] = 0,
            ["audioNativeEntryReady"] = false,
            ["audioNativeEntryQualified"] = false,
            ["audioNativeEntryStopped"] = false,
            ["audioNativeOriginalHarmony"] = IntPtr.Zero,
            ["audioNativeReplacementHarmony"] = IntPtr.Zero,
            ["audioNativeMutationEntries"] = 0,
            ["audioArmOwnPatch"] = (Action<MethodBase>)ArmOwnPatch,
            ["audioDisarmOwnPatch"] = (Action)DisarmOwnPatch
        };
    }

    internal void GuardInstallationComplete()
    {
        lock (gate) guardInstallationComplete = true;
    }

    internal bool ObserveHarmonyAssembly(Assembly assembly, bool originalLoad, bool replacementLoad)
    {
        if (assembly.ReflectionOnly) return true;
        lock (gate)
        {
            if (ReferenceEquals(assembly, State["audioOriginalHarmony"])
                || ReferenceEquals(assembly, State["audioReplacementHarmony"])) return true;
            // A replacement is provisional until the runtime verifies its
            // emitted guards and compares this exact Assembly reference.
            bool closed = (bool)State["audioAdmissionClosed"] && pending.Count == 0 && !(bool)State["audioNativeOnly"];
            if (closed && originalLoad && State["audioOriginalHarmony"] == null)
            {
                State["audioOriginalHarmony"] = assembly;
                if (NativeEntry.Active) State["audioNativeOriginalHarmony"] = NativeEntry.AssemblyIdentity(assembly);
                return true;
            }
            if (closed && replacementLoad && State["audioOriginalHarmony"] != null && State["audioReplacementHarmony"] == null)
            {
                State["audioReplacementHarmony"] = assembly;
                if (NativeEntry.Active) State["audioNativeReplacementHarmony"] = NativeEntry.AssemblyIdentity(assembly);
                return true;
            }
            State["audioUnexpectedHarmonyCopies"] = unchecked((int)State["audioUnexpectedHarmonyCopies"] + 1);
            State["audioEpoch"] = unchecked((long)State["audioEpoch"] + 1L);
            CloseAndSettle("unexpected-Harmony-assembly: " + assembly.FullName);
            return false;
        }
    }

    internal void BeforeMutation(MethodBase target)
    {
        lock (gate)
        {
            if (!Relevant(target) && !(guardInstallationComplete && Infrastructure(target))) return;
            State["audioEpoch"] = unchecked((long)State["audioEpoch"] + 1L);
            State["audioMutationEntries"] = unchecked((int)State["audioMutationEntries"] + 1);
            if (ownThread == Thread.CurrentThread.ManagedThreadId && Equals(ownTarget, target))
            {
                // Consume before Harmony can invoke a factory or transpiler.
                ownTarget = null;
                ownThread = 0;
                if (pending.Count == 0 && (bool)State["audioAdmissionClosed"] && !(bool)State["audioNativeOnly"])
                    return;
            }

            CloseAndSettle(target.Module.Assembly.GetName().Name + ":" + target.DeclaringType?.FullName + "." + target.Name);
        }
    }

    internal void BeforeNativeMutation(IntPtr assembly, bool forceDrain = false)
    {
        lock (gate)
        {
            if (!forceDrain && assembly != IntPtr.Zero && (assembly == (IntPtr)State["audioNativeOriginalHarmony"]
                || assembly == (IntPtr)State["audioNativeReplacementHarmony"])) return;
            State["audioNativeMutationEntries"] = unchecked((int)State["audioNativeMutationEntries"] + 1);
            State["audioEpoch"] = unchecked((long)State["audioEpoch"] + 1L);
            // Do not fast-return on nativeOnly: the first thread can have set
            // it while still blocked completing one of these same readers.
            CloseAndSettle("unexpected-Harmony-method-entry");
        }
    }

    internal void CloseNativeEntry()
    {
        lock (gate) CloseAndSettle("native-entry-normal-shutdown");
    }

    internal bool DrainNativeEntry()
    {
        lock (gate)
        {
            CloseAndSettle("native-entry-recovery");
            return pending.Count == 0;
        }
    }

    private void CloseAndSettle(string reason)
    {
        // Caller owns only the registry gate, never Entrypoint's diagnostic gate.
        State["audioAdmissionClosed"] = true;
        State["audioNativeOnly"] = true;
        if (((string)State["audioReason"]).Length == 0) State["audioReason"] = reason;
        foreach (var entry in new List<KeyValuePair<object, Func<int>>>(pending))
        {
            // Only the owned reader can establish its terminal state. A thrown
            // callback or an incomplete/unknown result is never settlement.
            int outcome = entry.Value();
            if (outcome < 1 || outcome > 3)
                throw new InvalidOperationException("Prepared audio settlement did not return a terminal reader outcome: " + outcome);
            pending.Remove(entry.Key);
        }
    }

    private void ArmOwnPatch(MethodBase target)
    {
        if (target == null || !OwnInstallTarget(target)) throw new ArgumentException("Not an initial prepared-audio hook target.", nameof(target));
        lock (gate)
        {
            if (ownTarget != null || pending.Count != 0 || !(bool)State["audioAdmissionClosed"] || (bool)State["audioNativeOnly"])
                throw new InvalidOperationException("Prepared-audio own-install admission is not closed and empty, or native fallback is sticky.");
            ownTarget = target;
            ownThread = Thread.CurrentThread.ManagedThreadId;
        }
    }

    private void DisarmOwnPatch()
    {
        lock (gate)
        {
            if (ownTarget != null && ownThread != Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Only the installing thread can disarm its prepared-audio patch permit.");
            ownTarget = null;
            ownThread = 0;
        }
    }

    private static bool Relevant(MethodBase method)
    {
        string? assembly = method.Module.Assembly.GetName().Name;
        if (assembly == "NAudio" || assembly == "NVorbis") return true;
        for (Type? type = method.DeclaringType; type != null; type = type.DeclaringType)
        {
            string? name = DefinitionName(type);
            if (assembly == "Assembly-CSharp" &&
                (name == "RuntimeAudioClipLoader.CustomAudioFileReader" || name == "NVorbis.NAudioSupport.VorbisWaveReader"
                    || name == "RuntimeAudioClipLoader.Manager" || name == "RimWorld.IO.FilesystemFile" && method.Name == "CreateReadStream"
                    || ContentLifetime(name, method.Name))) return true;
            if (type == typeof(System.IO.FileInfo) && method.Name == "OpenRead") return true;
            if (type == typeof(System.IO.FileStream) && method.Name == "Dispose") return true;
            if (type.Namespace == "WakeUp" && (type.Name.StartsWith("PreparedAudio", StringComparison.Ordinal)
                || type.Name.StartsWith("PreparedMp3", StringComparison.Ordinal))) return true;
        }
        return false;
    }

    private static bool OwnInstallTarget(MethodBase method)
    {
        if (method.Module.Assembly.GetName().Name == "NAudio"
            && method.DeclaringType?.FullName == "NAudio.Wave.AcmMp3FrameDecompressor"
            && !method.IsStatic && !method.IsGenericMethod && method.IsPublic && method is MethodInfo frame)
        {
            var parameters = method.GetParameters();
            if ((method.Name == "Reset" || method.Name == "Dispose") && parameters.Length == 0 && frame.ReturnType == typeof(void)) return true;
            if (method.Name == "DecompressFrame" && frame.ReturnType == typeof(int) && parameters.Length == 3
                && parameters[0].ParameterType.FullName == "NAudio.Wave.Mp3Frame"
                && parameters[1].ParameterType == typeof(byte[]) && parameters[2].ParameterType == typeof(int)) return true;
        }
        if (method.Module.Assembly.GetName().Name == "NAudio"
            && method.DeclaringType?.FullName == "NAudio.Wave.Mp3FileReader"
            && method.Name == "CreateTableOfContents" && !method.IsStatic && !method.IsGenericMethod
            && method.IsPrivate && method.GetParameters().Length == 0
            && method is MethodInfo scan && scan.ReturnType == typeof(void)) return true;
        if (method.DeclaringType == typeof(System.IO.FileStream) && method.Name == "Dispose"
            && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(bool)) return true;
        if (method.Module.Assembly.GetName().Name != "Assembly-CSharp" || method.DeclaringType == null) return false;
        string? type = DefinitionName(method.DeclaringType);
        return (type == "RuntimeAudioClipLoader.Manager" && method.Name == "Load" && method.GetParameters().Length == 6)
            || (type == "RuntimeAudioClipLoader.CustomAudioFileReader" && method.Name == "CreateReaderStream")
            || (type == "RuntimeAudioClipLoader.CustomAudioFileReader" && method.Name == "Dispose"
                && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(bool))
            || (type == "RimWorld.IO.FilesystemFile" && method.Name == "CreateReadStream");
    }

    private static bool ContentLifetime(string? type, string method) =>
        (type == "Verse.ModContentLoader`1" && method == "LoadItem")
        || (type == "Verse.ModContentHolder`1" && method == "ClearDestroy");

    private static string? DefinitionName(Type type) => (type.IsGenericType ? type.GetGenericTypeDefinition() : type).FullName;

    private static bool Infrastructure(MethodBase method) =>
        (method.Module.Assembly.GetName().Name == "0Harmony" && method.DeclaringType?.FullName == "HarmonyLib.PatchFunctions"
            && (method.Name == "UpdateWrapper" || method.Name == "ReversePatch"))
        || method.Module.Assembly == typeof(AudioRegistry).Assembly;

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new ReferenceComparer();
        public new bool Equals(object? left, object? right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}
