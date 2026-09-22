# Optional texture helper

This separate CPU program prepares one explicitly selected image. It never finds
mods, opens paths, edits originals, writes the cache, downloads dependencies, or
creates GPU devices. The game remains responsible for admission, source identity,
settings, fresh selection checks and atomic publication. Missing/failed helpers
leave original loading available.

## Build and package

Run `python scripts/build_texture_helper.py --fetch` from an existing Windows x64
Visual Studio C++/CMake environment or an existing Linux x64 C++/CMake environment.
`--fetch` explicitly permits the developer build to download the archives locked
by SHA-256 in `dependencies.json`; subsequent builds omit it. Nothing installs
system tooling. Sources, builds, local dependency installs and package output go
under ignored `artifacts/s10-helper`. Compilation has one worker. Required source
notices are copied unchanged into the local package. Exact runtime imports,
binary/source hashes and handshake results are in `helper-build.json`.
The Windows compiler configuration uses the x64 SSE2 baseline; no AVX/AVX2,
OpenMP or GPU compilation option is enabled. Runtime CPU coverage still requires
qualification on the intended machines.

Windows uses pinned DirectXTex and DirectXMath, the installed Windows SDK, a
statically linked MSVC C/C++ runtime and the OS Windows Imaging Component (WIC)
through COM. The Visual Studio license's redistribution rights and conditions
apply to its runtime; DirectXTex's MIT notice does not relicense the Microsoft
runtime or Windows. No Windows system DLLs are copied. The Windows SDK/Visual
Studio version is part of each build's evidence, not pinned dependency source.

Linux build inputs also pin DirectX-Headers, libpng, zlib and libjpeg-turbo.
Their image libraries are linked statically; system C/C++ runtime imports and
the minimum glibc version still require inspection on the actual Linux build.
This software is based in part on the work of the Independent JPEG Group.
The library source is unmodified. Linux decoder code/build configuration has
not been compiled or qualified on the Windows-only development host; no Linux
binary is supplied by a Windows build. Installing WSL or another build host
requires a separate owner action. No Linux runtime/rendering support claim is
made from source availability.

## Isolated GOG raw-pixel trial protocol v1

The Windows-only `--stdio-raw-v1` entry point is an explicitly activated fixture
experiment. It returns decoded base pixels for direct game-thread upload and
native finalization. It does not perform quality resizing, profile normalization,
mip generation or compression. It does not change the v2/v3 quality/export
protocols or authorize automatic helper execution for installed users.

Before its handshake, this mode establishes a self-only Windows Job Object
process-memory cap of **96 MiB**. It writes exactly 16 ASCII bytes
`WUTXRAWHELP00001`, then the cap as a little-endian uint32, and flushes. Failure
to establish the cap terminates without a handshake. The parent must reserve
this entire child allowance, including idle retained codec memory, inside its
existing aggregate queue budget; it is not a separate free allowance. Parent
encoded snapshots, response buffers and current upload buffers also need their
own reservation. This mode cannot use the legacy 256 MiB cap.

One owned hidden process handles requests serially until EOF between requests.
Each request contains 8 ASCII bytes `WUTXRAWQ`, then five little-endian uint32
fields: correlation id, source width, source height, source kind (1 PNG, 2 JPEG),
and encoded length. Exactly that many immutable source bytes follow. There are
no paths, output files, filename discovery or per-image child processes.

The response contains 8 ASCII bytes `WUTXRAWR`, then six little-endian uint32
fields: correlation id, status (0 success, 1 unsupported, 2 failure), width,
height, format (4 = RGBA32), and payload length. Success contains exactly
`width * height * 4` straight RGBA8 bytes, **bottom row first**, with no padding,
header or smaller mip levels. Hidden RGB under transparent alpha is not
intentionally normalized. WIC decodes stored orientation without EXIF rotation
or color-profile transformation. Both source signature and actual WIC container
must match the declared family; decoded dimensions must match the request.
Actual agreement with the game's decoder still requires native comparison.

Input is bounded to 1 through 16 MiB; each dimension is 1 through 8192 and the
RGBA base payload is at most 16 MiB. Non-power-of-two dimensions are allowed.
Larger sources keep the broader managed/native fallback and are not removed
from C06 scope. All per-request metadata and WIC objects are reset or released
before another request. Output rows are written in reverse order directly from
the decoded image, avoiding a second child pixel image.

A fully framed, bounded request whose dimensions, kind or image cannot be
decoded returns its correlation id and status 1 or 2 with zero dimensions,
format and payload length. The next request may proceed. Bad magic, invalid
encoded length, truncated headers/bodies or output-pipe failure terminates the
process with nonzero exit; it never scans for another frame. EOF at a request
boundary exits zero. The parent owns response correlation/layout checks,
deadlines, stderr draining, and cancellation by killing and joining only this
child. Native suspension waits for active image requests to finish; an idle
process is distinct from active image work and still consumes its reservation.

