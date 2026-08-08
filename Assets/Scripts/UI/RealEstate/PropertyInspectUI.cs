using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Haritada seçilen mülkün bilgi/işlem paneli. RealEstateSystem seçim yaptığında açılır.
///
/// Görseller runtime'da kurulur (prefab gerekmez) ve skill ağacının yardımcılarını
/// (SkillTreeUI + UISpriteFactory) kullanır — iki ekran aynı görsel dili konuşsun diye.
/// </summary>
public class PropertyInspectUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa sahnedeki ilk Canvas kullanılır.")]
    public Canvas canvas;

    [Header("Görünüm")]
    public float panelWidth  = 460f;
    public float rightMargin = 28f;
    public Color background  = new Color(0.07f, 0.08f, 0.11f, 0.97f);
    public Color border      = new Color(0.35f, 0.40f, 0.48f, 1f);
    public Color textColor   = new Color(0.95f, 0.97f, 1.00f);
    public Color mutedColor  = new Color(0.72f, 0.77f, 0.85f);
    public Color accentColor = new Color(0.45f, 0.80f, 1.00f);
    public Color warnColor   = new Color(0.95f, 0.55f, 0.45f);
    public Color goodColor   = new Color(0.42f, 0.88f, 0.58f);

    private RectTransform    root;
    private TextMeshProUGUI  title;
    private TextMeshProUGUI  body;
    private TextMeshProUGUI  portfolio;
    private Button           primaryButton;   //Satın Al / Sat
    private TextMeshProUGUI  primaryLabel;
    private Button           repairButton;    //Onar (sadece hasarlıyken)
    private TextMeshProUGUI  repairLabel;

    private bool built;
    private RealEstateSystem.PropertyQuote current;
    private bool hasCurrent;

    private void OnEnable()
    {
        RealEstateSystem.OnPropertySelected += HandleSelected;
        RealEstateSystem.OnSelectionCleared += HandleCleared;
        RealEstateSystem.OnPortfolioChanged += HandlePortfolioChanged;
        RealEstateSystem.OnRentTick         += HandleRentTick;
        GameStatManager.OnStatChanged       += HandleStatChanged;
    }

    private void OnDisable()
    {
        RealEstateSystem.OnPropertySelected -= HandleSelected;
        RealEstateSystem.OnSelectionCleared -= HandleCleared;
        RealEstateSystem.OnPortfolioChanged -= HandlePortfolioChanged;
        RealEstateSystem.OnRentTick         -= HandleRentTick;
        GameStatManager.OnStatChanged       -= HandleStatChanged;
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
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
    }

    private void Build()
    {
        if (built) return;
        built = true;

        EnsureCanvas();

        //panel — ekranın sağ ortasında sabit bir "inceleme" kutusu
        Image panel = SkillTreeUI.NewImage("PropertyInspector", canvas.transform, UISpriteFactory.RoundedRect(16, 6));
        panel.color = background;
        panel.raycastTarget = true;

        root = panel.rectTransform;
        root.anchorMin = new Vector2(1f, 0.5f);
        root.anchorMax = new Vector2(1f, 0.5f);
        root.pivot     = new Vector2(1f, 0.5f);
        root.anchoredPosition = new Vector2(-rightMargin, 0f);
        root.sizeDelta = new Vector2(panelWidth, 100f);

        Image edge = SkillTreeUI.NewImage("Edge", root, UISpriteFactory.RoundedRectOutline(16, 2, 6));
        SkillTreeUI.Stretch(edge.rectTransform);
        edge.color = border;
        edge.raycastTarget = false;
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 20, 20);
        layout.spacing = 10f;
        layout.childControlWidth   = true;
        layout.childControlHeight  = true;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        title = SkillTreeUI.NewText("Title", root, 32f, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.color = textColor;

        body = SkillTreeUI.NewText("Body", root, 24f, TextAlignmentOptions.TopLeft);
        body.color = mutedColor;
        body.lineSpacing = 6f;

        primaryButton = CreateButton("Primary", out primaryLabel);
        repairButton  = CreateButton("Repair",  out repairLabel);

        portfolio = SkillTreeUI.NewText("Portfolio", root, 21f, TextAlignmentOptions.TopLeft);
        portfolio.color = mutedColor;

        Button close = CreateButton("Close", out TextMeshProUGUI closeLabel);
        closeLabel.text = "Kapat";
        close.onClick.AddListener(() =>
        {
            if (RealEstateSystem.Instance != null) RealEstateSystem.Instance.ClearSelection();
        });

        primaryButton.onClick.AddListener(OnPrimaryClicked);
        repairButton.onClick.AddListener(OnRepairClicked);

        root.gameObject.SetActive(false);
    }

    private Button CreateButton(string name, out TextMeshProUGUI label)
    {
        Image bg = SkillTreeUI.NewImage("Btn_" + name, root, UISpriteFactory.RoundedRect(12, 5));
        bg.color = new Color(0.16f, 0.18f, 0.23f, 0.95f);
        bg.raycastTarget = true;

        LayoutElement element = bg.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 54f;

        Image edge = SkillTreeUI.NewImage("Edge", bg.rectTransform, UISpriteFactory.RoundedRectOutline(12, 2, 5));
        SkillTreeUI.Stretch(edge.rectTransform);
        edge.color = new Color(0.55f, 0.60f, 0.70f, 0.7f);
        edge.raycastTarget = false;

        label = SkillTreeUI.NewText("Label", bg.rectTransform, 24f, TextAlignmentOptions.Center);
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
    // EVENTLER
    // -------------------------------------------------------------------------

    private void HandleSelected(RealEstateSystem.PropertyQuote quote)
    {
        Build();
        current    = quote;
        hasCurrent = true;
        root.gameObject.SetActive(true);
        Refresh();
    }

    private void HandleCleared()
    {
        hasCurrent = false;
        if (root != null) root.gameObject.SetActive(false);
    }

    private void HandlePortfolioChanged(RealEstateSystem.PropertyQuote quote)
    {
        if (!hasCurrent || quote.tile != current.tile) return;
        current = quote;
        Refresh();
    }

    private void HandleRentTick(float amount)
    {
        if (hasCurrent) Refresh();
    }

    private void HandleStatChanged(StatType stat, float oldValue, float newValue)
    {
        //sadece para durumu butonların alınabilirliğini etkiler
        if (stat == StatType.Wealth && hasCurrent) Refresh();
    }

    // -------------------------------------------------------------------------
    // GÖRÜNÜM
    // -------------------------------------------------------------------------

    private void Refresh()
    {
        RealEstateSystem system = RealEstateSystem.Instance;
        if (system == null || !hasCurrent) return;

        //fiyat/durum güncel olsun (deprem arada hasar vermiş olabilir)
        current = system.GetQuote(current.tile);

        title.text = current.owned ? "Mülkün" : "Satılık Mülk";
        title.color = current.owned ? accentColor : textColor;

        GameStatManager stats = GameStatManager.Instance;
        bool canAfford = stats == null || stats.HasEnoughWealth(current.price);

        string state = current.broken
            ? "<color=#F28C7A>Hasarlı — kira getirmiyor</color>"
            : "<color=#6BE08F>Sağlam</color>";

        string rentLine = current.broken
            ? "—"
            : $"{SkillTreeUI.FormatMoney(current.rentPerSecond * 60f)} / dk";

        body.text =
            $"Konum: ({current.tile.x}, {current.tile.y})\n" +
            $"Durum: {state}\n" +
            $"Değer: <b>{SkillTreeUI.FormatMoney(current.price)}</b>\n" +
            $"Kira: <b>{rentLine}</b>\n" +
            $"Belediyeye: {current.distanceToHall} kare\n" +
            $"Yola: {current.distanceToRoad} kare";

        if (current.owned)
        {
            primaryLabel.text  = $"Sat  ·  {SkillTreeUI.FormatMoney(current.sellValue)}";
            primaryLabel.color = textColor;
            primaryButton.interactable = true;

            repairButton.gameObject.SetActive(current.broken);
            if (current.broken)
            {
                bool canRepair = stats == null || stats.HasEnoughWealth(current.rebuildCost);
                repairLabel.text  = $"Onar  ·  {SkillTreeUI.FormatMoney(current.rebuildCost)}";
                repairLabel.color = canRepair ? goodColor : warnColor;
                repairButton.interactable = canRepair;
            }
        }
        else
        {
            primaryLabel.text  = $"Satın Al  ·  {SkillTreeUI.FormatMoney(current.price)}";
            primaryLabel.color = canAfford ? textColor : warnColor;
            primaryButton.interactable = canAfford;

            repairButton.gameObject.SetActive(false);
        }

        int count = system.OwnedCount;
        portfolio.text = count == 0
            ? "Henüz mülkün yok."
            : $"{count} mülk  ·  toplam {SkillTreeUI.FormatMoney(system.GetTotalRentPerSecond() * 60f)} / dk";

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void OnPrimaryClicked()
    {
        RealEstateSystem system = RealEstateSystem.Instance;
        if (system == null || !hasCurrent) return;

        if (current.owned) system.Sell(current.tile);
        else               system.Buy(current.tile);

        Refresh();
    }

    private void OnRepairClicked()
    {
        RealEstateSystem system = RealEstateSystem.Instance;
        if (system == null || !hasCurrent) return;

        system.Rebuild(current.tile);
        Refresh();
    }
}
