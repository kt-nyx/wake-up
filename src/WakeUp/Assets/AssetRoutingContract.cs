// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

internal static class AssetRoutingContract
{
    internal static MethodBase[] Methods(Assembly game)
    {
        Type holder = game.GetType("Verse.ModContentHolder`1", true);
        Type pack = game.GetType("Verse.ModContentPack", true);
        Type finder = game.GetType("Verse.ContentFinder`1", true);
        Type paths = game.GetType("Verse.GenFilePaths", true);
        return new MethodBase[] {
            AccessTools.Method(finder, "TryFindAssetInModBundles"),
            AccessTools.Method(holder.MakeGenericType(typeof(AudioClip)), "Get"),
            AccessTools.Method(holder.MakeGenericType(typeof(string)), "Get"),
            AccessTools.Method(pack, "GetContentHolder").MakeGenericMethod(typeof(AudioClip)),
            AccessTools.Method(pack, "GetContentHolder").MakeGenericMethod(typeof(string)),
            AccessTools.PropertyGetter(pack, "FolderName"), AccessTools.PropertyGetter(pack, "PackageIdPlayerFacing"),
            AccessTools.PropertyGetter(pack, "IsOfficialMod"), AccessTools.Method(paths, "ContentPath").MakeGenericMethod(typeof(Texture2D)),
            AccessTools.Method(typeof(Resources), "Load", new[] { typeof(string), typeof(Type) }),
            typeof(Resources).GetMethods().Single(m => m.Name == "Load" && m.IsGenericMethodDefinition),
            AccessTools.Method(typeof(AssetBundle), "LoadAsset", new[] { typeof(string), typeof(Type) }),
            AccessTools.PropertyGetter(typeof(ResourcesAPI), "ActiveAPI"),
            AccessTools.Method(paths, "ContentPath").MakeGenericMethod(typeof(AudioClip)),
            AccessTools.Method(paths, "ContentPath").MakeGenericMethod(typeof(Shader)),
            AccessTools.Method(finder.MakeGenericType(typeof(Texture2D)), "TryFindAssetInModBundles"),
            AccessTools.Method(finder.MakeGenericType(typeof(AudioClip)), "TryFindAssetInModBundles"),
            AccessTools.Method(finder.MakeGenericType(typeof(Shader)), "TryFindAssetInModBundles"),
        };
    }
    // Exact native and coordinated prepatch bodies, populated from the pinned
    // GOG reference and independently exercised in Mono by the fixture.
    internal static readonly string[][] Bodies = {
        new[] { "B85212D3643C7FF0721FC8C1ADF95FAEA7BF58D1DFEE6FA450410DD34E45FAF8" },
        new[] { "D86F305BED8F809E867F95D0384CBFFA1EE61E443DC4C0E845D3FC7CFA7AE613", "FF53176B1E8E3437CD1B6276D802E1AB915CAA1E9FFF5A974A8081D4000BE99F" },
        new[] { "9CFECC46FF7C90B08E15D0A0E02A3719B9C10B5F82B612FAB54F84573933171B", "4783082434F2FED6B70B38F6BB9D0E872CE3B9708FB85C6F6666BEE31EB59A7D" },
        new[] { "0F2642BD5A62770E8AD8399F1F3DEF2C06427784DCF5570B77444F5246276157", "10FFEF6B20C7BA72667D7DCAB0B5E44AC5A06197FA68295FD178A0107EF373D5" },
        new[] { "C634271E08E8ED2347B9271441C88D1CAA71E45815533B03B01512A66D7B16B3", "15388F1AC65BC1F5FE83A7464A1E261C645B23138F3EB3BF392F113451E64680" },
        new[] { "E1193269E407BFBE0F7398F82FA1BB6E9DAAD7459BE31BABAAE6EB361CFFF600" },
        new[] { "7CA0EAA9A0AE339A5FCC0CA128B475D9972E20240B8A411410DACFCBAE66B8AC" },
        new[] { "CE4F253575A79C9E08D818B0FE09C2E87812D158BBED8D23ED26D25761877E5F" },
        new[] { "D45C65B86C0BA63284A1ED814C925089DB1D524D61FD846C73D2B226801D3BA9" },
        new[] { "B2E3188BB1DBFAE345D082985362FE913258F7E5CBC00C41673B32388CE0948C" },
        new[] { "FD1859CEA6F4FDDE495CC71485F8674AF47CA7312F8DF88DE3D8A59DD4A0F6C2" },
        new[] { "7DD450CEA14F01359D096C87E481EC8A06D89FBA10149FCBB27E02E610DA5E5A" },
        new[] { "17D489ED7A0F4E616ED56331FEDE8C60FF2F95C2A596840304F33CAE70B4D06B" },
        new[] { "AC4AD980BD611F4731CDB5D232EBFA9072826B9F7B38E2597D3E861663B980B5" },
        new[] { "C6C26D27B51090784EA8B5493054810FE93A01866A528AFD33442045F3A8C981" },
        new[] { "CC43FED3C2FE59F15B5FE9A0D0E42FE730776003E19471D656FFC74077D15EC4" },
        new[] { "538DA3128278A59D68B5E055340401FAFF2B6799BCCB55A03D15D042483947BC" },
        new[] { "CC3E511F90FF0FD8C7A9FA775DF6B40383A339B9B607CFC828B4CE6A8C01BF5D" },
    };
    internal static void Validate()
    {
        var methods = Methods(typeof(ContentFinder<>).Assembly);
        if (Bodies.Length != methods.Length) throw new InvalidOperationException("incomplete-broad-routing-contract");
        for (int i = 0; i < methods.Length; i++)
            if (!SemanticMethodIdentity.TryHash(methods[i], out string hash, out _) || !Bodies[i].Contains(hash))
                throw new InvalidOperationException("changed-broad-routing-body-" + methods[i].Name);
        var native = AccessTools.Method(typeof(AssetBundle), "LoadAsset_Internal");
        if (native == null || native.GetMethodBody() != null
            || (native.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) == 0)
            throw new InvalidOperationException("changed-native-bundle-loader");
    }
}
