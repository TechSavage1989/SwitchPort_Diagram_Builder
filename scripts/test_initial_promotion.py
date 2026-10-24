#!/usr/bin/env python3
"""Validate the one-time promotion proof without network or mutation."""
import copy
import unittest

import verify_initial_promotion as promotion


def evidence():
    run = {"id": promotion.RUN_ID, "head_sha": promotion.SOURCE_COMMIT, "head_branch": "main",
           "event": "push", "status": "completed", "conclusion": "failure",
           "repository": {"full_name": promotion.REPOSITORY},
           "path": ".github/workflows/build-and-release.yml"}
    jobs = [{"name": name, "run_id": promotion.RUN_ID, "status": "completed", "conclusion": "success"}
            for name in sorted(promotion.CHECK_JOBS)]
    jobs.append({"name": "Publish verified GitHub release", "run_id": promotion.RUN_ID,
                 "status": "completed", "conclusion": "failure", "steps": [
                     {"name": "Verify assets after artifact download", "conclusion": "success"},
                     {"name": "Publish a new immutable stable release", "conclusion": "failure"}]})
    artifact = {"id": promotion.ARTIFACT_ID, "name": "windows-release-assets", "expired": False,
                "size_in_bytes": 169148996, "digest": promotion.ARTIFACT_DIGEST,
                "workflow_run": {"id": promotion.RUN_ID, "head_sha": promotion.SOURCE_COMMIT,
                    "head_branch": "main", "repository_id": 1432646913, "head_repository_id": 1432646913}}
    release = {"id": promotion.RELEASE_ID, "name": "HUB Tech Network Diagram Builder 1.12.0",
               "tag_name": "v1.12.0", "target_commitish": promotion.SOURCE_COMMIT,
               "draft": True, "prerelease": False, "assets": [
                   {"name": name, "state": "uploaded", "digest": "sha256:" + digest}
                   for name, digest in promotion.ASSET_HASHES.items()]}
    return run, [{"jobs": jobs}], artifact, release


class PromotionChecks(unittest.TestCase):
    def test_exact_original_evidence_allows_draft_or_verified_published_state(self):
        items = evidence()
        self.assertEqual(promotion.verify_evidence(*items), "draft")
        items[-1]["draft"] = False
        self.assertEqual(promotion.verify_evidence(*items), "published")

    def test_failed_required_job_blocks_promotion(self):
        items = evidence()
        items[1][0]["jobs"][0]["conclusion"] = "failure"
        with self.assertRaisesRegex(ValueError, "required original test"):
            promotion.verify_evidence(*items)

    def test_failure_outside_known_publish_step_blocks_promotion(self):
        items = evidence()
        items[1][0]["jobs"][-1]["steps"][0]["conclusion"] = "failure"
        with self.assertRaisesRegex(ValueError, "known draft-publication"):
            promotion.verify_evidence(*items)

    def test_changed_source_run_or_artifact_blocks_promotion(self):
        original = evidence()
        for index, key, value in ((0, "head_sha", "0" * 40), (0, "head_branch", "other"),
                                  (2, "digest", "sha256:" + "0" * 64), (2, "expired", True)):
            items = copy.deepcopy(original)
            items[index][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                promotion.verify_evidence(*items)

    def test_changed_draft_asset_blocks_promotion(self):
        items = evidence()
        items[-1]["assets"][0]["digest"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "draft asset changed"):
            promotion.verify_evidence(*items)

    def test_changed_release_identity_blocks_promotion(self):
        original = evidence()
        for key, value in (("id", 1), ("name", "other"), ("target_commitish", "0" * 40),
                           ("tag_name", "v1.12.1"), ("prerelease", True)):
            items = copy.deepcopy(original)
            items[-1][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                promotion.verify_evidence(*items)


if __name__ == "__main__":
    unittest.main()
