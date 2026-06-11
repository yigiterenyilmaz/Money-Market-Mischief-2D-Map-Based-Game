using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TreasureSystem : MonoBehaviour
{
    public static TreasureSystem Instance { get; private set; }

    [Header("References")]
    public MapGenerator  mapGenerator;
    public MapPainter    mapPainter;
    public Camera        mainCamera;
    public Canvas        canvas;

    [Header("Treasure Sprites")]
    [Tooltip("Sprite shown on the underground map for discovered treasures. If null, uses a colored square.")]
    public Sprite treasureSprite;
    public float  treasureSpriteZ    = -0.55f;
    public float  treasureSpriteScale = 0.6f;

    [Header("Dig Cost")]
    [Tooltip("Cost to start digging a treasure. 0 = free.")]
    public float digCost = 50f;

    [Header("Panel Style")]
    public Color panelBg      = new Color(0f, 0f, 0f, 0.8f);
    public Color digBtnColor  = new Color(0.2f, 0.65f, 0.3f, 0.9f);
    public Color skipBtnColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);
    public int   panelWidth   = 340;
    public int   panelHeight  = 200;
    public int   panelFont    = 20;

    public float pixelsPerUnit = 100f;

    // Events
    public static event Action<float> OnTreasureDug;      // reward amount
    public static event Action<float> OnDigTimerStarted;   // duration
    public static event Action<float> OnDigTimerProgress;  // 0-1
    public static event Action        OnDigComplete;

    private TreasureGenerator   treasureGen;
    private List<GameObject>    treasureGOs = new List<GameObject>();
    private bool                ready;

    // Dig panel
    private GameObject panelRoot;
    private Text       panelInfoText;
    private TreasureGenerator.Treasure selectedTreasure;

    // Dig timer
    private bool  digging;
    private float digElapsed, digDuration;
    private TreasureGenerator.Treasure diggingTreasure;

    // Dig timer UI
    private GameObject digTimerGO;
    private Image      digTimerFill;
    private Text       digTimerText;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        TreasureGenerator.OnTreasuresGenerated           += OnTreasuresReady;
        UndergroundMapManager.OnViewModeChanged          += OnViewChanged;
    }

    void OnDisable()
    {
        TreasureGenerator.OnTreasuresGenerated           -= OnTreasuresReady;
        UndergroundMapManager.OnViewModeChanged          -= OnViewChanged;
    }

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void OnTreasuresReady()
    {
        treasureGen = TreasureGenerator.Instance;
        if (treasureGen == null) return;

        if (treasureGen.DebugRevealEnabled)
            treasureGen.DebugDiscoverAll();

        ready = true;
        RefreshTreasureSprites();
    }

    void OnViewChanged(UndergroundMapManager.ViewMode mode)
    {
        bool underground = (mode == UndergroundMapManager.ViewMode.Underground);
        foreach (var go in treasureGOs)
            if (go != null) go.SetActive(underground && IsGOTreasureVisible(go));

        // Hide panel when switching views
        if (!underground) HidePanel();
    }

    bool IsGOTreasureVisible(GameObject go)
    {
        // All treasure GOs are visible in underground if discovered and not dug up
        // The sprite is only spawned for discovered & !dugUp, so just return true
        return true;
    }

    void Update()
    {
        if (!ready) return;
        TickDigTimer();
        HandleClick();
    }

    // === CLICK DETECTION ===

    void HandleClick()
    {
        if (digging) return;
        if (UndergroundMapManager.Instance == null) return;
        if (UndergroundMapManager.Instance.CurrentView != UndergroundMapManager.ViewMode.Underground) return;

        Mouse mouse = Mouse.current;
        if (mouse == null || IsPointerOverUI()) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        Vector2Int tile = ScreenToTile(mouse.position.ReadValue());
        if (tile.x < 0) return;

        var treasure = treasureGen.GetTreasureAt(tile.x, tile.y);
        if (treasure == null || treasure.dugUp || !treasure.discovered) return;

        selectedTreasure = treasure;
        ShowPanel(treasure);
    }

    // === DIG PANEL ===

    void ShowPanel(TreasureGenerator.Treasure treasure)
    {
        if (panelRoot == null) BuildPanel();
        panelRoot.SetActive(true);

        float cost = digCost;
        float wealth = GameStatManager.Instance != null ? GameStatManager.Instance.Wealth : 0f;
        string costStr = cost > 0f ? $"\nDig cost: {cost:F0}" : "";
        bool canAfford = wealth >= cost || cost <= 0f;

        panelInfoText.text = $"Treasure found!\nReward: ~{treasure.reward:F0}\nDig time: ~{treasure.digTime:F1}s{costStr}";
        panelInfoText.color = canAfford ? Color.white : Color.red;
    }

    void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        selectedTreasure = null;
    }

    void OnDigClicked()
    {
        if (selectedTreasure == null || selectedTreasure.dugUp) { HidePanel(); return; }

        if (digCost > 0f && GameStatManager.Instance != null)
        {
            if (!GameStatManager.Instance.TrySpendWealth(digCost))
            {
                if (panelInfoText != null)
                {
                    float w = GameStatManager.Instance.Wealth;
                    panelInfoText.color = Color.red;
                    panelInfoText.text = $"Not enough! Need: {digCost:F0}  Have: {w:F0}";
                }
                return;
            }
        }

        HidePanel();
        StartDig(selectedTreasure);
    }

    void OnSkipClicked()
    {
        HidePanel();
    }

    // === DIG TIMER ===

    void StartDig(TreasureGenerator.Treasure treasure)
    {
        digging = true;
        digElapsed = 0f;
        digDuration = treasure.digTime;
        diggingTreasure = treasure;

        if (digTimerGO == null) BuildDigTimer();
        digTimerGO.SetActive(true);
        if (digTimerFill != null) digTimerFill.fillAmount = 0f;
        if (digTimerText != null) digTimerText.text = $"Digging... 0/{digDuration:F1}s";

        OnDigTimerStarted?.Invoke(digDuration);
    }

    void TickDigTimer()
    {
        if (!digging) return;
        digElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(digElapsed / digDuration);

        if (digTimerFill != null) digTimerFill.fillAmount = progress;
        if (digTimerText != null) digTimerText.text = $"Digging... {digElapsed:F1}/{digDuration:F1}s";
        OnDigTimerProgress?.Invoke(progress);

        if (digElapsed >= digDuration)
        {
            digging = false;
            if (digTimerGO != null) digTimerGO.SetActive(false);

            // Award money
            float reward = diggingTreasure.reward;
            if (GameStatManager.Instance != null)
                GameStatManager.Instance.AddWealth(reward);

            // Mark dug up
            treasureGen.MarkDugUp(diggingTreasure.tilePos);

            // Remove sprite
            RemoveTreasureSprite(diggingTreasure.tilePos);

            Debug.Log($"Hazine kazıldı: ({diggingTreasure.tilePos.x},{diggingTreasure.tilePos.y}) Ödül={reward:F0}");
            OnTreasureDug?.Invoke(reward);
            OnDigComplete?.Invoke();
            diggingTreasure = null;
        }
    }

    // === TREASURE SPRITES ===

    public void RefreshTreasureSprites()
    {
        // Clear old
        foreach (var go in treasureGOs)
            if (go != null) Destroy(go);
        treasureGOs.Clear();

        if (treasureGen == null || !treasureGen.IsGenerated) return;

        bool isUnderground = UndergroundMapManager.Instance != null &&
                             UndergroundMapManager.Instance.CurrentView == UndergroundMapManager.ViewMode.Underground;

        foreach (var t in treasureGen.GetTreasures())
        {
            if (t.dugUp) continue;
            if (!t.discovered) continue;

            var go = SpawnTreasureVisual(t);
            go.SetActive(isUnderground);
            treasureGOs.Add(go);
        }
    }

    /// <summary>
    /// Called by UndergroundMapManager after revealing a circle to show newly discovered treasures.
    /// </summary>
    public void OnTilesRevealed()
    {
        RefreshTreasureSprites();
    }

    GameObject SpawnTreasureVisual(TreasureGenerator.Treasure treasure)
    {
        var go = new GameObject("Treasure");
        go.transform.SetParent(transform);
        go.transform.position = TileToWorld(treasure.tilePos) + new Vector3(0, 0, treasureSpriteZ);
        go.transform.localScale = Vector3.one * treasureSpriteScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 90;

        if (treasureSprite != null)
        {
            sr.sprite = treasureSprite;
        }
        else
        {
            // Fallback: small yellow square
            var tex = new Texture2D(6, 6);
            Color c = new Color(0.9f, 0.75f, 0.1f);
            for (int i = 0; i < 36; i++) tex.SetPixel(i % 6, i / 6, c);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 6, 6), new Vector2(0.5f, 0.5f), 16f);
        }

        // Store tile position for lookup
        go.name = $"Treasure_{treasure.tilePos.x}_{treasure.tilePos.y}";
        return go;
    }

    void RemoveTreasureSprite(Vector2Int pos)
    {
        string targetName = $"Treasure_{pos.x}_{pos.y}";
        for (int i = treasureGOs.Count - 1; i >= 0; i--)
        {
            if (treasureGOs[i] != null && treasureGOs[i].name == targetName)
            {
                Destroy(treasureGOs[i]);
                treasureGOs.RemoveAt(i);
                break;
            }
        }
    }

    // === BUILD UI ===

    void BuildPanel()
    {
        EnsureCanvas();

        panelRoot = UI("TreasurePanel", canvas.transform);
        var rt = panelRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(panelWidth, panelHeight);
        rt.anchoredPosition = Vector2.zero;
        panelRoot.AddComponent<Image>().color = panelBg;

        // Info text
        var txtGO = UI("Info", panelRoot.transform);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.4f); trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16, 0); trt.offsetMax = new Vector2(-16, -12);
        panelInfoText = txtGO.AddComponent<Text>();
        panelInfoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panelInfoText.fontSize = panelFont; panelInfoText.color = Color.white;
        panelInfoText.alignment = TextAnchor.MiddleCenter;
        txtGO.AddComponent<Shadow>().effectColor = Color.black;

        // Buttons row
        var btnRow = UI("BtnRow", panelRoot.transform);
        var brt = btnRow.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0.38f);
        brt.offsetMin = new Vector2(16, 12); brt.offsetMax = new Vector2(-16, -4);
        var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        MakeBtn("Dig", digBtnColor, OnDigClicked, btnRow.transform);
        MakeBtn("Skip", skipBtnColor, OnSkipClicked, btnRow.transform);

        panelRoot.SetActive(false);
    }

    void BuildDigTimer()
    {
        EnsureCanvas();

        digTimerGO = UI("DigTimer", canvas.transform);
        var rt = digTimerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(400, 50);
        rt.anchoredPosition = new Vector2(0f, 20f);

        digTimerGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 0.9f);

        var fillGO = UI("Fill", digTimerGO.transform);
        var frt = fillGO.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
        frt.pivot = new Vector2(0f, 0.5f);
        frt.offsetMin = new Vector2(4, 4); frt.offsetMax = new Vector2(-4, -4);
        frt.sizeDelta = new Vector2(392, 0);
        digTimerFill = fillGO.AddComponent<Image>();
        digTimerFill.color = new Color(0.85f, 0.72f, 0.15f, 1f);
        digTimerFill.type = Image.Type.Filled;
        digTimerFill.fillMethod = Image.FillMethod.Horizontal;
        digTimerFill.fillAmount = 0f;

        var txtGO = UI("Txt", digTimerGO.transform);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        digTimerText = txtGO.AddComponent<Text>();
        digTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        digTimerText.fontSize = 18; digTimerText.color = Color.white;
        digTimerText.alignment = TextAnchor.MiddleCenter; digTimerText.fontStyle = FontStyle.Bold;
        txtGO.AddComponent<Shadow>().effectColor = Color.black;

        digTimerGO.SetActive(false);
    }

    void MakeBtn(string label, Color bg, UnityEngine.Events.UnityAction cb, Transform parent)
    {
        var go = UI(label, parent);
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        var cbl = btn.colors;
        cbl.normalColor = bg;
        cbl.highlightedColor = Br(bg, 0.1f);
        cbl.pressedColor = Br(bg, -0.1f);
        cbl.fadeDuration = 0.08f;
        btn.colors = cbl;
        btn.onClick.AddListener(cb);

        var lbl = UI("L", go.transform);
        var lr = lbl.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        var t = lbl.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = panelFont; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;
    }

    // === COORDINATES ===

    Vector2Int ScreenToTile(Vector2 sp)
    {
        if (mainCamera == null) return new Vector2Int(-1, -1);
        Vector3 wp = mainCamera.ScreenToWorldPoint(new Vector3(sp.x, sp.y, mainCamera.nearClipPlane + 1f));
        Vector3 o = GetMapOrigin();
        float hw = mapGenerator.width * 0.5f / pixelsPerUnit, hh = mapGenerator.height * 0.5f / pixelsPerUnit;
        int tx = Mathf.FloorToInt((wp.x - o.x + hw) * pixelsPerUnit);
        int ty = Mathf.FloorToInt((wp.y - o.y + hh) * pixelsPerUnit);
        if (tx < 0 || tx >= mapGenerator.width || ty < 0 || ty >= mapGenerator.height) return new Vector2Int(-1, -1);
        return new Vector2Int(tx, ty);
    }

    Vector3 TileToWorld(Vector2Int t)
    {
        Vector3 o = GetMapOrigin();
        float hw = mapGenerator.width * 0.5f / pixelsPerUnit, hh = mapGenerator.height * 0.5f / pixelsPerUnit;
        return new Vector3(o.x + (t.x / pixelsPerUnit) - hw, o.y + (t.y / pixelsPerUnit) - hh, 0f);
    }

    Vector3 GetMapOrigin() => (mapPainter != null && mapPainter.mapRenderer != null) ? mapPainter.mapRenderer.transform.position : Vector3.zero;
    bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void EnsureCanvas()
    {
        if (canvas != null) return;
        canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null) return;
        var go = new GameObject("Canvas");
        canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        go.AddComponent<CanvasScaler>(); go.AddComponent<GraphicRaycaster>();
    }

    static GameObject UI(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return g; }
    static Color Br(Color c, float a) => new Color(Mathf.Clamp01(c.r + a), Mathf.Clamp01(c.g + a), Mathf.Clamp01(c.b + a), c.a);

    public void Clear()
    {
        HidePanel();
        digging = false;
        if (digTimerGO != null) digTimerGO.SetActive(false);
        foreach (var go in treasureGOs) if (go != null) Destroy(go);
        treasureGOs.Clear();
    }
}