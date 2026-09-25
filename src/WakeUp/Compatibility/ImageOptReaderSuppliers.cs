// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

// This is a reader contract, not a replacement scheduler. Image Opt returns a
// finished synchronous image or a placeholder recorded in Tasks; its completion
// callback removes that ID only after publishing the finished image/fallback.
// Preview's scheduling history need not be reconstructed to read a live source
// that is not pending. The compatibility patch publishes its copies synchronously.
internal sealed class ImageOptReaderSuppliers
{
    internal const string PreviewHash = "4cb5a8434d1e44f3e69d483860f377b4204ed00483036f98fc2b86e34b7283ec";
    internal const string CompatHash = "cfc159f52cd9ddc7d52d19975bda18c4157152e0087f2ca221898f25d412fefd";
    private const string PreviewOwner = "FasterGameLoadingMod", CompatOwner = "DegradingAnt.RimCompat";
    private readonly MethodInfo load, complete;
    private MethodInfo? previewPrefix, previewPostfix, compatPostfix;
    private Func<bool>? imageOptActive, destroysOriginals;
    private ImageOptReaderSuppliers(MethodInfo load, MethodInfo complete)
    { this.load = load; this.complete = complete; }

    internal static bool TryCreate(MethodInfo load, MethodInfo complete, List<PublishedPatchGuard> guards,
        out ImageOptReaderSuppliers? adapter)
    {
        adapter = new ImageOptReaderSuppliers(load, complete);
        var preview = LoaderSupplierPolicy.Find("taranchuk.fastergameloading", "FasterGameLoading.ImageOptEarlyLoadCoordinator");
        if (preview != null && !adapter.AddPreview(preview, guards)) return false;
        bool compatPresent = LoadedModManager.RunningModsListForReading.Any(m =>
            m.PackageId.Equals(CompatOwner, StringComparison.OrdinalIgnoreCase));
        if (compatPresent)
        {
            var compat = LoaderSupplierPolicy.Find(CompatOwner, "ImageOptCompat.ImageOptCompatMod");
            if (compat == null || !adapter.AddCompatibility(compat, guards)) return false;
        }
        return true;
    }

    internal bool AllowsCurrentState(out string reason, out string code)
    {
        reason = ""; code = "image-producer-tuple";
        if (imageOptActive != null && !imageOptActive())
        { reason = "Faster Game Loading's active image provider changed. Giddy-Up keeps its original readback."; return false; }
        if (destroysOriginals != null && destroysOriginals())
        {
            code = "image-original-destruction";
            reason = "The Image Opt compatibility patch is set to destroy original textures. Wake-Up leaves Giddy-Up's original readback in control. To use the smaller readback, turn off destroyOriginalTexture in that patch and restart; Wake-Up does not change this setting.";
            return false;
        }
        return true;
    }

    internal bool AllowsLoad(Patch patch)
        => previewPrefix != null && previewPostfix != null && patch.owner == PreviewOwner
        && (patch.PatchMethod == previewPrefix || patch.PatchMethod == previewPostfix)
        && ImageSupplierPolicy.ExactPublication(load, previewPrefix, PreviewOwner, true, Priority.Normal, new[] { ImageSupplierPolicy.GraphicsOwner })
        && ImageSupplierPolicy.ExactPublication(load, previewPostfix, PreviewOwner, false, Priority.Normal, new[] { ImageSupplierPolicy.GraphicsOwner });

    internal bool AllowsCompletion(Patch patch)
        => compatPostfix != null && patch.owner == CompatOwner && patch.PatchMethod == compatPostfix
        && ImageSupplierPolicy.ExactPublication(complete, compatPostfix, CompatOwner, false);

