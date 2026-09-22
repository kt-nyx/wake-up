// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WakeUp;

// Optional cooperation with one verified patch implementation, not a package
// allowlist. Its postfix can hide an icon; its transpiler adjusts text/margins.
// Neither replaces pixels, texture identity or the native drawing operation.
internal sealed class PreparationMainButtonCompatibility
{
    private const string Owner = "OskarPotocki.VanillaTexturesExpanded";
    private const string Binary = "427A2544330495A2CA04498A79C2AFEDECB96B9448B06F3AFC18D5EBFFDDA683";
    private static readonly Guid ModuleIdentity = new("4d7cc2f2-5b08-41c5-84ba-053ef6513424");
    private const string IconType = "VanillaTexturesExpanded.Patch_MainButtonDef+get_Icon";
    private const string ButtonType = "VanillaTexturesExpanded.Patch_MainButtonWorker+DoButton";
    private static readonly MethodInfo Icon = AccessTools.PropertyGetter(typeof(MainButtonDef), "Icon");
    private static readonly MethodInfo Button = AccessTools.Method(typeof(MainButtonWorker), "DoButton");
    private MethodInfo? postfix, transpiler;
    internal string RefusalReason { get; private set; } = "No matching verified VTE main-button patches";
    // Whole closure, including all iterator methods and its constructor. Sorted
    // multiset comparison preserves the two identical get_Current bodies.
    private static readonly string[] Bodies = {
        "CFF478E0A7CBA3B4FE9D7D8631768D7CFF53110ABA82A9C6AE2A8050A6AC82C2",
        "74880F133871C9A6AB80240C14A717D8C14630DF9DBA0F31D059D480C221BE24",
        "209E73561146197F25C28AD2E36867125ACF5AC01751A6E0F4FAD75A442CAECF",
        "97C5AB90C7E8A817DA77AB4A51049AEBB457CC6324A670A5EBFEFF629394285E",
        "15769C9F95BBA3F1A3AF432AF2664A9C7352850645205C2AB8FD0CCEAC550BEE",
        "209E73561146197F25C28AD2E36867125ACF5AC01751A6E0F4FAD75A442CAECF",
        "115FDA1C53125F57E473F754C3B15193EC1691A983B3234457573D5F0EA8CDA9",
        "84CE095C467CA976BF96064BD9733A1BBF03B888603B361F885F1AF5060B3539",
        "5C42FE5C50EB9FE635C75076F5E8716FAE823825732EFF59BED61D47ED9BFDEA",
        "31725EEF8CAB71DDD65A27AA24A75EA53C3CEC242F10FBE80CF7247A19A82B3A",
        "EEBCEF7F5AB3C29ECE674D1F3C213B2628937F2E855AB28BD50CE7DA07501869",
        "E91626C169B7189583679DD7EE7A377C76D9E49C0BEC88CC0418304343C30B15",
        "7795E043DB8DB639ADCF0535C9CD4D9870056292A64B7293273C0BF8DD91C62A",
        "29E9684635E46EEEFF9D312A97CD63455DDA2B944A320C73FEE2AF6C9B93C33D",
        "854F2587687F7609A07F644C7C23F39D83AD41AE7BE53E3E85D8B71DAC7F4772",
        "B15276EBFF9ABB41935E874AFA5E6158E606DA9F4A0B551C8521D4EC67D3201F"
    };

    internal static bool IsTarget(MethodBase method) => method == Icon || method == Button;

