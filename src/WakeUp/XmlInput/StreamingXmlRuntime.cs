// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

// The native indexed scheduler still owns all work and publication. This bridge
// only avoids materializing an entire decoded input string before native parsing.
public static class StreamingXmlRuntime
{
    internal const string EffectiveConstructorBody = "6AF2664812D69439B58BB56AEC4F36FC5C315927B291E1D8C29C13972F8C5BB3";
    internal const string Owner = "kt-nyx.wake-up.streaming-xml";
    internal const int MaxFileBytes = 16 * 1024 * 1024;
    internal const int BufferBytes = 4 * 1024;
    internal const int MaxConcurrentReads = 3;
    private static volatile bool enabled, finished;
    private static int scopes, reading, completed, refused;
    private static ConstructorInfo? constructor;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    internal static string Status { get; private set; } = "Streaming XML input is off.";

    internal static void TryInitialize(IReadOnlyList<string> arguments)
    {
        if (StartupLaunchSelector.Parse(arguments).Selection != StartupSelection.Candidate
            || arguments.Count(a => a?.StartsWith("--wake-up-streaming-xml=", StringComparison.Ordinal) == true) != 1
            || !arguments.Contains("--wake-up-streaming-xml=on")) return;
        if (LoaderSupplierPolicy.YieldXml) { LoaderSupplierPolicy.RefuseXml("streaming-xml"); Status = LoaderSupplierPolicy.Reason(true); return; }
        if (LoaderSupplierPolicy.InsideXmlFallback)
        {
            Status = "Streaming XML needs its pre-load boundary; the current XML call has already entered without that bridge.";
            CompatibilityStatus.Refuse("streaming-xml", Status, "missing-preload-boundary");
            return;
        }
        try
        {
            if (PlayDataLoader.Loaded) throw new InvalidOperationException("startup XML loading already ended");
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason)) throw new InvalidOperationException(reason);
            constructor = typeof(LoadableXmlAsset).GetConstructor(new[] { typeof(FileInfo), typeof(ModContentPack) });
            if (constructor == null || !SemanticMethodIdentity.TryHash(constructor, out string hash, out reason)
                || hash != EffectiveConstructorBody) throw new InvalidOperationException("required XML constructor rewrite is unavailable");
            guards = CreateGuards(constructor);
            var harmony = new Harmony(Owner);
            foreach (string stage in new[] { "LoadModXML", "ErrorCheckPatches" })
                harmony.Patch(AccessTools.Method(typeof(LoadedModManager), stage),
                    prefix: new HarmonyMethod(typeof(StreamingXmlRuntime), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(StreamingXmlRuntime), nameof(End)));
            harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"),
                postfix: new HarmonyMethod(typeof(StreamingXmlRuntime), nameof(MenuUpdate)));
            enabled = true;
            Status = "Streaming XML input selected; awaiting supported native XML reads."; CompatibilityStatus.Available("streaming-xml");
        }
        catch (Exception exception)
        {
            enabled = false;
            new Harmony(Owner).UnpatchAll(Owner);
            Status = "Streaming XML input refused: " + exception.Message + ". Ordinary loading is in use."; CompatibilityStatus.Refuse("streaming-xml", Status);
        }
        Log.Message("[Wake-Up] " + Status);
    }

    private static void Begin(out bool __state)
    {
        __state = enabled && !finished;
        if (__state) Interlocked.Increment(ref scopes);
    }
    private static void End(bool __state) { if (__state) Interlocked.Decrement(ref scopes); }
    private static void MenuUpdate() { if (!PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting) return; Dispose(); }
    internal static void Dispose()
    {
        if (!enabled || finished) return;
        finished = true;
        enabled = false;
        Status = completed == 0
            ? "Streaming XML input completed with no active reads; " + refused + " ordinary fallbacks. Native or supplier loading remained in use."
            : "Streaming XML input completed: " + completed + " files; " + refused + " ordinary fallbacks.";
        Log.Message("[Wake-Up] " + Status);
    }

    internal static PublishedPatchGuard[] CreateGuards(ConstructorInfo target)
    {
        var targets = new MethodBase[] { target,
            typeof(XmlDocument).GetConstructor(Type.EmptyTypes)!,
            typeof(XmlDocument).GetMethod("Load", new[] { typeof(XmlReader) })!,
            typeof(XmlReader).GetMethod("Create", new[] { typeof(TextReader), typeof(XmlReaderSettings) })!,
            typeof(StringReader).GetConstructor(new[] { typeof(string) })!,
            typeof(StringReader).GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null)!,
            typeof(StreamReader).GetConstructor(new[] { typeof(Stream), typeof(Encoding), typeof(bool), typeof(int) })!,
            typeof(Encoding).GetProperty("UTF8")!.GetGetMethod()!
        };
        var mapping = typeof(LoadableXmlAsset).Assembly.GetType("Verse.MemoryMappedFileSpanWrapper", true);
        var nativeReaders = new MethodBase[] { mapping.GetConstructor(new[] { typeof(FileInfo) })!,
            mapping.GetMethod("GetReadOnlySpan")!, mapping.GetProperty("FileSize")!.GetGetMethod()!,
            mapping.GetMethod("Dispose")! };
        // Include the actual UTF-8 implementation and inherited overloads: the
        // game uses GetString(span), while the desktop semantic host lacks that
        // BCL overload. No missing overload is fabricated by the test host.
        var decoding = Encoding.UTF8.GetType().GetMethods().Where(m => m.Name == "GetString").Cast<MethodBase>();
        return targets.Concat(nativeReaders).Concat(decoding).Distinct().Select(method => PublishedPatchGuard.TryCreate(method, Owner, out var guard, allPatchKinds: true)
            ? guard! : throw new InvalidOperationException("XML patch publication contract unavailable")).ToArray();
    }

    public static bool TryRead(FileInfo file, XmlReaderSettings settings, out XmlDocument document)
    {
        if (ParsedXmlRuntime.TryTake(file, settings, out document)) return true;
        if (OrderedInputRuntime.TryRead(file, settings, out document)) return true;
        document = null!;
        if (!enabled || finished || Volatile.Read(ref scopes) == 0) return false;
        if (!CompatibilityStatus.Guard("streaming-xml", guards.Length != 0 && guards.All(g => g.AllowsOriginalContract()))) { Interlocked.Increment(ref refused); return false; }
        if (Interlocked.Increment(ref reading) > MaxConcurrentReads)
        {
            Interlocked.Decrement(ref reading);
            Interlocked.Increment(ref refused);
            return false;
        }
        try
        {
            if (settings.DtdProcessing != DtdProcessing.Prohibit || settings.ValidationType != ValidationType.None || settings.NameTable != null)
            { Interlocked.Increment(ref refused); return false; }
            // Keep native FileInfo identity/length semantics. Empty and oversized
            // files use the original constructor, including its exact diagnostics.
            if (!file.Exists || file.Length <= 0 || file.Length > MaxFileBytes) { Interlocked.Increment(ref refused); return false; }
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferBytes, FileOptions.SequentialScan);
            if (stream.Length != file.Length) { Interlocked.Increment(ref refused); return false; }
            // Native always decodes UTF-8, regardless of an XML encoding declaration.
            // Detecting UTF-16/32 BOMs here would silently change native semantics.
            using var text = new StreamReader(stream, Encoding.UTF8, false, BufferBytes);
            using var reader = XmlReader.Create(text, settings);
            var parsed = new XmlDocument();
            parsed.Load(reader);
            if (finished) return false;
            document = parsed;
            if (Interlocked.Increment(ref completed) == 1)
                Status = "Streaming XML input is active; native file order and publication remain in use.";
            return true;
        }
        catch (Exception)
        {
            // No logging and no partial publication: native retries and owns the
            // attributed error once, exactly as before this optional bridge.
            Interlocked.Increment(ref refused);
            return false;
        }
        finally { Interlocked.Decrement(ref reading); }
    }
}
