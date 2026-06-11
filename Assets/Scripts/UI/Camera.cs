using UnityEngine;
using UnityEngine.InputSystem;

public class MapController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer mapRenderer;
    public MapGenerator mapGenerator; // assign in inspector

    [Header("Zoom Settings")]
    public float zoomSpeed = 1.2f;
    public float minSize = 2f;
    private float maxSize;

    private Camera cam;
    private Vector3 dragOrigin;
    public bool enable = true;
    private bool mapReady = false;

    void Awake()
    {
        cam = GetComponent<Camera>();
    
        if (mapGenerator != null)
        {
            mapGenerator.OnMapGenerated += OnMapReady;
        }
    }

    void Start()
    {
        // Subscribe to map generated event
        if (mapGenerator != null)
        {
            mapGenerator.OnMapGenerated += OnMapReady;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (mapGenerator != null)
        {
            mapGenerator.OnMapGenerated -= OnMapReady;
        }
    }

    void OnMapReady()
    {
        if (mapRenderer != null && mapRenderer.sprite != null)
        {
            CalculateMaxZoom();
            CenterCamera();
            mapReady = true;
        }
    }

    void CalculateMaxZoom()
    {
        float mapHeight = mapRenderer.bounds.size.y / 2f;
        float mapWidthSize = (mapRenderer.bounds.size.x / 2f) / cam.aspect;
        maxSize = Mathf.Min(mapHeight, mapWidthSize);
        
        cam.orthographicSize = maxSize;
    }

    void CenterCamera()
    {
        Vector3 mapCenter = mapRenderer.bounds.center;
        transform.position = new Vector3(mapCenter.x, mapCenter.y, transform.position.z);
    }

    void LateUpdate()
    {
        if (!mapReady || mapRenderer == null) return;

        if (enable)
        {
            HandleZoom();
            HandlePan();
        }

        transform.position = ClampCamera(transform.position);
    }

    void HandlePan()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragOrigin = GetMouseWorldPosition();
        }

        if (Mouse.current.leftButton.isPressed)
        {
            Vector3 difference = dragOrigin - GetMouseWorldPosition();
            transform.position += difference;
        }
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0) return;

        Vector3 mouseBefore = GetMouseWorldPosition();

        float newSize = cam.orthographicSize - (scroll * zoomSpeed * 0.01f);
        cam.orthographicSize = Mathf.Clamp(newSize, minSize, maxSize);

        Vector3 mouseAfter = GetMouseWorldPosition();
        transform.position += (mouseBefore - mouseAfter);
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
    
        mousePos.x = Mathf.Clamp(mousePos.x, 0, Screen.width);
        mousePos.y = Mathf.Clamp(mousePos.y, 0, Screen.height);
    
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, Mathf.Abs(cam.transform.position.z)));
        return worldPos;
    }

    Vector3 ClampCamera(Vector3 targetPosition)
    {
        float camHeight = cam.orthographicSize;
        float camWidth = cam.orthographicSize * cam.aspect;

        float minX = mapRenderer.bounds.min.x + camWidth;
        float maxX = mapRenderer.bounds.max.x - camWidth;
        float minY = mapRenderer.bounds.min.y + camHeight;
        float maxY = mapRenderer.bounds.max.y - camHeight;

        float newX = Mathf.Clamp(targetPosition.x, minX, maxX);
        float newY = Mathf.Clamp(targetPosition.y, minY, maxY);

        return new Vector3(newX, newY, targetPosition.z);
    }
}