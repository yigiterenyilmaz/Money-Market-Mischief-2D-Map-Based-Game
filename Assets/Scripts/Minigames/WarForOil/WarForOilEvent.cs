using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/WarForOil/Event")]
public class WarForOilEvent : ScriptableObject
{
    public string id;
    [TextArea(1, 3)] public string displayName;
    [TextArea(2, 8)] public string description;

    [Header("Geliştirici Notu")]
    [TextArea(3, 10)] public string devNote; //sadece Inspector'da görünür, oyuna etkisi yok

    public float minWarTime = 0f; //bu event savaş başladıktan en az kaç saniye sonra gelebilir
    public float decisionTime = 10f; //karar süresi (saniye)
    public bool isRepeatable; //aynı savaşta tekrar tetiklenebilir mi
    public int maxRepeatCount = 1; //en fazla kaç kez tekrar edebilir (isRepeatable true ise)
    public List<WarForOilEventChoice> choices;
    public int defaultChoiceIndex = -1; //süre dolunca otomatik seçilecek seçenek (-1 = ilk seçenek)

    /// <summary>
    /// Şu an seçilebilir olan choice'ların listesini döner.
    /// </summary>
    public List<WarForOilEventChoice> GetAvailableChoices()
    {
        List<WarForOilEventChoice> available = new List<WarForOilEventChoice>();
        if (choices == null) return available;

        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i].IsAvailable())
                available.Add(choices[i]);
        }
        return available;
    }
}

[System.Serializable]
public class WarForOilEventChoice
{
    public string displayName;
    [TextArea(2, 4)] public string description;
    public float supportModifier; //destek stat'ını etkiler (pozitif = ülkeyi destekle)
    public float suspicionModifier; //şüphe etkisi
    public float politicalInfluenceModifier; //politik nüfuz etkisi (negatif = düşürür)
    public int costModifier; //maliyet etkisi

    //diğer sonuçlar (Editor tarafından foldout içinde çizilir)
    public bool endsWar; //bu seçenek savaşı bitirir mi
    public float warEndDelay; //savaş kaç saniye sonra biter (0 = anında)
    public bool reducesReward; //ödülü düşürür mü
    [Range(0f, 1f)] public float baseRewardReduction; //base reward'ı bu oranda düşürür (0.3 = %30 düşüş)
    public bool endsWarWithDeal; //savaşı anlaşmayla bitirir (garanti ödül)
    public float dealDelay; //anlaşma kaç saniye sonra savaşı bitirir (0 = anında)
    [Range(0f, 1f)] public float dealRewardRatio; //normal kazanımın bu oranı garanti verilir (0.8 = %80)
    public bool blocksEvents; //seçilirse savaş sonuna kadar yeni event gelmez

    //ön koşullar (Editor tarafından foldout içinde çizilir)
    public List<Skill> requiredSkills; //bu seçenek için açılmış olması gereken skill'ler
    public List<StatCondition> statConditions; //bu seçenek için sağlanması gereken stat koşulları

    /// <summary>
    /// Tüm ön koşullar sağlanıyorsa true döner. Koşul yoksa her zaman true.
    /// </summary>
    public bool IsAvailable()
    {
        if (requiredSkills != null && requiredSkills.Count > 0)
        {
            if (SkillTreeManager.Instance == null) return false;
            for (int i = 0; i < requiredSkills.Count; i++)
            {
                if (requiredSkills[i] != null && !SkillTreeManager.Instance.IsUnlocked(requiredSkills[i].id))
                    return false;
            }
        }

        if (statConditions != null && statConditions.Count > 0)
        {
            for (int i = 0; i < statConditions.Count; i++)
            {
                if (!statConditions[i].IsMet())
                    return false;
            }
        }

        return true;
    }
}
