using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class EnemyPlayerEventAdapter
{
    private static readonly FieldInfo? EnemyHealthCurrentField = AccessTools.Field(typeof(EnemyHealth), "healthCurrent");
    private static readonly FieldInfo? EnemyHealthDeadField = AccessTools.Field(typeof(EnemyHealth), "dead");
    private static readonly FieldInfo? EnemyParentSpawnedField = AccessTools.Field(typeof(EnemyParent), "Spawned");
    private static readonly FieldInfo? PlayerDeadSetField = AccessTools.Field(typeof(PlayerAvatar), "deadSet");
    private static readonly FieldInfo? PlayerSpectatingField = AccessTools.Field(typeof(PlayerAvatar), "spectating");
    private static readonly FieldInfo? PlayerHealthValueField = AccessTools.Field(typeof(PlayerHealth), "health");
    private static readonly FieldInfo? PlayerTumbleField = AccessTools.Field(typeof(PlayerAvatar), "tumble");
    private static readonly FieldInfo? PlayerDeathHeadField =
        AccessTools.Field(typeof(PlayerAvatar), "playerDeathHead");

    private readonly StagePhysicsConfig _config;
    private readonly Dictionary<int, Enemy> _leasedEnemies = new();
    private readonly Dictionary<int, bool> _playerAlive = new();
    private readonly Dictionary<int, int> _playerRevives = new();
    private readonly Dictionary<int, float> _playerReviveReadyAt = new();
    private readonly HashSet<int> _reviveInProgress = new();
    private StageEffect _effects;
    private float _nextEnemyLeaseAt;
    private float _nextWarpAt;
    private float _nextRegenAt;
    private float _nextPurgeAt;
    private float _nextDamageAt;
    private float _nextReviveCheckAt;
    private float _nextKnockbackAt;
    private float _nextSpeedRefreshAt;
    private float _secondChanceFailureGraceUntil;

    internal EnemyPlayerEventAdapter(StagePhysicsConfig config)
    {
        _config = config;
    }

    internal bool EnsureAvailable(StageEffect effect) => effect switch
    {
        StageEffect.Freeze or StageEffect.Stun or StageEffect.EnemyWarp or
            StageEffect.EnemyRegen or StageEffect.EnemyPurge or
            StageEffect.EnemySpeedUp or StageEffect.EnemySpeedDown => true,
        StageEffect.DamagePulse or StageEffect.SecondChance or StageEffect.Knockback =>
            true,
        _ => false
    };

    internal void Begin(StageEffect effects)
    {
        Stop();
        _effects = effects;
        float now = Time.time;
        _nextEnemyLeaseAt = now;
        _nextWarpAt = now;
        _nextRegenAt = now;
        _nextPurgeAt = now;
        _nextDamageAt = now;
        _nextReviveCheckAt = now;
        _nextKnockbackAt = now;
        _nextSpeedRefreshAt = now + 1f;
        _secondChanceFailureGraceUntil = 0f;
        CapturePlayerStates();
        EnemySpeedRuntime.SetMultiplier(
            Has(StageEffect.EnemySpeedUp)
                ? _config.EnemySpeedUpPercent.Value / 100f
                : Has(StageEffect.EnemySpeedDown)
                    ? _config.EnemySpeedDownPercent.Value / 100f
                    : 1f);
        Tick();
    }

    internal void Tick()
    {
        if (_effects == StageEffect.None)
        {
            return;
        }
        float now = Time.time;
        if (now >= _nextEnemyLeaseAt &&
            (Has(StageEffect.Freeze) || Has(StageEffect.Stun)))
        {
            RefreshEnemyLeases();
            _nextEnemyLeaseAt = now + 1f;
        }
        if (Has(StageEffect.EnemyWarp) && now >= _nextWarpAt)
        {
            WarpEnemies();
            _nextWarpAt = now + _config.EnemyWarpIntervalSeconds.Value;
        }
        if (Has(StageEffect.EnemyRegen) && now >= _nextRegenAt)
        {
            RegenerateEnemies();
            _nextRegenAt = now + _config.EnemyRegenIntervalSeconds.Value;
        }
        if (Has(StageEffect.EnemyPurge) && now >= _nextPurgeAt)
        {
            DamageEnemies();
            _nextPurgeAt = now + _config.EnemyPurgeIntervalSeconds.Value;
        }
        if (Has(StageEffect.DamagePulse) && now >= _nextDamageAt)
        {
            DamagePlayers();
            _nextDamageAt = now + _config.DamagePulseIntervalSeconds.Value;
        }
        if (Has(StageEffect.SecondChance) && now >= _nextReviveCheckAt)
        {
            ReviveNewlyDeadPlayers();
            _nextReviveCheckAt = now + _config.SecondChanceCheckIntervalSeconds.Value;
        }
        if (Has(StageEffect.Knockback) && now >= _nextKnockbackAt)
        {
            KnockbackPlayers();
            _nextKnockbackAt = now + _config.KnockbackIntervalSeconds.Value;
        }
        if ((Has(StageEffect.EnemySpeedUp) || Has(StageEffect.EnemySpeedDown)) &&
            now >= _nextSpeedRefreshAt)
        {
            EnemySpeedRuntime.RefreshExistingEnemies();
            _nextSpeedRefreshAt = now + 1f;
        }
    }

    internal void Stop()
    {
        if (Has(StageEffect.Freeze) || Has(StageEffect.Stun))
        {
            foreach (Enemy enemy in _leasedEnemies.Values)
            {
                if (enemy == null)
                {
                    continue;
                }
                try
                {
                    if (Has(StageEffect.Freeze))
                    {
                        enemy.Freeze(0f);
                    }
                    EnemyStateStunned? stunned = enemy.GetComponentInChildren<EnemyStateStunned>(true);
                    if (Has(StageEffect.Stun) && stunned != null)
                    {
                        stunned.Reset();
                    }
                }
                catch (Exception exception)
                {
                    StagePhysicsEventsPlugin.ModLogger.LogDebug($"Enemy lease cleanup failed: {exception.Message}");
                }
            }
        }
        _effects = StageEffect.None;
        EnemySpeedRuntime.Clear();
        _leasedEnemies.Clear();
        _playerAlive.Clear();
        _playerRevives.Clear();
        _playerReviveReadyAt.Clear();
        _reviveInProgress.Clear();
        _secondChanceFailureGraceUntil = 0f;
    }

    private bool Has(StageEffect effect) => StageEffectSet.Contains(_effects, effect);

    private void RefreshEnemyLeases()
    {
        foreach (Enemy enemy in ActiveEnemies())
        {
            try
            {
                if (Has(StageEffect.Freeze))
                {
                    enemy.Freeze(2f);
                }
                EnemyStateStunned? stunned = enemy.GetComponentInChildren<EnemyStateStunned>(true);
                if (Has(StageEffect.Stun) && stunned != null)
                {
                    stunned.Set(2f);
                }
                _leasedEnemies[EnemyKey(enemy)] = enemy;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogDebug($"Enemy state lease failed for {enemy.name}: {exception.Message}");
            }
        }
    }

    private void WarpEnemies()
    {
        List<Enemy> enemies = ActiveEnemies();
        Shuffle(enemies);
        int count = Mathf.Min(_config.EnemyWarpEnemiesPerPulse.Value, enemies.Count);
        for (int index = 0; index < count; index++)
        {
            try
            {
                enemies[index].TeleportToPoint(_config.EnemyWarpMinimumPlayerDistance.Value, 100f);
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogDebug($"Enemy Warp skipped {enemies[index].name}: {exception.Message}");
            }
        }
    }

    private void RegenerateEnemies()
    {
        foreach (Enemy enemy in ActiveEnemies())
        {
            EnemyHealth? health = enemy.GetComponentInChildren<EnemyHealth>(true);
            if (health == null || IsEnemyDead(health))
            {
                continue;
            }
            int current = ReadEnemyHealth(health);
            int amount = Mathf.Min(_config.EnemyRegenHealAmount.Value, Mathf.Max(0, health.health - current));
            if (amount > 0)
            {
                health.Heal(amount);
            }
        }
    }

    private void DamageEnemies()
    {
        foreach (Enemy enemy in ActiveEnemies())
        {
            EnemyHealth? health = enemy.GetComponentInChildren<EnemyHealth>(true);
            if (health == null || IsEnemyDead(health))
            {
                continue;
            }
            int damage = _config.EnemyPurgeDamageAmount.Value;
            if (!_config.EnemyPurgeCanKill.Value)
            {
                damage = Mathf.Min(damage, Mathf.Max(0, ReadEnemyHealth(health) - 1));
            }
            if (damage > 0)
            {
                RoleShuffleCompatibility.RegisterNonPlayerEnemyDamage(health);
                health.Hurt(damage, Vector3.up);
            }
        }
    }

    private void DamagePlayers()
    {
        int affected = 0;
        foreach (PlayerAvatar player in LivingPlayers())
        {
            try
            {
                player.playerHealth.HurtOther(
                    _config.DamagePulseAmount.Value,
                    Vector3.zero,
                    _config.DamagePulseSavingGrace.Value);
                affected++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogDebug($"Damage Pulse skipped a player: {exception.Message}");
            }
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Damage Pulse requested {_config.DamagePulseAmount.Value} damage for {affected} player(s).");
    }

    private void CapturePlayerStates()
    {
        _playerAlive.Clear();
        _playerRevives.Clear();
        _playerReviveReadyAt.Clear();
        _reviveInProgress.Clear();
        foreach (PlayerAvatar player in AllPlayers())
        {
            int key = PlayerKey(player);
            _playerAlive[key] = IsPlayerAlive(player);
            _playerRevives[key] = 0;
        }
    }

    internal bool TryReviveForLevelFailure()
    {
        if (!Has(StageEffect.SecondChance))
        {
            return false;
        }
        bool pending = HasPendingRevive();
        if (!pending)
        {
            _secondChanceFailureGraceUntil = 0f;
            return false;
        }
        if (_secondChanceFailureGraceUntil <= 0f)
        {
            _secondChanceFailureGraceUntil = Time.time + 5f;
        }
        int revived = ReviveNewlyDeadPlayers();
        if (revived > 0)
        {
            _secondChanceFailureGraceUntil = 0f;
            return true;
        }
        return Time.time < _secondChanceFailureGraceUntil;
    }

    internal bool WillHandleSecondChance(PlayerAvatar? player)
    {
        if (player == null || !Has(StageEffect.SecondChance) ||
            IsPlayerAlive(player) ||
            RoleShuffleCompatibility.HasImmediateCorrectiveRevival(player))
        {
            return false;
        }

        int key = PlayerKey(player);
        int used = _playerRevives.TryGetValue(key, out int count) ? count : 0;
        if (used >= _config.SecondChanceMaxRevivesPerPlayer.Value)
        {
            return false;
        }
        if (_reviveInProgress.Contains(key))
        {
            return true;
        }
        return _playerAlive.TryGetValue(key, out bool wasAlive) && wasAlive;
    }

    private int ReviveNewlyDeadPlayers()
    {
        int revived = 0;
        HashSet<int> seen = new();
        foreach (PlayerAvatar player in AllPlayers())
        {
            int key = PlayerKey(player);
            seen.Add(key);
            bool alive = IsPlayerAlive(player);
            bool wasAlive = _playerAlive.TryGetValue(key, out bool previous) && previous;
            int used = _playerRevives.TryGetValue(key, out int count) ? count : 0;
            bool waitingForRevive = false;
            if (alive)
            {
                _playerReviveReadyAt.Remove(key);
                _reviveInProgress.Remove(key);
            }
            if (wasAlive && !alive && used < _config.SecondChanceMaxRevivesPerPlayer.Value)
            {
                if (RoleShuffleCompatibility.HasImmediateCorrectiveRevival(player))
                {
                    waitingForRevive = true;
                    _playerAlive[key] = true;
                    continue;
                }
                if (!_playerReviveReadyAt.TryGetValue(key, out float readyAt))
                {
                    readyAt = Time.time + 2f;
                    _playerReviveReadyAt[key] = readyAt;
                    StagePhysicsEventsPlugin.ModLogger.LogDebug(
                        $"Second Chance scheduled player view={player.photonView?.ViewID ?? 0} for revival in 2 seconds.");
                }
                bool deathHeadReady = PlayerDeathHeadField?.GetValue(player) is PlayerDeathHead deathHead &&
                                      deathHead != null;
                if (Time.time < readyAt || !deathHeadReady)
                {
                    waitingForRevive = true;
                    _playerAlive[key] = true;
                    continue;
                }
                try
                {
                    _reviveInProgress.Add(key);
                    player.Revive(false);
                    alive = IsPlayerAlive(player);
                    if (alive)
                    {
                        _playerRevives[key] = used + 1;
                        _playerReviveReadyAt.Remove(key);
                        revived++;
                        RoleShuffleCompatibility.NotifyExternalRevival(player);
                        StagePhysicsEventsPlugin.ModLogger.LogInfo(
                            $"Second Chance revived player view={player.photonView?.ViewID ?? 0} ({used + 1}/{_config.SecondChanceMaxRevivesPerPlayer.Value}).");
                    }
                    else
                    {
                        waitingForRevive = true;
                        StagePhysicsEventsPlugin.ModLogger.LogDebug(
                            "Second Chance is waiting for the player's vanilla death head to initialize.");
                    }
                }
                catch (Exception exception)
                {
                    waitingForRevive = true;
                    StagePhysicsEventsPlugin.ModLogger.LogWarning($"Second Chance revive failed: {exception.Message}");
                }
                finally
                {
                    _reviveInProgress.Remove(key);
                }
            }
            // Preserve the pre-death state while the vanilla death head is being created.
            // Otherwise the next check sees wasAlive=false and never retries the revive.
            _playerAlive[key] = waitingForRevive ? true : alive;
            if (!_playerRevives.ContainsKey(key))
            {
                _playerRevives[key] = 0;
            }
        }
        foreach (int key in new List<int>(_playerAlive.Keys))
        {
            if (!seen.Contains(key))
            {
                _playerAlive.Remove(key);
                _playerRevives.Remove(key);
                _playerReviveReadyAt.Remove(key);
                _reviveInProgress.Remove(key);
            }
        }
        return revived;
    }

    private bool HasPendingRevive()
    {
        foreach (PlayerAvatar player in AllPlayers())
        {
            int key = PlayerKey(player);
            bool alive = IsPlayerAlive(player);
            bool wasAlive = _playerAlive.TryGetValue(key, out bool previous) && previous;
            int used = _playerRevives.TryGetValue(key, out int count) ? count : 0;
            if (wasAlive && !alive && used < _config.SecondChanceMaxRevivesPerPlayer.Value)
            {
                return true;
            }
        }
        return false;
    }

    private void KnockbackPlayers()
    {
        int affected = 0;
        foreach (PlayerAvatar player in LivingPlayers())
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized;
            Vector3 force = new(
                circle.x * _config.KnockbackHorizontalForce.Value,
                _config.KnockbackVerticalForce.Value,
                circle.y * _config.KnockbackHorizontalForce.Value);
            force *= 2f;
            PlayerTumble? tumble = PlayerTumbleField?.GetValue(player) as PlayerTumble;
            if (tumble != null)
            {
                tumble.TumbleRequest(_isTumbling: true, _playerInput: false);
                tumble.TumbleForce(force);
                tumble.TumbleOverrideTime(1.25f);
            }
            player.ForceImpulse(force);
            affected++;
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Knockback applied a tumbling impulse to {affected} player(s).");
    }

    private static List<Enemy> ActiveEnemies()
    {
        List<Enemy> result = new();
        foreach (Enemy enemy in Resources.FindObjectsOfTypeAll<Enemy>())
        {
            EnemyParent? parent = enemy != null ? enemy.GetComponentInParent<EnemyParent>() : null;
            EnemyHealth? health = enemy != null ? enemy.GetComponentInChildren<EnemyHealth>(true) : null;
            bool spawned = parent != null &&
                (EnemyParentSpawnedField?.GetValue(parent) is not bool value || value);
            if (enemy != null && enemy.gameObject.scene.IsValid() && enemy.gameObject.activeInHierarchy &&
                spawned && (health == null || !IsEnemyDead(health)))
            {
                result.Add(enemy);
            }
        }
        return result;
    }

    private static List<PlayerAvatar> AllPlayers()
    {
        List<PlayerAvatar> result = new();
        if (GameDirector.instance == null)
        {
            return result;
        }
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
        {
            // Dead players are disabled before their death head becomes available.
            // Keep them in the state table so Second Chance can retry after that transition.
            if (player != null)
            {
                result.Add(player);
            }
        }
        return result;
    }

    private static List<PlayerAvatar> LivingPlayers()
    {
        List<PlayerAvatar> result = new();
        foreach (PlayerAvatar player in AllPlayers())
        {
            if (IsPlayerAlive(player))
            {
                result.Add(player);
            }
        }
        return result;
    }

    private static bool IsPlayerAlive(PlayerAvatar player)
    {
        if (player.playerHealth == null)
        {
            return false;
        }
        if (PlayerDeadSetField?.GetValue(player) is bool dead && dead)
        {
            return false;
        }
        if (PlayerSpectatingField?.GetValue(player) is bool spectating && spectating)
        {
            return false;
        }
        return PlayerHealthValueField?.GetValue(player.playerHealth) is not int health || health > 0;
    }

    private static bool IsEnemyDead(EnemyHealth health) =>
        EnemyHealthDeadField?.GetValue(health) is bool dead && dead;

    private static int ReadEnemyHealth(EnemyHealth health) =>
        EnemyHealthCurrentField?.GetValue(health) is int current ? current : health.health;

    private static int EnemyKey(Enemy enemy) => enemy.photonView != null && enemy.photonView.ViewID != 0
        ? enemy.photonView.ViewID
        : enemy.GetInstanceID();

    private static int PlayerKey(PlayerAvatar player) => player.photonView != null && player.photonView.ViewID != 0
        ? player.photonView.ViewID
        : player.GetInstanceID();

    private static void Shuffle<T>(List<T> list)
    {
        for (int index = 0; index < list.Count; index++)
        {
            int selected = UnityEngine.Random.Range(index, list.Count);
            (list[index], list[selected]) = (list[selected], list[index]);
        }
    }
}
