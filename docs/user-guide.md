# Using Wake-Up

Wake-Up avoids selected repeated loading work. This guide describes corrected
0.4.0, authorized for publication after a targeted Windows menu check with
YaOpt, Image Opt and Adaptive Storage Framework. Linux/Deck remains outside the tested scope. See [current state](current-state.md) for
publication status and [release notes](release-notes-0.4.0.md) for evidence limits.

Each feature checks the functions and cooperating mods it needs. Unsupported
conditions normally retain ordinary loading. This is partial loading coverage:
keep supplier mods for capabilities you still need. There is no supplier-wide
replacement claim or advice to remove a supplier solely because Wake-Up is installed.

## Installation and settings

An approved distribution is installed as a normal RimWorld mod folder containing `About` and `Assemblies`, together with its included source and notices. Place that folder in the game's `Mods` directory, enable it in the mod list, and load it after Prepatcher and, if installed, the separate Harmony mod. Prepatcher is the only required Workshop dependency for Wake-Up; it supplies the Harmony library Wake-Up uses. No DLC is required. Release qualification is bounded to the workloads recorded in the release notes. No observer or test DLL is included. A Windows-helper bundle includes the optional DDS export executable, matching source and notices. Source archives also include development tooling and experiments; those sources are not automatically installed runtime components.

Open **Options → Mod settings → Wake-Up** to select features. Changes take effect after restarting RimWorld. The master switch disables all optimizations for the next launch. Each feature can also be disabled separately. Settings use RimWorld's ordinary settings storage, including any save-data-folder override selected for that game.

Definition/template searches and code type searches default on. Optional improvements for Gagarin, Character Editor, Loading Progress and Giddy-Up also default on, but only activate when the corresponding supported mod is present and its intercepted methods remain compatible. These mods are not required or bundled. Updating one can disable that feature while ordinary loading continues; do not downgrade a mod solely to satisfy Wake-Up's checks.

The **processed texture cache defaults off**. It stores finished PNG/JPEG output
and, when PSD support is enabled, decoded PSD output for later launches. Authored
DDS files already contain GPU-ready pixels, so ordinary automatic caching loads
them directly, including when compressed storage or prepared textures are enabled.
Old faithful DDS entries are ignored; explicit in-game quality conversion and DDS
exports remain separate user choices.
Automatic caching and manual preparation share the same storage. Native pixels,
all mip levels (smaller versions used at a distance), sampling and readability
are retained. Entries can exceed the former 8 MiB limit within the shared
allowance and per-image bounds.

Building entries adds work; loading benefits depend on the source workload. The
[C05 record](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c05-texture-storage.md) separates PNG-selected
results, ordinary DDS loading, explicit preparation and compression costs. Disk
savings do not by themselves establish faster loading. Automatic construction
saves bounded batches; an interrupted startup may rebuild its last unfinished
batch next time. Previously committed entries remain available. Manual preparation
saves each completed entry before continuing.

Disabling caching stops its reads and writes without deleting completed entries.
Source or graphics changes can require rebuilding; unsupported paths load normally.

**Load composited PSD images** is a separate option, off by default and applied
after restart. The decoder is included inside Wake-Up: users install nothing else.
It reads saved RGB composites in PSD version 1 with raw or PackBits encoding at
8/16-bit depth, converts to RGBA8, and treats color as sRGB. It does not render
Photoshop layers/effects or apply color profiles. Alpha follows the documented
stb white-matte interpretation, and 16-bit samples lose their low eight bits.
This is new decoding, not native PSD preservation. See the [supported format
contract](../third-party/StbImageSharp-PSD.md) before relying on less common PSD files.

**Compress stored texture bytes** is optional lossless disk compression. It can
reduce generated storage while preserving the exact texture bytes; it does not
reduce image dimensions or visual quality. Existing entries remain readable in
either setting, and newly built entries use the chosen setting.

