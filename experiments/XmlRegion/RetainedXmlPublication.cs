// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;

namespace WakeUp;

// A finite element-child contract, not a general XML transaction. The caller
// admits the effective native worker/XML methods before entering, validates the
// payload against current operations, and stages imported nodes separately.
// The document must remain exclusively owned from preparation through commit.
internal static class RetainedXmlPublication
{
    internal const int MaximumEdits = 8192;
    internal const int MaximumChildren = 32768;

    internal readonly struct Edit
    {
        internal readonly XmlElement Parent;
        internal readonly XmlElement Child;
        internal readonly XmlElement? Reference;
        internal readonly bool IsRemove;

        private Edit(XmlElement parent, XmlElement child, XmlElement? reference, bool remove)
        {
            Parent = parent;
            Child = child;
            Reference = reference;
            IsRemove = remove;
        }

        internal static Edit Insert(XmlElement parent, XmlElement child, XmlElement? reference = null) => new(parent, child, reference, false);
        internal static Edit Remove(XmlElement parent, XmlElement child) => new(parent, child, null, true);
    }

    // The only writes used by the supported native element mutations:
    // XmlNode.parentNode, XmlLinkedNode.next, XmlElement.lastChild, and the
    // document's reportValidity flag. Field names differ in modern .NET.
    private sealed class Access
    {
        internal readonly FieldInfo Parent = Find(typeof(XmlNode), "parentNode", "_parentNode", typeof(XmlNode));
        internal readonly FieldInfo Next = Find(typeof(XmlLinkedNode), "next", "_next", typeof(XmlLinkedNode));
        internal readonly FieldInfo Last = Find(typeof(XmlElement), "lastChild", "_lastChild", typeof(XmlLinkedNode));
        internal readonly FieldInfo Validity = Find(typeof(XmlDocument), "reportValidity", "_reportValidity", typeof(bool));
        internal readonly FieldInfo[] Events;
        internal readonly Action<XmlNode, XmlNode?>[] Store;
        internal readonly Action<XmlDocument> Invalidate;

        internal Access()
        {
            string[] names = { "onNodeInsertingDelegate", "onNodeInsertedDelegate", "onNodeRemovingDelegate", "onNodeRemovedDelegate", "onNodeChangingDelegate", "onNodeChangedDelegate" };
            Events = new FieldInfo[names.Length];
            for (int i = 0; i < names.Length; i++)
                Events[i] = Find(typeof(XmlDocument), names[i], "_" + names[i], typeof(XmlNodeChangedEventHandler));
            Store = new[] { Setter(Parent), Setter(Next), Setter(Last) };
            var invalidator = new DynamicMethod("WakeUpXmlValidity", typeof(void), new[] { typeof(XmlDocument) }, typeof(RetainedXmlPublication).Module, true);
            ILGenerator il = invalidator.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stfld, Validity);
            il.Emit(OpCodes.Ret);
            Invalidate = (Action<XmlDocument>)invalidator.CreateDelegate(typeof(Action<XmlDocument>));

            // Materialize and execute every generated store before it can touch
            // the caller's document. No reflection/JIT first use is in commit.
            var probe = new XmlDocument();
            XmlElement node = probe.CreateElement("probe");
            Store[0](node, null);
            Store[1](node, null);
            Store[2](node, node);
            Invalidate(probe);
            if (node.ParentNode != null || node.NextSibling != null || !node.IsEmpty || (bool)Validity.GetValue(probe)!)
                throw new NotSupportedException("Unsupported XML link fields.");
        }

        private static FieldInfo Find(Type type, string mono, string desktop, Type fieldType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo? field = type.GetField(mono, flags) ?? type.GetField(desktop, flags);
            if (field == null || field.FieldType != fieldType || field.IsInitOnly || field.IsStatic)
                throw new NotSupportedException("Unsupported XML field: " + type.Name + "." + mono);
            return field;
        }

