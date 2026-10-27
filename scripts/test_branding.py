"""Keep the current tree and executable identity consistently branded."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
DISPLAY = 'Switchport Diagram Builder'

class BrandingChecks(unittest.TestCase):
    def test_current_tree_has_no_retired_brand(self):
        # The split expression tests the retired mark without embedding it in current docs/source.
        retired = re.compile(r'hu[b][\s_-]*te[c]h', re.I)
        ignored = {'.git', 'node_modules', '__pycache__', 'bin', 'obj', 'build', 'dist', 'artifacts'}
        for path in ROOT.rglob('*'):
            if any(part in ignored for part in path.relative_to(ROOT).parts):
                continue
            self.assertIsNone(retired.search(str(path.relative_to(ROOT))), str(path))
            if not path.is_file() or path.suffix in {'.ttf', '.ico', '.png'}:
                continue
            text = path.read_text(encoding='utf-8-sig')
            self.assertIsNone(retired.search(text), str(path))

    def test_display_name_and_windows_metadata(self):
        root = ET.parse(ROOT / 'Source/SwitchportDiagramBuilder/SwitchportDiagramBuilder.csproj').getroot()
        for name in ['Product', 'Title', 'AssemblyTitle', 'Description']:
            self.assertEqual(root.findtext('./PropertyGroup/' + name), DISPLAY)
        self.assertEqual(root.findtext('./PropertyGroup/AssemblyName'), 'SwitchportDiagramBuilder')
        html = (ROOT / 'Source/SwitchportDiagramBuilder/wwwroot/index.html').read_text()
        self.assertIn('<title>' + DISPLAY + '</title>', html)
        self.assertIn('<h1>' + DISPLAY + '</h1>', html)
        self.assertIn('Text = "' + DISPLAY + '"', (ROOT / 'Source/SwitchportDiagramBuilder/MainForm.cs').read_text())
        self.assertIn('product!=UpdateProtocol.DisplayName', (ROOT / 'Source/SwitchportUpdater/PackageValidator.cs').read_text())

    def test_project_references_and_versions(self):
        versions = set()
        for path in (ROOT / 'Source').rglob('*.csproj'):
            root = ET.parse(path).getroot()
            for reference in root.iter('ProjectReference'):
                self.assertTrue((path.parent / reference.attrib['Include']).is_file(), str(path))
            versions.update(n.text for n in root.iter('Version'))
        self.assertEqual(versions, {'1.14.6'})
        for line in (ROOT / 'Source/SOURCE_MANIFEST.txt').read_text().splitlines()[2:]:
            if line.strip():
                self.assertTrue((ROOT / 'Source' / line).is_file(), line)

if __name__ == '__main__':
    unittest.main()
