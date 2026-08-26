using UnityEngine;

/// <summary>
/// a17 "polymarket" — tahmin piyasasını açar. Sorular skill alınır alınmaz işlemeye
/// başlar; panel kapalıyken de süreler akar ve sonuçlanır.
///
/// Yalnızca yetkiyi verir; paneli açan efekt ayrıdır (OpenPredictionMarketEffect).
/// </summary>
[System.Serializable]
public class UnlockPredictionMarketEffect : SkillEffect
{
    public override void Apply()
    {
        if (PredictionMarketSystem.Instance == null)
        {
            Debug.LogWarning("[UnlockPredictionMarketEffect] PredictionMarketSystem sahnede yok — " +
                             "tahmin piyasası açılamadı.");
            return;
        }

        PredictionMarketSystem.Instance.Unlock();
    }
}
