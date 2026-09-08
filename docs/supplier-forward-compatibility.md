# Compatibility with updated optional mods

Wake-Up now attempts its integrations after an optional mod updates, rather
than disabling them just because the entire DLL changed. Character Editor,
Giddy-Up, Loading Progress and Gagarin remain optional. A required function that
is missing or incompatible disables only the corresponding improvement.

The implementation discovers functions by declaring type and full signature
(name, parameters, return type and generic arguments). It checks their method
fingerprints with resolved references before installing any interceptions.
Assembly version numbers, module identifiers and internal method row numbers
are not supplier admission restrictions. All 31 baseline fingerprints were
captured from the same original supplier files reviewed for 0.2.0; no supplier
implementation is copied or bundled.

Character Editor validates its nine-function preset chain and the required
generic instances. Giddy-Up validates four texture/setup functions. Loading
Progress validates its two repaint functions independently from its five PNG
loader functions. Gagarin validates eleven XML/cache functions and retains the
game XML constructor and foreign-hook checks. Existing startup/thread limits,
output ownership, exception propagation and ordinary-path fallback remain.

An unrelated rebuild that renumbers metadata can pass these checks. Changed
instructions, required signatures, generated iterator structure or conflicting
interceptions can still require an adapter update. This is an automatic
compatibility attempt, not a guarantee about every future mod. Catching an
exception after partially changing game data is not a substitute for checking
the required functions first. Existing diagnostic logs report refusal reasons.

Harmony retains its exact identity restriction because the patch-state guard
depends on its internal publication behavior. The game's own version/revision
restriction is removed by the accompanying [forward compatibility change](forward-compatibility.md).

Offline tests exercise rebuilt synthetic suppliers with changed versions, MVIDs
and reference tokens; missing functions, altered signatures and changed helper
code are rejected. Independent feature resolution and the existing real-supplier
patch conflict, original-output and cleanup checks remain covered. The suite
passed 106 managed checks (one existing optional Character Editor fixture skip)
and 35 Python checks. The release notes record the Windows live-check outcome.
Compilation against the captured Linux references also passed with zero warnings
or errors; no new Linux live launch is claimed.
