# 0.4.0 publication receipt — 25 September 2026

Corrected **0.4.0 is published on GitHub and Steam Workshop**. The exact reviewed
package was uploaded after the user reconnected Steam. Both destinations confirmed
completion, and the live Workshop description and other listing fields are unchanged.
The owner authorized both destinations and required the live Workshop description
and unrelated listing fields to remain unchanged.

## Published package and source

- [GitHub release](https://github.com/kt-nyx/wake-up/releases/tag/v0.4.0) published
  at **2026-09-25 16:28:13 UTC**, with `wake-up-0.4.0.zip` (2,444,901 bytes).
  GitHub's uploaded asset digest matches the reviewed local ZIP:
  `32b0c9b9460bdd2523770f8c125fae419649f21522e51b54b7b7200b0bcc3907`.
- Private package/source snapshot:
  `5364455e499505b56d57883b546feaac780be9bb`.
  Public `v0.4.0`: `34e105b9e658ec9646a3bb25535e117c6fed0ab2`, whose sole parent
  is public `786d222cf8337f7ea4290df6f6360c9471608b83`. Both source trees are
  `8c53dca7d5cada4a84fd245d49ae83394ae86662`. No private ancestry was pushed.
- All 27 package files and all 493 bundled source files were checked against
  the reviewed package/source tree. Matching helper source and notices are included.
  No game binary, private fixture payload or observer runtime DLL is distributed.
- The exact tested core was reused without rebuilding: build source
  `3ac8973a2aee7879c0e184bd124d911198018875`, DLL SHA-256
  `ccfa3f0a6777856120a26181ada1ba642bbe2925fcee5d03702f45e0f67614c6`.
  Optional Windows helper SHA-256 remains
  `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`;
  original helper source is `83a2930f60967eba4fcfb8e6acef10f35a415c62`.
- Both public [main source checks](https://github.com/kt-nyx/wake-up/actions/runs/36160941324)
  and [tag source checks](https://github.com/kt-nyx/wake-up/actions/runs/36160941391)
  completed successfully. The Git boundary hook also passed.

The Windows release retains the bounded evidence and limitations in the
[release notes](release-notes-0.4.0.md). Linux/Deck remains outside the tested scope.
Earlier performance and broader colony evidence belong to their named earlier
binaries. No rebuild, game launch or performance test was performed for publication.

## Workshop completion

The content-only publisher verified app 294100, ownership of
[item 3798073152](https://steamcommunity.com/sharedfiles/filedetails/?id=3798073152)
and every staged package hash. It calls only the content setter; change notes
are submitted separately. Staging adds the existing unchanged `About/Preview.png`
and `About/PublishedFileId.txt` to the source-inclusive package.

The first submission returned Steam result **2 (failure)** while the local client
was signed out. Fresh metadata confirmed unchanged content and description. The
user reconnected Steam; the publisher verified `BLoggedOn=true` before retrying.
That update completed with **result 1 (success)**, no I/O failure and no agreement
requirement at **2026-09-25 16:31:02 UTC**.

The content handle changed from `5225586698232992710` to
`7760635627846918657`; Steam reports 4,673,172 uncompressed content bytes. The live
description is byte-for-byte equal before and after. Title, visibility, tags,
preview URL, creator, app IDs, item ID and other checked static fields also match.
The tracked `release/steam-description.bbcode` remains unchanged; the live
description is preserved independently. No description, title or preview setter
was called. The failed first attempt remains recorded separately.

Private package, metadata, failed submission/result and verification receipts are
under `artifacts/release-0.4.0/`. The exact release folder is
`artifacts/releases/0.4.0-5364455e499505b56d57883b546feaac780be9bb-texture-helper-win-x64/`.
The package and tag retain their reviewed source snapshot; later publication
receipt edits do not rebuild or silently replace that published archive.
