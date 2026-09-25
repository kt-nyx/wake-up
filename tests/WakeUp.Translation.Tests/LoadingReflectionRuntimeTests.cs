// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using NUnit.Framework;
using Verse;
using WakeUp;
using FixtureMenuObserver;

namespace WakeUp.Tests;

[TestFixture]
[NonParallelizable]
public sealed class LoadingReflectionRuntimeTests
{
    private bool profiler;
    private static int foreignCalls;
    private static void ForeignCall() { foreignCalls++; }
    private static int attributeOperationCalls;
    private static bool attributeOperationThrows;
    private static bool AttributeOperationPrefix(MemberInfo __0, ref bool __result)
    {
        if (!Equals(__0, typeof(C12AttributeProbeDef).GetField("text"))) return true;
        attributeOperationCalls++;
        if (attributeOperationThrows) throw new InvalidOperationException("C12 nongeneric operation callback");
        __result = false; return false;
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void NongenericOperationHooksRetainCallbacksAndErrors(bool preexisting, bool throws)
    {
        const string owner = "c12.test.attribute-operation";
        var previous = new Dictionary<FieldInfo, object?>();
        foreach (var entry in new[] {
            (typeof(TypeSearchLifetime), "selected"), (typeof(TypeSearchLifetime), "finished"),
            (typeof(LoadingReflectionRuntime), "installed"), (typeof(LoadingReflectionRuntime), "startup"), (typeof(LoadingReflectionRuntime), "consumer"),
            (typeof(LoadingAttributeRuntime), "operation") })
        {
            FieldInfo field = AccessTools.Field(entry.Item1, entry.Item2); previous.Add(field, field.GetValue(null));
        }
        var index = new LoadingReflectionIndex();
        var member = typeof(C12AttributeProbeDef).GetField("text")!;
        MethodInfo target = LoadingAttributeRuntime.NativeOperation;
        var harmony = new Harmony(owner);
        Action hook = () => harmony.Patch(target, prefix: new HarmonyMethod(typeof(LoadingReflectionRuntimeTests), nameof(AttributeOperationPrefix)));
        try
        {
            attributeOperationCalls = 0; attributeOperationThrows = throws;
            if (preexisting) hook();
            Assert.That(PublishedPatchGuard.TryCreate(target, "wakeup.loading-attribute-operation", out var operation, allPatchKinds: true), Is.True);
            Assert.That(PublishedPatchGuard.TryCreate(AccessTools.Method(typeof(LoadingReflectionRuntimeTests), nameof(ForeignCall)),
                "wakeup.loading-reflection", out var consumer, allPatchKinds: true), Is.True);
            AccessTools.Field(typeof(TypeSearchLifetime), "selected").SetValue(null, true);
            AccessTools.Field(typeof(TypeSearchLifetime), "finished").SetValue(null, 0);
            AccessTools.Field(typeof(LoadingReflectionRuntime), "installed").SetValue(null, true);
            AccessTools.Field(typeof(LoadingReflectionRuntime), "startup").SetValue(null, index);
            AccessTools.Field(typeof(LoadingReflectionRuntime), "consumer").SetValue(null, consumer);
            // Host test admits the concrete host operation explicitly; product
            // initialization still requires the pinned GOG and rewritten helper.
            AccessTools.Field(typeof(LoadingAttributeRuntime), "operation").SetValue(null, operation);
            if (!preexisting)
            {
                Assert.That(LoadingAttributeRuntime.IsDefined(member, typeof(NoTranslateAttribute), true), Is.True);
                Assert.That(LoadingAttributeRuntime.IsDefined(member, typeof(NoTranslateAttribute), true), Is.True);
                Assert.That(index.AttributeHits, Is.EqualTo(1));
                hook();
            }
            long hits = index.AttributeHits;
            for (int i = 0; i < 2; i++)
                if (throws)
                    Assert.That(Assert.Throws<InvalidOperationException>((Action)(() => LoadingAttributeRuntime.IsDefined(member, typeof(NoTranslateAttribute), true)))!.Message,
                        Is.EqualTo("C12 nongeneric operation callback"));
                else Assert.That(LoadingAttributeRuntime.IsDefined(member, typeof(NoTranslateAttribute), true), Is.False);
            Assert.That(attributeOperationCalls, Is.EqualTo(2));
            Assert.That(index.AttributeHits, Is.EqualTo(hits));
            harmony.UnpatchAll(owner);
            AccessTools.Field(typeof(LoadingReflectionRuntime), "consumer").SetValue(null, null);
            Assert.That(LoadingAttributeRuntime.IsDefined(member, typeof(NoTranslateAttribute), true), Is.True);
            Assert.That(index.AttributeHits, Is.EqualTo(hits), "Outside a loading consumer the native operation runs.");
            AccessTools.Field(typeof(LoadingReflectionRuntime), "consumer").SetValue(null, consumer);
            AccessTools.Field(typeof(TypeSearchLifetime), "selected").SetValue(null, false);
            Assert.That(LoadingAttributeRuntime.IsDefined(member, typeof(NoTranslateAttribute), true), Is.True);
            Assert.That(index.AttributeHits, Is.EqualTo(hits), "Option-off preserves the native operation.");
        }
        finally
        {
            harmony.UnpatchAll(owner); index.Clear(); attributeOperationThrows = false;
            foreach (var entry in previous) entry.Key.SetValue(null, entry.Value);
        }
    }
    [SetUp] public void SetUp() { profiler = DeepProfiler.enabled; DeepProfiler.enabled = false; }
    [TearDown] public void TearDown() { DeepProfiler.enabled = profiler; }

    private static string MonoHash(string canonical)
    {
        canonical = canonical.Replace("System.Private.CoreLib:", "mscorlib:").Replace("System.Linq:", "System.Core:")
            .Replace("System.Private.Xml:", "System.Xml:");
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "");
    }

