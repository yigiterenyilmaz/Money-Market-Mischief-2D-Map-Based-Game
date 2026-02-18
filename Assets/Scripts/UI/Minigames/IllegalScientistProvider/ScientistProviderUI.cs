using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// IllegalScientistProvider minigame UI controller.
/// 
/// SETUP: Root GameObject must be ACTIVE in the Hierarchy so Awake fires.
/// All panels will be hidden automatically in Awake — you do NOT need to
/// set them inactive in the scene yourself.
/// Call OpenAndTriggerOffer() to show the UI.
/// </summary>
public class IllegalScientistProviderUI : MonoBehaviour
{
    [Header("Ana Paneller")]
    public GameObject offerPanel;
    public GameObject processPanel;
    public GameObject eventPanel;
    public GameObject resultPanel;
    public GameObject scientistsKilledPanel;

    [Header("Teklif Paneli - Offer Panel")]
    public TextMeshProUGUI offerTitleText;
    public TextMeshProUGUI offerDescriptionText;
    public TextMeshProUGUI offerRewardText;
    public TextMeshProUGUI offerRiskText;
    public Slider offerTimerSlider;
    public TextMeshProUGUI offerTimerText;
    public Transform scientistListContainer;
    public GameObject scientistButtonPrefab;
    public Button rejectOfferButton;

    [Header("Süreç Paneli - Process Panel")]
    public TextMeshProUGUI processTargetText;
    public Slider processProgressSlider;
    public TextMeshProUGUI processProgressText;
    public Slider riskMeterSlider;
    public TextMeshProUGUI riskMeterText;
    public TextMeshProUGUI accumulatedStatsText;
    public GameObject postProcessIndicator;

    [Header("Event Paneli - Event Panel")]
    public TextMeshProUGUI eventTitleText;
    public TextMeshProUGUI eventDescriptionText;
    public Transform choiceButtonContainer;
    public GameObject choiceButtonPrefab;
    public Slider eventTimerSlider;
    public TextMeshProUGUI eventTimerText;

    [Header("Sonuç Paneli - Result Panel")]
    public TextMeshProUGUI resultTitleText;
    public TextMeshProUGUI resultDescriptionText;
    public TextMeshProUGUI resultWealthText;
    public TextMeshProUGUI resultSuspicionText;
    public Button resultContinueButton;

    [Header("Öldürülen Bilim Adamları Paneli")]
    public TextMeshProUGUI killedScientistsText;
    public Button killedAcknowledgeButton;

    [Header("Renkler")]
    public Color lowRiskColor = new Color(0.2f, 0.8f, 0.2f);
    public Color mediumRiskColor = new Color(0.9f, 0.7f, 0.1f);
    public Color highRiskColor = new Color(0.9f, 0.2f, 0.2f);

    // Only show UI panels when player explicitly opens the minigame
    private bool isOpen = false;

    private IllegalScientistProviderEvent currentOffer;
    private IllegalScientistProviderEvent currentEvent;
    private List<GameObject> spawnedScientistButtons = new List<GameObject>();
    private List<GameObject> spawnedChoiceButtons = new List<GameObject>();

    // ==================== LIFECYCLE ====================

    private void Awake()
    {
        // Always hide all panels on start.
        // Root GameObject must be active so this runs — panels are hidden here in code,
        // so it doesn't matter what state they are set to in the scene.
        ForceHideAll();
    }

    private void ForceHideAll()
    {
        if (offerPanel != null) offerPanel.SetActive(false);
        if (processPanel != null) processPanel.SetActive(false);
        if (eventPanel != null) eventPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (scientistsKilledPanel != null) scientistsKilledPanel.SetActive(false);
        isOpen = false;
    }

    private void OnEnable()
    {
        IllegalScientistProviderManager.OnOfferReceived += HandleOfferReceived;
        IllegalScientistProviderManager.OnOfferDecisionTimerUpdate += HandleOfferTimerUpdate;
        IllegalScientistProviderManager.OnProcessStarted += HandleProcessStarted;
        IllegalScientistProviderManager.OnProcessProgress += HandleProcessProgress;
        IllegalScientistProviderManager.OnSmuggleEventTriggered += HandleEventTriggered;
        IllegalScientistProviderManager.OnEventDecisionTimerUpdate += HandleEventTimerUpdate;
        IllegalScientistProviderManager.OnSmuggleEventResolved += HandleEventResolved;
        IllegalScientistProviderManager.OnMinigameFailed += HandleMinigameFailed;
        IllegalScientistProviderManager.OnProcessCompleted += HandleProcessCompleted;
        IllegalScientistProviderManager.OnPostProcessStarted += HandlePostProcessStarted;
        IllegalScientistProviderManager.OnPostProcessEnded += HandlePostProcessEnded;
        IllegalScientistProviderManager.OnScientistsKilled += HandleScientistsKilled;
    }

