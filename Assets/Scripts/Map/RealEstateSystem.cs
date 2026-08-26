using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// EMLAK SİSTEMİ — haritadaki şehir binalarını tıklayıp inceleme, satın alma, kiraya verme.
///
/// Skill ile açılır (UnlockRealEstateEffect). Açıldıktan sonra oyuncu şehir (Cities) bölgesindeki
/// sıradan binalara tıklayıp fiyatını görebilir ve satın alabilir. Sahip olunan bina periyodik
/// kira üretir.
///
/// FİYAT = boyut × konum × küçük rastgelelik
///   boyut : binanın ekranda kapladığı alan, satın alınabilir binalar arasında 0..1'e normalize
///   konum : belediyeye ve yola yakınlık çarpanları
///   jitter: harita seed'inden türetilen ±küçük oran — aynı seed'de HEP aynı sonucu verir
/// Kira, fiyatın sabit bir oranıdır (paybackSeconds), böylece tek bir sayı tüm dengeyi sürer.
///
/// Bu sınıf girdi + mantık taşır; sunum PropertyInspectUI'dadır (PetroleumSystem/PetroleumSkillUI
/// ayrımının aynısı).
/// </summary>
public class RealEstateSystem : MonoBehaviour
{
    public static RealEstateSystem Instance { get; private set; }

    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    public Camera mapCamera;
    [Tooltip("Popup açıkken kamerayı dondurmak için. Boş bırakılırsa sahnede aranır.")]
    public MapController mapController;

    [Header("Fiyat — Boyut Bileşeni")]
    [Tooltip("En küçük binanın temel değeri.")]
    public float minBuildingValue = 25_000f;
    [Tooltip("En büyük binanın temel değeri.")]
    public float maxBuildingValue = 400_000f;

    [Header("Fiyat — Konum: Belediyeye Uzaklık")]
    [Tooltip("Belediyenin dibindeki bina çarpanı.")]
    public float hallNearMultiplier = 1.6f;
    [Tooltip("Belediyeden uzaktaki bina çarpanı.")]
    public float hallFarMultiplier = 0.7f;
    [Tooltip("Bu kadar tile sonra çarpan tamamen 'uzak' değerine iner.")]
    public float hallFalloffTiles = 90f;

    [Header("Fiyat — Konum: Yola Uzaklık")]
    public float roadNearMultiplier = 1.25f;
    public float roadFarMultiplier = 0.85f;
    public float roadFalloffTiles = 25f;

    [Header("Fiyat — Rastgelelik")]
    [Tooltip("Fiyata eklenen ±oran. 0.04 = ±%4. Harita seed'ine bağlı, her açılışta aynı.")]
    [Range(0f, 0.25f)] public float priceJitter = 0.04f;

    [Header("Gelir")]
    [Tooltip("Bir mülkün kendini amorti etme süresi (saniye). Kira = fiyat / bu süre.")]
    public float paybackSeconds = 900f;
    [Tooltip("Kira kaç saniyede bir cüzdana yatar.")]
    public float rentInterval = 10f;

    [Header("Satış / Onarım")]
    [Tooltip("Satış fiyatı = güncel fiyat × bu oran.")]
    [Range(0f, 0.99f)] public float sellRatio = 0.75f;
    [Tooltip("Hasarlı mülkün satış değeri ayrıca bu oranla çarpılır.")]
    [Range(0f, 1f)] public float brokenSellPenalty = 0.4f;
    [Tooltip("Onarım bedeli = fiyat × bu oran.")]
    [Range(0f, 2f)] public float rebuildCostRatio = 0.35f;

    [Header("Etkileşim")]
    [Tooltip("Basıp bırakma arası bu kadar pikselden az hareket varsa tıklama sayılır " +
             "(fazlası kamera sürüklemesidir).")]
    public float clickMoveThreshold = 8f;
    [Tooltip("Sahip olunan binanın kalıcı rengi. Sprite rengiyle ÇARPILIR — kırmızı/yeşili " +
             "kısmak binayı maviye çeker.")]
    public Color ownedTint = new Color(0.42f, 0.68f, 1f);
    [Tooltip("Seçili binanın rengi.")]
    public Color selectedTint = new Color(1f, 0.92f, 0.55f);

