// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FixtureObserver17;

// Independent timer and native exit path are identical in controls/candidates.
// Content capture happens after the measured menu endpoint, not inside it.
public sealed class ObserverMod : Mod
{
    private static string? output;
    private static string runId = "", mode = "";
    private static long start, frequency, menuCounter;
    private static bool menu, contentCaptured, requested, failed;
    private static int gameplayStage, savedTick, savedPawns, settleFrames;
    private static string? loadedSave;
    private static long gameplayCounter;
    private static Game? savedGame;
    private static long completedGameplayCounter;

    public ObserverMod(ModContentPack content) : base(content)
    {
        if (output != null) return;
        string? selected = Arg("wakeFixtureDir");
        if (selected == null) return;
        string root = Path.GetFullPath(Path.Combine(content.RootDir, "..", "..", "..", "results"));
        selected = Path.GetFullPath(selected);
        if (!selected.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Observer output is outside its isolated lane.");
        output = selected;
        using JsonDocument clock = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "launch-clock.json")));
        runId = clock.RootElement.GetProperty("runId").GetString()!;
        start = clock.RootElement.GetProperty("counter").GetInt64();
        frequency = clock.RootElement.GetProperty("frequency").GetInt64();
        if (runId != Arg("wakeFixtureRun") || frequency != Stopwatch.Frequency || start > Stopwatch.GetTimestamp())
            throw new InvalidOperationException("Observer launch clock mismatch.");
        mode = Arg("wakeFixtureMode") ?? "menu";
        loadedSave = Arg("wakeFixtureSave");
        string saves = Path.GetFullPath(GenFilePaths.FilePathForSavedGame("WakeFixtureRoundTrip"));
        string profiles = Path.GetFullPath(Path.Combine(root, "..", "profiles"));
        if (!saves.StartsWith(profiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native save routing is outside the isolated profiles.");
        var harmony = new Harmony("local.fixture.wakeup17observer");
        harmony.Patch(AccessTools.Method(typeof(LoadedModManager), "ApplyPatches"),
            prefix: new HarmonyMethod(typeof(ObserverMod), nameof(XmlBegin)) { priority = Priority.First },
            finalizer: new HarmonyMethod(typeof(ObserverMod), nameof(XmlEnd)) { priority = Priority.Last });
        harmony.Patch(AccessTools.Method(typeof(MainMenuDrawer), "MainMenuOnGUI"),
            postfix: new HarmonyMethod(typeof(ObserverMod), nameof(MenuDrawn)) { priority = Priority.Last });
        harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"),
            postfix: new HarmonyMethod(typeof(ObserverMod), nameof(EntryUpdate)) { priority = Priority.Last });
        harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"),
            postfix: new HarmonyMethod(typeof(ObserverMod), nameof(PlayUpdate)) { priority = Priority.Last });
        Log.Message("[Fixture17] observer installed run=" + runId);
    }

    private static string? Arg(string name) => Environment.GetCommandLineArgs()
        .FirstOrDefault(a => a.StartsWith("-" + name + "=", StringComparison.OrdinalIgnoreCase))?
        .Substring(name.Length + 2);

    private static void XmlBegin(out long __state) => __state = Stopwatch.GetTimestamp();
    private static void XmlEnd(long __state, Exception? __exception)
    {
        Write("xml-stage", new { elapsedSeconds = (Stopwatch.GetTimestamp() - __state) / (double)frequency,
            definition = "ApplyPatches invocation including supplier callbacks", error = __exception?.ToString() });
    }

    private static void Write(string name, object value)
        => File.WriteAllText(Path.Combine(output!, name + ".json"), JsonSerializer.Serialize(value,
            new JsonSerializerOptions { WriteIndented = true }));

    private static void MenuDrawn(bool __runOriginal)
    {
        if (output == null || menu || !__runOriginal || Event.current?.type != EventType.Repaint
            || Current.ProgramState != ProgramState.Entry || !PlayDataLoader.Loaded) return;
        menu = true;
        menuCounter = Stopwatch.GetTimestamp();
        Write("menu-ready", new { runId, processId = Environment.ProcessId, utc = DateTime.UtcNow,
            definition = "first-completed-main-menu-repaint", elapsedSeconds = (menuCounter - start) / (double)frequency });
        Log.Message("[Fixture17] menu-ready run=" + runId);
    }

    private static void EntryUpdate()
    {
        if (output != null && !requested && File.Exists(Path.Combine(output, "abort-request.txt")))
        {
            requested = true;
            Write("functional", new { passed = false, error = "Owned fixture recovery requested; no successful sample." });
            Root.Shutdown();
            return;
        }
        if (output == null || !menu || requested || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            if (!contentCaptured)
            {
                if (++settleFrames < 3) return;
                CaptureContent();
                contentCaptured = true;
            }
            if (mode == "menu")
            {
                if (Stopwatch.GetTimestamp() - menuCounter < frequency * 5) return;
                requested = true;
                Root.Shutdown();
            }
            else if (gameplayStage == 0)
            {
                gameplayStage = 1;
                gameplayCounter = Stopwatch.GetTimestamp();
                Prefs.PauseOnLoad = true;
                if (mode == "load-save") GameDataSaveLoader.LoadGame(loadedSave!);
                else LongEventHandler.QueueLongEvent(() =>
                {
                    Rand.PushState(1704977);
                    try
                    {
                        Root_Play.SetupForQuickTestPlay();
                        Find.GameInitData.mapSize = 100;
                        PageUtility.InitGameStart();
                    }
                    finally { Rand.PopState(); }
                }, "GeneratingMap", true, Fail);
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void PlayUpdate()
    {
        if (output == null || gameplayStage == 0 || requested || failed || LongEventHandler.AnyEventNowOrWaiting
            || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
        try
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            if (gameplayStage == 3)
            {
                if (Stopwatch.GetTimestamp() - completedGameplayCounter < frequency * 15) return;
                requested = true;
                Root.Shutdown();
                return;
            }
            if (++settleFrames < 8) return;
            if (gameplayStage == 1)
            {
                for (int i = 0; i < 120; i++) Find.TickManager.DoSingleTick();
                // Actual alert construction occurs on play entry; touch the live list.
                if (Find.CurrentMap.mapPawns.FreeColonistsSpawnedCount <= 0)
                    throw new InvalidOperationException("No living spawned colonists.");
                savedPawns = Find.CurrentMap.mapPawns.FreeColonistsSpawnedCount;
                savedTick = Find.TickManager.TicksGame;
                savedGame = Current.Game;
                GameDataSaveLoader.SaveGame("WakeFixtureRoundTrip");
                string path = GenFilePaths.FilePathForSavedGame("WakeFixtureRoundTrip");
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                    throw new InvalidOperationException("Native save was not written.");
                Write("gameplay-before-reload", new { savedTick, savedPawns, map = Find.CurrentMap.uniqueID,
                    initialColonySeconds = (Stopwatch.GetTimestamp() - gameplayCounter) / (double)frequency });
                gameplayStage = 2;
                settleFrames = 0;
                GameDataSaveLoader.LoadGame("WakeFixtureRoundTrip");
            }
            else
            {
                if (ReferenceEquals(Current.Game, savedGame)) return;
                int loadedTick = Find.TickManager.TicksGame;
                int loadedPawns = Find.CurrentMap.mapPawns.FreeColonistsSpawnedCount;
                Write("gameplay-after-reload", new { loadedTick, loadedPawns, savedTick, savedPawns });
                // Native play entry performs its initialization tick before this
                // Update postfix. Compare the persisted state plus that boundary.
                if (loadedTick < savedTick || loadedTick > savedTick + 1 || loadedPawns != savedPawns)
                    throw new InvalidOperationException("Native save/reload changed tick or colonist count.");
                for (int i = 0; i < 120; i++) Find.TickManager.DoSingleTick();
                Write("functional", new { passed = true, mode, savedTick, savedPawns,
                    loadedTick, loadedPawns,
                    ticksAfterReload = Find.TickManager.TicksGame, nativeSaveReloadMatched = true,
                    colonyAndRoundTripSeconds = (Stopwatch.GetTimestamp() - gameplayCounter) / (double)frequency });
                gameplayStage = 3;
                completedGameplayCounter = Stopwatch.GetTimestamp();
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Fail(Exception error)
    {
        failed = true;
        Write("functional", new { passed = false, error = error.ToString(), mode });
        Log.Error("[Fixture17] functional failure: " + error);
        requested = true;
        Root.Shutdown();
    }

    private static object? Value(object? value, int depth)
    {
        if (value == null) return null;
        if (value is string || value is bool) return value;
        if (value is Def def) return def.GetType().FullName + ":" + def.defName;
        if (value is Type type) return type.FullName;
        if (value is Enum || value.GetType().IsPrimitive || value is decimal)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        if (depth == 0) return value.GetType().FullName;
        if (value is IEnumerable sequence)
            return sequence.Cast<object?>().Take(4096).Select(v => Value(v, depth - 1)).ToArray();
        return value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(f => f.DeclaringType!.FullName + "." + f.Name, StringComparer.Ordinal)
            .ToDictionary(f => f.DeclaringType!.FullName + "." + f.Name, f => Value(f.GetValue(value), depth - 1));
    }

    private static void CaptureContent()
    {
        var defs = new SortedDictionary<string, Def>(StringComparer.Ordinal);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (Type type in GenDefDatabase.AllDefTypesWithDatabases())
        {
            List<Def> entries = GenDefDatabase.GetAllDefsInDatabaseForDef(type).ToList();
            counts[type.FullName!] = entries.Count;
            foreach (Def def in entries)
            {
                string key = def.GetType().FullName + ":" + def.defName;
                defs[key] = def;
            }
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (var file = File.Create(Path.Combine(output!, "definitions.jsonl")))
        foreach (var row in defs)
        {
            var fields = row.Value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                // Debug IDs are intentionally random on every native load.
                .Where(f => f.Name != "modContentPack" && f.Name != "index" && f.Name != "shortHash" && f.Name != "debugRandomId")
                .OrderBy(f => f.DeclaringType!.FullName + "." + f.Name, StringComparer.Ordinal)
                .ToDictionary(f => f.DeclaringType!.FullName + "." + f.Name, f => Value(f.GetValue(row.Value), 2));
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { key = row.Key, fields });
            file.Write(bytes); file.WriteByte(10);
            hash.AppendData(bytes); hash.AppendData(new byte[] { 10 });
        }
        Write("content", new { counts, projectedDefs = defs.Count,
            definitionProjectionSha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            loadedMods = LoadedModManager.RunningModsListForReading.Select(m => new { id = m.PackageId, root = m.RootDir }).ToArray(),
            language = LanguageDatabase.activeLanguage.folderName,
            gameMvid = typeof(Mod).Assembly.ManifestModule.ModuleVersionId });
        var targets = new[] { "Verse.LoadedModManager:ApplyPatches", "Verse.XmlToObjectUtils:DoFieldSearch",
            "Verse.GenTypes:AllSubclasses", "HarmonyLib.AccessTools:TypeByName" };
        Write("patch-owners", targets.ToDictionary(name => name, name =>
        {
            MethodInfo? target = AccessTools.Method(name);
            Patches? info = target == null ? null : Harmony.GetPatchInfo(target);
            object[] Describe(IEnumerable<Patch>? patches) => patches?.Select(p => (object)new {
                p.owner, method = p.PatchMethod.DeclaringType?.FullName + "." + p.PatchMethod.Name,
                assembly = p.PatchMethod.Module.Assembly.GetName().Name,
                mvid = p.PatchMethod.Module.ModuleVersionId, location = p.PatchMethod.Module.Assembly.Location
            }).ToArray() ?? Array.Empty<object>();
            return new { prefixes = Describe(info?.Prefixes), postfixes = Describe(info?.Postfixes),
                transpilers = Describe(info?.Transpilers), finalizers = Describe(info?.Finalizers) };
        }));
    }
}
