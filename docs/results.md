# Results and decisions

The retained product contains two shared search improvements and five optional
integrations or texture improvements. The investigations support delivering
these features and qualifying Steam compatibility; they do not establish a
universal loading-time reduction or a technical ceiling for future work.

Use [current state](current-state.md) for source/build/deployment identities and
[the support policy](user-guide.md#supported-build-policy) for actual supported
environments. Normal settings enable established features; PNG caching remains
off by default. An optional feature still needs a present, supported supplier
and eligible work before it can help.

## Retained work

| Feature | What it avoids | Evidence and limits |
|---|---|---|
| XML definition and named-template searches | Repeated scans for a literal `defName` or template `Name` during supported vanilla patch operations | [Search comparisons](feature-benchmark-results.md#which-retained-features-produce-the-saving); benefits depend on patch workload and whether another cache skips patching |
| Harmony type searches | Repeated enumeration of loaded types in the fallback search | [Search comparisons](feature-benchmark-results.md#which-retained-features-produce-the-saving); index ends at the established startup cutoff |
| Gagarin parsed-XML reuse | A second parse of XML already available from the installed supplier | [Qualification](archive/gagarin-cache-loading.md); exact optional supplier checks, with ordinary supplier behavior retained around unsupported Loading Progress hooks |
| Character Editor preset lookup reuse | Repeated construction of the same read-only turret lookup during startup preset creation | [Reintegration qualification](character-editor-reintegration.md); 25.40% incremental reduction in its tested patch-heavy comparison, not an all-modlist average |
| Loading Progress repaint coalescing | Additional stage-change pauses within the supplier's existing frame budget | [Qualification](five-loading-opportunities.md#loading-progress-results-and-decision); roughly 8% incremental reduction on the recorded industrial workload |
| Giddy-Up texture readback | Reading a full converted texture when the offset calculation only needs its center column | [Qualification](further-loading-improvements.md); final matched gains of 0.45–0.51 seconds on the small tested workload |
| Processed-PNG cache | Reprocessing eligible PNG texture data on later launches | [PNG comparisons](archive/investigation-results.md#2026-09-07-png-processing-implementations); opt-in, first construction costs time, and normal DDS selection may leave no eligible PNG work |

These comparisons use different workloads, feature combinations and cache
conditions. Do not add their percentages or apply an older combined result to
a newer package. Fresh application caches do not imply a cold Windows file
cache. Use the actual menu-ready endpoint and matched forward/reverse controls
when making a loading-time claim.

## Routes closed under their tested conditions

The [consolidated investigation log](archive/investigation-results.md) preserves
the measurements, invalid runs and rejected implementations. Its section dates
matter: earlier scope restrictions and proposed next steps can be superseded
by later decisions.

| Area | Disposition |
|---|---|
| More XML workers, worker reuse, private inheritance preparation, cross-reference batching and scalar conversion preparation | Real work or overlap did not translate into repeatable useful complete-startup savings; prototypes removed |
| XML query splitting/alternatives, an indexed cursor, metadata catalogs and static type-index segment reuse | No useful repeatable gain justified the additional implementation |
| Warm DDS/PNG read-ahead and managed/native-hash DDS packs | Faster inner operations or cheaper hashing did not produce a qualified repeatable startup improvement |
| Streaming Gagarin cache writer and typed Harmony instruction emission | Preparation/installation costs erased or outweighed the proposed benefit |
| Atlas layout, reflective field stores and definition registration replacement | Measured removable work did not justify a replacement; overlapping timers were not an available wall-time budget |
| Combat Extended XML hook | Final reverse control erased practical benefit; removed |
| HugsLib obsolete-report filtering | Latest bounded original report cost was 181 ms; [prototype removed](product-activation-and-final-opportunities.md#hugslib-decision) |

Reopen a route only when a materially different premise addresses its recorded
failure. Invalid or inconclusive runs do not disprove an entire technique.

## Remaining questions

The [final combined-build assessment](product-activation-and-final-opportunities.md#remaining-loading-cost-on-the-combined-build)
found no eligible late type-search calls and did not justify another general
replacement. The large variation in Giddy-Up graphics work needs a repeatable
cause before it becomes an optimization proposal. Naturally PNG-heavy and
first-read workloads remain narrower applicability questions, not release blockers.

The current priority is [release preparation](release-preparation.md), including
Steam qualification. Historical package checkpoints are [archived separately](archive/development-checkpoints.md).
