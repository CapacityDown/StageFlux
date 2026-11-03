using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

// Only standard game prefabs and master-authorized vanilla RPCs cross the network.
// The owner controls every delayed spawn so no wave can leak into another event.
internal sealed class SunstoneEventAdapter
{
    private readonly MonoBehaviour _owner;
    private readonly StagePhysicsConfig _config;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly RandomStagePointSelector _points;
    private readonly List<Source> _sources = new();
    private readonly Dictionary<StageEffect, float> _nextWave = new();
    private readonly HashSet<StageEffect> _pending = new();
    private readonly HashSet<StageEffect> _warned = new();
    private readonly Dictionary<int, float> _rollUntil = new();
    private readonly List<(PhysGrabObject Target, float Until)> _protectedValuables = new();
    private StageEffect _effects;
    private TargetFilter _targets;
    private int _generation;
    private float _endsAt;

    internal SunstoneEventAdapter(MonoBehaviour owner, StagePhysicsConfig config,
        VanillaSpawnEffectResolver resolver, RandomStagePointSelector points)
    { _owner = owner; _config = config; _resolver = resolver; _points = points; }

    internal bool EnsureAvailable(StageEffect effect) => effect == StageEffect.Rockfall
        ? RockslideSources().Count > 0
        : _resolver.TryResolve(effect, out ResolvedSpawnPrefab prefab) &&
          (effect != StageEffect.ArcaneBurst ||
           prefab.Prefab.GetComponentInChildren<ValuableWizardCrystal>(true)?.affectPrefab?.IsValid() == true);

    internal void Begin(StageEffect effects, TargetFilter targets, float remaining)
    {
        _targets = targets;
        _endsAt = Time.time + remaining;
        _effects = effects & SunstoneEventCatalog.All;
        Tick(remaining);
    }

