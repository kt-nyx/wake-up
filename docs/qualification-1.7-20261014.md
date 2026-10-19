# Wake-Up 1.7 qualification — 14 October 2026

Automated testing has stopped for the owner's real-play handoff. The candidate has passed bounded game checks, but
has not yet established a consistent worthwhile loading improvement. Do not publish
or describe it as release-ready on the strength of a build or an isolated fast run.

## Tested scope and identities

The separate `.rlo-test-instance/1.7` lane copies the installed Steam 1.7 game and
the available current Host collection. Its frozen configured order contains 457
packages, including six official packages. Controls omit Wake-Up; candidates include
it. Both include the same independent test observer. Prepatcher remains absent.
Tests run on the vex workstation with copied inputs; these timings are not the
owner's ASTER/Apollo Host timings. Mod content, profiles and results are backed by
owned storage on C through junctions under the lane. The permanent 1.6 fixture and
normal played profiles are preserved. No publication is authorized.

Game: **1.7.4977 rev1248**, .NET `10.0.10-waylithclr.dev`. Assembly-CSharp SHA-256:
`3d4f1ddfe71bd8760cb488255e3bdeac9366f537dc1ef813cc6f8d09824e2da9`.
Harmony: **3.0.0.0-rc.2**, SHA-256:
`94dd929a06f720869f6a718a64bc6713a37dbe153c2f4c16c031b2587d7ee694`.
The released 1.6 payload remains 0.4.2. At the owner's subsequent request,
the normal installed preview was updated from `f09ca848` to the exact tested
`c56e89ab` DLL, without rebuilding or installing the private observer. All 33
package manifest files were verified, and the previous installation was backed up.
Host mod order and settings were preserved. The normal logs rotated by testing
were restored after preserving the final test logs. Installation evidence is under
`artifacts/implementation-1.7/candidate-c56e89ab-playtest-20261014/`.

The final earlier-mod-order batch completed two successful comparisons: native
217.6 s / Wake-Up 214.4 s, and native 231.8 s / Wake-Up 208.1 s. In the third
comparison the control passed at 215.7 s, but Wake-Up exited with code 1 after
reaching the menu at 210.2 s, while private definition capture was incomplete.
No Wake-Up exception was recorded; the cause is unresolved. Exclude that sample
from performance qualification. The successful colony checks remain tied to their
own captured runs, not to this failed sample.

Product changes tested:

- `8007d1f1`: preserve native XML-stage prefix/postfix/finalizer callbacks while
  admitting scoped query reuse; rewritten method bodies still yield.
- `572f9f0f`: preserve surrounding native XML-worker callbacks as well.
- `c56e89ab`: optionally bridge the exact reviewed XML Extensions helper methods;
  reuse static assembly type maps across assembly-sequence refreshes when safe.
  DLL SHA-256: `94a8d6206595f5cb5691c53f0a5e6a4b5ef106a7bfc1edc103593601617e0797`.

The installed runtime reports metadata updates supported, so the new static type
map optimization deliberately remains unavailable. Its offline checks do not prove
a loading benefit on this runtime. The candidate remains Prepatcher-free.

## Measurement and correctness boundaries

Time is process launch to the first completed native main-menu repaint. Warmups
are declared separately; repeated comparisons alternate native/Wake-Up order.
Setup, patch installation and index construction are included. Definition capture
occurs after the endpoint. Stage timings overlap startup and must not be added to it.
Other applications remain running; no normal game, Steam or ASTER session is stopped.

The public definition projection covers every registered definition, excludes
generated indexes and random debug IDs, follows public fields to depth two and
bounds enumerable inspection at 4,096 entries. Matching captures are useful evidence,
not a proof of every private/deep relationship. Identical complete projection files
are retained once by SHA-256 with per-run references; logs and failed runs remain.

## Completed results so far

