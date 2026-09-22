// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Only the exact filesystem selection is moved ahead. Native mod iterators,
// constructors, XML parsing and publication remain on their loading thread.
public static class OrderedInputRuntime
{
    internal const string SelectorBody = "4EE926A900CE426B8526BD803EF09406423A77C16CD5B20DD49CDC33696E2A37";
    internal const string Owner = "wakeup.ordered-input";
    internal static string Status { get; private set; } = "Ordered concurrent XML input is off.";
    private static bool enabled, loading, attempted, refusalRecorded;
    private static string? log;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static Func<ModContentPack, string, List<string>?, List<FileInfo>>? select;
    private static readonly FieldInfo Defs = AccessTools.Field(typeof(ModContentPack), "defs");
    private static OrderedInputQueue? queue;
    internal static bool HasQueue => queue != null;
    private static readonly Dictionary<ModContentPack, List<FileInfo>> selections = new();
    private static readonly Dictionary<FileInfo, int> indices = new();
    private static readonly List<string> sourceMods = new();
    private static readonly Dictionary<MethodInfo, PublishedPatchGuard> diagnosticCallbacks = new();
    [ThreadStatic] private static OrderedInputQueue.Snapshot? staged;
    private static int consumed, batches;
    private static readonly HashSet<int> consumedIndices = new();
    private static int currentIndex = -1;

    internal static bool Selected(IReadOnlyList<string> args, CacheLaunchPolicy policy)
        => StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
            && args.Count(a => a.StartsWith("--wake-up-ordered-input=", StringComparison.Ordinal)) == 1
            && args.Contains("--wake-up-ordered-input=on")
            && policy.Action != CacheAction.Bypass && policy.Action != CacheAction.Clear;

    internal static void Initialize(string[] args, string root)
    {
        log = Path.Combine(root, "WakeUp", "ordered-input.jsonl");
        if (!Selected(args, CacheLaunchPolicy.Current)) return;
        try
        {
            if (PlayDataLoader.Loaded || !RuntimeIdentity.ValidateBinaryIdentity(out _))
                throw new InvalidOperationException("startup input contract unavailable");
            guards = ParsedXmlRuntime.CreateGuards().Concat(CreateIntervalGuards()).ToArray();
            enabled = true;
            Status = "Ordered concurrent XML input selected; waiting for native filesystem sources.";
            JsonLineLog.WriteEvent(log, "selected");
        }
        catch (Exception e) { Status = "Ordered input refused: " + e.Message + ". Native loading remains."; }
        Log.Message("[Wake-Up] " + Status);
    }

