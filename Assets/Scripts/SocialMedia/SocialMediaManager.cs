using System;
using System.Collections.Generic;
using UnityEngine;

public class SocialMediaManager : MonoBehaviour
{
    public static SocialMediaManager Instance { get; private set; }

    public SocialMediaPostDatabase postDatabase;

    [Header("Natural Trend Settings")]
    public float minTrendDuration = 40f; //doğal trend minimum süresi (saniye)
    public float maxTrendDuration = 150f; //doğal trend maximum süresi (saniye)
    public float minNaturalTrendRatio = 0.28f; //doğal trend minimum oranı (%28)
    public float maxNaturalTrendRatio = 0.45f; //doğal trend maximum oranı (%45)

    [Header("Player Override Settings")]
    public float minPlayerOverrideDuration = 30f; //oyuncu override minimum süresi
    public float maxPlayerOverrideDuration = 90f; //oyuncu override maximum süresi
    public float minPlayerOverrideRatio = 0.70f; //oyuncu override minimum oranı (%70)
    public float maxPlayerOverrideRatio = 0.90f; //oyuncu override maximum oranı (%90)

    [Header("Post Settings")]
    public float baseMinPostInterval = 1f; //postlar arası temel minimum süre
    public float baseMaxPostInterval = 6f; //postlar arası temel maximum süre
    public float absoluteMinInterval = 1f; //kesinlikle 1 saniyenin altına inemez

    [Header("Speed Boost Settings")]
    public float minSpeedBoostDuration = 30f; //hız boost minimum süresi
    public float maxSpeedBoostDuration = 90f; //hız boost maximum süresi

    [Header("Feed Control Settings (Freeze/Slow)")]
    public float freezeDuration = 30f; //dondurma süresi
    public float slowDuration = 45f; //yavaşlatma süresi
    public float slowedMinInterval = 8f; //yavaşlatılmış min süre
    public float slowedMaxInterval = 15f; //yavaşlatılmış max süre

    [Header("Sensitive Topic Settings")]
    [Tooltip("Hassas konu olabilecek topic'ler. Boşsa hiçbir topic hassas olamaz.")]
    public List<TopicType> eligibleSensitiveTopics = new List<TopicType>();

    //runtime - feed control abilities (skill ile açılır)
    private bool canFreezeFeed = false; //feed dondurma yeteneği açık mı
    private bool canSlowFeed = false; //feed yavaşlatma yeteneği açık mı

    //runtime - feed control state
    private bool isFeedFrozen = false;
    private bool isFeedSlowed = false;
    private float feedControlTimer = 0f;
    private float feedControlDuration;

    //runtime - natural trend
    private TopicType currentNaturalTrend;
    private float naturalTrendRatio;
    private float naturalTrendTimer = 0f;
    private float nextNaturalTrendChange;

    //runtime - override (player veya event tarafından)
    private bool hasOverride = false;
    private TopicType overrideTopic;
    private float overrideRatio;
    private bool hasCounterTopic = false;
    private TopicType counterTopic;
    private float counterRatio;
    private float overrideTimer = 0f;
    private float overrideDuration;

    //runtime - post
    private float postTimer = 0f;
    private float nextPostTime;
    private HashSet<string> shownPostIds = new HashSet<string>();

    //runtime - speed boost
    private bool hasSpeedBoost = false;
    private float boostedMinInterval;
    private float boostedMaxInterval;
    private float speedBoostTimer = 0f;
    private float speedBoostDuration;

    //topic ağırlıkları - hangi topic'in doğal trend olma şansını etkiler
    private Dictionary<TopicType, float> topicWeights = new Dictionary<TopicType, float>();

    //runtime - sensitive topic (ülkenin hassas konusu, oyun başında random belirlenir)
    private TopicType sensitiveTopic;

