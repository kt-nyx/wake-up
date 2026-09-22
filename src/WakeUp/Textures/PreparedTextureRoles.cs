// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WakeUp;

// Metadata establishes what the native consumer requests, not that a particular
// encoder preserves that role. The quality planner must separately qualify the
// color/mask algorithm, bind each provider and validate the whole group before
// publishing any member. This catalog never loads a Graphic, Shader or texture.
internal sealed class PreparedTextureRoles
{
    internal const string Contract = "resolved-native-graphic-roles-c08-v2";
    internal enum RoleKind { Color, Mask, Ui }
    internal sealed class Role
    {
        internal readonly RoleKind Kind;
        internal readonly string Identity, Group;
        internal readonly IReadOnlyList<string> Members;
        internal readonly IReadOnlyList<(string Color, string Mask)> Pairs;
        internal readonly bool RequiresMainButtonContract;
        internal Role(RoleKind kind, string identity, string group, string[] members, (string Color, string Mask)[] pairs, bool mainButton = false)
        { Kind = kind; Identity = identity; Group = group; Members = Array.AsReadOnly(members); Pairs = Array.AsReadOnly(pairs); RequiresMainButtonContract = mainButton; }
    }
    private sealed class Claim
    {
        internal RoleKind Kind;
        internal readonly HashSet<string> Evidence = new(StringComparer.Ordinal);
        internal readonly HashSet<string> Linked = new(StringComparer.Ordinal);
        internal readonly HashSet<(string Color, string Mask)> Pairs = new();
        internal string Refusal = "";
        internal Claim(RoleKind kind) { Kind = kind; }
    }
    private readonly Dictionary<string, Claim> claims = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Role> roles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> refusals = new(StringComparer.Ordinal);
    private readonly HashSet<string> sources = new(StringComparer.Ordinal);
    private readonly List<PublishedPatchGuard> guards = new();
    private readonly List<PublishedPatchGuard> mainButtonGuards = new();
    private readonly HashSet<string> mainButtonSources = new(StringComparer.Ordinal);
    private bool mainButtonsReady = true;
    private string mainButtonRefusal = "";
    private string activeIdentity = "";
    internal bool Ready { get; private set; }
    internal string Status { get; private set; } = "Texture roles require completed definition loading";
    internal int Count => roles.Count;

    internal bool TryGet(string holderLogical, out Role role)
    {
        role = null!;
        string? path = Normalize(holderLogical);
        if (!Ready || path == null || !guards.All(g => g.AllowsOriginalContract())
            || !roles.TryGetValue(path, out var candidate)
            || candidate.RequiresMainButtonContract && !mainButtonGuards.All(g => g.AllowsOriginalContract())) return false;
        role = candidate; return true;
    }
    internal string Reason(string holderLogical)
    {
        if (!Ready) return Status;
        if (!guards.All(g => g.AllowsOriginalContract())) return "Native texture consumer hooks changed";
        string? path = Normalize(holderLogical);
        if (path != null && roles.TryGetValue(path, out var role) && role.RequiresMainButtonContract
            && !mainButtonGuards.All(g => g.AllowsOriginalContract())) return "Main button consumer hooks changed; this related group retains native quality";
        return path != null && refusals.TryGetValue(path, out string reason) ? reason
            : "No qualified native texture consumer metadata";
    }