    internal void Tick(float remaining)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _effects == StageEffect.None) return;
        _endsAt = Time.time + remaining;
        for (int index = _sources.Count - 1; index >= 0; index--)
        {
            Source source = _sources[index];
            if (source.Object == null || Time.time >= source.Expires)
            {
                DeferredObjectCleanupQueue.Enqueue(source.Object);
                _sources.RemoveAt(index);
                continue;
            }
            if (source.Effect != StageEffect.Rockfall)
                HoldCarrier(source.Object);
        }
        foreach (StageEffect effect in new[] { StageEffect.RechargeCloud, StageEffect.Rockfall, StageEffect.ArcaneBurst })
        {
            if (!StageEffectSet.Contains(_effects, effect) || remaining <= 1f || _pending.Contains(effect) ||
                (_nextWave.TryGetValue(effect, out float next) && Time.time < next)) continue;
            _nextWave[effect] = Time.time + _config.SunstoneSettings[effect].Interval.Value;
            if (!EnsureAvailable(effect)) continue;
            _pending.Add(effect);
            _owner.StartCoroutine(Wave(effect, _generation));
        }
    }

    internal void Stop()
    {
        _generation++;
        _effects = StageEffect.None;
        foreach (Source source in _sources)
        {
            // Stop charging immediately, even when destruction is spread across frames.
            if (source.Object != null && source.Effect == StageEffect.RechargeCloud)
            {
                var crystal = source.Object.GetComponentInChildren<ValuableDesertEnergyCrystalChunk>(true);
                if (crystal != null) crystal.chargeCloudTimer = 0f;
            }
            DeferredObjectCleanupQueue.Enqueue(source.Object);
        }
        _sources.Clear(); _nextWave.Clear(); _pending.Clear(); _rollUntil.Clear(); _warned.Clear();
    }

    internal void ClearProtection() => _protectedValuables.Clear();

    internal void TickProtection()
    {
        // Keep the tail alive during Waiting as well. Other simultaneous events
        // refresh the same vanilla timer with short values, so one long write is
        // not enough to guarantee the configured protection after Roll expires.
        for (int index = _protectedValuables.Count - 1; index >= 0; index--)
        {
            var entry = _protectedValuables[index];
            if (entry.Target == null || entry.Target.dead || Time.time >= entry.Until || !_config.TargetValuables.Value)
                _protectedValuables.RemoveAt(index);
            else entry.Target.OverrideIndestructible();
        }
    }

    private bool Valid(int generation) => generation == _generation && _effects != StageEffect.None &&
        SemiFunc.IsMasterClientOrSingleplayer() && Time.time < _endsAt;

    private IEnumerator Wave(StageEffect effect, int generation)
    {
        yield return null;
        try
        {
            if (!Valid(generation)) yield break;
            SunstoneEventSettings settings = _config.SunstoneSettings[effect];
            _sources.RemoveAll(source => source.Object == null);
            int requested = UnityEngine.Random.Range(Mathf.Min(settings.CountMin.Value, settings.CountMax.Value),
                Mathf.Max(settings.CountMin.Value, settings.CountMax.Value) + 1);
            int count = SunstoneEventPolicy.WaveCapacity(requested, settings.MaximumActive.Value,
                _sources.FindAll(source => source.Effect == effect).Count);
            HashSet<int> used = new();
            RockSlide? slide = null;
            Vector3 aim = default;
            if (effect == StageEffect.Rockfall)
            {
                List<RockSlide> slides = RockslideSources();
                if (slides.Count == 0 || count <= 0) yield break;
                slide = slides[UnityEngine.Random.Range(0, slides.Count)];
                if (!_points.TryGetNavigablePosition(0f, used, out aim, out _)) yield break;
                // Others execute the standard warning only; its spawning branch is master-only.
                if (SemiFunc.IsMultiplayer()) slide.GetComponent<PhotonView>().RPC("RockSlideRPC", RpcTarget.Others, aim);
                slide.triggerSound.Play(slide.transform.position);
                yield return new WaitForSeconds(1.5f);
            }
            List<Vector3> rockPositions = new();
            for (int index = 0; index < count && Valid(generation); index++)
            {
                if (effect == StageEffect.Rockfall)
                {
                    if (slide == null) break;
                    TrySpawnRock(slide, aim, rockPositions, settings.MinimumDistance.Value);
                }
                else
                {
                    if (!_points.TryGetNavigablePosition(settings.MinimumDistance.Value, used, out Vector3 point, out Quaternion rotation) ||
                        !FarEnough(point, settings.MinimumDistance.Value)) continue;
                    GameObject? source = TrySpawnCrystal(effect, point + Vector3.up * 0.1f, rotation);
                    if (source != null)
                    {
                        // Reliable server ordering: instantiate, initialization, effect, cleanup.
                        yield return new WaitForSeconds(0.3f);
                        if (!Valid(generation) || source == null) yield break;
                        try
                        {
                            if (effect == StageEffect.RechargeCloud) ActivateCloud(source);
                            else ActivateArcane(source);
                        }
                        catch (Exception error) { Warn(effect, error); DeferredObjectCleanupQueue.Enqueue(source); }
                    }
                }
                yield return new WaitForSeconds(0.1f);
            }
        }
        finally { if (generation == _generation) _pending.Remove(effect); }
    }

    private GameObject? TrySpawnCrystal(StageEffect effect, Vector3 position, Quaternion rotation)
    {
        GameObject? source = null;
        try
        {
            if (!_resolver.TryResolve(effect, out ResolvedSpawnPrefab prefab)) return null;
            source = SemiFunc.IsMultiplayer()
                ? PhotonNetwork.Instantiate(prefab.ResourcePath, position, rotation, 0)
                : UnityEngine.Object.Instantiate(prefab.Prefab, position, rotation);
            source.AddComponent<StagePhysicsEffectCarrierMarker>();
            if (effect == StageEffect.RechargeCloud) source.AddComponent<RechargeCloudSourceMarker>();
            source.name = "StageFlux_" + effect + "_Carrier";
            // Prevent temporary sources from adding money or increasing the level's value tally.
            foreach (ValuableObject valuable in source.GetComponentsInChildren<ValuableObject>(true))
            { valuable.dollarValueSet = true; valuable.dollarValueOriginal = 0f; valuable.dollarValueCurrent = 0f; }
            var phys = source.GetComponentInChildren<PhysGrabObject>(true);
            if (phys != null) phys.PhysRidingDisabledSet(true);
            var crystal = source.GetComponentInChildren<ValuableWizardCrystal>(true);
            if (crystal != null)
            {
                // Keep the native client-side burst visuals, but replace only the host's
                // unfiltered target enumeration for this carrier, never a real valuable.
                var detector = source.GetComponentInChildren<PhysGrabObjectImpactDetector>(true);
                if (detector != null) detector.onDestroy.RemoveListener(crystal.Destroyed);
            }
            _sources.Add(new Source(source, effect, effect == StageEffect.RechargeCloud
                ? Mathf.Min(_endsAt, Time.time + _config.RechargeCloudDurationSeconds.Value + 0.3f)
                : Mathf.Min(_endsAt, Time.time + 1.5f)));
            HoldCarrier(source);
            return source;
        }
        catch (Exception error) { Warn(effect, error); DeferredObjectCleanupQueue.Enqueue(source); return null; }
    }

    private static void HoldCarrier(GameObject source)
    {
        PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
        var phys = source.GetComponentInChildren<PhysGrabObject>(true);
        if (phys == null) return;
        phys.OverrideKinematic(1f); phys.OverrideGrabDisable(1f); phys.OverrideIndestructible(1f);
        // A vanilla guest may briefly attempt to grab the visible source. Reject that
        // through the standard grab-release message; never move the cloud with the grabber.
        foreach (PhysGrabber grabber in new List<PhysGrabber>(phys.playerGrabbing))
            if (grabber != null) grabber.OverrideGrabRelease(phys.photonView.ViewID, 0.5f);
    }

    private void ActivateCloud(GameObject source)
    {
        var crystal = source.GetComponentInChildren<ValuableDesertEnergyCrystalChunk>(true);
        if (crystal == null) throw new InvalidOperationException("Missing energy crystal.");
        float duration = Mathf.Min(_config.RechargeCloudDurationSeconds.Value, _endsAt - Time.time);
        if (duration <= 0f) return;
        PhotonView view = crystal.GetComponent<PhotonView>();
        if (SemiFunc.IsMultiplayer()) view.RPC(nameof(ValuableDesertEnergyCrystalChunk.ChargeCloudStartRPC), RpcTarget.AllViaServer, duration);
        else crystal.ChargeCloudStartRPC(duration);
    }

    private void ActivateArcane(GameObject source)
    {
        var crystal = source.GetComponentInChildren<ValuableWizardCrystal>(true);
        var detector = source.GetComponentInChildren<PhysGrabObjectImpactDetector>(true);
        if (crystal == null || detector == null || !crystal.affectPrefab.IsValid())
            throw new InvalidOperationException("Missing wizard crystal effect.");
        Vector3 origin = source.transform.position;
        List<int> ids = new(); List<float> times = new(), dx = new(), dy = new(), dz = new();
        HashSet<int> seen = new();
        // Do not layer another burst over an existing native Roll item/effect.
        // Scan once per burst, not once per target or per frame.
        foreach (SemiAffectTorque torque in UnityEngine.Object.FindObjectsByType<SemiAffectTorque>(FindObjectsSortMode.None))
        {
            SemiAffect? existing = torque.GetComponent<SemiAffect>();
            if (existing != null && existing.timer > 0f && existing.targetPhysGrabObject != null)
                seen.Add(existing.targetPhysGrabObject.GetInstanceID());
        }
        foreach (PhysGrabObject target in PhysGrabObjectRegistry.Snapshot(_targets))
        {
            if (target == null || target.dead || target.isPlayer ||
                StagePhysicsEffectCarrierUtility.IsInternalCarrier(target)) continue;
            AddArcaneTarget(target, null, crystal, origin, seen, ids, times, dx, dy, dz);
        }
        if (_targets.Players)
            foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
                if (PlayerAvatarState.IsLiving(player) && player.tumble != null &&
                    !StagePhysicsEffectCarrierUtility.IsInternalCarrier(player) && !RoleShuffleCompatibility.IsTricksterDecoy(player))
                    AddArcaneTarget(player.tumble.physGrabObject, player, crystal, origin, seen, ids, times, dx, dy, dz);
        PhotonView view = crystal.GetComponent<PhotonView>();
        if (SemiFunc.IsMultiplayer())
        {
            view.RPC(nameof(ValuableWizardCrystal.CrystalDestroyedEffectRPC), RpcTarget.AllViaServer,
                ids.ToArray(), times.ToArray(), dx.ToArray(), dy.ToArray(), dz.ToArray(), origin);
            // DestroyObjectRPC invokes the vanilla purple-smoke listener on vanilla peers.
            view.RPC(nameof(PhysGrabObjectImpactDetector.DestroyObjectRPC), RpcTarget.AllViaServer, false);
        }
        else detector.DestroyObject(false);
        crystal.PurpleSmokeSpawn(origin);
        crystal.rollStaffHitSound.Play(origin);
    }

    private void AddArcaneTarget(PhysGrabObject target, PlayerAvatar? player, ValuableWizardCrystal crystal,
        Vector3 origin, HashSet<int> seen, List<int> ids, List<float> times, List<float> dx, List<float> dy, List<float> dz)
    {
        if (target == null || StagePhysicsEffectCarrierUtility.IsInternalCarrier(target) || RoleShuffleCompatibility.IsTricksterDecoy(target)) return;
        Transform transform = player != null ? player.transform : target.transform;
        if (Vector3.Distance(transform.position, origin) > crystal.affectRadius) return;
        int key = target.GetInstanceID();
        if (!seen.Add(key) || (_rollUntil.TryGetValue(key, out float until) && Time.time < until)) return;
        // SemiAffectTorque.Start doubles player timers. Clamp the actual lifetime
        // first, then halve the wire payload so the effect cannot outlive the event.
        float payloadDuration = SunstoneEventPolicy.RollPayloadDuration(
            UnityEngine.Random.Range(crystal.affectTimeMin, crystal.affectTimeMax), _endsAt - Time.time, player != null);
        if (payloadDuration <= 0f) return;
        float duration = player != null ? payloadDuration * 2f : payloadDuration;
        PhotonView view = player != null ? player.photonView : target.photonView;
        if (SemiFunc.IsMultiplayer() && (view == null || view.ViewID <= 0)) return;
        Vector3 direction = crystal.AffectDirection(transform.position, origin);
        if (SemiFunc.IsMultiplayer())
        { ids.Add(view.ViewID); times.Add(payloadDuration); dx.Add(direction.x); dy.Add(direction.y); dz.Add(direction.z); }
        else
        {
            GameObject affect = UnityEngine.Object.Instantiate(crystal.affectPrefab.Prefab, transform.position, Quaternion.identity);
            SemiAffect component = affect.GetComponent<SemiAffect>();
            component.positionOfOriginalAreaOfEffect = origin; component.direction = direction; component.flatTorque = true;
            component.SetupSingleplayer(transform, target, payloadDuration, player!);
        }
        _rollUntil[key] = Time.time + duration;
        if (_config.TargetValuables.Value && _config.ArcaneBurstProtectValuables.Value && target.GetComponent<ValuableObject>() != null)
        {
            float protectUntil = Time.time + duration + _config.ValuableProtectionReleaseDelaySeconds.Value;
            _protectedValuables.RemoveAll(entry => entry.Target == target);
            _protectedValuables.Add((target, protectUntil));
            target.OverrideIndestructible();
        }
    }

    private void TrySpawnRock(RockSlide slide, Vector3 aim, List<Vector3> used, float minimumDistance)
    {
        GameObject? rock = null;
        try
        {
            if (!VanillaSpawnEffectResolver.TryGetVanillaPrefab(slide.rockPrefab, out ResolvedSpawnPrefab prefab)) return;
            Vector3 position = default;
            bool found = false;
            for (int attempt = 0; attempt < 12; attempt++)
                if (slide.TryGetSpawnPosition(used, out position) && FarEnough(position, minimumDistance)) { found = true; break; }
            if (!found) return;
            used.Add(position);
            rock = SemiFunc.IsMultiplayer() ? PhotonNetwork.Instantiate(prefab.ResourcePath, position, UnityEngine.Random.rotation, 0)
                : UnityEngine.Object.Instantiate(prefab.Prefab, position, UnityEngine.Random.rotation);
            rock.AddComponent<StagePhysicsEffectCarrierMarker>();
            RockSlideRock component = rock.GetComponent<RockSlideRock>();
            Vector3 direction = (aim - position).normalized;
            float size = UnityEngine.Random.Range(component.sizeRange.x, component.sizeRange.y);
            if (SemiFunc.IsMultiplayer()) component.GetComponent<PhotonView>().RPC("LaunchRPC", RpcTarget.AllViaServer, direction, size);
            else component.Launch(direction, size);
            _sources.Add(new Source(rock, StageEffect.Rockfall, Mathf.Min(_endsAt, Time.time + Mathf.Max(component.lifetime, component.despawnGracePeriod) + 2f)));
        }
        catch (Exception error) { Warn(StageEffect.Rockfall, error); DeferredObjectCleanupQueue.Enqueue(rock); }
    }

    private static List<RockSlide> RockslideSources()
    {
        List<RockSlide> sources = new();
        foreach (RockSlide slide in UnityEngine.Object.FindObjectsByType<RockSlide>(FindObjectsSortMode.None))
            if (slide != null && slide.gameObject.scene.IsValid() && slide.gameObject.activeInHierarchy &&
                !StagePhysicsEffectCarrierUtility.IsInternalCarrier(slide) &&
                VanillaSpawnEffectResolver.TryGetVanillaPrefab(slide.rockPrefab, out ResolvedSpawnPrefab prefab) &&
                prefab.Prefab.GetComponent<RockSlideRock>() != null &&
                (!SemiFunc.IsMultiplayer() || (slide.GetComponent<PhotonView>() != null && slide.GetComponent<PhotonView>().ViewID > 0)))
                sources.Add(slide);
        return sources;
    }

    private static bool FarEnough(Vector3 point, float distance)
    {
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
            if (PlayerAvatarState.IsLiving(player) && Vector3.Distance(point, player.transform.position) < distance) return false;
        return true;
    }

    private void Warn(StageEffect effect, Exception error)
    {
        if (_warned.Add(effect)) StagePhysicsEventsPlugin.ModLogger.LogWarning($"{effect} wave skipped: {error.GetBaseException().Message}");
    }

    private readonly struct Source
    {
        internal Source(GameObject instance, StageEffect effect, float expires) { Object = instance; Effect = effect; Expires = expires; }
        internal GameObject Object { get; }
        internal StageEffect Effect { get; }
        internal float Expires { get; }
    }
}

