// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WakeUp;

// A finite set of reviewed operation implementations, not a package allowlist.
// Wrappers keep their ordinary calls and completion callbacks. Their children
// are described by the owner, which also owns base fields and recorded outcomes.
internal sealed class ProcessedXmlAdapters
{
    internal static readonly Dictionary<string, string> ExpectedBodies = XmlSemanticContracts.Adapters;
    private static readonly Dictionary<Type, PublishedPatchGuard[]> ReviewedGunTypes = new();
    private readonly Dictionary<MethodBase, PublishedPatchGuard> guards = new();
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const int MaximumItems = 32768, MaximumText = 1024 * 1024;
    internal bool Allows() => guards.Values.All(g => g.AllowsOriginalContract());
    // Classification is limited to actual types that passed this helper's
    // complete method/layout admission. Same-name unknown types never qualify.
    internal static bool IsGunOperation(PatchOperation operation) => operation != null && ReviewedGunTypes.ContainsKey(operation.GetType());
    internal static void ReplayModNameEffects(PatchOperation operation, int count)
    {
        if (count < 0 || count > ProcessedXmlSnapshot.MaximumModNameCalls) throw new InvalidDataException("custom-mod-name-call-count");
        if (!IsGunOperation(operation))
        { if (count != 0) throw new InvalidDataException("custom-mod-name-calls-for-nongun"); return; }
        if (!ReviewedGunTypes[operation.GetType()].All(g => g.AllowsOriginalContract())) throw new InvalidDataException("custom-gun-replay-hook");
        // The native worker does this once per successfully visited target,
        // even when AllowWithRunAndGun is true. Cold execution records the
        // actual count; the final XML must not be queried to reconstruct it.
        for (int i = 0; i < count; i++) ModLister.HasActiveModWithName("RunAndGun");
    }

