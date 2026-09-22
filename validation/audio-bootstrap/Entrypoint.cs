// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;

namespace Doorstop;

// Fixture-only early boundary. This assembly has no game, Unity,
// codec, Harmony or Wake-Up reference. It stays outside Prepatcher's mod set.
public static class Entrypoint
{
    private const string StateKey = "WakeUp.PreparedAudio.Bootstrap.v1";
    private const string CallbackKey = "WakeUp.PreparedAudio.Bootstrap.Mutation.v1";
    private const string HarmonyHash = "7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C";
    private const string HarmonyMvid = "024a0e6e-c8c2-437e-ad04-7b6279389c23";
    private const string Owner = "kt-nyx.wake-up.prepared-audio-bootstrap";
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly object Gate = new object();
    private static Dictionary<string, object>? state;
    private static AudioRegistry? audioRegistry;
    private static Action<Assembly>? harmonyLoadObserver;
    private static object? harmony;
    private static MethodInfo? patch;
    private static ConstructorInfo? harmonyMethod;
    private static MethodInfo? getPatchInfo;
    private static Assembly? oldGame, oldPrepatcher;
    private static bool guardsComplete;
    private static int originalLoadThread;
    private static int sequence;
    private static string? receiptPath;

    public static void Start()
    {
        // Take this before loading Harmony or reflecting over any target type.
        Assembly[] initial = AppDomain.CurrentDomain.GetAssemblies();
        if (!Environment.GetCommandLineArgs().Contains("--fixture-c10-bootstrap-probe")) return;
        lock (Gate)
        {
            if (state != null || AppDomain.CurrentDomain.GetData(StateKey) != null) return;
            audioRegistry = new AudioRegistry();
            audioRegistry.State["audioHarmonyLoadObserve"] = (Action<Action<Assembly>?>)(observer =>
            {
                Interlocked.Exchange(ref harmonyLoadObserver, observer);
            });
            state = new Dictionary<string, object>
            {
                ["version"] = 1, ["session"] = "", ["gate"] = Gate,
                ["audioRegistry"] = audioRegistry.State,
                ["oldHarmony"] = null!, ["gameBefore"] = null!,
                ["oldGuardsInstalled"] = false, ["finalGuardsEmitted"] = false,
                ["failure"] = "", ["entryEvents"] = new List<object[]>(), ["finalCallbackEntries"] = 0,
                ["sharedAtGuard"] = Array.Empty<Assembly>(), ["admissionClosed"] = true,
                ["preloader"] = true, ["preloaderQualified"] = false, ["preloaderCompleted"] = false,
                ["initialAssemblies"] = initial.Take(256).ToArray(),
                ["initialRuntimeAssemblies"] = initial.Where(a => !a.ReflectionOnly).Take(256).ToArray(),
                ["afterHarmonyAssemblies"] = Array.Empty<Assembly>(), ["afterGuardAssemblies"] = Array.Empty<Assembly>(),
                ["afterHarmonyRuntimeAssemblies"] = Array.Empty<Assembly>(), ["afterGuardRuntimeAssemblies"] = Array.Empty<Assembly>(),
                ["chronology"] = new List<object[]>(), ["chronologyTruncated"] = false,
                ["lateChronologyOmitted"] = 0, ["finalGuardEventRecorded"] = false,
                ["createModClassesEntries"] = 0, ["prepatcherConstructorEntries"] = 0,
                ["createModClassesPrefixInstalled"] = false, ["prepatcherConstructorPrefixInstalled"] = false,
                ["firstExtensionRecorded"] = false,
                ["observeExtension"] = (Action<Assembly, Assembly>)ObserveExtension
            };
            AppDomain.CurrentDomain.SetData(StateKey, state);
            AppDomain.CurrentDomain.SetData(CallbackKey, (Action<Assembly, MethodBase>)ObserveMutation);
            Record("doorstop-entry", typeof(Entrypoint).Assembly, null);
        }
        try
        {
            AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded;
            var config = ReadConfig();
            lock (Gate)
            {
                state["session"] = config.Session;
                state["processPath"] = config.ProcessPath;
                state["harmonyPath"] = config.HarmonyPath;
                receiptPath = config.ReceiptPath;
            }
            WriteReceipt("entry");
            if (Environment.GetCommandLineArgs().Contains("--fixture-c10-native-entry-probe"))
                NativeEntry.Bind(audioRegistry!, receiptPath!);
            if (initial.Length > 256) throw new InvalidOperationException("initial-assembly-snapshot-truncated");
            if (((Assembly[])state["initialRuntimeAssemblies"]).Any(a => IsProtected(a) || a.GetName().Name == "0Harmony"))
                throw new InvalidOperationException("Harmony-game-or-codec-loaded-at-doorstop-entry");

            Assembly loadedHarmony;
            lock (Gate) originalLoadThread = Thread.CurrentThread.ManagedThreadId;
            try { loadedHarmony = Assembly.LoadFrom(config.HarmonyPath); }
            finally { lock (Gate) originalLoadThread = 0; }
            if (loadedHarmony.ReflectionOnly || loadedHarmony.GetName().Name != "0Harmony" || loadedHarmony.GetName().Version?.ToString() != "2.4.2.0"
                || loadedHarmony.ManifestModule.ModuleVersionId.ToString("D") != HarmonyMvid
                || !SamePath(loadedHarmony.Location, config.HarmonyPath))
                throw new InvalidOperationException("loaded-Harmony-does-not-match-configured-original");
            lock (Gate)
            {
                state["oldHarmony"] = loadedHarmony;
                Assembly[] after = Snapshot();
                state["afterHarmonyAssemblies"] = after;
                state["afterHarmonyRuntimeAssemblies"] = after.Where(a => !a.ReflectionOnly).ToArray();
                Record("harmony-loaded", loadedHarmony, null);
            }
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => !a.ReflectionOnly && IsProtected(a)))
                throw new InvalidOperationException("game-or-codec-loaded-before-guards");
            var harmonyType = loadedHarmony.GetType("HarmonyLib.Harmony", true)!;
            var methodType = loadedHarmony.GetType("HarmonyLib.HarmonyMethod", true)!;
            harmony = Activator.CreateInstance(harmonyType, Owner)!;
            harmonyMethod = methodType.GetConstructor(new[] { typeof(MethodInfo) })
                ?? throw new MissingMethodException("HarmonyMethod(MethodInfo)");
            patch = harmonyType.GetMethod("Patch", new[] { typeof(MethodBase), methodType, methodType, methodType, methodType })
                ?? throw new MissingMethodException("Harmony.Patch");
            getPatchInfo = harmonyType.GetMethod("GetPatchInfo", BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException("Harmony.GetPatchInfo");
            Type functions = loadedHarmony.GetType("HarmonyLib.PatchFunctions", true)!;
            MethodInfo update = functions.GetMethod("UpdateWrapper", Members) ?? throw new MissingMethodException("UpdateWrapper");
            MethodInfo reverse = functions.GetMethod("ReversePatch", Members) ?? throw new MissingMethodException("ReversePatch");
            RequireUnpatched(update); RequireUnpatched(reverse);
            Install(update, nameof(UpdatePrefix));
            Install(reverse, nameof(ReversePrefix));
            audioRegistry!.GuardInstallationComplete();
            lock (Gate)
            {
                // Snapshot after both calls returned: replacement and Harmony
                // patch-state publication have completed for both entry guards.
                Assembly[] after = Snapshot();
                state["afterGuardAssemblies"] = after;
                Assembly[] runtime = after.Where(a => !a.ReflectionOnly).ToArray();
                state["afterGuardRuntimeAssemblies"] = runtime;
                state["sharedAtGuard"] = runtime.Where(IsCodec).ToArray();
                state["oldGuardTargets"] = new[] { update, reverse };
                state["oldGuardsInstalled"] = true;
                if (runtime.Any(IsProtected)) Fail("game-or-codec-loaded-during-guard-installation");
                guardsComplete = true;
                Record("both-old-guards-complete", loadedHarmony, null);
                state["guardCompletionSequence"] = sequence;
                state["preloaderQualified"] = ((string)state["failure"]).Length == 0;
                state["preloaderCompleted"] = true;
            }
            WriteReceipt("guards-complete");
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
            WriteReceipt("failed");
        }
    }

    private static void AssemblyLoaded(object? sender, AssemblyLoadEventArgs args)
    {
        Assembly assembly = args.LoadedAssembly;
        // Returning to the loading caller must not bypass unfinished settlement.
        if (!assembly.ReflectionOnly && assembly.GetName().Name == "0Harmony")
        {
            // Disposable fixture observation distinguishes a repeated load event
            // from waiting inside the loader. No bootstrap/registry lock is held.
            try { Volatile.Read(ref harmonyLoadObserver)?.Invoke(assembly); }
            catch (Exception) { /* Diagnostic failure cannot skip mandatory settlement. */ }
            bool original, replacement;
            lock (Gate)
            {
                original = originalLoadThread == Thread.CurrentThread.ManagedThreadId && originalLoadThread != 0
                    && state != null && state["oldHarmony"] == null
                    && state.TryGetValue("harmonyPath", out object path) && SamePath(assembly.Location, (string)path)
                    && assembly.ManifestModule.ModuleVersionId.ToString("D") == HarmonyMvid;
                replacement = state != null && (bool)state["firstExtensionRecorded"] && (bool)state["finalGuardsEmitted"]
                    && state["oldHarmony"] is Assembly previous && previous.ReflectionOnly;
            }
            replacement = replacement && assembly.GetType("WakeUp.Generated.PreparedAudioBootstrapV1", false) != null;
            if (audioRegistry != null && !audioRegistry.ObserveHarmonyAssembly(assembly, original, replacement))
                Fail("unexpected-Harmony-assembly: " + assembly.FullName);
        }
        try
        {
            string name = assembly.GetName().Name ?? "";
            bool observeGame = false, observePrepatcher = false;
            lock (Gate)
            {
                Record(assembly.ReflectionOnly ? "assembly-load-reflection-only" : "assembly-load", assembly, null);
                if (!guardsComplete && !assembly.ReflectionOnly && IsProtected(assembly)) Fail("protected-assembly-exposed-before-guard-completion: " + name);
                if (!guardsComplete || assembly.ReflectionOnly || state == null || !(bool)state["preloaderQualified"]) return;
                if (name == "Assembly-CSharp" && oldGame == null)
                {
                    oldGame = assembly; state["gameBefore"] = assembly; observeGame = true;
                }
                if (name == "PrepatcherImpl" && oldPrepatcher == null)
                {
                    oldPrepatcher = assembly; state["oldPrepatcher"] = assembly; observePrepatcher = true;
                }
            }
            // Only metadata and Harmony patch construction here. Do not invoke
            // these native methods or ask a loader to resolve a missing assembly.
            if (observeGame)
            {
                MethodInfo target = assembly.GetType("Verse.LoadedModManager", true)!.GetMethod("CreateModClasses", Members)
                    ?? throw new MissingMethodException("LoadedModManager.CreateModClasses");
                RequireUnpatched(target); Install(target, nameof(CreateModClassesPrefix));
                lock (Gate) { state!["createModClassesPrefixInstalled"] = true; Record("old-create-mod-classes-observer-installed", assembly, target); }
            }
            if (observePrepatcher)
            {
                // If game metadata is not available naturally, retain unknown.
                if (oldGame == null) throw new InvalidOperationException("Prepatcher-loaded-before-observed-old-game");
                Type content = oldGame.GetType("Verse.ModContentPack", true)!;
                ConstructorInfo target = assembly.GetType("Prepatcher.PrepatcherMod", true)!.GetConstructor(Members, null, new[] { content }, null)
                    ?? throw new MissingMethodException("PrepatcherMod(ModContentPack)");
                RequireUnpatched(target); Install(target, nameof(PrepatcherConstructorPrefix));
                lock (Gate) { state!["prepatcherConstructorPrefixInstalled"] = true; Record("old-prepatcher-constructor-observer-installed", assembly, target); }
            }
        }
        catch (Exception exception) { Fail("assembly-load-observation: " + exception); WriteReceipt("observation-failed"); }
    }

    private static void Install(MethodBase target, string prefix)
    {
        MethodInfo method = typeof(Entrypoint).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)!;
        object descriptor = harmonyMethod!.Invoke(new object[] { method });
        patch!.Invoke(harmony, new object?[] { target, descriptor, null, null, null });
    }

    private static void RequireUnpatched(MethodBase target)
    {
        object? info = getPatchInfo!.Invoke(null, new object[] { target });
        if (info == null) return;
        foreach (string name in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers", "InnerPrefixes", "InnerPostfixes" })
        {
            object? values = info.GetType().GetProperty(name, Members)?.GetValue(info, null)
                ?? info.GetType().GetField(name, Members)?.GetValue(info);
            if (values is not IEnumerable items) throw new InvalidOperationException("unknown-Harmony-patch-info-layout");
            foreach (object _ in items) throw new InvalidOperationException("observation-target-already-patched: " + target);
        }
    }

    private static void UpdatePrefix(MethodBase __0, MethodBase __originalMethod)
        => ObserveMutation(__originalMethod.Module.Assembly, __0);
    private static void ReversePrefix(object __0, MethodBase __originalMethod)
    {
        MethodBase? target;
        try
        {
            target = __0?.GetType().GetField("method", Members)?.GetValue(__0) as MethodBase;
        }
        catch (Exception exception) { Fail("reverse-entry-observation: " + exception); throw; }
        if (target != null) ObserveMutation(__originalMethod.Module.Assembly, target);
    }
    private static void ObserveMutation(Assembly caller, MethodBase target)
    {
        try
        {
            lock (Gate)
            {
                if (state == null || target == null) return;
                if (!ReferenceEquals(caller, state["oldHarmony"])) state["finalCallbackEntries"] = (int)state["finalCallbackEntries"] + 1;
                var entries = (List<object[]>)state["entryEvents"];
                if (entries.Count < 128) entries.Add(new object[] { caller, target });
                Record("mutation-entry", caller, target);
            }
        }
        catch (Exception exception) { Fail("mutation-observation: " + exception); }
        // Mandatory settlement is outside the diagnostic lock and catch. A
        // failure here must stop replacement while the old method still exists.
        if (target != null) audioRegistry?.BeforeMutation(target);
    }
    private static void CreateModClassesPrefix(MethodBase __originalMethod) => ActualEntry("createModClassesEntries", "old-create-mod-classes-entry", __originalMethod);
    private static void PrepatcherConstructorPrefix(MethodBase __originalMethod) => ActualEntry("prepatcherConstructorEntries", "old-prepatcher-constructor-entry", __originalMethod);
    private static void ActualEntry(string count, string name, MethodBase method)
    {
        try { lock (Gate) { if (state == null) return; state[count] = (int)state[count] + 1; Record(name, method.Module.Assembly, method); } }
        catch (Exception exception) { Fail("native-entry-observation: " + exception); }
    }

    private static void ObserveExtension(Assembly harmonyAssembly, Assembly gameAssembly)
    {
        try
        {
            lock (Gate)
            {
                if (state == null || (bool)state["firstExtensionRecorded"]) return;
                state["firstExtensionRecorded"] = true;
                state["firstExtensionHarmony"] = harmonyAssembly;
                state["firstExtensionGame"] = gameAssembly;
                if (!ReferenceEquals(harmonyAssembly, state["oldHarmony"])) Fail("first-extension-uses-unguarded-Harmony-object");
                if (!ReferenceEquals(gameAssembly, oldGame)) Fail("first-extension-game-object-not-observed");
                if (state["gameBefore"] == null) state["gameBefore"] = gameAssembly;
                Record("first-wake-up-extension", gameAssembly, null);
            }
            WriteReceipt("first-extension");
        }
        catch (Exception exception) { Fail("extension-observation: " + exception); }
    }

    private static Assembly[] Snapshot()
    {
        Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
        if (all.Length > 256) Fail("assembly-snapshot-truncated");
        return all.Take(256).ToArray();
    }
    private static bool IsCodec(Assembly assembly) => assembly.GetName().Name == "NAudio" || assembly.GetName().Name == "NVorbis";
    private static bool IsProtected(Assembly assembly) => assembly.GetName().Name == "Assembly-CSharp" || IsCodec(assembly);
    private static void Record(string name, Assembly? assembly, MethodBase? method)
    {
        // Caller owns Gate; Monitor is reentrant during Harmony callbacks.
        if (state == null) return;
        if ((bool)state["firstExtensionRecorded"] && name != "first-wake-up-extension")
        {
            bool retain = (name == "assembly-load" || name == "assembly-load-reflection-only")
                && assembly != null && (IsProtected(assembly) || assembly.GetName().Name == "0Harmony");
            if (name == "mutation-entry" && !ReferenceEquals(assembly, state["oldHarmony"]) && !(bool)state["finalGuardEventRecorded"])
            {
                state["finalGuardEventRecorded"] = true;
                retain = true;
            }
            if (!retain) { state["lateChronologyOmitted"] = (int)state["lateChronologyOmitted"] + 1; return; }
        }
        // Sequence numbers enumerate retained events. The causal prefix up to
        // the first extension is complete; ordinary later work is summarized.
        sequence++;
        var events = (List<object[]>)state["chronology"];
        if (events.Count < 512) events.Add(new object[] { sequence, name, Thread.CurrentThread.ManagedThreadId, assembly!, method! });
        else if (!(bool)state["firstExtensionRecorded"] || name == "first-wake-up-extension")
        { state["chronologyTruncated"] = true; Fail("chronology-truncated-before-first-extension"); }
        else state["lateChronologyOmitted"] = (int)state["lateChronologyOmitted"] + 1;
    }
    private static void Fail(string reason)
    {
        lock (Gate)
        {
            if (state == null) return;
            if (((string)state["failure"]).Length == 0) state["failure"] = reason;
            state["preloaderQualified"] = false;
        }
    }

    private sealed class Config
    {
        internal string Session = "", ProcessPath = "", HarmonyPath = "", ReceiptPath = "";
    }
    private static Config ReadConfig()
    {
        string process = Absolute(Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH")
            ?? throw new InvalidOperationException("DOORSTOP_PROCESS_PATH-missing"));
        string game = Path.GetDirectoryName(process) ?? throw new InvalidOperationException("game-directory-missing");
        var gameDirectory = new DirectoryInfo(game);
        if (gameDirectory.Name != "game" || gameDirectory.Parent?.Name != ".rlo-test-instance")
            throw new InvalidOperationException("not-the-isolated-fixture-game-directory");
        string profile = Path.Combine(gameDirectory.Parent!.FullName, "profile");
        string[] saves = Environment.GetCommandLineArgs().Where(a => a.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (saves.Length != 1 || !SamePath(Absolute(saves[0].Substring("-savedatafolder=".Length)), profile))
            throw new InvalidOperationException("bootstrap-savedatafolder-is-not-fixture-profile");
        string configPath = Path.Combine(game, "WakeUpBootstrap", "bootstrap.xml");
        if (!File.Exists(configPath) || new FileInfo(configPath).Length > 16384) throw new InvalidOperationException("bootstrap-config-missing-or-large");
        var document = new XmlDocument { XmlResolver = null };
        using (var reader = XmlReader.Create(configPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16384 })) document.Load(reader);
        XmlElement node = document.DocumentElement ?? throw new InvalidOperationException("bootstrap-config-empty");
        if (node.Name != "bootstrap" || !SamePath(Absolute(node.GetAttribute("fixtureGame")), game)) throw new InvalidOperationException("bootstrap-fixture-path-mismatch");
        string library = Absolute(node.GetAttribute("harmonyPath"));
        string receipt = Absolute(node.GetAttribute("receiptPath"));
        if (!Under(library, Path.Combine(game, "Mods")) || Path.GetFileName(library) != "0Harmony.dll"
            || !SamePath(receipt, Path.Combine(profile, "FixtureMenuObserver", "c10-preloader-early.xml")))
            throw new InvalidOperationException("bootstrap-path-outside-fixture");
        string session = node.GetAttribute("session");
        if (session.Length == 0 || session.Length > 128) throw new InvalidOperationException("bootstrap-session-invalid");
        // A validated receipt path can retain hash/load failures too. Do not
        // derive any failure output path from an unvalidated configuration.
        lock (Gate)
        {
            receiptPath = receipt;
            state!["session"] = session;
            state["processPath"] = process;
            state["harmonyPath"] = library;
        }
        if (!string.Equals(node.GetAttribute("harmonySha256"), HarmonyHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("bootstrap-Harmony-contract-mismatch");
        using (var input = File.OpenRead(library))
        using (var hash = SHA256.Create())
            if (BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "") != HarmonyHash)
                throw new InvalidOperationException("bootstrap-Harmony-file-changed");
        return new Config { Session = session, ProcessPath = process, HarmonyPath = library, ReceiptPath = receipt };
    }
    private static string Absolute(string path) => Path.IsPathRooted(path)
        ? Path.GetFullPath(path) : throw new InvalidOperationException("bootstrap-path-not-absolute");
    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    private static bool Under(string path, string directory) => path.StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void WriteReceipt(string stage)
    {
        try
        {
            lock (Gate)
            {
                if (receiptPath == null || state == null) return;
                Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
                using var writer = XmlWriter.Create(receiptPath, new XmlWriterSettings { Indent = true });
                writer.WriteStartElement("c10-preloader-early");
                writer.WriteAttributeString("session", (string)state["session"]);
                writer.WriteAttributeString("stage", stage);
                writer.WriteAttributeString("qualified", state["preloaderQualified"].ToString());
                writer.WriteAttributeString("admissionClosed", "true");
                writer.WriteAttributeString("sequence", sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteElementString("failure", (string)state["failure"]);
                writer.WriteEndElement();
            }
        }
        catch (Exception exception) { Fail("early-receipt-write: " + exception); }
    }
}
