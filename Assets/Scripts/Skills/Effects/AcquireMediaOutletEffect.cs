using UnityEngine;

/// <summary>
/// Bir medya mecrası edinir (b2 gazete, b3 sosyal medya, b6 haber kanalı, b7 ünlüler).
/// Mecralar kendi başlarına hamle yapmaz — MediaSystem.Reach'i büyütürler, yani ağaçtaki
/// bütün aktif hamlelerin etkisini birlikte yükseltirler.
/// </summary>
[System.Serializable]
public class AcquireMediaOutletEffect : SkillEffect
{
    [Tooltip("Edinilecek mecra.")]
    public MediaOutlet outlet;

    public override void Apply()
    {
        if (MediaSystem.Instance != null)
        {
            MediaSystem.Instance.AcquireOutlet(outlet);
            return;
        }
        Debug.LogWarning("[AcquireMediaOutletEffect] MediaSystem sahnede yok — mecra edinilemedi: " + outlet);
    }
}