**Faster translation application** defaults off. Enable it in the same settings menu, then restart RimWorld. It reduces repeated checks for translations already applied to a definition field, while retaining the game's assignment and warnings. The choice is saved for following launches; turning it off or disabling the master switch takes effect on the next launch. Older settings files leave it off. The older core passed bounded French native-output and repeated stage checks. Its historical Steam comparison tested this together with remembered texture suppliers, so its menu result does not isolate either feature's benefit. It does not add a translation disk cache.

Explicit development launch arguments override these settings. Ordinary users do not need them. Remove old development arguments before relying on the settings; `--wake-up-bypass` remains an emergency way to skip activation for a launch.

### Replacing a pre-rename development copy

Disable and remove the old development mod folder before installing Wake-Up;
never enable both copies. Wake-Up uses its own settings identity and `WakeUp`
data folder, so select your settings again. Previous development settings and
PNG caches are not imported or deleted. Remove old development launch arguments;
the current emergency bypass is `--wake-up-bypass`.

## Supported-build policy

Enable only one copy of Wake-Up. Public metadata warns about the old
`kt.nyx.startupfixes` bundle and `local.rimworldloadingoptimizer.op7validation`
development package. Disable the old copy before enabling public Wake-Up, or
keep the old copy and leave public Wake-Up disabled. The warning does not
automatically change your mod list.

Other loading mods are not automatically incompatible. Wake-Up may leave an
individual feature to another active mod when that mod changes the same game
functions. If Loading Progress is active, Wake-Up's independent display stays
inactive. Keep Loading Progress if you need its other capabilities; Wake-Up's
partial display coverage is not a reason to remove it. Restart after changing
either display choice. Supported Loading Progress repaint and PNG integrations
remain available under their own checks. Settings show the individual decisions.
New conflicts appear in one persistent compatibility window; acknowledgement
requires the entries to have been displayed and preserves saved choices. Wake-Up
never changes the other mod's settings or removes its patches. See the
[compatibility guide](compatibility.md) for exact suppliers and useful setting choices.

Independent parsed XML, inherited XML reuse, ordered read-ahead, parsed-language
persistence, faithful DDS recaching and managed first-build acceleration are retired.
The game performs that work natively. On-demand textures/graphics/audio, progressive
detail and the companion app are deferred; stale development settings cannot enable
them. Preserved research source is not evidence of an active feature. Processed XML,
translation application and warm PNG reuse remain separate retained features.

## Expectations and limits

**Show Wake-Up loading display** and **Record loading display diagnostics** both
default off. The panel shows native stages, elapsed time and bounded feature
status. It formats updates up to four times per second; native synchronous work
can hold the last visible frame. It is not a full per-mod profiler. Loading
Progress keeps its own screen when enabled.

- **Allow loading to finish while unfocused** covers supported save, world, new-colony and
  later native map loading. It restores your current background preference and
  preserves native pause behavior. It defaults off. Startup, direct synchronous
  map/pocket generation and unknown mod-created queues remain native. Loading
  Progress cooperation requires its specified renderer-repaint option to be off;
  each operation still has its own checks.
- **Hide native loading mod summary**, off by default, hides the expansion/mod
  list on supported loading screens. Native loading text, tips and error handling
  remain; mod information stays in Mods. This is independent of Wake-Up's panel
  and is not a measured startup improvement. Loading Progress keeps its own screen.
  No Modlist on Loading keeps its official-content panel unless **Hide summary
  with No Modlist on Loading** is explicitly selected.
- **Record per-mod XML loading timings**, off by default, records up to 512 calls
  after Wake-Up initializes. A separate bounded report now observes actual mod
  constructors from the first constructor and saves `WakeUp/early-loading.txt`.
  View the XML report from Wake-Up settings; a copy is
  saved to `WakeUp/loading-timings.txt` in the selected game data folder. It
  includes mod identifiers, XML file read/parse work and patch-object construction.
  Nested/overlapping times must not be added as startup time. Textures, audio,
  patch application and many other stages are absent from that XML report.

**Reuse completed XML patches on later launches** saves the completed changes that
patches make to definition files. It defaults off, retains required callbacks and
native definition creation, and uses ordinary loading for unsupported work. Source
parsing and inheritance stay native. Historical processed-only gains were small and
workload-specific; no new standalone benefit claim for this option follows from
the 0.4.0 supplier comparisons.

