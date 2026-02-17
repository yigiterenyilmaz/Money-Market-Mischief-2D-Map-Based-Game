using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ScientistSmuggle minigame UI controller.
/// Handles all UI states: Offer, Process, Event choices, Results, and PostProcess.
/// </summary>
public class ScientistSmuggleUI : MonoBehaviour
{
    [Header("Ana Paneller")]
    public GameObject offerPanel;           // Teklif geldiğinde gösterilen panel
    public GameObject processPanel;         // Operasyon sırasında gösterilen panel
    public GameObject eventPanel;           // Event seçim popup'ı (process ve postProcess için ortak)
    public GameObject resultPanel;          // Operasyon sonucu paneli
    public GameObject scientistsKilledPanel;// Bilim adamları öldürüldü bildirimi

    [Header("Teklif Paneli - Offer Panel")]
    public TextMeshProUGUI offerTitleText;          // Ülke/teklif adı
    public TextMeshProUGUI offerDescriptionText;    // Ülke durumu açıklaması
    public TextMeshProUGUI offerRewardText;         // Taban ödül
    public TextMeshProUGUI offerRiskText;           // Risk seviyesi
    public Slider offerTimerSlider;                 // Karar süresi slider
    public TextMeshProUGUI offerTimerText;          // Karar süresi text
    public Transform scientistListContainer;        // Bilim adamı butonlarının parent'ı
    public GameObject scientistButtonPrefab;        // Bilim adamı seçim butonu prefab'ı
    public Button rejectOfferButton;                // Reddet butonu

    [Header("Süreç Paneli - Process Panel")]
    public TextMeshProUGUI processTargetText;       // Hedef ülke adı
    public Slider processProgressSlider;            // Operasyon ilerleme çubuğu
    public TextMeshProUGUI processProgressText;     // İlerleme yüzdesi
    public Slider riskMeterSlider;                  // Risk göstergesi
    public TextMeshProUGUI riskMeterText;           // Risk yüzdesi
    public TextMeshProUGUI accumulatedStatsText;    // Biriken modifier'lar (opsiyonel debug)
    public GameObject postProcessIndicator;         // PostProcess modunda olduğunu gösteren indicator

    [Header("Event Paneli - Event Panel")]
    public TextMeshProUGUI eventTitleText;          // Event başlığı
    public TextMeshProUGUI eventDescriptionText;    // Event açıklaması
    public Transform choiceButtonContainer;         // Seçenek butonlarının parent'ı
    public GameObject choiceButtonPrefab;           // Seçenek butonu prefab'ı
    public Slider eventTimerSlider;                 // Event karar süresi slider
    public TextMeshProUGUI eventTimerText;          // Event karar süresi text

    [Header("Sonuç Paneli - Result Panel")]
    public TextMeshProUGUI resultTitleText;         // Başarılı/Başarısız
    public TextMeshProUGUI resultDescriptionText;   // Sonuç açıklaması
    public TextMeshProUGUI resultWealthText;        // Kazanç/kayıp
    public TextMeshProUGUI resultSuspicionText;     // Şüphe değişimi
    public Button resultContinueButton;             // Devam butonu (PostProcess'e geçiş veya kapatma)

    [Header("Öldürülen Bilim Adamları Paneli")]
    public TextMeshProUGUI killedScientistsText;    // Öldürülen bilim adamları listesi
    public Button killedAcknowledgeButton;          // Tamam butonu

    [Header("Renkler")]
    public Color lowRiskColor = new Color(0.2f, 0.8f, 0.2f);    // Düşük risk (yeşil)
    public Color mediumRiskColor = new Color(0.9f, 0.7f, 0.1f); // Orta risk (sarı)
    public Color highRiskColor = new Color(0.9f, 0.2f, 0.2f);   // Yüksek risk (kırmızı)

    // Runtime değişkenler
    private ScientistSmuggleEvent currentOffer;
    private ScientistSmuggleEvent currentEvent;
    private List<GameObject> spawnedScientistButtons = new List<GameObject>();
    private List<GameObject> spawnedChoiceButtons = new List<GameObject>();

    // ==================== LIFECYCLE ====================

