// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace FixtureMenuObserver;

// Explicit native control, invoked only after the natural playback proof. No
// cache, playback, patches, or provider selection changes are made here.
internal static class C10Mp3ProviderProbe
{
    private const string NAudioSha256 = "0df62f4e48776870ee22450d1735f00a69493d3c2c6dfb91cc4feab27150d6f1";

    internal static void Capture(string directory, string path)
    {
        var report = new Dictionary<string, object>
        {
            ["schema"] = "c10-mp3-provider-prerequisite.v1", ["passed"] = false,
            ["failure"] = "", ["selectedBinaryIdentified"] = false,
            ["cachedOrDeferredMp3Tested"] = false,
            ["control"] = "Explicit native Mp3FileReader(Stream); no sample reads or playback.",
            ["candidateLimit"] = "Registered drivers and loaded .acm modules are candidates only; no supported selected-descriptor-to-module linkage was established.",
            ["managedThreadId"] = Thread.CurrentThread.ManagedThreadId,
            ["pointerSize"] = IntPtr.Size, ["osVersion"] = Environment.OSVersion.VersionString
        };
        IDisposable? reader = null;
        FileStream? input = null;
        try
        {
            path = Path.GetFullPath(path);
            if (!string.Equals(Path.GetExtension(path), ".mp3", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The supplied fixture control must be an MP3 file.");
            report["source"] = FileIdentity(path);
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "NAudio");
            var identity = FileIdentity(assembly.Location);
            identity["fullName"] = assembly.FullName!;
            identity["mvid"] = assembly.ManifestModule.ModuleVersionId.ToString();
            report["managedAssembly"] = identity;
            if (!Equals(identity["sha256"], NAudioSha256))
                throw new InvalidOperationException("The native MP3 control requires the reviewed fixture NAudio binary.");

            report["loadedAcmCandidatesBefore"] = LoadedAcmCandidates();
            Type type = assembly.GetType("NAudio.Wave.Mp3FileReader", true)!;
            input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            report["inputPositionBefore"] = input.Position;
            reader = (IDisposable)type.GetConstructor(new[] { typeof(Stream) })!.Invoke(new object[] { input });
            report["nativeConstructorCompleted"] = true;
            report["inputPositionAfterConstruction"] = input.Position;
            report["length"] = Property(reader, "Length");
            report["position"] = Property(reader, "Position");
            report["sourceFormat"] = Format(Property(reader, "Mp3WaveFormat"));
            report["outputFormat"] = Format(Property(reader, "WaveFormat"));
            object decompressor = Field(reader, "decompressor");
            if (decompressor.GetType() != assembly.GetType("NAudio.Wave.AcmMp3FrameDecompressor", true))
                throw new InvalidOperationException("Native MP3 returned an unexpected frame decompressor.");
            object conversion = Field(decompressor, "conversionStream");
            if (conversion.GetType() != assembly.GetType("NAudio.Wave.Compression.AcmStream", true))
                throw new InvalidOperationException("Native MP3 returned an unexpected ACM stream.");
            IntPtr handle = (IntPtr)Field(conversion, "streamHandle");
            report["streamHandleNonzero"] = handle != IntPtr.Zero;
            report["explicitDriverHandleNonzero"] = (IntPtr)Field(conversion, "driverHandle") != IntPtr.Zero;
            if (handle == IntPtr.Zero) throw new InvalidOperationException("Native ACM stream handle is zero.");
            uint result = acmDriverID(handle, out IntPtr driverId, 0);
            report["acmDriverIdResult"] = result;
            if (result != 0) throw new InvalidOperationException("acmDriverID returned " + result);
            report["driverIdProcessLocal"] = driverId.ToInt64().ToString("x", CultureInfo.InvariantCulture);
            var details = new AcmDriverDetailsW { cbStruct = (uint)Marshal.SizeOf(typeof(AcmDriverDetailsW)) };
            result = acmDriverDetailsW(driverId, ref details, 0);
            report["acmDriverDetailsResult"] = result;
            if (result != 0) throw new InvalidOperationException("acmDriverDetailsW returned " + result);
            report["selectedDescriptor"] = new Dictionary<string, object>
            {
                ["structureBytes"] = details.cbStruct, ["type"] = details.fccType, ["compression"] = details.fccComp,
                ["manufacturerId"] = details.wMid, ["productId"] = details.wPid,
                ["acmVersionRaw"] = details.vdwACM, ["driverVersionRaw"] = details.vdwDriver,
                ["supportFlags"] = details.fdwSupport, ["formatTags"] = details.cFormatTags, ["filterTags"] = details.cFilterTags,
                ["shortName"] = details.szShortName ?? "", ["longName"] = details.szLongName ?? "",
                ["copyright"] = details.szCopyright ?? "", ["licensing"] = details.szLicensing ?? "", ["features"] = details.szFeatures ?? ""
            };
            report["loadedAcmCandidatesAfter"] = LoadedAcmCandidates();
            report["registeredCandidates"] = RegisteredCandidates();
            report["passed"] = true;
        }
        catch (Exception error) { Fail(report, error); }
        finally
        {
            try
            {
                if (reader != null) { reader.Dispose(); report["nativeReaderDisposed"] = true; }
                if (input != null) report["borrowedInputStillOpenAfterReaderDispose"] = input.CanRead;
            }
            catch (Exception error) { Fail(report, error); }
            finally
            {
                try { if (input != null) { input.Dispose(); report["ownedInputDisposed"] = true; } }
                catch (Exception error) { Fail(report, error); }
            }
        }
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "c10-mp3-provider-probe.json"), Json(report) + "\n", new UTF8Encoding(false));
        }
        catch (Exception error) { UnityEngine.Debug.LogWarning("[FixtureMenuObserver] MP3 provider receipt could not be written: " + error); }
    }

    private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    private static object Property(object value, string name) => value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(value, null)!;
    private static Dictionary<string, object> Format(object value)
    {
        var result = new Dictionary<string, object> { ["type"] = value.GetType().FullName! };
        foreach (string name in new[] { "Encoding", "SampleRate", "Channels", "BitsPerSample", "BlockAlign", "AverageBytesPerSecond", "ExtraSize" })
            result[name] = Convert.ToInt64(Property(value, name), CultureInfo.InvariantCulture);
        return result;
    }

    private static Dictionary<string, object> FileIdentity(string path)
    {
        var result = new Dictionary<string, object> { ["path"] = path };
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var sha = SHA256.Create())
        {
            result["bytes"] = file.Length;
            result["sha256"] = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        return result;
    }

    private static object[] LoadedAcmCandidates()
    {
        var rows = new List<object>();
        try
        {
            using (Process process = Process.GetCurrentProcess())
                foreach (ProcessModule module in process.Modules)
                    if (string.Equals(Path.GetExtension(module.FileName), ".acm", StringComparison.OrdinalIgnoreCase))
                    {
                        try { rows.Add(FileIdentity(module.FileName)); }
                        catch (Exception error) { rows.Add(new Dictionary<string, object> { ["path"] = module.FileName, ["error"] = error.Message }); }
                    }
        }
        catch (Exception error) { rows.Add(new Dictionary<string, object> { ["inventoryError"] = error.Message }); }
        return rows.ToArray();
    }

    private static object[] RegisteredCandidates()
    {
        var rows = new List<object>();
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (RegistryKey? key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Drivers32", false))
                {
                    if (key == null) continue;
                    foreach (string name in key.GetValueNames().Where(n => n.StartsWith("msacm.", StringComparison.OrdinalIgnoreCase)))
                    {
                        string value = Convert.ToString(key.GetValue(name), CultureInfo.InvariantCulture) ?? "";
                        var row = new Dictionary<string, object> { ["view"] = view.ToString(), ["name"] = name, ["registeredValue"] = value };
                        string expanded = Environment.ExpandEnvironmentVariables(value);
                        string candidate = Path.IsPathRooted(expanded) ? expanded : Path.Combine(
                            Environment.GetFolderPath(view == RegistryView.Registry32 ? Environment.SpecialFolder.SystemX86 : Environment.SpecialFolder.System), expanded);
                        row["candidatePath"] = candidate;
                        try { if (File.Exists(candidate)) row["candidateFile"] = FileIdentity(candidate); }
                        catch (Exception error) { row["candidateError"] = error.Message; }
                        rows.Add(row);
                    }
                }
            }
            catch (Exception error) { rows.Add(new Dictionary<string, object> { ["view"] = view.ToString(), ["inventoryError"] = error.Message }); }
        }
        return rows.ToArray();
    }

    private static void Fail(Dictionary<string, object> report, Exception error)
    {
        report["passed"] = false;
        report["failure"] = (string)report["failure"] + (Equals(report["failure"], "") ? "" : "\n") + error;
    }

    [DllImport("msacm32.dll", ExactSpelling = true)]
    private static extern uint acmDriverID(IntPtr stream, out IntPtr driverId, uint flags);
    [DllImport("msacm32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint acmDriverDetailsW(IntPtr driverId, ref AcmDriverDetailsW details, uint flags);

    // msacm.h uses one-byte packing, including the pointer-sized icon field.
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    private struct AcmDriverDetailsW
    {
        internal uint cbStruct, fccType, fccComp;
        internal ushort wMid, wPid;
        internal uint vdwACM, vdwDriver, fdwSupport, cFormatTags, cFilterTags;
        internal IntPtr hicon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string? szShortName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string? szLongName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] internal string? szCopyright;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string? szLicensing;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] internal string? szFeatures;
    }

    private static string Json(object? value)
    {
        if (value == null) return "null";
        if (value is string text)
        {
            var result = new StringBuilder("\"");
            foreach (char c in text)
                if (c == '\\' || c == '"') result.Append('\\').Append(c);
                else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else result.Append(c);
            return result.Append('"').ToString();
        }
        if (value is bool flag) return flag ? "true" : "false";
        if (value is IDictionary<string, object> map) return "{" + string.Join(",", map.Select(p => Json(p.Key) + ":" + Json(p.Value))) + "}";
        if (value is IEnumerable items) return "[" + string.Join(",", items.Cast<object>().Select(Json)) + "]";
        return Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }
}
