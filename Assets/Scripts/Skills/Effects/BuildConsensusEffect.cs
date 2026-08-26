using UnityEngine;

/// <summary>
/// c21 — konsensüs inşası. Tek seferlik ve kalıcı: siyasi nüfuz kazanımını büyütür, şüphe
/// kazanımını kısar. Bir hamle değil, ağacın geri kalanının verimini değiştiren bir zemin.
/// </summary>
[System.Serializable]
public class BuildConsensusEffect : SkillEffect
{
    public override void Apply()
    {
        if (PoliticsSystem.Instance != null)
        {
            PoliticsSystem.Instance.BuildConsensus();
            return;
        }
        Debug.LogWarning("[BuildConsensusEffect] PoliticsSystem sahnede yok — konsensüs kurulamadı.");
    }
}
