using UnityEngine;

/// <summary>
/// Sürekli güven programı: skill açıldığı andan itibaren her tick'te belirli bir para
/// harcanır ve karşılığında güven kazanılır. "İnsanlara kira ödeme / ev alma" gibi
/// bitmeyen taahhütler böyle davranır — tek seferlik bir jest değildir.
///
/// Para yetmediği tick'te program o turu ATLAR: ne para gider ne güven gelir.
/// Böylece cüzdan eksiye düşmez ve oyuncu iflas etmez, sadece programı besleyemez.
///
/// UYARI: güvenin riski azaltma davranışı henüz bağlı değil — bu efekt görünmez bir
/// sayıyı besliyor. Bkz. Assets/Scripts/Stats/trust-system-readme.md
/// </summary>
[System.Serializable]
public class TrustProgramEffect : SkillEffect
{
    [Tooltip("Her tick'te harcanan para.")]
    public float wealthPerTick = 150f;

    [Tooltip("Her tick'te kazanılan güven.")]
    public float trustPerTick = 1f;

    public override void Apply()
    {
        if (SkillTreeManager.Instance == null)
        {
            Debug.LogWarning("[TrustProgramEffect] SkillTreeManager sahnede yok — program başlatılamadı.");
            return;
        }

        SkillTreeManager.Instance.AddTrustProgram(wealthPerTick, trustPerTick);
    }
}
