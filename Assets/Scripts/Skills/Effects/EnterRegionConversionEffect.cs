using UnityEngine;

/// <summary>
/// Bölge çizim modunu açar. Skill'in AKTİF yeteneğine bağlanır: oyuncu ağaçta node'a
/// tıklayınca harita moduna girilir ve sınır çizilmeye başlanabilir.
///
/// Pasif efekt (UnlockRegionConversionEffect) yalnızca yetkiyi verir; bu efekt ise
/// her tıklamada modu açar — bu yüzden ayrı bir sınıf.
/// </summary>
[System.Serializable]
public class EnterRegionConversionEffect : SkillEffect
{
    [Tooltip("Hangi dönüşüm modu açılacak.")]
    public MapDecorPlacer.ConvertTarget target = MapDecorPlacer.ConvertTarget.Cities;

    public override void Apply()
    {
        if (RegionConversionSystem.Instance == null)
        {
            Debug.LogWarning("[EnterRegionConversionEffect] RegionConversionSystem sahnede yok.");
            return;
        }

        RegionConversionSystem.Instance.EnterMode(target);
    }
}
