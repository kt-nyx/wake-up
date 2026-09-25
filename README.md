# Wake-Up: Loading Optimizations

Wake-Up avoids selected repeated loading work in RimWorld. **0.4.1** is available
on [GitHub](https://github.com/kt-nyx/wake-up/releases/tag/v0.4.1) and
[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3798073152),
with specific support for FGL Continued Preview + Image Opt + the current
ImageOptCompat repository distribution. It retains the 0.4.0 YaOpt worker and
Adaptive Storage Framework inheritance fixes. See the
[publication receipt](docs/release-0.4.1-publication.md).

The exact release DLL passed startup, 392 Giddy-Up texture reads and sampled-artwork
checks with the supported combination. Linux/Deck remains outside the tested scope.
Earlier gameplay and performance results retain their original build identities;
they are not new measurements of this corrected DLL.

Prepatcher is required; supported supplier mods remain optional. Benefits depend
on the workload. Enabling every optional feature is not a performance preset.

- [User guide](docs/user-guide.md): installation, settings and limitations.
- [Current state](docs/current-state.md): exact release and repository status.
- [0.4.1 release notes](docs/release-notes-0.4.1.md): compatibility change and validation limits.
- [0.4.0 release notes](docs/release-notes-0.4.0.md): earlier changes and measured tradeoffs.
- [Compatibility guide](docs/compatibility.md): mod/tool decisions and setting choices.
- [Source build](docs/source-build.md): dependencies and build commands.
- [Architecture](docs/architecture.md): implementation and inactive research.
- [Development](docs/development.md): repository and fixture operations.
- [Release procedure](docs/release.md): packaging and separate public history.
- [Results and history](docs/history.md): benefits and stopped approaches.

Source is in [src/WakeUp](src/WakeUp), with checks in [tests](tests).
Distributions include corresponding source and notices; private game data and
development observers are not installed runtime components.

See [licensing](LICENSE.md), [notices](NOTICE), [contributing](CONTRIBUTING.md)
and [agent guidance](AGENTS.md). Original code uses GPL-3.0-or-later with the
permission in LICENSE-EXCEPTION.md; original documentation uses CC BY-SA 4.0.

Portions of the materials used to create this content/mod are trademarks and/or
copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This
content/mod is not official and is not endorsed by Ludeon.
