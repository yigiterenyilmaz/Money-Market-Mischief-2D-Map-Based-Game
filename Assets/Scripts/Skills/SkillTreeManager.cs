using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    public SkillDatabase database;
    private HashSet<string> unlockedSkillIds = new HashSet<string>();
    private HashSet<string> blockedSkillIds = new HashSet<string>(); //sonsuza kadar kilitli skiller

    //passive income — ürün tabanlı sistem
    private HashSet<PassiveIncomeProduct> unlockedProducts = new HashSet<PassiveIncomeProduct>();
    private Dictionary<PassiveIncomeProduct, int> ownedProducts = new Dictionary<PassiveIncomeProduct, int>();
    private float passiveIncomeTimer = 0f;
    private const float PASSIVE_INCOME_INTERVAL = 10f; //kaç saniyede bir gelir eklenir
    private float investmentTickTimer = 0f;
    private const float INVESTMENT_TICK_INTERVAL = 2f; //yatırım fiyat güncelleme aralığı (saniye)

    //passive income — direkt gelir (skill açılınca akan, zamanla azalan gelir)
    //decay eğrisi her kaynak için ayrı tutulur (effect üzerinden gelir)
    private List<DirectIncomeSource> directIncomeSources = new List<DirectIncomeSource>();

    //investment — yatırım sistemi (al, değeri artsın, sat)
    private HashSet<InvestmentProduct> unlockedInvestments = new HashSet<InvestmentProduct>();
    private List<OwnedInvestment> ownedInvestments = new List<OwnedInvestment>();
    private Dictionary<InvestmentProduct, float> marketPricePercents = new Dictionary<InvestmentProduct, float>(); //piyasa salınım yüzdesi
    private bool marketOscillationPaused = false; //ekran açıkken salınım durur
    private Dictionary<InvestmentProduct, bool> investmentAvailable = new Dictionary<InvestmentProduct, bool>(); //piyasada şu an bulunuyor mu
    private Dictionary<InvestmentProduct, float> investmentPhaseEndTime = new Dictionary<InvestmentProduct, float>(); //mevcut fazın bitiş zamanı
    private Dictionary<InvestmentProduct, float> effectiveProfitChance = new Dictionary<InvestmentProduct, float>(); //streak breaker ile değişen kâr olasılığı

    //jeweler — kuyumcu sistemi
    private bool jewelerUnlocked = false;
    private List<JewelerProduct> availableJewelers = new List<JewelerProduct>(); //satın alınabilir kuyumcular
    private List<OwnedJeweler> ownedJewelers = new List<OwnedJeweler>();

    //training — bilim adamı eğitim sistemi
    private bool trainingUnlocked = false;
    private List<ScientistTraining> scientists = new List<ScientistTraining>();
    private float trainingCoefficient = 0f;
    private float trainingBaseSweetSpot = 0f;
    private float trainingSweetSpotGrowthRate = 0f;

    //events — passive income
    public static event Action<List<PassiveIncomeProduct>> OnProductsUnlocked; //yeni ürünler satın alınabilir
    public static event Action<PassiveIncomeProduct, int> OnProductBought; //ürün, yeni toplam adet
    public static event Action<PassiveIncomeProduct, int> OnProductSold; //ürün, yeni toplam adet
    public static event Action<float> OnPassiveIncomeTick; //bu tick'te kazanılan toplam gelir

    //events — investment
    public static event Action<List<InvestmentProduct>> OnInvestmentsUnlocked; //yeni yatırımlar açıldı
    public static event Action<int> OnInvestmentBought; //yatırım index'i
    public static event Action<int, float> OnInvestmentSold; //yatırım index'i, kâr/zarar
    public static event Action<int, float> OnInvestmentValueChanged; //yatırım index'i, yeni değer
    public static event Action<InvestmentProduct, float> OnMarketPriceChanged; //product, yeni piyasa fiyatı
    public static event Action<InvestmentProduct, bool> OnInvestmentAvailabilityChanged; //product, available?

    //events — jeweler
    public static event Action OnJewelerUnlocked; //kuyumcu satın alınabilir
    public static event Action<OwnedJeweler> OnJewelerBought; //kuyumcu satın alındı
    public static event Action<float, float, float> OnJewelerTick; //gelir, şüphe, itibar (tick başına toplam)

    //events — training
    public static event Action OnTrainingUnlocked; //eğitim sistemi açıldı
    public static event Action<int> OnScientistStarted; //yeni bilim adamı index'i
    public static event Action<int, float> OnScientistLevelChanged; //bilim adamı index'i, yeni seviye
    public static event Action<int> OnScientistCompleted; //eğitimi tamamlanan bilim adamı index'i

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        passiveIncomeTimer += Time.deltaTime;
        if (passiveIncomeTimer >= PASSIVE_INCOME_INTERVAL)
        {
            passiveIncomeTimer = 0f;

            //pasif gelir (ürün + direkt)
            bool hasIncome = ownedProducts.Count > 0 || directIncomeSources.Count > 0;
            if (hasIncome && GameStatManager.Instance != null)
            {
                float totalIncome = 0f;

                //ürün tabanlı gelir
                foreach (var kvp in ownedProducts)
                {
                    PassiveIncomeProduct product = kvp.Key;
                    int count = kvp.Value;
                    float incomePerUnit = UnityEngine.Random.Range(product.minIncomePerSecond, product.maxIncomePerSecond);
                    totalIncome += incomePerUnit * count;
                }

                totalIncome *= PASSIVE_INCOME_INTERVAL;

                //direkt gelir (her kaynak kendi decay eğrisiyle)
                float directTotal = CalculateDirectIncome();
                totalIncome += directTotal * PASSIVE_INCOME_INTERVAL;

                GameStatManager.Instance.AddWealth(totalIncome);
                OnPassiveIncomeTick?.Invoke(totalIncome);
            }

            //kuyumcu tick'i — gelir, şüphe, itibar
            if (ownedJewelers.Count > 0 && GameStatManager.Instance != null)
            {
                float totalJewelerIncome = 0f;
                float totalSuspicion = 0f;
                float totalReputation = 0f;

                //eğitim çarpanı: 0=0x, 50=1x, 75=2.25x, 100=4x şüphe
                float educationMultiplier = 1f;
                if (CountryData.Instance != null)
                {
                    float t = CountryData.Instance.EducationIndex / 50f;
                    educationMultiplier = t * t;
                }

                for (int i = 0; i < ownedJewelers.Count; i++)
                {
                    JewelerProduct product = ownedJewelers[i].product;
                    totalJewelerIncome += product.incomePer10Seconds;

                    if (product.jewelerType == JewelerType.Illegal)
                        totalSuspicion += product.suspicionPerMinute / 6f * educationMultiplier;

                    if (product.jewelerType == JewelerType.Legal)
                        totalReputation += product.reputationPerMinute / 6f;
                }

                if (totalJewelerIncome != 0f)
                    GameStatManager.Instance.AddWealth(totalJewelerIncome);
                if (totalSuspicion != 0f)
                    GameStatManager.Instance.AddSuspicion(totalSuspicion);
                if (totalReputation != 0f)
                    GameStatManager.Instance.AddReputation(totalReputation);

                OnJewelerTick?.Invoke(totalJewelerIncome, totalSuspicion, totalReputation);
            }

        }

        //yatırım tick'i (sahip olunan + piyasa salınımı)
        investmentTickTimer += Time.deltaTime;
        if (investmentTickTimer >= INVESTMENT_TICK_INTERVAL)
        {
            investmentTickTimer = 0f;

            //sahip olunan yatırımların fiyat simülasyonu
            for (int i = 0; i < ownedInvestments.Count; i++)
            {
                UpdateInvestmentPrice(ownedInvestments[i]);
                OnInvestmentValueChanged?.Invoke(i, ownedInvestments[i].currentValue);
            }

            //piyasa fiyat salınımı (ekran açık değilse)
            if (!marketOscillationPaused)
            {
                foreach (InvestmentProduct product in unlockedInvestments)
                {
                    if (!marketPricePercents.ContainsKey(product)) continue;

                    float mp = marketPricePercents[product];
                    float range = product.idleOscillationMax - product.idleOscillationMin;
                    float center = (product.idleOscillationMax + product.idleOscillationMin) / 2f;
                    float noise = UnityEngine.Random.Range(-range * 0.1f, range * 0.1f);
                    float pullToCenter = (center - mp) * 0.05f;

                    mp += noise + pullToCenter;
                    mp = Mathf.Clamp(mp, product.idleOscillationMin, product.idleOscillationMax);
                    marketPricePercents[product] = mp;

                    OnMarketPriceChanged?.Invoke(product, product.cost * (1f + mp / 100f));
                }
            }

            //piyasa erişilebilirlik faz geçişleri (ekran durumundan bağımsız çalışır)
            foreach (InvestmentProduct product in unlockedInvestments)
            {
                if (!product.hasLimitedAvailability) continue;
                if (!investmentPhaseEndTime.ContainsKey(product)) continue;

                if (Time.time >= investmentPhaseEndTime[product])
                {
                    bool wasAvailable = investmentAvailable.ContainsKey(product) && investmentAvailable[product];

                    if (wasAvailable)
                    {
                        //available → unavailable
                        float duration = product.availabilityCycleDuration * (1f - product.availabilityChance);
                        duration *= UnityEngine.Random.Range(0.7f, 1.3f);
                        investmentAvailable[product] = false;
                        investmentPhaseEndTime[product] = Time.time + duration;
                    }
                    else
                    {
                        //unavailable → available (min süre garantili)
                        float duration = Mathf.Max(
                            product.minAvailableDuration,
                            product.availabilityCycleDuration * product.availabilityChance
                        );
                        duration *= UnityEngine.Random.Range(0.7f, 1.3f);
                        investmentAvailable[product] = true;
                        investmentPhaseEndTime[product] = Time.time + duration;
                    }

                    OnInvestmentAvailabilityChanged?.Invoke(product, investmentAvailable[product]);
                }
            }
        }
    }

    /// <summary>
    /// Tüm direkt gelir kaynaklarının decay uygulanmış anlık saniye başına toplamını hesaplar.
    /// Süresi dolan kaynakları temizler.
    /// </summary>
    private float CalculateDirectIncome()
    {
        float total = 0f;

        for (int i = directIncomeSources.Count - 1; i >= 0; i--)
        {
            DirectIncomeSource source = directIncomeSources[i];
            float elapsed = Time.time - source.startTime;
            float multiplier = GetDecayMultiplier(elapsed, source);

            //süresi doldu — listeden çıkar
            if (multiplier <= 0f)
            {
                directIncomeSources.RemoveAt(i);
                continue;
            }

            float randomized = source.incomePerSecond * UnityEngine.Random.Range(0.95f, 1.05f);
            total += randomized * multiplier;
        }

        return total;
    }

    /// <summary>
    /// Kaynağın kendi decay eğrisinden çarpanı hesaplar.
    /// 0-5dk arası: keypoint'ler arası linear interpolation.
    /// 5dk sonrası: son segmentin eğimiyle sıfıra kadar devam eder.
    /// </summary>
    private float GetDecayMultiplier(float elapsedSeconds, DirectIncomeSource source)
    {
        float minutes = elapsedSeconds / 60f;

        if (minutes <= 0f) return 1f;

        //keypoint'ler: dakika 0 = %100, dakika 1-5 = kaynağın kendi değerleri
        float k0 = 100f;
        float k1 = source.decayAt1Min;
        float k2 = source.decayAt2Min;
        float k3 = source.decayAt3Min;
        float k4 = source.decayAt4Min;
        float k5 = source.decayAt5Min;

        float value;

        if (minutes < 1f)
            value = Mathf.Lerp(k0, k1, minutes);
        else if (minutes < 2f)
            value = Mathf.Lerp(k1, k2, minutes - 1f);
        else if (minutes < 3f)
            value = Mathf.Lerp(k2, k3, minutes - 2f);
        else if (minutes < 4f)
            value = Mathf.Lerp(k3, k4, minutes - 3f);
        else if (minutes < 5f)
            value = Mathf.Lerp(k4, k5, minutes - 4f);
        else
        {
            //5dk sonrası: 4-5dk arasındaki eğimle devam et
            float slopePerMinute = k5 - k4;
            value = k5 + slopePerMinute * (minutes - 5f);
        }

        return Mathf.Max(0f, value / 100f);
    }

    /// <summary>
    /// Tek bir yatırımın fiyatını simüle eder.
    /// Potansiyele ulaşana kadar zigzag hareket, sonra küçük salınım, timeout sonrası drift.
    /// </summary>
    private void UpdateInvestmentPrice(OwnedInvestment inv)
    {
        if (!inv.reachedPotential)
        {
            //potansiyele doğru zigzag hareket
            float distanceToTarget = inv.targetPercent - inv.currentPercent;
            float trendStep = distanceToTarget * inv.trendSpeed; //kalan mesafenin trendSpeed oranı kadar çek
            float noise = UnityEngine.Random.Range(-inv.product.volatility, inv.product.volatility);
            inv.currentPercent += trendStep + noise;

            //potansiyele ulaştı mı kontrol
            bool reached = (inv.targetPercent >= 0f && inv.currentPercent >= inv.targetPercent) ||
                           (inv.targetPercent < 0f && inv.currentPercent <= inv.targetPercent);

            if (reached)
            {
                inv.reachedPotential = true;
                inv.reachedPotentialTime = Time.time;
                inv.currentPercent = inv.targetPercent;
            }
        }
        else
        {
            //küçük salınımlar (potansiyel civarında)
            float smallNoise = UnityEngine.Random.Range(
                -inv.product.volatility * 0.3f,
                inv.product.volatility * 0.3f
            );

            float timeSinceReached = Time.time - inv.reachedPotentialTime;

            if (timeSinceReached > inv.product.postPotentialTimeout)
            {
                //drift fazı — potansiyelden sonra yön sapması
                float driftPerTick = inv.product.postPotentialDrift * (INVESTMENT_TICK_INTERVAL / 60f);
                inv.currentPercent += driftPerTick + smallNoise;
            }
            else
            {
                //potansiyel civarında küçük salınım (geri çekme kuvvetiyle)
                float pullBack = (inv.targetPercent - inv.currentPercent) * 0.15f;
                inv.currentPercent += pullBack + smallNoise;
            }
        }

        //güncel değeri hesapla
        inv.currentValue = inv.buyPrice * (1f + inv.currentPercent / 100f);

        //değer sıfırın altına düşmesin
        if (inv.currentValue < 0f)
            inv.currentValue = 0f;
    }

    // ==================== SKİLL SİSTEMİ ====================

    public bool IsUnlocked(string skillId)
    {
        return unlockedSkillIds.Contains(skillId);
    }

    public bool IsBlocked(string skillId)
    {
        return blockedSkillIds.Contains(skillId);
    }

    public bool CanUnlock(Skill skill)
    {
        if (skill == null)
            return false;

        if (IsUnlocked(skill.id))
            return false;

        if (IsBlocked(skill.id))
            return false;

        if (!GameStatManager.Instance.HasEnoughWealth(skill.cost))
            return false;

        if (skill.prerequisites != null)
        {
            foreach (Skill prerequisite in skill.prerequisites)
            {
                if (!IsUnlocked(prerequisite.id))
                    return false;
            }
        }

        //diğer ön koşullar kontrolü (flags enum)
        if (skill.otherPrerequisites != OtherPrerequisite.None)
        {
            if (!CheckOtherPrerequisites(skill.otherPrerequisites))
                return false;
        }

        return true;
    }

    public bool TryUnlock(string skillId)
    {
        Skill skill = database.GetById(skillId);

        if (skill == null)
            return false;
        if (!CanUnlock(skill))
            return false;

        GameStatManager.Instance.TrySpendWealth(skill.cost);

        unlockedSkillIds.Add(skillId);

        if (skill.effects != null)
        {
            foreach (SkillEffect effect in skill.effects)
            {
                effect.Apply();
            }
        }

        if (skill.blocksSkills != null)
        {
            foreach (Skill blockedSkill in skill.blocksSkills)
            {
                if (blockedSkill != null && !IsUnlocked(blockedSkill.id))
                {
                    blockedSkillIds.Add(blockedSkill.id);
                    SkillEvents.OnSkillBlocked?.Invoke(blockedSkill);
                }
            }
        }

        SkillEvents.OnSkillUnlocked?.Invoke(skill);

        return true;
    }

    /// <summary>
    /// Skill'i tekrar kilitler. unlockedSkills'den çıkarır ama blockedSkills'e EKLEMEZ.
    /// Böylece oyuncu tekrar satın alabilir. WarForOil zincir çöküşünde kullanılır.
    /// </summary>
    public void RelockSkill(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        if (!unlockedSkillIds.Contains(skillId)) return;

        unlockedSkillIds.Remove(skillId);
    }

    public List<Skill> GetAvailableSkills()
    {
        List<Skill> availableSkills = new List<Skill>();

        foreach (Skill skill in database.allSkills)
        {
            if (CanUnlock(skill))
            {
                availableSkills.Add(skill);
            }
        }

        return availableSkills;
    }

    // ==================== ÜRÜN SİSTEMİ ====================

    /// <summary>
    /// PassiveIncomeEffect tarafından çağrılır. Ürünleri satın alınabilir yapar.
    /// </summary>
    public void UnlockProducts(List<PassiveIncomeProduct> products)
    {
        List<PassiveIncomeProduct> newlyUnlocked = new List<PassiveIncomeProduct>();

        foreach (PassiveIncomeProduct product in products)
        {
            if (product != null && unlockedProducts.Add(product))
            {
                newlyUnlocked.Add(product);
            }
        }

        if (newlyUnlocked.Count > 0)
            OnProductsUnlocked?.Invoke(newlyUnlocked);
    }

    /// <summary>
    /// UI bu metodu çağırır. Ürün satın alır, parayı öder.
    /// </summary>
    public bool BuyProduct(PassiveIncomeProduct product)
    {
        if (product == null) return false;
        if (!unlockedProducts.Contains(product)) return false;
        if (GameStatManager.Instance == null) return false;
        if (!GameStatManager.Instance.HasEnoughWealth(product.cost)) return false;

        GameStatManager.Instance.TrySpendWealth(product.cost);

        if (ownedProducts.ContainsKey(product))
            ownedProducts[product]++;
        else
            ownedProducts[product] = 1;

        OnProductBought?.Invoke(product, ownedProducts[product]);
        return true;
    }

    /// <summary>
    /// UI bu metodu çağırır. Ürün satar, maliyetin altına satar.
    /// </summary>
    public bool SellProduct(PassiveIncomeProduct product)
    {
        if (product == null) return false;
        if (!product.isSellable) return false;
        if (!ownedProducts.ContainsKey(product) || ownedProducts[product] <= 0) return false;

        //satış fiyatı = cost * sellRatio
        float sellPrice = product.cost * product.sellRatio;

        ownedProducts[product]--;

        if (ownedProducts[product] <= 0)
            ownedProducts.Remove(product);

        if (GameStatManager.Instance != null)
            GameStatManager.Instance.AddWealth(sellPrice);

        int remaining = ownedProducts.ContainsKey(product) ? ownedProducts[product] : 0;
        OnProductSold?.Invoke(product, remaining);
        return true;
    }

    // ==================== DİREKT GELİR SİSTEMİ ====================

    /// <summary>
    /// DirectPassiveIncomeEffect tarafından çağrılır. Yeni bir gelir kaynağı ekler.
    /// Decay eğrisi effect'ten gelir, her kaynak kendi eğrisini taşır.
    /// </summary>
    public void AddDirectPassiveIncome(float incomePerSecond,
        float decayAt1Min, float decayAt2Min, float decayAt3Min,
        float decayAt4Min, float decayAt5Min)
    {
        directIncomeSources.Add(new DirectIncomeSource(
            incomePerSecond, Time.time,
            decayAt1Min, decayAt2Min, decayAt3Min, decayAt4Min, decayAt5Min
        ));
    }

    /// <summary>
    /// Şu anki toplam direkt geliri döner (decay uygulanmış, saniye başına).
    /// </summary>
    public float GetDirectPassiveIncome()
    {
        float total = 0f;
        for (int i = 0; i < directIncomeSources.Count; i++)
        {
            float elapsed = Time.time - directIncomeSources[i].startTime;
            float multiplier = GetDecayMultiplier(elapsed, directIncomeSources[i]);
            if (multiplier <= 0f) continue;
            total += directIncomeSources[i].incomePerSecond * multiplier;
        }
        return total;
    }

    // ==================== ÜRÜN GETTER'LARI ====================

    public bool IsProductUnlocked(PassiveIncomeProduct product)
    {
        return unlockedProducts.Contains(product);
    }

    public int GetOwnedCount(PassiveIncomeProduct product)
    {
        return ownedProducts.ContainsKey(product) ? ownedProducts[product] : 0;
    }

    public HashSet<PassiveIncomeProduct> GetUnlockedProducts()
    {
        return unlockedProducts;
    }

    public Dictionary<PassiveIncomeProduct, int> GetOwnedProducts()
    {
        return ownedProducts;
    }

    // ==================== YATIRIM SİSTEMİ ====================

    /// <summary>
    /// InvestmentEffect tarafından çağrılır. Yatırım ürünlerini satın alınabilir yapar.
    /// </summary>
    public void UnlockInvestments(List<InvestmentProduct> products)
    {
        List<InvestmentProduct> newlyUnlocked = new List<InvestmentProduct>();

        foreach (InvestmentProduct product in products)
        {
            if (product != null && unlockedInvestments.Add(product))
            {
                newlyUnlocked.Add(product);
                marketPricePercents[product] = 0f; //piyasa fiyatını başlat
                effectiveProfitChance[product] = product.profitChance; //streak breaker için başlangıç değeri

                //availability başlat (limited ise unavailable başla, hemen phase check tetiklensin)
                if (product.hasLimitedAvailability)
                {
                    investmentAvailable[product] = false;
                    investmentPhaseEndTime[product] = Time.time;
                }
            }
        }

        if (newlyUnlocked.Count > 0)
            OnInvestmentsUnlocked?.Invoke(newlyUnlocked);
    }

    /// <summary>
    /// UI bu metodu çağırır. Oyuncu istediği miktarı girer, o anki piyasa fiyatına bölünür.
    /// Küsüratlı adet olabilir (yatırım sonuçta).
    /// </summary>
    public int BuyInvestment(InvestmentProduct product, float investAmount)
    {
        if (product == null) return -1;
        if (investAmount <= 0f) return -1;
        if (!unlockedInvestments.Contains(product)) return -1;
        if (GameStatManager.Instance == null) return -1;
        if (!GameStatManager.Instance.HasEnoughWealth(investAmount)) return -1;

        //piyasada bulunmuyorsa satın alınamaz
        if (product.hasLimitedAvailability &&
            investmentAvailable.ContainsKey(product) &&
            !investmentAvailable[product])
            return -1;

        //en az 1 adet alabilecek kadar yatırım yapmalı
        float marketPrice = GetMarketPrice(product);
        if (investAmount < marketPrice) return -1;

        GameStatManager.Instance.TrySpendWealth(investAmount);

        float quantity = investAmount / marketPrice;

        //effectiveProfitChance ile oluştur
        float chance = effectiveProfitChance.ContainsKey(product) ? effectiveProfitChance[product] : product.profitChance;
        OwnedInvestment inv = new OwnedInvestment(product, investAmount, quantity, Time.time, chance);
        ownedInvestments.Add(inv);

        //streak breaker: her alımda profitChance'i ayarla
        if (product.isStreakBreakerActive)
        {
            float adjustment = UnityEngine.Random.Range(product.streakBreakerMin, product.streakBreakerMax) / 100f;
            if (product.streakBreakerType == StreakBreakerType.Rising)
                chance -= adjustment; //yükselen streak kırıcı — profitChance düşer
            else
                chance += adjustment; //düşen streak kırıcı — profitChance artar
            effectiveProfitChance[product] = Mathf.Clamp01(chance);
        }

        int index = ownedInvestments.Count - 1;
        OnInvestmentBought?.Invoke(index);
        return index;
    }

    /// <summary>
    /// UI bu metodu çağırır. Yatırımı satar, güncel değeri cüzdana ekler.
    /// Kâr/zarar = currentValue - buyPrice
    /// </summary>
    public bool SellInvestment(int index)
    {
        if (index < 0 || index >= ownedInvestments.Count) return false;
        if (GameStatManager.Instance == null) return false;

        OwnedInvestment inv = ownedInvestments[index];
        float profit = inv.currentValue - inv.buyPrice;

        GameStatManager.Instance.AddWealth(inv.currentValue);
        ownedInvestments.RemoveAt(index);

        OnInvestmentSold?.Invoke(index, profit);
        return true;
    }

    // ==================== YATIRIM GETTER'LARI ====================

    public bool IsInvestmentUnlocked(InvestmentProduct product)
    {
        return unlockedInvestments.Contains(product);
    }

    public HashSet<InvestmentProduct> GetUnlockedInvestments()
    {
        return unlockedInvestments;
    }

    public List<OwnedInvestment> GetOwnedInvestments()
    {
        return ownedInvestments;
    }

    public OwnedInvestment GetInvestment(int index)
    {
        if (index < 0 || index >= ownedInvestments.Count) return null;
        return ownedInvestments[index];
    }

    /// <summary>
    /// Ürün şu an piyasada alınabilir mi (limited availability kontrolü).
    /// </summary>
    public bool IsInvestmentAvailable(InvestmentProduct product)
    {
        if (!product.hasLimitedAvailability) return true;
        return investmentAvailable.ContainsKey(product) && investmentAvailable[product];
    }

    /// <summary>
    /// O anki piyasa fiyatını döner (base cost + salınım yüzdesi).
    /// </summary>
    public float GetMarketPrice(InvestmentProduct product)
    {
        float percent = marketPricePercents.ContainsKey(product) ? marketPricePercents[product] : 0f;
        return product.cost * (1f + percent / 100f);
    }

    /// <summary>
    /// Piyasa salınımını duraklat (oyuncu ürün ekranını açtığında).
    /// </summary>
    public void PauseMarketOscillation()
    {
        marketOscillationPaused = true;
    }

    /// <summary>
    /// Piyasa salınımını devam ettir (oyuncu ürün ekranını kapattığında).
    /// </summary>
    public void ResumeMarketOscillation()
    {
        marketOscillationPaused = false;
    }

    // ==================== EĞİTİM SİSTEMİ ====================

    /// <summary>
    /// UnlockTrainingEffect tarafından çağrılır. Bilim adamı eğitim sistemini açar.
    /// </summary>
    public void UnlockJeweler(List<JewelerProduct> jewelers)
    {
        if (jewelers != null)
        {
            for (int i = 0; i < jewelers.Count; i++)
            {
                if (!availableJewelers.Contains(jewelers[i]))
                    availableJewelers.Add(jewelers[i]);
            }
        }

        if (jewelerUnlocked) return;
        jewelerUnlocked = true;
        OnJewelerUnlocked?.Invoke();
    }

    /// <summary>
    /// UI bu metodu çağırır. Kuyumcu satın alır.
    /// Başarılıysa true, para yetmiyorsa veya kilitliyse false döner.
    /// </summary>
    public bool BuyJeweler(JewelerProduct product)
    {
        if (!jewelerUnlocked) return false;
        if (!availableJewelers.Contains(product)) return false;
        if (GameStatManager.Instance == null) return false;
        if (!GameStatManager.Instance.HasEnoughWealth(product.cost)) return false;

        GameStatManager.Instance.TrySpendWealth(product.cost);
        OwnedJeweler jeweler = new OwnedJeweler(product);
        ownedJewelers.Add(jeweler);
        OnJewelerBought?.Invoke(jeweler);
        return true;
    }

    public bool IsJewelerUnlocked()
    {
        return jewelerUnlocked;
    }

    public int GetJewelerCount()
    {
        return ownedJewelers.Count;
    }

    public List<JewelerProduct> GetAvailableJewelers()
    {
        return availableJewelers;
    }

    public List<OwnedJeweler> GetOwnedJewelers()
    {
        return ownedJewelers;
    }

    public void UnlockTraining(float coefficient, float baseSweetSpot, float sweetSpotGrowthRate)
    {
        if (trainingUnlocked) return;

        trainingCoefficient = coefficient;
        trainingBaseSweetSpot = baseSweetSpot;
        trainingSweetSpotGrowthRate = sweetSpotGrowthRate;
        trainingUnlocked = true;

        OnTrainingUnlocked?.Invoke();
    }

    /// <summary>
    /// UI bu metodu çağırır. Yeni bir bilim adamı eğitimine başlar.
    /// Dönen değer bilim adamının index'idir.
    /// </summary>
    public int StartScientistTraining(ScientistData data)
    {
        if (!trainingUnlocked) return -1;
        if (data == null) return -1;

        ScientistTraining scientist = new ScientistTraining(data, Time.time);
        scientists.Add(scientist);

        int index = scientists.Count - 1;
        OnScientistStarted?.Invoke(index);
        return index;
    }

    /// <summary>
    /// UI bu metodu çağırır. Belirtilen bilim adamına wealth yatırır.
    /// Verimlilik çan eğrisiyle hesaplanır: ideal noktaya yakın yatırımlar en verimli.
    /// İdeal nokta, o bilim adamının eğitim başlangıcından itibaren zamanla büyür.
    /// </summary>
    public bool InvestInScientist(int scientistIndex, float amount)
    {
        if (!trainingUnlocked) return false;
        if (scientistIndex < 0 || scientistIndex >= scientists.Count) return false;
        if (amount <= 0f) return false;
        if (GameStatManager.Instance == null) return false;
        if (!GameStatManager.Instance.HasEnoughWealth(amount)) return false;

        ScientistTraining scientist = scientists[scientistIndex];
        if (scientist.isCompleted) return false;

        //bu bilim adamının kendi zamanlayıcısından ideal noktayı hesapla (zeka hızı etkiler)
        float elapsed = Time.time - scientist.startTime;
        float sweetSpot = trainingBaseSweetSpot + trainingSweetSpotGrowthRate * scientist.data.intelligenceLevel * elapsed;

        //çan eğrisi: x * e^(1-x) — x=1'de zirve, altında ve üstünde düşer
        float newTotal = scientist.totalInvested + amount;
        float x = newTotal / sweetSpot;
        float efficiency = x * Mathf.Exp(1f - x);

        //puan hesapla ve uygula
        float points = amount * trainingCoefficient * efficiency;

        GameStatManager.Instance.TrySpendWealth(amount);
        scientist.totalInvested = newTotal;
        scientist.level += points;

        //tamamlanma kontrolü
        if (!scientist.isCompleted && scientist.level >= scientist.data.completeCost)
        {
            scientist.isCompleted = true;
            OnScientistCompleted?.Invoke(scientistIndex);
        }

        OnScientistLevelChanged?.Invoke(scientistIndex, scientist.level);
        return true;
    }

    /// <summary>
    /// Belirli bir bilim adamına belirli miktar yatırılsa kaç puan kazanılacağını hesaplar (UI önizleme).
    /// </summary>
    public float PreviewScientistInvestment(int scientistIndex, float amount)
    {
        if (!trainingUnlocked || amount <= 0f) return 0f;
        if (scientistIndex < 0 || scientistIndex >= scientists.Count) return 0f;

        ScientistTraining scientist = scientists[scientistIndex];
        if (scientist.isCompleted) return 0f;

        float elapsed = Time.time - scientist.startTime;
        float sweetSpot = trainingBaseSweetSpot + trainingSweetSpotGrowthRate * scientist.data.intelligenceLevel * elapsed;
        float x = (scientist.totalInvested + amount) / sweetSpot;
        float efficiency = x * Mathf.Exp(1f - x);

        return amount * trainingCoefficient * efficiency;
    }

    /// <summary>
    /// OtherPrerequisite flags kontrolü. Her flag için ilgili koşulu doğrular.
    /// </summary>
    private bool CheckOtherPrerequisites(OtherPrerequisite flags)
    {
        if ((flags & OtherPrerequisite.ThreeScientistsTrained) != 0)
        {
            if (GetCompletedScientistCount() < 3)
                return false;
        }

        return true;
    }

    // ==================== EĞİTİM GETTER'LARI ====================

    public bool IsTrainingUnlocked()
    {
        return trainingUnlocked;
    }

    public int GetScientistCount()
    {
        return scientists.Count;
    }

    public int GetCompletedScientistCount()
    {
        int count = 0;
        foreach (ScientistTraining s in scientists)
        {
            if (s.isCompleted) count++;
        }
        return count;
    }

    public ScientistTraining GetScientist(int index)
    {
        if (index < 0 || index >= scientists.Count) return null;
        return scientists[index];
    }

    public List<ScientistTraining> GetAllScientists()
    {
        return new List<ScientistTraining>(scientists);
    }

    public float GetScientistSweetSpot(int scientistIndex)
    {
        if (scientistIndex < 0 || scientistIndex >= scientists.Count) return 0f;
        ScientistTraining scientist = scientists[scientistIndex];
        float elapsed = Time.time - scientist.startTime;
        return trainingBaseSweetSpot + trainingSweetSpotGrowthRate * scientist.data.intelligenceLevel * elapsed;
    }

    /// <summary>
    /// Bilim adamını listeden kalıcı olarak çıkarır (kaçak operasyona gönderildi).
    /// </summary>
    public bool RemoveScientist(int index)
    {
        if (index < 0 || index >= scientists.Count) return false;

        scientists.RemoveAt(index);
        return true;
    }

    // ==================== EVENT BINDING ====================

    private void OnEnable()
    {
        SkillEvents.OnSkillUnlockRequested += HandleUnlockRequest;
    }

    private void OnDisable()
    {
        SkillEvents.OnSkillUnlockRequested -= HandleUnlockRequest;
    }

    private void HandleUnlockRequest(String skillId)
    {
        TryUnlock(skillId);
    }
}

