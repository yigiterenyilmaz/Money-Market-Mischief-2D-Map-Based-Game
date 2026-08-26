using UnityEngine;

/// <summary>
/// Tahmin piyasası ekranını açar. a17'nin AKTİF yeteneğine bağlanır: oyuncu ağaçta
/// node'a tıklayınca açık sorular listelenir.
///
/// Pasif efekt (UnlockPredictionMarketEffect) yalnızca piyasayı çalıştırır; bu efekt
/// her tıklamada ekranı açar — bu yüzden ayrı bir sınıf.
/// </summary>
[System.Serializable]
public class OpenPredictionMarketEffect : SkillEffect
{
    public override void Apply()
    {
        PredictionMarketUI ui = Object.FindFirstObjectByType<PredictionMarketUI>();

        if (ui == null)
        {
            Debug.LogWarning("[OpenPredictionMarketEffect] PredictionMarketUI sahnede yok — ekran açılamadı.");
            return;
        }

        ui.Open();
    }
}
