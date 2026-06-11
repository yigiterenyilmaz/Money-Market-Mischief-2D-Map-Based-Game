using UnityEngine;

public enum InvestmentProductType
{
    Element,
    Mineral
}

public enum StreakBreakerType
{
    Rising,  //yükselen streak kırıcı — her alımda profitChance düşer
    Falling  //düşen streak kırıcı — her alımda profitChance artar
}

[CreateAssetMenu(menuName = "SkillTree/Products/InvestmentProduct")]
public class InvestmentProduct : ScriptableObject
{
    public string id;
    public string displayName;
    public InvestmentProductType productType;
    public Sprite icon;

    public int cost; //satın alma fiyatı
    [Range(0f, 1f)] public float profitChance = 0.8f; //kâr olasılığı (0=hep zarar, 1=hep kâr)
    public bool isStreakBreakerActive = false; //streak kırıcı aktif mi
    public StreakBreakerType streakBreakerType = StreakBreakerType.Rising;
    public float streakBreakerMin = 4f; //her alımda minimum değişim (%, profitChance'e uygulanır)
    public float streakBreakerMax = 6f; //her alımda maximum değişim (%, profitChance'e uygulanır)
    public float maxProfitPercent = 40f; //maksimum kâr potansiyeli (%)
    public float maxLossPercent = 25f; //maksimum zarar potansiyeli (%)
    public float minReachTime = 120f; //potansiyele tahmini minimum varış süresi (sn)
    public float maxReachTime = 210f; //potansiyele tahmini maximum varış süresi (sn)
    public float volatility = 3f; //tick başına fiyat salınım büyüklüğü (%)
    public float postPotentialDrift = -5f; //potansiyelden sonra sapma yönü (%, + yukarı, - aşağı)
    public float postPotentialTimeout = 60f; //potansiyelde satılmazsa drift başlama süresi (sn)

    public float idleOscillationMin = -5f; //piyasa salınım alt sınır (%)
    public float idleOscillationMax = 5f;  //piyasa salınım üst sınır (%)

    public bool hasLimitedAvailability = false; //piyasada her zaman bulunmaz
    [Range(0f, 1f)] public float availabilityChance = 0.3f; //piyasada bulunma oranı (zamanın yüzdesi)
    public float availabilityCycleDuration = 120f; //faz döngü süresi (sn)
    public float minAvailableDuration = 15f; //minimum alınabilir kalma süresi (sn)
}
