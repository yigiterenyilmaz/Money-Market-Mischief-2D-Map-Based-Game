using System;
using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    public EventDatabase eventDatabase;
    public float phaseInterval = 900f; // her 900 saniyede (15 dakika) bir phase artar. Toplam 4 phase (0-3).
    public int currentGamePhase = 0; //şu anki phase
    public float elapsedTime = 0f; //oyun başladığından beri geçen toplam süre

    public float minEventInterval = 120f; // minimum 120 saniye
    public float maxEventInterval = 140f; // maximum 140 saniye
    private float eventTimer = 0f;
    private float nextEventTime;

    //global event engeli — WarForOilManager üzerinden yönetilir

    private HashSet<Event> triggeredEvents = new HashSet<Event>();
    //bu zamana kadar oyuncuya atılmış eventler.

    public static event Action<Event> OnEventTriggered;
    //her event tetiklendiğinde gönderilen action
    public static event Action<int> OnPhaseChanged;
    //her phase değiştiğinde atılan action
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        nextEventTime = UnityEngine.Random.Range(minEventInterval, maxEventInterval);
        //ilk event'in süresi rastgele belirlenir.
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        int newPhase = (int)(elapsedTime / phaseInterval);
        if (newPhase != currentGamePhase)
        {
            currentGamePhase = newPhase;
            OnPhaseChanged?.Invoke(currentGamePhase);
        }

        eventTimer += Time.deltaTime;
        if (eventTimer >= nextEventTime)
        {
            eventTimer = 0f;
            nextEventTime = UnityEngine.Random.Range(minEventInterval, maxEventInterval);
            TriggerRandomEvent(); //timer dolunca event tetikle, yeni rastgele süre belirle.
        }
    }

    public List<Event> GetEligibleEvents()
    {
        List<Event> eligibleEvents = new List<Event>();

        foreach (Event evt in eventDatabase.allEvents)
        {
            if (IsEventEligible(evt))
            {
                eligibleEvents.Add(evt);
            }
        }

        return eligibleEvents; //kullanıcının oyun durumuna göre atılabilecek eventlerin listesini döner
    }

    public bool IsEventEligible(Event evt) //tekil event kullanıcıya gösterilmeye uygun mu diye bakar
    {
        if (!evt.isRepeatable && triggeredEvents.Contains(evt))
            return false; //eğer tekrar tekrar atılabilir değilse ve daha önce atıldıysa false

        if (currentGamePhase < evt.minGamePhase || currentGamePhase > evt.maxGamePhase)
            return false; //eğer uygun game phase de değilse false.
            //temel olarak eventler bulunan game phase e bağlıdır. 

        if (evt.requiredSkills != null && evt.requiredSkills.Count > 0)
        {//eğer event tetiklemesi için bir skillin açılmasına ihtiyacımız varsa
        //o skill açık mı diye bakar.
            foreach (Skill skill in evt.requiredSkills)
            {
                if (!SkillTreeManager.Instance.IsUnlocked(skill.id))
                    return false;
            }
        }

        if (evt.statConditions != null && evt.statConditions.Count > 0)
        { //eğer event tetiklemesi için bir stat şartı varsa ona bakar.
            foreach (StatCondition condition in evt.statConditions)
            {
                if (!condition.IsMet())
                    return false;
            }
        }

        return true;
    }

    public Event GetRandomEvent()//event havuzundan bir adet random event çeker
    {
        List<Event> eligibleEvents = GetEligibleEvents();

        if (eligibleEvents.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (Event evt in eligibleEvents)
        {
            totalWeight += evt.weight; //atılması mümkün eventlerin ağırlıklarını toplar
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight); //toplam ağırlıkla 0 arasında random bir sayı
        float currentWeight = 0f;

        foreach (Event evt in eligibleEvents)
        {
            currentWeight += evt.weight;
            if (randomValue <= currentWeight)
            {
                return evt;
            }
        }

        return eligibleEvents[eligibleEvents.Count - 1];
    }

    public void TriggerRandomEvent() //random event tetikler
    {
        //global event engeli aktifse random event tetikleme, sayacı düşür
        if (WarForOilManager.Instance != null && WarForOilManager.Instance.TryConsumeGlobalBlock())
            return;

        //başka bir sistem az önce event gösterdiyse bu cycle'ı atla (en az 2s aralık)
        if (!EventCoordinator.CanShowEvent())
            return;

        Event evt = GetRandomEvent();
        if (evt == null) return;

        EventCoordinator.MarkEventShown();
        triggeredEvents.Add(evt);
        OnEventTriggered?.Invoke(evt);
    }

    public void SelectChoice(EventChoice choice)
    {
        //efektleri uygula
        if (choice.effects != null)
        {
            foreach (SkillEffect effect in choice.effects)
            {
                effect.Apply();
            }
        }

        //feed override varsa uygula
        if (choice.overridesFeed && SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.SetEventOverride(
                choice.feedTopic,
                choice.feedOverrideRatio,
                choice.feedOverrideDuration
            );
        }

        //feed speed boost varsa uygula
        if (choice.boostsFeedSpeed && SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.SetSpeedBoostWithDuration(
                choice.boostedMinInterval,
                choice.boostedMaxInterval,
                choice.speedBoostDuration
            );
        }

        //artık slot sistemi yok — cooldown otomatik işliyor
    }

    public void SetGamePhase(int phase)
    {
        currentGamePhase = phase;
    }

    public int CurrentPhase => currentGamePhase;
}
