// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;

namespace WakeUp.V17;

// The reviewed supplier helpers forward the actual document/expression to one
// ordinary XPath call. This optional bridge changes that call only. Supplier
// parsing, error handling, callbacks and modification ordering remain native.
// A missing or changed supplier does not add a dependency or admit new code.
internal static class OptionalXmlQueries
{
    private const string Mvid = "9678d8ef-bd95-4867-a011-deb9e5198def";
    private const string Hash = "1e64e318c905b94de3ef05186d82e2067933238453d818366eac73d688464b35";
    private static readonly object Gate = new();
    private static readonly Dictionary<Assembly, bool> Reviewed = new();

    internal static IEnumerable<MethodInfo> Find()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != "XmlExtensions" || !AllowsAssembly(assembly)) continue;
            Type? type = assembly.GetType("XmlExtensions.Helpers");
            if (type == null) continue;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (AllowsMethod(method)) yield return method;
        }
    }

    internal static bool AllowsMethod(MethodBase method)
    {
        if (method.DeclaringType?.FullName != "XmlExtensions.Helpers" || !method.IsStatic
            || method.Name != "SelectNodes" && method.Name != "SelectSingleNode"
            || method is not MethodInfo info) return false;
        ParameterInfo[] args = method.GetParameters();
        return args.Length == 3 && args[0].ParameterType == typeof(string) && args[1].ParameterType == typeof(XmlDocument)
            && args[2].ParameterType.FullName == "Verse.PatchOperation"
            && info.ReturnType == (method.Name == "SelectNodes" ? typeof(XmlNodeList) : typeof(XmlNode))
            && AllowsAssembly(method.Module.Assembly);
    }

    private static bool AllowsAssembly(Assembly assembly)
    {
        lock (Gate)
        {
            if (Reviewed.TryGetValue(assembly, out bool result)) return result;
            result = false;
            try
            {
                if (assembly.GetName().Name == "XmlExtensions" && assembly.ManifestModule.ModuleVersionId.ToString() == Mvid
                    && !string.IsNullOrEmpty(assembly.Location))
                {
                    using var file = File.OpenRead(assembly.Location);
                    result = Convert.ToHexString(SHA256.HashData(file)).Equals(Hash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            Reviewed[assembly] = result;
            return result;
        }
    }
}
