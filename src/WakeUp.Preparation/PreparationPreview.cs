// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace WakeUp.Preparation;

public sealed class PreparationPreviewRow
{
    public DiscoveredTexture Source { get; init; } = null!;
    public bool Requested { get; init; }
    public string Role { get; init; } = "";
    public string Group { get; init; } = "";
    public bool Eligible { get; internal set; }
    public string Reason { get; internal set; } = "";
    public string SourceDigest { get; init; } = "";
    public long SourceBytes { get; init; }
    public PreparationImageEstimate? Image { get; init; }
    public long MinimumRecordBytes { get; init; }
    public long MaximumRecordBytes { get; init; }
    public long MinimumStorageBytes => MinimumRecordBytes==0?0:PreparationStorageSession.EstimateRecordStorageBytes(checked((int)MinimumRecordBytes));
    public long MaximumStorageBytes => MaximumRecordBytes==0?0:PreparationStorageSession.EstimateRecordStorageBytes(checked((int)MaximumRecordBytes));
    public string SavedChoice { get; internal set; } = "Choose a save-data profile to inspect saved quality";
}

public sealed class PreparationBatchPreview
{
    public DiscoveredLoadout Loadout { get; init; } = null!;
    public PreparationOptions Options { get; init; } = new PreparationOptions();
    public bool PsdEnabled { get; init; }
    public string HelperIdentity { get; init; } = "";
    public IReadOnlyList<PreparationPreviewRow> Rows { get; init; } = Array.Empty<PreparationPreviewRow>();
    public IReadOnlyList<string> Notices { get; init; } = Array.Empty<string>();
    public int EligibleCount => Rows.Count(r => r.Eligible);
    public int SkippedCount => Rows.Count-EligibleCount;
    public long SourceBytes => Rows.Sum(r => r.SourceBytes);
    public long MinimumTextureBytes => Rows.Where(r => r.Eligible).Sum(r => r.Image?.MinimumTextureBytes ?? 0);
    public long MaximumTextureBytes => Rows.Where(r => r.Eligible).Sum(r => r.Image?.MaximumTextureBytes ?? 0);
    public long MinimumRecordBytes => Rows.Where(r => r.Eligible).Sum(r => r.MinimumRecordBytes);
    public long MaximumRecordBytes => Rows.Where(r => r.Eligible).Sum(r => r.MaximumRecordBytes);
    public long MinimumStorageBytes => Rows.Where(r=>r.Eligible).Sum(r=>r.MinimumStorageBytes);
    public long MaximumStorageBytes => Rows.Where(r=>r.Eligible).Sum(r=>r.MaximumStorageBytes);
    public long MaximumAdditionalPublicationBytes => MaximumStorageBytes==0?0:MaximumStorageBytes+PreparationStorageSession.MaximumIndexReplacementBytes;
    public string Identity => PreparationContract.Frame(Options.Identity,PsdEnabled.ToString(),HelperIdentity,
        StandalonePreparationBackend.Active(Loadout),PreparationContract.Frame(Rows.Select(r=>PreparationContract.Frame(
            PreparationPreview.Key(r.Source),r.Group,r.Role,r.SourceDigest,r.Eligible.ToString(),r.Reason,
            r.MinimumRecordBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),r.MaximumRecordBytes.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray()));
}

// The window and functional entry both display this same plan; execution rebuilds
// it before any write so known refusals cannot first appear after publication.
public static class PreparationPreview
{
    public static string Key(DiscoveredTexture source) => source.Provider.Root+"\n"+source.SourcePath;
    public static IReadOnlyList<DiscoveredTexture> Expand(DiscoveredLoadout loadout,
        IEnumerable<DiscoveredTexture> selected, ProvisionalTextureRoles roles)
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);var stems=new HashSet<string>(StringComparer.Ordinal);
        foreach(var source in selected){keys.Add(Key(source));stems.Add(PreparationContract.Stem(source.LogicalPath));if(roles.TryGetKnownGroup(source.LogicalPath,out var hint))stems.UnionWith(hint.Members);}
        return loadout.Textures.Where(t=>t.EffectiveInProvider&&(keys.Contains(Key(t))||stems.Contains(PreparationContract.Stem(t.LogicalPath)))).ToArray();
    }

    public static PreparationBatchPreview Create(DiscoveredLoadout loadout,IReadOnlyList<DiscoveredTexture> selected,
        PreparationOptions options,bool psdEnabled,string helperIdentity,string helperProblem,
        ProvisionalTextureRoles roles,CancellationToken cancellation,IProgress<PreparationBatchProgress>? progress=null)
    {
        if(!options.Valid)throw new InvalidDataException("Invalid preview quality options");
        var requested=new HashSet<string>(selected.Select(Key),StringComparer.Ordinal);
        IReadOnlyList<DiscoveredTexture> expanded=Expand(loadout,selected,roles);
        var rows=new List<PreparationPreviewRow>();string active=StandalonePreparationBackend.Active(loadout);
        foreach(DiscoveredTexture source in expanded)
        {
            cancellation.ThrowIfCancellationRequested();
            bool hasRole=roles.TryGet(source.LogicalPath,out var hint);
            bool knownGroup=roles.TryGetKnownGroup(source.LogicalPath,out var family);
            string role=hasRole?hint.Kind:"",group=knownGroup?PreparationContract.Frame(family.Members.ToArray()):"unknown:"+Key(source);
            PreparationImageEstimate? estimate=null;string refusal="",digest="";long bytes=source.SourceBytes,minimum=0,maximum=0;
            if(options.Preset!=0)
            {
                if(!hasRole)refusal=roles.Reason(source.LogicalPath);
                try
                {
                    byte[] snapshot=ReadSource(source.SourcePath);bytes=snapshot.Length;digest=PreparationContract.Hash(snapshot);
                    estimate=PreparationImageInspection.Inspect(snapshot,options,role=="mask",psdEnabled);
                    if(!estimate.Eligible)refusal=Join(refusal,estimate.Reason);
                    if(helperProblem.Length>0)refusal=Join(refusal,helperProblem);
                    if(estimate.Eligible)
                    {
                        var record=new PreparationRecord{Provider=StandalonePreparationBackend.Provider(source.Provider),Logical=source.LogicalPath.Replace('/',Path.DirectorySeparatorChar),
                            Physical=source.SourcePath,SourceDigest=digest,Active=active,Runtime=PreparationContract.RuntimeGog,Helper=helperIdentity,Role=role,Options=Copy(options),
                            Output=new PreparationPixels{Pixels=new byte[1]}};
                        int metadata=PreparationContract.Encode(record).Length-1;
                        minimum=checked(metadata+estimate.MinimumTextureBytes);maximum=checked(metadata+estimate.MaximumTextureBytes);
                    }
                }
                catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is OverflowException||e is NotSupportedException)
                {refusal=Join(refusal,e.Message);}
            }
            rows.Add(new PreparationPreviewRow{Source=source,Requested=requested.Contains(Key(source)),Role=role,Group=group,
                Eligible=refusal.Length==0,Reason=refusal.Length>0?refusal:options.Preset==0?"Reset selected quality to native on restart; no native capture or encoding"
                    :"Eligible for explicit conversion; known original pairs checked in preview, patched/live consumers require in-game validation",SourceDigest=digest,SourceBytes=bytes,Image=estimate,MinimumRecordBytes=minimum,MaximumRecordBytes=maximum});
            progress?.Report(new PreparationBatchProgress(rows.Count,expanded.Count,source.SourcePath,"Preview: "+rows[rows.Count-1].Reason));
        }
        foreach(var group in rows.GroupBy(r=>r.Group,StringComparer.Ordinal))
        {
            string refusal="";PreparationPreviewRow? failed=group.FirstOrDefault(r=>!r.Eligible);
            if(failed!=null)refusal="Whole group retains native: "+failed.Source.Provider.Name+":"+failed.Source.LogicalPath+" — "+failed.Reason;
            else if(group.Count()>512)refusal="Whole group exceeds 512 provider textures";
            else if(options.Preset!=0&&group.Sum(r=>r.SourceBytes)>96L*1024*1024)refusal="Whole group exceeds 96 MiB of source snapshots";
            else if(options.Preset!=0&&group.Sum(r=>r.Image?.MaximumTextureBytes??0)>96L*1024*1024)refusal="Whole group exceeds 96 MiB of converted texture payload";
            if(refusal.Length==0&&options.Preset!=0&&roles.TryGetKnownGroup(group.First().Source.LogicalPath,out var family))
                refusal=PairProblem(group.ToArray(),family);
            if(refusal.Length>0)foreach(var row in group){row.Eligible=false;row.Reason=refusal;}
        }
        return new PreparationBatchPreview{Loadout=loadout,Options=Copy(options),PsdEnabled=psdEnabled,HelperIdentity=helperIdentity,Rows=rows,Notices=roles.Notices};
    }
    private static string PairProblem(PreparationPreviewRow[] rows,ProvisionalTextureRoles.Hint family)
    {
        var members=rows.GroupBy(r=>PreparationContract.Stem(r.Source.LogicalPath),StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r.Source.Provider.Order).ToArray(),StringComparer.Ordinal);
        string[] missing=family.Members.Where(m=>!members.ContainsKey(m)).ToArray();
        if(missing.Length>0)return "Whole group retains native: known required source missing from expanded selection: "+string.Join(", ",missing);
        foreach(var pair in family.Pairs)
        {
            if(!members.TryGetValue(pair.Color,out var colors)||!members.TryGetValue(pair.Mask,out var masks))
                return "Whole group retains native: known color/mask pair is incomplete: "+pair.Color+" / "+pair.Mask;
            string problem=Mismatch(colors[colors.Length-1],masks[masks.Length-1],"effective global pair");
            if(problem.Length>0)return problem;
            foreach(var color in colors)
                foreach(var mask in masks.Where(m=>m.Source.Provider.Root==color.Source.Provider.Root))
                {problem=Mismatch(color,mask,"same-provider pair");if(problem.Length>0)return problem;}
        }
        return "";
        static string Mismatch(PreparationPreviewRow color,PreparationPreviewRow mask,string context)
        {
            if(color.Image==null||mask.Image==null)
                return "Whole group retains native: known pair source could not be inspected";
            return color.Image.OutputWidth==mask.Image.OutputWidth&&color.Image.OutputHeight==mask.Image.OutputHeight?""
                :"Whole group retains native: "+context+" output dimensions differ: "+color.Source.Provider.Name+":"+color.Source.LogicalPath
                    +" "+color.Image.OutputWidth+"x"+color.Image.OutputHeight+" / "+mask.Source.Provider.Name+":"+mask.Source.LogicalPath
                    +" "+mask.Image.OutputWidth+"x"+mask.Image.OutputHeight+". Original metadata is provisional; patched/live roles still require in-game validation.";
        }
    }
    internal static byte[] ReadSource(string path)
    {
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length<1||input.Length>PreparationEncoder.MaximumSource)throw new InvalidDataException("Encoded source exceeds the selected encoder bound");
        byte[] data=new byte[(int)input.Length];input.ReadExactly(data);return data;
    }
    internal static PreparationOptions Copy(PreparationOptions p)=>new PreparationOptions{Preset=p.Preset,Filter=p.Filter,Anisotropy=p.Anisotropy,MipBias=p.MipBias};
    private static string Join(string a,string b)=>a.Length==0?b:a+"; "+b;
}
