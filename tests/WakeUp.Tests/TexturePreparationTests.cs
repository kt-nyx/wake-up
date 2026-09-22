// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class TexturePreparationTests
{
    [Test]
    public void NativeColorRoleMatchesVerifiedConsumerBodiesAndExactPath()
    {
        for (int i = 0; i < PreparedColorRole.Targets.Length; i++)
        {
            Assert.That(SemanticMethodIdentity.TryHash(PreparedColorRole.Targets[i], out string body, out string reason), Is.True, reason);
            Assert.That(body, Is.EqualTo(PreparedColorRole.Bodies[i]));
        }
        var ctor = PatchProcessor.GetOriginalInstructions(PreparedColorRole.Targets[0]);
        Assert.That(ctor.Any(i => Equals(i.operand, "UI/HeroArt/BGPlanet")), Is.True);
        var draw = PatchProcessor.GetOriginalInstructions(PreparedColorRole.Targets[1]);
        Assert.That(draw.Any(i => i.operand is MethodInfo m && m.DeclaringType?.FullName == "UnityEngine.GUI" && m.Name == "DrawTexture"), Is.True);
        Assert.That(PreparedColorRole.MatchesPath("Textures/UI/HeroArt/BGPlanet.png"), Is.True);
        foreach (string unknown in new[] { "Textures/UI/HeroArt/Other.png", "Textures/Terrain/Thing.jpg", "Textures/Thing.png", "Textures/UI/HeroArt/BGPlanet_m.png" })
            Assert.That(PreparedColorRole.MatchesPath(unknown), Is.False);
    }

    [Test]
    public void SettingDefaultsOffAndSelectsOneExistingGuardedAdapter()
    {
        string root = Path.GetFullPath(Path.GetTempPath());
        var settings = new WakeUpSettings();
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, root).Any(a => a.StartsWith("--wake-up-prepared=")), Is.False);
        settings.PreparedTextures = true;
        foreach (int preset in new[] { -1, 0, 1, 2, 99 })
        {
            settings.PreparedPreset = preset;
            var args = UserStartupSelection.Resolve(Array.Empty<string>(), settings, root);
            Assert.That(args, Does.Contain("--wake-up-prepared=0"));
            Assert.That(args.Count(a => a.StartsWith("--wake-up-prepared=")), Is.EqualTo(1));
            Assert.That(args.Count(a => a.StartsWith("--wake-up-png=")), Is.EqualTo(1));
            Assert.That(args, Does.Contain("--wake-up-png=prepare"));
        }
        settings.PngCache = true;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, root), Does.Contain("--wake-up-png=cache"));
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, root), Is.Empty);
        settings.Enabled = true;
        Assert.That(UserStartupSelection.Resolve(new[] { "--wake-up-reference" }, settings, root), Is.EqualTo(new[] { "--wake-up-reference" }));
    }

    [TestCase("0", true)]
    [TestCase("1", false)]
    [TestCase("2", false)]
    [TestCase("-1", false)]
    [TestCase("invalid", false)]
    public void RuntimeAdmitsOnlyNativePreparation(string preset, bool expected)
    {
        string root = Path.GetFullPath(Path.GetTempPath());
        try
        {
            PreparedTextureRuntime.Initialize(root, root, new[] { "--wake-up-mode=candidate", "-savedatafolder=" + root, "--wake-up-prepared=" + preset });
            Assert.That(PreparedTextureRuntime.UseAtStartup, Is.EqualTo(expected));
            PreparedTextureRuntime.Initialize(root, root, new[] { "--wake-up-mode=candidate", "-savedatafolder=" + root, "--wake-up-prepared=0", "--wake-up-prepared=" + preset });
            Assert.That(PreparedTextureRuntime.UseAtStartup, Is.False, "Duplicate selectors must not admit prepared data.");
        }
        finally { PreparedTextureRuntime.FinishStartup(); }
    }

    [TestCase(".png", 1)]
    [TestCase(".dds", 0)]
    public void AbsentPreparedSlotAndRetiredDdsSkipPlatformAndSourceAccess(string extension, int expectedMisses)
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "prepared-admission-" + Guid.NewGuid().ToString("N"));
        long misses = PreparedTextureRuntime.Misses, refused = PreparedTextureRuntime.Refused;
        try
        {
            PreparedTextureRuntime.Initialize(root, root,
                new[] { "--wake-up-mode=candidate", "-savedatafolder=" + root, "--wake-up-prepared=0" });
            Assert.That(PreparedTextureRuntime.TryResolve(null!, "Textures/missing" + extension,
                new FileInfo(Path.Combine(root, "missing" + extension)), "provider"), Is.Null);
            Assert.That(PreparedTextureRuntime.Misses, Is.EqualTo(misses + expectedMisses));
            Assert.That(PreparedTextureRuntime.Refused, Is.EqualTo(refused),
                "No prepared slot must return an ordinary miss without Unity platform or missing-file access.");
        }
        finally
        {
            PreparedTextureRuntime.FinishStartup();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public void CacheConstructionRetainsNativeImageDecodeAndTextureConstructors()
    {
        var original = PatchProcessor.GetOriginalInstructions(PngRuntime.Image).ToArray();
        var rewritten = PngRuntime.ImageTranspiler(original).ToArray();
        Assert.That(rewritten.Where(i => i.operand is ConstructorInfo).Select(i => i.operand),
            Is.EqualTo(original.Where(i => i.operand is ConstructorInfo).Select(i => i.operand)));
        Assert.That(rewritten.Count(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "ImageConversion" && m.Name == "LoadImage"), Is.EqualTo(2));
        Assert.That(rewritten.Any(i => i.operand is MethodInfo m && m.Name == "FinalizeTexture"), Is.True, "native output capture remains for future warm reuse");
    }

    [Test]
    public void ActualEagerAndLoadingProgressPathsReachSharedResolverWithNativeFallback()
    {
        var route = PngRuntime.ReloadTranspiler(PatchProcessor.GetOriginalInstructions(PngRuntime.Reload)).ToArray();
        Assert.That(route.Any(i => i.operand is MethodInfo m && m.Name == "ReloadTextures"), Is.True);
        var enumerate = AccessTools.Method(typeof(PngRuntime), "EnumerateCore");
        var state = enumerate.GetCustomAttribute<System.Runtime.CompilerServices.IteratorStateMachineAttribute>()!.StateMachineType;
        var calls = PatchProcessor.GetOriginalInstructions(AccessTools.Method(state, "MoveNext")).Select(i => i.operand).OfType<MethodInfo>().ToArray();
        Assert.That(calls.Any(m => m == AccessTools.Method(typeof(PreparedTextureRuntime), nameof(PreparedTextureRuntime.TryResolve))), Is.True);
        Assert.That(calls.Any(m => m.Name == "LoadProcessedImage"), Is.True);
        Assert.That(calls.Any(m => m.Name == "LoadItem"), Is.True);
        var bridge = AccessTools.Method(typeof(PngRuntime), "ReloadLoadingProgressTextures");
        Assert.That(PatchProcessor.GetOriginalInstructions(bridge).Any(i => i.operand is MethodInfo m && m.Name == "ReloadTexturesCore"), Is.True);
    }

    [Test]
    public void NativeFolderOverrideAndDdsSelectionIncludingJpegArePreserved()
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "selection-" + Guid.NewGuid().ToString("N"));
        string high = Path.Combine(root, "1.6"), low = Path.Combine(root, "Common");
        Directory.CreateDirectory(Path.Combine(high, "Textures")); Directory.CreateDirectory(Path.Combine(low, "Textures"));
        foreach (string name in new[] { "same.png", "rgb.png", "rgb.dds", "photo.jpeg", "photo.dds", "odd.jpeg", "odd..dds" }) File.WriteAllBytes(Path.Combine(high, "Textures", name), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(low, "Textures", "same.png"), new byte[] { 2 });
        var mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        mod.foldersToLoadDescendingOrder = new List<string> { high, low };
        var selected = PngRuntime.SelectedFiles(mod).ToDictionary(p => Path.GetFileName(p.Key), p => p.Value);
        Assert.That(selected["same.png"].FullName, Is.EqualTo(Path.Combine(high, "Textures", "same.png")));
        Assert.That(selected.ContainsKey("rgb.png"), Is.False);
        Assert.That(selected.ContainsKey("photo.jpeg"), Is.True, "Native four-character removal does not map photo.jpeg to photo.dds.");
        Assert.That(selected.ContainsKey("odd.jpeg"), Is.False, "Preserve native odd..dds selection rather than repairing it.");
    }

    [Test]
    public void FullContentOptionsHelperAndSelectionChangeIdentityWithoutTimestampDependence()
    {
        string path = Path.Combine(Path.GetTempPath(), "same.png");
        string Key(string selection, byte[] bytes, int preset, string helper, string platform = "platform")
            => PreparedTextureRuntime.Identity(selection, "Textures/same.png", path, bytes, preset, platform, helper);
        var bytes = new byte[] { 1, 2 };
        var keys = new[] { Key("provider-A", bytes, 1, "helper-A"), Key("provider-B", bytes, 1, "helper-A"),
            Key("provider-A", new byte[] { 1, 3 }, 1, "helper-A"), Key("provider-A", bytes, 2, "helper-A"),
            Key("provider-A", bytes, 1, "helper-B"), Key("provider-A", bytes, 1, "helper-A", "changed-platform") };
        Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Length));
        Assert.That(PreparedTextureRuntime.Frame("a|b", "c"), Is.Not.EqualTo(PreparedTextureRuntime.Frame("a", "b|c")));
        Assert.That(keys[0], Does.Contain("helper-A"));
        string native = Key("provider-A", bytes, 0, "");
        Assert.That(keys, Does.Not.Contain(native), "Old lossy entries cannot be selected by the retained native identity.");
        Assert.That(Key("provider-A", bytes, 0, "changed-helper"), Is.EqualTo(native), "Native preparation does not require a helper.");
    }

    [Test]
    public void EarlierProviderMaskAdditionInvalidatesBothReuseAndPublicationWithoutRescan()
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "eligibility-" + Guid.NewGuid().ToString("N"));
        string early = Path.Combine(root, "early"), later = Path.Combine(root, "later");
        string logical = "Textures/UI/HeroArt/BGPlanet.png";
        string source = Path.Combine(later, logical);
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllBytes(source, new byte[] { 1 });
        var eligibility = new PreparedTextureEligibility(new[] { early, later }, "unchanged-roots-and-order");
        // The real mask admission and store boundaries; the independently tested
        // exact role match replaces the game's loaded Harmony environment here.
        var entry = new PngCache.Entry { Width = 4, Height = 4, Mips = 3, TextureFormat = 12,
            GraphicsFormat = 100, Filter = 2, Aniso = 2, Pixels = new byte[48] };
        using var store = new PreparedTextureStore(Path.Combine(root, "cache"));
        Assert.That(eligibility.Publish(store, "before", logical, entry, PreparedColorRole.MatchesPath), Is.True);
        Assert.That(eligibility.Read(store, "before", logical, PreparedColorRole.MatchesPath), Is.Not.Null);
        string mask = Path.Combine(early, "Textures/UI/HeroArt/BGPlanet_m.PNG");
        Directory.CreateDirectory(Path.GetDirectoryName(mask)!);
        File.WriteAllBytes(mask, new byte[] { 2 });
        // Same captured folder set, same B source, same prepared identity. Only A
        // gains a mask after the initial snapshot, including a new directory.
        Assert.That(eligibility.Read(store, "before", logical, PreparedColorRole.MatchesPath), Is.Null);
        Assert.That(eligibility.Publish(store, "after", logical, entry, PreparedColorRole.MatchesPath), Is.False);
        Assert.That(store.Read("after"), Is.Null);
        Assert.That(File.ReadAllBytes(source), Is.EqualTo(new byte[] { 1 }));
    }

    [Test]
    public void NativeResolverCannotConsumeHelperExports()
    {
        var resolver = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(PreparedTextureRuntime), "TryResolve"));
        Assert.That(resolver.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(TexturePreparationHelper)), Is.False);
        Assert.That(resolver.Any(i => i.operand is MethodInfo m && m.Name == "ReadExport"), Is.False);
    }

    [TestCase(0, 4)]
    [TestCase(8192, 8192)]
    public void HeaderRejectsDangerousDecodedDimensionsBeforeWork(int width, int height)
        => Assert.Throws<InvalidDataException>((Action)delegate { _ = PreparationImageHeader.Read(new MemoryStream(PngHeader(width, height))); });

    [Test]
    public void HeaderReadsDimensionsAndRejectsUnknownColorMetadataForLossyOnly()
    {
        var png = PngHeader(64, 32);
        var header = PreparationImageHeader.Read(new MemoryStream(png));
        Assert.That(header.Width, Is.EqualTo(64)); Assert.That(header.FullMips, Is.EqualTo(7));
        Assert.That(header.LossyFits(2), Is.True);
        png[25] = 3; // palette: native preservation remains available but explicit lossy is unknown
        Assert.That(PreparationImageHeader.Read(new MemoryStream(png)).LossyFits(1), Is.False);
    }
    private static byte[] PngHeader(int width, int height)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        w.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Big(int x) => w.Write(new[] { (byte)(x >> 24), (byte)(x >> 16), (byte)(x >> 8), (byte)x });
        Big(13); w.Write(Encoding.ASCII.GetBytes("IHDR")); Big(width); Big(height);
        w.Write(new byte[] { 8, 6, 0, 0, 0 }); Big(0);
        Big(0); w.Write(Encoding.ASCII.GetBytes("IDAT")); Big(0);
        return stream.ToArray();
    }

    [TestCase(5, 56)] // mip omission
    [TestCase(3, int.MaxValue)] // malicious length before allocation
    [TestCase(3, 55)] // wrong block tail layout
    public void HelperResponseRejectsBadMipsAndLengths(int mips, int length)
    {
        using var output = new MemoryStream();
        using (var w = new BinaryWriter(output, Encoding.ASCII, true))
        { w.Write(Encoding.ASCII.GetBytes("WUTXRES2")); foreach (int n in new[] { 0, 4, 4, 12, mips, length }) w.Write(n); }
        output.Position = 0;
        Assert.Throws<InvalidDataException>((Action)delegate { _ = TexturePreparationHelper.ReadResponse(new BinaryReader(output), new PreparationImageHeader { Width = 4, Height = 4 }, 1); });
    }

    [Test]
    public void MissingHelperFailsBeforeChildAndDoesNotNeedOriginalFiles()
        => Assert.Throws<IOException>((Action)delegate { _ = TexturePreparationHelper.Identity(Path.Combine(TestContext.CurrentContext.WorkDirectory, "missing-helper.exe")); });

    [Test]
    public void BuiltHelperHandshakesAndReturnsValidatedImageThroughManagedClient()
    {
        string? exe = Environment.GetEnvironmentVariable("WAKE_UP_TEXTURE_HELPER_TEST_PATH");
        string? input = Environment.GetEnvironmentVariable("WAKE_UP_TEXTURE_HELPER_TEST_IMAGE");
        if (exe == null || input == null) Assert.Ignore("Explicit local helper/image paths not supplied.");
        byte[] bytes = File.ReadAllBytes(input!);
        var header = PreparationImageHeader.Read(new MemoryStream(bytes));
        string identity = TexturePreparationHelper.Identity(exe!);
        var entry = TexturePreparationHelper.Convert(exe!, identity, bytes, header, 1, CancellationToken.None);
        Assert.That(entry.Mips, Is.EqualTo(header.FullMips));
        Assert.That(entry.Pixels.Length, Is.EqualTo(PngCache.ExpectedBytes(entry.Width, entry.Height, entry.TextureFormat, entry.Mips)));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>((Action)delegate { _ = TexturePreparationHelper.Convert(exe!, identity, bytes, header, 1, cancel.Token); });
        Assert.Throws<InvalidDataException>((Action)delegate { _ = TexturePreparationHelper.Convert(exe!, identity + "changed", bytes, header, 1, CancellationToken.None); });
        using var during = new CancellationTokenSource(); int ownedPid = 0;
        Assert.Throws<OperationCanceledException>((Action)delegate { _ = TexturePreparationHelper.Convert(exe!, identity, bytes, header, 1, during.Token,
            process => { ownedPid = process.Id; during.Cancel(); }); });
        Assert.That(ownedPid, Is.GreaterThan(0));
        Assert.Throws<ArgumentException>((Action)delegate { _ = System.Diagnostics.Process.GetProcessById(ownedPid); }, "Owned child must be reaped before resume.");
        Assert.That(TexturePreparationHelper.Convert(exe!, identity, bytes, header, 1, CancellationToken.None).Pixels, Is.EqualTo(entry.Pixels));
        // The same public export path crosses the real child, independently
        // validated DDS, source-keyed publication, reopen and reusable file.
        string exportRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, "actual-helper-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] dds = TexturePreparationHelper.ConvertDds(exe!, identity, bytes, header, 2, CancellationToken.None);
            string key = TexturePreparationBatch.ExportIdentity("native-selected-provider", "Textures/color.png", input!, bytes, 2, identity);
            using (var store = new PreparedTextureStore(exportRoot))
                Assert.That(TexturePreparationBatch.PublishExport(store, key, "Textures/color.png", dds,
                    () => File.ReadAllBytes(input!).SequenceEqual(bytes), CancellationToken.None), Is.EqualTo("prepared DDS export"));
            using var reopened = new PreparedTextureStore(exportRoot);
            Assert.That(reopened.ReadExport(key), Is.EqualTo(dds));
            Assert.That(PreparedDds.Parse(reopened.ReadExport(key)!).Width, Is.EqualTo(header.Width / 2));
            Assert.That(reopened.Read(key), Is.Null);
        }
        finally { if (Directory.Exists(exportRoot)) Directory.Delete(exportRoot, true); }
    }
}
