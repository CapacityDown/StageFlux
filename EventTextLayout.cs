using TMPro;

namespace REPOJP.StagePhysicsEvents;

internal static class EventTextLayout
{
    // Use the game's Unity 6 shaper for Arabic joining/bidi and Thai marks.
    // Apply only to this mod's text; do not mutate global TMP settings/fonts.
    internal static void Apply(TMP_Text text, EventLanguage language)
    {
        text.useTextShaper = true;
        text.isRightToLeftText = language == EventLanguage.Arabic;
    }

}