    private void OnEnable()
    {
        // Event'lere abone ol
        ScientistSmuggleManager.OnOfferReceived += HandleOfferReceived;
        ScientistSmuggleManager.OnOfferDecisionTimerUpdate += HandleOfferTimerUpdate;
        ScientistSmuggleManager.OnProcessStarted += HandleProcessStarted;
        ScientistSmuggleManager.OnProcessProgress += HandleProcessProgress;
        ScientistSmuggleManager.OnSmuggleEventTriggered += HandleEventTriggered;
        ScientistSmuggleManager.OnEventDecisionTimerUpdate += HandleEventTimerUpdate;
        ScientistSmuggleManager.OnSmuggleEventResolved += HandleEventResolved;
        ScientistSmuggleManager.OnMinigameFailed += HandleMinigameFailed;
        ScientistSmuggleManager.OnProcessCompleted += HandleProcessCompleted;
        ScientistSmuggleManager.OnPostProcessStarted += HandlePostProcessStarted;
        ScientistSmuggleManager.OnPostProcessEnded += HandlePostProcessEnded;
        ScientistSmuggleManager.OnScientistsKilled += HandleScientistsKilled;
    }

    private void OnDisable()
    {
        // Event'lerden çık
        ScientistSmuggleManager.OnOfferReceived -= HandleOfferReceived;
        ScientistSmuggleManager.OnOfferDecisionTimerUpdate -= HandleOfferTimerUpdate;
        ScientistSmuggleManager.OnProcessStarted -= HandleProcessStarted;
        ScientistSmuggleManager.OnProcessProgress -= HandleProcessProgress;
        ScientistSmuggleManager.OnSmuggleEventTriggered -= HandleEventTriggered;
        ScientistSmuggleManager.OnEventDecisionTimerUpdate -= HandleEventTimerUpdate;
        ScientistSmuggleManager.OnSmuggleEventResolved -= HandleEventResolved;
        ScientistSmuggleManager.OnMinigameFailed -= HandleMinigameFailed;
        ScientistSmuggleManager.OnProcessCompleted -= HandleProcessCompleted;
        ScientistSmuggleManager.OnPostProcessStarted -= HandlePostProcessStarted;
        ScientistSmuggleManager.OnPostProcessEnded -= HandlePostProcessEnded;
        ScientistSmuggleManager.OnScientistsKilled -= HandleScientistsKilled;
    }

    private void Start()
    {
        // Buton listener'ları
        if (rejectOfferButton != null)
            rejectOfferButton.onClick.AddListener(OnRejectOfferClicked);

        if (resultContinueButton != null)
            resultContinueButton.onClick.AddListener(OnResultContinueClicked);

        if (killedAcknowledgeButton != null)
            killedAcknowledgeButton.onClick.AddListener(OnKilledAcknowledgeClicked);

        // Başlangıçta tüm panelleri gizle
        HideAllPanels();
    }

