using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using IEnumerator = System.Collections.IEnumerator;

namespace REPOJP.StagePhysicsEvents;

internal sealed class StagePhysicsEventController : MonoBehaviourPunCallbacks
{
    internal const string UiCapabilityKey = "SPE.UI";
    private const string VersionKey = "SPE.Version";
    private const string StageIdKey = "SPE.StageId";
    private const string StateKey = "SPE.State";
    private const string EffectKey = "SPE.Effect";
    private const string DurationKey = "SPE.DurationSeconds";
    private const string IntervalKey = "SPE.IntervalSeconds";
    private const string EndTimestampKey = "SPE.EndServerTimestamp";
    private const string PreviewEffectKey = "SPE.PreviewEffect";
    private const string SlotLimitKey = "SPE.SlotLimit";
    private const string PhaseDurationKey = "SPE.PhaseDurationSeconds";
    private const string ModeKey = "SPE.Mode";
    private const int StateFormatVersion = 11;
    private const int PersistentEffectDurationSeconds = 300;
    private const int PersistentRefreshLeadSeconds = 10;
    private const int IntervalPlanningSeconds = 10;
    private const int EffectNameDisplaySeconds = 3;
    private const int StartCountdownSeconds = 5;
    private const int EndCountdownSeconds = 3;
    private const float DynamicTargetScanIntervalSeconds = 0.25f;
    private const float RollPlayerSteeringSpeed = 10f;
    private const float RollPlayerSteeringAcceleration = 30f;
    private static readonly FieldInfo? PlayerDeadSetField =
        AccessTools.Field(typeof(PlayerAvatar), "deadSet");
    private static readonly FieldInfo? PlayerSpectatingField =
        AccessTools.Field(typeof(PlayerAvatar), "spectating");
    private static readonly FieldInfo? PlayerHealthValueField =
        AccessTools.Field(typeof(PlayerHealth), "health");
    private static readonly FieldInfo? ExtractionPointsCompletedField =
        AccessTools.Field(typeof(RoundDirector), "extractionPointsCompleted");

    private StagePhysicsConfig _config = null!;
    private VanillaEffectResolver _resolver = null!;
    private FeatherOrbEffectAdapter _feather = null!;
    private StaffAffectBroadcaster _zeroGravity = null!;
    private AdditionalOrbEffectAdapter _batteryOrb = null!;
    private AdditionalOrbEffectAdapter _healOrb = null!;
    private AdditionalOrbEffectAdapter _indestructibleOrb = null!;
    private StaffAffectBroadcaster _rollStaff = null!;
    private VoidStaffEffectAdapter _void = null!;
    private VanillaSpawnEffectResolver _spawnResolver = null!;
    private RandomStagePointSelector _stagePointSelector = null!;
    private LevitationEventAdapter _levitation = null!;
    private GumballHypnosisEventAdapter _gumballHypnosis = null!;
    private HealingAuraEventAdapter _healingAura = null!;
    private StarBarrageEventAdapter _starBarrage = null!;
    private SpiderScareEventAdapter _spiderScare = null!;
    private TrafficShockEventAdapter _trafficShock = null!;
    private FlickerEventAdapter _flicker = null!;
    private EnemyHuntEventAdapter _enemyHunt = null!;
    private DangerousValuablesEventAdapter _dangerousValuables = null!;
    private VanillaGrenadeEventAdapter _shockwave = null!;
    private VanillaGrenadeEventAdapter _stunBlast = null!;
    private VanillaGrenadeEventAdapter _explosionRain = null!;
    private EnemyWaveEventAdapter _enemyWave = null!;
    private MinefieldEventAdapter _minefield = null!;
    private EnemyPlayerEventAdapter _enemyPlayerEvents = null!;
    private StageWorldEventAdapter _worldEvents = null!;
    private ExtendedEventAdapter _extendedEvents = null!;
    private ValuableIndestructibleProtection _valuableProtection = null!;
    private VanillaEventNotifier _notifier = null!;

    private EventRunState _state;
    private StageEffect _effect;
    private float _stateEndsAt;
    private int _durationSeconds;
    private int _intervalSeconds;
    private int _stageToken;
    private int _stageGeneration;
    private int _levelGeneratorInstanceId;
    private bool _stageReady;
    private bool _wasAuthority;
    private TargetFilter _activeTargetFilter;
    private EventMode _mode;
    private bool _stageEffectsEnabled;
    private StageEffect _fixedEffect;
    private StageEffect _plannedEffect;
    private int _plannedDurationSeconds;
    private int _plannedNextIntervalSeconds;
    private bool _intervalPlanPrepared;
    private bool _intervalNameAnnounced;
    private int _lastStartCountdownSecond;
    private int _stageSlotLimit = 3;
    private int _fixedDurationSeconds;
    private int _fixedIntervalSeconds;
    private int _lastExtractionDirectorId;
    private int _lastExtractionCompletionCount = -1;

    private EventRunState _remoteState;
    private StageEffect _remoteEffect;
    private StageEffect _remotePreviewEffect;
    private EventMode _remoteMode;
    private int _remoteSlotLimit = 3;
    private int _remotePhaseDuration;
    private int _remoteDuration;
    private int _remoteInterval;
    private int _remoteEndTimestamp;
    private int _remoteStageToken;
    private Coroutine? _endCountdownCoroutine;
    private Coroutine? _effectApplicationCoroutine;
    private int _countdownGeneration;
    private int _effectApplicationGeneration;
    private readonly HashSet<int> _activeTargetIds = new();
    private readonly Dictionary<int, bool> _playerAliveStates = new();
    private float _nextDynamicTargetScanAt;
    private float _nextVoidSpawnAt;

    internal void Initialize(StagePhysicsConfig config)
    {
        _config = config;
        _resolver = new VanillaEffectResolver();
        _feather = new FeatherOrbEffectAdapter(this, _resolver);
        _zeroGravity = new StaffAffectBroadcaster(this, _resolver, StaffAffectKind.ZeroGravity);
        _batteryOrb = new AdditionalOrbEffectAdapter(this, _resolver, config);
        _healOrb = new AdditionalOrbEffectAdapter(this, _resolver, config);
        _indestructibleOrb = new AdditionalOrbEffectAdapter(this, _resolver, config);
        _rollStaff = new StaffAffectBroadcaster(this, _resolver, StaffAffectKind.Roll);
        _void = new VoidStaffEffectAdapter(this, _resolver);
        _spawnResolver = new VanillaSpawnEffectResolver();
        _stagePointSelector = new RandomStagePointSelector();
        _levitation = new LevitationEventAdapter(this, _spawnResolver, _stagePointSelector);
        _gumballHypnosis = new GumballHypnosisEventAdapter(this, _spawnResolver);
        _healingAura = new HealingAuraEventAdapter(this, _spawnResolver, _stagePointSelector);
        _starBarrage = new StarBarrageEventAdapter(this, _spawnResolver, _stagePointSelector);
        _spiderScare = new SpiderScareEventAdapter(this, _spawnResolver);
        _trafficShock = new TrafficShockEventAdapter(this, _spawnResolver);
        _flicker = new FlickerEventAdapter(this, _spawnResolver);
        _enemyHunt = new EnemyHuntEventAdapter(this, _spawnResolver);
        _dangerousValuables = new DangerousValuablesEventAdapter(config);
        _shockwave = new VanillaGrenadeEventAdapter(
            this, _spawnResolver, _stagePointSelector, StageEffect.Shockwave);
        _stunBlast = new VanillaGrenadeEventAdapter(
            this, _spawnResolver, _stagePointSelector, StageEffect.StunBlast);
        _explosionRain = new VanillaGrenadeEventAdapter(
            this, _spawnResolver, _stagePointSelector, StageEffect.ExplosionRain);
        _enemyWave = new EnemyWaveEventAdapter(this, config);
        _minefield = new MinefieldEventAdapter(this, _spawnResolver, _stagePointSelector, config);
        _enemyPlayerEvents = new EnemyPlayerEventAdapter(config);
        _worldEvents = new StageWorldEventAdapter(config);
        _extendedEvents = new ExtendedEventAdapter(this, config, _stagePointSelector);
        _valuableProtection = new ValuableIndestructibleProtection();
        _notifier = new VanillaEventNotifier(this, config);
        _state = EventRunState.Inactive;
    }

