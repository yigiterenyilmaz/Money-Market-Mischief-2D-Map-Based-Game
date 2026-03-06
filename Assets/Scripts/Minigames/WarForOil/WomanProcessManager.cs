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
    private bool isInWar; //şu an savaş aktif mi
    private int eventCounter; //son kadın eventinden beri sayılan event sayısı
    private WarForOilEvent currentWomanEvent;
    private float eventDecisionTimer;
    private bool pendingTrigger; //bir sonraki uygun anda kadın eventi tetiklenecek
    private Dictionary<WarForOilEvent, int> womanEventTriggerCounts = new Dictionary<WarForOilEvent, int>();

    //events — UI dinleyecek
    public static event Action OnWomanProcessStarted; //süreç başladı
    public static event Action<float> OnObsessionChanged; //obsesyon değeri değişti (yeni değer)
    public static event Action<WarForOilEvent> OnWomanEventTriggered; //kadın eventi tetiklendi
    public static event Action<float> OnWomanEventDecisionTimerUpdate; //karar sayacı güncellendi
    public static event Action<WarForOilEventChoice> OnWomanEventResolved; //seçim yapıldı
    public static event Action OnWomanProcessEnded; //süreç bitti (obsesyon düştü)
    public static event Action OnWomanProcessGameOver; //game over (obsesyon 100)

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

        //bekleyen tetikleme — uygun an gelince kadın eventini göster
        if (pendingTrigger && currentState == WomanProcessState.Active)
        {
            if (CanTriggerNow())
            {
                pendingTrigger = false;
                TriggerWomanEvent();
            }
        }

        //event karar sayacı
        if (currentState == WomanProcessState.EventPhase)
        {
            eventDecisionTimer -= Time.unscaledDeltaTime;
            OnWomanEventDecisionTimerUpdate?.Invoke(eventDecisionTimer);

            if (eventDecisionTimer <= 0f)
            {
                //süre doldu — varsayılan seçenek
                int defaultIdx = currentWomanEvent.defaultChoiceIndex;
                if (defaultIdx < 0 || defaultIdx >= currentWomanEvent.choices.Count)
                    defaultIdx = 0;

                ResolveEvent(defaultIdx);
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
            womanObsession = Mathf.Clamp(womanObsession + choice.womanObsessionModifier, 0f, 100f);
            OnObsessionChanged?.Invoke(womanObsession);
        }

        //genel stat etkileri — savaş içi/dışı her zaman uygulanır
        if (GameStatManager.Instance != null)
        {
            if (choice.suspicionModifier != 0f)
                GameStatManager.Instance.AddSuspicion(choice.suspicionModifier);
            if (choice.reputationModifier != 0f)
                GameStatManager.Instance.AddReputation(choice.reputationModifier);
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
                else if (GameStatManager.Instance != null)
                    GameStatManager.Instance.ApplyPermanentGainMultiplier((StatType)entry.stat, entry.multiplier);
            }
        }

        OnWomanEventResolved?.Invoke(choice);

        currentWomanEvent = null;

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        currentState = WomanProcessState.Active;

        //bitiş kontrolleri
        CheckEndConditions();
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
        eventCounter++;

        int tier = database.GetTier(womanObsession);
        int frequency = database.GetTierFrequency(tier);

        if (eventCounter >= frequency)
        {
            eventCounter = 0;
            pendingTrigger = true;
        }
    }

    /// <summary>
    /// Şu an kadın eventi tetiklenebilir mi (başka event gösterilmiyor).
    /// </summary>
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
        int tier = database.GetTier(womanObsession);
        List<WarForOilEvent> pool = database.GetTierEvents(tier);

        if (pool == null || pool.Count == 0) return;

        WarForOilEvent evt = PickEventFromPool(pool);
        if (evt == null) return;

        currentWomanEvent = evt;
        womanEventTriggerCounts.TryGetValue(evt, out int count);
        womanEventTriggerCounts[evt] = count + 1;

        EventCoordinator.MarkEventShown();

        currentState = WomanProcessState.EventPhase;
        eventDecisionTimer = database.decisionTime;

        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnWomanEventTriggered?.Invoke(evt);
    }

    /// <summary>
    /// Havuzdan uygun bir event seçer (tekrar kontrolü ile).
    /// </summary>
    private WarForOilEvent PickEventFromPool(List<WarForOilEvent> pool)
    {
        List<WarForOilEvent> eligible = new List<WarForOilEvent>();

        for (int i = 0; i < pool.Count; i++)
        {
            WarForOilEvent evt = pool[i];
            if (evt == null) continue;

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

    private void CheckEndConditions()
    {
        if (currentState == WomanProcessState.Inactive) return;

        //game over: obsesyon 100'e ulaştı
        if (womanObsession >= 100f)
        {
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
            currentState = WomanProcessState.Inactive;
            OnWomanProcessEnded?.Invoke();
        }
    }

    private enum WomanProcessState
    {
        Inactive,   //süreç başlamadı veya bitti
        Active,     //süreç aktif, event bekleniyor
        EventPhase  //kadın eventi gösteriliyor
    }
}
