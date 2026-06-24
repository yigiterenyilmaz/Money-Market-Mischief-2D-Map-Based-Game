using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// MapDecorPlacer is split across several partial-class files by subsystem:
//   MapDecorPlacer.cs            — config, nested types, shared state, lifecycle (this file)
//   MapDecorPlacer.Visuals.cs    — day/night crossfade, shadows, GO/shadow creation
//   MapDecorPlacer.CityLayout.cs — city hall, layer fills, special & nature placement
//   MapDecorPlacer.IndustrialLayout.cs — lane-packed factories over the industrial biome
//   MapDecorPlacer.Placement.cs  — overlap/radius/edge-thinning/road-footprint helpers
//   MapDecorPlacer.Earthquake.cs — break / destroy buildings
//   MapDecorPlacer.Ports.cs      — port placement + getters
//   MapDecorPlacer.Ships.cs      — ships, foam, docking, nav grid, A* pathfinding
public partial class MapDecorPlacer : MonoBehaviour
{
    [Header("General Decor")]
    [Range(8, 64)]  public int   cellSize      = 14;
    public float pixelsPerUnit                  = 100f;
    public float spriteZ                        = -0.5f;

    [Header("Spawn Rates — Per Region")]
    [Range(0, 16)] public int agriculturalSpawnRate = 2;
    [Range(0, 16)] public int urbanSpawnRate        = 2;
    [Range(0, 16)] public int industrialSpawnRate   = 2;

    [Header("Sprite Scale")]
    public Vector2 spriteScaleRange = new Vector2(0.75f, 1.25f);

    [Header("Shore & Border")]
    [Range(0, 20)]     public int   cityShoreBuffer             = 3;
    [Range(0, 20)]     public int   cityRegionBorderBuffer      = 5;
    [Tooltip("Yoldan minimum uzaklık — sprite'ların yol üstüne taşmasını önler.")]
    [Range(1, 10)]     public int   cityMinRoadDistance         = 2;

    [Header("City Building Shadow — Dinamik Güneş Gölgesi")]
    [Tooltip("Gölge rengi ve saydamlığı.")]
    public Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    [Tooltip("Near edge (binaya yakın kenar) Y ölçeği. 1 = bina kenarı kadar tam yükseklik.")]
    [Range(0.2f, 1.2f)] public float shadowNearScale = 1f;
    [Tooltip("Far edge (uzak uç) trapez incelme oranı. 0 = sivri uç, 1 = dikdörtgen.")]
    [Range(0f, 1f)] public float shadowTipRatio = 0.3f;
    [Tooltip("Binanın sanal yükseklik çarpanı. Uzunluk = yükseklik/tan(elev).")]
    [Range(0.2f, 3f)] public float shadowHeightRatio = 1f;
    [Tooltip("Gölge uzunluk tavanı (bina tam genişliği çarpanı).")]
    [Range(0.1f, 5f)] public float shadowMaxLength = 0.4f;
    [Tooltip("Öğlede minimum gölge uzunluk oranı.")]
    [Range(0f, 1f)] public float shadowMidScale = 0.1f;
    [Tooltip("Güneş yayının ne kadarı GÜNDÜZ (Day) fazında süpürülsün? Gündüz fazı güneş yörüngesinde küçük bir dilim olduğu için normalde gölge gündüz neredeyse hiç hareket etmez. Bu oran yükseldikçe gündüz gölgeleri daha çok döner+uzar (hızlı), şafak/akşam daha yavaş kalır. 0 = orijinal (lineer, gündüz hareketsiz). 0.6 önerilir.")]
    [Range(0f, 0.95f)] public float daytimeSweepFraction = 0.6f;
    [Tooltip("Gündüz↔şafak/akşam HIZ geçişi ne kadar yumuşatılsın? Gölge süpürme hızı faz sınırında (gündüz başı/sonu) ani değişir. Bu oran o sınırda hızı smoothstep ile harmanlar; bant genişliği komşu faz uzunluklarından türetilir. 0 = ani (sert köşe), 1 = en yumuşak.")]
    [Range(0f, 1f)] public float shadowSpeedSmoothing = 0.6f;
    [Tooltip("Flat projeksiyon gölge: öğleden (düz aşağı) sabaha/akşama doğru yana en fazla eğilme açısı (derece). shadowDir = -1 sol, +1 sağ.")]
    [Range(0f, 85f)] public float shadowLeanDegrees = 55f;
    [Tooltip("Flat projeksiyon gölge: lengthFactor'ı dikey ölçeğe (gölge uzunluğu) çeviren çarpan. Büyük = alçak güneşte gölge daha uzun uzar.")]
    [Range(0.5f, 8f)] public float shadowProjectLength = 3f;
    [Tooltip("Flat projeksiyon gölge taban yüksekliği: 0 = sprite en alt kenarı, 1 = sprite merkezi. Gölge dönme/uzama noktasını bu kadar yukarı taşır (halfHeight oranı).")]
    [Range(0f, 1f)] public float shadowBaseRaiseRatio = 0.35f;

