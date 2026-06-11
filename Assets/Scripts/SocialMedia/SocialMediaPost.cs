using UnityEngine;

[CreateAssetMenu(menuName = "SocialMedia/Post")]
public class SocialMediaPost : ScriptableObject
{
    public string id;
    public string authorName; //post atan kullanıcı adı
    [TextArea(2, 5)]
    public string content; //post içeriği
    public TopicType topic; //hangi topic'e ait (UI'da gösterilmez, trend için kullanılır)
    public Sprite authorAvatar; //profil fotoğrafı (opsiyonel)
    public bool isRepeatable = true; //tekrar gelebilir mi
}
