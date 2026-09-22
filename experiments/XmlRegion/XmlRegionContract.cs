// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Hash effective native instructions once at startup; afterward observe Harmony's
// pinned immutable publication records. No game-wide assembly hash is required.
internal sealed class XmlRegionContract
{
    internal const string Owner = "wakeup.xml-region";
    private const string NoAutomaticOwner = "\0wakeup.xml-region.contract";
    // Archival bridge now references WakeUp.Tests. The measured product bridge
    // fingerprint was 9B48C4BDD09FD481E250F019D7D69D3F33F36AF6F3444C599891BA0EB5AD74C1.
    internal const string EffectiveApplyBody = "8B2F44CB47492AB989115583F5E3816554B6C200EB40ADD2CAAAA3A2FE1C055A";
    private readonly PublishedPatchGuard[] nativeGuards;
    private readonly Dictionary<MethodBase, byte[]> publications;
    private readonly Dictionary<MethodBase, PublishedPatchGuard> libraryGuards = new();

    // Current Steam original bodies and the single mechanically rewritten Apply
    // callsite were independently hashed offline; no values are learned at use.
    internal static readonly string[] Bodies = {
        EffectiveApplyBody,
        "0530440A3A28CBBEC2D1648252CD3B0795816B6758117B1D9207D92E347D9F69",
        "DEF5C0CD7055D6D01204C19BF8BFA35933769CBEEE56DE788B11A0ED933A9F21",
        "C03AF4FB239092157EC3EE0F2F67CD65054C9F2CEE55765C6D9676374FA28CED",
        "02D4FDCE8898CFB1D9612919F8716FF8F2BA86968875E772F5B64277BFBBFAFA",
        "E0BEB8E3C60B4F2310A2C4890CCF8F31DC537D0BFB81D93D34047EA252AB2005",
        "328415057FF8E5E896F94E7494BA9E04CBAF399DCF42757C04340D9B29285074",
        "ED66066D76024F959F8F050241C0F63237BC8B85CDC8A6905EB8C3A9618E7F0C",
        "69A389BC972D545C9FA37DFF482447BADAA0F02172E4C3DC160E8799E74885E4",
        "C357E641AC0859997B452017E85F67EC60670111C62473953F82B0045D71CA17",
    };

    // Exact reviewed game System.Xml intern/import helpers. Offline desktop XML
    // libraries are deliberately not learned as production admission values.
    internal static readonly string[] StagingBodies = {
        "524BDB29E35EC4D0F894DFF7EFBEE9602D337A611DF8B0E58409014D9097A98E", // System.Xml.XmlDocument.GetXmlName
        "2D5E590957A9CA5E7F5AD5309E47FEC7E40C725F43D0266140DBE9FF33CDDD15", // System.Xml.XmlDocument.AddXmlName
        "E3FA48509BAE31EAA59E2A5DEB4BB1ED19694ADE886A818670FB34ED9F85C6FF", // System.Xml.DomNameTable.GetName
        "444D11C91153E74F2E0B9D64436CA744002ADE721A12471B019A85A9FD0EFE95", // System.Xml.DomNameTable.AddName
        "EE5DD06A6E9D81842FDE7C574DB30559C8E3D75939CD654A31176AD99036534C", // System.Xml.XmlName.Equals
        "41117ADF698F6714A9AF6A279FA869CC51E10D358C6E573B98AF2C0AECFF0649", // System.Xml.NameTable.Get
        "52C2B993FD2A975140D0AE29B6A26F5BC4BA61BCCF5CF1F115CE5DAC67C530FA", // System.Xml.NameTable.Add
        "BCB838F0F031879D9232C6670333A2AE42745BC7B88E00A6C3FE35058541FAAA", // System.Xml.XmlDocument.ImportNodeInternal
        "012FD82EB0A5CF92460403CE5AF61F1E8ABA20DE1C1A93AB2B03FB8A84799E67", // System.Xml.XmlDocument.ImportAttributes
        "C55FC20A6788A1D221F6A3BAD66257B0D276F7EE68A841BB7A5F03ACF97CA0B5", // System.Xml.XmlDocument.ImportChildren
        "F0096F1F46ACC06B8FF4D66C597C68EBF213DA06BA8A38E86205C5F1FCEDC0D7", // System.Xml.XmlDocument.CreateElement
        "7F8323A9BEEBEFD75B5981E525C3669890AAB0D85670B5E72EBED347F58B83B5", // System.Xml.XmlDocument.CreateAttribute
        "E1833B22A055D140721BF2C40D96B983B0D6C4F3B3FC383E37DEB7118C186A79", // System.Xml.XmlDocument.CreateTextNode
    };

