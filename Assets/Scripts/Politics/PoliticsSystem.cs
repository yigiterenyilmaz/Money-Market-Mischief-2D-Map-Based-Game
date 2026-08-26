using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Siyaset ağacının aktif hamleleri.</summary>
public enum PoliticalAction
{
    GrassrootsMovement, //c2  — ufak siyasi hareketlere başlama
    DonateToParties,    //c5  — siyasi partilere bağış
    FundBackedParty,    //c7  — seçilen partiyi fonlama
    FundAllParties      //c8  — tüm partileri fonlama
}

/// <summary>Yetiştirilen kadronun yerleştirildiği alan.</summary>
public enum CadreTrack
{
    Holding, //c14 — holdinge adam gönderme (haraç → pasif gelir)
    Academy, //c15 — akademiye yönlendirme (siyasi nüfuz)
    Media    //c16 — medyaya yönlendirme (medya erişimi)
}

/// <summary>Kadro kaynağı — kadronun hangi hızda yetiştiği.</summary>
public enum CadreSource
{
    Scholarship, //c3  — burs programları
    YouthCamp    //c13 — gençlik kampları
}

/// <summary>Fonlanabilir bir siyasi parti. Şimdilik yalnızca isim + destek oranı.</summary>
[Serializable]
public class PoliticalParty
{
    public string name;
    [Range(0f, 1f)] public float support;
    public float fundedTotal;
}

/// <summary>
/// SİYASET SİSTEMİ — C ağacının rüşvet dışı kalan kısmının (c1–c3, c5, c7, c8, c13–c16, c21)
/// arkasındaki tek MonoBehaviour.
///
/// İki kol vardır:
///  1. NÜFUZ KOLU (c2/c5/c7/c8/c21). Para → siyasi nüfuz. Nüfuz kendi başına bir puan değil:
///     GameStatManager.GetSkillEfficiencyMultiplier() nüfuzu 0.5x–1.5x arası bir SKILL VERİM
///     çarpanına çevirir. c1'in "çarpan gibi çalışır" tarifi budur. O metot bu sınıf yazılana
///     kadar hiçbir yerden çağrılmıyordu — ilk tüketicisi burasıdır.
///  2. KADRO KOLU (c3/c13 → c14/c15/c16). Burs ve kamplar zamanla kadro yetiştirir; kadro
///     holdinge (haraç → pasif gelir), akademiye (nüfuz) veya medyaya (erişim) yerleştirilir.
///     Yani siyaset ağacı, medya ağacını c16 üzerinden besler.
///
/// RÜŞVET YOK: c4/c6/c10/c11/c12/c17/c18/c20 bilinçli olarak dışarıda. Hepsi henüz yazılmamış
/// rüşvet mekaniğine (beş sonuçlu: kabul/zincir/ifşa/paralel yapılanma/pazarlık) dayanıyor.
/// Bkz. Assets/Scripts/Media/media-politics-readme.md
///
/// UYARI — SUNUM YOK: parti seçimi ve kadro dağıtımı şu an otomatik yapılıyor, çünkü bunları
/// oyuncuya sunacak bir ekran yok. İkisi de aşağıda public API olarak duruyor, UI gelince
/// doğrudan bağlanabilir.
/// </summary>
public class PoliticsSystem : MonoBehaviour
{
    public static PoliticsSystem Instance { get; private set; }

    // -------------------------------------------------------------------------
    // AYARLAR
    // -------------------------------------------------------------------------

    [Header("Partiler")]
    [Tooltip("Oyun başında üretilen parti sayısı.")]
    [Range(2, 6)] public int partyCount = 3;

    [Header("c2 — Ufak Siyasi Hareketler")]
    public float movementCost = 10_000f;
    public float movementInfluence = 4f;
    public float movementReputation = 1.5f;

    [Header("c5 — Siyasi Partilere Bağış")]
    public float donationCost = 50_000f;
    public float donationInfluence = 8f;
    [Tooltip("Bağış tamamen temiz değildir: küçük bir şüphe bırakır.")]
    public float donationSuspicion = 0.5f;

    [Header("c7 — Seçilen Partiyi Fonlama")]
    [Tooltip("Tek partiye yüklenmek nüfuzu daha verimli üretir.")]
    public float backedPartyMultiplier = 1.6f;

    [Header("c8 — Tüm Partileri Fonlama")]
    [Tooltip("Herkesi fonlamak lira başına daha az nüfuz verir ama kimseye borçlu olmazsın.")]
    public float fundAllMultiplier = 1.15f;
    [Tooltip("Tüm partileri fonlarken maliyet parti sayısıyla çarpılır.")]
    public float fundAllCostPerParty = 40_000f;
    [Tooltip("Herkesle iyi geçinmek şüphe bırakmaz — tersine uzlaşmacı görünürsün.")]
    public float fundAllSuspicion = 0f;

