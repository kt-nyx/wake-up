# Wake-Up 0.2.1

- Attempt loading optimizations on future Windows and native Linux RimWorld
  revisions without requiring an exact game version or whole-file fingerprint.
- Attempt Character Editor, Giddy-Up, Loading Progress and Gagarin integrations
  after updates. Check the specific required functions before activation and
  retain ordinary behavior when an integration is incompatible.
- Discover supplier methods by signatures instead of fixed internal numbers.
  Loading Progress repaint and PNG integration checks are independent.
- Separate processed PNG cache entries by game build. Existing caches rebuild
  once after this update; PNG caching remains optional and off by default.
- Preserve the exact Harmony check and existing conflicting-patch fallbacks.

Offline validation: 106 managed checks passed, one optional Character Editor
fixture check skipped, and all 35 Python checks passed. The Windows Steam launch
reached the main menu and exited automatically with code 0. All 1,682 Giddy-Up
comparisons and seven PNG cache-creation comparisons matched original output;
Character Editor and Loading Progress repainting completed without errors.
Gagarin passed its function checks then preserved ordinary loading for the known
Loading Progress XML conflict. No new error headlines appeared. Modern Dev Tools
was temporarily disabled for unattended testing. A final package cache-reuse
launch is also required before publication; the release records its outcome.
Linux had startup and texture validation in 0.2.0;
this release does not add a new Deck live-test claim. Future compatibility and
arbitrary gameplay/save-load behavior are not guaranteed.

The Workshop description is preserved unchanged at the owner's request.
