# Reusable RimWorld cloud development

This folder supplies the common steps for a private RimWorld development machine
in the cloud. A project adds its own repository, build commands, candidate mod and
tests. Game files, third-party mods, settings and saves stay private; this folder
contains only our tools, instructions and examples.

A **baseline** is the prepared starting machine. A fresh cloud chat receives its
own copy of that baseline, with its own files and running programs. Later work in
one chat does not update another chat or the baseline. Agents delegated inside a
chat ordinarily share that chat's machine; give them separate fixtures and one
owner for any shared build or game process. A separate named environment is useful
for another project or a materially different test collection, not for every
ordinary chat in the same project.

Choose the prepared environment explicitly before starting ordinary cloud work;
a generic cloud-chat creation API call or an environment name in a prompt does
not select it. Verify the expected private paths and source before running tests.
Wake-Up's tested browser fallback is Settings > Codex Cloud > Actions > Edit.
That opens an environment-edit draft usable as a cloud workspace. Coordinate one
editor per draft; repeated Edit clicks can resume a draft rather than create
independent concurrent workers. Save/Publish updates the baseline and is separate
from ordinary Git commits. The normal desktop environment selector remains an
available UI route when shown, but was not tested by this implementation.

## Create a project environment

1. Read the project's current instructions and source history rules. Put its
   shareable source in a connected Git repository and choose the development
   branch. A cloud checkout builds there; do not point cloud builds at a live
   Windows folder, network share or a repository on a sleeping PC.
2. Through the supported cloud-environment UI, create a private environment for
   that project and select only its repository. Keep it **Only me** while it
   contains licensed game files. Reuse a compatible legal game/tool payload from
   the accepted Wake-Up environment, or acquire a current Linux game through the
   owner's licensed Steam account. Enter credentials only in an owner-controlled
   authentication flow. Never put credentials, Steam authentication caches or
   signed download links in the baseline or Git.
3. Record the actual Steam track, build, depot manifest and `Version.txt`. Target
   the latest 1.7 `unstable` build until the main track becomes the intended 1.7
   stream. Check current Steam metadata when refreshing. A recorded build is a
   dated fact, not a permanent promise of being latest. Never silently use an
   older game when an instruction requires the latest one.
4. Obtain the **Linux** game launcher and native libraries. Validate downloaded
   files against the official depot manifest. Local Windows DLC *data* can be
   reused when explicitly authorized and copied with byte verification; record
   that provenance separately from official Linux depot validation. Windows
   executables/native libraries are not a Linux installation. Do not package
   downloader caches, account configuration or unrelated local files.
5. Install the project's pinned SDK and required tools from official sources.
   Wake-Up currently requires SDK **10.0.401**, PowerShell **7.5.4**, Python 3.11+
   and exact Harmony rc.3. Set writable dotnet, NuGet and XDG config/cache/data
   directories under `/workspace/state`; XDG means the standard Linux locations
   applications use for their settings and caches. Retain activation instructions
   for every new shell. Check native dependencies before launch; Wake-Up's
   Waylith runtime additionally needed three LTTng packages from signed Debian
   metadata. Install only the prerequisites the actual game requires.
6. Keep an immutable private game source and the minimal project dependencies.
   Keep the mod collection out of an ordinary baseline unless the owner explicitly
   requests private retention. A personal baseline may retain the compressed
   store below, with its default snapshot identified in startup instructions. Build from the cloud
   checkout with exact reference checks and the project's relevant tests. An
   SDK build alone does not prove that a Windows-targeted mod runs on Linux.
   Review platform checks, patching support and exact game references before
   admitting Linux. Never remove a protective check to make setup look successful.
7. Use the prepared controller/bridge from the accepted private Wake-Up baseline
   as a starting point, adapting its project paths and explicit candidate package.
   The bridge is a small test-only mod that asks the real game to report state,
   save, reload and shut down. Build it against the actual game references.
   Keep test fixtures under the project's `.rlo-test-instance/` infrastructure;
   isolate profiles, saves, caches, results and process ownership. Use the native
   `RimWorld` apphost with its bundled runtime and `-Headless`, not a generic
   `dotnet RimWorld.dll` invocation. Do not copy Wake-Up-specific assertions into
   another project's test without adapting them.
