using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/PleasePaper/Database")]
public class PleasePaperDatabase : ScriptableObject
{
    public List<PleasePaperEvent> offerEvents; //teklif eventleri (eventType = Offer)
    public List<PleasePaperEvent> processEvents; //gerçek kriz süreç eventleri (eventType = Process)
}
