# Five loading opportunities: investigation and integration

The owner authorizes investigation, implementation, matched live testing and integration of useful improvements in five specific areas. Testing uses the existing isolated fixture while another game may run. Character Editor remains excluded; the smaller qualified selections and their established memory admission limits are retained. No larger test is admitted merely to rescue a weak result.

Work starts from local main `fa45d82` on `codex/five-loading-opportunities`. The preceding benchmark commits are fast-forward integrated into main; the retained runtime baseline remains `382996ca20863e2da88f7bb01758ecb9dacf3cbd`. One task owns all source, builds, deployment and sequential launches.

| Opportunity | Concrete question and changed premise | Decision |
|---|---|---|
| Later type-search cutoff | Does eligible shared search work remain between the existing loaded/empty-queue cutoff and the completed menu repaint, on the Character Editor-free selections? | No eligible calls in four qualified probes; no extension retained for these workloads |
| Broader XML searches | Can bounded name alternatives or indexed multi-result suffixes avoid shared search work while preserving real nodes and ordered mutation? | Neither OR alternatives nor the simpler eager-worker suffix extension produced a repeatable useful gain; both removed |
| Reuse type-index segments | How much rebuilding repeats unchanged static-assembly work? Preserve ordering, dynamic assembly reads, patch invalidation and memory bounds. | Not retained: no repeatable whole-menu gain in five pairs |
| Loading Progress compatibility/repaints | Can the previously demonstrated bridge/repaint mechanism produce useful, compatible gains on current qualified lists? The old large-list percentage is not a current forecast. | Repaint coalescing retained: final build saves 8.15% fresh and 8.24% cached on industrial + Loading Progress; XML bridge removed after inconsistent results |
| Faster DDS-cache validation | Native equivalent hashing addresses a measured cost of the rejected managed-hash pack; complete source and payload validation still count. | Not retained: faster hashing, but complete startup is slower in both orders |

Measure the smallest relevant cost first. A promising implementation must pass focused behavior checks and forward/reverse whole-menu comparisons against its original path, with matching content, settings, cache policy, observer and measurement overhead. Keep first qualifications separate, preserve failures and inconvenient successful timings, and distinguish fresh application caches from Windows file-cache state. Do not add overlapping stage savings or independent feature percentages.

Retain only repeatable useful changes. Remove unsuccessful prototypes and temporary probes while preserving source revisions and ignored raw evidence under `artifacts/five-opportunities/`. The decisions below distinguish retained features, negative results, inconclusive conditions and remaining resource-held work.

## Type-search eligibility probes

Diagnostic package `550cb519642d6f197adc7e04cc02e8124e45f120` preserves the original cutoff and original index rebuilding, and observes through the first completed menu repaint. It passed all 56 managed and 28 fixture checks, with zero build warnings/errors. Four normally completed qualification captures match the available original/processed XML, settings, order and scanned messages:

| Capture / selection | Type calls after original cutoff | Repeated static-index construction |
|---|---:|---:|
| `five-type-p01-quality` / UI | 0 | 771.786 ms |
| `five-type-p02-biocombat` / patch-heavy | 0 | 186.284 ms |
| `five-type-p03-industry-lp` / industrial + Loading Progress | 0 | 344.488 ms |
| `five-type-p04-visual` / visual | 0 | 146.141 ms |

Their first-launch whole-menu times varied widely and are qualification observations, not speed comparisons. There is no eligible late type-search work in these captures, even where loading itself is slow. The former large-fixture late interval is not transferable to these Character Editor-free lists. Larger mixed-list eligibility remains unmeasured under the current memory constraint.

Repeated static construction offers a bounded opportunity, strongest on the UI list. The candidate reuses only indexes from the immediately preceding compatible snapshot, observes current assembly order, rereads dynamic assemblies, discards all segments when the existing patch guard invalidates the snapshot, and counts every reused entry against the unchanged aggregate memory limits.

Candidate `221ab2fe57a73be9e90b6cde23cd238348861740` removed almost all repeated static construction (771.8 ms in the initial control probe versus 0.3 ms in candidate qualification). Five alternating-order pairs on the UI selection did not establish a useful whole-menu improvement:

| Pair | Control seconds | Reuse seconds |
|---|---:|---:|
| 1 | 25.539 | 26.285 |
| 2 | 25.966 | 26.454 |
| 3 | 26.104 | 25.958 |
| 4 | 28.662 | 30.166 |
| 5 | 27.463 | 24.993 |

