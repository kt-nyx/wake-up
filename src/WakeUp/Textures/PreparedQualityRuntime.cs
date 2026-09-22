// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld.IO;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace WakeUp;

// Ordinary producers and callbacks finish before an explicit group replaces
// its still-unobserved native holder entries. A late mutation therefore leaves
// the complete native group intact; no callback crosses a prepared-source lease.
internal static class PreparedQualityRuntime
{
    internal sealed class Source
    {
        internal ModContentPack Provider = null!;
        internal string Selection = "", Logical = "";
        internal FileInfo File = null!;
        internal string Slot => PreparationContract.Slot(Selection, Logical);
    }
    private static PreparedTextureRoles? roles;
    private static List<Source>? inventory;
    private static string active = "", helper = "";
    private static readonly HashSet<string> attempted = new(StringComparer.Ordinal);
    private sealed class LoadedSource
    {
        internal Source Source = null!;
        internal LoadedContentItem<Texture2D> Item = null!;
        internal Texture2D Native = null!;
        internal Dictionary<string,Texture2D> Holder = null!;
    }
    private static readonly Dictionary<string, LoadedSource> loaded = new(StringComparer.Ordinal);
    private static readonly HashSet<string> exposed = new(StringComparer.Ordinal);
    private static PublishedPatchGuard? holderGuard;
    private static string unsafeReason = "";
    internal static void Initialize(Harmony harmony)
    {
        var ctor = AccessTools.Constructor(typeof(LoadedContentItem<Texture2D>),
            new[] { typeof(VirtualFile), typeof(Texture2D), typeof(IDisposable) });
        if (!PublishedPatchGuard.TryCreate(ctor, PngRuntime.Owner, out holderGuard, true))
        { unsafeReason = "holder callback guard unavailable"; return; }
        harmony.Patch(AccessTools.Method(typeof(ModContentHolder<Texture2D>), "Get"),
            prefix: new HarmonyMethod(typeof(PreparedQualityRuntime), nameof(ReadOne)));
        harmony.Patch(AccessTools.Method(typeof(ModContentHolder<Texture2D>), "GetAllUnderPath"),
            prefix: new HarmonyMethod(typeof(PreparedQualityRuntime), nameof(ReadFolder)));
    }
    private static void ReadOne(string __0)
    { if (PreparedTextureRuntime.UseAtStartup) exposed.Add(__0 ?? ""); }
    // Shared successful-route exposure: a remembered Resources/bundle route
    // skips native holder misses, but the path is still observed by its caller.
    // Repeated observations are idempotent and never create or replace assets.
    internal static void ObserveRoute(string path) => ReadOne(path);
    internal static bool IsRoutingReadPrefix(Patch patch)
        => patch.owner == PngRuntime.Owner && patch.PatchMethod == AccessTools.Method(typeof(PreparedQualityRuntime), nameof(ReadOne));
    private static void ReadFolder(string __0)
    {
        if (!PreparedTextureRuntime.UseAtStartup) return;
        string prefix = string.IsNullOrEmpty(__0) ? "" : __0.TrimEnd('/') + "/";
        // Prefixes are retained even when the later provider is not loaded yet.
        exposed.Add(prefix + "*");
    }
    internal static void UnsafeBoundary(string reason)
    { if (PreparedTextureRuntime.UseAtStartup && unsafeReason.Length == 0) unsafeReason = reason; }
    internal static void BeforeCallback()
    { if (holderGuard?.AllowsOriginalContract() != true) UnsafeBoundary("foreign texture holder callback"); }
    internal static void BeforeSource(FileInfo source)
    {
        BeforeCallback();
        if (!PngRuntime.QualityCallbacksAllowed(source)) UnsafeBoundary("source loader callback changed");
    }
    internal static void Loaded(ModContentPack provider, string logical, FileInfo file, string selection, LoadedContentItem<Texture2D> item,
        Action beforeCollection)
    {
        if (!PreparedTextureRuntime.UseAtStartup || !CacheLaunchPolicy.Current.AllowRead) return;
        string slot = PreparationContract.Slot(selection, logical);
        if (!PreparedTextureRuntime.StartupStore.HasOwner(slot) || loaded.ContainsKey(slot)) return;
        // The predicates above inspect current in-memory state and release the
        // store lock. Only real collection crosses a holder/callback boundary.
        beforeCollection();
        loaded.Add(slot, new LoadedSource { Source = new Source { Provider=provider, Logical=logical, File=file, Selection=selection },
            Item=item, Native=item.contentItem, Holder=provider.GetContentHolder<Texture2D>().contentList });
    }
    internal static void CompleteReload()
    {
        if (!PreparedTextureRuntime.UseAtStartup || !CacheLaunchPolicy.Current.AllowRead) return;
        foreach (var candidate in loaded.Values.ToArray()) TryApply(candidate.Source);
    }
    internal static long Applied, Groups, Refused;
    internal static string Status = "No explicit quality output applied";

