using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skill tree ekranını veriden kurar: node'lar, bağlantılar, ağaç başlıkları.
/// Sahnede sadece bu component var — geri kalan her şey runtime'da oluşturulur,
/// böylece 82 node elle yerleştirilmek zorunda kalmaz.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SkillTreeView : MonoBehaviour
{
    [Header("Veri")]
    public SkillDatabase database;
    public SkillTreeLayout layout;
    public SkillTreeStyle style;

    [Header("Davranış")]
    [Tooltip("Açılışta üç ağacın tamamını sığdır (çok küçük görünür). Kapalıysa initialZoom + focusSkillId kullanılır.")]
    public bool fitOnFirstOpen = false;
    [Tooltip("Açılış zoom'u — yazılar okunabilsin diye 1'e yakın tutulur")]
    public float initialZoom = 0.85f;
    [Tooltip("En fazla uzaklaşma: ağacın genişliğini tam sığdıran zoom'un kaç katı. " +
             "1 = ağaç ekranı tam doldurur. Düşürmek dikeyde daha fazlasını gösterir ama " +
             "sağda boş alan bırakır — ağaç geniş değil, uzun.")]
    public float minZoomFactor = 1f;
    [Tooltip("Ağaç ilk açıldığında ortalanacak skill")]
    public string focusSkillId = "a1";

    [Header("Yerleşim Modu")]
    [Tooltip("Kapalı: ağaçlar soldan sağa büyür, üst üste dizilir. " +
             "Açık: ağaçlar yukarıdan aşağı büyür, yan yana dizilir. " +
             "Oyun içindeki butonla da değiştirilebilir.")]
    public bool verticalLayout = false;

    [Header("TEST")]
    [Tooltip("Açıkken tıklanan her skill ön koşul ve para aranmadan açılır. " +
             "Sadece ağaç görselini denemek için — normalde KAPALI kalmalı.")]
    public bool testUnlockAnything = false;

    [Header("Kapatma Butonu")]
    [Tooltip("Ağacın üstünde kalması gereken buton. Boş bırakılırsa aşağıdaki isimle aranır.")]
    public RectTransform closeButton;
    public string closeButtonName = "MenuClose";

    private RectTransform content;
    private SkillTreeConnectors connectors;
    private SkillTreeConnectors connectorGlow;
    private SkillTreeViewport viewport;
    private SkillTreeTooltip tooltip;

    private readonly Dictionary<string, SkillNodeView> nodes = new Dictionary<string, SkillNodeView>();
    private readonly List<SkillTreeConnectors.Edge> edges = new List<SkillTreeConnectors.Edge>();
    private readonly List<Skill> missingBuffer = new List<Skill>();

    private readonly List<GameObject> bandObjects = new List<GameObject>();
    private TMPro.TextMeshProUGUI layoutButtonLabel;

    private bool built;
    private bool focusedOnce;

    private void OnEnable()
    {
        EnsureBuilt();
        RaiseCloseButton();

        SkillEvents.OnSkillUnlocked += HandleSkillUnlocked;
        SkillEvents.OnSkillBlocked += HandleSkillBlocked;
        GameStatManager.OnStatChanged += HandleStatChanged;
        SkillTreeManager.OnSkillActivated += HandleSkillActivated;

        Refresh();

        if (!focusedOnce)
        {
            focusedOnce = true;

            //ya tamamını göster ya da bir skill'e odaklan — ikisi birbirini bozar
            if (fitOnFirstOpen)
            {
                FitToView();
            }
            else if (viewport != null)
            {
                viewport.SetZoom(initialZoom, true);
                FocusOn(focusSkillId);
            }
        }
    }

    private void OnDisable()
    {
        SkillEvents.OnSkillUnlocked -= HandleSkillUnlocked;
        SkillEvents.OnSkillBlocked -= HandleSkillBlocked;
        GameStatManager.OnStatChanged -= HandleStatChanged;
        SkillTreeManager.OnSkillActivated -= HandleSkillActivated;

        if (tooltip != null) tooltip.Hide();
    }

    // ==================== KURULUM ====================

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        if (style == null)
        {
            Debug.LogError("[SkillTreeView] Style atanmamış — ağaç kurulamıyor.", this);
            return;
        }

        if (database == null && SkillTreeManager.Instance != null)
            database = SkillTreeManager.Instance.database;

        if (database == null || layout == null)
        {
            Debug.LogError("[SkillTreeView] Database veya Layout atanmamış — ağaç kurulamıyor.", this);
            return;
        }

        //ters yön: manager'ın database'i boşsa kendi referansımızı veriyoruz.
        //Boş kalırsa TryUnlock/ForceUnlock skill'i bulamaz ve tıklamalar sessizce hiçbir şey yapmaz.
        if (SkillTreeManager.Instance != null && SkillTreeManager.Instance.database == null)
        {
            SkillTreeManager.Instance.database = database;
            Debug.Log("[SkillTreeView] SkillTreeManager.database boştu — view'daki database bağlandı.", this);
        }

        SetupViewport();
        BuildNodes();
        BuildEdges();
        BuildBranchButtons();
        BuildTooltip();
        ApplyLayout();
    }

    private void SetupViewport()
    {
        //rect sahnede ayarlanır (panel çerçevesinin içinde kalsın diye) — burada ezilmez
        RectTransform self = GetComponent<RectTransform>();

        //sabit opak zemin — panel altındaki harita/placeholder görünmesin.
        //content'in DIŞINDA olduğu için pan/zoom ile kaymaz. Sürükleme de burada yakalanır.
        Image backdrop = GetComponent<Image>();
        if (backdrop == null) backdrop = gameObject.AddComponent<Image>();
        backdrop.sprite = null;
        backdrop.color = style.background;
        backdrop.raycastTarget = true;

        //ince kareli desen — zemin düz siyah kalmasın
        if (style.backgroundGrid.a > 0f)
        {
            Image grid = SkillTreeUI.NewImage("Grid", self, UISpriteFactory.GridTile((int)style.gridSize));
            SkillTreeUI.Stretch(grid.rectTransform);
            grid.type = Image.Type.Tiled;
            grid.color = style.backgroundGrid;
            grid.raycastTarget = false;
        }

        if (style.vignetteStrength > 0f)
        {
            Image vignette = SkillTreeUI.NewImage("Vignette", self, UISpriteFactory.Vignette());
            SkillTreeUI.Stretch(vignette.rectTransform);
            vignette.color = new Color(0f, 0f, 0f, style.vignetteStrength);
            vignette.raycastTarget = false;
        }

        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();

        content = SkillTreeUI.NewRect("Content", self);
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;

        //parlama katmanı önce eklenir ki ana çizgilerin ALTINDA kalsın
        RectTransform glowRect = SkillTreeUI.NewRect("ConnectorGlow", content);
        SkillTreeUI.Stretch(glowRect);
        connectorGlow = glowRect.gameObject.AddComponent<SkillTreeConnectors>();
        connectorGlow.Setup(style, glow: true);

        RectTransform connectorRect = SkillTreeUI.NewRect("Connectors", content);
        SkillTreeUI.Stretch(connectorRect);
        connectors = connectorRect.gameObject.AddComponent<SkillTreeConnectors>();
        connectors.Setup(style);
        connectors.OnEdgeReached = HandleEdgeReached;

        //çerçeve en üstte — content'in üstüne binerek alanı sınırlar
        if (style.frameColor.a > 0f)
        {
            Image frame = SkillTreeUI.NewImage("Frame", self,
                UISpriteFactory.RoundedRectOutline(18, Mathf.Max(1, (int)style.frameThickness)));
            SkillTreeUI.Stretch(frame.rectTransform);
            frame.color = style.frameColor;
            frame.raycastTarget = false;
            frame.transform.SetAsLastSibling();
        }

        viewport = GetComponent<SkillTreeViewport>();
        if (viewport == null) viewport = gameObject.AddComponent<SkillTreeViewport>();
        viewport.Bind(content);
    }

    private void BuildNodes()
    {
        if (layout.nodes.Count == 0) return;

        foreach (SkillTreeNodeLayout entry in layout.nodes)
        {
            Skill skill = FindSkill(entry.skillId);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillTreeView] Layout'ta '{entry.skillId}' var ama database'de yok — atlandı.", this);
                continue;
            }

            RectTransform rect = SkillTreeUI.NewRect("Node_" + entry.skillId, content);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            SkillNodeView node = rect.gameObject.AddComponent<SkillNodeView>();
            node.Build(this, skill, style, entry.branches);
            nodes[entry.skillId] = node;
        }
    }

    private Vector2 layoutCenter;

    /// <summary>
    /// Ham yerleşim koordinatlarını üretir (henüz ortalanmamış).
    /// Yatay mod: ağaç soldan sağa büyür, ağaçlar üst üste.
    /// Dikey mod: ağaç yukarıdan aşağı büyür, ağaçlar yan yana.
    /// </summary>
    private Dictionary<string, Vector2> ComputeRawPositions()
    {
        var result = new Dictionary<string, Vector2>(layout.nodes.Count);

        if (!verticalLayout)
        {
            foreach (SkillTreeNodeLayout entry in layout.nodes)
                result[entry.skillId] = new Vector2(
                    entry.column * style.columnSpacing,
                    -entry.row * style.rowSpacing);

            return result;
        }

        //dikey mod: her ağacın kendi şerit aralığı hesaplanır, ağaçlar soldan sağa dizilir
        var branches = new List<SkillBranch>();
        var minRow = new Dictionary<SkillBranch, float>();
        var maxRow = new Dictionary<SkillBranch, float>();

        foreach (SkillTreeNodeLayout entry in layout.nodes)
        {
            if (!minRow.ContainsKey(entry.branches))
            {
                branches.Add(entry.branches);
                minRow[entry.branches] = entry.row;
                maxRow[entry.branches] = entry.row;
                continue;
            }

            minRow[entry.branches] = Mathf.Min(minRow[entry.branches], entry.row);
            maxRow[entry.branches] = Mathf.Max(maxRow[entry.branches], entry.row);
        }

        //yatay moddaki üst-alt sırası, dikey modda sol-sağ sırası olur
        branches.Sort((a, b) => minRow[a].CompareTo(minRow[b]));

        var offsetX = new Dictionary<SkillBranch, float>();
        float cursor = 0f;

        foreach (SkillBranch branch in branches)
        {
            offsetX[branch] = cursor;
            float laneSpan = maxRow[branch] - minRow[branch];
            cursor += (laneSpan + style.verticalTreeGap) * style.verticalLaneSpacing;
        }

        foreach (SkillTreeNodeLayout entry in layout.nodes)
        {
            float lane = entry.row - minRow[entry.branches];
            result[entry.skillId] = new Vector2(
                offsetX[entry.branches] + lane * style.verticalLaneSpacing,
                -entry.column * style.verticalDepthSpacing);
        }

        return result;
    }

    /// <summary>Seçili moda göre node'ları yerleştirir, bantları ve bağlantıları yeniler.</summary>
    private void ApplyLayout()
    {
        if (content == null || layout.nodes.Count == 0) return;

        Dictionary<string, Vector2> raw = ComputeRawPositions();

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (KeyValuePair<string, Vector2> pair in raw)
        {
            minX = Mathf.Min(minX, pair.Value.x); maxX = Mathf.Max(maxX, pair.Value.x);
            minY = Mathf.Min(minY, pair.Value.y); maxY = Mathf.Max(maxY, pair.Value.y);
        }

        layoutCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        content.sizeDelta = new Vector2(
            maxX - minX + style.contentPadding.x * 2f,
            maxY - minY + style.contentPadding.y * 2f
        );

        float labelWidth = (verticalLayout ? style.verticalLaneSpacing : style.columnSpacing) - 40f;

        foreach (KeyValuePair<string, SkillNodeView> pair in nodes)
        {
            if (!raw.TryGetValue(pair.Key, out Vector2 position)) continue;

            pair.Value.Rect.anchoredPosition = position - layoutCenter;
            pair.Value.SetLabelWidth(labelWidth);
        }

        RebuildBands(raw);

        if (connectors != null)
        {
            connectors.SetVertical(verticalLayout);
            connectorGlow.SetVertical(verticalLayout);
        }

        ApplyZoomLimits();
    }

    /// <summary>Yerleşim modunu değiştirir — iki görünümü karşılaştırmak için.</summary>
    public void ToggleLayout()
    {
        verticalLayout = !verticalLayout;
        ApplyLayout();

        if (layoutButtonLabel != null)
            layoutButtonLabel.text = verticalLayout ? "Yan Yana" : "Üst Üste";

        //mod değişince eski konum anlamsız — köke geri dön
        if (viewport != null) viewport.SetZoom(initialZoom, true);
        FocusOn(focusSkillId);
    }

    private void BuildEdges()
    {
        edges.Clear();

        foreach (KeyValuePair<string, SkillNodeView> pair in nodes)
        {
            Skill skill = pair.Value.Skill;
            if (skill.prerequisites == null) continue;

            foreach (Skill prerequisite in skill.prerequisites)
            {
                if (prerequisite == null) continue;
                if (!nodes.TryGetValue(prerequisite.id, out SkillNodeView parent)) continue;

                edges.Add(new SkillTreeConnectors.Edge { from = parent, to = pair.Value });
            }
        }

        connectors.SetEdges(edges);
        connectorGlow.SetEdges(edges);
    }

    /// <summary>Her ağacın arkasına hafif bir bant ve başlık koyar — üç ağaç birbirinden ayrışsın.</summary>
    /// <summary>
    /// Her ağacın arkasına bant ve başlık koyar. Bantlar moda göre yön değiştirir:
    /// yatay modda üst üste şeritler, dikey modda yan yana sütunlar.
    /// Mod değişiminde yeniden kurulur.
    /// </summary>
    private void RebuildBands(Dictionary<string, Vector2> raw)
    {
        for (int i = 0; i < bandObjects.Count; i++)
            if (bandObjects[i] != null) Destroy(bandObjects[i]);
        bandObjects.Clear();

        if (layout.headers == null || layout.headers.Count == 0) return;

        //her ağacın node'larının sınırları (ortalanmış koordinatta)
        var bounds = new Dictionary<SkillBranch, Rect>();

        foreach (SkillTreeNodeLayout entry in layout.nodes)
        {
            if (!raw.TryGetValue(entry.skillId, out Vector2 p)) continue;
            Vector2 c = p - layoutCenter;

            if (!bounds.TryGetValue(entry.branches, out Rect r))
            {
                bounds[entry.branches] = new Rect(c.x, c.y, 0f, 0f);
                continue;
            }

            float xMin = Mathf.Min(r.xMin, c.x), xMax = Mathf.Max(r.xMax, c.x);
            float yMin = Mathf.Min(r.yMin, c.y), yMax = Mathf.Max(r.yMax, c.y);
            bounds[entry.branches] = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        //bant ekseni: dikey modda X, yatay modda aşağı yön (-Y)
        var ordered = new List<SkillBranch>(bounds.Keys);
        ordered.Sort((a, b) => AxisMin(bounds[a]).CompareTo(AxisMin(bounds[b])));

        float pad = style.nodeSize * 0.75f;

        for (int i = 0; i < ordered.Count; i++)
        {
            SkillBranch branch = ordered[i];
            Rect r = bounds[branch];

            //komşu bantlarla ortada buluş — aralarında renksiz şerit kalmasın
            float start = i == 0
                ? AxisMin(r) - style.bandOverdraw
                : (AxisMax(bounds[ordered[i - 1]]) + AxisMin(r)) * 0.5f;

            float end = i == ordered.Count - 1
                ? AxisMax(r) + style.bandOverdraw
                : (AxisMax(r) + AxisMin(bounds[ordered[i + 1]])) * 0.5f;

            Color accent = style.AccentFor(branch);
            Image band = SkillTreeUI.NewImage("Band_" + branch, content, null);
            RectTransform br = band.rectTransform;
            br.anchorMin = new Vector2(0.5f, 0.5f);
            br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);

            float thickness = Mathf.Abs(end - start);
            float length = (verticalLayout ? content.sizeDelta.y : content.sizeDelta.x) + style.bandOverdraw * 2f;

            if (verticalLayout)
            {
                br.sizeDelta = new Vector2(thickness, length);
                br.anchoredPosition = new Vector2((start + end) * 0.5f, 0f);
            }
            else
            {
                br.sizeDelta = new Vector2(length, thickness);
                br.anchoredPosition = new Vector2(0f, -(start + end) * 0.5f);
            }

            band.color = new Color(accent.r, accent.g, accent.b, 0.06f);
            band.transform.SetAsFirstSibling();
            bandObjects.Add(band.gameObject);

            //başlık: ağacın başladığı yerin üstünde/solunda
            string title = TitleFor(branch);
            TextMeshProUGUI label = SkillTreeUI.NewText("Title_" + branch, content,
                style.titleFontSize, TextAlignmentOptions.Left);
            RectTransform tr = label.rectTransform;
            tr.anchorMin = new Vector2(0.5f, 0.5f);
            tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.sizeDelta = new Vector2(700f, 70f);
            tr.anchoredPosition = verticalLayout
                ? new Vector2(r.xMin - style.nodeSize * 0.5f, r.yMax + style.nodeSize * 1.15f)
                : new Vector2(-content.sizeDelta.x * 0.5f + style.contentPadding.x * 0.4f,
                              r.yMax + style.nodeSize * 0.9f);

            label.text = title;
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.color = style.ReadableAccent(branch);
            label.characterSpacing = 8f;
            label.transform.SetSiblingIndex(1);
            bandObjects.Add(label.gameObject);
        }
    }

    private float AxisMin(Rect r) => verticalLayout ? r.xMin : -r.yMax;
    private float AxisMax(Rect r) => verticalLayout ? r.xMax : -r.yMin;

    private string TitleFor(SkillBranch branch)
    {
        foreach (SkillTreeLayout.BranchHeader header in layout.headers)
            if (header.branch == branch) return header.title;

        return branch.ToString();
    }

    /// <summary>Ağaçlar arası hızlı geçiş için sabit kısayol butonları (pan/zoom'dan etkilenmez).</summary>
    private void BuildBranchButtons()
    {
        if (!style.showBranchButtons || layout.headers == null || layout.headers.Count == 0) return;

        RectTransform self = GetComponent<RectTransform>();
        RectTransform bar = SkillTreeUI.NewRect("BranchButtons", self);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(0f, 1f);
        bar.pivot = new Vector2(0f, 1f);
        bar.anchoredPosition = new Vector2(22f, -22f);
        bar.SetAsLastSibling();

        HorizontalLayoutGroup group = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = 10f;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;

        ContentSizeFitter fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        List<SkillTreeLayout.BranchHeader> ordered = new List<SkillTreeLayout.BranchHeader>(layout.headers);
        ordered.Sort((a, b) => a.rowStart.CompareTo(b.rowStart));

        foreach (SkillTreeLayout.BranchHeader header in ordered)
            CreateBranchButton(bar, header);

        CreateLayoutButton(bar);
    }

    /// <summary>İki yerleşim modu arasında geçiş butonu — ikisini karşılaştırmak için.</summary>
    private void CreateLayoutButton(RectTransform parent)
    {
        Image background = SkillTreeUI.NewImage("Btn_Layout", parent, UISpriteFactory.RoundedRect(12, 5));
        background.color = new Color(0.16f, 0.18f, 0.23f, 0.95f);
        background.raycastTarget = true;

        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = style.branchButtonSize.x;
        element.preferredHeight = style.branchButtonSize.y;

        Image edge = SkillTreeUI.NewImage("Edge", background.rectTransform, UISpriteFactory.RoundedRectOutline(12, 2, 5));
        SkillTreeUI.Stretch(edge.rectTransform);
        edge.color = new Color(0.55f, 0.60f, 0.70f, 0.7f);
        edge.raycastTarget = false;

        layoutButtonLabel = SkillTreeUI.NewText("Label", background.rectTransform,
            style.branchButtonFontSize, TextAlignmentOptions.Center);
        SkillTreeUI.Stretch(layoutButtonLabel.rectTransform);
        layoutButtonLabel.text = verticalLayout ? "Yan Yana" : "Üst Üste";
        layoutButtonLabel.fontStyle = FontStyles.Bold;
        layoutButtonLabel.color = new Color(0.90f, 0.93f, 0.97f);

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.82f, 0.82f, 0.82f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        colors.selectedColor = new Color(0.82f, 0.82f, 0.82f);
        button.colors = colors;

        button.onClick.AddListener(ToggleLayout);
    }

    private void CreateBranchButton(RectTransform parent, SkillTreeLayout.BranchHeader header)
    {
        Color accent = style.AccentFor(header.branch);

        Image background = SkillTreeUI.NewImage("Btn_" + header.branch, parent, UISpriteFactory.RoundedRect(12, 5));
        background.color = new Color(accent.r * 0.28f, accent.g * 0.28f, accent.b * 0.28f, 0.95f);
        background.raycastTarget = true;

        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = style.branchButtonSize.x;
        element.preferredHeight = style.branchButtonSize.y;

        Image edge = SkillTreeUI.NewImage("Edge", background.rectTransform, UISpriteFactory.RoundedRectOutline(12, 2, 5));
        SkillTreeUI.Stretch(edge.rectTransform);
        edge.color = new Color(accent.r, accent.g, accent.b, 0.7f);
        edge.raycastTarget = false;

        TextMeshProUGUI label = SkillTreeUI.NewText("Label", background.rectTransform,
            style.branchButtonFontSize, TextAlignmentOptions.Center);
        SkillTreeUI.Stretch(label.rectTransform);
        label.text = header.title;
        label.fontStyle = FontStyles.Bold;
        label.color = style.ReadableAccent(header.branch);

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.82f, 0.82f, 0.82f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        colors.selectedColor = new Color(0.82f, 0.82f, 0.82f);
        button.colors = colors;

        SkillBranch branch = header.branch;
        button.onClick.AddListener(() => FocusBranch(branch));
    }

    /// <summary>
    /// Kapatma butonunu ağacın üstüne çıkarır.
    /// Buton SkillTreeMenu altında Mask'tan ÖNCE geldiği için, opak ağaç zemini onu örtüyor.
    /// Prefab'ın çocuk sırasını değiştirmek yerine (o prefab yigit sahnesinde de kullanılıyor)
    /// menü her açıldığında butonu en son kardeş yapıyoruz.
    /// </summary>
    private void RaiseCloseButton()
    {
        RectTransform target = closeButton != null ? closeButton : FindCloseButton();

        if (target == null)
        {
            Debug.LogWarning(
                $"[SkillTreeView] '{closeButtonName}' bulunamadı — kapatma butonu ağacın altında kalabilir. " +
                "Butonu inspector'dan closeButton alanına sürükle.", this);
            return;
        }

        target.SetAsLastSibling();
    }

    private RectTransform FindCloseButton()
    {
        if (string.IsNullOrEmpty(closeButtonName)) return null;

        //view -> Mask -> SkillTreeMenu şeklinde yukarı doğru arar
        Transform node = transform.parent;

        for (int depth = 0; node != null && depth < 4; depth++, node = node.parent)
        {
            Transform found = node.Find(closeButtonName);
            if (found != null) return found as RectTransform;
        }

        return null;
    }

    private void BuildTooltip()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null
            ? canvas.rootCanvas.GetComponent<RectTransform>()
            : GetComponent<RectTransform>();

        RectTransform rect = SkillTreeUI.NewRect("SkillTreeTooltip", canvasRect);
        rect.SetAsLastSibling();
        tooltip = rect.gameObject.AddComponent<SkillTreeTooltip>();
        tooltip.Build(style, canvasRect);
    }

    private Skill FindSkill(string id)
    {
        if (database == null || database.allSkills == null) return null;

        for (int i = 0; i < database.allSkills.Count; i++)
        {
            Skill skill = database.allSkills[i];
            if (skill != null && skill.id == id) return skill;
        }

        return null;
    }

    // ==================== DURUM ====================

    public void Refresh()
    {
        if (!built) return;

        foreach (KeyValuePair<string, SkillNodeView> pair in nodes)
            pair.Value.SetState(Evaluate(pair.Value.Skill));

        if (connectors != null) connectors.Refresh();
        if (connectorGlow != null) connectorGlow.Refresh();
    }

    private SkillNodeState Evaluate(Skill skill)
    {
        if (skill == null) return SkillNodeState.Locked;

        SkillTreeManager manager = SkillTreeManager.Instance;

        if (manager != null)
        {
            if (manager.IsUnlocked(skill.id)) return SkillNodeState.Unlocked;
            if (manager.IsBlocked(skill.id)) return SkillNodeState.Blocked;
        }

        if (!PrerequisitesMet(skill, null)) return SkillNodeState.Locked;

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null) return SkillNodeState.Available;

        if (!stats.HasEnoughWealth(skill.cost)) return SkillNodeState.Unaffordable;

        //para ve ön koşullar tamamsa hâlâ alınamıyorsa sebep "diğer ön koşullar"dır
        if (manager != null && !manager.CanUnlock(skill)) return SkillNodeState.Locked;

        return SkillNodeState.Available;
    }

    /// <summary>Ön koşullar sağlanıyor mu; sağlanmayanlar istenirse listeye yazılır.</summary>
    private bool PrerequisitesMet(Skill skill, List<Skill> missing)
    {
        if (skill.prerequisites == null || skill.prerequisites.Count == 0) return true;

        SkillTreeManager manager = SkillTreeManager.Instance;
        bool met = true;

        foreach (Skill prerequisite in skill.prerequisites)
        {
            if (prerequisite == null) continue;

            bool unlocked = manager != null && manager.IsUnlocked(prerequisite.id);
            if (unlocked) continue;

            met = false;
            missing?.Add(prerequisite);
        }

        return met;
    }

    // ==================== ETKİLEŞİM ====================

    /// <summary>Alınmış + aktif yeteneği olan node: tek tıkla kullanılır, satın alınmaz.</summary>
    public bool IsActivatable(SkillNodeView node)
    {
        return node.State == SkillNodeState.Unlocked
               && node.HasActiveAbility
               && SkillTreeManager.Instance != null;
    }

    public void ActivateNode(SkillNodeView node)
    {
        if (!IsActivatable(node)) return;

        if (!SkillTreeManager.Instance.TryActivate(node.Skill.id))
            node.PlayDenied(); //hâlâ bekleme süresinde
    }

    /// <summary>Bu node için basılı tutma satın alması başlatılabilir mi.</summary>
    public bool CanPurchase(SkillNodeView node)
    {
        if (node.Skill == null) return false;

        if (SkillTreeManager.Instance == null)
        {
            Debug.LogWarning("[SkillTreeView] SkillTreeManager sahnede yok — hiçbir skill açılamaz.", this);
            return false;
        }

        if (node.State == SkillNodeState.Unlocked) return false;

        //TEST modu ön koşul/para aramaz
        return testUnlockAnything || node.State == SkillNodeState.Available;
    }

    /// <summary>Basılı tutma tamamlandı — skill'i satın alır.</summary>
    public void PurchaseNode(SkillNodeView node)
    {
        if (!CanPurchase(node))
        {
            node.PlayDenied();
            return;
        }

        if (testUnlockAnything)
        {
            if (!SkillTreeManager.Instance.ForceUnlock(node.Skill.id))
                node.PlayDenied();
            return;
        }

        SkillEvents.OnSkillUnlockRequested?.Invoke(node.Skill.id);
    }

    public void ShowTooltip(SkillNodeView node)
    {
        if (tooltip == null) return;

        missingBuffer.Clear();
        PrerequisitesMet(node.Skill, missingBuffer);

        GameStatManager stats = GameStatManager.Instance;
        bool affordable = stats == null || stats.HasEnoughWealth(node.Skill.cost);

        SkillTreeNodeLayout entry = layout.Get(node.Skill.id);
        tooltip.Show(node, entry != null ? entry.branches : SkillBranch.None, missingBuffer, affordable);
    }

    public void HideTooltip(SkillNodeView node)
    {
        if (tooltip != null) tooltip.Hide();
    }

    // ==================== KAMERA ====================

    /// <summary>
    /// En fazla uzaklaşma sınırını ağacın GENİŞLİĞİNE göre belirler:
    /// en geniş ağacın tamamı artı biraz pay görünür, daha fazlası okunmaz olurdu.
    /// Kolon aralığı değişse bile doğru kalsın diye sabit sayı yerine hesaplanır.
    /// </summary>
    private void ApplyZoomLimits()
    {
        if (viewport == null || content == null) return;

        Rect view = GetComponent<RectTransform>().rect;
        Vector2 size = content.sizeDelta;
        if (view.width <= 1f || view.height <= 1f || size.x <= 1f || size.y <= 1f) return;

        //İKİ eksenin büyüğü alınır: hangi eksen ekrana göre daha "dar" kalıyorsa o doldurulur.
        //Sadece genişliğe bakmak dikey modda felaket — ağaç 9950x2200 olduğu için
        //genişliği sığdıran zoom, yüksekliği ekranın yarısına düşürüp her yeri boş bırakıyordu.
        float fit = Mathf.Max(view.width / size.x, view.height / size.y);

        viewport.minZoom = Mathf.Clamp(fit * Mathf.Max(0.05f, minZoomFactor), 0.05f, viewport.maxZoom);
    }

    /// <summary>Ağacın tamamını görüş alanına sığdırır.</summary>
    public void FitToView()
    {
        if (viewport == null || content == null) return;

        RectTransform self = GetComponent<RectTransform>();
        Vector2 view = self.rect.size;
        Vector2 size = content.sizeDelta;

        if (size.x <= 1f || size.y <= 1f || view.x <= 1f) return;

        float zoom = Mathf.Min(view.x / size.x, view.y / size.y);
        viewport.SetZoom(zoom, true);
    }

    /// <summary>Belirtilen skill'i ekranın ortasına alır.</summary>
    public void FocusOn(string skillId, bool animate = false)
    {
        if (viewport == null || string.IsNullOrEmpty(skillId)) return;
        if (!nodes.TryGetValue(skillId, out SkillNodeView node)) return;

        viewport.CenterOn(node.Rect.anchoredPosition, animate);
    }

    /// <summary>
    /// Bir ağacı ekrana sığdırır: hem zoom'u o ağacın boyutuna ayarlar hem de ortasına kayar.
    /// Sadece köke gitmek yetmiyordu — ağacın ne kadarının göründüğü zoom'a bağlı.
    /// </summary>
    public void FocusBranch(SkillBranch branch)
    {
        if (viewport == null || content == null) return;

        //ağacın node'larının sınırlarını bul
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        bool found = false;

        foreach (KeyValuePair<string, SkillNodeView> pair in nodes)
        {
            SkillTreeNodeLayout entry = layout.Get(pair.Key);
            if (entry == null || (entry.branches & branch) == 0) continue;

            Vector2 p = pair.Value.Rect.anchoredPosition;
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            found = true;
        }

        if (!found) return;

        //node yarıçapı + altındaki yazılar için pay
        float margin = style.nodeSize * 1.2f;
        Rect view = GetComponent<RectTransform>().rect;

        float zoom = Mathf.Min(
            view.width / Mathf.Max(1f, maxX - minX + margin * 2f),
            view.height / Mathf.Max(1f, maxY - minY + margin * 2f));

        //ikisi de animasyonlu: zoom anında değişirse konum kaymasıyla birlikte sıçrama oluyor
        viewport.SetZoom(Mathf.Clamp(zoom, viewport.minZoom, viewport.maxZoom));
        viewport.CenterOn(new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f), animate: true);
    }

    /// <summary>Inspector/UnityEvent'ten bağlamak için: 0=Finans, 1=Siyaset, 2=Medya.</summary>
    public void FocusBranch(int branchIndex)
    {
        FocusBranch((SkillBranch)(1 << branchIndex));
    }

    // ==================== EVENTLER ====================

    private void HandleSkillUnlocked(Skill skill)
    {
        Refresh();

        if (skill == null || !nodes.TryGetValue(skill.id, out SkillNodeView node)) return;

        node.PlayUnlock();

        //çizgiler anında bağlanmasın: açılan node'dan komşulara doğru ilerlesinler
        if (connectors != null) connectors.AnimateEdgesFrom(node);
    }

    /// <summary>Çizgi komşu node'a vardı — o node'un etrafında halka süpürmesi oynat.</summary>
    private void HandleEdgeReached(SkillNodeView node)
    {
        if (node == null) return;

        node.PlayRingSweep(style.For(node.State).ring);
    }

    private void HandleSkillActivated(Skill skill)
    {
        if (skill != null && nodes.TryGetValue(skill.id, out SkillNodeView node))
            node.PlayActivated();
    }

    private void HandleSkillBlocked(Skill skill)
    {
        Refresh();
    }

    private void HandleStatChanged(StatType stat, float oldValue, float newValue)
    {
        //sadece para değişimi node durumlarını etkiler
        if (stat != StatType.Wealth) return;

        Refresh();
    }
}
