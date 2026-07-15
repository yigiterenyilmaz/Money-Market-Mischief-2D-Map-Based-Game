using UnityEngine;
using UnityEngine.InputSystem;

public class MapController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer mapRenderer;
    public MapGenerator mapGenerator; // assign in inspector

    [Header("Zoom Settings")]
    public float zoomSpeed = 1.2f;
    [Tooltip("Zoom yumuşatma hızı: büyük değer = hedefe daha çabuk oturur, küçük değer = daha yumuşak/uzun süzülme.")]
    public float zoomSmoothing = 10f;
    public float minSize = 2f;
    private float maxSize;
    private float targetSize;

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
        targetSize = maxSize;
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

        ApplySmoothZoom();

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

        // Çarpımsal (üstel) zoom: her scroll adımı hedef boyutu sabit bir ORANLA değiştirir,
        // böylece zoom hissi her ölçekte aynıdır. Girdi yalnızca HEDEFİ günceller; kamera
        // hedefe ApplySmoothZoom içinde kademesiz süzülür — böylece sprite LOD/detay
        // geçişleri tek karede "pat" diye olmaz.
        float zoomFactor = Mathf.Exp(-scroll * zoomSpeed * 0.001f);
        targetSize = Mathf.Clamp(targetSize * zoomFactor, minSize, maxSize);
    }

    void ApplySmoothZoom()
    {
        if (Mathf.Approximately(cam.orthographicSize, targetSize)) return;

        Vector3 mouseBefore = GetMouseWorldPosition();

        // Kare hızından bağımsız üstel yumuşatma: her karede kalan mesafenin sabit bir
        // oranını kapatır, hedefe yaklaştıkça doğal olarak yavaşlar.
        float t = 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);
        float newSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);

        // Hedefe çok yaklaşınca sonsuz küçük adımlarla sürünmesin diye kilitle.
        if (Mathf.Abs(newSize - targetSize) < 0.001f)
            newSize = targetSize;

        cam.orthographicSize = newSize;

        // Zoom animasyonu boyunca imlecin altındaki dünya noktasını sabit tut.
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