// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Private functional evidence only. Reads the game's own rendered framebuffer;
// no desktop capture, focus changes, synthetic input, or extra loading work.
public sealed class C13LoadingProbe : MonoBehaviour
{
    private const string Owner = "local.fixture.c13-rendered-loading";
    private const int CaptureLimit = 48;
    private static string directory = "", observerDirectory = "";
    private static C13LoadingProbe? instance;
    private static bool installed, started;
    private readonly HashSet<string> sampledStages = new(StringComparer.Ordinal);
    private bool capturePending, reportWorkflow, failed;
    private int screenshots, lastCaptureFrame = -30;
    private long imageBytes;
    private Window? reportWindow;
    private float workflowStarted;
    private Dictionary<ModMetaData, bool>? previewsBeforeReport;
    private static readonly FieldInfo PreviewLoadedField = AccessTools.Field(typeof(ModMetaData), "previewImageWasLoaded");
    private static readonly FieldInfo EventField = AccessTools.Field(typeof(LongEventHandler), "currentEvent");
    private static Type? observationType, diagnosticsType;
    private static MethodInfo? currentGetter, snapshotMethod;
    private static FieldInfo? stageField, workField;

    internal static void Install(string evidenceDirectory)
    {
        if (installed) return;
        observerDirectory = evidenceDirectory;
        directory = Path.Combine(evidenceDirectory, "c13-loading");
        Directory.CreateDirectory(directory);
        // Root.Update and LongEventHandler are guarded production contracts.
        // Root.OnGUI is an outer, unguarded native renderer; this void postfix
        // cannot suppress drawing or change any production arguments.
        new Harmony(Owner).Patch(AccessTools.Method(typeof(Root), "OnGUI"),
            postfix: new HarmonyMethod(typeof(C13LoadingProbe), nameof(AfterRootGui)) { priority = Priority.Last });
        installed = true;
        Receipt("installed", "Native Root.OnGUI repaint -> EndOfFrame framebuffer capture. Earlier constructors are outside screenshot coverage. Screenshots and handler calls are functional evidence, not performance samples or button-click evidence.");
    }
    private static void EnsureInstance()
    {
        if (instance != null) return;
        var host = new GameObject("FixtureC13RenderedLoading");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<C13LoadingProbe>();
        observationType = AccessTools.TypeByName("WakeUp.LoadingObservationRuntime");
        diagnosticsType = AccessTools.TypeByName("WakeUp.LoadingDiagnosticsRuntime");
        if (observationType != null) currentGetter = AccessTools.PropertyGetter(observationType, "Current");
    }
    private static void AfterRootGui()
    {
        if (!installed || Event.current == null || Event.current.type != EventType.Repaint) return;
        try
        {
            EnsureInstance();
            if (instance!.reportWorkflow || instance.capturePending || instance.screenshots >= CaptureLimit
                || Time.frameCount - instance.lastCaptureFrame < 12) return;
            object? nativeEvent = EventField.GetValue(null);
            if (nativeEvent == null) return;
            string phase = Current.ProgramState + "/" + (Current.Game == null ? "no-game" : "game-present");
            string observedStage = CurrentStage(out string work);
            string text = Value(nativeEvent, "eventText");
            string key = phase + "/" + observedStage;
            if (!instance.sampledStages.Add(key)) return;
            instance.capturePending = true;
            instance.StartCoroutine(instance.CaptureAfterFrame("loading-" + instance.screenshots.ToString("D2", CultureInfo.InvariantCulture),
                "stage-at-render=" + observedStage + "; work-at-render=" + work + "; native-event=" + text + "; phase=" + phase));
        }
        catch (Exception e) { Receipt("sampling-error", e.ToString()); }
    }
    internal static void Start(string evidenceDirectory)
    {
        if (started) return;
        if (!installed) Install(evidenceDirectory);
        EnsureInstance();
        started = true;
        instance!.reportWorkflow = true;
        instance.workflowStarted = Time.realtimeSinceStartup;
        instance.StartCoroutine(instance.ReportAndControls());
    }
    private IEnumerator ReportAndControls()
    {
        // Leave the native main-menu OnGUI iteration before adding a window.
        yield return null;
        try
        {
            if (observationType == null || diagnosticsType == null) throw new InvalidOperationException("Wake-Up observation/diagnostics types unavailable");
            previewsBeforeReport = PreviewFlags();
            Receipt("preview-flags-before-report", PreviewFlagText(previewsBeforeReport));
            object session = AccessTools.PropertyGetter(observationType, "Latest").Invoke(null, null)
                ?? throw new InvalidOperationException("No loading session was recorded");
            string path = (string)AccessTools.Method(observationType, "SaveReport").Invoke(null, new[] { session });
            if (!File.Exists(path)) throw new IOException("Shared SaveReport handler returned a missing path: " + path);
            string report = File.ReadAllText(path);
            if (report.Length == 0) throw new IOException("Shared SaveReport handler saved an empty report");
            File.Copy(path, Path.Combine(directory, "saved-session-report.txt"), true);
            Receipt("save-report-handler", "path=" + path + "; bytes=" + new FileInfo(path).Length + "; sha256=" + Hash(path)
                + "; called the same runtime handler used by the ordinary-user Save report control; no click simulated");
            string root = GenFilePaths.SaveDataFolderPath;
            string request = Path.Combine(root, "WakeUp", "xml-export-next-launch.request");
            object? requested = AccessTools.Method(diagnosticsType, "RequestNextLaunch").Invoke(null, new object[] { root });
            if (!File.Exists(request)) throw new IOException("RequestNextLaunch did not create its marker: " + requested);
            Receipt("xml-request-handler", Convert.ToString(requested) ?? "");
            object? cancelled = AccessTools.Method(diagnosticsType, "CancelRequest").Invoke(null, new object[] { root });
            if (File.Exists(request)) throw new IOException("CancelRequest left its marker: " + cancelled);
            Receipt("xml-cancel-handler", Convert.ToString(cancelled) ?? "");
            requested = AccessTools.Method(diagnosticsType, "RequestNextLaunch").Invoke(null, new object[] { root });
            if (!File.Exists(request)) throw new IOException("Final RequestNextLaunch did not create its marker");
            Receipt("xml-request-retained", "path=" + request + "; next launch required to exercise actual export; parent transaction must preserve/restore request state. " + requested);
            var windowType = AccessTools.TypeByName("WakeUp.LoadingTimingWindow") ?? throw new TypeLoadException("LoadingTimingWindow unavailable");
            reportWindow = (Window)Activator.CreateInstance(windowType, true);
            Find.WindowStack.Add(reportWindow);
            Receipt("report-window-opened", "Actual native WindowStack owns the product window. Capture follows genuine rendered frames, with no direct DoWindowContents call.");
        }
        catch (Exception e) { Fail(e); }
        if (!failed)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return CaptureAfterFrame("report-window", "Actual LoadingTimingWindow opened through native WindowStack; active fixture resolution and UI scale retained");
            try { VerifyReportAndNativePreview(); }
            catch (Exception e) { Fail(e); }
        }
        if (reportWindow != null)
        {
            try { reportWindow.Close(doCloseSound: false); } catch (Exception e) { Fail(e); }
            reportWindow = null;
        }
        reportWorkflow = false;
        Receipt(failed ? "report-controls-failed" : "report-controls-completed",
            "Rendered pixels need visual inspection. Handler receipts prove explicit report save/request/cancel execution, not pointer interaction. Resolution/UI scaling comparison requires separately prepared fixture settings.");
        if (!failed && Environment.GetCommandLineArgs().Contains("--fixture-gameplay-smoke"))
        {
            Receipt("handoff-gameplay", "Existing native gameplay smoke owns create/map/save/reload and normal exit; passive rendered loading capture remains installed.");
            FixtureGameplaySmoke.Start(observerDirectory);
        }
        else if (!failed && Environment.GetCommandLineArgs().Contains("--fixture-world-background"))
        {
            Receipt("handoff-world", "Existing world-loading probe owns lifecycle and normal exit; passive capture remains installed.");
            FixtureWorldBackground.Start(observerDirectory);
        }
        else if (Current.ProgramState == ProgramState.Entry && Current.Game == null)
        {
            Receipt("normal-exit-requested", "Product report/control probe finished at menu; native shutdown.");
            Root.Shutdown();
        }
        else Receipt("exit-not-requested", "No longer at empty entry menu; existing gameplay lifecycle retains ownership.");
    }
    private IEnumerator CaptureAfterFrame(string name, string context)
    {
        capturePending = true;
        yield return new WaitForEndOfFrame();
        Texture2D? pixels = null;
        RenderTexture? previous = RenderTexture.active;
        try
        {
            if (screenshots >= CaptureLimit) yield break;
            int width = Screen.width, height = Screen.height;
            if (width < 1 || height < 1 || (long)width * height > 16 * 1024 * 1024)
                throw new InvalidOperationException("Framebuffer unavailable or exceeds the 16-megapixel functional-capture bound: " + width + "x" + height);
            RenderTexture.active = null;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            pixels.Apply(false, false);
            byte[] png = ImageConversion.EncodeToPNG(pixels);
            if (imageBytes + png.LongLength > 256L * 1024 * 1024) throw new IOException("256 MiB capture limit reached");
            string file = screenshots.ToString("D2", CultureInfo.InvariantCulture) + "-" + name + ".png";
            File.WriteAllBytes(Path.Combine(directory, file), png);
            imageBytes += png.LongLength; screenshots++; lastCaptureFrame = Time.frameCount;
            object? nativeEvent = EventField.GetValue(null);
            string stage = CurrentStage(out string work);
            Receipt("framebuffer", "file=" + file + "; framebuffer=" + width + "x" + height + "; ui=" + UI.screenWidth + "x" + UI.screenHeight
                + "; pixels-per-ui-unit=" + (width / (double)Math.Max(1, UI.screenWidth)).ToString("F3", CultureInfo.InvariantCulture)
                + "; focused=" + Application.isFocused + "; game-present=" + (Current.Game != null)
                + "; native-extra-ui=" + (nativeEvent == null ? "no-event" : Value(nativeEvent, "showExtraUIInfo"))
                + "; summary-status=" + ProductStatus("WakeUp.NativeLoadingSummaryRuntime")
                + "; display-status=" + ProductStatus("WakeUp.LoadingDisplayRuntime")
                + "; stage-at-capture=" + stage + "; work-at-capture=" + work + "; " + context);
        }
        catch (Exception e) { Fail(e); }
        finally
        {
            RenderTexture.active = previous;
            if (pixels != null) Destroy(pixels);
            capturePending = false;
        }
    }
    private void Update()
    {
        if (!reportWorkflow || Time.realtimeSinceStartup - workflowStarted < 45) return;
        Fail(new TimeoutException("Report workflow did not receive its required native rendered frames within 45 seconds; no rendered pass claimed."));
        reportWorkflow = false;
        StopAllCoroutines();
        if (reportWindow != null)
        {
            try { reportWindow.Close(doCloseSound: false); } catch (Exception e) { Fail(e); }
            reportWindow = null;
        }
        Receipt("report-controls-failed", "Native rendered-frame timeout. Failed evidence is retained; normal shutdown is cleanup, not a passing probe.");
        if (Current.ProgramState == ProgramState.Entry && Current.Game == null) Root.Shutdown();
    }
    private static Dictionary<ModMetaData, bool> PreviewFlags()
    {
        if (PreviewLoadedField == null || PreviewLoadedField.FieldType != typeof(bool))
            throw new NotSupportedException("Native metadata preview-loaded field changed");
        return LoadedModManager.RunningModsListForReading.Select(mod => mod.ModMetaData).Where(meta => meta != null).Distinct()
            .ToDictionary(meta => meta, meta => (bool)PreviewLoadedField.GetValue(meta));
    }
    private static string PreviewFlagText(Dictionary<ModMetaData, bool> flags)
        => "active=" + flags.Count + "; loaded=" + flags.Count(pair => pair.Value) + "; flags="
            + string.Join(",", flags.Select(pair => pair.Key.PackageId + "=" + pair.Value));
    private void VerifyReportAndNativePreview()
    {
        if (previewsBeforeReport == null) throw new InvalidOperationException("Missing before-report native preview snapshot");
        var afterReport = PreviewFlags();
        if (previewsBeforeReport.Count != afterReport.Count || previewsBeforeReport.Any(pair => !afterReport.TryGetValue(pair.Key, out bool state) || state != pair.Value))
            throw new InvalidOperationException("Native preview-loaded flags changed while the loading report was displayed; lazy report proof failed");
        Receipt("preview-flags-after-report", "unchanged=true; product report did not request any artwork. " + PreviewFlagText(afterReport));
        var getter = AccessTools.PropertyGetter(typeof(ModMetaData), "PreviewImage");
        if (getter == null)
        {
            Receipt("native-preview-unsupported", "Native PreviewImage getter changed or is absent; no artwork request attempted.");
            return;
        }
        var patches = Harmony.GetPatchInfo(getter);
        if (patches != null && patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers)
            .Concat(patches.InnerPrefixes).Concat(patches.InnerPostfixes).Any())
        {
            Receipt("native-preview-unsupported", "PreviewImage getter has foreign hooks; native-only lazy getter proof was not attempted.");
            return;
        }
        // Inspected from this fixture's actual GOG assembly: PreviewImage reads
        // About/Preview.png only on its first getter call, then stores both the
        // Texture2D and previewImageWasLoaded. Metadata/path/header inspection
        // below never invokes that getter or decodes other artwork.
        ModMetaData? selected = null;
        long sourceBytes = 0;
        foreach (var meta in afterReport.Where(pair => !pair.Value).Select(pair => pair.Key).OrderBy(meta => meta.Official))
        {
            var file = new FileInfo(meta.PreviewImagePath);
            if (!file.Exists || file.Length < 24 || file.Length >= 4L * 1024 * 1024) continue;
            byte[] header = new byte[24];
            using (var stream = file.OpenRead()) if (stream.Read(header, 0, header.Length) != header.Length) continue;
            if (header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71
                || header[4] != 13 || header[5] != 10 || header[6] != 26 || header[7] != 10
                || header[12] != 73 || header[13] != 72 || header[14] != 68 || header[15] != 82) continue;
            long width = ((long)header[16] << 24) | ((long)header[17] << 16) | ((long)header[18] << 8) | header[19];
            long height = ((long)header[20] << 24) | ((long)header[21] << 16) | ((long)header[22] << 8) | header[23];
            if (width < 1 || height < 1 || width > 2048 || height > 2048) continue;
            selected = meta; sourceBytes = file.Length; break;
        }
        if (selected == null)
        {
            Receipt("native-preview-no-candidate", "No unloaded active native preview under 4 MiB with PNG dimensions at most 2048x2048. No artwork getter invoked; native request proof remains untested.");
            return;
        }
        Texture2D first = selected.PreviewImage;
        Texture2D second = selected.PreviewImage;
        var afterRequest = PreviewFlags();
        bool onlySelectedChanged = afterRequest.Count == afterReport.Count && afterRequest.All(pair =>
            afterReport.TryGetValue(pair.Key, out bool previous) && (ReferenceEquals(pair.Key, selected) ? pair.Value && !previous : pair.Value == previous));
        if (first == null || !ReferenceEquals(first, second) || !onlySelectedChanged)
            throw new InvalidOperationException("Single native preview request did not load only the selected metadata and retain the same cached Texture2D on the second getter");
        Receipt("native-preview-request-passed", "package=" + selected.PackageId + "; path=" + selected.PreviewImagePath + "; bytes=" + sourceBytes
            + "; dimensions=" + first.width + "x" + first.height + "; only-selected-loaded=true; second-getter-same-object=true; native getter requested one artwork; separate from rendered-report evidence");
        previewsBeforeReport = null;
    }
    private void OnApplicationQuit() => Receipt("application-quit", "screenshots=" + screenshots + "; failed=" + failed + "; image-bytes=" + imageBytes);
    private void Fail(Exception error) { failed = true; Receipt("probe-error", error.ToString()); }
    private static string CurrentStage(out string work)
    {
        work = "";
        object? session = currentGetter?.Invoke(null, null);
        if (session == null) return "unobserved";
        snapshotMethod ??= AccessTools.Method(session.GetType(), "Snapshot");
        object view = snapshotMethod.Invoke(session, null);
        stageField ??= AccessTools.Field(view.GetType(), "Stage");
        workField ??= AccessTools.Field(view.GetType(), "Work");
        work = Convert.ToString(workField.GetValue(view)) ?? "";
        return Convert.ToString(stageField.GetValue(view)) ?? "unobserved";
    }
    private static string Value(object value, string field) => Convert.ToString(AccessTools.Field(value.GetType(), field)?.GetValue(value)) ?? "unknown";
    private static string ProductStatus(string typeName)
    {
        var type = AccessTools.TypeByName(typeName);
        return type == null ? "supplier absent" : Convert.ToString(AccessTools.PropertyGetter(type, "Status")?.Invoke(null, null)) ?? "unknown";
    }
    private static string Hash(string path)
    {
        using var sha = SHA256.Create(); using var stream = File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string Json(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    private static void Receipt(string kind, string detail)
    {
        try { File.AppendAllText(Path.Combine(directory, "evidence.jsonl"), "{\"utc\":\"" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            + "\",\"frame\":" + (UnityData.IsInMainThread ? Time.frameCount : -1) + ",\"kind\":\"" + Json(kind) + "\",\"detail\":\"" + Json(detail) + "\"}\n", new UTF8Encoding(false)); }
        catch { /* A private receipt failure never replaces game behavior. */ }
    }
}
