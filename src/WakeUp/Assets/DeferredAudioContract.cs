// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using System.Reflection;

namespace WakeUp;

internal static class DeferredAudioContract
{
    internal static readonly string[] EffectiveBodies = {
        "B273FFD03B9909C19951B3481D3A9EFA0A763154F6C54503588C4683806EEAD8",
        "27C86420B6A96F41E2C6B9C87AC94D21FDD9CA33B675652BB00FD32491C3D40E",
        "7E2885BF59875CF089A3713534155208606DC7A9407160D86448937EABC6FCB2",
        "08A63CFF210EC408E0BD421C8854252F170DE6600BCF8A9C6548AE1FB897D816",
        "24F66E704335513F3EB6478565A87F333C475A2110D7CA75B1450F5888AD2247",
        "149E1B5AC9FB550D033AF06BDB6B1711F15EE7EABD85998A6F22F9888134DEAC",
        "BCFD945AB55F2372C469775906AFB5FB95987495DDAB52CA8209BF67692CE531",
        "05753410CC5946E2D32B112ECAEF1507E245D1275F5AEC50B75D2A40DB5A387E",
        "3729DB3467DC8E218B28A79E1CDBF4A360A6CBB855CE0E05E102CA769D5FD5B2",
        "7B3B5F95B2B628F231B6AF861FAB1377CF1DAB7F29CBCBA1CEBE198AC17DBAF6",
        "412CBD0595F1612E10A353358DB33261AAB9B920A9045AAE51B45B753E2DCAAD",
        "1BEA2CC3BFCD2FADCD4E63BB2EAEA86388A7C111CD2ED92ADDD083CB38047502",
        "E2BD702B80FD8E6C61BAE69352FCF1D7B0F08FE102202ECDFD497CBDCB65BF52",
        "6E38A614CC52C48F967345AAA255B7EDA79DD93268DDE3FABF94BCB3C875069C",
        "43342AD4A02DF44C47C89A469C5A60E7F6816FA0B67C94A38779C58122D7337B"
    };
    internal static MethodBase[] Methods(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        MethodInfo Named(string type, string name) => assembly.GetType(type, true).GetMethods(flags).Single(m => m.Name == name);
        return new MethodBase[] {
            Named("Verse.ModContentHolder`1", "ReloadAll"), Named("Verse.ModContentHolder`1", "Get"),
            Named("Verse.ModContentHolder`1", "GetAllUnderPath"), Named("Verse.ModContentHolder`1", "ClearDestroy"),
            Named("Verse.ModContentPack", "GetContentHolder"), Named("Verse.ModContentPack", "AnyNonTranslationContentLoaded"),
            assembly.GetType("Verse.SongDef", true).GetMethods(flags).Single(m => m.Name.StartsWith("<ResolveReferences>", StringComparison.Ordinal)),
            assembly.GetType("Verse.ContentFinder`1", true).GetNestedTypes(flags).Single(t => t.Name.StartsWith("<GetAllInFolder>", StringComparison.Ordinal)).GetMethod("MoveNext", flags)!,
            Named("Verse.ModContentLoader`1", "LoadItem"), Named("Verse.ModContentLoader`1", "ShouldStreamAudioClipFromFile"),
            Named("Verse.ModContentPack", "GetAllFilesForMod"),
            assembly.GetType("RuntimeAudioClipLoader.Manager", true).GetMethods(flags).Single(m => m.Name == "Load" && m.GetParameters().Length == 6),
            Named("Verse.ModContentLoader`1", "IsAcceptableExtension"), Named("Verse.ModContentLoader`1", "GetFormat"),
            Named("Verse.ContentFinder`1", "Get")
        };
    }
    internal static void Validate(Assembly assembly)
    {
        MethodBase[] methods = Methods(assembly);
        if (methods.Length != EffectiveBodies.Length) throw new InvalidOperationException("audio method set changed");
        for (int i = 0; i < methods.Length; i++)
            if (!SemanticMethodIdentity.TryHash(methods[i], out string hash, out _) || hash != EffectiveBodies[i])
                throw new InvalidOperationException("effective audio body changed: " + methods[i].Name);
    }
}
