using UnityEngine;

/// <summary>
/// Anket düğümleri (b11–b14). Gizli statları oyuncuya TAHMİNİ olarak açar.
///
/// Anketlerin bir anlamı olmasının sebebi, statların oyuncuya gösterilmiyor olması: ekranda
/// yalnızca para var (MoneyUI). İtibar, şüphe ve siyasi nüfuz oyuncudan gizlidir; bu ağaç
/// onları ölçmenin tek yoludur.
///
/// <see cref="unlockOnly"/> mantığı MediaActionEffect ile aynıdır.
/// </summary>
[System.Serializable]
public class PollEffect : SkillEffect
{
    [Tooltip("Hangi anket.")]
    public PollKind kind;

    [Tooltip("AÇIK: anketi yalnızca kullanılabilir yapar (skill.effects). " +
             "KAPALI: anketi o anda yapar (activeAbility.onActivate).")]
    public bool unlockOnly;

    public override void Apply()
    {
        if (MediaSystem.Instance == null)
        {
            Debug.LogWarning("[PollEffect] MediaSystem sahnede yok — anket işlenemedi: " + kind);
            return;
        }

        if (unlockOnly) MediaSystem.Instance.EnablePoll(kind);
        else            MediaSystem.Instance.RunPoll(kind);
    }
}
