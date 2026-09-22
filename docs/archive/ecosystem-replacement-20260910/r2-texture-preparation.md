# R2: useful texture batches and reusable DDS output

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

Wake-Up can now prepare selected images across active mods, keep finished work
across cancellation, and export ordinary color artwork as compressed DDS files.
The native preparation path no longer rejects a potentially small compressed
result merely because an uncompressed version would be large. Original images,
authored DDS files, mod order and the deployed candidate are unchanged.

This is an offline implementation checkpoint, **not full replacement of
RimSort/RimPy/todds or a demonstrated loading improvement**. Native preservation
feeds the existing guarded startup consumer. Quality-changing output is a useful
portable file export, deliberately not an automatic game-texture replacement.
Shader data, masks and shared atlases do not have a sufficiently general consumer
contract for that replacement. No new hardcoded UI-path allowlist was added.

## Public workflow

At an idle main menu, open Wake-Up settings and **Prepare textures**. Select all
active mods or toggle individual mods, then enter optional exact relative files
or folders separated by semicolons. Folder matching respects component boundaries;
overlapping selections do not add duplicate rows. Native load-folder and DDS
precedence remain authoritative, including the native `.jpeg` suffix behavior.
Each provider's own holder remains relevant even when a later mod shadows it.

Scan advances one provider's native discovery at a time and reads at most 16
image headers per update. It shows progress and can stop between those steps.
The native discovery call for one mod remains indivisible. Prepare/resume performs
a fresh incremental scan, then processes one image at a time. Each row records a
candidate, skip, prepared, reused or failed result. The aggregate counts distinguish
scan eligibility from actual publication. No old cursor is proof of completion.

Choose one of these explicit operations:

* **Preserve native output for Wake-Up**: retain the game's actual pixels,
  dimensions, formats, mip levels, sampling properties and final nonreadability.
  Native PNG/JPEG capture uses the existing exact loader/hook/platform checks.
  Authored DDS stays on its already-native route and is reported accordingly.
* **Export DDS, full size / half / quarter**: run the optional Windows x64 CPU
  helper on the selected PNG/JPEG or supported BC1/BC3/BC7 DDS. Width and height
  are divided by one, two or four. This requires a fresh acknowledgement that the
  selection is suitable ordinary color artwork. It can change pixels even at
  full size. It creates actual `.dds` files and mapping manifests, not another
  native cache envelope or a hidden startup option.

Exports live under `<save-data>/WakeUp/PreparedTextures/v1/exports`. Each text
`.manifest` has a version line, encoded source/options identity, **plain source
logical path**, and the digest-named DDS filename. The file is independently
usable by DDS tools; the source path maps it back to its intended artwork.
For use in an independently reviewed mod package, the package author must map
that path to a `.dds` filename and confirm the consumer requirements. Wake-Up
does not copy it into a mod, overwrite a sibling DDS or promise a ready-to-install
mod package. Export ownership, not blind loaded-object substitution, is the
materially different approach from the zero-match historical UI-key attempts.

**Use prepared textures** still defaults off and only consumes native-preserving
entries on a later restart. Export choices are local to the window, default to
native on reopening, and never change this startup selector. Original files stay
intact. Ordinary settings report whether preparation was selected, whether the
guarded loader was reached, actual hits/misses/refusals and the refusal reason.
No helper is required for native preparation or core loading. Linux helper work
remains deferred; Windows export refuses a missing or unsupported helper.

## Storage, memory and ownership

The automatic store remains **512 MiB**. Native prepared entries and DDS exports
**share the existing other 512 MiB**, including mapping files and interrupted or
replacement output. There is no third storage allowance. Native entries include
their manifest and 108-byte envelope in the initial **8 MiB complete-entry** bound;
exports include their DDS header and mapping in that bound. The helper reserves
64 KiB from its output ceiling for parent metadata. Export files remain until
explicit clearing or replacement; capacity refusal explains when clearing is
needed. Native entries retain existing bounded pruning.

One category owner lock protects both prepared representations. Before writes,
capacity accounts for old output plus temporary files. A digest-named DDS is
written and flushed first; its atomic mapping publication is the commit point.
Interrupted pending/orphan output is reclaimed on reopening. Reuse validates the
exact identity, mapping, source digest and complete DDS structure. Central clear
removes native prepared entries and exports, retaining unrelated categories/files.
Bypass/rebuild policies still govern reads and writes; neither enables a feature.

Source, decoding and stored-size bounds are different:

