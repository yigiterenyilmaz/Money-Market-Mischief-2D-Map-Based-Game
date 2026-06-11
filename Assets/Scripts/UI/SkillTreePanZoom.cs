using UnityEngine;
using UnityEngine.EventSystems;

public class TreePanZoom : MonoBehaviour, IDragHandler, IScrollHandler
{
    public RectTransform treeContent;
    public RectTransform viewArea;
    
    public float zoomSpeed = 0.1f;
    public float minZoom = 0.5f;
    public float maxZoom = 2f;
    
    private float currentZoom = 1f;

    public void OnDrag(PointerEventData eventData)
    {
        treeContent.anchoredPosition += eventData.delta / Mathf.Sqrt(currentZoom)  ;
    }

    public void OnScroll(PointerEventData eventData)
    {
        float scroll = eventData.scrollDelta.y;
        float newZoom = Mathf.Clamp(currentZoom + scroll * zoomSpeed, minZoom, maxZoom);
        
        currentZoom = newZoom;
        treeContent.localScale = Vector3.one * currentZoom;
    }
}