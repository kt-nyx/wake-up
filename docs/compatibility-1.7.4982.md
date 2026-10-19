# RimWorld 1.7.4982 compatibility update

Wake-Up's installed preview correctly returned to ordinary loading after the
game and Harmony updated. Preview 2 refreshes the exact reviewed identities;
it does not broaden the guard to arbitrary 1.7 builds or change the optimization
algorithms. The new candidate is staged for later installation. It has not been
installed or tested in a game session.

## Scope and evidence

The 18 October handoff authorizes investigation, implementation, offline checks
and staging only. The owner's game is running. Do not change its installation,
settings, saves or mod order, or launch/stop a game under this assignment.
The separate fixture and its original 1.7.4977 game are not refreshed.

The captured current-session log identifies `1.7.4982 rev1280`. At line 796,
Wake-Up reports `Unreviewed Assembly-CSharp build` and retains ordinary loading.
That is the expected protective behavior of the installed `c56e89ab` preview,
whose contracts cover 1.7.4977 and Harmony rc.2. No Wake-Up optimization is
enabled by that failed admission check.

Current references were copied read-only into the private review directory;
all inspection uses Cecil/ILSpy metadata and instruction decoding. Game code is
never loaded reflectively or initialized by the review.

| Reference | Reviewed identity |
| --- | --- |
| Game | 1.7.4982 rev1280; MVID `a46e6f45-b616-4261-bb63-a9471bf7402a` |
| Assembly-CSharp SHA-256 | `69cf35594dcc55d1d77cae482887e880ec2d7c043e139d54d5c59b2dd2b940d4` |
| UnityStubs SHA-256 | `207a76f55695d859691aef49818156725e2e6e94937cc6867b2e568b4be77ac6` |
| Waylith SHA-256 | `30e52fca5f3d62d900fa5a8ac6848bd578dda7b8cdabf4961629b0fa56524b4c` |
| Harmony | 3.0.0.0-rc.3+3c0e32cd3fd20e4ae27c47cb8c888844766f9f1a; MVID `f89a6d42-3950-4658-a805-68a32790beea` |
| Harmony SHA-256 | `c8ef7856b2be3cf24b345c9b5c2f197f68fcf92649635064fce7b3ccc8f77cb0` |

## Why the refresh is justified

All 212 methods in the existing game review set match the new build after
decoding instructions and resolving referenced member names. The comparison
also includes locals, method implementation flags, exception regions and field
layouts of the selected declaring types. No selected methods were added or
removed, and no selected declaring type's fields changed. Raw instruction hashes
do change because compiled reference identifiers change; matching names and
instructions establishes more than merely replacing a version string.

This covers native XML workers and their loading stage, type discovery and its
predicates, alert construction, the selected reflection/translation consumers,
and the entry update callback used to end startup reuse. The runtime still checks
the exact new assembly MVID/hash and each method's new raw instruction hash.
Unknown builds continue to fall back; the original preview remains the retained
candidate for the old game/Harmony pair.

Harmony also changed, so game-method equality alone was insufficient. Static
comparison found 11,485 unchanged method bodies and ten changed existing bodies,
plus new dynamic-method generation helpers. Review of patch generation, generated
assembly loading, weak method references and shared publication confirmed that
the guard's `Dictionary<MethodBase, byte[]>` remains readonly and synchronized,
with replacement byte arrays on publication. Publication serialization and
deserialization bodies are unchanged. New generated-method ownership does not
require Wake-Up to retain generated methods or alter supplier ownership checks.

The existing 66 offline C# checks pass against the copied rc.3 DLL. These execute
real Harmony patches on synthetic targets, including publication changes,
exception cleanup, surrounding supplier callbacks, deferred iteration and
translation scope cleanup. A check confirms game/Waylith assemblies are absent
from the test process. All four focused Python package checks also pass.

## Change and limits

- Refresh the checked game, engine and Harmony identities and 212 method entries.
- Identify the candidate as `0.5.0-preview.2`; retain the Prepatcher-free design,
  existing defaults, optional supplier checks and native fallbacks.
- Preserve the released 1.6 payload and the installed preview. Do not deploy the
  test observer or any game/reference assemblies with the staged candidate.

Private reference copies, normalized comparisons, decompiled review material,
test output, build receipt and candidate package belong under
`artifacts/compatibility-1.7.4982-20261018/`. The staged manifest and build receipt
identify the exact source revision and DLL. Product source is committed before
building so the package can carry matching immutable source.

These are offline compatibility checks on the ordinary .NET test host, not a
gameplay or Waylith-runtime test. The older candidate's bounded colony checks do
not qualify this binary on the updated game. Loading improvement remains
unestablished. A later authorized installation must wait for normal game exit;
the following owner-run session should verify startup admission, expected
feature-local fallbacks and save/reload behavior. Publication is not assigned.
