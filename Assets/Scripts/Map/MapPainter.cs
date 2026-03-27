using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MapDecorPlacer))]
public class MapPainter : MonoBehaviour
{
    [Header("References")]
    public MapGenerator   mapGenerator;
    public SpriteRenderer mapRenderer;
    public BiomePaintSettings settings;

    [Tooltip("Optional — if assigned, roads are generated after painting and before decor placement.")]
    public RoadGenerator roadGenerator;

    [Header("Water Depth")]
    [Range(5, 60)] public int waterDepthRange = 30;
    [Range(2, 6)]  public int waterDepthSteps = 4;

    [Header("Region Transitions")]
    [Range(1, 80)] public int transitionWidth = 30;

    [Header("Beaches")]
    [Range(0f, 1f)] public float beachChance = 0.5f;
    [Range(1, 40)]  public int   beachWidth  = 10;

    private MapDecorPlacer decorPlacer;
    private Texture2D      mapTexture;
    private float[,]       waterDistMap;
    private float[,]       borderDist;
    private int[,]         nearestOther;
    private float[,]       beachDistMap;
    private int[,]         shoreDistField;

    void Awake() { decorPlacer = GetComponent<MapDecorPlacer>(); }
    void OnEnable()  { if (mapGenerator != null) mapGenerator.OnMapGenerated += Paint; }
    void OnDisable() { if (mapGenerator != null) mapGenerator.OnMapGenerated -= Paint; }

    public Texture2D GetMapTexture() => mapTexture;

    // -------------------------------------------------------------------------
    // PAINT
    // -------------------------------------------------------------------------

    public void Paint()
    {
        if (settings == null) { Debug.LogError("MapPainter: BiomePaintSettings not assigned."); return; }

        int w = mapGenerator.width;
        int h = mapGenerator.height;

        BuildWaterDistanceField(w, h);
        BuildBorderDistanceField(w, h);
        BuildBeachMap(w, h);

        if (mapTexture != null) Destroy(mapTexture);
        mapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        mapTexture.filterMode = FilterMode.Point;

        float   seed   = Random.Range(0f, 9999f);
        Color[] pixels = new Color[w * h];

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            Color c = mapGenerator.IsLand(x, y)
                ? PaintLandWithTransition(x, y, seed)
                : PaintWater(x, y, seed, w, h);
            float fog = mapGenerator.GetFog(x, y);
            if (fog > 0f) c = Color.Lerp(c, settings.fogColor, fog);
            pixels[x + y * w] = c;
        }

        mapTexture.SetPixels(pixels);
        mapTexture.Apply();
        ApplyToRenderer(mapTexture);

        if (roadGenerator == null)
            roadGenerator = FindFirstObjectByType<RoadGenerator>();

        if (roadGenerator != null)
        {
            roadGenerator.GenerateRoads(mapGenerator, mapTexture);
            ApplyToRenderer(mapTexture);
        }
        else Debug.LogWarning("MapPainter: No RoadGenerator found. Roads will not be generated.");

