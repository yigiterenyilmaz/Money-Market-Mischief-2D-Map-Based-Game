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
    [Range(1, 5)]  public int minFaultLines = 2;
    [Range(3, 12)] public int maxFaultLines = 7;

    [Header("Fault Shape")]
    [Tooltip("Length of each fault line in steps.")]
    [Range(80, 500)]  public int   segmentLength    = 260;
    [Tooltip("Very low = nearly straight. Real faults are mostly straight.")]
    [Range(0f, 0.08f)] public float curvature       = 0.03f;
    [Tooltip("Chance per step of a lateral offset jump (transform fault step).")]
    [Range(0f, 0.03f)] public float stepOffsetChance = 0.010f;
    [Tooltip("How far the offset jumps sideways in tiles.")]
    [Range(3, 18)] public int stepOffsetDistance     = 7;
    [Tooltip("Fault line width in tiles.")]
    [Range(1, 3)]  public int faultWidth             = 1;

    [Header("En-Echelon")]
    [Tooltip("Chance a fault spawns a parallel offset sister segment.")]
    [Range(0f, 0.5f)]  public float echelonChance      = 0.35f;
    [Tooltip("Lateral offset of sister segment in tiles.")]
    [Range(5, 25)]     public int   echelonOffset       = 10;
    [Tooltip("Sister segment length as fraction of parent.")]
    [Range(0.3f, 0.7f)] public float echelonLengthRatio = 0.45f;

    [Header("Debug")]
    public bool showFaultLinesAlways = false;

    // Fault weight map — intersections accumulate higher values
    private float[,]        faultWeightMap;
    private bool[,]         faultMap;
    private List<Vector2Int> nodePoints = new List<Vector2Int>(); // high-weight intersection nodes
    private int              resolvedFaultCount;

    public bool             IsGenerated       { get; private set; }
    public int              ResolvedFaultCount => resolvedFaultCount;
    public List<Vector2Int> NodePoints         => nodePoints;

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

    // =========================================================================
    // GENERATION
    // =========================================================================

    public void Generate()
    {
        if (mapGenerator == null) { Debug.LogError("FaultLineGenerator: mapGenerator is null!"); return; }

        int w = mapGenerator.width;
        int h = mapGenerator.height;
        faultMap       = new bool[w, h];
        faultWeightMap = new float[w, h];
        nodePoints.Clear();
        IsGenerated = false;

        float eventsIndex = CountryData.Instance != null
            ? CountryData.Instance.NaturalEventsIndex : 0.5f;

        resolvedFaultCount = Mathf.RoundToInt(Mathf.Lerp(minFaultLines, maxFaultLines, eventsIndex));

        // Gather land tiles — bias heavily toward center of island
        List<Vector2Int> allLand    = new List<Vector2Int>();
        List<Vector2Int> centerLand = new List<Vector2Int>();

        float cx = w * 0.5f, cy = h * 0.5f;
        float innerRadius = Mathf.Min(w, h) * 0.28f; // inner 28% of map half-size = center zone

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!mapGenerator.IsActionableLand(x, y)) continue;
            allLand.Add(new Vector2Int(x, y));
            float dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy < innerRadius * innerRadius)
                centerLand.Add(new Vector2Int(x, y));
        }

        if (allLand.Count == 0) { Debug.LogWarning("FaultLineGenerator: No land."); return; }

        // Fall back to all land if island is tiny
        List<Vector2Int> seedPool = centerLand.Count > 20 ? centerLand : allLand;
        List<Vector2Int> seeds    = PlaceSeeds(seedPool, allLand, resolvedFaultCount);

        foreach (var seed in seeds)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI);
            DrawFault(seed, angle, segmentLength, w, h);

            // En-echelon sister
            if (UnityEngine.Random.value < echelonChance)
            {
                Vector2 perp   = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                float   side   = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                Vector2 along  = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 sStart = new Vector2(seed.x, seed.y)
                               + perp * echelonOffset * side
                               + along * UnityEngine.Random.Range(-segmentLength * 0.25f,
                                                                   segmentLength * 0.25f);
                int sx = Mathf.RoundToInt(sStart.x), sy = Mathf.RoundToInt(sStart.y);
                if (sx >= 0 && sx < w && sy >= 0 && sy < h && mapGenerator.IsLand(sx, sy))
                    DrawFault(new Vector2Int(sx, sy),
                              angle + UnityEngine.Random.Range(-0.12f, 0.12f),
                              Mathf.RoundToInt(segmentLength * echelonLengthRatio), w, h);
            }
        }

        // Identify node points: tiles stamped by more than one fault (weight > 1 before bonus)
        float intersectionThreshold = 1.5f;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (faultWeightMap[x, y] > intersectionThreshold)
            {
                nodePoints.Add(new Vector2Int(x, y));
                // Extra weight bonus at intersections — boosts earthquake probability here
                faultWeightMap[x, y] += 5f;
            }
        }

        IsGenerated = true;

        int count = 0;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            if (faultMap[x, y]) count++;

        Debug.Log($"FaultLineGenerator: {resolvedFaultCount} faults, {count} tiles, " +
                  $"{nodePoints.Count} node points. (NaturalEvents={eventsIndex:F2})");

        OnFaultLinesGenerated?.Invoke();
    }

    // =========================================================================
    // DRAW ONE FAULT — long, nearly straight with lateral transform offsets
    // =========================================================================

    void DrawFault(Vector2Int start, float angle, int steps, int w, int h)
    {
        Vector2 pos = start;

        for (int step = 0; step < steps; step++)
        {
            int fx = Mathf.Clamp(Mathf.RoundToInt(pos.x), 0, w - 1);
            int fy = Mathf.Clamp(Mathf.RoundToInt(pos.y), 0, h - 1);

            Stamp(fx, fy, w, h);

            // Peek ahead — bounce inward if leaving land
            Vector2 next  = pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2f;
            int     nx    = Mathf.RoundToInt(next.x), ny = Mathf.RoundToInt(next.y);
            bool leaving  = nx < 0 || nx >= w || ny < 0 || ny >= h
                          || !mapGenerator.IsLand(nx, ny);
            if (leaving)
            {
                // Reflect back toward island center with small random nudge
                Vector2 toCenter = new Vector2(w * 0.5f - pos.x, h * 0.5f - pos.y).normalized;
                angle = Mathf.Atan2(toCenter.y, toCenter.x)
                      + UnityEngine.Random.Range(-0.35f, 0.35f);
            }
            else
            {
                // Transform fault lateral step
                if (UnityEngine.Random.value < stepOffsetChance)
                {
                    Vector2 perp = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                    float   side = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                    Vector2 stepPos = pos;
                    for (int s = 0; s < stepOffsetDistance; s++)
                    {
                        stepPos += perp * side;
                        int scx = Mathf.Clamp(Mathf.RoundToInt(stepPos.x), 0, w - 1);
                        int scy = Mathf.Clamp(Mathf.RoundToInt(stepPos.y), 0, h - 1);
                        if (!mapGenerator.IsLand(scx, scy)) break;
                        Stamp(scx, scy, w, h);
                    }
                    pos = stepPos;
                }

                angle += UnityEngine.Random.Range(-curvature, curvature);
            }

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
            faultMap[nx, ny]        = true;
            faultWeightMap[nx, ny] += 1f;
        }
    }

    // =========================================================================
    // SEED PLACEMENT
    // =========================================================================

    List<Vector2Int> PlaceSeeds(List<Vector2Int> preferredPool, List<Vector2Int> fallbackPool, int count)
    {
        // Spacing: distribute evenly so faults cover the island, not clump
        int spacing   = Mathf.RoundToInt(
            Mathf.Min(mapGenerator.width, mapGenerator.height) * 0.5f / Mathf.Sqrt(count + 1));
        spacing       = Mathf.Max(spacing, 18);
        int spacingSq = spacing * spacing;

        // Shuffle preferred pool
        List<Vector2Int> pool = new List<Vector2Int>(preferredPool);
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

        // Fill from fallback if not enough in center zone
        if (seeds.Count < count)
        {
            List<Vector2Int> fb = new List<Vector2Int>(fallbackPool);
            for (int i = fb.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var t = fb[i]; fb[i] = fb[j]; fb[j] = t;
            }
            foreach (var candidate in fb)
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
        }

        while (seeds.Count < count)
            seeds.Add(fallbackPool[UnityEngine.Random.Range(0, fallbackPool.Count)]);

        return seeds;
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

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