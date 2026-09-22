# Bundled PSD decoding

Wake-Up includes its optional PSD image reader inside `WakeUp.dll`. Users do not install another library or framework, download a component at runtime, configure a dependency, or run an external helper. This reader creates new pixels from the saved composite image in a PSD; it does not preserve or reproduce Unity's native PSD decoding.

## Source identity and distribution

The reader is a bounded, managed source adaptation of the PSD routines in **StbImageSharp 2.30.15**, upstream commit **`6fd7aebe1dbf10e28d78745f42a0c095c61d1945`**. It is not an unmodified StbImageSharp assembly or NuGet package. Its internal output identity is `stbimagesharp-2.30.15-6fd7aebe-psd-bounded-rgba8-v1`; cached new-decoder output must use this identity and remain distinct from native-output identities.

- [Pinned PSD source](https://github.com/StbSharp/StbImageSharp/blob/6fd7aebe1dbf10e28d78745f42a0c095c61d1945/src/StbImage.Generated.Psd.cs), SHA-256 `7c1cfa629bd180fb2598dfb34e157b7c1abcf457647facb90a5d0c51e5deb451`.
- [Pinned project metadata](https://github.com/StbSharp/StbImageSharp/blob/6fd7aebe1dbf10e28d78745f42a0c095c61d1945/src/StbImageSharp.csproj) identifies version 2.30.15 and StbImageSharpTeam as authors.
- [Pinned README](https://github.com/StbSharp/StbImageSharp/blob/6fd7aebe1dbf10e28d78745f42a0c095c61d1945/README.md) declares the port public domain.
- [Pinned original stb_image.h](https://github.com/StbSharp/StbImageSharp/blob/6fd7aebe1dbf10e28d78745f42a0c095c61d1945/generation/StbImageSharp.Generator/stb_image.h) supplies the original MIT/public-domain terms, reproduced in `StbImageSharp-LICENSE.txt` alongside the port's attribution.

The attribution and this contract are embedded in `WakeUp.dll` as `WakeUp.ThirdParty.StbImageSharp.LICENSE` and `WakeUp.ThirdParty.StbImageSharp.Contract`, so even the fixture's minimal package carries them. The ordinary release package also supplies readable notices. The decoder itself is compiled by the existing product project's source inclusion into the ordinary product DLL; there is no new assembly dependency. A package restore on a development machine does not establish delivery: the packaged GOG mod must exercise the decoder from its deployed DLL before acceptance.

## Supported input and pixel meaning

The reader accepts PSD version 1 (`8BPS`) with RGB color mode 3, 3 to 16 channels, and a saved flattened/composited image. It supports uncompressed planar data and PackBits run-length encoding, at either 8 or 16 bits per channel. It does not render layers, effects, blend modes, masks, text, or vector content. The file must contain the composite the author wants displayed.

The first three planes become red, green and blue. With three planes, alpha is opaque. With four or more, the fourth plane is interpreted as composite alpha following stb's convention; additional planes are validated but ignored. Files that use the fourth plane as a different kind of channel are outside this interpretation. Pixels with partial alpha undergo stb's white-matte removal: each RGB channel becomes `255 + (storedChannel - 255) / alpha`, where alpha is between zero and one. Fully transparent and fully opaque colors remain unchanged. Results outside the byte range are clamped instead of relying on an unchecked conversion. This is an explicit interpretation, not a promise of Photoshop color-managed rendering.

Sixteen-bit samples are big-endian; only their high eight bits are retained before the same alpha treatment. Output is RGBA with eight bits per channel (RGBA8), and rows are reversed to the bottom-first layout used by Unity's raw texture upload. Color profiles and other image resources are ignored; there is no ICC conversion or gamma correction in the decoder. The product integration selects its documented sRGB texture interpretation and creates mip levels separately.

The reader rejects PSB/version 2, grayscale/indexed/CMYK/Lab modes, 1-bit or 32-bit samples, ZIP compression (with or without prediction), malformed section lengths, truncated composites, and malformed PackBits rows. It requires zero reserved header bytes. Refusal must leave the native source path available; it must never publish incomplete decoded output.

## Bounds and changes from upstream

The source must be at most 96 MiB. Each dimension must be between 1 and 8192, and base RGBA8 output must be at most 64 MiB. The header and required encoded-data lengths are validated before pixel allocation. The implementation allocates one output pixel array and decodes directly into it, without a full extra channel or image buffer. The source byte array remains owned by its caller.

The adaptation replaces unsafe allocation and pointer operations with bounded managed arrays. It validates section and composite bounds, respects every declared PackBits row length, and validates ignored extra planes. It explicitly handles 16-bit PackBits byte rows before discarding low sample bytes; the pinned generated upstream routine's RLE branch does not distinguish sample depth. It adds bottom-first output and deterministic clamping for matte removal. These are reviewed adaptation choices, not a claim of byte equivalence with every unsupported or malformed input accepted by upstream.

The owner approved bundled optional PSD support through steward relay `wake-up-c05-psd-approved-bundled-20260911`, with the condition that users install no dependency themselves. The opt-in switch and runtime integration are documented in the C05 handoff and user guide. Performance measurement remains separate from functional correctness.
