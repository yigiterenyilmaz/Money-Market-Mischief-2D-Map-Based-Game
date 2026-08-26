using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// TARIM EKONOMİSİ — ekin deposu, tavuk zinciri, stokçuluk ve zehirleme (a26–a28, a31).
///
/// ÇEKİRDEK (a26): tarım bölgesine depo kurulur, çevresindeki EKİLİ tarlalardan ürün
/// biriktirir, oyuncu stoğu dalgalanan piyasa fiyatından satar.
///
///   1. a26 açılır → UnlockCropDepotEffect sistemi yetkilendirir.
///   2. Oyuncu a26 node'una tıklar → EnterCropDepotPlacementEffect yerleştirme modunu açar.
///   3. Tarım bölgesindeki (biyom 4) bir kareye tıklanır; ücret ödenir, depo kurulur.
///   4. Depo, yarıçapı içindeki ekili tile sayısıyla orantılı hızda üretir, kapasitede durur.
///   5. Oyuncu istediği an satar.
///
/// Yer seçimi gerçek bir karardır: verim de kapasite de çevredeki ekili tile sayısından
/// türer (MapDecorPlacer.CountCropTilesAround), yani tarlanın ortası kenarından iyidir.
///
/// DALIN İKİ KOLU AYNI STOĞUN ÜSTÜNDE YARIŞIR — tasarımın çekirdeği budur:
///   * a27 "los pollos hermanos" — tavuk zinciri stoğu her tick GARANTİLİ ama piyasa
///     altı bir fiyattan alır ve kirli parayı akladığı için şüpheyi DÜŞÜRÜR. Güvenli kol.
///   * a28 "stokçuluk + talep dalgası" — kapasiteyi büyütür ve stok ne kadar doluysa o
///     kadar sert bir fiyat zirvesi yaratır (sosyal medyada akım → feed'e düşer), ama
///     şüphe ekler. Açgözlü kol: satmayı geciktirdikçe kazanç büyür.
///   * a31 "zehirleme" — üretimi kalıcı olarak hızlandırır, karşılığında sürekli şüphe
///     ve tek seferlik itibar kaybı.
/// İki kol aynı anda alınabilir ama birbirini yer: zincir stoğu sürekli boşalttığı için
/// dalga sırasında elde tutulan stok azalır.
///
/// Bu sınıf girdi + mantık taşır; EKRAN YOKTUR — bkz. crop-depot-readme.md.
/// </summary>
public class CropDepotSystem : MonoBehaviour
{
    public static CropDepotSystem Instance { get; private set; }

    public enum Phase { Idle, Placing }

    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    public Camera mapCamera;

    [Header("Kurulum")]
    [Tooltip("Bir deponun kurulum bedeli.")]
    public float depotCost = 15_000f;
    [Tooltip("Deponun ürün topladığı yarıçap (tile). Verim ve kapasite bu daire içindeki " +
             "EKİLİ tile sayısından hesaplanır.")]
    [Range(4, 120)] public int collectionRadiusTiles = 28;
    [Tooltip("İki depo arası en az mesafe (tile). Aynı tarlaya üst üste depo dizip geliri " +
             "katlamayı engeller.")]
    [Range(0, 200)] public int minDepotSpacingTiles = 24;
    [Tooltip("En fazla kaç depo kurulabilir. 0 = sınırsız.")]
    public int maxDepots = 6;
    [Tooltip("Yarıçap içinde en az bu kadar ekili tile yoksa depo kurulamaz — çorak araziye " +
             "depo dikip bedava bina sahibi olmayı engeller.")]
    public int minCropTilesToBuild = 40;

    [Header("Üretim")]
    [Tooltip("Ekili tile başına saniyede üretilen ürün birimi.")]
    public float yieldPerCropTilePerSecond = 0.015f;
    [Tooltip("Ekili tile başına depo kapasitesi (ürün birimi).")]
    public float capacityPerCropTile = 4f;
    [Tooltip("Kapasite bu değerin altına inmez.")]
    public float minCapacity = 250f;
    [Tooltip("Kapasite bu değerin üstüne çıkmaz.")]
    public float maxCapacity = 6000f;