    [Header("Sahiplik İşareti (taban halkası)")]
    [Tooltip("Kapatılırsa mülkler sadece renkle ayırt edilir.")]
    public bool showOwnedMarker = true;
    public Color ownedMarkerColor = new Color(0.35f, 0.72f, 1f);
    [Tooltip("Halkanın genişliği, binanın genişliğinin kaç katı.")]
    [Range(0.3f, 2.5f)] public float ownedMarkerWidthRatio = 1.05f;
    [Tooltip("Dikey basıklık — zeminde yatan elips hissi için. 1 = tam daire.")]
    [Range(0.1f, 1f)] public float ownedMarkerSquash = 0.45f;
    [Tooltip("Taban hizasından ek dikey kaydırma (dünya birimi). Halka varsayılan olarak alt " +
             "kenarı binanın taban çizgisine değecek şekilde oturur; sprite'ın altında saydam " +
             "boşluk varsa bu değerle yukarı çekilir. + = yukarı.")]
    [Range(-0.5f, 0.5f)] public float ownedMarkerVerticalOffset = 0f;
    [Tooltip("Nabız hızı. 0 = sabit parlaklık.")]
    [Range(0f, 8f)] public float ownedMarkerPulseSpeed = 2.2f;
    [Range(0f, 1f)] public float ownedMarkerMinAlpha = 0.30f;
    [Range(0f, 1f)] public float ownedMarkerMaxAlpha = 0.95f;

    /// <summary>Bir binanın oyuncuya sunulan fiyat/durum özeti.</summary>
    public struct PropertyQuote
    {
        public Vector2Int tile;
        public Vector3    worldPosition;
        public bool       owned;
        public bool       broken;
        public float      price;
        public float      rentPerSecond;
        public float      sellValue;
        public float      rebuildCost;
        public int        distanceToHall;
        public int        distanceToRoad;
    }

    private class OwnedProperty
    {
        public Vector2Int tile;
        public float      purchasePrice;
        public bool       broken;
    }

    private readonly Dictionary<Vector2Int, OwnedProperty> owned = new Dictionary<Vector2Int, OwnedProperty>();

    private bool       unlocked;
    private bool       hasSelection;
    private Vector2Int selectedTile;
    private Vector2    pressScreenPos;
    private bool       pressed;
    private float      rentTimer;

    //events
    public static event Action<PropertyQuote> OnPropertySelected;
    public static event Action                OnSelectionCleared;
    public static event Action<PropertyQuote> OnPortfolioChanged; //alım/satım/onarım sonrası
    public static event Action<float>         OnRentTick;         //bu tick'te kazanılan toplam
    public static event Action                OnUnlocked;

    public bool IsUnlocked  => unlocked;
    public int  OwnedCount  => owned.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            //SADECE bu bileşeni yok et, GameObject'i DEĞİL. Bu script Managers gibi paylaşılan
            //bir nesnede duruyor; kopya diye tüm nesneyi silmek SkillTreeManager/GameStatManager
            //dahil oradaki HER ŞEYİ götürür ve oyun sessizce çalışmaz hale gelir.
            Debug.LogWarning("[RealEstateSystem] Sahnede birden fazla kopya var — fazlası kaldırıldı.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>UnlockRealEstateEffect tarafından çağrılır.</summary>
    public void Unlock()
    {
        if (unlocked) return;
        unlocked = true;
        OnUnlocked?.Invoke();
    }

    // -------------------------------------------------------------------------
    // GİRDİ
    // -------------------------------------------------------------------------

    private void Update()
    {
        TickRent();

        if (!unlocked) return;
        if (!CanInteract()) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

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

        HandleClick(mouse.position.ReadValue());
    }

    /// <summary>Harita hazır, yüzey görünümünde ve binalar görünür mü.</summary>
    private bool CanInteract()
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.HasMap) return false;

        //uzak zoom'da binalar tek imposter quad'a iner — tıklama hedefi kalmaz
        if (!decor.BuildingsVisible) return false;

        if (UndergroundMapManager.Instance != null &&
            UndergroundMapManager.Instance.CurrentView != UndergroundMapManager.ViewMode.Surface)
            return false;