8. Prove actual candidate activation and the behavior relevant to the project.
   Baseline acceptance includes Playing, at least 600 ticks, a small generated
   colony, save/reload with retained colonists, and acknowledged normal exit 0.
   Preserve the source/DLL/game/mod/settings identities and failed logs. Do not
   treat fallback-only loading, a forced shutdown or a running process as success.
9. Save a cohesive install script and startup instructions in the environment
   draft. Explain the retained paths, source branch, build/test/fixture commands,
   current game identity, mod snapshot protocol and known limits. Publish the
   prepared filesystem through the supported UI, then start a **fresh chat**
   using that exact environment. Confirm private files, tools, source, build and
   real functional workflow survive without local mounts or another login.
   Configuration text alone does not retain the binary game files.

Do not reset an environment or delete an existing fixture game as routine cleanup.
Keep game caches, mod collections and running tests isolated from the played setup.
Final Windows verification and release remain local, under the owner's control.
Headless Linux success does not establish graphics, Windows behavior or speed.

## Source updates and baseline updates

Keep source on Git and private inputs in the prepared machine or supported private
file storage. At the start of a **new** chat, inspect the checkout and fetch the
project's development branch. Start a work branch from its current remote commit
when clean; preserve any existing work and coordinate before switching branches.
Push only the public history authorized for that project. Do not use a filesystem
sync service as a substitute for commits or overwrite changes in another chat.

Updating source does not update the game, mod snapshots or baseline. A game/tool
refresh requires a new prepared version and focused verification. A mod refresh
is a new immutable snapshot described below. Keep the older general RimWorld
environment as a temporary recovery source until replacements are accepted; it
need not become a permanent shared server. These instructions plus the legal
private payload are the reusable foundation.

## Capture the current local collection

The cloud cannot see future Workshop or custom-mod updates on a disconnected PC.
Before a stress, compatibility or personal-collection campaign, export the current
installed files while the PC is available. The result is a **snapshot**: a dated,
identified copy that stays fixed during comparisons. This captures the versions
Steam has actually installed and the current custom-mod files, not a guarantee
that Steam has already downloaded every upstream update.

Use Python 3.11+. Copy `source.example.json` into a private directory and enter the
actual game, Workshop and played-profile directories. `packageOverrides` resolves
a duplicated package ID by naming its exact source directory; selection never
silently guesses between competing copies. Do not put the private file in Git.

First inspect availability without copying the collection:

```powershell
python mod_snapshot.py inventory --source private/local-source.json --out private/inventory.json
```

Copy `test-request.example.json` to a private request and choose `full` or `subset`.
`full` uses the active local order. `subset` names relevant package IDs; the tool
adds required dependencies and retains their original relative order, Core and
active DLCs. It handles version-specific required dependencies. Optional mods and
`loadAfter`/`loadBefore` preferences are not mandatory dependencies. A package that
exists outside the active list is not silently activated. If the source list is
broken, fix it or explicitly revise the test request; do not label an incomplete
collection as a full test.

```powershell
python mod_snapshot.py export --source private/local-source.json --request private/test-request.json --out snapshots/new-capture
python mod_snapshot.py verify snapshots/new-capture
python transfer.py pack snapshots/new-capture --out transfers/new-capture
```

If a capture fails its final source checks after copying files, preserve that
incomplete tree and retry into a fresh output with updated capture context:

```sh
python cloud/rimworld/mod_snapshot.py export --source PRIVATE_SOURCE --request PRIVATE_REQUEST \
  --out NEW_CAPTURE --reuse-files PREVIOUS_COPIED_TREE
```

