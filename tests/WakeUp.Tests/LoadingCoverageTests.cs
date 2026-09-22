// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class LoadingCoverageTests
{
    [Test]
    public void NativeTimingContractsMatchFrozenBodies()
    {
        for (int i = 0; i < LoadingTimingRuntime.Targets.Length; i++)
        {
            var method = LoadingTimingRuntime.Targets[i];
            if (SemanticMethodIdentity.TryHash(method, out string hash, out string reason))
                Assert.That(hash, Is.EqualTo(LoadingTimingRuntime.Bodies[i]));
            else
            {
                // The game BCL has Dictionary.TryAdd; the .NET Framework test
                // host does not. Exact original PE-normalized hashes and actual
                // worker execution are checked separately on the modern host.
                Assert.That(i, Is.Zero);
                Assert.That(reason, Is.EqualTo("semantic-il-exception-missingmethodexception"));
            }
        }
    }

    [Test]
    public void SummaryRewriteOnlyAddsOneConditionAndPreservesEveryNativeCall()
    {
        var original = PatchProcessor.GetOriginalInstructions(NativeLoadingSummaryRuntime.Target);
        var rewritten = NativeLoadingSummaryRuntime.Rewrite(original.Select(i => new CodeInstruction(i))).ToList();
        var gate = AccessTools.Method(typeof(NativeLoadingSummaryRuntime), nameof(NativeLoadingSummaryRuntime.ShowSummary));
        Assert.That(rewritten.Count, Is.EqualTo(original.Count + 1));
        Assert.That(rewritten.Count(i => i.Calls(gate)), Is.EqualTo(1));
        Assert.That(rewritten.Where(i => !i.Calls(gate)).Select(i => (i.opcode, i.operand)),
            Is.EqualTo(original.Select(i => (i.opcode, i.operand))));
        int at = rewritten.FindIndex(i => i.Calls(gate));
        Assert.That(rewritten[at + 1].opcode, Is.EqualTo(OpCodes.Stloc_2));
        Assert.That(rewritten[at].labels, Is.EqualTo(original[at].labels));
        Assert.That(rewritten[at + 1].labels, Is.Empty);
        Assert.That((Action)(() => { NativeLoadingSummaryRuntime.Rewrite(original.Where(i => !i.Calls(AccessTools.Method(typeof(Verse.ModSummaryWindow), "GetEffectiveSize"))))
            .ToArray(); }), Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void SummaryChoiceCooperatesWithDisplayAndRefusesLateForeignDrawing()
    {
        var summary = new Harmony(NativeLoadingSummaryRuntime.Owner);
        var display = new Harmony(LoadingDisplayRuntime.Owner);
        var other = new Harmony("wakeup.tests.summary-foreign");
        try
        {
            Assert.That(NativeLoadingSummaryRuntime.ShowSummary(true), Is.True);
            NativeLoadingSummaryRuntime.Install(summary);
            LoadingDisplayRuntime.InstallHooks(display);
            LoadingDisplayRuntime.Begin(false);
            Assert.That(LoadingDisplayRuntime.CheckOwnership(), Is.True);
            Assert.That(NativeLoadingSummaryRuntime.ShowSummary(false), Is.False);
            Assert.That(NativeLoadingSummaryRuntime.ShowSummary(true), Is.False);
            other.Patch(NativeLoadingSummaryRuntime.Target, prefix: new HarmonyMethod(typeof(LoadingCoverageTests), nameof(Noop)));
            Assert.That(NativeLoadingSummaryRuntime.ShowSummary(true), Is.True);
            Assert.That(NativeLoadingSummaryRuntime.Status, Does.Contain("refused"));
        }
        finally
        {
            summary.UnpatchAll(summary.Id); display.UnpatchAll(display.Id); other.UnpatchAll(other.Id);
            NativeLoadingSummaryRuntime.Reset(); LoadingDisplayRuntime.Stop("test", "cleanup");
        }
    }
    private static void Noop() { }

    [Test]
    public void TimingReportBoundsMemoryMarksNestedAndFailedCallsAndDoesNotSum()
    {
        double now = 10;
        var report = new LoadingTimingReport(() => now);
        var outer = report.Begin("example.mod", "patch construction")!;
        now = 11;
        var inner = report.Begin("example.mod", "XML files")!;
        Assert.That(inner.Depth, Is.EqualTo(1));
        now = 12;
        var error = new InvalidOperationException("original");
        report.End(inner, error); report.End(inner, error);
        now = 13; report.End(outer, null);
        for (int i = 0; i < 1000; i++) { var token = report.Begin("mod", "files"); if (token != null) report.End(token, null); }
        Assert.That(report.Completed, Is.EqualTo(LoadingTimingReport.Limit));
        string text = report.Finish("ended");
        Assert.That(text, Does.Contain("DO NOT SUM").And.Contain("threw InvalidOperationException").And.Contain("Omitted calls: 490"));
        Assert.That(text, Does.Contain("1000.000").And.Contain("3000.000"));
        Assert.That(report.Current, Is.Empty);
        Assert.That(report.Begin("later", "ignored"), Is.Null);
    }

    [Test]
    public void TimingsBoundPendingScopesAndPreserveExceptionObserverContract()
    {
        var report = new LoadingTimingReport();
        for (int i = 0; i < 32; i++) Assert.That(report.Begin("mod", "files"), Is.Not.Null);
        Assert.That(report.Begin("overflow", "files"), Is.Null);
        Assert.That(report.Finish("interrupted"), Does.Contain("unfinished calls at stop: 32").And.Contain("Omitted calls: 1"));
        Assert.That(AccessTools.Method(typeof(LoadingTimingRuntime), nameof(LoadingTimingRuntime.After)).ReturnType, Is.EqualTo(typeof(void)),
            "A void Harmony finalizer observes but cannot replace the native exception.");
        Assert.That(AccessTools.Method(typeof(LoadingTimingRuntime), nameof(LoadingTimingRuntime.Menu)).ReturnType, Is.EqualTo(typeof(void)));
    }

    [Test]
    public void SummaryAndTimingsAreIndependentNextLaunchChoices()
    {
        string profile = TestContext.CurrentContext.TestDirectory;
        var settings = new WakeUpSettings();
        var before = UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile);
        Assert.That(NativeLoadingSummaryRuntime.Selected(before), Is.False);
        Assert.That(LoadingTimingRuntime.Selected(before), Is.False);
        settings.HideLoadingSummary = settings.LoadingTimings = true;
        var after = UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile);
        Assert.That(NativeLoadingSummaryRuntime.Selected(after), Is.True);
        Assert.That(LoadingTimingRuntime.Selected(after), Is.True);
        Assert.That(LoadingDisplayRuntime.Selected(after), Is.False);
        Assert.That(LoadingTimingRuntime.Selected(after.Concat(new[] { "--wake-up-loading-timings=off" }).ToArray()), Is.False);
        Assert.That(NativeLoadingSummaryRuntime.Selected(after.Concat(new[] { "--wake-up-hide-loading-summary=off" }).ToArray()), Is.False);
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, profile), Is.Empty);
    }

    [Test]
    public void FeatureFeedbackOnlySamplesOnDiagnosticRefreshBudget()
    {
        int calls = 0;
        string Snapshot() { calls++; return "selected, awaiting reads"; }
        LoadingCacheSnapshot Cache() => new(false, false, "", CacheAction.Normal, 0, 0, 0, 0, 0, "none");
        var state = new LoadingDisplayState(0, true);
        state.Refresh(0, 1, Cache, out _, Snapshot);
        state.Refresh(0.1, 1, Cache, out _, Snapshot);
        Assert.That(calls, Is.EqualTo(1));
        state.Refresh(0.25, 1, Cache, out var text, Snapshot);
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(text, Does.Contain("selected, awaiting reads"));
        new LoadingDisplayState(0, false).Refresh(0, 1, Cache, out _, Snapshot);
        Assert.That(calls, Is.EqualTo(2));
    }
}
