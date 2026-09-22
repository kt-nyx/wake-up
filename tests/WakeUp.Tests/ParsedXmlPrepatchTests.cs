using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture]
public sealed class ParsedXmlPrepatchTests
{
    [Test]
    public void CoordinatedBoundariesKeepNativeFallbackAndExactSelection()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        var methods = ParsedXmlPrepatch.Targets(module);
        for (int i = 0; i < methods.Length; i++)
        {
            TestContext.Out.WriteLine("PARSED_NATIVE " + methods[i].Name + " " + AssetRoutingPrepatch.Fingerprint(methods[i]));
            Assert.That(AssetRoutingPrepatch.Fingerprint(methods[i]), Is.EqualTo(ParsedXmlPrepatch.Bodies[i]));
        }
        StreamingXmlPrepatch.RewriteAssembly(module);
        ParsedXmlPrepatch.Inject(module, methods);
        foreach (var method in methods)
            TestContext.Out.WriteLine("PARSED_CECIL_EFFECTIVE " + method.Name + " " + AssetRoutingPrepatch.Fingerprint(method));
        module.Write(Path.Combine(TestContext.CurrentContext.TestDirectory, "parsed-xml-native.dll"));
        Assert.That(methods[0].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "TryLoadSelected"), Is.EqualTo(1));
        var selector = methods[0].DeclaringType.Methods.Single(m => m.Name == OrderedInputPrepatch.SelectorName);
        Assert.That(selector.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "TryAdd"), Is.EqualTo(1));
        Assert.That(methods[0].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "SelectFiles"), Is.EqualTo(1));
        Assert.That(methods[1].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "LoadSources"), Is.EqualTo(1));
        Assert.That(methods[1].Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "Combine"), Is.EqualTo(1));
    }

    [Test]
    public void ChangedBoundariesRefuseBeforeAnyRewriteInEitherRegistrationOrder()
    {
        foreach (bool streamFirst in new[] { true, false })
        {
            using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
            var methods = ParsedXmlPrepatch.Targets(module);
            methods[1].Body.Instructions.Insert(0, Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Nop));
            var before = methods.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
            Assert.That(ParsedXmlPrepatch.RewriteAssembly(module), Is.False);
            Assert.That(methods.Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(before));
            methods[1].Body.Instructions.RemoveAt(0);
            if (streamFirst) Assert.That(StreamingXmlPrepatch.RewriteAssembly(module), Is.True);
            Assert.That(ParsedXmlPrepatch.RewriteAssembly(module), Is.True);
            if (!streamFirst) Assert.That(StreamingXmlPrepatch.RewriteAssembly(module), Is.True);
            before = methods.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
            Assert.That(ParsedXmlPrepatch.RewriteAssembly(module), Is.False);
            Assert.That(methods.Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(before));
        }
    }
}
