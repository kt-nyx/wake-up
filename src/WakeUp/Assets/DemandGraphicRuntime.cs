// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Experimental supported-demand surface. Public fields retain their native
// initial values until completion; unknown direct field access is not covered.
// Keep the actual native callbacks, including requester state and icon handling.
public static class DemandGraphicRuntime
{
    private const int MaximumPending = 65536, MaximumReported = 128;
    private static readonly object Gate = new object();
    private static readonly Dictionary<ThingDef, Pending> PendingByDef = new Dictionary<ThingDef, Pending>(ReferenceComparer<ThingDef>.Instance);
    private static readonly Dictionary<GraphicData, List<ThingDef>> Owners = new Dictionary<GraphicData, List<ThingDef>>(ReferenceComparer<GraphicData>.Instance);
    private static readonly Dictionary<string, int> Excluded = new Dictionary<string, int>(StringComparer.Ordinal);
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static DemandGraphicCompatibility compatibility = new DemandGraphicCompatibility();
    private static bool active;
    private static string status = "Demand graphics are off.";
    private static long capturedGraphics, capturedIcons, completedGraphics, completedIcons, workerPreparations, failures, offThreadRefusals;
    private static string lastEagerReason = "";

    // Pinned offline against the original game and the owned prepatch output.
    internal static readonly string[] Expected =
    {
        "475B16A8C52A75538A7A84979B56E94D360365BADD4ED22B6E12B9885576994F",
        "FC337786ED2AD6A599ED8E0DAA6AE5AF3F69C7CEBE5E60996765D8ACFB064C8B",
        "C01187BECB786A40E053EA2BBAD8D0CC2B9AE990951260090BF00E4A56D622CA",
        "8E70D438AE10F12FE12DAB40BA94F3EBF7B85848F17C202C87822BCAF34630D0",
        "B769536B48CA6B07DB47BCEC3D4CD5EB35E6B84214EC6D774BD4246DC9E0E62B",
        "75A5A6308891C388E81F213D26CC51CB450551A3344BAF4FCD7C298A8EFE673A",
        "0C7FE40E95F938ABB76C357F2AD25421F9B1822ED7C7FD214B82C2DBA07F0C94",
        "A1936944606AD8D490A2B6BBE9F174188D78A652DDF340A4D29762D4DA00FACD",
        "52797C49F6409D7B74D9CDB3CBA9C6D3E243A13507A72595173A54A265C628CC",
        "349B0517B0D0A2DC6FD115BED3A3E4FC982816BF3D1FA8A2F307D6F0E1B5E9B4",
        "8DEC5C561C78AD5B21667BFC229092109AF9B8DAF7769F4BD120637711FCD8C4",
        "03C4A136DD89A9C32C623428BC79D62E6C644B29FC2E03AD7BAED2955ABB07D0",
        "D3F6383FE83C26B5038DF89F25F15C0A2F133AF0532F5260C43811A2488469E8",
        "05967E7777368AF25DFFF8C4AB37E7FA6B12EFF8F409CB8FFC285D66B6793BAE",
        "44B54E939B9779F86CE1B733F2857929B3CB685A7F00F916E763D001926018C9",
        "10D5912298297F4088F8980A8BA42D8979351A9DFD41B3DA8BC055448DFF2FF9",
        "AE499930A1D0D17A24D14FB47DEFE795467AE069A371DFAABCED9704CF58B42C",
        "B46E93C3B8565D40A7B6816CFD1B8D616E69A497CBE6A3B1E03D3DCFDBC1C013"
    };

    private sealed class Pending
    {
        internal Action? Graphic, Icon;
        internal bool Running;
        internal readonly GraphicData Data;
        internal readonly long Ordinal;
        internal Pending(GraphicData data, long ordinal) { Data = data; Ordinal = ordinal; }
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
        public bool Equals(T? left, T? right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }

