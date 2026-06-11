using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/WarForOil/EventGroups/OFPCEventGroup")]
public class OFPCEventGroup : ScriptableObject
{
    public string id;
    [TextArea(1, 3)] public string description;
    public List<WarForOilEvent> members; //grup üyesi eventler
}
