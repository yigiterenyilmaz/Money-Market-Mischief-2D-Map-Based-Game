using UnityEngine;

[System.Serializable]
public class FeedSpeedBoostEffect : SkillEffect
{
    public float minPostInterval = 1f; //postlar arası minimum süre (min 1 saniye)
    public float maxPostInterval = 2f; //postlar arası maximum süre

    public override void Apply()
    {
        if (SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.SetSpeedBoost(minPostInterval, maxPostInterval);
        }
    }
}
