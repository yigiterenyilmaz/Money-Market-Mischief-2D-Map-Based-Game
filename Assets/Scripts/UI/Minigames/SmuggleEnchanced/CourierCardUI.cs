using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CourierCardUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI reliabilityText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI costText;
    public Image background;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    private SmuggleCourier courier;
    private bool isSelected = false;

    public SmuggleCourier Courier => courier;

    public void Initialize(SmuggleCourier courier)
    {
        this.courier = courier;

        //referanslar Inspector'da atanmadıysa child TMP'lerden otomatik bul
        if (nameText == null || reliabilityText == null || speedText == null || costText == null)
            AutoFindTextReferences();

        if (nameText != null)
            nameText.text = courier.displayName;
        else
            Debug.LogWarning("CourierCardUI: nameText referansı bulunamadı!");

        if (reliabilityText != null)
            reliabilityText.text = $"Reliability: {courier.reliability:F0}%";
        else
            Debug.LogWarning("CourierCardUI: reliabilityText referansı bulunamadı!");

        if (speedText != null)
            speedText.text = $"Speed: {courier.speed:F0}%";
        else
            Debug.LogWarning("CourierCardUI: speedText referansı bulunamadı!");

        if (costText != null)
            costText.text = $"Cost: ${courier.cost}";
        else
            Debug.LogWarning("CourierCardUI: costText referansı bulunamadı!");

        SetSelected(false);
    }

    private void AutoFindTextReferences()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>();
        if (nameText == null && texts.Length > 0) nameText = texts[0];
        if (reliabilityText == null && texts.Length > 1) reliabilityText = texts[1];
        if (speedText == null && texts.Length > 2) speedText = texts[2];
        if (costText == null && texts.Length > 3) costText = texts[3];
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
            SmuggleSelectionUI.Instance.SelectCourier(courier);
        }
    }
}