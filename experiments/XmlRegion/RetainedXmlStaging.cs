// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace WakeUp;

// Native ImportNode can allocate detached nodes without changing the document's
// intern tables only when every required name already exists. This read-only
// admission avoids name-table rollback or replacing the document/name table.
// The caller separately admits effective native method bodies and hooks.
internal static class RetainedXmlStaging
{
    internal const int MaximumNodes = 32768;
    internal const int MaximumDepth = 64;
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private static readonly string[] EmptyDocumentFields = { "schemaInfo", "schemas", "htElementIDAttrDecl", "htElementIdMap" };

    // These helpers are additional to the publication/worker contracts. The
    // staging decision itself invokes only GetXmlName/GetName and NameTable.Get.
    internal static MethodInfo[] RelevantMethods()
    {
        Type document = typeof(XmlDocument);
        Type domNames = document.Assembly.GetType("System.Xml.DomNameTable", true)!;
        Type xmlName = document.Assembly.GetType("System.Xml.XmlName", true)!;
        return new[]
        {
            document.GetMethod("GetXmlName", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(IXmlSchemaInfo) }, null)!,
            document.GetMethod("AddXmlName", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(IXmlSchemaInfo) }, null)!,
            domNames.GetMethod("GetName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!,
            domNames.GetMethod("AddName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!,
            xmlName.GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(IXmlSchemaInfo) }, null)!,
            typeof(NameTable).GetMethod("Get", new[] { typeof(string) })!,
            typeof(NameTable).GetMethod("Add", new[] { typeof(string) })!,
            document.GetMethod("ImportNodeInternal", BindingFlags.Instance | BindingFlags.NonPublic)!,
            document.GetMethod("ImportAttributes", BindingFlags.Instance | BindingFlags.NonPublic)!,
            document.GetMethod("ImportChildren", BindingFlags.Instance | BindingFlags.NonPublic)!,
            document.GetMethod("CreateElement", new[] { typeof(string), typeof(string), typeof(string) })!,
            document.GetMethod("CreateAttribute", new[] { typeof(string), typeof(string), typeof(string) })!,
            document.GetMethod("CreateTextNode", new[] { typeof(string) })!
        };
    }

    internal static bool TryAdmit(XmlDocument document, IReadOnlyList<XmlElement> templates,
        Func<Delegate, bool>? ownedObserver, out string reason)
    {
        reason = "unsupported-xml-staging";
        try
        {
            if (templates == null || templates.Count > MaximumNodes ||
                !RetainedXmlPublication.TryPrepare(document, Array.Empty<RetainedXmlPublication.Edit>(), ownedObserver, out _, out reason))
                return false;
            FieldInfo? validity = Field("reportValidity", typeof(bool));
            if (validity == null || (bool)validity.GetValue(document)!)
            {
                reason = "xml-staging-validity";
                return false;
            }
            foreach (string name in EmptyDocumentFields)
            {
                FieldInfo? field = Field(name);
                if (field == null || field.GetValue(document) != null)
                {
                    reason = "xml-staging-schema-or-id-state";
                    return false;
                }
            }
            Type? domType = typeof(XmlDocument).Assembly.GetType("System.Xml.DomNameTable");
            FieldInfo? domField = Field("domNameTable", domType);
            if (domType == null || domField?.GetValue(document)?.GetType() != domType)
                return false;
            MethodInfo? getName = typeof(XmlDocument).GetMethod("GetXmlName", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(string), typeof(string), typeof(IXmlSchemaInfo) }, null);
            if (getName == null)
                return false;
            int nodes = 0;
            foreach (XmlElement template in templates)
                if (!Check(template, 0, document, getName, ref nodes, out reason))
                    return false;
            reason = "existing-native-names";
            return true;
        }
        catch
        {
            reason = "xml-staging-admission-failed";
            return false;
        }
    }

    private static FieldInfo? Field(string name, Type? expected = null)
    {
        FieldInfo? field = typeof(XmlDocument).GetField(name, InstanceFields) ?? typeof(XmlDocument).GetField("_" + name, InstanceFields);
        return field != null && !field.IsStatic && (expected == null || field.FieldType == expected) ? field : null;
    }

    private static bool Check(XmlNode node, int depth, XmlDocument document, MethodInfo getName, ref int nodes, out string reason)
    {
        reason = "xml-staging-node-kind-or-bound";
        if (node == null || ++nodes > MaximumNodes || depth > MaximumDepth)
            return false;
        Type kind = node.GetType();
        if (kind != typeof(XmlElement) && kind != typeof(XmlAttribute) && kind != typeof(XmlText))
            return false;
        if (kind != typeof(XmlText))
        {
            // Get only: adding missing strings here would defeat preflight.
            // XmlName.Name lazily interns a prefixed qualified name. Its atom
            // must exist too, even when the expanded tuple already exists.
            string qualifiedName = node.Prefix.Length == 0 ? node.LocalName : node.Prefix + ":" + node.LocalName;
            if (document.NameTable.Get(node.Prefix) == null || document.NameTable.Get(node.LocalName) == null ||
                document.NameTable.Get(node.NamespaceURI) == null || document.NameTable.Get(qualifiedName) == null ||
                getName.Invoke(document, new object?[] { node.Prefix, node.LocalName, node.NamespaceURI, null }) == null)
            {
                reason = "xml-staging-missing-native-name";
                return false;
            }
        }
        if (node is XmlElement element && element.HasAttributes)
            foreach (XmlAttribute attribute in element.Attributes)
                if (!Check(attribute, depth + 1, document, getName, ref nodes, out reason))
                    return false;
        foreach (XmlNode child in node.ChildNodes)
            if (!Check(child, depth + 1, document, getName, ref nodes, out reason))
                return false;
        return true;
    }
}
