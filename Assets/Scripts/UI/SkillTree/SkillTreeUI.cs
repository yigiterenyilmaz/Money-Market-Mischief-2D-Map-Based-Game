using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skill tree görsellerini koddan kurmak için küçük yardımcılar.
/// </summary>
public static class SkillTreeUI
{
    public static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : LayerMask.NameToLayer("UI");
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero)
            image.type = Image.Type.Sliced;
        return image;
    }

    private static Material textMaterial;

    public static TextMeshProUGUI NewText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;

        Material material = GetTextMaterial(text.font);
        if (material != null)
            text.fontSharedMaterial = material;

        return text;
    }

    /// <summary>
    /// Skill tree yazıları için ayrı bir paylaşılan materyal.
    /// Projenin varsayılan TMP materyalinin _FaceColor'ı mor; TMP bunu text.color ile ÇARPTIĞI için
    /// atadığımız renk ne olursa olsun yazılar mora dönüyordu. O materyal 10'dan fazla prefab
    /// tarafından kullanıldığı için değiştirilmiyor — bunun yerine beyaz face color'lı kopyası kullanılıyor.
    /// Tek kopya paylaşıldığından yazılar hâlâ tek batch'te çizilir.
    /// </summary>
    private static Material GetTextMaterial(TMP_FontAsset font)
    {
        if (textMaterial != null) return textMaterial;

        TMP_FontAsset source = font != null ? font : TMP_Settings.defaultFontAsset;
        if (source == null || source.material == null) return null;

        textMaterial = new Material(source.material) { hideFlags = HideFlags.HideAndDontSave };

        if (textMaterial.HasProperty(ShaderUtilities.ID_FaceColor))
            textMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color.white);

        return textMaterial;
    }

    /// <summary>Rect'i ebeveynine yayar, isteğe bağlı iç boşlukla.</summary>
    public static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    /// <summary>10.000 → "10 B", 1.500.000 → "1,5 M" gibi kısa para biçimi.</summary>
    public static string FormatMoney(float amount)
    {
        float abs = Mathf.Abs(amount);

        if (abs >= 1_000_000_000f) return (amount / 1_000_000_000f).ToString("0.#") + " Mr";
        if (abs >= 1_000_000f) return (amount / 1_000_000f).ToString("0.#") + " M";
        if (abs >= 1_000f) return (amount / 1_000f).ToString("0.#") + " B";
        return amount.ToString("0");
    }
}
