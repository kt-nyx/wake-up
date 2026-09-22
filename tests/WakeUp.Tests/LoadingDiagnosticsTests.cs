// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Xml;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class LoadingDiagnosticsTests
{
    private string root = "";
    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "WakeUp-xml-diagnostics-" + Guid.NewGuid().ToString("N"));
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        LoadingDiagnosticsRuntime.Initialize(root);
    }
    [Test]
    public void OrdinaryInitializationCreatesNoExportOrProfileFiles()
    {
        LoadingDiagnosticsRuntime.Initialize(root);
        Assert.That(Directory.Exists(root), Is.False);
    }
    [Test]
    public void RequestIsConsumedOnceAndCancellationPreventsIt()
    {
        LoadingDiagnosticsRuntime.RequestNextLaunch(root);
        string request = Path.Combine(root, "WakeUp", "xml-export-next-launch.request");
        Assert.That(File.Exists(request), Is.True);
        LoadingDiagnosticsRuntime.Initialize(root);
        Assert.That(File.Exists(request), Is.False);
        Assert.That(Directory.GetDirectories(LoadingDiagnosticsRuntime.ExportDirectory).Length, Is.EqualTo(1));
        LoadingDiagnosticsRuntime.Initialize(root);
        Assert.That(Directory.GetDirectories(LoadingDiagnosticsRuntime.ExportDirectory).Length, Is.EqualTo(1));
        LoadingDiagnosticsRuntime.RequestNextLaunch(root);
        LoadingDiagnosticsRuntime.CancelRequest(root);
        LoadingDiagnosticsRuntime.Initialize(root);
        Assert.That(File.Exists(request), Is.False);
        Assert.That(Directory.GetDirectories(LoadingDiagnosticsRuntime.ExportDirectory).Length, Is.EqualTo(1));
    }
    [Test]
    public void HistoryRetainsAtMostThreeRequestedLaunches()
    {
        for (int i = 0; i < 5; i++)
        {
            LoadingDiagnosticsRuntime.RequestNextLaunch(root);
            LoadingDiagnosticsRuntime.Initialize(root);
        }
        Assert.That(Directory.GetDirectories(LoadingDiagnosticsRuntime.ExportDirectory).Length, Is.EqualTo(3));
    }
    [Test]
    public void RequestedExportClosesAtItsOwnCallbackWithoutAPresentationSession()
    {
        Action? completion = null;
        int scheduled = 0;
        Action<Action> captureCallback = callback => { scheduled++; completion = callback; };
        LoadingDiagnosticsRuntime.Initialize(root);
        LoadingDiagnosticsRuntime.ObserveStartupCompletion(captureCallback);
        Assert.That(scheduled, Is.Zero, "Ordinary launches must not register export work");

        LoadingDiagnosticsRuntime.RequestNextLaunch(root);
        LoadingDiagnosticsRuntime.Initialize(root);
        LoadingDiagnosticsRuntime.ObserveStartupCompletion(captureCallback);
        LoadingDiagnosticsRuntime.ObserveStartupCompletion(captureCallback);
        Assert.That(scheduled, Is.EqualTo(1));
        Assert.That(completion is not null, Is.True);
        // No loading observation/display session is created for this request.
        completion!();
        string folder = Directory.GetDirectories(LoadingDiagnosticsRuntime.ExportDirectory)[0];
        string receipt = Path.Combine(folder, "completion-status.txt");
        Assert.That(File.ReadAllText(receipt), Does.Contain("0/2 observed stages").And.Contain("no complete export is claimed"));
        var laterDocument = new XmlDocument(); laterDocument.LoadXml("<Defs/>");
        LoadingDiagnosticsRuntime.Capture("later-reload-must-not-be-startup", laterDocument);
        Assert.That(Directory.GetFiles(folder).Length, Is.EqualTo(1), "A later reload cannot fill a closed startup request");
    }
}
