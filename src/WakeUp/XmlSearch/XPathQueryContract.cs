// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;

namespace WakeUp;

// Both caches need independent publication guards for these downstream calls.
// Sharing the method description must not share installation state: a refused
// plan cache cannot leave the earlier single-node return unguarded.
internal static class XPathQueryContract
{
    internal static readonly MethodInfo[] Methods = {
        typeof(XmlNode).GetMethod(nameof(XmlNode.SelectNodes), new[] { typeof(string) })!,
        typeof(XPathNavigator).GetMethod(nameof(XPathNavigator.Select), new[] { typeof(string) })!,
        typeof(XPathNavigator).GetMethod(nameof(XPathNavigator.Select), new[] { typeof(XPathExpression) })!,
        typeof(XPathExpression).GetMethod(nameof(XPathExpression.Compile), new[] { typeof(string) })!,
        typeof(XPathExpression).GetMethod(nameof(XPathExpression.Compile), new[] { typeof(string), typeof(IXmlNamespaceResolver) })!,
        typeof(XPathNavigator).GetMethod(nameof(XPathNavigator.Evaluate), new[] { typeof(XPathExpression) })!,
        typeof(XPathNavigator).GetMethod(nameof(XPathNavigator.Evaluate), new[] { typeof(XPathExpression), typeof(XPathNodeIterator) })!
    };
}
