# Building the public source

These instructions apply to 0.4.1. See [current state](current-state.md) for source, package and qualification identities. Use the SDK pinned in global.json.

The source archive contains the corresponding implementation, tests and build
scripts. It does not contain game files, third-party mod binaries or the
maintainer's private fixture. You can build without creating that fixture.

Use Windows, Git, PowerShell 7, Python 3.11+ and the .NET SDK pinned in `global.json`.
Obtain the required game and Prepatcher references legally. The present build
example below selects the exact reviewed Steam rev590 game and Harmony binaries;
version labels alone are insufficient. Their hashes are in
`build/Verify-LocalReferences.ps1`. The explicit `linux-rev600` target also permits
the captured native Linux references for offline compilation. The
`gog-rev573` target supplies the 0.4.1 release build. Compilation targets
are not live platform qualifications; exact build references remain reproducibility details. Runtime platform support and the build
host are separate: these instructions still use Windows and PowerShell 7.
Do not weaken identity checks to make another build compile; see the
[Linux qualification limits](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/linux-support.md).

These exact references keep builds reproducible. They are separate from the
retained [runtime forward compatibility policy](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/forward-compatibility.md),
which permits future game revisions to attempt each feature's checks.

Texture supplier routing also checks `0PrepatcherAPI.dll` 1.2.0.0 from that same frozen
Prepatcher directory. Locked restore obtains build-only Krafs.Publicizer 2.3.0
to expose Harmony's embedded Cecil assembly-rewriting types to the compiler.
Only a reference copy under build output is altered; the original Harmony/API
binaries are not changed or packaged. Generated access attributes are part of
Wake-Up; see [third-party notices](../THIRD_PARTY_NOTICES.md). No additional game
dependency is installed. The existing core Linux fallback remains; neither 0.3.0 nor 0.4.0 has Linux live qualification. Native texture preparation requires
no helper. A Windows-helper release bundle
also includes the optional DDS export executable and matching source/notices.
Linux helper work remains deferred.

From a source checkout or extracted source archive, set the paths for your own
copies, then restore and build:

```powershell
$env:WAKE_UP_RIMWORLD_MANAGED_DIR = 'C:\your-game-copy\RimWorldWin64_Data\Managed'
$env:WAKE_UP_PREPATCHER_ASSEMBLIES_DIR = 'C:\your-prepatcher-copy\Assemblies'
$env:WAKE_UP_REFERENCE_TARGET = 'steam-rev590'
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test tests/WakeUp.Tests -c Release --no-build
python -m unittest discover -s tests -p 'test_*.py'
```

When running the optional XML Extensions adapter tests outside the private
fixture, set `WAKE_UP_XML_EXTENSIONS_ASSEMBLY` to a legally obtained
`XmlExtensions.dll`. The tests validate its method contracts; they do not require
copying it into a normal game installation.

These commands do not launch the game. Runtime output is under
`artifacts/bin/WakeUp/Release/net472/`.
Tests require the separately obtained references; no public CI runner is
expected to download or possess the game. Python packaging checks run without it.

Translation comparisons additionally require the .NET 8 runtime.
After building the Release product above, run `dotnet restore tests/WakeUp.Translation.Tests --locked-mode`
and `dotnet test tests/WakeUp.Translation.Tests -c Release --no-restore`.
This separate project executes the unmodified native translation methods with a
test-only CoreCLR Harmony 2.4.2 dependency; the product still builds against the
frozen Prepatcher reference. It is not part of the ordinary net472 suite. See
[S03 host differences and limits](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/s03-translations.md#offline-host-limitation)
before interpreting these checks as game-runtime evidence.

To check compilation against the reviewed Linux game without changing the
fixture, set `WAKE_UP_RIMWORLD_MANAGED_DIR` to a private copy of its
`RimWorldLinux_Data/Managed` directory, set the Prepatcher path as above, and
select `WAKE_UP_REFERENCE_TARGET=linux-rev600`. Build only
`src/WakeUp/WakeUp.csproj`; the Windows fixture observer is not a Linux target.
Use an absolute `-p:OutputPath=...` under `artifacts/` to keep this output separate
from the ordinary build. The ordinary fixture suite uses the fixture's selected exact reference contract;
compiling Linux references on Windows is not a Linux execution test.

The package's `Source/source.zip` carries the actual corresponding source,
including these instructions, pinned dependencies, project files and scripts.
`Source/source-revision.txt` records the packaging/source revision. The manifest's
`buildSourceRevision` records the original DLL build revision; these differ when
an unchanged tested DLL is reused across documentation or packaging edits. When
rebuilding that archive without its Git history, pass
`-p:SourceRevisionId=<buildSourceRevision>` to `dotnet build` to preserve
the informational version. A public snapshot commit can differ from that build
revision; neither implies a different source file automatically.

The maintainer's `scripts/fixture.py` controls an existing private fixture
at a fixed path. It is included for transparency and maintenance, not as a
general installer or a prerequisite for contributors. The archive also contains
observer and optional texture-helper source, tests and historical evidence
reports. These are development materials, not additional runtime components.
Private fixture data, game/third-party binaries, raw local evidence and the
observer/test binaries are excluded. An optional Windows-helper bundle carries
the separately verified helper executable outside this source archive. Build the core project above to produce
the product; do not install a development observer alongside it.

## Optional Windows export helper

The pinned helper source is built separately by `scripts/build_texture_helper.py`
using the documented existing Windows toolchain and dependencies. Read the
[R2 source and protocol report](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r2-texture-preparation.md)
for exact inputs and checks, and the [release procedure](release.md) for the
`--texture-helper` packaging overlay. The helper's matching source, executable,
notices and metadata travel together. Native preservation and core loading remain
available without it. This helper exports portable DDS files; it does not replace
original images or automatically substitute quality-changing textures in game.