Three candidates were slower and two faster; the candidate median was 26.285 seconds versus 26.104 for the control (0.7% slower). All captures completed normally and matched loading data. Another game remained running throughout. The internal saving is real, but the end-to-end gain is unqualified under these conditions; quiet/larger workloads remain unmeasured. The prototype and temporary type probes are removed from the working runtime, with their source revisions and evidence retained. No further repetitions are justified on this same workload.

The initial XML candidate added bounded alternatives such as `defName='A' or defName='B'` for Replace/Remove workers, which already materialize their results before mutation. It preserves document order and node identity, rereads current roots after edits, and falls back for unsupported expressions or node shapes. All 71 managed and 28 fixture checks passed before live qualification.

## XML name alternatives

Package `ee8651e9a50a3b9757682eebdb39dc18ff45708f` exercised 96 additional selections on the patch-heavy list. Qualification `five-xml-q02` completed normally and matched loading data; it took 172.988 seconds and is not a speed comparison. Two attempted launches were held before startup for insufficient available memory. STALKER 2 restarted during offline preparation; an attempted idle series was rejected before preparation, and no idle result was scored.

| Pair | Original seconds | OR planner seconds | Original patch ms | OR planner patch ms |
|---|---:|---:|---:|---:|
| 1, original first | 70.153 | 37.088 | 6326 | 5438 |
| 2, candidate first | 36.364 | 37.932 | 5805 | 5868 |
| 3, original first | 40.023 | 38.404 | 5910 | 5578 |
| 4, candidate first | 37.383 | 41.079 | 5560 | 5844 |
| 5, original first | 38.778 | 37.701 | 6427 | 5228 |

All ten captures passed and matched loading data. The candidate was faster in all three forward pairs and slower in both reverse pairs: the second launch won regardless of implementation. Its 2.2% lower five-run median is therefore not a reliable feature claim. The original first run also contains a large wall-time difference without a corresponding process-CPU saving. The bounded OR planner is removed rather than integrated on this evidence.

A separate smaller extension used the existing literal-name index for multi-result suffixes in Replace/Remove. These workers already call `ToArray()` before edits, whereas the other workers retain their live whole-document iterators. This changes the eligibility boundary of the existing index and does not repeat the OR planner mechanism. Focused node/order/mutation checks passed before its own live comparison.

## Loading Progress and DDS candidates

LP package `f35c4c3ae8809a9ac2e4e71f21236577a5ac5596` separates `bridge`, `coalesce`, and `both`. The XML bridge recognizes only the frozen Loading Progress assembly and inspected callbacks; it restores eligibility for the existing Gagarin optimization. Repaint coalescing preserves the iterator and native elapsed-time condition while suppressing the framework's extra stop-for-repaint result. Progress can update less frequently within that existing budget. Unknown versions or interfering patches retain ordinary loading. The candidate passed 72 managed and 28 fixture checks.

