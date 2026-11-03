using System;

namespace REPOJP.StagePhysicsEvents;

internal enum EventLanguage
{
    English,
    Japanese,
    Korean,
    ChineseSimplified,
    ChineseTraditional,
    French,
    German,
    Spanish,
    PortugueseBrazilian,
    Italian,
    Russian,
    Polish,
    Turkish,
    Ukrainian,
    Arabic,
    Danish,
    Finnish,
    Swedish,
    Thai,
    PortuguesePortugal,
    SpanishLatinAmerica
}

internal static class EventLanguageCode
{
    internal static readonly string[] Names = Array.ConvertAll(
        (EventLanguage[])Enum.GetValues(typeof(EventLanguage)), NativeName);
    internal static EventLanguage Parse(string value)
    {
        int index = Array.IndexOf(Names, value);
        if (index >= 0) return (EventLanguage)index;
        // Preserve settings written before native language names were introduced.
        return Enum.TryParse(value, out EventLanguage language) && Enum.IsDefined(typeof(EventLanguage), language)
            ? language : EventLanguage.English;
    }
    internal static EventLanguage FromLocaleCode(string? code)
    {
        string normalized = (code ?? string.Empty).Replace('_', '-').ToLowerInvariant();
        if (normalized.StartsWith("zh"))
            return normalized.Contains("hant") || normalized is "zh-tw" or "zh-hk" or "zh-mo"
                ? EventLanguage.ChineseTraditional : EventLanguage.ChineseSimplified;
        if (normalized is "pt-pt") return EventLanguage.PortuguesePortugal;
        if (normalized is "es-419" or "es-mx" or "es-ar" or "es-cl" or "es-co" or "es-pe")
            return EventLanguage.SpanishLatinAmerica;
        string language = normalized.Split('-')[0];
        return language switch
        {
            "ja" => EventLanguage.Japanese, "ko" => EventLanguage.Korean,
            "fr" => EventLanguage.French, "de" => EventLanguage.German,
            "es" => EventLanguage.Spanish, "pt" => EventLanguage.PortugueseBrazilian,
            "it" => EventLanguage.Italian, "ru" => EventLanguage.Russian,
            "pl" => EventLanguage.Polish, "tr" => EventLanguage.Turkish,
            "uk" => EventLanguage.Ukrainian, "ar" => EventLanguage.Arabic,
            "da" => EventLanguage.Danish, "fi" => EventLanguage.Finnish,
            "sv" => EventLanguage.Swedish, "th" => EventLanguage.Thai,
            _ => EventLanguage.English
        };
    }
    internal static EventLanguage Next(EventLanguage current) =>
        (EventLanguage)(((int)current + 1) % Names.Length);
    internal static string NativeName(EventLanguage language) => language switch
    {
        EventLanguage.Japanese => "日本語",
        EventLanguage.Korean => "한국어",
        EventLanguage.ChineseSimplified => "简体中文",
        EventLanguage.ChineseTraditional => "繁體中文",
        EventLanguage.French => "Français",
        EventLanguage.German => "Deutsch",
        EventLanguage.Spanish => "Español",
        EventLanguage.PortugueseBrazilian => "Português (Brasil)",
        EventLanguage.Italian => "Italiano",
        EventLanguage.Russian => "Русский",
        EventLanguage.Polish => "Polski",
        EventLanguage.Turkish => "Türkçe",
        EventLanguage.Ukrainian => "Українська",
        EventLanguage.Arabic => "العربية",
        EventLanguage.Danish => "Dansk",
        EventLanguage.Finnish => "Suomi",
        EventLanguage.Swedish => "Svenska",
        EventLanguage.Thai => "ไทย",
        EventLanguage.PortuguesePortugal => "Português (Portugal)",
        EventLanguage.SpanishLatinAmerica => "Español (Latinoamérica)",
        _ => "English"
    };
    internal static bool NeedsTranslation(EventLanguage language) =>
        language is not EventLanguage.English and not EventLanguage.Japanese;
}
