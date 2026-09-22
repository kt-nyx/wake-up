# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Assemble a source-inclusive development/release bundle; never deploy or upload."""
from __future__ import annotations

import argparse
import ast
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile

REPO = Path(__file__).resolve().parents[1]
LEGAL = ("LICENSE", "LICENSE.md", "LICENSE-EXCEPTION.md", "LICENSE-DOCS", "NOTICE",
         "THIRD_PARTY_NOTICES.md", "CONTRIBUTING.md", "WORKSHOP_CONTRIBUTION_TERMS.md", "DCO",
         "third-party/StbImageSharp-LICENSE.txt", "third-party/StbImageSharp-PSD.md",
         "third-party/StbImageSharp-JPEG.md")
INPUT_FILES = {"About/About.xml", "Assemblies/WakeUp.dll"}
PRIVATE = {".rlo-test-instance", "artifacts", ".collector", "runs", ".git", "__pycache__"}


def require(value, message):
    if not value:
        raise ValueError(message)


def git(*args):
    return subprocess.check_output(["git", *args], cwd=REPO)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_source_path(name):
    path = PurePosixPath(name)
    return (not path.is_absolute() and ".." not in path.parts
            and "\\" not in name and ":" not in name
            and not PRIVATE.intersection(part.casefold() for part in path.parts)
            and path.suffix.lower() not in {".dll", ".exe", ".pdb", ".sav", ".rws"})


def source_zip(revision):
    entries = git("ls-tree", "-r", "-z", revision).split(b"\0")
    for entry in filter(None, entries):
        metadata, name = entry.split(b"\t", 1)
        require(metadata.startswith((b"100644 blob ", b"100755 blob ")),
                "Source tree contains a symlink or submodule")
        require(safe_source_path(name.decode()), "Private/binary path in source tree: " + name.decode())
    # Archive committed bytes, independent of the maintainer's Windows checkout
    # conversion. Otherwise even clean sources can acquire different hashes.
    return git("-c", "core.autocrlf=false", "archive", "--format=zip", revision)


def verify_input(package, receipt, revision):
    require(receipt.get("sourceRevision") == revision, "Build package does not match current source revision")
    require(not package.is_symlink(), "Symlink package is not supported")
    actual = set()
    for path in package.rglob("*"):
        require(not path.is_symlink(), "Symlink in build package")
        if path.is_file():
            actual.add(path.relative_to(package).as_posix())
    require(actual == INPUT_FILES, "Build package must contain only the reviewed About.xml and runtime DLL")
    rows = {row["path"]: row for row in receipt["files"] if row["kind"] == "file"}
    require(set(rows) == INPUT_FILES, "Unexpected build receipt contents")
    for name in INPUT_FILES:
        require(sha((package / name).read_bytes()) == rows[name]["sha256"], "Build file hash mismatch: " + name)


def about_bytes(product, template):
    root = ET.fromstring(template)
    for tag, field in (("name", "displayName"), ("packageId", "packageId"),
                       ("author", "author"), ("modVersion", "version"), ("url", "sourceUrl")):
        node = root.find(tag)
        require(node is not None, "Missing metadata field: " + tag)
        node.text = product[field]
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def preserved_helper_overlay(folder):
    """Reuse an already source-inclusive helper, without its older core DLL."""
    manifest_bytes = (folder / "release-manifest.json").read_bytes()
    manifest = json.loads(manifest_bytes)
    helper = dict(manifest["optionalTextureHelper"])
    require(helper["target"] == "win-x64" and helper["handshakePassed"] is True, "Unqualified preserved helper")
    source_revision = helper.get("preservedSourceRevision", manifest["sourceRevision"])
    source_archive = helper.get("matchingSourceArchive", "Source/source.zip")
    require(source_archive in {"Source/source.zip", "Source/TextureHelper/source.zip"}, "Unknown helper source archive")
    git("merge-base", "--is-ancestor", source_revision, "HEAD")
    expected = set(git("ls-tree", "-r", "--name-only", source_revision, "--",
                       "native/texture-helper", "scripts/build_texture_helper.py").decode().splitlines())
    require(set(helper["inputs"]) == expected and set(helper["sourceArchiveInputs"]) == expected,
            "Preserved helper source inventory mismatch")
    executable = "Tools/win-x64/WakeUp.TextureHelper.exe"
    names = {executable} | {"Notices/TextureHelper/" + Path(name).name for name in helper["inputs"]
                          if "/notices/" in name or name == "native/texture-helper/README.md"}
    files = {}
    for name in names | {source_archive}:
        path = folder / name
        require(not path.is_symlink() and path.resolve().is_relative_to(folder), "Unsafe preserved helper path")
        data = path.read_bytes()
        require(sha(data) == manifest["files"][name], "Preserved helper payload drift: " + name)
        if name != source_archive: files[name] = data
    require(sha(files[executable]) == helper["sha256"] and len(files[executable]) == helper["bytes"], "Preserved helper executable drift")
    output = io.BytesIO()
    with zipfile.ZipFile(folder / source_archive) as source, zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as target:
        for name in sorted(helper["inputs"]):
            require(safe_source_path(name), "Unsafe preserved helper source")
            data = source.read(name)
            require(sha(data) == helper["sourceArchiveInputs"][name]
                    and data == git("show", source_revision + ":" + name), "Preserved helper source drift: " + name)
            target.writestr(name, data)
    helper["preservedSourceRevision"] = source_revision
    helper["preservedManifestSha256"] = sha(manifest_bytes)
    helper["matchingSourceArchive"] = "Source/TextureHelper/source.zip"
    files[helper["matchingSourceArchive"]] = output.getvalue()
    return files, helper


