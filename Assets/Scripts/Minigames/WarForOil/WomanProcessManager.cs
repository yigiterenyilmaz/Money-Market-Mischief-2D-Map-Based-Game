using System;
using System.Collections.Generic;
using UnityEngine;

public class WomanProcessManager : MonoBehaviour
{
    public static WomanProcessManager Instance { get; private set; }

    [Header("Referanslar")]
    public WomanProcessDatabase database;

    //süreç durumu
    private WomanProcessState currentState = WomanProcessState.Inactive;
    private float womanObsession;
    private bool wasTriggeredThisGame; //oyun boyunca tek sefer başlatılabilir
    public bool WasTriggeredThisGame => wasTriggeredThisGame;
    private bool isInWar; //şu an savaş aktif mi
    private int eventCounter; //son kadın eventinden beri sayılan event sayısı
    private WarForOilEvent currentWomanEvent;
    private float eventDecisionTimer;
    private bool pendingTrigger; //bir sonraki uygun anda kadın eventi tetiklenecek
    private Dictionary<WarForOilEvent, int> womanEventTriggerCounts = new Dictionary<WarForOilEvent, int>();
    private HashSet<WarForOilEvent> dismissedWomanEvents = new HashSet<WarForOilEvent>(); //yasaklanan eventler

    //öncü event durumu
    private WarForOilEvent pendingWomanEventAfterPrecursor; //öncü event sonrası tetiklenecek kadın eventi
    private float precursorDelayTimer; //öncü event çözüldükten sonra kalan bekleme süresi
    private bool precursorWasWarEvent; //öncü event war for oil mi (savaş kontrolü için)
    private const float PRECURSOR_DELAY = 4f; //öncü event ile kadın eventi arası bekleme süresi (saniye)

    //öncü event karar sayacı
    private WarForOilEvent currentPrecursorWarEvent; //gösterilen öncü war for oil event
    private Event currentPrecursorRandomEvent; //gösterilen öncü random event
    private float precursorDecisionTimer;

    //havuz yönlendirme — bir choice ile kalıcı olarak başka database'e geçiş
    private WomanProcessDatabase redirectedDatabase; //null ise normal database kullanılır

    //kalıcı obsesyon kazanım çarpanı — çarpımsal birikir (1.0 = etkisiz)
    private float obsessionGainMultiplier = 1f;

    //son chain branch seçiminin anında event olup olmadığı
    private bool lastChainPickWasImmediate;
    private float lastChainPickImmediateDelay;

    //gecikmeli anında event (chain branch'ten)
    private WarForOilEvent pendingImmediateEvent;
    private float immediateEventTimer;

    //dondurma — belirli sayıda döngü boyunca kadın eventi tetiklenmez
    private int freezeRemainingCycles;

    //döngü başına birden fazla kadın eventi desteği
    private int remainingWomanEventsInCycle;

    //obsesyon düşüş limiti — aktifse obsesyon zirve değerinden belirli miktar düşünce süreç biter
    private bool hasActiveDropLimit;
    private float dropLimitDelta; //zirve obsesyondan bu kadar düşerse süreç biter
    private float dropLimitPeakObsession; //limit aktifken ulaşılan en yüksek obsesyon

    //zincir durumu
    private bool isInWomanChain;
    private List<ChainBranch> pendingWomanChainBranches; //koşulsuz dallar
    private List<ChainBranch> pendingWomanConditionalBranches; //koşullu dallar
    private ChainInfluenceStat pendingWomanChainInfluenceStat;
    private float pendingWomanChainThreshold0;
    private float pendingWomanChainThreshold1;
    private float pendingWomanChainThreshold2;
    private bool pendingWomanChainCanEnd;
    private float pendingWomanChainEndWeight;
    private bool pendingWomanConditionalBranching;
    private string pendingWomanBranchCounterKey;
    private int pendingWomanBranchCounterMin;
    private int pendingWomanBranchCounterMax = -1;
    private Dictionary<string, int> womanChainCounters = new Dictionary<string, int>();

