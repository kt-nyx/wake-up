// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Nonshipping R1 experiment. Compiled only by the offline test project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Verse;

namespace WakeUp;

// Describes only the initial uninterrupted native patch trees. Unknown code is
// never walked or invoked. Method/hook admission and external FindMod inputs
// belong to the caller's runtime contract, not this instance-state descriptor.
internal sealed class XmlReuseOperations
{
    private const int MaxOperations = 32768, MaxDepth = 64, MaxIdentity = 16 * 1024 * 1024;
    private const int StateMagic = 0x31534F58;
    private readonly Entry[] entries;
    private readonly int[] topIndices;
    private readonly byte[] identityHash;
    private readonly Action<PatchOperation, bool> setNeverSucceeded;
    private readonly Action<PatchOperationSequence, PatchOperation?> setLastFailed;
    internal int PrefixCount => topIndices.Length;
    internal int OperationCount => entries.Length;
    internal byte[] Identity { get; }
    internal bool CompletedSuccessfully => topIndices.All(i => !(bool)entries[i].NeverField.GetValue(entries[i].Operation));

    private sealed class Entry
    {
        internal readonly PatchOperation Operation;
        internal readonly FieldInfo NeverField;
        internal readonly FieldInfo? FailedField;
        internal readonly int[] Children;
        internal Entry(PatchOperation operation, FieldInfo never, FieldInfo? failed, int[] children)
        { Operation = operation; NeverField = never; FailedField = failed; Children = children; }
    }

    private XmlReuseOperations(Entry[] entries, int[] tops, byte[] identity)
    {
        this.entries = entries; topIndices = tops; Identity = identity;
        using (var hash = SHA256.Create()) identityHash = hash.ComputeHash(identity);
        setNeverSucceeded = MakeSetter<PatchOperation, bool>(entries[0].NeverField);
        FieldInfo failed = typeof(PatchOperationSequence).GetField("lastFailedOperation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        setLastFailed = MakeSetter<PatchOperationSequence, PatchOperation?>(failed);
    }

    internal static bool TryCreate(IReadOnlyList<PatchOperation> tops, out XmlReuseOperations? result, bool allowFindMod = true)
    {
        result = null;
        try
        {
            var builder = new Builder { AllowFindMod = allowFindMod };
            using var output = new IdentityWriter();
            output.Int(1); output.Int(0);
            var admitted = new List<int>();
            for (int i = 0; i < tops.Count; i++)
            {
                long mark = output.Length;
                int count = builder.Entries.Count;
                try
                {
                    int root = builder.Operation(tops[i], output, 0);
                    // Reserve the initial mutable-state records before accepting
                    // this entire top-level tree into the prefix.
                    output.Require(builder.Entries.Count * 5 + 4);
                    for (int j = count; j < builder.Entries.Count; j++) builder.FailureIndex(j);
                    admitted.Add(root);
                }
                catch
                {
                    output.Truncate(mark);
                    builder.Entries.RemoveRange(count, builder.Entries.Count - count);
                    break;
                }
            }
            if (admitted.Count == 0) return false;
            output.SetIntAt(4, admitted.Count);
            output.Int(builder.Entries.Count);
            // Mutable fields are separate from structural identity, but their
            // initial values are bound too: a pre-altered instance is not a hit.
            for (int i = 0; i < builder.Entries.Count; i++)
            {
                Entry entry = builder.Entries[i];
                output.Byte((bool)entry.NeverField.GetValue(entry.Operation) ? (byte)1 : (byte)0);
                output.Int(builder.FailureIndex(i));
            }
            result = new XmlReuseOperations(builder.Entries.ToArray(), admitted.ToArray(), output.ToArray());
            return true;
        }
        catch { return false; }
    }

    internal byte[] CaptureState()
    {
        using var stream = new MemoryStream(40 + entries.Length * 5);
        using var writer = new BinaryWriter(stream);
        writer.Write(StateMagic); writer.Write(entries.Length); writer.Write(identityHash);
        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            writer.Write((bool)entry.NeverField.GetValue(entry.Operation) ? (byte)1 : (byte)0);
            writer.Write(FailureIndex(entry, entries));
        }
        return stream.ToArray();
    }

