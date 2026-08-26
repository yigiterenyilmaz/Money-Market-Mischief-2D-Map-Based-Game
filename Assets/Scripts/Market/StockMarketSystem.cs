using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BORSA SİSTEMİ — a20 (Finance/Market) alt ağacının sahibi.
///
/// Piyasanın kendisi CandlestickChart'tır (mum simülasyonu + 38 formasyon). Bu sınıf o
/// piyasanın ÜSTÜNE oyuncunun manipülasyon araçlarını koyar; grafiği kendisi çizmez.
///   a21 hileli bot      -> her mum kapanışında yönden bağımsız kâr yazar (UnlockTradingBot)
///   a22 yalan bilanço   -> grafiğe pump enjekte eder (ForcePattern)
///   a23 formasyon manip.-> grafiğe istenen formasyonu enjekte eder (ForcePattern)
///   a24/a25 insider     -> formasyon başlarken yönünü/adını önceden sızdırır (UnlockInsider)
///
/// Mantık burada, sunum MarketIntelUI'dadır (RealEstateSystem/PropertyInspectUI ayrımının aynısı).
///
/// YARIM: oyuncunun ELİYLE alım satım yapabildiği bir ekran YOK. Bot olmadan formasyon
/// manipülasyonu ve insider bilgisi para kazandırmaz — sadece grafiği oynatır. Bkz. market-readme.md
/// </summary>
public class StockMarketSystem : MonoBehaviour
{
    public static StockMarketSystem Instance { get; private set; }

    [Header("Referanslar")]
    [Tooltip("Piyasa simülasyonu. Boş bırakılırsa sahnede aranır.")]
    public CandlestickChart chart;

    [Header("Bot — Şüphe")]
    [Tooltip("Bot her mum kapanışında bu kadar şüphe üretir. Mum aralığı 6 sn olduğu için " +
             "0.05 ≈ saatte 30 şüphe. Sıfırlanırsa bot bedava para basar.")]
    public float botSuspicionPerCandle = 0.05f;

    [Header("Manipülasyon")]
    [Tooltip("Manipülasyon sonrası ipucunun ekranda kalma süresi (saniye).")]
    public float tipDurationSeconds = 8f;

    // Bot durumu — UnlockTradingBotEffect ile gelir
    private bool botActive;
    private float botCapital;
    private float botEfficiency;

    // Insider durumu — 0 kapalı, 1 şirket içi (a24), 2 devlet içi (a25)
    private int insiderLevel;

    /// <summary>Ekranda gösterilecek istihbarat/manipülasyon mesajı: metin, yön, süre.</summary>
    public static event Action<string, PatternBias, float> OnMarketTip;

    /// <summary>Bot bir mumdan kâr yazdı (miktar). Şimdilik dinleyicisi yok, UI için ayrıldı.</summary>
    public static event Action<float> OnBotProfit;

    public bool BotActive => botActive;
    public int InsiderLevel => insiderLevel;

    private void Awake()
    {
        //DİKKAT: Destroy(gameObject) DEĞİL. Bu bileşen paylaşılan Managers objesine takılıyor;
        //objeyi yok etmek üzerindeki BÜTÜN yöneticileri sessizce öldürür (daha önce
        //SkillTreeManager'ı öldürüp hiçbir skill satın alınamamasına yol açmıştı).
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        //chart Start()'ta scheduler kuruyor; abonelik Start'ta yapılır (aşağıda).
        //OnEnable sadece yeniden etkinleşmede tekrar bağlanmak için.
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (chart == null)
            chart = FindFirstObjectByType<CandlestickChart>();

        if (chart == null) return;

        //aynı event'e iki kez bağlanmamak için önce çöz
        chart.CandleClosed -= HandleCandleClosed;
        chart.PatternActivated -= HandlePatternActivated;

        chart.CandleClosed += HandleCandleClosed;
        chart.PatternActivated += HandlePatternActivated;
    }

    private void Unsubscribe()
    {
        if (chart == null) return;

        chart.CandleClosed -= HandleCandleClosed;
        chart.PatternActivated -= HandlePatternActivated;
    }

    // ==================== a21 — HİLELİ ALIM SATIM BOTU ====================

    /// <summary>
    /// UnlockTradingBotEffect tarafından çağrılır. Bot açıldıktan sonra her mum kapanışında
    /// çalışır ve grafiğin hangi yöne gittiğinden bağımsız olarak kâr yazar — "hileli" olması
    /// tam olarak budur: yönü önceden bildiği için asla zarar etmez.
    /// </summary>
    public void UnlockTradingBot(float capital, float efficiency)
    {
        //ikinci kez açılırsa daha iyi olan sermaye/verim geçerli olsun (skill zinciri büyüterek gelir)
        botCapital = Mathf.Max(botCapital, capital);
        botEfficiency = Mathf.Max(botEfficiency, efficiency);
        botActive = true;

        Debug.Log($"[StockMarket] Hileli bot açıldı — sermaye {botCapital:N0}, verim {botEfficiency:P0}");
    }

