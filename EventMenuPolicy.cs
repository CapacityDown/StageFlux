using System;
using System.Globalization;

namespace REPOJP.StagePhysicsEvents;

internal static class EventMenuPolicy
{
    internal const int FormatVersion = 1;
    internal static bool CanEdit(bool gameReady, bool multiplayer, bool inRoom, bool master) =>
        gameReady && (inRoom ? master : !multiplayer);

    internal static string Encode(int actor, long mask, bool active) =>
        string.Join(":", FormatVersion.ToString(CultureInfo.InvariantCulture), actor.ToString(CultureInfo.InvariantCulture),
            mask.ToString(CultureInfo.InvariantCulture), active ? "1" : "0");

    internal static bool TryDecode(string? payload, int masterActor, out long mask, out bool active)
    {
        mask = 0; active = false;
        if (string.IsNullOrEmpty(payload) || payload!.Length > 128 || masterActor <= 0) return false;
        string[] parts = payload.Split(':');
        if (parts.Length != 4 || parts[0] != "1" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int actor) || actor != masterActor ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ||
            (parts[3] != "0" && parts[3] != "1")) return false;
        mask = parsed; active = parts[3] == "1"; return true;
    }
}
