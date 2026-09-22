# S03 — Faster translation application

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

S03 replaces repeated searches for already-applied translations with a temporary lookup. The game still resolves paths, assigns text, records replaced values and produces its ordinary warnings. Algorithm source `627e33239cbc1480ec1046b8bafed4c329d57fb2` plus user-control correction `0a26702db7d8aa733fc328e38aa0255354c33ae6` are **accepted by the steward for implementation and scoped offline checks**, with focused offline comparisons passing. Ordinary users can now select the persisted, default-off option. No game loading-time improvement or live compatibility is claimed.

## Base and ownership

Dispatch `wake-up-s03-20260909-7872e6f` assigned exact base `7872e6f80a629ab343e92b0c292a7e809aeac29f`. HEAD matched, the checkout was clean and no competing build was running before branch `codex/s03-translations` was created in the single existing checkout. This preserves public-input reconciliation, accepted S01 `691be0a`, corrected S02 `436c652` and S05 research `5565954`. Private/public ancestry remains separate; main was not replaced.

The normal Steam game was running. Only documented direct offline commands were used; the fixture guard was preserved. There was no deployment, game launch, Deck access, normal game-data change, UI automation, security-setting change, push or publication. The private `steward-state.json` remains for the steward. Checkout/build ownership is released with this committed handoff; this team has stopped and S04 is unstarted.

## Native behavior and implementation

An injection is one translation assignment to a definition field. [TranslationRuntime](../../../src/WakeUp/Translation/TranslationRuntime.cs) patches the native package application and its private setter. The setter's duplicate loop ordinarily examines all package records, looking for another successful record with the same normalized path. Normalization is the game's conversion of some alternative translation keys or named list handles to its target path.

The candidate lazily builds two bounded dictionaries on the first eligible duplicate check: successful normalized paths and the original dictionary key for each record. It then gives the **existing native loop** an empty dictionary or its matching successful entry. Consequently, the loop itself retains dictionary-key self-exclusion, original warning formatting, source-file attribution and assignment suppression. It does not use `record.path` for self-exclusion: those two strings can differ for programmatically constructed records. Ordinal string comparison follows native equality; field case and aliases are not newly canonicalized.

A second small substitution replaces the caller's `suggestedPath` field store with a helper that performs that same store, then records only successful injections. At that point the native caller has already written `injected` and `normalizedPath`. Failed assignments and placeholders are not winners. An earlier failure can therefore be followed by a successful record, and later passes can retry remaining failures. Preexisting successes seed a new scope in native dictionary order. Unusual multiple successful records for one normalized path, shared record objects, null records or packages above 65,536 records retain the full native scan.

Each `InjectIntoDefs` call owns fresh state. It is seeded from the **current native records**, including native stored paths from previous successful assignments. S03 never resolves an earlier successful record again or invents a new target after a list rearrangement: native duplicate checks use its stored path too. Current path traversal still sees changed definitions and lists. No definition, field, list index or setter access path is cached. The scope checks dictionary identity and its enumerator's modification check; changed dictionaries retain native enumeration. Nested application or setter entry invalidates the outer lookup. Cross-thread contention invalidates reuse through a generation counter and keeps the competing call native. Return and exception finalizers release scope and thread ownership. This is conservative fallback, not support for otherwise unsynchronized concurrent game-data mutation.

Native instructions retain case-insensitive ordinary field searches, `LoadAliasAttribute`, named list handles, translation restrictions, whole-list assignment, nested value-type writeback, replacement-value capture, syntax suggestions and final definition-cache clearing. Full-list versus element admission remains in the native parser; there is no new prefix-based duplicate rule. Native quirks also remain: intermediate and final list handles normalize at different points, and intermediate field traversal does not resolve every alias supported at the final field.

Three semantic method fingerprints constrain package application, the setter and final-field resolution. A semantic fingerprint describes resolved instructions, locals and exception regions rather than a whole game-file version. All-kind published Harmony guards reject interfering patches at installation and before reuse. Both substitutions match actual method/field operations and the named by-reference parameter, not assumed compiler local numbers. Changed instruction sequences are left intact. No global reflection patch or persistent translation cache was added.

