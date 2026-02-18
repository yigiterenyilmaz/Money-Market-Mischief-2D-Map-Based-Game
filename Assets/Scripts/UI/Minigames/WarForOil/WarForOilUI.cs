using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// SETUP: Root GameObject must be ACTIVE in the Hierarchy.
/// All panels are hidden automatically in Awake — no contentRoot needed.
/// Assign countryCardPrefab and countryContainer in the Inspector.
/// </summary>
public class WarForOilUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject selectionPanel;
    public GameObject pressurePanel;
    public GameObject warPanel;
    public GameObject eventPanel;
    public GameObject resultPanel;

    [Header("Selection Panel")]
    public Transform countryContainer;
    public GameObject countryCardPrefab;

    [Header("Pressure Panel")]
    public TextMeshProUGUI pressureCountryName;
    public TextMeshProUGUI pressureCooldownText;
    public Button pressureButton;
    public Button cancelButton;

    [Header("War Panel")]
    public TextMeshProUGUI warCountryName;
    public TextMeshProUGUI warTimerText;
    public TextMeshProUGUI supportText;
    public Slider progressBar;
    public Button ceasefireButton;

    [Header("Event Panel")]
    public TextMeshProUGUI eventTitle;
    public TextMeshProUGUI eventDescription;
    public TextMeshProUGUI eventTimerText;
    public Transform choiceContainer;
    public GameObject choiceButtonPrefab;

    [Header("Result Panel")]
    public TextMeshProUGUI resultTitle;
    public TextMeshProUGUI resultDescription;
    public TextMeshProUGUI resultStats;
    public Button dismissButton;

    // Runtime
    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<GameObject> spawnedChoices = new List<GameObject>();
    private WarForOilManager manager;
    private float warDuration;
    private float eventDuration;
    private bool initialized = false;
    private bool isOpen = false;

    public static WarForOilUI Instance { get; private set; }

    // ==================== LIFECYCLE ====================

    void Awake()
    {
        Instance = this;
        // Hide all panels immediately — root must be active for this to run
        ForceHideAll();
    }

    void ForceHideAll()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        if (pressurePanel != null) pressurePanel.SetActive(false);
        if (warPanel != null) warPanel.SetActive(false);
        if (eventPanel != null) eventPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        isOpen = false;
    }

    void HideAllPanels()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        if (pressurePanel != null) pressurePanel.SetActive(false);
        if (warPanel != null) warPanel.SetActive(false);
        if (eventPanel != null) eventPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    void SetupButtons()
    {
        if (pressureButton != null) pressureButton.onClick.AddListener(() => manager.AttemptPressure());
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (ceasefireButton != null) ceasefireButton.onClick.AddListener(() => manager.RequestCeasefire());
        if (dismissButton != null) dismissButton.onClick.AddListener(() => manager.DismissResultScreen());
    }

    void OnCancelClicked()
    {
        manager.CancelPressure();
        CloseWarForOil();
    }

    void Update()
    {
        // Refresh card list if selection panel is open but empty
        if (isOpen && initialized && selectionPanel != null && selectionPanel.activeSelf && spawnedCards.Count == 0)
            TryRefreshCountries();
    }

    void OnEnable()
    {
        if (initialized && manager != null)
            TryRefreshCountries();
    }

    void OnDestroy() => UnsubscribeEvents();

    // ==================== OPEN / CLOSE ====================

    public static void OpenWarForOil()
    {
        if (Instance == null)
        {
            Instance = FindFirstObjectByType<WarForOilUI>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                Debug.LogError("[WarForOilUI] Instance not found! Root GameObject must be ACTIVE in the scene.");
                return;
            }
        }

        Instance.manager = WarForOilManager.Instance;

        if (!Instance.initialized)
        {
            Instance.SubscribeEvents();
            Instance.SetupButtons();
            Instance.initialized = true;
        }

        Instance.isOpen = true;

        // Show correct panel for current state
        var state = Instance.manager.GetCurrentState();
        switch (state)
        {
            case WarForOilState.PressurePhase:
                Instance.ShowPanel(Instance.pressurePanel);
                var country = Instance.manager.GetSelectedCountry();
                if (country != null && Instance.pressureCountryName != null)
                    Instance.pressureCountryName.text = country.displayName;
                break;

            case WarForOilState.WarProcess:
            case WarForOilState.EventPhase:
                Instance.ShowPanel(Instance.warPanel);
                break;

            case WarForOilState.ResultPhase:
                Instance.ShowPanel(Instance.resultPanel);
                break;

            default:
                Instance.ShowPanel(Instance.selectionPanel);
                Instance.TryRefreshCountries();
                break;
        }

        UImanager.Instance.ToggleUI();
    }

    public static void CloseWarForOil()
    {
        if (Instance == null) return;
        Instance.HideAllPanels();
        Instance.isOpen = false;
        UImanager.Instance.ToggleUI();
    }

    // ==================== PANEL MANAGEMENT ====================

    void ShowPanel(GameObject panel)
    {
        if (selectionPanel != null) selectionPanel.SetActive(panel == selectionPanel);
        if (pressurePanel != null) pressurePanel.SetActive(panel == pressurePanel);
        if (warPanel != null) warPanel.SetActive(panel == warPanel);
        if (resultPanel != null) resultPanel.SetActive(panel == resultPanel);

        if (panel == selectionPanel)
            TryRefreshCountries();
    }

    void TryRefreshCountries()
    {
        if (manager == null) { Debug.LogError("[WarForOilUI] Manager is null!"); return; }
        if (countryCardPrefab == null) { Debug.LogError("[WarForOilUI] countryCardPrefab is not assigned!"); return; }
        if (countryContainer == null) { Debug.LogError("[WarForOilUI] countryContainer is not assigned!"); return; }

        var countries = manager.GetActiveCountries();
        if (countries == null || countries.Count == 0) return;

        RefreshCountryCards(countries);
    }

    void RefreshCountryCards(List<WarForOilCountry> countries)
    {
        foreach (var go in spawnedCards)
            if (go != null) Destroy(go);
        spawnedCards.Clear();

        if (countries == null || countries.Count == 0) return;

        foreach (var country in countries)
        {
            if (country == null) continue;

            var card = Instantiate(countryCardPrefab, countryContainer);
            spawnedCards.Add(card);

            bool conquered = manager.IsCountryConquered(country);
            var countryCopy = country;

            // Use dedicated card component if available
            var cardUI = card.GetComponent<WarForOilCountryCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(country, conquered, () => manager.SelectCountry(countryCopy));
                continue;
            }

            // Fallback: plain button + text
            var txt = card.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = conquered
                    ? $"<s>{country.displayName}</s>\n<size=70%>CONQUERED</size>"
                    : $"{country.displayName}\n<size=70%>Reward: ${country.baseReward:F0} | Difficulty: {country.invasionDifficulty:P0}</size>";
            }

            var btn = card.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = !conquered;
                btn.onClick.AddListener(() => manager.SelectCountry(countryCopy));
            }
        }
    }

    // ==================== EVENT SUBSCRIPTIONS ====================

    void SubscribeEvents()
    {
        WarForOilManager.OnActiveCountriesChanged += OnCountriesChanged;
        WarForOilManager.OnCountrySelected += OnCountrySelected;
        WarForOilManager.OnPressureResult += OnPressureResult;
        WarForOilManager.OnPressureCooldownUpdate += OnCooldownUpdate;
        WarForOilManager.OnWarStarted += OnWarStarted;
        WarForOilManager.OnWarProgress += OnWarProgress;
        WarForOilManager.OnWarEventTriggered += OnEventTriggered;
        WarForOilManager.OnEventDecisionTimerUpdate += OnEventTimerUpdate;
        WarForOilManager.OnWarEventResolved += OnEventResolved;
        WarForOilManager.OnCeasefireResult += OnResult;
        WarForOilManager.OnWarResultReady += OnResult;
        WarForOilManager.OnWarFinished += OnWarFinished;
    }

    void UnsubscribeEvents()
    {
        WarForOilManager.OnActiveCountriesChanged -= OnCountriesChanged;
        WarForOilManager.OnCountrySelected -= OnCountrySelected;
        WarForOilManager.OnPressureResult -= OnPressureResult;
        WarForOilManager.OnPressureCooldownUpdate -= OnCooldownUpdate;
        WarForOilManager.OnWarStarted -= OnWarStarted;
        WarForOilManager.OnWarProgress -= OnWarProgress;
        WarForOilManager.OnWarEventTriggered -= OnEventTriggered;
        WarForOilManager.OnEventDecisionTimerUpdate -= OnEventTimerUpdate;
        WarForOilManager.OnWarEventResolved -= OnEventResolved;
        WarForOilManager.OnCeasefireResult -= OnResult;
        WarForOilManager.OnWarResultReady -= OnResult;
        WarForOilManager.OnWarFinished -= OnWarFinished;
    }

    // ==================== EVENT HANDLERS ====================

    void OnCountriesChanged(List<WarForOilCountry> countries)
    {
        if (!isOpen) return;
        RefreshCountryCards(countries);
    }

    void OnCountrySelected(WarForOilCountry country)
    {
        CloseWarForOil();
    }

    void OnPressureResult(bool success, float cooldown)
    {
        if (!isOpen) return;
        if (!success && pressureButton != null)
            pressureButton.interactable = false;
    }

    void OnCooldownUpdate(float remaining)
    {
        if (!isOpen) return;
        if (pressureCooldownText != null)
            pressureCooldownText.text = remaining > 0 ? $"Cooldown: {remaining:F1}s" : "";
        if (remaining <= 0 && pressureButton != null)
            pressureButton.interactable = true;
    }

    void OnWarStarted(WarForOilCountry country, float duration)
    {
        if (!isOpen) return;
        ShowPanel(warPanel);
        warDuration = duration;
        if (warCountryName != null) warCountryName.text = country.displayName;
        if (progressBar != null) progressBar.value = 0;
        if (supportText != null) supportText.text = $"Support: {manager.GetSupportStat():F0}%";
    }

    void OnWarProgress(float progress)
    {
        if (!isOpen) return;
        if (progressBar != null) progressBar.value = progress;
        float remaining = warDuration * (1f - progress);
        if (warTimerText != null)
            warTimerText.text = $"{Mathf.FloorToInt(remaining / 60)}:{Mathf.FloorToInt(remaining % 60):D2}";
        if (supportText != null) supportText.text = $"Support: {manager.GetSupportStat():F0}%";
        if (ceasefireButton != null) ceasefireButton.interactable = manager.CanRequestCeasefire();
    }

    void OnEventTriggered(WarForOilEvent evt)
    {
        if (!isOpen) return;
        if (eventPanel != null) eventPanel.SetActive(true);
        if (eventTitle != null) eventTitle.text = evt.displayName;
        if (eventDescription != null) eventDescription.text = evt.description;
        eventDuration = evt.decisionTime;

        foreach (var go in spawnedChoices)
            if (go != null) Destroy(go);
        spawnedChoices.Clear();

        for (int i = 0; i < evt.choices.Count; i++)
        {
            int idx = i;
            var choice = evt.choices[i];
            var btn = Instantiate(choiceButtonPrefab, choiceContainer);
            spawnedChoices.Add(btn);

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = $"{choice.displayName}\n<size=70%>{FormatModifiers(choice)}</size>";

            var button = btn.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => manager.ResolveEvent(idx));
        }
    }

    void OnEventTimerUpdate(float remaining)
    {
        if (!isOpen) return;
        if (eventTimerText != null) eventTimerText.text = $"{remaining:F1}s";
    }

    void OnEventResolved(WarForOilEventChoice choice)
    {
        if (!isOpen) return;
        if (eventPanel != null) eventPanel.SetActive(false);
    }

    void OnResult(WarForOilResult result)
    {
        if (!isOpen) return;
        ShowPanel(resultPanel);

        if (resultTitle != null)
        {
            if (result.wasCeasefire) resultTitle.text = "Ceasefire";
            else if (result.warWon) resultTitle.text = "Victory!";
            else resultTitle.text = "Defeat";
        }

        if (resultDescription != null)
        {
            resultDescription.text = result.wasCeasefire
                ? $"Negotiated ceasefire with {result.country.displayName}."
                : result.warWon
                    ? $"Conquered {result.country.displayName}!"
                    : $"Failed to conquer {result.country.displayName}. War operations disabled.";
        }

        if (resultStats != null)
        {
            resultStats.text =
                $"Wealth: {(result.wealthChange >= 0 ? "+" : "")}{result.wealthChange:F0}\n" +
                $"Suspicion: {(result.suspicionChange >= 0 ? "+" : "")}{result.suspicionChange:F0}";
        }
    }

    void OnWarFinished(WarForOilResult result)
    {
        CloseWarForOil();
    }

    // ==================== UTILITY ====================

    string FormatModifiers(WarForOilEventChoice c)
    {
        var parts = new List<string>();
        if (c.supportModifier != 0) parts.Add($"Support {(c.supportModifier > 0 ? "+" : "")}{c.supportModifier}");
        if (c.suspicionModifier != 0) parts.Add($"Suspicion {(c.suspicionModifier > 0 ? "+" : "")}{c.suspicionModifier}");
        if (c.costModifier != 0) parts.Add($"Cost {(c.costModifier > 0 ? "+" : "")}{c.costModifier}");
        return string.Join(" | ", parts);
    }
}