    private void OnDisable()
    {
        IllegalScientistProviderManager.OnOfferReceived -= HandleOfferReceived;
        IllegalScientistProviderManager.OnOfferDecisionTimerUpdate -= HandleOfferTimerUpdate;
        IllegalScientistProviderManager.OnProcessStarted -= HandleProcessStarted;
        IllegalScientistProviderManager.OnProcessProgress -= HandleProcessProgress;
        IllegalScientistProviderManager.OnSmuggleEventTriggered -= HandleEventTriggered;
        IllegalScientistProviderManager.OnEventDecisionTimerUpdate -= HandleEventTimerUpdate;
        IllegalScientistProviderManager.OnSmuggleEventResolved -= HandleEventResolved;
        IllegalScientistProviderManager.OnMinigameFailed -= HandleMinigameFailed;
        IllegalScientistProviderManager.OnProcessCompleted -= HandleProcessCompleted;
        IllegalScientistProviderManager.OnPostProcessStarted -= HandlePostProcessStarted;
        IllegalScientistProviderManager.OnPostProcessEnded -= HandlePostProcessEnded;
        IllegalScientistProviderManager.OnScientistsKilled -= HandleScientistsKilled;
    }

    private void Start()
    {
        if (rejectOfferButton != null)
            rejectOfferButton.onClick.AddListener(OnRejectOfferClicked);
        if (resultContinueButton != null)
            resultContinueButton.onClick.AddListener(OnResultContinueClicked);
        if (killedAcknowledgeButton != null)
            killedAcknowledgeButton.onClick.AddListener(OnKilledAcknowledgeClicked);
    }

    private void Update()
    {
        if (!isOpen) return;
        if (IllegalScientistProviderManager.Instance == null) return;
        var state = IllegalScientistProviderManager.Instance.GetCurrentState();
        if (state == IllegalScientistProviderState.ActiveProcess ||
            state == IllegalScientistProviderState.EventPhase)
        {
            UpdateRiskMeter();
        }
    }

    // ==================== PANEL YÖNETİMİ ====================

    private void HideAllPanels()
    {
        if (offerPanel != null) offerPanel.SetActive(false);
        if (processPanel != null) processPanel.SetActive(false);
        if (eventPanel != null) eventPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (scientistsKilledPanel != null) scientistsKilledPanel.SetActive(false);
    }

    private void ShowOfferPanel(IllegalScientistProviderEvent offer)
    {
        if (!isOpen) return;
        HideAllPanels();
        if (offerPanel == null) return;

        offerPanel.SetActive(true);
        currentOffer = offer;

        if (offerTitleText != null) offerTitleText.text = offer.displayName;
        if (offerDescriptionText != null) offerDescriptionText.text = offer.description;
        if (offerRewardText != null) offerRewardText.text = $"Ödül: ${offer.baseReward:N0}";

        if (offerRiskText != null)
        {
            offerRiskText.text = $"Risk: {GetRiskLabel(offer.riskLevel)} ({offer.riskLevel * 100:F0}%)";
            offerRiskText.color = GetRiskColor(offer.riskLevel);
        }

        if (offerTimerSlider != null)
        {
            offerTimerSlider.maxValue = offer.decisionTime;
            offerTimerSlider.value = offer.decisionTime;
        }

        PopulateScientistList();
    }

    private void ShowProcessPanel(IllegalScientistProviderEvent offer, float duration)
    {
        if (!isOpen) return;
        HideAllPanels();
        if (processPanel == null) return;

        processPanel.SetActive(true);
        if (processTargetText != null) processTargetText.text = $"Hedef: {offer.displayName}";
        if (processProgressSlider != null) { processProgressSlider.maxValue = 1f; processProgressSlider.value = 0f; }
        if (processProgressText != null) processProgressText.text = "0%";
        if (postProcessIndicator != null) postProcessIndicator.SetActive(false);

        UpdateRiskMeter();
    }

