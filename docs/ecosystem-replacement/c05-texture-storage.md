# C05 broad faithful texture storage

## Independent metadata review; prepared-read correction next — 13 September UTC

Reviewed clean stopped `db26c9c64dd24e10807c50ca0286d681e5bdc1ab`, tested
`a782b92e80bce8e01685ac5ec63298d6c353fea0`. The metadata change passes independent
scoped source review: sizes remain reservation hints, actual opened length is
checked before allocation and full source hashing remains. C06 lifetimes and
quality/callback contracts are unchanged. The steward verified all 15 capture
manifests, 92 indexed captured hashes, 12 supporting artifacts, both DLLs, all 19
rollback receipts, seven baseline hashes, ten absences and retained repair
hash/size/time. Checkout and operations returned clean/stopped. Independent
verification is `c05/metadata-admission-20260913/steward-review.json` under the
ignored campaign artifact directory; no new tests or launches were needed.

Conditional PNG startup benefit of 2.86–3.48 seconds is supported in the matched
scenario; construction remains a separate 33.53-second cost. **DDS performance
fails and C05 is not fully accepted.** Compressed reads each record once but is
still slower. Prepared loading repeats 10,584/10,670 reads for 2,840 uploads,
with 4.96/5.02-second texture stages against native 0.78/0.74 seconds.

Independent source review confirms the inherited C06 boundary suspends before
`PreparedQualityRuntime.Loaded` discovers that no explicit-quality collection
is needed. Return a bounded correction to original C06: evaluate the existing
data-only no-op predicates inside `Loaded`, then suspend outside any store lock
immediately before real holder/content capture. Do not cache absence across
callbacks, weaken quality checks or preserve warm results across real mutation.
The safe no-op change does not prove the compressed DDS gap solved. C05 remains
queued for that obligation after responsible-owner correction and review.

## Advisory metadata correction: correct, DDS requirement still open — 13 September UTC

Warm planning now reuses file sizes already obtained by native directory
selection instead of forcing another file-system query for every planned image.
Correctness passes and the conditional PNG warm benefit remains. **The bounded
DDS comparison still fails the loading-performance requirement.** Prepared-only
loading also exposes repeated warm reads in the inherited callback-suspension
path. That separate lifecycle issue is recorded below for steward review; it was
not changed or waived in this metadata assignment.

### Exact source and mechanism

Assignment `wake-up-c05-metadata-admission-20260913` began at exact clean
`6576975e37388578abe68609bb9e843a1686b1e4` on the preserved review branch,
including independently reviewed C06 product `39178efb`. The final tested
product and matching observer source are
`a782b92e80bce8e01685ac5ec63298d6c353fea0`.
Core DLL SHA-256 is
`22df846518ea682d2d3a9bc8327043554756e71a3d475266788d8c9a72559891`;
observer DLL SHA-256 is
`c018d15f69594b8b444dd5c9981c7e00de0522118b35bcb628ec1f0269d4e07a`.
These tested packages are distinct from the restored baseline DLLs.

Local read-only GOG/Mono IL inspection established the complete path:
`GetAllFilesForMod` calls `DirectoryInfo.GetFiles`; enumeration initializes
`FileInfo` with the directory entry's size, and `SelectedFiles` preserves those
same objects. `Length` reads the cached size unless the object is uninitialized.
The removed explicit `Refresh` always queried file-system attributes again.
Evidence, exact Assembly-CSharp/mscorlib hashes and extract line references are
in `metadata-admission-20260913/findings.md` under the C05 artifact directory.

`WarmSourceSize.TryGet` uses the public length getter and refuses missing,
inaccessible or unsupported metadata per file. A new/uninitialized provider
`FileInfo` retains the getter's ordinary metadata query; this does not claim
that every provider avoids IO. No new metadata pass, moved scan, private runtime
layout access, dependency or default change was added.

Sizes remain advisory reservations, never freshness evidence. The unchanged
worker opens its source with the existing lease, checks actual length **before
allocation/read**, and then hashes complete source contents. Shorter, longer,
missing and same-size/same-time replacements cannot establish a false cache hit
or undercharge allocation. Cached hints do not become authority after callbacks
or generation changes. Source identity, block/entry digests, descriptors,
platform/publication checks and synchronous pinned-upload lifetime remain intact.
Native selection/order/DDS precedence and C06's stream/header estimator, retained
cold-result revalidation, callback suspension, warm retirement, actual-upload
receipts, nested-queue exclusion and shared budget are unchanged.

### Final-source correctness

Independent scoped source review found no actionable issue. The final build had
zero warnings, and **187 focused tests passed with none skipped**. They cover
cached enumeration hints after file mutation/deletion, provider-created and
unsupported metadata, changed length before allocation, same-size/time content
changes, fallback, shared queue/accounting/lifecycle, authenticated records and
quality storage. An initial test assertion incorrectly expected an IO exception
for changed-size rejection; it was corrected to the existing invalid-data
exception before the final pass. No runtime behavior was changed for that test.

All four final-source functional captures exited normally:

- `c05ma-native-fresh` and `c05ma-native-warm`: all **62 native samples**, both
  PSD negative controls, DDS unsupported-capture fallback, and **all four C06
  callback cases** passed. The late-callback case uses updated changed-source
  pixels and reuses unchanged retained pixels with zero workers/handles during
  the callback. The warm prepared-plus-automatic capture consumed 2,894 owned
  uploads, including 54 private probe records, with no array fallback uploads.
- `c05ma-combined-functional`: actual C05+C06 construction passed **76
  comparisons / 39 uncompressed**, with exactly **2,840 decodes, consumed results
  and cache writes**. The prior C06 repeated-decode correction remains intact.
- `c05ma-png-warm-functional`: C06-disabled warm reuse of that cache passed
  **76 / 39**, consumed 2,840 owned uploads and wrote no duplicate output.

All 15 functional/performance captures have normal automatic exit and zero
texture/cache errors or mismatches. Queue held bytes return to zero and active
workers remain at most two. Maximum reservation across all checks was
59,249,623 bytes, within the shared two-worker/eight-job/192-MiB limit including
contexts, source, record, scratch, waiting and current upload allocations.
Functional durations are diagnostic. C08 appearance/reset/restart remains a C15
obligation; no ordinary-gameplay or owner-assisted visual inspection is claimed.

### One bounded forward/reverse comparison

The active `owner-unattended-20260913-044927` grant was rechecked before runs.
Measurements used serial ownership, stopped game processes, paused substantial
agent/build work, the same 13 frozen/15 active order, muted profiles, no probes
and independent observer-driven normal exit. Generation remains
`gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
C06 was off in these performance arms. These are repeated starts under warmed
Windows file-cache conditions, not OS-cold measurements.

DDS order was native A → compressed A → prepared A → prepared B → compressed B
→ native B. All four warm arms independently imported `c05ma-native-fresh`,
run-manifest SHA-256
`509eed4600f4179f817334c30e1549946b1f22a68c11b9b9ffa25eb12f5dbe1c`.
Prepared loading consumes the same automatic native output; no new manual
preparation producer is claimed.

| Arm (`c05ma-dds-*`) | Menu A / B, seconds | Texture stage A / B, seconds |
| --- | --- | --- |
| Native | 16.903927 / 16.801797 | 0.777917 / 0.743275 |
| Compressed | 17.315383 / 17.668864 | 1.131047 / 1.099262 |
| Prepared | 21.515632 / 21.847478 | 4.961593 / 5.019811 |

All warm arms uploaded 2,840 authenticated records with zero array fallback and
zero refusals, but all lose to native loading. No slower samples were discarded
and no unchanged losing repetitions were run. Current compressed owner
preparation is 174.879 / 135.400 ms; the avoided query is established by source
inspection, not a same-base on/off measurement of its isolated time saving.
Historical cross-revision intervals must not be presented as that measurement.

PNG order was native A → separate empty-cache construction → warm A → warm B
→ native B. Both warm runs independently imported `c05ma-png-build`,
run-manifest SHA-256
`632b0f74d339e85d3aef57e68c6f75e5f1341766e41edbdc5cffeec716939860`.

| Arm (`c05ma-png-*`) | Menu A / B, seconds | Texture stage A / B, seconds |
| --- | --- | --- |
| Native | 20.502221 / 19.803691 | 3.947696 / 3.899994 |
| Warm | 17.023765 / 16.941768 | 0.925829 / 0.937458 |

The matched conditional whole-startup gain is **3.478456 / 2.861923 seconds** in
this fixture scenario, not a universal average or isolated metadata-change
speedup. Separate construction took **33.532316 seconds**, with 16.673692 seconds
in the texture stage. It produced 2,840 entries, 333,497,002 stored bytes and
332,443,410 payload bytes from 100,032,074 source bytes. Construction cost is not
counted as a warm-loading benefit.

### Concrete remaining cost and next design for review

Prepared-only loading plans 22,608 jobs in each measured run and actually reads
**10,584 / 10,670** records for 2,840 uploads; compressed loading reads each of
2,840 records once. Both routes report 2,840 guarded yields and no suspended
consumer yields, so the ordinary iterator consumer is not the recorded source
of this difference. Current source unconditionally calls `preparation.Suspend`
before `PreparedQualityRuntime.Loaded` whenever preparation is enabled.
`Loaded` then immediately returns when no explicit-quality owner exists.
The suspension was introduced in C06 lifecycle commit `59882e2e`; warm results
correctly retire under that policy and are repeatedly read after refill.
This source/receipt evidence identifies a separate lifecycle boundary to address;
it is not evidence that the metadata correction caused this inherited behavior.

A bounded next design is **data-only explicit-quality admission before
suspension**. Check the existing quality owner/presence and already-collected
slot first; when no work exists, retain the warm horizon. Suspend immediately
before actual holder/content-list capture or other callback-capable work.
Keep full C08 quality ownership checks, callback/generation retirement, source
validation, native ordering and nested/current-upload accounting. This requires
focused absent/present quality and changed-callback checks plus real comparisons;
it is proposed, not implemented or accepted here.

That would address the repeated prepared reads, not prove the remaining
compressed DDS gap solved. Compressed worker open/read/identity-group processing
sums were 532.013/244.549/1,224.239 ms in A and
496.994/233.994/1,183.258 ms in B; owner take, including waits, was
597.973/538.714 ms. Worker intervals overlap each other and owner work and must
not be added to texture or startup time. No new profiling framework, codec,
dependency, default, scope exclusion or disk-only acceptance waiver follows.

### Restored, stopped handoff

All **19 owned transactions** were rolled back. Seven baseline hashes, ten
absences and retained repair `20260912-170800-04578930` match the independently
verified C06 baseline; the repair hash/size/timestamp remain unchanged. No game,
helper, build process or child task remains active. Only C05 source/tests and
this report changed; parent plan/state/ledger files were not edited. No normal
data, Steam/Deck, UI, merge, push, publication or successor dispatch occurred.

Private evidence is in
`artifacts/ecosystem-next-20260910/c05/metadata-admission-20260913/`:
`findings.md` and local IL extracts, `source-review.json`,
`focused-tests-r2.log`, matching build/deployment receipts,
`functional-validation.json`, `all-results.json`, `validation-summary.json`
(92 indexed captured-file hashes), `restoration.json`, and `handoff.json`.
Actual captures remain `.rlo-test-instance/results/c05ma-*`.
C05 returns **stopped for independent steward review, not fully accepted**.
C06's remaining performance/coverage stays queued to its original owner; C14/C15
and deferred C10/C11/companion scope are unchanged.


## Independent buffer review; serial C06 qualification next — 13 September UTC

Reviewed clean stopped `96a60e14a2c340095a5eb4475d56f9caadbf775c`, tested
`72859aeb3e83a7bce287cfba2f4e93cf839c66b6`. Scoped independent source review
passes authenticated-range ownership, synchronous pin release, exact pointer
guards, array fallback, store completion and retained C06/C08 contracts.

The steward verified all 17 capture manifests, 80 selected captured files,
matching core/observer DLLs, supporting handoff artifact hashes, 21 rollback
receipts, seven baseline hashes, ten absences and retained repair hash/size/time.
No game/helper/build process remained. Final-source C06 actually consumed 2,840
first-build results with 76 native comparisons, including 39 uncompressed cases.
PNG warm benefit remains conditional at 2.17–2.51 seconds. **DDS still fails
performance; C05 is not fully accepted.** Independent evidence is
`artifacts/ecosystem-next-20260910/c05/authenticated-buffer-20260913/steward-review.json`.

The proposed metadata-admission correction is a credible future bounded route,
not implemented or approved as a performance success: obtain advisory size hints
from existing selection metadata, preserve worker actual-length admission before
allocation and full content validation, and use per-file native fallback for
missing or changed hints. Keep C06 stream/header estimation unchanged. Merely
moving the same metadata reads earlier does not remove their cost.

The steward will use the next serial slot for C06's outstanding first-build
performance qualification. C05 remains stopped and available for a later
correction; its DDS requirement and remaining coverage are not deferred from
the release or waived. No new codec/dependency/default/scope tradeoff is adopted.
The C14/C15 sequence and their actual correctness obligations remain open.

## Authenticated record upload: correct, DDS performance still open — 13 September UTC

Warm uploads can now use the pixel range inside a fully authenticated cache
record, avoiding a second full pixel array and copy. Final-source correctness
passes and the conditional PNG warm benefit remains. **This bounded correction
still does not demonstrate a reliable overall DDS loading gain; full C05
acceptance remains open.** No further optimization series was started.

### Source and upload contract

The exact product source is `72859aeb3e83a7bce287cfba2f4e93cf839c66b6`,
on the preserved `codex/ecosystem-next-plan-review` ancestry above reviewed
`b5960401fd425af9dadbc8d10755fad34fb6e093`. Matching GOG packages were built:

- Core DLL SHA-256: `0fa1a8c59def126eb2754e64ec7d63ae3255071b417df1f456295224f011893e`.
- Observer DLL SHA-256: `29d36a3d5511f0035564578835d992299f951fba688bb2eb53657d723361049e`.
- GOG generation: `gog-rev573-20260910-194017-cf1a1eb0`.
- Fixture manifest: `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.

