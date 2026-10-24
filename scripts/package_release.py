#!/usr/bin/env python3
"""Create the verified binary-only package consumed by the built-in updater."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import tempfile
import xml.etree.ElementTree as ET
import zipfile

REPOSITORY = "TechSavage1989/SwitchPort_Diagram_Builder"
PRODUCT = "HUBTechDiagramBuilder"
EXECUTABLE = PRODUCT + ".exe"
ZIP_MEMBERS = frozenset((EXECUTABLE, "READ_ME.txt", "SHA256SUMS.txt", "UPDATE_INFO.txt"))
MAX_EXE_BYTES = 512 * 1024 * 1024
MAX_DOC_BYTES = 2 * 1024 * 1024


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def parse_version(value: str) -> str:
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", value):
        raise ValueError("Release version must be a stable three-part version such as 1.12.0.")
    if any(int(part) > 65535 for part in value.split(".")):
        raise ValueError("Release version is outside Windows version-resource limits.")
    return value


def project_version(root: Path) -> str:
    project = root / "Source/HUBTechDiagramBuilder/HUBTechDiagramBuilder.csproj"
    values = [node.text or "" for node in ET.parse(project).iter("Version")]
    if len(values) != 1:
        raise ValueError("The application project must declare exactly one Version.")
    return parse_version(values[0])


def check_gui_executable(path: Path) -> None:
    size = path.stat().st_size
    if size < 1024 or size > MAX_EXE_BYTES:
        raise ValueError("Published executable size is outside the accepted limits.")
    with path.open("rb") as stream:
        header = stream.read(64)
        if header[:2] != b"MZ":
            raise ValueError("Published executable has no Windows MZ header.")
        pe_offset = struct.unpack_from("<I", header, 0x3C)[0]
        if pe_offset < 64 or pe_offset > size - 96:
            raise ValueError("Published executable has an invalid PE offset.")
        stream.seek(pe_offset)
        pe = stream.read(96)
    if pe[:4] != b"PE\0\0" or struct.unpack_from("<H", pe, 4)[0] != 0x8664:
        raise ValueError("Published executable is not a Windows x64 PE file.")
    if struct.unpack_from("<H", pe, 24)[0] != 0x20B:
        raise ValueError("Published executable is not PE32+.")
    if struct.unpack_from("<H", pe, 24 + 68)[0] != 2:
        raise ValueError("Published executable is not a Windows GUI application.")


def read_me(root: Path, version: str) -> bytes:
    licenses = root / "Source/Licenses"
    if not all((licenses / name).is_file() for name in ("SkiaSharp.txt", "DejaVu-Fonts.txt")):
        raise ValueError("Required third-party license notices are missing.")
    text = f"""HUB Tech Network Diagram Builder {version} - Windows 10/11 x64

Run HUBTechDiagramBuilder.exe. Keep the executable in a writable folder.
Microsoft Edge WebView2 Runtime must be installed. No separate .NET installation
or companion application files are required.

Open Updates in the application to check, download or install a newer release.
Automatic checks are enabled initially; download and install remain your choice.
Install and restart waits until report generation is idle. Existing output,
application preferences and report logos stay in your local application data.

Development, complete source and releases:
https://github.com/{REPOSITORY}

