using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PleasePaperUI : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private PleasePaperEvent testOffer;
    
    [Header("Offer Popup")]
    [SerializeField] private GameObject offerPopup;
    [SerializeField] private TextMeshProUGUI offerTitleText;
    [SerializeField] private TextMeshProUGUI offerDescriptionText;
    [SerializeField] private TextMeshProUGUI offerTimerText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    [Header("Result Popup")]
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI resultDescriptionText;
    [SerializeField] private Button dismissButton;

    public void DebugShowSuccessResult()
    {
        ShowSuccessResult(0.75f);
    }

    public void DebugShowFailureResult()
    {
        ShowFailureResult("Debug: Operation failed.");
    }
    
    
    public void DebugTriggerOffer()
    {
        PleasePaperManager.Instance.DebugForceOffer();
        Debug.Log("aaaaaaaaaaaaaaaaaa");
    }
    
    private void OnEnable()
    {
        PleasePaperManager.OnOfferReceived += ShowOfferPopup;
        PleasePaperManager.OnOfferDecisionTimerUpdate += UpdateOfferTimer;
        PleasePaperManager.OnBargainingStarted += ShowSuccessResult;
        PleasePaperManager.OnGameOver += ShowFailureResult;
    }

    private void OnDisable()
    {
        PleasePaperManager.OnOfferReceived -= ShowOfferPopup;
        PleasePaperManager.OnOfferDecisionTimerUpdate -= UpdateOfferTimer;
        PleasePaperManager.OnBargainingStarted -= ShowSuccessResult;
        PleasePaperManager.OnGameOver -= ShowFailureResult;
    }

    private void Start()
    {
        acceptButton.onClick.AddListener(OnAcceptClicked);
        rejectButton.onClick.AddListener(OnRejectClicked);
        dismissButton.onClick.AddListener(OnDismissClicked);

        offerPopup.SetActive(false);
        resultPopup.SetActive(false);
    }

    private void ShowOfferPopup(PleasePaperEvent offer)
    {
        offerTitleText.text = offer.displayName;
        offerDescriptionText.text = offer.description;
        offerPopup.SetActive(true);
    }

    private void UpdateOfferTimer(float timeRemaining)
    {
        offerTimerText.text = Mathf.CeilToInt(timeRemaining).ToString();
    }

    private void HideOfferPopup()
    {
        offerPopup.SetActive(false);
    }

    private void OnAcceptClicked()
    {
        HideOfferPopup();
        PleasePaperManager.Instance.AcceptOffer();
    }

    private void OnRejectClicked()
    {
        HideOfferPopup();
        PleasePaperManager.Instance.RejectOffer();
    }

    private void ShowSuccessResult(float bargainingPower)
    {
        int rewardPercent = Mathf.RoundToInt((0.5f + bargainingPower * 0.5f) * 100f);
        resultTitleText.text = "Operation Successful";
        resultDescriptionText.text = $"You secured {rewardPercent}% of the gold reserves.";
        resultPopup.SetActive(true);
    }

    private void ShowFailureResult(string reason)
    {
        resultTitleText.text = "Operation Failed";
        resultDescriptionText.text = reason;
        resultPopup.SetActive(true);
    }

    private void OnDismissClicked()
    {
        resultPopup.SetActive(false);
        PleasePaperManager.Instance.DismissResultScreen();
    }
}
