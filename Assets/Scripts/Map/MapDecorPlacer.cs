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
    // CITIES — ROAD-AWARE PLACEMENT
    // -------------------------------------------------------------------------

    [Header("Cities Decor — Road-Aware Placement")]

    [Tooltip("Minimum distance from water (in tiles) before a building can spawn. Set 0 to disable.")]
    [Range(0, 20)] public int cityShoreBuffer = 3;

    [Tooltip("Minimum distance in tiles from any non-Cities biome tile.")]
    [Range(0, 20)] public int cityRegionBorderBuffer = 5;

    [Tooltip("When enabled, building sprites are rotated in 90-degree increments only.")]
    public bool citySnapRotation = false;

    [Tooltip("World-space radius around each building that blocks other buildings from spawning.")]
    [Range(0.05f, 2f)] public float overlapRadius = 0.3f;

    [Tooltip("Maximum tile distance from a road for a building to spawn. 0 = disable road constraint.")]
    [Range(0, 30)] public int cityBuildingMaxRoadDistance = 8;

    [Tooltip("Buildings closer to roads are more likely to spawn. Higher = stricter clustering.")]
    [Range(0f, 1f)] public float cityRoadAffinityStrength = 0.7f;

    // -------------------------------------------------------------------------
    // PRIVATE STATE
    // -------------------------------------------------------------------------

    private List<GameObject> decorObjects    = new List<GameObject>();
    private List<Vector2>    occupiedCenters = new List<Vector2>();

    // -------------------------------------------------------------------------
    // ENTRY POINT
    // -------------------------------------------------------------------------

    public void Repaint(MapGenerator map, BiomePaintSettings settings, Texture2D mapTexture)
    {
        Clear();
        if (settings == null) { Debug.LogError("MapDecorPlacer: settings is null!"); return; }

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

        Debug.Log($"MapDecorPlacer: decor={decorObjects.Count}");
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

    public void SetDecorVisible(bool visible)
    {
        foreach (var go in decorObjects)
            if (go != null) go.SetActive(visible);
    }

    // -------------------------------------------------------------------------
    // PLACEMENT — CITIES (now road-aware)
    // -------------------------------------------------------------------------

    void TryPlaceCityBuilding(MapGenerator map, BiomePaintSettings settings,
                              int tx, int ty, float halfW, float halfH)
    {
        if (settings.citiesDecor == null || settings.citiesDecor.Count == 0) return;
        if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) return;

        // --- Road-aware placement ---
        if (cityBuildingMaxRoadDistance > 0 && RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated)
        {
            int roadDist = RoadGenerator.Instance.GetDistanceToRoad(tx, ty);

            // Hard cutoff: don't place beyond max distance
            if (roadDist > cityBuildingMaxRoadDistance) return;

            // Soft falloff: tiles further from roads have lower spawn chance
            if (cityRoadAffinityStrength > 0f && roadDist > 0)
            {
                float normalizedDist = (float)roadDist / cityBuildingMaxRoadDistance;
                float spawnChance = 1f - (normalizedDist * cityRoadAffinityStrength);
                if (Random.value > spawnChance) return;
            }
        }

        Sprite sprite = settings.citiesDecor[Random.Range(0, settings.citiesDecor.Count)];
        if (sprite == null) return;

        float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

        if (IsOverlapping(wx, wy)) return;

        occupiedCenters.Add(new Vector2(wx, wy));

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
    // CLEANUP
    // -------------------------------------------------------------------------

    public void Clear()
    {
        foreach (var go in decorObjects)
            if (go != null) Destroy(go);
        decorObjects.Clear();
        occupiedCenters.Clear();
    }
}