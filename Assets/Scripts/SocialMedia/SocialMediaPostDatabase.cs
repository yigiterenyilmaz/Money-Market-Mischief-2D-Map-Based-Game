using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SocialMedia/PostDatabase")]
public class SocialMediaPostDatabase : ScriptableObject
{
    public List<SocialMediaPost> allPosts;

    public List<SocialMediaPost> GetPostsByTopic(TopicType topic)
    {
        List<SocialMediaPost> result = new List<SocialMediaPost>();
        foreach (SocialMediaPost post in allPosts)
        {
            if (post.topic == topic)
            {
                result.Add(post);
            }
        }
        return result;
    }

    public SocialMediaPost GetById(string id)
    {
        foreach (SocialMediaPost post in allPosts)
        {
            if (post.id == id)
            {
                return post;
            }
        }
        return null;
    }
}
