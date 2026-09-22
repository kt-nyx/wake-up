# C10 advance preparation for native biome ambience

## Steward review: biome preparation and sustainer behavior retained — 12 September UTC

Clean stopped handoff `6ca9f36c`, tested source/core/observer/bootstrap `062b6990`,
passed independent functional review of the bounded current-biome preparation
interval and native sustained-sound lifecycle. The steward verified all three
package hashes, both run manifests and 28 observer receipts, ambience metadata,
20/47/64 common menu/playback/reload trace rows, seven baseline hashes and nine
absences. No fixture process remains. Independent source review found no blocker
in the native interval, field/accessor guards, event budgets, cancellation or
single-reader settlement. Traces sample at most 256 bytes per read; this is not
complete PCM equality, audible-quality qualification or a performance result.

Warm preparation completed on the existing loading worker before selected
playback. The original clip and one reader survived native playback/navigation
and reload; native navigation End/cleanup and reload source destruction were
correctly distinguished. Unrelated songs stayed deferred and selected-call
fallback retained its evidence. This accepts that precise functional increment,
not all sound-bank families, full C10, production adoption or measured improvement.

The same C10 owner next addresses the concrete concurrent loader gap: a second
thread can discover a newly loaded third patching-library copy before its
AssemblyLoad callback finishes settlement. Close that earlier publication/use
boundary without changing native consumer behavior, or return the precise missing
runtime contract and a concrete design. Do not expand this into universal defense
against arbitrary private-memory modification or repeat late-history inference.
MP3 decoded reuse/provider identity, broad texture/graphic/icon finalization and
production integration remain current release requirements. No C11 successor or
performance window follows. Raw independent evidence is
`artifacts/ecosystem-next-20260910/c10/audio-advance/steward-review.json`.

This increment prepares a small set of a map's existing ambient sounds while
the game is still loading that map. The game later chooses and plays those
sounds through its ordinary sustained-sound lifecycle. Preparation immediately
before a selected clip is assigned remains the fallback for other sounds.

## Scope and execution

Action `wake-up-c10-advance-audio-ff2b3cd0-20260912` began from reviewed commit
`ff2b3cd0f996a519a9cee159910ea404259692e3`, with exclusive checkout and isolated
GOG operation ownership. PC use remains active. All validation is functional;
no performance, codec-cost, overhead or resource-pressure experiment is included.
The existing Doorstop bootstrap remains an isolated experiment, not approved
production integration. This increment does not complete C10 or authorize a
successor, publication or platform promotion.

## Preparation interval and compatibility

The native map-switch notification queues ambience creation until the loading
worker finishes. A guarded prefix to `AmbientSoundManager.Notify_SwitchedMap`
prepares the current biome's already-resolved streaming clip grains on that
same worker. It admits only an asynchronous native loading event on its own
event thread, before queued completion actions execute. The original native
callback therefore remains later than completed preparation; no new scheduler
or main-thread dispatch is introduced.

The scan follows stored map, tile and biome fields without calling substitute
getters. Exact game-method identities and Harmony patch checks cover the
native scheduling boundaries and the accessor chain those fields substitute.
Unsupported identities or patches refuse advance preparation. The native
notification, selection, random state and playback callbacks remain unchanged.

Each native event permits at most 8 sound-definition entries, 16 subsound
entries, 64 grain entries and 16 distinct clip references. Visited unsupported
entries consume the traversal budget. A changed map invalidates the earlier
scope without resetting that event's budget. Weak references avoid retaining
maps, events or clips after the scan. Only exact native resolved clip grains
qualify; the product contains no fixture package, biome or asset allowlist.

Preparation uses the existing reader, registry admission and reader locks.
Demand arriving during preparation waits for that same reader; it does not
construct a second decoder. Epoch closure, disposal and latched decoder errors
retain the existing behavior. Unsupported or unprepared sounds still reach
the selected-call fallback or ordinary demand path.

## Functional case

The fixture observer selects a valid tile from a native 30% generated world with
Alpha Biomes' existing Ocular Forest biome. Its authored `AB_AmbientAlien`
sustained sound uses the naturally loaded streaming OGG. The observer neither
alters a biome nor creates a definition, clip or sustainer. It watches native
creation, later-frame playback, ending through map navigation, save/reload
cleanup and replacement playback.

Warm evidence must show loading-worker preparation completed before the first
native selected-sample assignment, one decoder construction, the same original
clip, successful captured reader operations and no replay differences. Cold
evidence must retain its already-native reader without reconstruction. Existing
selected music/WAV checks and unrelated deferred-song checks remain enabled.

