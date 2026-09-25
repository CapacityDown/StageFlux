using System;
using System.Collections.Generic;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REPOJP.StagePhysicsEvents;

internal sealed class EventHudLayout
{
    internal string Anchor = "BottomRight", Alignment = "Right", Style = "Graphical", Direction = "Vertical";
    internal int X, Y, Scale = 70, Opacity = 50;
    internal bool Enabled = true;
    internal void Refresh(StagePhysicsConfig c)
    {
        Anchor = c.HudAnchor.Value; Alignment = c.HudAlignment.Value; Style = c.HudStyle.Value; Direction = c.HudLayoutDirection.Value;
        X = c.HudOffsetX.Value; Y = c.HudOffsetY.Value; Scale = c.HudScalePercent.Value; Opacity = c.HudBackgroundOpacityPercent.Value; Enabled = c.HudEnabled.Value;
    }
    internal void Save(StagePhysicsConfig c)
    {
        c.HudAnchor.Value = Anchor; c.HudAlignment.Value = Alignment; c.HudStyle.Value = Style; c.HudLayoutDirection.Value = Direction;
        c.HudOffsetX.Value = X; c.HudOffsetY.Value = Y; c.HudScalePercent.Value = Scale; c.HudBackgroundOpacityPercent.Value = Opacity; c.HudEnabled.Value = Enabled;
    }
}

// Edits a draft rendered by the actual HUD. Cancel never changes configuration.
internal sealed class EventHudEditor : MonoBehaviour
{
    internal static EventHudEditor? Instance { get; private set; }
    internal static int ClosedFrame { get; private set; } = -1;
    private StagePhysicsConfig _config = null!;
    private REPOPopupPage _page = null!;
    private EventHudLayout _draft = new();
    private StagePhysicsEventHud _preview = null!;
    private RectTransform _canvas = null!, _toolbar = null!, _pointer = null!;
    private TMP_FontAsset _font = null!;
    private TextMeshProUGUI _status = null!;
    private CanvasGroup? _pageGroup;
    private float _oldAlpha;
    private bool _oldInteractable, _oldRaycasts, _addedGroup, _closed, _dragging;
    private int _openedFrame, _dragX, _dragY;
    private Vector2 _dragStart;
    private readonly List<(Behaviour Component, bool Enabled)> _disabled = new();
    private readonly List<(RectTransform Rect, Image Background, TextMeshProUGUI Label, Func<string> Text, Action Click)> _buttons = new();

    internal static void Open(StagePhysicsConfig config, REPOPopupPage page, TMP_FontAsset font)
    {
        if (Instance != null) return;
        EventHudEditor editor = new GameObject("StageFlux_HudEditor").AddComponent<EventHudEditor>();
        Instance = editor;
        try { editor.Initialize(config, page, font); }
        catch { editor.Close(false); throw; }
    }

