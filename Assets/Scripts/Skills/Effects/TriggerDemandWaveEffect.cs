using UnityEngine;

/// <summary>
/// Talep dalgası başlatır (a28 aktif yeteneği — "sosyal medyada akım yaratıp insanlara
/// alışveriş yaptırma, feede düşer").
///
/// İki şey birden yapar: sosyal medyada bir akım başlatır (SocialMediaManager.SetPlayerOverride
/// → gönderiler feed'e düşer) ve ürün fiyatını geçici olarak yukarı çeker. Çarpanın büyüklüğü
/// ELDE TUTULAN STOKLA orantılıdır; boş depoyla dalga başlatmak neredeyse işe yaramaz.
/// Bedeli şüphedir.
///
/// Süre, çarpan, şüphe ve öne çıkan konu CropDepotSystem üzerinde ayarlanır.
///
/// UYARI: PostDatabase.asset şu an BOŞ (allPosts: []). Akım başlar ve fiyat gerçekten yükselir,
/// ama feed'de gösterilecek gönderi olmadığı için "feede düşer" kısmı görsel olarak boş kalır.
/// Bkz. Assets/Scripts/Map/crop-depot-readme.md
/// </summary>
[System.Serializable]
public class TriggerDemandWaveEffect : SkillEffect
{
    public override void Apply()
    {
        if (CropDepotSystem.Instance == null)
        {
            Debug.LogWarning("[TriggerDemandWaveEffect] CropDepotSystem sahnede yok.");
            return;
        }

        CropDepotSystem.Instance.TriggerDemandWave();
    }
}
