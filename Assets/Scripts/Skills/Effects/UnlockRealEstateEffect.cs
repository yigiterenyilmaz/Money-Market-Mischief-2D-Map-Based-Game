using UnityEngine;

/// <summary>
/// Emlak sistemini açar: oyuncu haritadaki şehir binalarını tıklayıp satın alabilir hale gelir.
/// Satın alınan bina periyodik kira üretir (RealEstateSystem).
/// </summary>
[System.Serializable]
public class UnlockRealEstateEffect : SkillEffect
{
    public override void Apply()
    {
        if (RealEstateSystem.Instance != null)
        {
            RealEstateSystem.Instance.Unlock();
            return;
        }

        Debug.LogWarning("[UnlockRealEstateEffect] RealEstateSystem sahnede yok — emlak sistemi açılamadı.");
    }
}
