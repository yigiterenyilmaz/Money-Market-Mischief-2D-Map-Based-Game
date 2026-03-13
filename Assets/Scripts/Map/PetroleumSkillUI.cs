using UnityEngine;
using UnityEngine.UI;

public class PetroleumSkillUI : MonoBehaviour
{
    [Header("References")]
    public PetroleumSystem petroleumSystem;
    public UImanager       uiManager;
    public Canvas          canvas;

    [Header("Optional Icons")]
    public Sprite researchIcon, pumpIcon, cancelIcon;

    [Header("Popup")]
    public Color panelColor = new Color(0f, 0f, 0f, 0.75f);
    public Color btnNormal = new Color(0.18f, 0.18f, 0.22f, 0.9f);
    public Color btnHigh   = new Color(0.30f, 0.30f, 0.36f, 0.95f);
    public Color btnPress  = new Color(0.12f, 0.12f, 0.16f, 1f);
    public Color lblColor  = new Color(0.9f, 0.9f, 0.88f, 1f);
    public int btnSize = 128, btnSpacing = 24, panelPad = 30, fontSize = 22;

    [Header("Action Bar")]
    public Color cancelColor  = new Color(0.7f, 0.2f, 0.2f, 0.9f);
    public Color confirmColor = new Color(0.2f, 0.65f, 0.3f, 0.9f);
    public int actionW = 220, actionH = 64, actionBottom = 90, actionGap = 28, actionFont = 24;

    private GameObject popupRoot, actionBar, cancelBtn, confirmBtn, acceptBtn, costLabel;
    private GameObject timerSliderGO;
    private Image timerFill;
    private Text costText, timerText;
    private bool popupOpen;
    private float savedCamSize;

    void OnEnable()
    {
        PetroleumSystem.OnResearchDone          += OnFinished;
        PetroleumSystem.OnPumpsDone             += OnFinished;
        PetroleumSystem.OnModeCancelled         += OnFinished;
        PetroleumSystem.OnResearchCostChanged   += OnCostChanged;
        PetroleumSystem.OnResearchCircleReady   += OnCircleReady;
        PetroleumSystem.OnInsufficientFunds     += OnNotEnoughMoney;
        PetroleumSystem.OnResearchTimerStarted  += OnTimerStarted;
        PetroleumSystem.OnResearchTimerProgress += OnTimerProgress;
        PetroleumSystem.OnPumpPlaced            += OnPumpPlacedUI;
    }

    void OnDisable()
    {
        PetroleumSystem.OnResearchDone          -= OnFinished;
        PetroleumSystem.OnPumpsDone             -= OnFinished;
        PetroleumSystem.OnModeCancelled         -= OnFinished;
        PetroleumSystem.OnResearchCostChanged   -= OnCostChanged;
        PetroleumSystem.OnResearchCircleReady   -= OnCircleReady;
        PetroleumSystem.OnInsufficientFunds     -= OnNotEnoughMoney;
        PetroleumSystem.OnResearchTimerStarted  -= OnTimerStarted;
        PetroleumSystem.OnResearchTimerProgress -= OnTimerProgress;
        PetroleumSystem.OnPumpPlaced            -= OnPumpPlacedUI;
    }

    public void Toggle() { if (popupOpen) ClosePopup(); else OpenPopup(); }

    void OpenPopup()
    {
        if (popupRoot == null) BuildAll();
        popupRoot.SetActive(true);
        actionBar.SetActive(false);
        timerSliderGO.SetActive(false);
        popupOpen = true;
    }

