using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Minigames/WarForOil/Event")]
public class WarForOilEvent : ScriptableObject
{
    public string id;
    [TextArea(1, 3)] public string displayName;
    [TextArea(2, 8)] public string description;

    [Header("Geliştirici Notu")]
    [TextArea(3, 10)] public string devNote; //sadece Inspector'da görünür, oyuna etkisi yok

    [Header("Event Açıklaması")]
    [FormerlySerializedAs("skillNote")]
    [TextArea(3, 10)] public string eventNote; //geliştiriciler için event açıklama notu, oyuna etkisi yok

    [Range(0f, 1f)] public float minWarTime = 0f; //savaş süresinin yüzdesi olarak en erken tetiklenme (0.2 = %20, 300sn savaşta 60sn)
    [Range(-1f, 1f)] public float maxWarTime = -1f; //savaş süresinin yüzdesi olarak en geç tetiklenme (-1 = sınırsız, 0.8 = %80)
    public float decisionTime = 10f; //karar süresi (saniye)
    public bool isRepeatable; //aynı savaşta tekrar tetiklenebilir mi
    public bool isUnlimitedRepeat; //sınırsız tekrar (isRepeatable true ise)
    public int maxRepeatCount = 1; //en fazla kaç kez tekrar edebilir (isRepeatable true ve isUnlimitedRepeat false ise)
    public List<WarForOilEventChoice> choices;
    public int defaultChoiceIndex = -1; //süre dolunca otomatik seçilecek seçenek (-1 = ilk seçenek)

    [Header("Vandalizm Tetikleme")]
    public bool isVandalismEvent; //bu event tetiklendiğinde vandalizm seviyesi otomatik değişir
    public VandalismLevel vandalismLevelOnTrigger; //tetiklendiğinde atanacak vandalizm seviyesi

    [Header("Medya Takibi Tetikleme")]
    public bool isMediaPursuitEvent; //bu event tetiklendiğinde medya takibi seviyesi otomatik değişir
    public MediaPursuitLevel mediaPursuitLevelOnTrigger; //tetiklendiğinde atanacak medya takibi seviyesi

    [Header("Zincir Ayarları")]
    public ChainRole chainRole = ChainRole.None; //bu event zincirde mi (Head = zincir başlatıcı)

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
    public bool endsWarWithDeal; //savaşı anlaşmayla bitirir (garanti ödül)
    public float dealDelay; //anlaşma kaç saniye sonra savaşı bitirir (0 = anında)
    [Range(0f, 1f)] public float dealRewardRatio; //normal kazanımın bu oranı garanti verilir (0.8 = %80)
    public bool blocksEvents; //seçilirse savaş sonuna kadar yeni event gelmez
    [Range(0, 10)] public int eventBlockCycles; //seçilirse bu kadar event dönemi boyunca event gelmez (0 = etkisiz)
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

    //zincir dallanması — choice seçilince sıradaki chain event'in hangi havuzdan geleceğini belirler
    public ChainInfluenceStat chainInfluenceStat = ChainInfluenceStat.JustLuck; //dallanma seçimini etkileyen stat (JustLuck = stat yok)
    [Range(0f, 100f)] public float chainThreshold0 = 20f;  //1. eşik (0-t0 = aralık 0)
    [Range(0f, 100f)] public float chainThreshold1 = 50f;  //2. eşik (t0-t1 = aralık 1)
    [Range(0f, 100f)] public float chainThreshold2 = 75f;  //3. eşik (t1-t2 = aralık 2, t2-100 = aralık 3)
    public List<ChainBranch> chainBranches; //boşsa chain biter, doluysa dallanır
    public bool chainCanEnd; //true ise dallanma seçiminde chain'in bitme ihtimali de eklenir
    public float chainEndWeight = 1f; //chain bitme ağırlığı (dallanma ağırlıklarıyla yarışır)

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
