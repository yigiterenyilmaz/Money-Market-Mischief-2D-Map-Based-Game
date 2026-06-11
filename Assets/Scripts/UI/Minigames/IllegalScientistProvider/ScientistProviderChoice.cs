using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Event seçenek butonları için yardımcı component.
/// Modifier'ları renkli olarak gösterir.
/// </summary>
public class IllegalScientistProviderChoiceButton : MonoBehaviour
{
    [Header("UI Referansları")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI modifiersText;
    public Image backgroundImage;

    [Header("Renkler")]
    public Color positiveColor = new Color(0.2f, 0.8f, 0.2f);
    public Color negativeColor = new Color(0.9f, 0.2f, 0.2f);
    public Color neutralColor = new Color(0.6f, 0.6f, 0.6f);
    public Color hoverColor = new Color(0.3f, 0.3f, 0.4f);
    public Color normalColor = new Color(0.2f, 0.2f, 0.25f);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    /// <summary>
    /// Seçenek verilerini UI'a uygular.
    /// </summary>
    public void SetupChoice(IllegalScientistProviderEventChoice choice)
    {
        if (titleText != null)
            titleText.text = choice.displayName;

        if (descriptionText != null)
            descriptionText.text = choice.description;

        if (modifiersText != null)
            modifiersText.text = BuildColoredModifierText(choice);

        if (backgroundImage != null)
        {
            float netEffect = CalculateNetEffect(choice);
            if (netEffect > 0.1f)
                backgroundImage.color = Color.Lerp(normalColor, positiveColor, 0.2f);
            else if (netEffect < -0.1f)
                backgroundImage.color = Color.Lerp(normalColor, negativeColor, 0.2f);
            else
                backgroundImage.color = normalColor;
        }
    }

    private string BuildColoredModifierText(IllegalScientistProviderEventChoice choice)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (choice.riskModifier != 0)
        {
            string color = choice.riskModifier > 0 ? ColorToHex(negativeColor) : ColorToHex(positiveColor);
            string sign = choice.riskModifier > 0 ? "+" : "";
            sb.Append($"<color={color}>Risk: {sign}{choice.riskModifier * 100:F0}%</color>");
        }

        if (choice.suspicionModifier != 0)
        {
            if (sb.Length > 0) sb.Append("  ");
            string color = choice.suspicionModifier > 0 ? ColorToHex(negativeColor) : ColorToHex(positiveColor);
            string sign = choice.suspicionModifier > 0 ? "+" : "";
            sb.Append($"<color={color}>Şüphe: {sign}{choice.suspicionModifier:F1}</color>");
        }

        if (choice.costModifier != 0)
        {
            if (sb.Length > 0) sb.Append("  ");
            string color = choice.costModifier > 0 ? ColorToHex(negativeColor) : ColorToHex(positiveColor);
            string sign = choice.costModifier > 0 ? "-" : "+";
            sb.Append($"<color={color}>${sign}{Mathf.Abs(choice.costModifier)}</color>");
        }

        if (sb.Length == 0)
            sb.Append($"<color={ColorToHex(neutralColor)}>Etkisiz</color>");

        return sb.ToString();
    }

    private float CalculateNetEffect(IllegalScientistProviderEventChoice choice)
    {
        float effect = 0f;
        effect -= choice.riskModifier;
        effect -= choice.suspicionModifier * 0.1f;
        effect -= choice.costModifier * 0.001f;
        return effect;
    }

    private string ColorToHex(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGB(color)}";
    }

    public void OnPointerEnter()
    {
        if (backgroundImage != null && button != null && button.interactable)
            backgroundImage.color = hoverColor;
    }

    public void OnPointerExit()
    {
        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }
}