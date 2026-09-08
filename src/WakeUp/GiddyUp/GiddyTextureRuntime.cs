// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
namespace WakeUp;

internal static class GiddyTextureRuntime
{
    internal const string Owner = "wakeup.giddy-textures";
    internal const string SupplierSha256 = "E54C416CA81A1542A42577E9B13C36A79D054B28F5EB0B3B47AAEBD13C3CAB19";
    internal static readonly Guid SupplierMvid = new("e06e404d-ffd8-4a3b-a0ac-2285d47573a8");
    internal static readonly IReadOnlyDictionary<int, string> Bodies = new Dictionary<int, string>
    {
        [0x060000E1] = "3FCDFFD6DA25B8595C63E3DF8042A010AD9143A1F8DE766E774C0C4D477866C1", // SetDrawOffset
        [0x060000E2] = "E53AD35536452B891184F33385F6BA42ABB5A0C1000B6A589058B86151888F4C", // GetBackHeight
        [0x060000E3] = "A98EA42DECBDCA199FB20E85E03E5EB3AECCD0E3DB82E5AB57EF666EB61CAF6E", // GetReadableTexture
        [0x060000FE] = "E15D357DA30E93EE51B34311A812F2D67AB26FAFDFB630D828A6D350A3DC8069", // ProcessPawnKinds
    };
    private static bool attempted, enabled, verify, completed;
    private static int mainThread;
    private static string? evidencePath;
    private static MethodInfo? offset, readable;
    private static Func<Texture2D, Texture2D>? original;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static Scope? scope;

