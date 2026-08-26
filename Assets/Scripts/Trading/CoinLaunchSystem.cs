using System;
using UnityEngine;

public enum PlayerCoinType
{
    Scam,  //a14 — feed hype'ıyla şişer, zamanında boşaltılmazsa kendi kendine çöker
    Legal  //a18 — yavaş ve sürekli değerlenir, itibar kazandırır, çökmez
}

/// <summary>Oyuncunun kendi çıkardığı coin. Fiyat birim başınadır, arz oyuncunun elindedir.</summary>
[Serializable]
public class PlayerCoin
{
    public PlayerCoinType type;
    public string displayName;
    public float launchPrice;
    public float price;
    public float supply;   //oyuncunun elindeki adet
    public float hype;     //0..1, yalnızca scam coin için
    public bool alive;

    /// <summary>Şu an boşaltılsa cebe girecek para.</summary>
    public float MarketCap => price * supply;
}

/// <summary>
/// COIN ÇIKARMA SİSTEMİ — a14 (scam coin) ve a18 (legal coin) alt dallarının sahibi.
///
/// a14 "feed tabanlı scam coin çıkartma": coin'in fiyatı sosyal medya feed'inde piyasa
/// konuşuluyor olmasına bağlıdır. Feed'de StockMarket post'u geçtikçe hype yükselir,
/// hype yükseldikçe fiyat şişer. Hype ne kadar yüksekse coin'in kendiliğinden çökme
/// riski de o kadar yüksektir — oyuncu ÇÖKMEDEN ÖNCE boşaltmak zorundadır (rug pull).
/// Boşaltma büyük para ve büyük şüphe getirir; beklemek her saniye daha kârlı ve daha
/// risklidir. Oyunun geri kalanıyla bağı buradan gelir: feed'i override eden Media
/// skill'leri (c ağacı) hype'ı doğrudan besler.
///
/// a18 "kendi legal coinini çıkarma": tek seferlik, kalıcı. Fiyat yavaşça bir tavana
/// doğru büyür, her tick para ve az miktarda itibar üretir, çökmez, boşaltılmaz.
///
/// Mantık burada, sunum CoinPanelUI'dadır (RealEstateSystem/PropertyInspectUI ayrımının aynısı).
/// </summary>
public class CoinLaunchSystem : MonoBehaviour
{
    public static CoinLaunchSystem Instance { get; private set; }

    [Header("Scam Coin — Çıkış")]
    [Tooltip("Çıkışta belirlenen birim fiyat.")]
    public float scamLaunchPrice = 0.02f;
    [Tooltip("Oyuncunun elinde doğan toplam arz. Çıkıştaki piyasa değeri = fiyat × arz.")]
    public float scamSupply = 500_000f;

    [Header("Scam Coin — Hype")]
    [Tooltip("Feed'de bu konudan bir post geçtiğinde hype ne kadar artar.")]
    [Range(0f, 0.5f)] public float hypePerPost = 0.05f;
    [Tooltip("Hype'ın saniyede kendiliğinden sönme miktarı.")]
    [Range(0f, 0.5f)] public float hypeDecayPerSecond = 0.02f;
    [Tooltip("Hype'ı besleyen feed konusu.")]
    public TopicType hypeTopic = TopicType.StockMarket;
    [Tooltip("Hype 1'ken fiyat çıkış fiyatının kaç katı olur.")]
    public float maxHypeMultiplier = 12f;

    [Header("Scam Coin — Risk")]
    [Tooltip("Hype 1'ken saniye başına kendiliğinden çökme olasılığı. Hype ile orantılı ölçeklenir.")]
    [Range(0f, 0.2f)] public float collapseChanceAtFullHype = 0.01f;
    [Tooltip("Boşaltmanın taban şüphe bedeli.")]
    public float rugPullBaseSuspicion = 6f;
    [Tooltip("Hype 1'ken taban bedele eklenen ek şüphe.")]
    public float rugPullHypeSuspicion = 14f;

    [Header("Legal Coin")]
    public float legalLaunchPrice = 1f;
    public float legalSupply = 100_000f;
    [Tooltip("Fiyatın saniyedeki büyüme oranı (0.001 = binde 1).")]
    [Range(0f, 0.05f)] public float legalGrowthPerSecond = 0.0015f;
    [Tooltip("Fiyat çıkış fiyatının en fazla kaç katına çıkar.")]
    public float legalMaxMultiplier = 3f;
    [Tooltip("Fiyat çıkış seviyesindeyken saniyede kazandırdığı para. Fiyatla orantılı büyür.")]
    public float legalIncomePerSecond = 15f;
    [Tooltip("Her gelir tick'inde eklenen itibar.")]
    public float legalReputationPerTick = 0.1f;

    private const float INCOME_INTERVAL = 10f; //projedeki diğer pasif gelirlerle aynı ritim

    private PlayerCoin scamCoin;
    private PlayerCoin legalCoin;
    private float incomeTimer;

    /// <summary>Yeni bir coin çıktı.</summary>
    public static event Action<PlayerCoin> OnCoinLaunched;

    /// <summary>Coin boşaltıldı: coin, eline geçen para.</summary>
    public static event Action<PlayerCoin, float> OnCoinRugPulled;

    /// <summary>Coin kendiliğinden çöktü — oyuncu geç kaldı.</summary>
    public static event Action<PlayerCoin> OnCoinCollapsed;

    public PlayerCoin ScamCoin => scamCoin != null && scamCoin.alive ? scamCoin : null;
    public PlayerCoin LegalCoin => legalCoin != null && legalCoin.alive ? legalCoin : null;
    public bool HasLiveScamCoin => ScamCoin != null;

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

