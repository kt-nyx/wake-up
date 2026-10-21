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
from public `5a1073e6c1dc0b3afba8e98cc78311434fc03961`. Reviewed tooling checkpoints
are `4262f7242b4bd0b00c3dc5a5753f090715ddaf73` and its Windows test portability
follow-up `b516e33d2768eaf1edb98babd47a81d24498b448`. Follow the exact checkpoint
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
`/workspace/setup/refresh-environment.sh` now verifies the retained current base
only; it does not acquire updates, build, stage or launch. For an assigned build:

```sh
dotnet restore src/WakeUp17/WakeUp17.csproj --locked-mode
dotnet build src/WakeUp17/WakeUp17.csproj -c Release --no-restore
dotnet restore tests/WakeUp17.Tests/WakeUp17.Tests.csproj --locked-mode
dotnet test tests/WakeUp17.Tests/WakeUp17.Tests.csproj -c Release --no-restore
python -m unittest discover -s tests -p test_package_17_candidate.py
```

## Frozen personal collection

The owner authorized retaining a full installed collection. Retention is pending
until actual private bytes are received and verified. Consult
`/workspace/setup/personal-collection-handoff.md` and
`/workspace/setup/default-personal-snapshot.json` for private status and the default
selector. An unset selector is a blocker, not an empty collection. Populate it
only after complete verified receive; never infer restoration from a snapshot ID.
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
full 454-entry collection plus an explicitly recorded test-only bridge addition,
actual loaded order, headless entry readiness, acknowledged shutdown and actual
exit zero, under wall/CPU/RSS/disk limits. Preflight does not launch; a current
explicit launch authorization and flag are required. It has not yet run this
collection. Closed subsets can be staged/verified, but this controller currently
requires the full collection. No further harness work is implied.

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
personal baseline. A new dated acceptance receipt must record verified receive,
default selection, official-data equality, tooling/game/controller identities and
actual checks, then publication and independently observed fresh-chat retention.
It supersedes the historical receipt only for its explicitly recorded scope.
Pending checks must remain pending.

The [later discovery campaign](discovery-1.7-20261021.md) was conducted in another
chat. Its private snapshots, controllers, profiling artifacts and live-run results
are absent from this restored environment. Fetching its source SHA does not
restore those artifacts or transfer its runtime qualification.

Publish only after verified bytes and coherent default/startup instructions are
ready, using the supported environment workflow. Keep it Only me. Verify retention
in a fresh chat explicitly selecting the published environment. Existing chats do
not update automatically; live processes and credentials are not restored by
assumption. Final Windows installation and binary/tag/GitHub/Workshop releases
remain separate owner-controlled work.