Reuse/output must be separate from each other and the played game, Workshop and
profile roots. Each matching prior ordinary copy is fully hash-compared with the
current source before hardlink reuse; changed, already-shared or unavailable files
are copied from current source. Unsupported hardlinks fall back to copying. Played
files are never linked. The failed tree needs no manifest and is not modified by
export; keep both snapshot trees unchanged because reused files share storage.
Result/current-source hashes and all final capture consistency checks still apply.

The exporter copies whole selected mod packages, including their normal version
folders and `LoadFolders.xml`, so the game can choose the correct content. It
preserves settings named for the selected package folders/IDs, and the shared
HugsLib settings when selected. `extraSettings` permits explicitly named relative
XML files when a mod stores settings elsewhere. Settings with recognized populated
credential fields are rejected; supply a sanitized copy rather than exporting
credentials. Saves, logs, caches, `Prefs.xml`, Steam credentials and the game itself
are excluded. Official package identities are recorded; use the separately
verified Linux game/DLC baseline for their files. Any missing active package,
ambiguous selected package, copied-byte mismatch or source update during capture
stops the export. Failed output is incomplete; retry into a fresh directory.

`transfer.py` makes real ZIP attachments under 32 MiB each plus a small index ZIP.
Keep the printed snapshot ID, index and hashes with the parent's test request.
Attach the index and all required parts to the target cloud chat, or use supported
private storage tools when available. Never publish them to the public repository.
Materialize the actual attachments with the receiving chat's supported file tool;
Library IDs and Sediment attachment IDs are not interchangeable. Verify the index
ZIP hash supplied by the sender, extract its `transfer-index.json`, then:

```sh
python cloud/rimworld/transfer.py unpack --index /private/transfer-index.json \
  --parts /private/materialized-parts --out /private/snapshots/campaign
python cloud/rimworld/mod_snapshot.py verify /private/snapshots/campaign
```

If parts land in different attachment directories, copy those exact known files
into one private parts directory first. Unpack verifies ZIP parts, the reconstructed
archive, safe paths, every snapshot file and the snapshot identity. Retain the
source snapshot unchanged for the campaign. The tool does not authenticate, upload,
pair sync software, launch a game or update the played installation.

## Retain a personal collection and transfer updates efficiently

A retained collection must include the installed files. A snapshot name or a list
of Workshop IDs cannot restore those versions. When the owner authorizes personal
retention, publish the private collection with the prepared environment and test
it in a newly selected cloud chat. Keep the environment private. Existing chats
retain their own files; saving configuration does not publish a filesystem.

`snapshot_store.py` extends the immutable snapshot workflow. It compresses each
distinct file once and names it by its uncompressed SHA-256 hash (a content-addressed
store). Each snapshot keeps the complete standard manifest, including every file,
package, selected setting and mod order. Shared content remains outside disposable
fixtures. Whole mod packages and version folders are still selected together.

Capture with `mod_snapshot.py inventory` and `export` as above. Select the actual
current profile rather than reusing an old source configuration without checking.
Resolve duplicate package IDs with explicit overrides. Export checks the source
list, file membership, metadata and content hashes again at the end; an update
during capture requires a new output directory. Do not stop Steam or a normal game.
Record capture time and installed-only provenance; this does not acquire pending
upstream Workshop versions. Keep selected settings credential-free and exclude saves.

Create the complete first transfer:

```sh
python cloud/rimworld/snapshot_store.py pack PRIVATE_CAPTURE --out PRIVATE_TRANSFER
```

Transfer the printed index ZIP and all part ZIPs through supported private
attachments. Each part is below 32 MiB. Verify the sender's `indexZipSha256` from
`pack-receipt.json` before extracting `transfer-index.json`. The receiver streams
parts without joining a second complete archive:

```sh
python cloud/rimworld/snapshot_store.py receive --index PRIVATE_TRANSFER/transfer-index.json \
  --parts PRIVATE_TRANSFER --store /workspace/personal-snapshots
python cloud/rimworld/snapshot_store.py verify --store /workspace/personal-snapshots --snapshot SNAPSHOT_ID
```