    private void OnEnable()
    {
        SocialMediaManager.OnNewPost += HandleNewPost;
    }

    private void OnDisable()
    {
        SocialMediaManager.OnNewPost -= HandleNewPost;
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        TickScamCoin(delta);
        TickLegalCoinPrice(delta);

        incomeTimer += delta;
        if (incomeTimer >= INCOME_INTERVAL)
        {
            incomeTimer = 0f;
            PayLegalCoinIncome();
        }
    }

    // ==================== SCAM COIN (a14) ====================

    /// <summary>
    /// LaunchScamCoinEffect tarafından çağrılır. Zaten yaşayan bir scam coin varsa
    /// yenisini çıkarmaz — aynı anda iki coin'i yönetmek oyuncuyu ödüllendirmiyor,
    /// sadece hype'ı bölüyordu.
    /// </summary>
    public bool LaunchScamCoin()
    {
        if (HasLiveScamCoin)
        {
            Debug.LogWarning("[CoinLaunchSystem] Piyasada zaten bir scam coin var — önce onu boşalt.", this);
            return false;
        }

        scamCoin = new PlayerCoin
        {
            type = PlayerCoinType.Scam,
            displayName = "$" + GenerateTicker(),
            launchPrice = scamLaunchPrice,
            price = scamLaunchPrice,
            supply = scamSupply,
            hype = 0f,
            alive = true
        };

        OnCoinLaunched?.Invoke(scamCoin);
        return true;
    }

    /// <summary>
    /// Tüm arzı o anki fiyattan boşaltır: para cebe girer, coin ölür, şüphe yükselir.
    /// Şüphe hype ile ölçeklenir — büyüyen balonu patlatmak daha çok dikkat çeker.
    /// </summary>
    public float RugPull()
    {
        if (!HasLiveScamCoin) return 0f;
        if (GameStatManager.Instance == null) return 0f;

        float proceeds = scamCoin.MarketCap;
        float suspicion = rugPullBaseSuspicion + rugPullHypeSuspicion * scamCoin.hype;

        scamCoin.alive = false;

        GameStatManager.Instance.AddWealth(proceeds);
        GameStatManager.Instance.AddSuspicion(suspicion);

        OnCoinRugPulled?.Invoke(scamCoin, proceeds);
        return proceeds;
    }

    private void TickScamCoin(float delta)
    {
        if (!HasLiveScamCoin) return;

        //hype sürekli söner; feed beslemezse balon kendiliğinden iner
        scamCoin.hype = Mathf.Max(0f, scamCoin.hype - hypeDecayPerSecond * delta);
        scamCoin.price = scamCoin.launchPrice * (1f + scamCoin.hype * (maxHypeMultiplier - 1f));

        //hype ne kadar yüksekse çökme riski o kadar yüksek
        float collapseChance = collapseChanceAtFullHype * scamCoin.hype * delta;
        if (UnityEngine.Random.value < collapseChance)
            CollapseScamCoin();
    }

    private void CollapseScamCoin()
    {
        scamCoin.alive = false;
        scamCoin.price = 0f;

        OnCoinCollapsed?.Invoke(scamCoin);
    }

    private void HandleNewPost(SocialMediaPost post)
    {
        if (post == null) return;
        if (!HasLiveScamCoin) return;
        if (post.topic != hypeTopic) return;

        scamCoin.hype = Mathf.Clamp01(scamCoin.hype + hypePerPost);
    }

    /// <summary>Coin'e rastgele 3-4 harfli bir sembol üretir; her çıkış farklı görünsün diye.</summary>
    private string GenerateTicker()
    {
        const string letters = "ABCDEFGHIJKLMNOPRSTUVYZ";
        int length = UnityEngine.Random.Range(3, 5);
        char[] ticker = new char[length];

        for (int i = 0; i < length; i++)
            ticker[i] = letters[UnityEngine.Random.Range(0, letters.Length)];

        return new string(ticker);
    }

    // ==================== LEGAL COIN (a18) ====================

    /// <summary>LaunchLegalCoinEffect tarafından çağrılır. Tek seferlik, kalıcı.</summary>
    public bool LaunchLegalCoin()
    {
        if (legalCoin != null && legalCoin.alive) return false;

        legalCoin = new PlayerCoin
        {
            type = PlayerCoinType.Legal,
            displayName = "$" + GenerateTicker(),
            launchPrice = legalLaunchPrice,
            price = legalLaunchPrice,
            supply = legalSupply,
            hype = 0f,
            alive = true
        };

        OnCoinLaunched?.Invoke(legalCoin);
        return true;
    }

    private void TickLegalCoinPrice(float delta)
    {
        if (LegalCoin == null) return;

        float ceiling = legalCoin.launchPrice * legalMaxMultiplier;
        if (legalCoin.price >= ceiling) return;

        legalCoin.price = Mathf.Min(ceiling, legalCoin.price * (1f + legalGrowthPerSecond * delta));
    }

    private void PayLegalCoinIncome()
    {
        if (LegalCoin == null) return;
        if (GameStatManager.Instance == null) return;

        //gelir fiyatla orantılı: coin değerlendikçe kazanç da büyür
        float priceRatio = legalCoin.launchPrice > 0f ? legalCoin.price / legalCoin.launchPrice : 1f;
        float income = legalIncomePerSecond * priceRatio * INCOME_INTERVAL;

        GameStatManager.Instance.AddWealth(income);

        if (legalReputationPerTick != 0f)
            GameStatManager.Instance.AddReputation(legalReputationPerTick);
    }
}
