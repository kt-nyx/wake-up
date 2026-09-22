// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace WakeUp;

// One optional supplier prefix, on one native getter. Its customization branch
// remains eager; neither a package name nor another VEF patch gains admission.
internal sealed class DemandGraphicCompatibility
{
    internal const string PrefixType = "VEF.Graphics.VanillaExpandedFramework_Thing_DefaultGraphic_Patch";
    internal const string CacheType = "VEF.Graphics.ReflectionCache";
    internal const string PropertiesType = "VEF.Graphics.CompProperties_GraphicCustomization";
    internal const string CustomizationType = "VEF.Graphics.CompGraphicCustomization";
    private const string DependencyOwner = "wakeup.experimental-demand-graphics-dependency";
    internal static readonly SupplierMethodContract[] Contracts =
    {
        new SupplierMethodContract("default-graphic-prefix", PrefixType, "Prefix",
            "M|VEF:" + PrefixType + "|Prefix|mscorlib:System.Boolean|S||Assembly-CSharp:Verse.Thing,Assembly-CSharp:Verse.Graphic&", "8EA80C41049FFCD795C5C2B06E7E0F72A48C000C6F6137F0843A95C0CB97B7DB"),
        new SupplierMethodContract("graphic-field-accessor-construction", CacheType, ".cctor",
            "M|VEF:" + CacheType + "|.cctor|void|S||", "1A835CBFC9C04E793B0354A1441DE7F10E10305EA80960D9DE8A95933DF7D13C"),
        new SupplierMethodContract("customization-properties", PropertiesType, ".ctor",
            "M|VEF:" + PropertiesType + "|.ctor|void|I||", "C749A0D7404A43B48664C19CFFCCED5CE9A7B800A8D4DB618789D63EB41D6D7F")
    };
    internal static readonly string[] NativeExpected = { "8F2AD45B8ACB1A9C7CA9A05D6BA57EEB350071D9EB68FA283DE1E42BC71EE792", "EEA49220C70C935072D3EC2C3AA13E5BF5D32BB953E8C8FB69223E30DD83C12D" };
    internal static readonly string[] HarmonyExpected = { "3F7F5AB6A49A768890BB815EA87D399764B9E3EFD6D1A2F25DE10F6331F207D0", "3473F322D7EFDD51181B349808317C68E29ECCE18B319383DECA2C7867F909A9" };
    private readonly MethodInfo target = AccessTools.PropertyGetter(typeof(Thing), "DefaultGraphic");
    private readonly List<PublishedPatchGuard> dependencies = new List<PublishedPatchGuard>();
    private Assembly? supplier;
    private MethodBase? prefix;
    private Type? customization;
    internal string Status { get; private set; } = "No VEF default-graphic prefix observed.";

    internal static MethodBase[] SupplierTargets(Assembly assembly) => new MethodBase[]
    {
        assembly.GetType(PrefixType, true)!.GetMethod("Prefix", AccessTools.allDeclared)!,
        assembly.GetType(CacheType, true)!.TypeInitializer!,
        assembly.GetType(PropertiesType, true)!.GetConstructor(Type.EmptyTypes)!
    };

    internal static MethodInfo[] NativeTargets(Assembly game)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        return new[]
        {
            game.GetType("Verse.ThingCompUtility", true)!.GetMethods(flags).Single(m => m.Name == "TryGetComp" && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == "Verse.Thing"),
            game.GetType("Verse.ThingWithComps", true)!.GetMethods(flags).Single(m => m.Name == "GetComp" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
        };
    }

    internal static MethodInfo[] HarmonyTargets(Assembly harmony)
    {
        Type access = harmony.GetType("HarmonyLib.AccessTools", true)!;
        return new[]
        {
            access.GetMethods(AccessTools.allDeclared).Single(m => m.Name == "Field" && m.GetParameters().Select(p => p.ParameterType.FullName)
                .SequenceEqual(new[] { "System.Type", "System.String" })),
            access.GetMethods(AccessTools.allDeclared).Single(m => m.Name == "FieldRefAccess" && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 2 && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == "System.Reflection.FieldInfo")
        };
    }

    internal bool Allows(MethodBase nativeTarget, Patch patch)
    {
        if (nativeTarget != target || patch.PatchMethod.DeclaringType?.FullName != PrefixType || patch.PatchMethod.Name != "Prefix") return false;
        try
        {
            var info = Harmony.GetPatchInfo(nativeTarget);
            bool Same(Patch other) => other.owner == patch.owner && other.PatchMethod == patch.PatchMethod;
            if (info == null || !info.Prefixes.Any(Same)
                || info.Postfixes.Concat(info.Transpilers).Concat(info.Finalizers).Concat(info.InnerPrefixes).Concat(info.InnerPostfixes).Any(Same))
                throw new InvalidOperationException("verified method is not exclusively a native DefaultGraphic prefix");
            Assembly assembly = patch.PatchMethod.Module.Assembly;
            if (supplier == null) Resolve(assembly);
            if (!ReferenceEquals(supplier, assembly) || !Equals(prefix, patch.PatchMethod))
                throw new InvalidOperationException("different supplier assembly or prefix instance");
            return DependenciesAllowed();
        }
        catch (Exception error) { Status = "VEF default-graphic cooperation refused: " + error.GetBaseException().Message; return false; }
    }