    // The finite ordinary mutation and imported-node constructor paths reviewed
    // against the same game XML library. Attribute insertion follows SetNamedItem,
    // InternalAppendAttribute and AddNode; ID registration must remain absent.
    internal static readonly string[] MutationBodies = {
        "5FC6A180B1B279D2ED5B2CD7E4AD154E9938DDDFDC6B145FEEA8EEF63F4C424B", // System.Xml.XmlNode.InsertBefore
        "F948AAB98CCBFF5E090EEB6D1A15752D815C5FDC0C577D26B37E66B893DA0DBA", // System.Xml.XmlNode.AppendChild
        "348A9F461B4DF9954D782984993BE133F58BABE1C79F5B5E11A470725EEF6B80", // System.Xml.XmlNode.PrependChild
        "8A24AFD65CF8C774D358B6E3D9910F8FE23A51A08682D552B4308A8F73049F90", // System.Xml.XmlNode.RemoveChild
        "B2177A20E211C4A034188E9009685B25D115C870EBF7AAB1F1F9BF3FFCC762D6", // System.Xml.XmlNode.GetEventArgs
        "5D08A349AC2FAD6E0D2F324E44C9DB170381AD4BD0AA3300A02EC48F3CF2A7BE", // System.Xml.XmlElement.SetParent
        "ED6F55E465FA2F04F6B536C3D9FDF1E50623CF7EA512E75F9C6443D931C316F1", // System.Xml.XmlElement.get_LastNode
        "48D0B2986B5D6629DF8FA042BF44AC76BF11481749F9925854B4914D3EB43551", // System.Xml.XmlElement.set_LastNode
        "8200C0AB44B27B17269B062BBD9FCA3302C9E1C769815E1268EB4895AC429778", // System.Xml.XmlElement.get_IsEmpty
        "C777B0868BF6CEFCC029DD8DAD41427F9BD1631B2456AC54804C65EEF603F08B", // System.Xml.XmlElement.set_IsEmpty
        "C353317E8C90B64BB49F95DB8B1F1C38C2B63CD281895927A00C211369CF2099", // System.Xml.XmlElement.get_OwnerDocument
        "0D18895723E9474D6DD221BB219F9F0579E362E79DA886B0E5AC01F91927F301", // System.Xml.XmlDocument.GetEventArgs
        "263F421C3F8549274F579A4566F933BA151CF849E27F02507C3EE26B888B9396", // System.Xml.XmlDocument.ImportNode
        "8C4EA96A7620E661AABB6635DFFAC8A7678E98FCECCA53494FAB2E7602C01F8B", // System.Xml.XmlDocument.AddAttrXmlName
        "AB8F21887068E0BDD6DB1C51D98DFEC3F6927AA525BD65015E92CCD2F4811A8D", // System.Xml.XmlDocument.AddDefaultAttributes
        "479285E5D1688F9D970DC5D730036EE8BDF17D56DB18E2B12D1BAB7EF2A47D9E", // System.Xml.XmlAttribute.get_OwnerDocument
        "BC3469083D41AA652AD3A65E694BD82AF1D291046DF2C7A198A3647D886A2426", // System.Xml.XmlAttributeCollection.SetNamedItem
        "ED23E6BBCA01197069CB69FDF3B734D22ADEC8C17EF6904FA7C1350D9C692030", // System.Xml.XmlNode..ctor
        "0A7856D7A6775949FD1CA8D78D0473A6EBAA2373AFAD2496A74CE84DE032F02E", // System.Xml.XmlLinkedNode..ctor
        "55CF17DA40FDFA728B7601321B83D09AF421EB6A3B5B465095317C5A99E4871C", // System.Xml.XmlElement..ctor
        "B74182485A9FC38F42E8DFE350250A69FD72830286DC1BADB9D5773991F4D6D3", // System.Xml.XmlAttribute..ctor
        "A67E197BC135C585C3A6B8ED16A9ED2052CA211BAB7CAE3BFB3C479C20DCE2B0", // System.Xml.XmlCharacterData..ctor
        "770C645A3046ABBC2CDD2B67A7EDF254F6F371100B274C179948B71DDACB225D", // System.Xml.XmlText..ctor
        "5B16D6E562F0C79041635F4B63883299BF9CDA4B7DAC8E13BADCD33C12342AF8", // System.Xml.XmlNode.SetParent
        "7EF4BF4675E1EBD733ABA452717D39731EAD0924C2649E3C6EB7EF2925F07416", // System.Xml.XmlNamedNodeMap.FindNodeOffset
        "3B7A92F8E7DFBF2089FBE129A957319AB7798B148FFBA59ACBDE79095B42FA82", // System.Xml.XmlAttributeCollection.InternalAppendAttribute
        "1DC458BDD14BE8D37052508D12EBFDF49578CC972A303FAE8F8C262CC7248976", // System.Xml.XmlNamedNodeMap.AddNode
        "F4DF77ACF530D187678E2DFC6971DFC71345DEA0BACD37E75AC5EA0C9904CCDC", // System.Xml.XmlAttributeCollection.InsertParentIntoElementIdAttrMap
        "3C558DA454F8E234128991827AF6C30106359D41FC23313EF13B613C1692F08D", // System.Xml.XmlDocument.GetIDInfoByElement
    };

