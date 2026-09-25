using System;
using System.Collections.Generic;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REPOJP.StagePhysicsEvents;

internal sealed class EventMenu : MonoBehaviour
{
    private static EventMenu? _instance;
    internal static bool OwnsScrollBox(MenuScrollBox box) => _instance != null && _instance._page != null &&
        EventHudEditor.Instance == null && _instance._page.menuScrollBox == box;
    private StagePhysicsConfig _config = null!;
    private EventMenuState _settings = null!;
    private REPOPopupPage? _page;
    private readonly List<Row> _rows = new();
    private readonly EventIconCatalog _icons = new();
    private readonly Dictionary<View, REPOButton> _navigation = new();
    private View _view;
    private string _signature = "", _message = "";
    private float _nextRefresh;
    internal bool IsOpen => _page != null;

    internal void Initialize(StagePhysicsConfig config, EventMenuState settings)
    {
        _config = config; _settings = settings;
        _instance = this;
        MenuAPI.AddElementToEscapeMenu(AddButton);
        MenuAPI.AddElementToLobbyMenu(AddButton);
    }

    private void AddButton(Transform parent)
    {
        REPOButton button = MenuAPI.CreateREPOButton("Events", Open, parent, Vector2.zero);
        button.gameObject.AddComponent<EventMenuButton>().Initialize(button, parent);
    }

    private void Open()
    {
        if (_page != null) return;
        REPOPopupPage page = MenuAPI.CreateREPOPopupPage("Events", REPOPopupPage.PresetSide.Right,
            shouldCachePage: false, pageDimmerVisibility: true, spacing: 2f);
        _page = page; _rows.Clear(); _navigation.Clear(); _message = "";
        var padding = page.maskPadding; padding.top = 24; padding.bottom = 0; page.maskPadding = padding;
        page.AddElement(parent =>
        {
            AddNavigation(View.Current, "CURRENT EVENTS", parent, 268);
            AddNavigation(View.Guide, "EVENT GUIDE", parent, 230);
            AddNavigation(View.Settings, "EVENT SETTINGS", parent, 192);
            AddNavigation(View.Tools, "TOOLS", parent, 154);
            REPOLabel version = MenuAPI.CreateREPOLabel("Stage Flux v" + StagePhysicsEventsPlugin.PluginVersion, parent, new Vector2(108, 72));
            version.rectTransform.sizeDelta = new Vector2(190, 30);
            version.labelTMP.fontSize = 16; version.labelTMP.alignment = TextAlignmentOptions.Left;
            REPOButton back = MenuAPI.CreateREPOButton("Back", () => Close(page), parent, new Vector2(66, 18));
        });
        page.onEscapePressed += () =>
        {
            if (EventHudEditor.ClosedFrame == Time.frameCount) return false;
            if (EventHudEditor.Instance != null) { EventHudEditor.Instance.Close(false); return false; }
            Forget(); return true;
        };
        _view = View.Guide;
        Refresh();
        page.OpenPage(openOnTop: false);
    }

    private void AddNavigation(View view, string title, Transform parent, float y)
    {
        REPOButton button = MenuAPI.CreateREPOButton(title, () => Switch(view), parent, new Vector2(108, y));
        button.overrideButtonSize = new Vector2(190, 32);
        button.labelTMP.alignment = TextAlignmentOptions.Left; button.labelTMP.fontSize = 20;
        _navigation.Add(view, button);
    }

    private void Switch(View view)
    {
        if (_page == null) return;
        _view = view; _message = "";
        _page.scrollView.SetScrollPosition(0);
        Refresh();
    }