    internal static MethodInfo[] Targets(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Type Type(string name) => assembly.GetType("Verse." + name, true)!;
        MethodInfo Find(string type, string name, params string[] args) => Type(type).GetMethods(flags)
            .Single(method => method.Name == name && method.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(args));
        MethodInfo thingPost = Find("ThingDef", "PostLoad"), buildablePost = Find("BuildableDef", "PostLoad");
        MethodInfo Callback(MethodInfo parent) => PatchProcessor.GetOriginalInstructions(parent)
            .Where(i => i.opcode == OpCodes.Ldftn).Select(i => i.operand).OfType<MethodInfo>().Single();
        return new[]
        {
            thingPost, buildablePost, Callback(thingPost), Callback(buildablePost),
            Find("ThingDef", "ResolveIcon"), Find("BuildableDef", "ResolveIcon"),
            Find("GraphicData", "get_Graphic"), Find("GraphicData", "Init"),
            Find("Thing", "get_DefaultGraphic"), Find("Thing", "get_Graphic"),
            Find("BuildableDef", "get_DrawMatSingle"), Find("BuildableDef", "GetUIIconForStuff", "Verse.ThingDef"),
            Find("Widgets", "CanDrawIconFor", "Verse.Def"),
            Type("Widgets").GetMethods(flags).Single(m => m.Name == "ThingIcon" && m.GetParameters()[1].ParameterType == Type("ThingDef")),
            Type("Widgets").GetMethods(flags).Single(m => m.Name == "ThingIcon" && m.GetParameters()[1].ParameterType == Type("Thing")),
            Type("Widgets").GetMethods(flags).Single(m => m.Name == "GetIconFor" && m.GetParameters()[0].ParameterType == Type("ThingDef") && m.GetParameters().Any(p => p.IsOut)),
            Find("Thing", "DynamicDrawPhase", "Verse.DrawPhase"),
            Find("DynamicDrawManager", "DrawDynamicThings")
        };
    }

    internal static void Initialize(Harmony harmony)
    {
        if (!DemandTextureRuntime.Enabled) return;
        var graphicHarmony = new Harmony(harmony.Id + ".graphics");
        try
        {
            MethodInfo[] methods = Targets(typeof(ThingDef).Assembly);
            compatibility = new DemandGraphicCompatibility();
            var checks = new List<PublishedPatchGuard>();
            for (int i = 0; i < methods.Length; i++)
            {
                bool hashed = SemanticMethodIdentity.TryHash(methods[i], out string hash, out string reason);
                if (Expected[i]?.Length != 64 || !hashed || hash != Expected[i])
                    throw new InvalidOperationException("native body not pinned: " + i + " " + methods[i] + "; actual=" + hash + "; " + reason);
                MethodInfo target = methods[i];
                if (!PublishedPatchGuard.TryCreate(target, graphicHarmony.Id, out var guard, allPatchKinds: true,
                    allowedForeignPatch: patch => compatibility.Allows(target, patch)) || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("foreign graphic consumer patch: " + DemandGraphicCompatibility.DescribePatches(target)
                        + "; " + compatibility.Status);
                checks.Add(guard);
            }
            guards = checks.ToArray();
            graphicHarmony.Patch(methods[0], transpiler: new HarmonyMethod(typeof(DemandGraphicRuntime), nameof(RewriteQueue)));
            graphicHarmony.Patch(methods[1], transpiler: new HarmonyMethod(typeof(DemandGraphicRuntime), nameof(RewriteQueue)));
            void Prefix(int index, string method) => graphicHarmony.Patch(methods[index], prefix: new HarmonyMethod(typeof(DemandGraphicRuntime), method));
            Prefix(6, nameof(BeforeGraphicData));
            Prefix(8, nameof(BeforeThing)); Prefix(9, nameof(BeforeThing));
            Prefix(10, nameof(BeforeBuildable)); Prefix(11, nameof(BeforeBuildable));
            Prefix(12, nameof(BeforeCanDrawIcon)); Prefix(13, nameof(BeforeThingDefIcon));
            Prefix(14, nameof(BeforeThingIcon)); Prefix(15, nameof(BeforeThingDefIcon));
            Prefix(16, nameof(BeforeDrawPhase));
            UnityData.DisposeStatic += DisposeMetadata;
            active = true;
            status = "Experimental native realtime graphic/icon callbacks wait for supported main-thread demand.";
        }
        catch (Exception error)
        {
            active = false;
            status = "Demand graphics refused: " + error.GetBaseException().Message;
            guards = Array.Empty<PublishedPatchGuard>();
            // This owner contains only this increment's hooks. A failed partial
            // installation cannot remove the parent's texture or audio hooks.
            graphicHarmony.UnpatchAll(graphicHarmony.Id);
        }
    }

    internal static IEnumerable<CodeInstruction> RewriteQueue(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        int[] positions = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo method
            && method.DeclaringType == typeof(LongEventHandler) && method.Name == nameof(LongEventHandler.ExecuteWhenFinished)).ToArray();
        if (positions.Length != 1 || code[positions[0]].blocks.Count != 0)
            throw new InvalidOperationException("graphic PostLoad queue changed");
        int at = positions[0];
        var loadDef = new CodeInstruction(OpCodes.Ldarg_0);
        loadDef.labels.AddRange(code[at].labels); code[at].labels.Clear();
        code.Insert(at, loadDef);
        code[at + 1].opcode = OpCodes.Call;
        code[at + 1].operand = AccessTools.Method(typeof(DemandGraphicRuntime),
            __originalMethod.DeclaringType == typeof(ThingDef) ? nameof(QueueGraphic) : nameof(QueueIcon));
        return code;
    }

