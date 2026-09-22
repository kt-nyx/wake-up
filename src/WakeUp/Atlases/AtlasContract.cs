// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Original GOG rev573 method bodies, independently captured from the pinned
// 4a170804... game DLL. This qualifies the actual functions, not a game label.
internal static class AtlasContract
{
    private static readonly Dictionary<string, string> Expected = new(StringComparer.Ordinal)
    {
        ["Verse.GlobalTextureAtlasManager:Boolean TryGetStaticTile(Verse.TextureAtlasGroup, UnityEngine.Texture2D, Verse.StaticTextureAtlasTile ByRef, Boolean)"] = "CEFFF65A54EB831C4523BECE0284B27578468D829C8B74A00C2D889EB634BA6B",
        ["Verse.GlobalTextureAtlasManager:Boolean TryInsertStatic(Verse.TextureAtlasGroup, UnityEngine.Texture2D, UnityEngine.Texture2D)"] = "40EE55E30620E7E31CA2837C544EF7ED485C068B63F027A657B5E7F03C5AF9CB",
        ["Verse.GlobalTextureAtlasManager:Void <BakeStaticAtlases>g__FlushBatch|7_0(Verse.TextureAtlasGroupKey, <>c__DisplayClass7_0 ByRef)"] = "412CA30C72FA8975AB03BC108A908AF61FEFB27A60C81D563437B7E090745486",
        ["Verse.GlobalTextureAtlasManager:Void BakeStaticAtlases()"] = "E07BA26D6F9C0AABF0043870F4D91A8137DAE48753A73857A6C493156B4BFECB",
        ["Verse.GlobalTextureAtlasManager:Void ClearStaticAtlasBuildQueue()"] = "2B65BF0EAD1D12C9019C2BA7CEBC5565236E2284276A4136BF74488FD016E2D6",
        ["Verse.LongEventHandler:Void ClearQueuedEvents()"] = "64AD37C4630562BE12656B7371E984872869DDD7B4CEA4ED79C03C4B18D5D03D",
        ["Verse.LongEventHandler:Void ExecuteToExecuteWhenFinished()"] = "D66124A98DE410C2F59734EA7D76D304D4D16EBD9DA978200E992B5FC9BFA526",
        ["Verse.LongEventHandler:Void ExecuteWhenFinished(System.Action)"] = "255907211728E3C37B58485BBC25482F1E78F2F2ED55BDD2CC930913009F01A5",
        ["Verse.LongEventHandler:Void ForceExecuteToExecuteWhenFinished()"] = "330F15627699A3DEAA1C9D2A97694D116BFF9E45CCF5293686AF67E4C0A2A280",
        ["Verse.LongEventHandler:Void LongEventsUpdate(Boolean ByRef)"] = "43145D89CF55CBD93858867B52D713F58585A60838318743A0EF9CBFE867DEFA",
        ["Verse.LongEventHandler:Void UpdateCurrentAsynchronousEvent()"] = "074C69DB5382FB3A8AFCB2CC6BF10A655E50D897583F338071D09E3E7D58CCAE",
        ["Verse.PlayDataLoader+<>c:Void <DoPlayLoad>b__4_4()"] = "D0230551223507A7B52CA228F2499C8C48DA25EA18AC30C431109D43D20ADEA7",
        ["Verse.PlayDataLoader:Void ClearAllPlayData()"] = "2E25147F360346038F82B8250EE275F183623AF98C7BA07AAB4DC0B8C2C33C45",
        ["Verse.StaticTextureAtlas+<>c:Int32 <CalcRectsForAtlasNew>b__21_0(UnityEngine.Texture2D)"] = "7E7F78DA3113EE89CCB4967456A76E9D605FC8D3941EDF9192F07751DFBE3EB4",
        ["Verse.StaticTextureAtlas+<>c:Int32 <CalcRectsForAtlasNew>b__21_1(UnityEngine.Texture2D)"] = "93E59462636C5995FD10462C44DBA81099FF1CD06E5BAAEC1F1D47D177FB79F7",
        ["Verse.StaticTextureAtlas+<>c:UnityEngine.Texture2D <CalcRectsForAtlas>b__22_0(UnityEngine.Texture2D)"] = "B803C54ED51E20B31D8E4D453B6E3DE3BC39C3DC49524461B8C90B9AD9A34505",
        ["Verse.StaticTextureAtlas+<>c:Void .cctor()"] = "A92F7F985B5C7656A45B67082FC8E904F75F78DE96295595CBF46DDB2B3BC526",
        ["Verse.StaticTextureAtlas+<>c:Void .ctor()"] = "D95C6A391E13F2A9284C439347E90CB4B495153E6C65D9180629BE0B7B857A10",
        ["Verse.StaticTextureAtlas+<>c__DisplayClass21_0:UnityEngine.Vector2 <CalcRectsForAtlasNew>b__2(UnityEngine.Texture2D)"] = "3D1B22DEA8BF55BFFEF8D20BECD996666C74F07A44EF04D2D9EBC10B5B0CB141",
        ["Verse.StaticTextureAtlas+<>c__DisplayClass21_0:Void .ctor()"] = "D95C6A391E13F2A9284C439347E90CB4B495153E6C65D9180629BE0B7B857A10",
        ["Verse.StaticTextureAtlas:Boolean TryGetTile(UnityEngine.Texture, Verse.StaticTextureAtlasTile ByRef)"] = "7E20A1AD92D1824B3F8D95D42A19B1E7CACC645B6F6C0A6EA682B0EFD53CE5B3",
        ["Verse.StaticTextureAtlas:Int32 CalculateMaxMipmapsForDxtSupport(UnityEngine.Texture2D)"] = "BDFF47A03594527C06FF873FC87C9B499859F623F51385B478398D959BB73DE6",
        ["Verse.StaticTextureAtlas:Int32 get_MaxAtlasSize()"] = "7F3CBD9BABF66CAE2D72E0300A478C22F02CE1779920FE50CE71F71B34085185",
        ["Verse.StaticTextureAtlas:Int32 get_MaxPixelsPerAtlas()"] = "AA9FB2DC0252E0CAE77BF5B9DA36C4FBC92BA4C90A6857F280A43223812EF0A7",
        ["Verse.StaticTextureAtlas:UnityEngine.Rect[] CalcRectsForAtlas()"] = "75AA12B98BD225B8F171F7AEB89AF4832D1C4D0D30E2962C8C049D00696BEE94",
        ["Verse.StaticTextureAtlas:UnityEngine.Rect[] CalcRectsForAtlasNew(Int32, Boolean)"] = "00485871E00AC3B3E0471CAD6BC1A7156620BBE13D95419B49ABA4664A24043E",
        ["Verse.StaticTextureAtlas:UnityEngine.Texture2D FastCompressDXT(UnityEngine.Texture2D, Boolean)"] = "5886FBD4F39C70EFAEC23ACF2501CCDD110F53D550B08C95A677A95075959B42",
        ["Verse.StaticTextureAtlas:UnityEngine.Texture2D get_ColorTexture()"] = "D19FC19473519A9DE5394495D00F1B5372F7D1693BE92445BBFDE1CA841D7EB9",
        ["Verse.StaticTextureAtlas:UnityEngine.Texture2D get_MaskTexture()"] = "09AD17A7C306D8CF2735320E95BEA25CD9AA07CDEB182234EC90EA85462E172E",
        ["Verse.StaticTextureAtlas:Void .ctor(Verse.TextureAtlasGroupKey)"] = "DB536F66E91B71D3F3367374C1066932E0FC85FD6E15DB452DEB1ADE0F39F806",
        ["Verse.StaticTextureAtlas:Void ApplyTextureCompression(Boolean)"] = "9F5BD04D5F0DD64061D17BDE3CCE6D70C5E7F2BDC4BECFFE15EE9B9407512247",
        ["Verse.StaticTextureAtlas:Void Bake(Boolean)"] = "CC8B2EEAC2546B2ED9F507465FEFC52598085036C45FAB77C88D66D8EA05A3E4",
        ["Verse.StaticTextureAtlas:Void BlitTexturesToColorAtlas(UnityEngine.Rect[], Boolean)"] = "2227B96E5A283E8694462093FCFBD216F7EE45986F17FF20B794D0D92756E77E",
        ["Verse.StaticTextureAtlas:Void BuildMaskAtlas(UnityEngine.Rect[], Boolean)"] = "3712E89730E55C68A1BA00924763D511F47DA15437D27C46652E8FCACBAD4726",
        ["Verse.StaticTextureAtlas:Void BuildMeshesForUvs(UnityEngine.Rect[])"] = "F6756BFB292E722041B6FDFE2AEB1152F0C57E1D382E87BF7E0D553166D99DEA",
        ["Verse.StaticTextureAtlas:Void Destroy()"] = "E44C5F2E46168E5E3099E77E282A1AE6BBBC714AB4766478AAD0112911C7911A",
        ["Verse.StaticTextureAtlas:Void GenerateMipmapsWithCompute(UnityEngine.Texture2D)"] = "B4D1928FE2708381F3CCE2D61D3706D6ACECDBB9D31E86E7766D0CB177E45CB2",
        ["Verse.StaticTextureAtlas:Void Insert(UnityEngine.Texture2D, UnityEngine.Texture2D)"] = "6BBB3AFCF43C4F477944F32B0007974BF6352ECC808426050B02B0859FC4EE84",
        ["Verse.TextureAtlasHelper:UnityEngine.Mesh CreateMeshForUV(UnityEngine.Rect, Single)"] = "5D45ACEC747F496B19768C9178CFB15136DEE4F839BFD3E126E4C2D583AE72E8",
    };

