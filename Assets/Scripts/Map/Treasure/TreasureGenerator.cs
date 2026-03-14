using System;
using System.Collections.Generic;
using UnityEngine;

public class TreasureGenerator : MonoBehaviour
{
    public static TreasureGenerator Instance { get; private set; }

    [Header("References")]
    public MapGenerator mapGenerator;

    [Header("Spawn Rate")]
    [Tooltip("Number of treasures per 256x256 map area. Scales with map size.")]
    [SerializeField] [Range(1, 50)] public int baseTreasureCount = 10;

    [Header("Treasure Value")]
    [Tooltip("Base money earned from digging a treasure.")]
    [SerializeField] public float baseReward = 500f;
    [Tooltip("Random variation applied to reward (±fraction).")]
    [SerializeField] [Range(0f, 0.8f)] public float rewardVariation = 0.4f;

    [Header("Dig Time")]
    [Tooltip("Base time in seconds to dig a treasure.")]
    [SerializeField] public float baseDigTime = 4f;
    [Tooltip("Random variation applied to dig time (±fraction).")]
    [SerializeField] [Range(0f, 0.8f)] public float digTimeVariation = 0.3f;

    [Header("Placement")]
    [SerializeField] [Range(5, 100)] public int minTreasureSeparation = 20;
    [SerializeField] [Range(10, 200)] public int placementAttempts = 80;

    [Header("Debug")]
    [SerializeField] [Tooltip("Reveal all treasures on the underground map without needing research. Testing only.")]
    public bool debugRevealAllTreasures = false;

    [Header("Underground Color")]
    [Tooltip("Color for discovered treasure tiles on the underground map.")]
    public Color treasureColor = new Color(0.85f, 0.72f, 0.15f);

    private List<Treasure> treasures = new List<Treasure>();
    private Dictionary<Vector2Int, int> tileToTreasure = new Dictionary<Vector2Int, int>();
    private bool generated = false;

    public static event Action OnTreasuresGenerated;

    [Serializable]
    public class Treasure
    {
        public Vector2Int tilePos;
        public float reward;
        public float digTime;
        public bool discovered;
        public bool dugUp;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  { PetroleumBedGenerator.OnPetroleumGenerated += Generate; }
    void OnDisable() { PetroleumBedGenerator.OnPetroleumGenerated -= Generate; }

    public void Generate()
    {
        if (mapGenerator == null) { Debug.LogError("TreasureGenerator: MapGenerator missing."); return; }

        treasures.Clear();
        tileToTreasure.Clear();

        int w = mapGenerator.width, h = mapGenerator.height;
        float areaScale = (w * h) / (256f * 256f);
        int count = Mathf.Max(1, Mathf.RoundToInt(baseTreasureCount * areaScale));

        List<Vector2Int> usedPositions = new List<Vector2Int>();

        for (int i = 0; i < count; i++)
        {
            Vector2Int pos = FindPosition(w, h, usedPositions);
            if (pos.x < 0) continue;

            float rewardMul = 1f + UnityEngine.Random.Range(-rewardVariation, rewardVariation);
            float digMul    = 1f + UnityEngine.Random.Range(-digTimeVariation, digTimeVariation);

            Treasure t = new Treasure
            {
                tilePos    = pos,
                reward     = baseReward * rewardMul,
                digTime    = baseDigTime * digMul,
                discovered = false,
                dugUp      = false
            };

            int idx = treasures.Count;
            treasures.Add(t);
            tileToTreasure[pos] = idx;
            usedPositions.Add(pos);
        }

        generated = true;
        Debug.Log($"Hazineler: {treasures.Count} hazine oluşturuldu.");
        OnTreasuresGenerated?.Invoke();
    }

    Vector2Int FindPosition(int w, int h, List<Vector2Int> used)
    {
        for (int a = 0; a < placementAttempts; a++)
        {
            int x = UnityEngine.Random.Range(0, w), y = UnityEngine.Random.Range(0, h);
            if (!mapGenerator.IsActionableLand(x, y)) continue;

            bool tooClose = false;
            foreach (var c in used)
                if (Vector2Int.Distance(new Vector2Int(x, y), c) < minTreasureSeparation)
                { tooClose = true; break; }
            if (!tooClose) return new Vector2Int(x, y);
        }
        return new Vector2Int(-1, -1);
    }

    // === PUBLIC API ===

    public bool IsGenerated => generated;
    public IReadOnlyList<Treasure> GetTreasures() => treasures.AsReadOnly();

    /// <summary>Check if a tile has a treasure (not yet dug up).</summary>
    public bool HasTreasure(int x, int y)
    {
        if (!generated) return false;
        var key = new Vector2Int(x, y);
        if (!tileToTreasure.TryGetValue(key, out int idx)) return false;
        return !treasures[idx].dugUp;
    }

    /// <summary>Get treasure at a tile, or null.</summary>
    public Treasure GetTreasureAt(int x, int y)
    {
        if (!generated) return null;
        var key = new Vector2Int(x, y);
        if (!tileToTreasure.TryGetValue(key, out int idx)) return null;
        return treasures[idx];
    }

    /// <summary>Mark a treasure as discovered (visible on underground map).</summary>
    public void DiscoverTreasure(Vector2Int pos)
    {
        if (!tileToTreasure.TryGetValue(pos, out int idx)) return;
        treasures[idx].discovered = true;
    }

    /// <summary>Mark a treasure as dug up.</summary>
    public void MarkDugUp(Vector2Int pos)
    {
        if (!tileToTreasure.TryGetValue(pos, out int idx)) return;
        treasures[idx].dugUp = true;
    }

    /// <summary>Debug: mark all treasures as discovered.</summary>
    public void DebugDiscoverAll()
    {
        foreach (var t in treasures)
            t.discovered = true;
    }

    public bool DebugRevealEnabled => debugRevealAllTreasures;
}