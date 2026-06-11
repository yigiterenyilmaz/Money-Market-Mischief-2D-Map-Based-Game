using UnityEngine;

[System.Serializable]
public class UnlockFeedFreezeEffect : SkillEffect
{
    public override void Apply()
    {
        if (SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.UnlockFreezeAbility();
        }
    }
}
