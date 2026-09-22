// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Xml;

namespace WakeUp;

// Reuses only completed single-node queries over the current real XML tree.
// Every mutation invalidates all entries, including misses and detached results.
// No XmlNodeList is retained or substituted: arbitrary custom callers keep the
// library's original lazy-list behavior.
internal sealed class ScopedSingleQueryMemo : IDisposable
{
    internal const int MaximumEntries = 4096;
    private const int MaximumInspectedNodes = 1000000;
    private readonly XmlDocument document;
    private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
    private readonly object sync = new();
    private readonly Dictionary<XmlNode, Dictionary<string, XmlNode?>> results = new();
    private readonly int maximumEntries;
    private long revision;
    private int pendingMutations;
    private int entries;
    private bool refused;
    private bool disposed;
    internal long Hits { get; private set; }
    internal long MissHits { get; private set; }
    internal long Captures { get; private set; }
    internal long Invalidations { get; private set; }
    internal int PeakEntries { get; private set; }

    internal ScopedSingleQueryMemo(XmlDocument document, int maximumEntries = MaximumEntries)
    {
        this.document = document;
        this.maximumEntries = maximumEntries;
        refused = document.GetType() != typeof(XmlDocument) || !NativeTree(document);
        if (refused)
            return;
        document.NodeInserting += Changing;
        document.NodeInserted += Changed;
        document.NodeRemoving += Changing;
        document.NodeRemoved += Changed;
        document.NodeChanging += Changing;
        document.NodeChanged += Changed;
    }

    // A nonnegative version is an admission ticket for recording an original
    // result afterward. Mutations, reentrancy and foreign threads revoke it.
    internal bool TryRead(XmlNode context, string xpath, out XmlNode? result, out long version)
    {
        result = null;
        version = -1;
        lock (sync)
        {
            if (!Available() || xpath == null || xpath.Length > 4096 || !Connected(context))
                return false;
            version = revision;
            if (!results.TryGetValue(context, out Dictionary<string, XmlNode?>? queries)
                || !queries.TryGetValue(xpath, out result))
                return false;
            Hits++;
            if (result == null)
                MissHits++;
            return true;
        }
    }

    internal void Capture(XmlNode context, string xpath, long version, XmlNode? result)
    {
        lock (sync)
        {
            if (version < 0 || !Available() || revision != version || entries >= maximumEntries)
                return;
            if (!results.TryGetValue(context, out Dictionary<string, XmlNode?>? queries))
                results.Add(context, queries = new Dictionary<string, XmlNode?>(StringComparer.Ordinal));
            if (queries.ContainsKey(xpath))
                return;
            queries.Add(xpath, result);
            entries++;
            Captures++;
            PeakEntries = Math.Max(PeakEntries, entries);
        }
    }

    private bool Available()
    {
        if (Thread.CurrentThread.ManagedThreadId != ownerThread)
        {
            refused = true;
            Clear();
        }
        return !disposed && !refused && pendingMutations == 0;
    }

    private bool Connected(XmlNode node)
    {
        // Attribute.ParentNode is null, but its owner is part of the real tree.
        for (XmlNode? current = node; current != null;
            current = current is XmlAttribute attribute ? attribute.OwnerElement : current.ParentNode)
        {
            if (current.GetType().Assembly != typeof(XmlNode).Assembly)
                return false;
            if (ReferenceEquals(current, document))
                return true;
        }
        return false;
    }

    private static bool NativeTree(XmlNode node)
    {
        // A custom XmlNode can change its virtual properties without emitting
        // mutation events. Check attributes as well as child nodes, and bound
        // the scan without recursive calls on arbitrarily deep input.
        int count = 0;
        return NativeSubtree(node, ref count);
    }

    private static bool NativeSubtree(XmlNode root, ref int count)
    {
        XmlNode current = root;
        while (true)
        {
            Type type = current.GetType();
            if (++count > MaximumInspectedNodes || !(type == typeof(XmlElement) || type == typeof(XmlText)
                || type == typeof(XmlAttribute) || type == typeof(XmlDocument) || type.Assembly == typeof(XmlNode).Assembly))
                return false;
            var attributes = current.Attributes;
            if (attributes != null)
                foreach (XmlAttribute attribute in attributes)
                    // Native attributes cannot themselves own attributes, so
                    // this does not recurse with document depth.
                    if (!NativeSubtree(attribute, ref count)) return false;
            if (current.FirstChild is XmlNode first) { current = first; continue; }
            while (!ReferenceEquals(current, root) && current.NextSibling == null)
            {
                if (current.ParentNode == null) return false;
                current = current.ParentNode;
            }
            if (ReferenceEquals(current, root)) return true;
            current = current.NextSibling!;
        }
    }

    private void Changing(object sender, XmlNodeChangedEventArgs args)
    {
        lock (sync)
        {
            if (disposed || refused)
                return;
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                refused = true;
            pendingMutations++;
            revision++;
            Clear();
            // This includes a whole newly inserted subtree and custom
            // attributes. Once an unobservable object appears, stay native.
            if (!refused && args.Action == XmlNodeChangedAction.Insert && !NativeTree(args.Node))
                refused = true;
        }
    }

    private void Changed(object sender, XmlNodeChangedEventArgs args)
    {
        lock (sync)
        {
            if (disposed || refused)
                return;
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                refused = true;
            revision++;
            Clear();
            if (pendingMutations > 0)
                pendingMutations--;
        }
    }

    private void Clear()
    {
        if (entries != 0)
            Invalidations++;
        results.Clear();
        entries = 0;
    }

    internal bool OwnsObserver(Delegate observer)
        => ReferenceEquals(observer.Target, this)
            && (observer.Method == ((XmlNodeChangedEventHandler)Changing).Method
                || observer.Method == ((XmlNodeChangedEventHandler)Changed).Method);

    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            document.NodeInserting -= Changing;
            document.NodeInserted -= Changed;
            document.NodeRemoving -= Changing;
            document.NodeRemoved -= Changed;
            document.NodeChanging -= Changing;
            document.NodeChanged -= Changed;
            Clear();
        }
    }
}
