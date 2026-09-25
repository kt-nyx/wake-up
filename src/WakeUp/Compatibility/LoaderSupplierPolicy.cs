// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using HarmonyLib;
using Mono.Cecil;
using Verse;

namespace WakeUp;

internal enum LoaderChoice { Automatic, WakeUp, Wowgag }

// Decisions are made once before XML/content can arm. Settings predict only
// which observer to reserve; actual hooks decide which operation must yield.
internal static class LoaderSupplierPolicy
{
    internal const string WowPackage = "wowgag.guiperformancepatch";
    internal const string WowXmlOwner = WowPackage + ".patchcache";
    internal const string WowContentOwner = WowPackage + ".contentpreload";
    internal const string WowHash = "9d4a8f3360730a4f6447d6c782d9b3ab4e2925f02984f8976418ce88a65fd1ef";
    private static bool initialized, frozen, boundaryInstalled, defer;
    private static bool wowPresent;
    private static MethodBase? boundary;
    private static bool reservedContent, xmlComplete, contentComplete;
    private static Assembly? wow;
    private static LoaderChoice xmlChoice, contentChoice;
    private static readonly List<(string Name, Action Action)> pending = new();
    private static readonly Dictionary<(Assembly Assembly, string Hash), bool> images = new();
    private static readonly Dictionary<Assembly, string> imagePaths = new();
    internal static bool YieldXml { get; private set; }
    internal static bool YieldContent { get; private set; }
    internal static bool ReserveContent => !frozen && reservedContent || YieldContent;
    internal static bool Frozen => frozen;
    internal static bool InsideXmlFallback { get; private set; }

    internal static LoaderChoice ParseChoice(string? value)
        => value == "WakeUp" ? LoaderChoice.WakeUp : value == "Wowgag" ? LoaderChoice.Wowgag : LoaderChoice.Automatic;
    internal static string ChoiceLabel(string value)
        => ParseChoice(value) == LoaderChoice.WakeUp ? "Wake-Up" : ParseChoice(value) == LoaderChoice.Wowgag ? "WOWGAG" : "Automatic";
    internal static string NextChoice(string value)
        => ParseChoice(value) == LoaderChoice.Automatic ? "WakeUp" : ParseChoice(value) == LoaderChoice.WakeUp ? "Wowgag" : "Automatic";

    internal static Assembly? Find(string package, string type)
    {
        var matches = LoadedModManager.RunningModsListForReading
            .Where(m => string.Equals(m.PackageId, package, StringComparison.OrdinalIgnoreCase))
            .SelectMany(m => m.assemblies.loadedAssemblies).Where(a => a.GetType(type, false) != null).Distinct().ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    internal static string ProviderFor(params MethodBase[] methods)
    {
        try
        {
            var patches = methods.SelectMany(Patches).ToArray();
            foreach (var supplier in new[] {
                ("taranchuk.fastergameloading", "FasterGameLoading.FasterGameLoadingMod", "Faster Game Loading"),
                ("sz.yaopt", "YaOpt.YaOptMod", "YaOpt"), (WowPackage, "GUIPerformancePatch.GppMod", "WOWGAG") })
            {
                var assembly = Find(supplier.Item1, supplier.Item2);
                if (assembly != null && patches.Any(p => p.PatchMethod.Module.Assembly == assembly)) return supplier.Item3;
            }
        }
        catch { }
        return "Wake-Up";
    }
    internal static bool HasYaOptContentWrapper(MethodBase method)
    {
        try
        {
            var assembly = Find("sz.yaopt", "YaOpt.YaOptMod");
            return assembly != null && PatchProcessor.GetOriginalInstructions(method).Any(i => i.operand is MethodInfo called
                && called.Module.Assembly == assembly && called.DeclaringType?.FullName == "YaOpt.Helpers.ContentManager" && called.Name == "GetContent");
        }
        catch { return false; }
    }

    // Presentation only. Never feed display labels back into semantic decisions
    // or acknowledgement keys. Use actual loaded patch assemblies, not guesses
    // based on whichever mods happen to be installed.
    internal static string DisplayProviderFor(params MethodBase[] methods)
    {
        try
        {
            var assemblies = methods.SelectMany(Patches).Where(p => p.PatchMethod.Module.Assembly != typeof(LoaderSupplierPolicy).Assembly)
                .Select(p => p.PatchMethod.Module.Assembly).Distinct().ToArray();
            return string.Join(" + ", LoadedModManager.RunningModsListForReading
                .Where(m => m.assemblies.loadedAssemblies.Any(assemblies.Contains)).Select(m => m.Name).Distinct());
        }
        catch { return ""; }
    }

    // Read an already-loaded supplier's on-disk identity, never load/execute a
    // second copy. MVID ties the inspected image to the loaded module.
    internal static bool MatchesImage(Assembly assembly, string expected)
    {
        if (images.TryGetValue((assembly, expected), out bool cached)) return cached;
        bool matches = MatchImageCore(assembly, expected);
        images[(assembly, expected)] = matches;
        return matches;
    }
    private static bool MatchImageCore(Assembly assembly, string expected)
    {
        try
        {
            IEnumerable<string> paths;
            if (!string.IsNullOrEmpty(assembly.Location)) paths = new[] { assembly.Location };
            else
            {
                // Prepatcher loads mod assemblies from bytes. Resolve only the
                // native effective files belonging to this exact loaded owner.
                var owners = LoadedModManager.RunningModsListForReading.Where(m => m.assemblies.loadedAssemblies.Contains(assembly)).ToArray();
                if (owners.Length != 1) return false;
                paths = ModContentPack.GetAllFilesForMod(owners[0], "Assemblies",
                    extension => string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)).Values.Select(f => f.FullName);
            }
            string? source = MatchSourceFile(assembly, expected, paths);
            if (source == null) return false;
            imagePaths[assembly] = source;
            return true;
        }
        catch { return false; }
    }
    internal static string? MatchSourceFile(Assembly assembly, string expected, IEnumerable<string> paths)
    {
        string? match = null;
        foreach (string path in paths)
        {
            OwnedCacheStore.RejectLinkedPath(path);
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            if (BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != expected) continue;
            stream.Position = 0;
            using var module = ModuleDefinition.ReadModule(stream);
            if (module.Mvid != assembly.ManifestModule.ModuleVersionId) continue;
            if (match != null) return null;
            match = path;
        }
        return match;
    }
    internal static string SourceImage(Assembly assembly)
        => imagePaths.TryGetValue(assembly, out string path) ? path : assembly.Location;

