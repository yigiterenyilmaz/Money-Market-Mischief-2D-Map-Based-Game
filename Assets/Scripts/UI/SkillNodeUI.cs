using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// We use interfaces instead of a Button component
public class SkillNodeUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Image iconImage;
    public GameObject lockOverlay;
    
    [Header("Settings")]
    public float hoverScale = 1.1f;
    public bool isSmuggleSkill = false;
    public bool isUnlocked;
    
    [Header("Minigame Link")]
    public MiniGameData linkedMinigame;  // <-- ADD THIS, assign in Inspector
    
    private System.Action onClick;
    
    private void Start()
    {
        // If this node is marked as unlocked and has a linked minigame, unlock it
        if (isUnlocked && linkedMinigame != null)
        {
            MinigameManager.Instance?.UnlockMinigame(linkedMinigame);
        }
    }

    public void Setup(Sprite icon, bool unlocked, System.Action onClickCallback)
    {
        iconImage.sprite = icon;
        lockOverlay.SetActive(!unlocked);
        
        this.isUnlocked = unlocked;
        this.onClick = onClickCallback;

        // Ensure the Image is set to detect clicks
        iconImage.raycastTarget = true;
    }

    // This is the "OnClick" function now
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isUnlocked) return;

        Debug.Log($"Node {gameObject.name} Clicked!");
        onClick?.Invoke();
    }

    // Visual feedback since we don't have a Button's "Hover" state
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnlocked) transform.localScale = Vector3.one * hoverScale;
        
     
           
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = Vector3.one;
        
       
            
    }
}