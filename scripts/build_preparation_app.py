"""Build the optional portable Windows preparation window; never launch its UI."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

import fixture as f

PROJECT = f.REPO / "src/WakeUp.Preparation/WakeUp.Preparation.csproj"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Compile only; permits unfinished source")
    parser.add_argument("--update-lock", action="store_true", help="Developer-only initial dependency lock update")
    args = parser.parse_args()
    with f.fixture_lock(f.CANONICAL):
        f.guard_operation_processes(f.CANONICAL, "build")
        revision = f.git("rev-parse", "HEAD").strip()
        if not args.check:
            f.require(not f.git("status", "--porcelain"), "Commit source before packaging")
        def run(command):
            subprocess.run(command, cwd=f.REPO, check=True)
        run(["dotnet", "restore", str(PROJECT), "--force-evaluate" if args.update_lock else "--locked-mode"])
        run(["dotnet", "build", str(PROJECT), "-c", "Release", "--no-restore", "-m:1",
             "-p:SourceRevisionId=" + revision])
        if args.check:
            return
        output = f.REPO / "artifacts/preparation-package" / revision
        f.require(not output.exists(), "Package revision already exists; preserve its evidence")
        app = output / "WakeUp.Preparation"
        run(["dotnet", "publish", str(PROJECT), "-c", "Release", "--no-restore", "-m:1",
             "-p:SourceRevisionId=" + revision, "-o", str(app)])
        helper = f.REPO / "artifacts/s10-helper/package/win-x64"
        receipt = f.read(helper / "helper-build.json")
        f.require(receipt.get("qualityHandshakePassed") is True and receipt.get("sourceCommit") == revision
                  and not receipt.get("trackedSourceMayBeDirty"), "Rebuild the helper from this clean revision")
        f.require(f.digest(helper / f.HELPER_PATH) == receipt["sha256"] == f.C08_HELPER_SHA, "Helper identity drift")
        for relative, expected in receipt["inputs"].items():
            f.require(f.digest(f.REPO / relative) == expected, "Helper input drift: " + relative)
        shutil.copytree(helper / "Tools", app / "Tools")
        shutil.copytree(helper / "Notices", app / "Notices")
        shutil.copy2(helper / "helper-build.json", app / "Notices/TextureHelper/helper-build.json")
        for name in ("LICENSE", "LICENSE-EXCEPTION.md"):
            shutil.copy2(f.REPO / name, app / name)
        config = f.read(app / "WakeUp.Preparation.runtimeconfig.json")["runtimeOptions"]
        frameworks = {row["name"]: row["version"] for row in config.get("includedFrameworks", [])}
        f.require(frameworks == {"Microsoft.NETCore.App": "10.0.12", "Microsoft.WindowsDesktop.App": "10.0.12"},
                  "Expected bundled runtime closure; machine framework references are not allowed")
        for name in ("coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "System.Windows.Forms.dll",
                     "System.Private.CoreLib.dll", "WakeUp.Preparation.exe"):
            f.require((app / name).is_file(), "Missing standalone runtime file: " + name)
        nuget = Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages")))
        for package, filenames in (
                ("microsoft.netcore.app.runtime.win-x64", ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT")),
                ("microsoft.windowsdesktop.app.runtime.win-x64", ("LICENSE",))):
            source = nuget / package / "10.0.12"
            notices = app / "Notices" / package
            notices.mkdir(parents=True)
            for filename in filenames:
                matches = [p for p in source.iterdir() if p.name.upper() == filename]
                f.require(len(matches) == 1, "Missing bundled runtime notice: " + package + "/" + filename)
                shutil.copy2(matches[0], notices / filename)
        sources = output / "Source"
        relatives = ["Directory.Build.props", "global.json", "LICENSE", "LICENSE-EXCEPTION.md",
                     "scripts/build_preparation_app.py"]
        relatives += f.git("ls-files", "src/WakeUp.Preparation").splitlines()
        for node in ET.parse(PROJECT).getroot().findall(".//Compile"):
            relatives.append(str((PROJECT.parent / node.attrib["Include"].replace("\\", "/")).resolve().relative_to(f.REPO)))
        for relative in sorted(set(relatives)):
            target = sources / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(f.REPO / relative, target)
        # Helper source and locked upstream references accompany the executable.
        for relative in f.git("ls-files", "native/texture-helper", "scripts/build_texture_helper.py").splitlines():
            target = sources / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(f.REPO / relative, target)
        readme = ("Wake-Up Texture Preparation\n\n"
                  "Run WakeUp.Preparation/WakeUp.Preparation.exe. Select an existing game and ModsConfig.xml, "
                  "then the corresponding save-data folder. Scan, choose sources and prepare. Restart the game "
                  "to apply complete validated selections. Originals and mod order remain unchanged.\n\n"
                  "This Windows x64 package includes .NET 10.0.12 and the image helper. No separate .NET "
                  "installation is needed. The operating system must support .NET 10: "
                  "https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md\n"
                  "GOG revision 573 is the currently qualified game identity. Other builds cannot prepare. "
                  "Native preservation requires an actual game capture; converted outputs require the game's "
                  "resolved consumer checks before use. Unknown roles retain native output.\n\n"
                  "Included Source contains this app, its shared storage/encoder code and helper source. "
                  "Notices contains bundled runtime and helper licensing. No updater or system installation.\n")
        (output / "README.txt").write_text(readme, encoding="utf-8")
        files = f.inventory(output)
        manifest = {"schema": "wake-up-preparation-package.v1", "sourceCommit": revision,
                    "runtimeFrameworks": frameworks, "helperSha256": receipt["sha256"],
                    "bytes": sum(row.get("bytes", 0) for row in files), "files": files,
                    "functionalQualified": False, "visibleInspectionPassed": False}
        f.write(output / "package.json", manifest)
        print(json.dumps({"package": str(output), "app": str(app / "WakeUp.Preparation.exe"),
                          "bytes": manifest["bytes"], "sourceCommit": revision}, indent=2))


if __name__ == "__main__":
    main()