    internal Action PrepareRestore(byte[] state)
    {
        if (state == null || state.Length != 40 + entries.Length * 5) throw new InvalidDataException("xml-operation-state-length");
        using var stream = new MemoryStream(state, false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadInt32() != StateMagic || reader.ReadInt32() != entries.Length
            || !reader.ReadBytes(32).SequenceEqual(identityHash)) throw new InvalidDataException("xml-operation-state-identity");
        var never = new bool[entries.Length];
        var failed = new PatchOperation?[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            byte value = reader.ReadByte();
            if (value > 1) throw new InvalidDataException("xml-operation-state-boolean");
            never[i] = value != 0;
            int failure = reader.ReadInt32();
            Entry entry = entries[i];
            if (entry.FailedField == null)
            {
                if (failure != -2) throw new InvalidDataException("xml-operation-state-nonsequence");
            }
            else
            {
                if (failure < -1 || failure >= entries.Length || failure >= 0 && !entry.Children.Contains(failure))
                    throw new InvalidDataException("xml-operation-state-child");
                failed[i] = failure == -1 ? null : entries[failure].Operation;
            }
        }
        // All targets, values and typed field setters exist before this action
        // is returned. Its commit has no reflection, allocation or user calls.
        Action commit = () =>
        {
            for (int i = 0; i < entries.Length; i++)
            {
                setNeverSucceeded(entries[i].Operation, never[i]);
                if (entries[i].FailedField != null)
                    setLastFailed((PatchOperationSequence)entries[i].Operation, failed[i]);
            }
        };
        RuntimeHelpers.PrepareDelegate(commit);
        return commit;
    }

