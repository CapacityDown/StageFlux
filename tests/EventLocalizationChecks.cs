using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using REPOJP.StagePhysicsEvents;

internal static class EventLocalizationChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string description) { checks++; if (!value) throw new Exception(description); }
        string[] locales = { "en-US", "ja-JP", "ko-KR", "zh-CN", "zh-TW", "fr-FR", "de-DE", "es-ES", "pt-BR", "it-IT", "ru-RU", "pl-PL", "tr-TR", "uk-UA", "ar-001", "da-DK", "fi-FI", "sv-SE", "th-TH", "pt-PT", "es-419" };
        for (int i = 0; i < locales.Length; i++) Check((int)EventLanguageCode.FromLocaleCode(locales[i]) == i, "Native locale mapping: " + locales[i]);
        Check(EventLanguageCode.FromLocaleCode("ja_JP") == EventLanguage.Japanese, "Underscore locale");
        Check(EventLanguageCode.FromLocaleCode("zh-Hant-HK") == EventLanguage.ChineseTraditional, "Traditional Chinese variant");
        Check(EventLanguageCode.FromLocaleCode("es-MX") == EventLanguage.SpanishLatinAmerica, "Latin American variant");
        Check(EventLanguageCode.FromLocaleCode(null) == EventLanguage.English && EventLanguageCode.FromLocaleCode("unknown") == EventLanguage.English, "Unknown or not initialized locale uses English");
        var source = EventText.Catalog(EventLanguage.English);
        foreach (EventLanguage language in Enum.GetValues<EventLanguage>())
        {
            var catalog = EventText.Catalog(language);
            Check(catalog.Count == source.Count, "Same key coverage: " + language);
            foreach (string key in source.Keys)
            {
                Check(catalog.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value), "Embedded resource: " + language + ": " + key);
                int count = Regex.Matches(key, @"\{(\d+)\}").Select(m => int.Parse(m.Groups[1].Value) + 1).DefaultIfEmpty(0).Max();
                object[] arguments = Enumerable.Range(0, count).Select(n => (object)(n + 17)).ToArray();
                string formatted = EventText.Format(key, language, arguments);
                Check(!formatted.Contains("{0}") && !formatted.Contains("{1}"), "Valid format: " + language + ": " + key);
            }
            Check(EventText.Description("Unknown future host text 1289", language) == "Unknown future host text 1289", "Unknown hosts retain original text");
            Check(EventText.Description("Event duration: 12-45 seconds", language).Contains("12-45"), "Host numeric range retained");
            string description = EventText.Description("Enabled for draws, subject to stage conditions, available targets, combinations, and the simultaneous-event limit. Requires rockslide terrain.", language);
            Check(description.Contains(EventText.Get("Requires rockslide terrain.", language)), "Compound eligibility translates locally");
            if (language != EventLanguage.English)
                Check(EventText.Description("Event duration: 12-45 seconds", language) != "Event duration: 12-45 seconds", "Host details translate");
        }
        Check(EventText.Description("Event duration: 12-45 seconds", EventLanguage.Japanese) == "継続時間: 12-45秒", "Japanese host duration with unchanged value");
        Check(EventText.Description("Mode: FixedForStage\nEvent duration: 12-45 seconds", EventLanguage.Japanese) == "モード: FixedForStage\n継続時間: 12-45秒", "Native locale never changes canonical mode");
        foreach (string text in new[] { "日本語のイベントを表示します。", "ภาษาไทยทดสอบ", "العربية للاختبار", "한글 설명을 표시합니다", "a\u0301bc🙂" })
        foreach (int width in new[] { 1, 5, 12, 50 })
        {
            var lines = EventMenuLayout.Wrap(text, width, s => new StringInfo(s).LengthInTextElements);
            Check(string.Concat(lines).Replace(" ", "") == text.Replace(" ", ""), "Unicode wrap preserves text");
            Check(lines.All(line => new StringInfo(line).LengthInTextElements <= width), "Unicode wrap fits bounds");
        }
        Check(EventMenuLayout.ToolsTop == 112, "Tools aligns with Roles");
        string menu = File.ReadAllText(Path.Combine(root, "EventMenu.cs"));
        Check(menu.Contains("_language != EventGameLocalization.Language") && menu.Contains("EventGameLocalization.Apply(text)"), "Open menu refreshes locale and font");
        Check(!File.ReadAllText(Path.Combine(root, "EventMenuState.cs")).Contains("EventGameLocalization"), "Host snapshot never localized on sender");
        using var ledger = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "build-history.json")));
        int expected = 1;
        foreach (var item in ledger.RootElement.GetProperty("attempts").EnumerateArray()) Check(item.GetProperty("number").GetInt32() == expected++, "Build ledger has no holes or reused numbers");
        string build = File.ReadAllText(Path.Combine(root, "EventBuildInfo.cs"));
        Check(build.Contains($"Number = {expected - 1};"), "Build label matches ledger");
        return checks;
    }
}
