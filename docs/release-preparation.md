# Release preparation

The product is ready for release packaging, not yet qualified for a Steam
Workshop launch. Final user-facing copy and preview art remain pending.
This preparation does not publish a binary or create a Workshop
item. The source repository may be public independently of binary qualification.

## Product identity

`release/product.json` owns the release version, display name, folder and source
URL. `release/About.xml` is the descriptive metadata template. The packager fills
the corresponding fields from the JSON so a later rename does not require
changing the tested runtime's namespaces, assembly name, settings type, cache
paths, diagnostic names or patch-owner identifiers.

The template leaves generated identity fields empty. Edit the JSON values, not
duplicate copies of them in XML. The short in-game display label is `Wake-Up`,
defined in `WakeUpMod`. The formal title is **Wake-Up: Loading Optimizations**.
The DLL's stable
assembly version and the distribution version are separate identifiers.

The public package ID is `kt.nyx.wakeup`. Keep this ID stable after publication.
The repository and distribution folder use `wake-up`; the assembly and C#
namespace use `WakeUp`. The private fixture keeps
its existing local validation ID and two-file contract. Release metadata flags
that old package and the pre-rename `kt.nyx.startupfixes` bundle as incompatible:
remove/disable either old copy before
installing the public package. This is a menu warning, not an automatic migration
or duplicate-assembly protection. Never install both copies together.

## Packaging

Commit the intended inputs, run the existing offline checks, and build once:

```powershell
python scripts/fixture.py test
python scripts/fixture.py build
python scripts/package_release.py --package artifacts/fixture-package/<revision>
```

The last command requires a clean tracked tree and the package for the current
revision. It verifies the build receipt and exact two-file input, copies the
runtime DLL unchanged, applies release metadata, includes licenses/notices and
the full current tracked source as `Source/source.zip`, and writes a ZIP plus
a public manifest of hashes under `artifacts/releases/<version>-<revision>/`.
It rejects symlinks, private fixture paths and unexpected input files. It never
ships the fixture's private build receipt, game references or observer DLL.
There is no upload step. Inspect the generated folder before manual distribution.

The generated public manifest records source and package identities and marks
Steam qualification pending. The source archive contains only the current tree,
not Git history. This avoids dependence on an external source offer or a link
remaining available. Include the source archive and legal material in Workshop
uploads too; do not upload just the DLL.

## GitHub source

The initial public `main` is a parentless snapshot of the reviewed current tree.
Private investigation history remains local. Public commits use the GitHub
no-reply identity; source snapshot contents are the same tracked files as the
local release-preparation commit. The local `codex/public-source` branch tracks
public `main`; local `main` retains the investigation lineage. Do not push local
`main`, `--all`, tags or a mirror to this remote.

For later synchronization, fetch public `main`, review any remote changes, and
update the public branch with the intended local source tree using the same
checkout. Commit with `git -c core.hooksPath=.githooks commit`, then push only
`codex/public-source:main`. Restore local `main` afterward. Do not force-push over
external contributions. Published source tags belong to the public history.

## Licensing decision implemented

The approved GPL-3.0-or-later direction is now an actual grant, with a separate
section-7 linking permission, CC BY-SA 4.0 for original documentation/artwork,
attribution, DCO and explicit platform contribution consent. The current GNU,
Creative Commons, RimWorld EULA and Steam section 6 texts were checked when
preparing these files. The old archived licensing document is historical, not
an additional approval gate. This work is not an independent legal opinion.

No third-party implementation library is bundled. Runtime adapters call the
installed supplier originals; notices distinguish those dependencies from
project material. Incoming copied code still needs its own provenance and
permission review; our exception cannot extend another author's rights.

## Remaining release work

1. Qualify the exact intended Steam game/dependency environment and the generated
   public package, including ordinary activation, settings and focused gameplay.
2. Finalize the README, Workshop description and preview art. Preserve the
   disclaimer in NOTICE on or alongside the published content.
3. Set the release version/status and `steamQualified` only from actual evidence;
   review supported versions and feature limitations against that evidence.
4. Package that revision, publish a matching source tag, and manually upload the
   inspected mod folder with the bundled source and notices. Record the assigned
   Workshop item ID privately until publication; never invent one in advance.

The current development bundle is not a Workshop-ready compatibility claim.

## Initial preparation verification, 2026-09-08

The `765dff45b29e9bc239990489938267d277631e57` build and source-inclusive
`0.1.0-dev` bundle were produced successfully. The DLL is unchanged between
the input build and release folder; the packager checked every ZIP entry against
its intended bytes. Offline verification passed 79 managed checks with one
supplier-dependent identity check skipped, plus all 35 Python checks. The
Character Editor supplier remains intentionally excluded from the private
fixture. No game was launched and the earlier deployed fixture was preserved.

The canonical CC legal text ends with a blank line; it is retained verbatim
despite Git's end-of-file whitespace warning. This is not an edited license.

## Cleanup verification, 2026-09-08

The later `f14ec1bf5575f4150b612720c86f67cad137bf76` build includes shared
diagnostic writing, independent feature setup and documentation consolidation.
It passed 81 managed checks with the same optional supplier skip, all 35 Python
checks, and a build with zero warnings/errors. All 41 tracked Markdown files
decoded correctly and their 262 local links resolved to tracked material.
The source-inclusive bundle was regenerated and verified; its exact identity
is in [archived checkpoints](archive/development-checkpoints.md#final-pre-rename-cleanup-bundle).
No deployment or live test occurred.

## Wake-Up rename verification, 2026-09-08

The `30f005188ee17ac505845344f7a3541235cc4dd5` source revision builds `WakeUp.dll`
and a `wake-up` distribution folder with the formal title, public package ID,
new source URL and matching source archive. Project references, test namespaces,
settings identity, diagnostic/cache paths, patch owners, build variables and
development selectors use the new naming. The fixture tool is `scripts/fixture.py`.

All 81 managed and 35 Python checks passed, with one optional supplier check
skipped. The runtime and independent observer built without warnings/errors;
the public ZIP passed payload verification. All tracked Markdown links resolved.
[Current state](current-state.md) records the package hashes. No deployment,
game launch or new Steam qualification occurred. The fixed private fixture
contracts and historical evidence retain the original spellings described in
[development](development.md#naming-and-retained-historical-identities).
