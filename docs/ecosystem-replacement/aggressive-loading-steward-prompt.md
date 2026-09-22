# Prompt for the next steward

Act as Wake-Up's parent steward for a new performance campaign in the existing
checkout `Z:\Development\Large Projects\RimWorld Loading Optimizer`.

Start from the actual released 0.3.0 baseline and current private main. Read
AGENTS.md, docs/current-state.md, docs/development.md, docs/release.md and the latest
execution ledger. Verify branch, base commit, clean state and ownership before
assigning work. Preserve the separate private and public Git histories.

Implement and qualify these three areas:

1. Skip whole XML preparation stages: restore a ready-to-use combined result
   early enough to avoid substantial parsing/combination/patch work, while preserving
   mod ordering, changes, callbacks and object relationships that other mods observe.
2. Decode images directly once: use a fast native path that hands usable pixels to
   the game without duplicate decoding or unnecessary intermediate conversions.
   Preserve useful warm reuse and original image fidelity. Start with the bounded
   PNG experiment if inspection still supports it; JPEG needs its own evidence.
3. Overlap independent work: read and prepare upcoming content while earlier content
   is processed. Keep game callbacks and engine work on their required threads,
   preserve ordering, and bound memory and worker use.

**Do not implement on-demand loading.** Do not defer textures or audio until first
use, or hide work after the measured endpoint. Progressive detail and the companion
app remain outside scope. Do not silently revive retired implementations unchanged.

Consult docs/ecosystem-replacement/next-pilot-proposal.md,
next-campaign-evidence.md, next-campaign-matrix.md, retained-performance-20260921.md,
windows-steam-qualification-20260922.md, and the original research at
artifacts/ecosystem-review-20260909/report.md and comparison.md. Inspect the linked
supplier source and current code yourself. Treat old plans as research, not proof.
The next-pilot document's old scheduling/approval notices are historical; this new
assignment covers all three areas, developed in a sensible sequence.

Use a parent steward and bounded slice orchestrators/subagents. Only one team may
edit, build, deploy or operate the fixture at once, using this checkout directly;
read-only research can overlap. Do not create worktrees. Independently review actual
code and evidence, return corrections to the same owner, and maintain an execution
ledger with task, exact base, handoff, acceptance and next action. Do not duplicate
tasks. Begin with a concise status and proposed sequence, then proceed with routine
authorized implementation without repeatedly requesting approval.

Prove one mechanism at a time before building shared selection infrastructure.
Prefer a supported, beneficial aggressive path; otherwise use a proven beneficial
existing path; otherwise native. Fall back at the smallest safe unit. Count checks,
helper startup, transfer, restoration, waiting, upload, cleanup and memory costs.
Do not assume suppliers are less safe or compatibility checks caused our losses.
Explain the materially different premise before retrying a failed approach.
Stop low-yield loops; a slower implementation is not a useful conservative fallback.

Use focused correctness checks, then matched forward/reverse whole-loading
comparisons against both native behavior and released 0.3.0. Separate first-build
and warm results, measure individual effects before combined effects, and do not
add percentages from different workloads. Preserve content equality, scene entry,
first-use smoothness, settings and ordinary gameplay semantics. Broaden testing only
for concrete risks; do not turn the harness into the project.

Standing isolated GOG correctness permission continues. Performance testing needs
a fresh explicit unattended window; old overnight grants have expired. Platform
promotion, desktop interaction and normal-data boundaries remain as documented.
Do not touch normal game data or close games. No new merge, push, tag or publication
is authorized by this campaign prompt; the earlier release approval covered 0.3.0.
Bring the owner only material scope/dependency/default/experience decisions or
real authorization blockers, with a concrete recommendation. Record failures and
missing capabilities honestly. Success means demonstrated useful loading gains
with preserved behavior, not merely implemented hooks or passing startup.
