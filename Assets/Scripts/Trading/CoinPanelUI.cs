using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// COIN KARTI — oyuncunun çıkardığı coin'leri trade ekranının (investment panel) sağ
/// üstünde canlı gösterir: fiyat, piyasa değeri, hype çubuğu ve BOŞALT butonu.
///
/// Kart grafiğin yanında durur, çünkü coin'ler de bir piyasa aracı ve oyuncu fiyat
/// hareketiyle birlikte görmek istiyor. Bunun bedeli, boşaltma kararının paneli açmayı
/// gerektirmesi — hype feed'den besleniyor ama boşaltma anı burada veriliyor.
///
/// Yaşayan coin yokken kart tamamen gizlenir.
/// </summary>
public class CoinPanelUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Kartın içine kurulacağı trade paneli. Boş bırakılırsa CandlestickChart'tan alınır.")]
    public GameObject hostPanel;

    [Header("Görünüm")]
    [Tooltip("Kartın panelin sağ üstünden uzaklığı (piksel).")]
    public Vector2 margin = new Vector2(-16f, -16f);

    public float cardWidth = 260f;

    private const float REFRESH_INTERVAL = 0.1f;

    private GameObject root;
    private GameObject scamCard;
    private Text scamTitle;
    private Text scamValue;
    private Image hypeMeter;
    private Text scamRisk;
    private GameObject legalCard;
    private Text legalTitle;
    private Text legalValue;
    private Text messageText;

    private float refreshTimer;
    private float messageHideAt;

    private void Start()
    {
        if (hostPanel == null)
        {
            //TradingSystem varsa referansını kullan, yoksa (veya alanı boşsa) kendin bul
            CandlestickChart chart = TradingSystem.Instance != null ? TradingSystem.Instance.chart : null;
            if (chart == null)
                chart = FindFirstObjectByType<CandlestickChart>();

            if (chart != null)
                hostPanel = chart.investmentPanel;
        }

        if (hostPanel == null)
        {
            Debug.LogWarning("[CoinPanelUI] Trade paneli bulunamadı — coin kartı kurulamadı. " +
                             "CandlestickChart.investmentPanel atanmış olmalı.", this);
            enabled = false;
            return;
        }

        Build(hostPanel.transform);
        Refresh();
    }

    private void OnEnable()
    {
        CoinLaunchSystem.OnCoinLaunched += HandleLaunched;
        CoinLaunchSystem.OnCoinRugPulled += HandleRugPulled;
        CoinLaunchSystem.OnCoinCollapsed += HandleCollapsed;
    }

    private void OnDisable()
    {
        CoinLaunchSystem.OnCoinLaunched -= HandleLaunched;
        CoinLaunchSystem.OnCoinRugPulled -= HandleRugPulled;
        CoinLaunchSystem.OnCoinCollapsed -= HandleCollapsed;
    }

    private void Update()
    {
        if (root == null) return;

        //panel kapalıyken kart görünmüyor; coin'in kendisi CoinLaunchSystem'de işlemeye devam eder
        if (hostPanel != null && !hostPanel.activeSelf) return;

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
        CoinLaunchSystem system = CoinLaunchSystem.Instance;
        if (system == null)
        {
            root.SetActive(false);
            return;
        }

        PlayerCoin scam = system.ScamCoin;
        PlayerCoin legal = system.LegalCoin;

        scamCard.SetActive(scam != null);
        legalCard.SetActive(legal != null);

        //kart tamamen boşsa (ve gösterilecek mesaj da yoksa) hiç görünmesin
        bool anything = scam != null || legal != null || (messageText != null && messageText.enabled);
        root.SetActive(anything);

        if (scam != null)
        {
            scamTitle.text = $"{scam.displayName}  ·  SCAM";
            scamValue.text = $"{scam.price:N4}   değer {scam.MarketCap:N0}";
            hypeMeter.fillAmount = scam.hype;

            //oyuncunun kararı riski görmesine bağlı: hype yükseldikçe uyarı sertleşir
            if (scam.hype < 0.35f)
            {
                scamRisk.text = "hype düşük — balon henüz küçük";
                scamRisk.color = TradeUIBuilder.InkSoft;
            }
            else if (scam.hype < 0.75f)
            {
                scamRisk.text = "hype yükseliyor — çökme riski var";
                scamRisk.color = TradeUIBuilder.Warn;
            }
            else
            {
                scamRisk.text = "BALON PATLAMAK ÜZERE";
                scamRisk.color = TradeUIBuilder.Loss;
            }
        }

        if (legal != null)
        {
            legalTitle.text = $"{legal.displayName}  ·  LEGAL";
            legalValue.text = $"{legal.price:N3}   değer {legal.MarketCap:N0}";
        }
    }

    private void ShowMessage(string message, Color color)
    {
        if (messageText == null) return;

        messageText.text = message;
        messageText.color = color;
        messageText.enabled = true;
        messageHideAt = Time.unscaledTime + 4f;

        if (root != null) root.SetActive(true);
    }

    private void HandleLaunched(PlayerCoin coin)
    {
        if (coin.type == PlayerCoinType.Scam)
            ShowMessage($"{coin.displayName} piyasaya sürüldü. Feed'de piyasa konuşuldukça şişer.",
                TradeUIBuilder.Warn);
        else
            ShowMessage($"{coin.displayName} yasal olarak listelendi.", TradeUIBuilder.Profit);

        Refresh();
    }

    private void HandleRugPulled(PlayerCoin coin, float proceeds)
    {
        ShowMessage($"{coin.displayName} boşaltıldı. +{proceeds:N0}", TradeUIBuilder.Profit);
        Refresh();
    }

    private void HandleCollapsed(PlayerCoin coin)
    {
        ShowMessage($"{coin.displayName} çöktü. Geç kaldın — elinde hiçbir şey kalmadı.",
            TradeUIBuilder.Loss);
        Refresh();
    }

    private void OnRugPullPressed()
    {
        CoinLaunchSystem system = CoinLaunchSystem.Instance;
        if (system == null || system.ScamCoin == null) return;

        system.RugPull();
    }

    // ==================== KURULUM ====================

    private void Build(Transform host)
    {
        root = new GameObject("CoinPanel", typeof(RectTransform));
        root.transform.SetParent(host, false);
        TradeUIBuilder.Place(root.GetComponent<RectTransform>(), new Vector2(1f, 1f), margin,
            new Vector2(cardWidth, 240f));

        messageText = TradeUIBuilder.Label("Message", root.transform, 13, TextAnchor.UpperLeft,
            TradeUIBuilder.Warn);
        TradeUIBuilder.Place(messageText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f),
            new Vector2(cardWidth, 34f));
        messageText.enabled = false;

        scamCard = BuildScamCard(new Vector2(0f, -40f));
        legalCard = BuildLegalCard(new Vector2(0f, -152f));
    }

    private GameObject BuildScamCard(Vector2 position)
    {
        GameObject card = TradeUIBuilder.Panel("ScamCoin", root.transform, TradeUIBuilder.Surface);
        TradeUIBuilder.Place(card.GetComponent<RectTransform>(), new Vector2(1f, 1f), position,
            new Vector2(cardWidth, 104f));

        scamTitle = TradeUIBuilder.Label("Title", card.transform, 15, TextAnchor.MiddleLeft, Color.white);
        TradeUIBuilder.Place(scamTitle.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -10f),
            new Vector2(cardWidth - 24f, 20f));

        scamValue = TradeUIBuilder.Label("Value", card.transform, 13, TextAnchor.MiddleLeft,
            TradeUIBuilder.InkSoft);
        TradeUIBuilder.Place(scamValue.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -32f),
            new Vector2(cardWidth - 24f, 18f));

        hypeMeter = TradeUIBuilder.Meter("Hype", card.transform, TradeUIBuilder.Warn);
        RectTransform meterRect = hypeMeter.rectTransform.parent as RectTransform;
        TradeUIBuilder.Place(meterRect, new Vector2(0f, 1f), new Vector2(12f, -54f),
            new Vector2(cardWidth - 24f, 8f));

        scamRisk = TradeUIBuilder.Label("Risk", card.transform, 12, TextAnchor.MiddleLeft,
            TradeUIBuilder.InkSoft);
        TradeUIBuilder.Place(scamRisk.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -66f),
            new Vector2(cardWidth - 24f, 18f));

        Button rugPull = TradeUIBuilder.Button("RugPull", card.transform, "BOŞALT (RUG PULL)",
            new Color(0.6f, 0.16f, 0.15f), OnRugPullPressed);
        TradeUIBuilder.Place(rugPull.GetComponent<RectTransform>(), new Vector2(0f, 0f),
            new Vector2(12f, 10f), new Vector2(cardWidth - 24f, 28f));

        return card;
    }

    private GameObject BuildLegalCard(Vector2 position)
    {
        GameObject card = TradeUIBuilder.Panel("LegalCoin", root.transform, TradeUIBuilder.Surface);
        TradeUIBuilder.Place(card.GetComponent<RectTransform>(), new Vector2(1f, 1f), position,
            new Vector2(cardWidth, 56f));

        legalTitle = TradeUIBuilder.Label("Title", card.transform, 15, TextAnchor.MiddleLeft, Color.white);
        TradeUIBuilder.Place(legalTitle.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -10f),
            new Vector2(cardWidth - 24f, 20f));

        legalValue = TradeUIBuilder.Label("Value", card.transform, 13, TextAnchor.MiddleLeft,
            TradeUIBuilder.InkSoft);
        TradeUIBuilder.Place(legalValue.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -32f),
            new Vector2(cardWidth - 24f, 18f));

        return card;
    }
}
