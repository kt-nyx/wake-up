// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace WakeUp;

internal enum OperationState { Off, NotApplicable, AwaitingStage, Available, PartiallyAvailable, Unavailable }
internal sealed class OperationStatus
{
    internal readonly string Id, Name;
    internal bool Requested;
    internal OperationState State;
    internal string Provider = "Wake-Up", Reason = "Not selected", ReasonCode = "";
    internal OperationStatus(string id, string name, bool requested)
    { Id = id; Name = name; Requested = requested; State = requested ? OperationState.AwaitingStage : OperationState.Off; Reason = requested ? "Awaiting compatibility checks" : "Not selected"; }
    internal string Describe() => Name + ": " + State + " (requested: " + Requested + "; provider: " + Provider + "). " + Reason;
}
internal sealed class CompatibilityNotice
{
    internal readonly string Key, Text;
    internal CompatibilityNotice(string key, string text) { Key = key; Text = text; }
}

// No game APIs: can be initialized before the first Mod constructor. A receipt
// records a semantic decision, never a binary hash or arbitrary exception text.
internal sealed class OperationRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, OperationStatus> operations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CompatibilityNotice> pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> acknowledged = new(StringComparer.Ordinal);
    private readonly string file;
    private readonly Action<string> log;
    private bool persistenceError;
    internal const int HistoryLimit = 4096;
    internal OperationRegistry(string file, Action<string> log)
    {
        this.file = file; this.log = log;
        try
        {
            if (!File.Exists(file)) return;
            if (new FileInfo(file).Length > 4 * 1024 * 1024) throw new InvalidDataException("Oversized compatibility ledger");
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            XElement root = XElement.Load(reader);
            if (root.Name != "compatibility" || (string?)root.Attribute("version") != "1") throw new InvalidDataException("Unsupported compatibility ledger");
            foreach (XElement item in root.Elements("ack").Take(HistoryLimit)) acknowledged.Add((string)item.Attribute("key")!);
            foreach (XElement item in root.Elements("pending").Take(HistoryLimit))
            {
                string key = (string)item.Attribute("key")!;
                if (!acknowledged.Contains(key)) pending[key] = new CompatibilityNotice(key, item.Value);
            }
        }
        catch (Exception error) { log("Compatibility history could not be read: " + error.GetType().Name); }
    }
    internal void Request(string id, string name, bool requested)
    {
        lock (gate)
        {
            // Early setup and the Mod constructor may both declare the same intent.
            if (operations.TryGetValue(id, out var existing))
            {
                if (requested && !existing.Requested) { existing.Requested = true; existing.State = OperationState.AwaitingStage; existing.Reason = "Awaiting compatibility checks"; }
                return;
            }
            operations.Add(id, new OperationStatus(id, name, requested));
        }
    }
    internal void Set(string id, OperationState state, string reason, string code = "required-contract", string provider = "Wake-Up")
    {
        lock (gate)
        {
            if (!operations.TryGetValue(id, out var operation) || !operation.Requested) return;
            if (operation.State == state && operation.Reason == reason && operation.Provider == provider && operation.ReasonCode == code) return;
            operation.State = state; operation.Reason = reason; operation.Provider = provider; operation.ReasonCode = code;
            log(operation.Describe());
            int separator = id.IndexOf('/');
            if (separator > 0) SummarizeChildren(id.Substring(0, separator));
            if (state != OperationState.Unavailable && state != OperationState.PartiallyAvailable) return;
            string key = id + "|" + code + "|" + provider;
            if (acknowledged.Contains(key) || pending.ContainsKey(key)) return;
            pending.Add(key, new CompatibilityNotice(key, NoticeText(operation, reason, provider)));
            Save(acknowledged, pending); // Persist before any UI opportunity, including worker-thread failures.
        }
    }
    // Supplier advisories are not disabled Wake-Up operations. They share only
    // the persistent delivery/acknowledgement mechanism.
    internal void Advise(string code, string provider, string text)
    {
        lock (gate)
        {
            string key = "advisory|" + code + "|" + provider;
            log(text);
            if (acknowledged.Contains(key) || pending.ContainsKey(key)) return;
            pending.Add(key, new CompatibilityNotice(key, text));
            Save(acknowledged, pending);
        }
    }
    private static string NoticeText(OperationStatus operation, string reason, string provider)
    {
        string workName = operation.Id.StartsWith("definitions/", StringComparison.Ordinal)
            ? operation.Id.Substring("definitions/".Length) switch {
                "PatchOperationAdd" => "adding XML entries", "PatchOperationAddModExtension" => "adding mod extensions to XML entries",
                "PatchOperationInsert" => "inserting XML entries", "PatchOperationRemove" => "removing XML entries",
                "PatchOperationReplace" => "replacing XML entries", "PatchOperationSetName" => "renaming XML entries",
                "PatchOperationAttributeAdd" => "adding XML attributes", "PatchOperationAttributeRemove" => "removing XML attributes",
                "PatchOperationAttributeSet" => "changing XML attributes", "PatchOperationTest" => "checking whether an XML entry exists",
                "PatchOperationConditional" => "choosing an XML patch action based on a condition", _ => "XML patch work" }
            : operation.Id.StartsWith("reflection/", StringComparison.Ordinal) ? "finding code fields and methods during loading"
            : operation.Id == "type-name" ? "finding code types by name"
            : operation.Id == "translations" ? "applying translated text"
            : operation.Id.StartsWith("asset-routing", StringComparison.Ordinal) ? "finding images and other game resources"
            : operation.Name.ToLowerInvariant();
        if (provider == "WOWGAG")
        {
            workName = operation.Id switch {
                "observation/content" => "recording content reload calls", "texture-loader" => "its image loading improvement",
                "texture-cache" => "caching loaded images", "streaming-xml" => "reading XML in smaller portions",
                "processed-xml" => "reusing completed XML patches", "definitions" => "its XML patch searches",
                "single-query" => "reusing individual XML search results", "query-plans" => "reusing XML search expressions",
                "xml-timings/files" => "recording XML file-reading times", _ => workName };
            if (operation.ReasonCode.Contains("unqualified"))
                return "Wake-Up has disabled " + workName + " because it could not verify this part of WOWGAG.";
            if (operation.ReasonCode.Contains("action-required"))
                return "To use Wake-Up for " + workName + ", turn off WOWGAG's "
                    + (operation.ReasonCode.StartsWith("supplier-xml", StringComparison.Ordinal) ? "XML reuse" : "content preload") + " option and restart.";
            return "Wake-Up has disabled " + workName + " while WOWGAG handles "
                + (operation.ReasonCode.StartsWith("supplier-xml", StringComparison.Ordinal) ? "XML loading" : "content preloading") + ".";
        }
        if (operation.Id == "processed-xml" && reason.Contains("WakeUp.StreamingXmlRuntime"))
            return "Completed XML patch reuse cannot run together with Wake-Up's streaming XML input in this setup. Streaming input stays enabled; XML patches follow their existing loading path. This is a limit between two Wake-Up options.";
        if (provider == "Image Opt" || provider == "Graphics Settings+")
            return Bound(reason.Split(new[] { " Technical detail:" }, StringSplitOptions.None)[0]);
        if ((operation.Id == "texture-loader" || operation.Id == "texture-cache") && reason.Contains("hook-"))
            return (operation.Id == "texture-loader" ? "Wake-Up's image loading improvement" : "Wake-Up's saved image cache")
                + " is disabled because another loading callback changes the image-loading path. Images keep loading through the existing path.";
        if (operation.Id == "processed-xml" && reason.Contains("foreign-xml-hook"))
            return "Wake-Up cannot reuse completed XML patches because the patch-loading path has callbacks that this cache does not support. Mod patches still run through the existing loading path.";
        if (operation.Id == "single-query" && reason.Contains("Required XML query"))
            return "Wake-Up cannot reuse individual XML search results with the current search methods. Existing XML searches remain in use.";
        if (operation.Id == "display-scheduling" && operation.State == OperationState.PartiallyAvailable)
            return "Some startup work must run in its existing order. Wake-Up's loading display still runs, but it cannot split that work into smaller steps.";
        if (provider == "Faster Game Loading")
        {
            string work = operation.Id == "type-name" ? "searches for code types by name"
                : operation.Id == "leaf" ? "searches for the most specific code subtypes"
                : operation.Id == "definitions/PatchOperationTest" ? "checks for whether an XML entry exists"
                : operation.Id == "definitions/PatchOperationConditional" ? "XML patches that choose an action based on a condition"
                : operation.Id == "single-query" ? "individual XML searches" : operation.Name.ToLowerInvariant();
            return "Wake-Up leaves " + work + " with Faster Game Loading because it changes this loading path.";
        }
        if (provider == "YaOpt")
            return "Wake-Up leaves " + workName + " with YaOpt because it changes this loading path.";
        if (reason.Contains("foreign-") || reason.Contains("changed-") || reason.Contains("hook-") || reason.Contains("consumer changed"))
            return "Wake-Up leaves " + workName + " on the existing loading path because a required method has changed or another mod controls it. Technical details are available in Wake-Up settings.";
        // Keep exact diagnostics in status details/logs, while the popup explains the decision.
        return operation.Name + ": " + Bound(reason.Replace("Ordinary behavior retained. ", "")) + " Provider: " + provider + ".";
    }
    internal void RefuseChildren(string parent, string reason)
    {
        lock (gate) foreach (var operation in operations.Values.ToArray())
            if (operation.Id.StartsWith(parent + "/", StringComparison.Ordinal)) Set(operation.Id, OperationState.Unavailable, reason);
    }
    internal void SummarizeChildren(string parent)
    {
        lock (gate)
        {
            var children = operations.Values.Where(o => o.Id.StartsWith(parent + "/", StringComparison.Ordinal)).ToArray();
            if (children.Length == 0 || !operations.TryGetValue(parent, out var group)) return;
            int available = children.Count(o => o.State == OperationState.Available);
            OperationState previous = group.State;
            group.State = available == children.Length ? OperationState.Available : available == 0 ? OperationState.Unavailable : OperationState.PartiallyAvailable;
            group.Reason = available + " of " + children.Length + " operations available; individual decisions are listed separately.";
            if (group.State != previous) log(group.Describe());
        }
    }
    internal void InterruptSetup(string reason, IEnumerable<string>? ids = null)
    {
        lock (gate)
            foreach (var operation in operations.Values.ToArray())
                if (operation.Requested && operation.State == OperationState.AwaitingStage && (ids == null || ids.Contains(operation.Id)))
                    Set(operation.Id, OperationState.Unavailable, reason, "setup-interrupted");
    }
    internal bool HasRequested { get { lock (gate) return operations.Values.Any(o => o.Requested); } }
    internal bool HasPending { get { lock (gate) return pending.Count != 0; } }
    internal void FinishSetup()
    {
        lock (gate) foreach (OperationStatus operation in operations.Values) log(operation.Describe());
    }
    internal string[] Snapshot() { lock (gate) return operations.Values.Select(o => o.Describe()).ToArray(); }
    internal CompatibilityNotice[] Pending() { lock (gate) return pending.Values.ToArray(); }
    internal bool Acknowledge(IEnumerable<string> displayedKeys)
    {
        lock (gate)
        {
            var next = new HashSet<string>(acknowledged, StringComparer.Ordinal);
            var remaining = new Dictionary<string, CompatibilityNotice>(pending, StringComparer.Ordinal);
            foreach (string key in displayedKeys) if (remaining.Remove(key)) next.Add(key);
            // Finite history: very old decisions can be shown again after eviction.
            while (next.Count > HistoryLimit) next.Remove(next.First());
            if (!Save(next, remaining)) return false;
            acknowledged.Clear(); acknowledged.UnionWith(next);
            pending.Clear(); foreach (var pair in remaining) pending.Add(pair.Key, pair.Value);
            return true;
        }
    }
    private static string Bound(string text) => text.Length <= 600 ? text : text.Substring(0, 600) + " (full detail in log)";
    private bool Save(HashSet<string> acknowledgements, Dictionary<string, CompatibilityNotice> notices)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            string temporary = file + ".tmp";
            var root = new XElement("compatibility", new XAttribute("version", "1"),
                acknowledgements.Select(k => new XElement("ack", new XAttribute("key", k))),
                notices.Values.Select(n => new XElement("pending", new XAttribute("key", n.Key), n.Text)));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { root.Save(stream); stream.Flush(true); }
            if (File.Exists(file)) File.Replace(temporary, file, null); else File.Move(temporary, file);
            return true;
        }
        catch (Exception error)
        {
            if (!persistenceError) { persistenceError = true; log("Compatibility history could not be saved; notices remain pending: " + error.GetType().Name + " (0x" + error.HResult.ToString("X8") + "): " + error.Message); }
            return false;
        }
    }
}