## Reachability and activation

Read-only ILSpy inspection of the approved GOG reference establishes `PlayDataLoader.DoPlayLoad` → `LoadedLanguage.InjectIntoData_BeforeImpliedDefs` → `DefInjectionPackage.InjectIntoDefs(false)`. The later application is scheduled with `LongEventHandler.ExecuteWhenFinished`, then calls `InjectIntoData_AfterImpliedDefs` → `InjectIntoDefs(true)`. The package-scoped lifetime reaches both; it does not expire when `DoPlayLoad` returns. The native passes retain diagnostic clearing, missing-definition policy, success skipping and failed-record retries.

`WakeUpMod` has a separate failure-isolated translation initializer. Its internal selection requires `--wake-up-translations=on`, candidate mode and an absolute save-data path, plus the existing exact binary identity gate. Missing, repeated, off or unknown translation selectors install nothing; baseline/bypass stays native. Correction `0a26702` adds a persisted **Faster translation application** option (`translationApplication`), default off. On the next ordinary launch, `UserStartupSelection.Resolve` emits the existing translation selector when the option and master switch are enabled. Existing defaults and explicit development-control precedence remain unchanged. Older settings files leave it off. The original handoff exposed only the command-line path; the correction closes that ordinary-user gap. No fixture selector was added or required for this correction; controlled command-line qualification remains available. Default-on activation remains a separate decision.

When explicitly selected, receipts are written to `WakeUp/translations.jsonl`: installation/refusal and each completed package scope, including missing-definition policy, indexed lookup/seed counts, conservative fallback and zero retained entries. Those receipts were exercised only in the offline test process.

## Focused evidence

The [translation tests](../../../tests/WakeUp.Translation.Tests/TranslationRuntimeTests.cs) execute the original game methods against small in-memory definitions and restore their test state. **17 translation checks passed**. Native-versus-candidate snapshots compare final fields/lists, success flags, normalized/suggested paths, replaced strings/lists and exact errors/suggestions. Cases include duplicate TKeys, renamed-key warnings, differing dictionary/record keys, failed type assignment followed by success, placeholders, missing definitions, case/field aliases, restrictions, nested value fields, named/indexed lists, full-list replacement and preexisting duplicate successes. Additional checks cover changed list/record state between passes, exceptions, reentrancy, contention, foreign patches and changed instruction fallback.

The real native language methods run in one check: the early pass misses a definition, that definition is added, and the late pass successfully translates it. Both package scopes are counted and released. Only the language object's Unity icon constructor is bypassed; `dataIsLoaded` and in-memory records are prepared to avoid parsing external files. The methods under comparison are unmodified game DLL bytes. Empty English language records perform no lookup work; untranslated fields retain their English definition text. This is application/fallback evidence, not an end-to-end language-file or language-switch test (S06 owns persistence).

For 256 unique successful target fields, the verified native loop implies **65,536 record visits** (256 full scans of 256 entries). Observed candidate counters are **256 seed visits and 256 lookups**, with no matching-loop entries. This is a calculated native operation count and observed candidate count, not a stopwatch measurement, stage saving or loading benchmark.

The existing net472 suite also passed **159 checks with one existing optional Character Editor skip**. Release compilation using the frozen GOG and Prepatcher/Harmony references passed with zero warnings/errors. Existing Python evidence was trusted; Python/fixture tools were unchanged and no new Python run is claimed.

### Offline host limitation

RimWorld's setter calls `string.Contains(char)`, unavailable in the old .NET Framework test host. An initial native fingerprint attempt correctly failed there. A modern host can execute it, but the frozen **net472** Harmony build uses runtime APIs absent from modern .NET. The focused project therefore uses .NET 8 and the NuGet **net8 Harmony 2.4.2** variant, locked as a test-only dependency. This does not change product references, dependency metadata or bundled files. The existing main suite still uses frozen Harmony. Initial host failures and their logs are retained under `artifacts/s03`.