    internal static XmlNode? ReadSettings(string file, string type)
    {
        if (!File.Exists(file)) return null;
        if (new FileInfo(file).Length > 1024 * 1024) throw new InvalidDataException("Oversized settings");
        using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = new XmlDocument { XmlResolver = null }; document.Load(reader);
        var nodes = document.SelectNodes("/SettingsBlock/ModSettings");
        if (nodes?.Count != 1 || nodes[0]!.Attributes?["Class"]?.Value != type) throw new InvalidDataException("Unknown settings shape");
        return nodes[0];
    }
    internal static bool ReadDefaultOn(XmlNode? settings, string field)
    {
        var nodes = settings?.SelectNodes(field);
        if (nodes == null || nodes.Count == 0) return true;
        if (nodes.Count != 1 || !bool.TryParse(nodes[0]!.InnerText, out bool value)) throw new InvalidDataException("Unknown setting");
        return value;
    }
    internal static bool ContentExcluded(IEnumerable<string> packages)
        => packages.Any(p => new[] { "fastergameloading", "imageopt", "loadingprogress", "hyperdrive" }
            .Any(part => p.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0));

    internal static void EnsureEarly()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            var packages = LoadedModManager.RunningModsListForReading.Select(m => m.PackageId).ToArray();
            wowPresent = packages.Contains(WowPackage, StringComparer.OrdinalIgnoreCase);
            wow = Find(WowPackage, "GUIPerformancePatch.GppMod");
            // Presence reserves work only. A renamed/partially loaded entry
            // type must not bypass later actual-hook inspection.
            defer = wowPresent || packages.Contains("taranchuk.fastergameloading", StringComparer.OrdinalIgnoreCase)
                || packages.Contains("sz.yaopt", StringComparer.OrdinalIgnoreCase)
                || packages.Contains("vopaga.hyperdrive", StringComparer.OrdinalIgnoreCase)
                || packages.Contains(LoadingProgressCompatibility.PackageId, StringComparer.OrdinalIgnoreCase)
                || packages.Contains(LifecycleSupplierPolicy.RimThemesPackage, StringComparer.OrdinalIgnoreCase);
            var owner = LoadedModManager.RunningModsListForReading.Single(m => m.assemblies.loadedAssemblies.Contains(typeof(WakeUpMod).Assembly));
            XmlNode? own = null;
            try { own = ReadSettings(SettingsFile(owner, nameof(WakeUpMod)), "WakeUp.WakeUpSettings"); } catch { }
            xmlChoice = ParseChoice(own?.SelectSingleNode("xmlProvider")?.InnerText);
            contentChoice = ParseChoice(own?.SelectSingleNode("contentProvider")?.InnerText);
            if (wowPresent)
            {
                // Unknown settings/build: reserve until actual attachment is known.
                reservedContent = true;
                if (wow != null && MatchesImage(wow, WowHash))
                {
                    var mod = LoadedModManager.RunningModsListForReading.Single(m => m.assemblies.loadedAssemblies.Contains(wow));
                    try { reservedContent = ReadDefaultOn(ReadSettings(SettingsFile(mod, "GppMod"), "GUIPerformancePatch.GppSettings"), "preloadModContent"); }
                    catch { reservedContent = true; }
                    if (ContentExcluded(LoadedModManager.RunningModsListForReading.Select(m => m.PackageId))) reservedContent = false;
                }
            }
            if (defer && !packages.Contains("vopaga.hyperdrive", StringComparer.OrdinalIgnoreCase))
            {
                // CreateModClasses is already executing. This future-call
                // fallback removes itself BEFORE WOWGAG's priority-801 checker.
                // The existing LoadSources bridge normally removes it earlier.
                InstallBoundary(AccessTools.Method(typeof(LoadedModManager), "LoadModXML"));
            }
            // Hyperdrive rejects any earlier foreign LoadModXML prefix during
            // its constructor. Use the existing pre-XML LoadSources bridge only;
            // if that bridge is unavailable, pending work remains safely off.
        }
        catch (Exception e) { Log.Warning("[Wake-Up] Supplier selection reserved until the XML boundary: " + e.GetType().Name); }
    }
    private static string SettingsFile(ModContentPack mod, string handle)
        => Path.Combine(GenFilePaths.ConfigFolderPath, GenText.SanitizeFilename("Mod_" + mod.FolderName + "_" + handle + ".xml"));
    internal static void InstallBoundary(MethodBase target)
    {
        new Harmony("wakeup.supplier-selection").Patch(target,
            prefix: new HarmonyMethod(typeof(LoaderSupplierPolicy), nameof(BeforeXmlFallback)) {
                priority = int.MaxValue, before = new[] { WowXmlOwner, WowContentOwner } });
        boundary = target; boundaryInstalled = true;
    }

    internal static void Configure(WakeUpSettings? settings)
    {
        EnsureEarly();
        if (settings == null || frozen) return;
        xmlChoice = ParseChoice(settings.XmlProvider);
        contentChoice = ParseChoice(settings.ContentProvider);
    }
    internal static void Run(string name, Action action)
    {
        EnsureEarly();
        if (defer && !frozen) { pending.Add((name, action)); return; }
        StartupFeatureRunner.Run(name, action);
    }
    internal static void BeforeXml()
    {
        try { BeforeXmlCore(); }
        catch (Exception error)
        {
            frozen = true;
            foreach (var item in pending) CompatibilityStatus.SetupFailed(item.Name, error);
            pending.Clear();
            Log.Warning("[Wake-Up] Supplier selection could not complete; affected setup stays unavailable. " + error.GetType().Name);
        }
    }
    private static void BeforeXmlFallback()
    {
        InsideXmlFallback = true;
        try { BeforeXml(); } finally { InsideXmlFallback = false; }
    }
    private static void BeforeXmlCore()
    {
        if (frozen) return;
        EnsureEarly();
        if (boundaryInstalled)
        {
            new Harmony("wakeup.supplier-selection").Unpatch(boundary!,
                AccessTools.Method(typeof(LoaderSupplierPolicy), nameof(BeforeXmlFallback)));
            boundaryInstalled = false;
        }
        try
        {
            wowPresent |= LoadedModManager.RunningModsListForReading.Any(m => string.Equals(m.PackageId, WowPackage, StringComparison.OrdinalIgnoreCase));
            wow ??= Find(WowPackage, "GUIPerformancePatch.GppMod");
            ResolveInstalled(wowPresent, wow);
        }
        catch
        {
            // No invented active feature. Keep any reserved content boundary
            // unavailable if the live hook records themselves cannot be read.
            YieldContent = reservedContent;
            YieldXml = wowPresent;
            xmlComplete = contentComplete = false;
        }
        frozen = true;
        LifecycleSupplierPolicy.AdviseDefLoadCache();
        LoadingObservationRuntime.ReconcileContentObservation();
        foreach (var item in pending.ToArray()) StartupFeatureRunner.Run(item.Name, item.Action);
        pending.Clear();
        CompatibilityStatus.Registry?.FinishSetup();
    }
    internal static void ResolveInstalled(bool present, Assembly? assembly)
    {
        YieldXml = YieldContent = xmlComplete = contentComplete = false;
        if (!present) return;
        // Partial/unknown installations do not prove a working supplier,
        // but their actual interception still prevents taking over safely.
        YieldXml = XmlTargets().Any(m => HasOwner(m, WowXmlOwner));
        YieldContent = ContentTargets().Any(m => HasOwner(m, WowContentOwner));
        bool known = assembly != null && MatchesImage(assembly, WowHash);
        xmlComplete = known && Exact(assembly!, XmlHooks()) && ReadAttached(assembly!, "GppPatchCache", "enabled");
        contentComplete = known && Exact(assembly!, ContentHooks()) && ReadAttached(assembly!, "GppContentPreload", "Attached");
    }

    internal static string Reason(bool xml)
    {
        bool complete = xml ? xmlComplete : contentComplete;
        var choice = xml ? xmlChoice : contentChoice;
        string operation = xml ? "XML reuse" : "content preload";
        return !complete ? "WOWGAG has an unqualified or partial " + operation + " hook set. Affected Wake-Up work remains unavailable; check the supplier and restart."
            : choice == LoaderChoice.WakeUp ? "To use Wake-Up for this operation, turn off WOWGAG's " + operation + " in its settings and restart. Its attached operation is preserved for this launch."
            : "WOWGAG owns " + operation + " for this launch. Overlapping Wake-Up work and its checked observation hooks are omitted; saved choices are unchanged.";
    }
    internal static void RefuseXml(params string[] ids)
    { foreach (string id in ids) CompatibilityStatus.Refuse(id, Reason(true), DecisionCode(true, xmlComplete, xmlChoice), "WOWGAG"); }
    internal static void RefuseContent(string id)
        => CompatibilityStatus.Refuse(id, Reason(false), DecisionCode(false, contentComplete, contentChoice), "WOWGAG");
    internal static string DecisionCode(bool xml, bool complete, LoaderChoice choice)
        => "supplier-" + (xml ? "xml" : "content") + (complete ? "" : "-unqualified")
            + (choice == LoaderChoice.WakeUp ? "-action-required" : "");
    private static bool ReadAttached(Assembly assembly, string type, string name)
    {
        var field = AccessTools.Field(assembly.GetType("GUIPerformancePatch." + type), name);
        return field?.IsStatic == true && field.FieldType == typeof(bool) && field.GetValue(null) is true;
    }

    internal static IEnumerable<Patch> Patches(MethodBase target)
    {
        var p = Harmony.GetPatchInfo(target);
        return p == null ? Array.Empty<Patch>() : p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers).Concat(p.Finalizers).Concat(p.InnerPrefixes).Concat(p.InnerPostfixes);
    }
    private static bool HasOwner(MethodBase m, string owner) => Patches(m).Any(p => p.owner == owner);
    internal static MethodBase[] ContentTargets() => new MethodBase[] {
        AccessTools.Method(typeof(LoadedModManager), "LoadModXML"), AccessTools.Method(typeof(LongEventHandler), "LongEventsUpdate"),
        AccessTools.Method(typeof(ModContentPack), "ReloadContentInt"), AccessTools.Method(typeof(ModContentHolder<UnityEngine.Texture2D>), "ReloadAll"),
        AccessTools.Method(typeof(ModContentHolder<UnityEngine.AudioClip>), "ReloadAll") };
    internal static MethodBase[] XmlTargets()
    {
        var methods = new List<MethodBase>();
        foreach (string name in new[] { "ApplyPatches", "ParseAndProcessXML", "ClearCachedPatches", "LoadModXML", "CombineIntoUnifiedXML" })
            methods.Add(AccessTools.Method(typeof(LoadedModManager), name));
        methods.Add(AccessTools.Method(typeof(PatchOperation), "Apply", new[] { typeof(XmlDocument) }));
        methods.AddRange(typeof(XmlNode).GetMethods().Where(m => (m.Name == "SelectSingleNode" || m.Name == "SelectNodes") && m.GetParameters()[0].ParameterType == typeof(string)));
        // Only published hooks can block a provider. Include our native workers
        // explicitly and inspect other actually patched native workers without
        // loading every unrelated game type and its optional dependencies.
        foreach (string suffix in new[] { "Add", "AddModExtension", "Insert", "Remove", "Replace", "SetName", "AttributeAdd", "AttributeRemove", "AttributeSet", "Test", "Conditional" })
        {
            var type = typeof(PatchOperation).Assembly.GetType("Verse.PatchOperation" + suffix);
            if (type != null) methods.Add(AccessTools.Method(type, "ApplyWorker"));
        }
        methods.AddRange(Harmony.GetAllPatchedMethods().Where(m => m.Name == "ApplyWorker"
            && m.DeclaringType?.Assembly == typeof(PatchOperation).Assembly && typeof(PatchOperation).IsAssignableFrom(m.DeclaringType)
            && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(XmlDocument) })));
        methods.Add(AccessTools.Method(typeof(TKeySystem), "Parse"));
        methods.Add(AccessTools.Method(typeof(DirectXmlLoader), "XmlAssetsInModFolder"));
        methods.Add(AccessTools.Method(typeof(ModContentPack), "LoadDefs"));
        methods.AddRange(typeof(LoadableXmlAsset).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        return methods.Distinct().ToArray();
    }
    private static (MethodBase Target, string Type, string Method, HarmonyPatchType Kind, int Priority)[] ContentHooks() => new[] {
        (ContentTargets()[0], "GppContentPreload", "PostLoadXml", HarmonyPatchType.Postfix, 0),
        (ContentTargets()[1], "GppContentPreload", "PostLongEvents", HarmonyPatchType.Postfix, 0),
        (ContentTargets()[2], "GppContentPreload", "PreReload", HarmonyPatchType.Prefix, 803) };
    private static (MethodBase Target, string Type, string Method, HarmonyPatchType Kind, int Priority)[] XmlHooks()
    {
        var hooks = new[] { "LoadModXML", "CombineIntoUnifiedXML", "ApplyPatches", "ParseAndProcessXML", "ClearCachedPatches" }
            .Zip(new[] { "PreLoadXml", "PreCombine", "PreApply", "PreParse", "PreClear" },
                (name, hook) => ((MethodBase)AccessTools.Method(typeof(LoadedModManager), name), "GppPatchCache", hook, HarmonyPatchType.Prefix, 801)).ToList();
        hooks.Add((AccessTools.Method(typeof(LoadedModManager), "LoadModXML"), "GppPatchCache", "PostLoadXml", HarmonyPatchType.Postfix, 0));
        hooks.Add((AccessTools.Method(typeof(LoadedModManager), "ClearCachedPatches"), "GppPatchCache", "PostClear", HarmonyPatchType.Postfix, 0));
        hooks.Add((AccessTools.Method(typeof(LoadedModManager), "ApplyPatches"), "GppPatchCache", "FinApply", HarmonyPatchType.Finalizer, Priority.Normal));
        hooks.Add((AccessTools.Method(typeof(PatchOperation), "Apply", new[] { typeof(XmlDocument) }), "GppPatchCache", "PreOp", HarmonyPatchType.Prefix, 801));
        foreach (string name in new[] { "Message", "Warning", "Error" })
            hooks.Add((AccessTools.Method(typeof(Log), name, new[] { typeof(string) }), "GppPatchCache", "PostLog" + name, HarmonyPatchType.Postfix, 0));
        var yaopt = Find("sz.yaopt", "YaOpt.YaOptMod")?.GetType("YaOpt.Helpers.XPathReducer");
        if (yaopt != null) hooks.Add((AccessTools.Method(yaopt, "CreateCache"), "GppPatchCache", "PreYaOptCache", HarmonyPatchType.Prefix, 801));
        return hooks.ToArray();
    }
    private static bool Exact(Assembly assembly, IEnumerable<(MethodBase Target, string Type, string Method, HarmonyPatchType Kind, int Priority)> hooks)
    {
        var required = hooks.ToArray();
        return required.All(h => {
            var method = AccessTools.Method(assembly.GetType("GUIPerformancePatch." + h.Type), h.Method);
            var record = Harmony.GetPatchInfo(h.Target);
            var list = h.Kind == HarmonyPatchType.Prefix ? record?.Prefixes : h.Kind == HarmonyPatchType.Postfix ? record?.Postfixes : record?.Finalizers;
            string owner = h.Type == "GppPatchCache" ? WowXmlOwner : WowContentOwner;
            return method != null && SupplierBodyIdentity.Matches(method) && list?.Count(p => p.PatchMethod == method && p.priority == h.Priority
                && p.owner == owner) == 1
                && Patches(h.Target).Count(p => p.owner == owner) == required.Count(r => r.Target == h.Target)
                && !Patches(method).Any();
        });
    }
}
