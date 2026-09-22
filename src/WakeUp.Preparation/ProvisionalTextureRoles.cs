// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace WakeUp.Preparation;

// These hints let an external user prepare useful original Def-declared artwork.
// A finite icon-only XML projection reads supported declarative patches without
// executing mod code or a general XPath evaluator. It grants no shader authority. The game
// validates its resolved Defs, roles and complete source groups before using them.
public sealed class ProvisionalTextureRoles
{
    public sealed record Pair(string Color, string Mask);
    public sealed record Hint(string Kind, IReadOnlyList<string> Members)
    { public IReadOnlyList<Pair> Pairs { get; init; } = Array.Empty<Pair>(); }
    private sealed class Claim
    {
        public string Kind = "", Refusal = "";
        public HashSet<string> Links { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<Pair> Pairs { get; } = new HashSet<Pair>();
    }
    private readonly Dictionary<string, Hint> hints = new Dictionary<string, Hint>(StringComparer.Ordinal);
    private readonly Dictionary<string, Hint> knownGroups = new Dictionary<string, Hint>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> refusals = new Dictionary<string, string>(StringComparer.Ordinal);
    public IReadOnlyList<string> Notices { get; private set; } = Array.Empty<string>();
    public bool TryGet(string logical, out Hint hint) => hints.TryGetValue(PreparationContract.Stem(logical), out hint!);
    // Even a refused/incomplete family must expand its remaining filesystem
    // members, so selecting one member cannot silently omit the related refusal.
    public bool TryGetKnownGroup(string logical, out Hint hint) => knownGroups.TryGetValue(PreparationContract.Stem(logical), out hint!);
    public string Reason(string logical) => refusals.TryGetValue(PreparationContract.Stem(logical), out string? reason)
        ? reason : "No explicit supported original Def consumer; patched/custom roles require an in-game preparation session";
    private static readonly string[] Directions = { "_north", "_east", "_south", "_west" };

