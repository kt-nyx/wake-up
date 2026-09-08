// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;

namespace RimWorldLoadingOptimizer.RimWorld;

internal sealed class TypeLookupIndex
{
    internal const int MaximumTypes = 250000;
    internal const int MaximumNameCharacters = 16000000;

    private readonly Dictionary<string, Type> full = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> simple = new(StringComparer.Ordinal);
    internal static TypeLookupIndex? Create(IEnumerable<Type> types, ref int count, ref int characters)
    {
        var index = new TypeLookupIndex();
        foreach (Type type in types)
        {
            if (++count > MaximumTypes)
                return null;
            string? fullName = type.FullName;
            string name = type.Name;
            characters += (fullName?.Length ?? 0) + name.Length;
            if (characters > MaximumNameCharacters)
                return null;
            if (fullName != null && !index.full.ContainsKey(fullName))
                index.full.Add(fullName, type);
            if (!index.simple.ContainsKey(name))
                index.simple.Add(name, type);
        }
        return index;
    }
    internal Type? Find(string name, bool fullName) => (fullName ? full : simple).TryGetValue(name, out Type result) ? result : null;
}
