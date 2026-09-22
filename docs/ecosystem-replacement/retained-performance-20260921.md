# Retained GOG performance handoff — 21 September 2026

## Final continuation: progress-display cost with atlas options off

The existing progress display adds **0.914 seconds / 7.07%** to this fresh-cache
configuration. Both comparison directions agree. This is the cost of enabling the
display and its existing observation/scheduling behavior together, not an isolated
cost of drawing pixels or a claim that the display should make startup faster.
The visible progress information remains a user-facing function with this measured
tradeoff. No redesign, default change or removal was performed.

Assignment `wake-up-final-display-cost-20260921-1ae195e0` started clean at
`1ae195e0fa374f615a08df2fecaaaa509756a30b`. Exactly four fresh-cache performance
launches ran in **on/off/off/on** order on unchanged core `90844467`, observer
`0412c65d`, and the same ten-package English native-DDS content. Settings matched
the preceding no-atlas combined configuration; only `loadingDisplay` changed.
Both atlas options and both diagnostic options stayed false. No cache carry,
extra pair, warmup, probe, build or additional setting-isolation run occurred.

| Exact labels | Raw menu seconds | Mean / range |
| --- | --- | --- |
| `perf21-finaldisplay-on-a`, `perf21-finaldisplay-on-b` | 13.944610, 13.742143 | **13.843377**, 13.742143–13.944610 |
| `perf21-finaldisplay-off-a`, `perf21-finaldisplay-off-b` | 13.012217, 12.846233 | **12.929225**, 12.846233–13.012217 |

Paired display costs are **0.932393** and **0.895910 seconds**. Live on-run receipts
show display installation/completion, three stage changes, and `batching-installed`
for its shared scheduler. Both off runs have neither atlas/scheduler nor display
receipt files. Together with the already inspected selection/source boundary,
this confirms that disabling display removes that independent scheduling path when
both atlas options are also off. There was no other observed path requiring it.

All four runs constructed their fresh processed-XML prefix and restored no cache.
Texture receipts again report 2,853 native DDS pass-throughs with zero PNG/JPEG
decode calls or texture-cache hits/writes. The off mean remains numerically about
0.985 seconds above the **preceding batch's** ordinary-default mean of 11.944467 s;
this final four-run comparison has no new default arm, so that cross-batch residual
is context, not a newly isolated or precisely attributed feature cost. Do not
subtract this display result from older combined differences as additive savings.
Residual cause and other interactions remain unresolved. Measurement iteration is
now stopped as assigned; no extra pair or next experiment is proposed as authorized.

All four runs passed independent menu observation, normal exit 0, capture and
`automaticTestPassed`; the last capture was **08:49:39 UTC**, within the same
overnight grant. External surviving-process CPU averaged about 1.21–1.41 cores
across 32 logical processors; no observed contested sample was excluded, subject
to the monitoring limitations below. All six new transactions were restored;
actual seven hashes, eleven absences, repair hash/size/timestamp, audit and stopped
process checks pass. The prior 63 captures and evidence manifests remain unchanged.

Private evidence:
`artifacts/ecosystem-next-20260910/overnight-20260921/performance-final-display/`,
including `summary.json`, all four run hashes/receipts, on/off settings, exact
command logs, `restoration.json`, evidence hashes and stopped `handoff.json`.
Documentation-only handoff returns ownership to the parent for independent review,
honest private-package/readiness metadata and outstanding owner decisions. Runtime,
defaults, capability scope and existing archive are unchanged. Atlas retirement
and platform/publication approvals remain separate.

## Follow-up: combined options without atlas — stopped handoff

Turning off atlas caching and batching removes most of the combined slowdown, but
the remaining optional configuration still loses to matched ordinary defaults.
The warm configuration takes **1.548 seconds / 12.96% longer**; fresh construction
takes **1.683 seconds / 14.09% longer**. Stop here and return the residual to the
steward. No additional isolation loop, redesign, default change or atlas retirement
was performed. The owner scope decision remains separate.

