using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace REPOJP.StagePhysicsEvents;

// Shared measurements mirror the English Roles menu, in canvas units, not pixels.
internal static class EventMenuLayout
{
    internal const float RowPadding = 3f, RowSpacing = 1.5f, BodyHeight = 25f;
    internal const float BodyFont = 18f, TitleFont = 20f, GuideTitleHeight = 64f;
    internal const float FirstPadding = 20f, GuideSpacing = 10f;
    internal const float NavigationTop = 272f, NavigationStep = 32f;
    internal const float LineHeight = BodyHeight + RowPadding * 2f + RowSpacing;

    internal static List<string> Wrap(string text, float width, Func<string, float> measure)
    {
        var lines = new List<string>();
        width = Math.Max(1, width);
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (Match match in Regex.Matches(paragraph, @"\S+"))
            {
                string word = match.Value;
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (measure(candidate) <= width) { line = candidate; continue; }
                if (line.Length > 0) { lines.Add(line); line = ""; }
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
            lines.Add(line);
        }
        return lines;
    }
}
