# Wake-Up 0.2.0 — Windows Steam and Linux support

Supports **Windows Steam RimWorld 1.6.4871 rev590** and **native Linux / Steam
Deck RimWorld 1.6.4871 rev600**, alongside the reviewed Windows GOG build.
No extra launch flag is required.

This regular release contains the compatibility work from 0.2.0-preview.1:
Linux game-file discovery, OpenGL Giddy-Up texture reads, CPU-processed PNG
caching, and PNG integration with Loading Progress's staged loader. Unknown
game and optional-mod versions retain ordinary behavior. The settings screen's
outdated Linux preview message has been replaced; optimization code is unchanged
from the live-tested preview runtime.

Both platforms reached the main menu and exited normally in the final live
checks. On each platform, all **1,682 Giddy-Up reads matched the original**, and
**six cached PNG textures matched the original output with six cache hits and
zero cache errors**. Earlier checks created those six entries successfully.
The samples cover compressed textures with mipmaps and small uncompressed
textures. Exact scope and package identities are in the
[validation record](../docs/steam-linux-live-validation.md).

Modern Dev Tools was temporarily disabled for the final unattended checks;
its log/stack-trace window blocks the menu callback. The test observer and PNG
samples were removed afterward, and original settings/load orders restored.
The known Gagarin / Loading Progress XML conflict still uses ordinary loading.
Loading Progress may warn about the native texture-loader patch; the tested
staged-loader integration completed without errors or fallbacks.

PNG caching remains **off by default**. Building its cache may slow the first
launch; DDS textures keep their normal precedence. These are startup and texture
checks, not new-platform gameplay/save-load qualification or a speed benchmark.

Update the existing Workshop item or replace your local `wake-up` folder from
the ZIP. Keep Prepatcher enabled and load Wake-Up after it. Enable only one copy
of Wake-Up. Existing settings are retained; matching source and notices ship
with both distributions.