Local read-only ILSpy inspection established that GOG's
`Texture2D.LoadRawTextureData(IntPtr, int)` calls the native
`LoadRawTextureDataImpl(IntPtr, ulong)` synchronously. The warm path keeps the
complete record alive, validates its descriptor and exact pixel range before
creating a pointer, and pins it only during this synchronous call. A `finally`
block releases the pin when upload succeeds or throws. Workers and waiting
results never retain pins. Ordinary `Decode`, serial/native consumers,
`RestoreQuality`, and atlas consumers retain their owned pixel arrays.

Admission pins the exact reviewed GOG build, full Unity CoreModule SHA-256,
managed wrapper bodies, native method signature, and unchanged callback
publications. The reachable readability/exception paths are guarded too.
Unsupported or changed pointer contracts keep the existing array route. If a
pointer contract changes after a record was queued, the queue drains before
serial array fallback. Prepared-startup admission uses this separate array
contract; C08 retains its original complete guard set and quality checks.

Full source-byte identity, every block/entry digest, descriptor/range/layout
validation, native format/channel/mip/readability preservation, platform checks,
ordered selection/DDS precedence, generations, leases, mutation drains,
oversized serial preparation, and independence from C06 remain intact. The
shared queue remains two workers, eight jobs, and 192 MiB including source,
scratch, waiting records and two stream contexts. Only the eliminated second
record-sized allowance was removed. No codec, dependency, setting/default,
quality or feature-scope change was made.

### Final-source verification

Independent source review passed after correcting the reference-returning store
completion helper and prepared-startup array admission. The final focused build
had zero warnings; **158 tests passed, none skipped**. Coverage includes exact
record ranges, corrupt identity/descriptors/blocks, pin release on success and
exception, stale/cancelled reads, array-versus-pointer callback changes, shared
cold/warm queue bounds and quality storage. Two earlier build attempts caught a
nullable annotation and an obsolete test delegate; both were corrected before
this committed source was packaged. These are one final test invocation, not
counts added across retries.

All six functional captures use the same final product and observer source:

- `c05ab-native-fresh`, `c05ab-native-warm`, and `c05ab-prepared-warm` each
  passed all **62 native samples**, both PSD negative controls, and unsupported
  DDS fallback. Warm automatic and prepared-plus-automatic runs each consumed
  **2,894 owned-record uploads**, with zero array fallback uploads; this includes
  54 private probe records alongside 2,840 natural entries.
- `c05ab-png-cold` and `c05ab-png-warm` each passed **76 comparisons**, including
  **39 uncompressed** cases. Warm loading consumed 2,840 owned-record uploads.
- `c05ab-first-build` is an actual **final-source C06** check: 76 comparisons,
  39 uncompressed, and 2,840 cold queue results consumed. It does not reuse the
  earlier `339e4d34` result as final-source evidence.

All 17 functional/performance captures exited normally, passed the independent
menu observer/automatic test, and recorded zero errors, cache errors or
mismatches. Queue drains returned held bytes to zero; at most two workers were
active. Maximum reservation was 59,900,019 bytes across all checks, including
cold C06 work. Each measured DDS warm run reserved at most 40,263,338 bytes and
actually consumed 2,840 owned uploads with zero array fallback uploads.
Functional timings are diagnostic only. Combined visible C08 scene/reset/restart
checks remain with C15; this work makes no gameplay or owner-inspection claim.

### Bounded matched performance result

Measurements used the current unattended grant
`owner-unattended-20260913-044927`, serial fixture ownership, no competing
builds/substantial agent work, the same 13 frozen/15 active mod order, muted
profiles, no probes and normal observer-driven exit. All game processes were
stopped before each measurement. These are warm Windows file-cache conditions,
not OS-cold claims. Worker timings overlap each other and owner intervals and
must not be added to startup or texture time.

DDS order was native A → compressed A → prepared A → prepared B → compressed B
→ native B. Every warm arm independently imported the same
`c05ab-native-fresh` artifact, whose run-manifest SHA-256 is
`a8e9ec7255dfc1440031ffc1417b3203ea57580b5a71fba43b53947a3eeeaff0`.
The broad prepared arm consumed automatic native output; this is not a new
manual-preparation producer claim.

| DDS arm (labels `c05ab-dds-*`) | Menu A / B, seconds | Texture stage A / B, seconds |
| --- | --- | --- |
| Native | 16.927698 / 16.146982 | 0.665709 / 0.635200 |
| Compressed | 16.779500 / 17.010337 | 1.339908 / 1.089464 |
| Prepared | 16.725013 / 17.092916 | 1.222865 / 1.421036 |

The apparent small forward-order menu gains reverse in the second order, while
the texture stage remains slower in all warm arms. This is **no reliable useful
DDS startup gain**. Eliminating an allocation is not acceptance evidence by
itself, and cross-revision historical timing differences are not a controlled
measurement of this copy removal alone.

PNG order was native A → separate empty-store construction → warm A → warm B
→ native B. Both warm runs independently imported `c05ab-png-build`, run-manifest
SHA-256 `bb8b9da3ab5e9e0b92f59ca1785d32ac1464c2179b18166238b28fb4accc1a3e`.

| PNG arm | Menu A / B, seconds | Texture stage A / B, seconds |
| --- | --- | --- |
| Native (`c05ab-png-native-a/b`) | 19.433032 / 19.494628 | 3.952695 / 3.946054 |
| Warm (`c05ab-png-warm-a/b`) | 16.924847 / 17.329589 | 0.975022 / 1.350873 |

The matched PNG whole-startup benefit is **2.508185 / 2.165039 seconds** in this
specific scenario, not a universal average. Separate cache construction took
31.760761 seconds, including a 15.906084-second texture stage; it produced
2,840 entries, 333,497,002 stored bytes, and 332,443,410 payload bytes from
100,032,074 source bytes. Construction is not a warm loading benefit.

### Remaining design and stopped handoff

The remaining cost is not demonstrated to be pixel copying. Summed overlapping
DDS worker identity/group/record processing remained 1,256.736–1,565.693 ms;
owner preparation was 353.836–581.319 ms and owner take (including waits) was
639.604–865.127 ms. These scopes do not identify pure CPU cost or separately
attribute metadata reads.

A concrete next design for steward review is to remove repeated main-thread
file-metadata admission from the warm queue: capture immutable size hints during
selection, then let the existing worker open validate the actual length before
allocation and full source hashing. Current `PlanWarm` explicitly refreshes each
file on the owner thread even though worker open already checks actual length.
Keep changed-size refusal/serial fallback, all byte/hash validation and shared
reservations. This targets an observed owner interval and an actual duplicate
metadata boundary; the fraction attributable to that boundary and any useful DDS
gain remain unmeasured. This is a proposal, not implemented or accepted work,
and does not waive the DDS requirement or authorize an open-ended series.

All **21 own fixture transactions** were rolled back in reverse order. Seven
baseline hashes, ten absences and the retained repair transaction
`20260912-170800-04578930` match; the repair's hash, size and timestamp remain
unchanged. No game, helper or build process remains. The restore helper first
refused before mutation because its cutoff used UTC against local-time transaction
names; the cutoff was corrected to the exact first task deployment receipt and
all rollbacks then passed. Normal data, Steam/Deck, UI, merge, push and publication
were untouched.

