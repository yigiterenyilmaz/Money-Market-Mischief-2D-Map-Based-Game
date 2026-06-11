using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SmuggleSelectionUI : MonoBehaviour
{
    public static SmuggleSelectionUI Instance { get; private set; }

    [Header("Main Panel")]
    public GameObject selectionPanel;

    [Header("Containers")]
    public Transform routesContainer;
    public Transform couriersContainer;

    [Header("Prefabs")]
    public GameObject routeCardPrefab;
    public GameObject courierCardPrefab;

    [Header("UI Text")]
    public TextMeshProUGUI baseRewardText;
    public TextMeshProUGUI totalCostText;

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    // Current data
    private SmuggleRoutePack currentRoutePack;
    private List<SmuggleCourier> currentCouriers = new List<SmuggleCourier>();

    // Current selections
    private SmuggleRoute selectedRoute;
    private SmuggleCourier selectedCourier;

    // Card references
    private List<RouteCardUI> routeCards = new List<RouteCardUI>();
    private List<CourierCardUI> courierCards = new List<CourierCardUI>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Setup buttons
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
            confirmButton.interactable = false;
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        // Check layout components on containers
        if (routesContainer != null)
        {
            var layoutGroup = routesContainer.GetComponent<LayoutGroup>();
            Debug.Log($"routesContainer has LayoutGroup: {layoutGroup != null} (Type: {layoutGroup?.GetType().Name ?? "none"})");
        }
        
        if (couriersContainer != null)
        {
            var layoutGroup = couriersContainer.GetComponent<LayoutGroup>();
            Debug.Log($"couriersContainer has LayoutGroup: {layoutGroup != null} (Type: {layoutGroup?.GetType().Name ?? "none"})");
        }

        // Subscribe to SmuggleManager events
        SmuggleManager.OnSelectionPhaseStarted += OnSelectionPhaseStarted;

        // Hide panel initially
        if (selectionPanel != null)
            selectionPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        SmuggleManager.OnSelectionPhaseStarted -= OnSelectionPhaseStarted;
    }

    private void OnSelectionPhaseStarted(SmuggleRoutePack routePack, List<SmuggleCourier> couriers)
    {
        UImanager.Instance.ToggleUI();
        currentRoutePack = routePack;
        currentCouriers = couriers;
        selectedRoute = null;
        selectedCourier = null;

        // Clear old cards
        ClearCards();

        // Create route cards
        if (routePack != null && routePack.routes != null)
        {
            foreach (SmuggleRoute route in routePack.routes)
            {
                CreateRouteCard(route);
            }
        }

        // Create courier cards
        if (couriers != null)
        {
            foreach (SmuggleCourier courier in couriers)
            {
                CreateCourierCard(courier);
            }
        }

        // Update UI
        if (baseRewardText != null)
            baseRewardText.text = $"Base Reward: ${routePack.baseReward}";

        UpdateTotalCost();

        // Show panel
        if (selectionPanel != null)
            selectionPanel.SetActive(true);
    }

    private void CreateRouteCard(SmuggleRoute route)
    {
        if (routeCardPrefab == null || routesContainer == null) return;

        GameObject cardObj = Instantiate(routeCardPrefab, routesContainer);
        Debug.Log($"ROUTE CARD: Created {cardObj.name}, parent: {routesContainer.name}, sibling index: {cardObj.transform.GetSiblingIndex()}");

        RouteCardUI card = cardObj.GetComponent<RouteCardUI>();
        if (card == null)
        {
            card = cardObj.AddComponent<RouteCardUI>();
        }

        card.Initialize(route);
        routeCards.Add(card);

        Button btn = cardObj.GetComponent<Button>();
        if (btn == null)
            btn = cardObj.AddComponent<Button>();

        EnsureButtonHasTargetGraphic(btn, cardObj);

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => card.OnCardClicked());
        
        Debug.Log($"ROUTE CARD: Position: {cardObj.GetComponent<RectTransform>().anchoredPosition}");
    }

    private void CreateCourierCard(SmuggleCourier courier)
    {
        if (courierCardPrefab == null || couriersContainer == null) return;

        try
        {
            GameObject cardObj = Instantiate(courierCardPrefab, couriersContainer);
            Debug.Log($"COURIER CARD: Created {cardObj.name}, parent: {couriersContainer.name}, sibling index: {cardObj.transform.GetSiblingIndex()}");

            CourierCardUI card = cardObj.GetComponent<CourierCardUI>();
            if (card == null)
            {
                Debug.Log("COURIER CARD: Adding CourierCardUI component");
                card = cardObj.AddComponent<CourierCardUI>();
            }

            Debug.Log($"COURIER CARD: Calling Initialize for {courier.name}");
            card.Initialize(courier);
            Debug.Log("COURIER CARD: Initialize complete");
            
            courierCards.Add(card);

            Button btn = cardObj.GetComponent<Button>();
            if (btn == null)
                btn = cardObj.AddComponent<Button>();

            EnsureButtonHasTargetGraphic(btn, cardObj);

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => card.OnCardClicked());
            
            Debug.Log($"COURIER CARD: Position: {cardObj.GetComponent<RectTransform>().anchoredPosition}");
            Debug.Log($"COURIER CARD: ✓ Completed successfully for {courier.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"COURIER CARD ERROR: {e.Message}\n{e.StackTrace}");
        }
    }

    public void SelectRoute(SmuggleRoute route)
    {
        selectedRoute = route;

        // Update visual selection
        foreach (RouteCardUI card in routeCards)
        {
            card.SetSelected(card.Route == route);
        }

        UpdateTotalCost();
        UpdateConfirmButton();
    }

    public void SelectCourier(SmuggleCourier courier)
    {
        selectedCourier = courier;

        // Update visual selection
        foreach (CourierCardUI card in courierCards)
        {
            card.SetSelected(card.Courier == courier);
        }

        UpdateTotalCost();
        UpdateConfirmButton();
    }

    private void UpdateTotalCost()
    {
        int totalCost = 0;

        if (selectedRoute != null)
            totalCost += selectedRoute.cost;

        if (selectedCourier != null)
            totalCost += selectedCourier.cost;

        if (totalCostText != null)
            totalCostText.text = $"Total Cost: ${totalCost}";
    }

    private void UpdateConfirmButton()
    {
        if (confirmButton != null)
            confirmButton.interactable = (selectedRoute != null && selectedCourier != null);
    }

    private void OnConfirmClicked()
    {
        if (selectedRoute == null || selectedCourier == null) return;

        // Tell SmuggleManager about selections
        if (SmuggleManager.Instance != null)
        {
            SmuggleManager.Instance.SelectRoute(selectedRoute);
            SmuggleManager.Instance.SelectCourier(selectedCourier);
        }

        // Hide panel
        if (selectionPanel != null)
            selectionPanel.SetActive(false);
        UImanager.Instance.ToggleUI();
    }

    private void OnCancelClicked()
    {
        if (SmuggleManager.Instance != null)
        {
            SmuggleManager.Instance.CancelSelection();
        }

        if (selectionPanel != null)
            selectionPanel.SetActive(false);
        UImanager.Instance.ToggleUI();
    }

    private void ClearCards()
    {
        // Destroy route cards
        foreach (RouteCardUI card in routeCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        routeCards.Clear();

        // Destroy courier cards
        foreach (CourierCardUI card in courierCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        courierCards.Clear();
    }

    /// <summary>
    /// Button'un tıklanabilmesi için targetGraphic ve raycastTarget ayarlar.
    /// </summary>
    private void EnsureButtonHasTargetGraphic(Button btn, GameObject cardObj)
    {
        Image img = cardObj.GetComponent<Image>();

        if (img == null)
            img = cardObj.GetComponentInChildren<Image>();

        if (img == null)
        {
            img = cardObj.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
        }

        img.raycastTarget = true;
        btn.targetGraphic = img;
    }

    public void OpenSelectionMenu()
    {
        if (SmuggleManager.Instance == null)
        {
            Debug.LogError("SmuggleManager.Instance is null!");
            return;
        }

        Debug.Log($"Calling TryStartMinigame. State: {SmuggleManager.Instance.GetCurrentState()}");
        SmuggleManager.Instance.TryStartMinigame();
    }
}