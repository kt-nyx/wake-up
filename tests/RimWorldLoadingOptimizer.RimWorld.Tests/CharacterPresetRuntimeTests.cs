// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using NUnit.Framework;
using RimWorldLoadingOptimizer.RimWorld;
using Verse;

namespace RimWorldLoadingOptimizer.RimWorld.Tests;

[TestFixture]
public sealed class CharacterPresetRuntimeTests
{
    [Test]
    public void SupplierPinRejectsChangedBytesEvenWithUnchangedVersion()
    {
        var mvid = new Guid("3db27871-e2d8-425d-98d1-a758fe85d3bd");
        Assert.That(CharacterPresetRuntime.MatchesSupplierIdentity("1.6.3.3", mvid, CharacterPresetRuntime.SupplierSha256), Is.True);
        Assert.That(CharacterPresetRuntime.MatchesSupplierIdentity("1.6.3.4", mvid, CharacterPresetRuntime.SupplierSha256), Is.False);
        Assert.That(CharacterPresetRuntime.MatchesSupplierIdentity("1.6.3.3", Guid.Empty, CharacterPresetRuntime.SupplierSha256), Is.False);
        Assert.That(CharacterPresetRuntime.MatchesSupplierIdentity("1.6.3.3", mvid, new string('0', 64)), Is.False);
        Assert.That(CharacterPresetRuntime.MatchesSupplierIdentity(null, mvid, CharacterPresetRuntime.SupplierSha256), Is.False);
    }

    private sealed class SamplePreset
    {
        public readonly SortedDictionary<int, string> dicParams = new() { [1] = "first", [2] = "second" };
    }

    [Test]
    public void PresetDigestDetectsChangedValuesAndMissingPresets()
    {
        var preset = new SamplePreset();
        var values = new Dictionary<string, SamplePreset> { ["b"] = preset, ["a"] = new() };
        string initial = CharacterPresetRuntime.PresetDigest(values);
        Assert.That(initial, Has.Length.EqualTo(64));
        Assert.That(CharacterPresetRuntime.PresetDigest(new Dictionary<string, SamplePreset> { ["a"] = values["a"], ["b"] = preset }), Is.EqualTo(initial));
        preset.dicParams[2] = "changed";
        Assert.That(CharacterPresetRuntime.PresetDigest(values), Is.Not.EqualTo(initial));
        values.Remove("b");
        Assert.That(CharacterPresetRuntime.PresetDigest(values), Is.Not.EqualTo(initial));
    }