THIRD-PARTY NOTICES
===================
"""
    seen = set()
    for path in sorted(licenses.glob("*.txt")):
        notice = path.read_text(encoding="utf-8-sig")
        if notice not in seen:
            text += "\n" + path.name + "\n" + "-" * len(path.name) + "\n" + notice + "\n"
            seen.add(notice)
    result = text.encode("utf-8")
    if len(result) > MAX_DOC_BYTES:
        raise ValueError("Release notices exceed the accepted document size.")
    return result


def zip_info(name: str) -> zipfile.ZipInfo:
    info = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o100644 << 16
    return info


def verify_release(directory: Path) -> dict:
    """Validate complete local output before Actions uploads or publishes it."""
    manifest = json.loads((directory / "update-manifest.json").read_text(encoding="utf-8"))
    version = parse_version(manifest["version"])
    package = manifest["package"]
    executable = manifest["executable"]
    expected_package = f"HUBTech-Diagram-Builder-{version}-Windows-x64.zip"
    expected_url = f"https://github.com/{REPOSITORY}/releases/download/v{version}/{expected_package}"
    if (manifest["schema_version"] != 1 or manifest["product"] != PRODUCT
            or manifest["architecture"] != "x64" or package["name"] != expected_package
            or package["url"] != expected_url or executable["path"] != EXECUTABLE):
        raise ValueError("Release manifest does not match the canonical updater contract.")
    package_path = directory / expected_package
    executable_path = directory / EXECUTABLE
    check_gui_executable(executable_path)
    for path, entry in ((package_path, package), (executable_path, executable)):
        if path.stat().st_size != entry["size"] or sha256(path) != entry["sha256"]:
            raise ValueError("Release size or checksum mismatch: " + path.name)
    with zipfile.ZipFile(package_path) as archive:
        names = archive.namelist()
        if len(names) != len(ZIP_MEMBERS) or set(names) != ZIP_MEMBERS:
            raise ValueError("Portable ZIP contains missing, duplicate or unexpected files.")
        if archive.testzip() is not None:
            raise ValueError("Portable ZIP integrity check failed.")
        for name in names:
            maximum = MAX_EXE_BYTES if name == EXECUTABLE else MAX_DOC_BYTES
            if archive.getinfo(name).file_size > maximum:
                raise ValueError("Portable ZIP entry exceeds its size limit.")
        digest = hashlib.sha256()
        with archive.open(EXECUTABLE) as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
        if digest.hexdigest() != executable["sha256"]:
            raise ValueError("Portable executable checksum does not match the manifest.")
        inner_sums = archive.read("SHA256SUMS.txt").decode("ascii").splitlines()
        inner_names = set()
        for line in inner_sums:
            expected_hash, name = line.split("  ", 1)
            if name in inner_names or name not in ZIP_MEMBERS - {"SHA256SUMS.txt"}:
                raise ValueError("Invalid portable checksum entry.")
            inner_names.add(name)
            if hashlib.sha256(archive.read(name)).hexdigest() != expected_hash:
                raise ValueError("Portable member checksum mismatch: " + name)
        if inner_names != ZIP_MEMBERS - {"SHA256SUMS.txt"}:
            raise ValueError("Portable checksums do not cover every packaged file.")
    expected_outer = {EXECUTABLE, expected_package, "update-manifest.json"}
    outer_names = set()
    for line in (directory / "SHA256SUMS.txt").read_text(encoding="ascii").splitlines():
        expected_hash, name = line.split("  ", 1)
        if name in outer_names or name not in expected_outer:
            raise ValueError("Invalid release checksum entry.")
        outer_names.add(name)
        if sha256(directory / name) != expected_hash:
            raise ValueError("Release checksum mismatch: " + name)
    if outer_names != expected_outer:
        raise ValueError("Release checksums do not cover every asset.")
    if {path.name for path in directory.iterdir()} != expected_outer | {"SHA256SUMS.txt"}:
        raise ValueError("Release directory contains unapproved files.")
    return manifest


def package_release(root: Path, publish_directory: Path, output_directory: Path,
                    version: str | None = None) -> dict:
    root, publish_directory, output_directory = (path.resolve() for path in
                                                (root, publish_directory, output_directory))
    declared = project_version(root)
    version = declared if version is None else parse_version(version)
    if version != declared:
        raise ValueError("Requested version does not match the application project.")
    source = publish_directory / EXECUTABLE
    check_gui_executable(source)
    if output_directory.exists() and any(output_directory.iterdir()):
        raise ValueError("Release output must be empty to prevent accidental extra uploads.")
    output_directory.parent.mkdir(parents=True, exist_ok=True)
    # Nothing is exposed to the final release path until the complete ZIP and
    # its hashes have been closed and independently verified.
    with tempfile.TemporaryDirectory(prefix="diagram-release-", dir=output_directory.parent) as temporary:
        stage = Path(temporary)
        executable = stage / EXECUTABLE
        shutil.copyfile(source, executable)
        documents = {"READ_ME.txt": read_me(root, version), "UPDATE_INFO.txt": (
            f"HUB Tech Network Diagram Builder {version}\n\n"
            "This package contains the Windows application and third-party notices.\n"
            "Updates are provided through the application's Updates controls.\n"
            "Packages and executables are checked before installation.\n"
            "The previous executable is retained for rollback.\n\n"
            f"Release: https://github.com/{REPOSITORY}/releases/tag/v{version}\n"
            "Complete editable source is available from the repository.\n"
        ).encode("utf-8")}
        executable_hash = sha256(executable)
        inner_sums = executable_hash + "  " + EXECUTABLE + "\n"
        inner_sums += "".join(hashlib.sha256(data).hexdigest() + "  " + name + "\n"
                              for name, data in documents.items())
        package_name = f"HUBTech-Diagram-Builder-{version}-Windows-x64.zip"
        package = stage / package_name
        with zipfile.ZipFile(package, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            with executable.open("rb") as source_stream, archive.open(zip_info(EXECUTABLE), "w", force_zip64=True) as target:
                shutil.copyfileobj(source_stream, target, length=1024 * 1024)
            for name, data in documents.items():
                archive.writestr(zip_info(name), data, compresslevel=6)
            archive.writestr(zip_info("SHA256SUMS.txt"), inner_sums.encode("ascii"), compresslevel=6)
        if package.stat().st_size > MAX_EXE_BYTES:
            raise ValueError("Compressed release package exceeds the updater limit.")
        manifest = {"schema_version": 1, "product": PRODUCT, "version": version,
                    "architecture": "x64", "package": {"name": package_name,
                    "url": f"https://github.com/{REPOSITORY}/releases/download/v{version}/{package_name}",
                    "size": package.stat().st_size, "sha256": sha256(package)},
                    "executable": {"path": EXECUTABLE, "size": executable.stat().st_size,
                                   "sha256": executable_hash}}
        (stage / "update-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        (stage / "SHA256SUMS.txt").write_text("".join(sha256(path) + "  " + path.name + "\n" for path in
            (executable, package, stage / "update-manifest.json")), encoding="ascii")
        verify_release(stage)
        if output_directory.exists():
            output_directory.rmdir()
        os.replace(stage, output_directory)
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--publish-dir", type=Path)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--version")
    parser.add_argument("--verify", type=Path, help="Verify an existing release directory without creating files.")
    arguments = parser.parse_args()
    if arguments.verify:
        manifest = verify_release(arguments.verify)
    else:
        if arguments.publish_dir is None or arguments.output_dir is None:
            parser.error("--publish-dir and --output-dir are required when packaging.")
        manifest = package_release(arguments.root, arguments.publish_dir, arguments.output_dir, arguments.version)
    print("PASS verified Windows release " + manifest["version"] + ": " + manifest["package"]["name"])


if __name__ == "__main__":
    main()
