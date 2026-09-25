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
                if (!acknowledged.Contains(key)) pending[key] = new CompatibilityNotice(key, item.Value,
                    displayProvider: (string?)item.Attribute("mod") ?? "", displayCode: (string?)item.Attribute("noticeCode") ?? "");
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
    internal void Set(string id, OperationState state, string reason, string code = "required-contract", string provider = "Wake-Up", string displayProvider = "", string displayCode = "")
    {
        lock (gate)
        {
            if (!operations.TryGetValue(id, out var operation) || !operation.Requested) return;
            bool same = operation.State == state && operation.Reason == reason && operation.Provider == provider && operation.ReasonCode == code;
            string key = id + "|" + code + "|" + provider;
            if (same && (!pending.TryGetValue(key, out var prior) || prior.Current && prior.DisplayProvider == displayProvider && prior.DisplayCode == displayCode)) return;
            operation.State = state; operation.Reason = reason; operation.Provider = provider; operation.ReasonCode = code;
            if (!same) log(operation.Describe());
            int separator = id.IndexOf('/');
            if (separator > 0) SummarizeChildren(id.Substring(0, separator));
            foreach (var old in pending.Values.Where(n => n.Id == id && n.Current).ToArray()) pending[old.Key] = old.Historical();
            if (state != OperationState.Unavailable && state != OperationState.PartiallyAvailable) return;
            if (acknowledged.Contains(key)) return;
            pending[key] = new CompatibilityNotice(key, reason, true, displayProvider, displayCode);
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
            if (acknowledged.Contains(key)) return;
            pending[key] = new CompatibilityNotice(key, text, true);
            Save(acknowledged, pending);
        }
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
    internal CompatibilityNotice[] Pending()
    {
        lock (gate) return pending.Values.Select(n => !n.Current && operations.TryGetValue(n.Id, out var operation)
            ? n.Historical(operation.State) : n).ToArray();
    }
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
    private bool Save(HashSet<string> acknowledgements, Dictionary<string, CompatibilityNotice> notices)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            string temporary = file + ".tmp";
            var root = new XElement("compatibility", new XAttribute("version", "1"),
                acknowledgements.Select(k => new XElement("ack", new XAttribute("key", k))),
                notices.Values.Select(n => new XElement("pending", new XAttribute("key", n.Key),
                    new XAttribute("mod", n.DisplayProvider), new XAttribute("noticeCode", n.DisplayCode), n.Details)));
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
