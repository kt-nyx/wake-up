# Wake-Up: Loading Optimizations

Wake-Up avoids selected repeated loading work in RimWorld. This checkout preserves the previous R1–R5/F1 implementation and its bounded Windows fixture evidence on an unmerged review branch. Public **0.2.1** is the last recorded released version; publication state has not been refreshed. The corrected private core differs from the original rc.2 ZIP. See [current state](docs/current-state.md) for exact identities and the [approved replacement plan](docs/ecosystem-replacement/next-campaign-proposal.md) for the approved replacement scope and workflow.

The XML search feature builds a temporary lookup table (an index) from the active mod XML when needed. It finds a definition's `defName` or a template's `Name` directly, then lets the normal XML engine evaluate the rest of the query. Relevant changes invalidate the affected index; patch completion releases it. No prebuilt modlist data or persistent XML cache is shipped.

The retained candidate keeps definition/template and code searches, supported
optional-mod integrations, processed XML, translation application, native PNG/JPEG
output reuse, optional PSD decoding, explicit texture quality/preparation, broad
asset routes, loading reports/display and loading-only background behavior.
New optional features remain off by default. Prepatcher is required; supplier mods
remain optional. Streaming XML remains a separate experimental option.

The 19 September amendment retires independent parsed XML, inheritance caching,
ordered read-ahead, parsed-language persistence, faithful authored-DDS recaching and
managed first-build image acceleration. On 21 September the owner also retired static
atlas caching and batching. These operations use native behavior; the display
still independently schedules supported native loading units.
On-demand textures/graphics/audio, progressive detail and the companion app remain
deferred. Old settings cannot reactivate retired options. Research source remains.

The optional Windows quality helper still supports explicit in-game conversion and
DDS exports with matching source/notices. Quality choices can change pixels;
original artwork stays intact. Ordinary authored DDS loads natively. Linux helper
work is deferred. No supplier-wide replacement or supplier-removal advice follows.

Bounded GOG correctness, agent-operated quality/reset/menu/focus UI and workload
measurements were accepted before atlas retirement. The new candidate removes atlas
activation and preserves the display; old UI/performance proof is not an exact
new-binary test. Defaults tied native/public0.2.1 on the small English workload;
translation, type searches and warm PNG reuse have conditional gains. All-options
was slower. See the [retirement report](docs/ecosystem-replacement/c15a-atlas-retirement.md).
The owner directed a stop before Steam. Performance and desktop-control windows
are closed; Linux/Deck and publication remain separate later gates.

Historical [Steam comparisons](docs/archive/ecosystem-replacement-20260910/steam-qualification-comparisons.md)
and [ordinary-entry checks](docs/archive/ecosystem-replacement-20260910/ordinary-entry-final-candidate.md)
apply to their older core, workload and cache conditions. Their measured numbers
are preserved in those reports, not predictions for this candidate. The shared
InputLegacy dependency error and earlier engine stall are not declared repaired.
Broader gameplay/visual coverage and platform/release qualification remain bounded or outstanding as detailed in current reports. Older F1 evidence retains its original scope.

- [Current state](docs/current-state.md): what is built, deployed, tested and enabled.
- [User guide and support policy](docs/user-guide.md): activation, settings, supported builds and limitations.
- [Architecture](docs/architecture.md): how retained features work and preserve existing behavior.
- [Development and fixture](docs/development.md): build, test, compare and prepare for publication.
- [Ecosystem-replacement roadmap](docs/ecosystem-replacement/next-campaign-proposal.md): approved scope, acceptance criteria and slice sequence.
- [Release procedure](docs/release.md): package identity, source history and publication boundaries.
- [Archived results and decisions](docs/archive/results.md): previous benefits, failed experiments and evidence limits.
- [Historical evidence](docs/archive/README.md): curated detailed records and recovery pointers.

The implementation is in [src/WakeUp](src/WakeUp); focused checks are in [tests](tests). The package includes matching committed source, licenses and a file-hash manifest. Its source archive includes development tooling and observer/helper source for transparency; the installable payload contains no observer/test DLL, game files or private fixture data. A Windows-helper bundle includes its separately verified optional executable. Read [AGENTS.md](AGENTS.md) before agent work. No tag, publication or release approval follows from this local candidate.

Licensing: [GPL-3.0-or-later with linking permission](LICENSE.md) for original code; CC BY-SA 4.0 for original documentation. See [notices](NOTICE), [contributing](CONTRIBUTING.md), and [source-build instructions](docs/source-build.md).

Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