| Resource | Bound or disposition |
|---|---|
| Encoded source snapshot | 16 MiB per item |
| Decoded base RGBA image | 64 MiB, at most 8192 on either axis |
| Native engine peak memory | Unmeasured; mipmaps, decoder workspace, textures and GPU work are additional |
| Helper process | Existing 256 MiB private process limit; one CPU child, no GPU device |
| Final complete stored entry | 8 MiB, with final layout/metadata checks |
| Both owned store budgets combined | At most 1 GiB |

Native scan uses a smallest plausible compressed base-layout check, not a false
full-chain RGBA prediction. Capture remains bounded and checks the actual native
format/layout before copying output and again before publication. Some candidates
therefore legitimately refuse after native processing. DDS export first rejects
even-BC1 full-chain layouts that cannot fit; after bounded decode, actual alpha
determines BC1 versus BC3 size before mip allocation. A small compressed output
never grants arbitrary unbounded decode permission.

Native preparation first checks its content key, then offers promotion of a
matching automatic entry. Successful prepared publication removes **only that
exact automatic key**. Prepared startup hits likewise retire an overlapping
automatic entry after successful restoration; they bypass the automatic lookup,
so there is no second counted cache hit. Failed preparation preserves automatic
fallback. Disabling prepared use can later let automatic caching rebuild its own
entry; enabling prepared use reconciles that overlap again. Unrelated categories
and entries are not invalidated. Quality exports have a separate portable identity
and are never consumed as native engine bytes.

Every item rechecks active provider/order and the exact source contents. It probes
the relevant paths across the current ordered load folders, catching new higher
overrides and DDS siblings without recursively scanning a whole mod per item.
The initial full selection still participates in native identity; unrelated
inventory drift can cause a later conservative miss. Resume rescans fully.

## Quality and consumer limits

DDS is a texture file container. BC1/BC3/BC7 are compressed pixel formats; the
helper reads those supported 2D DDS forms and exports BC1 for opaque artwork or
BC3 for alpha-bearing artwork. Arrays, cubemaps, volumes, unsupported formats and
premultiplied-alpha contracts are refused. Source color values are explicitly
treated as standard display color (sRGB), with straight alpha and linear-light
box filtering. Existing source mip levels are replaced by a full generated chain.
Half/quarter output uses integer dimensions; odd-edge samples may be omitted by
the documented 2-by-2 filter. Full-size recompression need not reduce file size.

A DDS file does not encode Unity sampling state or object ownership. The game
has different anisotropy and upload behavior for native DDS and PNG/JPEG. Masks,
normal/data maps, custom color spaces, coupled color/mask sizes, atlas ownership,
samplers and readability must be assessed by a package author before using an
export in game. The explicit color acknowledgement permits file preparation;
it is not a proof that a loaded shader/atlas participant is safe to rewrite.

The helper/client require stdio protocol v2 plus the exact output-contract string;
the executable SHA-256 participates in every export key. The packager verifies
the executable and the complete native source/build recipe hashes, copies third
party notices and includes matching source. Native files, pinned dependencies,
Windows SDK/toolchain and binary identity are separate facts. Source archives do
not count as a shipping or executed helper. No runtime dependency download exists.

## Offline evidence and review

* Final focused guarded suite: **109 managed checks, zero skips; 50 Python
  checks passed**, zero build warnings/errors. The optional actual-helper managed
  test executed, including source selection identity, conversion, DDS validation,
  publication, reopen and proof that native consumption cannot read the export.
  Receipt: `artifacts/r2-texture-preparation/final-checks.log`; test output
  `artifacts/fixture-tests/d7de177d14424647baa2acf75df0110f`.
* **31 helper CPU cases passed**, including orientation, alpha, every mip layout,
  full/half/quarter, non-power-of-two exports, BC1/3/7 DDS inputs, malformed input,
  size refusal and child cancellation. `helper-cpu-checks.json` records the actual
  executable under `artifacts/r2-texture-preparation/`.
* Six real frozen DDS conversions passed with source hashes unchanged. Exploration
  (4096×2304) exported 6,291,656 / 1,573,064 / 393,416 bytes; Growth (4096×2560)
  exported 6,990,680 / 1,747,800 / 437,080 bytes at full/half/quarter. These are
  file sizes, not loading-time savings. `frozen-dds-helper-checks.json` records
  exact inputs/outputs. Their PNG siblings remain unselected.
