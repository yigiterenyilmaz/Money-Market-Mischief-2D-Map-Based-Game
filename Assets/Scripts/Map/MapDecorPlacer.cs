using System.Collections.Generic;
using UnityEngine;

public class MapDecorPlacer : MonoBehaviour
{
    [Header("General Decor")]
    [Range(8, 64)]  public int   cellSize      = 14;
    public float pixelsPerUnit                  = 100f;
    public float spriteZ                        = -0.5f;

    [Header("Spawn Rates — Per Region")]
    [Range(0, 16)] public int citiesSpawnRate       = 2;
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
    [Range(0, 30)]     public int   cityBuildingMaxRoadDistance = 8;
    [Range(0f, 1f)]    public float cityRoadAffinityStrength    = 0.7f;

    [Header("Broken Building Sprites")]
    [Tooltip("Sprites randomly picked when a city building is cracked by an earthquake.")]
    public List<Sprite> brokenBuildingSprites = new List<Sprite>();
    [Tooltip("Tint applied to broken buildings.")]
    public Color brokenBuildingTint = new Color(0.55f, 0.45f, 0.40f, 1f);

    [Header("Day / Night Building Sprites")]
    [Tooltip("Night variants of citiesDecor (lights on). Index-matched to citiesDecor.")]
    public List<Sprite> citiesDecorNight = new List<Sprite>();
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
        public float          speed;
        public float          currentAngle; // current facing angle (degrees), smoothly lerped
    }

    private List<GameObject>   decorObjects    = new List<GameObject>();
    private List<Vector2>      occupiedCenters = new List<Vector2>();
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

    // -------------------------------------------------------------------------
    // ENTRY POINT
    // -------------------------------------------------------------------------

    public void Repaint(MapGenerator map, BiomePaintSettings settings, Texture2D mapTexture)
    {
        Clear();
        if (settings == null) { Debug.LogError("MapDecorPlacer: settings is null!"); return; }

        dayNight  = DayNightCycle.Instance;
        cachedMap = map;

        int scaledCellSize = Mathf.Max(cellSize, Mathf.RoundToInt(cellSize * (map.width / 256f)));
        int cellArea       = scaledCellSize * scaledCellSize;
        float halfW = map.width  * 0.5f / pixelsPerUnit;
        float halfH = map.height * 0.5f / pixelsPerUnit;
        cachedHalfW = halfW;
        cachedHalfH = halfH;

        var cityTilePool   = new List<Vector2Int>();
        var biomeTilePools = new Dictionary<int, List<Vector2Int>>();

        for (int x = 0; x < map.width; x++)
        for (int y = 0; y < map.height; y++)
        {
            if (!map.IsLand(x, y)) continue;
            if (map.GetFog(x, y) > 0.6f) continue;
            int b = map.GetBiome(x, y);
            if (b == 2)
            {
                if (cityRegionBorderBuffer == 0 || IsInsideRegion(map, x, y, cityRegionBorderBuffer))
                    cityTilePool.Add(new Vector2Int(x, y));
            }
            else
            {
                if (!biomeTilePools.ContainsKey(b))
                    biomeTilePools[b] = new List<Vector2Int>();
                biomeTilePools[b].Add(new Vector2Int(x, y));
            }
        }

        int cityAttempts = (cityTilePool.Count / Mathf.Max(1, cellArea)) * citiesSpawnRate;
        for (int attempt = 0; attempt < cityAttempts; attempt++)
        {
            if (cityTilePool.Count == 0) break;
            Vector2Int tile = cityTilePool[Random.Range(0, cityTilePool.Count)];
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
        if (dayNight == null) dayNight = DayNightCycle.Instance;

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

    void ApplyCrossfade(float ratio)
    {
        // Buildings
        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (bd.dayRenderer == null) continue;

            float baseA = bd.baseAlpha;

            Color dc      = bd.dayRenderer.color;
            dc.a          = baseA * (1f - ratio);
            bd.dayRenderer.color = dc;

            if (bd.nightRenderer != null)
            {
                Color nc = bd.nightRenderer.color;
                nc.a     = baseA * ratio;
                bd.nightRenderer.color = nc;
            }
        }

        // Ports
        for (int i = 0; i < ports.Count; i++)
        {
            PortData pd = ports[i];
            if (pd.dayRenderer == null) continue;

            float baseA = pd.baseAlpha;
            Color dc = pd.dayRenderer.color;
            dc.a = baseA * (1f - ratio);
            pd.dayRenderer.color = dc;

            if (pd.nightRenderer != null)
            {
                Color nc = pd.nightRenderer.color;
                nc.a = baseA * ratio;
                pd.nightRenderer.color = nc;
            }
        }

        // Ships
        for (int i = 0; i < activeShips.Count; i++)
        {
            ShipInstance ship = activeShips[i];
            if (ship.dayRenderer == null) continue;

            float baseA = ship.baseAlpha;
            Color dc = ship.dayRenderer.color;
            dc.a = baseA * (1f - ratio);
            ship.dayRenderer.color = dc;

            if (ship.nightRenderer != null)
            {
                Color nc = ship.nightRenderer.color;
                nc.a = baseA * ratio;
                ship.nightRenderer.color = nc;
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

        if (cityBuildingMaxRoadDistance > 0 && RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated)
        {
            int roadDist = RoadGenerator.Instance.GetDistanceToRoad(tx, ty);
            if (roadDist > cityBuildingMaxRoadDistance) return;
            if (cityRoadAffinityStrength > 0f && roadDist > 0)
            {
                float normalizedDist = (float)roadDist / cityBuildingMaxRoadDistance;
                float spawnChance    = 1f - (normalizedDist * cityRoadAffinityStrength);
                if (Random.value > spawnChance) return;
            }
        }

        int spriteIdx = Random.Range(0, settings.citiesDecor.Count);
        Sprite daySprite = settings.citiesDecor[spriteIdx];
        if (daySprite == null) return;

        float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;
        if (IsOverlapping(wx, wy)) return;
        occupiedCenters.Add(new Vector2(wx, wy));

        float scale    = Random.Range(spriteScaleRange.x, spriteScaleRange.y);
        float baseA    = Random.Range(0.85f, 1f);
        int sortOrder  = 10 + (int)(wy * -100f);

        GameObject go = new GameObject("CityBuilding");
        go.transform.SetParent(transform);
        go.transform.position   = new Vector3(wx, wy, spriteZ);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        if (citySnapRotation)
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);

        SpriteRenderer daySR = go.AddComponent<SpriteRenderer>();
        daySR.sprite       = daySprite;
        daySR.sortingOrder = sortOrder;
        daySR.flipX        = false;
        daySR.color        = new Color(1f, 1f, 1f, baseA);

        SpriteRenderer nightSR = null;
        Sprite nightSprite = GetNightCitySprite(spriteIdx);
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

        decorObjects.Add(go);
        cityBuildings.Add(new BuildingData
        {
            go            = go,
            dayRenderer   = daySR,
            nightRenderer = nightSR,
            tileX         = tx,
            tileY         = ty,
            isBroken      = false,
            spriteIndex   = spriteIdx,
            brokenIndex   = -1,
            baseAlpha     = baseA
        });
    }

    Sprite GetNightCitySprite(int index)
    {
        if (citiesDecorNight == null || index < 0 || index >= citiesDecorNight.Count)
            return null;
        return citiesDecorNight[index];
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

    bool IsOverlapping(float wx, float wy)
    {
        float minDist = overlapRadius * 2f;
        foreach (var c in occupiedCenters)
            if (Vector2.Distance(new Vector2(wx, wy), c) < minDist) return true;
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
        ports.Add(new PortData
        {
            go           = go,
            dayRenderer  = daySR,
            nightRenderer = nightSR,
            tileX        = tile.x,
            tileY        = tile.y,
            baseAlpha    = baseA,
            worldPos     = new Vector3(wx, wy, spriteZ)
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

        // Spawn timer
        shipSpawnTimer -= Time.deltaTime;
        if (shipSpawnTimer <= 0f)
        {
            shipSpawnTimer = shipSpawnInterval;
            if (activeShips.Count < maxActiveShips)
                TrySpawnShip();
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
                    if (ship.waitTimer <= 0f)
                    {
                        // Build departure path (reverse: port → ocean edge)
                        Vector3 exitPoint = GetRandomOceanEdgePoint();
                        List<Vector3> depPath = FindShipPath(ship.go.transform.position, exitPoint);
                        if (depPath != null && depPath.Count > 0)
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
    }

    void MoveAlongPath(ShipInstance ship)
    {
        if (ship.path == null || ship.path.Count < 2) { ship.pathIndex = ship.path != null ? ship.path.Count : 0; return; }

        float totalSegments = ship.path.Count - 1;
        float distThisFrame = ship.speed * Time.deltaTime;

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

        // Catmull-Rom interpolation using 4 control points
        int i1 = ship.pathIndex;
        int i0 = Mathf.Max(0, i1 - 1);
        int i2 = Mathf.Min(ship.path.Count - 1, i1 + 1);
        int i3 = Mathf.Min(ship.path.Count - 1, i1 + 2);

        Vector3 pos = CatmullRom(ship.path[i0], ship.path[i1], ship.path[i2], ship.path[i3], ship.segmentT);
        ship.go.transform.position = pos;

        // Compute tangent for facing direction
        Vector3 tangent = CatmullRomTangent(ship.path[i0], ship.path[i1], ship.path[i2], ship.path[i3], ship.segmentT);
        if (tangent.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f;
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

    void TrySpawnShip()
    {
        if (ports.Count == 0) return;

        int portIdx   = Random.Range(0, ports.Count);
        PortData port = ports[portIdx];

        Vector3 origin    = GetRandomOceanEdgePoint();
        Vector3 portWorld = port.worldPos;

        // Offset the destination slightly into water near the port
        Vector3 portApproach = GetWaterApproachPoint(port.tileX, port.tileY);

        List<Vector3> path = FindShipPath(origin, portApproach);
        if (path == null || path.Count == 0) return;

        // Create ship GO
        int spriteIdx = Random.Range(0, shipSpritesDay.Count);
        Sprite daySprite = shipSpritesDay[spriteIdx];
        if (daySprite == null) return;

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
    /// Returns a world position in the water just off the port's shore tile.
    /// Ships approach this point rather than the exact port tile.
    /// </summary>
    Vector3 GetWaterApproachPoint(int portTileX, int portTileY)
    {
        // Find the water-side neighbor of the port tile
        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };

        for (int i = 0; i < 4; i++)
        {
            int nx = portTileX + dx4[i], ny = portTileY + dy4[i];
            if (cachedMap != null && !cachedMap.IsLand(nx, ny))
            {
                // Go a few tiles further into water
                int waterX = portTileX + dx4[i] * 3;
                int waterY = portTileY + dy4[i] * 3;
                float wx = transform.position.x + (waterX / pixelsPerUnit) - cachedHalfW;
                float wy = transform.position.y + (waterY / pixelsPerUnit) - cachedHalfH;
                return new Vector3(wx, wy, spriteZ);
            }
        }

        // Fallback: just offset slightly from the port itself
        float fwx = transform.position.x + (portTileX / pixelsPerUnit) - cachedHalfW;
        float fwy = transform.position.y + (portTileY / pixelsPerUnit) - cachedHalfH;
        return new Vector3(fwx, fwy - 0.1f, spriteZ);
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

        // 2. Chaikin corner-cutting subdivision: rounds off sharp corners
        //    Each pass replaces every segment AB with two points at 25% and 75%,
        //    progressively smoothing the curve while preserving start/end.
        for (int pass = 0; pass < shipPathSmoothingPasses; pass++)
            worldPath = ChaikinSmooth(worldPath);

        return worldPath;
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
            Vector3 d1 = (curr - prev).normalized;
            Vector3 d2 = (next - curr).normalized;
            if (Vector3.Dot(d1, d2) < 0.98f)
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
                smooth.Add(Vector3.Lerp(a, b, 0.75f));
            }
            else if (i == path.Count - 2)
            {
                smooth.Add(Vector3.Lerp(a, b, 0.25f));
            }
            else
            {
                smooth.Add(Vector3.Lerp(a, b, 0.25f));
                smooth.Add(Vector3.Lerp(a, b, 0.75f));
            }
        }

        smooth.Add(path[path.Count - 1]); // keep end
        return smooth;
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
        // Destroy all active ships
        foreach (var ship in activeShips)
            if (ship.go != null) Destroy(ship.go);
        activeShips.Clear();

        foreach (var go in decorObjects)
            if (go != null) Destroy(go);
        decorObjects.Clear();
        occupiedCenters.Clear();
        cityBuildings.Clear();
        ports.Clear();
        prevRatio = -1f;
        cachedMap = null;
        navGrid   = null;
        navLandDist = null;
        shipSpawnTimer = 0f;
    }
}