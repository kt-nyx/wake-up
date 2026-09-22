// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

internal static class C10AudioGameplayConsumers
{
    internal static Dictionary<string, object> Exercise(AudioClip clip, ModContentHolder<AudioClip> originalHolder, string key)
    {
        var result = new Dictionary<string, object>
        {
            ["passed"] = false, ["failure"] = "", ["audibleQualityClaim"] = false,
            ["workerUsesUnityApis"] = false, ["workerJoined"] = false,
            ["nativeMusicPassed"] = false, ["nativeStopPassed"] = false,
            ["temporaryEntryRemoved"] = false
        };
        string temporaryKey = "C10PreparedAudio/Gameplay/" + Guid.NewGuid().ToString("N");
        ModContentHolder<AudioClip>? observerHolder = null;
        SongDef? song = null;
        MusicManagerPlay? music = null;
        AudioSource? source = null;
        bool added = false, playbackAttempted = false;
        try
        {
            Require(UnityData.IsInMainThread && Current.ProgramState == ProgramState.Playing && Current.Game != null,
                "Gameplay consumers require the main gameplay thread.");
            Require(!LongEventHandler.AnyEventNowOrWaiting, "Song resolution requires completed native long events.");
            result["masterVolume"] = Prefs.VolumeMaster;
            Require(Prefs.VolumeMaster == 0f, "The fixture must already have master volume exactly zero.");
            if (clip == null || !ReferenceEquals(originalHolder.Get(key), clip))
                throw new InvalidOperationException("Original holder does not own the supplied clip/key.");

            observerHolder = LoadedModManager.GetMod<MenuObserverMod>().Content.GetContentHolder<AudioClip>();
            observerHolder.contentList.Add(temporaryKey, clip);
            added = true;
            song = new SongDef { defName = "C10GameplayConsumer_" + Guid.NewGuid().ToString("N"), clipPath = temporaryKey };
            song.ResolveReferences();
            FieldInfo clipField = typeof(SongDef).GetField(nameof(SongDef.clip), BindingFlags.Instance | BindingFlags.Public)!;
            FieldInfo contentsField = typeof(ModContentHolder<AudioClip>).GetField(nameof(ModContentHolder<AudioClip>.contentList), BindingFlags.Instance | BindingFlags.Public)!;
            result["songResolvePassed"] = ReferenceEquals(song.clip, clip);
            result["contentFinderPassed"] = ReferenceEquals(ContentFinder<AudioClip>.Get(temporaryKey), clip);
            result["publicFieldPassed"] = ReferenceEquals(clipField.GetValue(song), clip);
            Require((bool)result["songResolvePassed"] && (bool)result["contentFinderPassed"] && (bool)result["publicFieldPassed"],
                "Native song/content/public-field access changed clip identity.");

            int mainThread = Thread.CurrentThread.ManagedThreadId;
            SongDef workerSong = song;
            Task<object[]> worker = Task.Run(() =>
            {
                // No Unity properties, overloaded equality, logging or content
                // creation here. The main thread waits without pumping work.
                var contents = (Dictionary<string, AudioClip>)contentsField.GetValue(originalHolder)!;
                return new object[]
                {
                    Thread.CurrentThread.ManagedThreadId,
                    ReferenceEquals(originalHolder.Get(key), clip),
                    contents.TryGetValue(key, out AudioClip found) && ReferenceEquals(found, clip),
                    ReferenceEquals(clipField.GetValue(workerSong), clip)
                };
            });
            bool joined = worker.Wait(TimeSpan.FromSeconds(5));
            result["workerJoined"] = joined;
            Require(joined, "Worker access did not finish while the main thread waited.");
            object[] checks = worker.GetAwaiter().GetResult();
            result["mainThread"] = mainThread;
            result["workerThread"] = checks[0];
            result["workerDirectHolderPassed"] = checks[1];
            result["workerReflectedHolderPassed"] = checks[2];
            result["workerReflectedSongPassed"] = checks[3];
            Require((int)checks[0] != mainThread && (bool)checks[1] && (bool)checks[2] && (bool)checks[3],
                "Worker direct/reflected consumers changed clip identity.");

            music = Find.MusicManagerPlay;
            FieldInfo sourceField = typeof(MusicManagerPlay).GetField("audioSource", BindingFlags.Instance | BindingFlags.NonPublic)!;
            source = (AudioSource?)sourceField.GetValue(music);
            if (source == null)
            {
                music.MusicUpdate(); // Native initialization; ForcePlaySong assumes it already occurred.
                source = (AudioSource?)sourceField.GetValue(music);
            }
            Require(source != null && Prefs.VolumeMaster == 0f, "Native music source or existing mute is unavailable.");
            playbackAttempted = true;
            music.ForcePlaySong(song, false);
            float duration = music.SongDuration;
            result["songDuration"] = duration;
            result["clipDuration"] = clip.length;
            result["nativeCurrentSongSame"] = ReferenceEquals(music.CurrentSong, song);
            result["nativeSourceClipSame"] = ReferenceEquals(source!.clip, clip);
            result["nativeMusicPassed"] = music.IsPlaying && (bool)result["nativeCurrentSongSame"]
                && (bool)result["nativeSourceClipSame"] && duration > 0f && duration == clip.length;
            Require((bool)result["nativeMusicPassed"], "Native music manager did not retain the actual song/clip/duration.");
        }
        catch (Exception error) { result["failure"] = error.ToString(); }
        finally
        {
            try
            {
                if (playbackAttempted && music != null)
                {
                    music.Stop();
                    // Stop leaves clip assigned in the pinned native method.
                    // Release only this helper's source reference before the
                    // caller exercises its original holder disposal.
                    if (source != null && ReferenceEquals(source.clip, clip)) source.clip = null;
                    result["nativeStopPassed"] = !music.IsPlaying && source != null && !source.isPlaying
                        && !ReferenceEquals(source.clip, clip);
                }
            }
            catch (Exception error) { result["failure"] = result["failure"] + "\nNative stop: " + error; }
            if (song != null) song.clip = null!;
            if (added && observerHolder != null)
            {
                // Do not clear the holder or touch its unrelated native owners.
                if (observerHolder.contentList.TryGetValue(temporaryKey, out AudioClip current) && ReferenceEquals(current, clip))
                    observerHolder.contentList.Remove(temporaryKey);
                result["temporaryEntryRemoved"] = !observerHolder.contentList.ContainsKey(temporaryKey);
            }
        }
        result["passed"] = ((string)result["failure"]).Length == 0 && (bool)result["nativeMusicPassed"]
            && (bool)result["nativeStopPassed"] && (bool)result["temporaryEntryRemoved"];
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
