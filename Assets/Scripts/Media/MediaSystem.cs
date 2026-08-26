using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Oyuncunun sahip olduğu medya mecraları. Her biri ERİŞİM (reach) katar.</summary>
public enum MediaOutlet
{
    LocalNewspaper, //b2 — yerel gazete
    SocialMedia,    //b3 — sosyal medya hesabı
    NewsChannel,    //b6 — haber kanalı
    CelebrityDeal   //b7 — ünlülerle iş birliği
}

/// <summary>Oyuncunun tetiklediği medya hamleleri. Skill'lerin aktif yeteneklerinden çağrılır.</summary>
public enum MediaAction
{
    Charity,     //b4  — yardım yapma
    Statement,   //b5  — demeç verme
    Debate,      //b15 — münazara
    PraiseSelf,  //b3  — kendimizi övdürme
    SteerTrend,  //b8  — trendlere yön verme / konuyu dağıtma
    Propaganda,  //b9  — siyasi propaganda
    Scandal      //b10 — şantaj / ifşa / hadise
}

/// <summary>Anket türleri. Gizli statları oyuncuya TAHMİNİ olarak açar.</summary>
public enum PollKind
{
    Reputation,         //b11 — itibarı kestirme
    Suspicion,          //b12 — şüphe anketi
    PoliticalInfluence, //b13 — siyasi anket
    FeedLeaning         //b14 — feed yatkınlığı
}

/// <summary>Bir anketin sonucu. Kesin değer değil, ±<see cref="margin"/> paylı bir tahmindir.</summary>
public struct PollResult
{
    public PollKind kind;
    public float estimate;       //tahmini değer (FeedLeaning için anlamsız)
    public float margin;         //± hata payı
    public TopicType topic;      //FeedLeaning: an itibarıyla öne çıkan konu
    public bool topicIsSensitive; //FeedLeaning: bu konu haritanın hassas konusu mu
    public string summary;       //UI yokken okunabilir tek satır
}

/// <summary>
/// MEDYA SİSTEMİ — B ağacının (b1–b15) tamamının arkasındaki tek MonoBehaviour.
///
/// İki işi vardır:
///  1. İTİBAR ÜRETİMİ. İtibar tek başına bir puan değil, şüphenin ARTIŞ HIZINI belirleyen
///     çarpandır (GameStatManager.GetSuspicionMultiplier). Yani medya, şüpheyi silmez;
///     şüphenin birikme hızını yavaşlatır. Oyunun kaybetme koşulu şüphenin dolması olduğu
///     için bu ağaç doğrudan hayatta kalma süresini uzatır.
///  2. FEED MANİPÜLASYONU. SocialMediaManager üzerinde gündemi çevirir (b8/b9/b10).
///
/// ERİŞİM (reach): sahip olunan mecraların toplamı. Her aktif hamlenin etkisini çarpar.
/// b2/b6/b7 tek başlarına bir şey yapmaz — çarpan düğümleridir, bütün ağacı büyütürler.
///
/// HASSAS KONU: SocialMediaManager oyun başında haritaya bir hassas konu atar. Bir hamle
/// o konuya denk gelirse getirisi <see cref="sensitiveTopicPayoffMultiplier"/> katına çıkar —
/// "olasılığı fazla değil ama getirisi fazla" kuralı buradan geliyor.
///
/// UYARI — SUNUM YOK: anketler (b11–b14) gizli statları açar ama oyunda bunu gösterecek bir
/// ekran yok. Sonuç şimdilik OnPollCompleted event'i ve Debug.Log ile veriliyor.
/// Bkz. Assets/Scripts/Media/media-politics-readme.md
/// </summary>
public class MediaSystem : MonoBehaviour
{
    public static MediaSystem Instance { get; private set; }

    // -------------------------------------------------------------------------
    // AYARLAR
    // -------------------------------------------------------------------------

    [Header("Erişim (reach) — mecraların kattığı çarpan")]
    [Tooltip("Hiç mecra yokken taban erişim.")]
    public float baseReach = 1f;
    public float newspaperReach = 0.25f;
    public float socialMediaReach = 0.35f;
    public float newsChannelReach = 0.60f;
    public float celebrityReach = 0.50f;

