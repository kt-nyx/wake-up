// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace WakeUp.Preparation;

public enum ModRootKind { Local, Workshop }
public sealed record ModSearchRoot(string Path, ModRootKind Kind);
public sealed record LoadoutRequest(string GameRoot, string ModsConfigPath, IReadOnlyList<ModSearchRoot> AdditionalRoots);
public sealed record DiscoveryProgress(int ProvidersCompleted, int ProvidersTotal, int FilesFound, string Message);
public sealed record DiscoveredProvider(string PackageId, string BasePackageId, string Name, string Root,
    int Order, IReadOnlyList<string> Folders, string MetadataSha256, string LoadFoldersSha256);
public sealed class DiscoveredTexture
{
    public DiscoveredProvider Provider { get; init; } = null!;
    public string LogicalPath { get; init; } = "";
    public string SourcePath { get; init; } = "";
    public string LoadFolder { get; init; } = "";
    public long SourceBytes { get; init; }
    public bool AuthoredDds => SourcePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase);
    public bool EffectiveInProvider { get; init; }
    public bool GlobalWinner { get; internal set; }
    public string Reason { get; init; } = "";
}
public sealed record DiscoveredLoadout(string GameRoot, string ModsConfigPath, string ModsConfigSha256,
    string GameAssemblySha256, IReadOnlyList<DiscoveredProvider> Providers,
    IReadOnlyList<DiscoveredTexture> Textures, IReadOnlyList<string> Problems)
{
    public bool CanPrepare => Problems.Count == 0;
    public IReadOnlyList<string> Notices { get; init; } = Array.Empty<string>();
    public LoadoutRequest? Request { get; init; }
}