    private static void QueueGraphic(Action action, ThingDef def) => Queue(action, def, false);
    private static void QueueIcon(Action action, BuildableDef def) => Queue(action, def as ThingDef, true);
    private static void Queue(Action action, ThingDef? def, bool icon)
    {
        if (!active || !DemandTextureRuntime.Enabled || def == null)
        { LongEventHandler.ExecuteWhenFinished(action); return; }
        // Eligibility is decided at the original callback's main-thread slot,
        // after all ordinary PostLoad field changes have taken place.
        LongEventHandler.ExecuteWhenFinished(() => Capture(action, def, icon));
    }

    private static string? Exclusion(ThingDef def)
    {
        if (def.GetType() != typeof(ThingDef)) return "custom ThingDef";
        if (def.drawerType != DrawerType.RealtimeOnly) return "native atlas or non-realtime population";
        if (def.category == ThingCategory.Pawn) return "pawn race icon population";
        if (def.colorGenerator != null) return "color generator callback";
        if (compatibility.RequiresEager(def)) return "VEF graphic customization population";
        if (def.graphicData == null || def.graphicData.GetType() != typeof(GraphicData)) return "absent or custom GraphicData";
        Type type = def.graphicData.graphicClass;
        if (type != typeof(Graphic_Single) && type != typeof(Graphic_Multi) && type != typeof(Graphic_Random)) return "other graphic class";
        return null;
    }

    private static void Capture(Action action, ThingDef def, bool icon)
    {
        string? exclusion = !active || !DemandTextureRuntime.Enabled ? "disabled"
            : !UnityData.IsInMainThread ? "non-main callback" : null;
        PublishedPatchGuard? refused = null;
        if (exclusion == null)
            refused = guards.FirstOrDefault(g => !g.AllowsOriginalContract());
        if (exclusion == null && (refused != null || !compatibility.DependenciesAllowed()))
        {
            active = false;
            status = "Demand graphics closed after a foreign patch: "
                + (refused == null ? compatibility.Status : DemandGraphicCompatibility.DescribePatches(refused.Target) + "; " + compatibility.Status)
                + "; pending callbacks completed eagerly.";
            CompleteAll("foreign graphic consumer patch");
            exclusion = "foreign patch";
        }
        // A late supplier prefix is resolved by the guard above before deciding
        // which concrete customization definitions must keep eager callbacks.
        if (exclusion == null) exclusion = Exclusion(def);
        if (exclusion == null)
        {
            lock (Gate)
            {
                if (!PendingByDef.TryGetValue(def, out Pending pending))
                {
                    if (PendingByDef.Count >= MaximumPending) exclusion = "pending bound";
                    else
                    {
                        pending = new Pending(def.graphicData, capturedGraphics + capturedIcons);
                        PendingByDef.Add(def, pending);
                        if (!Owners.TryGetValue(pending.Data, out var owners)) Owners.Add(pending.Data, owners = new List<ThingDef>());
                        owners.Add(def);
                    }
                }
                if (exclusion == null)
                {
                    if (icon ? pending!.Icon != null : pending!.Graphic != null) exclusion = "repeated PostLoad callback";
                    else
                    {
                        if (icon) { pending.Icon = action; capturedIcons++; }
                        else { pending.Graphic = action; capturedGraphics++; }
                        return;
                    }
                }
            }
        }
        lock (Gate) { Excluded.TryGetValue(exclusion!, out int count); Excluded[exclusion!] = count + 1; }
        // A repeated callback must not run ahead of a previously held callback.
        Complete(def);
        action();
    }

