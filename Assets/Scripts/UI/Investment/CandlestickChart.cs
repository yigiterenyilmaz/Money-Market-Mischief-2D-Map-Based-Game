using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Mum grafigi komponenti. Fiyat simulasyonu ile mum cubuklarini cizer.
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

    [Tooltip("Harita kamera kontrolcusu (panel acikken devre disi)")]
    public MapController mapController;

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

    [Header("Zoom Ayarlari")]
    [Tooltip("Zoom hizi")]
    public float zoomSpeed = 0.1f;

    [Tooltip("Minimum zoom (uzaklastirma siniri)")]
    public float minZoom = 0.3f;

    [Tooltip("Maksimum zoom (yakinlastirma siniri)")]
    public float maxZoom = 3f;

    [Header("Fiyat Simulasyonu")]
    [Tooltip("Baslangic fiyati")]
    public float startPrice = 100f;

    [Tooltip("Tick basina max fiyat degisim yuzdesi")]
    public float volatility = 3f;

    [Tooltip("Trend rastgele itme siddeti (her tick yon degisimi)")]
    public float trendNoise = 2.5f;

    [Tooltip("Trend ortalamaya donus hizi (yuksek = trend kisa surede sonumlenir)")]
    public float trendDecay = 0.4f;

    [Tooltip("Trend ust siniri (mutlak deger)")]
    public float maxTrend = 2.5f;

    [Header("Pattern Sistem")]
    [Tooltip("Pattern hedef yuzdeleri bu carpan ile olceklenir (0.5=sakin, 2=sert)")]
    [Range(0.5f, 2f)]
    public float volatilityMultiplier = 1f;

    [Tooltip("Pattern arasi minimum mum sayisi")]
    public int patternCooldownMin = 5;

    [Tooltip("Pattern arasi maksimum mum sayisi")]
    public int patternCooldownMax = 15;

    // Fiyat durumu
    float currentPrice;
    float candleOpenPrice;
    float candleHighPrice;
    float candleLowPrice;
    float timer;

    // Pattern sistemi
    MarketState marketState;
    PatternScheduler scheduler;
    CandlePathPlayer pathPlayer;
    NoiseDriver noiseDriver;
    float currentCandleStartTime;

    // Frame sayaclari
    int priceFrameCounter;
    int candleFrameCounter;

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

    // Zoom
    float currentZoom = 1f;
    float basePriceToPixel;
    float baseCandleWidth;
    float baseCandleSpacing;

    // Renkler
    Color greenColor = new Color(0.18f, 0.8f, 0.34f);
    Color redColor = new Color(0.9f, 0.22f, 0.21f);

    // Debug buton
    GameObject debugButton;

    // Scroll referansi
    ScrollRect scrollRect;
    RectTransform viewportRect;
    Camera uiEventCamera;

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

        // Zoom icin base degerleri kaydet
        basePriceToPixel = priceToPixel;
        baseCandleWidth = candleWidth;
        baseCandleSpacing = candleSpacing;

        // ScrollRect referansini al
        scrollRect = contentParent.parent.GetComponent<ScrollRect>();

        // Mouse tekerlegini scroll icin degil zoom icin kullanacagiz
        if (scrollRect != null)
        {
            scrollRect.scrollSensitivity = 0f;

            // Surukleme algilamasi icin viewport ve canvas camerasini cache'le
            viewportRect = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
            Canvas canvas = scrollRect.GetComponentInParent<Canvas>();
            uiEventCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        }

        // Panel kapaliysa kapali kalsin
        if (investmentPanel != null)
            investmentPanel.SetActive(false);

        // Debug acma butonu olustur
        CreateDebugButton();

        // Pattern sistemi initialize
        marketState = new MarketState(startPrice);
        noiseDriver = new NoiseDriver(volatility, trendNoise, trendDecay, maxTrend, startPrice);
        pathPlayer = new CandlePathPlayer();
        scheduler = new PatternScheduler(patternCooldownMin, patternCooldownMax, volatilityMultiplier);
        scheduler.RegisterAll(new ChartPattern[]
        {
            new PumpEvent(),
            new DojiCandle(),
            new AscendingTriangle(),
            new HeadAndShoulders()
        });

        // Ilk mumu baslat
        StartNewCandle();
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Zoom: panel acikken mouse tekerlegiyle
        if (investmentPanel != null && investmentPanel.activeSelf)
            HandleZoom();

        priceFrameCounter++;
        if (priceFrameCounter >= 2)
        {
            priceFrameCounter = 0;
            UpdatePrice();
        }

        candleFrameCounter++;
        if (candleFrameCounter >= 10)
        {
            candleFrameCounter = 0;
            UpdateActiveCandle();
        }

        if (timer >= candleInterval)
        {
            CloseCurrentCandle();
            StartNewCandle();
            timer = 0f;
        }
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;

        float prevZoom = currentZoom;
        currentZoom += scroll * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

        if (Mathf.Approximately(prevZoom, currentZoom)) return;

        // Zoom'a gore degerler guncelle
        priceToPixel = basePriceToPixel * currentZoom;
        candleWidth = baseCandleWidth * currentZoom;
        candleSpacing = baseCandleSpacing * currentZoom;

        // Tum mumlarin X pozisyonlarini yeniden hesapla
        for (int i = 0; i < candles.Count; i++)
        {
            if (candles[i].rect == null) continue;
            float xPos = i * (candleWidth + candleSpacing);
            candles[i].rect.anchoredPosition = new Vector2(xPos, candles[i].rect.anchoredPosition.y);
        }

        UpdateContentSize();
        RedrawAllCandles();
    }

    void UpdatePrice()
    {
        // Mum baslangicinda pathPlayer'a o mumun nihai OHLC'si yuklendi.
        // Burada sadece o yolun mevcut zamandaki sample'ini okuyup currentPrice'i guncelliyoruz.
        // Tum OU random walk mantigi NoiseDriver icine tasindi.
        if (pathPlayer == null) return;

        float t = Time.time - currentCandleStartTime;
        currentPrice = pathPlayer.GetPriceAt(t);
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
        // Bu mumun nihai OHLC'sini pattern (aktifse) veya noise driver'dan al.
        // pathPlayer mum suresince currentPrice'i o yola sample'lar.
        CandleOHLC nextOHLC;
        if (scheduler != null && scheduler.HasActivePattern)
            nextOHLC = scheduler.ActivePattern.GenerateNextCandle(currentPrice);
        else if (noiseDriver != null)
            nextOHLC = noiseDriver.GenerateNextCandle(currentPrice, candleInterval);
        else
            nextOHLC = new CandleOHLC(currentPrice, currentPrice, currentPrice, currentPrice);

        pathPlayer?.LoadCandle(nextOHLC, candleInterval);
        currentCandleStartTime = Time.time;

        currentPrice = nextOHLC.open;
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

        // Pattern sistemine bildir
        if (marketState != null)
        {
            CandleOHLC ohlc = new CandleOHLC(data.open, data.high, data.low, data.close);
            marketState.OnCandleClosed(ohlc);
        }

        if (scheduler != null)
        {
            if (scheduler.HasActivePattern)
            {
                scheduler.ActivePattern.OnCandleClosed();
                if (scheduler.ActivePattern.IsDone())
                    scheduler.MarkActiveDone();
            }
            else
            {
                scheduler.OnIdleCandle(marketState);
            }
        }
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

    // Kullanici scroll viewport uzerinde sol tusla suruklerken auto-scroll devre disi
    bool IsUserDragging()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed) return false;
        if (viewportRect == null) return false;
        Vector2 screenPos = Mouse.current.position.ReadValue();
        return RectTransformUtility.RectangleContainsScreenPoint(viewportRect, screenPos, uiEventCamera);
    }

    void ScrollToRight()
    {
        if (IsUserDragging()) return;

        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
            scrollRect.horizontalNormalizedPosition = 1f;
    }

    void ScrollToCurrentPrice()
    {
        if (scrollRect == null) return;
        if (IsUserDragging()) return;

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

        // Panel acilinca harita kontrolunu kapat, kapaninca ac
        if (mapController != null)
            mapController.enable = isActive;

        if (!isActive)
            timer = 0f;
    }

    // === Debug pattern tetikleyiciler (Inspector right-click) ===

    [ContextMenu("Force Pattern: Pump (D1)")]
    void DebugForcePump()
    {
        if (scheduler != null && marketState != null)
            scheduler.TryForcePattern("D1_Pump", marketState);
    }

    [ContextMenu("Force Pattern: Doji (C5)")]
    void DebugForceDoji()
    {
        if (scheduler != null && marketState != null)
            scheduler.TryForcePattern("C5_Doji", marketState);
    }

    [ContextMenu("Force Pattern: Ascending Triangle (A6)")]
    void DebugForceAscTri()
    {
        if (scheduler != null && marketState != null)
            scheduler.TryForcePattern("A6_AscendingTriangle", marketState);
    }

    [ContextMenu("Force Pattern: Head and Shoulders (A1)")]
    void DebugForceHnS()
    {
        if (scheduler != null && marketState != null)
            scheduler.TryForcePattern("A1_HeadAndShoulders", marketState);
    }
}