DDS package `25a7e6422a0e286a985dd326f331e176719ca59d` changes the rejected byte-pack experiment's hashing implementation, retaining full payload, index, and current-source validation. A 64 KiB streaming buffer avoids a whole-pack memory allocation. Native SHA-256 and managed SHA-256 give identical bytes; unavailable native APIs use the managed fallback. The native interface follows Microsoft's [BCryptCreateHash](https://learn.microsoft.com/en-us/windows/win32/api/bcrypt/nf-bcrypt-bcryptcreatehash), [BCryptHashData](https://learn.microsoft.com/en-us/windows/win32/api/bcrypt/nf-bcrypt-bcrypthashdata), and [BCryptFinishHash](https://learn.microsoft.com/en-us/windows/win32/api/bcrypt/nf-bcrypt-bcryptfinishhash) contracts. All 81 managed and 28 fixture checks passed, including source edits with unchanged metadata, corrupt payload/index fallback, and hashes across buffer boundaries. Live installation, pack creation, validation, and completion all count toward startup.

## DDS decision

The visual selection produced a 1,499,384,571-byte pack with 9,447 entries. Warm qualifications validated and hit all 9,447 entries; one unsupported DDS retained its original path. Native streaming hashing took 1.900 seconds versus 23.305 seconds for managed hashing, with identical validated content. This is an internal validation improvement, not the effect of enabling a DDS cache versus ordinary loading.

| Matched pair | Ordinary DDS seconds | Native-hash pack seconds |
|---|---:|---:|
| 1, ordinary first | 22.345 | 23.730 |
| 2, pack first | 21.570 | 24.256 |

The pack was slower in both orders: 23.993 versus 21.958 seconds by the two-run medians, or 9.3% slower. All four comparisons and four qualifications completed normally, matched loading data, and recorded Project Wingman before and after each launch with the expanded game inventory. The first pack-build qualification took 93.316 seconds; it is not a matched estimate of cache-construction overhead. Native hashing fixes the old hashing bottleneck, but rereading and validating the source and pack still costs more than this workload saves. The entire DDS prototype and its inactive selectors are removed; source `25a7e64` and raw results remain available. The retained processed-PNG cache is unchanged.

## Workload correction

The initial game check watched only STALKER 2. After that game exited, eight `five-quiet-lp-*` captures were incorrectly treated as an idle series. Project Wingman was discovered during its final control; the current process started at `2026-09-07T21:19:47.9673146Z`. Earlier absence of other games was not inventoried. The apparent quiet-series improvement was explicitly withdrawn as confirmation evidence, and all raw captures were preserved with `artifacts/five-opportunities/environment-review.json`. Subsequent runs inventory game processes across the known game directories and shipping executables, compare process identities before and after, and retain host CPU/memory observations. The new `*-pw` comparisons are separate; they do not turn the earlier mislabeled series into confirmed quiet results.

The lighter Project Wingman workload permitted two additional type-index pairs after the earlier inconclusive STALKER 2 comparisons. Controls took 23.939 / 20.416 seconds and reuse took 19.911 / 20.095 seconds. The reversed pair saved only 0.321 seconds (1.6%), despite about 0.640 seconds less repeated static-index work. The drifting first control does not establish a large gain. Together with the five earlier mixed pairs, these results do not justify retention.

The simpler XML suffix implementation in `fa40776918338b47bf2d7d7b793d1297f3fcc108` exercised 48 extra indexed selections and matched loading data. Its two pairs were 31.772 / 32.284 seconds (original / candidate) and 31.692 / 31.666 seconds (candidate ran first). No useful improvement was demonstrated; its implementation and focused prototype tests are removed.

## Loading Progress results and decision

The initial STALKER 2 fresh-cache coalescing pairs were 37.378 / 34.654, 34.545 / 34.638, and 35.220 / 33.489 seconds (off / coalesce). The two warm-cache bridge pairs were 36.128 / 32.498 and 27.132 / 30.619 seconds. Those noisy results alone were insufficient for integration.

With Project Wingman properly inventoried before and after each launch, fresh-cache repaint pairs took 34.881 / 27.453 and 28.288 / 26.233 seconds. Both orders favored coalescing, including a 2.054-second (7.3%) improvement in the reversed pair. The larger forward difference is not treated as a stable forecast. Industrial warm-cache bridge pairs took 32.244 / 27.569 and 28.297 / 28.111 seconds: only 0.186 seconds improvement in the reversed pair. A patch-heavy bridge pair took 29.166 / 29.664 seconds. The next candidate completed normally in 28.307 seconds, but Project Wingman exited during it; that capture is excluded from timing comparisons and its sequence stopped.

The owner then reported leaving the PC and provided a stable background. Two new patch-heavy warm-cache bridge pairs had empty game inventories before and after all four launches:

| Order | Bridge off seconds | Bridge on seconds |
|---|---:|---:|
| Off first | 28.496108 | 27.560189 |
| Bridge first | 26.813556 | 27.155651 |

All four passed and matched loading data; actual Gagarin reuse occurred with the bridge. Nevertheless, the reverse control was faster, so the bridge still did not establish a repeatable useful whole-startup improvement. Its code and selectors are removed. Earlier separate verification `five-lp-q02-verify` preserved 17,172 roots, 17,154 mappings, and reported zero bad mappings or ownership errors with identical parsed XML. That establishes exercised behavior, not a performance benefit.

The final package retains only repaint coalescing from these five opportunities. It leaves Loading Progress's iterator, loading order and original elapsed-time budget intact, but allows several immediate repaint requests to be handled within that existing budget instead of stopping after each request. The progress display may update less often within a loading stage. Coalescing ends at the loaded startup menu. Exact game, Harmony and Loading Progress identities and inspected method bodies are required; foreign changes to the relevant methods retain the original stop decision. No Loading Progress assembly is linked or bundled, and its absence is an immediate no-op.

The final source removes all temporary type probes, segment reuse, XML extensions, DDS pack code and XML-bridge admission. Existing definition/type searches, Gagarin behavior and processed-PNG cache remain unchanged. `--loading-progress coalesce` is the only new fixture mode; default `off` preserves explicit activation.

## Environment and evidence boundaries

The current smaller fixtures reuse the dependency-checked, sorted selections from the [benchmark campaign](feature-benchmark-campaign.md). The industrial selection contains 30 content packages, plus Loading Progress, RLO and the independent observer in its final comparison. Character Editor, EyePatch and the diagnostic development mod remain excluded. All game/profile/cache operations stay within `.rlo-test-instance`; runtime-written fixture files are restored from verified frozen bytes after capture. No normal Steam, Workshop, profile, save or cache state is changed.

The host is a Ryzen 9 7950X3D with 32 GiB RAM and RTX 4080 SUPER. Game content is the frozen 1.6.4871 rev574 development fixture under its reviewed `gog-rev573` binary contract. The manifest SHA-256 is `b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d`. The run records retain exact package, game, observer, order, settings, cache and resource identities. The fixture uses below-normal CPU priority in all arms. Fresh caches refer to application caches; operating-system caches are not flushed.

Raw runs, qualifiers, stopped attempts, comparison receipts and environment corrections are under ignored `artifacts/five-opportunities/` and `.rlo-test-instance/results/`. The large mixed selection was not used to rescue weak results. This study does not establish a universal average, latest-Steam compatibility, arbitrary gameplay compatibility or a new overall absent-versus-mod percentage.

## Final package qualification

Runtime/package `6b1eed1d29de156609bd5d76b73313d3254dcd1f` passes 57 managed tests and 28 Python fixture checks, with zero build warnings/errors. The final comparison uses this cleaned package against accepted runtime `382996ca20863e2da88f7bb01758ecb9dacf3cbd`, not an instrumented research control. Each package has its own normally completed qualification and its own matching restored Gagarin cache. Qualification times are excluded from scoring. All scored runs use the independent completed-menu observer, normal automatic exit, unchanged content/settings and the same diagnostic selectors. The stable-background results below use captures `five-final-fresh-{1,2}-{old,new}` and `five-final-warm-{1,2}-{old,new}`.

| Cache / order | Previous mod seconds | Final mod seconds | Reduction |
|---|---:|---:|---:|
| Fresh, old first | 23.496069 | 21.632567 | 7.93% |
| Fresh, new first | 23.414740 | 21.455955 | 8.37% |
| Warm, old first | 22.537593 | 20.372321 | 9.61% |
| Warm, new first | 21.775921 | 20.287971 | 6.83% |

The two-run medians improve from **23.4554045 to 21.544261 seconds (8.15%)** with fresh application caches and from **22.156757 to 20.330146 seconds (8.24%)** with restored Gagarin caches. Every paired order favors the final build. This is about 1.8–1.9 seconds beyond the previously accepted mod on this industrial + Loading Progress selection. It is not an absent-versus-mod comparison or a new average across modlists. The warm comparisons reuse Gagarin's normal cache in both arms; our rejected XML bridge is absent from the final package.

All eight scored runs and three final qualifications exited normally with `automaticTestPassed=true`, matching available XML, settings, content order and scanned messages. Empty game inventories before and after all eleven runs support the owner's stable-background report. The five final industrial candidate launches recorded actual repaint suppression and zero guard refusals. `five-final-q03-optional-absent` exercised the enabled selector without Loading Progress: it emitted no coalescer receipt, matched the UI fixture's loading data and exited normally in 17.652457 seconds. That timing is a qualification, not another performance comparison.

The final DLL SHA-256 is `a65a2efa915adcc6af17adcc5cc0132845dc417757ee6198e1ae5401a580a9b6`, built from `6b1eed1d29de156609bd5d76b73313d3254dcd1f`. Documentation-only closeout does not change the tested runtime. The package is deployed only to the isolated fixture. Local main receives the completed work at closeout; nothing is pushed or published. The batch is complete and the game is closed. First-run construction and arbitrarily cold disk behavior remain distinct, and full gameplay compatibility has not been tested.