    private void ShowEventPanel(IllegalScientistProviderEvent evt)
    {
        if (!isOpen) return;
        if (eventPanel == null) return;

        eventPanel.SetActive(true);
        currentEvent = evt;

        if (eventTitleText != null) eventTitleText.text = evt.displayName;
        if (eventDescriptionText != null) eventDescriptionText.text = evt.description;

        if (eventTimerSlider != null)
        {
            eventTimerSlider.maxValue = evt.decisionTime;
            eventTimerSlider.value = evt.decisionTime;
        }

        PopulateChoiceButtons(evt);
    }

    private void HideEventPanel()
    {
        if (eventPanel != null) eventPanel.SetActive(false);
        currentEvent = null;
        ClearChoiceButtons();
    }

    private void ShowResultPanel(bool success, IllegalScientistProviderResult result)
    {
        if (!isOpen) return;
        HideEventPanel();
        if (resultPanel == null) return;
        resultPanel.SetActive(true);

        if (resultTitleText != null)
        {
            resultTitleText.text = success ? "OPERASYON BAŞARILI!" : "OPERASYON BAŞARISIZ!";
            resultTitleText.color = success ? lowRiskColor : highRiskColor;
        }

        if (resultDescriptionText != null)
        {
            resultDescriptionText.text = success
                ? $"Bilim adamı güvenli bir şekilde {result.offer.displayName}'e ulaştırıldı."
                : "Operasyon deşifre oldu! Bilim adamı yakalandı.";
        }

        if (resultWealthText != null)
        {
            string prefix = result.wealthChange >= 0 ? "+" : "";
            resultWealthText.text = $"Kazanç: {prefix}${result.wealthChange:N0}";
            resultWealthText.color = result.wealthChange >= 0 ? lowRiskColor : highRiskColor;
        }

        if (resultSuspicionText != null)
        {
            if (result.suspicionChange != 0)
            {
                string prefix = result.suspicionChange >= 0 ? "+" : "";
                resultSuspicionText.text = $"Şüphe: {prefix}{result.suspicionChange:F1}";
                resultSuspicionText.color = result.suspicionChange > 0 ? highRiskColor : lowRiskColor;
                resultSuspicionText.gameObject.SetActive(true);
            }
            else
            {
                resultSuspicionText.gameObject.SetActive(false);
            }
        }
    }

    private void ShowScientistsKilledPanel(List<ScientistData> killed)
    {
        if (!isOpen) return;
        if (scientistsKilledPanel == null) return;
        scientistsKilledPanel.SetActive(true);

        if (killedScientistsText != null)
        {
            if (killed.Count == 1)
                killedScientistsText.text = $"{killed[0].displayName} öldürüldü!";
            else
            {
                string names = string.Join(", ", killed.ConvertAll(s => s.displayName));
                killedScientistsText.text = $"{killed.Count} bilim adamı öldürüldü:\n{names}";
            }
        }
    }

    // ==================== BİLİM ADAMI LİSTESİ ====================

    private void PopulateScientistList()
    {
        ClearScientistButtons();
        if (scientistListContainer == null || scientistButtonPrefab == null) return;
        if (SkillTreeManager.Instance == null) return;

        int scientistCount = SkillTreeManager.Instance.GetScientistCount();

        if (scientistCount == 0)
        {
            GameObject infoObj = Instantiate(scientistButtonPrefab, scientistListContainer);
            spawnedScientistButtons.Add(infoObj);
            var button = infoObj.GetComponent<Button>();
            if (button != null) button.interactable = false;
            var allTexts = infoObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (allTexts.Length > 0) allTexts[0].text = "Eğitimli bilim adamı yok! Teklifi reddetmelisiniz.";
            return;
        }

        for (int i = 0; i < scientistCount; i++)
        {
            ScientistTraining scientist = SkillTreeManager.Instance.GetScientist(i);
            if (scientist == null) continue;

            GameObject buttonObj = Instantiate(scientistButtonPrefab, scientistListContainer);
            spawnedScientistButtons.Add(buttonObj);
            var button = buttonObj.GetComponent<Button>();

            var scientistButton = buttonObj.GetComponent<IllegalScientistProviderScientistButton>();
            if (scientistButton != null)
            {
                scientistButton.SetupScientist(scientist);
            }
            else
            {
                var allTexts = buttonObj.GetComponentsInChildren<TextMeshProUGUI>();
                if (allTexts.Length > 0) allTexts[0].text = scientist.data.displayName;
                if (allTexts.Length > 1) allTexts[1].text = $"Gizlilik: {scientist.data.stealthLevel * 100:F0}%";
                if (allTexts.Length > 2) allTexts[2].text = scientist.isCompleted ? "Hazır" : "Eğitimde";
            }

            if (!scientist.isCompleted)
            {
                if (button != null) button.interactable = false;
            }
            else
            {
                int capturedIndex = i;
                button?.onClick.AddListener(() => OnScientistSelected(capturedIndex));
            }
        }
    }

