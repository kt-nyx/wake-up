# C10 selected audio preparation

## Steward review: selected playback increment retained — 12 September UTC

Stopped handoff `9ec8cd90`, tested source/core/observer/bootstrap `994b88fc`,
passed bounded independent review of selected music and one-shot preparation.
The steward verified all three package hashes, both raw run manifests and 28
observer receipts, 101 metadata entries, 1,274 common startup trace entries and
64 music/43 WAV playback entries. Seven fixture hashes and nine absences match;
no fixture process remains. Independent source review found no concrete blocker.
Comparison uses bounded sampled traces, not full PCM or audible-quality proof.

Native playback prepares only the selected streaming OGG/WAV reader, keeps the
same public/reflected clip and leaves unrelated songs deferred. It does so
synchronously immediately before source assignment. This passes that precise
functional increment, not advance-interval prewarming, live sustainer coverage,
full C10, production integration or measured performance.

The same C10 owner continues from the exact new steward review base to a bounded
native scene/sound dependency preparation step before playback demand, plus actual
sustainer qualification. Preserve the game's selection/random state and original
callbacks; prepare a bounded imminent set rather than every audio asset. Keep
selected synchronous preparation as the demand fallback. Demonstrate real early
completion and later native consumption without observer pre-drain. MP3 decoded
reuse/provider identity and texture/graphic/icon requirements remain open before
release, together with stronger concurrent-loader compatibility. No C11 successor
or performance window follows this review. Raw review:
`artifacts/ecosystem-next-20260910/c10/audio-readiness/steward-review.json`.

This increment prepares an already selected audio clip immediately before the
game assigns it to a playback source. It preserves the same public clip and moves
the pending native decoder reconstruction into that selected playback call. It
does not select future songs or promise an advance preparation interval. Full C10
replacement and measured performance remain open.

## Scope and MP3 finding

Action `wake-up-c10-audio-first-use-ef1f310c-20260912` started from
`ef1f310cdce9c9377218a6247fc6484de66557f1`, with exclusive checkout and isolated
GOG operation ownership. The owner authorized the readiness implementation as
the immediate continuation if the bounded MP3 inspection found no useful new
compatible work. PC use remained active; no performance experiment was allowed.

The MP3 inspection found real eager allocations, but no useful new avoidance for
the verified ordinary nonstreaming files. `AcmStreamHeader` allocates and pins
source and destination conversion buffers; `Mp3FileReader` additionally allocates
its decoded-frame buffer. Native `Manager.DeferredLoaderMain` reads the complete
nonstreaming clip and disposes its reader, and `Manager.Update` uploads the samples
before publishing Loaded. Those buffers are therefore required during ordinary
startup. Moving their allocation from constructor to that worker would move work
and allocation-failure timing, without leaving an unused ready clip deferred.
Header preparation and conversion were already delayed until Convert.

Native streaming selection is format-independent: an existing filesystem file
larger than 307,200 bytes streams. The managed source alone does not establish
that `AudioClip.Create` leaves any natural streaming MP3's buffers unused; its
callbacks can request samples immediately. This inspection did not qualify an
additional useful streaming MP3 family and did not implement a buffer adapter.
It does not reject every possible MP3 design.

Actual ACM selection, open, output format, instance and native decoding remain
unchanged. Keeping that live instance establishes current reader ownership, not
the identity needed to trust decoded samples from an earlier process. Persistent
MP3 decoded-sample reuse still needs trustworthy selected-provider binary and
selection identity, or current native validation. The retained frame-index reuse
and its completion correction keep their separately recorded evidence.

Inspection used the pinned GOG game assembly and NAudio SHA256
`0df62f4e48776870ee22450d1735f00a69493d3c2c6dfb91cc4feab27150d6f1`.
The existing `stream-research/NAudio.Wave.Mp3FileReader.cs` extract under the private
C10 artifacts contains the constructor and read/seek path; Manager and
AcmStreamHeader were inspected through the existing read-only ILSpy tool.

## Selected playback behavior

Native music chooses a song when it starts playback; it has no committed next-song
queue that this increment can warm early without changing selection. Calling
`ChooseNextSong` early would affect random selection, recent-song history and
current map/time/danger conditions. The implemented boundary instead uses the
clip already chosen by the game.

`PreparedAudioReadiness` wraps the existing `AudioSource.clip` setter argument in
`MusicManagerPlay.PlaySong`, `SampleOneShot.TryMakeAndPlay` and
`SampleSustainer.TryMakeAndPlay`. The wrapper returns the same object. Original
allocation, assignment, seek and playback instructions remain. It performs no
extra Unity getter, song selection or outer reader Read/Seek.

Only the selected streaming OGG/WAV reader registered in the current audio epoch
can be prepared. Preparation uses the established registry-then-reader lock order
and reconstructs the original lower reader by replaying only operations already
served from cached data. Unrelated pending readers remain deferred. Repeated
preparation, disposed readers and closed/stale admission do not recreate work.
Native reconstruction errors are retained and rethrown by the original later
consumer; the optional readiness call does not invent a new playback exception.
Existing close, source disposal and epoch handling remain responsible for cleanup.
There is no new asynchronous queue or background selection policy.

The three native method contracts are checked before installation. The effective
music contract includes the existing Wake-Up startup rewrite of two SongDef.clip
field reads into its established lookup bridge. Effective identities are derived
offline from the original game through that exact known rewrite and serialization,
not accepted by learning an arbitrary running method. Published foreign patches
on the relevant consumer or clip setter disable this preparation path. Guard
queries occur before audio locks. This is bounded published-patch protection;
unknown concurrently changing loaders are not broadly qualified.

