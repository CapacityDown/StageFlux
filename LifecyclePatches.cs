using System.Collections.Generic;
using HarmonyLib;

namespace REPOJP.StagePhysicsEvents;

internal static class LifecyclePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static void LevelGeneratorGenerateDonePostfix(LevelGenerator __instance)
    {
        StagePhysicsEventsPlugin.Instance?.Controller?.StageReady(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ChangeLevel))]
    private static bool RunManagerChangeLevelPrefix(RunManager __instance, bool _levelFailed)
    {
        StagePhysicsEventController? controller = StagePhysicsEventsPlugin.Instance?.Controller;
        if (_levelFailed && controller != null && controller.TryPreventSecondChanceRetake())
        {
            __instance.AllPlayersDeadSet(_set: false);
            StagePhysicsEventsPlugin.ModLogger.LogInfo(
                "Second Chance prevented a failed-stage retake and revived the player at the death location.");
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ChangeLevel))]
    private static void RunManagerChangeLevelPostfix(bool __runOriginal)
    {
        // Another mod can legitimately prevent a failed-stage retake after our
        // prefix has run. Only tear down the current stage when ChangeLevel was
        // actually allowed to execute.
        if (__runOriginal)
        {
            StagePhysicsEventsPlugin.Instance?.Controller?.StageEnding();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RoundDirector), nameof(RoundDirector.ExtractionCompleted))]
    private static void RoundDirectorExtractionCompletedPostfix(RoundDirector __instance)
    {
        StagePhysicsEventsPlugin.Instance?.Controller?.ExtractionCompleted(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ExtractionPoint), "SpawnTaxReturn")]
    private static void ExtractionPointSpawnTaxReturnPrefix(ref HashSet<int>? __state)
    {
        __state = StagePhysicsEventsPlugin.Instance?.Controller?
            .CaptureTaxReturnValuablesBeforeSpawn();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ExtractionPoint), "SpawnTaxReturn")]
    private static void ExtractionPointSpawnTaxReturnPostfix(HashSet<int>? __state)
    {
        StagePhysicsEventsPlugin.Instance?.Controller?
            .ExcludeTaxReturnValuablesAfterSpawn(__state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PhysGrabObject), "Start")]
    private static void PhysGrabObjectStartPostfix(PhysGrabObject __instance)
    {
        PhysGrabObjectRegistry.Register(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "FixedUpdate")]
    private static void PhysGrabObjectImpactDetectorFixedUpdatePrefix(
        PhysGrabObjectImpactDetector __instance)
    {
        StagePhysicsEventsPlugin.Instance?.Controller?.RefreshFragilityForPhysics(__instance);
    }
}
