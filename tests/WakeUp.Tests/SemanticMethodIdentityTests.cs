// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using WakeUp;
namespace WakeUp.Tests;

[TestFixture]
public sealed class SemanticMethodIdentityTests
{
    [Test]
    public void MetadataRenumberingKeepsMeaningButOperandLocalFlagsAndCatchChangesDoNot()
    {
        MethodInfo original = Make(false, "payload", typeof(int), true, typeof(Exception));
        MethodInfo renumbered = Make(true, "payload", typeof(int), true, typeof(Exception));
        Assert.That(original.GetMethodBody()!.GetILAsByteArray()!.SequenceEqual(renumbered.GetMethodBody()!.GetILAsByteArray()!), Is.False);
        Assert.That(Hash(renumbered), Is.EqualTo(Hash(original)));
        Assert.That(Hash(Make(false, "payload", typeof(int), true, typeof(Exception), MethodImplAttributes.NoInlining)), Is.EqualTo(Hash(original)));
        Assert.That(Hash(Make(false, "payload", typeof(int), true, typeof(Exception), MethodImplAttributes.Synchronized)), Is.Not.EqualTo(Hash(original)));
        Assert.That(Hash(Make(false, "changed", typeof(int), true, typeof(Exception))), Is.Not.EqualTo(Hash(original)));
        Assert.That(Hash(Make(false, "payload", typeof(string), true, typeof(Exception))), Is.Not.EqualTo(Hash(original)));
        Assert.That(Hash(Make(false, "payload", typeof(int), false, typeof(Exception))), Is.Not.EqualTo(Hash(original)));
        Assert.That(Hash(Make(false, "payload", typeof(int), true, typeof(InvalidOperationException))), Is.Not.EqualTo(Hash(original)));
    }
    [Test]
    public void RequiredReturnModifiersRemainPartOfResolvedMemberIdentity()
    {
        Assert.That(Hash(CallsModifiedReturn(false)), Is.Not.EqualTo(Hash(CallsModifiedReturn(true))));
    }
    [Test]
    public void OpenGenericDefinitionHasNoCallerInstantiationButGenericTypeSpecsKeepTheirContext()
    {
        MethodInfo open = typeof(NonGenericOwner).GetMethod(nameof(NonGenericOwner.Open))!;
        Assert.That(SemanticMethodIdentity.TryHash(open, out string hash, out string reason, out string canonical), Is.True, reason);
        Assert.That(canonical, Does.Contain("T|mscorlib:System.Collections.Generic.Dictionary`2<!0,!1>"));
        Assert.That(Hash(typeof(GenericOwner<int>).GetMethod(nameof(GenericOwner<int>.Open))!), Is.EqualTo(hash));
        Assert.That(Hash(typeof(GenericOwner<int>).GetMethod(nameof(GenericOwner<int>.Context))!),
            Is.Not.EqualTo(Hash(typeof(GenericOwner<string>).GetMethod(nameof(GenericOwner<string>.Context))!)));
    }
    private static class NonGenericOwner
    {
        public static Type Open() => typeof(Dictionary<,>);
    }
    private static class GenericOwner<T>
    {
        public static Type Open() => typeof(Dictionary<,>);
        public static Type Context() => typeof(List<T>);
    }
    [Test]
    public void FrameworkQueueIdentityPreservesOtherTypesAndSameNamedUserDefinitions()
    {
        var queue = typeof(SemanticMethodIdentityTests).GetMethod(nameof(QueueReturn), BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.That(SemanticMethodIdentity.Signature(queue), Does.Contain("System:System.Collections.Generic.Queue`1<mscorlib:System.Int32>"));
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("UserQueueAssembly"), AssemblyBuilderAccess.Run);
        var builder = assembly.DefineDynamicModule("main").DefineType("System.Collections.Generic.Queue`1", TypeAttributes.Public);
        builder.DefineGenericParameters("T");
        Type userQueue = builder.CreateType()!.MakeGenericType(typeof(int));
        var holder = assembly.GetDynamicModule("main")!.DefineType("Holder", TypeAttributes.Public);
        var userMethod = holder.DefineMethod("QueueReturn", MethodAttributes.Private | MethodAttributes.Static, userQueue, Type.EmptyTypes);
        userMethod.GetILGenerator().Emit(OpCodes.Ldnull);
        userMethod.GetILGenerator().Emit(OpCodes.Ret);
        string userSignature = SemanticMethodIdentity.Signature(holder.CreateType()!.GetMethod("QueueReturn", BindingFlags.Static | BindingFlags.NonPublic)!);
        Assert.That(userSignature, Does.Contain("UserQueueAssembly:System.Collections.Generic.Queue`1<mscorlib:System.Int32>"));
    }
    private static Queue<int> QueueReturn() => new();
    private static MethodInfo CallsModifiedReturn(bool modified)
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("RloModifierTest"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("main").DefineType("Example", TypeAttributes.Public);
        var target = type.DefineMethod("Referenced", MethodAttributes.Public | MethodAttributes.Static);
        target.SetSignature(typeof(int), modified ? new[] { typeof(System.Runtime.InteropServices.InAttribute) } : null,
            null, Type.EmptyTypes, null, null);
        var targetIl = target.GetILGenerator();
        targetIl.Emit(OpCodes.Ldc_I4_0);
        targetIl.Emit(OpCodes.Ret);
        var caller = type.DefineMethod("Caller", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
        var callerIl = caller.GetILGenerator();
        callerIl.Emit(OpCodes.Call, target);
        callerIl.Emit(OpCodes.Ret);
        return type.CreateType()!.GetMethod("Caller")!;
    }

    private static string Hash(MethodBase method)
    {
        Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
        return hash;
    }
    private static MethodInfo Make(bool shift, string text, Type local, bool init, Type caught, MethodImplAttributes flags = MethodImplAttributes.IL)
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("RloSemanticTest"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("main");
        var type = module.DefineType("Example", TypeAttributes.Public);
        if (shift)
        {
            var noise = type.DefineMethod("Noise", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes).GetILGenerator();
            noise.Emit(OpCodes.Ldstr, "metadata row shift");
            noise.Emit(OpCodes.Pop);
            noise.Emit(OpCodes.Ret);
        }
        var method = type.DefineMethod("Target", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        method.InitLocals = init;
        method.SetImplementationFlags(flags);
        var il = method.GetILGenerator();
        il.DeclareLocal(local);
        il.BeginExceptionBlock();
        il.Emit(OpCodes.Ldstr, text);
        il.Emit(OpCodes.Pop);
        il.BeginCatchBlock(caught);
        il.Emit(OpCodes.Pop);
        il.EndExceptionBlock();
        il.Emit(OpCodes.Ret);
        return type.CreateType()!.GetMethod("Target")!;
    }
}
