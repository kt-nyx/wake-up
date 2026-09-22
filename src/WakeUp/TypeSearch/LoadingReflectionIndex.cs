// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace WakeUp;

// Metadata only: callers still invoke every getter, setter, constructor and
// custom XML loader. A cache never owns deserialized objects or attributes.
internal sealed class LoadingReflectionIndex
{
    private readonly object gate = new();
    private readonly Dictionary<(Type, string, BindingFlags, int), MemberInfo?> members = new();
    private readonly Dictionary<(Type, BindingFlags), FieldInfo[]> fields = new();
    private readonly Dictionary<(MemberInfo, Type, bool), bool> attributes = new();
    private int generation = -1;
    internal long Hits, Misses, FieldHits, MethodHits, PropertyHits, FieldArrayHits, AttributeHits, AttributeMisses;
    internal int Count { get { lock (gate) return members.Count + fields.Count + attributes.Count; } }

    internal static bool NativeType(Type? type)
        => type != null && type.GetType() == typeof(object).GetType()
            && !type.Assembly.IsDynamic && !type.ContainsGenericParameters;

    internal static bool NativeAttributeQuery(MemberInfo? member, Type? attributeType)
    {
        // Check concrete runtime types before invoking virtual metadata APIs.
        // Custom MemberInfo/Type implementations must retain all their callbacks.
        if (!NativeType(attributeType) || !typeof(Attribute).IsAssignableFrom(attributeType)) return false;
        if (member is Type type) return NativeType(type);
        if (member == null || member.GetType().Assembly != typeof(object).Assembly) return false;
        if (!NativeType(member.DeclaringType) || !NativeType(member.ReflectedType)) return false;
        return !(member is MethodBase method) || !method.ContainsGenericParameters;
    }

    internal bool IsDefined(MemberInfo member, Type attributeType, bool inherit, int currentGeneration)
    {
        if (!NativeAttributeQuery(member, attributeType)) return Attribute.IsDefined(member, attributeType, inherit);
        var key = (member, attributeType, inherit);
        lock (gate)
        {
            ObserveGeneration(currentGeneration);
            if (attributes.TryGetValue(key, out bool found))
            {
                Hits++; AttributeHits++;
                return found;
            }
        }
        // Only the boolean is retained; no constructed attribute or user object.
        // Native work and its errors remain outside the cache lock.
        bool value = Attribute.IsDefined(member, attributeType, inherit);
        lock (gate)
        {
            if (generation == currentGeneration && attributes.Count < 32768) attributes[key] = value;
            Misses++; AttributeMisses++;
        }
        return value;
    }

    internal FieldInfo? Field(Type type, string name, BindingFlags flags, int currentGeneration)
        => (FieldInfo?)Member(type, name, flags, 0, currentGeneration, () => type.GetField(name, flags));
    internal MethodInfo? Method(Type type, string name, BindingFlags flags, int currentGeneration)
        => (MethodInfo?)Member(type, name, flags, 1, currentGeneration, () => type.GetMethod(name, flags));
    internal PropertyInfo? Property(Type type, string name, BindingFlags flags, int currentGeneration)
        => (PropertyInfo?)Member(type, name, flags, 2, currentGeneration, () => type.GetProperty(name, flags));

    private MemberInfo? Member(Type type, string name, BindingFlags flags, int kind, int currentGeneration, Func<MemberInfo?> resolve)
    {
        // Exact strings deliberately remain distinct even for IgnoreCase; this
        // retains native culture/binding behavior instead of normalizing names.
        if (!NativeType(type) || name == null) return resolve();
        var key = (type, name, flags, kind);
        lock (gate)
        {
            ObserveGeneration(currentGeneration);
            if (members.TryGetValue(key, out MemberInfo? found))
            {
                Hits++;
                if (kind == 0) FieldHits++; else if (kind == 1) MethodHits++; else PropertyHits++;
                return found;
            }
        }
        MemberInfo? value = resolve(); // Native exceptions are never cached or swallowed.
        lock (gate)
        {
            if (generation == currentGeneration && members.Count < 32768) members[key] = value;
            Misses++;
        }
        return value;
    }

    internal FieldInfo[] Fields(Type type, BindingFlags flags, int currentGeneration)
    {
        if (!NativeType(type)) return type.GetFields(flags);
        var key = (type, flags);
        lock (gate)
        {
            ObserveGeneration(currentGeneration);
            if (fields.TryGetValue(key, out FieldInfo[]? found))
            {
                Hits++; FieldArrayHits++;
                return (FieldInfo[])found.Clone();
            }
        }
        FieldInfo[] value = type.GetFields(flags);
        lock (gate)
        {
            if (generation == currentGeneration && fields.Count < 4096) fields[key] = (FieldInfo[])value.Clone();
            Misses++;
        }
        return value;
    }

    private void ObserveGeneration(int value)
    {
        if (generation == value) return;
        members.Clear(); fields.Clear(); attributes.Clear(); generation = value;
    }

    internal void Clear()
    {
        lock (gate) { members.Clear(); fields.Clear(); attributes.Clear(); generation = -1; }
    }
}
