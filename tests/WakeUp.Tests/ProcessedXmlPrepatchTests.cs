using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;
using Verse;
namespace WakeUp.Tests;
[TestFixture]
public sealed class ProcessedXmlPrepatchTests
{
    [Test]
    public void FullPrepatchPreservesNativeInheritanceAndRetainsProcessedPatchBridge()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        var targets = ProcessedXmlPrepatch.Targets(module);
        var native = targets.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
        Assert.That(ParsedXmlPrepatch.RewriteAssembly(module), Is.True);
        Assert.That(targets.Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(native), "Retired inheritance bridges must not modify native bodies.");
        Assert.That(targets[1].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "CheckForDuplicateNodes"), Is.EqualTo(1));
        Assert.That(targets[1].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "CloneNode"), Is.EqualTo(1));
        Assert.That(targets.SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "ResolvedInheritanceRuntime"), Is.False);
        Assert.That(targets[0].Body.Instructions.Count(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "Verse.XmlInheritance" && m.Name == "Resolve"), Is.EqualTo(1));
        var all = ParsedXmlPrepatch.Targets(module)[1];
        Assert.That(all.Body.Instructions.Count(i => i.Operand is MethodReference m && m.DeclaringType.Name == "ProcessedXmlRuntime" && m.Name == "Apply"), Is.EqualTo(1));
    }
}