def helper_overlay(folder):
    """Optional local build input; private build-host receipt never enters bundle."""
    require(not folder.is_symlink(), "Symlink helper package")
    folder = folder.resolve()
    if folder.is_relative_to((REPO / "artifacts/releases").resolve()):
        return preserved_helper_overlay(folder)
    require(folder.is_relative_to((REPO / "artifacts/s10-helper/package").resolve()), "Use the local pinned helper build")
    receipt = json.loads((folder / "helper-build.json").read_text())
    target = receipt.get("target")
    require(target in {"win-x64", "linux-x64"} and receipt.get("handshakePassed") is True, "Unqualified helper handshake/target")
    expected = set(git("ls-files", "native/texture-helper", "scripts/build_texture_helper.py").decode().splitlines())
    inputs = {name.replace("\\", "/"): digest for name, digest in receipt["inputs"].items()}
    require(set(inputs) == expected, "Helper source inventory mismatch")
    for name, digest in inputs.items():
        require(safe_source_path(name) and sha((REPO / name).read_bytes()) == digest, "Helper build input drift: " + name)
    executable = "Tools/" + target + "/WakeUp.TextureHelper" + (".exe" if target == "win-x64" else "")
    require(receipt["executable"].replace("\\", "/") == executable, "Unexpected helper executable")
    files = {}
    for path in folder.rglob("*"):
        require(not path.is_symlink(), "Symlink helper payload")
        if path.is_file() and path.name != "helper-build.json":
            files[path.relative_to(folder).as_posix()] = path.read_bytes()
    notices = {"Notices/TextureHelper/" + Path(name).name: (REPO / name).read_bytes()
               for name in inputs if "/notices/" in name or name == "native/texture-helper/README.md"}
    require(set(files) == set(notices) | {executable}, "Unexpected or missing helper package files")
    require(all(files[name] == data for name, data in notices.items()), "Helper notice drift")
    require(len(files[executable]) == receipt["bytes"] and sha(files[executable]) == receipt["sha256"], "Helper binary drift")
    summary = dict(target=target, sha256=receipt["sha256"], bytes=receipt["bytes"],
                   inputs=inputs, dependencies=receipt["dependencies"], handshakePassed=True,
                   unityRenderingQualified=False, livePreparationQualified=False, startupBenefitMeasured=False)
    return files, summary


def verify_helper_source_archive(helper, source):
    """Separate original build-input bytes from Git-normalized source bytes."""
    archived = {}
    converted = []
    with zipfile.ZipFile(io.BytesIO(source)) as contents:
        for name, digest in helper["inputs"].items():
            data = contents.read(name)
            built = (REPO / name).read_bytes()
            require(sha(built) == digest, "Helper input changed during packaging: " + name)
            require(data == built or data.replace(b"\r\n", b"\n") == built.replace(b"\r\n", b"\n"),
                    "Included helper source differs from verified build input: " + name)
            archived[name] = sha(data)
            if data != built:
                converted.append(name)
    helper["sourceArchiveInputs"] = archived
    helper["sourceArchiveLineEndingOnlyDifferences"] = sorted(converted)


