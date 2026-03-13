using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PetroleumSystem : MonoBehaviour
{
    public static PetroleumSystem Instance { get; private set; }

    [Header("References")]
    public MapGenerator mapGenerator;
    public MapPainter   mapPainter;
    public Camera       mainCamera;

    [Header("Research")]
    [Range(5, 30)]  public int minScanRadius     = 8;
    [Range(10, 80)] public int maxScanRadius      = 40;
    [Range(5, 30)]  public int defaultScanRadius  = 15;
    [Tooltip("Cost = r^2.2 × this.")]
    public float researchCostPerTile = 0.5f;

    [Header("Research Timer")]
    [Tooltip("Fixed duration in seconds for any research scan.")]
    public float researchDuration = 5f;
    [Tooltip("Skip timer entirely for testing.")]
    public bool bypassResearchTimer = false;

    [Header("Research Circle Preview")]
    public Color circleColor  = new Color(1f, 1f, 0f, 0.8f);
    [Range(0.01f, 0.2f)] public float circleWidth = 0.06f;
    public int circleSegments = 64;

    [Header("Highlight")]
    public Color highlightColor = new Color(0.15f, 0.85f, 0.3f, 0.45f);
    public float pixelsPerUnit  = 100f;

    [Header("Pump")]
    public float pumpPlacementCost   = 200f;
    public float baseIncomePerSecond = 10f;
    [Range(0f, 0.05f)] public float offBedTrickle = 0.02f;
    [Range(0.1f, 2f)]  public float minPumpSpacing = 0.4f;
    public Sprite pumpSprite;
    public float  pumpSpriteZ = -0.6f;
    public float  pumpScale   = 1f;

    [Header("Debug")]
    public bool debugRevealBeds = true;

    public enum Mode { None, Research, Pump }
    public Mode CurrentMode => currentMode;
    private Mode currentMode = Mode.None;

    // Events
    public static event Action        OnResearchDone;
    public static event Action        OnPumpsDone;
    public static event Action        OnModeCancelled;
    public static event Action<float> OnResearchCostChanged;
    public static event Action        OnResearchCircleReady;
    public static event Action<float> OnInsufficientFunds;
    public static event Action<float> OnResearchTimerStarted;
    public static event Action<float> OnResearchTimerProgress;
    /// <summary>Fires after a pump is placed. Passes remaining wealth.</summary>
    public static event Action<float> OnPumpPlaced;

    // Research state
    private bool       hasCircle, isDragging, isTimerRunning;
    private float      timerElapsed, timerDuration;
    private Vector2Int researchCenter, confirmedCenter;
    private int        researchRadius, confirmedRadius;
    private Vector2    dragStartScreen;
    private GameObject circleGO;
    private LineRenderer circleLR;

    // Pump state
    private List<FuelPump>   sessionPumps   = new List<FuelPump>();
    private List<GameObject> sessionPumpGOs = new List<GameObject>();
    private List<FuelPump>   allPumps       = new List<FuelPump>();
    private List<GameObject> allPumpGOs     = new List<GameObject>();

    // Highlight
    private HashSet<int>           revealedPixels = new HashSet<int>();
    private Dictionary<int, Color> originalColors = new Dictionary<int, Color>();
    private Texture2D              mapTexture;
    private PetroleumBedGenerator  bedGen;

    [Serializable]
    public class FuelPump
    {
        public Vector2Int tilePos;
        public Vector3    worldPos;
        public float      tilePurity, incomePerSecond;
        public bool       onBed;
        [NonSerialized] public float totalEarned;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  { PetroleumBedGenerator.OnPetroleumGenerated += OnBedsGenerated; }
    void OnDisable() { PetroleumBedGenerator.OnPetroleumGenerated -= OnBedsGenerated; }

    void OnBedsGenerated() { if (debugRevealBeds) StartCoroutine(DebugRevealNextFrame()); }
    IEnumerator DebugRevealNextFrame() { yield return null; RevealAllBeds(); }

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

    // === MODE CONTROL ===

    public void EnterResearchMode()
    {
        currentMode = Mode.Research;
        hasCircle = isDragging = isTimerRunning = false;
        timerElapsed = 0f;
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
            isTimerRunning = false;
            ShowCircle(false);
        }
        else if (currentMode == Mode.Pump)
        {
            if (GameStatManager.Instance != null && sessionPumps.Count > 0)
                GameStatManager.Instance.AddWealth(sessionPumps.Count * pumpPlacementCost);
            foreach (var go in sessionPumpGOs) if (go != null) Destroy(go);
            foreach (var p in sessionPumps) allPumps.Remove(p);
            foreach (var go in sessionPumpGOs) allPumpGOs.Remove(go);
            sessionPumps.Clear();
            sessionPumpGOs.Clear();
        }
        currentMode = Mode.None;
        OnModeCancelled?.Invoke();
    }

    public void ConfirmResearch()
    {
        if (currentMode != Mode.Research || !hasCircle || isTimerRunning) return;

        float cost = GetCurrentResearchCost();
        if (GameStatManager.Instance != null && !GameStatManager.Instance.TrySpendWealth(cost))
        {
            OnInsufficientFunds?.Invoke(cost);
            return;
        }

        confirmedCenter = researchCenter;
        confirmedRadius = researchRadius;
        timerDuration = GetResearchDuration();

        if (bypassResearchTimer || timerDuration <= 0f)
        {
            PerformResearchScan(confirmedCenter, confirmedRadius);
            ShowCircle(false);
            currentMode = Mode.None;
            OnResearchDone?.Invoke();
        }
        else
        {
            isTimerRunning = true;
            timerElapsed = 0f;
            OnResearchTimerStarted?.Invoke(timerDuration);
        }
    }

    public void AcceptPumps()
    {
        if (currentMode != Mode.Pump) return;
        sessionPumps.Clear();
        sessionPumpGOs.Clear();
        currentMode = Mode.None;
        OnPumpsDone?.Invoke();
    }

    public float GetCurrentResearchCost() => Mathf.Pow(researchRadius, 2.2f) * researchCostPerTile;

    public float GetResearchDuration()
    {
        if (bypassResearchTimer) return 0f;
        return researchDuration;
    }

    // === RESEARCH UPDATE ===

    void UpdateResearch()
    {
        if (bedGen == null || !bedGen.IsGenerated) return;

        if (isTimerRunning)
        {
            timerElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(timerElapsed / timerDuration);
            OnResearchTimerProgress?.Invoke(progress);
            if (timerElapsed >= timerDuration)
            {
                isTimerRunning = false;
                PerformResearchScan(confirmedCenter, confirmedRadius);
                ShowCircle(false);
                currentMode = Mode.None;
                OnResearchDone?.Invoke();
            }
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || IsPointerOverUI()) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2Int tile = ScreenToTile(mouse.position.ReadValue());
            if (tile.x >= 0)
            {
                researchCenter = tile;
                researchRadius = defaultScanRadius;
                isDragging = hasCircle = true;
                dragStartScreen = mouse.position.ReadValue();
                ShowCircle(true);
                UpdateCircleShape();
            }
        }

        if (isDragging)
        {
            float dist = Vector2.Distance(mouse.position.ReadValue(), dragStartScreen);
            researchRadius = Mathf.Clamp(defaultScanRadius + Mathf.RoundToInt(dist * 0.15f), minScanRadius, maxScanRadius);
            UpdateCircleShape();
            OnResearchCostChanged?.Invoke(GetCurrentResearchCost());
        }

        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            OnResearchCircleReady?.Invoke();
        }
    }

    // === PUMP UPDATE ===

    void UpdatePump()
    {
        if (bedGen == null || !bedGen.IsGenerated) return;
        Mouse mouse = Mouse.current;
        if (mouse == null || IsPointerOverUI()) return;
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2Int tile = ScreenToTile(mouse.position.ReadValue());
            if (tile.x >= 0 && mapGenerator.IsLand(tile.x, tile.y))
                PlacePump(tile);
        }
    }

    // === SCAN ===

    public void PerformResearchScan(Vector2Int center, int radius)
    {
        if (bedGen == null || !bedGen.IsGenerated) return;
        var found = bedGen.GetPetroleumTilesInCircle(center, radius);
        if (found.Count > 0) HighlightTiles(found);
        Debug.Log($"Araştırma: ({center.x},{center.y}) R={radius} → {found.Count} petrol karesi");
    }

    public void RevealAllBeds()
    {
        if (bedGen == null || !bedGen.IsGenerated) return;
        int w = mapGenerator.width, h = mapGenerator.height;
        var all = new List<Vector2Int>();
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) if (bedGen.HasPetroleum(x, y)) all.Add(new Vector2Int(x, y));
        HighlightTiles(all);
    }

    // === PUMP PLACEMENT ===

    public FuelPump PlacePump(Vector2Int tilePos)
    {
        if (bedGen == null || !bedGen.IsGenerated) return null;
        if (GameStatManager.Instance != null && !GameStatManager.Instance.TrySpendWealth(pumpPlacementCost))
        { OnInsufficientFunds?.Invoke(pumpPlacementCost); return null; }

        Vector3 wp = TileToWorld(tilePos); wp.z = pumpSpriteZ;
        foreach (var ex in allPumps)
            if (Vector2.Distance(new Vector2(wp.x, wp.y), new Vector2(ex.worldPos.x, ex.worldPos.y)) < minPumpSpacing)
                return null;

        float purity = bedGen.GetPurity(tilePos.x, tilePos.y);
        bool onBed = purity > 0f;
        float income = onBed ? baseIncomePerSecond * purity : baseIncomePerSecond * offBedTrickle * UnityEngine.Random.Range(0.5f, 1.5f);

        FuelPump pump = new FuelPump { tilePos = tilePos, worldPos = wp, tilePurity = purity, incomePerSecond = income, onBed = onBed };
        allPumps.Add(pump);
        var go = SpawnPumpVisual(pump);
        allPumpGOs.Add(go);
        sessionPumps.Add(pump);
        sessionPumpGOs.Add(go);
        Debug.Log($"Pompa: ({tilePos.x},{tilePos.y}) Saflık={purity:F3} Gelir/sn={income:F2}");
        float remainingWealth = GameStatManager.Instance != null ? GameStatManager.Instance.Wealth : 0f;
        OnPumpPlaced?.Invoke(remainingWealth);
        return pump;
    }

    public void RemovePump(int i)
    {
        if (i < 0 || i >= allPumps.Count) return;
        if (i < allPumpGOs.Count && allPumpGOs[i] != null) Destroy(allPumpGOs[i]);
        allPumps.RemoveAt(i);
        if (i < allPumpGOs.Count) allPumpGOs.RemoveAt(i);
    }

    GameObject SpawnPumpVisual(FuelPump pump)
    {
        var go = new GameObject("FuelPump");
        go.transform.SetParent(transform);
        go.transform.position = pump.worldPos;
        go.transform.localScale = Vector3.one * pumpScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 100;
        if (pumpSprite != null) sr.sprite = pumpSprite;
        else
        {
            var tex = new Texture2D(4, 4);
            Color c = pump.onBed ? Color.green : Color.red;
            for (int i = 0; i < 16; i++) tex.SetPixel(i % 4, i / 4, c);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        }
        return go;
    }

    // === INCOME ===

    void AccumulateIncome()
    {
        if (allPumps.Count == 0) return;
        float dt = Time.deltaTime, total = 0f;
        foreach (var p in allPumps) { float e = p.incomePerSecond * dt; p.totalEarned += e; total += e; }
        if (total > 0f && GameStatManager.Instance != null) GameStatManager.Instance.AddWealth(total);
    }

    public float GetTotalIncomePerSecond() { float t = 0; foreach (var p in allPumps) t += p.incomePerSecond; return t; }
    public float GetTotalEarned() { float t = 0; foreach (var p in allPumps) t += p.totalEarned; return t; }
    public IReadOnlyList<FuelPump> GetPumps() => allPumps.AsReadOnly();

    // === HIGHLIGHTING ===

    void HighlightTiles(List<Vector2Int> tiles)
    {
        EnsureMapTexture(); if (mapTexture == null) return;
        foreach (var t in tiles)
        {
            int key = t.x + t.y * mapGenerator.width;
            if (revealedPixels.Contains(key)) continue;
            revealedPixels.Add(key);
            if (!originalColors.ContainsKey(key)) originalColors[key] = mapTexture.GetPixel(t.x, t.y);
            Color orig = originalColors[key];
            float a = Mathf.Lerp(highlightColor.a * 0.4f, highlightColor.a, bedGen.GetPurity(t.x, t.y));
            Color tint = new Color(highlightColor.r, highlightColor.g, highlightColor.b, a);
            Color c = Color.Lerp(orig, tint, tint.a); c.a = 1f;
            mapTexture.SetPixel(t.x, t.y, c);
        }
        mapTexture.Apply();
    }

    public void ClearHighlights()
    {
        EnsureMapTexture(); if (mapTexture == null) return;
        foreach (var kvp in originalColors) { int x = kvp.Key % mapGenerator.width, y = kvp.Key / mapGenerator.width; mapTexture.SetPixel(x, y, kvp.Value); }
        mapTexture.Apply(); revealedPixels.Clear(); originalColors.Clear();
    }

    // === CIRCLE ===

    void BuildCircle()
    {
        circleGO = new GameObject("ResearchCircle");
        circleGO.transform.SetParent(transform);
        circleLR = circleGO.AddComponent<LineRenderer>();
        circleLR.useWorldSpace = true; circleLR.loop = true; circleLR.positionCount = circleSegments;
        circleLR.startWidth = circleLR.endWidth = circleWidth; circleLR.sortingOrder = 500;
        circleLR.sortingLayerName = "Default";
        circleLR.startColor = circleLR.endColor = circleColor;
        circleLR.material = new Material(Shader.Find("Sprites/Default")) { color = circleColor };
        circleGO.SetActive(false);
    }

    void ShowCircle(bool s) { if (circleGO != null) circleGO.SetActive(s); }

    void UpdateCircleShape()
    {
        if (circleLR == null) return;
        Vector3 c = TileToWorld(researchCenter); c.z = -5f;
        float r = researchRadius / pixelsPerUnit;
        for (int i = 0; i < circleSegments; i++)
        {
            float a = (i / (float)circleSegments) * Mathf.PI * 2f;
            circleLR.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, c.z));
        }
    }

    // === COORDINATES ===

    public Vector2Int ScreenToTile(Vector2 sp)
    {
        if (mainCamera == null) return new Vector2Int(-1, -1);
        Vector3 wp = mainCamera.ScreenToWorldPoint(new Vector3(sp.x, sp.y, mainCamera.nearClipPlane + 1f));
        Vector3 o = GetMapOrigin();
        float hw = mapGenerator.width * 0.5f / pixelsPerUnit, hh = mapGenerator.height * 0.5f / pixelsPerUnit;
        int tx = Mathf.FloorToInt((wp.x - o.x + hw) * pixelsPerUnit), ty = Mathf.FloorToInt((wp.y - o.y + hh) * pixelsPerUnit);
        if (tx < 0 || tx >= mapGenerator.width || ty < 0 || ty >= mapGenerator.height) return new Vector2Int(-1, -1);
        return new Vector2Int(tx, ty);
    }

    public Vector3 TileToWorld(Vector2Int t)
    {
        Vector3 o = GetMapOrigin();
        float hw = mapGenerator.width * 0.5f / pixelsPerUnit, hh = mapGenerator.height * 0.5f / pixelsPerUnit;
        return new Vector3(o.x + (t.x / pixelsPerUnit) - hw, o.y + (t.y / pixelsPerUnit) - hh, 0f);
    }

    Vector3 GetMapOrigin() => (mapPainter != null && mapPainter.mapRenderer != null) ? mapPainter.mapRenderer.transform.position : Vector3.zero;
    bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void EnsureMapTexture()
    {
        if (mapTexture != null) return;
        if (mapPainter != null && mapPainter.mapRenderer != null && mapPainter.mapRenderer.sprite != null)
            mapTexture = mapPainter.mapRenderer.sprite.texture;
    }

    public void Clear()
    {
        ClearHighlights(); ShowCircle(false);
        foreach (var go in allPumpGOs) if (go != null) Destroy(go);
        allPumpGOs.Clear(); allPumps.Clear(); sessionPumps.Clear(); sessionPumpGOs.Clear();
    }
}