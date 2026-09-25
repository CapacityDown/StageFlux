using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal static class RoleShuffleCompatibility
{
    private const string PluginGuid = "REPOJP.RoleShuffle";
    private const string ApiTypeName =
        "REPOJP.StageRoles.RoleShuffleCompatibilityApi";
    internal const int MinimumSupportedApiVersion = 2;
    internal const int MaximumSupportedApiVersion = 2;

    private static Assembly? _resolvedAssembly;
    private static Type? _resolvedApiType;
    private static MethodInfo? _canUseAsUnseenEnemyTarget;
    private static MethodInfo? _canActivateValuableEffect;
    private static MethodInfo? _registerNonPlayerEnemyDamage;
    private static MethodInfo? _notifyExternalRevival;
    private static MethodInfo? _hasImmediateCorrectiveRevival;
    private static MethodInfo? _isRoleAssignmentReady;
    private static MethodInfo? _getAssignedRoleName;
    private static MethodInfo? _getActiveRoleCount;
    private static MethodInfo? _isTricksterDecoy;
    private static bool _resolutionLogged;
    private static bool _invocationFailureLogged;

    internal static bool CanUseAsUnseenEnemyTarget(PlayerAvatar? player)
    {
        return EnsureResolved()
            ? InvokeBoolean(_canUseAsUnseenEnemyTarget, true, player)
            : true;
    }

    internal static bool CanActivateValuableEffect(Component? effect)
    {
        return EnsureResolved()
            ? InvokeBoolean(_canActivateValuableEffect, true, effect)
            : true;
    }

    internal static void RegisterNonPlayerEnemyDamage(EnemyHealth? enemyHealth)
    {
        if (enemyHealth != null)
        {
            if (EnsureResolved())
            {
                InvokeVoid(_registerNonPlayerEnemyDamage, enemyHealth);
            }
        }
    }

    internal static void NotifyExternalRevival(PlayerAvatar? player)
    {
        if (player != null)
        {
            if (EnsureResolved())
            {
                InvokeVoid(_notifyExternalRevival, player);
            }
        }
    }

    internal static bool HasImmediateCorrectiveRevival(PlayerAvatar? player)
    {
        return player != null && EnsureResolved() &&
            InvokeBoolean(_hasImmediateCorrectiveRevival, false, player);
    }

    internal static bool IsRoleAssignmentReady()
    {
        return EnsureResolved() && InvokeBoolean(_isRoleAssignmentReady, false);
    }

    internal static string GetAssignedRoleName(PlayerAvatar? player)
    {
        if (player == null)
        {
            return string.Empty;
        }
        if (!EnsureResolved())
        {
            return string.Empty;
        }
        object? result = Invoke(_getAssignedRoleName, player);
        return result as string ?? string.Empty;
    }

    internal static int GetActiveRoleCount(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return 0;
        }
        if (!EnsureResolved())
        {
            return 0;
        }
        object? result = Invoke(_getActiveRoleCount, roleName);
        return result is int count && count >= 0 ? count : 0;
    }

    internal static bool IsTricksterDecoy(Component? component)
    {
        return component != null && EnsureResolved() &&
            InvokeBoolean(_isTricksterDecoy, false, component);
    }

    private static bool EnsureResolved()
    {
        try
        {
            if (!Chainloader.PluginInfos.TryGetValue(
                    PluginGuid,
                    out BepInEx.PluginInfo pluginInfo) ||
                pluginInfo.Instance == null)
            {
                return false;
            }

            Assembly assembly = pluginInfo.Instance.GetType().Assembly;
            if (ReferenceEquals(assembly, _resolvedAssembly))
            {
                return _resolvedApiType != null;
            }

            ResetForAssembly(assembly);
            Type? apiType = assembly.GetType(ApiTypeName, false);
            if (!CompatibilityApiContract.TryReadApiVersion(apiType, out int apiVersion))
            {
                WarnResolutionOnce(
                    "RoleShuffle is loaded without a readable public ApiVersion; optional integration " +
                    "is disabled and Stage Flux fallback behavior remains active.");
                return false;
            }
            if (apiVersion < MinimumSupportedApiVersion ||
                apiVersion > MaximumSupportedApiVersion)
            {
                WarnResolutionOnce(
                    $"RoleShuffle compatibility ApiVersion {apiVersion} is outside the supported range " +
                    $"{MinimumSupportedApiVersion}-{MaximumSupportedApiVersion}; optional integration " +
                    "is disabled and Stage Flux fallback behavior remains active.");
                return false;
            }

            _resolvedApiType = apiType;
            _canUseAsUnseenEnemyTarget = Find(apiType!, "CanUseAsUnseenEnemyTarget", typeof(bool), typeof(PlayerAvatar));
            _canActivateValuableEffect = Find(apiType!, "CanActivateValuableEffect", typeof(bool), typeof(Component));
            _registerNonPlayerEnemyDamage = Find(apiType!, "RegisterNonPlayerEnemyDamage", typeof(void), typeof(EnemyHealth));
            _notifyExternalRevival = Find(apiType!, "NotifyExternalRevival", typeof(void), typeof(PlayerAvatar));
            _hasImmediateCorrectiveRevival = Find(apiType!, "HasImmediateCorrectiveRevival", typeof(bool), typeof(PlayerAvatar));
            _isRoleAssignmentReady = Find(apiType!, "IsRoleAssignmentReady", typeof(bool));
            _getAssignedRoleName = Find(apiType!, "GetAssignedRoleName", typeof(string), typeof(PlayerAvatar));
            _getActiveRoleCount = Find(apiType!, "GetActiveRoleCount", typeof(int), typeof(string));
            _isTricksterDecoy = Find(apiType!, "IsTricksterDecoy", typeof(bool), typeof(Component));

            if (!AllMethodsResolved())
            {
                WarnResolutionOnce(
                    $"RoleShuffle ApiVersion {apiVersion} exposes an incomplete or incompatible method " +
                    "contract; unavailable calls will use their safe Stage Flux fallbacks.");
            }
            else
            {
                _resolutionLogged = true;
                StagePhysicsEventsPlugin.ModLogger.LogInfo(
                    $"RoleShuffle compatibility API v{apiVersion} detected lazily.");
            }
            return true;
        }
        catch (Exception exception)
        {
            _resolvedApiType = null;
            ClearCachedMethods();
            WarnResolutionOnce(
                $"RoleShuffle compatibility resolution failed open; Stage Flux fallback behavior remains " +
                $"active: {exception.GetBaseException().Message}");
            return false;
        }
    }

    private static MethodInfo? Find(
        Type apiType,
        string name,
        Type returnType,
        params Type[] parameterTypes) =>
        CompatibilityApiContract.FindMethod(apiType, name, returnType, parameterTypes);

    private static bool AllMethodsResolved() =>
        _canUseAsUnseenEnemyTarget != null &&
        _canActivateValuableEffect != null &&
        _registerNonPlayerEnemyDamage != null &&
        _notifyExternalRevival != null &&
        _hasImmediateCorrectiveRevival != null &&
        _isRoleAssignmentReady != null &&
        _getAssignedRoleName != null &&
        _getActiveRoleCount != null &&
        _isTricksterDecoy != null;

    private static void ResetForAssembly(Assembly assembly)
    {
        _resolvedAssembly = assembly;
        _resolvedApiType = null;
        ClearCachedMethods();
        _resolutionLogged = false;
        _invocationFailureLogged = false;
    }

    private static void ClearCachedMethods()
    {
        _canUseAsUnseenEnemyTarget = null;
        _canActivateValuableEffect = null;
        _registerNonPlayerEnemyDamage = null;
        _notifyExternalRevival = null;
        _hasImmediateCorrectiveRevival = null;
        _isRoleAssignmentReady = null;
        _getAssignedRoleName = null;
        _getActiveRoleCount = null;
        _isTricksterDecoy = null;
    }

    private static bool InvokeBoolean(
        MethodInfo? method,
        bool fallback,
        params object?[] arguments)
    {
        object? result = Invoke(method, arguments);
        return result is bool value ? value : fallback;
    }

    private static void InvokeVoid(MethodInfo? method, params object?[] arguments)
    {
        _ = Invoke(method, arguments);
    }

    private static object? Invoke(MethodInfo? method, params object?[] arguments)
    {
        if (method == null)
        {
            return null;
        }
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (Exception exception)
        {
            if (!_invocationFailureLogged)
            {
                _invocationFailureLogged = true;
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"RoleShuffle compatibility call failed open; Stage Flux will continue normally: " +
                    exception.GetBaseException().Message);
            }
            return null;
        }
    }

    private static void WarnResolutionOnce(string message)
    {
        if (_resolutionLogged)
        {
            return;
        }
        _resolutionLogged = true;
        StagePhysicsEventsPlugin.ModLogger.LogWarning(message);
    }
}