def verify_reused_build(build_revision, revision):
    """Permit only documented packaging changes after the exact tested build."""
    require(bool(re.fullmatch(r"[0-9a-f]{40}", build_revision or "")), "Invalid build source revision")
    git("merge-base", "--is-ancestor", build_revision, revision)
    changed = git("diff", "--name-only", "--no-renames", "-z", build_revision, revision).decode().split("\0")
    packaging = {"AGENTS.md", "release/product.json", "scripts/package_release.py", "tests/test_package_release.py", "tests/test_fixture.py"}
    for name in filter(None, changed):
        if name == "release/About.xml":
            # Qualification prose does not alter the tested runtime. Keep the
            # dependency/compatibility contract exact when reusing its build.
            def about_contract(ref):
                root = ET.fromstring(git("show", ref + ":release/About.xml"))
                description = root.find("description")
                require(description is not None, "Missing release description")
                description.text = ""
                return ET.tostring(root, encoding="utf-8")
            require(about_contract(build_revision) == about_contract(revision),
                    "Reused build has changed release About contract beyond description")
            continue
        if name == "scripts/fixture.py":
            # Fixture preparation/inspection and exact comparison admission can evolve without rebuilding the
            # tested product. Everything outside these entry points stays exact,
            # including build(), reference_environment() and module constants.
            def build_tool_contract(ref):
                tree = ast.parse(git("show", ref + ":scripts/fixture.py").decode())
                tree.body = [node for node in tree.body if not (isinstance(node, ast.FunctionDef)
                             and node.name in {"prepare", "verify_prepared", "deploy_helper", "main",
                                               "bind_comparison_package", "gameplay_save_source"})]
                return ast.dump(tree, include_attributes=False)
            require(build_tool_contract(build_revision) == build_tool_contract(revision),
                    "Reused build has changed fixture build/tool inputs")
            continue
        require(name in packaging or (name.startswith("docs/") and name.endswith(".md"))
                or (name.startswith("validation/menu-observer/") and name.endswith(".cs")),
                "Reused build has changed non-documentation/packaging input: " + name)


def make_bundle(package, texture_helper=None, reuse_build=False):
    require(not git("status", "--porcelain", "--untracked-files=normal").strip(), "Commit or remove untracked release inputs first")
    revision = git("rev-parse", "HEAD").decode().strip()
    product = json.loads((REPO / "release/product.json").read_text(encoding="utf-8"))
    for key in ("folderName", "version"):
        require(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", product[key]), "Unsafe product " + key)
    require(re.fullmatch(r"[a-z0-9]+(?:\.[a-z0-9]+)+", product["packageId"]), "Invalid public package ID")
    require(not package.is_symlink(), "Symlink package is not supported")
    package = package.resolve()
    allowed = (REPO / "artifacts/fixture-package").resolve()
    require(package.is_relative_to(allowed), "Use a build from this checkout's package output")
    receipt = json.loads(package.with_suffix(".json").read_text(encoding="utf-8"))
    build_revision = receipt.get("sourceRevision")
    if reuse_build:
        verify_reused_build(build_revision, revision)
    verify_input(package, receipt, build_revision if reuse_build else revision)
    files = {"About/About.xml": about_bytes(product, (REPO / "release/About.xml").read_bytes()),
             "Assemblies/WakeUp.dll": (package / "Assemblies/WakeUp.dll").read_bytes(),
             "Source/source.zip": source_zip(revision),
             "Source/source-revision.txt": (revision + "\n").encode()}
    for name in LEGAL:
        files[name] = (REPO / name).read_bytes()
    helper = None
    if texture_helper is not None:
        overlay, helper = helper_overlay(texture_helper)
        if "matchingSourceArchive" not in helper:
            verify_helper_source_archive(helper, files["Source/source.zip"])
        files.update(overlay)
    manifest = dict(product, sourceRevision=revision, buildSourceRevision=build_revision,
                    buildReferenceTarget=receipt["referenceTarget"],
                    files={name: sha(data) for name, data in sorted(files.items())})
    if helper is not None:
        manifest["optionalTextureHelper"] = helper
    files["release-manifest.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    destination = REPO / "artifacts/releases" / (product["version"] + "-" + revision
        + ("-texture-helper-" + helper["target"] if helper else ""))
    require(not destination.exists(), "Release output already exists; retain it or commit a new revision")
    destination.mkdir(parents=True)
    folder = destination / product["folderName"]
    for name, data in files.items():
        target = folder / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    archive = destination / (product["folderName"] + "-" + product["version"] + ".zip")
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED) as output:
        for name, data in sorted(files.items()):
            output.writestr(product["folderName"] + "/" + name, data)
    with zipfile.ZipFile(archive) as output:
        require(output.testzip() is None, "ZIP integrity failure")
        for name, data in files.items():
            require(output.read(product["folderName"] + "/" + name) == data, "ZIP payload mismatch")
    return {"folder": str(folder), "zip": str(archive), "zipSha256": sha(archive.read_bytes()),
            "sourceRevision": revision, "buildSourceRevision": build_revision,
            "steamQualified": product["steamQualified"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--texture-helper", type=Path, help="Optional locally built helper overlay; never downloads or deploys")
    parser.add_argument("--reuse-build", action="store_true", help="Reuse an ancestor build only across verified documentation/packaging changes")
    options = parser.parse_args()
    try:
        print(json.dumps(make_bundle(options.package, options.texture_helper, options.reuse_build), indent=2))
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, str(error) + "\n")