// No game assembly is loaded or shipped by this reader. These rules mirror the
// inspected rev573 methods; uncertain loadouts are shown but cannot be published.
public static class LoadoutDiscovery
{
    public const string GameAssemblySha256 = "4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28";
    public const string SelectionContract = "gog573-folders-and-holder-selection-v1";
    private static readonly Version GameVersion = new Version(1, 6, 4871, 573);
    private static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".psd", ".dds" };
    private sealed record Metadata(string Id, string Name, string Root, bool Workshop, bool Official,
        string AboutHash, string FolderHash, XDocument? Folders);
    private sealed record Folder(string Name, string[] Any, string[] All, string[] None);

    public static DiscoveredLoadout Scan(LoadoutRequest request, CancellationToken cancellation,
        IProgress<DiscoveryProgress>? progress = null)
    {
        string game = Path.GetFullPath(request.GameRoot), config = Path.GetFullPath(request.ModsConfigPath);
        string assembly = Path.Combine(game, "RimWorldWin64_Data", "Managed", "Assembly-CSharp.dll");
        string assemblyHash = Hash(assembly);
        var problems = new List<string>();
        var notices = new List<string>();
        if (!string.Equals(assemblyHash, GameAssemblySha256, StringComparison.Ordinal))
            problems.Add("This game is not the qualified GOG 1.6.4871 rev573 build. Discovery is informational; preparation is unavailable.");
        byte[] configBytes = File.ReadAllBytes(config);
        XDocument configuration = Xml(configBytes);
        if (configuration.Root?.Name.LocalName != "ModsConfigData")
            throw new InvalidDataException("Select an existing RimWorld ModsConfig.xml loadout.");
        string configVersion = (string?)configuration.Root.Element("version") ?? "";
        if (!configVersion.StartsWith("1.6.", StringComparison.Ordinal))
            problems.Add("This loadout has an old or unknown configuration version. Let the game update it before preparing; Wake-Up will not rewrite it.");
        string[] activeIds = configuration.Root.Element("activeMods")?.Elements("li")
            .Select(x => x.Value).ToArray() ?? Array.Empty<string>();
        if (activeIds.Length == 0) problems.Add("The selected loadout contains no active mods.");
        var metadata = new List<Metadata>();
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddRoot(Path.Combine(game, "Data"), false, true);
        AddRoot(Path.Combine(game, "Mods"), false, false);
        foreach (ModSearchRoot root in request.AdditionalRoots)
            AddRoot(Path.GetFullPath(root.Path), root.Kind == ModRootKind.Workshop, false);

        // Native discovery distinguishes local/Workshop copies by adding _steam
        // only when their base package IDs collide. Same-source duplicates are
        // deliberately not guessed: directory enumeration is not a user choice.
        var byId = new Dictionary<string, Metadata>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var group in metadata.GroupBy(m => m.Id, StringComparer.CurrentCultureIgnoreCase))
        {
            Metadata[] nonWorkshop = group.Where(m => !m.Workshop).ToArray();
            Metadata[] workshop = group.Where(m => m.Workshop).ToArray();
            if (nonWorkshop.Length > 1 || workshop.Length > 1)
            {
                string issue = "Ambiguous installed copies of " + group.Key + ": " + string.Join("; ", group.Select(m => m.Root));
                if (activeIds.Any(id => id.Equals(group.Key, StringComparison.CurrentCultureIgnoreCase) || id.Equals(group.Key + "_steam", StringComparison.CurrentCultureIgnoreCase))) problems.Add(issue);
                else notices.Add(issue);
            }
            if (nonWorkshop.Length > 0) AddIdentifier(group.Key, nonWorkshop[0]);
            if (workshop.Length > 0) AddIdentifier(group.Key + (nonWorkshop.Length > 0 ? "_steam" : ""), workshop[0]);
        }
        var ordered = new List<(string Id, Metadata Mod)>();
        var activeBaseIds = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (string configuredId in activeIds)
        {
            cancellation.ThrowIfCancellationRequested();
            string id = configuredId;
            if (!byId.TryGetValue(id, out Metadata? mod))
            {
                // Native startup repairs these identifiers in memory; leave the
                // selected XML unchanged and make that repair visible to users.
                if (id.EndsWith("_steam", StringComparison.Ordinal) && byId.TryGetValue(id.Substring(0, id.Length - 6), out mod))
                    id = id.Substring(0, id.Length - 6);
                else
                {
                    Metadata[] named = metadata.Where(m => Path.GetFileName(m.Root) == id).ToArray();
                    if (named.Length == 1)
                    {
                        mod = named[0];
                        id = byId.First(p => ReferenceEquals(p.Value, mod)).Key;
                    }
                }
            }
            if (mod == null) { problems.Add("Active mod was not found: " + configuredId); continue; }
            if (!activeBaseIds.Add(mod.Id))
            {
                problems.Add("More than one copy of " + mod.Id + " is active; native startup disables duplicates. Resolve this loadout in game first.");
                continue;
            }
            ordered.Add((id, mod));
        }
        var knownExpansions = new HashSet<string>(configuration.Root.Element("knownExpansions")?.Elements("li")
            .Select(x => x.Value) ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (Metadata mod in metadata.Where(m => m.Official && m.Id != "ludeon.rimworld"))
            if (!knownExpansions.Contains(mod.Id))
                problems.Add("New expansion " + mod.Name + " would change this loadout during native startup. Open this loadout in game before preparing it.");
        var providers = new List<DiscoveredProvider>();
        var textures = new List<DiscoveredTexture>();
        foreach (var entry in ordered)
        {
            cancellation.ThrowIfCancellationRequested();
            Metadata mod = entry.Mod;
            try
            {
                IReadOnlyList<string> folders = ResolveFolders(mod.Root, mod.Folders, activeBaseIds);
                var provider = new DiscoveredProvider(entry.Id, mod.Id, mod.Name, mod.Root, providers.Count,
                    folders, mod.AboutHash, mod.FolderHash);
                providers.Add(provider);
                DiscoverTextures(provider, textures, cancellation);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException || ex is ArgumentException)
            { problems.Add("Cannot resolve " + mod.Name + ": " + ex.Message); }
            progress?.Report(new DiscoveryProgress(providers.Count, ordered.Count, textures.Count, mod.Name));
        }
        // Global ContentFinder checks holders in reverse active order. Each
        // holder's own selection remains eligible for direct mod.GetContent calls.
        var winners = new Dictionary<string, DiscoveredTexture>(StringComparer.Ordinal);
        foreach (DiscoveredTexture texture in textures.Where(t => t.EffectiveInProvider))
            winners[HolderKey(texture.LogicalPath)] = texture;
        foreach (DiscoveredTexture winner in winners.Values) winner.GlobalWinner = true;
        return new DiscoveredLoadout(game, config, Hex(SHA256.HashData(configBytes)), assemblyHash, providers, textures, problems) { Notices = notices, Request = request };

        void AddIdentifier(string id, Metadata mod)
        {
            if (!byId.TryAdd(id, mod)) problems.Add("Ambiguous effective package ID: " + id);
        }
        void AddRoot(string root, bool workshop, bool official)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!Directory.Exists(root))
            {
                if (!official && request.AdditionalRoots.Any(r => Path.GetFullPath(r.Path).Equals(root, StringComparison.OrdinalIgnoreCase)))
                    problems.Add("Mod search folder does not exist: " + root);
                return;
            }
            foreach (string child in Directory.GetDirectories(root))
            {
                cancellation.ThrowIfCancellationRequested();
                string full = Path.GetFullPath(child);
                if (!seenRoots.Add(full)) continue;
                try
                {
                    string about = Path.Combine(full, "About", "About.xml");
                    if (!File.Exists(about)) continue;
                    byte[] bytes = File.ReadAllBytes(about);
                    XDocument document = Xml(bytes);
                    string id = ((string?)document.Root?.Element("packageId") ?? "").ToLower(CultureInfo.CurrentCulture);
                    if (id.Length == 0) { notices.Add("Missing package ID in " + about); continue; }
                    string name = (string?)document.Root?.Element("name") ?? Path.GetFileName(full);
                    string folderFile = Path.Combine(full, "LoadFolders.xml");
                    byte[]? folderBytes = File.Exists(folderFile) ? File.ReadAllBytes(folderFile) : null;
                    metadata.Add(new Metadata(id, name, full, workshop, official, Hex(SHA256.HashData(bytes)),
                        folderBytes == null ? "absent" : Hex(SHA256.HashData(folderBytes)), folderBytes == null ? null : Xml(folderBytes)));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException)
                { notices.Add("Cannot read mod metadata at " + full + ": " + ex.Message); }
            }
        }
    }

    public static IReadOnlyList<string> ResolveFolders(string root, XDocument? loadFolders, ISet<string> activeBaseIds)
    {
        root = Path.GetFullPath(root);
        var versions = new Dictionary<string, List<Folder>>(StringComparer.Ordinal);
        if (loadFolders?.Root != null)
        {
            foreach (XElement versionNode in loadFolders.Root.Elements())
            {
                string name = versionNode.Name.LocalName.ToLower(CultureInfo.CurrentCulture);
                if (name.StartsWith("v", StringComparison.Ordinal)) name = name.Substring(1);
                if (!versions.TryGetValue(name, out List<Folder>? list)) versions.Add(name, list = new List<Folder>());
                foreach (XElement node in versionNode.Elements())
                {
                    string folder = node.Value;
                    if (folder == "/" || folder == "\\") folder = "";
                    list.Add(new Folder(folder.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
                        Conditions(node, "IfModActive"), Conditions(node, "IfModActiveAll"), Conditions(node, "IfModNotActive")));
                }
            }
        }
        List<Folder>? chosen = null;
        if (versions.TryGetValue("1.6.4871", out List<Folder>? exact) && exact.Count > 0) chosen = exact;
        else
        {
            string? previous = versions.Keys.Where(k => k != "default" && k.Contains('.') && VersionForComparison(k) is Version v && v <= GameVersion)
                .OrderByDescending(k => k, StringComparer.CurrentCulture).FirstOrDefault();
            if (previous != null) chosen = versions[previous];
            else if (versions.TryGetValue("default", out List<Folder>? fallback)) chosen = fallback;
        }
        if (chosen != null)
            return chosen.AsEnumerable().Reverse().Where(f => (f.Any.Length == 0 || f.Any.Any(activeBaseIds.Contains))
                    && f.All.All(activeBaseIds.Contains) && !f.None.Any(activeBaseIds.Contains))
                .Select(f => CheckedFolder(root, f.Name)).ToArray();
        var result = new List<string>();
        string exactFolder = Path.Combine(root, "1.6");
        if (Directory.Exists(exactFolder)) result.Add(exactFolder);
        else
        {
            Version current = new Version(0, 0);
            // Native TryParseVersionString consumes only the first two parts and
            // reconstructs the path from those two parts, even for longer names.
            foreach (Version candidate in Directory.GetDirectories(root).Select(Path.GetFileName)
                .Select(ParseFolderVersion).Where(v => v != null).Cast<Version>().OrderBy(v => v))
                if ((candidate > current || current > GameVersion) && (candidate <= GameVersion || current.Major == 0)) current = candidate;
            if (current.Major > 0) result.Add(Path.Combine(root, current.ToString()));
        }
        string common = Path.Combine(root, "Common");
        if (Directory.Exists(common)) result.Add(common);
        result.Add(root);
        return result;
    }

    private static void DiscoverTextures(DiscoveredProvider provider, List<DiscoveredTexture> result, CancellationToken cancellation)
    {
        var files = new Dictionary<string, (string Path, string Folder)>(StringComparer.Ordinal);
        var shadowed = new List<(string Logical, string Path, string Folder)>();
        foreach (string folder in provider.Folders)
        {
            string textures = Path.Combine(folder, "Textures");
            if (!Directory.Exists(textures)) continue;
            foreach (string file in Directory.GetFiles(textures, "*.*", SearchOption.AllDirectories))
            {
                cancellation.ThrowIfCancellationRequested();
                if (!Extensions.Contains(Path.GetExtension(file))) continue;
                string logical = Path.GetFullPath(file).Substring(folder.Length + 1);
                if (!files.TryAdd(logical, (file, folder))) shadowed.Add((logical, file, folder));
            }
        }
        var dds = new HashSet<string>(files.Keys.Select(k => k.ToLowerInvariant()).Where(k => k.EndsWith(".dds", StringComparison.Ordinal)));
        var holderKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            string logical = file.Key;
            // Preserve rev573's four-character suffix removal for .jpeg too.
            bool prefersDds = logical.Length > 4 && !logical.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)
                && dds.Contains(logical.ToLowerInvariant().Substring(0, logical.Length - 4) + ".dds");
            bool duplicate = !prefersDds && !holderKeys.Add(HolderKey(logical));
            string reason = prefersDds ? "Authored DDS is selected by the native loader" : duplicate ? "Duplicate logical path; native holder keeps its first item" : "";
            Add(logical, file.Value.Path, file.Value.Folder, !prefersDds && !duplicate, reason);
        }
        foreach (var file in shadowed) Add(file.Logical, file.Path, file.Folder, false, "A higher-priority load folder supplies this exact filename");
        void Add(string logical, string file, string folder, bool effective, string reason)
        {
            result.Add(new DiscoveredTexture { Provider = provider, LogicalPath = logical.Replace('\\', '/'), SourcePath = Path.GetFullPath(file),
                LoadFolder = folder, SourceBytes = new FileInfo(file).Length, EffectiveInProvider = effective, Reason = reason });
        }
    }
    private static string HolderKey(string path)
    {
        string normalized = path.Replace('\\', '/');
        int dot = normalized.LastIndexOf('.');
        string withoutExtension = dot < 0 ? normalized : normalized.Substring(0, dot);
        return withoutExtension.StartsWith("Textures/", StringComparison.Ordinal) ? withoutExtension.Substring(9) : withoutExtension;
    }
    private static string[] Conditions(XElement node, string name) => node.Attribute(name) is XAttribute a
        ? a.Value.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToArray() : Array.Empty<string>();
    private static string CheckedFolder(string root, string folder)
    {
        string combined = Path.GetFullPath(Path.Combine(root, folder));
        if (!combined.Equals(root, StringComparison.OrdinalIgnoreCase) && !combined.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("LoadFolders entry leaves its own mod root; external preparation retains native for this provider: " + folder);
        return combined;
    }
    private static Version? ParseFolderVersion(string? text)
    {
        string[] parts = (text ?? "").Split('.');
        return parts.Length >= 2 && int.TryParse(parts[0], out int major) && major >= 0 && int.TryParse(parts[1], out int minor) && minor >= 0
            ? new Version(major, minor) : null;
    }
    private static Version? VersionForComparison(string text)
    {
        string[] parts = text.Split('.');
        if (parts.Length > 3) return null;
        var numbers = new int[3];
        for (int i = 0; i < parts.Length; i++) if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0) return null;
        return new Version(numbers[0], numbers[1], numbers[2]);
    }
    private static XDocument Xml(byte[] data)
    {
        using var stream = new MemoryStream(data, false);
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    private static string Hash(string file) { using FileStream stream = File.OpenRead(file); return Hex(SHA256.HashData(stream)); }
    private static string Hex(byte[] data) => Convert.ToHexString(data).ToLowerInvariant();
}
