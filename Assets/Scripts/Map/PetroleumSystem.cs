using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// RESEARCH FLOW:
///   1. EnterResearchMode()
///   2. Player clicks map → circle center placed
///   3. Player drags → circle radius adjusts, cost shown live
///   4. Player releases → circle stays, Confirm button visible
///   5. Not happy? Click again → old circle replaced with new one
///   6. Confirm → scan happens, fires OnResearchDone
///   7. Cancel → no scan, fires OnModeCancelled
///
/// PUMP FLOW:
///   1. EnterPumpMode()
///   2. Player clicks map → pump placed (mode stays active, can place more)
///   3. Accept → confirms all session pumps, fires OnPumpsDone
///   4. Cancel → removes session pumps, fires OnModeCancelled
/// </summary>
public class PetroleumSystem : MonoBehaviour
{
    public static PetroleumSystem Instance { get; private set; }

    [Header("References")]
    public MapGenerator mapGenerator;
    public MapPainter   mapPainter;
    public Camera       mainCamera;

    [Header("Research")]
    [Range(5, 30)]  public int minScanRadius    = 8;
    [Range(10, 80)] public int maxScanRadius     = 40;
    [Range(5, 30)]  public int defaultScanRadius = 15;
    [Tooltip("Cost multiplier. Final cost = r³ × this value.")]
    public float researchCostPerTile = 0.1f;

    [Header("Research Circle Preview")]
    public Color circleColor    = new Color(1f, 1f, 0f, 0.8f);
    [Range(0.01f, 0.1f)] public float circleWidth = 0.03f;
    public int circleSegments   = 64;

    [Header("Highlight")]
    public Color highlightColor = new Color(0.15f, 0.85f, 0.3f, 0.45f);
    public float pixelsPerUnit  = 100f;

    [Header("Pump")]
    public float pumpPlacementCost     = 200f;
    public float baseIncomePerSecond   = 10f;
    [Range(0f, 0.05f)] public float offBedTrickle = 0.02f;
    [Range(0.1f, 2f)]  public float minPumpSpacing = 0.4f;
    public Sprite pumpSprite;
    public float  pumpSpriteZ = -0.6f;
    public float  pumpScale   = 1f;

    [Header("Debug")]
    public bool debugRevealBeds = true;

    // =========================================================================
    // MODE
    // =========================================================================

    public enum Mode { None, Research, Pump }
    public Mode CurrentMode => currentMode;
    private Mode currentMode = Mode.None;

    // =========================================================================
    // EVENTS
    // =========================================================================

    public static event Action        OnResearchDone;
    public static event Action        OnPumpsDone;
    public static event Action        OnModeCancelled;
    /// <summary>Fires every frame while dragging research circle. Float = current cost.</summary>
    public static event Action<float> OnResearchCostChanged;
    /// <summary>Fires when research circle is placed (drag released). Player can now confirm.</summary>
    public static event Action        OnResearchCircleReady;
    /// <summary>Fires when player can't afford an action.</summary>
    public static event Action<float> OnInsufficientFunds;

    // =========================================================================
    // RESEARCH STATE
    // =========================================================================

    private bool       hasCircle;          // a circle is placed and waiting for confirm
    private bool       isDragging;         // mouse held, adjusting radius
    private Vector2Int researchCenter;
    private int        researchRadius;
    private Vector2    dragStartScreen;

    private GameObject   circleGO;
    private LineRenderer circleLR;

    // =========================================================================
    // PUMP STATE
    // =========================================================================

    private List<FuelPump>   sessionPumps   = new List<FuelPump>();
    private List<GameObject> sessionPumpGOs = new List<GameObject>();
    private List<FuelPump>   allPumps       = new List<FuelPump>();
    private List<GameObject> allPumpGOs     = new List<GameObject>();

    // =========================================================================
    // HIGHLIGHT
    // =========================================================================

    private HashSet<int>           revealedPixels = new HashSet<int>();
    private Dictionary<int, Color> originalColors = new Dictionary<int, Color>();
    private Texture2D              mapTexture;

    private PetroleumBedGenerator bedGen;

    // =========================================================================
    // DATA
    // =========================================================================

    [Serializable]
    public class FuelPump
    {
        public Vector2Int tilePos;
        public Vector3    worldPos;
        public float      tilePurity;
        public float      incomePerSecond;
        public bool       onBed;
        [NonSerialized] public float totalEarned;
    }

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  { PetroleumBedGenerator.OnPetroleumGenerated += OnBedsGenerated; }
    void OnDisable() { PetroleumBedGenerator.OnPetroleumGenerated -= OnBedsGenerated; }

    void OnBedsGenerated()
    {
        if (debugRevealBeds) StartCoroutine(DebugRevealNextFrame());
    }