    private bool AddPreview(Assembly assembly, List<PublishedPatchGuard> guards)
    {
        if (!LoaderSupplierPolicy.MatchesImage(assembly, PreviewHash)) return false;
        var texture = assembly.GetType("FasterGameLoading.ModContentLoaderTexture2D_LoadTexture_Patch", true);
        previewPrefix = AccessTools.Method(texture, "Prefix");
        previewPostfix = AccessTools.Method(texture, "Postfix");
        var active = AccessTools.PropertyGetter(assembly.GetType("FasterGameLoading.ImageOptCompat", true), "IsActive");
        if (!Check(guards, previewPrefix, previewPostfix, active,
            AccessTools.Method(assembly.GetType("FasterGameLoading.Utils", true), "IsModActive"),
            AccessTools.Method(texture, "RegisterSkippedBakingTextureIfApplicable"),
            AccessTools.Method(texture, "SaveTexturePath"),
            AccessTools.Method(assembly.GetType("FasterGameLoading.AdaptiveBakingSkipList", true), "ShouldSkipBaking"),
            AccessTools.Method(assembly.GetType("FasterGameLoading.AdaptiveBakingSkipList", true), "IsProtectedModTexturePath"))) return false;
        imageOptActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), active);
        return ImageSupplierPolicy.ExactPublication(load, previewPrefix, PreviewOwner, true, Priority.Normal, new[] { ImageSupplierPolicy.GraphicsOwner })
            && ImageSupplierPolicy.ExactPublication(load, previewPostfix, PreviewOwner, false, Priority.Normal, new[] { ImageSupplierPolicy.GraphicsOwner });
    }

    private bool AddCompatibility(Assembly assembly, List<PublishedPatchGuard> guards)
    {
        if (!LoaderSupplierPolicy.MatchesImage(assembly, CompatHash)) return false;
        compatPostfix = AccessTools.Method(assembly.GetType("ImageOptCompat.ModContentPack_AnyContentLoaded_Patch", true), "Postfix");
        var settings = AccessTools.PropertyGetter(assembly.GetType("ImageOptCompat.ImageOptCompatMod", true), "Settings");
        var vehicle = assembly.GetType("ImageOptCompat.VehicleReadback", true);
        var reads = assembly.GetType("ImageOptCompat.Texture2DReadPatches", true);
        var readPrefix = AccessTools.Method(assembly.GetType("ImageOptCompat.Texture2DReadPatches+GetPixel2", true), "Prefix");
        if (!Check(guards, compatPostfix, settings, readPrefix,
            AccessTools.Method(reads, "Readable"), AccessTools.Method(reads, "NativeIds"),
            AccessTools.Method(vehicle, "NeedsCpuReadback"), AccessTools.Method(vehicle, "ConvertHolder"),
            AccessTools.Method(vehicle, "ToCpuReadable"), AccessTools.Method(vehicle, "RecompressIfWorthwhile"),
            AccessTools.Method(vehicle, "IsBlockCompressed"), AccessTools.Method(vehicle, "SafeName"))) return false;
        var field = AccessTools.Field(settings.ReturnType, "destroyOriginalTexture");
        if (field == null || field.IsStatic || field.FieldType != typeof(bool)) return false;
        var getter = new DynamicMethod("WakeUpImageOptDestroyPolicy", typeof(bool), Type.EmptyTypes, typeof(ImageOptReaderSuppliers), true);
        var il = getter.GetILGenerator(); il.Emit(OpCodes.Call, settings); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
        destroysOriginals = (Func<bool>)getter.CreateDelegate(typeof(Func<bool>));
        var getPixel = AccessTools.Method(typeof(Texture2D), "GetPixel", new[] { typeof(int), typeof(int) });
        bool AllowsPixel(Patch p) => p.owner == CompatOwner && p.PatchMethod == readPrefix
            && ImageSupplierPolicy.ExactPublication(getPixel, readPrefix, CompatOwner, true);
        return ImageSupplierPolicy.ExactPublication(complete, compatPostfix, CompatOwner, false)
            && ImageSupplierPolicy.ExactPublication(getPixel, readPrefix, CompatOwner, true)
            && ImageOptReadiness.AddGuard(getPixel, guards, AllowsPixel);
    }

    private static bool Check(List<PublishedPatchGuard> guards, params MethodInfo?[] methods)
        => methods.All(m => m != null && SupplierBodyIdentity.Matches(m) && ImageOptReadiness.AddGuard(m, guards));
}
