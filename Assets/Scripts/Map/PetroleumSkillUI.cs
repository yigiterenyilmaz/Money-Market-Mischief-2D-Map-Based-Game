using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal popup overlay for petroleum actions.
/// Appears when the player clicks the Petroleum skill button in the main UI.
/// Shows three icon buttons: Research, Plant Pump, Cancel.
/// 
/// SETUP:
///   1. Create a Canvas (Screen Space - Overlay) if you don't have one.
///   2. Attach this script to any GameObject.
///   3. Assign references in Inspector.
///   4. Call Toggle() from your existing Petroleum skill button's OnClick.
///     
///   The script builds the entire popup at runtime — no manual UI construction needed.
///   Optionally assign custom icons via Inspector; without them it uses colored fallbacks.
/// </summary>
public class PetroleumSkillUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // REFERENCES
    // -------------------------------------------------------------------------

    [Header("References")]
    [Tooltip("Your UI Canvas. If null, the script finds or creates one.")]
    public Canvas canvas;

    [Tooltip("PetroleumSystem in the scene.")]
    public PetroleumSystem petroleumSystem;

    [Header("Optional Icons (leave null for auto-generated)")]
    public Sprite researchIcon;
    public Sprite pumpIcon;
    public Sprite cancelIcon;

    [Header("Popup Position")]
    [Tooltip("Screen-space offset from center. (0,0) = dead center.")]
    public Vector2 popupOffset = Vector2.zero;

    [Header("Appearance")]
    public Color panelColor       = new Color(0.12f, 0.12f, 0.14f, 0.92f);
    public Color buttonNormal     = new Color(0.22f, 0.22f, 0.26f, 1f);
    public Color buttonHighlight  = new Color(0.32f, 0.32f, 0.38f, 1f);
    public Color buttonPressed    = new Color(0.16f, 0.16f, 0.20f, 1f);
    public Color activeGlow       = new Color(0.30f, 0.75f, 0.45f, 1f);
    public Color labelColor       = new Color(0.82f, 0.82f, 0.80f, 1f);
    public int   buttonSize       = 64;
    public int   buttonSpacing    = 12;
    public int   panelPadding     = 14;
    public int   fontSize         = 11;

    // -------------------------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------------------------

    private GameObject popupRoot;
    private Button     btnResearch;
    private Button     btnPump;
    private Button     btnCancel;
    private Image      imgResearch;
    private Image      imgPump;
    private bool       isOpen = false;
    private PetroleumSystem.InteractionMode activeMode = PetroleumSystem.InteractionMode.None;

    // -------------------------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------------------------

    /// <summary>Toggle popup visibility. Hook this to your Petroleum skill button.</summary>
    public void Toggle()
    {
        if (isOpen) Close();
        else        Open();
    }

    public void Open()
    {
        if (popupRoot == null) BuildPopup();
        popupRoot.SetActive(true);
        isOpen = true;
        RefreshButtonStates();
    }

    public void Close()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
        isOpen = false;

        // Exit whatever mode was active
        if (petroleumSystem != null)
            petroleumSystem.ExitMode();

        activeMode = PetroleumSystem.InteractionMode.None;
    }

    public bool IsOpen => isOpen;

    // -------------------------------------------------------------------------
    // BUILD UI AT RUNTIME
    // -------------------------------------------------------------------------

    private void BuildPopup()
    {
        EnsureCanvas();

        // --- Root panel ---
        popupRoot = CreateUIObject("PetroleumPopup", canvas.transform);
        RectTransform rootRT = popupRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0.5f);
        rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot     = new Vector2(0.5f, 0.5f);

        float totalWidth  = (buttonSize * 3) + (buttonSpacing * 2) + (panelPadding * 2);
        float totalHeight = buttonSize + fontSize + 6 + (panelPadding * 2);
        rootRT.sizeDelta        = new Vector2(totalWidth, totalHeight);
        rootRT.anchoredPosition = popupOffset;

        Image panelImg = popupRoot.AddComponent<Image>();
        panelImg.color = panelColor;

        // Round corners if possible — works on Unity 2021.2+
        // Fallback: plain rectangle on older versions
        #if UNITY_2021_2_OR_NEWER
        // No built-in rounded rect in base Unity UI, but the dark panel looks fine
        #endif

        // --- Horizontal layout ---
        HorizontalLayoutGroup hlg = popupRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing            = buttonSpacing;
        hlg.padding            = new RectOffset(panelPadding, panelPadding, panelPadding, panelPadding);
        hlg.childAlignment     = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // --- Buttons ---
        btnResearch = CreateActionButton("Research",  researchIcon, new Color(0.2f, 0.6f, 0.9f), OnResearchClicked);
        btnPump     = CreateActionButton("Pump",      pumpIcon,     new Color(0.9f, 0.6f, 0.1f), OnPumpClicked);
        btnCancel   = CreateActionButton("Cancel",    cancelIcon,   new Color(0.8f, 0.25f, 0.25f), OnCancelClicked);

        imgResearch = btnResearch.GetComponent<Image>();
        imgPump     = btnPump.GetComponent<Image>();

        popupRoot.SetActive(false);
    }

    private Button CreateActionButton(string label, Sprite icon, Color fallbackColor, UnityEngine.Events.UnityAction onClick)
    {
        // Container (vertical: icon on top, label below)
        GameObject container = CreateUIObject(label + "Container", popupRoot.transform);
        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.spacing            = 2;
        vlg.childAlignment     = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        LayoutElement containerLE = container.AddComponent<LayoutElement>();
        containerLE.preferredWidth  = buttonSize;
        containerLE.preferredHeight = buttonSize + fontSize + 6;

        // Button object
        GameObject btnObj = CreateUIObject(label + "Btn", container.transform);
        RectTransform btnRT = btnObj.GetComponent<RectTransform>();
        btnRT.sizeDelta = new Vector2(buttonSize, buttonSize);

        LayoutElement btnLE = btnObj.AddComponent<LayoutElement>();
        btnLE.preferredWidth  = buttonSize;
        btnLE.preferredHeight = buttonSize;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = buttonNormal;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb    = btn.colors;
        cb.normalColor      = buttonNormal;
        cb.highlightedColor = buttonHighlight;
        cb.pressedColor     = buttonPressed;
        cb.selectedColor    = buttonHighlight;
        cb.fadeDuration     = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        // Icon inside button
        GameObject iconObj = CreateUIObject("Icon", btnObj.transform);
        RectTransform iconRT = iconObj.GetComponent<RectTransform>();
        iconRT.anchorMin = Vector2.zero;
        iconRT.anchorMax = Vector2.one;
        iconRT.offsetMin = new Vector2(12, 12);
        iconRT.offsetMax = new Vector2(-12, -12);

        Image iconImg = iconObj.AddComponent<Image>();
        if (icon != null)
        {
            iconImg.sprite = icon;
            iconImg.color  = Color.white;
        }
        else
        {
            // Fallback: solid colored square as placeholder icon
            iconImg.sprite = null;
            iconImg.color  = fallbackColor;
        }
        iconImg.preserveAspect = true;

        // Label below button
        GameObject labelObj = CreateUIObject(label + "Label", container.transform);
        Text txt       = labelObj.AddComponent<Text>();
        txt.text       = label;
        txt.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize   = fontSize;
        txt.color      = labelColor;
        txt.alignment  = TextAnchor.MiddleCenter;
        txt.resizeTextForBestFit = false;

        LayoutElement labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.preferredWidth  = buttonSize;
        labelLE.preferredHeight = fontSize + 4;

        return btn;
    }

    // -------------------------------------------------------------------------
    // BUTTON HANDLERS
    // -------------------------------------------------------------------------

    private void OnResearchClicked()
    {
        if (petroleumSystem == null) return;

        if (activeMode == PetroleumSystem.InteractionMode.Research)
        {
            // Already in research → deactivate
            petroleumSystem.ExitMode();
            activeMode = PetroleumSystem.InteractionMode.None;
        }
        else
        {
            petroleumSystem.EnterResearchMode();
            activeMode = PetroleumSystem.InteractionMode.Research;
        }

        RefreshButtonStates();
    }

    private void OnPumpClicked()
    {
        if (petroleumSystem == null) return;

        if (activeMode == PetroleumSystem.InteractionMode.PlacePump)
        {
            petroleumSystem.ExitMode();
            activeMode = PetroleumSystem.InteractionMode.None;
        }
        else
        {
            petroleumSystem.EnterPumpMode();
            activeMode = PetroleumSystem.InteractionMode.PlacePump;
        }

        RefreshButtonStates();
    }

    private void OnCancelClicked()
    {
        Close();
    }

    // -------------------------------------------------------------------------
    // VISUAL FEEDBACK
    // -------------------------------------------------------------------------

    private void RefreshButtonStates()
    {
        if (imgResearch != null)
        {
            ColorBlock cb = btnResearch.colors;
            cb.normalColor = activeMode == PetroleumSystem.InteractionMode.Research
                ? activeGlow : buttonNormal;
            btnResearch.colors = cb;
        }

        if (imgPump != null)
        {
            ColorBlock cb = btnPump.colors;
            cb.normalColor = activeMode == PetroleumSystem.InteractionMode.PlacePump
                ? activeGlow : buttonNormal;
            btnPump.colors = cb;
        }
    }

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------

    private void EnsureCanvas()
    {
        if (canvas != null) return;

        canvas = FindObjectOfType<Canvas>();
        if (canvas != null) return;

        GameObject canvasObj    = new GameObject("Canvas");
        canvas                 = canvasObj.AddComponent<Canvas>();
        canvas.renderMode      = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder    = 100;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}