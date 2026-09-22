// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using NAudio.Wave;

namespace FixtureMenuObserver;

// Synthetic pending work and isolated in-memory product readers qualify the
// native boundary. No product clip admission or cache is enabled here.
internal static class C10NativeEntryProbe
{
    private const int Timeout = 10000;
    private static int factoryCalls;
    private static int settled;
    private static int factoryBeforeSettlement;
    private static ManualResetEventSlim? factoryReached;
    private static Action? factoryObservation;
    private static readonly BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Start(string directory)
    {
        if (Environment.GetCommandLineArgs().Contains("--fixture-c10-native-entry-domain-probe"))
        { C10ProductShutdownProbe.StartDomain(directory); return; }
        bool stopOnly = Environment.GetCommandLineArgs().Contains("--fixture-c10-native-entry-stop-only");
        if (stopOnly) { C10ProductShutdownProbe.SelectStop(directory); return; }
        var report = new Dictionary<string, object>
        {
            ["schema"] = "c10-native-entry-functional.v1", ["functionalPassed"] = false,
            ["failure"] = "", ["syntheticPendingOnly"] = stopOnly, ["actualReaderAcceptance"] = false,
            ["probeMode"] = stopOnly ? "stop-only" : "boundary",
            ["performanceClaim"] = false, ["thirdCopyPatchCompletionClaim"] = false, ["cases"] = new List<object>(),
            ["coverage"] = new Dictionary<string, object>
            {
                ["assemblyLoadBytes"] = true, ["publicGetAssembliesDiscovery"] = !stopOnly,
                ["compiledDelegateCallsThirdUpdateWrapper"] = true, ["reflectedThirdUpdateWrapper"] = !stopOnly,
                ["publicHarmonyPatch"] = false,
                ["actualUpdateWrapperAndPatchFactory"] = true, ["reversePatch"] = false,
                ["sameFileConcurrentReuse"] = false, ["sameFileEarlyReturnClosure"] = false,
                ["limits"] = "Boundary mode qualifies entry and nonthrowing mandatory settlement, including isolated in-memory product readers without admission or cache. Stop-only mode qualifies shutdown independently. Public Harmony.Patch, complete third-copy wrapper compatibility and same-file closure are not qualified."
            }
        };
        Action<Action<IntPtr>?>? observe = null;
        Action<int>? controlThread = null;
        Action<Exception>? inject = null;
        Action<Exception>? injectBridge = null;
        try
        {
            var bootstrap = AppDomain.CurrentDomain.GetData("WakeUp.PreparedAudio.Bootstrap.v1") as Dictionary<string, object>
                ?? throw new InvalidOperationException("Bootstrap state is absent.");
            var registry = (Dictionary<string, object>)bootstrap["audioRegistry"];
            object gate = registry["audioRegistryGate"];
            var pending = (Dictionary<object, Func<int>>)registry["audioPending"];
            Require((bool)registry["audioNativeEntryReady"], "Native entry adapter is not ready.");
            var stats = (Func<ulong[]>)registry["audioNativeEntryStats"];
            observe = (Action<Action<IntPtr>?>)registry["audioNativeEntryObserve"];
            controlThread = (Action<int>)registry["audioNativeEntryControlThread"];
            inject = (Action<Exception>)registry["audioNativeEntryInjectFailure"];
            injectBridge = (Action<Exception>)registry["audioNativeEntryInjectBridgeFailure"];
            Func<long> diagnostics = () => Convert.ToInt64(registry["audioNativeEntryDiagnosticFailures"]);
            Assembly original = (Assembly)registry["audioOriginalHarmony"];
            string source = original.Location;
            Require(!string.IsNullOrEmpty(source) && File.Exists(source), "Original Harmony file is unavailable.");
            byte[] bytes = File.ReadAllBytes(source);
            using (var sha = new SHA256Managed()) report["originalHarmonySha256"] = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            report["sourcePath"] = source;
            report["nativeEntryModulePath"] = registry["audioNativeEntryModulePath"];
            report["statsBefore"] = stats();
            lock (gate) Require(pending.Count == 0, "Initial probe requires no real pending audio readers.");
            var cases = (List<object>)report["cases"];
            // The bypass is confined to the designated control thread. Neither
            // sticky fallback nor the global native guard is reset between cases.
            Assembly control = Race(bytes, registry, gate, pending, observe, controlThread, cases, true, false, nameof(ControlTarget), out string controlOutcome);
            Assembly direct = Race(bytes, registry, gate, pending, observe, controlThread, cases, false, true, nameof(DirectTarget), out string directOutcome);
            Assembly reflected = Race(bytes, registry, gate, pending, observe, controlThread, cases, false, false, nameof(ReflectedTarget), out string reflectedOutcome);
            Require(controlOutcome == directOutcome && controlOutcome == reflectedOutcome,
                "Ordinary post-factory outcomes differ between control and guarded entries.");
            Require(!ReferenceEquals(control, direct) && !ReferenceEquals(direct, reflected), "Later loads did not create distinct Harmony copies.");
            var sentinel = new InvalidOperationException("C10 native-entry diagnostic failure");
            FailureCase(direct, gate, pending, inject, stats, diagnostics, cases, sentinel, true, nameof(DirectFailureTarget), directOutcome);
            FailureCase(reflected, gate, pending, inject, stats, diagnostics, cases, sentinel, false, nameof(ReflectedFailureTarget), reflectedOutcome);
            FailureCase(direct, gate, pending, injectBridge, stats, diagnostics, cases, sentinel, true, nameof(RecoveryTarget), directOutcome, true);
            string reverseDirect = ReverseCase(direct, gate, pending, observe, inject, stats, diagnostics, cases, sentinel, true);
            string reverseReflected = ReverseCase(reflected, gate, pending, observe, inject, stats, diagnostics, cases, sentinel, false);
            Require(reverseDirect == reverseReflected, "ReversePatch ordinary outcomes differ between invocation routes.");
            ((Dictionary<string, object>)report["coverage"])["reversePatch"] = true;
            OwnedReaderCase(direct, gate, pending, observe, stats, diagnostics, cases, directOutcome);
            report["ordinaryPostFactoryOutcomesMatch"] = true;
            ulong[] after = stats();
            Require(after.Length >= 12 && after[0] == 1 && after[1] == 1 && after[2] == 1 && after[3] == 0,
                "Native adapter did not remain ready with zero in-flight entries.");
            Require(after.Length >= 20 && after[4] != 0 && after[18] >= 1 && after[19] == 0,
                "Actual entry/bridge recovery qualification was not observed.");
            report["statsAfter"] = after;
            report["nativeOnlyRemainedSticky"] = (bool)registry["audioNativeOnly"];
            Require((bool)registry["audioNativeOnly"], "Unexpected copies did not retain native fallback.");
            report["functionalPassed"] = true;
        }
        catch (Exception error) { report["failure"] = error.ToString(); }
        finally
        {
            observe?.Invoke(null);
            controlThread?.Invoke(0);
            inject?.Invoke(null!);
            injectBridge?.Invoke(null!);
            factoryReached = null;
            factoryObservation = null;
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "c10-native-entry-probe.json"), C10NaturalAudioProbe.Json(report), new System.Text.UTF8Encoding(false));
        }
    }

    private static Assembly Race(byte[] bytes, Dictionary<string, object> registry, object gate,
        Dictionary<object, Func<int>> pending, Action<Action<IntPtr>?> observe, Action<int> controlThread,
        List<object> cases, bool unguarded, bool compiled, string target, out string ordinaryOutcome, string? filePath = null)
    {
        ordinaryOutcome = "not-completed";
        using var drainStarted = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        using var patchFinished = new ManualResetEventSlim();
        using var reuseStarted = new ManualResetEventSlim();
        using var factory = new ManualResetEventSlim();
        object token = new object();
        Exception? loadError = null, patchError = null;
        Assembly? loaded = null, discovered = null;
        Thread? patcher = null;
        int patchThread = 0;
        int sameFileReturned = 0;
        IntPtr observedIdentity = IntPtr.Zero;
        IntPtr originalIdentity = (IntPtr)registry["audioNativeOriginalHarmony"];
        IntPtr replacementIdentity = (IntPtr)registry["audioNativeReplacementHarmony"];
        Assembly[] before = AppDomain.CurrentDomain.GetAssemblies();
        ResetFactory(factory);
        lock (gate)
        {
            Require(pending.Count == 0, "Previous case left pending actions.");
            pending.Add(token, () =>
            {
                drainStarted.Set();
                Require(release.Wait(Timeout * 3), "Coordinator did not release synthetic drain.");
                Volatile.Write(ref settled, 1);
                return 1;
            });
        }
        var loader = new Thread(() =>
        {
            try { loaded = filePath == null ? Assembly.Load(bytes) : Assembly.LoadFile(filePath); }
            catch (Exception error) { loadError = error; }
        }) { IsBackground = true, Name = "C10 native-entry assembly loader" };
        bool loaderStarted = false, patcherStarted = false, loaderJoined = false, patcherJoined = false;
        try
        {
            loader.Start();
            loaderStarted = true;
            Require(drainStarted.Wait(Timeout), "AssemblyLoad did not enter the pending drain.");
            discovered = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "0Harmony"
                && !before.Any(old => ReferenceEquals(old, a)));
            Action invoke = Invocation(discovered, target, compiled);
            observe(identity =>
            {
                if (Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref patchThread)) return;
                if (identity == originalIdentity || identity == replacementIdentity) return;
                observedIdentity = identity;
                entered.Set();
            });
            patcher = new Thread(() =>
            {
                Volatile.Write(ref patchThread, Thread.CurrentThread.ManagedThreadId);
                if (unguarded) controlThread(Thread.CurrentThread.ManagedThreadId);
                try
                {
                    if (filePath != null)
                    {
                        reuseStarted.Set();
                        Assembly reused = Assembly.LoadFile(filePath);
                        Require(ReferenceEquals(reused, discovered), "Concurrent same-file reuse returned a different assembly.");
                        Volatile.Write(ref sameFileReturned, 1);
                    }
                    invoke();
                }
                catch (Exception error) { patchError = error; }
                finally { if (unguarded) controlThread(0); patchFinished.Set(); }
            }) { IsBackground = true, Name = "C10 native-entry patch caller" };
            patcher.Start();
            patcherStarted = true;
            if (filePath != null) Require(reuseStarted.Wait(Timeout), "Same-file caller did not start its load.");
            int signal = WaitHandle.WaitAny(new[] { entered.WaitHandle, patchFinished.WaitHandle }, Timeout);
            bool earlyEntry = signal == 0;
            bool earlyReuse = filePath != null && Volatile.Read(ref sameFileReturned) == 1;
            Require(earlyEntry || (filePath != null && signal == WaitHandle.WaitTimeout),
                "Third Harmony did not reach its instrumented native entry. Caller failure: " + patchError);
            Require(Volatile.Read(ref settled) == 0 && (!earlyEntry || observedIdentity != IntPtr.Zero),
                "Original drain did not remain blocked during the entry observation.");
            if (unguarded) Require(WaitHandle.WaitAny(new[] { factory.WaitHandle, patchFinished.WaitHandle }, Timeout) == 0,
                "Unguarded control did not reach the actual patch factory before release. Caller failure: " + patchError);
            else Require(Volatile.Read(ref factoryCalls) == 0, "Guarded factory ran before pending settlement.");
            int beforeRelease = Volatile.Read(ref factoryCalls);
            release.Set();
            loaderJoined = loader.Join(Timeout);
            patcherJoined = patcher.Join(Timeout);
            Require(loaderJoined && patcherJoined, "Workers did not finish after drain release.");
            Require(loadError == null, "Assembly load failed: " + loadError);
            Require(entered.IsSet && observedIdentity != IntPtr.Zero && (filePath == null || sameFileReturned == 1),
                "Caller did not complete reuse and reach its actual entry after release: " + patchError);
            Require(ReferenceEquals(loaded, discovered), "Public discovery did not return the same assembly as the blocked load.");
            Require(Volatile.Read(ref settled) == 1 && Volatile.Read(ref factoryCalls) > 0, "Drain or actual factory did not complete.");
            Require(unguarded ? Volatile.Read(ref factoryBeforeSettlement) > 0 : Volatile.Read(ref factoryBeforeSettlement) == 0,
                "Factory/settlement ordering did not match the control.");
            lock (gate) Require(!pending.ContainsKey(token), "Completed sentinel was not removed.");
            ordinaryOutcome = Outcome(patchError);
            cases.Add(new Dictionary<string, object>
            {
                ["kind"] = filePath != null ? "guarded-concurrent-same-file-reuse" : unguarded ? "explicit-thread-unguarded-control" : compiled ? "guarded-compiled-delegate" : "guarded-reflected-later-copy",
                ["passed"] = true, ["loadRoute"] = filePath == null ? "Assembly.Load(byte[])" : "Assembly.LoadFile(same full path)", ["discoveredWhileAssemblyLoadBlocked"] = true,
                ["sameFileReuseReturnedWhileFirstLoadBlocked"] = earlyReuse,
                ["earlyReuseObserved"] = earlyReuse && earlyEntry,
                ["entryObservedWhileFirstLoadBlocked"] = earlyEntry,
                ["earlyReturnClosureClaim"] = filePath != null && earlyReuse && earlyEntry,
                ["reuseStartedBeforeRelease"] = reuseStarted.IsSet,
                ["actualEntryObservedAfterJoin"] = true,
                ["sameAssemblyReferenceAfterLoad"] = true, ["nativeAssemblyIdentity"] = observedIdentity.ToString(),
                ["factoryCallsBeforeRelease"] = beforeRelease, ["factoryCalls"] = factoryCalls,
                ["factoryBeforeSettlement"] = factoryBeforeSettlement, ["sentinelSettled"] = true,
                ["workersJoined"] = true, ["moduleMvid"] = discovered.ManifestModule.ModuleVersionId.ToString(),
                ["ordinaryPostFactoryOutcome"] = ordinaryOutcome,
                ["ordinaryPostFactoryError"] = patchError?.ToString() ?? "",
                ["wrapperCompleted"] = patchError == null
            });
            return discovered;
        }
        finally
        {
            release.Set();
            if (loaderStarted && !loaderJoined) loaderJoined = loader.Join(Timeout);
            if (patcherStarted && !patcherJoined) patcherJoined = patcher!.Join(Timeout);
            observe(null); controlThread(0); factoryReached = null;
            // Never block on a registry gate still owned by a timed-out worker.
            if ((!loaderStarted || loaderJoined) && (!patcherStarted || patcherJoined)) lock (gate) pending.Remove(token);
            Require((!loaderStarted || loaderJoined) && (!patcherStarted || patcherJoined), "A challenge worker remained alive after final barrier release.");
        }
    }

    private static void FailureCase(Assembly harmony, object gate, Dictionary<object, Func<int>> pending,
        Action<Exception> inject, Func<ulong[]> stats, Func<long> diagnostics, List<object> cases,
        Exception sentinel, bool compiled, string target, string expectedOrdinaryOutcome, bool bridgeRecovery = false)
    {
        object token = new object();
        ResetFactory(null);
        Action invoke = Invocation(harmony, target, compiled);
        lock (gate) pending.Add(token, () => { Volatile.Write(ref settled, 1); return 1; });
        ulong[] before = stats();
        long diagnosticBefore = diagnostics();
        Exception? observed = null;
        try
        {
            inject(sentinel);
            try { invoke(); } catch (Exception error) { observed = error; }
            ulong[] after = stats();
            Require(Outcome(observed) == expectedOrdinaryOutcome && !ReferenceEquals(observed?.GetBaseException(), sentinel),
                "Entry diagnostic/recovery changed the ordinary post-factory outcome.");
            Require(factoryCalls > 0 && factoryBeforeSettlement == 0 && Volatile.Read(ref settled) == 1 && after[3] == 0,
                "Entry diagnostic/recovery skipped mandatory settlement or leaked an in-flight entry.");
            lock (gate) Require(!pending.ContainsKey(token), "Mandatory settlement left pending work.");
            Require(bridgeRecovery
                ? after.Length >= 20 && after[8] == before[8] + 1 && after[18] == before[18] + 1 && after[19] == before[19]
                : diagnostics() == diagnosticBefore + 1 && after[8] == before[8],
                "Diagnostic catch or native bridge recovery counters did not match the injected control.");
            Exception? retryError = null;
            try { invoke(); } catch (Exception error) { retryError = error; }
            Require(factoryCalls > 0 && factoryBeforeSettlement == 0 && Volatile.Read(ref settled) == 1 && stats()[3] == 0,
                "Subsequent ordinary entry changed settled state.");
            lock (gate) Require(!pending.ContainsKey(token), "Retry left its completed sentinel pending.");
            Require(Outcome(retryError) == expectedOrdinaryOutcome, "Retry changed the ordinary post-factory outcome.");
            cases.Add(new Dictionary<string, object>
            {
                ["kind"] = bridgeRecovery ? "native-bridge-exception-recovered" : compiled ? "diagnostic-failure-compiled-delegate" : "diagnostic-failure-reflected",
                ["passed"] = true, ["diagnosticEscapedToCaller"] = false,
                ["mandatoryDrainBeforeFactory"] = true, ["pendingRemoved"] = true, ["inFlightAfterCall"] = 0,
                ["nativeBefore"] = before, ["nativeAfter"] = after,
                ["diagnosticFailuresBefore"] = diagnosticBefore, ["diagnosticFailuresAfter"] = diagnostics(),
                ["ordinaryOutcome"] = Outcome(observed), ["ordinaryError"] = observed?.ToString() ?? "",
                ["retryOrdinaryPostFactoryOutcome"] = Outcome(retryError),
                ["retryOrdinaryPostFactoryError"] = retryError?.ToString() ?? ""
            });
        }
        finally { inject(null!); lock (gate) pending.Remove(token); }
    }

    private static Action Invocation(Assembly assembly, string targetName, bool compiled) => UpdateInvocation(assembly,
        typeof(C10NativeEntryProbe).GetMethod(targetName, StaticMethods)!,
        typeof(C10NativeEntryProbe).GetMethod(nameof(Factory), StaticMethods)!, compiled);

    internal static Action UpdateInvocation(Assembly assembly, MethodBase target, MethodInfo factory, bool compiled)
    {
        Type hm = assembly.GetType("HarmonyLib.HarmonyMethod", true)!;
        Type infoType = assembly.GetType("HarmonyLib.PatchInfo", true)!;
        Type functions = assembly.GetType("HarmonyLib.PatchFunctions", true)!;
        object prefix = Activator.CreateInstance(hm, new object[] { factory })!;
        object info = Activator.CreateInstance(infoType)!;
        Array prefixes = Array.CreateInstance(hm, 1);
        prefixes.SetValue(prefix, 0);
        MethodInfo add = infoType.GetMethod("AddPrefixes", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(string), hm.MakeArrayType() }, null)
            ?? throw new InvalidOperationException("Pinned PatchInfo.AddPrefixes signature is absent.");
        add.Invoke(info, new object[] { "local.fixture.c10-native-entry." + target.Name, prefixes });
        MethodInfo patch = functions.GetMethod("UpdateWrapper", StaticMethods, null, new[] { typeof(MethodBase), infoType }, null)
            ?? throw new InvalidOperationException("Pinned PatchFunctions.UpdateWrapper signature is absent.");
        if (!compiled) return () => patch.Invoke(null, new[] { (object)target, info });
        var method = new DynamicMethod("C10DirectThirdUpdateWrapper", typeof(void), new[] { typeof(MethodBase), typeof(object) }, typeof(C10NativeEntryProbe), true);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Castclass, infoType);
        il.Emit(OpCodes.Call, patch); il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ret);
        var direct = (Action<MethodBase, object>)method.CreateDelegate(typeof(Action<MethodBase, object>));
        return () => direct(target, info);
    }

    private static string ReverseCase(Assembly assembly, object gate, Dictionary<object, Func<int>> pending,
        Action<Action<IntPtr>?> observe, Action<Exception> inject, Func<ulong[]> stats, Func<long> diagnostics, List<object> cases,
        Exception sentinel, bool compiled)
    {
        Type hm = assembly.GetType("HarmonyLib.HarmonyMethod", true)!;
        Type functions = assembly.GetType("HarmonyLib.PatchFunctions", true)!;
        MethodInfo standinMethod = typeof(C10NativeEntryProbe).GetMethod(compiled ? nameof(ReverseDirectTarget) : nameof(ReverseReflectedTarget), StaticMethods)!;
        MethodInfo original = typeof(C10NativeEntryProbe).GetMethod(nameof(ControlTarget), StaticMethods)!;
        object standin = Activator.CreateInstance(hm, new object[] { standinMethod })!;
        MethodInfo reverse = functions.GetMethod("ReversePatch", StaticMethods, null,
            new[] { hm, typeof(MethodBase), typeof(MethodInfo) }, null)
            ?? throw new InvalidOperationException("Pinned ReversePatch signature is absent.");
        Action invoke;
        if (!compiled) invoke = () => reverse.Invoke(null, new object?[] { standin, original, null });
        else
        {
            var method = new DynamicMethod("C10DirectThirdReversePatch", typeof(void),
                new[] { typeof(object), typeof(MethodBase) }, typeof(C10NativeEntryProbe), true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, hm); il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Call, reverse); il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ret);
            var direct = (Action<object, MethodBase>)method.CreateDelegate(typeof(Action<object, MethodBase>));
            invoke = () => direct(standin, original);
        }
        object token = new object();
        int drained = 0, observedEntries = 0, thread = Thread.CurrentThread.ManagedThreadId;
        IntPtr identity = IntPtr.Zero;
        ulong[] before = stats();
        long diagnosticBefore = diagnostics();
        observe(value => { if (Thread.CurrentThread.ManagedThreadId == thread) { identity = value; observedEntries++; } });
        lock (gate) pending.Add(token, () => { drained++; return 1; });
        try
        {
            inject(sentinel);
            Exception? failure = null;
            try { invoke(); } catch (Exception error) { failure = error; }
            Require(!ReferenceEquals(failure?.GetBaseException(), sentinel) && observedEntries == 1 && identity != IntPtr.Zero
                && drained == 1 && stats()[3] == 0 && diagnostics() == diagnosticBefore + 1,
                "ReversePatch diagnostic prevented mandatory settlement or escaped its entry.");
            lock (gate) Require(!pending.ContainsKey(token), "ReversePatch left pending work after its diagnostic.");
            Exception? ordinary = null;
            try { invoke(); } catch (Exception error) { ordinary = error; }
            ulong[] after = stats();
            Require(observedEntries >= 2 && drained == 1 && after[3] == 0 && after[5] > before[5] && after[6] >= before[6] + 2,
                "ReversePatch retry did not exercise the actual instrumented entry and settle pending work.");
            lock (gate) Require(!pending.ContainsKey(token), "ReversePatch retry retained completed work.");
            Require(Outcome(failure) == Outcome(ordinary) && after[8] == before[8], "ReversePatch diagnostic changed its ordinary behavior.");
            cases.Add(new Dictionary<string, object>
            {
                ["kind"] = compiled ? "reverse-patch-compiled-diagnostic" : "reverse-patch-reflected-diagnostic",
                ["passed"] = true, ["diagnosticEscapedToCaller"] = false, ["mandatoryDrainCompleted"] = true,
                ["ordinaryOutcomesMatch"] = true, ["nativeAssemblyIdentity"] = identity.ToString(),
                ["diagnosticFailuresBefore"] = diagnosticBefore, ["diagnosticFailuresAfter"] = diagnostics(),
                ["observedEntries"] = observedEntries, ["reverseFilterDelta"] = after[5] - before[5],
                ["ordinaryOutcome"] = Outcome(ordinary), ["ordinaryError"] = ordinary?.ToString() ?? "",
                ["userFactoryOrderingClaim"] = false, ["wrapperCompleted"] = ordinary == null
            });
            return Outcome(ordinary);
        }
        finally { inject(null!); observe(null); lock (gate) pending.Remove(token); }
    }

    internal static WaveStream CreateOwnedWarmReader()
    {
        object data = RecordedWav(out Type proxyType, out byte[] wav);
        Func<WaveStream> create = () => new WaveFileReader(new MemoryStream(wav, false));
        var reader = (WaveStream)Activator.CreateInstance(proxyType, BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { data, create }, null)!;
        _ = reader.WaveFormat; _ = reader.Length;
        Require(reader.Read(new byte[4], 0, 4) == 4, "Owned warm reader did not serve its genuine recording.");
        return reader;
    }

    private static object RecordedWav(out Type proxyType, out byte[] wav)
    {
        Assembly core = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "WakeUp");
        proxyType = core.GetType("WakeUp.PreparedAudioWaveStream", true)!;
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        using (var memory = new MemoryStream())
        {
            using (var writer = new BinaryWriter(memory, System.Text.Encoding.ASCII, true))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(44);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(8);
                writer.Write(new byte[] { 0, 0, 255, 127, 0, 128, 123, 0 });
            }
            wav = memory.ToArray();
        }
        object data;
        using (var cold = (WaveStream)Activator.CreateInstance(proxyType, instance, null,
            new object[] { new WaveFileReader(new MemoryStream(wav, false)) }, null)!)
        {
            _ = cold.WaveFormat; _ = cold.Length;
            Require(cold.Read(new byte[4], 0, 4) == 4, "Native WAV recording did not return its first bytes.");
            data = proxyType.GetMethod("FinishRecording", instance)!.Invoke(cold, null)
                ?? throw new InvalidOperationException("Genuine native WAV recording was absent.");
        }
        return data;
    }

    // Real product proxies with a transcript recorded by the original WAV
    // reader. They are deliberately isolated from clip admission and disk cache.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OwnedReaderCase(Assembly assembly, object gate, Dictionary<object, Func<int>> pending,
        Action<Action<IntPtr>?> observe, Func<ulong[]> stats, Func<long> diagnostics, List<object> cases,
        string expectedOrdinaryOutcome)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        object data = RecordedWav(out Type proxyType, out byte[] wav);
        Exception? nativeFailure = null;
        var constructionOrder = new List<string>();
        Func<WaveStream> faultFactory = () =>
        {
            constructionOrder.Add("fault");
            var malformed = new MemoryStream(new byte[] { 0 }, false);
            try { return new WaveFileReader(malformed); }
            catch (Exception error) { nativeFailure = error; malformed.Dispose(); throw; }
        };
        Func<WaveStream> healthyFactory = () =>
        {
            constructionOrder.Add("healthy");
            return new WaveFileReader(new MemoryStream(wav, false));
        };
        using var faulted = (WaveStream)Activator.CreateInstance(proxyType, instance, null, new object[] { data, faultFactory }, null)!;
        using var healthy = (WaveStream)Activator.CreateInstance(proxyType, instance, null, new object[] { data, healthyFactory }, null)!;
        foreach (WaveStream reader in new[] { faulted, healthy })
        {
            _ = reader.WaveFormat; _ = reader.Length;
            Require(reader.Read(new byte[4], 0, 4) == 4, "Warm product proxy did not serve the recorded bytes.");
        }
        MethodInfo settle = proxyType.GetMethod("SettleNativeOutcome", instance)!;
        var faultOutcome = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), faulted, settle);
        var healthyOutcome = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), healthy, settle);
        MethodInfo snapshot = proxyType.GetMethod("Snapshot", instance)!;
        Dictionary<string, object> Snapshot(WaveStream reader) => (Dictionary<string, object>)snapshot.Invoke(reader, null)!;
        long Number(Dictionary<string, object> state, string name) => Convert.ToInt64(state[name]);
        Dictionary<string, object>? faultAtFactory = null, healthyAtFactory = null;
        ResetFactory(null);
        factoryObservation = () =>
        {
            faultAtFactory = Snapshot(faulted); healthyAtFactory = Snapshot(healthy);
            Require((bool)faultAtFactory["terminallyFaulted"] && (bool)healthyAtFactory["nativeReady"]
                && Number(faultAtFactory, "initializationAttempts") == 1 && Number(healthyAtFactory, "initializationAttempts") == 1,
                "Actual factory reached a faulting or healthy reader before mandatory settlement completed.");
            Volatile.Write(ref settled, 1);
        };
        Action invoke = Invocation(assembly, nameof(OwnedReaderTarget), true);
        long diagnosticBefore = diagnostics();
        ulong[] nativeBefore = stats();
        lock (gate)
        {
            Require(pending.Count == 0, "Owned-reader case requires an empty registry.");
            pending.Add(faulted, faultOutcome); pending.Add(healthy, healthyOutcome);
        }
        var diagnostic = new InvalidOperationException("C10 entry-observer diagnostic failure");
        observe(_ => throw diagnostic);
        try
        {
            Exception? ordinary = null;
            try { invoke(); } catch (Exception error) { ordinary = error; }
            Require(Outcome(ordinary) == expectedOrdinaryOutcome && factoryCalls > 0 && factoryBeforeSettlement == 0,
                "Faulting owned reader or diagnostic prevented ordinary factory behavior.");
            lock (gate) Require(pending.Count == 0, "Mandatory settlement did not remove both owned readers.");
            Require(constructionOrder.SequenceEqual(new[] { "fault", "healthy" }) && nativeFailure != null
                && faultOutcome() == 2 && healthyOutcome() == 1, "Faulting reader did not settle before the healthy reader.");
            Require(diagnostics() == diagnosticBefore + 1 && stats()[8] == nativeBefore[8] && stats()[3] == 0,
                "Observer diagnostic escaped its catch or leaked a native entry.");
            Exception? readFailure = null, positionFailure = null;
            try { faulted.Read(new byte[4], 0, 4); } catch (Exception error) { readFailure = error; }
            try { _ = faulted.Position; } catch (Exception error) { positionFailure = error; }
            Require(ReferenceEquals(readFailure, nativeFailure) && ReferenceEquals(positionFailure, nativeFailure),
                "Ordinary public reader operations lost the original native constructor exception.");
            using var control = new WaveFileReader(new MemoryStream(wav, false));
            control.Read(new byte[4], 0, 4);
            var expected = new byte[4]; var actual = new byte[4];
            int expectedCount = control.Read(expected, 0, 4), actualCount = healthy.Read(actual, 0, 4);
            Require(actualCount == expectedCount && actual.SequenceEqual(expected)
                && Number(Snapshot(faulted), "initializationAttempts") == 1
                && Number(Snapshot(healthy), "initializationAttempts") == 1
                && Number(Snapshot(healthy), "replayMismatches") == 0,
                "Healthy reader bytes or the one-attempt settlement contract changed.");
            faulted.Dispose(); healthy.Dispose();
            Require(faultOutcome() == 3 && healthyOutcome() == 3, "Disposed owned readers did not return the terminal disposal outcome.");
            cases.Add(new Dictionary<string, object>
            {
                ["kind"] = "owned-faulted-reader-followed-by-healthy", ["passed"] = true,
                ["recordedFromNativeWav"] = true, ["productAdmissionEnabled"] = false, ["diskCacheUsed"] = false,
                ["constructionOrder"] = constructionOrder.ToArray(), ["faultOutcome"] = 2, ["healthyOutcome"] = 1,
                ["faultReaderAtFactory"] = faultAtFactory!, ["healthyReaderAtFactory"] = healthyAtFactory!,
                ["originalNativeExceptionType"] = nativeFailure!.GetType().FullName!, ["originalNativeExceptionMessage"] = nativeFailure.Message,
                ["consumerExceptionReferencePreserved"] = true, ["healthyNativeBytesMatch"] = true,
                ["diagnosticFailuresBefore"] = diagnosticBefore, ["diagnosticFailuresAfter"] = diagnostics(),
                ["ordinaryOutcome"] = Outcome(ordinary), ["ordinaryError"] = ordinary?.ToString() ?? "",
                ["disposedOutcomes"] = new[] { 3, 3 }, ["pendingRemoved"] = true
            });
        }
        finally
        {
            observe(null); factoryObservation = null;
            lock (gate)
            {
                foreach (var reader in new[] { new KeyValuePair<WaveStream, Func<int>>(faulted, faultOutcome),
                    new KeyValuePair<WaveStream, Func<int>>(healthy, healthyOutcome) })
                {
                    int outcome = reader.Value();
                    if (outcome < 1 || outcome > 3)
                    {
                        reader.Key.Dispose();
                        outcome = reader.Value();
                        Require(outcome == 3, "Owned reader cleanup did not confirm terminal disposal.");
                    }
                    pending.Remove(reader.Key);
                }
            }
        }
    }

    private static string Outcome(Exception? error) => error == null ? "returned" :
        error.GetBaseException().GetType().FullName + ": " + error.GetBaseException().Message;

    private static void ResetFactory(ManualResetEventSlim? signal)
    {
        factoryCalls = 0; settled = 0; factoryBeforeSettlement = 0; factoryReached = signal; factoryObservation = null;
    }

    private static MethodInfo Factory(MethodBase original)
    {
        factoryObservation?.Invoke();
        Interlocked.Increment(ref factoryCalls);
        if (Volatile.Read(ref settled) == 0) Interlocked.Increment(ref factoryBeforeSettlement);
        factoryReached?.Set();
        return typeof(C10NativeEntryProbe).GetMethod(nameof(Prefix), StaticMethods)!;
    }
    private static void Prefix() { }
    [MethodImpl(MethodImplOptions.NoInlining)] private static int ControlTarget() => 11;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int DirectTarget() => 12;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int ReflectedTarget() => 13;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int DirectFailureTarget() => 14;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int ReflectedFailureTarget() => 15;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int SameFileTarget() => 16;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int ReverseDirectTarget() => 17;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int ReverseReflectedTarget() => 18;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int StopTarget() => 19;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int RecoveryTarget() => 20;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int OwnedReaderTarget() => 21;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
