using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class VanillaEventNotifier
{
    private const float StageStartNotificationDelaySeconds = 1f;
    private const float NotificationRetryIntervalSeconds = 0.25f;
    private const float EventMessageLifetimeSeconds = 15f;
    private const float CountdownMessageLifetimeSeconds = 3f;
    private const int MaximumPendingEventMessages = 4;
    private readonly MonoBehaviour _coroutineOwner;
    private readonly StagePhysicsConfig _config;
    private readonly HashSet<int> _notifiedActors = new();
    private readonly List<PendingEventMessage> _pendingEventMessages = new();
    private int _stageGeneration;
    private int _eventMessageRunnerGeneration = -1;
    private string _stageMessage = string.Empty;

    private enum EventMessageKind
    {
        Started,
        Countdown,
        Ended
    }

    private sealed class PendingEventMessage
    {
        internal PendingEventMessage(
            int generation,
            string message,
            EventMessageKind kind,
            float expiresAt)
        {
            Generation = generation;
            Message = message;
            Kind = kind;
            ExpiresAt = expiresAt;
        }

        internal int Generation { get; }
        internal string Message { get; }
        internal EventMessageKind Kind { get; }
        internal float ExpiresAt { get; }
    }

    internal VanillaEventNotifier(MonoBehaviour coroutineOwner, StagePhysicsConfig config)
    {
        _coroutineOwner = coroutineOwner;
        _config = config;
    }

    internal void BeginStage(int stageGeneration, string stageMessage)
    {
        NotificationEnemyReactionGuard.ClearStageFluxSuppressions();
        NotificationWindowCoordinator.ReleaseStageFluxReservation();
        _stageGeneration = stageGeneration;
        _stageMessage = stageMessage;
        _notifiedActors.Clear();
        _pendingEventMessages.Clear();
        if (_config.ChatAnnouncementsEnabled.Value && !string.IsNullOrEmpty(_stageMessage) &&
            SemiFunc.IsMultiplayer() && PhotonNetwork.IsMasterClient)
        {
            NotificationWindowCoordinator.TryReserveStageFlux(
                4f + StageStartNotificationDelaySeconds);
            _coroutineOwner.StartCoroutine(NotifyAllAfterCapabilityGrace(stageGeneration));
        }
    }

    internal void PlayerEntered(Player player)
    {
        if (_config.ChatAnnouncementsEnabled.Value && !string.IsNullOrEmpty(_stageMessage) && PhotonNetwork.IsMasterClient)
        {
            _coroutineOwner.StartCoroutine(NotifyPlayerAfterCapabilityGrace(player, _stageGeneration));
        }
    }

    internal void EndStage()
    {
        NotificationEnemyReactionGuard.Clear();
        NotificationWindowCoordinator.ClearAll();
        _stageGeneration++;
        _notifiedActors.Clear();
        _pendingEventMessages.Clear();
        _stageMessage = string.Empty;
    }

    internal void EventStarted(StageEffect effect)
    {
        SendEventMessage(
            StageEffectSet.FormatForChat(effect, "/"),
            EventMessageKind.Started);
    }

    internal void Countdown(int seconds)
    {
        SendEventMessage(seconds.ToString(), EventMessageKind.Countdown);
    }

    internal void EventEnded()
    {
        SendEventMessage("End", EventMessageKind.Ended);
    }

    private IEnumerator NotifyAllAfterCapabilityGrace(int generation)
    {
        yield return new WaitForSeconds(2f);
        yield return WaitUntilReady(generation);
        if (!CanNotify(generation))
        {
            yield break;
        }
        yield return new WaitForSeconds(StageStartNotificationDelaySeconds);
        if (!CanNotify(generation))
        {
            yield break;
        }
        float reservationDeadline = Time.time + 15f;
        while (CanNotify(generation) &&
               !NotificationWindowCoordinator.TryReserveStageFlux(2f) &&
               Time.time < reservationDeadline)
        {
            yield return new WaitForSeconds(0.25f);
        }
        if (!CanNotify(generation) ||
            !NotificationWindowCoordinator.TryReserveStageFlux(2f))
        {
            yield break;
        }
        foreach (Player player in PhotonNetwork.PlayerListOthers)
        {
            _notifiedActors.Add(player.ActorNumber);
        }
        if (!ForceAllPlayersToSpeak(_stageMessage))
        {
            _notifiedActors.Clear();
        }
    }

    private IEnumerator NotifyPlayerAfterCapabilityGrace(Player player, int generation)
    {
        yield return new WaitForSeconds(2f);
        yield return WaitUntilReady(generation);
        if (CanNotify(generation) && player != null)
        {
            NotifyLateJoiner(player);
        }
    }

    private bool CanNotify(int generation) =>
        generation == _stageGeneration &&
        PhotonNetwork.IsMasterClient &&
        GameDirector.instance != null &&
        GameDirector.instance.currentState == GameDirector.gameState.Main;

    private IEnumerator WaitUntilReady(int generation)
    {
        float deadline = Time.time + 10f;
        while (generation == _stageGeneration && PhotonNetwork.IsMasterClient && !CanNotify(generation) && Time.time < deadline)
        {
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void NotifyLateJoiner(Player player)
    {
        if (!_notifiedActors.Add(player.ActorNumber))
        {
            return;
        }

        // The vanilla API only has the reliable RpcTarget.All path. Existing players may see
        // this one extra stage summary when somebody joins an in-progress stage.
        if (!ForceAllPlayersToSpeak(_stageMessage))
        {
            _notifiedActors.Remove(player.ActorNumber);
        }
    }

    private void SendEventMessage(string message, EventMessageKind kind)
    {
        if (!_config.ChatAnnouncementsEnabled.Value || !SemiFunc.IsMultiplayer() || !PhotonNetwork.IsMasterClient ||
            GameDirector.instance == null || GameDirector.instance.currentState != GameDirector.gameState.Main)
        {
            return;
        }
        if (_pendingEventMessages.Count == 0 && ForceAllPlayersToSpeak(message, kind: kind))
        {
            return;
        }
        EnqueueEventMessage(message, kind);
    }

    private void EnqueueEventMessage(string message, EventMessageKind kind)
    {
        int generation = _stageGeneration;
        _pendingEventMessages.RemoveAll(pending =>
            pending.Generation != generation ||
            kind == EventMessageKind.Ended ||
            pending.Kind == EventMessageKind.Countdown &&
            kind == EventMessageKind.Countdown);

        float lifetime = kind == EventMessageKind.Countdown
            ? CountdownMessageLifetimeSeconds
            : EventMessageLifetimeSeconds;
        _pendingEventMessages.Add(new PendingEventMessage(
            generation,
            message,
            kind,
            Time.realtimeSinceStartup + lifetime));
        while (_pendingEventMessages.Count > MaximumPendingEventMessages)
        {
            _pendingEventMessages.RemoveAt(0);
        }

        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Stage event chat queued behind another notification owner: {message}");
        if (_eventMessageRunnerGeneration == generation)
        {
            return;
        }
        _eventMessageRunnerGeneration = generation;
        _coroutineOwner.StartCoroutine(FlushEventMessageQueue(generation));
    }

    private IEnumerator FlushEventMessageQueue(int generation)
    {
        try
        {
            while (generation == _stageGeneration &&
                   PhotonNetwork.IsMasterClient &&
                   _pendingEventMessages.Count > 0)
            {
                float now = Time.realtimeSinceStartup;
                _pendingEventMessages.RemoveAll(pending =>
                    pending.Generation != generation || pending.ExpiresAt <= now);
                if (_pendingEventMessages.Count == 0)
                {
                    yield break;
                }
                if (GameDirector.instance == null ||
                    GameDirector.instance.currentState != GameDirector.gameState.Main)
                {
                    yield return new WaitForSecondsRealtime(
                        NotificationRetryIntervalSeconds);
                    continue;
                }

                PendingEventMessage pending = _pendingEventMessages[0];
                if (ForceAllPlayersToSpeak(
                        pending.Message,
                        logDeferral: false,
                        kind: pending.Kind))
                {
                    _pendingEventMessages.RemoveAt(0);
                }
                yield return new WaitForSecondsRealtime(
                    NotificationRetryIntervalSeconds);
            }
        }
        finally
        {
            if (_eventMessageRunnerGeneration == generation)
            {
                _eventMessageRunnerGeneration = -1;
            }
        }
    }

    private bool ForceAllPlayersToSpeak(
        string message,
        bool logDeferral = true,
        EventMessageKind? kind = null)
    {
        bool reserved = kind == EventMessageKind.Countdown
            ? NotificationWindowCoordinator.TryReserveStageFluxCountdown(2f)
            : NotificationWindowCoordinator.TryReserveStageFlux(2f);
        if (!reserved)
        {
            if (logDeferral)
            {
                StagePhysicsEventsPlugin.ModLogger.LogDebug(
                    $"Stage event chat deferred by another notification owner: {message}");
            }
            return false;
        }

        List<PlayerAvatar>? players = SemiFunc.PlayerGetList();
        if (players == null || players.Count == 0)
        {
            NotificationWindowCoordinator.ReleaseStageFluxReservation();
            return false;
        }

        int sent = 0;
        foreach (PlayerAvatar player in players)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.photonView == null)
            {
                continue;
            }

            try
            {
                // Matches EnergyCrystalAutoRefill's forced-chat approach. The master client is
                // accepted by the vanilla MasterAndOwnerOnlyRPC check for every player avatar.
                try
                {
                    // Track every Stage Flux TTS so the shared busy state remains
                    // accurate even when enemy-reaction suppression is disabled.
                    NotificationEnemyReactionGuard.Expect(player, message);
                }
                catch (Exception exception)
                {
                    StagePhysicsEventsPlugin.ModLogger.LogWarning(
                        $"Notification tracking was skipped for player view " +
                        $"{player.photonView.ViewID}: {DescribeException(exception)}");
                }
                player.ChatMessageSend(message);
                sent++;
            }
            catch (Exception exception)
            {
                NotificationEnemyReactionGuard.CancelExpected(player);
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Stage event chat failed for player view {player.photonView.ViewID}: " +
                    DescribeException(exception));
            }
        }

        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Stage event chat forced through {sent} player avatar(s): {message}");
        // ActiveNotification now owns the busy window until TTS playback ends.
        NotificationWindowCoordinator.ReleaseStageFluxReservation();
        return sent > 0;
    }

    private static string DescribeException(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }
        return $"{current.GetType().Name}: {current.Message}";
    }

}