    [Header("Isometric Shadow — İzometrik İkonlar İçin")]
    [Tooltip("İzometrik açı (derece). Gölge bu yönde diyagonal uzar. 30 = klasik iso.")]
    [Range(0f, 60f)] public float isoShadowAngleDegrees = 30f;
    [Tooltip("Pivot'un sprite alt kenarından yukarı kaydırma oranı (0=tam alt, 0.5=orta). İzometrik sprite'ta binanın yere değdiği görsel nokta.")]
    [Range(0f, 0.5f)] public float isoShadowPivotOffsetRatio = 0.10f;
    [Tooltip("Gölgenin yakın kenar yarı-kalınlığı, sprite YARIM GENİŞLİĞİNE oranla. Bina taban kalınlığı. 1.0 = tam sprite genişliği.")]
    [Range(0.1f, 2f)] public float isoShadowNearScale = 0.7f;
    [Tooltip("Gölge uzunluk hesabında bina yükseklik çarpanı. Yüksek = uzun gölge.")]
    [Range(0.2f, 3f)] public float isoShadowHeightRatio = 1f;
    [Tooltip("İzometrik gölge uzunluk tavanı (sprite YARIM YÜKSEKLİĞİNİN çarpanı). Flat'tekinden bağımsız — iso bina yüksek olduğu için büyük tutulmalı.")]
    [Range(0.5f, 5f)] public float isoShadowMaxLength = 2f;
    [Tooltip("Öğle kıstırma genişliği: gölge tam öğleye yaklaşırken bu |shadowDir| aralığında SÜREKLİ küçülüp tekrar büyür (sol↔sağ flip'i gizlemek için). Plato/donma YOK. Küçük = keskin/dar kıstırma, büyük = geniş yumuşak kıstırma. 0.25 önerilir.")]
    [Range(0.02f, 0.4f)] public float isoShadowNoonDeadZone = 0.25f;
    [Tooltip("Öğledeki minimum iso gölge ölçeği. Yalnızca tam öğle ANINDA bu değere iner (bant boyunca DEĞİL) → gölge gün boyunca hareketli kalır. 0 = öğlede tek an kaybolur, 0.3 = öğlede bile belirgin gölge. Görünürlük isteğine göre ayarla.")]
    [Range(0f, 1f)] public float isoShadowNoonMinScale = 0.3f;

    [Header("City Hall — Belediye Binası")]
    [Tooltip("Belediye binası scale aralığı. Büyük yapılabilir.")]
    public Vector2 cityHallScaleRange = new Vector2(1.5f, 2.0f);
    [Tooltip("Belediye etrafında bina olmayacak alan (tile).")]
    [Range(2, 30)] public int cityHallClearingRadius = 8;

    [Header("Isometric Depth — Arkaya Yığılma")]
    [Tooltip("Yarı-izometrik derinlik. Bir bina başka bir binanın ARKASINDA (yukarıda, +Y) " +
             "üretiliyorsa o bina tarafından kısmen örtülür; aralık bu oranla çarpılarak " +
             "daha sıkı paketlenmesine izin verilir. 1 = normal/simetrik aralık (etki yok), " +
             "0.5 = tam arkadaki binalar yarı mesafede. Düşük değer = daha yoğun, daha az düz görünüm.")]
    [Range(0.3f, 1f)] public float behindClearanceFactor = 0.6f;

