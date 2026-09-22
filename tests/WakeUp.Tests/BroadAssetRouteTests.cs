using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class BroadAssetRouteTests
{
    private static ModContentPack Mod()
    {
        var mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        AccessTools.Field(typeof(ModContentPack), "strings").SetValue(mod, new ModContentHolder<string>(mod));
        return mod;
    }
    [Test]
    public void ExternalSuccessSkipsDiscoveryOnlyWhileEveryHolderProofIsCurrent()
    {
        var mods = new List<ModContentPack> { Mod(), Mod() };
        var cache = new AssetRouteCache<string>();
        Assert.That(cache.TryGet(mods, "resource", out _), Is.False);
        Assert.That(cache.Count, Is.Zero, "a missing search stores no route");
        cache.RememberExternal(mods, "resource");
        Assert.That(cache.HasExternalRoute(mods, "resource"), Is.True);
        mods[0].GetContentHolder<string>().contentList["resource"] = "holder";
        Assert.That(cache.HasExternalRoute(mods, "resource"), Is.False);
        Assert.That(cache.TryGet(mods, "resource", out var value), Is.True);
        Assert.That(value, Is.EqualTo("holder"));
        Assert.That(cache.ExternalPaths, Is.Empty);
    }
    [Test]
    public void CallbackMutationCannotCertifyPostCallbackGeneration()
    {
        var mods = new List<ModContentPack> { Mod(), Mod() };
        var cache = new AssetRouteCache<string>();
        cache.TryGet(mods, "resource", out _);
        // A Resources callback adds a provider result after the native scan.
        mods[1].GetContentHolder<string>().contentList["resource"] = "new";
        cache.RememberExternal(mods, "resource");
        Assert.That(cache.HasExternalRoute(mods, "resource"), Is.False);
        Assert.That(cache.TryGet(mods, "resource", out var value), Is.True);
        Assert.That(value, Is.EqualTo("new"));
        cache.TryGet(mods, "different", out _);
        mods.Reverse();
        cache.RememberExternal(mods, "different");
        Assert.That(cache.HasExternalRoute(mods, "different"), Is.False);
    }
    [Test]
    public void HolderReplacementAndGenerationClearReleaseExternalRoutes()
    {
        var mods = new List<ModContentPack> { Mod() };
        var cache = new AssetRouteCache<string>();
        cache.TryGet(mods, "resource", out _); cache.RememberExternal(mods, "resource");
        var holder = new ModContentHolder<string>(mods[0]);
        AccessTools.Field(typeof(ModContentPack), "strings").SetValue(mods[0], holder);
        Assert.That(cache.HasExternalRoute(mods, "resource"), Is.False);
        cache.TryGet(mods, "resource", out _); cache.RememberExternal(mods, "resource");
        holder.contentList = new Dictionary<string, string>();
        Assert.That(cache.HasExternalRoute(mods, "resource"), Is.False);
        cache.Clear(); Assert.That(cache.Count, Is.Zero);
        Assert.That(cache.ExternalPaths, Is.Empty);
    }
    [Test]
    public void PublicDictionaryAndNativeFolderTrieRemainIndependent()
    {
        var mods = new List<ModContentPack> { Mod() };
        var holder = mods[0].GetContentHolder<string>();
        var trie = AccessTools.Field(holder.GetType(), "contentListTrie").GetValue(holder);
        AccessTools.Method(trie.GetType(), "Add", new[] { typeof(string) }).Invoke(trie, new object[] { "Folder/Original" });
        holder.contentList["Folder/Original"] = "original";
        holder.contentList["Folder/DirectOnly"] = "direct";
        var cache = new AssetRouteCache<string>();
        cache.TryGet(mods, "Folder/DirectOnly", out var found);
        Assert.That(found, Is.EqualTo("direct"));
        Assert.That(holder.GetAllUnderPath("Folder").ToArray(), Is.EqualTo(new[] { "original" }));
        holder.contentList.Remove("Folder/Original");
        Assert.Throws<KeyNotFoundException>((Action)(() => { holder.GetAllUnderPath("Folder").ToArray(); }));
        Assert.That(cache.TryGet(mods, "folder/DirectOnly", out _), Is.False);
    }
    [Test]
    public void QualityReadAdmissionIsExactAndExposureIsIdempotent()
    {
        var target = AccessTools.Method(typeof(ModContentHolder<UnityEngine.Texture2D>), "Get");
        var assembly = typeof(Harmony).Assembly;
        var state = (Dictionary<MethodBase, byte[]>)assembly.GetType("HarmonyLib.HarmonySharedState")!
            .GetField("state", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        var serialization = assembly.GetType("HarmonyLib.PatchInfoSerialization")!;
        state.TryGetValue(target, out var previous);
        void Publish(string owner, bool postfix)
        {
            object info = Activator.CreateInstance(assembly.GetType("HarmonyLib.PatchInfo")!)!;
            var patch = new HarmonyMethod(AccessTools.Method(typeof(PreparedQualityRuntime), "ReadOne"));
            AccessTools.Method(info.GetType(), postfix ? "AddPostfixes" : "AddPrefixes").Invoke(info, new object[] { owner, new[] { patch } });
            var bytes = (byte[])AccessTools.Method(serialization, "Serialize").Invoke(null, new[] { info });
            lock (state) state[target] = bytes;
        }
        try
        {
            var guard = new AssetRoutingPatchGuard();
            Publish(PngRuntime.Owner, false); Assert.That(guard.Allows(), Is.True);
            Publish("Foreign", false); Assert.That(guard.Allows(), Is.False);
            Publish(PngRuntime.Owner, true); Assert.That(guard.Allows(), Is.False);
            Publish(PngRuntime.Owner, false); Assert.That(guard.Allows(), Is.True);
        }
        finally { lock (state) { if (previous == null) state.Remove(target); else state[target] = previous; } }

        const string path = "C09/ExposureBoundary";
        var exposure = (HashSet<string>)AccessTools.Field(typeof(PreparedQualityRuntime), "exposed").GetValue(null);
        bool old = PreparedTextureRuntime.UseAtStartup;
        var setter = AccessTools.PropertySetter(typeof(PreparedTextureRuntime), "UseAtStartup");
        try
        {
            setter.Invoke(null, new object[] { true });
            PreparedQualityRuntime.ObserveRoute(path); PreparedQualityRuntime.ObserveRoute(path);
            Assert.That(exposure.Count(p => p == path), Is.EqualTo(1));
            exposure.Remove(path);
            setter.Invoke(null, new object[] { false });
            PreparedQualityRuntime.ObserveRoute(path); Assert.That(exposure.Contains(path), Is.False);
        }
        finally { exposure.Remove(path); setter.Invoke(null, new object[] { old }); }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference[] RetireHolder(AssetRouteCache<string> cache, List<ModContentPack> mods)
    {
        var old = mods[0].GetContentHolder<string>();
        var asset = new string('x', 32);
        old.contentList["retired"] = asset;
        cache.TryGet(mods, "retired", out _);
        var weak = new[] { new WeakReference(old), new WeakReference(old.contentList), new WeakReference(asset) };
        AccessTools.Field(typeof(ModContentPack), "strings").SetValue(mods[0], new ModContentHolder<string>(mods[0]));
        return weak;
    }
    [Test]
    public void RoutingDoesNotRootRetiredHolderDictionaryOrAssets()
    {
        var mods = new List<ModContentPack> { Mod() };
        var cache = new AssetRouteCache<string>();
        var retired = RetireHolder(cache, mods);
        // Collection is a deterministic liveness check, not a cost experiment.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.That(retired.All(w => !w.IsAlive), Is.True, "route snapshots must not extend asset lifetime");
        Assert.That(cache.TryGet(mods, "retired", out _), Is.False);
        mods[0].GetContentHolder<string>().contentList["retired"] = "current";
        Assert.That(cache.TryGet(mods, "retired", out var current), Is.True);
        Assert.That(current, Is.EqualTo("current"));
        GC.KeepAlive(mods); GC.KeepAlive(cache);
    }
}
