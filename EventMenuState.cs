using System;
using System.Collections.Generic;
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
    private ConfigFile _file = null!;
    private StagePhysicsConfig _config = null!;
    private StagePhysicsEventController _controller = null!;
    private readonly Dictionary<StageEffect, ConfigEntry<bool>> _entries = new();
    private object? _publishedRoom;
    private string _published = "";
    private float _nextRefresh;
    private bool _warned;

    internal static bool CanEdit => GameManager.instance != null && EventMenuPolicy.CanEdit(true,
        SemiFunc.IsMultiplayer(), PhotonNetwork.InRoom, PhotonNetwork.IsMasterClient);

    internal void Initialize(ConfigFile file, StagePhysicsConfig config, StagePhysicsEventController controller)
    {
        _file = file; _config = config; _controller = controller;
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
        if (ReferenceEquals(_publishedRoom, PhotonNetwork.CurrentRoom) && _published == value) return;
        if (PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [Key] = value }))
        { _publishedRoom = PhotonNetwork.CurrentRoom; _published = value; }
    }

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
        _published = "";
        Publish();
        _controller.RefreshMenuDisplay();
    }

    public override void OnJoinedRoom() { _publishedRoom = null; _published = ""; _warned = false; }
    public override void OnLeftRoom() { _publishedRoom = null; _published = ""; _warned = false; }
    public override void OnMasterClientSwitched(Player newMasterClient) { _published = ""; _nextRefresh = 0; }
}
