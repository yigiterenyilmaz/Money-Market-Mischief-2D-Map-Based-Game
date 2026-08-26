using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TRADE PANELİ SUNUMU — mum grafiğinin altına al/sat şeridini çalışma anında kurar.
///
/// Şerit: fiyat · cüzdan · pozisyon (adet, ortalama maliyet, değer, kâr/zarar) ve
/// miktar kutusu + Al / Hepsini Sat / Kapat butonları.
///
/// Mantık TradingSystem'dedir; burası yalnızca okur ve butonları ona bağlar.
/// UI çalışma anında kurulur (prefab yok) — ama skill ağacının UISpriteFactory'si yerine
/// düz legacy UI kullanılır, çünkü bu şerit CandlestickChart'ın kendi legacy panelinin
/// içine oturuyor ve iki farklı görsel dil aynı panelde çakışıyordu.
/// </summary>
public class TradingPanelUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa sahnede aranır.")]
    public TradingSystem tradingSystem;

    [Header("Görünüm")]
    [Tooltip("Şeridin yüksekliği (piksel).")]
    public float barHeight = 96f;

    private const float REFRESH_INTERVAL = 0.1f; //saniyede 10 kez yeter; her frame metin kurmak israf

    private Font font;
    private GameObject bar;
    private Text priceText;
    private Text walletText;
    private Text positionText;
    private Text profitText;
    private Text messageText;
    private InputField amountField;
    private Button buyButton;
    private Button sellButton;

    private float refreshTimer;
    private float messageHideAt;

    private readonly Color profitColor = new Color(0.18f, 0.8f, 0.34f);
    private readonly Color lossColor = new Color(0.9f, 0.22f, 0.21f);
    private readonly Color neutralColor = new Color(0.85f, 0.85f, 0.85f);

    private void Start()
    {
        if (tradingSystem == null)
            tradingSystem = TradingSystem.Instance != null
                ? TradingSystem.Instance
                : FindFirstObjectByType<TradingSystem>();

        if (tradingSystem == null)
        {
            Debug.LogWarning("[TradingPanelUI] TradingSystem sahnede yok — al/sat şeridi kurulamadı.", this);
            enabled = false;
            return;
        }

        //TradingSystem.Awake grafiği çözüyor; yine de boşsa kendin ara
        if (tradingSystem.chart == null)
            tradingSystem.chart = FindFirstObjectByType<CandlestickChart>();

        if (tradingSystem.chart == null || tradingSystem.chart.investmentPanel == null)
        {
            Debug.LogWarning("[TradingPanelUI] CandlestickChart veya investmentPanel atanmamış — " +
                             "al/sat şeridi kurulamadı.", this);
            enabled = false;
            return;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildBar(tradingSystem.chart.investmentPanel.transform);
        Refresh();
    }

    private void OnEnable()
    {
        TradingSystem.OnPositionChanged += Refresh;
    }

    private void OnDisable()
    {
        TradingSystem.OnPositionChanged -= Refresh;
    }

    private void Update()
    {
        if (tradingSystem == null || !tradingSystem.IsPanelOpen) return;

        //panel açıkken oyun duraklatılmış olabilir; şerit yine de akmalı
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < REFRESH_INTERVAL) return;

        refreshTimer = 0f;
        Refresh();

        if (messageText != null && messageText.enabled && Time.unscaledTime >= messageHideAt)
            messageText.enabled = false;
    }

    // ==================== VERİ ====================

    private void Refresh()
    {
        if (tradingSystem == null || bar == null) return;

        float price = tradingSystem.CurrentPrice;
        priceText.text = $"FİYAT  {price:N2}";

        if (GameStatManager.Instance != null)
            walletText.text = $"CÜZDAN  {GameStatManager.Instance.Wealth:N0}";

        if (tradingSystem.Quantity <= 0f)
        {
            positionText.text = "POZİSYON YOK";
            profitText.text = "";
            profitText.color = neutralColor;
        }
        else
        {
            positionText.text =
                $"{tradingSystem.Quantity:N3} adet  ·  ort. {tradingSystem.AverageEntryPrice:N2}  ·  " +
                $"değer {tradingSystem.PositionValue:N0}";

            float profit = tradingSystem.UnrealizedProfit;
            float percent = tradingSystem.CostBasis > 0f ? profit / tradingSystem.CostBasis * 100f : 0f;

            profitText.text = $"{(profit >= 0f ? "+" : "")}{profit:N0}  ({percent:+0.0;-0.0;0.0}%)";
            profitText.color = profit >= 0f ? profitColor : lossColor;
        }

        //satılacak bir şey yoksa sat butonu sönük dursun
        if (sellButton != null)
            sellButton.interactable = tradingSystem.Quantity > 0f;
    }

    private void ShowMessage(string message)
    {
        if (messageText == null) return;

        messageText.text = message;
        messageText.enabled = true;
        messageHideAt = Time.unscaledTime + 2f;
    }

    // ==================== BUTONLAR ====================

    private void OnBuyPressed()
    {
        float amount = ParseAmount();
        if (amount <= 0f)
        {
            ShowMessage("Geçerli bir miktar gir.");
            return;
        }

        if (!tradingSystem.Buy(amount))
        {
            ShowMessage("Alım yapılamadı — para yetersiz.");
            return;
        }

        ShowMessage($"{amount:N0} tutarında alım yapıldı.");
    }

    private void OnSellPressed()
    {
        if (tradingSystem.Quantity <= 0f)
        {
            ShowMessage("Satılacak pozisyon yok.");
            return;
        }

        float realized = tradingSystem.SellAll();
        ShowMessage(realized >= 0f
            ? $"Pozisyon kapandı. Kâr: +{realized:N0}"
            : $"Pozisyon kapandı. Zarar: {realized:N0}");
    }

    private void OnMaxPressed()
    {
        if (GameStatManager.Instance == null || amountField == null) return;

        amountField.text = Mathf.Floor(GameStatManager.Instance.Wealth).ToString("F0");
    }

    private void OnClosePressed()
    {
        tradingSystem.ClosePanel();
    }

    private float ParseAmount()
    {
        if (amountField == null) return 0f;
        if (!float.TryParse(amountField.text, out float amount)) return 0f;

        return amount;
    }

    // ==================== KURULUM ====================

    private void BuildBar(Transform panel)
    {
        bar = CreateRect("TradeBar", panel, new Color(0.09f, 0.09f, 0.11f, 0.96f));
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.offsetMin = new Vector2(0f, 0f);
        barRect.offsetMax = new Vector2(0f, barHeight);

        //üst satır — fiyat, cüzdan, pozisyon, kâr
        priceText = CreateText("Price", bar.transform, 20, TextAnchor.MiddleLeft, Color.white);
        Place(priceText, new Vector2(16f, -12f), new Vector2(170f, 26f), new Vector2(0f, 1f));

        walletText = CreateText("Wallet", bar.transform, 16, TextAnchor.MiddleLeft, neutralColor);
        Place(walletText, new Vector2(196f, -12f), new Vector2(200f, 26f), new Vector2(0f, 1f));

        positionText = CreateText("Position", bar.transform, 15, TextAnchor.MiddleLeft, neutralColor);
        Place(positionText, new Vector2(16f, -42f), new Vector2(420f, 24f), new Vector2(0f, 1f));

        profitText = CreateText("Profit", bar.transform, 17, TextAnchor.MiddleLeft, neutralColor);
        Place(profitText, new Vector2(444f, -42f), new Vector2(200f, 24f), new Vector2(0f, 1f));

        messageText = CreateText("Message", bar.transform, 14, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.4f));
        Place(messageText, new Vector2(444f, -12f), new Vector2(320f, 24f), new Vector2(0f, 1f));
        messageText.enabled = false;

        //sağ blok — miktar kutusu ve butonlar
        amountField = CreateInputField("AmountField", bar.transform, new Vector2(-372f, 16f), new Vector2(130f, 34f));
        amountField.text = tradingSystem.defaultOrderAmount.ToString("F0");

        CreateButton("MaxButton", bar.transform, "MAX", new Vector2(-300f, 16f), new Vector2(56f, 34f),
            new Color(0.25f, 0.25f, 0.3f), OnMaxPressed);

        buyButton = CreateButton("BuyButton", bar.transform, "AL", new Vector2(-222f, 16f), new Vector2(84f, 34f),
            new Color(0.12f, 0.5f, 0.24f), OnBuyPressed);

        sellButton = CreateButton("SellButton", bar.transform, "HEPSİNİ SAT", new Vector2(-118f, 16f),
            new Vector2(124f, 34f), new Color(0.6f, 0.16f, 0.15f), OnSellPressed);

        CreateButton("CloseButton", bar.transform, "KAPAT", new Vector2(-16f, 16f), new Vector2(80f, 34f),
            new Color(0.22f, 0.22f, 0.26f), OnClosePressed);
    }

    private GameObject CreateRect(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;

        return go;
    }

    private Text CreateText(string name, Transform parent, int size, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    /// <summary>Sol-üst köşeden konumlandırır (şeridin içi sabit düzen, ölçeklenmiyor).</summary>
    private void Place(Graphic graphic, Vector2 position, Vector2 size, Vector2 anchor)
    {
        RectTransform rect = graphic.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size,
        Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = CreateRect(name, parent, color);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        Text text = CreateText("Label", go.transform, 15, TextAnchor.MiddleCenter, Color.white);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.text = label;

        return button;
    }

    private InputField CreateInputField(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = CreateRect(name, parent, new Color(0.16f, 0.16f, 0.19f));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = CreateText("Text", go.transform, 15, TextAnchor.MiddleLeft, Color.white);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
        text.supportRichText = false;

        InputField field = go.AddComponent<InputField>();
        field.textComponent = text;
        field.contentType = InputField.ContentType.DecimalNumber;
        field.targetGraphic = go.GetComponent<Image>();

        return field;
    }
}
