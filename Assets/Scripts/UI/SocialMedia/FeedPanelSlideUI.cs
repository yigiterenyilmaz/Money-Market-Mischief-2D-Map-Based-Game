using UnityEngine;
using UnityEngine.UI;

// Feed paneli yerinden yukari kaydirir. Peek pozisyonu = panelin Inspector'daki konumu (Awake aninda okunur).
// Tiklayinca slideUpAmount kadar yukari kayar, tekrar tiklayinca peek'e doner.
// Panelin boyutu, anchor, pivot — hicbir seye dokunulmaz.
[RequireComponent(typeof(RectTransform))]
public class FeedPanelSlideUI : MonoBehaviour
{
    [Header("Kaydirma")]
    [Tooltip("Tiklayinca panel kac pixel yukari kaysin (acilma miktari).")]
    public float slideUpAmount = 540f;

    [Header("Animasyon")]
    public float slideSpeed = 8f;
    public float snapThreshold = 0.5f;

    [Header("Tiklanabilir Alan (opsiyonel)")]
    [Tooltip("Bos birakilirsa panelin kendisine Button eklenir.")]
    public Button toggleButton;

    RectTransform rect;
    bool isOpen;
    float peekY;
    float openY;
    float targetY;

    void Awake()
    {
        rect = GetComponent<RectTransform>();

        // Inspector'daki mevcut konum peek olarak alinir
        peekY = rect.anchoredPosition.y;
        openY = peekY + slideUpAmount;
        targetY = peekY;

        if (toggleButton == null)
        {
            toggleButton = GetComponent<Button>();
            if (toggleButton == null)
                toggleButton = gameObject.AddComponent<Button>();
        }
        toggleButton.onClick.AddListener(Toggle);
    }

    void Update()
    {
        Vector2 pos = rect.anchoredPosition;
        float newY = Mathf.Lerp(pos.y, targetY, Time.unscaledDeltaTime * slideSpeed);
        if (Mathf.Abs(newY - targetY) < snapThreshold)
            newY = targetY;
        pos.y = newY;
        rect.anchoredPosition = pos;
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        targetY = isOpen ? openY : peekY;
    }

    public void Open()
    {
        isOpen = true;
        targetY = openY;
    }

    public void Close()
    {
        isOpen = false;
        targetY = peekY;
    }

    public bool IsOpen => isOpen;
}
