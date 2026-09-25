using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class StageWorldEventAdapter
{
    private sealed class SavedValuable
    {
        internal SavedValuable(
            ValuableObject valuable,
            float originalValue,
            float appliedValue,
            float multiplier)
        {
            Valuable = valuable;
            OriginalValue = originalValue;
            AppliedValue = appliedValue;
            Multiplier = multiplier;
        }

        internal ValuableObject Valuable { get; }
        internal float OriginalValue { get; }
        internal float AppliedValue { get; }
        internal float Multiplier { get; }
    }

    private static readonly MethodInfo? HingeOpenMethod = AccessTools.Method(typeof(PhysGrabHinge), "OpenImpulse");
    private static readonly MethodInfo? HingeCloseMethod = AccessTools.Method(typeof(PhysGrabHinge), "CloseImpulse");
    private static readonly FieldInfo? HingeJointField = AccessTools.Field(typeof(PhysGrabHinge), "joint");
    private static readonly FieldInfo? HingePhysObjectField =
        AccessTools.Field(typeof(PhysGrabHinge), "physGrabObject");
    private static readonly FieldInfo? ValuableCurrentValueField = AccessTools.Field(typeof(ValuableObject), "dollarValueCurrent");
    private static readonly FieldInfo? ValuableValueSetField =
        AccessTools.Field(typeof(ValuableObject), "dollarValueSet");

    private readonly StagePhysicsConfig _config;
    private readonly Dictionary<int, SavedValuable> _savedValuables = new();
    private readonly HashSet<int> _valueChangeExcludedValuables = new();
    private readonly List<PhysGrabHinge> _doorChaosHinges = new();
    private readonly HashSet<PhysGrabObject> _fragilityTargets = new();
    private StageEffect _effects;
    private TargetFilter _filter;
    private TargetFilter _doorChaosFilter;
    private bool _doorChaosOpenNext;
    private float _nextQuakeAt;
    private float _nextDoorChaosAt;
    private float _nextValueScanAt;

    internal StageWorldEventAdapter(StagePhysicsConfig config)
    {
        _config = config;
    }

    internal bool EnsureAvailable(StageEffect effect, TargetFilter filter) => effect switch
    {
        StageEffect.Quake => filter.Players || filter.Valuables || filter.CosmeticBoxes || filter.Items || filter.Weapons,
        StageEffect.DoorChaos => true,
        StageEffect.ValueSurge or StageEffect.ValueCrash => true,
        StageEffect.Fragility => true,
        _ => false
    };

    internal void Begin(StageEffect effects, TargetFilter filter)
    {
        Stop();
        _effects = effects;
        _filter = filter;
        _doorChaosFilter = Has(StageEffect.DoorChaos) ? filter.WithDoors(true) : filter;
        float now = Time.time;
        _nextQuakeAt = now;
        _nextDoorChaosAt = now;
        _nextValueScanAt = now;
        if (Has(StageEffect.DoorChaos))
        {
            CaptureDoorChaosTargets();
        }
        Tick();
    }

    internal void Tick()
    {
        if (_effects == StageEffect.None)
        {
            return;
        }
        float now = Time.time;
        if (Has(StageEffect.Quake) && now >= _nextQuakeAt)
        {
            ApplyQuake();
            _nextQuakeAt = now + _config.QuakeIntervalSeconds.Value;
        }
        if (Has(StageEffect.DoorChaos) && now >= _nextDoorChaosAt)
        {
            ApplyDoorChaos();
            _nextDoorChaosAt = now + _config.DoorChaosIntervalSeconds.Value;
        }
        if ((Has(StageEffect.ValueSurge) || Has(StageEffect.ValueCrash)) && now >= _nextValueScanAt)
        {
            ApplyValueChangeToNewValuables();
            _nextValueScanAt = now + 1f;
        }
        if (Has(StageEffect.Fragility))
        {
            ApplyFragility(_config.FragilityMultiplierPercent.Value / 100f);
        }
    }

    internal void Stop()
    {
        bool restoreValues =
            (Has(StageEffect.ValueSurge) && _config.ValueSurgeRestoreOnEnd.Value) ||
            (Has(StageEffect.ValueCrash) && _config.ValueCrashRestoreOnEnd.Value);
        if (restoreValues)
        {
            foreach (SavedValuable saved in _savedValuables.Values)
            {
                if (saved.Valuable != null)
                {
                    SavedValuable queued = saved;
                    DeferredObjectCleanupQueue.Enqueue(() =>
                    {
                        if (queued.Valuable != null)
                        {
                            SetValuableValue(
                                queued.Valuable,
                                CalculateRestoredValue(queued));
                        }
                    });
                }
            }
        }
        if (Has(StageEffect.Fragility))
        {
            foreach (PhysGrabObject target in _fragilityTargets)
            {
                PhysGrabObject queued = target;
                DeferredObjectCleanupQueue.Enqueue(() =>
                {
                    if (queued != null)
                    {
                        queued.OverrideFragility(1f);
                    }
                });
            }
        }
        _effects = StageEffect.None;
        _doorChaosFilter = default;
        _savedValuables.Clear();
        _valueChangeExcludedValuables.Clear();
        _doorChaosHinges.Clear();
        _fragilityTargets.Clear();
    }

    internal HashSet<int>? CaptureTaxReturnValuableKeys()
    {
        if (!Has(StageEffect.ValueSurge) && !Has(StageEffect.ValueCrash))
        {
            return null;
        }

        HashSet<int> keys = new();
        foreach (ValuableObject valuable in SceneObjects<ValuableObject>())
        {
            keys.Add(ValuableKey(valuable));
        }
        return keys;
    }

    internal float GetValuableValueMultiplier(ValuableObject? valuable)
    {
        if (valuable == null ||
            (!Has(StageEffect.ValueSurge) && !Has(StageEffect.ValueCrash)))
        {
            return 1f;
        }

        int key = ValuableKey(valuable);
        if (!_savedValuables.TryGetValue(key, out SavedValuable saved) ||
            saved.Valuable != valuable ||
            saved.Multiplier <= 0f ||
            float.IsNaN(saved.Multiplier) ||
            float.IsInfinity(saved.Multiplier))
        {
            return 1f;
        }
        return saved.Multiplier;
    }

    internal void ExcludeNewTaxReturnValuables(HashSet<int>? keysBeforeSpawn)
    {
        if (keysBeforeSpawn == null ||
            (!Has(StageEffect.ValueSurge) && !Has(StageEffect.ValueCrash)))
        {
            return;
        }

        int excluded = 0;
        foreach (ValuableObject valuable in SceneObjects<ValuableObject>())
        {
            int key = ValuableKey(valuable);
            if (!keysBeforeSpawn.Contains(key) &&
                !_savedValuables.ContainsKey(key) &&
                _valueChangeExcludedValuables.Add(key))
            {
                excluded++;
            }
        }

        if (excluded > 0)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Excluded {excluded} tax-return money bag(s) from the current " +
                $"{(Has(StageEffect.ValueSurge) ? "Value Surge" : "Value Crash")} event.");
        }
    }

    private bool Has(StageEffect effect) => StageEffectSet.Contains(_effects, effect);

    private void ApplyFragility(float multiplier)
    {
        TargetFilter valuablesOnly = new(true, false, false, false, false, false, false);
        foreach (PhysGrabObject target in PhysGrabObjectRegistry.Snapshot(valuablesOnly))
        {
            if (target != null)
            {
                target.OverrideFragility(multiplier);
                _fragilityTargets.Add(target);
            }
        }
    }

    private void ApplyQuake()
    {
        float force = _config.QuakeForce.Value;
        if (_filter.Players && GameDirector.instance != null)
        {
            foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
            {
                if (player == null || !player.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized;
                player.ForceImpulse(new Vector3(circle.x * force, force * 0.35f, circle.y * force));
            }
        }

        foreach (PhysGrabObject target in PhysGrabObjectRegistry.Snapshot(_filter))
        {
            if (target == null || target.rb == null || target.grabbed ||
                target.GetComponentInParent<PlayerAvatar>() != null)
            {
                continue;
            }
            Vector3 impulse = UnityEngine.Random.onUnitSphere * force;
            impulse.y = Mathf.Abs(impulse.y) * 0.5f;
            target.rb.AddForce(impulse, ForceMode.VelocityChange);
            target.rb.AddTorque(UnityEngine.Random.onUnitSphere * force, ForceMode.VelocityChange);
        }
    }

    private void ApplyDoorChaos()
    {
        _doorChaosHinges.RemoveAll(hinge => hinge == null || !hinge.gameObject.activeInHierarchy);
        if (_doorChaosHinges.Count == 0)
        {
            CaptureDoorChaosTargets();
        }
        bool open = _doorChaosOpenNext;
        int changed = 0;
        int largeDoorCount = 0;
        foreach (PhysGrabHinge hinge in _doorChaosHinges)
        {
            try
            {
                bool largeDoor = IsLargeMapDoor(hinge);
                DriveHinge(hinge, open, largeDoor, _config.DoorChaosForce.Value);
                changed++;
                if (largeDoor)
                {
                    largeDoorCount++;
                }
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogDebug($"Door Chaos skipped {hinge.name}: {exception.GetBaseException().Message}");
            }
        }
        _doorChaosOpenNext = !_doorChaosOpenNext;
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Door Chaos drove {changed}/{_doorChaosHinges.Count} fixed hinge(s) " +
            $"({largeDoorCount} large map door(s)) toward {(open ? "open" : "closed")}.");
    }

    private void CaptureDoorChaosTargets()
    {
        _doorChaosHinges.Clear();
        List<PhysGrabHinge> eligible = SceneObjects<PhysGrabHinge>().FindAll(IsEligibleHinge);
        List<PhysGrabHinge> mapDoors = eligible.FindAll(hinge => !IsItemHinge(hinge));
        List<PhysGrabHinge> itemHinges = eligible.FindAll(IsItemHinge);
        Shuffle(mapDoors);
        Shuffle(itemHinges);
        AddDoorChaosSelection(mapDoors);
        AddDoorChaosSelection(itemHinges);
        _doorChaosOpenNext = true;
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Door Chaos fixed {_doorChaosHinges.Count} target(s) for this event " +
            $"from {eligible.Count} eligible hinge(s), including " +
            $"{_doorChaosHinges.FindAll(IsLargeMapDoor).Count} large map door(s); " +
            $"configuredDoors={_filter.Doors}, effectiveDoors={_doorChaosFilter.Doors}.");
    }

    private void AddDoorChaosSelection(List<PhysGrabHinge> candidates)
    {
        if (candidates.Count == 0)
        {
            return;
        }
        int count = Mathf.Max(
            1,
            Mathf.CeilToInt(candidates.Count * _config.DoorChaosAffectedPercent.Value / 100f));
        for (int index = 0; index < Mathf.Min(count, candidates.Count); index++)
        {
            _doorChaosHinges.Add(candidates[index]);
        }
    }

    private bool IsEligibleHinge(PhysGrabHinge hinge)
    {
        if (!_doorChaosFilter.Doors || hinge == null || hinge.GetComponentInParent<TruckDoor>() != null ||
            hinge.GetComponentInParent<ShopKeycardDoor>() != null ||
            hinge.GetComponentInParent<ExtractionPoint>() != null)
        {
            return false;
        }
        return IsItemHinge(hinge)
            ? _config.DoorChaosHingedItemsEnabled.Value
            : true;
    }

    private static bool IsItemHinge(PhysGrabHinge hinge) =>
        hinge.GetComponentInParent<ValuableObject>() != null ||
        hinge.GetComponentInParent<ItemAttributes>() != null;

    private static bool IsLargeMapDoor(PhysGrabHinge hinge) =>
        !IsItemHinge(hinge);

    private static void DriveHinge(
        PhysGrabHinge hinge,
        bool open,
        bool largeDoor,
        int configuredForce)
    {
        PhysGrabObject? physObject =
            HingePhysObjectField?.GetValue(hinge) as PhysGrabObject ??
            hinge.GetComponentInParent<PhysGrabObject>();
        Rigidbody? body = physObject?.rb ??
            hinge.GetComponent<Rigidbody>() ??
            hinge.GetComponentInParent<Rigidbody>();
        HingeJoint? joint =
            HingeJointField?.GetValue(hinge) as HingeJoint ??
            hinge.GetComponent<HingeJoint>();
        if (open)
        {
            HingeOpenMethod?.Invoke(hinge, null);
        }
        else
        {
            HingeCloseMethod?.Invoke(hinge, new object[] { false });
        }
        if (body == null || joint == null || body.isKinematic)
        {
            return;
        }

        physObject?.OverrideMass(largeDoor ? 1f : 2f, 1.5f);
        body.WakeUp();
        Vector3 axis = joint.transform.TransformDirection(joint.axis).normalized;
        float direction;
        if (open)
        {
            JointLimits limits = joint.limits;
            direction = Mathf.Abs(limits.max) >= Mathf.Abs(limits.min) ? 1f : -1f;
            if (Mathf.Abs(limits.max) < 5f && Mathf.Abs(limits.min) < 5f)
            {
                direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            }
        }
        else
        {
            float angle = joint.angle;
            direction = Mathf.Abs(angle) > 1f ? -Mathf.Sign(angle) : 0f;
        }
        float speed = Mathf.Max(1f, configuredForce) * (largeDoor ? 1f : 0.75f);
        body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, speed * 1.25f);
        Vector3 axialVelocity = axis * direction * speed;
        body.angularVelocity = Vector3.ProjectOnPlane(body.angularVelocity, axis) + axialVelocity;
    }

    private void ApplyValueChangeToNewValuables()
    {
        float multiplier = Has(StageEffect.ValueSurge)
            ? _config.ValueSurgeMultiplierPercent.Value / 100f
            : _config.ValueCrashMultiplierPercent.Value / 100f;
        int changed = 0;
        foreach (ValuableObject valuable in SceneObjects<ValuableObject>())
        {
            if (valuable == null ||
                ValuableValueSetField?.GetValue(valuable) is bool valueSet && !valueSet)
            {
                continue;
            }
            int key = ValuableKey(valuable);
            if (_savedValuables.ContainsKey(key) ||
                _valueChangeExcludedValuables.Contains(key))
            {
                continue;
            }
            float current = ReadValuableValue(valuable);
            float applied = Mathf.Max(0f, Mathf.Round(current * multiplier));
            _savedValuables[key] =
                new SavedValuable(valuable, current, applied, multiplier);
            SetValuableValue(valuable, applied);
            changed++;
        }
        if (changed > 0)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"{(Has(StageEffect.ValueSurge) ? "Value Surge" : "Value Crash")} updated " +
                $"{changed} newly discovered valuable(s), including extraction, cart, and truck areas.");
        }
    }

    private static float ReadValuableValue(ValuableObject valuable) =>
        ValuableCurrentValueField?.GetValue(valuable) is float current ? current : 0f;

    private static float CalculateRestoredValue(SavedValuable saved)
    {
        float current = ReadValuableValue(saved.Valuable);
        if (Mathf.Approximately(current, saved.AppliedValue))
        {
            return saved.OriginalValue;
        }

        // Remove only the event multiplier. Damage or healing received while the
        // event was active remains at the same percentage after the event ends.
        return Mathf.Max(
            0f,
            Mathf.Round(current / Mathf.Max(0.01f, saved.Multiplier)));
    }

    private static void SetValuableValue(ValuableObject valuable, float value)
    {
        try
        {
            PhotonView? view = valuable.GetComponent<PhotonView>() ??
                valuable.GetComponentInParent<PhotonView>() ??
                valuable.GetComponentInChildren<PhotonView>(true);
            if (SemiFunc.IsMultiplayer() && view != null)
            {
                view.RPC(nameof(ValuableObject.DollarValueSetRPC), RpcTarget.All, value);
            }
            else
            {
                valuable.DollarValueSetRPC(value);
            }
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug($"Valuable price update failed for {valuable.name}: {exception.Message}");
        }
    }

    private static int ValuableKey(ValuableObject valuable)
    {
        PhotonView? view = valuable.GetComponent<PhotonView>() ?? valuable.GetComponentInParent<PhotonView>();
        return view != null && view.ViewID != 0 ? view.ViewID : valuable.GetInstanceID();
    }

    private static List<T> SceneObjects<T>() where T : Component
    {
        List<T> result = new();
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component != null && component.gameObject.scene.IsValid() && component.gameObject.activeInHierarchy)
            {
                result.Add(component);
            }
        }
        return result;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int index = 0; index < list.Count; index++)
        {
            int selected = UnityEngine.Random.Range(index, list.Count);
            (list[index], list[selected]) = (list[selected], list[index]);
        }
    }
}
