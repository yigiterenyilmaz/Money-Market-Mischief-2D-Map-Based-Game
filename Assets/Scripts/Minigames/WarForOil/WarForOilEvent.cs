using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Minigames/WarForOil/Event")]
public class WarForOilEvent : ScriptableObject
{
    public string id;
    [TextArea(1, 3)] public string displayName;
    [TextArea(2, 8)] public string description;
    public bool useTypewriterEffect; //true ise açıklama harf harf akar, false ise direkt paragraf olarak gösterilir

    [Header("Koşullu Açıklamalar")]
    public List<ConditionalDescription> conditionalDescriptions; //hikaye bayrağına göre değişen açıklamalar

    [Header("Geliştirici Notu")]
    [TextArea(3, 10)] public string devNote; //sadece Inspector'da görünür, oyuna etkisi yok

    [Header("Event Açıklaması")]
    [FormerlySerializedAs("skillNote")]
    [TextArea(3, 10)] public string eventNote; //geliştiriciler için event açıklama notu, oyuna etkisi yok

    [Range(0f, 1f)] public float minWarTime = 0f; //savaş süresinin yüzdesi olarak en erken tetiklenme (0.2 = %20, 300sn savaşta 60sn)
    [Range(-1f, 1f)] public float maxWarTime = -1f; //savaş süresinin yüzdesi olarak en geç tetiklenme (-1 = sınırsız, 0.8 = %80)
    public float decisionTime = 45f; //karar süresi (saniye)
    public bool isRepeatable; //aynı savaşta tekrar tetiklenebilir mi
    public bool isUnlimitedRepeat; //sınırsız tekrar (isRepeatable true ise)
    public int maxRepeatCount = 1; //en fazla kaç kez tekrar edebilir (isRepeatable true ve isUnlimitedRepeat false ise)
    public List<WarForOilEventChoice> choices;
    public int defaultChoiceIndex = -1; //süre dolunca otomatik seçilecek seçenek (-1 = ilk seçenek)

    [Header("Narrative")]
    public bool hasNarrative; //true ise event gösterildiğinde narrative metni de gösterilir
    [TextArea(5, 20)] public string narrative; //UI'da gösterilecek anlatı metni

    [Header("Vandalizm Tetikleme")]
    public bool isVandalismEvent; //bu event tetiklendiğinde vandalizm seviyesi otomatik değişir
    public VandalismLevel vandalismLevelOnTrigger; //tetiklendiğinde atanacak vandalizm seviyesi
    public bool startsVandalism; //true ise bu event vandalizm başlatıcı — vandalizm aktifken havuzdan çıkarılır
    public bool forcesVandalismStart; //true ise vandalizm aktifken bile gelir (startsVandalism filtresini yok sayar)

    [Header("Medya Takibi Tetikleme")]
    public bool isMediaPursuitEvent; //bu event tetiklendiğinde medya takibi seviyesi otomatik değişir
    public MediaPursuitLevel mediaPursuitLevelOnTrigger; //tetiklendiğinde atanacak medya takibi seviyesi

    [Header("Kadın Süreci")]
    public bool requiresBothProcessesActive; //true ise bu event sadece hem savaş hem kadın süreci aktifken tetiklenebilir
    public bool isWomanProcessEvent; //true ise bu event kadın süreci havuzlarında kullanılır
    public float minObsession = 0f; //bu event sadece obsesyon bu değerin üstündeyken gelir (0 = sınırsız)
    public float maxObsession = 100f; //bu event sadece obsesyon bu değerin altındayken gelir (100 = sınırsız)
    public List<WarForOilEvent> blockedWomanProcessEvents; //bu event tetiklenince havuzdan/zincirlerden çıkarılacak eventler

