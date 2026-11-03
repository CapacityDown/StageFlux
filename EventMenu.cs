using System;
using System.Collections.Generic;
using HarmonyLib;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REPOJP.StagePhysicsEvents;

internal sealed class EventMenu : MonoBehaviour
{
    private static EventMenu? _instance;
    internal static bool TryGetScrollSettings(MenuScrollBox box, out float lineHeight)
    {
        lineHeight = EventMenuLayout.LineHeight;
        return _instance != null && _instance._page != null && EventHudEditor.Instance == null &&
            _instance._issuesDialog == null && _instance._page.menuScrollBox == box;
    }
    private StagePhysicsConfig _config = null!;
    private EventMenuState _settings = null!;
    private REPOPopupPage? _page;
    private readonly List<Row> _rows = new();
    private readonly EventIconCatalog _icons = new();
    private readonly Dictionary<View, REPOButton> _navigation = new();
    private View _view;
    private string _signature = "", _message = "";
    private float _nextRefresh;
    private bool _currentEnabled;
    private GuideView _guideView;
    private int _presetOverwriteSlot = -1, _dialogClosedFrame = -1;
    private MenuPage? _issuesDialog;
    private REPOButton? _back;
    internal bool IsOpen => _page != null;

    internal void Initialize(StagePhysicsConfig config, EventMenuState settings)
    {
        _config = config; _settings = settings;
        _instance = this;
        MenuAPI.AddElementToEscapeMenu(parent => AddButton(parent, true));
        MenuAPI.AddElementToLobbyMenu(parent => AddButton(parent, false));
    }

    private void AddButton(Transform parent, bool currentEnabled)
    {
        REPOButton button = MenuAPI.CreateREPOButton("Events", () => Open(currentEnabled), parent, Vector2.zero);
        button.gameObject.AddComponent<EventMenuButton>().Initialize(button, parent);
    }

    private void Open(bool currentEnabled)
    {
        if (_page != null) return;
        try { BuildPage(currentEnabled); }
        catch (Exception exception)
        {
            REPOPopupPage? failed = _page; Forget();
            if (failed != null) Destroy(failed.gameObject);
            StagePhysicsEventsPlugin.ModLogger.LogError("Could not open Events: " + exception);
        }
    }

    private void BuildPage(bool currentEnabled)
    {
        if (_page != null) return;
        REPOPopupPage page = MenuAPI.CreateREPOPopupPage("Events", REPOPopupPage.PresetSide.Right,
            shouldCachePage: false, pageDimmerVisibility: true, spacing: EventMenuLayout.RowSpacing);
        _page = page; _rows.Clear(); _navigation.Clear(); _message = "";
        _currentEnabled = currentEnabled; _guideView = GuideView.Overview; _presetOverwriteSlot = -1;
        var padding = page.maskPadding; padding.top = 24; padding.bottom = 0; page.maskPadding = padding;
        page.AddElement(parent =>
        {
            AddNavigation(View.Current, "CURRENT EVENTS", parent, EventMenuLayout.NavigationTop);
            AddNavigation(View.Guide, "EVENT GUIDE", parent, EventMenuLayout.NavigationTop - EventMenuLayout.NavigationStep);
            AddNavigation(View.Settings, "EVENT SETTINGS", parent, EventMenuLayout.NavigationTop - EventMenuLayout.NavigationStep * 2);
            AddNavigation(View.Tools, "TOOLS", parent, EventMenuLayout.NavigationTop - EventMenuLayout.NavigationStep * 3);
            REPOLabel version = MenuAPI.CreateREPOLabel("Stage Flux v" + StagePhysicsEventsPlugin.PluginVersion, parent, new Vector2(108, 54));
            version.rectTransform.sizeDelta = version.labelTMP.rectTransform.sizeDelta = new Vector2(190, 24);
            version.labelTMP.fontSize = 16; version.labelTMP.alignment = TextAlignmentOptions.Left;
            version.labelTMP.color = new Color(0.72f, 0.72f, 0.72f, 0.9f);
            _back = MenuAPI.CreateREPOButton("Back", () => Close(page), parent, new Vector2(66, 18));
        });
        page.onEscapePressed += () =>
        {
            if (_issuesDialog != null || _dialogClosedFrame == Time.frameCount || EventHudEditor.ClosedFrame == Time.frameCount) return false;
            if (EventHudEditor.Instance != null) { EventHudEditor.Instance.Close(false); return false; }
            Forget(); return true;
        };
        _view = currentEnabled ? View.Current : View.Guide;
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
        if (_page == null || (view == View.Current && !_currentEnabled)) return;
        if (_view != view) _presetOverwriteSlot = -1;
        _view = view; _message = "";
        _page.scrollView.SetScrollPosition(0);
        Refresh();
    }

