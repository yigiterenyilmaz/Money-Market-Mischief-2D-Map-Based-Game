using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prosedüral yol ağı üretici.
///
/// Yol hiyerarşisi:
///   1. Highway   — tüm bölgeleri bağlayan kalın ana arter (biyom fark etmez)
///   2. Dallanma  — highway'den yarım parabol şeklinde ayrılan, biyoma göre görünüm alan yollar
///
/// Highway yakınlarında dallanma dışında başka yol bulunmaz.
/// </summary>
public class RoadGenerator : MonoBehaviour
{
    public static RoadGenerator Instance { get; private set; }

    [Header("References")]
    public MapGenerator mapGenerator;

    // -------------------------------------------------------------------------
    // HIGHWAY
    // -------------------------------------------------------------------------

    [Header("Highway — Ana Arter")]
    [Tooltip("Kontrol noktası sayısı (eğri yumuşaklığı).")]
    [Range(1, 8)] public int highwayControlPoints = 4;
    [Tooltip("Kontrol noktası sapma oranı. Düşük = daha düz.")]
    [Range(0.01f, 0.5f)] public float highwayCurviness = 0.30f;
    [Tooltip("Spline çözünürlüğü (segment başına).")]
    [Range(8, 40)] public int highwaySplineResolution = 20;
    [Tooltip("Highway kalınlığı.")]
    [Range(2, 8)] public int highwayThickness = 4;
    [Tooltip("Highway outline genişliği.")]
    [Range(0, 3)] public int highwayOutlineWidth = 1;
    [Tooltip("Highway'in kıyıdan minimum uzaklığı (piksel).")]
    [Range(5, 50)] public int highwayShoreBuffer = 30;
    public Color highwayFill = new Color(0.38f, 0.36f, 0.32f);
    public Color highwayOutline = new Color(0.25f, 0.23f, 0.20f);
    [Tooltip("Yol noise (Perlin) frekansi. Yuksek = daha kucuk yamalar (yolun camur gorunmemesi icin 0.3+ onerilir).")]
    [Range(0.01f, 0.8f)] public float highwayNoiseScale = 0.35f;
    [Tooltip("Yol noise (Perlin) siddeti. Dusuk = yumusak doku, yuksek = lekeli/camurlu.")]
    [Range(0f, 0.5f)] public float highwayNoiseStrength = 0.05f;
    [Tooltip("Yol grain (rastgele) siddeti. Yuksek = kumlu doku (fazlasi gorsel kirlilik).")]
    [Range(0f, 0.2f)] public float highwayGrainStrength = 0.02f;

    // -------------------------------------------------------------------------
    // DALLANMA
    // -------------------------------------------------------------------------

    [Header("Dallanma — Kapsama Tabanlı")]
    [Tooltip("Herhangi bir yoldan bu kadar piksel uzaktaki kara alanları dallanma hedefi olur.")]
    [Range(15, 80)] public int branchMinCoverageDistance = 35;
    [Tooltip("Maksimum dal sayısı (güvenlik limiti).")]
    [Range(5, 40)] public int branchMaxCount = 25;
    [Tooltip("Dal kıvrım oranı. 0 = dümdüz, 0.2 = doğal kıvrım.")]
    [Range(0f, 0.3f)] public float branchCurviness = 0.08f;
    [Tooltip("Dal başlangıç kalınlığı (highway'e yakın uç).")]
    [Range(1, 6)] public int branchStartThickness = 3;
    [Tooltip("Dal bitiş kalınlığı (en uç). 2 alti dal ucunu cime eritir — 2+ onerilir.")]
    [Range(1, 4)] public int branchEndThickness = 2;
    [Tooltip("Dal outline genişliği.")]
    [Range(0, 2)] public int branchOutlineWidth = 1;
    [Tooltip("Preferred shore distance for branch roads. Uses soft penalty — branches prefer inland but CAN reach coastal areas.")]
    [Range(2, 30)] public int branchShoreBuffer = 12;
    [Tooltip("Highway -> biyom rengine gecis sertligi. 1 = lineer (yumusak), yuksek = dar gecis bandi (sert).")]
    [Range(1f, 20f)] public float branchColorTransitionSharpness = 4f;

    // -------------------------------------------------------------------------
    // DALLANMA UNIFIYE RENGI (tum biyomlarda ayni yol govdesi rengi)
    // -------------------------------------------------------------------------

    [Header("Dallanma Unifiye Rengi — Toprak Yol")]
    [Tooltip("Dallanmanin govde (fill) rengi — tum biyomlarda ayni. Biyom etkisi shoulder bandinda yapilir.")]
    public Color branchFill = new Color(0.42f, 0.36f, 0.26f);
    [Tooltip("Dallanmanin kenar (outline) rengi — tum biyomlarda ayni.")]
    public Color branchOutline = new Color(0.22f, 0.18f, 0.12f);

    // -------------------------------------------------------------------------
    // SHOULDER (OMUZ) — yoldan araziye yumusak gecis bandi, biyoma gore renklenir
    // -------------------------------------------------------------------------

    [Header("Shoulder (Omuz) Bandi — Biyom Tinti")]
    [Tooltip("Yolun dis kenari ile arazi arasindaki gecis bandi genisligi (piksel).")]
    [Range(0, 3)] public int shoulderWidth = 1;
    [Tooltip("Omuzun arazi uzerine alpha blend orani (0 = gorunmez, 1 = tam opak).")]
    [Range(0f, 1f)] public float shoulderBlend = 0.45f;
    [Tooltip("Highway (anayol) omuz rengi.")]
    public Color highwayShoulder = new Color(0.30f, 0.28f, 0.24f);
    [Tooltip("Forest (Biome 1) — toprak yol omuz tonu.")]
    public Color agriculturalShoulder = new Color(0.35f, 0.30f, 0.20f);
    [Tooltip("Desert (Biome 2) — sehir yolu omuz tonu.")]
    public Color citiesShoulder = new Color(0.40f, 0.36f, 0.30f);
    [Tooltip("Mountains (Biome 3) — sanayi yolu omuz tonu.")]
    public Color industrialShoulder = new Color(0.26f, 0.22f, 0.18f);
    [Tooltip("Plains (Biome 4) — kentsel yol omuz tonu.")]
    public Color urbanShoulder = new Color(0.40f, 0.38f, 0.32f);

    [Header("Faz 2 — Yol Detaylari")]
    [Tooltip("Dal uc noktalarinda (kavsaklarda) highway renginde kucuk plato bas.")]
    public bool enableJunctionPlateaus = true;
    [Tooltip("Kavsak platosunun yaricap (piksel).")]
    [Range(1, 4)] public int junctionPlateauRadius = 2;

    [Header("Faz 3 — Orta Serit")]
    [Tooltip("Highway merkezine kesikli sari serit ciz.")]
    public bool enableCenterStripes = true;
    [Tooltip("Dal yollarinda da serit cizilsin mi.")]
    public bool enableBranchStripes = false;
    public Color centerStripeColor = new Color(0.82f, 0.70f, 0.22f);
    [Tooltip("Dash uzunlugu (tangent yonunde piksel). Path'i takip etmez, hesaplanmis yolda uzar.")]
    [Range(1, 6)] public int stripeDashLength = 2;
    [Tooltip("Dash'ler arasi path-pikseli bosluk.")]
    [Range(1, 8)] public int stripeGapLength = 4;
    [Tooltip("Bu kalinligin altindaki path pikselinde serit cizme (taper uclari).")]
    [Range(2, 6)] public int stripeMinThickness = 3;

    // -------------------------------------------------------------------------
    // GENEL
    // -------------------------------------------------------------------------

    [Header("General")]
    [Range(0, 10)] public int shoreBuffer = 2;
    public bool scaleWithMapSize = true;

    public static event Action OnRoadsGenerated;

    // -------------------------------------------------------------------------
    // INTERNAL STATE
    // -------------------------------------------------------------------------

    private int[,] roadTypeMap;
    private int[,] roadDistanceField;
    private int[,] visualRoadDistanceField;
    private bool[,] branchFillMask; //T-kavsak acikligi: branch fill pikselleri; highway outline bu piksellerde cizilmez

    private float[,] roadThicknessMap;
    private Color[,] roadFillColorMap;
    private Color[,] roadOutlineColorMap;
    private int[,] roadOutlineWidthMap;

    private List<Vector2Int> allRoadTiles = new List<Vector2Int>();
    private List<Vector2Int> highwayTiles = new List<Vector2Int>();

    private List<List<Vector2Int>> highwaySegments = new List<List<Vector2Int>>();
    private List<List<Vector2Int>> branchPaths = new List<List<Vector2Int>>();

    private List<RegionCenter> regionCenters = new List<RegionCenter>();

    private Texture2D _tex;
    private int _w, _h;
    private bool _generated = false;

    private int[,] shoreDistCache;
    private static readonly int[] dx4 = { 1, -1, 0, 0 };
    private static readonly int[] dy4 = { 0, 0, 1, -1 };

    public struct RegionCenter
    {
        public Vector2Int position;
        public int biome;
        public int tileCount;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // =========================================================================
    // ANA GİRİŞ NOKTASI
    // =========================================================================

    public void GenerateRoads(MapGenerator map, Texture2D mapTexture)
    {
        if (map == null || mapTexture == null)
        {
            Debug.LogError("RoadGenerator: MapGenerator or texture is null.");
            return;
        }

        _tex = mapTexture;
        _w = map.width;
        _h = map.height;

        roadTypeMap = new int[_w, _h];
        roadThicknessMap = new float[_w, _h];
        roadFillColorMap = new Color[_w, _h];
        roadOutlineColorMap = new Color[_w, _h];
        roadOutlineWidthMap = new int[_w, _h];
        allRoadTiles.Clear();
        highwayTiles.Clear();
        highwaySegments.Clear();
        branchPaths.Clear();
        regionCenters.Clear();

        float areaScale = scaleWithMapSize ? Mathf.Sqrt((_w * _h) / (256f * 256f)) : 1f;

        BuildShoreDistanceCache(map);
        FindRegionCenters(map);
        GenerateHighways(map, areaScale);
        GenerateBranches(map, areaScale);

        // Iki ayri centerline arasindaki 2-5 tile'lik gap'lere ara centerline enjekte et.
        // Paint ve distance-field pass'leri bunu otomatik yansitacak (allRoadTiles uzerinden).
        InjectBridgeCenterlines(map);

        // visualRoadDistanceField'in bridge sonrasi taze olmasi icin once build — Fill buna dayaniyor
        BuildRoadDistanceField();

        PaintAllRoads();
        PaintJunctionPlateaus();
        PaintCenterStripes();
        // Kavis acisi / noise mikro-gap'leri icin guvenlik agi
        FillInteriorSandwichPixels();
        _tex.Apply();

        _generated = true;

        Debug.Log($"RoadGenerator: total={allRoadTiles.Count}px, highway={highwayTiles.Count}px, segments={highwaySegments.Count}, branches={branchPaths.Count}, regionCenters={regionCenters.Count}");

        OnRoadsGenerated?.Invoke();
    }

