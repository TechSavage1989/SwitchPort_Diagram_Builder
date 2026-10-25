#!/usr/bin/env python3
"""Exercise publication guards without contacting GitHub or creating releases."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

import yaml
import package_release as package
from test_package_release import stub_gui_pe

FAKE_GH = r'''
import hashlib,json,os,pathlib,sys
args=sys.argv[1:]
root=pathlib.Path.cwd();mode=os.environ['FAKE_GH_MODE']
state=root/'gh-state.json';calls=root/'gh-calls.txt'
with calls.open('a') as out:out.write(' '.join(args)+'\n')
current=json.loads(state.read_text()) if state.exists() else {}
def error(code):
 print('gh: simulated failure (HTTP '+str(code)+')',file=sys.stderr);sys.exit(1)
if args[:1]==['api']:
 endpoint=args[1]
 if endpoint.endswith('/releases?per_page=100') or endpoint.endswith('/releases/123'):
  if current.get('created'):
   if '--method' in args:
    current['draft']=False;state.write_text(json.dumps(current))
   manifest=json.loads((root/'dist/update-manifest.json').read_text())
   urltag='untagged-'+'a'*20 if current['draft'] else 'v1.12.0'
   assets=[{'name':p.name,'state':'uploaded','size':p.stat().st_size,
     'digest':'sha256:'+hashlib.sha256(p.read_bytes()).hexdigest(),
     'browser_download_url':'https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/download/'+urltag+'/'+p.name}
      for p in (root/'dist').iterdir()]
   receipt={'id':123,'tag_name':'v1.12.0','draft':current['draft'],'prerelease':False,'assets':assets,
    'target_commitish':'a'*40,'html_url':'https://github.com/TechSavage1989/SwitchPort_Diagram_Builder/releases/tag/'+urltag}
   print(json.dumps([[receipt]] if endpoint.endswith('/releases?per_page=100') else receipt));sys.exit(0)
  if mode=='existing':print(json.dumps([[{'tag_name':'v1.12.0','draft':False,'prerelease':False}]]));sys.exit(0)
  if mode=='first403':error(403)
  print(json.dumps([[]]));sys.exit(0)
 if endpoint.endswith('/releases/latest'):
  if mode=='latest503':error(503)
  if mode=='older':print(json.dumps({'tag_name':'v1.13.0'}));sys.exit(0)
  error(404)
 if endpoint.endswith('/git/ref/tags/v1.12.0'):
  if mode=='tag503':error(503)
  if mode=='tag-mismatch':print(json.dumps({'object':{'type':'commit','sha':'b'*40}}));sys.exit(0)
  error(404)
 error(500)
if args[:2]==['release','create']:
 state.write_text(json.dumps({'created':True,'draft':True}));sys.exit(0)
raise SystemExit('Unexpected fake gh command')
'''


@unittest.skipUnless(shutil.which("bash"), "Publication guard checks require bash.")
class WorkflowChecks(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="diagram-publish-check-")
        self.root = Path(self.temporary.name)
        self.publish = self.root / "publish"
        self.publish.mkdir()
        (self.publish / package.EXECUTABLE).write_bytes(stub_gui_pe())
        project = self.root / "Source/SwitchportDiagramBuilder/SwitchportDiagramBuilder.csproj"
        project.parent.mkdir(parents=True)
        project.write_text("<Project><PropertyGroup><Version>1.12.0</Version></PropertyGroup></Project>")
        notices = self.root / "Source/Licenses"
        notices.mkdir()
        for name in ("SkiaSharp.txt", "DejaVu-Fonts.txt"):
            (notices / name).write_text(name)
        package.package_release(self.root, self.publish, self.root / "dist")
        scripts = Path(__file__).resolve().parent
        (self.root / "scripts").symlink_to(scripts, target_is_directory=True)
        workflow = yaml.safe_load((scripts.parent / ".github/workflows/build-and-release.yml").read_text())
        self.script = self.root / "publish.sh"
        self.script.write_text(workflow["jobs"]["release"]["steps"][-1]["run"])
        fake_bin = self.root / "bin"
        fake_bin.mkdir()
        fake_gh = fake_bin / "gh"
        fake_gh.write_text("#!" + sys.executable + "\n" + FAKE_GH)
        fake_gh.chmod(0o755)
        (self.root / "runner-temp").mkdir()

    def tearDown(self):
        self.temporary.cleanup()

    def run_mode(self, mode):
        environment = dict(os.environ, FAKE_GH_MODE=mode,
            GH_REPO=package.REPOSITORY, GITHUB_SHA="a" * 40,
            RUNNER_TEMP=str(self.root / "runner-temp"),
            PATH=str(self.root / "bin") + os.pathsep + os.environ["PATH"])
        result = subprocess.run(["bash", str(self.script)], cwd=self.root, env=environment,
                                capture_output=True, text=True, timeout=20)
        calls = (self.root / "gh-calls.txt").read_text()
        return result, calls

    def assert_no_mutation(self, mode):
        result, calls = self.run_mode(mode)
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertNotIn("release create", calls)
        self.assertNotIn("--method PATCH", calls)

    def test_non404_initial_failure_blocks_publication(self):
        self.assert_no_mutation("first403")

    def test_latest_read_failure_blocks_publication(self):
        self.assert_no_mutation("latest503")

    def test_tag_read_failure_blocks_publication(self):
        self.assert_no_mutation("tag503")

    def test_older_version_cannot_become_latest(self):
        self.assert_no_mutation("older")

    def test_mismatched_existing_tag_blocks_publication(self):
        self.assert_no_mutation("tag-mismatch")

    def test_published_version_is_left_unchanged(self):
        result, calls = self.run_mode("existing")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("release create", calls)
        self.assertNotIn("--method PATCH", calls)

    def test_new_release_is_draft_verified_then_published(self):
        result, calls = self.run_mode("new")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("--draft", calls)
        self.assertIn('--title Switchport Diagram Builder 1.12.0', calls)
        self.assertIn("dist/SwitchportDiagramBuilder.exe", calls)
        self.assertIn("dist/Switchport-Diagram-Builder-1.12.0-Windows-x64.zip", calls)
        self.assertIn("/releases/123 --method PATCH -F draft=false -f make_latest=true", calls)
        self.assertNotIn("/releases/tags/v1.12.0", calls)
        self.assertGreater(calls.index("--method PATCH"), calls.index("release create"))
        self.assertEqual(json.loads((self.root / "gh-state.json").read_text()),
                         {"created": True, "draft": False})


if __name__ == "__main__":
    unittest.main()