    private void Initialize(StagePhysicsConfig config, REPOPopupPage page, TMP_FontAsset font)
    {
        _config = config; _page = page; _font = font; _draft.Refresh(config); _openedFrame = Time.frameCount;
        _pageGroup = page.GetComponent<CanvasGroup>(); _addedGroup = _pageGroup == null;
        if (_addedGroup) _pageGroup = page.gameObject.AddComponent<CanvasGroup>();
        _oldAlpha = _pageGroup!.alpha; _oldInteractable = _pageGroup.interactable; _oldRaycasts = _pageGroup.blocksRaycasts;
        _pageGroup.alpha = 0; _pageGroup.interactable = false; _pageGroup.blocksRaycasts = false;
        foreach (Behaviour component in page.GetComponentsInChildren<Behaviour>(true))
            if (component is MenuButton || component is Button || component is MenuScrollBox)
            { _disabled.Add((component, component.enabled)); component.enabled = false; }
        GameObject overlay = new("Editor Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        overlay.transform.SetParent(transform, false);
        Canvas canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960, 540);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        _canvas = (RectTransform)overlay.transform;
        RectTransform background = Box("Background", _canvas, new Color(0.025f, 0.035f, 0.055f, 0.94f));
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.sizeDelta = Vector2.zero;
        _preview = overlay.AddComponent<StagePhysicsEventHud>(); _preview.InitializePreview(config, _draft, _canvas, font);
        _toolbar = Box("Toolbar", _canvas, new Color(0.06f, 0.09f, 0.13f, 0.97f));
        _toolbar.anchorMin = new Vector2(0, 1); _toolbar.anchorMax = Vector2.one; _toolbar.pivot = new Vector2(0.5f, 1); _toolbar.sizeDelta = new Vector2(0, 188);
        Label(_toolbar, "HUD EDITOR - drag the sample HUD to move it; Save to apply", new Vector2(16, -6), new Vector2(920, 30), 18);
        Add(16, 40, 220, () => "Style: " + _draft.Style, () => { _draft.Style = Next(_draft.Style, "Graphical", "Classic"); Changed(); });
        Add(250, 40, 220, () => "Layout: " + _draft.Direction, () => { _draft.Direction = Next(_draft.Direction, "Vertical", "Horizontal"); Changed(); });
        Add(484, 40, 230, () => "Anchor: " + _draft.Anchor, () =>
        {
            _draft.Anchor = Next(_draft.Anchor, "BottomLeft", "BottomCenter", "BottomRight", "MiddleLeft", "MiddleCenter", "MiddleRight", "TopLeft", "TopCenter", "TopRight");
            _draft.X = _draft.Y = 0; Changed();
        });
        Add(728, 40, 216, () => "HUD: " + (_draft.Enabled ? "ON" : "OFF"), () => _draft.Enabled = !_draft.Enabled);
        Pair(16, 76, "Scale", () => _draft.Scale, delta => { _draft.Scale = Mathf.Clamp(_draft.Scale + delta * 5, 50, 200); Changed(); });
        Pair(250, 76, "Opacity", () => _draft.Opacity, delta => { _draft.Opacity = Mathf.Clamp(_draft.Opacity + delta * 5, 0, 100); Changed(); });
        Add(484, 76, 230, () => "Align: " + _draft.Alignment, () => { _draft.Alignment = Next(_draft.Alignment, "Left", "Center", "Right"); Changed(); });
        Add(728, 76, 216, () => "RESET LAYOUT", () => { var defaults = new EventHudLayout(); Copy(defaults, _draft); Changed(); });
        Pair(16, 112, "X", () => _draft.X, delta => { _draft.X = Mathf.Clamp(_draft.X + delta * 5, -3840, 3840); Changed(); });
        Pair(250, 112, "Y", () => _draft.Y, delta => { _draft.Y = Mathf.Clamp(_draft.Y + delta * 5, -2160, 2160); Changed(); });
        Add(484, 112, 230, () => "SAVE", () => Close(true));
        Add(728, 112, 216, () => "CANCEL", () => Close(false));
        _status = Label(_toolbar, "Sample only - no effects are started. Escape cancels. Alignment applies to Classic text.", new Vector2(16, -151), new Vector2(928, 30), 15);
        GameObject pointer = new("Editor Pointer", typeof(RectTransform), typeof(EventEditorPointer), typeof(Outline));
        pointer.transform.SetParent(_canvas, false); _pointer = (RectTransform)pointer.transform;
        _pointer.anchorMin = _pointer.anchorMax = new Vector2(0.5f, 0.5f); _pointer.pivot = new Vector2(0, 1); _pointer.sizeDelta = new Vector2(20, 28);
        pointer.GetComponent<EventEditorPointer>().raycastTarget = false; pointer.GetComponent<EventEditorPointer>().color = new Color(1, 0.65f, 0);
        pointer.GetComponent<Outline>().effectColor = Color.black; pointer.GetComponent<Outline>().effectDistance = new Vector2(1, -1);
        Canvas.ForceUpdateCanvases(); Changed(); _toolbar.SetAsLastSibling(); _pointer.SetAsLastSibling();
    }

