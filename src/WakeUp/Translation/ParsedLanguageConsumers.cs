// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace WakeUp;

// These exact consumers cannot change parsing while an instruction file is
// replayed. Their results are never cached: native insertion calls them itself.
internal sealed class ParsedLanguageConsumers
{
    internal static readonly SupplierMethodContract[] Contracts = {
        new("ce-postfix", "CombatExtended.HarmonyCE.Harmony_BackCompatibility_1_5", "Postfix",
            "M|CombatExtended:CombatExtended.HarmonyCE.Harmony_BackCompatibility_1_5|Postfix|mscorlib:System.Void|S||Assembly-CSharp:Verse.BackCompatibilityConverter_1_5,mscorlib:System.String&,mscorlib:System.Type,mscorlib:System.String",
            "C9A46F7BDAD111268535409E51CCF6C38F1335B59A04166FE6B278BFB98EFB2A"),
        new("ce-string-hash", "<PrivateImplementationDetails>", "ComputeStringHash",
            "M|CombatExtended:<PrivateImplementationDetails>|ComputeStringHash|mscorlib:System.UInt32|S||mscorlib:System.String",
            "6A1D47FB81F3190FBBF95BFB78AE40061CF43D02F8C0BFEBC13F8D8E3A4C522A"),
        new("vef-converter", "VEF.BackwardsCompatibilityMigrationUtility+BackCompatabilityConverter_VEF", "BackCompatibleDefName",
            "M|VEF:VEF.BackwardsCompatibilityMigrationUtility+BackCompatabilityConverter_VEF|BackCompatibleDefName|mscorlib:System.String|I||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode",
            "87231A7F5EA2B6D41EECCAEB729A8B958EEE778C2988981AD9E8CF78DD88C1C4"),
        new("vef-postfix", "VEF.VanillaExpandedFramework_BackCompatibility_BackCompatibleDefName_Patch", "Postfix",
            "M|VEF:VEF.VanillaExpandedFramework_BackCompatibility_BackCompatibleDefName_Patch|Postfix|mscorlib:System.Void|S||mscorlib:System.Type,mscorlib:System.String,mscorlib:System.Boolean,System.Xml:System.Xml.XmlNode,mscorlib:System.String&",
            "06B745D6FAB9930F4D873F483559FB1851F9B8EE11D0F44C9B543D4CA3C2C206")
    };
    private const string GuardOwner = "wakeup.language-consumer-contract";
    private static readonly MethodInfo NameMethod = AccessTools.Method(typeof(BackCompatibility), "BackCompatibleDefName");
    private static readonly MethodInfo SaveCheck = AccessTools.Method(typeof(BackCompatibility), "CheckSaveIdenticalToCurrentEnvironment");
    private static readonly FieldInfo Chain = AccessTools.Field(typeof(BackCompatibility), "conversionChain");
    private readonly List<PublishedPatchGuard> dependencies = new();
    private readonly Dictionary<string, MethodBase> methods = new();
    private bool initialized;
    private Exception? refusal;
    private ConsumerPublication? cePublication, vefPublication;
    private sealed class ConsumerPublication
    {
        private readonly MethodBase target;
        private readonly SupplierMethodContract contract;
        private readonly PublishedPatchGuard guard;
        private object? last;
        private bool read, present;
        internal ConsumerPublication(MethodBase target, string role)
        {
            this.target = target; contract = Contracts.Single(c => c.Role == role);
            if (!PublishedPatchGuard.TryCreate(target, GuardOwner, out var publication, true)) throw new InvalidDataException("language-consumer-publication");
            guard = publication!;
        }
        internal bool Check(Dictionary<string, MethodBase> methods)
        {
            object? stamp = guard.Publication;
            if (read && ReferenceEquals(last, stamp)) return present;
            bool found = false;
            var patches = Harmony.GetPatchInfo(target);
            if (patches != null)
                foreach (var patch in patches.Postfixes.Where(p => p.PatchMethod.DeclaringType?.FullName == contract.TypeName && p.PatchMethod.Name == contract.Name))
                {
                    // A late supplier/second assembly must not bypass the
                    // dependency guards resolved for this language session.
                    if (!methods.TryGetValue(contract.Role, out var expected) || patch.PatchMethod != expected)
                        throw new InvalidDataException("language-consumer-closure:" + contract.Role);
                    found = true;
                }
            if (!guard.PublicationUnchanged(stamp)) throw new InvalidDataException("language-consumer-publication");
            last = stamp; read = true; present = found; return present;
        }
    }