**Extend live XML query reuse** also defaults off. It reuses compiled query instructions
and completed single-node queries during patching. XML changes discard cached
query results and misses. Ordinary lazy multi-node queries keep native evaluation;
the exact XML Extensions integration separately supports its eagerly evaluated
queries while preserving node identity, order and mutation behavior.
This can avoid repeated work on a cold launch as well as uncached patch regions.

**Shared cache size** starts at 1 GiB, with 1/2/4 GiB choices. Texture, atlas and
processed XML output share the allowance. Older retired cache files still count;
disabling a feature does not delete them. Explicit next-launch clearing still clears
the old parsed XML, inheritance and parsed-language stores without activating them.

Restart after changing choices. The existing display diagnostics also show
streaming XML, routing and prepared-texture status; per-mod
attribution appears only with the timing choice. A selected feature may refuse
or have no eligible work; its status distinguishes those conditions. This candidate does not replace the remaining XML/texture/profiler
capabilities of other mods. See [coverage and live limits](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r4c-remaining-coverage.md).

Improvement depends on which work your modlist performs. Cached XML can already skip the searches Wake-Up accelerates, and DDS-heavy lists may do no PNG processing. Separate feature percentages cannot be added. The older core's [Steam comparison](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/steam-qualification-comparisons.md) used four measured repeats per arm on one French, 99-package mixed workload with fresh application caches and unmanaged Windows file caches. Mean menu times were 153.600 seconds absent, 68.258 for public 0.2.1, 68.634 for new defaults and 67.164 with translation/routing enabled. New defaults were 0.55% slower than public; the optional bundle was 1.60% shorter. These are historical menu results for the older core, not a prediction for this new package, universal average or colony-entry timing.

Successful startup and focused new-colony/save-reload checks are different levels
of evidence. Neither establishes every mod combination, long-running colony
behavior, all graphics settings or every game update. The earlier 0.4.0 combined Windows
collection passed those focused checks; its [release notes](release-notes-0.4.0.md)
identify the tested binary, suppliers and limits. No Workshop publication follows
from these instructions. Source licensing is defined in [LICENSE.md](../LICENSE.md).

## Experimental options

**Stream XML input (experimental)** defaults off. It feeds supported XML files into the parser in small pieces, keeping
the game's file order and existing reading workers. It can reduce temporary text
allocations; it is not an XML cache and has no measured game startup benefit yet.
The settings status reports selection, active work, refusal and completed counts.
Supplier hooks or unsupported input keep ordinary loading; a supplier cache that
skips XML reading leaves no work for this option. Restart after changing it.
See [R4a details](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r4a-ordered-reads-types.md).

On-demand audio/texture/graphic and progressive-detail experiments are inactive in
this release. Their previous instructions and captures remain historical research.

## Cache maintenance

Three buttons in Wake-Up settings queue an action for the **next ordinary launch** and then return to your normal settings. You can cancel the queued action before restarting. Explicit development launches leave queued user actions for the next ordinary launch.

- **Bypass once:** skip reading and writing Wake-Up caches, preserving existing files.
- **Rebuild next launch:** use ordinary PNG/JPEG processing and save successful replacement entries for enabled caches. A failed replacement keeps the previous valid file.
- **Clear next launch:** remove owned cache data, even with caching disabled, and do not rebuild it during that launch.

None of these actions enables caching. All new texture options remain off by
default. Sources and other mods' caches are unaffected. Old individual PNG/native
prepared formats are retired when the shared grouped store opens, so an enabled
launch may need to rebuild them. Unused entries are reclaimed within the shared
budget. Storage failures leave ordinary loading available. Clearing prepared
storage also clears automatic native entries and explicit DDS exports because
they share this owned category.

## Code searches