    internal static bool TryInitialize(IReadOnlyList<string> arguments)
    {
        string[] selectors = arguments.Where(a => a.StartsWith("--wake-up-giddy-textures=", StringComparison.Ordinal)).ToArray();
        if (selectors.Length != 1 || !new[] { "--wake-up-giddy-textures=on", "--wake-up-giddy-textures=timing", "--wake-up-giddy-textures=verify" }.Contains(selectors[0]))
            return false;
        if (attempted)
            return true;
        attempted = true;
        var harmony = new Harmony(Owner);
        try
        {
            StartupLaunchDecision mode = StartupLaunchSelector.Parse(arguments);
            if (mode.Selection != StartupSelection.Candidate || PlayDataLoader.Loaded
                || arguments.Count(a => a.StartsWith("--wake-up-strategy=", StringComparison.Ordinal)) != 1
                || !arguments.Contains("--wake-up-strategy=startup-searches"))
                return true;
            evidencePath = Path.Combine(mode.SaveDataRoot!, "WakeUp", "giddy-textures.jsonl");
            ModContentPack? supplier = LoadedModManager.RunningModsListForReading.SingleOrDefault(m => m.PackageId.Equals("memegoddess.giddyup", StringComparison.OrdinalIgnoreCase));
            if (supplier == null)
            {
                Receipt("inactive", "supplier-absent");
                return true;
            }
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason))
            {
                Receipt("refused", reason);
                return true;
            }
            Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "GiddyUpCore");
            if (assembly == null || !ValidateSupplier(assembly, Path.Combine(supplier.RootDir, "1.6", "Assemblies", "GiddyUpCore.dll")) || !ValidateBodies(assembly))
            {
                Receipt("refused", "supplier-identity");
                return true;
            }
            var methods = Bodies.Keys.Select(t => (MethodInfo)assembly.ManifestModule.ResolveMethod(t)).ToArray();
            offset = methods.Single(m => m.Name == "SetDrawOffset");
            readable = methods.Single(m => m.Name == "GetReadableTexture");
            original = (Func<Texture2D, Texture2D>)Delegate.CreateDelegate(typeof(Func<Texture2D, Texture2D>), readable);
            var list = new List<PublishedPatchGuard>();
            foreach (MethodInfo method in methods)
            {
                if (!PublishedPatchGuard.TryCreate(method, Owner, out var guard, allPatchKinds: true) || !guard!.AllowsOriginalContract())
                {
                    Receipt("refused", "foreign-texture-chain-patch");
                    return true;
                }
                list.Add(guard);
            }
            guards = list.ToArray();
            enabled = selectors[0] != "--wake-up-giddy-textures=timing";
            verify = selectors[0] == "--wake-up-giddy-textures=verify";
            harmony.Patch(offset, prefix: new HarmonyMethod(typeof(GiddyTextureRuntime), nameof(OffsetEnter)),
                finalizer: new HarmonyMethod(typeof(GiddyTextureRuntime), nameof(OffsetExit)),
                transpiler: new HarmonyMethod(typeof(GiddyTextureRuntime), nameof(Transpiler)) { priority = Priority.Last });
            mainThread = 0;
            harmony.Patch(AccessTools.Method(typeof(global::RimWorld.MainMenuDrawer), "MainMenuOnGUI"), postfix: new HarmonyMethod(typeof(GiddyTextureRuntime), nameof(Menu)));
            Receipt("installed", selectors[0]);
        }
        catch (Exception e) { enabled = false; try { harmony.UnpatchAll(Owner); } catch { } Receipt("refused", "installation-" + e.GetType().Name); }
        return true;
    }
    internal static bool MatchesSupplierIdentity(string? version, Guid mvid, string hash) => version == "2.2.5.0" && mvid == SupplierMvid && hash == SupplierSha256;
    internal static bool ValidateSupplier(Assembly assembly, string path)
    {
        if (!string.IsNullOrEmpty(assembly.Location) && !string.Equals(Path.GetFullPath(assembly.Location), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            return false;
        using var file = File.OpenRead(path);
        using var sha = SHA256.Create();
        return MatchesSupplierIdentity(assembly.GetName().Version?.ToString(), assembly.ManifestModule.ModuleVersionId, BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""));
    }
    internal static bool ValidateBodies(Assembly assembly)
    {
        using var sha = SHA256.Create();
        return Bodies.All(b => BitConverter.ToString(sha.ComputeHash(assembly.ManifestModule.ResolveMethod(b.Key).GetMethodBody()!.GetILAsByteArray())).Replace("-", "") == b.Value);
    }
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (offset == null || !InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(offset)))
            return code;
        if (code.Count(i => i.opcode == OpCodes.Call && Equals(i.operand, readable)) != 1)
            throw new InvalidOperationException("Unique readable texture call unavailable.");
        foreach (var i in code)
        if (i.opcode == OpCodes.Call && Equals(i.operand, readable))
            i.operand = AccessTools.Method(typeof(GiddyTextureRuntime), nameof(Read));
        return code;
    }
    private static void OffsetEnter(out long __state)
    {
        __state = 0;
        if (completed)
            return;
        Interlocked.CompareExchange(ref mainThread, Thread.CurrentThread.ManagedThreadId, 0);
        if (Thread.CurrentThread.ManagedThreadId != mainThread)
            return;
        if (scope == null)
            scope = new Scope();
        __state = Stopwatch.GetTimestamp();
    }
    private static void OffsetExit(float? __result, Exception? __exception, long __state)
    {
        if (scope == null || __state == 0)
            return;
        scope.Ticks += Stopwatch.GetTimestamp() - __state;
        scope.Calls++;
        if (__exception != null)
            scope.Errors++;
        scope.Values.Write(__result.HasValue);
        if (__result.HasValue)
            scope.Values.Write(__result.Value);
    }
    private static Texture2D Read(Texture2D source)
    {
        Scope? s = scope;
        if (!enabled || s == null || completed || Thread.CurrentThread.ManagedThreadId != mainThread)
            return original!(source);
        if (!guards.All(g => g.AllowsOriginalContract()) || !TexturePlatformSupport.SupportsReadback(GameBuildContract.Current, SystemInfo.graphicsDeviceType) || s.Textures.Count >= 4096)
        {
            s.Fallbacks++;
            return original!(source);
        }
        Texture2D? column = null;
        try
        {
            column = ReadColumn(source);
            if (verify)
            {
                Texture2D full = original!(source);
                try
                {
                    for (int y = 0; y < source.height; y++)
                        if (full.GetPixel(source.width / 2, y).a != column.GetPixel(0, y).a)
                        {
                            s.Mismatches++;
                            enabled = false;
                            throw new InvalidOperationException("alpha-readback-mismatch");
                        }
                    s.Verified++;
                }
                finally { UnityEngine.Object.Destroy(full); }
            }
            s.Textures.Add(column);
            s.Hits++;
            s.Pixels += source.width * (long)source.height;
            return column;
        }
        catch
        {
            if (column != null)
                UnityEngine.Object.Destroy(column);
            s.Errors++;
            s.Fallbacks++;
            return original!(source);
        }
    }
    private static Texture2D ReadColumn(Texture2D source)
    {
        // Keep the supplier's complete blit and conversion. Read only the same
        // integer center column; its original CPU GetBackHeight loop is unchanged.
        // ReadPixels uses lower-left coordinates on both admitted backends;
        // leave OpenGL orientation to Unity, with no additional vertical flip.
        RenderTexture active = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Texture2D? result = null;
        try
        {
            Graphics.Blit(source, temporary);
            active = RenderTexture.active;
            RenderTexture.active = temporary;
            result = new Texture2D(1, source.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(source.width / 2, 0, 1, source.height), 0, 0, false);
            return result;
        }
        catch { if (result != null) UnityEngine.Object.Destroy(result); throw; }
        finally { RenderTexture.active = active; RenderTexture.ReleaseTemporary(temporary); }
    }
    private static void Menu(bool __runOriginal)
    {
        if (completed || !__runOriginal || Event.current?.type != EventType.Repaint || !PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting)
            return;
        completed = true;
        if (scope == null)
        {
            Receipt("complete", "no-offset-calls");
            return;
        }
        Scope s = scope;
        scope = null;
        s.Watch.Stop();
        s.Values.Flush();
        foreach (Texture2D texture in s.Textures)
            UnityEngine.Object.Destroy(texture);
        s.Textures.Clear();
        using var sha = SHA256.Create();
        string digest = BitConverter.ToString(sha.ComputeHash(s.Data.ToArray())).Replace("-", "");
        Receipt("complete", "original-offsets", "\"calls\":" + s.Calls + ",\"hits\":" + s.Hits + ",\"fallbacks\":" + s.Fallbacks
            + ",\"errors\":" + s.Errors + ",\"verified\":" + s.Verified + ",\"mismatches\":" + s.Mismatches + ",\"sourcePixels\":" + s.Pixels
            + ",\"graphicsBackend\":\"" + SystemInfo.graphicsDeviceType + "\",\"offsetSha256\":\"" + digest + "\",\"retainedTextures\":" + s.Textures.Count
            + ",\"offsetMs\":" + (s.Ticks * 1000d / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture)
            + ",\"scopeThroughMenuMs\":" + s.Watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
            + ",\"exception\":" + (s.Errors == 0 ? "false" : "true"));
        s.Values.Dispose();
        s.Data.Dispose();
    }
    private sealed class Scope
    {
        internal readonly Stopwatch Watch = Stopwatch.StartNew();
        internal readonly List<Texture2D> Textures = new();
        internal readonly MemoryStream Data = new();
        internal readonly BinaryWriter Values;
        internal long Calls, Hits, Fallbacks, Errors, Verified, Mismatches, Pixels, Ticks;
        internal Scope()
        {
            Values = new BinaryWriter(Data);
        }
    }
    private static void Receipt(string kind, string reason, string? fields = null)
        => JsonLineLog.WriteReceipt(evidencePath, kind, reason, fields);
}
