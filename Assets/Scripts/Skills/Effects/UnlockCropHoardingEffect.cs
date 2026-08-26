using UnityEngine;

/// <summary>
/// Stokçuluğu açar (a28 pasif kısmı): depo kapasitesi çarpanla büyür, böylece oyuncu ürünü
/// satmayıp bekletebilir.
///
/// Tek başına para kazandırmaz; anlamı, aynı skill'in AKTİF yeteneği olan talep dalgasıyla
/// (TriggerDemandWaveEffect) birlikte ortaya çıkar — dalganın fiyat çarpanı elde tutulan
/// stokla orantılıdır, yani depolamak doğrudan kâra dönüşür.
///
/// Çarpan CropDepotSystem.hoardingCapacityMultiplier üzerinden ayarlanır.
/// </summary>
[System.Serializable]
public class UnlockCropHoardingEffect : SkillEffect
{
    public override void Apply()
    {
        if (CropDepotSystem.Instance != null)
        {
            CropDepotSystem.Instance.UnlockHoarding();
            return;
        }

        Debug.LogWarning("[UnlockCropHoardingEffect] CropDepotSystem sahnede yok — stokçuluk açılamadı.");
    }
}
