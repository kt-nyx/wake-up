// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using RuntimeAudioClipLoader;
using HarmonyLib;
using System.Reflection.Emit;

namespace WakeUp;

// The game module may change independently of these decoder bodies. The two
// external codec assemblies are pinned separately; no whole-game version gate.
internal static class PreparedAudioContract
{
    internal const string NativeMethodsHash = "BEDB8F2A84C01DBA178DFC8682126399C1C6A60019605C470DBD1EC62CE8AA0A";
    internal const string NAudioHash = "0DF62F4E48776870EE22450D1735F00A69493D3C2C6DFB91CC4FEAB27150D6F1";
    internal const string NVorbisHash = "255DE9AC2A2A4DD71306B82784DBD067EDBED5810564CCAEB2EA2F1994378339";
    internal const string NAudioMvid = "fa168faf-488c-4ac3-8a7c-7c759315a688";
    internal const string NVorbisMvid = "6486cbdd-6db6-40cb-84f0-641a9b81a532";
    private const string ReaderName = "RuntimeAudioClipLoader.CustomAudioFileReader";
    private const string VorbisName = "NVorbis.NAudioSupport.VorbisWaveReader";
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // Called before enabling the feature, never while holding its admission gate.
    internal static string Validate()
    {
        Assembly game = typeof(Manager).Assembly;
        ValidateMethods(game);
        ValidateSourceFactory(game);
        Type vorbis = game.GetType(VorbisName, true)!;
        Assembly naudio = vorbis.BaseType!.Assembly;
        Assembly nvorbis = vorbis.GetField("_reader", Declared)!.FieldType.Assembly;
        ValidateDependency(naudio, "NAudio", NAudioMvid, NAudioHash);
        ValidateDependency(nvorbis, "NVorbis", NVorbisMvid, NVorbisHash);
        return "prepared-audio-v1:" + NativeMethodsHash + ":" + NAudioHash + ":" + NVorbisHash;
    }

    internal static bool Relevant(MethodBase method)
    {
        Type? type = method.DeclaringType;
        if (type == null) return false;
        string? assembly = type.Assembly.GetName().Name;
        if (assembly == "NAudio" || assembly == "NVorbis") return true;
        if (type.Assembly != typeof(Manager).Assembly) return false;
        for (Type? current = type; current != null; current = current.DeclaringType)
            if (current.FullName == ReaderName || current.FullName == VorbisName) return true;
        return false;
    }

    internal static MethodBase[] Methods(Assembly game) => new[] { ReaderName, VorbisName }
        .SelectMany(name => Types(game.GetType(name, true)!))
        .SelectMany(type => type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
        .OrderBy(SemanticMethodIdentity.Signature, StringComparer.Ordinal).ToArray();

    private static IEnumerable<Type> Types(Type type)
    {
        yield return type;
        foreach (Type nested in type.GetNestedTypes(Declared))
            foreach (Type descendant in Types(nested)) yield return descendant;
    }

    internal static void ValidateMethods(Assembly game)
    {
        if (MethodsHash(game) != NativeMethodsHash)
            throw new InvalidOperationException("prepared audio native decoder methods changed");
    }

    internal static void ValidateSourceFactory(Assembly game)
    {
        Type type = game.GetType("RimWorld.IO.FilesystemFile", true)!;
        MethodInfo method = type.GetMethod("CreateReadStream")!;
        var code = PatchProcessor.GetOriginalInstructions(method).Where(c => c.opcode != OpCodes.Nop).ToArray();
        if (code.Length != 4 || code[0].opcode != OpCodes.Ldarg_0 || code[1].opcode != OpCodes.Ldfld
            || code[1].operand is not FieldInfo field || field.DeclaringType != type || field.FieldType != typeof(FileInfo)
            || code[2].opcode != OpCodes.Callvirt || !Equals(code[2].operand, typeof(FileInfo).GetMethod(nameof(FileInfo.OpenRead)))
            || code[3].opcode != OpCodes.Ret)
            throw new InvalidOperationException("native filesystem stream factory changed");
    }

    internal static string MethodsHash(Assembly game)
    {
        var canonical = new StringBuilder();
        foreach (MethodBase method in Methods(game))
        {
            if (!SemanticMethodIdentity.TryHash(method, out string body, out string reason))
                throw new InvalidOperationException("prepared audio decoder body unavailable: " + method + ": " + reason);
            canonical.Append(SemanticMethodIdentity.Signature(method)).Append('|').Append(body).Append('\n');
        }
        using var hash = SHA256.Create();
        return Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    internal static void ValidateDependency(Assembly assembly, string name, string mvid, string expectedHash)
    {
        if (assembly.GetName().Name != name || assembly.ManifestModule.ModuleVersionId.ToString("D") != mvid)
            throw new InvalidOperationException("prepared audio decoder identity changed: " + name);
        string path = assembly.Location;
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("prepared audio decoder file identity unavailable: " + name);
        using var input = File.OpenRead(path);
        using var hash = SHA256.Create();
        if (Hex(hash.ComputeHash(input)) != expectedHash)
            throw new InvalidOperationException("prepared audio decoder file changed: " + name);
    }

    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
}
