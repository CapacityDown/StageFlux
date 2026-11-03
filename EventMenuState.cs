using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

// Room properties are display data only. Guests never change the host's configuration.
internal sealed class EventMenuState : MonoBehaviourPunCallbacks
{
    private const string Key = "SF.Menu";
    private const string GuideKey = "SF.Guide";
    private ConfigFile _file = null!;
    private StagePhysicsConfig _config = null!;
    private StagePhysicsEventController _controller = null!;
    private readonly Dictionary<StageEffect, ConfigEntry<bool>> _entries = new();
    private object? _publishedRoom;
    private string _published = "";
    private float _nextRefresh;
    private bool _warned;
    private bool _guideDirty = true;
    private string _publishedGuide = "", _readGuide = "";
    private int _readActor;
    private Dictionary<long, EventGuideDetails> _localDetails = new(), _remoteDetails = new();
    internal CustomEventPresets Presets { get; private set; } = null!;
    internal int GuideRevision { get; private set; }

    internal static bool CanEdit => GameManager.instance != null && EventMenuPolicy.CanEdit(true,
        SemiFunc.IsMultiplayer(), PhotonNetwork.InRoom, PhotonNetwork.IsMasterClient);

    internal void Initialize(ConfigFile file, StagePhysicsConfig config, StagePhysicsEventController controller)
    {
        _file = file; _config = config; _controller = controller;
        Presets = new CustomEventPresets(Path.Combine(Path.GetDirectoryName(file.ConfigFilePath)!, "StageFlux.EventPresets.json"));
        if (!Presets.Available) StagePhysicsEventsPlugin.ModLogger.LogWarning("Custom event presets unavailable: " + Presets.Error);
        file.SettingChanged += OnSettingChanged;
        foreach (StageEffect effect in StageEffectSet.IndividualEffects)
        {
            string section = EventPresentation.Name(effect);
            if (!file.TryGetEntry(new ConfigDefinition(section, "Enabled"), out ConfigEntry<bool> entry))
                throw new InvalidOperationException("Missing event menu setting: " + section);
            _entries.Add(effect, entry);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.5f;
        try { Publish(); StagePhysicsEventsPlugin.Instance.BugReport?.RememberPlayers(); }
        catch (Exception exception)
        {
            if (!_warned) StagePhysicsEventsPlugin.ModLogger.LogWarning("Event menu sync unavailable: " + exception.Message);
            _warned = true;
        }
    }

    internal bool TryRead(out StageEffect enabled, out bool active)
    {
        enabled = StageEffect.None; active = false;
        if (CanEdit)
        {
            foreach (var pair in _entries) if (pair.Value.Value) enabled |= pair.Key;
            active = _config.Enabled.Value;
            return true;
        }
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return false;
        object? raw = PhotonNetwork.CurrentRoom.CustomProperties[Key];
        if (raw is not string payload) return false;
        if (!EventMenuPolicy.TryDecode(payload, PhotonNetwork.MasterClient?.ActorNumber ?? 0, out long mask, out active)) return false;
        enabled = (StageEffect)mask & StageEffectSet.All;
        return true;
    }

    private void Publish()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;
        if (!TryRead(out StageEffect effects, out bool active)) return;
        string value = EventMenuPolicy.Encode(PhotonNetwork.LocalPlayer.ActorNumber, (long)effects, active);
        EnsureLocalDetails();
        string guide = EventGuideSnapshot.Encode(PhotonNetwork.LocalPlayer.ActorNumber, _localDetails);
        var changes = new Hashtable();
        bool newRoom = !ReferenceEquals(_publishedRoom, PhotonNetwork.CurrentRoom);
        if (newRoom || _published != value) changes[Key] = value;
        if (newRoom || _publishedGuide != guide) changes[GuideKey] = guide;
        if (changes.Count > 0 && PhotonNetwork.CurrentRoom.SetCustomProperties(changes))
        { _publishedRoom = PhotonNetwork.CurrentRoom; _published = value; _publishedGuide = guide; }
    }

    private void OnSettingChanged(object sender, SettingChangedEventArgs args) { _guideDirty = true; GuideRevision++; }

