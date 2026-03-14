using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a full road network across the map using smooth Catmull-Rom splines.
/// 
/// Road hierarchy:
///   1. Highways     — wide roads connecting region clusters across the island
///   2. City streets — medium grid-like network inside Cities regions
///   3. Rural paths  — thin winding dirt paths through Agricultural regions
///   4. Industrial   — medium roads in Industrial regions connecting to highways
///
/// All roads are smooth, swirly curves — not jittery random walks.
/// Roads are painted with configurable outlines (darker border).
/// Exposes a public API for other systems (MapDecorPlacer) to query road proximity.
/// </summary>
public class RoadGenerator : MonoBehaviour
{
    public static RoadGenerator Instance { get; private set; }

    // -------------------------------------------------------------------------
    // REFERENCES
    // -------------------------------------------------------------------------

    [Header("References")]
    public MapGenerator mapGenerator;

    // -------------------------------------------------------------------------
    // HIGHWAY SETTINGS
    // -------------------------------------------------------------------------

    [Header("Highways — Inter-Region Connections")]

    [Tooltip("Number of nearest region centers each center tries to connect to.")]
    [Range(1, 4)] public int highwayConnectionCount = 2;

    [Tooltip("Number of random control points inserted between start and end to create curves.")]
    [Range(1, 8)] public int highwayControlPoints = 4;

    [Tooltip("How far control points can drift sideways from the straight line (fraction of total distance).")]
    [Range(0.05f, 0.5f)] public float highwayCurviness = 0.25f;

    [Tooltip("Number of interpolated points per spline segment. Higher = smoother.")]
    [Range(8, 40)] public int highwaySplineResolution = 20;

    [Tooltip("Minimum tile distance between parallel highways. Paths closer than this are rejected.")]
    [Range(5, 60)] public int highwayMinSeparation = 20;

    // -------------------------------------------------------------------------
    // CITY STREET SETTINGS
    // -------------------------------------------------------------------------

    [Header("City Streets — Curved Network Inside Cities")]

    [Tooltip("Approximate spacing between street lines in tiles.")]
    [Range(8, 40)] public int cityStreetSpacing = 16;

    [Tooltip("Border buffer — streets won't generate within this many tiles of a non-city biome.")]
    [Range(0, 15)] public int cityStreetBorderBuffer = 4;

    [Tooltip("Control points per street line for curvature.")]
    [Range(2, 10)] public int cityStreetControlPoints = 5;

    [Tooltip("How far city street control points drift from straight. Fraction of street length.")]
    [Range(0.02f, 0.3f)] public float cityStreetCurviness = 0.08f;

    [Tooltip("Spline resolution per segment for city streets.")]
    [Range(6, 30)] public int cityStreetSplineResolution = 12;

    // -------------------------------------------------------------------------
    // RURAL PATH SETTINGS
    // -------------------------------------------------------------------------

    [Header("Rural Paths — Winding Dirt Tracks in Agricultural Regions")]

    [Tooltip("Number of rural paths to attempt generating.")]
    [Range(0, 30)] public int ruralPathCount = 8;

    [Tooltip("Control points per rural path.")]
    [Range(2, 10)] public int ruralPathControlPoints = 5;

    [Tooltip("How far rural path control points wander. Higher = more winding.")]
    [Range(0.1f, 0.6f)] public float ruralPathCurviness = 0.35f;

    [Tooltip("Spline resolution per segment for rural paths.")]
    [Range(6, 30)] public int ruralPathSplineResolution = 15;

    [Tooltip("Minimum distance between path start and end.")]
    [Range(10, 150)] public int ruralPathMinLength = 40;

    // -------------------------------------------------------------------------
    // INDUSTRIAL ROAD SETTINGS
    // -------------------------------------------------------------------------

    [Header("Industrial Roads — Access Roads in Industrial Zones")]

    [Tooltip("Number of industrial roads to attempt.")]
    [Range(0, 20)] public int industrialRoadCount = 4;

    [Tooltip("Control points per industrial road.")]
    [Range(1, 6)] public int industrialRoadControlPoints = 3;

    [Tooltip("How far industrial road control points drift.")]
    [Range(0.05f, 0.3f)] public float industrialRoadCurviness = 0.10f;

    [Tooltip("Spline resolution per segment for industrial roads.")]
    [Range(6, 30)] public int industrialRoadSplineResolution = 12;

    [Tooltip("Minimum separation between industrial roads (tiles).")]
    [Range(5, 40)] public int industrialRoadMinSeparation = 15;

    // -------------------------------------------------------------------------
    // PER-BIOME ROAD APPEARANCE
    // -------------------------------------------------------------------------

    [Header("Road Appearance — Cities Biome")]
    [Range(2, 8)] public int citiesRoadThickness = 5;
    [Range(0, 4)] public int citiesRoadOutlineWidth = 1;
    public Color citiesRoadFill    = new Color(0.42f, 0.40f, 0.37f);
    public Color citiesRoadOutline = new Color(0.28f, 0.26f, 0.24f);

