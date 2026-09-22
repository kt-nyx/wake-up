// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Verse;

// Bounded observations in the private observer, never shipped with Wake-Up.
internal static class FeatureQualification
{
    private static Assembly? Product => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "WakeUp");
    private static Type? ProductType(string name) => Product?.GetType("WakeUp." + name);
    private static object? Field(string type, string name) => AccessTools.Field(ProductType(type), name)?.GetValue(null);
    private static object? Property(object value, string name) => AccessTools.Property(value.GetType(), name).GetValue(value, null);
    internal static bool Setting(string name)
    {
        Type? modType = ProductType("WakeUpMod");
        if (modType == null) return false;
        object? settings = AccessTools.Field(modType, "settings").GetValue(LoadedModManager.GetMod(modType));
        return settings != null && (bool)settings.GetType().GetField(name)!.GetValue(settings);
    }
    internal static void Menu(string directory)
    {
        File.WriteAllLines(Path.Combine(directory, "translation-samples.txt"), new[] {
            RimWorld.ThingDefOf.Steel.label, RimWorld.ThingDefOf.Steel.description,
            RimWorld.ThingDefOf.WoodLog.label, RimWorld.ThingDefOf.ComponentIndustrial.label,
            RimWorld.TerrainDefOf.Soil.label });
        // Native prepared hits are observed at startup. Do not manufacture a
        // prepared entry after the menu; the public controller owns preparation.
        if (Environment.GetCommandLineArgs().Contains("--fixture-c05-texture-probe"))
            CaptureMethods(directory, "NativeDdsCapture", (MethodBase[])Field("NativeDdsCapture", "Targets")!);
        if (Setting("TranslationApplication")) CaptureContract(directory, "TranslationRuntime");
        if (Setting("BackgroundLoading"))
        {
            File.WriteAllText(Path.Combine(directory, "background-status.txt"),
                Convert.ToString(AccessTools.Property(ProductType("BackgroundLoadingRuntime"), "Status").GetValue(null, null)) + "\n"
                + Convert.ToString(AccessTools.Property(ProductType("BackgroundLoadingRuntime"), "WorldStatus").GetValue(null, null)) + "\n"
                + Convert.ToString(AccessTools.Property(ProductType("BackgroundLoadingRuntime"), "ColonyStatus").GetValue(null, null)) + "\n"
                + Convert.ToString(AccessTools.Property(ProductType("BackgroundLoadingRuntime"), "MapStatus").GetValue(null, null)));
            if (C14BackgroundProbe.Required)
            {
                int index = 0;
                foreach (MethodBase method in (MethodBase[])Field("BackgroundLoadingMapContract", "Targets")!)
                    CaptureMethods(directory, "c14-map-" + index++, new[] { method });
            }
            foreach (MethodBase method in (MethodBase[])Field("WorldBackgroundLoadingContract", "Targets")!)
            {
                CaptureMethods(directory, "world-" + method.DeclaringType!.Name, new[] { method });
                var patches = Harmony.GetPatchInfo(method);
                if (patches != null)
                    File.AppendAllLines(Path.Combine(directory, "world-patches.txt"),
                        patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers)
                        .Select(p => method.DeclaringType!.FullName + ":" + method.Name + " owner=" + p.owner + " method=" + p.PatchMethod));
            }
        }
        if (Setting("AssetRouting")) CaptureContract(directory, "AssetRoutingRuntime");
        if (Setting("LoadingDisplay")) CaptureMethods(directory, "LoadingDisplayRuntime", (MethodBase[])Field("LoadingDisplayRuntime", "Targets")!);
        if (Setting("LoadingDisplay"))
        {
            float height = (float)(Field("LoadingDisplayRuntime", "panelHeight") ?? 0f);
            File.WriteAllText(Path.Combine(directory, "display-smoke.json"), "{\"panelLayoutExecuted\":" + (height > 0).ToString().ToLowerInvariant() + "}");
        }
        if (!Setting("AssetRouting")) return;
        try
        {
            if (!(bool)(Field("AssetRoutingRuntime", "enabled") ?? false)) throw new InvalidOperationException("Texture routing was selected but not admitted.");
            var mods = LoadedModManager.RunningModsListForReading;
            var sample = mods.AsEnumerable().Reverse().SelectMany(m => m.GetContentHolder<Texture2D>().contentList)
                .FirstOrDefault(p => p.Value != null);
            if (sample.Value == null)
            {
                File.WriteAllText(Path.Combine(directory, "routing-smoke.json"), "{\"covered\":false,\"reason\":\"no-physical-texture-holder\"}");
                return;
            }
            Texture2D expected = mods.AsEnumerable().Reverse().Select(m => m.GetContentHolder<Texture2D>().Get(sample.Key)).First(t => t != null);
            object cache = Field("AssetRoutingRuntime", "Textures")!;
            long before = (long)Property(cache, "Hits")!;
            Texture2D first = ContentFinder<Texture2D>.Get(sample.Key);
            Texture2D second = ContentFinder<Texture2D>.Get(sample.Key);
            long hits = (long)Property(cache, "Hits")! - before;
            if (!ReferenceEquals(first, expected) || !ReferenceEquals(second, expected) || hits < 1)
                throw new InvalidOperationException("Repeated native ContentFinder call did not reuse the winning holder object.");
            File.WriteAllText(Path.Combine(directory, "routing-smoke.json"), "{\"passed\":true,\"covered\":true,\"sameNativeWinner\":true,\"hitDelta\":" + hits + "}");
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "routing-smoke.json"), "{\"passed\":false}");
            File.WriteAllText(Path.Combine(directory, "routing-smoke-error.txt"), e.ToString());
        }
    }
    private static object? Call(string type, string method, params object[] args) => AccessTools.Method(ProductType(type), method).Invoke(null, args);
    private static void Preparation(string directory)
    {
        // One native-preserving item through the real capture/store/restore
        // components. This does not drive the preparation window or claim a
        // subsequent startup consumed the prepared store.
        object? store = null;
        Texture2D? restored = null;
        try
        {
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                var selected = ((IEnumerable<KeyValuePair<string, FileInfo>>)Call("PngRuntime", "SelectedFiles", mod)!).ToArray();
                var sample = selected.FirstOrDefault(p => p.Value.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) && p.Value.Length < 1024 * 1024);
                if (sample.Value == null) continue;
                string selection = (string)Call("PreparedTextureRuntime", "SelectionIdentity", mod, selected)!;
                byte[] source = (byte[])Call("PreparedTextureRuntime", "ReadSource", sample.Value)!;
                string platform = (string)Call("PngRuntime", "PreparationPlatform")!;
                string identity = (string)Call("PreparedTextureRuntime", "Identity", selection, sample.Key, sample.Value.FullName, source, 0, platform, "")!;
                Type storeType = ProductType("PreparedTextureStore")!;
                store = Activator.CreateInstance(storeType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object?[] { GenFilePaths.SaveDataFolderPath, null, 512L * 1024 * 1024, false }, null)!;
                Func<int, bool> admit = bytes => (bool)AccessTools.Method(storeType, "CanPublish").Invoke(store, new object[] { identity, bytes });
                object entry = Call("PngRuntime", "CapturePrepared", sample.Value, source, admit) ?? throw new InvalidOperationException("Native preparation did not capture an entry.");
                if (!(bool)Call("PreparedTextureRuntime", "Revalidate", mod, sample.Key, sample.Value.FullName, selection, source, 0, null!)!)
                    throw new InvalidOperationException("Preparation input changed.");
                if (!(bool)AccessTools.Method(storeType, "Publish").Invoke(store, new[] { identity, entry })) throw new InvalidOperationException("Prepared publication failed.");
                object loaded = AccessTools.Method(storeType, "Read").Invoke(store, new object[] { identity }) ?? throw new InvalidOperationException("Prepared read failed.");
                restored = (Texture2D)Call("PngRuntime", "Restore", loaded)!;
                // The selected disk key includes Textures; native holder keys
                // are slash-normalized paths relative to that content folder.
                string selectedPath = sample.Key.Replace('\\', '/');
                if (!selectedPath.StartsWith("Textures/", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected texture content root.");
                string nativePath = Path.ChangeExtension(selectedPath.Substring("Textures/".Length), null);
                Texture2D native = mod.GetContentHolder<Texture2D>().Get(nativePath);
                if (native == null) throw new InvalidOperationException("Native holder sample unavailable: " + nativePath
                    + "; same-basename keys=" + string.Join(",", mod.GetContentHolder<Texture2D>().contentList.Keys.Where(k => k.EndsWith(Path.GetFileNameWithoutExtension(sample.Value.Name), StringComparison.Ordinal))));
                long before = Convert.ToInt64(Field("PngRuntime", "mismatches"));
                Call("PngRuntime", "Compare", native, restored);
                if (Convert.ToInt64(Field("PngRuntime", "mismatches")) != before) throw new InvalidOperationException("Prepared output differs from native texture.");
                AccessTools.Method(storeType, "Complete").Invoke(store, null);
                File.WriteAllText(Path.Combine(directory, "preparation-smoke.json"), "{\"passed\":true,\"nativePreset\":true,\"items\":1,\"publishReadRestoreMatched\":true}");
                return;
            }
            File.WriteAllText(Path.Combine(directory, "preparation-smoke.json"), "{\"covered\":false,\"reason\":\"no-small-jpeg\"}");
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "preparation-smoke.json"), "{\"passed\":false}");
            File.WriteAllText(Path.Combine(directory, "preparation-smoke-error.txt"), e.ToString());
        }
        finally { if (restored != null) UnityEngine.Object.DestroyImmediate(restored); (store as IDisposable)?.Dispose(); }
    }
    private static void CaptureContract(string directory, string type)
    {
        MethodBase[] methods = (MethodBase[])AccessTools.Method(ProductType(type), "ContractMethods").Invoke(null, null);
        CaptureMethods(directory, type, methods);
    }
    private static void CaptureMethods(string directory, string type, MethodBase[] methods)
    {
        MethodInfo hash = ProductType("SemanticMethodIdentity")!.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(m => m.Name == "TryHash" && m.GetParameters().Length == 4);
        foreach (MethodBase method in methods)
        {
            object[] args = { method, "", "", "" };
            hash.Invoke(null, args);
            string name = string.Concat((type + "-" + method.Name).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            File.WriteAllText(Path.Combine(directory, name + ".txt"), args[1] + "\n" + args[2] + "\n" + args[3]);
            if ((string)args[1] == "") DiagnoseTokens(directory, type, method);
        }
    }
    private static void DiagnoseTokens(string directory, string type, MethodBase method)
    {
        var output = new StringBuilder();
        try
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
                .Select(f => (OpCode)f.GetValue(null)).ToDictionary(c => unchecked((ushort)c.Value));
            byte[] il = method.GetMethodBody()!.GetILAsByteArray();
            for (int index = 0; index < il.Length;)
            {
                int offset = index;
                ushort value = il[index++];
                if (value == 0xfe) value = (ushort)(0xfe00 | il[index++]);
                OpCode code = codes[value];
                int size = code.OperandType == OperandType.InlineNone ? 0
                    : code.OperandType == OperandType.InlineI8 || code.OperandType == OperandType.InlineR ? 8
                    : code.OperandType == OperandType.InlineVar ? 2
                    : code.OperandType == OperandType.ShortInlineBrTarget || code.OperandType == OperandType.ShortInlineI || code.OperandType == OperandType.ShortInlineVar ? 1
                    : code.OperandType == OperandType.InlineSwitch ? 4 + 4 * BitConverter.ToInt32(il, index) : 4;
                if (code.OperandType == OperandType.InlineMethod || code.OperandType == OperandType.InlineField || code.OperandType == OperandType.InlineType || code.OperandType == OperandType.InlineTok)
                {
                    int token = BitConverter.ToInt32(il, index);
                    output.Append(offset).Append(' ').Append(code.Name).Append(' ').Append(token.ToString("X8")).Append(' ');
                    try
                    {
                        var member = method.Module.ResolveMember(token, method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                        output.AppendLine(member.ToString());
                        AccessTools.Method(ProductType("SemanticMethodIdentity"), "Member").Invoke(null, new object[] { member });
                    }
                    catch (Exception e) { output.AppendLine(e.ToString()); }
                }
                index += size;
            }
        }
        catch (Exception e) { output.AppendLine(e.ToString()); }
        File.WriteAllText(Path.Combine(directory, type + "-" + method.Name + "-tokens.txt"), output.ToString());
    }
    internal static void Background(string directory, string phase, bool expectedActive, bool? expectedPreference = null)
    {
        bool selected = Setting("BackgroundLoading");
        object? session = Product == null ? null : Field("BackgroundLoadingRuntime", "Session");
        bool installed = Product != null && (bool)(Field("BackgroundLoadingRuntime", "installed") ?? false);
        bool active = session != null && (bool)Property(session, "Active")!;
        int completionFrame = session == null ? -1 : (int)AccessTools.Field(session.GetType(), "completionFrame").GetValue(session);
        bool engine = Application.runInBackground;
        bool preference = Prefs.RunInBackground;
        bool expectedEngine = (selected && expectedActive) || preference;
        bool passed = (!selected || installed) && active == (selected && expectedActive) && engine == expectedEngine
            && preference == (expectedPreference ?? !selected);
        File.AppendAllText(Path.Combine(directory, "background-smoke.jsonl"), "{\"phase\":\"" + phase + "\",\"selected\":" + selected.ToString().ToLowerInvariant()
            + ",\"installed\":" + installed.ToString().ToLowerInvariant()
            + ",\"active\":" + active.ToString().ToLowerInvariant() + ",\"engineBackground\":" + engine.ToString().ToLowerInvariant()
            + ",\"preference\":" + preference.ToString().ToLowerInvariant()
            + ",\"focused\":" + Application.isFocused.ToString().ToLowerInvariant()
            + ",\"frame\":" + Time.frameCount + ",\"completionFrame\":" + completionFrame
            + ",\"expectedEngineBackground\":" + expectedEngine.ToString().ToLowerInvariant()
            + ",\"passed\":" + passed.ToString().ToLowerInvariant() + "}\n");
        if (!passed)
            throw new InvalidOperationException("Background save-load mismatch at " + phase + ": selected=" + selected
                + ", installed=" + installed + ", active=" + active + " (expected " + (selected && expectedActive)
                + "), engine=" + engine + " (expected " + expectedEngine + "), preference=" + preference
                + ". The actual preference must match this probe's expected value.");
    }
}
