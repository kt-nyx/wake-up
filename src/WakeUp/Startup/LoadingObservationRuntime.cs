// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Native DeepProfiler calls are execution markers, including calls inside
// iterators and deferred actions. Recording these never enables the native
// profiler, changes its flags, or treats an iterator factory as completed work.
internal static class LoadingObservationRuntime
{
    internal const string Owner = "wakeup.loading-observation";
    private static readonly object Gate = new();
    private static readonly List<LoadingSession> sessions = new();
    private static readonly Dictionary<Assembly, string> packages = new();
    private static readonly Dictionary<string, string> modLabels = new(StringComparer.Ordinal);
    private static bool installed, selected, timings;
    private static string root = "";
    private static LoadingSession? current;
    [ThreadStatic] private static Stack<LoadingSession.Token?>? markers;
    [ThreadStatic] private static string? markerSessionId;
    [ThreadStatic] private static int markerOverflow;
    [ThreadStatic] private static string? package;
    [ThreadStatic] private static bool observing;
    internal static bool DisplaySelected { get; private set; }
    internal static string Status { get; private set; } = "Loading observation is off.";
    private static string invocationFailure = "";
    private static string InvocationStatus => invocationFailure.Length != 0 ? invocationFailure : LoadingInvocationObservation.Status;
    internal static string ReportDirectory => Path.Combine(root, "WakeUp", "LoadingReports");
    internal static IReadOnlyList<LoadingSession> Sessions { get { lock (Gate) return sessions.ToArray(); } }
    internal static LoadingSession? Current { get { lock (Gate) return current; } }
    internal static LoadingSession? Latest { get { lock (Gate) return sessions.LastOrDefault(); } }

