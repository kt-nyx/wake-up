// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class LoadingSessionTests
{
    [Test]
    public void NestedSpansSubtractTheUnionAndReportInStartOrder()
    {
        double now = 100;
        var session = new LoadingSession("Startup", true, () => now);
        var parent = session.Begin("XML", "Combine", "shared/unknown")!;
        now = 102;
        var child = session.Begin("XML", "Read a mod", "test.mod")!;
        now = 103;
        var grandchild = session.Begin("XML", "Read a file", "test.mod")!;
        now = 105;
        session.End(grandchild, null);
        now = 108;
        session.End(child, null);
        now = 110;
        session.End(parent, null);
        session.Finish("Loading queue completed");

        Assert.Multiple((Action)(() =>
        {
            Assert.That(parent.InclusiveSeconds, Is.EqualTo(10));
            Assert.That(parent.ExclusiveSeconds, Is.EqualTo(4));
            Assert.That(child.InclusiveSeconds, Is.EqualTo(6));
            Assert.That(child.ExclusiveSeconds, Is.EqualTo(4));
            Assert.That(grandchild.ExclusiveSeconds, Is.EqualTo(2));
            Assert.That(child.ParentSequence, Is.EqualTo(parent.Sequence));
            Assert.That(grandchild.ParentSequence, Is.EqualTo(child.Sequence));
        }));
        string report = session.RenderReport();
        Assert.That(report.IndexOf("\tCombine\t", StringComparison.Ordinal),
            Is.LessThan(report.IndexOf("\tRead a mod\t", StringComparison.Ordinal)));
        Assert.That(report, Does.Contain("10.000000\t4.000000\tshared/unknown\tXML\tCombine"));
        Assert.That(report, Does.Contain("test.mod\t2\t0\t8.000000\t6.000000")
            .And.Contain("shared/unknown\t1\t0\t10.000000\t4.000000").And.Contain("NOT wall time"));
        Assert.That(report, Does.Contain("unobserved").And.Contain("Different threads can overlap")
            .And.Contain("independent menu observer is separate"));
    }

    [Test]
    public void ConcurrentWorkHasNoFalseSameThreadParentOrSubtraction()
    {
        double now = 0;
        var session = new LoadingSession("Concurrent loading", true, () => now);
        var outer = session.Begin("Content", "Join workers")!;
        LoadingSession.Token? workerToken = null;
        LoadingSession.Context? workerContext = null;
        now = 1;
        var thread = new Thread(() =>
        {
            workerToken = session.Begin("Audio", "Worker", "test.worker");
            workerContext = session.CurrentThreadContext();
        });
        thread.Start();
        thread.Join();
        Assert.That(session.Snapshot().Stage, Is.EqualTo("Audio"), "The GUI shows the most recent worker activity.");
        Assert.That(session.CurrentThreadContext()!.Value.Stage, Is.EqualTo("Content"), "The main thread inherits only its own scope.");
        Assert.That(workerContext!.Value.Package, Is.EqualTo("test.worker"));
        Assert.That(workerContext.Value.Stage, Is.EqualTo("Audio"));
        now = 4;
        session.End(workerToken, null);
        now = 5;
        session.End(outer, null);
        session.Finish("Completed");
        Assert.Multiple((Action)(() =>
        {
            Assert.That(workerToken, Is.Not.Null);
            Assert.That(workerToken!.ParentSequence, Is.Zero);
            Assert.That(workerToken.ThreadId, Is.Not.EqualTo(outer.ThreadId));
            Assert.That(workerToken.InclusiveSeconds, Is.EqualTo(3));
            Assert.That(outer.ExclusiveSeconds, Is.EqualTo(5), "Other-thread work may overlap a parent's wait; it cannot be subtracted as nesting.");
            Assert.That(session.Snapshot().ActiveScopes, Is.Zero);
        }));
    }

    [Test]
    public void ExceptionsAreCopiedAsTextAndDuplicateEndDoesNotDuplicateErrors()
    {
        double now = 0;
        var session = new LoadingSession("Failure", true, () => now);
        var token = session.Begin("Constructors", "Broken constructor", "test.broken")!;
        var failure = new InvalidOperationException("native failure\nwith a new line");
        void ExecuteObservedWork()
        {
            Exception? observed = null;
            try { throw failure; }
            catch (Exception exception) { observed = exception; throw; }
            finally { now = 2; session.End(token, observed); }
        }
        Exception? received = Assert.Throws<InvalidOperationException>((Action)ExecuteObservedWork);
        session.End(token, failure);
        session.Finish("Stopped after native error");
        Assert.Multiple((Action)(() =>
        {
            Assert.That(received, Is.SameAs(failure));
            Assert.That(session.Snapshot().Errors, Is.EqualTo(1));
            Assert.That(token.Outcome, Is.EqualTo("Error"));
            Assert.That(token.Error, Is.EqualTo("System.InvalidOperationException: native failure with a new line"));
            Assert.That(session.RenderReport(), Does.Contain("test.broken").And.Contain("Stopped after native error"));
        }));
    }

    [Test]
    public void DisplayOnlyModeDoesNotReadClockAndPublishesKnownOrUnknownCounts()
    {
        var session = new LoadingSession("Save loading", false, () => throw new InvalidOperationException("Timing is off"));
        var parent = session.Begin("Map", "Restore map", "shared/unknown")!;
        session.Progress("Map", "Restore cells", 3, 10);
        var view = session.Snapshot();
        Assert.That(view.Completed, Is.EqualTo(3));
        Assert.That(view.Total, Is.EqualTo(10));
        Assert.That(view.Work, Is.EqualTo("Restore cells"));
        var child = session.Begin("Cross references", "Unknown amount of native work", "test.mod")!;
        Assert.That(session.Snapshot().Total, Is.Null);
        Assert.That(session.Snapshot().Package, Is.EqualTo("test.mod"));
        session.End(child, null);
        Assert.That(session.Snapshot().Stage, Is.EqualTo("Map"));
        Assert.That(session.Snapshot().Completed, Is.EqualTo(3));
        session.Progress("Map", "Unknown total", 5, null);
        Assert.That(session.Snapshot().Total, Is.Null);
        session.Progress("Map", "Inconsistent total", 8, 4);
        Assert.That(session.Snapshot().Total, Is.Null);
        session.End(parent, null);
        session.Error("A native error");
        session.Finish("Cancelled by the game");
        Assert.That(session.Snapshot().Active, Is.False);
        Assert.That(session.Snapshot().Work, Is.EqualTo("Cancelled by the game"));
        Assert.That(session.RenderReport(), Does.Contain("current progress and errors only").And.Contain("Retained spans: 0/")
            .And.Contain("A native error").And.Not.Contain("Elapsed since observation began:"));
        Assert.That(session.Omitted, Is.Zero, "Disabled timing is a preference, not accidental data loss.");
    }

    [Test]
    public void NoisyStageCannotStarveLaterDetailsAndOmittedDetailsStillContributeTotals()
    {
        double now = 0;
        var session = new LoadingSession("Large mod list", true, () => now);
        int noisyCount = LoadingSession.DetailPerStageLimit + 17;
        for (int i = 0; i < noisyCount; i++)
        {
            var token = session.Begin("XML inheritance", "Node " + i, "test.noisy");
            now++;
            session.End(token, null);
        }
        foreach (string stage in new[] { "Language", "Callbacks", "Atlases" })
        {
            var extra = session.Begin(stage, "Later " + stage, "last.mod");
            Assert.That(extra, Is.Not.Null);
            Assert.That(session.Snapshot().Work, Is.EqualTo("Later " + stage));
            Assert.That(session.Snapshot().Package, Is.EqualTo("last.mod"));
            now += 2;
            session.End(extra, null);
        }
        Assert.That(session.Omitted, Is.EqualTo(17));
        session.Finish("Completed");
        string report = session.RenderReport();
        Assert.That(report, Does.Contain("Retained spans: 515/32768; omitted observations: 17")
            .And.Contain("17 detail rows (their aggregates are preserved)"));
        Assert.That(report, Does.Contain("test.noisy\t529\t0\t529.000000\t529.000000")
            .And.Contain("XML inheritance\t529\t0\t529.000000\t529.000000")
            .And.Contain("last.mod\t3\t0\t6.000000\t6.000000"));
        Assert.That(report, Does.Contain("\tLater Language\t").And.Contain("\tLater Callbacks\t").And.Contain("\tLater Atlases\t")
            .And.Not.Contain("\tNode 528\t"));
    }

    [Test]
    public void AggregateTablesBoundLabelsAndPreserveOverflowDurations()
    {
        double now = 0;
        var session = new LoadingSession("Many labels", true, () => now);
        for (int i = 0; i < LoadingSession.PackageSummaryLimit + 3; i++)
        {
            var token = session.Begin("Stage " + i, "Work", "package." + i);
            now++;
            session.End(token, null);
        }
        session.Finish("Completed");
        string report = session.RenderReport();
        Assert.That(report, Does.Contain("shared/unknown\t4\t0\t4.000000\t4.000000")
            .And.Contain("Other stages (summary table limit)\t772\t0\t772.000000\t772.000000")
            .And.Contain("Package table limit: 1024").And.Contain("Stage table limit: 256"));
        Assert.That(report, Does.Not.Contain("package.1026\t"));
    }

    [Test]
    public void DepthAndErrorLimitsExposeOmissionsAndFinishClosesUnfinishedWork()
    {
        double now = 0;
        var session = new LoadingSession("Interrupted", true, () => now);
        var tokens = new List<LoadingSession.Token>();
        for (int i = 0; i < LoadingSession.DepthLimit; i++) tokens.Add(session.Begin("Nested", "Depth " + i)!);
        Assert.That(session.Begin("Nested", "Too deep"), Is.Null);
        for (int i = 0; i < LoadingSession.ErrorLimit + 3; i++) session.Error("Problem " + i);
        for (int i = 0; i < LoadingSession.UnobservedNoteLimit + 2; i++) session.NoteUnobserved("Unobserved region " + i);
        session.NoteUnobserved("Unobserved region 0");
        now = 4;
        session.Finish("Cancelled");
        Assert.That(session.Snapshot().ActiveScopes, Is.Zero);
        Assert.That(session.Omitted, Is.EqualTo(1));
        Assert.That(session.Unobserved, Is.EqualTo(35));
        Assert.That(session.Snapshot().Errors, Is.EqualTo(LoadingSession.ErrorLimit + 3), "Coverage gaps are not native game errors.");
        Assert.That(tokens[0].InclusiveSeconds, Is.EqualTo(4));
        Assert.That(tokens[0].ExclusiveSeconds, Is.Zero);
        Assert.That(tokens[tokens.Count - 1].ExclusiveSeconds, Is.EqualTo(4));
        string report = session.RenderReport();
        Assert.That(report, Does.Contain("unfinished scopes at session end: 16").And.Contain("Additional error messages omitted: 3"));
        Assert.That(report, Does.Contain("Unobserved-region notices: 35; notice text omitted: 2")
            .And.Contain("2 notice(s): Unobserved region 0").And.Not.Contain("Unobserved region 32"));
        long version = session.Version;
        session.End(tokens[0], null);
        session.Finish("Overwritten");
        session.Progress("Ignored", "Ignored", 1, 1);
        session.Error("Ignored");
        session.NoteUnobserved("Ignored");
        Assert.That(session.Begin("Ignored", "Ignored"), Is.Null);
        Assert.That(session.Version, Is.EqualTo(version));
        Assert.That(session.RenderReport(), Is.EqualTo(report));
    }

    [Test]
    public void OutOfOrderEndsAndForeignTokensCannotCorruptActiveScopes()
    {
        double now = 0;
        var session = new LoadingSession("Ordering", true, () => now);
        var other = new LoadingSession("Other session", true, () => now);
        var foreign = other.Begin("Foreign", "Foreign")!;
        var parent = session.Begin("Parent", "Parent")!;
        now = 1;
        var child = session.Begin("Child", "Child")!;
        now = 2;
        session.End(parent, null);
        session.End(foreign, null);
        Assert.That(session.Snapshot().ActiveScopes, Is.EqualTo(1));
        Assert.That(session.Snapshot().Work, Is.EqualTo("Child"));
        Assert.That(parent.ExclusiveSeconds, Is.EqualTo(1));
        now = 3;
        session.End(child, null);
        Assert.That(child.InclusiveSeconds, Is.EqualTo(2));
        Assert.That(child.ExclusiveSeconds, Is.EqualTo(2));
        Assert.That(session.Snapshot().ActiveScopes, Is.Zero);
        Assert.That(other.Snapshot().ActiveScopes, Is.EqualTo(1));
    }

    [Test]
    public void ReportsBoundCallerTextAndCannotInjectRows()
    {
        var session = new LoadingSession("Name\nSecond line", true, () => 0);
        var token = session.Begin("Stage\tFake column", new string('x', 10000), "package\nFake row")!;
        session.End(token, new InvalidOperationException(new string('y', 10000)));
        session.Finish("Completed");
        string report = session.RenderReport();
        Assert.That(token.Work.Length, Is.EqualTo(321));
        Assert.That(token.Error.Length, Is.EqualTo(1025));
        Assert.That(report, Does.Contain("Name Second line").And.Contain("Stage Fake column").And.Contain("package Fake row"));
        Assert.That(report.Length, Is.LessThan(6000));
    }

    [TestCase("Loading zetrith.prepatcher", "XML read", "XML read")]
    [TestCase("Loading Ludeon.RimWorld", "XML read", "XML read")]
    [TestCase("ParseValueAndReturnDef (for Verse.SoundDef)", "XML / Def loading", "Def creation")]
    [TestCase("ResolveAllReferences FixtureMenuObserver.LanguageProbeDef", "Definitions and references", "Cross-references")]
    [TestCase("TKeySystem.BuildMappings()", null, "Language")]
    [TestCase("Generate mipmaps with compute shader", null, "Content")]
    [TestCase("Generate mipmaps with compute shader", "Atlas baking", "Atlas baking")]
    [TestCase("Reload audio clips", "Content", "Audio")]
    [TestCase("RimWorld.PlantProperties+<>c -> Void <PostLoadSpecial>b__0()", "Deferred callbacks", "Deferred callbacks")]
    [TestCase("Verse.Sound.SubSustainer -> Void <.ctor>b__12_0()", "Deferred callbacks", "Deferred callbacks")]
    [TestCase("GenerateWorld", null, "World loading / generation")]
    [TestCase("WorldGen - RimWorld.SurfaceLayer", null, "World loading / generation")]
    [TestCase("WorldGenStep - Terrain", null, "World loading / generation")]
    [TestCase("GenStep - Terrain", null, "Map loading / generation")]
    [TestCase("Loading game from file RloFixtureSmoke", null, "Save loading")]
    [TestCase("World.FinalizeInit", "Save loading", "World loading / generation")]
    [TestCase("maps.FinalizeLoading", "Save loading", "Map loading / generation")]
    [TestCase("Scribe.loader.FinalizeLoading", "Save loading", "Save loading")]
    [TestCase("Finalize geometry", "Map loading / generation", "Map loading / generation")]
    [TestCase("UnknownSoundMapWorldPatchMod", null, "Native loading work")]
    [TestCase("Other.WorldGenStep - Terrain", null, "Native loading work")]
    public void NativeOperationFormsOverrideContextButNamesDoNotInventPhases(string label, string? parent, string expected)
        => Assert.That(LoadingObservationRuntime.Stage(label, parent), Is.EqualTo(expected));

    [Test]
    public void CurrentThreadContextIncludesExplicitScopesAndExpiresWithThem()
    {
        var session = new LoadingSession("Context", false);
        var explicitScope = session.Begin("XML patches", "Construct patch objects", "test.patches");
        session.Progress("World loading / generation", "An unrelated progress notice", 0, null);
        var context = session.CurrentThreadContext();
        Assert.That(context!.Value.Stage, Is.EqualTo("XML patches"));
        Assert.That(context.Value.Package, Is.EqualTo("test.patches"));
        var marker = session.Begin(LoadingObservationRuntime.Stage("An arbitrary inner operation", context.Value.Stage),
            "An arbitrary inner operation", context.Value.Package);
        Assert.That(marker!.Stage, Is.EqualTo("XML patches"));
        Assert.That(marker.Package, Is.EqualTo("test.patches"));
        session.End(marker, null);
        session.End(explicitScope, null);
        Assert.That(session.CurrentThreadContext(), Is.Null, "Ended scopes do not leave a stale marker context.");
        var next = session.Begin("Deferred callbacks", "New callback", "another.mod");
        Assert.That(session.CurrentThreadContext()!.Value.Package, Is.EqualTo("another.mod"));
        session.Finish("Ended");
        Assert.That(session.CurrentThreadContext(), Is.Null);
        session.End(next, null);
        Assert.That(new LoadingSession("New session", false).CurrentThreadContext(), Is.Null);
    }
}
