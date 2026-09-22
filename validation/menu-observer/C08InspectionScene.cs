// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace FixtureMenuObserver;

// One private copied-save artifact, not a product feature or a general scene builder.
internal static class C08InspectionScene
{
    internal static bool Selected => Environment.GetCommandLineArgs().Contains("--fixture-c08-quality=inspection-scene");
    private static IntVec3 center;
    private static string[] ids = Array.Empty<string>();

    internal static void Place(Map map)
    {
        if (!Selected) return;
        if (!Environment.GetCommandLineArgs().Contains("--fixture-gameplay-save")
            || !GenFilePaths.SaveDataFolderPath.EndsWith(@".rlo-test-instance\profile", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Inspection scene requires the isolated copied fixture save.");
        center = IntVec3.Invalid;
        var antenna = DefDatabase<ThingDef>.GetNamed("BurnoutMechlinkBooster");
        var brazier = DefDatabase<ThingDef>.GetNamed("Brazier");
        // Search near the existing camera/colony first; never wipe pawns or buildings.
        foreach (IntVec3 candidate in map.AllCells.OrderBy(c => c.DistanceToSquared(map.Center)))
        {
            var cells = new System.Collections.Generic.List<IntVec3>();
            for (int i = 0; i < 4; i++)
                cells.AddRange(GenAdj.OccupiedRect(new IntVec3(candidate.x - 6 + i * 4, 0, candidate.z - 3), new Rot4(i), antenna.size).Cells);
            cells.AddRange(GenAdj.OccupiedRect(new IntVec3(candidate.x + 5, 0, candidate.z + 4), Rot4.North, brazier.size).Cells);
            cells.AddRange(new CellRect(candidate.x - 3, candidate.z + 2, 4, 4).Cells);
            if (!cells.All(c => c.InBounds(map) && c.GetEdifice(map) == null
                && c.GetTerrain(map).passability != Traversability.Impassable
                && !c.GetThingList(map).Any(t => t is Pawn || t.def.category == ThingCategory.Item))) continue;
            center = candidate;
            break;
        }
        if (!center.IsValid) throw new InvalidOperationException("No clear display footprints in copied map.");
        var placed = new System.Collections.Generic.List<Thing>();
        for (int i = 0; i < 4; i++)
            placed.Add(Spawn(antenna, new IntVec3(center.x - 6 + i * 4, 0, center.z - 3), new Rot4(i), map));
        placed.Add(Spawn(brazier, new IntVec3(center.x + 5, 0, center.z + 4), Rot4.North, map));
        var straw = DefDatabase<TerrainDef>.GetNamed("StrawMatting");
        foreach (IntVec3 cell in new CellRect(center.x - 3, center.z + 2, 4, 4))
        {
            foreach (Plant plant in cell.GetThingList(map).OfType<Plant>().ToArray()) plant.Destroy();
            map.terrainGrid.SetTerrain(cell, straw);
        }
        ids = placed.Select(t => t.GetUniqueLoadID()).ToArray();
    }

    private static Thing Spawn(ThingDef def, IntVec3 cell, Rot4 rotation, Map map)
    {
        Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.Steel : null);
        thing.SetFaction(Faction.OfPlayer);
        return GenSpawn.Spawn(thing, cell, map, rotation, WipeMode.Vanish);
    }

    internal static void Verify(Map map, string directory)
    {
        if (!Selected) return;
        if (ids.Length != 5) throw new InvalidOperationException("Inspection placement did not finish.");
        Thing[] restored = ids.Select(id => map.listerThings.AllThings.Single(t => t.GetUniqueLoadID() == id)).ToArray();
        for (int i = 0; i < 4; i++)
            if (restored[i].def.defName != "BurnoutMechlinkBooster" || restored[i].Rotation.AsInt != i)
                throw new InvalidOperationException("Inspection antenna rotation changed on reload.");
        if (restored[4].def.defName != "Brazier"
            || new CellRect(center.x - 3, center.z + 2, 4, 4).Cells.Any(c => c.GetTerrain(map).defName != "StrawMatting"))
            throw new InvalidOperationException("Inspection brazier/straw display changed on reload.");
        File.WriteAllText(Path.Combine(directory, "c08-quality-probe.jsonl"),
            "{\"event\":\"complete\",\"passed\":true,\"phase\":\"inspection-scene\",\"centerX\":" + center.x
            + ",\"centerZ\":" + center.z + ",\"brazier\":1,\"antennaRotations\":[0,1,2,3],\"strawCells\":16,"
            + "\"nativeSaveReloadPreserved\":true,\"visualAcceptance\":false}\n");
    }
}
