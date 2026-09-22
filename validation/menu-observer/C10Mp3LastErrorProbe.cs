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

internal static class C10Mp3LastErrorProbe
{
    private const BindingFlags Methods = BindingFlags.Static | BindingFlags.NonPublic;
    [DllImport("mono-profiler-wakeupentry", CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_last_error_test_begin(uint sentinel);
    [DllImport("mono-profiler-wakeupentry", CallingConvention = CallingConvention.Cdecl)]
    private static extern int wakeup_acm_last_error_test_end([Out] ulong[] receipt, uint count);

    internal static void Run(Assembly core, string path)
    {
        Type frames = core.GetType("WakeUp.PreparedMp3FramesRuntime", true)!;
        MethodInfo attach = frames.GetMethod("Attach", Methods)!;
        MethodInfo snapshot = frames.GetMethod("Snapshot", Methods)!;
        var rows = new List<object>();
        C10NativeMp3Probe.Report["lastErrorControl"] = rows;
        var zero = Decode(null, 0, false);
        var other = Decode(null, 487, false);
        Require(Equals(zero["samplesAndReads"], other["samplesAndReads"]), "Original MP3 output depends on incoming LastError.");
        foreach (uint recorded in new uint[] { 0, 487 })
        {
            string key = "C10 LastError " + Guid.NewGuid().ToString("N");
            var cold = Decode(key, recorded, false);
            Require((bool)State(cold)["published"], "Sentinel native recording did not publish.");
            uint current = recorded == 0 ? 487u : 0u;
            var warm = Decode(key, current, false);
            Require(Number(State(warm), "cachedFrames") > 0 && Number(State(warm), "nativeFrames") == 1
                && Number(State(warm), "replayFrames") == 0, "Cross-sentinel read did not consume the complete frame cache.");
            var caughtUp = Decode(key, current, true);
            Require(Number(State(caughtUp), "cachedFrames") > 0 && Number(State(caughtUp), "replayFrames") > 0,
                "Cross-sentinel catch-up did not reconstruct skipped frames.");
            foreach (var row in new[] { cold, warm, caughtUp })
                Require(Equals(zero["samplesAndReads"], row["samplesAndReads"]), "Cross-sentinel cached/native output differs from original.");
        }

        Dictionary<string, object> Decode(string? key, uint sentinel, bool catchUp)
        {
            var row = new Dictionary<string, object> { ["attached"] = key != null, ["sentinel"] = sentinel, ["catchUp"] = catchUp };
            rows.Add(row);
            Mp3FileReader? reader = null;
            Require(wakeup_acm_last_error_test_begin(sentinel) == 1, "Native LastError sentinel control did not begin.");
            try
            {
                using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                reader = new Mp3FileReader(source);
                object? session = key == null ? null : attach.Invoke(null, new object[] { reader, key });
                Require(key == null || session != null, "Sentinel reader was not admitted.");
                using var output = new MemoryStream();
                using var trace = new BinaryWriter(output);
                var buffer = new byte[4096];
                int count = reader.Read(buffer, 0, buffer.Length);
                Record(count);
                if (catchUp)
                {
                    var before = (Dictionary<string, object>)snapshot.Invoke(null, new object[] { reader })!;
                    Require(Number(before, "cachedFrames") > 0 && Number(before, "historyBytes") > 0, "No skipped history before sentinel catch-up.");
                    Require((int)session!.GetType().GetMethod("Settle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null)! == 1,
                        "Same-decoder sentinel settlement failed.");
                    var after = (Dictionary<string, object>)snapshot.Invoke(null, new object[] { reader })!;
                    Require(Equals(before["retainedDecoderIdentity"], after["retainedDecoderIdentity"])
                        && Number(after, "replayFrames") == Number(before, "cachedFrames") && Number(after, "historyBytes") == 0
                        && !(bool)after["faulted"], "Sentinel settlement replaced or failed the retained decoder.");
                    row["beforeCatchUp"] = before; row["afterCatchUp"] = after;
                }
                while ((count = reader.Read(buffer, 0, 1022)) != 0) Record(count);
                Record(0);
                reader.Dispose();
                Require(source.CanRead, "Sentinel control closed borrowed input.");
                using var hash = SHA256.Create();
                row["samplesAndReads"] = BitConverter.ToString(hash.ComputeHash(output.ToArray())).Replace("-", "");
                if (key != null) row["frames"] = snapshot.Invoke(null, new object[] { reader })!;
                void Record(int returned) { trace.Write(returned); trace.Write(reader.Position); trace.Write(buffer, 0, returned); }
            }
            finally
            {
                reader?.Dispose();
                var receipt = new ulong[9];
                Require(wakeup_acm_last_error_test_end(receipt, 9) == 1, "Native sentinel control failed outer restoration.");
                row["nativeBoundaryReceipt"] = receipt;
                Require(receipt[0] == 1 && receipt[1] == sentinel && receipt[2] > 0 && receipt[7] == 0 && receipt[8] == 1
                    && receipt[6] == receipt[2] + receipt[3] + receipt[4], "Sentinel was not preserved at every tested native boundary.");
                if (catchUp) Require(receipt[3] > 0 && receipt[5] > 0, "Native receipt lacks actual cached consumption or replay.");
            }
            return row;
        }
    }
    private static Dictionary<string, object> State(Dictionary<string, object> row) => (Dictionary<string, object>)row["frames"];
    private static long Number(Dictionary<string, object> row, string key) => Convert.ToInt64(row[key]);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
