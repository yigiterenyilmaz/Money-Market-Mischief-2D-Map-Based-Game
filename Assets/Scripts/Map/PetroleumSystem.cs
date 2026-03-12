using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gameplay layer for petroleum: research scanning, visual highlighting,
/// pump placement, and income calculation.
/// Depends on PetroleumBedGenerator for underlying data.
/// </summary>
public class PetroleumSystem : MonoBehaviour
{
    public static PetroleumSystem Instance { get; private set; }

    // -------------------------------------------------------------------------
    // CONFIGURATION
    // -------------------------------------------------------------------------

    [Header("References")]
    public MapGenerator mapGenerator;
    public MapPainter   mapPainter;
    public Camera       mainCamera;

    [Header("Research Settings")]
    [Tooltip("Minimum scan circle radius in tiles.")]
    [Range(5, 30)] public int minScanRadius = 8;

    [Tooltip("Maximum scan circle radius in tiles.")]
    [Range(10, 80)] public int maxScanRadius = 40;

    [Tooltip("Default scan radius when the player starts dragging.")]
    [Range(5, 30)] public int defaultScanRadius = 15;

    [Header("Highlight Appearance")]
    [Tooltip("Color overlaid on discovered petroleum tiles.")]
    public Color highlightColor = new Color(0.15f, 0.85f, 0.3f, 0.45f);

    [Tooltip("Pixels per world unit (must match MapPainter).")]
    public float pixelsPerUnit = 100f;

    [Header("Pump Settings")]
    [Tooltip("Base income per second for a pump sitting on max-purity petroleum.")]
    public float baseIncomePerSecond = 10f;

    [Tooltip("Trickle income multiplier for pumps placed off-bed (fraction of base).")]
    [Range(0f, 0.05f)] public float offBedTrickle = 0.02f;

    [Tooltip("World-space minimum distance between two pumps.")]
    [Range(0.1f, 2f)] public float minPumpSpacing = 0.4f;

    [Header("Pump Visuals")]
    [Tooltip("Sprite used for placed pumps. Assign in Inspector.")]
    public Sprite pumpSprite;

    [Tooltip("Z depth for pump sprites.")]
    public float pumpSpriteZ = -0.6f;

    [Tooltip("Scale of the pump sprite.")]
    public float pumpScale = 1f;

    // -------------------------------------------------------------------------
    // INTERACTION MODE
    // -------------------------------------------------------------------------

    public enum InteractionMode { None, Research, PlacePump }

    [Header("Current Mode")]
    public InteractionMode currentMode = InteractionMode.None;

    // -------------------------------------------------------------------------
    // EVENTS
    // -------------------------------------------------------------------------

    /// <summary>Fired after a research scan reveals tiles. Passes revealed tile count.</summary>
    public static event Action<int> OnResearchComplete;

    /// <summary>Fired when a pump is placed. Passes the pump data.</summary>
    public static event Action<FuelPump> OnPumpPlaced;

    // -------------------------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------------------------

    private PetroleumBedGenerator bedGen;

    // Discovered tiles — persists across scans so previously revealed areas stay visible
    private HashSet<int> revealedPixels = new HashSet<int>();

    // Texture overlay tracking — we store original colors to allow clean repaints
    private Dictionary<int, Color> originalColors = new Dictionary<int, Color>();

    // Research preview state
    private bool    isDragging    = false;
    private Vector2Int dragCenter = Vector2Int.zero;
    private int     dragRadius    = 0;

    // Pumps
    private List<FuelPump>    pumps       = new List<FuelPump>();
    private List<GameObject>  pumpObjects = new List<GameObject>();

    // Cached map texture reference (from MapPainter via reflection or public access)
    private Texture2D mapTexture;

    // -------------------------------------------------------------------------
    // DATA STRUCTURES
    // -------------------------------------------------------------------------

