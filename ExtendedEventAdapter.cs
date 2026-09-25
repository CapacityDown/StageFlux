using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.StagePhysicsEvents;

internal sealed class ExtendedEventAdapter
{
    private static readonly FieldInfo? ValuableOriginal = AccessTools.Field(typeof(ValuableObject), "dollarValueOriginal");
    private static readonly FieldInfo? ValuableCurrent = AccessTools.Field(typeof(ValuableObject), "dollarValueCurrent");
    private static readonly FieldInfo? ValuableReady = AccessTools.Field(typeof(ValuableObject), "dollarValueSet");
    private static readonly MethodInfo? HealLogic = AccessTools.Method(typeof(PhysGrabObjectImpactDetector), "HealLogic", new[] { typeof(float), typeof(Vector3) });
    private static readonly FieldInfo? GrabbedObject = AccessTools.Field(typeof(PhysGrabber), "grabbedPhysGrabObject");
    private static readonly FieldInfo? PlayerGrounded = AccessTools.Field(typeof(PlayerAvatar), "isGrounded");
    private static readonly FieldInfo? PlayerTumbling = AccessTools.Field(typeof(PlayerAvatar), "isTumbling");
    private static readonly FieldInfo? PlayerCrouching = AccessTools.Field(typeof(PlayerAvatar), "isCrouching");
    private static readonly FieldInfo? PlayerCrawling = AccessTools.Field(typeof(PlayerAvatar), "isCrawling");
    internal static readonly FieldInfo? HealthValue = AccessTools.Field(typeof(PlayerHealth), "health");
    internal static readonly FieldInfo? HealthMaximum = AccessTools.Field(typeof(PlayerHealth), "maxHealth");
    internal static readonly FieldInfo? HealthReady = AccessTools.Field(typeof(PlayerHealth), "healthSet");
    private static readonly MethodInfo? BatterySyncLogic = AccessTools.Method(typeof(ItemBattery), "BatteryFullPercentChangeLogic", new[] { typeof(int), typeof(bool) });
    private static readonly FieldInfo? BatteryPreviousBars = AccessTools.Field(typeof(ItemBattery), "batteryLifeCountBarsPrev");
    private static readonly FieldInfo? BatteryPreviousLife = AccessTools.Field(typeof(ItemBattery), "batteryLifePrev");
    private static readonly FieldInfo? ItemInstanceName = AccessTools.Field(typeof(ItemAttributes), "instanceName");

    private readonly MonoBehaviour _owner;
    private readonly StagePhysicsConfig _config;
    private readonly RandomStagePointSelector _points;
    private readonly Dictionary<StageEffect, float> _nextPulse = new();
    private readonly HashSet<string> _warnings = new();
    private readonly List<PhysGrabObject> _cargo = new();
    private readonly Queue<(PlayerHealth Source, int Damage)> _sharedHits = new();
    private StageEffect _effects;
    private TargetFilter _filter;
    private float _nextCargoScan;
    private int _generation;
    private int _suppliesThisStage;
    private Coroutine? _supplyWave;
    internal static ExtendedEventAdapter? Active { get; private set; }

    internal ExtendedEventAdapter(MonoBehaviour owner, StagePhysicsConfig config,
        RandomStagePointSelector points)
    {
        _owner = owner;
        _config = config;
        _points = points;
    }

    internal bool Has(StageEffect effect) => SemiFunc.IsMasterClientOrSingleplayer() &&
        _config.Enabled.Value && StageEffectSet.Contains(_effects, effect);

    internal bool EnsureAvailable(StageEffect effect) => effect switch
    {
        StageEffect.Restoration => HealLogic != null && ValuableOriginal != null && ValuableCurrent != null && ValuableReady != null,
        StageEffect.BatteryDrain => BatterySyncLogic != null && BatteryPreviousBars != null && BatteryPreviousLife != null,
        StageEffect.PlayerSwap => LivingPlayers().Count >= 2 && PlayerGrounded != null,
        StageEffect.SharedPain => LivingPlayers().Count >= 2 && HealthValue != null && HealthReady != null,
        StageEffect.SupplyDrop => ResolveSupplies().Count > 0 && _suppliesThisStage < _config.SupplyDropMaximumPerStage.Value,
        StageEffect.HeavyCargo => _config.TargetValuables.Value || _config.TargetCosmeticBoxes.Value || _config.TargetItems.Value || _config.TargetWeapons.Value,
        _ => (effect & ExtendedEventCatalog.All) != StageEffect.None
    };

