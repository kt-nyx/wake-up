// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;
using RimWorld.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace WakeUp;

// Only the consumer's GPU readback needs a finished source. Ordinary routing
// continues to return the holder's current object without joining producer work.
internal static class ImageOptReadiness
{
    private static bool attempted, present, admitted;
    private static string refusal = "Image Opt's texture completion contract is unavailable.";
    private static string refusalCode = "image-producer-contract";
    private static Func<Dictionary<int, VirtualFile>>? pending;
    private static Func<HashSet<int>>? external;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static ImageOptReaderSuppliers? readerSuppliers;
    internal static long ReadyReads, ExternalReads, PendingRefusals, ContractRefusals;

    internal static bool CanRead(Texture2D source, out string reason, out string code)
    {
        reason = "";
        code = refusalCode;
        if (!attempted) Initialize();
        code = refusalCode;
        if (!present) return true;
        if (!UnityData.IsInMainThread || source == null)
        { reason = "The texture is unavailable or the read is outside the main game thread."; return false; }
        if (!admitted || !guards.All(g => g.AllowsOriginalContract()))
        { ContractRefusals++; reason = refusal; return false; }
        try
        {
            if (readerSuppliers != null && !readerSuppliers.AllowsCurrentState(out reason, out code))
            { ContractRefusals++; return false; }
            int id = source.GetInstanceID();
            var tasks = pending!();
            if (tasks == null || tasks.ContainsKey(id))
            {
                PendingRefusals++;
                code = "image-source-pending";
                reason = "Image Opt has not finished this texture. Giddy-Up keeps its original readback for this call.";
                return false;
            }
            ReadyReads++;
            if (external?.Invoke()?.Contains(id) == true) ExternalReads++; // Evidence only, never readiness admission.
            return true;
        }
        catch { ContractRefusals++; reason = refusal; return false; }
    }
    private static void Initialize()
    {
        attempted = true;
        present = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.Equals(ImageSupplierPolicy.ImageOptPackage, StringComparison.OrdinalIgnoreCase));
        if (!present) return;
        try
        {
            if (IntPtr.Size != 8 || Environment.OSVersion.Platform != PlatformID.Win32NT
                || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11) return;
            var assembly = LoaderSupplierPolicy.Find(ImageSupplierPolicy.ImageOptPackage, "ImageOpt.ImageOpt");
            if (assembly == null || !LoaderSupplierPolicy.MatchesImage(assembly, ImageSupplierPolicy.ImageOptHash)) return;
            if (!NativeImageMatches()) return;
            Type load = assembly.GetType("ImageOpt.TextureLoadPatch", true), completion = assembly.GetType("ImageOpt.ParallelTextureLoadPatch", true);
            var methods = new[] { AccessTools.Method(load, "Prefix"), AccessTools.Method(load, "_LoadTextureAsyncV2"),
                AccessTools.Method(load, "_LoadTextureSync"), AccessTools.Method(load, "VanillaLoadTexture"),
                AccessTools.Method(completion, "Prefix"), AccessTools.Method(completion, "PrefixV2") };
            var checks = new List<PublishedPatchGuard>();
            foreach (var method in methods)
                if (method == null || !SupplierBodyIdentity.Matches(method) || !AddGuard(method, checks)) return;
            MethodInfo nativeLoad = AccessTools.Method(typeof(ModContentLoader<Texture2D>), "LoadTexture");
            MethodInfo nativeComplete = AccessTools.Method(typeof(ModContentPack), "AnyContentLoaded");
            if (!ImageOptReaderSuppliers.TryCreate(nativeLoad, nativeComplete, checks, out readerSuppliers))
            {
                refusal = "The installed image-provider callbacks are not supported by Wake-Up's smaller Giddy-Up readback. Giddy-Up keeps its original readback; other Wake-Up operations keep their own checks.";
                refusalCode = "image-producer-tuple";
                return;
            }
            MethodInfo? graphicsPrefix = null;
            if (LoaderSupplierPolicy.Patches(nativeLoad).Any(p => p.owner == ImageSupplierPolicy.GraphicsOwner))
            {
                var graphics = LoaderSupplierPolicy.Find(ImageSupplierPolicy.GraphicsPackage, "GraphicSetter.Patches.TextureLoadingPatch+LoadTexture_Patch");
                if (graphics == null || !LoaderSupplierPolicy.MatchesImage(graphics, "4997bd1f8f94443ef2fa9b25fb75ba15de5b1fc2427d26b81e31c890f40c77d0")) return;
                graphicsPrefix = AccessTools.Method(graphics.GetType("GraphicSetter.Patches.TextureLoadingPatch+LoadTexture_Patch"), "Prefix");
                if (!SupplierBodyIdentity.Matches(graphicsPrefix) || !AddGuard(graphicsPrefix, checks)) return;
            }
            bool AllowsLoad(Patch p) => p.PatchMethod == methods[0] && p.owner == ImageSupplierPolicy.ImageOptOwner
                && ImageSupplierPolicy.ExactPublication(nativeLoad, methods[0], ImageSupplierPolicy.ImageOptOwner, true, 1000,
                    new[] { ImageSupplierPolicy.GraphicsOwner })
                || graphicsPrefix != null && p.PatchMethod == graphicsPrefix && p.owner == ImageSupplierPolicy.GraphicsOwner
                && ImageSupplierPolicy.ExactPublication(nativeLoad, graphicsPrefix, ImageSupplierPolicy.GraphicsOwner, true)
                || readerSuppliers!.AllowsLoad(p);
            bool AllowsCompletion(Patch p) => p.PatchMethod == methods[4] && p.owner == ImageSupplierPolicy.ImageOptOwner
                && ImageSupplierPolicy.ExactPublication(nativeComplete, methods[4], ImageSupplierPolicy.ImageOptOwner, true)
                || readerSuppliers!.AllowsCompletion(p);
            if (!ImageSupplierPolicy.ExactPublication(nativeLoad, methods[0], ImageSupplierPolicy.ImageOptOwner, true, 1000, new[] { ImageSupplierPolicy.GraphicsOwner })
                || !ImageSupplierPolicy.ExactPublication(nativeComplete, methods[4], ImageSupplierPolicy.ImageOptOwner, true)
                || !AddGuard(nativeLoad, checks, AllowsLoad) || !AddGuard(nativeComplete, checks, AllowsCompletion)) return;
            pending = FieldGetter<Dictionary<int, VirtualFile>>(AccessTools.Field(completion, "Tasks"));
            external = FieldGetter<HashSet<int>>(AccessTools.Field(assembly.GetType("ImageOpt.Texture2DPatch", true), "NativeTextures"));
            guards = checks.ToArray();
            admitted = pending != null;
        }
        catch { admitted = false; }
    }
    internal static bool AddGuard(MethodBase method, List<PublishedPatchGuard> checks, Func<Patch,bool>? allowed = null)
    {
        if (!PublishedPatchGuard.TryCreate(method, GiddyTextureRuntime.Owner, out var guard, true, allowed) || !guard!.AllowsOriginalContract()) return false;
        checks.Add(guard); return true;
    }
    private static Func<T>? FieldGetter<T>(FieldInfo? field)
    {
        if (field == null || !field.IsStatic || field.FieldType != typeof(T)) return null;
        var getter = new DynamicMethod("WakeUpImageOptField", typeof(T), Type.EmptyTypes, typeof(ImageOptReadiness), true);
        var il = getter.GetILGenerator(); il.Emit(OpCodes.Ldsfld, field); il.Emit(OpCodes.Ret);
        return (Func<T>)getter.CreateDelegate(typeof(Func<T>));
    }
    private static bool NativeImageMatches()
    {
        var mod = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId.Equals(ImageSupplierPolicy.ImageOptPackage, StringComparison.OrdinalIgnoreCase));
        string path = Path.GetFullPath(Path.Combine(mod.RootDir, "image_lib.dll"));
        using var process = Process.GetCurrentProcess();
        var loaded = process.Modules.Cast<ProcessModule>().Where(m => string.Equals(m.ModuleName, "image_lib.dll", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (loaded.Length != 1 || !string.Equals(Path.GetFullPath(loaded[0].FileName), path, StringComparison.OrdinalIgnoreCase)) return false;
        OwnedCacheStore.RejectLinkedPath(path);
        using var file = File.OpenRead(path); using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant()
            == "72cec9dc93e3f470df1f81791e310c3e09b580bd615c146fefd85a6166f815d4";
    }
}
