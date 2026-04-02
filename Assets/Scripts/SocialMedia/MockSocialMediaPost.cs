using UnityEngine;

public enum MessageCategory
{
    A, // surekli donen mesajlar
    B  // kosul saglaninca aktif olan mesajlar
}

[System.Serializable]
public class MockSocialMediaPost
{
    public string authorName;
    [TextArea(2, 5)]
    public string content;
    public MessageCategory category;
}
