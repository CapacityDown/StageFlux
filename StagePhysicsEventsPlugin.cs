using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace REPOJP.StagePhysicsEvents;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.HardDependency)]
public sealed class StagePhysicsEventsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "REPOJP.StagePhysicsEvents";
    public const string PluginName = "Stage Flux";
    public const string PluginVersion = "4.3.0";

    private Harmony? _harmony;
    private GameObject? _controllerObject;

    internal static StagePhysicsEventsPlugin Instance { get; private set; } = null!;
    internal static ManualLogSource ModLogger { get; private set; } = null!;
    internal StagePhysicsConfig Settings { get; private set; } = null!;
    internal StagePhysicsEventController Controller { get; private set; } = null!;
    internal EventMenuState MenuState { get; private set; } = null!;
    internal EventBugReport BugReport { get; private set; } = null!;
    internal void SaveSettings() => Config.Save();

    private void Awake()
    {
        Instance = this;
        ModLogger = Logger;
        Settings = new StagePhysicsConfig(Config, Logger);

        gameObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(gameObject);

        _controllerObject = new GameObject("StagePhysicsEvents_Controller");
        _controllerObject.hideFlags = HideFlags.HideAndDontSave;
        _controllerObject.transform.SetParent(transform, false);
        _controllerObject.SetActive(false);
        Controller = _controllerObject.AddComponent<StagePhysicsEventController>();
        Controller.Initialize(Settings);
        gameObject.AddComponent<StagePhysicsEventHud>().Initialize(Settings, Controller);
        MenuState = gameObject.AddComponent<EventMenuState>();
        MenuState.Initialize(Config, Settings, Controller);
        BugReport = new EventBugReport();
        BepInEx.Logging.Logger.Listeners.Add(BugReport);
        gameObject.AddComponent<EventMenu>().Initialize(Settings, MenuState);

        _harmony = new Harmony(PluginGuid);
        try
        {
            _harmony.PatchAll(typeof(LifecyclePatches));
            _harmony.PatchAll(typeof(NotificationEnemyReactionPatches));
            _harmony.PatchAll(typeof(EnemySpeedPatches));
            _harmony.PatchAll(typeof(ExtendedEventPatches));
            _harmony.PatchAll(typeof(EventMenuScroll));
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }
        catch (Exception exception)
        {
            Logger.LogError($"Failed to apply Harmony patches: {exception}");
            _harmony.UnpatchSelf();
            Controller.enabled = false;
        }
    }

    private void OnDestroy()
    {
        EventHudEditor.Instance?.Close(false);
        if (BugReport != null) BepInEx.Logging.Logger.Listeners.Remove(BugReport);
        SceneManager.activeSceneChanged -= ActiveSceneChanged;
        Controller?.Shutdown();
        _harmony?.UnpatchSelf();
        PhysGrabObjectRegistry.Clear();
        if (_controllerObject != null)
        {
            Destroy(_controllerObject);
        }
    }

    private void ActiveSceneChanged(Scene previous, Scene current)
    {
        Controller?.StageEnding();
    }
}