    [Test]
    public void ScopedReuseKeepsOriginalOrderThenReleasesForNextScope()
    {
        int calls = 0;
        Dictionary<string, ThingDef> Original()
        {
            calls++;
            return new Dictionary<string, ThingDef> { ["turret-b"] = null!, ["turret-a"] = null! };
        }
        var first = new CharacterPresetLookupScope(Original, () => true);
        Dictionary<string, ThingDef> original = first.Get();
        Assert.That(first.Get(), Is.SameAs(original));
        Assert.That(first.Get().Keys.ToArray(), Is.EqualTo(new[] { "turret-b", "turret-a" }));
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(first.Hits, Is.EqualTo(2));
        first.Dispose();
        Assert.That(first.RetainedEntries, Is.Zero);
        Assert.That(original.Count, Is.EqualTo(2), "Disposal must not mutate the original dictionary.");
        using var second = new CharacterPresetLookupScope(Original, () => true);
        Assert.That(second.Get(), Is.Not.SameAs(original));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void ChangedGuardFailsBackToFreshOriginalForRemainderOfScope()
    {
        int calls = 0;
        bool valid = true;
        using var scope = new CharacterPresetLookupScope(() => { calls++; return new Dictionary<string, ThingDef>(); }, () => valid);
        Dictionary<string, ThingDef> initial = scope.Get();
        valid = false;
        Assert.That(scope.Get(), Is.Not.SameAs(initial));
        Assert.That(scope.RetainedEntries, Is.Zero);
        valid = true;
        Assert.That(scope.Get(), Is.Not.SameAs(initial));
        Assert.That(calls, Is.EqualTo(3));
        Assert.That(scope.Fallbacks, Is.EqualTo(2));
    }

    [Test]
    public void OversizedDictionaryIsNotRetainedAndCrossThreadGetsFreshOriginal()
    {
        int calls = 0;
        using var scope = new CharacterPresetLookupScope(() =>
        {
            Interlocked.Increment(ref calls);
            return Enumerable.Range(0, CharacterPresetLookupScope.MaximumEntries + 1)
                .ToDictionary(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => (ThingDef)null!);
        }, () => true);
        Assert.That(scope.Get().Count, Is.EqualTo(CharacterPresetLookupScope.MaximumEntries + 1));
        Assert.That(scope.RetainedEntries, Is.Zero);
        scope.Get();
        var worker = new Thread(() => scope.Get());
        worker.Start(); worker.Join();
        Assert.That(calls, Is.EqualTo(3));
        Assert.That(scope.RetainedEntries, Is.Zero);
    }

    [Test]
    public void OriginalExceptionsPropagateOnceAndGuardExceptionsFallBack()
    {
        int calls = 0;
        var failure = new InvalidOperationException("original failure");
        using (var scope = new CharacterPresetLookupScope(() => { calls++; throw failure; }, () => true))
        {
            Assert.That(Assert.Throws<InvalidOperationException>(new Action(() => { scope.Get(); })), Is.SameAs(failure));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(scope.RetainedEntries, Is.Zero);
        }
        using var refused = new CharacterPresetLookupScope(() => new Dictionary<string, ThingDef>(), () => throw new InvalidOperationException("guard"));
        Assert.That(refused.Get(), Is.Empty);
        Assert.That(refused.Fallbacks, Is.EqualTo(1));
    }

    [Test]
    public void RedirectionChangesOnlyUniqueDictionaryCallAndPreservesItsLabels()
    {
        MethodInfo original = AccessTools.Method(typeof(CharacterPresetRuntimeTests), nameof(OriginalDictionary));
        MethodInfo replacement = AccessTools.Method(typeof(CharacterPresetRuntimeTests), nameof(ReplacementDictionary));
        ILGenerator il = new DynamicMethod("Callsite", typeof(void), Type.EmptyTypes).GetILGenerator();
        Label label = il.DefineLabel();
        var call = new CodeInstruction(OpCodes.Call, original);
        call.labels.Add(label);
        var source = new[] { new CodeInstruction(OpCodes.Nop), call, new CodeInstruction(OpCodes.Ret) };
        IReadOnlyList<CodeInstruction> result = CharacterPresetRuntime.RedirectDictionaryCall(source, original, replacement);
        Assert.That(result.Count, Is.EqualTo(source.Length));
        Assert.That(result[1].operand, Is.EqualTo(replacement));
        Assert.That(result[1].labels, Does.Contain(label));
        Assert.That(source[1].operand, Is.EqualTo(original), "The caller's original instruction stream is untouched.");
        Assert.Throws<InvalidOperationException>(new Action(() => { CharacterPresetRuntime.RedirectDictionaryCall(source.Concat(new[] { call }), original, replacement); }));
    }

    [Test]
    public void ScopePatchSnapshotDetectsLaterForeignPrefixAndRefusesAlreadyPatchedChain()
    {
        const string owner = "Rlo.CharacterPreset.Tests.Own", other = "Rlo.CharacterPreset.Tests.Foreign";
        var own = new Harmony(owner); var foreign = new Harmony(other);
        MethodInfo target = AccessTools.Method(typeof(CharacterPresetRuntimeTests), nameof(SnapshotTarget));
        var prefix = new HarmonyMethod(typeof(CharacterPresetRuntimeTests), nameof(EmptyPrefix));
        try
        {
            own.Patch(target, prefix: prefix);
            Assert.That(CharacterPresetPatchSnapshot.TryCapture(new MethodBase[] { target }, owner, out CharacterPresetPatchSnapshot? snapshot), Is.True);
            Assert.That(snapshot!.Unchanged(), Is.True);
            foreign.Patch(target, prefix: prefix);
            Assert.That(snapshot.Unchanged(), Is.False);
            Assert.That(CharacterPresetPatchSnapshot.TryCapture(new MethodBase[] { target }, owner, out _), Is.False);
        }
        finally { foreign.UnpatchAll(other); own.UnpatchAll(owner); }
    }

    [Test]
    public void PinnedCharacterEditorBodiesMatchPhysicalSupplier()
    {
        string? managed = Environment.GetEnvironmentVariable("RLO_RIMWORLD_MANAGED_DIR");
        if (string.IsNullOrEmpty(managed)) Assert.Ignore("Select pinned fixture references to validate the optional Character Editor supplier.");
        string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed!))!, "Mods", "1874644848", "v1.6", "Assemblies", "CharacterEditor.dll");
        if (!File.Exists(path)) Assert.Ignore("Character Editor is not available beside these game references.");
        Assembly supplier = Assembly.LoadFrom(path);
        Assert.That(CharacterPresetRuntime.ValidateSupplier(supplier, path), Is.True);
        Assert.That(CharacterPresetRuntime.ValidateBodies(supplier, out string reason), Is.True, reason);
    }

    private static Dictionary<string, ThingDef> OriginalDictionary() => new();
    private static Dictionary<string, ThingDef> ReplacementDictionary() => new();
    [MethodImpl(MethodImplOptions.NoInlining)] private static int SnapshotTarget(int value) => value + 1;
    private static void EmptyPrefix() { }
}
