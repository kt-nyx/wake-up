// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections;
using System.Collections.Generic;

namespace WakeUp;

// A proof of empty descendant lists over the game's CURRENT list, not a second
// assembly/type universe. Never enumerate assemblies or retain leaf results.
internal sealed class LeafSubclassIndex
{
    private static readonly Type RuntimeType = typeof(object).GetType();
    private List<Type>? source;
    private IEnumerator? version;
    private HashSet<Type>? ancestors;
    internal int Builds { get; private set; }
    internal int Refusals { get; private set; }

    internal bool IsCurrent(List<Type>? types)
    {
        if (types == null || !ReferenceEquals(source, types) || version == null)
            return false;
        try
        {
            // List's own enumerator checks its modification version, including
            // same-length replacement. No private framework field dependency.
            version.Reset();
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    internal bool TryProveLeaf(Type type, List<Type>? types)
    {
        // Runtime IsSubclassOf has special cases for Object and non-class types.
        // Leave those, custom Type implementations and dynamic types to native.
        if (type == null || type.GetType() != RuntimeType || !type.IsClass
            || type == typeof(object) || type.Assembly.IsDynamic || types == null)
            return false;
        if (!IsCurrent(types))
            Build(types);
        return ancestors != null && IsCurrent(types) && !ancestors.Contains(type);
    }

    private void Build(List<Type> types)
    {
        Clear();
        source = types;
        version = (IEnumerator)types.GetEnumerator();
        Builds++;
        try
        {
            if (types.Count > TypeLookupIndex.MaximumTypes)
                throw new InvalidOperationException("Too many types.");
            var found = new HashSet<Type>();
            int steps = 0;
            foreach (Type type in types)
            {
                if (type == null || type.GetType() != RuntimeType || type.Assembly.IsDynamic)
                    throw new InvalidOperationException("Unstable type metadata.");
                // Do not stop at a missing intermediate ancestor or normalize a
                // constructed generic base to its definition: IsSubclassOf does neither.
                for (Type? parent = type.BaseType; parent != null; parent = parent.BaseType)
                {
                    if (++steps > 2000000 || found.Count >= TypeLookupIndex.MaximumTypes)
                        throw new InvalidOperationException("Too much ancestry.");
                    found.Add(parent);
                }
            }
            if (IsCurrent(types))
                ancestors = found;
        }
        catch { Refusals++; }
    }

    internal void Clear()
    {
        (version as IDisposable)?.Dispose();
        source = null;
        version = null;
        ancestors = null;
    }
}
