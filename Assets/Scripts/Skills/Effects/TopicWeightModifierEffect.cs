using UnityEngine;

[System.Serializable]
public class TopicWeightModifierEffect : SkillEffect
{
    public TopicType topic; //hangi topic etkilenecek
    public float weightChange; //ne kadar ağırlık eklenecek (negatif de olabilir)

    public override void Apply()
    {
        if (SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.ModifyTopicWeight(topic, weightChange);
        }
    }
}
