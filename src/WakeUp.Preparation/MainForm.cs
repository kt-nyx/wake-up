// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WakeUp.Preparation;

public sealed class MainForm : Form
{
    private readonly IPreparationBackend? backend;
    private readonly TextBox game = new TextBox(), config = new TextBox(), profile = new TextBox();
    private readonly TextBox extraLocal = new TextBox(), workshop = new TextBox(), paths = new TextBox();
    private readonly CheckedListBox mods = new CheckedListBox { CheckOnClick = true, Dock = DockStyle.Fill };
    private readonly DataGridView table = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, BackgroundColor = SystemColors.Window };
    private readonly ComboBox preset = Choice("Native — reset selected quality changes", "Full size, high-quality compression (lossy)",
        "Half dimensions + compression (lossy)", "Quarter dimensions + compression (lossy)");
    private readonly ComboBox filter = Choice("Point — sharp pixel edges", "Bilinear — smooth pixels", "Trilinear — smooth mip transitions");
    private readonly NumericUpDown bias = new NumericUpDown { Minimum = -3, Maximum = 3, DecimalPlaces = 2, Increment = 0.1M, Width = 74 };
    private readonly NumericUpDown aniso = new NumericUpDown { Minimum = 0, Maximum = 16, Value = 2, Width = 58 };
    private readonly NumericUpDown budget = new NumericUpDown { Minimum = 64, Maximum = 65536, Value = 1024, Increment = 128, Width = 90 };
    private readonly CheckBox psd = new CheckBox { Text = "Allow merged PSD conversion", AutoSize = true };
    private readonly Button scan = new Button { Text = "Scan / dry run", AutoSize = true };
    private readonly Button previewButton = new Button { Text = "Preview selected output", AutoSize = true };
    private readonly Button prepare = new Button { Text = "Prepare / resume selected", AutoSize = true };
    private readonly Button reset = new Button { Text = "Reset selected to native", AutoSize = true };
    private readonly Button cancel = new Button { Text = "Cancel", AutoSize = true, Enabled = false };
    private readonly Button clear = new Button { Text = "Request clear of native cache, quality choices and exports", AutoSize = true };
    private readonly Button rebuild = new Button { Text = "Rebuild selected", AutoSize = true };
    private readonly Button bypass = new Button { Text = "Bypass next launch", AutoSize = true };
    private readonly Label explanation = new Label { AutoSize = true, MaximumSize = new Size(1250, 0) };
    private readonly Label selectionInfo = new Label { AutoSize = true };
    private readonly TextBox status = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill };
    private readonly BindingList<TextureRow> displayed = new BindingList<TextureRow>();
    private readonly List<Button> browseButtons = new List<Button>();
    private readonly Dictionary<string, TextureRow> rows = new Dictionary<string, TextureRow>(StringComparer.Ordinal);
    private DiscoveredLoadout? loadout;
    private CancellationTokenSource? operation;
    private bool changingMods;
    private bool validPaths = true;
    private ProvisionalTextureRoles? roleHints;
    private PreparationBatchPreview? currentPreview;
    private readonly HashSet<string> baseRows = new HashSet<string>(StringComparer.Ordinal);
    private bool refreshingRows;

    public MainForm(IPreparationBackend? backend = null)
    {
        this.backend = backend;
        Text = "Wake-Up — Prepare textures";
        MinimumSize = new Size(1000, 780); Size = new Size(1220, 920);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        filter.SelectedIndex = 2;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 8 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        Controls.Add(layout);
        var inputs = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3 };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        AddPath(inputs, "Existing game folder", game, false);
        AddPath(inputs, "Existing ModsConfig.xml", config, true);
        AddPath(inputs, "Save-data / output profile", profile, false);
        AddPath(inputs, "Additional local mod root", extraLocal, false);
        AddPath(inputs, "Workshop content root", workshop, false);
        AddPath(inputs, "Files or folders (; separated)", paths, false, browse: false);
        layout.Controls.Add(inputs, 0, 0);
        explanation.Text = "Select an existing loadout, scan, then choose mods or individual rows. Sources and mod order stay intact. "
            + "Selecting a color/mask family also includes every required member and provider with the same quality and sampling choice. "
            + "Converted output is provisional until the game verifies its real consumer and paired masks. Unsupported roles keep native output.";
        layout.Controls.Add(explanation, 0, 1);
        var scanning = Flow(scan, previewButton, Button("Select all mods", () => CheckMods(true)), Button("Select no mods", () => CheckMods(false)),
            Button("Include highlighted rows", () => MarkRows(true)), Button("Exclude highlighted rows", () => MarkRows(false)), selectionInfo);
        layout.Controls.Add(scanning, 0, 2);
        var split = new SplitContainer { Width = 1100, Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterDistance = 250, Panel1MinSize = 180 };
        split.Panel1.Controls.Add(mods); split.Panel2.Controls.Add(table); layout.Controls.Add(split, 0, 3);
        table.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(TextureRow.Use), HeaderText = "Use", Width = 40 });
        TextColumn("Why included", nameof(TextureRow.Inclusion), 145);
        TextColumn("Mod", nameof(TextureRow.Mod), 170);
        TextColumn("Texture / file", nameof(TextureRow.Logical), 240);
        TextColumn("Native selection", nameof(TextureRow.Selection), 190);
        TextColumn("Source KiB", nameof(TextureRow.Kib), 82);
        TextColumn("Selected output", nameof(TextureRow.Output), 190);
        TextColumn("Texture KiB", nameof(TextureRow.TextureSize), 110);
        TextColumn("Storage KiB", nameof(TextureRow.StorageSize), 110);
        TextColumn("Saved quality choice", nameof(TextureRow.SavedChoice), 310);
        TextColumn("Result / coverage", nameof(TextureRow.Result), 320);
        table.DataSource = displayed;
        table.CellValueChanged += (_, e) => { if(e.ColumnIndex==0&&!refreshingRows&&operation==null)RefreshRows(); };
        table.CurrentCellDirtyStateChanged += (_, _) => { if (table.IsCurrentCellDirty) table.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        table.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowSource(displayed[e.RowIndex]); };
        var quality = Flow(Label("Quality:"), preset, Label("Sampling:"), filter, Label("Mip bias:"), bias, Label("Anisotropy:"), aniso,
            Label("Shared disk ceiling (MiB):"), budget,psd);
        layout.Controls.Add(quality, 0, 4);
        layout.Controls.Add(Flow(prepare, reset, rebuild, cancel, clear, bypass), 0, 5);
        layout.Controls.Add(progress, 0, 6); layout.Controls.Add(status, 0, 7);
        scan.Click += async (_, _) => await ScanAsync();
        previewButton.Click += async (_, _) => await PreviewAsync();
        prepare.Click += async (_, _) => await ExecuteAsync(PreparationAction.Prepare);
        reset.Click += async (_, _) => await ExecuteAsync(PreparationAction.ResetSelection);
        rebuild.Click += async (_, _) => await ExecuteAsync(PreparationAction.Rebuild);
        clear.Click += async (_, _) => await ExecuteAsync(PreparationAction.ClearOwned);
        bypass.Click += async (_, _) => await ExecuteAsync(PreparationAction.BypassOnce);
        cancel.Click += (_, _) => operation?.Cancel();
        mods.ItemCheck += (_, _) => { if (!changingMods && IsHandleCreated) BeginInvoke((Action)RefreshRows); };
        paths.TextChanged += (_, _) => RefreshRows();
        preset.SelectedIndexChanged += (_,_)=>InvalidatePreview();filter.SelectedIndexChanged += (_,_)=>InvalidatePreview();
        bias.ValueChanged += (_,_)=>InvalidatePreview();aniso.ValueChanged += (_,_)=>InvalidatePreview();budget.ValueChanged += (_,_)=>InvalidatePreview();
        psd.CheckedChanged += (_,_)=>InvalidatePreview();profile.TextChanged += (_,_)=>InvalidatePreview();
        foreach (TextBox input in new[] { game, config, extraLocal, workshop }) input.TextChanged += (_, _) => InvalidateScan();
        FormClosing += (_, e) => { if (operation != null) { operation.Cancel(); e.Cancel = true; status.Text = "Stopping at a safe boundary. Close again after the operation finishes."; } };
        SetBusy(false);
        status.Text = "Scan discovers sources; Preview reads bounded source headers and saved choices without running the encoder or changing caches. Native preservation needs the game's actual producer; external full-size compression still changes pixels. "
            + "Mip bias changes which smaller mip image is sampled; negative favors detail, positive favors softer distant detail. "
            + "Anisotropy controls sampling on angled surfaces. Restart the game after changing selected output.";
    }

    private async Task ScanAsync()
    {
        loadout = null; rows.Clear(); displayed.Clear(); changingMods = true; mods.Items.Clear(); changingMods = false;
        var roots = new List<ModSearchRoot>();
        if (!string.IsNullOrWhiteSpace(extraLocal.Text)) roots.Add(new ModSearchRoot(extraLocal.Text, ModRootKind.Local));
        if (!string.IsNullOrWhiteSpace(workshop.Text)) roots.Add(new ModSearchRoot(workshop.Text, ModRootKind.Workshop));
        var request = new LoadoutRequest(game.Text, config.Text, roots);
        using var cancellation = new CancellationTokenSource(); operation = cancellation; SetBusy(true);
        progress.Style = ProgressBarStyle.Marquee;
        try
        {
            var updates = new Progress<DiscoveryProgress>(p => status.Text = $"Scanning {p.ProvidersCompleted}/{p.ProvidersTotal}: {p.Message}; {p.FilesFound:N0} source rows.");
            var foundData = await Task.Run(() =>
            {
                DiscoveredLoadout scanned = LoadoutDiscovery.Scan(request, cancellation.Token, updates);
                return (Loadout: scanned, Roles: ProvisionalTextureRoles.Read(scanned, cancellation.Token));
            }, cancellation.Token);
            DiscoveredLoadout found = foundData.Loadout; roleHints = foundData.Roles;
            loadout = found; rows.Clear(); changingMods = true; mods.Items.Clear();
            foreach (DiscoveredProvider provider in found.Providers) mods.Items.Add(new ModChoice(provider), true);
            changingMods = false;
            foreach (DiscoveredTexture texture in found.Textures)
            {
                var row = new TextureRow(texture);
                if (texture.EffectiveInProvider) row.Result = roleHints.TryGet(texture.LogicalPath, out var hint)
                    ? "Provisional " + hint.Kind + "; connected source paths: " + hint.Members.Count + "; runtime validation required"
                    : "Native retained: " + roleHints.Reason(texture.LogicalPath);
                rows.Add(RowKey(texture), row);
            }
            operation = null; RefreshRows(); operation = cancellation;
            status.Text = found.CanPrepare
                ? $"Scan complete: {found.Providers.Count:N0} active providers and {found.Textures.Count:N0} image rows. "
                    + "Shadowed providers remain available for their own direct content calls. Double-click a row for its exact source. "
                    + "GPU texture memory is reported only after a validated output descriptor is available; encoded source bytes are not GPU memory."
                : "Preparation blocked by unresolved loadout details:\r\n" + string.Join("\r\n", found.Problems);
            if (found.Notices.Count > 0) status.AppendText("\r\nDiscovery notices:\r\n" + string.Join("\r\n", found.Notices));
        }
        catch (OperationCanceledException) { status.Text = "Scan canceled. Scan again to refresh source selection; no outputs were changed."; }
        catch (Exception ex) { status.Text = "Scan failed: " + ex.Message; }
        finally { changingMods = false; operation = null; progress.Style = ProgressBarStyle.Blocks; SetBusy(false); }
    }

    private async Task PreviewAsync()
    {
        if(backend==null||loadout==null||!loadout.CanPrepare)return;
        var request=new PreparationBatchRequest(loadout,profile.Text,CurrentSelected(),SelectedOptions(),(long)budget.Value*1024*1024,PreparationAction.Prepare,psd.Checked);
        if(request.Selected.Count==0){status.Text="Select at least one texture to preview.";return;}
        using var cancellation=new CancellationTokenSource();operation=cancellation;SetBusy(true);
        try
        {
            var updates=new Progress<PreparationBatchProgress>(p=>{progress.Value=p.Total==0?0:Math.Clamp((int)(100L*p.Completed/p.Total),0,100);status.Text=p.Result+"\r\n"+p.SourcePath;});
            currentPreview=await backend.PreviewAsync(request,updates,cancellation.Token);
            refreshingRows=true;
            foreach(var row in rows.Values)row.Preview=null;
            displayed.RaiseListChangedEvents=false;
            foreach(var item in currentPreview.Rows)
            {
                string key=RowKey(item.Source);
                if(!rows.TryGetValue(key,out var row))rows.Add(key,row=new TextureRow(item.Source){Included=false});
                row.Preview=item;row.Result=item.Reason;row.Required=!item.Requested;
                if(!displayed.Contains(row))displayed.Add(row);
            }
            displayed.RaiseListChangedEvents=true;displayed.ResetBindings();
            foreach(DataGridViewRow row in table.Rows)if(row.DataBoundItem is TextureRow item)row.Cells[0].ReadOnly=item.Required||!item.Texture.EffectiveInProvider;
            table.Refresh();UpdateSelectionInfo();
            status.Text=$"Expanded selection: {currentPreview.Rows.Count:N0} provider textures; {currentPreview.EligibleCount:N0} eligible, {currentPreview.SkippedCount:N0} known skips.\r\n"
                +$"Texture payload {Range(currentPreview.MinimumTextureBytes,currentPreview.MaximumTextureBytes)}; added output storage {Range(currentPreview.MinimumStorageBytes,currentPreview.MaximumStorageBytes)} including record/block metadata.\r\n"
                +$"Conservative additional publication space: up to {Bytes(currentPreview.MaximumAdditionalPublicationBytes)} including a replacement index; existing outputs remain until commit. Actual shared quota is checked before publication.\r\n"
                +"These are representation byte estimates, not measured RAM/VRAM. BC1–BC3 ranges depend on decoded alpha. Full decoding and live role/pair validation can still refuse output. "+PreparationStorageSession.ClearDescription;
        }
        catch(OperationCanceledException){currentPreview=null;status.Text="Preview canceled; no conversion or cache change occurred.";}
        catch(Exception e){currentPreview=null;status.Text="Preview failed: "+e.Message;}
        finally{refreshingRows=false;operation=null;SetBusy(false);}
    }

    private async Task ExecuteAsync(PreparationAction action)
    {
        if (backend == null) { status.Text = "The preparation adapter is not available in this build."; return; }
        if (loadout == null || !loadout.CanPrepare) { status.Text = "Scan a supported loadout first."; return; }
        if (string.IsNullOrWhiteSpace(profile.Text)) { status.Text = "Choose the game's save-data/output profile folder first."; return; }
        var selected = CurrentSelected();
        if (selected.Length == 0 && action != PreparationAction.ClearOwned && action != PreparationAction.BypassOnce)
        { status.Text = "Select at least one effective texture row."; return; }
        var options = new PreparationOptions { Preset = action == PreparationAction.ResetSelection ? 0 : preset.SelectedIndex,
            Filter = filter.SelectedIndex, Anisotropy = (int)aniso.Value, MipBias = (float)bias.Value };
        string saveRoot;
        try { saveRoot = Path.GetFullPath(profile.Text); }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException) { status.Text = "Invalid output profile: " + ex.Message; return; }
        var request = new PreparationBatchRequest(loadout, saveRoot, selected, options,
            (long)budget.Value * 1024 * 1024, action,psd.Checked,
            action==PreparationAction.Prepare||action==PreparationAction.Rebuild?currentPreview:null);
        if((action==PreparationAction.Prepare||action==PreparationAction.Rebuild)&&currentPreview==null)
        {status.Text="Preview the complete selected output before preparing.";return;}
        if(action==PreparationAction.ClearOwned)status.Text=PreparationStorageSession.ClearDescription;
        using var cancellation = new CancellationTokenSource(); operation = cancellation; SetBusy(true);
        try
        {
            var updates = new Progress<PreparationBatchProgress>(p =>
            {
                progress.Value = p.Total == 0 ? 0 : Math.Clamp((int)(100L * p.Completed / p.Total), 0, 100);
                foreach (TextureRow row in rows.Values.Where(r => r.Texture.SourcePath == p.SourcePath)) row.Result = p.Result;
                table.Refresh(); status.Text = $"{p.Completed:N0}/{p.Total:N0}: {p.Result}\r\n{p.SourcePath}\r\n"
                    + $"Owned output {p.StoredBytes:N0} bytes; descriptor texture payload {p.TextureBytes:N0} bytes. This is not a RAM/VRAM measurement.";
            });
            PreparationBatchResult result = await backend.ExecuteAsync(request, updates, cancellation.Token);
            status.Text = $"Prepared {result.Prepared:N0}; reused {result.Reused:N0}; skipped {result.Skipped:N0}; failed {result.Failed:N0}.\r\n{result.Summary}\r\n"
                + $"Owned output {result.StoredBytes:N0} bytes; texture payload {result.TextureBytes:N0} bytes. Restart for actual consumer validation and use.";
        }
        catch (OperationCanceledException) { status.Text = "Canceled. Completed valid entries remain available. Prepare / resume rescans and revalidates them before reuse."; }
        catch (Exception ex) { status.Text = "Preparation stopped: " + ex.Message + "\r\nOriginal artwork remains unchanged."; }
        finally { operation = null; InvalidatePreview();SetBusy(false); }
    }

    private void RefreshRows()
    {
        if (operation != null || changingMods) return;
        string[] selectedPaths = paths.Text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim().Replace('\\', '/')).ToArray();
        validPaths = selectedPaths.Length <= 128 && !selectedPaths.Any(p => Path.IsPathRooted(p) || p.Contains(':') || p.Split('/').Any(s => s == "." || s == "..") || p.IndexOfAny(new[] { '*', '?' }) >= 0);
        if (!validPaths)
        { selectionInfo.Text = "Use up to 128 relative texture files/folders; no wildcards or parent paths."; SetBusy(false); return; }
        var selectedMods = new HashSet<string>(mods.CheckedItems.Cast<ModChoice>().Select(m => m.Provider.Root), StringComparer.OrdinalIgnoreCase);
        refreshingRows=true;InvalidatePreview();baseRows.Clear();
        foreach (TextureRow row in rows.Values)
            if (selectedMods.Contains(row.Texture.Provider.Root) && (selectedPaths.Length == 0 || selectedPaths.Any(p => row.Logical.Equals(p, StringComparison.OrdinalIgnoreCase)
                || row.Logical.StartsWith(p.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))))baseRows.Add(RowKey(row.Texture));
        var chosen=CurrentSelected();var chosenKeys=new HashSet<string>(chosen.Select(RowKey),StringComparer.Ordinal);
        var expanded=loadout!=null&&roleHints!=null?new HashSet<string>(PreparationPreview.Expand(loadout,chosen,roleHints).Select(RowKey),StringComparer.Ordinal):chosenKeys;
        displayed.RaiseListChangedEvents=false;displayed.Clear();
        foreach(TextureRow row in rows.Values)
        {
            string key=RowKey(row.Texture);row.Required=expanded.Contains(key)&&!chosenKeys.Contains(key);
            if(baseRows.Contains(key)||row.Required)displayed.Add(row);
        }
        displayed.RaiseListChangedEvents=true;displayed.ResetBindings();
        foreach(DataGridViewRow row in table.Rows)if(row.DataBoundItem is TextureRow item)row.Cells[0].ReadOnly=item.Required||!item.Texture.EffectiveInProvider;
        refreshingRows=false;UpdateSelectionInfo();SetBusy(false);
    }
    private void UpdateSelectionInfo()
    {
        DiscoveredTexture[] selected=CurrentSelected();TextureRow[] expanded=displayed.Where(r=>r.Use&&r.Texture.EffectiveInProvider).ToArray();
        selectionInfo.Text=$"{selected.Length:N0} requested + {expanded.Count(r=>r.Required):N0} required; expanded sources {Bytes(currentPreview?.SourceBytes??expanded.Sum(r=>r.Texture.SourceBytes))}"
            +(currentPreview==null?"; preview output before preparation":$"; texture {Range(currentPreview.MinimumTextureBytes,currentPreview.MaximumTextureBytes)}, storage {Range(currentPreview.MinimumStorageBytes,currentPreview.MaximumStorageBytes)}");
    }
    private void SetBusy(bool busy)
    {
        scan.Enabled = !busy; cancel.Enabled = busy;
        bool ready = !busy && validPaths && backend != null && loadout?.CanPrepare == true;
        previewButton.Enabled=ready;
        foreach (Button button in new[] { reset, clear, bypass }) button.Enabled = ready;
        prepare.Enabled=ready&&currentPreview!=null;rebuild.Enabled=ready&&currentPreview!=null;
        foreach (Control control in new Control[] { game, config, profile, extraLocal, workshop, paths, mods, preset, filter, bias, aniso, budget, table,psd }) control.Enabled = !busy;
        foreach (Button button in browseButtons) button.Enabled = !busy;
    }
    private void InvalidateScan()
    {
        if (operation != null) return;
        loadout = null; currentPreview=null;baseRows.Clear();rows.Clear(); displayed.Clear(); mods.Items.Clear(); SetBusy(false);
        status.Text = "Loadout locations changed. Scan again before preparing.";
    }
    private void CheckMods(bool value)
    {
        if (operation != null) return;
        changingMods = true; for (int i = 0; i < mods.Items.Count; i++) mods.SetItemChecked(i, value); changingMods = false; RefreshRows();
    }
    private void MarkRows(bool value)
    {
        foreach (DataGridViewRow row in table.SelectedRows) if (row.DataBoundItem is TextureRow texture&&!texture.Required) texture.Included = value && texture.Texture.EffectiveInProvider;
        RefreshRows();
    }
    private void ShowSource(TextureRow row) => status.Text = row.Texture.Provider.Name + " [" + row.Texture.Provider.PackageId + "]\r\n"
        + row.Texture.SourcePath + "\r\nLoad folder: " + row.Texture.LoadFolder + "\r\n" + row.Selection+"; "+row.Inclusion + "\r\n"+row.Output+"; texture "+row.TextureSize+" KiB; storage "+row.StorageSize+" KiB\r\nSaved: "+row.SavedChoice+"\r\n" + row.Result;
    private DiscoveredTexture[] CurrentSelected()=>rows.Values.Where(r=>baseRows.Contains(RowKey(r.Texture))&&r.Included&&r.Texture.EffectiveInProvider).Select(r=>r.Texture).ToArray();
    private PreparationOptions SelectedOptions()=>new PreparationOptions{Preset=preset.SelectedIndex,Filter=filter.SelectedIndex,Anisotropy=(int)aniso.Value,MipBias=(float)bias.Value};
    private void InvalidatePreview(){currentPreview=null;foreach(var row in rows.Values)row.Preview=null;if(operation==null){table.Refresh();UpdateSelectionInfo();SetBusy(false);}}
    private static string Bytes(long value)=>(value/1048576d).ToString("N2")+" MiB";
    private static string Range(long minimum,long maximum)=>minimum==maximum?Bytes(minimum):Bytes(minimum)+"–"+Bytes(maximum);
    private void TextColumn(string title, string property, int width) => table.Columns.Add(new DataGridViewTextBoxColumn
        { HeaderText = title, DataPropertyName = property, Width = width, ReadOnly = true });
    private static string RowKey(DiscoveredTexture texture) => texture.Provider.Root + "\n" + texture.SourcePath;
    private static ComboBox Choice(params string[] choices)
    { var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 255 }; combo.Items.AddRange(choices); combo.SelectedIndex = 0; return combo; }
    private static Label Label(string text) => new Label { Text = text, AutoSize = true, Margin = new Padding(4, 7, 2, 3) };
    private static Button Button(string text, Action action)
    { var button = new Button { Text = text, AutoSize = true }; button.Click += (_, _) => action(); return button; }
    private static FlowLayoutPanel Flow(params Control[] controls)
    { var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true }; panel.Controls.AddRange(controls); return panel; }
    private void AddPath(TableLayoutPanel panel, string title, TextBox box, bool file, bool browse = true)
    {
        int row = panel.RowCount++; panel.Controls.Add(Label(title), 0, row); box.Dock = DockStyle.Fill; panel.Controls.Add(box, 1, row);
        if (!browse) return;
        Button browseButton = Button("Browse…", () =>
        {
            if (file)
            { using var dialog = new OpenFileDialog { Filter = "RimWorld loadout (ModsConfig.xml)|ModsConfig.xml|XML files|*.xml", CheckFileExists = true }; if (dialog.ShowDialog() == DialogResult.OK) box.Text = dialog.FileName; }
            else
            { using var dialog = new FolderBrowserDialog { Description = title, UseDescriptionForTitle = true, ShowNewFolderButton = false }; if (dialog.ShowDialog() == DialogResult.OK) box.Text = dialog.SelectedPath; }
        });
        browseButtons.Add(browseButton); panel.Controls.Add(browseButton, 2, row);
    }
    private sealed record ModChoice(DiscoveredProvider Provider)
    { public override string ToString() => (Provider.Order + 1) + ". " + Provider.Name; }
    private sealed class TextureRow
    {
        public DiscoveredTexture Texture { get; }
        public bool Included { get; set; }
        public bool Required { get; set; }
        public bool Use {get=>Included||Required;set{if(!Required)Included=value&&Texture.EffectiveInProvider;}}
        public string Inclusion=>Required?"Required family/provider":Included?"Requested":"Not selected";
        public PreparationPreviewRow? Preview{get;set;}
        public string Output=>Preview==null?"Preview required":Preview.Image==null?(Preview.Eligible?"Native reset":"Unavailable; see reason"):$"{Preview.Image.OutputWidth}x{Preview.Image.OutputHeight}; {Preview.Image.Mips} mips; {Preview.Image.Representation}";
        public string TextureSize=>Preview?.Image==null?"—":KibRange(Preview.Image.MinimumTextureBytes,Preview.Image.MaximumTextureBytes);
        public string StorageSize=>Preview==null?"—":KibRange(Preview.MinimumStorageBytes,Preview.MaximumStorageBytes);
        public string SavedChoice=>Preview?.SavedChoice??"Preview with a save-data profile";
        public string Mod => Texture.Provider.Name;
        public string Logical => Texture.LogicalPath;
        public string Selection => !Texture.EffectiveInProvider ? "Skipped by native selection" : Texture.GlobalWinner ? "Global winner + own provider" : "Own provider; globally shadowed";
        public string Kib => ((Preview?.SourceBytes??Texture.SourceBytes) / 1024d).ToString("N1");
        public string Result { get; set; }
        public TextureRow(DiscoveredTexture texture)
        {
            Texture = texture; Included = texture.EffectiveInProvider;
            Result = texture.Reason.Length > 0 ? texture.Reason : texture.AuthoredDds
                ? "Authored DDS already uses native route; conversion is a separate quality choice"
                : "Role not yet verified in game; conversion remains provisional";
        }
        private static string KibRange(long minimum,long maximum)=>minimum==maximum?(minimum/1024d).ToString("N1"):(minimum/1024d).ToString("N1")+"–"+(maximum/1024d).ToString("N1");
    }
}
