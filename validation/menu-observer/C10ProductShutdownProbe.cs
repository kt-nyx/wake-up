// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using NAudio.Wave;

namespace FixtureMenuObserver;

// Arranges an owned reader and an in-flight bridge. Only the normal product
// shutdown path may settle that reader or stop the adapter.
internal static class C10ProductShutdownProbe
{
    private const int Timeout = 10000;
    private static readonly BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static string directory = "";
    private static Dictionary<string, object>? report, registry;
    private static Dictionary<object, Func<int>>? pending;
    private static object? gate;
    private static Func<ulong[]>? stats;
    private static Action<Action<IntPtr>?>? observe;
    private static WaveStream? reader;
    private static Action? invoke;
    private static Thread? caller, coordinator;
    private static readonly ManualResetEventSlim entered = new(), release = new();
    private static bool callerStarted, coordinatorStarted, callerJoined, coordinatorJoined, stopMode;
    private static int callerThread, factoryCalls, earlyFactory;
    private static Exception? callerError, coordinatorError;
    private static string ordinaryOutcome = "";

    internal static void SelectStop(string output)
    {
        stopMode = true;
        directory = output;
        report = new Dictionary<string, object>
        {
            ["schema"] = "c10-native-entry-functional.v1", ["probeMode"] = "stop-only",
            ["functionalPassed"] = false, ["failure"] = "", ["productRootShutdown"] = true,
            ["observerCalledStop"] = false, ["observerCalledSettlement"] = false,
            ["actualReaderAcceptance"] = false, ["performanceClaim"] = false,
            ["thirdCopyPatchCompletionClaim"] = false, ["cases"] = Array.Empty<object>()
        };
        Write();
    }

