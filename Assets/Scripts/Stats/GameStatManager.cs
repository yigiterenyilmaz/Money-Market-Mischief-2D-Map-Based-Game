using System;
using System.Collections.Generic;
using UnityEngine;

public class GameStatManager : MonoBehaviour
{
    public static GameStatManager Instance { get; private set; }

    [Header("Starting Values")]
    public float startingWealth = 1000f;
    public float startingSuspicion = 0f;
    public float startingReputation = 50f;
    public float startingPoliticalInfluence = 0f;

    [Header("Suspicion Settings")]
    public float minSuspicion = 0f;
    public float maxSuspicion = 100f;

    [Header("Reputation Settings")]
    public float minReputation = -50f; //eksiye düşebilir, bu değere ulaşınca game over
    public float maxReputation = 100f;
    public float reputationCeilingPenaltyMultiplier = 1f; //eksiye düşüşün kaç katı tavandan düşer (1 = aynı, 2 = 2 katı, 0.5 = yarısı)

    [Header("Political Influence Settings")]
    public float minPoliticalInfluence = -100f;
    public float maxPoliticalInfluence = 100f;

    [Header("Suspicion Modifier Settings (itibar etkisi)")]
    public float baseSuspicionMultiplier = 1.5f; //itibar 0'da çarpan
    public float minSuspicionMultiplier = 0.5f;  //itibar 100'de çarpan

    [Header("Skill Efficiency Settings (siyasi nüfuz etkisi)")]
    public float minSkillEfficiency = 0.5f;  //nüfuz -100'de
    public float maxSkillEfficiency = 1.5f;  //nüfuz +100'de

    //runtime values
    private float wealth;
    private float suspicion;
    private float reputation;
    private float politicalInfluence;

    //itibar tavan düşüşü — eksiye düşülen en düşük nokta kadar tavan kalıcı olarak düşer
    private float lowestNegativeReputation = 0f; //en düşük negatif itibar noktası (0 = hiç eksiye düşmedi)

    //dinamik stat tavanları — choice etkisiyle doğal sınırın altına çekilir
    private Dictionary<StatType, float> statCeilings = new Dictionary<StatType, float>();

    //kalıcı stat çarpanları — çarpımsal birikir (1.0 = etkisiz, 1.1 = %10 bonus)
    private float wealthGainMultiplier = 1f;
    private float suspicionGainMultiplier = 1f;
    private float reputationGainMultiplier = 1f;
    private float politicalInfluenceGainMultiplier = 1f;

