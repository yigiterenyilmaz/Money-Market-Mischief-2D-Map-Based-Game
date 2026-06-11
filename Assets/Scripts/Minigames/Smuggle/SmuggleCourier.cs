using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/Smuggle/Courier")]
public class SmuggleCourier : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description; //kuryenin açıklaması

    [Range(0f, 100f)] public float reliability; //kuryenin becerisi — başarı şansını etkiler, düşükse beceriksizlik eventleri tetiklenir
    [Range(0f, 100f)] public float speed; //operasyon süresini kısaltır
    public int cost; //kurye kiralama maliyeti
    [Range(0f, 1f)] public float betrayalChance; //ihanet olasılığı
}
