using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Add this to your country card prefab.
/// No Inspector fields to fill — finds Button and TextMeshProUGUI automatically.
/// Prefab only needs: Button component on root, TextMeshProUGUI anywhere in children.
/// </summary>
public class WarForOilCountryCardUI : MonoBehaviour
{
    private Action onClickCallback;

    public void Setup(WarForOilCountry country, bool isConquered, Action onClick)
    {
        if (country == null) return;

        onClickCallback = onClick;

        var txt = GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = isConquered
                ? $"<s>{country.displayName}</s>\n<size=70%>CONQUERED</size>"
                : $"{country.displayName}\n<size=70%>Reward: ${country.baseReward:F0} | Difficulty: {country.invasionDifficulty:P0}</size>";
        }

        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = !isConquered;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClickCallback?.Invoke());
        }
    }
}