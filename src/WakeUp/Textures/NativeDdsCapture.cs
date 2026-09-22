// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld.IO;
using UnityEngine;
using Verse;

namespace WakeUp;

// Observe the completed native DDS upload. The native parser, BGR swizzle,
// format selection and stored/generated mip decision continue to execute.
// No Span<T> or unsafe signature crosses this adapter's managed boundary.
internal static class NativeDdsCapture
{
    internal const int MaximumCaptureBytes = NativeTextureData.MaximumPixels;
    internal const int MaximumSourceBytes = 96 * 1024 * 1024;
    internal static readonly MethodInfo Load = AccessTools.Method(typeof(ModDdsLoader), "TryLoadDds");
    internal static readonly MethodInfo Create = AccessTools.Method(typeof(ModDdsLoader), "CreateTexture");
    internal static readonly MethodInfo OpenMap = AccessTools.Method(typeof(MemoryMappedFileSpanWrapper), "OpenExistingMmf",
        new[] { typeof(FileInfo), typeof(MemoryMappedFileAccess), typeof(bool) });
    internal static readonly MethodInfo[] Targets = new[] { Load, Create, OpenMap,
        AccessTools.Method(typeof(DdsPixelFormat), "ToTextureFormat") }
        .Concat(typeof(DdsPixelFormat).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.GetGetMethod()).Where(m => m != null).Cast<MethodInfo>()).ToArray();
    // Filled from the original GOG method-body receipt before activation.
    // An absent identity always refuses capture and restoration.
    internal static readonly Dictionary<string, string> Bodies = new()
    {
        ["CreateTexture"] = "CAB8F93C7EB8C5828D41D4D824687A4E06AE2A65694EA6C70B4CAA0DEA3E76A4",
        ["get_IsArgb4444"] = "8F9CAE883C73405D5A9BA79464CD3EDFC37E172A97A7845081E94E5391B197F2",
        ["get_IsBc7"] = "0D0D77FC40BCF757D114227CD804B13CDC457DFFD671159ADAEDBC7C916DF971",
        ["get_IsBgr888"] = "4AF8423A7914F7CFDA53D8DA1766F53C419B341CF840C6710E1AA4BF4B7160B8",
        ["get_IsCompressed"] = "B9841504514C6ADFCD1D245F3B991BBF8756795042826885D75108EA23970F2A",
        ["get_IsDxt1"] = "01CEDD1CEE2504B410D6EF4DE78CCB094BC9B38F445DA4CBA5039DA82F6C8D01",
        ["get_IsDxt5"] = "566ADE6FD54368B57E5951963023E681F424065766871EF5DE8CAB26B46E6F06",
        ["get_IsRgb565"] = "D3ABD38125ADDA585557C52D33F3B3A0947DFC155738DF5D2E3EA824BDBA1BF3",
        ["get_IsRgb888"] = "A9ADE5F07BD68BB187AFCA7A4BC2BED950828770FD2F3D2AF807CF553F410278",
        ["get_IsRgba4444"] = "21063EC53925E94AE6077D142A85C3DD5449A3ADFA843A1877876794EE1A27BE",
        ["get_IsUnsupportedCompressedFormat"] = "F354B8D0182BC6891505725C48935AF06702AC9DE495DE7E8C1946AC97CAC19D",
        ["OpenExistingMmf"] = "521CC42326E76A91FD5B04D6A5C14F6456EBF4E50AEA2CEF8275C883680E0F10",
        ["ToTextureFormat"] = "46882E0B4A7A41A5238F21C4B1B30F3D73213767E92D7937E5816D0E33C668E4",
        ["TryLoadDds"] = "28059705E24DF45164B4D37D05621858444132F6B306FE871D7DB68897517480",
    };
    private static readonly List<PublishedPatchGuard> Guards = new();
    private static bool installed;
    internal static string Refusal { get; private set; } = "not-initialized";
    [ThreadStatic] private static CaptureContext? current;

    private sealed class CaptureContext
    {
        internal readonly Func<int, bool> Admit;
        internal readonly FileStream SourceLease;
        internal byte[]? Pixels;
        internal Texture2D? Texture;
        internal bool Failed;
        internal CaptureContext(Func<int, bool> admit, FileStream sourceLease)
        { Admit = admit; SourceLease = sourceLease; }
    }

    internal static bool Compatible => installed && Guards.All(g => g.AllowsOriginalContract());

    internal static bool Initialize(Harmony harmony)
    {
        if (installed) return Compatible;
        Guards.Clear();
        try
        {
            foreach (var method in Targets)
            {
                if (!Bodies.TryGetValue(method.Name, out string expected)
                    || !SemanticMethodIdentity.TryHash(method, out string actual, out _)
                    || actual != expected)
                    throw new InvalidOperationException("dds-body-" + method.Name);
                if (!PublishedPatchGuard.TryCreate(method, harmony.Id, out var guard, true)
                    || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("dds-hook-" + method.Name);
                Guards.Add(guard!);
            }
            harmony.Patch(Create, transpiler: new HarmonyMethod(typeof(NativeDdsCapture), nameof(RewriteApply)));
            harmony.Patch(OpenMap, transpiler: new HarmonyMethod(typeof(NativeDdsCapture), nameof(RewriteMap)));
            installed = true;
            Refusal = "none";
            return true;
        }
        catch (Exception e)
        {
            // This owner also has other features: undo only this adapter's hook.
            harmony.Unpatch(Create, AccessTools.Method(typeof(NativeDdsCapture), nameof(RewriteApply)));
            harmony.Unpatch(OpenMap, AccessTools.Method(typeof(NativeDdsCapture), nameof(RewriteMap)));
            Guards.Clear();
            Refusal = e.Message;
            return false;
        }
    }

    // Caller holds its read-only, FileShare.Read source lease through this call
    // and keys the source bytes read from that same lease. The scoped map hook
    // maps this very handle; it never reopens a possibly changed pathname.
    internal static Texture2D Capture(VirtualFile file, Action<Texture2D, byte[]> completed,
        Func<int, bool>? admit = null, FileStream? sourceLease = null)
    {
        if (!Compatible || !UnityData.IsInMainThread || current != null || sourceLease == null)
            return ModDdsLoader.TryLoadDds(file);
        if (!sourceLease.CanRead || !sourceLease.CanSeek || sourceLease.CanWrite
            || !string.Equals(Path.GetFullPath(file.FullPath), Path.GetFullPath(sourceLease.Name), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("dds-source-lease");
        var context = new CaptureContext(admit ?? (_ => true), sourceLease);
        current = context;
        try
        {
            Texture2D result = ModDdsLoader.TryLoadDds(file);
            if (!context.Failed && ReferenceEquals(result, context.Texture) && context.Pixels != null && Compatible)
            {
                // Optional persistence cannot turn a successfully loaded native
                // texture into BadTex or leak its ownership on callback failure.
                try { completed(result, context.Pixels); }
                catch { Refusal = "dds-publication-failed"; }
            }
            return result;
        }
        finally { current = null; }
    }

    internal static IEnumerable<CodeInstruction> RewriteApply(IEnumerable<CodeInstruction> instructions)
    {
        var original = instructions.Select(i => new CodeInstruction(i)).ToArray();
        MethodInfo apply = AccessTools.Method(typeof(Texture2D), "Apply", new[] { typeof(bool), typeof(bool) });
        MethodInfo replacement = AccessTools.Method(typeof(NativeDdsCapture), nameof(FinalizeTexture));
        int replaced = 0;
        foreach (var instruction in original)
        {
            if (!instruction.Calls(apply)) continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = replacement;
            replaced++;
        }
        if (replaced != 1) throw new InvalidOperationException("dds-apply-callsites-" + replaced);
        return original;
    }

    internal static IEnumerable<CodeInstruction> RewriteMap(IEnumerable<CodeInstruction> instructions)
    {
        var original = instructions.Select(i => new CodeInstruction(i)).ToArray();
        MethodInfo map = AccessTools.Method(typeof(MemoryMappedFile), "CreateFromFile", new[] {
            typeof(string), typeof(FileMode), typeof(string), typeof(long), typeof(MemoryMappedFileAccess) });
        int replaced = 0;
        foreach (var instruction in original)
        {
            if (!instruction.Calls(map)) continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(NativeDdsCapture), nameof(CreateSourceMap));
            replaced++;
        }
        if (replaced != 1) throw new InvalidOperationException("dds-map-callsites-" + replaced);
        return original;
    }

    private static MemoryMappedFile CreateSourceMap(string path, FileMode mode, string? mapName,
        long capacity, MemoryMappedFileAccess access)
    {
        CaptureContext? context = current;
        if (context == null || !string.Equals(Path.GetFullPath(path), Path.GetFullPath(context.SourceLease.Name), StringComparison.OrdinalIgnoreCase))
            return MemoryMappedFile.CreateFromFile(path, mode, mapName, capacity, access);
        if (mode != FileMode.Open || access != MemoryMappedFileAccess.Read || mapName != null
            || capacity != context.SourceLease.Length)
            throw new InvalidOperationException("dds-source-map-contract");
        return MapLease(context.SourceLease, capacity);
    }

    internal static MemoryMappedFile MapLease(FileStream lease, long capacity)
        => MemoryMappedFile.CreateFromFile(lease, null, capacity, MemoryMappedFileAccess.Read,
            null, HandleInheritability.None, leaveOpen: true);

    private static void FinalizeTexture(Texture2D texture, bool updateMips, bool unreadable)
    {
        CaptureContext? context = current;
        if (context == null || context.Failed || !Compatible || !texture.isReadable)
        {
            texture.Apply(updateMips, unreadable);
            return;
        }
        int size = ExpectedBytes(texture.width, texture.height, (int)texture.format, texture.mipmapCount);
        bool admitted;
        try { admitted = size > 0 && context.Admit(size); }
        catch { admitted = false; }
        if (!admitted)
        {
            context.Failed = true;
            texture.Apply(updateMips, unreadable);
            return;
        }
        context.Texture = texture;
        context.Pixels = ApplyAndCapture(texture.Apply, texture.GetRawTextureData,
            updateMips, unreadable, size);
        context.Failed = context.Pixels == null;
    }

    // Only copying failure is optional. A native Apply failure still propagates
    // through the game's existing LoadItem exception handling.
    internal static byte[]? ApplyAndCapture(Action<bool, bool> apply, Func<byte[]> read,
        bool updateMips, bool unreadable, int expectedBytes)
    {
        if (expectedBytes < 1 || expectedBytes > MaximumCaptureBytes)
        {
            apply(updateMips, unreadable);
            return null;
        }
        apply(updateMips, false);
        try
        {
            try
            {
                byte[] bytes = read();
                return bytes.Length == expectedBytes ? bytes : null;
            }
            catch { return null; }
        }
        finally { if (unreadable) apply(false, true); }
    }

    internal static int ExpectedBytes(int width, int height, int format, int mips)
        => NativeTextureData.ExpectedBytes(width, height, format, mips);

    // This is native-preservation scan admission only. Portable DDS exports
    // continue to use PreparedDds and its independent quality contract.
    internal static PreparationImageHeader ReadHeader(Stream input)
    {
        if (!input.CanSeek || input.Length < 128 || input.Length > MaximumSourceBytes)
            throw new InvalidDataException("native-dds-source-size");
        input.Position = 0;
        using var reader = new BinaryReader(input, System.Text.Encoding.ASCII, true);
        byte[] h = reader.ReadBytes((int)Math.Min(148, input.Length));
        if (U32(h, 0) != 0x20534444 || U32(h, 4) != 124 || U32(h, 76) != 32)
            throw new InvalidDataException("native-dds-header");
        if (U32(h, 112) != 0 || U32(h, 24) > 1)
            throw new InvalidDataException("native-dds-dimension");
        int width = checked((int)U32(h, 16)), height = checked((int)U32(h, 12));
        uint flags = U32(h, 80), fourcc = U32(h, 84), bitCount = U32(h, 88);
        uint red = U32(h, 92), green = U32(h, 96), blue = U32(h, 100), alpha = U32(h, 104);
        int format, offset = 128;
        if ((flags & 4) != 0 && fourcc != 0)
        {
            if (fourcc == 0x31545844) format = 10;
            else if (fourcc == 0x35545844) format = 12;
            else if (fourcc == 0x30315844)
            {
                // Native treats DX10 as BC7. Do not describe other DXGI payloads
                // that native misinterprets as supported representations.
                if (h.Length < 148 || U32(h, 128) != 98 && U32(h, 128) != 99
                    || U32(h, 132) != 3 || U32(h, 136) != 0 || U32(h, 140) != 1)
                    throw new InvalidDataException("native-dds-dx10-representation");
                format = 25; offset = 148;
            }
            else throw new InvalidDataException("native-dds-compressed-format");
        }
        else if ((flags & 64) != 0)
        {
            bool hasAlpha = (flags & 1) != 0;
            bool rgb = red == 255 && green == 65280 && blue == 16711680;
            bool bgr = red == 16711680 && green == 65280 && blue == 255;
            if (rgb || bgr)
            {
                format = hasAlpha ? 4 : 3;
                if (bitCount != (hasAlpha ? 32u : 24u)) throw new InvalidDataException("native-dds-rgb-stride");
            }
            else if (red == 63488 && green == 2016 && blue == 31 && bitCount == 16) format = 7;
            else if (red == 61440 && green == 3840 && blue == 240 && alpha == 15 && hasAlpha && bitCount == 16)
                format = 2; // Deliberately matches native's ARGB4444 return.
            else throw new InvalidDataException("native-dds-rgb-format");
        }
        else if ((flags & 0x20002) != 0 && bitCount == 8) format = 1;
        else throw new InvalidDataException("native-dds-pixel-format");
        int mips = (U32(h, 8) & 0x20000) != 0 && U32(h, 28) > 1 ? checked((int)U32(h, 28)) : 1;
        int bytes = ExpectedBytes(width, height, format, mips);
        if (bytes < 1 || input.Length - offset != bytes) throw new InvalidDataException("native-dds-payload-layout");
        return new PreparationImageHeader { Width = width, Height = height, SourceBytes = input.Length,
            IsDds = true, OrdinaryMetadata = false };
    }

    private static uint U32(byte[] bytes, int offset) => (uint)(bytes[offset] | bytes[offset + 1] << 8
        | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
}
