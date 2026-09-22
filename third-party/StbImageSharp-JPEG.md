# Bundled JPEG decoding

Wake-Up's first-build JPEG reader creates private CPU pixels using source compiled into `WakeUp.dll`. Users install no additional DLL, framework, helper, or runtime component. It does not execute an external program or download code during game loading. Producing RGB pixels is not itself proof that the game would produce identical pixels: native equality must be established for each admitted image family before this output replaces the ordinary loader.

## Pinned source and attribution

This is a managed source adaptation of **StbImageSharp 2.30.15**, upstream commit **`6fd7aebe1dbf10e28d78745f42a0c095c61d1945`**. The public-domain C# port is by StbImageSharpTeam and derives from Sean Barrett's `stb_image`. The existing `StbImageSharp-LICENSE.txt` supplies the port attribution and original MIT/public-domain terms.

- [Pinned JPEG source](https://github.com/StbSharp/StbImageSharp/blob/6fd7aebe1dbf10e28d78745f42a0c095c61d1945/src/StbImage.Generated.Jpg.cs), SHA-256 `9d2db6f955843f8de3e1eaa0931bdf3a3e1f42f503cd7e570556798670e65251`.
- [Pinned common routines](https://github.com/StbSharp/StbImageSharp/blob/6fd7aebe1dbf10e28d78745f42a0c095c61d1945/src/StbImage.Generated.Common.cs), SHA-256 `b3bfea797bce4b8c565fb8997ebce4122a0b2d0113dd03ecf5286643ef3a0834`.
- [Pinned project metadata](https://github.com/StbSharp/StbImageSharp/blob/6fd7aebe1dbf10e28d78745f42a0c095c61d1945/src/StbImageSharp.csproj) records version 2.30.15.

The adaptation is split into `FirstBuildJpegDecoder.Stb.cs` (entropy decoding, inverse discrete cosine transform, and resampling arithmetic derived from the pinned JPEG routines) and `FirstBuildJpegDecoder.cs` (managed storage, bounded parsing, output, and cancellation). Its explicit identity is `stbimagesharp-2.30.15-6fd7aebe-jpeg-managed-rgb24-v2`. This identity describes newly decoded output; it does not imply Unity-native preservation.

The steward's authorization relay is `c06-jpeg-same-bundled-source-20260911`, reusing the approved pinned source-bundling direction. Any default admission or product choice remains subject to the campaign's separate fidelity and ownership decisions. The product packaging should carry this contract and the existing license; this source-only handoff does not claim that a built package has exercised JPEG decoding.

## Supported encoding and pixel meaning

The reader handles eight-bit Huffman JPEG frames: baseline sequential (SOF0), extended sequential eight-bit (SOF1), and progressive (SOF2), with one, three, or four components. Horizontal and vertical sampling factors from one through four must divide the largest corresponding sampling factor. It supports separate-component scans and restart intervals through the upstream routines. Arithmetic-coded, lossless, hierarchical, twelve-bit, zero-height/DNL-defined-size, unsupported sampling, and over-budget files are refused.

Ordinary first-build loading admits the separately qualified SOF0/SOF2 grayscale
and YCbCr 4:4:4, 4:2:2 and 4:2:0 families. EXIF orientation and ICC metadata retain
the game's unchanged sample interpretation. Component identifiers and metadata
are checked before and after decoding; direct RGB, CMYK/YCCK, other sampling and
unqualified frames use ordinary game loading. Native rejection is preserved.
Admission is based on encoding, never a mod name, filename or fixture identity.
Adobe APP14 uses five identifier bytes (`Adobe`), a two-byte version, two
two-byte flags fields and a transform at payload offset 11. Both early header
observation and later marker decoding accept nonzero version high bytes;
transform-zero direct RGB remains outside production admission.
The production queue retains C05's common `width * height * 4 <= 64 MiB` size
boundary and reserves all JPEG working buffers within its shared 192 MiB total.

Output is RGB24, exactly three bytes per pixel, with the bottom row first. The pinned transform factorization is adapted to 13-bit signed-rounded constants. Chroma interpolation uses alternating rounding biases, and YCbCr conversion uses 16-bit rounded coefficients. These source arithmetic adjustments address demonstrated differences from the game; no libjpeg source or binary is bundled. Coefficient narrowing and transform overflow are refused before publication. JPEG is already a lossy format, so full dimensions or visual similarity alone do not establish native equality. Exact base pixels and all final mip bytes/descriptors remain the admission evidence.

One-component grayscale is copied to all three RGB channels. Three-component images use the upstream RGB-versus-YCbCr interpretation (component identifiers and JFIF/Adobe metadata). Four-component Adobe CMYK/YCCK images use the upstream conversion conventions. EXIF, APP2/ICC, other application markers, sampling factors, component count, progressive encoding, JFIF, and Adobe transform remain visible in result metadata. EXIF orientation is not applied and ICC profiles are not transformed. Application metadata is conservatively flagged even when the reader does not interpret its contents. These families must remain distinct in the caller's fidelity admission.

## Allocation and lifetime bounds

`ReadHeader(Stream, CancellationToken)` preserves stream position and examines marker/frame data without entropy decoding or allocating image-sized arrays. It rejects inputs above 96 MiB, dimensions outside 1–8192, RGB base output above 64 MiB, and estimated working storage above 192 MiB. For each component it computes its dimensions padded to complete minimum coded units (the JPEG blocks implied by subsampling). The reservation is:

`source bytes + RGB base bytes + padded component-plane bytes + progressive coefficient bytes + 1 MiB scratch`

Progressive coefficient storage is two bytes for every padded component-plane sample; sequential decoding needs only a reusable 64-coefficient block. The reservation includes the output while all component buffers are alive. The decoder checks the actual parsed frame against the reservation and charges each large managed allocation before creating it. Row buffers, small Huffman tables, reusable transform/entropy scratch, metadata, and managed object overhead fit within the scratch allowance. The caller must reserve `Header.RequiredBytes` for a worker; reserving source plus output alone is insufficient.

All pointers and native allocations were replaced with value-type managed array slices. Offset arithmetic is checked and every access uses normal managed array bounds checking. No unsafe compilation, pinned buffers, native allocation/free, or native fault handling is involved. Entropy block and transform scratch arrays are reused, avoiding allocation per block. Output is written directly into the single bottom-first array; no second full-image copy or decoder invocation is required.

Cancellation is checked during marker parsing, bounded source reads, every decoded block/minimum coded unit, progressive finalization, and each output row. Each invocation owns its decoder state, buffers, and exceptions. There is no shared failure string to race between workers. Refusal or cancellation publishes nothing; managed private buffers become collectable when the call unwinds.

## Deliberate changes and refusal behavior

The original generated source aliases its JFIF and Adobe marker signatures to one common array. This adaptation distinguishes the actual `JFIF\0` and `Adobe\0` tags. That correction can change a pathological or ambiguous three-component file's color interpretation and must remain part of the explicit decoder contract.

The managed output routine writes only RGB bytes, removing the generated routine's speculative extra alpha-byte write. It writes bottom-first rows directly. Huffman tables, quantization tables, scan-component uniqueness, source lengths, marker/scan counts, component coverage, decoded-block counts, and entropy consumption receive focused additional checks. Marker processing is bounded to 4096 markers and 256 scans. Truncated data, missing tables/components, malformed indexes, early scan termination, and trailing bytes are refused rather than returning a partially initialized image. This is intentionally stricter than upstream's permissive recovery behavior.

This implementation retains the pinned decoding mathematics; it is not a general JPEG compatibility guarantee or a native-equality/performance result. Synthetic unit cases and real packaged native comparisons are separate evidence. No codec timing or performance claim is supplied by this implementation record.
