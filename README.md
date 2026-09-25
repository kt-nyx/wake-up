# Wake-Up: Loading Optimizations

Wake-Up avoids selected repeated loading work in RimWorld. Corrected **0.4.0**
improves cooperation with other loaders and fixes YaOpt worker rebuilding and
Adaptive Storage Framework inheritance conflicts. The owner has authorized its
publication; see [current state](docs/current-state.md) for completion status.

The exact release DLL passed a targeted Windows menu check with YaOpt, Image
Opt and ASF. Linux/Deck remains outside the tested scope.
Earlier gameplay and performance results retain their original build identities;
they are not new measurements of this corrected DLL.

Prepatcher is required; supported supplier mods remain optional. Benefits depend
on the workload. Enabling every optional feature is not a performance preset.

- [User guide](docs/user-guide.md): installation, settings and limitations.
- [Current state](docs/current-state.md): exact release and repository status.
- [0.4.0 release notes](docs/release-notes-0.4.0.md): changes and measured tradeoffs.
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
