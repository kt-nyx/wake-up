"""Build/package the optional S10 helper using installed tooling and locked sources.

Downloads are explicit developer --fetch work only; no installer or runtime network.
Linux build inputs are provided but must be qualified on an available Linux host.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "native/texture-helper"
WORK = ROOT / "artifacts/s10-helper"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(args: list[str | Path], **kwargs) -> str:
    result = subprocess.run([str(a) for a in args], check=False, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, **kwargs)
    print(result.stdout, end="")
    result.check_returncode()
    return result.stdout


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fetch", action="store_true", help="Explicitly fetch locked dependency sources")
    parser.add_argument("--cmake", type=Path, help="Existing CMake executable")
    args = parser.parse_args()
    system = platform.system()
    if system not in ("Windows", "Linux") or platform.machine().lower() not in ("amd64", "x86_64"):
        raise SystemExit("Only native Windows x64/Linux x64 build hosts are admitted")
    target = "win-x64" if system == "Windows" else "linux-x64"
    vs = None
    if system == "Windows":
        vswhere = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Microsoft Visual Studio/Installer/vswhere.exe"
        vs = Path(subprocess.check_output([str(vswhere), "-latest", "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath"], text=True).strip())
        if not vs.is_dir():
            raise SystemExit("No installed Visual Studio C++ toolchain; no tooling was installed")
    cmake = args.cmake or shutil.which("cmake")
    if not cmake and vs:
        cmake = vs / "Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe"
    if not cmake or not Path(cmake).is_file():
        raise SystemExit("An existing CMake executable is required; no tooling was installed")
    WORK.mkdir(parents=True, exist_ok=True)
    locked = json.loads((NATIVE / "dependencies.json").read_text())
    used = locked if system == "Linux" else locked[:2]
    sources = {}
    for spec in used:
        archive = WORK / (spec["name"] + ".zip")
        if not archive.exists():
            if not args.fetch:
                raise SystemExit(f"Missing locked archive {archive}; developer --fetch required")
            urllib.request.urlretrieve(spec["url"], archive)
        if sha(archive) != spec["sha256"]:
            raise SystemExit(f"Archive hash mismatch: {spec['name']}")
        with zipfile.ZipFile(archive) as content:
            for item in content.infolist():
                destination = (WORK / "sources" / item.filename).resolve()
                if not destination.is_relative_to((WORK / "sources" / spec["folder"]).resolve()):
                    raise SystemExit("Archive entry escaped its pinned directory")
                if (item.external_attr >> 16) & 0o170000 == 0o120000:
                    raise SystemExit("Dependency archive symlink refused")
            content.extractall(WORK / "sources")
        sources[spec["name"]] = WORK / "sources" / spec["folder"]
    build = WORK / ("build-" + target)
    prefix = WORK / ("prefix-" + target)
    common = [f"-DCMAKE_INSTALL_PREFIX={prefix}", f"-DCMAKE_PREFIX_PATH={prefix}", "-DCMAKE_INSTALL_LIBDIR=lib", "-DBUILD_SHARED_LIBS=OFF"]
    if system == "Linux":
        common.append("-DCMAKE_BUILD_TYPE=Release")
    logs = []
    if system == "Linux":
        # Static image dependencies; portable baseline without optional SIMD tools.
        dependencies = [
            ("DirectXMath", []),
            ("DirectX-Headers", ["-DDXHEADERS_BUILD_TEST=OFF", "-DDXHEADERS_BUILD_GOOGLE_TEST=OFF"]),
            ("zlib", ["-DZLIB_BUILD_SHARED=OFF", "-DZLIB_BUILD_TESTING=OFF"]),
            ("libpng", ["-DPNG_SHARED=OFF", "-DPNG_TESTS=OFF", "-DPNG_TOOLS=OFF", f"-DZLIB_ROOT={prefix}"]),
            ("libjpeg-turbo", ["-DENABLE_SHARED=OFF", "-DWITH_TOOLS=OFF", "-DWITH_TESTS=OFF", "-DWITH_TURBOJPEG=OFF", "-DWITH_SIMD=OFF"]),
        ]
        for name, options in dependencies:
            depbuild = WORK / ("build-linux-" + name)
            logs.append(run([cmake, "-S", sources[name], "-B", depbuild, *common, *options]))
            logs.append(run([cmake, "--build", depbuild, "--parallel", "1"]))
            logs.append(run([cmake, "--install", depbuild]))
    config = [cmake, "-S", NATIVE, "-B", build, *common,
              f"-DDIRECTXTEX_SOURCE={sources['DirectXTex']}", f"-DDIRECTXMATH_SOURCE={sources['DirectXMath']}"]
    if system == "Windows":
        config += ["-A", "x64"]
    logs.append(run(config))
    logs.append(run([cmake, "--build", build, "--config", "Release", "--target", "wake-up-texture-helper", "--parallel", "1"]))
    exe = build / ("Release/wake-up-texture-helper.exe" if system == "Windows" else "wake-up-texture-helper")
    package = WORK / "package" / target
    tool = package / "Tools" / target / ("WakeUp.TextureHelper.exe" if system == "Windows" else "WakeUp.TextureHelper")
    tool.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(exe, tool)
    shutil.copytree(NATIVE / "notices", package / "Notices" / "TextureHelper", dirs_exist_ok=True)
    shutil.copy2(NATIVE / "README.md", package / "Notices" / "TextureHelper" / "README.md")
    if system == "Windows":
        toolset = sorted((vs / "VC/Tools/MSVC").iterdir())[-1]
        imports = run([toolset / "bin/Hostx64/x64/dumpbin.exe", "/dependents", tool])
    else:
        imports = run(["ldd", tool])
        imports += run(["readelf", "--version-info", tool])
    # A real handshake followed by an intentionally invalid request proves the
    # binary executes under its private memory limit; it is no rendering claim.
    probe = subprocess.run([str(tool), "--stdio-v2"], input=b"invalid!", capture_output=True, timeout=10)
    if probe.returncode != 1 or not probe.stdout.startswith(b"WUTXHELPER000002srgb-color-box-straight-alpha-bc1-bc3-v2WUTXRES2"):
        raise SystemExit("Built helper handshake failed")
    quality_probe = subprocess.run([str(tool), "--stdio-v3"], input=b"invalid!", capture_output=True, timeout=10)
    if quality_probe.returncode != 1 or not quality_probe.stdout.startswith(b"WUTXHELPER000003srgb-canonical-metadata-area-alpha-bc-rgba-psd-v4WUTXRES3"):
        raise SystemExit("Built quality helper handshake failed")
    raw_probe = subprocess.run([str(tool), "--stdio-raw-v1"], input=b"", capture_output=True, timeout=10) if system == "Windows" else None
    if raw_probe is not None and (raw_probe.returncode != 0 or raw_probe.stdout != b"WUTXRAWHELP00001" + (96 * 1024 * 1024).to_bytes(4, "little")):
        raise SystemExit("Built raw helper handshake failed")
    receipt = {
        "schema": 1, "target": target, "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "trackedSourceMayBeDirty": bool(subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=no"], cwd=ROOT, text=True).strip()),
        "inputs": {str(p.relative_to(ROOT)): sha(p) for p in sorted([*NATIVE.rglob("*"), Path(__file__).resolve()]) if p.is_file() and "__pycache__" not in p.parts},
        "dependencies": used, "executable": str(tool.relative_to(package)), "sha256": sha(tool), "bytes": tool.stat().st_size,
        "packageBytes": sum(p.stat().st_size for p in package.rglob("*") if p.is_file() and p.name != "helper-build.json"),
        "handshakePassed": True, "renderingQualified": False, "buildHost": platform.platform(), "imports": imports,
        "protocol": "stdio-v2", "outputContract": "srgb-color-box-straight-alpha-bc1-bc3-v2",
        "qualityProtocol": "stdio-v3", "qualityOutputContract": "srgb-canonical-metadata-area-alpha-bc-rgba-psd-v4",
        "qualityHandshakePassed": True, "rawProtocol": "stdio-raw-v1" if raw_probe is not None else None,
        "rawHandshakePassed": raw_probe is not None,
    }
    (package / "helper-build.json").write_text(json.dumps(receipt, indent=2) + "\n")
    (WORK / ("build-" + target + ".log")).write_text("\n".join(logs))
    print(json.dumps({"package": str(package), "executable": str(tool), "sha256": receipt["sha256"], "bytes": receipt["bytes"]}))


if __name__ == "__main__":
    main()