    [Header("Special Building Spawn Scaling")]
    [Tooltip("Şehir bu kadar (ya da daha fazla) şehir tile'ı içeriyorsa 'büyük şehir' sayılır → " +
             "özel binalar tam 'count' kadar üretilir. Daha küçük şehirlerde üretilecek sayı, şehir " +
             "boyutuyla orantılı olarak 0..count arasında rastgele seçilir; yani count=1 olan binalar " +
             "küçük şehirlerde her zaman çıkmaz. Doğru eşiği bulmak için üretim sırasındaki " +
             "'allCityTiles=' log değerine bakın.")]
    public int specialBigCityTileCount = 3000;

    [Header("City Edge Thinning — Kenara Doğru Seyrelme")]
    [Tooltip("Şehir, belediyeden uzaklaştıkça (dış katmanlara doğru) seyrekleşir. 0 = seyrelme yok " +
             "(her yer eşit yoğun), 1 = şehrin en dış kenarında yoğunluk 0'a iner. İç katmanlar " +
             "(tower'lar) merkeze yakın olduğu için neredeyse hiç etkilenmez; dış katmanlar (mahalleler) " +
             "kenara doğru belirgin şekilde seyrelir.")]
    [Range(0f, 1f)] public float edgeThinning = 0.5f;
    [Tooltip("Şehir bölgesi (biome 2) KENARINA doğru seyrelmenin yayıldığı mesafe (tile). " +
             "Binalar bölge sınırına bu kadar tile kala seyrelmeye başlar; daha içeride yoğun kalır. " +
             "0 = bölge kenarına göre seyrelme yok (yalnızca belediyeye uzaklık bazlı seyrelme). " +
             "Mahalle outerRadius'u büyük olduğunda asıl kenar bu olduğundan etkili olan budur.")]
    [Range(0, 120)] public int edgeThinningBorderTiles = 30;

    [Header("Broken Building Sprites")]
    [Tooltip("Sprites randomly picked when a city building is cracked by an earthquake.")]
    public List<Sprite> brokenBuildingSprites = new List<Sprite>();
    [Tooltip("Tint applied to broken buildings.")]
    public Color brokenBuildingTint = new Color(0.55f, 0.45f, 0.40f, 1f);

    [Header("Day / Night Building Sprites")]
    [Tooltip("Night variants of broken building sprites. Index-matched to brokenBuildingSprites.")]
    public List<Sprite> brokenBuildingSpritesNight = new List<Sprite>();

    [Header("DEBUG — Day/Night Overlap Test")]
    [Tooltip("TEST ONLY: ignore the day/night cycle and show BOTH the day and night sprite " +
             "at the same time, each at debugOverlayAlpha. Use this to visually verify whether a " +
             "night variant is actually the same shape/dimensions as its day variant — if their " +
             "silhouettes don't line up here, the SPRITES differ (not the placement). " +
             "Turn off for normal play.")]
    public bool debugOverlayDayNight = false;
    [Tooltip("Alpha used for each sprite while debugOverlayDayNight is on. 0.5 = both half-visible.")]
    [Range(0f, 1f)] public float debugOverlayAlpha = 0.5f;

    // =========================================================================
    // PORT SETTINGS
    // =========================================================================

    [Header("Port Settings")]
    [Tooltip("Port day sprites. Each index is a port variant.")]
    public List<Sprite> portSpritesDay = new List<Sprite>();
    [Tooltip("Port night sprites. Index-matched to portSpritesDay.")]
    public List<Sprite> portSpritesNight = new List<Sprite>();
    [Tooltip("Scale applied to port sprites.")]
    public Vector2 portScaleRange = new Vector2(0.9f, 1.2f);
    [Tooltip("Minimum tiles of region biome surrounding a port candidate.")]
    [Range(1, 10)] public int portRegionBackingRadius = 3;
    [Tooltip("Minimum straight-line (Euclidean) distance in tiles between the two ports.")]
    [Range(10, 200)] public int portMinSeparation = 60;

    // =========================================================================
    // SHIP SETTINGS
    // =========================================================================