Raw private evidence is under
`artifacts/ecosystem-next-20260910/c05/authenticated-buffer-20260913/`:
`source-review.json`, final `focused-tests-r3.log`, build/deployment receipts,
`functional-validation.json`, `all-results.json`, `validation-summary.json`,
`restoration.json`, and `handoff.json`. Captured runs remain at
`.rlo-test-instance/results/c05ab-*`. The C05 task is **stopped for independent
steward review**, with DDS performance still open. No successor was dispatched.


## Independent parallel-read review; DDS correction remains open — 13 September UTC

The steward reviewed clean stopped `6189941febaec18019ef6ee91a32fc64a0d61d8a`,
tested product `5bda42762bfd8d4b19519b46a93203f84fe67876`. Independent source
reviews pass the bounded worker/store split, queue lifetimes, full validation,
guarded restoration and retained C06/C08 integration. No blocking source defect
was found. The new report prefix's mixed text encoding was corrected to UTF-8.

The steward checked all 29 capture manifests, 141 selected captured evidence
files, exact package identities, 37 rollback receipts, seven restored baseline
hashes, ten absences and the retained repair's hash/size/timestamp. No RimWorld,
helper or build process remained at review. Raw independent evidence is
`artifacts/ecosystem-next-20260910/c05/parallel-warm-20260913/steward-review.json`.
Matched order and independent seed reuse support the reported conditional PNG
warm benefit of 2.88–2.89 seconds. DDS still has a concrete texture-stage regression
and no reliable whole-startup benefit. **Full C05 acceptance remains open.**

The focused test receipts support 120 final store/queue checks, 20 later interval
checks, an earlier 35-check integration invocation, and 43 quality checks with
one external helper case skipped. These invocations overlap. The live C06
first-build result belongs to `339e4d34`; it is not a final-source live result.
Combined C08 appearance/reset behavior remains a consolidation obligation.

Next, test a materially different internal correction: consume a validated range
of the already-authenticated record buffer without making another full pixel
array. Guard the actual Unity upload path, keep its buffer alive through the
synchronous upload, and preserve existing owned-array consumers and fallback.
This targets duplicate allocation/copying, not worker-count or file-handle tuning.
Its contribution is unknown and may be insufficient to remove the remaining
DDS cost. Use a bounded actual comparison after correctness, and return the
result for review; neither a smaller allocation nor another failed experiment
closes the capability. See the active plan for boundaries.

## Parallel warm reads implemented; DDS performance remains open — 13 September UTC

Warm texture reading now overlaps source validation and stored-data decoding on
CPU workers, while the game creates textures in its original order. The corrected
path preserves the demonstrated PNG benefit. **It still does not demonstrate a
useful overall DDS loading gain; C05 remains unaccepted.** Three implementation
revisions were built and checked, including two concrete refinements after the
initial worker integration. This is a stopped handoff for independent steward
review, not a completed capability, release gate or platform promotion.

### Source and operational scope

This assignment began from clean reviewed
`ee8680e63992eab79dafdea1a4a65d6e71a9d069`, preserving the current campaign ancestry.
The implementation revisions are:

- `339e4d34d52d279ada04022492c7350f95041913`: immutable store read plans, independent
  worker reads, full content validation, ordered completion and the shared queue.
- `22b20316e10f24da83a0498f80d8576dc614b56b`: exact callback publication checks under
  one Harmony state lock, and captured platform keys with complete live input checks.
- `5bda42762bfd8d4b19519b46a93203f84fe67876`: warm source opening also moves to workers;
  each worker retains bounded private group streams until a drain boundary.

The final measured core SHA-256 is
`db89ca3d66079717e02fd6751578888e07264a68e4f60aec580e9ceff4987d72`;
the matching observer is
`89d0a5963bfa3a98ce5d31f8d77719640d94692d518a3853023a87ef04c326a6`.
Both use GOG generation `gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`, and the existing
`4a170804...` game assembly contract. Captured manifests retain every full identity.

There were **29 successful automatic captures: 12 functional and 17 performance**.
Each launch used `fixture.py`, verified its prepared inputs before launch, retained
exact 13 frozen / 15 active order and matching core/observer packages, waited for
normal exit and passed `automaticTestPassed`. Performance runs used the active
`owner-unattended-20260913-044927` grant, checked before every launch, with all
RimWorld instances already stopped and no competing builds, tests or substantial
agent work. Functional timings are diagnostic only. LoadingProgress and Gagarin
were excluded through recorded fixture transactions, alongside the existing three
exclusions. Settings, quality, dependencies, DDS precedence and native file
selection retain their existing contracts. C10, C11 and the companion remain deferred.
No Steam/Deck promotion, normal-data change, desktop automation, merge or publication
occurred.

### How the final implementation works

The game thread captures an immutable description of a cached record under the
store's lock. A worker opens the source, checks its actual size before allocation,
reads and hashes its full contents, then validates and decodes the identified
stored record. The game thread rechecks callback and platform state, completes
usage accounting and restores the texture in native order. A size hint only
reserves memory: same-size and same-timestamp source changes still fail the full
identity check. Worker failures are returned as data; workers do not invalidate
or publish cache entries, call Unity or mod callbacks, or mutate store accounting.

One queue handles warm reads and optional C06 cold image jobs. Across an active
interval it admits at most two workers, eight jobs and 192 MiB of speculative data.
Reservations include source bytes, compressed scratch, the decoded record, its
pixel copy and completed results awaiting consumption. The two private stream
contexts reserve 128 KiB each within that limit and retain at most eight handles
each. Oversized or ineligible jobs retain the existing serial preparation/fallback
path. Warm reuse does not depend on enabling C06.

Before native DDS fallback, failed reads, foreign holder callbacks, nested reload,
store mutation, pruning/admission, quality publication/reset or disposal, the owner
drains pending jobs outside store locks and closes their private group streams.
A nested reload drains outer speculative work before operating its inner queue.
Source leases survive successful ordered consumption; changed intervals acquire
new plans. C06 retains its original stream/header estimator and platform eligibility.
Current decoded C06 data survives the synchronous native continuation even when a
store mutation releases its queue lease; released leases are not falsely receipted.

The guard review also closed configurable-crypto callback paths in texture/quality
identity hashing and added the missing Unity format/platform entry points. Repeated
quality guard checks compare every immutable Harmony publication under one lock;
a changed or self-removed patch still refuses the original guarded interval.

### Correctness and actual worker use

The final store/queue check passed **120 focused tests**. Separate changed-guard and
integration checks passed 20 tests; retained preparation/quality checks passed 43,
with one external built-helper test skipped. The earlier fixture-tool test invocation
passed 75 checks. These are overlapping focused invocations, not a summed count of
unique tests. Independent internal reviews covered the store split, callback gaps,
shared queue and final stream lifetime integration; steward review remains pending.

Live GOG fresh and warm native probes passed all 62 samples, both PSD negative
controls and the unsupported-DDS capture fallback. Final prepared-plus-automatic
reuse consumed 2,894 warm records including 54 private probe records, with no errors.
Final PNG cold and warm runs each passed 76 native comparisons, including 39
uncompressed cases, with zero mismatches. Actual C06 first-build construction on
`339e4d34` also passed 76 comparisons; its unchanged decoders and retained stream
path are covered by the subsequent focused queue checks.

All final measured warm runs consumed **2,840 actual records**, reported two
concurrent source-open/read/decode jobs, and ended with zero held queue bytes.
The largest recorded final reservation, including both stream contexts, was
55,436,684 bytes. This demonstrates consumption and overlap, not merely thread
creation. Tests additionally cover independent same-group streams, independent
block/entry corruption, descriptor rejection, source changes, mutation outside
locks, cancellation, replacement after draining, handle eviction and deferred-open
errors. A failed worker open can be discovered after another worker has opened a
later file; the owner drains all such work before any native fallback callback.

### Matched measurements

Times below are seconds to the independent completed-menu repaint. Each warm arm
starts from the same captured seed independently. Construction is separate from
warm reuse; no private probes are enabled in measurements.

| Source / workload | Native controls | Compressed warm | Broad prepared warm |
| --- | --- | --- | --- |
| `22b20316`, native DDS selection | 17.504459 / 16.652557 | 17.029098 / 16.919295 | 17.500288 / 17.357871 |
| `5bda4276`, native DDS selection | 16.804025 / 16.170666 | 16.791716 / 17.372247 | 16.764891 / 16.498667 |

Both rounds use forward/reverse native, compressed and prepared arms. Their
functional seeds contain the natural 2,840 records plus private probe records;
only the natural 2,840 are consumed in measured loading. This proves broad prepared
consumption of automatic native output, not a new manual preparation producer.
The final native texture stage took **0.623–0.711 s**, versus **1.175–1.438 s** for
warm compressed/prepared texture loading. The whole-startup variation does not
establish a useful DDS speedup, and the texture-stage regression remains concrete.
Disk reduction does not compensate for this unmet acceptance criterion.

| Final PNG-selected workload | Menu seconds | Texture-stage seconds |
| --- | --- | --- |
| Native controls | 19.751411 / 19.807760 | 3.783207 / 3.763103 |
| Warm cache, same clean seed independently | 16.868820 / 16.913208 | 0.983019 / 0.981020 |
| Separate cache construction | 31.693429 | 15.944244 |

The PNG comparison preserves a **2.88–2.89 s** benefit in these matched pairs.
Its clean seed contains 2,840 entries: 333,497,002 stored bytes, 332,443,410 payload
bytes and 100,032,074 source bytes. This fixture-selected PNG workload is separate
from its ordinary DDS-native selection. The single new construction run is not a
new average or a construction speedup claim; prior construction costs remain recorded.

The final DDS worker diagnostics localize remaining work: summed worker opening
was 425–556 ms, source reading 219–247 ms, and identity/group/record decoding
1,286–1,598 ms. Owner preparation was 323–507 ms, owner take/wait intervals
638–850 ms, and guarded restoration/completion 181–224 ms. **These intervals
and worker sums overlap; do not add them together or to menu time.** Existing
serial group counters exclude the parallel path; use the per-queue worker records.
The changed parallel source-opening premise was implemented and tested; retained
handles alone are not being presented as a newly successful route. The remaining
validation/decode and ordered-admission costs require further engineering before
DDS reuse can be accepted. No unchecked shortcut, scope exclusion or default change
is adopted to conceal them.

### Restoration and handoff

