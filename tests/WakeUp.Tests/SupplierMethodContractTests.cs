// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class SupplierMethodContractTests
{
    private static Assembly Make(bool updated, int value = 7, bool omit = false, bool wrongSignature = false)
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName("OptionalSupplier") { Version = new Version(updated ? "9.0.0.0" : "1.0.0.0") }, AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("main").DefineType("Supplier.Example", TypeAttributes.Public);
        if (updated)
        {
            var extra = type.DefineMethod("Unrelated", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
            extra.GetILGenerator().Emit(OpCodes.Ldc_I4_0); extra.GetILGenerator().Emit(OpCodes.Ret);
        }
        var helper = type.DefineMethod("Helper", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
        helper.GetILGenerator().Emit(OpCodes.Ldc_I4, value); helper.GetILGenerator().Emit(OpCodes.Ret);
        if (!omit)
        {
            var method = type.DefineMethod("Value", MethodAttributes.Public | MethodAttributes.Static,
                typeof(int), wrongSignature ? new[] { typeof(int) } : Type.EmptyTypes);
            method.GetILGenerator().Emit(OpCodes.Call, helper); method.GetILGenerator().Emit(OpCodes.Ret);
        }
        type.CreateType();
        return assembly;
    }

    private static SupplierMethodContract Contract(Assembly assembly, string name)
    {
        var method = assembly.GetType("Supplier.Example")!.GetMethod(name)!;
        Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
        return new SupplierMethodContract(name, method.DeclaringType!.FullName!, name, SemanticMethodIdentity.Signature(method), hash);
    }

    [Test]
    public void UpdatedVersionModuleAndRenumberedReferencesKeepCompatibleMethods()
    {
        Assembly original = Make(false), updated = Make(true);
        var before = original.GetType("Supplier.Example")!.GetMethod("Value")!;
        var after = updated.GetType("Supplier.Example")!.GetMethod("Value")!;
        Assert.That(updated.ManifestModule.ModuleVersionId, Is.Not.EqualTo(original.ManifestModule.ModuleVersionId));
        Assert.That(after.GetMethodBody()!.GetILAsByteArray(), Is.Not.EqualTo(before.GetMethodBody()!.GetILAsByteArray()));
        var contracts = new[] { Contract(original, "Value"), Contract(original, "Helper") };
        Assert.That(SupplierMethodContract.ResolveAll(updated, contracts).Count, Is.EqualTo(2));
    }

    [Test]
    public void ChangedHelperMissingMethodOrChangedSignatureRefusesTheWholeFeature()
    {
        var original = Make(false);
        var contracts = new[] { Contract(original, "Value"), Contract(original, "Helper") };
        foreach (var updated in new[] { Make(true, value: 8), Make(true, omit: true), Make(true, wrongSignature: true) })
            Assert.Throws<InvalidOperationException>(new Action(() => SupplierMethodContract.ResolveAll(updated, contracts)));
    }

    [Test]
    public void AnIncompatibleFeatureDoesNotPreventAnIndependentFeatureFromResolving()
    {
        var original = Make(false);
        var updated = Make(true, omit: true);
        Assert.Throws<InvalidOperationException>(new Action(() => Contract(original, "Value").Resolve(updated)));
        Assert.That(Contract(original, "Helper").Resolve(updated), Is.Not.Null);
    }
}
