# C10 texture design: exact-player and ownership checks

## Steward review and return to audio implementation — 12 September UTC

Clean stopped handoff `a01a5846` passed review as a bounded source investigation.
The steward verified all 18 artifact hashes, both pinned native inputs, seven
fixture hashes and nine absences. Independent disassembly review confirmed the
renderer/TLS, base-subresource update and native finalization dependencies. No
texture implementation, new run or performance result is accepted.

A valid engine-owned producer need not run on the requesting worker itself: it
could service that worker independently of the blocked main thread, provided its
queue, lifetime and native finalization contract are established. No such route
is established here. This clarification neither authorizes new native interception
nor calls for another open-ended reverse-engineering pass.

The same C10 owner now resumes retained audio implementation from the exact new
steward review base. Target MP3 work genuinely avoided beyond frame-index reuse,
with the actual selected native decoder retained, and real audio first-use and
prewarming behavior. Existing OGG/WAV/index correctness evidence stays attributed
to its original sources. Texture/graphic/icon deferral stays an unresolved current
release requirement; audio progress cannot close it. No C11 successor, production
backend adoption or performance permission follows. Source-only texture research
remains authorized, but this assignment prioritizes the next working audio increment.

## Result - 12 September UTC

The engine investigation now identifies actual native functions and their
dependencies, rather than stopping at a managed API's missing thread-safety
annotation. Unity can queue a plugin's texture update through its own renderer,
but the inspected submission path also updates scene renderer state and uses a
graphics device assigned to the calling thread. A normal worker cannot simply
be given a command buffer and assumed to own that machinery. No complete
worker-first-exposure implementation is established by this source review.

The approved alternative of eager public objects plus private deferred work
remains valid without a compatibility concession. However, the inspected
collection graphics, materials, icons and atlases have no new useful private
residual workload: their eager work creates objects already exposed through
public fields/getters. The other inspected private caches are already lazy.
This finding is about those concrete families, not all possible ownership designs.

No product code, build, deployment or live run changed. Texture avoided-work
counts remain unestablished. Full 5b and all retained audio requirements remain
open. No production integration, new interception or compatibility change is
proposed as executable or adopted.

## Exact-player route and evidence

