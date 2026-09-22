// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using WakeUp;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class LoadingInvocationObservationTests
{
    [Test]
    public void PreloadCallbackRewritePreservesAllOtherInstructionsAndExceptionRegions()
    {
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(Provider().Location);
        var method = LoadingCallbackPrepatch.Find(module)!;
        string original = AssetRoutingPrepatch.Fingerprint(method);
        Assert.That(original, Is.EqualTo(LoadingCallbackPrepatch.OriginalBody));
        var instructions = method.Body.Instructions.ToArray();
        var operands = instructions.Select(i => i.Operand).ToArray();
        var opcodes = instructions.Select(i => i.OpCode).ToArray();
        LoadingCallbackPrepatch.Inject(module, method);
        var changed = Enumerable.Range(0, instructions.Length).Where(i => instructions[i].OpCode != opcodes[i]
            || !Equals(instructions[i].Operand, operands[i])).ToArray();
        Assert.That(changed.Length, Is.EqualTo(1));
        int at = changed.Single();
        Assert.That(method.Body.Instructions, Is.EqualTo(instructions));
        instructions[at].OpCode = opcodes[at]; instructions[at].Operand = operands[at];
        Assert.That(AssetRoutingPrepatch.Fingerprint(method), Is.EqualTo(original),
            "All branch targets, local variables, exception boundaries and other instructions remain exact.");
        method.Body.Instructions[0].OpCode = Mono.Cecil.Cil.OpCodes.Nop;
        Assert.That(LoadingCallbackPrepatch.RewriteAssembly(module), Is.False, "A changed supplier body must remain untouched.");
    }
    private static Assembly Provider()
    {
        string managed = Environment.GetEnvironmentVariable("WAKE_UP_RIMWORLD_MANAGED_DIR")!;
        return Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed))!,
            "Mods", "3535481557", "1.6", "Assemblies", "ilyvion.LoadingProgress.dll"));
    }

    [TestCase(0)]
    [TestCase(2)]
    public void ActualSupplierExecutionBodiesMatchReviewedIdentity(int index)
    {
        var target = LoadingInvocationObservation.ProviderMethods(Provider())[index];
        Assert.That(SemanticMethodIdentity.TryHash(target, out string actual, out string reason), Is.True, reason);
        Assert.That(actual, Is.EqualTo(LoadingInvocationObservation.ProviderBodies[index]));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void NativeAndProviderLoopsKeepEveryInstructionExceptActualInvocation(bool provider, bool constructors)
    {
        MethodInfo target = provider ? LoadingInvocationObservation.ProviderMethods(Provider())[constructors ? 0 : 1]
            : constructors ? LoadingInvocationObservation.NativeConstructors : LoadingInvocationObservation.NativeCallbacks;
        var original = PatchProcessor.GetOriginalInstructions(target);
        var copies = original.Select(i => new CodeInstruction(i)).ToArray();
        var after = (constructors ? LoadingInvocationObservation.RewriteConstructors(copies)
            : LoadingInvocationObservation.RewriteCallbacks(copies)).ToList();
        Assert.That(after.Count, Is.EqualTo(original.Count));
        var changed = Enumerable.Range(0, original.Count).Where(i => original[i].opcode != after[i].opcode
            || !Equals(original[i].operand, after[i].operand)).ToArray();
        Assert.That(changed.Length, Is.EqualTo(1));
        int at = changed.Single();
        Assert.That(original[at].Calls(constructors ? LoadingInvocationObservation.Constructor : LoadingInvocationObservation.Invoke), Is.True);
        Assert.That(after[at].opcode, Is.EqualTo(OpCodes.Call));
        Assert.That(after[at].operand, Is.EqualTo(AccessTools.Method(typeof(LoadingInvocationObservation),
            constructors ? nameof(LoadingInvocationObservation.RunConstructor) : nameof(LoadingInvocationObservation.ExecuteAction))));
        Assert.That(after[at].labels, Is.EqualTo(original[at].labels));
        Assert.That(after[at].blocks, Is.EqualTo(original[at].blocks));
        after[at] = new CodeInstruction(original[at]);
        Assert.That(InstructionComparison.SameInstructions(original, after), Is.True,
            "Enumeration, yields, appended callbacks, native catches and completion must remain intact.");
    }

    [Test]
    public void SupplierCallbackHasOneRealActionCallInsideItsExistingCatchRegion()
    {
        // The runtime references Mono's String.Contains overload, unavailable
        // in this .NET Framework host. Inspect metadata without resolving it;
        // the actual Harmony patch is qualified in the GOG functional capture.
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(Provider().Location);
        var factory = module.Types.Single(t => t.Name == "LongEventHandler_ExecuteToExecuteWhenFinished_Patches");
        var iterator = factory.NestedTypes.Single(t => t.Name.StartsWith("<ExecuteToExecuteWhenFinished>"));
        var body = iterator.Methods.Single(m => m.Name == "MoveNext").Body;
        var call = body.Instructions.Single(i => i.Operand is Mono.Cecil.MethodReference m
            && m.DeclaringType.FullName == "System.Action" && m.Name == "Invoke");
        Assert.That(body.ExceptionHandlers.Any(h => h.HandlerType == Mono.Cecil.Cil.ExceptionHandlerType.Catch
            && h.TryStart.Offset <= call.Offset && h.TryEnd.Offset > call.Offset), Is.True);
        // These original metadata owners ground the modern-host normalization.
        foreach (string type in new[] { "System.Linq.Enumerable", "System.Collections.Generic.HashSet`1" })
        {
            var reference = body.Instructions.Select(i => i.Operand).OfType<Mono.Cecil.MethodReference>()
                .First(m => m.DeclaringType.GetElementType().FullName == type);
            Assert.That(((Mono.Cecil.AssemblyNameReference)reference.DeclaringType.Scope).Name, Is.EqualTo("System.Core"));
        }
    }

    [Test]
    public void ActionObservationPreservesMulticastOrderAndOriginalException()
    {
        var order = new List<int>();
        var expected = new InvalidOperationException("original failure");
        Action action = () => order.Add(1);
        action += () => { order.Add(2); throw expected; };
        action += () => order.Add(3);
        Assert.That(Assert.Throws<InvalidOperationException>(new Action(() => LoadingInvocationObservation.ExecuteAction(action))), Is.SameAs(expected));
        Assert.That(order, Is.EqualTo(new[] { 1, 2 }));
        LoadingInvocationObservation.ExecuteAction(() => order.Add(4));
        Assert.That(order, Is.EqualTo(new[] { 1, 2, 4 }));
    }

    [Test]
    public void SuppressedNativeContentIsReportedAsHooksOnly()
    {
        var session = new LoadingSession("suppressed native content", true);
        var current = AccessTools.Field(typeof(LoadingObservationRuntime), "current");
        var previous = current.GetValue(null);
        var target = AccessTools.Method(typeof(ModContentPack), "ReloadContentInt");
        var observe = new Harmony("WakeUp.Tests.ContentObservation");
        var supplier = new Harmony("WakeUp.Tests.ContentReplacement");
        var mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        AccessTools.Field(typeof(ModContentPack), "packageIdInt").SetValue(mod, "test.content-owner");
        try
        {
            current.SetValue(null, session);
            observe.Patch(target, prefix: new HarmonyMethod(typeof(LoadingObservationRuntime), "BeforeContent"),
                finalizer: new HarmonyMethod(typeof(LoadingObservationRuntime), "AfterNativeContent"));
            supplier.Patch(target, prefix: new HarmonyMethod(typeof(LoadingInvocationObservationTests), nameof(SkipContent)) { priority = Priority.Last });
            target.Invoke(mod, new object[] { false });
            session.Finish("Completed");
            Assert.That(session.RenderReport(), Does.Contain("original body skipped for test.content-owner")
                .And.Contain("native call boundary contains hooks only")
                .And.Contain("Native content call boundaries").And.Not.Contain("Execute content reload"));
        }
        finally
        {
            observe.Unpatch(target, HarmonyPatchType.All, observe.Id);
            supplier.Unpatch(target, HarmonyPatchType.All, supplier.Id);
            current.SetValue(null, previous);
        }
    }
    private static bool SkipContent() => false;
}
