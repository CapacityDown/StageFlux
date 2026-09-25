using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REPOJP.StagePhysicsEvents;

internal sealed class StagePhysicsEventHud : MonoBehaviour
{
    private const int PhysicalSlotCount = 5;
    private const float ReelFrameSeconds = 0.075f;
    private const float FirstStopSeconds = 1.1f;
    private const float StopStepSeconds = 0.18f;
    private const float SettleSeconds = 0.22f;
    private const float SlideTravel = 44f;

    private StagePhysicsConfig _config = null!;
    private StagePhysicsEventController _controller = null!;
    private readonly EventIconCatalog _icons = new();
    private readonly List<TMP_Text> _fontTargets = new();
    private readonly SlotView[] _slots = new SlotView[PhysicalSlotCount];
    private GameObject? _root;
    private GameObject? _classicRoot;
    private GameObject? _graphicalRoot;
    private RectTransform? _canvasRect;
    private RectTransform? _classicRect;
    private RectTransform? _graphicalRect;
    private Image? _graphicalPanel;
    private TextMeshProUGUI? _classicText;
    private TextMeshProUGUI? _headerText;
    private TextMeshProUGUI? _limitText;
    private TextMeshProUGUI? _timerLabel;
    private RectTransform? _timerRoot;
    private RectTransform? _timerHand;
    private string _lastText = string.Empty;
    private string _lastLayout = string.Empty;
    private string _lastGraphicalState = string.Empty;
    private EventRunState _lastGraphicalRunState = EventRunState.Inactive;
    private StageEffect _lastGraphicalPreviewEffect = StageEffect.None;
    private float _nextFontProbeAt;
    private bool _graphicalInitialized;
    private EventHudLayout _layout = new();
    private bool _preview;
    private Transform? _previewParent;
    private TMP_FontAsset? _previewFont;
    internal RectTransform? PreviewRect => _layout.Style == "Classic" ? _classicRect : _graphicalRect;

    internal void InitializePreview(StagePhysicsConfig config, EventHudLayout layout, Transform parent, TMP_FontAsset font)
    {
        _config = config; _layout = layout; _previewParent = parent; _previewFont = font; _preview = true;
    }

    internal void RefreshPreview() { _lastLayout = ""; Update(); }

    internal void MovePreview(Vector2 screenDelta, int originalX, int originalY)
    {
        _layout.X = Mathf.Clamp(originalX + Mathf.RoundToInt(screenDelta.x * 540f / Mathf.Max(1, Screen.height)), -3840, 3840);
        _layout.Y = Mathf.Clamp(originalY + Mathf.RoundToInt(screenDelta.y * 540f / Mathf.Max(1, Screen.height)), -2160, 2160);
        RefreshPreview();
        ClampPreview();
    }

