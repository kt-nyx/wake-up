// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.IO;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// One explicit native-load regression, not another formats/playback matrix.
internal static class C10Mp3CompletionProbe
{
    private const string PathName = @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\game\Mods\3457785624\Sounds\Things\LightSMG.mp3";
    private static readonly InvalidOperationException Sentinel = new("C10 late load-state getter");
    private static bool throwGetter;
    private static int getterCalls;
    private static object? capturedReader;
    internal static bool Selected => Environment.GetCommandLineArgs().Contains("--fixture-c10-prepared-audio=completion");

    internal static void Start(string directory)
    {
        var result = new Dictionary<string, object> { ["schema"] = "c10-mp3-completion.v1", ["mode"] = "completion",
            ["functionalPassed"] = false, ["failure"] = "", ["performanceClaim"] = false, ["explicitNativeControls"] = true };
        Harmony? harmony = null;
        MethodInfo? pop = null;
        object? previous = null;
        bool scope = false;
        using var outerInput = new MemoryStream();
        try
        {
            Require(Prefs.VolumeMaster == 0f && AudioListener.volume == 0f, "Fixture must be master-muted.");
            Assembly core = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "WakeUp");
            Type runtime = core.GetType("WakeUp.PreparedAudioRuntime", true)!;
            Type index = core.GetType("WakeUp.PreparedMp3IndexRuntime", true)!;
            MethodInfo snapshot = runtime.GetMethod("Snapshot")!;
            var before = (Dictionary<string, object>)snapshot.Invoke(null, null)!;
            Require((bool)before["enabled"] && !(bool)before["nativeOnly"], "Runtime must initially admit native MP3 work.");
            var mod = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == "tro.vwe.overhaul");
            AudioClip natural = mod.GetContentHolder<AudioClip>().contentList["Things/LightSMG"];
            var naturalReceipt = (Dictionary<string, object>)runtime.GetMethod("Mp3SnapshotForClip")!.Invoke(null, new object[] { natural })!;
            var naturalIndex = (Dictionary<string, object>)naturalReceipt["index"];
            Require((bool)naturalIndex["published"] && (bool)naturalIndex["loadSucceeded"]
                && Convert.ToInt32(naturalIndex["nativeScans"]) == 1, "Ordinary admitted MP3 loading did not publish its successful native index.");
            result["ordinaryAdmittedLoad"] = naturalReceipt;
            MethodInfo push = index.GetMethod("PushContext", BindingFlags.Static | BindingFlags.NonPublic)!;
            pop = index.GetMethod("PopContext", BindingFlags.Static | BindingFlags.NonPublic)!;
            FieldInfo current = index.GetField("context", BindingFlags.Static | BindingFlags.NonPublic)!;
            previous = push.Invoke(null, new object[] { outerInput, new string('A', 64) }); scope = true;
            object outer = current.GetValue(null)!;
            Require(outer != null, "Outer context was not admitted before the late hook.");
            harmony = new Harmony("local.fixture.c10-mp3-completion");
            harmony.Patch(AccessTools.Method(typeof(Manager), nameof(Manager.GetAudioClipLoadState)),
                prefix: new HarmonyMethod(typeof(C10Mp3CompletionProbe), nameof(GetterPrefix)));
            var closed = (Dictionary<string, object>)snapshot.Invoke(null, null)!;
            Require((bool)closed["nativeOnly"], "The real late getter hook did not close native admission.");
            result["closedByGetterHook"] = true;
            // Capture only the newly constructed native outer reader so these
            // synchronous test-owned loads can dispose it after each control.
            harmony.Patch(runtime.GetMethod("CreateMp3Reader")!,
                prefix: new HarmonyMethod(typeof(C10Mp3CompletionProbe), nameof(CaptureReader)));
            var cases = new List<object>();
            foreach (bool throws in new[] { false, true })
            {
                throwGetter = throws; getterCalls = 0;
                // Prove the hook is live independently of Wake-Up bookkeeping.
                if (throws)
                {
                    Exception? observed = null;
                    try { Manager.GetAudioClipLoadState(null!); } catch (Exception error) { observed = error; }
                    Require(ReferenceEquals(observed, Sentinel), "Throwing getter hook was not active.");
                }
                else Manager.GetAudioClipLoadState(null!);
                Require(getterCalls == 1, "Getter hook counter is not active.");
                getterCalls = 0;
                capturedReader = null;
                AudioClip? clip = null;
                Type fileType = typeof(Manager).Assembly.GetType("RimWorld.IO.FilesystemFile", true)!;
                var file = (VirtualFile)fileType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static)!
                    .Invoke(null, new object[] { new FileInfo(PathName) })!;
                using Stream input = file.CreateReadStream();
                try
                {
                    clip = Manager.Load(input, AudioFormat.mp3, "C10-completion-" + throws, false, false, false);
                    Require(!ReferenceEquals(clip, null), "Native Manager.Load did not return its successful clip.");
                    Require(getterCalls == 0, "Wake-Up bookkeeping invoked the late native getter.");
                    Require(capturedReader is IDisposable, "The control did not capture its owned native reader for disposal.");
                    Require(ReferenceEquals(current.GetValue(null), outer), "Nested load did not restore its outer index context.");
                    var states = (Dictionary<AudioClip, AudioDataLoadState>)typeof(Manager)
                        .GetField("audioLoadState", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                    Require(states.Any(entry => ReferenceEquals(entry.Key, clip) && entry.Value == AudioDataLoadState.Loaded), "Original synchronous native load did not finish Loaded.");
                    cases.Add(new Dictionary<string, object> { ["throwingGetter"] = throws, ["getterCallsDuringLoad"] = getterCalls,
                        ["nativeReturnedLoadedClip"] = true, ["outerContextRestored"] = true });
                }
                finally
                {
                    (capturedReader as IDisposable)?.Dispose(); capturedReader = null;
                    if (!ReferenceEquals(clip, null)) UnityEngine.Object.Destroy(clip);
                }
            }
            result["cases"] = cases;
            result["ownedControlReadersDisposed"] = true;
            pop.Invoke(null, new object?[] { previous, false }); scope = false;
            Require(ReferenceEquals(current.GetValue(null), previous), "Outer context leaked after completion.");
            var after = (Dictionary<string, object>)snapshot.Invoke(null, null)!;
            Require(Equals(before["sourceBytes"], after["sourceBytes"]), "Closed admission still hashed MP3 sources.");
            Require(Equals(before["mp3CompletionFailures"], after["mp3CompletionFailures"]), "Closed admission attempted failing optional completion.");
            result["before"] = before; result["after"] = after;
            result["noContextLeak"] = true; result["noClosedAdmissionSourceHashing"] = true;
            result["functionalPassed"] = true;
        }
        catch (Exception error) { result["failure"] = error.ToString(); }
        finally
        {
            if (scope) pop?.Invoke(null, new object?[] { previous, false });
            harmony?.UnpatchAll(harmony.Id);
        }
        File.WriteAllText(System.IO.Path.Combine(directory, "c10-prepared-audio-probe.json"), C10NaturalAudioProbe.Json(result), new System.Text.UTF8Encoding(false));
    }
    private static void GetterPrefix() { getterCalls++; if (throwGetter) throw Sentinel; }
    private static void CaptureReader(object __1) { capturedReader = __1; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
