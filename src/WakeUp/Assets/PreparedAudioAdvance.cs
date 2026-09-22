// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WakeUp;

// During native map loading, prepare only the current biome's existing ambient
// bank before Notify_SwitchedMap queues its ordinary sustainer creation callback.
internal static class PreparedAudioAdvance
{
    internal const int MaximumDefs = 8, MaximumClips = 16, MaximumSubSounds = 16, MaximumGrains = 64;
    // Derived offline from the pinned GOG assembly, then independently through
    // our owned prepatch pipeline and Cecil serialization. These bodies agree.
    internal static readonly string[] Expected = {
        "4C1A3EAAAEEE2D456F32624C7825127A9715F59BAE5EDD842C48A218407C5F1A",
        "8447B0CB9C4AFA0ED8CA7644BB9D63E2A2FB4CA4C4FA414F535C4CA3B813B52D",
        "255907211728E3C37B58485BBC25482F1E78F2F2ED55BDD2CC930913009F01A5",
        "074C69DB5382FB3A8AFCB2CC6BF10A655E50D897583F338071D09E3E7D58CCAE",
        "3C6E54206239675B1156BE6DD82B0C547E71C117CFC8CB74EED0AA24178A5BB4", // Find.CurrentMap
        "274E38485ADB603396550BA848ED7F989D328BE9F6F1593F3FCF326587B36044", // Current.Game
        "B2E6A87B7F6A6F4E346495F6636F36E9C4FDD2CE3B470D72137B45635F987F9B", // Game.CurrentMap
        "BBDE832A0B6415E0F4E696B39207AF35B674D572DB34722582B2B2374A47C856", // Map.Biome
        "4335692258CE83A84498E50CF4BFEE7CE3A3B002DB5DE9006A5AEA6907F5BDF0", // Map.TileInfo
        "F80C70E31B09C663EB8403B152CC36484F3C1967712157B657BB8AE19AFADB4C", // Map.IsPocketMap
        "B366D2C7348ACC57E06ACE86ACB33791B99B0E5F43E91DBFC444244D6E59C52E", // Map.Tile
        "2EF8F4B7D4D90746B243E0AC90754F28FF28404D291325AB34B211F706CCDF7B", // MapInfo.Tile
        "603BB9A9AF14CF1BDFC8F58C2B517B530DAFD8F7E498748143A48A7C2EB63CE2", // WorldObject.Tile
        "8376B968D1E786CB49B1307E30CAE0407F7694F7269C49CDB37802E37A53B091", // Find.WorldGrid
        "3C54368D727226E4D2F976A0F12E2DFD4462F90A728CA5BB30F88CCB15488DAA", // Find.World
        "B63673282BD1AA1A8FCE4BFA8370282223BF4A0597979CE734B5EA0A52A58159", // Game.World
        "05E3618F82C0D4C2E5432E0703D216161C695889F237B17BAB95427730BF3A92", // WorldGrid[PlanetTile]
        "50E699F5F8E2E0571CE12B84EADC6CB10076C0806C505DF5FCE2B371BB230DA0", // PlanetTile.Layer
        "B3FAB614375BB8E22622FB30AD489A1F29E6D3921007CAC2AFCC2E4454FDA20F", // WorldGrid.Surface
        "68D93C7A89B9D26DFF7521CC3F9C73EBC7AA73DFED83D55546EE2260AF3E6968", // WorldGrid.PlanetLayers
        "71EA4B7A3EAC7074372C952C78B633EAF057B1814BF8DC3021BC2866226E0DCE", // PlanetLayer.Tiles
        "9BBBC3BD70AD6553CE1FCCEF5E527AC965C1A84064419DC5B92E2A12429EF1B9"  // Tile.PrimaryBiome
    };
    internal static readonly string[] EffectiveExpected = {
        "4C1A3EAAAEEE2D456F32624C7825127A9715F59BAE5EDD842C48A218407C5F1A",
        "8447B0CB9C4AFA0ED8CA7644BB9D63E2A2FB4CA4C4FA414F535C4CA3B813B52D",
        "255907211728E3C37B58485BBC25482F1E78F2F2ED55BDD2CC930913009F01A5",
        "074C69DB5382FB3A8AFCB2CC6BF10A655E50D897583F338071D09E3E7D58CCAE",
        "3C6E54206239675B1156BE6DD82B0C547E71C117CFC8CB74EED0AA24178A5BB4", // Find.CurrentMap
        "274E38485ADB603396550BA848ED7F989D328BE9F6F1593F3FCF326587B36044", // Current.Game
        "B2E6A87B7F6A6F4E346495F6636F36E9C4FDD2CE3B470D72137B45635F987F9B", // Game.CurrentMap
        "BBDE832A0B6415E0F4E696B39207AF35B674D572DB34722582B2B2374A47C856", // Map.Biome
        "4335692258CE83A84498E50CF4BFEE7CE3A3B002DB5DE9006A5AEA6907F5BDF0", // Map.TileInfo
        "F80C70E31B09C663EB8403B152CC36484F3C1967712157B657BB8AE19AFADB4C", // Map.IsPocketMap
        "B366D2C7348ACC57E06ACE86ACB33791B99B0E5F43E91DBFC444244D6E59C52E", // Map.Tile
        "2EF8F4B7D4D90746B243E0AC90754F28FF28404D291325AB34B211F706CCDF7B", // MapInfo.Tile
        "603BB9A9AF14CF1BDFC8F58C2B517B530DAFD8F7E498748143A48A7C2EB63CE2", // WorldObject.Tile
        "8376B968D1E786CB49B1307E30CAE0407F7694F7269C49CDB37802E37A53B091", // Find.WorldGrid
        "3C54368D727226E4D2F976A0F12E2DFD4462F90A728CA5BB30F88CCB15488DAA", // Find.World
        "B63673282BD1AA1A8FCE4BFA8370282223BF4A0597979CE734B5EA0A52A58159", // Game.World
        "05E3618F82C0D4C2E5432E0703D216161C695889F237B17BAB95427730BF3A92", // WorldGrid[PlanetTile]
        "50E699F5F8E2E0571CE12B84EADC6CB10076C0806C505DF5FCE2B371BB230DA0", // PlanetTile.Layer
        "B3FAB614375BB8E22622FB30AD489A1F29E6D3921007CAC2AFCC2E4454FDA20F", // WorldGrid.Surface
        "68D93C7A89B9D26DFF7521CC3F9C73EBC7AA73DFED83D55546EE2260AF3E6968", // WorldGrid.PlanetLayers
        "71EA4B7A3EAC7074372C952C78B633EAF057B1814BF8DC3021BC2866226E0DCE", // PlanetLayer.Tiles
        "9BBBC3BD70AD6553CE1FCCEF5E527AC965C1A84064419DC5B92E2A12429EF1B9"  // Tile.PrimaryBiome
    };
    private static readonly object Gate = new();
    private static readonly SceneBudget budget = new();
    private static readonly List<Dictionary<string, object>> rows = new();
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static Fields? fields;
    private static bool active;
    private static string status = "Advance ambient preparation is off.";
    private static long triggerCount, preparedCount, cancelledCount, skippedCount, budgetCount;
    internal static bool Active => Volatile.Read(ref active);

