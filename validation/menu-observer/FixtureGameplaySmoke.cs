// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

// Explicitly selected fixture-only create/tick/save/reload check. This assembly
// is never shipped with Wake-Up. It runs after the independently measured menu,
// uses native game APIs and exits normally. It drives no UI and imports no save.
public sealed class FixtureGameplaySmoke : MonoBehaviour
{
    private static string directory = "";
    private const string SaveName = "RloFixtureSmoke";
    private int stage;
    private long started = Stopwatch.GetTimestamp();
    private int savedTick;
    private string[] pawnIds = Array.Empty<string>();
    private string mapId = "";
    private Map? originalMap;

    internal static void Start(string evidenceDirectory)
    {
        directory = evidenceDirectory;
        var host = new GameObject("FixtureGameplaySmoke");
        DontDestroyOnLoad(host);
        host.AddComponent<FixtureGameplaySmoke>();
    }

    private void Update()
    {
        if (stage < 0) return;
        try
        {
            if ((Stopwatch.GetTimestamp()-started)/(double)Stopwatch.Frequency > 540)
                throw new TimeoutException("Fixture gameplay smoke exceeded nine minutes.");
            if (LongEventHandler.AnyEventNowOrWaiting) return;
            if (stage == 0)
            {
                CheckSettingsRoundTrip();
                stage = 1;
                LongEventHandler.QueueLongEvent(PrepareGame, "Play", "GeneratingWorld", true, Fail);
                return;
            }
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
            if (stage == 1)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                for (int i=0;i<600;i++) Find.TickManager.DoSingleTick();
                originalMap = Find.CurrentMap;
                if (originalMap.Size.x != 75 || originalMap.Size.z != 75) throw new InvalidOperationException("Unexpected map size.");
                pawnIds = originalMap.mapPawns.FreeColonistsSpawned.Select(p=>p.GetUniqueLoadID()).OrderBy(x=>x).ToArray();
                if (pawnIds.Length != 3) throw new InvalidOperationException("Expected three spawned crashlanded colonists.");
                savedTick = Find.TickManager.TicksGame;
                mapId = originalMap.GetUniqueLoadID();
                GameDataSaveLoader.SaveGame(SaveName);
                if (!File.Exists(GenFilePaths.FilePathForSavedGame(SaveName))) throw new IOException("Save was not created.");
                File.WriteAllText(Path.Combine(directory,"gameplay-before-reload.json"),"{\"ticks\":"+savedTick+",\"colonists\":3,\"mapSize\":75}");
                stage = 2;
                GameDataSaveLoader.LoadGame(SaveName);
                return;
            }
            if (stage == 2 && !ReferenceEquals(originalMap, Find.CurrentMap))
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                string[] restored = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Select(p=>p.GetUniqueLoadID()).OrderBy(x=>x).ToArray();
                if (Find.CurrentMap.GetUniqueLoadID()!=mapId || !restored.SequenceEqual(pawnIds)
                    || Find.TickManager.TicksGame < savedTick) throw new InvalidOperationException("Save/reload state mismatch.");
                for (int i=0;i<60;i++) Find.TickManager.DoSingleTick();
                File.WriteAllText(Path.Combine(directory,"gameplay-smoke.json"),"{\"passed\":true,\"createdMap\":true,\"mapSize\":75,\"colonists\":3,\"ticksBeforeSave\":"+savedTick+",\"ticksAfterReload\":"+Find.TickManager.TicksGame+",\"saveReloadMatched\":true}");
                stage = -1;
                Root.Shutdown();
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void PrepareGame()
    {
        // Same initialization order as native quick-test play, using a small
        // deterministic world/map to keep this a focused compatibility check.
        Rand.Seed = 19840117;
        Current.ProgramState = ProgramState.Entry;
        Game.ClearCaches();
        Current.Game = new Game();
        Current.Game.InitData = new GameInitData();
        Current.Game.Scenario = ScenarioDefOf.Crashlanded.scenario;
        Find.Scenario.PreConfigure();
        Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
        Current.Game.World = WorldGenerator.GenerateWorld(0.05f,"RloFixtureSmoke",OverallRainfall.Normal,OverallTemperature.Normal,OverallPopulation.Normal,LandmarkDensity.Normal);
        Find.GameInitData.ChooseRandomStartingTile();
        Find.GameInitData.mapSize = 75;
        Find.Scenario.PostIdeoChosen();
        Find.GameInitData.PrepForMapGen();
        Find.Scenario.PreMapGenerate();
    }

    private static void CheckSettingsRoundTrip()
    {
        Type? type = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="WakeUp")
            ?.GetType("WakeUp.WakeUpMod");
        if (type == null) return; // Physically absent control.
        Mod mod = LoadedModManager.GetMod(type);
        Type settingsType = type.Assembly.GetType("WakeUp.WakeUpSettings",true)!;
        var get = AccessTools.Method(typeof(Mod),"GetSettings").MakeGenericMethod(settingsType);
        object settings = get.Invoke(mod,null);
        FieldInfo field = settingsType.GetField("PngCache")!;
        bool original = (bool)field.GetValue(settings);
        field.SetValue(settings,!original);
        mod.WriteSettings();
        // Read through the game's own loader, not the cached Mod settings field.
        MethodInfo read = AccessTools.Method(typeof(LoadedModManager),"ReadModSettings").MakeGenericMethod(settingsType);
        object reloaded = read.Invoke(null,new object[]{mod.Content.FolderName,type.Name});
        bool matched = (bool)field.GetValue(reloaded)==!original;
        field.SetValue(settings,original);
        mod.WriteSettings();
        if (!matched) throw new InvalidOperationException("Settings did not persist through the native loader.");
        File.WriteAllText(Path.Combine(directory,"settings-smoke.json"),"{\"passed\":true,\"originalValueRestored\":true}");
    }

    private void Fail(Exception error)
    {
        stage = -1;
        File.WriteAllText(Path.Combine(directory,"gameplay-smoke.json"),"{\"passed\":false}");
        File.WriteAllText(Path.Combine(directory,"gameplay-smoke-error.txt"),error.ToString());
        Log.Error("[FixtureGameplaySmoke] " + error);
        Root.Shutdown();
    }
}
