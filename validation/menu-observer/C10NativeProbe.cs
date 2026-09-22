// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace FixtureMenuObserver;

// Private functional-only experiment. Success is device creation, not public readiness.
internal static class C10NativeProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Result
    {
        public uint DeviceFlags, FeatureLevel, Width, Height, Mips, Format;
        public uint MainThread, WorkerThread, Created, DescriptorMatched;
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Bootstrap(IntPtr resource, out IntPtr device, ref Result result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateDds(IntPtr device, byte[] bytes, uint length, ref Result result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReleaseDevice(IntPtr device);
    private static T Export<T>(IntPtr module, string name) where T : Delegate
    {
        IntPtr address = GetProcAddress(module, name);
        if (address == IntPtr.Zero) throw new InvalidOperationException("Missing export: " + name);
        return (T)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
    }
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    internal static void Menu(string directory, string package)
    {
        string library = Path.Combine(package, "Tools", "win-x64", "C10NativeProbe.dll");
        if (!File.Exists(library)) return;
        File.WriteAllText(Path.Combine(directory, "c10-native-probe-start.json"),
            "{\"package\":" + Quote(package) + ",\"assemblyLocation\":" + Quote(typeof(C10NativeProbe).Assembly.Location) + "}\n");
        string phase = "renderer", failure = "", payloadHash = "";
        int bootstrapHr = int.MinValue, createHr = int.MinValue;
        IntPtr module = IntPtr.Zero, device = IntPtr.Zero;
        Texture2D? privateProbe = null;
        ReleaseDevice? release = null;
        Result result = default;
        try
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                throw new InvalidOperationException("D3D11 required");
            phase = "load-library";
            // Absolute package path; dependency lookup limited to this directory and System32.
            module = LoadLibraryEx(library, IntPtr.Zero, 0x00000100 | 0x00000800);
            if (module == IntPtr.Zero) throw new InvalidOperationException("LoadLibraryEx: " + Marshal.GetLastWin32Error());
            var bootstrap = Export<Bootstrap>(module, "Bootstrap");
            var create = Export<CreateDds>(module, "CreateAuthoredDds");
            release = Export<ReleaseDevice>(module, "ReleaseDevice");
            phase = "bootstrap";
            privateProbe = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            privateProbe.SetPixels32(new Color32[16]);
            privateProbe.Apply(false, false);
            bootstrapHr = bootstrap(privateProbe.GetNativeTexturePtr(), out device, ref result);
            if (bootstrapHr < 0) Marshal.ThrowExceptionForHR(bootstrapHr);
            if ((result.DeviceFlags & 1) != 0) throw new InvalidOperationException("Single-threaded device; worker calls refused");
            phase = "read-authored-dds";
            byte[] payload = File.ReadAllBytes(Path.Combine(package, "Tools", "C10NativeProbe.dds"));
            using (var sha = SHA256.Create()) payloadHash = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
            phase = "worker-device-create";
            // Intentionally block main. Worker uses only the retained D3D11 device and managed bytes.
            // No timeout, main-thread pump, context access, external wrapper or holder publication.
            createHr = Task.Run(() => create(device, payload, (uint)payload.Length, ref result)).GetAwaiter().GetResult();
            if (createHr < 0) Marshal.ThrowExceptionForHR(createHr);
            phase = "native-resource-created-only";
        }
        catch (Exception ex) { failure = ex.ToString(); }
        finally
        {
            if (device != IntPtr.Zero) release?.Invoke(device);
            if (privateProbe != null) UnityEngine.Object.Destroy(privateProbe);
            if (module != IntPtr.Zero) FreeLibrary(module);
            File.WriteAllText(Path.Combine(directory, "c10-native-probe.json"),
                "{\"schema\":\"c10-native-device-probe.v1\",\"phase\":" + Quote(phase)
                + ",\"failure\":" + Quote(failure) + ",\"payloadSha256\":" + Quote(payloadHash)
                + ",\"bootstrapHr\":" + bootstrapHr + ",\"createHr\":" + createHr
                + ",\"deviceFlags\":" + result.DeviceFlags + ",\"featureLevel\":" + result.FeatureLevel
                + ",\"mainThread\":" + result.MainThread + ",\"workerThread\":" + result.WorkerThread
                + ",\"width\":" + result.Width + ",\"height\":" + result.Height
                + ",\"mips\":" + result.Mips + ",\"dxgiFormat\":" + result.Format
                + ",\"created\":" + result.Created + ",\"descriptorMatched\":" + result.DescriptorMatched
                + ",\"immediateContextUsed\":false,\"unityWrapperCompleted\":false,\"gpuReadbackVerified\":false,\"c10Accepted\":false}\n",
                new UTF8Encoding(false));
        }
    }
}
