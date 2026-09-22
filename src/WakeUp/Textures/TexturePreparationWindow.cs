// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace WakeUp;

internal sealed class TexturePreparationWindow : Window
{
    private sealed class Item
    {
        internal string Logical = "", Selection = "", Reason = "";
        internal FileInfo File = null!;
        internal ModContentPack Provider = null!;
        internal PreparationImageHeader? Header;
        internal bool Eligible;
    }
    private readonly WakeUpSettings settings;
    private readonly HashSet<ModContentPack> providers = new();
    private string paths = "", status = "Select active mods and optional paths, then Scan.";
    private bool running, qualityConsent;
    private int preset, cursor, completed, reused, failed, skipped, eligible;
    private List<Item> items = new();
    private PreparedTextureStore? store;
    private CancellationTokenSource? cancellation;
    private Task<byte[]>? worker;
    private Item? current;
    private byte[]? source;
    private string identity = "", platform = "", helper = "", helperPath = "";
    private string activeIdentity = "";
    private Vector2 scroll;
    private bool scanning, startAfterScan;
    private ModContentPack[] scanProviders = Array.Empty<ModContentPack>();
    private KeyValuePair<string, FileInfo>[] scanFiles = Array.Empty<KeyValuePair<string, FileInfo>>();
    private int scanProvider, scanFile;
    private string scanSelection = "";
    private long scannedSourceBytes;
    private static readonly string[] Choices = { "Prepare full-size output for Wake-Up", "Export DDS: full-size recompression",
        "Export DDS: half width and height", "Export DDS: quarter width and height" };
    public override Vector2 InitialSize => new(900f, 800f);
    internal TexturePreparationWindow(WakeUpSettings settings)
    {
        this.settings = settings;
        doCloseButton = true; doCloseX = true; absorbInputAroundWindow = true; forcePause = true;
    }
    public override void DoWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(new Rect(0, 0, inRect.width, inRect.height - 50));
        listing.Label("Batch texture preparation. Original mod files stay intact. Automatic and prepared output share one store. PNG/JPEG preparation preserves native output; optional PSD uses the bundled decoder. Authored DDS loads natively without recaching. In-game quality changes and DDS exports are separate explicit choices. All owned caches and DDS exports share the configured cache budget.");
        if (!running && !scanning)
        {
            if (listing.ButtonText($"Selected {providers.Count} active mods — choose / toggle"))
            {
                var options = new List<FloatMenuOption> {
                    new("Select all active mods", () => { providers.UnionWith(LoadedModManager.RunningModsListForReading); items.Clear(); }),
                    new("Clear selection", () => { providers.Clear(); items.Clear(); }) };
                options.AddRange(LoadedModManager.RunningModsListForReading.Select(m =>
                    new FloatMenuOption((providers.Contains(m) ? "✓ " : "") + m.Name, () => { if (!providers.Remove(m)) providers.Add(m); items.Clear(); })));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Label("Files or folders separated by semicolons; blank selects all (example: Textures/Things; Textures/UI/Icon.png):");
            string changed = listing.TextEntry(paths);
            if (changed != paths) { paths = changed; items.Clear(); }
            if (listing.ButtonText(Choices[preset])) Find.WindowStack.Add(new FloatMenu(Choices.Select((name, i) =>
                new FloatMenuOption(name, () => { preset = i; qualityConsent = false; items.Clear(); })).ToList()));
            if (preset != 0)
            {
                listing.Label("DDS export changes pixels. For ordinary sRGB color art only: it is not a safe conversion for masks, normal/data maps, custom color spaces or coordinated atlas/material inputs. Full mips, straight alpha, BC1/BC3; no sampler or readability contract is exported. The game does not automatically use these files. Windows helper required.");
                listing.CheckboxLabeled("I selected color artwork suitable for this explicit quality-changing export", ref qualityConsent);
            }
            if (listing.ButtonText("Scan / refresh dry run")) Scan(false);
            if (providers.Count > 0 && listing.ButtonText("Manage in-game quality for these selected mods/files..."))
                Find.WindowStack.Add(new TextureQualityWindow(settings, providers, paths));
            if (items.Count > 0 && listing.ButtonText("Prepare / resume selected content")) Scan(true);
            listing.Label(PreparationStorageSession.ClearDescription);
            if (listing.ButtonText(PreparationStorageSession.ClearActionLabel))
            {
                try
                {
                    CacheLaunchPolicy? selected = null;
                    PreparedTextureStore.Maintain(PreparedTextureRuntime.SaveRoot, CacheLaunchPolicy.Latch(ref selected, "clear", () => { }));
                    status = PreparationStorageSession.ClearCompleted;
                }
                catch (Exception e) { status = "Clear unavailable: " + e.Message; }
            }
        }
        else if (listing.ButtonText("Cancel — stop after current work is cleaned up")) cancellation!.Cancel();
        listing.Label(status);
        listing.Label($"Selected {items.Count}; eligible at scan {eligible}; prepared {completed}; reused {reused}; skipped {skipped}; failed {failed}; visited {cursor}/{items.Count}.");
        listing.Label("96 MiB encoded source; 64 MiB decoded image base; native mip data below 96 MiB, stored in independently checked blocks. DDS exports retain their 8 MiB limit. Native engine peak CPU/GPU memory is unmeasured (mips and workspace are extra). One item in flight. Resume rescans and hashes current sources; completed entries survive cancellation.");
        float top = listing.CurHeight;
        listing.End();
        var area = new Rect(0, top, inRect.width, Math.Max(20, inRect.height - top - 55));
        var view = new Rect(0, 0, area.width - 20, Math.Max(area.height, items.Count * 26));
        Widgets.BeginScrollView(area, ref scroll, view);
        int first = Math.Max(0, (int)(scroll.y / 26)), last = Math.Min(items.Count, first + (int)(area.height / 26) + 2);
        for (int i = first; i < last; i++)
            Widgets.Label(new Rect(0, i * 26, view.width, 26), items[i].Provider.Name + ": " + items[i].Logical + " — " + items[i].Reason);
        Widgets.EndScrollView();
    }
    private void Scan(bool prepareAfter)
    {
        try
        {
            items.Clear(); eligible = 0;
            activeIdentity = PreparedTextureRuntime.ActiveIdentity();
            _ = TexturePreparationBatch.Matches("Textures/probe.png", paths);
            scanProviders = LoadedModManager.RunningModsListForReading.Where(providers.Contains).ToArray();
            scanFiles = Array.Empty<KeyValuePair<string, FileInfo>>(); scanProvider = scanFile = 0; scannedSourceBytes = 0;
            cursor = completed = reused = failed = skipped = 0;
            cancellation = new CancellationTokenSource(); scanning = true; startAfterScan = prepareAfter;
            status = "Scanning selected mods: native discovery per mod, then 16 bounded headers per update. Cancel stops between these steps.";
        }
        catch (Exception e) { Finish(); status = "Scan unavailable: " + e.Message; }
    }
    private void AdvanceScan()
    {
        try
        {
            if (cancellation!.IsCancellationRequested) { Finish(); status = "Scan cancelled; no preparation started."; return; }
            if (PreparedTextureRuntime.ActiveIdentity() != activeIdentity) throw new InvalidDataException("active mod order changed; rescan");
            if (scanFile >= scanFiles.Length)
            {
                if (scanProvider >= scanProviders.Length)
                {
                    scanning = false; cancellation.Dispose(); cancellation = null;
                    status = $"Dry run: {items.Count} selected; {eligible} candidates; {items.Count - eligible} skipped; original inputs {(scannedSourceBytes / 1048576d):F2} MiB. Final format/size and content freshness remain unverified. Native DDS precedence and each provider's holder are preserved.";
                    scanFiles = Array.Empty<KeyValuePair<string, FileInfo>>();
                    if (startAfterScan && eligible > 0) Start();
                    return;
                }
                var mod = scanProviders[scanProvider++];
                var selected = PngRuntime.SelectedFiles(mod).ToArray();
                if (selected.Length > 131072) throw new InvalidDataException("provider selection exceeds 131072 items");
                scanSelection = PreparedTextureRuntime.SelectionIdentity(mod, selected);
                scanFiles = selected.Where(p => TexturePreparationBatch.Matches(p.Key, paths)).ToArray(); scanFile = 0;
                // Native provider discovery remains one indivisible game call;
                // never perform another provider or all headers in this update.
                return;
            }
            for (int budget = 0; budget < 16 && scanFile < scanFiles.Length; budget++)
            {
                    var pair = scanFiles[scanFile++];
                    if (items.Count >= 131072) throw new InvalidDataException("batch exceeds 131072 items; narrow selection");
                    var item = new Item { Logical = pair.Key, File = pair.Value, Selection = scanSelection, Provider = scanProviders[scanProvider - 1] };
                    try
                    {
                        scannedSourceBytes += pair.Value.Length;
                        using var file = pair.Value.OpenRead(); item.Header = PreparationImageHeader.Read(file, preset == 0);
                        if (preset == 0) item.Reason = TexturePreparationBatch.NativeAdmission(item.Header);
                        else item.Reason = TexturePreparationBatch.ExportAdmission(item.Header, preset);
                        item.Eligible = item.Reason.Length == 0;
                        if (item.Eligible) { eligible++; item.Reason = preset == 0 ? "candidate: final native format/size checked during capture" : "candidate: explicit DDS export; freshness unverified"; }
                    }
                    catch (Exception e) { item.Reason = e.Message; }
                    items.Add(item);
            }
            status = $"Scanning mod {scanProvider}/{scanProviders.Length}, file {scanFile}/{scanFiles.Length}; {items.Count} selected so far, {eligible} candidates.";
        }
        catch (Exception e) { Finish(); status = "Scan unavailable: " + e.Message; }
    }
    private void Start()
    {
        try
        {
            if (providers.Count == 0 || !settings.Enabled || Current.ProgramState != ProgramState.Entry || LongEventHandler.AnyEventNowOrWaiting)
                throw new InvalidOperationException("preparation requires enabled Wake-Up at an idle main menu");
            if (preset == 0)
            {
                PngRuntime.TryInitialize(new[] { "--wake-up-mode=candidate", "--wake-up-png=prepare", "-savedatafolder=" + PreparedTextureRuntime.SaveRoot }, true);
                if (!PngRuntime.PreparationAvailable) throw new InvalidOperationException("native loader contract unavailable or loading in progress");
                platform = PngRuntime.PreparationPlatform();
            }
            else
            {
                if (!qualityConsent) throw new InvalidOperationException("select suitable color inputs and explicitly acknowledge quality changes");
                helperPath = TexturePreparationHelper.ExactPath(PreparedTextureRuntime.ModRoot);
                helper = TexturePreparationHelper.Identity(helperPath);
            }
            if (items.Count == 0 || eligible == 0) return;
            store = new PreparedTextureStore(PreparedTextureRuntime.SaveRoot, compress: settings.CompressTextureStorage);
            cancellation = new CancellationTokenSource(); running = true; cursor = completed = reused = failed = skipped = 0;
            status = preset == 0 ? "Native capture is one image per update; a capture may briefly hold a frame."
                : "Encoding one color image in the Windows CPU helper. Exports: " + store.ExportRoot;
        }
        catch (Exception e) { Finish(); status = "Preparation unavailable: " + e.Message; }
    }
    public override void WindowUpdate()
    {
        base.WindowUpdate();
        if (scanning) { AdvanceScan(); return; }
        if (!running) return;
        try
        {
            if (worker != null)
            {
                if (!worker.IsCompleted) return;
                var done = worker; worker = null;
                byte[] dds = done.GetAwaiter().GetResult();
                Record(TexturePreparationBatch.PublishExport(store!, identity, current!.Logical, dds, Revalidate, cancellation!.Token));
                current = null; source = null;
            }
            if (cancellation!.IsCancellationRequested || cursor >= items.Count) { Finish(); return; }
            if (!settings.Enabled || Current.ProgramState != ProgramState.Entry || LongEventHandler.AnyEventNowOrWaiting
                || preset == 0 && !PngRuntime.PreparationAvailable) { cancellation.Cancel(); Finish(); return; }
            current = items[cursor++];
            if (!current.Eligible) { skipped++; current = null; return; }
            source = PreparedTextureRuntime.ReadSource(current.File);
            using (var input = new MemoryStream(source)) current.Header = PreparationImageHeader.Read(input, preset == 0);
            if (!Revalidate()) throw new InvalidDataException("selection or source changed; rescan");
            identity = preset == 0 ? PreparedTextureRuntime.Identity(current.Selection, current.Logical, current.File.FullName, source, 0, platform, "")
                : TexturePreparationBatch.ExportIdentity(current.Selection, current.Logical, current.File.FullName, source, preset, helper);
            if (preset == 0)
            {
                string refusal = TexturePreparationBatch.NativeAdmission(current.Header);
                if (refusal.Length != 0) { Record("skipped: " + refusal); current = null; source = null; return; }
                Record(TexturePreparationBatch.Native(store!, identity, Revalidate,
                    admit => PngRuntime.CapturePrepared(current.File, source, admit), cancellation.Token));
                current = null; source = null;
            }
            else if (store!.ReadExport(identity) != null) { Record("reused DDS export"); current = null; source = null; }
            else
            {
                string refusal = TexturePreparationBatch.ExportAdmission(current.Header, preset);
                if (refusal.Length != 0) { Record("skipped: " + refusal); current = null; source = null; return; }
                int divisor = 1 << (preset - 1);
                int estimate = PngCache.ExpectedBytes(current.Header.Width / divisor, current.Header.Height / divisor, 12,
                    PreparationImageHeader.Mips(current.Header.Width / divisor, current.Header.Height / divisor));
                if (!store.CanPrepareExport(identity, estimate < 1 ? TexturePreparationHelper.MaximumOutput : Math.Min(TexturePreparationHelper.MaximumOutput, estimate + 128)))
                { Record("skipped: prepared storage capacity or maintenance policy"); current = null; source = null; return; }
                // Only immutable source/options cross to the CPU worker; no game
                // API, store publication or provider objects run on that thread.
                byte[] snapshot = source; var header = current.Header; int choice = preset;
                string executable = helperPath, expected = helper; var token = cancellation.Token;
                worker = Task.Run(() => TexturePreparationHelper.ConvertDds(executable, expected, snapshot, header, choice, token));
            }
        }
        catch (OperationCanceledException) { if (current != null) current.Reason = "cancelled; resume will revalidate"; Finish(); }
        catch (Exception e)
        {
            failed++; status = "Item failed: " + e.Message;
            if (current != null) current.Reason = "failed: " + e.Message;
            current = null; source = null;
            if (cancellation?.IsCancellationRequested == true) Finish();
        }
    }
    private bool Revalidate() => settings.Enabled && Current.ProgramState == ProgramState.Entry && !LongEventHandler.AnyEventNowOrWaiting
        && (preset == 0 ? PngRuntime.PreparationAvailable && PngRuntime.PreparationPlatform() == platform
            : qualityConsent && TexturePreparationHelper.Identity(helperPath) == helper)
        && PreparedTextureRuntime.ActiveIdentity() == activeIdentity
        && TexturePreparationBatch.StillSelected(current!.Provider.foldersToLoadDescendingOrder.ToArray(), current.Logical, current.File.FullName)
        && PngCache.Hash(PreparedTextureRuntime.ReadSource(current.File)).SequenceEqual(PngCache.Hash(source!));
    private void Record(string result)
    {
        current!.Reason = result;
        if (result.StartsWith("reused", StringComparison.Ordinal)) reused++;
        else if (result.StartsWith("prepared", StringComparison.Ordinal)) completed++;
        else skipped++;
    }
    private void Finish()
    {
        cancellation?.Cancel();
        if (worker != null) { try { worker.GetAwaiter().GetResult(); } catch (Exception) { } worker = null; }
        running = scanning = false; source = null; current = null; scanFiles = Array.Empty<KeyValuePair<string, FileInfo>>();
        try { store?.Complete(); }
        finally { store?.Dispose(); store = null; cancellation?.Dispose(); cancellation = null; }
        status = preset == 0 ? "Stopped. Completed native output is reusable after restart with Use prepared textures enabled. Resume rescans."
            : "Stopped. Completed DDS exports and source/path mapping remain under " + Path.Combine(PreparedTextureRuntime.SaveRoot, "WakeUp", "PreparedTextures", "v1", "exports") + ". They are not installed into mods. Resume rescans.";
    }
    public override void PostClose() { Finish(); base.PostClose(); }
}