* `frozen-header-evidence.json` distinguishes inventory from actual selection.
  The frozen inventory contains 43,305 PNG and 43,305 DDS paths including inactive
  folders; these are not eligible runtime counts. The historical native-selected
  PNG count was zero. BestDog's 1470×1961 JPEG is not proven compressed-fitting.
  No DDS was hidden to manufacture a PNG benefit.
* The independent bounded reviewer found synchronous broad scanning and missing
  guaranteed-oversize export preflight. Both were corrected. Focused tests also
  caught and corrected export exception/path-validation handling. An initial test
  compilation ambiguity was fixed. No known failure was reclassified as a pass.
  The earlier R1 temporary-rename failure did not recur in this final 50-test run;
  this does not diagnose its historical cause.

No deployment, run preparation, game launch, UI automation, normal Steam/profile
change, Deck/Linux helper action, security change, push or publication occurred.
The existing deployed DLL remains
`6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`.
The English reset fixture and original mod order remain intact. The XML prefix
experiment remains nonshipping. R3 was not started.

Later owner-authorized qualification must exercise the incremental menu workflow,
native compressed capture and startup upload/rendering, sampler/readability
equivalence, cancellation/close UX, broad provider/override cases and reviewed
export consumers. Only then compare complete menu/colony behavior and matched
performance controls. No offline result here establishes GPU rendering, gameplay
compatibility, a universal benefit or preparation break-even.

## Committed source, packages and ownership closeout

Implementation source is **`b6e4224b422960952d7d89baf5a61aa62d6337bb`**.
The guarded core package build passed with zero warnings/errors and no game
launch. Its receipt is
`artifacts/fixture-package/b6e4224b422960952d7d89baf5a61aa62d6337bb.json`;
build output is `artifacts/r2-texture-preparation/core-build.log`.

| Artifact | SHA-256 |
|---|---|
| New core DLL, 269,824 bytes | `0c7e9443bd23d624bd9e74049048f8e23db1b8de6d3dc9f29fd34d973cc43898` |
| Executed Windows helper, 423,936 bytes | `b351d1794b82f2310f643c307d54498693db8c6818380bc6dd5e4141489023e5` |
| Combined source-inclusive ZIP | `4977184f5fe1903547d7ba7721a38cf2cdcc7abff7038d3f2124ba20ac8dd1a1` |
| Unchanged deployed DLL, checked from disk | `6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f` |

The optional local bundle is
`artifacts/releases/0.3.0-rc.1-b6e4224b422960952d7d89baf5a61aa62d6337bb-texture-helper-win-x64/wake-up-0.3.0-rc.1.zip`.
`combined-package.log` records successful source/executable/notices verification
and `steamQualified:false`. The helper was built while the assigned base had
uncommitted implementation changes; its build receipt says so. Packaging verified
every helper source/build-input hash against the committed implementation's
current files and verified the actual executable. It did not infer execution
from included source. The runtime build, helper build and older deployed
candidate remain separate identities.

Final read-only fixture checks confirm the original deployed hash, English
`langFolderName`, no `prepared.json`, and Wake-Up absent from the restored active
order. Profile `Prefs.xml` SHA-256 is
`b34d9738ee08fb3762d5e04a5f4bf7b1152fe7f69fcda878564f231bb618db2a`;
`ModsConfig.xml` is
`9b3ed71fff9bdf5a6dea84486ee5209646ef98c0dcb18e30911698a59e6e96d7`.
Those final observations do not claim a new live run or refresh of frozen mods.

All three bounded subagents have stopped, and no build or helper process remains
owned by this task. This documentation closeout follows the immutable source
package; no package rebuild is needed for receipt text. R2 returns exclusive
checkout/build ownership to the parent steward for review. R3 is not started.

## Parent acceptance — 10 September

The parent accepted handoff `d1cc0bf` and implementation `b6e4224` after inspecting
the actual source changes, focused test receipts, helper evidence and core/combined
package identities. A separate bounded read-only review checked native selection
precedence, publication revalidation, cancellation, shared storage accounting,
atomic export publication and successful-only automatic-cache retirement; it
found no actionable defect. No extra build or test was needed for this review.

This accepts implemented offline behavior, including the explicit portable-export
boundary. Automatic quality substitution, reviewed export consumers and full
converter replacement remain missing. Menu responsiveness, native texture
capture/upload/rendering and loading benefit remain later live checks. The reset
fixture and deployed candidate remain unchanged. Sole ownership returns to the
steward for the serial R3 handoff; the campaign continues without live permission.
