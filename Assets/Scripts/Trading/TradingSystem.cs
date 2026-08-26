using System;
using UnityEngine;

/// <summary>
/// TRADE SİSTEMİ — mum grafiğindeki canlı fiyattan gerçek parayla al/sat.
///
/// Skill ile açılır (a11 "Welcome To The Jungle" → UnlockTradingEffect). Açılana kadar
/// grafik paneli oyuncuya kapalıdır; açıldıktan sonra node'un aktif yeteneği
/// (OpenTradingPanelEffect) paneli açar.
///
/// POZİSYON MODELİ — tek varlık, tek birikmiş pozisyon:
///   alım   : para → adet (o anki fiyattan), maliyet havuzuna eklenir
///   satış  : adet → para, maliyet havuzundan orantılı pay düşülür
///   kâr    : (adet × güncel fiyat) − ödenen maliyet
/// Ortalama maliyet ayrı bir alan olarak tutulmaz, maliyet havuzundan türetilir —
/// böylece kısmi satışta yuvarlama hatası birikmez.
///
/// Fiyat kaynağı CandlestickChart'tır ve panel kapalıyken de akmaya devam eder:
/// pozisyonu açık bırakıp haritaya dönmek bilinçli olarak risklidir.
///
/// Bu sınıf yalnızca mantık taşır; sunum TradingPanelUI'dadır
/// (RealEstateSystem/PropertyInspectUI ayrımının aynısı).
/// </summary>
public class TradingSystem : MonoBehaviour
{
    public static TradingSystem Instance { get; private set; }

    [Header("Referanslar")]
    [Tooltip("Fiyat kaynağı ve panel sahibi. Boş bırakılırsa sahnede aranır.")]
    public CandlestickChart chart;

    [Header("İşlem")]
    [Tooltip("Her işlemden kesilen komisyon oranı (0.01 = %1). Denge kararı kullanıcıya ait; " +
             "varsayılan 0 = komisyonsuz.")]
    [Range(0f, 0.1f)] public float tradeFeeRate = 0f;

    [Tooltip("Al/Sat kutusunda açılışta yazılı gelen miktar.")]
    public float defaultOrderAmount = 100f;

    [Tooltip("Skill açıldıktan sonra ekranın sol üstündeki panel butonu görünür kalsın mı. " +
             "Kapalıysa panele yalnızca skill ağacındaki node'dan girilir.")]
    public bool showPanelButtonWhenUnlocked = true;

    private bool unlocked;
    private float quantity;      //elde tutulan adet
    private float costBasis;     //bu adet için ödenen toplam para (komisyon dahil)

    /// <summary>Trade sistemi skill ile açıldı.</summary>
    public static event Action OnUnlocked;

    /// <summary>Pozisyon değişti (alım, satım). UI kendini yeniler.</summary>
    public static event Action OnPositionChanged;

    /// <summary>Panel açıldı (true) veya kapandı (false).</summary>
    public static event Action<bool> OnPanelToggled;

    public bool IsUnlocked => unlocked;
    public float Quantity => quantity;
    public float CostBasis => costBasis;

    /// <summary>Elde adet varsa ortalama alış fiyatı, yoksa 0.</summary>
    public float AverageEntryPrice => quantity > 0f ? costBasis / quantity : 0f;

    /// <summary>Grafiğin o anki fiyatı. Grafik yoksa 0.</summary>
    public float CurrentPrice => chart != null ? chart.CurrentPrice : 0f;

    /// <summary>Pozisyonun güncel piyasa değeri.</summary>
    public float PositionValue => quantity * CurrentPrice;

    /// <summary>Henüz satılmamış kâr/zarar.</summary>
    public float UnrealizedProfit => PositionValue - costBasis;

    public bool IsPanelOpen => chart != null && chart.IsPanelOpen;

