// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NAudio.Wave;

namespace FixtureMenuObserver;

internal static class C10Mp3MathProbe
{
    private const string Library = "mono-profiler-wakeupentry";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_test_begin(int fma, uint csr, [Out] ulong[] previous, uint count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_test_change(int fma, uint csr);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_test_end();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_test_unwind([Out] ulong[] receipt, uint count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_test_aba(int enabled);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_math_test_aba_report([Out] ulong[] receipt, uint count);

    internal static void PreserveEnvironment(Action action)
    {
        Require(wakeup_acm_math_test_begin(-1, uint.MaxValue, new ulong[2], 2) == 1, "Cache-damage control could not preserve native caller state.");
        try { action(); }
        finally { Require(wakeup_acm_math_test_end() == 1, "Cache-damage control failed to restore native caller state."); }
    }

    internal static void Run(Assembly core, string path)
    {
        if (!C10Mp3CorrectionProbe.Selected) return;
        var rows = new List<object>();
        C10NativeMp3Probe.Report["controlledNativeMath"] = rows;
        Type runtime = core.GetType("WakeUp.PreparedMp3FramesRuntime", true)!;
        MethodInfo attach = runtime.GetMethod("Attach", BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo snapshot = runtime.GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)!;
        var unwind = new ulong[4];
        Require(wakeup_acm_math_test_unwind(unwind, 4) == 1, "Native complete floating-point restoration failed exceptional-exit control.");
        rows.Add(new Dictionary<string, object> { ["nativeExceptionalRestoration"] = unwind });
        string fixedKey = "C10 fixed math " + Guid.NewGuid().ToString("N");
        var nativeFixed = Decode(null, false, false);
        var coldFixed = Decode(fixedKey, false, false);
        Require(Equals(nativeFixed["samples"], coldFixed["samples"]) && Flag(coldFixed, "published"), "Fixed native arithmetic recording failed.");
        var changedSelector = Decode(fixedKey, true, false);
        var nativeSelector = Decode(null, true, false);
        Require(Equals(changedSelector["samples"], nativeSelector["samples"]) && Count(changedSelector, "cachedFrames") > 0
            && Count(changedSelector, "replayFrames") == 0, "Changed global arithmetic selector invalidated branch-independent cached output.");
        var nativeRounding = Decode(null, true, true);
        var reusedRounding = Decode(fixedKey, true, true);
        Require(Equals(nativeRounding["samples"], reusedRounding["samples"]) && Count(reusedRounding, "cachedFrames") > 0
            && Count(reusedRounding, "replayFrames") > 0, "Historical conversion under old controls did not preserve current native output.");
        string mixedKey = "C10 mixed math " + Guid.NewGuid().ToString("N");
        var coldMixed = Decode(mixedKey, true, true);
        Require(Equals(coldMixed["samples"], nativeRounding["samples"]), "Mixed-context native recording changed output.");
        var warmMixed = Decode(mixedKey, true, true);
        Require(Equals(warmMixed["samples"], nativeRounding["samples"]), "Mixed-context replay changed output.");
        Require(Flag(coldMixed, "published") ? Count(warmMixed, "cachedFrames") > 0
            : !Flag(warmMixed, "published") && Count(warmMixed, "cachedFrames") == 0,
            "Mixed-context provenance neither qualified each frame nor refused the incomplete transcript.");
        string abaKey = "C10 ABA math " + Guid.NewGuid().ToString("N");
        var coldAba = Decode(abaKey, false, false, true);
        var aba = (ulong[])coldAba["aba"];
        Require(Equals(coldAba["samples"], nativeFixed["samples"]) && Flag(coldAba, "published")
            && aba[2] > 0 && aba[3] > 0 && aba[5] == 0, "Actual changing-and-restored native math branches were not certified.");
        var warmAba = Decode(abaKey, false, false);
        Require(Equals(warmAba["samples"], nativeFixed["samples"]) && Count(warmAba, "cachedFrames") > 0
            && Count(warmAba, "replayFrames") == 0, "ABA-recorded native frames were not reusable.");
        var mutation = Decode(fixedKey, true, true, mutate: true);
        Require(Equals(mutation["samples"], nativeRounding["samples"]) && Count(mutation, "cachedFrames") > 0
            && Count(mutation, "replayFrames") > 0 && (bool)((Dictionary<string, object>)mutation["afterMutation"])["nativeOnly"],
            "Relevant runtime mutation did not settle historical MP3 state and preserve the original consumer output.");

        Dictionary<string, object> Decode(string? key, bool changeFma, bool changeRounding, bool aba = false, bool mutate = false)
        {
            var row = new Dictionary<string, object> { ["attached"] = key != null, ["changeFma"] = changeFma, ["changeRounding"] = changeRounding };
            rows.Add(row);
            var previous = new ulong[2];
            Require(wakeup_acm_math_test_begin(1, 0x1f80, previous, 2) == 1, "Disposable-process arithmetic control could not begin.");
            row["originalSelectorAndCsr"] = previous;
            Mp3FileReader? reader = null;
            try
            {
                if (aba) Require(wakeup_acm_math_test_aba(1) == 1, "Disposable ABA control could not arm.");
                using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                reader = new Mp3FileReader(source);
                if (key != null) Require(attach.Invoke(null, new object[] { reader, key }) != null, "Arithmetic control reader was not admitted.");
                using var output = new MemoryStream();
                var buffer = new byte[4608];
                int count = reader.Read(buffer, 0, buffer.Length);
                output.Write(buffer, 0, count);
                if (key != null) row["beforeChange"] = snapshot.Invoke(null, new object[] { reader })!;
                if (changeFma || changeRounding)
                    Require(wakeup_acm_math_test_change(changeFma ? 0 : -1, changeRounding ? 0x3fa0u : uint.MaxValue) == 1,
                        "Disposable-process arithmetic setting change failed.");
                if (mutate)
                {
                    var bootstrap = (Dictionary<string, object>)AppDomain.CurrentDomain.GetData("WakeUp.PreparedAudio.Bootstrap.v1");
                    var original = (Assembly)bootstrap["oldHarmony"];
                    Assembly.Load(File.ReadAllBytes(original.Location));
                    row["afterMutation"] = core.GetType("WakeUp.PreparedAudioRuntime", true)!.GetMethod("Snapshot")!.Invoke(null, null)!;
                    row["framesAfterMutation"] = snapshot.Invoke(null, new object[] { reader })!;
                }
                while ((count = reader.Read(buffer, 0, 1022)) != 0) output.Write(buffer, 0, count);
                reader.Dispose();
                Require(source.CanRead, "Arithmetic control disposed borrowed native source.");
                using var hash = SHA256.Create();
                row["samples"] = BitConverter.ToString(hash.ComputeHash(output.ToArray())).Replace("-", "");
                if (key != null) row["frames"] = snapshot.Invoke(null, new object[] { reader })!;
                if (aba)
                {
                    var receipt = new ulong[8];
                    Require(wakeup_acm_math_test_aba_report(receipt, 8) == 1, "ABA native receipt is absent.");
                    row["aba"] = receipt;
                }
            }
            finally
            {
                reader?.Dispose();
                Require(wakeup_acm_math_test_end() == 1, "Disposable-process arithmetic control failed to restore original settings and full environment.");
                row["settingsRestored"] = true;
            }
            return row;
        }
    }
    private static Dictionary<string, object> Frames(Dictionary<string, object> row) => (Dictionary<string, object>)row["frames"];
    private static bool Flag(Dictionary<string, object> row, string key) => (bool)Frames(row)[key];
    private static long Count(Dictionary<string, object> row, string key) => Convert.ToInt64(Frames(row)[key]);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
