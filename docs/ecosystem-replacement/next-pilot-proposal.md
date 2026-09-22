# Proposed single pilot after retained baseline consolidation

Status: proposal only. No implementation assignment, automatic helper adoption,
new dependency/default, performance window or tier-selection framework approved.
The retained candidate's inspection corrections are accepted. Finish owner visual/
focus checks first. Review this proposal with the owner before implementation.

## Why this one

Test direct native PNG decoding into raw pixel buffers, using the existing reusable
Windows helper trial rather than inventing another system. The recorded native PNG
texture stage was about 3.9 seconds in its measured workload; warm PNG reuse saved
about 2.86-3.48 seconds of menu time there. That identifies useful cold-image work,
not a prediction that the helper will save the same amount. The losing managed
first-build path added 3.31-4.25 seconds and has been retired from this release.

The inspected todds source processes decoded pixel buffers directly through a
bounded pipeline. It supports the mechanism, not a claim that supplier code is less
safe or native-equivalent in Unity. Gagarin's inspected combined-data bypass still
hashes source/constructed XML; blanket source-parsing bypass is not an established
explanation for its behavior. C04 measured published-hook checks at only a few
milliseconds, so weakening compatibility checks is not supported as the cure for
our observed shortfall.

## Small scope and fallback

Keep existing valid, beneficial warm PNG results on their current path. For cold
PNG images only, qualify the direct helper on precisely tested metadata families;
fallback before publishing a result for the individual unsupported/failed image.
Shared helper failure ends that helper session and leaves remaining cold images
native. Authored DDS and warm hits must not start the helper. Do not route fallback
to the retired slow managed accelerator or reject an entire modlist for one image.
No JPEG acceleration claim: the old JPEG trial decoded twice and compared pixels.

Selection is supported/proven-beneficial aggressive behavior, otherwise a validated
beneficial conservative implementation, otherwise native. There is currently no
proven beneficial conservative cold decoder. Preserve native finalization and exact
output fidelity, including the observed grayscale transparent-key hidden-RGB case.
The trial's old managed fallback is not a release design; use native instead.

## Proof and stop rule

Retain bounded correctness evidence, then use a NEW explicitly authorized unattended
window for one matched end-to-end native/helper comparison. Charge helper startup,
source transfer, validation/inspection, decoding, finalization/upload, waiting,
fallback and shutdown. The present PNG validator fully inflates the image before
WIC decodes it again: include that real cost instead of hiding it in preparation.
Separate construction from warm reuse and respect the existing combined memory and
worker limits. Avoid testing isolated decoder throughput as a startup benefit.

A native-equivalent correctness pass plus repeatable useful end-to-end gain is needed
before installed-user adoption. If the direct path loses after this materially
different mechanism is tested, park it and retain native cold loading. No general
selection framework, reduced safety promises or repeated low-yield correction loop.

Evidence: `artifacts/ecosystem-next-20260910/resume-20260919/performance-review.md`
and `c06-recovery-review.md` in that directory; dated
C04/C05/C06 reports and pinned supplier references in the active campaign plan.
