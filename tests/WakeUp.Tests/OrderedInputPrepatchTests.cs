using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture]
public sealed class OrderedInputPrepatchTests
{
    [Test]
    public void SelectorCopiesEveryNativeSelectionInstructionAndBranch()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        var files = ParsedXmlPrepatch.Targets(module)[0];
        Assert.That(AssetRoutingPrepatch.Fingerprint(files), Is.EqualTo(ParsedXmlPrepatch.Bodies[0]));
        var call = files.Body.Instructions.Single(i => i.Operand is GenericInstanceMethod g && g.Name == "ToList"
            && g.GenericArguments.Count == 1 && g.GenericArguments[0].FullName == "System.IO.FileInfo");
        var prefix = files.Body.Instructions.TakeWhile(i => i != call.Next).ToArray();
        var suffix = files.Body.Instructions.Skip(prefix.Length).ToArray();
        var selector = OrderedInputPrepatch.Inject(module, files, call);
        Assert.That(selector.ReturnType.FullName, Is.EqualTo("System.Collections.Generic.List`1<System.IO.FileInfo>"));
        Assert.That(selector.Parameters.Select(p => p.ParameterType.FullName), Is.EqualTo(files.Parameters.Select(p => p.ParameterType.FullName)));
        Assert.That(selector.Body.Variables.Select(v => v.VariableType.FullName), Is.EqualTo(files.Body.Variables.Select(v => v.VariableType.FullName)));
        Assert.That(selector.Body.Instructions.Count, Is.EqualTo(prefix.Length + 1));
        for (int i = 0; i < prefix.Length; i++)
        {
            var native = prefix[i]; var copied = selector.Body.Instructions[i];
            if (native.OpCode == OpCodes.Ldsfld && native.Operand is FieldReference f && f.Name == "EmptyXmlAssetsArray")
            {
                Assert.That(copied.OpCode, Is.EqualTo(OpCodes.Newobj));
                Assert.That(((MethodReference)copied.Operand).DeclaringType.FullName, Is.EqualTo(selector.ReturnType.FullName));
                continue;
            }
            Assert.That(copied.OpCode, Is.EqualTo(native.OpCode), "opcode at " + i);
            switch (native.Operand)
            {
                case Instruction branch:
                    Assert.That(selector.Body.Instructions.IndexOf((Instruction)copied.Operand), Is.EqualTo(Array.IndexOf(prefix, branch)), "branch at " + i); break;
                case VariableDefinition variable:
                    Assert.That(((VariableDefinition)copied.Operand).Index, Is.EqualTo(variable.Index)); break;
                case ParameterDefinition parameter:
                    Assert.That(((ParameterDefinition)copied.Operand).Index, Is.EqualTo(parameter.Index)); break;
                default: Assert.That(copied.Operand, Is.EqualTo(native.Operand), "operand at " + i); break;
            }
        }
        Assert.That(selector.Body.Instructions.Last().OpCode, Is.EqualTo(OpCodes.Ret));
        Assert.That(files.Body.Instructions.Skip(files.Body.Instructions.Count - suffix.Length), Is.EqualTo(suffix));
        Assert.That(files.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "GetFiles"), Is.Zero);
        Assert.That(selector.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "GetFiles"), Is.EqualTo(1));
        Assert.That(selector.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "TryAdd"), Is.EqualTo(1));
        Assert.That(files.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "SelectFiles"), Is.EqualTo(1));
        Assert.That(files.Body.Instructions.Count(i => i.OpCode == OpCodes.Ldsfld && i.Operand is FieldReference f && f.Name == "EmptyXmlAssetsArray"), Is.EqualTo(1));
        using var stream = new MemoryStream(); module.Write(stream); stream.Position = 0;
        using var reread = ModuleDefinition.ReadModule(stream);
        Assert.That(reread.GetType("Verse.DirectXmlLoader").Methods.Count(m => m.Name == OrderedInputPrepatch.SelectorName), Is.EqualTo(1));
        TestContext.Out.WriteLine("ORDERED_SELECTOR_CECIL " + AssetRoutingPrepatch.Fingerprint(selector));
    }

    [Test]
    public void CoordinatedRewriteDoesNotScheduleOrConstructInsideSelector()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedModManager).Assembly.Location);
        Assert.That(ParsedXmlPrepatch.RewriteAssembly(module), Is.True);
        var selector = module.GetType("Verse.DirectXmlLoader").Methods.Single(m => m.Name == OrderedInputPrepatch.SelectorName);
        Assert.That(selector.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>()
            .Any(m => m.DeclaringType.FullName == "Verse.LoadableXmlAsset" || m.DeclaringType.FullName == "System.Threading.Thread" || m.DeclaringType.Namespace == "WakeUp"), Is.False);
        Assert.That(ParsedXmlPrepatch.RewriteAssembly(module), Is.False);
    }
}
