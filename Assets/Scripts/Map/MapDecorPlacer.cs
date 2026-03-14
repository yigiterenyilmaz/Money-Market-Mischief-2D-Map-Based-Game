using System.Collections.Generic;
using UnityEngine;

public class MapDecorPlacer : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // GENERAL
    // -------------------------------------------------------------------------

    [Header("General Decor")]

    [Tooltip("Size of each placement cell in tiles at 256x256 resolution. Auto-scales with larger maps.")]
    [Range(8, 64)] public int cellSize = 14;

    [Tooltip("Pixels per world unit. Must match the value used in MapPainter (100 by default).")]
    public float pixelsPerUnit = 100f;

    [Tooltip("Z depth of all placed sprites. Negative = in front of map texture.")]
    public float spriteZ = -0.5f;

    // -------------------------------------------------------------------------
    // SPAWN RATES PER REGION
    // -------------------------------------------------------------------------

    [Header("Spawn Rates — Per Region")]

    [Tooltip("Sprite placement attempts per cell for Cities region. 0 = none, 16 = dense.")]
    [Range(0, 16)] public int citiesSpawnRate = 2;

    [Tooltip("Sprite placement attempts per cell for Agricultural region. 0 = none, 16 = dense.")]
    [Range(0, 16)] public int agriculturalSpawnRate = 2;

    [Tooltip("Sprite placement attempts per cell for Urban / Nature region. 0 = none, 16 = dense.")]
    [Range(0, 16)] public int urbanSpawnRate = 2;

    [Tooltip("Sprite placement attempts per cell for Industrial region. 0 = none, 16 = dense.")]
    [Range(0, 16)] public int industrialSpawnRate = 2;

    // -------------------------------------------------------------------------
    // SPRITE SCALE
    // -------------------------------------------------------------------------

    [Header("Sprite Scale — Applies to All Regions")]

    [Tooltip("Minimum and maximum random scale applied to ALL sprites across every region.")]
    public Vector2 spriteScaleRange = new Vector2(0.75f, 1.25f);

    // -------------------------------------------------------------------------
    // CITIES
    // -------------------------------------------------------------------------

    [Header("Cities Decor")]

    [Tooltip("Minimum distance from water (in tiles) before a building can spawn. Set 0 to disable.")]
    [Range(0, 20)] public int cityShoreBuffer = 3;

    [Tooltip("Minimum distance in tiles from any non-Cities biome tile. Keep low (4-8) due to domain warp noise.")]
    [Range(0, 20)] public int cityRegionBorderBuffer = 5;

    [Tooltip("When enabled, building sprites are rotated in 90-degree increments only.")]
    public bool citySnapRotation = false;

    [Tooltip("World-space radius around each building that blocks other buildings from spawning.")]
    [Range(0.05f, 2f)] public float overlapRadius = 0.3f;

    // -------------------------------------------------------------------------
    // ROADS
    // -------------------------------------------------------------------------

    [Header("Roads — Web Structure")]

    [Tooltip("How many nearest neighbours each building connects to. 2 creates a web with loops. 1 creates a tree.")]
    [Range(1, 5)] public int roadNeighbourConnections = 2;

    [Tooltip("How directly the road walks toward its target. 1 = straight line, 0 = fully random.")]
    [Range(0f, 1f)] public float roadConnectionBias = 0.90f;

    [Tooltip("Maximum steps a connection walk takes before giving up.")]
    [Range(50, 1000)] public int roadConnectionMaxSteps = 500;

    [Header("Roads — Branch Phase")]

    [Tooltip("Number of extra roads added after main connections.")]
    [Range(0, 20)] public int roadBranchCount = 5;

    [Tooltip("How directly each branch walks toward its target. Lower = more winding.")]
    [Range(0f, 1f)] public float roadBranchBias = 0.60f;

    [Tooltip("Maximum steps a branch walk takes.")]
    [Range(50, 1000)] public int roadBranchMaxSteps = 250;

    [Tooltip("Minimum pixel distance between branch start and end. Prevents tiny stub roads.")]
    [Range(10, 300)] public int roadBranchMinDistance = 50;

    [Header("Roads — Appearance & Borders")]

    [Tooltip("Road line thickness in pixels.")]
    [Range(1, 6)] public int roadThickness = 2;

    [Tooltip("Minimum distance in tiles from any non-Cities biome tile before a road pixel is painted.")]
    [Range(0, 20)] public int roadRegionBorderBuffer = 4;

    [Tooltip("Color painted onto the map texture for all road pixels.")]
    public Color roadColor = new Color(0.30f, 0.28f, 0.25f);

    // -------------------------------------------------------------------------
    // PRIVATE STATE
    // -------------------------------------------------------------------------

    private List<GameObject> decorObjects    = new List<GameObject>();
    private List<Vector2>    occupiedCenters = new List<Vector2>();
    private HashSet<int>     roadPixels      = new HashSet<int>();
    private List<Vector2Int> buildingTiles   = new List<Vector2Int>();
    private List<Vector2Int> roadPointList   = new List<Vector2Int>();

    private Texture2D _tex;
    private int _mapW, _mapH;

    // -------------------------------------------------------------------------
    // ENTRY POINT
    // -------------------------------------------------------------------------

    public void Repaint(MapGenerator map, BiomePaintSettings settings, Texture2D mapTexture)
    {
        Clear();
        if (settings == null) { Debug.LogError("MapDecorPlacer: settings is null!"); return; }

        _tex  = mapTexture;
        _mapW = map.width;
        _mapH = map.height;

        int scaledCellSize = Mathf.Max(cellSize, Mathf.RoundToInt(cellSize * (map.width / 256f)));
        int cellArea       = scaledCellSize * scaledCellSize;
        float halfW = map.width  * 0.5f / pixelsPerUnit;
        float halfH = map.height * 0.5f / pixelsPerUnit;

        // Build per-biome tile pools
        List<Vector2Int> cityTilePool = new List<Vector2Int>();
        Dictionary<int, List<Vector2Int>> biomeTilePools = new Dictionary<int, List<Vector2Int>>();

        for (int x = 0; x < map.width; x++)
        {
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
            List<Vector2Int> pool = kvp.Value;
            int spawnRate = GetSpawnRate(biome);
            if (spawnRate == 0) continue;

            int decorAttempts = (pool.Count / Mathf.Max(1, cellArea)) * spawnRate;
            for (int attempt = 0; attempt < decorAttempts; attempt++)
            {
                Vector2Int tile = pool[Random.Range(0, pool.Count)];
                TryPlaceNatureDecor(map, settings, biome, tile.x, tile.y, halfW, halfH);
            }
        }

        if (mapTexture != null)
        {
            PaintRoads(map);
            mapTexture.Apply();
        }

        Debug.Log($"MapDecorPlacer: cellArea={cellArea}, buildings={buildingTiles.Count}, decor={decorObjects.Count}");
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
    // VISIBILITY TOGGLE — used by UndergroundMapManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Show or hide all decor sprites (buildings, trees, etc.).
    /// Called when switching between surface and underground views.
    /// </summary>
    public void SetDecorVisible(bool visible)
    {
        foreach (var go in decorObjects)
            if (go != null) go.SetActive(visible);
    }

    // -------------------------------------------------------------------------
    // PLACEMENT — CITIES
    // -------------------------------------------------------------------------

    void TryPlaceCityBuilding(MapGenerator map, BiomePaintSettings settings,
                              int tx, int ty, float halfW, float halfH)
    {
        if (settings.citiesDecor == null || settings.citiesDecor.Count == 0) return;
        if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) return;

        Sprite sprite = settings.citiesDecor[Random.Range(0, settings.citiesDecor.Count)];
        if (sprite == null) return;

        float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

        if (IsOverlapping(wx, wy)) return;

        occupiedCenters.Add(new Vector2(wx, wy));
        buildingTiles.Add(new Vector2Int(tx, ty));

        float scale = Random.Range(spriteScaleRange.x, spriteScaleRange.y);
        PlaceSprite("CityBuilding", sprite, wx, wy, scale, false, 10 + (int)(wy * -100f));
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

        PlaceSprite("Decor", sprite, wx, wy, scale, Random.value > 0.5f, 2);
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

    void PlaceSprite(string goName, Sprite sprite, float wx, float wy,
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

        if (goName == "CityBuilding" && citySnapRotation)
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);

        decorObjects.Add(go);
    }

    // -------------------------------------------------------------------------
    // ROAD GENERATION
    // -------------------------------------------------------------------------

    void PaintRoads(MapGenerator map)
    {
        roadPixels.Clear();
        roadPointList.Clear();

        if (buildingTiles.Count < 2) return;

        HashSet<long> connectedPairs = new HashSet<long>();

        for (int i = 0; i < buildingTiles.Count; i++)
        {
            List<(float dist, int idx)> sorted = new List<(float, int)>();
            for (int j = 0; j < buildingTiles.Count; j++)
            {
                if (i == j) continue;
                sorted.Add((Vector2Int.Distance(buildingTiles[i], buildingTiles[j]), j));
            }
            sorted.Sort((a, b) => a.dist.CompareTo(b.dist));

            int connections = Mathf.Min(roadNeighbourConnections, sorted.Count);
            for (int n = 0; n < connections; n++)
            {
                int j = sorted[n].idx;
                long pairKey = i < j ? ((long)i << 32 | (uint)j) : ((long)j << 32 | (uint)i);
                if (connectedPairs.Contains(pairKey)) continue;
                connectedPairs.Add(pairKey);
                WalkToward(map, buildingTiles[i], buildingTiles[j], roadConnectionBias, roadConnectionMaxSteps);
            }
        }

        if (roadPointList.Count < 2) return;

        for (int b = 0; b < roadBranchCount; b++)
        {
            Vector2Int start  = roadPointList[Random.Range(0, roadPointList.Count)];
            Vector2Int target = start;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector2Int candidate = roadPointList[Random.Range(0, roadPointList.Count)];
                if (Vector2Int.Distance(start, candidate) >= roadBranchMinDistance)
                { target = candidate; break; }
            }

            if (target == start) continue;
            WalkToward(map, start, target, roadBranchBias, roadBranchMaxSteps);
        }
    }

    void WalkToward(MapGenerator map, Vector2Int start, Vector2Int target, float bias, int maxSteps)
    {
        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };

        Vector2Int pos = start;
        List<Vector2Int> candidate = new List<Vector2Int>();
        bool reached = false;

        for (int step = 0; step < maxSteps; step++)
        {
            if (!map.IsLand(pos.x, pos.y) || map.GetBiome(pos.x, pos.y) != 2) break;
            if (roadRegionBorderBuffer > 0 && !IsInsideRegion(map, pos.x, pos.y, roadRegionBorderBuffer)) break;

            candidate.Add(pos);

            if (Vector2Int.Distance(pos, target) <= roadThickness + 1)
            { reached = true; break; }

            int chosenDir = -1;

            if (Random.value < bias)
            {
                float bestDist = float.MaxValue;
                for (int i = 0; i < 4; i++)
                {
                    int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                    if (!InBounds(nx, ny, map) || !map.IsLand(nx, ny) || map.GetBiome(nx, ny) != 2) continue;
                    float d = Vector2Int.Distance(new Vector2Int(nx, ny), target);
                    if (d < bestDist) { bestDist = d; chosenDir = i; }
                }
            }

            if (chosenDir == -1)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int c = Random.Range(0, 4);
                    int nx = pos.x + dx4[c], ny = pos.y + dy4[c];
                    if (InBounds(nx, ny, map) && map.IsLand(nx, ny) && map.GetBiome(nx, ny) == 2)
                    { chosenDir = c; break; }
                }
            }

            if (chosenDir == -1) break;
            pos = new Vector2Int(pos.x + dx4[chosenDir], pos.y + dy4[chosenDir]);
        }

        if (!reached) return;

        foreach (var p in candidate)
        {
            PaintThickPixel(p.x, p.y);
            int key = p.x + p.y * _mapW;
            if (!roadPixels.Contains(key))
            {
                roadPixels.Add(key);
                roadPointList.Add(p);
            }
        }
    }

    void PaintThickPixel(int cx, int cy)
    {
        int half = roadThickness / 2;
        for (int dx = -half; dx <= half; dx++)
            for (int dy = -half; dy <= half; dy++)
            {
                int px = cx + dx, py = cy + dy;
                if (px >= 0 && px < _mapW && py >= 0 && py < _mapH)
                    _tex.SetPixel(px, py, roadColor);
            }
    }

    bool InBounds(int x, int y, MapGenerator map)
        => x >= 0 && x < map.width && y >= 0 && y < map.height;

    public void Clear()
    {
        foreach (var go in decorObjects)
            if (go != null) Destroy(go);
        decorObjects.Clear();
        occupiedCenters.Clear();
        roadPixels.Clear();
        buildingTiles.Clear();
        roadPointList.Clear();
        _tex = null;
    }
}