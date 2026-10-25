#!/usr/bin/env python3
"""Regression checks for the updater's published package contract."""
import hashlib
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
import zipfile

import package_release as package
from verify_release_assets import verify_receipt


def stub_gui_pe() -> bytes:
    # Packaging checks validate the container shape. The Windows core checks
    # separately verify actual product/version resources on the published EXE.
    data = bytearray(4096)
    data[:2] = b"MZ"
    struct.pack_into("<I", data, 0x3C, 0x80)
    data[0x80:0x84] = b"PE\0\0"
    struct.pack_into("<H", data, 0x84, 0x8664)
    struct.pack_into("<H", data, 0x80 + 24, 0x20B)
    struct.pack_into("<H", data, 0x80 + 24 + 68, 2)
    return bytes(data)


class PackageChecks(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="diagram-package-check-")
        self.root = Path(self.temporary.name)
        self.publish = self.root / "publish"
        self.publish.mkdir()
        (self.publish / package.EXECUTABLE).write_bytes(stub_gui_pe())
        project = self.root / "Source/SwitchportDiagramBuilder/SwitchportDiagramBuilder.csproj"
        project.parent.mkdir(parents=True)
        project.write_text("<Project><PropertyGroup><Version>1.12.0</Version></PropertyGroup></Project>")
        notices = self.root / "Source/Licenses"
        notices.mkdir()
        (notices / "SkiaSharp.txt").write_text("SkiaSharp required notice\n")
        (notices / "DejaVu-Fonts.txt").write_text("DejaVu required notice\n")
        # Files beside the app and elsewhere in source must never enter release ZIPs.
        (self.publish / "private-client.backup").write_bytes(b"never publish")
        (self.root / "Source/private-client.cfg").write_bytes(b"never publish")
        self.output = self.root / "dist"

    def tearDown(self):
        self.temporary.cleanup()

    def create(self):
        return package.package_release(self.root, self.publish, self.output)

    def test_complete_contract_is_deterministic_and_binary_only(self):
        manifest = self.create()
        self.assertEqual(manifest["version"], "1.12.0")
        self.assertEqual(manifest["package"]["url"],
            "https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/download/"
            "v1.12.0/Switchport-Diagram-Builder-1.12.0-Windows-x64.zip")
        with zipfile.ZipFile(self.output / manifest["package"]["name"]) as archive:
            self.assertEqual(set(archive.namelist()), package.ZIP_MEMBERS)
            self.assertEqual(len(archive.namelist()), 4)
            readme = archive.read("READ_ME.txt").decode()
            self.assertIn("SkiaSharp required notice", readme)
            self.assertIn("DejaVu required notice", readme)
        second = self.root / "second"
        package.package_release(self.root, self.publish, second)
        self.assertEqual((self.output / manifest["package"]["name"]).read_bytes(),
                         (second / manifest["package"]["name"]).read_bytes())
        self.assertFalse(list(self.root.glob("diagram-release-*")))

    def test_current_release_has_only_clean_names_and_documentation(self):
        manifest = self.create()
        self.assertEqual(manifest["product"], "SwitchportDiagramBuilder")
        self.assertEqual(manifest["executable"]["path"], "SwitchportDiagramBuilder.exe")
        self.assertEqual(set(path.name for path in self.output.iterdir()), {
            "SwitchportDiagramBuilder.exe", "Switchport-Diagram-Builder-1.12.0-Windows-x64.zip",
            "update-manifest.json", "SHA256SUMS.txt"})
        with zipfile.ZipFile(self.output / manifest["package"]["name"]) as archive:
            for name in ("READ_ME.txt", "UPDATE_INFO.txt"):
                text = archive.read(name).decode("utf-8")
                self.assertIn("Switchport Diagram Builder", text)
                self.assertNotRegex(text, r"(?i)hub[ _-]?tech")

    def test_retired_brand_executable_bytes_are_rejected(self):
        for encoding in ("utf-8", "utf-16-le"):
            for separator in ("", " ", "_-", "\t"):
                with self.subTest(encoding=encoding, separator=separator):
                    mark = ("hUb" + separator + "TeCh").encode(encoding)
                    (self.publish / package.EXECUTABLE).write_bytes(stub_gui_pe() + mark)
                    with self.assertRaisesRegex(ValueError, "Retired product branding"):
                        self.create()
                    self.assertFalse(self.output.exists())

    def test_retired_brand_detector_handles_every_chunk_boundary(self):
        for encoding in ("utf-8", "utf-16-le"):
            mark = ("HuB" + " " * 100 + "tEcH").encode(encoding)
            for boundary in range(1, len(mark)):
                class TinyStream(io.BytesIO):
                    def read(self, count=-1):
                        return super().read(boundary)
                with self.subTest(encoding=encoding, boundary=boundary):
                    with self.assertRaisesRegex(ValueError, "Retired product branding"):
                        package.check_retired_brand(TinyStream(b"!" + mark), "fixture")
        package.check_retired_brand(io.BytesIO(b"hubcap technical hub teacup"), "clean fixture")
        package.check_retired_brand(io.BytesIO("Switchport Diagram Builder".encode("utf-16-le")), "clean fixture")

    def test_retired_brand_in_packaged_documents_is_rejected(self):
        (self.root / "Source/Licenses/SkiaSharp.txt").write_text("HuB" + " TeCh")
        with self.assertRaisesRegex(ValueError, "portable document"):
            self.create()
        self.assertFalse(self.output.exists())

    def test_executable_tampering_is_rejected(self):
        self.create()
        with (self.output / package.EXECUTABLE).open("ab") as stream:
            stream.write(b"changed")
        with self.assertRaisesRegex(ValueError, "checksum mismatch"):
            package.verify_release(self.output)

    def test_manifest_cannot_redirect_to_another_repository(self):
        manifest = self.create()
        manifest["package"]["url"] = manifest["package"]["url"].replace("TechSavage1989", "someone-else")
        (self.output / "update-manifest.json").write_text(json.dumps(manifest))
        with self.assertRaisesRegex(ValueError, "canonical updater contract"):
            package.verify_release(self.output)

    def test_unexpected_output_is_rejected(self):
        self.output.mkdir()
        (self.output / "client.cfg").write_bytes(b"private")
        with self.assertRaisesRegex(ValueError, "output must be empty"):
            self.create()
        self.assertEqual((self.output / "client.cfg").read_bytes(), b"private")

    def test_version_mismatch_and_prereleases_are_rejected(self):
        for version in ("1.12.1", "1.12.0-beta", "v1.12.0", "1.12.0.1", "1.12.65536"):
            with self.subTest(version=version), self.assertRaises(ValueError):
                package.package_release(self.root, self.publish, self.output, version)

    def test_non_gui_or_non_x64_executable_is_rejected(self):
        original = bytearray(stub_gui_pe())
        for offset, value in ((0x84, 0x14C), (0x80 + 24 + 68, 3)):
            invalid = bytearray(original)
            struct.pack_into("<H", invalid, offset, value)
            (self.publish / package.EXECUTABLE).write_bytes(invalid)
            with self.subTest(offset=offset), self.assertRaises(ValueError):
                self.create()

    def test_missing_license_notice_is_rejected(self):
        (self.root / "Source/Licenses/SkiaSharp.txt").unlink()
        with self.assertRaisesRegex(ValueError, "license notices"):
            self.create()
        self.assertFalse(self.output.exists())

    def test_tampered_zip_member_is_rejected_even_with_new_outer_hash(self):
        manifest = self.create()
        archive_path = self.output / manifest["package"]["name"]
        replacement = self.root / "changed.zip"
        with zipfile.ZipFile(archive_path) as source, zipfile.ZipFile(replacement, "w") as target:
            for member in source.infolist():
                data = source.read(member.filename)
                if member.filename == package.EXECUTABLE:
                    data += b"modified"
                target.writestr(member.filename, data)
        replacement.replace(archive_path)
        manifest["package"]["size"] = archive_path.stat().st_size
        manifest["package"]["sha256"] = hashlib.sha256(archive_path.read_bytes()).hexdigest()
        (self.output / "update-manifest.json").write_text(json.dumps(manifest))
        with self.assertRaisesRegex(ValueError, "Portable executable checksum"):
            package.verify_release(self.output)

    def receipt(self, draft=True):
        manifest = self.create()
        return {"tag_name": "v1.12.0", "prerelease": False, "draft": draft,
                "html_url": "https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/tag/v1.12.0",
                "assets": [{"name": path.name, "state": "uploaded", "size": path.stat().st_size,
                    "digest": "sha256:" + package.sha256(path), "browser_download_url":
                    "https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/"
                    "download/v1.12.0/" + path.name} for path in self.output.iterdir()]}

    def test_complete_draft_and_published_receipts_are_accepted(self):
        receipt = self.receipt()
        verify_receipt(self.output, receipt, True)
        receipt["draft"] = False
        verify_receipt(self.output, receipt, False)

    def test_remote_incomplete_upload_is_rejected(self):
        receipt = self.receipt()
        receipt["assets"][0]["state"] = "starter"
        with self.assertRaisesRegex(ValueError, "upload is incomplete"):
            verify_receipt(self.output, receipt, True)

    def test_remote_digest_mismatch_is_rejected(self):
        receipt = self.receipt()
        receipt["assets"][0]["digest"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "digest differs"):
            verify_receipt(self.output, receipt, True)

    def test_remote_digest_must_be_present(self):
        receipt = self.receipt()
        del receipt["assets"][0]["digest"]
        with self.assertRaisesRegex(ValueError, "digest differs"):
            verify_receipt(self.output, receipt, True)

    def test_remote_missing_asset_is_rejected(self):
        receipt = self.receipt()
        receipt["assets"].pop()
        with self.assertRaisesRegex(ValueError, "assets are missing"):
            verify_receipt(self.output, receipt, True)

    def test_remote_wrong_repository_is_rejected(self):
        receipt = self.receipt()
        receipt["assets"][0]["browser_download_url"] = "https://example.com/app.exe"
        with self.assertRaisesRegex(ValueError, "outside the canonical release"):
            verify_receipt(self.output, receipt, True)

    def test_remote_publication_state_cannot_change_silently(self):
        receipt = self.receipt()
        with self.assertRaisesRegex(ValueError, "publication state"):
            verify_receipt(self.output, receipt, False)

    def test_exact_pending_draft_tag_is_accepted_only_before_publication(self):
        receipt = self.receipt()
        pending = "untagged-" + "a" * 20
        receipt["html_url"] = receipt["html_url"].replace("v1.12.0", pending)
        for asset in receipt["assets"]:
            asset["browser_download_url"] = asset["browser_download_url"].replace("v1.12.0", pending)
        verify_receipt(self.output, receipt, True)
        receipt["draft"] = False
        with self.assertRaisesRegex(ValueError, "release URL"):
            verify_receipt(self.output, receipt, False)

    def test_pending_tag_cannot_come_from_another_repository(self):
        receipt = self.receipt()
        receipt["html_url"] = "https://github.com/someone/else/releases/tag/untagged-" + "a" * 20
        with self.assertRaisesRegex(ValueError, "release URL"):
            verify_receipt(self.output, receipt, True)


if __name__ == "__main__":
    unittest.main()