    internal static MethodBase[] MutationMethods()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var methods = new List<MethodBase>();
        Type NativeType(string name) => typeof(XmlDocument).Assembly.GetType("System.Xml." + name, true)!;
        void Add(string type, string name, Type[]? parameters = null)
        {
            var found = NativeType(type).GetMethods(flags).Where(m => m.Name == name);
            if (parameters != null) found = found.Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
            methods.Add(found.Single());
        }
        void Constructor(string type, params Type[] parameters) => methods.Add(NativeType(type).GetConstructor(flags, null, parameters, null)
            ?? throw new InvalidOperationException("region-xml-constructor-" + type));
        foreach (string name in new[] { "InsertBefore", "AppendChild", "PrependChild", "RemoveChild", "GetEventArgs" }) Add("XmlNode", name);
        foreach (string name in new[] { "SetParent", "get_LastNode", "set_LastNode", "get_IsEmpty", "set_IsEmpty", "get_OwnerDocument" }) Add("XmlElement", name);
        foreach (string name in new[] { "GetEventArgs", "ImportNode", "AddAttrXmlName", "AddDefaultAttributes" }) Add("XmlDocument", name);
        Add("XmlAttribute", "get_OwnerDocument"); Add("XmlAttributeCollection", "SetNamedItem");
        Constructor("XmlNode", typeof(XmlDocument)); Constructor("XmlLinkedNode", typeof(XmlDocument));
        Constructor("XmlElement", NativeType("XmlName"), typeof(bool), typeof(XmlDocument));
        Constructor("XmlAttribute", NativeType("XmlName"), typeof(XmlDocument));
        Constructor("XmlCharacterData", typeof(string), typeof(XmlDocument)); Constructor("XmlText", typeof(string), typeof(XmlDocument));
        Add("XmlNode", "SetParent"); Add("XmlNamedNodeMap", "FindNodeOffset", new[] { typeof(string), typeof(string) });
        Add("XmlAttributeCollection", "InternalAppendAttribute"); Add("XmlNamedNodeMap", "AddNode");
        Add("XmlAttributeCollection", "InsertParentIntoElementIdAttrMap"); Add("XmlDocument", "GetIDInfoByElement");
        return methods.ToArray();
    }

    private XmlRegionContract(PublishedPatchGuard[] guards, Dictionary<MethodBase, byte[]> publications)
    { nativeGuards = guards; this.publications = publications; }

    internal static MethodBase[] Methods()
    {
        var result = new List<MethodBase>();
        void Add(Type type, string name) => result.Add(AccessTools.Method(type, name)
            ?? throw new InvalidOperationException("region-contract-method-" + name));
        Add(typeof(LoadedModManager), "ApplyPatches");
        Add(typeof(PatchOperation), "Apply");
        foreach (Type type in new[] { typeof(PatchOperationAdd), typeof(PatchOperationAddModExtension),
            typeof(PatchOperationRemove), typeof(PatchOperationReplace), typeof(PatchOperationConditional) }) Add(type, "ApplyWorker");
        Add(typeof(ModContentPack), "get_Patches");
        Add(typeof(LoadedModManager), "get_RunningModsListForReading");
        result.AddRange(typeof(LoadedModManager).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            .Where(m => m.Name.StartsWith("<ApplyPatches>", StringComparison.Ordinal)).OrderBy(m => m.Name));
        return result.ToArray();
    }

    internal static bool TryCreate(out XmlRegionContract? contract, out string reason)
    {
        contract = null; reason = "region-effective-contract";
        try
        {
            // This checks supported platform and the exact Harmony implementation
            // relied on below, while admitting newer games by per-method contract.
            if (!RuntimeIdentity.ValidateBinaryIdentity(out reason)) return false;
            MethodBase[] methods = Methods();
            if (methods.Length != Bodies.Length) return false;
            for (int i = 0; i < methods.Length; i++)
                if (!SemanticMethodIdentity.TryHash(methods[i], out string hash, out reason) || hash != Bodies[i])
                { reason = "changed-region-method-" + methods[i].Name; return false; }
            MethodInfo[] staging = RetainedXmlStaging.RelevantMethods();
            if (staging.Length != StagingBodies.Length) return false;
            for (int i = 0; i < staging.Length; i++)
                if (staging[i] == null || !SemanticMethodIdentity.TryHash(staging[i], out string hash, out reason) || hash != StagingBodies[i])
                { reason = "changed-region-xml-method-" + staging[i]?.Name; return false; }
            MethodBase[] mutations = MutationMethods();
            if (mutations.Length != MutationBodies.Length) return false;
            for (int i = 0; i < mutations.Length; i++)
                if (!SemanticMethodIdentity.TryHash(mutations[i], out string hash, out reason) || hash != MutationBodies[i])
                { reason = "changed-region-xml-mutation-" + mutations[i].Name; return false; }
            contract = CreateGuards(methods.Concat(staging).Concat(mutations).ToArray());
            if (!contract.Allows(out reason)) { contract = null; return false; }
            reason = "admitted-native-region"; return true;
        }
        catch (Exception exception) { contract = null; reason = "region-contract-" + exception.GetType().Name; return false; }
    }

    // Internal seam for tests after separately checking actual rewritten bodies.
    internal static XmlRegionContract CreateGuards(MethodBase[] methods)
    {
        var guards = new List<PublishedPatchGuard>();
        foreach (var method in methods)
        {
            // Deliberately do not automatically trust our owner string: exact
            // method, declaring type/module and prefix/finalizer category matter.
            if (!PublishedPatchGuard.TryCreate(method, NoAutomaticOwner, out var guard, allPatchKinds: true,
                allowedForeignPatch: _ => AllowsPatches(method))) throw new InvalidOperationException("region-published-state");
            guards.Add(guard!);
        }
        Type shared = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")
            ?? throw new InvalidOperationException("region-harmony-state");
        FieldInfo? field = shared.GetField("state", BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null || !field.IsInitOnly || field.FieldType != typeof(Dictionary<MethodBase, byte[]>)
            || field.GetValue(null) is not Dictionary<MethodBase, byte[]> publications)
            throw new InvalidOperationException("region-harmony-state-layout");
        return new XmlRegionContract(guards.ToArray(), publications);
    }

    internal bool Allows(out string reason)
    {
        reason = "foreign-region-hook";
        try
        {
            foreach (var guard in nativeGuards) if (!guard.AllowsOriginalContract()) return false;
            // Inspect current published method keys under Harmony's own lock so
            // newly installed XML hooks cannot hide behind startup-only checks.
            // No method hashing or PatchInfo deserialization occurs for unchanged
            // publications. GetPatchInfo runs only outside this lock.
            lock (publications)
                foreach (var method in publications.Keys)
                    if (method.DeclaringType?.Assembly == typeof(XmlDocument).Assembly && !libraryGuards.ContainsKey(method))
                    {
                        if (libraryGuards.Count >= 128 || !PublishedPatchGuard.TryCreate(method, NoAutomaticOwner, out var guard, allPatchKinds: true))
                        { reason = "region-xml-hook-inventory"; return false; }
                        libraryGuards.Add(method, guard!);
                    }
            foreach (var guard in libraryGuards.Values)
                if (!guard.AllowsOriginalContract()) { reason = "foreign-system-xml-hook"; return false; }
            reason = "admitted-native-region"; return true;
        }
        catch (Exception exception) { reason = "region-published-" + exception.GetType().Name; return false; }
    }

    internal int PatchInfoReads => nativeGuards.Sum(g => g.PatchInfoReads) + libraryGuards.Values.Sum(g => g.PatchInfoReads);

    internal static bool AllowsPatches(MethodBase target)
    {
        Patches? patches = Harmony.GetPatchInfo(target);
        return patches == null || patches.Prefixes.All(p => Allowed(target, p, "prefix"))
            && patches.Postfixes.All(p => Allowed(target, p, "postfix"))
            && patches.Transpilers.All(p => Allowed(target, p, "transpiler"))
            && patches.Finalizers.All(p => Allowed(target, p, "finalizer"))
            && patches.InnerPrefixes.All(p => Allowed(target, p, "inner-prefix"))
            && patches.InnerPostfixes.All(p => Allowed(target, p, "inner-postfix"));
    }

    private static bool Allowed(MethodBase target, Patch patch, string category)
    {
        MethodInfo callback = patch.PatchMethod;
        if (callback.Module != typeof(XmlRegionContract).Module && callback.Module != typeof(DefLookupRuntime).Module) return false;
        bool stage = target.DeclaringType == typeof(LoadedModManager) && target.Name == "ApplyPatches";
        if (patch.owner == "wakeup.def-lookup" && callback.DeclaringType == typeof(DefLookupRuntime))
        {
            if (stage) return category == "prefix" && callback == AccessTools.Method(typeof(DefLookupRuntime), "StagePrefix")
                || category == "finalizer" && callback == AccessTools.Method(typeof(DefLookupRuntime), "StageFinalizer");
            return target.Name == "ApplyWorker" && category == "transpiler"
                && callback == AccessTools.Method(typeof(DefLookupRuntime), "Transpiler");
        }
        Type? runtime = typeof(XmlRegionContract).Assembly.GetType("WakeUp.XmlRegionRuntime");
        return stage && patch.owner == Owner && runtime != null && callback.DeclaringType == runtime
            && (category == "prefix" && callback == AccessTools.Method(runtime, "StagePrefix")
                || category == "finalizer" && callback == AccessTools.Method(runtime, "StageFinalizer"));
    }
}
