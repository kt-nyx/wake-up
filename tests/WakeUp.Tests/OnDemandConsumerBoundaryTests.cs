// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using NUnit.Framework;
using UnityEngine;
using Verse;

namespace WakeUp.Tests;

// Native managed API checks, not a Unity creation or candidate readiness test.
// A managed texture shell is sufficient: these native readers use no engine API.
[TestFixture, NonParallelizable]
public sealed class OnDemandConsumerBoundaryTests
{
    [Test]
    public void FirstPublicHolderAcquisitionWorksOnWorkerWhileCallerWaits()
    {
        var pack = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        var holder = new ModContentHolder<Texture2D>(pack);
        var texture = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
        holder.contentList.Add("Artwork", texture);
        AccessTools.Field(typeof(ModContentPack), "textures").SetValue(pack, holder);
        int caller = Thread.CurrentThread.ManagedThreadId;

        var request = Task.Run(() =>
        {
            int worker = Thread.CurrentThread.ManagedThreadId;
            // First acquisition of this holder occurs here, with no main-thread
            // exposure or lease handshake in the native public API.
            var acquired = pack.GetContentHolder<Texture2D>();
            return Tuple.Create(worker, acquired, acquired.Get("Artwork"), acquired.contentList);
        });
        Assert.That(request.Wait(TimeSpan.FromSeconds(10)), Is.True, "Native managed holder access must finish without a main-thread pump.");
        Assert.That(request.Result.Item1, Is.Not.EqualTo(caller));
        Assert.That(request.Result.Item2, Is.SameAs(holder));
        Assert.That(request.Result.Item3, Is.SameAs(texture));
        Assert.That(request.Result.Item4, Is.SameAs(holder.contentList));
    }

    [Test]
    public void ReflectedPublicHolderGetterAndDictionaryReadWorkOnWorker()
    {
        var pack = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        var holder = new ModContentHolder<AudioClip>(pack);
        var clip = (AudioClip)FormatterServices.GetUninitializedObject(typeof(AudioClip));
        holder.contentList.Add("Sound", clip);
        AccessTools.Field(typeof(ModContentPack), "audioClips").SetValue(pack, holder);
        MethodInfo getter = typeof(ModContentPack).GetMethod("GetContentHolder")!.MakeGenericMethod(typeof(AudioClip));
        FieldInfo contents = typeof(ModContentHolder<AudioClip>).GetField("contentList")!;

        var request = Task.Run(() =>
        {
            object acquired = getter.Invoke(pack, null);
            var dictionary = (Dictionary<string, AudioClip>)contents.GetValue(acquired);
            return dictionary["Sound"];
        });
        Assert.That(request.Wait(TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(request.Result, Is.SameAs(clip));
    }
}
