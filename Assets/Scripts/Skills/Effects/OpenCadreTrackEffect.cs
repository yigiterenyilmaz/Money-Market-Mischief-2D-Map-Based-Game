using UnityEngine;

/// <summary>
/// Yetiştirilen kadronun yerleştirilebileceği bir alan açar:
/// c14 holding (haraç → pasif gelir), c15 akademi (siyasi nüfuz), c16 medya (medya erişimi).
/// c16, siyaset ağacının medya ağacına bağlandığı tek noktadır.
/// </summary>
[System.Serializable]
public class OpenCadreTrackEffect : SkillEffect
{
    [Tooltip("Kadronun yerleştirileceği alan.")]
    public CadreTrack track;

    public override void Apply()
    {
        if (PoliticsSystem.Instance != null)
        {
            PoliticsSystem.Instance.OpenCadreTrack(track);
            return;
        }
        Debug.LogWarning("[OpenCadreTrackEffect] PoliticsSystem sahnede yok — kadro alanı açılamadı: " + track);
    }
}
