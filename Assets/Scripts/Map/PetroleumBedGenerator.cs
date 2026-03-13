using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates hidden petroleum beds on the map after map generation completes.
/// Each bed is a Perlin-noise blob with a center, radius, purity, and per-tile data.
/// Global purity is scaled by CountryData.NaturalResourceWealth.
/// </summary>
public class PetroleumBedGenerator : MonoBehaviour
{
    public static PetroleumBedGenerator Instance { get; private set; }

    // -------------------------------------------------------------------------
    // CONFIGURATION
    // -------------------------------------------------------------------------

    [Header("References")]
    public MapGenerator mapGenerator;

    [Header("Bed Count")]
    [Tooltip("Minimum number of petroleum beds on the map.")]
    [Range(1, 20)] public int minBeds = 3;

    [Tooltip("Maximum number of petroleum beds on the map.")]
    [Range(1, 30)] public int maxBeds = 8;

    [Header("Bed Size (in tiles)")]
    [Tooltip("Minimum radius of a petroleum bed in tiles.")]
    [Range(5, 40)] public int minBedRadius = 12;

    [Tooltip("Maximum radius of a petroleum bed in tiles.")]
    [Range(10, 80)] public int maxBedRadius = 35;

    [Header("Noise Shape")]
    [Tooltip("Perlin noise scale for bed shape. Lower = smoother blobs, higher = more jagged.")]
    [Range(0.02f, 0.15f)] public float noiseScale = 0.06f;

    [Tooltip("Noise threshold for a tile to be considered part of the bed. Lower = larger blobs.")]
    [Range(0.25f, 0.6f)] public float noiseThreshold = 0.40f;

    [Header("Purity")]
    [Tooltip("Minimum base purity for a bed (before NaturalResourceWealth scaling).")]
    [Range(0.1f, 0.5f)] public float minBasePurity = 0.2f;

    [Tooltip("Maximum base purity for a bed (before NaturalResourceWealth scaling).")]
    [Range(0.5f, 1f)] public float maxBasePurity = 0.85f;

    [Tooltip("How much purity falls off from center to edge. 0 = uniform, 1 = full falloff.")]
    [Range(0f, 1f)] public float purityEdgeFalloff = 0.6f;

    [Header("Placement")]
    [Tooltip("Maximum attempts to find a valid center for each bed.")]
    [Range(10, 200)] public int placementAttempts = 60;

    [Tooltip("Minimum distance between bed centers in tiles.")]
    [Range(0, 60)] public int minBedSeparation = 20;

    // -------------------------------------------------------------------------
    // RUNTIME DATA
    // -------------------------------------------------------------------------

    /// <summary>Per-tile purity. 0 = no petroleum, >0 = petroleum present.</summary>
    private float[,] purityMap;

    /// <summary>Per-tile bed index (-1 = none).</summary>
    private int[,] bedIndexMap;

    private List<PetroleumBed> beds = new List<PetroleumBed>();
    private bool generated = false;

    public static event Action OnPetroleumGenerated;

    // -------------------------------------------------------------------------
    // DATA STRUCTURES
    // -------------------------------------------------------------------------

    [Serializable]
    public class PetroleumBed
    {
        public Vector2Int center;
        public int        radius;
        public float      basePurity;   // raw purity before wealth scaling
        public float      scaledPurity; // after NaturalResourceWealth multiplier
        public float      noiseOffsetX;
        public float      noiseOffsetY;
        public int        tileCount;
    }

