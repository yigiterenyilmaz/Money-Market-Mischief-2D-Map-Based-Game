using System;
using System.Collections.Generic;
using UnityEngine;

public class PetroleumBedGenerator : MonoBehaviour
{
    public static PetroleumBedGenerator Instance { get; private set; }

    [Header("References")]
    public MapGenerator mapGenerator;

    [Header("Bed Count")]
    [SerializeField] [Range(1, 20)] public int minBeds = 3;
    [SerializeField] [Range(1, 30)] public int maxBeds = 8;

    [Header("Bed Size (tiles)")]
    [SerializeField] [Range(5, 60)]  public int minBedRadius = 15;
    [SerializeField] [Range(10, 120)] public int maxBedRadius = 45;

    [Header("Noise Shape")]
    [SerializeField] [Range(0.02f, 0.15f)] public float noiseScale = 0.05f;
    [SerializeField] [Range(0.2f, 0.6f)]   public float noiseThreshold = 0.35f;

    [Header("Purity")]
    [SerializeField] [Range(0.1f, 0.5f)] public float minBasePurity = 0.2f;
    [SerializeField] [Range(0.5f, 1f)]   public float maxBasePurity = 0.85f;
    [SerializeField] [Range(0f, 1f)]     public float purityEdgeFalloff = 0.5f;

    [Header("Placement")]
    [SerializeField] [Range(10, 200)] public int placementAttempts = 60;
    [SerializeField] [Range(0, 80)]   public int minBedSeparation = 25;

    private float[,] purityMap;
    private int[,]   bedIndexMap;
    private List<PetroleumBed> beds = new List<PetroleumBed>();
    private bool generated = false;

    public static event Action OnPetroleumGenerated;

    [Serializable]
    public class PetroleumBed
    {
        public Vector2Int center;
        public int radius;
        public float basePurity, scaledPurity;
        public float noiseOffsetX, noiseOffsetY;
        public int tileCount;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  { CountryData.OnCountryDataReady += Generate; }
    void OnDisable() { CountryData.OnCountryDataReady -= Generate; }

    public void Generate()
    {
        if (mapGenerator == null) { Debug.LogError("PetroleumBedGenerator: MapGenerator missing."); return; }

        int w = mapGenerator.width, h = mapGenerator.height;
        purityMap = new float[w, h];
        bedIndexMap = new int[w, h];
        beds.Clear();
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) bedIndexMap[x, y] = -1;

        float areaScale = (w * h) / (256f * 256f);
        int bedCount = Mathf.Clamp(
            Mathf.RoundToInt(UnityEngine.Random.Range(minBeds, maxBeds + 1) * Mathf.Sqrt(areaScale)),
            minBeds, maxBeds);

        float wealth = CountryData.Instance != null ? CountryData.Instance.NaturalResourceWealth : 50f;
        float wealthMul = Mathf.Lerp(0.15f, 1f, wealth / 100f);

        List<Vector2Int> usedCenters = new List<Vector2Int>();

        for (int b = 0; b < bedCount; b++)
        {
            Vector2Int center = FindCenter(w, h, usedCenters);
            if (center.x < 0) continue;

            int radius = Mathf.Clamp(
                Mathf.RoundToInt(UnityEngine.Random.Range(minBedRadius, maxBedRadius + 1) * Mathf.Sqrt(areaScale)),
                minBedRadius, maxBedRadius);

            float basePurity = UnityEngine.Random.Range(minBasePurity, maxBasePurity);

            PetroleumBed bed = new PetroleumBed
            {
                center = center, radius = radius,
                basePurity = basePurity, scaledPurity = basePurity * wealthMul,
                noiseOffsetX = UnityEngine.Random.Range(0f, 9999f),
                noiseOffsetY = UnityEngine.Random.Range(0f, 9999f),
                tileCount = 0
            };

            StampBed(bed, beds.Count, w, h);
            beds.Add(bed);
            usedCenters.Add(center);
        }

        generated = true;
        Debug.Log($"Petrol yatakları: {beds.Count} yatak, kaynak çarpanı: {wealthMul:F2}");
        OnPetroleumGenerated?.Invoke();
    }

    Vector2Int FindCenter(int w, int h, List<Vector2Int> used)
    {
        for (int a = 0; a < placementAttempts; a++)
        {
            int x = UnityEngine.Random.Range(0, w), y = UnityEngine.Random.Range(0, h);
            if (!mapGenerator.IsActionableLand(x, y)) continue;
            bool tooClose = false;
            foreach (var c in used)
                if (Vector2Int.Distance(new Vector2Int(x, y), c) < minBedSeparation) { tooClose = true; break; }
            if (!tooClose) return new Vector2Int(x, y);
        }
        return new Vector2Int(-1, -1);
    }

    void StampBed(PetroleumBed bed, int idx, int w, int h)
    {
        int cx = bed.center.x, cy = bed.center.y, r = bed.radius;
        int scanR = Mathf.RoundToInt(r * 1.2f);

        for (int dx = -scanR; dx <= scanR; dx++)
        for (int dy = -scanR; dy <= scanR; dy++)
        {
            int px = cx + dx, py = cy + dy;
            if (px < 0 || px >= w || py < 0 || py >= h) continue;
            if (!mapGenerator.IsActionableLand(px, py)) continue;

            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float normDist = dist / r;

            float edgeFade = Mathf.Clamp01(normDist);
            float adjustedThreshold = Mathf.Lerp(noiseThreshold, 0.95f, edgeFade * edgeFade);

            float noise = Mathf.PerlinNoise(px * noiseScale + bed.noiseOffsetX, py * noiseScale + bed.noiseOffsetY);
            if (noise < adjustedThreshold) continue;

            float purityFade = 1f - Mathf.Clamp01(normDist) * purityEdgeFalloff;
            if (normDist > 0.7f && UnityEngine.Random.value < (normDist - 0.7f) * 0.5f) continue;

            float tilePurity = bed.scaledPurity * purityFade;
            if (tilePurity <= 0.001f) continue;

            if (tilePurity > purityMap[px, py])
            {
                purityMap[px, py] = tilePurity;
                bedIndexMap[px, py] = idx;
            }
            bed.tileCount++;
        }
    }

    // === PUBLIC API ===

    public float GetPurity(int x, int y)
    {
        if (!generated || x < 0 || x >= mapGenerator.width || y < 0 || y >= mapGenerator.height) return 0f;
        return purityMap[x, y];
    }

    public bool HasPetroleum(int x, int y) => GetPurity(x, y) > 0f;

    public int GetBedIndex(int x, int y)
    {
        if (!generated || x < 0 || x >= mapGenerator.width || y < 0 || y >= mapGenerator.height) return -1;
        return bedIndexMap[x, y];
    }

    public IReadOnlyList<PetroleumBed> GetBeds() => beds.AsReadOnly();
    public float[,] GetPurityMap() => purityMap;
    public bool IsGenerated => generated;

    public List<Vector2Int> GetPetroleumTilesInCircle(Vector2Int center, int radius)
    {
        var result = new List<Vector2Int>();
        if (!generated) return result;
        int w = mapGenerator.width, h = mapGenerator.height;
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            int px = center.x + dx, py = center.y + dy;
            if (px < 0 || px >= w || py < 0 || py >= h) continue;
            if (purityMap[px, py] > 0f) result.Add(new Vector2Int(px, py));
        }
        return result;
    }
}