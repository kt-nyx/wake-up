# Wake-Up 0.3.0

Owner-authorized release of the tested retained candidate. Windows Steam and GOG
are qualified within the recorded workloads. **This version has not been qualified
on Linux or Steam Deck.** The optional texture helper is Windows-only. Linux users
who need the previously validated version can retain 0.2.1 from GitHub.

## Changes from 0.2.1

- Reuse selected processed definitions and applied translations, and expand
  type/member searches.
- Extend image reuse to JPEG, add optional PSD support, and provide explicit
  texture preparation, quality choices and reset controls. Original files stay intact.
- Add loading display/reports and loading-specific background behavior that returns
  to the player's ordinary background preference when loading finishes.
- Share an adjustable cache budget, now 1 GiB by default rather than the previous
  PNG cache's 512 MiB.
- New optional controls default off. Additional search reuse follows the existing
  default-on type-search option. Prepatcher remains required.

This is selective loading functionality, not full replacement of other loading
mods. Retired experimental XML, language, first-build image and atlas paths use
native behavior. On-demand loading is excluded from the next campaign; progressive
detail and the companion app are not included. Streaming XML remains experimental.

## Validation and performance

The exact retained DLL passed Windows Steam correctness, actual settings/quality
and copied-colony UI checks, background/focus-return checks, and a focused GOG
regression. Full XML/language outputs matched native controls; image samples and
normal fixture restoration passed. Known InputLegacyModule logging remains;
arbitrary modlists and gameplay are not universally qualified.

The small English default workload was effectively tied: native 12.29 s,
0.2.1 12.54 s, 0.3.0 12.36 s. Separate workloads measured 2.65% for the complete
type/member-search group, 10.89% for French translation reuse and 15.78% for warm
PNG-selected reuse. These are not additive or incremental gains over 0.2.1.
First cache construction added 12.05 seconds and 333.5 MB, repaid after about four
later matching launches. Enabling every option is not a recommended speed preset.

See [the qualification report](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/windows-steam-qualification-20260922.md)
for workload, controls and limitations. The release reuses tested core
`67265e5696c80c9a461423722870a742054dd43b` unchanged, SHA-256
`a64f9a446aaf7743024e0246f5e795dee1986f9a5eb647bc12246955af2ddfda`, and
the qualified optional Windows quality helper. Source and notices are included.

The owner explicitly requested that the mod description remain unchanged.
Accordingly its older private-candidate wording is preserved in About.xml, and
the Workshop description, preview and unrelated listing fields are preserved.
These release notes and the package manifest record the current publication scope.
