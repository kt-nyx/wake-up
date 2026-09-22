// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using Verse;

namespace WakeUp;

// Bounded candidate kernel. Its caller must admit effective native methods,
// source/operation order and exclusive access before allowing a stored replay.
// Publication is outside the fallback catch: partially changed input is never
// passed to ordinary workers as recovery.
internal static class XmlRegionReplay
{
    internal const int StoreBytes = 16 * 1024 * 1024;
    private const int MaxEdits = 4096;
    private static readonly Action<PatchOperation, bool> SetNever = CreateStateSetter();
    internal sealed class Result
    {
        internal bool Hit, Captured;
        internal string Reason = "ordinary";
        internal bool[] Outcomes = Array.Empty<bool>();
    }

    internal static Result Execute(XmlDocument document, IReadOnlyList<PatchOperation> operations,
        OwnedCacheStore store, Func<PatchOperation, XmlDocument, bool> native,
        Func<Delegate, bool>? ownedObserver = null)
    {
        if (!TryBegin(document, operations, store, ownedObserver, null, out var session, out string reason))
        {
            var ordinary = new Result { Reason = reason, Outcomes = new bool[operations.Count] };
            for (int i = 0; i < operations.Count; i++) ordinary.Outcomes[i] = native(operations[i], document);
            return ordinary;
        }
        using (session)
            foreach (var operation in operations) session!.ApplyOne(operation, document, native);
        return session!.Result;
    }

    internal static bool TryBegin(XmlDocument document, IReadOnlyList<PatchOperation> operations,
        OwnedCacheStore store, Func<Delegate, bool>? ownedObserver, Func<XmlElement, string>? sourceIdentity,
        out Session? session, out string reason)
    {
        session = null; reason = "unsupported-interval";
        try
        {
            // Cold capture must obey the same observer rule as a warm hit;
            // otherwise a foreign listener could poison a later stored result.
            if (!RetainedXmlPublication.TryPrepare(document, Array.Empty<RetainedXmlPublication.Edit>(), ownedObserver, out _, out reason)) return false;
            if (!XmlRegionOperations.TryCreate(operations, 0, out var region) || region!.Tops.Length != operations.Count)
                throw new InvalidDataException("unsupported-interval");
            if (!RetainedXmlStaging.TryAdmit(document, region.ImportedTemplates(), ownedObserver, out reason)) return false;
            using var identity = new MemoryStream();
            using var writer = new BinaryWriter(identity, Encoding.UTF8, true);
            writer.Write(region.Identity.Length); writer.Write(region.Identity);
            XmlElement[] roots = region.Bind(document, writer);
            if (sourceIdentity != null) foreach (var root in roots) writer.Write(sourceIdentity(root));
            string key = BitConverter.ToString(CacheDigest.Hash(identity.ToArray())).Replace("-", "");
            var capture = new Capture(document, roots, region);
            // The store owns payload validation too, so a malformed journal is
            // removed and the same ordinary fallback can capture a replacement.
            var restored = store.Read(key, payload => capture.Prepare(payload, ownedObserver));
            if (restored == null) capture.DiscardStagedNodes();
            session = new Session(document, region, store, key, capture, restored);
            reason = restored == null ? store.LastReason : "hit";
            return true;
        }
        catch (Exception exception)
        {
            reason = exception is InvalidDataException ? exception.Message : exception.GetType().Name;
            return false;
        }
    }

    // Both CPU tests and the actual rewritten native callsite consume this
    // session one original operation at a time. Cold exceptions still escape
    // into the game's original per-operation catch, with no eager later calls.
    internal sealed class Session : IDisposable
    {
        private readonly XmlDocument document;
        private readonly XmlRegionOperations region;
        private readonly OwnedCacheStore store;
        private readonly string key;
        private readonly Capture capture;
        private readonly Restored? restored;
        private int next;
        private bool started, disposed;
        internal Result Result { get; }
        internal bool Complete => next == region.Tops.Length;
        internal bool Matches(PatchOperation operation) => !Complete && ReferenceEquals(region.Tops[next], operation);

