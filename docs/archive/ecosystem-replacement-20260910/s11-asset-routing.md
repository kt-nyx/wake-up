# S11 texture supplier routing checkpoint

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

**Accepted for implementation and focused offline checks**, 9 September 2026. This checkpoint
remembers the mod that supplied a texture so later requests can avoid repeating
the reverse search. It keeps ordinary eager loading: no texture is postponed,
scheduled, replaced with a placeholder or assigned a new image-quality role.

Assigned base: `a053fb17f7b03f381dc92a2ac84cdf00ebf68a4b`, branch
`codex/s11-asset-routing`, dispatch `wake-up-s11-routing-20260909-a053fb1`.
Accepted prerequisites carried by this base are S07 `4be5fb7`, S08 `07d024c`,
and corrected S10 `7196b5f` reviewed in `be04712`. S10's broader useful lossy
coverage remains unfinished; the owner deferred the Linux helper. Neither was
broadened in this checkpoint. Source/package identities are recorded below.

## Behavior and boundaries

**Remember texture suppliers** is saved in ordinary settings, defaults off and
selects `--wake-up-asset-routing=on` on the next candidate launch. The master
switch and explicit-control precedence remain. Existing defaults and fixture
presets are unchanged. Prepatcher stays required.

Native `ContentFinder<T>.Get` searches the current mod list last-to-first using
each holder's exact, case-sensitive path lookup. Texture Resources then precede
bundle fallback. Shader requests bypass holders; other types have distinct
native behavior. The new Prepatcher hook passes the actual content type to a
single runtime entry, which admits only textures on the native main thread.
It rewrites the generic definition before compilation, avoiding a detour of
shared generic machine code. The complete native body remains after the hook.

First admitted requests perform the native reverse holder search and remember
only a successful supplier index. Repeat hits validate mod-list identity/version
and holder/dictionary identity/version from the winner through all later mods,
then read the current object from that holder. Earlier mods cannot override the
winner. These inexpensive version checks remain linear in that relevant suffix;
this is not a constant-time lookup claim. Direct dictionary edits, same-length
replacement, reorder, holder replacement, clearing and reload invalidate reuse.
Unknown comparers, unavailable state, failures and bounds retain ordinary loading.
Limits are 16,384 routes and 4,096 providers, with cleanup on native static-resource
disposal. The route owns neither textures nor their destruction.

Missing results, Resources results and bundle results are never remembered.
An empty holder proves nothing about future content. A holder miss falls through
to the entire native lookup, so it currently repeats that unsuccessful holder
scan before native Resources/bundle resolution. This cost is explicit; broad
fallback replacement was not needed for the successful-route checkpoint.

Full native method fingerprints constrain preprocessing and runtime activation.
Published Harmony changes are watched for the relevant generic method families,
including another closed content type sharing code. Changed/unknown behavior
disables reuse locally. The existing Harmony binary gate remains.

## S10 and the next interface

The source supplier and the prepared replacement remain separate. S10's existing
`PngRuntime.Enumerate` calls `PreparedTextureRuntime.TryResolve` during eager
loading and publishes the returned object into that source provider's holder.
S11 reads that holder; it does not restore another texture or substitute the
global winner into another provider. Direct holder access returns the same
object as before. Raw file changes alone retain the already-loaded native object
until native reload; routing adds no per-request file reads.

The next checkpoint can use `AssetRouteCache<T>.TryGet` for already-published
objects. Deferred source selection must still use S10's provider, logical/physical
path, selection identity and fresh source validation before publication. A false
route result is **retry/fallback**, never proof of permanent absence. Readiness,
consumer coverage and scheduling remain separate, unimplemented interfaces.

## Focused offline evidence

Command, from this checkout:

```powershell
py -3 scripts/fixture.py test --test-filter 'FullyQualifiedName~AssetRoute|FullyQualifiedName~AssetRouting|FullyQualifiedName~TexturePreparationTests|FullyQualifiedName~UserStartupSelectionTests'
```

Final receipt: `artifacts/fixture-tests/9843810f5e0a43bc98787c5c652ae0f6/`.
**43 managed passes, one existing optional helper skip, 37 Python passes**, clean
Release build and `gameLaunched: false`. The unchanged helper's accepted S10
evidence was not rerun. Frozen GOG references remained selected while OS checks
proved the running normal Steam executable was outside the fixture.

Tests execute actual native `ContentFinder<Texture2D>.Get` and holder methods
with controlled in-memory mods and uninitialized Unity object handles. They
cover override order, identity, direct holders, missing/retry, dynamic changes,
stale routes, unknown comparers, exception fallback, thread/type refusal and
settings persistence. The actual injected generic texture entry executes from
an in-memory rewritten copy and returns the existing holder object. Complete
native fallback instructions are checked unchanged. S10 publication wiring and
prepared-store/fresh-mask regressions pass alongside routing.

