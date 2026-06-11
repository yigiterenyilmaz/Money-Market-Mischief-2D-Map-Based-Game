[System.Serializable]
public class DirectPassiveIncomeEffect : SkillEffect
{
    public float incomePerSecond; //bu skill açılınca saniye başına eklenen sabit gelir

    //decay eğrisi: her dakikada başlangıç gelirinin %kaçı kalmış
    public float decayAt1Min = 95f;
    public float decayAt2Min = 85f;
    public float decayAt3Min = 60f;
    public float decayAt4Min = 25f;
    public float decayAt5Min = 5f;

    public override void Apply()
    {
        if (SkillTreeManager.Instance != null)
        {
            SkillTreeManager.Instance.AddDirectPassiveIncome(
                incomePerSecond, decayAt1Min, decayAt2Min, decayAt3Min, decayAt4Min, decayAt5Min
            );
        }
    }
}