    //öncü event — kadın eventi tetiklenmeden önce bu event gösterilir, 4 saniye sonra asıl kadın eventi gelir
    public bool hasPrecursorEvent; //true ise bu kadın eventinin bir öncü eventi var
    public PrecursorEventType precursorEventType; //öncü eventin tipi
    public WarForOilEvent precursorWarEvent; //öncü war for oil eventi
    public Event precursorRandomEvent; //öncü random event

    [Header("Hikaye Bayrak Koşulları")]
    public List<StoryFlag> requiredStoryFlags; //bu event sadece bu bayraklar aktifken tetiklenebilir (hepsi gerekli)

    [Header("Zincir Ayarları")]
    public ChainRole chainRole = ChainRole.None; //bu event zincirde mi (Head = zincir başlatıcı)
    public bool blocksSubChainBranching; //true ise bu event tetiklendikten sonra başka zincirlerden dallanma hedefi olarak seçilemez
    public List<WarForOilEvent> alsoBlockedBranchEvents; //blocksSubChainBranching tetiklenince bu event'ler de dallanma hedefi olarak engellenir

    /// <summary>
    /// Aktif hikaye bayraklarına göre uygun açıklamayı döner.
    /// Koşullu açıklama varsa ve flag aktifse alternatif açıklama kullanılır (ilk eşleşen kazanır).
    /// Yoksa default description döner.
    /// </summary>
    public string GetDescription()
    {
        if (conditionalDescriptions != null && conditionalDescriptions.Count > 0 && StoryFlagManager.Instance != null)
        {
            for (int i = 0; i < conditionalDescriptions.Count; i++)
            {
                var cd = conditionalDescriptions[i];
                if (cd.requiredFlag != StoryFlag.None && StoryFlagManager.Instance.HasFlag(cd.requiredFlag))
                    return cd.alternativeDescription;
            }
        }
        return description;
    }

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
    public float reputationModifier; //itibar etkisi (pozitif = artar, negatif = düşer)
    public bool hasReputationFloor; //true ise itibar bu choice yüzünden belirli bir değerin altına düşmez
    public float reputationFloor; //itibarın düşemeyeceği minimum değer
    public float politicalInfluenceModifier; //politik nüfuz etkisi (negatif = düşürür)
    public int costModifier; //maliyet etkisi (savaş sonunda birikimli uygulanır)
    public float wealthModifier; //anlık para değişimi (pozitif = kazan, negatif = kaybet, seçildiğinde hemen uygulanır)
    public float cornerGrabModifier; //köşe kapma stat'ını etkiler (pozitif = bizim lehimize)
    public float protestModifier; //toplum tepkisi stat'ını etkiler (pozitif = tepki artar, negatif = azalır)
    [Range(0f, 1f)] public float protestTriggerChanceBonus; //protest tetiklenme şansına eklenen bonus (yarılanarak söner)
    public bool hasProtestChance; //true ise protestModifier yerine olasılık bazlı sistem kullanılır
    [Range(0f, 1f)] public float protestDecreaseChance; //azalma ihtimali (0-1)
    public float protestDecreaseAmount; //azalma miktarı (pozitif değer, otomatik çıkarılır)
    public float protestIncreaseAmount; //artma miktarı (pozitif değer, otomatik eklenir)

    //feed etkileri
    public bool freezesFeed; //seçilince sosyal medya feed'ini dondurur (SocialMediaManager.TryFreezeFeed)
    public bool slowsFeed; //seçilince sosyal medya feed'ini yavaşlatır (SocialMediaManager.TrySlowFeed)
    public bool hasFeedOverride; //feed'i belirli bir konuya yönlendirir (SocialMediaManager.SetEventOverride)
    public TopicType feedOverrideTopic; //yönlendirilecek konu
    [Range(0f, 1f)] public float feedOverrideRatio; //yönlendirme oranı (0-1, örn. 0.8 = %80)
    public bool hasCounterFeedTopic; //2. konu — istenmeyen konuları bastırmak için feed'e eklenir
    public TopicType counterFeedTopic; //counter konu
    [Range(0f, 1f)] public float counterFeedRatio; //counter konu oranı (0-1)
    public float feedOverrideDuration; //yönlendirme süresi (saniye, her iki topic için ortak)