    private void Update()
    {
        // Risk göstergesini sürekli güncelle (process sırasında)
        if (ScientistSmuggleManager.Instance != null)
        {
            var state = ScientistSmuggleManager.Instance.GetCurrentState();
            if (state == ScientistSmuggleState.ActiveProcess ||
                state == ScientistSmuggleState.EventPhase)
            {
                UpdateRiskMeter();
            }
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

    private void ShowOfferPanel(ScientistSmuggleEvent offer)
    {
        HideAllPanels();
        if (offerPanel == null) return;

        offerPanel.SetActive(true);
        currentOffer = offer;

        // Teklif bilgilerini doldur
        if (offerTitleText != null)
            offerTitleText.text = offer.displayName;

        if (offerDescriptionText != null)
            offerDescriptionText.text = offer.description;

        if (offerRewardText != null)
            offerRewardText.text = $"Ödül: ${offer.baseReward:N0}";

        if (offerRiskText != null)
        {
            string riskLabel = GetRiskLabel(offer.riskLevel);
            offerRiskText.text = $"Risk: {riskLabel} ({offer.riskLevel * 100:F0}%)";
            offerRiskText.color = GetRiskColor(offer.riskLevel);
        }

        // Timer başlangıç değeri
        if (offerTimerSlider != null)
        {
            offerTimerSlider.maxValue = offer.decisionTime;
            offerTimerSlider.value = offer.decisionTime;
        }

        // Bilim adamı listesini doldur
        PopulateScientistList();
    }

    private void ShowProcessPanel(ScientistSmuggleEvent offer, float duration)
    {
        HideAllPanels();
        if (processPanel == null) return;

        processPanel.SetActive(true);

        if (processTargetText != null)
            processTargetText.text = $"Hedef: {offer.displayName}";

        if (processProgressSlider != null)
        {
            processProgressSlider.maxValue = 1f;
            processProgressSlider.value = 0f;
        }

        if (processProgressText != null)
            processProgressText.text = "0%";

        if (postProcessIndicator != null)
            postProcessIndicator.SetActive(false);

        UpdateRiskMeter();
    }

    private void ShowEventPanel(ScientistSmuggleEvent evt)
    {
        if (eventPanel == null) return;

        eventPanel.SetActive(true);
        currentEvent = evt;

        if (eventTitleText != null)
            eventTitleText.text = evt.displayName;

        if (eventDescriptionText != null)
            eventDescriptionText.text = evt.description;

        if (eventTimerSlider != null)
        {
            eventTimerSlider.maxValue = evt.decisionTime;
            eventTimerSlider.value = evt.decisionTime;
        }

        // Seçenek butonlarını oluştur
        PopulateChoiceButtons(evt);
    }

    private void HideEventPanel()
    {
        if (eventPanel != null)
            eventPanel.SetActive(false);

        currentEvent = null;
        ClearChoiceButtons();
    }

    private void ShowResultPanel(bool success, ScientistSmuggleResult result)
    {
        // Event paneli açıksa kapat
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
        if (scientistsKilledPanel == null) return;

        scientistsKilledPanel.SetActive(true);

        if (killedScientistsText != null)
        {
            if (killed.Count == 1)
            {
                killedScientistsText.text = $"{killed[0].displayName} öldürüldü!";
            }
            else
            {
                string names = "";
                for (int i = 0; i < killed.Count; i++)
                {
                    if (i > 0) names += ", ";
                    names += killed[i].displayName;
                }
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

        for (int i = 0; i < scientistCount; i++)
        {
            ScientistTraining scientist = SkillTreeManager.Instance.GetScientist(i);
            if (scientist == null) continue;

            GameObject buttonObj = Instantiate(scientistButtonPrefab, scientistListContainer);
            spawnedScientistButtons.Add(buttonObj);

            var button = buttonObj.GetComponent<Button>();
            var texts = buttonObj.GetComponentsInChildren<TextMeshProUGUI>();

            // Ana text'i bul ve doldur
            if (texts.Length > 0)
            {
                string status = scientist.isCompleted ? "Hazır" : "Eğitimde";
                texts[0].text = $"{scientist.data.displayName}\n" +
                                $"Gizlilik: {scientist.data.stealthLevel * 100:F0}%\n" +
                                $"{status}";
            }

            // Eğitimi tamamlanmamışsa butonu devre dışı bırak
            if (!scientist.isCompleted)
            {
                button.interactable = false;
            }
            else
            {
                int capturedIndex = i; // Closure için
                button.onClick.AddListener(() => OnScientistSelected(capturedIndex));
            }
        }

        // Hiç bilim adamı yoksa bilgi mesajı göster
        if (scientistCount == 0)
        {
            GameObject infoObj = Instantiate(scientistButtonPrefab, scientistListContainer);
            spawnedScientistButtons.Add(infoObj);

            var button = infoObj.GetComponent<Button>();
            if (button != null) button.interactable = false;

            var texts = infoObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0)
            {
                texts[0].text = "Eğitimli bilim adamı yok!\nTeklifi reddetmelisiniz.";
            }
        }
    }

    private void ClearScientistButtons()
    {
        foreach (var btn in spawnedScientistButtons)
        {
            if (btn != null) Destroy(btn);
        }
        spawnedScientistButtons.Clear();
    }

    // ==================== SEÇENEK BUTONLARI ====================

    private void PopulateChoiceButtons(ScientistSmuggleEvent evt)
    {
        ClearChoiceButtons();

        if (choiceButtonContainer == null || choiceButtonPrefab == null) return;
        if (evt.choices == null || evt.choices.Count == 0) return;

        for (int i = 0; i < evt.choices.Count; i++)
        {
            ScientistSmuggleEventChoice choice = evt.choices[i];

            GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceButtonContainer);
            spawnedChoiceButtons.Add(buttonObj);

            var button = buttonObj.GetComponent<Button>();
            var texts = buttonObj.GetComponentsInChildren<TextMeshProUGUI>();

            // Seçenek bilgilerini göster
            if (texts.Length > 0)
            {
                string modifiers = BuildModifierText(choice);
                texts[0].text = $"{choice.displayName}\n" +
                                $"<size=80%>{choice.description}</size>\n" +
                                $"<size=70%><color=#888888>{modifiers}</color></size>";
            }

            int capturedIndex = i; // Closure için
            button.onClick.AddListener(() => OnChoiceSelected(capturedIndex));
        }
    }

    private void ClearChoiceButtons()
    {
        foreach (var btn in spawnedChoiceButtons)
        {
            if (btn != null) Destroy(btn);
        }
        spawnedChoiceButtons.Clear();
    }

    private string BuildModifierText(ScientistSmuggleEventChoice choice)
    {
        List<string> mods = new List<string>();

        if (choice.riskModifier != 0)
        {
            string sign = choice.riskModifier > 0 ? "+" : "";
            mods.Add($"Risk: {sign}{choice.riskModifier * 100:F0}%");
        }

        if (choice.suspicionModifier != 0)
        {
            string sign = choice.suspicionModifier > 0 ? "+" : "";
            mods.Add($"Şüphe: {sign}{choice.suspicionModifier:F1}");
        }

        if (choice.costModifier != 0)
        {
            string sign = choice.costModifier > 0 ? "-" : "+";
            mods.Add($"Para: {sign}${Mathf.Abs(choice.costModifier)}");
        }

        return mods.Count > 0 ? string.Join(" | ", mods) : "Etkisiz";
    }

    // ==================== RİSK GÖSTERGE ====================

    private void UpdateRiskMeter()
    {
        if (ScientistSmuggleManager.Instance == null) return;

        float risk = ScientistSmuggleManager.Instance.GetEffectiveRisk();

        if (riskMeterSlider != null)
        {
            riskMeterSlider.maxValue = 1f;
            riskMeterSlider.value = risk;
        }

        if (riskMeterText != null)
        {
            riskMeterText.text = $"Risk: {risk * 100:F0}%";
            riskMeterText.color = GetRiskColor(risk);
        }
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

    private void HandleOfferReceived(ScientistSmuggleEvent offer)
    {
        ShowOfferPanel(offer);
    }

    private void HandleOfferTimerUpdate(float remainingTime)
    {
        if (offerTimerSlider != null)
            offerTimerSlider.value = remainingTime;

        if (offerTimerText != null)
            offerTimerText.text = $"{remainingTime:F1}s";
    }

    private void HandleProcessStarted(ScientistSmuggleEvent offer, float duration)
    {
        ShowProcessPanel(offer, duration);
    }

    private void HandleProcessProgress(float progress)
    {
        if (processProgressSlider != null)
            processProgressSlider.value = progress;

        if (processProgressText != null)
            processProgressText.text = $"{progress * 100:F0}%";
    }

    private void HandleEventTriggered(ScientistSmuggleEvent evt)
    {
        ShowEventPanel(evt);
    }

    private void HandleEventTimerUpdate(float remainingTime)
    {
        if (eventTimerSlider != null)
            eventTimerSlider.value = remainingTime;

        if (eventTimerText != null)
            eventTimerText.text = $"{remainingTime:F1}s";
    }

    private void HandleEventResolved(ScientistSmuggleEventChoice choice)
    {
        HideEventPanel();
    }

    private void HandleMinigameFailed(string reason)
    {
        // Başarısız sonuç için result göster
        if (ScientistSmuggleManager.Instance != null)
        {
            // Manuel result oluştur (manager'dan gelmiyor)
            ScientistSmuggleResult failResult = new ScientistSmuggleResult();
            failResult.success = false;
            failResult.offer = currentOffer;
            failResult.wealthChange = 0;
            failResult.suspicionChange = 0;

            ShowResultPanel(false, failResult);

            if (resultDescriptionText != null)
                resultDescriptionText.text = reason;
        }
    }

    private void HandleProcessCompleted(ScientistSmuggleResult result)
    {
        ShowResultPanel(result.success, result);
    }

    private void HandlePostProcessStarted()
    {
        // Result panelini kapat (eğer açıksa)
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // Process panelini PostProcess modunda göster
        if (processPanel != null)
        {
            processPanel.SetActive(true);

            if (processTargetText != null)
                processTargetText.text = "Musallat Süreci";

            if (processProgressSlider != null)
                processProgressSlider.gameObject.SetActive(false);

            if (processProgressText != null)
                processProgressText.text = "Operasyon sonrası takip devam ediyor...";

            if (postProcessIndicator != null)
                postProcessIndicator.SetActive(true);
        }
    }

    private void HandlePostProcessEnded()
    {
        HideAllPanels();
    }

    private void HandleScientistsKilled(List<ScientistData> killed)
    {
        ShowScientistsKilledPanel(killed);
    }

    // ==================== BUTON CALLBACK'LERİ ====================

    private void OnScientistSelected(int scientistIndex)
    {
        if (ScientistSmuggleManager.Instance == null) return;

        ScientistSmuggleManager.Instance.AcceptOffer(scientistIndex);
    }

    private void OnRejectOfferClicked()
    {
        if (ScientistSmuggleManager.Instance == null) return;

        ScientistSmuggleManager.Instance.RejectOffer();
        HideAllPanels();
    }

    private void OnChoiceSelected(int choiceIndex)
    {
        if (ScientistSmuggleManager.Instance == null) return;

        var state = ScientistSmuggleManager.Instance.GetCurrentState();

        if (state == ScientistSmuggleState.EventPhase)
        {
            ScientistSmuggleManager.Instance.ResolveEvent(choiceIndex);
        }
        else if (state == ScientistSmuggleState.PostEventPhase)
        {
            ScientistSmuggleManager.Instance.ResolvePostEvent(choiceIndex);
        }
    }

    private void OnResultContinueClicked()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // PostProcess başlayacaksa o handle edecek, değilse zaten idle'a dönülmüş
    }

    private void OnKilledAcknowledgeClicked()
    {
        if (scientistsKilledPanel != null)
            scientistsKilledPanel.SetActive(false);
    }

    // ==================== PUBLIC METODLAR ====================

    /// <summary>
    /// Minigame UI'ını açar. Dışarıdan çağrılır.
    /// Eğer aktif bir operasyon varsa ilgili paneli gösterir.
    /// </summary>
    public void OpenMinigame()
    {
        gameObject.SetActive(true);

        // Mevcut duruma göre doğru paneli göster
        if (ScientistSmuggleManager.Instance != null)
        {
            var state = ScientistSmuggleManager.Instance.GetCurrentState();

            switch (state)
            {
                case ScientistSmuggleState.Idle:
                    HideAllPanels();
                    break;
                case ScientistSmuggleState.OfferPending:
                    // Offer zaten event ile gelecek
                    break;
                case ScientistSmuggleState.ActiveProcess:
                case ScientistSmuggleState.EventPhase:
                    // Process paneli zaten gösteriliyor olmalı
                    break;
                case ScientistSmuggleState.PostProcess:
                case ScientistSmuggleState.PostEventPhase:
                    // PostProcess paneli zaten gösteriliyor olmalı
                    break;
            }
        }
    }

    /// <summary>
    /// Minigame UI'ını kapatır.
    /// </summary>
    public void CloseMinigame()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Minigame aktif mi kontrol eder.
    /// </summary>
    public bool IsMinigameActive()
    {
        if (ScientistSmuggleManager.Instance == null) return false;
        return ScientistSmuggleManager.Instance.IsActive();
    }

    // ==================== UTILITY ====================

    private string FormatTime(float seconds)
    {
        if (seconds <= 0) return "0:00";

        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{mins}:{secs:D2}";
    }
    
    public void ShowOfferDirectly(ScientistSmuggleEvent offer)
    {
        if (offer == null || offer.eventType != ScientistSmuggleEventType.Offer) return;
    
        ShowOfferPanel(offer);
    }
}

