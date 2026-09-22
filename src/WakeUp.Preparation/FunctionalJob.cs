// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace WakeUp.Preparation;

// Headless functional entry into the exact same discovery and preparation path.
// Normal startup remains the standalone window. This does not verify the window.
internal static class FunctionalJob
{
    private sealed class Job
    {
        public Job() { }
        public string GameRoot { get; set; } = "";
        public string ModsConfigPath { get; set; } = "";
        public string SaveDataRoot { get; set; } = "";
        public List<ModSearchRoot> AdditionalRoots { get; set; } = new List<ModSearchRoot>();
        public string[] Paths { get; set; } = Array.Empty<string>();
        public int Preset { get; set; }
        public int Filter { get; set; } = 2;
        public int Aniso { get; set; } = 2;
        public float MipBias { get; set; }
        public string Action { get; set; } = "prepare";
        public int BudgetMiB { get; set; } = 1024;
        public int? CancelAfterCompleted { get; set; }
        public bool PreviewOnly { get; set; }
        public bool PsdEnabled { get; set; }
    }
    private sealed class SourceReceipt
    {
        public string SourcePath { get; init; } = "";
        public string LogicalPath { get; init; } = "";
        public string Provider { get; init; } = "";
        public string SourceBefore { get; init; } = "";
        public string SourceAfter { get; set; } = "";
    }
    private sealed class DirectProgress<T> : IProgress<T>
    {
        private readonly Action<T> action;
        public DirectProgress(Action<T> action) => this.action = action;
        public void Report(T value) => action(value);
    }
    internal static int Run(string[] args)
    {
        if(args.Length!=4||args[0]!="--functional-job"||args[2]!="--result")return 2;
        string resultPath=Path.GetFullPath(args[3]);
        string action="",error="",assembly="",before="",after="";bool success=false,canceled=false;
        PreparationBatchResult? result=null;
        PreparationBatchPreview? preview=null;
        var updates=new List<PreparationBatchProgress>();var sources=new List<SourceReceipt>();
        var json=new JsonSerializerOptions{PropertyNameCaseInsensitive=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true};
        json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues:false));
        Job? job=null;
        try
        {
            string jobPath=Path.GetFullPath(args[1]);if(jobPath==resultPath)throw new InvalidDataException("Job and result paths must differ");
            using var input=File.OpenRead(jobPath);if(input.Length>1024*1024)throw new InvalidDataException("Functional job size bound");
            job=JsonSerializer.Deserialize<Job>(input,json)??throw new InvalidDataException("Functional job JSON");action=job.Action;
            PreparationAction selectedAction=action switch {"prepare"=>PreparationAction.Prepare,"reset"=>PreparationAction.ResetSelection,
                "clear"=>PreparationAction.ClearOwned,"rebuild"=>PreparationAction.Rebuild,"bypass"=>PreparationAction.BypassOnce,_=>throw new InvalidDataException("Unknown preparation action")};
            if(job.BudgetMiB<1||job.BudgetMiB>65536||job.Paths.Length>128||job.CancelAfterCompleted.HasValue&&job.CancelAfterCompleted.Value<0)throw new InvalidDataException("Functional options bounds");
            if(job.Paths.Any(p=>Path.IsPathRooted(p)||p.Contains(':')||p.Replace('\\','/').Split('/').Any(s=>s=="."||s=="..")||p.IndexOfAny(new[]{'*','?'})>=0))
                throw new InvalidDataException("Paths must be relative texture files/folders");
            using var cancellation=new CancellationTokenSource();
            DiscoveredLoadout loadout=LoadoutDiscovery.Scan(new LoadoutRequest(job.GameRoot,job.ModsConfigPath,job.AdditionalRoots),cancellation.Token);
            if(!loadout.CanPrepare)throw new InvalidDataException(string.Join("; ",loadout.Problems));
            assembly=loadout.GameAssemblySha256;before=Hash(job.ModsConfigPath);
            DiscoveredTexture[] selected=loadout.Textures.Where(t=>t.EffectiveInProvider&&(job.Paths.Length==0||job.Paths.Any(p=>Matches(t.LogicalPath,p)))).ToArray();
            var request=new PreparationBatchRequest(loadout,job.SaveDataRoot,selected,new PreparationOptions{Preset=job.Preset,Filter=job.Filter,Anisotropy=job.Aniso,MipBias=job.MipBias},job.BudgetMiB*1024L*1024,selectedAction,job.PsdEnabled);
            var backend=new StandalonePreparationBackend();
            if(job.PreviewOnly||selectedAction!=PreparationAction.ClearOwned&&selectedAction!=PreparationAction.BypassOnce)
            {
                preview=backend.PreviewAsync(request,new DirectProgress<PreparationBatchProgress>(_=>{}),cancellation.Token).GetAwaiter().GetResult();
                request=request with {ApprovedPreview=preview};
            }
            foreach(DiscoveredTexture source in preview?.Rows.Select(r=>r.Source)??selected)
                sources.Add(new SourceReceipt{SourcePath=source.SourcePath,LogicalPath=source.LogicalPath,Provider=StandalonePreparationBackend.Provider(source.Provider),SourceBefore=Hash(source.SourcePath)});
            var progress=new DirectProgress<PreparationBatchProgress>(p=>{updates.Add(p);if(job.CancelAfterCompleted.HasValue&&p.Completed>=job.CancelAfterCompleted.Value)cancellation.Cancel();});
            if(!job.PreviewOnly)result=backend.ExecuteAsync(request,progress,cancellation.Token).GetAwaiter().GetResult();
            success=job.PreviewOnly||result?.Failed==0;
        }
        catch(OperationCanceledException){canceled=true;error="Canceled after the requested completed-item boundary";}
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;}
        finally
        {
            try
            {
                if(job!=null&&File.Exists(job.ModsConfigPath))after=Hash(job.ModsConfigPath);
                foreach(SourceReceipt source in sources)source.SourceAfter=Hash(source.SourcePath);
                if(before.Length>0&&(before!=after||sources.Any(s=>s.SourceBefore!=s.SourceAfter))){success=false;error="Original source or ModsConfig hash changed";}
                string? directory=Path.GetDirectoryName(resultPath);if(directory!=null)Directory.CreateDirectory(directory);
                File.WriteAllText(resultPath,JsonSerializer.Serialize(new{success,canceled,action,prepared=result?.Prepared??0,reused=result?.Reused??0,skipped=result?.Skipped??0,failed=result?.Failed??0,
                    storedBytes=result?.StoredBytes??0,textureBytes=result?.TextureBytes??0,summary=result?.Summary??"Read-only output preview; no conversion, activation or storage publication",error,gameAssemblySha256=assembly,modsConfigBefore=before,modsConfigAfter=after,selected=sources,progress=updates,
                    previewOnly=job?.PreviewOnly??false,preview=preview==null?null:new{preview.EligibleCount,preview.SkippedCount,preview.SourceBytes,preview.MinimumTextureBytes,preview.MaximumTextureBytes,
                        preview.MinimumStorageBytes,preview.MaximumStorageBytes,preview.MaximumAdditionalPublicationBytes,preview.Notices,
                        rows=preview.Rows.Select(r=>new{r.Source.SourcePath,r.Source.LogicalPath,provider=StandalonePreparationBackend.Provider(r.Source.Provider),r.Requested,r.Role,r.Eligible,r.Reason,r.SourceDigest,r.SourceBytes,
                            image=r.Image==null?null:new{r.Image.SourceKind,r.Image.Width,r.Image.Height,r.Image.OutputWidth,r.Image.OutputHeight,r.Image.Mips,r.Image.Representation,r.Image.MinimumTextureBytes,r.Image.MaximumTextureBytes},
                            r.MinimumRecordBytes,r.MaximumRecordBytes,r.MinimumStorageBytes,r.MaximumStorageBytes,r.SavedChoice})}},json));
            }
            catch{success=false;}
        }
        return success?0:canceled?3:1;
    }
    private static bool Matches(string logical,string path){path=path.Trim().Replace('\\','/');return logical.Equals(path,StringComparison.OrdinalIgnoreCase)||logical.StartsWith(path.TrimEnd('/')+"/",StringComparison.OrdinalIgnoreCase);}
    private static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
}