    internal bool TryDescribe(PatchOperation operation, BinaryWriter identity, out PatchOperation[] children,
        out FieldInfo? failureField, out bool runWorkerOnHit, out string reason)
    {
        children = Array.Empty<PatchOperation>(); failureField = null; runWorkerOnHit = true; reason = "unsupported-custom-operation";
        try
        {
            Type type = operation.GetType();
            var fields = new Dictionary<string, Type>(StringComparer.Ordinal);
            string name = type.FullName ?? "";
            switch (name)
            {
                case "Verse.PatchOperationFindMod":
                    if (type != typeof(PatchOperationFindMod)) return false;
                    fields.Add("mods", typeof(List<string>)); fields.Add("match", typeof(PatchOperation)); fields.Add("nomatch", typeof(PatchOperation)); break;
                case "VEF.PatchOperationToggableSequence":
                    fields.Add("enabled", typeof(bool)); fields.Add("label", typeof(string)); fields.Add("mods", typeof(List<string>));
                    fields.Add("operations", typeof(List<PatchOperation>)); fields.Add("lastFailedOperation", typeof(PatchOperation)); break;
                case "CombatExtended.PatchOperationSettingsConditional":
                    fields.Add("settingName", typeof(string)); fields.Add("match", typeof(PatchOperation)); fields.Add("nomatch", typeof(PatchOperation)); break;
                case "CombatExtended.PatchOperation_ConditionalGeneric":
                    fields.Add("standard", typeof(PatchOperation)); fields.Add("generic", typeof(PatchOperation)); break;
                case "CombatExtended.PatchOperationFindMod": fields.Add("modName", typeof(string)); break;
                case "CombatExtended.PatchOperationMakeGunCECompatible":
                    fields.Add("defName", typeof(string)); fields.Add("texPath", typeof(string)); fields.Add("isWeaponPlatform", typeof(bool)); fields.Add("AllowWithRunAndGun", typeof(bool));
                    foreach (string field in new[] { "statBases", "Properties", "AmmoUser", "FireModes", "weaponTags", "weaponClasses", "costList", "researchPrerequisite", "attachmentLinks", "defaultGraphicParts" }) fields.Add(field, typeof(XmlContainer));
                    runWorkerOnHit = false; break;
                default: return false;
            }
            FieldInfo[] actual = type.GetFields(Declared);
            if (type.BaseType != typeof(PatchOperation) || actual.Length != fields.Count
                || actual.Any(f => !fields.TryGetValue(f.Name, out Type expected) || f.FieldType != expected
                    || f.IsInitOnly && !(name == "VEF.PatchOperationToggableSequence" && (f.Name == "mods" || f.Name == "operations"))))
                throw new InvalidDataException("custom-operation-layout");
            // No operation or dependency is read until the effective methods
            // match the independently inspected original implementations.
            foreach (MethodInfo method in type.GetMethods(Declared)) Guard(method);
            Text(identity, name);
            var nested = new List<PatchOperation>();
            foreach (FieldInfo field in actual.OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (field.Name == "lastFailedOperation") { failureField = field; continue; }
                Text(identity, field.Name);
                object? value = field.GetValue(operation);
                if (field.FieldType == typeof(string)) Text(identity, (string?)value);
                else if (field.FieldType == typeof(bool)) identity.Write((bool)value!);
                else if (field.FieldType == typeof(PatchOperation))
                { identity.Write(value != null); if (value != null) nested.Add((PatchOperation)value); }
                else if (field.FieldType == typeof(List<PatchOperation>))
                {
                    var list = ExactList<PatchOperation>(value); identity.Write(list.Count);
                    if (list.Any(p => p == null)) throw new InvalidDataException("custom-null-child");
                    nested.AddRange(list);
                }
                else if (field.FieldType == typeof(List<string>))
                { var list = ExactList<string>(value); identity.Write(list.Count); foreach (string item in list) Text(identity, item); }
                else if (field.FieldType == typeof(XmlContainer)) Container(identity, value);
            }
            if (name == "Verse.PatchOperationFindMod" || name == "VEF.PatchOperationToggableSequence"
                || name == "CombatExtended.PatchOperationMakeGunCECompatible" || name == "CombatExtended.PatchOperationFindMod")
                ModNameInputs(identity, name == "CombatExtended.PatchOperationFindMod");
            if (name == "CombatExtended.PatchOperationSettingsConditional")
            {
                Guard(AccessTools.Method(typeof(AccessTools), "Field", new[] { typeof(Type), typeof(string) }));
                string? setting = (string?)Field(type, "settingName", typeof(string)).GetValue(operation);
                Type settings = type.Assembly.GetType("CombatExtended.Settings", true)!;
                // Match AccessTools.Field's inherited lookup without invoking it
                // early; the real wrapper retains errors at its native position.
                FieldInfo? value = FindField(settings, setting);
                Text(identity, value?.DeclaringType?.FullName); Text(identity, value?.FieldType.AssemblyQualifiedName);
                if (value != null && value.FieldType == typeof(bool)) identity.Write((bool)value.GetValue(SettingsObject(type.Assembly, "CombatExtended.Controller", settings))!);
            }
            else if (name == "CombatExtended.PatchOperation_ConditionalGeneric")
            {
                Type settings = type.Assembly.GetType("CombatExtended.Settings", true)!;
                Guard(AccessTools.PropertyGetter(settings, "GenericAmmo"));
                identity.Write((bool)Field(settings, "genericAmmo", typeof(bool)).GetValue(SettingsObject(type.Assembly, "CombatExtended.Controller", settings))!);
            }
            else if (name == "VEF.PatchOperationToggableSequence")
            {
                Type settings = type.Assembly.GetType("VEF.VFEGlobalSettings", true)!;
                object current = SettingsObject(type.Assembly, "VEF.VFEGlobal", settings);
                object? raw = Field(settings, "toggablePatch", typeof(Dictionary<string, bool>)).GetValue(current);
                if (raw != null && raw.GetType() != typeof(Dictionary<string, bool>)) throw new InvalidDataException("custom-settings-dictionary");
                var values = (Dictionary<string, bool>?)raw;
                if (values != null && (values.Count > MaximumItems || !ReferenceEquals(values.Comparer, EqualityComparer<string>.Default))) throw new InvalidDataException("custom-settings-comparer");
                identity.Write(values?.Count ?? -1);
                if (values != null) foreach (var pair in values.OrderBy(p => p.Key, StringComparer.Ordinal)) { Text(identity, pair.Key); identity.Write(pair.Value); }
            }
            children = nested.ToArray(); reason = "";
            if (!Allows()) return false;
            if (name == "CombatExtended.PatchOperationMakeGunCECompatible") ReviewedGunTypes[type] = guards.Values.ToArray();
            return true;
        }
        catch (Exception e) { reason = e.Message; return false; }
    }

