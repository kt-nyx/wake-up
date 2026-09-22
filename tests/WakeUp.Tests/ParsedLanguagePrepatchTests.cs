// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class ParsedLanguagePrepatchTests
{
    [Test]
    public void OriginalAndCoordinatedSerializedBodies()
    {
        using var module = ModuleDefinition.ReadModule(typeof(LoadedLanguage).Assembly.Location);
        var methods = ParsedLanguagePrepatch.Targets(module);
        var original = methods.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
        ParsedLanguagePrepatch.Inject(module, methods);
        using var output = new MemoryStream(); module.Write(output);
        ResolveEventHandler resolve = (_, args) =>
        {
            var existing = AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (existing != null) return existing;
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            return loaded == null ? Assembly.ReflectionOnlyLoad(args.Name) : Assembly.ReflectionOnlyLoadFrom(loaded.Location);
        };
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolve;
        string[] rewritten;
        try
        {
            Assembly copy = Assembly.ReflectionOnlyLoad(output.ToArray());
            var runtime = ParsedLanguagePrepatch.Names.Select((name, i) => copy.GetType(ParsedLanguagePrepatch.Types[i])!
                .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!).ToArray();
            rewritten = runtime.Select(m => SemanticMethodIdentity.TryHash(m, out string hash, out string reason) ? hash : throw new Exception(m.Name + ":" + reason)).ToArray();
            using var serialized = ModuleDefinition.ReadModule(new MemoryStream(output.ToArray()));
            foreach (var method in ParsedLanguagePrepatch.Targets(serialized))
                foreach (var instruction in method.Body.Instructions)
                    if (instruction.Operand is MemberReference member && member.DeclaringType?.FullName.StartsWith("WakeUp.") == true)
                    {
                        var bound = copy.ManifestModule.ResolveMember(member.MetadataToken.ToInt32());
                        Assert.That(bound.DeclaringType!.IsVisible, Is.True, member.FullName);
                        Assert.That(bound is MethodBase called ? called.IsPublic : ((FieldInfo)bound).IsPublic, Is.True, member.FullName);
                    }
        }
        finally { AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= resolve; }
        TestContext.Out.WriteLine("Original=" + string.Join(",", original));
        TestContext.Out.WriteLine("Rewritten=" + string.Join(",", rewritten));
        string? path = Environment.GetEnvironmentVariable("WAKE_UP_LANGUAGE_CONTRACT_OUTPUT");
        if (path != null) File.WriteAllLines(Path.ChangeExtension(path, ".prepatch.txt"), new[] { string.Join(",", original), string.Join(",", rewritten) });
        Assert.That(original, Is.EqualTo(ParsedLanguagePrepatch.Original));
        Assert.That(rewritten, Is.EqualTo(ParsedLanguagePrepatch.Rewritten));
    }

    [Test]
    public void MissingChangedOrAlreadyRewrittenBodiesKeepAllNativeMethodsIntact()
    {
        for (int changed = 0; changed < ParsedLanguagePrepatch.Names.Length; changed++)
        {
            using var module = ModuleDefinition.ReadModule(typeof(LoadedLanguage).Assembly.Location);
            var methods = ParsedLanguagePrepatch.Targets(module);
            methods[changed].Body.Instructions.Insert(0, Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Nop));
            var before = methods.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
            var references = module.AssemblyReferences.ToArray();
            Assert.That(ParsedLanguagePrepatch.RewriteAssembly(module), Is.False);
            Assert.That(methods.Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(before));
            Assert.That(module.AssemblyReferences, Is.EqualTo(references));
            methods[changed].Body.Instructions.RemoveAt(0);
            Assert.That(ParsedLanguagePrepatch.RewriteAssembly(module), Is.True);
            before = methods.Select(AssetRoutingPrepatch.Fingerprint).ToArray();
            Assert.That(ParsedLanguagePrepatch.RewriteAssembly(module), Is.False);
            Assert.That(methods.Select(AssetRoutingPrepatch.Fingerprint), Is.EqualTo(before));
            methods[changed].DeclaringType.Methods.Remove(methods[changed]);
            Assert.That(ParsedLanguagePrepatch.RewriteAssembly(module), Is.False);
        }
    }

    [Test]
    public void NativeInsertionCatchAndOtherPrepatchesRemainIndependent()
    {
        foreach (bool languageFirst in new[] { true, false })
        {
            using var module = ModuleDefinition.ReadModule(typeof(LoadedLanguage).Assembly.Location);
            if (languageFirst) Assert.That(ParsedLanguagePrepatch.RewriteAssembly(module), Is.True);
            Assert.That(ParsedXmlPrepatch.RewriteAssembly(module), Is.True);
            Assert.That(LoadingAttributePrepatch.RewriteAssembly(module), Is.True);
            if (!languageFirst) Assert.That(ParsedLanguagePrepatch.RewriteAssembly(module), Is.True);
            var methods = ParsedLanguagePrepatch.Targets(module);
            Assert.That(methods[0].Body.ExceptionHandlers.Last().HandlerType, Is.EqualTo(Mono.Cecil.Cil.ExceptionHandlerType.Finally));
            var injection = methods[6];
            var handler = injection.Body.ExceptionHandlers.Single(h => h.HandlerType == Mono.Cecil.Cil.ExceptionHandlerType.Catch);
            int start = injection.Body.Instructions.IndexOf(handler.TryStart), end = injection.Body.Instructions.IndexOf(handler.TryEnd);
            foreach (string name in new[] { "TryAddInjection", "TryAddFullListInjection" })
            {
                var calls = injection.Body.Instructions.Select((instruction, index) => new { instruction, index })
                    .Where(p => p.instruction.Operand is MethodReference m && m.Name == name).ToArray();
                Assert.That(calls.Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(calls.All(p => p.index >= start && p.index < end), Is.True);
            }
        }
    }
}