    [Header("Piyasa")]
    [Tooltip("Ürün biriminin temel fiyatı. Piyasa bunun etrafında dalgalanır.")]
    public float basePricePerUnit = 14f;
    [Tooltip("Fiyatın inebileceği en düşük oran (temel fiyatın katı).")]
    [Range(0.1f, 1f)] public float priceMinRatio = 0.65f;
    [Tooltip("Fiyatın çıkabileceği en yüksek oran (temel fiyatın katı).")]
    [Range(1f, 4f)] public float priceMaxRatio = 1.55f;
    [Tooltip("Fiyatın kaç saniyede bir güncellendiği.")]
    [Range(0.5f, 30f)] public float priceTickSeconds = 6f;
    [Tooltip("Her adımda fiyatın değişebileceği en büyük oran.")]
    [Range(0.005f, 0.3f)] public float priceStepRatio = 0.05f;
    [Tooltip("Fiyatın temel değere geri çekilme kuvveti. 0 = serbest sürüklenir, " +
             "1 = anında merkeze döner.")]
    [Range(0f, 1f)] public float priceMeanReversion = 0.08f;

    [Header("a27 — Tavuk Zinciri (Los Pollos Hermanos)")]
    [Tooltip("Zincirin her tick'te satın aldığı stok oranı.")]
    [Range(0.01f, 1f)] public float chainPurchaseRatio = 0.25f;
    [Tooltip("Zincirin ödediği fiyat, TEMEL fiyatın katı. Piyasanın altında olmalı — " +
             "zincirin cazibesi yüksek fiyat değil, GARANTİ olması. Talep dalgasından " +
             "etkilenmez: elde tutup zirvede satmak hep daha kârlıdır.")]
    [Range(0.1f, 1.5f)] public float chainPriceRatio = 0.85f;
    [Tooltip("Zincirin alım yaptığı her tick'te düşen şüphe. Meşru vitrin kirli parayı aklar.")]
    public float chainSuspicionPerTick = 0.4f;
    [Tooltip("Zincir kaç saniyede bir alım yapar.")]
    public float chainTickSeconds = 10f;

    [Header("a28 — Stokçuluk ve Talep Dalgası")]
    [Tooltip("Stokçuluk açılınca kapasitenin çarpıldığı kat. Dalgayı beklemek için " +
             "depolayacak yer gerekir.")]
    public float hoardingCapacityMultiplier = 2.5f;
    [Tooltip("Talep dalgasının ulaşabileceği en yüksek fiyat çarpanı (stok doluyken).")]
    public float demandSpikeMaxMultiplier = 2.2f;
    [Tooltip("Bu kadar stok elde tutulunca dalga TAM etkisini gösterir. Altında etki " +
             "oransal olarak zayıflar — 'stokçuluk' mekaniği burada.")]
    public float demandSpikeFullStockUnits = 3000f;
    [Tooltip("Dalganın sürdüğü saniye.")]
    public float demandSpikeSeconds = 45f;
    [Tooltip("Dalga başlatmanın şüphe bedeli.")]
    public float demandSpikeSuspicion = 6f;
    [Tooltip("Feed'de öne çıkarılan konu. PostDatabase'de o konuda gönderi yoksa akım " +
             "başlar ama feed'de görünmez.")]
    public TopicType demandSpikeTopic = TopicType.General;

    [Header("a31 — Zehirleme")]
    [Tooltip("Zehirleme sonrası üretim hızının çarpıldığı kat.")]
    public float poisonYieldMultiplier = 1.75f;
    [Tooltip("Zehirli üretim sürerken her tick eklenen şüphe.")]
    public float poisonSuspicionPerTick = 1.2f;
    [Tooltip("Zehirleme kaç saniyede bir şüphe ekler.")]
    public float poisonTickSeconds = 10f;
    [Tooltip("Zehirlemenin tek seferlik itibar bedeli.")]
    public float poisonReputationHit = 12f;

    [Header("a32 — Yumaklı File (Aldatıcı Ambalaj)")]
    [Tooltip("DİKKAT: a32'nin notu (\"yumaklı files\") çözülemedi; bu, konumundan çıkarılmış " +
             "bir YORUMDUR — file içinde ürünü kabartıp dolu göstermek. Yanlışsa yalnızca bu " +
             "iki sayı ve efekt değişir. Oyuncunun ELDEN yaptığı satışlarda birim fiyatın " +
             "çarpıldığı kat.")]
    public float packagingSaleMultiplier = 1.35f;
    [Tooltip("Aldatıcı ambalajla yapılan her satışın itibar bedeli.")]
    public float packagingReputationPerSale = 1.5f;

    [Header("Etkileşim")]
    [Tooltip("Basıp bırakma arası bu kadar pikselden az hareket varsa tıklama sayılır " +
             "(fazlası kamera sürüklemesidir).")]
    public float clickMoveThreshold = 8f;

    [Header("Görsel")]
    [Tooltip("Depo sprite'ı. Boş bırakılırsa pixel-art bir yer tutucu üretilir.")]
    public Sprite depotSprite;
    [Tooltip("Sprite ölçeği.")]
    public float depotScale = 0.6f;
    [Tooltip("Sprite'ın z konumu — haritanın önünde durması için negatif.")]
    public float depotZ = -0.6f;

