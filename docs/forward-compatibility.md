# Game updates and compatibility

The working source lets new Windows and native Linux RimWorld builds attempt
the selected improvements. An unrelated game revision no longer disables the
entire mod. Published 0.2.0 still uses the previous exact-game restriction;
this source change has not been released or installed.

The previous gate compared the game assembly version, module identifier (MVID)
and complete file hash against three reviewed builds. Runtime selection now
uses the operating system to choose Windows or Linux behavior and retains the
actual version and MVID for diagnostics. Reviewed identities remain useful as
historical baselines and for reproducible build references, not runtime vetoes.
Windows GOG uses the same forward-compatible Windows path as Steam.

Each feature still checks what it depends on. Definition searches require the
expected worker signature and unique native XPath call. Type searches retain
the reviewed Harmony search implementation. PNG caching checks the relevant
loader method fingerprints, and Gagarin checks the XML constructor fingerprint.
These fingerprints compare instructions with resolved references, so merely
renumbering assembly metadata does not invalidate identical method code.
Optional integrations still require their reviewed supplier files and methods.
Existing patch-conflict checks, startup limits, graphics restrictions and
feature-local setup exception handling remain in place.

Harmony remains pinned because the lightweight patch-state checks depend on
its internal data publication behavior. A Harmony update can still disable
Wake-Up until reviewed. Removing that dependency restriction is a separate
change from allowing game revisions.

PNG cache keys now include the actual loaded game MVID. An update rebuilds
eligible entries even if its directly intercepted methods remain compatible,
because unexamined game helpers may have changed. This also invalidates older
cache keys once after updating Wake-Up; PNG caching remains opt-in.

These checks cannot prove compatibility with unreleased games or every mod
combination. A method change may disable only one feature, and a larger API
change may still require a Wake-Up update. The existing live validation applies
to the recorded builds, not hypothetical future versions.

Offline validation simulates unknown versions and MVIDs on both platforms,
checks that future games reach the unchanged Harmony gate, retains the existing
method/patch regression tests, and verifies cache separation after a game update.
No live launch or normal-game deployment is part of this source change.

The offline suite passed 105 managed checks (one existing optional supplier skip)
and all 35 Python checks. Normal and captured-Linux-reference builds completed
with zero warnings or errors. The installed .NET 10.0.401 patch SDK was selected
temporarily for these checks; the repository's SDK policy was restored afterward.
