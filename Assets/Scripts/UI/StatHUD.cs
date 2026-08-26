using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// İTİBAR + ŞÜPHE GÖSTERGESİ — ekranın köşesinde iki bar.
///
/// Bu iki stat oyunun kaybetme koşulunu sürüyor ama bugüne kadar hiçbir yerde görünmüyordu:
/// ekrandaki tek stat paraydı (WealthUI). Bu bileşen o boşluğu kapatır.
///
/// Gösterilen şey sadece iki sayı değil, aralarındaki BAĞ:
///  • Şüphe barı dolunca oyun biter (GameStatManager.maxSuspicion).
///  • İtibar, şüphenin ARTIŞ HIZINI belirler (GetSuspicionMultiplier: itibar 0'da 1.5x,
///    tavanda 0.5x). Bu çarpan şüphe barının altında yazıyla gösterilir — oyuncunun
///    "neden itibar topluyorum" sorusunun cevabı orası.
///  • İtibar eksiye düştüyse tavanı kalıcı olarak düşer. Kaybedilen tavan, itibar barının
///    sağ ucunda soluk bir parça olarak durur; başka hiçbir yerde görünmeyen bir ceza.
///
/// TASARIM GERİLİMİ: b11–b13 anketleri (itibar/şüphe/nüfuz kestirme) tam da bu statlar
/// gizli olduğu için tasarlanmıştı. Barlar sürekli açıkken o üç skill anlamsızlaşır.
/// <see cref="hideUntilPollUnlocked"/> açılırsa barlar ancak ilgili anket skill'i alınınca
/// görünür ve iki tasarım bir arada yaşar.
///
/// Prefab gerektirmez, kendini koddan kurar (ev kuralı). Sahnede Canvas'ın altındaysa onu
/// kullanır, değilse sahnedeki ilk Canvas'ı bulur — böylece paylaşılan Canvas.prefab'a
/// dokunmadan herhangi bir objeye eklenebilir.
/// </summary>
public class StatHUD : MonoBehaviour
{
    [Header("Yerleşim")]
    [Tooltip("Sol üst köşeden uzaklık (piksel).")]
    public Vector2 offset = new Vector2(24f, -24f);
    public float barWidth = 260f;
    public float barHeight = 18f;
    [Tooltip("İki bar arasındaki dikey boşluk.")]
    public float rowSpacing = 46f;

    [Header("Görünürlük")]
    [Tooltip("AÇIK: barlar ancak ilgili anket skill'i (b11 itibar, b12 şüphe) alınınca görünür. " +
             "KAPALI: ikisi de baştan görünür.")]
    public bool hideUntilPollUnlocked = false;

    [Header("Renkler")]
    public Color panelColor = new Color(0.05f, 0.06f, 0.09f, 0.78f);
    public Color trackColor = new Color(1f, 1f, 1f, 0.10f);
    public Color reputationColor = new Color(0.35f, 0.75f, 1f, 1f);
    [Tooltip("İtibar eksideyken barın rengi.")]
    public Color reputationNegativeColor = new Color(1f, 0.45f, 0.35f, 1f);
    [Tooltip("Eksiye düşülerek kalıcı kaybedilen tavan parçasının rengi.")]
    public Color lostCeilingColor = new Color(1f, 0.35f, 0.3f, 0.22f);
    [Tooltip("Şüphe düşükken.")]
    public Color suspicionCalmColor = new Color(0.45f, 0.8f, 0.5f, 1f);
    [Tooltip("Şüphe tavana yaklaşırken.")]
    public Color suspicionDangerColor = new Color(1f, 0.3f, 0.25f, 1f);
    public Color labelColor = new Color(1f, 1f, 1f, 0.75f);

    //itibar satırı
    private RectTransform root;
    private GameObject reputationRow, suspicionRow;
    private Image reputationFill, lostCeilingFill, suspicionFill;
    private TextMeshProUGUI reputationText, suspicionText, multiplierText;

    private void OnEnable()
    {
        GameStatManager.OnStatChanged += HandleStatChanged;
        SkillEvents.OnSkillUnlocked += HandleSkillUnlocked;
    }