        internal Session(XmlDocument document, XmlRegionOperations region, OwnedCacheStore store, string key, Capture capture, Restored? restored)
        {
            this.document = document; this.region = region; this.store = store; this.key = key; this.capture = capture; this.restored = restored;
            Result = new Result { Outcomes = restored?.Outcomes ?? new bool[region.Tops.Length], Reason = restored == null ? store.LastReason : "hit" };
        }
        internal bool ApplyOne(PatchOperation operation, XmlDocument xml, Func<PatchOperation, XmlDocument, bool> native)
        {
            if (disposed || !ReferenceEquals(xml, document) || !Matches(operation)) throw new InvalidOperationException("region-call-order");
            if (!started)
            {
                started = true;
                if (restored != null)
                {
                    // No fallthrough catch surrounds publication. No native
                    // worker ever runs over a partially published cached result.
                    restored.Publication.Commit();
                    for (int i = 0; i < restored.State.Length; i++) SetNever(region.Objects[i], restored.State[i]);
                    Result.Hit = true;
                }
                else capture.Begin();
            }
            bool success;
            if (restored != null) success = Result.Outcomes[next];
            else
            {
                try { success = native(operation, xml); }
                catch { Dispose(); throw; }
                Result.Outcomes[next] = success;
            }
            next++;
            if (Complete)
            {
                capture.End();
                if (restored == null)
                {
                    try { Result.Captured = store.Publish(key, capture.Encode(Result.Outcomes)); }
                    catch (Exception exception) { Result.Reason = exception.GetType().Name; }
                }
            }
            return success;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; capture.End();
        }
    }

    internal sealed class Restored
    {
        internal readonly RetainedXmlPublication.Prepared Publication;
        internal readonly bool[] State, Outcomes;
        internal Restored(RetainedXmlPublication.Prepared publication, bool[] state, bool[] outcomes)
        { Publication = publication; State = state; Outcomes = outcomes; }
    }

