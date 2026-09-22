// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace WakeUp.Preparation;

public sealed class StandalonePreparationBackend : IPreparationBackend
{
    private readonly string helperPath;
    public StandalonePreparationBackend(string? helperPath = null)
        => this.helperPath = Path.GetFullPath(helperPath ?? Path.Combine(AppContext.BaseDirectory, "Tools", "win-x64", "WakeUp.TextureHelper.exe"));
    public Task<PreparationBatchPreview> PreviewAsync(PreparationBatchRequest request,
        IProgress<PreparationBatchProgress> progress,CancellationToken cancellation)
        => Task.Run(()=>BuildPreview(request,progress,cancellation),cancellation);
    public Task<PreparationBatchResult> ExecuteAsync(PreparationBatchRequest request,
        IProgress<PreparationBatchProgress> progress, CancellationToken cancellation)
        => Task.Run(() => Execute(request, progress, cancellation), cancellation);

    private PreparationBatchResult Execute(PreparationBatchRequest request, IProgress<PreparationBatchProgress> progress, CancellationToken cancellation)
    {
        if (!request.Options.Valid || request.SharedCeilingBytes < 1024*1024 || request.SharedCeilingBytes > 65536L*1024*1024)
            throw new InvalidDataException("Invalid quality options or shared disk ceiling");
        string save = Path.GetFullPath(request.SaveDataRoot);
        OwnedCacheStore.RejectLinkedPath(save);
        if (Inside(save, request.Loadout.GameRoot) || request.Loadout.Providers.Any(p => Inside(save,p.Root)))
            throw new InvalidDataException("Choose a save-data/output profile outside game and mod source folders");
        cancellation.ThrowIfCancellationRequested();
        int budgetMiB = checked((int)(request.SharedCeilingBytes/(1024*1024)));
        if (request.Action == PreparationAction.ClearOwned || request.Action == PreparationAction.BypassOnce)
        {
            WriteRequest(save,budgetMiB,null,request.Action == PreparationAction.BypassOnce,request.Action == PreparationAction.ClearOwned);
            return new PreparationBatchResult(0,0,0,0,0,0,request.Action == PreparationAction.ClearOwned
                ? "Clear requested for the next game launch. "+PreparationStorageSession.ClearDescription
                : "One-launch bypass requested. Existing outputs and feature enablement are unchanged.");
        }
        PreparationBatchPreview preview=BuildPreview(request,null,cancellation);
        if(request.ApprovedPreview!=null&&preview.Identity!=request.ApprovedPreview.Identity)
            throw new InvalidDataException("Selection, source contents, helper or eligibility changed after preview; preview again");
        DiscoveredLoadout fresh=preview.Loadout;
        var requestedSources = new HashSet<string>(request.Selected.Select(Key),StringComparer.Ordinal);
        var groups=preview.Rows.Where(r=>r.Eligible).GroupBy(r=>r.Group,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.Select(r=>r.Source).ToList(),StringComparer.Ordinal);
        var planned=preview.Rows.ToDictionary(r=>Key(r.Source),StringComparer.Ordinal);
        // Reset uses the same group expansion, but can also remove an obsolete
        // selected record whose original Def no longer exposes a role.
        bool reset = request.Action == PreparationAction.ResetSelection || request.Options.Preset == 0;
        int total = preview.Rows.Count, completed=0, prepared=0,reused=0,skipped=0,failed=0;
        long textureBytes=0,storedBytes=0;
        foreach(PreparationPreviewRow row in preview.Rows.Where(r=>!r.Eligible))
        {skipped++;progress.Report(new PreparationBatchProgress(++completed,total,row.Source.SourcePath,"Native retained: "+row.Reason));}
        if(groups.Count==0)return new PreparationBatchResult(0,0,skipped,0,0,0,"No eligible output in the reviewed selection; no conversion, activation or storage publication occurred.");
        // Explicit preparation enables the next-launch consumer. A native/reset
        // action removes chosen conversions without enabling a feature itself.
        WriteRequest(save,budgetMiB,reset ? null : true,false,false);
        using var store = new PreparationStorageSession(save,budgetMiB,action: request.Action == PreparationAction.Rebuild ? "rebuild" : "normal");
        string helper = preview.HelperIdentity, active = Active(fresh);
        foreach (List<DiscoveredTexture> group in groups.Values)
        {
            cancellation.ThrowIfCancellationRequested();
            if (group.Count > 512) { FailGroup(group,"Complete provider group exceeds 512 members",true); continue; }
            foreach (DiscoveredTexture added in group.Where(t=>!requestedSources.Contains(Key(t))))
                progress.Report(new PreparationBatchProgress(completed,total,added.SourcePath,"Required paired/provider member included with the same quality and sampling choice"));
            try
            {
                ValidateMetadata(fresh);
                if (reset)
                {
                    foreach (DiscoveredTexture source in group)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (!store.Reset(Provider(source.Provider),Logical(source))) throw new IOException("Native reset refused: "+store.LastReason);
                        progress.Report(new PreparationBatchProgress(++completed,total,source.SourcePath,"Reset to ordinary native pixels and sampling on restart; uncaptured native output cannot be produced outside the game",store.StoredBytes,textureBytes));
                        prepared++;
                    }
                    continue;
                }
                var leases = new List<FileStream>(); var records = new List<PreparationRecord>(); var reuse = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    long sourceBytes=0,groupBytes=0;
                    foreach (DiscoveredTexture source in group)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        PreparationPreviewRow plannedRow=planned[Key(source)];
                        ValidateSelection(source);
                        var lease = new FileStream(source.SourcePath,FileMode.Open,FileAccess.Read,FileShare.Read);leases.Add(lease);
                        byte[] bytes = ReadSource(lease);
                        if ((sourceBytes+=bytes.Length)>96L*1024*1024) throw new InvalidDataException("Connected source snapshots exceed the 96 MiB preparation bound");
                        if(PreparationContract.Hash(bytes)!=plannedRow.SourceDigest)throw new IOException("Source changed after output preview");
                        PreparationImageEstimate inspection=PreparationImageInspection.Inspect(bytes,request.Options,plannedRow.Role=="mask",request.PsdEnabled);
                        if(!inspection.Eligible)throw new InvalidDataException(inspection.Reason);
                        int w=inspection.Width,h=inspection.Height;
                        var record = new PreparationRecord {Provider=Provider(source.Provider),Logical=Logical(source),Physical=source.SourcePath,
                            SourceDigest=PreparationContract.Hash(bytes),Active=active,Runtime=PreparationContract.RuntimeGog,Helper=helper,Role=plannedRow.Role,
                            Options=new PreparationOptions{Preset=request.Options.Preset,Filter=request.Options.Filter,Anisotropy=request.Options.Anisotropy,MipBias=request.Options.MipBias}};
                        PreparationRecord? existing=store.Read(record.Provider,record.Logical);
                        if(existing!=null && Same(existing,record)) { record.Output=existing.Output;reuse.Add(Key(source)); }
                        else record.Output=PreparationEncoder.Convert(helperPath,helper,bytes,w,h,record.Options,plannedRow.Role=="mask",cancellation);
                        if ((groupBytes+=record.Output.Pixels.Length)>96L*1024*1024) throw new InvalidDataException("Complete converted group exceeds the 96 MiB private-output bound");
                        // The runtime validates actual color/mask pair edges and
                        // provider winners. Unrelated directional dimensions may
                        // differ within the same connected group.
                        records.Add(record);
                    }
                    ValidateMetadata(fresh);
                    for(int i=0;i<group.Count;i++)
                    {
                        cancellation.ThrowIfCancellationRequested();ValidateSelection(group[i]);leases[i].Position=0;
                        if(PreparationContract.Hash(ReadSource(leases[i]))!=records[i].SourceDigest)throw new IOException("Source contents changed during preparation");
                    }
                    if(PreparationEncoder.Identity(helperPath)!=helper)throw new IOException("Helper identity changed");
                    // Shared store replaces its index once for the entire group.
                    if(!store.PublishGroup(records,cancellation))throw new IOException("Atomic group publication refused: "+store.LastReason);
                    for(int i=0;i<group.Count;i++)
                    {
                        bool wasReused=reuse.Contains(Key(group[i]));if(wasReused)reused++;else prepared++;
                        textureBytes+=records[i].Output.Pixels.Length;
                        progress.Report(new PreparationBatchProgress(++completed,total,group[i].SourcePath,
                            (wasReused?"Reused":"Prepared")+" provisional "+records[i].Role+" "+records[i].Output.Width+"x"+records[i].Output.Height+"; live group validation required on restart",store.StoredBytes,textureBytes));
                    }
                }
                finally {foreach(FileStream lease in leases)lease.Dispose();}
            }
            catch(OperationCanceledException){throw;}
            catch(Exception ex) when(ex is IOException || ex is ArgumentException || ex is NotSupportedException || ex is OverflowException || ex is UnauthorizedAccessException)
            {FailGroup(group,ex.Message,ex is NotSupportedException || ex is InvalidDataException);}
        }
        store.Complete();storedBytes=store.StoredBytes;
        return new PreparationBatchResult(prepared,reused,skipped,failed,storedBytes,textureBytes,
            reset?"Selected quality entries reset. Native output is restored on restart; no external Unity-native capture was created."
                :"Completed records are in Wake-Up's shared texture store. Restart with Wake-Up to validate actual consumers and use supported groups. Unknown/patched roles and failed groups retain native output."
                    +(preview.Notices.Count>0?" Metadata preview notices: "+preview.Notices.Count+".":""));
        void FailGroup(IEnumerable<DiscoveredTexture> members,string reason,bool skip)
        {foreach(DiscoveredTexture source in members){if(skip)skipped++;else failed++;progress.Report(new PreparationBatchProgress(++completed,total,source.SourcePath,"Native retained; group not published: "+reason,store.StoredBytes,textureBytes));}}
    }
    private PreparationBatchPreview BuildPreview(PreparationBatchRequest request,IProgress<PreparationBatchProgress>? progress,CancellationToken cancellation)
    {
        DiscoveredLoadout fresh=LoadoutDiscovery.Scan(request.Loadout.Request??throw new InvalidDataException("Loadout request missing; scan again"),cancellation);
        if(!fresh.CanPrepare)throw new InvalidDataException(string.Join("; ",fresh.Problems));
        if(Active(fresh)!=Active(request.Loadout)||fresh.ModsConfigSha256!=request.Loadout.ModsConfigSha256)
            throw new InvalidDataException("Loadout providers, folders or configuration changed; scan again");
        var keys=new HashSet<string>(request.Selected.Select(Key),StringComparer.Ordinal);
        var selected=fresh.Textures.Where(t=>t.EffectiveInProvider&&keys.Contains(Key(t))).ToArray();
        if(selected.Length!=keys.Count)throw new InvalidDataException("Native source selection changed; scan again");
        PreparationOptions options=PreparationPreview.Copy(request.Options);
        if(request.Action==PreparationAction.ResetSelection)options.Preset=0;
        string helper="",problem="";
        if(options.Preset!=0)try{helper=PreparationEncoder.Identity(helperPath);}
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is NotSupportedException){problem="Encoder unavailable: "+e.Message;}
        var roles=ProvisionalTextureRoles.Read(fresh,cancellation);
        PreparationBatchPreview preview=PreparationPreview.Create(fresh,selected,options,request.PsdEnabled,helper,problem,roles,cancellation,progress);
        if(!string.IsNullOrWhiteSpace(request.SaveDataRoot))
        {
            var snapshots=PreparationStorageSession.Inspect(request.SaveDataRoot,preview.Rows.Select(r=>(Provider(r.Source.Provider),Logical(r.Source))))
                .ToDictionary(s=>PreparationContract.Slot(s.Provider,s.Logical),StringComparer.Ordinal);
            foreach(var row in preview.Rows)
            {
                var snapshot=snapshots[PreparationContract.Slot(Provider(row.Source.Provider),Logical(row.Source))];
                if(snapshot.Choice==null){row.SavedChoice=snapshot.Reason=="selection-missing"?"No saved quality choice":snapshot.Reason;continue;}
                var choice=snapshot.Choice;
                row.SavedChoice=snapshot.Description;
                string stale=choice.Physical!=row.Source.SourcePath?"source provider changed":choice.Active!=Active(fresh)?"active providers changed"
                    :choice.Runtime!=PreparationContract.RuntimeGog?"game/runtime contract changed"
                    :row.SourceDigest.Length>0&&choice.SourceDigest!=row.SourceDigest?"source contents changed"
                    :options.Preset!=0&&choice.Helper!=helper?"encoder changed or unavailable"
                    :row.Role.Length>0&&choice.Role!=row.Role?"provisional role changed":"";
                row.SavedChoice+=stale.Length>0?"; not applied: "+stale:"; completed output retained; live consumer validation still required";
            }
        }
        return preview;
    }
    public static string Provider(DiscoveredProvider p) => PreparationContract.Frame("native-selection-v2",p.PackageId,Path.GetFullPath(p.Root),PreparationContract.Frame(p.Folders.Select(Path.GetFullPath).ToArray()));
    public static string Active(DiscoveredLoadout loadout)=>PreparationContract.Frame(loadout.Providers.Select((p,i)=>PreparationContract.Frame(i.ToString(CultureInfo.InvariantCulture),p.PackageId,Path.GetFullPath(p.Root),PreparationContract.Frame(p.Folders.Select(Path.GetFullPath).ToArray()))).ToArray());
    private static string Logical(DiscoveredTexture t)=>t.LogicalPath.Replace('/',Path.DirectorySeparatorChar);
    private static string Key(DiscoveredTexture t)=>t.Provider.Root+"\n"+t.SourcePath;
    private static bool Same(PreparationRecord a,PreparationRecord b)=>a.Provider==b.Provider&&a.Logical==b.Logical&&a.Physical==b.Physical&&a.SourceDigest==b.SourceDigest&&a.Active==b.Active&&a.Runtime==b.Runtime&&a.Helper==b.Helper&&a.Role==b.Role&&a.Options.Identity==b.Options.Identity;
    private static bool Inside(string path,string root)=>path.Equals(Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase)||path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    private static byte[] ReadSource(Stream input)
    {if(input.Length<1||input.Length>PreparationEncoder.MaximumSource)throw new InvalidDataException("Encoded source exceeds 16 MiB");var bytes=new byte[(int)input.Length];int at=0;while(at<bytes.Length){int count=input.Read(bytes,at,bytes.Length-at);if(count==0)throw new EndOfStreamException();at+=count;}if(input.ReadByte()!=-1)throw new IOException("Source length changed");return bytes;}
    private static void ValidateMetadata(DiscoveredLoadout loadout)
    {
        if(!FileHash(loadout.ModsConfigPath).Equals(loadout.ModsConfigSha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Loadout configuration changed");
        foreach(DiscoveredProvider provider in loadout.Providers)
        {
            if(!FileHash(Path.Combine(provider.Root,"About","About.xml")).Equals(provider.MetadataSha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Provider metadata changed");
            string folder=Path.Combine(provider.Root,"LoadFolders.xml");string hash=File.Exists(folder)?FileHash(folder):"absent";
            if(!hash.Equals(provider.LoadFoldersSha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Load-folder metadata changed");
        }
    }
    private static string FileHash(string path){using var stream=File.OpenRead(path);using var hash=System.Security.Cryptography.SHA256.Create();return Convert.ToHexString(hash.ComputeHash(stream));}
    private static void ValidateSelection(DiscoveredTexture source)
    {
        string logical=Logical(source);string? selected=source.Provider.Folders.Select(f=>Path.GetFullPath(Path.Combine(f,logical))).FirstOrDefault(File.Exists);
        if(selected!=source.SourcePath)throw new IOException("Provider selected source changed");
        if(logical.EndsWith(".dds",StringComparison.OrdinalIgnoreCase))return;
        string sibling=logical.Substring(0,logical.Length-4)+".dds";
        foreach(string folder in source.Provider.Folders)
        {string directory=Path.Combine(folder,Path.GetDirectoryName(sibling)!);if(Directory.Exists(directory)&&Directory.EnumerateFiles(directory).Any(p=>Path.GetFileName(p).Equals(Path.GetFileName(sibling),StringComparison.OrdinalIgnoreCase)))throw new IOException("A new authored DDS overrides this source");}
    }
    private static void WriteRequest(string save,int budgetMiB,bool? enabled,bool bypass,bool clear)
    {
        using var budget=new SharedCacheBudget(save,budgetMiB*1024L*1024);string root=Path.Combine(save,"WakeUp","PreparedTextures"),path=Path.Combine(root,"request.xml");
        OwnedCacheStore.RejectLinkedPath(path);XElement request=new XElement("WakeUpPreparationRequest",new XAttribute("version","1"));
        if(File.Exists(path))
        {
            using var stream=File.OpenRead(path);if(stream.Length>4096)throw new InvalidDataException("Preparation request size");
            using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096});
            request=XElement.Load(reader);if(request.Name!="WakeUpPreparationRequest")throw new InvalidDataException("Preparation request version");
        }
        if(enabled.HasValue)request.SetElementValue("enabled",enabled.Value?"true":"false");
        request.SetElementValue("sharedCacheMiB",budgetMiB.ToString(CultureInfo.InvariantCulture));
        request.SetElementValue("bypassOnce",bypass?"true":"false");request.SetElementValue("clearOnce",clear?"true":"false");
        byte[] bytes=Encoding.UTF8.GetBytes(request.ToString(SaveOptions.DisableFormatting));if(bytes.Length>4096)throw new InvalidDataException("Preparation request size");
        string temporary=Path.Combine(root,".request.pending-"+Guid.NewGuid().ToString("N"));
        lock(budget.Gate)
        using(budget.Reserve(bytes.Length))
        {
            try {using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
            finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