    private void OnDisable()
    {
        GameStatManager.OnStatChanged -= HandleStatChanged;
        SkillEvents.OnSkillUnlocked -= HandleSkillUnlocked;
    }

    private void Start()
    {
        Build();
        RefreshAll();
    }

    // -------------------------------------------------------------------------
    // KURULUM
    // -------------------------------------------------------------------------

    private void Build()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[StatHUD] Sahnede Canvas yok — gösterge kurulamadı.");
            enabled = false;
            return;
        }

        Sprite rounded = UISpriteFactory.RoundedRect(10, 2);
        Sprite flat = UISpriteFactory.White();

        root = SkillTreeUI.NewRect("StatHUD", canvas.rootCanvas.transform);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = offset;
        root.sizeDelta = new Vector2(barWidth + 24f, rowSpacing * 2f + 24f);

        Image panel = SkillTreeUI.NewImage("Panel", root, rounded);
        panel.color = panelColor;
        SkillTreeUI.Stretch(panel.rectTransform);

        reputationRow = BuildRow("Reputation", 0, "İTİBAR", rounded, flat,
                                 out reputationFill, out reputationText, out lostCeilingFill, out _);
        suspicionRow  = BuildRow("Suspicion", 1, "ŞÜPHE", rounded, flat,
                                 out suspicionFill, out suspicionText, out _, out multiplierText);
    }

    /// <summary>Tek bir bar satırı: etiket + zemin + dolgu + değer yazısı.</summary>
    private GameObject BuildRow(string name, int index, string label, Sprite rounded, Sprite flat,
                                out Image fill, out TextMeshProUGUI valueText,
                                out Image lostCeiling, out TextMeshProUGUI subText)
    {
        RectTransform row = SkillTreeUI.NewRect(name, root);
        row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(12f, -12f - index * rowSpacing);
        row.sizeDelta = new Vector2(barWidth, rowSpacing - 6f);

        TextMeshProUGUI title = SkillTreeUI.NewText(name + "Label", row, 12f, TextAlignmentOptions.Left);
        title.text = label;
        title.color = labelColor;
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = Vector2.zero;
        title.rectTransform.sizeDelta = new Vector2(barWidth * 0.5f, 16f);

        valueText = SkillTreeUI.NewText(name + "Value", row, 12f, TextAlignmentOptions.Right);
        valueText.color = labelColor;
        valueText.rectTransform.anchorMin = valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        valueText.rectTransform.pivot = new Vector2(1f, 1f);
        valueText.rectTransform.anchoredPosition = Vector2.zero;
        valueText.rectTransform.sizeDelta = new Vector2(barWidth * 0.5f, 16f);

        Image track = SkillTreeUI.NewImage(name + "Track", row, rounded);
        track.color = trackColor;
        track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(0f, 1f);
        track.rectTransform.pivot = new Vector2(0f, 1f);
        track.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        track.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);

        //kaybedilen tavan parçası — dolgunun ALTINDA, sağ uca yaslı
        lostCeiling = SkillTreeUI.NewImage(name + "LostCeiling", track.rectTransform, flat);
        lostCeiling.color = lostCeilingColor;
        lostCeiling.rectTransform.anchorMin = new Vector2(1f, 0f);
        lostCeiling.rectTransform.anchorMax = new Vector2(1f, 1f);
        lostCeiling.rectTransform.pivot = new Vector2(1f, 0.5f);
        lostCeiling.rectTransform.offsetMin = lostCeiling.rectTransform.offsetMax = Vector2.zero;
        lostCeiling.rectTransform.sizeDelta = new Vector2(0f, 0f);
        lostCeiling.enabled = false;

        fill = SkillTreeUI.NewImage(name + "Fill", track.rectTransform, flat);
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        //yalnızca şüphe satırında: çarpan yazısı
        subText = SkillTreeUI.NewText(name + "Sub", row, 10.5f, TextAlignmentOptions.Left);
        subText.color = new Color(labelColor.r, labelColor.g, labelColor.b, 0.6f);
        subText.rectTransform.anchorMin = subText.rectTransform.anchorMax = new Vector2(0f, 1f);
        subText.rectTransform.pivot = new Vector2(0f, 1f);
        subText.rectTransform.anchoredPosition = new Vector2(0f, -18f - barHeight - 2f);
        subText.rectTransform.sizeDelta = new Vector2(barWidth, 14f);
        subText.gameObject.SetActive(false);

        return row.gameObject;
    }

    // -------------------------------------------------------------------------
    // GÜNCELLEME
    // -------------------------------------------------------------------------

    private void HandleStatChanged(StatType statType, float oldValue, float newValue)
    {
        //itibar değişince şüphe çarpanı da değişir — iki satır birbirine bağlı
        if (statType == StatType.Reputation) { RefreshReputation(); RefreshSuspicion(); }
        else if (statType == StatType.Suspicion) RefreshSuspicion();
    }

    //anket skill'i satın alınınca gizli barlar açılabilir
    private void HandleSkillUnlocked(Skill skill) => RefreshVisibility();

    private void RefreshAll()
    {
        RefreshVisibility();
        RefreshReputation();
        RefreshSuspicion();
    }

    private void RefreshVisibility()
    {
        if (reputationRow == null || suspicionRow == null) return;

        bool showRep = true, showSus = true;
        if (hideUntilPollUnlocked && SkillTreeManager.Instance != null)
        {
            showRep = SkillTreeManager.Instance.IsUnlocked("b11");
            showSus = SkillTreeManager.Instance.IsUnlocked("b12");
        }

        reputationRow.SetActive(showRep);
        suspicionRow.SetActive(showSus);
        if (root != null) root.gameObject.SetActive(showRep || showSus);
    }

    private void RefreshReputation()
    {
        var stats = GameStatManager.Instance;
        if (stats == null || reputationFill == null) return;

        float min = stats.minReputation;
        float naturalMax = stats.maxReputation;                 //hiç ceza yokken tavan (100)
        float effectiveMax = stats.EffectiveMaxReputation;      //kalıcı ceza sonrası tavan
        float span = naturalMax - min;
        if (span <= 0f) return;

        //bar HER ZAMAN doğal aralığa (min..naturalMax) göre çizilir; böylece kaybedilen
        //tavan görsel olarak yerinde durur, bar sinsice yeniden ölçeklenmez
        float t = Mathf.Clamp01((stats.Reputation - min) / span);
        reputationFill.rectTransform.anchorMax = new Vector2(t, 1f);
        reputationFill.color = stats.Reputation < 0f ? reputationNegativeColor : reputationColor;

        float lost = Mathf.Max(0f, naturalMax - effectiveMax);
        if (lost > 0.01f)
        {
            lostCeilingFill.enabled = true;
            lostCeilingFill.rectTransform.sizeDelta = new Vector2(barWidth * (lost / span), 0f);
        }
        else lostCeilingFill.enabled = false;

        reputationText.text = lost > 0.01f
            ? $"{stats.Reputation:F0} / {effectiveMax:F0}  (tavan −{lost:F0})"
            : $"{stats.Reputation:F0} / {effectiveMax:F0}";
    }

    private void RefreshSuspicion()
    {
        var stats = GameStatManager.Instance;
        if (stats == null || suspicionFill == null) return;

        float max = stats.maxSuspicion;
        if (max <= 0f) return;

        float t = Mathf.Clamp01(stats.Suspicion / max);
        suspicionFill.rectTransform.anchorMax = new Vector2(t, 1f);
        suspicionFill.color = Color.Lerp(suspicionCalmColor, suspicionDangerColor, t);
        suspicionText.text = $"{stats.Suspicion:F0} / {max:F0}";

        //asıl bilgi bu: itibarın şüphe artışına etkisi
        if (multiplierText != null)
        {
            float mult = stats.GetSuspicionMultiplier();
            multiplierText.gameObject.SetActive(true);
            multiplierText.text = $"şüphe artışı ×{mult:0.00}";
        }
    }
}