    [Header("Ship Settings")]
    [Tooltip("Maximum number of ships active at once across all ports.")]
    [Range(0, 20)] public int maxActiveShips = 4;
    [Tooltip("Ship day sprites. Each index is a ship variant.")]
    public List<Sprite> shipSpritesDay = new List<Sprite>();
    [Tooltip("Ship night sprites. Index-matched to shipSpritesDay.")]
    public List<Sprite> shipSpritesNight = new List<Sprite>();
    [Tooltip("Ship movement speed in world units per second.")]
    [Range(0.05f, 2f)] public float shipSpeed = 0.3f;
    [Tooltip("Scale applied to ship sprites.")]
    public Vector2 shipScaleRange = new Vector2(0.6f, 1.0f);
    [Tooltip("Seconds a ship waits at port before departing.")]
    public Vector2 shipWaitTimeRange = new Vector2(3f, 8f);
    [Tooltip("Seconds between ship spawn attempts.")]
    [Range(1f, 30f)] public float shipSpawnInterval = 5f;
    [Tooltip("How many tiles to downsample the pathfinding grid. Higher = faster but coarser.")]
    [Range(2, 16)] public int shipPathGridStep = 3;
    [Tooltip("Minimum clearance in tiles from land for ship waypoints.")]
    [Range(3, 50)] public int shipLandClearance = 20;
    [Tooltip("Minimum corridor width in nav cells. Cells without this many open neighbors in each axis are blocked. Prevents squeezing through narrow straits.")]
    [Range(1, 7)] public int shipMinCorridorWidth = 3;
    [Tooltip("How strongly ships prefer routes far from shore. 0 = no preference, 5 = strongly avoids coast.")]
    [Range(0f, 5f)] public float shipShoreAvoidanceWeight = 2.5f;
    [Tooltip("How fast ships turn toward their heading (degrees/sec). Lower = smoother, more graceful turns.")]
    [Range(15f, 360f)] public float shipTurnSpeed = 45f;

    [Tooltip("Chaikin corner-cutting passes applied to the path. More = rounder curves. 0 = raw A* path.")]
    [Range(0, 6)] public int shipPathSmoothingPasses = 3;

    // -------------------------------------------------------------------------
    // NESTED TYPES
    // -------------------------------------------------------------------------

    private class ShadowHandle
    {
        public Transform    transform;   // pivot node (bina origin = zemin temas çizgisi, y=0)
        public MeshRenderer renderer;    // iso modda mesh renderer (flat modda null)
        public Mesh         mesh;        // iso (flat modda null)
        public Material     material;    // iso (flat modda null)
        public Vector3[]    verts;       // iso (flat modda null)
        // Flat projeksiyon modu: binanın kendi sprite'ı koyu tonlanıp tabandan eğilip yassılır.
        public SpriteRenderer spriteRenderer; // flat (iso modda null)
        public Transform      spriteChild;     // flat: ayaklar pivot node'da, sprite yukarı durur
        public float        halfWidth;
        public float        halfHeight;
        public bool         isIsometric;
    }

    private struct BuildingData
    {
        public GameObject      go;
        public SpriteRenderer  dayRenderer;
        public SpriteRenderer  nightRenderer;
        public ShadowHandle    shadow;
        public int             tileX, tileY;
        public bool            isBroken;
        public bool            isSpecial;
        public int             spriteIndex;
        public int             brokenIndex;
        public float           baseAlpha;
    }

    private struct PortData
    {
        public GameObject     go;
        public SpriteRenderer dayRenderer;
        public SpriteRenderer nightRenderer;
        public int            tileX, tileY;
        public float          baseAlpha;
        public Vector3        worldPos;
        public Vector2        shoreDirection;  //kıyı çizgisine paralel yön (normalize)
        public Vector2        seaDirection;    //denize doğru yön (normalize, kıyıya dik)
    }

    private enum ShipState { Arriving, Waiting, Departing, Done }

    private class ShipInstance
    {
        public GameObject     go;
        public SpriteRenderer dayRenderer;
        public SpriteRenderer nightRenderer;
        public float          baseAlpha;
        public float          scale;
        public ShipState      state;
        public List<Vector3>  path;         // Catmull-Rom smoothed path points
        public int            pathIndex;
        public float          segmentT;     // 0–1 interpolation within current segment
        public float          waitTimer;
        public int            portIndex;
        public float          proximitySlowdown = 1f;
        public float          wakeTimer;
        public float          speed;
        public float          currentAngle; // current facing angle (degrees), smoothly lerped
    }

