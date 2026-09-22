// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace WakeUp;

// Harmony cannot reconstruct this iterator's fault regions. Change its one
// Action call before loading, preserving Cecil's original exception regions.
public static class LoadingCallbackPrepatch
{
    internal const string OriginalBody = "D90E94252C9C825190FC77EC7878ED36D05D3D44C69DF3EB5B56AD4A619E6697";
    internal const string RuntimeBody = "C3B9CF53A0D6985D6354BBA57308A03199C6BA613E2AB186791E5AE115FB07A8";

    [FreePatchAll]
    public static bool RewriteAssembly(ModuleDefinition module)
    {
        try
        {
            var method = Find(module);
            if (method == null || AssetRoutingPrepatch.Fingerprint(method) != OriginalBody) return false;
            Inject(module, method);
            return true;
        }
        catch { return false; }
    }
    internal static MethodDefinition? Find(ModuleDefinition module)
    {
        if (module.Assembly.Name.Name != "ilyvion.LoadingProgress") return null;
        var owner = module.GetType("ilyvion.LoadingProgress.LongEventHandler_ExecuteToExecuteWhenFinished_Patches");
        return owner?.NestedTypes.SingleOrDefault(t => t.Name == "<ExecuteToExecuteWhenFinished>d__3")
            ?.Methods.SingleOrDefault(m => m.Name == "MoveNext" && m.HasBody && !m.IsStatic
                && m.Parameters.Count == 0 && m.ReturnType.FullName == "System.Boolean");
    }
    internal static void Inject(ModuleDefinition module, MethodDefinition method)
    {
        var call = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Callvirt && i.Operand is MethodReference m
            && m.DeclaringType.FullName == "System.Action" && m.Name == "Invoke" && m.Parameters.Count == 0
            && m.ReturnType.FullName == "System.Void");
        var wrapper = module.ImportReference(typeof(EarlyLoadingObservation).GetMethod(nameof(EarlyLoadingObservation.ExecuteDeferredAction)));
        call.OpCode = OpCodes.Call;
        call.Operand = wrapper;
    }
}
