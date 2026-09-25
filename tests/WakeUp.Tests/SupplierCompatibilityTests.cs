// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class SupplierCompatibilityTests
{
    private string root = "";
    [SetUp] public void SetUp()
    { root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "supplier-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TearDown] public void TearDown() { Directory.Delete(root, true); }

    [Test] public void SupplierSourceRequiresUniqueHashAndLoadedModuleIdentity()
    {
        var assembly = typeof(SupplierCompatibilityTests).Assembly;
        string copy = Path.Combine(root, "supplier.dll"), duplicate = Path.Combine(root, "duplicate.dll");
        File.Copy(assembly.Location, copy); File.Copy(copy, duplicate);
        string Hash(string file) { using var sha = System.Security.Cryptography.SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant(); }
        string hash = Hash(copy);
        Assert.That(LoaderSupplierPolicy.MatchSourceFile(assembly, hash, new[] { copy }), Is.EqualTo(copy));
        Assert.That(LoaderSupplierPolicy.MatchSourceFile(assembly, hash, new[] { copy, duplicate }), Is.Null);
        Assert.That(LoaderSupplierPolicy.MatchSourceFile(assembly, new string('0', 64), new[] { copy }), Is.Null);
        using (var module = ModuleDefinition.ReadModule(copy)) { module.Mvid = Guid.NewGuid(); module.Write(duplicate); }
        Assert.That(LoaderSupplierPolicy.MatchSourceFile(assembly, Hash(duplicate), new[] { duplicate }), Is.Null);
    }

    [Test]
    public void SupplierAdvisoryPersistsWithoutInventingDisabledWakeUpWork()
    {
        string file = Path.Combine(root, "notices.xml");
        var registry = new OperationRegistry(file, _ => { });
        registry.Advise("conflicting-options", "Supplier", "Fix supplier settings.");
        Assert.That(registry.Snapshot(), Is.Empty);
        var restarted = new OperationRegistry(file, _ => { });
        Assert.That(restarted.Pending().Select(n => n.Key), Is.EqualTo(new[] { "advisory|conflicting-options|Supplier" }));
        restarted.Advise("conflicting-options", "Supplier", "Changed wording.");
        Assert.That(restarted.Pending(), Has.Length.EqualTo(1));
        Assert.That(restarted.Acknowledge(restarted.Pending().Select(n => n.Key)), Is.True);
        restarted = new OperationRegistry(file, _ => { });
        restarted.Advise("conflicting-options", "Supplier", "Changed wording.");
        Assert.That(restarted.Pending(), Is.Empty);
        restarted.Advise("different-options", "Supplier", "A different problem.");
        Assert.That(restarted.Pending(), Has.Length.EqualTo(1));
    }

    [Test]
    public void ChangingDisplaySupplierRequiresANewAcknowledgement()
    {
        var property = typeof(CompatibilityStatus).GetProperty("Registry", BindingFlags.Static | BindingFlags.NonPublic)!;
        var saved = property.GetValue(null);
        var registry = new OperationRegistry(Path.Combine(root, "notices.xml"), _ => { });
        try
        {
            property.SetValue(null, registry);
            registry.Request("display", "Loading display", true);
            foreach (string provider in new[] { "Loading Progress", "RimThemes", "RimThemes + Loading Progress" })
            {
                LifecycleSupplierPolicy.Refuse("display", "Choose a display provider.",
                    new SupplierConflictException("Choose a display provider.", provider, "supplier-display"));
                Assert.That(registry.Pending().Select(n => n.Key), Is.EqualTo(new[] { "display|supplier-display|" + provider }));
                Assert.That(registry.Acknowledge(registry.Pending().Select(n => n.Key)), Is.True);
            }
        }
        finally { property.SetValue(null, saved); }
    }

    [Test] public void ForeignPreferencesAreReadWithNativeDefaultsAndNeverWritten()
    {
        string file = Path.Combine(root, "settings.xml");
        Assert.That(LoaderSupplierPolicy.ReadDefaultOn(LoaderSupplierPolicy.ReadSettings(file, "Supplier.Settings"), "preload"), Is.True);
        string text = "<SettingsBlock><ModSettings Class='Supplier.Settings'><preload>false</preload></ModSettings></SettingsBlock>";
        File.WriteAllText(file, text);
        var node = LoaderSupplierPolicy.ReadSettings(file, "Supplier.Settings");
        Assert.That(LoaderSupplierPolicy.ReadDefaultOn(node, "preload"), Is.False);
        Assert.That(LoaderSupplierPolicy.ReadDefaultOn(node, "reuse"), Is.True);
        Assert.That(File.ReadAllText(file), Is.EqualTo(text));
        Assert.Throws<InvalidDataException>((Action)(() => LoaderSupplierPolicy.ReadSettings(file, "Wrong.Settings")));
        File.WriteAllText(file, text.Replace("false", "invalid"));
        Assert.Throws<InvalidDataException>((Action)(() => LoaderSupplierPolicy.ReadDefaultOn(LoaderSupplierPolicy.ReadSettings(file, "Supplier.Settings"), "preload")));
    }
    [TestCase("taranchuk.fastergameloading", true)]
    [TestCase("other.IMAGEOPT.variant", true)]
    [TestCase("zz.loadingprogress", true)]
    [TestCase("vopaga.hyperdrive", true)]
    [TestCase("sz.yaopt", false)]
    [TestCase("unrelated.mod", false)]
    public void PreloadReservationUsesSupplierExclusionsOnly(string package, bool excluded)
        => Assert.That(LoaderSupplierPolicy.ContentExcluded(new[] { package }), Is.EqualTo(excluded));

    [TestCase(true)] [TestCase(false)]
    public void AcknowledgedAutomaticYieldDoesNotHideNewExplicitAction(bool xml)
    {
        string file = Path.Combine(root, "notices.xml");
        var registry = new OperationRegistry(file, TestContext.Out.WriteLine);
        string automatic = LoaderSupplierPolicy.DecisionCode(xml, true, LoaderChoice.Automatic);
        registry.Request("feature", "Feature", true);
        registry.Set("feature", OperationState.Unavailable, "Yield automatically", automatic, "WOWGAG");
        Assert.That(registry.Acknowledge(registry.Pending().Select(n => n.Key)), Is.True);
        var restarted = new OperationRegistry(file, TestContext.Out.WriteLine);
        restarted.Request("feature", "Feature", true);
        restarted.Set("feature", OperationState.Unavailable, "Yield automatically", automatic, "WOWGAG");
        Assert.That(restarted.Pending(), Is.Empty);
        string action = LoaderSupplierPolicy.DecisionCode(xml, true, LoaderChoice.WakeUp);
        restarted.Set("feature", OperationState.Unavailable, "Disable supplier setting and restart", action, "WOWGAG");
        Assert.That(restarted.Pending(), Has.Length.EqualTo(1));
        Assert.That(restarted.Pending()[0].Key, Does.Contain("action-required"));
        restarted.Set("feature", OperationState.Unavailable, "Same action, new build", action, "WOWGAG");
        Assert.That(restarted.Pending(), Has.Length.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void LoadingProgressOnlySuppressesDisplayOnlyObservation(bool reports)
    {
        var property = typeof(CompatibilityStatus).GetProperty("Registry", BindingFlags.Static | BindingFlags.NonPublic)!;
        var saved = property.GetValue(null);
        var registry = new OperationRegistry(Path.Combine(root, "notices.xml"), _ => { });
        try
        {
            property.SetValue(null, registry);
            registry.Request("observation", "Observation", true); registry.Request("invocations", "Invocations", true);
            Assert.That(LoadingObservationRuntime.SelectObservation(true, reports, true), Is.EqualTo(reports));
            if (!reports)
            {
                Assert.That(registry.Snapshot().All(s => s.Contains("Unavailable")), Is.True);
                Assert.That(registry.Snapshot().Any(s => s.Contains("AwaitingStage")), Is.False);
            }
            else Assert.That(registry.Pending(), Is.Empty, "Reports still require actual observer setup.");
        }
        finally { property.SetValue(null, saved); }
    }

    private static void ForeignPrefix() { }
    private static IEnumerable<CodeInstruction> ForeignTranspiler(IEnumerable<CodeInstruction> code) => code;
    private static int workerTranspilerRuns;
    private static IEnumerable<CodeInstruction> CountWorkerTranspiler(IEnumerable<CodeInstruction> code)
    { workerTranspilerRuns++; return code; }
    [Test] public void EightOccupiedWorkersRetainRemoveReplaceAndSetName()
    {
        var foreign = new Harmony("WakeUp.Tests.YaOptWorkers"); var own = new Harmony("wakeup.def-lookup");
        var targets = (System.Collections.IList)AccessTools.Field(typeof(DefLookupRuntime), "Targets").GetValue(null);
        var guards = (System.Collections.IList)AccessTools.Field(typeof(DefLookupRuntime), "Guards").GetValue(null);
        Assert.That(targets, Is.Empty);
        var occupied = new[] { "Add", "AddModExtension", "Insert", "AttributeAdd", "AttributeRemove", "AttributeSet", "Test", "Conditional" };
        try
        {
            foreach (string name in occupied)
                foreign.Patch(AccessTools.Method(typeof(PatchOperation).Assembly.GetType("Verse.PatchOperation" + name), "ApplyWorker"),
                    transpiler: new HarmonyMethod(typeof(SupplierCompatibilityTests), nameof(CountWorkerTranspiler)));
            int publications = workerTranspilerRuns;
            foreach (string name in occupied.Concat(new[] { "Remove", "Replace", "SetName" }))
                AccessTools.Method(typeof(DefLookupRuntime), "InstallWorker").Invoke(null, new object[] { own, typeof(PatchOperation).Assembly.GetType("Verse.PatchOperation" + name)! });
            Assert.That(targets.Cast<MethodBase>().Select(m => m.DeclaringType!.Name),
                Is.EquivalentTo(new[] { "PatchOperationRemove", "PatchOperationReplace", "PatchOperationSetName" }));
            Assert.That(workerTranspilerRuns, Is.EqualTo(publications), "Admission refusals must not rebuild supplier methods.");
        }
        finally { own.UnpatchAll(own.Id); foreign.UnpatchAll(foreign.Id); targets.Clear(); guards.Clear(); }
    }
    private sealed class WorkerWithoutXPath
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public bool ApplyWorker(XmlDocument document) => document != null;
    }
    [Test] public void NativeWorkerWithoutXPathIsRefusedBeforeRegistration()
    {
        var own = new Harmony("wakeup.def-lookup");
        var targets = (System.Collections.IList)AccessTools.Field(typeof(DefLookupRuntime), "Targets").GetValue(null);
        var guards = (System.Collections.IList)AccessTools.Field(typeof(DefLookupRuntime), "Guards").GetValue(null);
        var worker = AccessTools.Method(typeof(WorkerWithoutXPath), "ApplyWorker");
        Assert.That(targets, Is.Empty); Assert.That(guards, Is.Empty);
        Assert.That(PublishedPatchGuard.TryCreate(worker, own.Id, out var guard) && guard!.AllowsOriginalContract(), Is.True);
        try
        {
            AccessTools.Method(typeof(DefLookupRuntime), "InstallWorker").Invoke(null, new object[] { own, typeof(WorkerWithoutXPath) });
            Assert.That(targets, Is.Empty); Assert.That(guards, Is.Empty);
            Assert.That(Harmony.GetPatchInfo(worker)?.Transpilers.Any(p => p.owner == own.Id) ?? false, Is.False);
            Assert.That(new WorkerWithoutXPath().ApplyWorker(new XmlDocument()), Is.True);
        }
        finally { own.UnpatchAll(own.Id); targets.Clear(); guards.Clear(); }
    }
    private static int boundaryChecks, deferredCalls;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int BoundaryTarget() => 17;
    private static void SupplierBoundaryCheck()
    {
        if (LoaderSupplierPolicy.Patches(AccessTools.Method(typeof(SupplierCompatibilityTests), nameof(BoundaryTarget)))
            .Any(p => p.owner == "wakeup.supplier-selection")) throw new InvalidOperationException("Selection observer still blocks supplier");
        if (deferredCalls != 1) throw new InvalidOperationException("Deferred initialization has not completed");
        boundaryChecks++;
    }
    [Test] public void MissingEarlyBridgeUsesFutureBoundaryAndRemovesObserverBeforeSupplierChecks()
    {
        var saved = typeof(LoaderSupplierPolicy).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => !f.IsInitOnly && !f.IsLiteral).ToDictionary(f => f, f => f.GetValue(null));
        var harmony = new Harmony(LoaderSupplierPolicy.WowXmlOwner);
        var target = AccessTools.Method(typeof(SupplierCompatibilityTests), nameof(BoundaryTarget));
        try
        {
            foreach (string field in new[] { "initialized", "defer" }) AccessTools.Field(typeof(LoaderSupplierPolicy), field).SetValue(null, true);
            foreach (string field in new[] { "frozen", "wowPresent", "reservedContent" }) AccessTools.Field(typeof(LoaderSupplierPolicy), field).SetValue(null, false);
            AccessTools.Field(typeof(LoaderSupplierPolicy), "wow").SetValue(null, null);
            boundaryChecks = deferredCalls = 0;
            LoaderSupplierPolicy.Run("test boundary", () => deferredCalls++);
            Assert.That(deferredCalls, Is.Zero);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(SupplierCompatibilityTests), nameof(SupplierBoundaryCheck)) { priority = 801 });
            LoaderSupplierPolicy.InstallBoundary(target);
            Assert.That(BoundaryTarget(), Is.EqualTo(17));
            Assert.That(boundaryChecks, Is.EqualTo(1));
            Assert.That(LoaderSupplierPolicy.Frozen, Is.True);
            LoaderSupplierPolicy.BeforeXml();
            Assert.That(deferredCalls, Is.EqualTo(1), "The existing wrapper and fallback cannot initialize twice.");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id); new Harmony("wakeup.supplier-selection").UnpatchAll("wakeup.supplier-selection");
            foreach (var value in saved) value.Key.SetValue(null, value.Value);
        }
    }
    [Test] public void UnknownEntryPointUsesActualHooksAndDoesNotInventDisabledFeatures()
    {
        var xml = new Harmony(LoaderSupplierPolicy.WowXmlOwner);
        var content = new Harmony(LoaderSupplierPolicy.WowContentOwner);
        try
        {
            LoaderSupplierPolicy.ResolveInstalled(true, null);
            Assert.That(LoaderSupplierPolicy.YieldXml || LoaderSupplierPolicy.YieldContent, Is.False);
            xml.Patch(AccessTools.Method(typeof(LoadedModManager), "LoadModXML"), prefix: new HarmonyMethod(typeof(SupplierCompatibilityTests), nameof(ForeignPrefix)));
            LoaderSupplierPolicy.ResolveInstalled(true, null);
            Assert.That(LoaderSupplierPolicy.YieldXml, Is.True); Assert.That(LoaderSupplierPolicy.YieldContent, Is.False);
            Assert.That(LoaderSupplierPolicy.Reason(true), Does.Contain("partial"));
            content.Patch(AccessTools.Method(typeof(ModContentPack), "ReloadContentInt"), prefix: new HarmonyMethod(typeof(SupplierCompatibilityTests), nameof(ForeignPrefix)));
            LoaderSupplierPolicy.ResolveInstalled(true, null);
            Assert.That(LoaderSupplierPolicy.YieldContent, Is.True);
            content.UnpatchAll(content.Id); xml.UnpatchAll(xml.Id);
            LoaderSupplierPolicy.ResolveInstalled(true, null);
            Assert.That(LoaderSupplierPolicy.YieldXml || LoaderSupplierPolicy.YieldContent, Is.False);
        }
        finally { content.UnpatchAll(content.Id); xml.UnpatchAll(xml.Id); LoaderSupplierPolicy.ResolveInstalled(false, null); }
    }
    [Test] public void SingleQueryObserverDoesNotBlockMultiNodeWorkers()
    {
        var harmony = new Harmony("WakeUp.Tests.SingleSupplier");
        MethodInfo single = typeof(XmlNode).GetMethod("SelectSingleNode", new[] { typeof(string) })!;
        MethodInfo nodes = typeof(XmlNode).GetMethod("SelectNodes", new[] { typeof(string) })!;
        Assert.That(PublishedPatchGuard.TryCreate(single, "wakeup.def-lookup", out var singleGuard, allPatchKinds: true), Is.True);
        Assert.That(PublishedPatchGuard.TryCreate(nodes, "wakeup.def-lookup", out var nodesGuard, allPatchKinds: true), Is.True);
        try
        {
            Assert.That(singleGuard!.AllowsOriginalContract(), Is.True);
            Assert.That(SingleWorkerQueryPolicy.CanInstall(), Is.True);
            harmony.Patch(single, prefix: new HarmonyMethod(typeof(SupplierCompatibilityTests), nameof(ForeignPrefix)));
            Assert.That(singleGuard.AllowsOriginalContract(), Is.False);
            Assert.That(SingleWorkerQueryPolicy.CanUse(), Is.False);
            Assert.That(nodesGuard!.AllowsOriginalContract(), Is.True);
            harmony.UnpatchAll(harmony.Id);
            Assert.That(singleGuard.AllowsOriginalContract(), Is.True, "First call after removal sees the new publication.");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static class PreviewQuery
    {
        [ThreadStatic] private static bool isInPatchOperationValue;
        public static bool isInPatchOperation { get => isInPatchOperationValue; set => isInPatchOperationValue = value; }
        public static void Prefix() { }
        public static void Postfix() { }
    }
    private static class PreviewStage
    {
        public static void Prefix() => PreviewQuery.isInPatchOperation = true;
        public static void Postfix() => PreviewQuery.isInPatchOperation = false;
        public static void Finalizer() => PreviewQuery.isInPatchOperation = false;
    }
    [Test] public void PreviewAdapterRequiresActualThreadScopeAndRejectsLaterCallbackHooks()
    {
        SingleWorkerQueryPolicy.CanInstall();
        var saved = typeof(SingleWorkerQueryPolicy).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => !f.IsInitOnly && !f.IsLiteral).ToDictionary(f => f, f => f.GetValue(null));
        var guards = (List<PublishedPatchGuard>)AccessTools.Field(typeof(SingleWorkerQueryPolicy), "guards").GetValue(null);
        var savedGuards = guards.ToArray();
        var supplier = new Harmony("FasterGameLoadingMod"); var foreign = new Harmony("WakeUp.Tests.LateScope");
        try
        {
            Assert.That(SingleWorkerQueryPolicy.TryBindPreview(typeof(PreviewQuery), typeof(PreviewStage)), Is.True);
            supplier.Patch(typeof(XmlNode).GetMethod("SelectSingleNode", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(PreviewQuery), "Prefix"), postfix: new HarmonyMethod(typeof(PreviewQuery), "Postfix"));
            supplier.Patch(AccessTools.Method(typeof(LoadedModManager), "ApplyPatches"), prefix: new HarmonyMethod(typeof(PreviewStage), "Prefix"),
                postfix: new HarmonyMethod(typeof(PreviewStage), "Postfix"), finalizer: new HarmonyMethod(typeof(PreviewStage), "Finalizer"));
            Assert.That(SingleWorkerQueryPolicy.CanInstall(), Is.True);
            Assert.That(SingleWorkerQueryPolicy.CanUse(), Is.False);
            PreviewStage.Prefix();
            Assert.That(SingleWorkerQueryPolicy.CanUse(), Is.True);
            Assert.That(System.Threading.Tasks.Task.Run(SingleWorkerQueryPolicy.CanUse).Result, Is.False, "Another thread does not inherit the scope.");
            foreign.Patch(AccessTools.PropertyGetter(typeof(PreviewQuery), "isInPatchOperation"),
                prefix: new HarmonyMethod(typeof(SupplierCompatibilityTests), nameof(ForeignPrefix)));
            Assert.That(SingleWorkerQueryPolicy.CanUse(), Is.False);
        }
        finally
        {
            PreviewStage.Finalizer(); foreign.UnpatchAll(foreign.Id); supplier.UnpatchAll(supplier.Id);
            guards.Clear(); guards.AddRange(savedGuards);
            foreach (var item in saved) item.Key.SetValue(null, item.Value);
        }
    }

    [Test] public void TopLevelHookDoesNotDisableBundleGuardButBundleHookDoes()
    {
        var harmony = new Harmony("WakeUp.Tests.BundleSupplier");
        var get = AccessTools.Method(typeof(ContentFinder<string>), "Get");
        var bundle = AccessTools.Method(typeof(ContentFinder<string>), "TryFindAssetInModBundles");
        var top = new AssetRoutingPatchGuard(); var bundles = new AssetRoutingPatchGuard(bundlesOnly: true);
        var stub = AccessTools.Method(typeof(SupplierCompatibilityTests), nameof(BoundaryTarget));
        var state = (Dictionary<MethodBase, byte[]>)typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")!
            .GetField("state", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        state.TryGetValue(get, out var oldGet); state.TryGetValue(bundle, out var oldBundle);
        try
        {
            Assert.That(top.Allows(), Is.True); Assert.That(bundles.Allows(), Is.True);
            // Desktop CLR cannot detour this native generic. Publish a real
            // Harmony record under its closed key to exercise the real guard.
            // This is a publication/guard test, not a Mono detour claim.
            harmony.Patch(stub, prefix: new HarmonyMethod(typeof(SupplierCompatibilityTests), nameof(ForeignPrefix)));
            lock (state) state[get] = state[stub];
            Assert.That(top.Allows(), Is.False); Assert.That(bundles.Allows(), Is.True);
            lock (state) state[bundle] = state[stub];
            Assert.That(bundles.Allows(), Is.False);
        }
        finally
        {
            lock (state)
            {
                if (oldGet == null) state.Remove(get); else state[get] = oldGet;
                if (oldBundle == null) state.Remove(bundle); else state[bundle] = oldBundle;
            }
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [TestCase(true)] [TestCase(false)]
    public void YaOptStyleWrapperSurvivesBothPrepatchOrders(bool supplierFirst)
    {
        using var module = ModuleDefinition.ReadModule(typeof(ContentFinder<>).Assembly.Location);
        var get = AssetRoutingPrepatch.FindGet(module)!;
        var bundle = module.GetType("Verse.ContentFinder`1").Methods.Single(m => m.Name == "TryFindAssetInModBundles");
        if (!supplierFirst) Assert.That(AssetRoutingPrepatch.RewriteAssembly(module), Is.True);
        var original = get.Body.Instructions[0];
        var foreign = new TypeReference("YaOpt.Patches.Prepatch", "Verse_ContentFinder_Get", module,
            new AssemblyNameReference("YaOpt", new Version(1, 1, 4, 0)));
        var enabled = new FieldReference("Enabled", module.TypeSystem.Boolean, foreign);
        var call = new MethodReference("GetContent", module.TypeSystem.Object, new TypeReference("YaOpt.Helpers", "ContentManager", module, foreign.Scope));
        var prefix = new[] { Instruction.Create(OpCodes.Ldsfld, enabled), Instruction.Create(OpCodes.Brfalse, original),
            Instruction.Create(OpCodes.Ldtoken, get.DeclaringType.GenericParameters[0]),
            Instruction.Create(OpCodes.Call, module.ImportReference(typeof(Type).GetMethod("GetTypeFromHandle")!)),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Call, call),
            Instruction.Create(OpCodes.Unbox_Any, get.DeclaringType.GenericParameters[0]), Instruction.Create(OpCodes.Ret) };
        foreach (var i in prefix) get.Body.GetILProcessor().InsertBefore(original, i);
        string wrapped = AssetRoutingPrepatch.Fingerprint(get);
        Assert.That(AssetRoutingPrepatch.RewriteAssembly(module), Is.EqualTo(supplierFirst));
        Assert.That(AssetRoutingPrepatch.Fingerprint(get), Is.EqualTo(wrapped), "Foreign top-level wrapper and its completion call remain untouched.");
        Assert.That(bundle.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "TryBundles"), Is.EqualTo(1));
        Assert.That(get.Body.Instructions.Take(prefix.Length), Is.EqualTo(prefix));
    }

    [Test] public void FailedBundleInjectionRestoresOwnMutationAndKeepsEarlierGet()
    {
        using var module = ModuleDefinition.ReadModule(typeof(ContentFinder<>).Assembly.Location);
        var get = AssetRoutingPrepatch.FindGet(module)!;
        AssetRoutingPrepatch.Inject(module, get);
        string beforeGet = AssetRoutingPrepatch.Fingerprint(get);
        var bundle = module.GetType("Verse.ContentFinder`1").Methods.Single(m => m.Name == "TryFindAssetInModBundles");
        string beforeBundle = AssetRoutingPrepatch.Fingerprint(bundle);
        var references = module.AssemblyReferences.ToArray();
        bool changed = AssetRoutingPrepatch.TryInject(module, bundle, AssetRoutingPrepatch.NativeBundleBody, (m, method) => {
            AssetRoutingPrepatch.InjectBundle(m, method);
            m.AssemblyReferences.Add(new AssemblyNameReference("FailedAttempt", new Version(1, 0)));
            throw new InvalidOperationException("Injected failure after mutation");
        });
        Assert.That(changed, Is.False); Assert.That(AssetRoutingPrepatch.Fingerprint(bundle), Is.EqualTo(beforeBundle));
        Assert.That(AssetRoutingPrepatch.Fingerprint(get), Is.EqualTo(beforeGet));
        Assert.That(module.AssemblyReferences, Is.EqualTo(references));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int BodyWithLocalsAndFinally(int value)
    {
        var values = new List<int>();
        try { values.Add(value); return values.Count; }
        finally { values.Clear(); }
    }
    private static volatile bool volatileProbe;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool BodyWithVolatile(bool value) { volatileProbe = value; return volatileProbe; }
    private static Type GameReferenceType() => typeof(LoadableXmlAsset);
    private static Type HarmonyReferenceType() => typeof(AccessTools.FieldRef<SupplierCompatibilityTests, List<int>>);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double BodyWithNumericAndSwitchOperands(int value)
    {
        switch (value)
        {
            case 0: return value + 123456789;
            case 1: return value + 123456789012345L;
            case 2: return value + 1.25f;
            case 3: return value + 2.125;
            case 4: return value - 29;
            default: return value > 8 ? value - 123 : value + 123;
        }
    }
    [TestCase(nameof(BodyWithNumericAndSwitchOperands))]
    [TestCase(nameof(BodyWithLocalsAndFinally))]
    public void SupplierInstructionsRejectEachChangedNonTokenOperandByte(string name)
    {
        var method = AccessTools.Method(typeof(SupplierCompatibilityTests), name);
        using var source = ModuleDefinition.ReadModule(method.Module.Assembly.Location);
        var original = (MethodDefinition)source.LookupToken(method.MetadataToken);
        byte[] bytes = method.GetMethodBody()!.GetILAsByteArray();
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, bytes, method), Is.True);
        int checkedBytes = 0;
        foreach (var instruction in original.Body.Instructions)
        {
            switch (instruction.OpCode.OperandType)
            {
                case OperandType.InlineString:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                case OperandType.InlineSig: continue;
            }
            int end = instruction.Next?.Offset ?? original.Body.CodeSize;
            for (int at = instruction.Offset + instruction.OpCode.Size; at < end; at++)
            {
                var changed = (byte[])bytes.Clone();
                changed[at] ^= 1;
                Assert.That(SupplierBodyIdentity.InstructionsMatch(original, changed, method), Is.False,
                    "Changed operand at " + at + " of " + instruction.OpCode.Name);
                checkedBytes++;
            }
        }
        Assert.That(checkedBytes, Is.GreaterThan(0));
        if (name == nameof(BodyWithNumericAndSwitchOperands))
            Assert.That(original.Body.Instructions.Any(i => i.OpCode.OperandType == OperandType.InlineSwitch), Is.True);
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, bytes.Take(bytes.Length - 1).ToArray(), method), Is.False);
    }
    [Test] public void SupplierHarmonyReferenceAllowsOnlyObservedActiveBinding()
    {
        var method = AccessTools.Method(typeof(SupplierCompatibilityTests), nameof(HarmonyReferenceType));
        using var source = ModuleDefinition.ReadModule(method.Module.Assembly.Location);
        var original = (MethodDefinition)source.LookupToken(method.MetadataToken);
        var reference = source.AssemblyReferences.Single(a => a.Name == "0Harmony");
        reference.Version = new Version(2, 4, 1, 0);
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, method.GetMethodBody()!.GetILAsByteArray(), method), Is.True);
        reference.Version = new Version(2, 4, 0, 0);
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, method.GetMethodBody()!.GetILAsByteArray(), method), Is.False);
        reference.Version = new Version(2, 4, 1, 0);
        reference.PublicKeyToken = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, method.GetMethodBody()!.GetILAsByteArray(), method), Is.False);
    }
    [Test] public void SupplierGameReferenceBindsOnlyUnsignedActiveGame()
    {
        var method = AccessTools.Method(typeof(SupplierCompatibilityTests), nameof(GameReferenceType));
        using var source = ModuleDefinition.ReadModule(method.Module.Assembly.Location);
        var original = (MethodDefinition)source.LookupToken(method.MetadataToken);
        var reference = source.AssemblyReferences.Single(a => a.Name == "Assembly-CSharp");
        reference.Version = new Version(1, 6, 9676, 17735); // Supplier's inspected Steam reference on GOG.
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, method.GetMethodBody()!.GetILAsByteArray(), method), Is.True);
        reference.PublicKeyToken = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, method.GetMethodBody()!.GetILAsByteArray(), method), Is.False);
    }
    [Test] public void SupplierInstructionsRequireVolatileFieldModifier()
    {
        var method = AccessTools.Method(typeof(SupplierCompatibilityTests), nameof(BodyWithVolatile));
        Assert.That(SupplierBodyIdentity.Matches(method), Is.True);
        using var source = ModuleDefinition.ReadModule(method.Module.Assembly.Location);
        var original = (MethodDefinition)source.LookupToken(method.MetadataToken);
        var field = (FieldReference)original.Body.Instructions.First(i => i.Operand is FieldReference).Operand;
        Assert.That(field.FieldType, Is.TypeOf<RequiredModifierType>());
        field.FieldType = ((RequiredModifierType)field.FieldType).ElementType;
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, method.GetMethodBody()!.GetILAsByteArray(), method), Is.False);
    }
    [Test] public void SupplierBodyCheckReadsDiskIlWithoutLoadingAnotherAssembly()
    {
        var method = AccessTools.Method(typeof(SupplierCompatibilityTests), nameof(BodyWithLocalsAndFinally));
        Assert.That(SupplierBodyIdentity.Matches(method), Is.True);
        using var stream = File.OpenRead(method.Module.Assembly.Location);
        using var module = ModuleDefinition.ReadModule(stream);
        var original = (MethodDefinition)module.LookupToken(method.MetadataToken);
        Assert.That(SupplierBodyIdentity.OriginalIl(stream, original.RVA), Is.EqualTo(method.GetMethodBody()!.GetILAsByteArray()));
    }

    [Test] public void SupplierInstructionsResolveRenumberedTokensButRejectChangedCalls()
    {
        var method = AccessTools.Method(typeof(SupplierCompatibilityTests), nameof(BodyWithLocalsAndFinally));
        using var source = ModuleDefinition.ReadModule(method.Module.Assembly.Location);
        var original = (MethodDefinition)source.LookupToken(method.MetadataToken);
        using var rewritten = ModuleDefinition.ReadModule(method.Module.Assembly.Location);
        var owner = rewritten.GetType(typeof(SupplierCompatibilityTests).FullName);
        var added = new MethodDefinition("InsertedForTokenTest", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, rewritten.TypeSystem.Void);
        added.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        owner.Methods.Insert(0, added);
        using var bytes = new MemoryStream(); rewritten.Write(bytes);
        // Only our own synthetic test assembly is byte-loaded; never game files.
        var loaded = Assembly.Load(bytes.ToArray()).GetType(typeof(SupplierCompatibilityTests).FullName)!;
        var actual = AccessTools.Method(loaded, nameof(BodyWithLocalsAndFinally));
        Assert.That(actual.MetadataToken, Is.Not.EqualTo(method.MetadataToken));
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, actual.GetMethodBody()!.GetILAsByteArray(), actual), Is.True);
        var call = original.Body.Instructions.First(i => i.Operand is MethodReference m && m.Name == "Clear");
        ((MethodReference)call.Operand).Name = "TrimExcess";
        Assert.That(SupplierBodyIdentity.InstructionsMatch(original, actual.GetMethodBody()!.GetILAsByteArray(), actual), Is.False);
    }

    [Test] public void WowgagCheckedTargetsCoverExactGroupsWithoutUnrelatedContentOrTranslation()
    {
        var xml = LoaderSupplierPolicy.XmlTargets();
        Assert.That(xml, Does.Contain(AccessTools.Method(typeof(LoadedModManager), "ApplyPatches")));
        Assert.That(xml, Does.Contain(AccessTools.Method(typeof(LoadedModManager), "LoadModXML")));
        Assert.That(xml.OfType<MethodInfo>().Count(m => m.DeclaringType == typeof(XmlNode)), Is.EqualTo(4));
        Assert.That(xml, Does.Contain(AccessTools.Method(typeof(PatchOperationAdd), "ApplyWorker")));
        Assert.That(xml, Does.Not.Contain(AccessTools.Method(typeof(ModContentPack), "LoadPatches")));
        Assert.That(xml, Does.Not.Contain(AccessTools.Method(typeof(ModContentPack), "ReloadContentInt")));
        Assert.That(xml.Any(m => m.DeclaringType == typeof(DefInjectionPackage)), Is.False);
    }
}
