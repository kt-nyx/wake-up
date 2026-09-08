// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NUnit.Framework;
using RimWorldLoadingOptimizer.RimWorld;

namespace RimWorldLoadingOptimizer.RimWorld.Tests;

[TestFixture]
public sealed class TypeLookupRuntimeTests
{
    [Test]
    public void EnumeratorGuardDetectsTheFirstLatePrefixAndCachesStablePublications()
    {
        const string ownId = "Rlo.TypeLookup.Tests.Own";
        const string foreignId = "Rlo.TypeLookup.Tests.Foreign";
        var foreign = new Harmony(foreignId);
        MethodInfo target = AccessTools.Method(typeof(TypeLookupRuntimeTests), nameof(GuardTarget));
        Assert.That(PublishedPatchGuard.TryCreate(target, ownId, out PublishedPatchGuard? guard, allPatchKinds: true), Is.True);
        PublishedPatchGuard activeGuard = guard!;
        try
        {
            Assert.That(activeGuard.AllowsOriginalContract(), Is.True);
            Assert.That(activeGuard.PatchInfoReads, Is.EqualTo(1));
            foreign.Patch(target, prefix: new HarmonyMethod(typeof(TypeLookupRuntimeTests), nameof(SkipWithReplacement)));
            Assert.That(activeGuard.AllowsOriginalContract(), Is.False, "A prefix can change enumeration without loading another assembly.");
            for (int i = 0; i < 100; i++)
                Assert.That(activeGuard.AllowsOriginalContract(), Is.False);
            Assert.That(activeGuard.PatchInfoReads, Is.EqualTo(2));
            foreign.Unpatch(target, HarmonyPatchType.All, foreignId);
            Assert.That(activeGuard.AllowsOriginalContract(), Is.True);
            Assert.That(activeGuard.PatchInfoReads, Is.EqualTo(3));
        }
        finally { foreign.Unpatch(target, HarmonyPatchType.All, foreignId); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type[] GuardTarget() => new[] { typeof(string) };

    private static bool SkipWithReplacement(ref Type[] __result)
    {
        __result = new[] { typeof(int) };
        return false;
    }

    [Test]
    public void OrderedIndexMatchesOriginalFullAndSimpleNamePredicates()
    {
        Type[] types = { typeof(First.Duplicate), typeof(Second.Duplicate), typeof(string), typeof(int) };
        int count = 0, characters = 0;
        TypeLookupIndex index = TypeLookupIndex.Create(types, ref count, ref characters)!;
        foreach (string name in new[] { "Duplicate", typeof(Second.Duplicate).FullName!, "String", "System.String", "Missing", "duplicate" })
        {
            Type? expected = types.FirstOrDefault(t => t.FullName == name) ?? types.FirstOrDefault(t => t.Name == name);
            Assert.That(index.Find(name, true) ?? index.Find(name, false), Is.SameAs(expected));
        }
        Assert.That(count, Is.EqualTo(types.Length));
    }

    [Test]
    public void IndexRefusesItsTypeAndNameMemoryBounds()
    {
        int count = 250000, characters = 0;
        Assert.That(TypeLookupIndex.Create(new[] { typeof(string) }, ref count, ref characters), Is.Null);
        count = 0;
        characters = 16000000;
        Assert.That(TypeLookupIndex.Create(new[] { typeof(string) }, ref count, ref characters), Is.Null);
    }

    [Test]
    public void FallbackRedirectPreservesEveryOtherOriginalInstruction()
    {
        MethodInfo target = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName));
        List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target);
        List<CodeInstruction> changed = TypeLookupRuntime.Transpiler(original).ToList();
        Assert.That(changed.Count, Is.EqualTo(original.Count));
        int[] changes = Enumerable.Range(0, original.Count).Where(i => original[i].opcode != changed[i].opcode
            || !Equals(original[i].operand, changed[i].operand)).ToArray();
        Assert.That(changes.Length, Is.EqualTo(2));
        Assert.That(changes[1], Is.EqualTo(changes[0] + 1));
        Assert.That(changed[changes[0]].opcode, Is.EqualTo(OpCodes.Ldarg_0));
        Assert.That(((MethodInfo)changed[changes[1]].operand).DeclaringType, Is.EqualTo(typeof(TypeLookupRuntime)));
        Assert.That(original.Count(i => i.operand is MethodInfo m && m.Name == "GetType"), Is.GreaterThanOrEqualTo(2));
        List<CodeInstruction> foreign = original.Select(i => new CodeInstruction(i)).ToList();
        foreign.Insert(0, new CodeInstruction(OpCodes.Nop));
        Assert.That(TypeLookupRuntime.Transpiler(foreign).Count(), Is.EqualTo(foreign.Count), "Foreign altered incoming body is left intact.");
    }

    public static class First
    {
        public sealed class Duplicate
        {
        }
    }
    public static class Second
    {
        public sealed class Duplicate
        {
        }
    }
}
