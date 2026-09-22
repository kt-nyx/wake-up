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

// Quality replacement destroys its former native objects only while the actual
// producer/restore Unity calls cannot have lent them to a foreign callback.
// Keep the publication epoch, independently of the optional first-build queue.
internal static class QualityUnityCallbacks
{
    private static PublishedPatchGuard.PublicationSet? publications;
    private static PublishedPatchGuard.PublicationSet? warmArrayPublications;
    internal static IEnumerable<MethodBase> Targets()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var ctor in typeof(Texture2D).GetConstructors(flags)) yield return ctor;
        foreach (var method in typeof(ImageConversion).GetMethods(flags).Where(m => m.Name == "LoadImage")) yield return method;
        var names = new HashSet<string>(StringComparer.Ordinal) {
            "Internal_Create", "Internal_CreateImpl", "ValidateFormat", "GetTextureColorSpace", "CreateNonReadableException",
            "LoadRawTextureDataImpl", "LoadRawTextureDataImplArray", "SetPixelDataImpl", "SetPixelDataImplArray", "ApplyImpl",
            "GetDataWidth", "GetDataHeight",
            "LoadRawTextureData", "Apply", "Compress", "GetRawTextureData",
            "get_width", "get_height", "get_graphicsFormat", "get_mipmapCount", "get_isReadable", "get_format",
            "get_filterMode", "set_filterMode", "get_anisoLevel", "set_anisoLevel", "get_mipMapBias", "set_mipMapBias",
            "get_wrapModeU", "set_wrapModeU", "get_wrapModeV", "set_wrapModeV", "get_wrapModeW", "set_wrapModeW" };
        foreach (var type in new[] { typeof(Texture), typeof(Texture2D) })
            foreach (var method in type.GetMethods(flags))
            {
                if (method.Name == "SetPixelData" && method.IsGenericMethodDefinition)
                    yield return method.MakeGenericMethod(typeof(byte));
                else if (names.Contains(method.Name) && !method.IsGenericMethodDefinition) yield return method;
            }
        foreach (var method in typeof(UnityEngine.Object).GetMethods(flags).Where(m =>
            new[] { "get_name", "set_name", "GetName", "SetName", "op_Equality", "op_Inequality", "op_Implicit", "CompareBaseObjects",
                "IsNativeObjectAlive", "GetCachedPtr", "GetInstanceID", "ContainsObjectWithInstanceID", "IsPersistent", "DestroyImmediate" }.Contains(m.Name)))
            yield return method;
        foreach (var method in typeof(UnityEngine.Experimental.Rendering.GraphicsFormatUtility).GetMethods(flags)
            .Where(m => m.Name == "IsSRGBFormat" || m.Name == "GetFormat" && m.GetParameters().Any(p => typeof(Texture).IsAssignableFrom(p.ParameterType))))
            yield return method;
        // Warm reads capture and recheck these engine inputs while sources are
        // leased. Their managed entry points must remain free of mod callbacks.
        foreach (var method in typeof(SystemInfo).GetMethods(flags).Where(m => new[] {
            "get_graphicsDeviceType", "get_graphicsDeviceVersion", "get_graphicsDeviceVendorID", "get_graphicsDeviceID",
            "IsFormatSupported", "GetGraphicsDeviceType", "GetGraphicsDeviceVersion", "GetGraphicsDeviceVendorID", "GetGraphicsDeviceID"
        }.Contains(m.Name))) yield return method;
        yield return AccessTools.PropertyGetter(typeof(Application), "unityVersion");
        yield return AccessTools.PropertyGetter(typeof(QualitySettings), "activeColorSpace");
        yield return AccessTools.PropertyGetter(typeof(Prefs), "TextureCompression");
        yield return AccessTools.PropertyGetter(typeof(UnityData), "ComputeShadersSupported");
        foreach (var method in typeof(Graphics).GetMethods(flags).Where(m => m.Name == "CopyTexture" && m.GetParameters().Length == 12))
            yield return method;
        yield return AccessTools.Method(typeof(StaticTextureAtlas), "CalculateMaxMipmapsForDxtSupport");
    }
    internal static void Initialize()
    {
        publications = null;
        warmArrayPublications = null;
        try
        {
            // Capture independently: a foreign pointer overload must not remove
            // the existing array route, while full C08 keeps every old target.
            var targets = Targets().Distinct().ToArray();
            publications = Capture(targets);
            warmArrayPublications = Capture(targets.Where(t => !WarmTextureUploadContract.IsPointerTarget(t)));
        }
        catch { publications = null; warmArrayPublications = null; }
        WarmTextureUploadContract.Initialize();
    }
    internal static IEnumerable<MethodBase> WarmArrayTargets()
        => Targets().Where(t => !WarmTextureUploadContract.IsPointerTarget(t));
    private static PublishedPatchGuard.PublicationSet? Capture(IEnumerable<MethodBase> targets)
    {
        var guards = new List<PublishedPatchGuard>();
        foreach (var target in targets)
        {
            if (target == null || !PublishedPatchGuard.TryCreate(target, PngRuntime.Owner, out var guard, true)) return null;
            guards.Add(guard!);
        }
        if (guards.Count >= 20 && PublishedPatchGuard.TryCapturePublications(guards, out var captured)) return captured;
        return null;
    }
    internal static bool WarmArrayAllowed
    {
        get
        {
            try { return warmArrayPublications?.Unchanged == true; }
            catch { return false; }
        }
    }
    internal static bool Allowed
    {
        get
        {
            try { return publications?.Unchanged == true; }
            catch { return false; }
        }
    }
    internal static bool Check()
    {
        bool allowed = Allowed;
        if (!allowed) PreparedQualityRuntime.UnsafeBoundary("Unity texture callback ownership changed");
        return allowed;
    }
}
