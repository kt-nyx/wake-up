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
    public void PreserveNativeFallbackAndResolveDiagnostics()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        var targets = ProcessedXmlPrepatch.Targets(module);
        foreach (var target in targets) TestContext.Out.WriteLine("C02_NATIVE " + target.Name + " " + AssetRoutingPrepatch.Fingerprint(target));
        var parsed = ParsedXmlPrepatch.Targets(module);
        ParsedXmlPrepatch.Inject(module, parsed);
        module.Write(Path.Combine(TestContext.CurrentContext.TestDirectory, "processed-xml-native.dll"));
        Assert.That(targets[1].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "CheckForDuplicateNodes"), Is.EqualTo(1));
        Assert.That(targets[1].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "CloneNode"), Is.EqualTo(1));
        Assert.That(targets[1].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "TryRestore"), Is.EqualTo(1));
        Assert.That(targets[1].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "Capture"), Is.EqualTo(1));
        Assert.That(targets[0].Body.Instructions.Count(i => i.Operand is MethodReference m && m.DeclaringType.Name == "ResolvedInheritanceRuntime" && m.Name == "Resolve"), Is.EqualTo(1));
    }
}
