using UnityEngine;

/// <summary>
/// Bir medya hamlesini AÇAR ya da ÇALIŞTIRIR — hangisi olduğunu <see cref="unlockOnly"/> belirler.
///
/// Aynı sınıfın iki yerde kullanılmasının sebebi: skill açıldığında hamlenin kullanılabilir hale
/// gelmesi (skill.effects) ile oyuncunun hamleyi her tetiklemesi (skill.activeAbility.onActivate)
/// ayrı olaylardır, ama ikisi de aynı MediaAction'a bakar. Tek sınıf, asset'te tek satır fark.
/// </summary>
[System.Serializable]
public class MediaActionEffect : SkillEffect
{
    [Tooltip("Hangi medya hamlesi.")]
    public MediaAction action;

    [Tooltip("AÇIK: skill alınınca hamleyi yalnızca kullanılabilir yapar (skill.effects içine koy). " +
             "KAPALI: hamleyi o anda çalıştırır (activeAbility.onActivate içine koy).")]
    public bool unlockOnly;

    public override void Apply()
    {
        if (MediaSystem.Instance == null)
        {
            Debug.LogWarning("[MediaActionEffect] MediaSystem sahnede yok — hamle işlenemedi: " + action);
            return;
        }

        if (unlockOnly) MediaSystem.Instance.EnableAction(action);
        else            MediaSystem.Instance.PerformAction(action);
    }
}