    /// <summary>Kurulmuş bir deponun oyuncuya sunulan özeti.</summary>
    public struct DepotInfo
    {
        public int        index;
        public Vector2Int tile;
        public Vector3    worldPosition;
        public int        cropTiles;      //yarıçap içindeki ekili tile
        public float      ratePerSecond;  //saniyede üretilen ürün
        public float      capacity;
        public float      stock;
        public float      stockValue;     //stock × güncel fiyat
        public float      totalSold;      //bugüne kadar satılan ürün
    }

    private class Depot
    {
        public Vector2Int  tile;
        public Vector3     worldPosition;
        public GameObject  go;
        public int         cropTiles;
        public float       ratePerSecond;
        public float       capacity;
        public float       stock;
        public float       totalSold;
    }

    private readonly List<Depot> depots = new List<Depot>();

    private bool    unlocked;
    private Phase   phase = Phase.Idle;
    private Vector2 pressScreenPos;
    private bool    pressed;

    //piyasa: marketPrice serbest yürüyüş, spikeMultiplier a28 dalgası. Satış fiyatı ikisinin çarpımı.
    private float marketPrice;
    private float priceTimer;
    private float spikeMultiplier = 1f;
    private float spikeRemaining;

    private bool  chainUnlocked;
    private float chainTimer;
    private float chainTotalUnits;
    private float chainTotalRevenue;

    private bool  hoardingUnlocked;

    private bool  poisoned;
    private float poisonTimer;

    private bool  packagingUnlocked;

    private static Sprite placeholderSprite;

    //events
    public static event Action              OnUnlocked;
    public static event Action<bool>        OnPlacementModeChanged; //mod açık mı
    public static event Action<DepotInfo>   OnDepotPlaced;
    public static event Action<float>       OnPriceChanged;         //yeni birim fiyat (dalga dahil)
    /// <summary>satılan ürün, kazanılan para</summary>
    public static event Action<float, float> OnSold;
    /// <summary>yetersiz para — gereken tutar</summary>
    public static event Action<float>       OnInsufficientFunds;
    /// <summary>depo kurulamadı — sebep metni</summary>
    public static event Action<string>      OnPlacementRejected;

    public static event Action              OnPoultryChainUnlocked;
    /// <summary>zincirin bu tick'te aldığı ürün, ödediği para</summary>
    public static event Action<float, float> OnPoultryChainTick;
    public static event Action              OnHoardingUnlocked;
    /// <summary>fiyat çarpanı, süre (saniye)</summary>
    public static event Action<float, float> OnDemandSpikeStarted;
    public static event Action              OnDemandSpikeEnded;
    public static event Action              OnCropsPoisoned;
    public static event Action              OnDeceptivePackagingUnlocked;

    public bool  IsUnlocked      => unlocked;
    public bool  IsPlacementMode => phase == Phase.Placing;
    public int   DepotCount      => depots.Count;

    /// <summary>Ürünün şu anki satış fiyatı — piyasa yürüyüşü × talep dalgası.</summary>
    public float PricePerUnit    => marketPrice * spikeMultiplier;
    /// <summary>Fiyatın temel değere oranı — UI'da "piyasa yüksek/düşük" göstergesi için.</summary>
    public float PriceRatio      => basePricePerUnit > 0f ? PricePerUnit / basePricePerUnit : 1f;

