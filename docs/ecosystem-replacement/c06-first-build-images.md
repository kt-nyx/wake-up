# C06 first-build image preparation

## Interrupted raw-pixel trial recovered � 19 September UTC

**The isolated helper trial produces usable pixels for the tested image families,
but no loading benefit or product adoption has been established. Its seven
functional runs were already captured when work was interrupted. Recovery ran
no new game launches, benchmarks or performance experiments, restored all 15
owned fixture transactions, and preserved the implementation and failed evidence.**
C06's earlier measured regressions remain failures; this trial does not turn the
managed path into a beneficial conservative fallback or establish release readiness.

Recovery action `wake-up-c06-recovery-20260919-228e07b9` started from exact clean
`228e07b9a7cf6a65b2eb59aafe02c68622e82175` on the preserved review branch. That
commit changes owner instructions/state documentation only relative to final
trial runtime `9061d6b26a7a29ce845535be31e28bf99431561f`. All earlier unattended
windows are closed. Coordination now belongs to Revamp Steward
`01a08711-7899-7ff3-a1b5-4a0b105ba661`. No parent state/timer, default setting,
normal installation, deferred feature or platform scope was changed by recovery.

### What the candidate actually does

The helper returns decoded pixels directly to the game's existing upload path;
it does not encode another PNG for the game to decode. A separate Windows Imaging
Component (WIC) protocol, `--stdio-raw-v1`, sends bounded source bytes and receives
bottom-first RGBA base pixels. Existing native finalization still creates the
final texture and smaller texture levels (mipmaps). The protocol is documented in
[the helper contract](../../native/texture-helper/README.md#isolated-gog-raw-pixel-trial-protocol-v1).

The explicit `--raw-pixel-helper` fixture selection binds the deployed executable's
SHA-256 identity. There is no installed-user setting or automatic adoption. One
hidden, owned child starts lazily for admitted cold images, serves correlated
requests serially and is joined on completion or failure. Cancellation and a
10-second request deadline terminate only that child. PNG input retains the
existing complete structural, checksum and compressed-stream validation before
WIC runs. JPEG retains its full managed decode and requires an exact per-image
pixel comparison before consuming helper bytes; that is a correctness guard,
not a demonstrated faster JPEG decoder.

The raw child has a 96 MiB private process-memory limit. The parent reserves
that full allowance plus 1 MiB for transport inside the existing 192 MiB shared
queue budget, preserving two workers/eight jobs, source revalidation, native
ordering and existing callback/nested-upload boundaries. Raw input and base
output each cap at 16 MiB; larger or unqualified families retain managed/native
loading. Recorded queue reservations peaked at 161,611,891 bytes, with at most
two active workers. These are accounting counters, not measured total resident
memory. Mono reported `childPeakWorkingSetBytes=0`; it is unavailable evidence,
not proof of zero child memory.

### Completed correctness evidence and preserved failures

All seven runs used functional purpose and exited normally with captured menu
signals. Normal exit alone does not make a failed texture probe pass.

| Run | Observed result |
| --- | --- |
| `c06-raw-png-verify-01` | At `32202e28`, actually uploaded helper output for 1,355 PNGs and 3 JPEGs; 76 sampled native comparisons matched. One child, 1,358 requests, no helper failures. Another 1,482 images used existing family fallback. |
| `c06-raw-native-probe-01` | **Failed**: 57 enabled samples passed, one grayscale transparency sample differed, and four PSD samples were disabled. Failure is preserved despite automatic exit passing. |
| `c06-raw-native-probe-02` | At corrected `9061d6b2`, 58 enabled samples passed, zero failed, four PSD samples remained disabled. Actual startup consumed 12 PNG and 17 JPEG helper outputs through one child/29 requests. This is not an all-62-sample pass. |
| `c06-raw-cache-cold` | Wrote 2,840 cache records, consuming 1,355 PNG and 3 JPEG helper outputs; no texture/cache errors. |
| `c06-raw-cache-warm` | Misconfigured warm check: copied the obsolete PNG cache directory and actually rebuilt all 2,840 records. It provides no warm-cache proof. |
| `c06-raw-cache-warm-02` | Correct shared-cache import: 2,840 hits, zero misses/writes, zero helper starts/requests/uploads. |
| `c06-raw-dds-only` | Misleading label: 2,140 DDS files passed through natively, but two JPEGs still caused one helper child/two uploads. A genuinely DDS-only no-child run was **not** established. |

The failing image uses an eight-bit grayscale transparent-color key (`tRNS`).
At one fully transparent pixel, native/managed decoding preserves RGBA
`48,48,48,0`, while WIC returns `0,0,0,0`. Hidden color still matters to later
texture processing. Commit `9061d6b2` routes the entire grayscale transparent-key
metadata family through the existing managed decoder, without recognizing a
fixture name or source hash. Source hash
`0cb5dafc7a91c372bffc02599c0e9ffd3b4b3d958bec41ce2f506f23becb5196`, input,
WIC bytes and the independent counterexample are preserved privately. RGB
transparent-key and other tested metadata families did not show this failure;
that does not establish universal WIC fidelity.

Before the live runs, 147 focused managed tests passed with none skipped,
including the actual helper's reuse, active cancellation and concurrent disposal.
Eight raw protocol check groups, 71 existing helper quality checks and 69
fixture-tool tests also passed. Earlier logs preserve test compilation errors,
two subsequent test failures, and a raw-test invocation missing `--helper`.
The valid-handshake test exposed a real 17-byte literal in a 16-byte protocol;
both ends now use exact 16-byte `WUTXRAWHELP00001`. That fix preceded every
captured game run. The final grayscale correction was verified by the native
probe rather than a new test framework.

A bounded independent source review at `32202e28` found no concrete integration
defect and explicitly reserved WIC fidelity for native checks. The subsequent
four-line grayscale fallback has the corrected native probe above. Recovery did
not duplicate that review. Existing callback/native-holder checks remain, but
post-menu callback probes run after helper disposal and do not demonstrate an
active raw child during a callback. Full family/malformed-input/gameplay coverage,
genuinely DDS-only activation and aggregate memory measurements remain limited.
No standalone or combined timing comparison was run for this trial. Functional
elapsed times must not be used as speed evidence, and no further experiment is
implied by this closeout.

### Exact identities and restored handoff

Initial native verification used core/observer source `32202e28f03450a486409bb64d7a948fea5048f0`.
The corrected probe, cache runs and last mixed DDS/JPEG run used matching
core/observer source `9061d6b26a7a29ce845535be31e28bf99431561f`:

- Core DLL SHA-256: `79bf3c3bbcc58fe1d6bb0b50e2c78530b72277ee90bc8b07e162c0ce8e07a6e3`.
- Observer DLL SHA-256: `2b45c650225c66f6c23bc04a64b3061665abfe18f15eb796abba8b21b4ec0f29`.
- Raw helper SHA-256: `9a404c526f54f91cd41ffc3664bc7dcb40b387c9dba0c75e5cf081d5b5dd7546`, built from `32202e28`; every recorded helper build input still matched during recovery.
- Helper build receipt SHA-256: `f9ea7221b1f75ba432ccb61d089748f249ac27c59a4cb8009d0971212a5b2ec1`. The ordinary optional-quality helper identity was not replaced or promoted.

Private evidence is under
`artifacts/ecosystem-next-20260910/c06/raw-pixel-helper-20260913/`:
`all-results.json`, `recovery-evidence.json`, individual original captures/logs,
`gray-trns-counterexample.json`, and `restoration.json`. Recovery verified the
15 committed journals belonged to this assignment, then rolled them back in
reverse order through `fixture.py`. Seven baseline file hashes and ten original
absences match, with the raw-trial executable additionally absent. Retained repair
`20260912-170800-04578930` remains intact, including its recorded file hash, size
and timestamp. No live fixture/helper/build/test process remains. The source
trial and all captures stay preserved; the fixture is restored for the next
owner. No merge, push, publication, Steam or Deck operation occurred.

## Independent prepared-boundary review; raw-pixel investigation next — 13 September UTC

Reviewed clean stopped `d6a5819a4031ffdacf261f2e67dd8ebab1568b57`, tested
`d84bfd40d6956c434fdc8b12d761431954302a14`. Scoped runtime and actual observer
review passes: no-op predicates stay local, real capture suspends outside the
store lock, and existing callback/mutation protection is intact. The real quality
group applies on the clean route; the holder callback produces native fallback
with no speculative workers/source/group handles alive. This is acceptance of
the bounded integration repair, not C05/C06 full feature acceptance.

The steward verified ten capture manifests, 112 indexed files, final DLLs,
actual quality-probe events, all 16 rollback receipts, seven baseline hashes,
ten absences and repair hash/size/time. The initial missing-provider functional
failure remains failed despite normal exit; corrected 14/16 quality and original
13/15 native/performance evidence are distinct. All measured warm arms read,
consume and upload 2,840 records once. Independent evidence is in
`c06/prepared-quality-boundary-20260913/steward-review.json` under the ignored
campaign artifacts. Checkout and operations returned clean/stopped.

Prepared startup still adds 0.61/0.70 seconds versus matched native controls;
compressed adds 0.32/0.67 seconds. **DDS performance remains failed.** Previous
C06 first-build/JPEG regressions and broader coverage are unchanged. No new
measurement of an unchanged failing route is required by this review.

The same C06 owner next investigates a different CPU route: the existing Windows
helper returns decoded base pixels directly, through a bounded reusable process,
for actual game upload/finalization. This avoids the rejected expanded-PNG
re-encoding/second-decode approach. The next plan section defines the isolated
GOG trial, strict native-fidelity and total-cost questions, resource limits and
adoption boundary. It does not enable automatic helper execution for installed
users, change defaults, add a library or close any parent capability. A concrete
candidate and its consequences must precede any material adoption decision.

## Prepared quality-collection boundary repaired — 13 September UTC

**Ordinary prepared loading now reads each unchanged record once. Correctness
checks pass, but both prepared and compressed DDS loading still lose to native
loading. C05 and C06 remain unaccepted.** The unnecessary suspension introduced
by the earlier C06 lifecycle change is repaired without retaining warm work
across actual callbacks or changing image quality, settings or dependencies.

Assignment `wake-up-c06-prepared-quality-boundary-20260913` resumed exact clean
`ff60ae4158bf6fe6021510772100456aee875f82`, with `39178efb` and `a782b92e`
ancestry preserved. Runtime commit `36763670` keeps the existing startup/read,
explicit-owner and already-collected decisions inside `PreparedQualityRuntime.Loaded`.
Only a real collection invokes the supplied suspension action, immediately before
capturing item content or calling `GetContentHolder<Texture2D>()`. The action is
allocated once per enumeration; absence is never cached between calls.

Source review confirms that the preliminary checks use current in-memory state.
`HasOwner` releases `OwnedCacheStore`'s lock before the action runs, and the
loading session initializes `StartupStore` before admitting its queue. Existing
yield/native-consumer guards, full source and record validation, C08 ownership/
role/group checks, mutation-driven warm retirement, GPU/callback suspension and
nested/current-upload memory accounting remain unchanged. C05's advisory native
enumeration metadata correction is preserved. A separate read-only source
review found no actionable issue; steward acceptance remains separate.

### Focused and actual GOG correctness

All **148 focused managed tests and 76 fixture-tool tests pass**, none skipped.
Three new tests call the actual `Loaded` method with absent, present and already
collected owners. They prove that no-op cases do not touch native objects and
real collection reaches the boundary before holder access. The first test build
failed only because NUnit's two delegate overloads were ambiguous; explicit
`Action` casts corrected that test compilation, with the failed log retained.

Observer commit `d84bfd40` extends the existing C08 boundary check. It observes
the real supplied action and calls actual `Loaded` again with a throwing action
to check already-collected behavior. The clean case applies the expected quality
output. A separate case installs a real holder callback with a populated
first-build queue; the callback observes suspended workers, zero active workers,
zero source streams and zero grouped-read handles. Its actual native group read
then correctly causes complete native fallback. Existing source replacement,
constructor, DDS and consumer fallback cases remain.

- `c06q-quality-boundaries` is a preserved **functional failure**, despite normal
  automatic exit. The 13-entry comparison selection lacked the natural Brazier
  pair required by the existing probe; no boundary cases ran.
- `c06q-quality-boundaries-02` adds installed Vanilla Textures Expanded only for
  this functional check (14 frozen entries / 16 active mods). All nine boundary
  cases and the existing format checks pass. Both observed cases record two
  real collections, one absent-owner no-op and two already-collected no-ops.
  The populated-queue case records two quiescent holder callbacks. Its inherited
  `lateInjectionPoint` text is not the injection point of this new scenario:
  the holder hook is installed before reload and acts only during collection.
- `c06q-native-fresh` and `c06q-prepared-warm` use the original 13/15 selection.
  Both pass all 62 native samples, two PSD negative controls, DDS unsupported
  capture fallback and all four C06 callback comparisons, including changed
  source pixels and retained unchanged output. Prepared warm reuse plans,
  reads, consumes and uploads exactly 2,894 records: 2,840 workload records plus
  54 private probe records. There are no array-fallback uploads, refusals,
  misses or texture errors. Private later callback queues remain separate.

These functional durations are diagnostic only. The quality check runs the
existing helper and actual game holder/texture paths without desktop automation;
it does not claim broader C08 appearance/reset/restart or gameplay acceptance.

### Exact package and bounded matched measurement

All ten captures use final core and observer source
`d84bfd40d6956c434fdc8b12d761431954302a14`. Core DLL SHA-256:
`3213dc46f8bae16188785ae8f3affbb2659e14f1ac2b6cb8a116702826a5c244`.
Observer DLL SHA-256:
`afd48c93579871be31eedf5474bae0afe3a9e7ceb6cc99f344989cf65c8b9c71`.
The unchanged pinned helper is
`ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`.
GOG generation, manifest and game references remain those recorded below.
The documentation handoff commit is distinct from this tested package.

The active `owner-unattended-20260913-044927` grant was checked before each run.
Measurements had sole operating ownership, stopped game processes, stopped
children and no competing builds/substantial work. The idle MSBuild reuse node
was left alone. All six measurements used the same 13/15 order, muted profiles,
C06 off, no probes, independent menu observation and normal automatic exit.
All four warm arms independently imported `c06q-native-fresh`; its seed includes
native automatically stored output, not a new explicit-quality producer.

Order was native A, compressed A, prepared A, prepared B, compressed B, native B.
Capture labels have prefix `c06q-dds-`. These are warmed Windows file-cache
conditions, not OS-cold measurements.

| Arm | Menu A / B (seconds) | Texture stage A / B (seconds) |
| --- | ---: | ---: |
| Native | 17.006359 / 16.850398 | 0.773021 / 0.720118 |
| Compressed | 17.326491 / 17.521543 | 1.180961 / 1.158324 |
| Prepared | 17.615595 / 17.549648 | 1.490059 / 1.204397 |

Each warm arm plans, reads, consumes and performs owned uploads exactly **2,840
times**, with zero array fallback, refusals, misses, writes or errors. Prepared
no longer performs the prior 10,584/10,670 reads. This establishes the repaired
mechanism; historical cross-revision times are not an isolated speedup measurement.
Prepared still adds **0.609236 / 0.699250 seconds** to complete startup and
0.717038 / 0.484279 seconds to the texture stage against matched native controls.
Compressed adds 0.320132 / 0.671145 seconds to startup. No losing sample was
discarded and no unchanged losing series was repeated.

Prepared worker open totals are 509.964/526.820 ms, record decoding totals
1,388.650/1,232.959 ms, owner preparation 137.355/138.550 ms, owner take
606.690/545.000 ms and owner restore 187.481/184.789 ms. These sums overlap
across workers and owner waits; do not add them to stage or menu durations.
The remaining cost is now ordinary record opening/decoding/validation and upload
coordination, rather than repeated reads caused by this no-op boundary. The
receipts do not establish a further safe shortcut. Maximum warm queue reservation
is 40,263,338 bytes; all queues end empty within the existing two-worker/eight-job/
192-MiB budget. This does not measure total process memory.

### Restored bounded handoff and remaining work

Evidence is under
`artifacts/ecosystem-next-20260910/c06/prepared-quality-boundary-20260913/`.
`all-results.json`, `handoff.json` and `evidence-index.json` retain exact capture,
package, seed, probe and supporting-file identities, including the prerequisite
failure. All **16 owned transactions** are reversed. The seven baseline hashes,
ten absences and repair `20260912-170800-04578930` hash/length/timestamp match;
the original full profile and disabled baseline packages are restored. Two
helper deployments are accounted for: core deployment replaced the first overlay,
and the second restored the helper for the quality check. No normal data changed.

Checkout returns clean and stopped to the steward for independent review. No
game, active build/test/helper or child work remains. Parent plan/ledger/state/
timer files were not changed. C06 standalone/combined decoding performance,
JPEG/DDS overhead and broader coverage remain required and separate; this task
does not adopt a decoder rewrite or suggest an unproven cure. C05 compressed DDS
performance, C14 correctness and C15 consolidation also remain open. No full
acceptance, successor, Steam/Deck/Linux promotion, merge, push or publication
follows. C10/C11 and the companion remain deferred.

## Independent lifecycle and decoder review — 13 September UTC

Reviewed clean stopped `7585152f068c9a8f0db0176203ca9e77df5bfda5`, tested
`39178efb8c53390f277400742dee053251d317cc`. The retained correction passes
independent scoped source and captured-evidence review. Completed cold pixels
survive unlocked callbacks, full-byte revalidation detects changed sources, and
both final measured combined constructions decode, consume and write each of
2,840 unchanged images once. The strict decoder changes preserve the reviewed
malformed-input checks and native-equivalent output for the tested families.
Nested reload retires speculative buffers and blocks a second queue; the current
outer upload remains charged until unwind. This implements the aggregate budget
rule without pretending a still-live upload buffer has been freed.

The steward independently verified 28 capture manifests (8 functional, 20
performance), 240 indexed evidence hashes, the final two DLLs, all 36 rollback
receipts, seven baseline hashes, ten required absences and the retained repair's
hash, length and timestamp. Final-source native samples/callback cases and the
76-texture/39-uncompressed combined comparison pass. Earlier-source evidence is
kept distinct. The checkout is clean and no game/build/helper process remained.
Raw independent verification is `c06/lifecycle-decoder-20260913/steward-review.json`
under the existing ignored campaign artifact directory. No new tests or launches
were needed for this review.

**C06 remains unaccepted.** Final standalone first-build loading adds 3.31/4.25
seconds against its matched native controls; combined construction adds
2.55/5.33 seconds. Warm reuse has no repeatable additional C06 gain. Three JPEGs
add 402/511 ms to the ordinary-DDS texture stage. The repaired repeated-decoding
mechanism is delivered; speed, broader image-family/PSD coverage and remaining
real gameplay qualification are not. Pass counts and fewer decodes do not waive
these requirements.

Use the next serial slot for C05's already queued metadata-admission correction.
C06 stays stopped with its performance/coverage obligations open and must return
to its original owner for a materially changed, evidence-backed decoding/waiting
approach. Do not repeat the same failing matrix unchanged, narrow JPEG admission,
or infer approval of a new dependency, default or capability exclusion. C14
correctness and C15 consolidation remain outstanding; no platform promotion follows.

## Retained-pixel lifecycle and strict decoder correction — 13 September UTC

**The repeated-decoding defect is fixed, and the bounded correctness checks pass.
Performance still fails: C06 is not accepted.** Both final combined construction
runs decode and upload each of the 2,840 eligible images once. They nevertheless
take longer than C05-only construction, and standalone first-build loading still
takes longer than native loading. These results preserve the remaining C06
requirements; they do not approve a smaller feature scope or a new dependency.

Assignment `wake-up-c06-lifecycle-decoder-correction-20260913` resumed clean
`46f6f703c5b46f527e1e0ccc59ddf45cde4076e6` under the original C06 owner. Work
used the existing single checkout and isolated GOG fixture. Measurements used
the active `owner-unattended-20260913-044927` grant, serial ownership, stopped
RimWorld processes and paused competing builds/agent work. The steward owns
independent review and acceptance. This section records the new implementation;
earlier experiments and failures below remain historical evidence.

### Completed pixels survive unlocked callbacks

The queue now keeps completed image data separately from its open source file.
Before arbitrary mod callbacks, cache mutation or the whole native GPU
compression/copy path, it stops admitting work, waits for active workers and
closes every speculative source/group stream. Pending, failed and warm-cache
work is discarded. Successful cold results retain their immutable pixels, full
source bytes, selected index and charged memory reservation. Repeated suspension
is safe, and workers remain stopped throughout the callback.

Before reusing a retained result, the queue reopens its current source with
`FileShare.Read` and compares all bytes, including changes beyond 64 KiB and
same-size/same-timestamp replacements. Its 64-KiB comparison scratch is charged
inside the existing per-job overhead. Missing, unreadable, changed or newly
ineligible input follows the ordinary current-source path. A completed retained
batch is consumed before another batch is admitted, avoiding a stop/restart cycle
for every retained image. Cache-dependent jobs are never retained across mutation.

Unknown iterator consumers suspend. The production native continuation uses
`PngConsumerContract`: exact ContentPath/KTrie method guards, actual holder/trie
and comparer checks, normalized paths, and suspension before duplicate logging.
This does not whitelist arbitrary GPU callbacks. A nested reload retires all
outer speculative results and prevents inner queue admission. The current outer
upload's reservation remains charged until its caller releases the still-live
buffer, preventing two full budgets from overlapping. Disposal retires everything.

Successful PNG/JPEG upload receipts are recorded immediately after pixel upload
and Apply, before later GPU/cache callbacks. A current PSD publication survives
unlocking so its eventual upload can still be counted. Unlocking therefore cannot
erase actual consumption or cause the native continuation to replay.

### Strict decoding does less per-byte work

`252d82b4` adds a bounded 512-entry Huffman prefix table: common short codes use
one lookup, while longer codes use the existing validated canonical decoder.
The lookup never invents bits at end of input; stored-block alignment preserves
whole bytes already buffered by lookahead. Existing malformed-stream, final-block,
history, output-length, cancellation, CRC and Adler checks remain enforced.

`59882e2e` implements the lifecycle/runtime work; `1b2b72bf` extends the native
callback proof. Initial matched measurements on `1b2b72bf` still failed badly:
native/first-build menu A was 20.213218/33.739979 seconds and first-build/native B
was 34.064520/19.967534. Texture stages were 4.077487/18.239498 and
18.112894/3.999655 seconds respectively. All four captures completed normally;
their poor performance is preserved, not excluded as an operational failure.

Concrete follow-up changes in `9536d82b` and `39178efb` bulk-read validated
DEFLATE matches, batch Adler arithmetic before row unfiltering, copy contiguous
8-bit RGB/RGBA rows directly, compare retained sources eight bytes at a time,
skip a provably empty JPEG post-conversion loop, consume completed batches before
refilling, and stop planning cold work across DDS-only boundaries. No admitted
image family, quality rule, default or runtime dependency changed.

### Correctness evidence and exact tested package

Four focused managed-test invocations passed 186, 61, 140 and 111 tests
respectively; these overlap and are not a count of distinct tests. Each invocation
also passed the existing 76 fixture-tool tests. Builds and observer builds passed.
Tests cover strict short-EOF/long-code/stored-block behavior, overlapping history
copies and cancellation, Adler limits and corruption, RGB row output, complete
source revalidation, callback suspension, mixed warm/cold retirement, retained
batch admission and nested reservation lifetime.

All eight functional captures completed normally. Final-source
`c06l3-probe-cold` passes all 62 native texture samples, both PSD negative
controls, DDS unsupported-capture fallback and all four callback cases. The new
late callback changes the third image without changing size/timestamp; that image
uses updated native pixels, while the unchanged fourth image reuses its retained
decode. The callback observes zero active workers, source streams and group
handles. The first, second and fourth images produce three actual raw uploads;
at least two retained sources are checked and exactly one is rejected.

Final `c06l3-combined-functional` verifies 76 final textures and 39 uncompressed
cases with zero mismatches/errors, 2,840 decodes, 2,840 consumed results and 2,840
cache writes. Earlier `c06l-probe-warm` checks changed-source warm behavior on
`1b2b72bf`, not the final package. `c06l-combined-functional` was misleadingly
named: its actual mode is standalone `verify-first-build` with cache off.
`c06l-cache-combined-functional` supplies the actual earlier combined check.
Functional durations are diagnostic only and are not performance evidence.

Final measured core and observer source is
`39178efb8c53390f277400742dee053251d317cc`. Core DLL SHA-256 is
`847745c028e10fcfe4f273e23a18f4fa7e68090a4e8d030869ab3d0c857b2743`;
observer DLL SHA-256 is
`ce0450ebe8da74a2bd7ac9fa76f23a0a16fbd885561ff1e1508593aec3f3575a`.
The generation is `gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
Game assembly and Unity CoreModule identities remain those recorded below.
The report closeout commit is separate from this tested source/package identity.

### Final matched measurements

All sixteen final-source measurements use the established 13-entry frozen
selection and 15 active mods, muted profiles, performance purpose, independent
menu observation and normal automatic exit. There are no private probes or
native verification modes in these runs. Every capture passes automatic exit
and records zero texture/cache errors. These are warmed Windows file-cache
conditions. Empty generated cache means first construction, not an OS-cold load.

Each row gives control then C06-on values. The actual order was control/on A,
then on/control B. Capture labels use prefix `c06l3-`.

| Comparison | Menu A, control / on (s) | Menu B, control / on (s) | Texture A, control / on (s) | Texture B, control / on (s) |
| --- | ---: | ---: | ---: | ---: |
| Original PNG native / first-build | 20.580525 / 23.889997 | 19.552889 / 23.807705 | 4.031333 / 7.816618 | 3.918926 / 8.160236 |
| C05-only / combined construction | 35.833782 / 38.387221 | 31.747155 / 37.081134 | 19.873798 / 22.379369 | 16.201482 / 21.366435 |
| Warm cache, C06 off / on | 16.769040 / 17.310531 | 17.125600 / 17.363906 | 1.057492 / 0.990617 | 0.986694 / 0.996568 |
| Ordinary DDS, C06 off / on | 16.512099 / 16.788202 | 16.589388 / 16.559609 | 0.784008 / 1.186196 | 0.688499 / 1.199799 |

Standalone C06 adds 3.31 and 4.25 seconds to menu startup; combined construction
adds 2.55 and 5.33 seconds. Both unchanged-image paths decode/consume exactly
2,840 results, with zero failed/rejected retained results. Scheduled-job counts
can exceed 2,840 because pending plans are discarded; these are not repeated
completed decodes. Combined cache writes also equal 2,840. Recorded queue
reservations peak below 60,000,000 bytes, within the existing eight-job/192-MiB
limit; all queues end with zero active workers and held bytes. This is queue
accounting, not a claim about total process memory.

All four warm runs independently seed the same `c06l3-cache-build-a` cache.
Each has 2,840 hits, zero misses/writes/errors and no cold image uploads.
The generic queue's `decoded` field includes warm record decoding; it must not
be interpreted as 2,840 cold PNG decodes or added to duplicate consumption
counters. Warm texture differences change sign across the pair; no useful C06
gain is established. Both on menus are slower.

Ordinary DDS runs pass through 2,837 DDS textures. C06-on decodes/uploads only
three JPEGs, with peak worker concurrency one and no repeated decode. Their
worker decode totals are 354.808/362.373 ms and owner take totals
374.123/381.562 ms. DDS texture loading is slower by 402.188/511.300 ms even
though complete menu differences are mixed. DDS pass-through is not a speedup.

The final standalone worker decode totals are 9.818/10.382 seconds, owner
preparation totals 1.355/1.446 seconds and owner take totals 3.201/3.560 seconds.
Worker sums overlap owner work and waiting; do not add them to texture/menu
times. These receipts identify remaining decoding and waiting costs, not a new
proven remedy. Broad format coverage, PSD performance, gameplay, later combined
release and separately authorized platform qualification remain open. No new
codec or narrowed JPEG admission is inferred from these failures.

### Restored handoff

Raw evidence is under
`artifacts/ecosystem-next-20260910/c06/lifecycle-decoder-20260913/`.
`all-results.json` records all 28 captures and their source/package/log hashes;
`handoff.json` indexes the bounded result and restoration. Focused-test logs,
build/deploy receipts, selection/settings and each prepare/verify/launch receipt
remain there. The refused `missile.betterloading` exclusion typo created no
transaction; the corrected Gagarin ID was used afterward.

All 36 owned transactions were rolled back in reverse order. `restoration.json`
verifies seven baseline hashes, ten required absences, and preservation of repair
transaction `20260912-170800-04578930` including its file hash/length/timestamp.
The original GOG generation, disabled baseline core/observer, seed profile/order
and original exclusions are restored. No RimWorld, active build/test or assignment
helper remains; child agents are stopped. Parent plan/ledger/state/timer and
normal data were untouched. Checkout ownership returns to the steward for
independent review. No successor, merge, push, publication or platform promotion
was performed or authorized by this handoff.

## Independent qualification review and deeper correction — 13 September UTC

Reviewed clean stopped `f76fdd4953183d032c45f43deb609e9d48025958`. The retained
fixture selector and observer changes pass scoped independent review, and
`src/WakeUp` is byte-identical to measured `72859aeb`. The ineffective runtime
experiment is retired, not adopted. The steward verified all 19 capture manifests,
86 selected evidence files, both measured DLLs, supporting artifact hashes,
31 rollback receipts, seven baseline hashes, ten absences and the retained
repair's hash/size/timestamp. No game or assignment helper remained.

All 14 performance captures use the same retained package. Their evidence
supports the isolated decoder regression, severe repeated construction work,
warm-stage overhead and lack of ordinary-DDS benefit. Four functional captures
support their stated scope; `c06p-fixed-probe-cold` remains failed despite normal
automatic exit. The corrected callback evidence belongs to `82ad39d1`, not the
measurement observer. Independent review is recorded under
`artifacts/ecosystem-next-20260910/c06/performance-20260913/steward-review.json`.
**Performance fails; C06 is not accepted.**

Return two distinct internal corrections to the same owner: separate completed
cold pixels from source-file lease lifetime, and accelerate the existing strict
Huffman decoder with a bounded prefix lookup. The first releases all speculative
handles and quiesces workers before callbacks, keeps bounded completed cold
results, and revalidates actual source content before ordered reuse. It must not
require proving that GPU completion cannot dispatch callbacks. The second keeps
canonical decoding and all malformed-input checks; it changes the algorithm,
not the accepted format or a dependency. See the active plan for exact contracts.

Neither reviewed design is already delivered or measured. Check them separately,
preserve strict native fidelity and all shared C05/C08 behavior, then measure
useful isolated and combined outcomes. The PNG algorithm does not automatically
resolve the measured three-JPEG/DDS-path overhead. Remaining format coverage,
PSD performance, gameplay and combined release checks stay open; no narrow
substitute or performance waiver follows.

## Performance qualification — 13 September 2026

**Result: performance failed; C06 is not accepted.** Preparing image pixels on
background workers made this workload slower than ordinary loading. Combining
that preparation with C05 cache construction also exposed repeated decoding:
cache writes discard queued results before later images can use them. A bounded
correction did not solve that problem and was retired. These are remaining
engineering requirements, not exclusions from C06's promised behavior.

This section supersedes earlier performance-pending statements below; historical
implementation and correctness records remain preserved. Assignment
`wake-up-c06-performance-20260913` began at clean
`7a891dbc28cb7f703cee1b8cbf1c218af1e2f757` on the preserved review branch.
Measurements used the current `owner-unattended-20260913-044927` grant, sole
operating ownership, stopped RimWorld processes and no competing build or agent
work. The steward retains independent review, acceptance and successor ownership.
No Steam/Deck qualification, merge, push or publication occurred.

### What was actually measured

All fourteen measurements used the same retained core and observer source
`72859aeb3e83a7bce287cfba2f4e93cf839c66b6`, GOG generation
`gog-rev573-20260910-194017-cf1a1eb0`, and generation manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
Core DLL SHA-256:
`0fa1a8c59def126eb2754e64ec7d63ae3255071b417df1f456295224f011893e`;
observer DLL SHA-256:
`29d36a3d5511f0035564578835d992299f951fba688bb2eb53657d723361049e`.
The GOG game assembly was
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`;
Unity CoreModule was
`c5c58ea254834291780a1d6c388c241443d07167b4a4b890a23c9494f626ddba`.

The established large workload has 13 frozen selection entries and 15 active
mods, with 2,837 PNG counterparts and three JPEGs. Original-PNG selection is an
explicit fixture variant; ordinary DDS precedence is measured separately.
Profiles were muted. Measurements used performance purpose, independent menu
observation, normal automatic exit, no private texture probes and no extra
native pixel verification. All fourteen captured `automaticTestPassed: true`,
exit code zero and zero texture/cache errors. These are warmed Windows file-cache
conditions; an empty generated texture store does not mean an OS-cold disk load.

The table follows measurement order. Menu is complete startup to the independent
menu observation; texture stage is the recorded `reloadMs`, converted to seconds.
Labels below have the common prefix `c06p-` in the retained capture directories.

| Capture suffix | Menu seconds | Texture-stage seconds |
| --- | ---: | ---: |
| `png-native-a` | 19.970225 | 3.880003 |
| `png-first-a` | 25.305413 | 9.685396 |
| `png-first-b` | 25.481869 | 9.643476 |
| `png-native-b` | 19.630769 | 3.825416 |
| `cache-build-baseline` | 33.291546 | 17.271872 |
| `combined-build-baseline` | 146.563942 | 130.544706 |
| `warm-off-a` | 16.982769 | 1.114195 |
| `warm-on-a` | 16.984024 | 1.435882 |
| `warm-on-b` | 16.718523 | 1.064083 |
| `warm-off-b` | 16.745417 | 0.835589 |
| `dds-off-a` | 16.734790 | 0.715173 |
| `dds-on-a` | 16.928908 | 1.227717 |
| `dds-on-b` | 17.506068 | 1.332551 |
| `dds-off-b` | 16.433843 | 0.655898 |

Standalone native/first-build, warm off/on and DDS off/on use forward and reverse
controls. The construction pair is one bounded off-to-on comparison, not a
replicated average: its severe failure and the diagnosed repeated work justified
stopping redundant losing arms with steward agreement.

Standalone C06 adds 5.335188 and 5.851100 seconds to menu startup in the two
pairs. Both C06-on runs upload all 2,840 worker-decoded images, without generated-cache
hits or writes. Two workers and an eight-job horizon operate within the shared
192 MiB reservation limit; the largest combined queue reservation was 59,900,019
bytes. Summed worker decode time is 17,905.083 / 17,887.462 ms; owner take time is
8,047.981 / 8,046.123 ms. Owner take includes admission and waiting, not pure
waiting. Worker time overlaps across workers and the main thread; these costs
must not be added to stage or menu elapsed time. No fine-grained CPU or first-use
compilation attribution was performed.

C05-only and combined construction both begin with an empty store and write
2,840 images: 332,443,410 payload bytes, 333,497,002 stored bytes, from
100,032,074 source bytes. The combined arm takes 113.272396 seconds longer to
menu. It schedules and decodes **22,608 jobs for 2,840 actual raw uploads**.
Its final lease-consumed counter is zero because capture's store mutation releases
the current lease before the consumption marker; this does not mean worker
results were unused. The raw-upload counters prove their use. Future queued
results are discarded and decoded again. Summed worker decode time is
144,106.727 ms and owner take is 19,032.162 ms, with the same overlap caveat.

Each warm arm independently seeds the same `cache-build-baseline` store. All four
perform 2,840 owned warm uploads, **zero cold raw uploads**, and zero misses,
writes or errors. Thus no cold decoding leaks into warm reuse. Menu differences
are negligible and inconsistent, while C06-on texture stages are 0.321687 and
0.228494 seconds slower. There is no demonstrated benefit or zero-overhead claim.
The largest warm reservation is 35,648,613 bytes.

Ordinary DDS arms use normal user-settings activation: cache and prepared textures
off, PSD support on, shared cache 1,024 MiB; only `firstBuildImages` changes.
All 2,837 DDS images pass through the native loader. C06-on decodes only the three
JPEGs, with a maximum reservation of 14,681,820 bytes. Its menu penalties are
0.194118 and 1.072225 seconds; texture-stage penalties are 0.512544 and 0.676653
seconds. These results do not establish a DDS decoding contribution. All queue
reports finish with zero held bytes and no active workers.

### Tested correction, retirement and retained tooling

The cache store calls `BeforeMutation` before operations that can alter stored
data. The shared texture session currently handles that event by draining its
queue: waiting for workers and discarding prepared results. This protects existing
cache readers, but source-only cold jobs do not depend on the generated store.
Draining them at each capture repeatedly destroys the look-ahead work.

The bounded candidate retained cold-only jobs across store mutation only while no
warm job had ever been scheduled and existing compatibility checks still held.
Independent review identified two real boundaries: a patched raw-pixel getter
can invoke another mod, and GPU capture reaches additional engine callbacks.
The candidate checked the raw-getter guard and explicitly drained before GPU
capture. That safe restriction left only 39 of 2,840 leases surviving: the combined
functional run still scheduled 22,335 jobs. It did not resolve the product defect.
No candidate performance claim was made from these functional timings.

Candidate history is preserved in `f35fe0ef`, `b4cf51c4` and `82ad39d1`.
Commit `92bbab53f66c8fd1a9dded92944f501175e9ebb6` retires both runtime-file changes;
**all `src/WakeUp` product source is byte-exact to retained `72859aeb`**.
The retired diff is also saved as `cpu-only-candidate.patch` under the evidence
root. No partial correction, new default, reduced coverage or backend adoption
remains in the product.

The narrow fixture amendment remains: `--first-build-images` selects the existing
runtime argument alongside explicit candidate PNG modes, persists it in the run
manifest, and rejects unsupported activation or manifest drift. It defaults off
and changes no runtime selector. Focused fixture tests passed 67 cases; the later
managed/fixture-tool check passed 172 managed and 76 fixture-tool cases.

The observer retains a fourth callback proof, `capture-after-first-item`, which
patches the actual non-generic `Texture2D.GetRawTextureData` getter, rewrites the
following private PNG once, then reads actual pixels through the equivalent
generic getter. The non-generic native method has no managed body; ordinary
Harmony unpatch produced invalid IL in `c06p-fixed-probe-cold` at `b4cf51c4`.
That run exited normally and passed the automatic menu/exit check, but its
texture probe **failed**. The failure is retained, not counted as correctness.
The corrected observer uses an extern-compatible replacement body, disarms the
rewrite after the private case, and keeps the equivalent getter until process
exit instead of attempting the invalid unpatch.

At `82ad39d1`, `c06p-fixed-probe-cold-02` and `c06p-fixed-probe-warm` pass all
62 requested samples and all four callback cases. The changed-source warm run
has 47 hits, two rebuilds and two warm refusals. The new observer evidence belongs
to that retired candidate pair, not to the older observer used in measurements.
`c06p-combined-functional` at retained `72859aeb`, and
`c06p-fixed-combined-functional` at candidate `82ad39d1`, each pass 76 native
comparisons plus 39 uncompressed checks, without mismatch/error. Existing C05
authenticated-buffer correctness evidence was reused for unchanged paths.

### Remaining engineering and coverage

Two distinct improvements remain necessary. First, the strict PNG decoder's
one-bit Huffman walk is a credible tuning target: a compact prefix lookup with
canonical fallback could reduce work. This was reviewed but neither implemented
nor measured. It must preserve short end-of-block handling, buffered bytes across
stored-block alignment, valid final-block/tree/history/output checks, and native
error behavior. Replacing it with the earlier .NET inflater would reopen the
demonstrated missing-final-block acceptance defect.

Second, repeated construction work needs a complete lifetime design for prepared
pixels and the source-file locks that protect them. Options include separating
those lifetimes with source revalidation before publication, or a fully justified
GPU callback contract. Simply guarding four GPU-related types is insufficient:
the reviewed engine path also includes format helpers, fallback logging,
`Graphics.CopyTexture`, native array copying and readback completion. Whether
native `WaitForCompletion` can dispatch unrelated pending callbacks remains open.
Holding future source locks across that boundary is not established safe.
These are credible next investigations, not delivered improvements.

Measured coverage is the 2,837-PNG/three-common-JPEG workload above. Unsupported
direct-RGB/unusual JPEG families and their native fallback obligations remain
open; they are not waived. Optional PSD has functional evidence but no PSD
performance measurement here. Broader first-use behavior, gameplay compatibility,
combined release acceptance and later platforms remain unqualified. Deferred
C10/C11 and companion work were not resumed. C05's separate DDS deficiency,
C14 correctness and C15 consolidation retain their own ownership.

### Evidence and restored handoff

Raw records are under ignored
`artifacts/ecosystem-next-20260910/c06/performance-20260913/`:
`preflight.json`, selection/settings, individual preparation/deployment/verification
receipts, `all-results.json`, test/build logs, the retired candidate patch,
`restoration.json` and `handoff.json`. Exact run manifests and texture/probe logs
are captured under `.rlo-test-instance/results/c06p-*/`; the aggregate records
their hashes, package identities, settings, arguments, order and observations.

Restoration completed at **2026-09-13 09:18:37 UTC**. All 31 transactions owned by
this assignment were reversed through `fixture.py`. Seven baseline file hashes,
ten expected absences and the retained repair's hash, length and timestamp match.
Repair transaction `20260912-170800-04578930` remains preserved. Original
exclusions, 315-mod baseline profile and disabled baseline core/observer are
restored; no prepared run remains. No RimWorld or assignment helper is running.
The restored DLL identities intentionally differ from measured packages: core
`e507575091f92f2b0d1882e828943666d3557cc7c8b0fc4487f2068b590b6f68`, observer
`b1fa3c6541529ef92763af2599c59defbf8ffeb484b267d9c0c6c273b3ed0165`.
No further operations or successor dispatch are planned by C06. Independent
steward review remains required; a normal capture does not override the failed
performance qualification.

## Historical implementation record — 11 September 2026

C06 implementation is returned for independent steward review under dispatch
`wake-up-next-c06-5f16dbd-20260911`. This task held exclusive ownership of the
single checkout, builds and isolated GOG fixture operations from clean
`5f16dbd4ea85fed4b91579cecaabe2d04f253d58` on
`codex/ecosystem-next-plan-review`. The assigned prior team is stopped; initial
process inspection found no RimWorld, Python or dotnet process. Approved
`f70aa08`, preserved `fe687d4` and corrected C05 `b847d20` are ancestors.
Main stays unmerged. The steward owns acceptance and successor dispatch.

The deliverable is concurrent CPU image decoding whose pixels are consumed
directly during an empty-cache load. Native engine creation, mip generation,
compression, sampling and ordered holder publication remain on the main thread.
The starting seam replaces native `LoadImage` with raw pixel assignment while
retaining native finalization. It does not expand PNG into another PNG, run an
offline encoder as a substitute, or count DDS as decoding. C05 owns completed
storage; C07 owns atlases and C08 owns explicit changed-quality preparation.

PNG/JPEG native equivalence and the separately approved PSD decoding contract
remain distinct. A narrow proven family is an increment; unsupported families
remain open. No new runtime dependency or automatic helper execution is enabled
without a concrete decision through the steward.

The performance grant expired at **2026-09-11 12:00 UTC**. Only authorized GOG
functional checks may run; no codec cost probes, microbenchmarks or performance
experiments are authorized. Later matched empty-cache, C05 warm, disabled/enabled
and combined measurements must include reads, decode, waiting, publication,
storage and complete startup costs. Functional durations are diagnostic only.

The assigned original GOG endpoint is recorded in
`artifacts/ecosystem-next-20260910/c03-qualification/restored-identities.json`.
Before handoff restore and rehash those seven files, original exclusions/profile,
disabled candidate/observer and absent settings/prepared pointer/helper. Keep
research and raw evidence under ignored `artifacts/`. No normal data, other apps,
Deck, Steam promotion, UI automation, push, merge or publication is permitted.

## Active implementation and native findings

The first implementation `5e8f9be` adds two private workers, an eight-job horizon
and 192 MiB total source/decoded/scratch reservations. Source files remain leased
against writes until their result is consumed or discarded. A disposed iterator
joins every worker before releasing buffers. Native holder order remains intact,
and hot reload uses ordinary loading. Existing C05 store owners suppress speculative
decoding on warm starts; a stale entry still receives exact source validation and
ordinary reconstruction. PSD uses its existing decoder once on a worker and its
unchanged main-thread creation/capture path.

Initial checks passed 97 managed and 63 fixture cases; observer compilation passed.
These do not establish native image fidelity. Functional capture
`c06-native-functional-01` consumed five worker results (one PNG and four PSDs),
with two workers and no held resources after disposal. It found native PNG uses
**ARGB32**, not the initially assumed RGBA32. Four PSD cases matched their existing
approved producer. `e68f4cd` corrects PNG byte order without a second pixel buffer.

Capture `c06-native-functional-02`, core `c71ae7d`, observer `f0a22aa`, confirms
the corrected initial PNG base pixels, complete final bytes/descriptors and actual
holder mips, plus all four PSD cases. It intentionally remains a failed functional
probe: a PNG with a missing final DEFLATE block was accepted by .NET's inflater
and rejected by Unity. Both accept trailing bytes after a complete compressed
stream. A strict single-pass DEFLATE reader is being added for that demonstrated
error-behavior difference; no second validation decode is planned.

All durations in these captures are diagnostic. No performance measurement or
native-preserving JPEG claim has been made. The larger PNG-selected functional
check retains its explicit source-selection distinction from ordinary DDS loading.

### Functional checkpoints through 08

These are correctness checks on the isolated GOG generation, not speed evidence.
Every listed run exited normally and captured `automaticTestPassed: true`.

| Capture | Source/package and useful result |
| --- | --- |
| `c06-png-selected-03` | Core `c71ae7d`, observer `f0a22aa`; deliberately selected original PNG counterparts. Consumed 2,569 worker results, 76 native comparisons, no mismatch/error. This is distinct from normal DDS precedence. |
| `c06-family-functional-04` | Core `1b37f0c`, observer `8512b64`; strict DEFLATE ending controls now agree with native. Twenty-three generated PNG encoding/metadata cases and four PSD cases match base/final data as applicable. JPEG candidate differences remained outside startup admission. |
| `c06-family-functional-05` | Core/observer `218d07a`; ordinary settings, native DDS precedence, empty cache. All 28 queued PNG/PSD results consumed, two workers, zero retained bytes/workers, 31 cache writes, zero errors. All 44 requested observer samples passed. |
| `c06-warm-functional-06` | Same packages/settings, prior owned store restored. 31 hits, zero misses/writes, no image worker queue created. All 44 observer samples passed. |
| `c06-changed-functional-07` | Same packages/settings/store, one private PNG and one PSD changed without changing source length/timestamp. 29 hits, two misses/writes, no errors; all 44 observer samples passed. Stale owned slots deliberately use native reconstruction after exact source validation. |
| `c06-color-functional-08` | Core/observer `4b18f4a`; added DDS callback barriers, common constructor guard, ICC sample and candidate JPEG IDCT correction. Grayscale JPEG now matches exact native base and complete final output; valid ICC PNG also matches. Color JPEG remains under investigation and is not admitted. |

The queue now drains before every DDS item and resumes only after its callback.
This preserves local PNG/PSD progress without holding speculative source locks
across another loader's possible callback. The common internal Unity texture
constructor is guarded alongside the two replaced public constructors.

The JPEG difference was not dismissed as visual similarity: the actual grayscale
image differed by one channel level in 4,317 byte positions. The same pinned
inverse-transform factorization with 13-bit signed-rounded constants reproduces
all 3,145,728 native base bytes. No libjpeg source was imported. Exact native
color reconstruction, ordinary JPEG queue consumption, and a forced upload
fallback check remain open at this checkpoint.

The focused codec check at `75ced4e` passed 73 managed tests. Its supplementary
unchanged fixture suite encountered two Windows access-denied directory renames
in disposable generation-transition tests; earlier checks passed all 63 fixture
tests. This is recorded separately from successful product builds and live
functional captures; no security or normal application state was changed.

### Consumed JPEG and complete family checks

`c06-jpeg-families-09` at core/observer `94efb6d` passed the corrected common
JPEG families, including baseline/progressive grayscale and YCbCr 4:4:4, 4:2:2,
4:2:0, EXIF orientation, ICC metadata and restart markers. Both natural JPEGs
matched exact base bytes, complete final native mip bytes/descriptors and actual
holder rendering. A deliberately empty private upload exercised the real Unity
refusal: ordinary loading recovered the same final bytes/descriptors and the
successful prepared-upload counter stayed unchanged.

`c06-jpeg-workers-10` at core/observer `a01f19a` then exercised ordinary first-build
JPEG consumption. It consumed 46 worker results: 25 PNGs, 17 JPEGs and four PSDs.
Two workers were used where independent images were available; all queues ended
with no held bytes or active workers. The 2,845 DDS sources retained normal
precedence. All 60 requested observer samples passed, including newly added
3x5, 4x3 and 1x1 JPEGs. The CMYK negative control was refused by both first-build
admission and native `LoadImage`; no native-preserving CMYK claim follows.

Runtime admission covers the qualified PNG families, common grayscale/YCbCr
JPEGs and separately selected PSD composites. JPEG component IDs, frame type,
sampling and both early/late metadata are checked. Direct RGB, CMYK/YCCK, unusual
sampling, unsupported frames and sources outside the existing C05 size boundary
retain ordinary behavior. Worker memory includes source, pixels, component planes,
progressive coefficients and scratch under the shared 192 MiB bound. These are
encoding restrictions, not a mod/source allowlist or a claim that all JPEG
variants are implemented. The feature remains default off.

At `a01f19a`, all 84 focused managed checks passed. The unchanged fixture suite
passed all 63 checks at `94efb6d`; the subsequent invocation repeated the two
Windows temporary-directory rename failures. Independent read-only review found
no remaining concrete issue in queue barriers, constructor guards, fallback,
JPEG reservation/admission or consumption. Parent review and all performance
acceptance remain separate.

## JPEG source authorization interpretation

Steward relay `c06-jpeg-same-bundled-source-20260911` interprets the existing owner
approval of bundled StbImageSharp source and approved JPEG scope as covering a
bounded JPEG adaptation from the **same 2.30.15 commit
`6fd7aebe1dbf10e28d78745f42a0c095c61d1945`**, compiled into WakeUp.dll with notices.
This is the steward's interpretation of existing authorization, not a new owner
response or general dependency grant. Users install no separate library, helper,
framework or runtime download. The PSD opt-in and output identity stay separate.

The existing Windows helper uses WIC; its version-2 output is converted BC1/BC3
mips, not a JPEG base-pixel handoff. Its libjpeg-turbo dependency is currently
Linux-only. Automatic helper execution or an in-process native DLL remains a new
material decision. The managed JPEG producer must prove exact Unity base pixels
and native-finalized all-mip/descriptor equality before admission. Different JPEG
inverse-transform/chroma rounding is an open risk, not a permitted quality change.

## Final package, evidence and returned ownership

Final build source is `17ee2fec2517aa67b8c7be486b4b48f2103ac430`, runtime DLL
SHA-256 `c2b05caecd0c20e072e5fd8516cc6ab9279c7a3104a8f2b19371962d459a6186`.
The observer source is `a01f19a3c7ea674840a5125f479573d3c8a2e042`, DLL
`8b8b3b6bf080169a0c38daa360b280b94b41e3529507c961cedb3207d1b0ee42`.
Final `c06-final-cold-11` and `c06-final-warm-12` both normally exited and captured
`automaticTestPassed: true`, with all 60 requested observer samples passing.
Cold consumed 46 worker results; warm had 47 hits, zero misses/writes/errors and
no worker queue. The attempted cross-revision warm seed was refused during
preparation, so final warm evidence uses its own exact-package cold seed.

The local package is under
`artifacts/releases/0.3.0-rc.2-17ee2fec2517aa67b8c7be486b4b48f2103ac430/`;
ZIP SHA-256 `d6fd51d8f873027f497a7cb7d7675eee5c1a502c36912b9d891e28970f32c67f`.
It contains the unchanged tested runtime DLL, exact tracked decoder/queue source,
PSD/JPEG contracts and license, with those notices also verified in the DLL.
No helper or other runtime component is included. Six packaging tests passed.
The source archive excludes private fixture/evidence paths. No publication occurred.

`artifacts/ecosystem-next-20260910/c06/handoff.json` consolidates selected capture
identities/counts, source-package proof and restoration. `restored-identities.json`
verifies all seven original endpoint hashes. Original GOG generation, core
`ba17b03`, observer `3d86b16`, three exclusions, seed profile/order and disabled
candidate/observer are restored; settings, prepared pointer and helper are absent.
No RimWorld process or active build/test/fixture operation remains. An idle
MSBuild reuse worker was left alone. Child tasks are complete. Checkout ownership
returns to the steward; parent timers/state and normal data were not modified.

Performance remains unmeasured and full acceptance remains open. The preserved
obligations are matched forward/reverse disabled/enabled empty-cache loading,
existing C05 warm composition, per-stage and complete menu costs, memory/waiting/
publication costs, individual feature settings and later combined behavior.
Measure only in a new explicit unattended window. No claim follows about general
gameplay, unqualified JPEG variants, Windows Steam or Linux/Deck. The steward owns
independent review and any successor; corrections return to this same owner.

## Steward review: corrections required — 11 September 2026

Reviewed clean handoff `7a1f03fb71595d83e8b0e12922ee26e15a433bd8`, tested
product `17ee2fec2517aa67b8c7be486b4b48f2103ac430` and observer `a01f19a`.
Actual worker consumption and cold/warm reuse are present. Independent source
review found two correctness/compatibility blockers; C06 has not passed review.

- **P1: a mod callback can encounter future image files locked by the queue.**
  `PngRuntime.cs` constructs `LoadedContentItem<Texture2D>` while future source
  leases remain open with `FileShare.Read`. Its constructor is absent from the
  first-build callback guards. A foreign constructor callback that replaces the
  following PNG can therefore fail or lose its native effect. Guard the actual
  closed generic constructor and refuse/drain speculative work at this boundary.
  Demonstrate the native callback effect is preserved with no future file lock.
- **P2: Adobe JPEG metadata is parsed with the wrong identifier length.**
  Both `FirstBuildJpegDecoder.cs` and its `.Stb.cs` parser treat the marker as
  six-byte `Adobe\0`. APP14 has five identifier bytes followed by a two-byte
  version. A nonzero version high byte can hide transform 0 and admit explicitly
  unsupported direct RGB as YCbCr. Correct early and late parsing and the bundled
  contract, and extend the focused marker/admission cases. Admission bypass is
  source-proven; native pixel divergence has not been measured or asserted.

The PNG/DEFLATE review found no additional concrete blocker. The steward verified
nine packaged source files against the exact tested git commit, the packaged DLL
and ZIP hashes, and all seven restored fixture endpoint hashes. The package proof
is `artifacts/ecosystem-next-20260910/c06/steward-package-review.json`. Prior
functional captures remain attributed to their individual builds; normal menu
success alone does not resolve these findings. Unsupported JPEG families remain
explicit native fallbacks and open coverage, not quietly accepted exclusions.

Corrections return to the same C06 orchestrator, with focused functional checks
and a clean restored handoff before re-review. No C07 dispatch yet. The owner is
back on the PC: **performance tests, microbenchmarks and performance experiments
are stopped until another explicit unattended window**. Standing isolated GOG
functionality/correctness tests and necessary variants remain authorized. The
12:00 UTC window expiry already stopped measurements; this owner instruction
reinforces it. No change to Steam/Deck, normal-data or publication boundaries.

## Callback and Adobe corrections returned — 11 September 2026

The two reviewed problems are corrected. A mod's image-holder constructor
callback can replace a following image without colliding with speculative file
locks or losing its replacement pixels. JPEG files declaring direct RGB through
Adobe metadata remain on ordinary native loading, including versions whose high
byte is nonzero. These are correctness results; speed remains unmeasured.

The same owner resumed clean `2987e36c53455223589226408d685695f9ee67a2` under
`wake-up-c06-review-callback-adobe-20260911`. Product correction `8f132d2` guards
the actual closed `LoadedContentItem<Texture2D>(VirtualFile, Texture2D,
IDisposable)` constructor across all Harmony patch kinds. Queue admission checks
it, and publication checks it again after image loading. A newly published
foreign hook drains current/future leases before entering that constructor;
ordinary fallback and error-holder construction use the same boundary.

Both JPEG readers now consume five identifier bytes, two version bytes, two
two-byte flag fields and the transform byte at offset 11. This agrees with the
primary [libjpeg-turbo APP14 reader](https://github.com/libjpeg-turbo/libjpeg-turbo/blob/main/src/jdmarker.c).
No additional decoder source or runtime dependency was imported. Tests cover
early/late markers with version high bytes 0, 1 and 255. Transform-zero files
remain refused before production raw-pixel publication. The bundled JPEG
contract documents the correction; unsupported families remain native fallbacks.

Focused checks pass **82 managed tests and 63 fixture-tool tests**. Live GOG
callback checks compare native loading with a hook present before queue creation
and one installed after the first item. Each callback writes the following PNG
once; native final output and changed-source cache contents match. The before
case consumes zero worker results; the late case consumes one and then drains.
Both generated Adobe high-version files remain unadmitted and match native base
pixels, final mip bytes and texture descriptors.

The first correction capture `c06-review-cold-01` is retained as **failed**. Its
three callback cases passed, but the new observer probe reopened the shared
startup cache and left it owned, preventing subsequent storage checks. Observer
correction `7b24047` gives both cache entry points the same private probe store
and restores their state. `c06-review-cold-02` then passed all 62 samples using
core `8f132d2` and observer `7b24047`; this intermediate identity is separate from
the final rebuilt package below. No extra product correction followed that
observer failure.

Final core and observer source: **`7b2404770995fe85d1137367149c44ea9b997ae6`**.
Core DLL SHA-256:
`70ef4f98752069710f9593d68503ef65949e95a87bbb634a6dbde98348333ae9`.
Observer DLL SHA-256:
`222999d26641622d7d17d5abf65cc71d29527e54aa2f4f4ab78bd5c7e0f121a8`.
Final `c06-review-final-cold-03` and `c06-review-final-warm-04` both exit normally
with `automaticTestPassed: true` and all 62 samples passing. They use ordinary
user settings `firstBuildImages=true`, `psdSupport=true`, `pngCache=true`,
`preparedTextures=false`, the existing large selection and native DDS precedence.
Cold consumes 46 worker results (25 PNG, 17 JPEG, four PSD), with 49 cache writes.
Warm reuses those 49 entries with zero misses/writes/errors and no startup queue.
The observer's later private callback queue is recorded separately from startup.
Every queue ends with zero active workers and held bytes. Functional durations
are diagnostic only and do not qualify performance.

The local source-inclusive ZIP under
`artifacts/releases/0.3.0-rc.2-7b2404770995fe85d1137367149c44ea9b997ae6/`
has SHA-256 `54b27372afaf5aa125bcf0778bf38ed142005b88f328cd0fe2a8d78a1a82683f`.
The package DLL equals the tested DLL; eleven selected source/contract files
match committed archive bytes, all three PSD/JPEG/license notices are distributed
and their complete bytes are embedded in the DLL. Private paths and helper
components are absent. Proof and consolidated captures are in
`artifacts/ecosystem-next-20260910/c06-review/source-package-proof.json` and
`handoff.json`.

All seven original hashes in `c06-review/restored-identities.json` match the
starting endpoint: GOG generation/game, core `ba17b03`, observer `3d86b16`, seed
order/preferences and original three exclusions. Wake-Up settings, prepared
pointer and helper are absent. No RimWorld process or active build/test/fixture
operation remains. The interrupted decoder child never initialized or edited;
an idle MSBuild reuse worker is left alone. Exclusive checkout ownership returns
to the steward for independent re-review, with this owner available for further
corrections. Parent coordination state/timer and normal data were untouched.

No performance test, microbenchmark, Steam promotion, Deck access, UI automation,
merge, push or publication occurred. All previous individual and combined
measurement obligations remain pending for a new explicit unattended window.
This handoff does not claim independent acceptance or authorize C07.

## Steward functional re-review — 11 September 2026

**Functional re-review passes for the delivered first-build image pipeline;
performance and full acceptance remain pending.** Reviewed clean handoff
`ddd2287689c29011786d2e4140e9b722309f1fa4`, correction base `2987e36`, and
final tested core/observer `7b2404770995fe85d1137367149c44ea9b997ae6`.
Both requested corrections are resolved in actual code. Independent callback
review confirms admission and pre-constructor drainage, including newly published
hooks. Both JPEG marker readers now consume the correct field boundaries.

The steward independently checked the two final captured run identities and
JSONL hashes, all 62 sample results, three native/before/late callback effects,
early/late Adobe native fallback and output equality. Actual startup receipts
show 46 consumed worker results on construction and 49 warm hits without a
startup queue; private post-startup observer activity was kept separate. The
source-inclusive ZIP, tested DLL and eleven committed source/contract files
match, as do all seven restored endpoint hashes. No RimWorld process remained.
Raw review: `artifacts/ecosystem-next-20260910/c06-review/steward-correction-review.json`.
Existing focused tests are adequate; no repeat build, fixture run or measurement
was needed for this review. The failed observer-only first correction capture
remains recorded as failed and is not included in the passing evidence.

Coverage remains explicit: ordinary supported PNG and qualified common JPEG
families use actual worker output, optional bundled PSD remains the separately
approved new composite decoder, and unsupported families keep native behavior.
The earlier broad PNG-selected capture remains attached to its earlier build.
This passes the implemented behavioral increment, not every format or supplier
removal. Direct RGB/unusual JPEG formats and other unqualified families remain
open coverage with C06; no exclusion is approved here. Atlas work in C07 and
quality integration in C08 remain separate required behavior.

C06's original owner retains future coverage/correction and performance duties:
matched native/first-construction/warm comparisons, isolated and combined with
C05, PNG-selected and ordinary DDS workloads, complete loading/first-use costs
and resource bounds. Native DDS pass-through is not a new C06 speedup. The owner
is back on the PC; no performance tests or cost probes until a new explicit
unattended grant. No full acceptance, release or platform promotion follows.

Returned stopped ownership and this functional review permit C07 dispatch from
the ensuing exact review commit, preserving all C01–C06 code and open obligations.
The same C06 orchestrator remains available; no earlier owner is awakened while
C07 owns edits/builds/fixture work.
