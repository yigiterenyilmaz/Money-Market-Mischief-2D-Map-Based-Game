using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RESEARCH:
///   Popup → Research → ToggleUI() hides everything → map active
///   Cancel button visible. Player clicks map → circle appears, drag to size.
///   Release → Confirm button appears with cost label. Click again → new circle.
///   Confirm → scan, ToggleUI() restores.  Cancel → ToggleUI() restores.
///
/// PUMP:
///   Popup → Pump → ToggleUI() hides everything → map active
///   Cancel + Accept buttons visible. Click map → place pumps (multiple).
///   Accept → confirm all, ToggleUI() restores.  Cancel → undo all, restores.
/// </summary>
public class PetroleumSkillUI : MonoBehaviour
{
    [Header("References")]
    public PetroleumSystem petroleumSystem;
    public UImanager       uiManager;
    public Canvas          canvas;

    [Header("Optional Icons")]
    public Sprite researchIcon;
    public Sprite pumpIcon;
    public Sprite cancelIcon;

    [Header("Popup Appearance")]
    public Color panelColor      = new Color(0.12f, 0.12f, 0.14f, 0.92f);
    public Color btnNormal       = new Color(0.22f, 0.22f, 0.26f, 1f);
    public Color btnHighlight    = new Color(0.32f, 0.32f, 0.38f, 1f);
    public Color btnPressed      = new Color(0.16f, 0.16f, 0.20f, 1f);
    public Color labelColor      = new Color(0.82f, 0.82f, 0.80f, 1f);
    public int   btnSize         = 64;
    public int   btnSpacing      = 12;
    public int   panelPad        = 14;
    public int   fontSize        = 11;

    [Header("Action Bar")]
    public Color cancelColor     = new Color(0.7f, 0.2f, 0.2f, 0.9f);
    public Color confirmColor    = new Color(0.2f, 0.65f, 0.3f, 0.9f);
    public int   actionW         = 120;
    public int   actionH         = 40;
    public int   actionBottom    = 60;
    public int   actionGap       = 16;
    public int   actionFont      = 14;

    // Runtime
    private GameObject popupRoot;
    private GameObject actionBar;    // container for cancel/confirm/accept
    private GameObject cancelBtn;
    private GameObject confirmBtn;   // research confirm
    private GameObject acceptBtn;    // pump accept
    private GameObject costLabel;    // shows research cost
    private Text       costText;
    private bool       popupOpen;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void OnEnable()
    {
        PetroleumSystem.OnResearchDone        += OnFinished;
        PetroleumSystem.OnPumpsDone           += OnFinished;
        PetroleumSystem.OnModeCancelled       += OnFinished;
        PetroleumSystem.OnResearchCostChanged += OnCostChanged;
        PetroleumSystem.OnResearchCircleReady += OnCircleReady;
        PetroleumSystem.OnInsufficientFunds   += OnNotEnoughMoney;
    }

    void OnDisable()
    {
        PetroleumSystem.OnResearchDone        -= OnFinished;
        PetroleumSystem.OnPumpsDone           -= OnFinished;
        PetroleumSystem.OnModeCancelled       -= OnFinished;
        PetroleumSystem.OnResearchCostChanged -= OnCostChanged;
        PetroleumSystem.OnResearchCircleReady -= OnCircleReady;
        PetroleumSystem.OnInsufficientFunds   -= OnNotEnoughMoney;
    }

    // =========================================================================
    // PUBLIC — wire to Petroleum skill button
    // =========================================================================

    public void Toggle()
    {
        if (popupOpen) ClosePopup();
        else           OpenPopup();
    }

    // =========================================================================
    // POPUP
    // =========================================================================

    void OpenPopup()
    {
        if (popupRoot == null) BuildAll();
        popupRoot.SetActive(true);
        actionBar.SetActive(false);
        popupOpen = true;
    }

