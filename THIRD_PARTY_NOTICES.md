# Attribution and third-party material

This file distinguishes dependencies and research references from incorporated
implementation. Review date: 2026-09-08. Project licenses cover only material
the project has authority to license.

## Runtime dependencies and interoperability

The optional S10 texture-preparation package additionally contains a separate
Wake-Up CPU helper linked with pinned DirectXTex (MIT) and DirectXMath headers
(MIT). Exact sources, archive hashes, build inputs and complete notices are in
[native/texture-helper](native/texture-helper/README.md) and its `notices` folder.
Windows uses the installed Windows SDK and statically linked MSVC runtime under
their own redistribution terms, plus OS Windows Imaging Component decoding;
no Windows DLL is copied. Linux inputs additionally include DirectX-Headers,
libpng, zlib and libjpeg-turbo, with their complete notices retained, but no Linux
helper binary has been built on this host. This software is based in part on the
work of the Independent JPEG Group. These dependencies do not become mandatory
for core loading or native-output preservation.

| Component | Use | Distribution |
|---|---|---|
| RimWorld, Ludeon Studios | Game interfaces, narrow loading compatibility glue and behavior reference | Not bundled; obtain a licensed game; NOTICE contains the required disclaimer |
| Unity, Unity Technologies | Game-supplied graphics and runtime interfaces | Not bundled |
| [Harmony](https://github.com/pardeike/Harmony), Andreas Pardeike and contributors | Runtime patching and type-search interoperability; MIT | External dependency, not bundled |
| [Prepatcher](https://github.com/Zetrith/Prepatcher), Zetrith and contributors | Supplies the reviewed bootstrap/Harmony environment; MIT | External dependency, not bundled |
| [MissileGirl/Gagarin](https://github.com/ViralReaction/MissileGirl) | Optional parsed-XML reuse around the installed supplier's original methods | Not bundled; no imported supplier source file |
| Character Editor, VOID | Optional preset lookup reuse around original supplier calls | Not bundled; no imported supplier source file |
| [Loading Progress](https://github.com/ilyvion/loading-progress), ilyvion and contributors | Optional repaint adjustment; original iterator and time budget remain supplier-owned | Not bundled; no imported supplier source file |
| Giddy-Up, its original authors and maintainers | Optional texture readback used by the supplier's unchanged offset calculation | Not bundled; no imported supplier source file |

Harmony's and Prepatcher's upstream MIT texts are linked at
https://github.com/pardeike/Harmony/blob/master/LICENSE and
https://github.com/Zetrith/Prepatcher/blob/master/LICENSE. No copies of their
binaries or source files are shipped, so these are dependency acknowledgments,
not a claim that their implementation has become project-owned.

## Source provenance

The retained source consists of project implementation and narrow interoperability
code. No vendored third-party implementation files or libraries were identified
in the current source tree. Compatibility hashes and method/type names identify
external code; they are not copies of that code. Runtime reverse patches obtain
original methods from the user's installed game, not an embedded game-code payload.

`Textures/PngRuntime.cs` includes game-behavior-aligned file selection and texture
loading glue. Its game API names, ordering and compatibility behavior derive from
inspection of the installed game. The Ludeon notice applies; project licensing
does not purport to relicense Ludeon's underlying material. Other supplier adapters
preserve and call installed original implementations instead of shipping them.

Earlier research examined DefLoadCache, Faster Game Loading - Continued,
Hyperdrive, FastLoader and other loading tools. Research or an acknowledgment
does not grant a right to import their implementations. None is bundled in this
release. Decompiled research and superseded experiments are excluded from the
initial public Git history and source distribution.

## Build and test dependencies

S11 uses [Krafs.Publicizer](https://github.com/krafs/Publicizer) 2.3.0 (MIT),
package source revision `91f9f5ad6589da81dd240075186d1c048a53929c`, as a pinned
build-only dependency. It exposes the embedded Cecil types in a compiler
reference copy of Harmony; no modified Harmony or Publicizer DLL is bundled.
Its generated `IgnoresAccessChecksToAttribute` is compiled into Wake-Up.
Copyright (c) 2021 Krafs; package metadata also states copyright Krafs 2024.
The full [MIT notice](build/notices/Publicizer-LICENSE.txt) accompanies source.
S11's Prepatcher API reference remains external and unbundled.

.NET SDK, .NET Framework reference assemblies, Microsoft.NET.Test.Sdk, NUnit,
NUnit3TestAdapter and NUnit.Analyzers are obtained separately using the pinned
SDK/configuration and NuGet lockfiles. They are build/test inputs, not mod payload.
Their own licenses apply to those separately obtained packages.

## Legal text and assets

LICENSE is the unmodified GNU GPL v3 text from https://www.gnu.org/licenses/gpl-3.0.txt.
LICENSE-DOCS is the CC BY-SA 4.0 English legal text from Creative Commons' own
repository: https://raw.githubusercontent.com/creativecommons/creativecommons.org/main/docroot/legalcode/by-sa_4.0.txt.
DCO is the Developer Certificate of Origin 1.1 from https://developercertificate.org/.
These texts retain their original notices and copying terms.
No preview art, game textures, logos or other third-party artwork is included.

## Bundled optional PSD decoder

Wake-Up compiles a bounded managed adaptation of StbImageSharp 2.30.15's PSD
reader (commit `6fd7aebe1dbf10e28d78745f42a0c095c61d1945`) into `WakeUp.dll`.
No separate runtime installation, download or helper is required. This is new,
optional composited-image decoding rather than native PSD preservation. The
[attribution and license](third-party/StbImageSharp-LICENSE.txt) and
[supported input/output contract](third-party/StbImageSharp-PSD.md) accompany the
release and are also embedded in the DLL.

## Bundled first-build JPEG decoder

The optional first-build image pipeline also compiles a bounded managed JPEG
adaptation from that same StbImageSharp version and commit. Source arithmetic
adjustments reproduce the qualified native grayscale and YCbCr output; the
decoder does not bundle libjpeg code or a native library. The same attribution
and license apply. Its [JPEG contract](third-party/StbImageSharp-JPEG.md) records
the adaptations, output and allocation bounds and accompanies the release and
embedded DLL resources. Users install no additional component.
