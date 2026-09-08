# Historical evidence

This archive keeps decisions and measurements that still explain the product. It is not the active development procedure. Start with [current state](../current-state.md), [development](../development.md) and [results](../results.md).

The 2026-09-06 cleanup removed obsolete implementations and repetitive plans, task prompts, review dispositions, authority ledgers and work-item registries. Those documents described earlier development phases. Their exact contents remain recoverable through Git and the local bundle. The source cleanup checkpoint is `f886277ace5ed9b71fb044e69b57cb2cee5269f3`.

## Retained records

These are dated snapshots. Reports now have descriptive filenames; original contents were preserved except for an archive notice and local-link repairs. The table retains original paths for recovery. Commands and absolute paths inside the reports are historical. Five records retrieved from `54bee10c3e806bed346a31a1fb4ac470f68ef5e0` existed only on research branches before cleanup. The later template and shared-loading investigations are consolidated in [results](../results.md), with experimental commits retained in main's merged history.

| Record | Retrieved from | Original path |
|---|---|---|
| [Startup search qualification](startup-search-qualification.md) | `f886277ace5e` | `records/implementation/OP7_STARTUP_SEARCH_CLOSEOUT.md` |
| [Initial loading investigation](initial-loading-investigation.md) | `f886277ace5e` | `records/implementation/OP7_PERFORMANCE_INVESTIGATION.md` |
| [Gagarin cache loading](gagarin-cache-loading.md) | `f886277ace5e` | `records/implementation/OP7_GAGARIN_CACHE_LOADING.md` |
| [DDS read-ahead qualification](dds-read-ahead-qualification.md) | `f886277ace5e` | `records/implementation/OP7_OVERNIGHT_DDS.md` |
| [HugsLib enumeration investigation](hugslib-enumeration-investigation.md) | `f886277ace5e` | `records/implementation/OP7_OVERNIGHT_HUGSLIB.md` |
| [PNG read-ahead qualification](png-read-ahead-qualification.md) | `f886277ace5e` | `records/implementation/OP7_OVERNIGHT_PNG.md` |
| [XML index rebuild investigation](xml-index-rebuild-investigation.md) | `f886277ace5e` | `records/implementation/OP7_OVERNIGHT_SHARED_XML.md` |
| [Definition registration costs](definition-registration-costs.md) | `f886277ace5e` | `records/implementation/OP7_OVERNIGHT_REGISTRATION.md` |
| [Loading experiments closeout](loading-experiments-closeout.md) | `f886277ace5e` | `records/implementation/OP7_OVERNIGHT_STEWARD_CLOSEOUT.md` |
| [Startup concurrency experiments](startup-concurrency-experiments.md) | `54bee10c3e80` | `records/implementation/OP7_STARTUP_CONCURRENCY.md` |
| [Definition conversion preparation](definition-conversion-preparation.md) | `54bee10c3e80` | `records/implementation/OP7_DEF_CONVERSION_PREPARATION.md` |
| [Texture and XML investigation](texture-and-xml-investigation.md) | `54bee10c3e80` | `records/implementation/OP7_TEXTURE_XML_NEXT.md` |
| [PNG read-ahead prototype](png-read-ahead-prototype.md) | `54bee10c3e80` | `records/implementation/OP7_PNG_READ_AHEAD.md` |
| [Startup work reduction](startup-work-reduction.md) | `54bee10c3e80` | `records/implementation/OP7_STARTUP_WORK_REDUCTION.md` |
| [Licensing and contributions](licensing-and-contributions.md) | `f886277ace5e` | `LICENSING_AND_CONTRIBUTION_PLAN.md` |

## Recovering other history

Before branch and stash cleanup, the steward verified `artifacts/repository-reset-20260906/history-and-stashes.bundle`. It contains the former branches and all 12 stash histories. This private ignored bundle is local recovery evidence; it is not included in a source distribution. Branch names in old records are historical labels, not instructions to recreate or merge their runtimes. [Current state](../current-state.md) records completion of repository cleanup.

Read a historical tracked file without switching the checkout:

```powershell
git show <full-commit>:<original-path>
git bundle list-heads artifacts/repository-reset-20260906/history-and-stashes.bundle
```