The path remains experimental and enabled only by the isolated
`readiness-prepare` / `readiness-warm` modes. This does not adopt the experimental
Doorstop backend for production or change the default-off product policy.

## Functional evidence and limitations

The first cold run, `c10-readiness-cold-01` at `a5692372`, completed ordinary native
playback and normal exit, but **does not qualify readiness**. The new hook refused
the effective music body, and the initial cold observer did not require active
admission. That assertion was corrected. The first warm preparation was refused
before mutation because its new cold-cache mode was missing from the existing
admission list; that list was corrected too. The first four deployment/profile
transactions were restored before the corrected packages were prepared.

The corrected pair, `c10-readiness-cold-02` and `c10-readiness-warm-02`, used source,
core, observer and bootstrap `994b88fce84c478e734f764f16ad958889661baf`. Both exited
normally with code zero and captured successful automatic, gameplay, prepared-audio,
bootstrap and preloader checks. Readiness was active in both runs. The unchanged
17-entry workload contains 15 frozen game/mod entries plus Wake-Up and the observer,
including P-Music and the native VWE WAV/MP3 content. English, muted native master
and listener, minimized nonactivating launch and independent menu observation were
preserved. No timing from these functional runs establishes a speedup, overhead
or first-use stutter improvement.

The warm run establishes the following bounded behavior:

- All 101 tracked OGG/WAV readers had zero lower native decoder constructions at
  menu. Their public native clips were already present.
- Native selection of `A_Place_of_Our_Own` prepared its original reader once on
  main thread 1 with cause `readiness-music`. It replayed the 307,200 bytes already
  served during startup, with zero differences. Later native playback returned
  1,536,000 bytes through that same reader.
- Native `VWE_Shot_AntiMaterialRifle` playback prepared its streaming WAV once on
  thread 1 with cause `readiness-oneshot`. It replayed 76,800 served bytes without
  differences; playback subsequently returned all 310,154 data bytes.
- Immediately around the selected calls, 55 unrelated pending songs remained
  deferred after WAV selection and 54 after music selection. Prior native setup
  playback is excluded from these immediate before/after comparisons.
- Public and reflected main-thread reads retained the same clip. A worker's
  reflected field read also retained it, without a Unity API call. Native music
  progress, stop, authored one-shot progress/removal and gameplay save/reload passed.
- The natural nonstreaming LightSMG MP3 retained ordinary background loading and
  playback. Cold native index scan/publication became one warm reused index and
  zero native scans. Its actual native decoder remained eager. This pair did not
  run the separate explicit MP3 read/seek controls or provider probe.

One-shot progress past 90 percent followed by removal from the native manager was
observed. The reason for removal was not directly instrumented; both WAV and MP3
retain `completionReasonObserved=false`. Audible quality and every possible
playback endpoint are not claimed.

Independent source review found no remaining bounded blocker. The focused tests
passed **108 managed cases and 72 tooling cases**, including history preservation,
failure identity, disposal, original/effective method contracts, unchanged native
assignment instructions and existing/late published-patch refusal using managed
controls. Independent completed-capture review verified matching packages,
selection and captured receipt hashes. All 101 startup metadata entries matched
between cold and warm, as did 1,274 common bounded recorded operations and the
64 music / 43 WAV common playback trace entries, excluding thread identifiers.
These sampled digests are not a full decoded-PCM comparison.

Package DLL fingerprints (SHA256), distinct from source and deployed/captured
receipts, are:

| Package | SHA256 |
| --- | --- |
| Core | `655aef30007774bbcd74aae166ad3bbb14c21268665d841eae918817e330fbce` |
| Observer | `4173a73d323c1b05ebb9e76f861dd0473bc4df0b61c56154d7dfc74197d202d0` |
| Experimental bootstrap | `1f02987dbf7afbc61805d93a766ed5471b31ae07e1fd9ea7a7bc8f413426d9f9` |

Private evidence is in `artifacts/ecosystem-next-20260910/c10/audio-readiness/`:
`final-captures.json`, `work-record.json`, `independent-review.json`,
`baseline.json` and `restoration.json`. The final managed receipt is
`artifacts/fixture-tests/0e1b81a0004648e79bb06de53e0a5b23/features.trx`. Original
run profiles, logs and source-bound deployment receipts remain in the two named
fixture captures. The earlier rejected cold capture is retained separately.

## Restoration and remaining work

All five corrected deployment/profile transactions were rolled back in reverse
order, in addition to the four earlier transactions. All seven baseline hashes
and nine expected absences match; the metadata audit passes. No fixture process
remains. Normal Steam process 50776 and local DeckHost 48204 retain their original
executable identities and were not modified. No normal data, desktop/UI, Deck,
Steam promotion, security, merge, push or publication operation occurred.

This is working synchronous selected-clip preparation in the experimental audio
path. It does not yet establish an advance scene/music preparation interval,
asynchronous warming, native sustainer playback qualification, a live foreign-patch
control, general unknown-loader/concurrent compatibility, or new MP3 decoded-data
deferral. The prior MP3 completion correction remains source ancestry with its
own evidence; this pair does not replace that targeted control. Texture, graphic
and icon publication was not reinvestigated in this increment and remains required.
Production backend integration and individual/combined measurements remain open.

The team is stopped and returns checkout/build/deploy/fixture ownership to the
steward for independent review. Full C10 is not self-accepted; no successor was
dispatched and no parent state or timer was edited. Corrections return to this
same C10 owner after ownership transfer.
