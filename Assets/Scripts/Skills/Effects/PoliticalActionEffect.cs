using UnityEngine;

/// <summary>
/// Bir siyaset hamlesini AÇAR ya da ÇALIŞTIRIR (c2, c5, c7, c8).
/// <see cref="unlockOnly"/> mantığı MediaActionEffect ile aynıdır.
/// </summary>
[System.Serializable]
public class PoliticalActionEffect : SkillEffect
{
    [Tooltip("Hangi siyaset hamlesi.")]
    public PoliticalAction action;

    [Tooltip("AÇIK: skill alınınca hamleyi kullanılabilir yapar (skill.effects). " +
             "KAPALI: hamleyi o anda çalıştırır (activeAbility.onActivate).")]
    public bool unlockOnly;

    public override void Apply()
    {
        if (PoliticsSystem.Instance == null)
        {
            Debug.LogWarning("[PoliticalActionEffect] PoliticsSystem sahnede yok — hamle işlenemedi: " + action);
            return;
        }

        if (unlockOnly) PoliticsSystem.Instance.EnableAction(action);
        else            PoliticsSystem.Instance.PerformAction(action);
    }
}