    [Header("Road Appearance — Agricultural Biome")]
    [Range(1, 4)] public int agriculturalRoadThickness = 2;
    [Range(0, 3)] public int agriculturalRoadOutlineWidth = 1;
    public Color agriculturalRoadFill    = new Color(0.50f, 0.42f, 0.28f);
    public Color agriculturalRoadOutline = new Color(0.38f, 0.32f, 0.20f);

    [Header("Road Appearance — Industrial Biome")]
    [Range(1, 5)] public int industrialBiomeRoadThickness = 3;
    [Range(0, 3)] public int industrialBiomeRoadOutlineWidth = 1;
    public Color industrialBiomeRoadFill    = new Color(0.32f, 0.28f, 0.24f);
    public Color industrialBiomeRoadOutline = new Color(0.20f, 0.17f, 0.14f);

    [Header("Road Appearance — Urban/Nature Biome")]
    [Range(1, 3)] public int urbanRoadThickness = 1;
    [Range(0, 2)] public int urbanRoadOutlineWidth = 1;
    public Color urbanRoadFill    = new Color(0.42f, 0.36f, 0.26f);
    public Color urbanRoadOutline = new Color(0.32f, 0.28f, 0.20f);

    // -------------------------------------------------------------------------
    // GENERAL
    // -------------------------------------------------------------------------

    [Header("General")]

    [Tooltip("Minimum distance from water (in tiles) for any road pixel.")]
    [Range(0, 10)] public int shoreBuffer = 2;

    [Tooltip("Scale factor for road counts on maps larger than 256x256.")]
    public bool scaleWithMapSize = true;

    // -------------------------------------------------------------------------
    // EVENTS
    // -------------------------------------------------------------------------

    public static event Action OnRoadsGenerated;

    // -------------------------------------------------------------------------
    // INTERNAL STATE
    // -------------------------------------------------------------------------

    private int[,] roadTypeMap;
    private int[,] roadDistanceField;

    private List<Vector2Int> allRoadTiles    = new List<Vector2Int>();
    private List<Vector2Int> highwayTiles    = new List<Vector2Int>();
    private List<Vector2Int> cityStreetTiles = new List<Vector2Int>();
    private List<Vector2Int> ruralPathTiles  = new List<Vector2Int>();
    private List<Vector2Int> industrialTiles = new List<Vector2Int>();

    private List<RegionCenter> regionCenters = new List<RegionCenter>();

    private Texture2D _tex;
    private int _w, _h;
    private bool _generated = false;

    private int[,] shoreDistCache;

    // Tracks distance from the nearest highway pixel — used to prevent highway clumping
    private int[,] highwayProximity;

    private static readonly int[] dx4 = { 1, -1, 0, 0 };
    private static readonly int[] dy4 = { 0, 0, 1, -1 };

    // -------------------------------------------------------------------------
    // DATA TYPES
    // -------------------------------------------------------------------------

    public struct RegionCenter
    {
        public Vector2Int position;
        public int biome;
        public int tileCount;
    }

    // -------------------------------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // -------------------------------------------------------------------------
    // PUBLIC ENTRY POINT
    // -------------------------------------------------------------------------

    public void GenerateRoads(MapGenerator map, Texture2D mapTexture)
    {
        if (map == null || mapTexture == null)
        {
            Debug.LogError("RoadGenerator: MapGenerator or texture is null.");
            return;
        }

        _tex = mapTexture;
        _w   = map.width;
        _h   = map.height;

        roadTypeMap = new int[_w, _h];
        allRoadTiles.Clear();
        highwayTiles.Clear();
        cityStreetTiles.Clear();
        ruralPathTiles.Clear();
        industrialTiles.Clear();
        regionCenters.Clear();

        float areaScale = scaleWithMapSize ? Mathf.Sqrt((_w * _h) / (256f * 256f)) : 1f;

        BuildShoreDistanceCache(map);
        FindRegionCenters(map);
        GenerateHighways(map, areaScale);
        GenerateCityStreets(map, areaScale);
        GenerateRuralPaths(map, areaScale);
        GenerateIndustrialRoads(map, areaScale);

        PaintAllRoads();
        _tex.Apply();

        BuildRoadDistanceField();

        _generated = true;

        Debug.Log($"RoadGenerator: highways={highwayTiles.Count}px, " +
                  $"cityStreets={cityStreetTiles.Count}px, " +
                  $"ruralPaths={ruralPathTiles.Count}px, " +
                  $"industrial={industrialTiles.Count}px, " +
                  $"total={allRoadTiles.Count}px, " +
                  $"regionCenters={regionCenters.Count}");

        OnRoadsGenerated?.Invoke();
    }

