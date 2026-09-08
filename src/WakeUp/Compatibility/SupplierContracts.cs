// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

namespace WakeUp;

// Fingerprints captured from the original reviewed supplier methods.
// These identify function signatures and instructions, not whole mod versions.
internal static class SupplierContracts
{
    internal static readonly SupplierMethodContract[] Character =
    {
        new("CreateDefaults", "CharacterEditor.Preset", "CreateDefaults", "M|CharacterEditor:CharacterEditor.Preset|CreateDefaults|mscorlib:System.Collections.Generic.Dictionary`2<mscorlib:System.String,!!0>|S|!!0,!!1|System.Core:System.Collections.Generic.HashSet`1<!!1>,mscorlib:System.Func`2<!!1,mscorlib:System.String>,mscorlib:System.Func`2<!!1,!!0>,mscorlib:System.String", "A49B2326B0203C1A85FCE052BBC1B6F6F4B37B6BD1E34861E5DA2D9B6143CAAE"),
        new("ListAllTurrets", "CharacterEditor.PresetObject", "get_ListAllTurrets", "M|CharacterEditor:CharacterEditor.PresetObject|get_ListAllTurrets|System.Core:System.Collections.Generic.HashSet`1<Assembly-CSharp:Verse.ThingDef>|S||", "FDDFA8ADE318F45E683469B4976572FA06663CEEB60FA12DD46D80A5EB58399E"),
        new("Dictionary", "CharacterEditor.PresetObject", "get_DicGunAndTurret", "M|CharacterEditor:CharacterEditor.PresetObject|get_DicGunAndTurret|mscorlib:System.Collections.Generic.Dictionary`2<mscorlib:System.String,Assembly-CSharp:Verse.ThingDef>|S||", "FC70FAAB6414EE81D526AFC01B00ED8DFC6916978DD6F5DF85993EEDF9E12B79"),
        new("Objects", "CharacterEditor.PresetObject", "CreateDefaultObjects", "M|CharacterEditor:CharacterEditor.PresetObject|CreateDefaultObjects|mscorlib:System.Collections.Generic.Dictionary`2<mscorlib:System.String,CharacterEditor:CharacterEditor.PresetObject>|S||", "3591435C0BC7D1B892012E7C9AB17ADD820D7253952ED1E1AB627C4ECB559606"),
        new("Turrets", "CharacterEditor.PresetObject", "CreateDefaultTurrets", "M|CharacterEditor:CharacterEditor.PresetObject|CreateDefaultTurrets|mscorlib:System.Collections.Generic.Dictionary`2<mscorlib:System.String,CharacterEditor:CharacterEditor.PresetObject>|S||", "56E2B433E1D0DF301F7EDC72100C5FCEEAE0E7AE97576CF1760C814171369DDB"),
        new("Constructor", "CharacterEditor.PresetObject", ".ctor", "M|CharacterEditor:CharacterEditor.PresetObject|.ctor|void|I||Assembly-CSharp:Verse.ThingDef", "E73322DF927B5DE606BCA71499481042F531A325A8F7DD9DCD28A4B5CD6E9D3A"),
        new("ListBy", "CharacterEditor.DefTool", "ListBy", "M|CharacterEditor:CharacterEditor.DefTool|ListBy|System.Core:System.Collections.Generic.HashSet`1<!!0>|S|!!0|mscorlib:System.Func`2<!!0,mscorlib:System.Boolean>", "595EF872EA39DB407B35338D244A38DAA6C5A793FA546816ED5799182E42F8BA"),
        new("KeyByValue", "CharacterEditor.Extension", "KeyByValue", "M|CharacterEditor:CharacterEditor.Extension|KeyByValue|!!0|S|!!0,!!1|mscorlib:System.Collections.Generic.Dictionary`2<!!0,!!1>,!!1", "481CA1D2BFBF1829FC267E8B7BB7CE3E41438CC403955552C0788454DF81B356"),
        new("Lookup", "CharacterEditor.WeaponTool", "GetTurretDef", "M|CharacterEditor:CharacterEditor.WeaponTool|GetTurretDef|Assembly-CSharp:Verse.ThingDef|S||Assembly-CSharp:Verse.ThingDef", "8830C6E04DAFC769EB0F21046C993BE75F62C69EBDBB4A285ED8EE0B441CFD66"),
    };
    internal static readonly SupplierMethodContract[] Giddy =
    {
        new("SetDrawOffset", "GiddyUp.TextureUtility", "SetDrawOffset", "M|GiddyUpCore:GiddyUp.TextureUtility|SetDrawOffset|mscorlib:System.Nullable`1<mscorlib:System.Single>|S||Assembly-CSharp:Verse.PawnKindLifeStage", "52D143206CA443E5EDFACCA271DE9516F492BBB561D6A4AB0FC355057D4ABD1C"),
        new("GetBackHeight", "GiddyUp.TextureUtility", "GetBackHeight", "M|GiddyUpCore:GiddyUp.TextureUtility|GetBackHeight|mscorlib:System.Int32|S||UnityEngine.CoreModule:UnityEngine.Texture2D", "D09C41C4015EB2171168867A89E53ADF22FF6249C1772EABD4D1D37CA5983B76"),
        new("GetReadableTexture", "GiddyUp.TextureUtility", "GetReadableTexture", "M|GiddyUpCore:GiddyUp.TextureUtility|GetReadableTexture|UnityEngine.CoreModule:UnityEngine.Texture2D|S||UnityEngine.CoreModule:UnityEngine.Texture2D", "99FDE3C6E4BDAC14BD0F2576521DD442304856E0AEFCB090DF6BC76A08C7E982"),
        new("ProcessPawnKinds", "GiddyUp.Setup", "ProcessPawnKinds", "M|GiddyUpCore:GiddyUp.Setup|ProcessPawnKinds|mscorlib:System.Void|S||0Harmony:HarmonyLib.Harmony", "E10A466601F5404F1D126028C1144EFE8DBA17594F064C11564D083857F4ABBD"),
    };
    internal static readonly SupplierMethodContract[] Repaint =
    {
        new("ShouldStopEarly", "ilyvion.LoadingProgress.LongEventHandler_UpdateCurrentEnumeratorEvent_Patches", "ShouldStopEarly", "M|ilyvion.LoadingProgress:ilyvion.LoadingProgress.LongEventHandler_UpdateCurrentEnumeratorEvent_Patches|ShouldStopEarly|mscorlib:System.Boolean|S||", "2674AA635E1EE28BF40774F2E1B67FF9DD38180FBE74B010F02B231C586F125F"),
        new("Transpiler", "ilyvion.LoadingProgress.LongEventHandler_UpdateCurrentEnumeratorEvent_Patches", "Transpiler", "M|ilyvion.LoadingProgress:ilyvion.LoadingProgress.LongEventHandler_UpdateCurrentEnumeratorEvent_Patches|Transpiler|mscorlib:System.Collections.Generic.IEnumerable`1<0Harmony:HarmonyLib.CodeInstruction>|S||mscorlib:System.Collections.Generic.IEnumerable`1<0Harmony:HarmonyLib.CodeInstruction>,mscorlib:System.Reflection.Emit.ILGenerator", "FBE8DAEEC1D0931570A2C2A94F4F06A2DEBBE7E1DD728E37E390679D1D778E79"),
    };
    internal static readonly SupplierMethodContract[] PngBridge =
    {
        new("Factory", "ilyvion.LoadingProgress.ReloadContentIntReplacement", "ReloadContentInt", "M|ilyvion.LoadingProgress:ilyvion.LoadingProgress.ReloadContentIntReplacement|ReloadContentInt|mscorlib:System.Collections.IEnumerable|S||Assembly-CSharp:Verse.ModContentPack", "3A3596F51889BC6A0D8BEE2BB44C5593023F6DA69D833BBB4075C7C6262C67D0"),
        new("Iterator", "ilyvion.LoadingProgress.ReloadContentIntReplacement+<ReloadContentInt>d__0", "MoveNext", "M|ilyvion.LoadingProgress:ilyvion.LoadingProgress.ReloadContentIntReplacement+<ReloadContentInt>d__0|MoveNext|mscorlib:System.Boolean|I||", "CB7EF9E6F51A8EBB4870A974B7E06FFE5985D9F0F9B9BD10F0329FEA4A7A9560"),
        new("Tracker", "ilyvion.LoadingProgress.ModContentPack_LoadingDataTracker_Patches", "ReloadContentIntPrefix", "M|ilyvion.LoadingProgress:ilyvion.LoadingProgress.ModContentPack_LoadingDataTracker_Patches|ReloadContentIntPrefix|mscorlib:System.Void|S||Assembly-CSharp:Verse.ModContentPack,mscorlib:System.Boolean", "E644371F78331254C28BA44BC5327EF05FA291618A2A5E7513B0E715F155498D"),
        new("Progress", "ilyvion.LoadingProgress.ModContentPack_ReloadContentInt_Patch", "ProgressPrefix", "M|ilyvion.LoadingProgress:ilyvion.LoadingProgress.ModContentPack_ReloadContentInt_Patch|ProgressPrefix|mscorlib:System.Void|S||Assembly-CSharp:Verse.ModContentPack", "388411759B503417A45DA4BEC9A66D4023BF15F04208771E8DB9499D8A61171B"),
        new("Skip", "ilyvion.LoadingProgress.ModContentPack_ReloadContentInt_Patch", "Prefix", "M|ilyvion.LoadingProgress:ilyvion.LoadingProgress.ModContentPack_ReloadContentInt_Patch|Prefix|mscorlib:System.Boolean|S||Assembly-CSharp:Verse.ModContentPack", "6B8D22C94BA37C27B99174E4855BCA9266FD071F6223487D36EEBCFD5B3521B6"),
    };
    internal static readonly SupplierMethodContract[] Gagarin =
    {
        new("Method0", "Gagarin.CachedDefHelper", "Load", "M|Gagarin:Gagarin.CachedDefHelper|Load|mscorlib:System.Void|S||System.Xml:System.Xml.XmlDocument,mscorlib:System.Collections.Generic.Dictionary`2<System.Xml:System.Xml.XmlNode,Assembly-CSharp:Verse.LoadableXmlAsset>", "A7EE541315FD82ED12F5A5EEEFF4A5B6469CF146F36FB12791ECA6CBD488287A"),
        new("Method1", "Gagarin.LoadableXmlAsset_Constructor_Patch", "Postfix_FileInfo", "M|Gagarin:Gagarin.LoadableXmlAsset_Constructor_Patch|Postfix_FileInfo|mscorlib:System.Void|S||Assembly-CSharp:Verse.LoadableXmlAsset,mscorlib:System.IO.FileInfo,Assembly-CSharp:Verse.ModContentPack", "7B77391A37FE4063336BD5518A751210403F247A13B2C8AB0AEDEEE9A7F57CE7"),
        new("Method2", "Gagarin.LoadableXmlAsset_Constructor_Patch", "Process", "M|Gagarin:Gagarin.LoadableXmlAsset_Constructor_Patch|Process|mscorlib:System.Void|S||Assembly-CSharp:Verse.LoadableXmlAsset,mscorlib:System.String", "7E24160DB66D4B1FA7BF209E07A0116652B7B1582D24CAD8DE914468FD1BD6BC"),
        new("Method3", "Gagarin.Context", "get_IsUsingCache", "M|Gagarin:Gagarin.Context|get_IsUsingCache|mscorlib:System.Boolean|S||", "644A510039AC9A02A764EB311683ADE422384B596B82F097883EF289CDD4A7F0"),
        new("Method4", "Gagarin.Context", "get_IsLoadingModXML", "M|Gagarin:Gagarin.Context|get_IsLoadingModXML|mscorlib:System.Boolean|S||", "6F19EF21A2F29C5C4FFCC6F141420A5E8E256B141A907E447B4AF195309AD4F7"),
        new("Method5", "Gagarin.Context", "get_IsLoadingPatchXML", "M|Gagarin:Gagarin.Context|get_IsLoadingPatchXML|mscorlib:System.Boolean|S||", "81B1970657C66C1D772556F927398159D888F254F0DD2FD8FFB1A17AB5594E82"),
        new("Method6", "Gagarin.GagarinEnvironmentInfo", "get_UnifiedXmlFilePath", "M|Gagarin:Gagarin.GagarinEnvironmentInfo|get_UnifiedXmlFilePath|mscorlib:System.String|S||", "2F3FB7D42B032CDB577B11A512915B0EC132B6DFD602EFF56D95EF26A22CF898"),
        new("Method7", "Gagarin.LoadedModManager_Patch+CombineIntoUnifiedXML_Patch", "Prefix", "M|Gagarin:Gagarin.LoadedModManager_Patch+CombineIntoUnifiedXML_Patch|Prefix|mscorlib:System.Boolean|S||mscorlib:System.Collections.Generic.List`1<Assembly-CSharp:Verse.LoadableXmlAsset>,System.Xml:System.Xml.XmlDocument&,mscorlib:System.Collections.Generic.Dictionary`2<System.Xml:System.Xml.XmlNode,Assembly-CSharp:Verse.LoadableXmlAsset>", "1099707DC51759C203AC8A73283B976D7A2DCFDD27FB0CF557255F120CFB69CD"),
        new("Method8", "Gagarin.LoadedModManager_Patch+CombineIntoUnifiedXML_Patch", "Postfix", "M|Gagarin:Gagarin.LoadedModManager_Patch+CombineIntoUnifiedXML_Patch|Postfix|mscorlib:System.Void|S||System.Xml:System.Xml.XmlDocument,mscorlib:System.Collections.Generic.Dictionary`2<System.Xml:System.Xml.XmlNode,Assembly-CSharp:Verse.LoadableXmlAsset>", "0752FD110584464F45421B974EED5067FD5F916A4251A0C842B902D4C85D7998"),
        new("Method9", "Gagarin.LoadedModManager_Profiler+CombineIntoUnifiedXML_Profiler", "Prefix", "M|Gagarin:Gagarin.LoadedModManager_Profiler+CombineIntoUnifiedXML_Profiler|Prefix|mscorlib:System.Void|S||", "B2170882A7F80995C36A103EECEA3897706CFD417C0D03DC5BC665A761180F2D"),
        new("Method10", "Gagarin.LoadedModManager_Profiler+CombineIntoUnifiedXML_Profiler", "Postfix", "M|Gagarin:Gagarin.LoadedModManager_Profiler+CombineIntoUnifiedXML_Profiler|Postfix|mscorlib:System.Void|S||", "C6F7CBB86DB45C6CBE26F23DD4C12C5B3B61658FD2778D5C4D1BFB1DF6F206B6"),
    };
}
