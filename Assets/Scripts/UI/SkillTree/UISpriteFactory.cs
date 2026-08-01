using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill tree görselleri için runtime'da sprite üretir (daire, halka, yuvarlak dikdörtgen).
/// Projeye yeni sprite asset'i eklemeden düzgün görünüm sağlar, üretilenler cache'lenir.
/// </summary>
public static class UISpriteFactory
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>Dolu daire. Node arka planı için.</summary>
    public static Sprite Circle(int size = 128)
    {
        string key = $"circle_{size}";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = NewTexture(size);
        float r = size * 0.5f - 1f;
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Coverage(r - d)));
            }
        }

        return Finish(tex, key, Vector4.zero);
    }

    /// <summary>Halka (içi boş daire). Node state çerçevesi için.</summary>
    public static Sprite Ring(int size = 128, float thickness = 6f)
    {
        string key = $"ring_{size}_{thickness}";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = NewTexture(size);
        float outer = size * 0.5f - 1f;
        float inner = outer - thickness;
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Min(Coverage(outer - d), Coverage(d - inner));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        return Finish(tex, key, Vector4.zero);
    }

    /// <summary>9-slice yuvarlak dikdörtgen. Panel/tooltip arka planı için.</summary>
    public static Sprite RoundedRect(int radius = 16, int padding = 4)
    {
        string key = $"rrect_{radius}_{padding}";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        int size = radius * 2 + padding * 2;
        Texture2D tex = NewTexture(size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectDistance(x, y, size, radius);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Coverage(-d)));
            }
        }

        float b = radius + padding - 1;
        return Finish(tex, key, new Vector4(b, b, b, b));
    }

    /// <summary>
    /// Basit asma kilit ikonu: yuvarlak köşeli gövde, üstünde yarım halka askı, ortasında anahtar deliği.
    /// Ayrı bir sprite asset'i gerektirmesin diye çizilerek üretilir.
    /// </summary>
    public static Sprite Lock(int size = 96)
    {
        string key = $"lock_{size}";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = NewTexture(size);
        float s = size;

        Vector2 bodyMin = new Vector2(0.18f * s, 0.06f * s);
        Vector2 bodyMax = new Vector2(0.82f * s, 0.56f * s);
        float bodyRadius = 0.09f * s;

        Vector2 shackleCenter = new Vector2(0.5f * s, 0.56f * s);
        float shackleRadius = 0.20f * s;
        float shackleHalfWidth = 0.045f * s;

        Vector2 keyhole = new Vector2(0.5f * s, 0.33f * s);
        float keyholeRadius = 0.07f * s;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                float body = RoundedBoxDistance(p, bodyMin, bodyMax, bodyRadius);

                //askı sadece gövdenin üstünde kalan yarım halka
                float shackle = float.MaxValue;
                if (p.y >= shackleCenter.y)
                    shackle = Mathf.Abs(Vector2.Distance(p, shackleCenter) - shackleRadius) - shackleHalfWidth;

                float alpha = Coverage(-Mathf.Min(body, shackle));

                //anahtar deliği gövdeden oyulur
                float hole = Vector2.Distance(p, keyhole) - keyholeRadius;
                alpha = Mathf.Min(alpha, Coverage(hole));

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        return Finish(tex, key, Vector4.zero);
    }

    /// <summary>Yuvarlak köşeli dikdörtgenin işaretli mesafesi (içeride negatif).</summary>
    private static float RoundedBoxDistance(Vector2 p, Vector2 min, Vector2 max, float radius)
    {
        Vector2 center = (min + max) * 0.5f;
        Vector2 half = (max - min) * 0.5f - Vector2.one * radius;

        float dx = Mathf.Abs(p.x - center.x) - half.x;
        float dy = Mathf.Abs(p.y - center.y) - half.y;

        float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
    }

    /// <summary>Tekrarlanabilir kare desen. Image.Type.Tiled ile zemine serilir.</summary>
    public static Sprite GridTile(int size = 64, int thickness = 1)
    {
        string key = $"grid_{size}_{thickness}";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = NewTexture(size);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                //sol ve alt kenarda çizgi — yan yana dizilince sürekli ızgara olur
                bool line = x < thickness || y < thickness;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, line ? 1f : 0f));
            }
        }

        return Finish(tex, key, Vector4.zero);
    }

    /// <summary>İçi boş, 9-slice yuvarlak dikdörtgen. Çerçeve için.</summary>
    public static Sprite RoundedRectOutline(int radius = 18, int thickness = 3, int padding = 4)
    {
        string key = $"rrectline_{radius}_{thickness}_{padding}";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        int size = radius * 2 + padding * 2;
        Texture2D tex = NewTexture(size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectDistance(x, y, size, radius);
                //dış kenar ile iç kenar arasındaki şerit
                float a = Mathf.Min(Coverage(-d), Coverage(d + thickness));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        float b = radius + padding - 1;
        return Finish(tex, key, new Vector4(b, b, b, b));
    }

    /// <summary>Yuvarlak dikdörtgenin işaretli mesafe alanı (içeride negatif).</summary>
    private static float RoundedRectDistance(int x, int y, int size, float radius)
    {
        float px = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
        float py = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
        float dx = Mathf.Max(px, 0f);
        float dy = Mathf.Max(py, 0f);
        return Mathf.Sqrt(dx * dx + dy * dy) + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
    }

    /// <summary>
    /// Merkezde saydam, kenarlara doğru koyulaşan radyal maske.
    /// Geniş bir rect'e gerildiğinde elips vinyet olur — zemine derinlik verir.
    /// </summary>
    public static Sprite Vignette(int size = 256)
    {
        string key = $"vignette_{size}";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = NewTexture(size);
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        float maxDistance = c.magnitude;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / maxDistance;
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }

        return Finish(tex, key, Vector4.zero);
    }

    /// <summary>Düz beyaz 1x1. Çizgi/dolgu için.</summary>
    public static Sprite White()
    {
        string key = "white";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = NewTexture(4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, Color.white);

        return Finish(tex, key, Vector4.zero);
    }

    private static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    /// <summary>SDF değerini 1 piksellik yumuşak kenara çevirir (antialias).</summary>
    private static float Coverage(float signedDistance)
    {
        return Mathf.Clamp01(signedDistance + 0.5f);
    }

    private static Sprite Finish(Texture2D tex, string key, Vector4 border)
    {
        tex.Apply();
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );
        sprite.hideFlags = HideFlags.HideAndDontSave;
        cache[key] = sprite;
        return sprite;
    }
}