    [Test]
    public void NativeContractsAndOnlyIntendedOverloadsAreRewritten()
    {
        MethodBase[] contracts = LoadingReflectionRuntime.ContractMethods();
        var actual = new List<string>();
        foreach (MethodBase method in contracts)
        {
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason, out string canonical), Is.True, reason);
            actual.Add(MonoHash(canonical));
            TestContext.Out.WriteLine("\"" + MonoHash(canonical) + "\", // " + method.DeclaringType!.Name + "." + method + " host=" + hash);
        }
        Assert.That(actual, Is.EqualTo(LoadingReflectionRuntime.ExpectedBodies));
        foreach (MethodBase method in LoadingReflectionRuntime.ConsumerMethods())
            Assert.That(PatchProcessor.GetOriginalInstructions(method).Any(i => i.operand is MethodInfo called
                && LoadingReflectionRuntime.Replacement(called) != null), Is.True, method.Name);
        Assert.That(LoadingReflectionRuntime.Replacement(typeof(Type).GetMethod("GetConstructor", new[] { typeof(Type[]) })!), Is.Null);
        Assert.That(LoadingReflectionRuntime.Replacement(typeof(MemberInfo).GetMethod("GetCustomAttributes", new[] { typeof(Type), typeof(bool) })!), Is.Null);
        Assert.That(LoadingReflectionRuntime.Replacement(typeof(Type).GetMethod("GetMethod", new[] { typeof(string), typeof(Type[]) })!), Is.Null);
        Assert.That(LoadingReflectionRuntime.Replacement(AccessTools.Method(typeof(GenAttribute), "HasAttribute")), Is.Null);
        foreach (Type attribute in new[] { typeof(NoTranslateAttribute), typeof(UnsavedAttribute), typeof(TranslationCanChangeCountAttribute) })
            Assert.That(LoadingReflectionRuntime.Replacement(C12AttributeQualification.Closed(attribute)), Is.Null);
        Assert.That(LoadingReflectionRuntime.ConsumerMethods(), Does.Not.Contain(AccessTools.Method(typeof(XmlToObjectUtils), "DirectGetFieldByName")));
    }

    [Test]
    public void NativeDeserializerMethodCachesRetainOwnershipAndInvocationBehavior()
    {
        MethodInfo custom = XmlToObjectUtils.CustomDataLoadMethodOf(typeof(CustomXml));
        MethodInfo postLoad = XmlToObjectUtils.PostLoadMethodOf(typeof(CustomXml));
        Func<System.Xml.XmlNode, bool, object> oldDelegate = DirectXmlToObject.GetObjectFromXmlMethod(typeof(CustomXml));
        DirectXmlToObjectNew.ParseValueAndSetFieldDelegate newDelegate = DirectXmlToObjectNew.GetFieldSetterForType(typeof(CustomXml));
        Assert.That(XmlToObjectUtils.CustomDataLoadMethodOf(typeof(CustomXml)), Is.SameAs(custom));
        Assert.That(XmlToObjectUtils.PostLoadMethodOf(typeof(CustomXml)), Is.SameAs(postLoad));
        Assert.That(DirectXmlToObject.GetObjectFromXmlMethod(typeof(CustomXml)), Is.SameAs(oldDelegate));
        Assert.That(DirectXmlToObjectNew.GetFieldSetterForType(typeof(CustomXml)), Is.SameAs(newDelegate));
        CustomXml.Constructions = 0; CustomXml.Loads = 0; CustomXml.PostLoads = 0;
        var node = new System.Xml.XmlDocument(); node.LoadXml("<item><value>1</value></item>");
        CustomXml first = DirectXmlToObject.ObjectFromXml<CustomXml>(node.DocumentElement!, true);
        CustomXml second = DirectXmlToObject.ObjectFromXml<CustomXml>(node.DocumentElement!, true);
        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(CustomXml.Constructions, Is.EqualTo(2));
        Assert.That(CustomXml.Loads, Is.EqualTo(2));
        Assert.That(CustomXml.PostLoads, Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeLoadingConsumersAndExistingTranslationWorkInEitherInstallOrder(bool translationFirst)
    {
        const string owner = "wakeup.loading-reflection";
        var previous = new Dictionary<string, object?>();
        string[] reflectionHashes = LoadingReflectionRuntime.ExpectedBodies.ToArray();
        string[] translationHashes = TranslationRuntime.ExpectedBodies.ToArray();
        foreach (string name in new[] { "selected", "finished" })
            previous[name] = AccessTools.Field(typeof(TypeSearchLifetime), name).GetValue(null);
        object? oldKeys = AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").GetValue(null);
        object? oldSuggestions = AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").GetValue(null);
        var refs = (IList)AccessTools.Field(typeof(DirectXmlCrossRefLoader), "wantedRefs").GetValue(null)!;
        int priorRefs = refs.Count;
        try
        {
            foreach (string name in new[] { "selected" }) AccessTools.Field(typeof(TypeSearchLifetime), name).SetValue(null, true);
            AccessTools.Field(typeof(TypeSearchLifetime), "finished").SetValue(null, 0);
            SetHostHashes(LoadingReflectionRuntime.ContractMethods(), LoadingReflectionRuntime.ExpectedBodies);
            SetHostHashes(TranslationRuntime.ContractMethods(), TranslationRuntime.ExpectedBodies);
            if (translationFirst) Assert.That(TranslationRuntime.Install(), Is.True);
            LoadingReflectionRuntime.Initialize();
            Assert.That(AccessTools.Field(typeof(LoadingReflectionRuntime), "installed").GetValue(null), Is.True);
            if (!translationFirst) Assert.That(TranslationRuntime.Install(), Is.True);
            var index = (LoadingReflectionIndex)AccessTools.Field(typeof(LoadingReflectionRuntime), "startup").GetValue(null)!;
            MethodBase setter = AccessTools.Method(typeof(DefInjectionPackage), "SetDefFieldAtPath");
            var nativeCalls = AttributeCalls(PatchProcessor.GetOriginalInstructions(setter));
            var rewritten = LoadingReflectionRuntime.Transpiler(TranslationRuntime.DuplicateTranspiler(PatchProcessor.GetOriginalInstructions(setter)), setter);
            Assert.That(AttributeCalls(rewritten), Is.EqualTo(nativeCalls), "Keep exact closed generic dispatch in both install orders.");

            var wanter = new CrossRefConsumer();
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(wanter, "target", "First");
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(wanter, "target", "Second");
            Assert.That(refs.Count, Is.EqualTo(priorRefs + 2));
            Assert.That(index.FieldHits, Is.GreaterThan(0));
            var nativeNode = new System.Xml.XmlDocument(); nativeNode.LoadXml("<WoodLog>3</WoodLog>");
            var firstCount = new ThingDefCountClass();
            var secondCount = new ThingDefCountClass();
            firstCount.LoadDataFromXmlCustom(nativeNode.DocumentElement!);
            long afterFirstCount = index.FieldHits;
            secondCount.LoadDataFromXmlCustom(nativeNode.DocumentElement!);
            Assert.That(firstCount.count, Is.EqualTo(3));
            Assert.That(secondCount.count, Is.EqualTo(3));
            Assert.That(index.FieldHits - afterFirstCount, Is.GreaterThanOrEqualTo(3), "Root field, default field and cross-reference field avoid discovery.");
            GenericConsumer<int>.Calls = 0;
            // Enter the patched helpers themselves: CoreCLR can inline these
            // small helpers into a caller that was JIT-compiled before install.
            MethodInfo methodLookup = AccessTools.Method(typeof(GenGeneric), "MethodOnGenericType");
            MethodInfo propertyLookup = AccessTools.Method(typeof(GenGeneric), "PropertyOnGenericType");
            object[] methodArguments = { typeof(GenericConsumer<>), typeof(int), "Read" };
            object[] propertyArguments = { typeof(GenericConsumer<>), typeof(int), "Next" };
            for (int i = 0; i < 2; i++)
                ((MethodInfo)methodLookup.Invoke(null, methodArguments)!).Invoke(null, null);
            for (int i = 0; i < 2; i++)
                ((PropertyInfo)propertyLookup.Invoke(null, propertyArguments)!).GetValue(null, null);
            Assert.That(GenericConsumer<int>.Calls, Is.EqualTo(4));
            Assert.That(index.MethodHits, Is.GreaterThan(0));
            Assert.That(index.PropertyHits, Is.GreaterThan(0));

            AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").SetValue(null, new Dictionary<string, string>());
            AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").SetValue(null, new Dictionary<string, string>());
            var definition = new TranslationDef { defName = "Example" };
            DefDatabase<TranslationDef>.Add(definition);
            var package = new DefInjectionPackage(typeof(TranslationDef));
            foreach (string path in new[] { "Example.words.0", "Example.words.1", "Example.oldText" })
                package.injections.Add(path, new DefInjectionPackage.DefInjection {
                    path = path, nonBackCompatiblePath = path, injection = "translated", fileSource = "c12.xml" });
            long scopes = TranslationRuntime.CompletedScopes;
            package.InjectIntoDefs(false);
            package.InjectIntoDefs(true);
            Assert.That(definition.words, Is.EqualTo(new[] { "translated", "translated" }));
            Assert.That(definition.text, Is.EqualTo("translated"));
            Assert.That(package.loadErrors, Is.Empty);
            Assert.That(TranslationRuntime.CompletedScopes - scopes, Is.EqualTo(2));
            Assert.That(index.FieldArrayHits, Is.GreaterThan(0));

            var foreign = new Harmony("WakeUp.Reflection.Tests.Foreign");
            foreign.Patch(AccessTools.Method(typeof(GenGeneric), "MethodOnGenericType"),
                prefix: new HarmonyMethod(typeof(LoadingReflectionRuntimeTests), nameof(ForeignCall)));
            long methodHits = index.MethodHits;
            long fieldHits = index.FieldHits;
            foreignCalls = 0;
            ((MethodInfo)methodLookup.Invoke(null, methodArguments)!).Invoke(null, null);
            Assert.That(foreignCalls, Is.EqualTo(1));
            Assert.That(index.MethodHits, Is.EqualTo(methodHits), "Only the foreign-patched consumer falls back.");
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(wanter, "target", "Third");
            Assert.That(index.FieldHits, Is.GreaterThan(fieldHits), "Unrelated custom-loader coverage remains active.");
        }
        finally
        {
            while (refs.Count > priorRefs) refs.RemoveAt(refs.Count - 1);
            DefDatabase<TranslationDef>.Clear();
            AccessTools.Field(typeof(TKeySystem), "tKeyToNormalizedTranslationKey").SetValue(null, oldKeys);
            AccessTools.Field(typeof(TKeySystem), "translationKeyToTKey").SetValue(null, oldSuggestions);
            new Harmony(owner).UnpatchAll(owner);
            new Harmony("WakeUp.Reflection.Tests.Foreign").UnpatchAll("WakeUp.Reflection.Tests.Foreign");
            new Harmony(TranslationRuntime.Owner).UnpatchAll(TranslationRuntime.Owner);
            AccessTools.Field(typeof(LoadingReflectionRuntime), "installed").SetValue(null, false);
            AccessTools.Field(typeof(TranslationRuntime), "installed").SetValue(null, false);
            LoadingReflectionRuntime.Complete();
            var handler = (AssemblyLoadEventHandler)Delegate.CreateDelegate(typeof(AssemblyLoadEventHandler),
                AccessTools.Method(typeof(LoadingReflectionRuntime), "AssemblyLoaded"));
            AppDomain.CurrentDomain.AssemblyLoad -= handler;
            Array.Copy(reflectionHashes, LoadingReflectionRuntime.ExpectedBodies, reflectionHashes.Length);
            Array.Copy(translationHashes, TranslationRuntime.ExpectedBodies, translationHashes.Length);
            foreach (var pair in previous) AccessTools.Field(typeof(TypeSearchLifetime), pair.Key).SetValue(null, pair.Value);
        }
    }

    private static void SetHostHashes(MethodBase[] methods, string[] hashes)
    {
        Assert.That(hashes.Length, Is.EqualTo(methods.Length));
        for (int i = 0; i < methods.Length; i++)
        {
            Assert.That(SemanticMethodIdentity.TryHash(methods[i], out string hash, out string reason), Is.True, reason);
            hashes[i] = hash;
        }
    }

    private static object[] AttributeCalls(IEnumerable<CodeInstruction> code) => code
        .Where(i => i.operand is MethodInfo method && method.DeclaringType == typeof(GenAttribute))
        .Select(i => (object)(i.opcode, i.operand)).ToArray();

    [TestCase(typeof(NoTranslateAttribute), "text", true, false)]
    [TestCase(typeof(NoTranslateAttribute), "text", false, false)]
    [TestCase(typeof(NoTranslateAttribute), "text", true, true)]
    [TestCase(typeof(NoTranslateAttribute), "text", false, true)]
    [TestCase(typeof(UnsavedAttribute), "unsaved", true, false)]
    [TestCase(typeof(UnsavedAttribute), "unsaved", false, false)]
    [TestCase(typeof(UnsavedAttribute), "unsaved", true, true)]
    [TestCase(typeof(UnsavedAttribute), "unsaved", false, true)]
    [TestCase(typeof(TranslationCanChangeCountAttribute), "words", true, false)]
    [TestCase(typeof(TranslationCanChangeCountAttribute), "words", false, false)]
    [TestCase(typeof(TranslationCanChangeCountAttribute), "words", true, true)]
    [TestCase(typeof(TranslationCanChangeCountAttribute), "words", false, true)]
    public void ClosedAttributeCallOperandsPreserveHooksWithMetadataReuse(Type attribute, string field, bool preexisting, bool throws)
    {
        var previous = new Dictionary<string, object?>();
        string[] hashes = LoadingReflectionRuntime.ExpectedBodies.ToArray();
        foreach (string name in new[] { "selected", "finished" })
            previous[name] = AccessTools.Field(typeof(TypeSearchLifetime), name).GetValue(null);
        try
        {
            if (preexisting) C12AttributeQualification.Install(attribute, field, throws);
            AccessTools.Field(typeof(TypeSearchLifetime), "selected").SetValue(null, true);
            AccessTools.Field(typeof(TypeSearchLifetime), "finished").SetValue(null, 0);
            SetHostHashes(LoadingReflectionRuntime.ContractMethods(), LoadingReflectionRuntime.ExpectedBodies);
            LoadingReflectionRuntime.Initialize();
            Assert.That(AccessTools.Field(typeof(LoadingReflectionRuntime), "installed").GetValue(null), Is.True);
            MethodBase setter = AccessTools.Method(typeof(DefInjectionPackage), "SetDefFieldAtPath");
            MethodInfo call = LoadingReflectionRuntime.Transpiler(PatchProcessor.GetOriginalInstructions(setter), setter)
                .Where(i => i.operand is MethodInfo m && m.DeclaringType == typeof(GenAttribute)
                    && m.IsGenericMethod && m.GetGenericArguments().Single() == attribute)
                .Select(i => (MethodInfo)i.operand).First();
            var member = typeof(C12AttributeProbeDef).GetField(field)!;
            if (!preexisting)
            {
                Assert.That(call.Invoke(null, new object[] { member }), Is.True);
                C12AttributeQualification.Install(attribute, field, throws);
            }
            // CoreCLR's compiled setter can bypass a closed detour. Enter the
            // exact preserved call operand here; the fixture checks real Mono
            // setter dispatch separately, without changing its JIT settings.
            for (int i = 0; i < 2; i++)
                if (throws)
                {
                    var error = Assert.Throws<TargetInvocationException>((Action)(() => call.Invoke(null, new object[] { member })));
                    Assert.That(error!.InnerException!.Message, Is.EqualTo("C12 attribute callback exception"));
                }
                else Assert.That(call.Invoke(null, new object[] { member }), Is.False);
            Assert.That(AccessTools.Field(typeof(C12AttributeQualification), "calls").GetValue(null), Is.EqualTo(2));
            var package = new DefInjectionPackage(typeof(C12AttributeProbeDef));
            MethodInfo lookup = AccessTools.Method(typeof(DefInjectionPackage), "GetFieldNamed");
            for (int i = 0; i < 2; i++) lookup.Invoke(package, new object[] { typeof(C12AttributeProbeDef), field });
            var index = (LoadingReflectionIndex)AccessTools.Field(typeof(LoadingReflectionRuntime), "startup").GetValue(null)!;
            Assert.That(index.FieldHits, Is.GreaterThan(0), "The native injection field lookup still reuses metadata.");
        }
        finally
        {
            C12AttributeQualification.Remove();
            new Harmony("wakeup.loading-reflection").UnpatchAll("wakeup.loading-reflection");
            AccessTools.Field(typeof(LoadingReflectionRuntime), "installed").SetValue(null, false);
            LoadingReflectionRuntime.Complete();
            AppDomain.CurrentDomain.AssemblyLoad -= (AssemblyLoadEventHandler)Delegate.CreateDelegate(typeof(AssemblyLoadEventHandler),
                AccessTools.Method(typeof(LoadingReflectionRuntime), "AssemblyLoaded"));
            Array.Copy(hashes, LoadingReflectionRuntime.ExpectedBodies, hashes.Length);
            foreach (var pair in previous) AccessTools.Field(typeof(TypeSearchLifetime), pair.Key).SetValue(null, pair.Value);
        }
    }

    public sealed class CrossRefConsumer { public ThingDef? target; }
    public sealed class GenericConsumer<T>
    {
        public static int Calls;
        public static int Read() => ++Calls;
        public static int Next => ++Calls;
    }
    public sealed class TranslationDef : Def
    {
        [LoadAlias("oldText")] public string text = "original";
        public List<string> words = new() { "one", "two" };
    }

    public sealed class CustomXml
    {
        public static int Constructions, Loads, PostLoads;
        public CustomXml() { Constructions++; }
        public void LoadDataFromXmlCustom(System.Xml.XmlNode root) { Loads++; }
        public void PostLoad() { PostLoads++; }
    }
}