    // Do not call HasActiveModWithName or lazy metadata getters while planning.
    // Bind primitive inputs without initializing native lazy caches. Wrappers
    // still execute their native getters, including empty-cache assignment.
    private void ModNameInputs(BinaryWriter writer, bool ordered)
    {
        foreach (var method in new[] {
            AccessTools.Method(typeof(ModLister), "HasActiveModWithName"),
            AccessTools.Method(typeof(ModLister), "GetExpansionWithIdentifier"),
            AccessTools.PropertyGetter(typeof(ModLister), "AllExpansions"),
            AccessTools.PropertyGetter(typeof(ModMetaData), "Name"), AccessTools.PropertyGetter(typeof(ModMetaData), "Expansion"),
            AccessTools.PropertyGetter(typeof(ModMetaData), "PackageId"), AccessTools.PropertyGetter(typeof(ModMetaData), "Active"),
            AccessTools.Method(typeof(ModsConfig), "IsActive", new[] { typeof(ModMetaData) }),
            AccessTools.Method(typeof(ModsConfig), "IsActive", new[] { typeof(string) }) }) Guard(method);
        var mods = ExactList<ModMetaData>(Static(typeof(ModLister), "mods", typeof(List<ModMetaData>)));
        object? rawExpansions = Static(typeof(ModLister), "AllExpansionsCached", typeof(List<ExpansionDef>));
        var expansions = ExactList<ExpansionDef>(rawExpansions);
        writer.Write(expansions.Count == 0);
        if (expansions.Count == 0)
        {
            Guard(AccessTools.PropertyGetter(typeof(DefDatabase<ExpansionDef>), "AllDefsListForReading"));
            var definitions = ExactList<ExpansionDef>(Static(typeof(DefDatabase<ExpansionDef>), "defsList", typeof(List<ExpansionDef>)));
            // The reviewed lazy getter's Where predicate never runs for an
            // empty backing list, so no official-mod lookup is evaluated.
            if (definitions.Count != 0) throw new InvalidDataException("custom-expansion-lazy-nonempty-definitions");
            writer.Write(definitions.Count);
        }
        object? rawActive = Static(typeof(ModsConfig), "activeModsHashSet", typeof(HashSet<string>));
        if (rawActive?.GetType() != typeof(HashSet<string>)) throw new InvalidDataException("custom-active-mod-set");
        var active = (HashSet<string>)rawActive;
        if (active.Count > MaximumItems || !ReferenceEquals(active.Comparer, EqualityComparer<string>.Default)) throw new InvalidDataException("custom-active-mod-comparer");
        writer.Write(active.Count); foreach (string value in active.OrderBy(s => s, StringComparer.Ordinal)) Text(writer, value);
        Text(writer, CultureInfo.CurrentCulture.Name);
        Text(writer, (string?)Static(typeof(ModMetaData), "SteamModPostfix", typeof(string)));
        WriteMods(writer, mods);
        writer.Write(expansions.Count);
        foreach (ExpansionDef expansion in expansions)
        {
            if (expansion == null || expansion.GetType() != typeof(ExpansionDef)) throw new InvalidDataException("custom-expansion-type");
            Text(writer, expansion.linkedMod); Text(writer, expansion.label);
        }
        if (ordered)
        {
            Guard(AccessTools.PropertyGetter(typeof(ModsConfig), "ActiveModsInLoadOrder"));
            Guard(AccessTools.Method(typeof(ModLister), "EnsureInit"));
            if (!(bool)Static(typeof(ModLister), "modListBuilt", typeof(bool))!
                || (bool)Static(typeof(ModsConfig), "activeModsInLoadOrderCachedDirty", typeof(bool))!) throw new InvalidDataException("custom-mod-order-not-materialized");
            WriteMods(writer, ExactList<ModMetaData>(Static(typeof(ModsConfig), "activeModsInLoadOrderCached", typeof(List<ModMetaData>))));
        }
    }

    private static void WriteMods(BinaryWriter writer, List<ModMetaData> mods)
    {
        writer.Write(mods.Count);
        FieldInfo metaField = AccessTools.DeclaredField(typeof(ModMetaData), "meta");
        if (metaField == null) throw new InvalidDataException("custom-metadata-field");
        foreach (ModMetaData mod in mods)
        {
            if (mod == null || mod.GetType() != typeof(ModMetaData)) throw new InvalidDataException("custom-metadata-type");
            object meta = metaField.GetValue(mod) ?? throw new InvalidDataException("custom-metadata-null");
            if (meta.GetType() != metaField.FieldType) throw new InvalidDataException("custom-metadata-subclass");
            Text(writer, (string?)Field(meta.GetType(), "name", typeof(string)).GetValue(meta));
            Text(writer, (string?)Field(meta.GetType(), "packageId", typeof(string)).GetValue(meta));
            Text(writer, (string?)Field(typeof(ModMetaData), "packageIdLowerCase", typeof(string)).GetValue(mod));
            writer.Write((bool)Field(typeof(ModMetaData), "appendPackageIdSteamPostfix", typeof(bool)).GetValue(mod)!);
        }
    }

