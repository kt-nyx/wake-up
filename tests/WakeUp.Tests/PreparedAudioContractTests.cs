// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;
using RuntimeAudioClipLoader;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class PreparedAudioContractTests
{
    [Test]
    public void OriginalReferenceContractEvidence()
    {
        Assembly game = typeof(Manager).Assembly;
        using var module = ModuleDefinition.ReadModule(game.Location);
        string methodsHash = PreparedAudioContract.MethodsHash(game);
        TestContext.Out.WriteLine("PREPARED_METHODS=" + methodsHash);
        Assert.That(methodsHash, Is.EqualTo("BEDB8F2A84C01DBA178DFC8682126399C1C6A60019605C470DBD1EC62CE8AA0A"));
        Assert.That(methodsHash, Is.EqualTo(PreparedAudioContract.NativeMethodsHash));
        Assert.DoesNotThrow((Action)(() => PreparedAudioContract.ValidateSourceFactory(game)));
        foreach (MethodBase method in PreparedAudioContract.Methods(game))
        {
            var native = (MethodDefinition)module.LookupToken(method.MetadataToken);
            Assert.That(SemanticMethodIdentity.TryHash(method, out string hash, out string reason), Is.True, reason);
            TestContext.Out.WriteLine("PREPARED_METHOD " + SemanticMethodIdentity.Signature(method) + "|" + hash + "|CECIL=" + AssetRoutingPrepatch.Fingerprint(native));
        }
        foreach (string name in new[] { "NAudio", "NVorbis" })
        {
            Assembly decoder = Assembly.Load(name);
            TestContext.Out.WriteLine(name + "_MVID=" + decoder.ManifestModule.ModuleVersionId.ToString("D"));
            string mvid = name == "NAudio" ? PreparedAudioContract.NAudioMvid : PreparedAudioContract.NVorbisMvid;
            string hash = name == "NAudio" ? PreparedAudioContract.NAudioHash : PreparedAudioContract.NVorbisHash;
            Assert.That(decoder.ManifestModule.ModuleVersionId.ToString("D"), Is.EqualTo(mvid));
            Assert.DoesNotThrow((Action)(() => PreparedAudioContract.ValidateDependency(decoder, name, mvid, hash)));
        }
    }
}
