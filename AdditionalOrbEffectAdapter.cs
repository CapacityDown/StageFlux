using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class AdditionalOrbEffectAdapter
{
    private static readonly FieldInfo? ObjectAffectedField = AccessTools.Field(typeof(ItemOrb), "objectAffected");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaEffectResolver _resolver;
    private readonly StagePhysicsConfig _config;
    private readonly List<PhysGrabObject> _targets = new();
    private readonly List<PlayerAvatar> _healPlayers = new();
    private GameObject? _carrier;
    private ItemOrb? _itemOrb;
    private StageEffect _effect;
    private Coroutine? _batteryCoroutine;
    private Coroutine? _healCoroutine;
    private int _carrierGeneration;

    internal AdditionalOrbEffectAdapter(
        MonoBehaviour coroutineOwner,
        VanillaEffectResolver resolver,
        StagePhysicsConfig config)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
        _config = config;
    }

    internal static bool Supports(StageEffect effect) =>
        effect == StageEffect.Battery ||
        effect == StageEffect.Heal ||
        effect == StageEffect.Indestructible;

    internal bool EnsureAvailable(StageEffect effect) =>
        Supports(effect) &&
        (effect == StageEffect.Heal || ObjectAffectedField != null) &&
        _resolver.TryResolveOrb(effect, out _, out _);

    internal void ApplyOnce(
        StageEffect effect,
        IReadOnlyList<PhysGrabObject> objects,
        IReadOnlyList<PlayerAvatar> players)
    {
        if (!EnsureAvailable(effect))
        {
            return;
        }

        if (_effect != effect)
        {
            Stop();
            _effect = effect;
        }

        if (effect == StageEffect.Heal)
        {
            _healPlayers.Clear();
            foreach (PlayerAvatar player in players)
            {
                if (player != null && player.gameObject.activeInHierarchy)
                {
                    _healPlayers.Add(player);
                }
            }
            _healCoroutine ??= _coroutineOwner.StartCoroutine(HealLoop());
            return;
        }

        _targets.Clear();
        foreach (PhysGrabObject target in objects)
        {
            if (target == null ||
                StagePhysicsEffectCarrierUtility.IsInternalCarrier(target))
            {
                continue;
            }
            if (effect == StageEffect.Battery && target.GetComponent<ItemBattery>() == null)
            {
                continue;
            }
            _targets.Add(target);
        }
        if (effect == StageEffect.Battery)
        {
            _batteryCoroutine ??= _coroutineOwner.StartCoroutine(BatteryLoop());
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Battery charge loop activated with {_targets.Count} stage target(s).");
            return;
        }
        if (_carrier == null)
        {
            CreateCarrier(effect, HiddenPosition());
        }
        else
        {
            SetCarrierPosition(HiddenPosition());
            SetTargets();
        }
    }

    internal void Tick()
    {
        if (_carrier == null || _itemOrb == null)
        {
            return;
        }
        _itemOrb.itemActive = true;
        ItemBattery? battery = _carrier.GetComponent<ItemBattery>();
        if (battery != null)
        {
            battery.autoDrain = false;
            battery.batteryLife = 100f;
        }
    }

    internal void AddPlayer(PlayerAvatar player)
    {
        if (_effect != StageEffect.Heal || player == null || !player.gameObject.activeInHierarchy ||
            _healPlayers.Contains(player))
        {
            return;
        }
        _healPlayers.Add(player);
        _healCoroutine ??= _coroutineOwner.StartCoroutine(HealLoop());
    }

    internal void AddTarget(PhysGrabObject target)
    {
        if ((_effect != StageEffect.Battery && _effect != StageEffect.Indestructible) ||
            target == null || StagePhysicsEffectCarrierUtility.IsInternalCarrier(target) ||
            (_effect == StageEffect.Battery && target.GetComponent<ItemBattery>() == null))
        {
            return;
        }
        foreach (PhysGrabObject existing in _targets)
        {
            if (existing == target)
            {
                return;
            }
        }
        _targets.Add(target);
        SetTargets();
    }

    internal void Stop()
    {
        _carrierGeneration++;
        if (_batteryCoroutine != null)
        {
            _coroutineOwner.StopCoroutine(_batteryCoroutine);
            _batteryCoroutine = null;
        }
        if (_healCoroutine != null)
        {
            _coroutineOwner.StopCoroutine(_healCoroutine);
            _healCoroutine = null;
        }
        _healPlayers.Clear();
        _targets.Clear();

        if (_itemOrb != null)
        {
            _itemOrb.itemActive = false;
            try
            {
                ObjectAffectedField?.SetValue(_itemOrb, new List<PhysGrabObject>());
            }
            catch
            {
                // Unity may already be unloading the scene.
            }
        }
        if (_carrier != null)
        {
            UnityEngine.Object.Destroy(_carrier);
        }
        _carrier = null;
        _itemOrb = null;
        _effect = StageEffect.None;
    }

    private void CreateCarrier(StageEffect effect, Vector3 position)
    {
        if (!_resolver.TryResolveOrb(effect, out GameObject? prefab, out _) || prefab == null)
        {
            return;
        }

        try
        {
            _carrier = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            _carrier.name = $"StagePhysicsEvents_Virtual{effect}Orb";
            _carrier.hideFlags = HideFlags.HideAndDontSave;
            _carrier.AddComponent<StagePhysicsEffectCarrierMarker>();

            foreach (Renderer renderer in _carrier.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
            foreach (Collider collider in _carrier.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            foreach (AudioSource audio in _carrier.GetComponentsInChildren<AudioSource>(true))
            {
                audio.enabled = false;
            }
            DisableCarrierPhysics(_carrier);

            int generation = ++_carrierGeneration;
            _coroutineOwner.StartCoroutine(InitializeCarrierAfterStart(effect, generation));
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not create virtual {effect} Orb: {exception}");
            Stop();
        }
    }

    private IEnumerator InitializeCarrierAfterStart(StageEffect effect, int generation)
    {
        yield return null;
        if (_carrier == null || _effect != effect || generation != _carrierGeneration)
        {
            yield break;
        }

        try
        {
            _itemOrb = _carrier.GetComponent<ItemOrb>();
            Behaviour? effectBehaviour = ResolveEffectBehaviour(_carrier, effect);
            if (_itemOrb == null || effectBehaviour == null)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning($"Resolved {effect} Orb prefab is missing its required component.");
                Stop();
                yield break;
            }

            foreach (Behaviour behaviour in _carrier.GetComponentsInChildren<Behaviour>(true))
            {
                behaviour.enabled = behaviour == effectBehaviour;
            }
            DisableCarrierPhysics(_carrier);
            ItemBattery? battery = _carrier.GetComponent<ItemBattery>();
            if (battery != null)
            {
                battery.autoDrain = false;
                battery.batteryLife = 100f;
            }
            _itemOrb.itemActive = true;
            SetTargets();
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Virtual {effect} Orb activated with {_targets.Count} stage target(s).");
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not initialize virtual {effect} Orb: {exception}");
            Stop();
        }
    }

    private IEnumerator HealLoop()
    {
        while (_effect == StageEffect.Heal)
        {
            yield return new WaitForSeconds(_config.HealIntervalSeconds.Value);
            if (_effect != StageEffect.Heal)
            {
                break;
            }
            foreach (PlayerAvatar player in _healPlayers)
            {
                if (player == null || !player.gameObject.activeInHierarchy || player.playerHealth == null)
                {
                    continue;
                }
                try
                {
                    // Orb Heal's vanilla cadence and amount, routed through the standard
                    // owner RPC so unmodded multiplayer participants are healed as well.
                    player.playerHealth.HealOther(_config.HealAmount.Value, true);
                }
                catch (Exception exception)
                {
                    StagePhysicsEventsPlugin.ModLogger.LogDebug(
                        $"Heal Orb target skipped for player view {player.photonView?.ViewID ?? 0}: {exception.Message}");
                }
            }
        }
        _healCoroutine = null;
    }

    private IEnumerator BatteryLoop()
    {
        while (_effect == StageEffect.Battery)
        {
            yield return new WaitForSeconds(_config.BatteryChargeIntervalSeconds.Value);
            if (_effect != StageEffect.Battery)
            {
                break;
            }
            float chargeRate = _config.BatteryChargeAmount.Value * 10f;
            foreach (PhysGrabObject target in _targets)
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    continue;
                }
                ItemBattery? battery = target.GetComponent<ItemBattery>();
                if (battery == null || battery.batteryLife >= 100f)
                {
                    continue;
                }
                try
                {
                    // ChargeBattery applies the supplied rate for 0.1 seconds. Multiplying
                    // the configured per-tick amount by ten restores that amount per tick
                    // while preserving the vanilla charge visuals and synchronization.
                    battery.ChargeBattery(_coroutineOwner.gameObject, chargeRate);
                }
                catch (Exception exception)
                {
                    StagePhysicsEventsPlugin.ModLogger.LogDebug(
                        $"Battery target skipped for {target.name}: {exception.Message}");
                }
            }
        }
        _batteryCoroutine = null;
    }

    private void SetTargets()
    {
        if (_itemOrb == null)
        {
            return;
        }
        ObjectAffectedField?.SetValue(_itemOrb, new List<PhysGrabObject>(_targets));
        _itemOrb.itemActive = true;
    }

    private void SetCarrierPosition(Vector3 position)
    {
        if (_carrier == null)
        {
            return;
        }
        _carrier.transform.position = position;
        PhysGrabObject? physObject = _carrier.GetComponent<PhysGrabObject>();
        if (physObject != null && physObject.rb != null)
        {
            physObject.rb.position = position;
            physObject.rb.velocity = Vector3.zero;
            physObject.rb.angularVelocity = Vector3.zero;
        }
    }

    private static Vector3 HiddenPosition() => new(0f, -1000f, 0f);

    private static Behaviour? ResolveEffectBehaviour(GameObject carrier, StageEffect effect) => effect switch
    {
        StageEffect.Battery => carrier.GetComponent<ItemOrbBattery>(),
        StageEffect.Indestructible => carrier.GetComponent<ItemOrbIndestructible>(),
        _ => null
    };

    private static void DisableCarrierPhysics(GameObject carrier)
    {
        foreach (Rigidbody rigidbody in carrier.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }
    }
}