    internal static void StartDomain(string output)
    {
        directory = output;
        report = new Dictionary<string, object>
        {
            ["schema"] = "c10-native-entry-functional.v1", ["probeMode"] = "domain",
            ["functionalPassed"] = false, ["failure"] = "", ["observerCalledStop"] = false,
            ["observerCalledSettlement"] = false, ["actualReaderAcceptance"] = false,
            ["performanceClaim"] = false, ["thirdCopyPatchCompletionClaim"] = false,
            ["wholeRemoteCallProtectedFromUnload"] = false, ["cases"] = new List<object>()
        };
        AppDomain? secondary = null;
        bool unloaded = false;
        Dictionary<string, object>? heldResult = null;
        try
        {
            var bootstrap = (Dictionary<string, object>)AppDomain.CurrentDomain.GetData("WakeUp.PreparedAudio.Bootstrap.v1");
            registry = (Dictionary<string, object>)bootstrap["audioRegistry"];
            gate = registry["audioRegistryGate"];
            pending = (Dictionary<object, Func<int>>)registry["audioPending"];
            stats = (Func<ulong[]>)registry["audioNativeEntryStats"];
            observe = (Action<Action<IntPtr>?>)registry["audioNativeEntryObserve"];
            lock (gate) Check(pending.Count == 0, "Domain case requires no unrelated pending reader.");
            ulong[] initial = stats();
            Check(initial.Length >= 33 && initial[2] == 1, "Domain lifecycle adapter is not active.");
            AppDomain bound = AppDomain.CurrentDomain;
            report["managedBoundDomainId"] = bound.Id;
            report["managedBoundDomainName"] = bound.FriendlyName;
            report["managedBoundDomainIsDefault"] = bound.IsDefaultAppDomain();
            report["nativeBoundDomainId"] = initial[27];
            report["nativeRootDomainId"] = initial[28];
            report["nativeBoundIsRoot"] = initial[29];
            Check(initial[27] == (ulong)bound.Id && (initial[29] == 1) == bound.IsDefaultAppDomain()
                && (initial[27] == initial[28]) == (initial[29] == 1), "Managed and native bound domain identities disagree.");
            report["nativeEntryModulePath"] = registry["audioNativeEntryModulePath"];
            byte[] harmony = File.ReadAllBytes(((Assembly)registry["audioOriginalHarmony"]).Location);
            // Prepatcher loads the active observer from bytes, leaving Location
            // empty. The secondary domain needs the actual deployed package.
            string observerPath = Path.Combine(Verse.LoadedModManager.RunningMods
                .Single(mod => mod.PackageId == "local.fixture.menuobserver").RootDir,
                "Assemblies", "FixtureMenuObserver.dll");
            report["secondaryObserverPath"] = observerPath;
            secondary = AppDomain.CreateDomain("C10 synchronized entry domain", null,
                new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(typeof(object).Assembly.Location) });
            // The file was already inspected reflection-only by Prepatcher.
            // Load executable bytes in this domain instead of reusing that image.
            secondary.Load(File.ReadAllBytes(observerPath));
            var remote = (C10DomainRemote)secondary.CreateInstanceAndUnwrap(
                typeof(C10DomainRemote).Assembly.FullName, typeof(C10DomainRemote).FullName);
            Dictionary<string, object> completed = remote.Exercise(harmony, false);
            ulong[] afterControl = stats();
            Check(Convert.ToInt32(completed["domainBefore"]) == secondary.Id && secondary.Id != bound.Id
                && Equals(completed["domainBefore"], completed["domainAfter"])
                && Convert.ToInt32(completed["contextBefore"]) != 0
                && Equals(completed["contextBefore"], completed["contextAfter"])
                && Convert.ToInt32(completed["factoryCalls"]) == 1,
                "Completed synchronized call did not preserve its secondary domain/context and actual factory.");
            Check(afterControl[24] > initial[24] && afterControl[25] > initial[25]
                && afterControl[26] == initial[26] && afterControl[2] == 1,
                "Completed call did not qualify the native cross-domain context restoration.");
            completed["name"] = "completed-synchronized-remote-entry";
            completed["passed"] = true;
            ((List<object>)report["cases"]).Add(completed);
            report["statsBeforeDomainControl"] = initial;
            report["statsAfterDomainControl"] = afterControl;

            reader = C10NativeEntryProbe.CreateOwnedWarmReader();
            Dictionary<string, object> before = ReaderSnapshot();
            Check(Number(before, "initializationAttempts") == 0 && !(bool)before["nativeReady"], "Domain reader was not deferred.");
            report["readerBeforeUnload"] = before;
            var settle = reader.GetType().GetMethod("SettleNativeOutcome", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var callback = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), reader, settle);
            lock (gate) pending.Add(reader, callback);
            observe(_ =>
            {
                if (Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref callerThread)) return;
                entered.Set();
                Check(release.Wait(Timeout * 3), "Domain coordinator did not release the held entry.");
            });
            caller = new Thread(() =>
            {
                Volatile.Write(ref callerThread, Thread.CurrentThread.ManagedThreadId);
                try { heldResult = remote.Exercise(harmony, true); }
                catch (Exception error) { callerError = error; }
            }) { IsBackground = true, Name = "C10 secondary domain held entry" };
            caller.Start(); callerStarted = true;
            Check(entered.Wait(Timeout), "Secondary actual native entry did not reach the hold.");
            ulong[] beforeUnload = stats();
            report["statsBeforeUnload"] = beforeUnload;
            coordinator = new Thread(() =>
            {
                try
                {
                    Check(SpinWait.SpinUntil(() => stats()[2] == 0, Timeout), "Domain unload did not close native admission.");
                    ulong[] held = stats();
                    Check(held[3] == 1, "Domain unload did not join the held entry bridge.");
                    Dictionary<string, object> terminal = ReaderSnapshot();
                    Check((bool)terminal["nativeReady"] && Number(terminal, "initializationAttempts") == 1
                        && Number(terminal, "decoderConstructions") == 1 && Number(terminal, "replayMismatches") == 0,
                        "Domain unload did not settle the actual pending reader before joining.");
                    lock (gate) Check(pending.Count == 0, "Domain unload left its reader pending.");
                    report["statsWhileUnloadWaits"] = held;
                    report["readerWhileUnloadWaits"] = terminal;
                }
                catch (Exception error) { coordinatorError = error; }
                finally { release.Set(); }
            }) { IsBackground = true, Name = "C10 domain unload coordinator" };
            coordinator.Start(); coordinatorStarted = true;
            AppDomain.Unload(secondary);
            unloaded = true;
            JoinWorkers();
            Check(callerJoined && coordinatorJoined && coordinatorError == null, "Domain workers did not join: " + coordinatorError);
            bool legitimateAbort = callerError is AppDomainUnloadedException || callerError is ThreadAbortException;
            if (heldResult != null)
                Check(Equals(heldResult["domainBefore"], heldResult["domainAfter"])
                    && Equals(heldResult["contextBefore"], heldResult["contextAfter"])
                    && Convert.ToInt32(heldResult["factoryCalls"]) == 1
                    && Equals(heldResult["ordinaryOutcome"], completed["ordinaryOutcome"]),
                    "A completed held call changed its native outcome or caller context.");
            else Check(legitimateAbort, "Held call failed outside the documented domain abort: " + callerError);
            ulong[] after = stats();
            Check(after[2] == 0 && after[3] == 0 && after[9] == 1 && after[21] > beforeUnload[21]
                && after[22] > beforeUnload[22] && after[23] == beforeUnload[23]
                && after[25] > beforeUnload[25] && after[26] == beforeUnload[26]
                && after[30] == 0 && after[31] == beforeUnload[31],
                "Domain lifecycle did not complete its bridge drain/join before unload.");
            lock (gate) Check(pending.Count == 0, "Reader remained pending after domain unload.");
            report["statsAfterUnload"] = after;
            report["readerAfterUnload"] = ReaderSnapshot();
            report["workersJoined"] = true;
            report["domainUnloadPassed"] = true;
            report["completedContextRestorationPassed"] = true;
            ((List<object>)report["cases"]).Add(new Dictionary<string, object>
            {
                ["name"] = "held-entry-domain-unload", ["passed"] = true,
                ["remoteCallReturned"] = heldResult != null, ["remoteResult"] = heldResult ?? new Dictionary<string, object>(),
                ["remoteExceptionType"] = callerError?.GetType().FullName ?? "", ["remoteOutcome"] = Outcome(callerError),
                ["legitimateDomainAbort"] = legitimateAbort, ["bridgeOnlyLifetimeClaim"] = true
            });
            report["functionalPassed"] = true;
        }
        catch (Exception error) { Fail(error); }
        finally
        {
            release.Set(); JoinWorkers(); observe?.Invoke(null);
            if (secondary != null && !unloaded)
            {
                try { AppDomain.Unload(secondary); } catch (Exception error) { Fail(error); }
            }
            DisposeTerminalReader();
            Write();
        }
    }

    internal static void BeforeRootShutdown()
    {
        if (!stopMode || report == null) return;
        try
        {
            var bootstrap = (Dictionary<string, object>)AppDomain.CurrentDomain.GetData("WakeUp.PreparedAudio.Bootstrap.v1");
            registry = (Dictionary<string, object>)bootstrap["audioRegistry"];
            gate = registry["audioRegistryGate"];
            pending = (Dictionary<object, Func<int>>)registry["audioPending"];
            stats = (Func<ulong[]>)registry["audioNativeEntryStats"];
            observe = (Action<Action<IntPtr>?>)registry["audioNativeEntryObserve"];
            Check((bool)registry["audioNativeProductStopInstalled"], "Product shutdown was not installed independently of audio admission.");
            lock (gate) Check(pending.Count == 0, "Product shutdown case requires no unrelated pending reader.");
            report["nativeEntryModulePath"] = registry["audioNativeEntryModulePath"];
            var original = (Assembly)registry["audioOriginalHarmony"];
            Assembly third = Assembly.Load(File.ReadAllBytes(original.Location));
            MethodInfo factory = typeof(C10ProductShutdownProbe).GetMethod(nameof(Factory), Methods)!;
            var control = C10NativeEntryProbe.UpdateInvocation(third,
                typeof(C10ProductShutdownProbe).GetMethod(nameof(ControlTarget), Methods)!, factory, true);
            Exception? controlError = null;
            try { control(); } catch (Exception error) { controlError = error; }
            Check(factoryCalls > 0, "Ordinary third-copy control never reached its actual factory.");
            ordinaryOutcome = Outcome(controlError);
            report["ordinaryControlOutcome"] = ordinaryOutcome;
            reader = C10NativeEntryProbe.CreateOwnedWarmReader();
            Dictionary<string, object> before = ReaderSnapshot();
            Check(Number(before, "initializationAttempts") == 0 && !(bool)before["nativeReady"], "Owned reader was not genuinely deferred.");
            report["readerBeforeShutdown"] = before;
            MethodInfo settle = reader.GetType().GetMethod("SettleNativeOutcome", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var callback = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), reader, settle);
            lock (gate) pending.Add(reader, callback);
            invoke = C10NativeEntryProbe.UpdateInvocation(third,
                typeof(C10ProductShutdownProbe).GetMethod(nameof(ShutdownTarget), Methods)!, factory, true);
            factoryCalls = 0; earlyFactory = 0;
            observe(_ =>
            {
                if (Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref callerThread)) return;
                entered.Set();
                Check(release.Wait(Timeout * 3), "Shutdown coordinator did not release the held native entry.");
            });
            caller = new Thread(() =>
            {
                Volatile.Write(ref callerThread, Thread.CurrentThread.ManagedThreadId);
                try { invoke(); } catch (Exception error) { callerError = error; }
            }) { IsBackground = true, Name = "C10 product shutdown held entry" };
            caller.Start(); callerStarted = true;
            Check(entered.Wait(Timeout), "Actual native entry did not reach the shutdown hold.");
            report["statsBeforeStop"] = stats();
            Check(factoryCalls == 0, "Factory ran before product shutdown started.");
            coordinator = new Thread(() =>
            {
                try
                {
                    Check(SpinWait.SpinUntil(() => stats()[2] == 0, Timeout), "Product shutdown did not close native admission.");
                    ulong[] held = stats();
                    Check(held[3] == 1 && factoryCalls == 0, "Product stop did not wait for the held native entry.");
                    Dictionary<string, object> terminal = ReaderSnapshot();
                    Check((bool)terminal["nativeReady"] && Number(terminal, "initializationAttempts") == 1
                        && Number(terminal, "decoderConstructions") == 1 && Number(terminal, "replayMismatches") == 0,
                        "Product shutdown did not settle its actual reader before joining.");
                    lock (gate) Check(pending.Count == 0, "Product shutdown left its settled reader pending.");
                    report["statsWhileStopWaits"] = held;
                    report["readerWhileStopWaits"] = terminal;
                }
                catch (Exception error) { coordinatorError = error; }
                finally { release.Set(); }
            }) { IsBackground = true, Name = "C10 product shutdown coordinator" };
            coordinator.Start(); coordinatorStarted = true;
        }
        catch (Exception error)
        {
            Fail(error);
            release.Set();
            JoinWorkers();
        }
    }

    internal static void AfterRootShutdown()
    {
        if (!stopMode || report == null) return;
        try
        {
            Check(callerStarted && coordinatorStarted, "Product shutdown arrangement was incomplete.");
            JoinWorkers();
            Check(callerJoined && coordinatorJoined && coordinatorError == null, "Product shutdown workers did not complete: " + coordinatorError);
            Check(Outcome(callerError) == ordinaryOutcome, "Product shutdown changed the ordinary third-copy outcome.");
            Check(factoryCalls == 1 && earlyFactory == 0, "Factory was not ordered after actual reader settlement.");
            ulong[] after = stats!();
            Check(after[2] == 0 && after[3] == 0 && after[9] == 1, "Native stop did not finish before Root.Shutdown returned.");
            var productState = registry!;
            lock (gate!)
            {
                Check(pending!.Count == 0 && Convert.ToInt32(productState["audioNativeProductStopEntries"]) > 0
                    && Equals(productState["audioNativeProductStopReason"], "Root.Shutdown")
                    && (bool)productState["audioNativeProductStopCompleted"], "Product Root.Shutdown completion was not recorded.");
                report["productStopEntries"] = productState["audioNativeProductStopEntries"];
                report["productStopReason"] = productState["audioNativeProductStopReason"];
                report["productStopCompleted"] = productState["audioNativeProductStopCompleted"];
            }
            report["statsAfterStop"] = after;
            Exception? laterError = null;
            try { invoke!(); } catch (Exception error) { laterError = error; }
            ulong[] later = stats();
            Check(later[7] == after[7] && later[3] == 0 && Outcome(laterError) == ordinaryOutcome,
                "A later actual entry re-entered the managed bridge or changed ordinary behavior.");
            report["statsAfterStoppedEntry"] = later;
            report["readerAfterRootShutdown"] = ReaderSnapshot();
            report["factoryBeforeSettlement"] = earlyFactory;
            report["workersJoined"] = true;
            report["inFlightStopPassed"] = true;
            report["noManagedBridgeCallsAfterStop"] = true;
            report["functionalPassed"] = ((string)report["failure"]).Length == 0;
        }
        catch (Exception error) { Fail(error); }
        finally
        {
            release.Set(); JoinWorkers(); observe?.Invoke(null);
            // Never settle or unregister a live reader on the product's behalf.
            // Only release our already terminal, no-longer-pending object.
            DisposeTerminalReader();
            Write();
        }
    }

    private static void DisposeTerminalReader()
    {
        if (reader == null || gate == null || pending == null) return;
        bool registered;
        lock (gate) registered = pending.ContainsKey(reader);
        var state = ReaderSnapshot();
        if (!registered && ((bool)state["nativeReady"] || (bool)state["terminallyFaulted"] || (bool)state["disposed"]))
        { reader.Dispose(); report!["ownedTerminalReaderDisposed"] = true; }
        else Fail(new InvalidOperationException("Product did not make the reader eligible for observer disposal."));
    }

    private static Dictionary<string, object> ReaderSnapshot() => (Dictionary<string, object>)reader!.GetType()
        .GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(reader, null)!;
    private static long Number(Dictionary<string, object> state, string key) => Convert.ToInt64(state[key]);
    private static void JoinWorkers()
    {
        if (callerStarted && !callerJoined) callerJoined = caller!.Join(Timeout);
        if (coordinatorStarted && !coordinatorJoined) coordinatorJoined = coordinator!.Join(Timeout);
    }
    private static string Outcome(Exception? error) => error == null ? "returned" : error.GetBaseException().GetType().FullName + ": " + error.GetBaseException().Message;
    private static MethodInfo Factory(MethodBase original)
    {
        Interlocked.Increment(ref factoryCalls);
        if (reader != null && !(bool)ReaderSnapshot()["nativeReady"]) Interlocked.Increment(ref earlyFactory);
        return typeof(C10ProductShutdownProbe).GetMethod(nameof(Prefix), Methods)!;
    }
    private static void Prefix() { }
    [MethodImpl(MethodImplOptions.NoInlining)] private static int ControlTarget() => 41;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int ShutdownTarget() => 42;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Fail(Exception error)
    {
        if (report == null) return;
        report["functionalPassed"] = false;
        report["failure"] = (string)report["failure"] + error + "\n";
    }
    private static void Write()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "c10-native-entry-probe.json"), C10NaturalAudioProbe.Json(report), new System.Text.UTF8Encoding(false));
    }
}