    private static Action<PatchOperation, bool> CreateStateSetter()
    {
        var field = typeof(PatchOperation).GetField("neverSucceeded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var method = new DynamicMethod("WakeUpRegionState", typeof(void), new[] { typeof(PatchOperation), typeof(bool) }, typeof(XmlRegionReplay), true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld, field); il.Emit(OpCodes.Ret);
        var setter = (Action<PatchOperation, bool>)method.CreateDelegate(typeof(Action<PatchOperation, bool>));
        RuntimeHelpers.PrepareDelegate(setter); return setter;
    }

    internal sealed class Capture
    {
        private readonly XmlDocument document;
        private readonly XmlRegionOperations operations;
        private readonly HashSet<XmlElement> roots;
        private readonly List<XmlElement> nodes = new();
        private readonly Dictionary<XmlElement, int> indices = new();
        private readonly List<XmlElement> templates = new();
        private readonly List<byte[]> templateIdentities = new();
        private readonly List<int[]> edits = new();
        private readonly int originalNodes;
        private bool invalid;

        internal Capture(XmlDocument document, XmlElement[] roots, XmlRegionOperations operations)
        {
            this.document = document; this.operations = operations; this.roots = new HashSet<XmlElement>(roots);
            foreach (var root in roots) Register(root);
            originalNodes = nodes.Count;
            // Native AddModExtension has one generated empty element; all other
            // inserted contents come directly from the current operation values.
            var privateDocument = new XmlDocument();
            AddTemplate(privateDocument.CreateElement("modExtensions"));
            foreach (var operation in operations.Objects)
            {
                var field = operation.GetType().GetField("value", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                var value = (XmlContainer)field.GetValue(operation);
                foreach (XmlElement child in value.node.ChildNodes) AddTemplate(child);
            }
        }

        private void AddTemplate(XmlElement element) { templates.Add(element); templateIdentities.Add(Identity(element)); }
        internal void DiscardStagedNodes()
        {
            for (int i = originalNodes; i < nodes.Count; i++) indices.Remove(nodes[i]);
            if (nodes.Count > originalNodes) nodes.RemoveRange(originalNodes, nodes.Count - originalNodes);
        }
        private static byte[] Identity(XmlNode node)
        { using var output = new MemoryStream(); using var writer = new BinaryWriter(output); int count = 0; XmlRegionOperations.WriteNode(writer, node, ref count); return output.ToArray(); }
        private void Register(XmlElement node)
        {
            if (nodes.Count >= XmlRegionOperations.MaxNodes) throw new InvalidDataException("region-node-bound");
            indices.Add(node, nodes.Count); nodes.Add(node);
            foreach (XmlNode child in node.ChildNodes) if (child is XmlElement element) Register(element);
        }
        private bool InRegion(XmlNode? node)
        {
            for (; node != null; node = node.ParentNode) if (node is XmlElement element && roots.Contains(element)) return true;
            return false;
        }
        internal void Begin()
        { document.NodeInserted += Inserted; document.NodeRemoving += Removing; document.NodeChanging += Changing; }
        internal void End()
        { document.NodeInserted -= Inserted; document.NodeRemoving -= Removing; document.NodeChanging -= Changing; }
        private void Changing(object sender, XmlNodeChangedEventArgs args) { if (InRegion(args.Node)) invalid = true; }
        private void Inserted(object sender, XmlNodeChangedEventArgs args)
        {
            if (invalid || !InRegion(args.NewParent)) return;
            try
            {
                if (args.Node is not XmlElement child || args.NewParent is not XmlElement parent
                    || indices.ContainsKey(child)) throw new InvalidDataException("region-insert");
                byte[] identity = Identity(child);
                int template = templateIdentities.FindIndex(value => value.SequenceEqual(identity));
                if (template < 0) throw new InvalidDataException("region-insert-template");
                var reference = child.NextSibling;
                int id = nodes.Count; Register(child);
                Add(new[] { 1, indices[parent], id, reference == null ? -1 : indices[(XmlElement)reference], template });
            }
            catch { invalid = true; }
        }
        private void Removing(object sender, XmlNodeChangedEventArgs args)
        {
            if (invalid || !InRegion(args.OldParent)) return;
            try
            {
                if (args.Node is not XmlElement child || args.OldParent is not XmlElement parent) throw new InvalidDataException("region-remove");
                Add(new[] { 0, indices[parent], indices[child], -1, -1 });
            }
            catch { invalid = true; }
        }
        private void Add(int[] edit)
        { if (edits.Count >= MaxEdits) throw new InvalidDataException("region-edit-bound"); edits.Add(edit); }

        internal byte[] Encode(bool[] outcomes)
        {
            if (invalid) throw new InvalidDataException("region-capture-refused");
            using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
            writer.Write(1); writer.Write(edits.Count);
            foreach (int[] edit in edits) foreach (int value in edit) writer.Write(value);
            var state = operations.CaptureState();
            writer.Write(state.Length); foreach (bool value in state) writer.Write(value);
            writer.Write(outcomes.Length); foreach (bool value in outcomes) writer.Write(value);
            if (output.Length > XmlRegionOperations.MaxBytes) throw new InvalidDataException("region-payload-bound");
            return output.ToArray();
        }

        internal Restored Prepare(byte[] payload, Func<Delegate, bool>? ownedObserver)
        {
            // Validate the entire byte layout and IDs before allocating any
            // node into the live document. No cached arbitrary XML is parsed.
            using var input = new MemoryStream(payload, false); using var reader = new BinaryReader(input);
            if (reader.ReadInt32() != 1) throw new InvalidDataException("region-payload-version");
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxEdits || payload.Length < 16 + count * 20) throw new InvalidDataException("region-edit-count");
            var records = new int[count][];
            int available = nodes.Count;
            for (int i = 0; i < count; i++)
            {
                int[] row = records[i] = new[] { reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32() };
                if (row[0] < 0 || row[0] > 1 || row[1] < 0 || row[1] >= available) throw new InvalidDataException("region-edit-parent");
                if (row[0] == 1)
                {
                    if (row[2] != available || row[3] < -1 || row[3] >= available || row[4] < 0 || row[4] >= templates.Count)
                        throw new InvalidDataException("region-edit-insert");
                    available += CountElements(templates[row[4]]);
                }
                else if (row[2] < 0 || row[2] >= available || row[3] != -1 || row[4] != -1)
                    throw new InvalidDataException("region-edit-remove");
                if (available > XmlRegionOperations.MaxNodes) throw new InvalidDataException("region-node-bound");
            }
            bool[] state = ReadStates(reader, operations.ObjectCount);
            bool[] outcomes = ReadStates(reader, operations.Tops.Length);
            if (input.Position != input.Length) throw new InvalidDataException("region-payload-tail");
            // A no-edit preparation performs event/type refusal before ImportNode
            // can produce detached-child insertion events in this document.
            if (!RetainedXmlStaging.TryAdmit(document, records.Where(row => row[0] == 1).Select(row => templates[row[4]]).ToArray(), ownedObserver, out string reason))
                throw new InvalidDataException(reason);
            var ready = new List<RetainedXmlPublication.Edit>();
            foreach (int[] row in records)
            {
                if (row[0] == 1)
                {
                    var child = (XmlElement)document.ImportNode(templates[row[4]], true);
                    Register(child);
                    ready.Add(RetainedXmlPublication.Edit.Insert(nodes[row[1]], child, row[3] < 0 ? null : nodes[row[3]]));
                }
                else ready.Add(RetainedXmlPublication.Edit.Remove(nodes[row[1]], nodes[row[2]]));
            }
            if (!RetainedXmlPublication.TryPrepare(document, ready, ownedObserver, out var prepared, out reason))
                throw new InvalidDataException(reason);
            return new Restored(prepared!, state, outcomes);
        }
        private static int CountElements(XmlElement node)
        { int count = 1; foreach (XmlNode child in node.ChildNodes) if (child is XmlElement element) count += CountElements(element); return count; }
        private static bool[] ReadStates(BinaryReader reader, int count)
        {
            if (reader.ReadInt32() != count) throw new InvalidDataException("region-state-count");
            var state = new bool[count];
            for (int i = 0; i < count; i++) { byte value = reader.ReadByte(); if (value > 1) throw new InvalidDataException("region-state-value"); state[i] = value == 1; }
            return state;
        }
    }
}
