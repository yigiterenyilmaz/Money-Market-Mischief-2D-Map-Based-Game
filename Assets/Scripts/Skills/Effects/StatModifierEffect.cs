using UnityEngine;

[System.Serializable]
public class StatModifierEffect : SkillEffect
{
    public StatType statType; //statları değiştiren etki.
    public float changeValue;

    public override void Apply()
    {
        GameStatManager.Instance.ModifyStat(statType, changeValue);
    }
}