    private void Update()
    {
        if (_page == null || EventHudEditor.Instance != null || _issuesDialog != null || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.25f;
        if (Signature() != _signature) Refresh();
    }

    private string Signature()
    {
        bool available = _settings.TryRead(out StageEffect mask, out bool active);
        _settings.TryReadDetails(StageEffect.Feather, out _);
        string value = $"{_view}:{_guideView}:{EventMenuState.CanEdit}:{available}:{mask}:{active}:{_settings.Status}:{_settings.GuideRevision}:{_settings.Presets.Revision}:{_page!.maskRectTransform.rect.width}";
        if (_view == View.Current && StagePhysicsEventsPlugin.Instance.Controller.TryGetHudState(out HudState state))
            value += $":{state.State}:{state.Effect}:{state.PreviewEffect}:{state.Mode}:{state.DurationSeconds}:{state.IntervalSeconds}:{state.SlotLimit}:{state.RemainingSeconds <= 10}";
        return value;
    }

    private void Refresh()
    {
        if (_page == null) return;
        List<Entry> entries = new();
        void Text(string text, bool separate = false) => AddText(entries, text, separate);
        void Button(string text, Action action, bool allowed = true, StageEffect icon = StageEffect.None, bool off = false, bool separate = false) =>
            entries.Add(new Entry((_view is View.Tools or View.Report ? "> " : "") + text, allowed ? action : null, icon, true,
                off: off, separate: separate, fontSize: _view is View.Tools or View.Report ? 18 : 20));
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
                foreach (GuideView mode in Enum.GetValues(typeof(GuideView)))
                {
                    GuideView captured = mode;
                    entries.Add(new Entry((_guideView == mode ? "> " : "  ") + GuideName(mode), () =>
                    { _guideView = captured; _page.scrollView.SetScrollPosition(0); }, control: true, height: 36));
                }
                Text($"Explore all {StageEffectSet.IndividualEffects.Count} events. Risk describes possible danger, not the chance of being selected.");
                Text("Roll may kill enemies. Void may kill players. High-risk events can cause damage or loss of valuables.");
                if (_guideView == GuideView.Eligibility)
                    Text("Configuration eligibility only. Stage resources and targets are checked when events are drawn. This page never runs an event or predicts a hidden draw.");
                foreach (StageEffect effect in StageEffectSet.IndividualEffects) AddGuide(entries, effect);
                break;
            case View.Settings:
                Text("Changes affect future draws. Active events and already selected stage plans stay unchanged. Chance and detailed settings are available in REPOConfig.");
                Text(editable ? "You are editing the host's event selection." : "Only the host can change event settings.");
                if (!available) { Text(_settings.Status); break; }
                if (!active) Text("Stage Flux events are disabled in General settings.");
                Button("PRESETS", () => Switch(View.Presets));
                Button("Draw eligibility", () => { _guideView = GuideView.Eligibility; Switch(View.Guide); });
                Text($"Enabled events: {CountEnabled(enabled)}/{StageEffectSet.IndividualEffects.Count}");
                if (enabled == StageEffect.None) Text("No events are enabled. Random selection has no candidates.");
                foreach (StageEffect effect in StageEffectSet.IndividualEffects)
                {
                    StageEffect captured = effect;
                    bool on = StageEffectSet.Contains(enabled, effect);
                    Button($"[{(on ? "ON" : "OFF")}] {EventPresentation.Name(effect)}", () =>
                    {
                        // Re-read authority and state at the moment of the click.
                        if (EventMenuState.CanEdit && _settings.TryRead(out StageEffect current, out _))
                            _settings.SetEnabled(captured, !StageEffectSet.Contains(current, captured));
                    }, editable, effect, off: !on);
                }
                break;
            case View.Presets:
                Text("Presets change event ON/OFF only. Probabilities, timing, and safety settings stay unchanged.");
                Button("BACK TO EVENT SETTINGS", () => Switch(View.Settings), separate: true);
                if (editable)
                {
                    if (!_settings.Presets.Available) Text("Saved presets unavailable. The existing file has been preserved.");
                    for (int slot = 0; slot < CustomEventPresets.Capacity; slot++)
                    {
                        int capturedSlot = slot;
                        string name = "Custom " + (slot + 1);
                        string[]? saved = _settings.Presets.Get(slot);
                        Text(name + " - " + (saved == null ? "Empty" : $"Enabled events: {_settings.CountPreset(saved)}/{StageEffectSet.IndividualEffects.Count}"), true);
                        Button("Apply: " + name, () => { if (_settings.ApplyCustomPreset(capturedSlot)) Switch(View.Settings); }, saved != null);
                        bool confirm = saved != null && _presetOverwriteSlot == slot;
                        Button((confirm ? "Confirm overwrite: " : saved == null ? "Save current: " : "Overwrite: ") + name, () =>
                        {
                            if (!EventMenuState.CanEdit) return;
                            if (confirm && _presetOverwriteSlot != capturedSlot) return;
                            if (saved != null && !confirm) { _presetOverwriteSlot = capturedSlot; return; }
                            if (_settings.SaveCustomPreset(capturedSlot, confirm)) { _message = "Saved."; _presetOverwriteSlot = -1; }
                        }, _settings.Presets.Available);
                        if (confirm) Button("CANCEL", () => _presetOverwriteSlot = -1);
                    }
                }
                foreach (string preset in new[] { "Defaults", "Low Risk", "All Off" })
                {
                    string captured = preset;
                    Button("Apply: " + preset, () => { _settings.ApplyPreset(captured); Switch(View.Settings); }, editable, separate: true);
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
                Button("OPEN GITHUB ISSUES", ConfirmOpenIssues);
                break;
        }
        _page.headerTMP.text = _view switch { View.Current => "Current Events", View.Guide => "Event Guide", View.Settings => "Event Settings",
            View.Presets => "Event Presets", View.Tools => "Tools", _ => "Bug Report" };
        UpdateNavigation();
        Apply(entries);
        _signature = Signature();
    }

    private void AddGuide(List<Entry> entries, StageEffect effect)
    {
        entries.Add(new Entry(EventPresentation.Name(effect) + " - " + EventPresentation.Danger(effect) + " risk", icon: effect, height: EventMenuLayout.GuideTitleHeight));
        if (_view != View.Guide || _guideView != GuideView.Eligibility) AddText(entries, EventGuideCatalog.Description(effect));
        if (_view == View.Guide && _guideView != GuideView.Overview)
        {
            if (_settings.TryReadDetails(effect, out EventGuideDetails details))
                AddText(entries, _guideView == GuideView.Details ? details.Text : details.Eligibility);
            else AddText(entries, "Host details unavailable. The host may not support this display. Local settings are not shown as host values.");
        }
        entries.Add(new Entry("", height: EventMenuLayout.GuideSpacing));
    }

    private void Apply(List<Entry> entries)
    {
        REPOPopupPage page = _page!;
        float width = ContentWidth;
        while (_rows.Count < entries.Count) _rows.Add(CreateRow(page));
        for (int i = 0; i < _rows.Count; i++)
        {
            Row row = _rows[i];
            row.Element.visibility = i < entries.Count;
            if (i >= entries.Count) continue;
            Entry entry = entries[i];
            bool hasIcon = entry.Icon != StageEffect.None;
            float height = entry.Height;
            float iconSize = Mathf.Min(56f, height - 8f);
            float padding = entry.Control ? 12f : 0;
            float inset = padding + (hasIcon ? iconSize + 10f : 0);
            TMP_Text text = row.Label.labelTMP;
            text.text = entry.Text; text.richText = false;
            text.fontSize = entry.FontSize;
            text.fontStyle = hasIcon || entry.Control ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.enableAutoSizing = entry.Control;
            text.fontSizeMin = 12; text.fontSizeMax = text.fontSize;
            text.overflowMode = hasIcon ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Left;
            row.Visual.Configure(entry.Control, entry.Click != null, entry.Off);
            bool separate = entry.Separate || (_view != View.Presets && (entry.Control || hasIcon));
            bool consecutive = i > 0 && entry.Control && entries[i - 1].Control;
            row.Separator.gameObject.SetActive(_view != View.Guide && i > 0 && separate && (entry.Separate || !consecutive));
            row.Separator.rectTransform.sizeDelta = new Vector2(width, 1);
            row.Separator.rectTransform.anchoredPosition = new Vector2(0, height + 2);
            row.Button.overrideButtonSize = new Vector2(width, height);
            row.Button.rectTransform.sizeDelta = new Vector2(width, height);
            float focusHeight = entry.Control ? height : Mathf.Max(12f, height * 0.5f);
            row.Focus.sizeDelta = new Vector2(width, focusHeight);
            row.Focus.anchoredPosition = new Vector2(0, (height - focusHeight) / 2);
            row.Label.rectTransform.anchoredPosition = new Vector2(inset, 0);
            row.Label.rectTransform.sizeDelta = text.rectTransform.sizeDelta = new Vector2(width - inset - padding, height);
            row.Icon.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                row.Icon.texture = _icons.Get(entry.Icon, entry.Off);
                row.Icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                row.Icon.rectTransform.anchoredPosition = new Vector2(padding, (height - iconSize) / 2);
            }
            row.Button.menuButton.enabled = entry.Click != null;
            View view = _view;
            row.Button.onClick = entry.Click == null ? null : () =>
            {
                if (_page != page || _view != view || EventHudEditor.Instance != null || _issuesDialog != null) return;
                try { entry.Click(); }
                catch (Exception exception) { _message = "Operation failed. Check the Stage Flux log."; StagePhysicsEventsPlugin.ModLogger.LogError(exception); }
                if (_page == page && EventHudEditor.Instance == null && _issuesDialog == null) Refresh();
            };
        }
        page.scrollView.UpdateElements();
    }