    public static ProvisionalTextureRoles Read(DiscoveredLoadout loadout, CancellationToken cancellation)
    {
        var result = new ProvisionalTextureRoles();
        var notices = new List<string>();
        var definitions = new List<XElement>();
        var iconPatches=new List<(XElement Operation,string Path)>();
        foreach (DiscoveredProvider provider in loadout.Providers)
        {
            cancellation.ThrowIfCancellationRequested();
            // Match native XML discovery: lower-priority folders first, filename
            // sort within each folder, with later exact relative files replacing.
            var files = new List<(string Logical, string Path)>();
            foreach (string folder in provider.Folders.Reverse())
            {
                string directory = Path.Combine(folder, "Defs");
                if (!Directory.Exists(directory)) continue;
                foreach (string path in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Where(p => Path.GetExtension(p).Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(Path.GetFileName, StringComparer.CurrentCulture))
                    files.Add((Path.GetFullPath(path).Substring(folder.Length + 1), path));
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = files.Count - 1; i >= 0; i--) if (!seen.Add(files[i].Logical)) files.RemoveAt(i);
            foreach (var file in files)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    using var stream = File.OpenRead(file.Path);
                    using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
                    XDocument document = XDocument.Load(reader);
                    if (document.Root?.Name.LocalName != "Defs") continue;
                    definitions.AddRange(document.Root.Elements().Select(e => new XElement(e)));
                    if (definitions.Count > 250000) throw new InvalidDataException("Definition preview bound exceeded");
                }
                catch (Exception ex) when (ex is IOException || ex is XmlException || ex is UnauthorizedAccessException)
                { notices.Add("Metadata preview skipped " + file.Path + ": " + ex.Message); }
            }
            var patchFiles=new List<(string Logical,string Path)>();
            foreach(string folder in provider.Folders.Reverse())
            {
                string directory=Path.Combine(folder,"Patches");if(!Directory.Exists(directory))continue;
                foreach(string path in Directory.GetFiles(directory,"*.*",SearchOption.AllDirectories)
                    .Where(p=>Path.GetExtension(p).Equals(".xml",StringComparison.OrdinalIgnoreCase)).OrderBy(Path.GetFileName,StringComparer.CurrentCulture))
                    patchFiles.Add((Path.GetFullPath(path).Substring(folder.Length+1),path));
            }
            seen.Clear();for(int i=patchFiles.Count-1;i>=0;i--)if(!seen.Add(patchFiles[i].Logical))patchFiles.RemoveAt(i);
            foreach(var file in patchFiles)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    using var stream=File.OpenRead(file.Path);
                    using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024});
                    XElement? patch=XDocument.Load(reader).Root;if(patch?.Name.LocalName!="Patch")continue;
                    foreach(var operation in patch.Elements())if(IconRelated(operation))iconPatches.Add((new XElement(operation),file.Path));
                    if(iconPatches.Count>25000)throw new InvalidDataException("Icon patch preview bound exceeded");
                }
                catch(Exception ex)when(ex is IOException||ex is XmlException||ex is UnauthorizedAccessException)
                {notices.Add("Icon patch metadata skipped "+file.Path+": "+ex.Message);}
            }
        }
        // Native ApplyPatches precedes ParseAndProcessXML / XmlInheritance.Resolve.
        var patchRefusals=ProjectIconPatches(definitions,iconPatches,loadout.Providers.Select(p=>p.BasePackageId),notices,cancellation,
            out var uncertainIconDefs,out var uncertainIconTypes);
        var named = definitions.Where(d => d.Attribute("Name") != null).GroupBy(d => (string)d.Attribute("Name")!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var resolved = new List<XElement>();
        foreach (XElement definition in definitions)
        {
            if (string.Equals((string?)definition.Attribute("Abstract"), "true", StringComparison.OrdinalIgnoreCase)) continue;
            try { resolved.Add(Resolve(definition, new HashSet<string>(StringComparer.Ordinal), 0)); }
            catch (InvalidDataException ex) { notices.Add("Inherited metadata preview skipped " + ((string?)definition.Element("defName") ?? definition.Name.LocalName) + ": " + ex.Message); }
        }
        // An uncertain child may have no direct icon field until inheritance is
        // resolved. Carry target identities, not only pre-inheritance paths, so
        // the inherited source cannot accidentally acquire an admissible hint.
        foreach(var definition in resolved.Where(d=>uncertainIconTypes.Contains(d.Name.LocalName)
            ||uncertainIconDefs.Contains(d.Name.LocalName+"\n"+(string?)d.Element("defName"))))
            foreach(var icon in definition.Elements().Where(e=>e.Name.LocalName=="iconPath"||e.Name.LocalName=="uiIconPath"))
            {string? path=Normalize(icon.Value);if(path!=null)patchRefusals[path]="Unresolved declarative effects on this resolved icon consumer; inherited sources require live role capture";}
        var sources = new HashSet<string>(loadout.Textures.Where(t => t.EffectiveInProvider).Select(t => PreparationContract.Stem(t.LogicalPath)), StringComparer.Ordinal);
        var shaders = resolved.Where(d => d.Name.LocalName == "ShaderTypeDef" && d.Attribute("Class") == null)
            .Where(d => d.Element("defName") != null).GroupBy(d => d.Element("defName")!.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var claims = new Dictionary<string, Claim>(StringComparer.Ordinal);
        foreach(string path in PreparationBuiltinUi.Paths)AddUi(path);
        foreach (XElement definition in resolved)
        {
            cancellation.ThrowIfCancellationRequested();
            if(definition.Attribute("Class")!=null)continue;
            if(definition.Name.LocalName=="AbilityDef"||definition.Name.LocalName=="MainButtonDef")
            {AddUi((string?)definition.Element("iconPath"));continue;}
            if (definition.Name.LocalName != "ThingDef" && definition.Name.LocalName != "TerrainDef") continue;
            AddUi((string?)definition.Element("uiIconPath"));
            foreach (XElement icon in definition.Element("uiIconPathsStuff")?.Elements() ?? Enumerable.Empty<XElement>()) AddUi((string?)icon.Element("iconPath"));
            if (definition.Name.LocalName == "ThingDef") AddGraphic(definition.Element("graphicData"), 0);
            else AddTerrain(definition);
        }
        foreach(var refusal in patchRefusals)if(sources.Contains(refusal.Key))Reject(refusal.Key,refusal.Value);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string start in claims.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!visited.Add(start)) continue;
            var queue = new Queue<string>(); queue.Enqueue(start); var members = new List<string>(); string refusal = "";
            while (queue.Count > 0)
            {
                string member = queue.Dequeue(); members.Add(member);
                if (claims[member].Refusal.Length > 0) refusal = claims[member].Refusal;
                foreach (string linked in claims[member].Links) if (visited.Add(linked)) queue.Enqueue(linked);
                if (members.Count > 512) refusal = "Connected color/mask group exceeds the external preparation bound";
            }
            string[] ordered = members.OrderBy(m => m, StringComparer.Ordinal).ToArray();
            Pair[] pairs=members.SelectMany(m=>claims[m].Pairs).Distinct().OrderBy(p=>p.Color,StringComparer.Ordinal)
                .ThenBy(p=>p.Mask,StringComparer.Ordinal).ToArray();
            foreach (string member in ordered)
            {
                var hint=new Hint(claims[member].Kind,ordered){Pairs=pairs};result.knownGroups[member]=hint;
                if (refusal.Length > 0) result.refusals[member] = refusal;
                else result.hints[member] = hint;
            }
        }
        result.Notices = notices; return result;

        XElement Resolve(XElement child, HashSet<string> ancestors, int depth)
        {
            if (depth > 64) throw new InvalidDataException("Inheritance depth limit");
            string? parent = (string?)child.Attribute("ParentName");
            if (string.IsNullOrEmpty(parent)) return new XElement(child);
            if (!ancestors.Add(parent)) throw new InvalidDataException("Inheritance cycle");
            if (!named.TryGetValue(parent, out XElement[]? parents) || parents.Length != 1) throw new InvalidDataException("Missing or ambiguous parent " + parent);
            XElement inherited = Resolve(parents[0], ancestors, depth + 1);
            inherited.Name = child.Name; Merge(inherited, child); inherited.Attribute("Abstract")?.Remove();
            return inherited;
        }
        void AddUi(string? path)
        { string? stem = Normalize(path); if (stem != null && sources.Contains(stem)) AddGroup(new[] { stem }, Array.Empty<string>(), true); }
        void AddTerrain(XElement definition)
        {
            string edge=(string?)definition.Element("edgeType")??"Hard";
            int edgeType=Array.IndexOf(new[]{"Hard","Fade","FadeRough","Water"},edge);
            if(edgeType<0&&!int.TryParse(edge,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out edgeType))edgeType=-1;
            foreach(var decision in PreparationTerrainPolicy.Evaluate((string?)definition.Element("texturePath"),(string?)definition.Element("pollutedTexturePath"),
                (string?)definition.Element("pollutionOverlayTexturePath"),string.Equals((string?)definition.Element("dontRender"),"true",StringComparison.OrdinalIgnoreCase),
                !string.IsNullOrEmpty((string?)definition.Element("customShader")),definition.Element("customShaderParameters")?.HasElements==true,
                edgeType,loadout.Providers.Any(p=>p.BasePackageId.Equals("ludeon.rimworld.biotech",StringComparison.OrdinalIgnoreCase))))
            {
                if(!sources.Contains(decision.Path))continue;
                if(decision.Allowed)AddGroup(new[]{decision.Path},Array.Empty<string>());
                else Reject(decision.Path,decision.Reason);
            }
        }
        void AddGraphic(XElement? data, int depth)
        {
            if (data == null || depth > 16) return;
            string? path = Normalize((string?)data.Element("texPath"));
            if (path == null) return;
            string shader = (string?)data.Element("shaderType") ?? "Cutout";
            bool known = shaders.TryGetValue(shader, out XElement? shaderDef);
            string shaderPath = known ? (string?)shaderDef!.Element("shaderPath") ?? "" : "";
            string uiShaderPath = known ? (string?)shaderDef!.Element("uiShaderPath") ?? "" : "";
            bool masked = shaderPath == "Map/CutoutComplex";
            string graphic = ((string?)data.Element("graphicClass") ?? "").Replace("Verse.", "", StringComparison.Ordinal);
            if (!known || shaderPath != "Map/Cutout" && shaderPath != "Map/EdgeDetect" && !masked || uiShaderPath.Length > 0 && (!masked || uiShaderPath != "Map/CutoutComplexUI")
                || data.Element("shaderParameters")?.HasElements == true)
            { Reject(path, "Unsupported original shader or shader parameters; native output retained"); return; }
            string? maskPath = Normalize((string?)data.Element("maskPath"));
            if (graphic == "Graphic_Single" || graphic == "Graphic_Terrain") Single(path, maskPath, masked);
            else if (graphic == "Graphic_Multi") Multi(path, maskPath, masked);
            else if (graphic == "Graphic_Random" || graphic == "Graphic_StackCount") Collection(path, masked);
            else Reject(path, "Unsupported original graphic class; native output retained");
            foreach (XElement attachment in data.Element("attachments")?.Elements() ?? Enumerable.Empty<XElement>()) AddGraphic(attachment, depth + 1);
        }
        void Single(string color, string? explicitMask, bool masked)
        {
            // This suffix is a verified Graphic_Single/CutoutComplex contract,
            // never a claim inferred from an arbitrary source filename.
            string? mask = masked ? explicitMask ?? color + "_m" : null;
            if(!sources.Contains(color)&&(mask==null||!sources.Contains(mask)))return;
            AddGroup(new[] { color }, mask == null ? Array.Empty<string>() : new[] { mask });
            if(!sources.Contains(color)||mask!=null&&!sources.Contains(mask))
                Reject(color,"Original metadata's paired source is missing from filesystem providers: "+(!sources.Contains(color)?color:mask));
        }
        void Multi(string path, string? explicitMask, bool masked)
        {
            string[] colors = Directions.Select(d => path + d).ToArray();
            string[] masks = masked ? Directions.Select(d => (explicitMask ?? path) + d + (explicitMask == null ? "m" : "")).ToArray() : Array.Empty<string>();
            if(!colors.Concat(masks).Any(sources.Contains))return;
            AddGroup(colors, masks);
            if (colors.Concat(masks).Any(m => !sources.Contains(m)))
                Reject(colors[0],"Directional family is incomplete in filesystem providers: "+string.Join(", ",colors.Concat(masks).Where(m=>!sources.Contains(m))));
        }
        void Collection(string path, bool masked)
        {
            string prefix = path + "/";
            string[] names = sources.Where(s => s.StartsWith(prefix, StringComparison.Ordinal)).Select(s => Path.GetFileName(s)!)
                .Where(n => !n.EndsWith("_m", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            foreach (var group in names.GroupBy(n => n.Split('_')[0], StringComparer.Ordinal))
            {
                bool directional = false;
                foreach (string name in group)
                {
                    if (Directions.Any(d => name.Contains(d, StringComparison.Ordinal))) { directional = true; continue; }
                    Single(prefix + name, masked ? path + "_m" : null, masked);
                }
                if (directional) Multi(prefix + group.Key, null, masked);
            }
        }
        void Reject(string path, string reason)
        {
            if (!claims.TryGetValue(path, out Claim? claim)) claims[path] = claim = new Claim();
            claim.Refusal = reason;
        }
        void AddGroup(string[] colors, string[] masks, bool ui = false)
        {
            string[] members = colors.Concat(masks).Distinct(StringComparer.Ordinal).ToArray();
            Pair[] pairs=masks.Length==0?Array.Empty<Pair>():colors.Select((color,index)=>new Pair(color,masks[index])).ToArray();
            foreach (string member in members)
            {
                string kind = masks.Contains(member, StringComparer.Ordinal) ? "mask" : ui ? "ui" : "color";
                if (!claims.TryGetValue(member, out Claim? claim)) claims[member] = claim = new Claim { Kind = kind };
                else if (claim.Kind.Length > 0 && (claim.Kind == "mask") != (kind == "mask")) claim.Refusal = "Conflicting original color/mask consumers";
                else if (kind == "ui" || claim.Kind.Length == 0) claim.Kind = kind;
                foreach (string related in members) claim.Links.Add(related);
                claim.Pairs.UnionWith(pairs);
            }
        }
    }
    private static bool IconRelated(XElement operation)=>operation.DescendantsAndSelf().Any(e=>e.Name.LocalName=="iconPath"||e.Name.LocalName=="uiIconPath"
        ||e.Name.LocalName=="xpath"&&(e.Value.Contains("iconPath",StringComparison.Ordinal)||e.Value.Contains("uiIconPath",StringComparison.Ordinal)
            ||e.Value.Contains("MainButtonDef",StringComparison.Ordinal)||e.Value.Contains("AbilityDef",StringComparison.Ordinal)));
    private static readonly Regex IconTarget=new Regex("^/?Defs/(MainButtonDef|AbilityDef|ThingDef|TerrainDef)\\[defName\\s*=\\s*(['\"])([A-Za-z0-9_.-]{1,128})\\2\\](?:/(iconPath|uiIconPath|minimized))?$",
        RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    // This finite projection knows only literal named Def targets, scalar icon
    // fields, and MainButton.minimized (needed for sequence success). No XPath
    // expressions, foreign classes or user assemblies are evaluated.
    private static Dictionary<string,string> ProjectIconPatches(List<XElement> definitions,List<(XElement Operation,string Path)> patches,
        IEnumerable<string> packageIds,List<string> notices,CancellationToken cancellation,
        out HashSet<string> unresolvedDefs,out HashSet<string> unresolvedTypes)
    {
        var active=new HashSet<string>(packageIds,StringComparer.OrdinalIgnoreCase);
        var refused=new Dictionary<string,string>(StringComparer.Ordinal);int operations=0,changed=0;
        var uncertainDefs=new HashSet<string>(StringComparer.Ordinal);var uncertainTypes=new HashSet<string>(StringComparer.Ordinal);
        unresolvedDefs=uncertainDefs;unresolvedTypes=uncertainTypes;
        foreach(var patch in patches)
        {
            cancellation.ThrowIfCancellationRequested();int before=changed;
            bool? result=Apply(patch.Operation,patch.Path,0);
            if(result==null)Uncertain(patch.Operation,patch.Path);
            if(changed>before)notices.Add("Provisional icon metadata: "+(changed-before)+" scalar changes from "+patch.Path+"; live resolved consumer validation remains required");
        }
        foreach(var definition in definitions.Where(d=>uncertainTypes.Contains(d.Name.LocalName)
            ||uncertainDefs.Contains(d.Name.LocalName+"\n"+(string?)d.Element("defName"))))
            foreach(var icon in definition.Elements().Where(e=>e.Name.LocalName=="iconPath"||e.Name.LocalName=="uiIconPath"))
            {string? path=Normalize(icon.Value);if(path!=null)refused[path]="Unresolved declarative effects on this icon consumer; live role capture required";}
        return refused;
        bool? Apply(XElement operation,string file,int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            if(depth>24||++operations>100000)throw new InvalidDataException("Finite icon patch preview bound exceeded");
            if(operation.Attributes().Any(a=>a.Name.LocalName!="Class"&&a.Name.LocalName!="MayRequire"))return null;
            string? requirements=(string?)operation.Attribute("MayRequire");
            if(requirements!=null)
            {
                var ids=requirements.Split(',').Select(p=>p.Trim()).ToArray();
                if(ids.Any(p=>p.Length==0))return null;
                if(ids.Any(p=>!active.Contains(p)))return true;
            }
            string kind=(string?)operation.Attribute("Class")??"";
            if(kind.StartsWith("Verse.",StringComparison.Ordinal))kind=kind.Substring(6);
            string success=(string?)operation.Element("success")??"Normal";
            if(!new[]{"Normal","Always","Never","Invert"}.Contains(success,StringComparer.Ordinal))return null;
            bool? value=Worker();
            if(value==null)return null;
            return success=="Always"?true:success=="Never"?false:success=="Invert"?!value.Value:value;
            bool? Worker()
            {
                if(kind=="PatchOperationSequence")
                {
                    if(operation.Elements().Any(e=>e.Name.LocalName!="operations"&&e.Name.LocalName!="success")||operation.Element("operations")==null)return null;
                    XElement[] sequence=operation.Element("operations")!.Elements().ToArray();
                    for(int i=0;i<sequence.Length;i++)
                    {
                        bool? step=Apply(sequence[i],file,depth+1);
                        if(step==null){foreach(var remaining in sequence.Skip(i))Uncertain(remaining,file);return null;}
                        if(!step.Value)return false;
                    }
                    return true;
                }
                if(!TryTarget(operation,out var targets,out string type,out string field))return null;
                if(kind=="PatchOperationConditional")
                {
                    if(operation.Elements().Any(e=>!new[]{"xpath","match","nomatch","success"}.Contains(e.Name.LocalName)))return null;
                    XElement? branch=operation.Element(targets.Length>0?"match":"nomatch");
                    return branch!=null?Apply(branch,file,depth+1):operation.Element("match")==null?operation.Element("nomatch")!=null:true;
                }
                if(kind!="PatchOperationAdd"&&kind!="PatchOperationReplace"&&kind!="PatchOperationRemove")return null;
                if(operation.Elements().Any(e=>!new[]{"xpath","value","order","success"}.Contains(e.Name.LocalName)))return null;
                if(targets.Length==0)return false;
                if(kind=="PatchOperationRemove")
                {
                    if(field.Length==0)return null;
                    foreach(var target in targets){target.Remove();changed++;}return true;
                }
                XElement[] values=operation.Element("value")?.Elements().ToArray()??Array.Empty<XElement>();
                if(values.Length!=1||values[0].HasElements||values[0].HasAttributes)return null;
                string name=values[0].Name.LocalName;
                if(!AllowedField(type,name)||kind=="PatchOperationAdd"&&field.Length!=0||kind=="PatchOperationReplace"&&field!=name)return null;
                string order=(string?)operation.Element("order")??"Append";
                if(order!="Append"&&order!="Prepend")return null;
                foreach(var target in targets)
                {
                    if(kind=="PatchOperationAdd")
                    {
                        // Duplicate scalar fields have no unambiguous projection.
                        if(target.Element(name)!=null)return null;
                        if(order=="Prepend")target.AddFirst(new XElement(values[0]));else target.Add(new XElement(values[0]));
                    }
                    else target.ReplaceWith(new XElement(values[0]));
                    changed++;
                }
                return true;
            }
        }
        bool TryTarget(XElement operation,out XElement[] targets,out string type,out string field)
        {
            targets=Array.Empty<XElement>();type=field="";
            string path=(string?)operation.Element("xpath")??"";if(path.Length>512)return false;
            Match match=IconTarget.Match(path);if(!match.Success)return false;
            type=match.Groups[1].Value;field=match.Groups[4].Value;
            if(field.Length>0&&!AllowedField(type,field))return false;
            string defType=type,defName=match.Groups[3].Value;
            var defs=definitions.Where(d=>d.Name.LocalName==defType&&(string?)d.Element("defName")==defName).ToArray();
            if(defs.Length>1||defs.Any(d=>d.Attribute("Class")!=null))return false;
            if(defs.Any(d=>d.Attribute("MayRequire")!=null&&!((string)d.Attribute("MayRequire")!).Split(',').All(id=>active.Contains(id.Trim()))))return false;
            string selectedField=field;targets=field.Length==0?defs:defs.SelectMany(d=>d.Elements(selectedField)).ToArray();
            return true;
        }
        void Uncertain(XElement operation,string file)
        {
            if(!IconRelated(operation))return;
            const string reason="Unresolved declarative icon patch effects; retain native until live role capture";
            // Refuse explicitly mentioned candidates and currently known icon
            // sources in the referenced native family. Unknown XPath/code never
            // becomes authority merely because an icon filename looks familiar.
            var candidates=operation.DescendantsAndSelf().Where(e=>e.Name.LocalName=="iconPath"||e.Name.LocalName=="uiIconPath").Select(e=>e.Value).ToList();
            string allPaths=string.Join(";",operation.DescendantsAndSelf().Where(e=>e.Name.LocalName=="xpath").Select(e=>e.Value));
            string[] types=new[]{"MainButtonDef","AbilityDef","ThingDef","TerrainDef"}.Where(t=>allPaths.Contains(t,StringComparison.Ordinal)).ToArray();
            foreach(var xpath in operation.DescendantsAndSelf().Where(e=>e.Name.LocalName=="xpath"))
            {
                Match match=xpath.Value.Length<=512?IconTarget.Match(xpath.Value):Match.Empty;
                if(match.Success)uncertainDefs.Add(match.Groups[1].Value+"\n"+match.Groups[3].Value);
                else foreach(string type in types.Length==0?new[]{"MainButtonDef","AbilityDef","ThingDef","TerrainDef"}:types)uncertainTypes.Add(type);
            }
            if(allPaths.Length==0)foreach(string type in new[]{"MainButtonDef","AbilityDef","ThingDef","TerrainDef"})uncertainTypes.Add(type);
            foreach(var definition in definitions.Where(d=>uncertainTypes.Contains(d.Name.LocalName)
                ||uncertainDefs.Contains(d.Name.LocalName+"\n"+(string?)d.Element("defName"))))
                candidates.AddRange(definition.Elements().Where(e=>e.Name.LocalName=="iconPath"||e.Name.LocalName=="uiIconPath").Select(e=>e.Value));
            foreach(string candidate in candidates){string? path=Normalize(candidate);if(path!=null)refused[path]=reason+": "+file;}
            notices.Add(reason+": "+file);
        }
        static bool AllowedField(string type,string field)=>type=="MainButtonDef"?field=="iconPath"||field=="minimized"
            :type=="AbilityDef"?field=="iconPath":field=="uiIconPath";
    }
    private static void Merge(XElement inherited, XElement child)
    {
        foreach (XAttribute attribute in child.Attributes()) inherited.SetAttributeValue(attribute.Name, attribute.Value);
        foreach (XElement item in child.Elements())
        {
            XElement? previous = inherited.Elements(item.Name).FirstOrDefault();
            if (previous == null) inherited.Add(new XElement(item));
            else if (!item.HasElements || string.Equals((string?)item.Attribute("Inherit"), "false", StringComparison.OrdinalIgnoreCase)) previous.ReplaceWith(new XElement(item));
            else if (item.Elements().All(e => e.Name.LocalName == "li")) foreach (XElement entry in item.Elements()) previous.Add(new XElement(entry));
            else Merge(previous, item);
        }
    }
    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Replace('\\', '/');
        if (Path.IsPathRooted(path) || path.Contains(':') || path.Split('/').Any(p => p == "." || p == "..")) return null;
        return path;
    }
}
