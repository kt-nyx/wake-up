# Windows Steam and Linux live validation — 2026-09-08

> Historical evidence from public source `b2d83c3`, archived 9 September 2026. Pending statements are dated history; use [current state](../current-state.md) for the current summary.

Both platforms completed a real startup, drew the main menu and exited through
the game's normal shutdown path. The PNG cache and Giddy-Up graphics output
matched the corresponding original game/mod output. This supports the owner's
requested promotion to regular version 0.2.0; it is not a gameplay/save-load or
performance qualification.

## Final checks

| Check | Windows Steam rev590 | Native Linux rev600 / Steam Deck |
|---|---|---|
| Graphics | Direct3D 11 | OpenGLCore |
| Menu repaint observed (UTC) | 22:35:44.377 | 22:41:50.078 |
| Normal shutdown | Direct process exit 0 at 22:35:56 | Observer requested shutdown; Steam launch wrapper exit 0 at 22:42:05 |
| Giddy-Up reads / original comparisons | 1,682 / 1,682 | 1,682 / 1,682 |
| Giddy-Up errors / mismatches / retained textures | 0 / 0 / 0 | 0 / 0 / 0 |
| PNG calls / cache hits / misses | 6 / 6 / 0 | 6 / 6 / 0 |
| PNG output comparisons / mismatches | 6 / 0 | 6 / 0 |
| PNG cache errors / ordinary texture fallbacks | 0 / 0 | 0 / 0 |
| Loading Progress texture reloads / fallbacks | 313 / 0 | 313 / 0 |

The normal 313-mod owner selection uses DDS textures, so an initial content
check exercised the Loading Progress bridge without PNG calls. Six temporary
RGBA PNGs (1×1, 3×5, 8×8, 16×32, 64×64 and 128×64) then exercised compression,
mipmaps, uncompressed output, cache creation and later cache restoration. Each
platform wrote six entries without cache errors in the preceding check; the
final run reused all six and compared rendered mip output and texture properties
with the original loader. There is no fixture recognition or special handling
of these samples in Wake-Up.

The final selection had 312 regular mods, with only `astryl.moderndevtools`
temporarily disabled, plus an independent temporary menu observer. Modern Dev
Tools' log window prevented the menu callback in preceding attempts; the owner
requested the exclusion. The background-running preference was temporarily
enabled. Neither these controls nor the comparison flags are required for
normal use. The original load orders, Wake-Up settings and background preference
were restored; the observer and temporary PNG assets were removed.

Character Editor completed 9,182 object and 82 turret presets without exceptions,
fallbacks or retained scope entries. Definition/type searches and Loading
Progress coalescing completed. Gagarin reuse declined the known Loading Progress
XML hook conflict. No new error headlines were found against the preceding
normal platform logs. Loading Progress's generic warning about the native
ReloadContentInt patch is not a failed integration: its replacement iterator
completed all 313 texture reloads without fallback.

## Identity and limits

The tested DLL SHA-256 is
`b88d66b0cc1b318e33d27ac770589e275039560b564a8dac0fa5f0ddd7b4622b`,
from public source `622d96ff81faa2a8b1317cab52e2589a6d19d599` and
0.2.0-preview.1. Version 0.2.0 changes release metadata/documentation and one
settings-label string; the optimization implementations are unchanged. Its
manifest identifies the separately rebuilt final DLL and source revision.

Raw captures, installation/cleanup receipts and comparison summaries are under
ignored `artifacts/live-release-check/`, particularly `pc-no-modern-warm/` and
`deck-no-modern-warm/`. Earlier interrupted attempts are excluded. One Deck
diagnostic call caused a debugger-induced crash; that attempt is discarded,
not attributed to Wake-Up. The successful final Deck run used a local Steam
application launch in offline mode, native PID 8514, and Gamescope focus app
294100; no streaming client ran. No debugger was attached during either final
successful check.

No save was loaded or changed. Extended gameplay, other graphics backends and
other exact game/supplier versions remain outside this evidence. The legacy
`steamQualified` manifest field remains false for broader gameplay qualification;
the explicit `steamStartupValidated` and `linuxStartupValidated` fields record
the completed startup checks. Regular release status does not expand that scope.
