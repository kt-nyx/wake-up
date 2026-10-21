# Wake-Up cloud development

Use the private `wake-up-dev-1.7` environment and keep it **Only me**. Select it
explicitly for a new chat. The verified Settings > Codex Cloud > Actions > Edit
route opens an environment-edit draft; repeated Edit clicks may resume that draft.
Keep one source/environment editor and one owner for any shared build or fixture.
A source commit, saved configuration and published filesystem are separate actions.

## Current source and retained inputs

Read AGENTS.md, this document and [the shared collection workflow](../cloud/rimworld/README.md).
Inspect Git status before work and preserve dirty/untracked files. The personal
collection tooling lane is `codex/personal-snapshot-environment-20261021`, derived
from public `5a1073e6c1dc0b3afba8e98cc78311434fc03961`. The published tooling
checkpoint is `847535c39ea5d9609321c06d04806f27daa4afe3`, including streamed
source verification, administrative exclusions and retained-only capture checks.
Follow the exact checkpoint
recorded in the environment's startup instructions. Do not automatically switch
to an older `1.7` branch, reset the checkout or merge private ancestry.

The authoritative game identity is `/workspace/setup/current-game.json`, not an
old example or source SHA. The currently prepared Linux base is **1.7.4984 rev1242**,
unstable build **26087487**, depot **294103**, manifest **8009297283328867975**.
All 1,709 base files were verified. The canonical base contains no DLC. It is a
dated target, not a promise of future Steam currency.

```sh
source /workspace/setup/activate.sh
cd /workspace/wake-up
export WAKE_UP_17_GAME_DIR="$(python -c 'import json; print(json.load(open("/workspace/setup/current-game.json"))["gamePath"])')"
export WAKE_UP_17_HARMONY_DIR=/workspace/private-inputs/seed-20261019/harmony
```

Use SDK 10.0.401, PowerShell 7.5.4 and exact game/Harmony guards. Current-source
locked restore/build, 67 C# checks and four 1.7 packaging checks passed at
`4262f724`; private results are under `/workspace/setup/personal-baseline-check-20261021/`.
The built DLL identity is separate from the installed Wake-Up bytes in a captured
collection. Do not substitute one for the other without an explicit recorded override.
`/workspace/setup/refresh-environment.sh` verifies the retained current base,
default compressed store and complete official Data; it does not acquire updates,
build, stage or launch. For an assigned build:

```sh
dotnet restore src/WakeUp17/WakeUp17.csproj --locked-mode
dotnet build src/WakeUp17/WakeUp17.csproj -c Release --no-restore
dotnet restore tests/WakeUp17.Tests/WakeUp17.Tests.csproj --locked-mode
dotnet test tests/WakeUp17.Tests/WakeUp17.Tests.csproj -c Release --no-restore
python -m unittest discover -s tests -p test_package_17_candidate.py
```

## Frozen personal collection

The full installed collection was received, verified and published privately.
An explicitly selected fresh ordinary chat verified actual retained store/game/Data
bytes and staged the full collection with its captured project DLL unchanged.
These successful retention and staging checks do not establish runtime compatibility:
the full collection's startup attempt failed during play-data loading. It produced
no ready state, confirmed loaded order or shutdown acknowledgement. A zero child
exit status following owned-controller failure recovery is not a normal pass.
The verified fresh receipt also records successful closed-subset file verification
and controlled incremental receive, including its required parent and complete
resulting target. The subset was not staged or launched; the test update did not
replace the full default. No Linux-specific cause for the runtime failure is
established. Private receipt:
`/workspace/setup/fresh-personal-acceptance-20261021/receipts/fresh-acceptance.json`.
Further private pointers belong in `/workspace/setup/personal-collection-handoff.md`.
Do not publish collection membership, package IDs/order/settings, file hashes,
failure logs or downloadable private bundle references. Consult
`/workspace/setup/personal-collection-handoff.md` and
`/workspace/setup/default-personal-snapshot.json` for private status and the default
selector and receipt scope. The original full default and compressed store remain
preserved despite runtime failure. Set future defaults only after complete verified
receive; never infer restoration or compatibility from a snapshot ID.
No personal manifest contents or payload belong in Git.

The compressed store is `/workspace/personal-snapshots`. Full manifests identify
all selected files/settings and order; immutable gzip objects are shared outside
fixtures. Receive through supported private attachments, verifying the sender's
index ZIP hash first. Incremental receive requires the exact retained parent and
verifies the complete resulting snapshot. Keep all accepted objects/parents and
index/receipts. Remove only newly materialized transport copies after success.

```sh
python cloud/rimworld/snapshot_store.py receive --store /workspace/personal-snapshots \
  --index PRIVATE_INDEX --parts PRIVATE_PARTS
python cloud/rimworld/snapshot_store.py verify --store /workspace/personal-snapshots \
  --snapshot VERIFIED_FULL_ID
```