    internal static void Configure(bool display, bool record, string saveRoot)
    {
        if (display && LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == LoadingProgressCompatibility.PackageId)) display = false;
        root = saveRoot; selected = display || record; timings = record;
        if (!selected || installed) return;
        var harmony = new Harmony(Owner);
        try
        {
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason)) throw new InvalidOperationException(reason);
            foreach (var pair in new[] {
                ("Start", "17C58814F47FBF4EB78FD6813765AE1259BE073922A1D79AFFF84736C9413F02"),
                ("End", "36BD002AAFA158737893BE223194DD2B5CD9A506E24E7E8FA297F3E760692EB8") })
            {
                var method = AccessTools.Method(typeof(DeepProfiler), pair.Item1);
                if (!SemanticMethodIdentity.TryHash(method, out string hash, out _) || hash != pair.Item2)
                    throw new InvalidOperationException("Native execution markers changed: " + pair.Item1);
                harmony.Patch(method, prefix: Hook(pair.Item1 == "Start" ? nameof(MarkerStart) : nameof(MarkerEnd)));
            }
            harmony.Patch(AccessTools.Method(typeof(Log), "Error", new[] { typeof(string) }), prefix: Hook(nameof(ObserveError)));
            // This call may be suppressed by a supplier after it reloads content.
            // Its boundary includes hooks; nested execution markers carry costs.
            harmony.Patch(AccessTools.Method(typeof(ModContentPack), "ReloadContentInt"),
                prefix: Hook(nameof(BeforeContent)), finalizer: Hook(nameof(AfterNativeContent)));
            foreach (Type factory in new[] { typeof(DirectXmlToObjectNew), typeof(DirectXmlLoader) })
                harmony.Patch(AccessTools.Method(factory, factory == typeof(DirectXmlLoader) ? "DefFromNode" : "DefFromNodeNew"),
                    prefix: Hook(nameof(BeforeDef)), finalizer: Hook(nameof(AfterContent)));
            harmony.Patch(AccessTools.Method(typeof(LongEventHandler), "LongEventsUpdate"),
                prefix: Hook(nameof(BeforeUpdate)), finalizer: Hook(nameof(AfterUpdate)));
            installed = true;
            RefreshPackages();
            StartSession("Startup");
            try { LoadingInvocationObservation.Install(); }
            catch (Exception error)
            {
                invocationFailure = "Individual invocation observation unavailable: " + error.GetBaseException().Message;
                Current?.NoteUnobserved(invocationFailure);
            }
            Current?.NoteUnobserved("Bootstrap, assembly discovery and core static constructors before the first Mod constructor are not observed. Every Mod constructor from the early boundary is included.");
            Status = "Recording native loading execution. Work before the first mod constructor is unobserved.";
        }
        catch (Exception error)
        {
            selected = false;
            try { harmony.UnpatchAll(Owner); } catch { }
            Status = "Loading observation unavailable: " + error.Message + ". Ordinary loading continues.";
        }
    }

    internal static void Attach(string[] args)
    {
        Configure(LoadingDisplayRuntime.Selected(args), LoadingTimingRuntime.Selected(args), GenFilePaths.SaveDataFolderPath);
        LoadingDiagnosticsRuntime.Initialize(GenFilePaths.SaveDataFolderPath);
    }
    internal static void SetDisplaySelected(bool value) => DisplaySelected = value;
    private static HarmonyMethod Hook(string name) => new(typeof(LoadingObservationRuntime), name);

    internal static bool AllowsHook(MethodBase target, Patch patch)
    {
        if (LoadingInvocationObservation.AllowsHook(target, patch)) return true;
        if (patch.owner != Owner || patch.PatchMethod.Module != typeof(LoadingObservationRuntime).Module) return false;
        string? expected = target.DeclaringType == typeof(DeepProfiler) ? target.Name == "Start" ? nameof(MarkerStart) : target.Name == "End" ? nameof(MarkerEnd) : null
            : target.DeclaringType == typeof(Log) && target.Name == "Error" ? nameof(ObserveError)
            : target.DeclaringType == typeof(ModContentPack) && target.Name == "ReloadContentInt" ? patch.PatchMethod.Name == nameof(BeforeContent) ? nameof(BeforeContent) : nameof(AfterNativeContent)
            : target.DeclaringType == typeof(LongEventHandler) && target.Name == "LongEventsUpdate" ? patch.PatchMethod.Name == nameof(BeforeUpdate) ? nameof(BeforeUpdate) : nameof(AfterUpdate) : null;
        if (expected == null || patch.PatchMethod != AccessTools.Method(typeof(LoadingObservationRuntime), expected)) return false;
        var record = Harmony.GetPatchInfo(target);
        if (record == null) return false;
        bool finalizer = expected == nameof(AfterNativeContent) || expected == nameof(AfterUpdate);
        return (finalizer ? record.Finalizers : record.Prefixes).Count(p => p.PatchMethod == patch.PatchMethod && p.owner == Owner) == 1
            && record.Prefixes.Concat(record.Postfixes).Concat(record.Transpilers).Concat(record.Finalizers)
                .Concat(record.InnerPrefixes).Concat(record.InnerPostfixes).Count(p => p.PatchMethod == patch.PatchMethod) == 1;
    }

    private static void RefreshPackages()
    {
        lock (Gate)
        {
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                foreach (var assembly in mod.assemblies.loadedAssemblies)
                    if (!packages.ContainsKey(assembly)) packages.Add(assembly, mod.PackageId);
                modLabels["Loading " + mod] = mod.PackageId;
                modLabels["Loading " + mod + " content"] = mod.PackageId;
            }
        }
    }
    internal static string PackageFor(Type? type)
    {
        if (type == null) return "shared/unknown";
        lock (Gate) return packages.TryGetValue(type.Assembly, out string value) ? value : "shared/unknown";
    }
    internal static LoadingSession.Token? Begin(string stage, string work, string mod = "shared/unknown")
    {
        try { return Current?.Begin(stage, work, mod); } catch { return null; }
    }
    internal static void End(LoadingSession.Token? token, Exception? error = null)
    {
        try { Current?.End(token, error); } catch { }
    }
    internal static void Progress(string stage, string work, long completed, long? total)
    {
        try { Current?.Progress(stage, work, completed, total); } catch { }
    }

    private static void StartSession(string name)
    {
        lock (Gate)
        {
            if (current?.IsActive == true) return;
            current = new LoadingSession(name, timings);
            sessions.Add(current);
            if (sessions.Count > 8) sessions.RemoveAt(0);
        }
    }
    internal static void Finish(string outcome)
    {
        LoadingSession? ended;
        lock (Gate) { ended = current; current = null; }
        if (ended == null) return;
        ended.Finish(outcome);
        LoadingDiagnosticsRuntime.Complete();
        Status = outcome + " Inspect loading sessions in Wake-Up settings.";
        if (timings)
        {
            try { SaveReport(ended); }
            catch (Exception error) { Status += " Report remains in memory; save failed: " + error.GetType().Name; }
        }
    }
    internal static string SaveReport(LoadingSession session)
    {
        Directory.CreateDirectory(ReportDirectory);
        string path = Path.Combine(ReportDirectory, session.Id + ".txt");
        File.WriteAllText(path, session.RenderReport() + "\nLaunch feature status:\n" + LoadingFeatureFeedback.Snapshot()
            + "\n" + AtlasBatchScheduling.Status + "\n" + NativeLoadingUnits.Status + "\n" + InvocationStatus + "\n" + LoadingDiagnosticsRuntime.Status
            + "\nNative assets remain eager except for specifically selected existing experimental paths; broad C10/C11 asset features are unfinished.\n");
        var retained = new DirectoryInfo(ReportDirectory).GetFiles("*.txt")
            .Where(f => Guid.TryParse(Path.GetFileNameWithoutExtension(f.Name), out _)).OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
        // Only our generated session names are managed; never touch arbitrary files.
        foreach (var old in retained.Skip(8))
            if (Guid.TryParse(Path.GetFileNameWithoutExtension(old.Name), out _)) old.Delete();
        long size = retained.Take(8).Where(f => f.Exists).Sum(f => f.Length);
        foreach (var old in retained.Take(8).Reverse())
        {
            if (size <= 128L * 1024 * 1024 || old.FullName == path) break;
            size -= old.Length; old.Delete();
        }
        return path;
    }

    private static void BeforeUpdate()
    {
        try
        {
            if (selected && installed && Current == null && LongEventHandler.AnyEventNowOrWaiting)
                StartSession(PlayDataLoader.Loaded ? "Later loading" : "Startup recovery");
        }
        catch { }
    }
    private static void AfterUpdate(Exception? __exception)
    {
        try
        {
            if (__exception != null) { Current?.Error(__exception.ToString()); Finish("Loading interrupted by a native exception."); }
            else if (Current != null && !LongEventHandler.AnyEventNowOrWaiting && !BackgroundLoadingRuntime.Session.AwaitingPlay)
                Finish("Native loading queue completed. Logged errors and unobserved regions are listed below; this is not a gameplay-success receipt.");
        }
        catch { }
    }
    private static void MarkerStart(string? __0)
    {
        if (observing) return;
        observing = true;
        try
        {
            var session = Current;
            if (session == null) return;
            if (markerSessionId != session.Id) { markers = new Stack<LoadingSession.Token?>(); markerSessionId = session.Id; markerOverflow = 0; }
            if (markers!.Count >= 128) { markerOverflow++; session.NoteUnobserved("Native execution marker depth exceeded 128; inner markers omitted."); return; }
            string label = __0 ?? "Unnamed native work";
            var context = session.CurrentThreadContext();
            string attribution = context?.Package ?? package ?? "shared/unknown";
            lock (Gate) if (modLabels.TryGetValue(label, out string found)) attribution = found;
            var token = session.Begin(Stage(label, context?.Stage), label, attribution);
            markers!.Push(token);
        }
        catch { }
        finally { observing = false; }
    }
    private static void MarkerEnd()
    {
        if (observing) return;
        observing = true;
        try
        {
            var session = Current;
            if (session == null) return;
            if (markerSessionId != session.Id || markers?.Count == 0) { session.NoteUnobserved("Native end marker began before observation or had no matching start."); return; }
            if (markerOverflow > 0) { markerOverflow--; return; }
            session.End(markers!.Pop(), null);
        }
        catch { }
        finally { observing = false; }
    }
    // These are native operation names, not words found anywhere in user/mod
    // labels. Reviewed PlayDataLoader/LoadedModManager/ModContentPack and the
    // captured GOG startup + world/map/save sessions supply the exact forms.
    // Arbitrary type names, package IDs and callback signatures inherit their
    // live same-thread scope, or remain unknown when no such scope is observed.
    internal static string Stage(string label, string? parentStage = null)
    {
        switch (label)
        {
            case "LoadAllPlayData":
            case "Load all active mods.": return "Play data loading";
            case "InitializeMods()": return "Mod initialization";
            case "CreateModClasses()": return "Mod constructors";
            case "LoadModXML()":
            case "Load defs via DirectXmlLoader":
            case "Parse loaded defs": return "XML read";
            case "CombineIntoUnifiedXML()": return "XML combination";
            case "ErrorCheckPatches()":
            case "ApplyPatches()":
            case "ClearCachedPatches()":
            case "Loading all patches": return "XML patches";
            case "ParseAndProcessXML()": return "XML / Def loading";
            case "XmlInheritance.TryRegister":
            case "XmlInheritance.Resolve()":
            case "XmlInheritance.Clear()":
            case "RecursiveNodeCopyOverwriteElements":
            case "RecursiveNodeCopyOverwriteElements - Remove all current nodes": return "XML inheritance";
            case "CreateFieldSetterForType":
            case "CreateListItemAdderForType": return "Def creation";
            case "Load language metadata.":
            case "TKeySystem.Parse()":
            case "TKeySystem.BuildMappings()":
            case "Legacy backstory translations.":
            case "Inject selected language data into game data (early pass).":
            case "Inject selected language data into game data.": return "Language";
            case "ResolveAllCrossReferences()":
            case "Resolve cross-references.":
            case "Resolve cross-references between non-implied Defs.":
            case "Resolve cross-references between Defs made by the implied defs.":
            case "Resolve references.":
            case "ThingCategoryDef resolver":
            case "RecipeDef resolver":
            case "Static resolver calls":
            case "ThingDef resolver": return "Cross-references";
            case "Copy all Defs from mods to global databases.":
            case "Rebind DefOfs (early).":
            case "Rebind DefOfs (final).":
            case "Global operations (early pass).":
            case "Generate implied Defs (pre-resolve).":
            case "Generate implied Defs (post-resolve).":
            case "Other def binding, resetting and global operations (pre-resolve).":
            case "Other def binding, resetting and global operations (post-resolve).":
            case "Error check all defs.":
            case "Short hash giving.": return "Definitions and references";
            case "LoadModContent()":
            case "LoadModContent":
            case "Reload textures":
            case "Reload strings":
            case "Reload asset bundles":
            case "GraphicDatabase.Clear()":
            case "Load all bios": return "Content";
            case "Reload audio clips": return "Audio";
            case "Atlas baking.":
            case "StaticTextureAtlas.Bake()":
            case "StaticTextureAtlas.CalcRectsForAtlasNew()":
            case "Compress atlas textures": return "Atlas baking";
            case "Generate mipmaps with compute shader": return parentStage == "Atlas baking" ? "Atlas baking" : "Content";
            case "Static constructor calls":
            case "StaticConstructorOnStartupUtility.CallAll()": return "Static constructors";
            case "ExecuteToExecuteWhenFinished()": return "Deferred callbacks";
            case "GenerateWorld":
            case "World.FinalizeInit": return "World loading / generation";
            case "InitNewGeneratedMap":
            case "Set up map":
            case "Generate contents into map":
            case "Finalize map init":
            case "Map generator post init":
            case "maps.FinalizeLoading":
            case "Load compressed things":
            case "Load non-compressed things":
            case "Merge compressed and non-compressed thing lists":
            case "Spawn everything into the map":
            case "Thing.PostMapInit()": return "Map loading / generation";
            case "InitLoading (read file)":
            case "Scribe.loader.FinalizeLoading": return "Save loading";
            case "DoAllPostLoadInits()": return "Post-load initialization";
            case "Game.FinalizeInit": return "Game initialization";
            case "Misc Init (InitializingInterface)":
            case "Instantiate UIRoot":
            case "Instantiating Alerts": return "Interface initialization";
            case "Load keyboard preferences.": return "Preferences";
            case "Garbage Collection": return "Cleanup";
        }
        if (label.StartsWith("ParseValueAndReturnDef (for ", StringComparison.Ordinal) && label.EndsWith(")", StringComparison.Ordinal)) return "Def creation";
        if (label.StartsWith("Loading defs for ", StringComparison.Ordinal) && label.EndsWith(" nodes", StringComparison.Ordinal)) return "Def creation";
        if (label.StartsWith("Loading asset nodes ", StringComparison.Ordinal)) return "XML / Def loading";
        if (label.StartsWith("Loading language data: ", StringComparison.Ordinal)) return "Language";
        if (label.StartsWith("ResolveAllReferences ", StringComparison.Ordinal)) return "Cross-references";
        if (label.StartsWith("Loading ", StringComparison.Ordinal) && label.EndsWith(" mod class", StringComparison.Ordinal)) return "Mod constructors";
        if (label.StartsWith("Loading game from file ", StringComparison.Ordinal)) return "Save loading";
        if (label.StartsWith("WorldGen - ", StringComparison.Ordinal) || label.StartsWith("WorldGenStep - ", StringComparison.Ordinal)) return "World loading / generation";
        if (label.StartsWith("GenStep - ", StringComparison.Ordinal)) return "Map loading / generation";
        return parentStage ?? "Native loading work";
    }
    private static void ObserveError(string __0)
    {
        if (observing) return;
        try { Current?.Error(__0); } catch { }
    }
    internal sealed class ContentScope
    {
        internal LoadingSession.Token? Token;
        internal string? Previous;
    }
    private static void BeforeContent(ModContentPack __instance, out ContentScope? __state)
    {
        __state = null;
        try
        {
            __state = new ContentScope { Previous = package };
            package = __instance.PackageId;
            __state.Token = Begin("Native content call boundaries", "ReloadContentInt invocation including hooks (original body may be skipped)", package);
        }
        catch { }
    }
    private static void AfterNativeContent(ContentScope? __state, Exception? __exception, bool __runOriginal)
    {
        try
        {
            if (!__runOriginal && __state?.Token != null)
                Current?.NoteUnobserved("ReloadContentInt original body skipped for " + __state.Token.Package
                    + "; its native call boundary contains hooks only. Replacement work is reported separately where observed.");
        }
        catch { }
        finally { AfterContent(__state, __exception); }
    }
    private static void AfterContent(ContentScope? __state, Exception? __exception)
    {
        if (__state == null) return;
        End(__state.Token, __exception); package = __state.Previous;
    }
    private static void BeforeDef(XmlNode __0, LoadableXmlAsset? __1, out ContentScope? __state)
    {
        __state = null;
        try
        {
            __state = new ContentScope { Previous = package };
            package = __1?.mod?.PackageId ?? "shared/unknown";
            __state.Token = Begin("Def creation", __0.Name + " " + (__0["defName"]?.InnerText ?? "unnamed"), package);
        }
        catch { }
    }
}
