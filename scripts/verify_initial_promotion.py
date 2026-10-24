#!/usr/bin/env python3
"""One-time proof for publishing the already tested 1.12.0 draft unchanged."""
import argparse
import json
from pathlib import Path

from package_release import sha256, verify_release
from verify_release_assets import verify_receipt

REPOSITORY = "TechSavage1989/SwitchPort_Diagram_Builder"
SOURCE_COMMIT = "392951e6232576899255d678cad3fb0a09c3f1c2"
RUN_ID = 40140473467
ARTIFACT_ID = 12579365266
RELEASE_ID = 423029567
ARTIFACT_DIGEST = "sha256:4c9bb4754afc88719b7a1ff67ad0f2f7571272e5856c739dd78c79a407a5a484"
ASSET_HASHES = {
    "HUBTechDiagramBuilder.exe": "1ac777e1f205d0cf41444cc30e18add2ff59d360f5ddae1b67e9243303dc2210",
    "HUBTech-Diagram-Builder-1.12.0-Windows-x64.zip": "8899fba262521fb6325ab1f4072ba14356412f8164131005206b8c9ee5dcb959",
    "update-manifest.json": "798816eaa8d9d898f8aef7e75d7240986bb04c6a2f17554ad51a8022f0422f6b",
    "SHA256SUMS.txt": "eb44a0d5d20e5c6738b89d016289d12f5d5332b4b2de4d9321f881d0fc5145e6",
}
CHECK_JOBS = {
    "Embedded update and configuration controls",
    "Windows build and real updater process checks",
    "Parser, report, geometry and updater checks",
}


def verify_evidence(run: dict, job_pages: list, artifact: dict, release: dict,
                    directory: Path | None = None) -> str:
    if (run.get("id") != RUN_ID or run.get("head_sha") != SOURCE_COMMIT
            or run.get("head_branch") != "main" or run.get("event") != "push"
            or run.get("status") != "completed" or run.get("conclusion") != "failure"
            or run.get("repository", {}).get("full_name") != REPOSITORY
            or run.get("path") != ".github/workflows/build-and-release.yml"):
        raise ValueError("Original run does not match the fully tested main-branch commit.")
    jobs = [job for page in job_pages for job in page.get("jobs", [])]
    expected_names = CHECK_JOBS | {"Publish verified GitHub release"}
    if len(jobs) != len(expected_names) or {job.get("name") for job in jobs} != expected_names:
        raise ValueError("Original workflow job evidence is missing or ambiguous.")
    for job in jobs:
        if job.get("run_id") != RUN_ID or job.get("status") != "completed":
            raise ValueError("Original workflow job is incomplete or belongs to another run.")
        if job["name"] in CHECK_JOBS:
            if job.get("conclusion") != "success":
                raise ValueError("A required original test job did not pass.")
        else:
            failures = [step.get("name") for step in job.get("steps", [])
                        if step.get("conclusion") not in {"success", "skipped"}]
            if job.get("conclusion") != "failure" or failures != ["Publish a new immutable stable release"]:
                raise ValueError("Original run failed outside the known draft-publication step.")
    artifact_run = artifact.get("workflow_run", {})
    if (artifact.get("id") != ARTIFACT_ID or artifact.get("name") != "windows-release-assets"
            or artifact.get("expired") is not False or artifact.get("digest") != ARTIFACT_DIGEST
            or artifact.get("size_in_bytes") != 169148996
            or artifact_run.get("id") != RUN_ID or artifact_run.get("head_sha") != SOURCE_COMMIT
            or artifact_run.get("head_branch") != "main"
            or artifact_run.get("repository_id") != 1432646913
            or artifact_run.get("head_repository_id") != 1432646913):
        raise ValueError("Artifact is not the exact verified Windows build from the original run.")
    if (release.get("id") != RELEASE_ID or release.get("tag_name") != "v1.12.0"
            or release.get("name") != "HUB Tech Network Diagram Builder 1.12.0"
            or release.get("target_commitish") != SOURCE_COMMIT
            or release.get("prerelease") is not False or not isinstance(release.get("draft"), bool)):
        raise ValueError("Release is not the original stable draft bound to the tested source.")
    assets = release.get("assets", [])
    if len(assets) != len(ASSET_HASHES) or {asset.get("name") for asset in assets} != set(ASSET_HASHES):
        raise ValueError("Original draft assets changed or are incomplete.")
    for asset in assets:
        if asset.get("state") != "uploaded" or asset.get("digest") != "sha256:" + ASSET_HASHES[asset["name"]]:
            raise ValueError("An original draft asset changed after its successful Windows checks.")
    if directory is not None:
        manifest = verify_release(directory)
        if manifest["version"] != "1.12.0":
            raise ValueError("Downloaded artifact has the wrong application version.")
        for name, expected in ASSET_HASHES.items():
            if sha256(directory / name) != expected:
                raise ValueError("Downloaded artifact differs from the original uploaded draft.")
        verify_receipt(directory, release, release["draft"])
    return "draft" if release["draft"] else "published"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("run-json", "jobs-pages-json", "artifact-json", "release-json"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--directory", type=Path)
    parser.add_argument("--github-output", type=Path)
    args = parser.parse_args()
    state = verify_evidence(*(json.loads(path.read_text(encoding="utf-8")) for path in
        (args.run_json, args.jobs_pages_json, args.artifact_json, args.release_json)), args.directory)
    if args.github_output:
        with args.github_output.open("a", encoding="utf-8") as output:
            output.write("state=" + state + "\n")
    print("PASS original 1.12.0 test, artifact and unchanged release evidence: " + state)


if __name__ == "__main__":
    main()
