// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HarmonyLib;
using RimWorld.IO;
using Verse;

namespace WakeUp;

// Native discovery, read barriers and side effects remain in LoadData. Only its
// two parser loops and the successful Strings read-to-parse interval are reused.
public static class ParsedLanguageRuntime
{
    internal const string Owner = "wakeup.parsed-language";
    internal static string Status { get; private set; } = "Parsed language reuse is off.";
    private static string? root, log;
    private static bool enabled, installed;
    private static Session? activeSession;
    private static int ownerThread;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static PublishedPatchGuard[] injectionNameGuards = Array.Empty<PublishedPatchGuard>();
    [ThreadStatic] private static Session? current;
    private static readonly MethodInfo KeyedMethod = AccessTools.Method(typeof(LoadedLanguage), "LoadFromFile_Keyed");
    private static readonly Action<LoadedLanguage, VirtualFile, string> NativeKeyed =
        AccessTools.MethodDelegate<Action<LoadedLanguage, VirtualFile, string>>(KeyedMethod);
    private sealed class Session : IDisposable
    {
        internal readonly LoadedLanguage Language;
        internal readonly OwnedCacheStore Store;
        internal readonly string Context;
        internal readonly ParsedLanguageKeyBuffer KeyBuffer = new();
        internal readonly ParsedLanguageConsumers Consumers = new();
        internal readonly Dictionary<Type, Action> LookupValidators = new();
        private readonly string bundleKey;
        private readonly Dictionary<string, byte[]> available;
        private readonly Dictionary<string, byte[]> consumed = new(StringComparer.Ordinal);
        private long consumedBytes = 8;
        private bool completed, overflow;
        internal bool Invalid;
        internal bool InjectionRefusalRecorded;
        internal int KeyedHits, InjectionHits, StringsHits, NativeFiles, Published, NativeInsertions;
        internal Session(LoadedLanguage language, OwnedCacheStore store, string context)
        {
            Language = language; Store = store; Context = context;
            bundleKey = Digest(Encoding.UTF8.GetBytes("parsed-language-bundle-v1:" + context));
            available = Store.Read(bundleKey, ParsedLanguageBundle.Decode) ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }
        internal T? Read<T>(string key, Func<byte[], T> decode) where T : class
        {
            if (!available.TryGetValue(key, out byte[] payload)) return null;
            T ready = decode(payload); Retain(key, payload); return ready;
        }
        internal bool Retain(string key, byte[] payload)
        {
            if (overflow) return false;
            long next = consumedBytes + 68L + payload.Length - (consumed.TryGetValue(key, out byte[] old) ? 68L + old.Length : 0);
            if (payload.Length > ParsedLanguageFormat.MaximumPayload || next > ParsedLanguageBundle.MaximumPayload
                || !consumed.ContainsKey(key) && consumed.Count >= ParsedLanguageBundle.MaximumGroups)
            { overflow = true; consumed.Clear(); return false; }
            consumed[key] = payload; consumedBytes = next; return true;
        }
        internal void Complete()
        {
            if (completed) return;
            completed = true;
            try
            {
                if (Published != 0 && (Invalid || overflow || !Store.Publish(bundleKey, ParsedLanguageBundle.Encode(consumed)))) Published = 0;
                Store.Complete();
            }
            catch (Exception e) { Published = 0; JsonLineLog.WriteReceipt(log, "completion-failed", e.Message); }
        }
        internal void Invalidate() { Invalid = true; KeyBuffer.DiscardIdle(); }
        public void Dispose()
        {
            try { Complete(); }
            finally { KeyBuffer.Dispose(); available.Clear(); consumed.Clear(); LookupValidators.Clear(); Store.Dispose(); }
        }
    }

