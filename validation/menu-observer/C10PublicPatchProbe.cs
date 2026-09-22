// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using NAudio.Wave;

namespace FixtureMenuObserver;

internal static class C10PublicPatchProbe
{
    private const string Owner = "local.fixture.c10-public-patch";
    private const string Input = @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\game\Mods\3457785624\Sounds\Things\LightSMG.mp3";
    private const string HarmonyInput = @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\game\Mods\2934420800\Assemblies\0Harmony.dll";
    private const BindingFlags Methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static Action? atFactory;
    private static int prefixCalls, factoryCalls;
    internal static bool Selected => Mode != null;
    private static string? Mode => Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--fixture-c10-public-patch-probe=", StringComparison.Ordinal))?.Split('=')[1];
    internal static string DirectoryPath => @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\profile\FixtureMenuObserver";

    internal static void Run(Assembly? core = null)
    {
        var report = new Dictionary<string, object> { ["schema"] = "c10-public-patch-functional.v1", ["mode"] = Mode!,
            ["functionalPassed"] = false, ["failure"] = "", ["enabled"] = core != null, ["performanceClaim"] = false };
        var harmony = new Harmony(Owner);
        MethodInfo target = typeof(Mp3FileReader).GetMethod("Read", new[] { typeof(byte[]), typeof(int), typeof(int) })!;
        MethodInfo prefix = typeof(C10PublicPatchProbe).GetMethod(nameof(Factory), Methods)!;
        bool patched = false;
        try
        {
            Require(ReferenceEquals(typeof(Harmony).Assembly, typeof(HarmonyMethod).Assembly), "Consumer Harmony types bind different libraries.");
            report["harmonyBinding"] = Identity(typeof(Harmony).Assembly);
            report["harmonyMethodBinding"] = Identity(typeof(HarmonyMethod).Assembly);
            report["assemblyNameResolution"] = Identity(Assembly.Load(typeof(Harmony).Assembly.FullName));
            report["target"] = target.ToString()!;
            report["targetAssembly"] = Identity(target.Module.Assembly);
            using var nativeSource = File.OpenRead(Input);
            using var native = new Mp3FileReader(nativeSource);
            string expected = Complete(native);
            report["nativeCompleteReadTrace"] = expected;

            MethodInfo? attach = core?.GetType("WakeUp.PreparedMp3FramesRuntime", true)!.GetMethod("Attach", Methods);
            MethodInfo? snapshot = core?.GetType("WakeUp.PreparedMp3FramesRuntime", true)!.GetMethod("Snapshot", Methods);
            string key = "C10 public patch " + Guid.NewGuid().ToString("N");
            if (core != null)
            {
                using var recordingSource = File.OpenRead(Input);
                using var recording = new Mp3FileReader(recordingSource);
                Require(attach!.Invoke(null, new object[] { recording, key }) != null, "Recording session was refused.");
                Require(Complete(recording) == expected, "Native recording changed complete output.");
                recording.Dispose();
                var recorded = (Dictionary<string, object>)snapshot!.Invoke(null, new object[] { recording })!;
                report["recording"] = recorded;
                Require((bool)recorded["published"], "Public patch control did not publish its native recording.");
            }

            using var source = File.OpenRead(Input);
            using var reader = new Mp3FileReader(source);
            object? session = core == null ? null : attach!.Invoke(null, new object[] { reader, key });
            Require(core == null || session != null, "Warm session was refused.");
            var trace = new MemoryStream();
            using var writer = new BinaryWriter(trace);
            var buffer = new byte[4096];
            Record(reader.Read(buffer, 0, buffer.Length));
            Dictionary<string, object>? registry = core == null ? null : (Dictionary<string, object>)((Dictionary<string, object>)AppDomain.CurrentDomain.GetData("WakeUp.PreparedAudio.Bootstrap.v1"))["audioRegistry"];
            Dictionary<string, object>? before = core == null ? null : State();
            if (before != null)
            {
                report["beforeMutation"] = before;
                Require(Number(before, "cachedFrames") > 0 && Number(before, "historyBytes") > 0,
                    "Public patch control has no real skipped MP3 work.");
                lock (registry!["audioRegistryGate"])
                    Require(((Dictionary<object, Func<int>>)registry["audioPending"]).ContainsKey(session!), "Real session is not registered.");
            }
            Action inspectSettlement = () =>
            {
                if (core == null) return;
                var after = State();
                report["atPatchFactory"] = after;
                lock (registry!["audioRegistryGate"])
                    Require(!((Dictionary<object, Func<int>>)registry["audioPending"]).ContainsKey(session!), "Factory ran before session removal.");
                Require(Number(after, "historyBytes") == 0 && Number(after, "replayFrames") == Number(before!, "cachedFrames")
                    && Equals(before!["retainedDecoderIdentity"], after["retainedDecoderIdentity"]) && !(bool)after["faulted"]
                    && (bool)registry["audioNativeOnly"], "Factory outran same-decoder settlement.");
            };
            atFactory = inspectSettlement;
            prefixCalls = factoryCalls = 0;
            Action install = () =>
            {
                MethodInfo wrapper = harmony.Patch(target, prefix: new HarmonyMethod(typeof(C10PublicPatchProbe), nameof(Factory)));
                patched = true;
                Require(wrapper != null && factoryCalls > 0, "Public Patch did not return an installed wrapper.");
                Require(Harmony.GetPatchInfo(target).Prefixes.Any(p => p.owner == Owner && p.PatchMethod == prefix), "Public patch publication is missing.");
                report["patchReturnedWrapper"] = wrapper!.ToString()!;
            };
            if (Mode == "same-file" && core != null)
                SameFilePending(registry!, session!, install, report);
            else install();
            int count;
            while ((count = reader.Read(buffer, 0, 1022)) != 0) Record(count);
            Record(0);
            string actual = Hash(trace.ToArray());
            report["patchedCompleteReadTrace"] = actual;
            report["prefixCalls"] = prefixCalls;
            report["factoryCalls"] = factoryCalls;
            Require(actual == expected && prefixCalls > 0, "Installed public prefix did not execute with complete native-equal output.");
            if (core != null) report["afterPatchedRead"] = State();
            atFactory = null;
            harmony.Unpatch(target, prefix);
            patched = false;
            Require(!Harmony.GetPatchInfo(target).Prefixes.Any(p => p.owner == Owner), "Public Unpatch left the owned prefix installed.");
            int callsBefore = prefixCalls;
            using var restoredSource = File.OpenRead(Input);
            using var restored = new Mp3FileReader(restoredSource);
            if (core != null)
            {
                Require(attach!.Invoke(null, new object[] { restored, key }) == null, "Later admission reopened after a foreign mutation.");
                report["laterAdmissionRefused"] = true;
            }
            Require(Complete(restored) == expected && prefixCalls == callsBefore, "Unpatch did not restore original behavior.");
            report["unpatchRestored"] = true;
            reader.Dispose();
            Require(source.CanRead, "Patch control closed borrowed source.");
            if (core == null) SameFileBaseline(report);
            report["functionalPassed"] = true;

            Dictionary<string, object> State() => (Dictionary<string, object>)snapshot!.Invoke(null, new object[] { reader })!;
            void Record(int returned) { writer.Write(returned); writer.Write(reader.Position); writer.Write(buffer, 0, returned); }
        }
        catch (Exception error) { report["failure"] = error.ToString(); }
        finally
        {
            atFactory = null;
            if (patched) try { harmony.Unpatch(target, prefix); } catch (Exception error) { report["cleanupFailure"] = error.ToString(); report["functionalPassed"] = false; }
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, "c10-public-patch-probe.json"), C10NaturalAudioProbe.Json(report), new System.Text.UTF8Encoding(false));
        }
        Require((bool)report["functionalPassed"], "Public patch functional control failed: " + report["failure"]);
    }

    private static void SameFileBaseline(Dictionary<string, object> report)
    {
        string file = CopyHarmony();
        Assembly first = Assembly.LoadFile(file), second = Assembly.LoadFile(file);
        Require(ReferenceEquals(first, second), "Baseline same-file reload changed assembly reference.");
        report["sameFileBaseline"] = new Dictionary<string, object> { ["returnedSameReference"] = true, ["assembly"] = Identity(first),
            ["consumerBindingUnchanged"] = ReferenceEquals(typeof(Harmony).Assembly, typeof(HarmonyMethod).Assembly) };
    }

    private static void SameFilePending(Dictionary<string, object> registry, object session, Action install, Dictionary<string, object> report)
    {
        const int timeout = 10000;
        object gate = registry["audioRegistryGate"];
        var pending = (Dictionary<object, Func<int>>)registry["audioPending"];
        var observe = (Action<Action<Assembly>?>)registry["audioHarmonyLoadObserve"];
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var secondNotification = new ManualResetEventSlim();
        Func<int> settle;
        int secondThread = 0, settled = 0, firstReturned = 0, secondReturned = 0;
        Assembly? first = null, second = null;
        Exception? firstError = null, secondError = null;
        string file = CopyHarmony();
        lock (gate)
        {
            settle = pending[session];
            pending[session] = () => { held.Set(); Require(release.Wait(timeout * 3), "Same-file coordinator did not release real settlement.");
                int result = settle(); Volatile.Write(ref settled, 1); return result; };
        }
        observe(a => { if (Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref secondThread)) secondNotification.Set(); });
        var loader = new Thread(() => { try { first = Assembly.LoadFile(file); Require(Volatile.Read(ref settled) == 1, "First load returned before settlement."); Volatile.Write(ref firstReturned, 1); }
            catch (Exception e) { firstError = e; } }) { IsBackground = true };
        var consumer = new Thread(() => { Volatile.Write(ref secondThread, Thread.CurrentThread.ManagedThreadId);
            try { second = Assembly.LoadFile(file); Require(Volatile.Read(ref settled) == 1, "Same-file reuse returned before settlement.");
                Volatile.Write(ref secondReturned, 1); install(); } catch (Exception e) { secondError = e; } }) { IsBackground = true };
        bool started = false, joined = false;
        loader.Start();
        try
        {
            Require(held.Wait(timeout), "New file load did not reach actual MP3 settlement.");
            consumer.Start(); started = true;
            Require(secondNotification.Wait(timeout), "Same-file call did not enter repeated load notification.");
            Require(Volatile.Read(ref firstReturned) == 0 && Volatile.Read(ref secondReturned) == 0 && factoryCalls == 0 && Volatile.Read(ref settled) == 0,
                "Same-file call or patch factory outran held settlement.");
            release.Set();
            bool firstJoined = loader.Join(timeout), secondJoined = consumer.Join(timeout);
            joined = firstJoined && secondJoined;
            Require(joined && firstError == null && secondError == null, "Same-file workers failed: " + firstError + " / " + secondError);
            Require(ReferenceEquals(first, second) && firstReturned == 1 && secondReturned == 1, "Same-file reference or return changed.");
            report["sameFilePending"] = new Dictionary<string, object> { ["secondNotificationBeforeRelease"] = true,
                ["neitherLoadReturnedBeforeSettlement"] = true, ["factoryAfterSettlement"] = true,
                ["sameAssemblyReference"] = true, ["assembly"] = Identity(first!), ["workersJoined"] = true,
                ["interpretation"] = "Repeated synchronous load notification waits for the owned registry; this is not evidence of a Mono loader-lock wait or the narrower losing-publication race." };
        }
        finally
        {
            release.Set();
            if (!joined) { bool firstJoined = loader.Join(timeout), secondJoined = !started || consumer.Join(timeout); joined = firstJoined && secondJoined; }
            observe(null);
            if (joined) lock (gate) { if (pending.ContainsKey(session)) pending[session] = settle; }
            Require(joined, "Same-file worker remains after final release.");
        }
    }

    private static string CopyHarmony()
    {
        Directory.CreateDirectory(DirectoryPath);
        string file = Path.Combine(DirectoryPath, "public-patch-original-Harmony.dll");
        File.Copy(HarmonyInput, file, false);
        return Path.GetFullPath(file);
    }
    private static Dictionary<string, object> Identity(Assembly assembly) => new Dictionary<string, object> {
        ["fullName"] = assembly.FullName, ["mvid"] = assembly.ManifestModule.ModuleVersionId.ToString(),
        ["location"] = assembly.Location, ["reflectionOnly"] = assembly.ReflectionOnly };
    private static MethodInfo Factory(MethodBase original) { atFactory?.Invoke(); Interlocked.Increment(ref factoryCalls); return typeof(C10PublicPatchProbe).GetMethod(nameof(Prefix), Methods)!; }
    private static void Prefix() => Interlocked.Increment(ref prefixCalls);
    private static string Complete(Mp3FileReader reader)
    {
        using var trace = new MemoryStream(); using var writer = new BinaryWriter(trace);
        var buffer = new byte[4096]; int count = reader.Read(buffer, 0, buffer.Length); Record(count);
        while ((count = reader.Read(buffer, 0, 1022)) != 0) Record(count); Record(0);
        return Hash(trace.ToArray());
        void Record(int returned) { writer.Write(returned); writer.Write(reader.Position); writer.Write(buffer, 0, returned); }
    }
    private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    private static long Number(Dictionary<string, object> state, string key) => Convert.ToInt64(state[key]);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