    internal static MethodInfo[] Targets(Assembly? assembly = null)
    {
        assembly ??= typeof(AmbientSoundManager).Assembly;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        MethodInfo Find(string type, string name, params string[] parameters) => assembly.GetType(type, true)!.GetMethods(flags)
            .Single(method => method.Name == name && method.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(parameters));
        return new[] { Find("RimWorld.AmbientSoundManager", "Notify_SwitchedMap"),
            Find("RimWorld.AmbientSoundManager", "RecreateMapSustainers"),
            Find("Verse.LongEventHandler", "ExecuteWhenFinished", "System.Action"),
            Find("Verse.LongEventHandler", "UpdateCurrentAsynchronousEvent"),
            // Exact native accessor chain replaced by Fields.TryScope. A field
            // layout alone cannot qualify changed getter bodies or their hooks.
            Find("Verse.Find", "get_CurrentMap"),
            Find("Verse.Current", "get_Game"),
            Find("Verse.Game", "get_CurrentMap"),
            Find("Verse.Map", "get_Biome"),
            Find("Verse.Map", "get_TileInfo"),
            Find("Verse.Map", "get_IsPocketMap"),
            Find("Verse.Map", "get_Tile"),
            Find("Verse.MapInfo", "get_Tile"),
            Find("RimWorld.Planet.WorldObject", "get_Tile"),
            Find("Verse.Find", "get_WorldGrid"),
            Find("Verse.Find", "get_World"),
            Find("Verse.Game", "get_World"),
            Find("RimWorld.Planet.WorldGrid", "get_Item", "RimWorld.Planet.PlanetTile"),
            Find("RimWorld.Planet.PlanetTile", "get_Layer"),
            Find("RimWorld.Planet.WorldGrid", "get_Surface"),
            Find("RimWorld.Planet.WorldGrid", "get_PlanetLayers"),
            Find("RimWorld.Planet.PlanetLayer", "get_Tiles"),
            Find("RimWorld.Planet.Tile", "get_PrimaryBiome") };
    }

