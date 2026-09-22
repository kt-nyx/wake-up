# S06 parsed language data: cost checkpoint

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

**Cost checkpoint accepted; capability deferred — 9 September 2026.** A grouped cache of parsed language records did not produce a repeatable useful advantage in the bounded offline comparison. The assigned cost stop was reached before runtime integration. **Persistent language reuse remains undelivered.** No production hook, setting or cache category was added, and existing behavior and defaults are unchanged.

The prototype preserved the checked parsed records and diagnostics, but complete warm loading had a median **23.24 ms**, versus **21.23 ms** for ordinary loading. It lost five of eight paired comparisons. This stops the tested representation/storage combination; it does not establish that all language caching is ineffective or approve removal of the capability from the combined release scope.

## Source and ownership

Dispatch `wake-up-s06-20260909-69b0463` assigned the existing checkout from **`69b0463206480ad465817c68dc8057c4f7fd6070`**. HEAD matched, tracked state was clean, and no competing build process was visible. The previous team had stopped. Branch `codex/s06-language-cache` was created directly from that accepted base, preserving S01 `691be0a`, S02 `436c652`, S03 `0a26702`, S04 report `cf2adc3` and their steward records. Private main and the separate public history were not changed.

Production source, tests and build inputs remain identical to the accepted base. This report's introducing commit is the handoff identity: `git log --diff-filter=A -1 --format=%H -- docs/ecosystem-replacement/s06-language-cache.md`. There is **no S06 product DLL, package, deployment or captured game run**. The independently written disposable [probe source](../../../artifacts/s06-language-cache/Program.cs), decompiled research and raw receipts remain ignored under `artifacts/s06-language-cache/`.

The [identity receipt](../../../artifacts/s06-language-cache/identity.json) records generation `20260904-182829-e058641c`, manifest SHA-256 `b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d` and the approved frozen GOG 1.6.4871 rev573 assembly SHA-256 `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`. Fresh read-only ILSpy inspection used the existing tool. The final probe source SHA-256 is `6fb374b1be8c47c767ad32630b2d51eac0a46b78c1736b452bd3c34861b91fd0`; its offline DLL SHA-256 is `15d85db8665b256d474d2be67a79c38db206c3bf177be0f0654c2a284c906624`. The final source adds a focused-check entry point after the cost measurement; the measured cost path is unchanged. This DLL is a console research program, not Wake-Up.

One bounded read-only sub-agent inspected native ownership, pinned references and the cost decision. It performed no build, test, native execution or tracked edit. Its [native review](../../../artifacts/s06-language-cache/review/native-review.md) and [cost review](../../../artifacts/s06-language-cache/review/cost-closeout-review.md) are supporting evidence, not additional qualification.

## What was tested

The cache stores instructions for constructing translation records, before those records are applied to game definitions. It retains file boundaries and ordered key/value/line records; scalar injection paths and values; full-list values and comment positions; legacy syntax flags and list-parse diagnostics; and parsed string-file lines. Reading the binary payload constructs fresh operations and lists. Native injection `TryAdd` methods still decide compatibility renames, duplicates and list conflicts against the current package. The probe's keyed replay implements the checked native merge rules; a production adapter would still need guarded integration around the native caller.

No translated definition objects, successful-application flags, normalized/suggested runtime paths or replaced values enter the payload. File names and paths are supplied again from the current input during reconstruction. This avoids capturing application-mutated records at menu completion. S03's native assignment algorithm and separate package scopes remain untouched.

Unlike S04's rejected per-file tree encoding, this probe publishes **one binary operation payload per sampled language directory**. It links unchanged S01 `OwnedCacheStore`, `CacheDigest` and `CacheLaunchPolicy`, with its own ignored `LanguageCache/v1` category. It uses ordinary SHA-256 content validation and the complete S01 envelope validation, durable publication, completion and disposal. Limits are 8 MiB per payload, 128 MiB per probe store, at most 512 selected files per sampled group, at most 4 MiB source per sample group, and 200,000 decoded operations. Capacity admission occurs before extra capture. These are research limits, not new product defaults.

The probe input key includes the exact captured bytes of every selected source, ordered source paths/kinds/type names, sampled package/language/folder, probe module identity, frozen game module identity, XML-library identity and parser convention. Decoding and hashing use the same captured bytes. It uses a constant English fallback descriptor and does **not** implement production effective-folder/mod discovery, current fallback selection, all relevant settings or effective-method/hook admission. Those omissions prevent runtime qualification independently of cost. Unknown game/mod definition types use `ThingDef` only for this isolated cost/record exercise; that substitution is not an allowed production fallback.

