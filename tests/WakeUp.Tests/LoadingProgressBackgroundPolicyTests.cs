// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class LoadingProgressBackgroundPolicyTests
{
    private sealed class Settings { public bool Deferred; }
    private sealed class Provider { public Settings Settings = new(); }
    private static Provider provider = new();
    private static int stage;
    internal sealed class ModeScope : IDisposable
    {
        private readonly Dictionary<FieldInfo, object?> saved;
        internal ModeScope()
        {
            var type = typeof(LoadingProgressBackgroundPolicy);
            string[] fields = { "resolved", "present", "admitted", "instance", "modSettings", "deferred", "stage", "settingsType", "finishedStage", "sharedBodies", "mapAdmitted", "mapBodies" };
            saved = fields.ToDictionary(n => AccessTools.Field(type, n), n => (object?)AccessTools.Field(type, n).GetValue(null));
            void Set(string name, object value) => AccessTools.Field(type, name).SetValue(null, value);
            provider = new Provider(); stage = 37;
            Set("resolved", true); Set("present", true); Set("admitted", true); Set("mapAdmitted", true);
            Set("instance", AccessTools.Field(typeof(LoadingProgressBackgroundPolicyTests), nameof(provider)));
            Set("modSettings", AccessTools.Field(typeof(Provider), nameof(Provider.Settings)));
            Set("deferred", AccessTools.Field(typeof(Settings), nameof(Settings.Deferred)));
            Set("stage", AccessTools.Field(typeof(LoadingProgressBackgroundPolicyTests), nameof(stage)));
            Set("settingsType", typeof(Settings)); Set("finishedStage", 37);
            Set("sharedBodies", Array.Empty<PublishedPatchGuard>()); Set("mapBodies", Array.Empty<PublishedPatchGuard>());
        }
        internal void Retire(bool setting) { if (setting) provider.Settings.Deferred = true; else stage = 0; }
        public void Dispose() { foreach (var item in saved) item.Key.SetValue(null, item.Value); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static void Target() { }
    private static void Callback() { }
    private sealed class Snapshot
    {
        public readonly int Value;
        [MethodImpl(MethodImplOptions.NoInlining)] public Snapshot(int value) { Value = value + 19; }
    }

    [Test]
    public void ConstructorBodyIdentityAndLateHookRemainGuarded()
    {
        var constructor = typeof(Snapshot).GetConstructor(new[] { typeof(int) })!;
        Assert.That(SupplierBodyIdentity.Matches(constructor), Is.True);
        Assert.That(PublishedPatchGuard.TryCreate(constructor, "WakeUp.Tests.ConstructorOwner", out var guard, true), Is.True);
        Assert.That(guard!.AllowsOriginalContract(), Is.True);
        var foreign = new Harmony("WakeUp.Tests.ConstructorChange");
        try
        {
            foreign.Patch(constructor, postfix: new HarmonyMethod(typeof(LoadingProgressBackgroundPolicyTests), nameof(Callback)));
            Assert.That(guard.AllowsOriginalContract(), Is.False);
        }
        finally { foreign.UnpatchAll(foreign.Id); }
    }

    [Test]
    public void LiveModeAndStageGateAcquisitionWithoutRefusingOrdinaryStartup()
    {
        var type = typeof(LoadingProgressBackgroundPolicy);
        string[] fields = { "resolved", "present", "admitted", "instance", "modSettings", "deferred", "stage", "settingsType", "finishedStage", "sharedBodies" };
        var saved = fields.ToDictionary(n => AccessTools.Field(type, n), n => AccessTools.Field(type, n).GetValue(null));
        void Set(string name, object value) => AccessTools.Field(type, name).SetValue(null, value);
        try
        {
            provider = new Provider(); stage = 0;
            Set("resolved", true); Set("present", true); Set("admitted", true);
            Set("instance", AccessTools.Field(typeof(LoadingProgressBackgroundPolicyTests), nameof(provider)));
            Set("modSettings", AccessTools.Field(typeof(Provider), nameof(Provider.Settings)));
            Set("deferred", AccessTools.Field(typeof(Settings), nameof(Settings.Deferred)));
            Set("stage", AccessTools.Field(typeof(LoadingProgressBackgroundPolicyTests), nameof(stage)));
            Set("settingsType", typeof(Settings)); Set("finishedStage", 37);
            Set("sharedBodies", Array.Empty<PublishedPatchGuard>());
            Assert.That(LoadingProgressBackgroundPolicy.Unchanged(false), Is.True, "Ordinary startup may install but cannot acquire yet.");
            Assert.That(LoadingProgressBackgroundPolicy.CanAcquire(), Is.False);
            stage = 37;
            Assert.That(LoadingProgressBackgroundPolicy.CanAcquire(), Is.True);
            Assert.That(LoadingProgressBackgroundPolicy.Unchanged(true), Is.True);
            stage = 0;
            Assert.That(LoadingProgressBackgroundPolicy.Unchanged(true), Is.False, "An active lease must retain native dispatcher mode.");
            stage = 37; provider.Settings.Deferred = true;
            Assert.That(LoadingProgressBackgroundPolicy.Unchanged(false), Is.False);
            Assert.That(LoadingProgressBackgroundPolicy.CanAcquire(), Is.False);
            provider.Settings = null!;
            Assert.That(LoadingProgressBackgroundPolicy.CanAcquire(), Is.False);
        }
        finally { foreach (var item in saved) item.Key.SetValue(null, item.Value); }
    }

    [Test]
    public void ExactPublicationRejectsWrongKindOrderingPriorityAndAdditionalOwnerCallback()
    {
        var target = AccessTools.Method(typeof(LoadingProgressBackgroundPolicyTests), nameof(Target));
        var callback = AccessTools.Method(typeof(LoadingProgressBackgroundPolicyTests), nameof(Callback));
        var harmony = new Harmony(LoadingProgressCompatibility.PackageId);
        try
        {
            harmony.Patch(target, postfix: new HarmonyMethod(callback));
            Assert.That(LoadingProgressBackgroundPolicy.Exact(target, callback, HarmonyPatchType.Postfix), Is.True);
            Assert.That(LoadingProgressBackgroundPolicy.Exact(target, callback, HarmonyPatchType.Prefix), Is.False);
            harmony.Patch(target, prefix: new HarmonyMethod(callback));
            Assert.That(LoadingProgressBackgroundPolicy.Exact(target, callback, HarmonyPatchType.Postfix), Is.False);
            harmony.UnpatchAll(harmony.Id);
            harmony.Patch(target, postfix: new HarmonyMethod(callback) { priority = Priority.First });
            Assert.That(LoadingProgressBackgroundPolicy.Exact(target, callback, HarmonyPatchType.Postfix), Is.False);
            harmony.UnpatchAll(harmony.Id);
            harmony.Patch(target, postfix: new HarmonyMethod(callback) { after = new[] { "unexpected" } });
            Assert.That(LoadingProgressBackgroundPolicy.Exact(target, callback, HarmonyPatchType.Postfix), Is.False);
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }
}