    //events
    public static event Action<StatType, float, float> OnStatChanged; //stat, oldValue, newValue
    public static event Action OnGameOver; //şüphe 100'e ulaştığında
    public static event Action<StatType, float> OnPermanentMultiplierChanged; //stat, yeni toplam çarpan
    public static event Action<StatType, float> OnStatCeilingSet; //stat, yeni tavan
    public static event Action<StatType> OnStatCeilingRemoved; //stat tavan kaldırıldı

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        wealth = startingWealth;
        suspicion = startingSuspicion;
        reputation = startingReputation;
        politicalInfluence = startingPoliticalInfluence;
    }

    #region Getters

    public float GetStat(StatType statType)
    {
        return statType switch
        {
            StatType.Wealth => wealth,
            StatType.Suspicion => suspicion,
            StatType.Reputation => reputation,
            StatType.PoliticalInfluence => politicalInfluence,
            _ => 0f
        };
    }

    public float Wealth => wealth;
    public float Suspicion => suspicion;
    public float Reputation => reputation;
    public float PoliticalInfluence => politicalInfluence;

    #endregion

    #region Modifiers (hesaplayıcılar)

    /// <summary>
    /// Efektif itibar tavanı. Eksiye düşülen en düşük nokta kadar tavan kalıcı olarak düşer.
    /// Örn: en düşük -20 → tavan = 100 - 20 = 80
    /// </summary>
    /// <summary>
    /// Efektif itibar tavanı. Eksiye düşülen en düşük nokta * çarpan kadar tavan kalıcı olarak düşer.
    /// Örn: en düşük -20, çarpan 1.5 → tavan = 100 - 30 = 70
    /// </summary>
    public float EffectiveMaxReputation => maxReputation + (lowestNegativeReputation * reputationCeilingPenaltyMultiplier);

    //itibar bazlı şüphe çarpanı
    //yüksek itibar = düşük çarpan (şüphe daha az artar)
    //düşük itibar = yüksek çarpan (şüphe daha çok artar)
    public float GetSuspicionMultiplier()
    {
        //itibar 0 → baseSuspicionMultiplier (1.5)
        //itibar 100 → minSuspicionMultiplier (0.5)
        float effectiveMax = EffectiveMaxReputation;
        float t = effectiveMax > 0f ? reputation / effectiveMax : 0f;
        t = Mathf.Clamp01(t);
        return Mathf.Lerp(baseSuspicionMultiplier, minSuspicionMultiplier, t);
    }

    //siyasi nüfuz bazlı skill verim çarpanı
    //yüksek nüfuz = yüksek verim
    //düşük nüfuz = düşük verim
    public float GetSkillEfficiencyMultiplier()
    {
        //nüfuz -100 → minSkillEfficiency (0.5)
        //nüfuz 0 → 1.0
        //nüfuz +100 → maxSkillEfficiency (1.5)
        float t = (politicalInfluence - minPoliticalInfluence) / (maxPoliticalInfluence - minPoliticalInfluence);
        return Mathf.Lerp(minSkillEfficiency, maxSkillEfficiency, t);
    }

    /// <summary>
    /// Belirtilen stat'ın kalıcı kazanım çarpanını çarpımsal olarak uygular.
    /// Örn: ApplyPermanentGainMultiplier(Reputation, 1.1f) → mevcut çarpan *= 1.1
    /// </summary>
    public void ApplyPermanentGainMultiplier(StatType statType, float multiplier)
    {
        switch (statType)
        {
            case StatType.Wealth:
                wealthGainMultiplier *= multiplier;
                break;
            case StatType.Suspicion:
                suspicionGainMultiplier *= multiplier;
                break;
            case StatType.Reputation:
                reputationGainMultiplier *= multiplier;
                break;
            case StatType.PoliticalInfluence:
                politicalInfluenceGainMultiplier *= multiplier;
                break;
        }
        OnPermanentMultiplierChanged?.Invoke(statType, GetPermanentGainMultiplier(statType));
    }

    public float GetPermanentGainMultiplier(StatType statType)
    {
        return statType switch
        {
            StatType.Wealth => wealthGainMultiplier,
            StatType.Suspicion => suspicionGainMultiplier,
            StatType.Reputation => reputationGainMultiplier,
            StatType.PoliticalInfluence => politicalInfluenceGainMultiplier,
            _ => 1f
        };
    }

    /// <summary>
    /// Stat'a dinamik tavan koyar. Doğal sınırın altına çeker.
    /// Mevcut değer tavandan yüksekse tavana clamp edilir.
    /// </summary>
    public void SetStatCeiling(StatType statType, float ceilingValue)
    {
        float naturalMax = GetNaturalMax(statType);
        ceilingValue = Mathf.Min(ceilingValue, naturalMax); //doğal sınırı aşamaz

        statCeilings[statType] = ceilingValue;
        OnStatCeilingSet?.Invoke(statType, ceilingValue);

        //mevcut değer yeni tavandan yüksekse clamp et
        float current = GetStat(statType);
        if (current > ceilingValue)
        {
            SetStat(statType, ceilingValue);
        }
    }

    /// <summary>
    /// Stat'ın dinamik tavanını kaldırır, doğal sınıra döner.
    /// </summary>
    public void RemoveStatCeiling(StatType statType)
    {
        if (statCeilings.Remove(statType))
        {
            OnStatCeilingRemoved?.Invoke(statType);
        }
    }

    /// <summary>
    /// Stat'ın aktif tavanını döner. Dinamik tavan varsa onu, yoksa doğal sınırı döner.
    /// </summary>
    public float GetEffectiveMax(StatType statType)
    {
        float naturalMax = GetNaturalMax(statType);
        if (statCeilings.TryGetValue(statType, out float ceiling))
        {
            return Mathf.Min(ceiling, naturalMax);
        }
        return naturalMax;
    }

    /// <summary>
    /// Stat'ın doğal (tanımlı) maksimum sınırını döner.
    /// </summary>
    public float GetNaturalMax(StatType statType)
    {
        return statType switch
        {
            StatType.Wealth => float.MaxValue,
            StatType.Suspicion => maxSuspicion,
            StatType.Reputation => EffectiveMaxReputation,
            StatType.PoliticalInfluence => maxPoliticalInfluence,
            _ => float.MaxValue
        };
    }

    /// <summary>
    /// Stat'ın dinamik tavanı aktif mi.
    /// </summary>
    public bool HasStatCeiling(StatType statType)
    {
        return statCeilings.ContainsKey(statType);
    }

    #endregion

    #region Stat Modification

    public void ModifyStat(StatType statType, float amount)
    {
        switch (statType)
        {
            case StatType.Wealth:
                AddWealth(amount);
                break;
            case StatType.Suspicion:
                AddSuspicion(amount);
                break;
            case StatType.Reputation:
                AddReputation(amount);
                break;
            case StatType.PoliticalInfluence:
                AddPoliticalInfluence(amount);
                break;
        }
    }

    public void AddWealth(float amount)
    {
        if (amount > 0)
            amount *= wealthGainMultiplier;

        float oldValue = wealth;
        wealth += amount;

        //dinamik tavan kontrolü
        if (statCeilings.TryGetValue(StatType.Wealth, out float wealthCeiling) && wealth > wealthCeiling)
            wealth = wealthCeiling;

        if (oldValue != wealth)
        {
            OnStatChanged?.Invoke(StatType.Wealth, oldValue, wealth);
        }
    }

    //şüphe ekleme - itibar çarpanı uygulanır (sadece artışlarda)
    public void AddSuspicion(float amount)
    {
        float oldValue = suspicion;

        //sadece pozitif değerlerde (şüphe artışında) çarpanlar uygulanır
        if (amount > 0)
        {
            amount *= GetSuspicionMultiplier();
            amount *= suspicionGainMultiplier;
        }

        float suspicionMax = GetEffectiveMax(StatType.Suspicion);
        suspicion = Mathf.Clamp(suspicion + amount, minSuspicion, suspicionMax);

        if (oldValue != suspicion)
        {
            OnStatChanged?.Invoke(StatType.Suspicion, oldValue, suspicion);
        }

        //game over kontrolü
        if (suspicion >= maxSuspicion)
        {
            OnGameOver?.Invoke();
        }
    }

    //şüphe ekleme - çarpan UYGULANMADAN (özel durumlar için)
    public void AddSuspicionRaw(float amount)
    {
        float oldValue = suspicion;
        float suspicionMax = GetEffectiveMax(StatType.Suspicion);
        suspicion = Mathf.Clamp(suspicion + amount, minSuspicion, suspicionMax);

        if (oldValue != suspicion)
        {
            OnStatChanged?.Invoke(StatType.Suspicion, oldValue, suspicion);
        }

        if (suspicion >= maxSuspicion)
        {
            OnGameOver?.Invoke();
        }
    }

    public void AddReputation(float amount)
    {
        if (amount > 0)
            amount *= reputationGainMultiplier;

        float oldValue = reputation;
        float repMax = GetEffectiveMax(StatType.Reputation);
        reputation = Mathf.Clamp(reputation + amount, minReputation, repMax);

        //eksiye düştüyse en düşük noktayı güncelle (tavan kalıcı olarak düşer)
        if (reputation < 0f && reputation < lowestNegativeReputation)
        {
            lowestNegativeReputation = reputation;
        }

        if (oldValue != reputation)
        {
            OnStatChanged?.Invoke(StatType.Reputation, oldValue, reputation);
        }

        //game over kontrolü — itibar minimum değere ulaştığında
        if (reputation <= minReputation)
        {
            OnGameOver?.Invoke();
        }
    }

    public void AddPoliticalInfluence(float amount)
    {
        if (amount > 0)
            amount *= politicalInfluenceGainMultiplier;

        float oldValue = politicalInfluence;
        float piMax = GetEffectiveMax(StatType.PoliticalInfluence);
        politicalInfluence = Mathf.Clamp(politicalInfluence + amount, minPoliticalInfluence, piMax);

        if (oldValue != politicalInfluence)
        {
            OnStatChanged?.Invoke(StatType.PoliticalInfluence, oldValue, politicalInfluence);
        }
    }

    #endregion

    #region Set Methods

    public void SetStat(StatType statType, float value)
    {
        float oldValue = GetStat(statType);

        switch (statType)
        {
            case StatType.Wealth:
                wealth = value;
                break;
            case StatType.Suspicion:
                suspicion = Mathf.Clamp(value, minSuspicion, GetEffectiveMax(StatType.Suspicion));
                break;
            case StatType.Reputation:
                reputation = Mathf.Clamp(value, minReputation, GetEffectiveMax(StatType.Reputation));
                if (reputation < 0f && reputation < lowestNegativeReputation)
                    lowestNegativeReputation = reputation;
                break;
            case StatType.PoliticalInfluence:
                politicalInfluence = Mathf.Clamp(value, minPoliticalInfluence, GetEffectiveMax(StatType.PoliticalInfluence));
                break;
            default:
                return;
        }

        float newValue = GetStat(statType);
        if (oldValue != newValue)
        {
            OnStatChanged?.Invoke(statType, oldValue, newValue);
        }

        //set ile de game over kontrolü
        if (statType == StatType.Suspicion && suspicion >= maxSuspicion)
        {
            OnGameOver?.Invoke();
        }
        if (statType == StatType.Reputation && reputation <= minReputation)
        {
            OnGameOver?.Invoke();
        }
    }

    #endregion

    #region Utility Methods

    public bool HasEnoughWealth(float amount)
    {
        return wealth >= amount;
    }

    public bool TrySpendWealth(float amount)
    {
        if (!HasEnoughWealth(amount))
            return false;

        AddWealth(-amount);
        return true;
    }

    //stat yüzdesini döner (UI için)
    public float GetStatPercent(StatType statType)
    {
        return statType switch
        {
            StatType.Suspicion => suspicion / maxSuspicion,
            StatType.Reputation => EffectiveMaxReputation > 0f ? reputation / EffectiveMaxReputation : 0f,
            StatType.PoliticalInfluence => (politicalInfluence - minPoliticalInfluence) / (maxPoliticalInfluence - minPoliticalInfluence),
            _ => 0f
        };
    }

    #endregion
}