The [sample receipt](../../../artifacts/s06-language-cache/sample.json) contains **177 files / 773,730 bytes**: Core English (100 files), Rimatomics ChineseSimplified (51), Snap Out ChineseSimplified (5), and Rimefeller ChineseSimplified (21). Selection takes the first four complete language directories meeting the bounded size/file criteria, considering English then the first other language. It includes keyed XML, definition-injection XML and string files. This is a varied cost sample, not a mod allowlist or the actual selected-language loadout. Sorted sample discovery does not replace native encounter order; archive providers and effective version overrides are not exercised.

## Complete cost and stop decision

The corrected ordinary arm reads each source once through native `VirtualFile.ReadAllText`, then executes the actual native per-file keyed, injection and string methods. The candidate reads immutable source bytes, decodes them, hashes all content and input identity, opens owned storage, validates and decodes payloads, reconstructs fresh records, and completes/disposes the store. No warm-stage validation or completion work is excluded from the main comparison.

| Condition | Measured offline cost |
|---|---:|
| Ordinary loading, eight alternating-order trials | 18.93–37.35 ms; median 21.23 ms |
| Complete warm candidate, same trials | 21.51–27.42 ms; median 23.24 ms |
| Empty-store first construction, two observations | 86.13 ms and 50.70 ms |
| Record comparison | All four sampled groups matched |

The [corrected cost receipt](../../../artifacts/s06-language-cache/cost.json) alternates ordinary-then-cache and cache-then-ordinary four times. Five of eight candidate measurements are slower than their paired ordinary measurement; native outliers do not establish a repeatable gain. Its individual-group checks each include the full store lifecycle and are **not additive**. They do not prove that each group loses when sharing lifecycle cost with other groups.

The [initial receipt](../../../artifacts/s06-language-cache/cost-initial.json) is retained for transparency but is superseded: its ordinary string path read strings twice. The correction removes that bias from the ordinary timed arm. First construction still executes ordinary parsing and then separately extracts operations for publication; string files are read again by their native method. Its figures describe this actual conservative capture probe, not the cheapest conceivable cache build.

