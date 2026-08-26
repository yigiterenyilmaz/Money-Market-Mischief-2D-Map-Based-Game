using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Borsa istihbarat şeridi — insider ipuçlarını ve manipülasyon sonuçlarını ekranın üstünde
/// kısa süreliğine gösterir (StockMarketSystem.OnMarketTip).
///
/// Kendi arayüzünü runtime'da kurar (projede prefab yok) ve SkillTreeUI/UISpriteFactory
/// yardımcılarını kullanır — böylece diğer ekranlarla aynı görsel dili paylaşır.
///
/// Sahneye elle eklenmesi gerekir; herhangi bir Canvas'ın altına takılabilir.
/// </summary>
public class MarketIntelUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Şeridin bineceği Canvas. Boş bırakılırsa ebeveynlerde, yoksa sahnede aranır.")]
    public Canvas canvas;

    [Header("Görünüm")]
    public float width = 620f;
    public float height = 54f;
    [Tooltip("Ekranın üstünden boşluk (piksel).")]
    public float topOffset = 90f;
    public float fadeSeconds = 0.35f;

    private static readonly Color bullish = new Color(0.18f, 0.8f, 0.34f);
    private static readonly Color bearish = new Color(0.9f, 0.22f, 0.21f);
    private static readonly Color neutral = new Color(0.85f, 0.82f, 0.72f);

    private CanvasGroup group;
    private RectTransform rect;
    private TextMeshProUGUI label;
    private Image accent;
    private float hideAtTime;

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        StockMarketSystem.OnMarketTip += Show;
    }

    private void OnDisable()
    {
        StockMarketSystem.OnMarketTip -= Show;
    }

    private void Build()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[MarketIntelUI] Canvas bulunamadı — istihbarat şeridi kurulamadı.");
            enabled = false;
            return;
        }

        Image background = SkillTreeUI.NewImage("MarketIntel", canvas.transform, UISpriteFactory.RoundedRect(12, 6));
        background.type = Image.Type.Sliced;
        background.color = new Color(0.06f, 0.07f, 0.09f, 0.92f);
        background.raycastTarget = false; //şerit tıklamaları yutmasın, altındaki harita çalışsın

        rect = background.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -topOffset);
        rect.sizeDelta = new Vector2(width, height);

        //sol kenardaki renk çubuğu: yönü metni okumadan da belli eder
        accent = SkillTreeUI.NewImage("Accent", rect, UISpriteFactory.White());
        accent.raycastTarget = false;
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.offsetMin = new Vector2(6f, 8f);
        accentRect.offsetMax = new Vector2(11f, -8f);

        label = SkillTreeUI.NewText("Text", rect, 20f, TextAlignmentOptions.Left);
        label.raycastTarget = false;
        RectTransform labelRect = label.rectTransform;
        SkillTreeUI.Stretch(labelRect);
        labelRect.offsetMin = new Vector2(24f, 6f);
        labelRect.offsetMax = new Vector2(-16f, -6f);

        group = background.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void Show(string text, PatternBias bias, float durationSeconds)
    {
        if (group == null) return;

        label.text = text;
        label.color = Color.white;
        accent.color = BiasColor(bias);

        group.alpha = 1f;
        //oyun duraklatılmış olabilir (timeScale = 0); şerit yine de sönmeli
        hideAtTime = Time.unscaledTime + Mathf.Max(0.5f, durationSeconds);
    }

    private void Update()
    {
        if (group == null || group.alpha <= 0f) return;

        float remaining = hideAtTime - Time.unscaledTime;
        if (remaining > fadeSeconds) return;

        group.alpha = remaining <= 0f ? 0f : Mathf.Clamp01(remaining / fadeSeconds);
    }

    private static Color BiasColor(PatternBias bias)
    {
        switch (bias)
        {
            case PatternBias.Bullish: return bullish;
            case PatternBias.Bearish: return bearish;
            default: return neutral;
        }
    }
}
