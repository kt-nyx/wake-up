# Windows Steam build support

Wake-Up now admits the owner's installed Windows Steam **RimWorld 1.6.4871
rev590** for all seven features' existing compatibility checks. The relevant
loading and texture methods match the reviewed Windows GOG code, so this adds
an exact supported build without replacing the optimization algorithms or
loosening checks for unknown game/mod versions. This change has passed offline
tests but has not been installed or exercised in a Steam game launch.

## Exact build and review

| Identity | Value |
|---|---|
| Contract | `steam-rev590` |
| Assembly version | `1.6.9676.17735` |
| Module identifier (MVID) | `61e41735-6189-4da4-9d21-0260257b5097` |
| Assembly SHA-256 | `5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A` |
| Installed Steam build ID | `23969874` |
| Installed Windows depot / manifest | `294104` / `3464668865934009251` |

The Steam app manifest reports installed state, successful update result and no
pending target build. This describes local Steam metadata as inspected on
2026-09-08; it is not an independent server-side guarantee that a newer build
cannot be available. Support remains tied to the actual file identities above,
not the word "latest".

Read-only metadata comparison found **230 of 230 selected method bodies
identical** to the reviewed GOG reference after resolving their referenced
types, methods and fields. The comparison retains instructions, branch targets,
locals, method flags and exception regions. It covers patch-operation workers,
XML/mod loading, definition searches, root/menu/long-event hooks, texture holders
and loaders (including generated iterators), texture compression and Unity data
initialization. These are the existing helper and interception paths; it is
not a claim that every method in the two game DLLs is identical.

The XML constructor and all PNG method fingerprints therefore retain the
reviewed Windows values. Linux keeps its separately reviewed `LoadItem` value.
Windows Steam textures use the existing Direct3D 11 support, including CPU
processing when compute shaders or texture compression are disabled. No launch
flag is added or required by this change. Other graphics backends retain their
existing fallback.

The installed Prepatcher Harmony DLL, Giddy-Up DLL and Loading Progress DLL
matched their reviewed physical hashes. Other bundled Harmony copies are not
substitutes for the authenticated Prepatcher library. Optional-mod admission
still checks the supplier actually loaded by the game and its intercepted
methods; this review does not force absent or changed optional suppliers active.

## Tests and remaining runtime check

- The ordinary GOG fixture suite passed **101 managed checks**, with one existing
  optional Character Editor physical-supplier skip, plus all **35 Python checks**.
- **22 additional focused checks passed with no skips** when compiled and run
  against the owner's actual Steam game and Prepatcher references. These cover
  exact version/module pairing, original PNG method fingerprints and callsites,
  semantic method checks, cache layouts, platform selection and startup identity
  resolution. Builds had no warnings/errors.
- No game was launched. Normal PC game files, Workshop packages, settings,
  saves and the Deck were not modified by this implementation. The shared
  fixture remains on its existing GOG references.

Private comparison data and Steam-reference test results are under
`artifacts/windows-steam-support/`; the full suite is under
`artifacts/fixture-tests/f5f0edaf2cd241f79b8484c51347b532/`.

After installing this build, the owner's Steam launch should be checked for
actual feature work and normal completion. The new PNG/Loading Progress bridge
still needs live cache creation/reuse and output checks, and focused gameplay /
save-load checks remain distinct from successful startup. The known Gagarin /
Loading Progress XML conflict still causes ordinary fallback on both operating
systems; it is not an unsupported Windows build.