This entry point deliberately does not reuse the v2/v3 metadata/quality policy.
The parent's strict source inspection, PNG CRC/Adler/final-block checks, family
qualification, identity revalidation and ordinary fallback remain required.
WIC's acceptance of malformed files or its PNG/JPEG pixel rounding must not be
treated as equivalent to native merely because a request succeeds.

Run `python native/texture-helper/test_raw_helper.py --helper <built executable>`
for independent raw framing, bounds, reuse, orientation and basic pixel checks.
These offline checks do not establish game base/mip/descriptor equivalence,
general JPEG fidelity, malformed-input equivalence or a loading speed benefit.

## Protocol v2

The sole constant argument is `--stdio-v2`. The helper writes the 16 ASCII bytes
`WUTXHELPER000002`, then the 40 ASCII bytes
`srgb-color-box-straight-alpha-bc1-bc3-v2`, and flushes. The parent must validate these before submitting
work. It then sends this single binary request, all integers unsigned 32-bit
little endian:

| Field | Value |
|---|---|
| Magic | 8 ASCII bytes `WUTXREQ2` |
| Preset | 1 = full size, 2 = half dimensions, 3 = quarter dimensions |
| Output mode | 0 = bottom-row-first raw Unity representation; 1 = top-row-first reusable DDS file |
| Source width, height | Actual image dimensions |
| Encoded length | 1 through 16 MiB |
| Encoded bytes | Exactly that many PNG, baseline JPEG or supported DDS bytes |

The parent closes stdin after the image. Extra bytes are refused. There is no
filename encoding, command string, path escaping, directory scan or parser
dependency. This is a narrow binary refinement of the decision's proposed JSON
schema. Content/options/helper/platform digests and job correlation are owned by
the parent, which owns exactly one child/pipe pair per item; the helper never
publishes from declarations or accepts a writable output path.

The response is 8 ASCII bytes `WUTXRES2`, then six uint32 values: status (0 success,
1 unsupported, 2 failure), width, height, Unity format (10 = DXT1/BC1, 12 = DXT5/BC3),
mip count, raw byte count. Mode 0 returns raw bytes in largest-to-smallest mip order. Mode 1 returns a complete legacy DXT1/DXT5 DDS container, including its header; dimensions/format/mips in that header must match the response. DDS input is permitted only in mode 1. An
error has zero output fields and no payload. Exit code equals status. A valid
success needs complete matching framing, no trailing bytes and exit zero. The
parent independently calculates every mip offset/length and hashes the payload.
Fixed stage names or a short failure reason go to stderr; stdout contains only
the binary protocol. Cancellation stops scheduling and kills/joins only this
owned child if it is still running; this version does not claim cooperative
mid-codec interruption.

## Protocol v3: integrated explicit quality

The additional `--stdio-v3` argument serves the in-game and standalone user
preparation windows through the same engine-independent managed client. V2
export framing and filtering remain unchanged. V3 writes the 16 bytes
`WUTXHELPER000003` followed by `srgb-color-alpha-weighted-bc-mask-rgba-v3`.
The request uses `WUTXREQ3`, preset, output mode (must be zero), role, source
width, source height, encoded length and encoded bytes. All integers remain
little-endian uint32. Role 1 means verified straight-alpha sRGB color; role 2
means a verified paired mask. Other roles are refused. The response uses
`WUTXRES3` with the same descriptor fields as v2. DDS export is not admitted.
Strict single-surface BC1/BC3/BC7 DDS input uses the existing validated decoder
after consumer role admission; cube/array/premultiplied and unsupported DDS
metadata remain refused. It flips decoded pixels into bottom-first upload order.
This is explicit quality conversion of authored DDS, not native DDS preservation.
Full-size mask values are exact relative to CPU decompression; GPU-decoder
agreement requires the separate game rendering check.

Color output uses lossy BC1/BC3. During resizing and mip generation, RGB is
averaged in linear light weighted by alpha, then converted back to straight
sRGB. Fully transparent samples cannot bleed hidden RGB into visible edges.
Mask output uses uncompressed RGBA32 (Unity format 4), and each encoded channel
is averaged independently without sRGB linearization or alpha weighting. A
full-size mask's base level exactly preserves decoded RGBA values (with the
required bottom-first row order); reduced dimensions and generated mips are
explicit changes. Upload retains the verified game's native sRGB interpretation.
The role is supplied by verified game consumers/metadata, never a filename or
the helper itself. Shader rendering and visible quality still need game checks.