The changed premise was to inspect Unity's own custom texture-update mechanism,
which may provide ordered upload without direct use of Unity's D3D context.
The exact CoreModule contains `CommandBuffer.IssuePluginCustomTextureUpdateV2`
and `Graphics.ExecuteCommandBuffer`. The former records a texture update callback;
Unity's upstream header supplies begin/end events and a plugin-provided source
pointer. Its contract differs from creating a separate GPU texture.
[Unity update API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rendering.CommandBuffer.IssuePluginCustomTextureUpdateV2.html),
[fixed upstream header](https://github.com/Unity-Technologies/com.unity.webrtc/blob/ada526af1ae65ff418669caef59ccb8575beab20/Plugin~/unity/include/IUnityRenderingExtensions.h).

The player's CodeView record identifies
`UnityPlayer_Win64_player_mono_x64.pdb`, GUID
`ff67de78-9de3-4c50-a915-72eed5f70d3b`, age 1. The matching compressed file was
retrieved from Unity's official symbol server and expanded locally; its internal
GUID/age match. Expanded size is 20,975,616 bytes, SHA-256
`a5f5cd4119848269233679149559441f4b8daccfd4fa1b6a84ac0ca1d4d22a9d`.
The player itself remains
`e0c489f1683609247fede45ea049d30baa4f4542060e308e25c0ec87f6c0fb96`.
Only its on-disk bytes were examined; no target code was loaded or executed.

Public symbols and bounded x64 disassembly establish the following. Addresses
are relative virtual addresses (RVA): offsets from this exact module's loaded
base, not callable addresses or an approved interface for another player.

| Exact function / RVA | Verified behavior and implication |
| --- | --- |
| `CommandBuffer_CUSTOM_IssuePluginCustomTextureUpdateInternal`, `0x139470` | Appends command `0x46` and a 24-byte payload through `GrowableBuffer::GetWriteDataPointer`. Recording is separate from submission. |
| `Graphics_CUSTOM_ExecuteCommandBuffer`, `0x8a010`, calls body `0x4a6380` | The body updates renderers, constructs a render-node queue, prepares state, executes the buffer, cleans up and synchronizes jobs. It is not merely a worker-safe enqueue. |
| `GetGfxDevice`, `0x70b5a0` | Reads thread-local storage using `TlsGetValue`; its missing-device branch logs `Graphics device is null.`. This is a positive per-thread engine dependency. No ordinary-worker registration contract was established. |
| `Texture2D_CUSTOM_ApplyImpl`, `0xdd000`, calls `Texture2D::Apply`, `0x49aae0` | The original path mutates native finalization state and dispatches the texture's virtual update operation. Bypassing a C# wrapper does not remove native state/lifetime obligations. |
| `Texture2D::UpdateImageData`, `0x497590`; `UpdateImageDataDontTouchMipmap`, `0x497610`; `UploadTexture`, `0x495f00` | The update operations lead to native upload, `GetGfxDevice`, `UploadTexture2DData` and shared texture-storage lifetime management. A borrowed CPU view alone is insufficient. |
| `GfxDeviceD3D11Base::InsertPluginTextureUpdateCallback`, `0x919930` | Calls the V2 begin event, then `UpdateSubresource` at `0x919a64` with destination subresource 0, a null region and pitches derived from width/height/bytes per pixel, then the end event. There is no mip loop or original Texture2D CPU/readability finalization in this handler. It supplies a base-subresource update, not full authored-mip finalization. |
| `GfxDeviceClient::InsertPluginTextureUpdateCallback`, `0x9f0ac0` | In threaded mode writes command `0x27b5`, callback and internal parameters to that client's `ThreadedStreamBuffer`, commits its cursor and may signal the consumer. This establishes an existing ordered producer stream, not arbitrary multi-worker ownership of it. |

Native readiness requires correct upload ordering and storage lifetime equivalent
to ordinary publication. This investigation adds **no requirement to wait for
physical GPU completion** where the native path only queues ordered work. The
existing wrapper/main-thread and CPU/GPU findings remain valid, but missing
`IsThreadSafe` metadata alone is not the reason to reject worker execution.
The custom-update route's precise mip limitation is now a verified binary fact,
not an inference from the upstream header's missing mip parameter. A single-mip
native-unreadable subset would still need the unresolved demand-thread submission
contract; it is not newly qualified by this narrower possibility.

The concrete unresolved dependency is a supported way to obtain and retire an
engine-owned graphics producer on the demand thread or one that services its
requests independently of a blocked main thread, with the correct ordering
and original Texture2D lifetime/finalization contract, without invoking unrelated
scene mutation from that worker. Neither setting thread-local storage manually,
sharing the main producer pointer nor calling a discovered RVA establishes that
contract. No such integration was executed. Private object writes, raw detours,
global synchronization changes and permanently occupied callbacks remain outside
this investigation.
The PDB names client constructors/destructors and `GfxDeviceWorker::Startup`,
`Shutdown` and queue methods. Those are C++ implementation symbols, not published
plugin APIs or an established safe registration lifecycle. This is the stopping
point for this bounded call-path investigation.

## Already-approved ownership route

Complete public populations, completion before opaque callbacks, privately owned
deferred work and known-worker preparation are already permitted by plan 6.1.
They require no new approval merely because they are used. The earlier problem
was narrower: delaying the very objects that previously successful public or
worker getters can return, when main may be waiting for that worker. Preparing
every provider before an opaque callback preserves behavior, but does not itself
leave texture creation deferred.

The changed check here examined generated outputs after keeping source textures
ready, instead of repeating private filesystem-holder descriptors:

| Candidate work | Actual first exposure and remaining work |
| --- | --- |
| Collection children and materials | `Graphic_Collection.Init` creates children through `GraphicDatabase` and stores them in `subGraphics`. `Graphic_Random.SubGraphicAtIndex` / `FirstSubgraphic` return them directly. `GraphicDatabase.GetInner` returns cache hits before its main-thread miss check, and single/multi graphic material getters return existing material fields. Making only the parent ready would change that existing graph. |
| Definition icons | `BuildableDef.PostLoad` / `ResolveIcon` populate public `graphic`, `uiIcon`, `uiIconMaterial` and `stuffUiIcons`. Their creation cannot remain private after those callbacks return. |
| Atlas output | `StaticTextureAtlas.Bake` completes packing, color/mask texture work and meshes before registration. `TryGetStaticTile` exposes a tile's `atlas`, `mesh` and `uvRect`; the atlas exposes color/mask textures. Delaying these outputs repeats public readiness, even if original source textures stay eager. |
| Shadow graphics, shadowless copies, replacement information and custom mesh caches | The inspected private caches already initialize on demand. They supply no new avoided eager work. |

Ordinary cultivated-plant `Graphic_Random` definitions reach the same collection
graph; no package-specific exemption changes the public exposure. This source
check does not claim supplier runtime failure. It does not forbid discovering a
different owned eager workload later. It does establish why another implementation
of these inspected candidates would not deliver the requested new broad deferral.

## Next implementation decision and handoff

Do not repeat CPU-only, separate-device or incomplete public-holder proofs.
The native integration prerequisite is now specific enough for a bounded design
review: a registered engine graphics producer with original-texture finalization,
not an assumed command-buffer worker call. If that ownership contract cannot be
established, no worker execution proposal is justified. Further texture work must
either supply it or identify a different real private workload; source inspection
itself remains authorized, and no compatibility reduction follows automatically.

For immediate product progress, return to retained C10 audio work while the
steward resolves whether further native integration work is warranted. MP3
first-use/decoded-data/provider qualification, sound readiness, broader consumers
and prewarming remain current release requirements. That ordering recommendation
does not close or postpone texture coverage beyond release.

Assignment `wake-up-c10-texture-design-4ab77eb2-20260912` started at exact clean
`4ab77eb2e2dc78e869d097debb14bfcbe6cff3a1` on the single preserved review checkout.
Fresh baseline hashes/absences, exact sources/symbols and disassembly are retained
under `artifacts/ecosystem-next-20260910/c10/texture-design/`. The fixture was not
modified; there are no new package/run identities or restoration transactions.
Independent child work stops before the clean committed handoff and ownership
return. Full C10, production integration and performance remain unaccepted.