The existing **Faster code type searches** option also skips some repeated searches for classes with no further descendants (leaf subclasses). It preserves the game's result order and mutable lists. This part operates only while constructing the colony's alert interface, including colony entry after the menu, and releases its temporary data immediately afterward. Existing name searches still stop at the menu. Unsupported conditions retain ordinary searches. There is no additional setting or dependency. An isolated speed benefit has not been measured. The current combined GOG colony-entry check and older focused evidence have bounded scope; neither is universal gameplay compatibility.


## Remember successful asset routes

**Remember successful asset routes** defaults off. Enable it in Wake-Up settings
and restart to remember which provider supplied a repeated texture, audio clip,
text file or bundled shader request. Each request still obtains the current
native object; changed providers and unloaded bundles invalidate remembered
routes. Missing assets retain ordinary diagnostics and retry behavior.

This changes lookup work, not loading readiness, image quality or audio playback.
Direct provider and folder queries retain native results and order. Unsupported
lookup hooks use ordinary loading. Audio uses its native loading points. The [C09 record](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c09-broad-routes.md)
separates current functional coverage from pending loading-benefit and broader
gameplay qualification. No supplier-removal claim follows from this option alone.

## Optional prepared textures

Open Wake-Up's mod settings at the main menu and choose **Prepare textures**.
The combined source supports multiple active mods or all active mods, with optional
image/folder paths separated by semicolons. Scan shows candidates and skip reasons
incrementally. Prepare/resume rescans and processes one image at a time. Cancel
stops between scan steps or cleans up the current helper; completed output remains
reusable. One mod's native discovery or one native capture can briefly hold a
frame. The C05 functional record exercises the underlying batch and ordinary startup consumers; it does not claim an interactive window walkthrough.

**Prepare full-size output for Wake-Up** keeps native PNG/JPEG output and
texture properties. If PSD support is selected for this launch, PSD uses its
separate bundled decoding contract. Matching completed entries are reused
without a second copy. Authored DDS is skipped in this mode because it already loads
natively; explicit quality conversion and DDS export still admit suitable DDS inputs. Final size and available storage can still refuse an item.

The optional Windows helper adds explicit **full-size, half-size and quarter-size
DDS exports** for selected PNG/JPEG and supported DDS inputs. Every choice can
change pixels, including full-size recompression. Select ordinary color artwork
only; masks, normal/data maps and shared material/atlas inputs need separate
review. The window requires acknowledging this choice. It never automatically
replaces game textures or original mod files.

Exports are actual `.dds` files under
`<save-data>/WakeUp/PreparedTextures/v1/exports`, with a text `.manifest` naming
the source logical path and output filename. DDS tools can read them directly;
a package author must map the source path to its DDS destination and review the
consuming mod before using them. They are not a ready-to-install mod pack.
If the helper is missing or unsupported, native preservation remains available.

Enable **Use prepared textures** for the next ordinary launch; it defaults off.
Cancel stops new work; resume rescans and reuses only fresh completed native
output. Clear removes only
Wake-Up's prepared category. Preparation takes time and native work may briefly hold a frame. Loading benefits
depend on eligible content: the measured one-JPEG comparison showed no menu
benefit, which does not settle larger PNG/JPEG workloads.

Native entries, DDS exports, automatic PNG/JPEG entries, language and XML stores share one
configurable allowance, initially 1 GiB. Clear prepared output also clears exports, so copy any exports you want
to keep elsewhere before clearing. Native/new-PSD entries are bounded below 96 MiB including metadata; explicit
DDS exports retain their separate 8 MiB limit. Source snapshots are at most
96 MiB for full-size preparation (16 MiB for exports), with base decoded images
at most 64 MiB. Engine working memory is additional.

Earlier native preparation has bounded [live evidence](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/overnight-qualification.md);
the C05 record adds real batch and ordinary startup-consumer evidence. This does not constitute a new interactive-window or helper-quality qualification. A Windows-helper
bundle includes its actual helper executable, matching source and notices; package contents determine whether that optional helper is included. Linux helper packaging is deferred. The
[R2 report](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r2-texture-preparation.md) details alpha, standard
color-space, generated mip levels, sampling/ownership limits and later live checks.
Prepatcher remains required; general on-demand texture loading is not implemented.