    internal static readonly MethodBase[] Methods = FindMethods();
    internal static bool Matches(MethodBase method) =>
        Expected.TryGetValue(Key(method), out string expected)
        && SemanticMethodIdentity.TryHash(method, out string actual, out _) && actual == expected;
    internal static string Key(MethodBase method) => method.DeclaringType!.FullName + ":" + method;
    internal static void Validate()
    {
        if (Methods.Length != Expected.Count || Methods.Any(method => !Matches(method)))
            throw new InvalidOperationException("The native atlas or startup callback code changed.");
    }
    private static MethodBase[] FindMethods()
    {
        var methods = new List<MethodBase>();
        void Add(Type type)
        {
            methods.AddRange(type.GetMethods(AccessTools.allDeclared));
            methods.AddRange(type.GetConstructors(AccessTools.allDeclared));
            foreach (Type nested in type.GetNestedTypes(AccessTools.allDeclared)) Add(nested);
        }
        Add(typeof(StaticTextureAtlas));
        methods.AddRange(typeof(GlobalTextureAtlasManager).GetMethods(AccessTools.allDeclared));
        methods.AddRange(typeof(LongEventHandler).GetMethods(AccessTools.allDeclared));
        methods.AddRange(typeof(PlayDataLoader).GetMethods(AccessTools.allDeclared));
        foreach (Type nested in typeof(PlayDataLoader).GetNestedTypes(AccessTools.allDeclared))
            methods.AddRange(nested.GetMethods(AccessTools.allDeclared));
        methods.AddRange(typeof(TextureAtlasHelper).GetMethods(AccessTools.allDeclared));
        return methods.Where(method => Expected.ContainsKey(Key(method))).Distinct().ToArray();
    }
}
