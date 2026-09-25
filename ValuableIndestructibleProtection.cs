using System.Collections.Generic;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class ValuableIndestructibleProtection
{
    private readonly List<PhysGrabObject> _valuables = new();
    private float _startsAt;
    private float _endsAt;

    internal void Schedule(IReadOnlyList<PhysGrabObject> valuables, float startsAt, float endsAt)
    {
        _valuables.Clear();
        HashSet<int> selected = new();
        foreach (PhysGrabObject valuable in valuables)
        {
            if (valuable != null && valuable.GetComponent<ValuableObject>() != null &&
                selected.Add(valuable.GetInstanceID()))
            {
                _valuables.Add(valuable);
            }
        }
        _startsAt = startsAt;
        _endsAt = Mathf.Max(startsAt, endsAt);
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Valuable Indestructible protection scheduled: targets={_valuables.Count}, " +
            $"startsIn={Mathf.Max(0f, _startsAt - Time.time):0.##}s, duration={Mathf.Max(0f, _endsAt - _startsAt):0.##}s.");
    }

    internal void Tick()
    {
        if (_valuables.Count == 0 || Time.time < _startsAt)
        {
            return;
        }
        if (Time.time > _endsAt)
        {
            Stop();
            return;
        }

        for (int index = _valuables.Count - 1; index >= 0; index--)
        {
            PhysGrabObject valuable = _valuables[index];
            if (valuable == null || !valuable.gameObject.activeInHierarchy)
            {
                _valuables.RemoveAt(index);
                continue;
            }
            valuable.OverrideIndestructible();
        }
    }

    internal void AddTarget(PhysGrabObject valuable)
    {
        if (valuable == null || valuable.GetComponent<ValuableObject>() == null)
        {
            return;
        }
        foreach (PhysGrabObject existing in _valuables)
        {
            if (existing == valuable)
            {
                return;
            }
        }
        _valuables.Add(valuable);
    }

    internal void Stop()
    {
        _valuables.Clear();
        _startsAt = 0f;
        _endsAt = 0f;
    }
}
