using UnityEngine;

/// <summary>
/// Aldatıcı ambalajı açar (a32 — "yumaklı files"): oyuncunun elden yaptığı satışlarda birim
/// fiyat yükselir, karşılığında her satış itibar yakar. Tavuk zincirinin (a27) alımlarına
/// işlemez — zincir bir iş ortağıdır, hileyi fark eder.
///
/// YORUM UYARISI: a32'nin asset notu yalnızca "yumaklı files" yazıyor ve ne kastedildiği
/// çözülemedi. Buradaki okuma, skill'in ağaçtaki KONUMUNDAN çıkarıldı — a28 "tüketiciye
/// sokma" ve a31 "zehirleme" kolunun devamı olduğu için tüketiciyi kandıran bir satış
/// hilesi varsayıldı: file içinde ürünü kabartıp olduğundan dolu göstermek.
///
/// Yanlışsa maliyeti düşüktür: bu efekt sınıfı, CropDepotSystem'deki iki alan
/// (packagingSaleMultiplier, packagingReputationPerSale) ve A32-.asset'teki tek bağlantı
/// değişir. Doğru mekanik söylenirse dakikalar içinde yeniden bağlanır.
/// </summary>
[System.Serializable]
public class UnlockDeceptivePackagingEffect : SkillEffect
{
    public override void Apply()
    {
        if (CropDepotSystem.Instance != null)
        {
            CropDepotSystem.Instance.UnlockDeceptivePackaging();
            return;
        }

        Debug.LogWarning("[UnlockDeceptivePackagingEffect] CropDepotSystem sahnede yok — " +
                         "aldatıcı ambalaj açılamadı.");
    }
}