    internal static IEnumerable<PublishedPatchGuard> CreateIntervalGuards()
    {
        // These routines lie between selecting a future source and consuming it.
        // A foreign callback must never run while its future sources are leased.
        var methods = new List<MethodBase> { Selector() };
        var bodies = new[] {
            "69A389BC972D545C9FA37DFF482447BADAA0F02172E4C3DC160E8799E74885E4",
            "95A4290A89B76574F98DDCA6921421856715BEF0F28BE70DAF8D0F3E945E534B",
            "D5A2D6791443A2A024D42A1D9521286E7600736F0D089AFD25F74A163D6CF3D9",
            "7CA0EAA9A0AE339A5FCC0CA128B475D9972E20240B8A411410DACFCBAE66B8AC",
            "A5EA33462A1CD14B06CCAEB70F0ADDD69417F3716160DBD836D29F47C1C29648",
            "17C58814F47FBF4EB78FD6813765AE1259BE073922A1D79AFFF84736C9413F02",
            "36BD002AAFA158737893BE223194DD2B5CD9A506E24E7E8FA297F3E760692EB8",
            "AE8D70269C0EE5D5A1ACE8F1F94CF4685ECB9425294A5C8E6E342AF304498E6E" };
        int at = 0;
        foreach (var pair in new[] {
            (typeof(LoadedModManager), "get_RunningModsListForReading"),
            (typeof(ModContentPack), "ToString"), (typeof(ModContentPack), "get_PackageId"),
            (typeof(ModContentPack), "get_PackageIdPlayerFacing"), (typeof(ModContentPack), "get_RootDir"),
            (typeof(DeepProfiler), "Start"), (typeof(DeepProfiler), "End"),
            (typeof(Prefs), "get_LogVerbose") })
        {
            var method = AccessTools.Method(pair.Item1, pair.Item2) ?? throw new InvalidOperationException("missing " + pair.Item2);
            if (!SemanticMethodIdentity.TryHash(method, out string hash, out _) || hash != bodies[at++])
                throw new InvalidOperationException("native source interval changed: " + pair.Item2);
            methods.Add(method);
        }
        // Selection and private read/hash operations must not invoke custom providers.
        methods.AddRange(typeof(Log).GetMethods().Where(m => m.Name == "Warning" || m.Name == "Error" || m.Name == "ErrorOnce"));
        methods.AddRange(typeof(FileSystemInfo).GetMethods().Where(m => m.Name == "get_Exists" || m.Name == "get_Name" || m.Name == "get_FullName" || m.Name == "Refresh"));
        methods.Add(typeof(DirectoryInfo).GetConstructor(new[] { typeof(string) })!);
        methods.AddRange(typeof(FileStream).GetConstructors());
        methods.AddRange(typeof(FileStream).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == "Read" || m.Name == "ReadByte" || m.Name == "get_Name" || m.Name == "get_Exists" || m.Name == "get_Length" || m.Name == "Dispose"));
        foreach (string name in new[] { "GetAttributes", "Exists" }) methods.Add(AccessTools.Method(typeof(File), name));
        methods.Add(AccessTools.Method(typeof(Directory), "Exists"));
        foreach (string name in new[] { "GetFullPath", "GetDirectoryName" }) methods.Add(typeof(Path).GetMethod(name, new[] { typeof(string) })!);
        methods.Add(typeof(SHA256Managed).GetConstructor(Type.EmptyTypes)!);
        methods.AddRange(typeof(SHA256Managed).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(m => m.Name == "ComputeHash" || m.Name == "HashCore" || m.Name == "HashFinal" || m.Name == "Initialize" || m.Name == "Dispose"));
        methods.Add(typeof(Path).GetMethod("Combine", new[] { typeof(string), typeof(string) })!);
        methods.AddRange(typeof(DirectoryInfo).GetMethods().Where(m => m.Name == "GetFiles" || m.Name == "get_Name" || m.Name == "get_Exists" || m.Name == "get_FullName"));
        methods.AddRange(typeof(FileInfo).GetMethods().Where(m => m.Name == "get_Name" || m.Name == "get_Exists" || m.Name == "get_Length" || m.Name == "get_FullName" || m.Name == "get_Directory"));
        foreach (var method in methods.Distinct())
        {
            if (!PublishedPatchGuard.TryCreate(method, Owner, out var guard, true, p => AllowsIntervalPatch(method, p)))
                throw new InvalidOperationException("source interval guard unavailable");
            yield return guard!;
        }
    }

    private static bool AllowsIntervalPatch(MethodBase method, Patch patch)
    {
        if (LoadingObservationRuntime.AllowsHook(method, patch)) return true;
        if (PrepatcherXmlContract.AllowsInputPatch(method, patch)) return true;
        // Optional settings-framework prefix only returns !suppressErrorMessages.
        // Preserve its native call and current flag; no supplier is required.
        var callback = patch.PatchMethod;
        if (method.DeclaringType == typeof(Log) && method.Name == "Error" && patch.owner == "ModSettingsFrameworkMod"
            && callback.DeclaringType?.FullName == "ModSettingsFramework.Log_Error_Patch" && callback.Name == "Prefix"
            && callback.IsStatic && callback.ReturnType == typeof(bool) && callback.GetParameters().Length == 0)
        {
            if (!diagnosticCallbacks.TryGetValue(callback, out var guard))
            {
                if (!SemanticMethodIdentity.TryHash(callback, out string hash, out _)
                    || hash != "5F3872B9B4F314B91E957DED8A77B3393EA60151F86A5AA3C29045DE805F1717"
                    || !PublishedPatchGuard.TryCreate(callback, Owner, out guard, true)) return false;
                diagnosticCallbacks.Add(callback, guard!);
            }
            return guard!.AllowsOriginalContract();
        }
        // Existing C02 diagnostics only invalidate their private stage session;
        // that session does not exist during source loading. Callbacks stay native.
        return method.DeclaringType == typeof(Log) &&
            ((patch.owner == ProcessedXmlRuntime.Owner && patch.PatchMethod == AccessTools.Method(typeof(ProcessedXmlRuntime), "ObserveLog"))
             || (patch.owner == ResolvedInheritanceRuntime.Owner && patch.PatchMethod == AccessTools.Method(typeof(ResolvedInheritanceRuntime), "ObserveDiagnostic")));
    }

    private static MethodInfo Selector()
        => AccessTools.Method(typeof(DirectXmlLoader), "WakeUpSelectXmlFiles")
            ?? throw new InvalidOperationException("native selected-file extraction unavailable");
    private static List<FileInfo> NativeSelect(ModContentPack mod, string folder, List<string>? debug)
    {
        select ??= (Func<ModContentPack, string, List<string>?, List<FileInfo>>)Delegate.CreateDelegate(
            typeof(Func<ModContentPack, string, List<string>?, List<FileInfo>>), Selector());
        return select(mod, folder, debug);
    }
    internal static bool IntervalCallbacksAllowed() => PrepatcherXmlContract.AllowsCallbacks()
        && diagnosticCallbacks.Values.All(g => g.AllowsOriginalContract()) && !(DeepProfiler.enabled && Prefs.LogVerbose);

    private static bool Admitted()
    {
        var refused = guards.FirstOrDefault(g => !g.AllowsOriginalContract());
        bool allowed = guards.Length != 0 && refused == null && IntervalCallbacksAllowed();
        if (!allowed && loading && !refusalRecorded)
        {
            refusalRecorded = true;
            string reason = refused == null ? "source-interval-unavailable" : refused.Target.DeclaringType?.FullName + "." + refused.Target.Name;
            Status = "Ordered input uses native loading: " + reason;
            JsonLineLog.WriteReceipt(log, "native-interval", reason);
            if (refused != null)
            {
                var hooks = Harmony.GetPatchInfo(refused.Target);
                if (hooks != null)
                    foreach (var patch in hooks.Prefixes.Concat(hooks.Postfixes).Concat(hooks.Transpilers).Concat(hooks.Finalizers).Concat(hooks.InnerPrefixes).Concat(hooks.InnerPostfixes))
                    {
                        SemanticMethodIdentity.TryHash(patch.PatchMethod, out string body, out _);
                        JsonLineLog.WriteEvent(log, "source-hook", "\"owner\":" + JsonLineLog.Quote(patch.owner)
                            + ",\"method\":" + JsonLineLog.Quote(patch.PatchMethod.DeclaringType?.FullName + "." + patch.PatchMethod.Name)
                            + ",\"body\":" + JsonLineLog.Quote(body));
                    }
            }
        }
        return allowed;
    }

    internal static void Begin(bool hotReload)
    {
        Stop("previous-scope");
        loading = enabled && !hotReload; attempted = false; consumed = batches = 0; consumedIndices.Clear(); refusalRecorded = false;
    }
    internal static void End() { Stop("source-stage-ended"); loading = false; }

    public static List<FileInfo> SelectFiles(ModContentPack mod, string folder, List<string>? debug)
    {
        if (!loading || folder != "Defs/" || debug != null) { Stop("unsupported-selection"); return NativeSelect(mod, folder, debug); }
        if (!Admitted()) { Stop("source-observer-changed"); return NativeSelect(mod, folder, debug); }
        if (selections.TryGetValue(mod, out var ready)) { selections.Remove(mod); return ready; }
        if (attempted) { Stop("outside-selected-interval"); return NativeSelect(mod, folder, debug); }
        var current = NativeSelect(mod, folder, debug);
        attempted = true;
        try
        {
            var mods = LoadedModManager.RunningModsListForReading;
            int first = mods.IndexOf(mod);
            if (first < 0 || mods.Count > 512) return current;
            var files = new List<FileInfo>();
            for (int at = first; at < mods.Count; at++)
            {
                var next = mods[at];
                // An already populated mod logs a native diagnostic BEFORE its
                // selector. End lookahead before that observable boundary.
                if (((System.Collections.ICollection)Defs.GetValue(next)).Count != 0) break;
                List<FileInfo> selected;
                try { selected = at == first ? current : NativeSelect(next, folder, null!); }
                catch { break; } // The original selector raises it at its native position.
                if (files.Count + selected.Count > 32768) break;
                if (at != first) selections.Add(next, selected);
                foreach (var file in selected)
                {
                    indices.Add(file, files.Count); files.Add(file); sourceMods.Add(next.PackageId);
                }
            }
            if (files.Count == 0) return current;
            if (!Admitted()) { Stop("changed-before-scheduling"); return current; }
            var groups = sourceMods.Distinct().Select((name, i) => new { name, i }).ToDictionary(x => x.name, x => x.i);
            queue = new OrderedInputQueue(files, sourceGroups: sourceMods.Select(name => groups[name]).ToArray());
        }
        catch { Stop("selection-preparation-failed"); }
        return current;
    }

    internal static OrderedInputQueue.Snapshot? Take(FileInfo file)
    {
        if (queue == null) return null;
        if (!Admitted() || !indices.TryGetValue(file, out int index)) { Stop("unsupported-consumer"); return null; }
        var snapshot = queue.Take(index);
        if (snapshot == null) { Stop("source-read-failed"); return null; }
        indices.Remove(file); currentIndex = index; return snapshot;
    }

    // Called only when parsed reuse did not take this whole selected batch.
    internal static bool TryLoadSelected(ModContentPack mod, List<FileInfo> files, out LoadableXmlAsset[] assets)
    {
        assets = null!;
        if (queue == null || files.Count != 0 && !indices.ContainsKey(files[0])) return false;
        assets = new LoadableXmlAsset[files.Count]; batches++;
        // Beyond the first constructor there is no batch retry: observable native
        // effects can occur. A failed slot cancels ahead work, then runs natively.
        for (int i = 0; i < files.Count; i++)
        {
            using var snapshot = Take(files[i]);
            try { staged = snapshot; assets[i] = new LoadableXmlAsset(files[i], mod); }
            catch { snapshot?.Dispose(); Stop("native-constructor-failed"); throw; }
            finally { staged = null; }
        }
        return true;
    }

    internal static bool TryRead(FileInfo file, XmlReaderSettings settings, out XmlDocument document)
    {
        document = null!;
        var input = staged;
        if (input == null || !ReferenceEquals(file, input.File)) return false;
        try
        {
            if (!Admitted() || !ParsedXmlRuntime.NativeSettings(settings)) throw new InvalidOperationException();
            document = ParsedXmlFormat.Parse(input.Bytes, settings);
            ConsumedSnapshot(); return true;
        }
        catch
        {
            // Release ALL leases before native error handling can invoke hooks.
            input.Dispose(); staged = null; Stop("native-parse-fallback"); return false;
        }
    }
    internal static void ConsumedSnapshot() { consumed++; consumedIndices.Add(currentIndex); }

    internal static void Stop(string reason)
    {
        var old = queue; queue = null;
        if (old != null)
        {
            old.Dispose();
            var pairs = old.CrossGroupOverlaps.Where(p => consumedIndices.Contains(p.Item1) && consumedIndices.Contains(p.Item2)).ToArray();
            foreach (var pair in pairs)
                JsonLineLog.WriteEvent(log, "consumed-cross-mod-overlap", "\"first\":" + pair.Item1 + ",\"second\":" + pair.Item2
                    + ",\"firstMod\":" + JsonLineLog.Quote(sourceMods[pair.Item1]) + ",\"secondMod\":" + JsonLineLog.Quote(sourceMods[pair.Item2]));
            Status = "Ordered input completed: " + consumed + " consumed files; " + reason + ". Performance pending.";
            JsonLineLog.WriteEvent(log, "completed", "\"reason\":" + JsonLineLog.Quote(reason)
                + ",\"consumed\":" + consumed + ",\"batches\":" + batches
                + ",\"scheduled\":" + old.Scheduled + ",\"completed\":" + old.Completed
                + ",\"maximumActive\":" + old.MaximumActive + ",\"peakReservedBytes\":" + old.PeakReservedBytes
                + ",\"peakOutstanding\":" + old.PeakOutstanding
                + ",\"consumedCrossModPairs\":" + pairs.Length + ",\"crossModOverlaps\":" + old.CrossGroupOverlap
                + ",\"remainingBytes\":" + old.ReservedBytes + ",\"remainingJobs\":" + old.Outstanding
                + ",\"workersAlive\":" + (old.WorkersAlive ? "true" : "false"));
        }
        selections.Clear(); indices.Clear(); sourceMods.Clear();
    }
}