    private void ClearScientistButtons()
    {
        foreach (var btn in spawnedScientistButtons)
            if (btn != null) Destroy(btn);
        spawnedScientistButtons.Clear();
    }

    // ==================== SEÇENEK BUTONLARI ====================

    private void PopulateChoiceButtons(IllegalScientistProviderEvent evt)
    {
        ClearChoiceButtons();
        if (choiceButtonContainer == null || choiceButtonPrefab == null) return;
        if (evt.choices == null || evt.choices.Count == 0) return;

        for (int i = 0; i < evt.choices.Count; i++)
        {
            IllegalScientistProviderEventChoice choice = evt.choices[i];
            GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceButtonContainer);
            spawnedChoiceButtons.Add(buttonObj);

            var button = buttonObj.GetComponent<Button>();
            var choiceButton = buttonObj.GetComponent<IllegalScientistProviderChoiceButton>();
            if (choiceButton != null)
            {
                choiceButton.SetupChoice(choice);
            }
            else
            {
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                    text.text = $"{choice.displayName}\n{choice.description}\n<size=80%><color=#888888>{BuildModifierText(choice)}</color></size>";
            }

            int capturedIndex = i;
            button?.onClick.AddListener(() => OnChoiceSelected(capturedIndex));
        }
    }

    private void ClearChoiceButtons()
    {
        foreach (var btn in spawnedChoiceButtons)
            if (btn != null) Destroy(btn);
        spawnedChoiceButtons.Clear();
    }

    private string BuildModifierText(IllegalScientistProviderEventChoice choice)
    {
        var mods = new List<string>();
        if (choice.riskModifier != 0) mods.Add($"Risk: {(choice.riskModifier > 0 ? "+" : "")}{choice.riskModifier * 100:F0}%");
        if (choice.suspicionModifier != 0) mods.Add($"Şüphe: {(choice.suspicionModifier > 0 ? "+" : "")}{choice.suspicionModifier:F1}");
        if (choice.costModifier != 0) mods.Add($"Para: {(choice.costModifier > 0 ? "-" : "+")}${Mathf.Abs(choice.costModifier)}");
        return mods.Count > 0 ? string.Join(" | ", mods) : "Etkisiz";
    }

    // ==================== RİSK GÖSTERGE ====================

    private void UpdateRiskMeter()
    {
        if (IllegalScientistProviderManager.Instance == null) return;
        float risk = IllegalScientistProviderManager.Instance.GetEffectiveRisk();
        if (riskMeterSlider != null) { riskMeterSlider.maxValue = 1f; riskMeterSlider.value = risk; }
        if (riskMeterText != null) { riskMeterText.text = $"Risk: {risk * 100:F0}%"; riskMeterText.color = GetRiskColor(risk); }
    }

    private Color GetRiskColor(float risk)
    {
        if (risk < 0.33f) return lowRiskColor;
        if (risk < 0.66f) return mediumRiskColor;
        return highRiskColor;
    }

    private string GetRiskLabel(float risk)
    {
        if (risk < 0.33f) return "Düşük";
        if (risk < 0.66f) return "Orta";
        return "Yüksek";
    }

    // ==================== EVENT HANDLER'LAR ====================

    private void HandleOfferReceived(IllegalScientistProviderEvent offer) => ShowOfferPanel(offer);

    private void HandleOfferTimerUpdate(float remainingTime)
    {
        if (!isOpen) return;
        if (offerTimerSlider != null) offerTimerSlider.value = remainingTime;
        if (offerTimerText != null) offerTimerText.text = $"{remainingTime:F1}s";
    }

    private void HandleProcessStarted(IllegalScientistProviderEvent offer, float duration) => ShowProcessPanel(offer, duration);

    private void HandleProcessProgress(float progress)
    {
        if (!isOpen) return;
        if (processProgressSlider != null) processProgressSlider.value = progress;
        if (processProgressText != null) processProgressText.text = $"{progress * 100:F0}%";
    }

    private void HandleEventTriggered(IllegalScientistProviderEvent evt) => ShowEventPanel(evt);

    private void HandleEventTimerUpdate(float remainingTime)
    {
        if (!isOpen) return;
        if (eventTimerSlider != null) eventTimerSlider.value = remainingTime;
        if (eventTimerText != null) eventTimerText.text = $"{remainingTime:F1}s";
    }

    private void HandleEventResolved(IllegalScientistProviderEventChoice choice) => HideEventPanel();

    private void HandleMinigameFailed(string reason)
    {
        if (!isOpen) return;
        var failResult = new IllegalScientistProviderResult
        {
            success = false,
            offer = currentOffer,
            wealthChange = 0,
            suspicionChange = 0
        };
        ShowResultPanel(false, failResult);
        if (resultDescriptionText != null) resultDescriptionText.text = reason;
    }

    private void HandleProcessCompleted(IllegalScientistProviderResult result) => ShowResultPanel(result.success, result);

    private void HandlePostProcessStarted()
    {
        if (!isOpen) return;
        if (resultPanel != null) resultPanel.SetActive(false);
        if (processPanel != null)
        {
            processPanel.SetActive(true);
            if (processTargetText != null) processTargetText.text = "Musallat Süreci";
            if (processProgressSlider != null) processProgressSlider.gameObject.SetActive(false);
            if (processProgressText != null) processProgressText.text = "Operasyon sonrası takip devam ediyor...";
            if (postProcessIndicator != null) postProcessIndicator.SetActive(true);
        }
    }

    private void HandlePostProcessEnded()
    {
        HideAllPanels();
        isOpen = false;
    }

    private void HandleScientistsKilled(List<ScientistData> killed) => ShowScientistsKilledPanel(killed);

    // ==================== BUTON CALLBACK'LERİ ====================

    private void OnScientistSelected(int scientistIndex)
    {
        IllegalScientistProviderManager.Instance?.AcceptOffer(scientistIndex);
    }

    private void OnRejectOfferClicked()
    {
        IllegalScientistProviderManager.Instance?.RejectOffer();
        HideAllPanels();
        isOpen = false;
    }

    private void OnChoiceSelected(int choiceIndex)
    {
        if (IllegalScientistProviderManager.Instance == null) return;
        var state = IllegalScientistProviderManager.Instance.GetCurrentState();
        if (state == IllegalScientistProviderState.EventPhase)
            IllegalScientistProviderManager.Instance.ResolveEvent(choiceIndex);
        else if (state == IllegalScientistProviderState.PostEventPhase)
            IllegalScientistProviderManager.Instance.ResolvePostEvent(choiceIndex);
    }

    private void OnResultContinueClicked()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    private void OnKilledAcknowledgeClicked()
    {
        if (scientistsKilledPanel != null) scientistsKilledPanel.SetActive(false);
    }

    // ==================== PUBLIC METODLAR ====================

    /// <summary>
    /// Minigame'i açar ve teklif tetikler.
    /// Root GameObject her zaman sahnede aktif olmalı.
    /// </summary>
    public void OpenAndTriggerOffer()
    {
        isOpen = true;

        if (IllegalScientistProviderManager.Instance == null) return;

        // If a process is already active, show the right panel
        if (IllegalScientistProviderManager.Instance.IsActive())
        {
            var state = IllegalScientistProviderManager.Instance.GetCurrentState();
            switch (state)
            {
                case IllegalScientistProviderState.ActiveProcess:
                case IllegalScientistProviderState.EventPhase:
                    if (processPanel != null) processPanel.SetActive(true);
                    break;
                case IllegalScientistProviderState.PostProcess:
                case IllegalScientistProviderState.PostEventPhase:
                    if (processPanel != null) processPanel.SetActive(true);
                    break;
            }
            return;
        }

        IllegalScientistProviderManager.Instance.GenerateOffer();
    }

    public void OpenMinigame()
    {
        isOpen = true;
    }

    public void CloseMinigame()
    {
        HideAllPanels();
        isOpen = false;
    }

    public bool IsMinigameActive()
    {
        if (IllegalScientistProviderManager.Instance == null) return false;
        return IllegalScientistProviderManager.Instance.IsActive();
    }
}