> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).
> Original: `LICENSING_AND_CONTRIBUTION_PLAN.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# RimWorld Loading Optimizer: Licensing and Contribution Assessment

Date: 2026-08-25  
Status: licensing direction approved; exact linking/Workshop terms require pre-publication review  
Scope: software, bundled dependencies, prior-art reuse, documentation, and community contributions

## Plain-language conclusion

Harmony and Prepatcher do not dictate this project's license. Both use the permissive MIT License, which allows a dependent project to use another license as long as required notices are preserved if their code or binaries are redistributed.

The project's license is mainly controlled by two choices:

1. the protection we want for our own source and downstream modifications;
2. whether we copy implementation code from an existing loader instead of independently implementing a researched design.

The approved model is:

- optimizer source code: **GNU GPL version 3 or later, with an explicit additional permission to link with RimWorld, Unity, Harmony, Prepatcher, and independently distributed RimWorld mods**;
- project documentation: **Creative Commons Attribution-ShareAlike 4.0**;
- original images and non-code assets: **CC BY-SA 4.0**, unless an asset needs another clearly recorded license;
- contributions: the same license as the component being changed, with an inbound-equals-outbound policy, a lightweight Developer Certificate of Origin process, and explicit consent needed for the project's RimWorld/Steam Workshop distribution terms;
- third-party implementation: import only after a file-level license and provenance review, preserving copyright and license notices.

This recommendation matches the stated goal of free, collaborative, community-owned development and makes proprietary forks of the optimizer itself difficult. It does **not**, by itself, prohibit someone from charging money for a copy. Genuine open-source licenses must allow commercial redistribution. Under the GPL, however, a person who receives a paid copy also receives the right to obtain the corresponding source and redistribute it, so a closed paywall cannot create exclusive control over the software. Separately, RimWorld's current EULA requires Mods to be distributed for free.

This is a technical licensing assessment, not formal legal advice.

## Required runtime dependencies

| Project | Current license | Effect on this project | Recommended treatment |
|---|---|---|---|
| [Harmony](https://github.com/pardeike/Harmony/blob/master/LICENSE) | MIT | Does not force our license; redistributed copies require its copyright/license notice | Workshop/runtime dependency; do not bundle unless necessary |
| [Prepatcher](https://github.com/Zetrith/Prepatcher/blob/master/LICENSE) | MIT | Does not force our license; redistributed copies require its copyright/license notice | Required early-bootstrap dependency; use its public API |

Prepatcher's current project description states that it provides Harmony for RimWorld and can satisfy dependencies on the Harmony mod. Both may be active without conflict. The exact `About.xml` dependency declaration should be decided during packaging, but the architecture may rely on both APIs.

No other mandatory user-installed dependency is currently recommended. Any future runtime dependency should meet all of these conditions:

- available through the Workshop or already supplied by an approved dependency;
- compatible with Windows, Linux, and macOS;
- actively maintained or small enough to vendor responsibly;
- compatible with the selected copyleft license;
- materially better than implementing the narrow required capability in the project;
- unable to create a second independent scheduler, cache owner, or patching framework that undermines the architecture.

Build-time tools and embedded libraries do not have to be separate Workshop subscriptions, but each still requires a license, security, platform, and packaging review.

## RimWorld and Steam distribution terms

The project license is not the only set of terms involved in publishing a RimWorld Workshop mod.

### RimWorld EULA

The [current official RimWorld EULA](https://rimworldgame.com/eula/) states that:

- Mods must be distributed free and may not be commercially exploited without Ludeon's prior written consent;
- Mods must work only with a full registered copy of RimWorld;
- Mods must not contain a substantial part of the game's code or other content;
- mod creators own their derivative work but grant Ludeon a broad, irrevocable license to use, modify, distribute, and sublicense it;
- Mods must display Ludeon's required unofficial-content disclaimer;
- decompiled game code/assets may be studied for learning or used as a reference for a Mod, but game resources may not be redistributed independently.

The required disclaimer is:

```text
“Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.”
```

This disclaimer belongs in the Workshop description, repository README, and packaged About/notice material before public distribution.

The EULA's free-distribution rule already prevents an authorized RimWorld Mod distribution from being placed behind a required paywall. That protection comes from Ludeon's terms, not from our open-source license, and Ludeon can update or interpret its own EULA. The project should not try to duplicate the restriction inside a custom “noncommercial open-source” license.

### Steam Workshop terms

The [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/) grants Valve a broad, non-exclusive license to host, use, reproduce, modify for Workshop/game compatibility, distribute, and promote uploaded Workshop Contributions. It also requires the uploader to have sufficient rights in every contribution to grant those permissions.

Consequences for this project:

- every contributor must know that accepted code may be packaged and uploaded to Steam Workshop under Valve's and RimWorld's applicable terms;
- a DCO alone confirms provenance but may not be enough to document the additional Workshop-distribution authorization;
- contribution terms should add a narrow statement authorizing the project maintainers to distribute the contribution as part of the RimWorld mod through Steam Workshop and other free channels, subject to the project license and platform terms;
- this authorization should not assign copyright or give maintainers general relicensing power;
- third-party code must permit the Steam/Workshop distribution grant or remain an external dependency rather than being copied into the package.

### GPL compatibility caution

GPL plus an explicit RimWorld/Unity linking permission addresses the proprietary-host issue for original project code. It does not automatically resolve every interaction among GPL, Ludeon's derivative-work license, Steam's upload grant, and third-party contributions.

Before the first public source or Workshop release, obtain a focused legal review or an informed opinion from an open-source licensing specialist on:

1. the exact GPL additional-permission text;
2. the Workshop authorization in the contribution terms;
3. whether corresponding source should be included directly in the Workshop package or whether an exact tagged GitHub source link is sufficient;
4. whether any imported GPL/EPL code can be included under the same distribution model.

Until that review, keep implementations original or MIT-derived, record provenance, and do not import Hyperdrive/MissileGirl code.

## Existing loader and profiler projects

| Project | Current repository license | May we study behavior? | May we copy implementation into the proposed GPL project? | Plan |
|---|---|---:|---:|---|
| [DefLoadCache](https://github.com/FluxxField/rimworld-defload-cache/blob/main/LICENSE) | MIT | yes | yes, with notice | useful reference; prefer selective, reviewed reuse or independent implementation |
| [Faster Game Loading - Continued](https://github.com/mushroomTW/FasterGameLoading---Continued/blob/main/LICENSE) | MIT | yes | yes, with notice | useful for parallel XML and guarded asset techniques |
| [Loading Progress](https://github.com/ilyvion/loading-progress) | Apache-2.0 OR MIT | yes | yes; selecting MIT is simplest for reused files | useful for progress UI and attribution concepts |
| [Hyperdrive](https://github.com/vopaga/rimworld-hyperdrive/blob/main/LICENSE) | GPL version 3 | yes | conditionally | do not copy without a specific provenance/linking-exception review |
| [MissileGirl/Gagarin](https://github.com/ViralReaction/MissileGirl/blob/main/LICENSE) | EPL-2.0 | yes | conditionally and file-by-file | behavioral reference; avoid direct reuse unless a later compatibility review approves it |
| [FastLoader](https://github.com/solaris0115/FastLoader) | no declared license found | yes, at the level of public behavior and documentation | no | behavioral research only; do not copy source or binary formats mechanically |

### Why “conditionally” matters

MIT code can be incorporated into a GPL project because MIT grants broad reuse rights; the original copyright and license text must remain with substantial copied portions.

Hyperdrive's repository declares GPL version 3. If we copy GPL-covered implementation, the combined distribution must comply with that GPL. More importantly, only the original copyright holder can grant an extra exception for their code to link with GPL-incompatible proprietary software. Our proposed RimWorld linking permission can cover code written by this project, but it cannot automatically be added to copied third-party GPL code. Therefore:

- concepts, public behavior, measurements, and independently designed algorithms remain research inputs;
- direct Hyperdrive code reuse requires either confirmation that its existing licensing is sufficient for the intended RimWorld distribution or permission from its author to use the same linking exception;
- until then, do not copy its source.

MissileGirl uses EPL-2.0, a file-level copyleft license. EPL can participate in some larger GPL works under its secondary-license mechanism, but the precise file notices and project designation matter. This is unnecessary complexity for the planned architecture, so direct Gagarin code reuse is not recommended.

FastLoader exposes detailed public behavior and cache descriptions but has no license file in the current repository. Default copyright therefore applies. Its ideas may inform requirements and testing, but its code, data structures, and implementation text should not be copied.

## License decision and considered alternatives

### Option A: GPL-3.0-or-later with a RimWorld linking permission

This is the approved software-license direction.

What it accomplishes:

- modifications and derivative versions of the optimizer must remain available under the GPL when distributed;
- recipients must receive or be offered corresponding source;
- attribution and license notices remain attached;
- community forks retain the same freedoms;
- MIT-licensed Harmony, Prepatcher, DefLoadCache, Faster Game Loading, and optionally selected Loading Progress code can be combined with proper notices;
- an explicit additional permission removes avoidable uncertainty about loading the mod with proprietary RimWorld/Unity assemblies and independently licensed mods.

What it does not accomplish:

- it cannot prohibit selling copies or charging for distribution;
- it cannot require every fork to be published publicly if the fork is never distributed;
- it does not automatically give us permission to relicense someone else's GPLv3-only code under our linking exception;
- it does not protect the product name or logo as a trademark.

The linking permission should be narrow: it should allow compiling, linking, and running with RimWorld, Unity, approved bootstrap/runtime APIs, and independently distributed mods without weakening copyleft for modifications to this optimizer.

Before publication, the exact exception text should be reviewed carefully and applied consistently to every original source file. The GNU GPL FAQ explicitly recommends an exception when GPL software must link with a GPL-incompatible library and notes that only the copyright holder can grant it.

### Option B: MPL-2.0

MPL is the simpler alternative if we want less legal friction around a proprietary game host.

- changes to existing MPL-covered files must remain MPL and source-available;
- new separate files can use another license, including a proprietary license;
- combining the mod with proprietary RimWorld/Unity code is expressly easier;
- it is weaker protection against a mostly proprietary fork that keeps only modified original files open.

Mozilla describes MPL as file-level copyleft. This makes it practical for plugins but less aligned with the desire to keep the complete optimizer implementation community-owned.

### Option C: LGPL-3.0-or-later

LGPL protects modifications to the licensed library while permitting linking by differently licensed software. It is a reasonable middle ground, but this project is an application/mod rather than a reusable library. The distinction between the optimizer and linked extensions could become unclear, making MPL or GPL-with-permission easier to communicate.

### AGPL and noncommercial clauses

AGPL adds network-service source obligations. This local game mod does not provide a network service, so AGPL offers no meaningful advantage over GPL.

A “noncommercial,” “no paywall,” or Commons-Clause restriction would prevent the license from being standard open source. The Open Source Definition requires free redistribution and prohibits discrimination against fields of endeavor, including business use. Such restrictions would also complicate Workshop mirrors, Linux distributions, mod packs, and community support. They are not recommended.

## What GPL means for a paywalled copy

GPL permits someone to charge any amount for delivering a copy. That cannot be prohibited while retaining standard open-source freedoms.

The practical protection is that a distributor must provide equivalent access to the corresponding source under GPL terms. A recipient can then legally redistribute both the binary and source, free of charge if they choose. A seller can offer convenience, support, or early packaging, but cannot turn the received GPL code into an exclusive closed-source product.

Attribution can also be protected through:

- copyright notices in source files and the license;
- a `NOTICE` or `THIRD_PARTY_NOTICES` file for imported work;
- visible project credits in the repository and Workshop page;
- a distinct project name and logo, potentially protected separately as trademarks if that ever becomes important;
- a release manifest that identifies official builds and source revisions.

Copyright and trademark serve different purposes. The software license controls code freedoms; a trademark policy can prevent unofficial commercial builds from pretending to be the official project without restricting their right to use the code.

## Recommended contribution model

### Inbound equals outbound

Contributions arrive under the same license already applied to the component:

- GPL plus the approved linking permission for optimizer code;
- CC BY-SA 4.0 for documentation and original community-facing media;
- the original third-party license for files intentionally retained under another compatible license.

This avoids a contributor license agreement that transfers broad relicensing power to one maintainer.

### Developer Certificate of Origin

A Developer Certificate of Origin (DCO) is a short contributor statement that the contributor has the right to submit the work under the project's license. Contributors add a `Signed-off-by` line to commits.

Recommended because it:

- records provenance without assigning copyright away;
- supports collaborative community ownership;
- helps prevent accidental copying from incompatible or unlicensed projects;
- is lighter than a custom contributor license agreement.

The DCO can be adopted when the public repository opens; it is not needed for current private planning documents. For this specific project, it should be paired with a narrow Workshop-distribution contribution notice rather than treated as the complete submission agreement.

### Third-party provenance rule

Every imported file or substantial copied fragment must record:

- upstream project and canonical URL;
- exact commit/release;
- original file path;
- original author/copyright notice;
- license and required notices;
- local modifications;
- reviewer approval that the license is compatible.

No source should be copied from FastLoader or another repository without a declared license. No GPL/EPL source should be pasted into exploratory code “temporarily,” because provenance is easily lost later.

## Proposed repository license layout

If the recommendation is approved:

```text
LICENSES/
  GPL-3.0-or-later.txt
  CC-BY-SA-4.0.txt
