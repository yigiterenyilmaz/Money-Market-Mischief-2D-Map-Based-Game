using UnityEngine;
using TMPro;

public class WealthUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI wealthText;
    [SerializeField] private string prefix = "$"; // optional prefix

    private void OnEnable()
    {
        GameStatManager.OnStatChanged += OnStatChanged;
    }

    private void OnDisable()
    {
        GameStatManager.OnStatChanged -= OnStatChanged;
    }

    private void Start()
    {
        // set initial value
        if (GameStatManager.Instance != null)
            UpdateText(GameStatManager.Instance.Wealth);
    }

    private void OnStatChanged(StatType statType, float oldValue, float newValue)
    {
        if (statType == StatType.Wealth)
            UpdateText(newValue);
    }

    private void UpdateText(float value)
    {
        wealthText.text = $"{prefix}{value:N0}"; // e.g. "$1,000"
    }
}