# Wake-Up: Loading Optimizations

Wake-Up provides targeted startup improvements. It accelerates supported XML definition/template and Harmony type searches, with optional improvements for Gagarin, Character Editor, Loading Progress, Giddy-Up and processed PNG textures. The [results and decisions](docs/results.md) page summarizes what is retained and why.

The XML search feature builds a temporary lookup table (an index) from the active mod XML when needed. It finds a definition's `defName` or a template's `Name` directly, then lets the normal XML engine evaluate the rest of the query. Relevant changes invalidate the affected index; patch completion releases it. No prebuilt modlist data or persistent XML cache is shipped.

**0.2.1 adds automatic compatibility attempts after updates.** Future Windows and native Linux game revisions, and updated Character Editor, Giddy-Up, Loading Progress and Gagarin suppliers, reach feature-specific checks instead of being rejected by whole-file identity. Harmony remains pinned. PNG entries are separated by game build. See [game compatibility](docs/forward-compatibility.md), [optional mod compatibility](docs/supplier-forward-compatibility.md) and the [release notes](release/notes-0.2.1.md).

**0.2.0 supports Windows Steam and native Linux / Steam Deck.** The exact added game builds are Windows Steam 1.6.4871 rev590 and Linux 1.6.4871 rev600. It includes OpenGL Giddy-Up texture readback, CPU PNG texture caching with Steam's default Linux settings, and PNG integration with Loading Progress. No extra launch flag is required. See the [release notes](release/notes-0.2.0.md).

Ordinary launches use in-game settings: supported searches and optional Gagarin, Character Editor, Loading Progress and Giddy-Up improvements default on. Processed-PNG caching is opt-in because its first build can slow loading. Unsupported game, supplier and hook conditions retain ordinary behavior. Both new platforms passed live startup, normal exit, Giddy-Up output comparisons and sampled PNG cache creation/reuse checks. Modern Dev Tools was temporarily disabled for the final unattended checks because its log window blocks the menu. See the [validation scope](docs/steam-linux-live-validation.md); these checks do not establish gameplay/save-load coverage or a new loading-speed claim.

- [Current state](docs/current-state.md): what is built, deployed, tested and enabled.
- [User guide and support policy](docs/user-guide.md): activation, settings, supported builds and limitations.
- [Architecture](docs/architecture.md): how retained features work and preserve existing behavior.
- [Development and fixture](docs/development.md): build, test, compare and prepare for publication.
- [Results and decisions](docs/results.md): demonstrated benefits, failed experiments and unresolved questions.
- [Historical evidence](docs/archive/README.md): curated detailed records and recovery pointers.

The implementation is in [src/WakeUp](src/WakeUp); focused checks are in [tests](tests). Private game files, packages and raw evidence are excluded from Git. Read [AGENTS.md](AGENTS.md) before agent work.

Licensing: [GPL-3.0-or-later with linking permission](LICENSE.md) for original code; CC BY-SA 4.0 for original documentation. See [notices](NOTICE), [contributing](CONTRIBUTING.md), and [source-build instructions](docs/source-build.md).

Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
