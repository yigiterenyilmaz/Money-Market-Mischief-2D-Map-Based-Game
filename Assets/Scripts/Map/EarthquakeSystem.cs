using System;
using UnityEngine;
using System.Collections.Generic;

public class EarthquakeSystem : MonoBehaviour
{
    public static EarthquakeSystem Instance { get; private set; }
    public static event Action OnEarthquakeOccurred;

    [Header("References")]
    public MapGenerator   mapGenerator;
    public MapPainter     mapPainter;
    public MapDecorPlacer mapDecorPlacer;

    [Header("Probability")]
    [Tooltip("Multiplied by NaturalEventsIndex (0-1) to get final chance per TryTriggerEarthquake call.")]
    [Range(0f, 1f)] public float baseEarthquakeProbability = 0.5f;

    [Header("Surface Crack Color")]
    public Color surfaceCrackColor = new Color(0.10f, 0.07f, 0.05f);

    private bool ready;

    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  { FaultLineGenerator.OnFaultLinesGenerated += OnReady; }
    void OnDisable() { FaultLineGenerator.OnFaultLinesGenerated -= OnReady; }

    void Start()
    {
        // Fallback: fault lines may have already generated before we subscribed
        if (!ready && FaultLineGenerator.Instance != null && FaultLineGenerator.Instance.IsGenerated)
            OnReady();
    }

    void OnReady() { ready = true; }

    // -------------------------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rolls probability against NaturalEventsIndex.
    /// Returns true if an earthquake was triggered. Call from your turn manager.
    /// </summary>
    public bool TryTriggerEarthquake()
    {
        if (!ready || CountryData.Instance == null) return false;
        float chance = baseEarthquakeProbability * CountryData.Instance.NaturalEventsIndex;
        if (UnityEngine.Random.value > chance) return false;
        TriggerEarthquake();
        return true;
    }

    /// <summary>Forces an earthquake regardless of probability.</summary>
    [ContextMenu("Debug: Force Earthquake")]
    public void TriggerEarthquake()
    {
        if (!ready)
        {
            if (FaultLineGenerator.Instance != null && FaultLineGenerator.Instance.IsGenerated)
                ready = true;
            else
            {
                Debug.LogWarning("EarthquakeSystem: Not ready — fault lines not yet generated.");
                return;
            }
        }

        var faultGen = FaultLineGenerator.Instance;
        if (faultGen == null || !faultGen.IsGenerated) return;

        // Pick epicenter weighted by fault weight map — intersections are more likely
        Vector2Int epicenter = SampleWeightedEpicenter(faultGen);
        Debug.Log($"EarthquakeSystem: Epicenter at {epicenter}");

        // Paint surface cracks along all fault tiles
        if (mapPainter != null)
            mapPainter.ApplyCracks(faultGen.GetFaultMap(), surfaceCrackColor);

        // Destroy city buildings that sit on fault tiles
        if (mapDecorPlacer != null)
            mapDecorPlacer.DestroyBuildingsOnFaultLines(faultGen);

        OnEarthquakeOccurred?.Invoke();
        Debug.Log("EarthquakeSystem: Earthquake triggered!");
    }

    // -------------------------------------------------------------------------
    // WEIGHTED EPICENTER SELECTION
    // -------------------------------------------------------------------------

    /// <summary>
    /// Samples a fault tile weighted by faultWeightMap.
    /// Intersection tiles (stamped by multiple faults) have proportionally
    /// higher weight and are more likely to be selected as the epicenter.
    /// </summary>
    Vector2Int SampleWeightedEpicenter(FaultLineGenerator faultGen)
    {
        float[,] weightMap = faultGen.GetFaultWeightMap();
        int w = mapGenerator.width;
        int h = mapGenerator.height;

        List<Vector2Int> tiles   = new List<Vector2Int>();
        List<float>      weights = new List<float>();
        float            total   = 0f;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!faultGen.IsFault(x, y)) continue;
            float wt = weightMap[x, y];
            tiles.Add(new Vector2Int(x, y));
            weights.Add(wt);
            total += wt;
        }

        if (tiles.Count == 0)
            return new Vector2Int(w / 2, h / 2);

        float roll       = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;

        for (int i = 0; i < tiles.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative) return tiles[i];
        }

        return tiles[tiles.Count - 1];
    }
}