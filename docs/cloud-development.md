# Wake-Up cloud development

Wake-Up now has a dedicated private environment, `wake-up-dev-1.7`, for building
and functional testing away from the owner's PC. The setup's real Linux test
passed; publication and a fresh-chat restoration check are the final acceptance
steps. This document will record that check when complete.

## Start work

Create a new cloud chat and select **wake-up-dev-1.7**. Give it the Wake-Up task
and ask it to read this document and AGENTS.md before working. The new chat should
use its own prepared machine; local Windows remains available for other work.
Keep a single owner for a shared build or fixture inside one chat.

In the cloud, the source checkout is `/workspace/wake-up`. Each shell uses:

```sh
source /workspace/setup/activate.sh
export WAKE_UP_17_GAME_DIR=/workspace/private-inputs/seed-20261019/windows-reference
export WAKE_UP_17_HARMONY_DIR=/workspace/private-inputs/seed-20261019/harmony
cd /workspace/wake-up
```

The public `1.7` branch is the source exchange between Windows and cloud. Inspect
the checkout and fetch `origin 1.7`; in a clean new chat, start the task's `codex/`
branch from the current public remote branch. Preserve dirty work in an ongoing
chat. Push only authorized public branches. The local private development history
must never be merged into or pushed over the public branch. Pull/review the public
task branch locally for final Windows verification. Source synchronization is
separate from publishing a binary or Workshop release.

Use SDK 10.0.401, PowerShell 7.5.4, locked restores and the 1.7 project:

```sh
dotnet restore src/WakeUp17/WakeUp17.csproj --locked-mode
dotnet build src/WakeUp17/WakeUp17.csproj -c Release --no-restore
dotnet restore tests/WakeUp17.Tests/WakeUp17.Tests.csproj --locked-mode
dotnet test tests/WakeUp17.Tests/WakeUp17.Tests.csproj -c Release --no-restore
```

`/workspace/setup/refresh-environment.sh` is the retained full prerequisite/build
recipe. It does not launch a game. Do not use the legacy 1.6 solution for this lane.
Current exact reference checks apply on either build host. Keep game files,
third-party mods, profiles, saves, caches and raw evidence outside Git.

## Minimal functional test

The private Linux game source is under
`/workspace/rimworld-runtime/games/linux-unstable-1.7.4982-rev1280-build26031547/game`.
The current controller is `/workspace/private-controller/latest-26031547`.
Read its README before operating it; the historical seed handoff targets an older
game and must not be replayed.

When the assigned task includes live functional testing:

```sh
python /workspace/private-controller/latest-26031547/stage_fixture.py --stage
python /workspace/private-controller/latest-26031547/qualify.py \
  --approve-live-game-launch --fixture <the-new-fixture-json-returned-above>
```

The fixture has exact Harmony, the current Wake-Up build, Core, all five DLCs and
a test-only bridge. It uses a separate profile/cache/save/results tree under
`.rlo-test-instance/1.7/` and launches the native Linux apphost. It proves actual
Wake-Up work, a small colony, 600+ ticks, save/reload and normal exit. The bridge
is test infrastructure and is never part of a release package. Preserve existing
fixtures and failed evidence; do not reuse old results or terminate another game.

## Full, partial and delegated collections

Follow the [shared baseline and mod snapshot workflow](../cloud/rimworld/README.md).
Its scripts are in `cloud/rimworld/`. On the owner's PC, a ready copy and private
source configuration live in the shared RimWorld `Cloud Development` directory.
The ordinary Wake-Up baseline includes no personal mod collection or owner saves.

For a larger campaign, have the local agent export the current active profile
with `mod_snapshot.py`. Use `full`, an explicit `subset`, or an agent investigation
that resolves to a documented subset. Required dependencies and active DLCs are
included in stable source order. Transfer the immutable snapshot through supported
private attachments/storage before the PC becomes unavailable. Future local
Workshop/custom-mod updates require a new export; they do not appear automatically
in a disconnected cloud chat.

Give a delegated agent the exact snapshot ID, accessible private bytes, candidate
source revision, checks, limits and result destination. When it receives a full
snapshot, it can derive a smaller child with the standard request schema without
contacting the PC. Separate cloud chats need their own actual access to the files;
ordinary subagents can use the parent's filesystem. Stage the selected snapshot
into a fresh isolated game/profile, explicitly replace `kt.nyx.wakeup` with the
built candidate, then attach the test bridge and run the appropriate controller.
Do not invoke minimal staging afterward and accidentally discard the collection.

## Accepted setup evidence and limits

On 19 October, source `1c1e48ed` built with zero warnings/errors and passed 67
synthetic C# tests. The real Linux test admitted Wake-Up, performed 29 indexed
definition queries and 28,207 reused member lookups, then saved at tick 637,
reloaded at tick 638 into a new game object retaining three colonists, and exited
normally with acknowledged status 0. Detailed private evidence is retained in
`/workspace/setup/acceptance-latest.json` and its referenced result directory.

The game is 1.7.4982 rev1280, unstable build 26031547, Linux depot 294103,
manifest 4415223985052641865. All 1,709 base files matched the official depot;
1,202 DLC data files were copied unchanged from the owner's installation. Exact
game, UnityStubs and Waylith references match the reviewed Windows bytes. Runtime
admission permits Windows/Linux x64 under .NET 10 while retaining the exact
game/Harmony/method checks and Prepatcher exclusion.

These are dated build identities. Follow the latest 1.7 unstable build until main
becomes the intended 1.7 stream. A changed game/Harmony needs review, refreshed
exact references/contracts and a new prepared environment version. If the new
source requires references the baseline lacks, stop for the actual update; do not
weaken checks or silently qualify an old build as current.

Type-name reuse correctly stayed native because the bundled runtime reported
metadata updates enabled. Headless texture/UI warnings, absent-Steam-client
warnings, DLC version labels on reload, ritual repairs and shutdown allocation
diagnostics remain in the logs. The test establishes bounded functional cloud
development, not graphics/audio, full DLC features, the full personal mod stack or
measured speed. Final Windows verification and release remain local and under the
owner's control; no binary, tag or Workshop publication accompanies setup.
