# Using Wake-Up

Wake-Up avoids repeated work during startup. With a supported game and dependency build, enabling the mod now enables its established improvements without special launch arguments. This is an early development build with deliberately narrow compatibility; a matching version number alone does not establish support.

## Installation and settings

The build output contains a normal RimWorld mod folder with `About` and `Assemblies`. Place that folder in the game's `Mods` directory, enable it in the mod list, and load it after Prepatcher and, if installed, the separate Harmony mod. Prepatcher is the only required Workshop dependency for this development build; it supplies the Harmony library Wake-Up uses. No DLC is required. Do not copy the separate fixture observer or any fixture game files. This project's automated qualification deploys only to its isolated fixture; it does not install into your normal game.

Open **Options → Mod settings → Wake-Up** to select features. Changes take effect after restarting RimWorld. The master switch disables all optimizations for the next launch. Each feature can also be disabled separately. Settings use RimWorld's ordinary settings storage, including any save-data-folder override selected for that game.

Definition/template searches and code type searches default on. Optional improvements for Gagarin, Character Editor, Loading Progress and Giddy-Up also default on, but only activate when the corresponding supported mod is present and its intercepted methods remain compatible. These mods are not required or bundled. Updating one can disable that feature while ordinary loading continues; do not downgrade a mod solely to satisfy Wake-Up's checks.

The **processed-PNG cache defaults off**. Building it can make the first launch slower because the mod saves finished texture data for future launches. It can help subsequent launches using those PNGs; it does not accelerate textures loaded from DDS files. Storage is capped at 512 MiB. Disabling the feature stops using and adding entries; it does not delete existing cache files. Graphics or source changes can cause entries to be rebuilt. Unsupported graphics settings use the original image loader.

Explicit development launch arguments override these settings. Ordinary users do not need them. Remove old development arguments before relying on the settings; `--wake-up-bypass` remains an emergency way to skip activation for a launch.

### Replacing a pre-rename development copy

Disable and remove the old development mod folder before installing Wake-Up;
never enable both copies. Wake-Up uses its own settings identity and `WakeUp`
data folder, so select your settings again. Previous development settings and
PNG caches are not imported or deleted. Remove old development launch arguments;
the current emergency bypass is `--wake-up-bypass`.

## Supported-build policy

Wake-Up checks actual game and dependency code, including file identity and the methods modified by other mods. This is stricter than comparing displayed version numbers. An unrecognized version uses ordinary behavior rather than an unverified replacement. A new game or supplier version requires focused identity, behavior and startup checks before it is added to the support list.

| Component | Current development contract |
|---|---|
| Operating system | Windows and native Linux / Steam Deck; live startup and texture checks completed on the reviewed builds |
| Game | Exact pinned GOG 1.6.4871 rev574 fixture (`gog-rev573`), Windows Steam 1.6.4871 rev590 (`steam-rev590`) and native Linux 1.6.4871 rev600 (`linux-rev600`); this is not all 1.6 builds |
| Harmony | Exact reviewed 2.4.2.0 binary supplied by the frozen Prepatcher environment |
| Definition/template searches | Reviewed GOG methods and matching Windows Steam/Linux methods; supported vanilla XML patch workers |
| Type searches | Reviewed Harmony fallback search; exact reviewed Windows GOG, Windows Steam and Linux game identities |
| Gagarin reuse | Exact frozen Missile Girl/Gagarin implementation; the known Loading Progress XML replacement causes ordinary fallback |
| Character Editor | Exact 1.6.3.3 binary and preset-construction methods |
| Loading Progress | Exact 0.14.0 binary and loading-loop hooks; progress may redraw less frequently within its original time budget |
| Giddy-Up | Exact 2.2.5.0 binary; Windows Direct3D 11 and Linux OpenGL |
| PNG cache | Windows Direct3D 11 compute or CPU processing; Linux OpenGL CPU processing; other backends retain original loading |

The precise hashes and implementation checks are documented in [architecture](architecture.md), [Character Editor qualification](character-editor-reintegration.md), [Loading Progress qualification](five-loading-opportunities.md), and [Giddy-Up qualification](further-loading-improvements.md). [Current state](current-state.md) records the actual tested package and live coverage.

The [Windows Steam rev590 support change](windows-steam-support.md) admits all
seven features to their existing checks. It was verified against the owner's
actual installed game files and passed a live startup, normal exit and sampled
cache/output checks. The known Gagarin/Loading Progress conflict remains an ordinary fallback.

The [Linux and Steam Deck support](linux-support.md) uses the game's default
Linux launch settings without adding any manual flag. Steam supplies
`-disable-compute-shaders`, and the captured Linux game also disables compute
processing internally; PNG caching uses its CPU texture-processing path.
Enable the cache through mod settings if desired. Both platforms completed
[live startup and texture checks](steam-linux-live-validation.md). Modern Dev
Tools was temporarily disabled during the final unattended checks because its
log window blocks the menu. Gameplay/save-load coverage remains separate.

## Expectations and limits

Improvement depends on which work your modlist performs. Cached XML can already skip the searches Wake-Up accelerates, and DDS-heavy lists may do no PNG processing. Separate feature percentages cannot be added. See the [measured comparisons](feature-benchmark-results.md).

Successful startup and focused new-colony/save-reload checks are different levels of evidence. Neither establishes every mod combination, long-running colony behavior, all graphics settings or every game update. The current qualification report states what was actually exercised. No Workshop binary release is created by these instructions. Source licensing is defined in [LICENSE.md](../LICENSE.md).
