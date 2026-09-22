// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using RimWorld.IO;
using UnityEngine;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class DemandTextureInitializationTests
{
    [Test]
    public void DisabledAndFailedAccessorSetupLeaveNativeBridgesUsableAndCanRecover()
    {
        var type = typeof(DemandTextureRuntime);
        string[] names = { "TextureHolder", "HolderMod", "AudioHolderMod", "Trie", "TrieAdd", "ToFile", "accessorsInitialized", "<Status>k__BackingField" };
        var fields = names.Select(n => AccessTools.Field(type, n)).ToArray();
        var saved = fields.Select(f => f.GetValue(null)).ToArray();
        try
        {
            foreach (var field in fields.Take(6)) field.SetValue(null, null);
            fields[6].SetValue(null, false);
            var mod = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
            var texture = new ModContentHolder<Texture2D>(mod);
            var audio = new ModContentHolder<AudioClip>(mod);
            AccessTools.Field(typeof(ModContentPack), "textures").SetValue(mod, texture);
            AccessTools.Field(typeof(ModContentPack), "audioClips").SetValue(mod, audio);
            texture.contentList.Add("native-null", null!);
            audio.contentList.Add("native-null", null!);
            void OrdinaryBridges()
            {
                Assert.That(DemandTextureRuntime.Enabled, Is.False);
                Assert.That(DeferredAudioRuntime.TryReload(typeof(AudioClip), texture, false), Is.False);
                DeferredAudioRuntime.EnsurePath(typeof(AudioClip), texture, "native-null", false);
                DeferredAudioRuntime.ExposeHolder(typeof(AudioClip), texture);
                DeferredAudioRuntime.ClearHolder(typeof(AudioClip), texture);
                Assert.That(DemandTextureRuntime.GetHolder(mod), Is.SameAs(texture));
                Assert.That(DeferredAudioRuntime.GetHolder<Texture2D>(mod), Is.SameAs(texture));
                Assert.That(DeferredAudioRuntime.GetHolder<AudioClip>(mod), Is.SameAs(audio));
                Assert.That(DeferredAudioRuntime.HasPending(audio), Is.False);
                Assert.That(texture.contentList.ContainsKey("native-null") && audio.contentList.ContainsKey("native-null"), Is.True);
            }
            string isolated = "-savedatafolder=" + Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "demand-init-probe"));
            string[] off = { "--wake-up-mode=candidate", "--wake-up-demand-textures=off", isolated };
            string[] on = { "--wake-up-mode=candidate", "--wake-up-demand-textures=on", isolated };
            int calls = 0;
            Func<Func<FileInfo, VirtualFile>> fail = () => { calls++; throw new MissingMethodException("conversion deliberately unavailable"); };
            Assert.That(DemandTextureRuntime.TryInitializeAccessors(off, fail), Is.False);
            Assert.That(calls, Is.Zero);
            OrdinaryBridges();
            Assert.That(DemandTextureRuntime.TryInitializeAccessors(on, fail), Is.False);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(DemandTextureRuntime.Status, Does.Contain("refused").And.Contain("conversion deliberately unavailable"));
            Assert.That(fields.Take(6).Select(f => f.GetValue(null)), Is.All.Null, "Failed setup must publish no partial accessor set.");
            OrdinaryBridges();
            Assert.That(DemandTextureRuntime.TryInitializeAccessors(on, () =>
            {
                calls++;
                return (Func<FileInfo, VirtualFile>)AccessTools.Method(type, "CreateFileConversion").Invoke(null, null);
            }), Is.True);
            Assert.That(DemandTextureRuntime.TryInitializeAccessors(on, fail), Is.True);
            Assert.That(calls, Is.EqualTo(2), "Successful accessors are initialized once, even after an earlier refusal.");
            Assert.That(fields.Take(6).Select(f => f.GetValue(null)), Is.All.Not.Null);
        }
        finally { for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]); }
    }
}