    internal static PreparationMainButtonCompatibility Capture(List<PublishedPatchGuard> dependencies)
    {
        var result = new PreparationMainButtonCompatibility();
        try
        {
            var patches = new[] { Icon, Button }.SelectMany(m => {
                var info = Harmony.GetPatchInfo(m);
                return info == null ? Enumerable.Empty<Patch>() : info.Postfixes.Concat(info.Transpilers);
            }).Where(p => p.owner == Owner && (p.PatchMethod.DeclaringType?.FullName == IconType
                || p.PatchMethod.DeclaringType?.FullName == ButtonType)).ToArray();
            if (patches.Length == 0) return result;
            Assembly assembly = patches[0].PatchMethod.Module.Assembly;
            if (patches.Any(p => p.PatchMethod.Module.Assembly != assembly))
                throw new InvalidOperationException("VTE patch methods belong to different assemblies");
            if (assembly.ManifestModule.ModuleVersionId != ModuleIdentity)
                throw new InvalidOperationException("VTE runtime module identity differs: " + assembly.ManifestModule.ModuleVersionId);
            VerifySourceBinary(assembly);
            var methods = new List<MethodBase>();
            void Add(Type type)
            {
                methods.AddRange(type.GetMethods(AccessTools.allDeclared));
                methods.AddRange(type.GetConstructors(AccessTools.allDeclared));
                foreach (Type nested in type.GetNestedTypes(AccessTools.allDeclared)) Add(nested);
            }
            Type icon = assembly.GetType(IconType, true)!, button = assembly.GetType(ButtonType, true)!;
            Add(icon); Add(button);
            methods.Add(AccessTools.PropertyGetter(assembly.GetType("VanillaTexturesExpanded.VanillaTexturesExpandedSettings", true)!, "MainButtonsHaveIcons"));
            methods.AddRange(assembly.GetType("VanillaTexturesExpanded.VanillaTexturesExpandedUtility", true)!.GetMethods(AccessTools.allDeclared));
            var remaining = Bodies.ToList(); var checkedGuards = new List<PublishedPatchGuard>();
            foreach (MethodBase method in methods)
            {
                string label = method.DeclaringType?.FullName + "." + method.Name;
                if (!SemanticMethodIdentity.TryHash(method, out string hash, out string reason))
                    throw new InvalidOperationException("VTE method identity unavailable for " + label + ": " + reason);
                if (!remaining.Remove(hash))
                    throw new InvalidOperationException("VTE method body differs for " + label + ": " + hash);
                if (!PublishedPatchGuard.TryCreate(method, "WakeUp.verified-main-button-dependency", out var guard, true))
                    throw new InvalidOperationException("VTE dependency hook guard unavailable for " + label);
                if (!guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("VTE dependency has unsupported hooks: " + label);
                checkedGuards.Add(guard);
            }
            if (remaining.Count != 0) throw new InvalidOperationException("VTE method closure incomplete: " + methods.Count + " of " + Bodies.Length + " methods");
            result.postfix = AccessTools.Method(icon, "Postfix");
            result.transpiler = AccessTools.Method(button, "Transpiler");
            dependencies.AddRange(checkedGuards);
            result.RefusalReason = "";
        }
        catch (Exception e) { result.postfix = result.transpiler = null; result.RefusalReason = e.GetType().Name + ": " + e.Message; }
        return result;
    }

    private static void VerifySourceBinary(Assembly assembly)
    {
        string Hash(string path)
        {
            OwnedCacheStore.RejectLinkedPath(path);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "");
        }
        if (!string.IsNullOrEmpty(assembly.Location))
        {
            string actual = Hash(assembly.Location);
            if (actual != Binary) throw new InvalidOperationException("VTE binary SHA256 differs: " + actual);
            return;
        }
        // Prepatcher loads assemblies from bytes. Bind the file to the running
        // owner of this exact Assembly, using native effective folder selection.
        // Runtime module/body checks still verify the loaded implementation.
        var owners = LoadedModManager.RunningModsListForReading
            .Where(m => m.assemblies.loadedAssemblies.Contains(assembly)).ToArray();
        if (owners.Length != 1) throw new InvalidOperationException("VTE byte-loaded assembly has no unique running owner");
        var files = ModContentPack.GetAllFilesForMod(owners[0], "Assemblies",
            extension => string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase));
        int matches = files.Values.Count(file => Hash(file.FullName) == Binary);
        if (matches != 1) throw new InvalidOperationException("VTE running owner's effective Assemblies files contain " + matches + " matching verified binaries");
    }

    internal bool Allows(MethodBase target, Patch patch)
    {
        MethodInfo? expected = target == Icon ? postfix : target == Button ? transpiler : null;
        if (expected == null) return false;
        if (patch.owner != Owner || patch.PatchMethod != expected)
        {
            RefusalReason = "Unsupported main-button patch: " + patch.owner + " / " + patch.PatchMethod.DeclaringType?.FullName + "." + patch.PatchMethod.Name;
            return false;
        }
        var info = Harmony.GetPatchInfo(target);
        if (info == null) return false;
        bool isTranspiler = target == Button;
        var intended = isTranspiler ? info.Transpilers : info.Postfixes;
        bool allowed = intended.Count(p => p.owner == Owner && p.PatchMethod == expected) == 1
            && info.Prefixes.Concat(info.Finalizers).Concat(info.InnerPrefixes).Concat(info.InnerPostfixes)
                .Concat(isTranspiler ? info.Postfixes : info.Transpilers).All(p => p.PatchMethod != expected);
        if (!allowed) RefusalReason = "VTE patch has an unexpected hook kind or duplicate registration: " + expected.Name;
        return allowed;
    }
}
