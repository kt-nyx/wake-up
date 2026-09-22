# Release procedure

This is the reusable packaging and publication procedure. Current source/package status belongs in [current state](current-state.md); historical copy, preparation and validation records are in the [archive](archive/README.md). Finishing an implementation slice or producing a package does not authorize publication.

The owner authorized releasing the confirmed Windows-tested candidate on 22 September
2026 as **0.3.0**, without waiting for separate Linux/Deck qualification. Windows
Steam and GOG evidence is accepted within its recorded scope; Linux validation
remains false. Release readiness is scoped to the disclosed Windows candidate.
See [release notes](release-notes-0.3.0.md) and [current state](current-state.md).
The tested runtime is reused unchanged. Preserve the mod description as explicitly
requested; version notes and qualification limits belong in separate release notes.
The owner authorization covers this release's commit, merge, push, tag and publication.
Later releases and platform tests require their own applicable permission.

## Qualification and promotion

First establish every feature and the combined candidate on isolated GOG. Prepare its concrete source-inclusive candidate and evidence, then obtain the owner's explicit approval before isolated Windows Steam testing. Linux/Steam Deck is a later, separately authorized promotion only after Windows Steam behavior is proven; it remains outside this Windows campaign. Follow [the promotion gates](ecosystem-replacement/next-campaign-proposal.md#platform-promotion-and-later-release-validation) and the unattended-only performance policy. Neither a GOG pass nor promotion approval is a release/publication grant.

## Product identity

`release/product.json` owns the release version, display name, folder and source
URL. `release/About.xml` is the descriptive metadata template. The packager fills
the corresponding fields from the JSON so a later rename does not require
changing the tested runtime's namespaces, assembly name, settings type, cache
paths, diagnostic names or patch-owner identifiers.

The template leaves generated identity fields empty. Edit the JSON values, not
duplicate copies of them in XML. The short in-game display label is `Wake-Up`,
defined in `WakeUpMod`. The formal title is **Wake-Up: Loading Optimizations**.
The DLL's stable
assembly version and the distribution version are separate identifiers.

The public package ID is `kt.nyx.wakeup`. Keep this ID stable after publication.
The repository and distribution folder use `wake-up`; the assembly and C#
namespace use `WakeUp`. The private fixture keeps
its existing local validation ID and two-file contract. Release metadata flags
that old package and the pre-rename `kt.nyx.startupfixes` bundle as incompatible:
remove/disable either old copy before
installing the public package. This is a menu warning, not an automatic migration
or duplicate-assembly protection. Never install both copies together.

## Packaging

Commit the intended inputs, run the existing offline checks, and build once:

```powershell
python scripts/fixture.py test
python scripts/fixture.py build
python scripts/package_release.py --package artifacts/fixture-package/<revision>
```

The last command requires a clean tracked tree and the package for the current
revision. It verifies the build receipt and exact two-file input, copies the
runtime DLL unchanged, applies release metadata, includes licenses/notices and
the full current tracked source as `Source/source.zip`, and writes a ZIP plus
a public manifest of hashes under `artifacts/releases/<version>-<revision>/`.
It rejects symlinks, private fixture paths and unexpected input files. It never
ships the fixture's private build receipt, game references or observer DLL.
The corresponding source archive does include tracked observer/helper source,
fixture tooling, tests and historical reports. These are development materials;
they do not add installed runtime components. Private fixture data and raw local
evidence are excluded. Do not describe the source archive as tooling-free.
The packager has no upload step. Inspect the generated folder before distribution.
For an already tested ancestor build, `--reuse-build` avoids changing its DLL solely
for documentation closeout. This explicit option permits documentation, release
qualification metadata, description-only `release/About.xml` changes, packager
code/tests and private observer C# changes. The reused-build gate keeps every other
About dependency/compatibility field exact. Fixture
prepare/verify/helper-deployment/CLI, exact comparison-package admission and captured
inspection-save admission changes (and their offline fixture tests) are allowed only if the rest of its Python syntax tree,
including build logic and constants, is identical. Runtime, reference/build inputs
and observer project changes are refused. The manifest records
the exact `buildSourceRevision` separately from the current archived `sourceRevision`.
Source archiving disables automatic Windows line-ending conversion. Helper
metadata distinguishes original build-input hashes from included source hashes;
only verified CRLF/LF differences are permitted between them. Every other source
change is refused. This preserves the executable receipt and normalized Git
source without rebuilding an unchanged helper merely for line endings.
When the owner authorizes a future update, the existing Steam client API can update item
`3798073152` without launching RimWorld. Verify app ID and listing ownership,
upload only the inspected source-inclusive content, preserve unrelated listing
fields and capture Steam's completion result. Publish the matching source tag
and downloadable ZIP on GitHub's public history.

