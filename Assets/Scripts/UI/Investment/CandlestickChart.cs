using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Mock mum grafigi - fragman icin bagimsiz fiyat simulasyonu.
/// InvestmentPanel > ChartArea > Content hiyerarsisine baglanir.
/// </summary>
public class CandlestickChart : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Content objesi - mumlar buraya spawn olur")]
    public RectTransform contentParent;

    [Tooltip("Mum prefab'i (beyaz Image)")]
    public GameObject candlePrefab;

    [Tooltip("InvestmentPanel objesi (acma/kapama icin)")]
    public GameObject investmentPanel;

    [Header("Grafik Ayarlari")]
    [Tooltip("Mum olusturma araligi (saniye)")]
    public float candleInterval = 6f;

    [Tooltip("Mum genisligi (piksel)")]
    public float candleWidth = 16f;

    [Tooltip("Mumlar arasi bosluk (piksel)")]
    public float candleSpacing = 3f;

    [Tooltip("Maksimum mum sayisi (performans icin)")]
    public int maxCandles = 50;

    [Tooltip("Minimum mum yuksekligi (piksel)")]
    public float minCandleHeight = 4f;

    [Tooltip("1 birim fiyat degisimi kac piksel olsun")]
    public float priceToPixel = 30f;

    [Header("Mock Fiyat Ayarlari")]
    [Tooltip("Baslangic fiyati")]
    public float startPrice = 100f;

    [Tooltip("Tick basina max fiyat degisim yuzdesi")]
    public float volatility = 3f;

    [Tooltip("Trend degisim araligi (saniye)")]
    public float trendChangeInterval = 15f;

    // Mock fiyat durumu
    float currentPrice;
    float candleOpenPrice;
    float candleHighPrice;
    float candleLowPrice;
    float timer;

    // Trend durumu
    float currentTrend;
    float trendTimer;

    // Mum verileri
    struct CandleData
    {
        public float open, close;
        public RectTransform rect;
        public Image image;
        public bool isClosed;
    }

    List<CandleData> candles = new List<CandleData>();
    int activeCandleIndex = -1;

    // Fiyat araligi (content boyutlandirma icin)
    float lowestPrice;
    float highestPrice;
    float chartAreaHeight;

    // Renkler
    Color greenColor = new Color(0.18f, 0.8f, 0.34f);
    Color redColor = new Color(0.9f, 0.22f, 0.21f);

    // Debug buton
    GameObject debugButton;

    // Scroll referansi
    ScrollRect scrollRect;

    void Start()
    {
        currentPrice = startPrice;
        lowestPrice = startPrice;
        highestPrice = startPrice;

        // Grafik alaninin yuksekligini al
        RectTransform chartArea = contentParent.parent as RectTransform;
        if (chartArea != null)
            chartAreaHeight = chartArea.rect.height;
        else
            chartAreaHeight = 300f;

        // ScrollRect referansini al
        scrollRect = contentParent.parent.GetComponent<ScrollRect>();

        // Panel kapaliysa kapali kalsin
        if (investmentPanel != null)
            investmentPanel.SetActive(false);

        // Debug acma butonu olustur
        CreateDebugButton();

        // Ilk mumu baslat
        StartNewCandle();
    }

    void Update()
    {
        timer += Time.deltaTime;

        UpdateMockPrice();
        UpdateActiveCandle();

        if (timer >= candleInterval)
        {
            CloseCurrentCandle();
            StartNewCandle();
            timer = 0f;
        }
    }

    void UpdateMockPrice()
    {
        // Trend periyodik olarak yon degistirir
        trendTimer += Time.deltaTime;
        if (trendTimer >= trendChangeInterval)
        {
            // -1 ile 1 arasi rastgele trend
            currentTrend = Random.Range(-1.5f, 1.5f);
            trendChangeInterval = Random.Range(8f, 25f);
            trendTimer = 0f;
        }

        // Rastgele degisim + trend etkisi
        float change = Random.Range(-volatility, volatility) * Time.deltaTime;
        change += currentTrend * Time.deltaTime;

        // Arada ani hareketler (hem yukari hem asagi)
        if (Random.value < 0.003f)
            change += Random.Range(-volatility * 4f, volatility * 4f) * Time.deltaTime * 10f;

        // Fiyat cok duserse yukari cek, cok yükselirse asagi cek (ortalamaya donus)
        float deviation = (currentPrice - startPrice) / startPrice;
        change -= deviation * 0.5f * Time.deltaTime;

        currentPrice += change;
        currentPrice = Mathf.Max(currentPrice, 1f);

        if (currentPrice > candleHighPrice) candleHighPrice = currentPrice;
        if (currentPrice < candleLowPrice) candleLowPrice = currentPrice;

        if (currentPrice < lowestPrice) lowestPrice = currentPrice;
        if (currentPrice > highestPrice) highestPrice = currentPrice;
    }

    // Fiyattan piksel Y pozisyonuna cevir
    float PriceToY(float price)
    {
        return (price - lowestPrice) * priceToPixel;
    }

    void UpdateContentSize()
    {
        // Yatay: mum sayisina gore
        float totalWidth = candles.Count * (candleWidth + candleSpacing);

        // Dikey: fiyat araligina gore
        float priceRange = highestPrice - lowestPrice;
        float totalHeight = priceRange * priceToPixel + chartAreaHeight * 0.5f;
        totalHeight = Mathf.Max(totalHeight, chartAreaHeight);

        contentParent.sizeDelta = new Vector2(totalWidth, totalHeight);
    }

    void StartNewCandle()
    {
        candleOpenPrice = currentPrice;
        candleHighPrice = currentPrice;
        candleLowPrice = currentPrice;

        GameObject candleObj = Instantiate(candlePrefab, contentParent);
        candleObj.SetActive(true);

        RectTransform rect = candleObj.GetComponent<RectTransform>();
        Image img = candleObj.GetComponent<Image>();

        // Anchor sol-alt
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);

        // X pozisyonu
        float xPos = candles.Count * (candleWidth + candleSpacing);

        // Pivot ve pozisyon: open fiyatinda, yukari dogru buyuyecek
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(candleWidth, minCandleHeight);
        rect.anchoredPosition = new Vector2(xPos, PriceToY(currentPrice));

        CandleData data = new CandleData
        {
            open = currentPrice,
            close = currentPrice,
            rect = rect,
            image = img,
            isClosed = false
        };

        candles.Add(data);
        activeCandleIndex = candles.Count - 1;

        UpdateContentSize();

        if (candles.Count > maxCandles)
            RemoveOldestCandle();

        ScrollToRight();
    }

    void UpdateActiveCandle()
    {
        if (activeCandleIndex < 0 || activeCandleIndex >= candles.Count) return;

        CandleData data = candles[activeCandleIndex];
        data.close = currentPrice;
        candles[activeCandleIndex] = data;

        // Content boyutunu guncelle (fiyat araligi degismis olabilir)
        UpdateContentSize();

        // Tum mumlari yeniden ciz (lowestPrice degismis olabilir)
        RedrawAllCandles();
    }

    void CloseCurrentCandle()
    {
        if (activeCandleIndex < 0 || activeCandleIndex >= candles.Count) return;

        CandleData data = candles[activeCandleIndex];
        data.close = currentPrice;
        data.isClosed = true;
        candles[activeCandleIndex] = data;
    }

    void RedrawAllCandles()
    {
        for (int i = 0; i < candles.Count; i++)
        {
            CandleData c = candles[i];
            if (c.rect == null) continue;

            float open = c.open;
            float close = c.close;

            bool isGreen = close >= open;
            float bodySize = Mathf.Abs(close - open);
            float bodyHeight = bodySize * priceToPixel;
            bodyHeight = Mathf.Max(bodyHeight, minCandleHeight);

            if (isGreen)
            {
                // Yesil: open'dan yukari uzar, pivot alt
                c.rect.pivot = new Vector2(0.5f, 0f);
                c.rect.anchoredPosition = new Vector2(c.rect.anchoredPosition.x, PriceToY(open));
            }
            else
            {
                // Kirmizi: open'dan asagi uzar, pivot ust
                c.rect.pivot = new Vector2(0.5f, 1f);
                c.rect.anchoredPosition = new Vector2(c.rect.anchoredPosition.x, PriceToY(open));
            }

            c.rect.sizeDelta = new Vector2(candleWidth, bodyHeight);
            c.image.color = isGreen ? greenColor : redColor;
        }
    }

    void RemoveOldestCandle()
    {
        if (candles.Count == 0) return;

        Destroy(candles[0].rect.gameObject);
        candles.RemoveAt(0);
        activeCandleIndex = candles.Count - 1;

        // X pozisyonlarini yeniden hesapla
        for (int i = 0; i < candles.Count; i++)
        {
            if (candles[i].rect == null) continue;
            float xPos = i * (candleWidth + candleSpacing);
            Vector2 pos = candles[i].rect.anchoredPosition;
            candles[i].rect.anchoredPosition = new Vector2(xPos, pos.y);
        }

        UpdateContentSize();
    }

    void ScrollToRight()
    {
        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
            scrollRect.horizontalNormalizedPosition = 1f;
    }

    // === Debug Buton ===

    void CreateDebugButton()
    {
        Canvas canvas = investmentPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        debugButton = new GameObject("InvestmentDebugButton");
        debugButton.transform.SetParent(canvas.transform, false);

        Image btnImage = debugButton.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);

        RectTransform btnRect = debugButton.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 1f);
        btnRect.anchorMax = new Vector2(0f, 1f);
        btnRect.pivot = new Vector2(0f, 1f);
        btnRect.anchoredPosition = new Vector2(10f, -10f);
        btnRect.sizeDelta = new Vector2(160f, 40f);

        Button btn = debugButton.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(ToggleInvestmentPanel);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(debugButton.transform, false);

        Text btnText = textObj.AddComponent<Text>();
        btnText.text = "Investment Panel";
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 16;
        btnText.color = Color.white;
        btnText.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }

    void ToggleInvestmentPanel()
    {
        if (investmentPanel == null) return;

        bool isActive = investmentPanel.activeSelf;
        investmentPanel.SetActive(!isActive);

        if (!isActive)
            timer = 0f;
    }
}