/// <summary>
/// Skill'den gelen direkt gelir kaynağı. Her kaynak kendi decay eğrisini taşır.
/// </summary>
public class DirectIncomeSource
{
    public float incomePerSecond;
    public float startTime;
    public float decayAt1Min;
    public float decayAt2Min;
    public float decayAt3Min;
    public float decayAt4Min;
    public float decayAt5Min;

    public DirectIncomeSource(float incomePerSecond, float startTime,
        float decayAt1Min, float decayAt2Min, float decayAt3Min,
        float decayAt4Min, float decayAt5Min)
    {
        this.incomePerSecond = incomePerSecond;
        this.startTime = startTime;
        this.decayAt1Min = decayAt1Min;
        this.decayAt2Min = decayAt2Min;
        this.decayAt3Min = decayAt3Min;
        this.decayAt4Min = decayAt4Min;
        this.decayAt5Min = decayAt5Min;
    }
}

/// <summary>
/// Sahip olunan yatırım. Değeri zamanla artar, satılınca kâr/zarar hesaplanır.
/// </summary>
public class OwnedInvestment
{
    public InvestmentProduct product;
    public float buyPrice;       //toplam yatırılan miktar
    public float quantity;       //satın alınan adet (küsüratlı olabilir)
    public float currentValue;   //toplam güncel değer
    public float purchaseTime;

