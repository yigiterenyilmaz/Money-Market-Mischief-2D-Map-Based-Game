using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RouteCardUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI riskText;
    public TextMeshProUGUI costText;
    public Image background;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    private SmuggleRoute route;
    private bool isSelected = false;

    public SmuggleRoute Route => route;

    public void Initialize(SmuggleRoute route)
    {
        this.route = route;

        //referanslar Inspector'da atanmadıysa child TMP'lerden otomatik bul
        if (nameText == null || riskText == null || costText == null)
            AutoFindTextReferences();

        if (nameText != null)
            nameText.text = route.displayName;
        else
            Debug.LogWarning("RouteCardUI: nameText referansı bulunamadı!");

        if (riskText != null)
            riskText.text = $"Risk: {route.riskLevel:F0}%";
        else
            Debug.LogWarning("RouteCardUI: riskText referansı bulunamadı!");

        if (costText != null)
            costText.text = $"Cost: ${route.cost}";
        else
            Debug.LogWarning("RouteCardUI: costText referansı bulunamadı!");

        SetSelected(false);
    }

    private void AutoFindTextReferences()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>();
        if (nameText == null && texts.Length > 0) nameText = texts[0];
        if (riskText == null && texts.Length > 1) riskText = texts[1];
        if (costText == null && texts.Length > 2) costText = texts[2];
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (background != null)
        {
            background.color = selected ? selectedColor : normalColor;
        }
    }

    public void OnCardClicked()
    {
        if (SmuggleSelectionUI.Instance != null)
        {
            SmuggleSelectionUI.Instance.SelectRoute(route);
        }
    }
}