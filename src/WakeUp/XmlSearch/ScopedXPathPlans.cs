// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Xml;
using System.Xml.XPath;

namespace WakeUp;

// These are syntax plans, not selected nodes. The native evaluator creates a
// fresh query tree and iterator for every selection, including after mutation.
internal sealed class ScopedXPathPlans : IDisposable
{
    internal const int MaximumEntries = 4096;
    private readonly XmlDocument document;
    private readonly Dictionary<string, XPathExpression> plans = new(StringComparer.Ordinal);
    private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
    private readonly int maximumEntries;
    private bool disposed;
    internal long Compilations { get; private set; }
    internal long Hits { get; private set; }
    internal long Fallbacks { get; private set; }
    internal int Count => plans.Count;

    internal ScopedXPathPlans(XmlDocument document, int maximumEntries = MaximumEntries)
    { this.document = document; this.maximumEntries = maximumEntries; }

    internal XPathNodeIterator Select(XPathNavigator navigator, string xpath)
    {
        if (disposed || Thread.CurrentThread.ManagedThreadId != ownerThread || xpath == null || xpath.Length > 4096
            || document.GetType() != typeof(XmlDocument) || !IsOwnedNavigator(navigator))
        {
            Fallbacks++;
            return navigator.Select(xpath);
        }
        if (!plans.TryGetValue(xpath, out XPathExpression? expression))
        {
            if (plans.Count >= maximumEntries)
            {
                Fallbacks++;
                return navigator.Select(xpath);
            }
            // A failed compilation throws once through the ordinary caller and
            // is never remembered. No retry or different exception is supplied.
            expression = XPathExpression.Compile(xpath);
            Compilations++;
            plans.Add(xpath, expression);
        }
        else Hits++;
        // GOG XPathNavigator.Evaluate clones/resets CompiledXpathExpr.QueryTree.
        // The private expression never receives SetContext or leaves this owner;
        // its native per-call clone preserves lazy results and iterator state.
        return navigator.Select(expression);
    }

    private bool IsOwnedNavigator(XPathNavigator navigator)
    {
        Type type = navigator.GetType();
        if (type.Assembly != typeof(XmlNode).Assembly || type.FullName != "System.Xml.DocumentXPathNavigator"
            || !(navigator is IHasXmlNode source)) return false;
        XmlNode? node = source.GetNode();
        for (XmlNode? current = node; current != null;
            current = current is XmlAttribute attribute ? attribute.OwnerElement : current.ParentNode)
        {
            if (current.GetType().Assembly != typeof(XmlNode).Assembly) return false;
            if (ReferenceEquals(current, document)) return true;
        }
        return false;
    }

    public void Dispose() { disposed = true; plans.Clear(); }
}