The bundle keeps otherwise unreferenced research and stash commits recoverable after Git eventually prunes old objects. A commit hash written in a document alone does not do that. Do not restore old code into the shared checkout while another task owns it.

Useful omitted history:

- At `7068f187b5c5d94406efaf1c6a44ab3408ce06d2`, `records/implementation/OP7_WORKSHOP_LOCK_RETIREMENT.md` and `records/implementation/OP7_CONNECTION_AND_PERFORMANCE_DIAGNOSTICS.md` explain retirement of the old live Workshop contract and why metadata/content checking did not constitute parsed-XML reuse. Some catalog comparisons remained slower after reducing diagnostic overhead.
- At `54bee10c3e806bed346a31a1fb4ac470f68ef5e0`, `validation/MEDIUM_PROFILE.md` and `validation/XML_EXPANDED_PROFILE.md` document historical fixed selections. Only lanes supported by the current tool are current procedures.
- At `0fe0302d12121c04602030d5e9b414a7385ba698`, `records/implementation/OP7_WORKSPACE_CONSOLIDATION.md` explains retirement of old C: checkouts; `records/implementation/OP7_DRM_FREE_FIXTURE.md` explains the frozen private GOG fixture. Old phase current-state records, C/D/X/S designs, implementation/review records and root architecture/orchestration plans are available at that checkpoint. They do not impose the old phase gates on new tasks.
- The five tracked reports formerly under `runs/` at that checkpoint document August 24–26 profiles. They are early research context, not measurements of the cleaned runtime. Raw machine-local run evidence has not been deleted or moved.
- The 12 stashes were paused historical correction candidates. Their document paths already existed in main, but the bundle retains their exact edits; absence of unique paths does not establish identical contents.

## Observer history

The independent observer passed the small `menu-auto-03-absent` live test on 2026-09-05, with RLO physically absent: menu 9.262180 seconds, normal exit code zero at 10.983 seconds process lifetime, `automaticTestPassed=true`. The deployed observer source was `f26270bc6fe7c7ad29664ce9a84aff50527a9b16`.

Two prior failures were fixed: the launcher and Unity Mono originally used different clock origins, and an attempted gameplay-only unsaved-progress check dereferenced state absent at startup. Both processes now use the raw Windows counter; the shutdown guard checks startup state and absence of a loaded game. Those failures were not automatic successes. The complete original account is `validation/menu-observer/README.md` at `0fe0302d12121c04602030d5e9b414a7385ba698`. This validates the observation/exit workflow, not an optimizer speedup.

## Former branch heads

Snapshot taken before deletion. These are provenance references, not the current branch inventory.

```text
codex/gagarin-cache-loading 2eb11b587f73219fbeeff8a2e0fc8a00dedba169
codex/gagarin-cache-main 7d464eac207d28d586d5c0321cdeb617b3ef0ed8
codex/general-loading-cleanup f886277ace5ed9b71fb044e69b57cb2cee5269f3
codex/op7-drm-free-fixture 4edd38cfc3d2f3e5a056714022ad3226b94fa9e1
codex/op7-fixture-runtime-integration d4b024f2c1172056267b0db9acfb1d4b4b9005f6
codex/op7-retire-workshop-lock 7068f187b5c5d94406efaf1c6a44ab3408ce06d2
codex/op7-windows-mvp-integration 114c9d52cddcb7c4358a430c9edc47af14f6215a
codex/overnight-dds-qualification ae3dbfc497d7d9dfa3695874a51b08299febd51b
codex/overnight-definition-registration f88786399bb98d5f09cb2fd1db001593061c980e
codex/overnight-hugslib-integration 4e7f048f282c93ff23183b3c0e91e3fff5adc697
codex/overnight-png-qualification 455ccbe2712c1fff396aeb4f0da672f3b60d579d
codex/overnight-shared-xml-work 10b5907810cb96b3fbdd205b2ccbc16fa7d96161
codex/startup-work-reduction 54bee10c3e806bed346a31a1fb4ac470f68ef5e0
main 0fe0302d12121c04602030d5e9b414a7385ba698
```

## Licensing history

[The dated licensing assessment](licensing-and-contributions.md) preserves the approved direction and its conditions. It is a plan, not an enacted license grant or a refreshed assessment of legal terms. See the existing pre-publication work in [development](../development.md#publication).