Assignment `wake-up-combined-without-atlas-20260921-77ae6d0d` resumed clean
`77ae6d0d2e779d87837f21436b18cbc7ecfc6f6e`. It used exactly six performance launches,
the same ten-package English native-DDS selection, unchanged core `90844467` and
observer `0412c65d`, the same current overnight authority and all prior guards.
There were no builds or additional warmups. The only differences from the prior
`combined.json` were `staticAtlases=false` and `atlasBatching=false`; loading display
remained on, detailed timing/display diagnostics remained off, and all other
settings were identical. Ordinary-default controls supplied no settings override.

| Exact run labels | Raw menu seconds | Mean / range | Difference from matched defaults |
| --- | --- | --- | --- |
| `perf21-noatlas-defaults-a`, `perf21-noatlas-defaults-b` | 12.034720, 11.854213 | **11.944467**, 11.854213–12.034720 | — |
| `perf21-noatlas-fresh-a`, `perf21-noatlas-fresh-b` | 13.801396, 13.452730 | **13.627063**, 13.452730–13.801396 | +1.682596 s / +14.09% |
| `perf21-noatlas-warm-a`, `perf21-noatlas-warm-b` | 13.379677, 13.605346 | **13.492511**, 13.379677–13.605346 | +1.548045 s / +12.96% |

Order was defaults/fresh/warm, then warm/fresh/defaults. Both warm runs restored
only the newly populated processed-XML store from `perf21-noatlas-fresh-a`.
There was no atlas restore or incompatible/empty-store substitution. Actual warm
receipts show 73 skipped top-level calls / 139 admitted operations and native
continuation at the unsupported VTE custom operation. All four combined runs show
**2,853 native DDS pass-throughs, zero PNG/JPEG calls, zero texture-cache hits or
writes, zero quality applications and zero helper starts**. Atlas settings are off;
no atlas reuse or group-batching completion/cache traffic occurred. The atlas log
still records `batching-installed`: source confirms the independently enabled
loading display installs these shared scheduling hooks through
`EnableDisplayScheduling`. It can yield between native loading units even when
`AtlasRuntime` reuse/batching options are off. This is retained display behavior,
not evidence that the two requested setting overrides failed. Fresh processed XML
captured its record; warm reused it. The warm-versus-fresh mean difference is only
0.135 seconds and changes sign in the two pairs, so it is not a separately qualified
processed-XML speedup here.

A concrete **untested residual hypothesis** is the display's retained observation
and native-unit scheduling cost: its `batching-installed` receipt and the existing
`LoadingDisplayRuntime` / `AtlasBatchScheduling` source prove those hooks remain.
Disabling the two atlas options therefore does not restore default loading
scheduling. A matched display-off check under these new no-atlas settings could
isolate that existing behavior if the steward finds the question useful. Another
possible contributor is setup/validation in the enabled image-cache/preparation
path on DDS-only content with no reusable texture work. Existing receipts show
that path selected, 12 native reloads and roughly
0.592–0.695 seconds inside those reload boundaries despite zero cache work. These
durations include native work and do **not** measure incremental overhead or explain
the full 1.55-second residual by themselves. Other enabled paths/interactions remain
possible contributors. No further test is implied or started; these are hypotheses,
not measured cost attribution.

All six runs reached the independent menu endpoint, exited normally with code 0,
captured and passed `automaticTestPassed`. Surviving-process background CPU totals
were approximately 1.22–1.36 cores out of 32 logical processors, within the earlier
steady load; no observed contested sample was excluded. The same limited-monitoring
caveat below applies. All eight new transactions were restored, and fresh seven
hashes/eleven absences/repair hash-size-timestamp/metadata audit/stopped-process checks
pass. The original 57 captures and their failure/evidence records are preserved.

New evidence is isolated under
`artifacts/ecosystem-next-20260910/overnight-20260921/performance-no-atlas/`:
`summary.json`, six run receipts, exact command/transaction logs, matching settings,
`restoration.json` and `handoff.json`. This documentation-only continuation returns
clean stopped ownership for parent review. The existing source-inclusive archive,
runtime, defaults and release capability scope are unchanged.

