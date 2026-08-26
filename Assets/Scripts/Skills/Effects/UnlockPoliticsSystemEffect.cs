using UnityEngine;

/// <summary>
/// c1 — siyaset ağacının kökü. PoliticsSystem'i açar ve başlangıç siyasi nüfuzu verir.
///
/// Başlangıç nüfuzunun bir anlamı var: nüfuz, GameStatManager.GetSkillEfficiencyMultiplier()
/// üzerinden 0.5x–1.5x arası bir verim çarpanına dönüşür. Yani kök düğüm "çarpan gibi çalışır"
/// tarifini birebir karşılar — ağacın geri kalanı bu çarpanın üstüne biner.
/// </summary>
[System.Serializable]
public class UnlockPoliticsSystemEffect : SkillEffect
{
    [Tooltip("Kök alınınca verilen siyasi nüfuz.")]
    public float startingInfluence = 10f;

    public override void Apply()
    {
        if (PoliticsSystem.Instance != null)
            PoliticsSystem.Instance.Unlock();
        else
            Debug.LogWarning("[UnlockPoliticsSystemEffect] PoliticsSystem sahnede yok — siyaset sistemi açılamadı.");

        if (startingInfluence != 0f && GameStatManager.Instance != null)
            GameStatManager.Instance.AddPoliticalInfluence(startingInfluence);
    }
}
