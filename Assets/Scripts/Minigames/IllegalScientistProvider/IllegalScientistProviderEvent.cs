using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/IllegalScientistProvider/Event")]
public class IllegalScientistProviderEvent : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description; //event açıklaması (oyuncuya gösterilecek)
    public IllegalScientistProviderEventType eventType; //bu event hangi gruba ait

    //--- Offer alanları (sadece eventType == Offer iken anlamlı) ---
    public int baseReward; //başarılı olursa taban kazanç
    [Range(0f, 1f)] public float riskLevel; //ülkenin risk seviyesi (0 = güvenli, 1 = çok riskli)

    //--- Süreç event havuzu (sadece Offer için, boşsa database'den alınır) ---
    public List<IllegalScientistProviderEvent> processEvents; //bu offer'a özel süreç eventleri

    //--- Musallat etki tipi (sadece eventType == PostProcess iken anlamlı) ---
    public PostProcessEffectType postProcessEffect; //bu musallat eventinin etki tipi
    public int scientistKillCount;  //ScientistKill: öldürülecek bilim adamı sayısı (rastgele seçilir)

    //--- Karar süresi ---
    public float decisionTime = 10f; //oyuncunun karar vermesi için verilen süre (saniye)

    //--- Seçenekler (Process eventleri için) ---
    public List<IllegalScientistProviderEventChoice> choices; //oyuncunun seçebileceği seçenekler
    public int defaultChoiceIndex = -1; //süre dolunca otomatik seçilecek seçenek (-1 = ilk seçenek)
}

public enum IllegalScientistProviderEventType
{
    Offer,       //teklif eventi — ülke bilgisi, baseReward, riskLevel taşır
    Process,     //süreç eventi — choices ile risk seviyesini etkiler
    PostProcess  //operasyon sonrası musallat eventi — choices ile suspicion/cost etkiler
}

/// <summary>
/// Musallat eventlerinin etki tipleri. Yeni musallat etkileri buraya eklenir.
/// </summary>
public enum PostProcessEffectType
{
    None,           //sadece choices etkisi (suspicion/cost)
    ScientistKill   //bilim adamı öldürme
}

[System.Serializable]
public class IllegalScientistProviderEventChoice
{
    public string displayName;
    [TextArea(2, 4)] public string description;
    public float riskModifier;      //risk seviyesi değişimi (- = riski azaltır, + = artırır)
    public float suspicionModifier; //şüphe değişimi
    public int costModifier;        //ek maliyet/zarar
}
