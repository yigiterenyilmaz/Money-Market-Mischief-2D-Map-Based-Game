using System;
using UnityEngine;

/// <summary>
/// FİYAT SAVAŞI — a15 "sidik yarışı".
///
/// Oyuncu bir yöne baskı uygular: sata sata fiyatı düşürmeye ya da ala ala yükseltmeye
/// çalışır. Karşı tarafta aynı anda ters yöne basan bir rakip vardır. Her baskı para
/// yakar ve rakibin gücünü aşındırır; rakip de aralıksız geri iter. İki seçenek kalır:
/// gücünü kırana kadar basmaya devam etmek ya da çekilmek.
///
/// Rakibin gücü GİZLİDİR. Oyuncu yalnızca bulanık bir geri bildirim görür ("rakip
/// sağlam duruyor" / "zayıflıyor"), çünkü oyunun tamamı bu belirsizliğin üstünde duruyor:
/// bilinen bir sayıyı tüketmek karar değil, hesap olurdu. Her baskı bir öncekinden pahalıdır,
/// yani beklemek de basmak da bedelli.
///
/// Kazanılırsa rakip masadaki parasını bırakıp çekilir ve fiyat oyuncunun yönünde sert
/// bir harekete geçer. Çekilirsen yakılan para gider.
///
/// Mantık burada, sunum PriceWarUI'dadır.
/// </summary>
public class PriceWarSystem : MonoBehaviour
{
    public static PriceWarSystem Instance { get; private set; }

    [Header("Referanslar")]
    [Tooltip("Zafer anında formasyon zorlamak için. Boş bırakılırsa sahnede aranır.")]
    public CandlestickChart chart;

    [Header("Rakip")]
    [Tooltip("Rakibin gizli gücünün alt sınırı.")]
    public float rivalStrengthMin = 4000f;
    [Tooltip("Rakibin gizli gücünün üst sınırı.")]
    public float rivalStrengthMax = 14000f;
    [Tooltip("Rakibin saniyede kendini toparlama miktarı. Beklemek rakibi güçlendirir.")]
    public float rivalRecoveryPerSecond = 25f;

    [Header("Baskı")]
    [Tooltip("İlk baskının maliyeti.")]
    public float basePushCost = 500f;
    [Tooltip("Her baskıda maliyetin artış oranı (0.15 = her seferinde %15 pahalı).")]
    [Range(0f, 1f)] public float pushCostGrowth = 0.15f;
    [Tooltip("Bir baskının rakibin gücünden götürdüğü miktar (harcanan paranın katı).")]
    public float pushPowerPerWealth = 1.4f;
    [Tooltip("Baskı gücüne binen rastgelelik (0.2 = ±%20).")]
    [Range(0f, 0.6f)] public float pushVariance = 0.2f;

    [Header("Sonuç")]
    [Tooltip("Kazanınca yakılan paranın kaç katı geri döner.")]
    public float victoryMultiplier = 1.8f;
    [Tooltip("Kazanınca eklenen şüphe — piyasayı tek başına kırmak dikkat çeker.")]
    public float victorySuspicion = 8f;
    [Tooltip("Zafer anında grafiğe zorlanan formasyon (yukarı baskı).")]
    public string victoryPatternUp = "D1_Pump";
    [Tooltip("Zafer anında grafiğe zorlanan formasyon (aşağı baskı).")]
    public string victoryPatternDown = "D2_Dump";

    private bool unlocked;
    private bool warActive;
    private bool pushingUp;
    private bool directionChosen;
    private float rivalStrength;    //savaş başındaki gizli toplam
    private float rivalRemaining;   //kalan güç
    private float spent;            //oyuncunun bu savaşta yaktığı toplam para
    private int pushCount;

    /// <summary>Savaş durumu değişti (başladı, baskı uygulandı, rakip toparlandı).</summary>
    public static event Action OnWarChanged;

    /// <summary>Savaş bitti: kazanıldı mı, oyuncunun net kâr/zararı.</summary>
    public static event Action<bool, float> OnWarEnded;

