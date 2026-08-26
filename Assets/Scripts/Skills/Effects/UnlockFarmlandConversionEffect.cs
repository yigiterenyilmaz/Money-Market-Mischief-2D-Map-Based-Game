using UnityEngine;

/// <summary>
/// Kırsaldan kente göçü açar (a29): bölge dönüşümü artık TARIM arazisini de kaynak olarak
/// kabul eder, yani tarlaya sınır çizip şehre çevirmek mümkün olur.
///
/// İki şey birden yapar, çünkü ikisi de gerekli:
///   * MapDecorPlacer'a tarımın dönüştürülebilir olduğunu söyler — yoksa çizim maskesi
///     tarlaları "kısıtlı" gösterir;
///   * RegionConversionSystem'de Cities hedefini açar — a38'i almamış bir oyuncunun da bu
///     skill'le şehir kurabilmesi için.
///
/// Mekaniğin bedeli kendiliğinden doğar: tarlayı şehre çevirmek o tarlayı besleyen ekin
/// deposunun verimini düşürür (CropDepotSystem harita değişince hızları yeniden hesaplar).
/// Ayrıca dönüşen karelerin parsel mozaiği silinir, yoksa yeni şehrin altından ekin görünürdü.
/// </summary>
[System.Serializable]
public class UnlockFarmlandConversionEffect : SkillEffect
{
    public override void Apply()
    {
        if (MapDecorPlacer.Instance != null)
        {
            MapDecorPlacer.Instance.SetAgriculturalConvertible(true);
        }
        else
        {
            Debug.LogWarning("[UnlockFarmlandConversionEffect] MapDecorPlacer sahnede yok — " +
                             "tarım arazisi dönüştürülebilir yapılamadı.");
        }

        if (RegionConversionSystem.Instance != null)
        {
            RegionConversionSystem.Instance.Unlock(MapDecorPlacer.ConvertTarget.Cities);
        }
        else
        {
            Debug.LogWarning("[UnlockFarmlandConversionEffect] RegionConversionSystem sahnede yok — " +
                             "şehir dönüşümü açılamadı.");
        }
    }
}
