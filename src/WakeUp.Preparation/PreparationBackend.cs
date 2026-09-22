// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WakeUp.Preparation;

public enum PreparationAction { Prepare, ResetSelection, ClearOwned, Rebuild, BypassOnce }
public sealed record PreparationBatchRequest(DiscoveredLoadout Loadout, string SaveDataRoot,
    IReadOnlyList<DiscoveredTexture> Selected, PreparationOptions Options, long SharedCeilingBytes,
    PreparationAction Action, bool PsdEnabled = false, PreparationBatchPreview? ApprovedPreview = null);
public sealed record PreparationBatchProgress(int Completed, int Total, string SourcePath, string Result,
    long StoredBytes = 0, long TextureBytes = 0);
public sealed record PreparationBatchResult(int Prepared, int Reused, int Skipped, int Failed,
    long StoredBytes, long TextureBytes, string Summary);

// The storage/helper adapter implements this surface. Discovery and form logic
// never write into artwork, ModsConfig, supplier caches, or game installation.
public interface IPreparationBackend
{
    Task<PreparationBatchPreview> PreviewAsync(PreparationBatchRequest request,
        System.IProgress<PreparationBatchProgress> progress, CancellationToken cancellation);
    Task<PreparationBatchResult> ExecuteAsync(PreparationBatchRequest request,
        System.IProgress<PreparationBatchProgress> progress, CancellationToken cancellation);
}
