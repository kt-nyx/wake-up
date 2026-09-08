// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Xml;
using HarmonyLib;
using Verse;

namespace RimWorldLoadingOptimizer.RimWorld;

// Optional supplier integration. No Gagarin types appear in assembly references.
// Keep the supplier's Load body, constructor, callbacks, ordered ImportNode loop,
// dictionary assignments, logging and exceptions. Reuse only its private parse.
internal static class GagarinCacheRuntime
{
    private const string Owner = "rimworldloadingoptimizer.gagarin-cache";
    private static string? path;
    private static string mode = "off";
    private static bool installed;
    private static Assembly? supplier;
    private static MethodInfo[] guarded = Array.Empty<MethodInfo>();
    private static MethodInfo? assetPostfix;
    private static Func<bool> usingCache = null!, loadingMods = null!, loadingPatches = null!;
    [ThreadStatic] private static Scope? current;

    internal static void TryInitialize(IReadOnlyList<string> arguments)
    {
        string[] selectors = arguments.Where(a => a.StartsWith("--rlo-gagarin-cache=", StringComparison.Ordinal)).ToArray();
        if (path != null || selectors.Length != 1) return;
        mode = selectors[0].Substring("--rlo-gagarin-cache=".Length);
        var selection = StartupLaunchSelector.Parse(arguments);
        if (!new[] { "timing", "on", "verify" }.Contains(mode)
            || selection.Selection != StartupSelection.Candidate || !arguments.Contains("--rlo-strategy=startup-searches")) return;
        path = Path.Combine(selection.SaveDataRoot!, "RimWorldLoadingOptimizer", "gagarin-cache.jsonl");
        try
        {
            if (!ReferenceEquals(GameBuildContract.Current, GameBuildContract.GogRev573)
                || !RuntimeIdentity.ValidateBinaryIdentity(out _)) throw new InvalidOperationException("runtime-identity");
            var harmony = new Harmony(Owner);
            // All mod constructors have completed here, including memory-loaded plugins.
            harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "LoadModXML"),
                prefix: new HarmonyMethod(typeof(GagarinCacheRuntime), nameof(InstallLate)) { priority = Priority.First });
            harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "CombineIntoUnifiedXML"),
                prefix: new HarmonyMethod(typeof(GagarinCacheRuntime), nameof(StageBegin)) { priority = Priority.First },
                finalizer: new HarmonyMethod(typeof(GagarinCacheRuntime), nameof(StageEnd)) { priority = Priority.Last });
        }
        catch (Exception e) { new Harmony(Owner).UnpatchAll(Owner); Write("refused", "\"reason\":" + Quote(e.Message)); }
    }

    private static void InstallLate()
    {
        if (installed) return;
        installed = true;
        long start = Stopwatch.GetTimestamp();
        try
        {
            Assembly[] matches = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetType("Gagarin.CachedDefHelper") != null).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException(matches.Length == 0 ? "optional-supplier-absent" : "ambiguous-supplier");
            supplier = matches[0];
            if (supplier.ManifestModule.ModuleVersionId != new Guid("23d812a3-057c-4caf-aa0c-6aa2e37e1ba3")) throw new InvalidOperationException("unsupported-supplier-module");
            var mod = LoadedModManager.RunningModsListForReading.SingleOrDefault(m => m.PackageId == "vr.missilegirl");
            if (mod == null) throw new InvalidOperationException("supplier-not-active");
            using (var file = File.OpenRead(Path.Combine(mod.RootDir, "1.6", "Plugins", "Stable", "Gagarin.dll")))
                if (Hash(file) != "D83EDC9FE3AE0381A71EE460F766563F1CB078F61211D8388614BAB3B0E1250C") throw new InvalidOperationException("unsupported-supplier-file");
            string[] types = { "CachedDefHelper", "LoadableXmlAsset_Constructor_Patch", "LoadableXmlAsset_Constructor_Patch", "Context", "Context", "Context", "GagarinEnvironmentInfo", "LoadedModManager_Patch+CombineIntoUnifiedXML_Patch", "LoadedModManager_Patch+CombineIntoUnifiedXML_Patch", "LoadedModManager_Profiler+CombineIntoUnifiedXML_Profiler", "LoadedModManager_Profiler+CombineIntoUnifiedXML_Profiler" };
            string[] names = { "Load", "Postfix_FileInfo", "Process", "get_IsUsingCache", "get_IsLoadingModXML", "get_IsLoadingPatchXML", "get_UnifiedXmlFilePath", "Prefix", "Postfix", "Prefix", "Postfix" };
            string[] hashes = {
                "A45EE8A2551B3B1D6F3D9062C29A07B3C77B24D608EEA554074C6ACA0888EDFC", "EEAC0DAD31A18C7A256FF9C68EEF8CEB5FF54B4A6A46E53F495C931D153AC2BB", "890657130AF6F7B93B9096BCCCA9F7B3D05B523716AB944DB428C0CF085A5620",
                "D953AC40664928C0E3981D1C995C652BACAD4249446094094CFECAC1955CBD18", "BA9288BF25018C0C05DFE0B64486D54E44F84EC0D9770220C1BEFB19486DA898", "6C52F9FBF400BADE6938A827BF540DE797E373922339FDB267C2761A8BD3BA45", "4B6907E58201AB3115EF1D9C3D59A5C820BDF0CCAF465B9C9CCC85A248C08A71",
                "1730DF571AFB56199F2CD98AB8BBF8397C583622B723657994A6E488065E9020", "B53C71B5D06118558AD791DDDFC6ABBB1134E5DD53272EC4FDA1DAB98B266929", "A48565B48EFEDB28AFC619C604E86462F319A8D02759512DEAFFD6C3719CA3FA", "88FD9766B064C60B9AB9710B51C2CD7D60FECB47008CE45702ED7F049D37CD6E" };
            guarded = names.Select((n, i) => AccessTools.Method(supplier.GetType("Gagarin." + types[i]), n)).ToArray();
            for (int i = 0; i < guarded.Length; i++)
            {
                using var body = new MemoryStream(guarded[i].GetMethodBody()!.GetILAsByteArray());
                if (Hash(body) != hashes[i]) throw new InvalidOperationException("loaded-body-mismatch-" + names[i]);
            }
            assetPostfix = guarded[1];
            usingCache = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), guarded[3]);
            loadingMods = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), guarded[4]);
            loadingPatches = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), guarded[5]);
            ConstructorInfo ctor = AssetConstructor;
            if (!SemanticMethodIdentity.TryHash(ctor, out string actual, out string reason)
                || actual != SemanticMethodIdentity.ExpectedFor(GameBuildContract.GogRev573, 0x060035A2)) throw new InvalidOperationException("asset-constructor-" + reason);
            if (!SafeHooks() && mode != "timing") throw new InvalidOperationException("unknown-interfering-hooks");
            var harmony = new Harmony(Owner);
            harmony.Patch(guarded[0], prefix: new HarmonyMethod(typeof(GagarinCacheRuntime), nameof(LoadBegin)) { priority = Priority.First },
                transpiler: new HarmonyMethod(typeof(GagarinCacheRuntime), nameof(RewriteLoad)),
                finalizer: new HarmonyMethod(typeof(GagarinCacheRuntime), nameof(LoadEnd)) { priority = Priority.Last });
            harmony.Patch(AssetConstructor, transpiler: new HarmonyMethod(typeof(GagarinCacheRuntime), nameof(RewriteConstructor)));
            harmony.Patch(assetPostfix, transpiler: new HarmonyMethod(typeof(GagarinCacheRuntime), nameof(RewriteOuterXml)));
            Write("installed", "\"mode\":" + Quote(mode) + ",\"installationMs\":" + Ms(start));
        }
        catch (Exception e) { new Harmony(Owner).UnpatchAll(Owner); Write("refused", "\"reason\":" + Quote(e.ToString()) + ",\"installationMs\":" + Ms(start)); }
    }

    private static ConstructorInfo AssetConstructor => AccessTools.Constructor(typeof(LoadableXmlAsset), new[] { typeof(FileInfo), typeof(ModContentPack) });
    private static bool SafeHooks()
    {
        foreach (MethodBase method in guarded.Cast<MethodBase>().Concat(new MethodBase[] { AssetConstructor, AccessTools.Method(typeof(LoadedModManager), "CombineIntoUnifiedXML"), AccessTools.Method(typeof(XmlDocument), "Load", new[] { typeof(XmlReader) }), AccessTools.PropertyGetter(typeof(XmlNode), "OuterXml"), AccessTools.PropertyGetter(typeof(XmlDocument), "OuterXml"), AccessTools.Constructor(typeof(XmlDocument), Type.EmptyTypes), AccessTools.Method(typeof(XmlNode), "RemoveAll"), AccessTools.Method(typeof(XmlDocument), "RemoveAll"), AccessTools.Constructor(typeof(StringReader), new[] { typeof(string) }) }))
        {
            Patches? patches = Harmony.GetPatchInfo(method);
            if (patches == null) continue;
            foreach (Patch patch in patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers))
            {
                if (patch.owner == Owner && patch.PatchMethod.DeclaringType == typeof(GagarinCacheRuntime)) continue;
                if (Equals(method, AssetConstructor) && Equals(patch.PatchMethod, assetPostfix) && patches.Postfixes.Contains(patch)) continue;
                if (method.DeclaringType == typeof(LoadedModManager) && (Equals(patch.PatchMethod, guarded[7]) || Equals(patch.PatchMethod, guarded[8]) || Equals(patch.PatchMethod, guarded[9]) || Equals(patch.PatchMethod, guarded[10]))) continue;
                Write("unknown-hook", "\"target\":" + Quote(method.ToString()!) + ",\"callback\":" + Quote(patch.PatchMethod.DeclaringType?.FullName + "." + patch.PatchMethod.Name) + ",\"owner\":" + Quote(patch.owner));
                return false;
            }
        }
        return true;
    }

    private static void StageBegin(out long __state) => __state = Stopwatch.GetTimestamp();
    private static void StageEnd(long __state, Exception? __exception)
        => Write("stage", "\"elapsedMs\":" + Ms(__state) + ",\"failed\":" + (__exception != null ? "true" : "false"));

    private static void LoadBegin(out Scope __state)
    {
        __state = new Scope { Parent = current };
        current = __state;
        try
        {
            __state.Admitted = (mode == "on" || mode == "verify") && __state.Parent == null && !PlayDataLoader.Loaded
                && SafeHooks() && usingCache() && !loadingMods() && !loadingPatches();
        }
        catch { __state.Admitted = false; }
        __state.AdmissionTicks = Stopwatch.GetTimestamp() - __state.Start;
    }
    private static void LoadEnd(Scope __state, XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> assets, Exception? __exception)
    {
        current = __state.Parent;
        if (mode == "verify" && __exception == null && __state.Reused) VerifyProjection(__state, document, assets);
        Write("load", "\"elapsedMs\":" + Ms(__state.Start) + ",\"admissionMs\":" + Ticks(__state.AdmissionTicks)
            + ",\"admitted\":" + (__state.Admitted ? "true" : "false") + ",\"reused\":" + (__state.Reused ? "true" : "false")
            + ",\"constructors\":" + __state.Constructors + ",\"outerXmlSkipped\":" + __state.OuterSkipped
            + ",\"readMs\":" + Ticks(__state.ReadTicks) + ",\"constructorMs\":" + Ticks(__state.ConstructorTicks)
            + ",\"outerXmlMs\":" + Ticks(__state.OuterTicks) + ",\"secondParseMs\":" + Ticks(__state.ParseTicks)
            + ",\"failed\":" + (__exception != null ? "true" : "false"));
    }

    internal static IEnumerable<CodeInstruction> RewriteLoad(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(c => new CodeInstruction(c)).ToArray();
        var counts = new int[6];
        MethodBase[] originals = { AccessTools.Method(typeof(File), "ReadAllText", new[] { typeof(string) }), AssetConstructor,
            AccessTools.Constructor(typeof(XmlDocument), Type.EmptyTypes), AccessTools.Method(typeof(XmlNode), "RemoveAll"),
            AccessTools.Method(typeof(XmlDocument), "Load", new[] { typeof(XmlReader) }), AccessTools.Method(typeof(XmlDocument), "ImportNode") };
        string[] replacements = { nameof(ReadText), nameof(CreateAsset), nameof(CreateDocument), nameof(RemoveAll), nameof(LoadDocument), string.Empty };
        foreach (CodeInstruction c in code)
            for (int i = 0; i < originals.Length; i++)
                if (Equals(c.operand, originals[i]) && (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt || c.opcode == OpCodes.Newobj))
                { counts[i]++; if (i == 5) break; c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(GagarinCacheRuntime), replacements[i]); break; }
        if (!counts.SequenceEqual(new[] { 1, 1, 1, 2, 1, 1 })) throw new InvalidOperationException("cache-load-call-shape-" + string.Join(",", counts));
        return code;
    }
    internal static IEnumerable<CodeInstruction> RewriteOuterXml(IEnumerable<CodeInstruction> instructions)
    {
        int count = 0;
        var code = instructions.Select(c => new CodeInstruction(c)).ToArray();
        foreach (CodeInstruction c in code)
            if (c.Calls(AccessTools.PropertyGetter(typeof(XmlNode), "OuterXml")))
            { count++; c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(GagarinCacheRuntime), nameof(OuterXml)); }
        if (count != 1) throw new InvalidOperationException("outer-xml-call-shape");
        return code;
    }

    internal static IEnumerable<CodeInstruction> RewriteConstructor(IEnumerable<CodeInstruction> instructions)
    {
        int count = 0;
        var code = instructions.Select(c => new CodeInstruction(c)).ToArray();
        ConstructorInfo target = AccessTools.Constructor(typeof(StringReader), new[] { typeof(string) });
        foreach (CodeInstruction c in code)
            if (c.opcode == OpCodes.Newobj && Equals(c.operand, target))
            { count++; c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(GagarinCacheRuntime), nameof(CaptureReader)); }
        if (count != 1) throw new InvalidOperationException("constructor-reader-shape");
        return code;
    }
    private static StringReader CaptureReader(string text)
    {
        if (current?.Admitted == true)
        {
            current.SameText = string.Equals(current.Text, text, StringComparison.Ordinal);
            current.Text = null;
        }
        return new StringReader(text);
    }

    private static string DocumentHash(XmlDocument document)
    {
        using var sha = SHA256.Create();
        using var stream = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { CheckCharacters = false, CloseOutput = false })) document.Save(writer);
        stream.FlushFinalBlock();
        return BitConverter.ToString(sha.Hash!).Replace("-", "");
    }
    private static void VerifyProjection(Scope scope, XmlDocument document, Dictionary<XmlNode, LoadableXmlAsset> assets)
    {
        long start = Stopwatch.GetTimestamp();
        var sourceAssets = (Dictionary<string, LoadableXmlAsset>)AccessTools.Field(supplier!.GetType("Gagarin.Context"), "XmlAssets").GetValue(null);
        int badOwnership = 0, badMapping = 0, roots = 0, mapped = 0;
        XmlNode? destination = document.DocumentElement!.FirstChild;
        foreach (XmlElement item in scope.Parsed!.DocumentElement!.ChildNodes)
        {
            roots++;
            bool expected = sourceAssets.TryGetValue(item.GetAttribute("path"), out LoadableXmlAsset asset);
            if (destination == null) { badMapping++; continue; }
            bool present = assets.TryGetValue(destination, out LoadableXmlAsset actual);
            if (present) mapped++;
            if (present != expected || (present && !ReferenceEquals(asset, actual))) badMapping++;
            destination = destination.NextSibling;
        }
        if (destination != null || mapped != assets.Count) badMapping++;
        // Depth-first walk with no per-node collection or document copy.
        XmlNode? node = document.DocumentElement;
        while (node != null)
        {
            if (!ReferenceEquals(node.OwnerDocument, document)) badOwnership++;
            if (node.FirstChild != null) { node = node.FirstChild; continue; }
            while (node != null && node != document && node.NextSibling == null) node = node.ParentNode;
            node = node == document ? null : node?.NextSibling;
        }
        Write("verification", "\"sameSourceParse\":" + (scope.ParseMatched ? "true" : "false") + ",\"roots\":" + roots
            + ",\"mapped\":" + mapped + ",\"badMapping\":" + badMapping + ",\"badOwnership\":" + badOwnership
            + ",\"outputSha256\":" + Quote(DocumentHash(document)) + ",\"verificationMs\":" + Ms(start));
        if (badMapping != 0 || badOwnership != 0) throw new InvalidOperationException("cache-projection-verification-mismatch");
    }

    private static string ReadText(string filename)
    {
        long start = Stopwatch.GetTimestamp();
        string text = File.ReadAllText(filename);
        if (current != null)
        {
            current.ReadTicks += Stopwatch.GetTimestamp() - start;
            // Compare with the actual string supplied to the unchanged constructor
            // reader. Different decoding or a file change retains the first reader.
            current.Text = text;
        }
        return text;
    }
    private static LoadableXmlAsset CreateAsset(FileInfo file, ModContentPack mod)
    {
        Scope? scope = current;
        long start = Stopwatch.GetTimestamp();
        var asset = new LoadableXmlAsset(file, mod); // Never elide or replay callbacks.
        if (scope != null)
        {
            scope.Constructors++;
            scope.ConstructorTicks += Stopwatch.GetTimestamp() - start;
            if (scope.Admitted && scope.SameText && asset.xmlDoc?.DocumentElement?.Name == "DefXmlStorage") scope.Parsed = asset.xmlDoc;
        }
        return asset;
    }
    private static XmlDocument CreateDocument()
    {
        if (current?.Parsed == null) return new XmlDocument();
        current.Reused = true;
        return current.Parsed;
    }
    private static void RemoveAll(XmlNode document)
    {
        if (current?.Reused == true && ReferenceEquals(current.Parsed, document)) return;
        document.RemoveAll();
    }
    private static void LoadDocument(XmlDocument document, XmlReader reader)
    {
        if (current?.Reused == true && ReferenceEquals(current.Parsed, document))
        {
            if (mode == "verify")
            {
                var original = new XmlDocument();
                original.Load(reader);
                current.ParseMatched = DocumentHash(original) == DocumentHash(document);
                if (!current.ParseMatched) throw new InvalidOperationException("cache-parse-verification-mismatch");
            }
            return;
        }
        long start = Stopwatch.GetTimestamp();
        document.Load(reader);
        if (current != null) current.ParseTicks += Stopwatch.GetTimestamp() - start;
    }
    private static string OuterXml(XmlNode node)
    {
        if (current?.Admitted == true && node != null && !loadingMods() && !loadingPatches())
        { current.OuterSkipped++; return string.Empty; } // Process still executes its original early return.
        if (current == null) return node!.OuterXml;
        long start = Stopwatch.GetTimestamp();
        string result = node!.OuterXml;
        if (current != null) current.OuterTicks += Stopwatch.GetTimestamp() - start;
        return result;
    }
    private static string Hash(Stream stream) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
    private static string Ms(long start) => Ticks(Stopwatch.GetTimestamp() - start);
    private static string Ticks(long ticks) => (ticks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture);
    private static string Quote(string text) => "\"" + text.Replace("\\", "/").Replace("\"", "'").Replace("\r", " ").Replace("\n", " ") + "\"";
    private static void Write(string kind, string fields)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(path!)!); File.AppendAllText(path!, "{\"event\":" + Quote(kind) + "," + fields + "}\n"); } catch { }
    }
    private sealed class Scope
    {
        internal readonly long Start = Stopwatch.GetTimestamp();
        internal Scope? Parent;
        internal bool Admitted, SameText, Reused, ParseMatched;
        internal string? Text;
        internal XmlDocument? Parsed;
        internal int Constructors, OuterSkipped;
        internal long AdmissionTicks, ReadTicks, ConstructorTicks, OuterTicks, ParseTicks;
    }
}
