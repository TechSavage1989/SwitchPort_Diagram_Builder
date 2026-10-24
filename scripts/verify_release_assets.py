#!/usr/bin/env python3
"""Verify a GitHub release asset receipt against the complete local package."""
import argparse
import json
from pathlib import Path
import re

from package_release import EXECUTABLE, parse_version, sha256, verify_release


def verify_receipt(directory: Path, release: dict, require_draft: bool) -> None:
    manifest = verify_release(directory)
    version = parse_version(manifest["version"])
    if release.get("tag_name") != "v" + version or release.get("prerelease") is not False:
        raise ValueError("GitHub release is not the expected stable version.")
    if release.get("draft") is not require_draft:
        raise ValueError("GitHub release has an unexpected publication state.")
    version_tag = "v" + version
    release_prefix = "https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/tag/"
    html_url = release.get("html_url")
    if html_url != release_prefix + version_tag:
        pending_tag = html_url.removeprefix(release_prefix) if isinstance(html_url, str) else ""
        if not require_draft or not re.fullmatch(r"untagged-[0-9a-f]{20}", pending_tag):
            raise ValueError("GitHub release URL is outside the expected stable or pending draft tag.")
        version_tag = pending_tag
    names = {EXECUTABLE, manifest["package"]["name"], "update-manifest.json", "SHA256SUMS.txt"}
    assets = release.get("assets", [])
    if len(assets) != len(names) or {item.get("name") for item in assets} != names:
        raise ValueError("GitHub release assets are missing, duplicated or unexpected.")
    for asset in assets:
        path = directory / asset["name"]
        if asset.get("state") != "uploaded" or asset.get("size") != path.stat().st_size:
            raise ValueError("GitHub asset upload is incomplete: " + path.name)
        expected_url = (f"https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/"
                        f"releases/download/{version_tag}/{path.name}")
        if asset.get("browser_download_url") != expected_url:
            raise ValueError("GitHub asset URL is outside the canonical release: " + path.name)
        # This updater deliberately targets public GitHub, which reports a
        # digest for every uploaded release asset. Fail closed if it is absent.
        digest = asset.get("digest")
        if digest != "sha256:" + sha256(path):
            raise ValueError("GitHub asset digest differs from the verified file: " + path.name)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--release-json", type=Path, required=True)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--require-draft", action="store_true")
    group.add_argument("--require-published", action="store_true")
    arguments = parser.parse_args()
    verify_receipt(arguments.directory,
                   json.loads(arguments.release_json.read_text(encoding="utf-8")),
                   arguments.require_draft)
    print("PASS complete GitHub release asset receipt matches verified local files.")


if __name__ == "__main__":
    main()
