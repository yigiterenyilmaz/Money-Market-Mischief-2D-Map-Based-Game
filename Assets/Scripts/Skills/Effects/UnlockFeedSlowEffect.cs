using UnityEngine;

[System.Serializable]
public class UnlockFeedSlowEffect : SkillEffect
{
    public override void Apply()
    {
        if (SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.UnlockSlowAbility();
        }
    }
}
