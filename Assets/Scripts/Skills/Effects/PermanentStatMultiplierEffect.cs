using UnityEngine;

[System.Serializable]
public class PermanentStatMultiplierEffect : SkillEffect
{
    public StatType statType;
    public float multiplier = 1.1f; //1.1 = %10 kazanım artışı

    public override void Apply()
    {
        GameStatManager.Instance.ApplyPermanentGainMultiplier(statType, multiplier);
    }
}
