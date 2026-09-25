using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal static class NotificationEnemyReactionGuard
{
    private const float PlaybackStartGraceSeconds = 10f;
    private const float PlaybackSilenceGraceSeconds = 1f;
    private const float InvestigateCooldownSeconds = 1f;
    private static readonly FieldInfo? PlayerVoiceChatField =
        AccessTools.Field(typeof(PlayerAvatar), "voiceChat");
    private static readonly FieldInfo? VoiceChatPlayerAvatarField =
        AccessTools.Field(typeof(PlayerVoiceChat), "playerAvatar");
    private static readonly FieldInfo? InvestigateTimerField =
        AccessTools.Field(typeof(PlayerVoiceChat), "investigateTimer");
    private static readonly FieldInfo? ClipLoudnessNoTtsField =
        AccessTools.Field(typeof(PlayerVoiceChat), "clipLoudnessNoTTS");
    private static readonly FieldInfo? ClipLoudnessCrawlingCounterField =
        AccessTools.Field(typeof(PlayerVoiceChat), "clipLoudnessCrawlingCounter");
    private static readonly FieldInfo? PlayerDeathHeadField =
        AccessTools.Field(typeof(PlayerAvatar), "playerDeathHead");
    private static readonly FieldInfo? PlayerDisabledField =
        AccessTools.Field(typeof(PlayerAvatar), "isDisabled");
    private static readonly FieldInfo? PlayerCrawlingField =
        AccessTools.Field(typeof(PlayerAvatar), "isCrawling");
    private static readonly FieldInfo? PlayerCrouchingField =
        AccessTools.Field(typeof(PlayerAvatar), "isCrouching");
    private static readonly FieldInfo? DeathHeadSpectatedField =
        AccessTools.Field(typeof(PlayerDeathHead), "spectated");
    private static readonly FieldInfo? DeathHeadPhysGrabObjectField =
        AccessTools.Field(typeof(PlayerDeathHead), "physGrabObject");

    private sealed class ActiveNotification
    {
        internal ActiveNotification(
            PlayerVoiceChat voiceChat,
            string message,
            bool forceSuppression)
        {
            VoiceChat = voiceChat;
            ForceSuppression = forceSuppression;
            AwaitPlaybackUntil = Time.time + PlaybackStartGraceSeconds;
            HardExpiresAt = Time.time + Mathf.Clamp(
                PlaybackStartGraceSeconds + 2f + message.Length * 0.25f,
                12f,
                45f);
            NextMicrophoneInvestigateAt =
                Time.time + Mathf.Max(0f, ReadFloat(InvestigateTimerField, voiceChat));
            NoTtsCrawlingCounter = ReadFloat(ClipLoudnessNoTtsField, voiceChat) > 0.05f
                ? ReadInt(ClipLoudnessCrawlingCounterField, voiceChat)
                : 0;
        }

        internal PlayerVoiceChat VoiceChat { get; }
        internal bool ForceSuppression { get; }
        internal float AwaitPlaybackUntil { get; }
        internal float HardExpiresAt { get; }
        internal bool PlaybackObserved { get; set; }
        internal float LastPlaybackAt { get; set; }
        internal float NextMicrophoneInvestigateAt { get; set; }
        internal int NoTtsCrawlingCounter { get; set; }
    }

    private static readonly Dictionary<long, ActiveNotification> ActiveByVoiceChat = new();

    internal static void Expect(PlayerAvatar player, string message)
    {
        ExpectCore(player, message, false);
    }

    internal static bool ExpectExternal(PlayerAvatar? player, string? message)
    {
        return ExpectCore(player, message, true);
    }

    private static bool ExpectCore(
        PlayerAvatar? player,
        string? message,
        bool forceSuppression)
    {
        PlayerVoiceChat? voiceChat = GetVoiceChat(player);
        if (player?.photonView == null || string.IsNullOrEmpty(message) ||
            voiceChat == null)
        {
            return false;
        }

        ActiveByVoiceChat[NotificationKey(voiceChat, forceSuppression)] =
            new ActiveNotification(voiceChat, message, forceSuppression);
        return true;
    }

    internal static void CancelExpected(PlayerAvatar? player)
    {
        PlayerVoiceChat? voiceChat = GetVoiceChat(player);
        if (voiceChat != null)
        {
            ActiveByVoiceChat.Remove(NotificationKey(voiceChat, forceSuppression: false));
        }
    }

    internal static void CancelExternalExpected(PlayerAvatar? player)
    {
        PlayerVoiceChat? voiceChat = GetVoiceChat(player);
        if (voiceChat == null)
        {
            return;
        }

        long key = NotificationKey(voiceChat, forceSuppression: true);
        if (ActiveByVoiceChat.TryGetValue(key, out ActiveNotification active) &&
            active.ForceSuppression)
        {
            ActiveByVoiceChat.Remove(key);
        }
    }

    internal static bool PrepareVoiceUpdate(PlayerVoiceChat voiceChat)
    {
        if (!TryGetActive(voiceChat, out ActiveNotification active) ||
            (!active.ForceSuppression && !SuppressionEnabled()))
        {
            return false;
        }

        voiceChat.OverrideDisableEnemyInvestigate(0.25f);
        return true;
    }

    internal static void RestoreMicrophoneInvestigation(PlayerVoiceChat voiceChat)
    {
        if (!SemiFunc.IsMultiplayer() || !SemiFunc.IsMasterClient() ||
            EnemyDirector.instance == null ||
            !TryGetActive(voiceChat, out ActiveNotification active))
        {
            return;
        }

        PlayerAvatar? player = ReadReference<PlayerAvatar>(
            VoiceChatPlayerAvatarField,
            voiceChat);
        if (player == null)
        {
            return;
        }

        PlayerDeathHead? deathHead = ReadReference<PlayerDeathHead>(
            PlayerDeathHeadField,
            player);
        bool spectated =
            deathHead != null && ReadBool(DeathHeadSpectatedField, deathHead);
        if (ReadBool(PlayerDisabledField, player) && !spectated)
        {
            active.NoTtsCrawlingCounter = 0;
            return;
        }

        bool microphoneTriggered;
        if (spectated)
        {
            active.NoTtsCrawlingCounter = 0;
            microphoneTriggered =
                ReadFloat(ClipLoudnessNoTtsField, voiceChat) > 0.05f;
        }
        else if (ReadBool(PlayerCrawlingField, player))
        {
            if (ReadFloat(ClipLoudnessNoTtsField, voiceChat) > 0.05f)
            {
                active.NoTtsCrawlingCounter++;
            }
            else
            {
                active.NoTtsCrawlingCounter = 0;
            }
            microphoneTriggered = active.NoTtsCrawlingCounter > 10;
        }
        else
        {
            active.NoTtsCrawlingCounter = 0;
            microphoneTriggered = ReadFloat(ClipLoudnessNoTtsField, voiceChat) >
                (ReadBool(PlayerCrouchingField, player) ? 0.05f : 0.025f);
        }

        if (!microphoneTriggered || Time.time < active.NextMicrophoneInvestigateAt)
        {
            return;
        }

        Vector3 position = player.PlayerVisionTarget.VisionTransform.position;
        PhysGrabObject? deathHeadObject = deathHead != null
            ? ReadReference<PhysGrabObject>(DeathHeadPhysGrabObjectField, deathHead)
            : null;
        if (spectated && deathHeadObject != null)
        {
            position = deathHeadObject.centerPoint;
        }

        active.NextMicrophoneInvestigateAt = Time.time + InvestigateCooldownSeconds;
        EnemyDirector.instance.SetInvestigate(position, 5f);
    }

    internal static void Clear()
    {
        ActiveByVoiceChat.Clear();
    }

    internal static void ClearStageFluxSuppressions()
    {
        ClearMatchingSuppressions(forceSuppression: false);
    }

    internal static void ClearExternalSuppressions()
    {
        ClearMatchingSuppressions(forceSuppression: true);
    }

    internal static bool HasActiveNotifications() =>
        HasActiveNotifications(forceSuppression: null);

    internal static bool HasActiveExternalNotifications() =>
        HasActiveNotifications(forceSuppression: true);

    private static void ClearMatchingSuppressions(bool forceSuppression)
    {
        List<long> notificationKeys = new();
        foreach (KeyValuePair<long, ActiveNotification> pair in ActiveByVoiceChat)
        {
            if (pair.Value.ForceSuppression == forceSuppression)
            {
                notificationKeys.Add(pair.Key);
            }
        }

        foreach (long key in notificationKeys)
        {
            ActiveByVoiceChat.Remove(key);
        }
    }

    private static bool HasActiveNotifications(bool? forceSuppression)
    {
        List<long>? expiredKeys = null;
        bool found = false;
        float now = Time.time;
        foreach (KeyValuePair<long, ActiveNotification> pair in ActiveByVoiceChat)
        {
            ActiveNotification active = pair.Value;
            if (!IsActive(active, now))
            {
                expiredKeys ??= new List<long>();
                expiredKeys.Add(pair.Key);
                continue;
            }
            if (!forceSuppression.HasValue ||
                active.ForceSuppression == forceSuppression.Value)
            {
                found = true;
            }
        }
        if (expiredKeys != null)
        {
            foreach (long key in expiredKeys)
            {
                ActiveByVoiceChat.Remove(key);
            }
        }
        return found;
    }

    private static bool TryGetActive(
        PlayerVoiceChat voiceChat,
        out ActiveNotification active)
    {
        long externalKey = NotificationKey(voiceChat, forceSuppression: true);
        long stageFluxKey = NotificationKey(voiceChat, forceSuppression: false);
        if (TryGetActive(externalKey, out active))
        {
            return true;
        }
        return TryGetActive(stageFluxKey, out active);
    }

    private static bool TryGetActive(long key, out ActiveNotification active)
    {
        if (!ActiveByVoiceChat.TryGetValue(key, out active) ||
            active.VoiceChat == null ||
            !IsActive(active, Time.time))
        {
            ActiveByVoiceChat.Remove(key);
            active = null!;
            return false;
        }
        return true;
    }

    private static bool IsActive(ActiveNotification active, float now)
    {
        if (active.VoiceChat == null || now > active.HardExpiresAt)
        {
            return false;
        }

        PlayerVoiceChat voiceChat = active.VoiceChat;
        bool playing = voiceChat.ttsAudioSource != null &&
                       voiceChat.ttsAudioSource.isPlaying;
        if (playing)
        {
            active.PlaybackObserved = true;
            active.LastPlaybackAt = now;
            return true;
        }

        if (!active.PlaybackObserved && now <= active.AwaitPlaybackUntil)
        {
            return true;
        }

        if (active.PlaybackObserved && now - active.LastPlaybackAt <= PlaybackSilenceGraceSeconds)
        {
            return true;
        }
        return false;
    }

    private static bool SuppressionEnabled() =>
        StagePhysicsEventsPlugin.Instance != null &&
        !StagePhysicsEventsPlugin.Instance.Settings.EnemyReactionEnabled.Value;

    private static long NotificationKey(
        PlayerVoiceChat voiceChat,
        bool forceSuppression) =>
        ((long)(uint)voiceChat.GetInstanceID() << 1) |
        (forceSuppression ? 1L : 0L);

    private static PlayerVoiceChat? GetVoiceChat(PlayerAvatar? player) =>
        player == null
            ? null
            : ReadReference<PlayerVoiceChat>(PlayerVoiceChatField, player);

    private static T? ReadReference<T>(FieldInfo? field, object instance)
        where T : class =>
        field?.GetValue(instance) as T;

    private static float ReadFloat(FieldInfo? field, object instance) =>
        field?.GetValue(instance) is float value ? value : 0f;

    private static int ReadInt(FieldInfo? field, object instance) =>
        field?.GetValue(instance) is int value ? value : 0;

    private static bool ReadBool(FieldInfo? field, object instance) =>
        field?.GetValue(instance) is bool value && value;
}
