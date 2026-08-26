using UnityEngine;

/// <summary>
/// Yeni bir fiyat savaşı başlatır ve trade panelini açar. a15'in AKTİF yeteneğine bağlanır.
///
/// Panel açılır çünkü savaşın tamamı orada yönetiliyor: baskı butonları ve rakibin
/// durumu grafiğin yanında duruyor.
/// </summary>
[System.Serializable]
public class StartPriceWarEffect : SkillEffect
{
    public override void Apply()
    {
        if (PriceWarSystem.Instance == null)
        {
            Debug.LogWarning("[StartPriceWarEffect] PriceWarSystem sahnede yok — savaş başlatılamadı.");
            return;
        }

        if (!PriceWarSystem.Instance.StartWar())
        {
            Debug.LogWarning("[StartPriceWarEffect] Savaş başlatılamadı — zaten süren bir savaş var olabilir.");
            return;
        }

        TradingSystem.Instance?.OpenPanel();
    }
}
