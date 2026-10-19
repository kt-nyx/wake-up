# Wake-Up 1.7 implementation plan

Wake-Up 1.7 will avoid selected repeated searches without requiring Prepatcher.
The first candidate keeps the released 1.6 runtime separate and leaves Waylith's
asset preparation, texture storage and loading queues in charge. It is a private
candidate for later game testing, not a published or performance-qualified update.

## Authorization and baseline

The owner authorized remaining research, planning, implementation, offline checks
and preparation for in-game testing on 13 October 2026. Do not launch the game or
fixture, install/deploy, change profiles/settings/caches, publish, stop games or
interfere with Steam/ASTER. The permanent fixture and the pre-existing edit to
scripts/fixture.py remain untouched.

The implementation branch starts at private main and carries forward its accepted
0.4.2 descendant 5e2fb1e7. Installed 0.4.2 and packaged source 4ac8605f match the
existing runtime source. Its DLL SHA-256 is
53d93c72ead60e5acb07a6537719f71270082acb1b709f373a5c71cee12a64a3.
The initial 1.7 references are 1.7.4977 rev1248, Assembly-CSharp SHA-256
3d4f1ddfe71bd8760cb488255e3bdeac9366f537dc1ef813cc6f8d09824e2da9,
and Harmony 3.0.0-rc.2 SHA-256
94dd929a06f720869f6a718a64bc6713a37dbe153c2f4c16c031b2587d7ee694.
Recheck identities before deriving method contracts or building.

## Scope and decisions

An index remembers where an answer can be found, so subsequent searches avoid
walking the same collection again. Index construction and validation also cost
time. Keep indexes bounded and release them when their loading scope ends.

| Feature | First 1.7 candidate |
| --- | --- |
| Literal defName and template Name searches | Retain native XML workers and mutation-aware, bounded lookup |
| Harmony type-name fallback | Preserve fast native paths and full-name precedence; index only fallback |
| Leaf-class checks | Retain lazy native enumeration; bounded startup and alert-construction scope |
| Selected loading member lookup | Cache metadata only; preserve every actual getter, setter and callback |
| Translation member lookup | Separate opt-in, per-package metadata scope |
| Translation duplicate scan | Native; old dictionary enumerator mutation proof is invalid on .NET 10 |
| Generic attribute bridge | Omit preload bridge; keep native attribute entry points |
| Texture/cache/asset routes, XML persistence/streaming, supplier adapters | Omit from this candidate |
| Loading display, background permission, broad reports and exports | Omit; use concise feature status/counters |

Dictionary removal, clear and value replacement no longer reliably invalidate
enumerators. Do not reuse that proof for routes or translation records. Type-list
mutation validation is separate and must be checked on .NET 10. Runtime patches
must refuse changed instruction bodies and foreign interception at the affected
operation, including changes during Harmony's method rebuild before publication.
No global framework patches, new early loader or Prepatcher API are included.

## Implementation sequence

- [x] Reconcile stale 0.4.1 release descriptions and record the current assignment.
- [x] Add a separate .NET 10 project and explicit 1.7 reference checks.
- [x] Add static method contracts and a reviewed Harmony 3 publication guard.
- [x] Implement independent search features, startup cleanup and concise settings.
- [x] Run synthetic offline tests without loading game assemblies into the test host.
- [x] Review correctness, foreign-patch handling and plan deviations; fix findings.
- [x] Package a private candidate with explicit 1.6/1.7 folders and dependencies.
- [x] Verify assembly references, preserved 1.6 payload, source identity and package boundaries.
- [x] Record checks, candidate hashes and instructions for later authorized game tests.

## Completion criteria

The 1.7 assembly builds against the recorded references and has no Prepatcher,
Unity/Mono loader, texture pipeline or bundled native-helper dependency. Focused
offline tests cover actual synthetic patch execution as well as XML mutation,
type resolution order, cache lifetimes, foreign patches and exception cleanup.
The package selects only its intended version's runtime. 1.6 uses the unchanged
released 0.4.2 DLL and its original matching source/helper payload. Build/package
records distinguish source, binary and package identity. No installed files or
fixture/profile state change.

## Later validation and release

Game testing needs new authorization and an isolated 1.7 installation admitted by
the supported fixture tool. Replacing the permanent fixture's game installation
requires explicit approval naming it; do not weaken existing guards or alter the
owner's current fixture edit. Test minimal loading, active feature counters,
representative suppliers, colony creation, save/reload, errors and normal exit.
Test translations with the optional member cache both off and on.

Performance comparisons need separate permission and matched native controls.
Separate startup from colony entry, native cache construction from warm reuse,
and supplier cache hits from misses. Include setup/index costs; a successful build
or correct output does not establish speed. Native fallbacks with no consumed
optimization must not be presented as accelerated samples.

Prepare one Workshop item containing version-specific 1.6 and 1.7 payloads.
RimWorld supports versioned load folders and dependency metadata. Steam's separate
branch-download feature is currently disabled for RimWorld. Keep the official
listing unchanged during development and plan publication after 1.7 reaches the
main branch and the candidate passes final checks, with explicit owner approval.

## Implementation record

The Release product build passes with zero warnings/errors. All 59 C# offline
checks pass, including actual Harmony 3 execution on synthetic targets, mutation,
exceptions, late foreign rewrites and cleanup. Four new packaging checks plus ten
existing release checks pass. Static metadata review found ten default and two
optional translation member consumers with the expected ordinary method shape;
the extra already-resolved cross-reference overload is deliberately left alone.
The 212 method fingerprints reproduce byte-for-byte under repeated static
inspection. Evidence belongs under `artifacts/implementation-1.7/`.

Review corrections preserve mutable native subclass lists by publishing only
proven empty lists and still running the native predicate. Refused/failed target
setup rolls back only Wake-Up's attempted hooks. The game-version and Harmony
gates are intentionally exact for this first testing candidate. This narrows the
original forward-admission proposal until a later update is reviewed.

The additional .NET 10 review confirmed the translation duplicate cache cannot
retain its old dictionary enumerator proof. It is not ported; optional translation
member reuse is a separate safe mechanism. The preload attribute bridge and early
constructor reports are omitted. Existing 1.6 implementation/build inputs and the
owner's fixture edit remain unchanged. There is no live test, installation or publication.

The frozen candidate is `0.5.0-preview.1`, built from
`f09ca8487b0b6840e7f6abaa79108787b44107fd`. The DLL SHA-256 is
`27c100419056375f557bfcb787e4accda939083d76556e74515c530f9e08d160`;
the package ZIP SHA-256 is
`5d6c385da5b8b52792bbaab8d0448f405623d1cb38f2dab738410b35627a2ac8`.
The folder/ZIP and receipts are under
`artifacts/implementation-1.7/candidate-f09ca848/`. The audit verified all 33
manifest files, ZIP/folder equality, version selection, 1.6 payload preservation,
matching committed 1.7 source, no game/fixture binaries in the source archive and
absence of a published item ID. Static inspection confirms the 1.7 DLL has no
Prepatcher, Cecil, NAudio or old UnityEngine module reference. It is 114,176 bytes;
the preserved 1.6 runtime is 1,428,992 bytes. These are binary sizes, not speed results.

This documentary closeout follows the frozen build/source commit and does not
rebuild its DLL or replace its matching source archive. The only remaining work
is separately authorized in-game qualification, performance decisions and eventual
publication after the release conditions above are met.
