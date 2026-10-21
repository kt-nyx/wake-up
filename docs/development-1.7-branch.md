# Public 1.7 development source

The `1.7` branch is the shared source for ongoing local and cloud development.
It is separate from the published 1.6 release on `main`. Publishing this source
does not publish a Workshop package, GitHub release, release tag or installation.

The current product remains unpublished 0.5.0-preview.2 development source.
The [21 October fresh discovery task](discovery-1.7-20261021.md) verified and
received unstable build 26087487 / 1.7.4984 rev1242, refreshed exact contracts and
tested Core and defined public stress workloads. A safe translation predicate
prototype matched behavior but failed the repeated balanced performance screen
and was withdrawn. No new feature or performance improvement is qualified.
The [19 October investigation](qualification-1.7-cloud-20261019.md) found no
qualified loading-time improvement. `codex/1.7-loading-reduction` removes runtime
activation of all search features and their settings toggles. Old preview values
are retained without activating hooks. Native 1.7 loading is the recommendation.

On the fresh discovery branch exact checks now admit the reviewed Linux
1.7.4984 rev1242 game and Harmony rc.3 under .NET 10. Current Windows bytes and
execution remain unverified. The following historical identity belongs to the
19 October source: 1.7.4982 rev1280, Linux unstable build
26031547, depot 294103, manifest 4415223985052641865 has byte-identical
Assembly-CSharp, UnityStubs and Waylith references to the Windows review. Earlier
active-candidate checks remain tied to their original DLLs; see
[cloud development](cloud-development.md) and
[compatibility review](compatibility-1.7.4982.md). None establishes a current
loading benefit or Windows/graphics/audio-playback qualification.

Development follows the latest 1.7 build on Steam's `unstable` track while 1.7
remains there, then the main release track when it becomes the intended 1.7
stream. A new build requires refreshed static review and exact contracts; never
remove the admission checks to accept an unreviewed build.

The private environment is published; fresh-image retention, a new live functional
run and cloud-to-Git-to-local source synchronization are verified. The
workflow uses a project environment, the exact SDK in `global.json`, matching
private game references and chosen full or partial snapshots of the owner's
active mod collection. Game files, third-party mod files, saves, private paths,
raw diagnostics and private investigation history must not be added to Git.

Cloud agents may develop and test source on this branch. Final Windows candidate
verification and owner-controlled release publication occur locally. A cloud
test is not a Windows release qualification.

Public changes use branches derived from this public history. The initial source
snapshot was exported from the accepted private implementation without merging
or publishing its private commit ancestry. Preserve that history separation.
