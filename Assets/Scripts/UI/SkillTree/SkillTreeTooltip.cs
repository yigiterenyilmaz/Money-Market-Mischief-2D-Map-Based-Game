using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Node üstüne gelince açılan bilgi kutusu: isim, açıklama, fiyat ve
/// sağlanmayan ön koşullar. İmleci takip eder, ekran dışına taşmaz.
/// </summary>
public class SkillTreeTooltip : MonoBehaviour
{
    private SkillTreeStyle style;
    private RectTransform rect;
    private RectTransform canvasRect;
    private TextMeshProUGUI title;
    private TextMeshProUGUI branchLabel;
    private TextMeshProUGUI body;
    private TextMeshProUGUI requirements;
    private readonly StringBuilder builder = new StringBuilder();

    public void Build(SkillTreeStyle treeStyle, RectTransform canvas)
    {
        style = treeStyle;
        canvasRect = canvas;
        rect = GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(style.tooltipWidth, 200f);

        Image background = gameObject.AddComponent<Image>();
        background.sprite = UISpriteFactory.RoundedRect(14, 6);
        background.type = Image.Type.Sliced;
        background.color = style.tooltipBackground;
        background.raycastTarget = false;

        //ince çerçeve — DOLU değil, sadece kenar. Dolu olan arka planı tamamen örtüyordu.
        //Layout grubunun dışında tutulur, yoksa metinlerle birlikte dizilir.
        Image border = SkillTreeUI.NewImage("Border", rect, UISpriteFactory.RoundedRectOutline(14, 2, 6));
        SkillTreeUI.Stretch(border.rectTransform, -2f);
        border.color = style.tooltipBorder;
        border.transform.SetAsFirstSibling();
        border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 20, 22);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        title = SkillTreeUI.NewText("Title", rect, style.tooltipTitleFontSize, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.color = style.tooltipText;

        branchLabel = SkillTreeUI.NewText("Branch", rect, style.tooltipBranchFontSize, TextAlignmentOptions.TopLeft);
        branchLabel.fontStyle = FontStyles.SmallCaps;

        body = SkillTreeUI.NewText("Body", rect, style.tooltipBodyFontSize, TextAlignmentOptions.TopLeft);
        body.color = style.tooltipMuted;
        body.lineSpacing = 6f;

        requirements = SkillTreeUI.NewText("Requirements", rect, style.tooltipBodyFontSize, TextAlignmentOptions.TopLeft);
        requirements.lineSpacing = 6f;

        gameObject.SetActive(false);
    }

