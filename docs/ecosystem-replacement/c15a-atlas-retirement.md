# Atlas retirement — 21 September 2026

Static atlas caching and atlas batching are removed from release activation and
settings. An atlas is a larger texture assembled from smaller images; its native
baking remains in use. Old saved settings and legacy command-line flags cannot
reactivate the retired work. The loading display still uses shared scheduling to
yield between supported native loading units, so its behavior is not wholly native
scheduling and its previously measured cost has not been removed.

The owner approved this retirement after the [measured losses](retained-performance-20260921.md)
and directed a stop before Steam. Assignment
`wake-up-atlas-retirement-20260921-866670ea` starts from clean
`866670ea4bc802120f5253458e767b7041cd4a49`. Performance and desktop-control windows
are closed. No Steam admission change, promotion, Deck access, normal-data change,
new optimization, merge, push or publication is included.

## Source change and evidence boundaries

Both settings remain readable solely to migrate old profiles to false before/after
native settings serialization. They no longer appear as UI options or generate
activation arguments. `AtlasRuntime.Initialize` keeps owned-cache maintenance and
the log destination used by the display, but cannot install cache/batching hooks.
The reusable cache/baking research source and historical evidence remain preserved.

`LoadingDisplayRuntime`, `AtlasBatchScheduling`, `NativeLoadingUnits` and their
compatibility guards remain unchanged. The scheduler's historical Harmony owner
name `wakeup.atlas-batching` is still valid for the display. Retirement therefore
means no `wakeup.static-atlases` hooks and false `ReuseInstalled`/`CanBatch`, not
the disappearance of every diagnostic or owner containing the word atlas.

The six previously retired paths remain native; C10/C11/companion and the proposed
PNG pilot remain deferred/excluded. All unrelated defaults and retained features
are preserved. C07 atlas optimization acceptance is removed; no supplier-wide
replacement or new speed claim follows.

Prior UI/performance evidence belongs to core `90844467`; combined correctness and
earlier hold/portal evidence retain their recorded earlier identities. They remain
applicable evidence for unchanged paths, not exact new-binary tests. The new GOG
build, focused functional result and source-inclusive private package are recorded
at closeout below. Qualified helper `ca12d392` and matching source `83a2930f` are
reused without a helper rebuild or experiment.

## Proportional validation

Focused managed checks cover migration of stale persisted settings, legacy flags
installing no cache hooks, preserved display selection, drawing compatibility and
native callback sequencing. All **53 managed checks** and **83 fixture/package
Python checks** pass. The first test compile lacked a Harmony import; it was fixed,
and its failed log is retained separately from the passing run. These are offline
tests, including simulated launch/platform operations; they launched no real game.

Read-only activation review found no additional release route. One automatic GOG
functional startup checked the concrete integration risk: the retained
display scheduler still completes after atlas activation removal. Stale settings
and explicit flags are already covered offline; no historical atlas lifecycle matrix
or repeated UI inspection is required. Functional elapsed time is diagnostic only.

Raw evidence and exact receipts live under
`artifacts/ecosystem-next-20260910/overnight-20260921/atlas-retirement/`.
Parent acceptance remains separate; stop before Steam.

The new GOG build is `36d739b1497c576e74e337e224bb1712f2337661`, compiled with
zero warnings/errors. Core DLL SHA-256:
`6cdf5fe145760de3c7570a3c0ad5ef8f139d57d32e20a97cf6b4d05b26d74b07`.

## Actual functional capture and restoration

`atlas-retired-display-20260921-01` ran with only the optional loading display selected
on the ten-package C08 content, matching observer `0412c65d`, ordinary user activation
and `--purpose functional`. It reached the independent menu observation, exited
normally with code 0, captured and passed `automaticTestPassed`. Run SHA-256:
`f138b9fd02210fa65e2f67544fb7a0c4a4ce11398d0a850b23825261c513d341`.
Its elapsed time is diagnostic only; the owner was using the PC.

The display recorded `installed` then `ended` with three stage changes. The atlas
log contains only `batching-installed`, the expected shared display scheduling
receipt. There is no retired cache selection/completion; the atlas store contains
only its ownership marker and zero group records. No UI automation, visual inspection
or repeated atlas lifecycle matrix was performed. Offline tests prove stale setting
and legacy-flag suppression; this live run separately verifies display completion.

All three owned transactions were restored. Fresh `restoration.json` verifies seven
baseline hashes, eleven absences, the retained supplier repair and passing metadata
audit. No fixture process or helper remains. The normal Steam game was verified
outside the fixture and left untouched. An idle reusable MSBuild worker is not a
running build or a game test; no active build/test operation remains.

## Private package and stopped handoff

Private archive:
`artifacts/releases/0.3.0-rc.2-ed36d07cb9135f335c74ae454b1d85ea32a0087a-texture-helper-win-x64/wake-up-0.3.0-rc.2.zip`.
ZIP SHA-256: `1473a90520f7b06ad96744f1d80ad33186cbc122b72397a8ddc63d148266b277`.
Archived source is `ed36d07cb9135f335c74ae454b1d85ea32a0087a`; embedded build source
is `36d739b1497c576e74e337e224bb1712f2337661`. Documentation-only handoff changes
after archiving do not require a rebuild or another archive.

Qualified helper SHA-256:
`ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`, with matching
source `83a2930f60967eba4fcfb8e6acef10f35a415c62`. Actual ZIP/manifest payload hashes,
new core/source, qualification metadata and historical helper source hashes pass.
Only the core and optional helper are installed binaries; no observer/trial/bootstrap/
companion is installed. Licenses/notices and source remain included.

Independent read-only review found no code or display-retention blocker. A copied
historical process snapshot in the restoration draft was removed rather than
presented as current evidence; the correct base and three created/restored transactions
are recorded. `current-game-processes.json` separately identifies the normal Steam
game outside the fixture, untouched. No fixture/helper or active build/test operation
remains. Parent acceptance is still separate, with no self-acceptance or successor.

`releaseReady`, Steam and Linux flags remain false. Atlas retirement is implemented,
not a pending owner choice. Real remaining limits are no new-core performance or
repeated manual UI proof, known logging limitations and later platform qualification.
The source difference and focused evidence above are sufficient for this bounded
retirement handoff; no further GOG experiment is assigned. Stop before Steam.
