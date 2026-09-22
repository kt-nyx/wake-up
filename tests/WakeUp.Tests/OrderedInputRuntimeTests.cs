using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class OrderedInputRuntimeTests
{
    private const string Foreign = "wakeup.tests.ordered.foreign";
    private static FieldInfo Field(string name) => typeof(OrderedInputRuntime).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
    private static void Set(string name, object? value) => Field(name).SetValue(null, value);
    private static void Boundary() { }
    private static void Observer() { }
    private string root = null!;
    private readonly Dictionary<string, object?> saved = new();
    private bool profilerEnabled;
    [SetUp]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "ordered-runtime-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        foreach (string name in new[] { "loading", "enabled", "attempted", "guards", "select", "log", "staged", "consumed", "currentIndex" }) saved[name] = Field(name).GetValue(null);
        profilerEnabled = DeepProfiler.enabled; DeepProfiler.enabled = false;
        Set("log", null);
        Assert.That(PublishedPatchGuard.TryCreate(AccessTools.Method(typeof(OrderedInputRuntimeTests), nameof(Boundary)), OrderedInputRuntime.Owner, out var guard, true), Is.True);
        Set("guards", new[] { guard! }); Set("loading", true); Set("attempted", true);
    }
    [TearDown]
    public void Cleanup()
    {
        (Field("staged").GetValue(null) as IDisposable)?.Dispose(); Set("staged", null);
        OrderedInputRuntime.Stop("test-ended");
        new Harmony(Foreign).UnpatchAll(Foreign);
        foreach (var pair in saved) Set(pair.Key, pair.Value); saved.Clear();
        DeepProfiler.enabled = profilerEnabled;
        Directory.Delete(root, true);
    }
    private FileInfo File(string name, string xml)
    { var file = new FileInfo(Path.Combine(root, name)); System.IO.File.WriteAllText(file.FullName, xml); return file; }

    [Test]
    public void UnknownCallbackReleasesFutureSourcesBeforeNativeSelectionCanMutateThem()
    {
        var first = File("a.xml", "<Defs/>"); var next = File("b.xml", "<Defs/>");
        var queue = new OrderedInputQueue(new[] { first, next }); Set("queue", queue);
        var mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        ((Dictionary<ModContentPack, List<FileInfo>>)Field("selections").GetValue(null)!).Add(mod, new List<FileInfo> { next });
        int calls = 0;
        Set("select", new Func<ModContentPack, string, List<string>?, List<FileInfo>>((_, _, _) =>
        {
            calls++;
            Assert.That(queue.WorkersAlive, Is.False);
            Assert.That(queue.Outstanding, Is.Zero);
            // Same-size, same-timestamp replacement: metadata alone cannot prove freshness.
            var stamp = next.LastWriteTimeUtc;
            System.IO.File.WriteAllText(next.FullName, "<Next/>"); next.LastWriteTimeUtc = stamp;
            return new List<FileInfo> { next };
        }));
        new Harmony(Foreign).Patch(AccessTools.Method(typeof(OrderedInputRuntimeTests), nameof(Boundary)), prefix: new HarmonyMethod(typeof(OrderedInputRuntimeTests), nameof(Observer)));
        var result = OrderedInputRuntime.SelectFiles(mod, "Defs/", null);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(System.IO.File.ReadAllText(result[0].FullName), Is.EqualTo("<Next/>"));
        Assert.That(queue.ReservedBytes, Is.Zero);
    }

    [Test]
    public void MalformedPrivateParseReleasesCurrentAndAheadLeasesBeforeNativeErrorPath()
    {
        var first = File("a.xml", "<Defs>"); var next = File("b.xml", "<Defs/>");
        var queue = new OrderedInputQueue(new[] { first, next }); Set("queue", queue);
        var snapshot = queue.Take(0)!; Set("staged", snapshot);
        var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, CheckCharacters = false };
        Assert.That(OrderedInputRuntime.TryRead(first, settings, out _), Is.False);
        Assert.That(queue.WorkersAlive, Is.False); Assert.That(queue.Outstanding, Is.Zero);
        System.IO.File.WriteAllText(first.FullName, "<Fixed/>"); System.IO.File.WriteAllText(next.FullName, "<Later/>");
        Assert.That(queue.ReservedBytes, Is.Zero);
    }

    [Test]
    public void SharedDigestsProduceTheExistingParsedGenerationIdentity()
    {
        var bytes = new[] { Encoding.UTF8.GetBytes("<Defs/>"), Encoding.UTF8.GetBytes("<Defs><X/></Defs>") };
        Assert.That(ParsedXmlRuntime.KeyDigests("actual-selection", 9, bytes.Select(b => b.Length).ToArray(), bytes.Select(CacheDigest.Hash).ToArray()),
            Is.EqualTo("FD725FD5F20C73A093DD68D9F31E0466958B799EE064C171D37DF5F85E071752"));
    }

    [Test]
    public void OrdinarySelectionIsIndependentDefaultOffAndOneLaunchMaintenanceWins()
    {
        var settings = new WakeUpSettings();
        var args = UserStartupSelection.Resolve(Array.Empty<string>(), settings, root);
        Assert.That(args, Does.Not.Contain("--wake-up-ordered-input=on"));
        settings.OrderedInput = true; settings.DefinitionSearches = settings.TypeSearches = false;
        args = UserStartupSelection.Resolve(Array.Empty<string>(), settings, root);
        CacheLaunchPolicy? policy = null;
        Assert.That(OrderedInputRuntime.Selected(args, CacheLaunchPolicy.Latch(ref policy, "normal", () => { })), Is.True);
        foreach (string action in new[] { "clear", "bypass" })
        { policy = null; Assert.That(OrderedInputRuntime.Selected(args, CacheLaunchPolicy.Latch(ref policy, action, () => { })), Is.False); }
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, root), Is.Empty);
        Assert.That(UserStartupSelection.Resolve(new[] { "--wake-up-ordered-input=off" }, settings, root), Is.EqualTo(new[] { "--wake-up-ordered-input=off" }));
    }
}
