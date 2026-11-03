using System;
using System.Collections.Generic;
using System.Globalization;

namespace REPOJP.StagePhysicsEvents;

// Shared measurements mirror the Roles menu, in canvas units, not pixels.
internal static class EventMenuLayout
{
    internal const float RowPadding = 3f, RowSpacing = 1.5f, BodyHeight = 25f;
    internal const float BodyFont = 18f, TitleFont = 20f, GuideTitleHeight = 64f;
    internal const float FirstPadding = 20f, GuideSpacing = 10f;
    internal const float NavigationTop = 272f, NavigationStep = 32f;
    internal const float ToolsTop = 112f;
    internal const float LineHeight = BodyHeight + RowPadding * 2f + RowSpacing;

    internal static List<string> Wrap(string text, float width, Func<string, float> measure)
    {
        var lines = new List<string>();
        width = Math.Max(1, width);
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (string word in EventTextTokens.Tokens(paragraph))
            {
                if (line.Length == 0 && string.IsNullOrWhiteSpace(word)) continue;
                string candidate = line + word;
                if (measure(candidate) <= width) { line = candidate; continue; }
                if (line.Length > 0) { lines.Add(line.TrimEnd()); line = ""; }
                if (string.IsNullOrWhiteSpace(word)) continue;
                // Keep long keys and URLs inside the mask without dropping characters.
                TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(word);
                while (elements.MoveNext())
                {
                    string element = elements.GetTextElement();
                    if (line.Length > 0 && measure(line + element) > width)
                    { lines.Add(line); line = ""; }
                    line += element;
                }
            }
            lines.Add(line.TrimEnd());
        }
        return lines;
    }
}
