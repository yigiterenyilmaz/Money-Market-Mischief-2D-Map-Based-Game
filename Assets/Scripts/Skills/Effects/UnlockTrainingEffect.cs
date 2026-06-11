[System.Serializable]
public class UnlockTrainingEffect : SkillEffect
{
    public float coefficient;         //temel puan katsayısı
    public float baseSweetSpot;       //başlangıç ideal yatırım miktarı
    public float sweetSpotGrowthRate; //idealin saniyede büyümesi

    public override void Apply()
    {
        if (SkillTreeManager.Instance != null)
        {
            SkillTreeManager.Instance.UnlockTraining(coefficient, baseSweetSpot, sweetSpotGrowthRate);
        }
    }
}
