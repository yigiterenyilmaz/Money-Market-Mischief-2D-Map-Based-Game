using UnityEngine;

/// <summary>
/// Ekini zehirler (a31 — "zehirleme"): ürüne katkı karıştırılır, depo üretimi kalıcı olarak
/// hızlanır. Bedeli tek seferlik itibar kaybı ve üretim işledikçe biriken şüphedir.
///
/// Şüphe yalnızca üretim SÜRERKEN birikir; depolar dolup üretim durduğunda ceza da durur.
/// Böylece "zehirle ve unut" değil, işleyen bir operasyonun sürekli riski olur.
///
/// Çarpan ve cezalar CropDepotSystem üzerinde ayarlanır (poisonYieldMultiplier,
/// poisonSuspicionPerTick, poisonTickSeconds, poisonReputationHit).
/// </summary>
[System.Serializable]
public class PoisonCropsEffect : SkillEffect
{
    public override void Apply()
    {
        if (CropDepotSystem.Instance != null)
        {
            CropDepotSystem.Instance.ApplyPoisoning();
            return;
        }

        Debug.LogWarning("[PoisonCropsEffect] CropDepotSystem sahnede yok — zehirleme uygulanamadı.");
    }
}
