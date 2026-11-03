namespace UnityEngine
{
    public struct Vector2 { public float y; }
    public struct Rect { public float height; }
    public class RectTransform { public Vector2 sizeDelta; public Rect rect; }
    public static class Mathf
    {
        public static float Sign(float value) => value >= 0 ? 1 : -1;
        public static float Abs(float value) => Math.Abs(value);
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.00001f;
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }
}
namespace UnityEngine.InputSystem
{
    public static class InputSystem
    {
        internal static float scrollWheelDeltaPerTick { get; set; } = 1f;
    }
}
public class MenuScrollBox
{
    public float scrollWheelStep = 15;
    public float scrollHeight = 1000;
    public float scrollerStartPosition = 1000;
    public float scrollerEndPosition = 0;
    public UnityEngine.RectTransform scrollHandle = new() { sizeDelta = new() { y = 40 } };
    public UnityEngine.RectTransform scrollBarBackground = new() { rect = new() { height = 1000 } };
}
public static class SemiFunc
{
    public static float Scroll;
    public static float InputScrollY() => Scroll;
}
// Model of the other menu's independently scoped helper. No RoleShuffle DLL
// or compile-time reference is needed to test both Harmony ordering cases.
public static class PeerMenuScroll
{
    public static MenuScrollBox Box = new();
    public static float WheelStep(float nativeStep, MenuScrollBox box, float start, float end)
    {
        if (!ReferenceEquals(box, Box)) return nativeStep;
        return SemiFunc.Scroll / UnityEngine.InputSystem.InputSystem.scrollWheelDeltaPerTick *
            97.5f * (box.scrollBarBackground.rect.height - box.scrollHandle.sizeDelta.y) / Math.Abs(start - end);
    }
}
namespace REPOJP.StagePhysicsEvents
{
    internal static class EventMenu
    {
        internal static MenuScrollBox Box = new();
        internal static float LineHeight = 32.5f;
        internal static bool Open = true;
        internal static bool TryGetScrollSettings(MenuScrollBox box, out float lineHeight)
        {
            lineHeight = LineHeight;
            return Open && ReferenceEquals(Box, box);
        }
    }
}
