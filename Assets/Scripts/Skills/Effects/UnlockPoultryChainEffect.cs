using UnityEngine;

/// <summary>
/// Tavuk zincirini açar (a27 — "los pollos hermanos"): meşru bir restoran zinciri, ekin
/// deposundaki stoğu her tick GARANTİLİ ama piyasa altı bir fiyattan satın alır ve kirli
/// parayı akladığı için şüpheyi düşürür.
///
/// Tarım dalının GÜVENLİ koludur: kazanç düşük ama kesin, üstelik şüphe eriyor. Karşı kol
/// a28 (stokçuluk + talep dalgası) ise stoğu elde tutup zirvede satmayı ödüllendirir.
/// İkisi aynı stoğun üstünde yarışır — zincir sürekli boşalttığı için dalga anında elde
/// tutulan stok azalır.
///
/// Oranlar ve fiyat CropDepotSystem üzerinde ayarlanır (chainPurchaseRatio, chainPriceRatio,
/// chainSuspicionPerTick, chainTickSeconds).
/// </summary>
[System.Serializable]
public class UnlockPoultryChainEffect : SkillEffect
{
    public override void Apply()
    {
        if (CropDepotSystem.Instance != null)
        {
            CropDepotSystem.Instance.UnlockPoultryChain();
            return;
        }

        Debug.LogWarning("[UnlockPoultryChainEffect] CropDepotSystem sahnede yok — tavuk zinciri açılamadı.");
    }
}
