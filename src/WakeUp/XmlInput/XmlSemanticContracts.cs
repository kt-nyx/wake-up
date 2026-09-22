// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace WakeUp;
// Exact method-level contracts from inspected GOG and optional supplier methods.
internal static class XmlSemanticContracts
{
    internal static string Key(MethodBase method) => TypeName(method.DeclaringType!) + "." + method.Name + "(" + string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType))) + ")";
    private static string TypeName(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition().FullName + "[" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + "]" : type.FullName!;
    internal static readonly Dictionary<string, string> Adapters = new(StringComparer.Ordinal)
    {
        ["Verse.DefDatabase`1[RimWorld.ExpansionDef].get_AllDefsListForReading()"] = "F0E0AF6E357E871B03AF8667BC54EB801F934D6C6BDF4AD34BA6B5F558CE0070",
        ["CombatExtended.PatchOperation_ConditionalGeneric.ApplyWorker(System.Xml.XmlDocument)"] = "1156E1EF580F504FEF35A57C950B3D702AAA9F0265BF0E0D51E5FA5378A53E17",
        ["CombatExtended.PatchOperation_ConditionalGeneric.Complete(System.String)"] = "ACB898AA91F80542FF6E2A26BFC47569E8F8BCA5331CE257DEA61A7883C161EC",
        ["CombatExtended.PatchOperationFindMod.<ApplyWorker>b__1_0(Verse.ModMetaData)"] = "5A7F7C57966FEBE2791C17815E069B4D477494DE7E452D434AEC0916955165CC",
        ["CombatExtended.PatchOperationFindMod.ApplyWorker(System.Xml.XmlDocument)"] = "4ED4838C7D39E4A37B0A27B4C40B4F8BCAEF00B1BA54E52F744324519A967635",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceAttachmentLinks(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "8684F313972937C914CB121539C96A48EF532458A873496CFD4757EFEF71A5DB",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceCompsCE(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "A384DF8062C6AC153C261CA1961914587A4DECB95F1CCD5DD86F5802270FBB65",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceCostList(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "0C09B7C3135B06150B9EABF609A6E7A013CD3172360DC1004EDE913A7168FBAE",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceDefaultGraphicParts(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "46B4CF482A5535F7BB180A9B64CAE6705D57C8195A3EA566F100278DC584D5CF",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceResearchPrereq(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "D55C03634312870CD8F54B7183077A1C6909FEC45185E34E7F86A90473AF6FC4",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceStatBases(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "FC88B3344B8C15CEB4ABD92F8C86A90E20C8B7A7F8B56619CA8339AA6E193521",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceTexPath(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "FB78A5E2727CE9D963E8E5548E607A7CEBCF38695857005191BCAD487BF9799E",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceVerbPropertiesCE(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "5F0616332F6FD2F77C09CB448FDBFC9A8224386BC356BAD638C43856EBDA5304",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceWeaponClasses(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "D04F623502054E1C05EA9063E3664D5B72864EF882A230F6F5F89971CBA3CED2",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddOrReplaceWeaponTags(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "46D3C8A306762248D4233F1C7BB83CDD4732B1979E6A1D3434D899340B885A40",
        ["CombatExtended.PatchOperationMakeGunCECompatible.AddRunAndGunExtension(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "F5864394178AF5F3A9E18573BAF7CB2D698144E5418726A2EBF3D18BD36C4B54",
        ["CombatExtended.PatchOperationMakeGunCECompatible.ApplyWorker(System.Xml.XmlDocument)"] = "1CA2F51A7BA607ECCD0E8E91E94DF980BB56EFB6402997B7B2F28471BC092776",
        ["CombatExtended.PatchOperationMakeGunCECompatible.CreateListElementAndPopulate(System.Xml.XmlDocument,System.Xml.XmlNode,System.String)"] = "595342B89BA697E4D864CB259E767B4EEC00DAEB9D2C4243533F6216B6FFE5ED",
        ["CombatExtended.PatchOperationMakeGunCECompatible.GetOrCreateNode(System.Xml.XmlDocument,System.Xml.XmlNode,System.String,System.Xml.XmlElement&)"] = "EDC22D7A45BFB7173EB11A7677E0CFEB19041CE6D7A9F9EC9BAC0E144D97BE4B",
        ["CombatExtended.PatchOperationMakeGunCECompatible.MakeWeaponPlatform(System.Xml.XmlDocument,System.Xml.XmlNode)"] = "53247126CBAD409B367269B995F7B8C4729A979586008408BF0AA9928563D4DE",
        ["CombatExtended.PatchOperationMakeGunCECompatible.Populate(System.Xml.XmlDocument,System.Xml.XmlNode,System.Xml.XmlElement&,System.Boolean)"] = "1F333228705DFC69FE79DD069B4120EE094E9DE739DBC3A697CD0F671EA7764D",
        ["CombatExtended.PatchOperationSettingsConditional.ApplyWorker(System.Xml.XmlDocument)"] = "508620D4C2177762794097F068B94F365382ECFADCC034C6611AEF1439A40393",
        ["CombatExtended.Settings.get_GenericAmmo()"] = "1B3115A986E1E9A94A174FC3D2930CDD6208246E8254CE8550DB7D293741FD42",
        ["HarmonyLib.AccessTools.Field(System.Type,System.String)"] = "3F7F5AB6A49A768890BB815EA87D399764B9E3EFD6D1A2F25DE10F6331F207D0",
        ["VEF.PatchOperationToggableSequence.ApplyPatches(System.Xml.XmlDocument)"] = "C25E3C20A91C65B163A5D7D2FF37356A329D29B21AE33CBF8CC38B5D47260C7D",
        ["VEF.PatchOperationToggableSequence.ApplyWorker(System.Xml.XmlDocument)"] = "F0A9F438CB4C058123EE106940AA9ADD5BC54AE6DB844633B011D652429E1027",
        ["VEF.PatchOperationToggableSequence.Complete(System.String)"] = "C33F052B6FC97285BA61E4659A70452EB502214EB97BB2E69C2E03979AC9C66D",
        ["VEF.PatchOperationToggableSequence.ModsFound()"] = "317F17318D0EDACD427F6275100D34F09E3E8AD40B91E61C434368384C1D16F4",
        ["VEF.PatchOperationToggableSequence.ToString()"] = "1EDBE59896F5BE49A56BDBB20C0A846D104639EB2ECF631241270630013BF4B3",
        ["Verse.ModLister.EnsureInit()"] = "C3FC1B23E61F7E94CC6C54C0047FC5D60F3A8F117E6DA101093C1AAAE971CBC7",
        ["Verse.ModLister.get_AllExpansions()"] = "2E3656D804CFF5105B8BF50281666B68B7D93867C9CC17F239F274C9775D8C39",
        ["Verse.ModLister.GetExpansionWithIdentifier(System.String)"] = "566CD74E20BA6CCEAF8B13A7A3A5E10988B43162AE629807BE0B88E64E1FF9FD",
        ["Verse.ModLister.HasActiveModWithName(System.String)"] = "750CD0AF8F4693ACABA7AD1796BA97F17F65A1BB63904B475D5CAAA4AD49D05B",
        ["Verse.ModMetaData.get_Active()"] = "E8AEE2A1481755634646EAE1BD1C2229EF0195E89A4AFE0727A26FC3F1F08965",
        ["Verse.ModMetaData.get_Expansion()"] = "2E88DD8DE890DDFC8E5857C20F338FB1BFC0E75D5479BAEF293D5FABF4FCB7C6",
        ["Verse.ModMetaData.get_Name()"] = "013A19CE90DAC66EE56A81D8F6C784679B6B26DF44CE525178D463E85117ADAA",
        ["Verse.ModMetaData.get_PackageId()"] = "777969DC0D78D4F125C0054EA628289BE214EB457CACC18F74D9168D36A3C8D1",
        ["Verse.ModsConfig.get_ActiveModsInLoadOrder()"] = "525681A80E18D718BDCAD9B19167308DCD65CA4DF5B03367F5B9379D2D0E0F26",
        ["Verse.ModsConfig.IsActive(System.String)"] = "E6542C36E32181DB466411798B46DCB15B76F7192A6D93445ECAD3AC1960EAFD",
        ["Verse.ModsConfig.IsActive(Verse.ModMetaData)"] = "C8F6EDDC607A0AFF3E8B43CCE8E1CD9E400E3ABEA4950C2C683B8152167C3106",
        ["Verse.PatchOperationFindMod.ApplyWorker(System.Xml.XmlDocument)"] = "A34D5D808D6F3EEA99A6C06CBFAE6482FD01F37C44C22FFA0F31C6B18D380E21",
        ["Verse.PatchOperationFindMod.ToString()"] = "4868A395A7B1834677F35ED60708B0EB6386804AF34DBF40F69229FB2A6F340B",
    };
    internal static readonly Dictionary<string, string> Inheritance = new(StringComparer.Ordinal)
    {
        ["Verse.LoadedModManager.ParseAndProcessXML(System.Xml.XmlDocument,System.Collections.Generic.Dictionary`2[System.Xml.XmlNode,Verse.LoadableXmlAsset],System.Boolean)"] = "C4189C774CF4CFE4CA80D8D6CBA09DC7C1A6FACAA8E56BD7B109541EC64AD113",
        ["Verse.ModLister.AllModsActiveNoSuffix(System.Collections.Generic.IEnumerable`1[System.String])"] = "8DA8A31830C6A6D49CF4ABE2BC3AB5658D3F59AC5ACBC40A2E71795C1DC9ABD7",
        ["Verse.ModLister.AllModsActiveNoSuffix(System.Collections.Generic.List`1[System.String])"] = "2FC3CBE39DB4A891F856651721A0B79A422DC3A5326CFE7FBC4DB128C88045ED",
        ["Verse.ModLister.GetActiveModWithIdentifier(System.String,System.Boolean)"] = "9137EE9C01218AEED05500847E0035FF3D32006A45649F2E6184B40F0E3E5FAA",
        ["Verse.XmlInheritance.CheckForDuplicateNodes(System.Xml.XmlNode,System.Xml.XmlNode)"] = "8E5AB686981596BE1AB33D9328F5BE65F9BCB1215D998BEEF838D0E414642836",
        ["Verse.XmlInheritance.Clear()"] = "8F292B2C00A1358010C3BA72EF18D8B1657BAC24BAA5B49FF5391A4A254BD474",
        ["Verse.XmlInheritance.GetBestParentFor(Verse.XmlInheritance+XmlInheritanceNode,System.String)"] = "BD7381D7BD25BC0D2C00F012B6C09C2D0F867729628910927D0F182560F357AD",
        ["Verse.XmlInheritance.GetResolvedNodeFor(System.Xml.XmlNode)"] = "D89132F480BFBBDFADC8EFCB69A49C8D8528C6F5BDFD95A010699D0A02EBB123",
        ["Verse.XmlInheritance.IsListElement(System.Xml.XmlNode)"] = "DC99790C8BADA3F8D21FD2DF1F6AC222F050F9B44E6A04B93C8C4F425A970A83",
        ["Verse.XmlInheritance.RecursiveNodeCopyOverwriteElements(System.Xml.XmlNode,System.Xml.XmlNode)"] = "6772FBB02C1D1D1B7938CE0D5882AC50C6A32343F5384662BB9B8C89DDEEBAB6",
        ["Verse.XmlInheritance.Resolve()"] = "2FD544F0FE03CF8E8839D8DF36FC4AE44BEDA22F2F192B1655466210975A783D",
        ["Verse.XmlInheritance.ResolveParentsAndChildNodesLinks()"] = "44FE6B059092188EFD46A1C001457F752FA223BB189DA6DBEE3F4730BBE3C814",
        ["Verse.XmlInheritance.ResolveXmlNodeFor(Verse.XmlInheritance+XmlInheritanceNode)"] = "736560FD257957EB306800D9D6BBAAD62F8B2D8177F7FD5472F22D1C02228183",
        ["Verse.XmlInheritance.ResolveXmlNodes()"] = "93C9CD0814F58FF1BA02B8652317F1CC481A22B9567D54946602E36CA1573A06",
        ["Verse.XmlInheritance.ResolveXmlNodesRecursively(Verse.XmlInheritance+XmlInheritanceNode)"] = "CC88CA0DC3C00D1B8E563BA914454B10C5F746F0AC2C6D7D2FC9604DF30D664B",
        ["Verse.XmlInheritance.TryRegister(System.Xml.XmlNode,Verse.ModContentPack)"] = "C17908D1F27478908B43125AFEBCB0D9FFF07C39AFE70A0B6543E8A7B27A4AE5",
        ["Verse.XmlInheritance.TryRegisterAllFrom(Verse.LoadableXmlAsset,Verse.ModContentPack)"] = "A413012D45D4251305D3F65FF65BA03E8DA3F1A99E737319E0366A38B15C1860",
        ["Verse.XmlInheritance+<>c__DisplayClass12_0.<GetResolvedNodeFor>b__0(Verse.XmlInheritance+XmlInheritanceNode)"] = "2D14537BB937C75215135C568F58A608007EE7B219AC48C2AEC2210F77B36AEB",
        ["Verse.XmlInheritance+<>c.<ResolveXmlNodes>b__15_0(Verse.XmlInheritance+XmlInheritanceNode)"] = "FDCD5EE49F5882C3A2483C803F4A268285AEC9269AEAD554887B762466EA38B2",
    };
}