The optional Windows x64 helper supports explicit portable DDS exports in the
preparation window. Native preparation and core loading require no helper. To
include the verified helper package alongside the committed core:

```powershell
python scripts/package_release.py --package artifacts/fixture-package/<revision> --texture-helper artifacts/s10-helper/package/win-x64
```

Use `scripts/build_texture_helper.py` when a helper build is needed. An unchanged
helper may be reused only when its source, pinned inputs, executable and receipt
identities still pass the existing checks. The overlay includes the executable,
matching helper source, licenses/notices and metadata; it must not introduce an
observer, test DLL, game dependency or private fixture data. A core-only bundle
is also valid: exports refuse a missing helper while native preservation remains
available. The helper is an optional supported Windows workflow, not full converter
parity or automatic in-game quality substitution. Linux helper work is deferred.
Neither bundle is deployment, publication or live qualification.
If the current helper build directory contains a later experiment, `--texture-helper`
may instead name a preserved source-inclusive `artifacts/releases/.../wake-up` folder.
The packager verifies its executable/notices/source hashes and exact committed helper
source, copies only the helper overlay (never the older core), and includes its matching
source separately at `Source/TextureHelper/source.zip`. The manifest records that
helper's original source revision separately from the current core/source archive.
See [R2 implementation and protocol](archive/ecosystem-replacement-20260910/r2-texture-preparation.md).

The generated public manifest records source/package identities and the qualification fields selected in `release/product.json`. Set those fields from the exact candidate's evidence; the packager does not qualify a platform. The source archive contains only the current tree,
not Git history. This avoids dependence on an external source offer or a link
remaining available. Include the source archive and legal material in Workshop
uploads too; do not upload just the DLL.

Record the ZIP hash, source revision, actual runtime DLL hash and relationship
to the accepted core in the final candidate report. A documentation-only rebuild
can change the assembly's recorded source revision and hence its bytes; do not
describe it as the older DLL unless the hashes match. Distinguish verified
unchanged runtime inputs from any product change needing fresh live checks.
Existing balanced Steam comparisons remain evidence for their older core. The
combined candidate requires its own live qualification. Record build-source and
documentation handoff revisions separately; do not rebuild after documentation-only
closeout merely to make the package revision equal the final documentation HEAD.

## Source history and publication

Public `main` began as a parentless snapshot: a source commit deliberately
separated from the private investigation history. That private history remains
local. The local `codex/public-source` branch tracks public `main`; local `main`
retains the investigation lineage. Preserve this arrangement unless the owner
changes it. Public commits use the GitHub no-reply identity. Do not push private
`main`, private tags, `--all` or a mirror to the public remote.

For later synchronization, fetch public `main`, review any remote changes, and
reconcile the intended local source and documentation changes with that public
tree using the same checkout. Review the complete result so newer public work
is not overwritten by an older local tree. Commit with
`git -c core.hooksPath=.githooks commit`, then push only
`codex/public-source:main`. Restore local `main` afterward. Do not force-push over
external contributions. Published source tags belong to the public history.

S01 reconciled the previously documented source mismatch in `bed726d`, as described in [current state](current-state.md). This is a private-lineage import, not a merge of public history or a publication. Preserve both histories and the accepted runtime base before building. Changing the permanent branch policy is a separate owner decision; do not infer permission to replace private main or merge unrelated histories.

## Evidence and public claims

Qualify the exact release candidate using the authorized environment and workload. Distinguish implemented behavior, offline checks, live startup/output checks, gameplay compatibility and measured speed. Set release status and qualification fields from that evidence; a platform's earlier package does not automatically qualify a newer DLL. Keep existing adequate checks and focus the combined pass on changed behavior and shared resources.

Include matching source and required license/attribution material in every distributed package. Check the provenance and permissions of any newly copied code or bundled dependency before inclusion. Existing project licensing does not grant rights to third-party material. See [LICENSE.md](../LICENSE.md), [notices](../THIRD_PARTY_NOTICES.md) and [contribution rules](../CONTRIBUTING.md).

Do not add percentages from different features or repeat an old combined average as the result of a new runtime. State workload, cache conditions and the measured endpoint. The [historical benchmark results](archive/feature-benchmark-results.md) are evidence for their recorded builds, not a forecast for this roadmap.

Publication needs explicit owner authorization. Preserve the Workshop description, preview, item ID and unrelated listing fields unless the owner requests changes; do not call `SetItemDescription` during an ordinary content update. Release change notes are separate from the listing description. Publish only the reviewed source branch/tag and matching package, never private history or fixture data.
