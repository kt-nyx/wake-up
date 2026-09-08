# Building the public source

The public repository contains the current implementation, tests and build
scripts. It does not contain game files, third-party mod binaries or the
maintainer's private fixture. You can build without creating that fixture.

Use Windows, Git, PowerShell 7, Python 3.11+ and the .NET SDK pinned in `global.json`.
Obtain the required game and Prepatcher references legally. The present build
example below selects the fixture's exact reviewed GOG game and Harmony binaries;
version labels alone are insufficient. Their hashes are in
`build/Verify-LocalReferences.ps1`. The explicit `linux-rev600` target also permits
the captured native Linux references for offline compilation. The historical
`steam-rev590` target remains available. Runtime platform support and the build
host are separate: these instructions still use Windows and PowerShell 7.
Do not weaken identity checks to make another build compile; see the
[Linux preview's qualification limits](linux-support.md).

From a source checkout or extracted source archive, set the paths for your own
copies, then restore and build:

```powershell
$env:WAKE_UP_RIMWORLD_MANAGED_DIR = 'C:\your-game-copy\RimWorldWin64_Data\Managed'
$env:WAKE_UP_PREPATCHER_ASSEMBLIES_DIR = 'C:\your-prepatcher-copy\Assemblies'
$env:WAKE_UP_REFERENCE_TARGET = 'gog-rev573'
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test tests/WakeUp.Tests -c Release --no-build
python -m unittest discover -s tests -p 'test_*.py'
```

These commands do not launch the game. Runtime output is under
`artifacts/bin/WakeUp/Release/net472/`.
Tests require the separately obtained references; no public CI runner is
expected to download or possess the game. Python packaging checks run without it.

To check compilation against the reviewed Linux game without changing the
fixture, set `WAKE_UP_RIMWORLD_MANAGED_DIR` to a private copy of its
`RimWorldLinux_Data/Managed` directory, set the Prepatcher path as above, and
select `WAKE_UP_REFERENCE_TARGET=linux-rev600`. Build only
`src/WakeUp/WakeUp.csproj`; the Windows fixture observer is not a Linux target.
Use an absolute `-p:OutputPath=...` under `artifacts/` to keep this output separate
from the ordinary build. The normal fixture test suite still uses GOG references;
compiling Linux references on Windows is not a Linux execution test.

The package's `Source/source.zip` carries the actual corresponding source,
including these instructions, pinned dependencies, project files and scripts.
`Source/source-revision.txt` records the original build revision. When rebuilding
that archive without its Git history, pass
`-p:SourceRevisionId=<revision-from-that-file>` to `dotnet build` to preserve
the informational version. A public snapshot commit can differ from that build
revision; neither implies a different source file automatically.

The maintainer's `scripts/fixture.py` controls an existing private fixture
at a fixed path. It is included for transparency and maintenance, not as a
general installer or a prerequisite for contributors.
