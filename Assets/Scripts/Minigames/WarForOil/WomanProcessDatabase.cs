using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/WarForOil/WomanProcessDatabase")]
public class WomanProcessDatabase : ScriptableObject
{
    [Header("Başlangıç Ayarları")]
    public float initialObsession = 40f; //kadın süreci başlangıç stat değeri (0-100)

    [Header("Bitiş Koşulları")]
    public float endThreshold = 10f; //stat bu değerin altına düşerse süreç biter
    //stat 100'e ulaşırsa game over (suspicion üzerinden GameStatManager tetikler)

    [Header("Kademe Eşikleri")]
    public float tier1Max = 30f; //0 - tier1Max = kademe 1
    public float tier2Max = 65f; //tier1Max - tier2Max = kademe 2
    //tier2Max - 100 = kademe 3

    [Header("Event Havuzları")]
    public List<WarForOilEvent> tier1Events; //kademe 1 event havuzu (düşük obsesyon)
    public List<WarForOilEvent> tier2Events; //kademe 2 event havuzu (orta obsesyon)
    public List<WarForOilEvent> tier3Events; //kademe 3 event havuzu (yüksek obsesyon)

    [Header("Event Sıklığı (her N eventte M kadın eventi)")]
    public int tier1Frequency = 5; //kademe 1'de her 5 eventte
    public int tier1WomanCount = 1; //1 kadın eventi
    public int tier2Frequency = 3; //kademe 2'de her 3 eventte
    public int tier2WomanCount = 1; //1 kadın eventi
    public int tier3Frequency = 2; //kademe 3'te her 2 eventte
    public int tier3WomanCount = 1; //1 kadın eventi

    [Header("Karar Süresi")]
    public float decisionTime = 10f; //kadın eventi karar süresi (saniye)

    /// <summary>
    /// Mevcut obsesyon değerine göre aktif kademeyi döner (1, 2 veya 3).
    /// </summary>
    public int GetTier(float obsession)
    {
        if (obsession <= tier1Max) return 1;
        if (obsession <= tier2Max) return 2;
        return 3;
    }

    /// <summary>
    /// Kademeye göre event havuzunu döner.
    /// </summary>
    public List<WarForOilEvent> GetTierEvents(int tier)
    {
        switch (tier)
        {
            case 1: return tier1Events;
            case 2: return tier2Events;
            case 3: return tier3Events;
            default: return tier3Events;
        }
    }

    /// <summary>
    /// Kademeye göre event sıklığını döner (her N eventte).
    /// </summary>
    public int GetTierFrequency(int tier)
    {
        switch (tier)
        {
            case 1: return tier1Frequency;
            case 2: return tier2Frequency;
            case 3: return tier3Frequency;
            default: return tier3Frequency;
        }
    }

    /// <summary>
    /// Kademeye göre döngü başına kaç kadın eventi geleceğini döner.
    /// </summary>
    public int GetTierWomanCount(int tier)
    {
        switch (tier)
        {
            case 1: return Mathf.Max(1, tier1WomanCount);
            case 2: return Mathf.Max(1, tier2WomanCount);
            case 3: return Mathf.Max(1, tier3WomanCount);
            default: return Mathf.Max(1, tier3WomanCount);
        }
    }

    /// <summary>
    /// Kademenin obsesyon aralığını döner (min, max).
    /// </summary>
    public void GetTierRange(int tier, out float tierMin, out float tierMax)
    {
        switch (tier)
        {
            case 1:
                tierMin = 0f;
                tierMax = tier1Max;
                break;
            case 2:
                tierMin = tier1Max;
                tierMax = tier2Max;
                break;
            case 3:
                tierMin = tier2Max;
                tierMax = 100f;
                break;
            default:
                tierMin = 0f;
                tierMax = 100f;
                break;
        }
    }
}