The probe runs on **.NET 8.0.31 / SDK 10.0.401**, with `DOTNET_TieredCompilation=0` and the approved frozen-reference verifier. Native per-file methods execute without Harmony interception. The documented [S03 host limitation](s03-translations.md#offline-host-limitation) was read first; no native translation setter or production Mono/Harmony qualification was attempted here.

Both arms are serial and exclude effective discovery; the real loader already reads XML in parallel. These measurements are not full `LoadData`, startup, application-pass or production-Mono timings. Inputs had already been read, Windows file-cache state was uncontrolled, and source-cold performance was not measured. Empty application storage is only a first-construction condition. No game launch, cache flushing or security change occurred.

**Decision:** stop this grouped operation representation before product integration. The complete-cost result supplies no useful repeatable advantage that justifies the additional hooks and lifecycle handling. Do not start a format search, new store framework or another slice from this task. Existing production regression evidence remains adequate because production source and tests did not change.

## Focused behavior evidence and limits

[Nineteen focused assertions passed](../../../artifacts/s06-language-cache/focused.json), including full native/restored snapshot equality on six synthetic files. They cover same-file/cross-file keyed duplicates, source paths and line numbers, TODO placeholders, escaped newlines, aliases, list values/comment positions, legacy `rep` and bracket paths, native ConceptDef backward compatibility, parse diagnostic ordering, whole-list/element conflicts, and string whitespace/comments. Reconstruction after mutation supplies fresh records with no applied state.

Content checks cover an equal-length edit with its timestamp restored, immutable captured input after an edit, selected-source order, distinct English/French identities, alternating independently owned payloads, a missing selected file, truncated payload refusal and S01 envelope corruption. Malformed XML cannot be encoded by the probe. That is not proof of runtime fallback: native keyed parsing discards that file's partial dictionary, while injection parsing can retain earlier records and diagnostic exception text. A production adapter must let those failures use native parsing rather than replay synthesized exception strings.

Full record equality on the four real groups and six synthetic files is **parsed-record evidence**. Neither native `LoadData`, `Translator` fallback, `SelectLanguage`, nor either actual definition-application pass ran in this probe. The language-identity checks are not a same-session game language-switch test. Existing S03 evidence for both application passes is retained without rerunning or changing its algorithm. No translated Def equivalence, live warning output, production patch admission, compatibility, loading gain or language-switch qualification is claimed.

## Native ownership findings for a future justified adapter

Keep the native loader in charge of selecting and attributing files. The fresh [LoadedLanguage extract](../../../artifacts/s06-language-cache/review/LoadedLanguage.cs) confirms these boundaries:

1. `AllDirectories` follows running mod order and each mod's descending effective folder order, using the selected language directory or its legacy folder name. `AbstractFilesystem` can prefer an adjacent archive; path resemblance does not prove a local-file provider. Unknown providers/hooks must load normally.
2. `TryRegisterFileIfNew` suppresses duplicate relative paths per actual mod object. Keep it, the native parallel XML reads and their indexed parser calls. `CodeLinked` and `DefLinked` take precedence when present and issue native rename warnings. Definition-type resolution, its plural-name retry, empty package creation and WordInfo loading remain native.
3. Strings are discovered under immediate subdirectories, then recursively. Native line parsing strips comments but can retain a space before an inline comment. Each directory also schedules a separate deferred icon callback; a cache hit must not skip it.
4. Native keyed parsing retains the first duplicate within a file and replaces earlier files' values. Injection parsing applies compatibility renames before duplicate checks; full-list duplicates can throw after logging, while scalar duplicates can replace. Native parse-time diagnostics and partially successful files require local fallback. `.slateRef` nested XML and list descendant comments are observable data.
5. English fallback can load lazily. `SelectLanguage` queues play-data clearing/reloading; new metadata replaces language objects and definition databases. A cache must release all previous language, package and definition references at its load/clear boundary and support reopening after menu completion. Persisted data must remain plain records, never old runtime objects.

No central clear/rebuild/bypass registration or completion consumer was added because the mechanism failed its cost checkpoint. A future retained adapter would still need its independent persisted default-off next-launch option, existing explicit-control precedence, S01 central maintenance participation, same-session ownership release and conservative effective-hook guards. The probe is not an implementation of those requirements.

## XML reassessment and handoff

Grouping durable entries addresses a different premise from S04's per-file approach: validation/publication can be shared across many parsed records without reconstructing general XML trees. Here that change still did not make the complete warm path usefully faster. The language and S04 samples differ, so their raw timings cannot quantify a grouping speedup or prove a viable grouped XML cache.

The useful transferable findings are to account for shared store lifecycle once per actual owner, retain immutable input identity, and separate cheap parsed operations from state-dependent merging/application. They do **not** establish S04 tree-restoration savings, S05 eligible replay work or effective-hook support. Both XML capabilities and S06 language reuse remain explicit gaps. The parent steward performs the pending XML reassessment; this task starts neither XML implementation nor S07. Any material release-scope change remains an owner decision.

Pinned FastLoader `d95ae707661e46ff50a04b39d90173f6b90ed1e6` language/injection formats were consulted from the local source snapshot and comparison. No license is established there, and no competitor code was copied. Its content-insensitive language identity and persisted runtime-derived path fields are unsuitable precedents. The inspected snapshot also sets its injection-cache enable flag false; source presence is not proof that feature is active.

Reproduce only the offline research with `python artifacts/s06-language-cache/sample.py`, then `pwsh -NoProfile -File artifacts/s06-language-cache/run.ps1` and the same script with `focused`. The script checks the frozen manifest and approved references. Build/check output and new uniquely named stores stay ignored. The final focused run exited zero. Fixture guards were preserved; no fixture operation was needed.

Prepatcher remains required, on-demand remains optional/off, and native-helper bundling remains unresolved. No deployment, game launch, Deck access, normal Steam/Workshop/profile/save/cache change, UI automation, security change, push or publication occurred. `steward-state.json` was untouched. **Checkout and build ownership return to parent steward `01a08711-7899-7ff3-a1b5-4a0b105ba661` with this committed handoff. The S06 task and its read-only reviewer have stopped.**

## Parent steward disposition and XML reassessment — 9 September 2026

**Cost/native-flow checkpoint accepted; this grouped representation is not retained. Persistent language reuse remains undelivered and deferred.** Reviewed report `5dadf75beeb6ec2a2e3e44c26227b27e09fb8a36`, based on `69b0463`, with unchanged production source. The steward checked the corrected ordinary read path, full warm timing boundaries, eight paired observations and focused receipt. These support the assigned stop, not a universal claim about caching. No further benchmark is needed to justify not integrating this prototype.

The scheduled XML reassessment is complete. Grouping was a materially different storage premise, but S06 demonstrates no useful complete-cost win on its own sample and measures neither S04 tree restoration nor eligible S05 patch work. It supplies no evidence-backed reason to reopen S04's rejected combination or bypass S05's eligibility/effective-hook prerequisites. Keep S04 independent parsed reuse, S05 processed replay/conditional inheritance reuse and S06 persistent language reuse explicitly deferred; their original goals remain in scope.

Proceed to independent S07. Reopening these routes requires a concrete changed premise addressing the recorded gap, not another automatic format search. Before deciding final replacement/release coverage, present the owner with any still-missing capabilities and obtain any material scope decision. No omission from release scope, new live authorization or default change is approved here.