The test host resolves core-library type names differently. Tests first verify the reviewed fingerprint after mapping `System.Private.CoreLib` to `mscorlib` and `System.Linq` to `System.Core`, then substitute the expected host hashes **only in the test process**. The package fingerprint also matches the old-host computation directly. The game DLL and native translation bodies are not rewritten. Runtime binary admission is not claimed from this setup: the modern-host tests call the guarded installer after that test-only identity adjustment. Actual Mono execution, exact production-Harmony installation, future/platform admission and speed still require a separately authorized live check.

## Reproduction and identities

Use the [documented offline environment](../../source-build.md), with managed references at `.rlo-test-instance/game/RimWorldWin64_Data/Managed`, Prepatcher references at `.rlo-test-instance/game/Mods/2934420800/Assemblies` and `WAKE_UP_REFERENCE_TARGET=gog-rev573`. SDK: `10.0.401`. No fixture operation is necessary:

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test tests/WakeUp.Tests -c Release --no-build
dotnet restore tests/WakeUp.Translation.Tests --locked-mode
dotnet test tests/WakeUp.Translation.Tests -c Release --no-restore
```

The focused project deliberately references the already-built Release product DLL; build it first. It is separate from the existing solution test command because it needs a different host. Logs/results are ignored:

- [17 focused results](../../../artifacts/s03/test-results/translation-complete.trx) and [detailed log](../../../artifacts/s03/translation-complete.log).
- [Existing managed results](../../../artifacts/s03/test-results/existing.trx) and [log](../../../artifacts/s03/existing-tests.log).
- [Committed-source build](../../../artifacts/s03/build-source-627e332.log) and [identity receipt](../../../artifacts/s03/build-source-627e332.json).
- [Native IL/source extracts](../../../artifacts/s03-review/DefInjectionPackage.il), with language, loading and helper extracts beside it.

The tested runtime DLL was built from the final runtime source before commit, with base `7872e6f` in revision metadata: SHA-256 `ADA356C65E1E0ED7F3E0F9A992F73AB9D450C44C730E27DC33214AE5A0020BCE`. Source `627e332` commits that runtime and the final focused tests. Rebuilding committed source changes revision metadata: DLL SHA-256 `57AF3B387E431A9AB2530FAFAA0EF03B7DD60718F000EE22CBE76EAD9A80B8D5`. The modern-host Harmony DLL has SHA-256 `2B0496067BDA368FF35C383D80421401C57A3ACC091DCB3E5A8F15636104987F`. Neither runtime DLL is a fixture package, deployment or live capture.

## Conditional work and disposition

**Further package/definition/field/path lookup reuse: not justified by current evidence.** The duplicate map has a direct repeated-work mechanism; no eligible repetition/cost evidence justifies extra accessors or reflection caching. The archived rejection of broad reflective field stores was not reopened. S06 owns parsed-language persistence; this implementation adds no disk cache.

Pinned YaOpt `ab60621f071fbee3e4f1a92bb163c2760b8605bf` inspired successful-target lookup; its root license is MIT, copyright 2026 szszss. Pinned FastLoader `d95ae707661e46ff50a04b39d90173f6b90ed1e6` has no established license in the reviewed snapshot. Its fast applier has no caller there. Both were consulted from `artifacts/ecosystem-review-20260909/{comparison.md,sources/}`. This independently written implementation copies neither applier nor their source files; native diagnostic text remains in the game method. No competitor dependency is added.

Ready for steward source/offline review, with the host and opt-in limits above. Prepatcher remains required; on-demand remains optional/off; native-helper bundling remains unresolved. No S03 live, speed, gameplay, Linux, release or default-activation claim is made. Do not start S04 from this team.

## Parent steward review — 9 September 2026

**Correction requested: ordinary user activation.** Reviewed source `627e332`, handoff `86ecbe7`. The temporary successful-target lookup, retained native loop/setter, package lifetime and documented 17 modern-host plus 159 existing managed passes provide useful bounded offline evidence. The test-only host adaptation is explicitly unqualified for production Mono/Harmony; do not expand that workaround into another framework or claim a live result.

The remaining delivery gap is that ordinary users cannot select the feature: `UserStartupSelection.Resolve` never produces the translation selector, and the settings model/UI has no translation control. A hidden development argument alone does not finish this approved product slice. Add a normal, persisted **default-off** translation option using the existing settings and next-launch activation pattern. Preserve all existing defaults, master-disable behavior and explicit development-control precedence. This is routine integration of S03, not authorization to enable translations for existing users or change release defaults. A future default-on decision remains separate.

Use focused selection/persistence checks and accurate user guidance. Keep the existing command-line path for controlled qualification; no fixture expansion or live test is required for this correction. Preserve native translation behavior and the recorded host limitations, then return the same task's correction for re-review before S04 dispatch.

## Ordinary user-control correction — ready for re-review

Correction `wake-up-s03-correction-user-control-20260909-af176ac` began at the verified clean review HEAD `af176ace25cedd0a5110abcc56f9bdb47aee0943` on the existing `codex/s03-translations` branch, with no competing build. Source `0a26702db7d8aa733fc328e38aa0255354c33ae6` adds a boolean in `WakeUpSettings`, native `Scribe_Values` persistence under `translationApplication`, one checkbox in the existing settings view, and next-launch selector generation. It does not change the translation algorithm, controlled CLI selector, host workaround, fixture or any existing default.

**12 focused net472 checks passed**, with zero-warning/error Release compilation against the documented frozen GOG and Prepatcher/Harmony references. The native settings persistence cases save and reload controlled XML under ignored test artifacts: true round-trips; default false omits the new key and loads as false even into an instance initially set true, matching older settings files. Existing defaults are checked after loading. The other cases cover independent selection, master-disable, next-launch snapshots, turning the option back off, explicit development precedence, repeated selectors, off/invalid selectors and baseline/bypass refusal. Scribe state is restored after each persistence case. No normal user settings file or game process is touched.

Evidence: [focused results](../../../artifacts/s03-correction/test-results/settings-final.trx), [detailed test log](../../../artifacts/s03-correction/settings-tests-final.log) and [Release build](../../../artifacts/s03-correction/build-final.log). Commands used the documented environment, `dotnet build --configuration Release --no-restore` and `dotnet test tests/WakeUp.Tests -c Release --no-build --filter FullyQualifiedName~UserStartupSelectionTests`. The initial build exposed a missing test `System.Linq` import; it was fixed before the final build and all 12 checks. The older seven-case binary's initial test output is not correction evidence.

The tested DLL SHA-256 is `96B0A224449749C607409976D62BAE0C2CA777EDA3805057482F3087D4EB5C9A`, built from the corrected runtime tree with `af176ac` in revision metadata before source commit `0a26702`. It is an offline DLL, not a fixture package or deployment. Existing algorithm and full-suite evidence was trusted; the modern translation-host suite was not rerun or expanded. Exact production Mono/Harmony execution and live loading benefit remain unqualified. The user guide labels the new option unreleased and explains restart, persistence and default-off behavior.

Checkout/build ownership is released with the committed correction handoff. The steward record and unrelated work are preserved. No deployment, launch, Deck/normal-data changes, UI automation, security changes, push or publication occurred. This correction is stopped; S04 remains unstarted.

## Parent steward acceptance — 9 September 2026

**Accepted for implementation and scoped offline checks:** corrected source `0a26702db7d8aa733fc328e38aa0255354c33ae6`, handoff `cc92d48e675fb71ebd25d09f2cf1ad2a7605bab4`, preserving algorithm source `627e332`. The steward reviewed the setting, native persistence, next-launch selector and focused evidence. All 12 correction checks passed and the frozen-reference Release build was clean; the unchanged translation algorithm and documented earlier checks did not need another run.

Ordinary users can select the feature without a hidden launch argument. It remains default-off, with existing defaults and development-control precedence preserved. No further blocking correction was found. Additional lookup caches remain not justified. Exact production Mono/Harmony execution, full language loading/switching, platform compatibility and speed remain unqualified; the modern test-host adaptation does not establish those claims. This acceptance permits S04 to begin, not deployment, a live run or release. S03 ownership has returned to the steward.
