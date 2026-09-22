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

internal sealed class TextureQualityWindow : Window
{
    private sealed class Row
    {
        internal PreparedQualityRuntime.Source Source = null!;
        internal PreparedTextureRoles.Role? Role;
        internal string Reason = "";
        internal string SavedChoice = "Saved selection not inspected";
        internal bool SelectedDirectly;
    }
    private readonly WakeUpSettings settings;
    private readonly HashSet<ModContentPack> providers;
    private readonly string paths;
    private readonly PreparationOptions options = new() { Preset=1 };
    private readonly List<Row> rows = new();
    private Vector2 scroll;
    private string status = "Scan to see current consumer roles and complete color/mask selection.", active="", helper="", helperPath="";
    private PreparedTextureRoles? roles;
    private PreparedTextureStore? store;
    private CancellationTokenSource? cancellation;
    private Task<PreparationPixels>? worker;
    private PreparationRecord? pending;
    private int cursor, prepared, reused, failed;
    private bool running;
    private long outputBytes;
    private long currentTextureBytes, estimatedMinimum, estimatedMaximum;
    private readonly List<(Row Row,PreparationRecord Record,bool Changed)> staged=new();
    private string stagedGroup="";
    private bool groupFailed;
    public override Vector2 InitialSize => new(940, 780);
    internal TextureQualityWindow(WakeUpSettings settings, IEnumerable<ModContentPack> providers, string paths)
    {
        this.settings=settings; this.providers=new HashSet<ModContentPack>(providers); this.paths=paths;
        doCloseX=true; doCloseButton=true; absorbInputAroundWindow=true; forcePause=true;
    }
    public override void DoWindowContents(Rect rect)
    {
        var list = new Listing_Standard(); list.Begin(new Rect(0,0,rect.width,rect.height-60));
        list.Label("Quality for the selected mods/files. Changes apply on the next ordinary launch. Full-size compression also changes pixels. Originals and authored DDS remain intact.");
        if (!running)
        {
            string[] names = { "Native quality (reset selected overrides)", "High-quality compression — full-size, lossy", "Reduced dimensions — half width/height", "Reduced dimensions — quarter width/height" };
            if (list.ButtonText(names[options.Preset])) Find.WindowStack.Add(new FloatMenu(names.Select((name,i) =>
                new FloatMenuOption(name, () => { options.Preset=i; rows.Clear(); })).ToList()));
            if (options.Preset != 0)
            {
                string[] filters={ "Point sampling", "Bilinear sampling", "Trilinear sampling" };
                if (list.ButtonText(filters[options.Filter])) Find.WindowStack.Add(new FloatMenu(filters.Select((name,i) =>
                    new FloatMenuOption(name, () => options.Filter=i)).ToList()));
                list.Label("Mip bias: " + options.MipBias.ToString("F2") + " (negative favors detail; positive favors smaller mip levels)");
                options.MipBias = (float)Math.Round(list.Slider(options.MipBias,-3,3)*4)/4;
                list.Label("Anisotropic sampling: " + options.Anisotropy + " (detail at oblique angles)");
                options.Anisotropy=(int)Math.Round(list.Slider(options.Anisotropy,0,16));
            }
            if (list.ButtonText("Scan effective selection and required paired sources")) Scan();
            if (rows.Count != 0 && list.ButtonText(options.Preset==0 ? "Reset these selections to ordinary native output" : "Prepare / resume and enable on next launch")) Start();
        }
        else if (list.ButtonText("Cancel and keep completed work")) cancellation!.Cancel();
        list.Label(status);
        list.Label($"Rows {rows.Count}; prepared {prepared}; reused {reused}; failed {failed}; visited {cursor}/{rows.Count}. Completed texture payload: {outputBytes/1048576d:F2} MiB (disk compression and GPU allocation differ).");
        list.Label($"Current loaded mip payload: {currentTextureBytes/1048576d:F2} MiB. Chosen output estimate: {estimatedMinimum/1048576d:F2}–{estimatedMaximum/1048576d:F2} MiB before disk compression; originals, metadata and replacement space are additional.");
        list.Label("Paired masks retain independent RGBA channels; resizing changes their detail. Related sources from every provider are shown and use the same choice. Unknown/conflicting shaders remain native. Reset restores ordinary sampling too. One bounded helper at a time; no preparation time estimate is available.");
        list.Label("Saved choices below persist across ordinary cache cleanup. Pending controls above replace them only after successful preparation. Source and consumer compatibility are checked again when loading.");
        float top=list.CurHeight; list.End();
        var area=new Rect(0,top,rect.width,Math.Max(40,rect.height-top-55));
        const int rowHeight=84;
        var content=new Rect(0,0,area.width-20,Math.Max(area.height,rows.Count*rowHeight));
        Widgets.BeginScrollView(area,ref scroll,content);
        int first=Math.Max(0,(int)(scroll.y/rowHeight)), end=Math.Min(rows.Count,first+(int)(area.height/rowHeight)+2);
        for(int i=first;i<end;i++)
        {
            Widgets.Label(new Rect(0,i*rowHeight,content.width,28),rows[i].Source.Provider.Name+": "+rows[i].Source.Logical);
            Widgets.Label(new Rect(0,i*rowHeight+28,content.width,28),rows[i].SavedChoice);
            Widgets.Label(new Rect(0,i*rowHeight+56,content.width,28),rows[i].Reason);
        }
        Widgets.EndScrollView();
    }
    private void Scan()
    {
        try
        {
            rows.Clear(); roles=PreparedTextureRoles.Capture();
            currentTextureBytes=estimatedMinimum=estimatedMaximum=0;
            active=PreparedTextureRuntime.ActiveIdentity();
            var sources=PreparedQualityRuntime.Sources();
            var winners=sources.GroupBy(s=>PreparationContract.Stem(s.Logical),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Last(),StringComparer.Ordinal);
            var direct=sources.Where(s=>providers.Contains(s.Provider)&&TexturePreparationBatch.Matches(s.Logical,paths)).ToArray();
            var selectedSources=new HashSet<PreparedQualityRuntime.Source>(direct);
            var groups=new HashSet<string>(StringComparer.Ordinal);
            foreach(var s in direct) if(roles.TryGet(s.Logical,out var role)) groups.Add(role.Group);
            foreach(var s in sources)
            {
                bool selected=selectedSources.Contains(s);
                roles.TryGet(s.Logical,out var role);
                if(!selected && (role==null || !groups.Contains(role.Group))) continue;
                var row=new Row {Source=s,Role=role,SelectedDirectly=selected};
                row.Reason=role==null ? "native: "+roles.Reason(s.Logical)
                    : role.Kind+ (selected ? "; selected" : "; required related source") + "; source freshness checked during preparation";
                var loaded=s.Provider.GetContentHolder<Texture2D>().Get(PreparationContract.Stem(s.Logical));
                if(loaded!=null)
                {
                    int currentBytes=NativeTextureData.ExpectedBytes(loaded.width,loaded.height,(int)loaded.format,loaded.mipmapCount);
                    if(currentBytes>0) currentTextureBytes+=currentBytes;
                }
                if(role!=null && options.Preset!=0)
                {
                    try
                    {
                        bool mask=role.Kind==PreparedTextureRoles.RoleKind.Mask;
                        var estimate=PreparationImageInspection.Inspect(PreparedTextureRuntime.ReadSource(s.File),options,mask,PreparedTextureRuntime.PsdSupport);
                        if(!estimate.Eligible) throw new InvalidDataException(estimate.Reason);
                        estimatedMinimum+=estimate.MinimumTextureBytes;estimatedMaximum+=estimate.MaximumTextureBytes;
                        row.Reason+=$"; {estimate.Width}x{estimate.Height} → {estimate.OutputWidth}x{estimate.OutputHeight}; {estimate.Representation}";
                    }
                    catch(Exception e) {row.Reason+="; preparation gap: "+e.Message;}
                }
                bool global=ReferenceEquals(s,winners[PreparationContract.Stem(s.Logical)]);
                row.Reason+=global?"; global winner":"; own holder (shadowed globally)";
                rows.Add(row);
            }
            var saved=PreparationStorageSession.Inspect(PreparedTextureRuntime.SaveRoot,rows.Select(r=>(r.Source.Selection,r.Source.Logical)));
            for(int i=0;i<rows.Count;i++) rows[i].SavedChoice=saved[i].Description;
            rows.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Role?.Group??"",b.Role?.Group??""));
            status=$"Effective selection: {direct.Length} requested, {rows.Count-direct.Length} related provider sources. {roles.Status}. Authored DDS is already native; explicit conversion changes its representation.";
        }
        catch(Exception e) { status="Scan unavailable: "+e.Message; }
    }
    private void Start()
    {
        try
        {
            if(!settings.Enabled || Current.ProgramState!=ProgramState.Entry || LongEventHandler.AnyEventNowOrWaiting)
                throw new InvalidOperationException("Preparation requires enabled Wake-Up at an idle main menu");
            Scan();
            if(rows.Count==0) return;
            if(options.Preset!=0 && QualitySettings.activeColorSpace!=ColorSpace.Gamma)
                throw new NotSupportedException("Quality conversion requires the qualified Gamma color-space player");
            store=new PreparedTextureStore(PreparedTextureRuntime.SaveRoot,compress:settings.CompressTextureStorage);
            if(options.Preset==0)
            {
                failed=0;
                foreach(var row in rows)
                {
                    if(store.ResetQuality(row.Source.Selection,row.Source.Logical))
                    { row.Reason="native reset; restart to use ordinary pixels and sampling"; row.SavedChoice="Saved: native quality (no override)"; }
                    else {failed++;row.Reason="reset failed: "+store.LastReason;}
                }
                store.Complete(); store.Dispose(); store=null;
                status=failed==0 ? "Selected overrides reset. Existing live textures change after restart."
                    : failed+" overrides could not be reset; retry after resolving storage availability."; return;
            }
            helperPath=TexturePreparationHelper.ExactPath(PreparedTextureRuntime.ModRoot);
            helper=PreparationEncoder.Identity(helperPath);
            cancellation=new CancellationTokenSource(); cursor=prepared=reused=failed=0; outputBytes=0; running=true;
            status="Preparing verified roles. Complete groups will be used on the next launch; missing members retain native output.";
        }
        catch(Exception e) { Stop(); status="Preparation unavailable: "+e.Message; }
    }
    public override void WindowUpdate()
    {
        base.WindowUpdate();
        if(!running) return;
        try
        {
            if(worker!=null)
            {
                if(!worker.IsCompleted) return;
                var completed=worker; worker=null;
                pending!.Output=completed.GetAwaiter().GetResult();
                var row=rows[cursor-1];
                Revalidate(row,pending);
                cancellation!.Token.ThrowIfCancellationRequested();
                if(staged.Sum(s=>(long)s.Record.Output.Pixels.Length)+pending.Output.Pixels.Length>NativeTextureData.MaximumPixels)
                    throw new InvalidDataException("complete group exceeds 96 MiB private bound");
                staged.Add((row,pending,true));
                row.Reason="encoded privately; waiting for complete group publication";
                pending=null;
            }
            if(cancellation!.IsCancellationRequested) { Stop(); return; }
            if(cursor>=rows.Count) { CompleteGroup(); Stop(); return; }
            var current=rows[cursor];
            if(current.Role==null) {cursor++;return;}
            if(stagedGroup!=current.Role.Group) { CompleteGroup(); stagedGroup=current.Role.Group; groupFailed=false; }
            cursor++;
            byte[] source=PreparedTextureRuntime.ReadSource(current.Source.File);
            var header=PreparationImageInspection.Inspect(source,options,current.Role.Kind==PreparedTextureRoles.RoleKind.Mask,PreparedTextureRuntime.PsdSupport);
            if(!header.Eligible) throw new InvalidDataException(header.Reason);
            var record=new PreparationRecord { Provider=current.Source.Selection,Logical=current.Source.Logical,
                Physical=current.Source.File.FullName,SourceDigest=PreparationContract.Hash(source),Active=active,
                Runtime=PreparationContract.RuntimeGog,Helper=helper,Role=current.Role.Kind.ToString().ToLowerInvariant(),
                RoleIdentity=current.Role.Identity,
                Options=new PreparationOptions {Preset=options.Preset,Filter=options.Filter,Anisotropy=options.Anisotropy,MipBias=options.MipBias} };
            Revalidate(current,record);
            var previous=store!.ReadQuality(record.Provider,record.Logical);
            if(previous!=null && previous.SourceDigest==record.SourceDigest && previous.Physical==record.Physical && previous.Active==active
                && previous.Runtime==record.Runtime && previous.Helper==helper && previous.Role==record.Role && previous.RoleIdentity==record.RoleIdentity && previous.Options.Identity==record.Options.Identity)
            { if(staged.Sum(s=>(long)s.Record.Output.Pixels.Length)+previous.Output.Pixels.Length>NativeTextureData.MaximumPixels)
                    throw new InvalidDataException("complete group exceeds 96 MiB private bound");
                staged.Add((current,previous,false)); current.Reason="valid previous output; checking complete group"; return; }
            pending=record;
            var token=cancellation.Token;
            worker=Task.Run(()=>PreparationEncoder.Convert(helperPath,helper,source,header.Width,header.Height,record.Options,record.Role=="mask",token));
        }
        catch(OperationCanceledException) { Stop(); }
        catch(Exception e)
        {
            failed++; status=e.Message;
            groupFailed=true;
            if(cursor>0) rows[cursor-1].Reason="native fallback: "+e.Message;
            pending=null;
            if(cancellation?.IsCancellationRequested==true) Stop();
        }
    }
    private void CompleteGroup()
    {
        if(staged.Count==0) return;
        try
        {
            if(groupFailed) {foreach(var item in staged) item.Row.Reason="previous group retained: another member failed";return;}
            long bytes=0;
            foreach(var item in staged)
            { Revalidate(item.Row,item.Record); bytes+=item.Record.Output.Pixels.Length; }
            if(bytes>NativeTextureData.MaximumPixels) throw new InvalidDataException("complete group exceeds 96 MiB private bound");
            cancellation!.Token.ThrowIfCancellationRequested();
            if(staged.Any(s=>s.Changed) && !store!.PublishQualityGroup(staged.Select(s=>s.Record).ToArray(),cancellation.Token))
                throw new IOException("previous group retained: "+store.LastReason);
            foreach(var item in staged)
            {
                if(item.Changed) prepared++; else reused++;
                outputBytes+=item.Record.Output.Pixels.Length;
                item.Row.SavedChoice=PreparationChoiceSnapshot.Describe(item.Record);
                item.Row.Reason=(item.Changed?"prepared ":"reused ")+item.Record.Output.Width+"x"+item.Record.Output.Height+"; "+item.Record.Output.Mips+" mips; restart to apply";
            }
            settings.PreparedTextures=true;settings.Write();
        }
        finally {staged.Clear();}
    }
    private void Revalidate(Row row,PreparationRecord record)
    {
        var currentRoles=PreparedTextureRoles.Capture();
        if(!settings.Enabled || Current.ProgramState!=ProgramState.Entry || LongEventHandler.AnyEventNowOrWaiting
            || active!=PreparedTextureRuntime.ActiveIdentity() || roles==null || !currentRoles.TryGet(row.Source.Logical,out var role)
            || role.Identity!=row.Role!.Identity || helper!=PreparationEncoder.Identity(helperPath)
            || !TexturePreparationBatch.StillSelected(row.Source.Provider.foldersToLoadDescendingOrder.ToArray(),row.Source.Logical,row.Source.File.FullName)
            || PreparationContract.Hash(PreparedTextureRuntime.ReadSource(row.Source.File))!=record.SourceDigest)
            throw new InvalidDataException("source, role, provider or helper changed; rescan");
    }
    private void Stop()
    {
        cancellation?.Cancel();
        if(worker!=null) { try {worker.GetAwaiter().GetResult();} catch(Exception) {} worker=null; }
        staged.Clear(); stagedGroup=""; groupFailed=false;
        try {store?.Complete();} finally {store?.Dispose();store=null;cancellation?.Dispose();cancellation=null;pending=null;running=false;}
        status="Stopped. Completed output remains; resume rechecks current sources. Restart to apply complete selections, or reset to native.";
    }
    public override void PostClose() {Stop();base.PostClose();}
}
