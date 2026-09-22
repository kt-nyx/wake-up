// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WakeUp;

// Native map queue callers and their actual workers, separate from save/world
// page admission. No delegate from an unknown mod is classified by its text.
internal static class BackgroundLoadingMapContract
{
    internal static readonly MethodInfo Renderer = AccessTools.Method(typeof(WorldRenderer), "RegenerateLayersIfDirtyInLongEvent");
    internal static readonly MethodInfo Portal = AccessTools.Method(typeof(MapPortal), "GeneratePocketMap");
    internal static readonly Type[] TransportTypes = { typeof(TransportersArrivalAction_AttackSettlement),
        typeof(TransportersArrivalAction_VisitSite), typeof(TransportersArrivalAction_VisitSpace) };
    internal static readonly MethodInfo[] QueueCallers = new[] {
        AccessTools.Method(typeof(SettleInEmptyTileUtility), "Settle"),
        Closures(typeof(SettleInEmptyTileUtility), "<SetupCamp>").Single(m =>
            PatchProcessor.GetOriginalInstructions(m).Any(c => c.Calls(BackgroundLoadingRuntime.WorldQueue))),
        AccessTools.Method(typeof(CaravanArrivalAction_VisitSite), "Arrived"),
        AccessTools.Method(typeof(CaravanArrivalAction_VisitEscapeShip), "Arrived"),
        AccessTools.Method(typeof(SettlementUtility), "Attack"),
        AccessTools.Method(typeof(TravellingTransporters), "Arrived")
    };
    internal static readonly MethodInfo[] Targets = FindTargets();
    // Pinned original GOG bodies, resolved against Unity's framework owners.
    // All 42 canonical bodies matched the offline originals after only the
    // exact framework type bindings recorded by BackgroundMapContractTests.
    // Runtime hashing stays strict and does not normalize arbitrary assemblies.
    internal static readonly string[] Expected = new[] {
        "5853542C18E81170A2691567C0F0238A4B61CA2B3BCF0BA09D00521EEFACF933", // RimWorld.MapPortal:Verse.Map GeneratePocketMapInt()
        "2BB11DDC707941EC450AB0CA1AB24035BED8C53668F3C49BACDAF570419F94C9", // RimWorld.MapPortal:Void GeneratePocketMap()
        "7AE9051E06A89F44A2CC0A2BCADC0DA489BDDF46D00D08C1B72D90DF15070320", // RimWorld.Planet.CaravanArrivalAction_VisitEscapeShip+<>c__DisplayClass8_0:Void <Arrived>b__0()
        "A163DEE8412FB48AF281F56DC667391DEBAD106316F101759224CAC97F3C856F", // RimWorld.Planet.CaravanArrivalAction_VisitEscapeShip:Void Arrived(RimWorld.Planet.Caravan)
        "1C87188504C907F4B8DBC3720B6E395D396C1244CA278A1BB2FE858BCA253729", // RimWorld.Planet.CaravanArrivalAction_VisitEscapeShip:Void DoArrivalAction(RimWorld.Planet.Caravan)
        "F608386820B76B1A980F4CE8278AECFBA6FD893F185F37C205CDC4434E6132F8", // RimWorld.Planet.CaravanArrivalAction_VisitSite+<>c__DisplayClass8_0:Void <Arrived>b__0()
        "485C71B418B7ECE4570B82A557A241F5503A780521D3A5404695499B3CBA4B5A", // RimWorld.Planet.CaravanArrivalAction_VisitSite:Void Arrived(RimWorld.Planet.Caravan)
        "744B841E5340F37783F600D5D8C15D613D7E549127F7B467E48C51D83B996EC2", // RimWorld.Planet.CaravanArrivalAction_VisitSite:Void DoEnter(RimWorld.Planet.Caravan, RimWorld.Planet.Site)
        "00BA0700A05DB2860B729762F03CE079D85F60D6A3B972428EE1845530C0933A", // RimWorld.Planet.SettleInEmptyTileUtility+<>c__DisplayClass1_0:Void <Settle>b__0()
        "BB7CBF27801D5B88D3794588A20FBD87FE43A339E50C8E6F608E855258D5ED10", // RimWorld.Planet.SettleInEmptyTileUtility+<>c__DisplayClass1_0:Void <Settle>b__1()
        "1974F20A5E5731BC23A8DC05BE21A32E08F58C17893405807CFB734D36D154A6", // RimWorld.Planet.SettleInEmptyTileUtility+<>c__DisplayClass1_1:Boolean <Settle>b__2(Verse.IntVec3)
        "46EAC305719D19AE2E5E6E4A351F2032D55777AB8E59CF2F67298EE6ABA2B9C2", // RimWorld.Planet.SettleInEmptyTileUtility+<>c__DisplayClass4_0:Void <SetupCamp>b__0()
        "1313DB8AF4F6D53E31F223866EC64229EF15A98745058402D59CC26CDF5067C5", // RimWorld.Planet.SettleInEmptyTileUtility+<>c__DisplayClass4_0:Void <SetupCamp>b__1()
        "7C712C247B605C0C32F040B3B16F79D717FA617CAACD4759F7C7A95A28AA3E35", // RimWorld.Planet.SettleInEmptyTileUtility+<>c__DisplayClass4_1:Boolean <SetupCamp>b__2(Verse.IntVec3)
        "AB81F4FF16EC0407B0F747168FDE06FCE795005EAF7B16837CA49EC182592415", // RimWorld.Planet.SettleInEmptyTileUtility:Void Settle(RimWorld.Planet.Caravan)
        "6C56B3E1C193D8E5688FEFA86C3C48E4F30371386E83AE9385EB6337E87BD0BF", // RimWorld.Planet.SettlementUtility+<>c__DisplayClass1_0:Void <Attack>b__0()
        "B7951C614BEB25181F6AE6BDE1199E6AD15D02DABABE09BC2E2A47276AC0B992", // RimWorld.Planet.SettlementUtility:Void Attack(RimWorld.Planet.Caravan, RimWorld.Planet.Settlement)
        "171D95ACBE33A748706FCD41D2094EE2E31A8AF6CC3F79D41A19096292B65FA4", // RimWorld.Planet.SettlementUtility:Void AttackNow(RimWorld.Planet.Caravan, RimWorld.Planet.Settlement)
        "B82E1B284E5BFFAA516D8A77F26FCD1333103B0B0EE3C59639D289418E46B73D", // RimWorld.Planet.TransportersArrivalAction_AttackSettlement:Boolean ShouldUseLongEvent(System.Collections.Generic.List`1[RimWorld.ActiveTransporterInfo], RimWorld.Planet.PlanetTile)
        "1CE5B01297902E7F6EC78D2342F80C9FB26FEE6BFC3CC3D74AA0165EE24E7740", // RimWorld.Planet.TransportersArrivalAction_AttackSettlement:Void Arrived(System.Collections.Generic.List`1[RimWorld.ActiveTransporterInfo], RimWorld.Planet.PlanetTile)
        "CF2A06C290D324FA8AE3874E9E9B3BA805B5B0E674489B397844033A1620C05B", // RimWorld.Planet.TransportersArrivalAction_VisitSite:Boolean ShouldUseLongEvent(System.Collections.Generic.List`1[RimWorld.ActiveTransporterInfo], RimWorld.Planet.PlanetTile)
        "2555EB0FD3268119D6B265213AF176F6EB2B39ABCF2F877A8083FB1BD2C3C1FA", // RimWorld.Planet.TransportersArrivalAction_VisitSite:Void Arrived(System.Collections.Generic.List`1[RimWorld.ActiveTransporterInfo], RimWorld.Planet.PlanetTile)
        "37278B86D084B2EC2D41E32CB383B2D4F413FF255EE41BE9700796A4AF74E2EA", // RimWorld.Planet.TransportersArrivalAction_VisitSpace:Boolean ShouldUseLongEvent(System.Collections.Generic.List`1[RimWorld.ActiveTransporterInfo], RimWorld.Planet.PlanetTile)
        "0D61F5CD00FD9E4B3117D39F935D70218CD01407678359C91FEDBC5C2BEBCE0F", // RimWorld.Planet.TransportersArrivalAction_VisitSpace:Void Arrived(System.Collections.Generic.List`1[RimWorld.ActiveTransporterInfo], RimWorld.Planet.PlanetTile)
        "F8CE702C09244D720C093980D4DAA3F2856005FAD71116B1F71BDA40622C6A7E", // RimWorld.Planet.TravellingTransporters:Void Arrived()
        "4B6F34120CAF0C34A0264C42C8B375602151F07FDDE8346148F8041C2C87DA7B", // RimWorld.Planet.TravellingTransporters:Void DoArrivalAction()
        "93C13E99C7C53E8E60034417365DB7F28A1A12556674DA54795AF508C226F27D", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:Boolean MoveNext()
        "BF927C938F0C6B2E7AA16434B6EDA0CA6491206A273604DDBBC2153B76E1ABBB", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:System.Collections.Generic.IEnumerator`1[System.Object] System.Collections.Generic.IEnumerable<System.Object>.GetEnumerator()
        "7F2695F62594AA49B4E286FFBA82BE79D57177A9A36FFEC90EB2E195619FDB4D", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        "3ACC63C8B5911A562A9C73B73A6372ED3D15858509DD864FBA9C745B87F1CC15", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:System.Object System.Collections.Generic.IEnumerator<System.Object>.get_Current()
        "3ACC63C8B5911A562A9C73B73A6372ED3D15858509DD864FBA9C745B87F1CC15", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:System.Object System.Collections.IEnumerator.get_Current()
        "4B3E80668BE2FDFC84C8A790247C3C1771A322F512ECA58EAE1C937B263CD3BA", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:Void <>m__Finally1()
        "2967C9AB28D434B2AA3F0DFE2F3D67405A67B31D993E84DF364E4B5AD0839731", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:Void <>m__Finally2()
        "84CE095C467CA976BF96064BD9733A1BBF03B888603B361F885F1AF5060B3539", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:Void System.Collections.IEnumerator.Reset()
        "760F721E31B24CD558FF8A5F5B8D90F16D2764065FD65B9BCB12B02FCDAF3D04", // RimWorld.Planet.WorldRenderer+<RegenerateDirtyLayersNow_Async>d__17:Void System.IDisposable.Dispose()
        "DCF4E309832BF9571E213EC21A9F01FCB2571ED10B4D82D89C0AB7774AC3AF5A", // RimWorld.Planet.WorldRenderer:Boolean RegenerateLayersIfDirtyInLongEvent()
        "13FB9FE89E29F5488AA3959258BBF35E584ED573803700C3DBCD69B9F923BA65", // RimWorld.Planet.WorldRenderer:Boolean get_ShouldRegenerateDirtyLayersInLongEvent()
        "2BE280982FD3DDA5EEE79F1C55154D6A02E4C61320501AB51945B2512AF030D8", // RimWorld.Planet.WorldRenderer:System.Collections.IEnumerable RegenerateDirtyLayersNow_Async()
        "89DB93C0D9E3FD7D68C98BE6081435C754A98AC5AE4CAF4589DEE329EEBC2813", // Verse.GetOrGenerateMapUtility:Verse.Map GetOrGenerateMap(RimWorld.Planet.PlanetTile, RimWorld.WorldObjectDef, System.Collections.Generic.IEnumerable`1[Verse.GenStepWithParams])
        "82943011B86E2727B30A929A9A4612284F834B6ACFBB8B7B867F0BAFA4E55956", // Verse.GetOrGenerateMapUtility:Verse.Map GetOrGenerateMap(RimWorld.Planet.PlanetTile, Verse.IntVec3, RimWorld.WorldObjectDef, System.Collections.Generic.IEnumerable`1[Verse.GenStepWithParams], Boolean)
        "D1D5455738E1D433F64E8285037D41D48A010A0B266201A948512CF7DDC1248F", // Verse.MapGenerator:Verse.Map GenerateMap(Verse.IntVec3, RimWorld.Planet.MapParent, Verse.MapGeneratorDef, System.Collections.Generic.IEnumerable`1[Verse.GenStepWithParams], System.Action`1[Verse.Map], Boolean, Boolean)
        "1C3BCCE5538417FA3E494409122E1F034C17EF770B9D360219F9F1D340662D96", // Verse.PocketMapUtility:Verse.Map GeneratePocketMap(Verse.IntVec3, Verse.MapGeneratorDef, System.Collections.Generic.IEnumerable`1[Verse.GenStepWithParams], Verse.Map)
    };

    private static MethodInfo[] FindTargets()
    {
        var methods = new List<MethodInfo>(QueueCallers) { Renderer, Portal,
            AccessTools.Method(typeof(MapPortal), "GeneratePocketMapInt"),
            AccessTools.Method(typeof(PocketMapUtility), "GeneratePocketMap"),
            AccessTools.Method(typeof(MapGenerator), "GenerateMap"),
            AccessTools.Method(typeof(CaravanArrivalAction_VisitSite), "DoEnter"),
            AccessTools.Method(typeof(CaravanArrivalAction_VisitEscapeShip), "DoArrivalAction"),
            AccessTools.Method(typeof(SettlementUtility), "AttackNow"),
            AccessTools.Method(typeof(WorldRenderer), "RegenerateDirtyLayersNow_Async"),
            AccessTools.PropertyGetter(typeof(WorldRenderer), "ShouldRegenerateDirtyLayersInLongEvent") };
        methods.AddRange(typeof(GetOrGenerateMapUtility).GetMethods(AccessTools.allDeclared).Where(m => m.Name == "GetOrGenerateMap"));
        methods.Add(AccessTools.Method(typeof(TravellingTransporters), "DoArrivalAction"));
        foreach (var type in TransportTypes)
        {
            methods.Add(AccessTools.Method(type, "Arrived"));
            methods.Add(AccessTools.Method(type, "ShouldUseLongEvent"));
            methods.AddRange(Closures(type, "<Arrived>"));
        }
        methods.AddRange(Closures(typeof(SettleInEmptyTileUtility), "<Settle>"));
        methods.AddRange(Closures(typeof(SettleInEmptyTileUtility), "<SetupCamp>"));
        methods.AddRange(Closures(typeof(CaravanArrivalAction_VisitSite), "<Arrived>"));
        methods.AddRange(Closures(typeof(CaravanArrivalAction_VisitEscapeShip), "<Arrived>"));
        methods.AddRange(Closures(typeof(SettlementUtility), "<Attack>"));
        var iterator = typeof(WorldRenderer).GetNestedTypes(AccessTools.all)
            .Single(t => t.Name.StartsWith("<RegenerateDirtyLayersNow_Async>", StringComparison.Ordinal));
        methods.AddRange(iterator.GetMethods(AccessTools.allDeclared));
        return methods.Distinct().OrderBy(BackgroundLoadingContract.Key, StringComparer.Ordinal).ToArray();
    }
    private static IEnumerable<MethodInfo> Closures(Type type, string prefix)
    {
        foreach (var method in type.GetMethods(AccessTools.allDeclared))
            if (method.Name.StartsWith(prefix, StringComparison.Ordinal)) yield return method;
        foreach (var nested in type.GetNestedTypes(AccessTools.all))
            foreach (var method in Closures(nested, prefix)) yield return method;
    }
    internal static void Validate()
    {
        if (Targets.Length != Expected.Length) throw new InvalidOperationException("Native map-loading functions changed.");
        for (int i = 0; i < Targets.Length; i++)
            if (!SemanticMethodIdentity.TryHash(Targets[i], out string hash, out _) || hash != Expected[i])
                throw new InvalidOperationException("Native map-loading function changed: " + BackgroundLoadingContract.Key(Targets[i]));
    }
}