    private void Resolve(Assembly assembly)
    {
        var resolved = Contracts.Select(contract => contract.Resolve(assembly)).ToArray();
        Type cache = assembly.GetType(CacheType, true)!;
        Type comp = assembly.GetType(CustomizationType, true)!;
        FieldInfo? field = cache.GetField("itemGraphic", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (field == null || !field.IsInitOnly || !field.FieldType.IsGenericType
            || field.FieldType.GetGenericTypeDefinition() != typeof(AccessTools.FieldRef<,>)
            || !field.FieldType.GetGenericArguments().SequenceEqual(new[] { typeof(Thing), typeof(Graphic) })
            || !typeof(ThingComp).IsAssignableFrom(comp))
            throw new InvalidOperationException("native graphic field accessor or customization type shape changed");
        FieldInfo? nativeField = typeof(Thing).GetField("graphicInt", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (nativeField?.FieldType != typeof(Graphic)) throw new InvalidOperationException("native cached graphic field changed");
        var checks = new List<PublishedPatchGuard>();
        void Guard(MethodBase method)
        {
            if (!PublishedPatchGuard.TryCreate(method, DependencyOwner, out var guard, allPatchKinds: true) || !guard!.AllowsOriginalContract())
                throw new InvalidOperationException(DescribePatches(method));
            checks.Add(guard);
        }
        foreach (MethodBase method in resolved) Guard(method);
        MethodInfo[] native = NativeTargets(typeof(Thing).Assembly), harmony = HarmonyTargets(typeof(Harmony).Assembly);
        void Pin(MethodInfo[] methods, string[] expected)
        {
            for (int i = 0; i < methods.Length; i++)
            {
                bool hashed = SemanticMethodIdentity.TryHash(methods[i], out string actual, out string reason);
                if (!hashed || expected[i].Length != 64 || actual != expected[i])
                    throw new InvalidOperationException("dependency body unpinned: " + SemanticMethodIdentity.Signature(methods[i]) + "; actual=" + actual + "; " + reason);
                Guard(methods[i]);
            }
        }
        Pin(native, NativeExpected); Pin(harmony, HarmonyExpected);
        // Watch the actual closed call sites as well as their generic definitions.
        foreach (MethodInfo method in native) Guard(method.MakeGenericMethod(comp));
        Guard(harmony[1].MakeGenericMethod(typeof(Thing), typeof(Graphic)));
        // Do not read itemGraphic: GetValue would run its entire cctor early.
        // The pinned cctor creates the ordinary readonly field-ref delegate at
        // its original first prefix use; no substitute accessor is installed.
        dependencies.AddRange(checks);
        customization = comp;
        prefix = resolved[0];
        supplier = assembly;
        Status = "Verified VEF ordinary DefaultGraphic prefix; customization definitions remain eager.";
    }

    internal bool DependenciesAllowed()
    {
        foreach (PublishedPatchGuard guard in dependencies)
            if (!guard.AllowsOriginalContract()) { Status = "VEF dependency changed: " + DescribePatches(guard.Target); return false; }
        return true;
    }

    internal bool RequiresEager(ThingDef def)
    {
        if (customization == null || def.comps == null) return false;
        foreach (CompProperties properties in def.comps)
            if (properties?.compClass != null && customization.IsAssignableFrom(properties.compClass)) return true;
        return false;
    }

    internal static string DescribePatches(MethodBase method)
    {
        string label = method.DeclaringType?.FullName + "." + method.Name;
        try
        {
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return label + ": no published patches (guard unavailable or publication changed)";
            var rows = new List<string>();
            void Add(string kind, IEnumerable<Patch> patches)
            {
                foreach (Patch patch in patches)
                    rows.Add(kind + " owner=" + patch.owner + " method=" + patch.PatchMethod.DeclaringType?.FullName + "." + patch.PatchMethod.Name);
            }
            Add("prefix", info.Prefixes); Add("postfix", info.Postfixes); Add("transpiler", info.Transpilers);
            Add("finalizer", info.Finalizers); Add("inner-prefix", info.InnerPrefixes); Add("inner-postfix", info.InnerPostfixes);
            return label + ": " + string.Join("; ", rows.Take(16));
        }
        catch (Exception error) { return label + ": patch inspection failed: " + error.GetType().Name; }
    }
}