    internal static void Initialize(Harmony harmony)
    {
        Close();
        try
        {
            var targets = Targets();
            if (targets.Length != Expected.Length || targets.Length != EffectiveExpected.Length)
                throw new InvalidOperationException("native ambient contract target count changed");
            fields = new Fields();
            var checks = new List<PublishedPatchGuard>();
            for (int i = 0; i < targets.Length; i++)
            {
                bool hashed = SemanticMethodIdentity.TryHash(targets[i], out string hash, out string reason);
                if (!hashed || EffectiveExpected[i].Length != 64 || hash != EffectiveExpected[i])
                    throw new InvalidOperationException(targets[i].Name + "; actual=" + hash + "; reason=" + reason);
                if (!PublishedPatchGuard.TryCreate(targets[i], harmony.Id, out var guard, allPatchKinds: true)
                    || !guard!.AllowsOriginalContract()) throw new InvalidOperationException("patched ambient boundary: " + targets[i].Name);
                checks.Add(guard);
            }
            guards = checks.ToArray();
            harmony.Patch(targets[0], prefix: new HarmonyMethod(typeof(PreparedAudioAdvance), nameof(BeforeMapSwitch)));
            lock (Gate)
            {
                status = "Current-biome ambient clips prepare inside the original map-loading worker.";
                Volatile.Write(ref active, true);
            }
        }
        catch (Exception error)
        {
            lock (Gate) status = "Advance ambient preparation refused: " + error.GetBaseException().Message;
        }
    }

    internal static void Close()
    {
        lock (Gate)
        {
            Volatile.Write(ref active, false);
            budget.Cancel();
            status = "Advance ambient preparation is off.";
        }
    }

    private static bool Allowed() => Active && guards.Length == EffectiveExpected.Length
        && guards.All(guard => guard.AllowsOriginalContract()) && PreparedAudioReadiness.Allowed("sustainer");

    private static void BeforeMapSwitch()
    {
        if (!Active) return;
        Interlocked.Increment(ref triggerCount);
        try
        {
            // Harmony queries and native field reads never run under our gate.
            if (!Allowed() || fields == null || !fields.TryScope(out object scene, out Map map, out BiomeDef biome))
            { Interlocked.Increment(ref skippedCount); return; }
            long generation;
            lock (Gate)
            {
                if (!active) return;
                generation = budget.Begin(scene, map);
            }
            List<SoundDef> sounds = biome.soundsAmbient;
            if (sounds == null) { Interlocked.Increment(ref skippedCount); return; }
            // Count visited entries, including unsupported/null ones. A changed
            // supplier cannot turn this into an unbounded scan of its bank.
            for (int d = 0; d < sounds.Count; d++)
            {
                if (!Current(scene, map, biome, generation)) { Interlocked.Increment(ref cancelledCount); return; }
                lock (Gate) if (!budget.TakeDef(generation)) { Interlocked.Increment(ref budgetCount); return; }
                SoundDef sound = sounds[d];
                if (sound == null || !sound.sustain || sound.subSounds == null) { Interlocked.Increment(ref skippedCount); continue; }
                for (int s = 0; s < sound.subSounds.Count; s++)
                {
                    lock (Gate) if (!budget.TakeSubSound(generation)) { Interlocked.Increment(ref budgetCount); return; }
                    SubSoundDef sub = sound.subSounds[s];
                    if (sub == null || sub.GetType() != typeof(SubSoundDef)
                        || fields.Resolved.GetValue(sub) is not List<ResolvedGrain> grains)
                    { Interlocked.Increment(ref skippedCount); continue; }
                    for (int g = 0; g < grains.Count; g++)
                    {
                        if (!Current(scene, map, biome, generation)) { Interlocked.Increment(ref cancelledCount); return; }
                        lock (Gate) if (!budget.TakeGrain(generation)) { Interlocked.Increment(ref budgetCount); return; }
                        ResolvedGrain grain = grains[g];
                        if (grain == null || grain.GetType() != typeof(ResolvedGrain_Clip)) { Interlocked.Increment(ref skippedCount); continue; }
                        AudioClip clip = ((ResolvedGrain_Clip)grain).clip;
                        if (ReferenceEquals(clip, null)) { Interlocked.Increment(ref skippedCount); continue; }
                        bool duplicate;
                        lock (Gate)
                            if (!budget.TakeClip(generation, clip, out duplicate))
                            { Interlocked.Increment(ref budgetCount); return; }
                        if (duplicate) { Interlocked.Increment(ref skippedCount); continue; }
                        if (!Current(scene, map, biome, generation)) { Interlocked.Increment(ref cancelledCount); return; }
                        // Synchronous private work only. A late epoch closure is
                        // rechecked by the runtime before constructing its reader.
                        bool prepared = PreparedAudioRuntime.PrepareAhead(clip, generation, out string key);
                        if (prepared) Interlocked.Increment(ref preparedCount);
                        lock (Gate)
                            if (rows.Count < MaximumClips)
                                rows.Add(new Dictionary<string, object> { ["generation"] = generation, ["cacheKey"] = key,
                                    ["prepared"] = prepared, ["thread"] = Thread.CurrentThread.ManagedThreadId });
                    }
                }
            }
        }
        // Optional discovery must not replace Notify_SwitchedMap's native
        // scheduling/errors. Real decoder errors are latched by PrepareAhead.
        catch { Interlocked.Increment(ref skippedCount); }
    }

