using System.Collections.Generic;

[System.Serializable]
public class UnlockJewelerEffect : SkillEffect
{
    public List<JewelerProduct> availableJewelers; //bu skill ile açılan kuyumcular

    public override void Apply()
    {
        SkillTreeManager.Instance.UnlockJeweler(availableJewelers);
    }
}
