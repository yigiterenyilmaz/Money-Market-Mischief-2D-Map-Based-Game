using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// a22 "yalan bilanço" ve a23 "formasyon manipülasyonu" — piyasaya istenen formasyonu
/// zorla bindirir. Aday listesinden rastgele biri seçilir; liste tek elemanlıysa
/// o formasyon garantidir.
///
/// Geçerli Id'ler chart-pattern-readme.md §10'da listelidir (örn. D1_Pump, A9_BullFlag).
/// Yanlış yazılan Id sessizce başarısız olur, Console'a uyarı düşer.
/// </summary>
[System.Serializable]
public class ForceChartPatternEffect : SkillEffect
{
    [Tooltip("Aday formasyon Id'leri. Örn: D1_Pump, A9_BullFlag, B1_WyckoffAccumulation")]
    public List<string> patternIds = new List<string>();

    [Tooltip("Manipülasyonun şüphe bedeli (tek seferlik).")]
    public float suspicionCost = 5f;

    public override void Apply()
    {
        if (StockMarketSystem.Instance == null)
        {
            Debug.LogWarning("[ForceChartPatternEffect] StockMarketSystem sahnede yok — manipülasyon uygulanamadı.");
            return;
        }

        StockMarketSystem.Instance.ForcePattern(patternIds, suspicionCost);
    }
}