        private static Action<XmlNode, XmlNode?> Setter(FieldInfo field)
        {
            var method = new DynamicMethod("WakeUpXmlLink" + field.Name, typeof(void), new[] { typeof(XmlNode), typeof(XmlNode) }, typeof(RetainedXmlPublication).Module, true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, field.DeclaringType!);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, field.FieldType);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return (Action<XmlNode, XmlNode?>)method.CreateDelegate(typeof(Action<XmlNode, XmlNode?>));
        }
    }

    internal readonly struct Write
    {
        internal readonly int Field;
        internal readonly XmlNode Target;
        internal readonly XmlNode? Value;
        internal Write(int field, XmlNode target, XmlNode? value) { Field = field; Target = target; Value = value; }
    }

    internal sealed class Prepared
    {
        private readonly XmlDocument document;
        private readonly Action<XmlNode, XmlNode?>[] stores;
        private readonly Action<XmlDocument> invalidate;
        private readonly Write[] writes;
        private bool committed;

        internal Prepared(XmlDocument document, Action<XmlNode, XmlNode?>[] stores, Action<XmlDocument> invalidate, Write[] writes)
        {
            this.document = document;
            this.stores = stores;
            this.invalidate = invalidate;
            this.writes = writes;
        }

        // After the first live write, no catch/fallback is permitted. All operands,
        // writes and delegates are prepared above. No XML API, allocation or
        // observer callback occurs here. Asynchronous process/thread failure is
        // outside this contract, just as for ordinary native XML mutations.
        internal void Commit()
        {
            if (committed)
                throw new InvalidOperationException("XML publication is single-use.");
            committed = true;
            if (writes.Length == 0)
                return;
            invalidate(document);
            for (int i = 0; i < writes.Length; i++)
            {
                Write write = writes[i];
                stores[write.Field](write.Target, write.Value);
            }
        }

        internal int WriteCount => writes.Length;
    }

    private static Access? access;
    private static bool accessRefused;

    // authorizeOwnedObserver may recognize ONLY a caller-owned observer whose
    // caches the caller invalidates before Commit. Foreign subscribers refuse.
    // This method never removes subscribers or claims their effects were run.
    internal static bool TryPrepare(XmlDocument document, IReadOnlyList<Edit> edits,
        Func<Delegate, bool>? authorizeOwnedObserver, out Prepared? prepared, out string reason)
    {
        prepared = null;
        reason = "unsupported-xml-publication";
        try
        {
            if (document == null || document.GetType() != typeof(XmlDocument) || edits == null || edits.Count > MaximumEdits ||
                document.DocumentType != null || document.NameTable.GetType() != typeof(NameTable) || document.Implementation.GetType() != typeof(XmlImplementation))
                return false;
            if (accessRefused)
                return false;
            Access fields;
            try { fields = access ??= new Access(); }
            catch { accessRefused = true; return false; }
            foreach (FieldInfo field in fields.Events)
            {
                if (field.GetValue(document) is not Delegate subscribers)
                    continue;
                foreach (Delegate subscriber in subscribers.GetInvocationList())
                    if (authorizeOwnedObserver == null || !authorizeOwnedObserver(subscriber))
                    {
                        reason = "xml-event-observer";
                        return false;
                    }
            }

            var simulation = new Simulation(document, fields);
            for (int i = 0; i < edits.Count; i++)
                if (!simulation.Apply(edits[i]))
                {
                    reason = "unsupported-or-invalid-element-edit";
                    return false;
                }
            prepared = new Prepared(document, fields.Store, fields.Invalidate, simulation.Writes.ToArray());
            reason = "prepared";
            return true;
        }
        catch
        {
            // Only preflight can fail back. No caller XML field has been written.
            reason = "xml-publication-preflight-failed";
            return false;
        }
    }

    private sealed class Simulation
    {
        private readonly XmlDocument document;
        private readonly Access fields;
        private readonly Dictionary<XmlElement, List<XmlElement>> children = new();
        private readonly Dictionary<XmlElement, XmlElement?> parents = new();
        private int childCount;
        internal readonly List<Write> Writes = new();

        internal Simulation(XmlDocument document, Access fields) { this.document = document; this.fields = fields; }

        private bool Native(XmlElement? node)
        {
            if (node == null || node.GetType() != typeof(XmlElement) || !ReferenceEquals(node.OwnerDocument, document))
                return false;
            // Entity/read-only ancestry and custom parent implementations cannot
            // participate. Detached imported elements are permitted.
            XmlNode? ancestor = node;
            int depth = 0;
            while (ancestor != null && !ReferenceEquals(ancestor, document))
            {
                if (++depth > 256 || ancestor.GetType() != typeof(XmlElement))
                    return false;
                ancestor = ancestor.ParentNode;
            }
            return true;
        }

        private List<XmlElement>? Children(XmlElement parent)
        {
            if (children.TryGetValue(parent, out List<XmlElement>? found))
                return found;
            if (!Native(parent))
                return null;
            var result = new List<XmlElement>();
            XmlNode? node = parent.FirstChild;
            while (node != null)
            {
                if (++childCount > MaximumChildren || node is not XmlElement element || !Native(element) || !ReferenceEquals(element.ParentNode, parent))
                    return null;
                if (result.Contains(element))
                    return null;
                result.Add(element);
                parents[element] = parent;
                node = element.NextSibling;
            }
            // Validate the complete native circular ring including empty-tag
            // sentinel. LastNode publicly hides lastChild == parent.
            object? last = fields.Last.GetValue(parent);
            if (result.Count == 0)
            {
                if (last != null && !ReferenceEquals(last, parent))
                    return null;
            }
            else
            {
                if (!ReferenceEquals(last, result[result.Count - 1]))
                    return null;
                for (int i = 0; i < result.Count; i++)
                    if (!ReferenceEquals(fields.Next.GetValue(result[i]), result[(i + 1) % result.Count]))
                        return null;
            }
            children.Add(parent, result);
            return result;
        }

        private XmlElement? Parent(XmlElement child)
        {
            if (parents.TryGetValue(child, out XmlElement? value))
                return value;
            value = child.ParentNode as XmlElement;
            parents.Add(child, value);
            return value;
        }

        internal bool Apply(Edit edit)
        {
            if (!Native(edit.Parent) || !Native(edit.Child) || ReferenceEquals(edit.Parent, edit.Child))
                return false;
            List<XmlElement>? list = Children(edit.Parent);
            if (list == null)
                return false;
            if (edit.IsRemove)
                return Remove(edit.Parent, edit.Child, list);
            if (edit.Reference != null && (!Native(edit.Reference) || !list.Contains(edit.Reference)))
                return false;
            // The finite contract admits only detached insertion; a move is an
            // explicit Remove followed by Insert. This is enough for imported
            // native patch values and preserves mutation order unambiguously.
            if (!parents.ContainsKey(edit.Child) && edit.Child.ParentNode == null && fields.Next.GetValue(edit.Child) != null)
                return false;
            if (Parent(edit.Child) != null || edit.Child.ParentNode is XmlDocument)
                return false;
            XmlElement? ancestor = edit.Parent;
            int depth = 0;
            while (ancestor != null)
            {
                if (++depth > 256 || ReferenceEquals(ancestor, edit.Child))
                    return false;
                ancestor = Parent(ancestor);
            }
            int index = edit.Reference == null ? list.Count : list.IndexOf(edit.Reference);
            if (list.Count == 0)
            {
                Writes.Add(new Write(1, edit.Child, edit.Child));
                Writes.Add(new Write(2, edit.Parent, edit.Child));
            }
            else
            {
                XmlElement previous = list[(index + list.Count - 1) % list.Count];
                XmlElement next = list[index % list.Count];
                Writes.Add(new Write(1, edit.Child, next));
                Writes.Add(new Write(1, previous, edit.Child));
                if (index == list.Count)
                    Writes.Add(new Write(2, edit.Parent, edit.Child));
            }
            Writes.Add(new Write(0, edit.Child, edit.Parent));
            list.Insert(index, edit.Child);
            parents[edit.Child] = edit.Parent;
            return true;
        }

        private bool Remove(XmlElement parent, XmlElement child, List<XmlElement> list)
        {
            int index = list.IndexOf(child);
            if (index < 0 || !ReferenceEquals(Parent(child), parent))
                return false;
            if (list.Count == 1)
                Writes.Add(new Write(2, parent, null));
            else
            {
                XmlElement previous = list[(index + list.Count - 1) % list.Count];
                XmlElement next = list[(index + 1) % list.Count];
                Writes.Add(new Write(1, previous, next));
                if (index == list.Count - 1)
                    Writes.Add(new Write(2, parent, previous));
            }
            Writes.Add(new Write(1, child, null));
            Writes.Add(new Write(0, child, null));
            list.RemoveAt(index);
            parents[child] = null;
            return true;
        }
    }
}