    IEnumerator DebugRevealNextFrame()
    {
        yield return null;
        RevealAllBeds();
    }

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        BuildCircle();
    }

    void Update()
    {
        if (bedGen == null) bedGen = PetroleumBedGenerator.Instance;

        switch (currentMode)
        {
            case Mode.Research: UpdateResearch(); break;
            case Mode.Pump:     UpdatePump();     break;
        }

        AccumulateIncome();
    }

    // =========================================================================
    // MODE CONTROL
    // =========================================================================

    public void EnterResearchMode()
    {
        currentMode = Mode.Research;
        hasCircle   = false;
        isDragging  = false;
        ShowCircle(false);
    }

    public void EnterPumpMode()
    {
        currentMode = Mode.Pump;
        sessionPumps.Clear();
        sessionPumpGOs.Clear();
    }

    public void CancelMode()
    {
        if (currentMode == Mode.Research)
        {
            ShowCircle(false);
        }
        else if (currentMode == Mode.Pump)
        {
            // Refund cost for each session pump
            if (GameStatManager.Instance != null && sessionPumps.Count > 0)
                GameStatManager.Instance.AddWealth(sessionPumps.Count * pumpPlacementCost);

            foreach (var go in sessionPumpGOs)
                if (go != null) Destroy(go);
            foreach (var p in sessionPumps)
                allPumps.Remove(p);
            foreach (var go in sessionPumpGOs)
                allPumpGOs.Remove(go);
            sessionPumps.Clear();
            sessionPumpGOs.Clear();
        }

        currentMode = Mode.None;
        OnModeCancelled?.Invoke();
    }

    /// <summary>Confirm research scan at current circle position/radius. Costs money.</summary>
    public void ConfirmResearch()
    {
        if (currentMode != Mode.Research || !hasCircle) return;

        float cost = GetCurrentResearchCost();

        if (GameStatManager.Instance != null)
        {
            if (!GameStatManager.Instance.TrySpendWealth(cost))
            {
                Debug.Log($"Araştırma için yeterli para yok. Gerekli: {cost:F0}");
                OnInsufficientFunds?.Invoke(cost);
                return; // don't end mode — let player resize or cancel
            }
        }

        PerformResearchScan(researchCenter, researchRadius);
        ShowCircle(false);
        currentMode = Mode.None;
        OnResearchDone?.Invoke();
    }

    /// <summary>Accept all pumps placed this session.</summary>
    public void AcceptPumps()
    {
        if (currentMode != Mode.Pump) return;
        sessionPumps.Clear();
        sessionPumpGOs.Clear();
        currentMode = Mode.None;
        OnPumpsDone?.Invoke();
    }

    /// <summary>Research cost = r³ × costMultiplier.</summary>
    public float GetCurrentResearchCost()
    {
        return researchRadius * researchRadius * researchRadius * researchCostPerTile;
    }

    // =========================================================================
    // RESEARCH UPDATE
    // =========================================================================

    void UpdateResearch()
    {
        if (bedGen == null || !bedGen.IsGenerated) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (IsPointerOverUI()) return;

        // Click → start new circle (replaces old one if any)
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2Int tile = ScreenToTile(mouse.position.ReadValue());
            if (tile.x >= 0)
            {
                researchCenter   = tile;
                researchRadius   = defaultScanRadius;
                isDragging       = true;
                hasCircle        = true;
                dragStartScreen  = mouse.position.ReadValue();
                ShowCircle(true);
                UpdateCircleShape();
            }
        }

        // Dragging → adjust radius
        if (isDragging)
        {
            Vector2 cur  = mouse.position.ReadValue();
            float dist   = Vector2.Distance(cur, dragStartScreen);
            researchRadius = Mathf.Clamp(
                defaultScanRadius + Mathf.RoundToInt(dist * 0.15f),
                minScanRadius, maxScanRadius);

            UpdateCircleShape();
            OnResearchCostChanged?.Invoke(GetCurrentResearchCost());
        }

        // Release → circle stays, player can now confirm or re-click
        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            OnResearchCircleReady?.Invoke();
        }
    }

    // =========================================================================
    // PUMP UPDATE
    // =========================================================================

    void UpdatePump()
    {
        if (bedGen == null || !bedGen.IsGenerated) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (IsPointerOverUI()) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2Int tile = ScreenToTile(mouse.position.ReadValue());
            if (tile.x >= 0 && mapGenerator.IsLand(tile.x, tile.y))
                PlacePump(tile);
        }
    }

    // =========================================================================
    // RESEARCH SCAN
    // =========================================================================

    public void PerformResearchScan(Vector2Int center, int radius)
    {
        if (bedGen == null || !bedGen.IsGenerated) return;
        List<Vector2Int> found = bedGen.GetPetroleumTilesInCircle(center, radius);
        if (found.Count > 0) HighlightTiles(found);
        Debug.Log($"Araştırma: ({center.x},{center.y}) R={radius} → {found.Count} petrol karesi");
    }

    public void RevealAllBeds()
    {
        if (bedGen == null || !bedGen.IsGenerated) return;
        int w = mapGenerator.width, h = mapGenerator.height;
        List<Vector2Int> all = new List<Vector2Int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (bedGen.HasPetroleum(x, y))
                    all.Add(new Vector2Int(x, y));
        HighlightTiles(all);
    }

    // =========================================================================
    // PUMP PLACEMENT
    // =========================================================================

    public FuelPump PlacePump(Vector2Int tilePos)
    {
        if (bedGen == null || !bedGen.IsGenerated) return null;

        // Check if player can afford it
        if (GameStatManager.Instance != null)
        {
            if (!GameStatManager.Instance.TrySpendWealth(pumpPlacementCost))
            {
                Debug.Log($"Pompa için yeterli para yok. Gerekli: {pumpPlacementCost:F0}");
                OnInsufficientFunds?.Invoke(pumpPlacementCost);
                return null;
            }
        }

        Vector3 worldPos = TileToWorld(tilePos);
        worldPos.z = pumpSpriteZ;

        foreach (var ex in allPumps)
            if (Vector2.Distance(
                    new Vector2(worldPos.x, worldPos.y),
                    new Vector2(ex.worldPos.x, ex.worldPos.y)) < minPumpSpacing)
            { Debug.Log("Pompa çok yakın."); return null; }

        float purity = bedGen.GetPurity(tilePos.x, tilePos.y);
        bool  onBed  = purity > 0f;
        float income = onBed
            ? baseIncomePerSecond * purity
            : baseIncomePerSecond * offBedTrickle * UnityEngine.Random.Range(0.5f, 1.5f);

        FuelPump pump = new FuelPump
        {
            tilePos = tilePos, worldPos = worldPos,
            tilePurity = purity, incomePerSecond = income,
            onBed = onBed, totalEarned = 0f
        };

        allPumps.Add(pump);
        GameObject go = SpawnPumpVisual(pump);
        allPumpGOs.Add(go);
        sessionPumps.Add(pump);
        sessionPumpGOs.Add(go);

        Debug.Log($"Pompa: ({tilePos.x},{tilePos.y}) Saflık={purity:F3} Gelir/sn={income:F2}");
        return pump;
    }

    public void RemovePump(int index)
    {
        if (index < 0 || index >= allPumps.Count) return;
        if (index < allPumpGOs.Count && allPumpGOs[index] != null)
            Destroy(allPumpGOs[index]);
        allPumps.RemoveAt(index);
        if (index < allPumpGOs.Count) allPumpGOs.RemoveAt(index);
    }

    GameObject SpawnPumpVisual(FuelPump pump)
    {
        GameObject go = new GameObject("FuelPump");
        go.transform.SetParent(transform);
        go.transform.position   = pump.worldPos;
        go.transform.localScale = new Vector3(pumpScale, pumpScale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 100;
        if (pumpSprite != null) { sr.sprite = pumpSprite; }
        else
        {
            Texture2D tex = new Texture2D(4, 4);
            Color col = pump.onBed ? Color.green : Color.red;
            for (int i = 0; i < 16; i++) tex.SetPixel(i % 4, i / 4, col);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        }
        return go;
    }

    // =========================================================================
    // INCOME
    // =========================================================================

    void AccumulateIncome()
    {
        if (allPumps.Count == 0) return;

        float dt = Time.deltaTime;
        float totalThisTick = 0f;

        foreach (var p in allPumps)
        {
            float earned = p.incomePerSecond * dt;
            p.totalEarned += earned;
            totalThisTick += earned;
        }

        // Feed into the actual wealth system
        if (totalThisTick > 0f && GameStatManager.Instance != null)
            GameStatManager.Instance.AddWealth(totalThisTick);
    }

    public float GetTotalIncomePerSecond()
    {
        float t = 0f; foreach (var p in allPumps) t += p.incomePerSecond; return t;
    }

    public float GetTotalEarned()
    {
        float t = 0f; foreach (var p in allPumps) t += p.totalEarned; return t;
    }

    public IReadOnlyList<FuelPump> GetPumps() => allPumps.AsReadOnly();

    // =========================================================================
    // HIGHLIGHTING
    // =========================================================================

    void HighlightTiles(List<Vector2Int> tiles)
    {
        EnsureMapTexture();
        if (mapTexture == null) return;

        foreach (var t in tiles)
        {
            int key = t.x + t.y * mapGenerator.width;
            if (revealedPixels.Contains(key)) continue;
            revealedPixels.Add(key);

            if (!originalColors.ContainsKey(key))
                originalColors[key] = mapTexture.GetPixel(t.x, t.y);

            Color orig = originalColors[key];
            float pur  = bedGen.GetPurity(t.x, t.y);
            float a    = Mathf.Lerp(highlightColor.a * 0.4f, highlightColor.a, pur);
            Color tint = new Color(highlightColor.r, highlightColor.g, highlightColor.b, a);
            Color c    = Color.Lerp(orig, tint, tint.a);
            c.a = 1f;
            mapTexture.SetPixel(t.x, t.y, c);
        }
        mapTexture.Apply();
    }

    public void ClearHighlights()
    {
        EnsureMapTexture();
        if (mapTexture == null) return;
        foreach (var kvp in originalColors)
        {
            int x = kvp.Key % mapGenerator.width;
            int y = kvp.Key / mapGenerator.width;
            mapTexture.SetPixel(x, y, kvp.Value);
        }
        mapTexture.Apply();
        revealedPixels.Clear();
        originalColors.Clear();
    }

    // =========================================================================
    // CIRCLE PREVIEW
    // =========================================================================

    void BuildCircle()
    {
        circleGO = new GameObject("ResearchCircle");
        circleGO.transform.SetParent(transform);

        circleLR = circleGO.AddComponent<LineRenderer>();
        circleLR.useWorldSpace = true;
        circleLR.loop          = true;
        circleLR.positionCount = circleSegments;
        circleLR.startWidth    = circleWidth;
        circleLR.endWidth      = circleWidth;
        circleLR.sortingOrder  = 200;
        circleLR.startColor    = circleColor;
        circleLR.endColor      = circleColor;
        circleLR.material      = new Material(Shader.Find("Sprites/Default"));
        circleLR.material.color = circleColor;

        circleGO.SetActive(false);
    }

    void ShowCircle(bool show)
    {
        if (circleGO != null) circleGO.SetActive(show);
    }

    void UpdateCircleShape()
    {
        if (circleLR == null) return;

        Vector3 center = TileToWorld(researchCenter);
        center.z = -1f;
        float worldR = researchRadius / pixelsPerUnit;

        for (int i = 0; i < circleSegments; i++)
        {
            float ang = (i / (float)circleSegments) * Mathf.PI * 2f;
            circleLR.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(ang) * worldR,
                center.y + Mathf.Sin(ang) * worldR,
                center.z));
        }
    }

    // =========================================================================
    // COORDINATES
    // =========================================================================

    public Vector2Int ScreenToTile(Vector2 screenPos)
    {
        if (mainCamera == null) return new Vector2Int(-1, -1);

        Vector3 wp = mainCamera.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, mainCamera.nearClipPlane + 1f));

        Vector3 origin = GetMapOrigin();
        float halfW = mapGenerator.width  * 0.5f / pixelsPerUnit;
        float halfH = mapGenerator.height * 0.5f / pixelsPerUnit;

        int tx = Mathf.FloorToInt((wp.x - origin.x + halfW) * pixelsPerUnit);
        int ty = Mathf.FloorToInt((wp.y - origin.y + halfH) * pixelsPerUnit);

        if (tx < 0 || tx >= mapGenerator.width || ty < 0 || ty >= mapGenerator.height)
            return new Vector2Int(-1, -1);
        return new Vector2Int(tx, ty);
    }

    public Vector3 TileToWorld(Vector2Int tile)
    {
        Vector3 o = GetMapOrigin();
        float halfW = mapGenerator.width  * 0.5f / pixelsPerUnit;
        float halfH = mapGenerator.height * 0.5f / pixelsPerUnit;
        return new Vector3(
            o.x + (tile.x / pixelsPerUnit) - halfW,
            o.y + (tile.y / pixelsPerUnit) - halfH, 0f);
    }

    Vector3 GetMapOrigin()
    {
        if (mapPainter != null && mapPainter.mapRenderer != null)
            return mapPainter.mapRenderer.transform.position;
        return Vector3.zero;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    void EnsureMapTexture()
    {
        if (mapTexture != null) return;
        if (mapPainter != null && mapPainter.mapRenderer != null &&
            mapPainter.mapRenderer.sprite != null)
            mapTexture = mapPainter.mapRenderer.sprite.texture;
    }

    public void Clear()
    {
        ClearHighlights();
        ShowCircle(false);
        foreach (var go in allPumpGOs) if (go != null) Destroy(go);
        allPumpGOs.Clear(); allPumps.Clear();
        sessionPumps.Clear(); sessionPumpGOs.Clear();
    }
}