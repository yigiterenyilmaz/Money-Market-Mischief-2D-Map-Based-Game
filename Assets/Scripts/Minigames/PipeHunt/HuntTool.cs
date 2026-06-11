using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/PipeHunt/Tool")]
public class HuntTool : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    public int cost; //her oyunda ödenen alet ücreti
    public int durability; //aletin toplam dayanıklılığı
    public int damagePerHit; //vuruş başına boruya verilen hasar

    [Range(0f, 1f)]
    public float stealth; //gizlilik (0 = dikkat çeker/kısa süre, 1 = gizli/uzun süre)
}
