// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld.IO;
using Verse;

namespace WakeUp;

// Contracts cover source discovery and the parsing/merging that a ready table
// replaces. Translation assignment remains native, including its S03 hooks.
internal static class ParsedLanguageContract
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private static readonly Type FileType = typeof(VirtualFile).Assembly.GetType("RimWorld.IO.FilesystemFile", true)!;
    private static readonly Type TarFileType = typeof(VirtualFile).Assembly.GetType("RimWorld.IO.TarFile", true)!;
    internal static readonly Type[] ConverterTypes = new[] { "0_17_AndLower", "0_18", "0_19", "1_0", "1_2", "1_3", "1_4", "1_5", "Universal" }
        .Select(n => typeof(BackCompatibility).Assembly.GetType("Verse.BackCompatibilityConverter_" + n, true)!).ToArray();

    // Populate only from the pinned ORIGINAL GOG assembly, never learn bodies
    // from a running modded game. Missing entries deliberately refuse reuse.
    internal static readonly IReadOnlyDictionary<string, string> Expected = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["M|Assembly-CSharp:RimWorld.IO.FilesystemFile|ReadAllText|mscorlib:System.String|I||"] = "A8E6CFD94A156AA953D473D517469AF30C0B85D00400720BCD215168D889AE6C",
        ["M|Assembly-CSharp:RimWorld.IO.FilesystemFile|get_FullPath|mscorlib:System.String|I||"] = "D62C6508C3FB1FA8AF46B38A93A3EBA1CF27A9AAAF0D3FBA8CFECA73BC80BBE9",
        ["M|Assembly-CSharp:RimWorld.IO.FilesystemFile|get_Name|mscorlib:System.String|I||"] = "06918284E8BA092697EAB1C4719AACCD4AF206D3FA1064882164DB5A5D4CABD2",
        ["M|Assembly-CSharp:RimWorld.IO.TarFile|CheckAccess|mscorlib:System.Void|I||"] = "A6D30495EA8675CE482431FF740EB81695BAD9E19C5804A44378ABCCD5B1764E",
        ["M|Assembly-CSharp:RimWorld.IO.TarFile|ReadAllText|mscorlib:System.String|I||"] = "51FEB934A60EBE6EBC2E75959C8A040F22D9E40ACE0C69B58DBCE645F2D9B3B7",
        ["M|Assembly-CSharp:RimWorld.IO.TarFile|get_FullPath|mscorlib:System.String|I||"] = "46B7A3896DFAC6E0459E04991BC41FC695037D2A620CEA64CFBF2A5B47464C57",
        ["M|Assembly-CSharp:RimWorld.IO.TarFile|get_Name|mscorlib:System.String|I||"] = "518ECB21CCE5F201D3FC7F162B385CDF2CAA3C749835E1B35166DB60322122E2",
        ["M|Assembly-CSharp:RimWorld.IO.VirtualFileInfoExt|LoadAsXDocument|System.Xml.Linq:System.Xml.Linq.XDocument|S||mscorlib:System.String"] = "CE74F34A0BB3092C5554DB3E1DB855803D68761C66F89175C43E4325F88D5849",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_0_17_AndLower|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "FC1D3F093CE27D4F9E8306D92530E09CF2DAB999D462CA692CCEA8605CA6434F",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_0_18|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "28092D596ECFEA5E46718AC6D368B394E50BAB7B07886FDFEA5ABFAF1E3ADCB2",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_0_19|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "9A90306E9B2211A1A1D755F1F6F6A2D5C8B3C2F4A3779C8C844999C289D31778",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_1_0|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "26679CA79AF58E2C7B9EB34E1FEF0C444A8B8D29F8AEAB4FE7B37AA3F158AF83",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_1_2|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "05BE9420DD81A92CCC4ED396347913AA43D3035751E87060ED88F271743F1D0E",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_1_3|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "03391C4C720980D9B38977E1A55FD6018CA991228001B4785CF77CC406E8F8CE",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_1_4|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "1536A268625FCB1CC28EA82F0944577AEF1D7C8903D29A694DACC9884063A036",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_1_5|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "A33D4B0C1EFED568DB55F5D2AAFD6ACA6966CF6F4856A8F1D6517AA4323D5D00",
        ["M|Assembly-CSharp:Verse.BackCompatibilityConverter_Universal|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "9B09E0D27F1AB126FADE608D5B2BB8003D0128FB6E52C30A1CDB79BC3FFF8F8C",
        ["M|Assembly-CSharp:Verse.BackCompatibility|BackCompatibleDefName|mscorlib:System.String|S||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode"] = "D1C235DD8029B93634AE57FA99FAEE680DD46C40CBEF2F5434ABDB2C8FEE26DE",
        ["M|Assembly-CSharp:Verse.BackCompatibility|CheckSaveIdenticalToCurrentEnvironment|mscorlib:System.Boolean|S||"] = "256BB0BEB67F74DD2041F2130BBC2F73EB8CFBFF0FFB95E074C194349FCFFD93",
        ["M|Assembly-CSharp:Verse.DefDatabase`1<!0>|GetNamedSilentFail|!0|S||mscorlib:System.String"] = "9898E33AE332276A601A09D9916313C9F5686C417284AC8CB573F0E1F70B0B9D",
        ["M|Assembly-CSharp:Verse.DefDatabase`1<!0>|GetNamed|!0|S||mscorlib:System.String,mscorlib:System.Boolean"] = "51976359DE183604BBD191F8F4E37A1164875558E1160810B5B0FE1EA5DAFE3C",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage+<>c__DisplayClass15_0|<CheckErrors>b__0|mscorlib:System.Boolean|I||mscorlib:System.Collections.Generic.KeyValuePair`2<mscorlib:System.String,Assembly-CSharp:Verse.DefInjectionPackage+DefInjection>"] = "730D74C0A1E8583F0EFE78ED8E10D42883ABD2ED32349B46ABE2EDAD7A9BBE2C",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage+DefInjection|.ctor|void|I||"] = "D95C6A391E13F2A9284C439347E90CB4B495153E6C65D9180629BE0B7B857A10",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage+DefInjection|get_IsFullListInjection|mscorlib:System.Boolean|I||"] = "8DECF78D8F688474A0F0795D525017303F9C4A7B5116C18542A83777E0B89201",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage|.ctor|void|I||mscorlib:System.Type"] = "D950362B3006C2CCDFC07DD624FA0599950028E538657221062E9BDAD8E719D1",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage|AddDataFromFile|mscorlib:System.Void|I||Assembly-CSharp:RimWorld.IO.VirtualFile,mscorlib:System.Boolean&,mscorlib:System.String"] = "1E722BB9462E7606D32919746C2B2C4CC8C106886F4BB7605F7C73AA17E579F4",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage|BackCompatibleKey|mscorlib:System.String|I||mscorlib:System.String"] = "0110B73E166448991E686DBB0AF05474D570E06D43E045B9AA5896D888D41070",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage|CheckErrors|mscorlib:System.Boolean|I||Assembly-CSharp:RimWorld.IO.VirtualFile,mscorlib:System.String,mscorlib:System.String,mscorlib:System.Boolean"] = "2F294FB382D3EF5B2FEA2C43BC017F266B23F33BE1DCD939A6A0732F4D65F1A1",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage|ProcessedPath|mscorlib:System.String|I||mscorlib:System.String"] = "B2DD9183915C13FE69B94C47910C6F2090937FDC33443B8F2358BC5F8F09AB5E",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage|ProcessedTranslation|mscorlib:System.String|I||mscorlib:System.String"] = "489CEBCD8DAFE25AED1EE134CCDD20A5599227207467FD5CA02745E1499099CE",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage|TryAddFullListInjection|mscorlib:System.Void|I||Assembly-CSharp:RimWorld.IO.VirtualFile,mscorlib:System.String,mscorlib:System.Collections.Generic.List`1<mscorlib:System.String>,mscorlib:System.Collections.Generic.List`1<Assembly-CSharp:Verse.Pair`2<mscorlib:System.Int32,mscorlib:System.String>>"] = "6159502C8133A2CB15277FE3E658EF801BE0DA01743D0DF0599B97411DE588D4",
        ["M|Assembly-CSharp:Verse.DefInjectionPackage|TryAddInjection|mscorlib:System.Void|I||Assembly-CSharp:RimWorld.IO.VirtualFile,mscorlib:System.String,mscorlib:System.String"] = "1464C61E8DE2A80D325B241B6BE2E176FCD4679687742A5639062E9B283FEC8D",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|.ctor|void|I||mscorlib:System.Int32"] = "914AF86A1FDA687BCADE9C65D64A3F1CA9592AA60314A37C860A3B8F09978A98",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|<>m__Finally1|mscorlib:System.Void|I||"] = "71340F7576DE366C54F347D7D8EFE5F40C9F1A2ED872E31A55CC0B49B9758993",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|MoveNext|mscorlib:System.Boolean|I||"] = "D3F37077CB556125BD5C9708746B6B9119575FB728BA0A0A7973B698389886A2",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|System.Collections.Generic.IEnumerable<Verse.DirectXmlLoaderSimple.XmlKeyValuePair>.GetEnumerator|mscorlib:System.Collections.Generic.IEnumerator`1<Assembly-CSharp:Verse.DirectXmlLoaderSimple+XmlKeyValuePair>|I||"] = "3D0C596D9BE8FB3FCBBCC4A8ADAF328BC2710A08FE57E9C372B6B7812A133DE0",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|System.Collections.Generic.IEnumerator<Verse.DirectXmlLoaderSimple.XmlKeyValuePair>.get_Current|Assembly-CSharp:Verse.DirectXmlLoaderSimple+XmlKeyValuePair|I||"] = "2F40F2B19E4469D5A8D0301FDCDC6B5B87295435D5CAFC7639EFEC83D4BD0E0C",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|System.Collections.IEnumerable.GetEnumerator|mscorlib:System.Collections.IEnumerator|I||"] = "9C0E1CBD670311A2BB1E2576E4881862E048FAF062182425F8758B25894336C9",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|System.Collections.IEnumerator.Reset|mscorlib:System.Void|I||"] = "84CE095C467CA976BF96064BD9733A1BBF03B888603B361F885F1AF5060B3539",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|System.Collections.IEnumerator.get_Current|mscorlib:System.Object|I||"] = "47177BEB8A2DA00D0B2FC2399BC3218282C8E9E3C12FD8212A5ECAC6EB0853ED",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple+<ValuesFromXmlFile>d__3|System.IDisposable.Dispose|mscorlib:System.Void|I||"] = "7357CEF9B49F37B3092C41E88C724662931535EE11D420E864833A984CD017F8",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple|ValuesFromXmlFile|mscorlib:System.Collections.Generic.IEnumerable`1<Assembly-CSharp:Verse.DirectXmlLoaderSimple+XmlKeyValuePair>|S||mscorlib:System.String"] = "0F4CA43DEFD42F688CF4288F7BEBEC09E186C4124B78FD70F457B49BB2AEABE8",
        ["M|Assembly-CSharp:Verse.DirectXmlLoaderSimple|ValuesFromXmlFile|mscorlib:System.Collections.Generic.IEnumerable`1<Assembly-CSharp:Verse.DirectXmlLoaderSimple+XmlKeyValuePair>|S||System.Xml.Linq:System.Xml.Linq.XDocument"] = "9B295C93EF41D72576CCC1112C1F8F8B25E26C05A49319C8B452EFBDF94654A1",
        ["M|Assembly-CSharp:Verse.DirectXmlSaveLoadUtility|GetInnerXml|mscorlib:System.String|S||System.Xml.Linq:System.Xml.Linq.XElement"] = "5751D792B5664F1330FC5405087D2B8ADD396C77A3DFCEED4512E3F27E13F358",
        ["M|Assembly-CSharp:Verse.GenCollection|SetOrAdd|mscorlib:System.Void|S|!!0,!!1|mscorlib:System.Collections.Generic.Dictionary`2<!!0,!!1>,!!0,!!1"] = "50954AF0A82290EF75481EC446674694D17CFDEC4A76ACB020F43D38225FB180",
        ["M|Assembly-CSharp:Verse.GenCollection|SetOrAdd|mscorlib:System.Void|S|mscorlib:System.String,Assembly-CSharp:Verse.DefInjectionPackage+DefInjection|mscorlib:System.Collections.Generic.Dictionary`2<mscorlib:System.String,Assembly-CSharp:Verse.DefInjectionPackage+DefInjection>,mscorlib:System.String,Assembly-CSharp:Verse.DefInjectionPackage+DefInjection"] = "10DBAFC1CB09F537DC7722C281EDB12C8B682AAD8CED014DA9A28D3BD4DA9E4B",
        ["M|Assembly-CSharp:Verse.GenCollection|SetOrAdd|mscorlib:System.Void|S|mscorlib:System.String,Assembly-CSharp:Verse.LoadedLanguage+KeyedReplacement|mscorlib:System.Collections.Generic.Dictionary`2<mscorlib:System.String,Assembly-CSharp:Verse.LoadedLanguage+KeyedReplacement>,mscorlib:System.String,Assembly-CSharp:Verse.LoadedLanguage+KeyedReplacement"] = "DDF314E72FD62304C8D1A58CBE1D99ADD77C697B84C94B5EABAAF8F9664793E2",
        ["M|Assembly-CSharp:Verse.GenDefDatabase|GetDefSilentFail|Assembly-CSharp:Verse.Def|S||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean"] = "AF5E41452C4C9BEAF5C2C6FE2EFAFE27C551676E77F68589CAD202CF9D2E2938",
        ["M|Assembly-CSharp:Verse.GenText+<LinesFromString>d__24|.ctor|void|I||mscorlib:System.Int32"] = "F2E7824504093A11DA44128272664CC3CDD56B5584A3456C72A8EF200E6576CB",
        ["M|Assembly-CSharp:Verse.GenText+<LinesFromString>d__24|MoveNext|mscorlib:System.Boolean|I||"] = "30358059C02F45715F823A38F1AAB44BBF615A2F01317EC700EDCA7BB54E3B0C",
        ["M|Assembly-CSharp:Verse.GenText+<LinesFromString>d__24|System.Collections.Generic.IEnumerable<System.String>.GetEnumerator|mscorlib:System.Collections.Generic.IEnumerator`1<mscorlib:System.String>|I||"] = "F3F2587791EBCBB6DA404E3869D05C84A297D4F0097E97A80734328DA91D09C0",
        ["M|Assembly-CSharp:Verse.GenText+<LinesFromString>d__24|System.Collections.Generic.IEnumerator<System.String>.get_Current|mscorlib:System.String|I||"] = "D83CF01CE889056AEC701FCE66D794462D57741533F35344753FC912D49049E3",
        ["M|Assembly-CSharp:Verse.GenText+<LinesFromString>d__24|System.Collections.IEnumerable.GetEnumerator|mscorlib:System.Collections.IEnumerator|I||"] = "C4FF9389F3F9B1E068117D4FC6CF2C2C4F69F1A9A0999956E61E2B1B98AA611D",
        ["M|Assembly-CSharp:Verse.GenText+<LinesFromString>d__24|System.Collections.IEnumerator.Reset|mscorlib:System.Void|I||"] = "84CE095C467CA976BF96064BD9733A1BBF03B888603B361F885F1AF5060B3539",
        ["M|Assembly-CSharp:Verse.GenText+<LinesFromString>d__24|System.Collections.IEnumerator.get_Current|mscorlib:System.Object|I||"] = "D83CF01CE889056AEC701FCE66D794462D57741533F35344753FC912D49049E3",
        ["M|Assembly-CSharp:Verse.GenText+<LinesFromString>d__24|System.IDisposable.Dispose|mscorlib:System.Void|I||"] = "5C42FE5C50EB9FE635C75076F5E8716FAE823825732EFF59BED61D47ED9BFDEA",
        ["M|Assembly-CSharp:Verse.GenText|LinesFromString|mscorlib:System.Collections.Generic.IEnumerable`1<mscorlib:System.String>|S||mscorlib:System.String"] = "412BD5226E8ECA2D2361B65E0CA6A227074DAC9215B22A6CBB6FBE32284570C7",
        ["M|Assembly-CSharp:Verse.LanguageDatabase|Clear|mscorlib:System.Void|S||"] = "90A961E3F79932F44386CE719365E584A5BF7BE57A77947A253BEF439983E2DF",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<>c__DisplayClass42_0|<LoadData>b__0|mscorlib:System.Void|I||"] = "E08FFE6D8CAF5E5255E15864BFA20F4CBCCB2D10D37E84DFFBFDE190BF135CBC",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<>c__DisplayClass46_0|<LoadFromFile_DefInject>b__0|mscorlib:System.Boolean|I||Assembly-CSharp:Verse.DefInjectionPackage"] = "41D36228D9568DF9A7D689CD731386FE5F339DAD23870610B19235C62B018150",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<>c__DisplayClass47_0|<EnsureAllDefTypesHaveDefInjectionPackage>b__0|mscorlib:System.Boolean|I||Assembly-CSharp:Verse.DefInjectionPackage"] = "499A8361554531C9E427D4B777C85470A37BD9035FB8436CE203C03740994351",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<>c|<LoadData>b__42_1|mscorlib:System.String|I||Assembly-CSharp:RimWorld.IO.VirtualFile"] = "832C49A75F97B45BEB1FA9B5EF7E2B1665C97540515127061DC605D2A24F91AD",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<>c|<LoadData>b__42_2|mscorlib:System.String|I||Assembly-CSharp:RimWorld.IO.VirtualFile"] = "832C49A75F97B45BEB1FA9B5EF7E2B1665C97540515127061DC605D2A24F91AD",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|.ctor|void|I||mscorlib:System.Int32"] = "B6B72DD393BD51CBFCF0442C7BBD96ADA0D0DEB7B04ABE392982AB3DCC7FE7DC",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|<>m__Finally1|mscorlib:System.Void|I||"] = "EF18061E89C7092E9AA59D6E2031C42C36A80340406C6C602BE1727C22F702FA",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|<>m__Finally2|mscorlib:System.Void|I||"] = "84ABDEC5EA00E8B54E14FDC782DBAAC9A99A6BA3B02AE42FF01F19AFD2F53463",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|MoveNext|mscorlib:System.Boolean|I||"] = "28E96B0401BE67C164EA1064D693BD10A34FAE6DCA8C99A59610867D38A9790B",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|System.Collections.Generic.IEnumerable<System.Tuple<RimWorld.IO.VirtualDirectory,Verse.ModContentPack,System.String>>.GetEnumerator|mscorlib:System.Collections.Generic.IEnumerator`1<mscorlib:System.Tuple`3<Assembly-CSharp:RimWorld.IO.VirtualDirectory,Assembly-CSharp:Verse.ModContentPack,mscorlib:System.String>>|I||"] = "22A179CB943D59E21F69267E45DDA664278C5073755DAE34477A39A85984464B",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|System.Collections.Generic.IEnumerator<System.Tuple<RimWorld.IO.VirtualDirectory,Verse.ModContentPack,System.String>>.get_Current|mscorlib:System.Tuple`3<Assembly-CSharp:RimWorld.IO.VirtualDirectory,Assembly-CSharp:Verse.ModContentPack,mscorlib:System.String>|I||"] = "F8EE8035E142C8A34872DD711D723F44FE36AAA400A6130D45697A7DF40D8AE8",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|System.Collections.IEnumerable.GetEnumerator|mscorlib:System.Collections.IEnumerator|I||"] = "7BDFB6381628049D29127EAA3A765ACA62CB498F74C9C87369AFE0E2EE7B29CF",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|System.Collections.IEnumerator.Reset|mscorlib:System.Void|I||"] = "84CE095C467CA976BF96064BD9733A1BBF03B888603B361F885F1AF5060B3539",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|System.Collections.IEnumerator.get_Current|mscorlib:System.Object|I||"] = "F8EE8035E142C8A34872DD711D723F44FE36AAA400A6130D45697A7DF40D8AE8",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+<get_AllDirectories>d__31|System.IDisposable.Dispose|mscorlib:System.Void|I||"] = "2CD2B49087CD4FC2A2028688B6D50F9E2EF4284CBEA748652775FDEE4A5369DD",
        ["M|Assembly-CSharp:Verse.LoadedLanguage+KeyedReplacement|.ctor|void|I||"] = "D95C6A391E13F2A9284C439347E90CB4B495153E6C65D9180629BE0B7B857A10",
        ["M|Assembly-CSharp:Verse.LoadedLanguage|EnsureAllDefTypesHaveDefInjectionPackage|mscorlib:System.Void|I||"] = "A82BF9644096A542DBFFC2B19274151F7792E3E99E0AFC4A777138A114638E42",
        ["M|Assembly-CSharp:Verse.LoadedLanguage|LoadData|mscorlib:System.Void|I||"] = "E3C827B4CCE907DFD93C51C6CF246FF4651E2DAC9A761B2B7CF555BCCFE17A65",
        ["M|Assembly-CSharp:Verse.LoadedLanguage|LoadFromFile_DefInject|mscorlib:System.Void|I||Assembly-CSharp:RimWorld.IO.VirtualFile,mscorlib:System.Type,mscorlib:System.String"] = "5012EF84725C82E315FE16EEFD78239E60716570D448B510BFC423A03FDBE8C9",
        ["M|Assembly-CSharp:Verse.LoadedLanguage|LoadFromFile_Keyed|mscorlib:System.Void|I||Assembly-CSharp:RimWorld.IO.VirtualFile,mscorlib:System.String"] = "A2C51127B2A54412022045F97B63A873BE53CF23F6D8038B90C1A90B753D43EA",
        ["M|Assembly-CSharp:Verse.LoadedLanguage|LoadFromFile_Strings|mscorlib:System.Void|I||Assembly-CSharp:RimWorld.IO.VirtualFile,Assembly-CSharp:RimWorld.IO.VirtualDirectory"] = "7BCDDB748E8CA8DBC88D220DF2BACEA841CC2CE21E5F29EEFBD1C7B348F0101E",
        ["M|Assembly-CSharp:Verse.LoadedLanguage|TryRegisterFileIfNew|mscorlib:System.Boolean|I||mscorlib:System.Tuple`3<Assembly-CSharp:RimWorld.IO.VirtualDirectory,Assembly-CSharp:Verse.ModContentPack,mscorlib:System.String>,mscorlib:System.String"] = "133550A35E268D4008465AEE2EC21E9556652AE687CC4FFE56112542B32752F7",
        ["M|Assembly-CSharp:Verse.LoadedLanguage|get_AllDirectories|mscorlib:System.Collections.Generic.IEnumerable`1<mscorlib:System.Tuple`3<Assembly-CSharp:RimWorld.IO.VirtualDirectory,Assembly-CSharp:Verse.ModContentPack,mscorlib:System.String>>|I||"] = "1EDDB40045FABDA60512FE9E98AE1C5A70E89BEE6DB2BE036C9B499CD29697EA",
    };

    internal static bool Matches(MethodBase method)
    {
        int rewritten = Array.FindIndex(ParsedLanguagePrepatch.Names, name => name == method.Name);
        string? expected;
        if (rewritten >= 0 && method.DeclaringType?.FullName == ParsedLanguagePrepatch.Types[rewritten])
            expected = ParsedLanguagePrepatch.Rewritten[rewritten];
        else if (!Expected.TryGetValue(SemanticMethodIdentity.Signature(method), out expected)) return false;
        return SemanticMethodIdentity.TryHash(method, out string actual, out _) && actual == expected;
    }

    internal static MethodBase[] Methods()
    {
        var result = new List<MethodBase>();
        void Add(Type type, params string[] names)
        {
            foreach (string name in names) result.Add(AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name));
        }
        Add(typeof(LoadedLanguage), "LoadData", "get_AllDirectories", "TryRegisterFileIfNew", "LoadFromFile_Keyed", "LoadFromFile_DefInject", "LoadFromFile_Strings", "EnsureAllDefTypesHaveDefInjectionPackage");
        Add(typeof(LanguageDatabase), "Clear");
        result.Add(AccessTools.Constructor(typeof(DefInjectionPackage), new[] { typeof(Type) }));
        result.Add(AccessTools.Constructor(typeof(DefInjectionPackage.DefInjection), Type.EmptyTypes));
        result.Add(AccessTools.Constructor(typeof(LoadedLanguage.KeyedReplacement), Type.EmptyTypes));
        Add(typeof(DefInjectionPackage), "AddDataFromFile", "ProcessedPath", "ProcessedTranslation", "TryAddInjection", "TryAddFullListInjection", "BackCompatibleKey", "CheckErrors");
        Add(typeof(DefInjectionPackage.DefInjection), "get_IsFullListInjection");
        result.Add(AccessTools.Method(typeof(DirectXmlLoaderSimple), "ValuesFromXmlFile", new[] { typeof(string) }));
        result.Add(AccessTools.Method(typeof(DirectXmlLoaderSimple), "ValuesFromXmlFile", new[] { typeof(XDocument) }));
        result.Add(AccessTools.Method(typeof(VirtualFileInfoExt), "LoadAsXDocument", new[] { typeof(string) }));
        Add(typeof(DirectXmlSaveLoadUtility), "GetInnerXml");
        Add(typeof(GenText), "LinesFromString");
        var merge = typeof(GenCollection).GetMethods(Declared).Single(m => m.Name == "SetOrAdd"
            && m.IsGenericMethodDefinition && m.GetParameters().Length == 3
            && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Dictionary<,>));
        result.Add(merge);
        result.Add(merge.MakeGenericMethod(typeof(string), typeof(LoadedLanguage.KeyedReplacement)));
        result.Add(merge.MakeGenericMethod(typeof(string), typeof(DefInjectionPackage.DefInjection)));
        Add(FileType, "get_Name", "get_FullPath", "ReadAllText");
        Add(TarFileType, "get_Name", "get_FullPath", "ReadAllText", "CheckAccess");
        Add(typeof(BackCompatibility), "BackCompatibleDefName", "CheckSaveIdenticalToCurrentEnvironment");
        Add(typeof(GenDefDatabase), "GetDefSilentFail");
        Add(typeof(DefDatabase<>), "GetNamed", "GetNamedSilentFail");
        foreach (Type converter in ConverterTypes) Add(converter, "BackCompatibleDefName");

        // Iterator factories hide their actual work in MoveNext; LINQ/deferred
        // callbacks likewise have distinct patchable bodies. Only helpers of
        // the operations above are included, not unrelated language UI work.
        AddGenerated(result, typeof(LoadedLanguage), "get_AllDirectories", "LoadData", "LoadFromFile_DefInject", "EnsureAllDefTypesHaveDefInjectionPackage");
        AddGenerated(result, typeof(DefInjectionPackage), "CheckErrors");
        AddGenerated(result, typeof(DirectXmlLoaderSimple), "ValuesFromXmlFile");
        AddGenerated(result, typeof(GenText), "LinesFromString");
        result.Add(AccessTools.Constructor(typeof(DefInjectionPackage), new[] { typeof(Type) }));
        return result.Distinct().OrderBy(SemanticMethodIdentity.Signature, StringComparer.Ordinal).ToArray();
    }

    // These helpers are consumed only by DefInjectionPackage.BackCompatibleKey.
    // Their callbacks must run in native injection parsing, but do not govern
    // the independent keyed or Strings parsers in the surrounding LoadData.
    internal static bool IsInjectionNameContract(MethodBase method)
    {
        Type? type = method.DeclaringType;
        return type == typeof(BackCompatibility) || type == typeof(GenDefDatabase)
            || type == typeof(DefDatabase<>)
            || type != null && typeof(BackCompatibilityConverter).IsAssignableFrom(type);
    }

    private static void AddGenerated(List<MethodBase> result, Type owner, params string[] operations)
    {
        foreach (Type nested in owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            bool iterator = operations.Any(n => nested.Name.StartsWith("<" + n + ">d__", StringComparison.Ordinal));
            foreach (MethodInfo method in nested.GetMethods(Declared))
                if (iterator || operations.Any(n => method.Name.StartsWith("<" + n + ">b__", StringComparison.Ordinal))) result.Add(method);
            if (iterator) result.AddRange(nested.GetConstructors(Declared));
        }
    }

    // Tar entries keep native archive discovery and decoding. The caller hashes
    // the text actually consumed, including the native archive cache's contents.
    internal static bool FileSupported(VirtualFile file) => file != null && (file.GetType() == FileType || file.GetType() == TarFileType);

    internal static void ValidateDefLookup(Type defType) => CreateDefLookupValidator(defType)();

    internal static Action CreateDefLookupValidator(Type defType, ParsedLanguageConsumers? consumers = null)
    {
        if (!typeof(Def).IsAssignableFrom(defType) || defType.ContainsGenericParameters) throw new InvalidDataException("language-def-type");
        if (consumers != null && defType.GetType() != typeof(ThingDef).GetType()) throw new InvalidDataException("language-consumer-def-type");
        Type database = typeof(DefDatabase<>).MakeGenericType(defType);
        MethodInfo lookup = AccessTools.Method(database, "GetNamedSilentFail");
        var guards = new List<PublishedPatchGuard>();
        foreach (string name in new[] { "GetNamedSilentFail", "GetNamed" })
        {
            if (!PublishedPatchGuard.TryCreate(AccessTools.Method(database, name), ParsedLanguageRuntime.Owner, out var guard, true))
                throw new InvalidDataException("language-def-lookup-hook");
            guards.Add(guard!);
        }
        var chainField = AccessTools.Field(typeof(BackCompatibility), "conversionChain");
        var syncField = AccessTools.Field(typeof(GenDefDatabase), "cachedGetNamedSilentFailLock");
        var delegatesField = AccessTools.Field(typeof(GenDefDatabase), "cachedGetNamedSilentFail");
        // Reuse only reflection metadata and guards for this load. Each call
        // still checks current publications, converters and cached delegates.
        return () =>
        {
            if (Scribe.mode != LoadSaveMode.Inactive) throw new InvalidDataException("language-scribe-active");
            if (consumers == null)
            {
                var chain = chainField.GetValue(null) as IList;
                if (chain == null || chain.Count != ConverterTypes.Length) throw new InvalidDataException("language-converter-chain");
                for (int i = 0; i < chain.Count; i++)
                    if (chain[i]?.GetType() != ConverterTypes[i]) throw new InvalidDataException("language-converter-chain");
            }
            else
            {
                consumers.Validate();
                // A custom name comparer is another arbitrary callback in the
                // consumer. It cannot be allowed to change later row parsing.
                var names = AccessTools.Field(database, "defsByName").GetValue(null) as IDictionary;
                if (names == null || !ReferenceEquals(names.GetType().GetProperty("Comparer")!.GetValue(names, null), EqualityComparer<string>.Default))
                    throw new InvalidDataException("language-def-name-comparer");
            }
            foreach (var guard in guards)
                if (!guard.AllowsOriginalContract()) throw new InvalidDataException("language-def-lookup-hook");
            object sync = syncField.GetValue(null);
            lock (sync)
            {
                var delegates = (IDictionary)delegatesField.GetValue(null);
                if (delegates.Contains(defType))
                {
                    var cached = delegates[defType] as Delegate;
                    if (cached == null || cached.Target != null || cached.GetInvocationList().Length != 1 || cached.Method != lookup)
                        throw new InvalidDataException("language-def-lookup-delegate");
                }
            }
        };
    }

    internal static void WriteDefContext(BinaryWriter writer, Type defType)
    {
        ValidateDefLookup(defType);
        foreach (Type converter in ConverterTypes) writer.Write(converter.FullName!);
        Type database = typeof(DefDatabase<>).MakeGenericType(defType);
        // Bind the actual dictionary used by GetNamed, rather than AllDefs:
        // implied definitions, aliases and same-session edits can change it.
        var names = (IDictionary)AccessTools.Field(database, "defsByName").GetValue(null);
        if (!ReferenceEquals(names.GetType().GetProperty("Comparer")!.GetValue(names, null), EqualityComparer<string>.Default))
            throw new InvalidDataException("language-def-name-comparer");
        if (names.Count > 1000000) throw new InvalidDataException("language-def-count");
        writer.Write(names.Count);
        foreach (string name in names.Keys.Cast<string>().OrderBy(n => n, StringComparer.Ordinal))
        { writer.Write(name); writer.Write(names[name] != null); }
    }

    internal static void ValidateInjection(DefInjectionPackage package)
    {
        if (package.defType != typeof(ThingDef)) return;
        foreach (var record in package.injections.Values)
        {
            string original = (record.nonBackCompatiblePath ?? record.path ?? "").Split('.')[0];
            if ((original == "Neurotrainer" || original == "MechSerumNeurotrainer")
                && GenDefDatabase.GetDefSilentFail(package.defType, original, false) == null)
                throw new InvalidDataException("language-random-legacy-neurotrainer");
        }
    }
}
