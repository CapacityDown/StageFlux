using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class EventIconCatalog
{
    private const string EventResourcePrefix =
        "REPOJP.StagePhysicsEvents.Assets.EventIcons.Runtime.";
    private const string UiResourcePrefix =
        "REPOJP.StagePhysicsEvents.Assets.UI.Runtime.";

    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private Texture2D? _lockTexture;
    private Texture2D? _stopwatchTexture;
    private Texture2D? _readyTexture;
    private Texture2D? _panelTexture;
    private Texture2D? _roundedTexture;
    private Sprite? _panelSprite;
    private Sprite? _roundedSprite;

    internal Texture2D Get(StageEffect effect)
    {
        string name = effect == StageEffect.None ? "Waiting" : effect.ToString();
        if (_textures.TryGetValue(name, out Texture2D texture))
        {
            return texture;
        }

        texture = Load(EventResourcePrefix, name) ?? CreateFallback(name);
        _textures[name] = texture;
        return texture;
    }

    internal Texture2D Lock =>
        _lockTexture ??= Load(UiResourcePrefix, "Lock") ?? CreateLock();

    internal Texture2D Stopwatch =>
        _stopwatchTexture ??= Load(UiResourcePrefix, "Stopwatch") ?? CreateStopwatch();

    internal Texture2D Ready =>
        _readyTexture ??= Load(UiResourcePrefix, "Ready") ?? CreateReady();

    internal Sprite PanelSprite
    {
        get
        {
            if (_panelSprite != null)
            {
                return _panelSprite;
            }

            _panelTexture = Load(UiResourcePrefix, "Panel") ??
                CreateSolidPanelTexture();
            float panelBorder = Mathf.Min(
                48f,
                Mathf.Min(_panelTexture.width, _panelTexture.height) * 0.2f);
            _panelSprite = Sprite.Create(
                _panelTexture,
                new Rect(0f, 0f, _panelTexture.width, _panelTexture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(panelBorder, panelBorder, panelBorder, panelBorder));
            _panelSprite.name = "StageFlux_GeneratedPanel";
            _panelSprite.hideFlags = HideFlags.HideAndDontSave;
            return _panelSprite;
        }
    }

    internal Sprite RoundedSprite
    {
        get
        {
            if (_roundedSprite != null)
            {
                return _roundedSprite;
            }

            _roundedTexture = CreateRoundedTexture();
            _roundedSprite = Sprite.Create(
                _roundedTexture,
                new Rect(0f, 0f, _roundedTexture.width, _roundedTexture.height),
                new Vector2(0.5f, 0.5f),
                32f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(12f, 12f, 12f, 12f));
            _roundedSprite.name = "StageFlux_Rounded";
            _roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return _roundedSprite;
        }
    }

    internal void Dispose()
    {
        foreach (Texture2D texture in _textures.Values)
        {
            UnityEngine.Object.Destroy(texture);
        }
        _textures.Clear();
        if (_lockTexture != null)
        {
            UnityEngine.Object.Destroy(_lockTexture);
        }
        if (_stopwatchTexture != null)
        {
            UnityEngine.Object.Destroy(_stopwatchTexture);
        }
        if (_readyTexture != null)
        {
            UnityEngine.Object.Destroy(_readyTexture);
        }
        if (_panelSprite != null)
        {
            UnityEngine.Object.Destroy(_panelSprite);
        }
        if (_panelTexture != null)
        {
            UnityEngine.Object.Destroy(_panelTexture);
        }
        if (_roundedSprite != null)
        {
            UnityEngine.Object.Destroy(_roundedSprite);
        }
        if (_roundedTexture != null)
        {
            UnityEngine.Object.Destroy(_roundedTexture);
        }
        _lockTexture = null;
        _stopwatchTexture = null;
        _readyTexture = null;
        _panelSprite = null;
        _panelTexture = null;
        _roundedSprite = null;
        _roundedTexture = null;
    }

    private static Texture2D? Load(string prefix, string name)
    {
        try
        {
            Assembly assembly = typeof(EventIconCatalog).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream(prefix + name + ".png");
            if (stream == null)
            {
                return null;
            }
            byte[] bytes = new byte[stream.Length];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read <= 0)
                {
                    break;
                }
                offset += read;
            }
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"StageFlux_{name}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture.LoadImage(bytes) ? texture : null;
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Could not load HUD icon '{name}': {exception.Message}");
            return null;
        }
    }

    private static Texture2D CreateFallback(string name)
    {
        Texture2D texture = NewTexture(96, 96, $"StageFlux_Fallback_{name}");
        Color clear = new(0f, 0f, 0f, 0f);
        Color ink = new(0.7f, 0.72f, 0.68f, 1f);
        Color[] pixels = new Color[96 * 96];
        Array.Fill(pixels, clear);
        if (name == nameof(StageEffect.EnemySpeedUp) ||
            name == nameof(StageEffect.EnemySpeedDown))
        {
            bool upward = name == nameof(StageEffect.EnemySpeedUp);
            for (int y = 22; y < 75; y++)
            {
                for (int x = 43; x < 53; x++)
                {
                    pixels[y * 96 + x] = ink;
                }
            }
            for (int step = 0; step < 25; step++)
            {
                int y = upward ? 22 + step : 73 - step;
                for (int x = 48 - step; x <= 48 + step; x++)
                {
                    if (x >= 10 && x < 86)
                    {
                        pixels[y * 96 + x] = ink;
                    }
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
        for (int y = 18; y < 78; y++)
        {
            for (int x = 42; x < 54; x++)
            {
                pixels[y * 96 + x] = ink;
            }
        }
        for (int y = 42; y < 54; y++)
        {
            for (int x = 18; x < 78; x++)
            {
                pixels[y * 96 + x] = ink;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateLock()
    {
        const int size = 96;
        Texture2D texture = NewTexture(size, size, "StageFlux_Lock");
        Color[] pixels = new Color[size * size];
        Array.Fill(pixels, new Color(0f, 0f, 0f, 0f));
        Color ink = EventPresentation.Color(EventDanger.Locked);

        for (int y = 37; y < 80; y++)
        {
            for (int x = 22; x < 74; x++)
            {
                pixels[y * size + x] = ink;
            }
        }
        for (int y = 14; y < 50; y++)
        {
            for (int x = 28; x < 68; x++)
            {
                float dx = x - 48f;
                float dy = y - 37f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                if (radius >= 15f && radius <= 21f && y <= 40)
                {
                    pixels[y * size + x] = ink;
                }
            }
        }
        for (int y = 50; y < 69; y++)
        {
            for (int x = 44; x < 52; x++)
            {
                pixels[y * size + x] = Color.black;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateStopwatch()
    {
        const int size = 128;
        Texture2D texture = NewTexture(size, size, "StageFlux_Stopwatch");
        Color[] pixels = new Color[size * size];
        Array.Fill(pixels, new Color(0f, 0f, 0f, 0f));
        Color ink = new(0.72f, 0.74f, 0.7f, 1f);
        Vector2 center = new(64f, 70f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), center);
                bool ring = radius >= 47f && radius <= 51f;
                bool face = radius < 47f;
                if (face)
                {
                    pixels[y * size + x] = new Color(0.025f, 0.028f, 0.027f, 0.94f);
                }
                if (ring)
                {
                    pixels[y * size + x] = ink;
                }
            }
        }
        for (int y = 5; y < 21; y++)
        {
            for (int x = 57; x < 71; x++)
            {
                pixels[y * size + x] = ink;
            }
        }
        for (int y = 0; y < 9; y++)
        {
            for (int x = 50; x < 78; x++)
            {
                pixels[y * size + x] = ink;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateReady()
    {
        const int size = 96;
        Texture2D texture = NewTexture(size, size, "StageFlux_Ready");
        Color[] pixels = new Color[size * size];
        Array.Fill(pixels, new Color(0f, 0f, 0f, 0f));
        Color ink = new(0.9f, 0.88f, 0.76f, 1f);
        for (int dot = 0; dot < 3; dot++)
        {
            Vector2 center = new(30f + dot * 18f, 48f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (Vector2.Distance(new Vector2(x, y), center) <= 4f)
                    {
                        pixels[y * size + x] = ink;
                    }
                }
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateSolidPanelTexture()
    {
        Texture2D texture = NewTexture(32, 32, "StageFlux_PanelFallback");
        Color[] pixels = new Color[32 * 32];
        Array.Fill(pixels, new Color(0.025f, 0.027f, 0.026f, 0.96f));
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateRoundedTexture()
    {
        const int size = 32;
        const float radius = 7f;
        Texture2D texture = NewTexture(size, size, "StageFlux_RoundedTexture");
        Color[] pixels = new Color[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        Vector2 half = new(center.x - radius, center.y - radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new(
                    Mathf.Max(Mathf.Abs(x - center.x) - half.x, 0f),
                    Mathf.Max(Mathf.Abs(y - center.y) - half.y, 0f));
                float alpha = Mathf.Clamp01(radius + 0.5f - point.magnitude);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D NewTexture(int width, int height, string name) =>
        new(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
}