    private static bool Current(object scene, Map map, BiomeDef biome, long generation)
    {
        if (!Allowed() || fields == null || !fields.TryScope(out object now, out Map current, out BiomeDef currentBiome)
            || !ReferenceEquals(scene, now) || !ReferenceEquals(map, current) || !ReferenceEquals(biome, currentBiome)) return false;
        lock (Gate) return active && budget.Matches(scene, map, generation);
    }

    internal static Dictionary<string, object> Snapshot()
    {
        lock (Gate) return new Dictionary<string, object> { ["active"] = Active, ["enabled"] = Active, ["status"] = status,
            ["generation"] = budget.Generation, ["triggerCount"] = Interlocked.Read(ref triggerCount),
            ["preparedCount"] = Interlocked.Read(ref preparedCount), ["cancelledCount"] = Interlocked.Read(ref cancelledCount),
            ["skippedCount"] = Interlocked.Read(ref skippedCount), ["budgetCount"] = Interlocked.Read(ref budgetCount),
            ["defsVisited"] = budget.Defs, ["subSoundsVisited"] = budget.SubSounds, ["grainsVisited"] = budget.Grains,
            ["candidateClips"] = budget.Clips, ["maximumDefs"] = MaximumDefs, ["maximumClips"] = MaximumClips,
            ["maximumSubSounds"] = MaximumSubSounds, ["maximumGrains"] = MaximumGrains,
            ["rows"] = rows.Select(row => (object)new Dictionary<string, object>(row)).ToArray() };
    }

    // A bounded event-local counter, not a work queue. A map switch invalidates
    // earlier scopes while retaining the event-wide budget and clip deduplication.
    internal sealed class SceneBudget
    {
        private WeakReference? scene, map;
        private readonly List<WeakReference> seen = new();
        internal long Generation { get; private set; }
        internal int Defs { get; private set; }
        internal int SubSounds { get; private set; }
        internal int Grains { get; private set; }
        internal int Clips => seen.Count;
        internal long Begin(object nextScene, object nextMap)
        {
            if (!ReferenceEquals(scene?.Target, nextScene)) { Defs = SubSounds = Grains = 0; seen.Clear(); }
            if (!ReferenceEquals(scene?.Target, nextScene) || !ReferenceEquals(map?.Target, nextMap)) Generation++;
            scene = new WeakReference(nextScene); map = new WeakReference(nextMap); return Generation;
        }
        internal bool Matches(object expectedScene, object expectedMap, long generation)
            => Generation == generation && ReferenceEquals(scene?.Target, expectedScene) && ReferenceEquals(map?.Target, expectedMap);
        internal bool TakeDef(long generation) { if (Generation != generation || scene?.Target == null || Defs >= MaximumDefs) return false; Defs++; return true; }
        internal bool TakeSubSound(long generation) { if (Generation != generation || scene?.Target == null || SubSounds >= MaximumSubSounds) return false; SubSounds++; return true; }
        internal bool TakeGrain(long generation) { if (Generation != generation || scene?.Target == null || Grains >= MaximumGrains) return false; Grains++; return true; }
        internal bool TakeClip(long generation, object clip, out bool duplicate)
        {
            duplicate = seen.Any(item => ReferenceEquals(item.Target, clip));
            if (Generation != generation || scene?.Target == null) return false;
            if (duplicate) return true;
            if (seen.Count >= MaximumClips) return false;
            seen.Add(new WeakReference(clip)); return true;
        }
        internal void Cancel() { Generation++; scene = map = null; seen.Clear(); Defs = SubSounds = Grains = 0; }
    }