        //bölge çizimi sürerken aynı sol tıklama iki sistemi birden tetiklemesin
        if (RegionConversionSystem.Instance != null && RegionConversionSystem.Instance.IsModeActive)
            return false;

        //depo yerleştirme de sol tıklamayı kullanıyor — aynı sebep
        if (CropDepotSystem.Instance != null && CropDepotSystem.Instance.IsPlacementMode)
            return false;

        return true;
    }

    private static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private Camera ActiveCamera => mapCamera != null ? mapCamera : Camera.main;

    private void HandleClick(Vector2 screenPos)
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        Vector2Int tile = decor.ScreenToTile(ActiveCamera, screenPos);

        if (tile.x < 0) { ClearSelection(); return; }

        //tıklanan tile'da bina olmayabilir; komşuları da tara (bina sprite'ı tek tile'dan geniş)
        if (!TryFindNearbyProperty(decor, tile, out MapDecorPlacer.PropertyView view))
        {
            ClearSelection();
            return;
        }

        if (!decor.IsPurchasable(view) && !owned.ContainsKey(view.tile))
        {
            ClearSelection();
            return;
        }

        Select(view.tile);
    }

    /// <summary>
    /// Bina sprite'ları tek tile'dan geniş olduğu için tam isabet nadirdir; küçük bir kare
    /// içinde en yakın satın alınabilir binayı arar.
    /// </summary>
    private bool TryFindNearbyProperty(MapDecorPlacer decor, Vector2Int center,
                                       out MapDecorPlacer.PropertyView best)
    {
        best = default;
        const int SEARCH = 6; //tile

        bool found = false;
        int bestDist = int.MaxValue;

        for (int dx = -SEARCH; dx <= SEARCH; dx++)
        for (int dy = -SEARCH; dy <= SEARCH; dy++)
        {
            Vector2Int t = new Vector2Int(center.x + dx, center.y + dy);
            if (!decor.TryGetProperty(t, out MapDecorPlacer.PropertyView v)) continue;
            if (!decor.IsPurchasable(v) && !owned.ContainsKey(t)) continue;

            int d = dx * dx + dy * dy;
            if (d >= bestDist) continue;

            bestDist = d;
            best     = v;
            found    = true;
        }

        return found;
    }

    // -------------------------------------------------------------------------
    // SEÇİM
    // -------------------------------------------------------------------------

    private void Select(Vector2Int tile)
    {
        if (hasSelection && selectedTile != tile) RestoreTint(selectedTile);

        hasSelection = true;
        selectedTile = tile;

        MapDecorPlacer.Instance.SetPropertyTint(tile, selectedTint);

        if (mapController != null) mapController.enable = false;

        OnPropertySelected?.Invoke(GetQuote(tile));
    }

    public void ClearSelection()
    {
        if (!hasSelection) return;

        RestoreTint(selectedTile);
        hasSelection = false;

        if (mapController != null) mapController.enable = true;

        OnSelectionCleared?.Invoke();
    }

    /// <summary>Seçim tonunu kaldırır: sahipliyse sahiplik rengine, değilse beyaza döner.</summary>
    private void RestoreTint(Vector2Int tile)
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null) return;

        decor.SetPropertyTint(tile, owned.ContainsKey(tile) ? ownedTint : Color.white);
    }

    /// <summary>Taban halkasını açar/kapatır (ayarlar kapalıysa hiç kurulmaz).</summary>
    private void ApplyMarker(Vector2Int tile, bool on)
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null) return;

        var style = new MapDecorPlacer.PropertyMarkerStyle
        {
            color          = ownedMarkerColor,
            widthRatio     = ownedMarkerWidthRatio,
            squash         = ownedMarkerSquash,
            verticalOffset = ownedMarkerVerticalOffset,
            pulseSpeed     = ownedMarkerPulseSpeed,
            minAlpha       = ownedMarkerMinAlpha,
            maxAlpha       = ownedMarkerMaxAlpha,
        };

        decor.SetPropertyMarker(tile, on && showOwnedMarker, style);
    }

    // -------------------------------------------------------------------------
    // FİYATLANDIRMA
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tile'a bağlı deterministik 0..1 gürültü (FNV-1a). Global Random'a DOKUNMAZ — harita
    /// üretimi bittikten sonra Random runtime'a devredildiği için (MapSeed.RandomizeRuntime)
    /// fiyatlar Random'dan türetilseydi her açılışta değişirdi.
    /// </summary>
    private static float TileNoise01(int seed, int x, int y)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            h = (h ^ (uint)x)    * 16777619u;
            h = (h ^ (uint)y)    * 16777619u;
            return (h & 0xFFFFFFu) / (float)0x1000000u;
        }
    }

    /// <summary>Uzaklığa göre yakın→uzak çarpanı.</summary>
    private static float DistanceMultiplier(float distance, float falloff, float near, float far)
    {
        if (falloff <= 0f) return near;
        float t = Mathf.Clamp01(distance / falloff);
        return Mathf.Lerp(near, far, t);
    }

    public float GetPrice(Vector2Int tile)
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.TryGetProperty(tile, out MapDecorPlacer.PropertyView view))
            return 0f;

        //boyut bileşeni
        float sizeScore = 0.5f;
        if (decor.TryGetPurchasableAreaRange(out float minArea, out float maxArea) && maxArea > minArea)
            sizeScore = Mathf.Clamp01((view.renderedArea - minArea) / (maxArea - minArea));

        float baseValue = Mathf.Lerp(minBuildingValue, maxBuildingValue, sizeScore);

        //konum bileşenleri
        float hallFactor = DistanceMultiplier(GetDistanceToHall(tile), hallFalloffTiles,
                                              hallNearMultiplier, hallFarMultiplier);
        float roadFactor = DistanceMultiplier(GetDistanceToRoad(tile), roadFalloffTiles,
                                              roadNearMultiplier, roadFarMultiplier);

        //küçük, seed'e bağlı sapma
        float jitter = 1f + (TileNoise01(MapSeed.CurrentSeed, tile.x, tile.y) * 2f - 1f) * priceJitter;

        return Mathf.Max(1f, baseValue * hallFactor * roadFactor * jitter);
    }

    public float GetDistanceToHall(Vector2Int tile)
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null) return 0f;

        Vector2Int hall = decor.CityHallTile;
        if (hall.x < 0) return hallFalloffTiles; //belediye yoksa nötr uzaklık

        return Vector2Int.Distance(tile, hall);
    }

    public float GetDistanceToRoad(Vector2Int tile)
    {
        if (RoadGenerator.Instance == null || !RoadGenerator.Instance.IsGenerated)
            return roadFalloffTiles;

        //yol yoksa / tile harita dışıysa int.MaxValue döner — falloff'a kırp,
        //yoksa hem çarpan hem de UI'daki RoundToInt taşar
        int distance = RoadGenerator.Instance.GetDistanceToRoadEdge(tile.x, tile.y);
        if (distance == int.MaxValue) return roadFalloffTiles;

        return Mathf.Min(distance, roadFalloffTiles);
    }

    public float GetRentPerSecond(Vector2Int tile)
    {
        if (paybackSeconds <= 0f) return 0f;
        return GetPrice(tile) / paybackSeconds;
    }

    public PropertyQuote GetQuote(Vector2Int tile)
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        decor.TryGetProperty(tile, out MapDecorPlacer.PropertyView view);

        bool isOwned  = owned.TryGetValue(tile, out OwnedProperty record);
        bool isBroken = isOwned ? record.broken : view.isBroken;
        float price   = GetPrice(tile);

        float sell = price * sellRatio;
        if (isBroken) sell *= brokenSellPenalty;

        return new PropertyQuote
        {
            tile           = tile,
            worldPosition  = view.worldPosition,
            owned          = isOwned,
            broken         = isBroken,
            price          = price,
            rentPerSecond  = isBroken ? 0f : GetRentPerSecond(tile),
            sellValue      = sell,
            rebuildCost    = price * rebuildCostRatio,
            distanceToHall = Mathf.RoundToInt(GetDistanceToHall(tile)),
            distanceToRoad = Mathf.RoundToInt(GetDistanceToRoad(tile)),
        };
    }

    // -------------------------------------------------------------------------
    // ALIM / SATIM / ONARIM
    // -------------------------------------------------------------------------

    public bool Buy(Vector2Int tile)
    {
        if (!unlocked) return false;
        if (owned.ContainsKey(tile)) return false;

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.TryGetProperty(tile, out MapDecorPlacer.PropertyView view)) return false;
        if (!decor.IsPurchasable(view)) return false;

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null) return false;

        float price = GetPrice(tile);
        if (!stats.HasEnoughWealth(price)) return false;
        if (!stats.TrySpendWealth(price)) return false;

        //orijinal sprite'ları MapDecorPlacer saklar (bina kırılırken) — burada tutmak yanlış
        //olurdu: zaten hasarlı bir bina satın alınırsa "orijinal" olarak enkaz kaydedilirdi
        owned[tile] = new OwnedProperty
        {
            tile          = tile,
            purchasePrice = price,
            broken        = view.isBroken,
        };

        decor.SetPropertyProtected(tile, true);
        decor.SetPropertyTint(tile, hasSelection && selectedTile == tile ? selectedTint : ownedTint);
        ApplyMarker(tile, true);

        OnPortfolioChanged?.Invoke(GetQuote(tile));
        return true;
    }

    public bool Sell(Vector2Int tile)
    {
        if (!owned.TryGetValue(tile, out OwnedProperty record)) return false;

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null) return false;

        float value = GetPrice(tile) * sellRatio;
        if (record.broken) value *= brokenSellPenalty;

        owned.Remove(tile);

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor != null)
        {
            decor.SetPropertyProtected(tile, false);
            decor.SetPropertyTint(tile, hasSelection && selectedTile == tile ? selectedTint : Color.white);
            ApplyMarker(tile, false);
        }

        stats.AddWealth(value);

        OnPortfolioChanged?.Invoke(GetQuote(tile));
        return true;
    }

    /// <summary>Depremde hasar görmüş mülkü ücret karşılığı onarır.</summary>
    public bool Rebuild(Vector2Int tile)
    {
        if (!owned.TryGetValue(tile, out OwnedProperty record)) return false;

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null) return false;

        //kayıt bayatsa haritadaki gerçek duruma güven
        if (decor.TryGetProperty(tile, out MapDecorPlacer.PropertyView view))
            record.broken = view.isBroken;

        if (!record.broken) return false;

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null) return false;

        float cost = GetPrice(tile) * rebuildCostRatio;
        if (!stats.HasEnoughWealth(cost)) return false;

        //önce onarımı dene: orijinal sprite kaydı yoksa para ALINMADAN vazgeç
        if (!decor.RestoreProperty(tile)) return false;
        if (!stats.TrySpendWealth(cost))  return false;

        record.broken = false;

        decor.SetPropertyTint(tile, hasSelection && selectedTile == tile ? selectedTint : ownedTint);

        OnPortfolioChanged?.Invoke(GetQuote(tile));
        return true;
    }

    // -------------------------------------------------------------------------
    // KİRA
    // -------------------------------------------------------------------------

    private void TickRent()
    {
        if (owned.Count == 0) return;

        rentTimer += Time.deltaTime;
        if (rentTimer < rentInterval) return;
        rentTimer = 0f;

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null) return;

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        float total = 0f;

        foreach (KeyValuePair<Vector2Int, OwnedProperty> pair in owned)
        {
            OwnedProperty record = pair.Value;

            //haritadaki hasar durumunu takip et — deprem kaydı bilgilendirmeden değiştirebilir
            if (decor != null && decor.TryGetProperty(pair.Key, out MapDecorPlacer.PropertyView view))
                record.broken = view.isBroken;

            if (record.broken) continue; //yıkık mülk kira getirmez

            total += GetRentPerSecond(pair.Key) * rentInterval;
        }

        if (total <= 0f) return;

        stats.AddWealth(total);
        OnRentTick?.Invoke(total);
    }

    /// <summary>Sahip olunan mülklerin saniyelik toplam kirası (UI özeti için).</summary>
    public float GetTotalRentPerSecond()
    {
        float total = 0f;
        foreach (KeyValuePair<Vector2Int, OwnedProperty> pair in owned)
            if (!pair.Value.broken) total += GetRentPerSecond(pair.Key);

        return total;
    }

    public bool IsOwned(Vector2Int tile) => owned.ContainsKey(tile);
}
