# Building the public source

The public repository contains the current implementation, tests and build
scripts. It does not contain game files, third-party mod binaries or the
maintainer's private fixture. You can build without creating that fixture.

Use Windows, PowerShell 7, Python 3.11+ and the .NET SDK pinned in `global.json`.
Obtain the required game and Prepatcher references legally. The present build
contract uses the exact reviewed GOG game and Harmony binaries; version labels
alone are insufficient. Their hashes are in `build/Verify-LocalReferences.ps1`.
Do not weaken those checks to make another build compile: Steam support is a
separate pending qualification task.

From a source checkout or extracted source archive, set the paths for your own
copies, then restore and build:

```powershell
$env:RLO_RIMWORLD_MANAGED_DIR = 'C:\your-game-copy\RimWorldWin64_Data\Managed'
$env:RLO_PREPATCHER_ASSEMBLIES_DIR = 'C:\your-prepatcher-copy\Assemblies'
$env:RLO_REFERENCE_TARGET = 'gog-rev573'
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test tests/RimWorldLoadingOptimizer.RimWorld.Tests -c Release --no-build
python -m unittest discover -s tests -p 'test_*.py'
```

These commands do not launch the game. Runtime output is under
`artifacts/bin/RimWorldLoadingOptimizer.RimWorld/Release/net472/`.
Tests require the separately obtained references; no public CI runner is
expected to download or possess the game. Python packaging checks run without it.

The package's `Source/source.zip` carries the actual corresponding source,
including these instructions, pinned dependencies, project files and scripts.
`Source/source-revision.txt` records the original build revision. When rebuilding
that archive without its Git history, pass
`-p:SourceRevisionId=<revision-from-that-file>` to `dotnet build` to preserve
the informational version. A public snapshot commit can differ from that build
revision; neither implies a different source file automatically.

The maintainer's `scripts/op7_fixture.py` controls an existing private fixture
at a fixed path. It is included for transparency and maintenance, not as a
general installer or a prerequisite for contributors.