    internal void ClampPreview()
    {
        if (!_preview || PreviewRect == null || _canvasRect == null) return;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_canvasRect, PreviewRect);
        Rect area = _canvasRect.rect;
        float x = bounds.min.x < area.xMin ? area.xMin - bounds.min.x : bounds.max.x > area.xMax ? area.xMax - bounds.max.x : 0;
        float y = bounds.min.y < area.yMin ? area.yMin - bounds.min.y : bounds.max.y > area.yMax ? area.yMax - bounds.max.y : 0;
        if (Mathf.Abs(x) < 0.5f && Mathf.Abs(y) < 0.5f) return;
        _layout.X += Mathf.RoundToInt(x / ResolveResolutionScale());
        _layout.Y += Mathf.RoundToInt(y / ResolveResolutionScale());
        RefreshPreview();
    }

    internal void Initialize(StagePhysicsConfig config, StagePhysicsEventController controller)
    {
        _config = config;
        _controller = controller;
    }

    private void Update()
    {
        if (!_preview) _layout.Refresh(_config);
        EnsureCreated();
        if (_root == null)
        {
            return;
        }

        HudState state = default;
        bool visible;
        if (_preview)
        {
            state = new HudState(EventRunState.Active, StageEffect.Feather | StageEffect.Battery | StageEffect.Heal,
                StageEffect.None, EventMode.RandomEachEvent, 30, 60, 20, 30, 3);
            visible = true;
        }
        else visible = EventHudEditor.Instance == null && _layout.Enabled && _controller.TryGetHudState(out state);
        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
        if (!visible)
        {
            _graphicalInitialized = false;
            _lastGraphicalRunState = EventRunState.Inactive;
            _lastGraphicalPreviewEffect = StageEffect.None;
            return;
        }

        bool graphical = !string.Equals(
            _layout.Style,
            "Classic",
            StringComparison.OrdinalIgnoreCase);
        _classicRoot?.SetActive(!graphical);
        _graphicalRoot?.SetActive(graphical);
        ApplyBackgroundOpacity();
        ApplyLayout(graphical);

        if (graphical)
        {
            UpdateGraphical(state);
        }
        else
        {
            UpdateClassic(state);
        }
        TryApplyRepoUiFont();
    }

    private void EnsureCreated()
    {
        if (_root != null)
        {
            return;
        }

        Transform? layerParent = _preview ? _previewParent : HealthUI.instance != null
            ? HealthUI.instance.transform.parent
            : HUDCanvas.instance?.transform;
        if (layerParent == null)
        {
            return;
        }

        _root = new GameObject("StageFlux_HUD", typeof(RectTransform));
        _root.hideFlags = HideFlags.HideAndDontSave;
        _root.transform.SetParent(layerParent, false);
        RectTransform rootRect = _root.GetComponent<RectTransform>();
        _canvasRect = rootRect;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        if (!_preview && HealthUI.instance != null && HealthUI.instance.transform.parent == layerParent)
        {
            _root.transform.SetSiblingIndex(HealthUI.instance.transform.GetSiblingIndex() + 1);
        }

        CreateClassic();
        CreateGraphical();
        _lastLayout = string.Empty;
        _lastText = string.Empty;
        _lastGraphicalState = string.Empty;
        _root.SetActive(false);
    }

    private void CreateClassic()
    {
        _classicRoot = new GameObject("Classic", typeof(RectTransform));
        _classicRoot.transform.SetParent(_root!.transform, false);
        _classicRect = _classicRoot.GetComponent<RectTransform>();
        _classicRect.sizeDelta = new Vector2(700f, 360f);

        _classicText = CreateText(_classicRoot.transform, "EventText", 28f);
        RectTransform textRect = _classicText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        _classicText.alignment = TextAlignmentOptions.Center;
    }

    private void CreateGraphical()
    {
        _graphicalRoot = new GameObject(
            "Graphical",
            typeof(RectTransform),
            typeof(Image));
        _graphicalRoot.transform.SetParent(_root!.transform, false);
        _graphicalRect = _graphicalRoot.GetComponent<RectTransform>();
        _graphicalPanel = _graphicalRoot.GetComponent<Image>();
        _graphicalPanel.sprite = _icons.PanelSprite;
        _graphicalPanel.type = Image.Type.Sliced;
        _graphicalPanel.color = new Color(1f, 1f, 1f, 0.5f);
        _graphicalPanel.raycastTarget = false;

        _headerText = CreateText(_graphicalRoot.transform, "Header", 20f);
        _headerText.alignment = TextAlignmentOptions.Center;
        _headerText.fontStyle = FontStyles.Bold;

        _limitText = CreateText(_graphicalRoot.transform, "Limit", 13f);
        _limitText.alignment = TextAlignmentOptions.Center;
        _limitText.fontStyle = FontStyles.Bold;
        _limitText.color = new Color(0.74f, 0.74f, 0.68f, 1f);

        GameObject timer = new("SharedStopwatch", typeof(RectTransform));
        timer.transform.SetParent(_graphicalRoot.transform, false);
        _timerRoot = timer.GetComponent<RectTransform>();
        _timerRoot.sizeDelta = new Vector2(100f, 100f);

        GameObject faceObject = new("Face", typeof(RectTransform), typeof(RawImage));
        faceObject.transform.SetParent(timer.transform, false);
        RectTransform faceRect = faceObject.GetComponent<RectTransform>();
        Stretch(faceRect, 0f);
        RawImage face = faceObject.GetComponent<RawImage>();
        face.texture = _icons.Stopwatch;
        face.raycastTarget = false;

        GameObject handObject = new("Hand", typeof(RectTransform), typeof(Image));
        handObject.transform.SetParent(timer.transform, false);
        _timerHand = handObject.GetComponent<RectTransform>();
        _timerHand.anchorMin = new Vector2(0.5f, 0.5f);
        _timerHand.anchorMax = new Vector2(0.5f, 0.5f);
        _timerHand.pivot = new Vector2(0.5f, 0f);
        _timerHand.anchoredPosition = new Vector2(0f, -3f);
        _timerHand.sizeDelta = new Vector2(2.6f, 21f);
        Image hand = handObject.GetComponent<Image>();
        hand.color = new Color(0.12f, 0.09f, 0.07f, 1f);
        hand.raycastTarget = false;

        GameObject hubObject = new("Hub", typeof(RectTransform), typeof(Image));
        hubObject.transform.SetParent(timer.transform, false);
        RectTransform hubRect = hubObject.GetComponent<RectTransform>();
        SetRect(hubRect, new Vector2(0f, -3f), new Vector2(7f, 7f));
        Image hub = hubObject.GetComponent<Image>();
        hub.sprite = _icons.RoundedSprite;
        hub.type = Image.Type.Sliced;
        hub.color = new Color(0.12f, 0.09f, 0.07f, 1f);
        hub.raycastTarget = false;

        _timerLabel = CreateText(_graphicalRoot.transform, "TimerLabel", 13f);
        _timerLabel.alignment = TextAlignmentOptions.Center;
        _timerLabel.fontStyle = FontStyles.Bold;

        for (int index = 0; index < PhysicalSlotCount; index++)
        {
            _slots[index] = CreateSlot(index);
        }
    }

    private SlotView CreateSlot(int index)
    {
        GameObject root = new($"Slot_{index + 1}", typeof(RectTransform));
        root.transform.SetParent(_graphicalRoot!.transform, false);

        GameObject frameObject = new("DangerFrame", typeof(RectTransform), typeof(Image));
        frameObject.transform.SetParent(root.transform, false);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        Image border = frameObject.GetComponent<Image>();
        border.sprite = _icons.RoundedSprite;
        border.type = Image.Type.Sliced;
        border.color = EventPresentation.Color(EventDanger.Neutral);
        border.raycastTarget = false;

        GameObject innerObject = new(
            "Inner",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        innerObject.transform.SetParent(frameObject.transform, false);
        RectTransform innerRect = innerObject.GetComponent<RectTransform>();
        Stretch(innerRect, 3f);
        Image inner = innerObject.GetComponent<Image>();
        inner.sprite = _icons.RoundedSprite;
        inner.type = Image.Type.Sliced;
        inner.color = new Color(0.025f, 0.029f, 0.028f, 0.96f);
        inner.raycastTarget = false;
        RectMask2D iconMask = innerObject.GetComponent<RectMask2D>();
        iconMask.padding = Vector4.zero;

        GameObject iconObject = new(
            "Icon",
            typeof(RectTransform),
            typeof(RawImage),
            typeof(AspectRatioFitter));
        iconObject.transform.SetParent(innerObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.08f, 0.08f);
        iconRect.anchorMax = new Vector2(0.92f, 0.92f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        AspectRatioFitter iconAspect = iconObject.GetComponent<AspectRatioFitter>();
        iconAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        iconAspect.aspectRatio = 1f;
        RawImage icon = iconObject.GetComponent<RawImage>();
        icon.raycastTarget = false;
        icon.color = Color.white;

        TextMeshProUGUI label = CreateText(root.transform, "Label", 15f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 15f;

        return new SlotView(
            root.GetComponent<RectTransform>(),
            frameRect,
            border,
            iconRect,
            icon,
            label);
    }

    private void UpdateClassic(HudState state)
    {
        if (_classicText == null)
        {
            return;
        }
        string display = BuildClassicText(state);
        if (_lastText != display)
        {
            _lastText = display;
            _classicText.text = display;
        }
    }

    private void UpdateGraphical(HudState state)
    {
        bool vertical = !string.Equals(
            _layout.Direction,
            "Horizontal",
            StringComparison.OrdinalIgnoreCase);
        int slotLimit = Mathf.Clamp(state.SlotLimit, 1, PhysicalSlotCount);
        StageEffect displayed = state.State == EventRunState.Waiting
            ? state.PreviewEffect
            : state.Effect;
        string signature =
            $"{state.State}:{(long)state.Effect}:{(long)state.PreviewEffect}:{slotLimit}:{vertical}";

        if (_lastGraphicalState != signature)
        {
            bool immediate = !_graphicalInitialized;
            bool revealPlannedEvent =
                state.State == EventRunState.Waiting &&
                state.PreviewEffect != StageEffect.None &&
                (_lastGraphicalRunState != EventRunState.Waiting ||
                 _lastGraphicalPreviewEffect != state.PreviewEffect);
            _lastGraphicalState = signature;
            _graphicalInitialized = true;
            ApplySlotTargets(
                state,
                displayed,
                slotLimit,
                immediate,
                revealPlannedEvent);
        }
        _lastGraphicalRunState = state.State;
        _lastGraphicalPreviewEffect = state.PreviewEffect;

        if (_headerText != null)
        {
            _headerText.text = vertical
                ? "STAGE EVENTS"
                : state.State switch
                {
                    EventRunState.Waiting => "WAITING",
                    _ => "STAGE EVENTS"
                };
        }
        if (_limitText != null)
        {
            _limitText.text = $"LIMIT {slotLimit} / {PhysicalSlotCount}";
        }
        if (_timerLabel != null)
        {
            _timerLabel.text = state.State == EventRunState.Waiting
                ? "INTERVAL"
                : state.Mode == EventMode.PersistentForStage
                    ? "PERSISTENT"
                    : "TIME";
        }

        float now = Time.unscaledTime;
        for (int index = 0; index < PhysicalSlotCount; index++)
        {
            _slots[index].Tick(now, _icons);
        }
        UpdateStopwatch(state);
    }

    private void ApplySlotTargets(
        HudState state,
        StageEffect displayed,
        int slotLimit,
        bool immediate,
        bool revealPlannedEvent)
    {
        List<StageEffect> effects = new(PhysicalSlotCount);
        foreach (StageEffect effect in StageEffectSet.IndividualEffects)
        {
            if (StageEffectSet.Contains(displayed, effect))
            {
                effects.Add(effect);
            }
        }

        EventDanger waitingDanger = EventPresentation.Danger(state.PreviewEffect);
        float startTime = Time.unscaledTime;
        bool waitingWithPlan =
            state.State == EventRunState.Waiting &&
            displayed != StageEffect.None;
        for (int index = 0; index < PhysicalSlotCount; index++)
        {
            if (index >= slotLimit)
            {
                _slots[index].SetTarget(
                    StageEffect.None,
                    locked: true,
                    waiting: false,
                    EventDanger.Locked,
                    startTime,
                    index,
                    immediate,
                    slideToTarget: false,
                    forceSpin: false,
                    _icons);
                continue;
            }

            if (state.State == EventRunState.Waiting && !waitingWithPlan)
            {
                _slots[index].SetTarget(
                    StageEffect.None,
                    locked: false,
                    waiting: index == 0,
                    index == 0 ? waitingDanger : EventDanger.Neutral,
                    startTime,
                    index,
                    immediate,
                    slideToTarget: true,
                    forceSpin: false,
                    _icons);
                continue;
            }

            StageEffect effect = index < effects.Count ? effects[index] : StageEffect.None;
            _slots[index].SetTarget(
                effect,
                locked: false,
                waiting: false,
                EventPresentation.Danger(effect),
                startTime,
                index,
                immediate,
                slideToTarget: false,
                forceSpin: waitingWithPlan && revealPlannedEvent,
                _icons);
        }
    }

    private void UpdateStopwatch(HudState state)
    {
        if (_timerHand == null)
        {
            return;
        }
        int total = Mathf.Max(1, state.PhaseDurationSeconds);
        float remaining = Mathf.Clamp(state.RemainingSeconds, 0, total);
        float elapsedRatio = 1f - remaining / total;
        _timerHand.localRotation = Quaternion.Euler(0f, 0f, -360f * elapsedRatio);
    }

    private void ApplyBackgroundOpacity()
    {
        if (_graphicalPanel == null)
        {
            return;
        }

        float alpha = Mathf.Clamp01(_layout.Opacity / 100f);
        Color color = _graphicalPanel.color;
        if (!Mathf.Approximately(color.a, alpha))
        {
            color.a = alpha;
            _graphicalPanel.color = color;
        }
    }

    private void ApplyLayout(bool graphical)
    {
        int canvasWidth = _canvasRect != null
            ? Mathf.RoundToInt(_canvasRect.rect.width)
            : Screen.width;
        int canvasHeight = _canvasRect != null
            ? Mathf.RoundToInt(_canvasRect.rect.height)
            : Screen.height;
        string signature =
            $"{graphical}:{_layout.Direction}:{_layout.Anchor}:" +
            $"{_layout.Alignment}:{_layout.X}:" +
            $"{_layout.Y}:{_layout.Scale}:" +
            $"{canvasWidth}x{canvasHeight}:{Screen.width}x{Screen.height}";
        if (_lastLayout == signature)
        {
            return;
        }
        _lastLayout = signature;
        Vector2 anchor = ResolveAnchor(_layout.Anchor);
        float userScale = _layout.Scale / 100f;
        float resolutionScale = ResolveResolutionScale();
        bool vertical = !string.Equals(
            _layout.Direction,
            "Horizontal",
            StringComparison.OrdinalIgnoreCase);

        if (_classicRect != null)
        {
            float classicScale = CalculateResponsiveScale(
                _classicRect.sizeDelta,
                userScale,
                resolutionScale);
            PlaceAtAnchor(_classicRect, anchor, classicScale, resolutionScale);
        }
        if (_classicText != null)
        {
            _classicText.alignment = _layout.Alignment switch
            {
                "Left" => TextAlignmentOptions.Left,
                "Right" => TextAlignmentOptions.Right,
                _ => TextAlignmentOptions.Center
            };
        }
        if (_graphicalRect != null)
        {
            LayoutGraphical(vertical);
            float graphicalScale = CalculateResponsiveScale(
                _graphicalRect.sizeDelta,
                userScale,
                resolutionScale);
            PlaceAtAnchor(_graphicalRect, anchor, graphicalScale, resolutionScale);
        }
        _lastGraphicalState = string.Empty;
    }

    private void LayoutGraphical(bool vertical)
    {
        if (_graphicalRect == null ||
            _headerText == null ||
            _limitText == null ||
            _timerLabel == null ||
            _timerRoot == null)
        {
            return;
        }
        RectTransform header = _headerText.rectTransform;
        RectTransform limit = _limitText.rectTransform;
        RectTransform timerLabel = _timerLabel.rectTransform;
        if (vertical)
        {
            _graphicalRect.sizeDelta = new Vector2(220f, 430f);
            SetRect(header, new Vector2(-36f, 188f), new Vector2(124f, 25f));
            SetRect(limit, new Vector2(-36f, 166f), new Vector2(124f, 18f));
            SetRect(_timerRoot, new Vector2(70f, 181f), new Vector2(58f, 58f));
            SetRect(timerLabel, new Vector2(70f, 145f), new Vector2(92f, 17f));
            for (int index = 0; index < PhysicalSlotCount; index++)
            {
                SetRect(
                    _slots[index].Rect,
                    new Vector2(0f, 111f - index * 62f),
                    new Vector2(196f, 58f));
                _slots[index].ApplyLayout(vertical: true);
            }
        }
        else
        {
            _graphicalRect.sizeDelta = new Vector2(640f, 160f);
            SetRect(header, new Vector2(-210f, 58f), new Vector2(185f, 22f));
            _headerText.alignment = TextAlignmentOptions.Left;
            SetRect(limit, new Vector2(207f, 58f), new Vector2(180f, 18f));
            _limitText.alignment = TextAlignmentOptions.Right;
            for (int index = 0; index < PhysicalSlotCount; index++)
            {
                SetRect(
                    _slots[index].Rect,
                    new Vector2(-220f + index * 94f, 0f),
                    new Vector2(88f, 98f));
                _slots[index].ApplyLayout(vertical: false);
            }
            SetRect(_timerRoot, new Vector2(252f, 2f), new Vector2(66f, 66f));
            SetRect(timerLabel, new Vector2(252f, -41f), new Vector2(86f, 18f));
        }

        if (vertical)
        {
            _headerText.alignment = TextAlignmentOptions.Center;
            _limitText.alignment = TextAlignmentOptions.Center;
        }
    }

    private float ResolveResolutionScale()
    {
        float canvasHeight = _canvasRect != null && _canvasRect.rect.height > 1f
            ? _canvasRect.rect.height
            : Screen.height;
        return Mathf.Max(0.1f, canvasHeight / 540f);
    }

    private float CalculateResponsiveScale(
        Vector2 layoutSize,
        float userScale,
        float resolutionScale)
    {
        float desiredScale = Mathf.Max(0.1f, resolutionScale * userScale);
        if (_canvasRect == null ||
            _canvasRect.rect.width <= 1f ||
            _canvasRect.rect.height <= 1f ||
            layoutSize.x <= 1f ||
            layoutSize.y <= 1f)
        {
            return desiredScale;
        }

        float safeMargin = 8f * resolutionScale;
        float availableWidth = Mathf.Max(1f, _canvasRect.rect.width - safeMargin * 2f);
        float availableHeight = Mathf.Max(1f, _canvasRect.rect.height - safeMargin * 2f);
        float fitScale = Mathf.Min(
            availableWidth / layoutSize.x,
            availableHeight / layoutSize.y);
        return Mathf.Max(0.1f, Mathf.Min(desiredScale, fitScale));
    }

    private void PlaceAtAnchor(
        RectTransform rect,
        Vector2 anchor,
        float scale,
        float resolutionScale)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = new Vector2(
            _layout.X * resolutionScale,
            _layout.Y * resolutionScale);
        rect.localScale = Vector3.one * scale;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, float size)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = new Color(0.9f, 0.91f, 0.87f, 1f);
        text.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        text.outlineWidth = 0.15f;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        if (_previewFont != null) text.font = _previewFont;
        _fontTargets.Add(text);
        return text;
    }

    private void TryApplyRepoUiFont()
    {
        if (_preview) return;
        if (Time.unscaledTime < _nextFontProbeAt)
        {
            return;
        }
        _nextFontProbeAt = Time.unscaledTime + 2f;

        TMP_Text? source = HealthUI.instance != null
            ? HealthUI.instance.GetComponent<TextMeshProUGUI>()
            : null;
        source ??= ChatUI.instance?.chatText;
        if (source == null && HUD.instance != null)
        {
            foreach (TMP_Text candidate in HUD.instance.GetComponentsInChildren<TMP_Text>(true))
            {
                if (candidate != null && !_fontTargets.Contains(candidate) && candidate.font != null)
                {
                    source = candidate;
                    break;
                }
            }
        }
        if (source?.font == null)
        {
            return;
        }
        foreach (TMP_Text target in _fontTargets)
        {
            if (target != null && target.font != source.font)
            {
                target.font = source.font;
            }
        }
    }

    private static string BuildClassicText(HudState state)
    {
        if (state.State == EventRunState.Waiting)
        {
            return $"Waiting\nInterval: {state.IntervalSeconds}s";
        }
        if (state.State == EventRunState.Countdown)
        {
            return StageEffectSet.Format(state.Effect, "\n");
        }
        if (state.IntervalSeconds == 0)
        {
            return $"{StageEffectSet.Format(state.Effect, "\n")}\nMode: Persistent";
        }
        return $"{StageEffectSet.Format(state.Effect, "\n")}\nDuration: {state.DurationSeconds}s";
    }

    private static Vector2 ResolveAnchor(string anchor) => anchor switch
    {
        "TopLeft" => new Vector2(0f, 1f),
        "TopCenter" => new Vector2(0.5f, 1f),
        "TopRight" => new Vector2(1f, 1f),
        "MiddleLeft" => new Vector2(0f, 0.5f),
        "MiddleCenter" => new Vector2(0.5f, 0.5f),
        "MiddleRight" => new Vector2(1f, 0.5f),
        "BottomLeft" => new Vector2(0f, 0f),
        "BottomCenter" => new Vector2(0.5f, 0f),
        "BottomRight" => new Vector2(1f, 0f),
        _ => new Vector2(1f, 0f)
    };

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void OnDestroy()
    {
        _icons.Dispose();
        if (_root != null)
        {
            Destroy(_root);
        }
    }

    private sealed class SlotView
    {
        private StageEffect _targetEffect;
        private bool _targetLocked;
        private bool _targetWaiting;
        private EventDanger _targetDanger;
        private bool _hasTarget;
        private bool _spinning;
        private bool _sliding;
        private bool _slideSwapped;
        private float _startedAt;
        private float _stopAt;
        private float _settleUntil;
        private float _slideStartsAt;
        private float _slideSwapAt;
        private float _slideEndsAt;
        private int _lastReelFrame = -1;

        internal SlotView(
            RectTransform rect,
            RectTransform frameRect,
            Image border,
            RectTransform iconRect,
            RawImage icon,
            TextMeshProUGUI label)
        {
            Rect = rect;
            FrameRect = frameRect;
            Border = border;
            IconRect = iconRect;
            Icon = icon;
            Label = label;
        }

        internal RectTransform Rect { get; }
        private RectTransform FrameRect { get; }
        private Image Border { get; }
        private RectTransform IconRect { get; }
        private RawImage Icon { get; }
        private TextMeshProUGUI Label { get; }

        internal void ApplyLayout(bool vertical)
        {
            if (vertical)
            {
                SetRect(FrameRect, new Vector2(-64f, 0f), new Vector2(56f, 56f));
                SetRect(Label.rectTransform, new Vector2(34f, 0f), new Vector2(126f, 34f));
                Label.alignment = TextAlignmentOptions.Left;
                Label.fontSizeMin = 8f;
                Label.fontSizeMax = 15f;
            }
            else
            {
                SetRect(FrameRect, new Vector2(0f, 10f), new Vector2(64f, 64f));
                SetRect(Label.rectTransform, new Vector2(0f, -35f), new Vector2(86f, 18f));
                Label.alignment = TextAlignmentOptions.Center;
                Label.fontSizeMin = 7f;
                Label.fontSizeMax = 12f;
            }
        }

        internal void SetTarget(
            StageEffect effect,
            bool locked,
            bool waiting,
            EventDanger danger,
            float startTime,
            int stopOrder,
            bool immediate,
            bool slideToTarget,
            bool forceSpin,
            EventIconCatalog icons)
        {
            bool changed = forceSpin ||
                !_hasTarget ||
                _targetEffect != effect ||
                _targetLocked != locked ||
                _targetWaiting != waiting;
            _targetEffect = effect;
            _targetLocked = locked;
            _targetWaiting = waiting;
            _targetDanger = danger;
            _hasTarget = true;

            if (!changed)
            {
                Border.color = EventPresentation.Color(danger);
                return;
            }
            if ((immediate && !forceSpin) || locked)
            {
                _spinning = false;
                _sliding = false;
                ShowTarget(icons);
                return;
            }

            if (slideToTarget)
            {
                _spinning = false;
                _sliding = true;
                _slideSwapped = false;
                _slideStartsAt = startTime;
                _slideSwapAt = _slideStartsAt + ReelFrameSeconds;
                _slideEndsAt = _slideSwapAt + ReelFrameSeconds;
                return;
            }

            _sliding = false;
            _spinning = true;
            _startedAt = startTime;
            _stopAt = startTime + FirstStopSeconds + stopOrder * StopStepSeconds;
            _settleUntil = _stopAt + SettleSeconds;
            _lastReelFrame = -1;
            Label.text = string.Empty;
            Border.color = EventPresentation.Color(EventDanger.Neutral);
        }

        internal void Tick(float now, EventIconCatalog icons)
        {
            if (_sliding)
            {
                if (now < _slideStartsAt)
                {
                    return;
                }

                if (now < _slideSwapAt)
                {
                    float outgoing = Mathf.InverseLerp(
                        _slideStartsAt,
                        _slideSwapAt,
                        now);
                    float offset = Mathf.Lerp(0f, -SlideTravel, outgoing);
                    IconRect.anchoredPosition = new Vector2(0f, offset);
                    return;
                }

                if (!_slideSwapped)
                {
                    _slideSwapped = true;
                    ShowTarget(icons);
                    IconRect.anchoredPosition = new Vector2(0f, SlideTravel);
                }

                if (now < _slideEndsAt)
                {
                    float incoming = Mathf.InverseLerp(
                        _slideSwapAt,
                        _slideEndsAt,
                        now);
                    float offset = Mathf.Lerp(SlideTravel, 0f, incoming);
                    IconRect.anchoredPosition = new Vector2(0f, offset);
                    return;
                }

                _sliding = false;
                IconRect.anchoredPosition = Vector2.zero;
                IconRect.localScale = Vector3.one;
            }

            if (_spinning)
            {
                if (now >= _stopAt)
                {
                    _spinning = false;
                    ShowTarget(icons);
                    _settleUntil = now + SettleSeconds;
                }
                else
                {
                    int frame = Mathf.FloorToInt((now - _startedAt) / ReelFrameSeconds);
                    if (frame != _lastReelFrame)
                    {
                        _lastReelFrame = frame;
                        int effectIndex = Math.Abs(frame + Rect.GetSiblingIndex()) %
                            StageEffectSet.IndividualEffects.Count;
                        Icon.texture = icons.Get(StageEffectSet.IndividualEffects[effectIndex]);
                        Icon.color = new Color(1f, 1f, 1f, 0.72f);
                    }
                    float cycle = Mathf.Repeat(
                        (now - _startedAt) / ReelFrameSeconds,
                        1f);
                    float offset = Mathf.Lerp(22f, -22f, cycle);
                    IconRect.anchoredPosition = new Vector2(0f, offset);
                    float squash = 0.7f + 0.3f * Mathf.Abs(Mathf.Cos(cycle * Mathf.PI));
                    IconRect.localScale = new Vector3(1f, squash, 1f);
                    return;
                }
            }

            if (now < _settleUntil)
            {
                float t = 1f - ((_settleUntil - now) / SettleSeconds);
                float bounce = Mathf.Sin(t * Mathf.PI) * 7f * (1f - t);
                IconRect.anchoredPosition = new Vector2(0f, bounce);
                IconRect.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * Mathf.PI));
            }
            else
            {
                IconRect.anchoredPosition = Vector2.zero;
                IconRect.localScale = Vector3.one;
            }
        }

        private void ShowTarget(EventIconCatalog icons)
        {
            Border.color = EventPresentation.Color(_targetDanger);
            IconRect.anchoredPosition = Vector2.zero;
            IconRect.localScale = Vector3.one;
            Icon.color = Color.white;

            if (_targetLocked)
            {
                Icon.texture = icons.Lock;
                Label.text = "LOCKED";
                return;
            }
            if (_targetWaiting)
            {
                Icon.texture = icons.Get(StageEffect.None);
                Label.text = "NEXT";
                return;
            }
            if (_targetEffect == StageEffect.None)
            {
                Icon.texture = icons.Ready;
                Label.text = "READY";
                return;
            }

            Icon.texture = icons.Get(_targetEffect);
            Label.text = EventPresentation.Name(_targetEffect).ToUpperInvariant();
        }
    }
}