    private void Update()
    {
        if (_page == null || EventHudEditor.Instance != null || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.5f;
        if (Signature() != _signature) Refresh();
    }

    private string Signature()
    {
        bool available = _settings.TryRead(out StageEffect mask, out bool active);
        string value = $"{_view}:{EventMenuState.CanEdit}:{available}:{mask}:{active}:{_settings.Status}";
        if (_view == View.Current && StagePhysicsEventsPlugin.Instance.Controller.TryGetHudState(out HudState state))
            value += $":{state.State}:{state.Effect}:{state.PreviewEffect}:{state.Mode}:{state.DurationSeconds}:{state.IntervalSeconds}:{state.SlotLimit}";
        return value;
    }

    private void Refresh()
    {
        if (_page == null) return;
        List<Entry> entries = new();
        void Text(string text) => entries.Add(new Entry(text));
        void Button(string text, Action action, bool allowed = true, StageEffect icon = StageEffect.None) =>
            entries.Add(new Entry(text, allowed ? action : null, icon, true));
        bool editable = EventMenuState.CanEdit;
        bool available = _settings.TryRead(out StageEffect enabled, out bool active);
        if (_message.Length > 0) Text(_message);
        switch (_view)
        {
            case View.Current:
                if (!StagePhysicsEventsPlugin.Instance.Controller.TryGetHudState(out HudState state))
                    Text("No active stage event schedule. Events may be disabled for this stage, or a playable stage has not started.");
                else
                {
                    Text("Phase: " + (state.State == EventRunState.Active ? "Active" : state.State == EventRunState.Countdown ? "Starting" : "Interval"));
                    Text("Mode: " + state.Mode + " | Event slots: " + state.SlotLimit + "/5");
                    // Keep unrevealed draws hidden, just like the HUD.
                    StageEffect shown = state.State != EventRunState.Waiting ? state.Effect :
                        state.RemainingSeconds <= 10 ? state.PreviewEffect : StageEffect.None;
                    if (shown == StageEffect.None) Text("Waiting for the next event.");
                    foreach (StageEffect effect in StageEffectSet.IndividualEffects)
                        if (StageEffectSet.Contains(shown, effect)) AddGuide(entries, effect);
                }
                break;
            case View.Guide:
                Text("Explore all 46 events. Risk describes possible danger, not the chance of being selected.");
                Text("Roll may kill enemies. Void may kill players. High-risk events can cause damage or loss of valuables.");
                foreach (StageEffect effect in StageEffectSet.IndividualEffects) AddGuide(entries, effect);
                break;
            case View.Settings:
                Text("Changes affect future draws. Active events and already selected stage plans stay unchanged. Chance and detailed settings are available in REPOConfig.");
                Text(editable ? "You are editing the host's event selection." : "Only the host can change event settings.");
                if (!available) { Text(_settings.Status); break; }
                if (!active) Text("Stage Flux events are disabled in General settings.");
                Button("PRESETS", () => Switch(View.Presets));
                foreach (StageEffect effect in StageEffectSet.IndividualEffects)
                {
                    StageEffect captured = effect;
                    bool on = StageEffectSet.Contains(enabled, effect);
                    Button($"[{(on ? "ON" : "OFF")}] {EventPresentation.Name(effect)}", () =>
                    {
                        // Re-read authority and state at the moment of the click.
                        if (EventMenuState.CanEdit && _settings.TryRead(out StageEffect current, out _))
                            _settings.SetEnabled(captured, !StageEffectSet.Contains(current, captured));
                    }, editable, effect);
                }
                break;
            case View.Presets:
                Text("Presets change event ON/OFF only. Probabilities, timing, and safety settings stay unchanged.");
                Button("BACK TO EVENT SETTINGS", () => Switch(View.Settings));
                foreach (string preset in new[] { "Defaults", "Low Risk", "All Off" })
                {
                    string captured = preset;
                    Button("APPLY: " + preset.ToUpperInvariant(), () => { _settings.ApplyPreset(captured); Switch(View.Settings); }, editable);
                }
                Text("Defaults: all events except Roll and Void. Low Risk: only events marked Low. All Off: exclude every event from future draws.");
                break;
            case View.Tools:
                Text("SYNC STATUS\n" + _settings.Status);
                Button("REFRESH DISPLAY DATA", () => { _settings.RefreshDisplay(); _message = "Display data refreshed. An unsupported host cannot provide event settings."; });
                Button("HUD EDITOR", () => EventHudEditor.Open(_config, _page!, _page!.headerTMP.font));
                Text("Preview and adjust your own HUD position, scale, layout and background. No gameplay effects are started.");
                Button("REPORT A PROBLEM", () => Switch(View.Report));
                break;
            case View.Report:
                Text("Copy or open a report, review it, then paste it into a new GitHub issue. Personal information is partly masked. Nothing is uploaded automatically.");
                Button("COPY REPORT", () =>
                {
                    EventBugReport report = StagePhysicsEventsPlugin.Instance.BugReport;
                    report.Create(_settings);
                    GUIUtility.systemCopyBuffer = report.LatestText;
                    _message = "Report copied to clipboard and saved to BepInEx/StageFluxReports.";
                });
                Button("OPEN SAVED REPORT", () =>
                {
                    EventBugReport report = StagePhysicsEventsPlugin.Instance.BugReport;
                    report.Create(_settings);
                    Application.OpenURL(new Uri(report.LatestPath).AbsoluteUri);
                    _message = "Report saved. Add reproduction steps before sharing.";
                });
                Button("OPEN GITHUB ISSUES", () => Switch(View.Issues));
                break;
            case View.Issues:
                Text("Open a new Stage Flux issue on GitHub in your browser? Your report will not be sent automatically. A GitHub account with access to the repository is required to submit an issue.");
                Button("OPEN IN BROWSER", () => Application.OpenURL(EventBugReport.IssuesUrl));
                Button("CANCEL", () => Switch(View.Report));
                break;
        }
        _page.headerTMP.text = _view switch { View.Current => "Current Events", View.Guide => "Event Guide", View.Settings => "Event Settings",
            View.Presets => "Event Presets", View.Tools => "Tools", View.Report => "Bug Report", _ => "GitHub Issues" };
        Apply(entries);
        _signature = Signature();
    }

    private static void AddGuide(List<Entry> entries, StageEffect effect)
    {
        entries.Add(new Entry(EventPresentation.Name(effect) + " - " + EventPresentation.Danger(effect) + " risk", icon: effect));
        entries.Add(new Entry(EventGuideCatalog.Description(effect)));
    }

    private void Apply(List<Entry> entries)
    {
        REPOPopupPage page = _page!;
        float width = Mathf.Clamp(page.maskRectTransform.rect.width - 20f, 260f, 520f);
        while (_rows.Count < entries.Count) _rows.Add(CreateRow(page));
        for (int i = 0; i < _rows.Count; i++)
        {
            Row row = _rows[i];
            row.Element.visibility = i < entries.Count;
            if (i >= entries.Count) continue;
            Entry entry = entries[i];
            bool hasIcon = entry.Icon != StageEffect.None;
            float inset = hasIcon ? 62f : 10f;
            TMP_Text text = row.Label.labelTMP;
            text.text = entry.Text; text.richText = false;
            text.fontSize = entry.Control || hasIcon ? 20 : 18;
            text.fontStyle = hasIcon ? FontStyles.Bold : FontStyles.Normal;
            text.enableWordWrapping = true; text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Truncate;
            float height = Mathf.Max(hasIcon ? 58f : entry.Control ? 40f : 30f,
                text.GetPreferredValues(entry.Text, width - inset - 12, 0).y + 14f);
            text.alignment = TextAlignmentOptions.Left;
            text.color = entry.Control && entry.Click == null ? new Color(0.5f, 0.52f, 0.55f) : Color.white;
            row.Button.overrideButtonSize = new Vector2(width, height);
            row.Button.rectTransform.sizeDelta = new Vector2(width, height);
            row.Focus.sizeDelta = new Vector2(width, height);
            row.Label.rectTransform.anchoredPosition = new Vector2(inset, 0);
            row.Label.rectTransform.sizeDelta = text.rectTransform.sizeDelta = new Vector2(width - inset - 12, height);
            row.Background.gameObject.SetActive(entry.Control);
            row.Background.color = entry.Click == null ? new Color(0.10f, 0.11f, 0.12f, 0.9f) :
                entry.Text.StartsWith("[OFF]", StringComparison.Ordinal) ? new Color(0.04f, 0.12f, 0.20f, 0.95f) : new Color(0.23f, 0.11f, 0.04f, 0.95f);
            row.Icon.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                row.Icon.texture = _icons.Get(entry.Icon);
                row.Icon.rectTransform.sizeDelta = new Vector2(48, 48);
                row.Icon.rectTransform.anchoredPosition = new Vector2(5, (height - 48) / 2);
            }
            row.Button.menuButton.enabled = entry.Click != null;
            row.Button.onClick = entry.Click == null ? null : () =>
            {
                if (_page != page || EventHudEditor.Instance != null) return;
                try { entry.Click(); }
                catch (Exception exception) { _message = "Operation failed. Check the Stage Flux log."; StagePhysicsEventsPlugin.ModLogger.LogError(exception); }
                if (_page == page && EventHudEditor.Instance == null) Refresh();
            };
        }
        page.scrollView.UpdateElements();
    }