    private class FoamDot
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float lifetime;
        public float maxLifetime;
        public Vector3 velocity; //yana sürüklenme
    }

    /// <summary>
    /// Comparer that allows duplicate keys in SortedList (for A* open set).
    /// </summary>
    private class DuplicateKeyComparer : IComparer<float>
    {
        public int Compare(float x, float y)
        {
            int result = x.CompareTo(y);
            return result == 0 ? 1 : result; // never return 0 so duplicates are allowed
        }
    }

    // -------------------------------------------------------------------------
    // SHARED RUNTIME STATE
    // -------------------------------------------------------------------------

    private List<GameObject>   decorObjects    = new List<GameObject>();
    private List<Vector2>      occupiedCenters = new List<Vector2>();
    private List<Vector3>      denseOccupied   = new List<Vector3>(); // x, y, radius
    private List<BuildingData> cityBuildings   = new List<BuildingData>();
    private List<PortData>     ports           = new List<PortData>();
    private List<ShipInstance> activeShips     = new List<ShipInstance>();

    private DayNightCycle dayNight;
    private float         prevRatio = -1f;
    private float         prevSunProgress = float.NaN; // shadows recompute only when the sun has moved perceptibly

    // Ship spawning timer
    private float shipSpawnTimer;

    // Cached references for ship pathfinding
    private MapGenerator cachedMap;
    private float        cachedHalfW;
    private float        cachedHalfH;

    // DayNightCycle Repaint sonrası bir kez arandı mı
    private bool dayNightLookedUp;

    // -------------------------------------------------------------------------
    // ENTRY POINT
    // -------------------------------------------------------------------------

    public void Repaint(MapGenerator map, BiomePaintSettings settings, Texture2D mapTexture)
    {
        Clear();
        if (settings == null) { Debug.LogError("MapDecorPlacer: settings is null!"); return; }

        dayNight         = DayNightCycle.Instance;
        dayNightLookedUp = (dayNight != null);
        cachedMap        = map;

        int scaledCellSize = Mathf.Max(cellSize, Mathf.RoundToInt(cellSize * (map.width / 256f)));
        int cellArea       = scaledCellSize * scaledCellSize;
        float halfW = map.width  * 0.5f / pixelsPerUnit;
        float halfH = map.height * 0.5f / pixelsPerUnit;
        cachedHalfW = halfW;
        cachedHalfH = halfH;

        var allCityTiles   = new List<Vector2Int>();
        var biomeTilePools = new Dictionary<int, List<Vector2Int>>();

        bool hasRoads = RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated;

        for (int x = 0; x < map.width; x++)
        for (int y = 0; y < map.height; y++)
        {
            if (!map.IsLand(x, y)) continue;
            if (map.GetFog(x, y) > 0.6f) continue;
            int b = map.GetBiome(x, y);
            if (b == 2)
            {
                if (cityRegionBorderBuffer > 0 && !IsInsideRegion(map, x, y, cityRegionBorderBuffer))
                    continue;
                // Yola çok yakın veya üstündeki tile'ları dışla
                if (hasRoads && RoadGenerator.Instance.GetDistanceToRoadEdge(x, y) < cityMinRoadDistance)
                    continue;
                allCityTiles.Add(new Vector2Int(x, y));
            }
            else
            {
                if (!biomeTilePools.ContainsKey(b))
                    biomeTilePools[b] = new List<Vector2Int>();
                biomeTilePools[b].Add(new Vector2Int(x, y));
            }
        }

        Debug.Log($"MapDecorPlacer: allCityTiles={allCityTiles.Count} (biome 2, sis/kıyı/yol filtreleri sonrası).");

        // 1) Belediye binasını yerleştir ve 3 yol çek
        Vector2Int cityHallTile = FindCityHallTile(allCityTiles);
        if (cityHallTile.x >= 0)
        {
            TryPlaceCityHall(map, settings, cityHallTile, halfW, halfH);
            if (hasRoads)
                RoadGenerator.Instance.ConnectCityHallToRoads(map, cityHallTile);
        }
        else
        {
            Debug.LogWarning($"MapDecorPlacer: belediye binası için uygun şehir tile'ı bulunamadı " +
                             $"(allCityTiles={allCityTiles.Count}). Biome 2 (şehir) alanı yok ya da " +
                             $"tümü sis/kıyı/yol filtrelerine takıldı.");
        }

        // Kenar seyrelmesi için şehrin belediyeden en uzak tile mesafesini ölç (tile).
        cityHallTileCached = cityHallTile;
        cityRadiusTiles    = 0f;
        if (cityHallTile.x >= 0)
            for (int i = 0; i < allCityTiles.Count; i++)
            {
                float d = Vector2Int.Distance(allCityTiles[i], cityHallTile);
                if (d > cityRadiusTiles) cityRadiusTiles = d;
            }

        // Şehir bölgesi (biome 2) kenarına uzaklık alanını kur — kenara doğru seyrelme için.
        BuildCityEdgeDistance(map);

        // 2) Tile'ları katmanlara ayır (clearing zone dışındakiler)
        var layerPools = ClassifyLayers(allCityTiles, cityHallTile, settings.cityLayers);

        // 3) Port'ları yerleştir ve yola bağla — bina yerleşimi yeni port yollarini gormeli
        PlacePorts(map, halfW, halfH);
        if (RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated)
        {
            foreach (var port in ports)
                RoadGenerator.Instance.ConnectPortToRoad(map, new Vector2Int(port.tileX, port.tileY));
        }

        // 4) Özel binaları yerleştir (sayı şehir boyutuna göre ölçeklenir)
        PlaceSpecialCityBuildings(map, settings, layerPools, allCityTiles.Count, halfW, halfH);

        // 5) Katmanları sırayla doldur (iç→dış)
        for (int i = 0; i < settings.cityLayers.Count; i++)
            FillLayer(map, settings, settings.cityLayers[i], cityHallTile, layerPools[i], halfW, halfH);

        // 5b) Sanayi bölgesi (biome 3) — lane düzeninde fabrika/sanayi binaları.
        //     Doğa dekorundan ÖNCE çalışsın ki footprint'ler denseOccupied'a yazılsın.
        if (biomeTilePools.TryGetValue(3, out var industrialTiles))
            PlaceIndustrialLayout(map, settings, industrialTiles, halfW, halfH);

        // 5c) Urban bölgesi (biome 4) — seyrek bina dağılımı. Doğa dekorundan ÖNCE çalışsın
        //     ki footprint'ler denseOccupied'a yazılsın.
        if (biomeTilePools.TryGetValue(4, out var urbanTiles))
            PlaceUrbanLayout(map, settings, urbanTiles, halfW, halfH);

        // 5d) Tarım bölgesi (biome 1) — seyrek bina dağılımı. Doğa dekorundan ÖNCE.
        if (biomeTilePools.TryGetValue(1, out var agriculturalTiles))
            PlaceAgriculturalLayout(map, settings, agriculturalTiles, halfW, halfH);

        foreach (var kvp in biomeTilePools)
        {
            int biome = kvp.Key;
            var pool  = kvp.Value;
            int spawnRate = GetSpawnRate(biome);
            if (spawnRate == 0) continue;
            int decorAttempts = (pool.Count / Mathf.Max(1, cellArea)) * spawnRate;
            for (int attempt = 0; attempt < decorAttempts; attempt++)
            {
                Vector2Int tile = pool[Random.Range(0, pool.Count)];
                TryPlaceNatureDecor(map, settings, biome, tile.x, tile.y, halfW, halfH);
            }
        }

        // --- Build navigation grid for ships ---
        BuildNavGrid(map);

        // Apply initial crossfade state
        if (dayNight != null)
            ApplyCrossfade(dayNight.LightingRatio);

        shipSpawnTimer = 0f;

        Debug.Log($"MapDecorPlacer: decor={decorObjects.Count}, cityBuildings={cityBuildings.Count}, ports={ports.Count}");
    }

    int GetSpawnRate(int biome)
    {
        switch (biome)
        {
            case 1: return agriculturalSpawnRate;
            case 3: return industrialSpawnRate;
            case 4: return urbanSpawnRate;
            default: return 0;
        }
    }

    // -------------------------------------------------------------------------
    // UPDATE LOOP
    // -------------------------------------------------------------------------

    void Update()
    {
        // DayNightCycle referansını sadece bir kez ara — her frame'de Instance lookup yapma
        if (!dayNightLookedUp)
        {
            dayNight = DayNightCycle.Instance;
            if (dayNight != null) dayNightLookedUp = true;
        }

        float ratio = (dayNight != null) ? dayNight.LightingRatio : 0f;

        // Crossfade buildings + ports
        if (cityBuildings.Count > 0 || ports.Count > 0)
        {
            // DEBUG overlap test: show both sprites every frame, bypassing the ratio gate
            // (otherwise a stable ratio would skip re-applying after the toggle is flipped).
            if (debugOverlayDayNight)
            {
                prevRatio = -1f; // force a normal re-apply once debug is turned back off
                ApplyCrossfade(ratio);
            }
            else if (Mathf.Abs(ratio - prevRatio) > 0.005f)
            {
                prevRatio = ratio;
                ApplyCrossfade(ratio);
            }
        }

        // Dinamik gölge güncelle — yalnızca güneş hissedilir şekilde hareket ettiyse yeniden hesapla.
        // Eşik döngü hızına göre kendini ayarlar: hızlı döngüde her frame, yavaş/duraklamış döngüde
        // imperceptible frame'leri atlar. Bina başına trig + mesh yeniden kurmayı boşa harcamaz.
        float sunProgress = (dayNight != null) ? dayNight.SunProgress : 0.5f;
        if (float.IsNaN(prevSunProgress) || Mathf.Abs(sunProgress - prevSunProgress) > 0.0005f)
        {
            prevSunProgress = sunProgress;
            UpdateShadows(sunProgress);
        }

        // Ship tick
        UpdateShips(ratio);

    }

    // -------------------------------------------------------------------------
    // VISIBILITY
    // -------------------------------------------------------------------------

    public void SetDecorVisible(bool visible)
    {
        foreach (var go in decorObjects)
            if (go != null) go.SetActive(visible);
    }

    // -------------------------------------------------------------------------
    // UTILITY
    // -------------------------------------------------------------------------

    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    // Negatif sayilarda C# % operatoru negatif sonuc verir — modulo'yu [0, m) araligina kilitle
    static int MathMod(int a, int m)
    {
        int r = a % m;
        return r < 0 ? r + m : r;
    }

    // Fisher-Yates karıştırma. Tile havuzu üretim sırasında x-then-y (sütun düzeni) sıralı
    // geldiği için doldurma da bu yönde "süpürüyor" → binalar düzenli sütun/satır bloklarına
    // (batch) hizalanıyor. Havuzu karıştırmak yerleşim sırasını rastgeleleştirir; ızgara/sokak
    // yapısı modulo kontrolünden geldiği için korunur, yalnızca hizalama yanlılığı kalkar.
    static void ShuffleInPlace<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Sprite varyant seçimini dengele: şu ana kadar EN AZ yerleştirilmiş varyantı seç
    // (eşitlikte rastgele). Düz Random.Range tüm varyantlara eşit DENEME hakkı verir; ama
    // büyük sprite'lar daha geniş yarıçap ayırıp overlap/yol kontrolünde daha sık reddedilir
    // → eşit deneme, eşitsiz YERLEŞİM olur ve küçük sprite baskın çıkar. Yerleşim sayısını
    // referans alınca zor yerleşen varyant geride kalır, sonraki turda öne çıkar → dengeli dağılım.
    static int PickBalancedSpriteIndex(int[] placedCounts)
    {
        int min = int.MaxValue;
        for (int i = 0; i < placedCounts.Length; i++)
            if (placedCounts[i] < min) min = placedCounts[i];

        int ties = 0;
        for (int i = 0; i < placedCounts.Length; i++)
            if (placedCounts[i] == min) ties++;

        int pick = Random.Range(0, ties);
        for (int i = 0; i < placedCounts.Length; i++)
            if (placedCounts[i] == min && pick-- == 0) return i;

        return 0;
    }

    // -------------------------------------------------------------------------
    // CLEANUP
    // -------------------------------------------------------------------------

    public void Clear()
    {
        // Aktif coroutine'leri durdur
        if (spawnCoroutine != null)  { StopCoroutine(spawnCoroutine); spawnCoroutine = null; }
        if (departCoroutine != null) { StopCoroutine(departCoroutine); departCoroutine = null; }
        isSpawnPathPending  = false;
        isDepartPathPending = false;

        // Destroy all active ships
        foreach (var ship in activeShips)
            if (ship.go != null) Destroy(ship.go);
        activeShips.Clear();

        foreach (var go in decorObjects)
            if (go != null) Destroy(go);
        decorObjects.Clear();
        occupiedCenters.Clear();
        denseOccupied.Clear();
        cityBuildings.Clear();
        ports.Clear();
        prevRatio = -1f;
        prevSunProgress = float.NaN;
        ClearDenseGrid();
        dayNightLookedUp = false;
        cachedMap = null;
        navGrid   = null;
        navLandDist = null;
        shipSpawnTimer = 0f;
    }
}