All **37 assignment transactions** were rolled back through `fixture.py`. The seven
baseline hashes and ten baseline absences match, and retained repair transaction
`20260912-170800-04578930` remains intact: `replacements.json` has its recorded
SHA-256, 1,367,187-byte size and `1788549061179635300` timestamp. No fixture process
remains. The final source is preserved for review; no successor is dispatched and
no parent plan, ledger or steward-state file is edited by C05.

Raw evidence is under ignored
`artifacts/ecosystem-next-20260910/c05/parallel-warm-20260913/`:
`all-results.json`, `validation-summary.json`, build/test logs, prepared-input
verification receipts, `restoration.json` and the final `handoff.json`. Run captures
remain in `.rlo-test-instance/results/c05pw-*`. C05 is **stopped awaiting independent
review, with full acceptance still blocked by DDS performance**.

## Independent steward review and next correction — 13 September UTC

The steward reviewed clean stopped `81ea27cc9b28e6fef552bd9696f0de48b78e77fa`
and tested `27c3016cb211b505919ad6dd59980b3e5e52e4bd`. Scoped independent source
review passes: one fresh digest still checks both expected digests for identical
single-block byte coverage, while multi-block checks remain independent. The
eight corruption cases exercise the intended checks, and the timing additions
change no identities, snapshots or return behavior.

The steward verified all 32 capture manifests and recorded comparison identities,
97 captured evidence files, both final DLLs, 39 rollback receipts, seven baseline
hashes, ten absences and the retained repair's hash/size/timestamp. Actual texture
and functional-probe records support the reported results. Raw independent review
is `qualification-20260913/steward-review.json`. The first unusually slow native
run is excluded from benefit claims; functional diagnostic times are not used as
measurements. Construction, PNG-selected and DDS-native workloads stay distinct.

