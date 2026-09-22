// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Public only for the Prepatcher bridge. The native generic helper still owns
// its entry, actual typeof(T), return and any externally installed hooks.
public static class LoadingAttributeRuntime
{
    internal const string RewrittenBody = "880A6256C284EB9CC74C3DB84768A65DC7BDDF276E62C15C91F0730EC97D7660";
    internal const string NativeModule = "51fded79-cd28-4d4d-911c-5949aff4cb21";
    internal const string NativeOperationBody = "A88F8E29E019AAA9B043B44E8803CE4A80431A1163A9A924BDE02F0881BC10D9";
    private static PublishedPatchGuard? operation;
    internal static string Status { get; private set; } = "native-before-initialization";

    internal static MethodInfo NativeOperation => typeof(Attribute).GetMethod(nameof(Attribute.IsDefined),
        new[] { typeof(MemberInfo), typeof(Type), typeof(bool) })!;

    internal static void Initialize()
    {
        operation = null;
        try
        {
            MethodInfo helper = AccessTools.Method(typeof(GenAttribute), "HasAttribute");
            if (!SemanticMethodIdentity.TryHash(helper, out string body, out _) || body != RewrittenBody)
                throw new InvalidOperationException("native-missing-or-changed-prepatch");
            Type[] arguments = helper.GetGenericArguments();
            if (!helper.IsGenericMethodDefinition || arguments.Length != 1
                || arguments[0].GenericParameterAttributes != GenericParameterAttributes.None
                || !arguments[0].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(Attribute) }))
                throw new InvalidOperationException("native-changed-helper-constraint");
            MethodInfo target = NativeOperation;
            if (!NativeOperationMatches(target)) throw new InvalidOperationException("native-changed-attribute-operation");
            if (!PublishedPatchGuard.TryCreate(target, "wakeup.loading-attribute-operation", out operation, allPatchKinds: true))
                throw new InvalidOperationException("native-unavailable-operation-guard");
            Status = operation!.AllowsOriginalContract() ? "prepatched-bounded-boolean" : "native-foreign-operation-hook";
        }
        catch (Exception exception) { operation = null; Status = exception.Message; }
        TypeLookupRuntime.LeafReceipt("reflection-attribute-contract", Status);
    }

    internal static bool NativeOperationMatches(MethodInfo target)
    {
        if (target.Module.ModuleVersionId.ToString() != NativeModule || target.MetadataToken != 0x06001514
            || (int)target.Attributes != 150
            || (target.GetMethodImplementationFlags() & ~MethodImplAttributes.NoInlining) != MethodImplAttributes.IL) return false;
        byte[]? il = target.GetMethodBody()?.GetILAsByteArray();
        if (il == null) return false;
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(il)).Replace("-", "") == NativeOperationBody;
    }

    public static bool IsDefined(MemberInfo member, Type attributeType, bool inherit)
    {
        LoadingReflectionIndex? index = LoadingReflectionRuntime.AttributeIndex;
        if (index == null || operation?.AllowsOriginalContract() != true)
            return Attribute.IsDefined(member, attributeType, inherit);
        return index.IsDefined(member, attributeType, inherit, LoadingReflectionRuntime.Generation);
    }
}
