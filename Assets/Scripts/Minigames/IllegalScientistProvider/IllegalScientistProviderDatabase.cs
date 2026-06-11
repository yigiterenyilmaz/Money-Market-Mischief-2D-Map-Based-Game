using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/IllegalScientistProvider/Database")]
public class IllegalScientistProviderDatabase : ScriptableObject
{
    public List<IllegalScientistProviderEvent> offerEvents;        //teklif eventleri (eventType = Offer)
    public List<IllegalScientistProviderEvent> processEvents;      //süreç eventleri (eventType = Process)
    public List<IllegalScientistProviderEvent> postProcessEvents;  //operasyon sonrası musallat eventleri (eventType = PostProcess)
}