    //events
    public static event Action<TopicType> OnNaturalTrendChanged;
    public static event Action<TopicType, float> OnOverrideStarted; //topic, duration
    public static event Action OnOverrideEnded;
    public static event Action<SocialMediaPost> OnNewPost;
    public static event Action<float> OnSpeedBoostStarted; //duration
    public static event Action OnSpeedBoostEnded;
    public static event Action<float> OnFeedFrozen; //duration
    public static event Action OnFeedUnfrozen;
    public static event Action<float> OnFeedSlowed; //duration
    public static event Action OnFeedSpeedRestored;
    public static event Action OnFreezeAbilityUnlocked;
    public static event Action OnSlowAbilityUnlocked;
    public static event Action<TopicType> OnSensitiveTopicDetermined; //oyun başında hassas konu belirlendiğinde

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeTopicWeights();
    }

    private void Start()
    {
        DetermineSensitiveTopic();
        SelectNewNaturalTrend();
        ScheduleNextPost();
    }

    //oyun başında ülkenin hassas konusunu belirle (sadece eligible listesinden)
    private void DetermineSensitiveTopic()
    {
        if (eligibleSensitiveTopics == null || eligibleSensitiveTopics.Count == 0)
        {
            //eligible liste boşsa hassas konu yok
            return;
        }

        sensitiveTopic = eligibleSensitiveTopics[UnityEngine.Random.Range(0, eligibleSensitiveTopics.Count)];
        OnSensitiveTopicDetermined?.Invoke(sensitiveTopic);
    }

    //hassas konu var mı kontrol et
    public bool HasSensitiveTopic()
    {
        return eligibleSensitiveTopics != null && eligibleSensitiveTopics.Count > 0;
    }

    public TopicType GetSensitiveTopic()
    {
        return sensitiveTopic;
    }

    //hassas topic mi kontrol et (ileride manipülasyon etkisi hesabında kullanılacak)
    public bool IsSensitiveTopic(TopicType topic)
    {
        if (!HasSensitiveTopic())
            return false;

        return topic == sensitiveTopic;
    }

    private void Update()
    {
        //natural trend timer
        naturalTrendTimer += Time.deltaTime;
        if (naturalTrendTimer >= nextNaturalTrendChange)
        {
            SelectNewNaturalTrend();
        }

        //override timer
        if (hasOverride)
        {
            overrideTimer += Time.deltaTime;
            if (overrideTimer >= overrideDuration)
            {
                EndOverride();
            }
        }

        //speed boost timer
        if (hasSpeedBoost)
        {
            speedBoostTimer += Time.deltaTime;
            if (speedBoostTimer >= speedBoostDuration)
            {
                EndSpeedBoost();
            }
        }

        //feed control timer (freeze/slow)
        if (isFeedFrozen || isFeedSlowed)
        {
            feedControlTimer += Time.deltaTime;
            if (feedControlTimer >= feedControlDuration)
            {
                EndFeedControl();
            }
        }

        //post timer (freeze durumunda post gelmez)
        if (!isFeedFrozen)
        {
            postTimer += Time.deltaTime;
            if (postTimer >= nextPostTime)
            {
                TryShowNewPost();
                ScheduleNextPost();
            }
        }
    }

    private void InitializeTopicWeights()
    {
        foreach (TopicType topic in Enum.GetValues(typeof(TopicType)))
        {
            topicWeights[topic] = 1f;
        }
    }

    //skill'ler bu metodu çağırarak topic'in doğal trend olma şansını değiştirir
    public void ModifyTopicWeight(TopicType topic, float amount)
    {
        if (topicWeights.ContainsKey(topic))
        {
            topicWeights[topic] += amount;
            if (topicWeights[topic] < 0f) topicWeights[topic] = 0f;
        }
    }

    public float GetTopicWeight(TopicType topic)
    {
        return topicWeights.ContainsKey(topic) ? topicWeights[topic] : 1f;
    }

    //oyuncu skill ile feed'i override eder
    public void SetPlayerOverride(TopicType topic)
    {
        hasOverride = true;
        overrideTopic = topic;
        overrideRatio = UnityEngine.Random.Range(minPlayerOverrideRatio, maxPlayerOverrideRatio);
        overrideDuration = UnityEngine.Random.Range(minPlayerOverrideDuration, maxPlayerOverrideDuration);
        overrideTimer = 0f;

        OnOverrideStarted?.Invoke(overrideTopic, overrideDuration);
    }

    //event choice ile feed'i override eder (custom ratio ve duration)
    public void SetEventOverride(TopicType topic, float ratio, float duration)
    {
        hasOverride = true;
        overrideTopic = topic;
        overrideRatio = Mathf.Clamp01(ratio);
        hasCounterTopic = false;
        overrideDuration = duration;
        overrideTimer = 0f;

        OnOverrideStarted?.Invoke(overrideTopic, overrideDuration);
    }

    //event choice ile feed'i override eder + counter topic ekler
    public void SetEventOverride(TopicType topic, float ratio, TopicType counter, float cRatio, float duration)
    {
        hasOverride = true;
        overrideTopic = topic;
        overrideRatio = Mathf.Clamp01(ratio);
        hasCounterTopic = true;
        counterTopic = counter;
        //counter ratio, ana ratio ile toplamı 1'i geçmeyecek şekilde sınırlanır
        counterRatio = Mathf.Clamp(cRatio, 0f, 1f - overrideRatio);
        overrideDuration = duration;
        overrideTimer = 0f;

        OnOverrideStarted?.Invoke(overrideTopic, overrideDuration);
    }

    private void EndOverride()
    {
        hasOverride = false;
        hasCounterTopic = false;
        overrideTimer = 0f;
        OnOverrideEnded?.Invoke();
    }

    //skill ile post hızını artır (bot basma vb.)
    public void SetSpeedBoost(float minInterval, float maxInterval)
    {
        hasSpeedBoost = true;
        boostedMinInterval = Mathf.Max(minInterval, absoluteMinInterval); //min 1 saniye
        boostedMaxInterval = Mathf.Max(maxInterval, absoluteMinInterval);
        speedBoostDuration = UnityEngine.Random.Range(minSpeedBoostDuration, maxSpeedBoostDuration);
        speedBoostTimer = 0f;

        OnSpeedBoostStarted?.Invoke(speedBoostDuration);
    }

    //event ile post hızını artır (custom duration)
    public void SetSpeedBoostWithDuration(float minInterval, float maxInterval, float duration)
    {
        hasSpeedBoost = true;
        boostedMinInterval = Mathf.Max(minInterval, absoluteMinInterval);
        boostedMaxInterval = Mathf.Max(maxInterval, absoluteMinInterval);
        speedBoostDuration = duration;
        speedBoostTimer = 0f;

        OnSpeedBoostStarted?.Invoke(speedBoostDuration);
    }

    private void EndSpeedBoost()
    {
        hasSpeedBoost = false;
        speedBoostTimer = 0f;
        OnSpeedBoostEnded?.Invoke();
    }

    //skill ile freeze yeteneği aç
    public void UnlockFreezeAbility()
    {
        canFreezeFeed = true;
        OnFreezeAbilityUnlocked?.Invoke();
    }

    //skill ile slow yeteneği aç
    public void UnlockSlowAbility()
    {
        canSlowFeed = true;
        OnSlowAbilityUnlocked?.Invoke();
    }

    //oyuncu feed'i dondurur (UI butonu ile çağrılır)
    public bool TryFreezeFeed()
    {
        if (!canFreezeFeed || isFeedFrozen || isFeedSlowed)
            return false;

        isFeedFrozen = true;
        feedControlTimer = 0f;
        feedControlDuration = freezeDuration;
        OnFeedFrozen?.Invoke(feedControlDuration);
        return true;
    }

    //oyuncu feed'i yavaşlatır (UI butonu ile çağrılır)
    public bool TrySlowFeed()
    {
        if (!canSlowFeed || isFeedFrozen || isFeedSlowed)
            return false;

        isFeedSlowed = true;
        feedControlTimer = 0f;
        feedControlDuration = slowDuration;
        OnFeedSlowed?.Invoke(feedControlDuration);
        return true;
    }

    private void EndFeedControl()
    {
        bool wasFrozen = isFeedFrozen;
        bool wasSlowed = isFeedSlowed;

        isFeedFrozen = false;
        isFeedSlowed = false;
        feedControlTimer = 0f;

        if (wasFrozen)
            OnFeedUnfrozen?.Invoke();
        if (wasSlowed)
            OnFeedSpeedRestored?.Invoke();
    }

    //getter'lar for feed control
    public bool CanFreezeFeed() => canFreezeFeed;
    public bool CanSlowFeed() => canSlowFeed;
    public bool IsFeedFrozen() => isFeedFrozen;
    public bool IsFeedSlowed() => isFeedSlowed;
    public float GetFeedControlTimeRemaining() => (isFeedFrozen || isFeedSlowed) ? feedControlDuration - feedControlTimer : 0f;

    private void SelectNewNaturalTrend()
    {
        naturalTrendTimer = 0f;
        nextNaturalTrendChange = UnityEngine.Random.Range(minTrendDuration, maxTrendDuration);
        naturalTrendRatio = UnityEngine.Random.Range(minNaturalTrendRatio, maxNaturalTrendRatio);

        //weighted random selection
        float totalWeight = 0f;
        foreach (var kvp in topicWeights)
        {
            totalWeight += kvp.Value;
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var kvp in topicWeights)
        {
            cumulative += kvp.Value;
            if (randomValue <= cumulative)
            {
                currentNaturalTrend = kvp.Key;
                break;
            }
        }

        OnNaturalTrendChanged?.Invoke(currentNaturalTrend);
    }

    private void ScheduleNextPost()
    {
        postTimer = 0f;

        if (isFeedSlowed)
        {
            //yavaşlatılmış
            nextPostTime = UnityEngine.Random.Range(slowedMinInterval, slowedMaxInterval);
        }
        else if (hasSpeedBoost)
        {
            //hızlandırılmış
            nextPostTime = UnityEngine.Random.Range(boostedMinInterval, boostedMaxInterval);
        }
        else
        {
            //normal
            nextPostTime = UnityEngine.Random.Range(baseMinPostInterval, baseMaxPostInterval);
        }
    }

    private void TryShowNewPost()
    {
        SocialMediaPost post = GetNextPost();
        if (post != null)
        {
            if (!post.isRepeatable)
            {
                shownPostIds.Add(post.id);
            }
            OnNewPost?.Invoke(post);
        }
    }

    private SocialMediaPost GetNextPost()
    {
        TopicType activeTopic;
        float activeRatio;

        //override varsa onu kullan, yoksa doğal trend
        if (hasOverride)
        {
            activeTopic = overrideTopic;
            activeRatio = overrideRatio;
        }
        else
        {
            activeTopic = currentNaturalTrend;
            activeRatio = naturalTrendRatio;
        }

        //aktif topic'ten mi, counter topic'ten mi, yoksa diğerlerinden mi?
        float roll = UnityEngine.Random.value;
        TopicType? selectedTopic = null;

        if (roll < activeRatio)
        {
            selectedTopic = activeTopic;
        }
        else if (hasOverride && hasCounterTopic && roll < activeRatio + counterRatio)
        {
            selectedTopic = counterTopic;
        }

        List<SocialMediaPost> eligiblePosts = GetEligiblePosts(selectedTopic);

        if (eligiblePosts.Count == 0)
        {
            eligiblePosts = GetEligiblePosts(null);
        }

        if (eligiblePosts.Count == 0)
            return null;

        return eligiblePosts[UnityEngine.Random.Range(0, eligiblePosts.Count)];
    }

    private List<SocialMediaPost> GetEligiblePosts(TopicType? filterTopic)
    {
        List<SocialMediaPost> eligible = new List<SocialMediaPost>();

        foreach (SocialMediaPost post in postDatabase.allPosts)
        {
            if (IsPostEligible(post, filterTopic))
            {
                eligible.Add(post);
            }
        }

        return eligible;
    }

    private bool IsPostEligible(SocialMediaPost post, TopicType? filterTopic)
    {
        if (filterTopic.HasValue && post.topic != filterTopic.Value)
            return false;

        if (!post.isRepeatable && shownPostIds.Contains(post.id))
            return false;

        return true;
    }

    //getter'lar
    public TopicType GetCurrentNaturalTrend()
    {
        return currentNaturalTrend;
    }

    public TopicType GetActiveTopic()
    {
        return hasOverride ? overrideTopic : currentNaturalTrend;
    }

    public bool HasOverride()
    {
        return hasOverride;
    }

    public float GetOverrideTimeRemaining()
    {
        return hasOverride ? overrideDuration - overrideTimer : 0f;
    }

    public float GetNaturalTrendTimeRemaining()
    {
        return nextNaturalTrendChange - naturalTrendTimer;
    }
}
