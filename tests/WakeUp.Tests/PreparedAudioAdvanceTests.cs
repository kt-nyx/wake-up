// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class PreparedAudioAdvanceTests
{
    [Test]
    public void BoundaryPinsDeriveFromOriginalAndKnownSerializedPrepatchPipeline()
    {
        string[] Hashes(Assembly assembly, string label) => PreparedAudioAdvance.Targets(assembly).Select(method =>
        {
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            TestContext.Out.WriteLine(label + " " + SemanticMethodIdentity.Signature(method) + "|" + hash);
            return hash;
        }).ToArray();
        var native = typeof(Verse.SongDef).Assembly;
        string[] original = Hashes(native, "PREPARED_ADVANCE_ORIGINAL");
        using var module = ModuleDefinition.ReadModule(native.Location);
        MethodDefinition[] Methods() => PreparedAudioAdvance.Targets().Select(target => (MethodDefinition)module.LookupToken(target.MetadataToken)).ToArray();
        string[] before = Methods().Select(AssetRoutingPrepatch.Fingerprint).ToArray();
        AssetRoutingPrepatch.Inject(module, AssetRoutingPrepatch.FindGet(module)!);
        DeferredAudioPrepatch.InjectNative(module, DeferredAudioPrepatch.Targets(module));
        Assert.That(Methods().Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(before), "Existing prepatches must leave these boundary bodies untouched.");
        using var bytes = new MemoryStream();
        module.Write(bytes);
        string[] effective = Hashes(Assembly.Load(bytes.ToArray()), "PREPARED_ADVANCE_EFFECTIVE");
        Assert.That(original, Is.EqualTo(PreparedAudioAdvance.Expected), "Pin the original reference hashes.");
        Assert.That(effective, Is.EqualTo(PreparedAudioAdvance.EffectiveExpected), "Pin only the offline-derived serialized hashes.");
    }

    [Test]
    public void AccessorRedirectionCannotKeepTheOldAmbientContract()
    {
        // RecreateMapSustainers still calls the same getter token if a game's
        // Map.Biome body changes. The accessor itself must be independently pinned.
        var getter = PreparedAudioAdvance.Targets().Single(method => method.DeclaringType == typeof(Verse.Map) && method.Name == "get_Biome");
        Assert.That(PreparedAudioAdvance.Targets().Count(method => method.DeclaringType == typeof(RimWorld.Planet.WorldGrid)
            && method.Name == "get_Item"), Is.EqualTo(1), "Only the substituted PlanetTile overload belongs to this chain.");
        using var module = ModuleDefinition.ReadModule(getter.Module.FullyQualifiedName);
        var native = (MethodDefinition)module.LookupToken(getter.MetadataToken);
        string before = AssetRoutingPrepatch.Fingerprint(native);
        native.Body.Instructions.Clear();
        native.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldnull));
        native.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        Assert.That(AssetRoutingPrepatch.Fingerprint(native), Is.Not.EqualTo(before));
    }

    [Test]
    public void NativeStoredFieldLayoutIsAvailableWithoutCallingItsGetters()
    {
        var fields = new PreparedAudioAdvance.Fields();
        Assert.That(fields.Resolved.FieldType, Is.EqualTo(typeof(System.Collections.Generic.List<Verse.Sound.ResolvedGrain>)));
        Assert.That(fields.TryScope(out _, out _, out _), Is.False, "The managed host is not an active native map-loading worker.");
    }

    [Test]
    public void EventBudgetBoundsTraversalAndCancelsOldMapScopesWithoutRestartingBudget()
    {
        var budget = new PreparedAudioAdvance.SceneBudget();
        var scene = new IdentityOnly(); var firstMap = new IdentityOnly(); var secondMap = new IdentityOnly();
        long first = budget.Begin(scene, firstMap);
        for (int i = 0; i < PreparedAudioAdvance.MaximumDefs; i++) Assert.That(budget.TakeDef(first), Is.True);
        Assert.That(budget.TakeDef(first), Is.False);
        for (int i = 0; i < PreparedAudioAdvance.MaximumSubSounds; i++) Assert.That(budget.TakeSubSound(first), Is.True);
        Assert.That(budget.TakeSubSound(first), Is.False);
        for (int i = 0; i < PreparedAudioAdvance.MaximumGrains; i++) Assert.That(budget.TakeGrain(first), Is.True);
        Assert.That(budget.TakeGrain(first), Is.False);
        var repeatedClip = new IdentityOnly();
        Assert.That(budget.TakeClip(first, repeatedClip, out bool duplicate), Is.True);
        Assert.That(duplicate, Is.False);
        Assert.That(budget.TakeClip(first, repeatedClip, out duplicate), Is.True);
        Assert.That(duplicate, Is.True);
        for (int i = 1; i < PreparedAudioAdvance.MaximumClips; i++)
            Assert.That(budget.TakeClip(first, new IdentityOnly(), out _), Is.True);
        Assert.That(budget.TakeClip(first, new IdentityOnly(), out _), Is.False);

        long second = budget.Begin(scene, secondMap);
        Assert.That(second, Is.GreaterThan(first));
        Assert.That(budget.Matches(scene, firstMap, first), Is.False);
        Assert.That(budget.Matches(scene, secondMap, second), Is.True);
        Assert.That(budget.TakeDef(second), Is.False, "Changing maps inside one event must not bypass the bounded scan.");
        Assert.That(budget.TakeClip(first, repeatedClip, out _), Is.False, "An older scope cannot continue even for a duplicate.");
        Assert.That(budget.TakeClip(second, repeatedClip, out duplicate), Is.True);
        Assert.That(duplicate, Is.True);
        budget.Cancel();
        Assert.That(budget.Matches(scene, secondMap, second), Is.False);
        Assert.That(budget.TakeGrain(second), Is.False);

        var nextScene = new IdentityOnly();
        long next = budget.Begin(nextScene, secondMap);
        Assert.That(budget.TakeDef(next), Is.True);
        Assert.That(budget.TakeClip(next, repeatedClip, out duplicate), Is.True);
        Assert.That(duplicate, Is.False, "A new event receives its own bounded budget.");
        GC.KeepAlive(nextScene);
        GC.KeepAlive(scene);
    }

    private sealed class IdentityOnly
    {
        public override bool Equals(object? other) => throw new InvalidOperationException("No user equality callback is allowed.");
        public override int GetHashCode() => throw new InvalidOperationException("No user hash callback is allowed.");
    }
}