LICENSE
COPYING.EXCEPTION
NOTICE
THIRD_PARTY_NOTICES.md
CONTRIBUTING.md
DCO.txt
WORKSHOP_CONTRIBUTION_TERMS.md
```

- `LICENSE` identifies GPL-3.0-or-later as the default for software.
- `COPYING.EXCEPTION` contains the RimWorld/Unity linking permission.
- documentation files carry an SPDX identifier for CC BY-SA 4.0.
- code files carry `SPDX-License-Identifier: GPL-3.0-or-later` plus the exception notice or a project-defined exception identifier after review.
- imported permissive files retain their original notices and are listed in `THIRD_PARTY_NOTICES.md`.
- `WORKSHOP_CONTRIBUTION_TERMS.md` records the narrow permission needed to package accepted contributions under the RimWorld and Steam Workshop terms without transferring contributor copyright.

Create these files when the source project is established, after the exact linking exception and Workshop contribution language have been reviewed. The standard GPL/CC license texts must be copied verbatim from their canonical sources; project-specific permissions belong in separate, clearly named files.

## Approved policy and pre-publication conditions

The following policy is approved for project planning and source development:

1. GPL-3.0-or-later for original optimizer software.
2. A narrow additional permission for linking/running with RimWorld, Unity, Harmony, Prepatcher, and independently distributed RimWorld mods.
3. CC BY-SA 4.0 for original documentation and media.
4. Inbound-equals-outbound contributions plus DCO and a narrow Workshop-distribution permission, without copyright assignment or broad relicensing authority.
5. Harmony and Prepatcher as external Workshop dependencies rather than redistributed binaries.
6. MIT prior art may be selectively reused with exact provenance and notices.
7. Hyperdrive and MissileGirl remain design references unless their code receives a separate license/exception review.
8. FastLoader remains behavioral research only while it lacks a declared license.
9. Public source/Workshop distribution waits for review of the exact GPL linking permission and Workshop contribution terms.

The selected direction is GPL-3.0-or-later with a narrow linking permission, rather than MPL-2.0. The remaining work is legal-text verification and distribution compliance, not a renewed permissive-versus-copyleft product decision.
