using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Bilim adamı seçim butonları için yardımcı component.
/// Bilim adamı bilgilerini düzgün gösterir.
/// </summary>
public class IllegalScientistProviderScientistButton : MonoBehaviour
{
    [Header("UI Referansları")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statsText;
    public TextMeshProUGUI statusText;
    public Slider trainingProgressSlider;
    public Image avatarImage;
    public Image backgroundImage;
    public GameObject readyIndicator;
    public GameObject trainingIndicator;

    [Header("Renkler")]
    public Color readyColor = new Color(0.2f, 0.7f, 0.2f);
    public Color trainingColor = new Color(0.8f, 0.6f, 0.1f);
    public Color disabledColor = new Color(0.4f, 0.4f, 0.4f);
    public Color highStealthColor = new Color(0.1f, 0.5f, 0.9f);
    public Color lowStealthColor = new Color(0.9f, 0.5f, 0.1f);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    /// <summary>
    /// Bilim adamı verilerini UI'a uygular.
    /// </summary>
    public void SetupScientist(ScientistTraining scientist)
    {
        if (scientist == null || scientist.data == null) return;

        if (nameText != null)
            nameText.text = scientist.data.displayName;

        if (statsText != null)
        {
            float stealth = scientist.data.stealthLevel;
            string stealthLabel = GetStealthLabel(stealth);
            string colorHex = ColorToHex(Color.Lerp(lowStealthColor, highStealthColor, stealth));
            statsText.text = $"<color={colorHex}>Gizlilik: {stealthLabel} ({stealth * 100:F0}%)</color>";
        }

        if (scientist.isCompleted)
        {
            if (statusText != null)
            {
                statusText.text = "HAZIR";
                statusText.color = readyColor;
            }

            if (readyIndicator != null) readyIndicator.SetActive(true);
            if (trainingIndicator != null) trainingIndicator.SetActive(false);

            if (trainingProgressSlider != null)
                trainingProgressSlider.gameObject.SetActive(false);

            if (backgroundImage != null)
                backgroundImage.color = Color.Lerp(Color.black, readyColor, 0.15f);

            if (button != null)
                button.interactable = true;
        }
        else
        {
            if (statusText != null)
            {
                statusText.text = "EĞİTİMDE";
                statusText.color = trainingColor;
            }

            if (readyIndicator != null) readyIndicator.SetActive(false);
            if (trainingIndicator != null) trainingIndicator.SetActive(true);

            if (trainingProgressSlider != null)
                trainingProgressSlider.gameObject.SetActive(false);

            if (backgroundImage != null)
                backgroundImage.color = Color.Lerp(Color.black, disabledColor, 0.15f);

            if (button != null)
                button.interactable = false;
        }

        if (avatarImage != null)
            avatarImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// "Bilim adamı yok" durumu için placeholder gösterir.
    /// </summary>
    public void SetupEmpty(string message)
    {
        if (nameText != null) nameText.text = message;
        if (statsText != null) statsText.text = "";
        if (statusText != null) statusText.text = "";

        if (trainingProgressSlider != null)
            trainingProgressSlider.gameObject.SetActive(false);

        if (readyIndicator != null) readyIndicator.SetActive(false);
        if (trainingIndicator != null) trainingIndicator.SetActive(false);
        if (avatarImage != null) avatarImage.gameObject.SetActive(false);

        if (backgroundImage != null)
            backgroundImage.color = disabledColor;

        if (button != null)
            button.interactable = false;
    }

    private string GetStealthLabel(float stealth)
    {
        if (stealth >= 0.8f) return "Uzman";
        if (stealth >= 0.6f) return "Yüksek";
        if (stealth >= 0.4f) return "Orta";
        if (stealth >= 0.2f) return "Düşük";
        return "Acemi";
    }

    private string ColorToHex(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGB(color)}";
    }
}