    internal void Begin(StageEffect effects, TargetFilter filter)
    {
        Stop();
        _effects = effects;
        _filter = filter;
        Active = this;
        Tick();
    }

    internal void Tick()
    {
        if (_effects == StageEffect.None || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        Pulse(StageEffect.Restoration, _config.RestorationIntervalSeconds.Value, RestoreValuables);
        Pulse(StageEffect.BatteryDrain, _config.BatteryDrainIntervalSeconds.Value, DrainBatteries);
        Pulse(StageEffect.HeavyCargo, 0.1f, ApplyHeavyCargo);
        Pulse(StageEffect.Butterfingers, _config.ButterfingersIntervalSeconds.Value, DropHeldObjects);
        Pulse(StageEffect.SupplyDrop, _config.SupplyDropIntervalSeconds.Value, StartSupplyWave);
        Pulse(StageEffect.PlayerSwap, _config.PlayerSwapIntervalSeconds.Value, SwapPlayers);
        // Defer propagation until outside health callbacks and limit work per frame.
        for (int count = 0; count < 4 && _sharedHits.Count > 0; count++)
        {
            var hit = _sharedHits.Dequeue();
            Safe("Shared Pain", () => ShareDamage(hit.Source, hit.Damage));
        }
    }

    internal void Stop()
    {
        _generation++;
        if (_supplyWave != null)
            _owner.StopCoroutine(_supplyWave);
        _supplyWave = null;
        _effects = StageEffect.None;
        _nextPulse.Clear();
        _cargo.Clear();
        _nextCargoScan = 0f;
        _sharedHits.Clear();
        if (Active == this)
            Active = null;
        // Timed mass/protection leases expire naturally; do not clear another mod's overrides.
        // Supply items are normal, usable items and intentionally survive event end.
    }

    internal void ResetStage()
    {
        Stop();
        _suppliesThisStage = 0;
        _warnings.Clear();
    }

    private void Pulse(StageEffect effect, float interval, Action action)
    {
        if (!Has(effect) || (_nextPulse.TryGetValue(effect, out float next) && Time.time < next))
            return;
        _nextPulse[effect] = Time.time + interval;
        Safe(effect.ToString(), action);
    }

    private void Safe(string name, Action action)
    {
        try { action(); }
        catch (Exception exception)
        {
            if (_warnings.Add(name))
                StagePhysicsEventsPlugin.ModLogger.LogWarning($"{name} skipped an unavailable target: {exception.GetBaseException().Message}");
        }
    }

    internal static bool IsStageObject(Component? component) => component != null &&
        component.gameObject.scene.IsValid() && component.gameObject.activeInHierarchy &&
        !StagePhysicsEffectCarrierUtility.IsInternalCarrier(component);

    private void RestoreValuables()
    {
        foreach (ValuableObject valuable in UnityEngine.Object.FindObjectsOfType<ValuableObject>())
        {
            if (!IsStageObject(valuable) || ValuableReady?.GetValue(valuable) is not true)
                continue;
            Safe("Restoration target", () =>
            {
                PhysGrabObjectImpactDetector? detector = valuable.GetComponent<PhysGrabObjectImpactDetector>() ??
                    valuable.GetComponentInParent<PhysGrabObjectImpactDetector>();
                if (detector == null || !IsStageObject(detector))
                    return;
                float original = ValuableOriginal?.GetValue(valuable) is float full ? full : 0f;
                float current = ValuableCurrent?.GetValue(valuable) is float remaining ? remaining : 0f;
                // DollarValueSetRPC changes both values when Surge/Crash starts. Repair
                // their difference, never the value multiplier or rounded price itself.
                float repair = ExtendedEventPolicy.RepairAmount(original, current, _config.RestorationRepairPercent.Value);
                if (repair <= 0f)
                    return;
                PhotonView? view = detector.GetComponent<PhotonView>() ?? detector.GetComponentInParent<PhotonView>();
                if (SemiFunc.IsMultiplayer() && (view == null || view.ViewID == 0))
                    return;
                Vector3 point = valuable.transform.position;
                HealLogic!.Invoke(detector, new object[] { repair, point });
                if (SemiFunc.IsMultiplayer())
                    view!.RPC("HealRPC", RpcTarget.Others, repair, point);
            });
        }
    }

    private void DrainBatteries()
    {
        foreach (ItemBattery battery in UnityEngine.Object.FindObjectsOfType<ItemBattery>())
        {
            if (!IsStageObject(battery))
                continue;
            Safe("Battery Drain target", () =>
            {
                float charge = ExtendedEventPolicy.DrainCharge(battery.batteryLife,
                    _config.BatteryDrainAmount.Value, _config.BatteryDrainMinimumChargePercent.Value);
                if (Mathf.Approximately(charge, battery.batteryLife) || battery.batteryBars <= 0)
                    return;
                PhotonView? view = battery.GetComponent<PhotonView>();
                if (SemiFunc.IsMultiplayer() && (view == null || view.ViewID == 0))
                    return;
                int bars = Mathf.Clamp(Mathf.RoundToInt(charge * battery.batteryBars / 100f), 0, battery.batteryBars);
                BatterySyncLogic!.Invoke(battery, new object[] { bars, false });
                if (SemiFunc.IsMultiplayer())
                    view!.RPC("BatteryFullPercentChangeRPC", RpcTarget.Others, bars, false);
                // Vanilla bar synchronization snaps charge to a whole bar. Keep the
                // host's exact percentage and minimum; peers receive vanilla bar visuals.
                battery.batteryLife = charge;
                BatteryPreviousBars!.SetValue(battery, bars);
                BatteryPreviousLife!.SetValue(battery, charge);
                ItemAttributes? attributes = battery.GetComponent<ItemAttributes>();
                if (attributes != null && ItemInstanceName?.GetValue(attributes) is string name && !string.IsNullOrEmpty(name))
                    SemiFunc.StatSetBattery(name, Mathf.RoundToInt(charge));
            });
        }
    }

    private void ApplyHeavyCargo()
    {
        if (Time.time >= _nextCargoScan)
        {
            _nextCargoScan = Time.time + 1f;
            _cargo.Clear();
            _cargo.AddRange(PhysGrabObjectRegistry.Snapshot(new TargetFilter(
                _filter.Valuables, _filter.CosmeticBoxes, _filter.Items, false, _filter.Weapons, false, false), false));
        }
        foreach (PhysGrabObject target in _cargo)
        {
            if (!IsStageObject(target) || target.rb == null)
                continue;
            target.OverrideMass(Mathf.Max(0.01f, target.massOriginal) * _config.HeavyCargoMassPercent.Value / 100f, 0.25f);
            if (_config.HeavyCargoProtectValuables.Value && _config.TargetValuables.Value && target.GetComponent<ValuableObject>() != null)
                target.OverrideIndestructible(_config.ValuableProtectionReleaseDelaySeconds.Value);
        }
    }

    private void DropHeldObjects()
    {
        foreach (PlayerAvatar player in LivingPlayers())
        {
            PhysGrabber grabber = player.physGrabber;
            PhysGrabObject? held = grabber != null ? GrabbedObject?.GetValue(grabber) as PhysGrabObject : null;
            if (grabber == null || !IsStageObject(held) || !TargetFilter.HeldObjects.Allows(held!))
                continue;
            PhotonView? view = held!.GetComponent<PhotonView>() ?? held.GetComponentInParent<PhotonView>();
            if (SemiFunc.IsMultiplayer() && (view == null || view.ViewID == 0))
                continue;
            if (_config.ButterfingersProtectValuables.Value && _config.TargetValuables.Value && held.GetComponent<ValuableObject>() != null)
                held.OverrideIndestructible(_config.ValuableProtectionReleaseDelaySeconds.Value);
            grabber.OverrideGrabRelease(view != null ? view.ViewID : -1, 0.5f);
        }
    }

    private List<ResolvedSpawnPrefab> ResolveSupplies()
    {
        List<ResolvedSpawnPrefab> result = new();
        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (Item item in VanillaSpawnEffectResolver.EnumerateItems())
        {
            bool allowed = (_config.SupplyDropHealthPacks.Value && item.itemType == SemiFunc.itemType.healthPack) ||
                (_config.SupplyDropEquipment.Value && (item.itemType == SemiFunc.itemType.tracker || item.itemType == SemiFunc.itemType.tool));
            if (allowed && VanillaSpawnEffectResolver.TryGetVanillaPrefab(item, out ResolvedSpawnPrefab prefab) &&
                prefab.Prefab.GetComponentInChildren<PhotonView>(true) != null &&
                prefab.Prefab.GetComponentInChildren<ItemAttributes>(true) != null && paths.Add(prefab.ResourcePath))
                result.Add(prefab);
        }
        return result;
    }

    private void StartSupplyWave()
    {
        if (_supplyWave != null || _suppliesThisStage >= _config.SupplyDropMaximumPerStage.Value)
            return;
        List<ResolvedSpawnPrefab> prefabs = ResolveSupplies();
        if (prefabs.Count == 0)
            return;
        int minimum = Math.Min(_config.SupplyDropCountMin.Value, _config.SupplyDropCountMax.Value);
        int maximum = Math.Max(_config.SupplyDropCountMin.Value, _config.SupplyDropCountMax.Value);
        int amount = Math.Min(UnityEngine.Random.Range(minimum, maximum + 1), _config.SupplyDropMaximumPerStage.Value - _suppliesThisStage);
        _supplyWave = _owner.StartCoroutine(SpawnSupplies(prefabs, amount, _generation));
    }

    private IEnumerator SpawnSupplies(List<ResolvedSpawnPrefab> prefabs, int amount, int generation)
    {
        yield return null;
        HashSet<int> used = new();
        for (int index = 0; index < amount && generation == _generation && Has(StageEffect.SupplyDrop); index++)
        {
            if (_suppliesThisStage >= _config.SupplyDropMaximumPerStage.Value ||
                !_points.TryGetNavigablePosition(3f, used, out Vector3 position, out Quaternion rotation))
                break;
            ResolvedSpawnPrefab prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            Safe("Supply Drop spawn", () =>
            {
                GameObject instance = SemiFunc.IsMultiplayer()
                    ? PhotonNetwork.Instantiate(prefab.ResourcePath, position + Vector3.up * 0.25f, rotation, 0)
                    : UnityEngine.Object.Instantiate(prefab.Prefab, position + Vector3.up * 0.25f, rotation);
                if (instance != null)
                    _suppliesThisStage++;
            });
            yield return new WaitForSeconds(0.1f);
        }
        if (generation == _generation)
            _supplyWave = null;
    }

    private void SwapPlayers()
    {
        List<PlayerAvatar> candidates = LivingPlayers();
        candidates.RemoveAll(player => !CanSwap(player));
        if (candidates.Count < 2)
            return;
        int firstIndex = UnityEngine.Random.Range(0, candidates.Count);
        PlayerAvatar first = candidates[firstIndex];
        candidates.RemoveAt(firstIndex);
        PlayerAvatar second = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        Vector3 firstPosition = first.transform.position;
        Vector3 secondPosition = second.transform.position;
        if (Vector3.Distance(firstPosition, secondPosition) < 2f ||
            !SafePlayerPosition(first, secondPosition, first, second) ||
            !SafePlayerPosition(second, firstPosition, first, second))
            return;
        Quaternion firstRotation = first.transform.rotation;
        Quaternion secondRotation = second.transform.rotation;
        first.Spawn(secondPosition, secondRotation);
        second.Spawn(firstPosition, firstRotation);
    }

    private static bool CanSwap(PlayerAvatar player) => PlayerAvatarState.IsLiving(player) &&
        PlayerGrounded?.GetValue(player) is true && PlayerTumbling?.GetValue(player) is false &&
        PlayerCrouching?.GetValue(player) is false && PlayerCrawling?.GetValue(player) is false &&
        player.physGrabber != null && !player.physGrabber.grabbed &&
        !RoleShuffleCompatibility.HasImmediateCorrectiveRevival(player);

    private static bool SafePlayerPosition(PlayerAvatar mover, Vector3 destination, PlayerAvatar first, PlayerAvatar second)
    {
        if (!NavMesh.SamplePosition(destination, out NavMeshHit navHit, 1.25f, NavMesh.AllAreas) ||
            Vector2.Distance(new Vector2(navHit.position.x, navHit.position.z), new Vector2(destination.x, destination.z)) > 0.5f)
            return false;
        CapsuleCollider? capsule = mover.GetComponent<CapsuleCollider>();
        if (capsule == null || !capsule.enabled || capsule.direction != 1)
            return false;
        Vector3 scale = mover.transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(radius * 2f, capsule.height * Mathf.Abs(scale.y));
        Vector3 center = destination + Vector3.Scale(capsule.center, scale);
        float half = Mathf.Max(0f, height * 0.5f - radius);
        foreach (Collider obstacle in Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half,
                     radius * 0.9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            PlayerAvatar? owner = obstacle.GetComponentInParent<PlayerAvatar>();
            if (owner == first || owner == second)
                continue;
            return false;
        }
        return true;
    }

    internal int EnemyDamagePercent => Has(StageEffect.EnemyArmor) ? _config.EnemyArmorDamagePercent.Value :
        Has(StageEffect.EnemyVulnerability) ? _config.EnemyVulnerabilityDamagePercent.Value : 100;
    internal float VisionMultiplier => Has(StageEffect.EnemyBlindness) ? _config.EnemyBlindnessVisionPercent.Value / 100f : 1f;

    internal void RecordHealthLoss(PlayerHealth source, int previous, int previousMaximum, bool ready, bool eligible)
    {
        if (!eligible || !ready || !Has(StageEffect.SharedPain) || source == null ||
            HealthMaximum?.GetValue(source) is not int maximum || maximum != previousMaximum ||
            HealthValue?.GetValue(source) is not int current || previous <= current || previous <= 0)
            return;
        PlayerAvatar? player = source.GetComponent<PlayerAvatar>() ?? source.GetComponentInParent<PlayerAvatar>();
        if (!IsStageObject(source) || RoleShuffleCompatibility.HasImmediateCorrectiveRevival(player))
            return;
        if (_sharedHits.Count < 32)
            _sharedHits.Enqueue((source, previous - Math.Max(0, current)));
    }

    private void ShareDamage(PlayerHealth source, int lost)
    {
        if (!Has(StageEffect.SharedPain))
            return;
        foreach (PlayerAvatar player in LivingPlayers())
        {
            PlayerHealth health = player.playerHealth;
            if (health == null || health == source || HealthValue?.GetValue(health) is not int current)
                continue;
            int damage = ExtendedEventPolicy.SharedDamage(lost, _config.SharedPainDamagePercent.Value,
                _config.SharedPainMaximumDamage.Value, current, _config.SharedPainCanKill.Value);
            if (damage > 0)
            {
                // Vanilla propagates hurtByHeal back in UpdateHealthRPC, so remote
                // acknowledgements cannot recursively produce another Shared Pain hit.
                health.HurtOther(damage, Vector3.zero, !_config.SharedPainCanKill.Value, -1, true);
            }
        }
    }

    private static List<PlayerAvatar> LivingPlayers()
    {
        List<PlayerAvatar> result = new();
        if (GameDirector.instance == null)
            return result;
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
            if (IsStageObject(player) && PlayerAvatarState.IsLiving(player) && player.playerHealth != null &&
                !RoleShuffleCompatibility.IsTricksterDecoy(player))
                result.Add(player);
        return result;
    }
}