    public void Show(SkillNodeView node, SkillBranch branch, IList<Skill> missingPrerequisites, bool affordable)
    {
        Skill skill = node.Skill;
        if (skill == null) return;

        gameObject.SetActive(true);

        title.text = string.IsNullOrWhiteSpace(skill.displayName) ? skill.id.ToUpperInvariant() : skill.displayName;

        branchLabel.text = BranchName(branch) + "  ·  " + skill.id.ToUpperInvariant();
        branchLabel.color = style.ReadableAccent(branch);

        body.text = string.IsNullOrWhiteSpace(skill.description) ? "<i>Açıklama yok.</i>" : skill.description;

        builder.Clear();

        //aktif yetenek bilgisi durumdan bağımsız gösterilir — almadan önce de görünsün
        if (SkillTreeManager.HasActiveAbility(skill))
        {
            SkillActiveAbility ability = skill.activeAbility;
            string abilityName = string.IsNullOrWhiteSpace(ability.abilityName) ? "Aktif yetenek" : ability.abilityName;

            //Emoji KULLANMA: LiberationSans'ta ⚡ (U+26A1) yok, TMP her çizimde uyarı basıp
            //konsolu dolduruyordu ve karakter boş kutuya düşüyordu. Düz ASCII işaret güvenli.
            builder.Append($"<color=#FFD65A><b>&gt;&gt; {abilityName}</b></color>");

            if (!string.IsNullOrWhiteSpace(ability.description))
                builder.Append("\n").Append(ability.description);

            builder.Append($"\n<color=#9AA5B1>Bekleme: {ability.cooldownSeconds:0} sn</color>");

            SkillTreeManager manager = SkillTreeManager.Instance;
            if (manager != null && node.State == SkillNodeState.Unlocked)
            {
                float remaining = manager.GetCooldownRemaining(skill.id);
                builder.Append(remaining <= 0f
                    ? "\n<color=#FFD65A>Kullanmak için tıkla.</color>"
                    : $"\n<color=#7AA6D9>Tekrar hazır: {Mathf.CeilToInt(remaining)} sn</color>");
            }

            builder.Append("\n\n");
        }

        switch (node.State)
        {
            case SkillNodeState.Unlocked:
                builder.Append("<color=#5CE68C>Bu skill alındı.</color>");
                break;

            case SkillNodeState.Blocked:
                builder.Append("<color=#E68C7A>Başka bir seçim yüzünden kalıcı olarak kilitli.</color>");
                break;

            default:
                string costText = skill.cost > 0 ? SkillTreeUI.FormatMoney(skill.cost) : "Bedava";
                builder.Append(affordable
                    ? $"Maliyet: <b>{costText}</b>"
                    : $"Maliyet: <color=#F2A07A><b>{costText}</b> (yetersiz para)</color>");

                if (missingPrerequisites != null && missingPrerequisites.Count > 0)
                {
                    builder.Append("\n<color=#9AA5B1>Gerekenler:</color>");
                    for (int i = 0; i < missingPrerequisites.Count; i++)
                    {
                        Skill p = missingPrerequisites[i];
                        if (p == null) continue;
                        string name = string.IsNullOrWhiteSpace(p.displayName) ? p.id.ToUpperInvariant() : p.displayName;
                        builder.Append("\n  · ").Append(name);
                    }
                }

                if (skill.otherPrerequisites != OtherPrerequisite.None)
                    builder.Append("\n  · ").Append(DescribeOther(skill.otherPrerequisites));

                //satın alma artık basılı tutmayla olduğu için bunu açıkça söylemek gerekiyor
                if (node.State == SkillNodeState.Available)
                    builder.Append("\n<color=#73FBB8>Almak için basılı tut.</color>");

                if (skill.blocksSkills != null && skill.blocksSkills.Count > 0)
                {
                    builder.Append("\n<color=#E68C7A>Dikkat: bunu alırsan şunlar kalıcı kilitlenir:</color>");
                    for (int i = 0; i < skill.blocksSkills.Count; i++)
                    {
                        Skill b = skill.blocksSkills[i];
                        if (b == null) continue;
                        string name = string.IsNullOrWhiteSpace(b.displayName) ? b.id.ToUpperInvariant() : b.displayName;
                        builder.Append("\n  · ").Append(name);
                    }
                }
                break;
        }

        requirements.text = builder.ToString();
        requirements.color = style.tooltipText;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (canvasRect == null || Mouse.current == null) return;

        Vector2 mouse = Mouse.current.position.ReadValue();

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mouse, GetCanvasCamera(), out local))
            return;

        //imlecin sağ altına yerleş, ekran kenarına yaklaşınca içeri döndür
        Vector2 offset = new Vector2(22f, -22f);
        Vector2 target = local + offset;

        Rect canvasBounds = canvasRect.rect;
        Vector2 size = rect.rect.size;

        if (target.x + size.x > canvasBounds.xMax)
            target.x = local.x - offset.x - size.x;

        if (target.y - size.y < canvasBounds.yMin)
            target.y = local.y - offset.y + size.y;

        rect.anchoredPosition = target;
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = canvasRect != null ? canvasRect.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private static string BranchName(SkillBranch branch)
    {
        bool multi = branch != SkillBranch.None && (branch & (branch - 1)) != 0;
        if (multi)
        {
            List<string> parts = new List<string>();
            if ((branch & SkillBranch.Finance) != 0) parts.Add("Finans");
            if ((branch & SkillBranch.Politics) != 0) parts.Add("Siyaset");
            if ((branch & SkillBranch.Media) != 0) parts.Add("Medya");
            return string.Join(" / ", parts) + " — Özel";
        }

        switch (branch)
        {
            case SkillBranch.Finance: return "Finans";
            case SkillBranch.Politics: return "Siyaset";
            case SkillBranch.Media: return "Medya";
            default: return "—";
        }
    }

    private static string DescribeOther(OtherPrerequisite flags)
    {
        if ((flags & OtherPrerequisite.ThreeScientistsTrained) != 0)
            return "3 bilim adamı eğitimi tamamlanmış olmalı";

        return flags.ToString();
    }
}