The same input, decoded-work, child memory, output-size, single-thread and
power-of-two raw-mode limits apply. Masks reserve their exact full RGBA mip
layout before decoding; they do not borrow the compressed color allowance.
The Windows client pins the executable against replacement during the child
lifetime, validates its complete hash/contract and response layout, and starts
it hidden only for explicit preparation. Cancellation/deadline kills and joins
that owned child. Ordinary installed-user game loading never starts this helper;
the separately activated isolated GOG raw trial is described above.

## Shared resource and image bounds

The helper admits at most 16 MiB encoded input and 64 MiB RGBA decoded input.
Raw Unity mode requires power-of-two dimensions. DDS export accepts other dimensions up to 8192 on each axis; output dimensions must each be at least four. Half/quarter choices use integer division (round down) and edge pixels outside a resulting 2-by-2 sample are omitted. The explicit quality choice includes this change.
It admits bounded decoding when the smaller possible BC1 layout (plus a 128-byte
DDS header for export) fits 8 MiB minus 64 KiB reserved for the parent's
manifest/envelope. After decoding, actual opacity selects BC1 or BC3 and its
exact complete layout must fit before allocating the output mip chain. A small
compressed output never permits exceeding the independent decoded-work bound. Oversized images
are refused; no implicit resize or discarded mip occurs. Output includes every
mip through 1x1. At most one item and one encoder thread run.

A self-only Windows Job Object process-memory limit (256 MiB) is required before
handshake. Linux uses a 256 MiB address-space limit; this is source-only until a
Linux host qualifies it. Failure to establish the bound refuses work. Codec or
allocation failure also refuses work. This bounds the entire child independently
of output size; its semantics differ across OSes. The parent separately bounds
the source snapshot and raw response/staging.

PNG admission permits only noninterlaced 8-bit gray, gray-alpha, RGB or RGBA.
Palettes, transparency chunks, ICC/chromaticity/EXIF, animation and unqualified
metadata are refused. sRGB intent and the usual gAMA 45455 are permitted.
JPEG admission permits 8-bit baseline one/three-component images; EXIF, ICC,
Adobe and other APP1-APP15 metadata are refused. Unknown source kinds and unqualified image metadata remain refused. For reusable DDS export, the user explicitly selects ordinary sRGB color interpretation. No filename or directory proves that an image is ordinary color: masks, shader data, normal maps, premultiplied alpha, atlas participants and color/mask groups need consumer-specific handling and must not be blindly replaced. Export files are not automatically inserted into the game.

WIC produces straight RGBA; the Linux paths specify RGBA to libpng/libjpeg-turbo.
Mode 0 flips decoded rows before compression so every mip, including 2x2
and 1x1, is bottom-row-first for Unity upload. Mode 1 preserves top-row-first
DDS file orientation. The current public quality workflow uses mode 1 only. Smaller images and subsequent
mips use a box average in linear-light RGB, separately averaged straight alpha,
then sRGB RGB output. Entirely opaque source alpha selects BC1; any transparency
selects BC3. Both are lossy. Sampler/readability/role restoration is the parent's
native contract; helper bytes alone are not evidence of Unity rendering fidelity
or startup speed. Native preservation uses the separate guarded S07 game path.

Run `python native/texture-helper/test_helper.py --helper <built executable>` for
the focused independent block decoder/orientation/alpha/mip and rejection cases.
These are offline image checks, not a substitute for separately authorized live
UI, texture rendering, sampler or native capture qualification.

## DDS export input and reuse

DDS input accepts only one ordinary 2D surface in legacy DXT1/DXT5 or DX10
BC1/BC3/BC7 UNORM/sRGB. Cube, volume, array, typeless, unsupported format,
custom or premultiplied alpha and truncated/trailing payloads are refused before
codec work. Dimensions and exact block/mip lengths are independently checked.
The helper decodes only the top level and regenerates a complete mip chain.
Existing authored mip filtering is therefore deliberately changed by a quality
preset. DDS UNORM does not prove ordinary-color intent; the explicit export
choice interprets RGB as sRGB and alpha as straight alpha. BC7 inputs can become
BC1/BC3; this is lossy conversion, not preservation. No DDS input is accepted
for raw Unity mode because the native consumer contract differs.

The managed reader validates the returned container again. Stored exported
representations are keyed by source/provider/order, preset and exact helper
binary identity, and remain separate from native-output cache admission. A DDS
file is reusable by a DDS-aware consumer or modding workflow; writing an export
does not enable it in RimWorld, edit a mod, qualify GPU rendering, or prove a
loading saving. Keep authored originals and use native preservation as the
fidelity alternative. Linux helper work remains deferred.
