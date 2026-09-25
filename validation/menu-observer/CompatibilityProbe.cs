// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Functional evidence from native rendering. No synthetic GUI calls or input.
public sealed class CompatibilityProbe : MonoBehaviour
{
    private static string Mode => Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--fixture-compatibility="))?.Split('=')[1] ?? "";
    internal static bool Selected => Mode.Length != 0;
    private static int repaint = -1;
    private static string installedDirectory = "";
    private static bool started;
    private string directory = "";
    private string failure = "";
    private readonly List<string> pages = new();
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private int notices, windows, acknowledged;
    private bool finished;
    internal static void Install(string directory)
    {
        installedDirectory = directory;
        new Harmony("local.fixture.compatibility-render").Patch(AccessTools.Method(typeof(Root), "OnGUI"),
            postfix: new HarmonyMethod(typeof(CompatibilityProbe), nameof(Repaint)) { priority = Priority.Last });
    }
    private static void Repaint()
    {
        if (Event.current?.type != EventType.Repaint) return;
        repaint = Time.frameCount;
        if (!started && PlayDataLoader.Loaded && !LongEventHandler.AnyEventNowOrWaiting && Current.ProgramState == ProgramState.Entry)
            Start(installedDirectory);
    }
    internal static void Start(string directory)
    {
        if (started) return;
        started = true;
        var probe = new GameObject("FixtureCompatibility").AddComponent<CompatibilityProbe>();
        probe.directory = directory;
        probe.StartCoroutine(probe.Observe());
    }
    private IEnumerator Observe()
    {
        float deadline = Time.realtimeSinceStartup + 45;
        float settled = Time.realtimeSinceStartup + 2;
        Window? previous = null;
        int lastFrame = -1;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForEndOfFrame();
            if (repaint <= lastFrame) continue;
            lastFrame = repaint;
            bool done = false;
            try
            {
                var type = AccessTools.TypeByName("WakeUp.CompatibilityStatus") ?? throw new InvalidOperationException("Compatibility status missing");
                var window = (Window?)AccessTools.Field(type, "window").GetValue(null);
                if (window == null)
                {
                    if (Time.realtimeSinceStartup >= settled && MenuObserverMod.MenuObserved)
                    {
                        if ((Mode == "dismiss" || Mode == "ack") && windows == 0) throw new InvalidOperationException("Expected warning window absent");
                        Capture("menu");
                        done = true;
                    }
                }
                else
                {
                    if (!Find.WindowStack.Windows.Contains(window)) throw new InvalidOperationException("Warning not in native WindowStack");
                    if (Mode == "none") throw new InvalidOperationException("Unexpected warning window");
                    var rendered = (HashSet<string>)AccessTools.Field(window.GetType(), "rendered").GetValue(window);
                    var snapshot = (Array)AccessTools.Field(window.GetType(), "notices").GetValue(window);
                    if (window != previous) { windows++; notices += snapshot.Length; previous = window; }
                    // Wait for a real paint, including when the first card is
                    // taller than the viewport and no complete card is read yet.
                    float viewport = (float)AccessTools.Field(window.GetType(), "observedViewportHeight").GetValue(window);
                    if (viewport <= 0) continue;
                    int before = seen.Count;
                    foreach (string key in rendered) seen.Add(key);
                    if (seen.Count != before) Capture("notice-" + pages.Count);
                    if (Mode == "dismiss" || Mode == "observe") { window.Close(); settled = Time.realtimeSinceStartup + 2; }
                    else if (rendered.Count == snapshot.Length)
                    {
                        if (!(bool)AccessTools.Method(window.GetType(), "AcknowledgeRendered").Invoke(window, null))
                            throw new InvalidOperationException("Acknowledgment handler refused");
                        acknowledged += rendered.Count;
                        settled = Time.realtimeSinceStartup + 2;
                    }
                    else
                    {
                        // Overlapping viewport steps expose every fragment of
                        // grouped/tall cards. Only the window's Repaint handler
                        // marks original keys as read; the observer never does.
                        var field = AccessTools.Field(window.GetType(), "scroll");
                        var position = (Vector2)field.GetValue(window);
                        float maximum = (float)AccessTools.Field(window.GetType(), "observedMaxScroll").GetValue(window);
                        field.SetValue(window, new Vector2(0, Math.Min(maximum, position.y + viewport * 0.8f)));
                    }
                }
            }
            catch (Exception ex) { failure = ex.ToString(); done = true; }
            if (done)
            {
                if (failure.Length == 0 && FastLoaderProbe.Selected)
                    yield return FastLoaderProbe.Run(directory, error => failure += error);
                Finish(); yield break;
            }
        }
        failure = "Timed out awaiting genuine warning rendering/settlement";
        Finish();
    }
    private void Capture(string name)
    {
        if (pages.Count >= 32 || (long)Screen.width * Screen.height > 16 * 1024 * 1024) throw new InvalidOperationException("Framebuffer capture bound exceeded");
        var prior = RenderTexture.active;
        Texture2D? pixels = null;
        try
        {
            RenderTexture.active = null;
            pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
            pixels.Apply(false, false);
            string path = "compatibility-" + name + ".png";
            File.WriteAllBytes(Path.Combine(directory, path), ImageConversion.EncodeToPNG(pixels));
            pages.Add(path);
        }
        finally { RenderTexture.active = prior; if (pixels != null) Destroy(pixels); }
    }
    private void Finish()
    {
        if (finished) return;
        finished = true;
        try
        {
            if (!FastLoaderProbe.Selected) failure += CompatibilityAssetProbe.Run(directory);
            var type = AccessTools.TypeByName("WakeUp.CompatibilityStatus");
            object registry = AccessTools.PropertyGetter(type, "Registry").Invoke(null, null);
            var status = (string[])AccessTools.Method(registry.GetType(), "Snapshot").Invoke(registry, null);
            bool xmlWorkload = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "local.fixture.compatibilityxml");
            string xmlResult = xmlWorkload ? DefDatabase<RimWorld.StatDef>.GetNamedSilentFail("RloCompatibilityProbe")?.label ?? "missing" : "not selected";
            if (xmlWorkload && xmlResult != "rlo compatibility passed") failure += " XML worker output mismatch: " + xmlResult;
            var hooks = Harmony.GetAllPatchedMethods().SelectMany(m =>
            {
                var p = Harmony.GetPatchInfo(m);
                return p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers).Concat(p.Finalizers)
                    .Select(h => m.DeclaringType?.FullName + "." + m.Name + " | " + h.owner + " | " + h.PatchMethod.DeclaringType?.FullName + "." + h.PatchMethod.Name);
            }).OrderBy(s => s).ToArray();
            var suppliers = new List<string>();
            suppliers.Add("activeHarmony=" + typeof(AccessTools).Assembly.FullName + "; activeGame=" + typeof(LoadedModManager).Assembly.FullName);
            var policy = AccessTools.TypeByName("WakeUp.LoaderSupplierPolicy");
            var identity = AccessTools.TypeByName("WakeUp.SupplierBodyIdentity");
            foreach (var mod in LoadedModManager.RunningModsListForReading.Where(m => m.PackageId == "wowgag.guiperformancepatch" || m.PackageId == "taranchuk.fastergameloading"))
            foreach (var assembly in mod.assemblies.loadedAssemblies)
            {
                string expected = mod.PackageId == "wowgag.guiperformancepatch" ? "9d4a8f3360730a4f6447d6c782d9b3ab4e2925f02984f8976418ce88a65fd1ef" : "4cb5a8434d1e44f3e69d483860f377b4204ed00483036f98fc2b86e34b7283ec";
                suppliers.Add(mod.PackageId + " assembly=" + assembly.GetName().Name + " location=" + assembly.Location + " mvid=" + assembly.ManifestModule.ModuleVersionId
                    + " imageMatch=" + AccessTools.Method(policy, "MatchesImage").Invoke(null, new object[] { assembly, expected }));
                foreach (var method in Harmony.GetAllPatchedMethods().SelectMany(m => { var p = Harmony.GetPatchInfo(m); return p.Prefixes.Concat(p.Postfixes).Concat(p.Finalizers).Concat(p.Transpilers); })
                    .Select(p => p.PatchMethod).Where(m => m.Module.Assembly == assembly && new[] {
                        "GUIPerformancePatch.GppPatchCache", "GUIPerformancePatch.GppContentPreload",
                        "FasterGameLoading.XmlNode_SelectSingleNode_Patch", "FasterGameLoading.LoadedModManager_ApplyPatches_Patch"
                    }.Contains(m.DeclaringType?.FullName)).Distinct())
                    suppliers.Add(method.DeclaringType?.FullName + "." + method.Name + " bodyMatch=" + AccessTools.Method(identity, "Matches").Invoke(null, new object[] { method }));
            }
            File.WriteAllLines(Path.Combine(directory, "supplier-identities.txt"), suppliers);
            File.WriteAllText(Path.Combine(directory, "compatibility.json"), "{\"schema\":\"fixture-compatibility.v1\",\"mode\":" + Q(Mode)
                + ",\"passed\":" + (failure.Length == 0 ? "true" : "false") + ",\"failure\":" + Q(failure)
                + ",\"windows\":" + windows + ",\"notices\":" + notices + ",\"acknowledged\":" + acknowledged
                + ",\"xmlWorkload\":" + Q(xmlResult) + ",\"renderedKeys\":" + ArrayJson(seen) + ",\"images\":" + ArrayJson(pages) + ",\"statuses\":" + ArrayJson(status)
                + ",\"hooks\":" + ArrayJson(hooks) + ",\"evidenceKind\":\"native repaint and framebuffer; shared handler execution, not mouse input\"}");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "compatibility-error.txt"), ex.ToString()); }
        if (Current.ProgramState == ProgramState.Entry && Current.Game == null) Root.Shutdown();
    }
    private static string ArrayJson(IEnumerable<string> values) => "[" + string.Join(",", values.Select(Q)) + "]";
    private static string Q(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
}
