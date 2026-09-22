// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class PreparedAudioRegistryTests
{
    private static (object Instance, Dictionary<string, object> State, MethodInfo Mutate) Registry()
    {
        Type type = typeof(Doorstop.Entrypoint).Assembly.GetType("Doorstop.AudioRegistry", true)!;
        object instance = Activator.CreateInstance(type, true)!;
        var state = (Dictionary<string, object>)type.GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
        return (instance, state, type.GetMethod("BeforeMutation", BindingFlags.Instance | BindingFlags.NonPublic)!);
    }
    private static readonly MethodInfo Target = typeof(PreparedAudioRuntime).GetMethod(nameof(PreparedAudioRuntime.CreateReader))!;

    private static bool ObserveAssembly(object registry, Assembly assembly, bool original = false, bool replacement = false)
        => (bool)registry.GetType().GetMethod("ObserveHarmonyAssembly", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(registry, new object[] { assembly, original, replacement })!;

    [Test]
    public void ActualLateHarmonyLoadSettlesPendingBeforeLoadingCallerContinues()
    {
        var registry = Registry();
        Assembly original = typeof(HarmonyLib.Harmony).Assembly;
        Assert.That(ObserveAssembly(registry.Instance, original, original: true), Is.True);
        byte[] bytes = File.ReadAllBytes(original.Location);
        registry.State["audioAdmissionClosed"] = false;
        var pending = (Dictionary<object, Func<int>>)registry.State["audioPending"];
        bool settled = false, closedDuringSettlement = false, observed = false;
        pending.Add(new object(), () =>
        {
            closedDuringSettlement = (bool)registry.State["audioAdmissionClosed"] && (bool)registry.State["audioNativeOnly"];
            settled = true;
            return 1;
        });
        AssemblyLoadEventHandler handler = (_, args) =>
        {
            if (args.LoadedAssembly.GetName().Name != "0Harmony") return;
            observed = true;
            ObserveAssembly(registry.Instance, args.LoadedAssembly);
        };
        Assembly loaded;
        AppDomain.CurrentDomain.AssemblyLoad += handler;
        try { loaded = Assembly.Load(bytes); }
        finally { AppDomain.CurrentDomain.AssemblyLoad -= handler; }
        Assert.Multiple((Action)(() =>
        {
            Assert.That(loaded, Is.Not.SameAs(original));
            Assert.That(loaded.ManifestModule.ModuleVersionId, Is.EqualTo(original.ManifestModule.ModuleVersionId), "matching image identity does not authorize another Assembly object");
            Assert.That(observed, Is.True);
            Assert.That(settled, Is.True, "the loading caller must not regain control with its reader pending");
            Assert.That(closedDuringSettlement, Is.True);
            Assert.That(pending, Is.Empty);
            Assert.That(registry.State["audioUnexpectedHarmonyCopies"], Is.EqualTo(1));
            Assert.That(registry.State["audioEpoch"], Is.EqualTo(1L));
        }));
    }

    [Test]
    public void OnlyExactOriginalAndOneClosedAdmissionReplacementAreAllowed()
    {
        var registry = Registry();
        Assembly original = typeof(HarmonyLib.Harmony).Assembly;
        byte[] bytes = File.ReadAllBytes(original.Location);
        Assembly replacement = Assembly.Load(bytes);
        Assembly extra = Assembly.Load(bytes);
        Assert.That(ObserveAssembly(registry.Instance, original, original: true), Is.True);
        Assert.That(ObserveAssembly(registry.Instance, replacement, replacement: true), Is.True);
        Assert.That(registry.State["audioReplacementHarmony"], Is.SameAs(replacement));
        registry.State["audioAdmissionClosed"] = false;
        Assert.That(ObserveAssembly(registry.Instance, original), Is.True);
        Assert.That(ObserveAssembly(registry.Instance, replacement), Is.True);
        // Even a caller presenting the same replacement marker/protocol cannot
        // authorize a third assembly or reopen admission after refusal.
        Assert.That(ObserveAssembly(registry.Instance, extra, replacement: true), Is.False);
        Assert.That(ObserveAssembly(registry.Instance, extra, replacement: true), Is.False);
        Assert.That(registry.State["audioNativeOnly"], Is.True);
        Assert.That(registry.State["audioAdmissionClosed"], Is.True);
        Assert.That(registry.State["audioReplacementHarmony"], Is.SameAs(replacement));
    }

    [Test]
    public void UnexpectedCopyBeforeReplacementMakesRefusalPermanent()
    {
        var registry = Registry();
        Assembly original = typeof(HarmonyLib.Harmony).Assembly;
        Assembly extra = Assembly.Load(File.ReadAllBytes(original.Location));
        Assert.That(ObserveAssembly(registry.Instance, original, original: true), Is.True);
        Assert.That(ObserveAssembly(registry.Instance, extra), Is.False);
        Assert.That(ObserveAssembly(registry.Instance, extra, replacement: true), Is.False);
        Assert.That(registry.State["audioReplacementHarmony"], Is.Null);
        Assert.That(registry.State["audioNativeOnly"], Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConcurrentMutationCannotPassAStillRunningSettlement(bool nativeEntry)
    {
        var registry = Registry();
        MethodInfo mutate = nativeEntry ? registry.Instance.GetType().GetMethod("BeforeNativeMutation", BindingFlags.Instance | BindingFlags.NonPublic)! : registry.Mutate;
        object[] arguments = nativeEntry ? new object[] { new IntPtr(19), false } : new object[] { Target };
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        bool stickyBeforeSettlement = false;
        ((Dictionary<object, Func<int>>)registry.State["audioPending"]).Add(new object(), () =>
        {
            stickyBeforeSettlement = (bool)registry.State["audioNativeOnly"];
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("test settlement was not released");
            return 1;
        });
        Task first = Task.Factory.StartNew(() => mutate.Invoke(registry.Instance, arguments), TaskCreationOptions.LongRunning);
        Task? second = null;
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            second = Task.Factory.StartNew(() =>
            {
                secondEntered.Set();
                mutate.Invoke(registry.Instance, arguments);
            }, TaskCreationOptions.LongRunning);
            Assert.That(secondEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(second.Wait(100), Is.False, "native-only must still wait for unfinished settlement");
            Assert.That(stickyBeforeSettlement, Is.True);
        }
        finally
        {
            release.Set();
            first.GetAwaiter().GetResult();
            second?.GetAwaiter().GetResult();
        }
        Assert.That((Dictionary<object, Func<int>>)registry.State["audioPending"], Is.Empty);
        Assert.That(registry.State[nativeEntry ? "audioNativeMutationEntries" : "audioMutationEntries"], Is.EqualTo(2));
    }

    [Test]
    public void UnsettledFailureStopsMutationAndRetainsItsPendingOwner()
    {
        var registry = Registry();
        var pending = (Dictionary<object, Func<int>>)registry.State["audioPending"];
        var failure = new InvalidOperationException("unsettled reader");
        object owner = new();
        pending.Add(owner, () => throw failure);
        var thrown = Assert.Throws<TargetInvocationException>((Action)(() => registry.Mutate.Invoke(registry.Instance, new object[] { Target })));
        Assert.That(thrown!.InnerException, Is.SameAs(failure));
        Assert.That(registry.State["audioNativeOnly"], Is.True);
        Assert.That(pending.ContainsKey(owner), Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(4)]
    public void IncompleteOrUnknownOutcomeRetainsOwnerForDiagnosticFreeRecovery(int outcome)
    {
        var registry = Registry();
        var pending = (Dictionary<object, Func<int>>)registry.State["audioPending"];
        object owner = new();
        pending.Add(owner, () => outcome);
        MethodInfo drain = registry.Instance.GetType().GetMethod("DrainNativeEntry", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>((Action)(() => drain.Invoke(registry.Instance, null)));
        Assert.That(error!.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(pending.ContainsKey(owner), Is.True);
        Assert.That(registry.State["audioNativeOnly"], Is.True);
        pending[owner] = () => 1;
        Assert.That(drain.Invoke(registry.Instance, null), Is.True);
        Assert.That(pending, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ZeroIdentityAndForcedKnownIdentityDrainInsteadOfReturningEarly(bool known)
    {
        var registry = Registry();
        var pending = (Dictionary<object, Func<int>>)registry.State["audioPending"];
        registry.State["audioNativeOriginalHarmony"] = known ? new IntPtr(23) : IntPtr.Zero;
        registry.State["audioAdmissionClosed"] = false;
        int calls = 0;
        pending.Add(new object(), () => { calls++; return 1; });
        MethodInfo mutation = registry.Instance.GetType().GetMethod("BeforeNativeMutation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (known)
        {
            mutation.Invoke(registry.Instance, new object[] { new IntPtr(23), false });
            Assert.That(calls, Is.Zero, "A recognized copy keeps its original mutation guard.");
        }
        mutation.Invoke(registry.Instance, new object[] { known ? new IntPtr(23) : IntPtr.Zero, known });
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(pending, Is.Empty);
        Assert.That(registry.State["audioEpoch"], Is.EqualTo(1L));
        Assert.That(registry.State["audioNativeOnly"], Is.True);
    }
}
