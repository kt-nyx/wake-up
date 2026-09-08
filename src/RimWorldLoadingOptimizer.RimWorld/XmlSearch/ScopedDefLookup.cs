// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace RimWorldLoadingOptimizer.RimWorld;

// A startup-only index of direct Def children, keyed separately by defName
// elements and Name template attributes. The XML library still evaluates
// the rest of each expression, preserving native suffix and node behavior.
internal sealed class ScopedDefLookup : IDisposable
{
    internal const int MaximumEntries = 65536;
    internal const int MaximumQueries = 4096;
    private static readonly Regex Prefix = new("^/?Defs/(?<type>[A-Za-z_][A-Za-z0-9_.-]*)\\[(?<key>defName|@Name)\\s*=\\s*(?<quote>['\"])(?<name>[^'\"]*)\\k<quote>\\](?<tail>.*)$", RegexOptions.CultureInvariant);
    private readonly XmlDocument document;
    private readonly Dictionary<string, Dictionary<string, XmlElement?>> types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Query?> queries = new(StringComparer.Ordinal);
    private readonly int maximumEntries;
    private bool disposed;
    private bool refused;
    private int pendingMutations;
    private int entries;
    internal long Hits
    {
        get; private set;
    }
    internal long AttributeHits
    {
        get; private set;
    }
    internal long Fallbacks
    {
        get; private set;
    }
    internal long Rebuilds
    {
        get; private set;
    }
    internal long Invalidations
    {
        get; private set;
    }
    internal int PeakEntries
    {
        get; private set;
    }
    internal int QueryCount => queries.Count;

    internal ScopedDefLookup(XmlDocument document, int maximumEntries = MaximumEntries)
    {
        this.document = document;
        this.maximumEntries = maximumEntries;
        refused = document.GetType() != typeof(XmlDocument);
        if (refused)
            return;
        // Before AND after: callbacks during a changing event may query the
        // old tree, whereas callbacks after it must see the new tree. Removing
        // events supply the former parent after the node is disconnected.
        document.NodeInserting += Changing;
        document.NodeInserted += Changed;
        document.NodeRemoving += Changing;
        document.NodeRemoved += Changed;
        document.NodeChanging += Changing;
        document.NodeChanged += Changed;
    }

    internal XmlNodeList SelectNodes(XmlNode context, string xpath)
    {
        if (TryFind(context, xpath, out XmlElement? def, out Query? query))
        {
            XmlNodeList result = def!.SelectNodes(query!.Relative)!;
            // A native foreach prefetches two results before the first worker
            // mutation. Keep that boundary: multi-result suffixes retain the
            // original document iterator and its mutation behavior.
            if (result.Item(1) == null)
            {
                Hits++;
                if (query.Attribute)
                    AttributeHits++;
                return result;
            }
        }
        Fallbacks++;
        return context.SelectNodes(xpath)!;
    }

    internal XmlNode? SelectSingleNode(XmlNode context, string xpath)
    {
        if (TryFind(context, xpath, out XmlElement? def, out Query? query))
        {
            Hits++;
            if (query!.Attribute)
                AttributeHits++;
            return def!.SelectSingleNode(query!.Relative);
        }
        Fallbacks++;
        return context.SelectSingleNode(xpath);
    }

    private bool TryFind(XmlNode context, string xpath, out XmlElement? def, out Query? query)
    {
        def = null;
        query = null;
        if (disposed || refused || pendingMutations != 0 || !ReferenceEquals(context, document) || xpath == null || xpath.Length > 4096)
            return false;
        XmlElement? root = document.DocumentElement;
        if (root == null || root.GetType() != typeof(XmlElement) || root.Name != "Defs" || root.NamespaceURI.Length != 0)
            return false;
        if (!queries.TryGetValue(xpath, out query))
        {
            query = Parse(xpath);
            if (queries.Count < MaximumQueries)
                queries.Add(xpath, query);
        }
        if (query == null)
            return false;
        if (!types.TryGetValue(query.IndexKey, out Dictionary<string, XmlElement?>? names))
        {
            names = Build(root, query);
            if (names == null)
                return false;
        }
        // Missing and duplicate names stay native. In particular, never claim
        // an absent result using an index which excludes unfamiliar XML nodes.
        return names.TryGetValue(query.Name, out def) && def != null;
    }

