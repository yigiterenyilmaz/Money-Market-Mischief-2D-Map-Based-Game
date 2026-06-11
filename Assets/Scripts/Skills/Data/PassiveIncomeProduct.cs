using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Products/PassiveIncomeProduct")]
public class PassiveIncomeProduct : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    public int cost; //satın alma fiyatı
    public bool isSellable = true; //geri satılabilir mi
    [Range(0f, 0.99f)] public float sellRatio = 0.6f; //satış fiyatı = cost * sellRatio

    public float minIncomePerSecond; //birim başına saniyede minimum gelir
    public float maxIncomePerSecond; //birim başına saniyede maksimum gelir
}