    // =========================================================================
    // CATMULL-ROM SPLINE CORE
    // =========================================================================

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    /// <summary>
    /// Build a smooth pixel path from control points using Catmull-Rom interpolation.
    /// Uses Bresenham fill between samples so there are no gaps.
    /// </summary>
    List<Vector2Int> SplineToPixels(List<Vector2> controlPoints, int stepsPerSegment)
    {
        List<Vector2Int> pixels = new List<Vector2Int>();
        if (controlPoints.Count < 2) return pixels;

        List<Vector2> pts = new List<Vector2>();
        pts.Add(controlPoints[0] * 2f - controlPoints[1]);
        pts.AddRange(controlPoints);
        pts.Add(controlPoints[controlPoints.Count - 1] * 2f -
                controlPoints[controlPoints.Count - 2]);

        HashSet<long> visited = new HashSet<long>();
        Vector2Int? prev = null;

        for (int i = 1; i < pts.Count - 2; i++)
        {
            for (int s = 0; s <= stepsPerSegment; s++)
            {
                float t = (float)s / stepsPerSegment;
                Vector2 point = CatmullRom(pts[i - 1], pts[i], pts[i + 1], pts[i + 2], t);

                int px = Mathf.RoundToInt(point.x);
                int py = Mathf.RoundToInt(point.y);
                if (px < 0 || px >= _w || py < 0 || py >= _h) { prev = null; continue; }

                Vector2Int current = new Vector2Int(px, py);

                if (prev.HasValue && prev.Value != current)
                {
                    foreach (var bp in BresenhamLine(prev.Value.x, prev.Value.y, px, py))
                    {
                        long bkey = (long)bp.x << 32 | (uint)bp.y;
                        if (!visited.Contains(bkey))
                        {
                            visited.Add(bkey);
                            pixels.Add(bp);
                        }
                    }
                }
                else
                {
                    long key = (long)px << 32 | (uint)py;
                    if (!visited.Contains(key))
                    {
                        visited.Add(key);
                        pixels.Add(current);
                    }
                }

                prev = current;
            }
        }

        return pixels;
    }

