using UnityEngine;

/// <summary>
/// Ekin deposu sistemini açar: oyuncu tarım bölgesine depo kurup ürün biriktirebilir ve
/// piyasaya satabilir hale gelir (CropDepotSystem).
///
/// Yalnızca YETKİYİ verir; depoyu kurma modunu skill'in aktif yeteneğine bağlı
/// EnterCropDepotPlacementEffect açar.
///
/// UYARI: sistemin EKRANI YOK — stok, fiyat ve satış düğmesi hiçbir UI tarafından
/// gösterilmiyor. Depo haritada görünür ve üretir, ama oyuncu stoğunu satamaz.
/// Bkz. Assets/Scripts/Map/crop-depot-readme.md
/// </summary>
[System.Serializable]
public class UnlockCropDepotEffect : SkillEffect
{
    public override void Apply()
    {
        if (CropDepotSystem.Instance != null)
        {
            CropDepotSystem.Instance.Unlock();
            return;
        }

        Debug.LogWarning("[UnlockCropDepotEffect] CropDepotSystem sahnede yok — ekin deposu açılamadı.");
    }
}
