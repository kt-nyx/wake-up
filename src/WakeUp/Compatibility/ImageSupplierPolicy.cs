// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

// The outer FastLoader callbacks keep their normal place around native reload.
// A supplier hit may skip native work; a miss can use our guarded native path.
internal static class ImageSupplierPolicy
{
    internal const string FastLoaderPackage = "solaris.fastloader";
    internal const string FastLoaderRelease = "cbe0831af57dfb85db674a2bc169969ba204ca5adaf7bb494fd1438f1bcd3773";
    internal const string FastLoaderRepository = "c7d008936d8fa586b6a57497ec3d494f9324447742bceef15cf72085d7db2fd2";
    internal const string ImageOptPackage = "dev.soeur.imageopt";
    internal const string ImageOptOwner = "dev.soeur.ImageOpt";
    internal const string ImageOptHash = "c4437b7ebcc10b4b3ff4f20636c81fe2511ee132a8a1a5e2ff932e3e8329035e";
    internal const string GraphicsPackage = "telefonmast.graphicssettings";
    internal const string GraphicsOwner = "com.telefonmast.graphicssettings.rimworld.mod";
    private static readonly MethodInfo reload = AccessTools.Method(typeof(ModContentPack), "ReloadContentInt");
    private static bool fastAttempted;
    private static Assembly? fast;
    private static MethodInfo? fastPrefix, fastPostfix;
    private static FieldInfo? fastSettings, fastMaster, fastAtlas;
    private static PublishedPatchGuard[] fastBodies = Array.Empty<PublishedPatchGuard>();
    private static PublishedPatchGuard? fastPublication;

    internal static bool ExactPublication(MethodBase target, MethodInfo method, string owner, bool prefix,
        int priority = Priority.Normal, string[]? before = null, string[]? after = null)
    {
        var record = Harmony.GetPatchInfo(target);
        if (record == null) return false;
        var expected = prefix ? record.Prefixes : record.Postfixes;
        var other = (prefix ? record.Postfixes : record.Prefixes).Concat(record.Transpilers)
            .Concat(record.Finalizers).Concat(record.InnerPrefixes).Concat(record.InnerPostfixes);
        return expected.Count(p => p.PatchMethod == method && p.owner == owner && p.priority == priority
            && p.before.SequenceEqual(before ?? Array.Empty<string>()) && p.after.SequenceEqual(after ?? Array.Empty<string>())) == 1
            && expected.Count(p => p.PatchMethod == method) == 1 && !other.Any(p => p.PatchMethod == method);
    }
    private static void FindFastLoader()
    {
        if (fastAttempted) return;
        fastAttempted = true;
        try
        {
            var assembly = LoaderSupplierPolicy.Find(FastLoaderPackage, "FastLoader.FastLoaderMod");
            if (assembly == null || !(LoaderSupplierPolicy.MatchesImage(assembly, FastLoaderRelease)
                || LoaderSupplierPolicy.MatchesImage(assembly, FastLoaderRepository))) return;
            var type = assembly.GetType("FastLoader.Patch_ModContentPackReloadContentInt_TextureCache", true);
            var prefix = AccessTools.Method(type, "Prefix"); var postfix = AccessTools.Method(type, "Postfix");
            var guards = new List<PublishedPatchGuard>();
            foreach (var method in new[] { prefix, postfix })
            {
                if (method == null || !SupplierBodyIdentity.Matches(method)
                    || !PublishedPatchGuard.TryCreate(method, PngRuntime.Owner, out var guard, true)
                    || !guard!.AllowsOriginalContract()) return;
                guards.Add(guard);
            }
            fastSettings = AccessTools.Field(assembly.GetType("FastLoader.FastLoaderRuntime"), "Settings");
            var settingsType = assembly.GetType("FastLoader.FastLoaderSettings");
            fastMaster = AccessTools.Field(settingsType, "CacheEnabled");
            fastAtlas = AccessTools.Field(settingsType, "AtlasCacheEnabled");
            if (fastSettings?.FieldType != settingsType || !fastSettings.IsStatic
                || fastMaster?.FieldType != typeof(bool) || fastMaster.IsStatic
                || fastAtlas?.FieldType != typeof(bool) || fastAtlas.IsStatic) return;
            bool Allowed(Patch p) => p.owner != FastLoaderPackage
                || (p.PatchMethod == prefix || p.PatchMethod == postfix)
                    && ExactPublication(reload, prefix, FastLoaderPackage, true)
                    && ExactPublication(reload, postfix, FastLoaderPackage, false)
                    && LoaderSupplierPolicy.Patches(reload).Count(q => q.owner == FastLoaderPackage) == 2;
            if (!PublishedPatchGuard.TryCreate(reload, PngRuntime.Owner, out fastPublication, true, Allowed)) return;
            fast = assembly; fastPrefix = prefix; fastPostfix = postfix; fastBodies = guards.ToArray();
        }
        catch { fast = null; }
    }
    internal static bool AllowsFastLoaderReload(MethodBase target, Patch patch)
    {
        if (target != reload || patch.owner != FastLoaderPackage) return false;
        FindFastLoader();
        return fast != null && (patch.PatchMethod == fastPrefix || patch.PatchMethod == fastPostfix)
            && FastLoaderCompatible();
    }
    internal static bool FastLoaderCompatible()
        => fast == null || fastBodies.All(g => g.AllowsOriginalContract())
            && fastPublication?.AllowsOriginalContract() == true;

    internal static bool FastLoaderMayOwnAtlas()
    {
        if (!LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.Equals(FastLoaderPackage, StringComparison.OrdinalIgnoreCase))) return false;
        FindFastLoader();
        if (fast == null || !FastLoaderCompatible()) return true;
        try
        {
            object? settings = fastSettings!.GetValue(null);
            return settings == null || (bool)fastMaster!.GetValue(settings) && (bool)fastAtlas!.GetValue(settings);
        }
        catch { return true; }
    }

    internal static void RefuseNativeLoader(string reason)
    {
        var target = AccessTools.Method(typeof(ModContentLoader<Texture2D>), "LoadTexture");
        string provider = LoaderSupplierPolicy.Patches(target).Any(p => p.owner == ImageOptOwner) ? "Image Opt"
            : LoaderSupplierPolicy.Patches(target).Any(p => p.owner == GraphicsOwner) ? "Graphics Settings+" : "Wake-Up";
        string explanation = provider == "Wake-Up" ? reason : provider + " owns image decoding and texture creation. "
            + "Its textures stay in use; Wake-Up's independent code, XML and resource searches can still run. "
            + (provider == "Graphics Settings+" ? "Its DDS checkbox does not restore the native loader. " : "")
            + "To use Wake-Up's native image cache, select a setup with the native image loader and restart. Technical detail: " + reason;
        foreach (string id in new[] { "texture-loader", "texture-cache", "prepared", "psd", "quality" })
            CompatibilityStatus.Refuse(id, explanation, provider == "Wake-Up" ? "required-contract" : "supplier-image-producer", provider);
    }
}