## Original measurement handoff

The current ordinary defaults are effectively tied with native loading and public
0.2.1 on the small English workload. French translation application, warm PNG
reuse and the complete type-search group show bounded benefits. Atlas reuse is
substantially slower even when every group hits its cache, and batching also adds
startup time. Enabling all currently retained optional features makes this English
workload much slower. Routing and the extended XML queries have no qualified
startup benefit from these measurements. An atlas is a larger texture sheet made
from many smaller images; reusing it is supposed to avoid rebuilding that sheet.
The type-search group reduces repeated searches for code types and their members
(reflection), plus repeated scans used by startup callers.

This is a stopped handoff for the steward's independent review, not acceptance or
a release-ready claim. No runtime code, product defaults or capability scope was
changed. Recommend native atlas loading and removal of the atlas speed claim;
keep routing/XML-extension speed claims unqualified. Batching's possible visible
responsiveness value needs separate evidence if it is retained as a user choice.
Do not describe its measured loading penalty as an optimization.

## Identity, authority and measurement conditions

Assignment: `wake-up-retained-performance-20260921-b4fc97ed`. The sole checkout
started clean at `b4fc97edd65f4e476973ba87c5ef0234248df535` on
`codex/ecosystem-next-plan-review`. The owner's 07:06 UTC overnight grant permitted
these serial isolated GOG measurements until 14:00 UTC, earlier on return/revocation.
All launches completed before 08:27 UTC. No Steam/Deck, normal-data, merge, push,
publication, security, excluded PNG pilot or new optimization work occurred.

| Identity | Exact value |
| --- | --- |
| Tested current core source | `908444675b1ddd33b818381fb67498eeb5faafc1` |
| Current core DLL SHA-256 | `3f96a341d7c2a777fb26b1c724e53d7e711ec46edf58f17a016bd6555de5d3e1` |
| Public 0.2.1 source | `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f` |
| Public DLL SHA-256 | `47164d9c94668d536a6d94922e047986710a6d5f62df9b639cee737e66de977d` |
| Constant observer source | `0412c65dd650332f60231dfbd46ec08781e22f58` |
| Observer DLL SHA-256 | `2dfee0d628cdcba506e03fbc164eecf31d7a014656bdfc79e5c83a4f76f56a27` |
| GOG game assembly SHA-256 | `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28` |
| Fixture generation | `gog-rev573-20260910-194017-cf1a1eb0` |

The GOG contract/generation retains its `rev573` name; the game log displays
`1.6.4871 rev574`. The actual assembly hash above is authoritative. The public
package was authenticated by `bind-comparison-package` and copied unchanged into
`artifacts/comparison-package/gog-rev573/b2d83c324a1fdf6095d927d6ed12c695f4a49d5f`.
Only private package metadata/runtime binding changed. It was not rebuilt and the
fixture did not switch platforms. Current `baseline` strategy mode is not public
0.2.1. No core, observer or helper was built; no helper was deployed or started.

The independent observer measured the first completed menu repaint through
`menuReadyObservation.elapsedSeconds`. Every launched run exited normally with
code 0, captured successfully and passed `automaticTestPassed`. There were **50
counted performance launches, six uncounted workload warmups, and one functional
diagnostic launch**. The diagnostic time is not performance evidence. No correctness
probe matrix was repeated; existing independent correctness evidence remains separate.

Each comparison held ordered content, observer, language, profile seed, resolution,
minimized/nonactivating launch style, audio muting and game background preference
constant. `runInBackground=true` is the common fixture preference. The optional
loading-only background feature was off in explicit settings. Fresh application
stores were prepared each run; warm arms restored only matching captured stores.
Windows filesystem caches were warmed, not flushed. These are not cold-disk results.

This task ran no competing build or source scan during launches. Before/after process
CPU snapshots showed roughly 1.06–1.85 aggregate external CPU cores across 32 logical
processors, dominated by a steady ChatGPT process near one core. No observed sample
was excluded for resource contention. This modest check cannot detect short-lived
processes or rule out disk/GPU contention; these are small local comparisons, not
controlled universal averages. Six warmups are excluded regardless of their time.