    void ClosePopup()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
        popupOpen = false;
    }

    // === MAP MODE TRANSITIONS ===

    void GoToMap()
    {
        ClosePopup();
        if (uiManager != null)
        {
            uiManager.pausePanel.SetActive(false);
            uiManager.skillTreePanel.SetActive(false);
            uiManager.pauseButton.SetActive(false);
            uiManager.skillTreeButton.SetActive(false);
            uiManager.moneyBar.SetActive(false);

            MapController mc = uiManager.mainCamera;
            mc.enable = false;

            Camera cam = mc.GetComponent<Camera>();
            if (cam != null)
            {
                savedCamSize = cam.orthographicSize;
                if (mc.mapRenderer != null && mc.mapRenderer.sprite != null)
                {
                    float mH = mc.mapRenderer.bounds.size.y / 2f;
                    float mW = (mc.mapRenderer.bounds.size.x / 2f) / cam.aspect;
                    cam.orthographicSize = Mathf.Min(mH, mW);
                }
                if (mc.mapRenderer != null)
                {
                    Vector3 c = mc.mapRenderer.bounds.center;
                    mc.transform.position = new Vector3(c.x, c.y, mc.transform.position.z);
                }
            }
        }
    }

    void RestoreUI()
    {
        actionBar.SetActive(false);
        // Only hide timer if no background research is running
        if (petroleumSystem == null || petroleumSystem.PendingResearchCount == 0)
            timerSliderGO.SetActive(false);
        if (uiManager != null)
        {
            Camera cam = uiManager.mainCamera.GetComponent<Camera>();
            if (cam != null) cam.orthographicSize = savedCamSize;
            uiManager.OnGameResumePress();
        }
    }

    // === POPUP HANDLERS ===

    void OnResearchClicked()
    {
        if (petroleumSystem == null) return;
        GoToMap();
        actionBar.SetActive(true);
        cancelBtn.SetActive(true);
        confirmBtn.SetActive(false);
        acceptBtn.SetActive(false);
        costLabel.SetActive(false);
        timerSliderGO.SetActive(false);
        petroleumSystem.EnterResearchMode();
    }

    void OnPumpClicked()
    {
        if (petroleumSystem == null) return;
        GoToMap();
        actionBar.SetActive(true);
        cancelBtn.SetActive(true);
        confirmBtn.SetActive(false);
        acceptBtn.SetActive(true);
        timerSliderGO.SetActive(false);
        costLabel.SetActive(true);
        if (costText != null)
        {
            float w = GameStatManager.Instance != null ? GameStatManager.Instance.Wealth : 0f;
            float pc = petroleumSystem.pumpPlacementCost;
            costText.color = w >= pc ? Color.white : Color.red;
            costText.text = $"Pump cost: {pc:F0}  (Wealth: {w:F0})";
        }
        petroleumSystem.EnterPumpMode();
    }

    void OnPopupCancel() { ClosePopup(); }
    void OnCancelClick() { if (petroleumSystem != null) petroleumSystem.CancelMode(); }
    void OnConfirmClick() { if (petroleumSystem != null) petroleumSystem.ConfirmResearch(); }
    void OnAcceptClick() { if (petroleumSystem != null) petroleumSystem.AcceptPumps(); }

    // === EVENT HANDLERS ===

    void OnFinished()
    {
        RestoreUI();
        popupOpen = false;
    }

    void OnCostChanged(float cost)
    {
        if (costText == null) return;
        costLabel.SetActive(true);
        float wealth = GameStatManager.Instance != null ? GameStatManager.Instance.Wealth : 0f;
        float dur = petroleumSystem != null ? petroleumSystem.GetResearchDuration() : 0f;
        bool ok = wealth >= cost;
        costText.color = ok ? Color.white : Color.red;
        costText.text = $"Cost: {cost:F0}  Time: {dur:F1}s  (Wealth: {wealth:F0})";
    }

    void OnCircleReady() { if (confirmBtn != null) confirmBtn.SetActive(true); }

    void OnNotEnoughMoney(float cost)
    {
        if (costText == null) return;
        costLabel.SetActive(true);
        costText.color = Color.red;
        float w = GameStatManager.Instance != null ? GameStatManager.Instance.Wealth : 0f;
        costText.text = $"Not enough! Need: {cost:F0}  Have: {w:F0}";
    }

    void OnPumpPlacedUI(float remainingWealth)
    {
        if (costText == null || petroleumSystem == null) return;
        costLabel.SetActive(true);
        float pc = petroleumSystem.pumpPlacementCost;
        int placed = petroleumSystem.GetPumps().Count;
        bool canAfford = remainingWealth >= pc;
        costText.color = canAfford ? Color.white : Color.red;
        costText.text = $"Pumps: {placed}  Next: {pc:F0}  (Wealth: {remainingWealth:F0})";
    }

    void OnTimerStarted(float duration)
    {
        // Timer starts after UI is already restored — show slider on normal map view
        if (timerSliderGO != null) timerSliderGO.SetActive(true);
        if (timerFill != null) timerFill.fillAmount = 0f;
        if (timerText != null) timerText.text = $"Researching... 0/{duration:F1}s";
    }

    void OnTimerProgress(float progress)
    {
        if (timerSliderGO != null && !timerSliderGO.activeSelf)
            timerSliderGO.SetActive(true);
        if (timerFill != null) timerFill.fillAmount = progress;
        if (timerText != null && petroleumSystem != null)
        {
            float d = petroleumSystem.GetResearchDuration();
            timerText.text = $"Researching... {(d * progress):F1}/{d:F1}s";
        }
        // Hide when done (progress == 1 means complete, next frame PendingResearch is removed)
        if (progress >= 1f && timerSliderGO != null)
            timerSliderGO.SetActive(false);
    }

    // === BUILD UI ===

    void BuildAll()
    {
        EnsureCanvas();
        BuildPopup();
        BuildActionBar();
        BuildTimerSlider();
        // Everything starts hidden
        popupRoot.SetActive(false);
        actionBar.SetActive(false);
        timerSliderGO.SetActive(false);
    }

    void BuildPopup()
    {
        popupRoot = UI("PetroPopup", canvas.transform);
        var rt = popupRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        float w = (btnSize * 3) + (btnSpacing * 2) + (panelPad * 2);
        float h = btnSize + fontSize + 6 + (panelPad * 2);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        popupRoot.AddComponent<Image>().color = panelColor;
        var hlg = popupRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = btnSpacing; hlg.padding = new RectOffset(panelPad, panelPad, panelPad, panelPad);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        IconBtn("Research", researchIcon, new Color(0.2f, 0.6f, 0.9f), OnResearchClicked, popupRoot.transform);
        IconBtn("Pump", pumpIcon, new Color(0.9f, 0.6f, 0.1f), OnPumpClicked, popupRoot.transform);
        IconBtn("Cancel", cancelIcon, new Color(0.8f, 0.25f, 0.25f), OnPopupCancel, popupRoot.transform);
    }

    void BuildActionBar()
    {
        actionBar = UI("PetroActions", canvas.transform);
        var art = actionBar.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0.5f, 0f); art.anchorMax = new Vector2(0.5f, 0f);
        art.pivot = new Vector2(0.5f, 0f);
        art.sizeDelta = new Vector2(actionW * 3 + actionGap * 2 + 20, actionH + 70);
        art.anchoredPosition = new Vector2(0f, actionBottom);
        var hlg = actionBar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = actionGap; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(10, 10, 5, 5);
        cancelBtn  = ActBtn("Cancel",  cancelColor,  OnCancelClick,  actionBar.transform);
        confirmBtn = ActBtn("Confirm", confirmColor, OnConfirmClick, actionBar.transform);
        acceptBtn  = ActBtn("Accept",  confirmColor, OnAcceptClick,  actionBar.transform);

        costLabel = UI("CostLbl", actionBar.transform);
        var clrt = costLabel.GetComponent<RectTransform>();
        clrt.anchorMin = new Vector2(0f, 1f); clrt.anchorMax = new Vector2(1f, 1f);
        clrt.pivot = new Vector2(0.5f, 0f); clrt.sizeDelta = new Vector2(0, 52);
        clrt.anchoredPosition = new Vector2(0f, 5f);
        costText = costLabel.AddComponent<Text>();
        costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        costText.fontSize = actionFont; costText.color = Color.white;
        costText.alignment = TextAnchor.MiddleCenter; costText.fontStyle = FontStyle.Bold;
        costLabel.AddComponent<Shadow>().effectColor = Color.black;
    }

    void BuildTimerSlider()
    {
        timerSliderGO = UI("ResearchTimer", canvas.transform);
        var rt = timerSliderGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(450, 56);
        rt.anchoredPosition = new Vector2(0f, actionBottom + actionH + 90);

        timerSliderGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 0.9f);

        var fillGO = UI("Fill", timerSliderGO.transform);
        var frt = fillGO.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
        frt.pivot = new Vector2(0f, 0.5f);
        frt.offsetMin = new Vector2(4, 4); frt.offsetMax = new Vector2(-4, -4);
        frt.sizeDelta = new Vector2(442, 0);
        timerFill = fillGO.AddComponent<Image>();
        timerFill.color = new Color(0.3f, 0.75f, 0.4f, 1f);
        timerFill.type = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        timerFill.fillAmount = 0f;

        var txtGO = UI("Txt", timerSliderGO.transform);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        timerText = txtGO.AddComponent<Text>();
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.fontSize = 20; timerText.color = Color.white;
        timerText.alignment = TextAnchor.MiddleCenter; timerText.fontStyle = FontStyle.Bold;
        txtGO.AddComponent<Shadow>().effectColor = Color.black;
    }

    // === HELPERS ===

    void IconBtn(string label, Sprite icon, Color fb, UnityEngine.Events.UnityAction cb, Transform p)
    {
        var c = UI(label + "C", p);
        var vlg = c.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2; vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var cle = c.AddComponent<LayoutElement>(); cle.preferredWidth = btnSize; cle.preferredHeight = btnSize + fontSize + 6;

        var b = UI(label + "B", c.transform);
        var ble = b.AddComponent<LayoutElement>(); ble.preferredWidth = ble.preferredHeight = btnSize;
        b.AddComponent<Image>().color = btnNormal;
        var btn = b.AddComponent<Button>();
        var cbl = btn.colors; cbl.normalColor = btnNormal; cbl.highlightedColor = btnHigh;
        cbl.pressedColor = btnPress; cbl.selectedColor = btnHigh; cbl.fadeDuration = 0.08f; btn.colors = cbl;
        btn.onClick.AddListener(cb);

        var ico = UI("I", b.transform);
        var irt = ico.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = Vector2.one * 14; irt.offsetMax = Vector2.one * -14;
        var im = ico.AddComponent<Image>();
        if (icon != null) { im.sprite = icon; im.color = Color.white; } else im.color = fb;
        im.preserveAspect = true;

        var l = UI(label + "L", c.transform);
        var t = l.AddComponent<Text>(); t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize; t.color = lblColor; t.alignment = TextAnchor.MiddleCenter;
        var lle = l.AddComponent<LayoutElement>(); lle.preferredWidth = btnSize; lle.preferredHeight = fontSize + 6;
    }

    GameObject ActBtn(string label, Color bg, UnityEngine.Events.UnityAction cb, Transform p)
    {
        var go = UI(label, p);
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = actionW; le.preferredHeight = actionH;
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        var cbl = btn.colors; cbl.normalColor = bg; cbl.highlightedColor = Br(bg, 0.1f);
        cbl.pressedColor = Br(bg, -0.1f); cbl.fadeDuration = 0.08f; btn.colors = cbl;
        btn.onClick.AddListener(cb);
        var l = UI("L", go.transform);
        var lr = l.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        var t = l.AddComponent<Text>(); t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = actionFont; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;
        return go;
    }

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
}