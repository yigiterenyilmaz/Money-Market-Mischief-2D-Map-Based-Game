using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MapDecorPlacer))]
public class MapPainter : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public SpriteRenderer mapRenderer;
    public BiomePaintSettings settings;

    [Header("Water Depth")]
    [Range(5, 60)] public int waterDepthRange = 30;
    [Range(2, 6)]  public int waterDepthSteps = 4;

    [Header("Region Transitions")]
    [Tooltip("Tile width of the transition band between two different regions")]
    [Range(1, 30)]  public int transitionWidth = 8;
    [Tooltip("Number of discrete color steps in the region transition")]
    [Range(2, 10)]  public int transitionSteps = 4;
    [Tooltip("0 = hard snap between dark/light within a region, 1 = full gradient")]
    [Range(0f, 1f)] public float intraRegionWidth = 0.30f;

    private MapDecorPlacer decorPlacer;
    private Texture2D mapTexture;
    private float[,] waterDistMap;
    private float[,] borderDist;
    private int[,]   nearestOther;

    void Awake() { decorPlacer = GetComponent<MapDecorPlacer>(); }
    void OnEnable()  { if (mapGenerator != null) mapGenerator.OnMapGenerated += Paint; }
    void OnDisable() { if (mapGenerator != null) mapGenerator.OnMapGenerated -= Paint; }

    public void Paint()
    {
        if (settings == null) { Debug.LogError("MapPainter: BiomePaintSettings not assigned."); return; }

        int w = mapGenerator.width;
        int h = mapGenerator.height;

        BuildWaterDistanceField(w, h);
        BuildBorderDistanceField(w, h);

        if (mapTexture != null) Destroy(mapTexture);
        mapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        mapTexture.filterMode = FilterMode.Point;

        float seed = Random.Range(0f, 9999f);
        Color[] pixels = new Color[w * h];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Color c = mapGenerator.IsLand(x, y)
                    ? PaintLandWithTransition(x, y, seed)
                    : PaintWater(x, y, seed, w, h);

                float fog = mapGenerator.GetFog(x, y);
                if (fog > 0f) c = Color.Lerp(c, settings.fogColor, fog);

                pixels[x + y * w] = c;
            }
        }

        mapTexture.SetPixels(pixels);
        mapTexture.Apply();
        ApplyToRenderer(mapTexture);
        decorPlacer.Repaint(mapGenerator, settings);
    }

    // -------------------------------------------------------------------------
    // BORDER DISTANCE FIELD
    // -------------------------------------------------------------------------

    void BuildBorderDistanceField(int w, int h)
    {
        borderDist   = new float[w, h];
        nearestOther = new int[w, h];

        int[,] dist        = new int[w, h];
        int[,] sourceOther = new int[w, h];

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                dist[x, y]        = int.MaxValue;
                sourceOther[x, y] = 0;
            }

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };

        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (!mapGenerator.IsLand(x, y)) continue;
                int myBiome = mapGenerator.GetBiome(x, y);

                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx4[i];
                    int ny = y + dy4[i];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (!mapGenerator.IsLand(nx, ny)) continue;

                    int neighborBiome = mapGenerator.GetBiome(nx, ny);
                    if (neighborBiome != myBiome)
                    {
                        if (dist[x, y] > 0)
                        {
                            dist[x, y]        = 0;
                            sourceOther[x, y] = neighborBiome;
                            queue.Enqueue(new Vector2Int(x, y));
                        }
                        break;
                    }
                }
            }
        }

        while (queue.Count > 0)
        {
            Vector2Int pos = queue.Dequeue();
            int d       = dist[pos.x, pos.y];
            int other   = sourceOther[pos.x, pos.y];
            int myBiome = mapGenerator.GetBiome(pos.x, pos.y);

            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i];
                int ny = pos.y + dy4[i];
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
                                     ? 1f
                                     : Mathf.Clamp01((float)dist[x, y] / transitionWidth);
                nearestOther[x, y] = sourceOther[x, y];
            }
    }

    // -------------------------------------------------------------------------
    // LAND PAINTING WITH TRANSITION
    // -------------------------------------------------------------------------

    Color PaintLandWithTransition(int x, int y, float seed)
    {
        int   myBiome    = mapGenerator.GetBiome(x, y);
        float d          = borderDist[x, y];
        int   otherBiome = nearestOther[x, y];

        if (d >= 1f || otherBiome == 0)
            return GetBiomeColor(myBiome, x, y, seed);

        // Only the lower-ID biome drives the transition to avoid double bands
        if (myBiome > otherBiome)
            return GetBiomeColor(myBiome, x, y, seed);

        // Warp t with noise so the border line is irregular, not a perfect ring
        float warp = Perlin(x, y, seed + 3000f, 0.05f) * 0.4f - 0.2f;
        float t    = Mathf.Clamp01(d + warp);

        Color myColor    = GetBiomeColor(myBiome,    x, y, seed);
        Color otherColor = GetBiomeColor(otherBiome, x, y, seed);

        return Color.Lerp(otherColor, myColor, t);
    }

    Color GetBiomeColor(int biome, int x, int y, float seed)
    {
        switch (biome)
        {
            case 1: return PaintUrban(x, y, seed);
            case 2: return PaintCities(x, y, seed);
            case 3: return PaintIndustrial(x, y, seed);
            case 4: return PaintAgricultural(x, y, seed);
            default: return settings.waterDeep;
        }
    }

    // -------------------------------------------------------------------------
    // BIOME PAINT METHODS
    // -------------------------------------------------------------------------

    Color PaintUrban(int x, int y, float seed)
    {
        // Layer 1: large landmass-scale variation
        float n1 = Mathf.PerlinNoise(x * 0.018f + seed,        y * 0.018f + seed);
        // Layer 2: medium formations — hills, clearings
        float n2 = Mathf.PerlinNoise(x * 0.045f + seed + 40f,  y * 0.045f + seed + 40f) * 0.5f;
        // Layer 3: small surface detail — rocks, undergrowth
        float n3 = Mathf.PerlinNoise(x * 0.11f  + seed + 80f,  y * 0.11f  + seed + 80f) * 0.25f;

        float n = (n1 + n2 + n3) / 1.75f;

        // Hard threshold — no smooth blend, snaps between dark and light
        // Multiple thresholds create distinct natural bands
        if (n < 0.35f) return settings.urbanDark;
        if (n < 0.42f) return Color.Lerp(settings.urbanDark,  settings.urbanLight, 0.33f);
        if (n < 0.52f) return settings.urbanLight;
        if (n < 0.60f) return Color.Lerp(settings.urbanDark,  settings.urbanLight, 0.66f);
        return settings.urbanDark;
    }

    Color PaintAgricultural(int x, int y, float seed)
    {
        float wx   = x + Perlin(x, y, seed + 110f, 0.015f) * 25f;
        float wy   = y + Perlin(x, y, seed + 120f, 0.015f) * 25f;
        float n    = Mathf.PerlinNoise(wx * 0.03f + seed + 100f, wy * 0.03f + seed + 100f);
        float half = intraRegionWidth * 0.5f;
        float t    = Mathf.InverseLerp(0.5f - half, 0.5f + half, n);
        t = Mathf.Floor(t * transitionSteps) / (transitionSteps - 1);
        t = Mathf.Clamp01(t);
        return Color.Lerp(settings.agriculturalDark, settings.agriculturalLight, t);
    }

    Color PaintCities(int x, int y, float seed)
    {
        float wx   = x + Perlin(x, y, seed + 310f, 0.015f) * 25f;
        float wy   = y + Perlin(x, y, seed + 320f, 0.015f) * 25f;
        float n    = Mathf.PerlinNoise(wx * 0.03f + seed + 300f, wy * 0.03f + seed + 300f);
        float half = intraRegionWidth * 0.5f;
        float t    = Mathf.InverseLerp(0.5f - half, 0.5f + half, n);
        t = Mathf.Floor(t * transitionSteps) / (transitionSteps - 1);
        t = Mathf.Clamp01(t);
        return Color.Lerp(settings.citiesDark, settings.citiesLight, t);
    }

    Color PaintIndustrial(int x, int y, float seed)
    {
        float wx   = x + Perlin(x, y, seed + 610f, 0.015f) * 25f;
        float wy   = y + Perlin(x, y, seed + 620f, 0.015f) * 25f;
        float n    = Mathf.PerlinNoise(wx * 0.03f + seed + 600f, wy * 0.03f + seed + 600f);

        float crack = Mathf.PerlinNoise(x * 0.06f + seed + 700f, y * 0.06f + seed + 700f);
        if (crack > 0.76f) return settings.industrialCrack;

        float half = intraRegionWidth * 0.5f;
        float t    = Mathf.InverseLerp(0.5f - half, 0.5f + half, n);
        t = Mathf.Floor(t * transitionSteps) / (transitionSteps - 1);
        t = Mathf.Clamp01(t);
        return Color.Lerp(settings.industrialDark, settings.industrialLight, t);
    }

    // -------------------------------------------------------------------------
    // WATER
    // -------------------------------------------------------------------------

    void BuildWaterDistanceField(int w, int h)
    {
        waterDistMap = new float[w, h];
        int[,] dist  = new int[w, h];

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                dist[x, y] = mapGenerator.IsLand(x, y) ? 0 : -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (mapGenerator.IsLand(x, y))
                    queue.Enqueue(new Vector2Int(x, y));

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int pos = queue.Dequeue();
            int d = dist[pos.x, pos.y];
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i];
                int ny = pos.y + dy4[i];
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
        float depth      = Mathf.Max(coastDist, radialDist);
        float jitter     = Perlin(x, y, seed + 1200f, 0.04f) * 0.18f - 0.09f;
        depth = Mathf.Clamp01(depth + jitter);
        float stepped = Mathf.Floor(depth * waterDepthSteps) / (waterDepthSteps - 1);
        return Color.Lerp(settings.waterShallow, settings.waterDeep, Mathf.Clamp01(stepped));
    }

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------

    static float Perlin(int x, int y, float seed, float scale)
        => Mathf.PerlinNoise(x * scale + seed, y * scale + seed);

    void ApplyToRenderer(Texture2D tex)
    {
        if (mapRenderer == null) return;
        if (mapRenderer.sprite != null) Destroy(mapRenderer.sprite);
        Sprite sprite = Sprite.Create(tex,
            new Rect(0, 0, mapGenerator.width, mapGenerator.height),
            new Vector2(0.5f, 0.5f), 100f);
        mapRenderer.sprite = sprite;
        mapRenderer.enabled = true;
    }
}