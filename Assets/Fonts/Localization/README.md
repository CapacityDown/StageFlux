# Localization fonts

The menu matches Roles' game-primary font, with private per-language fallback
clones at face scale 0.72. No global TMP font/settings are modified. Japanese
uses the same existing Checkpoint Revenge bundle; the other fonts use Noto.

Noto Sans and Noto Sans CJK are distributed under SIL OFL 1.1. The included
`OFL.txt` and `OFL-NotoSans.txt` notices are embedded, and copies are provided in
`package/LICENSES`. Subsets are renamed StageFlux European, StageFlux Korean,
StageFlux ChineseSimplified, StageFlux ChineseTraditional and StageFlux Names.

- CJK: https://github.com/notofonts/noto-cjk at the revision in `source-commit.txt`.
- Latin/Cyrillic: https://github.com/notofonts/noto-fonts at
  `ffebf8c1ee449e544955a7e813c54f9b73848eac`, NotoSans Regular.
- Arabic/Thai: unmodified Noto Sans Arabic/Thai Regular, with source revisions,
  URLs and SHA-256 values in `arabic-thai-sources.json`. Keeping the complete
  fonts preserves their shaping tables. Unity's native text shaper is enabled.

Regenerate subsets using fontTools and official source font files:

```text
python tools/build_localization_fonts.py --source-root <source-font-directory>
```

Fonts are extracted at runtime into `BepInEx/cache/StageFlux/Fonts` using
content-hashed filenames. TMP creates dynamic atlases. No other MOD source,
profile, installed font or game file is changed.