    public bool  IsPoultryChainUnlocked => chainUnlocked;
    public bool  IsHoardingUnlocked     => hoardingUnlocked;
    public bool  IsPoisoned             => poisoned;
    public bool  IsDemandSpikeActive    => spikeRemaining > 0f;
    public float DemandSpikeRemaining   => Mathf.Max(0f, spikeRemaining);
    public float DemandSpikeMultiplier  => spikeMultiplier;
    public float PoultryChainTotalUnits   => chainTotalUnits;
    public float PoultryChainTotalRevenue => chainTotalRevenue;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            //SADECE bu bileşeni yok et, GameObject'i DEĞİL. Managers gibi paylaşılan bir
            //nesnede duruyorsa tüm nesneyi silmek SkillTreeManager dahil oradaki her şeyi
            //götürür ve oyun sessizce çalışmaz hale gelir.
            Debug.LogWarning("[CropDepotSystem] Sahnede birden fazla kopya var — fazlası kaldırıldı.", this);
            Destroy(this);
            return;
        }

        Instance    = this;
        marketPrice = basePricePerUnit;
    }

    private void OnEnable()  { MapDecorPlacer.OnDecorPlaced += HandleDecorPlaced; }
    private void OnDisable() { MapDecorPlacer.OnDecorPlaced -= HandleDecorPlaced; }

    /// <summary>UnlockCropDepotEffect tarafından çağrılır.</summary>
    public void Unlock()
    {
        if (unlocked) return;

        unlocked = true;
        OnUnlocked?.Invoke();
    }

    // -------------------------------------------------------------------------
    // MOD KONTROLÜ
    // -------------------------------------------------------------------------

    /// <summary>EnterCropDepotPlacementEffect tarafından çağrılır (a26 aktif yeteneği).</summary>
    public void EnterPlacementMode()
    {
        if (!unlocked) return;
        if (phase == Phase.Placing) return;

        //bölge çizimi sürerken aynı sol tıklama iki sistemi birden tetiklemesin
        if (RegionConversionSystem.Instance != null && RegionConversionSystem.Instance.IsModeActive)
        {
            OnPlacementRejected?.Invoke("Önce bölge dönüşümünü bitirin.");
            return;
        }

        phase   = Phase.Placing;
        pressed = false;

        //skill ağacı tam ekran bir paneldir ve açıkken kamera kilitlidir. Mod ağaçtaki
        //node'a tıklanarak açıldığı için paneli kapatma işini burada üstleniyoruz —
        //RegionConversionSystem.EnterMode ile aynı sözleşme. Bu çağrı olmadan oyuncu
        //haritayı ne görebilir ne de tıklayabilir.
        if (UImanager.Instance != null) UImanager.Instance.SetMapToolMode(true);

        //Bölge çiziminin aksine oyunu DURDURMUYORUZ: yerleştirme tek bir tıklama, ve
        //oyuncunun uygun tarlayı ararken haritada gezinmesi gerekiyor.
        OnPlacementModeChanged?.Invoke(true);
    }

    public void CancelPlacementMode()
    {
        if (phase != Phase.Placing) return;

        phase   = Phase.Idle;
        pressed = false;

        if (UImanager.Instance != null) UImanager.Instance.SetMapToolMode(false);

        OnPlacementModeChanged?.Invoke(false);
    }

    // -------------------------------------------------------------------------
    // DÖNGÜ
    // -------------------------------------------------------------------------

    private void Update()
    {
        TickPrice();
        TickDemandSpike();
        TickProduction();
        TickPoultryChain();
        TickPoison();

        if (phase == Phase.Placing) UpdatePlacementInput();
    }

    /// <summary>
    /// Piyasayı merkeze çekilen rastgele yürüyüşle günceller: küçük adımlar + temel fiyata
    /// geri çekme. Saf rastgele yürüyüş bir süre sonra tavana ya da tabana yapışıp orada
    /// kalıyordu; geri çekme fiyatı dalgalı ama ortalaması sabit tutuyor.
    /// </summary>
    private void TickPrice()
    {
        priceTimer += Time.deltaTime;
        if (priceTimer < priceTickSeconds) return;
        priceTimer = 0f;

        float step = UnityEngine.Random.Range(-priceStepRatio, priceStepRatio) * basePricePerUnit;
        float pull = (basePricePerUnit - marketPrice) * priceMeanReversion;

        marketPrice = Mathf.Clamp(marketPrice + step + pull,
                                  basePricePerUnit * priceMinRatio,
                                  basePricePerUnit * priceMaxRatio);

        OnPriceChanged?.Invoke(PricePerUnit);
    }

    private void TickDemandSpike()
    {
        if (spikeRemaining <= 0f) return;

        spikeRemaining -= Time.deltaTime;
        if (spikeRemaining > 0f) return;

        spikeRemaining  = 0f;
        spikeMultiplier = 1f;

        OnDemandSpikeEnded?.Invoke();
        OnPriceChanged?.Invoke(PricePerUnit);
    }

    private void TickProduction()
    {
        if (depots.Count == 0) return;

        float dt = Time.deltaTime;

        for (int i = 0; i < depots.Count; i++)
        {
            Depot depot = depots[i];
            if (depot.stock >= depot.capacity) continue;

            depot.stock = Mathf.Min(depot.capacity, depot.stock + depot.ratePerSecond * dt);
        }
    }

    // -------------------------------------------------------------------------
    // YERLEŞTİRME
    // -------------------------------------------------------------------------

    private Camera ActiveCamera => mapCamera != null ? mapCamera : Camera.main;

    private void UpdatePlacementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CancelPlacementMode();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelPlacementMode();
            return;
        }

        if (!CanInteract()) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressed        = true;
            pressScreenPos = mouse.position.ReadValue();
            return;
        }

        if (!mouse.leftButton.wasReleasedThisFrame) return;
        if (!pressed) return;
        pressed = false;

        //kamera sürüklemesi tıklama sayılmasın
        if (Vector2.Distance(mouse.position.ReadValue(), pressScreenPos) > clickMoveThreshold) return;
        if (IsPointerOverUI()) return;

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        Vector2Int tile = decor.ScreenToTile(ActiveCamera, mouse.position.ReadValue());
        if (tile.x < 0) return;

        if (TryPlaceDepot(tile)) CancelPlacementMode();
    }

    /// <summary>Harita hazır ve yüzey görünümünde mi.</summary>
    private bool CanInteract()
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.HasMap) return false;

        if (UndergroundMapManager.Instance != null &&
            UndergroundMapManager.Instance.CurrentView != UndergroundMapManager.ViewMode.Surface)
            return false;

        return true;
    }

    private static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// <summary>
    /// Depoyu kurar. Reddedilirse sebebini OnPlacementRejected ile bildirir — sessizce
    /// hiçbir şey olmaması oyuncuya tıklamanın kaydedilmediğini düşündürüyor.
    /// </summary>
    public bool TryPlaceDepot(Vector2Int tile)
    {
        if (!unlocked) return false;

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.HasMap) return false;

        if (maxDepots > 0 && depots.Count >= maxDepots)
        {
            OnPlacementRejected?.Invoke($"En fazla {maxDepots} depo kurulabilir.");
            return false;
        }

        if (!decor.IsInAgriculturalRegion(tile))
        {
            OnPlacementRejected?.Invoke("Depo yalnızca tarım bölgesine kurulabilir.");
            return false;
        }

        for (int i = 0; i < depots.Count; i++)
        {
            if (Vector2Int.Distance(depots[i].tile, tile) >= minDepotSpacingTiles) continue;

            OnPlacementRejected?.Invoke($"Başka bir depoya çok yakın (en az {minDepotSpacingTiles} kare).");
            return false;
        }

        int cropTiles = decor.CountCropTilesAround(tile, collectionRadiusTiles);
        if (cropTiles < minCropTilesToBuild)
        {
            OnPlacementRejected?.Invoke($"Çevrede yeterli ekili tarla yok ({cropTiles}/{minCropTilesToBuild}).");
            return false;
        }

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null) return false;

        if (!stats.HasEnoughWealth(depotCost))
        {
            OnInsufficientFunds?.Invoke(depotCost);
            return false;
        }
        if (!stats.TrySpendWealth(depotCost)) return false;

        Depot depot = new Depot
        {
            tile          = tile,
            worldPosition = decor.TileToWorldCenter(tile),
            cropTiles     = cropTiles,
            ratePerSecond = ComputeRate(cropTiles),
            capacity      = ComputeCapacity(cropTiles),
            stock         = 0f,
            totalSold     = 0f,
        };

        depot.go = SpawnDepotVisual(depot);
        depots.Add(depot);

        OnDepotPlaced?.Invoke(GetInfo(depots.Count - 1));
        return true;
    }

    /// <summary>
    /// Deponun görselini kurar. İzometrik binalar zemine sprite'ın ALT KENARIYLA değer;
    /// merkeze hizalamak depoyu tarlanın yarım boy üstünde yüzdürür.
    /// </summary>
    private GameObject SpawnDepotVisual(Depot depot)
    {
        var go = new GameObject("CropDepot");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * depotScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = depotSprite != null ? depotSprite : GetPlaceholderSprite();

        //şehir binalarıyla aynı derinlik sözleşmesi: aşağıdaki her şey üstte çizilir
        sr.sortingOrder = 10 + (int)(depot.worldPosition.y * -100f);

        //sprite'ın alt kenarını tile'ın zeminine oturt (pivot nerede olursa olsun)
        float bottomOffset = -sr.sprite.bounds.min.y * depotScale;
        go.transform.position = new Vector3(depot.worldPosition.x,
                                            depot.worldPosition.y + bottomOffset,
                                            depotZ);
        return go;
    }

    /// <summary>
    /// Sprite atanmadığında kullanılan yer tutucu: silo + ambar silueti. Harita pixel-art
    /// olduğu için Point filtreli ve sert alfa eşikli üretilir (UISpriteFactory bilinear'dır,
    /// haritada yumuşak kenar yanlış durur).
    /// </summary>
    private static Sprite GetPlaceholderSprite()
    {
        if (placeholderSprite != null) return placeholderSprite;

        const int W = 16, H = 18;

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color wall  = new Color(0.74f, 0.30f, 0.22f); //ambar kırmızısı
        Color roof  = new Color(0.42f, 0.40f, 0.38f);
        Color silo  = new Color(0.80f, 0.78f, 0.70f);

        var pixels = new Color[W * H];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        //ambar gövdesi (sol) — x 0..9, y 0..9
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
            pixels[x + y * W] = wall;

        //ambar çatısı — x 0..9, y 10..13, kenarlardan daralan üçgen
        for (int y = 10; y < 14; y++)
        {
            int inset = y - 10;
            for (int x = inset; x < 10 - inset; x++)
                pixels[x + y * W] = roof;
        }

        //silo (sağ) — x 11..15, y 0..15, üstü kubbe
        for (int y = 0; y < 16; y++)
        for (int x = 11; x < 16; x++)
        {
            if (y >= 14 && (x == 11 || x == 15)) continue; //kubbe köşelerini yuvarla
            pixels[x + y * W] = silo;
        }

        tex.SetPixels(pixels);
        tex.Apply(false);

        //pivot ALT-ORTA: taban çizgisi sprite'ın alt kenarıdır
        placeholderSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
        placeholderSprite.name = "CropDepotPlaceholder";
        return placeholderSprite;
    }

    // -------------------------------------------------------------------------
    // SATIŞ
    // -------------------------------------------------------------------------

    /// <summary>Tek bir deponun stoğunu güncel fiyattan satar. Kazanılan parayı döndürür.</summary>
    public float SellDepot(int index)
    {
        if (index < 0 || index >= depots.Count) return 0f;

        Depot depot = depots[index];
        if (depot.stock <= 0f) return 0f;

        float units = depot.stock;

        depot.stock      = 0f;
        depot.totalSold += units;

        return CompleteSale(units);
    }

    /// <summary>Tüm depoların stoğunu tek seferde satar.</summary>
    public float SellAll()
    {
        if (depots.Count == 0) return 0f;

        float units = 0f;
        for (int i = 0; i < depots.Count; i++)
        {
            units += depots[i].stock;
            depots[i].totalSold += depots[i].stock;
            depots[i].stock = 0f;
        }

        return CompleteSale(units);
    }

    /// <summary>
    /// Oyuncunun ELDEN yaptığı satışın parasal sonucu. Tek yerde durur ki a32'nin ambalaj
    /// çarpanı iki satış yolunda da aynı davransın.
    ///
    /// Tavuk zinciri (a27) bu yoldan GEÇMEZ: zincir bir iş ortağıdır, kabartılmış fileyi
    /// fark eder — ambalaj hilesi yalnızca son tüketiciye işler.
    /// </summary>
    private float CompleteSale(float units)
    {
        if (units <= 0f) return 0f;

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null) return 0f;

        float revenue = units * PricePerUnit;

        if (packagingUnlocked)
        {
            revenue *= packagingSaleMultiplier;
            if (packagingReputationPerSale != 0f) stats.AddReputation(-packagingReputationPerSale);
        }

        stats.AddWealth(revenue);
        OnSold?.Invoke(units, revenue);

        return revenue;
    }

    /// <summary>
    /// UnlockDeceptivePackagingEffect tarafından çağrılır (a32). Elden yapılan satışların
    /// birim fiyatını yükseltir, karşılığında her satış itibar yakar.
    /// </summary>
    public void UnlockDeceptivePackaging()
    {
        if (packagingUnlocked) return;

        packagingUnlocked = true;
        OnDeceptivePackagingUnlocked?.Invoke();
    }

    public bool IsDeceptivePackagingUnlocked => packagingUnlocked;

    // -------------------------------------------------------------------------
    // a27 — TAVUK ZİNCİRİ
    // -------------------------------------------------------------------------

    /// <summary>UnlockPoultryChainEffect tarafından çağrılır.</summary>
    public void UnlockPoultryChain()
    {
        if (chainUnlocked) return;

        chainUnlocked = true;
        chainTimer    = 0f;
        OnPoultryChainUnlocked?.Invoke();
    }

    /// <summary>
    /// Zincir her tick stoğun bir oranını GARANTİLİ, piyasa altı bir fiyattan alır ve
    /// meşru bir vitrin olduğu için şüpheyi düşürür.
    ///
    /// Fiyat kasten TEMEL fiyattan hesaplanır, güncel piyasadan değil: zincir bir taban
    /// fiyat garantisidir, borsa değil. Talep dalgasından da etkilenmez — yoksa a28'in
    /// zirvesi otomatik olarak zincire de yansır ve "zirvede elle satmak" anlamsızlaşırdı.
    /// </summary>
    private void TickPoultryChain()
    {
        if (!chainUnlocked || depots.Count == 0) return;

        chainTimer += Time.deltaTime;
        if (chainTimer < chainTickSeconds) return;
        chainTimer = 0f;

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null) return;

        float units = 0f;
        for (int i = 0; i < depots.Count; i++)
        {
            Depot depot = depots[i];
            if (depot.stock <= 0f) continue;

            float take = depot.stock * Mathf.Clamp01(chainPurchaseRatio);
            depot.stock     -= take;
            depot.totalSold += take;
            units           += take;
        }

        if (units <= 0f) return;

        float revenue = units * basePricePerUnit * chainPriceRatio;

        chainTotalUnits   += units;
        chainTotalRevenue += revenue;

        stats.AddWealth(revenue);
        if (chainSuspicionPerTick != 0f) stats.AddSuspicion(-chainSuspicionPerTick);

        OnPoultryChainTick?.Invoke(units, revenue);
    }

    // -------------------------------------------------------------------------
    // a28 — STOKÇULUK VE TALEP DALGASI
    // -------------------------------------------------------------------------

    /// <summary>UnlockCropHoardingEffect tarafından çağrılır. Kapasiteyi büyütür.</summary>
    public void UnlockHoarding()
    {
        if (hoardingUnlocked) return;

        hoardingUnlocked = true;
        RecomputeAllDepots();
        OnHoardingUnlocked?.Invoke();
    }

    /// <summary>
    /// TriggerDemandWaveEffect tarafından çağrılır (a28 aktif yeteneği).
    ///
    /// Sosyal medyada bir akım başlatır (feed'e düşer) ve ürün fiyatını geçici olarak
    /// yukarı çeker. Çarpanın büyüklüğü ELDE TUTULAN STOKLA orantılıdır — "stokçuluk"
    /// mekaniği burada: boş depoyla dalga başlatmak neredeyse işe yaramaz, dolu depoyla
    /// başlatmak fiyatı katlar. Karşılığında şüphe artar.
    /// </summary>
    public bool TriggerDemandWave()
    {
        if (depots.Count == 0) return false;

        float stock = GetTotalStock();
        float fill  = demandSpikeFullStockUnits > 0f
            ? Mathf.Clamp01(stock / demandSpikeFullStockUnits)
            : 1f;

        spikeMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, demandSpikeMaxMultiplier), fill);
        spikeRemaining  = demandSpikeSeconds;

        //feed'e düşsün — akımı oyuncu başlattı
        if (SocialMediaManager.Instance != null)
            SocialMediaManager.Instance.SetPlayerOverride(demandSpikeTopic);

        if (GameStatManager.Instance != null && demandSpikeSuspicion != 0f)
            GameStatManager.Instance.AddSuspicion(demandSpikeSuspicion);

        OnDemandSpikeStarted?.Invoke(spikeMultiplier, spikeRemaining);
        OnPriceChanged?.Invoke(PricePerUnit);

        return true;
    }

    // -------------------------------------------------------------------------
    // a31 — ZEHİRLEME
    // -------------------------------------------------------------------------

    /// <summary>
    /// PoisonCropsEffect tarafından çağrılır. Üretimi kalıcı olarak hızlandırır; bedeli
    /// tek seferlik itibar kaybı ve üretim sürdükçe biriken şüphedir.
    /// </summary>
    public void ApplyPoisoning()
    {
        if (poisoned) return;

        poisoned    = true;
        poisonTimer = 0f;
        RecomputeAllDepots();

        if (GameStatManager.Instance != null && poisonReputationHit != 0f)
            GameStatManager.Instance.AddReputation(-poisonReputationHit);

        OnCropsPoisoned?.Invoke();
    }

    /// <summary>
    /// Zehirli üretim sürdükçe şüphe biriktirir. Hiç depo yoksa ya da hepsi doluysa
    /// (üretim durmuş) şüphe de eklenmez — ceza, işleyen üretime bağlıdır.
    /// </summary>
    private void TickPoison()
    {
        if (!poisoned || depots.Count == 0) return;
        if (poisonSuspicionPerTick == 0f) return;

        bool producing = false;
        for (int i = 0; i < depots.Count && !producing; i++)
            if (depots[i].stock < depots[i].capacity && depots[i].ratePerSecond > 0f) producing = true;

        if (!producing) return;

        poisonTimer += Time.deltaTime;
        if (poisonTimer < poisonTickSeconds) return;
        poisonTimer = 0f;

        if (GameStatManager.Instance != null)
            GameStatManager.Instance.AddSuspicion(poisonSuspicionPerTick);
    }

    // -------------------------------------------------------------------------
    // SORGULAR
    // -------------------------------------------------------------------------

    /// <summary>Ekili tile sayısından üretim hızı — zehirleme çarpanı dahil.</summary>
    private float ComputeRate(int cropTiles)
    {
        float rate = cropTiles * yieldPerCropTilePerSecond;
        if (poisoned) rate *= poisonYieldMultiplier;
        return rate;
    }

    /// <summary>Ekili tile sayısından kapasite — stokçuluk çarpanı dahil.</summary>
    private float ComputeCapacity(int cropTiles)
    {
        float capacity = Mathf.Clamp(cropTiles * capacityPerCropTile, minCapacity, maxCapacity);
        if (hoardingUnlocked) capacity *= hoardingCapacityMultiplier;
        return capacity;
    }

    /// <summary>Bir çarpan değiştiğinde tüm depoların hız/kapasitesini tazeler.</summary>
    private void RecomputeAllDepots()
    {
        for (int i = 0; i < depots.Count; i++)
        {
            Depot depot = depots[i];
            depot.ratePerSecond = ComputeRate(depot.cropTiles);
            depot.capacity      = ComputeCapacity(depot.cropTiles);
            depot.stock         = Mathf.Min(depot.stock, depot.capacity);
        }
    }

    public DepotInfo GetInfo(int index)
    {
        if (index < 0 || index >= depots.Count) return default;

        Depot depot = depots[index];

        return new DepotInfo
        {
            index         = index,
            tile          = depot.tile,
            worldPosition = depot.worldPosition,
            cropTiles     = depot.cropTiles,
            ratePerSecond = depot.ratePerSecond,
            capacity      = depot.capacity,
            stock         = depot.stock,
            stockValue    = depot.stock * PricePerUnit,
            totalSold     = depot.totalSold,
        };
    }

    /// <summary>Tüm depoların toplam stoğu (ürün birimi).</summary>
    public float GetTotalStock()
    {
        float total = 0f;
        for (int i = 0; i < depots.Count; i++) total += depots[i].stock;
        return total;
    }

    /// <summary>Stoğun tamamı şu an satılsa kazanılacak para.</summary>
    public float GetTotalStockValue() => GetTotalStock() * PricePerUnit;

    /// <summary>Tüm depoların saniyelik toplam üretimi (ürün birimi).</summary>
    public float GetTotalRatePerSecond()
    {
        float total = 0f;
        for (int i = 0; i < depots.Count; i++) total += depots[i].ratePerSecond;
        return total;
    }

    /// <summary>
    /// Depo kurulmadan önceki önizleme: verilen tile'a kurulsa kaç ekili tile toplanır,
    /// hız ve kapasite ne olur.
    /// </summary>
    public bool PreviewSite(Vector2Int tile, out int cropTiles, out float ratePerSecond, out float capacity)
    {
        cropTiles = 0; ratePerSecond = 0f; capacity = 0f;

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.HasMap) return false;
        if (!decor.IsInAgriculturalRegion(tile)) return false;

        cropTiles     = decor.CountCropTilesAround(tile, collectionRadiusTiles);
        ratePerSecond = ComputeRate(cropTiles);
        capacity      = ComputeCapacity(cropTiles);

        return cropTiles >= minCropTilesToBuild;
    }

    // -------------------------------------------------------------------------
    // HARİTA YENİDEN ÜRETİMİ
    // -------------------------------------------------------------------------

    /// <summary>
    /// Harita yeniden boyandığında (Repaint / bölge dönüşümü) parsel mozaiği değişir —
    /// eski ekili tile sayıları geçersizdir. Depoların verimini yeni tarlaya göre tazeler;
    /// tarlası yok olan depo üretimi durdurur ama YIKILMAZ (oyuncu parasını ödedi, sessizce
    /// silmek haksızlık olur — stoğunu hâlâ satabilir).
    ///
    /// a29 (kırsaldan kente göç) bu yolla kendi bedelini doğurur: tarlayı şehre çevirmek
    /// o tarlayı besleyen deponun gelirini düşürür.
    /// </summary>
    private void HandleDecorPlaced()
    {
        if (depots.Count == 0) return;

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.HasMap) return;

        for (int i = 0; i < depots.Count; i++)
        {
            Depot depot = depots[i];

            depot.cropTiles     = decor.CountCropTilesAround(depot.tile, collectionRadiusTiles);
            depot.ratePerSecond = ComputeRate(depot.cropTiles);
            depot.capacity      = ComputeCapacity(depot.cropTiles);

            //kapasite küçüldüyse fazla stok kaybolmasın diye stok kapasiteye kırpılır
            depot.stock = Mathf.Min(depot.stock, depot.capacity);
        }
    }
}