    internal static PngCache.Entry ToEntry(PreparationPixels e) => new() {
        Width=e.Width, Height=e.Height, GraphicsFormat=e.GraphicsFormat, TextureFormat=e.TextureFormat,
        Mips=e.Mips, Filter=e.Filter, WrapU=e.WrapU, WrapV=e.WrapV, WrapW=e.WrapW, Aniso=e.Aniso,
        Bias=e.Bias, Readable=e.Readable, Pixels=e.Pixels };
    internal static PreparationPixels ToPixels(PngCache.Entry e) => new() {
        Width=e.Width, Height=e.Height, GraphicsFormat=e.GraphicsFormat, TextureFormat=e.TextureFormat,
        Mips=e.Mips, Filter=e.Filter, WrapU=e.WrapU, WrapV=e.WrapV, WrapW=e.WrapW, Aniso=e.Aniso,
        Bias=e.Bias, Readable=e.Readable, Pixels=e.Pixels };
    internal static List<Source> Sources()
    {
        var found = new List<Source>();
        foreach (var mod in LoadedModManager.RunningModsListForReading)
        {
            var selected = PngRuntime.SelectedFiles(mod).ToArray();
            string identity = PreparedTextureRuntime.SelectionIdentity(mod, selected);
            var holderKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in selected)
            {
                if (!holderKeys.Add(PreparationContract.Stem(pair.Key))) continue;
                if (found.Count >= 131072) throw new InvalidDataException("quality-inventory-bound");
                found.Add(new Source { Provider=mod, Selection=identity, Logical=pair.Key, File=pair.Value });
            }
        }
        return found;
    }
    private static void TryApply(Source requested)
    {
        var provider = requested.Provider; string logical=requested.Logical, selection=requested.Selection; var file=requested.File;
        string slot = PreparationContract.Slot(selection, logical);
        try
        {
            // Presence admission avoids catalog/discovery work on ordinary native
            // preparation and on launches without a single converted selection.
            var store = PreparedTextureRuntime.StartupStore;
            if (!store.HasOwner(slot)) return;
            if (!GameBuildContract.Current.HasReviewedTextureConsumers)
                throw new NotSupportedException("quality runtime has not been qualified for this game build");
            if (QualitySettings.activeColorSpace != ColorSpace.Gamma)
                throw new NotSupportedException("quality output requires the qualified Gamma color-space player");
            roles ??= PreparedTextureRoles.Capture();
            if (!roles.Ready || !roles.TryGet(logical, out var role))
                throw new NotSupportedException(roles.Reason(logical));
            if (attempted.Contains(role.Group)) return;
            inventory ??= Sources();
            if (active.Length == 0) active = PreparedTextureRuntime.ActiveIdentity();
            if (active != PreparedTextureRuntime.ActiveIdentity()) throw new InvalidDataException("active providers changed");
            if (helper.Length == 0) helper = PreparationEncoder.Identity(TexturePreparationHelper.ExactPath(PreparedTextureRuntime.ModRoot));
            var stems = new HashSet<string>(role.Members, StringComparer.Ordinal);
            var members = inventory.Where(s => stems.Contains(PreparationContract.Stem(s.Logical))).ToArray();
            if (members.Length == 0 || members.Length > 512) throw new InvalidDataException("quality group coverage bound");
            // A group crossing providers waits for all ordinary holder loads.
            if (members.Any(m => !loaded.ContainsKey(m.Slot) && store.HasOwner(m.Slot))) return;
            if (!attempted.Add(role.Group)) return;
            void Admit()
            {
                if (unsafeReason.Length != 0) throw new InvalidDataException(unsafeReason);
                if (holderGuard?.AllowsOriginalContract() != true) throw new InvalidDataException("texture holder hooks changed");
                foreach (var member in members)
                {
                    string stem = PreparationContract.Stem(member.Logical);
                    if (exposed.Contains(stem) || exposed.Any(p => p.EndsWith("*", StringComparison.Ordinal)
                        && stem.StartsWith(p.Substring(0,p.Length-1), StringComparison.Ordinal)))
                        throw new InvalidDataException("native group already read by a consumer: " + member.Logical);
                    if (!PngRuntime.QualityRouteAllowed(member.File) || !roles!.TryGet(member.Logical, out var currentRole)
                        || currentRole.Identity != role.Identity) throw new InvalidDataException("group source route or consumer changed");
                    if (!TexturePreparationBatch.StillSelected(member.Provider.foldersToLoadDescendingOrder.ToArray(), member.Logical, member.File.FullName))
                        throw new InvalidDataException("source provider changed: " + member.Logical);
                    if (!loaded.TryGetValue(member.Slot, out var native) || ReferenceEquals(native.Native, null)
                        || ReferenceEquals(native.Native, BaseContent.BadTex) || native.Item.extraDisposable != null
                        || !ReferenceEquals(native.Item.contentItem, native.Native)
                        || !ReferenceEquals(member.Provider.GetContentHolder<Texture2D>().contentList,native.Holder)
                        || !native.Holder.TryGetValue(stem, out var held)
                        || !ReferenceEquals(held, native.Native)) throw new InvalidDataException("native group holder changed or incomplete");
                }
            }
            Admit();
            var privateObjects = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            var leases = new List<FileStream>();
            try
            {
                string options = "";
                long bytes = 0;
                var validated = new List<(Source Source, PngCache.Entry Entry)>();
                var records = new List<PreparationRecord>();
                bool bindRoles = false;
                foreach (var member in members)
                {
                    if (!roles.TryGet(member.Logical, out var memberRole) || memberRole.Group != role.Group)
                        throw new InvalidDataException("conflicting quality role");
                    var record = store.ReadQuality(member.Selection, member.Logical)
                        ?? throw new InvalidDataException("missing completed group member: " + member.Provider.Name + ":" + member.Logical);
                    string expectedRole = memberRole.Kind.ToString().ToLowerInvariant();
                    if (record.Active != active || record.Runtime != PreparationContract.RuntimeGog || record.Helper != helper
                        || record.Role != expectedRole || record.Physical != member.File.FullName
                        || record.RoleIdentity != "provisional" && record.RoleIdentity != memberRole.Identity
                        || record.Options.Filter != record.Output.Filter || record.Options.Anisotropy != record.Output.Aniso
                        || record.Options.MipBias != record.Output.Bias)
                        throw new InvalidDataException("source selection, role, runtime, helper or sampling changed: " + member.Logical);
                    if (options.Length == 0) options = record.Options.Identity;
                    if (options != record.Options.Identity) throw new InvalidDataException("paired selections use conflicting quality or sampling");
                    if (!TexturePreparationBatch.StillSelected(member.Provider.foldersToLoadDescendingOrder.ToArray(), member.Logical, member.File.FullName))
                        throw new InvalidDataException("source provider changed: " + member.Logical);
                    var lease = member.File.Open(FileMode.Open, FileAccess.Read, FileShare.Read); leases.Add(lease);
                    byte[] source = PreparedTextureRuntime.ReadSourceSnapshot(lease);
                    if (PreparationContract.Hash(source) != record.SourceDigest) throw new InvalidDataException("source contents changed: " + member.Logical);
                    var header = PreparationImageInspection.Inspect(source, record.Options, expectedRole == "mask", PreparedTextureRuntime.PsdSupport);
                    var entry = ToEntry(record.Output);
                    if (!header.Eligible || !NativeTextureData.Valid(entry) || entry.Width != header.OutputWidth
                        || entry.Height != header.OutputHeight || entry.Mips != header.Mips
                        || entry.Readable || entry.WrapU != 0 || entry.WrapV != 0 || entry.WrapW != 0
                        || (expectedRole == "mask" ? entry.TextureFormat != 4 : entry.TextureFormat != 4 && entry.TextureFormat != 10 && entry.TextureFormat != 12))
                        throw new InvalidDataException("converted representation refused: " + member.Logical);
                    if ((bytes += entry.Pixels.Length) > NativeTextureData.MaximumPixels)
                        throw new InvalidDataException("complete quality group exceeds private construction bound");
                    validated.Add((member, entry));
                    records.Add(record);
                    if (record.RoleIdentity == "provisional")
                    {
                        // External XML hints cannot certify resolved consumer
                        // metadata. Bind only after actual game role validation;
                        // future metadata changes then invalidate this selection.
                        record.RoleIdentity = memberRole.Identity;
                        bindRoles = true;
                    }
                }
                foreach (var pair in role.Pairs)
                {
                    var colors=validated.Where(v=>PreparationContract.Stem(v.Source.Logical)==pair.Color).ToArray();
                    var masks=validated.Where(v=>PreparationContract.Stem(v.Source.Logical)==pair.Mask).ToArray();
                    if(colors.Length==0 || masks.Length==0) throw new InvalidDataException("paired source is missing");
                    void Same(PngCache.Entry color,PngCache.Entry mask)
                    { if(color.Width!=mask.Width || color.Height!=mask.Height) throw new InvalidDataException("color/mask dimensions differ"); }
                    Same(colors[colors.Length-1].Entry,masks[masks.Length-1].Entry);
                    foreach(var color in colors)
                        foreach(var mask in masks.Where(m=>ReferenceEquals(m.Source.Provider,color.Source.Provider))) Same(color.Entry,mask.Entry);
                }
                // Texture construction itself can invoke foreign Unity hooks.
                // Release source locks before any such callback, then recheck
                // every source and resolved consumer after construction.
                foreach (var lease in leases) lease.Dispose();
                leases.Clear();
                foreach (var member in validated)
                {
                    var texture = PngRuntime.RestoreQuality(member.Entry);
                    privateObjects.Add(member.Source.Slot, texture);
                    texture.name = Path.GetFileNameWithoutExtension(member.Source.File.Name);
                    if (!QualityUnityCallbacks.Check()) throw new InvalidDataException("private texture callback may have borrowed output");
                }
                var currentRoles = PreparedTextureRoles.Capture();
                foreach (var member in members)
                {
                    if (!currentRoles.TryGet(member.Logical, out var currentRole) || currentRole.Identity != role.Identity)
                        throw new InvalidDataException("resolved consumer metadata changed during private construction");
                    var lease = member.File.Open(FileMode.Open, FileAccess.Read, FileShare.Read); leases.Add(lease);
                    if (PreparationContract.Hash(PreparedTextureRuntime.ReadSourceSnapshot(lease))
                        != records.Single(r => r.Provider == member.Selection && r.Logical == member.Logical).SourceDigest)
                        throw new InvalidDataException("source changed during private construction");
                }
                Admit();
                if (bindRoles && !store.PublishQualityGroup(records, default))
                    throw new IOException("cannot bind complete external group to current game roles");
                // No Unity/native/foreign callback executes during these plain
                // managed assignments. The whole group becomes visible together.
                foreach (var member in members)
                {
                    var native = loaded[member.Slot]; var texture = privateObjects[member.Slot];
                    native.Holder[PreparationContract.Stem(member.Logical)] = texture;
                    native.Item.contentItem = texture;
                }
                privateObjects.Clear(); Groups++; Applied += members.Length;
                foreach (var lease in leases) lease.Dispose();
                leases.Clear();
                Status = "Explicit quality group applied after native callbacks: " + members.Length + " provider textures";
                foreach (var member in members)
                {
                    if (!QualityUnityCallbacks.Check()) { Status += "; obsolete native cleanup deferred after callback change"; break; }
                    try { Object.DestroyImmediate(loaded[member.Slot].Native); }
                    catch { Status += "; obsolete native cleanup incomplete"; break; }
                }
            }
            finally
            {
                foreach (var lease in leases) lease.Dispose();
                // Once a callback epoch changes, even earlier private objects
                // may have escaped. Preserve them and retain all native holders.
                foreach (var texture in privateObjects.Values)
                {
                    if (!QualityUnityCallbacks.Check()) break;
                    try { Object.DestroyImmediate(texture); } catch { break; }
                }
            }
        }
        catch (Exception e)
        {
            Refused++; Status = "Native output retained for " + provider.Name + ":" + logical + " — " + e.Message;
            JsonLineLog.WriteEvent(Path.Combine(PreparedTextureRuntime.SaveRoot, "WakeUp", "quality.jsonl"), "refused",
                "\"source\":\"" + JsonLineLog.Escape(file.FullName) + "\",\"reason\":\"" + JsonLineLog.Escape(e.Message) + "\"");
            return;
        }
    }
    internal static void Finish()
    {
        loaded.Clear(); exposed.Clear(); attempted.Clear(); inventory=null; roles=null; active=helper=unsafeReason="";
        JsonLineLog.WriteEvent(Path.Combine(PreparedTextureRuntime.SaveRoot, "WakeUp", "quality.jsonl"), "complete",
            "\"applied\":" + Applied + ",\"groups\":" + Groups + ",\"refused\":" + Refused);
    }
}
