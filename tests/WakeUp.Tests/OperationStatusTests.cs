// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WakeUp;
namespace WakeUp.Tests;

[TestFixture]
public sealed class OperationStatusTests
{
    private string directory = null!, file = null!;
    private readonly List<string> log = new();
    [SetUp] public void SetUp()
    { directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "compatibility-" + Guid.NewGuid().ToString("N")); file = Path.Combine(directory, "notices.xml"); log.Clear(); }
    [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    private OperationRegistry Open() => new(file, text => { lock (log) log.Add(text); });
    private static void Failure(OperationRegistry registry, string id = "queries", string detail = "Another owner changed a required query method.")
    { registry.Request(id, "XML queries", true); registry.Set(id, OperationState.Unavailable, detail, "query-owner", "Native XML"); }
    [Test] public void CrashBeforeDisplayKeepsPendingAcrossLaunches()
    { Failure(Open()); Assert.That(Open().Pending(), Has.Length.EqualTo(1)); }
    [Test] public void SnapshotAcknowledgementCannotConsumeLaterFailure()
    {
        var registry = Open(); Failure(registry); var snapshot = registry.Pending(); Failure(registry, "reflection");
        Assert.That(registry.Acknowledge(snapshot.Select(n => n.Key)), Is.True, string.Join("\n", log));
        Assert.That(Open().Pending().Single().Key, Does.StartWith("reflection|"));
    }
    [Test] public void StableDecisionSurvivesDifferentDiagnosticBuildDetailsAndToggling()
    {
        var registry = Open(); Failure(registry, detail: "Build A, method hash A"); registry.Acknowledge(registry.Pending().Select(n => n.Key));
        var offLaunch = Open(); offLaunch.Request("queries", "XML queries", false);
        var next = Open(); Failure(next, detail: "Build B, method hash B, other settings changed");
        Assert.That(next.Pending(), Is.Empty);
        next.Set("queries", OperationState.Unavailable, "Different provider now owns query evaluation.", "query-owner", "Another provider");
        Assert.That(next.Pending(), Has.Length.EqualTo(1));
    }
    [Test] public void MissingOptionalProviderOffAndAvailableButBypassedNeverWarn()
    {
        var registry = Open(); registry.Request("optional", "Optional helper", true); registry.Set("optional", OperationState.NotApplicable, "Supplier absent");
        registry.Request("off", "Off helper", false); registry.Set("off", OperationState.Unavailable, "Changed method");
        registry.Request("fallback", "Native fallback", true); registry.Set("fallback", OperationState.Available, "Supplier bypassed native work");
        Assert.That(registry.Pending(), Is.Empty); Assert.That(File.Exists(file), Is.False);
    }
    [Test] public void PartialRefusalWarnsAndSuccessfulSiblingRemainsAvailable()
    {
        var registry = Open(); registry.Request("queries", "Queries", true);
        registry.Request("queries/a", "A", true); registry.Set("queries/a", OperationState.Available, "admitted");
        Failure(registry, "queries/b"); registry.SummarizeChildren("queries");
        Assert.That(registry.Snapshot().Single(s => s.StartsWith("Queries:")), Does.Contain("PartiallyAvailable"));
        Assert.That(registry.Snapshot().Single(s => s.StartsWith("A:")), Does.Contain("Available"));
        Assert.That(registry.Pending(), Has.Length.EqualTo(1));
    }
    [Test] public void FailedAcknowledgementWriteLeavesDurableAndInMemoryPending()
    {
        var registry = Open(); Failure(registry);
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.That(registry.Acknowledge(registry.Pending().Select(n => n.Key)), Is.False);
        Assert.That(registry.Pending(), Has.Length.EqualTo(1)); Assert.That(Open().Pending(), Has.Length.EqualTo(1));
        Assert.That(registry.Acknowledge(registry.Pending().Select(n => n.Key)), Is.True); Assert.That(Open().Pending(), Is.Empty);
    }
    [Test] public void FailedInitialPersistenceCannotStopLoadingOrLoseInMemoryNotice()
    {
        Directory.CreateDirectory(directory); Directory.CreateDirectory(file);
        var registry = Open(); Assert.DoesNotThrow((Action)(() => Failure(registry)));
        Assert.That(registry.Pending(), Has.Length.EqualTo(1)); Assert.That(registry.Acknowledge(registry.Pending().Select(n => n.Key)), Is.False);
    }
    [Test] public void ConcurrentRepeatedGuardLossIsOneDecision()
    {
        var registry = Open(); registry.Request("queries", "XML queries", true);
        Parallel.For(0, 30, _ => registry.Set("queries", OperationState.Unavailable, "Changed hook", "guard"));
        Assert.That(Open().Pending(), Has.Length.EqualTo(1)); Assert.That(log.Count, Is.EqualTo(1));
    }
    [Test] public void InterruptedSetupMarksOnlyUnattemptedRequestedOperations()
    {
        var registry = Open(); registry.Request("ready", "Ready", true); registry.Set("ready", OperationState.Available, "admitted");
        registry.Request("later", "Later", true); registry.Request("off", "Off", false); registry.InterruptSetup("Setup interrupted");
        Assert.That(registry.Pending().Single().Key, Does.StartWith("later|"));
    }
    [Test] public void EarlyDeclarationRetainsFailureThroughLaterSelection()
    {
        var registry = Open(); registry.Request("early", "Early", false); registry.Request("early", "Early", true);
        Assert.That(registry.Snapshot().Single(), Does.Contain("AwaitingStage"));
        Failure(registry, "early"); registry.Request("early", "Early", true);
        Assert.That(registry.Snapshot().Single(), Does.Contain("Unavailable"));
    }
    [Test] public void CorruptHistoryDoesNotStopNewDecisions()
    {
        Directory.CreateDirectory(directory); File.WriteAllText(file, "broken xml");
        var registry = Open(); Failure(registry); Assert.That(Open().Pending(), Has.Length.EqualTo(1), string.Join("\n", log));
    }
    [Test] public void RollbackRemovesFalseChildAvailability()
    {
        var registry = Open(); registry.Request("group/a", "A", true); registry.Set("group/a", OperationState.Available, "admitted");
        registry.RefuseChildren("group", "Shared scope failed");
        Assert.That(registry.Snapshot().Single(), Does.Contain("Unavailable")); Assert.That(Open().Pending(), Has.Length.EqualTo(1));
    }
}
