# RimWorld Loading Optimizer

RimWorld Loading Optimizer provides targeted startup improvements. It accelerates supported XML definition/template and Harmony type searches, with optional improvements for Gagarin, Character Editor, Loading Progress, Giddy-Up and processed PNG textures. The [results and decisions](docs/results.md) page summarizes what is retained and why.

The XML search feature builds a temporary lookup table (an index) from the active mod XML when needed. It finds a definition's `defName` or a template's `Name` directly, then lets the normal XML engine evaluate the rest of the query. Relevant changes invalidate the affected index; patch completion releases it. No prebuilt modlist data or persistent XML cache is shipped.

This is an unpublished development package. Ordinary launches now use in-game settings: supported searches and optional Gagarin, Character Editor, Loading Progress and Giddy-Up improvements default on. Processed-PNG caching is opt-in because its first build can slow loading. Unsupported game, supplier and hook conditions retain ordinary behavior. Qualification is limited to the recorded exact Windows GOG fixture; see the current-state record for package and live coverage.

- [Current state](docs/current-state.md): what is built, deployed, tested and enabled.
- [User guide and support policy](docs/user-guide.md): activation, settings, supported builds and limitations.
- [Architecture](docs/architecture.md): how retained features work and preserve existing behavior.
- [Development and fixture](docs/development.md): build, test, compare and prepare for publication.
- [Results and decisions](docs/results.md): demonstrated benefits, failed experiments and unresolved questions.
- [Historical evidence](docs/archive/README.md): curated detailed records and recovery pointers.

The implementation is in [src/RimWorldLoadingOptimizer.RimWorld](src/RimWorldLoadingOptimizer.RimWorld); focused checks are in [tests](tests). Private game files, packages and raw evidence are excluded from Git. Read [AGENTS.md](AGENTS.md) before agent work.

Licensing: [GPL-3.0-or-later with linking permission](LICENSE.md) for original code; CC BY-SA 4.0 for original documentation. See [notices](NOTICE), [contributing](CONTRIBUTING.md), and [source-build instructions](docs/source-build.md). Final release copy is pending.

Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