    internal static PreparedTextureRoles Capture()
    {
        var result = new PreparedTextureRoles();
        try
        {
            // ReloadContent schedules its texture work. On the normal startup,
            // the callbacks run after DoPlayLoad returned and set Loaded=true.
            // Forced/early drains and incomplete loads retain native quality.
            if (!UnityData.IsInMainThread || !PlayDataLoader.Loaded) return result;
            result.CheckContract();
            result.activeIdentity = PreparedTextureRuntime.ActiveIdentity();
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
                foreach (var item in PngRuntime.SelectedFiles(mod))
                {
                    string? path = Normalize(item.Key);
                    if (path != null) result.sources.Add(path);
                    if (result.sources.Count > 250000) throw new InvalidDataException("texture-role-source-limit");
                }
            // The exact native classes below own these fields and callbacks.
            // A custom Def/Graphic subclass does not acquire a native contract
            // merely by deriving from the same base type.
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.GetType() != typeof(ThingDef))
                { result.RejectGraphic(def.graphicData, "Custom definition consumer is not qualified"); continue; }
                result.AddGraphic(def.graphicData, "ThingDef:" + def.defName, 0);
                result.AddIcons(def);
            }
            foreach (TerrainDef def in DefDatabase<TerrainDef>.AllDefsListForReading)
                if (def.GetType() == typeof(TerrainDef)) { result.AddIcons(def); result.AddTerrain(def); }
            foreach (AbilityDef def in DefDatabase<AbilityDef>.AllDefsListForReading)
                if (def.GetType() == typeof(AbilityDef)) result.AddUi(def.iconPath, "AbilityDef:" + def.defName + ":iconPath");
            foreach (MainButtonDef def in DefDatabase<MainButtonDef>.AllDefsListForReading)
                if (def.GetType() == typeof(MainButtonDef))
                {
                    string? path = Normalize(def.iconPath, false);
                    if (path == null || !result.sources.Contains(path)) continue;
                    if (!result.mainButtonsReady) result.Reject(path, result.mainButtonRefusal);
                    else { result.mainButtonSources.Add(path); result.AddUi(def.iconPath, "MainButtonDef:" + def.defName + ":iconPath"); }
                }
            foreach (string path in PreparationBuiltinUi.Paths) result.AddUi(path, "Verse.TexButton:" + path);
            result.Finish();
            result.Ready = true;
            result.Status = "Resolved native consumer metadata: " + result.roles.Count + " source paths";
            if (!result.mainButtonsReady) result.Status += "; main button artwork retains native quality: " + result.mainButtonRefusal;
        }
        catch (Exception e)
        {
            result.roles.Clear(); result.Ready = false;
            result.Status = "Texture role metadata unavailable: " + e.Message;
        }
        return result;
    }

    private void AddTerrain(TerrainDef def)
    {
        bool pollutionRuns = ModsConfig.BiotechActive && (!string.IsNullOrEmpty(def.pollutionOverlayTexturePath)
            || !string.IsNullOrEmpty(def.pollutedTexturePath));
        string? overlay = pollutionRuns ? Normalize(def.pollutionOverlayTexturePath, false) : null;
        foreach (var decision in PreparationTerrainPolicy.Evaluate(def.texturePath, def.pollutedTexturePath, def.pollutionOverlayTexturePath,
            def.dontRender, def.customShader != null, def.customShaderParameters?.Count > 0, (int)def.edgeType, ModsConfig.BiotechActive))
        {
            string key = decision.Path;
            if (!sources.Contains(key)) continue;
            if (!decision.Allowed) { Reject(key, decision.Reason); continue; }
            AddGroup(new[] { key }, Array.Empty<string>(), Frame(Contract, PreparationTerrainPolicy.Contract, "TerrainDef-base-only", def.defName,
                ((int)def.edgeType).ToString(System.Globalization.CultureInfo.InvariantCulture), key, pollutionRuns.ToString(),
                def.pollutedTexturePath == null ? "null" : "value:" + def.pollutedTexturePath,
                overlay ?? "", def.pollutionShaderType?.defName ?? "", def.pollutionShaderType?.shaderPath ?? "",
                def.pollutionShaderType?.uiShaderPath ?? ""));
        }
    }

    private void AddIcons(BuildableDef def)
    {
        AddUi(def.uiIconPath, def.GetType().FullName + ":" + def.defName + ":uiIconPath");
        if (def.uiIconPathsStuff != null)
            foreach (var icon in def.uiIconPathsStuff)
                if (icon != null) AddUi(icon.IconPath, def.GetType().FullName + ":" + def.defName
                    + ":uiIconPathsStuff:" + icon.Appearance?.defName);
    }
    private void AddUi(string path, string evidence)
    {
        string? normalized = Normalize(path, false);
        if (normalized != null && sources.Contains(normalized))
            AddGroup(new[] { normalized }, Array.Empty<string>(), Frame(evidence, normalized), true);
    }
    private void AddGraphic(GraphicData? data, string owner, int depth)
    {
        if (data == null) return;
        if (depth > 16) throw new InvalidDataException("texture-role-attachment-depth");
        string? path = Normalize(data.texPath, false);
        if (path == null) return;
        ShaderTypeDef? shader = data.shaderType ?? ShaderTypeDefOf.Cutout;
        bool mask = shader?.shaderPath == "Map/CutoutComplex";
        bool knownShader = shader?.GetType() == typeof(ShaderTypeDef)
            && (shader.shaderPath == "Map/Cutout" || shader.shaderPath == "Map/EdgeDetect" || mask)
            && (string.IsNullOrEmpty(shader.uiShaderPath) || mask && shader.uiShaderPath == "Map/CutoutComplexUI");
        if (!knownShader || data.shaderParameters != null && data.shaderParameters.Count != 0)
        {
            RejectGraphic(data, "Native quality retained: shader=" + (shader?.defName ?? "missing")
                + ", path=" + (shader?.shaderPath ?? "missing") + ", UI=" + (shader?.uiShaderPath ?? "none")
                + ", parameters=" + (data.shaderParameters?.Count ?? 0));
            return;
        }
        string evidence = Frame(Contract, owner, data.graphicClass?.FullName ?? "", path,
            data.maskPath ?? "", shader!.defName, shader.shaderPath, shader.uiShaderPath ?? "");
        if (data.graphicClass == typeof(Graphic_Single) || data.graphicClass == typeof(Graphic_Terrain))
            AddSingle(path, data.maskPath, mask, evidence);
        else if (data.graphicClass == typeof(Graphic_Multi))
            AddMulti(path, data.maskPath, mask, evidence);
        else if (data.graphicClass == typeof(Graphic_Random) || data.graphicClass == typeof(Graphic_StackCount))
            AddCollection(path, mask, evidence);
        else RejectGraphic(data, "Graphic consumer is not qualified");
        // Attachments contain their own explicit GraphicData and are independent
        // role declarations. No cachedGraphic or lazy Graphic property is read.
        if (data.attachments != null)
            for (int i = 0; i < data.attachments.Count; i++)
                AddGraphic(data.attachments[i], owner + ":attachment:" + i, depth + 1);
    }
    private void AddSingle(string color, string? explicitMask, bool masked, string evidence)
    {
        if (!sources.Contains(color)) return;
        string? mask = masked ? Normalize(string.IsNullOrEmpty(explicitMask) ? color + "_m" : explicitMask!, false) : null;
        if (masked && (mask == null || !sources.Contains(mask)))
        { Reject(color, "Paired mask provider is not a mapped filesystem source"); return; }
        AddGroup(new[] { color }, mask == null ? Array.Empty<string>() : new[] { mask }, evidence);
    }
    private static readonly string[] Directions = { "_north", "_east", "_south", "_west" };
    private void AddMulti(string path, string? explicitMask, bool masked, string evidence)
    {
        string[] colors = Directions.Select(d => path + d).ToArray();
        // Missing filesystem directions can resolve from Resources/bundles.
        // Do not impersonate native null/fallback decisions using file absence.
        if (colors.Any(p => !sources.Contains(p)))
        {
            foreach (string item in colors.Concat(new[] { path }))
                if (sources.Contains(item)) Reject(item, "Incomplete directional family needs native resource fallback proof");
            return;
        }
        string? maskRoot = Normalize(string.IsNullOrEmpty(explicitMask) ? path : explicitMask!, false);
        string[] masks = !masked || maskRoot == null ? Array.Empty<string>()
            : Directions.Select(d => maskRoot + d + (string.IsNullOrEmpty(explicitMask) ? "m" : "")).ToArray();
        if (masked && (masks.Length != 4 || masks.Any(p => !sources.Contains(p))))
        {
            foreach (string item in colors.Concat(masks).Where(sources.Contains))
                Reject(item, "Incomplete directional masks need native resource fallback proof");
            return;
        }
        AddGroup(colors, masks, evidence);
    }
    private void AddCollection(string folder, bool masked, string evidence)
    {
        string prefix = folder + "/";
        // ContentFinder enumerates every holder, including shadowed providers.
        // Native collection selection subsequently uses texture.name (basename),
        // not the enumerated subdirectory path, when it requests a graphic.
        string[] names = sources.Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
            .Select(Path.GetFileName).Where(n => !n.EndsWith("_m", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (names.Length > 8192) throw new InvalidDataException("texture-role-collection-limit");
        string sharedMask = folder + "_m";
        foreach (var group in names.GroupBy(n => n.Split('_')[0], StringComparer.Ordinal))
        {
            bool directional = false;
            foreach (string name in group)
            {
                if (Directions.Any(d => name.Contains(d))) { directional = true; continue; }
                string color = prefix + name;
                if (masked && !sources.Contains(sharedMask))
                { Reject(color, "Collection shared-mask absence needs native resource fallback proof"); continue; }
                AddSingle(color, masked ? sharedMask : null, masked, Frame(evidence, "collection-single", name));
            }
            if (directional) AddMulti(prefix + group.Key, null, masked, Frame(evidence, "collection-multi", group.Key));
        }
    }

    private void RejectGraphic(GraphicData? data, string reason, int depth = 0)
    {
        if (data == null) return;
        if (depth > 16) throw new InvalidDataException("texture-role-attachment-depth");
        if (data.attachments != null)
            foreach (GraphicData attachment in data.attachments) RejectGraphic(attachment, reason, depth + 1);
        string? path = Normalize(data.texPath, false);
        if (path == null) return;
        // These are potential consumers of an unsupported declaration. Marking
        // them as refused cannot grant a role based on their filename.
        foreach (string source in sources.Where(p => p == path || p == path + "_m"
            || p.StartsWith(path + "/", StringComparison.Ordinal)
            || Directions.Any(d => p == path + d || p == path + d + "m"))) Reject(source, reason);
        string? mask = Normalize(data.maskPath, false);
        if (mask != null)
            foreach (string source in sources.Where(p => p == mask || Directions.Any(d => p == mask + d))) Reject(source, reason);
    }
    private void Reject(string path, string reason)
    {
        if (!claims.TryGetValue(path, out Claim claim)) claims[path] = claim = new Claim(RoleKind.Color);
        claim.Refusal = reason;
    }
    private void AddGroup(string[] colors, string[] masks, string evidence, bool ui = false)
    {
        string[] members = colors.Concat(masks).Distinct(StringComparer.Ordinal).ToArray();
        // Single and complete Multi declarations supply aligned arrays. These
        // edges, unlike the selection group, express actual dimension coupling.
        if (masks.Length != 0 && masks.Length != colors.Length) throw new InvalidDataException("texture-role-pair-mapping");
        var pairs = Enumerable.Range(0, masks.Length).Select(i => (Color: colors[i], Mask: masks[i])).ToArray();
        foreach (string member in members)
        {
            RoleKind kind = masks.Contains(member, StringComparer.Ordinal) ? RoleKind.Mask : ui ? RoleKind.Ui : RoleKind.Color;
            if (!claims.TryGetValue(member, out Claim claim)) claims[member] = claim = new Claim(kind);
            else if ((claim.Kind == RoleKind.Mask) != (kind == RoleKind.Mask)) claim.Refusal = "Conflicting color and mask consumers";
            else if (kind == RoleKind.Ui) claim.Kind = RoleKind.Ui;
            if (colors.Contains(member, StringComparer.Ordinal) && masks.Contains(member, StringComparer.Ordinal))
                claim.Refusal = "The same source is both color and mask in one consumer";
            claim.Evidence.Add(evidence);
            claim.Pairs.UnionWith(pairs);
            foreach (string related in members) claim.Linked.Add(related);
        }
    }
    private void Finish()
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string start in claims.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!visited.Add(start)) continue;
            var members = new List<string>(); var pending = new Queue<string>(); pending.Enqueue(start);
            var evidence = new HashSet<string>(StringComparer.Ordinal); string refusal = "";
            var pairs = new HashSet<(string Color, string Mask)>();
            while (pending.Count != 0)
            {
                string path = pending.Dequeue(); members.Add(path); Claim claim = claims[path];
                if (claim.Refusal.Length != 0) refusal = claim.Refusal;
                evidence.UnionWith(claim.Evidence);
                pairs.UnionWith(claim.Pairs);
                foreach (string related in claim.Linked) if (visited.Add(related)) pending.Enqueue(related);
                if (members.Count > 8192) throw new InvalidDataException("texture-role-group-limit");
            }
            string[] ordered = members.OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (refusal.Length != 0)
            { foreach (string member in ordered) refusals[member] = refusal; continue; }
            string group = Frame(Contract, Frame(ordered));
            var orderedPairs = pairs.OrderBy(p => p.Color, StringComparer.Ordinal).ThenBy(p => p.Mask, StringComparer.Ordinal).ToArray();
            string identity = Frame(Contract, activeIdentity, group, Frame(evidence.OrderBy(e => e, StringComparer.Ordinal).ToArray()),
                Frame(ordered.Select(p => p + ":" + claims[p].Kind).ToArray()), Frame(orderedPairs.Select(p => Frame(p.Color, p.Mask)).ToArray()));
            bool mainButton = ordered.Any(mainButtonSources.Contains);
            foreach (string member in ordered) roles[member] = new Role(claims[member].Kind, identity, group, ordered, orderedPairs, mainButton);
        }
    }
    private static string Frame(params string[] values) => PreparedTextureRuntime.Frame(values);
    internal static string? Normalize(string? path, bool sourceFilename = true)
    {
        if (string.IsNullOrEmpty(path)) return null;
        // ContentFinder.Get does not normalize a metadata request's slashes.
        if (!sourceFilename && path!.IndexOf('\\') >= 0) return null;
        string value = path!.Replace('\\', '/');
        if (sourceFilename && value.StartsWith("Textures/", StringComparison.Ordinal)) value = value.Substring(9);
        string extension = Path.GetExtension(value);
        if (sourceFilename && new[] { ".png", ".jpg", ".jpeg", ".dds", ".psd" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            value = value.Substring(0, value.Length - extension.Length);
        if (value.Length == 0 || value.IndexOfAny(new[] { ':', '*', '?', '\0' }) >= 0
            || value.Split('/').Any(p => p.Length == 0 || p == "." || p == "..")) return null;
        return value;
    }

    internal static readonly MethodBase[] ContractMethods = FindContractMethods();
    // Captured by the existing modern metadata host from original GOG rev573
    // 4a170804...; the Framework test host cannot resolve every game API.
    // Evidence: artifacts/ecosystem-next-20260910/c08/research/identity/role-pins.txt.
    internal static readonly Dictionary<string, string> ExpectedBodies = new(StringComparer.Ordinal)
    {
        ["Verse.BuildableDef:Void <PostLoad>b__78_0()"] = "8E70D438AE10F12FE12DAB40BA94F3EBF7B85848F17C202C87822BCAF34630D0",
        ["Verse.BuildableDef:Void PostLoad()"] = "FC337786ED2AD6A599ED8E0DAA6AE5AF3F69C7CEBE5E60996765D8ACFB064C8B",
        ["Verse.BuildableDef:Void ResolveIcon()"] = "75A5A6308891C388E81F213D26CC51CB450551A3344BAF4FCD7C298A8EFE673A",
        ["Verse.GraphicData:Void Init()"] = "A1936944606AD8D490A2B6BBE9F174188D78A652DDF340A4D29762D4DA00FACD",
        ["Verse.Graphic_Collection+<>c:Boolean <Init>b__6_0(UnityEngine.Texture2D)"] = "3934BCB6857DA16795677BB317B88C272AB3486A77C317F35F3313EB701E82E2",
        ["Verse.Graphic_Collection+<>c:System.String <Init>b__6_1(UnityEngine.Texture2D)"] = "5FF43C628174D13C7B4FF3BE98694011958456A461FFF6B102073AA94AE85672",
        ["Verse.Graphic_Collection+<>c:System.String <Init>b__6_3(System.ValueTuple`2[UnityEngine.Texture2D,System.String])"] = "0346E79A3295C88C48EEFD2A4F55E2453AD6D2B210093B5C33CD171813BD1578",
        ["Verse.Graphic_Collection+<>c:System.ValueTuple`2[UnityEngine.Texture2D,System.String] <Init>b__6_2(UnityEngine.Texture2D)"] = "BEF0AA330D1ECB5C26E8DEFED2AC79AD695319FFBC57FAB2C7D7E5BFB86F4363",
        ["Verse.Graphic_Collection:System.Type get_MultiGraphicType()"] = "91B463A36BB2F37F588810ADF36A39DF2F4B956848D1879EECF49A0490A9C819",
        ["Verse.Graphic_Collection:System.Type get_SingleGraphicType()"] = "E3F68F6CD5A413433B40A0E9B2C804D46F76074CC51D545B8E72402D53EDEB22",
        ["Verse.Graphic_Collection:Void Init(Verse.GraphicRequest)"] = "F6C9FAF56E94CEF7EDE5EAAE7E3955C265B81A5A8D5B9AA35A2EE7FE9F14EB78",
        ["Verse.Graphic_Multi:Void Init(Verse.GraphicRequest)"] = "3F9A6D132E797070F96526B5FF3354CD97472A8A4657B087E793A9922BBE4A4F",
        ["Verse.Graphic_Single:Void Init(Verse.GraphicRequest)"] = "81F101E6709DE06DEB1BB11B62C804EB01839D2B0F0BD86850D948BB2169760A",
        ["Verse.PlayDataLoader:Boolean get_Loaded()"] = "D436B63316A9FA57A55583D51C7231D4A60A8DB416FF010D8D62A020CE738204",
        ["Verse.ShaderDatabase:Boolean TryLoadShader(System.String, UnityEngine.Shader ByRef)"] = "67A25F5369ED5D3E86BF93A9331DED8E14CAC3402905E1BD6E1E344E94A3E1F8",
        ["Verse.ShaderUtility:Boolean SupportsMaskTex(UnityEngine.Shader)"] = "D1C684FFDE602AD22FDF47010B8C4FBB57A77E1980AD034356ADCAA093D111A9",
        ["Verse.ThingDef:Void <PostLoad>b__398_0()"] = "C01187BECB786A40E053EA2BBAD8D0CC2B9AE990951260090BF00E4A56D622CA",
        ["Verse.ThingDef:Void PostLoad()"] = "475B16A8C52A75538A7A84979B56E94D360365BADD4ED22B6E12B9885576994F",
        ["Verse.ThingDef:Void ResolveIcon()"] = "B769536B48CA6B07DB47BCEC3D4CD5EB35E6B84214EC6D774BD4246DC9E0E62B",
        ["RimWorld.AbilityDef:Void <PostLoad>b__88_0()"] = "E88BCF606329DA55C213E7A2486E02C04DDAB9019C45286DB99BA8D1655ED89B",
        ["RimWorld.AbilityDef:Void PostLoad()"] = "96704AD0F7335B1585AF54CFE4F5A6C69FD42D9B5CDA8D9D5E69BBB2000B6C71",
        ["Verse.TerrainDef:UnityEngine.Shader get_Shader()"] = "ED710B76B7ED25C7234EB28A898CDFBE0A4E7087F0822087018A7048F77BAF95",
        ["Verse.TerrainDef:UnityEngine.Shader get_ShaderPolluted()"] = "F0AED86420CA1E159771E9897BE556BD9CC29C55E4375B3DABCAE465B78A7E94",
        ["Verse.TerrainDef:Void <PostLoad>b__118_0()"] = "8185801A87FAC6B6DACB95F6BB63CA2EEF0880AD9CDB0B79845F8FBE6D6B5E0B",
        ["Verse.TerrainDef:Void PostLoad()"] = "DC69290AE761796E79A00F4593CB877394122F4D0FEF84AA0A4EC48B8833ECE8",
        ["Verse.TexButton:Void .cctor()"] = "A1E631D92AB4CCB1B9F3FF0313715960BA89037959FE8D460AF162B66085140E",
        ["RimWorld.MainButtonDef:UnityEngine.Texture2D get_Icon()"] = "0BA8C9C92E436CB16C990A2CFE7C43B8FFCE742E8527EBC4D7BF7042D404E4A5",
        ["RimWorld.MainButtonWorker:Void DoButton(UnityEngine.Rect)"] = "AFD15673683D5FE8069BBEE2DD6CE4249650933AF27AFC64C79202DDFD06E634",
    };
    internal static string MethodKey(MethodBase method) => method.DeclaringType!.FullName + ":" + method;
    private void CheckContract()
    {
        if (!GameBuildContract.Current.HasReviewedTextureConsumers || !RuntimeIdentity.ValidateBinaryIdentity(out _))
            throw new InvalidOperationException("Native role runtime is not qualified");
        if (ContractMethods.Length != ExpectedBodies.Count) throw new InvalidOperationException("Native role consumer methods changed");
        var mainButtonCompatibility = PreparationMainButtonCompatibility.Capture(mainButtonGuards);
        foreach (MethodBase method in ContractMethods)
        {
            if (PreparationMainButtonCompatibility.IsTarget(method))
            {
                string failure = "";
                if (!ExpectedBodies.TryGetValue(MethodKey(method), out string mainExpected)) failure = "Native method pin is missing";
                else if (!SemanticMethodIdentity.TryHash(method, out string mainActual, out string hashReason)) failure = "Native method identity unavailable: " + hashReason;
                else if (mainActual != mainExpected) failure = "Native method body differs: " + mainActual;
                else if (!PublishedPatchGuard.TryCreate(method, PngRuntime.Owner, out var mainGuard, true,
                    allowedForeignPatch: patch => mainButtonCompatibility.Allows(method, patch))) failure = "Native hook guard unavailable";
                else if (!mainGuard!.AllowsOriginalContract()) failure = "Consumer hooks refused; " +
                    (string.IsNullOrEmpty(mainButtonCompatibility.RefusalReason) ? "patch publication changed during verification" : mainButtonCompatibility.RefusalReason);
                else mainButtonGuards.Add(mainGuard);
                if (failure.Length != 0)
                {
                    mainButtonsReady = false;
                    if (mainButtonRefusal.Length != 0) mainButtonRefusal += "; ";
                    mainButtonRefusal += method.DeclaringType?.Name + "." + method.Name + ": " + failure;
                }
                continue;
            }
            if (!ExpectedBodies.TryGetValue(MethodKey(method), out string expected)
                || !SemanticMethodIdentity.TryHash(method, out string actual, out _) || actual != expected
                || !PublishedPatchGuard.TryCreate(method, PngRuntime.Owner, out var guard, true)
                || !guard!.AllowsOriginalContract()) throw new InvalidOperationException("Native role consumer changed: " + MethodKey(method));
            guards.Add(guard);
        }
    }
    private static MethodBase[] FindContractMethods()
    {
        var methods = new List<MethodBase>();
        void Add(Type type, params string[] names)
        {
            methods.AddRange(type.GetMethods(AccessTools.allDeclared).Where(m => names.Contains(m.Name)));
            foreach (Type nested in type.GetNestedTypes(AccessTools.allDeclared))
                methods.AddRange(nested.GetMethods(AccessTools.allDeclared).Where(m => names.Any(n => m.Name.StartsWith("<" + n + ">", StringComparison.Ordinal))));
            methods.AddRange(type.GetMethods(AccessTools.allDeclared).Where(m => names.Any(n => m.Name.StartsWith("<" + n + ">", StringComparison.Ordinal))));
        }
        Add(typeof(GraphicData), "Init"); Add(typeof(Graphic_Single), "Init"); Add(typeof(Graphic_Multi), "Init");
        Add(typeof(Graphic_Collection), "Init", "get_SingleGraphicType", "get_MultiGraphicType");
        Add(typeof(ShaderDatabase), "TryLoadShader"); Add(typeof(ShaderUtility), "SupportsMaskTex");
        Add(typeof(BuildableDef), "PostLoad", "ResolveIcon"); Add(typeof(ThingDef), "PostLoad", "ResolveIcon");
        Add(typeof(PlayDataLoader), "get_Loaded");
        Add(typeof(TerrainDef), "PostLoad", "get_Shader", "get_ShaderPolluted");
        Add(typeof(AbilityDef), "PostLoad");
        Add(typeof(MainButtonDef), "get_Icon");
        Add(typeof(MainButtonWorker), "DoButton");
        methods.Add(typeof(TexButton).TypeInitializer!);
        return methods.Distinct().OrderBy(MethodKey, StringComparer.Ordinal).ToArray();
    }
}
