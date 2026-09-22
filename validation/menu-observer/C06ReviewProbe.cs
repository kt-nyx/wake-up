// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

internal static partial class C05TextureProbe
{
    private static string callbackTrigger = "", callbackDestination = "";
    private static byte[] callbackReplacement = Array.Empty<byte>();
    private static int callbackWrites;
    private static bool rawCallbackArmed;
    private static bool callbackQuiescent;
    private static void CheckCallbackQuiescence()
    {
        object session = AccessTools.Field(Product("PngRuntime"), "textureReadSession").GetValue(null)
            ?? throw new InvalidDataException("Callback has no texture session");
        object queue = Read(session, "queue") ?? throw new InvalidDataException("Callback has no queue");
        if (!(bool)Read(queue, "IsSuspended")! || (int)Read(queue, "ActiveWorkers")! != 0 || (int)Read(queue, "OpenStreams")! != 0)
            throw new InvalidDataException("Callback crossed active workers or source handles");
        object contexts = Read(session, "readContexts")!;
        foreach (object context in (System.Collections.IEnumerable)Read(contexts, "Values")!)
            if ((int)Read(Read(context, "handles")!, "Count")! != 0)
                throw new InvalidDataException("Callback crossed grouped-read handles");
        callbackQuiescent = true;
    }
    private static byte[] ReadRawWithFollowingRewrite(Texture2D texture)
    {
        if (rawCallbackArmed && callbackWrites == 0)
        {
            CheckCallbackQuiescence();
            DateTime timestamp = File.GetLastWriteTimeUtc(callbackDestination);
            if (new FileInfo(callbackDestination).Length != callbackReplacement.Length)
                throw new InvalidDataException("Same-size callback source fixture changed");
            File.WriteAllBytes(callbackDestination, callbackReplacement);
            File.SetLastWriteTimeUtc(callbackDestination, timestamp);
            callbackWrites++;
        }
        return texture.GetRawTextureData<byte>().ToArray();
    }
    private static IEnumerable<CodeInstruction> ReplaceRawCapture(IEnumerable<CodeInstruction> _)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(C05TextureProbe), nameof(ReadRawWithFollowingRewrite)));
        yield return new CodeInstruction(OpCodes.Ret);
    }
    private static void RewriteFollowingPng(object __0)
    {
        if (!string.Equals((string)Read(__0, "FullPath")!, callbackTrigger, StringComparison.OrdinalIgnoreCase)) return;
        File.WriteAllBytes(callbackDestination, callbackReplacement);
        callbackWrites++;
    }

    private static void CheckHolderCallbacks()
    {
        string root = Path.Combine(directory, "C06HolderCallback");
        string staging = Path.Combine(root, "source-variants");
        Directory.CreateDirectory(staging);
        C06PngSources.Stage(staging);
        byte[] original = File.ReadAllBytes(Path.Combine(staging, "c06-rgba8.png"));
        C06PngSources.Stage(staging, true);
        callbackReplacement = File.ReadAllBytes(Path.Combine(staging, "c06-rgba8.png"));
        var ctor = typeof(LoadedContentItem<Texture2D>).GetConstructors().Single(c => c.GetParameters().Length == 3);
        var rawCapture = typeof(Texture2D).GetMethods().Single(m => m.Name == "GetRawTextureData" && !m.IsGenericMethod && m.GetParameters().Length == 0);
        var patch = new Harmony("fixture.c06.holder-callback");
        var startupStore = AccessTools.Field(Product("PreparedTextureRuntime"), "startupStore");
        object? previousStartupStore = startupStore.GetValue(null);
        var state = new[] { "finished", "firstBuildReloadAllowed", "cache", "calls", "jpegCalls", "errors", "sourceBytes",
                "firstBuildPngUploads", "firstBuildJpegUploads", "pngTicks", "captureTicks", "nativeFallbacks" }
            .Select(n => AccessTools.Field(Product("PngRuntime"), n)).ToDictionary(f => f, f => f.GetValue(null));
        try
        {
            AccessTools.Field(Product("PngRuntime"), "finished").SetValue(null, false);
            AccessTools.Field(Product("PngRuntime"), "firstBuildReloadAllowed").SetValue(null, true);
            foreach (string mode in new[] { "native", "patched-before-queue", "patched-after-first-item", "capture-after-first-item" })
            {
                string folder = Path.Combine(root, mode), textures = Path.Combine(folder, "Textures");
                Directory.CreateDirectory(textures);
                foreach (string name in new[] { "a.png", "b.png", "c.png", "d.png" }) File.WriteAllBytes(Path.Combine(textures, name), original);
                var mod = new ModContentPack(new DirectoryInfo(folder), "fixture.c06." + mode, "fixture.c06." + mode, 0, "C06 callback probe", false);
                var selected = ((IEnumerable<KeyValuePair<string, FileInfo>>)Call("PngRuntime", "SelectedFiles", mod)!).ToArray();
                if (selected.Length != 4) throw new InvalidDataException("Callback probe selection changed");
                bool late = mode.EndsWith("after-first-item", StringComparison.Ordinal);
                bool captureCallback = mode.StartsWith("capture-", StringComparison.Ordinal);
                MethodBase target = captureCallback ? rawCapture : ctor;
                string prefix = nameof(RewriteFollowingPng);
                int trigger = late ? 1 : 0, changed = trigger + 1;
                callbackTrigger = selected[trigger].Value.FullName;
                callbackDestination = selected[changed].Value.FullName;
                callbackWrites = 0; callbackQuiescent = false;
                bool unchangedReused = false;
                var uploadCounter = AccessTools.Field(Product("PngRuntime"), "firstBuildPngUploads");
                long uploadsBefore = (long)uploadCounter.GetValue(null)!;
                object store = Activator.CreateInstance(Product("PreparedTextureStore"), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object?[] { Path.Combine(folder, "store"), null, null, false }, null)!;
                AccessTools.Field(Product("PngRuntime"), "cache").SetValue(null, store);
                // Both production entry points share this owner. Keep the probe's
                // private store consistent so queue admission does not reopen the
                // normal post-startup store and retain its exclusive ownership.
                startupStore.SetValue(null, store);
                var loaded = new List<Pair<string, LoadedContentItem<Texture2D>>>();
                Texture2D? expected = null, restored = null;
                try
                {
                    if (!late) patch.Patch(target, prefix: new HarmonyMethod(typeof(C05TextureProbe), prefix));
                    rawCallbackArmed = captureCallback;
                    var sequence = mode == "native" ? ModContentLoader<Texture2D>.LoadAllForMod(mod)
                        : (IEnumerable<Pair<string, LoadedContentItem<Texture2D>>>)Call("PngRuntime", "Enumerate", mod)!;
                    using (var iterator = sequence.GetEnumerator())
                    {
                        while (iterator.MoveNext())
                        {
                            loaded.Add(iterator.Current);
                            if (captureCallback && loaded.Count == 4)
                            {
                                object session = AccessTools.Field(Product("PngRuntime"), "textureReadSession").GetValue(null)!;
                                object queue = Read(session, "queue")!;
                                unchangedReused = (long)Read(queue, "Revalidated")! >= 2 && (long)Read(queue, "RejectedRetained")! == 1;
                            }
                            if (late && loaded.Count == 1)
                            {
                                if (captureCallback) patch.Patch(target, transpiler: new HarmonyMethod(typeof(C05TextureProbe), nameof(ReplaceRawCapture)));
                                else patch.Patch(target, prefix: new HarmonyMethod(typeof(C05TextureProbe), prefix));
                            }
                        }
                    }
                    rawCallbackArmed = false;
                    if (callbackWrites != 1 || !File.ReadAllBytes(callbackDestination).SequenceEqual(callbackReplacement))
                        throw new InvalidDataException("Holder callback failed to replace following source");
                    object native = Call("PngRuntime", "CapturePrepared", new FileInfo(callbackDestination), callbackReplacement, new Func<int, bool>(_ => true))!;
                    expected = (Texture2D)Call("PngRuntime", "Restore", native)!;
                    Texture2D actual = loaded.Single(p => p.First == selected[changed].Key).Second.contentItem;
                    CompareTextures(expected, actual);
                    for (int mip = 0; mip < actual.mipmapCount; mip++)
                    {
                        if ((actual.format == TextureFormat.DXT1 || actual.format == TextureFormat.DXT5 || actual.format == TextureFormat.BC7)
                            && (Math.Max(1, actual.width >> mip) % 4 != 0 || Math.Max(1, actual.height >> mip) % 4 != 0)) continue;
                        if (!RenderBytes(expected, mip).SequenceEqual(RenderBytes(actual, mip))) throw new InvalidDataException("Following source used stale pixels");
                    }
                    if (mode != "native")
                    {
                        string selection = (string)Call("PreparedTextureRuntime", "SelectionIdentity", mod, selected)!;
                        string key = (string)Call("PreparedTextureRuntime", "Identity", selection, selected[changed].Key,
                            callbackDestination, callbackReplacement, 0, (string)Call("PngRuntime", "PreparationPlatform")!, "")!;
                        object entry = Invoke(store, "Read", key) ?? throw new InvalidDataException("Changed source cache entry missing");
                        CompareEntries(native, entry);
                        restored = (Texture2D)Call("PngRuntime", "Restore", entry)!;
                        CompareTextures(expected, restored);
                    }
                    long preparedUploads = (long)uploadCounter.GetValue(null)! - uploadsBefore;
                    if (captureCallback && (!callbackQuiescent || !unchangedReused))
                        throw new InvalidDataException("Callback did not prove quiescent changed-source rejection and unchanged retained reuse");
                    if (preparedUploads != (captureCallback ? 3 : late ? 1 : 0)) throw new InvalidDataException("Unexpected prepared consumption across callback");
                    Receipt("event", "c06-holder-callback", "mode", mode, "passed", true, "callbackWrites", callbackWrites, "preparedUploads", preparedUploads,
                        "followingSourceChanged", true, "callbackQuiescent", callbackQuiescent, "unchangedRetainedReused", unchangedReused, "nativeOutputPreserved", true, "cachedOutputChecked", mode != "native");
                }
                finally
                {
                    patch.Unpatch(ctor, HarmonyPatchType.All, patch.Id);
                    // Mono cannot restore an extern method's nonexistent IL after
                    // transpiling it. This final private case retains its equivalent
                    // raw-byte reader until automatic process exit, disarming writes.
                    rawCallbackArmed = false;
                    foreach (var pair in loaded) if (pair.Second.contentItem != null && !ReferenceEquals(pair.Second.contentItem, BaseContent.BadTex)) Object.DestroyImmediate(pair.Second.contentItem);
                    if (expected != null) Object.DestroyImmediate(expected);
                    if (restored != null) Object.DestroyImmediate(restored);
                    ((IDisposable)store).Dispose();
                }
            }
        }
        finally
        {
            patch.Unpatch(ctor, HarmonyPatchType.All, patch.Id);
            rawCallbackArmed = false;
            foreach (var pair in state) pair.Key.SetValue(null, pair.Value);
            startupStore.SetValue(null, previousStartupStore);
            callbackTrigger = callbackDestination = "";
        }
    }
}
