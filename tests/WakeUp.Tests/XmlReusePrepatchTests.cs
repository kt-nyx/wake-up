// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class XmlReusePrepatchTests
{
    [Test]
    public void FrozenCallsitesAndEffectiveContracts()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        var type = module.GetType("Verse.LoadedModManager");
        var load = type.Methods.Single(m => m.Name == "LoadAllActiveMods");
        var apply = type.Methods.Single(m => m.Name == "ApplyPatches");
        string loadHash = AssetRoutingPrepatch.Fingerprint(load), applyHash = AssetRoutingPrepatch.Fingerprint(apply);
        TestContext.Out.WriteLine("LOAD=" + loadHash + "\nAPPLY=" + applyHash);
        XmlReusePrepatch.Inject(module, load, apply);
        var bridge = load.Body.Instructions.Single(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "WakeUp.XmlReuseRuntime");
        Assert.That(((MethodReference)bridge.Operand).Parameters.Select(p => p.ParameterType.FullName), Is.EqualTo(new[] {
            "System.Xml.XmlDocument&", "System.Object&", "System.Object", "System.Boolean" }));
        Assert.That(bridge.Previous.OpCode, Is.EqualTo(OpCodes.Ldarg_0));
        Assert.That(bridge.Previous.Previous.Previous.OpCode, Is.EqualTo(OpCodes.Ldloca));
        Assert.That(bridge.Previous.Previous.Previous.Previous.OpCode, Is.EqualTo(OpCodes.Stloc));
        Assert.That(bridge.Previous.Previous.Previous.Previous.Previous.Previous.OpCode, Is.EqualTo(OpCodes.Ldloca));
        Assert.That(bridge.Next.Next.OpCode, Is.EqualTo(OpCodes.Castclass));
        Assert.That(bridge.Next.Next.Next.OpCode, Is.EqualTo(OpCodes.Stloc));
        Assert.That(apply.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "ApplyOne"), Is.EqualTo(1));
        using var bytes = new MemoryStream(); module.Write(bytes);
        Assembly copy = Assembly.Load(bytes.ToArray());
        var methods = XmlReuseContract.Methods();
        var hashes = methods.Select((m, i) =>
        {
            TestContext.Out.WriteLine("Checking " + m.DeclaringType!.FullName + "." + m.Name);
            TestContext.Out.WriteLine("Original: " + SemanticMethodIdentity.TryHash(m, out string originalHash, out string originalReason) + ":" + originalHash + ":" + originalReason);
            var effective = i < 2 ? copy.GetType(m.DeclaringType!.FullName)!.GetMethod(m.Name)! : m;
            Assert.That(SemanticMethodIdentity.TryHash(effective, out string hash, out string reason, out string canonical), Is.True, reason);
            if (i < 2)
            {
                // The preserved bridge now lives only in WakeUp.Tests. Compare
                // the reviewed historical contract after accounting for that
                // explicit assembly relocation, without changing its instructions.
                canonical = canonical.Replace("WakeUp.Tests:WakeUp.XmlReuseRuntime", "WakeUp:WakeUp.XmlReuseRuntime");
                using var digest = SHA256.Create();
                hash = BitConverter.ToString(digest.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "");
            }
            TestContext.Out.WriteLine(m.DeclaringType!.FullName + "." + m.Name + "=" + hash);
            return hash;
        }).ToArray();
        // The artifact supports updating reviewed constants; it never learns
        // an admission value in the production runtime.
        string? output = Environment.GetEnvironmentVariable("WAKE_UP_XML_CONTRACT_OUTPUT");
        if (output != null) File.WriteAllLines(output, new[] { loadHash, applyHash }.Concat(hashes));
        Assert.That(loadHash, Is.EqualTo(XmlReusePrepatch.LoadBody));
        Assert.That(applyHash, Is.EqualTo(XmlReusePrepatch.ApplyBody));
        Assert.That(hashes, Is.EqualTo(XmlReuseContract.Bodies));
    }

    [Test]
    public void ChangedAndAlreadyRewrittenBodiesAreUntouched()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        var load = module.GetType("Verse.LoadedModManager").Methods.Single(m => m.Name == "LoadAllActiveMods");
        load.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Nop));
        string changed = AssetRoutingPrepatch.Fingerprint(load);
        Assert.That(XmlReusePrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(load), Is.EqualTo(changed));
        load.Body.Instructions.RemoveAt(0);
        Assert.That(XmlReusePrepatch.RewriteAssembly(module), Is.True);
        string injected = AssetRoutingPrepatch.Fingerprint(load);
        Assert.That(XmlReusePrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(load), Is.EqualTo(injected));
    }
}
