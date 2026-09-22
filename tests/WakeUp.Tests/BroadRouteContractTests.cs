using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class BroadRouteContractTests
{
    [Test]
    public void ExactNativeAndCoordinatedBodies()
    {
        using var module = ModuleDefinition.ReadModule(typeof(ContentFinder<>).Assembly.Location);
        var bundle = module.GetType("Verse.ContentFinder`1").Methods.Single(m => m.Name == "TryFindAssetInModBundles");
        TestContext.Out.WriteLine("BUNDLE_CECIL=" + AssetRoutingPrepatch.Fingerprint(bundle));
        Print("NATIVE", typeof(ContentFinder<>).Assembly);
        AssetRoutingPrepatch.Inject(module, AssetRoutingPrepatch.FindGet(module)!);
        AssetRoutingPrepatch.InjectBundle(module, bundle);
        using var first = new MemoryStream(); module.Write(first);
        var original = Assembly.Load(first.ToArray()); Print("ROUTED", original);
        Assert.That(SemanticMethodIdentity.TryHash(original.GetType("Verse.ContentFinder`1")!.GetMethod("Get")!, out var plain, out _), Is.True);
        TestContext.Out.WriteLine("GET_ROUTED=" + plain);
        Assert.That(plain, Is.EqualTo(AssetRoutingRuntime.Bodies[0]));
        Assert.That(DeferredAudioPrepatch.RewriteNative(module), Is.True);
        using var second = new MemoryStream(); module.Write(second);
        var combined = Assembly.Load(second.ToArray()); Print("COMBINED", combined);
        Assert.That(SemanticMethodIdentity.TryHash(combined.GetType("Verse.ContentFinder`1")!.GetMethod("Get")!, out var deferred, out _), Is.True);
        TestContext.Out.WriteLine("GET_COMBINED=" + deferred);
        Assert.That(deferred, Is.EqualTo(AssetRoutingRuntime.DeferredBodies[0]));
    }
    private static void Print(string prefix, Assembly game)
    {
        var methods = AssetRoutingContract.Methods(game);
        for (int i = 0; i < methods.Length; i++)
        {
            Assert.That(SemanticMethodIdentity.TryHash(methods[i], out string hash, out string reason), Is.True, methods[i] + " " + reason);
            TestContext.Out.WriteLine(prefix + "_" + i + "=" + hash);
            if (prefix != "NATIVE" || i != 0 && i != 15 && i != 16 && i != 17)
                Assert.That(AssetRoutingContract.Bodies[i], Does.Contain(hash), prefix + " contract " + i);
        }
    }
}
