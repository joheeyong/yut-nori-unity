using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Static utility class for creating Unity UI elements procedurally.
/// All methods create GameObjects as children of the given parent Transform.
/// </summary>
public static class UIHelper
{
    // -----------------------------------------------------------------------
    //  RectTransform / container
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a bare RectTransform child on parent with given size and anchored position.
    /// Anchor and pivot are both set to (0.5, 0.5) (centered).
    /// </summary>
    public static RectTransform CreateRect(Transform parent, string name, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }

    // -----------------------------------------------------------------------
    //  Image
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a solid-color Image child on parent.
    /// Uses a 1×1 white texture tinted to the requested colour.
    /// </summary>
    public static Image CreateImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var rt  = CreateRect(parent, name, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite        = CreateWhiteSquareSprite();
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }

    // -----------------------------------------------------------------------
    //  Text
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a TextMeshProUGUI child on parent.
    /// </summary>
    public static TextMeshProUGUI CreateText(
        Transform parent, string name, string text,
        int fontSize, Color color,
        Vector2 size, Vector2 pos,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt  = CreateRect(parent, name, size, pos);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    // -----------------------------------------------------------------------
    //  Button
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a Button with a background Image and a TextMeshProUGUI label.
    /// Returns the Button component.
    /// </summary>
    public static Button CreateButton(
        Transform parent, string name, string label,
        int fontSize, Vector2 size, Vector2 pos,
        Color bgColor, Color textColor)
    {
        var rt  = CreateRect(parent, name, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = CreateRoundedRect(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 12);
        img.color  = bgColor;
        img.type   = Image.Type.Sliced;

        var btn = rt.gameObject.AddComponent<Button>();

        var colors             = btn.colors;
        colors.normalColor      = bgColor;
        colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
        colors.pressedColor     = Color.Lerp(bgColor, Color.black, 0.25f);
        colors.disabledColor    = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        btn.colors              = colors;

        // Label child
        CreateText(rt, "Label", label, fontSize, textColor,
                   size, Vector2.zero, TextAlignmentOptions.Center);

        return btn;
    }

    // -----------------------------------------------------------------------
    //  Sprites
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates an anti-aliased circle sprite with the given pixel resolution.
    /// The sprite is white; tint it via Image.color.
    /// </summary>
    public static Sprite CreateCircleSprite(int resolution = 64)
    {
        var tex  = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float ctr = resolution / 2f;
        float r   = ctr - 1.5f;
        for (int x = 0; x < resolution; x++)
        for (int y = 0; y < resolution; y++)
        {
            float dist  = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(ctr, ctr));
            float alpha = Mathf.Clamp01((r - dist) / 1.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution),
                             Vector2.one * 0.5f, resolution);
    }

    /// <summary>
    /// Creates a line Image between two canvas-local positions.
    /// The Image is positioned and rotated to span from→to.
    /// </summary>
    public static Image CreateLine(Transform parent, Vector2 from, Vector2 to, float thickness, Color color)
    {
        float dist  = Vector2.Distance(from, to);
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

        var rt  = CreateRect(parent, $"Line_{from.x:0}_{from.y:0}", new Vector2(dist, thickness), (from + to) * 0.5f);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite        = CreateWhiteSquareSprite();
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Creates a rounded-rectangle sprite of the given pixel dimensions and corner radius.
    /// </summary>
    public static Sprite CreateRoundedRect(int w, int h, int radius)
    {
        radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float alpha = RoundedRectAlpha(x + 0.5f, y + 0.5f, w, h, radius);
            pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply();

        // Border is 1/4 of the shorter dimension; keep it Sliced-friendly
        int border = radius;
        return Sprite.Create(tex, new Rect(0, 0, w, h),
                             Vector2.one * 0.5f, 100f,
                             0, SpriteMeshType.FullRect,
                             new Vector4(border, border, border, border));
    }

    /// <summary>
    /// Coroutine that animates a float value from→to over duration seconds.
    /// Optionally supply an AnimationCurve; defaults to linear.
    /// </summary>
    public static IEnumerator AnimateFloat(
        float from, float to, float duration,
        Action<float> onUpdate,
        AnimationCurve curve = null)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t   = Mathf.Clamp01(elapsed / duration);
            float val = curve != null ? Mathf.Lerp(from, to, curve.Evaluate(t))
                                      : Mathf.Lerp(from, to, t);
            onUpdate(val);
            yield return null;
        }
        onUpdate(to);
    }

    /// <summary>
    /// Coroutine that animates a Vector2 value from→to over duration seconds (linear).
    /// </summary>
    public static IEnumerator AnimateV2(
        Vector2 from, Vector2 to, float duration,
        Action<Vector2> onUpdate)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            onUpdate(Vector2.Lerp(from, to, t));
            yield return null;
        }
        onUpdate(to);
    }

    // -----------------------------------------------------------------------
    //  Internal helpers
    // -----------------------------------------------------------------------

    static Sprite CreateWhiteSquareSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
    }

    static float RoundedRectAlpha(float px, float py, int w, int h, int r)
    {
        // Distance from the pixel to the nearest point inside the rounded rect
        float cx = Mathf.Clamp(px, r, w - r);
        float cy = Mathf.Clamp(py, r, h - r);
        float dx = px - cx;
        float dy = py - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01((r - dist) / 1.0f);
    }
}
