using UnityEngine;

/// <summary>
/// a15 "sidik yarışı" — fiyat savaşı yetkisini verir.
///
/// Yalnızca yetkiyi verir; savaşı başlatan efekt ayrıdır (StartPriceWarEffect),
/// çünkü bu efekt skill alınırken bir kez, o efekt her kullanımda çalışır.
/// </summary>
[System.Serializable]
public class UnlockPriceWarEffect : SkillEffect
{
    public override void Apply()
    {
        if (PriceWarSystem.Instance == null)
        {
            Debug.LogWarning("[UnlockPriceWarEffect] PriceWarSystem sahnede yok — fiyat savaşı açılamadı.");
            return;
        }

        PriceWarSystem.Instance.Unlock();
    }
}
