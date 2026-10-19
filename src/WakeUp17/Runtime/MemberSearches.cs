// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

namespace WakeUp.V17;

internal sealed class MemberSearches
{
    internal const string Feature = "members";
    private static MemberSearches? instance;
    private readonly RuntimeContext context;
    private readonly bool loading, translations;
    private readonly ConcurrentDictionary<MethodBase, PublishedPatchGuard> consumers = new();
    private readonly List<PublishedPatchGuard> operations = new();
    private LoadingReflectionIndex? startup;
    private int generation;
    private bool enabled;
    [ThreadStatic] private static PublishedPatchGuard? consumer;
    [ThreadStatic] private static LoadingReflectionIndex? language;
    internal MemberSearches(RuntimeContext context, bool loading, bool translations)
    { this.context = context; this.loading = loading; this.translations = translations; instance = this; }

    internal void Install()
    {
        var targets = new List<MethodInfo>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        void Add(string type, params string[] names) => targets.AddRange(context.Game.GetType(type, true)!.GetMethods(flags).Where(m => names.Contains(m.Name)));
        if (loading)
        {
            Add("Verse.DirectXmlCrossRefLoader", "RegisterObjectWantsCrossRef");
            Add("Verse.GenGeneric", "MethodOnGenericType", "InvokeGenericMethod", "InvokeStaticGenericMethod", "PropertyOnGenericType");
            Add("Verse.XmlToObjectUtils", "DoFieldSearch");
            Add("Verse.XmlHelper", "ParseElements", "TryParseSingleDefault");
        }
        if (translations) Add("Verse.DefInjectionPackage", "SetDefFieldAtPath", "GetFieldNamed");
        MethodInfo? package = translations ? context.NamedMethod("Verse.DefInjectionPackage", "InjectIntoDefs") : null;
        Install(targets, package);
    }
    internal void Install(IEnumerable<MethodInfo> targets, MethodInfo? translationPackage)
    {
        if (translationPackage != null) context.Require(translationPackage);
        foreach (MethodInfo method in NativeOperations()) operations.Add(context.Guard(method));
        foreach (MethodInfo target in targets.Distinct())
        {
            bool patchAttempted = false;
            try
            {
                context.Require(target);
                var guard = context.Guard(target);
                if (!PatchProcessor.GetOriginalInstructions(target).Any(i => i.operand is MethodInfo method && Replacement(method) != null))
                    continue;
                if (!consumers.TryAdd(target, guard)) continue;
                patchAttempted = true;
                context.Patch(Feature, target, prefix: Hook(nameof(Begin)), finalizer: Hook(nameof(End)), transpiler: Hook(nameof(Rewrite)));
            }
            catch (Exception error)
            {
                if (patchAttempted)
                {
                    consumers.TryRemove(target, out _);
                    try { context.UnpatchTarget(Feature, target); } catch { }
                }
                context.Status(Feature + "/" + target.DeclaringType!.Name + "." + target.Name, "Ordinary lookup: " + error.Message);
            }
        }
        if (consumers.Count == 0) throw new InvalidOperationException("No compatible member lookup consumers.");
        if (translationPackage != null)
        {
            // Hooks on the containing package may introduce arbitrary mutation
            // or reentrancy; member metadata itself owns no game object values.
            context.Guard(translationPackage);
            context.Patch(Feature, translationPackage, prefix: Hook(nameof(BeginLanguage)), finalizer: Hook(nameof(EndLanguage)));
        }
        AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded;
        startup = loading ? new LoadingReflectionIndex() : null;
        enabled = true;
        context.Status(Feature, "Available for " + consumers.Count + " loading consumers. Translation member reuse " + (translations ? "on" : "off") + "; duplicate checking stays native.");
    }