    public bool IsUnlocked => unlocked;
    public bool WarActive => warActive;
    public bool DirectionChosen => directionChosen;
    public bool PushingUp => pushingUp;
    public float Spent => spent;
    public float NextPushCost => basePushCost * (1f + pushCostGrowth * pushCount);

    /// <summary>Kazanılırsa eline geçecek toplam. Çekilirsen bu kadarını kaybediyorsun.</summary>
    public float PotentialPayout => spent * victoryMultiplier;

    /// <summary>
    /// Rakibin ne kadar yıprandığı — 0 kırıldı, 1 hiç dokunulmadı. UI bunu sayı olarak
    /// DEĞİL, bulanık bir cümleye çevirerek gösterir.
    /// </summary>
    public float RivalIntegrity => rivalStrength > 0f ? Mathf.Clamp01(rivalRemaining / rivalStrength) : 0f;

    private void Awake()
    {
        //DİKKAT: Managers objesi paylaşımlı — Destroy(gameObject) oradaki tüm manager'ları silerdi.
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (chart == null)
            chart = FindFirstObjectByType<CandlestickChart>();
    }

    private void Update()
    {
        if (!warActive) return;

        //rakip sürekli geri itiyor: oyuncu duraklarsa kaybettiği zemini geri alır
        rivalRemaining = Mathf.Min(rivalStrength, rivalRemaining + rivalRecoveryPerSecond * Time.deltaTime);
    }

    // ==================== AÇILIŞ ====================

    /// <summary>UnlockPriceWarEffect tarafından çağrılır.</summary>
    public void Unlock()
    {
        unlocked = true;
    }

    // ==================== SAVAŞ ====================

    /// <summary>
    /// StartPriceWarEffect tarafından çağrılır. Yeni bir rakip masaya oturur; gücü
    /// gizli ve her savaşta farklıdır.
    /// </summary>
    public bool StartWar()
    {
        if (!unlocked) return false;
        if (warActive) return false;

        rivalStrength = UnityEngine.Random.Range(rivalStrengthMin, rivalStrengthMax);
        rivalRemaining = rivalStrength;
        spent = 0f;
        pushCount = 0;
        directionChosen = false;
        warActive = true;

        OnWarChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Bir baskı uygular. İlk baskı yönü belirler; sonrakiler aynı yönde olmak zorundadır —
    /// savaşın ortasında taraf değiştirmek "gücünü kırana kadar bas" kararını anlamsız kılardı.
    /// </summary>
    public bool Push(bool up)
    {
        if (!warActive) return false;
        if (directionChosen && up != pushingUp) return false;
        if (GameStatManager.Instance == null) return false;

        float cost = NextPushCost;
        if (!GameStatManager.Instance.HasEnoughWealth(cost)) return false;
        if (!GameStatManager.Instance.TrySpendWealth(cost)) return false;

        if (!directionChosen)
        {
            pushingUp = up;
            directionChosen = true;
        }

        spent += cost;
        pushCount++;

        float variance = UnityEngine.Random.Range(1f - pushVariance, 1f + pushVariance);
        rivalRemaining -= cost * pushPowerPerWealth * variance;

        if (rivalRemaining <= 0f)
        {
            EndWar(true);
            return true;
        }

        OnWarChanged?.Invoke();
        return true;
    }

    /// <summary>Çekilir. Yakılan para gider, rakip masada kalır.</summary>
    public void Withdraw()
    {
        if (!warActive) return;

        EndWar(false);
    }

    private void EndWar(bool won)
    {
        warActive = false;

        float net = -spent; //baskı parası zaten harcandı

        if (won && GameStatManager.Instance != null)
        {
            float payout = PotentialPayout;
            GameStatManager.Instance.AddWealth(payout);
            GameStatManager.Instance.AddSuspicion(victorySuspicion);
            net += payout;

            //rakip çekilince fiyat oyuncunun bastığı yöne boşalır
            if (chart != null)
                chart.ForcePattern(pushingUp ? victoryPatternUp : victoryPatternDown);
        }

        OnWarEnded?.Invoke(won, net);
        OnWarChanged?.Invoke();
    }
}
