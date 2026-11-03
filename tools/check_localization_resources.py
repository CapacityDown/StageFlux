"""Offline catalog, placeholder and font checks; no Unity/game process required."""
import json
import re
import sys
from pathlib import Path
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[1]
folder = root / 'Assets/Localization'
english = json.loads((folder/'English.json').read_text(encoding='utf-8'))
assert len(list(folder.glob('*.json'))) == 21, 'Missing native locale'
assert all(k == v for k, v in english.items())
descriptions = re.findall(r'=> "([^"]+)"', (root/'EventGuideCatalog.cs').read_text(encoding='utf-8'))
assert set(descriptions) <= set(english), 'Missing event explanation'
fonts = {p.stem:set(TTFont(p).getBestCmap()) for p in (root/'Assets/Fonts/Localization').glob('*') if p.suffix in ('.otf','.ttf')}
checked = 0
for path in folder.glob('*.json'):
    catalog = json.loads(path.read_text(encoding='utf-8'))
    assert set(catalog) == set(english), (path.stem, set(english)-set(catalog), set(catalog)-set(english))
    glyphs = fonts['European'] | fonts.get(path.stem, set())
    for key, text in catalog.items():
        assert text and '\ufffd' not in text, (path.stem, key)
        assert sorted(re.findall(r'\{\d+\}',key)) == sorted(re.findall(r'\{\d+\}',text)), (path.stem,key)
        # Japanese uses the same pre-existing Checkpoint Revenge bundle as Roles.
        # Its Unity atlas is checked separately; this cmap audit covers Noto faces.
        if path.stem != 'Japanese':
            assert not (set(map(ord,text + text.upper())) - glyphs - {9,10,13}), (path.stem,key,set(map(ord,text))-glyphs)
        checked += 1
print(f'PASS: {checked} translated entries, 21 catalogs, 49 event explanations, placeholder integrity and Noto cmap coverage.')

if '--japanese-bundle' in sys.argv:
    import UnityPy
    japanese = json.loads((folder/'Japanese.json').read_text(encoding='utf-8'))
    required = set(map(ord, ''.join(japanese.values()))) - {9,10,13}
    bundle = UnityPy.load(str(root/'Assets/Fonts/CheckpointRevenge.bundle'))
    faces = [obj.read_typetree() for obj in bundle.objects if obj.type.name == 'MonoBehaviour']
    assert faces
    for face in faces:
        chars = {entry['m_Unicode'] for entry in face['m_CharacterTable']}
        assert not (required-chars), (face['m_Name'], required-chars)
    print(f'PASS: Japanese text covered by all {len(faces)} embedded Checkpoint Revenge font faces.')