    //diğer sonuçlar (Editor tarafından foldout içinde çizilir)
    public bool endsWar; //bu seçenek savaşı bitirir mi
    public float warEndDelay; //savaş kaç saniye sonra biter (0 = anında)
    public bool reducesReward; //ödülü düşürür mü
    [Range(0f, 1f)] public float baseRewardReduction; //base reward'ı bu oranda düşürür (0.3 = %30 düşüş)
    public bool winsWar; //savaşı direkt kazandırır (garanti zafer)
    public float winWarDelay; //kazanım kaç saniye sonra gerçekleşir (0 = anında)
    public bool winWarCustomReward; //true ise ödül oranı direkt girilir, false ise war support tabanlı hesaplanır
    [Range(0f, 1f)] public float winWarRewardRatio = 1f; //kazanım ödül oranı (winWarCustomReward true ise kullanılır)
    public bool endsWarWithDeal; //savaşı anlaşmayla bitirir (garanti ödül)
    public float dealDelay; //anlaşma kaç saniye sonra savaşı bitirir (0 = anında)
    [Range(0f, 1f)] public float dealRewardRatio; //normal kazanımın bu oranı garanti verilir (0.8 = %80)
    public bool blocksEvents; //seçilirse savaş sonuna kadar yeni event gelmez
    [Range(0, 10)] public int eventBlockCycles; //seçilirse bu kadar event dönemi boyunca savaş eventi gelmez (0 = etkisiz)
    [Range(0, 10)] public int globalEventBlockCycles; //seçilirse bu kadar event dönemi boyunca kadın eventleri HARİÇ tüm eventler durur (0 = etkisiz)
    public bool blocksCeasefire; //seçilirse savaş sonuna kadar ateşkes yapılamaz
    public bool blocksEventGroup; //seçilirse belirtilen gruptaki tüm eventler bir daha tetiklenmez
    public ScriptableObject blockedGroup; //engellenecek grup (WTETWCEventGroup veya OFPCEventGroup sürüklenebilir)

    //olasılıklı ödül düşürme (3 sonuç: event tekrar tetiklenir / ödül düşer / hiçbir şey olmaz)
    public bool hasProbabilisticRewardReduction;
    [Range(0f, 1f)] public float probRetriggerChance; //event tekrar tetiklenme şansı
    [Range(0f, 1f)] public float probRewardReductionChance; //ödül düşme şansı
    [Range(0f, 1f)] public float probRewardReductionAmount; //ödül düşme miktarı (0.3 = %30)

    //olasılıklı savaş bitirme (Inspector tarafından "Diğer Sonuçlar" foldout'unda çizilir)
    public bool hasProbabilisticWarEnd; //olasılık bazlı 3 sonuç: savaş biter / event yok olur / tekrar tetiklenir
    [Range(0f, 1f)] public float probWarEndChance; //savaş bitme olasılığı (support=50 için base değer)
    [Range(0f, 1f)] public float probDismissChance; //event yok olma olasılığı (support=50 için base değer)
    public float probWarEndDelay; //savaş biterse gecikme süresi (saniye)

    //zincir sayaç sistemi — zincir boyunca seçimleri takip eder
    public bool incrementsChainCounter; //bu choice seçilince zincir sayacını artırır
    public string chainCounterKey; //sayaç adı (ör. "acele", "yavasla")
    public int chainCounterIncrement = 1; //artış miktarı
    public bool hasEarlyChainTrigger; //sayaç eşiğe ulaşırsa zinciri atlayıp direkt bu event'e geç
    public int earlyTriggerThreshold; //erken tetikleme eşiği
    public WarForOilEvent earlyTriggerEvent; //erken tetiklenecek event

