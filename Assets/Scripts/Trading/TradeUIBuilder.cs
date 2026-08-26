using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// a11 alt ağacının çalışma anında kurduğu panellerin ortak legacy-UI tuğlaları.
///
/// Skill ağacının UISpriteFactory'si burada kullanılmaz: bu paneller CandlestickChart'ın
/// kendi legacy (UnityEngine.UI) dünyasının içine oturuyor ve iki görsel dil aynı ekranda
/// çakışıyordu. Üç panel de (trade şeridi, coin kartı, tahmin piyasası) aynı tuğlaları
/// kullansın diye tek yerde toplandı.
/// </summary>
public static class TradeUIBuilder
{
    public static readonly Color Ink = new Color(0.92f, 0.92f, 0.94f);
    public static readonly Color InkSoft = new Color(0.70f, 0.72f, 0.78f);
    public static readonly Color Surface = new Color(0.09f, 0.09f, 0.11f, 0.96f);
    public static readonly Color SurfaceAlt = new Color(0.16f, 0.16f, 0.19f);
    public static readonly Color Profit = new Color(0.18f, 0.8f, 0.34f);
    public static readonly Color Loss = new Color(0.9f, 0.22f, 0.21f);
    public static readonly Color Warn = new Color(1f, 0.85f, 0.4f);

    private static Font cachedFont;

    public static Font Font
    {
        get
        {
            if (cachedFont == null)
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return cachedFont;
        }
    }

    /// <summary>Renkli bir dikdörtgen (panel, kart, çubuk zemini).</summary>
    public static GameObject Panel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;

        return go;
    }

    public static Text Label(string name, Transform parent, int size, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = Font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    /// <summary>
    /// RectTransform'u tek çağrıda yerleştirir. anchor hem anchorMin/Max hem pivot olur;
    /// paneller sabit düzenli olduğu için bu yeterli ve okunaklı.
    /// </summary>
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    public static Button Button(string name, Transform parent, string label, Color color,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = Panel(name, parent, color);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        Text text = Label("Label", go.transform, 15, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);
        text.text = label;

        return button;
    }

    public static InputField NumberField(string name, Transform parent, string startValue)
    {
        GameObject go = Panel(name, parent, SurfaceAlt);

        Text text = Label("Text", go.transform, 15, TextAnchor.MiddleLeft, Color.white);
        Stretch(text.rectTransform, 6f);
        text.supportRichText = false;

        InputField field = go.AddComponent<InputField>();
        field.textComponent = text;
        field.contentType = InputField.ContentType.DecimalNumber;
        field.targetGraphic = go.GetComponent<Image>();
        field.text = startValue;

        return field;
    }

    /// <summary>İçi dolan yatay çubuk (hype göstergesi gibi). Dönen Image'ın fillAmount'u sürülür.</summary>
    public static Image Meter(string name, Transform parent, Color fillColor)
    {
        GameObject background = Panel(name, parent, new Color(0.22f, 0.22f, 0.26f));

        GameObject fillObject = Panel("Fill", background.transform, fillColor);
        Image fill = fillObject.GetComponent<Image>();
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;

        return fill;
    }

    /// <summary>Sahnedeki ilk Canvas. Paneller buraya kurulur.</summary>
    public static Canvas FindCanvas()
    {
        return Object.FindFirstObjectByType<Canvas>();
    }
}
