using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/WarForOil/EventGroups/WTETWCEventGroup")]
public class WTETWCEventGroup : ScriptableObject
{
    public string id;
    [TextArea(1, 3)] public string description;
    public List<EventGroupMember> members;
    public int maxTriggerCount = -1; //en fazla kaç event tetiklenebilir (-1 = sınırsız)
}

[System.Serializable]
public class EventGroupMember
{
    public WarForOilEvent warEvent; //grup üyesi event
    public TriggerWeightLevel weightLevel = TriggerWeightLevel.Normal; //tetiklenme ağırlığı
}

public enum TriggerWeightLevel
{
    ExtremelyLess, // 0.25x
    Less,          // 0.5x
    Normal,        // 1.0x (varsayılan)
    More,          // 1.25x
    Extreme        // 1.5x
}