| Configuration / product | Counted pairs | Native median | Wake-Up median | Interpretation |
|---|---:|---:|---:|---|
| Full collection, `572f9f0f` | 3 | 216.7 s | 222.0 s | Slower in two pairs; no useful gain |
| MissileGirl and Loading Progress absent, `c56e89ab` | 3 | 214.6 s | 225.0 s | Slower in two pairs; no useful gain |
| Full collection, `c56e89ab` | 3 | 223.4 s | 210.6 s | 5.7% median reduction, two wins; native spread 28.0 s makes qualification uncertain |
| MissileGirl absent, supplier screen | 1 | 227.4 s | 228.0 s | No material difference; screening only |
| Loading Progress absent, supplier screen | 1 | 217.5 s | 230.9 s | Slower; screening only |
| Other two performance mods absent, supplier screen | 1 | 203.5 s | 240.0 s | Slower; screening only |
| No Modlist on Loading and Loading Screen Repository absent, supplier screen | 1 | 231.9 s | 246.0 s | Slower; screening only |
| Giddy-Up absent, supplier screen | 1 | 198.1 s | 193.8 s | 2.2% faster; screening only, not qualified |
| Leaf-class reuse only, full collection | 3 | 205.0 s | 235.8 s | Slower in two pairs; no useful gain |

Full-collection captures match across controls and candidates: 77,252 projected
definitions, SHA-256 `b35b99a3b863a3b1512c9c12a0d3b94096d0a97dbd5b80b4f6c7471f104f8a20`.
Omitted-supplier cases are compared within their own configuration.

Without MissileGirl, the measured native XML invocation took about 2.7 seconds;
the candidate took about 4.2 seconds while reusing about 29,695 queries. With a warm
Gagarin cache, both invoke the XML stage in roughly 0.3–0.4 seconds and Wake-Up reuses
zero queries. Correct handoff is established for these samples; XML search counts
do not establish an additional startup gain. Leaf reuse reports 197 proven leaves
with Gagarin, including when Loading Progress is absent; it reports zero without
Gagarin in these menu samples. The next comparison isolates that mechanism.

New-colony creation, 120 native simulation ticks, native save/reload and a further
120 ticks passed for the small control and the early-initialization candidate.
The copied current colony passed the same round trip, retaining ten colonists.
Native play entry advances one initialization tick after reload; the observer
records that boundary and checks a new game instance. The first overly strict
control assertion is retained as a failed test, not a Wake-Up failure.

The full copied-colony control and candidate both contain 19 logged errors,
including pre-existing CE compatibility, Simple Baby Carry, missing Tameness texture
and faction-relation errors. This is not an error-free collection. Window inspection
timed out twice; visual colony inspection is not claimed.

The first two attempted French checks actually loaded English: 1.7 uses the full
virtual language name `French (Français)` from its tar archive. These runs establish
only English behavior. The corrected pair captured that exact French language,
passed colony/save/reload and matched 16,239 translated definition projections:
SHA-256 `2af188e59fe4ed1db25a19034ac7ef01ae7fb24a7f7ecdb7785264b4f2360920`.
Wake-Up's optional translation member reuse was enabled. This is bounded language
coverage, not a full-collection translation performance claim.
Focused offline C# checks pass 66/66; the targeted private
package checks pass 4/4. They remain separate from game and speed qualification.

## Real-play handoff

The five supplier screens are complete, with four matching captures per case.
Leaf-only failed the benefit gate. The final earlier-mod-order comparison did not
qualify because its third candidate capture failed. These attempts have not
established consistent worthwhile loading improvement, and no public release is
recommended on that basis.

The owner requested installation for real play regardless of performance. The
exact latest DLL that passed bounded gameplay checks is installed, with no private
observer. No installation-time game launch was performed. The previous preview
is backed up, normal fixture-rotated logs are restored, and both normal modlists
and the released 1.6 payload hashes were verified unchanged during installation.
Review subsequent Host play logs when the owner supplies or requests them;
long-session stability and the isolated failed capture remain unresolved.

Raw runs, wave records, source manifests and storage receipts remain under the
ignored 1.7 lane and `artifacts/implementation-1.7/overnight-20261014/`.
