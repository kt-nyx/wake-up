// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System.IO;
using System.Linq;
using System.Reflection;
using System;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class LoadingAttributePrepatchTests
{
    private static ModuleDefinition Native() => ModuleDefinition.ReadModule(typeof(GenAttribute).Assembly.Location);

    [Test]
    public void FrozenNativeBodyAndRewrittenRuntimeIdentity()
    {
        using var module = Native();
        var helper = LoadingAttributePrepatch.FindHelper(module)!;
        Assert.That(helper, Is.Not.Null);
        string native = AssetRoutingPrepatch.Fingerprint(helper);
        TestContext.Out.WriteLine("C12 attribute native Cecil fingerprint: " + native);
        LoadingAttributePrepatch.Inject(module, helper);
        using var bytes = new MemoryStream();
        module.Write(bytes);
        Assembly copy = Assembly.Load(bytes.ToArray());
        var rewritten = copy.GetType("Verse.GenAttribute")!.GetMethod("HasAttribute")!;
        Assert.That(SemanticMethodIdentity.TryHash(rewritten, out string hash, out string reason), Is.True, reason);
        TestContext.Out.WriteLine("C12 attribute rewritten runtime semantic fingerprint: " + hash);
        Assert.That(hash, Is.EqualTo(LoadingAttributeRuntime.RewrittenBody));
        Assert.That(native, Is.EqualTo(LoadingAttributePrepatch.OriginalNativeBody));
        // Inspect the fixture corlib as data; do not load another core runtime.
        string managed = Environment.GetEnvironmentVariable("WAKE_UP_RIMWORLD_MANAGED_DIR")!;
        using var corlib = ModuleDefinition.ReadModule(Path.Combine(managed, "mscorlib.dll"));
        var operation = corlib.GetType("System.Attribute").Methods.Single(m => m.Name == "IsDefined"
            && m.Parameters.Count == 3 && m.Parameters[0].ParameterType.FullName == "System.Reflection.MemberInfo");
        using var raw = File.OpenRead(Path.Combine(managed, "mscorlib.dll"));
        using var reader = new BinaryReader(raw);
        raw.Position = 0x3c; int pe = reader.ReadInt32();
        raw.Position = pe + 6; int sections = reader.ReadUInt16();
        raw.Position = pe + 20; int optionalSize = reader.ReadUInt16();
        raw.Position = pe + 24 + optionalSize;
        long offset = -1;
        for (int i = 0; i < sections; i++)
        {
            long row = raw.Position; raw.Position += 8;
            uint virtualSize = reader.ReadUInt32(), address = reader.ReadUInt32(), size = reader.ReadUInt32(), pointer = reader.ReadUInt32();
            if (operation.RVA >= address && operation.RVA < address + Math.Max(virtualSize, size)) offset = pointer + operation.RVA - address;
            raw.Position = row + 40;
        }
        Assert.That(offset, Is.GreaterThan(0));
        raw.Position = offset; byte first = reader.ReadByte(); int codeSize;
        if ((first & 3) == 2) codeSize = first >> 2;
        else
        {
            raw.Position = offset; ushort flags = reader.ReadUInt16(); reader.ReadUInt16(); codeSize = reader.ReadInt32();
            raw.Position = offset + (flags >> 12) * 4;
        }
        using var sha = SHA256.Create();
        string operationHash = BitConverter.ToString(sha.ComputeHash(reader.ReadBytes(codeSize))).Replace("-", "");
        TestContext.Out.WriteLine("C12 native Attribute.IsDefined module=" + corlib.Mvid + " IL=" + operationHash
            + " token=" + operation.MetadataToken + " attributes=" + (int)operation.Attributes + " impl=" + (int)operation.ImplAttributes);
        Assert.That(corlib.Mvid.ToString(), Is.EqualTo(LoadingAttributeRuntime.NativeModule));
        Assert.That(operationHash, Is.EqualTo(LoadingAttributeRuntime.NativeOperationBody));
    }

    [Test]
    public void OnlyInnerCallOperandChangesAndActualTypeAndInheritanceRemainOnStack()
    {
        using var module = Native();
        var helper = LoadingAttributePrepatch.FindHelper(module)!;
        var original = helper.Body.Instructions.ToArray();
        var operands = original.Select(i => i.Operand).ToArray();
        var native = (MethodReference)original[4].Operand;
        int stack = helper.Body.MaxStackSize;
        bool initLocals = helper.Body.InitLocals;
        var callers = module.GetType("Verse.GenAttribute").Methods.Where(m => m.Name == "TryGetAttribute")
            .Concat(module.GetType("Verse.DefInjectionPackage").Methods.Where(m => m.Name == "SetDefFieldAtPath"))
            .Concat(module.GetType("Verse.XmlToObjectUtils").Methods.Where(m => m.Name == "DirectGetFieldByName")).ToArray();
        var callerBodies = callers.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
        Assert.That(callers.Length, Is.GreaterThanOrEqualTo(4));

        Assert.That(LoadingAttributePrepatch.RewriteAssembly(module), Is.True);

        Assert.That(helper.Body.Instructions, Is.EqualTo(original));
        Assert.That(original.Select(i => i.OpCode), Is.EqualTo(new[] {
            OpCodes.Ldarg_0, OpCodes.Ldtoken, OpCodes.Call, OpCodes.Ldc_I4_1, OpCodes.Call, OpCodes.Ret }));
        Assert.That(original[1].Operand, Is.SameAs(helper.GenericParameters[0]));
        Assert.That(original.Where((_, i) => i != 4).Select(i => i.Operand),
            Is.EqualTo(operands.Where((_, i) => i != 4)));
        var bridge = (MethodReference)original[4].Operand;
        Assert.That(bridge.DeclaringType.FullName, Is.EqualTo("WakeUp.LoadingAttributeRuntime"));
        Assert.That(bridge.Name, Is.EqualTo("IsDefined"));
        Assert.That(bridge.HasThis, Is.False);
        Assert.That(bridge.HasGenericParameters, Is.False);
        Assert.That(bridge.ReturnType, Is.SameAs(native.ReturnType));
        Assert.That(bridge.Parameters.Select(p => p.ParameterType), Is.EqualTo(native.Parameters.Select(p => p.ParameterType)));
        Assert.That(bridge.Parameters.Select(p => p.ParameterType.FullName),
            Is.EqualTo(new[] { "System.Reflection.MemberInfo", "System.Type", "System.Boolean" }));
        Assert.That(helper.Body.MaxStackSize, Is.EqualTo(stack));
        Assert.That(helper.Body.InitLocals, Is.EqualTo(initLocals));
        Assert.That(helper.Body.Variables, Is.Empty);
        Assert.That(helper.Body.ExceptionHandlers, Is.Empty);
        Assert.That(callers.Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(callerBodies));
    }

    [Test]
    public void ChangedBodyAndAlreadyRewrittenHelperAreUntouched()
    {
        using var module = Native();
        var helper = LoadingAttributePrepatch.FindHelper(module)!;
        helper.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Nop));
        string changed = AssetRoutingPrepatch.Fingerprint(helper);
        var references = module.AssemblyReferences.ToArray();
        Assert.That(LoadingAttributePrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(helper), Is.EqualTo(changed));
        Assert.That(module.AssemblyReferences, Is.EqualTo(references));
        helper.Body.Instructions.RemoveAt(0);
        Assert.That(LoadingAttributePrepatch.RewriteAssembly(module), Is.True);
        string rewritten = AssetRoutingPrepatch.Fingerprint(helper);
        references = module.AssemblyReferences.ToArray();
        Assert.That(LoadingAttributePrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(helper), Is.EqualTo(rewritten));
        Assert.That(module.AssemblyReferences, Is.EqualTo(references));
    }

    [TestCase("missing")]
    [TestCase("constraint")]
    [TestCase("generic-flags")]
    [TestCase("parameter")]
    [TestCase("duplicate")]
    public void MissingOrChangedHelperContractRefusesWithoutMutation(string change)
    {
        using var module = Native();
        var helper = LoadingAttributePrepatch.FindHelper(module)!;
        switch (change)
        {
            case "missing": helper.Name = "MissingHasAttribute"; break;
            case "constraint": helper.GenericParameters[0].Constraints.Clear(); break;
            case "generic-flags": helper.GenericParameters[0].Attributes = Mono.Cecil.GenericParameterAttributes.ReferenceTypeConstraint; break;
            case "parameter": helper.Parameters[0].ParameterType = module.TypeSystem.Object; break;
            case "duplicate": helper.DeclaringType.Methods.Add(new MethodDefinition("HasAttribute", Mono.Cecil.MethodAttributes.Public, module.TypeSystem.Boolean)); break;
        }
        string body = AssetRoutingPrepatch.Fingerprint(helper);
        var references = module.AssemblyReferences.ToArray();
        Assert.That(LoadingAttributePrepatch.FindHelper(module), Is.Null);
        Assert.That(LoadingAttributePrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(helper), Is.EqualTo(body));
        Assert.That(module.AssemblyReferences, Is.EqualTo(references));
    }
}
