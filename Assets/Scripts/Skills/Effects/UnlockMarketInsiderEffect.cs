using UnityEngine;

/// <summary>
/// a24 "şirketten insider" (seviye 1) ve a25 "devletten insider" (seviye 2) — piyasada bir
/// formasyon başlarken oyuncuya önceden haber verir.
///   seviye 1: sadece yön (yukarı / aşağı)
///   seviye 2: formasyonun adı, büyüklüğü ve boşa çıkıp çıkmayacağı
///
/// Seviye düşürülmez: a25 alındıktan sonra a24 tekrar uygulansa bile devlet kaynağı korunur.
/// </summary>
[System.Serializable]
public class UnlockMarketInsiderEffect : SkillEffect
{
    [Tooltip("1 = şirket içi kaynak (sadece yön), 2 = devlet kaynağı (ad + büyüklük + sonuç)")]
    [Range(1, 2)] public int level = 1;

    public override void Apply()
    {
        if (StockMarketSystem.Instance == null)
        {
            Debug.LogWarning("[UnlockMarketInsiderEffect] StockMarketSystem sahnede yok — insider açılamadı.");
            return;
        }

        StockMarketSystem.Instance.UnlockInsider(level);
    }
}