    internal static void Initialize(string[] args, string saveDataRoot)
    {
        enabled = false;
        root = Path.Combine(saveDataRoot, "WakeUp", "ParsedLanguage", "v1");
        log = Path.Combine(saveDataRoot, "WakeUp", "parsed-language.jsonl");
        bool selected = StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
            && args.Count(a => a.StartsWith("--wake-up-parsed-language=", StringComparison.Ordinal)) == 1
            && args.Contains("--wake-up-parsed-language=on");
        var policy = CacheLaunchPolicy.Current;
        try
        {
            if (policy.Action == CacheAction.Clear)
            {
                using var clear = OpenStore();
                JsonLineLog.WriteEvent(log, "clear", "\"remainingBytes\":" + clear.StoredBytes);
                Status = "Parsed language cache cleared; native loading this launch."; return;
            }
            if (!selected) return;
            if (!policy.AllowRead && !policy.AllowWrite) { Status = "Parsed language cache bypassed this launch."; return; }
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string why)) throw new InvalidOperationException(why);
            Install(); enabled = true;
            Status = "Parsed language reuse selected; unsupported or error groups load normally.";
            JsonLineLog.WriteEvent(log, "selected", "\"installation\":\"prepatched-native-consumers\"");
        }
        catch (Exception e) { enabled = false; Status = "Parsed language reuse refused: " + e.Message; JsonLineLog.WriteReceipt(log, "refused", e.Message); }
    }

    private static OwnedCacheStore OpenStore() => new(root!, ParsedLanguageBundle.MaximumPayload,
        SharedCacheBudget.ForRoot(root!)?.MaximumBytes ?? 1024L * 1024 * 1024, CacheLaunchPolicy.Current);

    internal static void Install()
    {
        if (installed) return;
        var methods = ParsedLanguageContract.Methods();
        foreach (var method in methods)
            if (!ParsedLanguageContract.Matches(method)) throw new InvalidOperationException("changed-language-method:" + method.DeclaringType!.Name + "." + method.Name);
        var contracts = methods.Select(m => PublishedPatchGuard.TryCreate(m, Owner, out var g, true, p => ParsedLanguageConsumers.AllowsPostfix(m, p))
            ? g! : throw new InvalidOperationException("language-hook-guard")).ToArray();
        guards = contracts.Where(g => !ParsedLanguageContract.IsInjectionNameContract(g.Target)).ToArray();
        injectionNameGuards = contracts.Where(g => ParsedLanguageContract.IsInjectionNameContract(g.Target)).ToArray();
        installed = true;
    }

    // Object token keeps the private session type out of the game assembly's API.
    public static object? BeginLoad(LoadedLanguage language, bool alreadyLoaded)
    {
        Begin(language, ref alreadyLoaded, out var state);
        return state;
    }
    public static void EndLoad(object? token, bool completed)
    {
        if (!(token is Session state)) return;
        try { End(state, completed ? null : new InvalidOperationException("native-language-load-failed")); }
        catch (Exception e) { JsonLineLog.WriteReceipt(log, "language-cleanup-failed", e.Message); }
    }

    private static bool Admitted(LoadedLanguage language) => current != null && !current.Invalid
        && ReferenceEquals(current.Language, language) && guards.All(g => g.AllowsOriginalContract());

    private static bool InjectionAdmitted(LoadedLanguage language)
    {
        if (!Admitted(language)) return false;
        var blocked = injectionNameGuards.FirstOrDefault(g => !g.AllowsOriginalContract());
        if (blocked == null)
        {
            try { current!.Consumers.Validate(); return true; }
            catch (Exception e)
            {
                if (!current!.InjectionRefusalRecorded) JsonLineLog.WriteReceipt(log, "injection-native", e.Message);
                current.InjectionRefusalRecorded = true; return false;
            }
        }
        if (!current!.InjectionRefusalRecorded)
        {
            current.InjectionRefusalRecorded = true;
            JsonLineLog.WriteReceipt(log, "injection-native", "foreign-hook:" + blocked.Target.DeclaringType!.FullName + "." + blocked.Target.Name);
            var hooks = Harmony.GetPatchInfo(blocked.Target);
            if (hooks != null)
                foreach (var patch in hooks.Prefixes.Concat(hooks.Postfixes).Concat(hooks.Transpilers).Concat(hooks.Finalizers).Concat(hooks.InnerPrefixes).Concat(hooks.InnerPostfixes))
                {
                    SemanticMethodIdentity.TryHash(patch.PatchMethod, out string body, out _);
                    JsonLineLog.WriteEvent(log, "injection-hook", "\"owner\":" + JsonLineLog.Quote(patch.owner)
                        + ",\"method\":" + JsonLineLog.Quote(patch.PatchMethod.DeclaringType?.FullName + "." + patch.PatchMethod.Name)
                        + ",\"body\":" + JsonLineLog.Quote(body));
                }
        }
        return false;
    }

    private static void Begin(LoadedLanguage __instance, ref bool ___dataIsLoaded, out Session? __state)
    {
        __state = null;
        if (current != null) { current.Invalidate(); return; }
        if (!enabled || ___dataIsLoaded) return;
        var blocked = guards.FirstOrDefault(g => !g.AllowsOriginalContract());
        if (blocked != null)
        { JsonLineLog.WriteReceipt(log, "load-native", "foreign-hook:" + blocked.Target.DeclaringType!.FullName + "." + blocked.Target.Name); return; }
        if (Interlocked.CompareExchange(ref ownerThread, Thread.CurrentThread.ManagedThreadId, 0) != 0)
        { if (activeSession != null) activeSession.Invalidate(); return; }
        try
        {
            // No old language/package/Def references survive this call. Sources
            // are still selected and consumed by the native loader itself.
            string context = Context(__instance);
            activeSession = current = __state = new Session(__instance, OpenStore(), context);
        }
        catch (Exception e) { Volatile.Write(ref ownerThread, 0); JsonLineLog.WriteReceipt(log, "load-native", e.Message); }
    }

    private static void End(Session? __state, Exception? __exception)
    {
        if (__state == null) return;
        activeSession = current = null;
        try
        {
            if (__exception != null) __state.Invalidate();
            __state.Complete();
            JsonLineLog.WriteEvent(log, "load-complete", "\"language\":" + JsonLineLog.Quote(__state.Language.folderName)
                + ",\"keyedHitFiles\":" + __state.KeyedHits + ",\"injectionHitFiles\":" + __state.InjectionHits
                + ",\"stringsHitFiles\":" + __state.StringsHits + ",\"nativeFiles\":" + __state.NativeFiles
                + ",\"nativeInsertionCalls\":" + __state.NativeInsertions + ",\"publishedGroups\":" + __state.Published + ",\"failed\":" + (__exception != null ? "true" : "false")
                + ",\"retainedOwners\":0");
            Status = "Parsed language files reused: " + (__state.KeyedHits + __state.InjectionHits + __state.StringsHits)
                + "; native files: " + __state.NativeFiles + ". Performance qualification pending.";
        }
        finally { try { __state.Dispose(); } finally { Volatile.Write(ref ownerThread, 0); } }
    }
    public static void ClearSession() { if (activeSession != null) activeSession.Invalidate(); }

    private static string Context(LoadedLanguage language)
    {
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        writer.Write("ready-language-group-fragments-v3");
        writer.Write(language.folderName); writer.Write(language.LegacyFolderName);
        writer.Write(LanguageDatabase.activeLanguage?.folderName ?? ""); writer.Write(LanguageDatabase.defaultLanguage?.folderName ?? "");
        writer.Write(GenFilePaths.SaveDataFolderPath);
        writer.Write(typeof(LoadedLanguage).Module.ModuleVersionId.ToString());
        writer.Write(typeof(System.Xml.XmlDocument).Module.ModuleVersionId.ToString());
        writer.Write(typeof(System.Xml.Linq.XDocument).Module.ModuleVersionId.ToString());
        // Bind code actually consumed here. Harmony may load generated wrapper
        // assemblies with process-specific names/MVIDs even when IsDynamic is
        // false. Native type selection still runs before InjectionGroup, whose
        // key separately binds the actual resolved type and module. Unknown
        // parser callbacks remain guarded; unrelated assemblies are not inputs.
        writer.Write(typeof(ParsedLanguageRuntime).Module.ModuleVersionId.ToString());
        foreach (var mod in LoadedModManager.RunningMods)
        {
            writer.Write(mod.PackageId); writer.Write(mod.RootDir);
            foreach (var folder in mod.foldersToLoadDescendingOrder) writer.Write(folder);
            writer.Write("");
        }
        writer.Flush();
        string identity = Digest(bytes.ToArray());
        JsonLineLog.WriteEvent(log, "context", "\"language\":" + JsonLineLog.Quote(language.folderName)
            + ",\"identity\":" + JsonLineLog.Quote(identity));
        return identity;
    }

    private static string Key(string kind, LoadedLanguage language, IList<VirtualFile> files, IList<string> texts, byte[] before, Type? type = null, bool nativeInsertion = false)
    {
        if (files.Count > 8192 || files.Count != texts.Count || texts.Sum(t => (long)t.Length) > 16 * 1024 * 1024)
            throw new InvalidDataException("language-source-bound");
        var session = current!;
        var writer = session.KeyBuffer.Begin(); bool complete = false;
        try
        {
            KeyHeader(writer, session.Context, kind, before, files.Count);
            for (int i = 0; i < files.Count; i++) KeyFile(writer, files[i], texts[i]);
            if (type != null)
            {
                writer.Write(type.AssemblyQualifiedName); writer.Write(type.Module.ModuleVersionId.ToString());
                // Keep current name/converter/delegate validation at the same
                // point after complete source serialization, including warm keys.
                if (Scribe.mode != LoadSaveMode.Inactive) throw new InvalidDataException("language-scribe-active");
                if (nativeInsertion)
                {
                    if (!session.LookupValidators.TryGetValue(type, out var validate))
                        session.LookupValidators.Add(type, validate = ParsedLanguageContract.CreateDefLookupValidator(type, session.Consumers));
                    validate();
                }
                else ParsedLanguageContract.WriteDefContext(writer, type);
            }
            string result = session.KeyBuffer.Digest(); complete = true; return result;
        }
        finally { session.KeyBuffer.Release(complete && !session.Invalid); }
    }

    private static string SingleFileKey(string kind, VirtualFile file, string text)
    {
        if (text.Length > 16 * 1024 * 1024) throw new InvalidDataException("language-source-bound");
        var session = current!;
        var writer = session.KeyBuffer.Begin(); bool complete = false;
        try
        {
            KeyHeader(writer, session.Context, kind, Array.Empty<byte>(), 1);
            KeyFile(writer, file, text);
            string result = session.KeyBuffer.Digest(); complete = true; return result;
        }
        finally { session.KeyBuffer.Release(complete && !session.Invalid); }
    }

    private static void KeyHeader(BinaryWriter writer, string context, string kind, byte[] before, int count)
    { writer.Write(context); writer.Write(kind); writer.Write(Digest(before)); writer.Write(count); }
    private static void KeyFile(BinaryWriter writer, VirtualFile file, string text)
    {
        if (!ParsedLanguageContract.FileSupported(file)) throw new InvalidDataException("language-provider");
        writer.Write(file.FullPath); writer.Write(file.Name); writer.Write(text);
    }
    private static string Digest(byte[] bytes) => BitConverter.ToString(CacheDigest.Hash(bytes)).Replace("-", "");

    public static void KeyedGroup(LoadedLanguage language, List<VirtualFile> files, List<string> texts)
    {
        for (int i = 0; i < texts.Count; i++) NativeKeyed(language, files[i], texts[i]);
    }

    public static void InjectionGroup(LoadedLanguage language, List<VirtualFile> files, List<string> texts, Type type)
    {
        var session = current; string? key = null;
        List<List<ParsedInjectionInstructions.Operation>>? operations = null;
        bool warm = false;
        if (InjectionAdmitted(language))
            try
            {
                key = Key("injection-native-operations-v1", language, files, texts, Array.Empty<byte>(), type, true);
                operations = session!.Read(key, ParsedLanguageFormat.DecodeInstructions);
                if (operations != null && operations.Count != files.Count) throw new InvalidDataException("language-instruction-files");
                warm = operations != null;
                if (operations == null)
                {
                    operations = new List<List<ParsedInjectionInstructions.Operation>>(files.Count);
                    for (int i = 0; i < files.Count; i++)
                    {
                        var parsed = ParsedInjectionInstructions.Parse(files[i], texts[i], type);
                        if (parsed == null) { operations = null; break; }
                        operations.Add(parsed);
                    }
                }
                if (!InjectionAdmitted(language)) operations = null;
            }
            catch (Exception e) { operations = null; JsonLineLog.WriteReceipt(log, "injection-native", e.Message); }
        // All payload validation finishes before this commit. Native per-file
        // catch, flags, partial insertion and later-file progression remain native.
        for (int i = 0; i < texts.Count; i++)
        {
            if (operations != null && InjectionAdmitted(language))
            {
                using var replay = new ParsedInjectionInstructions.Replay(files[i], texts[i], type, operations[i]);
                language.LoadFromFile_DefInject(files[i], type, texts[i]);
                session!.NativeInsertions += replay.Calls;
                if (warm && replay.Consumed) session.InjectionHits++;
                else session.NativeFiles++;
            }
            else
            {
                language.LoadFromFile_DefInject(files[i], type, texts[i]);
                if (session != null) session.NativeFiles++;
            }
        }
        if (!warm && key != null && operations != null && InjectionAdmitted(language))
            Publish(session!, key, () => ParsedLanguageFormat.EncodeInstructions(operations));
    }

    // Only source interpretation is cached. Native consumers retain duplicate
    // checks, error catches, path construction, fresh records and live merges.
    public static IEnumerable<string> StringLines(string text, LoadedLanguage language, VirtualFile file)
        => SourceRows("native-string-lines-v1", text, language, file,
            () => GenText.LinesFromString(text), ParsedLanguageFormat.DecodeLines, ParsedLanguageFormat.EncodeLines, false);

    public static IEnumerable<DirectXmlLoaderSimple.XmlKeyValuePair> KeyedRows(string text, LoadedLanguage language, VirtualFile file)
        => SourceRows("native-keyed-rows-v1", text, language, file,
            () => DirectXmlLoaderSimple.ValuesFromXmlFile(text), ParsedLanguageFormat.DecodeKeyedRows, ParsedLanguageFormat.EncodeKeyedRows, true);

    private static IEnumerable<T> SourceRows<T>(string kind, string text, LoadedLanguage language, VirtualFile file,
        Func<IEnumerable<T>> native, Func<byte[], List<T>> decode, Func<List<T>, byte[]> encode, bool keyed)
    {
        var session = current; string? key = null;
        if (Admitted(language))
            try
            {
                key = SingleFileKey(kind, file, text);
                var ready = session!.Read(key, decode);
                if (ready != null && Admitted(language))
                {
                    if (keyed) session.KeyedHits++; else session.StringsHits++;
                    return ready;
                }
            }
            catch (Exception e) { key = null; JsonLineLog.WriteReceipt(log, "source-native", e.Message); }
        if (session != null) session.NativeFiles++;
        return key == null ? native() : CaptureRows(session!, key, language, native, encode);
    }

    private static IEnumerable<T> CaptureRows<T>(Session session, string key, LoadedLanguage language,
        Func<IEnumerable<T>> native, Func<List<T>, byte[]> encode)
    {
        List<T>? rows = new();
        // Never catch a parser/consumer failure and retry after partial work.
        // Iterator completion alone permits publication; interrupted enumeration
        // and malformed XML retain the caller's original native catch behavior.
        foreach (T row in native())
        {
            if (rows != null)
            {
                if (rows.Count == 262144) rows = null;
                else rows.Add(row);
            }
            yield return row;
        }
        if (rows != null && ReferenceEquals(current, session) && Admitted(language))
            Publish(session, key, () => encode(rows));
    }

    private static void Publish(Session session, string key, Func<byte[]> encode)
    {
        try { if (session.Retain(key, encode())) session.Published++; }
        catch (Exception e) { JsonLineLog.WriteReceipt(log, "group-not-published", e.Message); }
    }

    internal static IEnumerable<CodeInstruction> LoadTranspiler(IEnumerable<CodeInstruction> source)
    {
        var code = source.Select(c => new CodeInstruction(c)).ToList();
        if (!Unchanged(code, AccessTools.Method(typeof(LoadedLanguage), "LoadData"))) return code;
        ReplaceLoop(code, "LoadFromFile_DefInject", nameof(InjectionGroup), true);
        ReplaceLoop(code, "LoadFromFile_Keyed", nameof(KeyedGroup), false);
        return code;
    }
    private static void ReplaceLoop(List<CodeInstruction> code, string parser, string bridge, bool injection)
    {
        int call = code.FindIndex(c => c.Calls(AccessTools.Method(typeof(LoadedLanguage), parser)));
        if (call < 9) throw new InvalidOperationException("language-loop-shape");
        var files = new CodeInstruction(code[call - (injection ? 7 : 6)]);
        var texts = new CodeInstruction(code[call - 3]);
        var type = injection ? new CodeInstruction(code[call - 4]) : null;
        int begin = call - (injection ? 11 : 10);
        int end = call + 8;
        if (code[begin].opcode != OpCodes.Ldc_I4_0 || code[end].opcode != OpCodes.Blt_S && code[end].opcode != OpCodes.Blt)
            throw new InvalidOperationException("language-loop-control");
        if (code.Skip(begin).Take(end - begin + 1).Any(c => c.blocks.Count != 0)) throw new InvalidOperationException("language-loop-region");
        var replacement = new List<CodeInstruction> { new(OpCodes.Ldarg_0), files, texts };
        if (type != null) replacement.Add(type);
        replacement.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ParsedLanguageRuntime), bridge)));
        foreach (var c in replacement) { c.labels.Clear(); c.blocks.Clear(); }
        replacement[0].labels.AddRange(code[begin].labels);
        code.RemoveRange(begin, end - begin + 1); code.InsertRange(begin, replacement);
    }
    internal static IEnumerable<CodeInstruction> StringsTranspiler(IEnumerable<CodeInstruction> source, ILGenerator generator)
        => ReplaceSourceCall(source, AccessTools.Method(typeof(LoadedLanguage), "LoadFromFile_Strings"),
            AccessTools.Method(typeof(GenText), nameof(GenText.LinesFromString)), nameof(StringLines), false);

    internal static IEnumerable<CodeInstruction> KeyedTranspiler(IEnumerable<CodeInstruction> source)
        => ReplaceSourceCall(source, KeyedMethod,
            AccessTools.Method(typeof(DirectXmlLoaderSimple), nameof(DirectXmlLoaderSimple.ValuesFromXmlFile), new[] { typeof(string) }), nameof(KeyedRows), false);

    private static IEnumerable<CodeInstruction> ReplaceSourceCall(IEnumerable<CodeInstruction> source, MethodInfo target,
        MethodInfo parser, string bridge, bool directory)
    {
        var code = source.Select(c => new CodeInstruction(c)).ToList();
        if (!Unchanged(code, target)) return code;
        int at = code.FindIndex(c => c.Calls(parser));
        if (at < 0 || code.Count(c => c.Calls(parser)) != 1) throw new InvalidOperationException("language-source-call");
        var replacement = new List<CodeInstruction> { new(OpCodes.Ldarg_0), new(OpCodes.Ldarg_1) };
        if (directory) replacement.Add(new CodeInstruction(OpCodes.Ldarg_2));
        replacement.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ParsedLanguageRuntime), bridge)));
        replacement[0].labels.AddRange(code[at].labels); replacement[0].blocks.AddRange(code[at].blocks);
        code.RemoveAt(at); code.InsertRange(at, replacement);
        return code;
    }
    private static bool Unchanged(List<CodeInstruction> code, MethodBase target)
        => Harmony.GetPatchInfo(target)?.Transpilers.Any(p => p.owner != Owner) != true
            && InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(target));
}