    [Serializable]
    public class FuelPump
    {
        public Vector2Int tilePos;
        public Vector3    worldPos;
        public float      tilePurity;       // purity at the exact tile
        public float      incomePerSecond;  // computed income rate
        public bool       onBed;

        /// <summary>Total money earned by this pump since placement.</summary>
        [NonSerialized] public float totalEarned;
    }

    // -------------------------------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        bedGen = PetroleumBedGenerator.Instance;
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        switch (currentMode)
        {
            case InteractionMode.Research:  HandleResearchInput();  break;
            case InteractionMode.PlacePump: HandlePumpInput();      break;
        }

        AccumulatePumpIncome();
    }

    // -------------------------------------------------------------------------
    // MODE SWITCHING
    // -------------------------------------------------------------------------

    /// <summary>Switch to research scan mode.</summary>
    public void EnterResearchMode()  => currentMode = InteractionMode.Research;

    /// <summary>Switch to pump placement mode.</summary>
    public void EnterPumpMode()      => currentMode = InteractionMode.PlacePump;

    /// <summary>Exit any active mode.</summary>
    public void ExitMode()           => currentMode = InteractionMode.None;

    // -------------------------------------------------------------------------
    // RESEARCH — INPUT
    // -------------------------------------------------------------------------

    private void HandleResearchInput()
    {
        if (bedGen == null || !bedGen.IsGenerated) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // Click to scan at point with default radius
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2Int tile = ScreenToTile(mouse.position.ReadValue());
            if (tile.x >= 0)
            {
                isDragging = true;
                dragCenter = tile;
                dragRadius = defaultScanRadius;
            }
        }

        // Drag to adjust radius via scroll wheel while held
        if (isDragging)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                dragRadius += scroll > 0 ? 2 : -2;
                dragRadius = Mathf.Clamp(dragRadius, minScanRadius, maxScanRadius);
            }
        }

        // Release to confirm scan
        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            PerformResearchScan(dragCenter, dragRadius);
        }
    }

    // -------------------------------------------------------------------------
    // RESEARCH — SCAN LOGIC
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans a circular area and reveals any petroleum tiles found.
    /// Can be called from UI or programmatically.
    /// </summary>
    public void PerformResearchScan(Vector2Int center, int radius)
    {
        if (bedGen == null || !bedGen.IsGenerated) return;

        List<Vector2Int> found = bedGen.GetPetroleumTilesInCircle(center, radius);

        if (found.Count > 0)
            HighlightTiles(found);

        OnResearchComplete?.Invoke(found.Count);

        Debug.Log($"Araştırma: Merkez=({center.x},{center.y}) R={radius} → " +
                  $"{found.Count} petrol karesi bulundu");
    }

    /// <summary>
    /// Reveals petroleum tiles without a research action (e.g., cheat/debug).
    /// </summary>
    public void RevealAllBeds()
    {
        if (bedGen == null || !bedGen.IsGenerated) return;

        int w = mapGenerator.width;
        int h = mapGenerator.height;
        List<Vector2Int> allTiles = new List<Vector2Int>();

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (bedGen.HasPetroleum(x, y))
                    allTiles.Add(new Vector2Int(x, y));

        HighlightTiles(allTiles);
        Debug.Log($"Tüm petrol yatakları gösterildi: {allTiles.Count} karo");
    }

    // -------------------------------------------------------------------------
    // HIGHLIGHTING
    // -------------------------------------------------------------------------

    private void HighlightTiles(List<Vector2Int> tiles)
    {
        EnsureMapTexture();
        if (mapTexture == null) return;

        foreach (var t in tiles)
        {
            int key = t.x + t.y * mapGenerator.width;
            if (revealedPixels.Contains(key)) continue;

            revealedPixels.Add(key);

            // Store original color for potential un-highlight later
            if (!originalColors.ContainsKey(key))
                originalColors[key] = mapTexture.GetPixel(t.x, t.y);

            // Blend highlight over the existing color
            Color original = originalColors[key];

            // Scale highlight opacity with purity — richer areas glow brighter
            float purity = bedGen.GetPurity(t.x, t.y);
            float alpha  = Mathf.Lerp(highlightColor.a * 0.4f, highlightColor.a, purity);
            Color tinted  = new Color(highlightColor.r, highlightColor.g, highlightColor.b, alpha);
            Color blended = Color.Lerp(original, tinted, tinted.a);
            blended.a = 1f;

            mapTexture.SetPixel(t.x, t.y, blended);
        }

        mapTexture.Apply();
    }

    /// <summary>Clears all revealed highlights and restores original colors.</summary>
    public void ClearHighlights()
    {
        EnsureMapTexture();
        if (mapTexture == null) return;

        foreach (var kvp in originalColors)
        {
            int key = kvp.Key;
            int x   = key % mapGenerator.width;
            int y   = key / mapGenerator.width;
            mapTexture.SetPixel(x, y, kvp.Value);
        }

        mapTexture.Apply();
        revealedPixels.Clear();
        originalColors.Clear();
    }

    // -------------------------------------------------------------------------
    // PUMP — INPUT
    // -------------------------------------------------------------------------

    private void HandlePumpInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2Int tile = ScreenToTile(mouse.position.ReadValue());
            if (tile.x >= 0 && mapGenerator.IsLand(tile.x, tile.y))
                PlacePump(tile);
        }
    }

    // -------------------------------------------------------------------------
    // PUMP — PLACEMENT
    // -------------------------------------------------------------------------

    /// <summary>
    /// Places a fuel pump at the given tile position.
    /// Returns the created pump, or null if placement failed.
    /// </summary>
    public FuelPump PlacePump(Vector2Int tilePos)
    {
        if (bedGen == null || !bedGen.IsGenerated) return null;

        float halfW = mapGenerator.width  * 0.5f / pixelsPerUnit;
        float halfH = mapGenerator.height * 0.5f / pixelsPerUnit;

        float wx = (tilePos.x / pixelsPerUnit) - halfW;
        float wy = (tilePos.y / pixelsPerUnit) - halfH;

        // Check spacing
        foreach (var existing in pumps)
        {
            if (Vector2.Distance(new Vector2(wx, wy), new Vector2(existing.worldPos.x, existing.worldPos.y)) < minPumpSpacing)
            {
                Debug.Log("Pompa çok yakın — yerleştirilemedi.");
                return null;
            }
        }

        float purity = bedGen.GetPurity(tilePos.x, tilePos.y);
        bool  onBed  = purity > 0f;

        float income;
        if (onBed)
            income = baseIncomePerSecond * purity;
        else
            income = baseIncomePerSecond * offBedTrickle * UnityEngine.Random.Range(0.5f, 1.5f);

        FuelPump pump = new FuelPump
        {
            tilePos        = tilePos,
            worldPos       = new Vector3(wx, wy, pumpSpriteZ),
            tilePurity     = purity,
            incomePerSecond = income,
            onBed          = onBed,
            totalEarned    = 0f
        };

        pumps.Add(pump);
        SpawnPumpVisual(pump);
        OnPumpPlaced?.Invoke(pump);

        Debug.Log($"Pompa yerleştirildi: ({tilePos.x},{tilePos.y}) " +
                  $"Saflık={purity:F3} Gelir/sn={income:F2} YatakÜstü={onBed}");

        return pump;
    }

    /// <summary>Remove a pump by index.</summary>
    public void RemovePump(int index)
    {
        if (index < 0 || index >= pumps.Count) return;

        if (index < pumpObjects.Count && pumpObjects[index] != null)
            Destroy(pumpObjects[index]);

        pumps.RemoveAt(index);
        if (index < pumpObjects.Count) pumpObjects.RemoveAt(index);
    }

    // -------------------------------------------------------------------------
    // PUMP — INCOME
    // -------------------------------------------------------------------------

    private void AccumulatePumpIncome()
    {
        float dt = Time.deltaTime;
        foreach (var pump in pumps)
            pump.totalEarned += pump.incomePerSecond * dt;
    }

    /// <summary>Total income per second across all pumps.</summary>
    public float GetTotalIncomePerSecond()
    {
        float total = 0f;
        foreach (var pump in pumps)
            total += pump.incomePerSecond;
        return total;
    }

    /// <summary>Total money earned by all pumps since their placement.</summary>
    public float GetTotalEarned()
    {
        float total = 0f;
        foreach (var pump in pumps)
            total += pump.totalEarned;
        return total;
    }

    /// <summary>All placed pumps (read-only).</summary>
    public IReadOnlyList<FuelPump> GetPumps() => pumps.AsReadOnly();

    // -------------------------------------------------------------------------
    // PUMP — VISUALS
    // -------------------------------------------------------------------------

    private void SpawnPumpVisual(FuelPump pump)
    {
        GameObject go = new GameObject("FuelPump");
        go.transform.SetParent(transform);
        go.transform.position   = pump.worldPos;
        go.transform.localScale = new Vector3(pumpScale, pumpScale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 100;

        if (pumpSprite != null)
            sr.sprite = pumpSprite;
        else
        {
            // Fallback: tiny colored square so the pump is visible even without a sprite
            Texture2D tex = new Texture2D(4, 4);
            Color col = pump.onBed ? Color.green : Color.red;
            for (int i = 0; i < 16; i++) tex.SetPixel(i % 4, i / 4, col);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        }

        pumpObjects.Add(go);
    }

    // -------------------------------------------------------------------------
    // COORDINATE HELPERS
    // -------------------------------------------------------------------------

    /// <summary>Converts screen position to map tile coordinates. Returns (-1,-1) if off-map.</summary>
    public Vector2Int ScreenToTile(Vector2 screenPos)
    {
        if (mainCamera == null) return new Vector2Int(-1, -1);

        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));

        float halfW = mapGenerator.width  * 0.5f / pixelsPerUnit;
        float halfH = mapGenerator.height * 0.5f / pixelsPerUnit;

        int tx = Mathf.FloorToInt((worldPos.x + halfW) * pixelsPerUnit);
        int ty = Mathf.FloorToInt((worldPos.y + halfH) * pixelsPerUnit);

        if (tx < 0 || tx >= mapGenerator.width || ty < 0 || ty >= mapGenerator.height)
            return new Vector2Int(-1, -1);

        return new Vector2Int(tx, ty);
    }

    /// <summary>Converts tile coordinates to world position.</summary>
    public Vector3 TileToWorld(Vector2Int tile)
    {
        float halfW = mapGenerator.width  * 0.5f / pixelsPerUnit;
        float halfH = mapGenerator.height * 0.5f / pixelsPerUnit;

        float wx = (tile.x / pixelsPerUnit) - halfW;
        float wy = (tile.y / pixelsPerUnit) - halfH;
        return new Vector3(wx, wy, 0f);
    }

    // -------------------------------------------------------------------------
    // MAP TEXTURE ACCESS
    // -------------------------------------------------------------------------

    private void EnsureMapTexture()
    {
        if (mapTexture != null) return;

        // MapPainter's SpriteRenderer holds the texture
        if (mapPainter != null && mapPainter.mapRenderer != null &&
            mapPainter.mapRenderer.sprite != null)
        {
            mapTexture = mapPainter.mapRenderer.sprite.texture;
        }
    }

    // -------------------------------------------------------------------------
    // CLEANUP
    // -------------------------------------------------------------------------

    public void Clear()
    {
        ClearHighlights();

        foreach (var go in pumpObjects)
            if (go != null) Destroy(go);

        pumpObjects.Clear();
        pumps.Clear();
    }
}