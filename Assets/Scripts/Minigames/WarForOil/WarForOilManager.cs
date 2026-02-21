using System;
using System.Collections.Generic;
using UnityEngine;

public class WarForOilManager : MonoBehaviour
{
    public static WarForOilManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData;
    public WarForOilDatabase database;
    
    [Header("Debug")]
    public bool bypassUnlockCheck = true;

    //mevcut durum
    private WarForOilState currentState = WarForOilState.Idle;
    private WarForOilCountry selectedCountry;

    //baskı fazı
    private float pressureCooldownTimer;

    //savaş fazı
    private float supportStat;
    private float warTimer;
    private float eventCheckTimer;
    private float eventDecisionTimer;
    private WarForOilEvent currentEvent;
    private Dictionary<WarForOilEvent, int> eventTriggerCounts = new Dictionary<WarForOilEvent, int>();

    //operasyon boyunca biriken modifier'lar
    private float accumulatedSuspicionModifier;
    private float accumulatedReputationModifier;
    private float accumulatedPoliticalInfluenceModifier;
    private int accumulatedCostModifier;
    private float rewardMultiplier; //baseRewardReduction'lar sonucu biriken ödül çarpanı (1.0'dan başlar)
    private bool eventsBlocked; //bir choice eventleri engelledi mi
    private bool pendingDeal; //anlaşmayla bitirme aktif mi
    private float dealRewardRatio; //anlaşma ödül oranı

    //zincir sistemi
    private bool isInChain; //şu an bir event zincirinde miyiz
    private WarForOilEvent chainStartEvent; //zincirin baş event'i (config burada)
    private int chainRefusalCount; //zincirdeki toplam reddetme sayısı
    private float chainTimer; //sonraki zincir eventine geri sayım
    private WarForOilEvent pendingChainEvent; //sıradaki zincir eventi (beklemede)

    //rakip işgal sistemi
    private bool isCornerGrabRace; //köşe kapma yarışı aktif mi
    private bool rivalInvasionTriggered; //bu savaşta rakip işgal zaten tetiklendi mi
    private WarForOilCountry rivalCountry; //rakip işgale giren ülke
    private float cornerGrabStat; //köşe kapma stat'ı (0-100, yüksek = bizim lehimize)
    private Dictionary<WarForOilCountry, float> bonusRewards = new Dictionary<WarForOilCountry, float>(); //rakip işgallerden ülkelere eklenen bonus ödül

    //toplum tepkisi sistemi
    private bool protestPending; //foreshadow aşaması (feed değişti, event henüz gösterilmedi)
    private bool protestActive; //toplum tepkisi aktif mi
    private bool protestTriggered; //bu savaşta toplum tepkisi zaten tetiklendi mi (tekrar tetiklenmez)
    private bool protestSuppressed; //başarıyla bastırıldı mı
    private float protestStat; //toplum tepkisi değeri (0-100)
    private float protestDriftRate; //pasif drift hızı (son choice modifier / divisor, her tick'te uygulanır)
    private float protestDriftTimer; //drift tick zamanlayıcı

    //vandalizm sistemi
    private VandalismLevel currentVandalismLevel = VandalismLevel.None;
    private float vandalismDamageTimer;

    //sonuç ekranı beklerken saklanan sonuç
    private WarForOilResult pendingResult;

    //savaş kaybedilirse minigame kalıcı olarak devre dışı kalır
    private bool permanentlyDisabled;

    //kazanılan (işgal edilen) ülkeler — tekrar seçilemez
    private HashSet<WarForOilCountry> conqueredCountries = new HashSet<WarForOilCountry>();

    //ülke rotasyonu — UI'da görünen 3 ülke
    private List<WarForOilCountry> activeCountries = new List<WarForOilCountry>();
    private Dictionary<WarForOilCountry, float> countryArrivalTime = new Dictionary<WarForOilCountry, float>();
    private float rotationTimer;
    private bool rotationInitialized;

    //events — UI bu event'leri dinleyecek
    public static event Action<WarForOilCountry> OnCountrySelected;
    public static event Action<bool, float> OnPressureResult; //başarı, cooldown süresi (başarısızsa)
    public static event Action<float> OnPressureCooldownUpdate; //kalan cooldown süresi
    public static event Action<WarForOilCountry, float> OnWarStarted; //ülke, savaş süresi
    public static event Action<float> OnWarProgress; //ilerleme (0-1)
    public static event Action<WarForOilEvent> OnWarEventTriggered; //event tetiklendi
    public static event Action<float> OnEventDecisionTimerUpdate; //event karar sayacı
    public static event Action<WarForOilEventChoice> OnWarEventResolved; //seçim yapıldı
    public static event Action<WarForOilResult> OnCeasefireResult; //ateşkes sonucu
    public static event Action<WarForOilResult> OnWarResultReady; //sonuç hesaplandı, sonuç ekranını göster
    public static event Action<WarForOilResult> OnWarFinished; //sonuç ekranı kapatıldı, her şey bitti
    public static event Action<List<WarForOilCountry>> OnActiveCountriesChanged; //UI'daki ülke listesi değişti
    public static event Action OnChainStarted; //zincir başladı (UI savaş timer'ı dondurabilir)
    public static event Action<string> OnChainEnded; //zincir bitti (sebep: "collapse", "ceasefire", "government_collapse")
    public static event Action<WarForOilCountry> OnRivalInvasionStarted; //rakip işgal tetiklendi (UI rakip ülkeyi gösterebilir)
    public static event Action OnCornerGrabStarted; //köşe kapma yarışı başladı (anlaşma reddedildi)
    public static event Action<float> OnCornerGrabStatChanged; //köşe kapma stat'ı değişti (0-100)
    public static event Action OnProtestForeshadow; //feed savaş karşıtı gönderilere döndü (foreshadowing)
    public static event Action OnProtestStarted; //toplum tepkisi başladı
    public static event Action<float> OnProtestStatChanged; //toplum tepkisi stat'ı değişti (0-100)
    public static event Action OnProtestSuppressed; //toplum tepkisi başarıyla bastırıldı
    public static event Action<VandalismLevel> OnVandalismLevelChanged; //vandalizm seviyesi değişti
    public static event Action<float> OnVandalismDamage; //vandalizm hasar tick'i (UI animasyon için)

    void Start()
    {
        // Force initialize countries for debugging
        if (bypassUnlockCheck && database != null && database.countries.Count > 0)
        {
            InitializeCountryRotation();
            Debug.Log($"[WarForOilManager] Force initialized {activeCountries.Count} countries");
        }
    }
    
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
        //ülke rotasyonunu her zaman güncelle (state'ten bağımsız)
        UpdateCountryRotation();