**Conditional warm PNG benefit is supported; C05 full acceptance is not.** The
compressed/prepared DDS regression persists, and disk reduction is not accepted
as compensation. The same owner receives the next bounded worker-read correction
under the [recorded engineering design](next-campaign-proposal.md#steward-engineering-correction-parallel-warm-texture-reads--13-september-utc).
Its measured source-validation/decode premise differs materially from the failed
serial handle/hash approaches. Do not repeat those designs unchanged or waive
the underlying capability. New source must first preserve behavior under focused
GOG checks, then demonstrate useful actual loading performance against native
controls. No default, quality, dependency or scope concession follows.

## Matched qualification and remaining DDS cost — 13 September 2026

PNG reuse has a useful measured benefit on the selected PNG workload. Compressed
and explicitly prepared DDS reuse still take longer than ordinary native loading.
Keeping group files open did not resolve that failure. A further small correction
removes duplicate hashing while preserving both validation checks, but does not
resolve the complete loading regression either. **C05 remains incomplete; disk
reduction alone is not acceptance.**

The owner opened unattended window `owner-unattended-20260913-044927` at
04:49:27 UTC with no fixed cutoff. This assignment started from clean reviewed
`f7d920b36060196f917063efc9435a39b86f95e9`, preserving later C14 corrections.
First measurements used the independently reviewed **existing** core/observer
packages from `7e4f40eec49420f5c04f4d65667536359e889e8b`; the checkout was not
reverted. Texture source had no changes between that source and the assigned base.
The subsequent correction and matching core/observer packages are
`27c3016cb211b505919ad6dd59980b3e5e52e4bd` on current ancestry.

### Conditions and scope

There are **29 performance captures and three functional captures**, all with
normal exit, a valid independent menu observation and `automaticTestPassed=true`.
Every launch checked current ownership/window permission, exact 13 frozen / 15
active order, matching core/observer source, the saved settings and a muted
profile. LoadingProgress and Gagarin were physically excluded alongside the three
existing exclusions. Measurements used minimized/nonactivating launches with no
private functional probes, no other RimWorld process, no competing builds/tests/
subagent work and no forced OS cache flushing. Incidental foreground behavior was
permitted during the unattended window; no desktop automation or owner-assisted
visual inspection occurred. These are bounded workload observations, not universal
averages or combined-candidate results.

All DDS user-setting arms retained PSD support and a 1,024-MiB shared disk budget.
Native controls disabled automatic/prepared texture reuse. Compressed arms enabled
automatic caching and lossless storage compression; explicit-prepared arms enabled
native prepared consumption alone. Ordinary automatic arms enabled caching without
storage compression or explicit preparation. PNG qualification instead used
fixture activation with `--png-source original`, selecting existing PNG siblings,
and changed only `--png control|cache`. C06 first-build decoding was not selected.
Within each comparison, non-feature profile overrides and content order matched.
PNG and DDS selections are distinct workloads and are not compared as alternatives.

### Reviewed file-handle candidate

The initial sequence was native → compressed → prepared → prepared → compressed →
native. Each warm arm independently imported `c05lc-handles-seed`, whose automatic
native producer populated 2,840 records. The first native control,
`c05q-r1-native-a`, took **57.183080 seconds**, including a 33.635-second texture
stage. Later native controls had approximately 0.68–0.71-second texture stages.
Retain this large initial order effect as evidence; do not use it to claim a gain.
OS file-cache warming is a plausible explanation, not an independently isolated
cause. No cache flush was attempted.

A closing warmed bracket, native-b → compressed-c → prepared-c → native-c,
resolved that ambiguity. All compressed/prepared arms consumed 2,840 records.

| Reviewed `7e4f40ee` arm | Menu seconds | Texture stage seconds |
| --- | ---: | ---: |
| Stable native controls `c05q-r1-native-b/c` | 17.092074 / 16.891500 | 0.683 / 0.709 |
| Compressed repeats `c05q-r1-compressed-a/b/c` | 18.441638 / 18.425464 / 18.619021 | See exact logs |
| Prepared repeats `c05q-r1-prepared-a/b/c` | 18.426528 / 18.661628 / 18.453194 | See exact logs |

Group-file acquisition was only 3–6 ms for the complete workload. Further work on
handle reuse is therefore not a credible explanation for the remaining loss.

### Small correction and current-source measurements

Most records occupy one block. The old reader hashed that block and then hashed
the identical complete record again. The correction computes that digest once,
compares it separately with **both** expected digests, and retains independent
whole-record hashing for multiple blocks. Storage format, keys, codecs, bounds,
fallback and texture representation are unchanged. Eight new cases corrupt the
block or entry digest independently, for compressed/uncompressed one/two-block
records, while resealing the index so the intended validation is exercised.
All **78 focused group/store/quality checks passed**. A bounded read-only subagent
identified the duplicate work and implemented this isolated change; the parent
reviewed it and ran all checks. The subagent stopped before measurements.

Three narrow counters separate platform lookup, source-byte reading and identity
construction within the existing source-identity cost. They change no validation.
They are diagnostic attribution, not a separate profiling system. Hash timing is
nested inside other scopes; never add it to stage or startup totals.

`c05q-r2-functional-seed` passed all **62 native-representation samples**, both
native PSD negative controls and unsupported DDS capture fallback. It populated
the automatic native store. Each measured warm arm imported this same seed
independently. Its index also retains 54 private probe records; measurement runs
activate no probes and consume only the 2,840 real selected records. Prepared
consumption is not evidence of a new broad manual-preparation job.

| Current `27c3016c` DDS arm | Menu seconds | Texture stage seconds |
| --- | ---: | ---: |
| `c05q-r2-native-a/b` | 16.542082 / 16.836612 | 0.689 / 0.679 |
| `c05q-r2-compressed-a/b` | 18.113203 / 18.011366 | 2.045 / 1.914 |
| `c05q-r2-prepared-a/b` | 18.324147 / 18.473999 | 2.143 / 2.381 |

The sequence again used native → compressed → prepared → prepared → compressed →
native. All reuse arms had 2,840 hits, no cache errors and no prepared refusals.
Explicit-prepared-only runs use `preparedHits`; their unused automatic-cache
counters are zero. Relative to the corresponding forward/reverse controls,
compressed loading still adds **1.17–1.57 seconds**, and prepared loading adds
**1.64–1.78 seconds**. Neither is accepted.

Measured group hashing fell from approximately 243–248 ms to 138–142 ms. The
remaining compressed-arm costs include platform lookup 63–89 ms, source-byte
reading 179–194 ms, source identity/hash construction 184–209 ms and approximately
340–351 ms of other source-identity work, including file acquisition. Group decode,
which includes reading compressed bytes, costs 468–492 ms; native-record decoding
and pixel copying costs 116–175 ms; texture restoration costs 51–54 ms. These
scopes explain separate parts of the path and are not an additional startup sum.
Cross-source whole-startup differences do not establish a separate net speedup
for the small hashing correction.

### PNG reuse and construction

Current-source functional PNG cold/warm runs each passed **76 native comparisons**,
including 39 uncompressed comparisons, with no mismatch or cache error. The warm
run consumed all 2,840 entries and wrote none. A preceding preparation,
`c05q-png-functional-seed`, was stopped by the helper's mute assertion before any
launch; the corrected functional layout was used under new labels. It is not a
failed game capture or performance sample.

The measurement order was native → construction A → warm A → warm B → construction
B → native. Both warm arms imported `c05q-png-build-a`; neither imported the prior
warm capture. All runs selected 2,837 PNGs and three JPEGs, reading 100,032,074
source bytes. Construction means an empty generated store, not an OS cold-cache
claim. Each construction wrote 2,840 records; each warm run had 2,840 hits and zero
writes.

| PNG-selected arm | Menu seconds | Texture stage seconds |
| --- | ---: | ---: |
| `c05q-png-native-a/b` | 20.352018 / 20.023148 | 4.279 / 4.112 |
| `c05q-png-warm-a/b` | 17.833959 / 17.896767 | 1.604 / 1.544 |
| `c05q-png-build-a/b` | 32.558670 / 33.590728 | 16.562 / 17.589 |

The corresponding forward/reverse warm differences are **2.52 / 2.13 seconds
shorter**. Construction instead adds **12.21 / 13.57 seconds** against those native
controls. Generated storage is 333,497,002 bytes for 332,443,410 record-payload
bytes. This supports useful conditional PNG reuse, with a separate first-build
cost. It does not establish DDS acceleration, C06 construction performance,
later gameplay/first-use savings or a universal percentage.

### DDS construction and ordinary automatic mode

`c05q-dds-native-a` → build-a → build-b → native-b measured empty compressed-store
construction separately. Each build produced 2,840 records with zero errors:
**74,607,565 stored bytes for 296,421,450 record-payload bytes**. Original sources
remain present and untouched; this is not a total-installation disk reduction.

| DDS-native selected arm | Menu seconds | Texture stage seconds |
| --- | ---: | ---: |
| `c05q-dds-native-a/b` | 16.982138 / 17.615461 | 0.774 / 1.025 |
| `c05q-dds-build-a/b` | 29.784687 / 30.150760 | 13.613 / 14.027 |
| `c05q-auto-build` | 17.034983 | 0.920 |
| `c05q-auto-warm-a/b` | 16.797573 / 16.941693 | 0.652 / 0.675 |
| Closing `c05q-auto-native-c` | 16.591957 | 0.720 |

The latter sequence continued from native-b through automatic construction, two
warm arms and native-c. Each warm imported `c05q-auto-build`. Ordinary automatic
mode passed through **all 2,837 DDS files natively** and cached only the three
JPEGs: three writes on construction, three hits/zero writes on each restart.
Generated storage was 14,328,199 bytes. Whole-startup differences are smaller than
the observed native-control spread and do not establish a stable overall saving;
the direct DDS route remains distinct from the regressing compressed/prepared
route. No change of default or silent refusal of explicit preparation follows.

### Next correction and handoff boundary

A faster lossless codec alone cannot remove the measured source-validation and
other serial costs. The next materially different candidate is bounded parallel
warm loading: worker-owned source/group validation and decoding, followed by
ordered texture creation on the game thread. Start with bounded concurrency and
explicit memory reservations; preserve full source/block/entry identity checks,
native fallback and drain-before-mutation/callback boundaries. It needs immutable
read descriptions and independently positioned group streams, because current
`ReadExternal` serializes grouped reads and retained streams share positions.
The C06 queue deliberately skips warm owners and treats DDS as a callback barrier;
simply queueing the current `cache.Read` would not solve this. This is a proposed
increment, not implemented or measured, and must preserve C06/C08 shared behavior.
Avoid new codec/dependency/default decisions without concrete owner review.

Do not repeat the measured serial compressed/prepared design under unchanged
conditions. Retain its useful PNG behavior and validation correction while the
original C05 owner addresses the unresolved capability under serial assignment.
Matrix 3a/3b and applicable 6a acceptance remain open; no disk-only tradeoff,
supplier-suite removal, manual-window, gameplay/first-use or release acceptance is
claimed. Other workloads and combined-candidate performance remain separate.
C10/C11/6c are deferred and were not resumed or treated as release gates.

Raw evidence is `artifacts/ecosystem-next-20260910/c05/qualification-20260913/`:
`all-results.json` records every capture's source/package/observer/settings/order,
cache seed, results and evidence hashes; `reviewed-package-results.json` preserves
the initial nine-run comparison; build/test logs and runner/settings are adjacent.
All **39 assignment transactions** were restored in reverse order.
`restoration.json` verifies seven baseline hashes, ten absences and retained repair
`20260912-170800-04578930`, including its exact hash, length and mtime. No game,
helper, build, test or child work remained. Only this C05 report and assigned
product/test files changed; parent state/plan/ledger were not edited. The candidate
returns for independent review, with performance failure explicitly retained.

## Steward review: bounded startup reads and broad consumption pass

Stopped `a3cd284c694bec999848c9938ad62852d3dbd069`, tested product/observer
`7e4f40eec49420f5c04f4d65667536359e889e8b`, passes independent functional
review. Scoped read-only storage review found no actionable regression in
handle bounds, cleanup, mutation, complete record validation or C08 shared-store
semantics. This depends on the existing serialized startup lifecycle; it does
not add general concurrent read/write support. Diagnostic additions change no
output representation. Their nested timings must not be added or used to qualify
a speedup from these functional runs.

The steward checked all four final-source run manifests, recorded settings and
sources, actual texture logs and the native probe. Broad prepared consumption
works both with automatic caching enabled and alone: each reports 2,840 prepared
hits with zero misses/refusals. The compressed run has 2,840 hits plus 54 probe
writes, rather than a zero-write run; all 62 sampled native representations pass.
The automatic producer is explicitly separate from manual preparation. Existing
adequate manual/quality/atlas evidence was retained, not recreated. The 96-test
and three-case follow-up logs pass; one repeated case yields 98 distinct tests.

Seven baseline hashes, ten absences, eleven rollback receipts and the exact
retained frozen-file repair were independently checked; no game/helper remains.
Raw steward review is `artifacts/ecosystem-next-20260910/c05/load-cost-20260912/steward-review.json`.
The owner returned before any performance run; full C05 acceptance and the prior
compressed DDS load-cost failure remain open. Keep the exact changed package
for later matched native/compressed/broad-prepared comparisons, plus separate
PNG-selected, construction and combined obligations. Proceed to the original
C04 owner for functional compatibility work under the owner's approved serial
progression. No performance work, accepted speed claim or scope exclusion follows.

## Startup group-file reuse candidate — 12 September 2026

Startup now keeps up to eight texture group files open while the texture store
owns them. These temporary open-file references (file handles) can serve later
reads from the same group. This removes repeated opens on those reads; whether
it reduces the known compressed DDS slowdown remains **unmeasured**. The prior
roughly three-second loss is still an unresolved failure, not an accepted disk
tradeoff. C05 full acceptance remains open.

The candidate continues from reviewed base
`0f2e0b4aa256f1fd18d347526e453ab9efada5c4`, preserving later implementation.
`0e109ec6f911cb5b1ffecbd91166c7dcb67b1989` adds read/restore attribution counters;
`7e4f40eec49420f5c04f4d65667536359e889e8b` implements bounded file reuse and is
the tested core/observer package source. No new codec, dependency, stored format,
user option or default was introduced.

Only batched startup `PreparedTextureStore` instances retain handles. Their
existing exclusive category ownership encloses each handle's lifetime. The least
recently used handle closes when the eight-file bound is reached. All borrowed
handles close before write admission, atomic quality publication, invalidation,
clear, completion and owner disposal. Windows read sharing prevents external
writes/replacement of the open file. Each read still checks block identity,
bounds, decoded size, every block digest and the complete record digest; source
and platform identity checks remain live. No decoded texture or validation
result is retained. Read errors release the handles and preserve ordinary
fallback. Manual preparation, read-only quality preview and atlas callers retain
their existing per-read closure. Automatic 128-entry/64-MiB publication batches,
manual durability, C06 CPU workers and C08 selection protection are unchanged.

### Functional evidence and its limits

The owner returned before any performance launch in this increment. The steward
revoked `owner-unattended-20260912-173527`; performance testing remains prohibited
until another explicit grant. Every captured run below used `--purpose functional`.
Its recorded timings are diagnostic only and provide no benefit comparison.
No performance run, codec trial or cost-attribution trial was launched.

Focused storage, quality and atlas checks passed 96 tests. Three additional
lifetime/atomic cases passed after extending coverage (one case reran, for 98
distinct passing tests overall). The new checks cover handle eviction and
closure, blocked external writes, reopening appended data, corruption refusal,
atomic quality writes, invalidation, clear and disposal before releasing ownership.

All four final-source GOG captures exited normally and passed the automatic
observer. Each launch asserted exactly 13 frozen content mods and 15 active mods
in the saved order, matching package sources and a muted profile. LoadingProgress
and Gagarin were physically excluded in addition to the existing exclusions.

| Functional capture | Result |
| --- | --- |
| `c05lc-handles-seed` | Automatic native producer wrote 2,840 compressed records; no cache errors. |
| `c05lc-handles-compressed` | Reused all 2,840 records, reading 296,421,450 record bytes; 62/62 native-representation samples passed, with no failed/refused samples. Both native PSD negative controls and unsupported DDS capture fallback passed. The probe added 54 records. |
| `c05lc-handles-prepared-auto` | Explicit preparation and automatic caching together: 2,840 prepared hits, zero prepared misses/refusals and zero writes. |
| `c05lc-handles-prepared` | Explicit preparation alone: 2,840 prepared hits, zero prepared misses/refusals. Use `preparedHits` here; the automatic store is not used, so its counters report zero. |

The latter three runs imported the unchanged seed capture through the fixture's
identity-checked cache transfer. Thus broad explicit **consumption** is now
demonstrated; its producer was automatic native capture, not a newly exercised
2,840-item manual preparation batch. Existing manual preparation evidence remains
separate. Sampled representation checks and menu startup do not establish a
complete gameplay or visual walkthrough.

Raw evidence, build/test logs, input settings and run/hash summary are under
`artifacts/ecosystem-next-20260910/c05/load-cost-20260912/`; the summary is
`functional-evidence.json`. An earlier attribution-source functional probe,
`c05lc-attribution-functional`, also passed 62 samples but is not final-source
qualification. Source counters overlap: group decoding includes encoded reads,
and group-read totals include acquisition, decompression and hashes; do not add
these nested timings. No dominant-cost conclusion follows from this increment.

All eleven assignment transactions were rolled back in reverse order. The
`restoration.json` receipt verifies seven baseline hashes and ten absences,
preserving transaction `20260912-170800-04578930`. Its frozen `replacements.json`
remains SHA-256 `6c74ade7594ce8f350c7821ab691bbeaccae622989b4e21b6e3876799a942e2d`,
1,367,187 bytes and mtime_ns `1788549061179635300`. No RimWorld process remained.

This candidate is ready for independent functional review; it is not adopted or
accepted. The next authorized measurement window must test whether handle reuse
addresses enough of the loss, with broad compressed/prepared consumption and
matched forward/reverse native controls on the exact package. Retain the separate
PNG-selected, cold-construction, other-workload and combined obligations. If a
material loss remains, return the evidence and concrete alternatives to the
steward/owner; do not close matrix 3a, 3b or 6a or silently accept the tradeoff.

## Steward correction review — 11 September 2026

Corrected handoff `9988c3fe4615dcb042bf8ad40ad7f0fecb210f55`, tested source
`b847d20e33ba4eb4ba4e14b0f4360978ba97c899`, passes independent functional review.
Two bounded read-only source reviews found the DDS fallback corrected, explicit
prepared consumption preserved alongside automatic caching, and coherent bounded
batch recovery, quota and immediate preparation durability. The steward checked
all 21 final capture manifests and their observer/texture evidence hashes, read
the actual fallback/native comparison and changed-source receipts, and verified
all seven restored endpoint hashes with no RimWorld process remaining. Final
PNG verification reports 2,840 hits and 76 sampled comparisons without mismatch.
No broad visual walkthrough or full atlas/colony claim is inferred.

**Functional review passed; full acceptance remains open with a known performance
failure.** PNG-selected warm texture processing improves modestly in the matched
controls, with a small variable whole-startup margin. Compressed DDS is still
about 19.31 seconds versus 16.37–16.43 seconds native. That is an unresolved
load-cost problem, not an accepted tradeoff or an excluded capability. Sparse
prepared measurements cannot qualify a whole prepared modlist. Retain first-use,
construction, other-workload and combined obligations with this same C05 owner;
the C06 first-build pipeline remains separately assigned. No supplier-removal or
release-readiness claim follows.

The owner permits serial progress after functional review. The steward will use
the remaining unattended window for earlier-slice qualification, starting with
the original C01 owner on the current preserved ancestry. This changes timing,
not C05 scope or acceptance. Return further C05 corrections to its existing owner
after a serial ownership transfer. The checked original fixture remains the next
assignment endpoint. Raw review: `artifacts/ecosystem-next-20260910/c05/steward-correction-evidence-review.json`.

## Corrected handoff — 11 September 2026

**Returned for independent steward review; C05 is not accepted.** The DDS fallback
is corrected, automatic construction now commits bounded batches, and real
PNG-selected reuse has a modest measured benefit. Ordinary automatic DDS loading
uses the native path. Explicit preparation and compressed DDS remain functional,
but their measured costs do not establish a loading benefit. No C06 dispatch,
merge, push or publication occurred.

C05 owner `01a08ed1-2f24-7603-a5e0-4fe081e9759f` resumed from exact clean steward
review `72c78abe2e81e1d3659a1bf18a49b8a1028bcfcc` under
`wake-up-c05-review-fallback-performance-20260911`. Final tested source is
`b847d20e33ba4eb4ba4e14b0f4360978ba97c899`; the final documentation commit is later.
All child work, builds, tests and fixture operations are stopped. Exclusive
ownership returns to steward `01a08d63-a8ef-7c40-8cd9-bbf9f512ce22` for independent
review. Earlier handoff and review sections below remain historical evidence.

### Corrected behavior and recovery

An unsupported processed-image capture platform now falls back to ordinary DDS
loading before source hashing or cache work, instead of returning `BadTex`.
The actual GOG observer injects that unsupported condition, forbids unexpected
source snapshotting, and verifies native pixels/descriptor and an untouched store.

Steward decision `wake-up-c05-native-dds-routing-20260911` keeps ordinary automatic
DDS consumption native unless explicit prepared output or compressed storage is
selected. This changes no default and adds no checkbox. Native pass-through has
its own counter; it is not a hit or a new DDS speedup. Explicit prepared DDS is
considered even with automatic caching enabled. A missing provider/path owner now
returns before platform/source work; presence never replaces the full content,
provider, platform and descriptor checks. A present-but-stale prepared entry with
compression selected can still repeat source work during automatic fallback.

Full growing-index publication no longer happens after every automatic image.
Automatic construction stages at most 128 complete entries and 64 MiB of newly
written block data before a checkpoint; a larger single entry commits separately.
Staged entries are complete and readable in-process, and their bytes count toward
the shared quota. Durability follows only after touched data files flush and an
atomic full-index replacement commits the batch. Normal completion saves the last
batch. An interrupted startup can lose that bounded unfinished cache work while
retaining prior committed entries. Failed batch writes/checkpoints roll back
uncommitted ranges and stop further batch writes. Original textures are untouched.

Explicit preparation retains immediate durable publication through atomic checked
entry records, with at most 128 records before a full checkpoint. Fresh authenticated
version 3 checkpoint generations prevent stale records reviving invalidated entries;
version 2 indexes remain readable. Existing source invalidation, complete-entry
validation, quota, cancellation, clear/bypass and independent block reads remain.
No persistent background writer or new runtime dependency was introduced.

The initial correction `457ec71` replaced per-image full indexes with bounded
records. Measured attribution on `372db49` (`c05r-compressed-cost-29`, 49.190 s menu)
then found 15.519 s in individual records and 6.273 s in data flushes. This concrete
cost prompted the final automatic batching correction. Those earlier revisions
were not interleaved with the final package and do not justify a cross-version
percentage. Timing counters overlap; do not add publication, checkpoint, flush or
capture totals to startup time.

### Final source, package and functionality

| Layer | Exact evidence |
|---|---|
| Tested core source | `b847d20e33ba4eb4ba4e14b0f4360978ba97c899` |
| Tested core DLL SHA-256 | `24768650767febeef90d4b709068c76cb53a44d854a6066dbb4d86a78f574abe` |
| Observer source / DLL | Same source; `fbab4241d85f117131c9f0bf8318564f3baaaf9aa3fdab5e1b731d041331c8d7` |
| Local source-inclusive ZIP | `89485b218ea0dc9f7f48faec2f950cd2e61080b386fd5bd4aa41470eda26ca4c`; exact tested DLL, embedded PSD decoder, source and notices, no helper; not published |
| GOG generation | `gog-rev573-20260910-194017-cf1a1eb0`; manifest `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`; game assembly `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28` |

Focused managed tests passed **78**, including interrupted count/byte-bounded
batches, source-owner replacement, failed checkpoint rollback, quota, immediate
oversized entries, native DDS fallback and prepared admission. Fixture tooling
checks passed **54** for the correction; observer builds passed. A separate
read-only integration review found no blockers. Storage review of the record
correction found a stale-generation edge; it was corrected and re-reviewed.
The batch implementation was reviewed by the root owner and passed focused tests;
it still awaits the steward's independent final review.

All **21 final-source captures** (`32` through `52`) exited normally with automatic
checks passing: six functional captures and fifteen performance captures. Counts
are not added to historical overlapping tests. Functional evidence:

- `c05r-batch-compressed-32`: 2,853 automatic writes, zero errors; all 21 texture
  samples, two native PSD negative controls, and the injected DDS fallback pass.
- `c05r-batch-changed-33`: 2,852 hits and exactly one changed-source replacement;
  all 21 samples still pass. The test changes only a private profile source.
- `c05r-manual-seed-34` and `c05r-prepared-auto-35`: actual manual batch publication
  then ordinary startup with preparation and automatic caching both enabled;
  13 successful prepared resolver calls are observed, alongside shared automatic
  reads, native DDS pass-through and passing full texture probes.
- `c05r-png-final-verify-41`: imports the final measured PNG seed, restores all
  2,840 entries, writes none, and passes 76 sampled native/restored comparisons
  with zero mismatches/errors. Earlier functional `30`/`31` on `372db49` also
  established actual cold/warm PNG selection, not a synthetic image-only test.
- `c05r-clear-off-52`: the captured compressed store is cleared while texture
  features are disabled; the owned groups directory is empty afterward.

The full probe retains all-mip byte/descriptor checks and explicit copied-mip
render comparisons where Unity can construct the separate mip. Non-block-aligned
compressed tails remain byte-checked and explicitly unrendered. The PNG sampled
comparison alone is not proof that every mip was rendered independently. No
interactive window walkthrough or ordinary colony visual inspection is claimed.

### Final matched measurements

The owner granted an unattended window at `2026-09-11T05:57:38Z`, ending at
`12:00:00Z` or earlier owner return/revocation. All final measurements ran between
07:36 and 07:47 UTC with `--purpose performance`, other RimWorld processes stopped,
child/build/test work paused, the same minimized nonactivating launch, independent
menu observation and normal exit/capture. Windows file caching was not forcibly
reset; these are bounded forward/reverse comparisons, not universal averages.
Each warm arm imports its named captured seed independently.

The frozen selection contains Prepatcher, Harmony, Core plus five DLC, VEF,
VMemes, P-Music, Alpha Animals and ReGrowth (13 frozen entries; 15 with candidate
and observer). Loading Progress and Gagarin are absent from discovery for these
replacement tests; original unrelated exclusions remain. PSD private probes are
absent from performance launches. Earlier supplier compatibility evidence is
separate; this does not establish removal of an entire supplier suite.

**PNG-selected workload.** Fixture activation uses `--png-source original` and
`--png control|cache`, replacing selection with existing PNG siblings only for
qualification. It loads 2,837 PNGs and three JPEGs. The warm seed is
`c05r-perf-png-build-37`; all arms otherwise share candidate/settings/selection.

| Run | Menu seconds | Texture stage ms | Automatic hits / writes |
|---|---:|---:|---:|
| `c05r-perf-png-control-36` | 19.988997 | 4098.499 | 0 / 0 |
| `c05r-perf-png-build-37` | 34.237355 | 18405.203 | 0 / 2840 |
| `c05r-perf-png-warm-38` | 19.000983 | 3389.102 | 2840 / 0 |
| `c05r-perf-png-warm-39` | 19.400612 | 3308.692 | 2840 / 0 |
| `c05r-perf-png-control-40` | 19.460493 | 3931.356 | 0 / 0 |

Warm texture loading falls from 3.931–4.098 s to 3.309–3.389 s. Whole-menu results
are closer and variable: 19.001–19.401 s warm versus 19.460–19.989 s controls.
This supports a modest benefit for this selected workload; it does not establish
a general average or a large startup improvement. Empty-store construction still
costs 34.237 s. The generated store is 333,497,002 bytes for 332,443,410 payload
bytes and 100,032,074 source bytes. C06 still owns first-build decode/preparation;
this result does not close that obligation.

**Ordinary DDS-native workload.** User activation keeps PSD support on and other
settings at their recorded defaults. Controls disable caching/preparation;
automatic arms enable `pngCache`, compressed arms also enable storage compression,
and prepared arms enable only `preparedTextures`. The shared allowance is 1 GiB.

| Run | Menu seconds | Texture stage ms | Automatic hits / writes |
|---|---:|---:|---:|
| `c05r-perf-dds-control-42` | 16.425357 | 758.688 | 0 / 0 |
| `c05r-perf-dds-auto-build-43` | 16.933548 | 824.568 | 0 / 3 |
| `c05r-perf-dds-auto-warm-44` | 17.025459 | 949.641 | 3 / 0 |
| `c05r-perf-dds-compressed-build-45` | 29.250848 | 13484.150 | 0 / 2840 |
| `c05r-perf-dds-compressed-warm-46` | 19.309520 | 3530.506 | 2840 / 0 |
| `c05r-perf-dds-prepared-47` | 16.878978 | 883.929 | separate prepared reader |
| `c05r-perf-dds-prepared-48` | 16.788578 | 1117.506 | separate prepared reader |
| `c05r-perf-dds-compressed-warm-49` | 19.308167 | 3586.489 | 2840 / 0 |
| `c05r-perf-dds-auto-warm-50` | 16.565803 | 741.350 | 3 / 0 |
| `c05r-perf-dds-control-51` | 16.370652 | 860.466 | 0 / 0 |

Ordinary automatic caching passes all 2,837 DDS files through natively; its three
cache hits/writes are JPEGs. Whole startup remains 0.140–0.655 s above the native
controls in these two warm samples, with texture-stage results straddling the
controls. No automatic DDS gain or proven zero overhead is claimed.

Compressed warm reads all 2,840 entries but consistently takes about 19.31 s,
versus 16.37–16.43 s natively. Construction takes 29.251 s. Its generated storage
is 74,607,565 bytes for 296,421,450 complete payload bytes (about 75% smaller than
the payload); original DDS files remain. This disk saving is not a speedup, and
the measured compressed DDS regression remains an explicit acceptance limit.

Prepared arms independently import manual seed `c05r-manual-seed-34`. The normal
performance observer does not patch per-request prepared counters; absence of
`prepared-startup.jsonl` therefore means uninstrumented, not zero hits. Checked
index use timestamps show seven existing entries read (26,632,057 payload bytes)
in each run, with every seed key retained. Functional run `35` separately proves
actual prepared restoration. These sparse prepared timings do not measure full
modlist preparation or establish a prepared-DDS loading benefit.

The storage correction, native fallback and PNG benefit are sufficient to return
for review; optional further storage tuning is not pursued as a new project.
Full acceptance/release readiness remain open because compressed/prepared costs,
other workloads, later first-use/colony behavior and individual/combined campaign
performance are not all positive or complete. C06 decode and C08 integrated
quality/preparation keep their assigned scope. C05 neither accepts itself nor
dispatches a successor.

### Restored endpoint and evidence

The tested package is retained, not deployed. Original core
`ba17b033479f28fb5902be7ff9e5da8ac4ad1bc8` and observer
`3d86b1636976678407583e9ba5b7e55c9ddd0f07` are restored through transactions
`20260911-034921-59614206` and `20260911-034924-1d641fa7`; profile restoration is
`20260911-034930-45d860b9`. All seven original hashes match, settings/prepared
pointer are absent, original order and three exclusions are restored, and audit
reports `deployedRuntimeMatches=true`. No RimWorld process remains.

Raw evidence stays under `artifacts/ecosystem-next-20260910/c05/` and
`.rlo-test-instance/results/`: `correction-final-runs.json`,
`correction-prepared-read-proof.json`, `correction-package-proof.json`,
`correction-restored-identities.json`, `correction-closeout-proof.json`, focused
check logs and per-run manifests. No parent coordination state/timer, normal
installation/data, Deck, other application, security setting or public listing
was changed.

## Steward review — 11 September 2026

Review of handoff `939094ac939feaf3794f0fad120190810cc4084b` requires corrections;
C05 has neither a functional pass nor full acceptance. Two bounded read-only
source reviews found no storage corruption blocker, but found a DDS fallback
defect: `LoadProcessedDds` calls `PreparationPlatform()` without handling its
unsupported result. The outer enumeration catch then publishes `BadTex` instead
of ordinary DDS output. Correct this before further qualification.

The steward independently rehashed twelve selected capture manifests and their
observer/texture evidence, verified all seven restored endpoint identities and
confirmed no RimWorld process remained. Final native/compressed warm controls
support the reported DDS-heavy regression. The source also still rebuilds,
hashes and rereads the whole growing index after every image publication. The
append correction does not remove that construction cost.

Return both the correctness fix and a focused performance redesign to the same
C05 orchestrator under exclusive ownership. Investigate bounded publication
transactions and source-format-appropriate consumption; preserve atomic complete
entries, original files, content validation and broad grouped/prepared behavior.
Measure a real PNG-selected workload as well as DDS controls before judging the
processed-image capability. No existing failure excludes a capability, and
default-off status is not a remedy for measured regression. C06 first-build
decode remains separate. The current overnight window ends at 12:00 UTC or
earlier owner return; no successor is dispatched during this correction.

Raw independent checks: `artifacts/ecosystem-next-20260910/c05/steward-evidence-review.json`.
The original handoff below remains historical evidence.

**Implementation handed back for independent steward review. Not accepted.**
C05 owner `01a08ed1-2f24-7603-a5e0-4fe081e9759f` returns stopped exclusive ownership
to steward `01a08d63-a8ef-7c40-8cd9-bbf9f512ce22`. Functional preservation and
compressed reuse pass the bounded checks. Both cache modes regress startup on the
measured DDS-native workload; a general loading benefit is not established.

C05 lets automatic caching and manual preparation keep the same finished texture,
including large images and every smaller mip level. Native PNG/JPEG and DDS keep
the game's pixels and texture behavior. Optional compressed storage restores the
same bytes. A separately approved bundled PSD decoder adds new optional behavior;
it is not described as preserving native PSD output.

## Assignment and permission

Dispatch `wake-up-next-c05-dad8637-20260911` started from exact clean
`dad86377b7522641e93966e0ae88db8d7eee778d` on
`codex/ecosystem-next-plan-review`. Approved `f70aa08` and preserved later
`fe687d4` ancestry were verified; main `5299ff4` remains unmerged. No additional
checkout/worktree, push or publication is authorized. Only this owner operates
the checkout and the isolated fixture; bounded children returned their edits.

GOG functional tests and necessary modlist variants use standing permission. The
owner additionally granted an unattended performance window at
`2026-09-11T05:57:38Z`, with steward cutoff `2026-09-11T12:00:00Z` (08:00 Toronto)
or earlier owner return/revocation. Before that grant there were no performance
experiments. Functional timings remain diagnostic. Measurements require all
RimWorld instances stopped, quiet child/build/test work, declared performance
purpose, matched launch style, independent observer and normal exit/capture.
This grant cannot authorize a future overnight run. Steam, Deck/Linux, normal
data, other applications, UI automation and security changes remain outside scope.

## Shared native output

`NativeTextureData` records actual format, dimensions, mip count, pixel bytes,
sampling/wrap/anisotropy/bias and final CPU readability. It validates layout and
rechecks the actual Unity texture created from a stored entry. GOG exposes legacy
Alpha8/ARGB32 graphics-format values absent from its public enum; their actual
format pairs were observed and admitted explicitly. PNG/JPEG retain the native
conversion/compression path. DDS retains the native parser, format selection,
BGR swizzle and upload; a scoped capture temporarily preserves the readable bytes
and then restores native unreadability. The source hash and native DDS mapping
share one read-locked file snapshot. Unsupported exact methods/hooks keep the
ordinary path. Already GPU-ready DDS is not counted as avoided image decoding.

Identity is specific to a provider and selected physical/logical source, content,
effective folders, runtime/device/settings and representation contract. Other
sources remain reusable after one changes. Each provider retains its own texture
holder; lower-provider access is not replaced with a global winner. Automatic and
prepared paths use the same entry and allowance. Current actual DDS branches
include BC1, BC3, BC7, RGB/BGR24/32, RGB565, RGBA4444 and Alpha/luminance data;
coverage is recorded per observed branch rather than inferred from an extension.

## Grouped storage and bounds

Generated output shares `WakeUp/PreparedTextures/v1` with explicit DDS exports;
new entries are under `groups`. The checked index addresses independently checked
blocks of at most 1 MiB in groups at most 16 MiB. A read reconstructs only its
texture's blocks. Missing/corrupt blocks cannot return a partial texture. Deflate
is optional and used only for smaller blocks; it changes disk bytes, not image
quality or the identity of the restored representation.

The writer appends after immutable committed ranges, then atomically replaces the
checked index. It removes only the new suffix on a failed publication; reopening
trims bytes beyond all indexed ranges. Previous committed bytes are never
overwritten. This replaces repeated tail-file copying after the measured
123.029-second first construction at `24b9027`. Existing Windows SHA-256 now hashes
array ranges directly, retaining exact digests and the portable managed fallback.
An independent bounded read-only review found no blocker in append rollback,
recovery, quota accounting or the pinned hash range. The corrected package's
measurements below retain its remaining construction and warm-load costs.

Native/new-PSD entries are below 96 MiB including descriptor/identity. Native
source snapshots are at most 96 MiB; base PNG/JPEG/PSD decoded RGBA space is at most
64 MiB, dimensions at most 8192. Full native mip layout has its own entry bound.
Source bytes, completed pixels, encoded entry, GPU texture and native staging can
coexist: this is not a measured total process-memory ceiling. Exports retain the
separate 8 MiB output and 16 MiB source limits. No new helper execution or worker
pool is introduced; native work remains on its required thread, one item at a time.

The configurable shared allowance remains 1 GiB by default across XML/language,
textures and exports, including temporary/replacement/index bytes. A superseded
source mapping disappears only after complete publication. Quota pruning protects
current-session entries and prunes bounded unvisited prior entries; age pruning
uses 30 days with at most 128 removals per pass. Old individual PNG/prepared-native
formats are retired conservatively; exports and unknown files are preserved.
Obsolete blocks inside a live group remain charged until the group is reclaimed.

## Bundled PSD decision and delivery

`c05-native-contract-03` supplied valid 4x4 raw and RLE PSD composites. The actual
GOG native loader returned 8x8 output without the known channels, establishing
that extension discovery did not provide working native PSD decoding. The owner
then approved the proposed dependency under relay
`wake-up-c05-psd-approved-bundled-20260911`: "sure, that's fine if we're bundling it,
so long as users don't have to install this dependency manually on their own machines".
The approval is also appended to the active plan without changing older history.

A bounded source adaptation of StbImageSharp **2.30.15**, upstream
`6fd7aebe1dbf10e28d78745f42a0c095c61d1945`, is compiled into **WakeUp.dll**. Its license
and contract are embedded resources and readable release notices. Users install
no separate library/framework, download nothing at runtime and execute no helper.
`psdSupport` defaults off and applies on restart, independently of caching. The
[exact source and output contract](../../third-party/StbImageSharp-PSD.md) documents
RGB PSDv1 raw/PackBits, 8/16-bit samples, saved-composite-only interpretation,
white-matte alpha removal, high-byte 16-to-8 reduction, ignored ICC/resources and
bottom-first RGBA8 output. Unity uploads sRGB RGBA32 with full mips, trilinear
filtering, anisotropy 2 and final unreadability. Unsupported inputs retain native
loading. The new-decoder identity is distinct from native texture preservation.

The private observer creates known PNG/PSD/DDS inputs inside the fixture profile
before ordinary texture discovery, through its own provider folder only. Product
code contains no fixture or mod allowlist. PSD delivery evidence must come from
ordinary loaded holders and the actual deployed DLL/MVID/resources; a successful
build or package restore alone is insufficient.

## Functional evidence and exact identities

Raw build/test/research receipts are ignored under
`artifacts/ecosystem-next-20260910/c05/`; game captures remain under
`.rlo-test-instance/results/`. The current GOG generation is
`gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`, game assembly
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.

Retained functional checkpoints:

| Capture | Core / observer | What it establishes |
|---|---|---|
| `c05-native-contract-01` | `dd11d42` | Launch refused for new argument reconstruction drift; no game launch. Corrected before run02. |
| `c05-native-contract-02` | `dd11d42` | Normal menu/exit and exact native DDS method pins. Probe stopped at reflection ambiguity; no texture-fidelity claim. |
| `c05-native-contract-03` | `4c46c28` / same | Native PSD negative controls, DDS capture activation. Private compressed-mip staging and small PNG enum checks needed correction. |
| `c05-native-large-04` | `4c46c28` / `6b209f1` | Real 1470x1961 BestDog RGB24 JPEG: 11 mips and 11,527,047 pixel bytes preserved. GPU readback could not decode compressed formats and was replaced with explicit supported mip copies. |
| `c05-native-psd-05` | `7efa83d` / same | Menu/exit and probe pass: 12 samples, zero failures/refusals; four actual startup PSD variants, native PNG/JPEG/DDS, large/non-square and >8MiB data. Two expected native PSD negative controls pass. |
| `c05-prepared-warm-06` | `7efa83d` / `e3e4b16` | Actual prepared startup hits. Extended DDS exposed Alpha8's enum omission and one recorded I/O refusal; incomplete probe, not a full functional pass. |
| `c05-auto-cold-07` | `296a121` / same | Native automatic startup: 2,853 misses, 1,395 writes, zero cache errors; 128MiB quota kept remaining sources native. Probe passed 18 but two large manual publications were refused by capacity. Normal exit only, not full probe pass. |
| `c05-compressed-cold-08` | `296a121` / `4b7623b` | 21 samples pass, real native style-mask material/holder relationship, four bundled PSD variants. Automatic compressed startup writes 2,844 entries; nine misses remain unfilled. |
| `c05-compressed-changed-warm-09` | `296a121` / `4b7623b` | 2,843 hits and ten writes, including the equal-size/timestamp changed PSD. Probe passes 20/fails 1: actual Windows `File.Replace` throws while replacing the index. Old data/fallback retained; not full probe pass. |
| `c05-native-retry-10` | `24b9027` / same | Bounded atomic replacement retry; all 21 samples pass in both storage forms. |
| `c05-prepared-changed-11` | `24b9027` / same | Actual later manual-prepared startup consumes 19 entries, including large JPEG/DDS and multiple mip/format layouts, while changed PSD loads fresh. All21 probe samples pass. |
| `c05-append-compressed-17` | `d1a665c` / same | Final append/range-hash package writes all 2,853 automatic entries from empty storage; all 21 samples pass, zero cache errors, native PSD negative controls 2. |
| `c05-append-changed-18` | `d1a665c` / same | Final compressed store restores 2,852 entries and rebuilds exactly one changed PSD; all 21 samples pass and zero cache errors. |
| `c05-clear-off-19` | `d1a665c` / same | Imports the populated final store, selects clear with texture features off, exits normally; generated groups are absent/empty. |

Final tested core and observer source is
`d1a665c513ae9103d019d373fe4bd0fc08d5e51e`. Core DLL SHA-256 is
`86c493efe5faf4eca115aa4170046eafd30d47f6ae198f13135ebe5a5ae24940` (614,912 bytes);
observer DLL SHA-256 is
`bea122476324fea7d5bf4726406a4e886de0145d4ac3622724857fae2b4c45f9` (147,968 bytes).
They are retained under `artifacts/fixture-package/<revision>` and
`artifacts/fixture-menu-observer/<revision>`, separately from the restored endpoint.
All final functional captures exit 0 and pass `automaticTestPassed`; runs 17/18 also
pass `textureStorageProbeTestPassed` and `allRequestedSamplesPassed`.

The real mask is VMemes' `VME_Serketist_Bed` style using `Custom/CutoutComplex` and
`VME_SerketistBed_northm.dds`. The observer obtains the material from native
`ThingStyleDef.Graphic` and proves its mask is the actual loaded holder reference.
Large samples include 1470x1961 RGB24 JPEG with 11 mips (11,527,047 pixel bytes)
and 3200x3200 BC7 DDS with 12 mips. No frozen mod file is edited. The changed
private raw8 PSD stays 104 bytes with UTC ticks 639246816000000000; its SHA changes
from `e5b40bcd9d404135a0df90a0c8c775697a089ae55a4f1ea4a1acde93efe199dc`
to `e6cee69549cb710f1713abbc8dfad7d35b2daff267f9d58f5a88025a6f6c6da5`.

The standard source-inclusive packager produced a local development-delivery
proof at `artifacts/releases/0.3.0-rc.2-d1a665c513ae9103d019d373fe4bd0fc08d5e51e/`.
ZIP SHA-256 is `22cc5dbf2219516da8a86dc78df00496deed0ff667369f4c908e3b674624c866`.
Its runtime DLL exactly matches the tested DLL above; embedded-license/MVID
receipts come from actual ordinary PSD startup holders. The archive includes
exact committed source and both readable PSD notices, and no helper executable.
Unfinished documentation was byte-preserved under ignored artifacts while the
standard tool archived the clean tested revision, then restored exactly. This
is delivery evidence, not a published or Steam-qualified release; final handoff
documentation is later than that archived source revision.

Focused check `focused-07.log` passes 83 storage/native DDS/PSD checks;
`psd-build-tests-03.log` passes 65 PSD/adapter/settings checks. These overlapping
counts are not additive. Final append/range/store checks pass 56; final fixture
checks pass 53 and release-boundary checks pass 6. The fixture test's old
functional-only display expectation was corrected to cover both explicit run
purposes and refusal of an undeclared purpose. All process-admission guards
remain. Observer builds pass. All-mip raw data is compared exactly. Rendered checks copy
supported native mips explicitly; compressed sub-block/nonaligned dimensions that
Unity cannot construct independently retain byte equality evidence and are labeled
unrendered, rather than silently approximating an explicit mip selection.

This does not claim a visible gameplay walkthrough, full colony/material/atlas
compatibility, Linux/Steam qualification, helper conversion quality, or release
acceptance. C06/C08/C09 retain their assigned broader work.

## Performance and review

All measurements below are declared performance runs, taken within the explicit
window with no other RimWorld process, child work, builds, tests or substantial
source scans running. All use the same `minimized-noactivate` request, automatic
independent first-menu observer, actual normal process exit and successful capture.
No memory/file cache flushing, desktop automation or normal-state change occurred.

The frozen selection is `selection-large.json`: Prepatcher, Harmony, Core and all
five DLC, VEF, VMemes, P-Music, Alpha Animals and ReGrowth. Candidate and observer
make 15 active entries. This workload selects native DDS alongside three JPEG
sources, with no private C05 probe sources during measurement. It is not the
PNG-selected qualification workload or a universal modlist. All arms retain
ordinary user settings activation, PSD support on and shared allowance 1024MiB;
only `pngCache` and `compressTextureStorage` vary, with `preparedTextures=false`.
Other features retain the same recorded defaults. Warm runs import the exact
same captured seed separately, not a progressively warmed generated store.

| Final package run | Menu seconds | Texture-holder loading ms | Hits / writes | Generated store bytes |
|---|---:|---:|---:|---:|
| `c05-perf-final-control-20` | 16.819447 | 751.247 | 0 / 0 | 0 |
| `c05-perf-final-auto-build-21` | 75.218801 | 59049.163 | 0 / 2840 | 297467616 |
| `c05-perf-final-auto-warm-22` | 19.473707 | 3195.924 | 2840 / 0 | 297467616 |
| `c05-perf-final-compressed-build-23` | 78.903990 | 62821.816 | 0 / 2840 | 74607549 |
| `c05-perf-final-compressed-warm-24` | 19.759531 | 3661.363 | 2840 / 0 | 74607549 |
| `c05-perf-final-compressed-warm-25` | 20.327993 | 3461.525 | 2840 / 0 | 74607549 |
| `c05-perf-final-auto-warm-26` | 19.698918 | 3448.896 | 2840 / 0 | 297467616 |
| `c05-perf-final-control-27` | 16.835894 | 678.508 | 0 / 0 | 0 |

The warm comparison reverses control → uncompressed → compressed → compressed →
uncompressed → control, with empty-store construction recorded separately. Both
forms read the same 296,421,450 completed payload bytes, but compression uses about
75% less generated disk storage. Original DDS/source files remain untouched and
their disk footprint is not reduced. Both warm modes are slower than both native
controls. Construction also adds substantial work. These results reject a DDS-native
loading-speed claim for this workload; caching and compression remain default-off.
`reloadMs` is the full guarded texture-holder stage; `pngMs` only covers PNG/JPEG.
Neither is added to the whole-startup observation. First-use reads restore bounded
individual entries; no separate gameplay/late-first-use performance claim is made.

Predecessor `24b9027` controls 12/16 were 17.066069/17.016601 seconds, automatic
warm 14/15 were 21.378163/21.437772, and construction 13 was 123.029225 seconds
(107082.159ms texture stage). That concrete cost prompted append-only ranges and
accelerated block/index hashing. The final construction observation is lower,
but each construction arm has one measurement and revisions were not interleaved;
do not turn that observation into a generalized percentage benefit. The final
forward/reverse controls support the remaining negative warm-load result.

The product-progress stopping point is the faithful shared representation,
working bounded storage/compression/maintenance and one measured correction to
the demonstrated write/hash costs. Do not turn C05 into an unbounded storage
benchmark project. The assigned C06 first-build pipeline and C08 integrated
quality/preparation retain their actual scope. PNG-selected, other workloads,
supplier integrations affected by future shared-boundary changes, later first-use
and combined-candidate measurements remain distinct obligations. C01–C04 are not
measured by C05 totals. Independent steward review and full acceptance remain open;
no performance benefit or release readiness is inferred from work counts.

## Restoration and ownership

Restored exact original core `ba17b033479f28fb5902be7ff9e5da8ac4ad1bc8`,
observer `3d86b1636976678407583e9ba5b7e55c9ddd0f07`, profile/order, original three
exclusions, absent settings/prepared pointer and seven initial identity hashes
recorded in `initial-identities.json`; `restored-identities.json` verifies all seven.
Core restore transaction is `20260911-024852-9de186c3`, observer
`20260911-024855-46c61b89`, and seed profile/order `20260911-024929-4c0a1255`.
Temporary Loading Progress and Gagarin exclusions are restored; the original three
remain. Audit reports `deployedRuntimeMatches=true`; no RimWorld process remains.
An initial restore attempt used a concatenated package ID/folder number and was
refused before mutation; retry used the exact saved package ID. `closeout-proof.json`
also verifies clear/off and exact packaged/runtime identity.

All captures/packages remain retained. Child work, builds, tests and fixture
operations are stopped; clean documentation handoff returns exclusive ownership
to the steward for independent review. C05 does not accept itself or dispatch C06.
No parent coordination state/timer, normal game/data, Deck, other application,
security settings, main branch, remote or public listing was modified.