    [Header("b4 — Yardım Yapma")]
    [Tooltip("Bir yardım kampanyasının maliyeti.")]
    public float charityCost = 25_000f;
    [Tooltip("Taban itibar kazancı. Boş arazi oranıyla ölçeklenir.")]
    public float charityReputation = 6f;
    [Tooltip("Haritada hiç boş arazi yokken kalan kazanç oranı. Yardım şehirde de yapılır " +
             "ama asıl karşılığını el değmemiş bölgede verir.")]
    [Range(0f, 1f)] public float charityMinLandFactor = 0.3f;

    [Header("b5 — Demeç Verme")]
    public float statementReputation = 3f;
    [Tooltip("Demecin gündemi kendi konusuna çektiği süre (saniye).")]
    public float statementOverrideSeconds = 25f;
    [Range(0f, 1f)] public float statementOverrideRatio = 0.6f;

    [Header("b15 — Münazara")]
    [Tooltip("Kazanılan münazaranın itibar getirisi.")]
    public float debateWinReputation = 10f;
    [Tooltip("Kaybedilen münazaranın itibar bedeli.")]
    public float debateLoseReputation = -6f;
    [Tooltip("Kaybedilen münazaranın şüphe bedeli.")]
    public float debateLoseSuspicion = 2f;
    [Tooltip("Taban kazanma şansı. İtibar ve erişim bunu yukarı çeker.")]
    [Range(0f, 1f)] public float debateBaseWinChance = 0.5f;

    [Header("b3 — Kendini Övdürme")]
    public float praiseReputation = 4f;
    [Tooltip("Kendini övmek göze batar: küçük bir şüphe bedeli vardır.")]
    public float praiseSuspicion = 0.5f;

    [Header("b8 — Trendlere Yön Verme")]
    [Tooltip("Gündemi dağıtma süresi (saniye).")]
    public float steerSeconds = 45f;
    [Tooltip("Yerine geçen konunun feed'deki payı.")]
    [Range(0f, 1f)] public float steerRatio = 0.65f;
    [Tooltip("Bastırılan eski trendin feed'de kalan payı.")]
    [Range(0f, 1f)] public float steerCounterRatio = 0.1f;

    [Header("b9 — Siyasi Propaganda")]
    public float propagandaInfluence = 5f;
    public float propagandaSuspicion = 1f;
    public float propagandaSeconds = 60f;
    [Range(0f, 1f)] public float propagandaRatio = 0.7f;

    [Header("b10 — Şantaj / İfşa / Hadise")]
    public float scandalInfluence = 10f;
    [Tooltip("İfşa pahalıdır: en yüksek şüphe bedeli bu hamlededir.")]
    public float scandalSuspicion = 6f;
    public float scandalSeconds = 90f;
    [Range(0f, 1f)] public float scandalRatio = 0.85f;

    [Header("Hassas Konu")]
    [Tooltip("Hamle haritanın hassas konusuna denk gelirse getiri bu katsayıyla çarpılır.")]
    public float sensitiveTopicPayoffMultiplier = 2.5f;

    [Header("Anketler (b11–b14)")]
    [Tooltip("İtibar anketinin taban hata payı (±).")]
    public float reputationPollMargin = 8f;
    [Tooltip("Şüphe anketinin taban hata payı (±).")]
    public float suspicionPollMargin = 5f;
    [Tooltip("Siyasi anketin taban hata payı (±).")]
    public float influencePollMargin = 6f;
    [Tooltip("Şüphe anketinin kendi bedeli: anket yapıldığı duyulur, şüphe bir tık artar.")]
    public float suspicionPollCost = 1f;

    // -------------------------------------------------------------------------
    // DURUM
    // -------------------------------------------------------------------------

    private bool unlocked;
    private readonly HashSet<MediaOutlet> outlets = new HashSet<MediaOutlet>();
    private readonly HashSet<MediaAction> actions = new HashSet<MediaAction>();
    private readonly HashSet<PollKind> polls = new HashSet<PollKind>();

    //c16 (siyaset ağacı) medyaya kadro yollar; erişimi dışarıdan büyütür
    private float externalReach;

    public bool IsUnlocked => unlocked;
    public bool HasOutlet(MediaOutlet o) => outlets.Contains(o);
    public bool HasAction(MediaAction a) => actions.Contains(a);
    public bool HasPoll(PollKind p) => polls.Contains(p);
    public PollResult LastPollResult { get; private set; }

