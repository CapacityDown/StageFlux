using System;
using MenuLib.MonoBehaviors;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

// Resolves actual menu geometry after both mods have inserted their elements.
// No RoleShuffle reference or dependency/load-order cycle is required.
internal sealed class EventMenuButton : MonoBehaviour
{
    private REPOButton _button = null!;
    private Transform _parent = null!;
    private float _nextProbe;
    internal void Initialize(REPOButton button, Transform parent)
    { _button = button; _parent = parent; Place(); }

    private void LateUpdate()
    {
        if (Time.unscaledTime < _nextProbe) return;
        _nextProbe = Time.unscaledTime + 0.25f;
        Place();
    }

    private void Place()
    {
        if (_button == null || _parent is not RectTransform parent) return;
        float top = 36f;
        foreach (REPOButton candidate in _parent.GetComponentsInChildren<REPOButton>(true))
        {
            if (candidate == _button || candidate.transform.parent != _button.transform.parent ||
                !candidate.gameObject.activeSelf || candidate.labelTMP == null ||
                !string.Equals(candidate.labelTMP.text.Trim(), "Roles", StringComparison.OrdinalIgnoreCase)) continue;
            Vector3[] corners = new Vector3[4];
            candidate.rectTransform.GetWorldCorners(corners);
            top = Mathf.Max(top, parent.rect.yMax - parent.InverseTransformPoint(corners[0]).y + 12f);
        }
        Vector2 size = _button.labelTMP.GetPreferredValues("Events");
        size.y = Mathf.Max(26f, size.y);
        _button.overrideButtonSize = size;
        _button.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 36, size.x);
        _button.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, top, size.y);
    }
}