[HarmonyPatch(typeof(ValuableDesertEnergyCrystalChunk), "ChargeItemsInRange")]
internal static class RechargeCloudCarrierTargetsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ValuableDesertEnergyCrystalChunk __instance)
    {
        if (__instance.GetComponentInParent<RechargeCloudSourceMarker>() == null) return true;
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return false;
        // Reuse the native charger and rate, filtering only our event carrier's
        // recipients. Real crystals and other mods' normal items are untouched.
        var batteries = __instance.chargeCloudBatteries;
        batteries.Clear();
        foreach (PhysGrabObject target in SemiFunc.PhysGrabObjectGetAllWithinRange(
            __instance.chargeCloudRadius, __instance.physGrabObject.midPoint))
        {
            if (target == null || target.dead || StagePhysicsEffectCarrierUtility.IsInternalCarrier(target) ||
                RoleShuffleCompatibility.IsTricksterDecoy(target)) continue;
            ItemBattery? battery = target.GetComponent<ItemBattery>();
            if (SemiFunc.BatteryChargeCondition(battery) && !batteries.Contains(battery!)) batteries.Add(battery!);
        }
        if (batteries.Count > 0)
        {
            float amount = SemiFunc.BatteryGetChargeRate(3) * __instance.chargeCloudChargeMultiplier / batteries.Count;
            foreach (ItemBattery battery in batteries) battery.ChargeBattery(__instance.gameObject, amount);
        }
        return false;
    }
}

internal sealed class RechargeCloudSourceMarker : MonoBehaviour { }
