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

    [Header("Savaş Ayarları")]
    public float warDuration = 300f; //savaş süresi (saniye)
    public float eventInterval = 15f; //savaş sırasında event kontrol aralığı (saniye)
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
}
