using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace REPOJP.StagePhysicsEvents;

internal static class EliteEnemyVariantsCompatibility
{
    internal const string PluginGuid = "REPOJP.EliteEnemyVariants";
    internal const int MinimumSupportedApiVersion = 1;
    internal const int MaximumSupportedApiVersion = 1;
    private const string ApiTypeName =
        "REPOJP.EliteEnemyVariants.EliteEnemyVariantsCompatibilityApi";

    private static Assembly? _resolvedAssembly;
    private static MethodInfo? _notifyExternalSpawn;
    private static bool _resolutionLogged;
    private static bool _invocationFailureLogged;

    internal static bool NotifyExternalSpawn(EnemyParent? enemyParent)
    {
        if (enemyParent == null || !SemiFunc.IsMasterClientOrSingleplayer())
        {
            return false;
        }

        MethodInfo? method = ResolveNotifyExternalSpawn();
        if (method == null)
        {
            return false;
        }

        try
        {
            return method.Invoke(null, new object?[] { enemyParent }) is true;
        }
        catch (Exception exception)
        {
            WarnInvocationOnce(
                $"Elite Enemy Variants spawn notification failed open; " +
                $"Enemy Wave will continue normally: {exception.GetBaseException().Message}");
            return false;
        }
    }

    private static MethodInfo? ResolveNotifyExternalSpawn()
    {
        try
        {
            if (!Chainloader.PluginInfos.TryGetValue(
                    PluginGuid,
                    out BepInEx.PluginInfo pluginInfo) ||
                pluginInfo.Instance == null)
            {
                return null;
            }

            Assembly assembly = pluginInfo.Instance.GetType().Assembly;
            if (ReferenceEquals(assembly, _resolvedAssembly))
            {
                return _notifyExternalSpawn;
            }

            ResetForAssembly(assembly);
            Type? apiType = assembly.GetType(ApiTypeName, false);
            if (!CompatibilityApiContract.TryReadApiVersion(apiType, out int apiVersion))
            {
                WarnResolutionOnce(
                    "Elite Enemy Variants is loaded without a readable public ApiVersion; " +
                    "external-spawn integration is disabled and Enemy Wave will continue normally.");
                return null;
            }
            if (apiVersion < MinimumSupportedApiVersion ||
                apiVersion > MaximumSupportedApiVersion)
            {
                WarnResolutionOnce(
                    $"Elite Enemy Variants compatibility ApiVersion {apiVersion} is outside the supported " +
                    $"range {MinimumSupportedApiVersion}-{MaximumSupportedApiVersion}; external-spawn " +
                    "integration is disabled and Enemy Wave will continue normally.");
                return null;
            }

            _notifyExternalSpawn = CompatibilityApiContract.FindMethod(
                apiType!,
                "NotifyExternalSpawn",
                typeof(bool),
                typeof(EnemyParent));
            if (_notifyExternalSpawn == null)
            {
                WarnResolutionOnce(
                    $"Elite Enemy Variants ApiVersion {apiVersion} does not expose the expected " +
                    "bool NotifyExternalSpawn(EnemyParent) contract; external-spawn integration is " +
                    "disabled and Enemy Wave will continue normally.");
                return null;
            }

            _resolutionLogged = true;
            StagePhysicsEventsPlugin.ModLogger.LogInfo(
                $"Elite Enemy Variants compatibility API v{apiVersion} detected lazily.");
            return _notifyExternalSpawn;
        }
        catch (Exception exception)
        {
            WarnResolutionOnce(
                $"Elite Enemy Variants compatibility resolution failed open; Enemy Wave will continue " +
                $"normally: {exception.GetBaseException().Message}");
            return null;
        }
    }

    private static void ResetForAssembly(Assembly assembly)
    {
        _resolvedAssembly = assembly;
        _notifyExternalSpawn = null;
        _resolutionLogged = false;
        _invocationFailureLogged = false;
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

    private static void WarnInvocationOnce(string message)
    {
        if (_invocationFailureLogged)
        {
            return;
        }
        _invocationFailureLogged = true;
        StagePhysicsEventsPlugin.ModLogger.LogWarning(message);
    }
}
