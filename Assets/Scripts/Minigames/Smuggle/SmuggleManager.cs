using System;
using System.Collections.Generic;
using UnityEngine;

public class SmuggleManager : MonoBehaviour
{
    public static SmuggleManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData;
    public SmuggleDatabase database;

    [Header("Operasyon Ayarları")]
    public float eventCheckInterval = 15f;
    public float eventDecisionTime = 10f;

    [Header("Şüphe Ayarları")]
    public int jewelersForFullReduction = 3; //bu kadar kuyumcu sabit şüpheyi tamamen sıfırlar
    
    [Header("Debug")]
    [SerializeField] private bool bypassUnlockCheck = false;

    private SmuggleState currentState = SmuggleState.Idle;
    private SmuggleRoutePack currentRoutePack;
    private SmuggleRoute selectedRoute;
    private SmuggleCourier selectedCourier;

    private List<SmuggleCourier> currentCourierOptions = new List<SmuggleCourier>();

    private float operationDuration;
    private float operationTimer;
    private float eventCheckTimer;

    private SmuggleEvent currentEvent;
    private float eventDecisionTimer;
    private List<SmuggleEvent> triggeredEvents = new List<SmuggleEvent>();
    private List<SmuggleEvent> activeEventPool;

    private float accumulatedSuspicionModifier;
    private int accumulatedCostModifier;

    private float pendingFailureTimer;