    internal void StageReady(LevelGenerator generator)
    {
        if (!IsPlayableStageContext())
        {
            StageEnding();
            return;
        }

        int instanceId = generator != null ? generator.GetInstanceID() : 0;
        if (_stageReady && _levelGeneratorInstanceId == instanceId)
        {
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        StopLocalEffects();
        _valuableProtection.Stop();
        _stageReady = true;
        _levelGeneratorInstanceId = instanceId;
        _extendedEvents.ResetStage();
        _stageGeneration++;
        _lastExtractionDirectorId = 0;
        _lastExtractionCompletionCount = -1;
        _stageToken = unchecked((SceneManager.GetActiveScene().handle * 397) ^ _stageGeneration ^ PhotonNetwork.ServerTimestamp);
        PhysGrabObjectRegistry.Clear();
        PhysGrabObjectRegistry.SeedFromScene();
        SetLocalUiCapability();
        ConsumeRoomState();

        if (IsAuthority() && _config.Enabled.Value)
        {
            _config.LogProbabilityPolicy();
            PrepareStagePlan();
            _notifier.BeginStage(_stageGeneration, BuildStageStartMessage());
        }
        else
        {
            _state = EventRunState.Inactive;
        }
    }

    internal void StageEnding()
    {
        _extendedEvents?.ResetStage();
        if (!_stageReady && _state == EventRunState.Inactive)
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
            return;
        }
        if (IsAuthority())
        {
            Publish(EventRunState.Inactive, StageEffect.None, 0, 0, 0);
        }
        StopLocalEffects();
        _valuableProtection.Stop();
        _notifier.EndStage();
        _stageReady = false;
        _state = EventRunState.Inactive;
        _effect = StageEffect.None;
        _stageEffectsEnabled = false;
        _fixedEffect = StageEffect.None;
        _plannedEffect = StageEffect.None;
        _plannedDurationSeconds = 0;
        _plannedNextIntervalSeconds = 0;
        _intervalPlanPrepared = false;
        _intervalNameAnnounced = false;
        _lastExtractionDirectorId = 0;
        _lastExtractionCompletionCount = -1;
        _remoteState = EventRunState.Inactive;
        PhysGrabObjectRegistry.Clear();
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    internal bool TryPreventSecondChanceRetake()
    {
        if (!IsAuthority() ||
            !_stageReady ||
            _state != EventRunState.Active ||
            !StageEffectSet.Contains(_effect, StageEffect.SecondChance))
        {
            return false;
        }
        return _enemyPlayerEvents.TryReviveForLevelFailure();
    }

    internal float GetValuableValueMultiplier(ValuableObject? valuable)
    {
        if (!IsAuthority() || !_stageReady || _state != EventRunState.Active ||
            valuable == null ||
            (!StageEffectSet.Contains(_effect, StageEffect.ValueSurge) &&
             !StageEffectSet.Contains(_effect, StageEffect.ValueCrash)))
        {
            return 1f;
        }
        return _worldEvents.GetValuableValueMultiplier(valuable);
    }

    internal bool WillHandleSecondChance(PlayerAvatar? player)
    {
        if (!IsAuthority() || !_stageReady || _state != EventRunState.Active ||
            player == null ||
            !StageEffectSet.Contains(_effect, StageEffect.SecondChance))
        {
            return false;
        }
        return _enemyPlayerEvents.WillHandleSecondChance(player);
    }

    internal void Shutdown()
    {
        StageEnding();
    }

    internal void RefreshFragilityForPhysics(PhysGrabObjectImpactDetector impactDetector)
    {
        if (!IsAuthority() ||
            !_stageReady ||
            _state != EventRunState.Active ||
            !StageEffectSet.Contains(_effect, StageEffect.Fragility) ||
            impactDetector == null)
        {
            return;
        }

        PhysGrabObject? target = impactDetector.GetComponent<PhysGrabObject>();
        if (target == null ||
            target.GetComponent<ValuableObject>() == null ||
            StagePhysicsEffectCarrierUtility.IsInternalCarrier(target))
        {
            return;
        }

        float multiplier = _config.FragilityMultiplierPercent.Value / 100f;

        // OverrideFragility is a short-lived vanilla override. Set the detector
        // value as well so this host-authoritative FixedUpdate uses the requested
        // multiplier even if another Update reset the vanilla timer first.
        target.OverrideFragility(multiplier);
        impactDetector.fragilityMultiplier = multiplier;
    }

    internal HashSet<int>? CaptureTaxReturnValuablesBeforeSpawn()
    {
        if (!IsAuthority() || !_stageReady || _state != EventRunState.Active)
        {
            return null;
        }
        return _worldEvents.CaptureTaxReturnValuableKeys();
    }

    internal void ExcludeTaxReturnValuablesAfterSpawn(HashSet<int>? keysBeforeSpawn)
    {
        if (!IsAuthority() || !_stageReady || _state != EventRunState.Active)
        {
            return;
        }
        _worldEvents.ExcludeNewTaxReturnValuables(keysBeforeSpawn);
    }

    internal bool TryGetHudState(out HudState hudState)
    {
        hudState = default;
        if (!_stageReady || !IsHudStageActive())
        {
            return false;
        }

        if (IsAuthority())
        {
            if (_state == EventRunState.Inactive)
            {
                return false;
            }
            int remaining = Mathf.Max(0, Mathf.CeilToInt(_stateEndsAt - Time.time));
            int phaseDuration = GetPhaseDuration(_state, _durationSeconds, _intervalSeconds);
            hudState = new HudState(
                _state,
                _effect,
                _plannedEffect,
                _mode,
                _durationSeconds,
                _intervalSeconds,
                remaining,
                phaseDuration,
                GetHudSlotLimit(_state, _effect, _plannedEffect, _stageSlotLimit));
            return true;
        }

        if (_remoteState == EventRunState.Inactive || _remoteStageToken == 0)
        {
            return false;
        }
        int remainingMilliseconds = unchecked(_remoteEndTimestamp - PhotonNetwork.ServerTimestamp);
        int remoteRemaining = Mathf.Max(0, Mathf.CeilToInt(remainingMilliseconds / 1000f));
        hudState = new HudState(
            _remoteState,
            _remoteEffect,
            _remotePreviewEffect,
            _remoteMode,
            _remoteDuration,
            _remoteInterval,
            remoteRemaining,
            _remotePhaseDuration,
            _remoteSlotLimit);
        return true;
    }

    internal void RefreshMenuDisplay()
    {
        SetLocalUiCapability();
        if (!IsAuthority()) ConsumeRoomState();
    }

    private void Update()
    {
        DeferredObjectCleanupQueue.ProcessFrame();
        if (!_stageReady)
        {
            return;
        }
        if (!IsPlayableStageContext())
        {
            StageEnding();
            return;
        }

        bool authority = IsAuthority();
        if (_wasAuthority && !authority)
        {
            StopLocalEffects();
            _valuableProtection.Stop();
            _state = EventRunState.Inactive;
        }
        _wasAuthority = authority;

        if (!authority)
        {
            return;
        }
        if (!_config.Enabled.Value)
        {
            if (_state != EventRunState.Inactive)
            {
                StopLocalEffects();
                _valuableProtection.Stop();
                _state = EventRunState.Inactive;
                Publish(EventRunState.Inactive, StageEffect.None, 0, 0, 0);
            }
            return;
        }
        RefreshStageSlotLimit();
        _valuableProtection.Tick();
        if (!_stageEffectsEnabled)
        {
            return;
        }
        if (GameDirector.instance == null || GameDirector.instance.currentState != GameDirector.gameState.Main)
        {
            return;
        }
        if (_state == EventRunState.Inactive)
        {
            if (_mode == EventMode.PersistentForStage)
            {
                BeginPersistentEvent();
            }
            else
            {
                BeginWaiting(NextIntervalSeconds());
            }
            return;
        }

        if (_state == EventRunState.Waiting)
        {
            UpdateWaitingPhase();
            if (Time.time < _stateEndsAt)
            {
                return;
            }

            if (!_intervalPlanPrepared)
            {
                PrepareIntervalPlan();
            }
            StageEffect selected = FilterAvailableEffects(_plannedEffect);
            if (selected == StageEffect.None)
            {
                int retryInterval = _plannedNextIntervalSeconds > 0
                    ? _plannedNextIntervalSeconds
                    : NextIntervalSeconds();
                BeginWaiting(retryInterval);
                return;
            }
            BeginEvent(
                selected,
                _plannedDurationSeconds,
                _plannedNextIntervalSeconds);
            return;
        }

        if (_state == EventRunState.Active)
        {
            if (_effectApplicationCoroutine != null)
            {
                return;
            }
            _feather.TickFollowOrbs();
            _batteryOrb.Tick();
            _healOrb.Tick();
            _indestructibleOrb.Tick();
            RefreshDynamicTargets();
            RefreshVoidEffects();
            float remainingSeconds = Mathf.Max(0f, _stateEndsAt - Time.time);
            _levitation.Tick(remainingSeconds);
            _shockwave.Tick(remainingSeconds);
            _stunBlast.Tick(remainingSeconds);
            _explosionRain.Tick(remainingSeconds);
            _enemyWave.Tick(remainingSeconds);
            _minefield.Tick(remainingSeconds);
            _gumballHypnosis.Tick();
            _healingAura.Tick(remainingSeconds);
            _starBarrage.Tick(remainingSeconds);
            _spiderScare.Tick(remainingSeconds);
            _trafficShock.Tick(remainingSeconds);
            _flicker.Tick(remainingSeconds);
            _enemyHunt.Tick(remainingSeconds);
            _dangerousValuables.Tick(remainingSeconds);
            _enemyPlayerEvents.Tick();
            _worldEvents.Tick();
            _extendedEvents.Tick();
            if (_mode == EventMode.PersistentForStage)
            {
                if (Time.time >= _stateEndsAt - PersistentRefreshLeadSeconds)
                {
                    RefreshPersistentEvent();
                }
                return;
            }
            if (_endCountdownCoroutine != null)
            {
                return;
            }
            if (Time.time >= _stateEndsAt)
            {
                EndEventAndBeginWaiting();
                return;
            }
            if (_config.EndCountdownEnabled.Value && Time.time >= _stateEndsAt - EndCountdownSeconds)
            {
                BeginEndCountdown();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!_stageReady || _state != EventRunState.Active || !IsAuthority() ||
            !_activeTargetFilter.Players || !StageEffectSet.Contains(_effect, StageEffect.Roll) ||
            GameDirector.instance == null)
        {
            return;
        }

        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
        {
            if (!IsPlayerAlive(player))
            {
                continue;
            }
            PhysGrabObject? playerObject = player.GetComponentInChildren<PhysGrabObject>(true);
            Rigidbody? rigidbody = playerObject != null ? playerObject.rb : null;
            Transform? cameraTransform = player.localCamera != null
                ? player.localCamera.GetOverrideTransform()
                : null;
            if (rigidbody == null || cameraTransform == null)
            {
                continue;
            }

            Vector3 viewDirection = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (viewDirection.sqrMagnitude < 0.0001f)
            {
                continue;
            }
            viewDirection.Normalize();

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(rigidbody.velocity, Vector3.up);
            Vector3 desiredVelocity = viewDirection * RollPlayerSteeringSpeed;
            Vector3 requiredAcceleration = (desiredVelocity - horizontalVelocity) /
                Mathf.Max(Time.fixedDeltaTime, 0.001f);
            requiredAcceleration = Vector3.ClampMagnitude(
                requiredAcceleration,
                RollPlayerSteeringAcceleration);
            rigidbody.AddForce(requiredAcceleration, ForceMode.Acceleration);
        }
    }

    private bool CanApply(StageEffect effects)
    {
        if (effects == StageEffect.None || !StageEffectSet.IsValid(effects))
        {
            return false;
        }

        return FilterAvailableEffects(effects) == effects;
    }

    private StageEffect FilterAvailableEffects(StageEffect effects)
    {
        if (effects == StageEffect.None || !StageEffectSet.IsValid(effects))
        {
            return StageEffect.None;
        }
        StageEffect availableEffects = StageEffect.None;
        foreach (StageEffect effect in StageEffectSet.IndividualEffects)
        {
            if (!StageEffectSet.Contains(effects, effect))
            {
                continue;
            }
            bool available = effect switch
            {
                StageEffect.Feather => _feather.EnsureAvailable(),
                StageEffect.ZeroGravity => _zeroGravity.EnsureAvailable(),
                StageEffect.Battery => _batteryOrb.EnsureAvailable(effect),
                StageEffect.Heal => _healOrb.EnsureAvailable(effect),
                StageEffect.Indestructible => _indestructibleOrb.EnsureAvailable(effect),
                StageEffect.Roll => _rollStaff.EnsureAvailable(),
                StageEffect.Void => _void.EnsureAvailable(),
                StageEffect.Levitation => _levitation.EnsureAvailable(),
                StageEffect.Shockwave => _shockwave.EnsureAvailable(),
                StageEffect.StunBlast => _stunBlast.EnsureAvailable(),
                StageEffect.ExplosionRain => _explosionRain.EnsureAvailable(),
                StageEffect.EnemyWave => _enemyWave.EnsureAvailable(),
                StageEffect.Minefield => _minefield.EnsureAvailable(),
                StageEffect.GumballHypnosis => _gumballHypnosis.EnsureAvailable(),
                StageEffect.HealingAura => _healingAura.EnsureAvailable(),
                StageEffect.StarBarrage => _starBarrage.EnsureAvailable(),
                StageEffect.SpiderScare => _spiderScare.EnsureAvailable(),
                StageEffect.TrafficShock => _trafficShock.EnsureAvailable(),
                StageEffect.Flicker => _flicker.EnsureAvailable(),
                StageEffect.EnemyHunt => _enemyHunt.EnsureAvailable(),
                StageEffect.DangerousValuables => _dangerousValuables.EnsureAvailable(),
                StageEffect.Restoration or StageEffect.BatteryDrain or StageEffect.HeavyCargo or
                    StageEffect.Butterfingers or StageEffect.EnemyBlindness or StageEffect.EnemyArmor or
                    StageEffect.EnemyVulnerability or StageEffect.SupplyDrop or StageEffect.PlayerSwap or
                    StageEffect.SharedPain => _extendedEvents.EnsureAvailable(effect),
                StageEffect.Freeze or StageEffect.Stun or StageEffect.EnemyWarp or
                    StageEffect.EnemyRegen or StageEffect.EnemyPurge or
                    StageEffect.EnemySpeedUp or StageEffect.EnemySpeedDown or
                    StageEffect.DamagePulse or StageEffect.SecondChance or StageEffect.Knockback =>
                    _enemyPlayerEvents.EnsureAvailable(effect),
                StageEffect.Quake or StageEffect.DoorChaos or StageEffect.ValueSurge or
                    StageEffect.ValueCrash or StageEffect.Fragility =>
                    _worldEvents.EnsureAvailable(effect, _config.CaptureTargetFilter()),
                _ => false
            };
            if (!available)
            {
                if ((effect & ExtendedEventCatalog.All) != StageEffect.None)
                    StagePhysicsEventsPlugin.ModLogger.LogDebug(
                        $"Skipping {effect}: its target, supply budget, or game capability is not currently available.");
                else
                    StagePhysicsEventsPlugin.ModLogger.LogWarning(
                        $"Selected {effect}, but its vanilla effect path is unavailable; removing it from the combined event.");
                continue;
            }
            availableEffects |= effect;
        }
        return availableEffects;
    }

    private void PrepareStagePlan()
    {
        _mode = _config.CaptureMode();
        _stageSlotLimit = Mathf.Clamp(_config.MaxSimultaneousEffects.Value, 1, 5);
        _stageEffectsEnabled = _config.RollStageEnabled() && _config.HasSelectableEffect;
        _fixedEffect = StageEffect.None;
        _plannedEffect = StageEffect.None;
        _plannedDurationSeconds = 0;
        _plannedNextIntervalSeconds = 0;
        _intervalPlanPrepared = false;
        _intervalNameAnnounced = false;
        _lastStartCountdownSecond = StartCountdownSeconds + 1;
        _fixedDurationSeconds = 0;
        _fixedIntervalSeconds = 0;

        if (_stageEffectsEnabled && UsesStageFixedEffect())
        {
            _fixedEffect = FilterAvailableEffects(_config.RollEffects(_stageSlotLimit));
            if (_mode == EventMode.PersistentForStage)
            {
                _fixedDurationSeconds = PersistentEffectDurationSeconds;
                _fixedIntervalSeconds = 0;
            }
            else
            {
                _fixedDurationSeconds = _config.RollDurationSeconds();
                _fixedIntervalSeconds = _config.RollIntervalSeconds();
            }
            _stageEffectsEnabled = _fixedEffect != StageEffect.None && CanApply(_fixedEffect);
        }

        if (_stageEffectsEnabled)
        {
            StartCoroutine(ResolveEnabledEffects(_stageGeneration));
            _state = EventRunState.Inactive;
            _effect = StageEffect.None;
            Publish(EventRunState.Inactive, StageEffect.None, 0, 0, 0);
            StagePhysicsEventsPlugin.ModLogger.LogInfo(
                UsesStageFixedEffect()
                    ? $"Stage plan enabled: mode={_mode}, effect={_fixedEffect}, duration={_fixedDurationSeconds}s, interval={_fixedIntervalSeconds}s."
                    : $"Stage plan enabled: mode={_mode}.");
        }
        else
        {
            StopLocalEffects();
            _state = EventRunState.Inactive;
            _effect = StageEffect.None;
            Publish(EventRunState.Inactive, StageEffect.None, 0, 0, 0);
            StagePhysicsEventsPlugin.ModLogger.LogInfo("Stage event activation roll failed or no selectable effect exists; effects and HUD are disabled for this stage.");
        }
    }

    private int NextIntervalSeconds() =>
        UsesFixedTiming() ? _fixedIntervalSeconds : _config.RollIntervalSeconds();

    private void RefreshStageSlotLimit()
    {
        int configuredLimit = Mathf.Clamp(
            _config.MaxSimultaneousEffects.Value,
            1,
            5);
        if (configuredLimit == _stageSlotLimit)
        {
            return;
        }

        int previousLimit = _stageSlotLimit;
        _stageSlotLimit = configuredLimit;
        StagePhysicsEventsPlugin.ModLogger.LogInfo(
            $"MaxSimultaneousEffects changed during the stage: " +
            $"{previousLimit} -> {_stageSlotLimit}. The new limit applies to the next roll.");

        if (_state == EventRunState.Inactive)
        {
            return;
        }

        int remaining = Mathf.Max(0, Mathf.CeilToInt(_stateEndsAt - Time.time));
        Publish(
            _state,
            _effect,
            _durationSeconds,
            _intervalSeconds,
            remaining,
            GetPhaseDuration(_state, _durationSeconds, _intervalSeconds));
    }

    private bool UsesFixedTiming() =>
        _mode == EventMode.FixedForStage || _mode == EventMode.FixedPerExtraction;

    private bool UsesStageFixedEffect() =>
        UsesFixedTiming() || _mode == EventMode.PersistentForStage;

    internal void ExtractionCompleted(RoundDirector? director)
    {
        if (!_stageReady || !IsAuthority() || !_config.Enabled.Value ||
            !_stageEffectsEnabled || _mode != EventMode.FixedPerExtraction)
        {
            return;
        }

        int directorId = director != null ? director.GetInstanceID() : 0;
        int completionCount = ReadExtractionCompletionCount(director);
        if (directorId == _lastExtractionDirectorId && completionCount == _lastExtractionCompletionCount)
        {
            return;
        }
        _lastExtractionDirectorId = directorId;
        _lastExtractionCompletionCount = completionCount;

        StageEffect selected = FilterAvailableEffects(_config.RollEffects(_stageSlotLimit));
        _fixedEffect = selected;
        StagePhysicsEventsPlugin.ModLogger.LogInfo(
            $"Completed extraction #{completionCount}; FixedPerExtraction selected " +
            $"the effect for the next unplanned interval: {_fixedEffect}.");
    }

    private int ReadExtractionCompletionCount(RoundDirector? director)
    {
        try
        {
            if (director != null && ExtractionPointsCompletedField?.GetValue(director) is int count)
            {
                return count;
            }
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Could not read the completed extraction count: {exception.Message}");
        }
        return _lastExtractionCompletionCount + 1;
    }

    private IEnumerator ResolveEnabledEffects(int generation)
    {
        for (int attempt = 0; attempt < 5 && _stageReady && generation == _stageGeneration && IsAuthority(); attempt++)
        {
            bool ready = true;
            foreach (StageEffect effect in StageEffectSet.IndividualEffects)
            {
                bool needed = UsesStageFixedEffect()
                    ? StageEffectSet.Contains(_fixedEffect, effect)
                    : _config.IsEffectSelectable(effect);
                if (needed && !CanApply(effect))
                {
                    ready = false;
                }
            }
            if (ready)
            {
                yield break;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private void BeginWaiting(int intervalSeconds)
    {
        StopLocalEffects();
        _state = EventRunState.Waiting;
        _effect = StageEffect.None;
        _plannedEffect = StageEffect.None;
        _plannedDurationSeconds = 0;
        _plannedNextIntervalSeconds = 0;
        _intervalPlanPrepared = false;
        _intervalNameAnnounced = false;
        _lastStartCountdownSecond = StartCountdownSeconds + 1;
        _durationSeconds = 0;
        _intervalSeconds = intervalSeconds;
        _stateEndsAt = Time.time + intervalSeconds;
        if (intervalSeconds <= IntervalPlanningSeconds)
        {
            PrepareIntervalPlan();
        }
        else
        {
            Publish(_state, _effect, 0, intervalSeconds, intervalSeconds);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Waiting for {intervalSeconds}s before the next stage event. " +
            $"The event roll begins in the final {IntervalPlanningSeconds}s.");
    }

    private void UpdateWaitingPhase()
    {
        int remaining = Mathf.Max(0, Mathf.CeilToInt(_stateEndsAt - Time.time));
        if (!_intervalPlanPrepared && remaining <= IntervalPlanningSeconds)
        {
            PrepareIntervalPlan();
        }
        if (!_intervalPlanPrepared || _plannedEffect == StageEffect.None)
        {
            return;
        }

        int nameAnnouncementSecond = _config.StartCountdownEnabled.Value
            ? StartCountdownSeconds + EffectNameDisplaySeconds
            : EffectNameDisplaySeconds;
        if (!_intervalNameAnnounced && remaining <= nameAnnouncementSecond)
        {
            _intervalNameAnnounced = true;
            _notifier.EventStarted(_plannedEffect);
        }

        if (!_config.StartCountdownEnabled.Value || remaining > StartCountdownSeconds)
        {
            return;
        }

        int currentSecond = Mathf.Clamp(remaining, 0, StartCountdownSeconds);
        for (int seconds = _lastStartCountdownSecond - 1;
             seconds >= currentSecond;
             seconds--)
        {
            _notifier.Countdown(seconds);
        }
        _lastStartCountdownSecond = currentSecond;
    }

    private void PrepareIntervalPlan()
    {
        if (_intervalPlanPrepared)
        {
            return;
        }

        _intervalPlanPrepared = true;
        if (_mode == EventMode.PersistentForStage)
        {
            _plannedEffect = FilterAvailableEffects(_fixedEffect);
            _plannedDurationSeconds = PersistentEffectDurationSeconds;
            _plannedNextIntervalSeconds = 0;
        }
        else
        {
            _plannedEffect = UsesFixedTiming()
                ? FilterAvailableEffects(_fixedEffect)
                : FilterAvailableEffects(_config.RollEffects(_stageSlotLimit));
            _plannedDurationSeconds = UsesFixedTiming()
                ? _fixedDurationSeconds
                : _config.RollDurationSeconds();
            _plannedNextIntervalSeconds = NextIntervalSeconds();
        }

        int remaining = Mathf.Max(0, Mathf.CeilToInt(_stateEndsAt - Time.time));
        Publish(
            _state,
            _effect,
            0,
            _intervalSeconds,
            remaining,
            _intervalSeconds);
        StagePhysicsEventsPlugin.ModLogger.LogInfo(
            $"Interval plan selected with {remaining}s remaining: " +
            $"effect={_plannedEffect}, duration={_plannedDurationSeconds}s, " +
            $"next interval={_plannedNextIntervalSeconds}s.");
    }

    private void BeginEvent(StageEffect effect, int durationSeconds, int nextIntervalSeconds)
    {
        _state = EventRunState.Active;
        _effect = effect;
        _plannedEffect = effect;
        _durationSeconds = durationSeconds;
        _intervalSeconds = nextIntervalSeconds;
        _activeTargetFilter = _config.CaptureTargetFilter();
        _stateEndsAt = Time.time + durationSeconds;
        Publish(_state, effect, durationSeconds, nextIntervalSeconds, durationSeconds);
        ScheduleSelectedEffectsApplication();
        StagePhysicsEventsPlugin.ModLogger.LogInfo(
            $"Stage effect started: {effect}, duration={durationSeconds}s, next interval={nextIntervalSeconds}s.");
    }

    private void BeginEndCountdown()
    {
        int generation = ++_countdownGeneration;
        _endCountdownCoroutine = StartCoroutine(RunEndCountdown(_stageGeneration, generation));
        StagePhysicsEventsPlugin.ModLogger.LogInfo(
            $"Stage effect end countdown: {_effect}, {EndCountdownSeconds}s.");
    }

    private IEnumerator RunEndCountdown(int stageGeneration, int countdownGeneration)
    {
        for (int seconds = EndCountdownSeconds; seconds >= 1; seconds--)
        {
            if (!EndCountdownIsValid(stageGeneration, countdownGeneration))
            {
                _endCountdownCoroutine = null;
                yield break;
            }
            _notifier.Countdown(seconds);
            yield return new WaitForSeconds(1f);
        }

        if (!EndCountdownIsValid(stageGeneration, countdownGeneration))
        {
            _endCountdownCoroutine = null;
            yield break;
        }
        _endCountdownCoroutine = null;
        EndEventAndBeginWaiting();
    }

    private bool EndCountdownIsValid(int stageGeneration, int countdownGeneration) =>
        _stageReady &&
        _stageGeneration == stageGeneration &&
        _countdownGeneration == countdownGeneration &&
        _state == EventRunState.Active &&
        _mode != EventMode.PersistentForStage &&
        IsAuthority() &&
        _config.Enabled.Value;

    private void BeginPersistentEvent()
    {
        if (_fixedEffect == StageEffect.None || !CanApply(_fixedEffect))
        {
            _stageEffectsEnabled = false;
            _state = EventRunState.Inactive;
            Publish(EventRunState.Inactive, StageEffect.None, 0, 0, 0);
            return;
        }

        BeginWaiting(IntervalPlanningSeconds);
    }

    private void RefreshPersistentEvent()
    {
        _durationSeconds = PersistentEffectDurationSeconds;
        _stateEndsAt = Time.time + PersistentEffectDurationSeconds;
        Publish(_state, _effect, _durationSeconds, 0, _durationSeconds);
        ScheduleSelectedEffectsApplication();
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Persistent stage effect refreshed: {_effect}, next refresh in {PersistentEffectDurationSeconds - PersistentRefreshLeadSeconds}s.");
    }

    private void EndEventAndBeginWaiting()
    {
        StageEffect ended = _effect;
        int nextInterval = _intervalSeconds;
        StopLocalEffects();
        StagePhysicsEventsPlugin.ModLogger.LogInfo($"Stage effect ended: {ended}.");
        _notifier.EventEnded();
        BeginWaiting(nextInterval);
    }

    private string BuildStageStartMessage()
    {
        return _stageEffectsEnabled ? "Effects" : string.Empty;
    }

    private void ScheduleSelectedEffectsApplication()
    {
        int generation = ++_effectApplicationGeneration;
        if (_effectApplicationCoroutine != null)
        {
            StopCoroutine(_effectApplicationCoroutine);
        }
        _effectApplicationCoroutine = StartCoroutine(ApplySelectedEffectsStaged(generation));
    }

    private IEnumerator ApplySelectedEffectsStaged(int generation)
    {
        // StartCoroutine runs immediately until its first yield. Deferring the snapshot
        // keeps UI/chat state publication and the expensive target discovery off the
        // same frame.
        yield return null;
        if (!EffectApplicationIsValid(generation))
        {
            _effectApplicationCoroutine = null;
            yield break;
        }

        List<PhysGrabObject> objects = PhysGrabObjectRegistry.Snapshot(_activeTargetFilter);
        List<PhysGrabObject> batteryObjects = StageEffectSet.Contains(_effect, StageEffect.Battery)
            ? PhysGrabObjectRegistry.Snapshot(TargetFilter.All, discoverEnemies: false)
            : objects;
        List<PlayerAvatar> players = CollectPlayers();
        int enemyCount = 0;
        foreach (PhysGrabObject physObject in objects)
        {
            if (physObject != null &&
                (physObject.GetComponent<EnemyRigidbody>() != null ||
                 physObject.GetComponentInParent<EnemyRigidbody>() != null ||
                 physObject.GetComponentInParent<EnemyParent>() != null))
            {
                enemyCount++;
            }
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Stage effect target snapshot: objects={objects.Count}, batteryObjects={batteryObjects.Count}, " +
            $"enemies={enemyCount}, broadPlayers={(_activeTargetFilter.Players ? players.Count : 0)}, " +
            $"fixedPlayers={players.Count}.");

        _activeTargetIds.Clear();
        foreach (PhysGrabObject target in objects)
        {
            if (target != null)
            {
                _activeTargetIds.Add(target.GetInstanceID());
            }
        }

        // Arm valuable protection before Roll or Void can move or damage anything.
        ScheduleValuableProtection(objects);
        yield return null;
        if (!EffectApplicationIsValid(generation))
        {
            _effectApplicationCoroutine = null;
            yield break;
        }

        if (StageEffectSet.Contains(_effect, StageEffect.Feather))
        {
            _feather.ApplyOnce(objects, _activeTargetFilter.Players ? players : Array.Empty<PlayerAvatar>());
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.ZeroGravity))
        {
            _zeroGravity.ApplyOnce(objects, _activeTargetFilter.Players ? players : Array.Empty<PlayerAvatar>(), _durationSeconds);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Battery))
        {
            _batteryOrb.ApplyOnce(
                StageEffect.Battery,
                batteryObjects,
                Array.Empty<PlayerAvatar>());
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Heal))
        {
            _healOrb.ApplyOnce(
                StageEffect.Heal,
                Array.Empty<PhysGrabObject>(),
                players);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Indestructible))
        {
            _indestructibleOrb.ApplyOnce(
                StageEffect.Indestructible,
                objects,
                _activeTargetFilter.Players ? players : Array.Empty<PlayerAvatar>());
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Roll))
        {
            _rollStaff.ApplyOnce(
                objects,
                _activeTargetFilter.Players ? players : Array.Empty<PlayerAvatar>(),
                _durationSeconds);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Void))
        {
            _void.ApplyOnce(objects, players, _config.RollVoidSpawnCount());
            _nextVoidSpawnAt = Time.time + _config.VoidSpawnIntervalSeconds.Value;
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Levitation))
        {
            _levitation.Begin(
                _config.RollLevitationSpawnCount(),
                _config.LevitationSpawnIntervalSeconds.Value,
                _config.LevitationMinimumPlayerDistance.Value,
                _config.LevitationMaximumActiveInstances.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Shockwave))
        {
            _shockwave.Begin(
                _config.RollShockwaveSpawnCount(),
                _config.ShockwaveSpawnIntervalSeconds.Value,
                _config.ShockwaveMinimumPlayerDistance.Value,
                _config.ShockwaveMaximumActiveInstances.Value,
                _config.ShockwaveLaunchForceMin.Value,
                _config.ShockwaveLaunchForceMax.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.StunBlast))
        {
            _stunBlast.Begin(
                _config.RollStunBlastSpawnCount(),
                _config.StunBlastSpawnIntervalSeconds.Value,
                _config.StunBlastMinimumPlayerDistance.Value,
                _config.StunBlastMaximumActiveInstances.Value,
                _config.StunBlastLaunchForceMin.Value,
                _config.StunBlastLaunchForceMax.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.ExplosionRain))
        {
            _explosionRain.Begin(
                _config.RollExplosionRainSpawnCount(),
                _config.ExplosionRainSpawnIntervalSeconds.Value,
                _config.ExplosionRainMinimumPlayerDistance.Value,
                _config.ExplosionRainMaximumActiveInstances.Value,
                _config.ExplosionRainLaunchForceMin.Value,
                _config.ExplosionRainLaunchForceMax.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.EnemyWave))
        {
            _enemyWave.Begin(_config.RollEnemyWaveSpawnCount());
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Minefield))
        {
            _minefield.Begin(_config.RollMinefieldSpawnCount());
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.GumballHypnosis))
        {
            _gumballHypnosis.Begin();
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.HealingAura))
        {
            _healingAura.Begin(
                _config.RollHealingAuraSpawnCount(),
                _config.HealingAuraHealthPool.Value,
                _config.HealingAuraSpawnIntervalSeconds.Value,
                _config.HealingAuraMinimumPlayerDistance.Value,
                _config.HealingAuraMaximumActiveInstances.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.StarBarrage))
        {
            _starBarrage.Begin(
                _config.RollStarBarrageProjectileCount(),
                _config.StarBarrageSpawnIntervalSeconds.Value,
                _config.StarBarrageMinimumPlayerDistance.Value,
                _config.StarBarrageMaximumActiveInstances.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.SpiderScare))
        {
            _spiderScare.Begin(
                _config.RollSpiderScarePlayerCount(),
                _config.SpiderScareSpawnIntervalSeconds.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.TrafficShock))
        {
            _trafficShock.Begin(
                _config.RollTrafficShockPlayerCount(),
                _config.TrafficShockPulseIntervalSeconds.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Flicker))
        {
            _flicker.Begin(
                _config.FlickerIntervalSeconds.Value,
                _config.FlickerIntensityPercent.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.EnemyHunt))
        {
            _enemyHunt.Begin(_config.EnemyHuntRetargetIntervalSeconds.Value);
            yield return null;
        }
        if (StageEffectSet.Contains(_effect, StageEffect.DangerousValuables))
        {
            _dangerousValuables.Begin(_config.RollDangerousValuableActivationCount());
            yield return null;
        }
        StageEffect enemyPlayerEffects = _effect & (
            StageEffect.Freeze | StageEffect.Stun | StageEffect.EnemyWarp |
            StageEffect.EnemyRegen | StageEffect.EnemyPurge | StageEffect.DamagePulse |
            StageEffect.SecondChance | StageEffect.Knockback |
            StageEffect.EnemySpeedUp | StageEffect.EnemySpeedDown);
        if (enemyPlayerEffects != StageEffect.None)
        {
            _enemyPlayerEvents.Begin(enemyPlayerEffects);
            yield return null;
        }
        StageEffect worldEffects = _effect & (
            StageEffect.Quake | StageEffect.DoorChaos | StageEffect.ValueSurge |
            StageEffect.ValueCrash | StageEffect.Fragility);
        if (worldEffects != StageEffect.None)
        {
            _worldEvents.Begin(worldEffects, _activeTargetFilter);
            yield return null;
        }
        if ((_effect & ExtendedEventCatalog.All) != StageEffect.None)
        {
            _extendedEvents.Begin(_effect & ExtendedEventCatalog.All, _activeTargetFilter);
            yield return null;
        }
        CapturePlayerAliveStates();
        _nextDynamicTargetScanAt = Time.time + DynamicTargetScanIntervalSeconds;
        _effectApplicationCoroutine = null;
    }

    private bool EffectApplicationIsValid(int generation) =>
        generation == _effectApplicationGeneration &&
        _stageReady &&
        _state == EventRunState.Active &&
        IsAuthority() &&
        _config.Enabled.Value;

    private void RefreshDynamicTargets()
    {
        if (Time.time < _nextDynamicTargetScanAt)
        {
            return;
        }
        _nextDynamicTargetScanAt = Time.time + DynamicTargetScanIntervalSeconds;
        RefreshRevivedPlayers();

        List<PhysGrabObject> currentTargets = PhysGrabObjectRegistry.Snapshot(
            _activeTargetFilter,
            discoverEnemies: false);
        foreach (PhysGrabObject target in currentTargets)
        {
            if (target == null || !_activeTargetIds.Add(target.GetInstanceID()))
            {
                continue;
            }
            if (target.GetComponentInParent<PlayerAvatar>() != null)
            {
                continue;
            }
            ApplyCurrentEffectsToNewTarget(target);
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Battery))
        {
            foreach (PhysGrabObject target in PhysGrabObjectRegistry.Snapshot(
                         TargetFilter.All,
                         discoverEnemies: false))
            {
                _batteryOrb.AddTarget(target);
            }
        }
        RefreshDynamicValuableProtection();
    }

    private void RefreshVoidEffects()
    {
        if (!StageEffectSet.Contains(_effect, StageEffect.Void) || Time.time < _nextVoidSpawnAt)
        {
            return;
        }

        List<PhysGrabObject> objects = PhysGrabObjectRegistry.Snapshot(_activeTargetFilter);
        List<PlayerAvatar> players = CollectPlayers();
        _void.ApplyOnce(objects, players, _config.RollVoidSpawnCount());
        _nextVoidSpawnAt = Time.time + _config.VoidSpawnIntervalSeconds.Value;
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Void effects regenerated; next random spawn in {_config.VoidSpawnIntervalSeconds.Value}s.");
    }

    private void RefreshDynamicValuableProtection()
    {
        if (!_activeTargetFilter.Valuables || !HasActiveValuableProtection())
        {
            _valuableProtection.Stop();
            return;
        }
        TargetFilter valuablesOnly = new(
            valuables: true,
            cosmeticBoxes: false,
            items: false,
            doors: false,
            weapons: false,
            players: false,
            enemies: false);
        foreach (PhysGrabObject valuable in PhysGrabObjectRegistry.Snapshot(
                     valuablesOnly,
                     discoverEnemies: false))
        {
            _valuableProtection.AddTarget(valuable);
        }
    }

    private void ApplyCurrentEffectsToNewTarget(PhysGrabObject target)
    {
        float remainingSeconds = Mathf.Max(0.1f, _stateEndsAt - Time.time);
        if (StageEffectSet.Contains(_effect, StageEffect.Feather))
        {
            _feather.AddTarget(target);
        }
        if (StageEffectSet.Contains(_effect, StageEffect.ZeroGravity))
        {
            _zeroGravity.ApplyOnce(new[] { target }, Array.Empty<PlayerAvatar>(), remainingSeconds);
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Battery))
        {
            _batteryOrb.AddTarget(target);
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Indestructible))
        {
            _indestructibleOrb.AddTarget(target);
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Roll))
        {
            _rollStaff.ApplyOnce(new[] { target }, Array.Empty<PlayerAvatar>(), remainingSeconds);
        }
        if (_activeTargetFilter.Valuables && target.GetComponent<ValuableObject>() != null &&
            HasActiveValuableProtection())
        {
            _valuableProtection.AddTarget(target);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Current stage effects added to new target: {target.name}, effects={_effect}, remaining={remainingSeconds:0.##}s.");
    }

    private void CapturePlayerAliveStates()
    {
        _playerAliveStates.Clear();
        if (GameDirector.instance == null)
        {
            return;
        }
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
        {
            if (player != null)
            {
                _playerAliveStates[PlayerKey(player)] = IsPlayerAlive(player);
            }
        }
    }

    private void RefreshRevivedPlayers()
    {
        bool hasBroadPlayerEffect = _activeTargetFilter.Players && (
            StageEffectSet.Contains(_effect, StageEffect.Feather) ||
            StageEffectSet.Contains(_effect, StageEffect.ZeroGravity) ||
            StageEffectSet.Contains(_effect, StageEffect.Indestructible) ||
            StageEffectSet.Contains(_effect, StageEffect.Roll));
        bool hasFixedPlayerEffect = StageEffectSet.Contains(_effect, StageEffect.Heal);
        if ((!hasBroadPlayerEffect && !hasFixedPlayerEffect) || GameDirector.instance == null)
        {
            return;
        }
        bool hasPlayerEffect = hasBroadPlayerEffect || hasFixedPlayerEffect;
        HashSet<int> currentPlayerKeys = new();
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
        {
            if (player == null)
            {
                continue;
            }
            int key = PlayerKey(player);
            currentPlayerKeys.Add(key);
            bool alive = IsPlayerAlive(player);
            bool wasAlive = _playerAliveStates.TryGetValue(key, out bool previous) && previous;
            if (!alive)
            {
                _playerAliveStates[key] = false;
                continue;
            }
            if (!wasAlive && TryApplyCurrentEffectsToRevivedPlayer(player))
            {
                _playerAliveStates[key] = true;
                if (hasPlayerEffect)
                {
                    StagePhysicsEventsPlugin.ModLogger.LogInfo(
                        $"Current stage effects restored after player revival: view={player.photonView?.ViewID ?? 0}, effects={_effect}.");
                }
            }
        }
        List<int> trackedKeys = new(_playerAliveStates.Keys);
        foreach (int trackedKey in trackedKeys)
        {
            if (!currentPlayerKeys.Contains(trackedKey))
            {
                _playerAliveStates[trackedKey] = false;
            }
        }
    }

    private bool TryApplyCurrentEffectsToRevivedPlayer(PlayerAvatar player)
    {
        bool broadPlayersEnabled = _activeTargetFilter.Players;
        bool needsPhysicalTarget = broadPlayersEnabled &&
            StageEffectSet.Contains(_effect, StageEffect.Indestructible);
        PhysGrabObject? playerObject = player.GetComponentInChildren<PhysGrabObject>(true);
        if (needsPhysicalTarget && (playerObject == null || playerObject.rb == null))
        {
            return false;
        }

        float remainingSeconds = Mathf.Max(0.1f, _stateEndsAt - Time.time);
        if (broadPlayersEnabled && StageEffectSet.Contains(_effect, StageEffect.Feather))
        {
            _feather.RefreshPlayer(player);
        }
        if (broadPlayersEnabled && StageEffectSet.Contains(_effect, StageEffect.ZeroGravity))
        {
            _zeroGravity.ApplyOnce(Array.Empty<PhysGrabObject>(), new[] { player }, remainingSeconds);
        }
        if (StageEffectSet.Contains(_effect, StageEffect.Heal))
        {
            _healOrb.AddPlayer(player);
        }
        if (broadPlayersEnabled &&
            StageEffectSet.Contains(_effect, StageEffect.Indestructible) &&
            playerObject != null)
        {
            _indestructibleOrb.AddTarget(playerObject);
        }
        if (broadPlayersEnabled && StageEffectSet.Contains(_effect, StageEffect.Roll))
        {
            _rollStaff.ApplyOnce(Array.Empty<PhysGrabObject>(), new[] { player }, remainingSeconds);
        }
        return true;
    }

    private void ScheduleValuableProtection(IReadOnlyList<PhysGrabObject> eventTargets)
    {
        if (!_activeTargetFilter.Valuables)
        {
            _valuableProtection.Stop();
            return;
        }

        bool hasRoll = HasValuableProtection(StageEffect.Roll, _config.RollProtectValuables.Value);
        bool hasVoid = HasValuableProtection(StageEffect.Void, _config.VoidProtectValuables.Value);
        bool hasSpawnedHazard = HasProtectedSpawnedHazardEffect();
        bool protectForWholeEvent = hasRoll || hasVoid || hasSpawnedHazard;
        bool protectAroundRelease = HasValuableProtection(
            StageEffect.ZeroGravity,
            _config.ZeroGravityProtectValuables.Value);
        if (!protectForWholeEvent && !protectAroundRelease)
        {
            _valuableProtection.Stop();
            return;
        }

        float startsAt = protectForWholeEvent ? Time.time : _stateEndsAt - 0.5f;
        float protectionAfterReleaseSeconds = _config.ValuableProtectionReleaseDelaySeconds.Value;
        float endsAt = _stateEndsAt + protectionAfterReleaseSeconds;
        _valuableProtection.Schedule(eventTargets, startsAt, endsAt);
    }

    private bool HasActiveValuableProtection() =>
        HasValuableProtection(StageEffect.ZeroGravity, _config.ZeroGravityProtectValuables.Value) ||
        HasValuableProtection(StageEffect.Roll, _config.RollProtectValuables.Value) ||
        HasValuableProtection(StageEffect.Void, _config.VoidProtectValuables.Value) ||
        HasProtectedSpawnedHazardEffect();

    private bool HasProtectedSpawnedHazardEffect() =>
        HasValuableProtection(StageEffect.Levitation, _config.LevitationProtectValuables.Value) ||
        HasValuableProtection(StageEffect.Shockwave, _config.ShockwaveProtectValuables.Value) ||
        HasValuableProtection(StageEffect.StunBlast, _config.StunBlastProtectValuables.Value) ||
        HasValuableProtection(StageEffect.ExplosionRain, _config.ExplosionRainProtectValuables.Value) ||
        HasValuableProtection(StageEffect.Minefield, _config.MinefieldProtectValuables.Value) ||
        HasValuableProtection(StageEffect.StarBarrage, _config.StarBarrageProtectValuables.Value) ||
        HasValuableProtection(
            StageEffect.DangerousValuables,
            _config.DangerousValuablesProtectValuables.Value) ||
        HasValuableProtection(StageEffect.Quake, _config.QuakeProtectValuables.Value);

    private bool HasValuableProtection(StageEffect effect, bool enabled) =>
        enabled && StageEffectSet.Contains(_effect, effect);

    private static List<PlayerAvatar> CollectPlayers()
    {
        List<PlayerAvatar> result = new();
        if (GameDirector.instance == null)
        {
            return result;
        }
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
        {
            if (IsPlayerAlive(player))
            {
                result.Add(player);
            }
        }
        return result;
    }

    private static int PlayerKey(PlayerAvatar player) => player.GetInstanceID();

    private static bool IsPlayerAlive(PlayerAvatar? player)
    {
        if (player == null || !player.gameObject.activeInHierarchy || player.playerHealth == null)
        {
            return false;
        }

        // These fields are internal/private in the runtime game assembly even though the
        // publicized compile-time reference exposes them. Read them through Harmony so a
        // FieldAccessException cannot abort the entire effect application pipeline.
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

    private void StopLocalEffects()
    {
        _countdownGeneration++;
        _effectApplicationGeneration++;
        if (_effectApplicationCoroutine != null)
        {
            StopCoroutine(_effectApplicationCoroutine);
            _effectApplicationCoroutine = null;
        }
        if (_endCountdownCoroutine != null)
        {
            StopCoroutine(_endCountdownCoroutine);
            _endCountdownCoroutine = null;
        }
        _feather?.Stop();
        _zeroGravity?.Stop();
        _batteryOrb?.Stop();
        _healOrb?.Stop();
        _indestructibleOrb?.Stop();
        _rollStaff?.Stop();
        _void?.Stop();
        _levitation?.Stop();
        _shockwave?.Stop();
        _stunBlast?.Stop();
        _explosionRain?.Stop();
        _enemyWave?.Stop();
        _minefield?.Stop();
        _gumballHypnosis?.Stop();
        _healingAura?.Stop();
        _starBarrage?.Stop();
        _spiderScare?.Stop();
        _trafficShock?.Stop();
        _flicker?.Stop();
        _enemyHunt?.Stop();
        _dangerousValuables?.Stop();
        _enemyPlayerEvents?.Stop();
        _worldEvents?.Stop();
        _extendedEvents?.Stop();
        _activeTargetIds.Clear();
        _playerAliveStates.Clear();
        _nextDynamicTargetScanAt = 0f;
        _nextVoidSpawnAt = 0f;
    }

    private static bool IsAuthority() => SemiFunc.IsMasterClientOrSingleplayer();

    private static bool IsPlayableStageContext()
    {
        try
        {
            return SemiFunc.RunIsLevel() &&
                   !SemiFunc.RunIsLobby() &&
                   !SemiFunc.RunIsShop() &&
                   !SemiFunc.RunIsArena() &&
                   !SemiFunc.RunIsTutorial();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHudStageActive()
    {
        return IsPlayableStageContext() &&
               LevelGenerator.Instance != null &&
               LevelGenerator.Instance.Generated &&
               GameDirector.instance != null &&
               GameDirector.instance.currentState == GameDirector.gameState.Main;
    }

    private void Publish(
        EventRunState state,
        StageEffect effect,
        int duration,
        int interval,
        int secondsUntilEnd,
        int phaseDurationOverride = -1)
    {
        if (!SemiFunc.IsMultiplayer() || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
        {
            return;
        }
        int endTimestamp = unchecked(PhotonNetwork.ServerTimestamp + secondsUntilEnd * 1000);
        Hashtable properties = new()
        {
            [VersionKey] = StateFormatVersion,
            [StageIdKey] = _stageToken,
            [StateKey] = (int)state,
            [EffectKey] = (long)effect,
            [DurationKey] = duration,
            [IntervalKey] = interval,
            [EndTimestampKey] = endTimestamp,
            [PreviewEffectKey] = (long)_plannedEffect,
            [SlotLimitKey] = GetHudSlotLimit(
                state,
                effect,
                _plannedEffect,
                _stageSlotLimit),
            [PhaseDurationKey] = phaseDurationOverride >= 0
                ? phaseDurationOverride
                : GetPhaseDuration(state, duration, interval, secondsUntilEnd),
            [ModeKey] = (int)_mode
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

    private static void SetLocalUiCapability()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [UiCapabilityKey] = 1 });
        }
    }

    private void ConsumeRoomState()
    {
        if (PhotonNetwork.CurrentRoom != null)
        {
            ReadRoomState(PhotonNetwork.CurrentRoom.CustomProperties);
        }
    }

    private void ReadRoomState(Hashtable properties)
    {
        if (!TryGetInt(properties, VersionKey, out int version) || version != StateFormatVersion ||
            !TryGetInt(properties, StageIdKey, out _remoteStageToken) ||
            !TryGetInt(properties, StateKey, out int state) ||
            !TryGetLong(properties, EffectKey, out long effect) ||
            !TryGetInt(properties, DurationKey, out _remoteDuration) ||
            !TryGetInt(properties, IntervalKey, out _remoteInterval) ||
            !TryGetInt(properties, EndTimestampKey, out _remoteEndTimestamp) ||
            !TryGetLong(properties, PreviewEffectKey, out long previewEffect) ||
            !TryGetInt(properties, SlotLimitKey, out _remoteSlotLimit) ||
            !TryGetInt(properties, PhaseDurationKey, out _remotePhaseDuration) ||
            !TryGetInt(properties, ModeKey, out int mode) ||
            !Enum.IsDefined(typeof(EventRunState), state) ||
            !Enum.IsDefined(typeof(EventMode), mode) ||
            !StageEffectSet.IsValid((StageEffect)effect) ||
            !StageEffectSet.IsValid((StageEffect)previewEffect))
        {
            _remoteState = EventRunState.Inactive;
            return;
        }
        _remoteState = (EventRunState)state;
        _remoteEffect = (StageEffect)effect;
        _remotePreviewEffect = (StageEffect)previewEffect;
        _remoteMode = (EventMode)mode;
        _remoteSlotLimit = Mathf.Clamp(_remoteSlotLimit, 1, 5);
    }

    private static int GetPhaseDuration(
        EventRunState state,
        int duration,
        int interval,
        int explicitDuration = -1)
    {
        if (explicitDuration >= 0)
        {
            return explicitDuration;
        }
        return state switch
        {
            EventRunState.Waiting => interval,
            EventRunState.Active => duration,
            EventRunState.Countdown => EffectNameDisplaySeconds + StartCountdownSeconds,
            _ => 0
        };
    }

    private static int GetHudSlotLimit(
        EventRunState state,
        StageEffect activeEffect,
        StageEffect plannedEffect,
        int configuredLimit)
    {
        StageEffect visibleEffect = state == EventRunState.Waiting
            ? plannedEffect
            : activeEffect;
        int visibleEffectCount = 0;
        foreach (StageEffect effect in StageEffectSet.IndividualEffects)
        {
            if (StageEffectSet.Contains(visibleEffect, effect))
            {
                visibleEffectCount++;
            }
        }
        return Mathf.Clamp(
            Mathf.Max(configuredLimit, visibleEffectCount),
            1,
            5);
    }

    private static bool TryGetInt(Hashtable properties, string key, out int value)
    {
        value = 0;
        return properties.TryGetValue(key, out object raw) && raw is int parsed && (value = parsed) == parsed;
    }

    private static bool TryGetLong(Hashtable properties, string key, out long value)
    {
        value = 0;
        if (!properties.TryGetValue(key, out object raw))
        {
            return false;
        }
        if (raw is long parsedLong)
        {
            value = parsedLong;
            return true;
        }
        if (raw is int parsedInt)
        {
            value = parsedInt;
            return true;
        }
        return false;
    }

    public override void OnJoinedRoom()
    {
        SetLocalUiCapability();
        ConsumeRoomState();
    }

    public override void OnLeftRoom()
    {
        _remoteState = EventRunState.Inactive;
        StageEnding();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        ConsumeRoomState();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (_stageReady && IsAuthority())
        {
            _notifier.PlayerEntered(newPlayer);
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (_stageReady && PhotonNetwork.LocalPlayer == newMasterClient && _config.Enabled.Value)
        {
            StopLocalEffects();
            _valuableProtection.Stop();
            _stageToken = unchecked(_stageToken + 1);
            PrepareStagePlan();
            _notifier.BeginStage(_stageGeneration, BuildStageStartMessage());
        }
    }
}