    private void Awake()
    {
        //DİKKAT: Managers objesi paylaşımlı — Destroy(gameObject) buradaki TÜM manager'ları
        //silerdi. Yalnızca bu kopyayı sil.
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        //KRİTİK: grafik referansı Start'ta DEĞİL burada çözülür. Bu bileşenler Managers
        //prefab'ında, grafik ise Canvas prefab'ında duruyor; alan elle atanamıyor. Panelleri
        //kuran UI'lar Start'ta bu alanı okuyor ve Unity bileşen Start sırasını garanti
        //etmiyor — tüm Awake'ler tüm Start'lardan önce koştuğu için tek güvenli yer burası.
        if (chart == null)
            chart = FindFirstObjectByType<CandlestickChart>();

        if (chart == null)
            Debug.LogWarning("[TradingSystem] Sahnede CandlestickChart yok — fiyat kaynağı bulunamadı.", this);
    }

    // ==================== AÇILIŞ ====================

    /// <summary>UnlockTradingEffect tarafından çağrılır. Trade panelini oyuncuya açar.</summary>
    public void Unlock()
    {
        if (unlocked) return;

        unlocked = true;

        if (chart != null && showPanelButtonWhenUnlocked)
            chart.SetPanelButtonVisible(true);

        OnUnlocked?.Invoke();
    }

    // ==================== PANEL ====================

    /// <summary>
    /// Paneli açar. Ağaçtaki node'dan tetiklendiği için önce skill ağacını kapatır —
    /// aksi halde panel ağacın altında kalır ve kamera kilitli kalırdı.
    /// </summary>
    public void OpenPanel()
    {
        if (!unlocked)
        {
            Debug.LogWarning("[TradingSystem] Trade henüz açılmadı — panel açılamaz.", this);
            return;
        }

        if (chart == null)
        {
            Debug.LogWarning("[TradingSystem] CandlestickChart yok — panel açılamaz.", this);
            return;
        }

        if (UImanager.Instance != null)
            UImanager.Instance.OnSkillTreeClose();

        chart.SetPanelOpen(true);
        OnPanelToggled?.Invoke(true);
    }

    public void ClosePanel()
    {
        if (chart == null) return;

        chart.SetPanelOpen(false);
        OnPanelToggled?.Invoke(false);
    }

    // ==================== AL / SAT ====================

    /// <summary>
    /// Verilen para miktarıyla o anki fiyattan alım yapar.
    /// Komisyon paradan düşülür, kalan miktar adete çevrilir.
    /// </summary>
    public bool Buy(float amount)
    {
        if (!unlocked) return false;
        if (amount <= 0f) return false;

        float price = CurrentPrice;
        if (price <= 0f) return false;

        if (GameStatManager.Instance == null) return false;
        if (!GameStatManager.Instance.HasEnoughWealth(amount)) return false;
        if (!GameStatManager.Instance.TrySpendWealth(amount)) return false;

        float net = amount * (1f - tradeFeeRate);
        quantity += net / price;
        costBasis += amount; //komisyon da maliyettir, kâr hesabına dahil olmalı

        OnPositionChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Elden belirtilen adedi o anki fiyattan satar. Maliyet havuzundan orantılı pay düşer.
    /// Dönen değer bu satıştan realize edilen kâr/zarardır.
    /// </summary>
    public float Sell(float sellQuantity)
    {
        if (!unlocked) return 0f;
        if (sellQuantity <= 0f) return 0f;
        if (quantity <= 0f) return 0f;
        if (GameStatManager.Instance == null) return 0f;

        float price = CurrentPrice;
        if (price <= 0f) return 0f;

        sellQuantity = Mathf.Min(sellQuantity, quantity);

        float gross = sellQuantity * price;
        float net = gross * (1f - tradeFeeRate);

        //satılan adedin maliyetteki payı
        float costPortion = costBasis * (sellQuantity / quantity);

        quantity -= sellQuantity;
        costBasis -= costPortion;

        //son adet de satıldıysa kuyrukta kalan küsuratı sıfırla
        if (quantity <= 0.000001f)
        {
            quantity = 0f;
            costBasis = 0f;
        }

        GameStatManager.Instance.AddWealth(net);

        OnPositionChanged?.Invoke();
        return net - costPortion;
    }

    /// <summary>Tüm pozisyonu kapatır. Dönen değer realize edilen kâr/zarardır.</summary>
    public float SellAll()
    {
        return Sell(quantity);
    }
}