## Validation status

The product passed 113 focused managed tests and 72 fixture-tooling tests.
Independent review corrected the substituted-accessor guard coverage and the
observer's rejection of failed captured reader/position requests. Original and
owned-prepatch serialized identities agree for all 22 guarded methods.

Two failed functional cases remain preserved. `c10-advance-cold-01` at
`14bb664d` had no valid Ocular Forest tile in the developer-only 5% world and
never reached sustained playback. The native worker requires warm, wet land
and a particular world-noise region. `8e75c8dd` therefore changes only the
observer to the smallest ordinary coverage, 30%, retaining seed and climate.
`c10-advance-cold-02` selected tile `38,0` and proved native looping playback,
but exposed an incorrect reload assertion: native reload destroys the source
and abandons the old sound manager without necessarily setting the abandoned
managed sustainer's `Ended` flag or clearing its sample list. Both games exited
normally with code 0, both functional cases failed, and both sets of four
fixture transactions were restored. Neither failed case qualifies this increment.

The corrected cold/warm pair, `c10-advance-cold-03` and
`c10-advance-warm-03`, passed automatic observation, bootstrap admission,
prepared audio, native formats, selected music/WAV preparation, native sustained
playback, map navigation and gameplay save/reload. Both processes exited
normally with code 0. This is functional evidence submitted for independent
steward review, not full C10 acceptance.

Both runs used source and all three packages at
`062b6990e460a41415b8bf923d076e1536c66398`. The product implementation is
unchanged from `14bb664d`; the intervening corrections concern the observer.
Package SHA-256 values:

| Package DLL | SHA-256 |
| --- | --- |
| Core | `5205fca77c92737af63156e7484777ecccc20d16223afd4169accb8c55a0f9ca` |
| Observer | `8e50d2c0bd6feefa62216966ee250ce593d914a7105010d7f1894b8b84a0bd4d` |
| Audio bootstrap | `dbba4c05b20e4107309ec6c23352b71c7a7562ca591ac413f5b8d05f04cf55e4` |

The warm ambience had no decoder at the menu. Its sole reconstruction completed
on loading worker 134 with cause `readiness-advance-biome`, generation 2 and
completion sequence number (ordinal) 1. The first selected-sample call had
ordinal 2. The reader
reconstructed 282,240 recorded bytes with zero replay differences and remained
the same single native reader through playback, navigation and reload. The
advance scan visited exactly one definition, subsound, grain and clip; later
notifications added no advance reconstruction. Native map switching ended the
original sustainer, removed it from the manager, emptied its samples and
destroyed its source before replacement playback. Reload independently
destroyed the replacement source and recreated playback with the original clip.

The retained selected-call fallback passed for music and streaming WAV.
The warm run recorded 56 unrelated songs still deferred after the one-shot
assignment and 55 after the selected music assignment. These observations are
bounded functional evidence, not full decoded-audio equality or audible quality.

Independent capture review found no bounded blocker. The ambience metadata
matches: 2,051,091 samples, stereo, 44,100 Hz. Ordered reader/position operations
match across all 20 captured menu rows, 47 after-playback rows and 64 after-reload
rows when excluding thread identity. Each read digest covers at most 256 bytes;
this comparison does not establish equality of the complete decoded output.

## Restoration and evidence

All 13 transactions across the failed and final runs were restored in reverse
order within each set. The seven baseline hashes and nine absent paths match
the pre-assignment fixture. The metadata audit passed against manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef` and GOG
generation `gog-rev573-20260910-194017-cf1a1eb0`. No fixture process remains.
The original normal Steam process 50776 and local DeckHost process 48204 retain
their verified executable identities and were not modified.

Raw run manifests and observer receipts remain in the isolated results folders.
Private summaries, exact package identities, review notes, pin derivation and
restoration receipts are under
`artifacts/ecosystem-next-20260910/c10/audio-advance/` in `final-captures.json`,
`work-record.json`, `independent-review.json`, `pin-derivation.txt` and
`restoration.json`. The preserved failed captures are part of this record.

## Remaining C10 obligations

This is bounded biome-ambience coverage, not complete future-sound prediction.
MP3 decoded-data reuse/provider identity, the texture/graphic/icon native
finalization requirements and stronger concurrent-loader compatibility remain
open. Performance requires a new explicit unattended window, followed by the
individual and combined release checks. No speed or audible-quality claim is
made from this functional case.
