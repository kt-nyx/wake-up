# Wake-Up 0.2.0-preview.1 — Windows Steam and Linux support

Adds support for **Windows Steam RimWorld 1.6.4871 rev590** and **native Linux /
Steam Deck RimWorld 1.6.4871 rev600**, alongside the existing reviewed Windows
GOG build. No extra launch flag is required.

## Changes

- Recognizes the reviewed Windows Steam and Linux game files while retaining
  exact dependency and conflicting-patch checks.
- Supports Giddy-Up's optimized texture readback on Linux OpenGL.
- Caches the game's CPU-processed PNG textures, including compression and
  mipmaps, with Steam's default disabled-compute settings on Linux.
- Integrates PNG caching with Loading Progress's staged texture loader on both
  platforms, preserving its progress reporting and other loading stages.
- Fixes Linux game-file discovery and distinguishes unsupported game builds
  from Harmony problems in startup diagnostics.

## Testing and limits

This is a **compatibility preview**. Five features completed startup in the
owner's Steam Deck launch, including 1,682 Giddy-Up OpenGL reads with no reported
errors or fallbacks. The final Windows Steam admission and PNG/Loading Progress
integration passed offline checks but have not yet completed live validation.
PNG cache creation/reuse, original-output comparison and focused gameplay /
save-load coverage remain pending on the new combinations.

Validation: 101 managed tests passed, with one existing optional supplier test
skipped; all 35 Python tests passed. Another 22 focused tests passed against the
actual Windows Steam game/Harmony files. Relevant game-method comparisons
support the exact added identities. No new loading-speed claim is made.

PNG caching remains **off by default**. Enable it in Wake-Up's mod settings if
desired; creating the cache can slow the first launch. DDS textures retain their
normal precedence. The separate Gagarin / Loading Progress XML conflict still
uses ordinary loading. Unknown game or optional-mod versions also retain their
ordinary behavior; this is not support for every RimWorld 1.6 build or graphics
backend.

## Installation

Update the existing Workshop item, or replace your local `wake-up` folder with
the folder from the ZIP. Keep Prepatcher enabled and load Wake-Up after it. Do
not enable a local copy and the Workshop copy together. Existing Wake-Up settings
are retained. The ZIP and Workshop package include matching source and notices.
