using System;
using System.Collections.Generic;
using UnityEngine;

public class FaultLineGenerator : MonoBehaviour
{
    public static FaultLineGenerator Instance { get; private set; }
    public static event Action OnFaultLinesGenerated;

    [Header("References")]
    public MapGenerator mapGenerator;

    [Header("Fault Count — driven by NaturalEventsIndex")]
    [Tooltip("Minimum fault lines regardless of natural events stat.")]
    [Range(1, 5)]  public int minFaultLines = 2;
    [Tooltip("Maximum fault lines at NaturalEventsIndex = 1.")]
    [Range(3, 16)] public int maxFaultLines = 9;

    [Header("Fault Shape")]
    [Tooltip("Base length of each fault segment in steps.")]
    [Range(60, 400)] public int segmentLength = 180;
    [Tooltip("Very low = nearly straight. Real faults are mostly straight.")]
    [Range(0f, 0.12f)] public float curvature = 0.04f;
    [Tooltip("Chance per step of a lateral offset jump (transform fault step).")]
    [Range(0f, 0.04f)] public float stepOffsetChance = 0.012f;
    [Tooltip("How far the offset jumps sideways in tiles.")]
    [Range(3, 20)] public int stepOffsetDistance = 8;
    [Tooltip("Fault line width in tiles.")]
    [Range(1, 4)] public int faultWidth = 1;

    [Header("En-Echelon")]
    [Tooltip("Chance a fault spawns a parallel offset sister segment.")]
    [Range(0f, 0.6f)] public float echelonChance = 0.4f;
    [Tooltip("Lateral offset of sister segment in tiles.")]
    [Range(5, 30)] public int echelonOffset = 12;
    [Tooltip("Sister segment length as fraction of parent.")]
    [Range(0.3f, 0.8f)] public float echelonLengthRatio = 0.5f;

    [Header("Intersection Weighting")]
    [Tooltip("Intersection tiles get this multiplier added to their weight.")]
    public float intersectionWeightBonus = 4f;

    [Header("Debug")]
    public bool showFaultLinesAlways = false;

    // Per-tile fault weight used for earthquake epicenter selection
    private float[,] faultWeightMap;
    private bool[,]  faultMap;
    private int      resolvedFaultCount;

    public bool  IsGenerated      { get; private set; }
    public int   ResolvedFaultCount => resolvedFaultCount;

    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  { if (mapGenerator != null) mapGenerator.OnMapGenerated += Generate; }
    void OnDisable() { if (mapGenerator != null) mapGenerator.OnMapGenerated -= Generate; }

    void Start()
    {
        if (!IsGenerated && mapGenerator != null &&
            mapGenerator.IsLand(mapGenerator.width / 2, mapGenerator.height / 2))
            Generate();
    }

    // -------------------------------------------------------------------------
    // GENERATION
    // -------------------------------------------------------------------------

    public void Generate()
    {
        if (mapGenerator == null) { Debug.LogError("FaultLineGenerator: mapGenerator is null!"); return; }

        int w = mapGenerator.width;
        int h = mapGenerator.height;
        faultMap       = new bool[w, h];
        faultWeightMap = new float[w, h];
        IsGenerated    = false;

        // Fault count driven by NaturalEventsIndex
        float eventsIndex = CountryData.Instance != null
            ? CountryData.Instance.NaturalEventsIndex
            : 0.5f;
        resolvedFaultCount = Mathf.RoundToInt(Mathf.Lerp(minFaultLines, maxFaultLines, eventsIndex));

        List<Vector2Int> landTiles = new List<Vector2Int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (mapGenerator.IsActionableLand(x, y))
                    landTiles.Add(new Vector2Int(x, y));

        if (landTiles.Count == 0) { Debug.LogWarning("FaultLineGenerator: No land."); return; }

        List<Vector2Int> seeds = PlaceSeeds(landTiles, resolvedFaultCount);

        foreach (var seed in seeds)
        {
            // Real faults tend to run in consistent regional directions.
            // Pick a dominant angle per fault with small variation.
            float angle = UnityEngine.Random.Range(0f, Mathf.PI); // 0–180 so no doubling
            DrawFault(seed, angle, segmentLength, w, h);

            // En-echelon sister
            if (UnityEngine.Random.value < echelonChance)
            {
                Vector2 perp   = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                float   side   = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                Vector2 offset = perp * echelonOffset * side;
                // Stagger start along the fault direction
                Vector2 along  = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 sStart = new Vector2(seed.x, seed.y)
                               + offset
                               + along * UnityEngine.Random.Range(-segmentLength * 0.3f,
                                                                   segmentLength * 0.3f);
                int sx = Mathf.RoundToInt(sStart.x), sy = Mathf.RoundToInt(sStart.y);
                if (sx >= 0 && sx < w && sy >= 0 && sy < h && mapGenerator.IsLand(sx, sy))
                    DrawFault(new Vector2Int(sx, sy), angle
                              + UnityEngine.Random.Range(-0.15f, 0.15f),
                              Mathf.RoundToInt(segmentLength * echelonLengthRatio), w, h);
            }
        }

        // Build intersection bonus — tiles stamped more than once get extra weight
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (faultWeightMap[x, y] > 1.01f)
                faultWeightMap[x, y] += intersectionWeightBonus;

        IsGenerated = true;

        int count = 0;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (faultMap[x, y]) count++;

        Debug.Log($"FaultLineGenerator: {resolvedFaultCount} faults " +
                  $"(NaturalEvents={eventsIndex:F2}), {count} tiles.");
        OnFaultLinesGenerated?.Invoke();
    }