    private static Row CreateRow(REPOPopupPage page)
    {
        REPOButton button = null!; REPOLabel label = null!;
        page.AddElementToScrollView(parent =>
        {
            button = MenuAPI.CreateREPOButton("", () => { }, parent, Vector2.zero);
            label = MenuAPI.CreateREPOLabel("", button.rectTransform, Vector2.zero);
            return button.rectTransform;
        }, topPadding: 4f, bottomPadding: 4f);
        button.labelTMP.gameObject.SetActive(false);
        label.labelTMP.raycastTarget = false;
        BottomLeft(label.rectTransform); BottomLeft(label.labelTMP.rectTransform);
        GameObject focus = new("Event Focus", typeof(RectTransform), typeof(MenuSelectableElement));
        focus.transform.SetParent(button.rectTransform, false);
        RectTransform focusRect = (RectTransform)focus.transform; BottomLeft(focusRect);
        button.menuButton.rectTransformSelection = focusRect;
        GameObject icon = new("Event Icon", typeof(RectTransform), typeof(RawImage));
        icon.transform.SetParent(button.rectTransform, false);
        RawImage raw = icon.GetComponent<RawImage>(); raw.raycastTarget = false; BottomLeft(raw.rectTransform);
        GameObject background = new("Event Button Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(button.rectTransform, false); background.transform.SetAsFirstSibling();
        Image image = background.GetComponent<Image>(); image.raycastTarget = false;
        image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        return new Row(button, label, button.GetComponent<REPOScrollViewElement>(), raw, image, focusRect);
    }

    private static void BottomLeft(RectTransform rect)
    { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
    private void Close(REPOPopupPage page) { Forget(); page.ClosePage(closePagesAddedOnTop: true); }
    private void Forget() { _page = null; _rows.Clear(); _navigation.Clear(); }
    private void OnDestroy() { EventHudEditor.Instance?.Close(false); if (_page != null) _page.ClosePage(true); _icons.Dispose(); if (_instance == this) _instance = null; }
    private enum View { Current, Guide, Settings, Presets, Tools, Report, Issues }
    private sealed class Entry(string text, Action? click = null, StageEffect icon = StageEffect.None, bool control = false)
    { internal readonly string Text = text; internal readonly Action? Click = click; internal readonly StageEffect Icon = icon; internal readonly bool Control = control; }
    private sealed class Row(REPOButton button, REPOLabel label, REPOScrollViewElement element, RawImage icon, Image background, RectTransform focus)
    { internal readonly REPOButton Button = button; internal readonly REPOLabel Label = label; internal readonly REPOScrollViewElement Element = element; internal readonly RawImage Icon = icon; internal readonly Image Background = background; internal readonly RectTransform Focus = focus; }
}