    private void EnsureLocalDetails()
    {
        if (!_guideDirty) return;
        var result = new Dictionary<long, EventGuideDetails>();
        foreach (StageEffect effect in StageEffectSet.IndividualEffects)
        {
            string section = EventPresentation.Name(effect);
            var text = new StringBuilder();
            text.Append("Mode: ").Append(_config.Mode.Value).Append('\n')
                .Append("Stage activation chance: ").Append(_config.StageActivationChancePercent.Value).Append("%\n")
                .Append("Event duration: ").Append(_config.EffectDurationMinSeconds.Value).Append('-').Append(_config.EffectDurationMaxSeconds.Value).Append(" seconds\n")
                .Append("Interval: ").Append(_config.IntervalMinSeconds.Value).Append('-').Append(_config.IntervalMaxSeconds.Value).Append(" seconds\n")
                .Append("Simultaneous event limit: ").Append(_config.MaxSimultaneousEffects.Value);
            if (_config.Mode.Value is "PersistentForStage" or "AllMode")
                text.Append("\nPersistent mode keeps its events active for the stage instead of using the duration and interval above.");
            foreach (var setting in _file.Where(p => p.Key.Section == section).OrderBy(p => p.Key.Key, StringComparer.Ordinal))
            {
                string label = Regex.Replace(setting.Key.Key, "([a-z0-9])([A-Z])", "$1 $2");
                object value = setting.Value.BoxedValue;
                text.Append('\n').Append(label).Append(": ").Append(value is bool on ? on ? "ON" : "OFF" : Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            string eligibility = !_config.Enabled.Value ? "Not selected: Stage Flux events are disabled." :
                !_entries[effect].Value ? "Not selected: this event is OFF." :
                !_config.IsEffectSelectable(effect) ? "Not selected: chance is zero or required event types are disabled." :
                _config.StageActivationChancePercent.Value <= 0 ? "Not selected: stage activation chance is zero." :
                "Enabled for draws, subject to stage conditions, available targets, combinations, and the simultaneous-event limit.";
            if (effect == StageEffect.Rockfall) eligibility += " Requires rockslide terrain.";
            if (effect == StageEffect.PlayerSwap) eligibility += " Requires two eligible living players.";
            if (effect == StageEffect.EnemyWave) eligibility += " Requires an available enemy on the stage.";
            result.Add((long)effect, new EventGuideDetails(text.ToString(), eligibility));
        }
        _localDetails = result; _guideDirty = false;
    }

    internal bool TryReadDetails(StageEffect effect, out EventGuideDetails details)
    {
        details = default;
        if (CanEdit) { EnsureLocalDetails(); return _localDetails.TryGetValue((long)effect, out details); }
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return false;
        int actor = PhotonNetwork.MasterClient?.ActorNumber ?? 0;
        string raw = PhotonNetwork.CurrentRoom.CustomProperties[GuideKey] as string ?? "";
        if (_readGuide != raw || _readActor != actor)
        {
            _readGuide = raw; _readActor = actor; GuideRevision++;
            if (!EventGuideSnapshot.TryDecode(raw, actor, (long)StageEffectSet.All, out _remoteDetails)) _remoteDetails.Clear();
        }
        return _remoteDetails.TryGetValue((long)effect, out details);
    }

    internal bool SaveCustomPreset(int slot, bool overwrite)
    {
        if (!CanEdit || !TryRead(out StageEffect enabled, out _)) return false;
        return Presets.TrySave(slot, StageEffectSet.IndividualEffects.Where(e => StageEffectSet.Contains(enabled, e)).Select(e => e.ToString()), overwrite);
    }

    internal bool ApplyCustomPreset(int slot)
    {
        if (!CanEdit || Presets.Get(slot) is not string[] names) return false;
        var enabled = new HashSet<string>(names, StringComparer.Ordinal);
        Save(_entries.ToDictionary(p => p.Value, p => enabled.Contains(p.Key.ToString())));
        return true;
    }

    internal int CountPreset(string[] names) => StageEffectSet.IndividualEffects.Count(e => Array.IndexOf(names, e.ToString()) >= 0);

    internal bool SetEnabled(StageEffect effect, bool value)
    {
        if (!CanEdit || !_entries.ContainsKey(effect)) return false;
        Save(new Dictionary<ConfigEntry<bool>, bool> { [_entries[effect]] = value });
        return true;
    }

    internal void ApplyPreset(string preset)
    {
        if (!CanEdit || (preset != "Defaults" && preset != "Low Risk" && preset != "All Off")) return;
        var values = new Dictionary<ConfigEntry<bool>, bool>();
        foreach (var pair in _entries)
            values[pair.Value] = preset == "Defaults" ? (bool)pair.Value.DefaultValue :
                preset == "Low Risk" && EventPresentation.Danger(pair.Key) == EventDanger.Low;
        Save(values);
    }

    private void Save(Dictionary<ConfigEntry<bool>, bool> values)
    {
        if (!CanEdit) return;
        bool automatic = _file.SaveOnConfigSet;
        var previous = new Dictionary<ConfigEntry<bool>, bool>();
        _file.SaveOnConfigSet = false;
        try
        {
            foreach (var pair in values) { previous[pair.Key] = pair.Key.Value; pair.Key.Value = pair.Value; }
            _file.Save();
        }
        catch { foreach (var pair in previous) pair.Key.Value = pair.Value; throw; }
        finally { _file.SaveOnConfigSet = automatic; }
        Publish();
    }

    internal string Status
    {
        get
        {
            if (CanEdit) return PhotonNetwork.InRoom ? "Host - sharing event settings." : "Single player - local event settings.";
            return TryRead(out _, out _) ? "Connected - host event settings received." :
                "Host event settings unavailable. The host may be using an older version or may not have Stage Flux.";
        }
    }

    internal void RefreshDisplay()
    {
        _published = ""; _publishedGuide = ""; _readGuide = ""; _readActor = 0; _remoteDetails.Clear(); _guideDirty = true; GuideRevision++;
        Publish();
        _controller.RefreshMenuDisplay();
    }

    private void ResetDisplayState()
    {
        _publishedRoom = null; _published = _publishedGuide = _readGuide = ""; _readActor = 0;
        _remoteDetails.Clear(); _guideDirty = true; GuideRevision++; _warned = false; _nextRefresh = 0;
    }
    public override void OnJoinedRoom() => ResetDisplayState();
    public override void OnLeftRoom() => ResetDisplayState();
    public override void OnMasterClientSwitched(Player newMasterClient) => ResetDisplayState();
    private void OnDestroy() { if (_file != null) _file.SettingChanged -= OnSettingChanged; }
}
