using UnityEngine;



[CreateAssetMenu(menuName = "Minigames/MiniGame Data")]
public class MiniGameData : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public string description;
    public GameObject prefab; //minigame prefab'ı
    public float cooldownDuration = 300f; //minigame bittikten sonra bekleme süresi (saniye) — varsayılan 5 dk
}
