// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using RimWorld;
using RimWorld.IO;
using UnityEngine;
using Verse;

namespace WakeUp;

// Only the verified native callback is decomposed. Each Unity operation and each
// constructor still runs once, on its original thread, before dependent work.
internal static class NativeLoadingUnits
{
    internal const string CallAllBody = "4504210DABE5F4D7C4B8DB2C0B27B3E316380618F90272D787FF8AF3926FD752";
    private static readonly MethodInfo CallAll = AccessTools.Method(typeof(StaticConstructorOnStartupUtility), "CallAll");
    private static readonly MethodInfo BakeAll = AccessTools.Method(typeof(GlobalTextureAtlasManager), "BakeStaticAtlases");
    internal static string Status { get; private set; } = "Native loading units have not run.";

    private static bool OrdinaryBody(MethodInfo target, string expected) =>
        SemanticMethodIdentity.TryHash(target, out string actual, out _) && actual == expected
        && PublishedPatchGuard.TryCreate(target, AtlasBatchScheduling.Owner, out var guard, allPatchKinds: true,
            allowedForeignPatch: patch => LoadingInvocationObservation.AllowsHook(target, patch))
        && guard!.AllowsOriginalContract();

    internal static IEnumerator StartupCallback()
    {
        DeepProfiler.Start("Static constructor calls");
        var constructors = Constructors();
        try
        {
            while (constructors.MoveNext()) yield return null;
            if (Prefs.DevMode) StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes();
        }
        finally { (constructors as IDisposable)?.Dispose(); DeepProfiler.End(); }
        FloatMenuMakerMap.Init();
        yield return null;

        DeepProfiler.Start("Atlas baking.");
        IEnumerator atlases = AtlasRuntime.CanBatch ? AtlasRuntime.BakeBatches() : NativeAtlases();
        try { while (atlases.MoveNext()) yield return null; }
        finally { (atlases as IDisposable)?.Dispose(); DeepProfiler.End(); }

        // Preserve the native suffix and its exception behavior. In particular,
        // do not run cleanup after a constructor-enumeration or atlas failure.
        DeepProfiler.Start("Garbage Collection");
        try
        {
            AbstractFilesystem.ClearAllCache();
            GC.Collect(int.MaxValue, GCCollectionMode.Forced);
            Resources.UnloadUnusedAssets();
        }
        finally { DeepProfiler.End(); }
    }

    private static IEnumerator Constructors()
    {
        if (!OrdinaryBody(CallAll, CallAllBody))
        {
            Status = "Static constructor scheduling is unsupported with the current method or hooks; the ordinary CallAll executes once.";
            LoadingObservationRuntime.Progress("Static constructors", Status, 0, null);
            StaticConstructorOnStartupUtility.CallAll();
            yield break;
        }
        Status = "Static constructors run in native order; loading updates between batches of completed calls.";
        DeepProfiler.Start("StaticConstructorOnStartupUtility.CallAll()");
        // Do not bypass the actual generic attribute helper: C12 owns its
        // internal acceleration and suppliers can legitimately observe it.
        var types = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartup>();
        int completed = 0;
        foreach (Type type in types)
        {
            string work = type.FullName ?? type.Name;
            LoadingObservationRuntime.Progress("Static constructors", work, completed, types.Count);
            // This is a safe scheduling boundary, not a forced extra frame. The
            // scheduler consumes cheap units together within its frame budget.
            yield return null;
            try { LoadingInvocationObservation.RunConstructor(type.TypeHandle); }
            catch (Exception error)
            {
                Log.Error("Error in static constructor of " + type?.ToString() + ": " + error);
            }
            LoadingObservationRuntime.Progress("Static constructors", work, ++completed, types.Count);
            yield return null;
        }
        // This invocation began without CallAll hooks. Constructors may add
        // future hooks; do not invoke CallAll again to notify them retrospectively.
        DeepProfiler.End();
        StaticConstructorOnStartupUtility.coreStaticAssetsLoaded = true;
    }

    private static IEnumerator NativeAtlases()
    {
        MethodInfo flush = AccessTools.Method(typeof(GlobalTextureAtlasManager), "<BakeStaticAtlases>g__FlushBatch|7_0");
        bool admitted = AtlasContract.Matches(BakeAll) && flush != null && AtlasContract.Matches(flush)
            && PublishedPatchGuard.TryCreate(BakeAll, AtlasBatchScheduling.Owner, out var guard, allPatchKinds: true)
            && guard!.AllowsOriginalContract();
        if (!admitted)
        {
            Status = "Native atlas grouping changed; ordinary atlas baking executes as one indivisible operation.";
            LoadingObservationRuntime.Progress("Atlas baking", Status, 0, null);
            GlobalTextureAtlasManager.BakeStaticAtlases();
            yield break;
        }
        // Retain the native outer grouping loop. Its actual FlushBatch helper
        // owns Insert/Bake/publication, including any helper hooks installed by
        // earlier units. The boxed by-ref closure is only active loading state.
        BuildingsDamageSectionLayerUtility.TryInsertIntoAtlas();
        MinifiedThing.TryInsertIntoAtlas();
        var queue = (Dictionary<TextureAtlasGroupKey, (List<Texture2D>, HashSet<Texture2D>)>)AccessTools.Field(typeof(GlobalTextureAtlasManager), "buildQueue").GetValue(null);
        Type closureType = flush!.GetParameters()[1].ParameterType.GetElementType()!;
        var pixelsField = AccessTools.Field(closureType, "pixels");
        var batchField = AccessTools.Field(closureType, "currentBatch");
        object closure = Activator.CreateInstance(closureType)!;
        int pixels = 0, completed = 0;
        var batch = new List<Texture2D>();
        batchField.SetValue(closure, batch);
        foreach (var group in queue)
        {
            foreach (Texture2D texture in group.Value.Item1)
            {
                int area = texture.width * texture.height;
                if (area + pixels > StaticTextureAtlas.MaxPixelsPerAtlas)
                {
                    pixelsField.SetValue(closure, pixels);
                    closure = FlushNative(flush, group.Key, closure);
                    pixels = (int)pixelsField.GetValue(closure);
                    batch = (List<Texture2D>)batchField.GetValue(closure);
                    LoadingObservationRuntime.Progress("Atlas baking", group.Key.ToString(), ++completed, null);
                    yield return null;
                }
                pixels += area;
                batch.Add(texture);
            }
            pixelsField.SetValue(closure, pixels);
            closure = FlushNative(flush, group.Key, closure);
            pixels = (int)pixelsField.GetValue(closure);
            batch = (List<Texture2D>)batchField.GetValue(closure);
            LoadingObservationRuntime.Progress("Atlas baking", group.Key.ToString(), ++completed, null);
            yield return null;
        }
        LoadingObservationRuntime.Progress("Atlas baking", "Native atlases complete", completed, completed);
    }

    private static object FlushNative(MethodInfo flush, TextureAtlasGroupKey key, object closure)
    {
        var token = LoadingObservationRuntime.Begin("Atlas baking", key.ToString(), "shared");
        Exception? failure = null;
        try
        {
            object[] arguments = { key, closure };
            try { flush.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
            return arguments[1];
        }
        catch (Exception error) { failure = error; throw; }
        finally { LoadingObservationRuntime.End(token, failure); }
    }
}
