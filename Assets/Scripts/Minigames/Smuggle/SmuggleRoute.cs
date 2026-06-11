using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/Smuggle/Route")]
public class SmuggleRoute : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description; //rotanın açıklaması

    [Range(0f, 100f)] public float riskLevel; //genel risk seviyesi, event tetiklenme olasılığını etkiler
    public float distance; //operasyon süresini etkiler
    public int cost; //rotayı kullanma maliyeti
}
