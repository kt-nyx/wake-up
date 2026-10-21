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

namespace WakeUp.V17.Tests;

public sealed class TranslationPredicateTests
{
    private sealed class Entry { public bool injected; public string? normalizedPath; }
    private readonly Harmony harmony = new("test.wakeup.translation-predicate");
    private static readonly MethodInfo Target = typeof(TranslationPredicateTests).GetMethod(nameof(Find), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static int applications;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? Find(Dictionary<string, Entry> rows, string ownKey, string? target, out string? normalized)
    {
        normalized = target;
        foreach (var row in rows)
            if (!(row.Key == ownKey) && row.Value.injected && row.Value.normalizedPath == normalized)
                return row.Key;
        return null;
    }
    private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> input, ILGenerator generator)
    {
        var code = input.ToArray();
        IEnumerable<CodeInstruction> result;
        try { result = TranslationPredicateRewrite.Apply(code, generator,
            typeof(Entry).GetField(nameof(Entry.injected))!, typeof(Entry).GetField(nameof(Entry.normalizedPath))!); }
        catch { TestContext.Out.WriteLine(string.Join("\n", code.Select((c, i) => i + ": " + c))); throw; }
        applications++; return result;
    }
    private void Patch() => harmony.Patch(Target, transpiler: new HarmonyMethod(typeof(TranslationPredicateTests), nameof(Rewrite)));
    [TearDown] public void Cleanup() => harmony.UnpatchAll(harmony.Id);
    private static (string? match, Type? error) Observe(Dictionary<string, Entry> rows, string own, string? path)
    {
        try { return (Find(rows, own, path, out _), null); }
        catch (Exception error) { return (null, error.GetType()); }
    }
    [Test]
    public void LiveRewritePreservesOrderAliasesNullShortCircuitsAndOrdinalPaths()
    {
        var shared = new Entry { injected = true, normalizedPath = "Target.label" };
        var cases = new List<(Dictionary<string, Entry> rows, string own, string? path)>();
        foreach (bool injected in new[] { false, true })
        foreach (string? stored in new[] { null, "Target.label", "target.label", "Other.label" })
        foreach (string? query in new[] { null, "Target.label", "target.label" })
        foreach (string own in new[] { "first", "second", "absent" })
            cases.Add((new() { ["first"] = new() { injected = injected, normalizedPath = stored }, ["second"] = shared, ["alias"] = shared }, own, query));
        cases.Add((new() { ["self"] = null!, ["second"] = shared }, "self", "Target.label"));
        cases.Add((new() { ["other"] = null!, ["second"] = shared }, "self", "Target.label"));
        var expected = cases.Select(c => Observe(c.rows, c.own, c.path)).ToArray();
        int before = applications; Patch();
        Assert.That(applications, Is.GreaterThan(before));
        Assert.That(cases.Select(c => Observe(c.rows, c.own, c.path)).ToArray(), Is.EqualTo(expected));
        Assert.That(expected[^2].match, Is.EqualTo("second"));
        Assert.That(expected[^1].error, Is.EqualTo(typeof(NullReferenceException)));
    }
    [Test]
    public void ChangesBetweenCallsRemainLiveWithoutInvalidationOrCacheState()
    {
        Patch();
        var rows = new Dictionary<string, Entry> { ["first"] = new() { injected = false, normalizedPath = "n" }, ["second"] = new() { injected = true, normalizedPath = "n" } };
        Assert.That(Observe(rows, "own", "n").match, Is.EqualTo("second"));
        rows["first"].injected = true;
        Assert.That(Observe(rows, "own", "n").match, Is.EqualTo("first"));
        rows["first"].normalizedPath = "changed";
        rows["second"] = new() { injected = false, normalizedPath = "n" };
        Assert.That(Observe(rows, "own", "n").match, Is.Null);
        rows.Remove("second"); rows["third"] = new() { injected = true, normalizedPath = "n" };
        Assert.That(Observe(rows, "own", "n").match, Is.EqualTo("third"));
    }
    [Test]
    public void UnknownBodyShapeIsRejected()
    {
        var generator = new DynamicMethod("shape", typeof(void), Type.EmptyTypes).GetILGenerator();
        Assert.Throws<InvalidOperationException>((Action)(() => { TranslationPredicateRewrite.Apply(new[] { new CodeInstruction(OpCodes.Ret) }, generator,
            typeof(Entry).GetField(nameof(Entry.injected))!, typeof(Entry).GetField(nameof(Entry.normalizedPath))!); }));
    }
}
