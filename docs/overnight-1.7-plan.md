# Wake Up 1.7 overnight qualification

The owner authorized correctness tests, controlled performance comparisons and
evidence-led fixes on 14 October 2026, until approximately 10:30–11:00 AM Toronto
time. This supersedes the earlier offline-only scope. Publication remains separate.
The goal is a working candidate with a repeatable, worthwhile additional benefit
over the same RimWorld 1.7 setup without Wake-Up. Failure to establish that benefit
is an acceptable outcome; do not prolong losing approaches.

## Preserve the played setup

Use the installed 1.7 game and current Host mod collection as the source for a new
lane under the permanent `.rlo-test-instance/`. Do not replace its existing 1.6
game. Copy profiles, mod content and any selected save before changing test inputs.
Preserve the owner's existing `scripts/fixture.py` path correction. Never stop a
normal game, Steam, ASTER or Apollo. Only one owned game runs at a time. Save the
normal logs before Waylith's process-wide logger can rotate them, and restore them
after capturing all test logs. Do not publish or update dependencies.

## Directed sequence

1. Establish a small 1.7 correctness/control run and independently observe its menu.
   Record normal exit, actual loaded content, feature execution and errors.
2. Establish the current full collection. Separate baseline failures from Wake-Up
   regressions. Temporarily omit incompatible content when it blocks useful tests;
   report the omissions and retain an as-installed comparison.
3. Screen Wake-Up on/off with supplier combinations. Include native loading,
   MissileGirl, Loading Progress, XML Extensions and relevant content consumers
   such as Giddy-Up and Character Editor when present. Inspect other installed
   loading suppliers before deciding whether a live combination is meaningful.
   Test graceful yielding and unsupported-provider behavior, not only named
   integrations. Keep Prepatcher's obsolete Mono loader out of the 1.7 lane.
4. Use declared warmups followed by forward/reverse matched pairs. Keep native and
   supplier cold-cache construction separate from warm comparisons. Count full
   process-to-menu elapsed time including Wake-Up setup/index costs. Do not sum
   overlapping stage timers or discard inconvenient samples.
5. Use observed stage work to select at most a few substantive improvements. Review
   correctness, run focused offline tests, rebuild, then repeat matched comparisons.
6. Check new-colony creation, alert construction, translation options, a copied
   current save, save/reload and normal exit. Compare semantic content and errors
   against controls. Menu success is not complete gameplay qualification.
7. Freeze the final source/binary/package identity, record the matrix and limitations,
   restore the played setup and hand over either a qualified private candidate or
   a clear failure report.

## Decision rule

Screening differences select follow-up work; they do not qualify a release.
For a representative warm-loading result, seek at least a 5 percent and 10 second
median reduction, a consistent direction across matched pairs, and a difference
larger than the observed run-to-run spread. Smaller workloads can demonstrate
correctness but need their own practical interpretation. Report supplier-specific
losses and yields alongside gains. If repeated controls show no useful difference
and a bounded set of concrete algorithmic improvements does not change that result,
stop rather than expanding the harness or chasing noise.

Detailed inputs, captures, failed samples and source identities remain in ignored
fixture/evidence directories. Windows evidence does not qualify Linux or Deck-native
execution; Apollo streaming is distinct from running RimWorld on the Deck.
