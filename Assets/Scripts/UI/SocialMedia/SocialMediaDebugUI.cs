using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SocialMediaDebugUI : MonoBehaviour
{
    [Header("Referanslar")]
    public GameObject feedPanel; // sosyal medya paneli (SocialMediaBubbleUI olan)

    [Header("Buton Ayarlari")]
    public int fontSize = 14;
    public Vector2 buttonSize = new Vector2(80, 35);
    public Vector2 buttonOffset = new Vector2(-10, 10); // sag alttan offset

    void Start()
    {
        if (feedPanel != null)
            feedPanel.SetActive(false);

        CreateDebugButton();
    }

    void CreateDebugButton()
    {
        // canvas bul
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // buton objesi
        GameObject btnObj = new GameObject("FeedDebugButton");
        btnObj.transform.SetParent(canvas.transform, false);

        // rect — sag alt kose
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = buttonOffset;
        rect.sizeDelta = buttonSize;

        // arka plan
        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        // buton
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(ToggleFeedPanel);

        // yazi
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "Feed";
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
    }

    void ToggleFeedPanel()
    {
        if (feedPanel != null)
            feedPanel.SetActive(!feedPanel.activeSelf);
    }
}
