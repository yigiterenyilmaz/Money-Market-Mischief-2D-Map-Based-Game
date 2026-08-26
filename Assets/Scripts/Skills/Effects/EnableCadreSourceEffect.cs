using UnityEngine;

/// <summary>
/// Kadro kaynağı açar (c3 burs programları, c13 gençlik kampları).
/// Kaynak tek başına işe yaramaz: kadronun yerleşeceği en az bir alan (c14/c15/c16) da açık
/// olmalı, yoksa PoliticsSystem kadro üretmeye başlamaz.
/// </summary>
[System.Serializable]
public class EnableCadreSourceEffect : SkillEffect
{
    [Tooltip("Hangi kadro kaynağı.")]
    public CadreSource source;

    public override void Apply()
    {
        if (PoliticsSystem.Instance != null)
        {
            PoliticsSystem.Instance.EnableCadreSource(source);
            return;
        }
        Debug.LogWarning("[EnableCadreSourceEffect] PoliticsSystem sahnede yok — kadro kaynağı açılamadı: " + source);
    }
}