    // =========================================================================
    // CATMULL-ROM SPLINE
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
                        if (!visited.Contains(bkey)) { visited.Add(bkey); pixels.Add(bp); }
                    }
                }
                else
                {
                    long key = (long)px << 32 | (uint)py;
                    if (!visited.Contains(key)) { visited.Add(key); pixels.Add(current); }
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

    List<Vector2> BuildControlPoints(Vector2 start, Vector2 end, int numMiddle, float curviness)
    {
        List<Vector2> points = new List<Vector2>();
        points.Add(start);

        Vector2 dir = end - start;
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
            float drift = (Mathf.PerlinNoise(t * 2.5f + noiseSeed, noiseSeed * 0.3f) - 0.5f) * 2f * maxDrift;
            float wobble = (Mathf.PerlinNoise(noiseSeed + 500f, t * 5f) - 0.5f) * maxDrift * 0.3f;
            points.Add(baseline + perp * (drift + wobble));
        }
        points.Add(end);
        return points;
    }

    List<Vector2> BuildValidatedControlPoints(MapGenerator map, Vector2 start, Vector2 end,
                                               int numMiddle, float curviness)
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
                if (IsValidRoadPoint(map, cx, cy)) { raw[i] = candidate; break; }
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

    // =========================================================================
    // KIYIDAN UZAKLIK
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
    // BÖLGE MERKEZLERİ
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

                if (cluster.Count < 500) continue;

                long sumX = 0, sumY = 0;
                foreach (var p in cluster) { sumX += p.x; sumY += p.y; }
                int cx = (int)(sumX / cluster.Count);
                int cy = (int)(sumY / cluster.Count);
                Vector2Int center = SnapToNearestInList(new Vector2Int(cx, cy), cluster);

                regionCenters.Add(new RegionCenter { position = center, biome = biome, tileCount = cluster.Count });
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
    // HIGHWAY
    // =========================================================================

    void GenerateHighways(MapGenerator map, float areaScale)
    {
        int[,] landComponent = new int[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                landComponent[x, y] = -1;

        int componentCount = 0;
        List<int> componentSizes = new List<int>();

        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            {
                if (!map.IsLand(x, y) || landComponent[x, y] >= 0) continue;

                int compId = componentCount++;
                int size = 0;
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, y));
                landComponent[x, y] = compId;

                while (queue.Count > 0)
                {
                    var pos = queue.Dequeue();
                    size++;
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                        if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                        if (!map.IsLand(nx, ny) || landComponent[nx, ny] >= 0) continue;
                        landComponent[nx, ny] = compId;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
                componentSizes.Add(size);
            }

        int mainComponent = 0;
        for (int i = 1; i < componentSizes.Count; i++)
            if (componentSizes[i] > componentSizes[mainComponent]) mainComponent = i;

        List<Vector2Int> mainLandPoints = new List<Vector2Int>();
        for (int x = 0; x < _w; x += 3)
            for (int y = 0; y < _h; y += 3)
                if (map.IsLand(x, y) && landComponent[x, y] == mainComponent
                    && shoreDistCache[x, y] >= Mathf.Max(1, shoreBuffer))
                    mainLandPoints.Add(new Vector2Int(x, y));

        if (mainLandPoints.Count < 2)
        {
            Debug.LogWarning("RoadGenerator: Yeterli kara noktası bulunamadı!");
            return;
        }

        Vector2Int startPt = Vector2Int.zero, endPt = Vector2Int.zero;
        float maxEuclidean = 0f;
        int sampleCount = Mathf.Min(mainLandPoints.Count, 200);
        for (int i = 0; i < sampleCount; i++)
        {
            Vector2Int a = mainLandPoints[i * mainLandPoints.Count / sampleCount];
            for (int j = i + 1; j < sampleCount; j++)
            {
                Vector2Int b = mainLandPoints[j * mainLandPoints.Count / sampleCount];
                float d = Vector2Int.Distance(a, b);
                if (d > maxEuclidean) { maxEuclidean = d; startPt = a; endPt = b; }
            }
        }

        if (maxEuclidean < 30f)
        {
            Debug.LogWarning("RoadGenerator: Kuşbakışı mesafe çok kısa!");
            return;
        }

        int waypointCount = Mathf.Max(5, Mathf.RoundToInt(maxEuclidean / 20f));
        List<Vector2Int> waypoints = new List<Vector2Int>();
        waypoints.Add(startPt);

        for (int i = 1; i < waypointCount; i++)
        {
            float t = (float)i / waypointCount;
            int wx = Mathf.RoundToInt(Mathf.Lerp(startPt.x, endPt.x, t));
            int wy = Mathf.RoundToInt(Mathf.Lerp(startPt.y, endPt.y, t));
            wx = Mathf.Clamp(wx, 0, _w - 1);
            wy = Mathf.Clamp(wy, 0, _h - 1);

            Vector2Int wp = new Vector2Int(wx, wy);

            if (!map.IsLand(wx, wy) || landComponent[wx, wy] != mainComponent)
                wp = SnapToNearestLand(map, landComponent, mainComponent, wp);

            if (wp.x >= 0) waypoints.Add(wp);
        }
        waypoints.Add(endPt);

        List<Vector2Int> fullPath = new List<Vector2Int>();
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            List<Vector2Int> segment = BFSPathOnLand(map, landComponent, mainComponent, waypoints[i], waypoints[i + 1]);
            if (segment.Count > 0)
            {
                int skip = (i > 0 && fullPath.Count > 0) ? 1 : 0;
                for (int s = skip; s < segment.Count; s++)
                    fullPath.Add(segment[s]);
            }
        }

        if (fullPath.Count < 10)
        {
            Debug.LogWarning("RoadGenerator: Highway yolu oluşturulamadı!");
            return;
        }

        List<Vector2Int> bfsPath = fullPath;
        Debug.Log($"RoadGenerator: Highway start={startPt}, end={endPt}, euclidean={maxEuclidean:F0}, path={bfsPath.Count}px, waypoints={waypoints.Count}");

        List<Vector2Int> smoothed = SmoothPath(bfsPath, highwayCurviness);

        foreach (var pixel in smoothed)
            RegisterRoadTile(pixel, 1, highwayThickness, highwayOutlineWidth, highwayFill, highwayOutline);

        highwayTiles.AddRange(smoothed);
        highwaySegments.Add(new List<Vector2Int>(smoothed));
    }

    /// <summary>
    /// Highway pathfinding — weighted Dijkstra that penalizes coastal tiles.
    /// Hard cutoff at 40% of highwayShoreBuffer prevents any highway from
    /// getting very close to shore.
    /// </summary>
    List<Vector2Int> BFSPathOnLand(MapGenerator map, int[,] landComponent, int compId, Vector2Int from, Vector2Int to)
    {
        int[,] cost = new int[_w, _h];
        Vector2Int[,] parent = new Vector2Int[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            { cost[x, y] = int.MaxValue; parent[x, y] = new Vector2Int(-1, -1); }

        int maxPenalty = highwayShoreBuffer * 4;
        int hardCutoff = Mathf.RoundToInt(highwayShoreBuffer * 0.4f);

        int maxBuckets = (_w + _h) * maxPenalty;
        maxBuckets = Mathf.Min(maxBuckets, 500000);
        Queue<Vector2Int>[] buckets = new Queue<Vector2Int>[maxBuckets];

        cost[from.x, from.y] = 0;
        if (buckets[0] == null) buckets[0] = new Queue<Vector2Int>();
        buckets[0].Enqueue(from);

        bool found = false;
        for (int bucket = 0; bucket < maxBuckets && !found; bucket++)
        {
            if (buckets[bucket] == null || buckets[bucket].Count == 0) continue;

            while (buckets[bucket].Count > 0)
            {
                var pos = buckets[bucket].Dequeue();
                if (pos == to) { found = true; break; }

                int curCost = cost[pos.x, pos.y];
                if (curCost > bucket) continue;

                for (int i = 0; i < 4; i++)
                {
                    int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                    if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                    if (!map.IsLand(nx, ny) || landComponent[nx, ny] != compId) continue;

                    int shore = shoreDistCache[nx, ny];

                    if (shore >= 0 && shore < hardCutoff) continue;

                    int penalty = shore >= highwayShoreBuffer ? 1 : Mathf.Max(1, maxPenalty - shore * 3);
                    int newCost = curCost + penalty;

                    if (newCost < cost[nx, ny] && newCost < maxBuckets)
                    {
                        cost[nx, ny] = newCost;
                        parent[nx, ny] = pos;
                        if (buckets[newCost] == null) buckets[newCost] = new Queue<Vector2Int>();
                        buckets[newCost].Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }

        List<Vector2Int> path = new List<Vector2Int>();
        if (!found) return path;

        Vector2Int cur = to;
        while (cur != from)
        {
            path.Add(cur);
            cur = parent[cur.x, cur.y];
            if (cur.x < 0) break;
        }
        path.Add(from);
        path.Reverse();
        return path;
    }

    List<Vector2Int> SmoothPath(List<Vector2Int> rawPath, float curviness)
    {
        if (rawPath.Count < 20) return rawPath;

        int smoothStep = Mathf.Max(10, rawPath.Count / 20);
        List<Vector2> waypoints = new List<Vector2>();
        waypoints.Add((Vector2)rawPath[0]);
        for (int i = smoothStep; i < rawPath.Count - smoothStep; i += smoothStep)
        {
            Vector2Int rawPt = rawPath[i];
            Vector2 pushed = PushInland(rawPt, highwayShoreBuffer);
            waypoints.Add(pushed);
        }
        waypoints.Add((Vector2)rawPath[rawPath.Count - 1]);

        // Clamp all waypoints to stay inland
        MapGenerator hwMap = FindFirstObjectByType<MapGenerator>();
        for (int i = 0; i < waypoints.Count; i++)
        {
            int wx = Mathf.Clamp(Mathf.RoundToInt(waypoints[i].x), 0, _w - 1);
            int wy = Mathf.Clamp(Mathf.RoundToInt(waypoints[i].y), 0, _h - 1);
            int shore = shoreDistCache[wx, wy];
            if (shore >= 0 && shore < highwayShoreBuffer)
                waypoints[i] = PushInland(new Vector2Int(wx, wy), highwayShoreBuffer);
        }

        List<Vector2Int> smoothed = new List<Vector2Int>();
        HashSet<long> visited = new HashSet<long>();
        List<Vector2Int> splinePixels = SplineToPixels(waypoints, 10);
        foreach (var p in splinePixels)
        {
            long key = (long)p.x << 32 | (uint)p.y;
            if (!visited.Contains(key)) { visited.Add(key); smoothed.Add(p); }
        }

        return smoothed;
    }

    Vector2 PushInland(Vector2Int pt, int targetDist)
    {
        if (pt.x < 0 || pt.x >= _w || pt.y < 0 || pt.y >= _h) return (Vector2)pt;
        if (shoreDistCache[pt.x, pt.y] >= targetDist) return (Vector2)pt;

        int cx = pt.x, cy = pt.y;
        for (int step = 0; step < targetDist * 2; step++)
        {
            if (shoreDistCache[cx, cy] >= targetDist) break;

            int bestX = cx, bestY = cy;
            int bestShore = shoreDistCache[cx, cy];

            for (int dx = -5; dx <= 5; dx++)
                for (int dy = -5; dy <= 5; dy++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                    if (shoreDistCache[nx, ny] > bestShore)
                    {
                        bestShore = shoreDistCache[nx, ny];
                        bestX = nx; bestY = ny;
                    }
                }

            if (bestX == cx && bestY == cy) break;
            cx = bestX; cy = bestY;
        }

        return new Vector2(cx, cy);
    }

    Vector2Int SnapToNearestLand(MapGenerator map, int[,] landComponent, int compId, Vector2Int from)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        bool[,] visited = new bool[_w, _h];
        queue.Enqueue(from);
        visited[from.x, from.y] = true;

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            if (map.IsLand(pos.x, pos.y) && landComponent[pos.x, pos.y] == compId)
                return pos;

            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (visited[nx, ny]) continue;
                visited[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return new Vector2Int(-1, -1);
    }

    Vector2Int BFSFarthestLandPoint(MapGenerator map, int[,] landComponent, int compId, Vector2Int from, int minShore)
    {
        int[,] dist = new int[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                dist[x, y] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        dist[from.x, from.y] = 0;
        queue.Enqueue(from);

        Vector2Int farthest = from;
        int maxScore = 0;

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d = dist[pos.x, pos.y];
            int shore = shoreDistCache[pos.x, pos.y];

            int score = d + shore / 2;
            if (score > maxScore && shore >= minShore)
            {
                maxScore = score;
                farthest = pos;
            }

            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (dist[nx, ny] >= 0) continue;
                if (!map.IsLand(nx, ny) || landComponent[nx, ny] != compId) continue;
                dist[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return farthest;
    }

    // =========================================================================
    // DALLANMA — BFS TABANLI KAPSAMA SİSTEMİ
    // =========================================================================

    // Branchin varolan yol bandina ne kadar yakin gecmesine izin verildigi (tile)
    // Bu degerden daha yakin tile'lara BFS step atilamaz — sadece from/to corridor istisnasi
    private const int BRANCH_CLEARANCE = 7;
    // Branch painted bandinin highway band'iyla cakistigi tile'lar boyamada atlanir
    // (corridor exception bolgesinde branch centerline highway'in icinde gecebilir)
    private const int BRANCH_PAINT_SKIP_DIST = 5;

    // Bridge injection: iki ayri yol centerline'i arasindaki gorsel gap'leri kapatmak icin
    // ara centerline tile'lari enjekte edilir. Cheb mesafe [MIN,MAX] araligindaki cift
    // centerline'lar hedef. Altindaki mesafede diskler zaten ortusuyor, ustunde ise iki
    // ayri yol saymak gerek.
    private const int BRIDGE_MIN_DIST = 2;
    private const int BRIDGE_MAX_DIST = 5;
    // Iki centerline zaten komsu-zincir ile bagliysa bridge gereksiz — bu kadar cap'li
    // BFS ile bagli mi diye bakariz.
    private const int BRIDGE_CONNECTIVITY_CHECK_RADIUS = 7;

    void GenerateBranches(MapGenerator map, float areaScale)
    {
        if (highwaySegments.Count == 0) return;

        List<Vector2Int> allRoadNodes = new List<Vector2Int>();
        foreach (var seg in highwaySegments)
            allRoadNodes.AddRange(seg);

        if (allRoadNodes.Count < 20) return;

        int[,] roadDist = new int[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                roadDist[x, y] = int.MaxValue;

        Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>();
        foreach (var hp in allRoadNodes)
        {
            roadDist[hp.x, hp.y] = 0;
            bfsQueue.Enqueue(hp);
        }
        while (bfsQueue.Count > 0)
        {
            var pos = bfsQueue.Dequeue();
            int d = roadDist[pos.x, pos.y];
            if (d >= 600) continue;
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (!map.IsLand(nx, ny)) continue;
                if (roadDist[nx, ny] <= d + 1) continue;
                roadDist[nx, ny] = d + 1;
                bfsQueue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        int placed = 0;
        for (int iter = 0; iter < branchMaxCount; iter++)
        {
            int maxDist = 0;
            Vector2Int target = Vector2Int.zero;
            bool found = false;

            //önce normal eşiklerle dene, bulamazsa kademeli olarak düşür
            int currentShoreBuffer = branchShoreBuffer;
            int currentMinCoverage = branchMinCoverageDistance;

            for (int relaxPass = 0; relaxPass < 3 && !found; relaxPass++)
            {
                for (int x = 0; x < _w; x += 3)
                    for (int y = 0; y < _h; y += 3)
                    {
                        if (!map.IsLand(x, y)) continue;
                        int d = roadDist[x, y];
                        if (d == int.MaxValue) continue;
                        int shore = shoreDistCache[x, y];
                        if (shore >= 0 && shore < currentShoreBuffer) continue;
                        if (d > maxDist)
                        {
                            maxDist = d;
                            target = new Vector2Int(x, y);
                            found = true;
                        }
                    }

                if (!found || maxDist < currentMinCoverage)
                {
                    //eşikleri düşür ve tekrar dene
                    found = false;
                    maxDist = 0;
                    currentShoreBuffer = Mathf.Max(2, currentShoreBuffer / 2);
                    currentMinCoverage = Mathf.Max(10, currentMinCoverage / 2);
                }
            }

            if (!found || maxDist < 10) break;

            float bestHwDist = float.MaxValue;
            int bestHwIdx = 0;
            int searchStep = Mathf.Max(1, allRoadNodes.Count / 200);
            for (int i = 0; i < allRoadNodes.Count; i += searchStep)
            {
                float dd = Vector2Int.Distance(allRoadNodes[i], target);
                if (dd < bestHwDist) { bestHwDist = dd; bestHwIdx = i; }
            }
            int sFrom = Mathf.Max(0, bestHwIdx - searchStep);
            int sTo = Mathf.Min(allRoadNodes.Count - 1, bestHwIdx + searchStep);
            for (int i = sFrom; i <= sTo; i++)
            {
                float dd = Vector2Int.Distance(allRoadNodes[i], target);
                if (dd < bestHwDist) { bestHwDist = dd; bestHwIdx = i; }
            }
            Vector2Int hwStart = allRoadNodes[bestHwIdx];

            float eucDist = Vector2Int.Distance(hwStart, target);
            int wpCount = Mathf.Max(3, Mathf.RoundToInt(eucDist / 25f));
            List<Vector2Int> branchWaypoints = new List<Vector2Int>();
            branchWaypoints.Add(hwStart);
            for (int w = 1; w < wpCount; w++)
            {
                float wt = (float)w / wpCount;
                int wx = Mathf.RoundToInt(Mathf.Lerp(hwStart.x, target.x, wt));
                int wy = Mathf.RoundToInt(Mathf.Lerp(hwStart.y, target.y, wt));
                wx = Mathf.Clamp(wx, 0, _w - 1);
                wy = Mathf.Clamp(wy, 0, _h - 1);
                if (!map.IsLand(wx, wy))
                {
                    Vector2Int snapped = SnapToNearestLandSimple(map, wx, wy, 40);
                    if (snapped.x < 0) continue;
                    wx = snapped.x; wy = snapped.y;
                }
                branchWaypoints.Add(new Vector2Int(wx, wy));
            }
            branchWaypoints.Add(target);

            List<Vector2Int> rawPath = new List<Vector2Int>();
            bool pathBroken = false;
            for (int w = 0; w < branchWaypoints.Count - 1; w++)
            {
                List<Vector2Int> seg = BFSPathOnLandSimple(map, branchWaypoints[w], branchWaypoints[w + 1], roadDist, BRANCH_CLEARANCE);
                if (seg.Count == 0) { pathBroken = true; break; }
                int skip = (w > 0 && rawPath.Count > 0) ? 1 : 0;
                for (int s = skip; s < seg.Count; s++) rawPath.Add(seg[s]);
            }

            //waypoint'li yol başarısızsa, direkt BFS dene (tek segment)
            if (pathBroken || rawPath.Count < 15)
            {
                rawPath = BFSPathOnLandSimple(map, hwStart, target, roadDist, BRANCH_CLEARANCE);
                pathBroken = rawPath.Count < 10;
            }

            if (pathBroken || rawPath.Count < 10)
            {
                roadDist[target.x, target.y] = 0;
                continue;
            }

            List<Vector2Int> smoothed = SmoothBranchPath(map, rawPath);
            if (smoothed.Count < 10) smoothed = rawPath;

            // Highway'i gecip diger tarafa cikmayi onle.
            // Branch basinda hwStart'in corridor'u (roadDist<SKIP_DIST) dogal olarak var.
            // Corridor'dan cikip (roadDist>=SKIP_DIST) sonra tekrar highway yakinina donulurse
            // baska bir highway segmentini geciyor demektir — path'i o noktada kes.
            int truncateAt = smoothed.Count;
            bool escapedStartCorridor = false;
            for (int p = 0; p < smoothed.Count; p++)
            {
                int d = roadDist[smoothed[p].x, smoothed[p].y];
                if (d >= BRANCH_PAINT_SKIP_DIST)
                {
                    escapedStartCorridor = true;
                }
                else if (escapedStartCorridor)
                {
                    truncateAt = p;
                    break;
                }
            }
            if (truncateAt < smoothed.Count)
            {
                if (truncateAt < 10)
                {
                    roadDist[target.x, target.y] = 0;
                    continue;
                }
                smoothed = smoothed.GetRange(0, truncateAt);
                //corridor kenarinda kesildi — fill band'i diger yolla kesin ortussun
                //diye centerline'a pad et (gorsel gap kapanir)
                PadPathToNearestCenterline(map, smoothed, roadDist);
            }

            float hwDist = float.MaxValue;
            foreach (var seg in highwaySegments)
            {
                int hStep = Mathf.Max(1, seg.Count / 40);
                for (int si = 0; si < seg.Count; si += hStep)
                {
                    float dd = Vector2Int.Distance(seg[si], smoothed[0]);
                    if (dd < hwDist) hwDist = dd;
                }
            }
            float mapDiag = Mathf.Sqrt(_w * _w + _h * _h) * 0.4f;
            float tStart = Mathf.Clamp01(hwDist / mapDiag) * 0.6f;
            float tEnd = Mathf.Clamp01(tStart + (1f - tStart));

            // Store the branch path for the traffic system
            branchPaths.Add(new List<Vector2Int>(smoothed));

            RegisterBranchPixels(map, smoothed, tStart, tEnd, roadDist, BRANCH_PAINT_SKIP_DIST);
            for (int ri = 0; ri < smoothed.Count; ri += 5)
                allRoadNodes.Add(smoothed[ri]);
            placed++;

            foreach (var bp in smoothed)
            {
                if (roadDist[bp.x, bp.y] == 0) continue;
                roadDist[bp.x, bp.y] = 0;
                bfsQueue.Enqueue(bp);
            }
            //branch kapsama yarıçapı — çok yüksek olursa az branch üretilir
            int branchCoverageRadius = 150;
            while (bfsQueue.Count > 0)
            {
                var pos = bfsQueue.Dequeue();
                int d = roadDist[pos.x, pos.y];
                if (d >= branchCoverageRadius) continue;
                for (int i = 0; i < 4; i++)
                {
                    int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                    if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                    if (!map.IsLand(nx, ny)) continue;
                    if (roadDist[nx, ny] <= d + 1) continue;
                    roadDist[nx, ny] = d + 1;
                    bfsQueue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        if (placed == 0)
            Debug.LogWarning($"RoadGenerator: HİÇ BRANCH YOL ÜRETİLEMEDİ! branchMinCoverage={branchMinCoverageDistance}, shoreBuffer={branchShoreBuffer}, branchMaxCount={branchMaxCount}");
        else
            Debug.Log($"RoadGenerator: {placed} dal yerleştirildi (kapsama tabanlı, eşik={branchMinCoverageDistance}px).");
    }

    /// <summary>
    /// Branch BFS pathfinding — uses SOFT PENALTY for shore proximity.
    /// Branches prefer inland routes but CAN reach coastal areas.
    /// This prevents the old hard-cutoff from blocking all branch generation.
    /// </summary>
    List<Vector2Int> BFSPathOnLandSimple(MapGenerator map, Vector2Int from, Vector2Int to,
        int[,] roadDist = null, int clearance = 0)
    {
        Vector2Int[,] parent = new Vector2Int[_w, _h];
        bool[,] visited = new bool[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                parent[x, y] = new Vector2Int(-1, -1);

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        visited[from.x, from.y] = true;

        bool found = false;
        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            if (pos == to) { found = true; break; }

            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (visited[nx, ny]) continue;
                if (!map.IsLand(nx, ny)) continue;

                // Yol bandina girmeyi engelle — sadece from/to corridor'da izin ver
                if (roadDist != null && clearance > 0 && roadDist[nx, ny] < clearance)
                {
                    int chebFrom = Mathf.Max(Mathf.Abs(nx - from.x), Mathf.Abs(ny - from.y));
                    int chebTo   = Mathf.Max(Mathf.Abs(nx - to.x),   Mathf.Abs(ny - to.y));
                    if (chebFrom > clearance && chebTo > clearance) continue;
                }

                visited[nx, ny] = true;
                parent[nx, ny] = pos;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        List<Vector2Int> path = new List<Vector2Int>();
        if (!found) return path;

        Vector2Int cur = to;
        while (cur != from)
        {
            path.Add(cur);
            cur = parent[cur.x, cur.y];
            if (cur.x < 0) return new List<Vector2Int>();
        }
        path.Add(from);
        path.Reverse();
        return path;
    }

    List<Vector2Int> SmoothBranchPath(MapGenerator map, List<Vector2Int> rawPath)
    {
        if (rawPath.Count < 30) return rawPath;

        int step1 = Mathf.Clamp(rawPath.Count / 8, 20, 60);
        List<Vector2> wp1 = new List<Vector2>();
        wp1.Add((Vector2)rawPath[0]);
        for (int i = step1; i < rawPath.Count - step1 / 2; i += step1)
        {
            int avgR = Mathf.Min(step1 / 3, 12);
            float ax = 0, ay = 0; int cnt = 0;
            for (int j = i - avgR; j <= i + avgR; j++)
            {
                if (j < 0 || j >= rawPath.Count) continue;
                ax += rawPath[j].x; ay += rawPath[j].y; cnt++;
            }
            wp1.Add(new Vector2(ax / cnt, ay / cnt));
        }
        wp1.Add((Vector2)rawPath[rawPath.Count - 1]);

        if (wp1.Count < 3) return rawPath;

        // Clamp first-pass waypoints away from shore
        for (int i = 0; i < wp1.Count; i++)
        {
            int wx = Mathf.Clamp(Mathf.RoundToInt(wp1[i].x), 0, _w - 1);
            int wy = Mathf.Clamp(Mathf.RoundToInt(wp1[i].y), 0, _h - 1);
            int shore = shoreDistCache[wx, wy];
            if (shore >= 0 && shore < 5)
                wp1[i] = PushInland(new Vector2Int(wx, wy), 5);
        }

        List<Vector2Int> pass1 = SplineToPixels(wp1, 20);
        List<Vector2Int> pass1Valid = pass1;

        int step2 = Mathf.Clamp(pass1Valid.Count / 12, 10, 40);
        float noiseSeed = UnityEngine.Random.Range(0f, 9999f);
        Vector2 pathDir = ((Vector2)(pass1Valid[pass1Valid.Count - 1] - pass1Valid[0])).normalized;
        Vector2 pathPerp = new Vector2(-pathDir.y, pathDir.x);
        float pathLength = Vector2Int.Distance(pass1Valid[0], pass1Valid[pass1Valid.Count - 1]);
        float maxDrift = pathLength * branchCurviness;

        List<Vector2> wp2 = new List<Vector2>();
        wp2.Add((Vector2)pass1Valid[0]);
        for (int i = step2; i < pass1Valid.Count - step2 / 2; i += step2)
        {
            Vector2 pt = (Vector2)pass1Valid[i];
            float t = (float)i / pass1Valid.Count;
            float edgeFade = Mathf.Sin(t * Mathf.PI);
            float noise = (Mathf.PerlinNoise(t * 3f + noiseSeed, noiseSeed * 0.7f) - 0.5f) * 2f;
            pt += pathPerp * (noise * maxDrift * edgeFade);

            int px = Mathf.Clamp(Mathf.RoundToInt(pt.x), 0, _w - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(pt.y), 0, _h - 1);
            if (!map.IsLand(px, py)) pt = (Vector2)pass1Valid[i];

            wp2.Add(pt);
        }
        wp2.Add((Vector2)pass1Valid[pass1Valid.Count - 1]);

        if (wp2.Count < 3) return pass1Valid;

        // Clamp second-pass waypoints away from shore
        for (int i = 0; i < wp2.Count; i++)
        {
            int wx = Mathf.Clamp(Mathf.RoundToInt(wp2[i].x), 0, _w - 1);
            int wy = Mathf.Clamp(Mathf.RoundToInt(wp2[i].y), 0, _h - 1);
            int shore = shoreDistCache[wx, wy];
            if (shore >= 0 && shore < 5)
                wp2[i] = PushInland(new Vector2Int(wx, wy), 5);
        }

        List<Vector2Int> pass2 = SplineToPixels(wp2, 20);
        List<Vector2Int> result = pass2;

        if (result.Count < rawPath.Count * 0.3f) return pass1Valid;
        return result;
    }

    Vector2Int SnapToNearestLandSimple(MapGenerator map, int x, int y, int maxRadius)
    {
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int dy1 = r - Mathf.Abs(dx);
                int dy2 = -dy1;
                int nx, ny;

                nx = x + dx; ny = y + dy1;
                if (nx >= 0 && nx < _w && ny >= 0 && ny < _h && map.IsLand(nx, ny))
                    return new Vector2Int(nx, ny);

                if (dy2 != dy1)
                {
                    nx = x + dx; ny = y + dy2;
                    if (nx >= 0 && nx < _w && ny >= 0 && ny < _h && map.IsLand(nx, ny))
                        return new Vector2Int(nx, ny);
                }
            }
        }
        return new Vector2Int(-1, -1);
    }

    void RegisterBranchPixels(MapGenerator map, List<Vector2Int> pixels, float tStart, float tEnd,
        int[,] roadDist = null, int skipThreshold = 0)
    {
        bool isSubBranch = tStart > 0.15f;
        float thickStart = isSubBranch
            ? Mathf.Lerp(branchStartThickness, branchEndThickness, 0.5f)
            : branchStartThickness;

        //Skip mantigi SADECE baslangic corridor'u icin — hwStart'ta highway bandina
        //taşmamak icin gereklidir. Path corridor'dan cikip (d>=threshold) sonra
        //baska bir yolun corridor'una tekrar girerse (re-entry), o piksellerin
        //boyanmasi GEREKIYOR — aksi halde iki yol arasinda gorsel gap olusur.
        bool escapedStartCorridor = false;

        for (int p = 0; p < pixels.Count; p++)
        {
            int d = (roadDist != null) ? roadDist[pixels[p].x, pixels[p].y] : int.MaxValue;

            if (skipThreshold > 0 && roadDist != null)
            {
                if (d >= skipThreshold)
                    escapedStartCorridor = true;
                //yalnizca baslangic corridor'undayken atla
                if (!escapedStartCorridor && d < skipThreshold)
                    continue;
            }

            float localT = (float)p / Mathf.Max(1, pixels.Count - 1);

            float colorT = Mathf.Lerp(tStart, tEnd, localT);

            float thickness = Mathf.Lerp(thickStart, branchEndThickness, localT);
            int outW = localT < 0.6f ? branchOutlineWidth : Mathf.Max(0, branchOutlineWidth - 1);

            // Unifiye renk: tum dallar ayni toprak govdesini paylasir. highway junction'da
            // asfalttan topraga yumusak gecis icin ilk tile'larda short lerp (sharpness ile).
            float sharpenedT = Mathf.Clamp01((colorT - 0.5f) * branchColorTransitionSharpness + 0.5f);

            Color fill = Color.Lerp(highwayFill, branchFill, sharpenedT);
            Color outline = Color.Lerp(highwayOutline, branchOutline, sharpenedT);

            RegisterRoadTile(pixels[p], 2, thickness, outW, fill, outline);
        }
    }

    // =========================================================================
    // SHOULDER (OMUZ) RENGI — biyoma gore disariya gecis tonu
    // =========================================================================

    Color GetBiomeShoulderColor(int biome)
    {
        switch (biome)
        {
            case 1: return agriculturalShoulder;
            case 2: return citiesShoulder;
            case 3: return industrialShoulder;
            case 4: return urbanShoulder;
            default: return agriculturalShoulder;
        }
    }

    /// <summary>
    /// Yolun tipine ve uzerinde bulundugu biyoma gore shoulder rengi dondur.
    /// Highway icin sabit highwayShoulder, branch icin biyoma gore tonlanir.
    /// </summary>
    Color GetShoulderColorForTile(Vector2Int tile)
    {
        int type = roadTypeMap[tile.x, tile.y];
        if (type == 1) return highwayShoulder;
        if (mapGenerator == null) return highwayShoulder;
        int biome = mapGenerator.GetBiome(tile.x, tile.y);
        return GetBiomeShoulderColor(biome);
    }

    // =========================================================================
    // YOL PİKSEL KAYIT
    // =========================================================================

    void RegisterRoadTile(Vector2Int tile, int roadType, float thickness, int outlineWidth, Color fill, Color outline)
    {
        if (tile.x < 0 || tile.x >= _w || tile.y < 0 || tile.y >= _h) return;

        int existing = roadTypeMap[tile.x, tile.y];
        if (existing == 0)
        {
            allRoadTiles.Add(tile);
            roadTypeMap[tile.x, tile.y] = roadType;
            roadThicknessMap[tile.x, tile.y] = thickness;
            roadOutlineWidthMap[tile.x, tile.y] = outlineWidth;
            roadFillColorMap[tile.x, tile.y] = fill;
            roadOutlineColorMap[tile.x, tile.y] = outline;
        }
        else if (roadType < existing)
        {
            roadTypeMap[tile.x, tile.y] = roadType;
            roadThicknessMap[tile.x, tile.y] = thickness;
            roadOutlineWidthMap[tile.x, tile.y] = outlineWidth;
            roadFillColorMap[tile.x, tile.y] = fill;
            roadOutlineColorMap[tile.x, tile.y] = outline;
        }
        else if (roadType == existing && roadType == 2 && thickness > roadThicknessMap[tile.x, tile.y])
        {
            roadThicknessMap[tile.x, tile.y] = thickness;
            roadOutlineWidthMap[tile.x, tile.y] = outlineWidth;
            roadFillColorMap[tile.x, tile.y] = fill;
            roadOutlineColorMap[tile.x, tile.y] = outline;
        }
    }

    // =========================================================================
    // BRIDGE CENTERLINE INJECTION — GORSEL GAP KAPATMA
    // =========================================================================

    /// <summary>
    /// Iki ayri centerline cheb-mesafe [BRIDGE_MIN_DIST, BRIDGE_MAX_DIST] araliginda
    /// ve centerline-komsuluk zinciriyle bagli DEGIL ise, aralarina ara centerline
    /// tile'lari enjekte eder. Bu sayede fill pass daireleri ortusur, arka plan
    /// pikseli kalmaz. Silueti korumak icin Min(thickness) kullanilir.
    /// </summary>
    void InjectBridgeCenterlines(MapGenerator map)
    {
        if (map == null || allRoadTiles == null) return;
        if (roadTypeMap == null) return;

        int originalCount = allRoadTiles.Count;
        // Tile indeksi: _w buyukluklu coord kodlamasi (ai = x * _h + y kullanamayiz
        // cunku _h uyuk degisebilir). (long)x<<32 | (uint)y daha guvenli.
        HashSet<long> bridgedPairs = new HashSet<long>();

        for (int i = 0; i < originalCount; i++)
        {
            Vector2Int a = allRoadTiles[i];

            for (int ddx = -BRIDGE_MAX_DIST; ddx <= BRIDGE_MAX_DIST; ddx++)
            for (int ddy = -BRIDGE_MAX_DIST; ddy <= BRIDGE_MAX_DIST; ddy++)
            {
                int absX = ddx < 0 ? -ddx : ddx;
                int absY = ddy < 0 ? -ddy : ddy;
                int cheb = absX > absY ? absX : absY;
                if (cheb < BRIDGE_MIN_DIST || cheb > BRIDGE_MAX_DIST) continue;

                int bx = a.x + ddx;
                int by = a.y + ddy;
                if (bx < 0 || bx >= _w || by < 0 || by >= _h) continue;
                if (roadTypeMap[bx, by] == 0) continue;

                // cift tekilligi (a,b ve b,a ayni sayilir)
                long aKey = ((long)a.x << 32) | (uint)a.y;
                long bKey = ((long)bx << 32) | (uint)by;
                long pairKey = aKey < bKey ? (aKey * 73856093L) ^ bKey : (bKey * 73856093L) ^ aKey;
                if (bridgedPairs.Contains(pairKey)) continue;
                bridgedPairs.Add(pairKey);

                Vector2Int b = new Vector2Int(bx, by);

                // normal branch kavisi ise bridge gereksiz
                if (IsAlreadyConnected(a, b, BRIDGE_CONNECTIVITY_CHECK_RADIUS)) continue;

                // parametre kaynaklari — silueti korumak icin Min(thickness)
                float thickness = Mathf.Min(roadThicknessMap[a.x, a.y], roadThicknessMap[bx, by]);
                int outlineW = Mathf.Min(roadOutlineWidthMap[a.x, a.y], roadOutlineWidthMap[bx, by]);
                int type = Mathf.Min(roadTypeMap[a.x, a.y], roadTypeMap[bx, by]);
                Color fill = roadFillColorMap[a.x, a.y];
                Color outline = roadOutlineColorMap[a.x, a.y];

                // a → b arasi ara tile'lari (endpoint'ler disinda) linear interp
                int steps = cheb;
                for (int s = 1; s < steps; s++)
                {
                    float t = (float)s / steps;
                    int mx = Mathf.RoundToInt(Mathf.Lerp(a.x, bx, t));
                    int my = Mathf.RoundToInt(Mathf.Lerp(a.y, by, t));
                    if (mx < 0 || mx >= _w || my < 0 || my >= _h) continue;
                    if (!map.IsLand(mx, my)) continue;
                    if (roadTypeMap[mx, my] != 0) continue;

                    RegisterRoadTile(new Vector2Int(mx, my), type, thickness, outlineW, fill, outline);
                }
            }
        }
    }

    /// <summary>
    /// a ve b centerline tile'lari, centerline komsuluk zinciriyle (roadTypeMap != 0
    /// 8-komsu adimlar) maxRadius cheb capi icinde birbirine bagli mi?
    /// Bagli ise bridge enjeksiyonu gereksiz (bu zaten normal bir kavis/kavsak).
    /// </summary>
    bool IsAlreadyConnected(Vector2Int a, Vector2Int b, int maxRadius)
    {
        if (a == b) return true;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<long> visited = new HashSet<long>();
        queue.Enqueue(a);
        visited.Add(((long)a.x << 32) | (uint)a.y);

        // performans sinir — ufak capli BFS, pattern patlamasin
        int iterCap = (2 * maxRadius + 1) * (2 * maxRadius + 1) * 2;

        while (queue.Count > 0 && iterCap-- > 0)
        {
            var pos = queue.Dequeue();
            for (int ddx = -1; ddx <= 1; ddx++)
            for (int ddy = -1; ddy <= 1; ddy++)
            {
                if (ddx == 0 && ddy == 0) continue;
                int nx = pos.x + ddx;
                int ny = pos.y + ddy;
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;

                int absDX = nx - a.x; if (absDX < 0) absDX = -absDX;
                int absDY = ny - a.y; if (absDY < 0) absDY = -absDY;
                if (absDX > maxRadius || absDY > maxRadius) continue;
                if (roadTypeMap[nx, ny] == 0) continue;

                long key = ((long)nx << 32) | (uint)ny;
                if (visited.Contains(key)) continue;
                visited.Add(key);

                if (nx == b.x && ny == b.y) return true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
        return false;
    }

    // =========================================================================
    // BOYAMA
    // =========================================================================

    void PaintAllRoads()
    {
        PaintRoadsByType(2);
        PaintRoadsByType(1);
    }

    //Dal uc noktalarinda (highway ile birlesim) kucuk yuvarlak plato — sert T-birlesimi yerine
    //planlanmis kavsak hissi verir. Sadece zaten boyali yol piksellerinin uzerine yazar.
    void PaintJunctionPlateaus()
    {
        if (!enableJunctionPlateaus || branchPaths == null || visualRoadDistanceField == null) return;
        int plateauHalf = junctionPlateauRadius;
        int plateauHalfSq = plateauHalf * plateauHalf;
        foreach (var path in branchPaths)
        {
            if (path == null || path.Count == 0) continue;
            PaintPlateauAt(path[0], plateauHalf, plateauHalfSq);
            if (path.Count > 1)
            {
                Vector2Int last = path[path.Count - 1];
                if (last != path[0]) PaintPlateauAt(last, plateauHalf, plateauHalfSq);
            }
        }
    }

    void PaintPlateauAt(Vector2Int j, int plateauHalf, int plateauHalfSq)
    {
        if (j.x < 0 || j.x >= _w || j.y < 0 || j.y >= _h) return;
        Color fillColor = roadFillColorMap[j.x, j.y];
        for (int ddx = -plateauHalf; ddx <= plateauHalf; ddx++)
        for (int ddy = -plateauHalf; ddy <= plateauHalf; ddy++)
        {
            int distSq = ddx * ddx + ddy * ddy;
            if (distSq > plateauHalfSq) continue;
            int px = j.x + ddx, py = j.y + ddy;
            if (px < 0 || px >= _w || py < 0 || py >= _h) continue;
            //Sadece yol ustu — cim/shoulder tasmasi olmasin.
            if (visualRoadDistanceField[px, py] != 0) continue;
            Color outColor = ApplyRoadNoise(fillColor, px, py);
            _tex.SetPixel(px, py, outColor);
        }
    }

    //Highway + (opsiyonel) branch path pixellerine kesikli sari orta serit bas.
    void PaintCenterStripes()
    {
        if (!enableCenterStripes || visualRoadDistanceField == null) return;
        int period = stripeDashLength + stripeGapLength;
        if (period < 1) return;
        int plateauSkip = enableJunctionPlateaus ? junctionPlateauRadius + 1 : 0;

        if (highwaySegments != null)
            foreach (var seg in highwaySegments) PaintStripeOnPath(seg, period, plateauSkip);
        if (enableBranchStripes && branchPaths != null)
            foreach (var path in branchPaths) PaintStripeOnPath(path, period, plateauSkip);
    }

    void PaintStripeOnPath(List<Vector2Int> path, int period, int plateauSkip)
    {
        if (path == null || path.Count == 0) return;
        int start = Mathf.Min(plateauSkip, path.Count);
        int end = Mathf.Max(start, path.Count - plateauSkip);
        //Anchor'lar her 'period' path pikselinde bir; dash anchor'dan tangent yonunde uzanir — path staircase'ini
        //takip etmez, cunku staircase'i takip edince capraz yolda zigzag yumru cikiyordu.
        for (int i = start; i < end; i += period)
        {
            Vector2Int anchor = path[i];
            //Tangent smoothing: path[i-3] ile path[i+3] arasi — staircase artefaktini yumusatmak icin genis pencere.
            int lo = Mathf.Max(0, i - 3);
            int hi = Mathf.Min(path.Count - 1, i + 3);
            float dx = path[hi].x - path[lo].x;
            float dy = path[hi].y - path[lo].y;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f) { PaintStripePixel(anchor.x, anchor.y); continue; }
            float tx = dx / len, ty = dy / len;
            //Dash: anchor'dan tangent yonunde stripeDashLength piksel. Float->round ayni pixel'e denk gelirse atla.
            int prevPx = int.MinValue, prevPy = int.MinValue;
            for (int k = 0; k < stripeDashLength; k++)
            {
                int px = anchor.x + Mathf.RoundToInt(tx * k);
                int py = anchor.y + Mathf.RoundToInt(ty * k);
                if (px == prevPx && py == prevPy) continue;
                prevPx = px; prevPy = py;
                PaintStripePixel(px, py);
            }
        }
    }

    void PaintStripePixel(int px, int py)
    {
        if (px < 0 || px >= _w || py < 0 || py >= _h) return;
        //Taper uclari / ince yerlerde serit atla.
        if (roadThicknessMap[px, py] < stripeMinThickness) return;
        //Sadece yol ic pikselinde (cim/shoulder tasmasi yok).
        if (visualRoadDistanceField[px, py] != 0) return;
        _tex.SetPixel(px, py, ApplyRoadNoise(centerStripeColor, px, py));
    }

    void PaintRoadsByType(int targetType)
    {
        // Branch pass'i baslarken mask'i sifirla — highway outline pass'i bu mask'i okuyarak
        // T-kavsak noktalarinda (branch fill'in oldugu piksellerde) outline'i atlar ve
        // anayolun dis hattini "kesip" branch'in saçaklanmis goruntusunu ortaya cikarir.
        if (targetType == 2)
        {
            if (branchFillMask == null || branchFillMask.GetLength(0) != _w || branchFillMask.GetLength(1) != _h)
                branchFillMask = new bool[_w, _h];
            else
                System.Array.Clear(branchFillMask, 0, branchFillMask.Length);
        }

        // Shoulder (omuz) pass: outline'dan daha disariya, araziye yumusak biyom-tonlu bant.
        // Arazi / alt yol piksellerinin UZERINE alpha-blend ile yazilir — outline ve fill
        // sonradan ustune geldigi icin yol icini etkilemez.
        if (shoulderWidth > 0 && shoulderBlend > 0f)
        {
            foreach (var tile in allRoadTiles)
            {
                if (roadTypeMap[tile.x, tile.y] != targetType) continue;

                float thickness = roadThicknessMap[tile.x, tile.y];
                int outlineW = roadOutlineWidthMap[tile.x, tile.y];

                int totalHalf = Mathf.CeilToInt((thickness + outlineW * 2) / 2f);
                int shoulderHalf = totalHalf + shoulderWidth;
                int shoulderHalfSq = shoulderHalf * shoulderHalf;
                int totalHalfSq = totalHalf * totalHalf;

                Color shoulderColor = GetShoulderColorForTile(tile);

                for (int ddx = -shoulderHalf; ddx <= shoulderHalf; ddx++)
                for (int ddy = -shoulderHalf; ddy <= shoulderHalf; ddy++)
                {
                    int distSq = ddx * ddx + ddy * ddy;
                    if (distSq > shoulderHalfSq) continue;
                    if (distSq <= totalHalfSq) continue; //sadece dis halka

                    int px = tile.x + ddx, py = tile.y + ddy;
                    if (px < 0 || px >= _w || py < 0 || py >= _h) continue;

                    Color existing = _tex.GetPixel(px, py);
                    _tex.SetPixel(px, py, Color.Lerp(existing, shoulderColor, shoulderBlend));
                }
            }
        }

        foreach (var tile in allRoadTiles)
        {
            if (roadTypeMap[tile.x, tile.y] != targetType) continue;

            float thickness = roadThicknessMap[tile.x, tile.y];
            int outlineW = roadOutlineWidthMap[tile.x, tile.y];
            Color outlineColor = roadOutlineColorMap[tile.x, tile.y];

            if (outlineW <= 0) continue;

            int totalHalf = Mathf.CeilToInt((thickness + outlineW * 2) / 2f);
            int totalHalfSq = totalHalf * totalHalf;

            for (int ddx = -totalHalf; ddx <= totalHalf; ddx++)
                for (int ddy = -totalHalf; ddy <= totalHalf; ddy++)
                {
                    if (ddx * ddx + ddy * ddy > totalHalfSq) continue;
                    int px = tile.x + ddx, py = tile.y + ddy;
                    if (px < 0 || px >= _w || py < 0 || py >= _h) continue;

                    //T-kavsak acikligi: highway outline'i branch fill piksellerinde cizme —
                    //branch fill highway bandinin icine saçaklanarak gorsel kavsak olusur.
                    if (targetType == 1 && branchFillMask != null && branchFillMask[px, py]) continue;

                    _tex.SetPixel(px, py, outlineColor);
                }
        }

        foreach (var tile in allRoadTiles)
        {
            if (roadTypeMap[tile.x, tile.y] != targetType) continue;

            float thickness = roadThicknessMap[tile.x, tile.y];
            Color fillColor = roadFillColorMap[tile.x, tile.y];

            int fillHalf = Mathf.CeilToInt(thickness / 2f);
            int fillHalfSq = fillHalf * fillHalf;

            for (int ddx = -fillHalf; ddx <= fillHalf; ddx++)
                for (int ddy = -fillHalf; ddy <= fillHalf; ddy++)
                {
                    if (ddx * ddx + ddy * ddy > fillHalfSq) continue;
                    int px = tile.x + ddx, py = tile.y + ddy;
                    if (px < 0 || px >= _w || py < 0 || py >= _h) continue;

                    // Noise tum yol tiplerine uygulanir — anayol ve arayol ayni kirli doku hissini paylasir.
                    Color outColor = ApplyRoadNoise(fillColor, px, py);
                    _tex.SetPixel(px, py, outColor);

                    //Branch fill pass'inde mask'i doldur — highway outline pass'i bunu okuyacak.
                    if (targetType == 2 && branchFillMask != null)
                        branchFillMask[px, py] = true;
                }
        }
    }

    // Perlin (buyuk olcekli yama) + random grain (kumlu doku) ile parlaklik modulasyonu
    Color ApplyRoadNoise(Color baseColor, int px, int py)
    {
        float perlin = Mathf.PerlinNoise(px * highwayNoiseScale, py * highwayNoiseScale);
        float perlinMod = (perlin - 0.5f) * 2f * highwayNoiseStrength;
        float grain = (UnityEngine.Random.value - 0.5f) * 2f * highwayGrainStrength;
        float mod = 1f + perlinMod + grain;
        return new Color(
            Mathf.Clamp01(baseColor.r * mod),
            Mathf.Clamp01(baseColor.g * mod),
            Mathf.Clamp01(baseColor.b * mod),
            baseColor.a);
    }

    // =========================================================================
    // MESAFE ALANI
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

        // Görsel kenardan mesafe — yolun boyanmış halini baz alır
        BuildVisualRoadDistanceField();
    }

    void BuildVisualRoadDistanceField()
    {
        visualRoadDistanceField = new int[_w, _h];
        for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
                visualRoadDistanceField[x, y] = int.MaxValue;

        // Seed: her yol tile'ının görsel yarıçapı içindeki tüm pikseller
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        foreach (var tile in allRoadTiles)
        {
            float thickness = roadThicknessMap[tile.x, tile.y];
            int outW = roadOutlineWidthMap[tile.x, tile.y];
            int totalHalf = Mathf.CeilToInt((thickness + outW * 2) / 2f);
            int totalHalfSq = totalHalf * totalHalf;

            for (int ddx = -totalHalf; ddx <= totalHalf; ddx++)
                for (int ddy = -totalHalf; ddy <= totalHalf; ddy++)
                {
                    if (ddx * ddx + ddy * ddy > totalHalfSq) continue;
                    int px = tile.x + ddx, py = tile.y + ddy;
                    if (px < 0 || px >= _w || py < 0 || py >= _h) continue;
                    if (visualRoadDistanceField[px, py] == 0) continue;
                    visualRoadDistanceField[px, py] = 0;
                    queue.Enqueue(new Vector2Int(px, py));
                }
        }

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d = visualRoadDistanceField[pos.x, pos.y];
            if (d >= 30) continue;
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (visualRoadDistanceField[nx, ny] <= d + 1) continue;
                visualRoadDistanceField[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    // =========================================================================
    // POST-PAINT SANDWICH FILTER — GAP GUVENLIK AGI
    // =========================================================================

    /// <summary>
    /// Paint pass'lerinden sonra, yol piksellerinin arasinda sikismis tekil arka plan
    /// piksellerini kapatir. 4 eksen (yatay, dikey, 2 diagonal) boyunca her iki yonde
    /// [1, R] mesafede yol pikseli varsa, o piksel "sandwich" demektir ve en yakin yol
    /// rengiyle doldurulur. Bridge injection sonrasi kalan kavis-aci kaynakli mikro
    /// delikleri kapatan guvenlik agi.
    /// </summary>
    void FillInteriorSandwichPixels()
    {
        if (_tex == null || visualRoadDistanceField == null) return;

        const int R = 3;

        Color32[] pixels = _tex.GetPixels32();
        bool modified = false;

        // 4 eksen: yatay, dikey, iki kosegen
        int[] axDX = { 1, 0, 1, 1 };
        int[] axDY = { 0, 1, 1, -1 };

        for (int y = 0; y < _h; y++)
        for (int x = 0; x < _w; x++)
        {
            int d = visualRoadDistanceField[x, y];
            if (d == 0) continue;
            if (d > R + 1) continue; // yol cok uzak — sandwich olamaz

            bool sandwich = false;
            for (int a = 0; a < 4; a++)
            {
                int ax = axDX[a];
                int ay = axDY[a];

                bool posFound = false;
                bool negFound = false;
                for (int s = 1; s <= R; s++)
                {
                    int nx = x + ax * s;
                    int ny = y + ay * s;
                    if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) break;
                    if (visualRoadDistanceField[nx, ny] == 0) { posFound = true; break; }
                }
                if (!posFound) continue;
                for (int s = 1; s <= R; s++)
                {
                    int nx = x - ax * s;
                    int ny = y - ay * s;
                    if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) break;
                    if (visualRoadDistanceField[nx, ny] == 0) { negFound = true; break; }
                }
                if (posFound && negFound) { sandwich = true; break; }
            }

            if (!sandwich) continue;

            int idx = x + y * _w;
            pixels[idx] = SampleNearestRoadColor32(x, y, pixels);
            modified = true;
        }

        if (modified)
        {
            _tex.SetPixels32(pixels);
        }
    }

    /// <summary>
    /// (x, y) piksel etrafinda en yakin yol pikselinin rengini dondur. Bulamazsa
    /// mevcut rengi dondurur (no-op fallback).
    /// </summary>
    Color32 SampleNearestRoadColor32(int x, int y, Color32[] pixels)
    {
        for (int r = 1; r <= 3; r++)
        {
            for (int ddx = -r; ddx <= r; ddx++)
            for (int ddy = -r; ddy <= r; ddy++)
            {
                int absX = ddx < 0 ? -ddx : ddx;
                int absY = ddy < 0 ? -ddy : ddy;
                int cheb = absX > absY ? absX : absY;
                if (cheb != r) continue;

                int nx = x + ddx;
                int ny = y + ddy;
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (visualRoadDistanceField[nx, ny] != 0) continue;
                return pixels[nx + ny * _w];
            }
        }
        return pixels[x + y * _w];
    }

    // =========================================================================
    // HELPER
    // =========================================================================

    List<Vector2Int> FilterToLand(MapGenerator map, List<Vector2Int> path, bool allowSkip)
    {
        List<Vector2Int> valid = new List<Vector2Int>();
        foreach (var p in path)
        {
            if (!IsValidRoadPoint(map, p.x, p.y))
            {
                if (allowSkip) continue;
                break;
            }
            valid.Add(p);
        }
        return valid;
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

    /// <summary>
    /// Yolun görsel kenarından (boyanan piksel sınırı) mesafe döndürür.
    /// Highway kalın, branch ince — her biri kendi genişliğine göre hesaplanır.
    /// </summary>
    public int GetDistanceToRoadEdge(int x, int y)
    {
        if (!_generated || visualRoadDistanceField == null || x < 0 || x >= _w || y < 0 || y >= _h)
            return int.MaxValue;
        return visualRoadDistanceField[x, y];
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
    public IReadOnlyList<List<Vector2Int>> GetHighwaySegments() => highwaySegments.AsReadOnly();
    public IReadOnlyList<List<Vector2Int>> GetBranchPaths() => branchPaths.AsReadOnly();
    public int[,] GetRoadTypeMap() => roadTypeMap;
    public bool IsGenerated => _generated;

    /// <summary>
    /// Liman tile'ına en yakın yoldan doğal kıvrımlı bir bağlantı yolu oluşturur.
    /// MapDecorPlacer tarafından limanlar yerleştirildikten sonra çağrılır.
    /// </summary>
    public void ConnectPortToRoad(MapGenerator map, Vector2Int portTile)
    {
        if (!_generated || roadDistanceField == null) return;

        //en yakın yol tile'ını bul
        int bestDist = int.MaxValue;
        Vector2Int bestRoadTile = portTile;

        int searchRadius = 120;
        for (int ddx = -searchRadius; ddx <= searchRadius; ddx++)
        for (int ddy = -searchRadius; ddy <= searchRadius; ddy++)
        {
            int nx = portTile.x + ddx, ny = portTile.y + ddy;
            if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
            if (roadTypeMap[nx, ny] == 0) continue;
            int dist = ddx * ddx + ddy * ddy; //öklid kare mesafe
            if (dist < bestDist)
            {
                bestDist = dist;
                bestRoadTile = new Vector2Int(nx, ny);
            }
        }

        if (bestDist == 0 || bestDist == int.MaxValue) return;

        //BFS ile doğal yol bul — clearance ile mevcut yollarin corridor'ina girmeyi engelle.
        //Boylece path, highway veya baska branch'leri pathfinding seviyesinde zaten KESEMEZ.
        List<Vector2Int> rawPath = BFSPathOnLandSimple(map, bestRoadTile, portTile, roadDistanceField, BRANCH_CLEARANCE);
        if (rawPath.Count < 2)
        {
            //kisitli modda bulunamadi — kisitsiz fallback (nadir edge case)
            rawPath = BFSPathOnLandSimple(map, bestRoadTile, portTile);
            if (rawPath.Count < 2) return;
        }

        //kıvrımlı yumuşatma uygula
        List<Vector2Int> smoothed = SmoothBranchPath(map, rawPath);
        if (smoothed.Count < 2) smoothed = rawPath;

        //iki tarafli corridor tespit + orta tarama — start ve end dogal olarak
        //yol yakininda olabilir; ortada d<SKIP_DIST pikseli varsa orada bir BASKA
        //yolu gecmeye calisiyoruz demektir. O noktada truncate — port artik o
        //kesilen yola baglanir (bestRoadTile yerine). Path iptal edilmez.
        if (roadDistanceField != null)
        {
            int startCorrEnd = 0;
            for (int i = 0; i < smoothed.Count; i++)
            {
                if (roadDistanceField[smoothed[i].x, smoothed[i].y] < BRANCH_PAINT_SKIP_DIST)
                    startCorrEnd = i + 1;
                else break;
            }
            int endCorrStart = smoothed.Count;
            for (int i = smoothed.Count - 1; i >= 0; i--)
            {
                if (roadDistanceField[smoothed[i].x, smoothed[i].y] < BRANCH_PAINT_SKIP_DIST)
                    endCorrStart = i;
                else break;
            }
            for (int i = startCorrEnd; i < endCorrStart; i++)
            {
                if (roadDistanceField[smoothed[i].x, smoothed[i].y] < BRANCH_PAINT_SKIP_DIST)
                {
                    if (i >= 2)
                        smoothed = smoothed.GetRange(0, i + 1);
                    break;
                }
            }
        }

        //yolun otobana girdiği kısmı kes — en yakın yol tile'ında bitir (centerline)
        List<Vector2Int> trimmed = new List<Vector2Int>();
        for (int i = 0; i < smoothed.Count; i++)
        {
            trimmed.Add(smoothed[i]);
            //ilk tile zaten yol üzerinde (başlangıç), onu atla
            if (i > 2 && roadTypeMap[smoothed[i].x, smoothed[i].y] != 0)
                break; //mevcut bir yola ulaştık, burada dur
        }

        if (trimmed.Count < 2) return;

        //corridor'da bitti ama centerline'a ulasmadi (ornek: truncation corridor
        //kenarinda d=3/4'te durdu) — gradient descent ile centerline'a kadar pad
        //et. Boylece iki yolun fill disc'leri centerline'da kesin ortusur; arada
        //arka plan pikseli kalmaz.
        PadPathToNearestCenterline(map, trimmed);

        //branch olarak kaydet ve boya
        branchPaths.Add(new List<Vector2Int>(trimmed));
        RegisterBranchPixels(map, trimmed, 0.8f, 1f);

        // Yeni eklenen connector tile'larinin mevcut yol bandlariyla olasi gap'lerini kapat
        InjectBridgeCenterlines(map);
        BuildRoadDistanceField();

        PaintRoadsByType(2);
        PaintRoadsByType(1); //highway'i uste tekrar boya — konektor bandi asmis olsa dahi highway goruntusu korunur
        PaintJunctionPlateaus();
        PaintCenterStripes();
        FillInteriorSandwichPixels();
        _tex.Apply();

        Debug.Log($"RoadGenerator: Port road ({portTile.x},{portTile.y}) → ({bestRoadTile.x},{bestRoadTile.y}), {trimmed.Count}px");
    }

    /// <summary>
    /// Özel bina tile'ına en yakın yola ince bir bağlantı yolu çeker.
    /// ConnectPortToRoad ile aynı mantık; daha ince yol (0.4f-0.5f kalınlık).
    /// </summary>
    public void ConnectBuildingToRoad(MapGenerator map, Vector2Int buildingTile)
    {
        if (!_generated || roadDistanceField == null) return;

        int bestDist = int.MaxValue;
        Vector2Int bestRoadTile = buildingTile;

        int searchRadius = 120;
        for (int ddx = -searchRadius; ddx <= searchRadius; ddx++)
        for (int ddy = -searchRadius; ddy <= searchRadius; ddy++)
        {
            int nx = buildingTile.x + ddx, ny = buildingTile.y + ddy;
            if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
            if (roadTypeMap[nx, ny] == 0) continue;
            int dist = ddx * ddx + ddy * ddy;
            if (dist < bestDist) { bestDist = dist; bestRoadTile = new Vector2Int(nx, ny); }
        }

        if (bestDist == 0 || bestDist == int.MaxValue) return;

        //BFS ile doğal yol bul — clearance ile mevcut yollari KESEMEZ
        List<Vector2Int> rawPath = BFSPathOnLandSimple(map, bestRoadTile, buildingTile, roadDistanceField, BRANCH_CLEARANCE);
        if (rawPath.Count < 2)
        {
            //kisitli modda bulunamadi — fallback
            rawPath = BFSPathOnLandSimple(map, bestRoadTile, buildingTile);
            if (rawPath.Count < 2) return;
        }

        List<Vector2Int> smoothed = SmoothBranchPath(map, rawPath);
        if (smoothed.Count < 2) smoothed = rawPath;

        //iki tarafli corridor tespit + orta tarama — crossing noktasinda truncate,
        //bina o kesilen yola baglanir (bestRoadTile yerine). Path iptal edilmez.
        if (roadDistanceField != null)
        {
            int startCorrEnd = 0;
            for (int i = 0; i < smoothed.Count; i++)
            {
                if (roadDistanceField[smoothed[i].x, smoothed[i].y] < BRANCH_PAINT_SKIP_DIST)
                    startCorrEnd = i + 1;
                else break;
            }
            int endCorrStart = smoothed.Count;
            for (int i = smoothed.Count - 1; i >= 0; i--)
            {
                if (roadDistanceField[smoothed[i].x, smoothed[i].y] < BRANCH_PAINT_SKIP_DIST)
                    endCorrStart = i;
                else break;
            }
            for (int i = startCorrEnd; i < endCorrStart; i++)
            {
                if (roadDistanceField[smoothed[i].x, smoothed[i].y] < BRANCH_PAINT_SKIP_DIST)
                {
                    if (i >= 2)
                        smoothed = smoothed.GetRange(0, i + 1);
                    break;
                }
            }
        }

        List<Vector2Int> trimmed = new List<Vector2Int>();
        for (int i = 0; i < smoothed.Count; i++)
        {
            trimmed.Add(smoothed[i]);
            if (i > 2 && roadTypeMap[smoothed[i].x, smoothed[i].y] != 0) break;
        }

        if (trimmed.Count < 2) return;

        //corridor'da bitti ama centerline'a ulasmadi — gradient descent ile
        //centerline'a kadar pad et (branch-to-branch junction'daki gorsel kopukluk fix'i)
        PadPathToNearestCenterline(map, trimmed);

        branchPaths.Add(new List<Vector2Int>(trimmed));
        RegisterThinRoadPixels(map, trimmed);

        // Yeni eklenen connector tile'larinin mevcut yol bandlariyla olasi gap'lerini kapat
        InjectBridgeCenterlines(map);
        BuildRoadDistanceField();

        PaintRoadsByType(2);
        PaintRoadsByType(1); //highway'i uste tekrar boya
        PaintJunctionPlateaus();
        PaintCenterStripes();
        FillInteriorSandwichPixels();
        _tex.Apply();

        Debug.Log($"RoadGenerator: Building road ({buildingTile.x},{buildingTile.y}), {trimmed.Count}px");
    }

    /// <summary>
    /// Belediye binasına 4 kardinal yönden (sağ, üst, sol, alt) TEMİZ + şeklinde yol çeker.
    /// Mimari (basit):
    ///   1. Her yönde düz bir ışın gönder. Adım adım ilerle.
    ///   2. Her adımda ışının karşısında ±5 tile perpendicular pencereye bak.
    ///      İlk rastlanan yol tile'ı hedef olur.
    ///   3. Suya çarpınca veya haritadan çıkınca → o yönde yol yok, atlanır.
    ///   4. Hedef bulunursa bina ile hedef arasına DÜZ çizgi çizilir (kavis yok).
    /// BFS yok, smoothing yok — temiz + görüntüsü.
    /// </summary>
    public void ConnectCityHallToRoads(MapGenerator map, Vector2Int cityHallTile)
    {
        if (!_generated || allRoadTiles.Count == 0) return;

        // 4 kardinal yön vektörü: sağ, üst, sol, alt
        Vector2Int[] directions = {
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 0),
            new Vector2Int(0, -1),
        };

        const int minTargetDistance = 35;  // binaya çok yakın yollar hedef alınmaz (bina içi buffer)
        const int tooCloseThreshold = 30;  // ilk yol bu mesafeden yakınsa yön atlanır — zaten yakın, konektör gereksiz
        const int maxRayLength = 1000;     // en uzun arama (harita büyük olabilir)
        const int perpWindow = 5;          // ışın uç noktasında ±5 tile yanlara bak

        int connected = 0;
        foreach (var dir in directions)
        {
            Vector2Int perp = new Vector2Int(-dir.y, dir.x); // ışına dik yön
            Vector2Int hit = new Vector2Int(-1, -1);

            for (int d = minTargetDistance; d < maxRayLength; d++)
            {
                int cx = cityHallTile.x + dir.x * d;
                int cy = cityHallTile.y + dir.y * d;
                if (cx < 0 || cx >= _w || cy < 0 || cy >= _h) break;
                if (!map.IsLand(cx, cy)) break; // suya/harita dışına çarptı → yön iptal

                // Perpendicular sweep — hafif sağa/sola yerleşik yolları da yakala
                for (int p = 0; p <= perpWindow; p++)
                {
                    // p = 0 merkez, sonra ±1, ±2 ... (merkeze en yakın önce)
                    int[] offsets = p == 0 ? new[] { 0 } : new[] { p, -p };
                    foreach (int off in offsets)
                    {
                        int nx = cx + perp.x * off;
                        int ny = cy + perp.y * off;
                        if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                        if (roadTypeMap[nx, ny] != 0) { hit = new Vector2Int(nx, ny); break; }
                    }
                    if (hit.x >= 0) break;
                }
                if (hit.x >= 0) break;
            }

            if (hit.x < 0) continue; // bu yönde yol yok — atla, zorlamayı kaldırdık

            // Yol zaten çok yakınsa konektör çizmeye gerek yok — bina o yola yeterince yakın
            float hitDist = Vector2.Distance(new Vector2(cityHallTile.x, cityHallTile.y), new Vector2(hit.x, hit.y));
            if (hitDist <= tooCloseThreshold) continue;

            // Bina ile hedef arasında düz iskelet çizgi (Bresenham benzeri lerp)
            int steps = Mathf.Max(Mathf.Abs(hit.x - cityHallTile.x), Mathf.Abs(hit.y - cityHallTile.y));
            if (steps < 2) continue;

            List<Vector2Int> rawPath = new List<Vector2Int>(steps + 1);
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                int px = Mathf.RoundToInt(Mathf.Lerp(cityHallTile.x, hit.x, t));
                int py = Mathf.RoundToInt(Mathf.Lerp(cityHallTile.y, hit.y, t));
                rawPath.Add(new Vector2Int(px, py));
            }

            // Perlin tabanlı doğal kavis (kısa yollarda iskelet aynen döner)
            List<Vector2Int> curved = SmoothBranchPath(map, rawPath);
            if (curved.Count < 2) curved = rawPath;

            // Kıvrım path'i hit'e varmadan baska bir yolu geciyor mu kontrol et.
            // Bina ve hit uclarinda dogal olarak road-yakini corridor'lar var.
            // Bu iki corridor'un dışında d<SKIP_DIST bulursak, path o yolu kesiyor —
            // o noktada path'i kes. Bina artik o gecilen yola baglanir (hit yerine).
            if (roadDistanceField != null)
            {
                int startCorrEnd = 0;
                for (int i = 0; i < curved.Count; i++)
                {
                    if (roadDistanceField[curved[i].x, curved[i].y] < BRANCH_PAINT_SKIP_DIST)
                        startCorrEnd = i + 1;
                    else break;
                }
                int endCorrStart = curved.Count;
                for (int i = curved.Count - 1; i >= 0; i--)
                {
                    if (roadDistanceField[curved[i].x, curved[i].y] < BRANCH_PAINT_SKIP_DIST)
                        endCorrStart = i;
                    else break;
                }
                for (int i = startCorrEnd; i < endCorrStart; i++)
                {
                    if (roadDistanceField[curved[i].x, curved[i].y] < BRANCH_PAINT_SKIP_DIST)
                    {
                        if (i >= 2)
                            curved = curved.GetRange(0, i + 1);
                        break;
                    }
                }
            }

            // İlk yol tile'ında kes — anayolun içine dalmayı engeller (temiz T-kavşak)
            List<Vector2Int> trimmed = new List<Vector2Int>();
            for (int i = 0; i < curved.Count; i++)
            {
                trimmed.Add(curved[i]);
                if (i > 0 && roadTypeMap[curved[i].x, curved[i].y] != 0) break;
            }
            if (trimmed.Count < 2) continue;

            //corridor-aware truncation ortada tetiklenmis olabilir — gap onlem
            PadPathToNearestCenterline(map, trimmed);

            branchPaths.Add(new List<Vector2Int>(trimmed));
            RegisterThinRoadPixels(map, trimmed);
            connected++;
        }

        if (connected > 0)
        {
            // Yeni eklenen cityhall yollarinin mevcut bandlariyla olasi gap'lerini kapat
            InjectBridgeCenterlines(map);
            BuildRoadDistanceField();

            PaintRoadsByType(2);
            PaintRoadsByType(1); //highway'i uste tekrar boya — konektor bandi asmis olsa dahi highway goruntusu korunur
            PaintJunctionPlateaus();
            PaintCenterStripes();
                FillInteriorSandwichPixels();
            _tex.Apply();
            Debug.Log($"RoadGenerator: CityHall connected with {connected} roads.");
        }
    }

    /// <summary>
    /// Path corridor kenarinda (d=1..BRANCH_PAINT_SKIP_DIST-1) bittiyse,
    /// distance-field gradient'ini takip ederek centerline'a (d=0) kadar pad
    /// eder. Amac: iki yolun fill disc'leri centerline'da kesin ortussun; arada
    /// arka plan pikseli ("gorsel kopukluk") kalmasin.
    /// Path zaten centerline'da bitiyorsa veya hedefte boslukta ise (d cok
    /// buyuk — port/bina asil hedef) hicbir sey yapmaz.
    /// </summary>
    void PadPathToNearestCenterline(MapGenerator map, List<Vector2Int> path, int[,] distField = null)
    {
        int[,] df = distField ?? roadDistanceField;
        if (path.Count == 0 || df == null) return;
        Vector2Int cur = path[path.Count - 1];
        if (cur.x < 0 || cur.x >= _w || cur.y < 0 || cur.y >= _h) return;

        int curD = df[cur.x, cur.y];
        //sadece corridor icinde bitenler icin pad. d==0 => zaten centerline.
        //d>=SKIP_DIST => dogal son (hedef boslukta — port/bina/target).
        if (curD <= 0 || curD >= BRANCH_PAINT_SKIP_DIST) return;

        int safety = BRANCH_PAINT_SKIP_DIST + 2;
        while (curD > 0 && safety-- > 0)
        {
            Vector2Int best = cur;
            int bestD = curD;
            //8-komsu icinde en dusuk d'ye yurur (Chebyshev adim — diyagonal serbest)
            for (int ddx = -1; ddx <= 1; ddx++)
            for (int ddy = -1; ddy <= 1; ddy++)
            {
                if (ddx == 0 && ddy == 0) continue;
                int nx = cur.x + ddx, ny = cur.y + ddy;
                if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
                if (!map.IsLand(nx, ny)) continue;
                int nd = df[nx, ny];
                if (nd < bestD) { bestD = nd; best = new Vector2Int(nx, ny); }
            }
            if (bestD >= curD) break; //yerel minimum — daha ileri gidemiyoruz
            path.Add(best);
            cur = best;
            curD = bestD;
        }
    }

    /// <summary>
    /// Belediye ve özel bina yolları için ince sabit kalınlıklı yol kaydı.
    /// </summary>
    void RegisterThinRoadPixels(MapGenerator map, List<Vector2Int> pixels)
    {
        for (int p = 0; p < pixels.Count; p++)
        {
            // Sabit 1px kalınlık — çok ince erişim yolu. Renk dallanma ile ayni unifiye govde.
            RegisterRoadTile(pixels[p], 2, 1f, Mathf.Max(0, branchOutlineWidth - 1), branchFill, branchOutline);
        }
    }

    public void Clear()
    {
        allRoadTiles.Clear();
        highwayTiles.Clear();
        highwaySegments.Clear();
        branchPaths.Clear();
        regionCenters.Clear();
        roadTypeMap = null;
        roadDistanceField = null;
        visualRoadDistanceField = null;
        roadThicknessMap = null;
        roadFillColorMap = null;
        roadOutlineColorMap = null;
        roadOutlineWidthMap = null;
        shoreDistCache = null;
        _tex = null;
        _generated = false;
    }
}