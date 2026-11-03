"""Subset official Noto CJK OTFs for the bundled catalogs (fontTools required)."""
import argparse
import json
import re
import shutil
from pathlib import Path
from fontTools import subset
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--source-root', type=Path, required=True)
args = parser.parse_args()
destination = root / 'Assets/Fonts/Localization'
destination.mkdir(parents=True, exist_ok=True)
native = ''.join(re.findall(r'=> "([^"]+)"', (root/'EventLanguage.cs').read_text(encoding='utf-8')))
base = set(range(32,127)) | set(map(ord, native + '→−—…'))
for name, source in [('Names','sc'), ('European','sc'), ('Korean','kr'), ('ChineseSimplified','sc'), ('ChineseTraditional','tc')]:
    chars = set(base)
    if name == 'European':
        chars.update(range(0xa0,0x250))
        chars.update(range(0x400,0x530))
        catalogs = [p for p in (root/'Assets/Localization').glob('*.json') if p.stem not in ['Japanese','ChineseSimplified','ChineseTraditional','Korean','Arabic','Thai']]
    elif name == 'Names': catalogs=[]
    else: catalogs=[root/f'Assets/Localization/{name}.json']
    for catalog in catalogs:
        chars.update(map(ord, ''.join(json.loads(catalog.read_text(encoding='utf-8')).values())))
    font = TTFont(args.source_root / ('NotoSans-Regular.ttf' if name == 'European' else f'NotoSansCJK{source}-Regular.otf'))
    cmap = font.getBestCmap()
    required = chars - {10,13}
    # The European ranges intentionally include unassigned code points.
    if name not in ['European','Names']:
        required -= set(map(ord,native))
        assert not (required-set(cmap)), (name, required-set(cmap))
    chars &= set(cmap)
    options = subset.Options()
    options.name_IDs=['*']; options.name_legacy=True; options.name_languages=['*']
    sub = subset.Subsetter(options=options); sub.populate(unicodes=chars); sub.subset(font)
    family = 'StageFlux ' + name
    for record in font['name'].names:
        if record.nameID in [1,3,4,6,16]:
            value = family.replace(' ','') if record.nameID==6 else family
            record.string = value.encode(record.getEncoding())
    if 'CFF ' in font:
        cff=font['CFF '].cff; cff.fontNames=[family.replace(' ','')]
        cff.topDictIndex[0].FamilyName=family; cff.topDictIndex[0].FullName=family
    output=destination/(name+('.ttf' if name=='European' else '.otf'));font.save(output)
    print(f'{name}: {len(chars)} glyphs, {output.stat().st_size} bytes')
shutil.copyfile(args.source_root/'OFL.txt',destination/'OFL.txt')
(destination/'source-commit.txt').write_text((args.source_root/'noto-commit.txt').read_text().strip()+'\n')
