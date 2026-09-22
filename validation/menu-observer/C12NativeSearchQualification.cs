// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HarmonyLib;
using Verse;

namespace FixtureMenuObserver;

// Functional-only confirmation of caches supplied by the actual game. No
// observer patch replaces a resolver or changes Wake-Up admission.
internal static class C12NativeSearchQualification
{
    internal static void Run(string directory)
    {
        bool names = false, delegates = false, unpatched = false;
        string error = "";
        int nameAdded = 0, delegateAdded = 0;
        try
        {
            var nameMethod = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly), new[] { typeof(string), typeof(string) });
            var delegateMethod = AccessTools.Method(typeof(DirectXmlToObject), nameof(DirectXmlToObject.GetObjectFromXmlMethod));
            unpatched = !(Harmony.GetPatchInfo(nameMethod)?.Owners.Any() == true)
                && !(Harmony.GetPatchInfo(delegateMethod)?.Owners.Any() == true);
            var nameCache = (IDictionary)AccessTools.Field(typeof(GenTypes), "typeCache").GetValue(null);
            int before = nameCache.Count;
            Type first = GenTypes.GetTypeInAnyAssembly(typeof(C12NativeSearchMarker).FullName, "C12NativeSearchProbe");
            int once = nameCache.Count;
            Type second = GenTypes.GetTypeInAnyAssembly(typeof(C12NativeSearchMarker).FullName, "C12NativeSearchProbe");
            nameAdded = once - before;
            names = first == typeof(C12NativeSearchMarker) && ReferenceEquals(first, second)
                && nameAdded == 1 && nameCache.Count == once;
            var delegateCache = (IDictionary)AccessTools.Field(typeof(DirectXmlToObject), "objectFromXmlMethods").GetValue(null);
            before = delegateCache.Count;
            var a = DirectXmlToObject.GetObjectFromXmlMethod(typeof(C12NativeSearchMarker));
            once = delegateCache.Count;
            var b = DirectXmlToObject.GetObjectFromXmlMethod(typeof(C12NativeSearchMarker));
            delegateAdded = once - before;
            delegates = ReferenceEquals(a, b) && delegateAdded == 1 && delegateCache.Count == once;
        }
        catch (Exception e) { error = e.ToString(); }
        File.WriteAllText(Path.Combine(directory, "c12-native-search.json"),
            "{\"passed\":" + B(names && delegates && unpatched) + ",\"nativeNameMemo\":" + B(names)
            + ",\"nativeConstructionDelegateMemo\":" + B(delegates) + ",\"unpatchedNativeMethods\":" + B(unpatched)
            + ",\"nameEntriesAdded\":" + nameAdded + ",\"delegateEntriesAdded\":" + delegateAdded
            + ",\"error\":\"" + error.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"}");
    }
    private static string B(bool value) => value ? "true" : "false";
}

public sealed class C12NativeSearchMarker { }