    private void HandleCandleClosed(CandleOHLC ohlc)
    {
        if (!botActive) return;
        if (GameStatManager.Instance == null) return;

        //kâr, mumun GÖVDESİ kadar: fitil (mum içi salınım) botun yakalayamayacağı gürültü sayılır
        float open = Mathf.Max(ohlc.open, 0.01f);
        float movePercent = Mathf.Abs(ohlc.close - open) / open;
        float profit = botCapital * movePercent * botEfficiency;
        if (profit <= 0f) return;

        GameStatManager.Instance.AddWealth(profit);
        OnBotProfit?.Invoke(profit);

        if (botSuspicionPerCandle > 0f)
            GameStatManager.Instance.AddSuspicion(botSuspicionPerCandle);
    }

    // ==================== a22 / a23 — FORMASYON ENJEKSİYONU ====================

    /// <summary>
    /// ForceChartPatternEffect tarafından çağrılır. Verilen adaylardan birini grafiğe zorlar.
    /// Piyasada zaten bir formasyon işliyorsa BAŞARISIZ olur — grafiğin tek bir formasyon
    /// hattı var, üstüne yazmak mumları bozardı.
    /// </summary>
    public bool ForcePattern(IList<string> candidateIds, float suspicionCost)
    {
        if (chart == null) chart = FindFirstObjectByType<CandlestickChart>();

        if (chart == null)
        {
            Debug.LogWarning("[StockMarket] CandlestickChart sahnede yok — manipülasyon uygulanamadı.");
            return false;
        }

        if (candidateIds == null || candidateIds.Count == 0)
        {
            Debug.LogWarning("[StockMarket] Manipülasyon efektinde formasyon Id'si yazılı değil.");
            return false;
        }

        if (chart.HasActivePattern)
        {
            RaiseTip("Piyasada zaten bir formasyon işliyor — manipülasyon tutmadı.", PatternBias.Neutral);
            return false;
        }

        string patternId = candidateIds[UnityEngine.Random.Range(0, candidateIds.Count)];
        if (!chart.ForcePattern(patternId)) return false;

        if (suspicionCost > 0f && GameStatManager.Instance != null)
            GameStatManager.Instance.AddSuspicion(suspicionCost);

        bool known = MarketIntel.TryGet(patternId, out PatternIntel intel);
        string label = known ? intel.displayName : patternId;
        PatternBias bias = known ? intel.bias : PatternBias.Neutral;
        RaiseTip($"Piyasaya {label} formasyonu işlendi.", bias);

        return true;
    }

    // ==================== a24 / a25 — INSIDER ====================

    /// <summary>
    /// UnlockMarketInsiderEffect tarafından çağrılır. Seviye düşürülmez; a25 alındıktan sonra
    /// a24 tekrar uygulansa bile devlet kaynağı korunur.
    /// </summary>
    public void UnlockInsider(int level)
    {
        insiderLevel = Mathf.Max(insiderLevel, level);
        Debug.Log($"[StockMarket] Insider kaynağı açıldı — seviye {insiderLevel}");
    }

    private void HandlePatternActivated(ChartPattern pattern)
    {
        if (insiderLevel <= 0 || pattern == null) return;
        if (!MarketIntel.TryGet(pattern.Id, out PatternIntel intel)) return;

        //başarısız koşan formasyon ters yöne çözülür; devlet kaynağı GERÇEK sonucu bilir,
        //şirket kaynağı sadece formasyonun kitaptaki yönünü bilir.
        PatternBias bias = intel.bias;
        if (insiderLevel >= 2 && pattern.IsFailedRun)
            bias = Flip(bias);

        string text = insiderLevel >= 2
            ? $"Devlet kaynağı: {intel.displayName} — {MarketIntel.BiasLabel(bias)}, {MarketIntel.ImpactLabel(intel.impact)}" +
              (pattern.IsFailedRun ? " (formasyon boşa çıkacak)" : "")
            : $"Şirket kaynağı: piyasa {MarketIntel.BiasLabel(bias)} yönlü hareket edecek";

        RaiseTip(text, bias);
    }

    private static PatternBias Flip(PatternBias bias)
    {
        if (bias == PatternBias.Bullish) return PatternBias.Bearish;
        if (bias == PatternBias.Bearish) return PatternBias.Bullish;
        return PatternBias.Neutral;
    }

    private void RaiseTip(string text, PatternBias bias)
    {
        OnMarketTip?.Invoke(text, bias, tipDurationSeconds);
        Debug.Log($"[StockMarket] {text}");
    }
}