    private static void Copy(EventHudLayout source, EventHudLayout destination)
    {
        destination.Anchor = source.Anchor; destination.Alignment = source.Alignment; destination.Style = source.Style; destination.Direction = source.Direction;
        destination.X = source.X; destination.Y = source.Y; destination.Scale = source.Scale; destination.Opacity = source.Opacity; destination.Enabled = source.Enabled;
    }
    private static string Next(string value, params string[] options) => options[(Array.IndexOf(options, value) + 1) % options.Length];
    private void Changed() { _preview.RefreshPreview(); _preview.ClampPreview(); }
    private void Pair(float x, float y, string name, Func<int> value, Action<int> adjust)
    {
        Add(x, y, 36, () => "-", () => adjust(-1));
        Add(x + 40, y, 136, () => name + ": " + value(), () => { });
        Add(x + 180, y, 40, () => "+", () => adjust(1));
    }
    private void Add(float x, float y, float width, Func<string> title, Action click)
    {
        RectTransform rect = Box("Editor Button", _toolbar, new Color(0.15f, 0.20f, 0.27f));
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, 28);
        TextMeshProUGUI label = Label(rect, title(), new Vector2(4, -1), new Vector2(width - 8, 26), 16);
        label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = 16;
        _buttons.Add((rect, rect.GetComponent<Image>(), label, title, click));
    }
    private static RectTransform Box(string name, Transform parent, Color color)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return (RectTransform)obj.transform;
    }
    private TextMeshProUGUI Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        GameObject obj = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<TextMeshProUGUI>(); text.font = _font; text.fontSize = fontSize; text.text = value; text.color = Color.white;
        text.richText = false; text.raycastTarget = false; text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1); text.rectTransform.pivot = new Vector2(0, 1);
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
        return text;
    }

    private void Update()
    {
        if (_closed) return;
        if (_page == null || !_page.gameObject.activeInHierarchy) { Close(false); return; }
        if (!Application.isFocused) { _dragging = false; return; }
        SemiFunc.CursorUnlock(0.1f);
        if (Time.frameCount <= _openedFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(false); return; }
        Vector2 pointer = Input.mousePosition;
        foreach (var button in _buttons)
        {
            bool hover = RectTransformUtility.RectangleContainsScreenPoint(button.Rect, pointer);
            button.Background.color = hover ? new Color(0.25f, 0.38f, 0.48f) : new Color(0.15f, 0.20f, 0.27f);
            button.Label.text = button.Text();
            if (hover && Input.GetMouseButtonDown(0)) { button.Click(); return; }
        }
        if (Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(_toolbar, pointer) &&
            _preview.PreviewRect != null && RectTransformUtility.RectangleContainsScreenPoint(_preview.PreviewRect, pointer))
        { _dragging = true; _dragStart = pointer; _dragX = _draft.X; _dragY = _draft.Y; }
        if (!Input.GetMouseButton(0)) _dragging = false;
        if (_dragging) _preview.MovePreview(pointer - _dragStart, _dragX, _dragY);
    }
    private void LateUpdate()
    {
        if (_closed || _pointer == null) return;
        _pointer.gameObject.SetActive(Application.isFocused);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, Input.mousePosition, null, out Vector2 point);
        _pointer.anchoredPosition = point; _pointer.SetAsLastSibling();
    }

    internal void Close(bool save)
    {
        if (_closed) return;
        if (save)
        {
            EventHudLayout before = new(); before.Refresh(_config);
            var file = StagePhysicsEventsPlugin.Instance.Config;
            bool automatic = file.SaveOnConfigSet; file.SaveOnConfigSet = false;
            try { _preview.ClampPreview(); _draft.Save(_config); file.Save(); }
            catch (Exception exception)
            {
                before.Save(_config); _status.text = "Could not save. Retry or cancel.";
                StagePhysicsEventsPlugin.ModLogger.LogError(exception); return;
            }
            finally { file.SaveOnConfigSet = automatic; }
        }
        _closed = true; ClosedFrame = Time.frameCount; if (Instance == this) Instance = null;
        Restore(); gameObject.SetActive(false); Destroy(gameObject);
    }
    private void Restore()
    {
        foreach (var item in _disabled) if (item.Component != null) item.Component.enabled = item.Enabled;
        _disabled.Clear();
        if (_pageGroup != null)
        {
            if (_addedGroup) Destroy(_pageGroup);
            else { _pageGroup.alpha = _oldAlpha; _pageGroup.interactable = _oldInteractable; _pageGroup.blocksRaycasts = _oldRaycasts; }
        }
        _pageGroup = null;
    }
    private void OnDestroy() { Restore(); if (Instance == this) Instance = null; }
}

internal sealed class EventEditorPointer : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        foreach (Vector2 point in new[] { new Vector2(0, 0), new Vector2(0, -24), new Vector2(6, -18), new Vector2(11, -28), new Vector2(15, -26), new Vector2(10, -16), new Vector2(20, -16) })
            mesh.AddVert(point, color, Vector2.zero);
        mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(0, 2, 5); mesh.AddTriangle(0, 5, 6); mesh.AddTriangle(2, 3, 4); mesh.AddTriangle(2, 4, 5);
    }
}