    private void AssemblyLoaded(object? sender, AssemblyLoadEventArgs args) => Interlocked.Increment(ref generation);
    private static MethodInfo Hook(string name) => RuntimeContext.Hook(typeof(MemberSearches), name);
    private static IEnumerable<MethodInfo> NativeOperations()
    {
        foreach (Type type in new[] { typeof(Type), typeof(object).GetType() })
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (type == typeof(Type) ? Replacement(method) != null : new[] { "GetField", "GetMethodImpl", "GetPropertyImpl", "GetFields" }.Contains(method.Name))
                yield return method;
        }
    }
    private LoadingReflectionIndex? Current
    {
        get
        {
            var selected = language ?? (context.StartupActive ? startup : null);
            return selected != null && enabled && consumer?.AllowsOriginalContract() == true
                && operations.All(g => g.AllowsOriginalContract()) ? selected : null;
        }
    }

    private static void Begin(MethodBase __originalMethod, out PublishedPatchGuard? __state)
    {
        __state = consumer;
        instance!.consumers.TryGetValue(__originalMethod, out consumer);
    }
    private static void End(PublishedPatchGuard? __state) => consumer = __state;
    private static void BeginLanguage(out LoadingReflectionIndex? __state)
    { __state = language; language = new LoadingReflectionIndex(); }
    private static void EndLanguage(LoadingReflectionIndex? __state)
    {
        var completed = language; language = __state;
        if (completed == null) return;
        instance!.context.Log("Translation member scope: " + completed.Hits + " hits, " + completed.Misses + " native lookups; metadata released.");
        completed.Clear();
    }
    private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (!InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(__originalMethod)))
        {
            instance!.consumers.TryRemove(__originalMethod, out _);
            instance.context.Status(Feature + "/" + __originalMethod.Name, "Ordinary lookup: method changed during patch rebuilding.");
            return code;
        }
        foreach (CodeInstruction instruction in code)
        {
            if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) || instruction.operand is not MethodInfo method) continue;
            MethodInfo? replacement = Replacement(method);
            if (replacement == null) continue;
            instruction.opcode = OpCodes.Call; instruction.operand = replacement;
        }
        return code;
    }
    internal static MethodInfo? Replacement(MethodInfo method)
    {
        if (method.DeclaringType != typeof(Type)) return null;
        Type[] args = method.GetParameters().Select(p => p.ParameterType).ToArray();
        string? name = null;
        if (args.SequenceEqual(new[] { typeof(string), typeof(BindingFlags) }))
            name = method.Name == "GetField" ? nameof(Field) : method.Name == "GetMethod" ? nameof(Method) : method.Name == "GetProperty" ? nameof(Property) : null;
        else if (args.SequenceEqual(new[] { typeof(string) }))
            name = method.Name == "GetField" ? nameof(DefaultField) : method.Name == "GetProperty" ? nameof(DefaultProperty) : null;
        else if (method.Name == "GetFields" && args.SequenceEqual(new[] { typeof(BindingFlags) })) name = nameof(Fields);
        return name == null ? null : Hook(name);
    }
    private static FieldInfo? Field(Type type, string name, BindingFlags flags)
        => instance!.Current is { } index ? index.Field(type, name, flags, Volatile.Read(ref instance.generation)) : type.GetField(name, flags);
    private static MethodInfo? Method(Type type, string name, BindingFlags flags)
        => instance!.Current is { } index ? index.Method(type, name, flags, Volatile.Read(ref instance.generation)) : type.GetMethod(name, flags);
    private static PropertyInfo? Property(Type type, string name, BindingFlags flags)
        => instance!.Current is { } index ? index.Property(type, name, flags, Volatile.Read(ref instance.generation)) : type.GetProperty(name, flags);
    private static FieldInfo? DefaultField(Type type, string name) => Field(type, name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
    private static PropertyInfo? DefaultProperty(Type type, string name) => Property(type, name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
    private static FieldInfo[] Fields(Type type, BindingFlags flags)
        => instance!.Current is { } index ? index.Fields(type, flags, Volatile.Read(ref instance.generation)) : type.GetFields(flags);

    internal void Clear()
    {
        LoadingReflectionIndex? done = Interlocked.Exchange(ref startup, null);
        if (done != null)
        {
            context.Status(Feature, "Startup completed: " + done.Hits + " reused member lookups, " + done.Misses + " native lookups; startup metadata released.");
            done.Clear();
        }
        if (!translations) { enabled = false; AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded; }
    }
    internal void Dispose()
    {
        Clear(); enabled = false;
        AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded;
    }
}