// A real remoting context, rather than a manually switched thread domain. The
// completed control qualifies restoration; unload may abort the later call
// after its protected entry bridge has returned.
[System.Runtime.Remoting.Contexts.Synchronization(System.Runtime.Remoting.Contexts.SynchronizationAttribute.REQUIRES_NEW)]
public sealed class C10DomainRemote : ContextBoundObject
{
    private static int factoryCalls;
    private static readonly BindingFlags Methods = BindingFlags.NonPublic | BindingFlags.Static;
    public override object InitializeLifetimeService() => null!;

    public Dictionary<string, object> Exercise(byte[] harmony, bool held)
    {
        int domain = AppDomain.CurrentDomain.Id;
        int context = Thread.CurrentContext.ContextID;
        Assembly copy = Assembly.Load(harmony);
        factoryCalls = 0;
        Action invoke = C10NativeEntryProbe.UpdateInvocation(copy,
            typeof(C10DomainRemote).GetMethod(held ? nameof(HeldTarget) : nameof(ControlTarget), Methods)!,
            typeof(C10DomainRemote).GetMethod(nameof(Factory), Methods)!, true);
        Exception? failure = null;
        try { invoke(); } catch (Exception error) { failure = error; }
        return new Dictionary<string, object>
        {
            ["domainBefore"] = domain, ["domainAfter"] = AppDomain.CurrentDomain.Id,
            ["contextBefore"] = context, ["contextAfter"] = Thread.CurrentContext.ContextID,
            ["factoryCalls"] = factoryCalls,
            ["ordinaryOutcome"] = failure == null ? "returned" : failure.GetBaseException().GetType().FullName + ": " + failure.GetBaseException().Message
        };
    }

    private static MethodInfo Factory(MethodBase original)
    { Interlocked.Increment(ref factoryCalls); return typeof(C10DomainRemote).GetMethod(nameof(Prefix), Methods)!; }
    private static void Prefix() { }
    [MethodImpl(MethodImplOptions.NoInlining)] private static int ControlTarget() => 51;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int HeldTarget() => 52;
}
