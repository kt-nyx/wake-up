# Wake-Up 1.7 testing handoff

**Current development disposition (19 October):** all search features are disabled
in `codex/1.7-loading-reduction`, including for existing enabled profiles. No
reliable loading benefit was established. See the
[current investigation](qualification-1.7-cloud-20261019.md). The active-feature
behavior and dated assignments below describe earlier candidates. Build commands
and exact reference requirements remain applicable.

This private candidate removes the Prepatcher requirement for 1.7. It retains
selected searches and leaves textures, persistent caches, asset preparation and
loading screens to RimWorld. Offline checks establish build and synthetic behavior;
in-game correctness and useful loading-time improvement remain separate questions.

## Earlier active-candidate behavior

Definition/template, Harmony type-name fallback, leaf-class and loading-member
reuse are independently selectable and initially on. Translation member reuse is
off by default. Translation duplicate scans remain native because the old cache's
dictionary-change proof is invalid on .NET 10. No preload attribute bridge is used.
The overnight branch adds an optional bridge for the exact reviewed XML Extensions
query helpers. Missing or changed helpers stay native. No native helper,
background-loading option or display replacement is present in the 1.7 assembly.
The installed runtime's metadata-update support disables static type-map reuse.
See the [qualification record](qualification-1.7-20261014.md) for measured behavior;
the candidate has not established the required consistent benefit.

The settings class is `WakeUp.V17.WakeUp17Settings`, owned by `WakeUp17Mod`, with
separate keys and settings filename from 1.6. Settings take effect next launch.
Compatibility status and consumed-work counters appear in settings and the log.
An installed hook is not proof that an index was used. Gagarin can bypass XML patch
application; zero indexed queries is a valid result, not an acceleration claim.
Observation starts at Wake-Up's constructor and misses earlier constructors and
asset prefetch. Startup indexes are released after loaded data and queue drain.
Leaf proofs may also run within a bounded native alert-construction scope; optional
translation metadata is released after each package, including on exceptions.

## Reviewed references

`src/WakeUp17/Contracts.json` records the exact Windows 1.7.4982 rev1280 assembly
and Harmony 3.0.0-rc.3 identities for preview 2. See the
[compatibility review](compatibility-1.7.4982.md). Earlier test evidence below
belongs to its original game and DLL. The candidate is deliberately pinned for
testing: changed game/Harmony identities preserve ordinary loading and report the
reason. Do not bypass the gate when unstable updates. Refresh static inspection and
review the affected code before accepting new identities. No game assemblies are
loaded reflectively by inspection scripts or the offline test host.

Install the exact .NET SDK 10.0.401 pinned by `global.json`; 10.0.402 does not
satisfy its disabled roll-forward setting. In PowerShell:

```powershell
$env:WAKE_UP_17_GAME_DIR = 'C:\your-legal-1.7-game-copy'
$env:WAKE_UP_17_HARMONY_DIR = 'C:\your-harmony-copy\1.7\Assemblies'
dotnet restore src/WakeUp17/WakeUp17.csproj --locked-mode
$candidateRevision = (git rev-parse HEAD).Trim()
dotnet build src/WakeUp17/WakeUp17.csproj -c Release --no-restore "-p:SourceRevisionId=$candidateRevision"
dotnet restore tests/WakeUp17.Tests/WakeUp17.Tests.csproj --locked-mode
dotnet test tests/WakeUp17.Tests/WakeUp17.Tests.csproj -c Release --no-restore
python -m unittest discover -s tests -p test_package_17_candidate.py
```

The test project compiles the actual search adapters with synthetic targets and
references Harmony, but no game, Unity or Waylith assembly. It checks real patch
execution and exception cleanup, not just instruction rewriting. Build output is
`artifacts/bin/WakeUp17/Release/net10.0/WakeUp17.dll`.
When building an extracted source archive without Git, set `candidateRevision`
to the companion `Source/source-revision.txt` value instead. The private packager
requires the built DLL to identify its exact committed source revision.

The prepared `0.5.0-preview.1` package is under
`artifacts/implementation-1.7/candidate-f09ca848/` in the development checkout.
It contains the frozen `f09ca848` build and corresponding source. The implementation
plan records full DLL/ZIP hashes; its subsequent closeout is documentation only.

Static reference review uses `scripts/inspect_17_contracts.ps1` with explicit
`-GameDir`, `-HarmonyDir`, `-CecilPath` and `-OutputPath`. Cecil reads metadata and
PE bytes without executing the target. The compiled product is inspected in the
same way by `scripts/record_17_build.ps1`; its receipt checks dependency absence
and records the build revision. Compiler references are never bundled with 1.7.

## Package boundary

`scripts/package_17_candidate.py` creates a fresh private folder and zip under
ignored artifacts. It reads a verified 0.4.2 package, preserves every recorded
payload byte except the combined metadata, and moves its DLL to
`1.6/Assemblies/WakeUp.dll`. The original 1.6 source/manifest are under `Source/1.6`;
the unchanged optional helper and its original source/notices serve 1.6 only.
The new runtime is `1.7/Assemblies/WakeUp17.dll`. Root Assemblies is absent.

Explicit `LoadFolders.xml` and versioned dependencies select Prepatcher for 1.6
and Harmony for 1.7. Prepatcher is excluded on 1.7. The candidate has no published
item ID and does not upload, install or change any subscribed package. The root
source archive comes from the exact committed build revision; user edits and
private game/fixture files cannot enter it. Package receipts retain full hashes.

## Authorized overnight tests

The owner authorized full overnight correctness/performance tests and improvements
on 14 October, superseding the earlier offline-only scope. The separate 1.7 lane
uses `scripts/fixture.py modern`; it does not replace the permanent 1.6 installation.
The owner's unrelated fixture path correction remains preserved. Follow the
[overnight plan](overnight-1.7-plan.md). Do not change fixture records to evade admission.

Start with Harmony, Core and Wake-Up, then inspect settings/status and actual
nonzero work counts on representative inputs. Compare master-off and individual
options, including translation member reuse off/on. Test load completion, colony
creation, alert construction, translations, save/reload and normal exit. Add
Gagarin and Loading Progress independently and confirm native fallback where they
own work. Never stop a normal game or use force-closure as a successful result.

Performance tests need separate authorization and matched controls. Report full
startup and colony-entry times separately, and separate native cold cache builds
from warm reuse and supplier cache hits from misses. Include index/patch setup and
diagnostic overhead. Preserve negative outcomes. Publication remains unassigned
until 1.7 reaches the main game branch, final checks pass and the owner approves.