A closed subset includes required dependencies and active official packages in
original relative order. Its private request uses the standard test-request
schema, `selection.mode=subset`, explicit package IDs/reason and exact
`sourceSnapshotId`. Unresolved `investigate` is not executable.

```sh
python cloud/rimworld/snapshot_store.py subset --store /workspace/personal-snapshots \
  --snapshot VERIFIED_FULL_ID --request PRIVATE_SUBSET_REQUEST
python cloud/rimworld/snapshot_store.py verify --store /workspace/personal-snapshots \
  --snapshot RETURNED_CHILD_ID
```

## Fresh game, retained DLC and startup check

The private handoff gives the exact copy recipe: create a new fixture below
`/workspace/wake-up/.rlo-test-instance/1.7/`, independently copy the canonical
current base identified by current-game.json, then independently copy **only**
Royalty, Ideology, Biotech, Anomaly and Odyssey from the retained historical
`linux-unstable-1.7.4982-rev1280-build26031547/game/Data` tree. Do not copy its old
Core, executable or libraries. Use no hard links to writable fixture files;
copy-on-write cloning was unsupported in the measured fixture storage.

Before staging, verify complete official package file sets/sizes/hashes against
the private owner-capture evidence. All six packages (2,600 files) matched during
preparation. Retained DLC remains owner-installed data with historical provenance;
byte equality is not a refreshed official Linux DLC acquisition or full DLC
compatibility qualification. Standard snapshot official-package metadata alone
cannot prove those bytes. Preserve old games, DLC, fixtures and evidence.

After independently verifying the fresh base/Data, stage the selected full or
closed-subset snapshot into its fresh game without Mods and a nonexistent profile:

```sh
python cloud/rimworld/snapshot_store.py stage --store /workspace/personal-snapshots \
  --snapshot SELECTED_ID --game NEW_FIXTURE/game --profile NEW_FIXTURE/profile
python cloud/rimworld/snapshot_store.py verify-stage --store /workspace/personal-snapshots \
  --snapshot SELECTED_ID --game NEW_FIXTURE/game --profile NEW_FIXTURE/profile
```

Stage decompresses directly into independent writable files. Budget compressed
objects plus the expanded fixture, file overhead and working space; keep at least
4 GiB free. Read the private handoff before materializing parts in memory-backed
`/tmp`; memory and disk budgets are separate. Do not join a second full archive.

The prepared current-target startup controller is
`/workspace/private-controller/startup-26087487`; read its README. It verifies the
full collection plus an explicitly recorded test-only bridge addition,
actual loaded order, headless entry readiness, acknowledged shutdown and actual
exit zero as required acceptance conditions, under wall/CPU/RSS/disk limits.
The fresh full-collection attempt failed before those conditions were met; do not
repeat it automatically. Supplier-specific findings and installed-package behavior
remain private; this failure does not establish a Linux-specific cause or qualify
an active optimization. Preserve exact guards.
Preflight does not launch; a current explicit launch authorization and flag are
required. Closed subsets can be staged/verified, but this retained controller
requires the full collection. The verified subset evidence covers derivation and
files only; a subset check cannot qualify full-collection runtime behavior.

Never invoke `/workspace/private-controller/latest-26031547/stage_fixture.py` or
other historical minimal staging over a personal fixture. Preserve the installed
project package by default. Candidate replacement, bridge addition and effective
order must be explicit and recorded separately. No normal Steam/game process may
be stopped to obtain conditions. Startup/exit does not prove gameplay, saves,
graphics/audio, Windows compatibility or speed.

## Acceptance, publication and missing evidence

`/workspace/setup/acceptance-fresh-copy.json` remains historical, including
`personalSnapshotsAbsent=true`. It records source `9d346a0`, historical build
26031547 and its own DLL/fixture identities. Do not rewrite it to describe the
personal baseline. The new editor acceptance records verified receive/default and
official-data equality. The separate fresh receipt must record actual restored
configuration/version, retention/staging, failed full runtime and any completed
subset/incremental checks with their own scope. The fresh evidence bundle is
hash-verified and retained privately; full failed fixtures stay in their original
fresh chat, not in the baseline. Evidence-only subset/test manifests are not
retained snapshots in another chat unless installed through the supported protocol.
Preserve historical/editor receipts. Successful retention or
staging supersedes neither a runtime failure nor missing evidence.

The [later discovery campaign](discovery-1.7-20261021.md) was conducted in another
chat. Its private snapshots, controllers, profiling artifacts and live-run results
are absent from this restored environment. Fetching its source SHA does not
restore those artifacts or transfer its runtime qualification.

The first personal-store publication and fresh retention/staging succeeded;
full startup failed. Final documentation/handoff publication remains separate and
owner-controlled. Keep it Only me. Check final file and
configuration identities in a freshly selected chat; do not repeat full content
hashing absent a concrete change or restore uncertainty. Existing chats do
not update automatically; live processes and credentials are not restored by
assumption. Final Windows installation and binary/tag/GitHub/Workshop releases
remain separate owner-controlled work.
