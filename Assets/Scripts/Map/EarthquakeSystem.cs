using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EarthquakeSystem : MonoBehaviour
{
    public static EarthquakeSystem Instance { get; private set; }
    public static event Action<Vector2Int, int> OnEarthquakeOccurred;

    [Header("References")]
    public MapGenerator   mapGenerator;
    public MapPainter     mapPainter;
    public MapDecorPlacer mapDecorPlacer;
    public RoadGenerator  roadGenerator;

    [Header("Probability")]
    [Range(0f, 1f)] public float baseEarthquakeProbability = 0.5f;

    [Header("Earthquake Radius")]
    [Range(5,  40)]  public int minRadius          = 12;
    [Range(20, 120)] public int maxRadius          = 60;
    [Range(5,  50)]  public int nodeInfluenceRange = 22;

    [Header("Surface Cracks")]
    public Color crackColor                       = new Color(0.06f, 0.04f, 0.02f);
    [Range(4, 16)]    public int   numCracks      = 9;
    [Range(1, 3)]     public int   maxBranchDepth = 2;
    [Range(0f, 0.4f)] public float branchChance   = 0.25f;

    [Header("Debug Button")]
    public Canvas debugCanvas;
    public Font   debugButtonFont;

    private HashSet<Vector2Int> brokenRoadTiles = new HashSet<Vector2Int>();
    public  IReadOnlyCollection<Vector2Int> BrokenRoadTiles => brokenRoadTiles;

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
        if (!ready && FaultLineGenerator.Instance != null && FaultLineGenerator.Instance.IsGenerated)
            OnReady();
        if (debugCanvas != null)
            CreateDebugButton();
    }

    void OnReady() { ready = true; Debug.Log("EarthquakeSystem: Ready."); }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public bool TryTriggerEarthquake()
    {
        if (!ready || CountryData.Instance == null) return false;
        float chance = baseEarthquakeProbability * CountryData.Instance.NaturalEventsIndex;
        if (UnityEngine.Random.value > chance) return false;
        TriggerEarthquake();
        return true;
    }

    public void TriggerEarthquake()
    {
        if (!ready)
        {
            if (FaultLineGenerator.Instance != null && FaultLineGenerator.Instance.IsGenerated)
                ready = true;
            else { Debug.LogWarning("EarthquakeSystem: FaultLineGenerator not ready."); return; }
        }

        var faultGen = FaultLineGenerator.Instance;
        if (faultGen == null || !faultGen.IsGenerated) return;

        Vector2Int epicenter = SampleWeightedEpicenter(faultGen);
        int        radius    = CalculateRadius(epicenter, faultGen);

        Debug.Log($"EarthquakeSystem: Epicenter={epicenter}, Radius={radius}");

        // 1. Draw cracks originating from fault tiles inside the circle
        HashSet<Vector2Int> crackedTiles = null;
        if (mapPainter != null)
        {
            crackedTiles = mapPainter.DrawCracks(
                faultGen.GetFaultMap(),
                epicenter, radius, crackColor,
                numCracks, maxBranchDepth, branchChance);
        }

        // 2. Swap sprites on buildings whose tiles were cracked
        if (crackedTiles != null && mapDecorPlacer != null)
            mapDecorPlacer.MarkBuildingsBroken(crackedTiles);

        // 3. Break roads that intersect cracked tiles
        if (crackedTiles != null && roadGenerator != null)
            BreakRoads(crackedTiles);

        OnEarthquakeOccurred?.Invoke(epicenter, radius);
        Debug.Log("EarthquakeSystem: Done.");
    }

    // =========================================================================
    // ROAD BREAKING
    // =========================================================================

    void BreakRoads(HashSet<Vector2Int> crackedTiles)
    {
        int broken = 0;
        Texture2D tex = mapPainter?.GetMapTexture();

        foreach (var tile in crackedTiles)
        {
            if (!roadGenerator.IsRoad(tile.x, tile.y)) continue;
            brokenRoadTiles.Add(tile);
            broken++;
            if (tex != null)
            {
                Color existing = tex.GetPixel(tile.x, tile.y);
                tex.SetPixel(tile.x, tile.y, Color.Lerp(existing, crackColor, 0.9f));
            }
        }

        if (broken > 0)
        {
            tex?.Apply();
            UndergroundMapManager.Instance?.RefreshSurfaceSprite();
            RoadTrafficSystem.Instance?.OnRoadsBreaking(brokenRoadTiles);
            Debug.Log($"EarthquakeSystem: {broken} road tile(s) broken.");
        }
    }

    public bool IsRoadBroken(int x, int y) => brokenRoadTiles.Contains(new Vector2Int(x, y));

    // =========================================================================
    // EPICENTER SELECTION
    // =========================================================================

    Vector2Int SampleWeightedEpicenter(FaultLineGenerator faultGen)
    {
        float[,] weightMap = faultGen.GetFaultWeightMap();
        int w = mapGenerator.width, h = mapGenerator.height;

        var tiles   = new List<Vector2Int>();
        var weights = new List<float>();
        float total = 0f;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!faultGen.IsFault(x, y)) continue;
            float wt = Mathf.Max(0.01f, weightMap[x, y]);
            tiles.Add(new Vector2Int(x, y));
            weights.Add(wt);
            total += wt;
        }

        if (tiles.Count == 0) return new Vector2Int(w / 2, h / 2);

        float roll = UnityEngine.Random.Range(0f, total), cumulative = 0f;
        for (int i = 0; i < tiles.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative) return tiles[i];
        }
        return tiles[tiles.Count - 1];
    }

    // =========================================================================
    // RADIUS
    // =========================================================================

    int CalculateRadius(Vector2Int epicenter, FaultLineGenerator faultGen)
    {
        var nodes = faultGen.NodePoints;
        if (nodes == null || nodes.Count == 0) return minRadius;

        float minDist = float.MaxValue;
        foreach (var node in nodes)
        {
            float d = Vector2Int.Distance(epicenter, node);
            if (d < minDist) minDist = d;
        }

        float t = 1f - Mathf.Clamp01(minDist / nodeInfluenceRange);
        return Mathf.RoundToInt(Mathf.Lerp(minRadius, maxRadius, t));
    }

    // =========================================================================
    // DEBUG BUTTON
    // =========================================================================

    void CreateDebugButton()
    {
        var btnObj = new GameObject("DEBUG_ForceEarthquake");
        btnObj.transform.SetParent(debugCanvas.transform, false);

        var rect              = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta        = new Vector2(220f, 50f);
        rect.anchorMin        = new Vector2(0f, 0f);
        rect.anchorMax        = new Vector2(0f, 0f);
        rect.pivot            = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(20f, 20f);

        var img   = btnObj.AddComponent<Image>();
        img.color = new Color(0.75f, 0.15f, 0.10f, 0.92f);

        var btn             = btnObj.AddComponent<Button>();
        var cb              = btn.colors;
        cb.normalColor      = new Color(0.75f, 0.15f, 0.10f, 0.92f);
        cb.highlightedColor = new Color(0.90f, 0.25f, 0.15f, 1.00f);
        cb.pressedColor     = new Color(0.50f, 0.08f, 0.05f, 1.00f);
        btn.colors          = cb;
        btn.onClick.AddListener(TriggerEarthquake);

        var textObj        = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect       = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var label       = textObj.AddComponent<Text>();
        label.text      = "⚠ Force Earthquake";
        label.color     = Color.white;
        label.fontSize  = 16;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        if (debugButtonFont != null) label.font = debugButtonFont;
    }
}