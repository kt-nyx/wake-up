// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class StreamingXmlTests
{
    private string root = null!;
    private static XmlReaderSettings Settings() => new() { IgnoreComments = true, IgnoreWhitespace = true, CheckCharacters = false, Async = false };
    [SetUp] public void Setup()
    {
        root = Path.Combine(TestContext.CurrentContext.TestDirectory, "streaming-xml-input", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Configure(true);
    }
    [TearDown] public void Cleanup()
    {
        Configure(false);
        Directory.Delete(root, true);
    }
    private FileInfo Input(byte[] bytes)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllBytes(path, bytes);
        return new FileInfo(path);
    }
    private static void Set(string name, object value) => typeof(StreamingXmlRuntime).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, value);
    private static void Configure(bool active)
    {
        Set("enabled", active); Set("finished", false); Set("scopes", active ? 1 : 0);
        Set("reading", 0); Set("completed", 0); Set("refused", 0);
        Set("guards", StreamingXmlRuntime.CreateGuards(typeof(LoadableXmlAsset).GetConstructor(new[] { typeof(FileInfo), typeof(ModContentPack) })!));
    }
    private static XmlDocument FullText(FileInfo file)
    {
        byte[] bytes = File.ReadAllBytes(file.FullName);
        int start = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
        using var text = new StringReader(Encoding.UTF8.GetString(bytes, start, bytes.Length - start));
        using var reader = XmlReader.Create(text, Settings());
        var document = new XmlDocument(); document.Load(reader); return document;
    }

    [Test]
    public void ExactNativeAndRewrittenConstructorContracts()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadableXmlAsset).Assembly.Location);
        var method = StreamingXmlPrepatch.FindConstructor(module)!;
        string native = AssetRoutingPrepatch.Fingerprint(method);
        TestContext.Out.WriteLine("STREAMING_XML_NATIVE " + native);
        Assert.That(native, Is.EqualTo(StreamingXmlPrepatch.NativeBody));
        var original = method.Body.Instructions.ToArray();
        var operands = original.Select(i => i.Operand).ToArray();
        var handlers = method.Body.ExceptionHandlers.Select(h => (h.TryStart, h.TryEnd, h.HandlerStart, h.HandlerEnd)).ToArray();
        StreamingXmlPrepatch.Inject(module, method);
        Assert.That(method.Body.Instructions.Where(original.Contains), Is.EqualTo(original));
        Assert.That(original.Select(i => i.Operand), Is.EqualTo(operands));
        Assert.That(method.Body.ExceptionHandlers.Select(h => (h.TryStart, h.TryEnd, h.HandlerStart, h.HandlerEnd)), Is.EqualTo(handlers));
        using var bytes = new MemoryStream(); module.Write(bytes);
        File.WriteAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "streaming-xml-native.dll"), bytes.ToArray());
        var assembly = Assembly.Load(bytes.ToArray());
        var target = assembly.GetType("Verse.LoadableXmlAsset")!.GetConstructors().Single(c => c.GetParameters()[0].ParameterType == typeof(FileInfo));
        if (SemanticMethodIdentity.TryHash(target, out string effective, out string why))
        {
            TestContext.Out.WriteLine("STREAMING_XML_EFFECTIVE " + effective);
            Assert.That(effective, Is.EqualTo(StreamingXmlRuntime.EffectiveConstructorBody));
        }
        else
        {
            Assert.That(why, Is.EqualTo("semantic-il-exception-typeloadexception"));
            TestContext.Out.WriteLine("Desktop host cannot resolve the native Span BCL. Cecil identity/fallback checks passed; runtime identity requires the separate modern native-constructor receipt.");
        }
    }

    [Test]
    public void ChangedAndAlreadyRewrittenConstructorsRefuseWithoutMutation()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadableXmlAsset).Assembly.Location);
        var method = StreamingXmlPrepatch.FindConstructor(module)!;
        method.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Nop));
        string changed = AssetRoutingPrepatch.Fingerprint(method);
        Assert.That(StreamingXmlPrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(method), Is.EqualTo(changed));
        method.Body.Instructions.RemoveAt(0);
        Assert.That(StreamingXmlPrepatch.RewriteAssembly(module), Is.True);
        changed = AssetRoutingPrepatch.Fingerprint(method);
        Assert.That(StreamingXmlPrepatch.RewriteAssembly(module), Is.False);
        Assert.That(AssetRoutingPrepatch.Fingerprint(method), Is.EqualTo(changed));
    }

    [TestCase("<Defs><ThingDef><defName>A</defName></ThingDef><ThingDef><defName>A</defName></ThingDef></Defs>")]
    [TestCase("<?xml version='1.0' encoding='iso-8859-1'?><Defs>héllo 😺<!-- comment --><x a='é'/></Defs>")]
    public void StreamingMatchesNativeUtf8TextMeaning(string xml)
    {
        foreach (bool bom in new[] { false, true })
        {
            var payload = Encoding.UTF8.GetBytes(xml);
            var file = Input(bom ? Encoding.UTF8.GetPreamble().Concat(payload).ToArray() : payload);
            Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out var streamed), Is.True);
            Assert.That(streamed.OuterXml, Is.EqualTo(FullText(file).OuterXml));
        }
    }

    [Test]
    public void DecoderBoundariesAndInvalidUtf8KeepNativeReplacement()
    {
        var bytes = Encoding.UTF8.GetBytes("<Defs>" + new string('a', 16377) + "😺é尾</Defs>").ToList();
        bytes.Insert(bytes.Count - 7, 0xff);
        var file = Input(bytes.ToArray());
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out var parsed), Is.True);
        Assert.That(parsed.OuterXml, Is.EqualTo(FullText(file).OuterXml));
    }

    [Test]
    public void MissingMalformedEmptyAndDifferentEncodingKeepOriginalDiagnosticsPath()
    {
        foreach (byte[] bytes in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("<Defs>"), Encoding.UTF8.GetBytes("<!DOCTYPE Defs [<!ENTITY test 'value'>]><Defs>&test;</Defs>"), Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("<Defs/>")).ToArray() })
            Assert.That(StreamingXmlRuntime.TryRead(Input(bytes), Settings(), out var document), Is.False);
        Assert.That(StreamingXmlRuntime.TryRead(new FileInfo(Path.Combine(root, "missing.xml")), Settings(), out _), Is.False);
        var large = Input(new byte[StreamingXmlRuntime.MaxFileBytes + 1]);
        Assert.That(StreamingXmlRuntime.TryRead(large, Settings(), out _), Is.False);
    }

    [Test]
    public void DisabledCutoffAndUnknownHooksKeepOrdinaryPath()
    {
        var file = Input(Encoding.UTF8.GetBytes("<Defs/>"));
        Configure(false);
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out _), Is.False);
        Configure(true);
        Set("guards", Array.Empty<PublishedPatchGuard>());
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out _), Is.False);
        Configure(true);
        // Dispose logs only when actually selected; tests exercise the same cutoff
        // without requiring Unity's native logging entry point.
        typeof(StreamingXmlRuntime).GetField("finished", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, true);
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out _), Is.False);
    }

    [Test]
    public void ConstructorBridgePublishesNativeAttributionAndActualParsedDocument()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadableXmlAsset).Assembly.Location);
        var method = StreamingXmlPrepatch.FindConstructor(module)!;
        StreamingXmlPrepatch.Inject(module, method);
        // Desktop .NET lacks the game's Span BCL. Remove only the native fallback
        // tail in this execution copy; the preceding test proves it is preserved
        // in the production rewrite. Net8/Mono qualification exercises that tail.
        var call = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m && m.Name == "TryRead");
        var original = call.Next.Operand as Instruction;
        int tail = method.Body.Instructions.IndexOf(original!);
        while (method.Body.Instructions.Count > tail) method.Body.Instructions.RemoveAt(tail);
        method.Body.ExceptionHandlers.Clear();
        method.Body.Instructions.Add(original!);
        original!.OpCode = OpCodes.Newobj;
        original.Operand = module.ImportReference(typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes)!);
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Throw));
        // Remove unused native Span locals so CLR metadata need not resolve them.
        var retained = method.Body.Variables.Last();
        method.Body.Variables.Clear(); method.Body.Variables.Add(retained);
        using var bytes = new MemoryStream(); module.Write(bytes);
        var copy = Assembly.Load(bytes.ToArray()).GetType("Verse.LoadableXmlAsset")!;
        var file = Input(Encoding.UTF8.GetBytes("<Defs><x/></Defs>"));
        object asset = Activator.CreateInstance(copy, new object?[] { file, null })!;
        Assert.That(copy.GetField("name")!.GetValue(asset), Is.EqualTo(file.Name));
        Assert.That(copy.GetField("fullFolderPath")!.GetValue(asset), Is.EqualTo(file.Directory!.FullName));
        Assert.That(((XmlDocument)copy.GetField("xmlDoc")!.GetValue(asset)).OuterXml, Is.EqualTo("<Defs><x /></Defs>"));
        Configure(false);
        Assert.Throws<TargetInvocationException>((Action)(() => { Activator.CreateInstance(copy, new object?[] { file, null }); }));
    }

    [Test]
    public void IndependentDocumentsAndBoundedAdmissionPreserveNativeFallback()
    {
        var file = Input(Encoding.UTF8.GetBytes("<Defs><x/></Defs>"));
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out var first), Is.True);
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out var second), Is.True);
        Assert.That(first, Is.Not.SameAs(second));
        first.DocumentElement!.AppendChild(first.CreateElement("changed"));
        Assert.That(second.DocumentElement!.ChildNodes.Count, Is.EqualTo(1));
        Set("reading", StreamingXmlRuntime.MaxConcurrentReads);
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out _), Is.False);
        Assert.That(typeof(StreamingXmlRuntime).GetField("reading", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null), Is.EqualTo(StreamingXmlRuntime.MaxConcurrentReads));
    }

    [TestCase("AddPrefixes")]
    [TestCase("AddPostfixes")]
    [TestCase("AddTranspilers")]
    [TestCase("AddFinalizers")]
    [TestCase("AddInnerPrefixes")]
    [TestCase("AddInnerPostfixes")]
    public void ForeignConstructorHooksRefuseAndLaterRemovalRecovers(string category)
    {
        var target = typeof(LoadableXmlAsset).GetConstructor(new[] { typeof(FileInfo), typeof(ModContentPack) })!;
        PublishedHookRefuses(category, target);
    }
    [TestCase("FileSize")]
    [TestCase("Dispose")]
    public void BypassedNativeHelperHooksRefuseStreaming(string member)
    {
        var mapping = typeof(LoadableXmlAsset).Assembly.GetType("Verse.MemoryMappedFileSpanWrapper", true);
        MethodBase target = member == "FileSize" ? mapping.GetProperty(member)!.GetGetMethod()! : mapping.GetMethod(member)!;
        PublishedHookRefuses("AddPostfixes", target);
    }
    private void PublishedHookRefuses(string category, MethodBase target)
    {
        var file = Input(Encoding.UTF8.GetBytes("<Defs/>"));
        Type shared = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")!;
        var state = (Dictionary<MethodBase, byte[]>)AccessTools.Field(shared, "state").GetValue(null);
        byte[]? previous; lock (state) state.TryGetValue(target, out previous);
        Type serialization = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfoSerialization")!;
        Type infoType = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfo")!;
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out _), Is.True);
        try
        {
            object info = previous == null ? Activator.CreateInstance(infoType)!
                : AccessTools.Method(serialization, "Deserialize").Invoke(null, new object[] { previous });
            var hook = new HarmonyMethod(typeof(StreamingXmlTests), nameof(EmptyHook));
            AccessTools.Method(infoType, category).Invoke(info, new object[] { "foreign.xml.test", new[] { hook } });
            var published = (byte[])AccessTools.Method(serialization, "Serialize").Invoke(null, new[] { info });
            lock (state) state[target] = published;
            Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out _), Is.False);
        }
        finally { lock (state) { if (previous == null) state.Remove(target); else state[target] = previous; } }
        Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out _), Is.True);
    }
    private static void EmptyHook() { }

    [Test]
    public void OptionalRepresentativeCostProbe()
    {
        string? manifest = Environment.GetEnvironmentVariable("WAKE_UP_STREAMING_XML_PROBE_MANIFEST");
        if (string.IsNullOrEmpty(manifest)) Assert.Ignore("Optional CPU/input cost probe requires a representative file manifest.");
        var files = File.ReadAllLines(manifest!).Where(p => p.Length != 0).Select(p => new FileInfo(p)).ToArray();
        Assert.That(files.Length, Is.GreaterThan(0));
        foreach (var file in files)
        {
            Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out var document), Is.True, file.FullName);
            Assert.That(document.OuterXml, Is.EqualTo(FullText(file).OuterXml), file.FullName);
        }
        for (int round = 0; round < 6; round++)
            foreach (bool stream in round % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                var watch = Stopwatch.StartNew(); long characters = 0;
                foreach (var file in files)
                {
                    XmlDocument document;
                    if (stream) Assert.That(StreamingXmlRuntime.TryRead(file, Settings(), out document), Is.True);
                    else document = FullText(file);
                    characters += document.DocumentElement!.ChildNodes.Count;
                }
                TestContext.Out.WriteLine($"STREAMING_XML_COST round={round} streaming={stream} files={files.Length} ms={watch.Elapsed.TotalMilliseconds:F3} nodes={characters}");
            }
    }
}