        switch (currentState)
        {
            case WarForOilState.PressurePhase:
                UpdatePressurePhase();
                break;
            case WarForOilState.WarProcess:
                UpdateWarProcess();
                break;
            case WarForOilState.EventPhase:
                UpdateEventPhase();
                break;
            case WarForOilState.ChainWaiting:
                UpdateChainWaiting();
                break;
        }
    }

    // ==================== UI'IN ÇAĞIRDIĞI METODLAR ====================

    /// <summary>
    /// UI'dan ülke seçimi yapılır. CountrySelection → PressurePhase geçişi.
    /// </summary>
    public void SelectCountry(WarForOilCountry country)
    {
        if (permanentlyDisabled) return;
        if (currentState != WarForOilState.Idle && currentState != WarForOilState.CountrySelection) return;
        if (country == null) return;

        //minigame açık mı ve cooldown'da mı kontrol et
        if (!bypassUnlockCheck && MinigameManager.Instance != null)
        {
            if (!MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            {
                Debug.Log("[WarForOilManager] BLOCKED: not unlocked");
                return;
            }
            if (MinigameManager.Instance.IsOnCooldown(minigameData))
            {
                Debug.Log("[WarForOilManager] BLOCKED: on cooldown");
                return;
            }
        }

        //bu ülke zaten işgal edilmiş mi
        if (conqueredCountries.Contains(country)) return;

        //bu ülke aktif listede mi (UI'da görünüyor mu)
        if (!activeCountries.Contains(country)) return;

        selectedCountry = country;
        currentState = WarForOilState.PressurePhase;
        pressureCooldownTimer = 0f;

        OnCountrySelected?.Invoke(country);
    }

    /// <summary>
    /// Oyuncu "Baskı Yap" butonuna bastı. Siyasi nüfuza göre başarı kontrolü.
    /// Başarılı → savaş başlar. Başarısız → cooldown başlar.
    /// </summary>
    public void AttemptPressure()
    {
        if (currentState != WarForOilState.PressurePhase) return;
        if (pressureCooldownTimer > 0f) return; //cooldown devam ediyor

        float politicalInfluence = 0f;
        if (GameStatManager.Instance != null)
            politicalInfluence = GameStatManager.Instance.PoliticalInfluence;

        float successChance = Mathf.Clamp(
            politicalInfluence * database.politicalInfluenceMultiplier,
            0f, 0.95f
        );

        bool success = UnityEngine.Random.value < successChance;

        if (success)
        {
            OnPressureResult?.Invoke(true, 0f);
            StartWar();
        }
        else
        {
            pressureCooldownTimer = database.pressureCooldown;
            OnPressureResult?.Invoke(false, database.pressureCooldown);
        }
    }

    /// <summary>
    /// Oyuncu baskı fazından vazgeçip ülke seçimine geri dönmek istiyor.
    /// </summary>
    public void CancelPressure()
    {
        if (currentState != WarForOilState.PressurePhase) return;

        selectedCountry = null;
        currentState = WarForOilState.Idle;
        pressureCooldownTimer = 0f;
    }

    /// <summary>
    /// Oyuncu event seçimi yaptı.
    /// </summary>
    public void ResolveEvent(int choiceIndex)
    {
        if (currentState != WarForOilState.EventPhase || currentEvent == null) return;
        if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Count) return;

        WarForOilEventChoice choice = currentEvent.choices[choiceIndex];

        //ön koşulları sağlanmayan seçenek seçilemez
        if (!choice.IsAvailable()) return;

        //modifier'ları biriktir
        accumulatedSuspicionModifier += choice.suspicionModifier;
        accumulatedReputationModifier += choice.reputationModifier;
        accumulatedPoliticalInfluenceModifier += choice.politicalInfluenceModifier;
        accumulatedCostModifier += choice.costModifier;

        //feed dondurma
        if (choice.freezesFeed && SocialMediaManager.Instance != null)
            SocialMediaManager.Instance.TryFreezeFeed();

        //feed yavaşlatma
        if (choice.slowsFeed && SocialMediaManager.Instance != null)
            SocialMediaManager.Instance.TrySlowFeed();

        //feed yönlendirme (her zaman Militarizm konusuna yönlendirir)
        if (choice.hasFeedOverride && SocialMediaManager.Instance != null)
            SocialMediaManager.Instance.SetEventOverride(TopicType.Militarizm, choice.feedOverrideRatio, choice.feedOverrideDuration);

        //supportStat güncelle
        supportStat = Mathf.Clamp(supportStat + choice.supportModifier, 0f, 100f);

        //köşe kapma stat güncelle (sadece yarış aktifse)
        if (isCornerGrabRace && choice.cornerGrabModifier != 0f)
        {
            cornerGrabStat = Mathf.Clamp(cornerGrabStat + choice.cornerGrabModifier, 0f, 100f);
            OnCornerGrabStatChanged?.Invoke(cornerGrabStat);
        }

        //toplum tepkisi stat güncelle (sadece tepki aktifse)
        if (protestActive && !protestSuppressed)
        {
            float effectiveProtestMod = 0f;

            if (choice.hasProtestChance)
            {
                //olasılık bazlı: zar at, azalma veya artma uygula
                if (UnityEngine.Random.value < choice.protestDecreaseChance)
                    effectiveProtestMod = -choice.protestDecreaseAmount;
                else
                    effectiveProtestMod = choice.protestIncreaseAmount;
            }
            else
            {
                effectiveProtestMod = choice.protestModifier;
            }

            if (effectiveProtestMod != 0f)
            {
                protestStat = Mathf.Clamp(protestStat + effectiveProtestMod, 0f, 100f);
                protestDriftRate = effectiveProtestMod / database.protestDriftDivisor;
                protestDriftTimer = 0f; //drift timer'ı sıfırla (yeni choice'tan itibaren say)
                OnProtestStatChanged?.Invoke(protestStat);

                //anında eşik kontrolleri
                if (protestStat >= database.protestFailThreshold)
                {
                    if (GameManager.Instance != null)
                        GameManager.Instance.ResumeGame();
                    ProtestForceCeasefire();
                    return;
                }
                if (protestStat < database.protestSuccessThreshold)
                {
                    SuppressProtest();
                }
            }
        }

        //vandalizm seviyesi güncelle
        ApplyVandalismChange(choice);

        WarForOilEvent resolvedEvent = currentEvent;
        OnWarEventResolved?.Invoke(choice);

        currentEvent = null;

        //zincir event'i mi kontrol et — zincir seçenekleri normal akışı atlar
        if (isInChain || resolvedEvent.chainRole == ChainRole.Head)
        {
            //ilk zincir event'i ise zinciri başlat
            if (!isInChain)
                StartChain(resolvedEvent);

            //oyunu devam ettir (event paneli kapandı, oyun çalışsın — sadece savaş timer'ı durur)
            if (GameManager.Instance != null)
                GameManager.Instance.ResumeGame();

            //ateşkes seçeneği — zinciri ateşkesle bitir
            if (choice.triggersCeasefire)
            {
                ChainCeasefire();
                return;
            }

            //reddetme seçeneği — sayacı artır, threshold kontrol et
            if (choice.isChainRefusal)
            {
                chainRefusalCount++;
                int maxRefusals = GetMaxRefusalsForCurrentSupport();

                if (chainRefusalCount >= maxRefusals)
                {
                    //çok fazla reddedildi — zincir çöker
                    CollapseChain(false);
                    return;
                }
            }

            //zinciri devam ettir (continuesChain veya isChainRefusal ama henüz çökmedi)
            if (choice.continuesChain || choice.isChainRefusal)
            {
                if (resolvedEvent.nextChainEvent != null)
                {
                    //sonraki event var — kuyrukla
                    QueueNextChainEvent(resolvedEvent.nextChainEvent, resolvedEvent.chainInterval);
                    return;
                }
                else
                {
                    //son adım — hükümet düşüşü (ceza yok, sadece skill lock)
                    CollapseChain(true);
                    return;
                }
            }

            return;
        }

        //rakip işgal teklifi — kabul veya red
        if (choice.acceptsRivalDeal)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ResumeGame();

            AcceptRivalDeal();
            return;
        }
        if (choice.rejectsRivalDeal)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ResumeGame();

            RejectRivalDeal();
            return;
        }

        //normal event akışı (zincir dışı)

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //ödül düşürme
        if (choice.reducesReward && choice.baseRewardReduction > 0f)
            rewardMultiplier *= (1f - choice.baseRewardReduction);

        //event engelleme (blocksEvents, endsWar veya anlaşma seçildiyse artık event gelmez)
        if (choice.blocksEvents || choice.endsWar || choice.endsWarWithDeal)
            eventsBlocked = true;

        //savaşı bitirme seçeneği seçildiyse kalan süreyi ayarla
        if (choice.endsWar)
        {
            float targetTimer = database.warDuration - choice.warEndDelay;
            warTimer = Mathf.Max(warTimer, targetTimer); //süreyi geri almaz, sadece ileri sarar
        }

        //anlaşmayla bitirme — süreyi ilerlet ve garanti ödül işaretle
        if (choice.endsWarWithDeal)
        {
            float targetTimer = database.warDuration - choice.dealDelay;
            warTimer = Mathf.Max(warTimer, targetTimer);
            pendingDeal = true;
            dealRewardRatio = choice.dealRewardRatio;
        }

        //savaş sürecine geri dön
        currentState = WarForOilState.WarProcess;
    }

    /// <summary>
    /// Oyuncu ateşkes talep etti. SupportStat'a göre kazanç/kayıp hesaplanır.
    /// Ülke fethedilmez, minigame kapanmaz.
    /// </summary>
    public void RequestCeasefire()
    {
        if (currentState != WarForOilState.WarProcess) return;
        if (supportStat < database.ceasefireMinSupport) return;

        //ateşkes oranı: 0 (en kötü) → 1 (en iyi)
        float ratio = (supportStat - database.ceasefireMinSupport)
            / (100f - database.ceasefireMinSupport);

        float effectiveCeasefireReward = GetEffectiveBaseReward(selectedCountry);

        //kazanç hesapla: düşük support → zarar, yüksek support → kâr
        float wealthChange = Mathf.Lerp(
            -database.ceasefirePenalty,
            effectiveCeasefireReward * rewardMultiplier * database.ceasefireMaxReward,
            ratio
        ) - accumulatedCostModifier;

        pendingResult = new WarForOilResult();
        pendingResult.country = selectedCountry;
        pendingResult.warWon = false;
        pendingResult.wasCeasefire = true;
        pendingResult.finalSupportStat = supportStat;
        pendingResult.finalVandalismLevel = currentVandalismLevel;
        pendingResult.winChance = 0f;
        pendingResult.wealthChange = wealthChange;
        pendingResult.suspicionChange = accumulatedSuspicionModifier;
        pendingResult.reputationChange = accumulatedReputationModifier;
        pendingResult.politicalInfluenceChange = accumulatedPoliticalInfluenceModifier;

        currentState = WarForOilState.ResultPhase;

        //oyunu duraklat
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnCeasefireResult?.Invoke(pendingResult);
    }

    /// <summary>
    /// Sonuç ekranını kapatır. UI bu metodu çağırır.
    /// Stat'lar uygulanır, oyun devam eder, cooldown başlar.
    /// </summary>
    public void DismissResultScreen()
    {
        if (pendingResult == null) return;

        WarForOilResult result = pendingResult;
        pendingResult = null;

        //stat'lara uygula
        if (GameStatManager.Instance != null)
        {
            if (result.wealthChange != 0)
                GameStatManager.Instance.AddWealth(result.wealthChange);
            if (result.suspicionChange != 0)
                GameStatManager.Instance.AddSuspicion(result.suspicionChange);
            if (result.reputationChange != 0)
                GameStatManager.Instance.AddReputation(result.reputationChange);
            if (result.politicalInfluenceChange != 0)
                GameStatManager.Instance.AddPoliticalInfluence(result.politicalInfluenceChange);
        }

        //savaş kaybedildiyse minigame kalıcı olarak devre dışı (ateşkes hariç)
        if (!result.warWon && !result.wasCeasefire)
            permanentlyDisabled = true;

        //cooldown başlat (kazanıldıysa)
        if (!permanentlyDisabled && MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //durumu sıfırla
        ResetState();

        OnWarFinished?.Invoke(result);
    }

    // ==================== ÜLKE ROTASYONU ====================

    /// <summary>
    /// Ülke rotasyonunu başlatır — database'den rastgele ülkeler seçer.
    /// </summary>
    private void InitializeCountryRotation()
    {
        activeCountries.Clear();
        countryArrivalTime.Clear();

        List<WarForOilCountry> pool = GetAvailableCountryPool();

        //havuzdan rastgele seç
        int count = Mathf.Min(database.visibleCountryCount, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            WarForOilCountry country = pool[idx];
            activeCountries.Add(country);
            countryArrivalTime[country] = Time.time;
            pool.RemoveAt(idx);
        }

        rotationTimer = 0f;
        rotationInitialized = true;

        OnActiveCountriesChanged?.Invoke(activeCountries);
    }

    /// <summary>
    /// Rotasyon timer'ı günceller. Her interval'de bir ülke değiştirilir.
    /// </summary>
    private void UpdateCountryRotation()
    {
        //minigame açık değilse rotasyon çalışmasın
        if (permanentlyDisabled) return;
        if (MinigameManager.Instance == null || !MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            return;

        //ilk çalıştırmada ülkeleri seç
        if (!rotationInitialized)
        {
            InitializeCountryRotation();
            return;
        }

        rotationTimer += Time.deltaTime;
        if (rotationTimer < database.rotationInterval) return;
        rotationTimer = 0f;

        //swap için uygun ülkeleri bul: en az 1 döngü orada olmuş + şu an savaşta/baskıda olmayan
        List<int> swappableIndices = new List<int>();
        for (int i = 0; i < activeCountries.Count; i++)
        {
            WarForOilCountry country = activeCountries[i];

            //bu ülke şu an seçilmiş ve işlem devam ediyorsa swap'a dahil etme
            if (country == selectedCountry && currentState != WarForOilState.Idle) continue;

            //en az bir rotasyon süresi orada olmuş mu
            if (Time.time - countryArrivalTime[country] < database.rotationInterval) continue;

            swappableIndices.Add(i);
        }

        if (swappableIndices.Count == 0) return;

        //yerine gelecek ülke havuzu — aktif listede olmayan ve conquered olmayan
        List<WarForOilCountry> replacementPool = GetAvailableCountryPool();
        for (int i = 0; i < activeCountries.Count; i++)
            replacementPool.Remove(activeCountries[i]);

        if (replacementPool.Count == 0) return;

        //rastgele birini swap et
        int swapIdx = swappableIndices[UnityEngine.Random.Range(0, swappableIndices.Count)];
        WarForOilCountry oldCountry = activeCountries[swapIdx];
        WarForOilCountry newCountry = replacementPool[UnityEngine.Random.Range(0, replacementPool.Count)];

        activeCountries[swapIdx] = newCountry;
        countryArrivalTime.Remove(oldCountry);
        countryArrivalTime[newCountry] = Time.time;

        OnActiveCountriesChanged?.Invoke(activeCountries);
    }

    /// <summary>
    /// Database'den uygun ülke havuzunu döner (conquered olanlar hariç).
    /// </summary>
    private List<WarForOilCountry> GetAvailableCountryPool()
    {
        List<WarForOilCountry> pool = new List<WarForOilCountry>();
        if (database.countries == null) return pool;

        for (int i = 0; i < database.countries.Count; i++)
        {
            if (!conqueredCountries.Contains(database.countries[i]))
                pool.Add(database.countries[i]);
        }
        return pool;
    }

    // ==================== STATE GÜNCELLEMELERI ====================

    /// <summary>
    /// PressurePhase: cooldown geri sayımı.
    /// </summary>
    private void UpdatePressurePhase()
    {
        if (pressureCooldownTimer > 0f)
        {
            pressureCooldownTimer -= Time.deltaTime;
            if (pressureCooldownTimer < 0f) pressureCooldownTimer = 0f;
            OnPressureCooldownUpdate?.Invoke(pressureCooldownTimer);
        }
    }

    /// <summary>
    /// WarProcess: savaş timer'ı ilerler, event'ler kontrol edilir.
    /// </summary>
    private void UpdateWarProcess()
    {
        warTimer += Time.deltaTime;

        //UI'a ilerleme bildir
        float progress = Mathf.Clamp01(warTimer / database.warDuration);
        OnWarProgress?.Invoke(progress);

        //toplum tepkisi drift güncelleme
        if (protestActive && !protestSuppressed)
        {
            protestDriftTimer += Time.deltaTime;
            if (protestDriftTimer >= database.protestDriftInterval)
            {
                protestDriftTimer = 0f;
                protestStat = Mathf.Clamp(protestStat + protestDriftRate, 0f, 100f);
                OnProtestStatChanged?.Invoke(protestStat);

                //eşik kontrolleri
                if (protestStat >= database.protestFailThreshold)
                {
                    //toplum tepkisi çok yükseldi — otomatik ateşkes
                    ProtestForceCeasefire();
                    return;
                }
                if (protestStat < database.protestSuccessThreshold)
                {
                    //toplum tepkisi bastırıldı
                    SuppressProtest();
                }
            }
        }

        //vandalizm hasar tick'i
        if (currentVandalismLevel != VandalismLevel.None && currentVandalismLevel != VandalismLevel.Ended)
        {
            vandalismDamageTimer += Time.deltaTime;
            if (vandalismDamageTimer >= database.vandalismDamageInterval)
            {
                vandalismDamageTimer = 0f;
                float damage = GetVandalismDamage(currentVandalismLevel);
                if (damage > 0f && GameStatManager.Instance != null)
                {
                    GameStatManager.Instance.AddWealth(-damage);
                    OnVandalismDamage?.Invoke(damage);
                }
            }
        }

        //event kontrol
        eventCheckTimer += Time.deltaTime;
        if (eventCheckTimer >= database.eventInterval)
        {
            eventCheckTimer = 0f;
            TryTriggerWarEvent();

            //event tetiklendiyse bu frame'de savaş sonucu hesaplama
            if (currentState != WarForOilState.WarProcess) return;
        }

        //savaş bitti mi
        if (warTimer >= database.warDuration)
        {
            CalculateWarResult();
        }
    }

    /// <summary>
    /// EventPhase: event karar sayacı (oyun duraklatılmış, unscaledDeltaTime).
    /// </summary>
    private void UpdateEventPhase()
    {
        eventDecisionTimer -= Time.unscaledDeltaTime;
        OnEventDecisionTimerUpdate?.Invoke(eventDecisionTimer);

        //süre doldu — default seçeneği otomatik seç (available olmalı)
        if (eventDecisionTimer <= 0f)
        {
            int defaultIdx = -1;

            //önce belirlenmiş default'u dene
            if (currentEvent.defaultChoiceIndex >= 0 &&
                currentEvent.defaultChoiceIndex < currentEvent.choices.Count &&
                currentEvent.choices[currentEvent.defaultChoiceIndex].IsAvailable())
            {
                defaultIdx = currentEvent.defaultChoiceIndex;
            }

            //default available değilse, available olan ilk choice'u bul
            if (defaultIdx < 0)
            {
                for (int i = 0; i < currentEvent.choices.Count; i++)
                {
                    if (currentEvent.choices[i].IsAvailable())
                    {
                        defaultIdx = i;
                        break;
                    }
                }
            }

            //hiç available choice yoksa event'i etkisiz kapat
            if (defaultIdx < 0)
            {
                currentEvent = null;
                if (GameManager.Instance != null)
                    GameManager.Instance.ResumeGame();
                currentState = WarForOilState.WarProcess;
                return;
            }

            ResolveEvent(defaultIdx);
        }
    }

    // ==================== ZİNCİR SİSTEMİ ====================

    /// <summary>
    /// ChainWaiting: sonraki zincir eventine geri sayım (oyun çalışıyor, sadece savaş timer'ı durmuş).
    /// </summary>
    private void UpdateChainWaiting()
    {
        chainTimer -= Time.deltaTime;
        if (chainTimer <= 0f && pendingChainEvent != null)
        {
            TriggerChainEvent(pendingChainEvent);
        }
    }

    /// <summary>
    /// Zincir eventini tetikler — EventPhase'e geçirir.
    /// </summary>
    private void TriggerChainEvent(WarForOilEvent chainEvent)
    {
        currentEvent = chainEvent;
        pendingChainEvent = null;

        currentState = WarForOilState.EventPhase;
        eventDecisionTimer = chainEvent.decisionTime;

        //event paneli gösterilirken oyunu duraklat
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnWarEventTriggered?.Invoke(chainEvent);
    }

    /// <summary>
    /// Zinciri başlatır. İlk event tetiklendikten sonra çağrılır.
    /// </summary>
    private void StartChain(WarForOilEvent headEvent)
    {
        isInChain = true;
        chainStartEvent = headEvent;
        chainRefusalCount = 0;

        //zincir boyunca diğer sistemlerin event göstermesini engelle
        EventCoordinator.LockEvents("WarForOilChain");

        OnChainStarted?.Invoke();
    }

    /// <summary>
    /// Zincirde sonraki eventi kuyruklar (chainInterval sonra tetiklenir).
    /// </summary>
    private void QueueNextChainEvent(WarForOilEvent nextEvent, float interval)
    {
        pendingChainEvent = nextEvent;
        chainTimer = interval;
        currentState = WarForOilState.ChainWaiting;
    }

    /// <summary>
    /// Support stat'a göre izin verilen max reddetme sayısını döner.
    /// </summary>
    private int GetMaxRefusalsForCurrentSupport()
    {
        if (chainStartEvent == null || chainStartEvent.refusalThresholds == null)
            return 1; //fallback

        for (int i = 0; i < chainStartEvent.refusalThresholds.Count; i++)
        {
            RefusalThreshold t = chainStartEvent.refusalThresholds[i];
            if (supportStat >= t.minSupport && supportStat < t.maxSupport)
                return t.maxRefusals;
        }

        return 1; //hiçbir aralığa düşmezse fallback
    }

    /// <summary>
    /// Zincir çöküşü: skill'leri kilitle + ceza kes + savaşı bitir.
    /// isGovernmentCollapse true ise ceza kesilmez (son adım = hükümet düşüşü).
    /// </summary>
    private void CollapseChain(bool isGovernmentCollapse)
    {
        //skill'leri kilitle
        if (chainStartEvent != null && chainStartEvent.skillsToLock != null && SkillTreeManager.Instance != null)
        {
            for (int i = 0; i < chainStartEvent.skillsToLock.Count; i++)
            {
                Skill skill = chainStartEvent.skillsToLock[i];
                if (skill != null)
                    SkillTreeManager.Instance.RelockSkill(skill.id);
            }
        }

        //ceza — hükümet düşüşünde ceza yok
        if (!isGovernmentCollapse && chainStartEvent != null && chainStartEvent.chainFine > 0f)
            accumulatedCostModifier += (int)chainStartEvent.chainFine;

        string reason = isGovernmentCollapse ? "government_collapse" : "collapse";
        EndChain(reason);
    }

    /// <summary>
    /// Zincirden ateşkes — ceasefireMinSupport kontrolü yapılmaz.
    /// </summary>
    private void ChainCeasefire()
    {
        EndChain("ceasefire");
    }

    /// <summary>
    /// Zinciri sonlandırır ve savaşı bitirir.
    /// </summary>
    private void EndChain(string reason)
    {
        isInChain = false;
        chainStartEvent = null;
        pendingChainEvent = null;

        //event kilidini bırak
        EventCoordinator.UnlockEvents("WarForOilChain");

        OnChainEnded?.Invoke(reason);

        if (reason == "ceasefire")
        {
            //ateşkes — normal ateşkes formülü ama minSupport kontrolü yok
            float ratio = supportStat / 100f; //0 support = en kötü, 100 = en iyi
            float effectiveChainReward = GetEffectiveBaseReward(selectedCountry);

            float wealthChange = Mathf.Lerp(
                -database.ceasefirePenalty,
                effectiveChainReward * rewardMultiplier * database.ceasefireMaxReward,
                ratio
            ) - accumulatedCostModifier;

            pendingResult = new WarForOilResult();
            pendingResult.country = selectedCountry;
            pendingResult.warWon = false;
            pendingResult.wasCeasefire = true;
            pendingResult.finalSupportStat = supportStat;
            pendingResult.finalVandalismLevel = currentVandalismLevel;
            pendingResult.winChance = 0f;
            pendingResult.wealthChange = wealthChange;
            pendingResult.suspicionChange = accumulatedSuspicionModifier;
            pendingResult.politicalInfluenceChange = accumulatedPoliticalInfluenceModifier;

            currentState = WarForOilState.ResultPhase;

            //zaten pause durumda (zincir sırasında), sonuç ekranını göster
            OnCeasefireResult?.Invoke(pendingResult);
        }
        else
        {
            //collapse veya government_collapse — savaş kaybedildi
            pendingResult = new WarForOilResult();
            pendingResult.country = selectedCountry;
            pendingResult.warWon = false;
            pendingResult.wasCeasefire = false;
            pendingResult.wasChainCollapse = true;
            pendingResult.finalSupportStat = supportStat;
            pendingResult.finalVandalismLevel = currentVandalismLevel;
            pendingResult.winChance = 0f;
            pendingResult.wealthChange = -(database.warLossPenalty + accumulatedCostModifier);
            pendingResult.suspicionChange = database.warLossSuspicionIncrease + accumulatedSuspicionModifier;
            pendingResult.reputationChange = accumulatedReputationModifier;
            pendingResult.politicalInfluenceChange = -database.warLossPoliticalPenalty + accumulatedPoliticalInfluenceModifier;

            currentState = WarForOilState.ResultPhase;

            OnWarResultReady?.Invoke(pendingResult);
        }
    }

    // ==================== RAKİP İŞGAL SİSTEMİ ====================

    /// <summary>
    /// Rakip işgal anlaşması kabul edildi — savaş hızla biter, ödül bölüşülür.
    /// </summary>
    private void AcceptRivalDeal()
    {
        //savaşı hızla bitir
        float targetTimer = database.warDuration - database.rivalDealEndDelay;
        warTimer = Mathf.Max(warTimer, targetTimer);

        //anlaşma ödül oranını işaretle — CalculateWarResult'ta kullanılacak
        pendingDeal = true;
        dealRewardRatio = database.rivalDealRewardRatio;
        eventsBlocked = true; //artık event gelmez

        //rakip ülkenin payını bonusRewards'a ekle
        float totalReward = GetEffectiveBaseReward(selectedCountry);
        float rivalShare = totalReward * (1f - database.rivalDealRewardRatio);
        AddBonusReward(rivalCountry, rivalShare);

        //savaş sürecine geri dön
        currentState = WarForOilState.WarProcess;
    }

    /// <summary>
    /// Rakip işgal anlaşması reddedildi — köşe kapma yarışı başlar.
    /// Bundan sonra eventler sadece cornerGrabEvents havuzundan gelir.
    /// </summary>
    private void RejectRivalDeal()
    {
        isCornerGrabRace = true;
        cornerGrabStat = database.initialCornerGrabStat;
        eventTriggerCounts.Clear(); //yeni havuz için sayaçları sıfırla

        OnCornerGrabStarted?.Invoke();
        OnCornerGrabStatChanged?.Invoke(cornerGrabStat);

        //savaş sürecine geri dön
        currentState = WarForOilState.WarProcess;
    }

    /// <summary>
    /// Bir ülkenin efektif base reward'ını döner (baseReward + bonus).
    /// </summary>
    private float GetEffectiveBaseReward(WarForOilCountry country)
    {
        float reward = country.baseReward;
        if (bonusRewards.TryGetValue(country, out float bonus))
            reward += bonus;
        return reward;
    }

    /// <summary>
    /// Bir ülkeye bonus ödül ekler (rakip işgalden kazandığı pay).
    /// </summary>
    private void AddBonusReward(WarForOilCountry country, float amount)
    {
        if (country == null || amount <= 0f) return;
        if (bonusRewards.ContainsKey(country))
            bonusRewards[country] += amount;
        else
            bonusRewards[country] = amount;
    }

    /// <summary>
    /// Bir ülkenin bonus reward'ını döner (UI için).
    /// </summary>
    public float GetBonusReward(WarForOilCountry country)
    {
        if (country == null) return 0f;
        bonusRewards.TryGetValue(country, out float bonus);
        return bonus;
    }

    /// <summary>
    /// Köşe kapma stat'ını döner (0-100).
    /// </summary>
    public float GetCornerGrabStat()
    {
        return cornerGrabStat;
    }

    /// <summary>
    /// Köşe kapma yarışı aktif mi.
    /// </summary>
    public bool IsCornerGrabRace()
    {
        return isCornerGrabRace;
    }

    /// <summary>
    /// Rakip ülkeyi döner (aktif savaşta rakip işgal varsa).
    /// </summary>
    public WarForOilCountry GetRivalCountry()
    {
        return rivalCountry;
    }

    // ==================== TOPLUM TEPKİSİ SİSTEMİ ====================

    /// <summary>
    /// Toplum tepkisini aktif eder ve başlangıç event'ini gösterir (faz 2).
    /// </summary>
    private void ActivateProtest()
    {
        protestActive = true;
        protestStat = database.initialProtestStat;
        protestDriftRate = 0f; //ilk choice'a kadar drift yok
        protestDriftTimer = 0f;

        OnProtestStarted?.Invoke();
        OnProtestStatChanged?.Invoke(protestStat);

        //başlangıç event'ini göster
        if (!EventCoordinator.CanShowEvent()) return;

        EventCoordinator.MarkEventShown();

        currentEvent = database.protestTriggerEvent;
        currentState = WarForOilState.EventPhase;
        eventDecisionTimer = currentEvent.decisionTime;

        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnWarEventTriggered?.Invoke(currentEvent);
    }

    /// <summary>
    /// Toplum tepkisi eşiği aşıldı — savaş otomatik ateşkese bağlanır.
    /// </summary>
    private void ProtestForceCeasefire()
    {
        protestActive = false;

        //mevcut ateşkes formülü ile sonuç hesapla
        float ratio = (supportStat - database.ceasefireMinSupport)
            / (100f - database.ceasefireMinSupport);
        ratio = Mathf.Clamp01(ratio); //support < minSupport olabilir, negatife düşmesin

        float effectiveReward = GetEffectiveBaseReward(selectedCountry);

        float wealthChange = Mathf.Lerp(
            -database.ceasefirePenalty,
            effectiveReward * rewardMultiplier * database.ceasefireMaxReward,
            ratio
        ) - accumulatedCostModifier;

        pendingResult = new WarForOilResult();
        pendingResult.country = selectedCountry;
        pendingResult.warWon = false;
        pendingResult.wasCeasefire = true;
        pendingResult.wasProtestCeasefire = true;
        pendingResult.finalSupportStat = supportStat;
        pendingResult.finalProtestStat = protestStat;
        pendingResult.finalVandalismLevel = currentVandalismLevel;
        pendingResult.winChance = 0f;
        pendingResult.wealthChange = wealthChange;
        pendingResult.suspicionChange = accumulatedSuspicionModifier;
        pendingResult.reputationChange = accumulatedReputationModifier;
        pendingResult.politicalInfluenceChange = accumulatedPoliticalInfluenceModifier;

        currentState = WarForOilState.ResultPhase;

        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnCeasefireResult?.Invoke(pendingResult);
    }

    /// <summary>
    /// Toplum tepkisi başarıyla bastırıldı (stat eşiğin altına düştü).
    /// Protest eventleri durur, normal savaş devam eder.
    /// </summary>
    private void SuppressProtest()
    {
        protestSuppressed = true;
        protestActive = false;
        protestDriftRate = 0f;

        //protest bastırıldıysa vandalizm de sona erer
        if (currentVandalismLevel != VandalismLevel.None && currentVandalismLevel != VandalismLevel.Ended)
        {
            currentVandalismLevel = VandalismLevel.Ended;
            OnVandalismLevelChanged?.Invoke(currentVandalismLevel);
        }

        OnProtestSuppressed?.Invoke();
    }

    /// <summary>
    /// Toplum tepkisi aktif mi.
    /// </summary>
    public bool IsProtestActive()
    {
        return protestActive;
    }

    /// <summary>
    /// Toplum tepkisi stat'ını döner (0-100).
    /// </summary>
    public float GetProtestStat()
    {
        return protestStat;
    }

    // ==================== VANDALİZM SİSTEMİ ====================

    /// <summary>
    /// Choice'un vandalizm etkisini uygular.
    /// </summary>
    private void ApplyVandalismChange(WarForOilEventChoice choice)
    {
        if (!choice.affectsVandalism) return;

        VandalismLevel newLevel;

        if (choice.vandalismChangeType == VandalismChangeType.Direct)
        {
            newLevel = choice.vandalismTargetLevel;
        }
        else
        {
            //göreceli değişim: mevcut seviyeyi sayısal olarak kaydır
            int currentNumeric = VandalismLevelToInt(currentVandalismLevel);
            int result = currentNumeric + choice.vandalismLevelDelta;

            if (result < 1)
                newLevel = VandalismLevel.Ended; //alt sınırın altı → bitti
            else if (result > 4)
                newLevel = VandalismLevel.Severe; //üst sınır → severe'da kal
            else
                newLevel = IntToVandalismLevel(result);
        }

        if (newLevel != currentVandalismLevel)
        {
            currentVandalismLevel = newLevel;
            vandalismDamageTimer = 0f; //seviye değişince timer sıfırla
            OnVandalismLevelChanged?.Invoke(currentVandalismLevel);
        }
    }

    /// <summary>
    /// Vandalizm seviyesine göre tick başına hasar miktarını döner.
    /// </summary>
    private float GetVandalismDamage(VandalismLevel level)
    {
        switch (level)
        {
            case VandalismLevel.Light: return database.vandalismLightDamage;
            case VandalismLevel.Moderate: return database.vandalismModerateDamage;
            case VandalismLevel.Heavy: return database.vandalismHeavyDamage;
            case VandalismLevel.Severe: return database.vandalismSevereDamage;
            default: return 0f;
        }
    }

    /// <summary>
    /// VandalismLevel → sayısal değer (Light=1, Moderate=2, Heavy=3, Severe=4). None/Ended → 0.
    /// </summary>
    private int VandalismLevelToInt(VandalismLevel level)
    {
        switch (level)
        {
            case VandalismLevel.Light: return 1;
            case VandalismLevel.Moderate: return 2;
            case VandalismLevel.Heavy: return 3;
            case VandalismLevel.Severe: return 4;
            default: return 0;
        }
    }

    /// <summary>
    /// Sayısal değer → VandalismLevel (1=Light, 2=Moderate, 3=Heavy, 4=Severe).
    /// </summary>
    private VandalismLevel IntToVandalismLevel(int value)
    {
        switch (value)
        {
            case 1: return VandalismLevel.Light;
            case 2: return VandalismLevel.Moderate;
            case 3: return VandalismLevel.Heavy;
            case 4: return VandalismLevel.Severe;
            default: return VandalismLevel.None;
        }
    }

    /// <summary>
    /// Mevcut vandalizm seviyesini döner.
    /// </summary>
    public VandalismLevel GetVandalismLevel()
    {
        return currentVandalismLevel;
    }

    // ==================== İÇ MANTIK ====================

    /// <summary>
    /// Savaşı başlatır. PressurePhase → WarProcess geçişi.
    /// </summary>
    private void StartWar()
    {
        currentState = WarForOilState.WarProcess;

        supportStat = database.initialSupportStat;
        warTimer = 0f;
        eventCheckTimer = 0f;
        accumulatedSuspicionModifier = 0f;
        accumulatedReputationModifier = 0f;
        accumulatedPoliticalInfluenceModifier = 0f;
        accumulatedCostModifier = 0;
        rewardMultiplier = 1f;
        eventsBlocked = false;
        pendingDeal = false;
        dealRewardRatio = 0f;
        eventTriggerCounts.Clear();
        currentEvent = null;
        isInChain = false;
        chainStartEvent = null;
        chainRefusalCount = 0;
        chainTimer = 0f;
        pendingChainEvent = null;
        isCornerGrabRace = false;
        rivalInvasionTriggered = false;
        rivalCountry = null;
        protestPending = false;
        protestActive = false;
        protestTriggered = false;
        protestSuppressed = false;
        protestStat = 0f;
        protestDriftRate = 0f;
        protestDriftTimer = 0f;
        currentVandalismLevel = VandalismLevel.None;
        vandalismDamageTimer = 0f;

        OnWarStarted?.Invoke(selectedCountry, database.warDuration);
    }

    /// <summary>
    /// Savaş sırasında event tetiklemeyi dener.
    /// Köşe kapma yarışı aktifse cornerGrabEvents'ten, değilse database.events'ten çeker.
    /// Toplum tepkisi aktifse ek olarak protestEvents havuzundan da event gelebilir.
    /// </summary>
    private void TryTriggerWarEvent()
    {
        if (eventsBlocked) return;

        //toplum tepkisi faz 2: bekleyen protest event'ini göster
        if (protestPending)
        {
            protestPending = false;
            ActivateProtest();
            return;
        }

        //rakip işgal tetikleme kontrolü (henüz tetiklenmemişse ve koşullar uygunsa)
        if (!rivalInvasionTriggered && !isCornerGrabRace && !isInChain
            && warTimer >= database.rivalInvasionMinWarTime
            && database.rivalOfferEvent != null)
        {
            if (UnityEngine.Random.value < database.rivalInvasionChance)
            {
                TryTriggerRivalInvasion();
                if (currentState != WarForOilState.WarProcess) return; //tetiklendiyse çık
            }
        }

        //toplum tepkisi faz 1: koşullar uygunsa foreshadow tetikle (bu cycle'da event gösterilmez)
        if (!protestTriggered && !protestActive && !isInChain
            && warTimer >= database.protestMinWarTime
            && database.protestTriggerEvent != null)
        {
            if (UnityEngine.Random.value < database.protestChance)
            {
                protestPending = true;
                protestTriggered = true; //bir daha tetiklenmez
                OnProtestForeshadow?.Invoke();
                return; //bu cycle'ı tüket, event gösterilmez
            }
        }

        //aktif event havuzunu belirle
        List<WarForOilEvent> eventPool = isCornerGrabRace ? database.cornerGrabEvents : database.events;

        //tetiklenebilir eventleri filtrele (tekrar limiti + minimum süre)
        List<WarForOilEvent> available = new List<WarForOilEvent>();
        if (eventPool != null)
        {
            for (int i = 0; i < eventPool.Count; i++)
            {
                WarForOilEvent evt = eventPool[i];
                if (warTimer < evt.minWarTime) continue;

                eventTriggerCounts.TryGetValue(evt, out int count);
                if (count == 0)
                    available.Add(evt);
                else if (evt.isRepeatable && count <= evt.maxRepeatCount)
                    available.Add(evt);
            }
        }

        //toplum tepkisi aktifse protest havuzundan da eventler ekle (çift havuz)
        if (protestActive && !protestSuppressed && database.protestEvents != null)
        {
            for (int i = 0; i < database.protestEvents.Count; i++)
            {
                WarForOilEvent evt = database.protestEvents[i];
                if (warTimer < evt.minWarTime) continue;

                eventTriggerCounts.TryGetValue(evt, out int count);
                if (count == 0)
                    available.Add(evt);
                else if (evt.isRepeatable && count <= evt.maxRepeatCount)
                    available.Add(evt);
            }
        }

        if (available.Count == 0) return;

        //EventCoordinator cooldown kontrolü
        if (!EventCoordinator.CanShowEvent()) return;

        //rastgele bir event seç ve sayacını artır
        int idx = UnityEngine.Random.Range(0, available.Count);
        currentEvent = available[idx];
        eventTriggerCounts.TryGetValue(currentEvent, out int currentCount);
        eventTriggerCounts[currentEvent] = currentCount + 1;

        EventCoordinator.MarkEventShown();

        //event fazına geç
        currentState = WarForOilState.EventPhase;
        eventDecisionTimer = currentEvent.decisionTime;

        //oyunu duraklat
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnWarEventTriggered?.Invoke(currentEvent);
    }

    /// <summary>
    /// Rakip işgal tetiklemeyi dener — uygun rakip ülke varsa teklif event'ini fırlatır.
    /// </summary>
    private void TryTriggerRivalInvasion()
    {
        //rakip ülke seç — hedef ülke ve conquered ülkeler hariç
        List<WarForOilCountry> rivalPool = new List<WarForOilCountry>();
        if (database.countries != null)
        {
            for (int i = 0; i < database.countries.Count; i++)
            {
                WarForOilCountry c = database.countries[i];
                if (c != selectedCountry && !conqueredCountries.Contains(c))
                    rivalPool.Add(c);
            }
        }

        if (rivalPool.Count == 0) return; //rakip ülke yok

        rivalCountry = rivalPool[UnityEngine.Random.Range(0, rivalPool.Count)];
        rivalInvasionTriggered = true;

        //EventCoordinator cooldown kontrolü
        if (!EventCoordinator.CanShowEvent()) return;

        EventCoordinator.MarkEventShown();

        //teklif event'ini göster
        currentEvent = database.rivalOfferEvent;
        currentState = WarForOilState.EventPhase;
        eventDecisionTimer = currentEvent.decisionTime;

        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnRivalInvasionStarted?.Invoke(rivalCountry);
        OnWarEventTriggered?.Invoke(currentEvent);
    }

    /// <summary>
    /// Savaş sonu: kazanma olasılığı hesapla, random check yap.
    /// Anlaşma varsa zar atılmaz, garanti ödül verilir.
    /// </summary>
    private void CalculateWarResult()
    {
        float effectiveBaseReward = GetEffectiveBaseReward(selectedCountry);

        //anlaşmayla bitirme aktifse — zar yok, garanti ödül
        if (pendingDeal)
        {
            float dealReward = effectiveBaseReward * rewardMultiplier * dealRewardRatio;

            pendingResult = new WarForOilResult();
            pendingResult.country = selectedCountry;
            pendingResult.warWon = true;
            pendingResult.wasDeal = true;
            pendingResult.finalSupportStat = supportStat;
            pendingResult.finalVandalismLevel = currentVandalismLevel;
            pendingResult.winChance = 1f;
            pendingResult.wealthChange = dealReward - accumulatedCostModifier;
            pendingResult.suspicionChange = accumulatedSuspicionModifier;
            pendingResult.politicalInfluenceChange = accumulatedPoliticalInfluenceModifier;
        }
        else
        {
            //destek oranı (0-1)
            float supportRatio = supportStat / 100f;

            //kazanma şansı hesapla
            float winChance = database.baseWinChance
                - selectedCountry.invasionDifficulty
                + supportRatio * database.supportWinBonus;
            winChance = Mathf.Clamp(winChance, database.minWinChance, database.maxWinChance);

            bool warWon = UnityEngine.Random.value < winChance;

            //sonucu hazırla
            pendingResult = new WarForOilResult();
            pendingResult.country = selectedCountry;
            pendingResult.warWon = warWon;
            pendingResult.finalSupportStat = supportStat;
            pendingResult.finalVandalismLevel = currentVandalismLevel;
            pendingResult.winChance = winChance;

            if (warWon)
            {
                //köşe kapma yarışı aktifse bölüşüm cornerGrabStat'a göre
                if (isCornerGrabRace && rivalCountry != null)
                {
                    float grabRatio = cornerGrabStat / 100f; //bizim payımız (0-1)
                    float rivalShare = effectiveBaseReward * (1f - grabRatio);
                    AddBonusReward(rivalCountry, rivalShare);
                }

                //kazanıldı — ödül destek oranına göre (supportRewardRatio ile sınırlı)
                //köşe kapma yarışındaysa ödül cornerGrabStat'a göre bölünür
                float rewardRatio = isCornerGrabRace ? (cornerGrabStat / 100f) : supportRatio;
                float reward = effectiveBaseReward * rewardMultiplier * rewardRatio * database.supportRewardRatio;
                pendingResult.wealthChange = reward - accumulatedCostModifier;
                pendingResult.suspicionChange = accumulatedSuspicionModifier;
                pendingResult.politicalInfluenceChange = accumulatedPoliticalInfluenceModifier;
            }
            else
            {
                //kaybedildi — ceza
                pendingResult.wealthChange = -(database.warLossPenalty + accumulatedCostModifier);
                pendingResult.suspicionChange = database.warLossSuspicionIncrease + accumulatedSuspicionModifier;
                pendingResult.politicalInfluenceChange = -database.warLossPoliticalPenalty + accumulatedPoliticalInfluenceModifier;
            }
        }

        //rakip işgal bilgilerini sonuca ekle
        if (rivalCountry != null)
        {
            pendingResult.rivalCountry = rivalCountry;
            pendingResult.wasCornerGrabRace = isCornerGrabRace;

            //rakip ülkenin bu savaştan kazandığı toplam bonus
            if (isCornerGrabRace && pendingResult.warWon)
            {
                float grabRatioForResult = cornerGrabStat / 100f;
                pendingResult.rivalRewardGain = effectiveBaseReward * (1f - grabRatioForResult);
            }
            else if (pendingDeal)
            {
                pendingResult.rivalRewardGain = effectiveBaseReward * (1f - database.rivalDealRewardRatio);
            }
        }

        currentState = WarForOilState.ResultPhase;

        //kazanıldıysa ülkeyi işgal edilmiş olarak işaretle (anlaşma hariç — anlaşma işgal sayılmaz)
        if (pendingResult.warWon && !pendingResult.wasDeal)
            conqueredCountries.Add(selectedCountry);

        //oyunu duraklat — sonuç ekranında zaman durmalı
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnWarResultReady?.Invoke(pendingResult);
    }

    /// <summary>
    /// Tüm state değişkenlerini sıfırlar.
    /// </summary>
    private void ResetState()
    {
        currentEvent = null;
        selectedCountry = null;
        currentState = WarForOilState.Idle;
        pressureCooldownTimer = 0f;
    }

    // ==================== GETTER'LAR ====================

    public bool IsActive()
    {
        return currentState != WarForOilState.Idle;
    }

    public bool IsPermanentlyDisabled()
    {
        return permanentlyDisabled;
    }

    public bool IsCountryConquered(WarForOilCountry country)
    {
        return conqueredCountries.Contains(country);
    }

    public bool CanRequestCeasefire()
    {
        return currentState == WarForOilState.WarProcess
            && supportStat >= database.ceasefireMinSupport;
    }

    public bool IsInChain()
    {
        return isInChain;
    }

    public WarForOilState GetCurrentState()
    {
        return currentState;
    }

    public WarForOilCountry GetSelectedCountry()
    {
        return selectedCountry;
    }

    public float GetSupportStat()
    {
        return supportStat;
    }

    public List<WarForOilCountry> GetActiveCountries()
    {
        return activeCountries;
    }

    public float GetWarProgress()
    {
        if (currentState != WarForOilState.WarProcess && currentState != WarForOilState.EventPhase
            && currentState != WarForOilState.ChainWaiting)
            return 0f;
        return Mathf.Clamp01(warTimer / database.warDuration);
    }
}

/// <summary>
/// WarForOil minigame durumları
/// </summary>
public enum WarForOilState
{
    Idle,               //minigame aktif değil
    CountrySelection,   //ülke seçimi yapılıyor
    PressurePhase,      //yönetime baskı yapılıyor
    WarProcess,         //savaş devam ediyor
    EventPhase,         //event geldi, karar bekleniyor
    ChainWaiting,       //zincir eventi bekleniyor (chainInterval geri sayımı)
    ResultPhase         //sonuç ekranı gösteriliyor
}

/// <summary>
/// Savaş sonucu
/// </summary>
[System.Serializable]
public class WarForOilResult
{
    public WarForOilCountry country;
    public bool warWon;
    public bool wasCeasefire;
    public bool wasDeal; //anlaşmayla mı bitti
    public bool wasChainCollapse; //zincir çöküşüyle mi bitti
    public bool wasCornerGrabRace; //köşe kapma yarışı mıydı
    public bool wasProtestCeasefire; //toplum tepkisi yüzünden ateşkes mi
    public WarForOilCountry rivalCountry; //rakip ülke (varsa)
    public float rivalRewardGain; //rakip ülkenin kazandığı bonus reward
    public float finalSupportStat;
    public float finalProtestStat; //toplum tepkisi son değeri (aktifse)
    public VandalismLevel finalVandalismLevel; //savaş sonu vandalizm seviyesi
    public float winChance; //hesaplanan kazanma şansı
    public float wealthChange;
    public float suspicionChange;
    public float reputationChange;
    public float politicalInfluenceChange;
}