## Ordinary defaults and the combined English candidate

All rows use the ten-package `c08/selection.json` native-DDS workload. The initial
order was absent/public/current, then current/public/absent. Public and current
ordinary defaults used `candidate/user` with no settings file. Absent used the
supported `absent/fixture` mode: the proposed `absent/user` combination was refused
before a transaction or launch. Source inspection and captured commands confirm
that absent/fixture emits no Wake-Up selectors, like the ordinary user launches.

Numbers are whole-menu seconds. The two raw samples also give each row's range.
Percentages describe these sample means only; differences within spread are not wins.

| Condition / run label stem (`perf21-`, suffixes `a`, `b`) | Samples | Mean | Difference from native | Difference from public 0.2.1 |
| --- | --- | --- | --- | --- |
| `default-absent` | 11.691298, 11.772829 | 11.732064 | — | — |
| `default-public` | 11.603594, 11.695542 | 11.649568 | 0.70% shorter | — |
| `default-current` | 11.713801, 11.553038 | 11.633420 | 0.84% shorter | 0.14% shorter |
| `combined-fresh` | 22.875897, 22.916241 | 22.896069 | **95.16% longer** | **96.54% longer** |
| `combined-warm` | 19.523216, 19.567416 | 19.545316 | **66.60% longer** | **67.78% longer** |

The default differences change sign in paired comparisons and are within ordinary
spread. Final drift controls, `perf21-bracket-current` **11.967539** followed by
`perf21-bracket-absent` **11.904315**, likewise do not establish a default gain.
They are reported separately rather than silently pooled into the initial balanced
public/current/native comparison. Later baseline drift of a few tenths of a second
does not explain the multi-second combined regression.

Combined options used `combined.json`: processed XML, translation application, PNG
cache, prepared textures, PSD support, compressed storage, static atlases, atlas
batching, asset routing, XML-query extensions and loading display enabled. Detailed
timing/display diagnostics were off; all retired/deferred keys and background loading
were off. Shared cache budget was 1024 MiB, preset native, maintenance normal. Product
default definition/type searches remained on. The exact full settings and effective
arguments are in every run record and `summary.json`.

Fresh/warm order was fresh-a, warm-a, warm-b, fresh-b. Both warm runs restored the
same exact `perf21-combined-fresh-a` atlas and processed-XML stores. The prepared
texture store contained only ownership/usage markers, so no prepared-cache restore
was attempted. Receipts show eight real atlas hits, 73 skipped processed-XML calls
(139 admitted operations), and native fallback from the first unsupported custom
VTE operation. This is a valid processed prefix, not full patch skipping. Atlas
records were compressed to 21,280,996 bytes; processed XML stored 10,273,962 bytes.
There were 2,853 native DDS pass-throughs, zero PNG/JPEG decode calls, zero quality
overrides, and zero helper starts. Translation had no English lookup work; the
external XML-query supplier was absent on this workload.

Warm reuse saves **3.351 seconds against its own construction launch**, but still
loses badly against native/default loading. The individual feature gains must not
be added, and this ten-package result is not the complete 315-package profile.

## Individual decisions

Each optional-feature settings file begins with every supported Boolean override
off, 1024 MiB budget, native preset and normal maintenance, then enables only the
named option. Definition/type settings are not in that JSON allowlist and retain
product defaults except in the explicit type strategy comparison.

