# Current state — 22 September 2026

## Owner-authorized 0.3.0 release

On 22 September the owner authorized committing, merging and publishing the accepted
Windows candidate as **0.3.0**. Release preparation is in progress; a publication
receipt will record remote completion. The exact tested runtime remains unchanged.
Windows Steam and GOG are qualified within documented workloads; Linux/Deck remains
unqualified for this version. `releaseReady=true` is scoped to this explicitly
authorized Windows release, not a cross-platform claim. The older private-candidate
wording in the mod description is preserved at the owner's explicit request.
Release notes separately explain current scope and changes.

Parent acceptance of Windows handoff `c185db68` was recorded at `66b23eb2`, and final
package handoff `ffab8639` was accepted at `b9371c34`. No new live testing or runtime
change is needed to publish those binaries. The old steward timer remains paused.

The corrected core passed native XML/language output comparisons, texture checks,
ordinary copied-colony loading, the actual native → prepared → reset quality cycle,
and background loading with an unchanged colony tick during a 45-second unfocused
hold. The same binary passed a focused GOG regression. These are bounded automated
checks and agent UI observations, not universal gameplay compatibility or the
owner's preference for reduced texture quality. Known InputLegacyModule log messages
remain. No more Windows qualification runs or optimization experiments are assigned.

## What changes from the last release

The previous released version used for comparison is **v0.2.1**. Compared with it, this
candidate adds more ways to reuse loading work: processed game definitions and
translations, additional code-type/member searches, JPEG alongside PNG reuse, and
optional PSD image support. It also adds explicit texture preparation and quality
controls, a loading display/reports, and loading that can finish while the game is
in the background before returning to the player's normal background preference.
Original mod files stay intact.

Optional new controls default off. Additional code-search reuse follows the existing
default-on type-search setting. The shared cache budget defaults to **1 GiB** and is
adjustable; v0.2.1's PNG cache default was **512 MiB**. Prepatcher was already required,
supplier mods remain optional, and the Windows quality-conversion helper is optional.
Retired experimental paths were never public v0.2.1 features. The
[detailed release comparison](ecosystem-replacement/windows-steam-qualification-20260922.md#what-changes-from-the-last-actual-release)
explains the individual additions and limits.

This adds useful optional capabilities; the small English ordinary-default test
shows **no large general speedup** over native loading or v0.2.1.

## What the measurements support

Twenty counted Steam runs and four warmups finished at 07:43:55 UTC. Each compared
matched settings/content in forward and reverse order using the exact tested binary.

| Workload | Bounded whole-startup result |
| --- | --- |
| Small English ordinary defaults | Effectively tied: native 12.29 s, public v0.2.1 12.54 s, current 12.36 s |
| Complete type/member-search group | 0.654 s / 2.65% shorter |
| French translation application | 1.392 s / 10.89% shorter |
| PNG-selected warm reuse | 3.235 s / 15.78% shorter |
| First PNG cache construction | Adds 12.050 s and 333.5 MB; about four later matching warm launches repay it |

Only the ordinary-default comparison directly benchmarks v0.2.1. The other results
compare whole feature groups with disabled/native behavior; their percentages cannot
be added or treated as incremental release gains. Filesystem caches were warmed.
No new Steam speed claim is made for processed XML, routing, XML-query extensions,
display or all-options loading. Earlier [GOG measurements](ecosystem-replacement/retained-performance-20260921.md)
found a display cost and slower all-options loading; enabling everything is not a
recommended performance preset.

## Exact candidate and restored fixture

- Sole checkout: `Z:\Development\Large Projects\RimWorld Loading Optimizer`, branch
  `codex/ecosystem-next-plan-review`, being merged into private `main` for release. Public history stays separate.
- Tested core source/build: `67265e5696c80c9a461423722870a742054dd43b`, built with Steam
  references and tested unchanged on Steam and GOG. DLL SHA-256:
  `a64f9a446aaf7743024e0246f5e795dee1986f9a5eb647bc12246955af2ddfda`.
- Qualified optional Windows helper SHA-256:
  `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`;
  matching source `83a2930f60967eba4fcfb8e6acef10f35a415c62`.
- Release version is `0.3.0`; the accepted private predecessor was `0.3.0-rc.2`. The [qualification report](ecosystem-replacement/windows-steam-qualification-20260922.md)
  records the final archive, archived-source identity and distinct core build identity.
  Only the core and optional quality helper are runtime binaries in the package.
- The single fixture is restored to GOG generation
  `gog-rev573-20260910-194017-cf1a1eb0`. All 50 qualification transactions were rolled
  back; seven baseline hashes, eleven absences, supplier repair and metadata audit
  passed. Final metadata packaging performs no fixture operation or new test.

## Next campaign and remaining platform boundary

Linux/Steam Deck remains unqualified; no Deck access or test is authorized by this
release action. The current release may be published within its disclosed Windows
scope. Preserve separate public/private source histories and the mod description.

The owner requested a new steward for early whole-stage XML reuse, direct image
decoding and overlapping independent loading work. **On-demand loading is excluded**,
not merely awaiting implementation. Progressive detail and the companion app remain
out of scope. See the [fresh steward prompt](ecosystem-replacement/aggressive-loading-steward-prompt.md).
No new implementation or performance run is started by this release preparation.
Old overnight measurement and desktop-control grants have ended.

Historical permissions and exact evidence remain in AGENTS.md, the execution ledger,
campaign plan and qualification reports. Later owner decisions supersede dated notices.
