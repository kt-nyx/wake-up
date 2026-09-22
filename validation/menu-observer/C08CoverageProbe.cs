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
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

// Private, bounded functional observer. Calling a window controller is explicitly
// not visible UI inspection. No performance result is derived from this probe.
internal static class C08CoverageProbe
{
    private static ThingDef? directionalDef;
    private static TerrainDef? terrainDef;
    private static MainButtonDef? buttonDef;
    private static readonly List<Sample> available = new();
    private static string selectionIdentity = "";
    private sealed class Sample
    {
        internal object Source = null!, Role = null!;
        internal ModContentPack Provider = null!;
        internal FileInfo File = null!;
        internal string Logical = "", Selection = "", Stem = "", Kind = "", Group = "";
        internal int Preset, SourceWidth, SourceHeight;
    }
    private static readonly List<Sample> samples = new();
    private static string directory = "", phase = "";
    private static bool started, finished;
    private static int cursor, checks, failures, controllerIndex;
    private static Window? window;
    private static Type Product(string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "WakeUp").GetType("WakeUp." + name, true)!;
    private static object? Read(object value, string name) => AccessTools.Field(value.GetType(), name)?.GetValue(value)
        ?? AccessTools.Property(value.GetType(), name)?.GetValue(value, null);
    private static object? Call(string type, string method, params object?[] args) => AccessTools.Method(Product(type), method).Invoke(null, args);
    private static object? Invoke(object value, string method, params object?[] args) => AccessTools.Method(value.GetType(), method).Invoke(value, args);
    private static string Hash(byte[] bytes) { using var h = SHA256.Create(); return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", ""); }
    private static string Quote(object? v) => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture)!.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    private static void Receipt(params object?[] values)
    {
        var fields = new List<string>();
        for (int i = 0; i < values.Length; i += 2)
        {
            object? v = values[i + 1];
            fields.Add(Quote(values[i]) + ":" + (v == null ? "null" : v is bool b ? b.ToString().ToLowerInvariant()
                : v is int || v is long || v is float ? Convert.ToString(v, CultureInfo.InvariantCulture) : Quote(v)));
        }
        File.AppendAllText(Path.Combine(directory, "c08-coverage-probe.jsonl"), "{" + string.Join(",", fields) + "}\n");
    }
    private static void Assert(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); checks++; }
    private static object OpenStore() => Activator.CreateInstance(Product("PreparedTextureStore"), BindingFlags.Instance | BindingFlags.NonPublic,
        null, new object?[] { GenFilePaths.SaveDataFolderPath, null, null, false }, null)!;
    private static Texture2D Holder(Sample s)
    {
        Assert(s.Provider.GetContentHolder<Texture2D>().contentList.TryGetValue(s.Stem, out var texture) && texture != null,
            "actual provider holder is missing: " + s.Logical);
        return texture!;
    }
    internal static void Start(string outputDirectory)
    {
        if (started) return;
        started = true; directory = outputDirectory; Directory.CreateDirectory(directory);
        phase = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--fixture-c08-quality=", StringComparison.Ordinal))?.Substring("--fixture-c08-quality=".Length) ?? "";
        try
        {
            Assert(new[] { "coverage-catalog", "coverage-prepare", "coverage-verify", "coverage-reset", "coverage-native" }.Contains(phase), "unknown C08 probe phase: " + phase);
            Receipt("event", "start", "phase", phase, "gameMvid", typeof(Mod).Module.ModuleVersionId,
                "productMvid", Product("PreparedQualityRuntime").Module.ModuleVersionId,
                "graphics", SystemInfo.graphicsDeviceType, "controllerOnly", true, "actualUiVerified", false);
            object roles = Call("PreparedTextureRoles", "Capture")!;
            Receipt("event", "catalog", "ready", Read(roles, "Ready"), "count", Read(roles, "Count"), "status", Read(roles, "Status"));
            Assert((bool)Read(roles, "Ready")!, "role catalog not ready: " + Read(roles, "Status"));
            SelectNaturalGroups(roles);
            Assert(samples.Count > 0 && samples.Count <= 20, "bounded natural selection required");
            selectionIdentity = Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",
                samples.Select(s => s.Selection + "|" + s.Logical + "|" + Hash(File.ReadAllBytes(s.File.FullName))))));
            foreach(var s in samples) Receipt("event","selected-source","selectionIdentity",selectionIdentity,
                "logical",s.Logical,"provider",s.Provider.PackageId,"providerRoot",s.Provider.RootDir,
                "providerOrder",LoadedModManager.RunningModsListForReading.IndexOf(s.Provider),
                "path",s.File.FullName,"sourceBytes",s.File.Length,"sourceSha256",Hash(File.ReadAllBytes(s.File.FullName)),
                "role",s.Kind,"group",s.Group,"pngOnly",s.File.Extension.Equals(".png",StringComparison.OrdinalIgnoreCase),"preset",s.Preset);
            Receipt("event","selection","selectionIdentity",selectionIdentity,"samples",samples.Count,
                "directionalDef",directionalDef!.defName,"terrainDef",terrainDef!.defName,"mainButtonDef",buttonDef!.defName,
                "duplicateProviderPaths",samples.GroupBy(s=>s.Stem,StringComparer.Ordinal).Count(g=>g.Count()>1),
                "pngOnlySources",samples.Count(s=>s.File.Extension.Equals(".png",StringComparison.OrdinalIgnoreCase)),
                "nativeStartupHits",AccessTools.Field(Product("PreparedTextureRuntime"),"Hits").GetValue(null),
                "qualityStartupApplied",AccessTools.Field(Product("PreparedQualityRuntime"),"Applied").GetValue(null));
            if(phase=="coverage-catalog")Finish();
        }
        catch (Exception e) { Fail(e); Finish(); }
    }
    private static void SelectNaturalGroups(object roles)
    {
        foreach(object source in (IEnumerable)Call("PreparedQualityRuntime","Sources")!)
        {
            string logical=(string)Read(source,"Logical")!;
            object?[] arguments={logical,null};
            if(!(bool)AccessTools.Method(roles.GetType(),"TryGet").Invoke(roles,arguments)!)continue;
            object role=arguments[1]!;
            available.Add(new Sample{Source=source,Role=role,Provider=(ModContentPack)Read(source,"Provider")!,
                File=(FileInfo)Read(source,"File")!,Logical=logical,Selection=(string)Read(source,"Selection")!,
                Stem=(string)Call("PreparationContract","Stem",logical)!,Kind=Convert.ToString(Read(role,"Kind"))!.ToLowerInvariant(),
                Group=(string)Read(role,"Group")!,Preset=3});
        }
        var groups=available.GroupBy(s=>s.Group,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        var byStem=available.GroupBy(s=>s.Stem,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().Group,StringComparer.Ordinal);
        var admitted=new HashSet<string>(StringComparer.Ordinal);
        var refused=new HashSet<string>(StringComparer.Ordinal);
        object options=Activator.CreateInstance(Product("PreparationOptions"))!;
        AccessTools.Property(options.GetType(),"Preset").SetValue(options,3,null);
        AccessTools.Property(options.GetType(),"Filter").SetValue(options,2,null);
        AccessTools.Property(options.GetType(),"Anisotropy").SetValue(options,4,null);
        AccessTools.Property(options.GetType(),"MipBias").SetValue(options,0.5f,null);
        int inspectedGroups=0;
        bool Admit(string path)
        {
            if(string.IsNullOrEmpty(path)||!byStem.TryGetValue(path,out string group)||refused.Contains(group))return false;
            if(admitted.Contains(group))return true;
            Sample[] members=groups[group];
            if(members.Length>12||samples.Count+members.Length>20||members.Any(s=>s.File.Length<1||s.File.Length>1024*1024))return false;
            if(++inspectedGroups>64)throw new InvalidDataException("Natural coverage candidate inspection bound exceeded");
            foreach(var s in members)
            {
                object estimate=Call("PreparationImageInspection","Inspect",File.ReadAllBytes(s.File.FullName),options,s.Kind=="mask",false)!;
                if(!(bool)Read(estimate,"Eligible")!)
                {
                    refused.Add(group);Receipt("event","candidate-refused","path",s.File.FullName,"reason",Read(estimate,"Reason"));return false;
                }
                s.SourceWidth=(int)Read(estimate,"Width")!;s.SourceHeight=(int)Read(estimate,"Height")!;
            }
            var stems=new HashSet<string>(members.Select(s=>s.Stem),StringComparer.Ordinal);
            foreach(string member in (IEnumerable)Read(members[0].Role,"Members")!)
                Assert(stems.Contains(member),"role family source missing from native inventory: "+member);
            samples.AddRange(members);admitted.Add(group);return true;
        }
        int Score(string path)
        {
            if(string.IsNullOrEmpty(path)||!byStem.TryGetValue(path,out string group))return -1;
            var members=groups[group];
            return (members.GroupBy(s=>s.Stem,StringComparer.Ordinal).Any(g=>g.Count()>1)?2:0)
                +(members.Any(s=>s.File.Extension.Equals(".png",StringComparison.OrdinalIgnoreCase))?1:0);
        }
        foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.GetType()==typeof(ThingDef)&&d.category==ThingCategory.Building&&d.graphicData?.graphicClass==typeof(Graphic_Multi))
            .OrderByDescending(d=>Score(d.graphicData.texPath+"_north")).ThenBy(d=>d.defName,StringComparer.Ordinal))
            if(Admit(def.graphicData.texPath+"_north")){directionalDef=def;break;}
        Assert(directionalDef!=null,"No complete bounded qualified natural directional family");
        foreach(var def in DefDatabase<TerrainDef>.AllDefsListForReading.Where(d=>d.GetType()==typeof(TerrainDef)&&!d.dontRender
            &&d.designationCategory!=null&&d.costList!=null&&d.costList.Count>0&&!string.IsNullOrEmpty(d.texturePath))
            .OrderByDescending(d=>Score(d.texturePath)).ThenBy(d=>d.defName,StringComparer.Ordinal))
            if(Admit(def.texturePath)){terrainDef=def;break;}
        Assert(terrainDef!=null,"No bounded qualified natural terrain artwork");
        foreach(var def in DefDatabase<MainButtonDef>.AllDefsListForReading.Where(d=>d.GetType()==typeof(MainButtonDef)&&!string.IsNullOrEmpty(d.iconPath))
            .OrderByDescending(d=>Score(d.iconPath)).ThenBy(d=>d.defName,StringComparer.Ordinal))
            if(Admit(def.iconPath)){buttonDef=def;break;}
        Assert(buttonDef!=null,"No bounded qualified unrelated main-button icon");
        if(!samples.GroupBy(s=>s.Stem,StringComparer.Ordinal).Any(g=>g.Count()>1))
            foreach(var group in groups.Values.Where(g=>g.GroupBy(s=>s.Stem,StringComparer.Ordinal).Any(p=>p.Count()>1))
                .OrderBy(g=>g.Length).ThenBy(g=>g[0].Stem,StringComparer.Ordinal))
                if(Admit(group[0].Stem))break;
        if(!samples.Any(s=>s.File.Extension.Equals(".png",StringComparison.OrdinalIgnoreCase)))
            foreach(var group in groups.Values.Where(g=>g.Any(s=>s.File.Extension.Equals(".png",StringComparison.OrdinalIgnoreCase)))
                .OrderBy(g=>g.Length).ThenBy(g=>g[0].Stem,StringComparer.Ordinal))
                if(Admit(group[0].Stem))break;
        samples.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Selection+a.Logical,b.Selection+b.Logical));
        Receipt("event","natural-overlap","qualifiedDuplicatePaths",available.GroupBy(s=>s.Stem,StringComparer.Ordinal).Count(g=>g.Count()>1),
            "selectedDuplicatePaths",samples.GroupBy(s=>s.Stem,StringComparer.Ordinal).Count(g=>g.Count()>1),
            "selectedPngOnlySources",samples.Count(s=>s.File.Extension.Equals(".png",StringComparison.OrdinalIgnoreCase)),
            "limitation","No selected overlap means a separate bounded fixture variant is required for duplicate-provider evidence");
    }
    internal static bool Advance()
    {
        if (!started || finished) return false;
        try
        {
            if (phase == "coverage-prepare" || phase == "coverage-reset")
            {
                if (window != null)
                {
                    window.WindowUpdate();
                    if ((bool)Read(window, "running")!) return true;
                    Assert((int)Read(window, "failed")! == 0, "quality controller reported failures: " + Read(window, "status"));
                    if(phase=="coverage-prepare")Assert((int)Read(window,"prepared")!+(int)Read(window,"reused")! == samples.Count,
                        "controller did not complete every selected provider source");
                    Receipt("event", "controller-finished", "index", controllerIndex - 1, "phase", phase,
                        "prepared", Read(window, "prepared"), "reused", Read(window, "reused"), "status", Read(window, "status"), "actualUiVerified", false);
                    window.PostClose(); window = null;
                }
                if (controllerIndex < 1) { StartController(controllerIndex++); return true; }
                VerifyPublished(); Finish(); return false;
            }
            if (cursor < samples.Count) { VerifyHolder(samples[cursor++], phase == "coverage-native"); return true; }
            CheckMaterial();
            if (phase == "coverage-verify") Assert(Convert.ToInt64(AccessTools.Field(Product("PreparedQualityRuntime"), "Applied").GetValue(null)) >= samples.Count,
                "actual startup did not apply every expected quality holder");
            Finish(); return false;
        }
        catch (Exception e) { Fail(e); BeforeExit(); return false; }
    }
    private static void StartController(int group)
    {
        var selected = samples.ToArray();
        object mod = LoadedModManager.GetMod(Product("WakeUpMod"));
        object? settings = Read(mod, "settings");
        if (settings == null)
        {
            var get = typeof(Mod).GetMethods().First(m => m.Name == "GetSettings" && m.IsGenericMethodDefinition);
            settings = get.MakeGenericMethod(Product("WakeUpSettings")).Invoke(mod, null);
        }
        Assert(settings != null && (bool)Read(settings!, "Enabled")!, "Wake-Up must be enabled");
        window = (Window)Activator.CreateInstance(Product("TextureQualityWindow"), BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { settings!, selected.Select(s => s.Provider).Distinct().ToArray(), string.Join(";", selected.Select(s => s.Logical).Distinct()) }, null)!;
        object options = Read(window, "options")!;
        AccessTools.Property(options.GetType(), "Preset").SetValue(options, phase == "coverage-reset" ? 0 : 3, null);
        AccessTools.Property(options.GetType(), "MipBias").SetValue(options, 0.5f, null);
        AccessTools.Property(options.GetType(), "Anisotropy").SetValue(options, 4, null);
        AccessTools.Property(options.GetType(), "Filter").SetValue(options, 2, null);
        Invoke(window, "Scan");
        Assert(((ICollection)Read(window, "rows")!).Count == selected.Length, "effective selection differs from complete expected provider group");
        var actualKeys=((IEnumerable)Read(window,"rows")!).Cast<object>().Select(row=>Read(Read(row,"Source")!,"Selection")+"|"+Read(Read(row,"Source")!,"Logical"));
        Assert(new HashSet<string>(actualKeys,StringComparer.Ordinal).SetEquals(selected.Select(s=>s.Selection+"|"+s.Logical)),
            "controller source/provider identities differ from selected complete groups");
        Invoke(window, "Start");
        Assert(phase == "coverage-reset" || (bool)Read(window, "running")!, "quality controller did not start: " + Read(window, "status"));
    }
    private static object NativeEntry(Sample s)
    {
        byte[] source = (byte[])Call("PreparedTextureRuntime", "ReadSource", s.File)!;
        return Call("PngRuntime", "CapturePrepared", s.File, source, new Func<int, bool>(_ => true))
            ?? throw new InvalidDataException("native producer did not capture: " + s.Logical);
    }
    private static void VerifyPublished()
    {
        object store = OpenStore();
        try
        {
            foreach (var s in samples)
            {
                object? record = Invoke(store, "ReadQuality", s.Selection, s.Logical);
                Assert(phase == "coverage-reset" ? record == null : record != null, "quality publication/reset missing: " + s.Logical);
                if (record != null) Assert((int)Read(Read(record, "Options")!, "Preset")! == s.Preset, "published preset mismatch");
            }
        }
        finally { ((IDisposable)store).Dispose(); }
        // Reopen the actual window's read-only saved-choice scan after the
        // writer closed, so persistence is visible through the real controller.
        object mod = LoadedModManager.GetMod(Product("WakeUpMod"));
        object settings = Read(mod, "settings")!;
        var inspection = (Window)Activator.CreateInstance(Product("TextureQualityWindow"), BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { settings, samples.Select(s => s.Provider).Distinct().ToArray(), string.Join(";", samples.Select(s => s.Logical).Distinct()) }, null)!;
        try
        {
            Invoke(inspection, "Scan");
            var rows = ((IEnumerable)Read(inspection, "rows")!).Cast<object>().ToArray();
            Assert(rows.Length == samples.Count, "reopened saved-choice scan lost provider rows");
            string expected = phase == "coverage-reset" ? "Saved: native quality" : "Saved: quarter dimensions";
            foreach (var row in rows) Assert(((string)Read(row, "SavedChoice")!).StartsWith(expected, StringComparison.Ordinal),
                "reopened saved choice not visible: " + Read(row, "SavedChoice"));
            Receipt("event", "saved-choice-visible", "passed", true, "rows", rows.Length, "expected", expected, "actualUiVerified", false);
        }
        finally { inspection.PostClose(); }
    }
    private static void VerifyHolder(Sample s, bool native)
    {
        Texture2D actual = Holder(s), expected = null!;
        object store = OpenStore();
        try
        {
            byte[] source = (byte[])Call("PreparedTextureRuntime", "ReadSource", s.File)!;
            object entry;
            if (native)
            {
                Assert(Invoke(store, "ReadQuality", s.Selection, s.Logical) == null, "reset left quality entry");
                entry = NativeEntry(s);
            }
            else
            {
                object record = Invoke(store, "ReadQuality", s.Selection, s.Logical) ?? throw new InvalidDataException("quality record absent");
                Assert((string)Read(record, "SourceDigest")! == Hash(source), "quality source digest mismatch");
                Assert((int)Read(Read(record, "Options")!, "Preset")! == s.Preset, "quality preset mismatch");
                entry = Call("PreparedQualityRuntime", "ToEntry", Read(record, "Output"))!;
            }
            expected = (Texture2D)Call("PngRuntime", "Restore", entry)!;
            if(!native)Assert(actual.width==Math.Max(1,s.SourceWidth/4)&&actual.height==Math.Max(1,s.SourceHeight/4)
                &&actual.filterMode==FilterMode.Trilinear&&actual.anisoLevel==4&&actual.mipMapBias==0.5f,
                "quarter dimensions or explicit sampling choice not applied: "+s.Logical);
            Assert(actual.width == expected.width && actual.height == expected.height && actual.format == expected.format
                && actual.graphicsFormat == expected.graphicsFormat && actual.mipmapCount == expected.mipmapCount
                && actual.filterMode == expected.filterMode && actual.anisoLevel == expected.anisoLevel && actual.mipMapBias == expected.mipMapBias
                && actual.wrapModeU == expected.wrapModeU && actual.wrapModeV == expected.wrapModeV && actual.wrapModeW == expected.wrapModeW
                && actual.isReadable == expected.isReadable, "startup holder descriptor differs: " + s.Logical);
            string observed = actual.width >= 512 || actual.height >= 512
                ? C08TexturePixels.Revision(actual) : (string)Call("AtlasProvenance", "Revision", actual)!;
            string baseline = expected.width >= 512 || expected.height >= 512
                ? C08TexturePixels.Revision(expected) : (string)Call("AtlasProvenance", "Revision", expected)!;
            Assert(observed == baseline, "all-mip GPU pixels differ from completed output: " + s.Logical);
            Texture2D global = ContentFinder<Texture2D>.Get(s.Stem, false);
            var winner = LoadedModManager.RunningModsListForReading.Last(m => m.GetContentHolder<Texture2D>().contentList.ContainsKey(s.Stem));
            Assert(ReferenceEquals(global, winner.GetContentHolder<Texture2D>().contentList[s.Stem]), "global winner is not last provider own holder");
            Receipt("event", "startup-holder", "passed", true, "nativeReset", native, "logical", s.Logical, "provider", s.Provider.PackageId,
                "sourceSha256", Hash(source), "width", actual.width, "height", actual.height, "format", actual.format, "mips", actual.mipmapCount,
                "filter", actual.filterMode, "bias", actual.mipMapBias, "aniso", actual.anisoLevel, "holderId", actual.GetInstanceID(),
                "globalId", global.GetInstanceID(), "globalWinner", winner.PackageId, "allMipGpuRevision", observed, "expectedRevision", baseline,
                "preparedPixelsSha256", Hash((byte[])Read(entry, "Pixels")!), "actualStartupHolder", true,"selectionIdentity",selectionIdentity);
        }
        finally { if (expected != null) Object.DestroyImmediate(expected); ((IDisposable)store).Dispose(); }
    }
    private static void CheckMaterial()
    {
        var directions = new[] {Rot4.North,Rot4.East,Rot4.South,Rot4.West};
        var suffixes = new[] {"_north","_east","_south","_west"};
        for(int i=0;i<directions.Length;i++)
        {
            Material material=directionalDef!.graphicData.Graphic.MatAt(directions[i]);
            string path=directionalDef.graphicData.texPath+suffixes[i];
            Texture2D expected=ContentFinder<Texture2D>.Get(path);
            Assert(ReferenceEquals(material.mainTexture,expected),"directional material did not consume selected global texture: "+path);
            bool maskExpected=directionalDef.graphicData.shaderType?.shaderPath=="Map/CutoutComplex";
            if(maskExpected)
            {
                string maskPath=string.IsNullOrEmpty(directionalDef.graphicData.maskPath)?path+"m":directionalDef.graphicData.maskPath+suffixes[i];
                Texture2D mask=ContentFinder<Texture2D>.Get(maskPath);
                Assert(material.HasProperty(ShaderPropertyIDs.MaskTex)&&ReferenceEquals(material.GetTexture(ShaderPropertyIDs.MaskTex),mask)
                    &&mask.width==expected.width&&mask.height==expected.height,"directional mask reference/dimensions mismatch");
            }
            Receipt("event","directional-material","passed",true,"def",directionalDef.defName,"direction",directions[i],
                "path",path,"textureId",expected.GetInstanceID(),"materialId",material.GetInstanceID(),"mask",maskExpected,"selectionIdentity",selectionIdentity);
        }
        Material terrain=terrainDef!.graphic.MatSingle;
        Texture2D terrainTexture=ContentFinder<Texture2D>.Get(terrainDef.texturePath);
        Assert(ReferenceEquals(terrain.mainTexture,terrainTexture),"actual TerrainDef graphic did not consume selected terrain artwork");
        Receipt("event","terrain-material","passed",true,"def",terrainDef.defName,"path",terrainDef.texturePath,
            "shader",terrain.shader.name,"textureId",terrainTexture.GetInstanceID(),"materialId",terrain.GetInstanceID(),"selectionIdentity",selectionIdentity);
        Texture2D icon=buttonDef!.Icon, expectedIcon=ContentFinder<Texture2D>.Get(buttonDef.iconPath);
        Assert(ReferenceEquals(icon,expectedIcon),"actual MainButtonDef.Icon did not consume selected UI artwork");
        Receipt("event","mainbutton-icon","passed",true,"def",buttonDef.defName,"path",buttonDef.iconPath,
            "textureId",icon.GetInstanceID(),"selectionIdentity",selectionIdentity,"actualUiVerified",false);
        int duplicatePaths=0;
        foreach(var duplicate in samples.GroupBy(s=>s.Stem,StringComparer.Ordinal).Where(g=>g.Count()>1))
        {
            var owned=duplicate.Select(Holder).ToArray();
            Assert(owned.Select(t=>t.GetInstanceID()).Distinct().Count()==owned.Length,"duplicate providers share an owned Unity texture object");
            var global=ContentFinder<Texture2D>.Get(duplicate.Key,false);
            var winner=LoadedModManager.RunningModsListForReading.Last(m=>m.GetContentHolder<Texture2D>().contentList.ContainsKey(duplicate.Key));
            Assert(ReferenceEquals(global,winner.GetContentHolder<Texture2D>().contentList[duplicate.Key]),"duplicate global winner mismatch");
            duplicatePaths++;
            Receipt("event","duplicate-provider-objects","passed",true,"logical",duplicate.Key,"providers",string.Join(";",duplicate.Select(s=>s.Provider.PackageId)),
                "holderIds",string.Join(";",owned.Select(t=>t.GetInstanceID())),"globalWinner",winner.PackageId,"globalId",global.GetInstanceID(),"selectionIdentity",selectionIdentity);
        }
        Receipt("event","coverage-limits","duplicatePaths",duplicatePaths,"actualGameplaySceneRendered",false,"actualUiVerified",false,"humanVisualQualityAccepted",false);
    }
    private static void Fail(Exception e) { failures++; Receipt("event", "failure", "passed", false, "phase", phase, "error", e.ToString()); }
    internal static void BeforeExit()
    {
        if (!started || finished) return;
        try { window?.PostClose(); } catch (Exception e) { Fail(e); }
        window = null;
        if (failures == 0) Fail(new OperationCanceledException("probe interrupted before completion"));
        Finish();
    }
    private static void Finish()
    {
        if (finished) return; finished = true;
        Receipt("event", "complete", "phase", phase, "passed", failures == 0 && checks > 0,
            "checks", checks, "failures", failures, "sources", samples.Count,"selectionIdentity",selectionIdentity,
            "actualUiVerified", false, "performanceMeasured", false, "humanVisualQualityAccepted", false);
    }
}