    private void Guard(MethodBase? method)
    {
        if (method == null) throw new InvalidDataException("custom-method-missing");
        if (guards.TryGetValue(method, out var existing)) { if (!existing.AllowsOriginalContract()) throw new InvalidDataException("custom-method-hook"); return; }
        string key = XmlSemanticContracts.Key(method);
        if (!ExpectedBodies.TryGetValue(key, out string expected) || !SemanticMethodIdentity.TryHash(method, out string actual, out _) || actual != expected)
            throw new InvalidDataException("custom-method-identity:" + key);
        if (!PublishedPatchGuard.TryCreate(method, "wakeup.processed-xml", out var guard, true) || !guard!.AllowsOriginalContract()) throw new InvalidDataException("custom-method-hook:" + key);
        guards.Add(method, guard);
    }
    private static FieldInfo? FindField(Type type, string? name)
    { if (name == null) return null; for (Type? t = type; t != null; t = t.BaseType) { var f = t.GetField(name, Declared | BindingFlags.Static); if (f != null) return f; } return null; }
    private static FieldInfo Field(Type type, string name, Type expected)
    { var field = type.GetField(name, Declared); if (field == null || field.FieldType != expected) throw new InvalidDataException("custom-field:" + type.FullName + "." + name); return field; }
    private static object? Static(Type type, string name, Type expected)
    { var field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field == null || field.FieldType != expected) throw new InvalidDataException("custom-static-field:" + name); return field.GetValue(null); }
    private static object SettingsObject(Assembly assembly, string owner, Type settings)
    { object? result = Static(assembly.GetType(owner, true)!, "settings", settings); if (result?.GetType() != settings) throw new InvalidDataException("custom-settings-instance"); return result; }
    private static List<T> ExactList<T>(object? value)
    { if (value?.GetType() != typeof(List<T>)) throw new InvalidDataException("custom-list-type"); var list = (List<T>)value; if (list.Count > MaximumItems) throw new InvalidDataException("custom-list-limit"); return list; }
    private static void Text(BinaryWriter writer, string? value)
    { if (value != null && value.Length > MaximumText) throw new InvalidDataException("custom-text-limit"); writer.Write(value != null); if (value != null) writer.Write(value); }
    private static void Container(BinaryWriter writer, object? value)
    {
        writer.Write(value != null); if (value == null) return;
        if (value.GetType() != typeof(XmlContainer)) throw new InvalidDataException("custom-container-type");
        FieldInfo[] fields = typeof(XmlContainer).GetFields(Declared);
        if (fields.Length != 1 || fields[0].Name != "node" || fields[0].FieldType != typeof(XmlNode)) throw new InvalidDataException("custom-container-layout");
        var node = (XmlNode?)fields[0].GetValue(value); writer.Write(node != null); int count = 0; if (node != null) Node(writer, node, 0, ref count);
    }
    private static void Node(BinaryWriter writer, XmlNode node, int depth, ref int count)
    {
        if (depth >= 64 || ++count > MaximumItems || node.OwnerDocument?.GetType() != typeof(XmlDocument) || node.OwnerDocument.DocumentType != null) throw new InvalidDataException("custom-xml-boundary");
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
            default: throw new InvalidDataException("custom-xml-kind");
        }
        if (node.GetType() != expected) throw new InvalidDataException("custom-xml-type");
        writer.Write((int)node.NodeType); Text(writer, node.Prefix); Text(writer, node.LocalName); Text(writer, node.NamespaceURI); Text(writer, node.Value);
        writer.Write(node is XmlElement element && element.IsEmpty); writer.Write(node is XmlAttribute attribute && attribute.Specified);
        writer.Write(node.Attributes?.Count ?? -1); if (node.Attributes != null) foreach (XmlAttribute item in node.Attributes) Node(writer, item, depth + 1, ref count);
        writer.Write(node.ChildNodes.Count); foreach (XmlNode child in node.ChildNodes) Node(writer, child, depth + 1, ref count);
    }
}