    //zincir arası tick etkisi — bir sonraki chain eventine kadar her event aralığında uygulanır
    public bool hasChainTickEffect; //true ise dallanma sonrası her event tick'inde stat etkisi uygulanır
    public ChainTickStatType chainTickStat; //etkilenecek stat
    public float chainTickAmount; //her tick'te uygulanacak miktar (pozitif = artır, negatif = azalt)

    //zincir dallanması — choice seçilince sıradaki chain event'in hangi havuzdan geleceğini belirler
    public ChainInfluenceStat chainInfluenceStat = ChainInfluenceStat.JustLuck; //dallanma seçimini etkileyen stat (JustLuck = stat yok)
    [Range(0f, 100f)] public float chainThreshold0 = 20f;  //1. eşik (0-t0 = aralık 0)
    [Range(0f, 100f)] public float chainThreshold1 = 50f;  //2. eşik (t0-t1 = aralık 1)
    [Range(0f, 100f)] public float chainThreshold2 = 75f;  //3. eşik (t1-t2 = aralık 2, t2-100 = aralık 3)
    public List<ChainBranch> chainBranches; //koşulsuz dallar — koşul sağlanmazsa veya koşullu dallanma yoksa buradan seçilir
    public bool chainCanEnd; //true ise dallanma seçiminde chain'in bitme ihtimali de eklenir
    public float chainEndWeight = 1f; //chain bitme ağırlığı (dallanma ağırlıklarıyla yarışır)
    public bool hasConditionalBranching; //true ise koşullu dallanma aktif
    public string branchCounterKey; //koşullu dallanma sayaç adı
    public int branchCounterMin; //koşullu dallanma minimum sayaç değeri (dahil)
    public int branchCounterMax = -1; //koşullu dallanma maksimum sayaç değeri (-1 = sınırsız)
    public List<ChainBranch> conditionalChainBranches; //koşullu dallar — koşul sağlanırsa buradan seçilir

    //rakip işgal flagleri (Editor tarafından foldout içinde çizilir)
    public bool acceptsRivalDeal; //rakip işgal anlaşmasını kabul eder
    public bool rejectsRivalDeal; //rakip işgal anlaşmasını reddeder → köşe kapma yarışı başlar

    //vandalizm etkileri (Editor tarafından foldout içinde çizilir)
    public bool affectsVandalism; //bu choice vandalizm seviyesini değiştirir mi
    public VandalismChangeType vandalismChangeType; //direkt atama mı göreceli mi
    public VandalismLevel vandalismTargetLevel; //direkt atama: hedef seviye
    public int vandalismLevelDelta; //göreceli değişim: +/- tık (Light=1, Moderate=2, Heavy=3, Severe=4)

    //medya takibi etkileri (Editor tarafından foldout içinde çizilir)
    public bool affectsMediaPursuit; //bu choice medya takibi seviyesini değiştirir mi
    public MediaPursuitChangeType mediaPursuitChangeType; //direkt atama mı göreceli mi
    public MediaPursuitLevel mediaPursuitTargetLevel; //direkt atama: hedef seviye
    public int mediaPursuitLevelDelta; //göreceli değişim: +/- tık (Low=1, Medium=2, High=3)

    //kadın süreci
    public bool startsWomanProcess; //seçilince kadın sürecini başlatır (oyun boyunca tek sefer)
    public bool endsWomanProcess; //seçilince kadın sürecini anında bitirir
    public float womanObsessionModifier; //kadın süreci stat'ını etkiler (pozitif = artar, negatif = azalır)
    public bool hasObsessionFloor; //true ise obsesyon bu choice yüzünden belirli bir değerin altına düşmez
    public float obsessionFloor; //obsesyonun düşemeyeceği minimum değer
    public bool redirectsWomanPool; //seçilince kadın süreci havuzunu başka bir database'e yönlendirir (kalıcı)
    public WomanProcessDatabase womanPoolDatabase; //yönlendirilecek database
    public bool freezesWomanProcess; //seçilince kadın sürecini belirli döngü sayısı kadar dondurur
    public int womanProcessFreezeCycles = 1; //kaç döngü boyunca kadın eventi gelmeyecek
    public bool hasObsessionDropLimit; //true ise bu choice seçildikten sonra obsesyon belirli miktar düşerse süreç biter
    public float obsessionDropLimit; //seçildiği andaki obsesyondan bu kadar düşerse kadın süreci otomatik sona erer