    [Header("c21 — Konsensüs İnşası")]
    [Tooltip("Konsensüs kalıcıdır: siyasi nüfuz kazanımını bu oranda büyütür.")]
    public float consensusInfluenceMultiplier = 1.25f;
    [Tooltip("Konsensüs kalıcıdır: şüphe kazanımını bu oranda kısar (0.9 = %10 az).")]
    public float consensusSuspicionMultiplier = 0.9f;

    [Header("Kadro Üretimi (c3, c13)")]
    [Tooltip("Burs programlarının dakikada yetiştirdiği kadro.")]
    public float scholarshipCadrePerMinute = 0.5f;
    [Tooltip("Gençlik kamplarının dakikada eklediği kadro.")]
    public float youthCampCadrePerMinute = 1.0f;
    [Tooltip("Ülkenin eğitim seviyesi kadro hızını ölçekler (50 = nötr).")]
    public bool scaleCadreByEducation = true;

    [Header("Kadro Yerleşimi (c14, c15, c16)")]
    [Tooltip("Holdingdeki her kadronun saniyede getirdiği haraç.")]
    public float holdingIncomePerCadre = 15f;
    [Tooltip("Akademideki her kadronun saniyede kattığı siyasi nüfuz.")]
    public float academyInfluencePerCadre = 0.02f;
    [Tooltip("Medyadaki her kadronun kattığı erişim.")]
    public float mediaReachPerCadre = 0.03f;
    [Tooltip("Kadrodan gelen medya erişiminin tavanı — ağaç tek koldan patlamasın.")]
    public float mediaReachCap = 0.6f;

    // -------------------------------------------------------------------------
    // DURUM
    // -------------------------------------------------------------------------

    private bool unlocked;
    private readonly HashSet<PoliticalAction> actions = new HashSet<PoliticalAction>();
    private readonly HashSet<CadreSource> cadreSources = new HashSet<CadreSource>();
    private readonly HashSet<CadreTrack> openTracks = new HashSet<CadreTrack>();

    private readonly List<PoliticalParty> parties = new List<PoliticalParty>();
    private int backedPartyIndex = -1;
    private bool consensusBuilt;

    //kadro sayacı — tam sayıya ulaştıkça bir kadro yerleşir
    private float cadreProgress;
    private readonly Dictionary<CadreTrack, int> placedCadres = new Dictionary<CadreTrack, int>();
    private int nextTrackCursor; //otomatik dağıtım için sıra imleci
    private float grantedMediaReach;

    public bool IsUnlocked => unlocked;
    public IReadOnlyList<PoliticalParty> Parties => parties;
    public int BackedPartyIndex => backedPartyIndex;
    public bool ConsensusBuilt => consensusBuilt;
    public int GetPlacedCadres(CadreTrack track) => placedCadres.TryGetValue(track, out int n) ? n : 0;
    public float CadreProgress => cadreProgress;

    public static event Action OnPoliticsUnlocked;
    public static event Action<int> OnPartyBacked;              //parti index'i
    public static event Action<PoliticalAction, float> OnPoliticalActionUsed; //hamle, nüfuz etkisi
    public static event Action<CadreTrack, int> OnCadrePlaced;  //alan, o alandaki yeni toplam
    public static event Action OnConsensusBuilt;

