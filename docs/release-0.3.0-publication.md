# 0.3.0 publication receipt — 22 September 2026

The owner authorized commit, merge and release of the confirmed Windows candidate,
then explicitly required the mod description remain unchanged. Publication succeeded.

- Private review branch merged by fast-forward into private `main`, release source
  `12e616dcb2ec7ff3c94de63ea6a97da7d9612262`.
- Public source commit `d5fc9646d74a47a6f30fe0556be02f44cd1a67dc` descends from
  public 0.2.1, with exactly the same source tree as the private release commit.
  No private history was pushed. Public tag `v0.3.0` points to that public commit.
- [GitHub release](https://github.com/kt-nyx/wake-up/releases/tag/v0.3.0) published
  at 2026-09-22 15:19:53 UTC. The uploaded ZIP digest matches the local package:
  `d243f6738cd5dd93e30378c47cfb9c7bd91b18e0e3005a05b67b9429ef99178b`.
- [Source checks](https://github.com/kt-nyx/wake-up/actions/runs/35746463661) passed.
  All 86 local Python checks passed, as did the fixture Git boundary guard.
- [Workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=3798073152)
  update returned result 1, no I/O failure and no agreement requirement. Its content
  handle changed. Description, title, visibility, tags and preview URL matched
  before/after. The publisher never called the description setter. Source
  `release/About.xml` and `release/steam-description.bbcode` were also unchanged.
- Every package payload hash and every bundled current-source file was checked
  against the public source tree. Core DLL remains the accepted
  `a64f9a446aaf7743024e0246f5e795dee1986f9a5eb647bc12246955af2ddfda`;
  optional helper remains
  `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`.
  Matching source and notices are included. No new runtime build, game launch,
  performance run, normal-data edit or Deck operation was performed for publication.

The release is qualified on Windows Steam and GOG within the recorded workloads.
Linux/Deck remains unqualified for this version; the release notes disclose that
limitation. This is not full ecosystem replacement or a universal speedup claim.
The original description's private-candidate wording remains by explicit owner
instruction; the manifest and separate release notes give current release status.

Raw receipts are private under `artifacts/release-0.3.0/`, including package
verification, before/after Workshop metadata, submission/result and GitHub receipt.
The source-inclusive package is under
`artifacts/releases/0.3.0-12e616dcb2ec7ff3c94de63ea6a97da7d9612262-texture-helper-win-x64/`.

The old campaign timer stays paused. The [next steward prompt](ecosystem-replacement/aggressive-loading-steward-prompt.md)
prepares early whole-stage XML reuse, direct image decoding and overlap of independent
work. On-demand loading is explicitly excluded. No successor task has been created.