    public static event Action OnMediaUnlocked;
    public static event Action<MediaOutlet> OnOutletAcquired;
    public static event Action<MediaAction, float> OnMediaActionUsed; //hamle, itibar etkisi
    public static event Action<PollResult> OnPollCompleted;

    private void Awake()
    {
        //DİKKAT: paylaşılan Managers objesinde olduğumuz için Destroy(gameObject) DEĞİL —
        //o, objedeki bütün manager'ları birlikte götürür.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // -------------------------------------------------------------------------
    // AÇILIŞ (skill efektlerinden çağrılır)
    // -------------------------------------------------------------------------

    /// <summary>b1 — medya ağacının kökü. Sistemi çalışır hale getirir.</summary>
    public void Unlock()
    {
        if (unlocked) return;
        unlocked = true;
        OnMediaUnlocked?.Invoke();
    }

    /// <summary>Bir mecra satın alınır. Mecralar erişimi büyütür, tek başlarına hamle yapmaz.</summary>
    public void AcquireOutlet(MediaOutlet outlet)
    {
        Unlock(); //kök alınmadan bir alt düğüm açılamaz ama efekt sırası garanti değil
        if (!outlets.Add(outlet)) return;
        OnOutletAcquired?.Invoke(outlet);
    }

    /// <summary>Bir aktif hamleyi kullanılabilir yapar.</summary>
    public void EnableAction(MediaAction action)
    {
        Unlock();
        actions.Add(action);
    }

    /// <summary>Bir anket türünü açar.</summary>
    public void EnablePoll(PollKind kind)
    {
        Unlock();
        polls.Add(kind);
    }

    /// <summary>c16 — akademiden/kadrodan medyaya aktarılan güç. Erişimi kalıcı büyütür.</summary>
    public void AddExternalReach(float amount)
    {
        externalReach += amount;
    }

    /// <summary>Toplam erişim çarpanı. Bütün aktif hamlelerin etkisi bununla çarpılır.</summary>
    public float Reach
    {
        get
        {
            float r = baseReach + externalReach;
            if (outlets.Contains(MediaOutlet.LocalNewspaper)) r += newspaperReach;
            if (outlets.Contains(MediaOutlet.SocialMedia))    r += socialMediaReach;
            if (outlets.Contains(MediaOutlet.NewsChannel))    r += newsChannelReach;
            if (outlets.Contains(MediaOutlet.CelebrityDeal))  r += celebrityReach;
            return r;
        }
    }

    // -------------------------------------------------------------------------
    // HAMLELER
    // -------------------------------------------------------------------------

    /// <summary>
    /// Aktif yeteneklerin tek giriş kapısı. Skill'in cooldown'u SkillTreeManager'da tutulur,
    /// burada yalnızca hamlenin kendisi yürür.
    /// </summary>
    public bool PerformAction(MediaAction action)
    {
        if (!unlocked)
        {
            Debug.LogWarning("[MediaSystem] Medya sistemi açık değil — hamle yapılamadı: " + action);
            return false;
        }
        if (!actions.Contains(action))
        {
            Debug.LogWarning("[MediaSystem] Bu hamle açılmamış: " + action);
            return false;
        }
        if (GameStatManager.Instance == null) return false;

        switch (action)
        {
            case MediaAction.Charity:    return DoCharity();
            case MediaAction.Statement:  return DoStatement();
            case MediaAction.Debate:     return DoDebate();
            case MediaAction.PraiseSelf: return DoPraiseSelf();
            case MediaAction.SteerTrend: return DoSteerTrend();
            case MediaAction.Propaganda: return DoPropaganda();
            case MediaAction.Scandal:    return DoScandal();
            default: return false;
        }
    }

    //b4 — yardım yapma. Boş arazi (Urban = el değmemiş doğa bölgesi) oranı kazancı belirler.
    private bool DoCharity()
    {
        var stats = GameStatManager.Instance;
        if (!stats.TrySpendWealth(charityCost))
        {
            Debug.Log("[MediaSystem] Yardım için para yetmedi.");
            return false;
        }

        float landFactor = charityMinLandFactor;
        if (CountryData.Instance != null)
        {
            //Urban bölgesi haritada "doğa"dır (CountryData log'unda da öyle etiketli):
            //yapılaşmamış, yardımın görünür olduğu yer.
            float emptyRatio = Mathf.Clamp01(CountryData.Instance.GetRegionRatio(RegionType.Urban));
            landFactor = Mathf.Lerp(charityMinLandFactor, 1f, emptyRatio);
        }

        float gain = charityReputation * landFactor * Reach;
        stats.AddReputation(gain);
        OnMediaActionUsed?.Invoke(MediaAction.Charity, gain);
        return true;
    }

    //b5 — demeç verme. Konu haritanın baskın bölgesinden seçilir.
    private bool DoStatement()
    {
        TopicType topic = TopicForDominantRegion();
        float gain = statementReputation * Reach * PayoffMultiplierFor(topic);

        GameStatManager.Instance.AddReputation(gain);
        PushTopic(topic, statementOverrideRatio, statementOverrideSeconds);

        OnMediaActionUsed?.Invoke(MediaAction.Statement, gain);
        return true;
    }

    //b15 — münazara. Tek zar: kazanırsan büyük itibar, kaybedersen itibar + şüphe bedeli.
    private bool DoDebate()
    {
        var stats = GameStatManager.Instance;

        //itibarı yüksek ve erişimi geniş olan münazarayı daha sık kazanır
        float repRatio = stats.GetStatPercent(StatType.Reputation);
        float chance = Mathf.Clamp01(debateBaseWinChance + 0.25f * repRatio + 0.10f * (Reach - 1f));

        bool won = UnityEngine.Random.value < chance;
        float delta;
        if (won)
        {
            delta = debateWinReputation * Reach;
            stats.AddReputation(delta);
        }
        else
        {
            delta = debateLoseReputation;
            stats.AddReputation(delta);
            stats.AddSuspicion(debateLoseSuspicion);
        }

        OnMediaActionUsed?.Invoke(MediaAction.Debate, delta);
        return true;
    }

    //b3 — kendini övdürme.
    private bool DoPraiseSelf()
    {
        var stats = GameStatManager.Instance;
        float gain = praiseReputation * Reach;
        stats.AddReputation(gain);
        stats.AddSuspicion(praiseSuspicion);

        OnMediaActionUsed?.Invoke(MediaAction.PraiseSelf, gain);
        return true;
    }

    //b8 — trendlere yön verme / konuyu dağıtma.
    //Gündemde ne varsa onu bastırıp yerine sıradan bir konu koyar. "Konuyu dağıtmak" budur.
    private bool DoSteerTrend()
    {
        var feed = SocialMediaManager.Instance;
        if (feed == null)
        {
            Debug.LogWarning("[MediaSystem] SocialMediaManager yok — trend çevrilemedi.");
            return false;
        }

        TopicType suppressed = feed.GetActiveTopic();
        feed.SetEventOverride(TopicType.General, steerRatio * Mathf.Clamp01(Reach / 2f),
                              suppressed, steerCounterRatio, steerSeconds);

        OnMediaActionUsed?.Invoke(MediaAction.SteerTrend, 0f);
        return true;
    }

    //b9 — siyasi propaganda. Gündemi siyasete çevirir, siyasi nüfuz kazandırır.
    private bool DoPropaganda()
    {
        var stats = GameStatManager.Instance;
        float mult = PayoffMultiplierFor(TopicType.Politics);
        float gain = propagandaInfluence * Reach * mult;

        stats.AddPoliticalInfluence(gain);
        stats.AddSuspicion(propagandaSuspicion);
        PushTopic(TopicType.Politics, propagandaRatio, propagandaSeconds);

        OnMediaActionUsed?.Invoke(MediaAction.Propaganda, gain);
        return true;
    }

    //b10 — şantaj / ifşa / hadise. Ağacın en pahalı ve en getirili hamlesi.
    private bool DoScandal()
    {
        var stats = GameStatManager.Instance;
        float mult = PayoffMultiplierFor(TopicType.Scandal);
        float gain = scandalInfluence * Reach * mult;

        stats.AddPoliticalInfluence(gain);
        stats.AddSuspicion(scandalSuspicion);
        PushTopic(TopicType.Scandal, scandalRatio, scandalSeconds);

        OnMediaActionUsed?.Invoke(MediaAction.Scandal, gain);
        return true;
    }

    // -------------------------------------------------------------------------
    // ANKETLER
    // -------------------------------------------------------------------------

    /// <summary>
    /// Anket yapar. Anket KESİN değer vermez — "kestirir": gerçek değerin etrafında
    /// ±hata paylı bir tahmin döner. Erişim arttıkça hata payı daralır.
    /// </summary>
    public bool RunPoll(PollKind kind)
    {
        if (!unlocked || !polls.Contains(kind))
        {
            Debug.LogWarning("[MediaSystem] Bu anket açılmamış: " + kind);
            return false;
        }
        var stats = GameStatManager.Instance;
        if (stats == null) return false;

        PollResult result = new PollResult { kind = kind };

        switch (kind)
        {
            case PollKind.Reputation:
                result.margin   = reputationPollMargin / Reach;
                result.estimate = Estimate(stats.Reputation, result.margin);
                result.summary  = $"İtibar tahmini: {result.estimate:F0} (±{result.margin:F0})";
                break;

            case PollKind.Suspicion:
                //"şüphe bir tık artar, şüphe öğrenilmiş olur" — anketin kendi bedeli.
                //Raw kullanılır: bu artış itibarla yumuşatılmaz, anketin sabit bedelidir.
                stats.AddSuspicionRaw(suspicionPollCost);
                result.margin   = suspicionPollMargin / Reach;
                result.estimate = Estimate(stats.Suspicion, result.margin);
                result.summary  = $"Şüphe tahmini: {result.estimate:F0} (±{result.margin:F0}) " +
                                  $"— anketin bedeli +{suspicionPollCost:F0} şüphe";
                break;

            case PollKind.PoliticalInfluence:
                result.margin   = influencePollMargin / Reach;
                result.estimate = Estimate(stats.PoliticalInfluence, result.margin);
                result.summary  = $"Siyasi nüfuz tahmini: {result.estimate:F0} (±{result.margin:F0})";
                break;

            case PollKind.FeedLeaning:
                var feed = SocialMediaManager.Instance;
                if (feed == null)
                {
                    Debug.LogWarning("[MediaSystem] SocialMediaManager yok — feed anketi yapılamadı.");
                    return false;
                }
                result.topic            = feed.GetActiveTopic();
                result.topicIsSensitive = feed.IsSensitiveTopic(result.topic);
                result.summary          = $"Feed yatkınlığı: {result.topic}" +
                                          (result.topicIsSensitive ? " — HASSAS KONU" : "");
                break;
        }

        LastPollResult = result;
        OnPollCompleted?.Invoke(result);
        Debug.Log("[Anket] " + result.summary);
        return true;
    }

    private static float Estimate(float trueValue, float margin)
    {
        return trueValue + UnityEngine.Random.Range(-margin, margin);
    }

    // -------------------------------------------------------------------------
    // YARDIMCILAR
    // -------------------------------------------------------------------------

    /// <summary>
    /// Haritanın baskın bölgesine karşılık gelen gündem konusu. b5 "konu mape göre seçilir"
    /// kuralı bu tablodan gelir; eşleştirme tasarım kararıdır, türetilmiş değildir.
    /// </summary>
    public TopicType TopicForDominantRegion()
    {
        if (CountryData.Instance == null) return TopicType.General;

        return CountryData.Instance.GetDominantRegion() switch
        {
            RegionType.Industrial   => TopicType.StockMarket,
            RegionType.Cities       => TopicType.RealEstate,
            RegionType.Agricultural => TopicType.Tax,
            RegionType.Urban        => TopicType.General, //el değmemiş doğa — özel bir gündemi yok
            _                       => TopicType.General
        };
    }

    /// <summary>Konu haritanın hassas konusuysa getiri katlanır.</summary>
    private float PayoffMultiplierFor(TopicType topic)
    {
        var feed = SocialMediaManager.Instance;
        if (feed != null && feed.IsSensitiveTopic(topic))
            return sensitiveTopicPayoffMultiplier;
        return 1f;
    }

    private void PushTopic(TopicType topic, float ratio, float seconds)
    {
        var feed = SocialMediaManager.Instance;
        if (feed == null) return;
        feed.SetEventOverride(topic, Mathf.Clamp01(ratio), seconds);
    }
}