    public static void Complete(ThingDef def)
    {
        if (def == null) return;
        bool mainThread = UnityData.IsInMainThread;
        if (mainThread && active)
        {
            var changed = guards.FirstOrDefault(g => !g.AllowsOriginalContract());
            if (changed != null || !compatibility.DependenciesAllowed())
            {
                active = false;
                status = "Demand graphics completed eagerly at a changed supported request: "
                    + (changed == null ? compatibility.Status : DemandGraphicCompatibility.DescribePatches(changed.Target));
                CompleteAll("changed request dependency");
            }
        }
        Pending pending;
        lock (Gate)
        {
            if (!PendingByDef.TryGetValue(def, out pending) || pending.Running) return;
            if (!mainThread)
            {
                offThreadRefusals++;
                throw new InvalidOperationException("Pending experimental graphic requires main-thread preparation before worker dispatch: " + def.defName);
            }
            pending.Running = true;
        }
        try
        {
            for (int stage = 0; stage < 2; stage++)
            {
                Action? action;
                lock (Gate)
                {
                    action = stage == 0 ? pending.Graphic : pending.Icon;
                    if (stage == 0) pending.Graphic = null; else pending.Icon = null;
                }
                if (action == null) continue;
                try { action(); }
                catch
                {
                    // Keep the original callback available after a failed
                    // demand. Never publish a completed receipt for failure.
                    lock (Gate)
                    {
                        if (stage == 0) pending.Graphic = action; else pending.Icon = action;
                    }
                    throw;
                }
                lock (Gate) { if (stage == 0) completedGraphics++; else completedIcons++; }
            }
        }
        catch { lock (Gate) failures++; throw; }
        finally
        {
            lock (Gate)
            {
                pending.Running = false;
                if (pending.Graphic == null && pending.Icon == null)
                {
                    PendingByDef.Remove(def);
                    if (Owners.TryGetValue(pending.Data, out var owners))
                    {
                        owners.RemoveAll(owner => ReferenceEquals(owner, def));
                        if (owners.Count == 0) Owners.Remove(pending.Data);
                    }
                }
            }
        }
    }

    // Known callers may finish an opaque population before dispatch. Never wait
    // for the main thread from a worker whose owner may be synchronously joining.
    public static void PrepareForWorker(ThingDef def)
    {
        if (def == null) return;
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("PrepareForWorker must run on the main thread before dispatch.");
        lock (Gate) { if (PendingByDef.ContainsKey(def)) workerPreparations++; }
        Complete(def);
    }

    public static void CompleteAll(string reason)
    {
        if (!UnityData.IsInMainThread) { lock (Gate) offThreadRefusals++; return; }
        ThingDef[] defs;
        lock (Gate) { lastEagerReason = reason; defs = PendingByDef.OrderBy(pair => pair.Value.Ordinal).Select(pair => pair.Key).ToArray(); }
        foreach (ThingDef def in defs) Complete(def);
    }

    private static void BeforeGraphicData(GraphicData __instance)
    {
        ThingDef[] defs;
        lock (Gate) { if (!Owners.TryGetValue(__instance, out var owners)) return; defs = owners.ToArray(); }
        foreach (ThingDef def in defs) Complete(def);
    }
    private static void BeforeThing(Thing __instance) => Complete(__instance.def);
    private static void BeforeBuildable(BuildableDef __instance) { if (__instance is ThingDef def) Complete(def); }
    private static void BeforeCanDrawIcon(Def def) { if (def is ThingDef thingDef) Complete(thingDef); }
    private static void BeforeThingDefIcon(ThingDef thingDef) => Complete(thingDef);
    private static void BeforeThingIcon(Thing thing) => Complete(thing.def);
    private static void BeforeDrawPhase(Thing __instance, DrawPhase phase)
    { if (phase == DrawPhase.EnsureInitialized) PrepareForWorker(__instance.def); }

    public static ThingDef[] PendingDefs()
    { lock (Gate) return PendingByDef.Keys.Take(MaximumReported).ToArray(); }

    public static void DisposeMetadata()
    {
        lock (Gate)
        {
            active = false;
            PendingByDef.Clear();
            Owners.Clear();
            guards = Array.Empty<PublishedPatchGuard>();
            compatibility = new DemandGraphicCompatibility();
            status = "Demand graphic metadata discarded with native static data.";
        }
    }

    public static Dictionary<string, object> Snapshot()
    {
        lock (Gate) return new Dictionary<string, object>
        {
            ["active"] = active, ["status"] = status, ["compatibility"] = compatibility.Status, ["pendingDefs"] = PendingByDef.Count,
            ["capturedGraphics"] = capturedGraphics, ["capturedIcons"] = capturedIcons,
            ["completedGraphics"] = completedGraphics, ["completedIcons"] = completedIcons,
            ["workerPreparations"] = workerPreparations, ["failures"] = failures,
            ["offThreadRefusals"] = offThreadRefusals, ["lastEagerReason"] = lastEagerReason,
            ["excluded"] = new Dictionary<string, int>(Excluded, StringComparer.Ordinal),
            ["rows"] = PendingByDef.Take(MaximumReported).Select(pair => (object)new Dictionary<string, object>
            {
                ["defName"] = pair.Key.defName ?? "", ["graphicPending"] = pair.Value.Graphic != null,
                ["iconPending"] = pair.Value.Icon != null
            }).ToArray()
        };
    }
}
