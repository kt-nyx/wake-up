# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
"""Create an unpublished multi-version testing package; never installs or uploads."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
import zipfile

from package_release import require, safe_source_path, source_zip

REPO = Path(__file__).resolve().parents[1]


def digest(data):
    return hashlib.sha256(data).hexdigest()


def metadata(legacy_xml, candidate_xml):
    """Game-version selection changes both code and dependencies."""
    legacy = ET.fromstring(legacy_xml)
    candidate = ET.fromstring(candidate_xml)
    root = copy.deepcopy(legacy)
    root.find("modVersion").text = candidate.findtext("modVersion")
    versions = root.find("supportedVersions")
    versions.clear()
    for version in ("1.6", "1.7"):
        ET.SubElement(versions, "li").text = version
    for base, versioned in (("modDependencies", "modDependenciesByVersion"),
                            ("loadAfter", "loadAfterByVersion"),
                            ("incompatibleWith", "incompatibleWithByVersion")):
        existing = root.find(versioned)
        if existing is not None:
            root.remove(existing)
        overrides = ET.SubElement(root, versioned)
        for version, source in (("1.6", legacy), ("1.7", candidate)):
            branch = ET.SubElement(overrides, "v" + version)
            node = source.find(base)
            if node is not None:
                for item in node:
                    branch.append(copy.deepcopy(item))
    overrides = ET.SubElement(root, "descriptionsByVersion")
    ET.SubElement(overrides, "v1.7").text = candidate.findtext("description")
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def load_folders():
    return b'''<?xml version="1.0" encoding="utf-8"?>
<loadFolders>
  <v1.6><li>/</li><li>1.6</li></v1.6>
  <v1.7><li>1.7</li></v1.7>
</loadFolders>
'''


def legacy_payload(folder, expected):
    manifest_bytes = (folder / "release-manifest.json").read_bytes()
    manifest = json.loads(manifest_bytes)
    if "preserved16ManifestSha256" in expected:
        require(digest(manifest_bytes) == expected["preserved16ManifestSha256"], "Unreviewed preserved manifest")
    require(manifest["version"] == expected["preserved16Version"], "Wrong preserved 1.6 release")
    payload = {}
    mapping = {}
    for name, sha in manifest["files"].items():
        require(safe_source_path(name) or name in {
            "Assemblies/WakeUp.dll", "Tools/win-x64/WakeUp.TextureHelper.exe"}, "Unsafe legacy package path")
        path = folder / name
        require(not path.is_symlink() and path.resolve().is_relative_to(folder.resolve()), "Linked legacy payload")
        data = path.read_bytes()
        require(digest(data) == sha, "Changed preserved file: " + name)
        destination = {"Assemblies/WakeUp.dll": "1.6/Assemblies/WakeUp.dll",
                       "Source/source.zip": "Source/1.6/source.zip",
                       "Source/source-revision.txt": "Source/1.6/source-revision.txt"}.get(name, name)
        mapping[name] = destination
        payload[destination] = data
    require(digest(payload["1.6/Assemblies/WakeUp.dll"]) == expected["preserved16DllSha256"], "Unreviewed 1.6 runtime")
    payload["Source/1.6/release-manifest.json"] = manifest_bytes
    # Artwork is optional and is never uploaded or changed by this script.
    preview = folder / "About/Preview.png"
    if preview.is_file():
        require(not preview.is_symlink() and preview.resolve().is_relative_to(folder.resolve()), "Linked preview")
        payload["About/Preview.png"] = preview.read_bytes()
    return payload, manifest, mapping


def build(legacy, receipt_path, output, revision):
    expected = json.loads((REPO / "release/1.7/product.json").read_text())
    receipt = json.loads(receipt_path.read_text())
    runtime = Path(receipt["dll"])
    require(digest(runtime.read_bytes()) == receipt["dllSha256"], "Changed 1.7 build")
    require(receipt["prepatcherFree"] is True, "1.7 assembly references not verified")
    require(receipt["buildSourceRevision"] == revision, "Build and source revision differ")
    # Archive immutable committed bytes, never user edits or private game files.
    source = source_zip(revision)
    payload, original, mapping = legacy_payload(legacy, expected)
    payload["About/About.xml"] = metadata(payload["About/About.xml"], (REPO / "release/1.7/About.xml").read_bytes())
    payload["LoadFolders.xml"] = load_folders()
    payload["1.7/Assemblies/WakeUp17.dll"] = runtime.read_bytes()
    payload["Source/source.zip"] = source
    payload["Source/source-revision.txt"] = (revision + "\n").encode()
    payload["TESTING.txt"] = (
        "Private Wake-Up 1.7 candidate, not an official release.\n"
        "No live game tests or performance qualification have been performed.\n"
        "1.6: unchanged released 0.4.2 runtime, original matching source and helper.\n"
        "1.7: standalone .NET 10 search runtime; Harmony 3 required, Prepatcher unsupported.\n"
        "Do not deploy or launch until the owner authorizes isolated in-game testing.\n"
        "Read Source/source.zip: docs/implementation-1.7.md and docs/testing-1.7.md.\n"
    ).encode()
    manifest = dict(expected, sourceRevision=revision, buildSourceRevision=receipt["buildSourceRevision"],
                    runtime17=receipt, preserved16=dict(version=original["version"],
                    sourceRevision=original["sourceRevision"], files=mapping),
                    files={name: digest(data) for name, data in sorted(payload.items())})
    payload["release-manifest.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    require(output.resolve().is_relative_to((REPO / "artifacts").resolve()), "Output must stay under ignored artifacts")
    require(not output.exists(), "Output already exists; choose a fresh candidate directory")
    for name, data in payload.items():
        destination = output / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    archive = output.parent / ("wake-up-" + expected["version"] + ".zip")
    require(not archive.exists(), "Archive already exists")
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as package:
        for name, data in sorted(payload.items()):
            package.writestr("wake-up/" + name, data)
    result = dict(folder=str(output), zip=str(archive), zipSha256=digest(archive.read_bytes()),
                  dll17Sha256=receipt["dllSha256"], dll16Sha256=expected["preserved16DllSha256"],
                  sourceRevision=revision, installed=False, published=False, liveTestsPerformed=False)
    (output.parent / "candidate-receipt.json").write_text(json.dumps(result, indent=2) + "\n")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--legacy-package", required=True, type=Path)
    parser.add_argument("--build-receipt", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip()
    print(json.dumps(build(args.legacy_package, args.build_receipt, args.output, revision), indent=2))
