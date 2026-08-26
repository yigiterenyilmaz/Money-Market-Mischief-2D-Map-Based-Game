using UnityEngine;

/// <summary>
/// Trade sistemini açar: oyuncu mum grafiğindeki canlı fiyattan gerçek parayla
/// alım satım yapabilir hale gelir (TradingSystem).
///
/// Yalnızca yetkiyi verir; paneli açan efekt ayrıdır (OpenTradingPanelEffect),
/// çünkü bu efekt skill alınırken bir kez, o efekt her tıklamada çalışır.
/// </summary>
[System.Serializable]
public class UnlockTradingEffect : SkillEffect
{
    public override void Apply()
    {
        if (TradingSystem.Instance != null)
        {
            TradingSystem.Instance.Unlock();
            return;
        }

        Debug.LogWarning("[UnlockTradingEffect] TradingSystem sahnede yok — trade sistemi açılamadı.");
    }
}
