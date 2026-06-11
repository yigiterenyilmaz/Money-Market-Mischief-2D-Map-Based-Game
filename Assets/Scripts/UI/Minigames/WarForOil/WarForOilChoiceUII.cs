using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Event seçenek butonu UI bileşeni
/// </summary>
public class WarForOilChoiceUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI choiceNameText;
    public TextMeshProUGUI choiceDescriptionText;
    public TextMeshProUGUI modifiersText;
    public Button choiceButton;

    [Header("Colors")]
    public Color positiveColor = new Color(0.2f, 0.8f, 0.2f);
    public Color negativeColor = new Color(0.8f, 0.2f, 0.2f);
    public Color neutralColor = new Color(0.7f, 0.7f, 0.7f);

    private Action onClickCallback;

    public void Setup(WarForOilEventChoice choice, Action onClick)
    {
        this.onClickCallback = onClick;

        choiceNameText.text = choice.displayName;

        if (choiceDescriptionText != null)
            choiceDescriptionText.text = choice.description;

        if (modifiersText != null)
            modifiersText.text = BuildModifiersText(choice);

        choiceButton.onClick.RemoveAllListeners();
        choiceButton.onClick.AddListener(OnClicked);
    }

    private string BuildModifiersText(WarForOilEventChoice choice)
    {
        string text = "";

        if (!Mathf.Approximately(choice.supportModifier, 0f))
        {
            string sign = choice.supportModifier > 0 ? "+" : "";
            string colorHex = choice.supportModifier > 0
                ? ColorUtility.ToHtmlStringRGB(positiveColor)
                : ColorUtility.ToHtmlStringRGB(negativeColor);
            text += $"<color=#{colorHex}>Support {sign}{choice.supportModifier:F0}</color>  ";
        }

        if (!Mathf.Approximately(choice.suspicionModifier, 0f))
        {
            string sign = choice.suspicionModifier > 0 ? "+" : "";
            // Suspicion artışı kötü, azalması iyi
            string colorHex = choice.suspicionModifier > 0
                ? ColorUtility.ToHtmlStringRGB(negativeColor)
                : ColorUtility.ToHtmlStringRGB(positiveColor);
            text += $"<color=#{colorHex}>Suspicion {sign}{choice.suspicionModifier:F0}</color>  ";
        }

        if (choice.costModifier != 0)
        {
            string sign = choice.costModifier > 0 ? "+" : "";
            // Cost artışı kötü
            string colorHex = choice.costModifier > 0
                ? ColorUtility.ToHtmlStringRGB(negativeColor)
                : ColorUtility.ToHtmlStringRGB(positiveColor);
            text += $"<color=#{colorHex}>Cost {sign}{choice.costModifier}</color>";
        }

        return text.Trim();
    }

    private void OnClicked()
    {
        onClickCallback?.Invoke();
    }
}