using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class PipeHuntUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject toolSelectionPanel;
    public GameObject gamePanel;
    public GameObject resultPanel;

    [Header("Tool Selection")]
    public Transform toolButtonContainer;
    public GameObject toolButtonPrefab;
    public Button startButton;
    public TextMeshProUGUI selectedToolInfoText;

    [Header("Game Screen")]
    public RectTransform gameArea; //boruların yerleştirileceği alan
    public GameObject pipePrefab;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI incomeText;
    public Slider toolDurabilitySlider;
    public TextMeshProUGUI toolDurabilityText;
    public Button leaveButton;
    public GameObject overtimeWarning;
    public Slider suspicionSlider;

    [Header("Result Screen")]
    public TextMeshProUGUI resultTitleText;
    public TextMeshProUGUI resultIncomeText;
    public TextMeshProUGUI resultPipesText;
    public TextMeshProUGUI resultOvertimeText;
    public TextMeshProUGUI resultSuspicionText;
    public Button resultCloseButton;

    [Header("Colors")]
    public Color pipeHiddenColor = new Color(0.6f, 0.4f, 0.2f); //toprak rengi
    public Color pipeDamagedColor = new Color(0.8f, 0.5f, 0.3f); //hasar almış
    public Color pipeBurstColor = new Color(0.2f, 0.2f, 0.2f); //patlamış

    //runtime
    private HuntTool selectedTool;
    private Dictionary<int, GameObject> pipeObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, PipeInstance> pipeDataCache = new Dictionary<int, PipeInstance>();

    private void OnEnable()
    {
        //eventlere abone ol
        PipeHuntManager.OnGameStarted += HandleGameStarted;
        PipeHuntManager.OnTimerUpdate += HandleTimerUpdate;
        PipeHuntManager.OnPipeHit += HandlePipeHit;
        PipeHuntManager.OnPipeBurst += HandlePipeBurst;
        PipeHuntManager.OnEmptyHit += HandleEmptyHit;
        PipeHuntManager.OnToolDamaged += HandleToolDamaged;
        PipeHuntManager.OnToolBroken += HandleToolBroken;
        PipeHuntManager.OnIncomeUpdate += HandleIncomeUpdate;
        PipeHuntManager.OnOvertimeStarted += HandleOvertimeStarted;
        PipeHuntManager.OnOvertimeUpdate += HandleOvertimeUpdate;
        PipeHuntManager.OnGameFinished += HandleGameFinished;
    }

    private void OnDisable()
    {
        //eventlerden çık
        PipeHuntManager.OnGameStarted -= HandleGameStarted;
        PipeHuntManager.OnTimerUpdate -= HandleTimerUpdate;
        PipeHuntManager.OnPipeHit -= HandlePipeHit;
        PipeHuntManager.OnPipeBurst -= HandlePipeBurst;
        PipeHuntManager.OnEmptyHit -= HandleEmptyHit;
        PipeHuntManager.OnToolDamaged -= HandleToolDamaged;
        PipeHuntManager.OnToolBroken -= HandleToolBroken;
        PipeHuntManager.OnIncomeUpdate -= HandleIncomeUpdate;
        PipeHuntManager.OnOvertimeStarted -= HandleOvertimeStarted;
        PipeHuntManager.OnOvertimeUpdate -= HandleOvertimeUpdate;
        PipeHuntManager.OnGameFinished -= HandleGameFinished;
    }

    private void Start()
    {
        //buton listenerları
        startButton.onClick.AddListener(OnStartButtonClicked);
        leaveButton.onClick.AddListener(OnLeaveButtonClicked);
        resultCloseButton.onClick.AddListener(OnResultCloseClicked);

        //başlangıçta tool selection göster
        ShowToolSelection();
    }

    private void Update()
    {
        //oyun sırasında tıklama kontrolü
        if (PipeHuntManager.Instance == null) return;

        var state = PipeHuntManager.Instance.GetCurrentState();
        if (state != PipeHuntState.Active && state != PipeHuntState.Overtime) return;

        //Yeni Input System
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick(Mouse.current.position.ReadValue());
        }
    }

    // ==================== PANEL YÖNETİMİ ====================

    private void ShowToolSelection()
    {
        toolSelectionPanel.SetActive(true);
        gamePanel.SetActive(false);
        resultPanel.SetActive(false);

        selectedTool = null;
        startButton.interactable = false;
        selectedToolInfoText.text = "Bir alet seçin";

        PopulateToolButtons();
    }

    private void ShowGamePanel()
    {
        toolSelectionPanel.SetActive(false);
        gamePanel.SetActive(true);
        resultPanel.SetActive(false);

        overtimeWarning.SetActive(false);
        suspicionSlider.gameObject.SetActive(false);
    }

    private void ShowResultPanel(PipeHuntResult result)
    {
        toolSelectionPanel.SetActive(false);
        gamePanel.SetActive(false);
        resultPanel.SetActive(true);

        //başlık
        switch (result.endReason)
        {
            case PipeHuntEndReason.PlayerLeft:
                resultTitleText.text = "Ayrıldın";
                break;
            case PipeHuntEndReason.GameOver:
                resultTitleText.text = "YAKALANDIN!";
                break;
        }

        resultIncomeText.text = $"Kazanç: ${result.totalIncome:F0}";
        resultPipesText.text = $"Borular: {result.burstPipeCount}/{result.totalPipeCount}";

        if (result.overtimeElapsed > 0)
        {
            resultOvertimeText.text = $"Fazla süre: {result.overtimeElapsed:F1}s";
            resultSuspicionText.text = $"Şüphe: +{result.suspicionAdded:F1}";
            resultOvertimeText.gameObject.SetActive(true);
            resultSuspicionText.gameObject.SetActive(true);
        }
        else
        {
            resultOvertimeText.gameObject.SetActive(false);
            resultSuspicionText.gameObject.SetActive(false);
        }
    }

    // ==================== ALET SEÇİMİ ====================

    private void PopulateToolButtons()
    {
        //mevcut butonları temizle
        foreach (Transform child in toolButtonContainer)
        {
            Destroy(child.gameObject);
        }

        if (PipeHuntManager.Instance == null) return;

        var tools = PipeHuntManager.Instance.GetAvailableTools();

        foreach (var tool in tools)
        {
            GameObject buttonObj = Instantiate(toolButtonPrefab, toolButtonContainer);
            var button = buttonObj.GetComponent<Button>();
            var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                text.text = $"{tool.displayName}\n${tool.cost}";
            }

            HuntTool capturedTool = tool; //closure için
            button.onClick.AddListener(() => OnToolSelected(capturedTool));
        }
    }

    private void OnToolSelected(HuntTool tool)
    {
        selectedTool = tool;
        startButton.interactable = true;

        float duration = PipeHuntManager.Instance.GetToolDuration(tool);
        selectedToolInfoText.text = $"{tool.displayName}\n" +
                                    $"Maliyet: ${tool.cost}\n" +
                                    $"Dayanıklılık: {tool.durability}\n" +
                                    $"Hasar: {tool.damagePerHit}\n" +
                                    $"Süre: {duration:F0}s";
    }

    private void OnStartButtonClicked()
    {
        if (selectedTool == null || PipeHuntManager.Instance == null) return;

        PipeHuntManager.Instance.StartGame(selectedTool);
    }

    // ==================== OYUN EKRANI ====================

    private void HandleClick(Vector2 screenPosition)
    {
        //tıklama game area içinde mi kontrol et
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gameArea, screenPosition, null, out Vector2 localPoint))
        {
            return;
        }

        //normalize koordinata çevir (0-1)
        Rect rect = gameArea.rect;
        float normalizedX = (localPoint.x - rect.xMin) / rect.width;
        float normalizedY = (localPoint.y - rect.yMin) / rect.height;

        //alan dışındaysa işleme
        if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1)
        {
            return;
        }

        //en yakın boruyu bul
        int hitPipeId = FindPipeAtPosition(new Vector2(normalizedX, normalizedY));

        if (hitPipeId >= 0)
        {
            PipeHuntManager.Instance.HitPipe(hitPipeId);
        }
        else
        {
            PipeHuntManager.Instance.HitEmpty();
        }
    }

    private int FindPipeAtPosition(Vector2 normalizedPosition)
    {
        float hitRadius = 0.05f; //tıklama toleransı

        foreach (var kvp in pipeDataCache)
        {
            if (kvp.Value.isBurst) continue; //patlamış boruları atla

            float distance = Vector2.Distance(normalizedPosition, kvp.Value.position);
            if (distance <= hitRadius)
            {
                return kvp.Key;
            }
        }

        return -1; //boru bulunamadı
    }

    private void CreatePipeVisuals(List<PipeInstance> pipes)
    {
        //önceki boruları temizle
        ClearPipeVisuals();

        Rect areaRect = gameArea.rect;

        foreach (var pipe in pipes)
        {
            GameObject pipeObj = Instantiate(pipePrefab, gameArea);
            RectTransform rt = pipeObj.GetComponent<RectTransform>();

            //pivot ve anchor'ı ortala
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            //pozisyonu hesapla (0-1 normalize → pixel)
            float x = pipe.position.x * areaRect.width;
            float y = pipe.position.y * areaRect.height;
            rt.anchoredPosition = new Vector2(x, y);

            //rengi ayarla
            var image = pipeObj.GetComponent<Image>();
            if (image != null)
            {
                image.color = pipeHiddenColor;
            }

            pipeObjects[pipe.id] = pipeObj;
            pipeDataCache[pipe.id] = pipe;
        }
    }

    private void ClearPipeVisuals()
    {
        foreach (var kvp in pipeObjects)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        pipeObjects.Clear();
        pipeDataCache.Clear();
    }

    private void UpdatePipeVisual(PipeInstance pipe)
    {
        if (!pipeObjects.TryGetValue(pipe.id, out GameObject pipeObj)) return;

        var image = pipeObj.GetComponent<Image>();
        if (image == null) return;

        if (pipe.isBurst)
        {
            image.color = pipeBurstColor;
        }
        else
        {
            //hasar oranına göre renk geçişi
            float healthPercent = (float)pipe.remainingDurability / pipe.pipeType.durability;
            image.color = Color.Lerp(pipeDamagedColor, pipeHiddenColor, healthPercent);
        }

        //cache güncelle
        pipeDataCache[pipe.id] = pipe;
    }

    // ==================== EVENT HANDLER'LAR ====================

    private void HandleGameStarted(List<PipeInstance> pipes, float duration, HuntTool tool)
    {
        ShowGamePanel();
        CreatePipeVisuals(pipes);

        timerText.text = FormatTime(duration);
        incomeText.text = "$0";

        toolDurabilitySlider.maxValue = tool.durability;
        toolDurabilitySlider.value = tool.durability;
        toolDurabilityText.text = $"{tool.durability}/{tool.durability}";
    }

    private void HandleTimerUpdate(float remainingTime)
    {
        timerText.text = FormatTime(remainingTime);
    }

    private void HandlePipeHit(PipeInstance pipe, int remainingDurability)
    {
        UpdatePipeVisual(pipe);
        //TODO: vuruş efekti
    }

    private void HandlePipeBurst(PipeInstance pipe)
    {
        UpdatePipeVisual(pipe);
        //TODO: patlama efekti
    }

    private void HandleEmptyHit(int remainingToolDurability)
    {
        //TODO: boş vuruş efekti (ekran sallanması vs.)
    }

    private void HandleToolDamaged(int remainingDurability)
    {
        toolDurabilitySlider.value = remainingDurability;
        toolDurabilityText.text = $"{remainingDurability}/{(int)toolDurabilitySlider.maxValue}";
    }

    private void HandleToolBroken()
    {
        //TODO: kırılma efekti
    }

    private void HandleIncomeUpdate(float totalIncome)
    {
        incomeText.text = $"${totalIncome:F0}";
    }

    private void HandleOvertimeStarted()
    {
        overtimeWarning.SetActive(true);
        suspicionSlider.gameObject.SetActive(true);
        suspicionSlider.value = 0;
    }

    private void HandleOvertimeUpdate(float overtimeElapsed, float totalSuspicion)
    {
        suspicionSlider.value = totalSuspicion;
    }

    private void HandleGameFinished(PipeHuntResult result)
    {
        ClearPipeVisuals();
        ShowResultPanel(result);
    }

    // ==================== BUTON CALLBACK'LERİ ====================

    private void OnLeaveButtonClicked()
    {
        if (PipeHuntManager.Instance != null)
        {
            PipeHuntManager.Instance.LeaveGame();
        }
    }

    private void OnResultCloseClicked()
    {
        CloseMinigame();
    }

    // ==================== PUBLIC METODLAR ====================

    /// <summary>
    /// Minigame UI'ını açar. Dışarıdan çağrılır (örn: ana menü butonu, trigger zone, vs.)
    /// </summary>
    public void OpenMinigame()
    {
        gameObject.SetActive(true);
        ShowToolSelection();
    }

    /// <summary>
    /// Minigame UI'ını kapatır.
    /// </summary>
    public void CloseMinigame()
    {
        gameObject.SetActive(false);
    }

    // ==================== UTILITY ====================

    private string FormatTime(float seconds)
    {
        if (seconds <= 0) return "0:00";

        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{mins}:{secs:D2}";
    }
}