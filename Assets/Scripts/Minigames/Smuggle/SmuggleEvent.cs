using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/Smuggle/Event")]
public class SmuggleEvent : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description; //event açıklaması (oyuncuya gösterilecek)
    public SmuggleEventTrigger triggerType; //bu event ne tarafından tetiklenir
    public List<SmuggleEventChoice> choices; //oyuncunun seçebileceği seçenekler
}

/// <summary>
/// Event tetiklenme kaynağı
/// </summary>
public enum SmuggleEventTrigger
{
    Risk,           //rota riskine bağlı (route.riskLevel)
    Betrayal,       //kurye ihanet olasılığına bağlı (courier.betrayalChance)
    Incompetence    //kuryenin beceriksizliğine bağlı (100 - courier.reliability)
}

[System.Serializable]
public class SmuggleEventChoice
{
    public string displayName;
    [TextArea(2, 4)] public string description;
    public float suspicionModifier; //şüphe değişimi
    public int costModifier; //ekstra maliyet (rüşvet, kayıp vs.)
    public bool causesFailure; //bu seçim operasyonu anında başarısız yapar (yakalanma, ihanet vs.)
    public float failureDelay; //0'dan büyükse operasyon X saniye sonra başarısız olur (gecikmeli yakalanma vs.)
    public List<SmuggleEvent> nextEventPool; //bu seçim yapılırsa sonraki eventler bu havuzdan gelir (boşsa zincir biter)
}
