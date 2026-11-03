using TMPro;

namespace REPOJP.StagePhysicsEvents;

// Local presentation only. Never publish localized strings in host snapshots.
internal static class EventGameLocalization
{
    internal static EventLanguage Language => EventLanguageCode.FromLocaleCode(
        LocalizationManager.instance != null ? SemiFunc.LocaleGetCode() : "en-US");
    internal static string Text(string english) => EventText.Description(english, Language);
    internal static string Format(string english, params object[] values) => EventText.Format(english, Language, values);
    internal static void Apply(TMP_Text text)
    {
        text.font = EventMenuFont.ForLanguage(text.font, Language);
        EventTextLayout.Apply(text, Language);
    }
}