        decorPlacer.Repaint(mapGenerator, settings, mapTexture);
    }

    // =========================================================================
    // CRACK PAINTING — glass crack pattern
    // =========================================================================

    public void ApplyCracks(bool[,] faultMap, Color crackColor)
    {
        if (mapTexture == null || faultMap == null) return;
        int w = mapGenerator.width, h = mapGenerator.height;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!faultMap[x, y] || !mapGenerator.IsLand(x, y)) continue;
            mapTexture.SetPixel(x, y, Color.Lerp(mapTexture.GetPixel(x, y), crackColor, 0.85f));
        }
        mapTexture.Apply();
        UndergroundMapManager.Instance?.RefreshSurfaceSprite();
    }

    /// <summary>
    /// Draws a glass-crack pattern: arms radiate from the epicenter outward,
    /// each arm can sub-branch once or twice. No shadows, no tip steering.
    /// Returns cracked tile positions for road/building detection.
    /// </summary>
    public HashSet<Vector2Int> DrawCracks(
        bool[,]    faultMap,
        Vector2Int epicenter,
        int        radius,
        Color      crackColor,
        int        numCracks      = 8,
        int        maxBranchDepth = 2,
        float      branchChance   = 0.30f)
    {
        if (mapTexture == null) return new HashSet<Vector2Int>();

        int w = mapGenerator.width;
        int h = mapGenerator.height;
        var crackedTiles = new HashSet<Vector2Int>();

        // Find the nearest fault tile to the epicenter as the true crack origin
        Vector2Int origin = FindNearestFaultTile(faultMap, epicenter, radius, w, h);

        // Evenly-spaced arms around the full circle with a small random offset each
        for (int i = 0; i < numCracks; i++)
        {
            float angle = (Mathf.PI * 2f / numCracks) * i + Random.Range(-0.25f, 0.25f);
            int   len   = Mathf.RoundToInt(radius * Random.Range(0.5f, 1.0f));
            WalkCrack(origin, angle, len, crackColor,
                      0, maxBranchDepth, branchChance,
                      epicenter, radius, w, h, crackedTiles);
        }

        mapTexture.Apply();
        UndergroundMapManager.Instance?.RefreshSurfaceSprite();
        return crackedTiles;
    }

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------

    /// <summary>Finds the fault tile closest to the epicenter within the radius.</summary>
    Vector2Int FindNearestFaultTile(bool[,] faultMap, Vector2Int epicenter, int radius, int w, int h)
    {
        float      bestDist = float.MaxValue;
        Vector2Int best     = epicenter;
        int        r2       = radius * radius;

        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > r2) continue;
            int fx = epicenter.x + dx, fy = epicenter.y + dy;
            if (fx < 0 || fx >= w || fy < 0 || fy >= h) continue;
            if (!mapGenerator.IsLand(fx, fy)) continue;
            if (!faultMap[fx, fy]) continue;
            float d = dx * dx + dy * dy;
            if (d < bestDist) { bestDist = d; best = new Vector2Int(fx, fy); }
        }
        return best;
    }

    void WalkCrack(
        Vector2Int          start,
        float               angle,
        int                 length,
        Color               crackColor,
        int                 depth,
        int                 maxDepth,
        float               branchChance,
        Vector2Int          epicenter,
        int                 radius,
        int                 w, int h,
        HashSet<Vector2Int> crackedTiles)
    {
        Vector2 pos = start;
        int     r2  = radius * radius;

        for (int step = 0; step < length; step++)
        {
            int cx = Mathf.RoundToInt(pos.x);
            int cy = Mathf.RoundToInt(pos.y);

            if (cx < 0 || cx >= w || cy < 0 || cy >= h) break;
            if (!mapGenerator.IsLand(cx, cy)) break;

            int ddx = cx - epicenter.x, ddy = cy - epicenter.y;
            if (ddx * ddx + ddy * ddy > r2) break;

            // Single dark pixel — no shadow, no edge
            mapTexture.SetPixel(cx, cy, crackColor);
            crackedTiles.Add(new Vector2Int(cx, cy));

            // Branch at a sharp fork angle
            if (depth < maxDepth && Random.value < branchChance)
            {
                float forkAngle = angle + (Random.value > 0.5f ? 1f : -1f) * Random.Range(0.4f, 0.9f);
                int   forkLen   = Mathf.RoundToInt(length * Random.Range(0.35f, 0.6f));
                WalkCrack(new Vector2Int(cx, cy), forkAngle, forkLen, crackColor,
                          depth + 1, maxDepth, branchChance * 0.5f,
                          epicenter, radius, w, h, crackedTiles);
            }

            // Mostly straight with rare sharp kink
            angle += Random.value < 0.05f
                ? Random.Range(-0.6f, 0.6f)
                : Random.Range(-0.07f, 0.07f);

            pos += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    // =========================================================================
    // SHORE DISTANCE FIELD
    // =========================================================================

    void BuildShoreDistField(int w, int h)
    {
        shoreDistField = new int[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                shoreDistField[x, y] = int.MaxValue;

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };
        var queue = new Queue<Vector2Int>();

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!mapGenerator.IsLand(x, y)) continue;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx4[i], ny = y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (!mapGenerator.IsLand(nx, ny))
                {
                    shoreDistField[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                    break;
                }
            }
        }

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d   = shoreDistField[pos.x, pos.y];
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (!mapGenerator.IsLand(nx, ny)) continue;
                if (shoreDistField[nx, ny] <= d + 1) continue;
                shoreDistField[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    // =========================================================================
    // BEACH MAP
    // =========================================================================

    void BuildBeachMap(int w, int h)
    {
        BuildShoreDistField(w, h);
        beachDistMap    = new float[w, h];
        float beachSeed = 7777f;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            beachDistMap[x, y] = -1f;
            if (!mapGenerator.IsLand(x, y)) continue;
            if (mapGenerator.IsSeaRock(x, y)) continue;
            int sd = shoreDistField[x, y];
            if (sd == int.MaxValue || sd > beachWidth) continue;
            float selector   = Mathf.PerlinNoise(x * 0.015f + beachSeed, y * 0.015f + beachSeed);
            float inlandFade = 1f - ((float)sd / beachWidth);
            if (selector * inlandFade > (1f - beachChance))
                beachDistMap[x, y] = (float)sd / beachWidth;
        }
    }

    // =========================================================================
    // BORDER DISTANCE FIELD
    // =========================================================================

    void BuildBorderDistanceField(int w, int h)
    {
        borderDist   = new float[w, h];
        nearestOther = new int[w, h];

        int[,] dist        = new int[w, h];
        int[,] sourceOther = new int[w, h];

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        { dist[x, y] = int.MaxValue; sourceOther[x, y] = 0; }

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };
        var queue = new Queue<Vector2Int>();

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!mapGenerator.IsLand(x, y)) continue;
            int myBiome = mapGenerator.GetBiome(x, y);
            if (myBiome == 5) continue;
            int foundOther = 0;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx4[i], ny = y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (!mapGenerator.IsLand(nx, ny)) continue;
                int nb = mapGenerator.GetBiome(nx, ny);
                if (nb == 5) continue;
                if (nb != myBiome) { foundOther = nb; break; }
            }
            if (foundOther != 0)
            {
                dist[x, y]        = 0;
                sourceOther[x, y] = foundOther;
                queue.Enqueue(new Vector2Int(x, y));
            }
        }

        while (queue.Count > 0)
        {
            var pos     = queue.Dequeue();
            int d       = dist[pos.x, pos.y];
            int other   = sourceOther[pos.x, pos.y];
            int myBiome = mapGenerator.GetBiome(pos.x, pos.y);
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (!mapGenerator.IsLand(nx, ny)) continue;
                if (mapGenerator.GetBiome(nx, ny) != myBiome) continue;
                if (dist[nx, ny] <= d + 1) continue;
                dist[nx, ny]        = d + 1;
                sourceOther[nx, ny] = other;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            borderDist[x, y]   = dist[x, y] == int.MaxValue
                                 ? 1f : Mathf.Clamp01((float)dist[x, y] / transitionWidth);
            nearestOther[x, y] = sourceOther[x, y];
        }
    }

    // =========================================================================
    // LAND PAINTING
    // =========================================================================

    Color PaintLandWithTransition(int x, int y, float seed)
    {
        int   myBiome    = mapGenerator.GetBiome(x, y);
        float d          = borderDist[x, y];
        int   otherBiome = nearestOther[x, y];

        if (myBiome == 5) return PaintSeaRock(x, y, seed);

        float beachD = beachDistMap[x, y];
        if (beachD >= 0f)
        {
            float blendT = Mathf.SmoothStep(0f, 1f, beachD);
            return Color.Lerp(PaintBeach(x, y, seed), GetBiomeColor(myBiome, x, y, seed), blendT);
        }

        if (d >= 1f || otherBiome == 0) return GetBiomeColor(myBiome, x, y, seed);
        if (myBiome > otherBiome)       return GetBiomeColor(myBiome, x, y, seed);

        float warp  = Perlin(x, y, seed + 3000f, 0.05f) * 0.4f - 0.2f;
        float chaos = Perlin(x, y, seed + 4000f, 0.09f) * 0.3f - 0.15f;
        float t     = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d + warp + chaos));
        return Color.Lerp(GetBiomeColor(otherBiome, x, y, seed), GetBiomeColor(myBiome, x, y, seed), t);
    }

    Color GetBiomeColor(int biome, int x, int y, float seed)
    {
        switch (biome)
        {
            case 1:  return PaintUrban(x, y, seed);
            case 2:  return PaintCities(x, y, seed);
            case 3:  return PaintIndustrial(x, y, seed);
            case 4:  return PaintAgricultural(x, y, seed);
            case 5:  return PaintSeaRock(x, y, seed);
            default: return settings.waterDeep;
        }
    }

    // =========================================================================
    // BIOME PAINT METHODS
    // =========================================================================

    Color PaintBeach(int x, int y, float seed)
    {
        float n = MultiNoise(x, y, seed + 2000f, 0.025f, 0.06f, 0.13f);
        if (n < 0.38f) return settings.beachDark;
        if (n < 0.46f) return Color.Lerp(settings.beachDark, settings.beachLight, 0.33f);
        if (n < 0.56f) return settings.beachLight;
        if (n < 0.64f) return Color.Lerp(settings.beachDark, settings.beachLight, 0.66f);
        return settings.beachDark;
    }

    Color PaintUrban(int x, int y, float seed)
    {
        float n = MultiNoise(x, y, seed, 0.018f, 0.045f, 0.11f);
        if (n < 0.35f) return settings.urbanDark;
        if (n < 0.42f) return Color.Lerp(settings.urbanDark, settings.urbanLight, 0.33f);
        if (n < 0.52f) return settings.urbanLight;
        if (n < 0.60f) return Color.Lerp(settings.urbanDark, settings.urbanLight, 0.66f);
        return settings.urbanDark;
    }

    Color PaintAgricultural(int x, int y, float seed)
    {
        float n = MultiNoise(x, y, seed + 100f, 0.022f, 0.055f, 0.13f);
        if (n < 0.32f) return settings.agriculturalDark;
        if (n < 0.40f) return Color.Lerp(settings.agriculturalDark, settings.agriculturalLight, 0.33f);
        if (n < 0.55f) return settings.agriculturalLight;
        if (n < 0.63f) return Color.Lerp(settings.agriculturalDark, settings.agriculturalLight, 0.66f);
        return settings.agriculturalDark;
    }

    Color PaintCities(int x, int y, float seed)
    {
        float n = MultiNoise(x, y, seed + 300f, 0.014f, 0.038f, 0.09f);
        if (n < 0.38f) return settings.citiesDark;
        if (n < 0.46f) return Color.Lerp(settings.citiesDark, settings.citiesLight, 0.33f);
        if (n < 0.56f) return settings.citiesLight;
        if (n < 0.64f) return Color.Lerp(settings.citiesDark, settings.citiesLight, 0.66f);
        return settings.citiesDark;
    }

    Color PaintIndustrial(int x, int y, float seed)
    {
        float n = MultiNoise(x, y, seed + 600f, 0.020f, 0.055f, 0.14f);
        if (Mathf.PerlinNoise(x * 0.06f + seed + 700f, y * 0.06f + seed + 700f) > 0.76f)
            return settings.industrialCrack;
        if (n < 0.36f) return settings.industrialDark;
        if (n < 0.44f) return Color.Lerp(settings.industrialDark, settings.industrialLight, 0.33f);
        if (n < 0.54f) return settings.industrialLight;
        if (n < 0.62f) return Color.Lerp(settings.industrialDark, settings.industrialLight, 0.66f);
        return settings.industrialDark;
    }

    Color PaintSeaRock(int x, int y, float seed)
    {
        float n = MultiNoise(x, y, seed + 800f, 0.025f, 0.065f, 0.15f);
        if (Mathf.PerlinNoise(x * 0.08f + seed + 900f, y * 0.08f + seed + 900f) > 0.74f)
            return settings.seaRockCrack;
        if (n < 0.36f) return settings.seaRockDark;
        if (n < 0.44f) return Color.Lerp(settings.seaRockDark, settings.seaRockLight, 0.33f);
        if (n < 0.54f) return settings.seaRockLight;
        if (n < 0.62f) return Color.Lerp(settings.seaRockDark, settings.seaRockLight, 0.66f);
        return settings.seaRockDark;
    }

    // =========================================================================
    // WATER
    // =========================================================================

    void BuildWaterDistanceField(int w, int h)
    {
        waterDistMap = new float[w, h];
        int[,] dist  = new int[w, h];

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            dist[x, y] = mapGenerator.IsLand(x, y) ? 0 : -1;

        var queue = new Queue<Vector2Int>();
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (mapGenerator.IsLand(x, y)) queue.Enqueue(new Vector2Int(x, y));

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d   = dist[pos.x, pos.y];
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (dist[nx, ny] != -1) continue;
                dist[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            waterDistMap[x, y] = Mathf.Clamp01((float)dist[x, y] / waterDepthRange);
    }

    Color PaintWater(int x, int y, float seed, int w, int h)
    {
        float coastDist  = waterDistMap[x, y];
        float cx         = (x - w * 0.5f) / (w * 0.5f);
        float cy         = (y - h * 0.5f) / (h * 0.5f);
        float radialDist = Mathf.Clamp01(Mathf.Sqrt(cx * cx + cy * cy));
        float depth      = Mathf.Clamp01(Mathf.Max(coastDist, radialDist)
                         + Perlin(x, y, seed + 1200f, 0.04f) * 0.18f - 0.09f);
        float stepped    = Mathf.Floor(depth * waterDepthSteps) / (waterDepthSteps - 1);
        return Color.Lerp(settings.waterShallow, settings.waterDeep, Mathf.Clamp01(stepped));
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    static float MultiNoise(int x, int y, float seed, float s1, float s2, float s3)
    {
        float n1 = Mathf.PerlinNoise(x * s1 + seed, y * s1 + seed);
        float n2 = Mathf.PerlinNoise(x * s2 + seed, y * s2 + seed) * 0.5f;
        float n3 = Mathf.PerlinNoise(x * s3 + seed, y * s3 + seed) * 0.25f;
        return (n1 + n2 + n3) / 1.75f;
    }

    static float Perlin(int x, int y, float seed, float scale)
        => Mathf.PerlinNoise(x * scale + seed, y * scale + seed);

    void ApplyToRenderer(Texture2D tex)
    {
        if (mapRenderer == null) return;
        if (mapRenderer.sprite != null) Destroy(mapRenderer.sprite);
        Sprite sprite = Sprite.Create(tex,
            new Rect(0, 0, mapGenerator.width, mapGenerator.height),
            new Vector2(0.5f, 0.5f), 100f);
        mapRenderer.sprite  = sprite;
        mapRenderer.enabled = true;
    }
}