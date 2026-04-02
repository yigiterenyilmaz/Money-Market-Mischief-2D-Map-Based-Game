using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapDecorPlacer : MonoBehaviour
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

    [Header("Cities Decor — Road-Aware Placement")]
    [Range(0, 20)]     public int   cityShoreBuffer             = 3;
    [Range(0, 20)]     public int   cityRegionBorderBuffer      = 5;
    public bool                     citySnapRotation            = false;
    [Range(0.05f, 2f)] public float overlapRadius               = 0.3f;
    [Range(0, 30)]     public int   cityBuildingMaxRoadDistance = 15;
    [Tooltip("Yoldan minimum uzaklık — sprite'ların yol üstüne taşmasını önler.")]
    [Range(1, 10)]     public int   cityMinRoadDistance         = 2;
    [Tooltip("Şehir sprite'ları için ayrı scale aralığı.")]
    public Vector2 citySpriteScaleRange = new Vector2(0.3f, 0.5f);

    [Header("City Building Shadow")]
    [Tooltip("Gölge offset (world unit). X=sağ, Y=aşağı.")]
    public Vector2 shadowOffset = new Vector2(0.02f, -0.02f);
    [Tooltip("Gölge rengi ve saydamlığı.")]
    public Color shadowColor = new Color(0f, 0f, 0f, 0.4f);

    [Tooltip("Yoğun bölgede sprite'lar arası minimum mesafe. Küçük = daha sıkı.")]
    [Range(0.01f, 0.5f)] public float denseOverlapRadius        = 0.04f;

    [Header("City Sub-Zones — Yoğunluk")]
    [Tooltip("Yoğun bölge oranı alt sınırı (şehir tile'larının yüzdesi).")]
    [Range(0.1f, 0.5f)] public float cityDenseRatioMin          = 0.2f;
    [Tooltip("Yoğun bölge oranı üst sınırı.")]
    [Range(0.2f, 0.7f)] public float cityDenseRatioMax          = 0.5f;
    [Tooltip("Seyrek bölge spawn oranı.")]
    [Range(0, 16)]      public int   citySparseSpawnRate        = 1;
    [Tooltip("Noise ölçeği — küçük değer = büyük kümeler.")]
    [Range(0.01f, 0.1f)] public float cityDenseNoiseScale       = 0.03f;

    [Header("Broken Building Sprites")]
    [Tooltip("Sprites randomly picked when a city building is cracked by an earthquake.")]
    public List<Sprite> brokenBuildingSprites = new List<Sprite>();
    [Tooltip("Tint applied to broken buildings.")]
    public Color brokenBuildingTint = new Color(0.55f, 0.45f, 0.40f, 1f);

    [Header("Day / Night Building Sprites")]
    [Tooltip("Night variants of broken building sprites. Index-matched to brokenBuildingSprites.")]
    public List<Sprite> brokenBuildingSpritesNight = new List<Sprite>();

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

    private struct BuildingData
    {
        public GameObject      go;
        public SpriteRenderer  dayRenderer;
        public SpriteRenderer  nightRenderer;
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

    private List<GameObject>   decorObjects    = new List<GameObject>();
    private List<Vector2>      occupiedCenters = new List<Vector2>();
    private List<Vector3>      denseOccupied   = new List<Vector3>(); // x, y, radius
    private List<BuildingData> cityBuildings   = new List<BuildingData>();
    private List<PortData>     ports           = new List<PortData>();
    private List<ShipInstance> activeShips     = new List<ShipInstance>();

    private DayNightCycle dayNight;
    private float         prevRatio = -1f;

    // Ship spawning timer
    private float shipSpawnTimer;

    // Cached references for ship pathfinding
    private MapGenerator cachedMap;
    private float        cachedHalfW;
    private float        cachedHalfH;

    // Downsampled water navigation grid for pathfinding
    private bool[,] navGrid;       // true = navigable water
    private int[,]  navLandDist;   // per-cell minimum land distance (for cost biasing)
    private int     navW, navH;    // dimensions of the nav grid

    // Asenkron A* pathfinding — ana thread'i bloklamadan yol hesaplama
    private bool     dayNightLookedUp;   // Repaint sonrası bir kez arandı mı
    private Coroutine spawnCoroutine;     // aktif spawn coroutine'i (aynı anda tek)
    private Coroutine departCoroutine;    // aktif ayrılış coroutine'i
    private bool      isSpawnPathPending; // spawn için yol hesaplanıyor mu
    private bool      isDepartPathPending; // ayrılış için yol hesaplanıyor mu

    // A* hesaplama sırasında frame başına işlenecek maksimum iterasyon
    private const int ASTAR_ITERATIONS_PER_FRAME = 200;

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

        // Yoğun / seyrek alt bölge ayrımı
        var cityDensePool  = new List<Vector2Int>();
        var citySparsePool = new List<Vector2Int>();
        ClassifyCityDensity(allCityTiles, cityDensePool, citySparsePool);

        // 1) Özel binaları önce yerleştir
        PlaceSpecialCityBuildings(map, settings, cityDensePool, citySparsePool, halfW, halfH);

        // 2) Yoğun bölge — tüm boşlukları doldur
        FillDenseCityArea(map, settings, cityDensePool, halfW, halfH);

        // 3) Seyrek bölge — genel binalar
        int sparseAttempts = (citySparsePool.Count / Mathf.Max(1, cellArea)) * citySparseSpawnRate;
        for (int attempt = 0; attempt < sparseAttempts; attempt++)
        {
            if (citySparsePool.Count == 0) break;
            Vector2Int tile = citySparsePool[Random.Range(0, citySparsePool.Count)];
            TryPlaceCityBuilding(map, settings, tile.x, tile.y, halfW, halfH);
        }

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

        // --- Port placement ---
        PlacePorts(map, halfW, halfH);

        // --- Connect ports to road network ---
        if (RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated)
        {
            foreach (var port in ports)
                RoadGenerator.Instance.ConnectPortToRoad(map, new Vector2Int(port.tileX, port.tileY));
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
    // DAY / NIGHT CROSSFADE
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
            if (Mathf.Abs(ratio - prevRatio) > 0.005f)
            {
                prevRatio = ratio;
                ApplyCrossfade(ratio);
            }
        }

        // Ship tick
        UpdateShips(ratio);

    }

    // Tekrar tekrar oluşturmamak için crossfade'de kullanılan geçici Color yapıları
    private Color crossfadeTemp;

    void ApplyCrossfade(float ratio)
    {
        float dayAlphaMultiplier   = 1f - ratio;
        float nightAlphaMultiplier = ratio;

        // Buildings
        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (bd.dayRenderer == null) continue;

            float baseA = bd.baseAlpha;

            crossfadeTemp   = bd.dayRenderer.color;
            crossfadeTemp.a = baseA * dayAlphaMultiplier;
            bd.dayRenderer.color = crossfadeTemp;

            if (bd.nightRenderer != null)
            {
                crossfadeTemp   = bd.nightRenderer.color;
                crossfadeTemp.a = baseA * nightAlphaMultiplier;
                bd.nightRenderer.color = crossfadeTemp;
            }
        }

        // Ports
        for (int i = 0; i < ports.Count; i++)
        {
            PortData pd = ports[i];
            if (pd.dayRenderer == null) continue;

            float baseA = pd.baseAlpha;

            crossfadeTemp   = pd.dayRenderer.color;
            crossfadeTemp.a = baseA * dayAlphaMultiplier;
            pd.dayRenderer.color = crossfadeTemp;

            if (pd.nightRenderer != null)
            {
                crossfadeTemp   = pd.nightRenderer.color;
                crossfadeTemp.a = baseA * nightAlphaMultiplier;
                pd.nightRenderer.color = crossfadeTemp;
            }
        }

        // Ships
        for (int i = 0; i < activeShips.Count; i++)
        {
            ShipInstance ship = activeShips[i];
            if (ship.dayRenderer == null) continue;

            float baseA = ship.baseAlpha;

            crossfadeTemp   = ship.dayRenderer.color;
            crossfadeTemp.a = baseA * dayAlphaMultiplier;
            ship.dayRenderer.color = crossfadeTemp;

            if (ship.nightRenderer != null)
            {
                crossfadeTemp   = ship.nightRenderer.color;
                crossfadeTemp.a = baseA * nightAlphaMultiplier;
                ship.nightRenderer.color = crossfadeTemp;
            }
        }
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
    // EARTHQUAKE — SPRITE SWAP
    // -------------------------------------------------------------------------

    public int MarkBuildingsBroken(HashSet<Vector2Int> crackedTiles)
    {
        if (crackedTiles == null || crackedTiles.Count == 0) return 0;

        int count = 0;
        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (bd.isBroken) continue;
            if (!crackedTiles.Contains(new Vector2Int(bd.tileX, bd.tileY))) continue;

            if (bd.dayRenderer == null) continue;

            int brokenIdx = -1;
            if (brokenBuildingSprites != null && brokenBuildingSprites.Count > 0)
            {
                brokenIdx = Random.Range(0, brokenBuildingSprites.Count);

                bd.dayRenderer.sprite = brokenBuildingSprites[brokenIdx];

                if (bd.nightRenderer != null
                    && brokenBuildingSpritesNight != null
                    && brokenIdx < brokenBuildingSpritesNight.Count
                    && brokenBuildingSpritesNight[brokenIdx] != null)
                {
                    bd.nightRenderer.sprite = brokenBuildingSpritesNight[brokenIdx];
                }
                else if (bd.nightRenderer != null)
                {
                    bd.nightRenderer.sprite = brokenBuildingSprites[brokenIdx];
                }
            }

            bd.dayRenderer.color = new Color(
                brokenBuildingTint.r, brokenBuildingTint.g, brokenBuildingTint.b,
                bd.dayRenderer.color.a);

            if (bd.nightRenderer != null)
                bd.nightRenderer.color = new Color(
                    brokenBuildingTint.r, brokenBuildingTint.g, brokenBuildingTint.b,
                    bd.nightRenderer.color.a);

            bd.isBroken    = true;
            bd.brokenIndex = brokenIdx;
            cityBuildings[i] = bd;
            count++;
        }

        if (count > 0)
            Debug.Log($"MapDecorPlacer: {count} building(s) marked broken.");

        return count;
    }

    public bool IsBuildingBroken(int tileX, int tileY)
    {
        foreach (var bd in cityBuildings)
            if (bd.tileX == tileX && bd.tileY == tileY)
                return bd.isBroken;
        return false;
    }

    public List<Vector2Int> GetBrokenBuildingTiles()
    {
        var result = new List<Vector2Int>();
        foreach (var bd in cityBuildings)
            if (bd.isBroken) result.Add(new Vector2Int(bd.tileX, bd.tileY));
        return result;
    }

    // -------------------------------------------------------------------------
    // EARTHQUAKE — FULL DESTRUCTION
    // -------------------------------------------------------------------------

    public void DestroyBuildingsOnFaultLines(FaultLineGenerator faultGen)
    {
        int destroyed = 0;
        for (int i = cityBuildings.Count - 1; i >= 0; i--)
        {
            BuildingData bd = cityBuildings[i];
            if (!faultGen.IsFault(bd.tileX, bd.tileY)) continue;
            decorObjects.Remove(bd.go);
            if (bd.go != null) Destroy(bd.go);
            cityBuildings.RemoveAt(i);
            destroyed++;
        }
        Debug.Log($"MapDecorPlacer: {destroyed} building(s) destroyed by fault lines.");
    }

    public void DestroyBuildingsInRadius(FaultLineGenerator faultGen, Vector2Int epicenter, int radius)
    {
        int destroyed = 0;
        int r2        = radius * radius;
        for (int i = cityBuildings.Count - 1; i >= 0; i--)
        {
            BuildingData bd = cityBuildings[i];
            int dx = bd.tileX - epicenter.x, dy = bd.tileY - epicenter.y;
            if (dx * dx + dy * dy > r2)                continue;
            if (!faultGen.IsFault(bd.tileX, bd.tileY)) continue;
            decorObjects.Remove(bd.go);
            if (bd.go != null) Destroy(bd.go);
            cityBuildings.RemoveAt(i);
            destroyed++;
        }
        Debug.Log($"MapDecorPlacer: {destroyed} building(s) destroyed by earthquake.");
    }

    // -------------------------------------------------------------------------
    // PLACEMENT — CITIES
    // -------------------------------------------------------------------------

    void TryPlaceCityBuilding(MapGenerator map, BiomePaintSettings settings,
                              int tx, int ty, float halfW, float halfH)
    {
        if (settings.citiesDecor == null || settings.citiesDecor.Count == 0) return;
        if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) return;

        // Güvenlik: yola çok yakın tile'a sprite koyma
        if (RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated
            && RoadGenerator.Instance.GetDistanceToRoadEdge(tx, ty) < cityMinRoadDistance)
            return;

        int spriteIdx = Random.Range(0, settings.citiesDecor.Count);
        var entry = settings.citiesDecor[spriteIdx];
        Sprite daySprite = entry.daySprite;
        if (daySprite == null) return;

        float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;
        if (IsOverlapping(wx, wy)) return;
        occupiedCenters.Add(new Vector2(wx, wy));

        float scale    = Random.Range(citySpriteScaleRange.x, citySpriteScaleRange.y);
        float baseA    = Random.Range(0.85f, 1f);
        int sortOrder  = 10 + (int)(wy * -100f);

        var (go, daySR, nightSR) = CreateCityBuildingObject(
            daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder);

        decorObjects.Add(go);
        cityBuildings.Add(new BuildingData
        {
            go            = go,
            dayRenderer   = daySR,
            nightRenderer = nightSR,
            tileX         = tx,
            tileY         = ty,
            isBroken      = false,
            isSpecial     = false,
            spriteIndex   = spriteIdx,
            brokenIndex   = -1,
            baseAlpha     = baseA
        });
    }

    // -------------------------------------------------------------------------
    // CITY DENSITY CLASSIFICATION — Yoğun / Seyrek bölge ayrımı
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // DENSE CITY — Grid taramayla tüm boşlukları doldurma
    // -------------------------------------------------------------------------

    void FillDenseCityArea(MapGenerator map, BiomePaintSettings settings,
                           List<Vector2Int> densePool, float halfW, float halfH)
    {
        if (settings.citiesDecor == null || settings.citiesDecor.Count == 0) return;
        if (densePool.Count == 0) return;

        // Dense tile'ları hızlı lookup için set'e al
        var denseSet = new HashSet<Vector2Int>(densePool);

        // Grid adımı: sprite boyutuna göre (piksel cinsinden)
        // citySpriteScaleRange ortalaması * sprite genişliği tahmini
        float avgScale = (citySpriteScaleRange.x + citySpriteScaleRange.y) * 0.5f;
        // Step küçük = daha fazla aday nokta = boşluk kalmaz
        int step = Mathf.Max(1, Mathf.RoundToInt(avgScale * pixelsPerUnit * 0.08f));

        // Dense bölgenin sınırlarını bul
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < densePool.Count; i++)
        {
            var t = densePool[i];
            if (t.x < minX) minX = t.x;
            if (t.x > maxX) maxX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.y > maxY) maxY = t.y;
        }

        int placed = 0;

        // Grid şeklinde tara, rastgele offset ile kaotik yerleştir
        int jitter = Mathf.Max(1, step / 2);
        for (int x = minX; x <= maxX; x += step)
        for (int y = minY; y <= maxY; y += step)
        {
            // Rastgele kaydırma — düz grid hissini kırar
            int jx = x + Random.Range(-jitter, jitter + 1);
            int jy = y + Random.Range(-jitter, jitter + 1);

            // Kaydırılmış nokta veya yakın komşusu dense bölgede mi kontrol et
            bool inDense = false;
            int half = step / 2;
            for (int dx = -half; dx <= half && !inDense; dx += half)
            for (int dy = -half; dy <= half && !inDense; dy += half)
            {
                if (denseSet.Contains(new Vector2Int(jx + dx, jy + dy)))
                    inDense = true;
            }
            if (!inDense) continue;

            if (TryPlaceCityBuildingDense(map, settings, jx, jy, halfW, halfH))
                placed++;
        }

        Debug.Log($"MapDecorPlacer: dense fill complete, step={step}, placed={placed}");
    }

    /// <summary>
    /// Dense bölge için sıkı overlap kontrolüyle yerleştirme — üst üste binmez ama bitişik durur.
    /// </summary>
    bool TryPlaceCityBuildingDense(MapGenerator map, BiomePaintSettings settings,
                                   int tx, int ty, float halfW, float halfH)
    {
        if (settings.citiesDecor.Count == 0) return false;
        if (!map.IsLand(tx, ty)) return false;
        if (map.GetBiome(tx, ty) != 2) return false;
        if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) return false;

        // Yol kenarı kontrolü
        if (RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated
            && RoadGenerator.Instance.GetDistanceToRoadEdge(tx, ty) < cityMinRoadDistance)
            return false;

        int spriteIdx = Random.Range(0, settings.citiesDecor.Count);
        var entry = settings.citiesDecor[spriteIdx];
        Sprite daySprite = entry.daySprite;
        if (daySprite == null) return false;

        float scale = Random.Range(citySpriteScaleRange.x, citySpriteScaleRange.y);

        // Sprite'ın world boyutunu hesapla
        float spriteWorldW = (daySprite.rect.width / daySprite.pixelsPerUnit) * scale;
        float spriteWorldH = (daySprite.rect.height / daySprite.pixelsPerUnit) * scale;
        float spriteRadius = Mathf.Min(spriteWorldW, spriteWorldH) * 0.45f;

        float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

        // Sprite'ın gerçek boyutuna göre overlap kontrolü
        if (IsDenseOverlapping(wx, wy, spriteRadius)) return false;
        denseOccupied.Add(new Vector3(wx, wy, spriteRadius));

        float baseA    = 1f;
        int sortOrder  = 10 + (int)(wy * -100f);

        // Ortak yerleştirme
        var (go, daySR, nightSR) = CreateCityBuildingObject(
            daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder);

        decorObjects.Add(go);
        cityBuildings.Add(new BuildingData
        {
            go            = go,
            dayRenderer   = daySR,
            nightRenderer = nightSR,
            tileX         = tx,
            tileY         = ty,
            isBroken      = false,
            isSpecial     = false,
            spriteIndex   = spriteIdx,
            brokenIndex   = -1,
            baseAlpha     = baseA
        });

        return true;
    }

    // -------------------------------------------------------------------------
    // CITY DENSITY CLASSIFICATION — Yoğun / Seyrek bölge ayrımı
    // -------------------------------------------------------------------------

    void ClassifyCityDensity(List<Vector2Int> allTiles,
                             List<Vector2Int> denseOut, List<Vector2Int> sparseOut)
    {
        if (allTiles.Count == 0) return;

        // Merkez noktayı hesapla
        float cx = 0f, cy = 0f;
        for (int i = 0; i < allTiles.Count; i++)
        {
            cx += allTiles[i].x;
            cy += allTiles[i].y;
        }
        cx /= allTiles.Count;
        cy /= allTiles.Count;

        // Tek küme — merkez nokta seed olarak kullanılır
        Vector2 seed = new Vector2(cx, cy);
        float noiseOffsetX = Random.Range(0f, 1000f);
        float noiseOffsetY = Random.Range(0f, 1000f);

        // Her tile için skor hesapla: noise + merkeze yakınlık
        float maxDist = Mathf.Sqrt(allTiles.Count) * 0.5f + 1f;
        var scored = new List<(Vector2Int tile, float score)>(allTiles.Count);

        for (int i = 0; i < allTiles.Count; i++)
        {
            Vector2Int t = allTiles[i];
            float noise = Mathf.PerlinNoise(
                t.x * cityDenseNoiseScale + noiseOffsetX,
                t.y * cityDenseNoiseScale + noiseOffsetY);

            float dist = Vector2.Distance(new Vector2(t.x, t.y), seed);
            float proximity = 1f - Mathf.Clamp01(dist / maxDist);

            // Skor: proximity ağırlıklı — tek bütün küme oluşturur
            float score = noise * 0.3f + proximity * 0.7f;
            scored.Add((t, score));
        }

        // Skora göre sırala (yüksekten düşüğe)
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        // Yoğun bölge oranı (rastgele min-max arası)
        float denseRatio = Random.Range(cityDenseRatioMin, cityDenseRatioMax);
        int denseCount = Mathf.Max(1, Mathf.RoundToInt(allTiles.Count * denseRatio));

        for (int i = 0; i < scored.Count; i++)
        {
            if (i < denseCount)
                denseOut.Add(scored[i].tile);
            else
                sparseOut.Add(scored[i].tile);
        }

        Debug.Log($"MapDecorPlacer: city tiles={allTiles.Count}, dense={denseOut.Count}, sparse={sparseOut.Count}");
    }

    // -------------------------------------------------------------------------
    // SPECIAL CITY BUILDINGS — Sabit sayılı özel binalar
    // -------------------------------------------------------------------------

    void PlaceSpecialCityBuildings(MapGenerator map, BiomePaintSettings settings,
                                   List<Vector2Int> densePool, List<Vector2Int> sparsePool,
                                   float halfW, float halfH)
    {
        if (settings.specialCityBuildings == null) return;

        for (int s = 0; s < settings.specialCityBuildings.Count; s++)
        {
            var special = settings.specialCityBuildings[s];
            if (special.daySprite == null || special.count <= 0) continue;

            // Hedef havuzu belirle
            List<Vector2Int> pool;
            if (special.denseOnly)
            {
                pool = densePool;
            }
            else
            {
                pool = new List<Vector2Int>(densePool.Count + sparsePool.Count);
                pool.AddRange(densePool);
                pool.AddRange(sparsePool);
            }

            if (pool.Count == 0) continue;

            int placed = 0;
            int maxAttempts = pool.Count;

            for (int attempt = 0; attempt < maxAttempts && placed < special.count; attempt++)
            {
                Vector2Int tile = pool[Random.Range(0, pool.Count)];
                if (TryPlaceSpecialBuilding(map, special, tile.x, tile.y, halfW, halfH))
                    placed++;
            }

            if (placed < special.count)
                Debug.LogWarning($"MapDecorPlacer: özel bina '{special.daySprite.name}' — " +
                                 $"hedef={special.count}, yerleşen={placed}");
        }
    }

    bool TryPlaceSpecialBuilding(MapGenerator map, SpecialCityBuilding special,
                                 int tx, int ty, float halfW, float halfH)
    {
        if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) return false;

        // Güvenlik: yola çok yakın tile'a sprite koyma
        if (RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated
            && RoadGenerator.Instance.GetDistanceToRoadEdge(tx, ty) < cityMinRoadDistance)
            return false;

        float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;
        if (IsOverlapping(wx, wy)) return false;
        occupiedCenters.Add(new Vector2(wx, wy));

        float scale    = Random.Range(citySpriteScaleRange.x, citySpriteScaleRange.y);
        float baseA    = Random.Range(0.85f, 1f);
        int sortOrder  = 10 + (int)(wy * -100f);

        var (go, daySR, nightSR) = CreateCityBuildingObject(
            special.daySprite, special.nightSprite, wx, wy, scale, baseA, sortOrder);

        decorObjects.Add(go);
        cityBuildings.Add(new BuildingData
        {
            go            = go,
            dayRenderer   = daySR,
            nightRenderer = nightSR,
            tileX         = tx,
            tileY         = ty,
            isBroken      = false,
            isSpecial     = true,
            spriteIndex   = -1,
            brokenIndex   = -1,
            baseAlpha     = baseA
        });

        return true;
    }

    Sprite GetNightBrokenSprite(int index)
    {
        if (brokenBuildingSpritesNight == null || index < 0 || index >= brokenBuildingSpritesNight.Count)
            return null;
        return brokenBuildingSpritesNight[index];
    }

    bool HasShoreBuffer(MapGenerator map, int tx, int ty)
    {
        for (int dx = -cityShoreBuffer; dx <= cityShoreBuffer; dx++)
        for (int dy = -cityShoreBuffer; dy <= cityShoreBuffer; dy++)
        {
            if (Mathf.Abs(dx) + Mathf.Abs(dy) > cityShoreBuffer) continue;
            if (!map.IsLand(tx + dx, ty + dy)) return false;
        }
        return true;
    }

    bool IsInsideRegion(MapGenerator map, int tx, int ty, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            int nx = tx + dx, ny = ty + dy;
            if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) return false;
            if (!map.IsLand(nx, ny) || map.GetBiome(nx, ny) != 2) return false;
        }
        return true;
    }

    (GameObject go, SpriteRenderer daySR, SpriteRenderer nightSR) CreateCityBuildingObject(
        Sprite daySprite, Sprite nightSprite, float wx, float wy,
        float scale, float baseA, int sortOrder)
    {
        GameObject go = new GameObject("CityBuilding");
        go.transform.SetParent(transform);
        go.transform.position   = new Vector3(wx, wy, spriteZ);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        if (citySnapRotation)
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);

        // Gölge
        AddShadow(go, daySprite, sortOrder);

        SpriteRenderer daySR = go.AddComponent<SpriteRenderer>();
        daySR.sprite       = daySprite;
        daySR.sortingOrder = sortOrder;
        daySR.flipX        = false;
        daySR.color        = new Color(1f, 1f, 1f, baseA);

        SpriteRenderer nightSR = null;
        if (nightSprite != null)
        {
            GameObject nightGo = new GameObject("NightOverlay");
            nightGo.transform.SetParent(go.transform, false);
            nightGo.transform.localPosition = Vector3.zero;
            nightGo.transform.localScale    = Vector3.one;
            nightGo.transform.localRotation = Quaternion.identity;

            nightSR              = nightGo.AddComponent<SpriteRenderer>();
            nightSR.sprite       = nightSprite;
            nightSR.sortingOrder = sortOrder + 1;
            nightSR.flipX        = false;
            nightSR.color        = new Color(1f, 1f, 1f, 0f);
        }

        return (go, daySR, nightSR);
    }

    void AddShadow(GameObject parent, Sprite sprite, int sortOrder)
    {
        GameObject shadowGo = new GameObject("Shadow");
        shadowGo.transform.SetParent(parent.transform, false);
        shadowGo.transform.localPosition = new Vector3(shadowOffset.x, shadowOffset.y, 0f);
        shadowGo.transform.localScale    = Vector3.one;
        shadowGo.transform.localRotation = Quaternion.identity;

        SpriteRenderer sr = shadowGo.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = sortOrder - 1;
        sr.flipX        = false;
        sr.color        = shadowColor;
    }

    bool IsOverlapping(float wx, float wy)
    {
        float minDist = overlapRadius * 2f;
        foreach (var c in occupiedCenters)
            if (Vector2.Distance(new Vector2(wx, wy), c) < minDist) return true;
        return false;
    }

    bool IsDenseOverlapping(float wx, float wy, float myRadius)
    {
        for (int i = 0; i < denseOccupied.Count; i++)
        {
            var other = denseOccupied[i];
            float minDist = myRadius + other.z; // iki sprite'ın yarıçapı toplamı
            float dx = wx - other.x;
            float dy = wy - other.y;
            if (dx * dx + dy * dy < minDist * minDist) return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // PLACEMENT — NATURE DECOR
    // -------------------------------------------------------------------------

    void TryPlaceNatureDecor(MapGenerator map, BiomePaintSettings settings,
                             int biome, int tx, int ty, float halfW, float halfH)
    {
        Sprite sprite = PickDecorSprite(biome, settings);
        if (sprite == null) return;
        float scale = Random.Range(spriteScaleRange.x, spriteScaleRange.y);
        float wx    = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy    = transform.position.y + (ty / pixelsPerUnit) - halfH;
        PlaceSimpleSprite("Decor", sprite, wx, wy, scale, Random.value > 0.5f, 2);
    }

    Sprite PickDecorSprite(int biome, BiomePaintSettings s)
    {
        List<Sprite> pool;
        switch (biome)
        {
            case 1: pool = s.agriculturalDecor; break;
            case 3: pool = s.industrialDecor;   break;
            case 4: pool = s.urbanDecor;        break;
            default: return null;
        }
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    GameObject PlaceSimpleSprite(string goName, Sprite sprite, float wx, float wy,
                                float scale, bool flipX, int sortOrder)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(transform);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = sortOrder;
        sr.flipX        = flipX;
        sr.color        = new Color(1f, 1f, 1f, Random.Range(0.85f, 1f));
        go.transform.position   = new Vector3(wx, wy, spriteZ);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        decorObjects.Add(go);
        return go;
    }

    // =========================================================================
    // PORT PLACEMENT
    // =========================================================================

    /// <summary>
    /// Finds city tiles adjacent to water (shore tiles) and places ports.
    /// Only spawns if the city region is NOT at the map edge (i.e. not touching
    /// the island boundary defined by fog / safe zone).
    /// </summary>
    void PlacePorts(MapGenerator map, float halfW, float halfH)
    {
        if (portSpritesDay == null || portSpritesDay.Count == 0) return;

        // 0. Build ocean mask: flood-fill from map edges through water tiles.
        bool[,] isOcean = BuildOceanMask(map);

        // 1. Collect ocean-shore candidates per biome, in priority order:
        //    Cities (2) → Industrial (3) → Urban (4) → Agricultural (1)
        int[] biomePriority = { 2, 3, 4, 1 };
        int edgeMargin = 8;

        var candidatesByBiome = new Dictionary<int, List<Vector2Int>>();
        foreach (int b in biomePriority)
            candidatesByBiome[b] = new List<Vector2Int>();

        for (int x = edgeMargin; x < map.width - edgeMargin; x++)
        for (int y = edgeMargin; y < map.height - edgeMargin; y++)
        {
            if (!map.IsLand(x, y)) continue;
            if (map.GetFog(x, y) > 0.4f) continue;

            int biome = map.GetBiome(x, y);
            if (!candidatesByBiome.ContainsKey(biome)) continue;

            // Must be adjacent to ocean
            if (!IsAdjacentToOcean(map, x, y, isOcean)) continue;

            // Must have enough backing of same biome inland
            if (!HasRegionBacking(map, x, y, biome)) continue;

            candidatesByBiome[biome].Add(new Vector2Int(x, y));
        }

        // 2. Try to place both ports in the highest-priority biome that works.
        //    "Works" = we can find 2 candidates with portMinSeparation Euclidean distance.
        //    If a biome can't satisfy both, fall through to the next.
        List<Vector2Int> portTiles = null;

        foreach (int biome in biomePriority)
        {
            var candidates = candidatesByBiome[biome];
            if (candidates.Count < 2) continue;

            portTiles = TryPickTwoPorts(candidates);
            if (portTiles != null)
            {
                Debug.Log($"MapDecorPlacer: Both ports placed in biome {biome}.");
                break;
            }
        }

        // 3. If no single biome could hold both, try mixed: place first in highest
        //    priority available, second in next available biome that satisfies distance.
        if (portTiles == null)
        {
            portTiles = TryPickTwoPortsMixed(candidatesByBiome, biomePriority);
            if (portTiles != null)
                Debug.Log("MapDecorPlacer: Ports placed across different biomes.");
        }

        if (portTiles == null || portTiles.Count < 2)
        {
            Debug.Log("MapDecorPlacer: Could not place 2 ports with required separation.");
            return;
        }

        // 4. Instantiate the 2 port GameObjects
        foreach (var tile in portTiles)
            InstantiatePort(tile, halfW, halfH);

        Debug.Log($"MapDecorPlacer: Placed {ports.Count} port(s), separation={TileDistance(portTiles[0], portTiles[1]):F0} tiles.");
    }

    /// <summary>
    /// Tries to pick 2 candidates from a single list with at least portMinSeparation Euclidean distance.
    /// Shuffles and greedily searches.
    /// </summary>
    List<Vector2Int> TryPickTwoPorts(List<Vector2Int> candidates)
    {
        ShuffleList(candidates);

        // Try each candidate as first port, find a second that's far enough
        for (int i = 0; i < Mathf.Min(candidates.Count, 80); i++)
        {
            Vector2Int first = candidates[i];
            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (TileDistance(first, candidates[j]) >= portMinSeparation)
                    return new List<Vector2Int> { first, candidates[j] };
            }
        }
        return null;
    }

    /// <summary>
    /// Picks first port from highest-priority biome, second from any other biome
    /// that satisfies the distance constraint.
    /// </summary>
    List<Vector2Int> TryPickTwoPortsMixed(Dictionary<int, List<Vector2Int>> candidatesByBiome, int[] biomePriority)
    {
        // Pick first port from highest priority biome that has candidates
        Vector2Int first = Vector2Int.zero;
        bool foundFirst = false;

        foreach (int biome in biomePriority)
        {
            var list = candidatesByBiome[biome];
            if (list.Count > 0)
            {
                first = list[Random.Range(0, list.Count)];
                foundFirst = true;
                break;
            }
        }

        if (!foundFirst) return null;

        // Pick second from any biome (in priority order) that satisfies distance
        foreach (int biome in biomePriority)
        {
            var list = candidatesByBiome[biome];
            ShuffleList(list);
            foreach (var candidate in list)
            {
                if (TileDistance(first, candidate) >= portMinSeparation)
                    return new List<Vector2Int> { first, candidate };
            }
        }

        return null;
    }

    float TileDistance(Vector2Int a, Vector2Int b)
    {
        float dx = a.x - b.x, dy = a.y - b.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    void InstantiatePort(Vector2Int tile, float halfW, float halfH)
    {
        int spriteIdx = Random.Range(0, portSpritesDay.Count);
        Sprite daySprite = portSpritesDay[spriteIdx];
        if (daySprite == null) return;

        float wx = transform.position.x + (tile.x / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (tile.y / pixelsPerUnit) - halfH;

        float scale   = Random.Range(portScaleRange.x, portScaleRange.y);
        float baseA   = Random.Range(0.9f, 1f);
        int sortOrder = 12 + (int)(wy * -100f);

        GameObject go = new GameObject("Port");
        go.transform.SetParent(transform);
        go.transform.position   = new Vector3(wx, wy, spriteZ);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer daySR = go.AddComponent<SpriteRenderer>();
        daySR.sprite       = daySprite;
        daySR.sortingOrder = sortOrder;
        daySR.color        = new Color(1f, 1f, 1f, baseA);

        SpriteRenderer nightSR = null;
        if (portSpritesNight != null && spriteIdx < portSpritesNight.Count &&
            portSpritesNight[spriteIdx] != null)
        {
            GameObject nightGo = new GameObject("PortNight");
            nightGo.transform.SetParent(go.transform, false);
            nightGo.transform.localPosition = Vector3.zero;
            nightGo.transform.localScale    = Vector3.one;
            nightGo.transform.localRotation = Quaternion.identity;

            nightSR              = nightGo.AddComponent<SpriteRenderer>();
            nightSR.sprite       = portSpritesNight[spriteIdx];
            nightSR.sortingOrder = sortOrder + 1;
            nightSR.color        = new Color(1f, 1f, 1f, 0f);
        }

        decorObjects.Add(go);

        //kıyı yönünü hesapla — tüm su komşularının ortalama yönü
        Vector2 seaDir = Vector2.zero;
        int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        for (int d = 0; d < 8; d++)
        {
            int nx = tile.x + dx8[d], ny = tile.y + dy8[d];
            if (cachedMap != null && nx >= 0 && nx < cachedMap.width && ny >= 0 && ny < cachedMap.height && !cachedMap.IsLand(nx, ny))
            {
                seaDir += new Vector2(dx8[d], dy8[d]);
            }
        }
        if (seaDir.sqrMagnitude < 0.001f) seaDir = Vector2.down;
        seaDir = seaDir.normalized;
        //kıyıya paralel yön = deniz yönüne dik (saat yönünde 90 derece)
        Vector2 shoreDir = new Vector2(-seaDir.y, seaDir.x);

        ports.Add(new PortData
        {
            go             = go,
            dayRenderer    = daySR,
            nightRenderer  = nightSR,
            tileX          = tile.x,
            tileY          = tile.y,
            baseAlpha      = baseA,
            worldPos       = new Vector3(wx, wy, spriteZ),
            shoreDirection = shoreDir,
            seaDirection   = seaDir
        });
    }

    bool[,] BuildOceanMask(MapGenerator map)
    {
        bool[,] isOcean = new bool[map.width, map.height];
        Queue<Vector2Int> oceanQueue = new Queue<Vector2Int>();

        for (int x = 0; x < map.width; x++)
        {
            if (!map.IsLand(x, 0))              { isOcean[x, 0] = true;              oceanQueue.Enqueue(new Vector2Int(x, 0)); }
            if (!map.IsLand(x, map.height - 1)) { isOcean[x, map.height - 1] = true; oceanQueue.Enqueue(new Vector2Int(x, map.height - 1)); }
        }
        for (int y = 1; y < map.height - 1; y++)
        {
            if (!map.IsLand(0, y))              { isOcean[0, y] = true;              oceanQueue.Enqueue(new Vector2Int(0, y)); }
            if (!map.IsLand(map.width - 1, y))  { isOcean[map.width - 1, y] = true; oceanQueue.Enqueue(new Vector2Int(map.width - 1, y)); }
        }

        int[] odx = { 1, -1, 0, 0 };
        int[] ody = { 0, 0, 1, -1 };
        while (oceanQueue.Count > 0)
        {
            var pos = oceanQueue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + odx[i], ny = pos.y + ody[i];
                if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) continue;
                if (isOcean[nx, ny] || map.IsLand(nx, ny)) continue;
                isOcean[nx, ny] = true;
                oceanQueue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return isOcean;
    }

    bool IsAdjacentToWater(MapGenerator map, int x, int y)
    {
        return (!map.IsLand(x + 1, y) || !map.IsLand(x - 1, y) ||
                !map.IsLand(x, y + 1) || !map.IsLand(x, y - 1));
    }

    /// <summary>
    /// True if at least one 4-connected neighbor is ocean water (not an inland lake).
    /// </summary>
    bool IsAdjacentToOcean(MapGenerator map, int x, int y, bool[,] isOcean)
    {
        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };
        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx4[i], ny = y + dy4[i];
            if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) continue;
            if (!map.IsLand(nx, ny) && isOcean[nx, ny]) return true;
        }
        return false;
    }

    /// <summary>
    /// Checks that behind this shore tile there are enough tiles of the given biome,
    /// ensuring the port isn't on a thin sliver of coast.
    /// </summary>
    bool HasRegionBacking(MapGenerator map, int x, int y, int biome)
    {
        int count = 0;
        int r = portRegionBackingRadius;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            if (dx * dx + dy * dy > r * r) continue;
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) continue;
            if (map.IsLand(nx, ny) && map.GetBiome(nx, ny) == biome) count++;
        }
        int totalInCircle = 0;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
            if (dx * dx + dy * dy <= r * r) totalInCircle++;

        return count >= totalInCircle * 0.5f;
    }

    // =========================================================================
    // SHIP NAVIGATION GRID (downsampled)
    // =========================================================================

    /// <summary>
    /// Builds a coarse navigation grid for A* pathfinding.
    /// Each nav cell is shipPathGridStep × shipPathGridStep tiles.
    /// A cell is navigable only if EVERY tile in the cell is water AND
    /// every tile has at least shipLandClearance distance from any land.
    /// Also stores per-cell minimum land distance for A* cost biasing
    /// so ships strongly prefer routes that keep well away from shore.
    /// </summary>
    void BuildNavGrid(MapGenerator map)
    {
        int step = Mathf.Max(1, shipPathGridStep);
        navW = Mathf.CeilToInt((float)map.width / step);
        navH = Mathf.CeilToInt((float)map.height / step);
        navGrid = new bool[navW, navH];
        navLandDist = new int[navW, navH]; // minimum land distance in each cell

        // Build full-resolution land distance field via BFS
        int maxBfsDist = shipLandClearance * 3 + step; // propagate well beyond clearance for cost biasing
        int[,] landDist = new int[map.width, map.height];
        Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>();

        for (int x = 0; x < map.width; x++)
        for (int y = 0; y < map.height; y++)
        {
            if (map.IsLand(x, y))
            {
                landDist[x, y] = 0;
                bfsQueue.Enqueue(new Vector2Int(x, y));
            }
            else
            {
                landDist[x, y] = int.MaxValue;
            }
        }

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };
        while (bfsQueue.Count > 0)
        {
            var pos = bfsQueue.Dequeue();
            int d = landDist[pos.x, pos.y];
            if (d >= maxBfsDist) continue;
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) continue;
                if (landDist[nx, ny] <= d + 1) continue;
                landDist[nx, ny] = d + 1;
                bfsQueue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        // Build nav grid: check EVERY tile in the cell, not just center
        for (int gx = 0; gx < navW; gx++)
        for (int gy = 0; gy < navH; gy++)
        {
            int startX = gx * step;
            int startY = gy * step;
            int endX   = Mathf.Min(startX + step, map.width);
            int endY   = Mathf.Min(startY + step, map.height);

            bool passable = true;
            int minDist   = int.MaxValue;

            for (int tx = startX; tx < endX && passable; tx++)
            for (int ty = startY; ty < endY && passable; ty++)
            {
                if (map.IsLand(tx, ty))
                {
                    passable = false;
                    minDist  = 0;
                    break;
                }
                int d = landDist[tx, ty];
                if (d < minDist) minDist = d;
                if (d < shipLandClearance) passable = false;
            }

            navGrid[gx, gy]     = passable;
            navLandDist[gx, gy] = minDist;
        }

        // Corridor width enforcement: erode narrow passages.
        // A cell is only truly navigable if it has at least shipMinCorridorWidth
        // contiguous open cells in BOTH the X and Y axes (centered on itself).
        // This prevents ships from threading through 1-cell-wide gaps between
        // land masses or between land and the map edge.
        if (shipMinCorridorWidth > 1)
        {
            bool[,] eroded = new bool[navW, navH];
            int halfCorridor = shipMinCorridorWidth / 2;

            for (int gx = 0; gx < navW; gx++)
            for (int gy = 0; gy < navH; gy++)
            {
                if (!navGrid[gx, gy]) { eroded[gx, gy] = false; continue; }

                // Check horizontal span
                bool hOk = true;
                for (int dx = -halfCorridor; dx <= halfCorridor && hOk; dx++)
                {
                    int nx = gx + dx;
                    if (nx < 0 || nx >= navW || !navGrid[nx, gy]) hOk = false;
                }

                // Check vertical span
                bool vOk = true;
                for (int dy = -halfCorridor; dy <= halfCorridor && vOk; dy++)
                {
                    int ny = gy + dy;
                    if (ny < 0 || ny >= navH || !navGrid[gx, ny]) vOk = false;
                }

                eroded[gx, gy] = hOk && vOk;
            }

            navGrid = eroded;
        }
    }

    // =========================================================================
    // SHIP SPAWNING & UPDATE
    // =========================================================================

    void UpdateShips(float lightingRatio)
    {
        if (cachedMap == null || ports.Count == 0) return;
        if (shipSpritesDay == null || shipSpritesDay.Count == 0) return;

        // Spawn timer — asenkron A* ile spawn et
        shipSpawnTimer -= Time.deltaTime;
        if (shipSpawnTimer <= 0f)
        {
            shipSpawnTimer = shipSpawnInterval;
            if (activeShips.Count < maxActiveShips && !isSpawnPathPending)
            {
                if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
                spawnCoroutine = StartCoroutine(TrySpawnShipAsync());
            }
        }

        // Update each ship
        for (int i = activeShips.Count - 1; i >= 0; i--)
        {
            ShipInstance ship = activeShips[i];

            switch (ship.state)
            {
                case ShipState.Arriving:
                    MoveAlongPath(ship);
                    if (ship.pathIndex >= ship.path.Count)
                    {
                        ship.state     = ShipState.Waiting;
                        ship.waitTimer = Random.Range(shipWaitTimeRange.x, shipWaitTimeRange.y);
                    }
                    break;

                case ShipState.Waiting:
                    ship.waitTimer -= Time.deltaTime;
                    if (ship.waitTimer <= 0f && !isDepartPathPending)
                    {
                        // Ayrılış: asenkron A* ile yol hesapla
                        if (departCoroutine != null) StopCoroutine(departCoroutine);
                        departCoroutine = StartCoroutine(DepartShipAsync(ship));
                    }
                    break;

                case ShipState.Departing:
                    MoveAlongPath(ship);
                    if (ship.pathIndex >= ship.path.Count)
                        ship.state = ShipState.Done;
                    break;

                case ShipState.Done:
                    break;
            }

            // Cleanup finished ships
            if (ship.state == ShipState.Done)
            {
                decorObjects.Remove(ship.go);
                if (ship.go != null) Destroy(ship.go);
                activeShips.RemoveAt(i);
            }
        }

        //köpük sprite'larını güncelle
        UpdateFoamSprites();
    }

    // =========================================================================
    // BOW WAVE — geminin burun ucundan nokta nokta köpük
    // =========================================================================

    private class FoamDot
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float lifetime;
        public float maxLifetime;
        public Vector3 velocity; //yana sürüklenme
    }

    private List<FoamDot> foamDots = new List<FoamDot>();
    private Queue<GameObject> foamPool = new Queue<GameObject>();
    private Sprite foamDotSprite;

    Sprite GetFoamDotSprite()
    {
        if (foamDotSprite != null) return foamDotSprite;
        int size = 6;
        Texture2D tex = new Texture2D(size, size);
        float c = (size - 1) * 0.5f;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        foamDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return foamDotSprite;
    }

    void CreateBowWave(ShipInstance ship) { }

    void UpdateFoamSprites()
    {
        float dt = Time.deltaTime;

        //hareket eden gemilerden burun ucunda köpük dot'ları spawn
        for (int i = 0; i < activeShips.Count; i++)
        {
            ShipInstance ship = activeShips[i];
            if (ship.go == null) continue;
            if (ship.state != ShipState.Arriving && ship.state != ShipState.Departing) continue;

            ship.wakeTimer -= dt;
            if (ship.wakeTimer > 0f) continue;
            ship.wakeTimer = 0.015f;

            float bowAngle = (ship.currentAngle + 90f) * Mathf.Deg2Rad;
            Vector2 fwd = new Vector2(Mathf.Cos(bowAngle), Mathf.Sin(bowAngle));
            Vector2 right = new Vector2(-fwd.y, fwd.x);
            Vector3 shipPos = ship.go.transform.position;

            //burun ucunun iki kenarından dot spawn — yana doğru sürüklenecek
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 dotPos = shipPos
                    + new Vector3(fwd.x, fwd.y, 0f) * 0.15f * ship.scale
                    + new Vector3(right.x, right.y, 0f) * side * 0.055f * ship.scale;
                dotPos.z = shipPos.z - 0.05f; //geminin ALTINDA değil ÜSTÜNDE
                dotPos.x += Random.Range(-0.005f, 0.005f);
                dotPos.y += Random.Range(-0.005f, 0.005f);

                //yana doğru sürüklenme hızı
                Vector3 sideVel = new Vector3(right.x, right.y, 0f) * side * 0.08f;
                SpawnFoamDot(dotPos, sideVel);
            }
        }

        //dot'ları güncelle — yana sürüklen + fade out
        for (int i = foamDots.Count - 1; i >= 0; i--)
        {
            FoamDot fd = foamDots[i];
            fd.lifetime -= dt;

            if (fd.lifetime <= 0f)
            {
                fd.go.SetActive(false);
                foamPool.Enqueue(fd.go);
                foamDots.RemoveAt(i);
                continue;
            }

            //yana sürüklen
            fd.go.transform.position += fd.velocity * dt;
            fd.velocity *= (1f - dt * 2f); //yavaşla

            float t = 1f - (fd.lifetime / fd.maxLifetime);
            Color col = fd.sr.color;
            col.a = 0.7f * (1f - t);
            fd.sr.color = col;
        }
    }

    void SpawnFoamDot(Vector3 pos, Vector3 velocity)
    {
        GameObject go;
        SpriteRenderer sr;

        if (foamPool.Count > 0)
        {
            go = foamPool.Dequeue();
            go.SetActive(true);
            sr = go.GetComponent<SpriteRenderer>();
        }
        else
        {
            go = new GameObject("FoamDot");
            go.transform.SetParent(transform);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetFoamDotSprite();
            sr.sortingOrder = 7; //gemilerin altında, denizin üstünde
        }

        float s = Random.Range(0.08f, 0.13f);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(s, s, 1f);
        sr.color = new Color(1f, 1f, 1f, 0.7f);

        foamDots.Add(new FoamDot
        {
            go = go,
            sr = sr,
            lifetime = 1.5f,
            maxLifetime = 1.5f,
            velocity = velocity
        });
    }

    // =========================================================================
    // KÖPÜK İZİ (WAKE) — geminin değdiği yerlerde oluşan sabit köpük
    // =========================================================================

    void MoveAlongPath(ShipInstance ship)
    {
        if (ship.path == null || ship.path.Count < 2) { ship.pathIndex = ship.path != null ? ship.path.Count : 0; return; }

        float totalSegments = ship.path.Count - 1;
        float distThisFrame = ship.speed * Time.deltaTime;

        //yakındaki gemilere çarpmamak için yavaşla (her 10 frame'de kontrol)
        if (Time.frameCount % 10 == 0)
        {
            float minShipDist = 0.15f;
            ship.proximitySlowdown = 1f;
            for (int s = 0; s < activeShips.Count; s++)
            {
                ShipInstance other = activeShips[s];
                if (other == ship || other.go == null) continue;
                float d = Vector3.Distance(ship.go.transform.position, other.go.transform.position);
                if (d < minShipDist)
                {
                    ship.proximitySlowdown = Mathf.Clamp01(d / minShipDist);
                    break;
                }
            }
        }
        distThisFrame *= ship.proximitySlowdown;

        // Estimate segment length for t-step conversion
        while (distThisFrame > 0f && ship.pathIndex < totalSegments)
        {
            Vector3 segStart = ship.path[ship.pathIndex];
            Vector3 segEnd   = ship.path[ship.pathIndex + 1];
            float segLen     = Vector3.Distance(segStart, segEnd);
            if (segLen < 0.0001f) { ship.pathIndex++; ship.segmentT = 0f; continue; }

            float remainingInSeg = (1f - ship.segmentT) * segLen;
            if (distThisFrame < remainingInSeg)
            {
                ship.segmentT += distThisFrame / segLen;
                distThisFrame = 0f;
            }
            else
            {
                distThisFrame -= remainingInSeg;
                ship.pathIndex++;
                ship.segmentT = 0f;
            }
        }

        if (ship.pathIndex >= totalSegments)
        {
            ship.go.transform.position = ship.path[ship.path.Count - 1];
            ship.pathIndex = ship.path.Count; // signal done
            return;
        }

        //lineer interpolasyon + yumuşak dönüş
        Vector3 pos = Vector3.Lerp(ship.path[ship.pathIndex], ship.path[ship.pathIndex + 1], ship.segmentT);
        ship.go.transform.position = pos;

        //hareket yönüne göre yumuşak dönüş
        Vector3 dir = ship.path[ship.pathIndex + 1] - ship.path[ship.pathIndex];
        if (dir.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            ship.currentAngle = Mathf.MoveTowardsAngle(ship.currentAngle, targetAngle, shipTurnSpeed * Time.deltaTime);
            ship.go.transform.rotation = Quaternion.Euler(0f, 0f, ship.currentAngle);
        }
    }

    /// <summary>Catmull-Rom spline position at parameter t ∈ [0,1].</summary>
    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>Catmull-Rom spline tangent (derivative) at parameter t ∈ [0,1].</summary>
    Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return 0.5f * (
            (-p0 + p2) +
            (4f * p0 - 10f * p1 + 8f * p2 - 2f * p3) * t +
            (-3f * p0 + 9f * p1 - 9f * p2 + 3f * p3) * t2);
    }

    void RotateShipToward(ShipInstance ship, Vector3 direction)
    {
        // Kept for initial facing setup; runtime rotation now uses smooth lerp in MoveAlongPath
        if (direction.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        ship.currentAngle = angle - 90f;
        ship.go.transform.rotation = Quaternion.Euler(0f, 0f, ship.currentAngle);
    }

    /// <summary>
    /// Asenkron gemi spawn — A* hesaplamasını birden fazla frame'e yayar.
    /// </summary>
    IEnumerator TrySpawnShipAsync()
    {
        isSpawnPathPending = true;

        if (ports.Count == 0) { isSpawnPathPending = false; yield break; }

        //aynı limana giden veya bekleyen başka gemi yoksa spawn et
        int portIdx = Random.Range(0, ports.Count);

        //bu limanda zaten gemi var mı kontrol et
        bool portOccupied = false;
        for (int s = 0; s < activeShips.Count; s++)
        {
            if (activeShips[s].portIndex == portIdx &&
                (activeShips[s].state == ShipState.Arriving || activeShips[s].state == ShipState.Waiting))
            {
                portOccupied = true;
                break;
            }
        }

        //meşgulse diğer limanı dene
        if (portOccupied)
        {
            portIdx = (portIdx + 1) % ports.Count;
            for (int s = 0; s < activeShips.Count; s++)
            {
                if (activeShips[s].portIndex == portIdx &&
                    (activeShips[s].state == ShipState.Arriving || activeShips[s].state == ShipState.Waiting))
                { isSpawnPathPending = false; yield break; } //iki liman da meşgul
            }
        }

        PortData port = ports[portIdx];

        Vector3 origin = GetRandomOceanEdgePoint();

        //dock noktası — limanın deniz tarafında güvenli mesafede
        Vector3 dockPoint = GetSafeDockPoint(port);

        //A* ile dock noktasına git — asenkron hesaplama
        List<Vector3> path = null;
        yield return StartCoroutine(FindShipPathAsync(origin, dockPoint, (result) => { path = result; }));

        if (path == null || path.Count == 0) { isSpawnPathPending = false; yield break; }

        // Coroutine sırasında max ship sayısı aşılmış olabilir
        if (activeShips.Count >= maxActiveShips) { isSpawnPathPending = false; yield break; }

        // Create ship GO
        int spriteIdx = Random.Range(0, shipSpritesDay.Count);
        Sprite daySprite = shipSpritesDay[spriteIdx];
        if (daySprite == null) { isSpawnPathPending = false; yield break; }

        float scale = Random.Range(shipScaleRange.x, shipScaleRange.y);
        float baseA = Random.Range(0.85f, 1f);
        int sortOrder = 8;

        GameObject go = new GameObject("Ship");
        go.transform.SetParent(transform);
        go.transform.position   = origin;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer daySR = go.AddComponent<SpriteRenderer>();
        daySR.sprite       = daySprite;
        daySR.sortingOrder = sortOrder;
        daySR.color        = new Color(1f, 1f, 1f, baseA);

        SpriteRenderer nightSR = null;
        if (shipSpritesNight != null && spriteIdx < shipSpritesNight.Count &&
            shipSpritesNight[spriteIdx] != null)
        {
            GameObject nightGo = new GameObject("ShipNight");
            nightGo.transform.SetParent(go.transform, false);
            nightGo.transform.localPosition = Vector3.zero;
            nightGo.transform.localScale    = Vector3.one;
            nightGo.transform.localRotation = Quaternion.identity;

            nightSR              = nightGo.AddComponent<SpriteRenderer>();
            nightSR.sprite       = shipSpritesNight[spriteIdx];
            nightSR.sortingOrder = sortOrder + 1;
            nightSR.color        = new Color(1f, 1f, 1f, 0f);
        }

        // Apply current day/night ratio immediately
        float ratio = (dayNight != null) ? dayNight.LightingRatio : 0f;
        daySR.color = new Color(1f, 1f, 1f, baseA * (1f - ratio));
        if (nightSR != null)
            nightSR.color = new Color(1f, 1f, 1f, baseA * ratio);

        // Set initial facing angle from first path direction
        float initAngle = 0f;
        if (path.Count > 1)
        {
            Vector3 dir = path[1] - path[0];
            if (dir.sqrMagnitude > 0.001f)
            {
                initAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, initAngle);
            }
        }

        decorObjects.Add(go);
        activeShips.Add(new ShipInstance
        {
            go            = go,
            dayRenderer   = daySR,
            nightRenderer = nightSR,
            baseAlpha     = baseA,
            scale         = scale,
            state         = ShipState.Arriving,
            path          = path,
            pathIndex     = 0,
            segmentT      = 0f,
            waitTimer     = 0f,
            portIndex     = portIdx,
            speed         = shipSpeed * Random.Range(0.8f, 1.2f),
            currentAngle  = initAngle
        });

        //bow wave köpük izi
        CreateBowWave(activeShips[activeShips.Count - 1]);

        isSpawnPathPending = false;
    }

    /// <summary>
    /// Asenkron gemi ayrılışı — A* hesaplamasını birden fazla frame'e yayar.
    /// </summary>
    IEnumerator DepartShipAsync(ShipInstance ship)
    {
        isDepartPathPending = true;

        PortData depPort = ports[ship.portIndex];
        Vector2 seaDir = depPort.seaDirection;

        //deniz yönünde güvenli bir noktaya çık
        Vector3 seaPoint = ship.go.transform.position + new Vector3(seaDir.x, seaDir.y, 0f) * 0.5f;
        seaPoint = PushToWater(seaPoint, seaDir);

        Vector3 exitPoint = GetRandomOceanEdgePoint();

        //A* ile mevcut konumdan okyanus kenarına — asenkron
        Vector3 shipPos = ship.go.transform.position;
        List<Vector3> toExit = null;
        yield return StartCoroutine(FindShipPathAsync(shipPos, exitPoint, (result) => { toExit = result; }));

        // Gemi beklerken yok edilmiş olabilir
        if (ship.go == null || ship.state != ShipState.Waiting) { isDepartPathPending = false; yield break; }

        List<Vector3> depPath = new List<Vector3>();
        if (toExit != null && toExit.Count > 0)
            depPath.AddRange(toExit);

        if (depPath.Count > 0)
        {
            ship.path      = depPath;
            ship.pathIndex = 0;
            ship.segmentT  = 0f;
            ship.state     = ShipState.Departing;
        }
        else
        {
            ship.state = ShipState.Done;
        }

        isDepartPathPending = false;
    }

    /// <summary>
    /// Returns a random world-space point on the ocean edge of the map.
    /// </summary>
    Vector3 GetRandomOceanEdgePoint()
    {
        float margin = 0.1f; // small offset inside the map boundary
        int side = Random.Range(0, 4);
        float wx, wy;

        switch (side)
        {
            case 0: // left
                wx = transform.position.x - cachedHalfW - margin;
                wy = transform.position.y + Random.Range(-cachedHalfH * 0.8f, cachedHalfH * 0.8f);
                break;
            case 1: // right
                wx = transform.position.x + cachedHalfW + margin;
                wy = transform.position.y + Random.Range(-cachedHalfH * 0.8f, cachedHalfH * 0.8f);
                break;
            case 2: // bottom
                wx = transform.position.x + Random.Range(-cachedHalfW * 0.8f, cachedHalfW * 0.8f);
                wy = transform.position.y - cachedHalfH - margin;
                break;
            default: // top
                wx = transform.position.x + Random.Range(-cachedHalfW * 0.8f, cachedHalfW * 0.8f);
                wy = transform.position.y + cachedHalfH + margin;
                break;
        }

        return new Vector3(wx, wy, spriteZ);
    }

    /// <summary>
    /// Geminin limana yanaşması için 3 waypoint döner:
    /// 1. Manevra noktası — denizde, kıyıdan uzakta, geniş dönüş için
    /// 2. Hizalama noktası — kıyıya paralel hizalanma başlangıcı
    /// 3. Yanaşma noktası — limanın hemen yanında, kıyıya paralel
    /// </summary>
    /// <summary>
    /// Yer şekillerine bakarak en uygun yaklaşma rotasını belirler.
    /// Kıyının her iki yönünü de dener, açık su olan tarafı seçer.
    /// dockPoint: geminin yanaşacağı son nokta (kıyıya paralel)
    /// approachPoint: geminin A* ile ulaşacağı yaklaşma noktası (dock'tan kıyı boyunca uzakta)
    /// </summary>
    bool FindBestApproach(PortData port, out Vector3 dockPoint, out Vector3 approachPoint)
    {
        dockPoint = GetSafeDockPoint(port);
        approachPoint = dockPoint;

        Vector2 shore = port.shoreDirection;
        float approachDist = 0.8f;

        //her iki yönü de dene — hangisi açık su ise oradan yaklaş
        Vector3 candidateA = dockPoint + new Vector3(shore.x, shore.y, 0f) * approachDist;
        Vector3 candidateB = dockPoint + new Vector3(-shore.x, -shore.y, 0f) * approachDist;

        bool aIsWater = IsPointInWater(candidateA) && !SegmentCrossesLand(dockPoint, candidateA);
        bool bIsWater = IsPointInWater(candidateB) && !SegmentCrossesLand(dockPoint, candidateB);

        if (aIsWater && bIsWater)
        {
            //ikisi de açık — rastgele seç
            approachPoint = Random.value > 0.5f ? candidateA : candidateB;
            return true;
        }
        else if (aIsWater)
        {
            approachPoint = candidateA;
            return true;
        }
        else if (bIsWater)
        {
            approachPoint = candidateB;
            return true;
        }

        //her iki yön de karaya çarpıyor — daha kısa mesafe dene
        float shortDist = 0.4f;
        candidateA = dockPoint + new Vector3(shore.x, shore.y, 0f) * shortDist;
        candidateB = dockPoint + new Vector3(-shore.x, -shore.y, 0f) * shortDist;

        aIsWater = IsPointInWater(candidateA) && !SegmentCrossesLand(dockPoint, candidateA);
        bIsWater = IsPointInWater(candidateB) && !SegmentCrossesLand(dockPoint, candidateB);

        if (aIsWater)
        {
            approachPoint = candidateA;
            return true;
        }
        if (bIsWater)
        {
            approachPoint = candidateB;
            return true;
        }

        return false; //hiçbir yön uygun değil
    }

    /// <summary>
    /// Verilen world pozisyonun suda olup olmadığını kontrol eder.
    /// </summary>
    bool IsPointInWater(Vector3 worldPos)
    {
        if (cachedMap == null) return true;
        int tileX = Mathf.RoundToInt((worldPos.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
        int tileY = Mathf.RoundToInt((worldPos.y - transform.position.y + cachedHalfH) * pixelsPerUnit);
        if (tileX < 0 || tileX >= cachedMap.width || tileY < 0 || tileY >= cachedMap.height) return true;
        return !cachedMap.IsLand(tileX, tileY);
    }

    /// <summary>
    /// Limanın deniz tarafında, karadan güvenli mesafede bir dock noktası bulur.
    /// Deniz yönünde adım adım ilerleyip su olan en yakın noktayı döner.
    /// </summary>
    Vector3 GetSafeDockPoint(PortData port)
    {
        Vector3 pos = port.worldPos;
        Vector2 sea = port.seaDirection;

        //deniz yönünde adım adım ilerle, suya ulaşana kadar
        for (int i = 1; i <= 30; i++)
        {
            Vector3 candidate = pos + new Vector3(sea.x, sea.y, 0f) * (i * 0.05f);
            int tileX = Mathf.RoundToInt((candidate.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
            int tileY = Mathf.RoundToInt((candidate.y - transform.position.y + cachedHalfH) * pixelsPerUnit);

            if (cachedMap != null && tileX >= 0 && tileX < cachedMap.width && tileY >= 0 && tileY < cachedMap.height
                && !cachedMap.IsLand(tileX, tileY))
            {
                //suya ulaştıktan sonra biraz daha aç
                return candidate + new Vector3(sea.x, sea.y, 0f) * 0.15f;
            }
        }

        //fallback
        return pos + new Vector3(sea.x, sea.y, 0f) * 0.3f;
    }

    List<Vector3> GetDockingWaypoints(PortData port)
    {
        Vector3 portW = port.worldPos;
        Vector2 sea = port.seaDirection;
        Vector2 shore = port.shoreDirection;

        //rastgele sağdan veya soldan yanaşsın
        if (Random.value > 0.5f) shore = -shore;

        float dockOffset = 0.15f;
        float alignDist = 0.8f;
        float maneuverDist = 0.6f;

        Vector3 dockPoint = portW + new Vector3(sea.x, sea.y, 0f) * dockOffset;
        Vector3 alignPoint = dockPoint + new Vector3(shore.x, shore.y, 0f) * alignDist;
        Vector3 maneuverPoint = alignPoint + new Vector3(sea.x, sea.y, 0f) * maneuverDist;

        //karaya düşen waypoint'leri denize it
        alignPoint = PushToWater(alignPoint, sea);
        maneuverPoint = PushToWater(maneuverPoint, sea);

        return new List<Vector3> { maneuverPoint, alignPoint, dockPoint };
    }

    /// <summary>
    /// Geminin ayrılması için 3 waypoint döner (yanaşmanın tersi):
    /// Limandan kıyıya paralel ayrılıp denize açılır.
    /// </summary>
    List<Vector3> GetUndockingWaypoints(PortData port)
    {
        Vector3 portW = port.worldPos;
        Vector2 sea = port.seaDirection;
        Vector2 shore = port.shoreDirection;

        if (Random.value > 0.5f) shore = -shore;

        float dockOffset = 0.15f;
        float departDist = 0.8f;
        float seaDist = 0.6f;

        Vector3 dockPoint = portW + new Vector3(sea.x, sea.y, 0f) * dockOffset;
        Vector3 departPoint = dockPoint + new Vector3(shore.x, shore.y, 0f) * departDist;
        Vector3 seaPoint = departPoint + new Vector3(sea.x, sea.y, 0f) * seaDist;

        //karaya düşen waypoint'leri denize it
        departPoint = PushToWater(departPoint, sea);
        seaPoint = PushToWater(seaPoint, sea);

        return new List<Vector3> { dockPoint, departPoint, seaPoint };
    }

    /// <summary>
    /// Eğer verilen world pozisyon karaya düşüyorsa, deniz yönünde iterek suya taşır.
    /// </summary>
    Vector3 PushToWater(Vector3 worldPos, Vector2 seaDir)
    {
        if (cachedMap == null) return worldPos;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            int tileX = Mathf.RoundToInt((worldPos.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
            int tileY = Mathf.RoundToInt((worldPos.y - transform.position.y + cachedHalfH) * pixelsPerUnit);

            if (tileX < 0 || tileX >= cachedMap.width || tileY < 0 || tileY >= cachedMap.height)
                return worldPos;

            if (!cachedMap.IsLand(tileX, tileY))
                return worldPos;

            worldPos += new Vector3(seaDir.x, seaDir.y, 0f) * 0.1f;
        }

        Debug.LogWarning("MapDecorPlacer: PushToWater başarısız — waypoint hâlâ karada!");
        return worldPos;
    }

    // =========================================================================
    // A* PATHFINDING ON NAV GRID
    // =========================================================================

    /// <summary>
    /// Finds a path in world-space from 'from' to 'to', navigating around the island.
    /// Uses A* on the downsampled navGrid, simplifies collinear points, then applies
    /// Chaikin corner-cutting subdivision to produce smooth, round sailing curves.
    /// </summary>
    List<Vector3> FindShipPath(Vector3 from, Vector3 to)
    {
        if (cachedMap == null || navGrid == null) return null;

        int step = Mathf.Max(1, shipPathGridStep);

        // Convert world → tile → nav grid coords
        Vector2Int fromTile = WorldToTile(from);
        Vector2Int toTile   = WorldToTile(to);

        int fromGx = Mathf.Clamp(fromTile.x / step, 0, navW - 1);
        int fromGy = Mathf.Clamp(fromTile.y / step, 0, navH - 1);
        int toGx   = Mathf.Clamp(toTile.x / step, 0, navW - 1);
        int toGy   = Mathf.Clamp(toTile.y / step, 0, navH - 1);

        // Snap start/end to nearest navigable cell if they're on land
        fromGx = FindNearestNavigable(fromGx, fromGy, out fromGy);
        int tempToGy = toGy;
        toGx   = FindNearestNavigable(toGx, tempToGy, out toGy);

        if (fromGx < 0 || toGx < 0) return null;

        // A* search
        List<Vector2Int> gridPath = AStarSearch(fromGx, fromGy, toGx, toGy);
        if (gridPath == null || gridPath.Count == 0) return null;

        // Convert grid path → world positions
        List<Vector3> worldPath = new List<Vector3>();
        worldPath.Add(from);

        for (int i = 0; i < gridPath.Count; i++)
        {
            int tileX = gridPath[i].x * step + step / 2;
            int tileY = gridPath[i].y * step + step / 2;
            tileX = Mathf.Clamp(tileX, 0, cachedMap.width - 1);
            tileY = Mathf.Clamp(tileY, 0, cachedMap.height - 1);

            float wx = transform.position.x + (tileX / pixelsPerUnit) - cachedHalfW;
            float wy = transform.position.y + (tileY / pixelsPerUnit) - cachedHalfH;
            worldPath.Add(new Vector3(wx, wy, spriteZ));
        }

        worldPath.Add(to);

        // 1. Simplify: remove collinear points
        worldPath = SimplifyPath(worldPath);

        // Chaikin smoothing devre dışı — karadan geçen rotalar yaratıyordu.
        // CatmullRom spline hareket sırasında yeterli yumuşaklık sağlıyor.

        return worldPath;
    }

    /// <summary>
    /// FindShipPath'in asenkron versiyonu — A* hesaplamasını frame'lere yayar.
    /// Sonucu callback ile döner.
    /// </summary>
    IEnumerator FindShipPathAsync(Vector3 from, Vector3 to, System.Action<List<Vector3>> callback)
    {
        if (cachedMap == null || navGrid == null) { callback(null); yield break; }

        int step = Mathf.Max(1, shipPathGridStep);

        // Convert world → tile → nav grid coords
        Vector2Int fromTile = WorldToTile(from);
        Vector2Int toTile   = WorldToTile(to);

        int fromGx = Mathf.Clamp(fromTile.x / step, 0, navW - 1);
        int fromGy = Mathf.Clamp(fromTile.y / step, 0, navH - 1);
        int toGx   = Mathf.Clamp(toTile.x / step, 0, navW - 1);
        int toGy   = Mathf.Clamp(toTile.y / step, 0, navH - 1);

        // Snap start/end to nearest navigable cell if they're on land
        fromGx = FindNearestNavigable(fromGx, fromGy, out fromGy);
        int tempToGy = toGy;
        toGx   = FindNearestNavigable(toGx, tempToGy, out toGy);

        if (fromGx < 0 || toGx < 0) { callback(null); yield break; }

        // A* search — asenkron
        List<Vector2Int> gridPath = null;
        yield return StartCoroutine(AStarSearchAsync(fromGx, fromGy, toGx, toGy, (result) => { gridPath = result; }));

        if (gridPath == null || gridPath.Count == 0) { callback(null); yield break; }

        // Convert grid path → world positions
        List<Vector3> worldPath = new List<Vector3>();
        worldPath.Add(from);

        for (int i = 0; i < gridPath.Count; i++)
        {
            int tileX = gridPath[i].x * step + step / 2;
            int tileY = gridPath[i].y * step + step / 2;
            tileX = Mathf.Clamp(tileX, 0, cachedMap.width - 1);
            tileY = Mathf.Clamp(tileY, 0, cachedMap.height - 1);

            float wx = transform.position.x + (tileX / pixelsPerUnit) - cachedHalfW;
            float wy = transform.position.y + (tileY / pixelsPerUnit) - cachedHalfH;
            worldPath.Add(new Vector3(wx, wy, spriteZ));
        }

        worldPath.Add(to);

        // Simplify: remove collinear points
        worldPath = SimplifyPath(worldPath);

        callback(worldPath);
    }

    /// <summary>
    /// A* aramasının asenkron versiyonu — frame başına ASTAR_ITERATIONS_PER_FRAME iterasyon işler.
    /// </summary>
    IEnumerator AStarSearchAsync(int sx, int sy, int ex, int ey, System.Action<List<Vector2Int>> callback)
    {
        // Directions: 8-connected for smoother paths
        int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        float[] cost8 = { 1f, 1f, 1f, 1f, 1.414f, 1.414f, 1.414f, 1.414f };

        int maxIterations = navW * navH; // safety cap
        float[,] gScore = new float[navW, navH];
        int[,] cameFromX = new int[navW, navH];
        int[,] cameFromY = new int[navW, navH];
        bool[,] closed   = new bool[navW, navH];

        for (int x = 0; x < navW; x++)
        for (int y = 0; y < navH; y++)
        {
            gScore[x, y] = float.MaxValue;
            cameFromX[x, y] = -1;
            cameFromY[x, y] = -1;
        }

        gScore[sx, sy] = 0f;

        var open = new SortedList<float, Vector2Int>(new DuplicateKeyComparer());
        float h0 = Heuristic(sx, sy, ex, ey);
        open.Add(h0, new Vector2Int(sx, sy));

        int iterations = 0;
        int frameIterations = 0;

        while (open.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            frameIterations++;

            // Frame başına iterasyon limitine ulaşınca sonraki frame'e geç
            if (frameIterations >= ASTAR_ITERATIONS_PER_FRAME)
            {
                frameIterations = 0;
                yield return null;
            }

            var current = open.Values[0];
            open.RemoveAt(0);

            int cx = current.x, cy = current.y;
            if (cx == ex && cy == ey)
            {
                callback(ReconstructPath(cameFromX, cameFromY, sx, sy, ex, ey));
                yield break;
            }

            if (closed[cx, cy]) continue;
            closed[cx, cy] = true;

            for (int i = 0; i < 8; i++)
            {
                int nx = cx + dx8[i], ny = cy + dy8[i];
                if (nx < 0 || nx >= navW || ny < 0 || ny >= navH) continue;
                if (!navGrid[nx, ny] || closed[nx, ny]) continue;

                // For diagonals, ensure both adjacent axis-aligned cells are navigable
                if (i >= 4)
                {
                    if (!navGrid[cx + dx8[i], cy] || !navGrid[cx, cy + dy8[i]])
                        continue;
                }

                float tentG = gScore[cx, cy] + cost8[i];

                // Shore avoidance: cells closer to land are penalized
                if (shipShoreAvoidanceWeight > 0f && navLandDist != null)
                {
                    int ld = navLandDist[nx, ny];
                    if (ld < shipLandClearance * 3)
                    {
                        float proximity = 1f - ((float)ld / (shipLandClearance * 3));
                        tentG += proximity * proximity * shipShoreAvoidanceWeight * cost8[i];
                    }
                }

                if (tentG >= gScore[nx, ny]) continue;

                gScore[nx, ny]    = tentG;
                cameFromX[nx, ny] = cx;
                cameFromY[nx, ny] = cy;
                float f = tentG + Heuristic(nx, ny, ex, ey);
                open.Add(f, new Vector2Int(nx, ny));
            }
        }

        callback(null); // no path found
    }

    Vector2Int WorldToTile(Vector3 worldPos)
    {
        float localX = worldPos.x - transform.position.x + cachedHalfW;
        float localY = worldPos.y - transform.position.y + cachedHalfH;
        int tx = Mathf.RoundToInt(localX * pixelsPerUnit);
        int ty = Mathf.RoundToInt(localY * pixelsPerUnit);
        return new Vector2Int(
            Mathf.Clamp(tx, 0, cachedMap.width - 1),
            Mathf.Clamp(ty, 0, cachedMap.height - 1));
    }

    int FindNearestNavigable(int gx, int gy, out int outGy)
    {
        // Check the cell itself first
        if (gx >= 0 && gx < navW && gy >= 0 && gy < navH && navGrid[gx, gy])
        { outGy = gy; return gx; }

        // BFS outward to find nearest navigable cell
        int searchRadius = Mathf.Max(navW, navH) / 2;
        for (int r = 1; r <= searchRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // only perimeter
                int nx = gx + dx, ny = gy + dy;
                if (nx < 0 || nx >= navW || ny < 0 || ny >= navH) continue;
                if (navGrid[nx, ny])
                { outGy = ny; return nx; }
            }
        }

        outGy = gy;
        return -1;
    }

    List<Vector2Int> AStarSearch(int sx, int sy, int ex, int ey)
    {
        // Directions: 8-connected for smoother paths
        int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        float[] cost8 = { 1f, 1f, 1f, 1f, 1.414f, 1.414f, 1.414f, 1.414f };

        int maxIterations = navW * navH; // safety cap
        float[,] gScore = new float[navW, navH];
        int[,] cameFromX = new int[navW, navH];
        int[,] cameFromY = new int[navW, navH];
        bool[,] closed   = new bool[navW, navH];

        for (int x = 0; x < navW; x++)
        for (int y = 0; y < navH; y++)
        {
            gScore[x, y] = float.MaxValue;
            cameFromX[x, y] = -1;
            cameFromY[x, y] = -1;
        }

        gScore[sx, sy] = 0f;

        // Min-heap approximation using a sorted list (good enough for the small nav grid)
        var open = new SortedList<float, Vector2Int>(new DuplicateKeyComparer());
        float h0 = Heuristic(sx, sy, ex, ey);
        open.Add(h0, new Vector2Int(sx, sy));

        int iterations = 0;
        while (open.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            var currentKey = open.Keys[0];
            var current    = open.Values[0];
            open.RemoveAt(0);

            int cx = current.x, cy = current.y;
            if (cx == ex && cy == ey)
                return ReconstructPath(cameFromX, cameFromY, sx, sy, ex, ey);

            if (closed[cx, cy]) continue;
            closed[cx, cy] = true;

            for (int i = 0; i < 8; i++)
            {
                int nx = cx + dx8[i], ny = cy + dy8[i];
                if (nx < 0 || nx >= navW || ny < 0 || ny >= navH) continue;
                if (!navGrid[nx, ny] || closed[nx, ny]) continue;

                // For diagonals, ensure both adjacent axis-aligned cells are navigable
                if (i >= 4)
                {
                    if (!navGrid[cx + dx8[i], cy] || !navGrid[cx, cy + dy8[i]])
                        continue;
                }

                float tentG = gScore[cx, cy] + cost8[i];

                // Shore avoidance: cells closer to land are penalized
                // navLandDist stores the minimum land distance for each cell
                if (shipShoreAvoidanceWeight > 0f && navLandDist != null)
                {
                    int ld = navLandDist[nx, ny];
                    if (ld < shipLandClearance * 3)
                    {
                        // Inverse: closer to shore = higher penalty
                        float proximity = 1f - ((float)ld / (shipLandClearance * 3));
                        tentG += proximity * proximity * shipShoreAvoidanceWeight * cost8[i];
                    }
                }

                if (tentG >= gScore[nx, ny]) continue;

                gScore[nx, ny]    = tentG;
                cameFromX[nx, ny] = cx;
                cameFromY[nx, ny] = cy;
                float f = tentG + Heuristic(nx, ny, ex, ey);
                open.Add(f, new Vector2Int(nx, ny));
            }
        }

        return null; // no path found
    }

    float Heuristic(int ax, int ay, int bx, int by)
    {
        // Octile distance for 8-connected grid
        int dx = Mathf.Abs(ax - bx), dy = Mathf.Abs(ay - by);
        return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
    }

    List<Vector2Int> ReconstructPath(int[,] cameFromX, int[,] cameFromY,
                                     int sx, int sy, int ex, int ey)
    {
        var path = new List<Vector2Int>();
        int cx = ex, cy = ey;
        while (cx != sx || cy != sy)
        {
            path.Add(new Vector2Int(cx, cy));
            int px = cameFromX[cx, cy], py = cameFromY[cx, cy];
            if (px < 0)
            {
                // Should not happen, but safety
                path.Clear();
                return path;
            }
            cx = px; cy = py;
        }
        path.Add(new Vector2Int(sx, sy));
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Removes intermediate waypoints that are roughly collinear to produce smoother paths.
    /// </summary>
    List<Vector3> SimplifyPath(List<Vector3> path)
    {
        if (path.Count <= 2) return path;

        var simplified = new List<Vector3> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector3 prev = simplified[simplified.Count - 1];
            Vector3 next = path[i + 1];
            Vector3 curr = path[i];

            // Keep waypoints where direction changes noticeably (lower threshold = keep more)
            // 0.95 daha muhafazakar — kıyıdan kaçınan waypoint'leri korur
            Vector3 d1 = (curr - prev).normalized;
            Vector3 d2 = (next - curr).normalized;
            if (Vector3.Dot(d1, d2) < 0.95f)
                simplified.Add(curr);
        }
        simplified.Add(path[path.Count - 1]);
        return simplified;
    }

    /// <summary>
    /// Chaikin corner-cutting: for each segment A→B, insert two new points
    /// at 25% and 75% along the segment.  Preserves first and last points.
    /// Each pass doubles point count and progressively rounds all corners.
    /// </summary>
    List<Vector3> ChaikinSmooth(List<Vector3> path)
    {
        if (path.Count <= 2) return path;

        var smooth = new List<Vector3>();
        smooth.Add(path[0]); // keep start

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = path[i];
            Vector3 b = path[i + 1];

            // Skip cutting the very first and last segments to anchor endpoints
            if (i == 0)
            {
                smooth.Add(ValidateWaypoint(Vector3.Lerp(a, b, 0.75f), a));
            }
            else if (i == path.Count - 2)
            {
                smooth.Add(ValidateWaypoint(Vector3.Lerp(a, b, 0.25f), a));
            }
            else
            {
                smooth.Add(ValidateWaypoint(Vector3.Lerp(a, b, 0.25f), a));
                smooth.Add(ValidateWaypoint(Vector3.Lerp(a, b, 0.75f), b));
            }
        }

        smooth.Add(path[path.Count - 1]); // keep end
        return smooth;
    }

    /// <summary>
    /// Path'teki ardışık waypoint'ler arasında karadan geçen segment varsa,
    /// o segmenti A* ile yeniden hesaplar.
    /// </summary>
    List<Vector3> ValidateSegments(List<Vector3> path)
    {
        if (cachedMap == null || path.Count < 2) return path;

        var validated = new List<Vector3> { path[0] };

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = path[i];
            Vector3 b = path[i + 1];

            if (SegmentCrossesLand(a, b))
            {
                //bu segment karadan geçiyor — orta noktayı denize iterek kır
                Vector3 mid = Vector3.Lerp(a, b, 0.5f);
                //karaya en uzak yönü bul — a ve b'nin ortasından denize doğru it
                Vector3 outward = (mid - transform.position).normalized;
                Vector2 pushDir = new Vector2(outward.x, outward.y).normalized;
                mid = PushToWater(mid, pushDir);

                //hâlâ geçiyorsa segmenti atla (ship zaten CatmullRom ile yumuşatacak)
                if (!SegmentCrossesLand(a, mid) && !SegmentCrossesLand(mid, b))
                    validated.Add(mid);
            }

            validated.Add(b);
        }

        return validated;
    }

    /// <summary>
    /// İki nokta arasındaki düz çizginin karadan geçip geçmediğini kontrol eder.
    /// </summary>
    bool SegmentCrossesLand(Vector3 a, Vector3 b)
    {
        if (cachedMap == null) return false;

        float dist = Vector3.Distance(a, b);
        int steps = Mathf.Max(3, Mathf.CeilToInt(dist / 0.1f)); //her 0.1 birimde bir kontrol

        for (int s = 1; s < steps; s++)
        {
            float t = (float)s / steps;
            Vector3 point = Vector3.Lerp(a, b, t);

            int tileX = Mathf.RoundToInt((point.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
            int tileY = Mathf.RoundToInt((point.y - transform.position.y + cachedHalfH) * pixelsPerUnit);

            if (tileX >= 0 && tileX < cachedMap.width && tileY >= 0 && tileY < cachedMap.height
                && cachedMap.IsLand(tileX, tileY))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Smoothing sonrası oluşan noktanın karaya düşüp düşmediğini kontrol eder.
    /// Karaya düştüyse orijinal waypoint'e geri döner.
    /// </summary>
    Vector3 ValidateWaypoint(Vector3 candidate, Vector3 fallback)
    {
        if (cachedMap == null) return candidate;
        int tileX = Mathf.RoundToInt((candidate.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
        int tileY = Mathf.RoundToInt((candidate.y - transform.position.y + cachedHalfH) * pixelsPerUnit);
        if (tileX < 0 || tileX >= cachedMap.width || tileY < 0 || tileY >= cachedMap.height)
            return candidate;
        if (cachedMap.IsLand(tileX, tileY))
            return fallback; //karaya düştü, orijinal noktaya dön
        return candidate;
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

    // =========================================================================
    // PORT PUBLIC GETTERS
    // =========================================================================

    public int PortCount => ports.Count;

    public Vector2Int GetPortTile(int index)
    {
        if (index < 0 || index >= ports.Count) return Vector2Int.zero;
        return new Vector2Int(ports[index].tileX, ports[index].tileY);
    }

    public int ActiveShipCount => activeShips.Count;

    // =========================================================================
    // UTILITY
    // =========================================================================

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
        dayNightLookedUp = false;
        cachedMap = null;
        navGrid   = null;
        navLandDist = null;
        shipSpawnTimer = 0f;
    }
}