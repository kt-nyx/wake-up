# Wake-Up: Loading Optimizations

> **1.7 development:** no reliable loading-time improvement was established. The unqualified preview features are disabled in current development source, including for existing enabled profiles. Use native 1.7 loading. See [measurements and decisions](docs/qualification-1.7-cloud-20261019.md). No binary release or played installation is included.

Wake-Up avoids selected repeated loading work in RimWorld. **0.4.2** is available
on [GitHub](https://github.com/kt-nyx/wake-up/releases/tag/v0.4.2) and
[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3798073152),
with clearer compatibility notices on the existing 1.6 feature set. It retains
the 0.4.1 supplier support and 0.4.0 compatibility fixes. See the
[current state](docs/current-state.md) for verified release identities.

0.4.2's notice changes received offline validation. Earlier 0.4.1 startup,
392 Giddy-Up readbacks and sampled-artwork checks belong to their original DLL,
as do older gameplay/performance results. Linux/Deck remains outside the tested scope.

The published 1.6 version requires Prepatcher; supported supplier mods remain optional. Benefits depend
on the workload. Enabling every optional feature is not a performance preset.

The separate **unpublished 1.7 preview** requires no Prepatcher. Its search
features failed to establish a useful loading benefit on the reviewed 1.7 stream
and are disabled. Exact compatibility checks and legacy preferences are retained.
The [implementation plan](docs/implementation-1.7.md) and
[testing handoff](docs/testing-1.7.md) preserve earlier candidate history. Final
Windows verification and binary/tag/Workshop release remain owner-controlled.

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