    private Dictionary<string, XmlElement?>? Build(XmlElement root, Query query)
    {
        var names = new Dictionary<string, XmlElement?>(StringComparer.Ordinal);
        foreach (XmlNode child in root.ChildNodes)
        {
            // XPath exposes expanded entity children, which this direct-node
            // walk does not index. Keep those documents on the native path.
            if (child.NodeType == XmlNodeType.EntityReference)
            {
                Refuse();
                return null;
            }
            if (child.NodeType != XmlNodeType.Element)
                continue;
            if (child.GetType() != typeof(XmlElement))
            {
                Refuse();
                return null;
            }
            if (child.Name != query.Type || child.NamespaceURI.Length != 0)
                continue;
            if (query.Attribute)
            {
                XmlAttribute? name = ((XmlElement)child).GetAttributeNode("Name", string.Empty);
                if (name == null)
                    continue;
                if (name.GetType() != typeof(XmlAttribute) || !NativeTextTree(name))
                {
                    Refuse();
                    return null;
                }
                if (!AddName(names, name.Value, (XmlElement)child))
                    return null;
                continue;
            }
            foreach (XmlNode name in child.ChildNodes)
            {
                if (name.NodeType == XmlNodeType.EntityReference)
                {
                    Refuse();
                    return null;
                }
                if (name.NodeType != XmlNodeType.Element || name.Name != "defName" || name.NamespaceURI.Length != 0)
                    continue;
                if (name.GetType() != typeof(XmlElement) || !NativeTextTree(name))
                {
                    Refuse();
                    return null;
                }
                if (!AddName(names, name.InnerText, (XmlElement)child))
                    return null;
            }
        }
        types.Add(query.IndexKey, names);
        entries += names.Count;
        PeakEntries = Math.Max(PeakEntries, entries);
        Rebuilds++;
        return names;
    }

    private bool AddName(Dictionary<string, XmlElement?> names, string key, XmlElement child)
    {
        if (names.TryGetValue(key, out XmlElement? previous))
        {
            if (!ReferenceEquals(previous, child))
                names[key] = null;
        }
        else
        {
            if (entries + names.Count >= maximumEntries)
            {
                Refuse();
                return false;
            }
            names.Add(key, child);
        }
        return true;
    }

    private static bool NativeTextTree(XmlNode node)
    {
        // Names containing nested markup are uncommon; reject custom nodes at
        // any depth because their string-value or navigation may be overridden.
        if (node.GetType().Assembly != typeof(XmlNode).Assembly)
            return false;
        foreach (XmlNode child in node.ChildNodes)
        if (!NativeTextTree(child))
            return false;
        return true;
    }

    private static Query? Parse(string xpath)
    {
        Match match = Prefix.Match(xpath);
        if (!match.Success)
            return null;
        string tail = match.Groups["tail"].Value;
        if (tail.Length != 0 && tail[0] != '/')
            return null;
        // Only downward paths. Predicates can still use normal XPath functions
        // and comparisons; reject escape/union syntax conservatively, including
        // when quoted, rather than partially implementing the XPath language.
        if (tail.Contains("|") || tail.Contains("..") || tail.Contains("::") || tail.Contains("/Defs"))
            return null;
        string relative = "self::" + match.Groups["type"].Value
            + xpath.Substring(xpath.IndexOf('['));
        try
        {
            XPathExpression.Compile(xpath);
        }
        catch (XPathException) { return null; } // Original call reproduces its error.
        return new Query(match.Groups["type"].Value, match.Groups["name"].Value, relative, match.Groups["key"].Value == "@Name");
    }