    private static Action<TTarget, TValue> MakeSetter<TTarget, TValue>(FieldInfo field)
    {
        if (field == null || field.IsInitOnly || field.FieldType != typeof(TValue)) throw new InvalidDataException("xml-operation-state-field");
        var method = new DynamicMethod("WakeUpXmlState_" + field.Name, typeof(void), new[] { typeof(TTarget), typeof(TValue) }, typeof(XmlReuseOperations), true);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld, field); il.Emit(OpCodes.Ret);
        var setter = (Action<TTarget, TValue>)method.CreateDelegate(typeof(Action<TTarget, TValue>));
        RuntimeHelpers.PrepareDelegate(setter);
        return setter;
    }

    private static int FailureIndex(Entry entry, IReadOnlyList<Entry> entries)
    {
        if (entry.FailedField == null) return -2;
        object? failed = entry.FailedField.GetValue(entry.Operation);
        if (failed == null) return -1;
        foreach (int child in entry.Children)
            if (ReferenceEquals(entries[child].Operation, failed)) return child;
        throw new InvalidDataException("xml-operation-foreign-failed-child");
    }

    private sealed class ReferenceComparer : IEqualityComparer<PatchOperation>
    {
        public bool Equals(PatchOperation? x, PatchOperation? y) => ReferenceEquals(x, y);
        public int GetHashCode(PatchOperation obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private sealed class Builder
    {
        internal bool AllowFindMod;
        internal readonly List<Entry> Entries = new();
        private readonly HashSet<PatchOperation> seen = new(new ReferenceComparer());
        private readonly Dictionary<Type, FieldInfo[]> layouts = new();
        internal int FailureIndex(int index) => XmlReuseOperations.FailureIndex(Entries[index], Entries);
        internal int Operation(PatchOperation operation, IdentityWriter output, int depth)
        {
            if (operation == null || depth >= MaxDepth || Entries.Count >= MaxOperations || !seen.Add(operation))
                throw new InvalidDataException("xml-operation-tree-boundary");
            Type type = operation.GetType();
            if (!IsConcrete(type) || type == typeof(PatchOperationFindMod) && !AllowFindMod)
                throw new InvalidDataException("xml-operation-custom-or-unadmitted-type");
            FieldInfo[] fields = Layout(type);
            int index = Entries.Count;
            var never = fields.Single(f => f.Name == "neverSucceeded");
            var failure = fields.SingleOrDefault(f => f.Name == "lastFailedOperation");
            // The placeholder reserves preorder indices while children recurse.
            Entries.Add(new Entry(operation, never, failure, Array.Empty<int>()));
            var children = new List<int>();
            output.Text(type.FullName);
            foreach (FieldInfo field in fields)
            {
                if (field == never || field == failure) continue;
                output.Text(field.DeclaringType!.FullName); output.Text(field.Name);
                object? value = field.GetValue(operation);
                if (field.FieldType == typeof(string)) output.Text((string?)value);
                else if (field.FieldType.IsEnum) output.Int(Convert.ToInt32(value));
                else if (field.FieldType == typeof(PatchOperation))
                {
                    output.Byte(value == null ? (byte)0 : (byte)1);
                    if (value != null) children.Add(Operation((PatchOperation)value, output, depth + 1));
                }
                else if (field.FieldType == typeof(List<PatchOperation>))
                {
                    var list = (List<PatchOperation>?)value;
                    if (list != null && (list.GetType() != typeof(List<PatchOperation>) || list.Count > MaxOperations))
                        throw new InvalidDataException("xml-operation-list");
                    output.Int(list?.Count ?? -1);
                    if (list != null) foreach (PatchOperation child in list) children.Add(Operation(child, output, depth + 1));
                }
                else if (field.FieldType == typeof(List<string>))
                {
                    var list = (List<string>?)value;
                    if (list != null && (list.GetType() != typeof(List<string>) || list.Count > MaxOperations))
                        throw new InvalidDataException("xml-operation-name-list");
                    output.Int(list?.Count ?? -1);
                    if (list != null) foreach (string name in list) output.Text(name);
                }
                else if (field.FieldType == typeof(XmlContainer))
                {
                    output.Byte(value == null ? (byte)0 : (byte)1);
                    if (value == null) continue;
                    if (value.GetType() != typeof(XmlContainer)) throw new InvalidDataException("xml-operation-custom-container");
                    FieldInfo[] container = Layout(typeof(XmlContainer));
                    var node = (XmlNode?)container[0].GetValue(value);
                    output.Byte(node == null ? (byte)0 : (byte)1);
                    if (node != null) output.Node(node, 0);
                }
                else throw new InvalidDataException("xml-operation-field-type");
            }
            Entries[index] = new Entry(operation, never, failure, children.ToArray());
            return index;
        }

        private FieldInfo[] Layout(Type type)
        {
            if (layouts.TryGetValue(type, out var result)) return result;
            var expected = new Dictionary<string, Type>();
            Type parent;
            if (type == typeof(PatchOperation))
            {
                parent = typeof(object);
                expected.Add("sourceFile", typeof(string)); expected.Add("neverSucceeded", typeof(bool));
                expected.Add("success", EnumType(type, "Success", "Normal", "Invert", "Always", "Never"));
            }
            else if (type == typeof(PatchOperationPathed)) { parent = typeof(PatchOperation); expected.Add("xpath", typeof(string)); }
            else if (type == typeof(PatchOperationAttribute)) { parent = typeof(PatchOperationPathed); expected.Add("attribute", typeof(string)); }
            else if (type == typeof(XmlContainer)) { parent = typeof(object); expected.Add("node", typeof(XmlNode)); }
            else if (type == typeof(PatchOperationSequence))
            {
                parent = typeof(PatchOperation); expected.Add("operations", typeof(List<PatchOperation>)); expected.Add("lastFailedOperation", typeof(PatchOperation));
            }
            else if (type == typeof(PatchOperationFindMod) || type == typeof(PatchOperationConditional))
            {
                parent = type == typeof(PatchOperationFindMod) ? typeof(PatchOperation) : typeof(PatchOperationPathed);
                expected.Add("match", typeof(PatchOperation)); expected.Add("nomatch", typeof(PatchOperation));
                if (type == typeof(PatchOperationFindMod)) expected.Add("mods", typeof(List<string>));
            }
            else if (type == typeof(PatchOperationAttributeAdd) || type == typeof(PatchOperationAttributeSet) || type == typeof(PatchOperationAttributeRemove))
            {
                parent = typeof(PatchOperationAttribute);
                if (type != typeof(PatchOperationAttributeRemove)) expected.Add("value", typeof(string));
            }
            else if (type == typeof(PatchOperationAdd) || type == typeof(PatchOperationInsert)
                || type == typeof(PatchOperationAddModExtension) || type == typeof(PatchOperationReplace))
            {
                parent = typeof(PatchOperationPathed); expected.Add("value", typeof(XmlContainer));
                if (type == typeof(PatchOperationAdd) || type == typeof(PatchOperationInsert))
                    expected.Add("order", EnumType(type, "Order", "Append", "Prepend"));
            }
            else if (type == typeof(PatchOperationSetName)) { parent = typeof(PatchOperationPathed); expected.Add("name", typeof(string)); }
            else if (type == typeof(PatchOperationRemove) || type == typeof(PatchOperationTest)) parent = typeof(PatchOperationPathed);
            else throw new InvalidDataException("xml-operation-layout-type");
            FieldInfo[] declared = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (type.BaseType != parent || declared.Length != expected.Count
                || declared.Any(f => f.IsInitOnly || !expected.TryGetValue(f.Name, out var fieldType) || f.FieldType != fieldType))
                throw new InvalidDataException("xml-operation-changed-layout");
            result = (parent == typeof(object) ? Array.Empty<FieldInfo>() : Layout(parent))
                .Concat(declared.OrderBy(f => f.Name, StringComparer.Ordinal)).ToArray();
            layouts.Add(type, result);
            return result;
        }
        private static Type EnumType(Type parent, string name, params string[] names)
        {
            Type? type = parent.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
            if (type == null || !type.IsEnum || Enum.GetUnderlyingType(type) != typeof(int)
                || !Enum.GetNames(type).SequenceEqual(names) || !Enum.GetValues(type).Cast<object>().Select(Convert.ToInt32).SequenceEqual(Enumerable.Range(0, names.Length)))
                throw new InvalidDataException("xml-operation-changed-enum");
            return type;
        }
        private static bool IsConcrete(Type type) => type == typeof(PatchOperationAdd) || type == typeof(PatchOperationAddModExtension)
            || type == typeof(PatchOperationInsert) || type == typeof(PatchOperationRemove) || type == typeof(PatchOperationReplace)
            || type == typeof(PatchOperationSetName) || type == typeof(PatchOperationAttributeAdd) || type == typeof(PatchOperationAttributeRemove)
            || type == typeof(PatchOperationAttributeSet) || type == typeof(PatchOperationTest) || type == typeof(PatchOperationSequence)
            || type == typeof(PatchOperationConditional) || type == typeof(PatchOperationFindMod);
    }

    private sealed class IdentityWriter : IDisposable
    {
        private readonly MemoryStream stream = new();
        private readonly BinaryWriter writer;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        internal IdentityWriter() { writer = new BinaryWriter(stream, Utf8); }
        internal long Length => stream.Length;
        internal void Require(int bytes) { if (bytes < 0 || stream.Length + bytes > MaxIdentity) throw new InvalidDataException("xml-operation-identity-limit"); }
        internal void Int(int value) { Require(4); writer.Write(value); }
        internal void Byte(byte value) { Require(1); writer.Write(value); }
        internal void Text(string? value)
        {
            if (value == null) { Int(-1); return; }
            int count = Utf8.GetByteCount(value); Require(4 + count); Int(count); writer.Write(Utf8.GetBytes(value));
        }
        internal void Truncate(long mark) { stream.SetLength(mark); stream.Position = mark; }
        internal void SetIntAt(long position, int value) { long end = stream.Position; stream.Position = position; writer.Write(value); stream.Position = end; }
        internal byte[] ToArray() => stream.ToArray();
        internal void Node(XmlNode node, int depth)
        {
            if (depth >= MaxDepth || node.OwnerDocument?.GetType() != typeof(XmlDocument) || node.OwnerDocument.DocumentType != null)
                throw new InvalidDataException("xml-operation-xml-boundary");
            Type expected;
            switch (node.NodeType)
            {
                case XmlNodeType.Element: expected = typeof(XmlElement); break;
                case XmlNodeType.Attribute: expected = typeof(XmlAttribute); break;
                case XmlNodeType.Text: expected = typeof(XmlText); break;
                case XmlNodeType.CDATA: expected = typeof(XmlCDataSection); break;
                case XmlNodeType.Comment: expected = typeof(XmlComment); break;
                case XmlNodeType.Whitespace: expected = typeof(XmlWhitespace); break;
                case XmlNodeType.SignificantWhitespace: expected = typeof(XmlSignificantWhitespace); break;
                case XmlNodeType.ProcessingInstruction: expected = typeof(XmlProcessingInstruction); break;
                case XmlNodeType.DocumentFragment: expected = typeof(XmlDocumentFragment); break;
                default: throw new InvalidDataException("xml-operation-xml-kind");
            }
            if (node.GetType() != expected) throw new InvalidDataException("xml-operation-custom-node");
            Int((int)node.NodeType); Text(node.Prefix); Text(node.LocalName); Text(node.NamespaceURI); Text(node.Value);
            Byte(node is XmlElement element && element.IsEmpty ? (byte)1 : (byte)0);
            Byte(node is XmlAttribute attribute && attribute.Specified ? (byte)1 : (byte)0);
            Int(node.Attributes?.Count ?? -1);
            if (node.Attributes != null) foreach (XmlAttribute item in node.Attributes) Node(item, depth + 1);
            Int(node.ChildNodes.Count);
            foreach (XmlNode child in node.ChildNodes) Node(child, depth + 1);
        }
        public void Dispose() { writer.Dispose(); stream.Dispose(); }
    }
}