| Question / `perf21-` label stem | Off/native samples; mean | Enabled samples; mean | Decision |
| --- | --- | --- | --- |
| C07 `atlas-off` / `atlas-batch` | 9.132138, 9.200101; **9.166120** | 10.037639, 9.944414; **9.991027** | Batching costs 0.825 s / 9.00%. No responsiveness claim from timings. |
| C07 `atlas-fresh` | Same native controls | 15.984848, 15.694188; **15.839518** | Construction costs 6.673 s / 72.81%. |
| C07 `atlas-warm` | Same native controls | 15.726164, 15.749666; **15.737915** | Reuse costs 6.572 s / 71.70%. Stop this speed route; recommend native. |
| C09 `routes-off` / `routes-routes` | 13.582756, 13.982056; **13.782406** | 13.583415, 13.963562; **13.773489** | 0.009 s / 0.06% is effectively tied; no qualified benefit. |
| C12 `type-baseline` / `type-candidate` | 24.420911, 23.868609, 24.608734; **24.299418** | 23.856851, 23.335839, 23.348318; **23.513669** | Whole type group saves 0.786 s / 3.23%; all three paired differences positive. |
| C12 `query-off` / `query-queries` | 17.876056, 18.286824, 18.056035; **18.072972** | 17.907113, 17.633327, 18.061511; **17.867317** | Apparent mean 0.206 s / 1.14% is not repeatable; two pairs slightly lose, one wins. Inconclusive. |

For the three-sample rows, suffixes are `a`, `b`, `c`; otherwise `a`, `b`.

**Atlas:** official activation lane, no selection override. Order off/batching/
fresh/warm then warm/fresh/batching/off. Cache-only settings had `staticAtlases=true`
and `atlasBatching=false`; batching-only reversed those values. Both warm runs
restored fresh-a. Every cache run used eight natural groups. Fresh runs wrote all
eight; warm runs hit all eight without writing, covering 117,642,060 bytes.
Batching completed eight groups and 62 scheduler advances. This proves real work
and a repeatable whole-menu loss; no redesign or both-enabled atlas matrix followed.

**Routing:** 13-package `c09/selection.json`, off/on/on/off. The ordinary speed logs
prove ready activation but do not export route-hit counts. A separate natural
functional run, `perf21-route-observation`, enabled only existing timing-report
diagnostics in addition to routing. Its saved report says routing was active for
filesystem/Resources/bundles after actual startup lookups. No artificial lifecycle
queries were invoked. Its **31.641022-second diagnostic time is excluded**. This
does not supply repeat-hit counts for the four speed runs, prove each listed provider
family had traffic, or qualify arbitrary gameplay first-use benefit. Existing 140
functional checks remain separate. Keep the feature optional/default-off and its
speed claim unqualified; no further inconclusive timing pair is useful without
better natural repeated-route evidence.

**Type group:** 15-package `c12/selection-proton.json`; explicit fixture activation,
`--strategy type-lookup`, baseline/candidate/candidate/baseline, then one additional
baseline/candidate pair because the initial spread was comparable to the saving.
No user-settings file was supplied. Matching profiles kept Proton/Gagarin cache
state fresh: both controls and candidates report the cache absent/purged and newly
created. Candidate receipts show 10 indexed fallback calls, 44,097 reflection hits,
129 avoided full scans and no retained metadata at completion. Baseline used 10
original fallback calls. The paired savings are 0.564060, 0.532770 and 1.260416 s.
This compares the complete current group against native-timing behavior. It does
not isolate C12 additions from earlier type-search work or claim zero timing-hook
overhead. No new product setting was invented.

**XML-query extensions:** same Proton content, ordinary user activation, all
unrelated optional settings off, translation off. Off/on/on/off then one final
off/on pair. Real receipts include nine eager custom-index hits, one custom
single-index hit and 1,060 query-plan hits in on-a. Yet paired savings were
**-0.031057, +0.653497, -0.005476 s**. The bounded extra pair failed to resolve the
ambiguity. Stop the comparison and retain native/default-off as the recommendation,
without declaring this inconclusive route a proven general failure.

## Applicable French and PNG results

| `perf21-` label stem | Samples (seconds) | Mean | Meaning |
| --- | --- | --- | --- |
| `french-off` | 12.313301, 12.345169 | 12.329235 | Native French translation application |
| `french-translation` | 10.435623, 10.657937 | 10.546780 | **1.782 s / 14.46% saved** |
| `png-native` | 19.558860, 19.735400 | 19.647130 | Native loading of selected original PNG siblings |
| `png-fresh` | 31.703246, 32.071322 | 31.887284 | **12.240 s / 62.30% construction penalty** |
| `png-warm` | 16.235865, 16.694284 | 16.465075 | **3.182 s / 16.20% saved** |