    // -------------------------------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        CountryData.OnCountryDataReady += Generate;
    }

    private void OnDisable()
    {
        CountryData.OnCountryDataReady -= Generate;
    }

    // -------------------------------------------------------------------------
    // GENERATION
    // -------------------------------------------------------------------------

    public void Generate()
    {
        if (mapGenerator == null)
        {
            Debug.LogError("PetroleumBedGenerator: MapGenerator reference is missing.");
            return;
        }

        int w = mapGenerator.width;
        int h = mapGenerator.height;

        purityMap   = new float[w, h];
        bedIndexMap = new int[w, h];
        beds.Clear();

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                bedIndexMap[x, y] = -1;

        // Scale bed count with map area relative to 256×256 base
        float areaScale = (w * h) / (256f * 256f);
        int bedCount = Mathf.RoundToInt(UnityEngine.Random.Range(minBeds, maxBeds + 1) * Mathf.Sqrt(areaScale));
        bedCount = Mathf.Clamp(bedCount, minBeds, maxBeds);

        // NaturalResourceWealth: 0–100 → 0.15–1.0 multiplier
        // Even wealth=0 countries have some oil, just very low quality
        float wealth = CountryData.Instance != null ? CountryData.Instance.NaturalResourceWealth : 50f;
        float wealthMultiplier = Mathf.Lerp(0.15f, 1f, wealth / 100f);

        List<Vector2Int> usedCenters = new List<Vector2Int>();

        for (int b = 0; b < bedCount; b++)
        {
            Vector2Int center = FindBedCenter(w, h, usedCenters);
            if (center.x < 0) continue; // couldn't find valid spot

            int radius = UnityEngine.Random.Range(minBedRadius, maxBedRadius + 1);
            radius = Mathf.RoundToInt(radius * Mathf.Sqrt(areaScale));
            radius = Mathf.Clamp(radius, minBedRadius, maxBedRadius);

            float basePurity = UnityEngine.Random.Range(minBasePurity, maxBasePurity);

            PetroleumBed bed = new PetroleumBed
            {
                center       = center,
                radius       = radius,
                basePurity   = basePurity,
                scaledPurity = basePurity * wealthMultiplier,
                noiseOffsetX = UnityEngine.Random.Range(0f, 9999f),
                noiseOffsetY = UnityEngine.Random.Range(0f, 9999f),
                tileCount    = 0
            };

            StampBed(bed, beds.Count, w, h);
            beds.Add(bed);
            usedCenters.Add(center);
        }

        generated = true;
        LogBedData(wealthMultiplier);
        OnPetroleumGenerated?.Invoke();
    }

    private Vector2Int FindBedCenter(int w, int h, List<Vector2Int> usedCenters)
    {
        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            int x = UnityEngine.Random.Range(0, w);
            int y = UnityEngine.Random.Range(0, h);

            if (!mapGenerator.IsLand(x, y)) continue;

            bool tooClose = false;
            foreach (var c in usedCenters)
            {
                if (Vector2Int.Distance(new Vector2Int(x, y), c) < minBedSeparation)
                { tooClose = true; break; }
            }
            if (tooClose) continue;

            return new Vector2Int(x, y);
        }

        return new Vector2Int(-1, -1); // failed
    }

    private void StampBed(PetroleumBed bed, int bedIndex, int w, int h)
    {
        int cx = bed.center.x;
        int cy = bed.center.y;
        int r  = bed.radius;

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                int px = cx + dx;
                int py = cy + dy;

                if (px < 0 || px >= w || py < 0 || py >= h) continue;
                if (!mapGenerator.IsLand(px, py)) continue;

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > r) continue;

                // Perlin noise mask — creates irregular blob shape
                float noise = Mathf.PerlinNoise(
                    px * noiseScale + bed.noiseOffsetX,
                    py * noiseScale + bed.noiseOffsetY
                );
                if (noise < noiseThreshold) continue;

                // Purity: richest at center, fades toward edge
                float normalizedDist = dist / r;
                float centerBoost    = 1f - (normalizedDist * purityEdgeFalloff);
                float tilePurity     = bed.scaledPurity * centerBoost;

                // If overlapping another bed, keep the higher purity
                if (tilePurity > purityMap[px, py])
                {
                    purityMap[px, py]   = tilePurity;
                    bedIndexMap[px, py] = bedIndex;
                }

                bed.tileCount++;
            }
        }
    }

    // -------------------------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------------------------

    /// <summary>Petroleum purity at a tile. 0 = no petroleum.</summary>
    public float GetPurity(int x, int y)
    {
        if (!generated) return 0f;
        if (x < 0 || x >= mapGenerator.width || y < 0 || y >= mapGenerator.height) return 0f;
        return purityMap[x, y];
    }

    /// <summary>Returns true if this tile sits on a petroleum bed.</summary>
    public bool HasPetroleum(int x, int y) => GetPurity(x, y) > 0f;

    /// <summary>Bed index at tile, or -1 if none.</summary>
    public int GetBedIndex(int x, int y)
    {
        if (!generated) return -1;
        if (x < 0 || x >= mapGenerator.width || y < 0 || y >= mapGenerator.height) return -1;
        return bedIndexMap[x, y];
    }

    /// <summary>All generated beds (read-only access).</summary>
    public IReadOnlyList<PetroleumBed> GetBeds() => beds.AsReadOnly();

    /// <summary>The full purity map for bulk queries.</summary>
    public float[,] GetPurityMap() => purityMap;

    /// <summary>Whether generation has completed.</summary>
    public bool IsGenerated => generated;

    /// <summary>
    /// Returns all petroleum tile positions inside a circle.
    /// Used by the research/scan system.
    /// </summary>
    public List<Vector2Int> GetPetroleumTilesInCircle(Vector2Int center, int radius)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        if (!generated) return result;

        int w = mapGenerator.width;
        int h = mapGenerator.height;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > radius * radius) continue;

                int px = center.x + dx;
                int py = center.y + dy;

                if (px < 0 || px >= w || py < 0 || py >= h) continue;
                if (purityMap[px, py] > 0f)
                    result.Add(new Vector2Int(px, py));
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // DEBUG
    // -------------------------------------------------------------------------

    private void LogBedData(float wealthMultiplier)
    {
        string log = $"=== PETROL YATAKLARI ===\n" +
                     $"Toplam yatak: {beds.Count}, Kaynak çarpanı: {wealthMultiplier:F2}\n";

        for (int i = 0; i < beds.Count; i++)
        {
            var b = beds[i];
            log += $"  [{i}] Merkez=({b.center.x},{b.center.y}) " +
                   $"R={b.radius} Saflık={b.scaledPurity:F2} " +
                   $"Karo={b.tileCount}\n";
        }

        Debug.Log(log);
    }
}