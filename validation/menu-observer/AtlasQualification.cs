// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

// Functional-only private fixture observer. It never patches native atlas
// methods, changes global atlas membership, changes source assets, or measures
// duration. A bounded natural group is compared against an unpublished native
// bake with precisely the same borrowed sources, masks and request order.
internal static class AtlasQualification
{
    private static readonly Queue<StaticTextureAtlas> pending = new();
    private static string directory = "";
    private static bool started, finished, provenanceChecked, compressedTailChecked, privateBatchChecked, resizeChecked;
    private static int compared, passed, failed, restored;
    private const int MaximumGroups = 3;
    private const long MaximumProbePixels = 4L * 1024 * 1024;
    private static readonly BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo Textures = AccessTools.Field(typeof(StaticTextureAtlas), "textures");
    private static readonly FieldInfo Masks = AccessTools.Field(typeof(StaticTextureAtlas), "masks");
    private static readonly FieldInfo Atlases = AccessTools.Field(typeof(GlobalTextureAtlasManager), "staticTextureAtlases");
    private static readonly FieldInfo Tiles = AccessTools.Field(typeof(StaticTextureAtlas), "tiles");
    private static readonly Dictionary<Texture2D, HashSet<string>> providers = new(new TextureReferenceComparer());
    private sealed class TextureReferenceComparer : IEqualityComparer<Texture2D>
    {
        public bool Equals(Texture2D? left, Texture2D? right) => ReferenceEquals(left, right);
        public int GetHashCode(Texture2D value) => RuntimeHelpers.GetHashCode(value);
    }
    private static Type Runtime => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "WakeUp")
        .GetType("WakeUp.AtlasRuntime", true)!;

    internal static void Start(string output)
    {
        if (started) return;
        started = true; directory = output;
        try
        {
            var all = ((IEnumerable<StaticTextureAtlas>)Atlases.GetValue(null)).ToArray();
            var eligible = all.Where(a => a.ColorTexture != null && Sources(a).Count > 0
                && (long)a.ColorTexture.width * a.ColorTexture.height <= MaximumProbePixels).ToArray();
            // Natural selection is by actual runtime group and size, never a
            // package name or fixture-specific supplier identity.
            MethodInfo wasRestored = Runtime.GetMethod("WasRestored", AnyStatic)
                ?? throw new MissingMethodException("AtlasRuntime.WasRestored");
            foreach (var atlas in eligible.OrderByDescending(a => (bool)wasRestored.Invoke(null, new object[] { a }))
                .ThenByDescending(a => a.groupKey.hasMask)
                .ThenBy(a => (long)a.ColorTexture.width * a.ColorTexture.height)
                .GroupBy(a => a.groupKey).Select(g => g.First()).Take(MaximumGroups)) pending.Enqueue(atlas);
            var selectedInputs = new HashSet<Texture2D>(pending.SelectMany(a => Sources(a).Concat(MaskSources(a).Values)),
                new TextureReferenceComparer());
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            foreach (var texture in mod.GetContentHolder<Texture2D>().contentList.Values)
                if (texture != null && selectedInputs.Contains(texture))
                {
                    if (!providers.TryGetValue(texture, out var packages)) providers.Add(texture, packages = new HashSet<string>(StringComparer.Ordinal));
                    packages.Add(mod.PackageId);
                }
            Receipt("event", "start", "naturalGroups", all.Length, "eligibleGroups", eligible.Length,
                "selectedGroups", pending.Count, "maxGroupPixels", MaximumProbePixels,
                "gameModule", typeof(StaticTextureAtlas).Assembly.ManifestModule.ModuleVersionId,
                "productModule", Runtime.Assembly.ManifestModule.ModuleVersionId,
                "observerModule", typeof(AtlasQualification).Assembly.ManifestModule.ModuleVersionId);
            if (pending.Count == 0) { failed++; Receipt("event", "failure", "reason", "no-bounded-natural-atlas-group"); }
        }
        catch (Exception e) { failed++; Receipt("event", "failure", "reason", e.ToString()); }
    }

    internal static bool Advance()
    {
        if (!started || finished) return false;
        if (pending.Count > 0)
        {
            CompareNatural(pending.Dequeue());
            return true;
        }
        if (!provenanceChecked)
        {
            provenanceChecked = true;
            ProbeProvenance();
            return true;
        }
        if (!compressedTailChecked)
        {
            compressedTailChecked = true;
            ProbeCompressedTailMips();
            return true;
        }
        if (!privateBatchChecked)
        {
            privateBatchChecked = true;
            ProbePrivateBatchAndRollback();
            return true;
        }
        if (!resizeChecked)
        {
            resizeChecked = true;
            ProbeResizeBetweenYields();
            return true;
        }
        finished = true;
        Receipt("event", "complete", "compared", compared, "passed", passed, "failed", failed,
            "actualRestoredGroups", restored, "functionalOnly", true);
        return false;
    }

    private static List<Texture2D> Sources(StaticTextureAtlas atlas) => (List<Texture2D>)Textures.GetValue(atlas);
    private static Dictionary<Texture2D, Texture2D> MaskSources(StaticTextureAtlas atlas) => (Dictionary<Texture2D, Texture2D>)Masks.GetValue(atlas);

    private static void ProbePrivateBatchAndRollback()
    {
        var groups = new List<StaticTextureAtlas>();
        var ownedSources = new List<Texture2D>();
        IEnumerator? work = null, cancelledWork = null;
        try
        {
            TextureAtlasGroupKey key = ((IEnumerable<StaticTextureAtlas>)Atlases.GetValue(null)).First().groupKey;
            key.hasMask = false;
            Texture2D MakeSource(string name, Color32 color)
            {
                var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
                ownedSources.Add(texture); texture.name = name;
                var pixels = Enumerable.Repeat(color, 256 * 256).ToArray();
                pixels[0] = new Color32(0, 0, 0, 0);
                pixels[255] = new Color32(251, 131, 7, 53);
                pixels[128 * 256 + 127] = new Color32(11, 237, 101, 193);
                texture.SetPixels32(pixels); texture.Apply(false, false);
                texture.filterMode = FilterMode.Point;
                return texture;
            }
            Texture2D a = MakeSource("FixtureAtlasPrivateA", new Color32(211, 31, 53, 255));
            Texture2D b = MakeSource("FixtureAtlasPrivateB", new Color32(17, 67, 239, 127));
            Texture2D changed = MakeSource("FixtureAtlasPrivateChanged", new Color32(29, 223, 83, 255));
            StaticTextureAtlas Group()
            {
                var atlas = new StaticTextureAtlas(key); groups.Add(atlas);
                atlas.Insert(a); atlas.Insert(b); return atlas;
            }
            MethodInfo nativeBake = Runtime.GetMethod("ProbeNativeBake", AnyStatic)!;
            MethodInfo bakeOne = Runtime.GetMethod("BakeOne", AnyStatic)!;
            var reference = Group(); nativeBake.Invoke(null, new object[] { reference });
            string referencePixels = RenderHash(reference.ColorTexture), referenceDescriptor = Descriptor(reference.ColorTexture);
            int globalCount = ((ICollection<StaticTextureAtlas>)Atlases.GetValue(null)).Count;

            // This is a valid image/layout entry. Duplicating one *private*
            // input key forces the second tile insertion to fail after the
            // restored texture and first mesh have been allocated.
            Type pixelsType = Runtime.Assembly.GetType("WakeUp.AtlasPixels", true)!;
            object scope = pixelsType.GetMethod("BeginCapture", AnyStatic)!.Invoke(null, null);
            object image;
            try { image = AccessTools.Method(scope.GetType(), "Capture").Invoke(scope, new object[] { a }); }
            finally { ((IDisposable)scope).Dispose(); }
            Type storeType = Runtime.Assembly.GetType("WakeUp.AtlasCacheStore", true)!;
            Type entryType = Runtime.Assembly.GetType("WakeUp.AtlasCacheStore+Entry", true)!;
            object entry = Activator.CreateInstance(entryType, true)!;
            AccessTools.Field(entryType, "Color").SetValue(entry, image);
            AccessTools.Field(entryType, "Rects").SetValue(entry, new[] { 0f, 0f, .5f, 1f, .5f, 0f, .5f, 1f });
            bool entryValid = (bool)storeType.GetMethod("Valid", AnyStatic)!.Invoke(null, new[] { entry });
            if (!entryValid) throw new InvalidOperationException("observer-private-entry-must-be-valid");
            var rollback = Group();
            Sources(rollback)[1] = a;
            Texture2D placeholder = rollback.ColorTexture;
            object initialTiles = Tiles.GetValue(rollback), initialMasks = Masks.GetValue(rollback);
            bool committed = (bool)Runtime.GetMethod("RestoreGroup", AnyStatic)!.Invoke(null, new[] { (object)rollback, entry });
            bool rollbackIntact = !committed && ReferenceEquals(placeholder, rollback.ColorTexture) && placeholder != null
                && rollback.MaskTexture == null && ReferenceEquals(initialTiles, Tiles.GetValue(rollback))
                && ReferenceEquals(initialMasks, Masks.GetValue(rollback)) && ((IDictionary)initialTiles).Count == 0
                && ((IDictionary)initialMasks).Count == 0 && Sources(rollback).Count == 2
                && ReferenceEquals(Sources(rollback)[0], a) && ReferenceEquals(Sources(rollback)[1], a)
                && a != null && b != null;
            // Revert just the observer's duplicate-key injection, then ask the
            // ordinary native path to finish this same unpublished group.
            Sources(rollback)[1] = b!;
            nativeBake.Invoke(null, new object[] { rollback });
            bool fallback = RenderHash(rollback.ColorTexture) == referencePixels
                && Descriptor(rollback.ColorTexture) == referenceDescriptor
                && CompareTiles(rollback, reference, new[] { a!, b! }, out _)
                && ((IDictionary)Tiles.GetValue(rollback)).Count == 2;
            bool rollbackOk = rollbackIntact && fallback;
            if (rollbackOk) passed++; else failed++;
            Receipt("event", "private-restore-rollback", "passed", rollbackOk, "entryStructurallyValid", entryValid,
                "duplicateSourceTileKeyRejected", !committed, "placeholderTilesMasksAndBorrowedInputsPreserved", rollbackIntact,
                "ordinaryNativeFallbackMatchesReference", fallback,
                "faultInjection", "private duplicate key reverted before ordinary native fallback");

            var batched = Group();
            work = (IEnumerator)bakeOne.Invoke(null, new object[] { batched });
            bool firstYield = work.MoveNext();
            if (!firstYield) throw new InvalidOperationException("observer-private-batch-did-not-yield");
            object snapshot = SnapshotOf(work);
            var frozen = (IDictionary)AccessTools.Field(snapshot.GetType(), "copies").GetValue(snapshot);
            if (!frozen.Contains(a)) throw new InvalidOperationException("observer-first-source-not-frozen-at-yield");
            Texture2D firstCopy = (Texture2D)frozen[a];
            string beforeCpu = CpuHash(a), beforeGpu = RenderHash(a);
            bool initiallyExact = RenderHash(firstCopy) == beforeGpu;
            Texture2D[] frozenReferences = frozen.Values.Cast<Texture2D>().ToArray();
            GpuOnlyCopy(changed, a!, 0);
            bool gpuChanged = beforeGpu != RenderHash(a), cpuStale = beforeCpu == CpuHash(a);
            bool snapshotStable = RenderHash(firstCopy) == beforeGpu;
            int yields = 1;
            while (work.MoveNext()) if (++yields > 4096) throw new InvalidOperationException("observer-private-batch-progress-bound");
            (work as IDisposable)?.Dispose(); work = null;
            bool snapshotsFreed = frozenReferences.All(t => t == null) && frozen.Count == 0;
            bool batchPixels = RenderHash(batched.ColorTexture) == referencePixels;
            bool batchDescriptor = Descriptor(batched.ColorTexture) == referenceDescriptor;
            bool batchTiles = CompareTiles(batched, reference, new[] { a!, b! }, out _);
            bool batchOk = initiallyExact && gpuChanged && cpuStale && snapshotStable && snapshotsFreed
                && batchPixels && batchDescriptor && batchTiles && a != null && b != null;
            if (batchOk) passed++; else failed++;
            Receipt("event", "private-batch-input-freeze", "passed", batchOk, "actualYields", yields,
                "firstSourceFrozenAtFirstYield", initiallyExact, "borrowedGpuChangedBetweenYields", gpuChanged,
                "borrowedCpuStayedStale", cpuStale, "ownedSnapshotRetainedOriginalGpu", snapshotStable,
                "completedPixelsMatchPreMutationNative", batchPixels, "completedDescriptorMatchesNative", batchDescriptor,
                "completedTilesMatchNative", batchTiles, "completedSnapshotsDestroyed", snapshotsFreed);

            var abandoned = Group();
            cancelledWork = (IEnumerator)bakeOne.Invoke(null, new object[] { abandoned });
            bool cancellationYield = cancelledWork.MoveNext();
            if (!cancellationYield) throw new InvalidOperationException("observer-private-cancel-did-not-yield");
            object abandonedSnapshot = SnapshotOf(cancelledWork);
            var abandonedCopies = (IDictionary)AccessTools.Field(abandonedSnapshot.GetType(), "copies").GetValue(abandonedSnapshot);
            Texture2D[] abandonedReferences = abandonedCopies.Values.Cast<Texture2D>().ToArray();
            (cancelledWork as IDisposable)?.Dispose();
            bool cannotAdvance = !cancelledWork.MoveNext();
            (cancelledWork as IDisposable)?.Dispose(); cancelledWork = null;
            bool cancelFreed = abandonedReferences.Length > 0 && abandonedReferences.All(t => t == null) && abandonedCopies.Count == 0;
            bool privateOnly = ((ICollection<StaticTextureAtlas>)Atlases.GetValue(null)).Count == globalCount;
            bool cancelOk = cannotAdvance && cancelFreed && privateOnly && a != null && b != null;
            if (cancelOk) passed++; else failed++;
            Receipt("event", "private-batch-dispose", "passed", cancelOk, "yieldedBeforeDispose", cancellationYield,
                "capturedSnapshots", abandonedReferences.Length, "snapshotsDestroyed", cancelFreed,
                "disposedIteratorCannotAdvance", cannotAdvance, "borrowedSourcesSurvived", a != null && b != null,
                "globalAtlasMembershipUnchanged", privateOnly, "unpublishedAtlasCleanup", "observer caller owns Destroy");
        }
        catch (Exception e) { failed++; Receipt("event", "failure", "case", "private-batch-and-rollback", "reason", e.ToString()); }
        finally
        {
            (work as IDisposable)?.Dispose(); (cancelledWork as IDisposable)?.Dispose();
            foreach (StaticTextureAtlas atlas in groups) atlas.Destroy();
            foreach (Texture2D texture in ownedSources) if (texture != null) Object.DestroyImmediate(texture);
        }
    }

    private static void ProbeResizeBetweenYields()
    {
        var sources = new List<Texture2D>(); var groups = new List<StaticTextureAtlas>();
        IEnumerator? work = null; object? privateStore = null;
        FieldInfo storeField = AccessTools.Field(Runtime, "store"), finishedField = AccessTools.Field(Runtime, "finished");
        FieldInfo reuseField = AccessTools.Field(Runtime, "reuse");
        object priorStore = storeField.GetValue(null), priorFinished = finishedField.GetValue(null), priorReuse = reuseField.GetValue(null);
        try
        {
            int globalCount = ((ICollection<StaticTextureAtlas>)Atlases.GetValue(null)).Count;
            var key = ((IEnumerable<StaticTextureAtlas>)Atlases.GetValue(null)).First().groupKey; key.hasMask = false;
            for (int i = 0; i < 8; i++)
            {
                var source = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
                sources.Add(source); source.name = "FixtureAtlasResize" + i; Fill(source, i);
            }
            void Fill(Texture2D source, int seed)
            {
                var pixels = new Color32[source.width * source.height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)(i + seed * 23), (byte)(i / source.width + 47), (byte)(seed * 31 + 17), 255);
                source.SetPixels32(pixels); source.Apply(false, false); source.filterMode = FilterMode.Point;
            }
            StaticTextureAtlas Group()
            {
                var group = new StaticTextureAtlas(key); groups.Add(group);
                foreach (var source in sources) group.Insert(source);
                return group;
            }
            MethodInfo native = Runtime.GetMethod("ProbeNativeBake", AnyStatic)!, bake = Runtime.GetMethod("BakeOne", AnyStatic)!;
            MethodInfo identity = Runtime.GetMethod("Identity", AnyStatic)!;
            Type storeType = Runtime.Assembly.GetType("WakeUp.AtlasCacheStore", true)!;
            object OpenStore() => Activator.CreateInstance(storeType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object?[] { Path.Combine(directory, "atlas-resize-cache"), null, null, false, true }, CultureInfo.InvariantCulture)!;
            void FinishWork()
            {
                int steps = 0;
                while (work!.MoveNext()) if (++steps > 4096) throw new InvalidOperationException("resize-probe-progress-bound");
                ((IDisposable)work).Dispose(); work = null;
            }
            bool Matches(StaticTextureAtlas actual, StaticTextureAtlas expected) => RenderHash(actual.ColorTexture) == RenderHash(expected.ColorTexture)
                && Descriptor(actual.ColorTexture) == Descriptor(expected.ColorTexture) && CompareTiles(actual, expected, sources.ToArray(), out _);
            var originalNative = Group(); native.Invoke(null, new object[] { originalNative });
            var frozenBake = Group(); string oldKey = (string)identity.Invoke(null, new object[] { frozenBake });
            privateStore = OpenStore(); storeField.SetValue(null, privateStore); finishedField.SetValue(null, false); reuseField.SetValue(null, true);
            Texture2D placeholder = frozenBake.ColorTexture;
            work = (IEnumerator)bake.Invoke(null, new object[] { frozenBake });
            if (!work.MoveNext()) throw new InvalidOperationException("resize-probe-missing-input-yield");
            var copies = (IDictionary)AccessTools.Field(SnapshotOf(work).GetType(), "copies").GetValue(SnapshotOf(work));
            bool beforeLayout = ReferenceEquals(placeholder, frozenBake.ColorTexture) && copies.Contains(sources[0]);
            if (!beforeLayout) throw new InvalidOperationException("resize-probe-must-resize-after-capture-before-layout");
            Texture2D copy = (Texture2D)copies[sources[0]];
            int instance = sources[0].GetInstanceID();
            // Resize only an observer-created source. Its object identity stays
            // fixed, while dimensions, pixels and the native packing order change.
            if (!sources[0].Reinitialize(128, 384, TextureFormat.RGBA32, false)) throw new InvalidOperationException("resize-probe-reinitialize");
            Fill(sources[0], 29);
            bool frozenGeometry = copy.width == 256 && copy.height == 256;
            FinishWork();
            bool frozenOutput = Matches(frozenBake, originalNative);
            bool borrowedKeys = Sources(frozenBake).SequenceEqual(sources) && sources[0].GetInstanceID() == instance;
            bool oldPublished = (long)AccessTools.Field(storeType, "Writes").GetValue(privateStore) == 1;
            AccessTools.Method(storeType, "Complete").Invoke(privateStore, null);
            // Model another startup with another store session. Complete is the
            // publication boundary, not a mid-session checkpoint for more bakes.
            ((IDisposable)privateStore).Dispose(); privateStore = OpenStore(); storeField.SetValue(null, privateStore);
            var changedNative = Group(); native.Invoke(null, new object[] { changedNative });
            var changedBake = Group(); string newKey = (string)identity.Invoke(null, new object[] { changedBake });
            work = (IEnumerator)bake.Invoke(null, new object[] { changedBake }); FinishWork();
            bool changedMiss = oldKey != newKey && (long)AccessTools.Field(storeType, "Hits").GetValue(privateStore) == 0
                && (long)AccessTools.Field(storeType, "Writes").GetValue(privateStore) == 1;
            bool changedOutput = Matches(changedBake, changedNative);
            AccessTools.Method(storeType, "Complete").Invoke(privateStore, null);
            ((IDisposable)privateStore).Dispose(); privateStore = OpenStore(); storeField.SetValue(null, privateStore);
            var warm = Group(); work = (IEnumerator)bake.Invoke(null, new object[] { warm }); FinishWork();
            bool warmHit = (long)AccessTools.Field(storeType, "Hits").GetValue(privateStore) == 1
                && (long)AccessTools.Field(storeType, "Writes").GetValue(privateStore) == 0;
            bool warmOutput = Matches(warm, changedNative);
            bool privateOnly = ((ICollection<StaticTextureAtlas>)Atlases.GetValue(null)).Count == globalCount && sources.All(t => t != null);
            bool ok = beforeLayout && frozenGeometry && frozenOutput && borrowedKeys && oldPublished && changedMiss && changedOutput && warmHit && warmOutput && privateOnly;
            if (ok) passed++; else failed++;
            Receipt("event", "resize-between-input-yield-and-layout", "passed", ok,
                "resizedBeforeLayout", beforeLayout, "frozenGeometryUnchanged", frozenGeometry,
                "oldSnapshotPixelsDescriptorAndMeshesMatchNative", frozenOutput, "borrowedObjectsRemainTileKeys", borrowedKeys,
                "oldSnapshotEntryPublished", oldPublished, "changedSourceMissedOldEntryAndPublishedNew", changedMiss,
                "changedSourcePixelsDescriptorAndMeshesMatchNative", changedOutput, "reopenedStoreWarmHit", warmHit,
                "warmPixelsDescriptorAndMeshesMatchNative", warmOutput, "borrowedSourcesAndGlobalMembershipPreserved", privateOnly,
                "oldKey", oldKey, "changedKey", newKey);
        }
        catch (Exception error) { failed++; Receipt("event", "failure", "case", "resize-between-yields", "reason", error.ToString()); }
        finally
        {
            (work as IDisposable)?.Dispose(); (privateStore as IDisposable)?.Dispose();
            storeField.SetValue(null, priorStore); finishedField.SetValue(null, priorFinished); reuseField.SetValue(null, priorReuse);
            object restoredGroups = AccessTools.Field(Runtime, "Restored").GetValue(null);
            foreach (var group in groups) AccessTools.Method(restoredGroups.GetType(), "Remove").Invoke(restoredGroups, new object[] { group });
            foreach (var group in groups) group.Destroy();
            foreach (var source in sources) if (source != null) Object.DestroyImmediate(source);
        }
    }

    private static object SnapshotOf(IEnumerator work) => work.GetType()
        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => f.FieldType.FullName == "WakeUp.AtlasInputSnapshot")
        .Select(f => f.GetValue(work)).FirstOrDefault(v => v != null)
        ?? throw new MissingFieldException("observer-batch-owned-input-snapshot");

    private static void ProbeCompressedTailMips()
    {
        Texture2D? source = null;
        try
        {
            Type provenance = Runtime.Assembly.GetType("WakeUp.AtlasProvenance", true)!;
            MethodInfo revision = provenance.GetMethod("Revision", AnyStatic)
                ?? throw new MissingMethodException("AtlasProvenance.Revision");
            source = new Texture2D(8, 8, TextureFormat.DXT5, 4, true, true);
            source.name = "FixtureAtlasCompressedTailMips";
            source.filterMode = FilterMode.Trilinear; source.wrapMode = TextureWrapMode.Mirror; source.mipMapBias = 0.375f;
            if (source.format != TextureFormat.DXT5 || source.mipmapCount != 4 || source.GetRawTextureData().Length != 112)
                throw new InvalidDataException("observer-dxt5-four-mip-layout");
            // Four BC3 blocks for 8x8, then one block each for 4x4, 2x2,
            // and 1x1. All alpha selectors and color selectors choose endpoint
            // zero: opaque red. The test changes only the selected tail block.
            var originalBytes = new byte[112];
            for (int block = 0; block < originalBytes.Length; block += 16)
            {
                originalBytes[block] = 255;
                originalBytes[block + 8] = 0; originalBytes[block + 9] = 0xf8;
                originalBytes[block + 10] = 0xe0; originalBytes[block + 11] = 7;
            }
            int instance = source.GetInstanceID();
            bool untouched = true;
            string Observe()
            {
                string beforeCpu = CpuHash(source), beforeDescriptor = Descriptor(source);
                string result = (string)revision.Invoke(null, new object[] { source });
                untouched &= source != null && source.GetInstanceID() == instance
                    && beforeCpu == CpuHash(source) && beforeDescriptor == Descriptor(source);
                return result;
            }
            void Upload(byte[] bytes) { source.LoadRawTextureData(bytes); source.Apply(false, false); }
            Upload(originalBytes);
            string original = Observe(), same = Observe();
            byte[] mip2 = (byte[])originalBytes.Clone();
            mip2[80 + 8] = 0x1f; mip2[80 + 9] = 0; // 2x2 blue, other mips unchanged.
            Upload(mip2);
            string changed2 = Observe(), same2 = Observe();
            byte[] mip3 = (byte[])originalBytes.Clone();
            mip3[96 + 8] = 0xe0; mip3[96 + 9] = 0xff; // 1x1 yellow, other mips unchanged.
            Upload(mip3);
            string changed3 = Observe(), same3 = Observe();
            Upload(originalBytes);
            string restoredOriginal = Observe();
            bool stable = original == same && changed2 == same2 && changed3 == same3;
            bool detects2 = original != changed2, detects3 = original != changed3;
            bool sourceRestored = original == restoredOriginal && originalBytes.SequenceEqual(source.GetRawTextureData());
            bool ok = stable && detects2 && detects3 && untouched && sourceRestored;
            if (ok) passed++; else failed++;
            Receipt("event", "compressed-tail-mip-provenance", "passed", ok, "format", "DXT5", "width", 8,
                "height", 8, "mips", 4, "nativeBytes", 112, "unchangedRevisionStable", stable,
                "only2x2MipChangedInvalidates", detects2, "only1x1MipChangedInvalidates", detects3,
                "revisionPreservesCpuDescriptorAndSource", untouched, "originalBytesAndRevisionRestored", sourceRestored,
                "originalRevision", original, "only2x2ChangedRevision", changed2, "only1x1ChangedRevision", changed3);
        }
        catch (Exception e) { failed++; Receipt("event", "failure", "case", "compressed-tail-mip-provenance", "reason", e.ToString()); }
        finally { if (source != null) Object.DestroyImmediate(source); }
    }

    private static void ProbeProvenance()
    {
        Texture2D? source = null, replacement = null, restoredImage = null;
        try
        {
            Type provenance = Runtime.Assembly.GetType("WakeUp.AtlasProvenance", true)!;
            MethodInfo revision = provenance.GetMethod("Revision", AnyStatic)
                ?? throw new MissingMethodException("AtlasProvenance.Revision");
            string Revision(Texture2D t) => (string)revision.Invoke(null, new object[] { t });
            source = new Texture2D(32, 32, TextureFormat.RGBA32, true, true);
            var pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = new Color32((byte)(x * 7), (byte)(y * 7), (byte)((x ^ y) * 7),
                    (byte)(x == 0 || y == 0 || x == 31 || y == 31 ? 0 : 173));
            source.SetPixels32(pixels); source.Apply(true, false);
            string cpu = CpuHash(source), original = Revision(source), unchanged = Revision(source);
            replacement = new Texture2D(32, 32, TextureFormat.RGBA32, false, true);
            pixels[15 * 32 + 16] = new Color32(255, 13, 71, 31);
            replacement.SetPixels32(pixels); replacement.Apply(false, false);
            GpuOnlyCopy(replacement, source, 0);
            string baseChanged = Revision(source);
            bool staleCpuPreserved = cpu == CpuHash(source);
            GpuOnlyCopy(replacement, source, 1);
            string mipChanged = Revision(source);
            bool allMipIdentity = mipChanged != baseChanged;
            bool stable = unchanged == original, baseIdentity = baseChanged != original;
            Type pixelsType = Runtime.Assembly.GetType("WakeUp.AtlasPixels", true)!;
            object scope = pixelsType.GetMethod("BeginCapture", AnyStatic)!.Invoke(null, null);
            try
            {
                object image = AccessTools.Method(scope.GetType(), "Capture").Invoke(scope, new object[] { source });
                restoredImage = (Texture2D)pixelsType.GetMethod("Restore", AnyStatic)!.Invoke(null, new[] { image });
            }
            finally { ((IDisposable)scope).Dispose(); }
            bool gpuRoundtrip = RenderHash(source) == RenderHash(restoredImage);
            bool cpuRoundtrip = CpuHash(source) == CpuHash(restoredImage);
            bool descriptorRoundtrip = Descriptor(source) == Descriptor(restoredImage);
            bool groupIdentity = ProbeGroupIdentity(source, replacement);
            bool ok = stable && baseIdentity && allMipIdentity && staleCpuPreserved && cpu == CpuHash(source)
                && gpuRoundtrip && cpuRoundtrip && descriptorRoundtrip && groupIdentity;
            if (ok) passed++; else failed++;
            Receipt("event", "generated-gpu-revision", "passed", ok, "sameSizeAndFormat", true,
                "unchangedRevisionStable", stable, "gpuOnlyBaseChangeInvalidates", baseIdentity,
                "gpuOnlyNonBaseMipChangeInvalidates", allMipIdentity, "cpuBytesRemainUnchanged", staleCpuPreserved,
                "divergentGpuRoundtrip", gpuRoundtrip, "divergentCpuRoundtrip", cpuRoundtrip,
                "roundtripDescriptor", descriptorRoundtrip,
                "groupIdentityCases", groupIdentity,
                "sourceStillAlive", source != null, "originalRevision", original,
                "baseChangedRevision", baseChanged, "mipChangedRevision", mipChanged,
                "pixelCase", "columns-rows-edge-alpha-and-one-central-pixel");
        }
        catch (Exception e) { failed++; Receipt("event", "failure", "case", "generated-gpu-revision", "reason", e.ToString()); }
        finally
        {
            if (replacement != null) Object.DestroyImmediate(replacement);
            if (restoredImage != null) Object.DestroyImmediate(restoredImage);
            if (source != null) Object.DestroyImmediate(source);
        }
    }

    private static bool ProbeGroupIdentity(Texture2D a, Texture2D b)
    {
        var owned = new List<StaticTextureAtlas>();
        FilterMode filter = a.filterMode;
        try
        {
            var natural = ((IEnumerable<StaticTextureAtlas>)Atlases.GetValue(null)).First();
            TextureAtlasGroupKey key = natural.groupKey;
            key.hasMask = true;
            StaticTextureAtlas Group(Texture2D first, Texture2D firstMask, Texture2D second, Texture2D secondMask)
            {
                var group = new StaticTextureAtlas(key); owned.Add(group);
                group.Insert(first, firstMask); group.Insert(second, secondMask);
                return group;
            }
            MethodInfo identity = Runtime.GetMethod("Identity", AnyStatic)
                ?? throw new MissingMethodException("AtlasRuntime.Identity");
            string Id(StaticTextureAtlas group) => (string)identity.Invoke(null, new object[] { group });
            var baseline = Group(a, a, b, b);
            string original = Id(baseline);
            bool stable = original == Id(baseline);
            bool order = original != Id(Group(b, b, a, a));
            bool mask = original != Id(Group(a, b, b, a));
            a.filterMode = filter == FilterMode.Point ? FilterMode.Bilinear : FilterMode.Point;
            bool sampler = original != Id(baseline);
            a.filterMode = filter;
            bool restoredSetting = original == Id(baseline);

            // A malformed descriptor must be rejected before altering the
            // placeholder, source list or global atlas membership.
            Type entryType = Runtime.Assembly.GetType("WakeUp.AtlasCacheStore+Entry", true)!;
            object malformed = Activator.CreateInstance(entryType, true)!;
            Texture2D placeholder = baseline.ColorTexture;
            bool admitted = (bool)Runtime.GetMethod("RestoreGroup", AnyStatic)!.Invoke(null, new[] { (object)baseline, malformed });
            bool malformedRejected = !admitted && ReferenceEquals(placeholder, baseline.ColorTexture)
                && baseline.MaskTexture == null && Sources(baseline).Count == 2
                && !baseline.TryGetTile(a, out _) && a != null && b != null;
            bool ok = stable && order && mask && sampler && restoredSetting && malformedRejected;
            Receipt("event", "group-identity-cases", "passed", ok, "unchangedGroupStable", stable,
                "orderedSourcesInvalidate", order, "maskAssociationInvalidates", mask,
                "sourceSamplingInvalidates", sampler, "sourceSamplingRestored", restoredSetting,
                "malformedEntryRejectedBeforeMutation", malformedRejected);
            return ok;
        }
        finally
        {
            if (a != null) a.filterMode = filter;
            foreach (StaticTextureAtlas group in owned) group.Destroy();
        }
    }

    private static void GpuOnlyCopy(Texture2D from, Texture2D to, int mip)
    {
        int width = Math.Max(1, to.width >> mip), height = Math.Max(1, to.height >> mip);
        RenderTexture previous = RenderTexture.active;
        RenderTexture? rendered = null;
        try
        {
            rendered = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(from, rendered);
            Graphics.CopyTexture(rendered, 0, 0, to, 0, mip);
        }
        finally { RenderTexture.active = previous; if (rendered != null) RenderTexture.ReleaseTemporary(rendered); }
    }

    private static void CompareNatural(StaticTextureAtlas live)
    {
        StaticTextureAtlas? native = null;
        string group = live.groupKey.ToString();
        compared++;
        try
        {
            Texture2D[] sources = Sources(live).ToArray();
            var masks = new Dictionary<Texture2D, Texture2D>(MaskSources(live));
            Texture2D[] borrowed = sources.Concat(masks.Values).Distinct().ToArray();
            var providerCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int providerKnown = 0;
            foreach (Texture2D input in borrowed)
                if (providers.TryGetValue(input, out var packages))
                {
                    providerKnown++;
                    foreach (string package in packages)
                    { providerCounts.TryGetValue(package, out int count); providerCounts[package] = count + 1; }
                }
            int[] borrowedIds = borrowed.Select(t => t.GetInstanceID()).ToArray();
            string beforeColorCpu = CpuHash(live.ColorTexture), beforeMaskCpu = CpuHash(live.MaskTexture);
            int originalGlobalCount = ((ICollection<StaticTextureAtlas>)Atlases.GetValue(null)).Count;
            MethodInfo wasRestored = Runtime.GetMethod("WasRestored", AnyStatic)
                ?? throw new MissingMethodException("AtlasRuntime.WasRestored");
            bool warm = (bool)wasRestored.Invoke(null, new object[] { live });
            if (warm) restored++;

            native = new StaticTextureAtlas(live.groupKey);
            foreach (Texture2D source in sources)
                native.Insert(source, masks.TryGetValue(source, out var mask) ? mask : null);
            MethodInfo bake = Runtime.GetMethod("ProbeNativeBake", AnyStatic)
                ?? throw new MissingMethodException("AtlasRuntime.ProbeNativeBake");
            bake.Invoke(null, new object[] { native });

            bool colorDescriptor = Descriptor(live.ColorTexture) == Descriptor(native.ColorTexture);
            bool maskDescriptor = Descriptor(live.MaskTexture) == Descriptor(native.MaskTexture);
            string color = RenderHash(live.ColorTexture), nativeColor = RenderHash(native.ColorTexture);
            string maskPixels = RenderHash(live.MaskTexture), nativeMask = RenderHash(native.MaskTexture);
            bool colorPixels = color == nativeColor, maskEqual = maskPixels == nativeMask;
            bool meshes = CompareTiles(live, native, sources, out string meshHash);
            bool privateOnly = ((ICollection<StaticTextureAtlas>)Atlases.GetValue(null)).Count == originalGlobalCount;
            bool cpuUnchanged = beforeColorCpu == CpuHash(live.ColorTexture) && beforeMaskCpu == CpuHash(live.MaskTexture);
            string orderedSources = Hash(Encoding.UTF8.GetBytes(string.Join("\n", sources.Select((t, i) =>
                i.ToString(CultureInfo.InvariantCulture) + ":" + t.name + ":" + Descriptor(t)
                + ":mask=" + (masks.TryGetValue(t, out var m) ? m.name + ":" + Descriptor(m) : "none")))));

            // Destroy() frees native's owned outputs/meshes and clears its own
            // lists. This must leave all borrowed live source/mask objects alive.
            native.Destroy(); native = null;
            bool survived = borrowed.Select((t, i) => t != null && t.GetInstanceID() == borrowedIds[i]).All(v => v);
            bool ok = colorDescriptor && maskDescriptor && colorPixels && maskEqual && meshes && privateOnly && cpuUnchanged && survived;
            if (ok) passed++; else failed++;
            Receipt("event", "natural-group", "group", group, "textures", sources.Length, "maskAssociations", masks.Count,
                "sourceProviderPackageIds", string.Join(",", providerCounts.Keys),
                "sourceProviderTextureCounts", string.Join(",", providerCounts.Select(p => p.Key + "=" + p.Value)),
                "sourceTexturesWithProvider", providerKnown, "sourceTexturesWithoutProvider", borrowed.Length - providerKnown,
                "providerMatching", "reference-equality-in-loaded-mod-Texture2D-content-holders",
                "actualRestore", warm, "passed", ok, "colorDescriptorEqual", colorDescriptor, "maskDescriptorEqual", maskDescriptor,
                "colorAllMipRenderEqual", colorPixels, "maskAllMipRenderEqual", maskEqual, "orderedMeshesEqual", meshes,
                "privateNativeGroup", privateOnly, "borrowedSourcesSurvived", survived, "liveCpuUnchanged", cpuUnchanged,
                "colorDescriptor", Descriptor(live.ColorTexture), "maskDescriptor", Descriptor(live.MaskTexture),
                "colorAllMipRenderSha256", color, "nativeColorAllMipRenderSha256", nativeColor,
                "maskAllMipRenderSha256", maskPixels, "nativeMaskAllMipRenderSha256", nativeMask,
                "orderedSourcesSha256", orderedSources, "orderedMeshSha256", meshHash,
                "colorCpuSha256", beforeColorCpu, "maskCpuSha256", beforeMaskCpu,
                "cpuComparisonScope", "live-before-after; compare captured cold and warm receipts for persistence");
        }
        catch (Exception e) { failed++; Receipt("event", "failure", "group", group, "reason", e.ToString()); }
        finally { if (native != null) native.Destroy(); }
    }

    private static string Descriptor(Texture2D? t) => t == null ? "none" : string.Join("/", new object[]
    {
        t.width, t.height, t.format, (int)t.graphicsFormat, t.mipmapCount, t.isReadable, t.filterMode,
        t.wrapModeU, t.wrapModeV, t.wrapModeW, t.anisoLevel, t.mipMapBias.ToString("R", CultureInfo.InvariantCulture),
        t.minimumMipmapLevel, t.requestedMipmapLevel
    });
    private static string CpuHash(Texture2D? t) => t == null ? "none" : !t.isReadable ? "unreadable" : Hash(t.GetRawTextureData());

    private static bool CompareTiles(StaticTextureAtlas live, StaticTextureAtlas native, Texture2D[] sources, out string digest)
    {
        using var left = new MemoryStream(); using var lw = new BinaryWriter(left);
        using var right = new MemoryStream(); using var rw = new BinaryWriter(right);
        foreach (Texture2D source in sources)
        {
            if (!live.TryGetTile(source, out var l) || !native.TryGetTile(source, out var r)
                || !ReferenceEquals(l.atlas, live) || !ReferenceEquals(r.atlas, native))
            { digest = "missing-or-wrong-owner"; return false; }
            WriteTile(lw, l); WriteTile(rw, r);
        }
        lw.Flush(); rw.Flush();
        byte[] a = left.ToArray(), b = right.ToArray(); digest = Hash(a);
        return a.SequenceEqual(b);
    }

    private static void WriteTile(BinaryWriter writer, StaticTextureAtlasTile tile)
    {
        Rect r = tile.uvRect;
        writer.Write(r.x); writer.Write(r.y); writer.Write(r.width); writer.Write(r.height);
        Mesh mesh = tile.mesh;
        writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount);
        foreach (Vector3 v in mesh.vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
        writer.Write(mesh.uv.Length);
        foreach (Vector2 uv in mesh.uv) { writer.Write(uv.x); writer.Write(uv.y); }
        writer.Write(mesh.normals.Length);
        foreach (Vector3 n in mesh.normals) { writer.Write(n.x); writer.Write(n.y); writer.Write(n.z); }
        writer.Write(mesh.tangents.Length);
        foreach (Vector4 t in mesh.tangents) { writer.Write(t.x); writer.Write(t.y); writer.Write(t.z); writer.Write(t.w); }
        writer.Write(mesh.colors32.Length);
        foreach (Color32 c in mesh.colors32) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            writer.Write((int)mesh.GetTopology(i));
            int[] indices = mesh.GetIndices(i); writer.Write(indices.Length);
            foreach (int index in indices) writer.Write(index);
        }
        Vector3 center = mesh.bounds.center, extents = mesh.bounds.extents;
        writer.Write(center.x); writer.Write(center.y); writer.Write(center.z);
        writer.Write(extents.x); writer.Write(extents.y); writer.Write(extents.z);
    }

    // Read every actual mip without changing borrowed texture sampling fields.
    // Copy the mip into a private same-format texture; its point-sampled linear
    // float render preserves fractional compressed channels and alpha. Read in
    // bounded tiles to keep the validation render/readback allocation small.
    private static string RenderHash(Texture2D? texture)
    {
        if (texture == null) return "none";
        RenderTexture previous = RenderTexture.active;
        using var hash = SHA256.Create();
        try
        {
            for (int mip = 0; mip < texture.mipmapCount; mip++)
            {
                int width = Math.Max(1, texture.width >> mip), height = Math.Max(1, texture.height >> mip);
                Texture2D? copy = null;
                try
                {
                    copy = new Texture2D(width, height, texture.format, 1,
                        !GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat), true);
                    if (copy.graphicsFormat != texture.graphicsFormat || copy.format != texture.format || copy.mipmapCount != 1)
                        throw new InvalidOperationException("atlas-observer-private-mip-format");
                    Graphics.CopyTexture(texture, 0, mip, copy, 0, 0);
                    copy.filterMode = FilterMode.Point; copy.wrapMode = TextureWrapMode.Clamp; copy.anisoLevel = 0; copy.mipMapBias = 0;
                    for (int y = 0; y < height; y += 256)
                    for (int x = 0; x < width; x += 256)
                    {
                        int w = Math.Min(256, width - x), h = Math.Min(256, height - y);
                        RenderTexture? target = null; Texture2D? readable = null;
                        try
                        {
                            target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                            Graphics.Blit(copy, target, new Vector2(w / (float)width, h / (float)height),
                                new Vector2(x / (float)width, y / (float)height));
                            RenderTexture.active = target;
                            readable = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
                            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                            byte[] bytes = readable.GetRawTextureData();
                            hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                        }
                        finally
                        {
                            RenderTexture.active = previous;
                            if (readable != null) Object.DestroyImmediate(readable);
                            if (target != null) RenderTexture.ReleaseTemporary(target);
                        }
                    }
                }
                finally { if (copy != null) Object.DestroyImmediate(copy); }
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Hex(hash.Hash!);
        }
        finally { RenderTexture.active = previous; }
    }

    private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return Hex(sha.ComputeHash(bytes)); }
    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    private static string Quote(object? value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture)!
        .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    private static void Receipt(params object?[] pairs)
    {
        var fields = new List<string>();
        for (int i = 0; i < pairs.Length; i += 2)
        {
            object? value = pairs[i + 1];
            fields.Add(Quote(pairs[i]) + ":" + (value == null ? "null" : value is bool b ? b.ToString().ToLowerInvariant()
                : value is int || value is long ? Convert.ToString(value, CultureInfo.InvariantCulture) : Quote(value)));
        }
        File.AppendAllText(Path.Combine(directory, "atlas-qualification.jsonl"), "{" + string.Join(",", fields) + "}\n");
    }
}