The desktop CLR cannot detour this game generic. The generic-family guard test
therefore publishes a real Harmony patch record under a closed generic key and
checks refusal/recovery without detouring that native method. This is a guard
test, not Mono patch-chain execution. Test-output Harmony retains the exact
frozen SHA-256 `7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c`.

Four alternating pairs each used 12,000 repeated requests and 250 nonempty
providers, including runtime guard and version-proof costs:

| Winner location | Native total ms | Routed total ms | Meaning |
|---|---|---|---|
| First mod; native checks all 250 | 46.887 / 47.518 / 46.775 / 46.905 | 20.643 / 21.320 / 20.913 / 20.773 | Repeat hits reduce 250 holder key lookups to one |
| Last mod; native checks one | 0.271 / 0.277 / 0.272 / 0.268 | 0.772 / 0.764 / 0.794 / 0.760 | Added guard/cache overhead, about 0.04 microseconds per call on this host |

These are controlled desktop-CLR lookup costs, not a representative game benchmark
or universal average. Deep repeat hits improve in this sample; nearest hits cost
more. First requests, missing assets and actual workload distribution matter.

Actual Prepatcher/Mono startup integration, Unity Resources/bundle execution,
rendering, game reload, gameplay, complete startup and platform performance are
unperformed. Existing core Linux fallback is preserved without new qualification.
The bounded read-only reviewer found no remaining material issue after the
generic-family and unknown-comparer corrections. This is not steward acceptance.

## Build and ownership handoff

The build additionally pins the existing `0PrepatcherAPI.dll` 1.2.0.0, SHA-256
`39a3841d1c61c41d173e5cfb81262fde78db655a0d604a2dd91a5697e9d57300`, and locked
build-only Krafs.Publicizer 2.3.0. Only compiler copies of Harmony's embedded
Cecil/collection types are exposed; original references are verified and no
third-party DLL enters the fixture package. Notices accompany source.

Implementation source is **`950144b80c22338e38ce2519b7489b667c366c0a`**.
`py -3 scripts/fixture.py build` completed against the frozen GOG/Harmony/API
references with zero warnings/errors. The undeployed two-file core package is
`artifacts/fixture-package/950144b80c22338e38ce2519b7489b667c366c0a/`;
its adjacent `.json` receipt records that exact source revision and input hashes.

| Output | SHA-256 |
|---|---|
| Packaged `Assemblies/WakeUp.dll` (224,768 bytes) | `97d27abf42a22b4295ad4be8b330f865c1f1e8f6dd0dc193572ccf8ef4a6b463` |
| Packaged `About/About.xml` | `f6e07f9178757aab64b287a1940cf672db8c6416c804f56d4df541b5d8492cee` |
| Adjacent package receipt | `0f9b86d80bcad56eb28dd2900c610834bfe1ea7cca3ee3fcf07c5fa66093e819` |

This documentation closeout follows the built source commit; it does not change
the package or claim release qualification. Additional compact raw evidence is
`artifacts/s11-routing-20260909/evidence.json`.
No optional helper build/package was needed. No deployment, game launch, normal
Steam/Workshop/profile/save/cache change, Deck access, UI automation, security
change, push or publication occurred. Steward ignored state was not edited.

The S11 task and read-only reviewer stop all writes/builds and explicitly return
checkout/build ownership to steward task `01a08711-7899-7ff3-a1b5-4a0b105ba661`.
The ledger is awaiting review. Do not treat this handoff as acceptance or start
the optional on-demand checkpoint without its separate dispatch.

## Parent-steward routing acceptance — 9 September 2026

Accepted source `950144b80c22338e38ce2519b7489b667c366c0a` and handoff `08f8dd1e74313c69bf7163264ab7195c6a367543` after reviewing the actual cache, native lookup, prepatch insertion and body guards, generic-family patch guard, settings/build changes and focused evidence. The supplier index returns the current holder object, preserving S10 publication and direct consumers. List and relevant holder version checks cover reorder, replacement, direct writes and newly added overrides; missing results remain retryable. Complete native fallback instructions remain after the injected call. The build-only dependency and existing required Prepatcher API are pinned; no additional runtime DLL is packaged.

The recorded 43 managed passes, one optional helper skip and 37 Python passes adequately support this checkpoint. The steward checked the receipt/test evidence and matching packaged DLL hash without repeating adequate tests. Deep repeated lookup benefit and nearest-provider overhead are reported separately. Actual Prepatcher/Mono startup, Unity Resources/bundles, rendering, game reload, gameplay and overall speed remain unqualified. This is successful texture-holder routing only; Resources/bundle route memoization and on-demand loading are not delivered.

The stopped team returned the clean checkout. Assign the separate on-demand checkpoint to this same S11 task from the ensuing steward record commit. First identify one useful asset family whose consumers and ownership can be fully covered, then implement only that admitted family with default-off activation. If no bounded safe family is justified, return the concrete evidence and coverage gap rather than broadening into arbitrary hooks. Live launch/deployment remain unauthorized; Linux helper remains owner-deferred.
