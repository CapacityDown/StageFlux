using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class DangerousValuablesEventAdapter
{
    private static readonly MethodInfo? CarUpdateStateMethod =
        AccessTools.Method(typeof(ValuableCar), "UpdateState", new[] { typeof(ValuableCar.State) });
    private static readonly MethodInfo? PlaneUpdateStateMethod =
        AccessTools.Method(typeof(ValuablePlane), "UpdateState", new[] { typeof(ValuablePlane.State) });
    private readonly StagePhysicsConfig _config;
    private readonly List<DangerousInstance> _instances = new();
    private bool _active;
    private int _targetCount;
    private float _nextReactivationAt;

    internal DangerousValuablesEventAdapter(
        StagePhysicsConfig config)
    {
        _config = config;
    }

    internal bool EnsureAvailable()
    {
        // Availability is finalized when the event begins, after stage valuables
        // and their prefab presets have finished loading. Rejecting the event
        // during the interval preview made it impossible to select on some maps.
        return _config.DangerousValuablesHasEnabledType;
    }

    internal void Begin(int targetCount)
    {
        Stop();
        _targetCount = Mathf.Clamp(
            targetCount,
            1,
            _config.DangerousValuablesMaximumActiveInstances.Value);
        _active = true;
        CollectAndActivatePlacedValuables();
        _nextReactivationAt =
            Time.time + _config.DangerousValuablesReactivationIntervalSeconds.Value;
    }

    internal void Tick(float remainingSeconds)
    {
        if (!_active)
        {
            return;
        }
        _instances.RemoveAll(instance => instance.Source == null);
        if (remainingSeconds <= 3f || Time.time < _nextReactivationAt)
        {
            return;
        }
        ReactivateExisting();
        _nextReactivationAt =
            Time.time + _config.DangerousValuablesReactivationIntervalSeconds.Value;
    }

    internal void Stop()
    {
        _active = false;
        // Activated stage valuables remain in their current dangerous state
        // after the event. Dangerous Valuables never creates new valuables.
        _instances.Clear();
        _nextReactivationAt = 0f;
    }

    private static bool Activate(DangerousInstance instance)
    {
        if (instance.Source == null ||
            !RoleShuffleCompatibility.CanActivateValuableEffect(instance.Effect))
        {
            return false;
        }
        switch (instance.Kind)
        {
            case DangerousValuableKind.IceSaw:
                instance.Source.GetComponentInChildren<IceSawValuable>(true)?.TrapActivate();
                break;
            case DangerousValuableKind.Blender:
                instance.Source.GetComponentInChildren<BlenderValuable>(true)?.TrapActivate();
                break;
            case DangerousValuableKind.Flamethrower:
                instance.Source.GetComponentInChildren<FlamethrowerValuable>(true)?.GrabTrigger();
                break;
            case DangerousValuableKind.Egg:
                ValuableEgg? egg = instance.Source.GetComponentInChildren<ValuableEgg>(true);
                if (egg != null)
                {
                    SetEggState(egg, ValuableEgg.EggState.State5);
                }
                break;
            case DangerousValuableKind.Car:
                ValuableCar? car = instance.Source.GetComponentInChildren<ValuableCar>(true);
                if (car != null)
                {
                    SetCarState(car, ValuableCar.State.MoveForward);
                }
                break;
            case DangerousValuableKind.Plane:
                ValuablePlane? plane = instance.Source.GetComponentInChildren<ValuablePlane>(true);
                if (plane != null)
                {
                    SetPlaneState(plane, ValuablePlane.State.MoveForward);
                }
                break;
            case DangerousValuableKind.Broom:
                WizardBroomValuable? broom =
                    instance.Source.GetComponentInChildren<WizardBroomValuable>(true);
                if (broom != null)
                {
                    broom.TrapActivate();
                }
                else
                {
                    PhysGrabObjectImpactDetector? detector =
                        instance.Source.GetComponentInChildren<PhysGrabObjectImpactDetector>(true);
                    if (detector != null)
                    {
                        InvokeActivation(detector, instance.Activation);
                    }
                }
                break;
        }
        return true;
    }

    private void ReactivateExisting()
    {
        foreach (DangerousInstance instance in _instances)
        {
            if (instance.Source == null)
            {
                continue;
            }
            try
            {
                if (!RoleShuffleCompatibility.CanActivateValuableEffect(instance.Effect))
                {
                    continue;
                }
                switch (instance.Kind)
                {
                    case DangerousValuableKind.IceSaw:
                        IceSawValuable? saw = instance.Source.GetComponentInChildren<IceSawValuable>(true);
                        if (saw != null)
                        {
                            saw.TrapActivate();
                        }
                        break;
                    case DangerousValuableKind.Blender:
                        BlenderValuable? blender = instance.Source.GetComponentInChildren<BlenderValuable>(true);
                        if (blender != null)
                        {
                            blender.TrapActivate();
                        }
                        break;
                    case DangerousValuableKind.Flamethrower:
                        FlamethrowerValuable? flame =
                            instance.Source.GetComponentInChildren<FlamethrowerValuable>(true);
                        if (flame != null)
                        {
                            flame.GrabTrigger();
                        }
                        break;
                    case DangerousValuableKind.Car:
                        ValuableCar? car = instance.Source.GetComponentInChildren<ValuableCar>(true);
                        if (car != null)
                        {
                            SetCarState(car, ValuableCar.State.MoveForward);
                        }
                        break;
                    case DangerousValuableKind.Plane:
                        ValuablePlane? plane = instance.Source.GetComponentInChildren<ValuablePlane>(true);
                        if (plane != null)
                        {
                            SetPlaneState(plane, ValuablePlane.State.MoveForward);
                        }
                        break;
                    case DangerousValuableKind.Egg:
                    case DangerousValuableKind.Broom:
                        Activate(instance);
                        break;
                }
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogDebug(
                    $"Could not reactivate dangerous {instance.Kind} valuable: {exception.Message}");
            }
        }
    }

    private void CollectAndActivatePlacedValuables()
    {
        List<DangerousInstance> placed = FindPlacedDangerousValuables();
        Shuffle(placed);
        int availableSlots = Mathf.Max(
            0,
            Mathf.Min(
                _targetCount,
                _config.DangerousValuablesMaximumActiveInstances.Value) -
            _instances.Count);
        int added = 0;
        foreach (DangerousInstance instance in placed)
        {
            if (added >= availableSlots || _instances.Exists(current => current.Source == instance.Source))
            {
                continue;
            }
            try
            {
                if (!RoleShuffleCompatibility.CanActivateValuableEffect(instance.Effect) ||
                    !Activate(instance))
                {
                    continue;
                }
                _instances.Add(instance);
                added++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogDebug(
                    $"Could not activate placed dangerous {instance.Kind} valuable: {exception.Message}");
            }
        }
        StagePhysicsEventsPlugin.ModLogger.LogInfo(
            $"Dangerous Valuables activated {added} already-placed stage valuable(s).");
    }

    private List<DangerousInstance> FindPlacedDangerousValuables()
    {
        List<DangerousInstance> result = new();
        HashSet<int> seen = new();
        foreach (ValuableObject valuable in Resources.FindObjectsOfTypeAll<ValuableObject>())
        {
            if (valuable == null || !valuable.gameObject.scene.IsValid() ||
                !valuable.gameObject.activeInHierarchy)
            {
                continue;
            }
            GameObject source = valuable.gameObject;
            if (StagePhysicsEffectCarrierUtility.IsInternalCarrier(valuable) ||
                !TryIdentifyKind(
                    source,
                    out DangerousValuableKind kind,
                    out Component? effect) ||
                !IsKindEnabled(kind) ||
                !RoleShuffleCompatibility.CanActivateValuableEffect(effect))
            {
                continue;
            }
            int key = source.GetInstanceID();
            if (!seen.Add(key))
            {
                continue;
            }
            result.Add(new DangerousInstance(
                source,
                kind,
                LevitationActivationKind.None,
                effect));
        }
        return result;
    }

    private static bool TryIdentifyKind(
        GameObject source,
        out DangerousValuableKind kind,
        out Component? effect)
    {
        effect = source.GetComponentInChildren<IceSawValuable>(true);
        if (effect != null)
        {
            kind = DangerousValuableKind.IceSaw;
            return true;
        }
        effect = source.GetComponentInChildren<BlenderValuable>(true);
        if (effect != null)
        {
            kind = DangerousValuableKind.Blender;
            return true;
        }
        effect = source.GetComponentInChildren<FlamethrowerValuable>(true);
        if (effect != null)
        {
            kind = DangerousValuableKind.Flamethrower;
            return true;
        }
        effect = source.GetComponentInChildren<ValuableEgg>(true);
        if (effect != null)
        {
            kind = DangerousValuableKind.Egg;
            return true;
        }
        effect = source.GetComponentInChildren<ValuableCar>(true);
        if (effect != null)
        {
            kind = DangerousValuableKind.Car;
            return true;
        }
        effect = source.GetComponentInChildren<ValuablePlane>(true);
        if (effect != null)
        {
            kind = DangerousValuableKind.Plane;
            return true;
        }
        effect = source.GetComponentInChildren<WizardBroomValuable>(true);
        if (effect != null)
        {
            kind = DangerousValuableKind.Broom;
            return true;
        }
        kind = default;
        effect = null;
        return false;
    }

    private static void SetEggState(ValuableEgg egg, ValuableEgg.EggState state)
    {
        PhotonView? view = egg.GetComponent<PhotonView>() ?? egg.GetComponentInParent<PhotonView>();
        if (SemiFunc.IsMultiplayer() && view != null && view.ViewID != 0)
        {
            view.RPC("SetStateRPC", RpcTarget.All, state);
        }
        else
        {
            egg.SetStateRPC(state);
        }
    }

    private static void SetCarState(ValuableCar car, ValuableCar.State state)
    {
        PhotonView? view = car.GetComponent<PhotonView>() ?? car.GetComponentInParent<PhotonView>();
        if (SemiFunc.IsMultiplayer() && view != null && view.ViewID != 0)
        {
            view.RPC("UpdateStateRPC", RpcTarget.All, state);
        }
        else if (CarUpdateStateMethod != null)
        {
            CarUpdateStateMethod.Invoke(car, new object[] { state });
        }
    }

    private static void SetPlaneState(ValuablePlane plane, ValuablePlane.State state)
    {
        PhotonView? view = plane.GetComponent<PhotonView>() ?? plane.GetComponentInParent<PhotonView>();
        if (SemiFunc.IsMultiplayer() && view != null && view.ViewID != 0)
        {
            view.RPC("UpdateStateRPC", RpcTarget.All, (int)state);
        }
        else if (PlaneUpdateStateMethod != null)
        {
            PlaneUpdateStateMethod.Invoke(plane, new object[] { state });
        }
    }

    private bool IsKindEnabled(DangerousValuableKind kind) => kind switch
    {
        DangerousValuableKind.IceSaw => _config.DangerousValuablesIceSawEnabled.Value,
        DangerousValuableKind.Blender => _config.DangerousValuablesBlenderEnabled.Value,
        DangerousValuableKind.Flamethrower => _config.DangerousValuablesFlamethrowerEnabled.Value,
        DangerousValuableKind.Egg => _config.DangerousValuablesEggEnabled.Value,
        DangerousValuableKind.Car => _config.DangerousValuablesCarEnabled.Value,
        DangerousValuableKind.Plane => _config.DangerousValuablesPlaneEnabled.Value,
        DangerousValuableKind.Broom => _config.DangerousValuablesBroomEnabled.Value,
        _ => false
    };

    private static void InvokeActivation(
        PhysGrabObjectImpactDetector detector,
        LevitationActivationKind activation)
    {
        Vector3 point = detector.transform.position;
        switch (activation)
        {
            case LevitationActivationKind.ImpactLight:
                detector.ImpactLight(100f, point);
                break;
            case LevitationActivationKind.ImpactMedium:
                detector.ImpactMedium(100f, point);
                break;
            case LevitationActivationKind.ImpactHeavy:
                detector.ImpactHeavy(100f, point);
                break;
            case LevitationActivationKind.BreakLight:
                detector.BreakLight(point, true);
                break;
            case LevitationActivationKind.BreakMedium:
                detector.BreakMedium(point, true);
                break;
            case LevitationActivationKind.BreakHeavy:
                detector.BreakHeavy(point, true);
                break;
            case LevitationActivationKind.Destroy:
                detector.DestroyObject(false);
                break;
            default:
                throw new InvalidOperationException("No synchronized Broom activation was resolved.");
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int index = 0; index < list.Count; index++)
        {
            int selected = UnityEngine.Random.Range(index, list.Count);
            (list[index], list[selected]) = (list[selected], list[index]);
        }
    }

    private sealed class DangerousInstance
    {
        internal DangerousInstance(
            GameObject source,
            DangerousValuableKind kind,
            LevitationActivationKind activation,
            Component? effect)
        {
            Source = source;
            Kind = kind;
            Activation = activation;
            Effect = effect;
        }

        internal GameObject Source { get; }
        internal DangerousValuableKind Kind { get; }
        internal LevitationActivationKind Activation { get; }
        internal Component? Effect { get; }
    }
}
