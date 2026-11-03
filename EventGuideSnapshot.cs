using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace REPOJP.StagePhysicsEvents;

internal readonly struct EventGuideDetails(string text, string eligibility)
{
    internal string Text { get; } = text;
    internal string Eligibility { get; } = eligibility;
}

// Optional display-only room property. Existing SF.Menu v1 remains unchanged.
internal static class EventGuideSnapshot
{
    internal const int MaxLength = 98304;
    internal static string Encode(int actor, IReadOnlyDictionary<long, EventGuideDetails> rows)
    {
        var text = new StringBuilder("1:" + actor.ToString(CultureInfo.InvariantCulture));
        foreach (var pair in rows)
        {
            if (pair.Value.Text.Length > 8192 || pair.Value.Eligibility.Length > 1024)
                throw new InvalidOperationException("Event guide row too large.");
            text.Append('\n').Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value.Text))).Append('\t')
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value.Eligibility)));
        }
        if (text.Length > MaxLength) throw new InvalidOperationException("Event guide snapshot too large.");
        return text.ToString();
    }

    internal static bool TryDecode(string? payload, int actor, long allowedMask, out Dictionary<long, EventGuideDetails> rows)
    {
        rows = new();
        if (actor <= 0 || string.IsNullOrEmpty(payload) || payload!.Length > MaxLength) return false;
        string[] lines = payload.Split('\n');
        if (lines.Length > 65 || lines.Length < 2 || lines[0] != "1:" + actor.ToString(CultureInfo.InvariantCulture)) return false;
        var parsed = new Dictionary<long, EventGuideDetails>();
        try
        {
            foreach (string line in new ArraySegment<string>(lines, 1, lines.Length - 1))
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 3 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long bit) ||
                    bit <= 0 || (bit & (bit - 1)) != 0 || (bit & allowedMask) != bit || parsed.ContainsKey(bit)) return false;
                string text = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(parts[1]));
                string eligibility = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(parts[2]));
                if (text.Length > 8192 || eligibility.Length > 1024) return false;
                parsed.Add(bit, new EventGuideDetails(text, eligibility));
            }
        }
        catch (Exception e) when (e is FormatException || e is DecoderFallbackException) { return false; }
        long received = 0; foreach (long bit in parsed.Keys) received |= bit;
        if (received != allowedMask) return false;
        rows = parsed; return true;
    }
}
