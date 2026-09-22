// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WakeUp;

internal static partial class PngRuntime
{
    private static TextureReadSession? textureReadSession;

    // One queue owns the complete speculative budget, including C06 cold jobs.
    // A callback closes every speculative handle and retires cache-dependent
    // work. Successful cold pixels keep their bounded reservation until the
    // next ordered take revalidates the complete current source contents.
    private sealed class TextureReadSession : IDisposable
    {
        private readonly KeyValuePair<string, FileInfo>[] selected;
        private readonly string selection;
        private readonly PreparedTextureStore? store;
        private FirstBuildImageQueue<FirstBuildImageData>? queue;
        private int index = -1, requested = -1;
        private string platform = "";
        private NativePlatformState? platformState;
        private bool disposed;
        private readonly bool allowQueue;
        private long warmScheduled, warmStarted, warmConsumed, warmRefused;
        private long ownedUploads, arrayUploads;
        private readonly ThreadLocal<GroupedTextureBlocks.ReadContext> readContexts =
            new(() => new GroupedTextureBlocks.ReadContext(), true);
        private const long ContextReservation = 2L * GroupedTextureBlocks.ReadContext.ReservedBytes;
        private long ownerTakeTicks, ownerRestoreTicks, guardedYields, suspendedYields;

        internal TextureReadSession(KeyValuePair<string, FileInfo>[] selected, string selection, bool allowQueue = true)
        {
            this.selected = selected; this.selection = selection; this.allowQueue = allowQueue;
            if (cache != null || PreparedTextureRuntime.UseAtStartup)
            {
                store = PreparedTextureRuntime.StartupStore;
                store.BeforeMutation += Suspend;
            }
        }
        private bool Allowed => Active && Compatible() && firstBuildReloadAllowed
            && firstBuildHolderGuard?.AllowsOriginalContract() == true;
        private bool WarmAllowed => Allowed && QualityUnityCallbacks.WarmArrayAllowed;
        private bool WarmSelected(FileInfo file) => store != null && CacheLaunchPolicy.Current.AllowRead
            && (IsCacheableImageSource(file.Name)
                || PreparedTextureRuntime.PsdSupport && file.Extension.Equals(".psd", StringComparison.OrdinalIgnoreCase));
        private bool ColdSelected(FileInfo file) => (IsCacheableImageSource(file.Name)
                || PreparedTextureRuntime.PsdSupport && file.Extension.Equals(".psd", StringComparison.OrdinalIgnoreCase))
            && FirstBuildCompatible();
        private FirstBuildImageQueue<FirstBuildImageData>.WorkItem? PlanWarm(int slot)
        {
            var pair = selected[slot];
            if (platform.Length == 0 || !WarmSelected(pair.Value)) return null;
            string owner = PreparedTextureRuntime.OwnerIdentity(selection, pair.Key, 0);
            var read = store!.PlanNative(owner);
            if (read == null) return null;
            if (!WarmSourceSize.TryGet(pair.Value, out long sourceBytes)) return null;
            var identity = PreparedTextureRuntime.NativeIdentityReader(owner, selection, pair.Key, pair.Value.FullName, platform);
            bool ownedRecord = WarmTextureUploadContract.Allowed;
            warmScheduled++;
            return new FirstBuildImageQueue<FirstBuildImageData>.WorkItem(ownedRecord ? read.OwnedRecordLiveBytes : read.ExtraLiveBytes,
                (source, cancellation) =>
                {
                    Interlocked.Increment(ref warmStarted);
                    string fullIdentity = identity(source);
                    return new FirstBuildImageData { WarmPlan = read, WarmIdentity = fullIdentity,
                        WarmOutcome = read.Read(fullIdentity, cancellation, readContexts.Value, ownedRecord) };
                }, expectedSourceBytes: sourceBytes, retainAcrossCallbacks: false);
        }
        private FirstBuildImageQueue<FirstBuildImageData>.WorkItem? PlanCold(int slot, Stream input)
        {
            if (!ColdSelected(selected[slot].Value)) return null;
            long required = RawPixelTrial.Estimate(input);
            return required > FirstBuildJpegDecoder.MaximumWorkingBytes ? null
                : new FirstBuildImageQueue<FirstBuildImageData>.WorkItem(required, RawPixelTrial.Decode);
        }
        internal FirstBuildImageQueue<FirstBuildImageData>.Lease? Take(int current)
        {
            long started = Stopwatch.GetTimestamp();
            try { return TakeCore(current); }
            finally { ownerTakeTicks += Stopwatch.GetTimestamp() - started; }
        }
        private FirstBuildImageQueue<FirstBuildImageData>.Lease? TakeCore(int current)
        {
            index = current;
            if (!allowQueue) return null; // Nested native work never acquires a second speculative budget.
            if (queue == null)
            {
                var file = selected[index].Value;
                // Ordinary DDS loading must not pay for idle worker creation.
                bool present = WarmSelected(file) && store!.HasOwner(PreparedTextureRuntime.OwnerIdentity(selection, selected[index].Key, 0));
                if (!present && !ColdSelected(file)) return null;
            }
            if (!Allowed) { Suspend(); return null; }
            if (queue?.IsSuspended == true && platformState != null && !LivePlatformMatches()) Drain();
            if (platform.Length == 0 && store != null && WarmAllowed)
            {
                try { platformState = new NativePlatformState(); platform = platformState.Identity; }
                catch (Exception e) when (e is InvalidOperationException || e is NotSupportedException)
                { platformState = null; platform = ""; }
            }
            if (queue == null)
            {
                queue = new FirstBuildImageQueue<FirstBuildImageData>(selected.Select(p => (FileInfo?)p.Value).ToArray(),
                    RawPixelTrial.Estimate, RawPixelTrial.Decode, maxDecodedBytes: 192L * 1024 * 1024,
                    maxReservedBytes: 192L * 1024 * 1024 - ContextReservation - (RawPixelTrial.Selected ? RawPixelTrial.Reservation : 0),
                    plan: PlanCold, startIndex: index, unopenedPlan: PlanWarm,
                    barriers: selected.Select(p => p.Value.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase)
                        && !WarmSelected(p.Value)).ToArray());
            }
            requested = index;
            if (!queue.TryTake(index, out var lease)) { Suspend(); return null; }
            if (lease!.Error != null || !Allowed || (lease.Value?.WarmPlan == null && !FirstBuildCompatible()))
            { lease?.Dispose(); Suspend(); return null; }
            return lease;
        }
        internal Texture2D? RestoreWarm(FirstBuildImageQueue<FirstBuildImageData>.Lease? lease)
        {
            long started = Stopwatch.GetTimestamp();
            try { return RestoreWarmCore(lease); }
            finally { ownerRestoreTicks += Stopwatch.GetTimestamp() - started; }
        }
        private Texture2D? RestoreWarmCore(FirstBuildImageQueue<FirstBuildImageData>.Lease? lease)
        {
            var data = lease?.Value;
            if (data?.WarmPlan == null) return null;
            // Live engine/settings state and patch publication are owner-only.
            // Refusal releases every speculative source before serial fallback.
            if (!WarmAllowed || !LivePlatformMatches())
            { warmRefused++; Suspend(); return null; }
            bool hasRecord = data.WarmOutcome!.OwnedRecord != null;
            // A changed pointer callback cannot run while speculative sources
            // remain leased. Drain before the established serial array fallback.
            if (hasRecord && !WarmTextureUploadContract.Allowed)
            { warmRefused++; Suspend(); return null; }
            var record = hasRecord ? store!.CompleteOwnedRead(data.WarmPlan, data.WarmOutcome) : null;
            var entry = hasRecord ? null : store!.CompleteRead(data.WarmPlan, data.WarmOutcome);
            if (entry == null && record == null) { warmRefused++; Suspend(); return null; }
            try
            {
                var texture = record != null ? RestoreOwned(record) : Restore(entry!);
                if (record != null) ownedUploads++; else arrayUploads++;
                texture.name = Path.GetFileNameWithoutExtension(selected[index].Value.Name);
                lease!.MarkConsumed(); warmConsumed++;
                if (PreparedTextureRuntime.UseAtStartup)
                { PreparedTextureRuntime.Hits++; PreparedTextureRuntime.Status = "Native prepared output used"; }
                if (IsCacheableImageSource(selected[index].Value.Name) && (mode == "verify-cache" || mode == "verify-first-build"))
                { Suspend(); VerifyImageResult(ToVirtualFile(selected[index].Value), texture); }
                return texture;
            }
            catch
            {
                warmRefused++; store!.Hits--; store.Misses++; store.Errors++;
                Suspend();
                store.Invalidate(data.WarmIdentity!);
                return null;
            }
        }
        private bool LivePlatformMatches()
        {
            try { return platformState?.MatchesLive() == true; }
            catch (Exception e) when (e is InvalidOperationException || e is NotSupportedException) { return false; }
        }
        internal void BeforeCallback()
        { if (queue != null && !Allowed) Suspend(); }
        internal void BeforeYield(PngConsumerContract? consumer, string key)
        {
            if (queue == null) return;
            if (Allowed && consumer?.Allows(key) == true) guardedYields++;
            else { suspendedYields++; Suspend(); }
        }
        internal void RetireForNested()
        {
            if (disposed) return;
            if (requested >= 0) queue?.RetireSpeculationAfter(requested);
            foreach (var context in readContexts.Values) context.Clear();
            platform = ""; platformState = null;
        }
        internal void Suspend()
        {
            if (disposed) return;
            if (requested >= 0) queue?.SuspendAfter(requested);
            foreach (var context in readContexts.Values) context.Clear();
        }
        internal void Drain()
        {
            if (disposed) return;
            if (requested >= 0) queue?.DrainAfter(requested);
            foreach (var context in readContexts.Values) context.Clear();
            platform = ""; platformState = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (store != null) store.BeforeMutation -= Suspend;
            queue?.Dispose();
            foreach (var context in readContexts.Values) context.Dispose();
            readContexts.Dispose();
            if (queue == null) return;
            ReportFirstBuild(queue);
            Write("warm-read-queue", "\"planned\":" + warmScheduled + ",\"readJobs\":" + warmStarted + ",\"consumed\":" + warmConsumed
                + ",\"ownedUploads\":" + ownedUploads + ",\"arrayUploads\":" + arrayUploads
                + ",\"refused\":" + warmRefused + ",\"guardedYields\":" + guardedYields + ",\"suspendedYields\":" + suspendedYields
                + ",\"maxActiveWorkers\":" + queue.MaxActiveWorkers
                + ",\"maxReservedBytes\":" + (queue.MaxReservedBytes + ContextReservation + (RawPixelTrial.Selected ? RawPixelTrial.Reservation : 0)) + ",\"heldBytes\":" + queue.HeldBytes
                + ",\"workerOpenMs\":" + (queue.WorkerOpenTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"workerReadMs\":" + (queue.WorkerReadTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"workerDecodeMs\":" + (queue.WorkerDecodeTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"ownerPreparationMs\":" + (queue.OwnerPreparationTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"ownerTakeMs\":" + (ownerTakeTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"ownerRestoreMs\":" + (ownerRestoreTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