    private void Changing(object sender, XmlNodeChangedEventArgs args)
    {
        if (disposed || refused)
            return;
        // Event subscribers can query before or after our own callback. Do
        // not let a before-event query rebuild an index which another
        // subscriber would observe after the tree changes. If a subscriber
        // aborts the mutation, the unmatched depth conservatively keeps native
        // queries for the remaining scope. Nested mutations balance normally.
        pendingMutations++;
        InvalidateChanged(args);
    }

    private void Changed(object sender, XmlNodeChangedEventArgs args)
    {
        if (disposed || refused)
            return;
        InvalidateChanged(args);
        if (pendingMutations > 0)
            pendingMutations--;
    }

    private void InvalidateChanged(XmlNodeChangedEventArgs args)
    {
        if (disposed || refused)
            return;
        XmlElement? root = document.DocumentElement;
        if (ReferenceEquals(args.OldParent, document) || ReferenceEquals(args.NewParent, document))
        {
            InvalidateAll();
            return;
        }
        if (root == null)
            return;
        InvalidateFor(args.Node, args.OldParent, root);
        InvalidateFor(args.Node, args.NewParent, root);
    }

    private void InvalidateFor(XmlNode node, XmlNode? parent, XmlElement root)
    {
        if (node is XmlAttribute attribute)
        {
            // Attributes have no ParentNode. Removal events retain the old
            // element in their parent argument after OwnerElement becomes null.
            XmlElement? owner = parent as XmlElement ?? attribute.OwnerElement;
            if (owner != null && ReferenceEquals(owner.ParentNode, root)
                && attribute.LocalName == "Name" && attribute.NamespaceURI.Length == 0)
                Invalidate("@" + owner.Name);
            return;
        }
        if (parent == null)
            parent = node.ParentNode;
        if (ReferenceEquals(parent, root))
        {
            if (node.NodeType == XmlNodeType.EntityReference)
                InvalidateAll();
            else
            {
                Invalidate(node.Name);
                Invalidate("@" + node.Name);
            }
            return;
        }
        XmlNode current = node;
        XmlNode? ancestor = parent;
        while (ancestor != null && !ReferenceEquals(ancestor, root))
        {
            // Value replacement, text edits and entity-child edits report the
            // attribute (possibly above the immediate parent), not its owner.
            if (ancestor is XmlAttribute ownerAttribute)
            {
                InvalidateFor(ownerAttribute, ownerAttribute.OwnerElement, root);
                return;
            }
            if (ReferenceEquals(ancestor.ParentNode, root))
            {
                if (current.NodeType == XmlNodeType.EntityReference
                    || (current.Name == "defName" && current.NamespaceURI.Length == 0))
                    Invalidate(ancestor.Name);
                return;
            }
            current = ancestor;
            ancestor = ancestor.ParentNode;
        }
    }

    private void Invalidate(string type)
    {
        if (!types.TryGetValue(type, out Dictionary<string, XmlElement?>? names))
            return;
        entries -= names.Count;
        types.Remove(type);
        Invalidations++;
    }

    private void InvalidateAll()
    {
        if (types.Count != 0)
            Invalidations++;
        types.Clear();
        entries = 0;
    }

    private void Refuse()
    {
        refused = true;
        InvalidateAll();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        document.NodeInserting -= Changing;
        document.NodeInserted -= Changed;
        document.NodeRemoving -= Changing;
        document.NodeRemoved -= Changed;
        document.NodeChanging -= Changing;
        document.NodeChanged -= Changed;
        types.Clear();
        queries.Clear();
        entries = 0;
    }

    private sealed class Query
    {
        internal readonly string Type;
        internal readonly string Name;
        internal readonly string Relative;
        internal readonly bool Attribute;
        // Cache the index identity with the parsed query. '@' cannot begin an
        // admitted element type, so template and defName keys cannot collide.
        internal readonly string IndexKey;
        internal Query(string type, string name, string relative, bool attribute)
        {
            Type = type;
            Name = name;
            Relative = relative;
            Attribute = attribute;
            IndexKey = attribute ? "@" + type : type;
        }
    }
}
