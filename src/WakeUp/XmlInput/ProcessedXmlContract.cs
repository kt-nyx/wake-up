// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Coordinated C02 private processed-stage boundary.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace WakeUp;

internal static class ProcessedXmlContract
{
    internal const string SettingsPrefixIdentity = "6895F089D0AF1F8F96596BFEC45A8416099C5B15FE694BD38AE2EC8AC98157DC";
    private static readonly Dictionary<MethodInfo, PublishedPatchGuard> SettingsGuards = new();
    // Generated from independently inspected current Steam methods, then the
    // two mechanically rewritten callsites. No runtime learning of contracts.
    internal static readonly string[] Bodies = {
        ParsedXmlRuntime.EffectiveAll,
        "47C51B18D9BFF0B6865A15A05C94996CEC279B9E550EA2A05AE6E1D0C21A9076",
        "4F9ECDC5FE541B25238FE5E34E7229DB87C332C73F177FCFFDF654B94F827ED3",
        "9B0813EF2985EC5A12A4A0FB21A370FCFB7B1290227AE6E4C2C7A46A914DEE29",
        "1BDB8EA714FF97AD8FE40D75D5E2C4C9E75D6F00F3C70CA5698ADF0D4EABFD68",
        "D8E18ACA321F9E737B6BB6E2E1529A973A292876E57DBA2511575FC4A26852FF",
        "C64D02FF6B98986A1502CDA7C59B37D06771B73D9A606C91F32B3D90D616E8DC",
        "724BC99423D3B0CFAB5510C01676F0B2D512E872C2FDAA0A28C1C4906E693D7F",
        "364B96398777A3DC11553C7489C744F50488FD2740F6354234BFF11FBFE20C88",
        "ED66066D76024F959F8F050241C0F63237BC8B85CDC8A6905EB8C3A9618E7F0C",
        "0530440A3A28CBBEC2D1648252CD3B0795816B6758117B1D9207D92E347D9F69",
        "B760A5158C3D9D8C00A7CA17A29CF098F7A2252BE6D9DF34362CB542B7204204",
        "3D6DC0F42401B844037553DAE57CB1CD5ADB8DEDC9B2AF443F928E8B9F51EA3F",
        "DEF5C0CD7055D6D01204C19BF8BFA35933769CBEEE56DE788B11A0ED933A9F21",
        "C03AF4FB239092157EC3EE0F2F67CD65054C9F2CEE55765C6D9676374FA28CED",
        "1EB7003F4CEE17BCA57C193D554D4CB39E461BB000EE3A23A0A57B8673B64F1E",
        "02D4FDCE8898CFB1D9612919F8716FF8F2BA86968875E772F5B64277BFBBFAFA",
        "E0BEB8E3C60B4F2310A2C4890CCF8F31DC537D0BFB81D93D34047EA252AB2005",
        "5458507A9E2063635E02AF22E73ECEE8F298E8B38B53242494DE75DA0B50EDCC",
        "4300D9B811FF75EB7EE5A841D5B09B2184D20CF4A266B7593A8DD295232221BF",
        "95C26D1B760234AFCA5C978BED12B26A0F7583F66129F6E658881970D5D6662A",
        "CB8A0B5F568807D197A0A72258F6EB5EFA87B1427278AC6E3146178DE729372B",
        "4E880D027584767186826A1C9D2358A98B5D352B52D6AE9F95518536C8CDAB52",
        "A0FD3014F363AFA9F04AE5D9BC86DF941C99F37236F580BD55AEF3026371A73D",
        "328415057FF8E5E896F94E7494BA9E04CBAF399DCF42757C04340D9B29285074",
        "836B11E6BF7791DEA544D97568890EB054F1CFE70202E142E07A1981191C881B",
        "C357E641AC0859997B452017E85F67EC60670111C62473953F82B0045D71CA17",
        "69A389BC972D545C9FA37DFF482447BADAA0F02172E4C3DC160E8799E74885E4",
        "CC2DC798A56BED86304DA853145E89564CB698EB2C38451EFC91F9AE19914548",
        "A5EA33462A1CD14B06CCAEB70F0ADDD69417F3716160DBD836D29F47C1C29648",
        "D5A2D6791443A2A024D42A1D9521286E7600736F0D089AFD25F74A163D6CF3D9",
    };
    internal static MethodBase[] Methods()
    {
        var result = new List<MethodBase>();
        void Add(Type t, params string[] names)
        {
            foreach (string name in names)
                result.Add(AccessTools.Method(t, name) ?? throw new InvalidOperationException("xml-contract-method-" + name));
        }
        Add(typeof(LoadedModManager), "LoadAllActiveMods", "ApplyPatches", "CombineIntoUnifiedXML", "ErrorCheckPatches",
            "ClearCachedPatches");
        Add(typeof(TKeySystem), "Parse", "ParseDefNode", "get_ShouldUseHardcodedMapping", "Clear");
        Add(typeof(ModContentPack), "get_Patches");
        Add(typeof(PatchOperation), "Apply", "Complete", "ConfigErrors");
        foreach (string suffix in new[] { "Add", "AddModExtension", "Insert", "Remove", "Replace", "SetName",
            "AttributeAdd", "AttributeRemove", "AttributeSet", "Test", "Sequence", "Conditional" })
        {
            Type t = typeof(PatchOperation).Assembly.GetType("Verse.PatchOperation" + suffix, true)!;
            Add(t, "ApplyWorker");
        }
        Add(typeof(PatchOperationSequence), "Complete");
        // SelectMany's native selector must not substitute another patch list.
        var nested = typeof(LoadedModManager).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
        result.AddRange(nested.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => m.Name.StartsWith("<ApplyPatches>", StringComparison.Ordinal)).OrderBy(m => m.Name));
        Add(typeof(LoadedModManager), "get_RunningModsListForReading");
        Add(typeof(ModContentPack), "get_Name", "get_RootDir", "get_PackageId");
        return result.ToArray();
    }

    internal static bool Allows(out string reason)
    {
        reason = "xml-effective-contract";
        try
        {
            if (!RuntimeIdentity.ValidateBinaryIdentity(out reason)) return false;
            if (!PrepatcherXmlContract.AllowsCallbacks()) { reason = "changed-prepatcher-xml-callback"; return false; }
            if (SettingsGuards.Values.Any(g => !g.AllowsOriginalContract())) { reason = "changed-patch-settings-callback"; return false; }
            if (DeepProfiler.enabled && Prefs.LogVerbose) { reason = "active-deep-profiler"; return false; }
            MethodBase[] methods = Methods();
            if (methods.Length != Bodies.Length) { reason = "xml-method-contract-count"; return false; }
            // Other caches may own the downstream parse boundary. XML-library
            // hooks can observe skipped mutations even with native workers.
            MethodBase? rejected = new[] { AccessTools.Method(typeof(LoadedModManager), "ParseAndProcessXML") }.Cast<MethodBase>()
                .Concat(Harmony.GetAllPatchedMethods().Where(m => m.DeclaringType?.Assembly == typeof(System.Xml.XmlDocument).Assembly))
                .FirstOrDefault(m => !AllowsPatches(m));
            if (rejected != null) { reason = "foreign-xml-hook-" + DescribePatches(rejected); return false; }
            for (int i = 0; i < methods.Length; i++)
            {
                if (!SemanticMethodIdentity.TryHash(methods[i], out string hash, out reason) || hash != Bodies[i])
                { reason = "changed-xml-method-" + methods[i].Name; return false; }
                if (methods[i].Name != "ClearCachedPatches" && !AllowsPatches(methods[i])) { reason = "foreign-xml-hook-" + DescribePatches(methods[i]); return false; }
            }
            reason = "admitted-native-prefix";
            return true;
        }
        catch (Exception exception) { reason = "xml-contract-" + exception.GetType().Name; return false; }
    }

    internal static bool AllowsPatches(MethodBase method)
    {
        Patches? patches = Harmony.GetPatchInfo(method);
        return patches == null || !patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers)
            .Concat(patches.Finalizers).Concat(patches.InnerPrefixes).Concat(patches.InnerPostfixes)
            .Any(p => !AllowedSearchPatch(method, p));
    }

    internal static string DescribePatches(MethodBase method)
    {
        Patches? patches = Harmony.GetPatchInfo(method);
        return method.DeclaringType!.FullName + "." + method.Name + ":" + (patches == null ? "none" : string.Join(",",
            patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers)
                .Concat(patches.InnerPrefixes).Concat(patches.InnerPostfixes)
                .Select(p => p.owner + "/" + p.PatchMethod.DeclaringType!.FullName + "." + p.PatchMethod.Name)));
    }

    private static bool AllowedSearchPatch(MethodBase target, Patch patch)
    {
        MethodInfo method = patch.PatchMethod;
        if (PrepatcherXmlContract.AllowsPatch(target, patch)) return true;
        if (AllowsSettingsPrefix(target, patch)) return true;
        if (patch.owner == "wakeup.def-lookup" && method.Module == typeof(XPathPlanRuntime).Module
            && method.DeclaringType == typeof(XPathPlanRuntime) && method.Name == "Transpiler"
            && target == typeof(System.Xml.XmlNode).GetMethod("SelectNodes", new[] { typeof(string) })) return true;
        if (method.DeclaringType == typeof(ProcessedXmlRuntime) && method.Module == typeof(ProcessedXmlRuntime).Module
            && target.DeclaringType == typeof(PatchOperation) && target.Name == "Apply"
            && method.Name is "BeforeOperation" or "AfterOperation") return true;
        if (patch.owner == ProcessedXmlRuntime.Owner && method.DeclaringType == typeof(ProcessedXmlRuntime)
            && method.Module == typeof(ProcessedXmlRuntime).Module && method.Name == "PrepareStage"
            && target.DeclaringType == typeof(LoadedModManager) && target.Name == "ApplyPatches") return true;
        if (method.DeclaringType == typeof(ResolvedInheritanceRuntime) && method.Module == typeof(ProcessedXmlRuntime).Module) return true;
        if (method.DeclaringType == typeof(DefLookupRuntime) && method.Module == typeof(DefLookupRuntime).Module
            && target.DeclaringType == typeof(System.Xml.XmlNode) && target.Name == "SelectSingleNode"
            && method.Name is "SinglePrefix" or "SinglePostfix" or "SingleFinalizer") return true;
        if (patch.owner != "wakeup.def-lookup" || method.Module != typeof(DefLookupRuntime).Module
            || method.DeclaringType != typeof(DefLookupRuntime)) return false;
        return target.DeclaringType == typeof(LoadedModManager) && target.Name == "ApplyPatches"
            ? method.Name is "StagePrefix" or "StageFinalizer"
            : target.Name == "ApplyWorker" && method.Name == "Transpiler";
    }

    private static bool AllowsSettingsPrefix(MethodBase target, Patch patch)
    {
        // This callback executes before our explicitly ordered setup prefix.
        // It has no XML arguments; native loading/deserialization and settings
        // callbacks finish before the descriptor reads actual resulting state.
        MethodInfo method = patch.PatchMethod;
        if (target.DeclaringType != typeof(LoadedModManager) || target.Name != "ApplyPatches"
            || patch.owner != "ModSettingsFrameworkMod" || method.Name != "Prefix"
            || method.DeclaringType?.FullName != "ModSettingsFramework.LoadedModManager_ApplyPatches_Patch"
            || !method.IsStatic || method.ReturnType != typeof(void) || method.GetParameters().Length != 0) return false;
        if (!SettingsGuards.TryGetValue(method, out var guard))
        {
            if (!SemanticMethodIdentity.TryHash(method, out string hash, out _) || hash != SettingsPrefixIdentity
                || !PublishedPatchGuard.TryCreate(method, "wakeup.xml-callback-contract", out guard, true)) return false;
            SettingsGuards.Add(method, guard!);
        }
        return guard!.AllowsOriginalContract();
    }
}
