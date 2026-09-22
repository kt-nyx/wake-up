// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class LoadingInvocationContractTests
{
    [Test]
    public void RewrittenSupplierCallbackHasReviewedRuntimeIdentity()
    {
        string managed = Environment.GetEnvironmentVariable("WAKE_UP_RIMWORLD_MANAGED_DIR")!;
        string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed))!,
            "Mods", "3535481557", "1.6", "Assemblies", "ilyvion.LoadingProgress.dll");
        // The host's Harmony embeds Cecil privately. Exercise the real public
        // prepatch entry via its actual parameter type without another package.
        var rewrite = typeof(LoadingCallbackPrepatch).GetMethod(nameof(LoadingCallbackPrepatch.RewriteAssembly))!;
        Type moduleType = rewrite.GetParameters()[0].ParameterType;
        using var input = new MemoryStream(File.ReadAllBytes(path));
        var module = moduleType.GetMethod("ReadModule", new[] { typeof(Stream) })!.Invoke(null, new object[] { input })!;
        using var output = new MemoryStream();
        try
        {
            Assert.That(rewrite.Invoke(null, new[] { module }), Is.EqualTo(true));
            moduleType.GetMethod("Write", new[] { typeof(Stream) })!.Invoke(module, new object[] { output });
        }
        finally { ((IDisposable)module).Dispose(); }
        var target = LoadingInvocationObservation.ProviderMethods(Assembly.Load(output.ToArray()))[1];
        Assert.That(SemanticMethodIdentity.TryHash(target, out _, out string reason, out string canonical), Is.True, reason);
        using var sha = SHA256.Create();
        string actual = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(MonoNames(canonical))));
        Assert.That(actual, Is.EqualTo(LoadingCallbackPrepatch.RuntimeBody));
    }
    private static string MonoNames(string canonical) => canonical
        .Replace("System.Private.CoreLib:System.Collections.Generic.HashSet`1", "System.Core:System.Collections.Generic.HashSet`1")
        .Replace("System.Collections:System.Collections.Generic.HashSet`1", "System.Core:System.Collections.Generic.HashSet`1")
        .Replace("System.Linq:System.Linq.Enumerable", "System.Core:System.Linq.Enumerable")
        .Replace("System.Private.CoreLib:", "mscorlib:");
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void OriginalSupplierBodiesHaveReviewedSemanticIdentity(int index)
    {
        string managed = Environment.GetEnvironmentVariable("WAKE_UP_RIMWORLD_MANAGED_DIR")!;
        string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed))!,
            "Mods", "3535481557", "1.6", "Assemblies", "ilyvion.LoadingProgress.dll");
        using var sha = SHA256.Create();
        Assert.That(Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(path))), Is.EqualTo(LoadingInvocationObservation.ProviderAssemblyHash));
        var inMemory = Assembly.Load(File.ReadAllBytes(path));
        Assert.That(inMemory.Location, Is.Empty);
        var target = LoadingInvocationObservation.ProviderMethods(inMemory)[index];
        Assert.That(SemanticMethodIdentity.TryHash(target, out _, out string reason, out string canonical), Is.True, reason);
        // CoreCLR moved framework primitives, HashSet and Enumerable between
        // libraries; the original Cecil references pin the System.Core owners.
        // Product identity uses the Mono/.NET Framework name. No product rule
        // is relaxed: compare the original supplier instructions on this host.
        canonical = MonoNames(canonical);
        string actual = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        Assert.That(actual, Is.EqualTo(LoadingInvocationObservation.ProviderBodies[index]));
        if (index != 1) return;
        var before = PatchProcessor.GetOriginalInstructions(target);
        var after = LoadingInvocationObservation.RewriteCallbacks(before.Select(i => new CodeInstruction(i))).ToList();
        var changed = Enumerable.Range(0, before.Count).Where(i => before[i].opcode != after[i].opcode || !Equals(before[i].operand, after[i].operand)).ToArray();
        Assert.That(changed.Length, Is.EqualTo(1));
        int at = changed.Single();
        Assert.That(before[at].Calls(LoadingInvocationObservation.Invoke), Is.True);
        Assert.That(after[at].Calls(AccessTools.Method(typeof(LoadingInvocationObservation), nameof(LoadingInvocationObservation.ExecuteAction))), Is.True);
        Assert.That(after[at].labels, Is.EqualTo(before[at].labels));
        Assert.That(after[at].blocks, Is.EqualTo(before[at].blocks));
        after[at] = new CodeInstruction(before[at]);
        Assert.That(InstructionComparison.SameInstructions(before, after), Is.True);
    }
}