    internal static bool AllowsPostfix(MethodBase target, Patch patch)
    {
        string? role = target == AccessTools.Method(typeof(BackCompatibilityConverter_1_5), "BackCompatibleDefName") ? "ce-postfix"
            : target == NameMethod ? "vef-postfix" : null;
        if (role == null) return false;
        var contract = Contracts.Single(c => c.Role == role);
        try
        {
            if (contract.Resolve(patch.PatchMethod.Module.Assembly) != patch.PatchMethod) return false;
            var info = Harmony.GetPatchInfo(target);
            bool Same(Patch p) => p.owner == patch.owner && p.PatchMethod == patch.PatchMethod;
            return info != null && info.Postfixes.Any(Same)
                && !info.Prefixes.Concat(info.Transpilers).Concat(info.Finalizers).Concat(info.InnerPrefixes).Concat(info.InnerPostfixes).Any(Same);
        }
        catch { return false; }
    }

    private void Initialize()
    {
        if (initialized) { if (refusal != null) throw refusal; return; }
        initialized = true;
        try
        {
            foreach (string name in new[] { "CombatExtended", "VEF" })
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == name);
                if (assembly == null) continue;
                foreach (var contract in Contracts.Where(c => c.Role.StartsWith(name == "VEF" ? "vef-" : "ce-", StringComparison.Ordinal)))
                {
                    var method = contract.Resolve(assembly); methods.Add(contract.Role, method);
                    if (!PublishedPatchGuard.TryCreate(method, GuardOwner, out var guard, true)) throw new InvalidDataException("language-consumer-guard");
                    dependencies.Add(guard!);
                }
            }
            cePublication = new ConsumerPublication(AccessTools.Method(typeof(BackCompatibilityConverter_1_5), "BackCompatibleDefName"), "ce-postfix");
            vefPublication = new ConsumerPublication(NameMethod, "vef-postfix");
        }
        catch (Exception e) { refusal = e; throw; }
    }

    internal void Validate()
    {
        Initialize();
        if (Scribe.mode != LoadSaveMode.Inactive) throw new InvalidDataException("language-scribe-active");
        foreach (var guard in dependencies)
            if (!guard.AllowsOriginalContract()) throw new InvalidDataException("language-consumer-hook:" + guard.Target.Name);
        var chain = Chain.GetValue(null) as IList;
        if (chain == null || chain.Count < ParsedLanguageContract.ConverterTypes.Length) throw new InvalidDataException("language-converter-chain");
        for (int i = 0; i < ParsedLanguageContract.ConverterTypes.Length; i++)
            if (chain[i]?.GetType() != ParsedLanguageContract.ConverterTypes[i]) throw new InvalidDataException("language-converter-chain");
        cePublication!.Check(methods);
        bool vefHookPresent = vefPublication!.Check(methods);
        int extra = chain.Count - ParsedLanguageContract.ConverterTypes.Length;
        if (extra == 0 && !vefHookPresent) return;
        if (extra != 1 || !methods.TryGetValue("vef-converter", out var converter)
            || chain[chain.Count - 1]?.GetType() != converter.DeclaringType) throw new InvalidDataException("language-converter-chain");
        // The live chain proves VEF registered its converter already. Do not
        // trigger its migration initializer merely because its DLL is loaded.
        Type utility = converter.DeclaringType!.DeclaringType!;
        ValidateMappings(AccessTools.Field(utility, "defNameConverters").GetValue(null));
        if (vefHookPresent)
        {
            if (AccessTools.Field(utility, "converter").GetValue(null)?.GetType() != converter.DeclaringType)
                throw new InvalidDataException("language-vef-converter");
            object callback = AccessTools.Field(methods["vef-postfix"].DeclaringType, "CheckSaveIdenticalToCurrentEnvironmentMethod").GetValue(null);
            if (!(callback is Func<bool> check) || check.Target != null || check.GetInvocationList().Length != 1 || check.Method != SaveCheck)
                throw new InvalidDataException("language-vef-save-delegate");
        }
    }

    internal static void ValidateMappings(object value)
    {
        if (!(value is Dictionary<string, Dictionary<Type, string>> outer)
            || !ReferenceEquals(outer.Comparer, EqualityComparer<string>.Default)) throw new InvalidDataException("language-vef-map-comparer");
        foreach (var inner in outer.Values)
        {
            if (inner == null || !ReferenceEquals(inner.Comparer, EqualityComparer<Type>.Default)) throw new InvalidDataException("language-vef-type-comparer");
            foreach (var key in inner.Keys)
                if (key == null || key.GetType() != typeof(ThingDef).GetType()) throw new InvalidDataException("language-vef-type-key");
        }
    }
}
