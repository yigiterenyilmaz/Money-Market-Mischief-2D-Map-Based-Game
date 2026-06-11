using UnityEngine;

public enum JewelerType
{
    Legal,
    Illegal
}

[CreateAssetMenu(menuName = "SkillTree/Products/JewelerProduct")]
public class JewelerProduct : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public int cost; //satın alma fiyatı
    public JewelerType jewelerType;

    public float incomePer10Seconds; //10 saniyede gelir
    public float suspicionPerMinute; //dakikada taban şüphe üretimi (illegal için)
    public float reputationPerMinute; //dakikada itibar değişimi (legal için)
}
