using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/PleasePaper/Event")]
public class PleasePaperEvent : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description; //event açıklaması (oyuncuya gösterilecek)
    public PleasePaperEventType eventType; //bu event hangi gruba ait

    //--- Offer alanları (sadece eventType == Offer iken anlamlı) ---
    public int baseReward; //başarılı olursa taban kazanç
    public bool isFakeCrisis; //sahte kriz mi (tuzak)
    [Range(0f, 1f)] public float realChanceOnRepeat = 0.3f; //tekrar geldiğinde gerçek olma şansı (sadece fake offer'lar için)
    public List<PleasePaperEvent> fakeCrisisEvents; //sahte kriz event zinciri (isFakeCrisis true ise)
    public List<PleasePaperEvent> processEvents; //bu offer'a ait süreç event havuzu (gerçek kriz olarak çalıştığında kullanılır)

    //--- Karar süresi (Offer dışında, Inspector'dan ayarlanabilir) ---
    public float decisionTime = 10f; //oyuncunun karar vermesi için verilen süre (saniye)

    //--- Seçenekler (Offer dışında tüm tipler için) ---
    public List<PleasePaperEventChoice> choices; //oyuncunun seçebileceği seçenekler
    public int defaultChoiceIndex = -1; //süre dolunca otomatik seçilecek seçenek (-1 = hiçbiri, ceza/rastgele)
}

public enum PleasePaperEventType
{
    Offer,       //teklif eventi — ülke bilgisi, baseReward, isFakeCrisis taşır
    FakeCrisis,  //sahte kriz zinciri eventi — choices ile zarar verir
    Process      //gerçek kriz süreç eventi — choices ile controlStat etkiler
}

[System.Serializable]
public class PleasePaperEventChoice
{
    public string displayName;
    [TextArea(2, 4)] public string description;
    public float controlStatModifier; //controlStat değişimi (+ veya -)
    public float suspicionModifier; //şüphe değişimi
    public int costModifier; //ek maliyet/zarar
}