    void ClosePopup()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
        popupOpen = false;
    }

    // =========================================================================
    // ENTERING MAP MODES
    // =========================================================================

    private float savedCameraSize;

    void GoToMap()
    {
        ClosePopup();

        if (uiManager != null)
        {
            // Hide all UI panels and buttons
            uiManager.pausePanel.SetActive(false);
            uiManager.skillTreePanel.SetActive(false);
            uiManager.pauseButton.SetActive(false);
            uiManager.skillTreeButton.SetActive(false);
            uiManager.moneyBar.SetActive(false);

            MapController mc = uiManager.mainCamera;

            // Disable camera pan/zoom so left-click drag doesn't conflict
            mc.enable = false;

            // Save current zoom and zoom out to maximum (full map view)
            Camera cam = mc.GetComponent<Camera>();
            if (cam != null)
            {
                savedCameraSize = cam.orthographicSize;

                // Calculate max zoom the same way MapController does
                if (mc.mapRenderer != null && mc.mapRenderer.sprite != null)
                {
                    float mapH = mc.mapRenderer.bounds.size.y / 2f;
                    float mapW = (mc.mapRenderer.bounds.size.x / 2f) / cam.aspect;
                    cam.orthographicSize = Mathf.Min(mapH, mapW);
                }

                // Center camera on map
                if (mc.mapRenderer != null)
                {
                    Vector3 center = mc.mapRenderer.bounds.center;
                    mc.transform.position = new Vector3(center.x, center.y, mc.transform.position.z);
                }
            }
        }
    }

    void RestoreUI()
    {
        actionBar.SetActive(false);

        if (uiManager != null)
        {
            // Restore camera zoom
            MapController mc = uiManager.mainCamera;
            Camera cam = mc.GetComponent<Camera>();
            if (cam != null)
                cam.orthographicSize = savedCameraSize;

            // Restore full UI (also re-enables camera via mainCamera.enable = true)
            uiManager.OnGameResumePress();
        }
    }

    // =========================================================================
    // POPUP BUTTON HANDLERS
    // =========================================================================

    void OnResearchClicked()
    {
        if (petroleumSystem == null) return;
        GoToMap();

        // Show action bar with only Cancel (Confirm appears after circle is placed)
        actionBar.SetActive(true);
        cancelBtn.SetActive(true);
        confirmBtn.SetActive(false);
        acceptBtn.SetActive(false);
        costLabel.SetActive(false);

        petroleumSystem.EnterResearchMode();
    }

    void OnPumpClicked()
    {
        if (petroleumSystem == null) return;
        GoToMap();

        // Show action bar with Cancel + Accept
        actionBar.SetActive(true);
        cancelBtn.SetActive(true);
        confirmBtn.SetActive(false);
        acceptBtn.SetActive(true);
        costLabel.SetActive(false);

        petroleumSystem.EnterPumpMode();
    }

    void OnPopupCancel()
    {
        ClosePopup();
    }

    // =========================================================================
    // ACTION BUTTON HANDLERS
    // =========================================================================

    void OnCancelClick()
    {
        if (petroleumSystem != null)
            petroleumSystem.CancelMode();
        // OnModeCancelled → OnFinished → RestoreUI
    }

    void OnConfirmClick()
    {
        if (petroleumSystem != null)
            petroleumSystem.ConfirmResearch();
        // OnResearchDone → OnFinished → RestoreUI
    }

    void OnAcceptClick()
    {
        if (petroleumSystem != null)
            petroleumSystem.AcceptPumps();
        // OnPumpsDone → OnFinished → RestoreUI
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    void OnFinished()
    {
        RestoreUI();
        popupOpen = false;
    }

    void OnCostChanged(float cost)
    {
        if (costText != null)
        {
            costLabel.SetActive(true);
            float wealth = GameStatManager.Instance != null ? GameStatManager.Instance.Wealth : 0f;
            bool canAfford = wealth >= cost;
            costText.color = canAfford ? Color.white : Color.red;
            costText.text = $"Cost: {cost:F0}  (Wealth: {wealth:F0})";
        }
    }

    void OnCircleReady()
    {
        if (confirmBtn != null)
            confirmBtn.SetActive(true);
    }

    void OnNotEnoughMoney(float cost)
    {
        if (costText != null)
        {
            costLabel.SetActive(true);
            costText.color = Color.red;
            float wealth = GameStatManager.Instance != null ? GameStatManager.Instance.Wealth : 0f;
            costText.text = $"Not enough! Need: {cost:F0}  Have: {wealth:F0}";
        }
    }

    // =========================================================================
    // BUILD UI
    // =========================================================================

    void BuildAll()
    {
        EnsureCanvas();
        BuildPopup();
        BuildActionBar();
    }

    void BuildPopup()
    {
        popupRoot = UI("PetroleumPopup", canvas.transform);
        RectTransform rt = popupRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        float w = (btnSize * 3) + (btnSpacing * 2) + (panelPad * 2);
        float h = btnSize + fontSize + 6 + (panelPad * 2);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;

        popupRoot.AddComponent<Image>().color = panelColor;

        var hlg = popupRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = btnSpacing;
        hlg.padding = new RectOffset(panelPad, panelPad, panelPad, panelPad);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

        IconBtn("Research", researchIcon, new Color(0.2f, 0.6f, 0.9f),  OnResearchClicked, popupRoot.transform);
        IconBtn("Pump",     pumpIcon,     new Color(0.9f, 0.6f, 0.1f),  OnPumpClicked,     popupRoot.transform);
        IconBtn("Cancel",   cancelIcon,   new Color(0.8f, 0.25f, 0.25f), OnPopupCancel,     popupRoot.transform);

        popupRoot.SetActive(false);
    }

    void BuildActionBar()
    {
        actionBar = UI("PetroActions", canvas.transform);
        RectTransform art = actionBar.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0.5f, 0f);
        art.anchorMax = new Vector2(0.5f, 0f);
        art.pivot     = new Vector2(0.5f, 0f);
        art.sizeDelta        = new Vector2(actionW * 3 + actionGap * 2 + 20, actionH + 50);
        art.anchoredPosition = new Vector2(0f, actionBottom);

        var hlg = actionBar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = actionGap;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(10, 10, 5, 5);

        cancelBtn  = ActionBtn("Cancel",  cancelColor,  OnCancelClick,  actionBar.transform);
        confirmBtn = ActionBtn("Confirm", confirmColor, OnConfirmClick, actionBar.transform);
        acceptBtn  = ActionBtn("Accept",  confirmColor, OnAcceptClick,  actionBar.transform);

        // Cost label — above the action bar
        costLabel = UI("CostLabel", actionBar.transform);
        RectTransform clrt = costLabel.GetComponent<RectTransform>();
        clrt.anchorMin = new Vector2(0f, 1f);
        clrt.anchorMax = new Vector2(1f, 1f);
        clrt.pivot     = new Vector2(0.5f, 0f);
        clrt.sizeDelta = new Vector2(0, 30);
        clrt.anchoredPosition = new Vector2(0f, 5f);

        costText = costLabel.AddComponent<Text>();
        costText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        costText.fontSize  = actionFont;
        costText.color     = Color.white;
        costText.alignment = TextAnchor.MiddleCenter;
        costText.fontStyle = FontStyle.Bold;
        costText.text      = "";

        // Add shadow for readability
        var shadow = costLabel.AddComponent<Shadow>();
        shadow.effectColor    = Color.black;
        shadow.effectDistance  = new Vector2(1, -1);

        actionBar.SetActive(false);
    }

    // =========================================================================
    // UI BUILDER HELPERS
    // =========================================================================

    void IconBtn(string label, Sprite icon, Color fallback,
                 UnityEngine.Events.UnityAction onClick, Transform parent)
    {
        GameObject c = UI(label + "C", parent);
        var vlg = c.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2; vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var cle = c.AddComponent<LayoutElement>();
        cle.preferredWidth = btnSize; cle.preferredHeight = btnSize + fontSize + 6;

        GameObject b = UI(label + "B", c.transform);
        var ble = b.AddComponent<LayoutElement>();
        ble.preferredWidth = ble.preferredHeight = btnSize;
        b.AddComponent<Image>().color = btnNormal;
        Button btn = b.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = btnNormal; cb.highlightedColor = btnHighlight;
        cb.pressedColor = btnPressed; cb.selectedColor = btnHighlight;
        cb.fadeDuration = 0.08f; btn.colors = cb;
        btn.onClick.AddListener(onClick);

        GameObject ico = UI("I", b.transform);
        RectTransform irt = ico.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = Vector2.one * 12; irt.offsetMax = Vector2.one * -12;
        Image iim = ico.AddComponent<Image>();
        if (icon != null) { iim.sprite = icon; iim.color = Color.white; }
        else iim.color = fallback;
        iim.preserveAspect = true;

        GameObject l = UI(label + "L", c.transform);
        Text t = l.AddComponent<Text>();
        t.text = label; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize; t.color = labelColor; t.alignment = TextAnchor.MiddleCenter;
        var lle = l.AddComponent<LayoutElement>();
        lle.preferredWidth = btnSize; lle.preferredHeight = fontSize + 4;
    }

    GameObject ActionBtn(string label, Color bg,
                          UnityEngine.Events.UnityAction onClick, Transform parent)
    {
        GameObject go = UI(label, parent);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = actionW; le.preferredHeight = actionH;
        go.AddComponent<Image>().color = bg;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = bg;
        cb.highlightedColor = Bright(bg, 0.1f);
        cb.pressedColor = Bright(bg, -0.1f);
        cb.fadeDuration = 0.08f; btn.colors = cb;
        btn.onClick.AddListener(onClick);

        GameObject l = UI("L", go.transform);
        RectTransform lr = l.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        Text t = l.AddComponent<Text>();
        t.text = label; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = actionFont; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;

        return go;
    }

    // =========================================================================
    // UTILITIES
    // =========================================================================

    void EnsureCanvas()
    {
        if (canvas != null) return;
        canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null) return;
        GameObject go = new GameObject("Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
    }

    static GameObject UI(string n, Transform p)
    {
        GameObject g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(p, false); return g;
    }

    static Color Bright(Color c, float a)
    {
        return new Color(
            Mathf.Clamp01(c.r + a), Mathf.Clamp01(c.g + a),
            Mathf.Clamp01(c.b + a), c.a);
    }
}