    //kalıcı stat çarpanları (seçildiğinde anında ve kalıcı uygulanır — tüm oyun boyunca geçerli)
    public List<PermanentMultiplierEntry> permanentMultipliers = new List<PermanentMultiplierEntry>();

    //dinamik stat tavanı — choice seçildiğinde belirli stat'ların tavanını düşürür veya kaldırır
    public List<StatCeilingEntry> statCeilingEffects; //tavan koy veya kaldır

    //anında tetiklenen event — choice seçildiğinde havuzdan biri gösterilir
    public bool hasImmediateEvent; //true ise seçildiğinde bir event tetiklenir
    [Range(0f, 15f)] public float immediateEventDelay; //tetikleme gecikmesi (0 = anında, saniye cinsinden)
    public bool immediateEventIsTiered; //true ise kadın obsesyon tier'ına göre farklı event seçilir
    public List<ImmediateEventEntry> immediateEventPool; //ağırlıklı event havuzu (tier'sız mod)
    public WarForOilEvent immediateEventTier1; //low obsesyon → bu event gelir
    public WarForOilEvent immediateEventTier2; //mid obsesyon → bu event gelir
    public WarForOilEvent immediateEventTier3; //high obsesyon → bu event gelir

    //hikaye bayrakları — bu choice seçildiğinde aktif edilen bayraklar
    public List<StoryFlag> setsStoryFlags;

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

public enum ChainRole
{
    None,   //normal event, zincir dışı
    Head    //zincirin başlangıç event'i — normal havuzdan tetiklenir, chain sürecini başlatır
}

/// <summary>
/// Zincir arası tick etkisinde kullanılacak stat tipi.
/// </summary>
public enum ChainTickStatType
{
    Support,            //savaş destek stat'ı (WarForOilManager internal)
    Suspicion,          //şüphe (GameStatManager)
    Reputation,         //itibar (GameStatManager)
    PoliticalInfluence  //politik nüfuz (GameStatManager)
}

/// <summary>
/// Dallanma stat seçimi. JustLuck seçilirse stat etkisi yok, sadece tek ağırlık kullanılır.
/// </summary>
public enum ChainInfluenceStat
{
    JustLuck,           //stat etkisi yok, saf olasılık
    Wealth,             //para stat'ına göre
    Suspicion,          //şüphe stat'ına göre
    Reputation,         //itibar stat'ına göre
    PoliticalInfluence  //politik nüfuz stat'ına göre
}

/// <summary>
/// Bir choice seçildiğinde sıradaki chain event'in olası hedeflerinden birini tanımlar.
/// JustLuck: sadece weightRange0 kullanılır.
/// Stat bazlı: stat'ın mevcut yüzdesine göre 4 aralıktan biri seçilir, o aralığın ağırlığı kullanılır.
/// </summary>
[System.Serializable]
public class ChainBranch
{
    public WarForOilEvent targetEvent;   //dallanmanın hedef eventi
    [Range(0f, 1f)] public float weightRange0; //aralık 0 ağırlığı (JustLuck'ta tek ağırlık)
    [Range(0f, 1f)] public float weightRange1; //aralık 1 ağırlığı
    [Range(0f, 1f)] public float weightRange2; //aralık 2 ağırlığı
    [Range(0f, 1f)] public float weightRange3; //aralık 3 ağırlığı
    public bool triggersAsImmediateEvent; //true ise zincir devamı yerine anında event olarak tetiklenir (zincir biter)
    [Range(0f, 15f)] public float immediateEventDelay; //anında event gecikmesi (0 = anında, saniye cinsinden)
}

/// <summary>
/// Vandalizm seviyesi. Light(1)-Severe(4) aktif seviyeler, altına düşerse Ended olur.
/// </summary>
public enum VandalismLevel
{
    None,       //vandalizm yok (başlamadı)
    Light,      //hafif (1)
    Moderate,   //orta (2)
    Heavy,      //ağır (3)
    Severe,     //şiddetli (4)
    Ended       //vandalizm bitti/bastırıldı
}

public enum VandalismChangeType
{
    Direct,     //direkt belirli bir seviyeye ata
    Relative    //mevcut seviyeyi +/- kaydır
}

/// <summary>
/// Medya takibi seviyesi. Low(1)-High(3) aktif seviyeler, altına düşerse Ended olur.
/// </summary>
public enum MediaPursuitLevel
{
    None,       //medya takibi yok (başlamadı)
    Low,        //düşük baskı (1)
    Medium,     //orta baskı (2)
    High,       //yüksek baskı (3)
    Ended       //medya takibi bitti/atlatıldı
}

public enum MediaPursuitChangeType
{
    Direct,     //direkt belirli bir seviyeye ata
    Relative    //mevcut seviyeyi +/- kaydır
}

/// <summary>
/// Kalıcı stat çarpanı girişi. Bir choice birden fazla stat'ı kalıcı olarak çarpabilir.
/// </summary>
[System.Serializable]
public class PermanentMultiplierEntry
{
    public PermanentMultiplierStatType stat;
    public float multiplier = 1f; //1.1 = %10 artış, 0.9 = %10 azalış
}

/// <summary>
/// Kalıcı çarpan için seçilebilir stat tipleri.
/// GameStatManager stat'ları + savaşa özel WarSupport.
/// </summary>
public enum PermanentMultiplierStatType
{
    Wealth,
    Suspicion,
    Reputation,
    PoliticalInfluence,
    WarSupport,
    WomanObsession
}

/// <summary>
/// Stat tavan işlem tipi.
/// </summary>
public enum StatCeilingMode
{
    Set,      //direkt değer ata
    Multiply, //mevcut tavanı çarpanla çarp (0-1 arası)
    Remove    //tavanı kaldır
}

/// <summary>
/// Dinamik stat tavanı girişi. Bir stat'ın tavanını düşürür, çarpanla değiştirir veya kaldırır.
/// </summary>
[System.Serializable]
public class StatCeilingEntry
{
    public StatType stat; //etkilenecek stat
    public StatCeilingMode mode; //işlem tipi
    public float ceilingValue; //tavan değeri (Set modunda kullanılır)
    [Range(0f, 1f)] public float ceilingMultiplier = 1f; //çarpan (Multiply modunda kullanılır)
}

/// <summary>
/// Kadın süreci eventlerinin öncü event tipi.
/// </summary>
public enum PrecursorEventType
{
    WarForOil,      //öncü event bir war for oil eventi (savaş yoksa ikisi de tetiklenmez)
    RandomEvent     //öncü event bir random event
}

/// <summary>
/// Anında tetiklenen event havuzu girişi. Ağırlığa göre rastgele seçilir.
/// </summary>
[System.Serializable]
public class ImmediateEventEntry
{
    public WarForOilEvent targetEvent; //tetiklenecek event
    [Range(0f, 100f)] public float weight = 50f; //seçilme yüzdesi (tüm girişlerin toplamı %100 olmalı)
}

/// <summary>
/// Koşullu açıklama girişi. Hikaye bayrağı aktifse default açıklama yerine alternatif gösterilir.
/// </summary>
[System.Serializable]
public class ConditionalDescription
{
    public StoryFlag requiredFlag; //bu bayrak aktifse alternatif açıklama kullanılır
    [TextArea(2, 8)] public string alternativeDescription; //bayrak aktifken gösterilecek açıklama
}