    // -------------------------------------------------------------------------
    // DRAW ONE FAULT — long, nearly straight, with occasional transform offsets
    // -------------------------------------------------------------------------

    void DrawFault(Vector2Int start, float angle, int steps, int w, int h)
    {
        Vector2 pos = start;

        for (int step = 0; step < steps; step++)
        {
            int cx = Mathf.Clamp(Mathf.RoundToInt(pos.x), 0, w - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt(pos.y), 0, h - 1);

            Stamp(cx, cy, w, h);

            // Peek ahead — bounce inward if leaving land
            Vector2 next = pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2f;
            int nx2 = Mathf.RoundToInt(next.x), ny2 = Mathf.RoundToInt(next.y);
            bool leaving = nx2 < 0 || nx2 >= w || ny2 < 0 || ny2 >= h
                         || !mapGenerator.IsLand(nx2, ny2);
            if (leaving)
            {
                Vector2 toCenter = new Vector2(w * 0.5f - pos.x, h * 0.5f - pos.y).normalized;
                angle = Mathf.Atan2(toCenter.y, toCenter.x)
                      + UnityEngine.Random.Range(-0.3f, 0.3f);
            }

            // Transform fault lateral step — sudden sideways jump
            if (UnityEngine.Random.value < stepOffsetChance)
            {
                Vector2 perp = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                float   side = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                // Draw short connector across the step
                Vector2 stepPos = pos;
                for (int s = 0; s < stepOffsetDistance; s++)
                {
                    stepPos += perp * side;
                    int scx = Mathf.Clamp(Mathf.RoundToInt(stepPos.x), 0, w - 1);
                    int scy = Mathf.Clamp(Mathf.RoundToInt(stepPos.y), 0, h - 1);
                    Stamp(scx, scy, w, h);
                }
                pos = stepPos;
            }

            // Very gentle curvature — real faults are nearly straight
            angle += UnityEngine.Random.Range(-curvature, curvature);

            pos += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2f;
        }
    }

    void Stamp(int cx, int cy, int w, int h)
    {
        for (int dx = -faultWidth; dx <= faultWidth; dx++)
        for (int dy = -faultWidth; dy <= faultWidth; dy++)
        {
            if (dx * dx + dy * dy > faultWidth * faultWidth) continue;
            int nx = cx + dx, ny = cy + dy;
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
            faultMap[nx, ny] = true;
            faultWeightMap[nx, ny] += 1f; // accumulate for intersection detection
        }
    }

    // -------------------------------------------------------------------------
    // SEED PLACEMENT — Poisson-disk spacing
    // -------------------------------------------------------------------------

    List<Vector2Int> PlaceSeeds(List<Vector2Int> landTiles, int count)
    {
        // Spacing scales with map size and fault count so they spread out
        int spacing = Mathf.RoundToInt(
            Mathf.Min(mapGenerator.width, mapGenerator.height) * 0.55f / Mathf.Sqrt(count));
        spacing = Mathf.Max(spacing, 20);
        int spacingSq = spacing * spacing;

        // Shuffle
        List<Vector2Int> pool = new List<Vector2Int>(landTiles);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var t = pool[i]; pool[i] = pool[j]; pool[j] = t;
        }

        List<Vector2Int> seeds = new List<Vector2Int>();
        foreach (var candidate in pool)
        {
            if (seeds.Count >= count) break;
            bool ok = true;
            foreach (var s in seeds)
            {
                int ddx = candidate.x - s.x, ddy = candidate.y - s.y;
                if (ddx * ddx + ddy * ddy < spacingSq) { ok = false; break; }
            }
            if (ok) seeds.Add(candidate);
        }

        while (seeds.Count < count)
            seeds.Add(landTiles[UnityEngine.Random.Range(0, landTiles.Count)]);

        return seeds;
    }

    // -------------------------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------------------------

    public bool IsFault(int x, int y)
    {
        if (!IsGenerated || faultMap == null) return false;
        if (x < 0 || x >= mapGenerator.width || y < 0 || y >= mapGenerator.height) return false;
        return faultMap[x, y];
    }

    public bool[,]  GetFaultMap()       => faultMap;
    public float[,] GetFaultWeightMap() => faultWeightMap;

    [ContextMenu("Debug: Force Generate")]
    public void DebugForceGenerate() => Generate();
}