    internal sealed class Fields
    {
        private readonly FieldInfo currentEvent, eventThread, executing, asynchronous, game, maps, world, mapDisposed,
            worldTile, layerId, surface, layers, tiles, biome;
        internal readonly FieldInfo Resolved;
        internal Fields()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo Field(Type owner, string name, Type type, bool isStatic = false)
            {
                var value = owner.GetField(name, flags);
                if (value == null || value.FieldType != type || value.IsStatic != isStatic)
                    throw new InvalidOperationException("native ambient field changed: " + owner.FullName + "." + name);
                return value;
            }
            Type queued = typeof(LongEventHandler).GetNestedType("QueuedLongEvent", BindingFlags.NonPublic)!;
            currentEvent = Field(typeof(LongEventHandler), "currentEvent", queued, true);
            eventThread = Field(typeof(LongEventHandler), "eventThread", typeof(Thread), true);
            executing = Field(typeof(LongEventHandler), "executingToExecuteWhenFinished", typeof(bool), true);
            asynchronous = Field(queued, "doAsynchronously", typeof(bool));
            game = Field(typeof(Verse.Current), "gameInt", typeof(Game), true);
            maps = Field(typeof(Game), "maps", typeof(List<Map>));
            world = Field(typeof(Game), "worldInt", typeof(World));
            mapDisposed = Field(typeof(Map), "<Disposed>k__BackingField", typeof(bool));
            worldTile = Field(typeof(WorldObject), "tile", typeof(PlanetTile));
            layerId = Field(typeof(PlanetTile), "layerId", typeof(int));
            surface = Field(typeof(WorldGrid), "surface", typeof(SurfaceLayer));
            layers = Field(typeof(WorldGrid), "planetLayers", typeof(Dictionary<int, PlanetLayer>));
            tiles = Field(typeof(PlanetLayer), "tiles", typeof(List<Tile>));
            biome = Field(typeof(Tile), "biome", typeof(BiomeDef));
            Resolved = Field(typeof(SubSoundDef), "resolvedGrains", typeof(List<ResolvedGrain>));
        }
        internal bool TryScope(out object scene, out Map map, out BiomeDef selectedBiome)
        {
            scene = null!; map = null!; selectedBiome = null!;
            object? current = currentEvent.GetValue(null);
            if (current == null || !(bool)asynchronous.GetValue(current)! || (bool)executing.GetValue(null)!
                || !ReferenceEquals(eventThread.GetValue(null), Thread.CurrentThread)) return false;
            if (game.GetValue(null) is not Game owner || maps.GetValue(owner) is not List<Map> list) return false;
            int index = owner.currentMapIndex;
            if (index < 0 || index >= list.Count) return false;
            Map selected = list[index];
            if (selected == null || (bool)mapDisposed.GetValue(selected)! || selected.info == null) return false;
            Tile? tile;
            if (selected.info.isPocketMap) tile = selected.pocketTileInfo;
            else
            {
                // Pinned Find.World returns Game.World on this admitted branch;
                // never substitute its separate Current.CreatingWorld fallback.
                if (selected.info.parent == null || world.GetValue(owner) is not World currentWorld || currentWorld.grid == null) return false;
                var address = (PlanetTile)worldTile.GetValue(selected.info.parent)!;
                int layer = (int)layerId.GetValue(address)!;
                PlanetLayer? planetLayer;
                if (layer < 0) planetLayer = surface.GetValue(currentWorld.grid) as PlanetLayer;
                else
                {
                    if (layers.GetValue(currentWorld.grid) is not Dictionary<int, PlanetLayer> available) return false;
                    // Dictionary lookup could invoke an installed custom comparer.
                    // Inspect only a bounded number of primitive stored keys.
                    planetLayer = null;
                    int inspected = 0;
                    foreach (var entry in available)
                    {
                        if (inspected++ >= 32) break;
                        if (entry.Key == layer) { planetLayer = entry.Value; break; }
                    }
                    if (planetLayer == null) return false;
                }
                if (planetLayer == null || tiles.GetValue(planetLayer) is not List<Tile> contents
                    || address.tileId < 0 || address.tileId >= contents.Count) return false;
                tile = contents[address.tileId];
            }
            if (tile == null || biome.GetValue(tile) is not BiomeDef result) return false;
            scene = current; map = selected; selectedBiome = result; return true;
        }
    }
}
