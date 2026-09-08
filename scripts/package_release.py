# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Assemble a source-inclusive development/release bundle; never deploy or upload."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile

REPO = Path(__file__).resolve().parents[1]
LEGAL = ("LICENSE", "LICENSE.md", "LICENSE-EXCEPTION.md", "LICENSE-DOCS", "NOTICE",
         "THIRD_PARTY_NOTICES.md", "CONTRIBUTING.md", "WORKSHOP_CONTRIBUTION_TERMS.md", "DCO")
INPUT_FILES = {"About/About.xml", "Assemblies/RimWorldLoadingOptimizer.RimWorld.dll"}
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
            and not PRIVATE.intersection(path.parts)
            and path.suffix.lower() not in {".dll", ".exe", ".pdb", ".sav", ".rws"})


def source_zip(revision):
    entries = git("ls-tree", "-r", "-z", revision).split(b"\0")
    for entry in filter(None, entries):
        metadata, name = entry.split(b"\t", 1)
        require(metadata.startswith((b"100644 blob ", b"100755 blob ")),
                "Source tree contains a symlink or submodule")
        require(safe_source_path(name.decode()), "Private/binary path in source tree: " + name.decode())
    return git("archive", "--format=zip", revision)


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


def make_bundle(package):
    require(not git("status", "--porcelain", "--untracked-files=normal").strip(), "Commit or remove untracked release inputs first")
    revision = git("rev-parse", "HEAD").decode().strip()
    product = json.loads((REPO / "release/product.json").read_text(encoding="utf-8"))
    for key in ("folderName", "version"):
        require(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", product[key]), "Unsafe product " + key)
    require(re.fullmatch(r"[a-z0-9]+(?:\.[a-z0-9]+)+", product["packageId"]), "Invalid public package ID")
    package = package.resolve()
    allowed = (REPO / "artifacts/op7-fixture-package").resolve()
    require(package.is_relative_to(allowed), "Use a build from this checkout's package output")
    receipt = json.loads(package.with_suffix(".json").read_text(encoding="utf-8"))
    verify_input(package, receipt, revision)
    files = {"About/About.xml": about_bytes(product, (REPO / "release/About.xml").read_bytes()),
             "Assemblies/RimWorldLoadingOptimizer.RimWorld.dll": (package / "Assemblies/RimWorldLoadingOptimizer.RimWorld.dll").read_bytes(),
             "Source/source.zip": source_zip(revision),
             "Source/source-revision.txt": (revision + "\n").encode()}
    for name in LEGAL:
        files[name] = (REPO / name).read_bytes()
    manifest = dict(product, sourceRevision=revision,
                    buildReferenceTarget=receipt["referenceTarget"],
                    files={name: sha(data) for name, data in sorted(files.items())})
    files["release-manifest.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    destination = REPO / "artifacts/releases" / (product["version"] + "-" + revision)
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
            "sourceRevision": revision, "steamQualified": product["steamQualified"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    options = parser.parse_args()
    try:
        print(json.dumps(make_bundle(options.package), indent=2))
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, str(error) + "\n")
