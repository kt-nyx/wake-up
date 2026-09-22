// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;

namespace WakeUp;

// A validated record may lend its pinned pixel range only to the reviewed Windows
// synchronous upload route. Unsupported engines or changed callbacks retain
// the separately guarded array upload; C08's full callback policy is unchanged.
internal static class WarmTextureUploadContract
{
    internal const string CoreBinaryHash = "C5C58EA254834291780A1D6C388C241443D07167B4A4B890A23C9494F626DDBA";
    internal const string PointerBodyHash = "1E87EE7C72840829DBDEA8DEF016E6860B465B16299E0C1D9803571EC4C8634D";
    private const string NonReadableBodyHash = "A8F6803DC7E65D2AFBF06845280E084B212098F2A34A78EFCD5EBC007B3389D0";
    private const string ExceptionBodyHash = "E74940C1EAAF1BE4442FB77A84D7521E6F11F0BB195B8BE42B4EBB9D8C83B3A1";
    private const string GuardOwner = "WakeUp.AuthenticatedTextureUpload";
    private static PublishedPatchGuard.PublicationSet? publications;

    internal static bool IsPointerTarget(MethodBase method) => method?.DeclaringType == typeof(Texture2D)
        && (method.Name == "LoadRawTextureData" && method.GetParameters().Select(p => p.ParameterType)
                .SequenceEqual(new[] { typeof(IntPtr), typeof(int) })
            || method.Name == "LoadRawTextureDataImpl" && method.GetParameters().Select(p => p.ParameterType)
                .SequenceEqual(new[] { typeof(IntPtr), typeof(ulong) }));

    internal static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(Texture2D), "LoadRawTextureData", new[] { typeof(IntPtr), typeof(int) });
        yield return AccessTools.Method(typeof(Texture2D), "LoadRawTextureDataImpl", new[] { typeof(IntPtr), typeof(ulong) });
        // The virtual readability getter dispatches to Texture2D. Keep the
        // failure route too: engine refusal can still construct an exception.
        yield return AccessTools.PropertyGetter(typeof(Texture), "isReadable");
        yield return AccessTools.PropertyGetter(typeof(Texture2D), "isReadable");
        yield return AccessTools.Method(typeof(Texture), "CreateNonReadableException", new[] { typeof(Texture) });
        yield return AccessTools.PropertyGetter(typeof(UnityEngine.Object), "name");
        yield return AccessTools.Constructor(typeof(UnityException), new[] { typeof(string) });
        yield return AccessTools.Constructor(typeof(SystemException), new[] { typeof(string) });
        yield return AccessTools.Constructor(typeof(Exception), new[] { typeof(string) });
        yield return AccessTools.PropertySetter(typeof(Exception), "HResult");
        yield return AccessTools.Method(typeof(string), "Format", new[] { typeof(string), typeof(object) });
        yield return AccessTools.Method(typeof(IntPtr), "op_Equality", new[] { typeof(IntPtr), typeof(IntPtr) });
        // Debug.LogError is unreachable only because the caller validates a
        // nonzero pinned pointer and strictly positive byte length before entry.
    }

    internal static void Initialize()
    {
        publications = null;
        try
        {
            if (!GameBuildContract.Current.HasReviewedTextureConsumers) return;
            ValidateReference(typeof(Texture2D).Assembly);
            var guards = new List<PublishedPatchGuard>();
            foreach (var target in Targets().Distinct())
            {
                if (target == null || !PublishedPatchGuard.TryCreate(target, GuardOwner, out var guard, true)) return;
                guards.Add(guard!);
            }
            PublishedPatchGuard.TryCapturePublications(guards, out publications);
        }
        catch { publications = null; }
    }

    internal static bool Allowed
    {
        get
        {
            try { return QualityUnityCallbacks.WarmArrayAllowed && publications?.Unchanged == true; }
            catch { return false; }
        }
    }

    internal static void ValidateReference(Assembly core)
    {
        if (core.GetName().Name != "UnityEngine.CoreModule" || string.IsNullOrEmpty(core.Location))
            throw new InvalidOperationException("warm-pointer-core-identity");
        ValidateBinary(core.Location);
        ValidateBodies(core);
    }

    internal static void ValidateBinary(string path)
    {
        using (var file = File.OpenRead(path))
        using (var sha = SHA256.Create())
            if (PngCache.Hex(sha.ComputeHash(file)) != CoreBinaryHash)
                throw new InvalidOperationException("warm-pointer-core-binary");
    }

    internal static void ValidateBodies(Assembly core)
    {
        Type texture = core.GetType("UnityEngine.Texture", true)!;
        Type texture2D = core.GetType("UnityEngine.Texture2D", true)!;
        Type exception = core.GetType("UnityEngine.UnityException", true)!;
        ValidateBody(AccessTools.Method(texture2D, "LoadRawTextureData", new[] { typeof(IntPtr), typeof(int) }), PointerBodyHash);
        ValidateBody(AccessTools.Method(texture, "CreateNonReadableException", new[] { texture }), NonReadableBodyHash);
        ValidateBody(AccessTools.Constructor(exception, new[] { typeof(string) }), ExceptionBodyHash);
        var implementation = AccessTools.Method(texture2D, "LoadRawTextureDataImpl", new[] { typeof(IntPtr), typeof(ulong) });
        if (implementation == null || implementation.IsStatic || implementation.ReturnType != typeof(bool)
            || (implementation.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) == 0
            || implementation.GetMethodBody() != null)
            throw new InvalidOperationException("warm-pointer-native-implementation");
    }

    private static void ValidateBody(MethodBase method, string expected)
    {
        byte[]? body = method?.GetMethodBody()?.GetILAsByteArray();
        // Raw tokens are meaningful here because the complete supplier binary
        // is pinned above. This also rejects rewritten in-memory wrapper bodies.
        if (body == null || PngCache.Hex(CacheDigest.HashInput(body)) != expected)
            throw new InvalidOperationException("warm-pointer-managed-body");
    }
}
