using UnityEngine;

/// <summary>
/// Depo yerleştirme modunu açar. Skill'in AKTİF yeteneğine bağlanır: oyuncu ağaçta a26
/// node'una tıklayınca haritada tarım bölgesindeki bir tile'ı seçip depo kurabilir.
///
/// Pasif efekt (UnlockCropDepotEffect) yalnızca yetkiyi verir; bu efekt her tıklamada modu
/// açar — UnlockRegionConversionEffect / EnterRegionConversionEffect ayrımının aynısı.
/// </summary>
[System.Serializable]
public class EnterCropDepotPlacementEffect : SkillEffect
{
    public override void Apply()
    {
        if (CropDepotSystem.Instance == null)
        {
            Debug.LogWarning("[EnterCropDepotPlacementEffect] CropDepotSystem sahnede yok.");
            return;
        }

        CropDepotSystem.Instance.EnterPlacementMode();
    }
}