    private void Awake()
    {
        //paylaşılan Managers objesi — Destroy(gameObject) bütün manager'ları götürürdü
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!unlocked) return;
        TickCadres(Time.deltaTime);
        TickPlacedCadres(Time.deltaTime);
    }

    // -------------------------------------------------------------------------
    // AÇILIŞ
    // -------------------------------------------------------------------------

    /// <summary>c1 — siyaset ağacının kökü.</summary>
    public void Unlock()
    {
        if (unlocked) return;
        unlocked = true;
        EnsureParties();
        OnPoliticsUnlocked?.Invoke();
    }

    public void EnableAction(PoliticalAction action)
    {
        Unlock();
        actions.Add(action);

        //c7 açıldığında oyuncunun desteklediği parti belirlenmiş olmalı. Seçim ekranı
        //olmadığı için en güçlü parti otomatik seçilir — UI gelince BackParty() çağrılsın.
        if (action == PoliticalAction.FundBackedParty && backedPartyIndex < 0)
            BackStrongestParty();
    }

    public void EnableCadreSource(CadreSource source)
    {
        Unlock();
        cadreSources.Add(source);
    }

    public void OpenCadreTrack(CadreTrack track)
    {
        Unlock();
        if (openTracks.Add(track) && !placedCadres.ContainsKey(track))
            placedCadres[track] = 0;
    }

    /// <summary>c21 — konsensüs. Kalıcı çarpanları bir kez uygular.</summary>
    public void BuildConsensus()
    {
        if (consensusBuilt) return;
        consensusBuilt = true;

        var stats = GameStatManager.Instance;
        if (stats != null)
        {
            stats.ApplyPermanentGainMultiplier(StatType.PoliticalInfluence, consensusInfluenceMultiplier);
            stats.ApplyPermanentGainMultiplier(StatType.Suspicion, consensusSuspicionMultiplier);
        }
        OnConsensusBuilt?.Invoke();
    }

    // -------------------------------------------------------------------------
    // PARTİLER
    // -------------------------------------------------------------------------

    private void EnsureParties()
    {
        if (parties.Count > 0) return;

        //İsimler yer tutucudur; parti kimliği (ideoloji, tavır) henüz tasarlanmadı.
        string[] names = { "Merkez Parti", "Halkçı Parti", "Milli Blok", "Reform Hareketi", "Yeşil Yol", "Ulusal Birlik" };
        int n = Mathf.Clamp(partyCount, 2, names.Length);

        float total = 0f;
        for (int i = 0; i < n; i++)
        {
            float support = UnityEngine.Random.Range(0.15f, 1f);
            total += support;
            parties.Add(new PoliticalParty { name = names[i], support = support });
        }
        //destekleri normalize et ki toplamları 1 olsun
        for (int i = 0; i < parties.Count; i++)
            parties[i].support /= total;
    }

    /// <summary>UI gelince doğrudan bağlanacak seçim kapısı.</summary>
    public bool BackParty(int index)
    {
        EnsureParties();
        if (index < 0 || index >= parties.Count) return false;

        backedPartyIndex = index;
        OnPartyBacked?.Invoke(index);
        return true;
    }

    private void BackStrongestParty()
    {
        EnsureParties();
        int best = 0;
        for (int i = 1; i < parties.Count; i++)
            if (parties[i].support > parties[best].support) best = i;

        BackParty(best);
        Debug.Log($"[PoliticsSystem] Desteklenen parti otomatik seçildi: {parties[best].name} " +
                  "(seçim ekranı yok — BackParty() ile değiştirilebilir).");
    }

    // -------------------------------------------------------------------------
    // HAMLELER
    // -------------------------------------------------------------------------

    /// <summary>Aktif yeteneklerin tek giriş kapısı.</summary>
    public bool PerformAction(PoliticalAction action)
    {
        if (!unlocked)
        {
            Debug.LogWarning("[PoliticsSystem] Siyaset sistemi açık değil — hamle yapılamadı: " + action);
            return false;
        }
        if (!actions.Contains(action))
        {
            Debug.LogWarning("[PoliticsSystem] Bu hamle açılmamış: " + action);
            return false;
        }
        if (GameStatManager.Instance == null) return false;

        switch (action)
        {
            case PoliticalAction.GrassrootsMovement: return DoMovement();
            case PoliticalAction.DonateToParties:    return DoDonate();
            case PoliticalAction.FundBackedParty:    return DoFundBacked();
            case PoliticalAction.FundAllParties:     return DoFundAll();
            default: return false;
        }
    }

    //c2 — ufak siyasi hareket. Ucuz, temiz, küçük.
    private bool DoMovement()
    {
        var stats = GameStatManager.Instance;
        if (!stats.TrySpendWealth(movementCost)) return false;

        float gain = movementInfluence * Efficiency;
        stats.AddPoliticalInfluence(gain);
        stats.AddReputation(movementReputation);

        OnPoliticalActionUsed?.Invoke(PoliticalAction.GrassrootsMovement, gain);
        return true;
    }

    //c5 — genel bağış. Kime verdiğin belli değil, o yüzden verimi düşük ama kapısı açık.
    private bool DoDonate()
    {
        var stats = GameStatManager.Instance;
        if (!stats.TrySpendWealth(donationCost)) return false;

        float gain = donationInfluence * Efficiency;
        stats.AddPoliticalInfluence(gain);
        stats.AddSuspicion(donationSuspicion);
        SpreadFunding(donationCost);

        OnPoliticalActionUsed?.Invoke(PoliticalAction.DonateToParties, gain);
        return true;
    }

    //c7 — tek partiye yüklenmek. Aynı para, daha çok nüfuz.
    private bool DoFundBacked()
    {
        var stats = GameStatManager.Instance;
        if (backedPartyIndex < 0) BackStrongestParty();
        if (!stats.TrySpendWealth(donationCost)) return false;

        //desteklenen partinin halk desteği ne kadar yüksekse fon o kadar işe yarar
        float supportFactor = 0.5f + parties[backedPartyIndex].support;
        float gain = donationInfluence * backedPartyMultiplier * supportFactor * Efficiency;

        stats.AddPoliticalInfluence(gain);
        stats.AddSuspicion(donationSuspicion);
        parties[backedPartyIndex].fundedTotal += donationCost;

        OnPoliticalActionUsed?.Invoke(PoliticalAction.FundBackedParty, gain);
        return true;
    }

    //c8 — herkesi fonlamak. Pahalı, verimi düşük, ama şüphe bırakmaz ve konsensüse zemin hazırlar.
    private bool DoFundAll()
    {
        var stats = GameStatManager.Instance;
        EnsureParties();

        float cost = fundAllCostPerParty * parties.Count;
        if (!stats.TrySpendWealth(cost)) return false;

        float gain = donationInfluence * fundAllMultiplier * Efficiency;
        stats.AddPoliticalInfluence(gain);
        if (fundAllSuspicion != 0f) stats.AddSuspicion(fundAllSuspicion);
        SpreadFunding(cost);

        OnPoliticalActionUsed?.Invoke(PoliticalAction.FundAllParties, gain);
        return true;
    }

    private void SpreadFunding(float amount)
    {
        EnsureParties();
        float share = amount / parties.Count;
        for (int i = 0; i < parties.Count; i++)
            parties[i].fundedTotal += share;
    }

    /// <summary>
    /// Siyasi nüfuzun kendi getirisine geri beslemesi. c1'in "çarpan gibi" tarifi:
    /// nüfuz yükseldikçe siyaset hamleleri daha çok nüfuz üretir.
    /// </summary>
    private float Efficiency =>
        GameStatManager.Instance != null ? GameStatManager.Instance.GetSkillEfficiencyMultiplier() : 1f;

    // -------------------------------------------------------------------------
    // KADRO
    // -------------------------------------------------------------------------

    private void TickCadres(float dt)
    {
        if (cadreSources.Count == 0 || openTracks.Count == 0) return;

        float perMinute = 0f;
        if (cadreSources.Contains(CadreSource.Scholarship)) perMinute += scholarshipCadrePerMinute;
        if (cadreSources.Contains(CadreSource.YouthCamp))   perMinute += youthCampCadrePerMinute;

        if (scaleCadreByEducation && CountryData.Instance != null)
        {
            //eğitim 50 nötr; 0'da yarı hız, 100'de bir buçuk kat
            perMinute *= Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(CountryData.Instance.EducationIndex / 100f));
        }

        cadreProgress += perMinute * dt / 60f;
        while (cadreProgress >= 1f)
        {
            cadreProgress -= 1f;
            PlaceNextCadre();
        }
    }

    //Açık alanlara sırayla dağıtır. Oyuncuya seçtirecek ekran yok — UI gelince
    //PlaceCadre(track) doğrudan çağrılabilir.
    private void PlaceNextCadre()
    {
        if (openTracks.Count == 0) return;

        var tracks = new List<CadreTrack>(openTracks);
        tracks.Sort(); //Set sırası garanti değil; dağıtım deterministik olsun
        CadreTrack track = tracks[nextTrackCursor % tracks.Count];
        nextTrackCursor++;
        PlaceCadre(track);
    }

    /// <summary>Bir kadroyu belirtilen alana yerleştirir.</summary>
    public void PlaceCadre(CadreTrack track)
    {
        if (!openTracks.Contains(track)) return;

        placedCadres.TryGetValue(track, out int count);
        count++;
        placedCadres[track] = count;

        //medya kadrosu anlık bir gelir değil, kalıcı erişim katkısıdır — yerleşince bir kez verilir
        if (track == CadreTrack.Media) GrantMediaReach();

        OnCadrePlaced?.Invoke(track, count);
    }

    private void GrantMediaReach()
    {
        if (MediaSystem.Instance == null) return;

        float target = Mathf.Min(GetPlacedCadres(CadreTrack.Media) * mediaReachPerCadre, mediaReachCap);
        float delta = target - grantedMediaReach;
        if (delta <= 0f) return;

        grantedMediaReach = target;
        MediaSystem.Instance.AddExternalReach(delta);
    }

    private void TickPlacedCadres(float dt)
    {
        var stats = GameStatManager.Instance;
        if (stats == null) return;

        int holding = GetPlacedCadres(CadreTrack.Holding);
        if (holding > 0 && holdingIncomePerCadre != 0f)
            stats.AddWealth(holding * holdingIncomePerCadre * dt);

        int academy = GetPlacedCadres(CadreTrack.Academy);
        if (academy > 0 && academyInfluencePerCadre != 0f)
            stats.AddPoliticalInfluence(academy * academyInfluencePerCadre * dt);
    }
}