    //simülasyon state
    public float targetPercent;        //hedef potansiyel (%, + kâr, - zarar)
    public float currentPercent;       //şu anki yüzde değişim
    public float trendSpeed;           //hesaplanmış trend hızı (reachTime'dan türetilir)
    public bool reachedPotential;      //potansiyeline ulaştı mı
    public float reachedPotentialTime; //potansiyeline ulaşma zamanı

    public OwnedInvestment(InvestmentProduct product, float investAmount, float quantity, float purchaseTime, float profitChance)
    {
        this.product = product;
        this.buyPrice = investAmount;
        this.quantity = quantity;
        this.currentValue = investAmount;
        this.purchaseTime = purchaseTime;
        this.currentPercent = 0f;
        this.reachedPotential = false;

        //reachTime aralığından rastgele süre seç, trendSpeed hesapla
        float reachTime = UnityEngine.Random.Range(product.minReachTime, product.maxReachTime);
        this.trendSpeed = 1f - Mathf.Pow(0.05f, 2f / reachTime); //2f = tick interval

        //kâr mı zarar mı? effectiveProfitChance olasılığına göre belirle
        bool isProfit = UnityEngine.Random.value <= profitChance;

        if (isProfit)
            targetPercent = UnityEngine.Random.Range(0f, product.maxProfitPercent);
        else
            targetPercent = -UnityEngine.Random.Range(0f, product.maxLossPercent);
    }
}

[System.Serializable]
public class OwnedJeweler
{
    public JewelerProduct product;

    public OwnedJeweler(JewelerProduct product)
    {
        this.product = product;
    }
}