    //events — UI dinleyecek
    public static event Action OnWomanProcessStarted; //süreç başladı
    public static event Action<float> OnObsessionChanged; //obsesyon değeri değişti (yeni değer)
    public static event Action<WarForOilEvent> OnWomanEventTriggered; //kadın eventi tetiklendi
    public static event Action<float> OnWomanEventDecisionTimerUpdate; //karar sayacı güncellendi
    public static event Action<WarForOilEventChoice> OnWomanEventResolved; //seçim yapıldı
    public static event Action OnWomanProcessEnded; //süreç bitti (obsesyon düştü)
    public static event Action OnWomanProcessGameOver; //game over (obsesyon 100)
    public static event Action<WarForOilEvent> OnPrecursorWarEventTriggered; //öncü war for oil event tetiklendi
    public static event Action<Event> OnPrecursorRandomEventTriggered; //öncü random event tetiklendi
    public static event Action<float> OnPrecursorDecisionTimerUpdate; //öncü event karar sayacı güncellendi
    public static event Action OnPrecursorEventResolved; //öncü event çözüldü, bekleme başlıyor

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        WarForOilManager.OnWarStarted += HandleWarStarted;
        WarForOilManager.OnWarFinished += HandleWarFinished;
        WarForOilManager.OnWarEventResolved += HandleWarEventResolved;
        RandomEventManager.OnEventTriggered += HandleRandomEventTriggered;
    }

    private void OnDisable()
    {
        WarForOilManager.OnWarStarted -= HandleWarStarted;
        WarForOilManager.OnWarFinished -= HandleWarFinished;
        WarForOilManager.OnWarEventResolved -= HandleWarEventResolved;
        RandomEventManager.OnEventTriggered -= HandleRandomEventTriggered;
    }

    private void Update()
    {
        if (currentState == WomanProcessState.Inactive) return;

        //gecikmeli anında event (chain branch'ten)
        if (pendingImmediateEvent != null && currentState == WomanProcessState.Active)
        {
            immediateEventTimer -= Time.deltaTime;
            if (immediateEventTimer <= 0f)
            {
                WarForOilEvent evt = pendingImmediateEvent;
                pendingImmediateEvent = null;
                ShowWomanEvent(evt);
                return;
            }
        }

        //bekleyen tetikleme — uygun an gelince kadın eventini göster
        if (pendingTrigger && currentState == WomanProcessState.Active)
        {
            if (CanTriggerNow())
            {
                pendingTrigger = false;
                TriggerWomanEvent();
            }
        }

        //öncü event karar sayacı
        if (currentState == WomanProcessState.PrecursorPhase)
        {
            if (precursorDecisionTimer < 0f) goto skipPrecursor; //süresiz — oyuncu seçene kadar bekle

            precursorDecisionTimer -= Time.unscaledDeltaTime;
            OnPrecursorDecisionTimerUpdate?.Invoke(precursorDecisionTimer);

            if (precursorDecisionTimer <= 0f)
            {
                //süre doldu — öncü event için varsayılan seçenek
                if (currentPrecursorWarEvent != null)
                {
                    int defaultIdx = currentPrecursorWarEvent.defaultChoiceIndex;
                    if (defaultIdx >= 0 && defaultIdx < currentPrecursorWarEvent.choices.Count
                        && currentPrecursorWarEvent.choices[defaultIdx].IsAvailable())
                    {
                        ResolvePrecursorWarEvent(defaultIdx);
                    }
                    else
                    {
                        for (int i = 0; i < currentPrecursorWarEvent.choices.Count; i++)
                        {
                            if (currentPrecursorWarEvent.choices[i].IsAvailable())
                            {
                                ResolvePrecursorWarEvent(i);
                                break;
                            }
                        }
                    }
                }
                else if (currentPrecursorRandomEvent != null)
                {
                    //random event — varsayılan ilk seçenek
                    if (currentPrecursorRandomEvent.choices != null && currentPrecursorRandomEvent.choices.Count > 0)
                        ResolvePrecursorRandomEvent(0);
                }
            }
            skipPrecursor:;
        }

        //öncü event sonrası bekleme
        if (currentState == WomanProcessState.PrecursorDelay)
        {
            precursorDelayTimer -= Time.deltaTime;
            if (precursorDelayTimer <= 0f)
            {
                //savaş kontrolü — öncü war for oil eventiyse ve artık savaşta değilsek iptal
                if (precursorWasWarEvent && !isInWar)
                {
                    pendingWomanEventAfterPrecursor = null;
                    currentState = WomanProcessState.Active;
                }
                else
                {
                    //asıl kadın eventini tetikle
                    ShowWomanEvent(pendingWomanEventAfterPrecursor);
                    pendingWomanEventAfterPrecursor = null;
                }
            }
        }

        //event karar sayacı
        if (currentState == WomanProcessState.EventPhase)
        {
            //süresiz event (decisionTime <= 0) — sayaç işlemez, oyuncu seçene kadar bekler
            if (eventDecisionTimer < 0f) return;

            eventDecisionTimer -= Time.unscaledDeltaTime;
            OnWomanEventDecisionTimerUpdate?.Invoke(eventDecisionTimer);

            if (eventDecisionTimer <= 0f)
            {
                //süre doldu — önce varsayılan, yoksa ilk available seçenek
                int fallbackIdx = -1;

                //varsayılan seçenek available mı
                int defaultIdx = currentWomanEvent.defaultChoiceIndex;
                if (defaultIdx >= 0 && defaultIdx < currentWomanEvent.choices.Count
                    && currentWomanEvent.choices[defaultIdx].IsAvailable())
                {
                    fallbackIdx = defaultIdx;
                }
                else
                {
                    //ilk available seçeneği bul
                    for (int i = 0; i < currentWomanEvent.choices.Count; i++)
                    {
                        if (currentWomanEvent.choices[i].IsAvailable())
                        {
                            fallbackIdx = i;
                            break;
                        }
                    }
                }

                if (fallbackIdx >= 0)
                {
                    ResolveEvent(fallbackIdx);
                }
                else
                {
                    //hiçbir seçenek available değil — eventi sonuçsuz kapat
                    currentWomanEvent = null;
                    currentState = WomanProcessState.Active;
                    if (GameManager.Instance != null)
                        GameManager.Instance.ResumeGame();
                }
            }
        }
    }

    // ==================== DIŞ ÇAĞRILAR ====================

    /// <summary>
    /// Kadın sürecini başlatır. Sadece savaş sırasında çağrılmalı.
    /// Oyun boyunca tek sefer çalışır.
    /// </summary>
    public void StartProcess()
    {
        if (wasTriggeredThisGame) return;
        if (database == null) return;

        wasTriggeredThisGame = true;
        womanObsession = database.initialObsession;
        eventCounter = 0;
        pendingTrigger = false;
        currentWomanEvent = null;
        womanEventTriggerCounts.Clear();
        dismissedWomanEvents.Clear();
        redirectedDatabase = null;
        obsessionGainMultiplier = 1f;
        isInWomanChain = false;
        pendingWomanChainBranches = null;
        pendingWomanEventAfterPrecursor = null;
        pendingImmediateEvent = null;
        immediateEventTimer = 0f;
        freezeRemainingCycles = 0;
        remainingWomanEventsInCycle = 0;
        hasActiveDropLimit = false;
        dropLimitDelta = 0f;
        dropLimitPeakObsession = 0f;
        currentPrecursorWarEvent = null;
        currentPrecursorRandomEvent = null;
        currentState = WomanProcessState.Active;

        OnWomanProcessStarted?.Invoke();
        OnObsessionChanged?.Invoke(womanObsession);
    }

    /// <summary>
    /// Oyuncu kadın eventinde seçim yaptı.
    /// </summary>
    public void ResolveEvent(int choiceIndex)
    {
        if (currentState != WomanProcessState.EventPhase || currentWomanEvent == null) return;
        if (choiceIndex < 0 || choiceIndex >= currentWomanEvent.choices.Count) return;

        WarForOilEventChoice choice = currentWomanEvent.choices[choiceIndex];

        //ön koşulları sağlanmayan seçenek seçilemez
        if (!choice.IsAvailable()) return;

        //womanObsession güncelle
        if (choice.womanObsessionModifier != 0f)
        {
            float obsessionAmount = choice.womanObsessionModifier;
            if (obsessionAmount > 0f)
                obsessionAmount *= obsessionGainMultiplier;

            if (choice.hasObsessionFloor && obsessionAmount < 0f)
            {
                if (womanObsession < choice.obsessionFloor)
                {
                    //zaten floor'un altında — sadece 0.1 düşür
                    womanObsession = Mathf.Max(womanObsession - 0.1f, 0f);
                }
                else
                {
                    womanObsession = Mathf.Clamp(womanObsession + obsessionAmount, 0f, 100f);
                    if (womanObsession < choice.obsessionFloor)
                        womanObsession = choice.obsessionFloor;
                }
            }
            else
            {
                womanObsession = Mathf.Clamp(womanObsession + obsessionAmount, 0f, 100f);
            }
            OnObsessionChanged?.Invoke(womanObsession);
        }

        //genel stat etkileri — savaş içi/dışı her zaman uygulanır
        if (GameStatManager.Instance != null)
        {
            if (choice.suspicionModifier != 0f)
                GameStatManager.Instance.AddSuspicion(choice.suspicionModifier);
            if (choice.reputationModifier != 0f)
            {
                if (choice.hasReputationFloor && choice.reputationModifier < 0f)
                {
                    if (GameStatManager.Instance.Reputation < choice.reputationFloor)
                        GameStatManager.Instance.AddReputation(-0.1f);
                    else
                    {
                        GameStatManager.Instance.AddReputation(choice.reputationModifier);
                        if (GameStatManager.Instance.Reputation < choice.reputationFloor)
                            GameStatManager.Instance.SetStat(StatType.Reputation, choice.reputationFloor);
                    }
                }
                else
                {
                    GameStatManager.Instance.AddReputation(choice.reputationModifier);
                }
            }
            if (choice.politicalInfluenceModifier != 0f)
                GameStatManager.Instance.AddPoliticalInfluence(choice.politicalInfluenceModifier);
            if (choice.wealthModifier != 0f)
                GameStatManager.Instance.AddWealth(choice.wealthModifier);
        }

        //feed etkileri
        if (SocialMediaManager.Instance != null)
        {
            if (choice.freezesFeed)
                SocialMediaManager.Instance.TryFreezeFeed();
            if (choice.slowsFeed)
                SocialMediaManager.Instance.TrySlowFeed();
            if (choice.hasFeedOverride)
            {
                if (choice.hasCounterFeedTopic)
                    SocialMediaManager.Instance.SetEventOverride(choice.feedOverrideTopic, choice.feedOverrideRatio, choice.counterFeedTopic, choice.counterFeedRatio, choice.feedOverrideDuration);
                else
                    SocialMediaManager.Instance.SetEventOverride(choice.feedOverrideTopic, choice.feedOverrideRatio, choice.feedOverrideDuration);
            }
        }

        //savaş-spesifik etkiler — sadece savaş aktifken uygulanır
        if (isInWar && WarForOilManager.Instance != null)
            WarForOilManager.Instance.ApplyExternalWarEffects(choice);

        //dinamik stat tavanı uygula
        if (choice.statCeilingEffects != null && choice.statCeilingEffects.Count > 0 && GameStatManager.Instance != null)
        {
            for (int i = 0; i < choice.statCeilingEffects.Count; i++)
            {
                var entry = choice.statCeilingEffects[i];
                switch (entry.mode)
                {
                    case StatCeilingMode.Set:
                        GameStatManager.Instance.SetStatCeiling(entry.stat, entry.ceilingValue);
                        break;
                    case StatCeilingMode.Multiply:
                        float currentMax = GameStatManager.Instance.GetEffectiveMax(entry.stat);
                        GameStatManager.Instance.SetStatCeiling(entry.stat, currentMax * entry.ceilingMultiplier);
                        break;
                    case StatCeilingMode.Remove:
                        GameStatManager.Instance.RemoveStatCeiling(entry.stat);
                        break;
                }
            }
        }

        //kalıcı stat çarpanları
        if (choice.permanentMultipliers != null && choice.permanentMultipliers.Count > 0)
        {
            for (int i = 0; i < choice.permanentMultipliers.Count; i++)
            {
                var entry = choice.permanentMultipliers[i];
                if (entry.stat == PermanentMultiplierStatType.WarSupport)
                {
                    if (WarForOilManager.Instance != null)
                        WarForOilManager.Instance.ApplyPermanentSupportMultiplier(entry.multiplier);
                }
                else if (entry.stat == PermanentMultiplierStatType.WomanObsession)
                {
                    ApplyPermanentObsessionMultiplier(entry.multiplier);
                }
                else if (GameStatManager.Instance != null)
                    GameStatManager.Instance.ApplyPermanentGainMultiplier((StatType)entry.stat, entry.multiplier);
            }
        }

        //havuz yönlendirme — kalıcı olarak başka database'e geçiş
        if (choice.redirectsWomanPool && choice.womanPoolDatabase != null)
        {
            redirectedDatabase = choice.womanPoolDatabase;
        }

        //kadın sürecini dondurma
        if (choice.freezesWomanProcess && choice.womanProcessFreezeCycles > 0)
        {
            FreezeProcess(choice.womanProcessFreezeCycles);
        }

        //obsesyon düşüş limiti — zirve obsesyondan belirli miktar düşerse süreç biter
        if (choice.hasObsessionDropLimit && choice.obsessionDropLimit > 0f)
        {
            //daha sıkı (küçük delta) olan geçerli
            if (!hasActiveDropLimit || choice.obsessionDropLimit < dropLimitDelta)
                dropLimitDelta = choice.obsessionDropLimit;
            hasActiveDropLimit = true;
            dropLimitPeakObsession = Mathf.Max(dropLimitPeakObsession, womanObsession);
        }

        OnWomanEventResolved?.Invoke(choice);

        currentWomanEvent = null;

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //direkt bitirme flag'i
        if (choice.endsWomanProcess)
        {
            currentWomanEvent = null;
            EndWomanChain();
            currentState = WomanProcessState.Inactive;
            OnWomanProcessEnded?.Invoke();
            return;
        }

        //zincir sayaç artırma
        if (isInWomanChain && choice.incrementsChainCounter && !string.IsNullOrEmpty(choice.chainCounterKey))
        {
            if (!womanChainCounters.ContainsKey(choice.chainCounterKey))
                womanChainCounters[choice.chainCounterKey] = 0;
            womanChainCounters[choice.chainCounterKey] += choice.chainCounterIncrement;
        }

        //zincir erken tetikleme — sayaç eşiğe ulaştıysa zinciri atlayıp direkt event'e geç
        if (isInWomanChain && choice.hasEarlyChainTrigger && choice.earlyTriggerEvent != null
            && !string.IsNullOrEmpty(choice.chainCounterKey))
        {
            int currentCount = 0;
            womanChainCounters.TryGetValue(choice.chainCounterKey, out currentCount);
            if (currentCount >= choice.earlyTriggerThreshold)
            {
                WarForOilEvent earlyEvent = choice.earlyTriggerEvent;
                EndWomanChain();
                ShowWomanEvent(earlyEvent);
                return;
            }
        }

        //anında event tetikleme — choice'a bağlı havuzdan veya tier'a göre event gösterilir
        bool immediateEventTriggered = false;
        if (choice.hasImmediateEvent)
        {
            WarForOilEvent picked = null;

            //tier bazlı — kadın obsesyon seviyesine göre tek event
            if (choice.immediateEventIsTiered)
            {
                WomanProcessDatabase activeDb = redirectedDatabase != null ? redirectedDatabase : database;
                int tier = activeDb.GetTier(womanObsession);
                switch (tier)
                {
                    case 1: picked = choice.immediateEventTier1; break;
                    case 2: picked = choice.immediateEventTier2; break;
                    case 3: picked = choice.immediateEventTier3; break;
                }
            }
            else
            {
                //normal havuzdan seç
                picked = (choice.immediateEventPool != null && choice.immediateEventPool.Count > 0)
                    ? PickImmediateEvent(choice.immediateEventPool)
                    : null;
            }

            if (picked != null)
            {
                immediateEventTriggered = true;
                if (choice.immediateEventDelay > 0f)
                {
                    pendingImmediateEvent = picked;
                    immediateEventTimer = choice.immediateEventDelay;
                }
                else
                {
                    currentState = WomanProcessState.Active;
                    CheckEndConditions();
                    ShowWomanEvent(picked);
                    return;
                }
            }
        }

        //anında event yoksa veya gecikmeli tetikleniyorsa — zincir dallanması kontrolü
        if (!immediateEventTriggered)
        {
            bool hasAnyBranches = (choice.chainBranches != null && choice.chainBranches.Count > 0)
                || (choice.conditionalChainBranches != null && choice.conditionalChainBranches.Count > 0);
            if (hasAnyBranches)
            {
                //zincir başlat veya devam ettir
                isInWomanChain = true;
                pendingWomanChainBranches = choice.chainBranches != null ? new List<ChainBranch>(choice.chainBranches) : new List<ChainBranch>();
                pendingWomanConditionalBranches = choice.conditionalChainBranches != null ? new List<ChainBranch>(choice.conditionalChainBranches) : new List<ChainBranch>();
                pendingWomanChainInfluenceStat = choice.chainInfluenceStat;
                pendingWomanChainThreshold0 = choice.chainThreshold0;
                pendingWomanChainThreshold1 = choice.chainThreshold1;
                pendingWomanChainThreshold2 = choice.chainThreshold2;
                pendingWomanChainCanEnd = choice.chainCanEnd;
                pendingWomanChainEndWeight = choice.chainCanEnd ? choice.chainEndWeight : 0f;
                pendingWomanConditionalBranching = choice.hasConditionalBranching;
                pendingWomanBranchCounterKey = choice.branchCounterKey;
                pendingWomanBranchCounterMin = choice.branchCounterMin;
                pendingWomanBranchCounterMax = choice.branchCounterMax;
            }
            else if (isInWomanChain)
            {
                //choice'ta branch yok — zincir biter
                EndWomanChain();
            }
        }

        currentState = WomanProcessState.Active;

        //bitiş kontrolleri
        CheckEndConditions();

        //döngüde bekleyen kadın eventi varsa bir sonrakini tetikle
        if (currentState == WomanProcessState.Active && remainingWomanEventsInCycle > 0)
        {
            pendingTrigger = true;
        }
    }

    /// <summary>
    /// Süreç aktif mi.
    /// </summary>
    public bool IsActive()
    {
        return currentState != WomanProcessState.Inactive;
    }

    /// <summary>
    /// EventPhase'de mi (kadın eventi gösteriliyorken).
    /// </summary>
    public bool IsInEventPhase()
    {
        return currentState == WomanProcessState.EventPhase;
    }

    /// <summary>
    /// Mevcut obsesyon değeri.
    /// </summary>
    public float GetObsession()
    {
        return womanObsession;
    }

    /// <summary>
    /// Aktif database'i döner (yönlendirilmiş varsa onu, yoksa ana database'i).
    /// </summary>
    public WomanProcessDatabase GetActiveDatabase()
    {
        return redirectedDatabase != null ? redirectedDatabase : database;
    }

    /// <summary>
    /// Kadın obsesyonu kalıcı kazanım çarpanını uygular. Çarpan birikimlidir (mevcut *= yeni).
    /// </summary>
    public void ApplyPermanentObsessionMultiplier(float multiplier)
    {
        obsessionGainMultiplier *= multiplier;
    }

    /// <summary>
    /// Kadın sürecini belirli döngü sayısı kadar dondurur. Mevcut freeze varsa üstüne eklenir.
    /// </summary>
    public void FreezeProcess(int cycles)
    {
        freezeRemainingCycles += cycles;
    }

    /// <summary>
    /// Mevcut kademe (1-3).
    /// </summary>
    public int GetCurrentTier()
    {
        if (database == null) return 1;
        return database.GetTier(womanObsession);
    }

    // ==================== EVENT DİNLEYİCİLER ====================

    private void HandleWarStarted(WarForOilCountry country, float duration)
    {
        isInWar = true;
    }

    private void HandleWarFinished(WarForOilResult result)
    {
        isInWar = false;
    }

    private void HandleWarEventResolved(WarForOilEventChoice choice)
    {
        if (currentState != WomanProcessState.Active) return;
        if (!isInWar) return;

        IncrementEventCounter();
    }

    private void HandleRandomEventTriggered(Event evt)
    {
        if (currentState != WomanProcessState.Active) return;
        if (isInWar) return; //savaş sırasında random event'leri sayma

        IncrementEventCounter();
    }

    // ==================== İÇ MANTIK ====================

    private void IncrementEventCounter()
    {
        //döngüde bekleyen kadın eventi varsa normal event sayacını artırma
        if (remainingWomanEventsInCycle > 0) return;

        eventCounter++;

        WomanProcessDatabase activeDb = redirectedDatabase != null ? redirectedDatabase : database;
        int tier = activeDb.GetTier(womanObsession);
        int frequency = activeDb.GetTierFrequency(tier);

        if (eventCounter >= frequency)
        {
            eventCounter = 0;

            //dondurma aktifse bu döngüde kadın eventi tetiklenmez
            if (freezeRemainingCycles > 0)
            {
                freezeRemainingCycles--;
                return;
            }

            int womanCount = activeDb.GetTierWomanCount(tier);
            remainingWomanEventsInCycle = womanCount;
            pendingTrigger = true;
        }
    }

    /// <summary>
    /// Şu an kadın eventi tetiklenebilir mi (başka event gösterilmiyor).
    /// </summary>
    private bool IsStoryFlagsSatisfied(WarForOilEvent evt)
    {
        if (evt.requiredStoryFlags == null || evt.requiredStoryFlags.Count == 0) return true;
        if (StoryFlagManager.Instance == null) return false;
        for (int i = 0; i < evt.requiredStoryFlags.Count; i++)
        {
            if (!StoryFlagManager.Instance.HasFlag(evt.requiredStoryFlags[i])) return false;
        }
        return true;
    }

    private bool CanTriggerNow()
    {
        //EventCoordinator ile çakışma kontrolü
        if (!EventCoordinator.CanShowEvent()) return false;

        //savaş sırasında WarForOilManager EventPhase'deyse bekleme
        if (isInWar && WarForOilManager.Instance != null)
        {
            //WarForOilManager'ın state'ini kontrol edemiyoruz doğrudan
            //ama currentEvent null değilse event gösteriliyor demektir
            //bunun yerine EventCoordinator zaten 2sn aralık sağlıyor
        }

        return true;
    }

    private void TriggerWomanEvent()
    {
        //freeze aktifse zincir dahil hiçbir kadın eventi tetiklenmez — zincir askıda kalır
        if (freezeRemainingCycles > 0)
        {
            freezeRemainingCycles--;
            remainingWomanEventsInCycle = 0;
            pendingTrigger = false;
            return;
        }

        WarForOilEvent evt;

        if (isInWomanChain)
        {
            //zincir aktif — chain branch'lerinden seç
            lastChainPickWasImmediate = false;
            evt = PickEventFromChainBranches();
            if (evt == null)
            {
                //uygun branch kalmadı — zincir biter, havuzdan devam et
                EndWomanChain();
                evt = PickEventFromTierPool();
            }
            else if (lastChainPickWasImmediate)
            {
                //anında event olarak seçildi — zincir devam ediyor, sadece hemen gösterilecek
                if (lastChainPickImmediateDelay > 0f)
                {
                    //gecikmeli tetikle
                    pendingImmediateEvent = evt;
                    immediateEventTimer = lastChainPickImmediateDelay;
                    return;
                }
            }
        }
        else
        {
            //normal — tier havuzundan seç
            evt = PickEventFromTierPool();
        }

        if (evt == null)
        {
            remainingWomanEventsInCycle = 0;
            return;
        }

        //döngü sayacını düşür
        if (remainingWomanEventsInCycle > 0)
            remainingWomanEventsInCycle--;

        //öncü event kontrolü
        if (evt.hasPrecursorEvent)
        {
            //war for oil öncüsü ve savaşta değilsek — bu eventi atla, havuzdan başka bir event seç
            if (evt.precursorEventType == PrecursorEventType.WarForOil && !isInWar)
            {
                WarForOilEvent fallback = PickEventFromTierPool(evt);
                if (fallback == null) return;
                evt = fallback;
            }

            pendingWomanEventAfterPrecursor = evt;
            precursorWasWarEvent = evt.precursorEventType == PrecursorEventType.WarForOil;
            ShowPrecursorEvent(evt);
            return;
        }

        //öncü event yok — doğrudan kadın eventini göster
        ShowWomanEvent(evt);
    }

    /// <summary>
    /// Öncü eventi gösterir ve PrecursorPhase'e geçer.
    /// </summary>
    private void ShowPrecursorEvent(WarForOilEvent womanEvent)
    {
        EventCoordinator.MarkEventShown();
        currentState = WomanProcessState.PrecursorPhase;

        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        if (womanEvent.precursorEventType == PrecursorEventType.WarForOil)
        {
            currentPrecursorWarEvent = womanEvent.precursorWarEvent;
            currentPrecursorRandomEvent = null;
            precursorDecisionTimer = currentPrecursorWarEvent.decisionTime > 0f
                ? currentPrecursorWarEvent.decisionTime : -1f;
            OnPrecursorWarEventTriggered?.Invoke(currentPrecursorWarEvent);
        }
        else
        {
            currentPrecursorRandomEvent = womanEvent.precursorRandomEvent;
            currentPrecursorWarEvent = null;
            precursorDecisionTimer = -1f; //random eventlerde decisionTime yok, süresiz
            OnPrecursorRandomEventTriggered?.Invoke(currentPrecursorRandomEvent);
        }
    }

    /// <summary>
    /// Kadın eventini doğrudan gösterir (öncü event yoksa veya öncü event sonrası).
    /// </summary>
    private void ShowWomanEvent(WarForOilEvent evt)
    {
        currentWomanEvent = evt;
        womanEventTriggerCounts.TryGetValue(evt, out int count);
        womanEventTriggerCounts[evt] = count + 1;

        //tetiklenen eventin yasakladığı eventleri dismissed'e ekle
        if (evt.blockedWomanProcessEvents != null)
        {
            for (int i = 0; i < evt.blockedWomanProcessEvents.Count; i++)
            {
                if (evt.blockedWomanProcessEvents[i] != null)
                    dismissedWomanEvents.Add(evt.blockedWomanProcessEvents[i]);
            }
        }

        EventCoordinator.MarkEventShown();

        currentState = WomanProcessState.EventPhase;
        eventDecisionTimer = evt.decisionTime > 0f ? evt.decisionTime : -1f;

        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnWomanEventTriggered?.Invoke(evt);
    }

    private WarForOilEvent PickEventFromTierPool(WarForOilEvent exclude = null)
    {
        //yönlendirilmiş database varsa onu kullan
        WomanProcessDatabase activeDb = redirectedDatabase != null ? redirectedDatabase : database;

        int tier = activeDb.GetTier(womanObsession);
        List<WarForOilEvent> pool = activeDb.GetTierEvents(tier);
        if (pool == null || pool.Count == 0) return null;

        activeDb.GetTierRange(tier, out float tierMin, out float tierMax);
        return PickEventFromPool(pool, tierMin, tierMax, exclude);
    }

    /// <summary>
    /// Anında event havuzundan ağırlıklı seçim yapar.
    /// </summary>
    private WarForOilEvent PickImmediateEvent(List<ImmediateEventEntry> pool)
    {
        if (pool == null || pool.Count == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].targetEvent != null)
                totalWeight += pool[i].weight;
        }

        if (totalWeight <= 0f)
        {
            //tüm ağırlıklar 0 — eşit dağıt
            Debug.LogWarning("WOMAN PROCESS: ANINDA EVENT HAVUZUNDA TÜM AĞIRLIKLAR 0! EŞİT DAĞITIM YAPILIYOR.");
            var valid = new List<WarForOilEvent>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].targetEvent != null)
                    valid.Add(pool[i].targetEvent);
            }
            return valid.Count > 0 ? valid[UnityEngine.Random.Range(0, valid.Count)] : null;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].targetEvent == null) continue;
            cumulative += pool[i].weight;
            if (roll <= cumulative)
                return pool[i].targetEvent;
        }

        return pool[^1].targetEvent;
    }

    /// <summary>
    /// Zincir dallanmasından ağırlıklı seçim yapar.
    /// </summary>
    private WarForOilEvent PickEventFromChainBranches()
    {
        bool hasUnconditioned = pendingWomanChainBranches != null && pendingWomanChainBranches.Count > 0;
        bool hasConditioned = pendingWomanConditionalBranches != null && pendingWomanConditionalBranches.Count > 0;
        if (!hasUnconditioned && !hasConditioned) return null;

        //hangi aralıkta olduğumuzu belirle
        int rangeIndex = 0;
        if (pendingWomanChainInfluenceStat != ChainInfluenceStat.JustLuck)
        {
            float statPercent = GetWomanChainStatPercent(pendingWomanChainInfluenceStat) * 100f;
            if (statPercent >= pendingWomanChainThreshold2) rangeIndex = 3;
            else if (statPercent >= pendingWomanChainThreshold1) rangeIndex = 2;
            else if (statPercent >= pendingWomanChainThreshold0) rangeIndex = 1;
            else rangeIndex = 0;
        }

        //koşullu dallanma aktifse sayaç kontrolü yap
        bool conditionMet = false;
        if (pendingWomanConditionalBranching && !string.IsNullOrEmpty(pendingWomanBranchCounterKey)
            && pendingWomanConditionalBranches != null && pendingWomanConditionalBranches.Count > 0)
        {
            int counterVal = 0;
            womanChainCounters.TryGetValue(pendingWomanBranchCounterKey, out counterVal);
            bool meetsMin = counterVal >= pendingWomanBranchCounterMin;
            bool meetsMax = pendingWomanBranchCounterMax < 0 || counterVal <= pendingWomanBranchCounterMax;
            conditionMet = meetsMin && meetsMax;
        }

        List<ChainBranch> activePool = conditionMet ? pendingWomanConditionalBranches : pendingWomanChainBranches;
        if (activePool == null || activePool.Count == 0) return null;

        float[] weights = new float[activePool.Count];
        float endWeight = pendingWomanChainCanEnd ? pendingWomanChainEndWeight : 0f;
        float totalWeight = endWeight;

        for (int i = 0; i < activePool.Count; i++)
        {
            if (activePool[i].targetEvent == null
                || dismissedWomanEvents.Contains(activePool[i].targetEvent))
            {
                weights[i] = 0f;
                continue;
            }

            float w = GetBranchWeight(activePool[i], rangeIndex);
            if (w < 0f) w = 0f;
            weights[i] = w;
            totalWeight += w;
        }

        if (totalWeight <= 0f)
        {
            int eligibleCount = 0;
            for (int i = 0; i < activePool.Count; i++)
            {
                if (activePool[i].targetEvent != null
                    && !dismissedWomanEvents.Contains(activePool[i].targetEvent))
                    eligibleCount++;
            }
            if (eligibleCount == 0) return null;
            Debug.LogWarning("[KADIN SÜRECİ] ZİNCİR DALLANMASI: TÜM AĞIRLIKLAR 0! EŞİT DAĞITIM YAPILIYOR. INSPECTOR'DAN AĞIRLIKLARI KONTROL ET!");
            float equalWeight = 1f / eligibleCount;
            totalWeight = 0f;
            for (int i = 0; i < activePool.Count; i++)
            {
                if (activePool[i].targetEvent != null
                    && !dismissedWomanEvents.Contains(activePool[i].targetEvent))
                {
                    weights[i] = equalWeight;
                    totalWeight += equalWeight;
                }
            }
        }

        //ağırlıklı seçim
        float roll = UnityEngine.Random.value * totalWeight;

        //önce chain bitme kontrolü
        if (roll < endWeight)
            return null;

        float cumulative = endWeight;
        for (int i = 0; i < activePool.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                if (activePool[i].triggersAsImmediateEvent)
                {
                    lastChainPickWasImmediate = true;
                    lastChainPickImmediateDelay = activePool[i].immediateEventDelay;
                }
                return activePool[i].targetEvent;
            }
        }

        //fallback
        int lastIdx = activePool.Count - 1;
        if (activePool[lastIdx].triggersAsImmediateEvent)
        {
            lastChainPickWasImmediate = true;
            lastChainPickImmediateDelay = activePool[lastIdx].immediateEventDelay;
        }
        return activePool[lastIdx].targetEvent;
    }

    private float GetBranchWeight(ChainBranch branch, int rangeIndex)
    {
        switch (rangeIndex)
        {
            case 0: return branch.weightRange0;
            case 1: return branch.weightRange1;
            case 2: return branch.weightRange2;
            case 3: return branch.weightRange3;
            default: return branch.weightRange0;
        }
    }

    private float GetWomanChainStatPercent(ChainInfluenceStat stat)
    {
        if (GameStatManager.Instance == null) return 0.5f;
        switch (stat)
        {
            case ChainInfluenceStat.Wealth:
                return GameStatManager.Instance.GetStatPercent(StatType.Wealth);
            case ChainInfluenceStat.Suspicion:
                return GameStatManager.Instance.GetStatPercent(StatType.Suspicion);
            case ChainInfluenceStat.Reputation:
                return GameStatManager.Instance.GetStatPercent(StatType.Reputation);
            case ChainInfluenceStat.PoliticalInfluence:
                return GameStatManager.Instance.GetStatPercent(StatType.PoliticalInfluence);
            default: return 0.5f;
        }
    }

    private void EndWomanChain()
    {
        isInWomanChain = false;
        pendingWomanChainBranches = null;
        pendingWomanConditionalBranches = null;
        pendingWomanConditionalBranching = false;
        pendingWomanBranchCounterKey = null;
        pendingWomanBranchCounterMin = 0;
        pendingWomanBranchCounterMax = -1;
        womanChainCounters.Clear();
    }

    /// <summary>
    /// Havuzdan uygun bir event seçer (tekrar kontrolü + obsesyon aralığı kesişimi ile).
    /// Eventin özel aralığı tier aralığıyla kesiştirilir — daraltabilir ama genişletemez.
    /// Kesişim yoksa eventin özel aralığı geçersiz sayılır, tier aralığı olduğu gibi kullanılır.
    /// </summary>
    private WarForOilEvent PickEventFromPool(List<WarForOilEvent> pool, float tierMin, float tierMax, WarForOilEvent exclude = null)
    {
        List<WarForOilEvent> eligible = new List<WarForOilEvent>();

        for (int i = 0; i < pool.Count; i++)
        {
            WarForOilEvent evt = pool[i];
            if (evt == null) continue;
            if (evt == exclude) continue;

            //yasaklanmış mı
            if (dismissedWomanEvents.Contains(evt)) continue;

            //ikili süreç kontrolü — hem savaş hem kadın süreci aktif olmalı
            if (evt.requiresBothProcessesActive && !isInWar) continue;

            //hikaye bayrak kontrolü — tüm gerekli bayraklar aktif olmalı
            if (!IsStoryFlagsSatisfied(evt)) continue;

            //öncü event kontrolü — war for oil öncüsü varsa ve savaşta değilsek atla
            if (evt.hasPrecursorEvent && evt.precursorEventType == PrecursorEventType.WarForOil && !isInWar)
                continue;

            //obsesyon aralığı kontrolü — tier ile kesişim
            float effectiveMin = tierMin;
            float effectiveMax = tierMax;

            //eventin özel aralığı tier ile kesişiyor mu
            float intersectMin = Mathf.Max(evt.minObsession, tierMin);
            float intersectMax = Mathf.Min(evt.maxObsession, tierMax);

            if (intersectMin <= intersectMax)
            {
                //kesişim var — daraltılmış aralık kullan
                effectiveMin = intersectMin;
                effectiveMax = intersectMax;
            }
            //kesişim yoksa effectiveMin/Max tier aralığı olarak kalır (özel aralık geçersiz)

            if (womanObsession < effectiveMin || womanObsession > effectiveMax) continue;

            //tekrar kontrolü
            if (!evt.isRepeatable)
            {
                womanEventTriggerCounts.TryGetValue(evt, out int count);
                if (count > 0) continue;
            }
            else if (!evt.isUnlimitedRepeat)
            {
                womanEventTriggerCounts.TryGetValue(evt, out int count);
                if (count >= evt.maxRepeatCount) continue;
            }

            eligible.Add(evt);
        }

        if (eligible.Count == 0) return null;

        //rastgele seç (eşit ağırlık)
        return eligible[UnityEngine.Random.Range(0, eligible.Count)];
    }

    // ==================== ÖNCÜ EVENT ÇÖZÜM ====================

    /// <summary>
    /// Oyuncu öncü war for oil eventinde seçim yaptı.
    /// </summary>
    public void ResolvePrecursorWarEvent(int choiceIndex)
    {
        if (currentState != WomanProcessState.PrecursorPhase || currentPrecursorWarEvent == null) return;
        if (choiceIndex < 0 || choiceIndex >= currentPrecursorWarEvent.choices.Count) return;

        WarForOilEventChoice choice = currentPrecursorWarEvent.choices[choiceIndex];
        if (!choice.IsAvailable()) return;

        //stat etkileri uygula
        if (GameStatManager.Instance != null)
        {
            if (choice.suspicionModifier != 0f)
                GameStatManager.Instance.AddSuspicion(choice.suspicionModifier);
            if (choice.reputationModifier != 0f)
            {
                if (choice.hasReputationFloor && choice.reputationModifier < 0f)
                {
                    if (GameStatManager.Instance.Reputation < choice.reputationFloor)
                        GameStatManager.Instance.AddReputation(-0.1f);
                    else
                    {
                        GameStatManager.Instance.AddReputation(choice.reputationModifier);
                        if (GameStatManager.Instance.Reputation < choice.reputationFloor)
                            GameStatManager.Instance.SetStat(StatType.Reputation, choice.reputationFloor);
                    }
                }
                else
                {
                    GameStatManager.Instance.AddReputation(choice.reputationModifier);
                }
            }
            if (choice.politicalInfluenceModifier != 0f)
                GameStatManager.Instance.AddPoliticalInfluence(choice.politicalInfluenceModifier);
            if (choice.wealthModifier != 0f)
                GameStatManager.Instance.AddWealth(choice.wealthModifier);
        }

        //womanObsession etkisi
        if (choice.womanObsessionModifier != 0f)
        {
            float obsessionAmount = choice.womanObsessionModifier;
            if (obsessionAmount > 0f)
                obsessionAmount *= obsessionGainMultiplier;

            if (choice.hasObsessionFloor && obsessionAmount < 0f)
            {
                if (womanObsession < choice.obsessionFloor)
                {
                    //zaten floor'un altında — sadece 0.1 düşür
                    womanObsession = Mathf.Max(womanObsession - 0.1f, 0f);
                }
                else
                {
                    womanObsession = Mathf.Clamp(womanObsession + obsessionAmount, 0f, 100f);
                    if (womanObsession < choice.obsessionFloor)
                        womanObsession = choice.obsessionFloor;
                }
            }
            else
            {
                womanObsession = Mathf.Clamp(womanObsession + obsessionAmount, 0f, 100f);
            }
            OnObsessionChanged?.Invoke(womanObsession);
        }

        //feed etkileri
        if (SocialMediaManager.Instance != null)
        {
            if (choice.freezesFeed) SocialMediaManager.Instance.TryFreezeFeed();
            if (choice.slowsFeed) SocialMediaManager.Instance.TrySlowFeed();
            if (choice.hasFeedOverride)
            {
                if (choice.hasCounterFeedTopic)
                    SocialMediaManager.Instance.SetEventOverride(choice.feedOverrideTopic, choice.feedOverrideRatio, choice.counterFeedTopic, choice.counterFeedRatio, choice.feedOverrideDuration);
                else
                    SocialMediaManager.Instance.SetEventOverride(choice.feedOverrideTopic, choice.feedOverrideRatio, choice.feedOverrideDuration);
            }
        }

        //savaş-spesifik etkiler
        if (isInWar && WarForOilManager.Instance != null)
            WarForOilManager.Instance.ApplyExternalWarEffects(choice);

        currentPrecursorWarEvent = null;
        OnPrecursorEventResolved?.Invoke();

        //oyunu devam ettir ve bekleme başlat
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        precursorDelayTimer = PRECURSOR_DELAY;
        currentState = WomanProcessState.PrecursorDelay;
    }

    /// <summary>
    /// Oyuncu öncü random eventinde seçim yaptı.
    /// </summary>
    public void ResolvePrecursorRandomEvent(int choiceIndex)
    {
        if (currentState != WomanProcessState.PrecursorPhase || currentPrecursorRandomEvent == null) return;
        if (choiceIndex < 0 || choiceIndex >= currentPrecursorRandomEvent.choices.Count) return;

        EventChoice choice = currentPrecursorRandomEvent.choices[choiceIndex];

        //random event seçenek etkilerini uygula
        if (RandomEventManager.Instance != null)
            RandomEventManager.Instance.SelectChoice(choice);

        currentPrecursorRandomEvent = null;
        OnPrecursorEventResolved?.Invoke();

        //oyunu devam ettir ve bekleme başlat
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        precursorDelayTimer = PRECURSOR_DELAY;
        currentState = WomanProcessState.PrecursorDelay;
    }

    private void CheckEndConditions()
    {
        if (currentState == WomanProcessState.Inactive) return;

        //game over: obsesyon 100'e ulaştı
        if (womanObsession >= 100f)
        {
            EndWomanChain();
            currentState = WomanProcessState.Inactive;
            OnWomanProcessGameOver?.Invoke();

            //suspicion üzerinden game over tetikle
            if (GameStatManager.Instance != null)
                GameStatManager.Instance.AddSuspicionRaw(100f);

            return;
        }

        //süreç bitti: obsesyon eşiğin altına düştü
        if (womanObsession < database.endThreshold)
        {
            EndWomanChain();
            currentState = WomanProcessState.Inactive;
            OnWomanProcessEnded?.Invoke();
            return;
        }

        //obsesyon düşüş limiti — zirve takipli
        if (hasActiveDropLimit)
        {
            WomanProcessDatabase activeDb = redirectedDatabase != null ? redirectedDatabase : database;

            //low→mid geçişi: obsesyon tier1Max'ı aştıysa limit kalıcı olarak kalkar
            if (womanObsession > activeDb.tier1Max)
            {
                hasActiveDropLimit = false;
                return;
            }

            //zirveyi güncelle
            if (womanObsession > dropLimitPeakObsession)
                dropLimitPeakObsession = womanObsession;

            //eşik = zirve - delta
            float threshold = dropLimitPeakObsession - dropLimitDelta;
            if (womanObsession <= threshold)
            {
                hasActiveDropLimit = false;
                EndWomanChain();
                currentState = WomanProcessState.Inactive;
                OnWomanProcessEnded?.Invoke();
            }
        }
    }

    private enum WomanProcessState
    {
        Inactive,       //süreç başlamadı veya bitti
        Active,         //süreç aktif, event bekleniyor
        PrecursorPhase, //öncü event gösteriliyor, oyuncu seçim yapıyor
        PrecursorDelay, //öncü event çözüldü, 4 saniye bekleniyor
        EventPhase      //kadın eventi gösteriliyor
    }
}
