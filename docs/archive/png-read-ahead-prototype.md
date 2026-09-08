> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.
> Original: `records/implementation/OP7_PNG_READ_AHEAD.md` at `54bee10c3e806bed346a31a1fb4ac470f68ef5e0`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# PNG texture read-ahead

**Closeout: PNG support works in the small native and Loading Progress tests.**
The owner has requested small-profile testing only and manually closed the
medium candidate because it interfered with Stalker 2. Its failure capture is
complete and the fixture is closed. No further tests are being launched.
No useful medium/full PNG performance improvement has been established.

PNG implementation and small-profile qualification checkpoint, 2026-09-05. Continues
`codex/startup-work-reduction`; accepted searches and the DDS prototype remain.

PNG files can use the same upcoming-file workers as DDS. The new adapter returns
an exact-length copy of prepared bytes to the original PNG reader's caller.
The original game still decodes, builds mipmaps, compresses and publishes the
texture in its original order. No Unity calls move to workers and no bytes are
retained between startups. The shared pool remains two 8 MiB buffers; Unity's
ordinary input byte array is additional, as it is in the original PNG path.

`--png-read-ahead on` requires `--buffered-dds --dds-read-ahead` and shares its
one/two reader setting. `timing` records original PNG reads for matched controls.
Default is off. The adapter patches the non-generic physical file reader only
inside an admitted texture scope, with physical runtime identity, original
method-body validation and a live foreign-patch guard. Unsupported sizes
(under 149 bytes or above 8 MiB), changed files, different call order and unknown
patches fall back. DDS precedence is unchanged in ordinary loading.

The converted fixture still has 43,304 PNG/DDS sibling pairs in its mod folders.
Those inventory totals include inactive/version-specific files and previews,
not actual startup counts. `--fixture-png-source` is a comparison-only switch:
the returned texture-file dictionary selects retained original PNGs instead of
paired DDS entries. Both arms must use it. It does not change any frozen file,
mod, XML or load order. Native PNG decoding remains in both arms. DDS files
without a PNG counterpart remain selected.

Tests cover mixed ordering, DDS precedence, comparison selection, exact-length
arrays, independence from pool reuse and shared resource limits, plus existing
reader/fallback checks. PNG read-ahead works in the small native and Loading
Progress lanes. A worthwhile full-profile speedup remains unqualified; the DDS
percentage must not be generalized to PNGs. The scope is loose physical PNG
files on the game's original image-conversion path, not bundled Unity assets.

## Initial verification

Implementation `c25bf22` passes 117 managed tests and 30 Python checks, with no
skips (`artifacts/startup-next/png-check-02.log`). Its pinned-reader test verifies
the actual inherited FileSystemInfo.FullName call. The initial `0dd2620` guard
checked FileInfo.FullName instead; `png-s01-native-on` safely refused activation
and passed with the original reader. It is not a PNG performance result.

The corrected small native lane `png-s02-native-on` passed at 50.708586 seconds
to menu: 2,853 PNG calls, 2,851 prepared reads (50,265,325 bytes), two fallback
files, zero errors and zero outstanding buffers. Its 6.683 seconds in source
reads includes 6.123 seconds waiting; this was the first actual PNG-source run.
Filesystem cache state is uncontrolled.

Matched Loading Progress small lane:

| Label | PNG reading | PNG calls | PNG read seconds | Menu seconds |
| --- | --- | ---: | ---: | ---: |
| png-s03-observed-control | Original | 2,854 | 0.692310 | 46.995427 |
| png-s04-observed-on | Read-ahead | 2,854 | 0.427040 | 48.291705 |

Both passed automatic acceptance and normal exit with identical observer and
content selection. The candidate supplied 2,852 PNGs, used two fallbacks, had
zero errors, two peak buffers and zero outstanding leases. The read-stage
difference is small and does not establish an overall small-profile gain.

The earlier full DDS repeat's unresolved late-startup stall remains a separate
qualification limitation. No wider-concurrency investigation is part of this
PNG request. Normal Steam, Workshop, profiles, saves and caches stay untouched.

