using UnityEngine;

/// <summary>
/// b1 — medya ağacının kökü. MediaSystem'i çalışır hale getirir; tek başına bir kazanç vermez,
/// altındaki bütün medya düğümlerinin çalışabilmesi için gereken anahtardır.
/// </summary>
[System.Serializable]
public class UnlockMediaSystemEffect : SkillEffect
{
    public override void Apply()
    {
        if (MediaSystem.Instance != null)
        {
            MediaSystem.Instance.Unlock();
            return;
        }
        Debug.LogWarning("[UnlockMediaSystemEffect] MediaSystem sahnede yok — medya sistemi açılamadı.");
    }
}
