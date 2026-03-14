using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Size")]
    public int width = 256;
    public int height = 256;

    [Header("Main Island")]
    [Range(0.25f, 0.45f)]
    public float islandSize = 0.35f;
    [Range(4, 10)]
    public int basePoints = 7;
    [Range(3, 7)]
    public int subdivisions = 5;
    [Range(0.3f, 0.7f)]
    public float roughness = 0.5f;
    [Range(0f, 0.5f)]
    public float stretchVariation = 0.3f;

    [Header("Organic Details")]
    public bool addPeninsulas = true;
    [Range(0, 5)]
    public int peninsulaCount = 3;
    [Range(0.1f, 0.3f)]
    public float peninsulaSize = 0.15f;

    public bool addBays = true;
    [Range(0, 4)]
    public int bayCount = 2;
    [Range(0.08f, 0.2f)]
    public float baySize = 0.12f;

    [Header("Sea Rocks")]
    [Tooltip("Generate small rock formations in the sea on the sides of the island.")]
    public bool addSeaRocks = true;
    [Range(0, 12)]
    public int seaRockCount = 6;
    [Tooltip("Size of rocks relative to the island radius.")]
    [Range(0.02f, 0.12f)]
    public float seaRockSize = 0.06f;
    [Tooltip("How far from the island center rocks can spawn (as fraction of half-map).")]
    [Range(0.4f, 0.9f)]
    public float seaRockSpread = 0.7f;

    [Header("Fog")]
    public bool useFog = true;
    [Range(0.5f, 0.95f)]
    public float fogStart = 0.8f;
    [Range(0f, 1f)]
    public float cornerRadius = 0.5f;
    [Tooltip("Higher = sharper fog edge. 1 = original soft gradient, 3+ = crisp boundary, 6+ = hard cutoff.")]
    [Range(1f, 10f)]
    public float fogSharpness = 1f;

    [Header("Safety Margin")]
    [Tooltip("Fraction of half-map that the island must stay within. Prevents vertical/horizontal overflow.")]
    [Range(0.6f, 0.95f)]
    public float islandMaxExtent = 0.85f;

    [Header("Colors")]
    public Color waterColor = new Color(0.1f, 0.3f, 0.8f);
    public Color fogColor = new Color(0.7f, 0.75f, 0.8f);

    [Header("Biomes")]
    public Color biome1Color = new Color(0.2f, 0.6f, 0.2f);
    public Color biome2Color = new Color(0.85f, 0.75f, 0.4f);
    public Color biome3Color = new Color(0.4f, 0.4f, 0.45f);
    public Color biome4Color = new Color(0.3f, 0.7f, 0.5f);
    [Tooltip("Fallback color for sea rocks (biome 5). MapPainter overrides with its own paint method.")]
    public Color biome5Color = new Color(0.5f, 0.5f, 0.52f);

    [Header("Biome Spawn Settings")]
    [Range(0f, 1f)] public float biome2MaxRatio = 0.2f;
    [Range(0f, 1f)] public float biome3MaxRatio = 0.2f;
    [Range(0f, 1f)] public float biome4MaxRatio = 0.3f;

    [Header("Spawn Threshold")]
    [Range(0f, 0.2f)] public float minSpawnThreshold = 0.03f;

    public float ForestRatio   { get; private set; }
    public float DesertRatio   { get; private set; }
    public float MountainRatio { get; private set; }
    public float PlainsRatio   { get; private set; }

    [Header("Cleanup")]
    public bool fillSmallLakes = true;
    public int minLakeSize = 80;

    [Header("References")]
    public SpriteRenderer mapRenderer;

    public System.Action OnMapGenerated;

    private bool[,] landMap;
    private int[,] biomeMap;
    private float[,] fogMap;
    private Texture2D mapTexture;
    private int totalLandTiles;
    private int[] biomeTileCounts = new int[5]; // 1-4 normal + 5 sea rock

    private bool[,] seaRockMap;

    void Start()
    {
        if (mapRenderer != null)
            mapRenderer.enabled = false;

        GenerateMap();
    }

    [ContextMenu("Regenerate Map")]
    public void Regenerate()
    {
        GenerateMap();
    }

    public void GenerateMap()
    {
        landMap    = new bool[width, height];
        biomeMap   = new int[width, height];
        fogMap     = new float[width, height];
        seaRockMap = new bool[width, height];

        Vector2 center     = new Vector2(width / 2f, height / 2f);
        float baseRadius   = Mathf.Min(width, height) * islandSize;
        float stretchX     = 1f + Random.Range(-stretchVariation, stretchVariation);
        float stretchY     = 1f + Random.Range(-stretchVariation, stretchVariation);
        float rotation     = Random.Range(0f, Mathf.PI * 2f);

        List<Vector2> mainPoly = GenerateDetailedPolygon(center, baseRadius, stretchX, stretchY, rotation);
        ClampPolygonToSafeZone(mainPoly, center);
        FillPolygon(mainPoly);

        if (addPeninsulas)
            for (int i = 0; i < peninsulaCount; i++)
                AddPeninsula(center, baseRadius);

        if (addBays)
            for (int i = 0; i < bayCount; i++)
                AddBay(center, baseRadius);

        if (fillSmallLakes)
            FillSmallLakes();

        ClampLandToSafeZone();

        if (addSeaRocks)
            GenerateSeaRocks(center, baseRadius);

        GenerateBiomes();

        if (useFog)
            GenerateFog();

        CalculateBiomeRatios();

        mapTexture = CreateTexture();
        ApplyTexture();
    }

    // -------------------------------------------------------------------------
    // POLYGON CLAMPING
    // -------------------------------------------------------------------------

    void ClampPolygonToSafeZone(List<Vector2> polygon, Vector2 center)
    {
        float safeHalfW = width  * 0.5f * islandMaxExtent;
        float safeHalfH = height * 0.5f * islandMaxExtent;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 p = polygon[i];
            float dx = p.x - center.x;
            float dy = p.y - center.y;

            float scaleX = (Mathf.Abs(dx) > safeHalfW) ? safeHalfW / Mathf.Abs(dx) : 1f;
            float scaleY = (Mathf.Abs(dy) > safeHalfH) ? safeHalfH / Mathf.Abs(dy) : 1f;
            float scale = Mathf.Min(scaleX, scaleY);

            if (scale < 1f)
                polygon[i] = new Vector2(center.x + dx * scale, center.y + dy * scale);
        }
    }

    void ClampLandToSafeZone()
    {
        float safeMinX = width  * (1f - islandMaxExtent) * 0.5f;
        float safeMaxX = width  - safeMinX;
        float safeMinY = height * (1f - islandMaxExtent) * 0.5f;
        float safeMaxY = height - safeMinY;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (landMap[x, y] && (x < safeMinX || x > safeMaxX || y < safeMinY || y > safeMaxY))
                    landMap[x, y] = false;
    }

    // -------------------------------------------------------------------------
    // SEA ROCKS
    // -------------------------------------------------------------------------

    void GenerateSeaRocks(Vector2 islandCenter, float islandRadius)
    {
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        for (int r = 0; r < seaRockCount; r++)
        {
            float side = (r % 2 == 0) ? -1f : 1f;

            float minDistFromCenter = islandRadius * 0.9f;
            float maxDistFromCenter = halfW * seaRockSpread;
            float hDist = Random.Range(minDistFromCenter, maxDistFromCenter);

            float cx = islandCenter.x + side * hDist;
            float vScatter = halfH * 0.5f;
            float cy = islandCenter.y + Random.Range(-vScatter, vScatter);

            if (cx < 15 || cx > width - 15 || cy < 15 || cy > height - 15) continue;

            int icx = Mathf.RoundToInt(cx), icy = Mathf.RoundToInt(cy);
            if (icx >= 0 && icx < width && icy >= 0 && icy < height && landMap[icx, icy]) continue;

            float rockRadius = islandRadius * seaRockSize * Random.Range(0.5f, 1.5f);

            List<Vector2> rockPoly = GenerateRockPolygon(new Vector2(cx, cy), rockRadius);
            FillPolygon(rockPoly);
            MarkSeaRockTiles(new Vector2(cx, cy), rockRadius);
        }
    }

    List<Vector2> GenerateRockPolygon(Vector2 center, float radius)
    {
        int pts = Random.Range(4, 7);
        List<Vector2> polygon = new List<Vector2>();
        float startAngle = Random.Range(0f, Mathf.PI * 2f);
        float angleStep = (Mathf.PI * 2f) / pts;

        for (int i = 0; i < pts; i++)
        {
            float angle = startAngle + i * angleStep;
            float rr = radius * Random.Range(0.5f, 1.2f);
            polygon.Add(new Vector2(center.x + Mathf.Cos(angle) * rr, center.y + Mathf.Sin(angle) * rr));
        }

        polygon = SubdividePolygon(polygon, 0.4f);
        return polygon;
    }

    void MarkSeaRockTiles(Vector2 center, float radius)
    {
        int r = Mathf.CeilToInt(radius * 1.5f);
        int cx = Mathf.RoundToInt(center.x), cy = Mathf.RoundToInt(center.y);

        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            int px = cx + dx, py = cy + dy;
            if (px < 0 || px >= width || py < 0 || py >= height) continue;
            if (landMap[px, py])
                seaRockMap[px, py] = true;
        }
    }

    // -------------------------------------------------------------------------
    // POLYGON GENERATION
    // -------------------------------------------------------------------------

    List<Vector2> GenerateDetailedPolygon(Vector2 center, float radius, float stretchX, float stretchY, float rotation)
    {
        List<Vector2> polygon = new List<Vector2>();
        float angleStep  = (Mathf.PI * 2f) / basePoints;
        float startAngle = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < basePoints; i++)
        {
            float angle = startAngle + i * angleStep;
            float rr = radius * Random.Range(0.7f, 1.3f);
            float x = Mathf.Cos(angle) * rr * stretchX;
            float y = Mathf.Sin(angle) * rr * stretchY;
            float rx = x * Mathf.Cos(rotation) - y * Mathf.Sin(rotation);
            float ry = x * Mathf.Sin(rotation) + y * Mathf.Cos(rotation);
            polygon.Add(new Vector2(center.x + rx, center.y + ry));
        }

        for (int s = 0; s < subdivisions; s++)
            polygon = SubdividePolygon(polygon, roughness * Mathf.Pow(0.55f, s));

        return polygon;
    }

    List<Vector2> SubdividePolygon(List<Vector2> polygon, float displacement)
    {
        List<Vector2> newPoly = new List<Vector2>();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next    = polygon[(i + 1) % polygon.Count];
            newPoly.Add(current);
            Vector2 mid  = (current + next) / 2f;
            Vector2 dir  = next - current;
            Vector2 perp = new Vector2(-dir.y, dir.x).normalized;
            float disp   = dir.magnitude * displacement * Random.Range(-0.8f, 1f);
            mid += perp * disp;
            newPoly.Add(mid);
        }
        return newPoly;
    }

    void AddPeninsula(Vector2 islandCenter, float islandRadius)
    {
        float angle         = Random.Range(0f, Mathf.PI * 2f);
        float distFromCenter = islandRadius * Random.Range(0.6f, 0.9f);
        Vector2 basePos     = islandCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distFromCenter;
        float penRadius     = islandRadius * peninsulaSize * Random.Range(0.7f, 1.3f);
        Vector2 penCenter   = basePos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * penRadius * 0.5f;
        List<Vector2> penPoly = GenerateDetailedPolygon(penCenter, penRadius,
            1f + Random.Range(0.3f, 0.8f),
            Random.Range(0.4f, 0.7f),
            angle);
        FillPolygon(penPoly);
    }

    void AddBay(Vector2 islandCenter, float islandRadius)
    {
        float angle          = Random.Range(0f, Mathf.PI * 2f);
        float distFromCenter = islandRadius * Random.Range(0.5f, 0.8f);
        Vector2 bayCenter    = islandCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distFromCenter;
        float bayRadius      = islandRadius * baySize * Random.Range(0.8f, 1.2f);
        List<Vector2> bayPoly = GenerateDetailedPolygon(bayCenter, bayRadius,
            Random.Range(0.6f, 1f),
            Random.Range(0.6f, 1f),
            Random.Range(0f, Mathf.PI * 2f));
        CarveBay(bayPoly);
    }

    void FillPolygon(List<Vector2> polygon)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in polygon) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }

        int startX = Mathf.Max(0, Mathf.FloorToInt(minX));
        int endX   = Mathf.Min(width  - 1, Mathf.CeilToInt(maxX));
        int startY = Mathf.Max(0, Mathf.FloorToInt(minY));
        int endY   = Mathf.Min(height - 1, Mathf.CeilToInt(maxY));

        for (int y = startY; y <= endY; y++)
        {
            List<float> intersections = new List<float>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 p1 = polygon[i];
                Vector2 p2 = polygon[(i + 1) % polygon.Count];
                if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                    intersections.Add(p1.x + (y - p1.y) / (p2.y - p1.y) * (p2.x - p1.x));
            }
            intersections.Sort();
            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                int x1 = Mathf.Max(0, Mathf.CeilToInt(intersections[i]));
                int x2 = Mathf.Min(width - 1, Mathf.FloorToInt(intersections[i + 1]));
                for (int x = x1; x <= x2; x++) landMap[x, y] = true;
            }
        }
    }

    void CarveBay(List<Vector2> polygon)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in polygon) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }

        int startX = Mathf.Max(0, Mathf.FloorToInt(minX));
        int endX   = Mathf.Min(width  - 1, Mathf.CeilToInt(maxX));
        int startY = Mathf.Max(0, Mathf.FloorToInt(minY));
        int endY   = Mathf.Min(height - 1, Mathf.CeilToInt(maxY));

        for (int y = startY; y <= endY; y++)
        {
            List<float> intersections = new List<float>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 p1 = polygon[i];
                Vector2 p2 = polygon[(i + 1) % polygon.Count];
                if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                    intersections.Add(p1.x + (y - p1.y) / (p2.y - p1.y) * (p2.x - p1.x));
            }
            intersections.Sort();
            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                int x1 = Mathf.Max(0, Mathf.CeilToInt(intersections[i]));
                int x2 = Mathf.Min(width - 1, Mathf.FloorToInt(intersections[i + 1]));
                for (int x = x1; x <= x2; x++) landMap[x, y] = false;
            }
        }
    }

    void FillSmallLakes()
    {
        bool[,] visited = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (!landMap[x, y] && !visited[x, y])
                {
                    List<Vector2Int> lake = new List<Vector2Int>();
                    bool touchesEdge = FloodFillWater(x, y, visited, lake);
                    if (!touchesEdge && lake.Count < minLakeSize)
                        foreach (var pos in lake) landMap[pos.x, pos.y] = true;
                }
    }

    bool FloodFillWater(int startX, int startY, bool[,] visited, List<Vector2Int> region)
    {
        bool touchesEdge = false;
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startX, startY));
        while (stack.Count > 0)
        {
            Vector2Int pos = stack.Pop();
            int x = pos.x, y = pos.y;
            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (visited[x, y] || landMap[x, y]) continue;
            visited[x, y] = true;
            region.Add(pos);
            if (x == 0 || x == width - 1 || y == 0 || y == height - 1) touchesEdge = true;
            stack.Push(new Vector2Int(x + 1, y));
            stack.Push(new Vector2Int(x - 1, y));
            stack.Push(new Vector2Int(x, y + 1));
            stack.Push(new Vector2Int(x, y - 1));
        }
        return touchesEdge;
    }

    // -------------------------------------------------------------------------
    // FOG
    // -------------------------------------------------------------------------

    void GenerateFog()
    {
        float noiseOffset = Random.Range(0f, 1000f);
        float halfW = width  / 2f;
        float halfH = height / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float nx    = (x - halfW) / halfW;
                float ny    = (y - halfH) / halfH;
                float power = 2f + (1f - cornerRadius) * 6f;
                float dist  = Mathf.Pow(Mathf.Abs(nx), power) + Mathf.Pow(Mathf.Abs(ny), power);
                dist = Mathf.Pow(dist, 1f / power);

                float noise        = Mathf.PerlinNoise((x + noiseOffset) / 50f, (y + noiseOffset) / 50f) * 0.06f;
                float fogThreshold = fogStart + noise;

                if (dist > fogThreshold)
                {
                    float t = Mathf.Clamp01((dist - fogThreshold) / (1f - fogThreshold));
                    float smoothed = t * t * (3f - 2f * t);
                    float sharpened = Mathf.Pow(smoothed, 1f / fogSharpness);
                    fogMap[x, y] = Mathf.Clamp01(sharpened);
                }
                else
                {
                    fogMap[x, y] = 0f;
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // BIOMES
    // -------------------------------------------------------------------------

    void GenerateBiomes()
    {
        totalLandTiles = 0;
        List<Vector2Int> landTiles = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (landMap[x, y]) { landTiles.Add(new Vector2Int(x, y)); totalLandTiles++; }

        if (totalLandTiles == 0) return;

        float[] maxRatios   = { biome2MaxRatio, biome3MaxRatio, biome4MaxRatio };
        float[] rolledRatios = new float[3];
        bool[]  biomeActive  = new bool[3];

        for (int i = 0; i < 3; i++)
        {
            rolledRatios[i] = Random.Range(0f, maxRatios[i]);
            if (rolledRatios[i] < minSpawnThreshold) { rolledRatios[i] = 0f; biomeActive[i] = false; }
            else biomeActive[i] = true;
        }

        float totalSecondary = 0f;
        for (int i = 0; i < 3; i++) totalSecondary += rolledRatios[i];
        float biome1Ratio = Mathf.Max(0f, 1f - totalSecondary);

        float[] finalRatios = new float[4];
        finalRatios[0] = biome1Ratio;
        for (int i = 0; i < 3; i++) finalRatios[i + 1] = rolledRatios[i];

        float total = 0f;
        for (int i = 0; i < 4; i++) total += finalRatios[i];
        for (int i = 0; i < 4; i++) finalRatios[i] = total > 0 ? finalRatios[i] / total : 0f;

        List<Vector3> biomeSeeds = new List<Vector3>();
        int totalSeeds  = 12;
        int biome1Seeds = Mathf.Max(1, Mathf.RoundToInt(totalSeeds * finalRatios[0]));
        for (int i = 0; i < biome1Seeds; i++)
        {
            Vector2Int pos = landTiles[Random.Range(0, landTiles.Count)];
            biomeSeeds.Add(new Vector3(pos.x, pos.y, 1));
        }

        for (int bType = 2; bType <= 4; bType++)
        {
            if (!biomeActive[bType - 2]) continue;
            int seedCount = Mathf.Max(1, Mathf.RoundToInt(totalSeeds * finalRatios[bType - 1]));
            for (int i = 0; i < seedCount; i++)
            {
                Vector2Int pos = landTiles[Random.Range(0, landTiles.Count)];
                biomeSeeds.Add(new Vector3(pos.x, pos.y, bType));
            }
        }

        float warpStrength = 25f;
        float warpScale    = 0.05f;
        float noiseOffset  = Random.Range(0f, 1000f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!landMap[x, y]) { biomeMap[x, y] = 0; continue; }

                // Sea rocks get biome 5
                if (seaRockMap[x, y])
                {
                    biomeMap[x, y] = 5;
                    continue;
                }

                float wnx     = (Mathf.PerlinNoise(x * warpScale + noiseOffset, y * warpScale) - 0.5f) * warpStrength;
                float wny     = (Mathf.PerlinNoise(y * warpScale, x * warpScale + noiseOffset) - 0.5f) * warpStrength;
                float warpedX = x + wnx;
                float warpedY = y + wny;

                float minDist     = float.MaxValue;
                int nearestBiome  = 1;

                foreach (var seed in biomeSeeds)
                {
                    float d = Vector2.Distance(new Vector2(warpedX, warpedY), new Vector2(seed.x, seed.y));
                    if (d < minDist) { minDist = d; nearestBiome = (int)seed.z; }
                }

                biomeMap[x, y] = nearestBiome;
            }
        }

        for (int i = 0; i < 5; i++) biomeTileCounts[i] = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int b = biomeMap[x, y];
                if (b >= 1 && b <= 5) biomeTileCounts[b - 1]++;
            }

        Debug.Log($"Biomes - Forest(BG): {(finalRatios[0]*100):F1}%, " +
                  $"Desert: {biomeActive[0]} ({(finalRatios[1]*100):F1}%), " +
                  $"Mountains: {biomeActive[1]} ({(finalRatios[2]*100):F1}%), " +
                  $"Plains: {biomeActive[2]} ({(finalRatios[3]*100):F1}%), " +
                  $"SeaRocks: {biomeTileCounts[4]}tiles");
    }

    void CalculateBiomeRatios()
    {
        if (totalLandTiles == 0) { ForestRatio = DesertRatio = MountainRatio = PlainsRatio = 0f; return; }
        ForestRatio   = (float)biomeTileCounts[0] / totalLandTiles;
        DesertRatio   = (float)biomeTileCounts[1] / totalLandTiles;
        MountainRatio = (float)biomeTileCounts[2] / totalLandTiles;
        PlainsRatio   = (float)biomeTileCounts[3] / totalLandTiles;
    }

    // -------------------------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------------------------

    public int GetBiome(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return 0;
        return biomeMap[x, y];
    }

    public bool IsLand(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return landMap[x, y];
    }

    /// <summary>True if the tile is a sea rock formation. These are land but not usable for gameplay actions.</summary>
    public bool IsSeaRock(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return seaRockMap[x, y];
    }

    /// <summary>True if the tile is normal land (not water, not sea rock). Use this for gameplay placement checks.</summary>
    public bool IsActionableLand(int x, int y)
    {
        return IsLand(x, y) && !IsSeaRock(x, y);
    }

    public float GetFog(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return 1f;
        return fogMap[x, y];
    }

    // -------------------------------------------------------------------------
    // TEXTURE
    // -------------------------------------------------------------------------

    Texture2D CreateTexture()
    {
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;
        Color[] biomeColors = { biome1Color, biome2Color, biome3Color, biome4Color, biome5Color };

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int biome      = biomeMap[x, y];
                Color baseColor = (biome >= 1 && biome <= 5) ? biomeColors[biome - 1] : waterColor;
                if (useFog && fogMap[x, y] > 0)
                    baseColor = Color.Lerp(baseColor, fogColor, fogMap[x, y]);
                texture.SetPixel(x, y, baseColor);
            }

        texture.Apply();
        return texture;
    }

    void ApplyTexture()
    {
        if (mapRenderer == null) return;
        if (mapRenderer.sprite != null) Destroy(mapRenderer.sprite);

        Sprite sprite = Sprite.Create(mapTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), 100f);

        sprite.name         = "GeneratedMap";
        mapRenderer.sprite  = sprite;
        mapRenderer.enabled = true;

        OnMapGenerated?.Invoke();
    }

    public void SetTile(int x, int y, bool isLand, int biome = 1)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        landMap[x, y]  = isLand;
        biomeMap[x, y] = isLand ? Mathf.Clamp(biome, 1, 5) : 0;

        Color[] biomeColors = { biome1Color, biome2Color, biome3Color, biome4Color, biome5Color };
        Color baseColor = isLand ? biomeColors[biomeMap[x, y] - 1] : waterColor;
        if (useFog && fogMap[x, y] > 0)
            baseColor = Color.Lerp(baseColor, fogColor, fogMap[x, y]);

        mapTexture.SetPixel(x, y, baseColor);
        mapTexture.Apply();
    }

    public int[,] GetBiomeMapCopy()
    {
        int[,] copy = new int[width, height];
        System.Array.Copy(biomeMap, copy, biomeMap.Length);
        return copy;
    }

    public bool[,] GetLandMapCopy()
    {
        bool[,] copy = new bool[width, height];
        System.Array.Copy(landMap, copy, landMap.Length);
        return copy;
    }

    void OnValidate()
    {
        width  = Mathf.Max(64, width);
        height = Mathf.Max(64, height);
    }
}