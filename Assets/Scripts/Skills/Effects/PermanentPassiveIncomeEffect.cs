using UnityEngine;

/// <summary>
/// Sönmeyen pasif gelir: skill açıldığı andan itibaren saniyede sabit kazanç ekler ve
/// bu kazanç ZAMANLA AZALMAZ. Sahip olunan bir işletmenin (çimento şirketi vb.) geliri
/// böyle davranır — tek seferlik bir vurgun değildir.
///
/// DirectPassiveIncomeEffect ile aynı boruyu kullanır, tek farkı sönüm eğrisidir:
/// tüm keypoint'ler %100 verildiğinde SkillTreeManager.GetDecayMultiplier sonsuza kadar
/// 1.0 döner (5. dakikadan sonra eğim k5-k4 = 0 olduğu için düz devam eder). Ayrı bir
/// efekt sınıfı olmasının sebebi okunabilirlik: "hepsi 100 olan sönüm eğrisi" niyeti
/// asset'te gizli kalırdı.
/// </summary>
[System.Serializable]
public class PermanentPassiveIncomeEffect : SkillEffect
{
    [Tooltip("Saniyede eklenen kalıcı gelir.")]
    public float incomePerSecond = 30f;

    private const float NO_DECAY = 100f;

    public override void Apply()
    {
        if (SkillTreeManager.Instance == null)
        {
            Debug.LogWarning("[PermanentPassiveIncomeEffect] SkillTreeManager sahnede yok — gelir eklenemedi.");
            return;
        }

        SkillTreeManager.Instance.AddDirectPassiveIncome(
            incomePerSecond, NO_DECAY, NO_DECAY, NO_DECAY, NO_DECAY, NO_DECAY);
    }
}
