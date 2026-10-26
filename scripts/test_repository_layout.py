"""Keep the GitHub landing page compact without hiding unreviewed new files."""
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]
ALLOWED = {'AGENTS.md', '.github', 'docs', 'package.json', 'global.json', 'package-lock.json', 'scripts', '.gitignore', 'DEVELOPMENT.md', 'Source', 'README.md'}

def unexpected(paths):
    return sorted({p.split('/')[0] for p in paths} - ALLOWED)

class RepositoryLayoutTests(unittest.TestCase):
    def test_tracked_root_allowlist(self):
        # Git source archives have no index; ignore only documented local outputs.
        if (ROOT / '.git').exists():
            paths = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0')
        else:
            paths = [p.name for p in ROOT.iterdir()
                     if p.name not in {'build', 'dist', 'artifacts', 'node_modules', 'test-results'}]
        self.assertEqual(unexpected(p for p in paths if p), [], 'Group new files under existing directories; read AGENTS.md')

    def test_guard_rejects_future_root_clutter(self):
        self.assertEqual(unexpected(['README.md', 'scratch.txt', 'new-feature.cpp', 'notes/one.md']),
                         ['new-feature.cpp', 'notes', 'scratch.txt'])

    def test_required_documents(self):
        for path in ['AGENTS.md', 'README.md', 'docs/UPDATING.md']:
            self.assertTrue((ROOT / path).is_file(), path)

if __name__ == '__main__':
    unittest.main()