The store contains `snapshots/ID/manifest.json` and `objects/XX/SHA256.gz`. Publish
both trees. Objects are verified by decompression and SHA-256; filenames, sizes and
timestamps alone are never sufficient. A snapshot manifest is retained only after
the complete resulting content has passed verification. Existing objects and
snapshots are preserved; interrupted transfers may leave reusable objects, but
cannot establish a successfully verified new snapshot.

For an update, export a new complete capture, then:

```sh
python cloud/rimworld/snapshot_store.py pack NEW_CAPTURE --base-manifest PREVIOUS_CAPTURE/manifest.json --out NEW_TRANSFER
```

This transfers only unique content absent from the named parent manifest. It
handles additions, removals, renames, settings and order changes through the complete
target manifest. The receiver requires and fully verifies that parent; missing
parents, parts or payloads fail clearly. Receive and verify as above. Never delete
the parent to make an incremental transfer fit. Keep the original default selected
until the new snapshot passes verification; label controlled test updates explicitly.
Republish the prepared filesystem to retain an accepted update for future chats.
The PC can then go offline; it is not needed to reconstruct or stage retained content.

To reuse content retained in any older complete snapshot, the destination exports
a verified store inventory before the sender packs an update:

```sh
python cloud/rimworld/snapshot_store.py inventory --store /workspace/personal-snapshots \
  --out PRIVATE_HAVE_INVENTORY.json
```

The command decompresses and SHA-256/size-checks every distinct object referenced
by retained complete manifests. It records only snapshot IDs and hash/size pairs;
it fails on missing/corrupt referenced content. Unreferenced objects and incomplete
snapshot directories are not advertised. Filenames/timestamps do not prove reuse.
Send the inventory and its printed SHA-256 to the capture owner through supported
private attachments, then verify the attachment hash before using it:

```sh
python cloud/rimworld/snapshot_store.py pack NEW_CAPTURE --base-manifest PREVIOUS_CAPTURE/manifest.json \
  --have-inventory PRIVATE_HAVE_INVENTORY.json --out NEW_TRANSFER
```

Packing reuses named-parent hashes plus matching inventory hashes/sizes, including
content removed from the parent and later re-added. The parent remains mandatory
when named. The optional index field `additionalReuseHashes` identifies target
hashes outside that parent; absence means an empty list, so existing initial and
parent-only transfers remain readable. The receiver validates the complete
target/parent/additional-reuse/payload partition and independently checks every
reused object against target hash/size. A stale inventory cannot make missing
objects acceptable: receive fails without retaining the target manifest. Return
the new index/parts and hashes through supported private attachments as usual.
Zero payload retransmission still has small index/archive framing overhead. Keep
inventory/index/receipts private; no credentials or synchronization service is used.

Derive a smaller collection without copying shared content. The request uses the
standard subset schema and names the exact parent `sourceSnapshotId`:

```sh
python cloud/rimworld/snapshot_store.py subset --store /workspace/personal-snapshots \
  --snapshot FULL_ID --request PRIVATE_SUBSET_REQUEST.json
```

Required dependencies are included, preserving original relative order and active
DLCs. Verify the returned child ID, then stage either full or child into a fresh
isolated game/profile:

```sh
python cloud/rimworld/snapshot_store.py stage --store /workspace/personal-snapshots \
  --snapshot SELECTED_ID --game /workspace/wake-up/.rlo-test-instance/CASE/game \
  --profile /workspace/wake-up/.rlo-test-instance/CASE/profile
python cloud/rimworld/snapshot_store.py verify-stage --store /workspace/personal-snapshots \
  --snapshot SELECTED_ID --game /workspace/wake-up/.rlo-test-instance/CASE/game \
  --profile /workspace/wake-up/.rlo-test-instance/CASE/profile
```

Use a separately verified matching game and official Data, without Mods; the profile
must not exist. Staging writes independent writable copies directly from compressed
objects, avoiding a second expanded snapshot. It checks the exact assembly,
official package metadata, full deployed mod file set, selected settings and order.
It retains the installed project package unchanged. Any later candidate replacement
or test bridge is an explicit recorded change with separate file identities.
Never run a minimal stager afterward. A successful file check does not establish
Linux compatibility: preserve startup failures and identify incompatible mods.

