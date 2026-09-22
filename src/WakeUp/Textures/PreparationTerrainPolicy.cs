// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace WakeUp;

// Shared metadata policy for the pinned GOG TerrainDef.PostLoad consumers.
// It grants no engine authority: the live caller still authenticates methods,
// resolved definitions, source providers and the complete publication group.
public static class PreparationTerrainPolicy
{
    public const string Contract = "gog573-terrain-base-pollution-split-v1";
    public sealed class Decision
    {
        public string Path { get; }
        public bool Allowed { get; }
        public string Reason { get; }
        internal Decision(string path,bool allowed,string reason){Path=path;Allowed=allowed;Reason=reason;}
    }
    public static IReadOnlyList<Decision> Evaluate(string? basePath,string? pollutedPath,string? overlayPath,
        bool dontRender,bool customShader,bool customParameters,int edgeType,bool biotechActive)
    {
        var decisions=new Dictionary<string,Decision>(StringComparer.Ordinal);
        if(dontRender)return Array.Empty<Decision>();
        void Add(string? path,bool allowed,string reason)
        {
            string? key=Normalize(path);if(key==null)return;
            if(!decisions.TryGetValue(key,out var old)||old.Allowed)decisions[key]=new Decision(key,allowed,reason);
        }
        if(customShader||customParameters||edgeType<0||edgeType>3)
        {
            foreach(string? path in new[]{basePath,pollutedPath,overlayPath})Add(path,false,"Custom terrain shader/data contract is not qualified");
        }
        else
        {
            // Native ?? fallback deliberately distinguishes null from empty.
            // A polluted material can reuse ordinary base artwork; refusal then
            // applies to that entire source, even its ordinary terrain consumer.
            bool pollutionRuns=biotechActive&&(!string.IsNullOrEmpty(overlayPath)||!string.IsNullOrEmpty(pollutedPath));
            if(pollutionRuns)
            {
                Add(pollutedPath??basePath,false,"Polluted terrain or overlay consumer is not qualified");
                Add(overlayPath,false,"Polluted terrain or overlay consumer is not qualified");
            }
            Add(basePath,true,"Ordinary terrain base artwork; separate pollution sources retain native quality");
        }
        return decisions.Values.OrderBy(d=>d.Path,StringComparer.Ordinal).ToArray();
    }
    private static string? Normalize(string? path)
    {
        if(string.IsNullOrEmpty(path)||path!.IndexOfAny(new[]{'\\',':','*','?','\0'})>=0
            ||path.Split('/').Any(p=>p.Length==0||p=="."||p==".."))return null;
        return path;
    }
}
