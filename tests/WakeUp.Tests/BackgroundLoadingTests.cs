// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Threading;
using HarmonyLib;
using NUnit.Framework;
using RimWorld;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class BackgroundLoadingTests
{
    [Test]
    public void NativeContractsMatchFrozenBodies()
    {
        var lines = new List<string>();
        foreach (var target in BackgroundLoadingContract.Targets)
        {
            Assert.That(SemanticMethodIdentity.TryHash(target, out string hash, out string reason), Is.True, reason);
            lines.Add(hash + " " + BackgroundLoadingContract.Key(target));
        }
        File.WriteAllLines(Path.Combine(TestContext.CurrentContext.WorkDirectory, "background-contracts.txt"), lines);
        BackgroundLoadingContract.Validate();
    }

    [Test]
    public void NativeWorldContractsMatchFrozenBodies()
    {
        WorldBackgroundLoadingContract.Validate();
    }

    [Test]
    public void NativeColonyContractsMatchFrozenBodies()
    {
        var lines = new List<string>();
        foreach (var target in BackgroundLoadingColonyContract.Targets)
        {
            Assert.That(SemanticMethodIdentity.TryHash(target, out string hash, out string reason), Is.True, reason);
            lines.Add(hash + " " + BackgroundLoadingContract.Key(target));
        }
        File.WriteAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "background-colony-contracts.txt"), lines);
        BackgroundLoadingColonyContract.Validate();
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void NativeColonyEntryKeepsSceneGapMapCallbacksAndFadeThenRestoresLatestPreference(bool latest, bool focused)
    {
        WithHooks(context =>
        {
            PageUtility.InitGameStart();
            Assert.That(BackgroundLoadingRuntime.CheckColonyOwnership(), Is.True, BackgroundLoadingRuntime.ColonyStatus);
            Assert.That(context.Background, Is.True);
            Assert.That(Field(FirstQueued(), "levelToLoad").GetValue(FirstQueued()), Is.EqualTo("Play"));
            var order = new List<string>();
            ReplaceFirstQueuedAction(() => order.Add("entry-preparation"));
            UpdateNative(); UpdateNative(); JoinWorker(); UpdateNative();
            Assert.That(context.Background, Is.True, "Completion of the preparation event must not release before Play arrival.");

            // The production prefix/finalizer bracket the inspected complete
            // Root_Play.Start. Simulate only Unity arrival and its queued content:
            // a native async new-game worker followed by the native sync fade.
            AccessTools.Method(typeof(BackgroundLoadingRuntime), "EnterBoundary")
                .Invoke(null, new object[] { false });
            LongEventHandler.QueueLongEvent(() =>
            {
                order.Add("init-new-game");
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Assert.That(context.Background, Is.True);
                    order.Add("native-pause-on-load");
                    context.Preference = latest;
                    BackgroundLoadingRuntime.SetBackground(latest);
                });
            }, null, true, null);
            Queue(() => { Assert.That(context.Background, Is.True); order.Add("fade"); });
            AccessTools.Method(typeof(BackgroundLoadingRuntime), "AfterPlayStart")
                .Invoke(null, new object[] { true, null! });
            Assert.That(context.Background, Is.True);
            UpdateNative(); UpdateNative(); JoinWorker(); UpdateNative();
            Assert.That(context.Background, Is.True, "The synchronous fade is still queued after map callbacks.");
            UpdateNative();
            Assert.That(order, Is.EqualTo(new[] { "entry-preparation", "init-new-game", "native-pause-on-load", "fade" }));
            Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
            Assert.That(context.Background, Is.EqualTo(latest));
            BackgroundLoadingRuntime.Focused = () => focused;
            Assert.That(BackgroundLoadingRuntime.ShouldBlockPlay(), Is.EqualTo(!latest && !focused));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeColonyCancellationAndRepeatedEntryRestoreCurrentPreference(bool latest)
    {
        WithHooks(context =>
        {
            for (int i = 0; i < 2; i++)
            {
                PageUtility.InitGameStart();
                Assert.That(context.Background, Is.True);
                context.Preference = latest;
                BackgroundLoadingRuntime.SetBackground(latest);
                LongEventHandler.ClearQueuedEvents();
                Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
                Assert.That(context.Background, Is.EqualTo(latest));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeColonyWorkerAbortPreservesErrorHandlerAndRestoresCurrentPreference(bool latest)
    {
        WithHooks(context =>
        {
            PageUtility.InitGameStart();
            bool handled = false;
            var failure = new InvalidOperationException("expected map initialization failure");
            // Use native worker error dispatch. Replace the engine-only error
            // dialog/menu transition with its observed cancellation boundary.
            ReplaceFirstQueuedAction(() => throw failure);
            Field(FirstQueued(), "exceptionHandler").SetValue(FirstQueued(), (Action<Exception>)(e =>
            {
                handled = ReferenceEquals(e, failure);
                AccessTools.Method(typeof(BackgroundLoadingRuntime), "CancelSceneWait").Invoke(null, null);
            }));
            context.Preference = latest;
            UpdateNative(); UpdateNative(); JoinWorker(); UpdateNative();
            Assert.That(handled, Is.True);
            Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
            Assert.That(context.Background, Is.EqualTo(latest));
        });
    }

    [Test]
    public void ColonyPageAdmissionRefusesUnrelatedQueueAndUnloadedOrWorkerEntry()
    {
        WithHooks(context =>
        {
            Queue(() => { });
            PageUtility.InitGameStart();
            Assert.That(context.Writes, Is.Zero);
            LongEventHandler.ClearQueuedEvents();
            BackgroundLoadingRuntime.Loaded = () => false;
            PageUtility.InitGameStart();
            Assert.That(context.Writes, Is.Zero);
            LongEventHandler.ClearQueuedEvents();
            BackgroundLoadingRuntime.Loaded = () => true;
            var worker = new Thread(PageUtility.InitGameStart);
            worker.Start();
            Assert.That(worker.Join(5000), Is.True);
            Assert.That(context.Writes, Is.Zero);
        });
    }

    [Test]
    public void DirectPlayEntryUsesNativeQueueDrainAndCompletionProtection()
    {
        WithHooks(context =>
        {
            AccessTools.Method(typeof(BackgroundLoadingRuntime), "BeforePlayStart")
                .Invoke(null, new object[] { false });
            Assert.That(context.Background, Is.True);
            Queue(() => { });
            AccessTools.Method(typeof(BackgroundLoadingRuntime), "AfterPlayStart")
                .Invoke(null, new object[] { true, null! });
            UpdateNative(); UpdateNative();
            Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
            Assert.That(context.Background, Is.False);
            Assert.That(BackgroundLoadingRuntime.ShouldBlockPlay(), Is.True);
        });
    }

    [Test]
    public void RefusedDirectColonyContractDoesNotAcquirePermission()
    {
        WithHooks(context =>
        {
            var foreign = new Harmony("wakeup.tests.direct-colony");
            try
            {
                foreign.Patch(BackgroundLoadingRuntime.NewColony, prefix: new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(Observe)));
                AccessTools.Method(typeof(BackgroundLoadingRuntime), "BeforePlayStart").Invoke(null, new object[] { false });
                AccessTools.Method(typeof(BackgroundLoadingRuntime), "AfterPlayStart").Invoke(null, new object[] { true, null! });
                Assert.That(context.Writes, Is.Zero);
            }
            finally { foreign.UnpatchAll(foreign.Id); }
        });
    }

    [Test]
    public void NativeStartupObservationDoesNotReleaseNativePermissionOnRefusal()
    {
        WithHooks(context =>
        {
            AccessTools.Field(typeof(BackgroundLoadingRuntime), "nativeStartupPending").SetValue(null, true);
            context.Background = true; // Native Root.Start, no Wake-Up lease.
            var foreign = new Harmony("wakeup.tests.native-startup");
            try
            {
                foreign.Patch(BackgroundLoadingRuntime.Update, prefix: new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(Observe)));
                Assert.That(BackgroundLoadingRuntime.CheckOwnership(), Is.False);
                Assert.That(context.Background, Is.True);
                Assert.That(context.Writes, Is.Zero);
            }
            finally { foreign.UnpatchAll(foreign.Id); }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeStartupKeepsPermissionAcrossPreferenceChangeAndNestedReleaseUntilNativeApply(bool latest)
    {
        WithHooks(context =>
        {
            var nativeFlag = AccessTools.Field(typeof(Root), "prefsApplied");
            object previous = nativeFlag.GetValue(null);
            try
            {
                nativeFlag.SetValue(null, false);
                AccessTools.Field(typeof(BackgroundLoadingRuntime), "nativeStartupPending").SetValue(null, true);
                context.Background = true;
                context.Preference = latest;
                BackgroundLoadingRuntime.SetBackground(latest);
                Assert.That(context.Background, Is.True, "A real preference change cannot revoke initial native permission.");
                var nested = BackgroundLoadingRuntime.Session.Begin(waitForPlay: false, owner: "map");
                BackgroundLoadingRuntime.Session.Release(nested);
                Assert.That(context.Background, Is.True, "Releasing our own nested operation cannot release native startup.");
                Assert.That(LongEventHandler.AnyEventNowOrWaiting, Is.False);
                BackgroundLoadingRuntime.SetBackground(latest);
                Assert.That(context.Background, Is.True, "An idle callback gap is not the native completion boundary.");
                nativeFlag.SetValue(null, true); // Exact native Root.Update order before Prefs.Apply.
                BackgroundLoadingRuntime.SetBackground(latest);
                Assert.That(context.Background, Is.EqualTo(latest));
                Assert.That(BackgroundLoadingRuntime.ShouldBlockPlay(), Is.EqualTo(!latest));
            }
            finally { nativeFlag.SetValue(null, previous); }
        });
    }

    [Test]
    public void NestedLeaseFailureAndRefusalReleaseOnlyTheResponsibleOwner()
    {
        bool background = false, preference = false;
        var session = new BackgroundLoadingSession(() => preference, value => background = value, () => 12);
        session.Begin(owner: "save");
        var map = session.Begin(waitForPlay: false, owner: "map");
        session.Release(map);
        Assert.That(background, Is.True);
        session.Begin(waitForPlay: false, owner: "world");
        session.ReleaseOwner("world");
        Assert.That(background, Is.True);
        preference = true;
        session.ArrivedOrCanceled();
        session.FinishIfIdle(false);
        Assert.That(session.Active, Is.False);
        Assert.That(background, Is.True, "Restore the current preference, not the value at acquisition.");
    }

    [Test]
    public void ScopedMapRefusesUnrelatedQueueAndKeepsNativeSynchronousThread()
    {
        WithHooks(context =>
        {
            // The older Framework host cannot decode the GOG KeyValuePair
            // Deconstruct call. The complete map contract is checked separately
            // on the existing modern host and again inside the real game.
            AccessTools.Field(typeof(BackgroundLoadingRuntime), "mapInstalled").SetValue(null, true);
            var wrapper = AccessTools.Method(typeof(BackgroundLoadingRuntime), "QueueMap");
            Action work = () => { };
            Queue(work);
            wrapper.Invoke(null, new object[] { work, null!, false, null!, true, false, null! });
            Assert.That(context.Writes, Is.Zero, "A known map cannot adopt unrelated pending work.");
            LongEventHandler.ClearQueuedEvents();
            int thread = Thread.CurrentThread.ManagedThreadId, actual = 0;
            work = () => actual = Thread.CurrentThread.ManagedThreadId;
            wrapper.Invoke(null, new object[] { work, null!, false, null!, true, false, null! });
            Assert.That(context.Background, Is.True);
            Assert.That(Field(FirstQueued(), "canEverUseStandardWindow").GetValue(FirstQueued()), Is.False);
            UpdateNative(); UpdateNative();
            Assert.That(actual, Is.EqualTo(thread));
            Assert.That(context.Background, Is.False);
        });
    }

    [Test]
    public void InheritedNativePortalErrorCleansOnlyItsOwnPointerAndRefusesOverrides()
    {
        WithHooks(context =>
        {
            AccessTools.Field(typeof(BackgroundLoadingRuntime), "mapInstalled").SetValue(null, true);
            MapPortal saved = PocketMapUtility.currentlyGeneratingPortal;
            var native = (MapPortal)FormatterServices.GetUninitializedObject(typeof(AncientHatch));
            var overridden = (MapPortal)FormatterServices.GetUninitializedObject(typeof(OverriddenPortal));
            var before = AccessTools.Method(typeof(BackgroundLoadingRuntime), "BeforePortal");
            var after = AccessTools.Method(typeof(BackgroundLoadingRuntime), "AfterPortal");
            try
            {
                object[] state = { native, null! };
                before.Invoke(null, state);
                Assert.That(state[1], Is.SameAs(native), "Native inheritance retains the exact base implementation.");
                PocketMapUtility.currentlyGeneratingPortal = native;
                after.Invoke(null, new object[] { state[1], new InvalidOperationException("native portal failure") });
                Assert.That(PocketMapUtility.currentlyGeneratingPortal, Is.Null);
                PocketMapUtility.currentlyGeneratingPortal = overridden;
                after.Invoke(null, new object[] { state[1], new InvalidOperationException("late failure") });
                Assert.That(PocketMapUtility.currentlyGeneratingPortal, Is.SameAs(overridden));
                state = new object[] { overridden, null! };
                before.Invoke(null, state);
                Assert.That(state[1], Is.Null, "A different implementation retains native fallback.");
            }
            finally { PocketMapUtility.currentlyGeneratingPortal = saved; }
        });
    }
    private sealed class OverriddenPortal : MapPortal
    {
        protected override Map GeneratePocketMapInt() => throw new InvalidOperationException("foreign implementation");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForeignColonyHooksRefuseOrReleaseWithoutDisablingSaveAndWorld(bool late)
    {
        WithHooks(context =>
        {
            if (late) PageUtility.InitGameStart();
            var foreign = new Harmony("wakeup.tests.foreign-colony");
            try
            {
                foreign.Patch(BackgroundLoadingRuntime.NewColony, prefix: new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(Observe)));
                if (!late) BackgroundLoadingRuntime.TryInstallColonyHooks();
                Assert.That(BackgroundLoadingRuntime.CheckColonyOwnership(), Is.False);
                Assert.That(BackgroundLoadingRuntime.CheckOwnership(), Is.True);
                Assert.That(BackgroundLoadingRuntime.CheckWorldOwnership(), Is.True);
                Assert.That(context.Background, Is.False);
                LongEventHandler.ClearQueuedEvents();
                GameDataSaveLoader.LoadGame("save-remains-supported");
                Assert.That(context.Background, Is.True);
            }
            finally { foreign.UnpatchAll(foreign.Id); }
        });
    }

    [Test]
    public void ColonyContractPreservesNativeMapPauseAndFailureWiring()
    {
        MethodInfo initializer = AccessTools.Method(typeof(Game), "InitNewGame");
        MethodInfo pause = BackgroundLoadingColonyContract.Targets.Single(method => method.DeclaringType == typeof(Game)
            && method.Name.StartsWith("<InitNewGame>", StringComparison.Ordinal));
        var body = PatchProcessor.GetOriginalInstructions(initializer);
        Assert.That(body.Any(code => code.Calls(AccessTools.PropertyGetter(typeof(Prefs), "PauseOnLoad"))), Is.True);
        Assert.That(body.Any(code => Equals(code.operand, AccessTools.Field(typeof(GameInitData), "startedFromEntry"))), Is.True);
        Assert.That(body.Any(code => Equals(code.operand, pause)), Is.True);
        var callback = PatchProcessor.GetOriginalInstructions(pause).ToList();
        Assert.That(callback.Any(code => code.Calls(AccessTools.Method(typeof(TickManager), "DoSingleTick"))), Is.True);
        Assert.That(callback.Any(code => code.Calls(AccessTools.PropertySetter(typeof(TickManager), "CurTimeSpeed"))), Is.True);
        Assert.That(Harmony.GetPatchInfo(initializer), Is.Null, "Native initialization and time-speed policy are not replaced.");
        var rootWorker = BackgroundLoadingContract.Targets.Single(method => method.Name.StartsWith("<Start>", StringComparison.Ordinal)
            && method.DeclaringType!.DeclaringType == typeof(Root_Play)
            && PatchProcessor.GetOriginalInstructions(method).Any(code => code.Calls(initializer)));
        Assert.That(rootWorker, Is.Not.Null);
        Assert.That(PatchProcessor.GetOriginalInstructions(BackgroundLoadingRuntime.PlayStart)
            .Any(code => Equals(code.operand, AccessTools.Method(typeof(GameAndMapInitExceptionHandlers), "ErrorWhileGeneratingMap"))), Is.True);
    }

    [Test]
    public void WorldSessionFinishesInEntryButCannotEraseNestedSaveSceneWait()
    {
        bool background = false;
        var session = new BackgroundLoadingSession(() => false, x => background = x, () => 1);
        session.Begin(waitForPlay: false);
        session.FinishIfIdle(false);
        Assert.That(background, Is.False, "World generation has no Play scene arrival.");
        session.Begin();
        session.Begin(waitForPlay: false);
        session.FinishIfIdle(false);
        Assert.That(background, Is.True, "A nested world call cannot clear a save arrival wait.");
        session.ArrivedOrCanceled();
        session.FinishIfIdle(false);
        Assert.That(background, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeWorldPageQueueOwnsDeferredPageRedrawAndTailsWithoutPlayArrival(bool latest)
    {
        WithHooks(context =>
        {
            QueueNativeWorld();
            Assert.That(context.Background, Is.True);
            var order = new List<string>();
            // Keep the actual CanDoNext gate, rewrite, native queue and callback
            // drain. Only world content generation/Unity rendering are replaced.
            ReplaceFirstQueuedAction(() => LongEventHandler.ExecuteWhenFinished(() =>
            {
                Assert.That(context.Background, Is.True);
                order.Add("page-redraw");
                context.Preference = latest;
                BackgroundLoadingRuntime.SetBackground(latest);
                Queue(() => { Assert.That(context.Background, Is.True); order.Add("tail"); });
            }));
            UpdateNative(); UpdateNative(); JoinWorker(); UpdateNative();
            Assert.That(context.Background, Is.True);
            UpdateNative();
            Assert.That(order, Is.EqualTo(new[] { "page-redraw", "tail" }));
            Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
            Assert.That(context.Background, Is.EqualTo(latest));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void NativeWorldCancellationAndWorkerFailureRestoreCurrentPreference(bool failure, bool latest)
    {
        WithHooks(context =>
        {
            QueueNativeWorld();
            Assert.That(context.Background, Is.True);
            context.Preference = latest;
            if (failure)
            {
                bool handled = false;
                var expected = new InvalidOperationException("expected world worker failure");
                ReplaceFirstQueuedAction(() => throw expected);
                Field(FirstQueued(), "exceptionHandler").SetValue(FirstQueued(), (Action<Exception>)(e => handled = ReferenceEquals(e, expected)));
                UpdateNative(); UpdateNative(); JoinWorker(); UpdateNative();
                Assert.That(handled, Is.True);
            }
            else LongEventHandler.ClearQueuedEvents();
            Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
            Assert.That(context.Background, Is.EqualTo(latest));
        });
    }

    [Test]
    public void NativeWorldDeferredPageFailureRestoresWithoutSuppressingError()
    {
        WithHooks(context =>
        {
            QueueNativeWorld();
            var failure = new InvalidOperationException("expected page/redraw failure");
            ReplaceFirstQueuedAction(() => LongEventHandler.ExecuteWhenFinished(() => throw failure));
            // Native callback drainage logs/catches individual deferred failures.
            UpdateNative(); UpdateNative(); JoinWorker(); UpdateNative();
            Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
            Assert.That(context.Background, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForeignWorldPageHooksRefuseOrReleaseWorldWithoutDisablingSave(bool late)
    {
        WithHooks(context =>
        {
            if (late) QueueNativeWorld();
            var foreign = new Harmony("wakeup.tests.foreign-world");
            try
            {
                foreign.Patch(BackgroundLoadingRuntime.WorldNext, prefix: new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(Observe)));
                if (!late) BackgroundLoadingRuntime.TryInstallWorldHooks();
                Assert.That(BackgroundLoadingRuntime.CheckWorldOwnership(), Is.False);
                Assert.That(BackgroundLoadingRuntime.CheckOwnership(), Is.True);
                Assert.That(context.Background, Is.False);
                Assert.That(Harmony.GetPatchInfo(BackgroundLoadingRuntime.WorldNext).Owners, Does.Contain(foreign.Id));
                LongEventHandler.ClearQueuedEvents();
                GameDataSaveLoader.LoadGame("save-remains-supported");
                Assert.That(context.Background, Is.True);
                LongEventHandler.ClearQueuedEvents();
                Assert.That(context.Background, Is.False);
            }
            finally { foreign.UnpatchAll(foreign.Id); }
        });
    }

    [TestCase("wakeup.type-lookup", typeof(TypeLookupRuntime), "MenuUpdate")]
    [TestCase(PngRuntime.Owner, typeof(PngRuntime), "Menu")]
    [TestCase(DeferredAudioRuntime.Owner, typeof(DeferredAudioRuntime), "Idle")]
    [TestCase(StreamingXmlRuntime.Owner, typeof(StreamingXmlRuntime), "MenuUpdate")]
    [TestCase(LoadingTimingRuntime.Owner, typeof(LoadingTimingRuntime), "Menu")]
    [TestCase(LoadingDisplayRuntime.Owner, typeof(LoadingDisplayRuntime), "Menu")]
    public void ExactOwnEntryCompletionPostfixesPermitWorldAdmission(string owner, Type type, string method)
    {
        WithHooks(context =>
        {
            var feature = new Harmony(owner);
            try
            {
                feature.Patch(AccessTools.Method(typeof(Root_Entry), "Update"),
                    postfix: new HarmonyMethod(type, method));
                Assert.That(BackgroundLoadingRuntime.CheckWorldOwnership(), Is.True, "Known late startup observations remain compatible.");
                Assert.That(BackgroundLoadingRuntime.CheckColonyOwnership(), Is.True, "The same exact observations permit ordinary colony entry.");
                new Harmony(BackgroundLoadingRuntime.WorldOwner).UnpatchAll(BackgroundLoadingRuntime.WorldOwner);
                BackgroundLoadingRuntime.TryInstallWorldHooks();
                Assert.That(BackgroundLoadingRuntime.CheckWorldOwnership(), Is.True);
                QueueNativeWorld();
                Assert.That(context.Background, Is.True);
                LongEventHandler.ClearQueuedEvents();
                Assert.That(context.Background, Is.False);
            }
            finally { feature.UnpatchAll(feature.Id); }
        });
    }

    [TestCase("prefix")]
    [TestCase("foreign-owner")]
    [TestCase("foreign-method")]
    public void OwnEntryPostfixAllowanceRejectsWrongPlacementOwnerOrMethod(string change)
    {
        WithHooks(context =>
        {
            string owner = change == "foreign-owner" ? "wakeup.tests.foreign-entry" : "wakeup.type-lookup";
            var feature = new Harmony(owner);
            MethodInfo method = change == "foreign-method" ? AccessTools.Method(typeof(BackgroundLoadingTests), nameof(Observe))
                : AccessTools.Method(typeof(TypeLookupRuntime), "MenuUpdate");
            try
            {
                MethodInfo target = AccessTools.Method(typeof(Root_Entry), "Update");
                if (change == "prefix") feature.Patch(target, prefix: new HarmonyMethod(method));
                else feature.Patch(target, postfix: new HarmonyMethod(method));
                Assert.That(BackgroundLoadingRuntime.CheckWorldOwnership(), Is.False);
                Assert.That(BackgroundLoadingRuntime.CheckOwnership(), Is.True);
                Assert.That(context.Writes, Is.Zero);
            }
            finally { feature.UnpatchAll(feature.Id); }
        });
    }

    [Test]
    public void UnrelatedQueueRefusesWorldOwnershipAndRewriteRequiresOneNativeCall()
    {
        WithHooks(context =>
        {
            Queue(() => { });
            QueueNativeWorld();
            Assert.That(context.Writes, Is.Zero);
        });
        Assert.That((Action)(() => BackgroundLoadingRuntime.RewriteWorldQueue(Array.Empty<CodeInstruction>()).ToList()),
            Throws.TypeOf<InvalidOperationException>());
        var twice = new[] { new CodeInstruction(OpCodes.Call, BackgroundLoadingRuntime.WorldQueue),
            new CodeInstruction(OpCodes.Call, BackgroundLoadingRuntime.WorldQueue) };
        Assert.That((Action)(() => BackgroundLoadingRuntime.RewriteWorldQueue(twice).ToList()),
            Throws.TypeOf<InvalidOperationException>());
    }

    private static void QueueNativeWorld()
    {
        var page = (Page_CreateWorldParams)FormatterServices.GetUninitializedObject(typeof(Page_CreateWorldParams));
        Assert.That(BackgroundLoadingRuntime.WorldNext.Invoke(page, null), Is.False);
    }

    [Test]
    public void SelectionIsDefaultOffPersistedNextLaunchAndExplicitControlsWin()
    {
        string profile = TestContext.CurrentContext.TestDirectory;
        var settings = new WakeUpSettings();
        string[] before = UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile);
        Assert.That(BackgroundLoadingRuntime.Selected(before), Is.False);
        settings.BackgroundLoading = true;
        Assert.That(BackgroundLoadingRuntime.Selected(UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile)), Is.True);
        Assert.That(BackgroundLoadingRuntime.Selected(before), Is.False);
        foreach (string argument in new[] { "--wake-up-background-loading=off", "--wake-up-background-loading=invalid",
            "--wake-up-bypass", "--wake-up-mode=baseline", "--wake-up-reference" })
        {
            var args = new[] { "--wake-up-mode=candidate", "-savedatafolder=" + profile, "--wake-up-background-loading=on", argument };
            Assert.That(BackgroundLoadingRuntime.Selected(UserStartupSelection.Resolve(args, settings, profile)), Is.False);
        }
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile), Is.Empty);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void LatestPreferenceAndPostLoadGuardAreIndependentOfSimulationPause(bool latest, bool focused)
    {
        bool intended = !latest, actual = intended;
        int frame = 10;
        var session = new BackgroundLoadingSession(() => intended, x => actual = x, () => frame);
        session.Begin();
        intended = latest;
        session.ApplyPreference(intended);
        Assert.That(actual, Is.True);
        session.Enter();
        session.ArrivedOrCanceled();
        session.FinishIfIdle(false);
        Assert.That(session.Active, Is.True, "An executing completion callback still owns the chain.");
        session.Leave();
        session.FinishIfIdle(true);
        Assert.That(session.Active, Is.True, "Follow-up events still own the chain.");
        session.FinishIfIdle(false);
        Assert.That(actual, Is.EqualTo(latest));
        Assert.That(session.BlockPlay(focused), Is.EqualTo(!focused && !latest));
        frame++;
        Assert.That(session.BlockPlay(focused), Is.EqualTo(!focused && !latest));
        Assert.That(session.BlockPlay(true), Is.False);
        frame++;
        Assert.That(session.BlockPlay(false), Is.False, "After actual focus returns, ordinary later focus loss stays native.");
        // Repeat with the same object to expose leaked ownership/boundary state.
        session.Begin();
        session.ArrivedOrCanceled();
        session.FinishIfIdle(false);
        Assert.That(session.Active, Is.False);
    }

    [Test]
    public void SceneArrivalGapDoesNotReleaseAndCancellationCanEndTheWait()
    {
        bool background = false;
        var session = new BackgroundLoadingSession(() => false, x => background = x, () => 1);
        session.Begin();
        session.FinishIfIdle(false);
        Assert.That(background, Is.True, "Async scene completion may precede Root_Play.Start.");
        session.ArrivedOrCanceled();
        session.FinishIfIdle(false);
        Assert.That(background, Is.False);
    }

    [Test]
    public void PostLoadWaitEndsOnCurrentPreferenceEnableOrExplicitSceneCleanup()
    {
        bool preference = false;
        int frame = 10;
        var session = new BackgroundLoadingSession(() => preference, _ => { }, () => frame);
        session.Begin(false);
        session.FinishIfIdle(false);
        frame += 300;
        Assert.That(session.BlockPlay(false), Is.True);
        preference = true;
        session.ApplyPreference(true);
        preference = false;
        frame++;
        Assert.That(session.BlockPlay(false), Is.False, "A later false setting must not revive the old completed-load wait.");
        session.Begin(false);
        session.FinishIfIdle(false);
        frame++;
        Assert.That(session.BlockPlay(false), Is.True, "A new load owns a new post-load wait.");
        session.ClearCompletionGuard();
        Assert.That(session.BlockPlay(false), Is.False);
        Assert.That(session.Active, Is.False);
    }

    [Test]
    public void MenuCancellationBeforeFinalQueueDrainDoesNotRearmPostLoadGuard()
    {
        int frame = 1;
        var session = new BackgroundLoadingSession(() => false, _ => { }, () => frame);
        session.Begin();
        session.Enter();
        session.ArrivedOrCanceled();
        session.CancelCompletionGuard();
        session.FinishIfIdle(false);
        Assert.That(session.Active, Is.True, "Cancelling the completion wait must not release an executing loading boundary.");
        session.Leave();
        session.FinishIfIdle(false);
        frame++;
        Assert.That(session.BlockPlay(false), Is.False);
        session.Begin(false);
        session.FinishIfIdle(false);
        frame++;
        Assert.That(session.BlockPlay(false), Is.True, "A subsequent admitted load gets its own guard.");
    }

    [Test]
    public void ForeignSharedHookAfterCompletionStopsPendingPostLoadGuard()
    {
        WithHooks(context =>
        {
            BackgroundLoadingRuntime.Session.Begin(false);
            BackgroundLoadingRuntime.Session.FinishIfIdle(false);
            context.Frame++;
            Assert.That(BackgroundLoadingRuntime.ShouldBlockPlay(), Is.True);
            var foreign = new Harmony("wakeup.tests.foreign-after-background-completion");
            try
            {
                foreign.Patch(BackgroundLoadingRuntime.Update, prefix: new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(Observe)));
                Assert.That(BackgroundLoadingRuntime.ShouldBlockPlay(), Is.False);
                Assert.That(BackgroundLoadingRuntime.Session.NeedsPlayGuard, Is.False);
                Assert.That(BackgroundLoadingRuntime.Status, Does.Contain("stopped"));
            }
            finally { foreign.UnpatchAll(foreign.Id); }
        });
    }

    [Test]
    public void ActualSaveQueueAcquiresBeforeExecutionAndNativeFollowupsKeepPermission()
    {
        WithHooks(context =>
        {
            var order = new List<string>();
            GameDataSaveLoader.LoadGame("offline-placeholder-no-file-read");
            Assert.That(context.Background, Is.True);
            Assert.That(order, Is.Empty);
            // Keep the real queue, worker, callback drain and synchronous tail.
            // Replace only game-data/Unity scene work, unavailable on this host.
            ReplaceFirstQueuedAction(() =>
            {
                order.Add("worker");
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Assert.That(context.Background, Is.True);
                    order.Add("deferred");
                    LongEventHandler.ExecuteWhenFinished(() => order.Add("nested-deferred"));
                    Queue(() => { Assert.That(context.Background, Is.True); order.Add("followup"); },
                        () => { Assert.That(context.Background, Is.True); order.Add("callback"); Queue(() => order.Add("tail")); });
                });
            });
            BackgroundLoadingRuntime.Session.ArrivedOrCanceled(); // simulated engine scene arrival
            UpdateNative(); UpdateNative(); JoinWorker(); UpdateNative();
            Assert.That(context.Background, Is.True);
            UpdateNative();
            Assert.That(context.Background, Is.True, "Callback queued a tail after currentEvent was cleared.");
            UpdateNative();
            Assert.That(order, Is.EqualTo(new[] { "worker", "deferred", "nested-deferred", "followup", "callback", "tail" }));
            Assert.That(context.Background, Is.False);
            Assert.That(BackgroundLoadingRuntime.ShouldBlockPlay(), Is.True);
            context.Frame++;
            Assert.That(BackgroundLoadingRuntime.ShouldBlockPlay(), Is.True, "A later unfocused frame must not advance gameplay after loading.");
            BackgroundLoadingRuntime.Focused = () => true;
            Assert.That(BackgroundLoadingRuntime.ShouldBlockPlay(), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeQueueCancellationAndRepeatedLoadsRestoreLatestPreference(bool latest)
    {
        WithHooks(context =>
        {
            for (int i = 0; i < 2; i++)
            {
                GameDataSaveLoader.LoadGame("offline-placeholder");
                Assert.That(context.Background, Is.True);
                context.Preference = latest;
                BackgroundLoadingRuntime.SetBackground(latest);
                Assert.That(context.Background, Is.True);
                LongEventHandler.ClearQueuedEvents();
                Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
                Assert.That(context.Background, Is.EqualTo(latest));
            }
        });
    }

    [Test]
    public void ClearingInsideCallbackDoesNotReleaseBeforeReplacementIsQueued()
    {
        WithHooks(context =>
        {
            GameDataSaveLoader.LoadGame("offline-placeholder");
            ReplaceFirstQueuedAction(() => { }, async: false);
            BackgroundLoadingRuntime.Session.ArrivedOrCanceled();
            UpdateNative();
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                LongEventHandler.ClearQueuedEvents();
                Assert.That(context.Background, Is.True);
                Queue(() => Assert.That(context.Background, Is.True));
            });
            UpdateNative();
            Assert.That(context.Background, Is.True);
            UpdateNative();
            Assert.That(context.Background, Is.False);
        });
    }

    [Test]
    public void NestedSaveRequestKeepsOwnershipThroughTheNewSceneAndTail()
    {
        WithHooks(context =>
        {
            GameDataSaveLoader.LoadGame("outer-placeholder");
            ReplaceFirstQueuedAction(() => { }, async: false);
            BackgroundLoadingRuntime.Session.ArrivedOrCanceled();
            Field(FirstQueued(), "callback").SetValue(FirstQueued(), (Action)(() =>
            {
                GameDataSaveLoader.LoadGame("nested-placeholder");
                Assert.That(context.Background, Is.True);
                ReplaceFirstQueuedAction(() => { }, async: false);
            }));
            UpdateNative(); UpdateNative(); UpdateNative();
            Assert.That(context.Background, Is.True, "The nested scene still has to reach its play root.");
            BackgroundLoadingRuntime.Session.ArrivedOrCanceled();
            Queue(() => Assert.That(context.Background, Is.True));
            UpdateNative(); UpdateNative();
            Assert.That(context.Background, Is.False);
        });
    }

    [Test]
    public void KnownLoadingDisplayObserverCanCoexist()
    {
        WithHooks(context =>
        {
            var display = new Harmony(LoadingDisplayRuntime.Owner);
            try
            {
                display.Patch(BackgroundLoadingRuntime.Update, finalizer: new HarmonyMethod(typeof(LoadingDisplayRuntime), "AfterUpdate"));
                Assert.That(BackgroundLoadingRuntime.CheckOwnership(), Is.True);
                GameDataSaveLoader.LoadGame("offline-placeholder");
                Assert.That(context.Background, Is.True);
                LongEventHandler.ClearQueuedEvents();
                Assert.That(context.Background, Is.False);
            }
            finally { display.UnpatchAll(display.Id); }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeWorkerAndCompletionCallbackExceptionsRestoreWithoutSuppressingNativeHandling(bool callbackFailure)
    {
        WithHooks(context =>
        {
            GameDataSaveLoader.LoadGame("offline-placeholder");
            Exception failure = new InvalidOperationException("offline expected failure");
            bool handled = false;
            object pending = FirstQueued();
            ReplaceFirstQueuedAction(() => { if (!callbackFailure) throw failure; });
            Field(pending, "exceptionHandler").SetValue(pending, (Action<Exception>)(e => handled = ReferenceEquals(e, failure)));
            if (callbackFailure) Field(pending, "callback").SetValue(pending, (Action)(() => throw failure));
            BackgroundLoadingRuntime.Session.ArrivedOrCanceled();
            UpdateNative(); UpdateNative(); JoinWorker();
            if (callbackFailure) Assert.That(Assert.Throws<InvalidOperationException>((Action)UpdateNative), Is.SameAs(failure));
            else UpdateNative();
            Assert.That(handled, Is.EqualTo(!callbackFailure));
            Assert.That(context.Background, Is.False);
            Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
        });
    }

    [Test]
    public void OrdinaryAndDisplayWaitingSynchronousEventsRemainNative()
    {
        WithHooks(context =>
        {
            bool ran = false;
            Queue(() => ran = true);
            GameDataSaveLoader.LoadGame("busy-queue-is-ineligible");
            Assert.That(context.Background, Is.False);
            LongEventHandler.ClearQueuedEvents();
            Queue(() => ran = true);
            UpdateNative();
            LongEventHandler.SetCurrentEventText("Must be displayed");
            UpdateNative();
            Assert.That(ran, Is.False);
            LongEventHandler.SetCurrentEventText("");
            UpdateNative();
            Assert.That(ran, Is.True);
            Assert.That(context.Writes, Is.Zero, "Ineligible events must not touch the engine preference.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForeignLifecycleHooksRefuseOrReleaseWithoutRemovingForeignPatches(bool late)
    {
        WithHooks(context =>
        {
            if (late) GameDataSaveLoader.LoadGame("offline-placeholder");
            var foreign = new Harmony("wakeup.tests.foreign-background");
            try
            {
                foreign.Patch(BackgroundLoadingRuntime.Update, prefix: new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(Observe)));
                Assert.That(BackgroundLoadingRuntime.CheckOwnership(), Is.False);
                Assert.That(context.Background, Is.False);
                Assert.That(Harmony.GetPatchInfo(BackgroundLoadingRuntime.Update).Owners, Does.Contain(foreign.Id));
                if (!late)
                    Assert.That((Action)(() => BackgroundLoadingRuntime.InstallHooks(new Harmony(BackgroundLoadingRuntime.Owner))), Throws.TypeOf<InvalidOperationException>());
            }
            finally { foreign.UnpatchAll(foreign.Id); }
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PublishedForeignInnerLifecycleRecordsReleaseActiveOwnershipWithoutChangingRecords(bool prefix)
    {
        WithHooks(context =>
        {
            QueueNativeWorld();
            Assert.That(context.Background, Is.True);
            const string foreignId = "wakeup.tests.foreign-inner-background";
            Type shared = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")!;
            var publications = (Dictionary<MethodBase, byte[]>)AccessTools.Field(shared, "state").GetValue(null);
            Type serialization = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfoSerialization")!;
            byte[] original;
            lock (publications) original = publications[BackgroundLoadingRuntime.Update];
            try
            {
                // This pinned Harmony exposes inner patch records but its public
                // processor leaves innerMethod null, so wrapper generation fails.
                // Exercise its actual serialization/publication boundary instead;
                // do not pretend an inner executable hook was installed.
                object info = AccessTools.Method(serialization, "Deserialize").Invoke(null, new object[] { original });
                var hook = new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(Observe));
                AccessTools.Method(info.GetType(), prefix ? "AddInnerPrefixes" : "AddInnerPostfixes")
                    .Invoke(info, new object[] { foreignId, new[] { hook } });
                var published = (byte[])AccessTools.Method(serialization, "Serialize").Invoke(null, new[] { info });
                lock (publications) publications[BackgroundLoadingRuntime.Update] = published;
                Assert.That(BackgroundLoadingRuntime.CheckOwnership(), Is.False);
                Assert.That(BackgroundLoadingRuntime.Session.Active, Is.False);
                Assert.That(context.Background, Is.False);
                var record = Harmony.GetPatchInfo(BackgroundLoadingRuntime.Update);
                Assert.That((prefix ? record.InnerPrefixes : record.InnerPostfixes).Any(p => p.owner == foreignId), Is.True);
                lock (publications) Assert.That(publications[BackgroundLoadingRuntime.Update], Is.SameAs(published));
            }
            finally { lock (publications) publications[BackgroundLoadingRuntime.Update] = original; }
        });
    }

    [Test]
    public void GuardRunsAfterBaseUpdateBeforeGameplayAndLeavesNativePauseCallbackIntact()
    {
        var dynamic = new DynamicMethod("offline", typeof(void), Type.EmptyTypes);
        var original = PatchProcessor.GetOriginalInstructions(BackgroundLoadingRuntime.PlayUpdate).ToList();
        var rewritten = BackgroundLoadingRuntime.GuardPlayUpdate(original, dynamic.GetILGenerator()).ToList();
        int root = rewritten.FindIndex(c => c.Calls(AccessTools.Method(typeof(Root), "Update")));
        Assert.That(rewritten[root + 1].Calls(AccessTools.Method(typeof(BackgroundLoadingRuntime), "ShouldBlockPlay")), Is.True);
        Assert.That(rewritten[root + 2].opcode, Is.EqualTo(OpCodes.Brfalse));
        Assert.That(rewritten[root + 3].opcode, Is.EqualTo(OpCodes.Ret));
        Assert.That(rewritten[root + 4].labels, Does.Contain((Label)rewritten[root + 2].operand));
        Assert.That(rewritten.FindIndex(c => c.Calls(AccessTools.Method(typeof(Game), "UpdatePlay"))), Is.GreaterThan(root + 4));
        Assert.That(Harmony.GetPatchInfo(AccessTools.Method(typeof(Game), "LoadGame")), Is.Null);
        Assert.That(Harmony.GetPatchInfo(AccessTools.Method(typeof(TickManager), "DoSingleTick")), Is.Null);
        var apply = BackgroundLoadingRuntime.RewritePreference(PatchProcessor.GetOriginalInstructions(BackgroundLoadingRuntime.Apply)).ToList();
        Assert.That(apply.Count(c => c.Calls(AccessTools.Method(typeof(BackgroundLoadingRuntime), "SetBackground"))), Is.EqualTo(1));
    }

    private static Action? baseUpdateAction;
    private static int playUpdates;
    [TestCase(false, false, 0)]
    [TestCase(false, true, 1)]
    [TestCase(true, false, 1)]
    [TestCase(true, true, 1)]
    public void EmittedNativeCompletionBoundaryExecutesBaseButCanSkipPlay(bool preference, bool focused, int expected)
    {
        WithHooks(context =>
        {
            context.Preference = preference;
            BackgroundLoadingRuntime.Focused = () => focused;
            BackgroundLoadingRuntime.Session.Begin();
            playUpdates = 0;
            bool baseRan = false;
            baseUpdateAction = () => { baseRan = true; BackgroundLoadingRuntime.Session.Release(); };
            var dynamic = new DynamicMethod("NativeCompletionBoundary", typeof(void), Type.EmptyTypes, typeof(BackgroundLoadingTests), true);
            ILGenerator il = dynamic.GetILGenerator();
            var rewritten = BackgroundLoadingRuntime.GuardPlayUpdate(
                PatchProcessor.GetOriginalInstructions(BackgroundLoadingRuntime.PlayUpdate), il).ToList();
            int root = rewritten.FindIndex(c => c.Calls(AccessTools.Method(typeof(Root), "Update")));
            Assert.That(root, Is.EqualTo(1));
            // Emit the actual rewritten native prefix. Replace only base.Update
            // and the remaining Unity gameplay body with observed test actions.
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(BackgroundLoadingTests), nameof(CompleteInBaseUpdate)));
            for (int i = root + 1; i <= root + 3; i++)
            {
                var code = rewritten[i];
                if (code.operand is MethodInfo method) il.Emit(code.opcode, method);
                else if (code.operand is Label label) il.Emit(code.opcode, label);
                else il.Emit(code.opcode);
            }
            foreach (Label label in rewritten[root + 4].labels) il.MarkLabel(label);
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(BackgroundLoadingTests), nameof(RecordPlayUpdate)));
            il.Emit(OpCodes.Ret);
            ((Action)dynamic.CreateDelegate(typeof(Action)))();
            Assert.That(baseRan, Is.True);
            Assert.That(playUpdates, Is.EqualTo(expected));
            Assert.That(context.Background, Is.EqualTo(preference));
            baseUpdateAction = null;
        });
    }
    private static void CompleteInBaseUpdate() => baseUpdateAction!();
    private static void RecordPlayUpdate() => playUpdates++;

    private sealed class Context
    {
        internal bool Preference, Background;
        internal int Frame = 50, Writes;
    }
    private static void WithHooks(Action<Context> test)
    {
        var harmony = new Harmony(BackgroundLoadingRuntime.Owner);
        var engine = new Harmony("wakeup.tests.background-engine");
        var session = BackgroundLoadingRuntime.Session;
        var main = BackgroundLoadingRuntime.MainThread;
        var loaded = BackgroundLoadingRuntime.Loaded;
        var focused = BackgroundLoadingRuntime.Focused;
        var writeBackground = BackgroundLoadingRuntime.WriteEngineBackground;
        var context = new Context();
        int thread = Thread.CurrentThread.ManagedThreadId;
        try
        {
            BackgroundLoadingRuntime.WriteEngineBackground = x => { context.Writes++; context.Background = x; };
            BackgroundLoadingRuntime.Session = new(() => context.Preference, BackgroundLoadingRuntime.ApplyEngineBackground, () => context.Frame);
            BackgroundLoadingRuntime.MainThread = () => Thread.CurrentThread.ManagedThreadId == thread;
            BackgroundLoadingRuntime.Loaded = () => true;
            BackgroundLoadingRuntime.Focused = () => false;
            foreach (var method in new[] { AccessTools.Method(typeof(Log), "Error", new[] { typeof(string) }),
                AccessTools.Method(typeof(DeepProfiler), "Start"), AccessTools.Method(typeof(DeepProfiler), "End"),
                AccessTools.PropertyGetter(typeof(TutorSystem), "TutorialMode") })
                engine.Patch(method, prefix: new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(SkipEngine)));
            // Native async completion mentions Unity even when levelToLoad is
            // null; .NET Framework otherwise tries to JIT its engine-only calls.
            offlineAsync = EmitOfflineAsync();
            harmony.Patch(BackgroundLoadingRuntime.Update,
                transpiler: new HarmonyMethod(typeof(BackgroundLoadingTests), nameof(RouteOfflineAsync)));
            BackgroundLoadingRuntime.InstallHooks(harmony, includeEngineHooks: false);
            test(context);
        }
        finally
        {
            JoinWorker();
            harmony.UnpatchAll(harmony.Id);
            new Harmony(BackgroundLoadingRuntime.WorldOwner).UnpatchAll(BackgroundLoadingRuntime.WorldOwner);
            new Harmony(BackgroundLoadingRuntime.ColonyOwner).UnpatchAll(BackgroundLoadingRuntime.ColonyOwner);
            new Harmony(BackgroundLoadingRuntime.MapOwner).UnpatchAll(BackgroundLoadingRuntime.MapOwner);
            engine.UnpatchAll(engine.Id);
            BackgroundLoadingRuntime.ResetForTests();
            BackgroundLoadingRuntime.Session = session;
            BackgroundLoadingRuntime.MainThread = main;
            BackgroundLoadingRuntime.Loaded = loaded;
            BackgroundLoadingRuntime.Focused = focused;
            BackgroundLoadingRuntime.WriteEngineBackground = writeBackground;
            LongEventHandler.ClearQueuedEvents();
            AccessTools.Field(typeof(LongEventHandler), "currentEvent").SetValue(null, null);
            AccessTools.Field(typeof(LongEventHandler), "eventThread").SetValue(null, null);
            ((List<Action>)AccessTools.Field(typeof(LongEventHandler), "toExecuteWhenFinished").GetValue(null)).Clear();
        }
    }
    private static object FirstQueued() => ((IEnumerable)AccessTools.Field(typeof(LongEventHandler), "eventQueue").GetValue(null)).Cast<object>().First();
    private static FieldInfo Field(object value, string name) => AccessTools.Field(value.GetType(), name);
    private static void ReplaceFirstQueuedAction(Action action, bool async = true)
    {
        object item = FirstQueued();
        Field(item, "eventAction").SetValue(item, action);
        Field(item, "levelToLoad").SetValue(item, null);
        Field(item, "eventTextKey").SetValue(item, null);
        Field(item, "doAsynchronously").SetValue(item, async);
    }
    private static void Queue(Action action, Action? callback = null) => LongEventHandler.QueueLongEvent(action, null, false, null, callback: callback);
    private static void UpdateNative() => LongEventHandler.LongEventsUpdate(out _);
    private static void JoinWorker()
    {
        var thread = (Thread?)AccessTools.Field(typeof(LongEventHandler), "eventThread").GetValue(null);
        if (thread != null) Assert.That(thread.Join(5000), Is.True, "Native offline worker must finish before cleanup.");
    }
    private static bool SkipEngine() => false;
    private static void Observe() { }
    private static Action? offlineAsync;
    private static void RunOfflineAsync() => offlineAsync!();
    private static IEnumerable<CodeInstruction> RouteOfflineAsync(IEnumerable<CodeInstruction> codes)
    {
        foreach (var code in codes)
        {
            if (code.Calls(AccessTools.Method(typeof(LongEventHandler), "UpdateCurrentAsynchronousEvent")))
                code.operand = AccessTools.Method(typeof(BackgroundLoadingTests), nameof(RunOfflineAsync));
            yield return code;
        }
    }
    private static Action EmitOfflineAsync()
    {
        MethodInfo native = AccessTools.Method(typeof(LongEventHandler), "UpdateCurrentAsynchronousEvent");
        var copy = new DynamicMethod("NativeAsyncWithoutUnityScene", typeof(void), Type.EmptyTypes, typeof(LongEventHandler), true);
        var il = copy.GetILGenerator();
        foreach (var local in native.GetMethodBody()!.LocalVariables) il.DeclareLocal(local.LocalType);
        var codes = SubstituteSceneCalls(PatchProcessor.GetOriginalInstructions(native)).ToList();
        var labels = codes.SelectMany(c => c.labels).Distinct().ToDictionary(label => label, label => il.DefineLabel());
        foreach (var code in codes)
        {
            foreach (Label label in code.labels) il.MarkLabel(labels[label]);
            Assert.That(code.blocks, Is.Empty, "This inspected async method has no exception regions.");
            if (code.operand is MethodInfo method) il.Emit(code.opcode, method);
            else if (code.operand is ConstructorInfo constructor) il.Emit(code.opcode, constructor);
            else if (code.operand is FieldInfo field) il.Emit(code.opcode, field);
            else if (code.operand is Label label) il.Emit(code.opcode, labels[label]);
            else if (code.operand == null) il.Emit(code.opcode);
            else throw new InvalidOperationException("Unreviewed native async operand: " + code.operand);
        }
        return (Action)copy.CreateDelegate(typeof(Action));
    }
    private static IEnumerable<CodeInstruction> SubstituteSceneCalls(IEnumerable<CodeInstruction> codes)
    {
        foreach (var code in codes)
        {
            if (code.Calls(AccessTools.Method(typeof(SceneManager), "LoadSceneAsync", new[] { typeof(string) })))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(BackgroundLoadingTests), nameof(NoSceneLoad));
            }
            else if (code.Calls(AccessTools.PropertyGetter(typeof(AsyncOperation), "isDone")))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(BackgroundLoadingTests), nameof(NoSceneDone));
            }
            yield return code;
        }
    }
    private static AsyncOperation NoSceneLoad(string scene) => throw new InvalidOperationException("Tests must not request Unity scene loading.");
    private static bool NoSceneDone(AsyncOperation operation) => throw new InvalidOperationException("Tests must not poll Unity scene loading.");
}