Budget retained compressed objects plus a full independent fixture, game/DLC and
working space before staging. The stager checks a conservative file allocation
estimate plus a 4 GiB free-space reserve before creating output. `pack-receipt.json` records bytes and elapsed packing;
receive prints retained object bytes and elapsed verification/reconstruction.
Record upload/materialization times separately. Retained historical DLC is not a
refreshed official DLC acquisition. Final Windows installation and binary/release
publication remain separate owner decisions.

## Parent and delegated-agent handoff

Send the child the same request schema, the exact `sourceSnapshotId`, a private
path or supported artifact/attachment references for the data, candidate source
revision, game identity, requested checks, limits and private result destination.
A filename or snapshot ID alone is not access to its bytes. An ordinary subagent
can use the parent's files; a separate cloud chat needs actual transferred files
or its own supported access to the snapshot. Give concurrent workers separate
fixtures and output directories. Only one worker may own a shared build or game.

For an investigative selection, the parent can initially specify:

```json
{
  "selection": {"mode": "investigate", "reason": "Find the smallest collection reproducing the reported interaction"}
}
```

The agent reads the available inventory/snapshot, determines the relevant package
IDs and records its reasons, then produces a resolved `subset` request. Execution
tools reject unresolved `investigate` requests. Do not omit mods silently or claim
a subset tests the full personal collection. When a full frozen snapshot is
already available, a child can create a smaller copy without contacting the PC:

```sh
python cloud/rimworld/mod_snapshot.py subset /private/snapshots/full \
  --request /private/resolved-child-request.json --out /private/snapshots/child
```

The child request must name the exact parent `sourceSnapshotId`. It receives the
same dependency checks, stable order and a new snapshot ID tied to its parent.

Stage into a fresh Linux game copy and fresh profile under `.rlo-test-instance/`:

```sh
python cloud/rimworld/mod_snapshot.py stage /private/snapshots/child \
  --game /workspace/project/.rlo-test-instance/case/game \
  --profile /workspace/project/.rlo-test-instance/case/profile \
  --override your.mod.packageid=/private/current-built-package
```

The game copy must already contain the matching game and official Data, with no
`Mods` directory; the profile must not exist. The helper preserves original mod
folder names so their saved settings still match. `--override` explicitly replaces
the installed project package with the current candidate of the same package ID;
it records deployed file hashes. `--override` replaces an existing selected ID;
it does not add a candidate absent from the selected active list. For a new
project whose candidate is absent from the personal active list, capture the
selected base first, then explicitly add the candidate, its dependencies and its
load position in that project's staging/request. Record those additions with the
effective order and candidate identity. Then add only the project's explicit test
bridge and requested test configuration, record that difference, and run the owned
controller against this staged game/profile. Do not run a minimal-baseline staging
command afterward that would replace the chosen collection.

The handoff result should name snapshot and parent IDs, effective mod order,
dependency additions/omissions, game/source/DLL identities, actual checks and exit
status, warning/fallback limits and private evidence paths. Report unavailable
inputs or Linux-incompatible mods explicitly. A Windows-only supplier cannot be
made a Linux compatibility test merely by copying its files.

## Prompt for another project

> Set up a private cloud dev and functional-test environment for this project using
> the reusable RimWorld cloud instructions. Inspect this project's instructions
> and build requirements first. Use only its repository and required dependencies,
> the latest intended 1.7 game track, and a minimal default mod collection. Reuse
> legally retained game inputs through supported private transfer; do not copy
> credentials or my saves. Implement the project's exact build/candidate/fixture
> steps, prove real activation plus save/reload and normal exit, publish the
> prepared environment, then verify it in a fresh chat. Keep larger collections
> in identified snapshots selected through the standard test-request protocol.
> Final Windows verification and release stay local. Work autonomously and report
> a concrete hard blocker or completion.
