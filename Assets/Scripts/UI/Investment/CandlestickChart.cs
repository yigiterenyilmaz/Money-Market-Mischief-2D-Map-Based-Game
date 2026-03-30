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

    [Tooltip("Fitil genisligi (piksel)")]
    public float wickWidth = 2f;

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
        public float open, close, high, low;
        public RectTransform rect;
        public Image image;
        public RectTransform wickRect;
        public Image wickImage;
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

        // X pozisyonu
        float xPos = candles.Count * (candleWidth + candleSpacing);

        // Mum govdesi olustur
        GameObject candleObj = Instantiate(candlePrefab, contentParent);
        candleObj.SetActive(true);

        RectTransform rect = candleObj.GetComponent<RectTransform>();
        Image img = candleObj.GetComponent<Image>();

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(candleWidth, minCandleHeight);
        rect.anchoredPosition = new Vector2(xPos, PriceToY(currentPrice));

        // Fitil olustur (mumun child'i, arkasinda kalacak)
        GameObject wickObj = new GameObject("Wick");
        wickObj.transform.SetParent(rect, false);
        wickObj.transform.SetAsFirstSibling(); // mumun arkasinda kalsin
        Image wickImg = wickObj.AddComponent<Image>();
        wickImg.color = Color.white;
        RectTransform wickRect = wickObj.GetComponent<RectTransform>();
        wickRect.anchorMin = new Vector2(0.5f, 0f);
        wickRect.anchorMax = new Vector2(0.5f, 0f);
        wickRect.pivot = new Vector2(0.5f, 0f);
        wickRect.sizeDelta = new Vector2(wickWidth, minCandleHeight);
        wickRect.anchoredPosition = Vector2.zero;

        CandleData data = new CandleData
        {
            open = currentPrice,
            close = currentPrice,
            high = currentPrice,
            low = currentPrice,
            rect = rect,
            image = img,
            wickRect = wickRect,
            wickImage = wickImg,
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
        data.high = candleHighPrice;
        data.low = candleLowPrice;
        candles[activeCandleIndex] = data;

        // Content boyutunu guncelle (fiyat araligi degismis olabilir)
        UpdateContentSize();

        // Tum mumlari yeniden ciz (lowestPrice degismis olabilir)
        RedrawAllCandles();

        // Kamerayi mevcut fiyata odakla
        ScrollToCurrentPrice();
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
            float high = c.high;
            float low = c.low;
            float xPos = c.rect.anchoredPosition.x;

            bool isGreen = close >= open;
            float bodySize = Mathf.Abs(close - open);
            float bodyHeight = bodySize * priceToPixel;
            bodyHeight = Mathf.Max(bodyHeight, minCandleHeight);

            // Mum govdesi
            if (isGreen)
            {
                c.rect.pivot = new Vector2(0.5f, 0f);
                c.rect.anchoredPosition = new Vector2(xPos, PriceToY(open));
            }
            else
            {
                c.rect.pivot = new Vector2(0.5f, 1f);
                c.rect.anchoredPosition = new Vector2(xPos, PriceToY(open));
            }

            c.rect.sizeDelta = new Vector2(candleWidth, bodyHeight);
            c.image.color = isGreen ? greenColor : redColor;

            // Fitil: low'dan high'a uzanir (mumun local koordinatinda)
            if (c.wickRect != null)
            {
                float wickHeight = (high - low) * priceToPixel;
                wickHeight = Mathf.Max(wickHeight, 1f);

                // Mumun pivot noktasina gore fitil offseti
                // Yesil: pivot alt = open fiyati, fitil low'dan baslar → offset = (low - open) * priceToPixel
                // Kirmizi: pivot ust = open fiyati, fitil low'dan baslar → offset = (low - open) * priceToPixel + bodyHeight
                float localY;
                if (isGreen)
                    localY = (low - open) * priceToPixel;
                else
                    localY = (low - open) * priceToPixel + bodyHeight;

                c.wickRect.pivot = new Vector2(0.5f, 0f);
                c.wickRect.anchoredPosition = new Vector2(0f, localY);
                c.wickRect.sizeDelta = new Vector2(wickWidth, wickHeight);
                c.wickImage.color = isGreen ? greenColor : redColor;
            }
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
            // Fitil mumun child'i, X otomatik takip eder
        }

        UpdateContentSize();
    }

    void ScrollToRight()
    {
        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
            scrollRect.horizontalNormalizedPosition = 1f;
    }

    void ScrollToCurrentPrice()
    {
        if (scrollRect == null) return;

        // Yatay: en saga
        scrollRect.horizontalNormalizedPosition = 1f;

        // Dikey: sadece mum viewport disina cikinca scroll yap
        float contentHeight = contentParent.sizeDelta.y;
        float viewportHeight = chartAreaHeight;
        if (contentHeight <= viewportHeight) return;

        float maxScroll = contentHeight - viewportHeight;
        float priceY = PriceToY(currentPrice);

        // Viewport'un su an gosterdigi alt ve ust sinirlar
        float currentScrollY = scrollRect.verticalNormalizedPosition * maxScroll;
        float viewBottom = currentScrollY;
        float viewTop = currentScrollY + viewportHeight;

        // Ust kenara yaklasinca padding kadar bosluk birak
        float padding = viewportHeight * 0.1f;

        float targetScrollY = currentScrollY;

        if (priceY > viewTop - padding)
        {
            // Fiyat ustten tasiyor, yukari kaydir
            targetScrollY = priceY - viewportHeight + padding;
        }
        else if (priceY < viewBottom + padding)
        {
            // Fiyat alttan tasiyor, asagi kaydir
            targetScrollY = priceY - padding;
        }

        targetScrollY = Mathf.Clamp(targetScrollY, 0f, maxScroll);
        scrollRect.verticalNormalizedPosition = targetScrollY / maxScroll;
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