French used the official activation lane, `French (Français)`, off/on/on/off and
only the `translationApplication` option changed. On-a recorded 26,572 lookups,
27,814 seed visits and zero package native fallbacks. Parsed-language persistence
stayed off. Both paired whole-menu savings were positive (1.878 and 1.687 s).

The 13-package PNG workload is `c05/qualification-20260913/selection.json`, explicit
fixture activation, `--png-source original`, `--png control` or `cache`. Order was
native/fresh/warm then warm/fresh/native. Both warm runs restored only the current
matching fresh-a prepared store. There was no first-build-images/raw-helper flag.
Fresh-a wrote 2,840 entries; warm-a read 2,840 hits with zero writes, cache errors,
misses or helper starts. Fresh receipt also records three JPEG calls, so this is a
PNG-selected workload, not an assertion of exclusively PNG input. Stored bytes were
**333,497,002 (333.5 MB / 318.0 MiB)**. Native reload work was about 4.0 seconds,
warm reload about 0.93 seconds; overlapping internal timings are not added to totals.

The mean construction penalty is recovered after approximately **four subsequent
warm launches**, assuming these same stable inputs/cache hits and similar savings
(five launches including construction). Each forward/reverse pair also gives four
subsequent launches when rounded up. This is a local break-even estimate, not a
guarantee for changing modlists or cache eviction. It must not be applied to the
ordinary DDS workload, which had no PNG cache traffic.

## Loading display and diagnostic cost

One bounded fresh-cache on/off/off/on comparison retained every other combined
option. Labels `perf21-display-combined-a/b` gave **21.968804 / 22.299903 s**
(mean **22.134354**). `perf21-display-combined-no-display-a/b` gave
**22.456183 / 22.790858 s** (mean **22.623521**). Both detailed timing and display
diagnostics stayed off. Turning off the display did not remove the combined loss;
these results do not support attributing the regression to the display or promising
that the display accelerates startup. Its value is user-visible progress information.
The separately observed heavy routing report demonstrates why diagnostic elapsed
times must stay outside these comparisons.

## Evidence, restoration and remaining gates

Private evidence directory:
`artifacts/ecosystem-next-20260910/overnight-20260921/performance/`.
`commands.jsonl` records exact current CLI invocations and exits; each command has
its own log. `transactions.jsonl` records all owned mutations. `summary.json`
contains all run identities/hashes, raw values, ranges/means, comparisons, effective
settings/commands, feature receipts and external CPU observations. Individual
`perf21-*-receipt.json` files preserve run hashes and process observations. Actual
run/profile/feature captures remain under `.rlo-test-instance/results/<label>`.

Warmup labels are `perf21-default-warmup2`, `perf21-atlas-warmup`,
`perf21-routes-warmup`, `perf21-type-warmup`, `perf21-french-warmup`, and
`perf21-png-warmup`. The refused `perf21-default-warmup` preparation log is preserved;
it launched nothing. No launched sample failed or was silently discarded.

All **61 owned transactions** were rolled back. Fresh `restoration.json` verifies
the actual seven baseline hashes, eleven absences, retained supplier repair and
successful metadata audit. No RimWorld process remains. No normal installation,
other project application or normal profile/save/cache was controlled or modified.
There are no runtime fixes requiring new builds or correctness tests in this handoff.

The steward must independently review the result and decide the atlas/scope claims.
The all-options configuration does not pass a beneficial-performance gate. Routing
and query-extension benefits remain unqualified; processed XML was exercised in
the combined run but not independently remeasured here. Previous bounded correctness
and quality/background UI observations retain their own identities and limits.
The tested core and existing private archive remain unchanged. Windows Steam,
then Deck/Linux, merge/push and publication retain their separate approval gates.