    static List<Vector2Int> BresenhamLine(int x0, int y0, int x1, int y1)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            result.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
        return result;
    }

    /// <summary>
    /// Generate control points with Perlin-driven perpendicular drift for organic S-curves.
    /// </summary>
    List<Vector2> BuildControlPoints(Vector2 start, Vector2 end, int numMiddle, float curviness)
    {
        List<Vector2> points = new List<Vector2>();
        points.Add(start);

        Vector2 dir  = end - start;
        float length = dir.magnitude;
        if (length < 1f) { points.Add(end); return points; }

        Vector2 norm = dir.normalized;
        Vector2 perp = new Vector2(-norm.y, norm.x);
        float maxDrift = length * curviness;

        float noiseSeed = UnityEngine.Random.Range(0f, 9999f);

        for (int i = 1; i <= numMiddle; i++)
        {
            float t = (float)i / (numMiddle + 1);
            Vector2 baseline = Vector2.Lerp(start, end, t);

            float drift  = (Mathf.PerlinNoise(t * 2.5f + noiseSeed, noiseSeed * 0.3f) - 0.5f) * 2f * maxDrift;
            float wobble = (Mathf.PerlinNoise(noiseSeed + 500f, t * 5f) - 0.5f) * maxDrift * 0.3f;

            points.Add(baseline + perp * (drift + wobble));
        }

        points.Add(end);
        return points;
    }

    List<Vector2> BuildValidatedControlPoints(MapGenerator map, Vector2 start, Vector2 end,
                                               int numMiddle, float curviness, bool allowCrossBiome)
    {
        List<Vector2> raw = BuildControlPoints(start, end, numMiddle, curviness);

        for (int i = 1; i < raw.Count - 1; i++)
        {
            Vector2 pt = raw[i];
            Vector2 baseline = Vector2.Lerp(start, end, (float)i / (raw.Count - 1));

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 candidate = Vector2.Lerp(pt, baseline, attempt * 0.1f);
                int cx = Mathf.RoundToInt(candidate.x);
                int cy = Mathf.RoundToInt(candidate.y);

                if (IsValidRoadPoint(map, cx, cy))
                {
                    raw[i] = candidate;
                    break;
                }
            }
        }

        return raw;
    }

    bool IsValidRoadPoint(MapGenerator map, int x, int y)
    {
        if (x < 0 || x >= _w || y < 0 || y >= _h) return false;
        if (!map.IsLand(x, y)) return false;
        if (map.IsSeaRock(x, y)) return false;
        if (map.GetFog(x, y) > 0.6f) return false;
        if (!HasShoreBuffer(x, y)) return false;
        return true;
    }

    List<Vector2Int> FilterPathToLand(MapGenerator map, List<Vector2Int> path, bool allowSkipBadPixels)
    {
        List<Vector2Int> valid = new List<Vector2Int>();
        foreach (var p in path)
        {
            if (!IsValidRoadPoint(map, p.x, p.y))
            {
                if (allowSkipBadPixels) continue;
                break;
            }
            valid.Add(p);
        }
        return valid;
    }

    // =========================================================================
    // STEP 0: SHORE DISTANCE CACHE
    // =========================================================================

    void BuildShoreDistanceCache(MapGenerator map)
    {
        shoreDistCache = new int[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                shoreDistCache[x, y] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            {
                if (!map.IsLand(x, y)) continue;
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx4[i], ny = y + dy4[i];
                    if (nx < 0 || nx >= _w || ny < 0 || ny >= _h || !map.IsLand(nx, ny))
                    {
                        shoreDistCache[x, y] = 0;
                        queue.Enqueue(new Vector2Int(x, y));
                        break;
                    }
                }
            }

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d = shoreDistCache[pos.x, pos.y];
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (!map.IsLand(nx, ny)) continue;
                if (shoreDistCache[nx, ny] >= 0) continue;
                shoreDistCache[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    bool HasShoreBuffer(int x, int y)
    {
        if (shoreBuffer <= 0) return true;
        if (x < 0 || x >= _w || y < 0 || y >= _h) return false;
        return shoreDistCache[x, y] >= shoreBuffer;
    }

    // =========================================================================
    // STEP 1: FIND REGION CENTERS
    // =========================================================================

    void FindRegionCenters(MapGenerator map)
    {
        bool[,] visited = new bool[_w, _h];

        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            {
                if (visited[x, y]) continue;
                if (!map.IsLand(x, y)) continue;
                int biome = map.GetBiome(x, y);
                if (biome < 1 || biome > 4) continue;

                List<Vector2Int> cluster = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                while (queue.Count > 0)
                {
                    var pos = queue.Dequeue();
                    cluster.Add(pos);

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                        if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                        if (visited[nx, ny]) continue;
                        if (!map.IsLand(nx, ny)) continue;
                        if (map.GetBiome(nx, ny) != biome) continue;
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }

                if (cluster.Count < 50) continue;

                long sumX = 0, sumY = 0;
                foreach (var p in cluster) { sumX += p.x; sumY += p.y; }
                int cx = (int)(sumX / cluster.Count);
                int cy = (int)(sumY / cluster.Count);

                Vector2Int center = SnapToNearestInList(new Vector2Int(cx, cy), cluster);

                regionCenters.Add(new RegionCenter
                {
                    position  = center,
                    biome     = biome,
                    tileCount = cluster.Count
                });
            }
    }

    Vector2Int SnapToNearestInList(Vector2Int target, List<Vector2Int> tiles)
    {
        float bestDist = float.MaxValue;
        Vector2Int best = target;
        int step = Mathf.Max(1, tiles.Count / 200);
        for (int i = 0; i < tiles.Count; i += step)
        {
            float d = Vector2Int.Distance(target, tiles[i]);
            if (d < bestDist) { bestDist = d; best = tiles[i]; }
        }
        return best;
    }

    // =========================================================================
    // STEP 2: HIGHWAYS (spline-based, with separation enforcement)
    // =========================================================================

    void GenerateHighways(MapGenerator map, float areaScale)
    {
        if (regionCenters.Count < 2) return;

        // Initialize highway proximity map
        highwayProximity = new int[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                highwayProximity[x, y] = int.MaxValue;

        HashSet<long> connected = new HashSet<long>();
        // Track which centers are already reachable via existing highways
        // (union-find style via simple set of connected groups)
        List<HashSet<int>> connectedGroups = new List<HashSet<int>>();

        for (int i = 0; i < regionCenters.Count; i++)
        {
            List<(float dist, int idx)> sorted = new List<(float, int)>();
            for (int j = 0; j < regionCenters.Count; j++)
            {
                if (i == j) continue;
                float d = Vector2Int.Distance(regionCenters[i].position, regionCenters[j].position);
                sorted.Add((d, j));
            }
            sorted.Sort((a, b) => a.dist.CompareTo(b.dist));

            int connections = Mathf.Min(highwayConnectionCount, sorted.Count);
            for (int n = 0; n < connections; n++)
            {
                int j = sorted[n].idx;
                long pairKey = i < j ? ((long)i << 32 | (uint)j) : ((long)j << 32 | (uint)i);
                if (connected.Contains(pairKey)) continue;

                // Skip if these two centers are already connected through other highways
                if (AreIndirectlyConnected(i, j, connectedGroups)) continue;

                connected.Add(pairKey);

                Vector2 start = regionCenters[i].position;
                Vector2 end   = regionCenters[j].position;

                int ctrlPts = Mathf.Max(2, Mathf.RoundToInt(highwayControlPoints * areaScale));
                List<Vector2> controlPts = BuildValidatedControlPoints(
                    map, start, end, ctrlPts, highwayCurviness, true);

                // Push control points away from existing highways
                RepelControlPointsFromHighways(controlPts, start, end);

                List<Vector2Int> pixels = SplineToPixels(controlPts, highwaySplineResolution);
                List<Vector2Int> filtered = FilterPathToLand(map, pixels, true);

                // Check how much of this path overlaps with existing highways
                if (filtered.Count > 0 && GetPathOverlapRatio(filtered) > 0.4f)
                    continue; // Too much overlap, skip this connection

                if (filtered.Count < 10) continue; // Too short to be useful

                foreach (var p in filtered)
                    RegisterRoadTile(p, 1, highwayTiles);

                // Update highway proximity BFS for the new path
                UpdateHighwayProximity(filtered);

                // Merge connected groups
                MergeConnectedGroups(i, j, connectedGroups);
            }
        }
    }

    /// <summary>Check if two region centers are already indirectly connected through highway groups.</summary>
    bool AreIndirectlyConnected(int a, int b, List<HashSet<int>> groups)
    {
        foreach (var group in groups)
            if (group.Contains(a) && group.Contains(b)) return true;
        return false;
    }

    /// <summary>Merge two centers into the same connected group.</summary>
    void MergeConnectedGroups(int a, int b, List<HashSet<int>> groups)
    {
        HashSet<int> groupA = null, groupB = null;
        foreach (var g in groups)
        {
            if (g.Contains(a)) groupA = g;
            if (g.Contains(b)) groupB = g;
        }

        if (groupA == null && groupB == null)
        {
            groups.Add(new HashSet<int> { a, b });
        }
        else if (groupA != null && groupB == null)
        {
            groupA.Add(b);
        }
        else if (groupA == null && groupB != null)
        {
            groupB.Add(a);
        }
        else if (groupA != groupB)
        {
            // Merge B into A
            foreach (var item in groupB) groupA.Add(item);
            groups.Remove(groupB);
        }
    }

    /// <summary>Push control points away from existing highway pixels.</summary>
    void RepelControlPointsFromHighways(List<Vector2> controlPts, Vector2 start, Vector2 end)
    {
        if (highwayProximity == null) return;

        Vector2 dir = end - start;
        float length = dir.magnitude;
        if (length < 1f) return;
        Vector2 perp = new Vector2(-dir.normalized.y, dir.normalized.x);

        // Skip first and last (start/end are fixed)
        for (int i = 1; i < controlPts.Count - 1; i++)
        {
            Vector2 pt = controlPts[i];
            int px = Mathf.RoundToInt(pt.x);
            int py = Mathf.RoundToInt(pt.y);

            if (px < 0 || px >= _w || py < 0 || py >= _h) continue;

            int dist = highwayProximity[px, py];
            if (dist >= highwayMinSeparation) continue;

            // Find which direction to push (away from nearest highway)
            // Sample both perpendicular directions, push toward the farther one
            float pushAmount = (highwayMinSeparation - dist) * 0.8f;

            Vector2 pushA = pt + perp * pushAmount;
            Vector2 pushB = pt - perp * pushAmount;

            int distA = SampleHighwayProximity(pushA);
            int distB = SampleHighwayProximity(pushB);

            controlPts[i] = (distA >= distB) ? pushA : pushB;
        }
    }

    int SampleHighwayProximity(Vector2 pt)
    {
        int px = Mathf.RoundToInt(pt.x);
        int py = Mathf.RoundToInt(pt.y);
        if (px < 0 || px >= _w || py < 0 || py >= _h) return 0;
        return highwayProximity[px, py];
    }

    /// <summary>Returns fraction (0-1) of path pixels that are within separation distance of existing highways.</summary>
    float GetPathOverlapRatio(List<Vector2Int> path)
    {
        if (path.Count == 0) return 0f;
        int closeCount = 0;
        int checkSep = highwayMinSeparation / 2; // Use half separation for overlap check
        foreach (var p in path)
        {
            if (p.x >= 0 && p.x < _w && p.y >= 0 && p.y < _h)
                if (highwayProximity[p.x, p.y] < checkSep)
                    closeCount++;
        }
        return (float)closeCount / path.Count;
    }

    /// <summary>BFS update of highway proximity from newly placed highway tiles.</summary>
    void UpdateHighwayProximity(List<Vector2Int> newTiles)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        foreach (var tile in newTiles)
        {
            if (tile.x < 0 || tile.x >= _w || tile.y < 0 || tile.y >= _h) continue;
            if (highwayProximity[tile.x, tile.y] == 0) continue;
            highwayProximity[tile.x, tile.y] = 0;
            queue.Enqueue(tile);
        }

        int maxSpread = highwayMinSeparation + 10; // Spread a bit beyond separation for smooth repel
        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d = highwayProximity[pos.x, pos.y];
            if (d >= maxSpread) continue;

            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (highwayProximity[nx, ny] <= d + 1) continue;
                highwayProximity[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    // =========================================================================
    // STEP 3: CITY STREETS
    // =========================================================================

    void GenerateCityStreets(MapGenerator map, float areaScale)
    {
        int spacing = Mathf.Max(4, Mathf.RoundToInt(cityStreetSpacing * areaScale));

        int minX = _w, maxX = 0, minY = _h, maxY = 0;
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                if (map.IsLand(x, y) && map.GetBiome(x, y) == 2)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }

        if (maxX <= minX || maxY <= minY) return;

        // Horizontal
        for (int y = minY + spacing; y < maxY; y += spacing)
        {
            int left = -1, right = -1;
            for (int x = minX; x <= maxX; x++)
                if (IsCityStreetCandidate(map, x, y)) { left = x; break; }
            for (int x = maxX; x >= minX; x--)
                if (IsCityStreetCandidate(map, x, y)) { right = x; break; }

            if (left < 0 || right < 0 || right - left < spacing) continue;

            List<Vector2> ctrl = BuildControlPoints(
                new Vector2(left, y), new Vector2(right, y),
                cityStreetControlPoints, cityStreetCurviness);
            List<Vector2Int> pixels = SplineToPixels(ctrl, cityStreetSplineResolution);

            foreach (var p in pixels)
                if (IsCityStreetCandidate(map, p.x, p.y))
                    RegisterRoadTile(p, 2, cityStreetTiles);
        }

        // Vertical
        for (int x = minX + spacing; x < maxX; x += spacing)
        {
            int bottom = -1, top = -1;
            for (int y = minY; y <= maxY; y++)
                if (IsCityStreetCandidate(map, x, y)) { bottom = y; break; }
            for (int y = maxY; y >= minY; y--)
                if (IsCityStreetCandidate(map, x, y)) { top = y; break; }

            if (bottom < 0 || top < 0 || top - bottom < spacing) continue;

            List<Vector2> ctrl = BuildControlPoints(
                new Vector2(x, bottom), new Vector2(x, top),
                cityStreetControlPoints, cityStreetCurviness);
            List<Vector2Int> pixels = SplineToPixels(ctrl, cityStreetSplineResolution);

            foreach (var p in pixels)
                if (IsCityStreetCandidate(map, p.x, p.y))
                    RegisterRoadTile(p, 2, cityStreetTiles);
        }
    }

    bool IsCityStreetCandidate(MapGenerator map, int x, int y)
    {
        if (x < 0 || x >= _w || y < 0 || y >= _h) return false;
        if (!map.IsLand(x, y)) return false;
        if (map.GetBiome(x, y) != 2) return false;
        if (map.GetFog(x, y) > 0.5f) return false;
        if (!HasShoreBuffer(x, y)) return false;

        if (cityStreetBorderBuffer > 0)
        {
            for (int ddx = -cityStreetBorderBuffer; ddx <= cityStreetBorderBuffer; ddx++)
                for (int ddy = -cityStreetBorderBuffer; ddy <= cityStreetBorderBuffer; ddy++)
                {
                    if (ddx * ddx + ddy * ddy > cityStreetBorderBuffer * cityStreetBorderBuffer) continue;
                    int nx = x + ddx, ny = y + ddy;
                    if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) return false;
                    if (!map.IsLand(nx, ny) || map.GetBiome(nx, ny) != 2) return false;
                }
        }

        return true;
    }

    // =========================================================================
    // STEP 4: RURAL PATHS
    // =========================================================================

    void GenerateRuralPaths(MapGenerator map, float areaScale)
    {
        int count = Mathf.RoundToInt(ruralPathCount * areaScale);

        List<Vector2Int> agTiles = new List<Vector2Int>();
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                if (map.IsLand(x, y) && map.GetBiome(x, y) == 1 && map.GetFog(x, y) < 0.5f)
                    agTiles.Add(new Vector2Int(x, y));

        if (agTiles.Count < ruralPathMinLength) return;

        for (int p = 0; p < count; p++)
        {
            Vector2Int startTile = agTiles[UnityEngine.Random.Range(0, agTiles.Count)];
            Vector2Int endTile = startTile;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2Int candidate = agTiles[UnityEngine.Random.Range(0, agTiles.Count)];
                if (Vector2Int.Distance(startTile, candidate) >= ruralPathMinLength)
                { endTile = candidate; break; }
            }
            if (endTile == startTile) continue;

            if (highwayTiles.Count > 0 && UnityEngine.Random.value < 0.5f)
            {
                Vector2Int nearHwy = FindNearestInList(startTile, highwayTiles, 100);
                if (nearHwy.x >= 0) endTile = nearHwy;
            }

            List<Vector2> ctrl = BuildValidatedControlPoints(
                map, (Vector2)startTile, (Vector2)endTile,
                ruralPathControlPoints, ruralPathCurviness, false);

            List<Vector2Int> pixels = SplineToPixels(ctrl, ruralPathSplineResolution);
            List<Vector2Int> filtered = FilterPathToLand(map, pixels, false);

            if (filtered.Count < ruralPathMinLength / 2) continue;

            foreach (var t in filtered)
                RegisterRoadTile(t, 3, ruralPathTiles);
        }
    }

    // =========================================================================
    // STEP 5: INDUSTRIAL ROADS (cleaner, with separation)
    // =========================================================================

    void GenerateIndustrialRoads(MapGenerator map, float areaScale)
    {
        int count = Mathf.RoundToInt(industrialRoadCount * areaScale);

        List<Vector2Int> indTiles = new List<Vector2Int>();
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                if (map.IsLand(x, y) && map.GetBiome(x, y) == 3 && map.GetFog(x, y) < 0.5f)
                    indTiles.Add(new Vector2Int(x, y));

        if (indTiles.Count < 20) return;

        // Track placed industrial road centers to enforce separation
        List<Vector2Int> placedEndpoints = new List<Vector2Int>();

        for (int r = 0; r < count; r++)
        {
            Vector2Int startTile = indTiles[UnityEngine.Random.Range(0, indTiles.Count)];

            // Enforce separation from existing industrial road endpoints
            bool tooClose = false;
            foreach (var ep in placedEndpoints)
                if (Vector2Int.Distance(startTile, ep) < industrialRoadMinSeparation)
                { tooClose = true; break; }
            if (tooClose) continue;

            Vector2Int endTile = startTile;

            // Prefer connecting to nearest highway
            if (highwayTiles.Count > 0)
            {
                Vector2Int nearHwy = FindNearestInList(startTile, highwayTiles, 150);
                if (nearHwy.x >= 0) endTile = nearHwy;
            }

            // Fallback: connect to another distant industrial tile
            if (endTile == startTile)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    Vector2Int candidate = indTiles[UnityEngine.Random.Range(0, indTiles.Count)];
                    if (Vector2Int.Distance(startTile, candidate) >= 40)
                    { endTile = candidate; break; }
                }
            }
            if (endTile == startTile) continue;

            List<Vector2> ctrl = BuildValidatedControlPoints(
                map, (Vector2)startTile, (Vector2)endTile,
                industrialRoadControlPoints, industrialRoadCurviness, true);

            List<Vector2Int> pixels = SplineToPixels(ctrl, industrialRoadSplineResolution);
            List<Vector2Int> filtered = FilterPathToLand(map, pixels, true);

            if (filtered.Count < 15) continue;

            foreach (var t in filtered)
                RegisterRoadTile(t, 4, industrialTiles);

            placedEndpoints.Add(startTile);
            placedEndpoints.Add(endTile);
        }
    }

    // =========================================================================
    // ROAD TILE REGISTRATION
    // =========================================================================

    void RegisterRoadTile(Vector2Int tile, int roadType, List<Vector2Int> typeList)
    {
        if (tile.x < 0 || tile.x >= _w || tile.y < 0 || tile.y >= _h) return;

        int existing = roadTypeMap[tile.x, tile.y];
        if (existing == 0 || roadType < existing)
        {
            if (existing == 0)
                allRoadTiles.Add(tile);
            roadTypeMap[tile.x, tile.y] = roadType;
        }

        typeList.Add(tile);
    }

    // =========================================================================
    // PAINTING — biome-adaptive per-pixel appearance
    // =========================================================================

    MapGenerator _mapRef; // cached for paint phase

    void PaintAllRoads()
    {
        _mapRef = mapGenerator;
        if (_mapRef == null) return;

        // Collect all unique road pixels with their road type
        // Paint outline pass first, then fill pass — both adapt to local biome
        // Order: lowest priority roads first, highest last

        PaintAdaptiveRoad(ruralPathTiles,  3);
        PaintAdaptiveRoad(industrialTiles, 4);
        PaintAdaptiveRoad(cityStreetTiles, 2);
        PaintAdaptiveRoad(highwayTiles,    1);
    }

    void PaintAdaptiveRoad(List<Vector2Int> tiles, int roadType)
    {
        if (tiles.Count == 0) return;

        // Pass 1: outline
        foreach (var tile in tiles)
        {
            int biome = GetBiomeAt(tile.x, tile.y);
            GetBiomeRoadAppearance(biome, roadType, out _, out Color outlineColor,
                                   out int thickness, out int outlineWidth);

            if (outlineWidth <= 0) continue;

            int totalHalf = (thickness + outlineWidth * 2) / 2;
            int totalHalfSq = totalHalf * totalHalf;

            for (int ddx = -totalHalf; ddx <= totalHalf; ddx++)
                for (int ddy = -totalHalf; ddy <= totalHalf; ddy++)
                {
                    if (ddx * ddx + ddy * ddy > totalHalfSq) continue;
                    int px = tile.x + ddx, py = tile.y + ddy;
                    if (px >= 0 && px < _w && py >= 0 && py < _h)
                        _tex.SetPixel(px, py, outlineColor);
                }
        }

        // Pass 2: fill
        foreach (var tile in tiles)
        {
            int biome = GetBiomeAt(tile.x, tile.y);
            GetBiomeRoadAppearance(biome, roadType, out Color fillColor, out _,
                                   out int thickness, out _);

            int fillHalf = thickness / 2;
            int fillHalfSq = fillHalf * fillHalf;

            for (int ddx = -fillHalf; ddx <= fillHalf; ddx++)
                for (int ddy = -fillHalf; ddy <= fillHalf; ddy++)
                {
                    if (ddx * ddx + ddy * ddy > fillHalfSq) continue;
                    int px = tile.x + ddx, py = tile.y + ddy;
                    if (px >= 0 && px < _w && py >= 0 && py < _h)
                        _tex.SetPixel(px, py, fillColor);
                }
        }
    }

    int GetBiomeAt(int x, int y)
    {
        if (_mapRef == null || x < 0 || x >= _w || y < 0 || y >= _h) return 1;
        return _mapRef.GetBiome(x, y);
    }

    /// <summary>
    /// Returns road fill/outline color and thickness based on what biome the pixel sits in.
    /// A highway through farmland becomes a thin dirt road; through cities it stays wide asphalt.
    /// </summary>
    void GetBiomeRoadAppearance(int biome, int roadType,
                                 out Color fillColor, out Color outlineColor,
                                 out int thickness, out int outlineWidth)
    {
        // Default to the biome's appearance regardless of road type
        // Road type only matters as a minor modifier (highways get +1 thickness in cities)
        switch (biome)
        {
            case 2: // Cities
                fillColor    = citiesRoadFill;
                outlineColor = citiesRoadOutline;
                thickness    = citiesRoadThickness;
                outlineWidth = citiesRoadOutlineWidth;
                // City streets are thinner than highways passing through
                if (roadType == 2) thickness = Mathf.Max(1, thickness - 2);
                break;

            case 1: // Agricultural
                fillColor    = agriculturalRoadFill;
                outlineColor = agriculturalRoadOutline;
                thickness    = agriculturalRoadThickness;
                outlineWidth = agriculturalRoadOutlineWidth;
                break;

            case 3: // Industrial
                fillColor    = industrialBiomeRoadFill;
                outlineColor = industrialBiomeRoadOutline;
                thickness    = industrialBiomeRoadThickness;
                outlineWidth = industrialBiomeRoadOutlineWidth;
                break;

            case 4: // Urban / Nature
                fillColor    = urbanRoadFill;
                outlineColor = urbanRoadOutline;
                thickness    = urbanRoadThickness;
                outlineWidth = urbanRoadOutlineWidth;
                break;

            default: // Fallback (shouldn't happen on land)
                fillColor    = agriculturalRoadFill;
                outlineColor = agriculturalRoadOutline;
                thickness    = 1;
                outlineWidth = 0;
                break;
        }
    }

    // =========================================================================
    // ROAD DISTANCE FIELD
    // =========================================================================

    void BuildRoadDistanceField()
    {
        roadDistanceField = new int[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                roadDistanceField[x, y] = int.MaxValue;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        foreach (var tile in allRoadTiles)
        {
            roadDistanceField[tile.x, tile.y] = 0;
            queue.Enqueue(tile);
        }

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d = roadDistanceField[pos.x, pos.y];
            if (d >= 60) continue;

            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (roadDistanceField[nx, ny] <= d + 1) continue;
                roadDistanceField[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public bool IsRoad(int x, int y)
    {
        if (!_generated || x < 0 || x >= _w || y < 0 || y >= _h) return false;
        return roadTypeMap[x, y] > 0;
    }

    public int GetRoadType(int x, int y)
    {
        if (!_generated || x < 0 || x >= _w || y < 0 || y >= _h) return 0;
        return roadTypeMap[x, y];
    }

    public int GetDistanceToRoad(int x, int y)
    {
        if (!_generated || x < 0 || x >= _w || y < 0 || y >= _h) return int.MaxValue;
        return roadDistanceField[x, y];
    }

    public bool IsNearRoad(int x, int y, int maxDistance)
    {
        return GetDistanceToRoad(x, y) <= maxDistance;
    }

    public bool IsNearRoadType(int x, int y, int roadType, int maxDistance)
    {
        if (!_generated) return false;
        for (int ddx = -maxDistance; ddx <= maxDistance; ddx++)
            for (int ddy = -maxDistance; ddy <= maxDistance; ddy++)
            {
                if (ddx * ddx + ddy * ddy > maxDistance * maxDistance) continue;
                int nx = x + ddx, ny = y + ddy;
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (roadTypeMap[nx, ny] == roadType) return true;
            }
        return false;
    }

    public IReadOnlyList<RegionCenter> GetRegionCenters() => regionCenters.AsReadOnly();
    public IReadOnlyList<Vector2Int> GetHighwayTiles() => highwayTiles.AsReadOnly();
    public int[,] GetRoadTypeMap() => roadTypeMap;
    public bool IsGenerated => _generated;

    // =========================================================================
    // HELPERS
    // =========================================================================

    Vector2Int FindNearestInList(Vector2Int from, List<Vector2Int> list, float maxDist)
    {
        float bestDist = maxDist;
        Vector2Int best = new Vector2Int(-1, -1);
        int step = Mathf.Max(1, list.Count / 300);
        for (int i = 0; i < list.Count; i += step)
        {
            float d = Vector2Int.Distance(from, list[i]);
            if (d < bestDist) { bestDist = d; best = list[i]; }
        }
        return best;
    }

    public void Clear()
    {
        allRoadTiles.Clear();
        highwayTiles.Clear();
        cityStreetTiles.Clear();
        ruralPathTiles.Clear();
        industrialTiles.Clear();
        regionCenters.Clear();
        roadTypeMap = null;
        roadDistanceField = null;
        shoreDistCache = null;
        highwayProximity = null;
        _tex = null;
        _generated = false;
    }
}