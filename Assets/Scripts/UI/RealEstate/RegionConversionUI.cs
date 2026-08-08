using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bölge dönüşümünün onay kutusu.
///
/// MOD BUTONU YOKTUR — mod, skill ağacındaki a38/a40 node'una tıklayınca (aktif yetenek)
/// açılır. Bu panel yalnızca modda görünür: önce "sınır çiz" yönergesi, çizim kapandıktan
/// sonra alan/fiyat ve Onayla/Vazgeç, onaydan sonra inşaat ilerlemesi.
/// </summary>
public class RegionConversionUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa sahnedeki ilk Canvas kullanılır.")]
    public Canvas canvas;

    [Header("Görünüm")]
    public Vector2 anchoredOffset = new Vector2(0f, 36f);
    public float   panelWidth     = 460f;
    public Color   background     = new Color(0.07f, 0.08f, 0.11f, 0.97f);
    public Color   border         = new Color(0.35f, 0.40f, 0.48f, 1f);
    public Color   textColor      = new Color(0.95f, 0.97f, 1.00f);
    public Color   mutedColor     = new Color(0.72f, 0.77f, 0.85f);
    public Color   warnColor      = new Color(0.95f, 0.55f, 0.45f);
    public Color   goodColor      = new Color(0.42f, 0.88f, 0.58f);

    private RectTransform   root;
    private TextMeshProUGUI title;
    private TextMeshProUGUI status;
    private RectTransform   buttonRow;
    private Button          confirmButton, cancelButton;
    private TextMeshProUGUI confirmLabel;
    private Image           progressFill;
    private RectTransform   progressTrack;

    private bool built;

    private void OnEnable()
    {
        RegionConversionSystem.OnModeEntered          += HandleModeEntered;
        RegionConversionSystem.OnModeExited           += HandleModeExited;
        RegionConversionSystem.OnSelectionChanged     += HandleSelectionChanged;
        RegionConversionSystem.OnSelectionCleared     += HandleSelectionCleared;
        RegionConversionSystem.OnConstructionProgress += HandleProgress;
        RegionConversionSystem.OnConverted            += HandleConverted;
    }

    private void OnDisable()
    {
        RegionConversionSystem.OnModeEntered          -= HandleModeEntered;
        RegionConversionSystem.OnModeExited           -= HandleModeExited;
        RegionConversionSystem.OnSelectionChanged     -= HandleSelectionChanged;
        RegionConversionSystem.OnSelectionCleared     -= HandleSelectionCleared;
        RegionConversionSystem.OnConstructionProgress -= HandleProgress;
        RegionConversionSystem.OnConverted            -= HandleConverted;
    }

    // -------------------------------------------------------------------------
    // KURULUM
    // -------------------------------------------------------------------------

    private void EnsureCanvas()
    {
        if (canvas != null) return;

        canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null) return;

        GameObject go = new GameObject("Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
    }

    private void Build()
    {
        if (built) return;
        built = true;

        EnsureCanvas();

        Image panel = SkillTreeUI.NewImage("RegionConversionPrompt", canvas.transform,
                                           UISpriteFactory.RoundedRect(16, 6));
        panel.color = background;
        panel.raycastTarget = true;

        root = panel.rectTransform;
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot     = new Vector2(0.5f, 0f);
        root.anchoredPosition = anchoredOffset;
        root.sizeDelta = new Vector2(panelWidth, 100f);

        Image edge = SkillTreeUI.NewImage("Edge", root, UISpriteFactory.RoundedRectOutline(16, 2, 6));
        SkillTreeUI.Stretch(edge.rectTransform);
        edge.color = border;
        edge.raycastTarget = false;
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 16, 16);
        layout.spacing = 9f;
        layout.childControlWidth  = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        title = SkillTreeUI.NewText("Title", root, 26f, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.color = textColor;

        status = SkillTreeUI.NewText("Status", root, 21f, TextAlignmentOptions.Center);
        status.color = mutedColor;

        //inşaat çubuğu
        Image track = SkillTreeUI.NewImage("ProgressTrack", root, UISpriteFactory.RoundedRect(6, 3));
        track.color = new Color(1f, 1f, 1f, 0.10f);
        track.raycastTarget = false;
        progressTrack = track.rectTransform;
        track.gameObject.AddComponent<LayoutElement>().preferredHeight = 10f;

        progressFill = SkillTreeUI.NewImage("Fill", progressTrack, UISpriteFactory.RoundedRect(6, 3));
        progressFill.color = goodColor;
        progressFill.raycastTarget = false;
        progressFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        progressFill.rectTransform.pivot     = new Vector2(0f, 0.5f);
        progressFill.rectTransform.offsetMin = Vector2.zero;
        progressFill.rectTransform.offsetMax = Vector2.zero;

        //butonlar yan yana
        buttonRow = SkillTreeUI.NewRect("Buttons", root);
        HorizontalLayoutGroup row = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 10f;
        row.childControlWidth  = true;
        row.childControlHeight = true;
        row.childForceExpandWidth  = true;
        row.childForceExpandHeight = false;
        buttonRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;

        confirmButton = CreateButton(buttonRow, "Confirm", out confirmLabel);
        cancelButton  = CreateButton(buttonRow, "Cancel",  out TextMeshProUGUI cancelLabel);
        cancelLabel.text = "Vazgeç";

        confirmButton.onClick.AddListener(() =>
        {
            if (RegionConversionSystem.Instance != null) RegionConversionSystem.Instance.ConfirmSelection();
        });

        cancelButton.onClick.AddListener(() =>
        {
            if (RegionConversionSystem.Instance != null) RegionConversionSystem.Instance.ExitMode();
        });

        root.gameObject.SetActive(false);
    }

    private Button CreateButton(RectTransform parent, string name, out TextMeshProUGUI label)
    {
        Image bg = SkillTreeUI.NewImage("Btn_" + name, parent, UISpriteFactory.RoundedRect(12, 5));
        bg.color = new Color(0.16f, 0.18f, 0.23f, 0.95f);
        bg.raycastTarget = true;

        Image edge = SkillTreeUI.NewImage("Edge", bg.rectTransform, UISpriteFactory.RoundedRectOutline(12, 2, 5));
        SkillTreeUI.Stretch(edge.rectTransform);
        edge.color = new Color(0.55f, 0.60f, 0.70f, 0.7f);
        edge.raycastTarget = false;

        label = SkillTreeUI.NewText("Label", bg.rectTransform, 23f, TextAlignmentOptions.Center);
        SkillTreeUI.Stretch(label.rectTransform);
        label.fontStyle = FontStyles.Bold;
        label.color = textColor;

        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        ColorBlock colors = button.colors;
        colors.normalColor      = new Color(0.85f, 0.85f, 0.85f);
        colors.highlightedColor = Color.white;
        colors.pressedColor     = new Color(0.65f, 0.65f, 0.65f);
        colors.selectedColor    = new Color(0.85f, 0.85f, 0.85f);
        button.colors = colors;

        return button;
    }

    // -------------------------------------------------------------------------
    // DURUMLAR
    // -------------------------------------------------------------------------

    private void HandleModeEntered(MapDecorPlacer.ConvertTarget target)
    {
        Build();
        root.gameObject.SetActive(true);

        title.text = target == MapDecorPlacer.ConvertTarget.Cities
            ? "Şehir Kur"
            : "Sanayi Bölgesi Kur";

        ShowInstruction();
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void ShowInstruction()
    {
        status.text  = "Boş arazi üzerinde <b>sağ tuşu</b> basılı tutup sınırı çiz.\n" +
                       "<color=#9AA5B1>Bıraktığında şekil kapanır. Sol tuş haritayı kaydırır. " +
                       "Esc: vazgeç.</color>";
        status.color = mutedColor;

        //çizim sırasında da çıkış yolu dursun — sadece Onayla gizlenir
        SetButtonsVisible(true);
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        SetProgressVisible(false);
    }

    private void HandleModeExited()
    {
        if (root != null) root.gameObject.SetActive(false);
    }

    private void HandleSelectionCleared()
    {
        if (root == null || !root.gameObject.activeSelf) return;
        ShowInstruction();
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void HandleSelectionChanged(int tileCount, float cost, bool ok)
    {
        Build();

        if (tileCount <= 0)
        {
            status.text  = "<color=#F28C7A>Kapalı alan içinde dönüştürülebilir arazi yok.</color>\n" +
                           "<color=#9AA5B1>Tekrar çiz ya da Vazgeç.</color>";
            status.color = warnColor;

            SetButtonsVisible(true);
            confirmButton.gameObject.SetActive(false);
            SetProgressVisible(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            return;
        }

        status.text  = $"{tileCount} kare  ·  <b>{SkillTreeUI.FormatMoney(cost)}</b>";
        status.color = ok ? mutedColor : warnColor;

        confirmLabel.text  = ok ? "Onayla" : "Yetersiz";
        confirmLabel.color = ok ? goodColor : warnColor;
        confirmButton.interactable = ok;

        SetButtonsVisible(true);
        confirmButton.gameObject.SetActive(true);
        SetProgressVisible(false);

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void HandleProgress(float t)
    {
        if (root == null) return;

        SetButtonsVisible(false);
        SetProgressVisible(true);

        status.text  = "İnşaat sürüyor…";
        status.color = mutedColor;

        if (progressFill != null)
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
    }

    private void HandleConverted(MapDecorPlacer.ConvertTarget target, int tiles, float cost)
    {
        if (root == null) return;

        string what = target == MapDecorPlacer.ConvertTarget.Cities ? "şehre" : "sanayiye";
        status.text  = $"<color=#6BE08F>{tiles} kare {what} çevrildi.</color>";
        status.color = goodColor;

        SetProgressVisible(false);
        SetButtonsVisible(false);
    }

    private void SetButtonsVisible(bool visible)
    {
        if (buttonRow != null) buttonRow.gameObject.SetActive(visible);
    }

    private void SetProgressVisible(bool visible)
    {
        if (progressTrack != null) progressTrack.gameObject.SetActive(visible);
        if (visible && progressFill != null)
            progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
    }
}
