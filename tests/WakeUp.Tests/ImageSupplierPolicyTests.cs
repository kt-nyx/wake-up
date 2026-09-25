// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ImageSupplierPolicyTests
{
    [MethodImpl(MethodImplOptions.NoInlining)] private static int Target(int value) => value + 1;
    private static void Callback() { }
    [Test]
    public void CallbackAllowanceRejectsWrongKindPriorityOrderingAndDuplicates()
    {
        var target = AccessTools.Method(typeof(ImageSupplierPolicyTests), nameof(Target));
        var callback = AccessTools.Method(typeof(ImageSupplierPolicyTests), nameof(Callback));
        const string owner = "WakeUp.Tests.ImageSupplier";
        var harmony = new Harmony(owner);
        try
        {
            harmony.Patch(target, prefix: new HarmonyMethod(callback));
            Assert.That(ImageSupplierPolicy.ExactPublication(target, callback, owner, true), Is.True);
            Assert.That(ImageSupplierPolicy.ExactPublication(target, callback, owner, false), Is.False);
            Assert.That(ImageSupplierPolicy.ExactPublication(target, callback, owner, true, 1000), Is.False);
            harmony.Patch(target, postfix: new HarmonyMethod(callback));
            Assert.That(ImageSupplierPolicy.ExactPublication(target, callback, owner, true), Is.False);
            harmony.UnpatchAll(owner);
            harmony.Patch(target, prefix: new HarmonyMethod(callback) { before = new[] { "unexpected" } });
            Assert.That(ImageSupplierPolicy.ExactPublication(target, callback, owner, true), Is.False);
        }
        finally { harmony.UnpatchAll(owner); }
    }
}
