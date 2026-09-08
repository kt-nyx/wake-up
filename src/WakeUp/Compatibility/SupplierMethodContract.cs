// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WakeUp;

// Resolve every required method before installing any interception. Assembly
// versions, MVIDs and metadata row numbers are deliberately not admission gates.
internal sealed class SupplierMethodContract
{
    internal readonly string Role, TypeName, Name, Signature, Body;
    internal SupplierMethodContract(string role, string typeName, string name, string signature, string body)
    {
        Role = role; TypeName = typeName; Name = name; Signature = signature; Body = body;
    }

    internal MethodBase Resolve(Assembly assembly)
    {
        Type? type = assembly.GetType(TypeName, false);
        if (type == null) throw new InvalidOperationException("missing-supplier-type-" + Role);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        MethodBase[] matches = type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags))
            .Where(m => m.Name == Name && SemanticMethodIdentity.Signature(m) == Signature).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("missing-or-ambiguous-supplier-method-" + Role);
        MethodBase method = matches[0];
        if (!SemanticMethodIdentity.TryHash(method, out string hash, out string reason) || hash != Body)
            throw new InvalidOperationException("changed-supplier-method-" + Role + "-" + reason);
        return method;
    }

    internal static Dictionary<string, MethodBase> ResolveAll(Assembly assembly, IEnumerable<SupplierMethodContract> contracts)
        => contracts.ToDictionary(c => c.Role, c => c.Resolve(assembly), StringComparer.Ordinal);
}
