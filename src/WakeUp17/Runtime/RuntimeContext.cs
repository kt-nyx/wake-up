// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;

namespace WakeUp.V17;

internal sealed class RuntimeContext
{
    internal const string OwnerPrefix = "kt.nyx.wakeup.1.7";
    private readonly object gate = new();
    private readonly Dictionary<string, string> statuses = new(StringComparer.Ordinal);
    private readonly HashSet<(string, MethodInfo)> ownHooks = new();
    private readonly Action<string> logger;
    private readonly Func<MethodBase, bool> contract;
    internal Assembly Game { get; }
    internal bool StartupActive { get; private set; } = true;
    internal event Action? StartupCompleted;
    internal string Report { get { lock (gate) return string.Join("\n", statuses.Select(p => p.Key + ": " + p.Value)); } }

    internal RuntimeContext(Assembly game, Action<string> logger, Func<MethodBase, bool>? contract = null)
    { Game = game; this.logger = logger; this.contract = contract ?? RuntimeContract.AllowsMethod; }

    internal MethodInfo Method(string type, string name, params Type[] parameters)
        => AccessTools.Method(Game.GetType(type, true), name, parameters)
            ?? throw new InvalidOperationException("Missing method " + type + "." + name);
    internal MethodInfo NamedMethod(string type, string name)
        => AccessTools.Method(Game.GetType(type, true), name)
            ?? throw new InvalidOperationException("Missing method " + type + "." + name);

    internal void Require(MethodBase target)
    {
        if (!contract(target)) throw new InvalidOperationException("Unreviewed method: " + target.DeclaringType?.FullName + "." + target.Name);
    }

    internal PublishedPatchGuard Guard(MethodBase target) => Guard(target, allPatchKinds: true);
    internal PublishedPatchGuard Guard(MethodBase target, bool allPatchKinds)
    {
        // Never silently admit a patch merely because its owner string matches.
        if (!PublishedPatchGuard.TryCreate(target, OwnerPrefix + ".guard", out var result,
                allPatchKinds: allPatchKinds, allowedForeignPatch: OwnHook) || !result!.AllowsOriginalContract())
            throw new InvalidOperationException("Another patch owns " + target.DeclaringType?.Name + "." + target.Name);
        return result;
    }

    private bool OwnHook(Patch patch)
    { lock (gate) return ownHooks.Contains((patch.owner, patch.PatchMethod)); }

    internal void Patch(string feature, MethodBase target, MethodInfo? prefix = null,
        MethodInfo? postfix = null, MethodInfo? transpiler = null, MethodInfo? finalizer = null)
    {
        string owner = OwnerPrefix + "." + feature;
        HarmonyMethod? Hook(MethodInfo? method)
        {
            if (method == null) return null;
            lock (gate) ownHooks.Add((owner, method));
            return new HarmonyMethod(method) { priority = Priority.Last };
        }
        new Harmony(owner).Patch(target, Hook(prefix), Hook(postfix), Hook(transpiler), Hook(finalizer));
    }

    internal static MethodInfo Hook(Type type, string name) => AccessTools.Method(type, name)!;
    internal void Unpatch(string feature) => new Harmony(OwnerPrefix + "." + feature).UnpatchAll(OwnerPrefix + "." + feature);
    internal void UnpatchTarget(string feature, MethodBase target)
        => new Harmony(OwnerPrefix + "." + feature).Unpatch(target, HarmonyPatchType.All, OwnerPrefix + "." + feature);

    internal void Status(string feature, string message)
    {
        lock (gate)
        {
            if (statuses.TryGetValue(feature, out string? previous) && previous == message) return;
            statuses[feature] = message;
        }
        Log(feature + ": " + message);
    }
    internal void Log(string message)
    { try { logger("[Wake-Up 1.7] " + message); } catch { } }

    internal void Install(string feature, bool selected, Action install, Action clear)
    {
        if (!selected) { Status(feature, "Off for this launch."); return; }
        try { install(); StartupCompleted += clear; }
        catch (Exception error)
        {
            clear();
            try { Unpatch(feature); } catch { }
            Status(feature, "Ordinary loading: " + error.GetBaseException().Message);
        }
    }

    internal void CompleteStartup()
    {
        if (!StartupActive) return;
        StartupActive = false;
        foreach (Delegate action in StartupCompleted?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { ((Action)action)(); } catch (Exception error) { Log("Cleanup: " + error.GetType().Name); }
        StartupCompleted = null;
        Log("Startup search scope completed. Diagnostics begin at Wake-Up's mod constructor; earlier loading is unobserved.");
    }
}

internal static class RuntimeContract
{
    private static readonly JsonDocument Catalog = ReadCatalog();
    internal static string GameLabel => Catalog.RootElement.GetProperty("gameLabel").GetString()!;
    private static JsonDocument ReadCatalog()
    {
        using Stream source = typeof(RuntimeContract).Assembly.GetManifestResourceStream("WakeUp17.Contracts.json")!;
        return JsonDocument.Parse(source);
    }
    private static string Value(string name) => Catalog.RootElement.GetProperty(name).GetString()!;

    internal static void RequireRuntime(Assembly game)
    {
        if (!OperatingSystem.IsWindows() || Environment.Version.Major != 10)
            throw new InvalidOperationException("This testing candidate requires Windows and the .NET 10 game runtime.");
        Verify(game, "gameMvid", "gameSha256");
        Verify(typeof(Harmony).Assembly, "harmonyMvid", "harmonySha256");
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "PrepatcherImpl"))
            throw new InvalidOperationException("This 1.7 candidate requires an ordinary game assembly without Prepatcher.");
    }
    private static void Verify(Assembly assembly, string mvid, string sha)
    {
        if (assembly.ManifestModule.ModuleVersionId.ToString() != Value(mvid) || string.IsNullOrEmpty(assembly.Location))
            throw new InvalidOperationException("Unreviewed " + assembly.GetName().Name + " build. Expected " + GameLabel + " and the recorded Harmony 3 build.");
        using var file = File.OpenRead(assembly.Location);
        if (!Convert.ToHexString(SHA256.HashData(file)).Equals(Value(sha), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Changed " + assembly.GetName().Name + " file.");
    }

    internal static bool AllowsMethod(MethodBase method)
    {
        if (OptionalXmlQueries.AllowsMethod(method)) return true;
        if (method.Module.ModuleVersionId.ToString() != Value("gameMvid")) return false;
        if (!Catalog.RootElement.GetProperty("methods").TryGetProperty(method.MetadataToken.ToString(), out var entry)) return false;
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        return il != null && Convert.ToHexString(SHA256.HashData(il)).Equals(entry.GetProperty("ilSha256").GetString(), StringComparison.OrdinalIgnoreCase);
    }
}
