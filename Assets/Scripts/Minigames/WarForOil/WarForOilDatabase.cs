using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/WarForOil/Database")]
public class WarForOilDatabase : ScriptableObject
{
    [Header("Ülkeler")]
    public List<WarForOilCountry> countries;

    [Header("Ülke Rotasyonu")]
    public int visibleCountryCount = 3; //UI'da aynı anda görünen ülke sayısı
    public float rotationInterval = 90f; //ülke değişim aralığı (saniye)

    [Header("Baskı Ayarları")]
    public float pressureCooldown = 20f; //baskı başarısız olunca bekleme süresi (saniye)
    public float politicalInfluenceMultiplier = 0.01f; //siyasi nüfuzun başarı şansına çarpanı

    [Header("Savaş Eventleri")]
    public List<WarForOilEvent> events; //savaş sırasında tetiklenen eventler

    [Header("Event Grupları")]
    public List<WTETWCEventGroup> eventGroups; //event ilişki grupları (ağırlık + karşılıklı dışlama)
    public List<OFPCEventGroup> ofpcEventGroups; //Oil for Peace concept event grupları

    [Header("Zincir")]
    [Range(0f, 1f)] public float chainDoubleChance = 0.5f; //3'lü döngüde 2 chain slotu çıkma olasılığı (0.5 = %50)

    [Header("Savaş Ayarları")]
    public float warDuration = 300f; //savaş süresi (saniye)
    public float eventInterval = 80f; //savaş sırasında normal event aralığı (saniye)
    public float chainEventInterval = 40f; //zincir aktifken event aralığı (saniye)
    public float initialSupportStat = 50f; //destek stat başlangıç değeri

    [Header("Sonuç Ayarları")]
    public float baseWinChance = 0.375f; //temel savaş kazanma şansı (invasionDifficulty ve support'a göre değişir)
    public float supportWinBonus = 0.625f; //tam destek vermenin kazanma şansına max katkısı
    [Range(0f, 1f)] public float supportRewardRatio = 0.8f; //support 100 olsa bile baseReward'ın max bu oranı alınır
    public float minWinChance = 0.1f; //minimum kazanma şansı
    public float maxWinChance = 0.9f; //maximum kazanma şansı

    [Header("Ateşkes Ayarları")]
    public float ceasefireMinSupport = 40f; //ateşkes yapabilmek için minimum destek değeri
    public float ceasefirePenalty = 100f; //en kötü ateşkesteki para kaybı (minSupport'ta)
    public float ceasefireMaxReward = 200f; //en iyi ateşkesteki max kazanç çarpanı (support 100'de)

    [Header("Ödül/Ceza Ayarları")]
    public float warLossPenalty = 200f; //savaş kaybedildiğinde para kaybı
    public float warLossPoliticalPenalty = 20f; //savaş kaybedildiğinde siyasi nüfuz düşüşü
    public float warLossSuspicionIncrease = 15f; //savaş kaybedildiğinde şüphe artışı

    [Header("Rakip İşgal Ayarları")]
    public float rivalInvasionMinWarTime = 60f; //rakip işgalin en erken tetiklenebileceği savaş süresi (saniye)
    [Range(0f, 1f)] public float rivalInvasionChance = 0.3f; //her event check'te rakip işgal tetiklenme şansı
    [Range(0f, 1f)] public float rivalDealRewardRatio = 0.6f; //anlaşmada oyuncuya kalan ödül oranı
    public float rivalDealEndDelay = 10f; //anlaşma kabul edilince savaş bitiş gecikmesi (saniye)
    public float initialCornerGrabStat = 50f; //köşe kapma stat başlangıç değeri (0-100, 50 = eşit)
    public WarForOilEvent rivalOfferEvent; //rakip işgal teklif event'i (kabul/red seçenekleri)
    public List<WarForOilEvent> cornerGrabEvents; //köşe kapma yarışı event havuzu

    [Header("Toplum Tepkisi Ayarları")]
    public float protestMinWarTime = 90f; //toplum tepkisinin en erken tetiklenebileceği savaş süresi (saniye)
    [Range(0f, 1f)] public float protestChance = 0.25f; //her event check'te toplum tepkisi tetiklenme şansı
    public float initialProtestStat = 30f; //toplum tepkisi başlangıç değeri (0-100)
    public float protestFailThreshold = 80f; //bu değerin üstünde savaş otomatik ateşkese bağlanır
    public float protestSuccessThreshold = 10f; //bu değerin altına düşürülürse tepki bastırılmış sayılır
    public float protestDriftInterval = 3f; //pasif drift tick aralığı (saniye)
    public float protestDriftDivisor = 10f; //drift = son choice modifier / divisor (her tick'te uygulanır)
    public WarForOilEvent protestTriggerEvent; //toplum tepkisi başlangıç event'i (gösterilere başlandı)
    public List<WarForOilEvent> protestEvents; //toplum tepkisi event havuzu

    [Header("Vandalizm Ayarları")]
    public WarForOilEvent vandalismTriggerEvent; //vandalizm başlangıç event'i
    [Range(0f, 1f)] public float vandalismChance = 0.2f; //protest aktifken her event check'te vandalizm tetiklenme şansı
    public VandalismLevel initialVandalismLevel = VandalismLevel.Light; //otomatik tetiklemede başlangıç seviyesi
    public float vandalismDamageInterval = 5f; //hasar tick aralığı (saniye)
    public float vandalismLightDamage = 5f; //hafif seviyede tick başına wealth kaybı
    public float vandalismModerateDamage = 15f; //orta seviyede tick başına wealth kaybı
    public float vandalismHeavyDamage = 30f; //ağır seviyede tick başına wealth kaybı
    public float vandalismSevereDamage = 50f; //şiddetli seviyede tick başına wealth kaybı

    [Header("Medya Takibi Ayarları")]
    public float mediaPursuitMinWarTime = 120f; //medya takibinin en erken tetiklenebileceği savaş süresi (saniye)
    [Range(0f, 1f)] public float mediaPursuitChance = 0.2f; //her event check'te medya takibi tetiklenme şansı
    public WarForOilEvent mediaPursuitTriggerEvent; //medya takibi başlangıç event'i
    public MediaPursuitLevel initialMediaPursuitLevel = MediaPursuitLevel.Low; //otomatik tetiklemede başlangıç seviyesi
    public List<WarForOilEvent> mediaPursuitLevel1Events; //Low state event havuzu
    public List<WarForOilEvent> mediaPursuitLevel2Events; //Medium state event havuzu
    public List<WarForOilEvent> mediaPursuitLevel3Events; //High state event havuzu
    public float mediaPursuitTickInterval = 5f; //periyodik etki tick aralığı (saniye)
    //Low seviye etkileri
    public float mediaPursuitLowReputationPerTick = 1f; //tick başına itibar kaybı
    public float mediaPursuitLowSuspicionPerTick = 0.5f; //tick başına şüphe artışı
    //Medium seviye etkileri
    public float mediaPursuitMediumReputationPerTick = 2f;
    public float mediaPursuitMediumSuspicionPerTick = 1.5f;
    //High seviye etkileri
    public float mediaPursuitHighReputationPerTick = 4f;
    public float mediaPursuitHighSuspicionPerTick = 3f;
}
