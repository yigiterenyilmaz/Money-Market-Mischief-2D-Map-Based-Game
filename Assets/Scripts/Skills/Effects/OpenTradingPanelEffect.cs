using UnityEngine;

/// <summary>
/// Trade panelini açar. Skill'in AKTİF yeteneğine bağlanır: oyuncu ağaçta node'a
/// tıklayınca grafik açılır ve alım satım yapılabilir.
///
/// Pasif efekt (UnlockTradingEffect) yalnızca yetkiyi verir; bu efekt her tıklamada
/// paneli açar — bu yüzden ayrı bir sınıf.
/// </summary>
[System.Serializable]
public class OpenTradingPanelEffect : SkillEffect
{
    public override void Apply()
    {
        if (TradingSystem.Instance == null)
        {
            Debug.LogWarning("[OpenTradingPanelEffect] TradingSystem sahnede yok — panel açılamadı.");
            return;
        }

        TradingSystem.Instance.OpenPanel();
    }
}
