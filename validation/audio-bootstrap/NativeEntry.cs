// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Doorstop;

// Fixture-only managed half of the early native Mono adapter. Registration of
// the profiler itself happens in Unity's native startup, never from this class.
internal static class NativeEntry
{
    private const string Library = "mono-profiler-wakeupentry";
    private static AudioRegistry? registry;
    private static GCHandle bridgeRoot;
    private static Exception? injectedFailure;
    private static Exception? injectedBridgeFailure;
    private static Action<IntPtr>? entryObserver;
    private static int controlThread;
    internal static bool Active { get; private set; }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_entry_bind(IntPtr typeHandle, [MarshalAs(UnmanagedType.LPWStr)] string receiptPath);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr wakeup_entry_assembly(IntPtr assemblyHandle);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong wakeup_entry_stat(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_entry_stop();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr wakeup_entry_module_path();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void wakeup_entry_process_exit();

    internal static void Bind(AudioRegistry owner, string receiptPath)
    {
        registry = owner;
        // Keep callback metadata/root alive for the lifetime of this process.
        bridgeRoot = GCHandle.Alloc(typeof(NativeEntry));
        if (wakeup_entry_bind(GCHandle.ToIntPtr(bridgeRoot), Path.Combine(Path.GetDirectoryName(receiptPath)!, "c10-native-entry-native.json")) != 1)
            throw new InvalidOperationException("Native method-entry adapter was not initialized before managed execution.");
        string actualModule = Marshal.PtrToStringUni(wakeup_entry_module_path()) ?? "";
        string expectedModule = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(NativeEntry).Assembly.Location)!, "..", "mono-profiler-wakeupentry.dll"));
        if (!string.Equals(actualModule, expectedModule, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native entry companion loaded from an unexpected path.");
        owner.State["audioNativeEntryModulePath"] = actualModule;
        Active = true;
        owner.State["audioNativeManagedBoundDomainId"] = AppDomain.CurrentDomain.Id;
        owner.State["audioNativeManagedBoundDomainName"] = AppDomain.CurrentDomain.FriendlyName;
        owner.State["audioNativeManagedBoundIsDefault"] = AppDomain.CurrentDomain.IsDefaultAppDomain();
        // The pinned host dispatches ProcessExit before aborting managed threads
        // and before profiler shutdown_begin/metadata cleanup. Register before
        // loading game/mod code, independently of optional audio initialization.
        AppDomain.CurrentDomain.ProcessExit += BeforeProcessExit;
        owner.State["audioNativeEntryReady"] = true;
        // The isolated GOG error/order and independent held-stop captures at
        // 184ffd50 qualify this bounded contract. Admission is selected only by
        // explicit isolated functional or measurement selection, never normal use.
        string[] arguments = Environment.GetCommandLineArgs();
        owner.State["audioNativeEntryQualified"] = !Array.Exists(arguments,
            argument => argument == "--fixture-c10-audio-measurement=off") && Array.Exists(arguments,
            argument => argument.StartsWith("--fixture-c10-prepared-audio=", StringComparison.Ordinal)
                || argument == "--fixture-c10-audio-measurement=cold"
                || argument == "--fixture-c10-audio-measurement=warm");
        owner.State["audioNativeEntryQualification"] = "c10-settlement-stop-01+c10-settlement-boundary-01@184ffd50";
        owner.State["audioNativeEntryStop"] = (Action)Stop;
        owner.State["audioNativeEntryStats"] = (Func<ulong[]>)Stats;
        owner.State["audioNativeEntryInjectFailure"] = (Action<Exception>)(error => Interlocked.Exchange(ref injectedFailure, error));
        owner.State["audioNativeEntryInjectBridgeFailure"] = (Action<Exception>)(error => Interlocked.Exchange(ref injectedBridgeFailure, error));
        owner.State["audioNativeEntryDiagnosticFailures"] = 0L;
        owner.State["audioNativeEntryObserve"] = (Action<Action<IntPtr>?>)(observer => Interlocked.Exchange(ref entryObserver, observer));
        owner.State["audioNativeEntryControlThread"] = (Action<int>)(thread => Volatile.Write(ref controlThread, thread));
    }

    internal static IntPtr AssemblyIdentity(Assembly assembly)
    {
        GCHandle root = GCHandle.Alloc(assembly);
        try
        {
            IntPtr value = wakeup_entry_assembly(GCHandle.ToIntPtr(root));
            if (value == IntPtr.Zero) throw new InvalidOperationException("Native assembly identity is absent.");
            return value;
        }
        finally { root.Free(); }
    }

    // Diagnostics cannot prevent mandatory settlement. Owned reader callbacks
    // retain decoder errors for their ordinary consumers and return terminal state.
    private static int OnMethodEntry(IntPtr assembly)
    {
        bool forceDrain = false;
        try
        {
            Volatile.Read(ref entryObserver)?.Invoke(assembly);
            Exception? diagnostic = Interlocked.Exchange(ref injectedFailure, null);
            if (diagnostic != null) throw diagnostic;
        }
        catch (Exception)
        {
            forceDrain = true;
            lock (registry!.State["audioRegistryGate"])
                registry.State["audioNativeEntryDiagnosticFailures"] = (long)registry.State["audioNativeEntryDiagnosticFailures"] + 1;
        }
        // Explicit unguarded fixture control, confined to the observer's selected
        // thread. Never enabled by the product or inferred from sticky fallback.
        if (!forceDrain && Volatile.Read(ref controlThread) == Thread.CurrentThread.ManagedThreadId) return 1;
        // Separately challenge native exception-out recovery in this fixture.
        Exception? error = Interlocked.Exchange(ref injectedBridgeFailure, null);
        if (error != null) throw error;
        registry!.BeforeNativeMutation(assembly, forceDrain);
        return 1;
    }

    // Recovery and early domain-unload settlement run no observer/diagnostic
    // hooks and can authorize continuation only after the registry is empty.
    private static int DrainOnly() => registry!.DrainNativeEntry() ? 1 : 0;

    private static ulong[] Stats()
    {
        var values = new ulong[33];
        for (int i = 0; i < values.Length; i++) values[i] = wakeup_entry_stat(i);
        return values;
    }

    private static void Stop()
    {
        if (!Active) return;
        // Finish readers first; release the registry gate before waiting for any
        // entry already counted by the native adapter to finish its bridge.
        registry!.CloseNativeEntry();
        if (wakeup_entry_stop() != 1) throw new InvalidOperationException("Native entry stop did not drain its active calls.");
        registry.State["audioNativeEntryStopped"] = true;
    }

    private static void BeforeProcessExit(object sender, EventArgs args)
    {
        Stop();
        wakeup_entry_process_exit();
    }
}
