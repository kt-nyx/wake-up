# Retained features and missing capabilities

This is the scope of the combined R1–R5 offline candidate, not a full replacement
for other loading mods or a claim that its new behavior has passed in-game tests.
The older deployed candidate's accepted results are preserved in the
[historical retained-scope report](../retained-features-pre-campaign.md).
Exact new package identities and checks belong to the [R5 report](r5-combined-offline-review.md).

## Defaults, dependencies and storage

Required Prepatcher remains. Gagarin, Character Editor, Loading Progress and
Giddy-Up integrations are optional; a missing or unsupported supplier does not
become a required dependency. Existing definition/template/type searches and
ordinary established supplier defaults remain. Master-disable wins ordinary
activation; explicit development controls remain a separate selection path.

Translation application, automatic PNG/JPEG reuse, prepared-texture use,
texture-supplier routing, streamed audio, streaming XML input, display/diagnostics,
background loading, native-summary hiding and per-mod timings default off.
Texture preparation is a deliberate menu action, never an install-time job.
Windows DDS exports require a suitable-color acknowledgement; full-size export
can also change pixels. Exports are files for a separately reviewed consumer,
not automatically substituted game textures. Native preparation preserves quality.

Automatic PNG/JPEG storage is capped at 512 MiB. Native prepared entries and
DDS exports share another 512 MiB; there is no third allowance. Complete entries
are at most 8 MiB. Native prepared publication/consumption retires only matching
automatic entries after success. Central maintenance does not enable features.

## Every original goal and its disposition

“Deferred” means missing, not proved impossible. Historical live evidence belongs
only to its recorded package. The table consolidates the R4c coverage assessment.

| Original goal | Current source and offline evidence | Historical live evidence / remaining scope |
|---|---|---|
| 1: independent XML reuse | R1/R4b replay experiments rejected and test-only. R4a optional streaming input preserves native workers/order. | Established query searches/Gagarin assistance qualified on older packages; independent parsed/processed/inheritance caches **missing**. [R4a](r4a-ordered-reads-types.md), [R4b](r4b-retained-xml.md). |
| 2: translation loading | S03 faster application retained; revised parsed-language attempt stopped here. | Older French application evidence in [overnight report](overnight-qualification.md); persistent parsed-language/input change **missing**. |
| 3: faithful textures | PNG/JPEG native output reuse; R2 broad native preparation/export workflow offline. | Older rendered PNG/JPEG/native-prepared checks retained; first-build, grouped/compressed stores, new formats and complete atlases **missing**. [S07](s07-texture-coverage.md), [R2](r2-texture-preparation.md). |
| 4: cache ownership | Existing integrity, quota, interrupted-write recovery and bypass/rebuild/clear retained. Automatic 512 MiB plus prepared/export 512 MiB. | Historical maintenance evidence remains bounded; no new language/XML category. [S01](s01-cache-maintenance.md). |
| 5: on-demand assets | R1 routing guard/status fix; R3 default-off filesystem streamed-audio deferral with compiled consumer lifecycle, offline only. | Older texture routing qualified. General texture deferral, Resources/bundle routes, progressive DDS **missing**. R3 arbitrary reflection/generated and worker first-use limitations remain. |
| 6: preparation/quality | R2 native batches plus Windows full/half/quarter DDS exports; exports do not automatically substitute in-game. | Older narrow native capture/consumption qualified. Broad quality substitution/full converter parity **missing**; Linux helper owner-deferred. |
| 7: code searches | Existing Harmony type/leaf and definition/template search improvements. R4a confirmed native GenTypes/construction delegates already cache. | Historical narrow calls qualified; no duplicate native memo or broad reflection parity. [S02](s02-code-searches.md), [R4a](r4a-ordered-reads-types.md). |
| 8: loading display | R4c native-summary choice, bounded XML report/attribution and richer diagnostic status; offline only. | Older basic panel live evidence. Full profiler/progression/error inventory/constructor scheduling **missing**; previews already lazy. |
| 9: background loading | R4c ordinary new-colony lifetime added and checked offline; save/world preserved. | Older save/world minimized evidence remains. New colony needs actual focus/pause/gameplay checks; standalone/pocket maps, initial autostart and world-map opening **missing**. |
| S14: warnings | Existing exact duplicate-product conflicts; feature-local refusal/status. No new metadata or launch dialog justified. | No automatic disabling/reordering; required Prepatcher **not replaced**. Supplier overlaps do not establish removability. [S14](s14-conflict-warnings.md). |
| Recovered BetterLoading ideas | Current game already parallel-reads XML within a mod and caches native type results; R4a streams input, R4c adds a partial XML report and ordinary colony background lifetime. | Cross-mod scheduling, detailed save progress, constructor timing and general error/progress replacement **missing**. Existing Loading Progress repaint cooperation remains. |


## Experimental limits and rejected work

R3 streamed audio supports a bounded compiled consumer/holder family and ordinary
main-thread first use. Arbitrary reflection/generated reads can bypass it;
worker first use is unsupported, and a read before a requested main-thread drain
can observe an unassigned song field. Default-off experimental acceptance does
not establish universal safety, playback quality or release qualification.

R4a streaming XML changes input storage while retaining native workers, order
and fallback; it does not reuse completed XML work. R1 whole-prefix replay and
R4b retained-node replay lost their complete CPU comparisons and remain test-only
experiments. The R4c revised language route lost half its pairs and was not
integrated. Native GenTypes and construction delegates already cache their work;
no duplicate memo was added. Do not rerun these routes unchanged.

No Linux helper, general deferred/progressive textures, grouped stores, complete
atlases, broad automatic image-format/first-build replacement, full profiler,
standalone/pocket-map or general background-event coverage is delivered.
Overlapping supplier features do not justify removal recommendations. Existing
duplicate-copy metadata and feature-local refusal remain the warning policy.
