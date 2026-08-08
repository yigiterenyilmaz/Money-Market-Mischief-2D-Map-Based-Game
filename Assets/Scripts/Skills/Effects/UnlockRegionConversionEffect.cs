using UnityEngine;

/// <summary>
/// Bölge dönüşümünü açar: oyuncu haritadaki boş araziyi (Urban) seçip şehre ya da sanayiye
/// çevirebilir hale gelir. Hedef başına ayrı açılır — a38 şehri, a40 sanayiyi açar.
/// </summary>
[System.Serializable]
public class UnlockRegionConversionEffect : SkillEffect
{
    [Tooltip("Bu skill hangi dönüşümü açar.")]
    public MapDecorPlacer.ConvertTarget target = MapDecorPlacer.ConvertTarget.Cities;

    public override void Apply()
    {
        if (RegionConversionSystem.Instance != null)
        {
            RegionConversionSystem.Instance.Unlock(target);
            return;
        }

        Debug.LogWarning("[UnlockRegionConversionEffect] RegionConversionSystem sahnede yok — " +
                         "bölge dönüşümü açılamadı.");
    }
}