    private Row CreateRow(REPOPopupPage page)
    {
        REPOButton button = null!; REPOLabel label = null!;
        page.AddElementToScrollView(parent =>
        {
            button = MenuAPI.CreateREPOButton("", () => { }, parent, Vector2.zero);
            label = MenuAPI.CreateREPOLabel("", button.rectTransform, Vector2.zero);
            return button.rectTransform;
        }, topPadding: _rows.Count == 0 ? EventMenuLayout.FirstPadding : EventMenuLayout.RowPadding, bottomPadding: EventMenuLayout.RowPadding);
        button.labelTMP.gameObject.SetActive(false);
        button.labelTMP.raycastTarget = false;
        label.labelTMP.raycastTarget = false;
        BottomLeft(label.rectTransform); BottomLeft(label.labelTMP.rectTransform);
        GameObject focus = new("Event Focus", typeof(RectTransform), typeof(MenuSelectableElement));
        focus.transform.SetParent(button.rectTransform, false);
        RectTransform focusRect = (RectTransform)focus.transform; BottomLeft(focusRect);
        button.menuButton.rectTransformSelection = focusRect;
        GameObject icon = new("Event Icon", typeof(RectTransform), typeof(RawImage));
        icon.transform.SetParent(button.rectTransform, false);
        RawImage raw = icon.GetComponent<RawImage>(); raw.raycastTarget = false; BottomLeft(raw.rectTransform);
        GameObject divider = new("Section Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(button.rectTransform, false);
        Image image = divider.GetComponent<Image>(); image.raycastTarget = false;
        image.color = new Color(0.65f, 0.4f, 0.16f, 0.6f);
        BottomLeft(image.rectTransform); divider.SetActive(false);
        EventMenuButtonVisual visual = button.gameObject.AddComponent<EventMenuButtonVisual>();
        visual.Initialize(button, label.labelTMP);
        return new Row(button, label, button.GetComponent<REPOScrollViewElement>(), raw, image, focusRect, visual);
    }

    private float ContentWidth => Mathf.Clamp(_page!.maskRectTransform.rect.width - 20f, 260f, 520f);

    private void AddText(List<Entry> entries, string value, bool separate = false)
    {
        if (_rows.Count == 0) _rows.Add(CreateRow(_page!));
        TMP_Text text = _rows[0].Label.labelTMP;
        float oldSize = text.fontSize; FontStyles oldStyle = text.fontStyle;
        TextWrappingModes oldWrap = text.textWrappingMode; bool oldAuto = text.enableAutoSizing;
        try
        {
            text.richText = false; text.fontSize = EventMenuLayout.BodyFont; text.fontStyle = FontStyles.Normal;
            text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.NoWrap;
            foreach (string line in EventMenuLayout.Wrap(value, ContentWidth, s => text.GetPreferredValues(s).x))
            {
                entries.Add(new Entry(line, height: line.Length == 0 ? EventMenuLayout.GuideSpacing : EventMenuLayout.BodyHeight, separate: separate));
                separate = false;
            }
        }
        finally { text.fontSize = oldSize; text.fontStyle = oldStyle; text.textWrappingMode = oldWrap; text.enableAutoSizing = oldAuto; }
    }

    private static int CountEnabled(StageEffect mask)
    { int count = 0; foreach (StageEffect effect in StageEffectSet.IndividualEffects) if (StageEffectSet.Contains(mask, effect)) count++; return count; }

    private static string GuideName(GuideView view) => view switch
    { GuideView.Overview => "Overview", GuideView.Details => "Details", _ => "Draw eligibility" };

    private void UpdateNavigation()
    {
        foreach (var pair in _navigation)
        {
            View selected = _view == View.Presets ? View.Settings : _view == View.Report ? View.Tools : _view;
            string title = pair.Key switch { View.Current => "CURRENT EVENTS", View.Guide => "EVENT GUIDE", View.Settings => "EVENT SETTINGS", _ => "TOOLS" };
            pair.Value.labelTMP.text = (selected == pair.Key ? "> " : "  ") + title;
            pair.Value.labelTMP.enableAutoSizing = true; pair.Value.labelTMP.fontSizeMin = 12; pair.Value.labelTMP.fontSizeMax = 20;
            bool enabled = pair.Key != View.Current || _currentEnabled;
            pair.Value.menuButton.enabled = enabled;
            pair.Value.labelTMP.color = enabled ? Color.white : new Color(0.35f, 0.35f, 0.35f, 0.75f);
            Button? unity = pair.Value.GetComponent<Button>(); if (unity != null) unity.interactable = enabled;
        }
        if (_back != null) _back.labelTMP.text = "  Back";
        _page!.headerTMP.enableAutoSizing = true; _page.headerTMP.fontSizeMin = 18; _page.headerTMP.fontSizeMax = 32;
    }

    private void ConfirmOpenIssues()
    {
        if (_issuesDialog != null || _page == null) return;
        REPOPopupPage page = _page;
        bool decided = false;
        void Decide(bool open)
        {
            if (decided) return;
            decided = true; _dialogClosedFrame = Time.frameCount;
            if (!open || _page != page) return;
            try { Application.OpenURL(EventBugReport.IssuesUrl); }
            catch (Exception exception) { _message = "Could not open the browser."; StagePhysicsEventsPlugin.ModLogger.LogError(exception); }
        }
        MenuAPI.OpenPopup("OPEN EXTERNAL SITE", new Color(1, 0.65f, 0),
            "Open GitHub Issues in your browser? Nothing is sent automatically.\n\n" + EventBugReport.IssuesUrl,
            () => Decide(true), () => Decide(false));
        _issuesDialog = AccessTools.Field(typeof(MenuManager), "currentMenuPage").GetValue(MenuManager.instance) as MenuPage;
        if (_issuesDialog == null) return;
        MenuPageTwoOptions dialog = _issuesDialog.GetComponent<MenuPageTwoOptions>();
        if (dialog == null) { _issuesDialog = null; return; }
        dialog.option1Button.buttonTextString = "OPEN IN BROWSER";
        dialog.option2Button.buttonTextString = "CANCEL";
        dialog.bodyTextMesh.textWrappingMode = TextWrappingModes.Normal;
        dialog.bodyTextMesh.enableAutoSizing = true; dialog.bodyTextMesh.fontSizeMin = 12; dialog.bodyTextMesh.fontSizeMax = 18;
    }

    private static void BottomLeft(RectTransform rect)
    { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
    private void Close(REPOPopupPage page) { Forget(); page.ClosePage(closePagesAddedOnTop: true); }
    private void Forget() { _page = null; _rows.Clear(); _navigation.Clear(); _back = null; _presetOverwriteSlot = -1; }
    private void OnDestroy() { EventHudEditor.Instance?.Close(false); if (_page != null) _page.ClosePage(true); _icons.Dispose(); if (_instance == this) _instance = null; }
    private enum View { Current, Guide, Settings, Presets, Tools, Report }
    private enum GuideView { Overview, Details, Eligibility }
    private sealed class Entry(string text, Action? click = null, StageEffect icon = StageEffect.None, bool control = false,
        float height = 0, bool off = false, bool separate = false, float fontSize = 0)
    {
        internal readonly string Text = text; internal readonly Action? Click = click;
        internal readonly StageEffect Icon = icon; internal readonly bool Control = control, Off = off, Separate = separate;
        internal readonly float Height = height > 0 ? height : icon != StageEffect.None ? 56 : control ? 40 : EventMenuLayout.BodyHeight;
        internal readonly float FontSize = fontSize > 0 ? fontSize : control || icon != StageEffect.None ? EventMenuLayout.TitleFont : EventMenuLayout.BodyFont;
    }
    private sealed class Row(REPOButton button, REPOLabel label, REPOScrollViewElement element, RawImage icon, Image separator, RectTransform focus, EventMenuButtonVisual visual)
    { internal readonly REPOButton Button = button; internal readonly REPOLabel Label = label; internal readonly REPOScrollViewElement Element = element; internal readonly RawImage Icon = icon; internal readonly Image Separator = separator; internal readonly RectTransform Focus = focus; internal readonly EventMenuButtonVisual Visual = visual; }
}
