# Events menu localization

The Events menu and local HUD-editor controls follow `SemiFunc.LocaleGetCode()`.
The 21 embedded UTF-8 catalogs cover every locale supported by R.E.P.O. 0.5.0.
Each has the same English-key set, including all 49 event descriptions. Unknown
locales and unknown descriptions from a newer host fall back to English.

The corner button remains `Events`. Event names, configuration identifiers,
diagnostic reports, live stage HUD text and spoken announcements remain English.
Host snapshots keep their existing canonical English values. Only the receiving
client's display is translated; no RNG, event settings, or network contract changes.
Open menus refresh after a native-language change without saving a MOD language.

Shared UI phrases follow the existing Roles translations. New event explanations
are authored for Stage Flux. Traditional Chinese uses OpenCC `s2twp` conversion;
regional Spanish and Portuguese retain their own shared menu translations.
No runtime translation service, network request or OS font installation is used.

Checks:

```text
python tools/check_localization_resources.py
python tools/check_localization_resources.py --japanese-bundle
dotnet run --project tests/StageFlux.ExtendedEvents.Tests.csproj -c Release -- <project> <game-assembly>
```

The Python check requires fontTools; the optional bundle check also needs UnityPy.
It verifies catalog/key parity, placeholders
and bundled Noto cmap coverage. The C# suite loads the actual embedded catalogs,
checks locale aliases, host value preservation, fallback and grapheme wrapping.
Native proofreading and live Unity layout/shaping acceptance remain separate.