    public static event Action<SmuggleRoutePack, List<SmuggleCourier>> OnSelectionPhaseStarted;
    public static event Action<SmuggleRoute, SmuggleCourier, float> OnOperationStarted;
    public static event Action<float> OnOperationProgress;
    public static event Action<SmuggleEvent> OnSmuggleEventTriggered;
    public static event Action<float> OnEventDecisionTimerUpdate;
    public static event Action<SmuggleEventChoice> OnSmuggleEventResolved;
    public static event Action<SmuggleResult> OnOperationCompleted;
    public static event Action<string> OnSmuggleFailed;
    public static event Action<float> OnOperationCancelled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (currentState == SmuggleState.InProgress)
        {
            operationTimer += Time.deltaTime;

            float progress = Mathf.Clamp01(operationTimer / operationDuration);
            OnOperationProgress?.Invoke(progress);

            if (pendingFailureTimer > 0f)
            {
                pendingFailureTimer -= Time.deltaTime;
                if (pendingFailureTimer <= 0f)
                {
                    FailOperation();
                    return;
                }
            }

            eventCheckTimer += Time.deltaTime;
            if (eventCheckTimer >= eventCheckInterval)
            {
                eventCheckTimer = 0f;
                TryTriggerEvent();

                if (currentState != SmuggleState.InProgress) return;
            }

            if (operationTimer >= operationDuration)
            {
                CalculateResult();
            }
        }
        else if (currentState == SmuggleState.EventPhase)
        {
            //event karar sayacını geri say (oyun duraklatılmış, unscaledDeltaTime kullanılır)
            eventDecisionTimer -= Time.unscaledDeltaTime;
            OnEventDecisionTimerUpdate?.Invoke(eventDecisionTimer);

            if (eventDecisionTimer <= 0f)
            {
                ResolveEvent(0);
            }
        }
    }

    public bool TryStartMinigame()
    {
        if (!bypassUnlockCheck)
        {
            if (MinigameManager.Instance == null || !MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            {
                OnSmuggleFailed?.Invoke("Minigame is not unlocked yet.");
                return false;
            }

            if (MinigameManager.Instance.IsOnCooldown(minigameData))
            {
                float remaining = MinigameManager.Instance.GetRemainingCooldown(minigameData);
                OnSmuggleFailed?.Invoke("Cooldown active. Remaining: " + Mathf.CeilToInt(remaining) + "s");
                return false;
            }
        }

        if (currentState != SmuggleState.Idle)
        {
            OnSmuggleFailed?.Invoke("An operation is already in progress.");
            return false;
        }

        if (currentRoutePack != null && currentCourierOptions.Count > 0)
        {
            currentState = SmuggleState.SelectingRoute;
            if (GameManager.Instance != null)
                GameManager.Instance.PauseGame();
            OnSelectionPhaseStarted?.Invoke(currentRoutePack, currentCourierOptions);
            return true;
        }

        StartSelectionPhase();
        return true;
    }

    private void StartSelectionPhase()
    {
        currentState = SmuggleState.SelectingRoute;

        if (database == null)
        {
            Debug.LogError("SmuggleManager: database is NULL! Assign it in the Inspector.");
            OnSmuggleFailed?.Invoke("Database not configured.");
            currentState = SmuggleState.Idle;
            return;
        }

        if (database.routePacks == null || database.routePacks.Count == 0)
        {
            Debug.LogError("SmuggleManager: database has no route packs!");
            OnSmuggleFailed?.Invoke("No route packs available.");
            currentState = SmuggleState.Idle;
            return;
        }

        int packIndex = UnityEngine.Random.Range(0, database.routePacks.Count);
        currentRoutePack = database.routePacks[packIndex];

        if (currentRoutePack.routes == null || currentRoutePack.routes.Count == 0)
        {
            Debug.LogError($"SmuggleManager: Selected route pack '{currentRoutePack.name}' has no routes!");
            OnSmuggleFailed?.Invoke("Route pack is empty.");
            currentState = SmuggleState.Idle;
            return;
        }

        currentCourierOptions.Clear();
        
        if (database.couriers == null || database.couriers.Count == 0)
        {
            Debug.LogError("SmuggleManager: database has no couriers!");
            OnSmuggleFailed?.Invoke("No couriers available.");
            currentState = SmuggleState.Idle;
            return;
        }
        
        Debug.Log($"SmuggleManager: Total couriers in database: {database.couriers.Count}");
        
        List<SmuggleCourier> availableCouriers = new List<SmuggleCourier>(database.couriers);
        int courierCount = Mathf.Min(3, availableCouriers.Count);
        
        Debug.Log($"SmuggleManager: Will select {courierCount} couriers");
        
        for (int i = 0; i < courierCount; i++)
        {
            int idx = UnityEngine.Random.Range(0, availableCouriers.Count);
            currentCourierOptions.Add(availableCouriers[idx]);
            Debug.Log($"SmuggleManager: Selected courier #{i}: {availableCouriers[idx].name}");
            availableCouriers.RemoveAt(idx);
        }

        Debug.Log($"SmuggleManager: Final courier count in currentCourierOptions: {currentCourierOptions.Count}");

    //oyunu duraklat — seçim ekranında zaman durmalı
    if (GameManager.Instance != null)
        GameManager.Instance.PauseGame();

    OnSelectionPhaseStarted?.Invoke(currentRoutePack, currentCourierOptions);
}

    public void SelectRoute(SmuggleRoute route)
    {
        if (currentState != SmuggleState.SelectingRoute) return;

        selectedRoute = route;
        currentState = SmuggleState.SelectingCourier;
    }

    public void SelectCourier(SmuggleCourier courier)
    {
        if (currentState != SmuggleState.SelectingCourier) return;

        int totalCost = selectedRoute.cost + courier.cost;
        if (GameStatManager.Instance == null || !GameStatManager.Instance.HasEnoughWealth(totalCost))
        {
            OnSmuggleFailed?.Invoke("Not enough funds. Required: " + totalCost);
            return;
        }

        GameStatManager.Instance.AddWealth(-totalCost);

        selectedCourier = courier;
        StartOperation();
    }

    private void StartOperation()
    {
        currentState = SmuggleState.InProgress;

        //oyunu devam ettir — seçim tamamlandı, operasyon başlıyor
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //operasyon süresini hesapla: mesafe / (kurye hızı çarpanı)
        //speed 0 → distance saniye, speed 50 → distance/6, speed 100 → distance/11
        float speedFactor = selectedCourier.speed * 0.1f + 1f;
        operationDuration = selectedRoute.distance / speedFactor;

        operationTimer = 0f;
        eventCheckTimer = 0f;

        accumulatedSuspicionModifier = 0f;
        accumulatedCostModifier = 0;
        pendingFailureTimer = 0f;
        triggeredEvents.Clear();
        currentEvent = null;
        activeEventPool = database.events;

        OnOperationStarted?.Invoke(selectedRoute, selectedCourier, operationDuration);
    }

    private void TryTriggerEvent()
    {
        if (activeEventPool == null || activeEventPool.Count == 0) return;

        List<SmuggleEvent> available = new List<SmuggleEvent>();
        for (int i = 0; i < activeEventPool.Count; i++)
        {
            if (!triggeredEvents.Contains(activeEventPool[i]))
                available.Add(activeEventPool[i]);
        }

        if (available.Count == 0) return;

        int idx = UnityEngine.Random.Range(0, available.Count);
        SmuggleEvent candidate = available[idx];

        float triggerChance = GetTriggerChance(candidate.triggerType);

        float roll = UnityEngine.Random.Range(0f, 100f);
        if (roll > triggerChance) return;

        //EventCoordinator cooldown kontrolü — başka bir event az önce geldiyse ertele
        if (!EventCoordinator.CanShowEvent()) return;

        //event tetiklendi
        currentEvent = candidate;
        triggeredEvents.Add(currentEvent);

        EventCoordinator.MarkEventShown();

        //operasyonu duraklat, event fazına geç
        currentState = SmuggleState.EventPhase;
        eventDecisionTimer = eventDecisionTime;

        //oyunu duraklat — event karar ekranında zaman durmalı
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnSmuggleEventTriggered?.Invoke(currentEvent);
    }

    private float GetTriggerChance(SmuggleEventTrigger triggerType)
    {
        switch (triggerType)
        {
            case SmuggleEventTrigger.Risk:
                return selectedRoute.riskLevel * (1f - selectedCourier.reliability / 200f);
            case SmuggleEventTrigger.Betrayal:
                return selectedCourier.betrayalChance * 100f;
            case SmuggleEventTrigger.Incompetence:
                return 100f - selectedCourier.reliability;
            default:
                return 0f;
        }
    }

    public void ResolveEvent(int choiceIndex)
    {
        if (currentState != SmuggleState.EventPhase || currentEvent == null) return;

        if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Count) return;

        SmuggleEventChoice choice = currentEvent.choices[choiceIndex];

        accumulatedSuspicionModifier += choice.suspicionModifier;
        accumulatedCostModifier += choice.costModifier;

        activeEventPool = (choice.nextEventPool != null && choice.nextEventPool.Count > 0)
            ? choice.nextEventPool
            : null;

        OnSmuggleEventResolved?.Invoke(choice);

        //oyunu devam ettir — event karar ekranı kapandı
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //bu seçim operasyonu anında başarısız yapar mı (yakalanma, ihanet vs.)
        if (choice.causesFailure)
        {
            FailOperation();
            return;
        }

        if (choice.failureDelay > 0f)
        {
            float remainingTime = operationDuration - operationTimer;
            pendingFailureTimer = Mathf.Min(choice.failureDelay, Mathf.Max(remainingTime - 1f, 0.1f));
        }

        currentEvent = null;
        currentState = SmuggleState.InProgress;
    }

    private void CalculateResult()
    {
        SmuggleResult result = new SmuggleResult();
        result.success = true;
        result.route = selectedRoute;
        result.courier = selectedCourier;

        result.wealthChange = currentRoutePack.baseReward - accumulatedCostModifier;

        //sabit şüphe: kuyumcu sayısına göre azalır, 3+ kuyumcuda sıfırlanır
        float baseSuspicion = selectedRoute.riskLevel * 0.1f;
        if (SkillTreeManager.Instance != null)
        {
            int jewelers = SkillTreeManager.Instance.GetJewelerCount();
            if (jewelers >= jewelersForFullReduction)
                baseSuspicion = 0f;
            else
                baseSuspicion *= 1f - ((float)jewelers / jewelersForFullReduction);
        }
        result.suspicionChange = baseSuspicion + accumulatedSuspicionModifier;

        if (GameStatManager.Instance != null)
        {
            GameStatManager.Instance.AddWealth(result.wealthChange);
            GameStatManager.Instance.AddSuspicion(result.suspicionChange);
        }

        if (MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        ClearSelectionData();
        currentState = SmuggleState.Idle;

        OnOperationCompleted?.Invoke(result);
    }

    private void FailOperation()
    {
        SmuggleResult result = new SmuggleResult();
        result.success = false;
        result.route = selectedRoute;
        result.courier = selectedCourier;

        result.wealthChange = -accumulatedCostModifier;
        result.suspicionChange = selectedRoute.riskLevel * 0.3f + accumulatedSuspicionModifier;

        if (GameStatManager.Instance != null)
        {
            GameStatManager.Instance.AddWealth(result.wealthChange);
            GameStatManager.Instance.AddSuspicion(result.suspicionChange);
        }

        if (MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        currentEvent = null;
        ClearSelectionData();
        currentState = SmuggleState.Idle;

        OnOperationCompleted?.Invoke(result);
    }

    public void CancelSelection()
    {
        if (currentState != SmuggleState.SelectingRoute && currentState != SmuggleState.SelectingCourier) return;

        currentState = SmuggleState.Idle;

        //oyunu devam ettir — seçim iptal edildi
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();
    }

    public void CancelOperation()
    {
        if (currentState != SmuggleState.InProgress && currentState != SmuggleState.EventPhase) return;

        float progress = Mathf.Clamp01(operationTimer / operationDuration);
        float remaining = 1f - progress;

        int totalCost = selectedRoute.cost + selectedCourier.cost;
        float refundAmount = totalCost * (remaining / 2f);

        if (GameStatManager.Instance != null && refundAmount > 0f)
        {
            GameStatManager.Instance.AddWealth(refundAmount);
        }

        currentEvent = null;
        ClearSelectionData();
        currentState = SmuggleState.Idle;

        OnOperationCancelled?.Invoke(refundAmount);
    }

    public bool CanPlay()
    {
        if (MinigameManager.Instance == null || !MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            return false;
        if (MinigameManager.Instance.IsOnCooldown(minigameData))
            return false;
        if (currentState != SmuggleState.Idle)
            return false;
        return true;
    }

    public float GetOperationProgress()
    {
        if (currentState != SmuggleState.InProgress && currentState != SmuggleState.EventPhase)
            return 0f;
        return Mathf.Clamp01(operationTimer / operationDuration);
    }

    public SmuggleState GetCurrentState()
    {
        return currentState;
    }

    private void ClearSelectionData()
    {
        currentRoutePack = null;
        selectedRoute = null;
        selectedCourier = null;
        currentCourierOptions.Clear();
    }
}

public enum SmuggleState
{
    Idle,
    SelectingRoute,
    SelectingCourier,
    InProgress,
    EventPhase
}

[System.Serializable]
public class SmuggleResult
{
    public bool success;
    public SmuggleRoute route;
    public SmuggleCourier courier;
    public float wealthChange;
    public float suspicionChange;
}