## Full-profile control and resource pressure

`png-r01-control` selected the retained original PNGs with PNG read-ahead
**disabled** (`timing`). It progressed through loading and then stopped advancing
near LunarFramework/unused-asset cleanup and `Starting patches: 1066`. After
over five minutes at that late log position, the owner-authorized verified fixture
PID 9420 was force-closed. Process lifetime was 1183.648 seconds. The launcher
finished capture and correctly recorded `automaticTestPassed=false`, exit
4294967295 and no menu event. This is not a loading-time benchmark or successful
full PNG qualification. No PNG candidate full run was launched afterward.

A GPU sample during the control showed 99% utilization and 15,900 MiB occupied
out of 16,376 MiB. Available system RAM later fell to about 1.8 GiB. The owner
reported Stalker 2 running concurrently and seeing both memory pools near their
limits. Resource interference is plausible, but the last log marker and these
counters do not prove the stall's cause. No D3D11 device error or texture-loading
exception was found in the captured log. The external sampler used 11.391 CPU
seconds and reported no errors. Detailed evidence stays under
`.rlo-test-instance/results/png-r01-control`; the forced-close note and GPU sample
are under `artifacts/startup-next`.

The owner chose **small-profile testing for now; defer full-profile tests until
the PC is less busy**. No hard process-memory cap or texture-resolution reduction
was added. The adapter's shared pool already has a 16 MiB limit, but the game's
decoded textures, input arrays, graphics memory and other startup state are
outside that pool. A hard allocation limit would not make the whole game fit
within it and could instead trigger allocation failures.

The owner subsequently requested a medium profile. `--lane medium` selects
twelve workshop packages with their required foundation, about 10,178 texture
pairs in the frozen inventories. See the fixed selection (historical path: `../../validation/MEDIUM_PROFILE.md`).
Small and medium testing were initially authorized; the owner subsequently
withdrew medium testing because it disrupted Stalker 2. Small profile only now.

`png-m01-control` passed normal automatic exit at 119.419583 seconds: 22 loaded
packages including official/test components, 22,829 definitions, 5,132 patches
and 10,156 PNG reads. PNG reads took 48.460821 seconds; the texture scope took
62.844934 seconds. Peak process working set was 3.506 GiB and minimum available
system RAM was 9.715 GiB. The medium candidate `png-m02-on` did not reach the
menu before the owner stopped it manually; capture completed with
`automaticTestPassed=false`, exit code 1. A mid-run sample had 100% GPU use
and 10,126 MiB of 16,376 MiB VRAM occupied. This demonstrates that substantial
system interference can persist even with memory headroom; it does not isolate
the cause or prove a PNG-reader performance regression. There is no completed
medium candidate timing to compare.

The final small native-precedence regression `png-s05-dds-precedence` passed
normal automatic exit at 58.716833 seconds with PNG support enabled but the
PNG-source comparison switch off. It retained all 2,854 DDS reads, prepared
293,334,224 bytes, reported zero PNG replacements/calls, zero reader fallbacks,
two peak buffers and zero outstanding leases. This confirms native DDS
precedence remains active, not an additional performance claim.

## Continue when the PC is less busy

1. Keep this implementation opt-in. Use the existing small texture lanes while
   the owner is playing a resource-intensive game; do not automatically retry
   the full profile or schedule a run merely because it is later in the day.
2. After the owner indicates the PC is less busy, repeat a full original-PNG
   control using `--png-read-ahead timing --fixture-png-source`, then the same
   profile with `--png-read-ahead on --fixture-png-source`. Both use candidate
   startup-searches, `--hugslib-filter on --buffered-dds --dds-read-ahead
   --dds-read-ahead-workers 2`, the same diagnostics and unchanged menu observer.
3. Inspect actual exit, automatic acceptance, PNG receipts, loaded content and
   captured combined XML before interpreting performance. Repeat promising
   measurements with fresh application caches; Windows filesystem cache is
   uncontrolled. No PNG whole-startup improvement is claimed from current data.
