using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.StagePhysicsEvents;

internal sealed class RandomStagePointSelector
{
    private static readonly FieldInfo? InStartRoomField = AccessTools.Field(typeof(LevelPoint), "inStartRoom");

    internal bool TryGetPosition(float minimumPlayerDistance, HashSet<int> excludedPointIds, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        List<LevelPoint> candidates;
        try
        {
            candidates = SemiFunc.LevelPointsGetAll();
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug($"Could not collect stage points: {exception.Message}");
            return false;
        }
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        if (TrySelect(candidates, minimumPlayerDistance, excludedPointIds, allowReusedPoint: false,
                out position, out rotation))
        {
            return true;
        }

        // Small stages can have every point inside the requested player-distance radius.
        // Prefer spawning the event over silently creating zero instances.
        if (minimumPlayerDistance > 0f &&
            TrySelect(candidates, 0f, excludedPointIds, allowReusedPoint: false,
                out position, out rotation))
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Relaxed the requested {minimumPlayerDistance:0.#}m player distance to place a stage event.");
            return true;
        }

        // Waves may request more instances than the stage exposes unique LevelPoints.
        // Reuse a valid point with a small floor-projected offset instead of truncating the wave.
        if (TrySelect(candidates, minimumPlayerDistance, excludedPointIds, allowReusedPoint: true,
                out position, out rotation))
        {
            return true;
        }
        return minimumPlayerDistance > 0f &&
               TrySelect(candidates, 0f, excludedPointIds, allowReusedPoint: true,
                   out position, out rotation);
    }

    internal bool TryGetNavigablePosition(
        float minimumPlayerDistance,
        HashSet<int> excludedPointIds,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        List<LevelPoint> candidates;
        try
        {
            candidates = SemiFunc.LevelPointsGetAll();
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Could not collect navigable stage points: {exception.Message}");
            return false;
        }
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        if (TrySelectNavigable(
                candidates,
                minimumPlayerDistance,
                excludedPointIds,
                allowReusedPoint: false,
                out position,
                out rotation))
        {
            return true;
        }
        if (minimumPlayerDistance > 0f &&
            TrySelectNavigable(
                candidates,
                0f,
                excludedPointIds,
                allowReusedPoint: false,
                out position,
                out rotation))
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Relaxed the requested {minimumPlayerDistance:0.#}m player distance to place a mine on the stage NavMesh.");
            return true;
        }
        if (TrySelectNavigable(
                candidates,
                minimumPlayerDistance,
                excludedPointIds,
                allowReusedPoint: true,
                out position,
                out rotation))
        {
            return true;
        }
        return minimumPlayerDistance > 0f &&
               TrySelectNavigable(
                   candidates,
                   0f,
                   excludedPointIds,
                   allowReusedPoint: true,
                   out position,
                   out rotation);
    }

    private static bool TrySelectNavigable(
        List<LevelPoint> candidates,
        float minimumPlayerDistance,
        HashSet<int> excludedPointIds,
        bool allowReusedPoint,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        int start = UnityEngine.Random.Range(0, candidates.Count);
        for (int offset = 0; offset < candidates.Count; offset++)
        {
            LevelPoint point = candidates[(start + offset) % candidates.Count];
            if (!IsAllowed(point, minimumPlayerDistance, excludedPointIds, allowReusedPoint))
            {
                continue;
            }

            Vector3 horizontalOffset = Vector3.zero;
            if (allowReusedPoint)
            {
                Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized *
                                 UnityEngine.Random.Range(0.75f, 2.25f);
                horizontalOffset = new Vector3(circle.x, 0f, circle.y);
            }
            Vector3 requested = point.transform.position + horizontalOffset;
            if (!NavMesh.SamplePosition(requested, out NavMeshHit navHit, 2.5f, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 rayOrigin = navHit.position + Vector3.up * 1.5f;
            if (!Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit floorHit,
                    4f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) ||
                floorHit.normal.y < 0.65f)
            {
                continue;
            }
            position = floorHit.point + floorHit.normal * 0.12f;
            rotation = Quaternion.FromToRotation(Vector3.up, floorHit.normal);
            excludedPointIds.Add(point.GetInstanceID());
            return true;
        }
        return false;
    }

    private static bool TrySelect(
        List<LevelPoint> candidates,
        float minimumPlayerDistance,
        HashSet<int> excludedPointIds,
        bool allowReusedPoint,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        int start = UnityEngine.Random.Range(0, candidates.Count);
        for (int offset = 0; offset < candidates.Count; offset++)
        {
            LevelPoint point = candidates[(start + offset) % candidates.Count];
            if (!IsAllowed(point, minimumPlayerDistance, excludedPointIds, allowReusedPoint))
            {
                continue;
            }

            Vector3 horizontalOffset = Vector3.zero;
            if (allowReusedPoint)
            {
                Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized *
                                 UnityEngine.Random.Range(0.75f, 2.25f);
                horizontalOffset = new Vector3(circle.x, 0f, circle.y);
            }
            Vector3 origin = point.transform.position + horizontalOffset + Vector3.up * 3f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < 0.55f)
                {
                    continue;
                }
                position = hit.point + hit.normal * 0.08f;
                rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            else
            {
                position = point.transform.position + horizontalOffset + Vector3.up * 0.08f;
                rotation = Quaternion.identity;
            }
            excludedPointIds.Add(point.GetInstanceID());
            return true;
        }
        return false;
    }

    private static bool IsAllowed(
        LevelPoint? point,
        float minimumPlayerDistance,
        HashSet<int> excludedPointIds,
        bool allowReusedPoint)
    {
        if (point == null || !point.gameObject.activeInHierarchy ||
            (!allowReusedPoint && excludedPointIds.Contains(point.GetInstanceID())) ||
            point.Truck || (point.Room != null && (point.Room.Truck || point.Room.Extraction)))
        {
            return false;
        }
        try
        {
            if (InStartRoomField?.GetValue(point) is bool inStartRoom && inStartRoom)
            {
                return false;
            }
        }
        catch
        {
            // The publicized reference may expose an internal field that is inaccessible at runtime.
        }

        if (minimumPlayerDistance <= 0f || GameDirector.instance == null)
        {
            return true;
        }
        float minimumSqr = minimumPlayerDistance * minimumPlayerDistance;
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                continue;
            }
            if ((player.transform.position - point.transform.position).sqrMagnitude < minimumSqr)
            {
                return false;
            }
        }
        return true;
    }
}
